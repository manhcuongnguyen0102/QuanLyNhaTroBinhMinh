using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

public class ChiSoDienNuocController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public ChiSoDienNuocController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH CHỐT ĐIỆN NƯỚC THEO KỲ THÁNG / NĂM
    [HttpGet]
    public async Task<IActionResult> Index(int? thang, int? nam, string? maPhong)
    {
        var query = _context.ChiSoDienNuocs
            .Include(c => c.MaPhongNavigation)
            .AsNoTracking()
            .AsQueryable();

        if (thang.HasValue && thang >= 1 && thang <= 12)
        {
            query = query.Where(c => c.Thang == thang.Value);
        }

        if (nam.HasValue && nam > 2000)
        {
            query = query.Where(c => c.Nam == nam.Value);
        }

        if (!string.IsNullOrWhiteSpace(maPhong))
        {
            query = query.Where(c => c.MaPhong == maPhong);
        }

        ViewBag.ThangHienTai = thang;
        ViewBag.NamHienTai = nam;
        ViewBag.MaPhongHienTai = maPhong;

        var rooms = await _context.PhongTros.AsNoTracking().OrderBy(p => p.MaPhong).ToListAsync();
        ViewBag.DanhSachPhong = new SelectList(rooms, "MaPhong", "TenPhong", maPhong);

        var danhSach = await query
            .OrderByDescending(c => c.Nam)
            .ThenByDescending(c => c.Thang)
            .ThenBy(c => c.MaPhong)
            .ToListAsync();

        return View(danhSach);
    }

    // 2. API JSON: TỰ ĐỘNG TRA CỨU CHỈ SỐ MỚI CỦA KỲ TRƯỚC ĐỂ ĐIỀN VÀO CHỈ SỐ CŨ KỲ NÀY
    [HttpGet]
    public async Task<IActionResult> LayChiSoCu(string maPhong, int thang, int nam)
    {
        if (string.IsNullOrWhiteSpace(maPhong))
        {
            return Json(new { dienCu = 0, nuocCu = 0, coDuLieuCu = false });
        }

        // Tìm bản ghi chốt điện nước gần nhất trước kỳ (thang/nam) của phòng này
        var banGhiTruoc = await _context.ChiSoDienNuocs
            .AsNoTracking()
            .Where(c => c.MaPhong == maPhong && (c.Nam < nam || (c.Nam == nam && c.Thang < thang)))
            .OrderByDescending(c => c.Nam)
            .ThenByDescending(c => c.Thang)
            .FirstOrDefaultAsync();

        if (banGhiTruoc != null)
        {
            return Json(new
            {
                dienCu = banGhiTruoc.DienMoi ?? 0,
                nuocCu = banGhiTruoc.NuocMoi ?? 0,
                kyTruoc = $"{banGhiTruoc.Thang:D2}/{banGhiTruoc.Nam}",
                coDuLieuCu = true
            });
        }

        return Json(new { dienCu = 0, nuocCu = 0, coDuLieuCu = false });
    }

    // 3. TRANG GHI CHỈ SỐ ĐIỆN NƯỚC MỚI (GET)
    [HttpGet]
    public async Task<IActionResult> Create(string? maPhong)
    {
        int thangHienTai = DateTime.Today.Month;
        int namHienTai = DateTime.Today.Year;

        await PopulatePhongDropdownAsync(maPhong);

        var model = new ChiSoDienNuoc
        {
            MaChiSo = $"CS{namHienTai % 100}{thangHienTai:D2}_{Random.Shared.Next(100, 999)}",
            MaPhong = maPhong,
            Thang = thangHienTai,
            Nam = namHienTai,
            DienCu = 0,
            DienMoi = 0,
            NuocCu = 0,
            NuocMoi = 0
        };

        return View(model);
    }

    // 4. XỬ LÝ LƯU CHỈ SỐ ĐIỆN NƯỚC (POST - KHỚP TOÀN BỘ CHECK CONSTRAINTS SQL)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaChiSo,MaPhong,Thang,Nam,DienCu,DienMoi,NuocCu,NuocMoi,AnhDongHo")] ChiSoDienNuoc chiSo)
    {
        if (!string.IsNullOrWhiteSpace(chiSo.MaChiSo))
        {
            chiSo.MaChiSo = chiSo.MaChiSo.Trim().ToUpper();
            if (await _context.ChiSoDienNuocs.AnyAsync(c => c.MaChiSo == chiSo.MaChiSo))
            {
                ModelState.AddModelError("MaChiSo", "Mã chỉ số này đã tồn tại trong hệ thống.");
            }
        }

        // Kiểm tra xem phòng đã chốt chỉ số trong Tháng/Năm này chưa
        if (!string.IsNullOrWhiteSpace(chiSo.MaPhong) && chiSo.Thang.HasValue && chiSo.Nam.HasValue)
        {
            bool daChotKyNay = await _context.ChiSoDienNuocs.AnyAsync(c =>
                c.MaPhong == chiSo.MaPhong && c.Thang == chiSo.Thang && c.Nam == chiSo.Nam);

            if (daChotKyNay)
            {
                ModelState.AddModelError("MaPhong", $"Phòng {chiSo.MaPhong} đã được chốt chỉ số cho kỳ Tháng {chiSo.Thang}/{chiSo.Nam} rồi! Vui lòng bấm Sửa nếu muốn điều chỉnh.");
            }
        }

        // Kiểm tra ràng buộc SQL: Thang (1-12), Nam (>2000), DienCu >= 0, NuocCu >= 0, DienMoi >= DienCu, NuocMoi >= NuocCu
        ValidateChiSoConstraints(chiSo);

        if (ModelState.IsValid)
        {
            _context.Add(chiSo);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã chốt điện nước phòng {chiSo.MaPhong} kỳ {chiSo.Thang:D2}/{chiSo.Nam} (Tiêu thụ: {(chiSo.DienMoi - chiSo.DienCu)} kWh điện, {(chiSo.NuocMoi - chiSo.NuocCu)} khối nước).";
            return RedirectToAction(nameof(Index));
        }

        await PopulatePhongDropdownAsync(chiSo.MaPhong);
        return View(chiSo);
    }

    // 5. CHỈNH SỬA CHỈ SỐ ĐIỆN NƯỚC (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var chiSo = await _context.ChiSoDienNuocs.FindAsync(id);
        if (chiSo == null) return NotFound();

        await PopulatePhongDropdownAsync(chiSo.MaPhong);
        return View(chiSo);
    }

    // 6. XỬ LÝ CẬP NHẬT CHỈ SỐ ĐIỆN NƯỚC (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaChiSo,MaPhong,Thang,Nam,DienCu,DienMoi,NuocCu,NuocMoi,AnhDongHo")] ChiSoDienNuoc chiSo)
    {
        if (id != chiSo.MaChiSo) return NotFound();

        ValidateChiSoConstraints(chiSo);

        if (ModelState.IsValid)
        {
            _context.Update(chiSo);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật chỉ số điện nước mã #{chiSo.MaChiSo} (Phòng {chiSo.MaPhong} — Kỳ {chiSo.Thang:D2}/{chiSo.Nam}).";
            return RedirectToAction(nameof(Index));
        }

        await PopulatePhongDropdownAsync(chiSo.MaPhong);
        return View(chiSo);
    }

    private void ValidateChiSoConstraints(ChiSoDienNuoc chiSo)
    {
        if (!chiSo.Thang.HasValue || chiSo.Thang < 1 || chiSo.Thang > 12)
            ModelState.AddModelError("Thang", "Tháng chốt chỉ số phải từ 1 đến 12.");

        if (!chiSo.Nam.HasValue || chiSo.Nam <= 2000)
            ModelState.AddModelError("Nam", "Năm chốt chỉ số phải lớn hơn 2000.");

        if (!chiSo.DienCu.HasValue || chiSo.DienCu < 0)
            ModelState.AddModelError("DienCu", "Chỉ số điện cũ phải >= 0.");

        if (!chiSo.NuocCu.HasValue || chiSo.NuocCu < 0)
            ModelState.AddModelError("NuocCu", "Chỉ số nước cũ phải >= 0.");

        // Khớp với CHK_ChiSo: ([DienMoi]>=[DienCu] AND [NuocMoi]>=[NuocCu])
        if (!chiSo.DienMoi.HasValue || chiSo.DienMoi < chiSo.DienCu)
            ModelState.AddModelError("DienMoi", "Chỉ số điện mới bắt buộc phải lớn hơn hoặc bằng chỉ số điện cũ.");

        if (!chiSo.NuocMoi.HasValue || chiSo.NuocMoi < chiSo.NuocCu)
            ModelState.AddModelError("NuocMoi", "Chỉ số nước mới bắt buộc phải lớn hơn hoặc bằng chỉ số nước cũ.");
    }

    private async Task PopulatePhongDropdownAsync(string? selectedPhong)
    {
        var rooms = await _context.PhongTros
            .AsNoTracking()
            .OrderByDescending(p => p.TrangThai == "Đang thuê")
            .ThenBy(p => p.MaPhong)
            .Select(p => new
            {
                p.MaPhong,
                HienThi = $"{p.MaPhong} — {p.TenPhong} ({p.TrangThai})"
            })
            .ToListAsync();

        ViewBag.DanhSachPhong = new SelectList(rooms, "MaPhong", "HienThi", selectedPhong);
    }
}