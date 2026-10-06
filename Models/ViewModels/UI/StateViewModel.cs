namespace Hotel_Management_Website.Models.ViewModels.UI;

public sealed class StateViewModel
{
    public string Message { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Icon { get; init; } = "bi-info-circle";
    public string CssClass { get; init; } = "text-body-secondary";
    public string? ActionText { get; init; }
    public string? ActionId { get; init; }
}
