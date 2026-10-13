using System;
using System.Collections.Generic;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class DichVu
{
    public string MaDichVu { get; set; } = null!;

    public string TenDichVu { get; set; } = null!;

    public string DonViTinh { get; set; } = null!;

    public decimal DonGia { get; set; }

    public bool? LaCoDinh { get; set; }

    public bool? DaXoa { get; set; }

    public virtual ICollection<ChiTietHoaDon> ChiTietHoaDons { get; set; } = new List<ChiTietHoaDon>();

    public virtual ICollection<DichVuHopDong> DichVuHopDongs { get; set; } = new List<DichVuHopDong>();
}
