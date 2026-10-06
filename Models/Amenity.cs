using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Amenity
{
    public Guid Id { get; set; }

    public string AmenityCode { get; set; } = null!;

    public string AmenityName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<RoomTypeAmenity> RoomTypeAmenities { get; set; } = new List<RoomTypeAmenity>();
}
