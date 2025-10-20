using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace KuyumculukTakipProgrami.Infrastructure.Util;

public static class SequenceUtil
{
    public static async Task<int> NextIntAsync(DatabaseFacade database, string sequenceName, CancellationToken ct = default)
        => await NextIntAsync(database, sequenceName, null, null, ct);

    public static async Task<int> NextIntAsync(DatabaseFacade database, string sequenceName, string? initTable, string? initColumn, CancellationToken ct = default)
    {
        var conn = database.GetDbConnection();
        var openedHere = false;
        if (conn.State != ConnectionState.Open)
        {
            await database.OpenConnectionAsync(ct);
            openedHere = true;
        }

        // Check if sequence exists
        bool exists;
        await using (var cmdCheck = conn.CreateCommand())
        {
            cmdCheck.CommandText = "SELECT EXISTS(SELECT 1 FROM pg_class WHERE relkind='S' AND relname=@name);";
            var p = cmdCheck.CreateParameter();
            p.ParameterName = "@name";
            p.Value = sequenceName;
            cmdCheck.Parameters.Add(p);
            var o = await cmdCheck.ExecuteScalarAsync(ct);
            exists = o is bool b && b;
        }

        if (!exists)
        {
            await using (var cmdCreate = conn.CreateCommand())
            {
                cmdCreate.CommandText = $"CREATE SEQUENCE IF NOT EXISTS \"{sequenceName}\" AS BIGINT START WITH 1 INCREMENT BY 1;";
                await cmdCreate.ExecuteNonQueryAsync(ct);
            }

            if (!string.IsNullOrWhiteSpace(initTable) && !string.IsNullOrWhiteSpace(initColumn))
            {
                // Initialize sequence to current MAX(table.column)
                await using var cmdInit = conn.CreateCommand();
                cmdInit.CommandText = $"SELECT setval('\"{sequenceName}\"', (SELECT COALESCE(MAX(\"{initColumn}\"),0) FROM \"{initTable}\"));";
                await cmdInit.ExecuteScalarAsync(ct);
            }
        }

        // Get next value
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT nextval('\"{sequenceName}\"');";
            var obj = await cmd.ExecuteScalarAsync(ct);
            var next = Convert.ToInt64(obj);
            if (openedHere)
            {
                await database.CloseConnectionAsync();
            }
            return next >= int.MaxValue ? int.MaxValue : (int)next;
        }
    }
}
