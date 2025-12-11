using StreamForge.Cli;
using StreamForge.Core;

var cli = new CliHandler(args);

if (cli.ShowHelp)
{
    cli.PrintHelp();
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = false;
    cts.Cancel();
    cli.PrintWarning("\n⚠ Cancelling...");
    Environment.Exit(130);
};

try
{
    var parsedArgs = cli.ParseArguments();

    using var httpClient = new HttpClient
    {
        Timeout = parsedArgs.Timeout ?? AppSettings.DefaultTimeout
    };

    var engine = new Engine(httpClient, parsedArgs.MaxConcurrency);
    var settings = await cli.ResolveSettingsAsync(parsedArgs, engine, cts.Token);

    if (settings is null)
    {
        Environment.Exit(1);
        return;
    }

    var lastPercent = -1;
    var progress = new Progress<ProgressReport>(report =>
    {
        if (report.PercentageCompleted == lastPercent) return;
        lastPercent = report.PercentageCompleted;
        cli.PrintProgress(report);
    });

    cli.PrintHeader();
    cli.PrintSettings(settings);
    Console.WriteLine();

    var report = await engine.ProcessAsync(settings, progress, cts.Token);
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
        Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
    Environment.Exit(1);
}
