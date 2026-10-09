using System.Diagnostics;
using System.Security.Cryptography;
using FauFau.Formats;
using Npgsql;
using SDBrowser;

namespace Calldown.DbSetup;

internal static class SdbImport
{
    private const string Step = "sdb";
    private const string Schema = "sdb";

    public static async Task RunAsync(
        NpgsqlDataSource dataSource,
        SetupState state,
        string connStr,
        string sdbPath,
        bool force
    )
    {
        if (!File.Exists(sdbPath))
        {
            throw new FileNotFoundException(
                $"Static database not found at {sdbPath}. Check the firefall-path parameter."
            );
        }

        var fingerprint = await FingerprintAsync(sdbPath);

        if (!force && await state.GetAsync(Step) == fingerprint)
        {
            Log.Info("sdb schema is up to date");
            return;
        }

        Log.Info($"Importing {sdbPath} into schema '{Schema}', this takes a minute");
        var sw = Stopwatch.StartNew();

        // A half-finished import can't be told apart from a complete one, so always start from scratch
        await state.ClearAsync(Step);
        await dataSource.ExecuteAsync($"DROP SCHEMA IF EXISTS {Schema} CASCADE;");

        var sdb = new StaticDB();
        sdb.Read(sdbPath);
        Log.Info($"Read {sdb.Tables.Count} tables (patch {sdb.Patch})");

        var names = SdbNames.LoadDefault();
        var importConnStr = new NpgsqlConnectionStringBuilder(connStr)
        {
            SearchPath = Schema,
        }.ConnectionString;

        var importer = new PgImportExport(
            importConnStr,
            Schema,
            sdb,
            names.GetName,
            (msg, error) =>
            {
                // The importer logs every executed statement, which buries the useful lines
                if (msg.StartsWith("Ran: ", StringComparison.Ordinal))
                {
                    return;
                }

                if (error)
                {
                    Log.Error(msg);
                }
                else
                {
                    Log.Info(msg);
                }
            }
        );

        if (!importer.HasConnection)
        {
            throw new InvalidOperationException("SDB importer could not connect to the database");
        }

        importer.CreateSchema();
        importer.ImportData();

        if (importer.HadErrors)
        {
            throw new InvalidOperationException(
                "SDB import finished with errors, see the log above"
            );
        }

        await state.SetAsync(Step, fingerprint);
        Log.Info($"SDB import done in {sw.Elapsed:mm\\:ss}");
    }

    private static async Task<string> FingerprintAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}
