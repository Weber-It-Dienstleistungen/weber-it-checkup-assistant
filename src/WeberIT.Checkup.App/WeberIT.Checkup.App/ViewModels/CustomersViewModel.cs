using System.Collections.ObjectModel;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomersViewModel : BaseViewModel
{
    public ObservableCollection<Customer> Customers { get; }

    public CustomersViewModel(ICustomerService customerService)
    {
        Customers = new ObservableCollection<Customer>(
            customerService.GetCustomers());
    }
}