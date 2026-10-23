namespace Mini_Project___Order_Processing_System.Services;

// I/O-bound work: every send mostly waits, so many sends can run together
public class EmailResendService
{
    private readonly IEmailService _email;

    public EmailResendService(IEmailService email)
    {
        _email = email;
    }

    // sends all the emails, at most 4 at the same time. Returns the highest number running together.
    public async Task<int> ResendAsync(List<Order> orders, Customer customer, CancellationToken ct)
    {
        object gate = new object();
        int running = 0;
        int highest = 0;

        var options = new ParallelOptions();
        options.MaxDegreeOfParallelism = 4;     // the limit comes from the library
        options.CancellationToken = ct;

        await Parallel.ForEachAsync(orders, options, async (order, token) =>
        {
            // only to measure (this is not the limit)
            lock (gate)
            {
                running++;
                if (running > highest) highest = running;
            }

            try
            {
                await _email.SendConfirmationAsync(order, customer, token);
            }
            finally
            {
                lock (gate)
                {
                    running--;
                }
            }
        });

        return highest;
    }
}
