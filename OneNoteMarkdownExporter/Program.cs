using System;
using System.Runtime.InteropServices;
using System.Threading;
using OneNoteMarkdownExporter.Services;

namespace OneNoteMarkdownExporter;

public static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [STAThread]
    public static int Main(string[] args)
    {
        if (CliHandler.ShouldRunCli(args))
        {
            return RunCliAndExit(args);
        }

        // Hide console window when running GUI mode from console-subsystem binary.
        FreeConsole();
        var app = new App();
        return app.Run();
    }

    private static int RunCliAndExit(string[] args)
    {
        var exitCode = RunCli(args);
        Environment.Exit(exitCode);
        return exitCode;
    }

    private static int RunCli(string[] args)
    {
        int exitCode = 1;

        var cliThread = new Thread(() =>
        {
            try
            {
                exitCode = CliHandler.RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                exitCode = 1;
            }
        });

        cliThread.SetApartmentState(ApartmentState.STA);
        cliThread.Start();
        cliThread.Join();

        Console.Out.Flush();
        Console.Error.Flush();

        return exitCode;
    }
}
