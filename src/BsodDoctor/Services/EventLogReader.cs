using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Windows Event Viewer'dan BSOD ile ilgili logları okur.
/// </summary>
public class EventLogReader
{
    /// <summary>
    /// Sistem logundan son N adet BSOD kaydını getirir.
    /// </summary>
    public Task<IReadOnlyList<AnalysisResult>> ReadBsodEventsAsync(int maxEvents = 10, CancellationToken cancellationToken = default)
    {
        // TODO: System.Diagnostics.Eventing.EventLogReader ile Event Log okuma
        // Kaynak: "System" log, Event ID: 1001 (BSOD / Windows Error Reporting)

        var results = new List<AnalysisResult>();
        return Task.FromResult<IReadOnlyList<AnalysisResult>>(results);
    }
}
