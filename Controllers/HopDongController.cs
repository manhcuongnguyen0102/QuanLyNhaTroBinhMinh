using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

public class HopDongController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public HopDongController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH HỢP ĐỒNG (KHỚP RÀNG BUỘC: 'Hiệu lực' & 'Đã thanh lý')
    [HttpGet]
    public async Task<IActionResult> Index(string? trangThai, string? tuKhoa)
    {
        var query = _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .Include(h => h.MaNguoiDungDaiDienNavigation)
            .Include(h => h.KhachOs)
            .AsNoTracking()
            .AsQueryable();

        var homNay = DateOnly.FromDateTime(DateTime.Today);
        var mocSapHetHan = homNay.AddDays(30);

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            if (trangThai == "Sắp hết hạn")
            {
                query = query.Where(h => h.TrangThai == "Hiệu lực"
                                      && h.NgayKetThuc >= homNay
                                      && h.NgayKetThuc <= mocSapHetHan);
            }
            else
            {
                query = query.Where(h => h.TrangThai == trangThai);
            }
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var kw = tuKhoa.Trim();
            query = query.Where(h => h.MaHopDong.Contains(kw)
                                  || (h.MaPhong != null && h.MaPhong.Contains(kw))
                                  || (h.MaPhongNavigation != null && h.MaPhongNavigation.TenPhong.Contains(kw))
                                  || (h.MaNguoiDungDaiDienNavigation != null && h.MaNguoiDungDaiDienNavigation.TenDangNhap.Contains(kw)));
        }

        var allContracts = await _context.HopDongs.AsNoTracking().ToListAsync();
        ViewBag.TongHopDong = allContracts.Count;
        ViewBag.DangHieuLuc = allContracts.Count(h => h.TrangThai == "Hiệu lực");
        ViewBag.SapHetHan = allContracts.Count(h => h.TrangThai == "Hiệu lực"
                                                 && h.NgayKetThuc >= homNay
                                                 && h.NgayKetThuc <= mocSapHetHan);
        ViewBag.DaThanhLy = allContracts.Count(h => h.TrangThai == "Đã thanh lý");

        ViewBag.TrangThaiHienTai = trangThai;
        ViewBag.TuKhoaHienTai = tuKhoa;

        
        var danhSach = await query
            .OrderBy(h => h.TrangThai == "Đã thanh lý" ? 1 : 0)
            .ThenByDescending(h => h.NgayBatDau)
            .ToListAsync();

        return View(danhSach);
        
    }

    // 2. CHI TIẾT HỢP ĐỒNG & DANH SÁCH KHÁCH Ở TRONG PHÒNG
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var hopDong = await _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .Include(h => h.MaNguoiDungDaiDienNavigation)
            .Include(h => h.KhachOs)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.MaHopDong == id);

        if (hopDong == null) return NotFound();

        return View(hopDong);
    }

    // 3. LẬP HỢP ĐỒNG MỚI (GET) - Chỉ hiển thị các phòng đang "Trống"
    [HttpGet]
    public async Task<IActionResult> Create(string? maPhong)
    {
        await PopulateDropdownsAsync(maPhong, null, onlyAvailableRooms: true);

        var model = new HopDong
        {
            MaPhong = maPhong,
            NgayBatDau = DateOnly.FromDateTime(DateTime.Today),
            NgayKetThuc = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
            TienCoc = 0
        };

        return View(model);
    }

    // 4. XỬ LÝ LƯU HỢP ĐỒNG MỚI (POST) - Tự động gán cứng TrangThai = "Hiệu lực"
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaHopDong,MaPhong,MaNguoiDungDaiDien,NgayBatDau,NgayKetThuc,TienCoc")] HopDong hopDong)
    {
        if (!string.IsNullOrWhiteSpace(hopDong.MaHopDong))
        {
            hopDong.MaHopDong = hopDong.MaHopDong.Trim().ToUpper();
            if (await _context.HopDongs.AnyAsync(h => h.MaHopDong == hopDong.MaHopDong))
            {
                ModelState.AddModelError("MaHopDong", "Mã hợp đồng này đã tồn tại trong hệ thống.");
            }
        }

        // CHỐT CHẶN QUAN TRỌNG: Kiểm tra xem phòng này đã có hợp đồng nào đang "Hiệu lực" chưa
        if (!string.IsNullOrWhiteSpace(hopDong.MaPhong))
        {
            var hopDongDangChay = await _context.HopDongs
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.MaPhong == hopDong.MaPhong && h.TrangThai == "Hiệu lực");

            if (hopDongDangChay != null)
            {
                ModelState.AddModelError("MaPhong", $"Phòng {hopDong.MaPhong} hiện đang có hợp đồng #{hopDongDangChay.MaHopDong} đang Hiệu lực. Một phòng không thể có 2 hợp đồng Hiệu lực cùng lúc.");
            }
        }

        if (hopDong.NgayKetThuc <= hopDong.NgayBatDau)
        {
            ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc hợp đồng phải lớn hơn ngày bắt đầu.");
        }

        if (hopDong.TienCoc.HasValue && hopDong.TienCoc.Value < 0)
        {
            ModelState.AddModelError("TienCoc", "Tiền đặt cọc không được nhỏ hơn 0.");
        }

        if (ModelState.IsValid)
        {
            hopDong.TrangThai = "Hiệu lực";
            _context.Add(hopDong);

            if (!string.IsNullOrWhiteSpace(hopDong.MaPhong))
            {
                var phong = await _context.PhongTros.FindAsync(hopDong.MaPhong);
                if (phong != null)
                {
                    phong.TrangThai = "Đang thuê";
                    _context.Update(phong);
                }
            }

            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã lập hợp đồng {hopDong.MaHopDong} thành công và chuyển phòng sang Đang thuê.";
            return RedirectToAction(nameof(Details), new { id = hopDong.MaHopDong });
        }

        await PopulateDropdownsAsync(hopDong.MaPhong, hopDong.MaNguoiDungDaiDien, onlyAvailableRooms: true);
        return View(hopDong);
    }

    // 5. CHỈNH SỬA HỢP ĐỒNG (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var hopDong = await _context.HopDongs.FindAsync(id);
        if (hopDong == null) return NotFound();

        await PopulateDropdownsAsync(hopDong.MaPhong, hopDong.MaNguoiDungDaiDien, onlyAvailableRooms: false);
        return View(hopDong);
    }

    // 6. XỬ LÝ CẬP NHẬT HỢP ĐỒNG (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaHopDong,MaPhong,MaNguoiDungDaiDien,NgayBatDau,NgayKetThuc,TienCoc,TrangThai")] HopDong hopDong)
    {
        if (id != hopDong.MaHopDong) return NotFound();

        if (hopDong.NgayKetThuc <= hopDong.NgayBatDau)
        {
            ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc hợp đồng phải lớn hơn ngày bắt đầu.");
        }

        // CHỐT CHẶN QUAN TRỌNG KHI SỬA: Nếu để trạng thái là "Hiệu lực", kiểm tra xem phòng đó có đang vướng hợp đồng "Hiệu lực" nào khác không
        if (hopDong.TrangThai == "Hiệu lực" && !string.IsNullOrWhiteSpace(hopDong.MaPhong))
        {
            var hopDongTrung = await _context.HopDongs
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.MaPhong == hopDong.MaPhong
                                       && h.MaHopDong != hopDong.MaHopDong
                                       && h.TrangThai == "Hiệu lực");

            if (hopDongTrung != null)
            {
                ModelState.AddModelError("TrangThai", $"Không thể chuyển sang Hiệu lực vì phòng {hopDong.MaPhong} đang có hợp đồng #{hopDongTrung.MaHopDong} đang hoạt động!");
                ModelState.AddModelError("MaPhong", $"Phòng {hopDong.MaPhong} hiện đang thuộc hợp đồng #{hopDongTrung.MaHopDong} (Hiệu lực).");
            }
        }

        if (ModelState.IsValid)
        {
            _context.Update(hopDong);

            // Đồng bộ lại trạng thái phòng một cách an toàn
            if (!string.IsNullOrWhiteSpace(hopDong.MaPhong))
            {
                var phong = await _context.PhongTros.FindAsync(hopDong.MaPhong);
                if (phong != null)
                {
                    if (hopDong.TrangThai == "Hiệu lực")
                    {
                        phong.TrangThai = "Đang thuê";
                    }
                    else
                    {
                        // Nếu đổi sang Đã thanh lý, kiểm tra xem phòng còn hợp đồng Hiệu lực nào khác không trước khi trả về Trống
                        bool conHopDongKhac = await _context.HopDongs
                            .AnyAsync(h => h.MaPhong == hopDong.MaPhong && h.MaHopDong != id && h.TrangThai == "Hiệu lực");
                        if (!conHopDongKhac)
                        {
                            phong.TrangThai = "Trống";
                        }
                    }
                    _context.Update(phong);
                }
            }

            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật hợp đồng {hopDong.MaHopDong}.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(hopDong.MaPhong, hopDong.MaNguoiDungDaiDien, onlyAvailableRooms: false);
        return View(hopDong);
    }

    // 7. THANH LÝ HỢP ĐỒNG 1-CLICK & TRẢ PHÒNG VỀ TRẠNG THÁI "TRỐNG" (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThanhLy(string id)
    {
        var hopDong = await _context.HopDongs.FindAsync(id);
        if (hopDong == null) return NotFound();

        hopDong.TrangThai = "Đã thanh lý";
        _context.Update(hopDong);

        if (!string.IsNullOrWhiteSpace(hopDong.MaPhong))
        {
            bool conHopDongKhac = await _context.HopDongs
                .AnyAsync(h => h.MaPhong == hopDong.MaPhong && h.MaHopDong != id && h.TrangThai == "Hiệu lực");

            if (!conHopDongKhac)
            {
                var phong = await _context.PhongTros.FindAsync(hopDong.MaPhong);
                if (phong != null)
                {
                    phong.TrangThai = "Trống";
                    _context.Update(phong);
                }
            }
        }

        await _context.SaveChangesAsync();
        TempData["ThongBao"] = $"Đã thanh lý hợp đồng {hopDong.MaHopDong} và chuyển trạng thái phòng về Trống.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(string? selectedPhong, string? selectedNguoiDung, bool onlyAvailableRooms)
    {
        var roomsQuery = _context.PhongTros.AsNoTracking().AsQueryable();

        if (onlyAvailableRooms)
        {
            roomsQuery = roomsQuery.Where(p => p.TrangThai == "Trống" || p.MaPhong == selectedPhong);
        }

        var rooms = await roomsQuery
            .OrderBy(p => p.MaPhong)
            .Select(p => new
            {
                p.MaPhong,
                HienThi = $"{p.MaPhong} — {p.TenPhong} ({p.TrangThai} | {p.GiaThueCung:N0}đ/tháng)"
            }).ToListAsync();

        var users = await _context.NguoiDungs
            .AsNoTracking()
            .Where(u => u.TrangThai != "Khóa")
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