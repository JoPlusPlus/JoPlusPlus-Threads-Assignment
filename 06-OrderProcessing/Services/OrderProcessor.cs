using System.Diagnostics;
using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;

namespace Mini_Project___Order_Processing_System.Services;

public class OrderProcessor
{
    private readonly CustomerService _customers;
    private readonly InventoryService _inventory;
    private readonly PricingService _pricing;
    private readonly IPaymentGateway _payment;
    private readonly OrderRepository _orders;
    private readonly IEmailService _email;

    // all the services come from outside (manual constructor injection)
    public OrderProcessor(CustomerService customers, InventoryService inventory, PricingService pricing,
        IPaymentGateway payment, OrderRepository orders, IEmailService email)
    {
        _customers = customers;
        _inventory = inventory;
        _pricing = pricing;
        _payment = payment;
        _orders = orders;
        _email = email;
    }

    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct)
    {
        bool stockReserved = false;
        string? transactionId = null;
        var watch = new Stopwatch();

        try
        {
            // ===== Step 1: customer =====
            watch.Restart();
            Customer customer;
            try
            {
                customer = await _customers.GetCustomerAsync(order.CustomerId, ct);
            }
            catch (CustomerNotFoundException ex)
            {
                StepFailed(order, "Step 1/6 Customer", ex.Message, watch);
                return await EndAsync(order, OrderStatus.Rejected, ex.Message);
            }

            if (customer.IsActive == false)
            {
                StepFailed(order, "Step 1/6 Customer", "Customer is inactive", watch);
                return await EndAsync(order, OrderStatus.Rejected, "Customer is inactive");
            }
            StepOk(order, "Step 1/6 Customer", $"({customer.Name})", watch);

            // ===== Step 2: reserve the stock =====
            watch.Restart();
            stockReserved = await _inventory.ReserveStockAsync(order, ct);
            if (stockReserved == false)
            {
                string productName = await _inventory.GetShortProductNameAsync(order, ct);
                string reason = "Insufficient stock for '" + productName + "'";
                StepFailed(order, "Step 2/6 Stock", reason, watch);
                return await EndAsync(order, OrderStatus.Rejected, reason);
            }
            StepOk(order, "Step 2/6 Stock", "", watch);

            // ===== Step 3: price the order =====
            watch.Restart();
            try
            {
                await _pricing.CalculateTotalAsync(order, ct);
            }
            catch (InvalidCouponException ex)
            {
                StepFailed(order, "Step 3/6 Pricing", ex.Message, watch);
                await _inventory.ReleaseStockAsync(order, CancellationToken.None);
                stockReserved = false;
                return await EndAsync(order, OrderStatus.Rejected, ex.Message);
            }
            StepOk(order, "Step 3/6 Pricing", $"(total {order.TotalAmount})", watch);

            // ===== Step 4: payment, stops after 3 seconds or when the run is cancelled =====
            watch.Restart();
            PaymentResult payment;
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    payment = await _payment.ProcessPaymentAsync(order.Id, order.TotalAmount, timeoutSource.Token);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested == false)
                {
                    // only our 3 second timer fired, the run itself was not cancelled
                    Log.Error($"Order #{order.Id}", $"Step 4/6 Payment ... TIMEOUT after {watch.ElapsedMilliseconds} ms, released");
                    await _inventory.ReleaseStockAsync(order, CancellationToken.None);
                    stockReserved = false;
                    return await EndAsync(order, OrderStatus.PaymentFailed, "Payment gateway timeout (3s)");
                }
            }

            if (payment.Success == false)
            {
                string reason = payment.Message ?? "Payment failed";
                StepFailed(order, "Step 4/6 Payment", reason, watch);
                await _inventory.ReleaseStockAsync(order, CancellationToken.None);
                stockReserved = false;
                return await EndAsync(order, OrderStatus.PaymentFailed, reason);
            }

            transactionId = payment.TransactionId;
            StepOk(order, "Step 4/6 Payment", $"({transactionId})", watch);

            // ===== Step 5: save the completed order =====
            watch.Restart();
            order.TransactionId = transactionId;
            order.Status = OrderStatus.Completed;
            order.FailureReason = null;
            try
            {
                await _orders.SaveAsync(order, ct);
            }
            catch (DatabaseWriteException ex)
            {
                // the customer paid but we could not save: undo everything
                StepFailed(order, "Step 5/6 Save", ex.Message, watch);
                await _payment.RefundAsync(transactionId!, CancellationToken.None);
                await _inventory.ReleaseStockAsync(order, CancellationToken.None);
                stockReserved = false;
                string reason = $"Database save failed after payment; payment {transactionId} refunded";
                transactionId = null;
                return await EndAsync(order, OrderStatus.Failed, reason);
            }
            StepOk(order, "Step 5/6 Save", "", watch);

            // ===== Step 6: confirmation email, it never changes the order status =====
            watch.Restart();
            try
            {
                await _email.SendConfirmationWithRetryAsync(order, customer, ct);
                StepOk(order, "Step 6/6 Email", "", watch);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Log.Info($"Order #{order.Id}", "Step 6/6 Email ... skipped (run cancelled), the order stays Completed");
            }
            catch (Exception ex)
            {
                StepFailed(order, "Step 6/6 Email", ex.Message, watch);
            }

            return order.Status == OrderStatus.Completed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // the run was cancelled: undo what this order did, it goes back to Pending
            Log.Info($"Order #{order.Id}", "Cancelled, undoing the work of this order");
            if (transactionId != null) await _payment.RefundAsync(transactionId, CancellationToken.None);
            if (stockReserved) await _inventory.ReleaseStockAsync(order, CancellationToken.None);
            order.TransactionId = null;
            return await EndAsync(order, OrderStatus.Pending, "Cancelled");
        }
        catch (Exception ex)
        {
            // something we did not expect: the order must still end with a status
            Log.Error($"Order #{order.Id}", $"Unexpected error: {ex.Message}");
            if (transactionId != null) await _payment.RefundAsync(transactionId, CancellationToken.None);
            if (stockReserved) await _inventory.ReleaseStockAsync(order, CancellationToken.None);
            return await EndAsync(order, OrderStatus.Failed, ex.Message);
        }
    }

    // sets the final status, saves the order and returns false
    // (CancellationToken.None: the final save must finish even when the run is cancelled)
    private async Task<bool> EndAsync(Order order, OrderStatus status, string reason)
    {
        order.Status = status;
        order.FailureReason = reason;
        await _orders.SaveAsync(order, CancellationToken.None);
        return false;
    }

    private void StepOk(Order order, string step, string detail, Stopwatch watch)
    {
        if (detail != "") detail = detail + " ";
        Log.Success($"Order #{order.Id}", $"{step} ... OK {detail}in {watch.ElapsedMilliseconds} ms");
    }

    private void StepFailed(Order order, string step, string reason, Stopwatch watch)
    {
        Log.Error($"Order #{order.Id}", $"{step} ... FAILED ({reason}) in {watch.ElapsedMilliseconds} ms");
    }
}
