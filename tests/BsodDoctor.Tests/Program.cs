using BsodDoctor.Services;

// Test: Sahte minidump dosyasını MinidumpReader ile oku
var dmpPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "test_minidump.dmp");

Console.WriteLine("=== BSOD Doctor — Unit Test ===");
Console.WriteLine($"Test file: {dmpPath}");
Console.WriteLine();

if (!File.Exists(dmpPath))
{
    Console.WriteLine($"ERROR: Test file not found: {dmpPath}");
    return 1;
}

var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(dmpPath);

if (errorCode == null)
{
    Console.WriteLine($"FAIL: MinidumpReader returned error: {errorMessage}");
    return 1;
}

Console.WriteLine($"Result  : {errorCode}");
Console.WriteLine($"Expected: 0x00000050");
Console.WriteLine();

if (errorCode == "0x00000050")
{
    Console.WriteLine("✓ PASS — MinidumpReader correctly parsed the test dump!");
    return 0;
}
else
{
    Console.WriteLine($"✗ FAIL — Expected 0x00000050 but got {errorCode}");
    return 1;
}
