using Mini_Project___Order_Processing_System.Data;

namespace Mini_Project___Order_Processing_System.Services;

public class InventoryService
{
    private const string Name = "InventoryService";
    private readonly JsonFileStore _store = new();
    private readonly ProductRepository _products = new();

    // All items or nothing. The check and the subtract are inside ONE locked update.
    public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct)
    {
        bool reserved = false;

        try
        {
            await Simulate.DbWriteAsync(Name, $"Reserving stock for Order #{order.Id}", async token =>
            {
                await _store.UpdateAsync<Product>(DataFiles.Products, list =>
                {
                    // 1. check every item first
                    foreach (var item in order.Items)
                    {
                        var product = list.FirstOrDefault(p => p.Id == item.ProductId);
                        if (product == null || product.StockQuantity < item.Quantity) return;
                    }

                    // 2. everything is available, take it all
                    foreach (var item in order.Items)
                    {
                        list.First(p => p.Id == item.ProductId).StockQuantity -= item.Quantity;
                    }
                    reserved = true;
                }, token);
            }, ct);
        }
        catch (OperationCanceledException) when (reserved)
        {
            // cancelled after the stock was already taken: give it back before leaving
            await ReleaseStockAsync(order, CancellationToken.None);
            throw;
        }

        if (reserved) Log.Success(Name, $"Stock reserved for Order #{order.Id}");
        else Log.Error(Name, $"Not enough stock for Order #{order.Id}");

        return reserved;
    }

    // Adds the quantity of every item back
    public async Task ReleaseStockAsync(Order order, CancellationToken ct)
    {
        await Simulate.DbWriteAsync(Name, $"Releasing stock of Order #{order.Id}", async token =>
        {
            await _store.UpdateAsync<Product>(DataFiles.Products, list =>
            {
                foreach (var item in order.Items)
                {
                    var product = list.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null) product.StockQuantity += item.Quantity;
                }
            }, token);
        }, ct);

        Log.Success(Name, $"Stock released for Order #{order.Id}");
    }

    // Name of the first product that has not enough stock (for the rejection reason)
    public async Task<string> GetShortProductNameAsync(Order order, CancellationToken ct)
    {
        string name = "unknown product";
        foreach (var item in order.Items)
        {
            var product = await _products.GetByIdAsync(item.ProductId, ct);
            if (product != null && product.StockQuantity < item.Quantity)
            {
                return product.Name;
            }
            if (product != null) name = product.Name;
        }
        return name;
    }
}
