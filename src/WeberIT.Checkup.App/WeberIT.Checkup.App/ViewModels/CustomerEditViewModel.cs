using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerEditViewModel : BaseViewModel
{
    private readonly Customer _customer;

    private string _customerNumber = string.Empty;
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _street = string.Empty;
    private string _postalCode = string.Empty;
    private string _city = string.Empty;

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

    public CustomerEditViewModel()
        : this(new Customer())
    {
    }

    public CustomerEditViewModel(Customer customer)
    {
        _customer = customer;

        CustomerNumber = customer.CustomerNumber;
        FirstName = customer.FirstName;
        LastName = customer.LastName;
        Email = customer.Email;
        Phone = customer.Phone;
        Street = customer.Street;
        PostalCode = customer.PostalCode;
        City = customer.City;
    }
}