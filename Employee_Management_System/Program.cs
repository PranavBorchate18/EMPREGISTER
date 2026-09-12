using Employee_Management_System.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// =========================================================
// MVC
// =========================================================

builder.Services.AddControllersWithViews();


// =========================================================
// DATABASE
// =========================================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));


// =========================================================
// SESSION
// =========================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;
});


var app = builder.Build();


// =========================================================
// ERROR HANDLING
// =========================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}


// =========================================================
// MIDDLEWARE
// =========================================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


// =========================================================
// SESSION
// IMPORTANT
// =========================================================

app.UseSession();


app.UseAuthorization();


// =========================================================
// DEFAULT ROUTE
// =========================================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Employee}/{action=Register}/{id?}");


app.Run();