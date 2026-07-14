using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Repositories.Interfaces;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public IEnumerable<Customer> GetCustomers()
    {
        return _customerRepository.GetAll();
    }

    public Customer? GetCustomerById(Guid customerId)
    {
        return _customerRepository.GetById(customerId);
    }

    public void CreateCustomer(Customer customer)
    {
        customer.CustomerNumber = GenerateCustomerNumber();
        customer.CreatedAt = DateTime.Now;
        customer.UpdatedAt = null;

        _customerRepository.Add(customer);
    }

    public void UpdateCustomer(Customer customer)
    {
        customer.UpdatedAt = DateTime.Now;

        _customerRepository.Update(customer);
    }

    public void DeleteCustomer(Guid customerId)
    {
        _customerRepository.Delete(customerId);
    }

    public bool AddDeviceToCustomer(
        Guid customerId,
        CustomerDevice device)
    {
        var customer = _customerRepository.GetById(customerId);

        if (customer is null)
        {
            return false;
        }

        device.CreatedAt = DateTime.Now;
        device.UpdatedAt = null;

        customer.Devices.Add(device);
        customer.UpdatedAt = DateTime.Now;

        _customerRepository.Update(customer);

        return true;
    }

    public bool UpdateCustomerDevice(
        Guid customerId,
        CustomerDevice device)
    {
        var customer = _customerRepository.GetById(customerId);

        if (customer is null)
        {
            return false;
        }

        var existingDevice =
            customer.Devices.FirstOrDefault(
                existingDevice => existingDevice.Id == device.Id);

        if (existingDevice is null)
        {
            return false;
        }

        existingDevice.DisplayName = device.DisplayName;
        existingDevice.CheckupSession = device.CheckupSession;
        existingDevice.UpdatedAt = DateTime.Now;

        customer.UpdatedAt = DateTime.Now;

        _customerRepository.Update(customer);

        return true;
    }

    public bool DeleteCustomerDevice(
        Guid customerId,
        Guid deviceId)
    {
        var customer = _customerRepository.GetById(customerId);

        if (customer is null)
        {
            return false;
        }

        var device =
            customer.Devices.FirstOrDefault(
                existingDevice => existingDevice.Id == deviceId);

        if (device is null)
        {
            return false;
        }

        customer.Devices.Remove(device);
        customer.UpdatedAt = DateTime.Now;

        _customerRepository.Update(customer);

        return true;
    }

    private string GenerateCustomerNumber()
    {
        var existingCustomerNumbers = _customerRepository
            .GetAll()
            .Select(customer => customer.CustomerNumber)
            .Where(customerNumber => customerNumber.StartsWith("K-"))
            .Select(customerNumber =>
                customerNumber.Replace("K-", string.Empty))
            .Where(customerNumber =>
                int.TryParse(customerNumber, out _))
            .Select(int.Parse);

        var nextNumber = existingCustomerNumbers.Any()
            ? existingCustomerNumbers.Max() + 1
            : 1;

        return $"K-{nextNumber:0000}";
    }
}