using Mini_Project___Order_Processing_System.MyExceptions;

namespace Mini_Project___Order_Processing_System.Services;

public class CustomerService
{
    private readonly CustomerRepository _customers = new();

    // DB query (the repository has the delays). Throws if the customer does not exist.
    public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(id, ct);
        if (customer == null)
        {
            throw new CustomerNotFoundException($"Customer #{id} not found");
        }
        return customer;
    }
}
