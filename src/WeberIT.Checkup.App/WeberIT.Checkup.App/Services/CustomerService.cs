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

    public void AddDeviceToCustomer(Guid customerId, CustomerDevice device)
    {
        var customer = _customerRepository.GetById(customerId);

        if (customer is null)
        {
            return;
        }

        device.CreatedAt = DateTime.Now;
        device.UpdatedAt = null;

        customer.Devices.Add(device);
        customer.UpdatedAt = DateTime.Now;

        _customerRepository.Update(customer);
    }

    public void UpdateCustomerDevice(Guid customerId, CustomerDevice device)
    {
        var customer = _customerRepository.GetById(customerId);

        if (customer is null)
        {
            return;
        }

        var existingDevice = customer.Devices.FirstOrDefault(d => d.Id == device.Id);

        if (existingDevice is null)
        {
            return;
        }

        existingDevice.DisplayName = device.DisplayName;
        existingDevice.CheckupSession = device.CheckupSession;
        existingDevice.UpdatedAt = DateTime.Now;

        customer.UpdatedAt = DateTime.Now;

        _customerRepository.Update(customer);
    }

    public void DeleteCustomerDevice(Guid customerId, Guid deviceId)
    {
        var customer = _customerRepository.GetById(customerId);

        if (customer is null)
        {
            return;
        }

        var device = customer.Devices.FirstOrDefault(d => d.Id == deviceId);

        if (device is not null)
        {
            customer.Devices.Remove(device);
            customer.UpdatedAt = DateTime.Now;

            _customerRepository.Update(customer);
        }
    }

    private string GenerateCustomerNumber()
    {
        var existingCustomerNumbers = _customerRepository
            .GetAll()
            .Select(c => c.CustomerNumber)
            .Where(n => n.StartsWith("K-"))
            .Select(n => n.Replace("K-", string.Empty))
            .Where(n => int.TryParse(n, out _))
            .Select(int.Parse);

        var nextNumber = existingCustomerNumbers.Any()
            ? existingCustomerNumbers.Max() + 1
            : 1;

        return $"K-{nextNumber:0000}";
    }
}