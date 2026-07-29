using System.IO;
using System.Text.Json;
using BsodDoctor.Models;
using Microsoft.Data.Sqlite;

namespace BsodDoctor.Services;

/// <summary>
/// Yerel SQLite veritabanına erişim sağlayan servis.
/// Tarama, history, seed data ve resolve işlemlerini yönetir.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    /// <summary>
    /// Veritabanını oluşturur (yoksa) ve varsa seed data'yı import eder.
    /// </summary>
    public async Task InitializeAsync(string? seedDataPath = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS bsod_errors (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                error_code TEXT NOT NULL UNIQUE,
                error_name TEXT NOT NULL,
                category TEXT,
                description TEXT,
                solution_steps TEXT,
                kesin_cozum TEXT,
                common_causes TEXT,
                related_kb_urls TEXT,
                severity INTEGER DEFAULT 0,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS analysis_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                dump_file_path TEXT,
                error_code TEXT,
                error_name TEXT,
                resolved INTEGER DEFAULT 0,
                user_feedback TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_bsod_errors_code ON bsod_errors(error_code);
            CREATE INDEX IF NOT EXISTS idx_analysis_history_timestamp ON analysis_history(timestamp);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        // Migration: kesin_cozum sütunu yoksa ekle (eski veritabanları için)
        if (!await ColumnExistsAsync(connection, "bsod_errors", "kesin_cozum", cancellationToken))
        {
            var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE bsod_errors ADD COLUMN kesin_cozum TEXT";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Migration: is_notified sütunu yoksa ekle (eski veritabanları için)
        if (!await ColumnExistsAsync(connection, "analysis_history", "is_notified", cancellationToken))
        {
            var alterCmd2 = connection.CreateCommand();
            alterCmd2.CommandText = "ALTER TABLE analysis_history ADD COLUMN is_notified INTEGER DEFAULT 0";
            await alterCmd2.ExecuteNonQueryAsync(cancellationToken);
        }

        // Seed data import (tablo boşsa)
        if (!string.IsNullOrEmpty(seedDataPath))
        {
            await SeedDataFromJsonAsync(seedDataPath, connection, cancellationToken);
        }
    }

    /// <summary>
    /// Hata koduna göre veritabanında çözüm ara.
    /// </summary>
    public async Task<BsodError?> FindErrorByCodeAsync(string errorCode, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM bsod_errors WHERE error_code = @code LIMIT 1";
        command.Parameters.AddWithValue("@code", errorCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new BsodError
            {
                Id = reader.GetInt32(0),
                ErrorCode = reader.GetString(1),
                ErrorName = reader.GetString(2),
                Category = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                SolutionSteps = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                KesinCozum = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                CommonCauses = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                RelatedKbUrls = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Severity = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                CreatedAt = reader.GetDateTime(10),
                UpdatedAt = reader.GetDateTime(11)
            };
        }

        return null;
    }

    /// <summary>
    /// Aynı hata kodu için belirtilen cooldown süresi içinde kayıt var mı?
    /// </summary>
    public async Task<bool> IsErrorInCooldownAsync(string errorCode, TimeSpan cooldown, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var since = DateTime.UtcNow - cooldown;

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1) FROM analysis_history
            WHERE error_code = @code AND timestamp >= @since
            LIMIT 1
            """;
        command.Parameters.AddWithValue("@code", errorCode);
        command.Parameters.AddWithValue("@since", since.ToString("O"));

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    /// <summary>
    /// Yeni bir analiz kaydını history tablosuna ekler.
    /// </summary>
    public async Task<int> SaveAnalysisRecordAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO analysis_history (dump_file_path, error_code, error_name, resolved, user_feedback)
            VALUES (@path, @code, @name, 0, NULL)
            RETURNING id
            """;
        command.Parameters.AddWithValue("@path", result.DumpFilePath);
        command.Parameters.AddWithValue("@code", result.ErrorCode);
        command.Parameters.AddWithValue("@name", result.ErrorName);

        var resultId = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(resultId);
    }

    /// <summary>
    /// Bir analiz kaydını "çözüldü" olarak işaretler.
    /// </summary>
    public async Task MarkAsResolvedAsync(int historyId, string? feedback = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE analysis_history
            SET resolved = 1,
                user_feedback = @feedback
            WHERE id = @id
            """;
        command.Parameters.AddWithValue("@id", historyId);
        command.Parameters.AddWithValue("@feedback", feedback ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Belirtilen analiz kaydı için bildirim daha önce gönderilmiş mi?
    /// </summary>
    public async Task<bool> IsNotifiedAsync(int historyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT is_notified FROM analysis_history WHERE id = @id LIMIT 1";
        command.Parameters.AddWithValue("@id", historyId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && Convert.ToBoolean(result);
    }

    /// <summary>
    /// Belirtilen analiz kaydını bildirim gönderildi olarak işaretler.
    /// </summary>
    public async Task MarkAsNotifiedAsync(int historyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE analysis_history SET is_notified = 1 WHERE id = @id";
        command.Parameters.AddWithValue("@id", historyId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Seed data JSON'daki hata kayıtlarını veritabanına import eder.
    /// Tablo boşsa doldurur, doluysa atlar.
    /// </summary>
    private async Task SeedDataFromJsonAsync(string jsonPath, SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!File.Exists(jsonPath))
            return;

        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(1) FROM bsod_errors";
        var existingCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        if (existingCount > 0)
            return; // seed data zaten yüklü, atla

        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
        var errors = JsonSerializer.Deserialize<List<JsonSeedError>>(json);
        if (errors == null || errors.Count == 0)
            return;

        var transaction = connection.BeginTransaction();

        try
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO bsod_errors (error_code, error_name, category, description, solution_steps, kesin_cozum, common_causes, related_kb_urls, severity)
                VALUES (@code, @name, @cat, @desc, @solutions, @kesinCozum, @causes, @urls, @sev)
                """;

            var pCode = command.Parameters.Add("@code", SqliteType.Text);
            var pName = command.Parameters.Add("@name", SqliteType.Text);
            var pCat = command.Parameters.Add("@cat", SqliteType.Text);
            var pDesc = command.Parameters.Add("@desc", SqliteType.Text);
            var pSolutions = command.Parameters.Add("@solutions", SqliteType.Text);
            var pKesinCozum = command.Parameters.Add("@kesinCozum", SqliteType.Text);
            var pCauses = command.Parameters.Add("@causes", SqliteType.Text);
            var pUrls = command.Parameters.Add("@urls", SqliteType.Text);
            var pSev = command.Parameters.Add("@sev", SqliteType.Integer);

            foreach (var error in errors)
            {
                pCode.Value = error.ErrorCode;
                pName.Value = error.ErrorName;
                pCat.Value = error.Category ?? (object)DBNull.Value;
                pDesc.Value = error.Description ?? (object)DBNull.Value;
                pSolutions.Value = error.SolutionSteps ?? (object)DBNull.Value;
                pKesinCozum.Value = error.KesinCozum ?? (object)DBNull.Value;
                pCauses.Value = error.CommonCauses ?? (object)DBNull.Value;
                pUrls.Value = error.RelatedKbUrls ?? (object)DBNull.Value;
                pSev.Value = error.Severity;

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Geçmiş analiz kayıtlarını getirir.
    /// <paramref name="onlyUnresolved"/> = true ise sadece çözülmemiş kayıtlar.
    /// </summary>
    public async Task<List<Models.HistoryItem>> GetHistoryAsync(bool onlyUnresolved = true, CancellationToken cancellationToken = default)
    {
        var items = new List<Models.HistoryItem>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var sql = "SELECT id, timestamp, dump_file_path, error_code, error_name, resolved FROM analysis_history";
        if (onlyUnresolved)
            sql += " WHERE resolved = 0 OR resolved IS NULL";
        sql += " ORDER BY timestamp DESC LIMIT 100";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new Models.HistoryItem
            {
                Id = reader.GetInt32(0),
                Timestamp = reader.GetDateTime(1),
                DumpFilePath = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ErrorCode = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                ErrorName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                IsResolved = !reader.IsDBNull(5) && reader.GetBoolean(5),
            });
        }

        return items;
    }

    /// <summary>
    /// Tabloda belirtilen sütunun var olup olmadığını kontrol eder.
    /// </summary>
    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetString(1) == columnName)
                return true;
        }
        return false;
    }

    private sealed class JsonSeedError
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int Severity { get; set; }
        public string? Description { get; set; }
        public string? CommonCauses { get; set; }
        public string? SolutionSteps { get; set; }
        public string? KesinCozum { get; set; }
        public string? RelatedKbUrls { get; set; }
    }
}
