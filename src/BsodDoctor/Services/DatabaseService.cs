using BsodDoctor.Models;
using Microsoft.Data.Sqlite;

namespace BsodDoctor.Services;

/// <summary>
/// Yerel SQLite veritabanına erişim sağlayan servis.
/// </summary>
public class DatabaseService
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
                CommonCauses = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                RelatedKbUrls = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Severity = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10)
            };
        }

        return null;
    }

    /// <summary>
    /// Veritabanını oluşturur (yoksa).
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
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
                user_feedback TEXT,
                FOREIGN KEY (error_code) REFERENCES bsod_errors(error_code)
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
