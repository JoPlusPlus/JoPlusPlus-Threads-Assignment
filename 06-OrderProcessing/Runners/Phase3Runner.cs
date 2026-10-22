using System.Diagnostics;
using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.Services;

namespace Mini_Project___Order_Processing_System.Runners;

public class Phase3Runner
{
    private readonly IncomingOrderRepository _incoming = new();
    private readonly JsonFileStore _store = new();

    // runs the 3 parts one after another
    public async Task RunAsync(CancellationToken ct)
    {
        await RunCheckoutAsync(ct);
        if (ct.IsCancellationRequested) return;

        await RunComparisonAsync(ct);
        if (ct.IsCancellationRequested) return;

        await RunDemoAsync(ct);
    }

    // ================= Task 9.3 / 9.4: full checkout of the 20 orders =================
    public async Task RunCheckoutAsync(CancellationToken ct)
    {
        int finished = 0;
        int cancelled = 0;

        try
        {
            Console.WriteLine("\n===== Phase 3: full checkout of all 20 orders =====");
            await DataSeeder.ResetAsync(ct);
            var orders = await _incoming.GetAllAsync(ct);
            var processor = BuildProcessor(true);

            // start every order
            var watch = Stopwatch.StartNew();
            var tasks = new List<Task<bool>>();
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;
                tasks.Add(processor.ProcessOrderAsync(order, ct));
            }

            // then await them one by one
            for (int i = 0; i < tasks.Count; i++)
            {
                await tasks[i];

                if (orders[i].Status == OrderStatus.Pending && orders[i].FailureReason == "Cancelled") cancelled++;
                else finished++;
            }
            watch.Stop();

            PrintTable(orders.Take(tasks.Count).ToList());
            Console.WriteLine($"Total time: {watch.Elapsed.TotalSeconds:F1} seconds");

            if (ct.IsCancellationRequested)
            {
                PrintCancelled(finished, cancelled);
                return;
            }

            await PrintFilesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(finished, cancelled);
        }
    }

    // ================= Task 9.6: sync (10 threads) vs async (10 tasks) =================
    public async Task RunComparisonAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine("\n===== Phase 3: sync vs async, orders #1-#10, no failures =====");

            // ---------- sync: every order on its own Thread ----------
            await DataSeeder.ResetAsync(ct);
            var syncOrders = (await _incoming.GetAllAsync(ct)).Where(o => o.Id <= 10).ToList();
            var syncProcessor = new SyncOrderProcessor();

            Log.ResetThreads();
            var watch = Stopwatch.StartNew();
            var threads = new List<Thread>();
            foreach (var order in syncOrders)
            {
                var thread = new Thread(() => syncProcessor.ProcessOrder(order));
                threads.Add(thread);
                thread.Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            watch.Stop();

            double syncSeconds = watch.Elapsed.TotalSeconds;
            int syncThreads = Log.DistinctThreads;
            int syncPool = ThreadPool.ThreadCount;
            var syncSaved = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);

            // ---------- async: 10 tasks ----------
            await DataSeeder.ResetAsync(ct);
            var asyncOrders = (await _incoming.GetAllAsync(ct)).Where(o => o.Id <= 10).ToList();
            var asyncProcessor = BuildProcessor(false);

            Log.ResetThreads();
            watch.Restart();
            var tasks = new List<Task<bool>>();
            foreach (var order in asyncOrders)
            {
                tasks.Add(asyncProcessor.ProcessOrderAsync(order, ct));
            }
            foreach (var task in tasks)
            {
                await task;
            }
            watch.Stop();

            double asyncSeconds = watch.Elapsed.TotalSeconds;
            int asyncThreads = Log.DistinctThreads;
            int asyncPool = ThreadPool.ThreadCount;
            var asyncSaved = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);

            Console.WriteLine();
            Console.WriteLine("===== Sync vs Async (10 orders) =====");
            Console.WriteLine($"Sync : {syncSeconds:F1} s | threads that logged: {syncThreads} | ThreadPool.ThreadCount: {syncPool} | Completed {Count(syncSaved, OrderStatus.Completed)}, Rejected {Count(syncSaved, OrderStatus.Rejected)}");
            Console.WriteLine($"Async: {asyncSeconds:F1} s | threads that logged: {asyncThreads} | ThreadPool.ThreadCount: {asyncPool} | Completed {Count(asyncSaved, OrderStatus.Completed)}, Rejected {Count(asyncSaved, OrderStatus.Rejected)}");
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(0, 0);
        }
    }

    // ================= Task 9.7: fire-and-forget =================
    public async Task RunDemoAsync(CancellationToken ct)
    {
        try
        {
            // order #2 always fails to send its email
            var email = new EmailService(new Dictionary<int, int> { [2] = int.MaxValue });
            await new FireAndForgetDemo(email).RunAsync(ct);
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(0, 0);
        }
    }

    // ================= helpers =================

    // withFailures = true  -> the controlled failures of Task 9.2
    // withFailures = false -> no failures at all (Task 9.6)
    private OrderProcessor BuildProcessor(bool withFailures)
    {
        IPaymentGateway payment;
        OrderRepository orders;
        IEmailService email;

        if (withFailures)
        {
            payment = new PaymentGateway(timeoutOrderIds: new[] { 5 }, declineOrderIds: new[] { 8 });
            orders = new OrderRepository(failSaveForOrderIds: new[] { 13 });
            email = new EmailService(new Dictionary<int, int> { [3] = 2, [17] = int.MaxValue });
        }
        else
        {
            payment = new PaymentGateway();
            orders = new OrderRepository();
            email = new EmailService();
        }

        // no shipping failures in Phase 3
        var pricing = new PricingService(new List<int>());

        return new OrderProcessor(new CustomerService(), new InventoryService(), pricing, payment, orders, email);
    }

    private int Count(List<Order> orders, OrderStatus status)
    {
        return orders.Count(o => o.Status == status);
    }

    private void PrintTable(List<Order> orders)
    {
        Console.WriteLine();
        Console.WriteLine("Order  Status         Total  Reason");
        Console.WriteLine("-----  -------------  ---------  ------");
        foreach (var order in orders.OrderBy(o => o.Id))
        {
            Console.WriteLine($"#{order.Id,-4}  {order.Status,-13}  {order.TotalAmount,9:F2}  {order.FailureReason}");
        }
    }

    // reads the files back and prints what is inside (compare with Task 9.4)
    private async Task PrintFilesAsync(CancellationToken ct)
    {
        var orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        var payments = await _store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, ct);
        var emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        var retry = await _store.ReadAllAsync<EmailMessage>(DataFiles.EmailRetry, ct);
        var products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);

        Console.WriteLine();
        Console.WriteLine("===== Files after the run =====");
        Console.WriteLine($"orders.json:      {orders.Count} records | Completed {Count(orders, OrderStatus.Completed)}, Rejected {Count(orders, OrderStatus.Rejected)}, PaymentFailed {Count(orders, OrderStatus.PaymentFailed)}, Failed {Count(orders, OrderStatus.Failed)}, Pending {Count(orders, OrderStatus.Pending)}");
        Console.WriteLine($"payments.json:    {payments.Count} records | Captured {payments.Count(p => p.Status == PaymentStatus.Captured)}, Declined {payments.Count(p => p.Status == PaymentStatus.Declined)}, Refunded {payments.Count(p => p.Status == PaymentStatus.Refunded)}");

        var email3 = emails.FirstOrDefault(e => e.OrderId == 3);
        Console.WriteLine($"emails.json:      {emails.Count} records | Order #3 Attempts = {email3?.Attempts}");

        var retry17 = retry.FirstOrDefault(e => e.OrderId == 17);
        Console.WriteLine($"email_retry.json: {retry.Count} records | Order #17 Attempts = {retry17?.Attempts}, LastError = {retry17?.LastError}");

        var keyboard = products.First(p => p.Id == 4);
        Console.WriteLine($"products.json:    {keyboard.Name} stock = {keyboard.StockQuantity}");
    }

    private void PrintCancelled(int finished, int cancelled)
    {
        Console.WriteLine($"\nRun cancelled — {finished} orders finished, {cancelled} cancelled.");
    }
}
