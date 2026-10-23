using System.Diagnostics;
using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;
using Mini_Project___Order_Processing_System.Services;

namespace Mini_Project___Order_Processing_System.Runners;

public class Phase4Runner
{
    private readonly IncomingOrderRepository _incoming = new();
    private readonly CustomerRepository _customers = new();
    private readonly JsonFileStore _store = new();

    // runs the 5 parts one after another (10.2 needs the files of 10.1)
    public async Task RunAsync(CancellationToken ct)
    {
        await RunProgressAsync(ct);
        if (ct.IsCancellationRequested) return;

        await RunDashboardAsync(ct);
        if (ct.IsCancellationRequested) return;

        await RunShippingAsync(ct);
        if (ct.IsCancellationRequested) return;

        await RunFraudAsync(ct);
        if (ct.IsCancellationRequested) return;

        await RunEmailResendAsync(ct);
    }

    // ================= Task 10.1: live checkout progress =================
    public async Task RunProgressAsync(CancellationToken ct)
    {
        int finished = 0;
        int cancelled = 0;

        try
        {
            Console.WriteLine("\n===== Task 10.1: live checkout progress =====");
            await DataSeeder.ResetAsync(ct);
            var orders = await _incoming.GetAllAsync(ct);

            // the same processor and the same failures as Task 9.2
            var processor = new Phase3Runner().BuildProcessor(true);

            // start all the orders at once
            var tasks = new List<Task<(Order Order, double Seconds)>>();
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;
                tasks.Add(RunOneOrderAsync(processor, order, ct));
            }

            // Task.WhenEach gives me every task at the moment it finishes
            int done = 0;
            await foreach (var task in Task.WhenEach(tasks))
            {
                var result = await task;
                done++;

                if (result.Order.Status == OrderStatus.Pending && result.Order.FailureReason == "Cancelled") cancelled++;
                else finished++;

                string detail = "";
                if (result.Order.FailureReason != null) detail = $" ({result.Order.FailureReason})";
                else if (result.Order.Status == OrderStatus.Completed) detail = $" (total {result.Order.TotalAmount:F2})";

                Console.WriteLine($"[{done,2}/{tasks.Count}] Order #{result.Order.Id} -> {result.Order.Status}{detail} after {result.Seconds:F1} s");
            }

            // count per status
            Console.WriteLine();
            Console.WriteLine("Orders per status:");
            foreach (var group in orders.GroupBy(o => o.Status).OrderBy(g => g.Key))
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }

