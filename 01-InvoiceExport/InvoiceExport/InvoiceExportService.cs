using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public async Task ExportAllPending()
    {
        var watch = Stopwatch.StartNew();
        List<Order> orders = await _db.GetPendingOrders();
        var tasks = new List<Task>();

        foreach (Order order in orders)
        {
            tasks.Add(StartThis(order)); // start all orders
        }
        
        await Task.WhenAll(tasks); // then wait only 0.6 for all

        // Honestly I used AI Here when i noticed the Time is still 3.9s , and it told me to create another method to allow the 
        // loop start all orders at same time and avoiding waiting for each one to finish individually
            // now all ReadProfile -> 0.3s (since all start together)
            // all WriteInvoice -> 0.3s
            // 0.8s to fetch all orders 
            // making a total of 1.4s 

        Console.WriteLine($"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }
    public async Task StartThis(Order order)
    {
        CustomerProfile customer = await _customers.ReadProfile(order.CustomerId);
        string path = await _invoices.WriteInvoice(order, customer);
        Console.WriteLine($"Wrote {path}");
    }
}
