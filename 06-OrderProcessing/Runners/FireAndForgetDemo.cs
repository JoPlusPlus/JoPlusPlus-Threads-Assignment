using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;
using Mini_Project___Order_Processing_System.Services;

namespace Mini_Project___Order_Processing_System.Runners;

// Task 9.7: the email is not awaited (broken) and then awaited (fixed)
public class FireAndForgetDemo
{
    private readonly OrderRepository _orderRepository = new();
    private readonly IncomingOrderRepository _incoming = new();
    private readonly CustomerRepository _customers = new();
    private readonly JsonFileStore _store = new();
    private readonly IEmailService _emailService;

    public FireAndForgetDemo(IEmailService emailService)
    {
        _emailService = emailService;
    }

    // THE BROKEN METHOD (the same as the assignment, I only added the ct parameter)
#pragma warning disable CS4014
    public async Task ProcessOrderAsync(Order order, Customer customer, CancellationToken ct)
    {
        await _orderRepository.SaveAsync(order, ct);
        _emailService.SendConfirmationAsync(order, customer, ct); // not awaited
        Console.WriteLine("Order processing finished");
    }
#pragma warning restore CS4014

    // THE FIX: await the email. A failed email is logged, the saved order stays as it is.
    public async Task ProcessOrderFixedAsync(Order order, Customer customer, CancellationToken ct)
    {
        await _orderRepository.SaveAsync(order, ct);
        try
        {
            await _emailService.SendConfirmationAsync(order, customer, ct);
        }
        catch (SmtpUnavailableException ex)
        {
            Log.Error("FireAndForgetDemo", $"The caller can see the email error: {ex.Message}");
        }
        Console.WriteLine("Order processing finished");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 9.7: fire-and-forget investigation =====");

        await DataSeeder.ResetAsync(ct);
        var orders = await _incoming.GetAllAsync(ct);
        var order1 = orders.First(o => o.Id == 1);
        var order2 = orders.First(o => o.Id == 2);
        var customer1 = (await _customers.GetByIdAsync(1, ct))!;
        var customer2 = (await _customers.GetByIdAsync(2, ct))!;

        // ---------- BROKEN 1: the email is still being sent when the method returns ----------
        Console.WriteLine("\n--- BROKEN 1: the method returns while the email is still being sent ---");
        await ProcessOrderAsync(order1, customer1, ct);
        var emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        Console.WriteLine($"emails.json right after the method returned: {emails.Count} record(s)");
        Console.WriteLine("If the console app exited here, the email would be lost.");
        await Task.Delay(2000, ct);
        emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        Console.WriteLine($"emails.json 2 seconds later: {emails.Count} record(s)");

        // ---------- BROKEN 2: the email throws ----------
        Console.WriteLine("\n--- BROKEN 2: the email throws an exception ---");
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (sender, e) =>
        {
            Log.Error("FireAndForgetDemo", "UnobservedTaskException: " + (e.Exception.InnerException?.Message ?? e.Exception.Message));
            e.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += handler;

        await ProcessOrderAsync(order2, customer2, ct);
        await Task.Delay(2500, ct);
        Console.WriteLine("No exception reached the caller, nobody knows the email failed.");
        Console.WriteLine("Forcing the garbage collector to show the lost exception...");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(500, ct);

        TaskScheduler.UnobservedTaskException -= handler;

        // ---------- FIXED ----------
        Console.WriteLine("\n--- FIXED: the email is awaited ---");
        await DataSeeder.ResetAsync(ct);
        await ProcessOrderFixedAsync(order1, customer1, ct);
        emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        Console.WriteLine($"emails.json right after the method returned: {emails.Count} record(s)");

        Console.WriteLine("\n--- FIXED: the email throws ---");
        await ProcessOrderFixedAsync(order2, customer2, ct);
    }
}
