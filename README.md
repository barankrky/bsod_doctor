# BSOD Doctor 🩺

Windows mavi ekran (BSOD) hatalarını analiz eden, açıklayan ve Türkçe çözüm önerileri sunan bir WPF masaüstü uygulaması. Minidump dosyalarını doğrudan binary olarak okur, hata kodunu çıkarır ve veritabanındaki çözümle eşleştirir.

## Özellikler

- **Otomatik tarama** — Uygulama açıldığında minidump dizinini tarar (Registry'den okur, fallback: `C:\Windows\Minidump`)
- **Tüm dump'ları tara** — Manuel butonla eski dump'lar da dahil tüm dosyaları tarar
- **PAGEDUMP64 + PAGEDU32 + MDMP desteği** — Windows 24H2+ dahil tüm dump formatlarını açar
- **Ham binary parser** — ClrMD gerektirmez, doğrudan binary okur
- **50 BSOD hata kodu** — En yaygın mavi ekran hataları için Türkçe açıklama ve çözüm adımları
- **Kesin Çözüm** — Her hata için en kısa ve etkili çözüm özeti
- **Severity bildirimi** — Her hata 1-5 arası ciddiyet puanıyla işaretlenir
- **Cooldown sistemi** — Aynı hata 24 saat içinde tekrar bildirilmez
- **Geçmiş analiz listesi** — Çözülmemiş kayıtlar listelenir, geçmişe tıklayarak detay görüntülenir
- **Dark / Light tema** — Kullanıcı tercihi `Data/settings.json`'a kaydedilir, kalıcıdır
- **One-shot servis** — Tarama bittiğinde kapanır, arkada beklemez
- **SQLite veritabanı** — Yerel, hızlı, kurulum gerektirmez
- **CI/CD** — GitHub Actions ile otomatik build + InnoSetup kurulum paketi

## Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| UI | WPF (.NET 10) — XAML + MVVM |
| Dil | C# 12 |
| Dump Analiz | Ham binary parser (PAGEDU64 / PAGEDU32 / MDMP) |
| Veritabanı | SQLite (Microsoft.Data.Sqlite 10) |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.4.2 |
| Build | dotnet CLI + GitHub Actions |
| Kurulum | InnoSetup |

## Gereksinimler

- Windows 10 veya 11 (x64)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (çalıştırmak için)
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (geliştirme için)
- Yönetici yetkileri (minidump dizinini okumak için)

## Kurulum

### Geliştirme

```bash
# Repoyu klonla
git clone https://github.com/burakdmrbkr/bsod_doctor.git
cd bsod_doctor

# Build al
dotnet build src/BsodDoctor

# Çalıştır
dotnet run --project src/BsodDoctor
```

Veya `src/BsodDoctor/bin/Debug/net10.0-windows/BsodDoctor.exe` dosyasını çalıştırın.

### Kurulum (Release)

GitHub Releases sayfasından `.exe` kurulum dosyasını indirin ve yönetici olarak çalıştırın. InnoSetup ile paketlenmiştir, .NET 10 Runtime otomatik denetlenir.

## Proje Yapısı

```
bsod_doctor/
├── src/BsodDoctor/                  # WPF uygulaması (net10.0-windows)
│   ├── App.xaml / .cs               # Uygulama giriş noktası
│   ├── MainWindow.xaml / .cs        # Ana pencere (code-behind boş, MVVM)
│   ├── BoolInverterConverter.cs     # XAML value converter
│   ├── ViewModels/
│   │   └── MainViewModel.cs         # Ana VM (CommunityToolkit.Mvvm source gen)
│   ├── Models/
│   │   ├── BsodError.cs             # BSOD hata kodu + çözüm modeli
│   │   ├── AnalysisResult.cs        # Tarama sonucu modeli
│   │   └── HistoryItem.cs           # Geçmiş analiz kaydı modeli
│   ├── Services/
│   │   ├── MinidumpReader.cs        # Binary .dmp parser (PAGEDU64/32, MDMP)
│   │   ├── BsodWatchService.cs      # One-shot tarama + cooldown
│   │   ├── DatabaseService.cs       # SQLite CRUD + seed import + migration
│   │   └── IDatabaseService.cs      # Servis arayüzü
│   └── Resources/Themes/            # Dark/Light tema XAML'leri
├── tests/BsodDoctor.Tests/          # Test projesi (console app)
│   ├── Program.cs                   # Synthetic dump ile doğrulama
│   └── TestData/                    # Test .dmp dosyaları
├── database/
│   ├── schema.sql                   # SQLite şema referansı
│   └── seed_data.json               # 50 BSOD hata kodu + çözümler
├── setup/
│   └── bsod-doctor.iss              # InnoSetup kurulum betiği
├── .github/workflows/
│   └── release.yml                  # CI/CD: build + test + publish + release
├── docs/
│   └── architecture.md              # Mimari dokümantasyon
├── AGENTS.md                        # Claude Code geliştirici notları
└── README.md                        # Bu dosya
```

## Veritabanı

**50 BSOD hata kodu** `database/seed_data.json` içinde hazır. İlk çalıştırmada otomatik SQLite veritabanına import edilir. Build/publish çıktısına otomatik kopyalanır — geliştirme ve kurulum sürümlerinde çalışır.

**Tablolar:**
- `bsod_errors` — Hata kodları, açıklamalar, çözüm adımları, kesin çözüm
- `analysis_history` — Analiz kayıtları, çözülme durumu, cooldown kontrolü

## Test

```bash
# Test projesini çalıştır (synthetic dump ile)
dotnet run --project tests/BsodDoctor.Tests
```

Test, `tests/BsodDoctor.Tests/TestData/test_minidump.dmp` dosyasını okuyup MinidumpReader'ın `0x00000050` (PAGE_FAULT_IN_NONPAGED_AREA) döndürdüğünü doğrular.

## CI/CD

Her `master` push'unda GitHub Actions tetiklenir:

1. `dotnet restore` — Bağımlılıkları yükler
2. `dotnet build` — Release build alır
3. `dotnet run --project tests/BsodDoctor.Tests` — Testleri çalıştırır
4. `dotnet publish` — Yayın çıktısı hazırlar
5. `iscc` — InnoSetup ile kurulum paketi oluşturur
6. `gh release create` — GitHub Release oluşturur (prerelease olarak)

## Ekran Görüntüleri

*(yakında)*

## Lisans

MIT