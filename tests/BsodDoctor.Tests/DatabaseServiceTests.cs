using Xunit;
using BsodDoctor.Models;
using BsodDoctor.Services;

namespace BsodDoctor.Tests;

public sealed class DatabaseServiceTests : IDisposable
{
    private readonly string _dbPath;

    public DatabaseServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bsod-test-db-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    private DatabaseService CreateService() => new(_dbPath);

    // -----------------------------------------------------------------------
    //  Initialize
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Initialize_CreatesTablesSuccessfully()
    {
        var svc = CreateService();

        await svc.InitializeAsync();

        // Tables should be queryable without error — spot-check via FindErrorByCode
        var result = await svc.FindErrorByCodeAsync("0x00000050");
        Assert.Null(result); // no seed data was loaded, so nothing found
    }

    [Fact]
    public async Task Initialize_WithSeedData_ImportsErrors()
    {
        var svc = CreateService();
        var seedPath = CreateTempSeedJson("""
            [
                {
                    "ErrorCode": "0x0000001A",
                    "ErrorName": "MEMORY_MANAGEMENT",
                    "Category": "Donanım",
                    "Severity": 5,
                    "Description": "Memory yönetim hatası",
                    "CommonCauses": "Bozuk RAM modülü",
                    "SolutionSteps": "1. RAM testi yapın\\n2. Slotları değiştirin",
                    "KesinCozum": "RAM modüllerini yeniden takın",
                    "RelatedKbUrls": "https://support.microsoft.com/example"
                }
            ]
            """);

        await svc.InitializeAsync(seedPath);

        var error = await svc.FindErrorByCodeAsync("0x0000001A");
        Assert.NotNull(error);
        Assert.Equal("MEMORY_MANAGEMENT", error.ErrorName);
        Assert.Equal("Donanım", error.Category);
        Assert.Equal(5, error.Severity);
        Assert.Contains("RAM testi", error.SolutionSteps);
        Assert.Contains("yeniden takın", error.KesinCozum);

        File.Delete(seedPath);
    }

    [Fact]
    public async Task Initialize_WithSeedData_DoesNotDuplicateOnSecondCall()
    {
        var svc = CreateService();
        var seedPath = CreateTempSeedJson("""
            [
                {
                    "ErrorCode": "0x00000050",
                    "ErrorName": "PAGE_FAULT_IN_NONPAGED_AREA",
                    "Category": "Donanım",
                    "Severity": 4
                }
            ]
            """);

        await svc.InitializeAsync(seedPath);
        await svc.InitializeAsync(seedPath); // second init

        var error = await svc.FindErrorByCodeAsync("0x00000050");
        Assert.NotNull(error);
        Assert.Equal("PAGE_FAULT_IN_NONPAGED_AREA", error.ErrorName);

        File.Delete(seedPath);
    }

    // -----------------------------------------------------------------------
    //  FindErrorByCodeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FindErrorByCodeAsync_KnownCode_ReturnsError()
    {
        var svc = CreateService();
        await SeedSingleError(svc);

        var error = await svc.FindErrorByCodeAsync("0x000000D1");

        Assert.NotNull(error);
        Assert.Equal("DRIVER_IRQL_NOT_LESS_OR_EQUAL", error.ErrorName);
    }

    [Fact]
    public async Task FindErrorByCodeAsync_UnknownCode_ReturnsNull()
    {
        var svc = CreateService();
        await SeedSingleError(svc);

        var error = await svc.FindErrorByCodeAsync("0xDEADBEEF");

        Assert.Null(error);
    }

    // -----------------------------------------------------------------------
    //  IsErrorInCooldownAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task IsErrorInCooldownAsync_NoRecentRecord_ReturnsFalse()
    {
        var svc = CreateService();
        await svc.InitializeAsync();

        var inCooldown = await svc.IsErrorInCooldownAsync("0x00000050", TimeSpan.FromDays(1));

        Assert.False(inCooldown);
    }

