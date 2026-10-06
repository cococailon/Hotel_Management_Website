using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class BillDetail
{
    public Guid Id { get; set; }

    public Guid BillId { get; set; }

    public Guid BookingRoomId { get; set; }

    public decimal TotalPrice { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual Bill Bill { get; set; } = null!;

    public virtual BookingRoom BookingRoom { get; set; } = null!;
}
