using Mini_Project___Order_Processing_System.Data;

namespace Mini_Project___Order_Processing_System.Services;

public class PaymentGateway : IPaymentGateway
{
    private const string Name = "PaymentGateway";
    private readonly JsonFileStore _store = new();
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;

    // the controlled failures come from outside, this class never checks "order 5" by itself
    public PaymentGateway(IEnumerable<int>? timeoutOrderIds = null, IEnumerable<int>? declineOrderIds = null)
    {
        _timeoutOrderIds = new HashSet<int>(timeoutOrderIds ?? new List<int>());
        _declineOrderIds = new HashSet<int>(declineOrderIds ?? new List<int>());
    }

    public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct)
    {
        await Simulate.StepAsync(Name, $"Authorizing payment for Order #{orderId}...", 800, ct);
        await Simulate.StepAsync(Name, "Fraud check...", 500, ct);

        // declined (after the fraud check)
        if (_declineOrderIds.Contains(orderId))
        {
            var declined = new PaymentTransaction();
            declined.OrderId = orderId;
            declined.Amount = amount;
            declined.Status = PaymentStatus.Declined;
            declined.TransactionId = null;
            declined.CreatedAt = DateTime.Now;
            await _store.AddAsync(DataFiles.Payments, declined, ct);

            Log.Error(Name, $"Payment for Order #{orderId} declined");

            var declinedResult = new PaymentResult();
            declinedResult.Success = false;
            declinedResult.Status = PaymentStatus.Declined;
            declinedResult.Message = "Card declined by issuer";
            return declinedResult;
        }

        // capture (a timeout order waits 5000 ms instead of 700 ms)
        int captureMs = 700;
        if (_timeoutOrderIds.Contains(orderId)) captureMs = 5000;
        await Simulate.StepAsync(Name, "Capturing payment...", captureMs, ct);

        string transactionId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        var captured = new PaymentTransaction();
        captured.OrderId = orderId;
        captured.Amount = amount;
        captured.Status = PaymentStatus.Captured;
        captured.TransactionId = transactionId;
        captured.CreatedAt = DateTime.Now;
        await _store.AddAsync(DataFiles.Payments, captured, ct);

        Log.Success(Name, $"Payment for Order #{orderId} captured ({transactionId})");

        var result = new PaymentResult();
        result.Success = true;
        result.Status = PaymentStatus.Captured;
        result.TransactionId = transactionId;
        return result;
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct)
    {
        await Simulate.ExternalApiAsync(Name, $"refund {transactionId}", ct);

        await _store.UpdateAsync<PaymentTransaction>(DataFiles.Payments, list =>
        {
            var payment = list.FirstOrDefault(p => p.TransactionId == transactionId);
            if (payment != null) payment.Status = PaymentStatus.Refunded;
        }, ct);

        Log.Success(Name, $"Payment {transactionId} refunded");
    }
}
