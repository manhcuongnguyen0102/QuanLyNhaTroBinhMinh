using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;
namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
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
        ViewBag.SoPhongBaoTri = allRooms.Count(p => p.TrangThai == "Đang sửa");

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
        // Đặt trong hàm Edit [HttpPost] của PhongTroController:
        bool coHopDongHieuLuc = await _context.HopDongs
            .AnyAsync(h => h.MaPhong == phongTro.MaPhong && h.TrangThai == "Hiệu lực");

        if (coHopDongHieuLuc && phongTro.TrangThai != "Đang thuê")
        {
            ModelState.AddModelError("TrangThai", $"Phòng {phongTro.MaPhong} đang có Hợp đồng đang Hiệu lực! Không thể chuyển thủ công sang '{phongTro.TrangThai}'. Vui lòng thực hiện Thanh lý hợp đồng nếu khách trả phòng.");
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
        // Đặt ở đầu hàm Delete [HttpPost] của PhongTroController:
        bool dangCoKhachThue = await _context.HopDongs
            .AnyAsync(h => h.MaPhong == id && h.TrangThai == "Hiệu lực");

        if (dangCoKhachThue)
        {
            TempData["Loi"] = $"Không thể xóa phòng {id} vì phòng đang có Hợp đồng thuê đang Hiệu lực!";
            return RedirectToAction(nameof(Index));
        }
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
        // Đặt ở đầu hàm Delete [HttpPost] của PhongTroController:
      
        _context.PhongTros.Remove(phongTro);
        await _context.SaveChangesAsync();
        TempData["ThongBao"] = $"Đã xóa phòng {phongTro.TenPhong} ({phongTro.MaPhong}).";

        return RedirectToAction(nameof(Index));
    }
}