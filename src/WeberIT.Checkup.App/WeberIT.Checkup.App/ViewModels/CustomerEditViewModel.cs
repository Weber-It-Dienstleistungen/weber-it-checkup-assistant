using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerEditViewModel : ValidatableViewModel
{
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;

    private Customer? _customer;
    private bool _isNewCustomer;

    private string _title = string.Empty;
    private string _customerNumber = string.Empty;
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _street = string.Empty;
    private string _postalCode = string.Empty;
    private string _city = string.Empty;

    public ICommand SaveCommand { get; }

    public string Title
    {
        get => _title;
        private set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public string CustomerNumber
    {
        get => _customerNumber;
        set
        {
            _customerNumber = value;
            OnPropertyChanged();
        }
    }

    public string FirstName
    {
        get => _firstName;
        set
        {
            _firstName = value;
            OnPropertyChanged();
        }
    }

    public string LastName
    {
        get => _lastName;
        set
        {
            _lastName = value;
            OnPropertyChanged();
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
        }
    }

    public string Phone
    {
        get => _phone;
        set
        {
            _phone = value;
            OnPropertyChanged();
        }
    }

    public string Street
    {
        get => _street;
        set
        {
            _street = value;
            OnPropertyChanged();
        }
    }

    public string PostalCode
    {
        get => _postalCode;
        set
        {
            _postalCode = value;
            OnPropertyChanged();
        }
    }

    public string City
    {
        get => _city;
        set
        {
            _city = value;
            OnPropertyChanged();
        }
    }

    public CustomerEditViewModel(
        ICustomerService customerService,
        IDialogService dialogService)
    {
        _customerService = customerService;
        _dialogService = dialogService;

        SaveCommand = new RelayCommand(_ => Save());
    }

    public void Initialize(Customer customer, bool isNewCustomer)
    {
        _customer = customer;
        _isNewCustomer = isNewCustomer;

        Title = _isNewCustomer
            ? "Neuen Kunden anlegen"
            : "Kunden bearbeiten";

        CustomerNumber = customer.CustomerNumber;
        FirstName = customer.FirstName;
        LastName = customer.LastName;
        Email = customer.Email;
        Phone = customer.Phone;
        Street = customer.Street;
        PostalCode = customer.PostalCode;
        City = customer.City;
    }

    private void Save()
    {
        if (_customer is null)
        {
            return;
        }

        _customer.CustomerNumber = CustomerNumber;
        _customer.FirstName = FirstName;
        _customer.LastName = LastName;
        _customer.Email = Email;
        _customer.Phone = Phone;
        _customer.Street = Street;
        _customer.PostalCode = PostalCode;
        _customer.City = City;

        if (_isNewCustomer)
        {
            _customerService.CreateCustomer(_customer);
        }
        else
        {
            _customerService.UpdateCustomer(_customer);
        }

        _dialogService.CloseDialog(true);
    }
}