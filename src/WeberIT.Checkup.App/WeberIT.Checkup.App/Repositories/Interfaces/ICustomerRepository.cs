using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Repositories.Interfaces;

public interface ICustomerRepository
{
    IEnumerable<Customer> GetAll();
}