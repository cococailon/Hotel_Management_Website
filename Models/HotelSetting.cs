using System;
using System.Collections.Generic;

namespace Hotel_Management_Website.Models;

public partial class HotelSetting
{
    public Guid Id { get; set; }

    public TimeOnly CheckInTime { get; set; }

    public TimeOnly CheckOutTime { get; set; }

    public string? Note { get; set; }
}
