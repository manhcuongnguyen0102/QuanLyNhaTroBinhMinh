using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class DichVuHopDong
{
    public string MaDvHd { get; set; } = null!;

    public string? MaHopDong { get; set; }

    public string? MaDichVu { get; set; }

    public virtual DichVu? MaDichVuNavigation { get; set; }

    public virtual HopDong? MaHopDongNavigation { get; set; }
}
