using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

[Authorize(Roles = "VT_ADMIN")]
public class SuCoController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public SuCoController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH SỰ CỐ & BẢO TRÌ
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

            // Tự động đồng bộ chi phí sửa chữa vào Hóa đơn tháng của phòng (nếu có)
            await DongBoChiPhiSuaVaoHoaDonAsync(suCo.MaPhong, suCo.NgayTao);

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

    // 5. XỬ LÝ CẬP NHẬT SỰ CỐ & TỰ ĐỘNG CỘNG CHI PHÍ VÀO HÓA ĐƠN PHÒNG (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaSuCo,MaPhong,MaNguoiDungBao,MoTa,TrangThai,ChiPhiSua,NgayTao")] SuCo suCo)
    {
        if (id != suCo.MaSuCo) return NotFound();

        ValidateSuCoConstraints(suCo);

        if (ModelState.IsValid)
        {
            suCo.NgayTao ??= DateTime.Now;
            suCo.DaXoa = false;
            _context.Update(suCo);

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

            // TỰ ĐỘNG CẬP NHẬT TỔNG TIỀN HÓA ĐƠN CỦA PHÒNG TRONG THÁNG TƯƠNG ỨNG
            string thongBaoHoaDon = await DongBoChiPhiSuaVaoHoaDonAsync(suCo.MaPhong, suCo.NgayTao);

            TempData["ThongBao"] = $"Đã cập nhật sự cố #{suCo.MaSuCo} sang '{suCo.TrangThai}' (Chi phí: {(suCo.ChiPhiSua ?? 0):N0}đ). {thongBaoHoaDon}";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(suCo.MaPhong, suCo.MaNguoiDungBao);
        return View(suCo);
    }

    // 6. XÓA MỀM & KHÔI PHỤC SỰ CỐ (ĐỒNG BỘ LẠI TỔNG TIỀN HÓA ĐƠN)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var suCo = await _context.SuCos.FindAsync(id);
        if (suCo != null)
        {
            string? maPhong = suCo.MaPhong;
            DateTime? ngayTao = suCo.NgayTao;

            _context.SuCos.Remove(suCo);
            await _context.SaveChangesAsync();

            await DongBoChiPhiSuaVaoHoaDonAsync(maPhong, ngayTao);
            TempData["ThongBao"] = $"Đã ẩn phiếu sự cố #{id} và cập nhật lại hóa đơn liên quan.";
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

            await DongBoChiPhiSuaVaoHoaDonAsync(suCo.MaPhong, suCo.NgayTao);
            TempData["ThongBao"] = $"Đã khôi phục phiếu sự cố #{id} và đồng bộ lại hóa đơn.";
        }
        return RedirectToAction(nameof(Index));
    }

    // HÀM TỰ ĐỘNG ĐỒNG BỘ CHI PHÍ SỬA CHỮA VÀO HÓA ĐƠN CỦA PHÒNG
    private async Task<string> DongBoChiPhiSuaVaoHoaDonAsync(string? maPhong, DateTime? ngayTaoSuCo)
    {
        if (string.IsNullOrWhiteSpace(maPhong)) return string.Empty;

        int thang = (ngayTaoSuCo ?? DateTime.Now).Month;
        int nam = (ngayTaoSuCo ?? DateTime.Now).Year;

        // Tìm hóa đơn của phòng này trong kỳ Tháng/Năm xảy ra sự cố (hoặc hóa đơn mới nhất chưa thanh toán xong)
        var hoaDon = await _context.HoaDons
            .Include(h => h.MaHopDongNavigation)
                .ThenInclude(hd => hd != null ? hd.MaPhongNavigation : null)
            .Include(h => h.ChiTietHoaDons)
            .Include(h => h.ThanhToans)
            .Where(h => h.MaHopDongNavigation != null && h.MaHopDongNavigation.MaPhong == maPhong)
            .OrderByDescending(h => h.Thang == thang && h.Nam == nam ? 1 : 0)
            .ThenByDescending(h => h.Nam)
            .ThenByDescending(h => h.Thang)
            .FirstOrDefaultAsync();

        if (hoaDon == null) return "(Chưa có hóa đơn kỳ này, chi phí sẽ tự động cộng khi xuất hóa đơn).";

        int kyThang = hoaDon.Thang ?? thang;
        int kyNam = hoaDon.Nam ?? nam;

        // Tính tổng chi phí của các sự cố ĐÃ HOÀN THÀNH hoặc ĐANG SỬA có ChiPhiSua > 0 của phòng trong kỳ hóa đơn đó
        decimal tongChiPhiSuCoKyNay = await _context.SuCos
            .Where(s => s.MaPhong == maPhong
                     && s.ChiPhiSua > 0
                     && s.NgayTao.HasValue
                     && s.NgayTao.Value.Month == kyThang
                     && s.NgayTao.Value.Year == kyNam)
            .SumAsync(s => s.ChiPhiSua ?? 0);

        decimal giaPhongCung = hoaDon.MaHopDongNavigation?.MaPhongNavigation?.GiaThueCung ?? 0;
        decimal tongTienDichVu = hoaDon.ChiTietHoaDons.Sum(ct => ct.ThanhTien);

        // Cập nhật lại Tổng tiền Hóa đơn = Tiền phòng cứng + Tiền dịch vụ + Chi phí sửa chữa sự cố
        hoaDon.TongTien = giaPhongCung + tongTienDichVu + tongChiPhiSuCoKyNay;

        // Kiểm tra lại trạng thái thanh toán của hóa đơn sau khi cộng thêm phí sửa chữa
        decimal daTra = hoaDon.ThanhToans.Sum(t => t.SoTienTra);
        if (daTra == 0)
        {
            hoaDon.TrangThai = "Chưa thanh toán";
        }
        else if (daTra < hoaDon.TongTien)
        {
            hoaDon.TrangThai = "Thanh toán một phần";
        }
        else
        {
            hoaDon.TrangThai = "Đã thanh toán";
        }

        _context.HoaDons.Update(hoaDon);
        await _context.SaveChangesAsync();

        return $"Đã tự động cộng {tongChiPhiSuCoKyNay:N0}đ phí sửa chữa vào Hóa đơn #{hoaDon.MaHoaDon} (Tổng mới: {hoaDon.TongTien:N0}đ)!";
    }

    private void ValidateSuCoConstraints(SuCo suCo)
    {
        var validStates = new[] { "Chờ xử lý", "Đang sửa", "Đã hoàn thành" };
        if (string.IsNullOrWhiteSpace(suCo.TrangThai) || !validStates.Contains(suCo.TrangThai))
        {
            ModelState.AddModelError("TrangThai", "Trạng thái sự cố chỉ nhận: 'Chờ xử lý', 'Đang sửa' hoặc 'Đã hoàn thành'.");
        }

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