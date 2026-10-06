using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeAssociationInputViewModel
{
    public Guid? AssignmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn tiện nghi/dịch vụ.")]
    public Guid? ItemId { get; set; }

    public bool IsActive { get; set; } = true;
}
