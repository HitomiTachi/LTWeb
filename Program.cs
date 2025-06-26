using NguyenNhan_2179_tuan3.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using System;

// Đảm bảo bạn đã có using cho ServicesEmailSender
// using NguyenNhan_2179_tuan3.Services; // Nếu bạn để class trong folder Services

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();

// Đăng ký đúng class gửi mail -- chỉ đăng ký 1 lần!
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, ServicesEmailSender>();

// 1.2 Cấu hình Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// 1.3 Cấu hình cookie đăng nhập
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// 1.5 Razor Pages và MVC
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// 1.6 Cấu hình Distributed Cache cho Session
builder.Services.AddDistributedMemoryCache();

// 1.7 Cấu hình Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);  // thời gian timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 1.8 Inject các Repository
builder.Services.AddScoped<IProductRepository, EFProductRepository>();
builder.Services.AddScoped<ICategoryRepository, EFCategoryRepository>();
// Connect VNPay API
builder.Services.AddScoped<IVnPayService, VnPayService>();
// =======================
// 2. Cấu hình Middleware
// =======================
var app = builder.Build();

// 2.1 Middleware xử lý lỗi
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 2.2 Middleware cơ bản
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// 2.4 Định tuyến Razor Pages
app.MapRazorPages();

// 2.5 Định tuyến MVC mặc định và có Area
app.MapControllerRoute(
    name: "Admin",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();