using Mini_Project___Order_Processing_System.Data;

namespace Mini_Project___Order_Processing_System.Services;

public class SyncEmailService
{
    private const string Name = "SyncEmailService";
    private readonly JsonFileStore _store = new();

    public void SendConfirmation(Order order, Customer customer)
    {
        Simulate.Step(Name, $"Rendering template for Order #{order.Id}...", 200);
        Simulate.Step(Name, "Connecting to SMTP...", 300);
        Simulate.Step(Name, "Sending...", 500);

        var email = new EmailMessage();
        email.Id = Guid.NewGuid();
        email.OrderId = order.Id;
        email.To = customer.Email;
        email.Subject = $"Order #{order.Id} confirmation";
        email.Body = $"Hello {customer.Name}, your order #{order.Id} is confirmed. Total: {order.TotalAmount}";
        email.SentAt = DateTime.Now;
        email.Attempts = 1;
        _store.Add(DataFiles.Emails, email);

        Log.Success(Name, $"Email for Order #{order.Id} sent to {customer.Email}");
    }
}
