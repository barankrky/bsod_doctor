using System.IO;
using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump dosyalarını ClrMD (Microsoft.Diagnostics.Runtime) kullanarak analiz eder.
/// </summary>
public class DumpAnalyzer : IDumpAnalyzer
{
    public async Task<AnalysisResult> AnalyzeDumpAsync(string dumpFilePath, CancellationToken cancellationToken = default)
    {
        // TODO: ClrMD ile dump analizi implementasyonu
        // Microsoft.Diagnostics.Runtime.DataTarget.LoadDump() kullanılacak

        return await Task.FromResult(new AnalysisResult
        {
            DumpFilePath = dumpFilePath,
            ErrorCode = "UNKNOWN",
            ErrorName = "Bilinmeyen Hata",
            Description = "Dump analizi henüz implemente edilmedi.",
            AnalysisTime = DateTime.Now
        });
    }

    public Task<IReadOnlyList<string>> FindDumpFilesAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var files = Directory.GetFiles(directoryPath, "*.dmp", SearchOption.AllDirectories);
        return Task.FromResult<IReadOnlyList<string>>(files);
    }
}
