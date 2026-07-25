using System.IO;

namespace BsodDoctor.Services;

/// <summary>
/// BSOD minidump (.dmp) dosyalarını ham binary olarak okuyup
/// içindeki hata kodunu (BugCheckCode) çıkaran parser.
/// ClrMD kullanmaz — doğrudan Windows Minidump formatını okur.
/// </summary>
public static class MinidumpReader
{
    // Minidump format signatures
    private const uint SignatureMdmp = 0x504D444Du;  // "MDMP"
    private const uint SignaturePage = 0x45474150u;  // "PAGE" (PAGEDUMP format)
    
    // Stream type sabitleri
    private const uint ExceptionStream = 6;

    /// <summary>
    /// .dmp dosyasını açar, BugCheckCode'u okur.
    /// Hem klasik MDMP (minidump) hem de PAGEDU64 (full/active dump) formatını destekler.
    /// </summary>
    public static (string? ErrorCode, string? ErrorMessage) ReadBugCheckCode(string dumpFilePath)
    {
        try
        {
            if (!File.Exists(dumpFilePath))
                return (null, $"Dosya bulunamadı: {dumpFilePath}");

            var fileInfo = new FileInfo(dumpFilePath);
            if (fileInfo.Length < 32)
                return (null, "Dosya çok küçük, geçerli bir dump değil.");

            using var stream = new FileStream(dumpFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            // İmzayı oku ve formatı belirle
            var signature = reader.ReadUInt32();

            if (signature == SignaturePage)
            {
                // PAGEDUMP formatı (PAGEDU32 / PAGEDU64)
                return ReadPageDumpBugCheck(reader, fileInfo);
            }
            else if (signature == SignatureMdmp)
            {
                // Klasik MDMP minidump formatı
                return ReadMinidumpBugCheck(reader, fileInfo);
            }
            else
            {
                return (null, $"Geçersiz dump imzası: 0x{signature:X8}");
            }
        }
        catch (EndOfStreamException)
        {
            return (null, "Dosya beklenenden kısa, dump bozuk.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, $"Dosya okunamadı: {ex.Message}");
        }
    }

    /// <summary>
    /// PAGEDUMP (PAGEDU64/PAGEDU32) formatından BugCheckCode okur.
    /// _DMP_HEADER64 / _DMP_HEADER yapısını kullanır.
    /// </summary>
    private static (string? ErrorCode, string? ErrorMessage) ReadPageDumpBugCheck(BinaryReader reader, FileInfo fileInfo)
    {
        // Offset 0x04: ValidDump (4 bytes) - "DU64" veya "DU32"
        var validDump = reader.ReadBytes(4);
        var versionStr = System.Text.Encoding.ASCII.GetString(validDump);
        
        // Offset 0x08: MajorVersion
        /* uint majorVersion = */ reader.ReadUInt32();
        // Offset 0x0C: MinorVersion
        /* uint minorVersion = */ reader.ReadUInt32();
        // Offset 0x10: DirectoryTableBase 
        /* ulong dtb = */ reader.ReadUInt64();
        // Offset 0x18: PfnDataBase
        /* ulong pfn = */ reader.ReadUInt64();
        // Offset 0x20: PsLoadedModuleList
        /* ulong loaded = */ reader.ReadUInt64();
        // Offset 0x28: PsActiveProcessHead
        /* ulong active = */ reader.ReadUInt64();
        // Offset 0x30: MachineImageType
        /* uint imageType = */ reader.ReadUInt32();
        // Offset 0x34: NumberProcessors
        /* uint numCpu = */ reader.ReadUInt32();

        // Offset 0x38: BugCheckCode
        if (fileInfo.Length < 0x3C)
            return (null, "PAGEDUMP başlığı çok kısa.");

        var bugCheckCode = reader.ReadUInt32();
        var formattedCode = $"0x{bugCheckCode:X8}";
        return (formattedCode, null);
    }

    /// <summary>
    /// Klasik MDMP minidump formatından ExceptionStream üzerinden BugCheckCode okur.
    /// </summary>
    private static (string? ErrorCode, string? ErrorMessage) ReadMinidumpBugCheck(BinaryReader reader, FileInfo fileInfo)
    {
        /* uint version = */ reader.ReadUInt32();
        var numberOfStreams = reader.ReadUInt32();
        var streamDirectoryRva = reader.ReadUInt32();
        /* uint checkSum = */ reader.ReadUInt32();
        /* uint reserved = */ reader.ReadUInt32();
        /* uint timeDateStamp = */ reader.ReadUInt32();
        /* ulong flags = */ reader.ReadUInt64();

        if (streamDirectoryRva >= fileInfo.Length)
            return (null, "Stream dizini dosya dışında.");

        reader.BaseStream.Seek(streamDirectoryRva, SeekOrigin.Begin);

        for (uint i = 0; i < numberOfStreams; i++)
        {
            if (reader.BaseStream.Position + 12 > fileInfo.Length)
                break;

            var streamType = reader.ReadUInt32();
            var dataSize = reader.ReadUInt32();
            var rva = reader.ReadUInt32();

            if (streamType != ExceptionStream)
                continue;

            if (rva + 8 + 4 > fileInfo.Length)
                return (null, "ExceptionStream verisi dosya dışında.");

            reader.BaseStream.Seek(rva + 8, SeekOrigin.Begin);
            var bugCheckCode = reader.ReadUInt32();

            var formattedCode = $"0x{bugCheckCode:X8}";
            return (formattedCode, null);
        }

        return (null, "ExceptionStream bulunamadı.");
    }
}
