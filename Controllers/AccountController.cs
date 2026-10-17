using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;

namespace QuanLyNhaTroBinhMinh.Controllers;

public class AccountController : Controller
{
    private readonly QuanLyNhaTroBinhMinhContext _context;

    public AccountController(QuanLyNhaTroBinhMinhContext context)
    {
        _context = context;
    }

    // 1. MÀN HÌNH ĐĂNG NHẬP (GET)
    [HttpGet]
    public IActionResult Login()
    {
        // Nếu đã đăng nhập rồi thì điều hướng thẳng theo vai trò
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (role == "VT_TENANT")
                return RedirectToAction("Index", "KhachThue");

            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    // 2. XỬ LÝ ĐĂNG NHẬP & ĐIỀU HƯỚNG THEO VAI TRÒ (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string tenDangNhap, string matKhau)
    {
        if (string.IsNullOrWhiteSpace(tenDangNhap) || string.IsNullOrWhiteSpace(matKhau))
        {
            ViewBag.Loi = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
            return View();
        }

        var username = tenDangNhap.Trim();
        var user = await _context.NguoiDungs
            .Include(u => u.MaVaiTroNavigation)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenDangNhap == username && u.MatKhau == matKhau);

        if (user == null)
        {
            ViewBag.Loi = "Tên đăng nhập hoặc mật khẩu không chính xác!";
            return View();
        }

        // Chặn tài khoản đã bị Chủ trọ khóa
        if (user.TrangThai == "Khóa")
        {
            ViewBag.Loi = $"Tài khoản '{user.TenDangNhap}' hiện đang bị khóa. Vui lòng liên hệ Chủ trọ để được hỗ trợ!";
            return View();
        }

        string maVaiTro = user.MaVaiTro?.Trim() ?? "VT_TENANT";
        string tenVaiTro = user.MaVaiTroNavigation?.TenVaiTro ?? (maVaiTro == "VT_ADMIN" ? "Chủ trọ" : "Khách thuê");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.MaNguoiDung),
            new Claim(ClaimTypes.Name, user.TenDangNhap),
            new Claim(ClaimTypes.Role, maVaiTro),
            new Claim("TenVaiTro", tenVaiTro)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        // Điều hướng tự động theo vai trò
        if (maVaiTro == "VT_TENANT")
        {
            return RedirectToAction("Index", "KhachThue");
        }

        return RedirectToAction("Index", "Home");
    }

    // 3. ĐĂNG XUẤT KHỎI HỆ THỐNG
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [ActionName("Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // 4. ĐỔI MẬT KHẨU CÁ NHÂN (CHO CẢ KHÁCH THUÊ & CHỦ TRỌ)
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhanMatKhau, string? returnUrl)
    {
        string? maNguoiDung = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(maNguoiDung)) return RedirectToAction(nameof(Login));

        var user = await _context.NguoiDungs.FindAsync(maNguoiDung);
        if (user == null) return RedirectToAction(nameof(Login));

        if (user.MatKhau != matKhauCu)
        {
            TempData["Loi"] = "Mật khẩu hiện tại không chính xác!";
        }
        else if (string.IsNullOrWhiteSpace(matKhauMoi) || matKhauMoi.Length < 4)
        {
            TempData["Loi"] = "Mật khẩu mới phải có ít nhất 4 ký tự.";
        }
        else if (matKhauMoi != xacNhanMatKhau)
        {
            TempData["Loi"] = "Mật khẩu xác nhận không khớp với mật khẩu mới.";
        }
        else
        {
            user.MatKhau = matKhauMoi.Trim();
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đã đổi mật khẩu đăng nhập thành công!";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Nếu là Chủ trọ thì về Home/Index, nếu là Khách thuê thì về KhachThue/Index
        if (User.FindFirstValue(ClaimTypes.Role) == "VT_ADMIN")
            return RedirectToAction("Index", "Home");

        return RedirectToAction("Index", "KhachThue");



    }
}