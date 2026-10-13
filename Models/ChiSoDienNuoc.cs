using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class ChiSoDienNuoc
{
    public string MaChiSo { get; set; } = null!;

    public string? MaPhong { get; set; }

    public int? Thang { get; set; }

    public int? Nam { get; set; }

    public int? DienCu { get; set; }

    public int? DienMoi { get; set; }

    public int? NuocCu { get; set; }

    public int? NuocMoi { get; set; }

    public string? AnhDongHo { get; set; }

    public virtual PhongTro? MaPhongNavigation { get; set; }
}
