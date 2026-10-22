using System.Collections.Concurrent;
using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;

namespace Mini_Project___Order_Processing_System.Services;

public class EmailService : IEmailService
{
    private const string Name = "EmailService";
    private readonly JsonFileStore _store = new();

    // order id -> how many attempts must fail (example: [3] = 2 means attempts 1 and 2 fail)
    private readonly Dictionary<int, int> _failAttempts;

    // order id -> how many attempts were made so far
    private readonly ConcurrentDictionary<int, int> _attempts = new();

    public EmailService(IDictionary<int, int>? failAttempts = null)
    {
        _failAttempts = new Dictionary<int, int>(failAttempts ?? new Dictionary<int, int>());
    }

    public async Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct)
    {
        int attempt = _attempts.AddOrUpdate(order.Id, 1, (id, old) => old + 1);

        await Simulate.StepAsync(Name, $"Rendering template for Order #{order.Id} (attempt {attempt})...", 200, ct);
        await Simulate.StepAsync(Name, "Connecting to SMTP...", 300, ct);

        // controlled failure
        if (_failAttempts.TryGetValue(order.Id, out int failCount) && attempt <= failCount)
        {
            throw new SmtpUnavailableException($"SMTP server unavailable (attempt {attempt})");
        }

        await Simulate.StepAsync(Name, "Sending...", 500, ct);

        var email = BuildEmail(order, customer);
        email.Attempts = attempt;
        await _store.AddAsync(DataFiles.Emails, email, ct);

        Log.Success(Name, $"Email for Order #{order.Id} sent to {customer.Email}");
    }

    public async Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct)
    {
        int[] waitTimes = { 500, 1000, 2000 };
        string lastError = "";

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                await SendConfirmationAsync(order, customer, ct);
                return;
            }
            catch (SmtpUnavailableException ex)
            {
                lastError = ex.Message;
                Log.Error(Name, $"Email for Order #{order.Id} failed (attempt {attempt} of 4): {ex.Message}");
            }

            if (attempt < 4)
            {
                int wait = waitTimes[attempt - 1];
                await Simulate.StepAsync(Name, $"Waiting {wait} ms before retry {attempt}...", wait, ct);
            }
        }

        // all 4 attempts failed: keep the email for later and return normally
        var email = BuildEmail(order, customer);
        email.Attempts = 4;
        email.LastError = lastError;
        await _store.AddAsync(DataFiles.EmailRetry, email, ct);

        Log.Error(Name, $"Email for Order #{order.Id} queued for retry later");
    }

    private EmailMessage BuildEmail(Order order, Customer customer)
    {
        var email = new EmailMessage();
        email.Id = Guid.NewGuid();
        email.OrderId = order.Id;
        email.To = customer.Email;
        email.Subject = $"Order #{order.Id} confirmation";
        email.Body = $"Hello {customer.Name}, your order #{order.Id} is confirmed. Total: {order.TotalAmount}";
        email.SentAt = DateTime.Now;
        return email;
    }
}