            if (ct.IsCancellationRequested) PrintCancelled(finished, cancelled);
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(finished, cancelled);
        }
    }

    // starts one order and measures its own time
    private async Task<(Order Order, double Seconds)> RunOneOrderAsync(OrderProcessor processor, Order order, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        await processor.ProcessOrderAsync(order, ct);
        watch.Stop();
        return (order, watch.Elapsed.TotalSeconds);
    }

    // ================= Task 10.2: customer dashboard =================
    public async Task RunDashboardAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine("\n===== Task 10.2: customer dashboard =====");

            // the payments load fails for customer 99
            var service = new CustomerDashboardService(failPaymentsForCustomerIds: new[] { 99 });

            foreach (int customerId in new[] { 3, 99 })
            {
                Console.WriteLine();
                var watch = Stopwatch.StartNew();
                try
                {
                    var dashboard = await service.LoadAsync(customerId, ct);
                    watch.Stop();
                    Console.WriteLine(DashboardText(dashboard));
                }
                catch (AggregateException ex)
                {
                    watch.Stop();
                    Console.WriteLine($"Customer #{customerId}: {ex.InnerExceptions.Count} failure(s)");
                    foreach (var inner in ex.InnerExceptions)
                    {
                        Console.WriteLine($"  - {inner.Message}");
                    }
                }
                Console.WriteLine($"Dashboard time: {watch.Elapsed.TotalSeconds:F1} s (the 4 loads ran at the same time)");
            }
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(0, 0);
        }
    }

    private string DashboardText(CustomerDashboard d)
    {
        string orders = string.Join(", ", d.Orders.Select(o => $"#{o.Id} {o.Status}"));
        string payments = string.Join(", ", d.Payments.Select(p => $"#{p.OrderId} {p.Status}"));
        string emails = string.Join(", ", d.Emails.Select(e => $"#{e.OrderId}"));

        return $"Customer #{d.Customer.Id} {d.Customer.Name} — {d.Orders.Count} {Word(d.Orders.Count, "order")} ({orders}), " +
               $"{d.Payments.Count} {Word(d.Payments.Count, "payment")} ({payments}), " +
               $"{d.Emails.Count} {Word(d.Emails.Count, "email")} ({emails})";
    }

    private string Word(int count, string word)
    {
        if (count == 1) return word;
        return word + "s";
    }

    // ================= Task 10.3: fastest shipping quote =================
    public async Task RunShippingAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine("\n===== Task 10.3: fastest shipping quote =====");
            var orders = await _incoming.GetAllAsync(ct);

            // normal run, orders #1 to #5
            var service = new ShippingQuoteService(new IShippingCarrier[] { new FastShip(), new CairoExpress(), new NileCourier() });

            var results = new List<(Order Order, ShippingQuote Quote)>();
            foreach (var order in orders.Where(o => o.Id <= 5))
            {
                Console.WriteLine();
                var quote = await service.GetFastestQuoteAsync(order, ct);
                results.Add((order, quote));
            }

            Console.WriteLine();
            Console.WriteLine("Order  Carrier       Price    Time     Cancelled carriers");
            Console.WriteLine("-----  ------------  -------  -------  ------------------");
            foreach (var r in results)
            {
                string cancelledText = r.Quote.CancelledCarriers.Count == 0 ? "-" : string.Join(", ", r.Quote.CancelledCarriers);
                Console.WriteLine($"#{r.Order.Id,-4}  {r.Quote.Carrier,-12}  {r.Quote.Price,7:F2}  {r.Quote.ElapsedMs,4} ms  {cancelledText}");
            }

            // separate run: every carrier fails for order #6
            Console.WriteLine();
            Console.WriteLine("--- every carrier fails for order #6 ---");
            var failing = new ShippingQuoteService(new IShippingCarrier[]
            {
                new FastShip(new[] { 6 }),
                new CairoExpress(new[] { 6 }),
                new NileCourier(new[] { 6 })
            });
            try
            {
                await failing.GetFastestQuoteAsync(orders.First(o => o.Id == 6), ct);
            }
            catch (ShippingProviderUnavailableException ex)
            {
                Console.WriteLine($"Order #6 -> ShippingProviderUnavailableException: {ex.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(0, 0);
        }
    }

    // ================= Task 10.4 A: fraud scoring (CPU-bound) =================
    public async Task RunFraudAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine("\n===== Task 10.4 A: fraud scoring of 10,000 orders =====");

            var orders = new List<Order>();
            for (int id = 1; id <= 10000; id++)
            {
                orders.Add(new Order { Id = id });
            }

            var service = new FraudScoringService();

            var watch = Stopwatch.StartNew();
            var sequential = service.ScoreSequential(orders, ct);
            watch.Stop();
            double sequentialSeconds = watch.Elapsed.TotalSeconds;

            watch.Restart();
            var parallel = await service.ScoreParallelAsync(orders, ct);
            watch.Stop();
            double parallelSeconds = watch.Elapsed.TotalSeconds;

            Console.WriteLine($"Flagged orders:      {sequential.Count} (sequential) / {parallel.Flagged.Count} (parallel)");
            Console.WriteLine($"First 5 flagged ids: {string.Join(", ", parallel.Flagged.Take(5))}");
            Console.WriteLine($"Same ids in both:    {sequential.SequenceEqual(parallel.Flagged)}");
            Console.WriteLine($"Sequential time:     {sequentialSeconds:F1} s");
            Console.WriteLine($"Parallel time:       {parallelSeconds:F1} s");
            Console.WriteLine($"Speed-up:            {sequentialSeconds / parallelSeconds:F1}x");
            Console.WriteLine($"Distinct threads:    {parallel.Threads}");
            Console.WriteLine($"ProcessorCount:      {Environment.ProcessorCount}");
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(0, 0);
        }
    }

    // ================= Task 10.4 B: email re-send (I/O-bound) =================
    public async Task RunEmailResendAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine("\n===== Task 10.4 B: re-send 30 emails, 4 at a time =====");

            var customer = await _customers.GetByIdAsync(1, ct);
            if (customer == null) return;

            // 30 test emails, order ids 1001 to 1030
            var orders = new List<Order>();
            for (int id = 1001; id <= 1030; id++)
            {
                orders.Add(new Order { Id = id, CustomerId = customer.Id, TotalAmount = 100 });
            }

            var service = new EmailResendService(new EmailService());

            var watch = Stopwatch.StartNew();
            int highest = await service.ResendAsync(orders, customer, ct);
            watch.Stop();

            // every test email must be in emails.json exactly once
            var emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
            var testEmails = emails.Where(e => e.OrderId >= 1001 && e.OrderId <= 1030).ToList();

            Console.WriteLine($"Total time:                      {watch.Elapsed.TotalSeconds:F1} s");
            Console.WriteLine($"Highest sends at the same time:  {highest}");
            Console.WriteLine($"Test emails in emails.json:      {testEmails.Count} (distinct order ids: {testEmails.Select(e => e.OrderId).Distinct().Count()})");
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(0, 0);
        }
    }

    private void PrintCancelled(int finished, int cancelled)
    {
        Console.WriteLine($"\nRun cancelled — {finished} orders finished, {cancelled} cancelled.");
    }
}
