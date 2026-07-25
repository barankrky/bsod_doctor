using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BsodDoctor.Services;
using BsodDoctor.Service;

var builder = Host.CreateApplicationBuilder(args);

// Windows Service desteği
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "BsodDoctorService";
});

// Veritabanı servisi (WPF uygulamasıyla aynı DB dosyasını paylaşır)
var baseDir = AppDomain.CurrentDomain.BaseDirectory;
var dataDir = Path.Combine(baseDir, "..", "Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "bsod_errors.db");

builder.Services.AddSingleton<IDatabaseService>(new DatabaseService(dbPath));

// Tarama servisi
builder.Services.AddHostedService<DumpScannerService>();

var host = builder.Build();
await host.RunAsync();
