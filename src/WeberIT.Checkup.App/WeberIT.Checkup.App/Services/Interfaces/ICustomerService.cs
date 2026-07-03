using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICustomerService
{
    IEnumerable<Customer> GetCustomers();

    void CreateCustomer(Customer customer);

    void UpdateCustomer(Customer customer);

    void DeleteCustomer(Guid customerId);
}