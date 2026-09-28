using Employee_Management_System.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// =====================================================
// MVC
// =====================================================

builder.Services.AddControllersWithViews();


// =====================================================
// DATABASE
// =====================================================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlServer(
            builder.Configuration
                .GetConnectionString(
                    "DefaultConnection"
                )
        );
    }
);


// =====================================================
// SESSION
// =====================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    // Login session timeout
    options.IdleTimeout =
        TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly =
        true;

    options.Cookie.IsEssential =
        true;

    options.Cookie.Name =
        ".EmployeeManagement.Session";
});


var app = builder.Build();


// =====================================================
// ERROR HANDLING
// =====================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Home/Error"
    );

    app.UseHsts();
}


// =====================================================
// MIDDLEWARE
// =====================================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


// IMPORTANT:
// Session must be before Authorization
app.UseSession();

app.UseAuthorization();


// =====================================================
// DEFAULT ROUTE
//
// OLD:
// Employee/Register
//
// NEW:
// Account/Login
//
// Project Run
//      ?
// Login Page
// =====================================================

app.MapControllerRoute(
    name: "default",

    pattern:
        "{controller=Account}/{action=Login}/{id?}"
);


app.Run();