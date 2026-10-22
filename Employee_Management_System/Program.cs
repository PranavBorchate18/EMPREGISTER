using Employee_Management_System.Data;
using Employee_Management_System.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Data Protection and URL protection
builder.Services.AddDataProtection();
builder.Services.AddSingleton<UrlEncryptionService>();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".EmployeeManagement.Session";
});

// Login session and active-tab state
builder.Services.AddSingleton<ActiveSessionService>();

var app = builder.Build();

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Rewrite protected URLs before MVC routing.
// Keep this middleware only if UrlProtectionMiddleware
// already exists and is implemented in your project.
app.UseMiddleware<UrlProtectionMiddleware>();

app.UseRouting();
app.UseSession();

// Validate the active login session.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    if (path.StartsWithSegments(
            "/Account/Login",
            StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(
            "/Account/Logout",
            StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(
            "/Home/Error",
            StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    string? loginId = context.Session.GetString("LoginId");
    string? token = context.Session.GetString("ActiveSessionToken");

    var sessions = context.RequestServices
        .GetRequiredService<ActiveSessionService>();

    if (string.IsNullOrWhiteSpace(loginId) ||
        string.IsNullOrWhiteSpace(token) ||
        !sessions.IsCurrent(loginId, token))
    {
        context.Session.Clear();

        if (string.Equals(
            context.Request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Session expired. Please login again."
            });
        }
        else
        {
            context.Response.Redirect("/Account/Login");
        }

        return;
    }

    await next();
});

app.UseAuthorization();

// Default MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();