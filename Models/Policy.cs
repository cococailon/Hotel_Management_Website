using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Policy
{
    public Guid Id { get; set; }

    public string? Content { get; set; }

    public string? Note { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }
}
