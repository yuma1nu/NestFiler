using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace FilerApp.Core;

public sealed class StateConflictException() : IOException("別の起動プロセスが設定を変更しました。上書きを防ぐため保存を停止しました。アプリを開き直してください。");

public sealed class StateStore(string databasePath)
{
    private static readonly JsonSerializerOptions jsonOptions = new() { MaxDepth = 256 };
    private readonly SemaphoreSlim gate = new(1, 1);
    private long? revision;
    private string? lastJson;
    public bool RecoveredBackup { get; private set; }
    public Task<SessionState> LoadAsync() => RunAsync(connection =>
    {
        using var command = connection.CreateCommand(); command.CommandText = "SELECT json,revision FROM state WHERE id=1";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) { revision = 0; return new SessionState(); }
        string json = reader.GetString(0); lastJson = json; revision = reader.GetInt64(1); reader.Close();
        SessionState state;
        try { state = Deserialize(json); }
        catch (JsonException)
        {
            using var backup = connection.CreateCommand(); backup.CommandText = "SELECT json FROM state_backup WHERE id=1";
            if (backup.ExecuteScalar() is not string previous) throw;
            state = Deserialize(previous); RecoveredBackup = true;
        }
        FavoriteTree.GetRoots(state); return state;
    });
    private static SessionState Deserialize(string json) => JsonSerializer.Deserialize<SessionState>(json, jsonOptions) ?? throw new JsonException("設定が空です。");
    public Task SaveAsync(SessionState state)
    {
        // Capture on the caller's UI thread, before awaiting the serial writer.
        var json = JsonSerializer.Serialize(state, jsonOptions);
        return RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var read = connection.CreateCommand(); read.Transaction = transaction;
            read.CommandText = "SELECT revision FROM state WHERE id=1";
            long current = read.ExecuteScalar() is long number ? number : 0;
            if (revision is { } expected && expected != current) throw new StateConflictException();
            read.CommandText = "SELECT json FROM state WHERE id=1";
            if (lastJson != null && !string.Equals(read.ExecuteScalar() as string, lastJson, StringComparison.Ordinal)) throw new StateConflictException();
            using var backup = connection.CreateCommand(); backup.Transaction = transaction;
            // Keep the valid backup until the recovered settings are saved successfully.
            backup.CommandText = RecoveredBackup ? "SELECT 1" : "INSERT OR REPLACE INTO state_backup(id,json) SELECT id,json FROM state WHERE id=1";
            backup.ExecuteNonQuery();
            using var write = connection.CreateCommand(); write.Transaction = transaction;
            write.CommandText = "INSERT INTO state(id,json,revision) VALUES(1,$json,$revision) ON CONFLICT(id) DO UPDATE SET json=$json,revision=$revision";
            write.Parameters.AddWithValue("$json", json); write.Parameters.AddWithValue("$revision", current + 1); write.ExecuteNonQuery();
            transaction.Commit(); revision = current + 1; lastJson = json; RecoveredBackup = false; return true;
        });
    }
    private async Task<T> RunAsync<T>(Func<SqliteConnection, T> action)
    {
        await gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(databasePath)!);
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()); connection.Open();
                using var schema = connection.CreateCommand();
                schema.CommandText = "CREATE TABLE IF NOT EXISTS state(id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL, revision INTEGER NOT NULL DEFAULT 0); CREATE TABLE IF NOT EXISTS state_backup(id INTEGER PRIMARY KEY CHECK(id=1),json TEXT NOT NULL)"; schema.ExecuteNonQuery();
                using var columns = connection.CreateCommand(); columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('state') WHERE name='revision'";
                if ((long)columns.ExecuteScalar()! == 0) { using var migrate = connection.CreateCommand(); migrate.CommandText = "ALTER TABLE state ADD COLUMN revision INTEGER NOT NULL DEFAULT 0"; migrate.ExecuteNonQuery(); }
                return action(connection);
            });
        }
        finally { gate.Release(); }
    }
}
