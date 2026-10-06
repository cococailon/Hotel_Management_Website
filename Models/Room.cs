using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Room
{
    public Guid Id { get; set; }

    public string RoomCode { get; set; } = null!;

    public string RoomName { get; set; } = null!;

    public Guid RoomTypeId { get; set; }

    public int Floor { get; set; }

    public int Row { get; set; }

    public int LeftToRightOrder { get; set; }

    public string? Description { get; set; }

    public byte Status { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public string? Note { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<BookingRoom> BookingRooms { get; set; } = new List<BookingRoom>();

    public virtual RoomType RoomType { get; set; } = null!;
}
