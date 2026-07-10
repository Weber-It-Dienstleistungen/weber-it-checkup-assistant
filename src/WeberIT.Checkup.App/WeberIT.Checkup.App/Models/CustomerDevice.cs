namespace WeberIT.Checkup.App.Models;

public class CustomerDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public CheckupSession CheckupSession { get; set; } = new();
}