using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Service
{
    public Guid Id { get; set; }

    public string ServiceCode { get; set; } = null!;

    public string ServiceName { get; set; } = null!;

    public decimal? UnitPrice { get; set; }

    public string? PriceUnitType { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<BookingRoomService> BookingRoomServices { get; set; } = new List<BookingRoomService>();

    public virtual ICollection<RoomTypeService> RoomTypeServices { get; set; } = new List<RoomTypeService>();
}
