# BSOD Doctor 🩺

Windows mavi ekran (BSOD) hatalarını analiz eden, açıklayan ve çözüm önerileri sunan bir WPF masaüstü uygulaması. Türkçe, anlaşılır adımlarla kullanıcıya rehberlik eder.

## Özellikler

- **Otomatik tarama** — Uygulama açıldığında `C:\Windows\Minidump\` dizinini tarar
- **Ham binary parser** — Minidump dosyalarını doğrudan binary olarak okur, ClrMD gerektirmez
- **51 BSOD hata kodu** — En yaygın mavi ekran hataları için hazır açıklama ve çözüm adımları
- **Cooldown sistemi** — Aynı hata 24 saat içinde tekrar bildirilmez
- **Severity (ciddiyet) bildirimi** — Her hata 1-5 arası puanla işaretlenir
- **Çözüm adımları + KB linkleri** — Adım adım çözüm ve Microsoft dokümantasyon linkleri
- **One-shot servis** — Tarama bittiğinde kapanır, arkada beklemez
- **SQLite veritabanı** — Yerel, hızlı, kurulum gerektirmez

## Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| UI | WPF (.NET 10) — XAML + MVVM |
| Dil | C# 12 |
| Dump Analiz | Ham binary parser (ClrMD kullanılmaz) |
| Veritabanı | SQLite (Microsoft.Data.Sqlite) |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.4.2 |
| Build | dotnet CLI |
| Sürüm Kontrol | Git + GitHub |

## Gereksinimler

- Windows 10 veya 11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Yönetici yetkileri (Minidump dizinini okumak için)

## Kurulum

```bash
# Repoyu klonla
git clone https://github.com/barankrky/bsod_doctor.git
cd bsod_doctor

# Build al
dotnet build src/BsodDoctor

# Çalıştır
dotnet run --project src/BsodDoctor
```

Veya `src/BsodDoctor/bin/Debug/net10.0-windows/BsodDoctor.exe` dosyasını çalıştırın.

## Proje Yapısı

```
bsod_doctor/
├── AGENTS.md                        # Proje geliştirici dokümantasyonu
├── README.md                        # Bu dosya
├── src/
│   └── BsodDoctor/                  # WPF uygulaması
│       ├── App.xaml / .cs
│       ├── MainWindow.xaml / .cs
│       ├── BoolInverterConverter.cs
│       ├── ViewModels/
│       │   └── MainViewModel.cs
│       ├── Models/
│       │   ├── BsodError.cs
│       │   └── AnalysisResult.cs
│       ├── Services/
│       │   ├── MinidumpReader.cs     # Binary .dmp parser
│       │   ├── BsodWatchService.cs   # One-shot watcher
│       │   └── DatabaseService.cs    # SQLite CRUD
│       └── Data/                     # Runtime DB klasörü
├── tests/
│   └── BsodDoctor.Tests/            # Test projesi
│       ├── Program.cs
│       └── TestData/                # Synthetic dump
├── database/
│   ├── schema.sql
│   └── seed_data.json               # 51 BSOD hata kodu
└── docs/
    └── architecture.md
```

## Veritabanı

**51 BSOD hata kodu** seed_data.json içinde hazır. İlk çalıştırmada otomatik SQLite veritabanına import edilir.

**Tablolar:**
- `bsod_errors` — Hata kodları, açıklamalar, çözüm adımları
- `analysis_history` — Analiz kayıtları, cooldown kontrolü

## Test

```bash
# Test projesini çalıştır (synthetic dump ile)
dotnet run --project tests/BsodDoctor.Tests
```

Test, `tests/BsodDoctor.Tests/TestData/test_minidump.dmp` dosyasını okuyup MinidumpReader'ın doğru çalıştığını doğrular.

## Debug için Windows Binary

Release binary'leri:
```
src/BsodDoctor/bin/Release/net10.0-windows/BsodDoctor.dll
```

Debug modda çalıştırmak için:
```powershell
cd bsod_doctor
dotnet run --project src/BsodDoctor
```

## Lisans

MIT
