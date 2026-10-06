namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestActionResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string> FieldErrors { get; init; }
        = new Dictionary<string, string>();
}
