using Xunit;
using BsodDoctor.Services;

namespace BsodDoctor.Tests;

public sealed class MinidumpReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    // -----------------------------------------------------------------------
    //  Happy paths — each dump format
    // -----------------------------------------------------------------------

    [Fact]
    public void ReadBugCheckCode_ValidMinidump_ReturnsErrorCode()
    {
        var dmpPath = Path.Combine(AppContext.BaseDirectory, "TestData", "test_minidump.dmp");
        Assert.True(File.Exists(dmpPath));

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(dmpPath);

        Assert.Equal("0x00000050", errorCode);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ReadBugCheckCode_ValidPageDump64_ReturnsErrorCode()
    {
        var data = CreatePageDump64(bugCheckCode: 0x0000001A);
        var path = WriteTempFile(data);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Equal("0x0000001A", errorCode);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ReadBugCheckCode_ValidPageDump32_ReturnsErrorCode()
    {
        var data = CreatePageDump32(bugCheckCode: 0x00000050);
        var path = WriteTempFile(data);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Equal("0x00000050", errorCode);
        Assert.Null(errorMessage);
    }

    // -----------------------------------------------------------------------
    //  Error paths — file-level problems
    // -----------------------------------------------------------------------

    [Fact]
    public void ReadBugCheckCode_FileNotFound_ReturnsError()
    {
        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(@"X:\nonexistent\file.dmp");

        Assert.Null(errorCode);
        Assert.NotNull(errorMessage);
        Assert.Contains("bulunamadı", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBugCheckCode_EmptyFile_ReturnsError()
    {
        var path = WriteTempFile([]);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.Contains("küçük", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBugCheckCode_WrongSignature_ReturnsError()
    {
        // >= 32 bytes to pass the size check, but wrong magic bytes
        var data = new byte[64];
        data[0] = 0xFF; data[1] = 0xFF; data[2] = 0xFF; data[3] = 0xFF;
        var path = WriteTempFile(data);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.Contains("imza", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    //  Error paths — MDMP minidump edge cases
    // -----------------------------------------------------------------------

    [Fact]
    public void ReadBugCheckCode_MinidumpWithoutExceptionStream_ReturnsError()
    {
        // A valid MDMP header with numberOfStreams = 1 and streamDirRva pointing
        // to an entry that is NOT the ExceptionStream (streamType != 6).
        // Create a file big enough so RVA stays in-bounds.
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        // Header
        w.Write(0x504D444Du);     // "MDMP"
        w.Write(0x0000A7E9u);     // version
        w.Write(1u);              // numberOfStreams = 1
        w.Write(0x30u);           // streamDirectoryRva at offset 0x30 (past the header padding)
        w.Write(0u);              // checkSum
        w.Write(0u);              // reserved
        w.Write(0u);              // timeDateStamp
        w.Write(0uL);             // flags
        // Pad to 0x30
        w.Write(new byte[0x30 - ms.Length]);
        // Stream directory entry: streamType = 999 (not ExceptionStream = 6)
        w.Write(999u);            // streamType
        w.Write(4u);              // dataSize
        w.Write(0x40u);           // rva (pointing to some data)
        // Pad to 0x44+ for valid bounds
        w.Write(new byte[20]);
        var data = ms.ToArray();
        var path = WriteTempFile(data);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.Contains("ExceptionStream", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBugCheckCode_MinidumpWithInvalidStreamRva_ReturnsError()
    {
        // streamDirectoryRva points past EOF → early validation should catch it
        var data = CreateMinidump(streamCount: 1, streamDirRva: 0x10_0000, buildStreamAction: null);
        var path = WriteTempFile(data);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void ReadBugCheckCode_MinidumpWithExceptionDataOutOfBounds_ReturnsError()
    {
        // ExceptionStream found but rva + 8 + 4 > file length
        byte[] BuildStream(BinaryWriter w)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(6u);            // streamType = ExceptionStream
            bw.Write(3u);            // dataSize = 3 (but we need 12 min for rva+8+4)
            bw.Write(0x40u);         // rva = past end of a short file
            return ms.ToArray();
        }

        var streamDir = BuildStream(null!);
        var headerLen = 36;                                 // fixed header
        var totalLen = headerLen + streamDir.Length;        // 36 + 12 = 48
        var data = CreateMinidump(streamCount: 1, streamDirRva: (uint)headerLen,
            buildStreamAction: w => w.Write(streamDir));
        // Truncate to 48 bytes so the stream dir is valid but rva + 12 > file
        data = data[..Math.Min(data.Length, 48)];
        var path = WriteTempFile(data);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.NotNull(errorMessage);
    }

    // -----------------------------------------------------------------------
    //  Error paths — PAGEDUMP truncated headers
    // -----------------------------------------------------------------------

    [Fact]
    public void ReadBugCheckCode_PageDump64TruncatedHeader_ReturnsError()
    {
        var full = CreatePageDump64(bugCheckCode: 0x1A);
        // 0x3C is the expected minimum length; truncate to 0x3A
        var path = WriteTempFile(full[..0x3A]);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.Contains("kısa", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBugCheckCode_PageDump32TruncatedHeader_ReturnsError()
    {
        var full = CreatePageDump32(bugCheckCode: 0x50);
        // 0x2C is the expected minimum length; truncate to 0x2A
        var path = WriteTempFile(full[..0x2A]);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.Contains("kısa", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBugCheckCode_PageDumpCorruptStream_ThrowsEndOfStream()
    {
        // A PAGEDUMP64 file that passes the 32-byte minimum but ends before
        // the header is fully read — triggers EndOfStreamException
        var data = CreatePageDump64(bugCheckCode: 0x1A);
        // Truncate to 0x30: passes 32-byte check, reads PAGE + DU64 header
        // contents, but fails at PsLoadedModuleList (needs 8 bytes at 0x20)
        var path = WriteTempFile(data[..0x20]);

        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(path);

        Assert.Null(errorCode);
        Assert.Contains("beklenenden kısa", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    //  Binary dump builders
    // -----------------------------------------------------------------------

    private string WriteTempFile(byte[] data)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bsod-test-{Guid.NewGuid():N}.dmp");
        File.WriteAllBytes(path, data);
        _tempFiles.Add(path);
        return path;
    }

    private static byte[] CreatePageDump64(uint bugCheckCode)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 0x00: PAGE signature
        writer.Write(0x45474150u);
        // 0x04: validDump = "DU64"
        writer.Write("DU64"u8.ToArray());
        // 0x08: MajorVersion
        writer.Write(0u);
        // 0x0C: MinorVersion
        writer.Write(0u);
        // 0x10: DirectoryTableBase (8 bytes)
        writer.Write(0uL);
        // 0x18: PfnDataBase (8 bytes)
        writer.Write(0uL);
        // 0x20: PsLoadedModuleList (8 bytes)
        writer.Write(0uL);
        // 0x28: PsActiveProcessHead (8 bytes)
        writer.Write(0uL);
        // 0x30: MachineImageType
        writer.Write(0u);
        // 0x34: NumberProcessors
        writer.Write(1u);
        // 0x38: BugCheckCode
        writer.Write(bugCheckCode);

        return ms.ToArray();
    }

    private static byte[] CreatePageDump32(uint bugCheckCode)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 0x00: PAGE signature
        writer.Write(0x45474150u);
        // 0x04: validDump = "DU32"
        writer.Write("DU32"u8.ToArray());
        // 0x08: MajorVersion
        writer.Write(0u);
        // 0x0C: MinorVersion
        writer.Write(0u);
        // 0x10: DirectoryTableBase (4 bytes)
        writer.Write(0u);
        // 0x14: PfnDataBase (4 bytes)
        writer.Write(0u);
        // 0x18: PsLoadedModuleList (4 bytes)
        writer.Write(0u);
        // 0x1C: PsActiveProcessHead (4 bytes)
        writer.Write(0u);
        // 0x20: MachineImageType
        writer.Write(0u);
        // 0x24: NumberProcessors
        writer.Write(1u);
        // 0x28: BugCheckCode
        writer.Write(bugCheckCode);

        return ms.ToArray();
    }

    /// <summary>Build a minimal MDMP dump.</summary>
    private static byte[] CreateMinidump(
        uint streamCount,
        uint streamDirRva,
        Action<BinaryWriter>? buildStreamAction)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Header (36 bytes)
        writer.Write(0x504D444Du);    // "MDMP"
        writer.Write(0x0000A7E9u);    // version
        writer.Write(streamCount);
        writer.Write(streamDirRva);
        writer.Write(0u);             // checkSum
        writer.Write(0u);             // reserved
        writer.Write(0u);             // timeDateStamp
        writer.Write(0uL);            // flags

        // Optional stream directory
        buildStreamAction?.Invoke(writer);

        return ms.ToArray();
    }
}
