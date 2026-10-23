using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;

namespace Mini_Project___Order_Processing_System;

public class OrderRepository
{
    private const string Name = "OrderRepository";
    private readonly JsonFileStore _store = new();
    private readonly HashSet<int> _failSaveForOrderIds;

    // Phase 3: saving these orders (with Status Completed) throws DatabaseWriteException
    public OrderRepository(IEnumerable<int>? failSaveForOrderIds = null)
    {
        _failSaveForOrderIds = new HashSet<int>(failSaveForOrderIds ?? new List<int>());
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        await Simulate.DbWriteAsync(Name, $"Saving Order #{order.Id}", async token =>
        {
            // controlled failure: nothing is written
            if (_failSaveForOrderIds.Contains(order.Id) && order.Status == OrderStatus.Completed)
            {
                Log.Error(Name, $"Connection lost while saving Order #{order.Id}");
                throw new DatabaseWriteException("Connection lost during commit");
            }

            await _store.UpdateAsync<Order>(DataFiles.Orders, list =>
            {
                int index = list.FindIndex(o => o.Id == order.Id);
                if (index >= 0) list[index] = order;
                else list.Add(order);
            }, token);
        }, ct);

        Log.Success(Name, $"Order #{order.Id} saved ({order.Status})");
    }

    public Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        return Simulate.DbQueryAsync<List<Order>>(Name, "all orders", async token =>
        {
            var orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, token);
            Log.Success(Name, $"Found {orders.Count} order(s)");
            return orders;
        }, ct);
    }

    public Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct)
    {
        return Simulate.DbQueryAsync<List<Order>>(Name, $"orders with status {status}", async token =>
        {
            var orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, token);
            var result = orders.Where(o => o.Status == status).ToList();
            Log.Success(Name, $"Found {result.Count} order(s) with status {status}");
            return result;
        }, ct);
    }
}
