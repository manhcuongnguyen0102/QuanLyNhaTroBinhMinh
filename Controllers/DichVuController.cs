using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
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
        IQueryable<DichVu> query;

        if (loaiDichVu == "DaXoa")
        {
            // Dùng IgnoreQueryFilters() để lấy các dịch vụ đã bị xóa mềm (DaXoa == true)
            query = _context.DichVus.IgnoreQueryFilters().AsNoTracking().Where(d => d.DaXoa == true);
        }
        else
        {
            query = _context.DichVus.AsNoTracking().AsQueryable();

            if (loaiDichVu == "CoDinh")
            {
                query = query.Where(d => d.LaCoDinh == true);
            }
            else if (loaiDichVu == "TheoChiSo")
            {
                query = query.Where(d => d.LaCoDinh != true);
            }
        }

        var activeServices = await _context.DichVus.AsNoTracking().ToListAsync();
        ViewBag.TongDichVu = activeServices.Count;
        ViewBag.SoTheoChiSo = activeServices.Count(d => d.LaCoDinh != true);
        ViewBag.SoCoDinh = activeServices.Count(d => d.LaCoDinh == true);
        ViewBag.SoDaXoa = await _context.DichVus.IgnoreQueryFilters().CountAsync(d => d.DaXoa == true);
        ViewBag.LoaiHienTai = loaiDichVu;

        var danhSach = await query
            .OrderBy(d => d.LaCoDinh == true ? 1 : 0)
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
    // 7. KHÔI PHỤC DỊCH VỤ ĐÃ XÓA MỀM (1-CLICK)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var dichVu = await _context.DichVus
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.MaDichVu == id);

        if (dichVu != null)
        {
            dichVu.DaXoa = false;
            _context.DichVus.Update(dichVu);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã khôi phục dịch vụ {dichVu.TenDichVu} ({dichVu.MaDichVu}) hoạt động trở lại.";
        }

        return RedirectToAction(nameof(Index));
    }
}