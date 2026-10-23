using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;

namespace Mini_Project___Order_Processing_System.Services;

public class CustomerDashboardService
{
    private const string Name = "CustomerDashboardService";
    private readonly JsonFileStore _store = new();
    private readonly CustomerRepository _customers = new();
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _failPaymentsForCustomerIds = new HashSet<int>(failPaymentsForCustomerIds ?? new List<int>());
    }

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct)
    {
        // start the 4 loads, they all run at the same time
        Task<Customer> profileTask = LoadProfileAsync(customerId, ct);
        Task<List<Order>> ordersTask = LoadOrdersAsync(customerId, ct);
        Task<List<PaymentTransaction>> paymentsTask = LoadPaymentsAsync(customerId, ct);
        Task<List<EmailMessage>> emailsTask = LoadEmailsAsync(customerId, ct);

        // ONE await for all 4
        Task all = Task.WhenAll(profileTask, ordersTask, paymentsTask, emailsTask);
        try
        {
            await all;
        }
        catch
        {
            // a plain await only throws the FIRST exception.
            // The task "all" keeps every exception, so I throw all of them.
            if (all.Exception != null) throw all.Exception;
            throw;
        }

        // the 4 tasks are finished, so these awaits return immediately
        var dashboard = new CustomerDashboard();
        dashboard.Customer = await profileTask;
        dashboard.Orders = await ordersTask;
        dashboard.Payments = await paymentsTask;
        dashboard.Emails = await emailsTask;
        return dashboard;
    }

    private async Task<Customer> LoadProfileAsync(int customerId, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer == null)
        {
            throw new CustomerNotFoundException($"Customer #{customerId} not found");
        }
        return customer;
    }

    private Task<List<Order>> LoadOrdersAsync(int customerId, CancellationToken ct)
    {
        return Simulate.DbQueryAsync<List<Order>>(Name, $"orders of Customer #{customerId}", async token =>
        {
            var orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, token);
            return orders.Where(o => o.CustomerId == customerId).OrderBy(o => o.Id).ToList();
        }, ct);
    }

    private Task<List<PaymentTransaction>> LoadPaymentsAsync(int customerId, CancellationToken ct)
    {
        return Simulate.DbQueryAsync<List<PaymentTransaction>>(Name, $"payments of Customer #{customerId}", async token =>
        {
            // controlled failure
            if (_failPaymentsForCustomerIds.Contains(customerId))
            {
                Log.Error(Name, "Payments store timeout");
                throw new TimeoutException("Payments store timeout");
            }

            // a payment knows its order, not its customer
            var orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, token);
            var orderIds = orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToList();

            var payments = await _store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, token);
            return payments.Where(p => orderIds.Contains(p.OrderId)).OrderBy(p => p.OrderId).ToList();
        }, ct);
    }

    private Task<List<EmailMessage>> LoadEmailsAsync(int customerId, CancellationToken ct)
    {
        return Simulate.DbQueryAsync<List<EmailMessage>>(Name, $"emails of Customer #{customerId}", async token =>
        {
            var orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, token);
            var orderIds = orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToList();

            var emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, token);
            return emails.Where(e => orderIds.Contains(e.OrderId)).OrderBy(e => e.OrderId).ToList();
        }, ct);
    }
}
