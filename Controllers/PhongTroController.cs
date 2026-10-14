using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

public class PhongTroController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public PhongTroController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH PHÒNG TRỌ (GRID CARD + LỌC TRẠNG THÁI & TỪ KHÓA)
    [HttpGet]
    public async Task<IActionResult> Index(string? trangThai, string? tuKhoa)
    {
        var query = _context.PhongTros.AsNoTracking().AsQueryable();

        // Lọc theo trạng thái nếu người dùng chọn trên thanh Filter
        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(p => p.TrangThai == trangThai);
        }

        // Tìm kiếm theo Mã phòng hoặc Tên phòng
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var keyword = tuKhoa.Trim();
            query = query.Where(p => p.MaPhong.Contains(keyword) || p.TenPhong.Contains(keyword));
        }

        // Thống kê nhanh số lượng để hiển thị trên đầu trang
        var allRooms = await _context.PhongTros.AsNoTracking().ToListAsync();
        ViewBag.TongSoPhong = allRooms.Count;
        ViewBag.SoPhongTrong = allRooms.Count(p => p.TrangThai == "Trống");
        ViewBag.SoPhongDangThue = allRooms.Count(p => p.TrangThai == "Đang thuê");
        ViewBag.SoPhongBaoTri = allRooms.Count(p => p.TrangThai == "Bảo trì");

        ViewBag.TrangThaiHienTai = trangThai;
        ViewBag.TuKhoaHienTai = tuKhoa;

        var danhSachPhong = await query.OrderBy(p => p.MaPhong).ToListAsync();
        return View(danhSachPhong);
    }

    // 2. TRANG BIỂU MẪU THÊM PHÒNG MỚI (GET)
    [HttpGet]
    public IActionResult Create()
    {
        var model = new PhongTro
        {
            TrangThai = "Trống"
        };
        return View(model);
    }

    // 3. XỬ LÝ LƯU PHÒNG MỚI (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaPhong,TenPhong,DienTich,GiaThueCung,TrangThai")] PhongTro phongTro)
    {
        if (!string.IsNullOrWhiteSpace(phongTro.MaPhong))
        {
            phongTro.MaPhong = phongTro.MaPhong.Trim().ToUpper();
            bool daTonTai = await _context.PhongTros.AnyAsync(p => p.MaPhong == phongTro.MaPhong);
            if (daTonTai)
            {
                ModelState.AddModelError("MaPhong", "Mã phòng này đã tồn tại trong hệ thống.");
            }
        }

        if (ModelState.IsValid)
        {
            _context.Add(phongTro);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã thêm thành công phòng {phongTro.TenPhong} ({phongTro.MaPhong}).";
            return RedirectToAction(nameof(Index));
        }

        return View(phongTro);
    }

    // 4. TRANG BIỂU MẪU CHỈNH SỬA PHÒNG (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var phongTro = await _context.PhongTros.FindAsync(id);
        if (phongTro == null)
        {
            return NotFound();
        }

        return View(phongTro);
    }

    // 5. XỬ LÝ CẬP NHẬT PHÒNG (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaPhong,TenPhong,DienTich,GiaThueCung,TrangThai")] PhongTro phongTro)
    {
        if (id != phongTro.MaPhong)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(phongTro);
                await _context.SaveChangesAsync();
                TempData["ThongBao"] = $"Đã cập nhật thông tin phòng {phongTro.TenPhong}.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.PhongTros.AnyAsync(e => e.MaPhong == phongTro.MaPhong))
                {
                    return NotFound();
                }
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        return View(phongTro);
    }

    // 6. XỬ LÝ XÓA PHÒNG AN TOÀN (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var phongTro = await _context.PhongTros
            .Include(p => p.HopDongs)
            .Include(p => p.ChiSoDienNuocs)
            .Include(p => p.SuCos)
            .FirstOrDefaultAsync(p => p.MaPhong == id);

        if (phongTro == null)
        {
            return NotFound();
        }

        // Kiểm tra nếu phòng đã phát sinh dữ liệu liên kết và chưa có cột DaXoa
        bool coDuLieuLienKet = phongTro.HopDongs.Any() || phongTro.ChiSoDienNuocs.Any() || phongTro.SuCos.Any();
        var coCotDaXoa = _context.Entry(phongTro).Metadata.FindProperty("DaXoa") != null;

        if (coDuLieuLienKet && !coCotDaXoa)
        {
            TempData["Loi"] = $"Không thể xóa cứng phòng {phongTro.TenPhong} vì đã có dữ liệu Hợp đồng/Điện nước/Sự cố liên kết.";
            return RedirectToAction(nameof(Index));
        }

        _context.PhongTros.Remove(phongTro);
        await _context.SaveChangesAsync();
        TempData["ThongBao"] = $"Đã xóa phòng {phongTro.TenPhong} ({phongTro.MaPhong}).";

        return RedirectToAction(nameof(Index));
    }
}