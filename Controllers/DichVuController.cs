using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

public class DichVuController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public DichVuController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH ĐƠN GIÁ DỊCH VỤ (BẢNG SB ADMIN 2 + LỌC LOẠI DỊCH VỤ)
    [HttpGet]
    public async Task<IActionResult> Index(string? loaiDichVu)
    {
        var query = _context.DichVus.AsNoTracking().AsQueryable();

        if (loaiDichVu == "CoDinh")
        {
            query = query.Where(d => d.LaCoDinh == true);
        }
        else if (loaiDichVu == "TheoChiSo")
        {
            query = query.Where(d => d.LaCoDinh != true);
        }

        var allServices = await _context.DichVus.AsNoTracking().ToListAsync();
        ViewBag.TongDichVu = allServices.Count;
        ViewBag.SoTheoChiSo = allServices.Count(d => d.LaCoDinh != true);
        ViewBag.SoCoDinh = allServices.Count(d => d.LaCoDinh == true);
        ViewBag.LoaiHienTai = loaiDichVu;

        var danhSach = await query
            .OrderBy(d => d.LaCoDinh == true ? 1 : 0) // Ưu tiên Điện/Nước lên đầu
            .ThenBy(d => d.MaDichVu)
            .ToListAsync();

        return View(danhSach);
    }

    // 2. THÊM DỊCH VỤ MỚI (GET)
    [HttpGet]
    public IActionResult Create()
    {
        var model = new DichVu
        {
            DonViTinh = "Tháng",
            LaCoDinh = true,
            DaXoa = false
        };
        return View(model);
    }

    // 3. XỬ LÝ LƯU DỊCH VỤ MỚI (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaDichVu,TenDichVu,DonViTinh,DonGia,LaCoDinh")] DichVu dichVu)
    {
        if (!string.IsNullOrWhiteSpace(dichVu.MaDichVu))
        {
            dichVu.MaDichVu = dichVu.MaDichVu.Trim().ToUpper();
            if (await _context.DichVus.IgnoreQueryFilters().AnyAsync(d => d.MaDichVu == dichVu.MaDichVu))
            {
                ModelState.AddModelError("MaDichVu", "Mã dịch vụ này đã tồn tại trong hệ thống.");
            }
        }

        // Khớp với CK__DichVu__DonGia__656C112C: [DonGia] >= (0)
        if (dichVu.DonGia < 0)
        {
            ModelState.AddModelError("DonGia", "Đơn giá dịch vụ phải lớn hơn hoặc bằng 0.");
        }

        if (ModelState.IsValid)
        {
            dichVu.DaXoa = false;
            _context.Add(dichVu);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã thêm dịch vụ {dichVu.TenDichVu} ({dichVu.MaDichVu}) thành công.";
            return RedirectToAction(nameof(Index));
        }

        return View(dichVu);
    }

    // 4. CHỈNH SỬA ĐƠN GIÁ DỊCH VỤ (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var dichVu = await _context.DichVus.FindAsync(id);
        if (dichVu == null) return NotFound();

        return View(dichVu);
    }

    // 5. XỬ LÝ CẬP NHẬT ĐƠN GIÁ (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaDichVu,TenDichVu,DonViTinh,DonGia,LaCoDinh")] DichVu dichVu)
    {
        if (id != dichVu.MaDichVu) return NotFound();

        if (dichVu.DonGia < 0)
        {
            ModelState.AddModelError("DonGia", "Đơn giá dịch vụ không được âm.");
        }

        // Bảo vệ 2 dịch vụ cốt lõi DV_DIEN và DV_NUOC luôn ở chế độ tính theo chỉ số đồng hồ (LaCoDinh = false)
        if (dichVu.MaDichVu == "DV_DIEN" || dichVu.MaDichVu == "DV_NUOC")
        {
            dichVu.LaCoDinh = false;
        }

        if (ModelState.IsValid)
        {
            dichVu.DaXoa = false;
            _context.Update(dichVu);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật đơn giá dịch vụ {dichVu.TenDichVu} thành {dichVu.DonGia:N0}đ/{dichVu.DonViTinh}.";
            return RedirectToAction(nameof(Index));
        }

        return View(dichVu);
    }

    // 6. XÓA MỀM DỊCH VỤ (CHẶN XÓA DV_DIEN & DV_NUOC ĐỂ BẢO VỆ THUẬT TOÁN HÓA ĐƠN)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == "DV_DIEN" || id == "DV_NUOC")
        {
            TempData["Loi"] = "CẢNH BÁO HỆ THỐNG: Không thể xóa dịch vụ Điện (DV_DIEN) và Nước (DV_NUOC) vì đây là 2 dịch vụ cốt lõi dùng để tính chỉ số đồng hồ hàng tháng!";
            return RedirectToAction(nameof(Index));
        }

        var dichVu = await _context.DichVus.FindAsync(id);
        if (dichVu != null)
        {
            _context.DichVus.Remove(dichVu); // Tự động chuyển thành DaXoa = true nhờ SoftDelete.cs
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa mềm dịch vụ {dichVu.TenDichVu} ({dichVu.MaDichVu}).";
        }

        return RedirectToAction(nameof(Index));
    }
}