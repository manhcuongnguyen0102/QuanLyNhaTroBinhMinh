using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class KhachO
{
    public string MaKhach { get; set; } = null!;

    public string? MaHopDong { get; set; }

    public string HoTen { get; set; } = null!;

    public string Cccd { get; set; } = null!;

    public string? SoDienThoai { get; set; }

    public bool? DaDangKyTamTru { get; set; }

    public bool? DaXoa { get; set; }

    public virtual HopDong? MaHopDongNavigation { get; set; }
}
