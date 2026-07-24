# BSOD Doctor — AGENTS.md

## 🧠 Proje Tanımı

**BSOD Doctor**, Windows mavi ekran (BSOD) hatalarını analiz eden ve çözüm önerileri sunan bir Windows masaüstü uygulamasıdır. `C:\Windows\Minidump\` dizinindeki `.dmp` dosyalarını ham binary olarak okuyup hata kodunu çıkarır, yerel SQLite veritabanındaki çözümlerle eşleştirir ve kullanıcıya adım adım çözüm sunar.

## 🎯 Hedef Kitle

- **Son kullanıcı** — teknik bilgisi olmayan, mavi ekran görünce ne yapacağını bilemeyen kişiler
- Anlaşılır Türkçe çözüm adımları
- Karmaşık teknik terimler yerine sade açıklamalar

## 🏗 Mimari

```
┌──────────────────────────────────────────────────────┐
│               BSOD Doctor (WPF Desktop)               │
│                                                        │
│  Başlangıçta otomatik çalışır:                         │
│  ┌────────────────────────────────────────────┐       │
│  │  BsodWatchService (one-shot)               │       │
│  │  ┌──────────────┐   ┌────────────────┐     │       │
│  │  │ MinidumpReader│──→│  DatabaseService│     │       │
│  │  │ (binary       │   │  - cooldown    │     │       │
│  │  │  parser)      │   │  - sorgulama   │     │       │
│  │  └──────┬───────┘   │  - seed import  │     │       │
│  │         │           │  - history      │     │       │
│  │         │           └───────┬────────┘     │       │
│  │         ▼                   │              │       │
│  │  ┌────────────┐            │              │       │
│  │  │  Cooldown  │◄───────────┘              │       │
│  │  │  (1 gün)   │                           │       │
│  │  └────────────┘                           │       │
│  └────────────────────────────────────────────┘       │
│                         │                             │
│                         ▼                             │
│               ┌──────────────────┐                    │
│               │  MainViewModel   │                    │
│               │  (event → UI)    │                    │
│               └────────┬────────┘                     │
│                        │                              │
│               ┌────────▼────────┐                     │
│               │   MainWindow    │                     │
│               │  (WPF + XAML)   │                     │
│               └─────────────────┘                     │
└──────────────────────────────────────────────────────┘
```

### Bileşenler

1. **MinidumpReader** — `.dmp` dosyasını ham binary okuyup ExceptionStream'den BugCheckCode çıkarır. ClrMD kullanmaz.
2. **BsodWatchService** — One-shot watcher. Uygulama açılırken Minidump dizinini tarar, son 1 günde değişmiş dosyaları okur, cooldown kontrolü yapar, yeni hata bulursa UI'a event fırlatır. Sürekli beklemez — tara, bul/kapat, bitir.
3. **DatabaseService** — SQLite bağlantısı, tablo yönetimi, seed data import, history kaydı ve cooldown sorgulaması.
4. **MainViewModel** — CommunityToolkit.Mvvm ile ObservableObject, WatchService event'lerine abone olur, UI property'lerini günceller.

## 🛠 Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| **UI** | WPF (.NET 10) — XAML + MVVM |
| **Dil** | C# 12 |
| **Dump Analiz** | Ham binary parser (ClrMD kullanılmaz) |
| **Veritabanı** | SQLite (Microsoft.Data.Sqlite + SQLitePCLRaw) |
| **MVVM Toolkit** | CommunityToolkit.Mvvm 8.4.2 |
| **Paket Yönetimi** | NuGet |
| **Build** | dotnet CLI |
| **Sürüm Kontrol** | Git + GitHub |

## 🗄 Veritabanı Şeması

```sql
CREATE TABLE bsod_errors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    error_code TEXT NOT NULL UNIQUE,          -- 0x0000001A, 0x00000050
    error_name TEXT NOT NULL,                 -- MEMORY_MANAGEMENT
    category TEXT,                            -- Donanım, Sürücü, Yazılım
    description TEXT,                         -- Kısa açıklama
    solution_steps TEXT,                      -- Adım adım çözüm (Markdown)
    common_causes TEXT,                       -- Yaygın nedenler
    related_kb_urls TEXT,                     -- Microsoft KB linkleri
    severity INTEGER DEFAULT 0,              -- 1-5 arası ciddiyet
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE analysis_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    dump_file_path TEXT,
    error_code TEXT,
    error_name TEXT,
    resolved INTEGER DEFAULT 0,
    user_feedback TEXT
);

