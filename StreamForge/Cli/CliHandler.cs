using StreamForge.Core;

namespace StreamForge.Cli;

/// <summary>
/// Handles CLI argument parsing, display, and user interaction with modern UI.
/// </summary>
public sealed class CliHandler
{
    private readonly string[] _args;
    private static readonly bool SupportsColor = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;
    private static readonly string[] Spinner = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

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
        PrintLogo();
        
        PrintColored("\n  ", ConsoleColor.White);
        PrintColored("Clean and enhance M3U/M3U8 playlists with smart detection\n\n", ConsoleColor.DarkGray);

        PrintColored("  USAGE\n", ConsoleColor.White);
        PrintColored("  ─────────────────────────────────────────────────────────\n", ConsoleColor.DarkGray);
        PrintColored("    streamforge ", ConsoleColor.Cyan);
        PrintColored("[options]\n", ConsoleColor.DarkGray);
        PrintColored("    streamforge ", ConsoleColor.Cyan);
        PrintColored("-src ", ConsoleColor.Yellow);
        PrintColored("<file|url> ", ConsoleColor.White);
        PrintColored("[-dest ", ConsoleColor.Yellow);
        PrintColored("<file>", ConsoleColor.White);
        PrintColored("] [options]\n\n", ConsoleColor.Yellow);

        PrintSection("📁 SOURCE", [
            ("-src, --source <path>", "Source M3U file or URL"),
            ("-dest, --destination <path>", "Output file path"),
            ("-merge", "Merge multiple sources into one")
        ]);

        PrintSection("⚡ VALIDATION", [
            ("-timeout <seconds>", "Connection timeout (default: 10)"),
            ("-c <n>", "Parallel workers (default: 10, max: 50)"),
            ("-rd <bool>", "Remove duplicates (default: true)"),
            ("-skip-validation", "Skip stream testing")
        ]);

        PrintSection("🔍 FILTERING", [
            ("-g, --group <name>", "Filter by group name"),
            ("-n, --name <pattern>", "Filter by channel name"),
            ("-regex", "Use regex for patterns"),
            ("-sort <type>", "Sort: name, group, group-name"),
            ("-desc", "Sort descending")
        ]);

        PrintSection("🧠 SMART FEATURES", [
            ("-smart-cat", "Auto-categorize channels (on)"),
            ("-lang", "Detect languages (on)"),
            ("-deep-scan", "Content hash duplicates (slow)"),
            ("-rename <pat> <repl>", "Rename channels")
        ]);

        PrintSection("📤 OUTPUT", [
            ("-split", "Split output by group"),
            ("-v, --verbose", "Show detailed errors"),
            ("-h, --help", "Show this help")
        ]);

