using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Infrastructure.Validation;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerEditViewModel : ValidatableViewModel
{
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;
    private readonly RelayCommand _saveCommand;

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

    public CustomerEditViewModel(
        ICustomerService customerService,
        IDialogService dialogService)
    {
        _customerService = customerService;
        _dialogService = dialogService;

        _saveCommand =
            new RelayCommand(
                _ => Save(),
                _ => CanSave());
    }

    public ICommand SaveCommand => _saveCommand;

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
            Validate();
        }
    }

    public string LastName
    {
        get => _lastName;
        set
        {
            _lastName = value;
            OnPropertyChanged();
            Validate();
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

    public void Initialize(
        Customer customer,
        bool isNewCustomer)
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

        Validate();
    }

    private bool CanSave()
    {
        return !HasErrors;
    }

    protected override void Validate()
    {
        ValidateProperty(
            nameof(FirstName),
            ValidationRules.Required(
                FirstName,
                "Vorname"));

        ValidateProperty(
            nameof(LastName),
            ValidationRules.Required(
                LastName,
                "Nachname"));

        _saveCommand.RaiseCanExecuteChanged();
    }

    private void Save()
    {
        if (_customer is null)
        {
            return;
        }

        Validate();

        if (HasErrors)
        {
            return;
        }

        var customerToSave =
            CreateCustomerForPersistence(_customer);

        try
        {
            if (_isNewCustomer)
            {
                _customerService.CreateCustomer(customerToSave);
            }
            else
            {
                _customerService.UpdateCustomer(customerToSave);
            }
        }
        catch (Exception exception)
        {
            ShowPersistenceError(exception);
            return;
        }

        ApplyPersistedCustomer(
            customerToSave,
            _customer);

        _dialogService.CloseDialog(true);
    }

    private Customer CreateCustomerForPersistence(
        Customer sourceCustomer)
    {
        return new Customer
        {
            Id = sourceCustomer.Id,
            CustomerNumber = CustomerNumber,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            Phone = Phone,
            Street = Street,
            PostalCode = PostalCode,
            City = City,
            CreatedAt = sourceCustomer.CreatedAt,
            UpdatedAt = sourceCustomer.UpdatedAt,
            Devices = sourceCustomer.Devices
        };
    }

    private static void ApplyPersistedCustomer(
        Customer persistedCustomer,
        Customer targetCustomer)
    {
        targetCustomer.CustomerNumber =
            persistedCustomer.CustomerNumber;

        targetCustomer.FirstName =
            persistedCustomer.FirstName;

        targetCustomer.LastName =
            persistedCustomer.LastName;

        targetCustomer.Email =
            persistedCustomer.Email;

        targetCustomer.Phone =
            persistedCustomer.Phone;

        targetCustomer.Street =
            persistedCustomer.Street;

        targetCustomer.PostalCode =
            persistedCustomer.PostalCode;

        targetCustomer.City =
            persistedCustomer.City;

        targetCustomer.CreatedAt =
            persistedCustomer.CreatedAt;

        targetCustomer.UpdatedAt =
            persistedCustomer.UpdatedAt;
    }

    private void ShowPersistenceError(Exception exception)
    {
        var errorDetails =
            string.IsNullOrWhiteSpace(exception.Message)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : exception.Message;

        _dialogService.ShowError(
            "Kunde konnte nicht gespeichert werden",
            "Die Kundendaten konnten nicht dauerhaft in der Datenbank gespeichert werden. "
            + "Der Dialog bleibt geöffnet und die Eingaben können erneut gespeichert werden."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {errorDetails}");
    }
}