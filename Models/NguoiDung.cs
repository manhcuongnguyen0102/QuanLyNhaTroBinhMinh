using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class NguoiDung
{
    public string MaNguoiDung { get; set; } = null!;

    public string TenDangNhap { get; set; } = null!;

    public string MatKhau { get; set; } = null!;

    public string? MaVaiTro { get; set; }

    public string? TrangThai { get; set; }

    public virtual ICollection<HopDong> HopDongs { get; set; } = new List<HopDong>();

    public virtual VaiTro? MaVaiTroNavigation { get; set; }

    public virtual ICollection<SuCo> SuCos { get; set; } = new List<SuCo>();

    public virtual ICollection<ThongBao> ThongBaos { get; set; } = new List<ThongBao>();
}
