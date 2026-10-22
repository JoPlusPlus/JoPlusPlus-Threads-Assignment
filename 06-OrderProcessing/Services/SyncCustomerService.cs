using Mini_Project___Order_Processing_System.MyExceptions;
using Mini_Project___Order_Processing_System.Repositories;

namespace Mini_Project___Order_Processing_System.Services;

public class SyncCustomerService
{
    private readonly SyncCustomerRepository _customers = new();

    public Customer GetCustomer(int id)
    {
        var customer = _customers.GetById(id);
        if (customer == null)
        {
            throw new CustomerNotFoundException($"Customer #{id} not found");
        }
        return customer;
    }
}
