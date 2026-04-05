using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SaksAppWeb.Data;

public class BusyTimeoutInterceptor : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 5000;

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection sqlite)
        {
            await sqlite.OpenAsync(cancellationToken);
            using var cmd = sqlite.CreateCommand();
            cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs}";
            cmd.ExecuteNonQuery();
            return InterceptionResult.Suppress();
        }

        return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        if (connection is SqliteConnection sqlite)
        {
            sqlite.Open();
            using var cmd = sqlite.CreateCommand();
            cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs}";
            cmd.ExecuteNonQuery();
            return InterceptionResult.Suppress();
        }

        return base.ConnectionOpening(connection, eventData, result);
    }
}
