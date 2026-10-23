using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;

namespace Mini_Project___Order_Processing_System.Services;

// what the 3 carriers have in common: handshake, wait, price
public abstract class ShippingCarrierBase : IShippingCarrier
{
    private readonly JsonFileStore _store = new();
    private readonly HashSet<int> _failForOrderIds;

    protected ShippingCarrierBase(IEnumerable<int>? failForOrderIds)
    {
        _failForOrderIds = new HashSet<int>(failForOrderIds ?? new List<int>());
    }

    public abstract string Name { get; }
    protected abstract int MinMs { get; }
    protected abstract int MaxMs { get; }
    protected abstract decimal Factor { get; }

    // a carrier can fail by itself (CairoExpress does it for even ids)
    protected virtual bool FailsByItself(Order order)
    {
        return false;
    }

    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        try
        {
            await Simulate.StepAsync(Name, $"Handshake for Order #{order.Id}...", 150, ct);

            if (_failForOrderIds.Contains(order.Id) || FailsByItself(order))
            {
                Log.Error(Name, $"Order #{order.Id}: carrier unavailable");
                throw new ShippingProviderUnavailableException($"{Name} is unavailable");
            }

            await Simulate.StepAsync(Name, "Waiting for the quote...", Random.Shared.Next(MinMs, MaxMs + 1), ct);

            var products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
            decimal price = Math.Round(ShippingPrice.Calculate(order, products) * Factor, 2, MidpointRounding.AwayFromZero);

            Log.Success(Name, $"Order #{order.Id}: quote {price}");
            return price;
        }
        catch (OperationCanceledException)
        {
            Log.Info("Carrier", $"[{Name}] cancelled");
            throw;
        }
    }
}
