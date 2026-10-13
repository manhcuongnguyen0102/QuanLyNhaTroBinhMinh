using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class ThanhToan
{
    public string MaThanhToan { get; set; } = null!;

    public string? MaHoaDon { get; set; }

    public decimal SoTienTra { get; set; }

    public DateTime? NgayTra { get; set; }

    public string? PhuongThuc { get; set; }

    public bool? DaXoa { get; set; }

    public virtual HoaDon? MaHoaDonNavigation { get; set; }
}
