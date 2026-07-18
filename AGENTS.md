# BSOD Doctor — AGENTS.md

## 🧠 Proje Tanımı

**BSOD Doctor**, Windows mavi ekran (Blue Screen of Death) hatalarını analiz eden ve çözüm önerileri sunan bir Windows masaüstü uygulamasıdır. Minidump (.dmp) dosyalarını, Event Viewer loglarını ve BSOD anında oluşan sistem dosyalarını okuyarak hata kodunu tespit eder ve kullanıcıya anlaşılır, adım adım çözüm sunar.

## 🎯 Hedef Kitle

- **Son kullanıcı** — teknik bilgisi olmayan, mavi ekran görünce ne yapacağını bilemeyen kişiler
- Anlaşılır Türkçe/İngilizce çözüm adımları
- Karmaşık teknik terimler yerine sade açıklamalar

## 🏗 Mimari

```
┌─────────────────────────────────────────────────────┐
│               BSOD Doctor (WPF Desktop)              │
│                                                       │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────┐  │
│  │ Minidump     │  │ Event Viewer │  │ BSOD        │  │
│  │ Analizörü    │  │ Okuyucu      │  │ Dosya Tarayıcı│  │
│  └──────┬──────┘  └──────┬───────┘  └──────┬──────┘  │
│         │                │                  │         │
│         └────────────────┼──────────────────┘         │
│                          ▼                           │
│                 ┌────────────────┐                    │
│                 │  Çözüm Motoru  │                    │
│                 │  (Hata → KB)   │                    │
│                 └───────┬────────┘                    │
│                         │                            │
│                 ┌───────▼────────┐                    │
│                 │   Local DB     │                    │
│                 │   (SQLite)     │                    │
│                 └────────────────┘                    │
└─────────────────────────────────────────────────────┘
```

### Bileşenler

1. **Minidump Analizörü** — `.dmp` dosyalarını okuyup hata kodunu ve crash context'ini çıkarır
2. **Event Viewer Okuyucu** — Windows Event Log'dan BSOD ile ilgili kayıtları okur
3. **BSOD Dosya Tarayıcı** — `%SystemRoot%\Minidump\`, `%SystemRoot%\MEMORY.DMP` gibi yolları tarar
4. **Çözüm Motoru** — Hata koduna göre local DB'den çözüm önerisini getirir
5. **Local Veritabanı** — Hata kodu ↔ çözüm eşleştirmelerini tutar (SQLite)

## 🛠 Teknoloji Yığını

|| Katman | Teknoloji |
||--------|-----------|
|| **UI** | WPF (.NET 8) — XAML + MVVM |
|| **Dil** | C# 12 |
|| **Dump Analiz** | Microsoft.Diagnostics.Runtime (ClrMD) |
|| **Event Log** | System.Diagnostics.Eventing |
|| **Veritabanı** | SQLite (Microsoft.Data.Sqlite) |
|| **Paket Yönetimi** | NuGet |
|| **Build** | MSBuild / dotnet CLI |
|| **Sürüm Kontrol** | Git + GitHub |

## 🗄 Veritabanı Şeması (Taslak)

```sql
CREATE TABLE bsod_errors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    error_code TEXT NOT NULL UNIQUE,          -- 0x0000001A, 0x00000050
    error_name TEXT NOT NULL,                  -- MEMORY_MANAGEMENT, PAGE_FAULT_IN_NONPAGED_AREA
    category TEXT,                             -- Donanım, Sürücü, Yazılım, Bilinmiyor
    description TEXT,                          -- Kısa açıklama
    solution_steps TEXT,                       -- Adım adım çözüm (JSON veya Markdown)
    common_causes TEXT,                        -- Yaygın nedenler
    related_kb_urls TEXT,                      -- Microsoft KB linkleri
    severity INTEGER,                          -- 1-5 arası ciddiyet
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE analysis_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    dump_file_path TEXT,
    error_code TEXT,
    error_name TEXT,
    resolved BOOLEAN DEFAULT 0,
    user_feedback TEXT,
    FOREIGN KEY (error_code) REFERENCES bsod_errors(error_code)
);
```

## 🔄 Veri Akışı

1. Kullanıcı "Tara" butonuna basar
2. Uygulama Minidump/EventLog/BSOD dosyalarını tarar
3. Hata kodu tespit edilir
4. Yerel veritabanında sorgulanır
5. **Varsa**: Çözüm doğrudan gösterilir
6. **Yoksa**: Kullanıcıya henüz kayıtlı çözüm olmadığı bildirilir
   (Veritabanı güncellemeleri Vis tarafından harici olarak yapılır)

## 🚀 Başlangıç

```bash
# Repoyu klonla
git clone https://github.com/barankrky/bsod_doctor.git
cd bsod_doctor

