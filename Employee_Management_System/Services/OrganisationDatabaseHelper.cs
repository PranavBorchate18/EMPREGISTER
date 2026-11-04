using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Employee_Management_System.Services
{
    /// <summary>
    /// Single place for organisation code / database name rules.
    ///
    /// The Organisation Code is ALPHANUMERIC (e.g. ABC001). The database an
    /// organisation uses is stored in OrganisationMaster.DatabaseName and is
    /// read on the server - it is never taken from the browser.
    /// </summary>
    public static class OrganisationDatabaseHelper
    {
        /// <summary>Template database that new organisations are copied from.</summary>
        public const string MasterDatabaseName = "PayData_STM";

        private static readonly Regex CodePattern =
            new("^[A-Za-z0-9]{1,20}$", RegexOptions.Compiled);

        private static readonly Regex DatabaseNamePattern =
            new("^[A-Za-z0-9_]{1,100}$", RegexOptions.Compiled);

        /// <summary>Trim + upper-case so "abc001" and "ABC001" are the same code.</summary>
        public static string NormalizeCode(string? code)
        {
            return (code ?? "").Trim().ToUpperInvariant();
        }

        public static bool IsValidCode(string? code)
        {
            return code != null && CodePattern.IsMatch(code);
        }

        public static bool IsValidDatabaseName(string? databaseName)
        {
            return databaseName != null && DatabaseNamePattern.IsMatch(databaseName);
        }

        /// <summary>Database name given to a NEW organisation: PayData_{Code}.</summary>
        public static string GenerateDatabaseName(string organisationCode)
        {
            if (!IsValidCode(organisationCode))
            {
                throw new ArgumentException(
                    "Organisation Code must be 1-20 letters/digits.",
                    nameof(organisationCode));
            }

            return $"PayData_{organisationCode}";
        }

        public static string BuildConnectionString(
            string baseConnectionString,
            string databaseName)
        {
            if (string.IsNullOrWhiteSpace(baseConnectionString))
            {
                throw new InvalidOperationException(
                    "DefaultConnection is not configured in appsettings.json.");
            }

            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = databaseName
            };

            return builder.ConnectionString;
        }

        /// <summary>
        /// Quotes an identifier for use in dynamic T-SQL (same rules as QUOTENAME).
        /// </summary>
        public static string QuoteName(string identifier)
        {
            return "[" + identifier.Replace("]", "]]") + "]";
        }
    }
}