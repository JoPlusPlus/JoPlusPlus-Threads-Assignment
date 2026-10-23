using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.Repositories;

namespace Mini_Project___Order_Processing_System.Services;

public class SyncInventoryService
{
    private const string Name = "SyncInventoryService";
    private readonly JsonFileStore _store = new();
    private readonly SyncProductRepository _products = new();

    public bool ReserveStock(Order order)
    {
        bool reserved = false;

        Simulate.DbWrite(Name, $"Reserving stock for Order #{order.Id}", () =>
        {
            _store.Update<Product>(DataFiles.Products, list =>
            {
                foreach (var item in order.Items)
                {
                    var product = list.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product == null || product.StockQuantity < item.Quantity) return;
                }

                foreach (var item in order.Items)
                {
                    list.First(p => p.Id == item.ProductId).StockQuantity -= item.Quantity;
                }
                reserved = true;
            });
        });

        if (reserved) Log.Success(Name, $"Stock reserved for Order #{order.Id}");
        else Log.Error(Name, $"Not enough stock for Order #{order.Id}");
        return reserved;
    }

    public void ReleaseStock(Order order)
    {
        Simulate.DbWrite(Name, $"Releasing stock of Order #{order.Id}", () =>
        {
            _store.Update<Product>(DataFiles.Products, list =>
            {
                foreach (var item in order.Items)
                {
                    var product = list.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null) product.StockQuantity += item.Quantity;
                }
            });
        });

        Log.Success(Name, $"Stock released for Order #{order.Id}");
    }

    public string GetShortProductName(Order order)
    {
        string name = "unknown product";
        foreach (var item in order.Items)
        {
            var product = _products.GetById(item.ProductId);
            if (product != null && product.StockQuantity < item.Quantity) return product.Name;
            if (product != null) name = product.Name;
        }
        return name;
    }
}
