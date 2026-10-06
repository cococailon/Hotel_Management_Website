using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class BookingRoom
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid RoomTypeId { get; set; }

    public Guid? RoomId { get; set; }

    public int? Adults { get; set; }

    public int? Children { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<AdditionalFeeRoom> AdditionalFeeRooms { get; set; } = new List<AdditionalFeeRoom>();

    public virtual ICollection<BillDetail> BillDetails { get; set; } = new List<BillDetail>();

    public virtual Booking Booking { get; set; } = null!;

    public virtual ICollection<BookingRoomService> BookingRoomServices { get; set; } = new List<BookingRoomService>();

    public virtual Room? Room { get; set; }

    public virtual RoomType RoomType { get; set; } = null!;
}
