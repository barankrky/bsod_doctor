-- BSOD Doctor — Veritabanı Şeması
-- SQLite 3.x uyumlu

-- BSOD hata kodları ve çözümleri
CREATE TABLE IF NOT EXISTS bsod_errors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    error_code TEXT NOT NULL UNIQUE,          -- 0x0000001A, 0x00000050
    error_name TEXT NOT NULL,                  -- MEMORY_MANAGEMENT, PAGE_FAULT_IN_NONPAGED_AREA
    category TEXT,                             -- Donanım, Sürücü, Yazılım, Bilinmiyor
    description TEXT,                          -- Kısa açıklama
    solution_steps TEXT,                       -- Adım adım çözüm (JSON veya Markdown)
    kesin_cozum TEXT,                          -- En kesin çözüm (1-2 adım)
    common_causes TEXT,                        -- Yaygın nedenler
    related_kb_urls TEXT,                      -- Microsoft KB linkleri
    severity INTEGER DEFAULT 0,               -- 1-5 arası ciddiyet (0 = bilinmiyor)
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Analiz geçmişi
CREATE TABLE IF NOT EXISTS analysis_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    dump_file_path TEXT,
    error_code TEXT,
    error_name TEXT,
    resolved INTEGER DEFAULT 0,
    user_feedback TEXT,
    is_notified INTEGER DEFAULT 0
);

-- Hızlı arama için indeksler
CREATE INDEX IF NOT EXISTS idx_bsod_errors_code ON bsod_errors(error_code);
CREATE INDEX IF NOT EXISTS idx_analysis_history_timestamp ON analysis_history(timestamp);
CREATE INDEX IF NOT EXISTS idx_analysis_history_code_timestamp ON analysis_history(error_code, timestamp);
