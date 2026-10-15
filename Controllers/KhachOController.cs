using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

public class KhachOController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public KhachOController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH KHÁCH Ở & LỌC TẠM TRÚ (GLOBAL QUERY FILTER TỰ ĐỘNG ẨN DaXoa == true)
    [HttpGet]
    public async Task<IActionResult> Index(string? tamTru, string? tuKhoa)
    {
        var query = _context.KhachOs
            .Include(k => k.MaHopDongNavigation)
                .ThenInclude(h => h != null ? h.MaPhongNavigation : null)
            .AsNoTracking()
            .AsQueryable();

        // Lọc theo tab: Mặc định chỉ lấy khách thuộc hợp đồng đang "Hiệu lực"
        if (tamTru == "DaTraPhong")
        {
            query = query.Where(k => k.MaHopDongNavigation != null && k.MaHopDongNavigation.TrangThai == "Đã thanh lý");
        }
        else if (tamTru == "DaDangKy")
        {
            query = query.Where(k => (k.MaHopDongNavigation == null || k.MaHopDongNavigation.TrangThai == "Hiệu lực")
                                  && k.DaDangKyTamTru == true);
        }
        else if (tamTru == "ChuaDangKy")
        {
            query = query.Where(k => (k.MaHopDongNavigation == null || k.MaHopDongNavigation.TrangThai == "Hiệu lực")
                                  && k.DaDangKyTamTru != true);
        }
        else
        {
            // Tab mặc định: Tất cả cư dân đang ở hiện tại
            query = query.Where(k => k.MaHopDongNavigation == null || k.MaHopDongNavigation.TrangThai == "Hiệu lực");
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var kw = tuKhoa.Trim();
            query = query.Where(k => k.HoTen.Contains(kw)
                                  || k.Cccd.Contains(kw)
                                  || (k.SoDienThoai != null && k.SoDienThoai.Contains(kw))
                                  || k.MaKhach.Contains(kw)
                                  || (k.MaHopDong != null && k.MaHopDong.Contains(kw)));
        }

        var allTenants = await _context.KhachOs
            .Include(k => k.MaHopDongNavigation)
            .AsNoTracking()
            .ToListAsync();

        var activeTenants = allTenants
            .Where(k => k.MaHopDongNavigation == null || k.MaHopDongNavigation.TrangThai == "Hiệu lực")
            .ToList();

        ViewBag.TongSoKhach = activeTenants.Count;
        ViewBag.SoDaDangKy = activeTenants.Count(k => k.DaDangKyTamTru == true);
        ViewBag.SoChuaDangKy = activeTenants.Count(k => k.DaDangKyTamTru != true);
        ViewBag.SoDaTraPhong = allTenants.Count(k => k.MaHopDongNavigation != null && k.MaHopDongNavigation.TrangThai == "Đã thanh lý");

        ViewBag.TamTruHienTai = tamTru;
        ViewBag.TuKhoaHienTai = tuKhoa;

        var danhSach = await query.OrderBy(k => k.MaHopDong).ThenBy(k => k.HoTen).ToListAsync();
        return View(danhSach);
    }

    // 2. THÊM KHÁCH Ở MỚI (GET - NHẬN SẴN MÃ HỢP ĐỒNG NẾU ĐI TỪ TRANG CHI TIẾT HỢP ĐỒNG)
    [HttpGet]
    public async Task<IActionResult> Create(string? maHopDong)
    {
        await PopulateHopDongDropdownAsync(maHopDong);

        var model = new KhachO
        {
            MaHopDong = maHopDong,
            DaDangKyTamTru = false,
            DaXoa = false
        };

        ViewBag.ReturnToContract = !string.IsNullOrWhiteSpace(maHopDong);
        return View(model);
    }

    // 3. XỬ LÝ LƯU KHÁCH Ở MỚI (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaKhach,MaHopDong,HoTen,Cccd,SoDienThoai,DaDangKyTamTru")] KhachO khachO, bool returnToContract = false)
    {
        if (!string.IsNullOrWhiteSpace(khachO.MaKhach))
        {
            khachO.MaKhach = khachO.MaKhach.Trim().ToUpper();
            if (await _context.KhachOs.IgnoreQueryFilters().AnyAsync(k => k.MaKhach == khachO.MaKhach))
            {
                ModelState.AddModelError("MaKhach", "Mã khách này đã tồn tại trong cơ sở dữ liệu.");
            }
        }
        if (string.IsNullOrWhiteSpace(khachO.Cccd) || khachO.Cccd.Trim().Length != 12)
        {
            ModelState.AddModelError("Cccd", "Số CCCD bắt buộc phải có đúng 12 ký tự theo quy định CSDL.");
        }
        else
        {
            khachO.Cccd = khachO.Cccd.Trim();
        }

        if (ModelState.IsValid)
        {
            khachO.DaXoa = false;
            _context.Add(khachO);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = $"Đã thêm khách ở {khachO.HoTen} ({khachO.MaKhach}) thành công.";

            if (returnToContract && !string.IsNullOrWhiteSpace(khachO.MaHopDong))
            {
                return RedirectToAction("Details", "HopDong", new { id = khachO.MaHopDong });
            }
            return RedirectToAction(nameof(Index));
        }

        await PopulateHopDongDropdownAsync(khachO.MaHopDong);
        ViewBag.ReturnToContract = returnToContract;
        return View(khachO);
    }

    // 4. CHỈNH SỬA THÔNG TIN KHÁCH Ở (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var khachO = await _context.KhachOs.FindAsync(id);
        if (khachO == null) return NotFound();

        await PopulateHopDongDropdownAsync(khachO.MaHopDong);
        return View(khachO);
    }

    // 5. XỬ LÝ CẬP NHẬT KHÁCH Ở (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaKhach,MaHopDong,HoTen,Cccd,SoDienThoai,DaDangKyTamTru")] KhachO khachO)
    {
        if (id != khachO.MaKhach) return NotFound();

        if (ModelState.IsValid)
        {
            khachO.DaXoa = false;
            _context.Update(khachO);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật hồ sơ khách ở {khachO.HoTen}.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateHopDongDropdownAsync(khachO.MaHopDong);
        return View(khachO);
    }

    // 6. ĐỔI TRẠNG THÁI ĐĂNG KÝ TẠM TRÚ 1-CLICK (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTamTru(string id, string? returnUrl)
    {
        var khachO = await _context.KhachOs.FindAsync(id);
        if (khachO == null) return NotFound();

        khachO.DaDangKyTamTru = !(khachO.DaDangKyTamTru ?? false);
        _context.Update(khachO);
        await _context.SaveChangesAsync();

        string trangThaiMoi = khachO.DaDangKyTamTru == true ? "Đã đăng ký tạm trú" : "Chưa đăng ký tạm trú";
        TempData["ThongBao"] = $"Đã cập nhật trạng thái của {khachO.HoTen} thành: {trangThaiMoi}.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }

    // 7. XÓA MỀM KHÁCH Ở (TỰ ĐỘNG CHUYỂN THÀNH UPDATE DaXoa = true NHỜ SoftDelete.cs)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, string? returnUrl)
    {
        var khachO = await _context.KhachOs.FindAsync(id);
        if (khachO != null)
        {
            // Gọi Remove() chuẩn, tầng DbContext (SoftDelete.cs) sẽ tự động bẻ lái sang DaXoa = true
            _context.KhachOs.Remove(khachO);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa mềm khách ở {khachO.HoTen} khỏi danh sách đang lưu trú.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateHopDongDropdownAsync(string? selectedHopDong)
    {
        var contracts = await _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .AsNoTracking()
            .Where(h => h.TrangThai == "Hiệu lực" || h.MaHopDong == selectedHopDong)
            .OrderBy(h => h.MaHopDong)
            .Select(h => new
            {
                h.MaHopDong,
                HienThi = $"#{h.MaHopDong} — Phòng {(h.MaPhongNavigation != null ? h.MaPhongNavigation.TenPhong : h.MaPhong)} ({h.TrangThai})"
            })
            .ToListAsync();

        ViewBag.DanhSachHopDong = new SelectList(contracts, "MaHopDong", "HienThi", selectedHopDong);
    }
}