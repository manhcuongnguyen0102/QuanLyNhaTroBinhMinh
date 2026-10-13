using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class ThongBao
{
    public string MaThongBao { get; set; } = null!;

    public string TieuDe { get; set; } = null!;

    public string NoiDung { get; set; } = null!;

    public string? MaNguoiDungTao { get; set; }

    public DateTime? NgayTao { get; set; }

    public bool? DaXoa { get; set; }

    public virtual NguoiDung? MaNguoiDungTaoNavigation { get; set; }
}
