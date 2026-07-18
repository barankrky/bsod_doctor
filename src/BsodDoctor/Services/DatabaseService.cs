using System.Data;
using BsodDoctor.Models;
using Microsoft.Data.Sqlite;

namespace BsodDoctor.Services;

/// <summary>
/// Yerel SQLite veritabanı servisi.
/// Embedded DB dosyasını kullanır, ilk açılışta seed data ile birlikte gelir.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data", "bsod_errors.db"
        );

        // Embedded kaynaktan çıkaramazsak fallback
        if (!File.Exists(dbPath))
        {
            var fallback = Path.Combine(
                AppContext.BaseDirectory,
                "Data", "bsod_errors.db"
            );
            dbPath = fallback;
        }

        _connectionString = $"Data Source={dbPath};Foreign Keys=True;";
    }

    private SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static BsodError MapBsodError(SqliteDataReader reader)
    {
        return new BsodError
        {
            Id = reader.GetInt32(0),
            ErrorCode = reader.GetString(1),
            ErrorName = reader.GetString(2),
            Category = reader.IsDBNull(3) ? null : reader.GetString(3),
            Description = reader.IsDBNull(4) ? null : reader.GetString(4),
            SolutionSteps = reader.IsDBNull(5) ? null : reader.GetString(5),
            CommonCauses = reader.IsDBNull(6) ? null : reader.GetString(6),
            RelatedKbUrls = reader.IsDBNull(7) ? null : reader.GetString(7),
            Severity = reader.GetInt32(8),
            CreatedAt = reader.GetDateTime(9),
            UpdatedAt = reader.GetDateTime(10),
        };
    }

    public async Task<BsodError?> GetErrorByCodeAsync(string errorCode)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM bsod_errors WHERE error_code = @code";
        cmd.Parameters.AddWithValue("@code", errorCode);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return MapBsodError(reader);

        return null;
    }

    public async Task<List<BsodError>> GetAllErrorsAsync()
    {
        var results = new List<BsodError>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM bsod_errors ORDER BY severity DESC, error_code";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapBsodError(reader));

        return results;
    }

    public async Task<List<BsodError>> GetErrorsByCategoryAsync(string category)
    {
        var results = new List<BsodError>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM bsod_errors WHERE category = @cat ORDER BY severity DESC";
        cmd.Parameters.AddWithValue("@cat", category);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapBsodError(reader));

        return results;
    }

    public async Task<List<BsodError>> GetErrorsBySeverityAsync(int minSeverity)
    {
        var results = new List<BsodError>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM bsod_errors WHERE severity >= @min ORDER BY severity DESC";
        cmd.Parameters.AddWithValue("@min", minSeverity);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapBsodError(reader));

        return results;
    }

    public async Task<List<BsodError>> SearchErrorsAsync(string query)
    {
        var results = new List<BsodError>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM bsod_errors 
            WHERE error_code LIKE @q 
               OR error_name LIKE @q 
               OR description LIKE @q
            ORDER BY severity DESC";
        cmd.Parameters.AddWithValue("@q, $q", $"%{query}%");

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapBsodError(reader));

        return results;
    }

    public async Task SaveAnalysisAsync(AnalysisResult result)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO analysis_history (dump_file_path, error_code, error_name, resolved, user_feedback)
            VALUES (@path, @code, @name, @resolved, @feedback)";

        cmd.Parameters.AddWithValue("@path", (object?)result.DumpFilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@code", (object?)result.ErrorCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@name", (object?)result.ErrorName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@resolved", result.Resolved ? 1 : 0);
        cmd.Parameters.AddWithValue("@feedback", (object?)result.UserFeedback ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AnalysisResult>> GetAnalysisHistoryAsync(int limit = 50)
    {
        var results = new List<AnalysisResult>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT h.*, e.description, e.solution_steps, e.common_causes
            FROM analysis_history h
            LEFT JOIN bsod_errors e ON h.error_code = e.error_code
            ORDER BY h.timestamp DESC
            LIMIT @lim";
        cmd.Parameters.AddWithValue("@lim", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var result = new AnalysisResult
            {
                Id = reader.GetInt32(0),
                Timestamp = reader.GetDateTime(1),
                DumpFilePath = reader.IsDBNull(2) ? null : reader.GetString(2),
                ErrorCode = reader.IsDBNull(3) ? null : reader.GetString(3),
                ErrorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                Resolved = reader.GetInt32(5) == 1,
                UserFeedback = reader.IsDBNull(6) ? null : reader.GetString(6),
            };

            // Varsa hata detaylarını da ekle
            if (!reader.IsDBNull(7))
            {
                result.ErrorDetails = new BsodError
                {
                    ErrorCode = result.ErrorCode ?? "",
                    ErrorName = result.ErrorName ?? "",
                    Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                    SolutionSteps = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CommonCauses = reader.IsDBNull(9) ? null : reader.GetString(9),
                };
            }

            results.Add(result);
        }

        return results;
    }

    public async Task InsertErrorAsync(BsodError error)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO bsod_errors (error_code, error_name, category, description, solution_steps, common_causes, related_kb_urls, severity)
            VALUES (@code, @name, @cat, @desc, @steps, @causes, @urls, @sev)";

        cmd.Parameters.AddWithValue("@code", error.ErrorCode);
        cmd.Parameters.AddWithValue("@name", error.ErrorName);
        cmd.Parameters.AddWithValue("@cat", (object?)error.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", (object?)error.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@steps", (object?)error.SolutionSteps ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@causes", (object?)error.CommonCauses ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@urls", (object?)error.RelatedKbUrls ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sev", error.Severity);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateErrorAsync(BsodError error)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE bsod_errors SET
                error_name = @name, category = @cat, description = @desc,
                solution_steps = @steps, common_causes = @causes,
                related_kb_urls = @urls, severity = @sev,
                updated_at = CURRENT_TIMESTAMP
            WHERE error_code = @code";

        cmd.Parameters.AddWithValue("@code", error.ErrorCode);
        cmd.Parameters.AddWithValue("@name", error.ErrorName);
        cmd.Parameters.AddWithValue("@cat", (object?)error.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", (object?)error.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@steps", (object?)error.SolutionSteps ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@causes", (object?)error.CommonCauses ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@urls", (object?)error.RelatedKbUrls ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sev", error.Severity);

        await cmd.ExecuteNonQueryAsync();
    }
}
