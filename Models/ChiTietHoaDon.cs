using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class ChiTietHoaDon
{
    public string MaChiTiet { get; set; } = null!;

    public string? MaHoaDon { get; set; }

    public string? MaDichVu { get; set; }

    public double SoLuong { get; set; }

    public decimal DonGia { get; set; }

    public decimal ThanhTien { get; set; }

    public bool? DaXoa { get; set; }

    public virtual DichVu? MaDichVuNavigation { get; set; }

    public virtual HoaDon? MaHoaDonNavigation { get; set; }
}