# .NET SDK kontrolü
dotnet --version  # >= 8.0 olmalı

# WPF projesini oluştur
dotnet new wpf -n BsodDoctor -o src/BsodDoctor

# Bağımlılıkları yükle
dotnet add src/BsodDoctor package Microsoft.Diagnostics.Runtime
dotnet add src/BsodDoctor package Microsoft.Data.Sqlite
dotnet add src/BsodDoctor package CommunityToolkit.Mvvm  # MVVM için

# Build
dotnet build src/BsodDoctor
```

## 🔧 Geliştirme Konvansiyonları

- **Branch modeli:** `master` — kararlı, `dev` — geliştirme
- **Commit mesajları:** Türkçe, açıklayıcı (örn: `minidump analizörü eklendi`)
- **MVVM pattern:** View → ViewModel → Model ayrımına uyulacak
- **Testler:** Birim testleri `tests/` altında (xUnit)
- **DB şema değişiklikleri:** Migration script'i ile yapılacak

## 🤖 Agent Notları

| Makine | Agent | Rol |
|--------|-------|-----|
| **NextroByte** (bu PC) | — *(ben)* | WPF uygulamasını geliştirir, kod yazar. Baran ile birlikte çalışır. |
| **NextroPad** (Baran'ın laptop) | **Friday** | Burak (kuzen) tarafından kullanılır. Proje geliştirmeye yardımcı olur. |
| **NextroServer** (homelab, 192.168.1.69) | **Vis** | BSOD araştırması yapar, veritabanını doldurur ve günceller. **Kod içinde yer almaz.** |

## 📂 Proje Dizini (Planlanan)

```
bsod_doctor/
├── AGENTS.md                 # Bu dosya
├── README.md
├── src/
│   └── BsodDoctor/           # WPF uygulaması
│       ├── App.xaml
│       ├── MainWindow.xaml
│       ├── ViewModels/
│       │   └── MainViewModel.cs
│       ├── Views/
│       ├── Models/
│       │   ├── BsodError.cs
│       │   └── AnalysisResult.cs
│       ├── Services/
│       │   ├── IDumpAnalyzer.cs
│       │   ├── DumpAnalyzer.cs
│       │   ├── EventLogReader.cs
│       │   └── DatabaseService.cs
│       └── Data/
│           └── bsod_errors.db
├── tests/
│   └── BsodDoctor.Tests/
├── database/
│   └── schema.sql
└── docs/
    └── architecture.md
```

## 📌 Notlar

- Bu proje **NextroByte**, **NextroPad (Friday/Burak)** ve **NextroServer (Vis)** olmak üzere üç ortamda ortak geliştirilmektedir
- Vis kod içinde yer almaz, sadece araştırma ve DB güncelleme için harici olarak çalışır
- Frontend WPF ile başlıyor, ileride ihtiyaca göre değişebilir
- Kullanıcı arayüzü sade ve anlaşılır olacak, son kullanıcı odaklı
- Veritabanı repoya embedded olarak dahil edilecek (başlangıç datası ile)
