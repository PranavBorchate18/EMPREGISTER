using Employee_Management_System.Data;
using Employee_Management_System.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".EmployeeManagement.Session";
});
builder.Services.AddSingleton<ActiveSessionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/Error", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    string? loginId = context.Session.GetString("LoginId");
    string? token = context.Session.GetString("ActiveSessionToken");
    var sessions = context.RequestServices.GetRequiredService<ActiveSessionService>();

    if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(token) ||
        !sessions.IsCurrent(loginId, token))
    {
        app.Logger.LogWarning("Session rejected on {Path}", path);
        context.Session.Clear();
        if (string.Equals(context.Request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
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
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
