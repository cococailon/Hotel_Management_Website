using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Booking
{
    public Guid Id { get; set; }

    public string BookingCode { get; set; } = null!;

    public Guid? GuestId { get; set; }

    public byte BookingType { get; set; }

    public DateTime ExpectedCheckInAt { get; set; }

    public DateTime ExpectedCheckOutAt { get; set; }

    public DateTime? ActualCheckInAt { get; set; }

    public DateTime? ActualCheckOutAt { get; set; }

    public int TotalAdults { get; set; }

    public int TotalChildren { get; set; }

    public decimal DepositAmount { get; set; }

    public byte? PaymentMethod { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? ReferenceCode { get; set; }

    public byte Status { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual Bill? Bill { get; set; }

    public virtual ICollection<BookingRoom> BookingRooms { get; set; } = new List<BookingRoom>();

    public virtual Guest? Guest { get; set; }
}
