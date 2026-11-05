using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTroBinhMinh.Models;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<QuanLyNhaTroBinhMinhContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// xác thực bằng cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // đưa về login nếu chưa đăng nhập
        options.AccessDeniedPath = "/Account/Login";// sai quyền -> khách thuê vào admin =  route sẽ chặn
        options.ExpireTimeSpan = TimeSpan.FromHours(12);// phiên đăng nhập hiệu lực 12 tiếng
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Bật xác thực người dùng trước khi kiểm tra quyền
app.UseAuthentication();// kiểm tra là ai( đọc cookie ra claims)

app.UseAuthorization();// kiểm tra vai trò(check[authorize])

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();