using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomersViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;

    private string _searchText = string.Empty;
    private Customer? _selectedCustomer;

    public ObservableCollection<Customer> Customers { get; }

    public ICollectionView CustomersView { get; }

    public ICommand AddCustomerCommand { get; }

    public RelayCommand EditCustomerCommand { get; }

    public RelayCommand DeleteCustomerCommand { get; }

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
            EditCustomerCommand.RaiseCanExecuteChanged();
            DeleteCustomerCommand.RaiseCanExecuteChanged();
        }
    }

    public CustomersViewModel(
        ICustomerService customerService,
        IDialogService dialogService)
    {
        _customerService = customerService;
        _dialogService = dialogService;

        Customers = new ObservableCollection<Customer>(
            _customerService.GetCustomers());

        CustomersView = CollectionViewSource.GetDefaultView(Customers);
        CustomersView.Filter = FilterCustomer;

        AddCustomerCommand = new RelayCommand(_ => AddCustomer());
        EditCustomerCommand = new RelayCommand(_ => EditCustomer(), _ => SelectedCustomer is not null);
        DeleteCustomerCommand = new RelayCommand(_ => DeleteCustomer(), _ => SelectedCustomer is not null);
    }

    private void AddCustomer()
    {
        var customer = new Customer();

        var result = _dialogService.ShowCustomerEditDialog(customer, true);

        if (result == true)
        {
            Customers.Add(customer);
            SelectedCustomer = customer;
            CustomersView.Refresh();
        }
    }

    private void EditCustomer()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var result = _dialogService.ShowCustomerEditDialog(SelectedCustomer, false);

        if (result == true)
        {
            CustomersView.Refresh();
            OnPropertyChanged(nameof(SelectedCustomer));
        }
    }

    private void DeleteCustomer()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var customer = SelectedCustomer;

        var confirmed = _dialogService.Confirm(
            "Kunden löschen",
            $"Soll der Kunde \"{customer.DisplayName}\" wirklich gelöscht werden?");

        if (!confirmed)
        {
            return;
        }

        _customerService.DeleteCustomer(customer.Id);
        Customers.Remove(customer);

        SelectedCustomer = Customers.FirstOrDefault();
        CustomersView.Refresh();
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