using System.Text;
using Core.Interfaces;
using Core.Models;
using Infrastructure;
using Infrastructure.Services;

// ═══════════════════════════════════════════════════════════════════════════════
// M3U List Editor - Clean your playlists by removing dead links
// ═══════════════════════════════════════════════════════════════════════════════

var cli = new CliHandler(args);

if (cli.ShowHelp)
{
    cli.PrintHelp();
    return;
}

// Setup cancellation
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = false; // Don't prevent the default behavior - let it terminate
    cts.Cancel();
    cli.PrintWarning("\n⚠ Cancelling...");
    Environment.Exit(130); // Standard exit code for Ctrl+C
};

try
{
    // Parse command line arguments FIRST (before creating HttpClient)
    var parsedArgs = cli.ParseArguments();

    // Create HttpClient with timeout configured BEFORE any requests
    using var httpClient = new HttpClient
    {
        Timeout = parsedArgs.Timeout ?? AppSettings.DefaultTimeout
    };

    var fileHandler = new FileHandler();
    var fileDownloader = new FileDownloader(httpClient, fileHandler);

    // Now resolve the actual settings (may download if URL)
    var settings = await cli.ResolveSettingsAsync(parsedArgs, fileDownloader, cts.Token);

    if (settings is null)
    {
        Environment.Exit(1);
        return;
    }

    // Build services
    var signalTester = new SignalTester(httpClient);
    var parser = new M3uParser();
    var duplicateRemover = new DuplicateRemover();
    var channelFilter = new ChannelFilter(signalTester, settings.MaxConcurrency);

    var engine = new M3uCleanerEngine(parser, channelFilter, duplicateRemover, fileHandler);

    // Setup progress reporting with visual progress bar
    var lastPercent = -1;
    var progress = new Progress<ProgressReportModel>(report =>
    {
        if (report.PercentageCompleted == lastPercent) return;
        lastPercent = report.PercentageCompleted;
        cli.PrintProgress(report);
    });

    cli.PrintHeader();
    cli.PrintSettings(settings);
    Console.WriteLine();

    // Process the M3U file
    var report = await engine.ProcessAsync(settings, progress, cts.Token);

    // Display results
    cli.PrintResults(report, settings);
}
catch (OperationCanceledException)
{
    cli.PrintError("\n✗ Operation cancelled by user.");
    Environment.Exit(1);
}
catch (FileNotFoundException ex)
{
    cli.PrintError($"\n✗ File not found: {ex.FileName}");
    Environment.Exit(1);
}
catch (Exception ex)
{
    cli.PrintError($"\n✗ Error: {ex.Message}");
    if (cli.Verbose)
    {
        Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
    }
    Environment.Exit(1);
}

