using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanLyNhaTroBinhMinh.Controllers;
[Authorize(Roles = "VT_ADMIN")]
public class HoaDonController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public HoaDonController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. DANH SÁCH HÓA ĐƠN & THEO DÕI CÔNG NỢ (BẢNG SB ADMIN 2 + THANH LỌC PHẲNG)
    [HttpGet]
    public async Task<IActionResult> Index(string? trangThai, int? thang, int? nam, string? tuKhoa)
    {
        var query = _context.HoaDons
            .Include(h => h.MaHopDongNavigation)
                .ThenInclude(hd => hd != null ? hd.MaPhongNavigation : null)
            .Include(h => h.ThanhToans)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(h => h.TrangThai == trangThai);
        }

        if (thang.HasValue && thang >= 1 && thang <= 12)
        {
            query = query.Where(h => h.Thang == thang.Value);
        }

        if (nam.HasValue && nam > 2000)
        {
            query = query.Where(h => h.Nam == nam.Value);
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var kw = tuKhoa.Trim();
            query = query.Where(h => h.MaHoaDon.Contains(kw)
                                  || (h.MaHopDong != null && h.MaHopDong.Contains(kw))
                                  || (h.MaHopDongNavigation != null && h.MaHopDongNavigation.MaPhong != null && h.MaHopDongNavigation.MaPhong.Contains(kw)));
        }

        var allInvoices = await _context.HoaDons.AsNoTracking().ToListAsync();
        ViewBag.TongHoaDon = allInvoices.Count;
        ViewBag.ChuaThanhToan = allInvoices.Count(h => h.TrangThai == "Chưa thanh toán");
        ViewBag.ThanhToanMotPhan = allInvoices.Count(h => h.TrangThai == "Thanh toán một phần");
        ViewBag.DaThanhToan = allInvoices.Count(h => h.TrangThai == "Đã thanh toán");

        ViewBag.TrangThaiHienTai = trangThai;
        ViewBag.ThangHienTai = thang;
        ViewBag.NamHienTai = nam;
        ViewBag.TuKhoaHienTai = tuKhoa;

        // Ưu tiên đưa các hóa đơn còn nợ lên đầu, hóa đơn "Đã thanh toán" xuống dưới
        var danhSach = await query
            .OrderBy(h => h.TrangThai == "Đã thanh toán" ? 1 : 0)
            .ThenByDescending(h => h.Nam)
            .ThenByDescending(h => h.Thang)
            .ThenByDescending(h => h.NgayTao)
            .ToListAsync();

        return View(danhSach);
    }

    // 2. CHI TIẾT HÓA ĐƠN (PHIẾU BÁO TIỀN TRỌ + LỊCH SỬ THU TIỀN TỪNG PHẦN)
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var hoaDon = await _context.HoaDons
            .Include(h => h.MaHopDongNavigation)
                .ThenInclude(hd => hd != null ? hd.MaPhongNavigation : null)
            .Include(h => h.MaHopDongNavigation)
                .ThenInclude(hd => hd != null ? hd.KhachOs : null)
            .Include(h => h.ChiTietHoaDons)
                .ThenInclude(ct => ct.MaDichVuNavigation)
            .Include(h => h.ThanhToans)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.MaHoaDon == id);

        if (hoaDon == null) return NotFound();

        // Truy vấn chỉ số Điện Nước của phòng trong kỳ hóa đơn này để in rõ trên phiếu
        if (hoaDon.MaHopDongNavigation?.MaPhong != null)
        {
            ViewBag.ChiSoKyNay = await _context.ChiSoDienNuocs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MaPhong == hoaDon.MaHopDongNavigation.MaPhong
                                       && c.Thang == hoaDon.Thang
                                       && c.Nam == hoaDon.Nam);
        }
        // Lấy danh sách các sự cố có phát sinh chi phí sửa chữa của phòng trong kỳ hóa đơn này
        if (hoaDon.MaHopDongNavigation?.MaPhong != null)
        {
            string maP = hoaDon.MaHopDongNavigation.MaPhong;
            ViewBag.DanhSachSuCoCoPhi = await _context.SuCos
                .AsNoTracking()
                .Where(s => s.MaPhong == maP
                         && s.ChiPhiSua > 0
                         && s.NgayTao.HasValue
                         && s.NgayTao.Value.Month == hoaDon.Thang
                         && s.NgayTao.Value.Year == hoaDon.Nam)
                .ToListAsync();
        }

        return View(hoaDon);
    }

    // 3. MÀN HÌNH XUẤT HÓA ĐƠN TỰ ĐỘNG (GET)
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateActiveContractsDropdownAsync(null);
        ViewBag.ThangMacDinh = DateTime.Today.Month;
        ViewBag.NamMacDinh = DateTime.Today.Year;
        return View();
    }

    // 4. THUẬT TOÁN TỰ ĐỘNG TÍNH TIỀN & XUẤT HÓA ĐƠN (POST - PHƯƠNG ÁN A)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? maHopDong, int thang, int nam)
    {
        if (thang < 1 || thang > 12 || nam <= 2000)
        {
            TempData["Loi"] = "Tháng (1-12) hoặc Năm (>2000) không hợp lệ.";
            await PopulateActiveContractsDropdownAsync(maHopDong);
            return View();
        }

        // Lấy danh sách hợp đồng cần xuất hóa đơn (1 hợp đồng cụ thể hoặc TẤT CẢ hợp đồng đang "Hiệu lực")
        var queryHopDong = _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .Include(h => h.DichVuHopDongs)
            .Where(h => h.TrangThai == "Hiệu lực");

        if (!string.IsNullOrWhiteSpace(maHopDong) && maHopDong != "ALL")
        {
            queryHopDong = queryHopDong.Where(h => h.MaHopDong == maHopDong);
        }

        var dsHopDong = await queryHopDong.ToListAsync();
        if (!dsHopDong.Any())
        {
            TempData["Loi"] = "Không tìm thấy hợp đồng nào đang Hiệu lực để xuất hóa đơn.";
            await PopulateActiveContractsDropdownAsync(maHopDong);
            return View();
        }

        // Lấy toàn bộ bảng giá dịch vụ hiện hành
        var bangGiaDichVu = await _context.DichVus.AsNoTracking().ToDictionaryAsync(d => d.MaDichVu.Trim().ToUpper());

        int soHoaDonTaoMoi = 0;
        int soHoaDonBoQua = 0;
        var danhSachThieuChiSo = new List<string>();
        string? maHoaDonVuaTao = null;

        foreach (var hd in dsHopDong)
        {
            // Kiểm tra xem hợp đồng này đã được xuất hóa đơn cho kỳ Tháng/Năm này chưa
            bool daCoHoaDon = await _context.HoaDons
                .AnyAsync(x => x.MaHopDong == hd.MaHopDong && x.Thang == thang && x.Nam == nam);

            if (daCoHoaDon)
            {
                soHoaDonBoQua++;
                continue;
            }

            // Kiểm tra xem phòng đã chốt chỉ số điện nước kỳ này chưa
            var chiSo = await _context.ChiSoDienNuocs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MaPhong == hd.MaPhong && c.Thang == thang && c.Nam == nam);

            if (chiSo == null)
            {
                danhSachThieuChiSo.Add($"{hd.MaPhong} (HĐ #{hd.MaHopDong})");
                continue;
            }

            string suffixNgauNhien = $"{Random.Shared.Next(100, 999)}";
            string maHoaDonMoi = $"HD{nam % 100}{thang:D2}_{hd.MaPhong?.Trim()}_{suffixNgauNhien}";
            if (maHoaDonMoi.Length > 18)
            {
                maHoaDonMoi = $"HD{nam % 100}{thang:D2}_{suffixNgauNhien}";
            }

            decimal giaThueCung = hd.MaPhongNavigation?.GiaThueCung ?? 0;
            decimal tongTienDichVu = 0;

            var danhSachChiTiet = new List<ChiTietHoaDon>();

            // A. Tính tiền Điện (DV_DIEN) - Mã chi tiết gọn 13 ký tự (VD: CTD2610_48219)
            int soDien = (chiSo.DienMoi ?? 0) - (chiSo.DienCu ?? 0);
            if (soDien > 0 && bangGiaDichVu.TryGetValue("DV_DIEN", out var dvDien))
            {
                decimal thanhTienDien = soDien * dvDien.DonGia;
                tongTienDichVu += thanhTienDien;

                danhSachChiTiet.Add(new ChiTietHoaDon
                {
                    MaChiTiet = $"CTD{nam % 100}{thang:D2}_{Random.Shared.Next(10000, 99999)}",
                    MaHoaDon = maHoaDonMoi,
                    MaDichVu = dvDien.MaDichVu,
                    SoLuong = soDien,
                    DonGia = dvDien.DonGia,
                    ThanhTien = thanhTienDien,
                    DaXoa = false
                });
            }

            // B. Tính tiền Nước (DV_NUOC) - Mã chi tiết gọn 13 ký tự (VD: CTN2610_51920)
            int soNuoc = (chiSo.NuocMoi ?? 0) - (chiSo.NuocCu ?? 0);
            if (soNuoc > 0 && bangGiaDichVu.TryGetValue("DV_NUOC", out var dvNuoc))
            {
                decimal thanhTienNuoc = soNuoc * dvNuoc.DonGia;
                tongTienDichVu += thanhTienNuoc;

                danhSachChiTiet.Add(new ChiTietHoaDon
                {
                    MaChiTiet = $"CTN{nam % 100}{thang:D2}_{Random.Shared.Next(10000, 99999)}",
                    MaHoaDon = maHoaDonMoi,
                    MaDichVu = dvNuoc.MaDichVu,
                    SoLuong = soNuoc,
                    DonGia = dvNuoc.DonGia,
                    ThanhTien = thanhTienNuoc,
                    DaXoa = false
                });
            }

            // C. Cộng các Dịch vụ cố định đã đăng ký trong DichVuHopDong (VD: DV_WIFI, Vệ sinh...)
            int sttDv = 1;
            foreach (var dvhd in hd.DichVuHopDongs)
            {
                if (string.IsNullOrWhiteSpace(dvhd.MaDichVu)) continue;
                string keyDv = dvhd.MaDichVu.Trim().ToUpper();

                if (keyDv != "DV_DIEN" && keyDv != "DV_NUOC" && bangGiaDichVu.TryGetValue(keyDv, out var dvCoDinh))
                {
                    decimal thanhTienCoDinh = 1 * dvCoDinh.DonGia;
                    tongTienDichVu += thanhTienCoDinh;

                    danhSachChiTiet.Add(new ChiTietHoaDon
                    {
                        MaChiTiet = $"CTV{sttDv++}{nam % 100}{thang:D2}_{Random.Shared.Next(1000, 9999)}",
                        MaHoaDon = maHoaDonMoi,
                        MaDichVu = dvCoDinh.MaDichVu,
                        SoLuong = 1,
                        DonGia = dvCoDinh.DonGia,
                        ThanhTien = thanhTienCoDinh,
                        DaXoa = false
                    });
                }
            }

            // D. Khởi tạo Hóa đơn tổng (TongTien = GiaThueCung + TongTienDichVu)
            var hoaDonMoi = new HoaDon
            {
                MaHoaDon = maHoaDonMoi,
                MaHopDong = hd.MaHopDong,
                Thang = thang,
                Nam = nam,
                TongTien = giaThueCung + tongTienDichVu,
                TrangThai = "Chưa thanh toán",
                NgayTao = DateTime.Now,
                DaXoa = false
            };


            _context.HoaDons.Add(hoaDonMoi);
            if (danhSachChiTiet.Any())
            {
                _context.ChiTietHoaDons.AddRange(danhSachChiTiet);
            }

            soHoaDonTaoMoi++;
            maHoaDonVuaTao = maHoaDonMoi;
        }

        await _context.SaveChangesAsync();

        if (danhSachThieuChiSo.Any())
        {
            TempData["Loi"] = $"CHƯA CHỐT ĐIỆN NƯỚC: Các phòng [{string.Join(", ", danhSachThieuChiSo)}] chưa có chỉ số điện nước kỳ {thang:D2}/{nam}. Vui lòng chốt điện nước trước khi xuất hóa đơn!";
        }

        if (soHoaDonTaoMoi > 0)
        {
            TempData["ThongBao"] = $"Đã xuất tự động thành công {soHoaDonTaoMoi} hóa đơn kỳ {thang:D2}/{nam} (Bỏ qua {soHoaDonBoQua} hợp đồng đã có hóa đơn).";
            if (soHoaDonTaoMoi == 1 && maHoaDonVuaTao != null)
            {
                return RedirectToAction(nameof(Details), new { id = maHoaDonVuaTao });
            }
        }
        else if (!danhSachThieuChiSo.Any())
        {
            TempData["Loi"] = $"Tất cả hợp đồng được chọn đều đã được xuất hóa đơn kỳ {thang:D2}/{nam} từ trước.";
        }

        return RedirectToAction(nameof(Index));
    }

    // 5. GHI NHẬN THU TIỀN TỪNG PHẦN / TOÀN BỘ & TỰ ĐỘNG CẬP NHẬT TRẠNG THÁI NỢ (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhiNhanThanhToan(string maHoaDon, decimal soTienTra, string phuongThuc)
    {
        var hoaDon = await _context.HoaDons
            .Include(h => h.ThanhToans)
            .FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);

        if (hoaDon == null) return NotFound();

        // Khớp CK__ThanhToan__SoTie__02FC7413: [SoTienTra] > (0)
        if (soTienTra <= 0)
        {
            TempData["Loi"] = "Số tiền thu bắt buộc phải lớn hơn 0đ.";
            return RedirectToAction(nameof(Details), new { id = maHoaDon });
        }

        // Khớp CK__ThanhToan__Phuon__04E4BC85: 'Tiền mặt' hoặc 'Chuyển khoản'
        if (phuongThuc != "Tiền mặt" && phuongThuc != "Chuyển khoản")
        {
            phuongThuc = "Chuyển khoản";
        }

        decimal daThuTruocDo = hoaDon.ThanhToans.Sum(t => t.SoTienTra);
        decimal tongTienHoaDon = hoaDon.TongTien ?? 0;
        decimal conNo = tongTienHoaDon - daThuTruocDo;

        if (soTienTra > conNo)
        {
            TempData["Loi"] = $"Số tiền nhập vào ({soTienTra:N0}đ) vượt quá số tiền còn nợ của hóa đơn ({conNo:N0}đ)!";
            return RedirectToAction(nameof(Details), new { id = maHoaDon });
        }

        string maThanhToan = $"TT{DateTime.Now:ddHHmmss}_{Random.Shared.Next(10, 99)}";
        var giaoDichMoi = new ThanhToan
        {
            MaThanhToan = maThanhToan,
            MaHoaDon = maHoaDon,
            SoTienTra = soTienTra,
            NgayTra = DateTime.Now,
            PhuongThuc = phuongThuc,
            DaXoa = false
        };

        _context.ThanhToans.Add(giaoDichMoi);

        // Tự động cập nhật trạng thái hóa đơn theo tổng tiền đã thu
        decimal tongDaThuMoi = daThuTruocDo + soTienTra;
        if (tongDaThuMoi >= tongTienHoaDon)
        {
            hoaDon.TrangThai = "Đã thanh toán";
        }
        else if (tongDaThuMoi > 0)
        {
            hoaDon.TrangThai = "Thanh toán một phần";
        }
        else
        {
            hoaDon.TrangThai = "Chưa thanh toán";
        }

        _context.HoaDons.Update(hoaDon);
        await _context.SaveChangesAsync();

        TempData["ThongBao"] = $"Đã ghi nhận thu {soTienTra:N0}đ ({phuongThuc}). Trạng thái hóa đơn hiện tại: {hoaDon.TrangThai}.";
        return RedirectToAction(nameof(Details), new { id = maHoaDon });
    }
    // CHỦ TRỌ GHI NHẬN KHÁCH ĐÓNG TIỀN MẶT TRỰC TIẾP & TRỪ VÀO CÔNG NỢ HÓA ĐƠN
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThuTienMat(string maHoaDon, decimal soTienThu)
    {
        if (string.IsNullOrWhiteSpace(maHoaDon)) return NotFound();

        var hoaDon = await _context.HoaDons
            .Include(h => h.ThanhToans)
            .FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);

        if (hoaDon == null) return NotFound();

        if (soTienThu <= 0)
        {
            TempData["Loi"] = "Số tiền mặt thu vào phải lớn hơn 0đ.";
            return RedirectToAction(nameof(Details), new { id = maHoaDon });
        }

        decimal tongHoaDon = hoaDon.TongTien ?? 0;
        decimal daTraTruocDo = hoaDon.ThanhToans.Sum(t => t.SoTienTra);
        decimal conNo = Math.Max(0, tongHoaDon - daTraTruocDo);

        if (soTienThu > conNo)
        {
            TempData["Loi"] = $"Số tiền mặt nhập vào ({soTienThu:N0}đ) vượt quá số tiền còn nợ của hóa đơn ({conNo:N0}đ)!";
            return RedirectToAction(nameof(Details), new { id = maHoaDon });
        }

        // Tạo bản ghi ThanhToan bằng Tiền mặt (Mã <= 15 ký tự)
        string maThanhToan = $"TM{DateTime.Now:ddHHmmss}_{Random.Shared.Next(10, 99)}";
        var dotThuMoi = new ThanhToan
        {
            MaThanhToan = maThanhToan,
            MaHoaDon = maHoaDon,
            SoTienTra = soTienThu,
            NgayTra = DateTime.Now,
            PhuongThuc = "Tiền mặt",
            DaXoa = false
        };

        _context.ThanhToans.Add(dotThuMoi);

        // Tự động trừ công nợ và cập nhật trạng thái Hóa đơn
        decimal tongDaThuMoi = daTraTruocDo + soTienThu;
        decimal conNoMoi = Math.Max(0, tongHoaDon - tongDaThuMoi);

        if (conNoMoi == 0 && tongHoaDon > 0)
        {
            hoaDon.TrangThai = "Đã thanh toán";
        }
        else
        {
            hoaDon.TrangThai = "Thanh toán một phần";
        }

        _context.HoaDons.Update(hoaDon);
        await _context.SaveChangesAsync();

        TempData["ThongBao"] = $"Đã thu {soTienThu:N0}đ tiền mặt (Mã GD #{maThanhToan}). Số tiền còn nợ lại của hóa đơn: {conNoMoi:N0}đ.";
        return RedirectToAction(nameof(Details), new { id = maHoaDon });
    }
    // 6. XÓA MỀM HÓA ĐƠN (CHỈ CHO XÓA KHI CHƯA PHÁT SINH GIAO DỊCH THU TIỀN)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var hoaDon = await _context.HoaDons
            .Include(h => h.ThanhToans)
            .Include(h => h.ChiTietHoaDons)
            .FirstOrDefaultAsync(h => h.MaHoaDon == id);

        if (hoaDon == null) return NotFound();

        if (hoaDon.ThanhToans.Any())
        {
            TempData["Loi"] = $"Không thể xóa hóa đơn #{hoaDon.MaHoaDon} vì đã phát sinh lịch sử thu tiền!";
            return RedirectToAction(nameof(Index));
        }

        _context.ChiTietHoaDons.RemoveRange(hoaDon.ChiTietHoaDons);
        _context.HoaDons.Remove(hoaDon);
        await _context.SaveChangesAsync();

        TempData["ThongBao"] = $"Đã xóa mềm hóa đơn #{hoaDon.MaHoaDon}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateActiveContractsDropdownAsync(string? selectedHopDong)
    {
        var contracts = await _context.HopDongs
            .Include(h => h.MaPhongNavigation)
            .AsNoTracking()
            .Where(h => h.TrangThai == "Hiệu lực")
            .OrderBy(h => h.MaPhong)
            .Select(h => new
            {
                h.MaHopDong,
                HienThi = $"Phòng {h.MaPhong} ({h.MaPhongNavigation!.TenPhong}) — Hợp đồng #{h.MaHopDong} — Giá phòng: {h.MaPhongNavigation.GiaThueCung:N0}đ"
            })
            .ToListAsync();

        ViewBag.DanhSachHopDong = new SelectList(contracts, "MaHopDong", "HienThi", selectedHopDong);
    }
}