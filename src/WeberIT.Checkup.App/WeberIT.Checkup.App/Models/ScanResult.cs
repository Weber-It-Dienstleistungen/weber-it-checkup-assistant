namespace WeberIT.Checkup.App.Models;

public class ScanResult<T>
{
    public bool IsSuccessful { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }
}