using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class RoomTypeService
{
    public Guid Id { get; set; }

    public Guid RoomTypeId { get; set; }

    public Guid ServiceId { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual RoomType RoomType { get; set; } = null!;

    public virtual Service Service { get; set; } = null!;
}
