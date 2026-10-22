using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;
using Mini_Project___Order_Processing_System.Repositories;

namespace Mini_Project___Order_Processing_System.Services;

// The same workflow as OrderProcessor, but with Thread.Sleep and no failures.
// Used only for the sync vs async comparison (each order runs on its own Thread).
public class SyncOrderProcessor
{
    private readonly SyncCustomerService _customers = new();
    private readonly SyncInventoryService _inventory = new();
    private readonly SyncPricingService _pricing = new();
    private readonly SyncPaymentGateway _payment = new();
    private readonly SyncOrderRepository _orders = new();
    private readonly SyncEmailService _email = new();

    public bool ProcessOrder(Order order)
    {
        string who = $"Order #{order.Id}";

        // 1. customer
        Customer customer;
        try
        {
            customer = _customers.GetCustomer(order.CustomerId);
        }
        catch (CustomerNotFoundException ex)
        {
            return End(order, OrderStatus.Rejected, ex.Message);
        }
        if (customer.IsActive == false) return End(order, OrderStatus.Rejected, "Customer is inactive");

        // 2. stock
        if (_inventory.ReserveStock(order) == false)
        {
            string name = _inventory.GetShortProductName(order);
            return End(order, OrderStatus.Rejected, "Insufficient stock for '" + name + "'");
        }

        // 3. pricing
        try
        {
            _pricing.CalculateTotal(order);
        }
        catch (InvalidCouponException ex)
        {
            _inventory.ReleaseStock(order);
            return End(order, OrderStatus.Rejected, ex.Message);
        }

        // 4. payment
        var payment = _payment.ProcessPayment(order.Id, order.TotalAmount);
        order.TransactionId = payment.TransactionId;
        Log.Success(who, $"Step 4/6 Payment ... OK ({payment.TransactionId})");

        // 5. save
        order.Status = OrderStatus.Completed;
        order.FailureReason = null;
        _orders.Save(order);

        // 6. email
        _email.SendConfirmation(order, customer);
        Log.Success(who, "Step 6/6 Email ... OK");

        return true;
    }

    private bool End(Order order, OrderStatus status, string reason)
    {
        order.Status = status;
        order.FailureReason = reason;
        _orders.Save(order);
        Log.Error($"Order #{order.Id}", $"{status}: {reason}");
        return false;
    }
}
