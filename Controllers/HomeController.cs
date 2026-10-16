using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
public class HomeController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public HomeController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // TRUNG TÂM ĐIỀU HÀNH TỔNG QUAN DÀNH CHO CHỦ TRỌ (5 ĐIỂM NHẤN)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Today);
        var mocSapHetHan = homNay.AddDays(30);
        int thangNay = DateTime.Today.Month;
        int namNay = DateTime.Today.Year;

        // 1. DỮ LIỆU PHÒNG TRỌ & HỢP ĐỒNG ĐANG HIỆU LỰC (CHO SƠ ĐỒ MẶT BẰNG PHÒNG)
        var dsPhong = await _context.PhongTros
            .AsNoTracking()
            .OrderBy(p => p.MaPhong)
            .ToListAsync();

        var dsHopDongHieuLuc = await _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .Include(h => h.MaNguoiDungDaiDienNavigation)
            .Include(h => h.KhachOs)
            .AsNoTracking()
            .Where(h => h.TrangThai == "Hiệu lực")
            .ToListAsync();

        int tongPhong = dsPhong.Count;
        int phongDangThue = dsPhong.Count(p => p.TrangThai == "Đang thuê");
        int phongTrong = dsPhong.Count(p => p.TrangThai == "Trống");
        int phongDangSua = dsPhong.Count(p => p.TrangThai == "Đang sửa");

        ViewBag.DanhSachPhong = dsPhong;
        ViewBag.HopDongTheoPhong = dsHopDongHieuLuc
            .Where(h => !string.IsNullOrWhiteSpace(h.MaPhong))
            .GroupBy(h => h.MaPhong!.Trim())
            .ToDictionary(g => g.Key, g => g.First());

        ViewBag.TongPhong = tongPhong;
        ViewBag.PhongDangThue = phongDangThue;
        ViewBag.PhongTrong = phongTrong;
        ViewBag.PhongDangSua = phongDangSua;
        ViewBag.TyLeLapDay = tongPhong > 0 ? (int)Math.Round((double)phongDangThue / tongPhong * 100) : 0;

        // 2. THỐNG KÊ NHÂN KHẨU, HỢP ĐỒNG SẮP HẾT HẠN & SỰ CỐ BẢO TRÌ (SuCo)
        ViewBag.SoHopDongHieuLuc = dsHopDongHieuLuc.Count;
        ViewBag.TongCuDanDangO = dsHopDongHieuLuc.Sum(h => h.KhachOs.Count);
        ViewBag.ChuaDangKyTamTru = dsHopDongHieuLuc.Sum(h => h.KhachOs.Count(k => k.DaDangKyTamTru != true));

        var hopDongSapHetHan = dsHopDongHieuLuc
            .Where(h => h.NgayKetThuc >= homNay && h.NgayKetThuc <= mocSapHetHan)
            .OrderBy(h => h.NgayKetThuc)
            .ToList();
        ViewBag.HopDongSapHetHan = hopDongSapHetHan;

        var dsSuCo = await _context.SuCos
            .Include(s => s.MaPhongNavigation)
            .Include(s => s.MaNguoiDungBaoNavigation)
            .AsNoTracking()
            .OrderByDescending(s => s.NgayTao)
            .ToListAsync();

        var suCoChuaXong = dsSuCo.Where(s => s.TrangThai != "Đã hoàn thành").ToList();
        ViewBag.SoSuCoChuaXong = suCoChuaXong.Count;
        ViewBag.TongChiPhiSuaChua = dsSuCo.Sum(s => s.ChiPhiSua ?? 0);
        ViewBag.DanhSachSuCoMoi = dsSuCo.Take(4).ToList();
        ViewBag.PhongCoSuCo = suCoChuaXong
            .Where(s => !string.IsNullOrWhiteSpace(s.MaPhong))
            .Select(s => s.MaPhong!.Trim())
            .ToHashSet();

        // 3. THANH TIẾN ĐỘ VẬN HÀNH THÁNG HIỆN TẠI (3 BƯỚC: CHỐT SỐ -> XUẤT HĐ -> THU TIỀN)
        var chiSoThangNay = await _context.ChiSoDienNuocs
            .AsNoTracking()
            .Where(c => c.Thang == thangNay && c.Nam == namNay && c.MaPhong != null)
            .Select(c => c.MaPhong!.Trim())
            .ToListAsync();

        var setPhongDaChotKyNay = chiSoThangNay.ToHashSet();
        ViewBag.PhongDaChotKyNay = setPhongDaChotKyNay;

        int mucTieuPhongCanChot = Math.Max(1, dsHopDongHieuLuc.Count);
        int soPhongDaChot = dsHopDongHieuLuc.Count(h => h.MaPhong != null && setPhongDaChotKyNay.Contains(h.MaPhong.Trim()));
        ViewBag.SoPhongDaChotKyNay = soPhongDaChot;
        ViewBag.SoPhongChuaChotKyNay = Math.Max(0, dsHopDongHieuLuc.Count - soPhongDaChot);
        ViewBag.PhanTramChotDienNuoc = dsHopDongHieuLuc.Count > 0
            ? (int)Math.Min(100, Math.Round((double)soPhongDaChot / mucTieuPhongCanChot * 100))
            : 100;

        // 4. DỮ LIỆU HÓA ĐƠN, DOANH THU & CÔNG NỢ
        var dsHoaDon = await _context.HoaDons
            .Include(h => h.MaHopDongNavigation)
                .ThenInclude(hd => hd != null ? hd.MaPhongNavigation : null)
            .Include(h => h.ChiTietHoaDons)
            .Include(h => h.ThanhToans)
            .AsNoTracking()
            .ToListAsync();

        var hoaDonThangNay = dsHoaDon.Where(h => h.Thang == thangNay && h.Nam == namNay).ToList();
        int soHoaDonThangNay = hoaDonThangNay.Count;
        ViewBag.SoHoaDonThangNay = soHoaDonThangNay;
        ViewBag.PhanTramXuatHoaDon = dsHopDongHieuLuc.Count > 0
            ? (int)Math.Min(100, Math.Round((double)soHoaDonThangNay / mucTieuPhongCanChot * 100))
            : 100;

        decimal phaiThuThangNay = hoaDonThangNay.Sum(h => h.TongTien ?? 0);
        decimal daThuCuaHoaDonThangNay = hoaDonThangNay.SelectMany(h => h.ThanhToans).Sum(t => t.SoTienTra);
        ViewBag.PhaiThuThangNay = phaiThuThangNay;
        ViewBag.DaThuCuaHoaDonThangNay = daThuCuaHoaDonThangNay;
        ViewBag.PhanTramThuTienThangNay = phaiThuThangNay > 0
            ? (int)Math.Min(100, Math.Round((daThuCuaHoaDonThangNay / phaiThuThangNay) * 100))
            : 0;

        decimal tongDoanhThuDaThu = dsHoaDon.SelectMany(h => h.ThanhToans).Sum(t => t.SoTienTra);
        decimal doanhThuThangNay = dsHoaDon
            .SelectMany(h => h.ThanhToans)
            .Where(t => t.NgayTra.HasValue && t.NgayTra.Value.Month == thangNay && t.NgayTra.Value.Year == namNay)
            .Sum(t => t.SoTienTra);

        var dsHoaDonConNo = dsHoaDon
            .Where(h => h.TrangThai != "Đã thanh toán")
            .OrderByDescending(h => h.Nam)
            .ThenByDescending(h => h.Thang)
            .ToList();

        decimal tongCongNoCanThu = dsHoaDonConNo.Sum(h =>
            Math.Max(0, (h.TongTien ?? 0) - h.ThanhToans.Sum(t => t.SoTienTra)));

        ViewBag.TongDoanhThuDaThu = tongDoanhThuDaThu;
        ViewBag.DoanhThuThangNay = doanhThuThangNay;
        ViewBag.TongCongNoCanThu = tongCongNoCanThu;
        ViewBag.SoHoaDonConNo = dsHoaDonConNo.Count;

        // 5. DỮ LIỆU BIỂU ĐỒ 1: DOANH THU THỰC THU VS CÔNG NỢ 6 THÁNG GẦN NHẤT
        var labels6Thang = new List<string>();
        var dataThucThu6Thang = new List<decimal>();
        var dataCongNo6Thang = new List<decimal>();

        for (int i = 5; i >= 0; i--)
        {
            var mocThang = DateTime.Today.AddMonths(-i);
            int m = mocThang.Month;
            int y = mocThang.Year;
            labels6Thang.Add($"T{m:D2}/{y}");

            var hdTrongThang = dsHoaDon.Where(h => h.Thang == m && h.Nam == y).ToList();
            decimal daThuM = hdTrongThang.SelectMany(h => h.ThanhToans).Sum(t => t.SoTienTra);
            decimal tongM = hdTrongThang.Sum(h => h.TongTien ?? 0);
            decimal conNoM = Math.Max(0, tongM - daThuM);

            dataThucThu6Thang.Add(daThuM);
            dataCongNo6Thang.Add(conNoM);
        }

        ViewBag.ChartLabels6Thang = JsonSerializer.Serialize(labels6Thang);
        ViewBag.ChartThucThu6Thang = JsonSerializer.Serialize(dataThucThu6Thang);
        ViewBag.ChartCongNo6Thang = JsonSerializer.Serialize(dataCongNo6Thang);

        // 6. DỮ LIỆU BIỂU ĐỒ 2: CƠ CẤU NGUỒN THU (TIỀN PHÒNG, ĐIỆN, NƯỚC, DỊCH VỤ KHÁC)
        var allChiTiet = dsHoaDon.SelectMany(h => h.ChiTietHoaDons).ToList();
        decimal tongTienDien = allChiTiet
            .Where(c => c.MaDichVu != null && c.MaDichVu.Trim().Equals("DV_DIEN", StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.ThanhTien);
        decimal tongTienNuoc = allChiTiet
            .Where(c => c.MaDichVu != null && c.MaDichVu.Trim().Equals("DV_NUOC", StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.ThanhTien);
        decimal tongDichVuCoDinh = allChiTiet
            .Where(c => c.MaDichVu != null
                     && !c.MaDichVu.Trim().Equals("DV_DIEN", StringComparison.OrdinalIgnoreCase)
                     && !c.MaDichVu.Trim().Equals("DV_NUOC", StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.ThanhTien);
        decimal tongTienPhongCung = Math.Max(0, dsHoaDon.Sum(h => h.TongTien ?? 0) - (tongTienDien + tongTienNuoc + tongDichVuCoDinh));

        ViewBag.ChartCoCauData = JsonSerializer.Serialize(new[]
        {
            tongTienPhongCung,
            tongTienDien,
            tongTienNuoc,
            tongDichVuCoDinh
        });

        // 7. TIMELINE GIAO DỊCH THU TIỀN VỪA PHÁT SINH
        var giaoDichMoiNhat = await _context.ThanhToans
            .Include(t => t.MaHoaDonNavigation)
                .ThenInclude(h => h != null ? h.MaHopDongNavigation : null)
            .AsNoTracking()
            .OrderByDescending(t => t.NgayTra)
            .Take(5)
            .ToListAsync();
        ViewBag.GiaoDichMoiNhat = giaoDichMoiNhat;

        return View(dsHoaDonConNo);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}