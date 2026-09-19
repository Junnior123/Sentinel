CREATE TABLE IF NOT EXISTS report_shares (
 scan_id TEXT PRIMARY KEY REFERENCES scans(id) ON DELETE CASCADE,
 token_hash TEXT NOT NULL UNIQUE,
 expires INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS report_shares_expiry ON report_shares(expires);
