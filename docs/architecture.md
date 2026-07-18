# BSOD Doctor — Mimari Doküman

## Genel Bakış

BSOD Doctor, Windows mavi ekran (BSOD) hatalarını analiz eden ve kullanıcıya anlaşılır çözümler sunan bir WPF masaüstü uygulamasıdır.

## Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| UI | WPF (.NET 8) — XAML + MVVM |
| Dil | C# 12 |
| Dump Analiz | Microsoft.Diagnostics.Runtime (ClrMD) |
| Event Log | System.Diagnostics.Eventing |
| Veritabanı | SQLite (Microsoft.Data.Sqlite) |
| A2A İletişim | Hermes A2A Bridge (harici servis — WPF içinde değil) |
| Paket Yönetimi | NuGet |
| Build | MSBuild / dotnet CLI |

## Proje Dizini

```
bsod_doctor/
├── AGENTS.md
├── README.md
├── LICENSE
├── database/
│   └── schema.sql              # Veritabanı şeması
├── src/
│   └── BsodDoctor/             # WPF uygulaması
│       ├── BsodDoctor.csproj
│       ├── App.xaml / .cs      # Uygulama giriş noktası + DI
│       ├── MainWindow.xaml / .cs
│       ├── Models/
│       │   ├── BsodError.cs    # BSOD hata kodu modeli
│       │   └── AnalysisResult.cs # Analiz sonucu modeli
│       ├── ViewModels/
│       │   └── MainViewModel.cs # Ana ViewModel
│       ├── Services/
│       │   ├── IDatabaseService.cs
│       │   ├── DatabaseService.cs   # SQLite CRUD
│       │   ├── IDumpAnalyzer.cs
│       │   ├── DumpAnalyzer.cs      # Minidump analizi
│       │   ├── EventLogReader.cs    # Event Log okuyucu
│       ├── Data/
│       │   └── bsod_errors.db   # SQLite veritabanı (embedded)
│       └── Styles/
│           └── ModernStyles.xaml # Modern koyu tema
├── tests/
│   └── BsodDoctor.Tests/
├── docs/
│   └── architecture.md          # Bu dosya
└── .gitignore
```

## Veri Akışı

1. Kullanıcı "Tara" butonuna basar
2. Uygulama Minidump/EventLog/BSOD dosyalarını tarar
3. Hata kodu tespit edilir
4. **Varsa**: Yerel SQLite veritabanından çözüm getirilir
5. **Yoksa**: Hata kodu local DB'de yoksa, kayıt "bilinmeyen hata" olarak işaretlenir
6. Vis (harici agent) periyodik olarak DB'yi güncel tutar
7. Sonuç kullanıcıya gösterilir

## Veritabanı Şeması

- `bsod_errors` — Hata kodu ↔ çözüm eşleştirmeleri (20 seed kayıt)
- `analysis_history` — Yapılan analizlerin geçmişi

Detaylı şema için: `database/schema.sql`

## Vis ile Entegrasyon

Vis (192.168.1.69:8765) harici bir Hermes agent olarak çalışır:
- WPF uygulaması **kendi local veritabanını** kullanır
- Vis, `hermes-a2a-bridge` üzerinden periyodik olarak DB'yi günceller
- Yeni BSOD hataları araştırıldıkça seed data genişletilir

## Geliştirme

```bash
# Bağımlılıkları yükle
dotnet restore src/BsodDoctor

# Build
dotnet build src/BsodDoctor

# Çalıştır
dotnet run --project src/BsodDoctor

# Test
dotnet test tests/BsodDoctor.Tests
```
