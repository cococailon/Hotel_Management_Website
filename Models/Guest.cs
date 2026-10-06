using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Guest
{
    public Guid Id { get; set; }

    public string GuestCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime? DateOfBirth { get; set; }

    public byte? Sex { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? IdentityNumber { get; set; }

    public byte? IdentityType { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
