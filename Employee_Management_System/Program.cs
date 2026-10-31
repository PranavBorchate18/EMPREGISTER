
using Employee_Management_System.Data;
using Employee_Management_System.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Data Protection and URL Protection
builder.Services.AddDataProtection();
builder.Services.AddSingleton<UrlEncryptionService>();

// HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// Organisation Database Connection
builder.Services.AddDbContext<OrganisationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrganisationConnection")));

// Main Application Database - Dynamic Connection
var mainConnectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var httpContext = serviceProvider
        .GetRequiredService<IHttpContextAccessor>()
        .HttpContext;

    var organisationCode =
        httpContext?.Session.GetInt32("OrganisationCode");

    if (!organisationCode.HasValue)
    {
        throw new InvalidOperationException(
            "Please select an organisation before accessing the application database.");
    }

    string databaseName = organisationCode.Value == 1
        ? "PayData_STM"
        : $"PayData_{organisationCode.Value}";

    var connectionBuilder =
        new SqlConnectionStringBuilder(mainConnectionString)
        {
            InitialCatalog = databaseName
        };

    options.UseSqlServer(connectionBuilder.ConnectionString);
});

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".EmployeeManagement.Session";
});

// Active Session Service
builder.Services.AddSingleton<ActiveSessionService>();

var app = builder.Build();

// Error Handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// IMPORTANT: Session must be available before URL Protection.
app.UseSession();

// IMPORTANT: URL Protection must execute before Routing.
// It rewrites /p/{token} into the original MVC URL.
app.UseMiddleware<UrlProtectionMiddleware>();

// Routing
app.UseRouting();

// Organisation and Login Session Validation
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    // Organisation Selection Page
    if (path.StartsWithSegments(
        "/Organisation",
        StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Error Page
    if (path.StartsWithSegments(
        "/Home/Error",
        StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Organisation Selection Required
    var organisationCode =
        context.Session.GetInt32("OrganisationCode");

    if (!organisationCode.HasValue)
    {
        context.Response.Redirect("/Organisation/Index");
        return;
    }

    // Login and Logout Pages
    if (path.StartsWithSegments(
            "/Account/Login",
            StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(
            "/Account/Logout",
            StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Validate Active Login Session
    string? loginId =
        context.Session.GetString("LoginId");

    string? token =
        context.Session.GetString("ActiveSessionToken");

    var sessions = context.RequestServices
        .GetRequiredService<ActiveSessionService>();

    string sessionKey =
        !string.IsNullOrWhiteSpace(loginId)
            ? $"{organisationCode.Value}:{loginId}"
            : "";

    if (string.IsNullOrWhiteSpace(loginId) ||
        string.IsNullOrWhiteSpace(token) ||
        !sessions.IsCurrent(sessionKey, token))
    {
        context.Session.Remove("LoginId");
        context.Session.Remove("UserName");
        context.Session.Remove("MarathiName");
        context.Session.Remove("Authority");
        context.Session.Remove("Access");
        context.Session.Remove("WorkingDate");
        context.Session.Remove("ActiveSessionToken");

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

// Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Organisation}/{action=Index}/{id?}");

app.Run();
