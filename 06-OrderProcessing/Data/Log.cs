using System.Collections.Concurrent;

namespace Mini_Project___Order_Processing_System.Data;

public static class Log
{
    private static readonly object _sync = new();

    // every thread id that wrote a log line (used by the sync vs async comparison)
    private static readonly ConcurrentDictionary<int, byte> _threads = new();

    public static int DistinctThreads => _threads.Count;

    public static void ResetThreads()
    {
        _threads.Clear();
    }

    public static void Info(string component, string message)
    {
        Write(null, component, message);
    }

    public static void Success(string component, string message)
    {
        Write(ConsoleColor.Green, component, message);
    }

    public static void Error(string component, string message)
    {
        Write(ConsoleColor.Red, component, message);
    }

    private static void Write(ConsoleColor? color, string component, string message)
    {
        _threads[Environment.CurrentManagedThreadId] = 0;

        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] {component} -> {message}";

        lock (_sync)
        {
            if (color.HasValue) Console.ForegroundColor = color.Value;
            Console.WriteLine(line);
            Console.ResetColor();
        }
    }
}
