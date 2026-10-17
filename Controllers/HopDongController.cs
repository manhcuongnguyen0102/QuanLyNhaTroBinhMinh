using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
public class HopDongController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public HopDongController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH HỢP ĐỒNG (ƯU TIÊN "HIỆU LỰC" LÊN ĐẦU, ĐẨY "ĐÃ THANH LÝ" XUỐNG ĐÁY)
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

        // Sắp xếp: Hợp đồng "Hiệu lực" (0) nằm trên, "Đã thanh lý" (1) đẩy xuống dưới cùng
        var danhSach = await query
            .OrderBy(h => h.TrangThai == "Đã thanh lý" ? 1 : 0)
            .ThenByDescending(h => h.NgayBatDau)
            .ToListAsync();

        return View(danhSach);
    }

    // 2. CHI TIẾT HỢP ĐỒNG & DANH SÁCH KHÁCH Ở
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var hopDong = await _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .Include(h => h.MaNguoiDungDaiDienNavigation)
            .Include(h => h.KhachOs)
            .Include(h => h.HoaDons)
            .Include(h => h.DichVuHopDongs)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.MaHopDong == id);

        if (hopDong == null) return NotFound();

        if (hopDong.TrangThai == "Đã thanh lý" && !hopDong.KhachOs.Any())
        {
            hopDong.KhachOs = await _context.KhachOs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(k => k.MaHopDong == id)
                .ToListAsync();
        }

        // Lấy danh sách dịch vụ cố định (LaCoDinh == true, như Wifi...)
        ViewBag.DichVuCoDinh = await _context.DichVus
            .AsNoTracking()
            .Where(d => d.LaCoDinh == true)
            .OrderBy(d => d.MaDichVu)
            .ToListAsync();

        // Truy vấn trực tiếp danh sách Mã dịch vụ mà hợp đồng này đã đăng ký
        var daDangKyList = await _context.DichVuHopDongs
            .AsNoTracking()
            .Where(x => x.MaHopDong == id && x.MaDichVu != null)
            .Select(x => x.MaDichVu!.Trim().ToUpper())
            .ToListAsync();

        ViewBag.MaDichVuDaDangKy = daDangKyList;

        return View(hopDong);
    }

    // 3. LẬP HỢP ĐỒNG MỚI (GET) - Chỉ nạp các phòng đang "Trống"
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
        

        // Chặn tuyệt đối trường hợp 1 phòng có 2 hợp đồng "Hiệu lực" cùng lúc
        if (!string.IsNullOrWhiteSpace(hopDong.MaPhong))
        {
            var hopDongDangChay = await _context.HopDongs
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.MaPhong == hopDong.MaPhong && h.TrangThai == "Hiệu lực");

            if (hopDongDangChay != null)
            {
                ModelState.AddModelError("MaPhong", $"Phòng {hopDong.MaPhong} hiện đang có hợp đồng #{hopDongDangChay.MaHopDong} đang Hiệu lực.");
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
        hopDong.TrangThai = "Hiệu lực";
        if (!string.IsNullOrWhiteSpace(hopDong.MaNguoiDungDaiDien))
        {
            string maTK = hopDong.MaNguoiDungDaiDien.Trim();

            var hdDangDaiDien = await _context.HopDongs
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.MaNguoiDungDaiDien != null
                                       && h.MaNguoiDungDaiDien.Trim() == maTK
                                       && h.TrangThai == "Hiệu lực");

            if (hdDangDaiDien != null)
            {
                ModelState.AddModelError("MaNguoiDungDaiDien",
                    $"Tài khoản '{maTK}' hiện đang đứng tên đại diện cho Hợp đồng #{hdDangDaiDien.MaHopDong} (Phòng {hdDangDaiDien.MaPhong}). Mỗi tài khoản khách thuê chỉ được đại diện cho 1 phòng đang thuê!");
            }
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
            TempData["ThongBao"] = $"Đã lập hợp đồng #{hopDong.MaHopDong} thành công và chuyển phòng {hopDong.MaPhong} sang Đang thuê.";
            return RedirectToAction(nameof(Details), new { id = hopDong.MaHopDong });
        }

        await PopulateDropdownsAsync(hopDong.MaPhong, hopDong.MaNguoiDungDaiDien, onlyAvailableRooms: true);
        return View(hopDong);
    }

    // 5. CHỈNH SỬA HỢP ĐỒNG (GET) - Chặn sửa nếu hợp đồng "Đã thanh lý"
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var hopDong = await _context.HopDongs.FindAsync(id);
        if (hopDong == null) return NotFound();

        // Đóng băng hồ sơ lịch sử: Không cho phép chỉnh sửa hợp đồng Đã thanh lý
        if (hopDong.TrangThai == "Đã thanh lý")
        {
            TempData["Loi"] = $"Hợp đồng #{hopDong.MaHopDong} đã thanh lý (hồ sơ lịch sử đã đóng), không thể chỉnh sửa!";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(hopDong.MaPhong, hopDong.MaNguoiDungDaiDien, onlyAvailableRooms: true);
        return View(hopDong);
    }

    // 6. XỬ LÝ CẬP NHẬT HỢP ĐỒNG (POST) - Không cho đổi TrangThai, giữ cố định "Hiệu lực"
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("MaHopDong,MaPhong,MaNguoiDungDaiDien,NgayBatDau,NgayKetThuc,TienCoc")] HopDong hopDong)
    {
        if (id != hopDong.MaHopDong) return NotFound();
      

        var hopDongGoc = await _context.HopDongs.AsNoTracking().FirstOrDefaultAsync(h => h.MaHopDong == id);
        if (hopDongGoc == null) return NotFound();

        if (hopDongGoc.TrangThai == "Đã thanh lý")
        {
            TempData["Loi"] = $"Hợp đồng #{hopDong.MaHopDong} đã thanh lý, không thể cập nhật!";
            return RedirectToAction(nameof(Index));
        }
        
        if (hopDong.NgayKetThuc <= hopDong.NgayBatDau)
        {
            ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc hợp đồng phải lớn hơn ngày bắt đầu.");
        }

        if (!string.IsNullOrWhiteSpace(hopDong.MaPhong))
        {
            var hopDongTrung = await _context.HopDongs
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.MaPhong == hopDong.MaPhong
                                       && h.MaHopDong != hopDong.MaHopDong
                                       && h.TrangThai == "Hiệu lực");

            if (hopDongTrung != null)
            {
                ModelState.AddModelError("MaPhong", $"Phòng {hopDong.MaPhong} hiện đang thuộc hợp đồng #{hopDongTrung.MaHopDong} (Hiệu lực).");
            }
        }
        string trangThaiKiemTra = string.IsNullOrWhiteSpace(hopDong.TrangThai) ? "Hiệu lực" : hopDong.TrangThai.Trim();

        if (!string.IsNullOrWhiteSpace(hopDong.MaNguoiDungDaiDien) && trangThaiKiemTra == "Hiệu lực")
        {
            string maTK = hopDong.MaNguoiDungDaiDien.Trim();
            string maHDHienTai = hopDong.MaHopDong.Trim();

            var hdDangDaiDien = await _context.HopDongs
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.MaNguoiDungDaiDien != null
                                       && h.MaNguoiDungDaiDien.Trim() == maTK
                                       && h.TrangThai == "Hiệu lực"
                                       && h.MaHopDong.Trim() != maHDHienTai);

            if (hdDangDaiDien != null)
            {
                ModelState.AddModelError("MaNguoiDungDaiDien",
                    $"Tài khoản '{maTK}' hiện đang đứng tên đại diện cho Hợp đồng #{hdDangDaiDien.MaHopDong} (Phòng {hdDangDaiDien.MaPhong}). Không thể gán trùng vào hợp đồng này!");
            }
        }
        if (ModelState.IsValid)
        {
            // Giữ nguyên trạng thái "Hiệu lực" (chỉ có nút Thanh lý mới được đóng hợp đồng)
            hopDong.TrangThai = "Hiệu lực";
            _context.Update(hopDong);

            // Nếu người dùng đổi sang phòng trống khác, trả phòng cũ về "Trống" và chuyển phòng mới sang "Đang thuê"
            if (hopDongGoc.MaPhong != hopDong.MaPhong)
            {
                if (!string.IsNullOrWhiteSpace(hopDongGoc.MaPhong))
                {
                    var phongCu = await _context.PhongTros.FindAsync(hopDongGoc.MaPhong);
                    if (phongCu != null)
                    {
                        phongCu.TrangThai = "Trống";
                        _context.Update(phongCu);
                    }
                }

                if (!string.IsNullOrWhiteSpace(hopDong.MaPhong))
                {
                    var phongMoi = await _context.PhongTros.FindAsync(hopDong.MaPhong);
                    if (phongMoi != null)
                    {
                        phongMoi.TrangThai = "Đang thuê";
                        _context.Update(phongMoi);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật thông tin hợp đồng #{hopDong.MaHopDong}.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(hopDong.MaPhong, hopDong.MaNguoiDungDaiDien, onlyAvailableRooms: true);
        return View(hopDong);
    }

    // 7. NGHIỆP VỤ THANH LÝ HỢP ĐỒNG & TRẢ PHÒNG (KIỂM TRA CÔNG NỢ -> ĐÓNG HĐ -> XÓA MỀM KHÁCH -> TRẢ PHÒNG)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThanhLy(string id)
    {
        var hopDong = await _context.HopDongs
            .Include(h => h.HoaDons)
            .Include(h => h.KhachOs)
            .FirstOrDefaultAsync(h => h.MaHopDong == id);

        if (hopDong == null) return NotFound();

        if (hopDong.TrangThai == "Đã thanh lý")
        {
            TempData["Loi"] = $"Hợp đồng #{hopDong.MaHopDong} đã được thanh lý từ trước.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // BƯỚC 1: KIỂM TRA CÔNG NỢ HÓA ĐƠN CHƯA TẤT TOÁN
        var hoaDonChuaThu = hopDong.HoaDons
            .Where(hd => hd.TrangThai != "Đã thanh toán")
            .ToList();

        if (hoaDonChuaThu.Any())
        {
            TempData["Loi"] = $"CẢNH BÁO CÔNG NỢ: Không thể thanh lý hợp đồng #{hopDong.MaHopDong} vì còn {hoaDonChuaThu.Count} hóa đơn chưa thanh toán hết! Vui lòng thu đủ tiền nợ trước khi làm thủ tục trả phòng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // BƯỚC 2: CHUYỂN TRẠNG THÁI HỢP ĐỒNG SANG "ĐÃ THANH LÝ"
        // (Giữ nguyên danh sách KhachOs trong hợp đồng để lưu vết lịch sử nhân khẩu)
        hopDong.TrangThai = "Đã thanh lý";
        _context.Update(hopDong);

        // BƯỚC 3: GIẢI PHÓNG PHÒNG VỀ TRẠNG THÁI "TRỐNG"
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
        TempData["ThongBao"] = $"Đã thanh lý hợp đồng #{hopDong.MaHopDong}, trả phòng {hopDong.MaPhong} về trạng thái Trống và chuyển danh sách thành viên sang hồ sơ lưu trữ.";
        return RedirectToAction(nameof(Details), new { id = hopDong.MaHopDong });
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
    // 8. BẬT / TẮT DỊCH VỤ CỐ ĐỊNH CHO HỢP ĐỒNG 1-CLICK (DICHVUHOPDONG)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDichVu(string maHopDong, string maDichVu)
    {
        var hopDong = await _context.HopDongs.FindAsync(maHopDong);
        if (hopDong == null || hopDong.TrangThai == "Đã thanh lý")
        {
            return RedirectToAction(nameof(Details), new { id = maHopDong });
        }

        var dichVu = await _context.DichVus.FindAsync(maDichVu);
        string tenHienThi = dichVu?.TenDichVu ?? maDichVu;

        var existingList = await _context.DichVuHopDongs
            .Where(x => x.MaHopDong == maHopDong && x.MaDichVu == maDichVu)
            .ToListAsync();

        if (existingList.Any())
        {
            // Xóa toàn bộ bản ghi trùng (nếu trước đó bạn bấm nhiều lần)
            _context.DichVuHopDongs.RemoveRange(existingList);
            TempData["ThongBao"] = $"Đã hủy đăng ký dịch vụ {tenHienThi} khỏi hợp đồng #{maHopDong}.";
        }
        else
        {
            string maDvHd = $"DVHD{DateTime.Now:ddHHmmss}{Random.Shared.Next(10, 99)}";
            _context.DichVuHopDongs.Add(new DichVuHopDong
            {
                MaDvHd = maDvHd,
                MaHopDong = maHopDong,
                MaDichVu = maDichVu
            });
            TempData["ThongBao"] = $"Đã đăng ký dịch vụ {tenHienThi} vào hợp đồng #{maHopDong} thành công.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = maHopDong });
    }
}