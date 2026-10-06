using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class AdditionalFeeRoom
{
    public Guid Id { get; set; }

    public Guid BookingRoomId { get; set; }

    public string FeeName { get; set; } = null!;

    public byte FeeType { get; set; }

    public decimal TotalFee { get; set; }

    public string? Decription { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual BookingRoom BookingRoom { get; set; } = null!;
}