    [Fact]
    public async Task IsErrorInCooldownAsync_RecentRecord_ReturnsTrue()
    {
        var svc = CreateService();
        await svc.InitializeAsync();
        await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\test.dmp",
            ErrorCode = "0x00000050",
            ErrorName = "PAGE_FAULT_IN_NONPAGED_AREA"
        });

        var inCooldown = await svc.IsErrorInCooldownAsync("0x00000050", TimeSpan.FromDays(1));

        Assert.True(inCooldown);
    }

    [Fact]
    public async Task IsErrorInCooldownAsync_DifferentCode_NotAffected()
    {
        var svc = CreateService();
        await svc.InitializeAsync();
        await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\test.dmp",
            ErrorCode = "0x00000050",
            ErrorName = "PAGE_FAULT_IN_NONPAGED_AREA"
        });

        var inCooldown = await svc.IsErrorInCooldownAsync("0x0000001A", TimeSpan.FromDays(1));

        Assert.False(inCooldown);
    }

    // -----------------------------------------------------------------------
    //  SaveAnalysisRecordAsync / MarkAsResolvedAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAnalysisRecordAsync_ReturnsPositiveId()
    {
        var svc = CreateService();
        await svc.InitializeAsync();

        var id = await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\test.dmp",
            ErrorCode = "0x00000050",
            ErrorName = "PAGE_FAULT_IN_NONPAGED_AREA"
        });

        Assert.True(id > 0);
    }

    [Fact]
    public async Task MarkAsResolvedAsync_UpdatesRecord()
    {
        var svc = CreateService();
        await svc.InitializeAsync();

        var id = await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\test.dmp",
            ErrorCode = "0x00000050",
            ErrorName = "PAGE_FAULT_IN_NONPAGED_AREA"
        });

        await svc.MarkAsResolvedAsync(id, "Kullanıcı çözdü.");

        // After resolving, the item should no longer appear in unresolved-only history
        var history = await svc.GetHistoryAsync(onlyUnresolved: true);
        Assert.DoesNotContain(history, h => h.Id == id);
    }

    // -----------------------------------------------------------------------
    //  GetHistoryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_ReturnsItems()
    {
        var svc = CreateService();
        await svc.InitializeAsync();

        await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\a.dmp",
            ErrorCode = "0x00000050",
            ErrorName = "PAGE_FAULT"
        });
        await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\b.dmp",
            ErrorCode = "0x0000001A",
            ErrorName = "MEMORY_MANAGEMENT"
        });

        var history = await svc.GetHistoryAsync(onlyUnresolved: false);

        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.ErrorCode == "0x00000050");
        Assert.Contains(history, h => h.ErrorCode == "0x0000001A");
    }

    [Fact]
    public async Task GetHistoryAsync_OnlyUnresolved_FiltersResolved()
    {
        var svc = CreateService();
        await svc.InitializeAsync();

        var id1 = await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\a.dmp",
            ErrorCode = "0x00000050",
            ErrorName = "PAGE_FAULT"
        });
        await svc.SaveAnalysisRecordAsync(new AnalysisResult
        {
            DumpFilePath = @"C:\b.dmp",
            ErrorCode = "0x0000001A",
            ErrorName = "MEMORY_MANAGEMENT"
        });

        await svc.MarkAsResolvedAsync(id1);

        var history = await svc.GetHistoryAsync(onlyUnresolved: true);

        Assert.Single(history);
        Assert.Equal("0x0000001A", history[0].ErrorCode);
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    private async Task SeedSingleError(DatabaseService svc)
    {
        var seedPath = CreateTempSeedJson("""
            [
                {
                    "ErrorCode": "0x000000D1",
                    "ErrorName": "DRIVER_IRQL_NOT_LESS_OR_EQUAL",
                    "Category": "Sürücü",
                    "Severity": 4
                }
            ]
            """);
        await svc.InitializeAsync(seedPath);
        File.Delete(seedPath);
    }

    private static string CreateTempSeedJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bsod-seed-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
