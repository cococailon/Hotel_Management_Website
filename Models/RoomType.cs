using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class RoomType
{
    public Guid Id { get; set; }

    public string RoomTypeCode { get; set; } = null!;

    public string RoomTypeName { get; set; } = null!;

    public int MaxAdults { get; set; }

    public int MaxChildren { get; set; }

    public decimal PricePerNight { get; set; }

    public decimal DepositPercent { get; set; }

    public decimal Area { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<BookingRoom> BookingRooms { get; set; } = new List<BookingRoom>();

    public virtual ICollection<RoomTypeAmenity> RoomTypeAmenities { get; set; } = new List<RoomTypeAmenity>();

    public virtual ICollection<RoomTypeService> RoomTypeServices { get; set; } = new List<RoomTypeService>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
