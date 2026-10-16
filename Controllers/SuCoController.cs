using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;
namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
public class SuCoController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public SuCoController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH SỰ CỐ & BẢO TRÌ (CÓ TAB THÙNG RÁC & KHÔI PHỤC 1-CLICK)
    [HttpGet]
    public async Task<IActionResult> Index(string? trangThai, string? maPhong, string? tuKhoa)
    {
        IQueryable<SuCo> query;

        if (trangThai == "DaXoa")
        {
            query = _context.SuCos
                .IgnoreQueryFilters()
                .Include(s => s.MaPhongNavigation)
                .Include(s => s.MaNguoiDungBaoNavigation)
                .AsNoTracking()
                .Where(s => s.DaXoa == true);
        }
        else
        {
            query = _context.SuCos
                .Include(s => s.MaPhongNavigation)
                .Include(s => s.MaNguoiDungBaoNavigation)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                query = query.Where(s => s.TrangThai == trangThai);
            }
        }

        if (!string.IsNullOrWhiteSpace(maPhong))
        {
            query = query.Where(s => s.MaPhong == maPhong);
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var kw = tuKhoa.Trim();
            query = query.Where(s => s.MaSuCo.Contains(kw)
                                  || s.MoTa.Contains(kw)
                                  || (s.MaPhong != null && s.MaPhong.Contains(kw)));
        }

        var allActive = await _context.SuCos.AsNoTracking().ToListAsync();
        ViewBag.TongSuCo = allActive.Count;
        ViewBag.ChoXuLy = allActive.Count(s => s.TrangThai == "Chờ xử lý");
        ViewBag.DangSua = allActive.Count(s => s.TrangThai == "Đang sửa");
        ViewBag.DaHoanThanh = allActive.Count(s => s.TrangThai == "Đã hoàn thành");
        ViewBag.TongChiPhi = allActive.Sum(s => s.ChiPhiSua ?? 0);
        ViewBag.SoDaXoa = await _context.SuCos.IgnoreQueryFilters().CountAsync(s => s.DaXoa == true);

        ViewBag.TrangThaiHienTai = trangThai;
        ViewBag.MaPhongHienTai = maPhong;
        ViewBag.TuKhoaHienTai = tuKhoa;

        // Ưu tiên đưa các sự cố "Chờ xử lý" và "Đang sửa" lên trên, "Đã hoàn thành" xuống dưới
        var danhSach = await query
            .OrderBy(s => s.TrangThai == "Đã hoàn thành" ? 1 : 0)
            .ThenByDescending(s => s.NgayTao)
            .ToListAsync();

        return View(danhSach);
    }

    // 2. GHI NHẬN SỰ CỐ MỚI (GET)
    [HttpGet]
    public async Task<IActionResult> Create(string? maPhong)
    {
        await PopulateDropdownsAsync(maPhong, "ND_ADMIN01");

        var model = new SuCo
        {
            MaSuCo = $"SC{DateTime.Now:yyMMdd}_{Random.Shared.Next(100, 999)}",
            MaPhong = maPhong,
            MaNguoiDungBao = "ND_ADMIN01",
            TrangThai = "Chờ xử lý",
            ChiPhiSua = 0
        };

        return View(model);
    }

    // 3. XỬ LÝ LƯU SỰ CỐ MỚI (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaSuCo,MaPhong,MaNguoiDungBao,MoTa,TrangThai,ChiPhiSua")] SuCo suCo, bool chuyenPhongSangDangSua = false)
    {
        if (!string.IsNullOrWhiteSpace(suCo.MaSuCo))
        {
            suCo.MaSuCo = suCo.MaSuCo.Trim().ToUpper();
            if (await _context.SuCos.IgnoreQueryFilters().AnyAsync(s => s.MaSuCo == suCo.MaSuCo))
            {
                ModelState.AddModelError("MaSuCo", "Mã sự cố này đã tồn tại trong hệ thống.");
            }
        }

        ValidateSuCoConstraints(suCo);

        if (ModelState.IsValid)
        {
            suCo.NgayTao = DateTime.Now;
            suCo.DaXoa = false;
            _context.Add(suCo);

            // Tùy chọn đồng bộ trạng thái phòng sang "Đang sửa" nếu là phòng trống cần bảo trì lớn
            if (chuyenPhongSangDangSua && !string.IsNullOrWhiteSpace(suCo.MaPhong))
            {
                var phong = await _context.PhongTros.FindAsync(suCo.MaPhong);
                if (phong != null && phong.TrangThai == "Trống")
                {
                    phong.TrangThai = "Đang sửa";
                    _context.Update(phong);
                }
            }

            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã ghi nhận phiếu báo cáo sự cố #{suCo.MaSuCo} tại phòng {suCo.MaPhong ?? "Khu vực chung"}.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(suCo.MaPhong, suCo.MaNguoiDungBao);
        return View(suCo);
    }

    // 4. CẬP NHẬT TRẠNG THÁI XỬ LÝ & CHI PHÍ SỬA CHỮA (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var suCo = await _context.SuCos.FindAsync(id);
        if (suCo == null) return NotFound();

        await PopulateDropdownsAsync(suCo.MaPhong, suCo.MaNguoiDungBao);
        return View(suCo);
    }

    // 5. XỬ LÝ CẬP NHẬT SỰ CỐ & TỰ ĐỘNG MỞ LẠI PHÒNG KHI BẢO TRÌ XONG (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaSuCo,MaPhong,MaNguoiDungBao,MoTa,TrangThai,ChiPhiSua,NgayTao")] SuCo suCo)
    {
        if (id != suCo.MaSuCo) return NotFound();

        ValidateSuCoConstraints(suCo);

        if (ModelState.IsValid)
        {
            suCo.DaXoa = false;
            _context.Update(suCo);

            // Nếu sự cố chuyển sang "Đã hoàn thành", kiểm tra nếu phòng đó đang ở trạng thái "Đang sửa" và hết sự cố mở thì trả về "Trống"
            if (suCo.TrangThai == "Đã hoàn thành" && !string.IsNullOrWhiteSpace(suCo.MaPhong))
            {
                bool conSuCoKhac = await _context.SuCos
                    .AnyAsync(s => s.MaPhong == suCo.MaPhong && s.MaSuCo != id && s.TrangThai != "Đã hoàn thành");

                if (!conSuCoKhac)
                {
                    var phong = await _context.PhongTros.FindAsync(suCo.MaPhong);
                    if (phong != null && phong.TrangThai == "Đang sửa")
                    {
                        phong.TrangThai = "Trống";
                        _context.Update(phong);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật sự cố #{suCo.MaSuCo} sang trạng thái '{suCo.TrangThai}' (Chi phí: {(suCo.ChiPhiSua ?? 0):N0}đ).";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(suCo.MaPhong, suCo.MaNguoiDungBao);
        return View(suCo);
    }

    // 6. XÓA MỀM & KHÔI PHỤC SỰ CỐ
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var suCo = await _context.SuCos.FindAsync(id);
        if (suCo != null)
        {
            _context.SuCos.Remove(suCo);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã ẩn phiếu sự cố #{id} vào danh sách lưu trữ.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var suCo = await _context.SuCos.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.MaSuCo == id);
        if (suCo != null)
        {
            suCo.DaXoa = false;
            _context.SuCos.Update(suCo);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã khôi phục phiếu sự cố #{id} hoạt động trở lại.";
        }
        return RedirectToAction(nameof(Index));
    }

    private void ValidateSuCoConstraints(SuCo suCo)
    {
        // Khớp CHECK (TrangThai IN (N'Chờ xử lý', N'Đang sửa', N'Đã hoàn thành'))
        var validStates = new[] { "Chờ xử lý", "Đang sửa", "Đã hoàn thành" };
        if (string.IsNullOrWhiteSpace(suCo.TrangThai) || !validStates.Contains(suCo.TrangThai))
        {
            ModelState.AddModelError("TrangThai", "Trạng thái sự cố chỉ nhận: 'Chờ xử lý', 'Đang sửa' hoặc 'Đã hoàn thành'.");
        }

        // Khớp CK__SuCo__ChiPhiSua__09A971A2: [ChiPhiSua] >= (0)
        if (suCo.ChiPhiSua.HasValue && suCo.ChiPhiSua.Value < 0)
        {
            ModelState.AddModelError("ChiPhiSua", "Chi phí sửa chữa không được nhỏ hơn 0đ.");
        }
    }

    private async Task PopulateDropdownsAsync(string? selectedPhong, string? selectedNguoiDung)
    {
        var rooms = await _context.PhongTros
            .AsNoTracking()
            .OrderBy(p => p.MaPhong)
            .Select(p => new
            {
                p.MaPhong,
                HienThi = $"{p.MaPhong} — {p.TenPhong} ({p.TrangThai})"
            }).ToListAsync();

        var users = await _context.NguoiDungs
            .AsNoTracking()
            .OrderBy(u => u.TenDangNhap)
            .Select(u => new
            {
                u.MaNguoiDung,
                HienThi = $"{u.TenDangNhap} (Mã: {u.MaNguoiDung})"
            }).ToListAsync();

        ViewBag.DanhSachPhong = new SelectList(rooms, "MaPhong", "HienThi", selectedPhong);
        ViewBag.DanhSachNguoiDung = new SelectList(users, "MaNguoiDung", "HienThi", selectedNguoiDung);
    }
}