using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

[Authorize]
public class KhachThueController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public KhachThueController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. TRANG TỔNG QUAN CỔNG KHÁCH THUÊ (HỢP ĐỒNG, HÓA ĐƠN, ĐIỆN NƯỚC, SỰ CỐ, THÔNG BÁO)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string? maNguoiDung = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(maNguoiDung))
            return RedirectToAction("Login", "Account");

        // CHỐT CHẶN BUG-05: Kiểm tra thời gian thực xem tài khoản có vừa bị Chủ trọ khóa không
        var taiKhoanHienTai = await _context.NguoiDungs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.MaNguoiDung == maNguoiDung);

        if (taiKhoanHienTai == null || taiKhoanHienTai.TrangThai == "Khóa")
        {
            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(
                HttpContext,
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // Tìm hợp đồng đang "Hiệu lực" của tài khoản khách thuê này (nếu không có thì lấy hợp đồng gần nhất)
        var hopDong = await _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .Include(h => h.KhachOs)
            .Include(h => h.DichVuHopDongs)
                .ThenInclude(d => d.MaDichVuNavigation)
            .Include(h => h.HoaDons)
                .ThenInclude(hd => hd.ThanhToans)
            .AsNoTracking()
            .OrderBy(h => h.TrangThai == "Hiệu lực" ? 0 : 1)
            .ThenByDescending(h => h.NgayBatDau)
            .FirstOrDefaultAsync(h => h.MaNguoiDungDaiDien == maNguoiDung);

        // Lấy lịch sử chốt chỉ số Điện Nước & Ảnh đồng hồ của phòng đang ở
        if (hopDong != null && !string.IsNullOrWhiteSpace(hopDong.MaPhong))
        {
            ViewBag.LichSuDienNuoc = await _context.ChiSoDienNuocs
                .AsNoTracking()
                .Where(c => c.MaPhong == hopDong.MaPhong)
                .OrderByDescending(c => c.Nam)
                .ThenByDescending(c => c.Thang)
                .Take(6)
                .ToListAsync();

            ViewBag.DanhSachSuCo = await _context.SuCos
                .AsNoTracking()
                .Where(s => s.MaPhong == hopDong.MaPhong || s.MaNguoiDungBao == maNguoiDung)
                .OrderByDescending(s => s.NgayTao)
                .ToListAsync();
        }
        else
        {
            ViewBag.LichSuDienNuoc = new List<ChiSoDienNuoc>();
            ViewBag.DanhSachSuCo = await _context.SuCos
                .AsNoTracking()
                .Where(s => s.MaNguoiDungBao == maNguoiDung)
                .OrderByDescending(s => s.NgayTao)
                .ToListAsync();
        }

        // Lấy bảng tin thông báo chung từ Chủ trọ
        ViewBag.BangTinThongBao = await _context.ThongBaos
            .AsNoTracking()
            .OrderByDescending(t => t.NgayTao)
            .Take(5)
            .ToListAsync();

        return View(hopDong);
    }

    // 2. XEM CHI TIẾT PHIẾU HÓA ĐƠN & THANH TOÁN TRỰC TUYẾN (GET)
    [HttpGet]
    public async Task<IActionResult> ChiTietHoaDon(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        string? maNguoiDung = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var hoaDon = await _context.HoaDons
            .Include(h => h.MaHopDongNavigation)
                .ThenInclude(hd => hd != null ? hd.MaPhongNavigation : null)
            .Include(h => h.ChiTietHoaDons)
                .ThenInclude(ct => ct.MaDichVuNavigation)
            .Include(h => h.ThanhToans)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.MaHoaDon == id);

        if (hoaDon == null) return NotFound();

        // Bảo mật: Khách chỉ được xem hóa đơn thuộc hợp đồng của chính mình
        if (User.FindFirstValue(ClaimTypes.Role) == "VT_TENANT"
            && hoaDon.MaHopDongNavigation?.MaNguoiDungDaiDien != maNguoiDung)
        {
            return RedirectToAction(nameof(Index));
        }

        // Lấy bản ghi chốt Điện Nước & danh sách sự cố có phí sửa chữa để khách kiểm tra
        if (hoaDon.MaHopDongNavigation?.MaPhong != null)
        {
            string maP = hoaDon.MaHopDongNavigation.MaPhong;

            ViewBag.ChiSoKyNay = await _context.ChiSoDienNuocs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MaPhong == maP
                                       && c.Thang == hoaDon.Thang
                                       && c.Nam == hoaDon.Nam);

            // Lấy danh sách sự cố có thu phí sửa chữa để hiển thị rõ cho Khách thuê đối chiếu
            ViewBag.DanhSachSuCoCoPhi = await _context.SuCos
                .AsNoTracking()
                .Where(s => s.MaPhong == maP
                         && s.ChiPhiSua > 0
                         && s.NgayTao.HasValue
                         && s.NgayTao.Value.Month == hoaDon.Thang
                         && s.NgayTao.Value.Year == hoaDon.Nam)
                .ToListAsync();
        }

        return View(hoaDon);
    }

    // 3. KHÁCH THUÊ TỰ THANH TOÁN HÓA ĐƠN (TỰ ĐỘNG NHẢY LỊCH SỬ & TỰ ĐỘNG ĐỔI TRẠNG THÁI HÓA ĐƠN)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThanhToanHoaDon(string maHoaDon, decimal soTienTra)
    {
        string? maNguoiDung = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(maNguoiDung))
            return RedirectToAction("Login", "Account");

        var hoaDon = await _context.HoaDons
            .Include(h => h.MaHopDongNavigation)
            .Include(h => h.ThanhToans)
            .FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);

        if (hoaDon == null) return NotFound();

        // Chốt chặn bảo mật IDOR
        if (hoaDon.MaHopDongNavigation?.MaNguoiDungDaiDien != maNguoiDung)
        {
            TempData["Loi"] = "Bạn không có quyền thanh toán hóa đơn của phòng khác.";
            return RedirectToAction(nameof(Index));
        }

        if (soTienTra <= 0)
        {
            TempData["Loi"] = "Số tiền tạo mã QR thanh toán phải lớn hơn 0đ.";
            return RedirectToAction(nameof(ChiTietHoaDon), new { id = maHoaDon });
        }

        decimal daTraTruocDo = hoaDon.ThanhToans.Sum(t => t.SoTienTra);
        decimal tongHoaDon = hoaDon.TongTien ?? 0;
        decimal conNo = Math.Max(0, tongHoaDon - daTraTruocDo);

        if (soTienTra > conNo)
        {
            TempData["Loi"] = $"Số tiền chuyển khoản ({soTienTra:N0}đ) vượt quá dư nợ còn lại của hóa đơn ({conNo:N0}đ)!";
            return RedirectToAction(nameof(ChiTietHoaDon), new { id = maHoaDon });
        }

        // Tạo bản ghi ThanhToan mới — Khóa cứng phương thức là "Chuyển khoản" (VietQR)
        string maThanhToan = $"QR{DateTime.Now:ddHHmmss}_{Random.Shared.Next(10, 99)}";
        var giaoDichMoi = new ThanhToan
        {
            MaThanhToan = maThanhToan,
            MaHoaDon = maHoaDon,
            SoTienTra = soTienTra,
            NgayTra = DateTime.Now,
            PhuongThuc = "Chuyển khoản",
            DaXoa = false
        };

        _context.ThanhToans.Add(giaoDichMoi);

        // Tự động cập nhật số tiền còn lại & trạng thái Hóa đơn
        decimal tongDaTraMoi = daTraTruocDo + soTienTra;
        if (tongDaTraMoi >= tongHoaDon)
        {
            hoaDon.TrangThai = "Đã thanh toán";
        }
        else
        {
            hoaDon.TrangThai = "Thanh toán một phần";
        }

        _context.HoaDons.Update(hoaDon);
        await _context.SaveChangesAsync();

        TempData["ThongBao"] = $"Đối soát VietQR thành công: Đã nhận {soTienTra:N0}đ (Mã GD #{maThanhToan}). Số dư nợ còn lại: {Math.Max(0, tongHoaDon - tongDaTraMoi):N0}đ!";
        return RedirectToAction(nameof(ChiTietHoaDon), new { id = maHoaDon });
    }

    // 4. KHÁCH THUÊ GỬI BÁO CÁO SỰ CỐ HỎNG HÓC TỪ PHÒNG ĐANG Ở (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BaoCaoSuCo(string moTa)
    {
        string? maNguoiDung = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(maNguoiDung))
            return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(moTa))
        {
            TempData["Loi"] = "Vui lòng nhập mô tả chi tiết sự cố hỏng hóc.";
            return RedirectToAction(nameof(Index));
        }

        // CHỐT CHẶN CHỐNG IDOR: Tự truy vấn phòng từ Hợp đồng đang Hiệu lực của chính tài khoản này trong CSDL,
        // tuyệt đối không tin tưởng mã phòng truyền từ thẻ input hidden dưới trình duyệt!
        var hopDongCuaKhach = await _context.HopDongs
            .AsNoTracking()
            .OrderBy(h => h.TrangThai == "Hiệu lực" ? 0 : 1)
            .FirstOrDefaultAsync(h => h.MaNguoiDungDaiDien == maNguoiDung);

        if (hopDongCuaKhach == null || string.IsNullOrWhiteSpace(hopDongCuaKhach.MaPhong))
        {
            TempData["Loi"] = "Tài khoản của bạn không có hợp đồng thuê phòng hợp lệ để gửi báo cáo sự cố.";
            return RedirectToAction(nameof(Index));
        }

        string maSuCo = $"SC{DateTime.Now:yyMMdd}_{Random.Shared.Next(100, 999)}";
        var suCoMoi = new SuCo
        {
            MaSuCo = maSuCo,
            MaPhong = hopDongCuaKhach.MaPhong, // Luôn gắn đúng phòng thật của khách thuê
            MaNguoiDungBao = maNguoiDung,
            MoTa = moTa.Trim(),
            TrangThai = "Chờ xử lý",
            ChiPhiSua = 0,
            NgayTao = DateTime.Now,
            DaXoa = false
        };

        _context.SuCos.Add(suCoMoi);
        await _context.SaveChangesAsync();

        TempData["ThongBao"] = $"Đã gửi báo cáo sự cố #{maSuCo} (Phòng {hopDongCuaKhach.MaPhong}) tới Chủ trọ thành công.";
        return RedirectToAction(nameof(Index));
    }
}