using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class User
{
    public Guid Id { get; set; }

    public string UserCode { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string Password { get; set; } = null!;

    public byte Role { get; set; }

    public bool IsActive { get; set; }

    public Guid? EmployeeId { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual Employee? Employee { get; set; }
}
