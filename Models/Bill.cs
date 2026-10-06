using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class Bill
{
    public Guid Id { get; set; }

    public string BillCode { get; set; } = null!;

    public Guid BookingId { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public byte PaymentMethod { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? ReferenceCode { get; set; }

    public byte Status { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid LastModifiedBy { get; set; }

    public DateTime LastModifiedAt { get; set; }

    public virtual ICollection<BillDetail> BillDetails { get; set; } = new List<BillDetail>();

    public virtual Booking Booking { get; set; } = null!;
}
