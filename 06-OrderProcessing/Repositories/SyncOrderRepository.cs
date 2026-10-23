using Mini_Project___Order_Processing_System.Data;

namespace Mini_Project___Order_Processing_System.Repositories;

public class SyncOrderRepository
{
    private const string Name = "SyncOrderRepository";
    private readonly JsonFileStore _store = new();

    public void Save(Order order)
    {
        Simulate.DbWrite(Name, $"Saving Order #{order.Id}", () =>
        {
            _store.Update<Order>(DataFiles.Orders, list =>
            {
                int index = list.FindIndex(o => o.Id == order.Id);
                if (index >= 0) list[index] = order;
                else list.Add(order);
            });
        });

        Log.Success(Name, $"Order #{order.Id} saved ({order.Status})");
    }
}
