<p align="center">
  <img src="docs/hero-banner.gif" alt="BSOD Doctor - Windows Mavi Ekran Analiz Aracı" width="100%">
</p>

# BSOD Doctor 🩺

Windows BSOD (mavi ekran) hatalarını analiz eden, tanımlayan ve Türkçe çözüm sunan araç. Minidump dosyalarını doğrudan okuyup hata kodunu çıkararak kullanıcıya adım adım rehberlik eder.

---

## ✨ Özellikler

- **Otomatik tanı** — Uygulama başlarken minidump dizinini tarar, yeni hataları yakalar
- **75 BSOD kodu** — En yaygın mavi ekran hataları için Türkçe açıklama, nedenler, çözüm adımları ve kesin çözüm özeti
- **Tüm dump formatları** — PAGEDUMP64, PAGEDUMP32 ve klasik MDMP (ClrMd gerekmez, ham binary okuma)
- **Windows Service** — Arkada çalışan servis ile yeni dump'ları 30dk'da bir otomatik tarar
- **Toast bildirimleri** — Yeni BSOD tespit edildiğinde Windows bildirimi gönderir, tıklayınca detayı açar
- **Ham minidump parser** — ClrMD bağımlılığı yok, doğrudan binary format okuyucu
- **Koyu/Açık tema desteği** — 🌙 butonu ile tek tıkla geçiş, tercih kalıcı olarak kaydedilir
- **Geçmiş listesi** — Çözülmemiş kayıtlar, çift tıkla detay görüntüleme
- **Cooldown sistemi** — Aynı hata için 24s tekrar tarama önleme (servis için 7 gün bildirim cooldown)
- **SQLite (WAL modu)** — Kurulum gerektirmez, bağlantı havuzu ile yüksek performans
- **Seed data** — Veritabanı ilk çalıştırmada otomatik doldurulur

---

## 🚀 Hızlı Başlangıç

```bash
git clone https://github.com/burakdmrbkr/bsod_doctor.git
cd bsod_doctor
dotnet run --project src/BsodDoctor
```

**Gereksinim:** Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), yönetici yetkisi.

Kurulum sürümü için GitHub Releases sayfasından `.exe` indirip çalıştırın.

---

## 🧱 Proje Yapısı

```
bsod_doctor/
├── src/
│   ├── BsodDoctor/                  # WPF uygulaması (MVVM)
│   │   ├── ViewModels/              # MainViewModel (CommunityToolkit.Mvvm)
│   │   ├── Models/                  # BsodError, AnalysisResult, HistoryItem
│   │   ├── Services/                # MinidumpReader, BsodWatchService, DatabaseService
│   │   └── Resources/Themes/        # Dark / Light tema XAML'leri
│   └── BsodDoctor.Service/          # Windows Background Service
│       ├── DumpScannerService.cs    # 30dk'da bir tarama yapan servis
│       └── Program.cs               # Generic Host ile DI yapılandırması
├── tests/
│   └── BsodDoctor.Tests/            # xUnit testleri (24 test)
├── database/
│   └── seed_data.json               # 75 BSOD kodu + Türkçe çözümler
├── setup/
│   └── bsod-doctor.iss              # InnoSetup kurulum betiği
└── .github/workflows/
    └── release.yml                  # CI/CD: build → test → publish → release
```

---

## 📄 Lisans

MIT