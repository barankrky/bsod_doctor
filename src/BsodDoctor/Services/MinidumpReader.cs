using System.IO;
using System.Runtime.InteropServices;

namespace BsodDoctor.Services;

/// <summary>
/// BSOD minidump (.dmp) dosyalarını ham binary olarak okuyup
/// içindeki hata kodunu (BugCheckCode) çıkaran parser.
/// ClrMD kullanmaz — doğrudan Windows Minidump formatını okur.
/// </summary>
public static class MinidumpReader
{
    // Stream type sabitleri
    private const uint ExceptionStream = 6;

    // Minidump signature: "MDMP" little-endian
    private static readonly uint[] MagicVariants =
    [
        0x504D444Du, // "MDMP" (little-endian okuma)
    ];

    /// <summary>
    /// .dmp dosyasını açar, ExceptionStream'den BugCheckCode'u okur.
    /// </summary>
    /// <returns>Hata kodu string'i (örn. "0x0000001A") veya başarısızsa null.</returns>
    public static (string? ErrorCode, string? ErrorMessage) ReadBugCheckCode(string dumpFilePath)
    {
        try
        {
            if (!File.Exists(dumpFilePath))
                return (null, $"Dosya bulunamadı: {dumpFilePath}");

            var fileInfo = new FileInfo(dumpFilePath);
            if (fileInfo.Length < 32)
                return (null, "Dosya çok küçük, geçerli bir minidump değil.");

            using var stream = new FileStream(dumpFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            // 1) MINIDUMP_HEADER (32 bytes)
            var signature = reader.ReadUInt32();
            if (signature != 0x504D444Du) // "MDMP"
                return (null, "Geçerli bir minidump imzası bulunamadı.");

            /* uint version = */ reader.ReadUInt32();
            var numberOfStreams = reader.ReadUInt32();
            var streamDirectoryRva = reader.ReadUInt32();
            /* uint checkSum = */ reader.ReadUInt32();
            /* uint reserved = */ reader.ReadUInt32();
            /* uint timeDateStamp = */ reader.ReadUInt32();
            /* ulong flags = */ reader.ReadUInt64();

            // 2) Stream Directory'ye git
            if (streamDirectoryRva >= fileInfo.Length)
                return (null, "Stream dizini dosya dışında.");

            reader.BaseStream.Seek(streamDirectoryRva, SeekOrigin.Begin);

            for (uint i = 0; i < numberOfStreams; i++)
            {
                // Okunacak kadar byte kalmadıysa çık
                if (reader.BaseStream.Position + 12 > fileInfo.Length)
                    break;

                var streamType = reader.ReadUInt32();
                var dataSize = reader.ReadUInt32();
                var rva = reader.ReadUInt32();

                if (streamType != ExceptionStream)
                    continue;

                // ExceptionStream bulundu — BugCheckCode'u oku
                if (rva + 8 + 4 > fileInfo.Length) // ThreadId(4) + Alignment(4) + ExceptionCode(4)
                    return (null, "ExceptionStream verisi dosya dışında.");

                reader.BaseStream.Seek(rva + 8, SeekOrigin.Begin); // ThreadId + Alignment atla
                var bugCheckCode = reader.ReadUInt32();

                var formattedCode = $"0x{bugCheckCode:X8}";
                return (formattedCode, null);
            }

            return (null, "ExceptionStream bulunamadı.");
        }
        catch (EndOfStreamException)
        {
            return (null, "Dosya beklenenden kısa, minidump bozuk.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, $"Dosya okunamadı: {ex.Message}");
        }
    }
}
