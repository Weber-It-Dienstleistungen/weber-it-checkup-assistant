using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Repositories.Interfaces;

public interface ICustomerRepository
{
    IEnumerable<Customer> GetAll();

    void Add(Customer customer);

    void Update(Customer customer);

    void Delete(Guid customerId);
}