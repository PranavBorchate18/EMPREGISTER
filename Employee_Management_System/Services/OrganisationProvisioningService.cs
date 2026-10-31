using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace Employee_Management_System.Services
{
    /// <summary>
    /// Settings read from the "OrganisationProvisioning" section of appsettings.json.
    /// </summary>
    public sealed class OrganisationProvisioningOptions
    {
        /// <summary>Organisation whose logins may open the Organisation Management page.</summary>
        public int MasterOrganisationCode { get; set; } = 1;

        /// <summary>UserMast.authority values allowed to create organisations (case-insensitive).</summary>
        public string[] AllowedAuthorities { get; set; } = new[] { "ADMIN" };

        /// <summary>
        /// Tables (schema dbo) whose DATA is copied from PayData_STM into a new
        /// organisation database. Listed tables that do not exist are skipped.
        /// Employee / payroll transaction tables must NOT be listed.
        /// </summary>
        public string[] MasterTables { get; set; } = Array.Empty<string>();

        public int CommandTimeoutSeconds { get; set; } = 600;
    }

    public sealed class ProvisioningResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<string> Log { get; } = new();
    }

    /// <summary>
    /// Creates a new organisation: new database, structure copied from PayData_STM,
    /// master data copied, verified, and only then registered in OrganisationDB.
    /// </summary>
    public sealed class OrganisationProvisioningService
    {
        // Only one organisation may be provisioned at a time.
        private static readonly SemaphoreSlim Gate = new(1, 1);

        private static readonly Regex SafeObjectName =
            new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        private static readonly Regex SafeCollation =
            new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        private readonly OrganisationDbContext _organisations;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrganisationProvisioningService> _logger;

        public OrganisationProvisioningOptions Options { get; }

        public OrganisationProvisioningService(
            OrganisationDbContext organisations,
            IConfiguration configuration,
            ILogger<OrganisationProvisioningService> logger)
        {
            _organisations = organisations;
            _configuration = configuration;
            _logger = logger;

            Options =
                configuration
                    .GetSection("OrganisationProvisioning")
                    .Get<OrganisationProvisioningOptions>()
                ?? new OrganisationProvisioningOptions();
        }

        public async Task<ProvisioningResult> CreateOrganisationAsync(
            int code,
            string? name,
            CancellationToken ct = default)
        {
            var result = new ProvisioningResult();
            name = (name ?? "").Trim();

            if (code <= 0)
                return Fail(result, "Organisation Code must be greater than zero.");

            if (name.Length == 0 || name.Length > 200)
                return Fail(result, "Organisation Name is required (maximum 200 characters).");

            if (!await Gate.WaitAsync(0, ct))
                return Fail(result,
                    "Another organisation is currently being created. Please try again shortly.");

            string step = "starting";
            string? createdDatabase = null;

            try
            {
                string baseCs =
                    _configuration.GetConnectionString("DefaultConnection") ?? "";

                string sourceDb = OrganisationDatabaseHelper.MasterDatabaseName;
                string targetDb = OrganisationDatabaseHelper.GetDatabaseName(code);

                string masterCs = OrganisationDatabaseHelper.BuildConnectionString(baseCs, "master");
                string sourceCs = OrganisationDatabaseHelper.BuildConnectionString(baseCs, sourceDb);
                string targetCs = OrganisationDatabaseHelper.BuildConnectionString(baseCs, targetDb);

                // ---- 1. Validate --------------------------------------------------
                step = "validating the organisation code";

                if (await _organisations.OrganisationMasters
                        .AsNoTracking()
                        .AnyAsync(x => x.Code == code, ct))
                {
                    return Fail(result, $"Organisation Code {code} already exists.");
                }

                // ---- 2. Create empty database ------------------------------------
                step = "creating the database";

                await using (var conn = new SqlConnection(masterCs))
                {
                    await conn.OpenAsync(ct);

                    if (!await DatabaseExistsAsync(conn, sourceDb, ct))
                    {
                        return Fail(result,
                            $"The template database {sourceDb} was not found on the server.");
                    }

                    if (await DatabaseExistsAsync(conn, targetDb, ct))
                    {
                        return Fail(result,
                            $"Database {targetDb} already exists. It was left untouched " +
                            "and no organisation was created.");
                    }

                    string collation = await GetCollationAsync(conn, sourceDb, ct);

                    string sql =
                        "CREATE DATABASE " +
                        OrganisationDatabaseHelper.QuoteName(targetDb) +
                        (collation.Length > 0 ? " COLLATE " + collation : "");

                    await ExecuteAsync(conn, sql, ct);
                    createdDatabase = targetDb;
                    result.Log.Add($"Database {targetDb} created.");
                }

                // ---- 3. Structure (tables, types, no FKs/triggers yet) ----------
                step = "copying the table structure";
                var deferred = await Task.Run(
                    () => CopyStructure(sourceCs, targetCs, sourceDb, result.Log), ct);

                // ---- 4. Master data ---------------------------------------------
                step = "copying master data";
                await CopyMasterDataAsync(targetCs, sourceDb, result.Log, ct);

                // ---- 5. Foreign keys, programmability, triggers -----------------
                step = "creating foreign keys, stored procedures and triggers";
                await Task.Run(
                    () => ApplyDeferredObjects(targetCs, deferred, result.Log), ct);

                // ---- 6. Verify ----------------------------------------------------
                step = "verifying the new database";
                await VerifyAsync(targetCs, sourceDb, targetDb, result.Log, ct);

                // ---- 7. Register organisation (last, so a failed build is never listed) ----
                step = "registering the organisation";
                _organisations.OrganisationMasters.Add(new OrganisationMaster
                {
                    Code = code,
                    OrganisationName = name
                });
                await _organisations.SaveChangesAsync(ct);
                result.Log.Add($"Organisation {code} - {name} registered.");

                result.Success = true;
                result.Message =
                    $"Organisation \"{name}\" (Code {code}) was created with database {targetDb}.";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Organisation creation failed for Code {Code} while {Step}.", code, step);

                if (createdDatabase != null)
                {
                    await TryDropDatabaseAsync(createdDatabase, result.Log);
                }

                // Detach anything half-added so the scoped context stays usable.
                foreach (var entry in _organisations.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;

                result.Success = false;
                result.Message =
                    $"The organisation could not be created (failed while {step}). " +
                    "Nothing was registered and the partly created database was removed. " +
                    "See the server log for details.";
                return result;
            }
            finally
            {
                Gate.Release();
            }
        }

        // =====================================================================
        // STRUCTURE
        // =====================================================================

        private sealed class DeferredScripts
        {
            public List<(string Name, StringCollection Sql)> ForeignKeys { get; } = new();
            public List<(string Name, StringCollection Sql)> Programmability { get; } = new();
            public List<(string Name, StringCollection Sql)> Triggers { get; } = new();
        }

        private DeferredScripts CopyStructure(
            string sourceCs,
            string targetCs,
            string sourceDb,
            List<string> log)
        {
            var deferred = new DeferredScripts();

            using var sourceConnection = new SqlConnection(sourceCs);
            var server = new Server(new ServerConnection(sourceConnection));
            Database db = server.Databases[sourceDb]
                ?? throw new InvalidOperationException("Template database not found.");

            using var target = new SqlConnection(targetCs);
            target.Open();

            // Base options shared by everything.
            ScriptingOptions Base() => new ScriptingOptions
            {
                SchemaQualify = true,
                IncludeIfNotExists = false,
                ScriptDrops = false,
                ScriptBatchTerminator = false,   // return plain batches, no "GO"
                NoFileGroup = true,              // everything goes to the new DB's default filegroup
                WithDependencies = false,
                Permissions = false
            };

            // 1. Schemas (other than dbo)
            foreach (Schema schema in db.Schemas)
            {
                if (schema.IsSystemObject || schema.Name == "dbo") continue;
                RunAll(target, schema.Script(Base()), log, "schema " + schema.Name);
            }

            // 2. User-defined data types and table types (tables / procs may use them)
            foreach (UserDefinedDataType t in db.UserDefinedDataTypes)
                RunAll(target, t.Script(Base()), log, "type " + t.Name);

            foreach (UserDefinedTableType t in db.UserDefinedTableTypes)
                RunAll(target, t.Script(Base()), log, "table type " + t.Name);

            // 3. Tables: columns, identity, PK, unique, defaults, checks, indexes.
            //    Foreign keys and triggers are deferred until the data is loaded.
            ScriptingOptions tableOptions = Base();
            tableOptions.DriPrimaryKey = true;
            tableOptions.DriUniqueKeys = true;
            tableOptions.DriDefaults = true;
            tableOptions.DriChecks = true;
            tableOptions.DriClustered = true;
            tableOptions.DriNonClustered = true;
            tableOptions.Indexes = true;
            tableOptions.ClusteredIndexes = true;
            tableOptions.NonClusteredIndexes = true;
            tableOptions.XmlIndexes = true;
            tableOptions.ExtendedProperties = true;
            tableOptions.DriForeignKeys = false;
            tableOptions.Triggers = false;

            int tableCount = 0;
            foreach (Table table in db.Tables)
            {
                if (table.IsSystemObject ||
                    table.Name.Equals("sysdiagrams", StringComparison.OrdinalIgnoreCase))
                    continue;

                RunAll(target, table.Script(tableOptions), log, "table " + table.Name);
                tableCount++;

                // Deferred: foreign keys
                ScriptingOptions fkOptions = Base();
                fkOptions.DriForeignKeys = true;
                foreach (ForeignKey fk in table.ForeignKeys)
                    deferred.ForeignKeys.Add(
                        ($"{table.Name}.{fk.Name}", fk.Script(fkOptions)));

                // Deferred: triggers
                ScriptingOptions trOptions = Base();
                foreach (Trigger tr in table.Triggers)
                {
                    if (tr.IsSystemObject) continue;
                    try { deferred.Triggers.Add(($"{table.Name}.{tr.Name}", tr.Script(trOptions))); }
                    catch (Exception ex)
                    {
                        log.Add($"WARNING: trigger {tr.Name} could not be scripted ({ex.GetType().Name}).");
                    }
                }
            }
            log.Add($"{tableCount} table(s) created.");

            // Deferred: views, functions, stored procedures
            ScriptingOptions codeOptions = Base();
            codeOptions.ExtendedProperties = false;

            foreach (View v in db.Views)
            {
                if (v.IsSystemObject) continue;
                TryScript(deferred, v.Name, () => v.Script(codeOptions), log);
            }
            foreach (UserDefinedFunction f in db.UserDefinedFunctions)
            {
                if (f.IsSystemObject) continue;
                TryScript(deferred, f.Name, () => f.Script(codeOptions), log);
            }
            foreach (StoredProcedure p in db.StoredProcedures)
            {
                if (p.IsSystemObject) continue;
                TryScript(deferred, p.Name, () => p.Script(codeOptions), log);
            }

            return deferred;
        }

        private static void TryScript(
            DeferredScripts deferred,
            string name,
            Func<StringCollection> script,
            List<string> log)
        {
            try
            {
                deferred.Programmability.Add((name, script()));
            }
            catch (Exception ex)
            {
                // Typically WITH ENCRYPTION objects, which cannot be scripted.
                log.Add($"WARNING: {name} could not be scripted ({ex.GetType().Name}).");
            }
        }

        private void ApplyDeferredObjects(
            string targetCs,
            DeferredScripts deferred,
            List<string> log)
        {
            using var target = new SqlConnection(targetCs);
            target.Open();

            // Foreign keys (all data is already in place, so they are validated).
            foreach (var fk in deferred.ForeignKeys)
                RunAll(target, fk.Sql, log, "foreign key " + fk.Name);
            log.Add($"{deferred.ForeignKeys.Count} foreign key(s) created.");

            // Views / functions / procedures can depend on each other. Retry the
            // ones that fail until a full pass makes no progress.
            var pending = new List<(string Name, StringCollection Sql)>(deferred.Programmability);
            Exception? lastError = null;

            for (int pass = 0; pass < 10 && pending.Count > 0; pass++)
            {
                var failed = new List<(string Name, StringCollection Sql)>();

                foreach (var item in pending)
                {
                    try { RunAll(target, item.Sql, null, item.Name); }
                    catch (Exception ex) { lastError = ex; failed.Add(item); }
                }

                if (failed.Count == pending.Count) break; // no progress
                pending = failed;
            }

            if (pending.Count > 0)
            {
                _logger.LogError(lastError,
                    "Objects that could not be created: {Names}",
                    string.Join(", ", pending.Select(p => p.Name)));

                throw new InvalidOperationException(
                    "Could not create: " + string.Join(", ", pending.Select(p => p.Name)));
            }
            log.Add($"{deferred.Programmability.Count} view/function/procedure object(s) created.");

            // Triggers last, so they never fire during the master-data copy.
            foreach (var tr in deferred.Triggers)
                RunAll(target, tr.Sql, log, "trigger " + tr.Name);
            log.Add($"{deferred.Triggers.Count} trigger(s) created.");
        }

        private void RunAll(
            SqlConnection connection,
            StringCollection batches,
            List<string>? log,
            string what)
        {
            foreach (string? batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;

                using var cmd = connection.CreateCommand();
                cmd.CommandText = batch;
                cmd.CommandTimeout = Options.CommandTimeoutSeconds;
                cmd.ExecuteNonQuery();
            }
        }

        // =====================================================================
        // MASTER DATA
        // =====================================================================

        private async Task CopyMasterDataAsync(
            string targetCs,
            string sourceDb,
            List<string> log,
            CancellationToken ct)
        {
            await using var conn = new SqlConnection(targetCs);
            await conn.OpenAsync(ct);

            string src = OrganisationDatabaseHelper.QuoteName(sourceDb);

            foreach (string raw in Options.MasterTables)
            {
                string table = (raw ?? "").Trim();

                if (!SafeObjectName.IsMatch(table))
                {
                    log.Add($"WARNING: master table name '{table}' is not valid and was skipped.");
                    continue;
                }

                string quoted = OrganisationDatabaseHelper.QuoteName(table);
                string targetObject = "[dbo]." + quoted;
                string sourceObject = src + ".[dbo]." + quoted;

                // Columns present in BOTH databases (computed and rowversion excluded).
                var columns = new List<(string Name, bool Identity)>();

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = Options.CommandTimeoutSeconds;
                    cmd.CommandText = $@"
SELECT c.name, c.is_identity
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@Target)
  AND c.is_computed = 0
  AND c.system_type_id <> 189
  AND EXISTS (SELECT 1 FROM {src}.sys.columns s
              WHERE s.object_id = OBJECT_ID(@Source) AND s.name = c.name)
ORDER BY c.column_id;";
                    cmd.Parameters.AddWithValue("@Target", targetObject);
                    cmd.Parameters.AddWithValue("@Source", sourceObject);

                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                        columns.Add((reader.GetString(0), reader.GetBoolean(1)));
                }

                if (columns.Count == 0)
                {
                    log.Add($"Master table {table}: not found in both databases, skipped.");
                    continue;
                }

                string columnList = string.Join(", ",
                    columns.Select(c => OrganisationDatabaseHelper.QuoteName(c.Name)));

                bool hasIdentity = columns.Any(c => c.Identity);

                string sql =
                    (hasIdentity ? $"SET IDENTITY_INSERT {targetObject} ON;\n" : "") +
                    $"INSERT INTO {targetObject} ({columnList})\n" +
                    $"SELECT {columnList} FROM {sourceObject};\n" +
                    "SELECT @@ROWCOUNT;\n" +
                    (hasIdentity ? $"SET IDENTITY_INSERT {targetObject} OFF;" : "");

                await using var insert = conn.CreateCommand();
                insert.CommandTimeout = Options.CommandTimeoutSeconds;
                insert.CommandText = sql;
                object? rows = await insert.ExecuteScalarAsync(ct);

                log.Add($"Master table {table}: {rows} row(s) copied.");
            }
        }

        // =====================================================================
        // VERIFY / HELPERS
        // =====================================================================

        private async Task VerifyAsync(
            string targetCs,
            string sourceDb,
            string targetDb,
            List<string> log,
            CancellationToken ct)
        {
            await using var conn = new SqlConnection(targetCs);
            await conn.OpenAsync(ct); // proves the new database is reachable with the app's connection settings

            string src = OrganisationDatabaseHelper.QuoteName(sourceDb);

            async Task<(int Source, int Target)> Compare(string catalogView, string extraFilter)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
SELECT
  (SELECT COUNT(*) FROM {src}.sys.{catalogView} WHERE is_ms_shipped = 0 {extraFilter}),
  (SELECT COUNT(*) FROM sys.{catalogView} WHERE is_ms_shipped = 0 {extraFilter});";
                await using var r = await cmd.ExecuteReaderAsync(ct);
                await r.ReadAsync(ct);
                return (r.GetInt32(0), r.GetInt32(1));
            }

            var tables = await Compare("tables", "AND name <> 'sysdiagrams'");
            var views = await Compare("views", "");
            var procs = await Compare("procedures", "");

            if (tables.Target != tables.Source ||
                views.Target != views.Source ||
                procs.Target != procs.Source)
            {
                throw new InvalidOperationException(
                    $"Structure mismatch. Tables {tables.Source}/{tables.Target}, " +
                    $"views {views.Source}/{views.Target}, procedures {procs.Source}/{procs.Target}.");
            }

            log.Add(
                $"Verified {targetDb}: {tables.Target} tables, {views.Target} views, " +
                $"{procs.Target} stored procedures (match {sourceDb}).");
        }

        private static async Task<bool> DatabaseExistsAsync(
            SqlConnection master, string name, CancellationToken ct)
        {
            await using var cmd = master.CreateCommand();
            cmd.CommandText = "SELECT DB_ID(@Name);";
            cmd.Parameters.AddWithValue("@Name", name);
            object? value = await cmd.ExecuteScalarAsync(ct);
            return value != null && value != DBNull.Value;
        }

        private static async Task<string> GetCollationAsync(
            SqlConnection master, string database, CancellationToken ct)
        {
            await using var cmd = master.CreateCommand();
            cmd.CommandText =
                "SELECT CAST(DATABASEPROPERTYEX(@Name, 'Collation') AS nvarchar(128));";
            cmd.Parameters.AddWithValue("@Name", database);
            string value = Convert.ToString(await cmd.ExecuteScalarAsync(ct)) ?? "";
            return SafeCollation.IsMatch(value) ? value : "";
        }

        private static async Task ExecuteAsync(
            SqlConnection conn, string sql, CancellationToken ct)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task TryDropDatabaseAsync(string database, List<string> log)
        {
            try
            {
                string baseCs = _configuration.GetConnectionString("DefaultConnection") ?? "";
                string masterCs = OrganisationDatabaseHelper.BuildConnectionString(baseCs, "master");

                SqlConnection.ClearAllPools();

                await using var conn = new SqlConnection(masterCs);
                await conn.OpenAsync();

                string q = OrganisationDatabaseHelper.QuoteName(database);
                await ExecuteAsync(conn,
                    $"ALTER DATABASE {q} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {q};",
                    CancellationToken.None);

                log.Add($"Partly created database {database} was removed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Could not remove partly created database {Database}. Drop it manually.",
                    database);
                log.Add($"WARNING: database {database} could not be removed automatically.");
            }
        }

        private static ProvisioningResult Fail(ProvisioningResult result, string message)
        {
            result.Success = false;
            result.Message = message;
            return result;
        }
    }
}