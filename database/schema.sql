-- BSOD Doctor Database Schema
-- SQLite

CREATE TABLE IF NOT EXISTS bsod_errors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    error_code TEXT NOT NULL UNIQUE,          -- 0x0000001A, 0x00000050
    error_name TEXT NOT NULL,                  -- MEMORY_MANAGEMENT, PAGE_FAULT_IN_NONPAGED_AREA
    category TEXT,                             -- Donanım, Sürücü, Yazılım, Bilinmiyor
    description TEXT,                          -- Kısa açıklama
    solution_steps TEXT,                       -- Adım adım çözüm (Markdown)
    common_causes TEXT,                        -- Yaygın nedenler
    related_kb_urls TEXT,                      -- Microsoft KB linkleri
    severity INTEGER DEFAULT 0,                -- 1-5 arası ciddiyet
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS analysis_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    dump_file_path TEXT,
    error_code TEXT,
    error_name TEXT,
    resolved INTEGER DEFAULT 0,
    user_feedback TEXT,
    FOREIGN KEY (error_code) REFERENCES bsod_errors(error_code)
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_bsod_errors_code ON bsod_errors(error_code);
CREATE INDEX IF NOT EXISTS idx_bsod_errors_category ON bsod_errors(category);
CREATE INDEX IF NOT EXISTS idx_analysis_history_timestamp ON analysis_history(timestamp);
