namespace Mini_Project___Order_Processing_System.Services;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct);
    Task RefundAsync(string transactionId, CancellationToken ct);
}
