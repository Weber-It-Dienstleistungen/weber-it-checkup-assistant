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
}