using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class PhongTro
{
    public string MaPhong { get; set; } = null!;

    public string TenPhong { get; set; } = null!;

    public double? DienTich { get; set; }

    public decimal GiaThueCung { get; set; }

    public string? TrangThai { get; set; }

    public virtual ICollection<ChiSoDienNuoc> ChiSoDienNuocs { get; set; } = new List<ChiSoDienNuoc>();

    public virtual ICollection<HopDong> HopDongs { get; set; } = new List<HopDong>();

    public virtual ICollection<SuCo> SuCos { get; set; } = new List<SuCo>();
}
