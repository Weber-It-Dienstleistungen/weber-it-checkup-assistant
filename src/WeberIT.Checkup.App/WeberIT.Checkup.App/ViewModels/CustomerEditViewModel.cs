using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerEditViewModel : BaseViewModel
{
    private Customer _customer;

    public Customer Customer
    {
        get => _customer;
        set
        {
            _customer = value;
            OnPropertyChanged();
        }
    }

    public CustomerEditViewModel()
    {
        Customer = new Customer();
    }

    public CustomerEditViewModel(Customer customer)
    {
        Customer = customer;
    }
}