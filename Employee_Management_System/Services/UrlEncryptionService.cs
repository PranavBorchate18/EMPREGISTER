using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Employee_Management_System.Services
{
    public sealed class UrlEncryptionService
    {
        private readonly IDataProtector _protector;

        public UrlEncryptionService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("EmployeeManagementSystem.UrlProtection.v1");
        }

        // Produces /p/{opaque-token}. Optional query text is encrypted inside the token.
        public string GetProtectedUrl(string controller, string action, string? queryString = null)
        {
            if (!IsSafeSegment(controller) || !IsSafeSegment(action))
                throw new ArgumentException("Invalid controller/action.");

            var payload = "/" + controller + "/" + action;
            if (!string.IsNullOrWhiteSpace(queryString))
                payload += "?" + queryString.TrimStart('?');

            var protectedValue = _protector.Protect(payload);
            var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protectedValue));
            return "/p/" + token;
        }

        public string UnprotectToken(string token)
        {
            var protectedValue = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            return _protector.Unprotect(protectedValue);
        }

        private static bool IsSafeSegment(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }
}
