
using Employee_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Employee_Management_System.Services
{
    public sealed class UrlProtectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UrlProtectionMiddleware> _logger;

        public UrlProtectionMiddleware(
            RequestDelegate next,
            ILogger<UrlProtectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            UrlEncryptionService urlEncryptionService)
        {
            var path = context.Request.Path.Value ?? "/";

            // Login, logout, error and static files remain accessible.
            if (IsExcludedPath(path))
            {
                await _next(context);
                return;
            }

            // Handle encrypted URL: /p/{token}
            if (context.Request.Path.StartsWithSegments(
                "/p", out var remaining))
            {
                var token = remaining.Value?.Trim('/');

                if (string.IsNullOrWhiteSpace(token) ||
                    token.Contains('/'))
                {
                    context.Response.StatusCode =
                        StatusCodes.Status404NotFound;
                    return;
                }

                string decoded;

                try
                {
                    decoded = urlEncryptionService.UnprotectToken(token);
                }
                catch (Exception ex) when (
                    ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        "Invalid protected URL token.");

                    context.Response.StatusCode =
                        StatusCodes.Status404NotFound;
                    return;
                }

                // Token format: /Controller/Action?query=value
                var questionMark = decoded.IndexOf('?');

                var routePath = questionMark >= 0
                    ? decoded[..questionMark]
                    : decoded;

                var query = questionMark >= 0
                    ? decoded[questionMark..]
                    : string.Empty;

                var parts = routePath.Trim('/')
                    .Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2 ||
                    !IsSafeSegment(parts[0]) ||
                    !IsSafeSegment(parts[1]))
                {
                    context.Response.StatusCode =
                        StatusCodes.Status404NotFound;
                    return;
                }

                var originalPath = context.Request.Path;
                var originalQuery = context.Request.QueryString;

                try
                {
                    // Rewrite request before MVC routing.
                    context.Request.Path = new PathString(routePath);
                    context.Request.QueryString =
                        new QueryString(query);

                    await _next(context);
                }
                finally
                {
                    context.Request.Path = originalPath;
                    context.Request.QueryString = originalQuery;
                }

                return;
            }

            // Redirect normal MVC URLs to encrypted URLs.
            if (LooksLikeMvcPath(path))
            {
                var segments = path.Trim('/')
                    .Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length is 1 or 2 &&
                    segments.All(IsSafeSegment))
                {
                    var controller = segments[0];

                    var action = segments.Length == 2
                        ? segments[1]
                        : "Index";

                    var query = context.Request.QueryString.HasValue
                        ? context.Request.QueryString.Value
                        : null;

                    var protectedUrl =
                        urlEncryptionService.GetProtectedUrl(
                            controller,
                            action,
                            query);

                    context.Response.Redirect(protectedUrl);
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsExcludedPath(string path)
        {
            return
                path.Equals(
                    "/Account/Login",
                    StringComparison.OrdinalIgnoreCase) ||

                path.Equals(
                    "/Account/Logout",
                    StringComparison.OrdinalIgnoreCase) ||

                path.Equals(
                    "/Home/Error",
                    StringComparison.OrdinalIgnoreCase) ||

                // AJAX POST endpoints used by single-active-tab.js
                // and the login page. A 302 redirect would turn these
                // POST requests into GET requests and break them.
                path.Equals(
                    "/Account/ClaimTab",
                    StringComparison.OrdinalIgnoreCase) ||

                path.Equals(
                    "/Account/CheckTab",
                    StringComparison.OrdinalIgnoreCase) ||

                path.Equals(
                    "/Account/TransferTab",
                    StringComparison.OrdinalIgnoreCase) ||

                path.Equals(
                    "/",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWith(
                    "/css/",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWith(
                    "/js/",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWith(
                    "/images/",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWith(
                    "/lib/",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWith(
                    "/favicon",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWith(
                    "/.well-known/",
                    StringComparison.OrdinalIgnoreCase) ||

                // Static files with extensions.
                (!path.StartsWith(
                    "/p/",
                    StringComparison.OrdinalIgnoreCase)
                 && path.Contains('.'));
        }

        private static bool LooksLikeMvcPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                path == "/")
            {
                return false;
            }

            if (path.StartsWith(
                "/api/",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !path.Contains('.');
        }

        private static bool IsSafeSegment(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.All(c =>
                    char.IsLetterOrDigit(c) ||
                    c == '_' ||
                    c == '-');
        }
    }
}