CREATE INDEX idx_bsod_errors_code ON bsod_errors(error_code);
CREATE INDEX idx_analysis_history_timestamp ON analysis_history(timestamp);
```

## 🔄 Veri Akışı (Otomatik Tarama)

1. Uygulama başlatılır → `DatabaseService.InitializeAsync()` çalışır (tablolar + seed data)
2. `BsodWatchService.ScanOnceAsync()` tetiklenir
3. `C:\Windows\Minidump\` dizininde son **1 günde** değişmiş `.dmp` dosyaları taranır
4. Her dosya `MinidumpReader.ReadBugCheckCode()` ile parse edilir
5. Bulunan hata kodu `DatabaseService.IsErrorInCooldownAsync()` ile kontrol edilir (son 24 saatte aynı hata kaydı varsa atlanır)
6. **Yeni hata bulunduysa**: History'ye kaydedilir, `NewErrorFound` event'i tetiklenir, UI otomatik güncellenir
7. **Bulunamadıysa**: `ScanCompleted` event'i tetiklenir, uygulama "Yeni BSOD bulunamadı" durumunda kalır
8. Servis kapanır — arkada beklemez

Manuel tarama için "Minidump Tara" butonu kullanılır.

## 🚀 Başlangıç

```bash
# Repoyu klonla
git clone https://github.com/barankrky/bsod_doctor.git
cd bsod_doctor

# .NET SDK kontrolü
dotnet --version  # >= 10.0 olmalı

# Build
dotnet build src/BsodDoctor
```

Not: Bağımlılıklar NuGet restore ile otomatik gelir (manual `dotnet add` gerekmez).

## 🔧 Geliştirme Konvansiyonları

- **Branch modeli:** `master` — kararlı, `feature/*` — geliştirme
- **Commit mesajları:** Türkçe, feat/fix/docs prefix'li
- **MVVM pattern:** View → ViewModel → Model ayrımına uyulur
- **DB şema değişiklikleri:** `InitializeAsync()` içinde migration ile yapılır
|- **Birim testleri:** `tests/BsodDoctor.Tests/` altında (konsol test) — MinidumpReader synthetic dump ile test edildi

## 🤖 Agent Notları

| Makine | Agent | Rol |
|--------|-------|-----|
| **NextroByte** (bu PC) | Baran | WPF uygulaması geliştirir, kod yazar |
| **NextroPad** (Burak'ın laptop) | **Friday** | Burak tarafından kullanılır, proje geliştirmeye yardımcı olur |
| **NextroServer** (192.168.1.69) | **Vis** | BSOD araştırması yapar, veritabanını doldurur. Kod içinde yer almaz |

## 📂 Proje Dizini

```
bsod_doctor/
├── AGENTS.md                 # Bu dosya
├── README.md
├── src/
│   └── BsodDoctor/           # WPF uygulaması
│       ├── App.xaml / .cs
│       ├── MainWindow.xaml / .cs
│       ├── BoolInverterConverter.cs
│       ├── ViewModels/
│       │   └── MainViewModel.cs
│       ├── Models/
│       │   ├── BsodError.cs
│       │   └── AnalysisResult.cs
│       ├── Services/
│       │   ├── MinidumpReader.cs       # Binary .dmp parser
│       │   ├── BsodWatchService.cs     # One-shot watcher
│       │   └── DatabaseService.cs      # SQLite CRUD
│       └── Data/                       # Runtime DB klasörü
├── tests/                    # Birim testleri
├── database/
│   ├── schema.sql
│   └── seed_data.json        # 51 BSOD hatası
└── docs/
    └── architecture.md
```

## 📌 Notlar

- Uygulama **Windows** hedeflidir (WPF, Minidump yolları)
- Linux'ta geliştirilir, test için Windows gereklidir veya sahte dump dosyası kullanılır
- Seed data (51 BSOD hata kodu) veritabanına ilk çalıştırmada otomatik import edilir
- Bu proje **NextroByte**, **NextroPad (Friday/Burak)** ve **NextroServer (Vis)** olmak üzere üç ortamda ortak geliştirilmektedir
- Vis kod içinde yer almaz, sadece araştırma ve DB güncelleme için harici çalışır
- Veritabanı repoya embedded dahil edilmez; çalışma anında `bin/Data/` altında oluşur
