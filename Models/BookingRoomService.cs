using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class BookingRoomService
{
    public Guid Id { get; set; }

    public Guid BookingRoomId { get; set; }

    public Guid? ServiceId { get; set; }

    public decimal TotalPrice { get; set; }

    public DateTime UsedAt { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual BookingRoom BookingRoom { get; set; } = null!;

    public virtual Service? Service { get; set; }
}
