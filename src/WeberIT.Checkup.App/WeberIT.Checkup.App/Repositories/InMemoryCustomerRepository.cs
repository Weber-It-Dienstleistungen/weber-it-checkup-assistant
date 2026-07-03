using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Repositories.Interfaces;

namespace WeberIT.Checkup.App.Repositories;

public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers;

    public InMemoryCustomerRepository()
    {
        _customers =
        [
            new Customer
            {
                CustomerNumber = "K-0001",
                FirstName = "Max",
                LastName = "Mustermann",
                Email = "max.mustermann@example.com",
                Phone = "09568 123456",
                Street = "Musterstraße 1",
                PostalCode = "96465",
                City = "Neustadt bei Coburg"
            },
            new Customer
            {
                CustomerNumber = "K-0002",
                FirstName = "Erika",
                LastName = "Musterfrau",
                Email = "erika.musterfrau@example.com",
                Phone = "09568 654321",
                Street = "Beispielweg 5",
                PostalCode = "96465",
                City = "Neustadt bei Coburg"
            }
        ];
    }

    public IEnumerable<Customer> GetAll()
    {
        return _customers;
    }

    public void Add(Customer customer)
    {
        _customers.Add(customer);
    }

    public void Update(Customer customer)
    {
        var existingCustomer = _customers.FirstOrDefault(c => c.Id == customer.Id);

        if (existingCustomer is null)
        {
            return;
        }

        existingCustomer.CustomerNumber = customer.CustomerNumber;
        existingCustomer.FirstName = customer.FirstName;
        existingCustomer.LastName = customer.LastName;
        existingCustomer.Email = customer.Email;
        existingCustomer.Phone = customer.Phone;
        existingCustomer.Street = customer.Street;
        existingCustomer.PostalCode = customer.PostalCode;
        existingCustomer.City = customer.City;
        existingCustomer.UpdatedAt = DateTime.Now;
    }

    public void Delete(Guid customerId)
    {
        var customer = _customers.FirstOrDefault(c => c.Id == customerId);

        if (customer is not null)
        {
            _customers.Remove(customer);
        }
    }
}