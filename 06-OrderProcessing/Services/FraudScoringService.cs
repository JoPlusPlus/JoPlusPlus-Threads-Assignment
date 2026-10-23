using System.Collections.Concurrent;

namespace Mini_Project___Order_Processing_System.Services;

// CPU-bound work: the CPU is busy all the time, there is nothing to await
public class FraudScoringService
{
    public static int Score(int orderId)
    {
        long h = orderId;
        for (int i = 0; i < 100_000; i++)
            h = (h * 31 + i) % 1_000_000_007;
        return (int)(h % 100);
    }

    // one order after the other
    public List<int> ScoreSequential(List<Order> orders, CancellationToken ct)
    {
        var flagged = new List<int>();
        foreach (var order in orders)
        {
            ct.ThrowIfCancellationRequested();
            if (Score(order.Id) >= 90) flagged.Add(order.Id);
        }
        return flagged;
    }

    // the library spreads the work over the cores, never more than ProcessorCount at the same time
    public async Task<(List<int> Flagged, int Threads)> ScoreParallelAsync(List<Order> orders, CancellationToken ct)
    {
        var flagged = new ConcurrentBag<int>();            // safe when many threads add
        var threads = new ConcurrentDictionary<int, byte>();

        var options = new ParallelOptions();
        options.MaxDegreeOfParallelism = Environment.ProcessorCount;
        options.CancellationToken = ct;

        await Parallel.ForEachAsync(orders, options, (order, token) =>
        {
            threads[Environment.CurrentManagedThreadId] = 0;
            if (Score(order.Id) >= 90) flagged.Add(order.Id);
            return ValueTask.CompletedTask;
        });

        // the bag has no order, so sort the ids
        return (flagged.OrderBy(id => id).ToList(), threads.Count);
    }
}
