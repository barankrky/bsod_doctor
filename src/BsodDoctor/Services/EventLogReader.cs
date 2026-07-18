using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BsodDoctor.Services;

/// <summary>
/// Windows Event Log'dan BSOD ile ilgili kayıtları okur.
/// </summary>
public class EventLogReader
{
    /// <summary>Son BSOD olaylarını Event Log'dan getirir</summary>
    public List<EventLogEntry> GetBsodEvents(int maxEvents = 20)
    {
        var events = new List<EventLogEntry>();

        try
        {
            using var log = new EventLog("System");

            for (var i = log.Entries.Count - 1; i >= 0 && events.Count < maxEvents; i--)
            {
                var entry = log.Entries[i];

                // BugCheck (BSOD) olayları: Source = "BugCheck" veya "EventLog"
                if (entry.EntryType == EventLogEntryType.Error &&
                    (entry.Source?.Contains("BugCheck", StringComparison.OrdinalIgnoreCase) == true ||
                     entry.Source?.Contains("EventLog", StringComparison.OrdinalIgnoreCase) == true))
                {
                    // Mesajda BSOD kodu kontrolü
                    if (entry.Message?.Contains("bugcheck", StringComparison.OrdinalIgnoreCase) == true ||
                        entry.Message?.Contains("0x", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        events.Add(entry);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EventLog okuma hatası: {ex.Message}");
        }

        return events;
    }

    /// <summary>Event Log girdisinden BSOD hata kodunu çıkarır</summary>
    public static string? ExtractBsodCode(EventLogEntry entry)
    {
        if (entry.Message == null) return null;

        var match = Regex.Match(entry.Message, @"0x[0-9A-Fa-f]{8}");
        return match.Success ? match.Value.ToUpper() : null;
    }

    /// <summary>Event Log girdisinden BSOD hata adını çıkarır</summary>
    public static string? ExtractBsodName(EventLogEntry entry)
    {
        if (entry.Message == null) return null;

        // "The computer has rebooted from a bugcheck. The bugcheck was: 0x0000001A (0x0000000000041792, 0x0000000000000000, 0x0000000000000000, 0x0000000000000000). A dump was saved in: C:\Windows\MEMORY.DMP."
        // gibi mesajlardan hata adını çıkarmak zor olabilir, genelde sadece kod verilir

        var match = Regex.Match(entry.Message, @"bugcheck was:\s*(0x[0-9A-Fa-f]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Son N tane BSOD olayını özet olarak döndürür</summary>
    public List<(DateTime Time, string? Code, string? Message)> GetRecentBsodSummary(int count = 10)
    {
        return GetBsodEvents(count)
            .Select(e => (
                e.TimeGenerated,
                ExtractBsodCode(e),
                e.Message?.Length > 150 ? e.Message[..150] + "..." : e.Message
            ))
            .ToList();
    }
}
