using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class SuCo
{
    public string MaSuCo { get; set; } = null!;

    public string? MaPhong { get; set; }

    public string? MaNguoiDungBao { get; set; }

    public string MoTa { get; set; } = null!;

    public string? TrangThai { get; set; }

    public decimal? ChiPhiSua { get; set; }

    public DateTime? NgayTao { get; set; }

    public bool? DaXoa { get; set; }

    public virtual NguoiDung? MaNguoiDungBaoNavigation { get; set; }

    public virtual PhongTro? MaPhongNavigation { get; set; }
}
