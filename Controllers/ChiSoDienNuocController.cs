using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
public class ChiSoDienNuocController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;
    private readonly IWebHostEnvironment _env;

    public ChiSoDienNuocController(QuanLyNhaTroBinhMinhContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
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

    // 4. XỬ LÝ LƯU CHỈ SỐ ĐIỆN NƯỚC & TẢI NHIỀU ẢNH ĐỒNG HỒ TỪ MÁY TÍNH (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("MaChiSo,MaPhong,Thang,Nam,DienCu,DienMoi,NuocCu,NuocMoi")] ChiSoDienNuoc chiSo,
        List<IFormFile>? filesAnhDongHo)
    {
        if (!string.IsNullOrWhiteSpace(chiSo.MaChiSo))
        {
            chiSo.MaChiSo = chiSo.MaChiSo.Trim().ToUpper();
            if (await _context.ChiSoDienNuocs.AnyAsync(c => c.MaChiSo == chiSo.MaChiSo))
            {
                ModelState.AddModelError("MaChiSo", "Mã chỉ số này đã tồn tại trong hệ thống.");
            }
        }

        if (!string.IsNullOrWhiteSpace(chiSo.MaPhong) && chiSo.Thang.HasValue && chiSo.Nam.HasValue)
        {
            bool daChotKyNay = await _context.ChiSoDienNuocs.AnyAsync(c =>
                c.MaPhong == chiSo.MaPhong && c.Thang == chiSo.Thang && c.Nam == chiSo.Nam);

            if (daChotKyNay)
            {
                ModelState.AddModelError("MaPhong", $"Phòng {chiSo.MaPhong} đã được chốt chỉ số cho kỳ Tháng {chiSo.Thang}/{chiSo.Nam} rồi! Vui lòng bấm Sửa nếu muốn điều chỉnh.");
            }
        }

        ValidateChiSoConstraints(chiSo);
        ValidateDanhSachFileAnh(filesAnhDongHo);

        if (ModelState.IsValid)
        {
            if (filesAnhDongHo != null && filesAnhDongHo.Any(f => f.Length > 0))
            {
                var dsDuongDan = await LuuNhieuFileAnhDongHoAsync(filesAnhDongHo, chiSo.MaChiSo);
                chiSo.AnhDongHo = string.Join(";", dsDuongDan);
            }

            _context.Add(chiSo);
            await _context.SaveChangesAsync();

            int soAnh = string.IsNullOrWhiteSpace(chiSo.AnhDongHo) ? 0 : chiSo.AnhDongHo.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
            TempData["ThongBao"] = $"Đã chốt điện nước phòng {chiSo.MaPhong} kỳ {chiSo.Thang:D2}/{chiSo.Nam} (Đính kèm {soAnh} ảnh đồng hồ).";
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

    // 6. XỬ LÝ CẬP NHẬT CHỈ SỐ & THÊM/XÓA ẢNH ĐỒNG HỒ (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        [Bind("MaChiSo,MaPhong,Thang,Nam,DienCu,DienMoi,NuocCu,NuocMoi")] ChiSoDienNuoc chiSo,
        List<string>? anhGiuLai,
        List<IFormFile>? filesAnhDongHo)
    {
        if (id != chiSo.MaChiSo) return NotFound();

        ValidateChiSoConstraints(chiSo);
        ValidateDanhSachFileAnh(filesAnhDongHo, anhGiuLai?.Count ?? 0);
        var banGhiGoc = await _context.ChiSoDienNuocs.AsNoTracking().FirstOrDefaultAsync(c => c.MaChiSo == id);
        if (banGhiGoc != null && (banGhiGoc.DienCu != chiSo.DienCu || banGhiGoc.DienMoi != chiSo.DienMoi || banGhiGoc.NuocCu != chiSo.NuocCu || banGhiGoc.NuocMoi != chiSo.NuocMoi))
        {
            bool daCoHoaDonThuTien = await _context.HoaDons
                .Include(h => h.MaHopDongNavigation)
                .AnyAsync(h => h.MaHopDongNavigation != null
                            && h.MaHopDongNavigation.MaPhong == chiSo.MaPhong
                            && h.Thang == chiSo.Thang
                           && h.Nam == chiSo.Nam
                            && h.TrangThai != "Chưa thanh toán");

            if (daCoHoaDonThuTien)
            {
                ModelState.AddModelError("", $"Kỳ Tháng {chiSo.Thang:D2}/{chiSo.Nam} của phòng {chiSo.MaPhong} đã phát hành hóa đơn và đã thu tiền! Bạn chỉ có thể cập nhật ảnh minh chứng, không được thay đổi số liệu đồng hồ.");
            }
        }

        if (ModelState.IsValid)
        {
            // Tập hợp các ảnh cũ được tích giữ lại
            var danhSachAnh = new List<string>();
            if (anhGiuLai != null && anhGiuLai.Any())
            {
                danhSachAnh.AddRange(anhGiuLai.Where(a => !string.IsNullOrWhiteSpace(a)));
            }

            // Cộng thêm các ảnh mới vừa chọn từ máy tính (nếu có)
            if (filesAnhDongHo != null && filesAnhDongHo.Any(f => f.Length > 0))
            {
                var anhMoi = await LuuNhieuFileAnhDongHoAsync(filesAnhDongHo, chiSo.MaChiSo);
                danhSachAnh.AddRange(anhMoi);
            }

            chiSo.AnhDongHo = danhSachAnh.Any() ? string.Join(";", danhSachAnh) : null;

            _context.Update(chiSo);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật phiếu chốt #{chiSo.MaChiSo} (Hiện có {danhSachAnh.Count} ảnh minh chứng).";
            return RedirectToAction(nameof(Index));
        }

        await PopulatePhongDropdownAsync(chiSo.MaPhong);
        return View(chiSo);
    }

    private void ValidateDanhSachFileAnh(List<IFormFile>? files, int soAnhCuGiuLai = 0)
    {
        int soAnhMoi = files?.Count(f => f.Length > 0) ?? 0;
        if (soAnhCuGiuLai + soAnhMoi > 4)
        {
            ModelState.AddModelError("AnhDongHo", $"Tối đa chỉ được đính kèm 4 ảnh đồng hồ cho mỗi kỳ chốt (Hiện bạn đang chọn {soAnhCuGiuLai + soAnhMoi} ảnh).");
            return;
        }

        if (files == null || !files.Any()) return;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("AnhDongHo", $"File '{file.FileName}' không hợp lệ. Chỉ chấp nhận ảnh .jpg, .jpeg, .png hoặc .webp.");
                break;
            }
        }
    }

    // LƯU NHIỀU FILE ẢNH VỚI ĐƯỜNG DẪN NGẮN GỌN (/uploads/dh/...) ĐỂ LƯU ĐƯỢC NHIỀU ẢNH TRONG CỘT AnhDongHo
    private async Task<List<string>> LuuNhieuFileAnhDongHoAsync(List<IFormFile> files, string maChiSo)
    {
        var ketQua = new List<string>();
        string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "dh");
        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        string safeCode = string.Concat(maChiSo.Where(char.IsLetterOrDigit));
        int stt = 1;

        foreach (var file in files.Where(f => f.Length > 0))
        {
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            string fileName = $"{safeCode}_{stt++}_{Random.Shared.Next(100, 999)}{ext}";
            string fullPath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            ketQua.Add($"/uploads/dh/{fileName}");
        }

        return ketQua;
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