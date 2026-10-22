using Mini_Project___Order_Processing_System.Data;

namespace Mini_Project___Order_Processing_System.Services;

public class SyncPaymentGateway
{
    private const string Name = "SyncPaymentGateway";
    private readonly JsonFileStore _store = new();

    public PaymentResult ProcessPayment(int orderId, decimal amount)
    {
        Simulate.Step(Name, $"Authorizing payment for Order #{orderId}...", 800);
        Simulate.Step(Name, "Fraud check...", 500);
        Simulate.Step(Name, "Capturing payment...", 700);

        string transactionId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        var payment = new PaymentTransaction();
        payment.OrderId = orderId;
        payment.Amount = amount;
        payment.Status = PaymentStatus.Captured;
        payment.TransactionId = transactionId;
        payment.CreatedAt = DateTime.Now;
        _store.Add(DataFiles.Payments, payment);

        Log.Success(Name, $"Payment for Order #{orderId} captured ({transactionId})");

        var result = new PaymentResult();
        result.Success = true;
        result.Status = PaymentStatus.Captured;
        result.TransactionId = transactionId;
        return result;
    }
}
