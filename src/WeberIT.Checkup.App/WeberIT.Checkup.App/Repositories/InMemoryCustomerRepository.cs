using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Repositories.Interfaces;

namespace WeberIT.Checkup.App.Repositories;

public class InMemoryCustomerRepository : ICustomerRepository
{
    public IEnumerable<Customer> GetAll()
    {
        return new List<Customer>
        {
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
        };
    }
}