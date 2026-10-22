using Mini_Project___Order_Processing_System.Runners;

namespace Mini_Project___Order_Processing_System;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // one cancellation source for the whole run
        using var cts = new CancellationTokenSource();

        // Ctrl+C cancels the run instead of killing the program
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // --cancel-after <ms>
        int index = Array.IndexOf(args, "--cancel-after");
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int ms))
        {
            cts.CancelAfter(ms);
        }

        // dotnet run -- phase3            (runs all the parts of phase 3)
        // dotnet run -- phase3 checkout   (only 9.3 / 9.4)
        // dotnet run -- phase3 compare    (only 9.6)
        // dotnet run -- phase3 demo       (only 9.7)
        string phase = "phase3";
        if (args.Length > 0 && !args[0].StartsWith("--")) phase = args[0];

        string part = "all";
        if (args.Length > 1 && !args[1].StartsWith("--")) part = args[1];

        if (phase == "phase1")
        {
            await new Phase1Runner().RunAsync(cts.Token);
        }
        else if (phase == "phase2")
        {
            await new Phase2Runner().RunAsync(cts.Token);
        }
        else if (phase == "phase3")
        {
            var runner = new Phase3Runner();
            if (part == "checkout") await runner.RunCheckoutAsync(cts.Token);
            else if (part == "compare") await runner.RunComparisonAsync(cts.Token);
            else if (part == "demo") await runner.RunDemoAsync(cts.Token);
            else await runner.RunAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("Unknown phase: " + phase);
        }
    }
}
