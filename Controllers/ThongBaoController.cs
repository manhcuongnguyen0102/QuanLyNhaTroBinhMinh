using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
public class ThongBaoController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public ThongBaoController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH BẢNG TIN THÔNG BÁO TOÀN KHU TRỌ
    [HttpGet]
    public async Task<IActionResult> Index(string? cheDo, string? tuKhoa)
    {
        IQueryable<ThongBao> query;

        if (cheDo == "DaXoa")
        {
            query = _context.ThongBaos
                .IgnoreQueryFilters()
                .Include(t => t.MaNguoiDungTaoNavigation)
                .AsNoTracking()
                .Where(t => t.DaXoa == true);
        }
        else
        {
            query = _context.ThongBaos
                .Include(t => t.MaNguoiDungTaoNavigation)
                .AsNoTracking()
                .AsQueryable();
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var kw = tuKhoa.Trim();
            query = query.Where(t => t.TieuDe.Contains(kw) || t.NoiDung.Contains(kw) || t.MaThongBao.Contains(kw));
        }

        ViewBag.TongHienHanh = await _context.ThongBaos.CountAsync();
        ViewBag.SoDaXoa = await _context.ThongBaos.IgnoreQueryFilters().CountAsync(t => t.DaXoa == true);
        ViewBag.CheDoHienTai = cheDo;
        ViewBag.TuKhoaHienTai = tuKhoa;

        var danhSach = await query.OrderByDescending(t => t.NgayTao).ToListAsync();
        return View(danhSach);
    }

    // 2. ĐĂNG THÔNG BÁO MỚI (GET)
    [HttpGet]
    public IActionResult Create()
    {
        var model = new ThongBao
        {
            MaThongBao = $"TB{DateTime.Now:yyMMdd}_{Random.Shared.Next(100, 999)}",
            MaNguoiDungTao = "ND_ADMIN01"
        };
        return View(model);
    }

    // 3. XỬ LÝ LƯU THÔNG BÁO MỚI (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaThongBao,TieuDe,NoiDung,MaNguoiDungTao")] ThongBao thongBao)
    {
        if (!string.IsNullOrWhiteSpace(thongBao.MaThongBao))
        {
            thongBao.MaThongBao = thongBao.MaThongBao.Trim().ToUpper();
            if (await _context.ThongBaos.IgnoreQueryFilters().AnyAsync(t => t.MaThongBao == thongBao.MaThongBao))
            {
                ModelState.AddModelError("MaThongBao", "Mã thông báo này đã tồn tại.");
            }
        }

        if (ModelState.IsValid)
        {
            thongBao.MaNguoiDungTao ??= "ND_ADMIN01";
            thongBao.NgayTao = DateTime.Now;
            thongBao.DaXoa = false;

            _context.Add(thongBao);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã phát hành bản tin '{thongBao.TieuDe}' tới toàn bộ khách thuê trọ.";
            return RedirectToAction(nameof(Index));
        }

        return View(thongBao);
    }

    // 4. CHỈNH SỬA THÔNG BÁO (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var tb = await _context.ThongBaos.FindAsync(id);
        if (tb == null) return NotFound();

        return View(tb);
    }

    // 5. XỬ LÝ CẬP NHẬT THÔNG BÁO (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaThongBao,TieuDe,NoiDung,MaNguoiDungTao,NgayTao")] ThongBao thongBao)
    {
        if (id != thongBao.MaThongBao) return NotFound();

        if (ModelState.IsValid)
        {
            thongBao.DaXoa = false;
            _context.Update(thongBao);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật nội dung bản tin #{thongBao.MaThongBao}.";
            return RedirectToAction(nameof(Index));
        }

        return View(thongBao);
    }

    // 6. ẨN (XÓA MỀM) & KHÔI PHỤC THÔNG BÁO
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var tb = await _context.ThongBaos.FindAsync(id);
        if (tb != null)
        {
            _context.ThongBaos.Remove(tb);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã gỡ thông báo #{id} khỏi bảng tin hiển thị.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var tb = await _context.ThongBaos.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.MaThongBao == id);
        if (tb != null)
        {
            tb.DaXoa = false;
            _context.ThongBaos.Update(tb);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã khôi phục thông báo #{id} lên lại bảng tin.";
        }
        return RedirectToAction(nameof(Index));
    }
}