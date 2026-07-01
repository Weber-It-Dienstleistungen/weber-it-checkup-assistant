using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomersViewModel : BaseViewModel
{
    private string _searchText = string.Empty;
    private Customer? _selectedCustomer;

    public ObservableCollection<Customer> Customers { get; }

    public ICollectionView CustomersView { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            CustomersView.Refresh();
        }
    }

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

        CustomersView = CollectionViewSource.GetDefaultView(Customers);
        CustomersView.Filter = FilterCustomer;
    }

    private bool FilterCustomer(object item)
    {
        if (item is not Customer customer)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var searchText = SearchText.Trim();

        return customer.CustomerNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || customer.FirstName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || customer.LastName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || customer.Email.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || customer.Phone.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || customer.City.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}