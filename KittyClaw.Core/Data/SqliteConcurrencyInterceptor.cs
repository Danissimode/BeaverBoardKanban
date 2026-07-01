using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KittyClaw.Core.Data;

/// <summary>
/// Interceptor that applies SQLite concurrency pragmas on every new connection:
/// - busy_timeout = 5000 ms (retry instead of immediate SQLITE_BUSY)
/// - journal_mode = WAL (readers do not block writers)
/// </summary>
public sealed class SqliteConcurrencyInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is SqliteConnection sqlite)
        {
            using var cmd = sqlite.CreateCommand();
            cmd.CommandText = "PRAGMA busy_timeout = 5000;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
        }
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection sqlite)
        {
            using var cmd = sqlite.CreateCommand();
            cmd.CommandText = "PRAGMA busy_timeout = 5000;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
