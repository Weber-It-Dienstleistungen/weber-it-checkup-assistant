namespace WeberIT.Checkup.App.Models;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CustomerNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public string DisplayName => $"{FirstName} {LastName}".Trim();
}