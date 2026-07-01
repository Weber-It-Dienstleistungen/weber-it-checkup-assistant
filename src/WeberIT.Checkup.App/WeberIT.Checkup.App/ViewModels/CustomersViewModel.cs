using System.Collections.ObjectModel;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomersViewModel : BaseViewModel
{
    private Customer? _selectedCustomer;

    public ObservableCollection<Customer> Customers { get; }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            _selectedCustomer = value;
            OnPropertyChanged();
        }
    }

    public CustomersViewModel(ICustomerService customerService)
    {
        Customers = new ObservableCollection<Customer>(
            customerService.GetCustomers());
    }
}