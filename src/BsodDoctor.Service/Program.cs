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

// Veritabanı servisi — CommonApplicationData altında, WPF ile paylaşılır
// Not: WPF uygulaması da okuma yaparken bu yolu kontrol etmeli
var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "BsodDoctor", "Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "bsod_errors.db");

builder.Services.AddSingleton<IDatabaseService>(new DatabaseService(dbPath));

// Tarama servisi
builder.Services.AddHostedService<DumpScannerService>();

var host = builder.Build();
await host.RunAsync();
