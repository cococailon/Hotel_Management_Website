using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Employee
{
    public Guid Id { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string EmployeeName { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public byte Sex { get; set; }

    public byte Position { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public byte Status { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
