using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;
namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
public class NguoiDungController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public NguoiDungController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH TÀI KHOẢN & LIÊN KẾT PHÒNG ĐANG THUÊ
    [HttpGet]
    public async Task<IActionResult> Index(string? maVaiTro, string? trangThai, string? tuKhoa)
    {
        var query = _context.NguoiDungs
            .Include(u => u.MaVaiTroNavigation)
            .Include(u => u.HopDongs)
                .ThenInclude(h => h.MaPhongNavigation)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maVaiTro))
        {
            query = query.Where(u => u.MaVaiTro == maVaiTro);
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(u => u.TrangThai == trangThai);
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var kw = tuKhoa.Trim();
            query = query.Where(u => u.MaNguoiDung.Contains(kw) || u.TenDangNhap.Contains(kw));
        }

        var allUsers = await _context.NguoiDungs.AsNoTracking().ToListAsync();
        ViewBag.TongTaiKhoan = allUsers.Count;
        ViewBag.SoChuTro = allUsers.Count(u => u.MaVaiTro == "VT_ADMIN");
        ViewBag.SoKhachThue = allUsers.Count(u => u.MaVaiTro == "VT_TENANT");
        ViewBag.SoBiKhoa = allUsers.Count(u => u.TrangThai == "Khóa");

        ViewBag.VaiTroHienTai = maVaiTro;
        ViewBag.TrangThaiHienTai = trangThai;
        ViewBag.TuKhoaHienTai = tuKhoa;

        var danhSach = await query
            .OrderBy(u => u.MaVaiTro == "VT_ADMIN" ? 0 : 1)
            .ThenBy(u => u.TenDangNhap)
            .ToListAsync();

        return View(danhSach);
    }

    // 2. CẤP TÀI KHOẢN MỚI CHO KHÁCH THUÊ / QUẢN LÝ (GET)
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateVaiTroDropdownAsync("VT_TENANT");

        var model = new NguoiDung
        {
            MaNguoiDung = $"ND_KHACH{Random.Shared.Next(10, 99)}",
            MaVaiTro = "VT_TENANT",
            TrangThai = "Hoạt động",
            MatKhau = "123456"
        };

        return View(model);
    }

    // 3. XỬ LÝ LƯU TÀI KHOẢN MỚI (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaNguoiDung,TenDangNhap,MatKhau,MaVaiTro")] NguoiDung nguoiDung)
    {
        if (!string.IsNullOrWhiteSpace(nguoiDung.MaNguoiDung))
        {
            nguoiDung.MaNguoiDung = nguoiDung.MaNguoiDung.Trim().ToUpper();
            if (await _context.NguoiDungs.AnyAsync(u => u.MaNguoiDung == nguoiDung.MaNguoiDung))
            {
                ModelState.AddModelError("MaNguoiDung", "Mã người dùng này đã tồn tại.");
            }
        }

        if (!string.IsNullOrWhiteSpace(nguoiDung.TenDangNhap))
        {
            nguoiDung.TenDangNhap = nguoiDung.TenDangNhap.Trim();
            if (await _context.NguoiDungs.AnyAsync(u => u.TenDangNhap == nguoiDung.TenDangNhap))
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập này đã có người sử dụng.");
            }
        }

        if (ModelState.IsValid)
        {
            nguoiDung.TrangThai = "Hoạt động";
            _context.Add(nguoiDung);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cấp tài khoản '{nguoiDung.TenDangNhap}' ({nguoiDung.MaNguoiDung}) thành công.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateVaiTroDropdownAsync(nguoiDung.MaVaiTro);
        return View(nguoiDung);
    }

    // 4. CHỈNH SỬA / CẤP LẠI MẬT KHẨU TÀI KHOẢN (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var user = await _context.NguoiDungs.FindAsync(id);
        if (user == null) return NotFound();

        await PopulateVaiTroDropdownAsync(user.MaVaiTro);
        return View(user);
    }

    // 5. XỬ LÝ CẬP NHẬT TÀI KHOẢN (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaNguoiDung,TenDangNhap,MatKhau,MaVaiTro,TrangThai")] NguoiDung nguoiDung)
    {
        if (id != nguoiDung.MaNguoiDung) return NotFound();

        // Chặn trùng tên đăng nhập với người khác
        if (await _context.NguoiDungs.AnyAsync(u => u.TenDangNhap == nguoiDung.TenDangNhap && u.MaNguoiDung != id))
        {
            ModelState.AddModelError("TenDangNhap", "Tên đăng nhập này đã thuộc về một tài khoản khác.");
        }

        // Bảo vệ tài khoản Chủ trọ gốc ND_ADMIN01 không bị khóa nhầm
        if (id == "ND_ADMIN01")
        {
            nguoiDung.TrangThai = "Hoạt động";
            nguoiDung.MaVaiTro = "VT_ADMIN";
        }

        if (ModelState.IsValid)
        {
            _context.Update(nguoiDung);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật thông tin tài khoản '{nguoiDung.TenDangNhap}'.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateVaiTroDropdownAsync(nguoiDung.MaVaiTro);
        return View(nguoiDung);
    }

    // 6. KHÓA / MỞ KHÓA TÀI KHOẢN NHANH 1-CLICK
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTrangThai(string id)
    {
        if (id == "ND_ADMIN01")
        {
            TempData["Loi"] = "CẢNH BÁO: Không thể khóa tài khoản Chủ trọ quản trị gốc (ND_ADMIN01)!";
            return RedirectToAction(nameof(Index));
        }

        var user = await _context.NguoiDungs.FindAsync(id);
        if (user != null)
        {
            user.TrangThai = user.TrangThai == "Hoạt động" ? "Khóa" : "Hoạt động";
            _context.Update(user);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã chuyển trạng thái tài khoản '{user.TenDangNhap}' sang '{user.TrangThai}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateVaiTroDropdownAsync(string? selectedVaiTro)
    {
        var roles = await _context.VaiTros
            .AsNoTracking()
            .OrderBy(v => v.MaVaiTro)
            .Select(v => new
            {
                v.MaVaiTro,
                HienThi = $"{v.TenVaiTro} ({v.MaVaiTro})"
            }).ToListAsync();

        ViewBag.DanhSachVaiTro = new SelectList(roles, "MaVaiTro", "HienThi", selectedVaiTro);
    }
}