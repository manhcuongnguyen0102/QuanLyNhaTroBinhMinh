using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class HopDong
{
    public string MaHopDong { get; set; } = null!;

    public string? MaPhong { get; set; }

    public string? MaNguoiDungDaiDien { get; set; }

    public DateOnly NgayBatDau { get; set; }

    public DateOnly NgayKetThuc { get; set; }

    public decimal? TienCoc { get; set; }

    public string? TrangThai { get; set; }

    public virtual ICollection<DichVuHopDong> DichVuHopDongs { get; set; } = new List<DichVuHopDong>();

    public virtual ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();

    public virtual ICollection<KhachO> KhachOs { get; set; } = new List<KhachO>();

    public virtual NguoiDung? MaNguoiDungDaiDienNavigation { get; set; }

    public virtual PhongTro? MaPhongNavigation { get; set; }
}
