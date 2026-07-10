using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICustomerService
{
    IEnumerable<Customer> GetCustomers();

    Customer? GetCustomerById(Guid customerId);

    void CreateCustomer(Customer customer);

    void UpdateCustomer(Customer customer);

    void DeleteCustomer(Guid customerId);

    void AddDeviceToCustomer(Guid customerId, CustomerDevice device);

    void UpdateCustomerDevice(Guid customerId, CustomerDevice device);

    void DeleteCustomerDevice(Guid customerId, Guid deviceId);
}