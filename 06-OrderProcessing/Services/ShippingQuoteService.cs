using System.Diagnostics;
using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;

namespace Mini_Project___Order_Processing_System.Services;

public class ShippingQuoteService
{
    private const string Name = "ShippingQuoteService";
    private readonly List<IShippingCarrier> _carriers;

    public ShippingQuoteService(IEnumerable<IShippingCarrier> carriers)
    {
        _carriers = carriers.ToList();
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();

        // One source for all the carriers. It is cancelled when:
        // the caller cancels (ct), the 1.2 s budget ends, or we have a winner.
        using var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        source.CancelAfter(1200);

        // 1. ask all the carriers at the same time
        var tasks = new List<Task<decimal>>();
        var carrierNames = new Dictionary<Task<decimal>, string>();
        foreach (var carrier in _carriers)
        {
            var task = carrier.GetQuoteAsync(order, source.Token);
            tasks.Add(task);
            carrierNames[task] = carrier.Name;
        }

        // 2. the first SUCCESSFUL quote wins. A failed carrier is skipped, we keep waiting.
        ShippingQuote? winner = null;
        await foreach (var task in Task.WhenEach(tasks))
        {
            if (task.IsCompletedSuccessfully)
            {
                winner = new ShippingQuote();
                winner.Carrier = carrierNames[task];
                winner.Price = await task;
                winner.ElapsedMs = watch.ElapsedMilliseconds;
                break;
            }
        }

        // 3. stop every carrier that is still running
        source.Cancel();

        // wait until they stopped, so we know which ones were cancelled
        var cancelled = new List<string>();
        foreach (var task in tasks)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                cancelled.Add(carrierNames[task]);
            }
            catch (Exception)
            {
                // this carrier failed by itself, it was not cancelled
            }
        }

        // the caller cancelled
        ct.ThrowIfCancellationRequested();

        if (winner != null)
        {
            winner.CancelledCarriers = cancelled;
            Log.Success(Name, $"Order #{order.Id}: {winner.Carrier} won ({winner.Price}) in {winner.ElapsedMs} ms");
            return winner;
        }

        // nobody won: all failed, or the 1.2 s budget ended
        if (tasks.All(t => t.IsFaulted))
        {
            Log.Error(Name, $"Order #{order.Id}: all carriers failed");
            throw new ShippingProviderUnavailableException("All carriers failed");
        }

        var flat = new ShippingQuote();
        flat.Carrier = "Flat rate";
        flat.Price = 25.00m;
        flat.ElapsedMs = watch.ElapsedMilliseconds;
        flat.CancelledCarriers = cancelled;
        Log.Error(Name, $"Order #{order.Id}: no quote in 1.2 s, flat rate {flat.Price}");
        return flat;
    }
}
