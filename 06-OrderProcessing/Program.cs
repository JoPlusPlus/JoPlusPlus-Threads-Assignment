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

        // dotnet run -- phase1 | phase2 | phase3 | phase4 | all
        // phase3 can be followed by: checkout | compare | demo
        // phase4 can be followed by: progress | dashboard | shipping | fraud | email
        string phase = "";
        if (args.Length > 0 && !args[0].StartsWith("--")) phase = args[0];

        string part = "all";
        if (args.Length > 1 && !args[1].StartsWith("--")) part = args[1];

        // no phase in the arguments: show the menu
        if (phase == "") phase = AskPhase();

        if (phase == "all")
        {
            await RunPhaseAsync("phase1", "all", cts.Token);
            if (cts.IsCancellationRequested == false) await RunPhaseAsync("phase2", "all", cts.Token);
            if (cts.IsCancellationRequested == false) await RunPhaseAsync("phase3", "all", cts.Token);
            if (cts.IsCancellationRequested == false) await RunPhaseAsync("phase4", "all", cts.Token);
        }
        else
        {
            await RunPhaseAsync(phase, part, cts.Token);
        }
    }

    // Run Menu
    private static string AskPhase()
    {
        Console.WriteLine("===== Order Processing System =====");
        Console.WriteLine("1) Phase 1 - async data access layer");
        Console.WriteLine("2) Phase 2 - concurrent processing");
        Console.WriteLine("3) Phase 3 - full checkout workflow");
        Console.WriteLine("4) Phase 4 - scale-up");
        Console.WriteLine("5) All phases");
        Console.Write("Choose a number: ");

        string? choice = Console.ReadLine();
        if (choice == "1") return "phase1";
        if (choice == "2") return "phase2";
        if (choice == "3") return "phase3";
        if (choice == "4") return "phase4";
        if (choice == "5") return "all";
        return "unknown";
    }

    private static async Task RunPhaseAsync(string phase, string part, CancellationToken ct)
    {
        if (phase == "phase1")
        {
            await new Phase1Runner().RunAsync(ct);
        }
        else if (phase == "phase2")
        {
            await new Phase2Runner().RunAsync(ct);
        }
        else if (phase == "phase3")
        {
            var runner = new Phase3Runner();
            if (part == "checkout") await runner.RunCheckoutAsync(ct);
            else if (part == "compare") await runner.RunComparisonAsync(ct);
            else if (part == "demo") await runner.RunDemoAsync(ct);
            else await runner.RunAsync(ct);
        }
        else if (phase == "phase4")
        {
            var runner = new Phase4Runner();
            if (part == "progress") await runner.RunProgressAsync(ct);
            else if (part == "dashboard") await runner.RunDashboardAsync(ct);
            else if (part == "shipping") await runner.RunShippingAsync(ct);
            else if (part == "fraud") await runner.RunFraudAsync(ct);
            else if (part == "email") await runner.RunEmailResendAsync(ct);
            else await runner.RunAsync(ct);
        }
        else
        {
            Console.WriteLine("Unknown phase: " + phase);
        }
    }
}
