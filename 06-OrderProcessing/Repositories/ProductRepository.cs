using Mini_Project___Order_Processing_System.Data;

namespace Mini_Project___Order_Processing_System;

public class ProductRepository
{
    private const string Name = "ProductRepository";
    private readonly JsonFileStore _store = new();

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        return Simulate.DbQueryAsync<Product?>(Name, $"Product #{id}", async token =>
        {
            var products = await _store.ReadAllAsync<Product>(DataFiles.Products, token);
            var product = products.FirstOrDefault(p => p.Id == id);

            if (product == null) Log.Error(Name, $"Product #{id} not found");
            else Log.Success(Name, $"Product #{id} found ({product.Name})");

            return product;
        }, ct);
    }

    public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct)
    {
        bool reserved = false;

        await Simulate.DbWriteAsync(Name, $"Reserving {qty} x Product #{productId}", async token =>
        {
            await _store.UpdateAsync<Product>(DataFiles.Products, list =>
            {
                var product = list.FirstOrDefault(p => p.Id == productId);
                if (product != null && product.StockQuantity >= qty)
                {
                    product.StockQuantity -= qty;
                    reserved = true;
                }
            }, token);
        }, ct);

        if (reserved) Log.Success(Name, $"Reserved {qty} x Product #{productId}");
        else Log.Error(Name, $"Could not reserve Product #{productId}");

        return reserved;
    }
}
