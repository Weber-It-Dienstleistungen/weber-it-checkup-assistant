using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICustomerService
{
    IEnumerable<Customer> GetCustomers();

    Customer? GetCustomerById(Guid customerId);

    void CreateCustomer(Customer customer);

    void UpdateCustomer(Customer customer);

    void DeleteCustomer(Guid customerId);

    bool AddDeviceToCustomer(
        Guid customerId,
        CustomerDevice device);

    bool UpdateCustomerDevice(
        Guid customerId,
        CustomerDevice device);

    bool DeleteCustomerDevice(
        Guid customerId,
        Guid deviceId);
}