        PrintColored("\n  EXAMPLES\n", ConsoleColor.White);
        PrintColored("  ─────────────────────────────────────────────────────────\n", ConsoleColor.DarkGray);
        PrintExample("streamforge -src playlist.m3u");
        PrintExample("streamforge -src playlist.m3u -g \"Sports\" -sort name");
        PrintExample("streamforge -src list1.m3u -src list2.m3u -merge");
        PrintExample("streamforge -src playlist.m3u -rename \"US:\" \"\"");
        Console.WriteLine();
    }

    private void PrintSection(string title, (string cmd, string desc)[] items)
    {
        PrintColored($"\n  {title}\n", ConsoleColor.White);
        PrintColored("  ─────────────────────────────────────────────────────────\n", ConsoleColor.DarkGray);
        foreach (var (cmd, desc) in items)
        {
            PrintColored($"    {cmd,-28}", ConsoleColor.Cyan);
            PrintColored($"{desc}\n", ConsoleColor.DarkGray);
        }
    }

    private void PrintExample(string example)
    {
        PrintColored("    $ ", ConsoleColor.DarkGray);
        PrintColored($"{example}\n", ConsoleColor.White);
    }

    private void PrintLogo()
    {
        Console.Clear();
        var logo = @"
   ███████╗████████╗██████╗ ███████╗ █████╗ ███╗   ███╗
   ██╔════╝╚══██╔══╝██╔══██╗██╔════╝██╔══██╗████╗ ████║
   ███████╗   ██║   ██████╔╝█████╗  ███████║██╔████╔██║
   ╚════██║   ██║   ██╔══██╗██╔══╝  ██╔══██╝██║╚██╔╝██║
   ███████║   ██║   ██║  ██║███████╗██║  ██║██║ ╚═╝ ██║
   ╚══════╝   ╚═╝   ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝
";
        var lines = logo.Split('\n');
        var colors = new[] { ConsoleColor.Cyan, ConsoleColor.Cyan, ConsoleColor.DarkCyan, ConsoleColor.DarkCyan, ConsoleColor.Blue, ConsoleColor.Blue, ConsoleColor.DarkBlue };
        
        for (var i = 0; i < lines.Length && i < colors.Length; i++)
            PrintColored(lines[i] + "\n", colors[i]);

        PrintColored("                          FORGE ", ConsoleColor.DarkGray);
        PrintColored("v2.0\n", ConsoleColor.Cyan);
    }

    public void PrintHeader()
    {
        PrintLogo();
        Console.WriteLine();
    }

    public void PrintSettings(AppSettings settings)
    {
        PrintColored("  ╭───────────────────────────────────────────────────────╮\n", ConsoleColor.DarkGray);
        
        // Source
        PrintColored("  │  ", ConsoleColor.DarkGray);
        PrintColored("📁 Source      ", ConsoleColor.White);
        if (settings.IsLinkSourcePath) PrintColored("[URL] ", ConsoleColor.Magenta);
        var srcDisplay = settings.MergeFiles && settings.SourcePaths.Count > 1
            ? $"{settings.SourcePaths.Count} files (merge)"
            : Path.GetFileName(settings.SourcePath);
        PrintColored($"{srcDisplay,-35}", ConsoleColor.Cyan);
        PrintColored("│\n", ConsoleColor.DarkGray);
        
        // Destination
        PrintColored("  │  ", ConsoleColor.DarkGray);
        PrintColored("📤 Output      ", ConsoleColor.White);
        var destDisplay = settings.SplitByGroup ? $"{Path.GetFileName(settings.ExportPath)} (split)" : Path.GetFileName(settings.ExportPath);
        PrintColored($"{destDisplay,-35}", ConsoleColor.Cyan);
        PrintColored("│\n", ConsoleColor.DarkGray);

        PrintColored("  ├───────────────────────────────────────────────────────┤\n", ConsoleColor.DarkGray);

        // Settings row 1
        PrintColored("  │  ", ConsoleColor.DarkGray);
        PrintColored("⏱️  Timeout ", ConsoleColor.White);
        PrintColored($"{(settings.Timeout ?? AppSettings.DefaultTimeout).TotalSeconds}s", ConsoleColor.Cyan);
        PrintColored("    ⚡ Workers ", ConsoleColor.White);
        PrintColored($"{settings.MaxConcurrency}", ConsoleColor.Cyan);
        PrintColored("    🧹 Dedupe ", ConsoleColor.White);
        PrintColored(settings.RemoveDoubles ? "On " : "Off", settings.RemoveDoubles ? ConsoleColor.Green : ConsoleColor.DarkGray);
        PrintColored("     │\n", ConsoleColor.DarkGray);

        // Filters if any
        if (!string.IsNullOrEmpty(settings.GroupFilter) || !string.IsNullOrEmpty(settings.NamePattern) || settings.SortBy != SortOrder.None)
        {
            PrintColored("  ├───────────────────────────────────────────────────────┤\n", ConsoleColor.DarkGray);
            
            if (!string.IsNullOrEmpty(settings.GroupFilter))
            {
                PrintColored("  │  ", ConsoleColor.DarkGray);
                PrintColored("🔍 Group       ", ConsoleColor.White);
                PrintColored($"\"{settings.GroupFilter}\"", ConsoleColor.Yellow);
                var pad = 35 - settings.GroupFilter.Length - 2;
                PrintColored(new string(' ', Math.Max(0, pad)), ConsoleColor.White);
                PrintColored("│\n", ConsoleColor.DarkGray);
            }
            if (!string.IsNullOrEmpty(settings.NamePattern))
            {
                PrintColored("  │  ", ConsoleColor.DarkGray);
                PrintColored("🔍 Name        ", ConsoleColor.White);
                PrintColored($"\"{settings.NamePattern}\"", ConsoleColor.Yellow);
                if (settings.UseRegex) PrintColored(" (regex)", ConsoleColor.DarkYellow);
                Console.WriteLine();
            }
            if (settings.SortBy != SortOrder.None)
            {
                PrintColored("  │  ", ConsoleColor.DarkGray);
                PrintColored("📊 Sort        ", ConsoleColor.White);
                PrintColored($"{settings.SortBy}", ConsoleColor.Cyan);
                if (settings.SortDescending) PrintColored(" ↓", ConsoleColor.DarkYellow);
                Console.WriteLine();
            }
        }

        PrintColored("  ╰───────────────────────────────────────────────────────╯\n", ConsoleColor.DarkGray);
    }

    private int _progressStartLine = -1;

    public void PrintProgress(ProgressReport report)
    {
        try
        {
            // Track where progress display starts
            if (_progressStartLine < 0)
                _progressStartLine = Console.CursorTop;
            else
                Console.SetCursorPosition(0, _progressStartLine);

            // Progress bar
            const int barWidth = 40;
            var filled = (int)(barWidth * report.PercentageCompleted / 100.0);
            var bar = new string('█', filled) + new string('░', barWidth - filled);
            
            var spinnerIdx = (int)(DateTime.Now.Ticks / 1000000 % Spinner.Length);
            PrintColored($"\n  {Spinner[spinnerIdx]} ", ConsoleColor.Cyan);
            PrintColored($"{report.CurrentActivity,-50}\n\n", ConsoleColor.White);
            
            PrintColored("  ", ConsoleColor.White);
            PrintColored(bar, ConsoleColor.DarkCyan);
            PrintColored($" {report.PercentageCompleted,3}%\n\n", ConsoleColor.White);

            // Stats box
            PrintColored("  ┌─────────────────┬────────────┐\n", ConsoleColor.DarkGray);
            
            PrintColored("  │ ", ConsoleColor.DarkGray);
            PrintColored("✓ Working     ", ConsoleColor.Green);
            PrintColored("│ ", ConsoleColor.DarkGray);
            PrintColored($"{report.WorkingChannelsCount,-10}", ConsoleColor.White);
            PrintColored("│\n", ConsoleColor.DarkGray);
            
            PrintColored("  │ ", ConsoleColor.DarkGray);
            PrintColored("✗ Failed      ", ConsoleColor.Red);
            PrintColored("│ ", ConsoleColor.DarkGray);
            PrintColored($"{report.NotWorkingChannelsCount,-10}", ConsoleColor.White);
            PrintColored("│\n", ConsoleColor.DarkGray);
            
            var remaining = report.ChannelsCountTotal - report.WorkingChannelsCount - report.NotWorkingChannelsCount;
            PrintColored("  │ ", ConsoleColor.DarkGray);
            PrintColored("⏳ Remaining  ", ConsoleColor.DarkYellow);
            PrintColored("│ ", ConsoleColor.DarkGray);
            PrintColored($"{remaining,-10}", ConsoleColor.White);
            PrintColored("│\n", ConsoleColor.DarkGray);
            
            PrintColored("  └─────────────────┴────────────┘", ConsoleColor.DarkGray);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Console buffer too small, just print without cursor manipulation
        }
    }

    public void PrintResults(ProcessingReport report, AppSettings settings)
    {
        Console.WriteLine();
        Console.WriteLine();

        var successRate = report.TotalChannelsCount > 0 ? (double)report.WorkingChannelsCount / report.TotalChannelsCount * 100 : 0;
        var failedCount = report.TotalChannelsCount - report.WorkingChannelsCount;

        // Success rate bar
        const int rateBarWidth = 20;
        var rateFilled = (int)(rateBarWidth * successRate / 100.0);
        var rateBar = new string('█', rateFilled) + new string('░', rateBarWidth - rateFilled);

        PrintColored("  ╭─────────────────── ", ConsoleColor.Green);
        PrintColored("✓ COMPLETE", ConsoleColor.Green);
        PrintColored(" ───────────────────╮\n", ConsoleColor.Green);
        PrintColored("  │                                                   │\n", ConsoleColor.DarkGray);

        // Results section
        PrintColored("  │  ", ConsoleColor.DarkGray);
        PrintColored("📊 Results", ConsoleColor.White);
        PrintColored("                                       │\n", ConsoleColor.DarkGray);
        
        PrintColored("  │  ", ConsoleColor.DarkGray);
        PrintColored("   ✓ ", ConsoleColor.Green);
        PrintColored($"{report.WorkingChannelsCount} working", ConsoleColor.White);
        PrintColored($"   ({successRate:F0}%)  ", ConsoleColor.DarkGray);
        PrintColored(rateBar, successRate > 80 ? ConsoleColor.Green : successRate > 50 ? ConsoleColor.Yellow : ConsoleColor.Red);
        PrintColored("   │\n", ConsoleColor.DarkGray);

        if (!settings.SkipValidation && failedCount > 0)
        {
            PrintColored("  │  ", ConsoleColor.DarkGray);
            PrintColored("   ✗ ", ConsoleColor.Red);
            PrintColored($"{failedCount} failed", ConsoleColor.White);
            PrintColored("                                  │\n", ConsoleColor.DarkGray);
        }

        if (report.DoublesRemovedCount > 0)
        {
            PrintColored("  │  ", ConsoleColor.DarkGray);
            PrintColored("   🗑️  ", ConsoleColor.Yellow);
            PrintColored($"{report.DoublesRemovedCount} duplicates removed", ConsoleColor.White);
            PrintColored("                      │\n", ConsoleColor.DarkGray);
        }

        PrintColored("  │                                                   │\n", ConsoleColor.DarkGray);

        // Enhancements section
        if (report.CategorizedCount > 0 || report.LanguagesDetectedCount > 0 || report.GroupCount > 0)
        {
            PrintColored("  │  ", ConsoleColor.DarkGray);
            PrintColored("🏷️  Enhancements", ConsoleColor.White);
            PrintColored("                                 │\n", ConsoleColor.DarkGray);
            
            PrintColored("  │     ", ConsoleColor.DarkGray);
            if (report.GroupCount > 0)
            {
                PrintColored($"📦 {report.GroupCount} groups", ConsoleColor.Cyan);
                PrintColored("   ", ConsoleColor.DarkGray);
            }
            if (report.CategorizedCount > 0)
            {
                PrintColored($"🎯 {report.CategorizedCount} categorized", ConsoleColor.Cyan);
                PrintColored("   ", ConsoleColor.DarkGray);
            }
            if (report.LanguagesDetectedCount > 0)
            {
                PrintColored($"🌍 {report.LanguagesDetectedCount} languages", ConsoleColor.Cyan);
            }
            Console.WriteLine();
            
            PrintColored("  │                                                   │\n", ConsoleColor.DarkGray);
        }

        // Output section
        PrintColored("  │  ", ConsoleColor.DarkGray);
        PrintColored("📁 Output", ConsoleColor.White);
        PrintColored("                                        │\n", ConsoleColor.DarkGray);
        
        if (settings.SplitByGroup)
        {
            PrintColored("  │     ", ConsoleColor.DarkGray);
            PrintColored($"{report.GroupCount} files created", ConsoleColor.Green);
            PrintColored("                            │\n", ConsoleColor.DarkGray);
        }
        
        PrintColored("  │                                                   │\n", ConsoleColor.DarkGray);
        PrintColored("  ╰───────────────────────────────────────────────────╯\n\n", ConsoleColor.Green);

        // Show full path outside the box for better readability
        PrintColored("  📁 Saved to: ", ConsoleColor.DarkGray);
        PrintColored($"{settings.ExportPath}\n", ConsoleColor.Cyan);

        if (!settings.SplitByGroup && File.Exists(settings.ExportPath))
        {
            var fileInfo = new FileInfo(settings.ExportPath);
            PrintColored($"     Size: {FormatFileSize(fileInfo.Length)}\n", ConsoleColor.DarkGray);
        }
        Console.WriteLine();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
        return $"{size:0.##} {sizes[order]}";
    }

    public ParsedArguments ParseArguments()
    {
        var sourcePaths = new List<string>();
        string? exportPath = null;
        TimeSpan? timeout = null;
        var removeDoubles = true;
        var maxConcurrency = 10;
        string? groupFilter = null, namePattern = null;
        var useRegex = false;
        var sortBy = SortOrder.None;
        var sortDescending = false;
        var splitByGroup = false;
        var mergeFiles = false;
        var skipValidation = false;
        var autoCategorize = true;
        var detectLanguage = true;
        var detectContentDuplicates = false;
        string? renamePattern = null, renameReplacement = null;

        for (var i = 0; i < _args.Length; i++)
        {
            var arg = _args[i].ToLowerInvariant();
            var hasValue = i + 1 < _args.Length;

            switch (arg)
            {
                case "-src" or "--src" or "-source" or "--source" when hasValue:
                    sourcePaths.Add(_args[++i].Trim('"')); break;
                case "-dest" or "--dest" or "-dst" or "--dst" or "-destination" or "--destination" when hasValue:
                    exportPath = _args[++i].Trim('"'); break;
                case "-timeout" or "--timeout" or "-to" or "--to" when hasValue:
                    if (int.TryParse(_args[++i], out var seconds) && seconds > 0) timeout = TimeSpan.FromSeconds(seconds); break;
                case "-rd" or "--rd" or "-removedoubles" or "--removedoubles" when hasValue:
                    removeDoubles = !IsFalseValue(_args[++i]); break;
                case "-c" or "--concurrency" when hasValue:
                    if (int.TryParse(_args[++i], out var concurrency)) maxConcurrency = Math.Clamp(concurrency, 1, 50); break;
                case "-g" or "--group" when hasValue:
                    groupFilter = _args[++i].Trim('"'); break;
                case "-n" or "--name" when hasValue:
                    namePattern = _args[++i].Trim('"'); break;
                case "-regex" or "--regex":
                    useRegex = true; break;
                case "-sort" or "--sort" when hasValue:
                    sortBy = _args[++i].ToLowerInvariant() switch { "name" => SortOrder.Name, "group" => SortOrder.Group, "group-name" or "groupname" => SortOrder.GroupThenName, _ => SortOrder.None }; break;
                case "-desc" or "--desc":
                    sortDescending = true; break;
                case "-split" or "--split":
                    splitByGroup = true; break;
                case "-merge" or "--merge":
                    mergeFiles = true; break;
                case "-skip-validation" or "--skip-validation":
                    skipValidation = true; break;
                case "-smart-cat" or "--smart-cat" or "--auto-categorize":
                    autoCategorize = true; break;
                case "-no-smart-cat" or "--no-smart-cat" or "--no-auto-categorize":
                    autoCategorize = false; break;
                case "-lang" or "--lang" or "--detect-language":
                    detectLanguage = true; break;
                case "-no-lang" or "--no-lang" or "--no-detect-language":
                    detectLanguage = false; break;
                case "-deep-scan" or "--deep-scan" or "--content-dupes":
                    detectContentDuplicates = true; break;
                case "-rename" or "--rename" or "-r" when i + 2 < _args.Length:
                    renamePattern = _args[++i].Trim('"'); renameReplacement = _args[++i].Trim('"'); break;
            }
        }

        return new ParsedArguments(sourcePaths.Count > 0 ? sourcePaths[0] : null, sourcePaths, exportPath, timeout, removeDoubles, maxConcurrency,
            groupFilter, namePattern, useRegex, sortBy, sortDescending, splitByGroup, mergeFiles, skipValidation, autoCategorize, detectLanguage, detectContentDuplicates, renamePattern, renameReplacement);
    }

    public async Task<AppSettings?> ResolveSettingsAsync(ParsedArguments parsed, Engine engine, CancellationToken cancellationToken)
    {
        var sourcePath = parsed.SourcePath;
        var sourcePaths = parsed.SourcePaths.ToList();
        var isLinkSource = false;

        if (string.IsNullOrEmpty(sourcePath) && sourcePaths.Count == 0)
        {
            PrintHeader();
            Console.WriteLine();
            PrintColored("  📁 ", ConsoleColor.Cyan);
            PrintColored("Drop an M3U file here, or paste a URL:\n", ConsoleColor.White);
            PrintColored("     ", ConsoleColor.DarkGray);
            PrintColored("(Use ", ConsoleColor.DarkGray);
            PrintColored("-h", ConsoleColor.Cyan);
            PrintColored(" for all options)\n\n", ConsoleColor.DarkGray);
            PrintColored("  ❯ ", ConsoleColor.Cyan);

            do { sourcePath = Console.ReadLine()?.Trim().Trim('"'); } while (string.IsNullOrEmpty(sourcePath));
            Console.WriteLine();
            sourcePaths.Add(sourcePath);
        }

        for (var i = 0; i < sourcePaths.Count; i++)
        {
            var path = sourcePaths[i];
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                await PrintSpinnerAsync($"Downloading ({i + 1}/{sourcePaths.Count})...", async () =>
                {
                    sourcePaths[i] = await engine.DownloadAsync(path, cancellationToken);
                });
                isLinkSource = true;
            }

            if (!File.Exists(sourcePaths[i]))
            {
                PrintError($"File not found: {sourcePaths[i]}");
                return null;
            }
        }

        sourcePath = sourcePaths[0];
        var exportPath = parsed.ExportPath;
        if (string.IsNullOrEmpty(exportPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            exportPath = Path.Combine(AppContext.BaseDirectory, $"{fileName}-Cleaned.m3u");
        }

        return new AppSettings
        {
            SourcePath = sourcePath,
            SourcePaths = sourcePaths,
            ExportPath = exportPath,
            Timeout = parsed.Timeout,
            RemoveDoubles = parsed.RemoveDoubles,
            IsLinkSourcePath = isLinkSource,
            MaxConcurrency = parsed.MaxConcurrency,
            GroupFilter = parsed.GroupFilter,
            NamePattern = parsed.NamePattern,
            UseRegex = parsed.UseRegex,
            SortBy = parsed.SortBy,
            SortDescending = parsed.SortDescending,
            SplitByGroup = parsed.SplitByGroup,
            MergeFiles = parsed.MergeFiles,
            SkipValidation = parsed.SkipValidation,
            AutoCategorize = parsed.AutoCategorize,
            DetectLanguage = parsed.DetectLanguage,
            DetectContentDuplicates = parsed.DetectContentDuplicates,
            RenamePattern = parsed.RenamePattern,
            RenameReplacement = parsed.RenameReplacement,
            RenameUseRegex = parsed.UseRegex
        };
    }

    private async Task PrintSpinnerAsync(string message, Func<Task> action)
    {
        var spinnerTask = Task.Run(async () =>
        {
            var startPos = Console.CursorTop;
            var idx = 0;
            while (true)
            {
                Console.SetCursorPosition(0, startPos);
                PrintColored($"  {Spinner[idx++ % Spinner.Length]} ", ConsoleColor.Cyan);
                PrintColored(message, ConsoleColor.White);
                Console.Write("   ");
                await Task.Delay(80);
            }
        });

        try { await action(); }
        finally
        {
            // Clear spinner line
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            PrintColored("  ✓ ", ConsoleColor.Green);
            PrintColored("Downloaded\n", ConsoleColor.White);
        }
    }

    public void PrintInfo(string message) => PrintColored($"  ℹ️  {message}\n", ConsoleColor.Cyan);
    public void PrintWarning(string message) => PrintColored($"  ⚠️  {message}\n", ConsoleColor.Yellow);
    public void PrintError(string message) => PrintColored($"  ❌ {message}\n", ConsoleColor.Red);

    private static void PrintColored(string message, ConsoleColor color)
    {
        if (SupportsColor)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ForegroundColor = prev;
        }
        else Console.Write(message);
    }

    private static bool IsFalseValue(string value) =>
        value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("f", StringComparison.OrdinalIgnoreCase) ||
        value == "0" ||
        value.Equals("no", StringComparison.OrdinalIgnoreCase);
}
