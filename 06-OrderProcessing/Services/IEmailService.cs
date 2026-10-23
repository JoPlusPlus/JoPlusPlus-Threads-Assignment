namespace Mini_Project___Order_Processing_System.Services;

public interface IEmailService
{
    // one single attempt
    Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct);

    // 1 attempt + up to 3 retries (Task 9.5)
    Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct);
}