// ═══════════════════════════════════════════════════════════════════════════════
// CLI Handler - Encapsulates all CLI presentation logic
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed class CliHandler
{
    private readonly string[] _args;
    private static readonly bool SupportsColor = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    public bool ShowHelp { get; }
    public bool Verbose { get; }

    public CliHandler(string[] args)
    {
        _args = args;
        ShowHelp = args.Any(a => a is "-h" or "--help" or "-?" or "help");
        Verbose = args.Any(a => a is "-v" or "--verbose");
    }

    public void PrintHelp()
    {
        var help = """

            ╔═══════════════════════════════════════════════════════════════════════╗
            ║                       M3U LIST EDITOR v2.0                            ║
            ╠═══════════════════════════════════════════════════════════════════════╣
            ║  Clean your M3U playlists by removing dead links and duplicates       ║
            ╚═══════════════════════════════════════════════════════════════════════╝

            USAGE:
              m3u-editor [options]
              m3u-editor -src <file|url> [-dest <file>] [options]

            OPTIONS:
              -src, --source <path>       Source M3U file path or URL
              -dest, --destination <path> Output file path (default: <source>-Cleaned.m3u)
              -timeout, --to <seconds>    Connection timeout in seconds (default: 10)
              -c, --concurrency <n>       Parallel connections (default: 10, max: 50)
              -rd, --removedoubles <bool> Remove duplicates (default: true)
              -v, --verbose               Show detailed error information
              -h, --help                  Show this help message

            EXAMPLES:
              m3u-editor                              # Interactive mode
              m3u-editor -src playlist.m3u            # Clean local file
              m3u-editor -src https://example.com/list.m3u -dest clean.m3u
              m3u-editor -src list.m3u -c 20 -timeout 5

            """;
        Console.WriteLine(help);
    }

    public void PrintHeader()
    {
        Console.Clear();
        PrintColored("╔════════════════════════════════════════╗\n", ConsoleColor.Cyan);
        PrintColored("║        M3U LIST EDITOR v2.0            ║\n", ConsoleColor.Cyan);
        PrintColored("╚════════════════════════════════════════╝\n", ConsoleColor.Cyan);
        Console.WriteLine();
    }

    public void PrintSettings(AppSettings settings)
    {
        PrintColored("  CONFIGURATION\n", ConsoleColor.White);
        PrintColored("  ─────────────────────────────────────\n", ConsoleColor.DarkGray);

        // Source
        PrintColored("  Source:      ", ConsoleColor.DarkGray);
        if (settings.IsLinkSourcePath)
        {
            PrintColored("[URL] ", ConsoleColor.Magenta);
        }
        Console.WriteLine(Path.GetFileName(settings.SourcePath));

        // Destination
        PrintColored("  Destination: ", ConsoleColor.DarkGray);
        Console.WriteLine(settings.ExportPath);

        // Settings row
        PrintColored("  ─────────────────────────────────────\n", ConsoleColor.DarkGray);
        PrintColored("  Timeout: ", ConsoleColor.DarkGray);
        Console.Write($"{(settings.Timeout ?? AppSettings.DefaultTimeout).TotalSeconds}s");
        PrintColored("  │  Concurrency: ", ConsoleColor.DarkGray);
        Console.Write($"{settings.MaxConcurrency}");
        PrintColored("  │  Remove duplicates: ", ConsoleColor.DarkGray);
        PrintColored(settings.RemoveDoubles ? "Yes" : "No", settings.RemoveDoubles ? ConsoleColor.Green : ConsoleColor.Red);
        Console.WriteLine();
        PrintColored("  ─────────────────────────────────────\n", ConsoleColor.DarkGray);

        // Hints for users
        PrintColored("  Tip: ", ConsoleColor.DarkYellow);
        PrintColored("Use ", ConsoleColor.DarkGray);
        PrintColored("-dest <path>", ConsoleColor.Cyan);
        PrintColored(" to change output, ", ConsoleColor.DarkGray);
        PrintColored("-h", ConsoleColor.Cyan);
        PrintColored(" for all options\n", ConsoleColor.DarkGray);
    }

    public void PrintProgress(ProgressReportModel report)
    {
        Console.SetCursorPosition(0, Console.CursorTop > 0 ? Console.CursorTop : 0);

        // Progress bar
        const int barWidth = 30;
        var filled = (int)(barWidth * report.PercentageCompleted / 100.0);
        var empty = barWidth - filled;

        var bar = new string('█', filled) + new string('░', empty);

        PrintColored($"  [{bar}] {report.PercentageCompleted,3}%", ConsoleColor.DarkCyan);
        Console.WriteLine();

        // Stats line
        PrintColored($"  ✓ ", ConsoleColor.Green);
        Console.Write($"{report.WorkingChannelsCount,-5}");
        PrintColored($"  ✗ ", ConsoleColor.Red);
        Console.Write($"{report.NotWorkingChannelsCount,-5}");
        PrintColored($"  Total: ", ConsoleColor.DarkGray);
        Console.WriteLine($"{report.WorkingChannelsCount + report.NotWorkingChannelsCount}/{report.ChannelsCountTotal}");

        // Move cursor back up for next update
        Console.SetCursorPosition(0, Console.CursorTop - 2);
    }

    public void PrintResults(FinalChannelReport report, AppSettings settings)
    {
        Console.SetCursorPosition(0, Console.CursorTop + 3);
        Console.WriteLine();

        PrintColored("╔════════════════════════════════════════╗\n", ConsoleColor.Green);
        PrintColored("║            ✓ COMPLETE!                 ║\n", ConsoleColor.Green);
        PrintColored("╚════════════════════════════════════════╝\n", ConsoleColor.Green);
        Console.WriteLine();

        var successRate = report.TotalChannelsCount > 0
            ? (double)report.WorkingChannelsCount / report.TotalChannelsCount * 100
            : 0;
        var failedCount = report.TotalChannelsCount - report.WorkingChannelsCount;

        PrintColored("  RESULTS\n", ConsoleColor.White);
        PrintColored("  ─────────────────────────────────────\n", ConsoleColor.DarkGray);

        // Working channels
        PrintColored("  Working:    ", ConsoleColor.DarkGray);
        PrintColored($"{report.WorkingChannelsCount}", ConsoleColor.Green);
        PrintColored($" / {report.TotalChannelsCount}", ConsoleColor.DarkGray);
        PrintColored($"  ({successRate:F1}%)\n", ConsoleColor.DarkCyan);

        // Failed channels
        PrintColored("  Failed:     ", ConsoleColor.DarkGray);
        PrintColored($"{failedCount}\n", failedCount > 0 ? ConsoleColor.Red : ConsoleColor.Green);

        // Duplicates removed
        if (report.DoublesRemovedCount > 0)
        {
            PrintColored("  Duplicates: ", ConsoleColor.DarkGray);
            PrintColored($"{report.DoublesRemovedCount} removed\n", ConsoleColor.Yellow);
        }

        PrintColored("  ─────────────────────────────────────\n", ConsoleColor.DarkGray);

        // Output file info
        PrintColored("  OUTPUT FILE\n", ConsoleColor.White);
        PrintColored("  ─────────────────────────────────────\n", ConsoleColor.DarkGray);
        PrintColored("  Path: ", ConsoleColor.DarkGray);
        Console.WriteLine(settings.ExportPath);

        // Show file size if exists
        if (File.Exists(settings.ExportPath))
        {
            var fileInfo = new FileInfo(settings.ExportPath);
            PrintColored("  Size: ", ConsoleColor.DarkGray);
            Console.WriteLine(FormatFileSize(fileInfo.Length));
        }

        PrintColored("  ─────────────────────────────────────\n", ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Parses command line arguments synchronously (no HTTP calls).
    /// </summary>
    public ParsedArguments ParseArguments()
    {
        string? sourcePath = null;
        string? exportPath = null;
        TimeSpan? timeout = null;
        var removeDoubles = true;
        var maxConcurrency = 10;

        for (var i = 0; i < _args.Length; i++)
        {
            var arg = _args[i].ToLowerInvariant();
            var hasValue = i + 1 < _args.Length;

            switch (arg)
            {
                case "-src" or "--src" or "-source" or "--source" when hasValue:
                    sourcePath = _args[++i].Trim('"');
                    break;

                case "-dest" or "--dest" or "-dst" or "--dst" or "-destination" or "--destination" when hasValue:
                    exportPath = _args[++i].Trim('"');
                    break;

                case "-timeout" or "--timeout" or "-to" or "--to" when hasValue:
                    if (int.TryParse(_args[++i], out var seconds) && seconds > 0)
                    {
                        timeout = TimeSpan.FromSeconds(seconds);
                    }
                    break;

                case "-rd" or "--rd" or "-removedoubles" or "--removedoubles" when hasValue:
                    removeDoubles = !IsFalseValue(_args[++i]);
                    break;

                case "-c" or "--concurrency" when hasValue:
                    if (int.TryParse(_args[++i], out var concurrency))
                    {
                        maxConcurrency = Math.Clamp(concurrency, 1, 50);
                    }
                    break;
            }
        }

        return new ParsedArguments(sourcePath, exportPath, timeout, removeDoubles, maxConcurrency);
    }

    /// <summary>
    /// Resolves parsed arguments into AppSettings, downloading if needed.
    /// </summary>
    public async Task<AppSettings?> ResolveSettingsAsync(
        ParsedArguments parsed,
        IFileDownloader fileDownloader,
        CancellationToken cancellationToken)
    {
        var sourcePath = parsed.SourcePath;
        var isLinkSource = false;

        // Interactive mode if no source specified
        if (string.IsNullOrEmpty(sourcePath))
        {
            PrintHeader();
            PrintColored("  Drag and drop an M3U file here, or paste a URL:\n", ConsoleColor.Cyan);
            PrintColored("  (Use ", ConsoleColor.DarkGray);
            PrintColored("-h", ConsoleColor.Cyan);
            PrintColored(" for advanced options: custom output path, timeout, concurrency)\n\n", ConsoleColor.DarkGray);
            Console.Write("  > ");

            do
            {
                sourcePath = Console.ReadLine()?.Trim().Trim('"');
            } while (string.IsNullOrEmpty(sourcePath));
            Console.WriteLine();
        }

        // Handle URL source
        if (Uri.TryCreate(sourcePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            PrintInfo("Downloading from URL...");
            isLinkSource = true;
            sourcePath = await fileDownloader.DownloadAsync(sourcePath, cancellationToken);
        }

        // Validate source exists
        if (!File.Exists(sourcePath))
        {
            PrintError($"✗ File not found: {sourcePath}");
            return null;
        }

        // Generate export path if not specified - default to temp folder
        var exportPath = parsed.ExportPath;
        if (string.IsNullOrEmpty(exportPath))
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "M3uListEditor");
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            exportPath = Path.Combine(tempDirectory, $"{fileName}-Cleaned.m3u");
        }

        return new AppSettings(
            SourcePath: sourcePath,
            ExportPath: exportPath,
            Timeout: parsed.Timeout,
            RemoveDoubles: parsed.RemoveDoubles,
            IsLinkSourcePath: isLinkSource,
            MaxConcurrency: parsed.MaxConcurrency);
    }

    public void PrintInfo(string message) => PrintColored($"  ℹ {message}\n", ConsoleColor.Cyan);
    public void PrintWarning(string message) => PrintColored($"  ⚠ {message}\n", ConsoleColor.Yellow);
    public void PrintError(string message) => PrintColored($"  {message}\n", ConsoleColor.Red);

    private static void PrintColored(string message, ConsoleColor color)
    {
        if (SupportsColor)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ForegroundColor = prev;
        }
        else
        {
            Console.Write(message);
        }
    }

    private static bool IsFalseValue(string value) =>
        value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("f", StringComparison.OrdinalIgnoreCase) ||
        value == "0" ||
        value.Equals("no", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Parsed command line arguments (before downloading/validation).
/// </summary>
internal record ParsedArguments(
    string? SourcePath,
    string? ExportPath,
    TimeSpan? Timeout,
    bool RemoveDoubles,
    int MaxConcurrency);
