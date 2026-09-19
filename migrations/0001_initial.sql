PRAGMA foreign_keys = ON;
CREATE TABLE operators (id TEXT PRIMARY KEY, login TEXT NOT NULL, role TEXT NOT NULL CHECK(role IN ('owner','reviewer')), invited_by TEXT);
CREATE TABLE auth_sessions (token_hash TEXT PRIMARY KEY, operator_id TEXT NOT NULL REFERENCES operators(id) ON DELETE CASCADE, expires INTEGER NOT NULL);
CREATE TABLE oauth_states (hash TEXT PRIMARY KEY, expires INTEGER NOT NULL);
CREATE TABLE scans (
 id TEXT PRIMARY KEY, owner_id TEXT NOT NULL REFERENCES operators(id), nickname TEXT NOT NULL, reason TEXT NOT NULL,
 scope TEXT NOT NULL, code_hash TEXT UNIQUE NOT NULL, code_expires INTEGER NOT NULL, token_hash TEXT UNIQUE,
 state TEXT NOT NULL DEFAULT 'created', created INTEGER NOT NULL, expires INTEGER NOT NULL,
 progress TEXT, summary TEXT, review TEXT, reviewed_by TEXT, bytes INTEGER NOT NULL DEFAULT 0, chunk_count INTEGER,
 finalized INTEGER NOT NULL DEFAULT 0, receipt TEXT
);
CREATE INDEX scans_owner_created ON scans(owner_id,created DESC);
CREATE INDEX scans_expiry ON scans(expires);
CREATE TABLE chunks (scan_id TEXT NOT NULL REFERENCES scans(id) ON DELETE CASCADE, seq INTEGER NOT NULL, hash TEXT NOT NULL, data TEXT NOT NULL, bytes INTEGER NOT NULL, PRIMARY KEY(scan_id,seq));
CREATE TRIGGER chunks_budget BEFORE INSERT ON chunks
WHEN (SELECT bytes + NEW.bytes > 10485760 OR finalized != 0 OR state NOT IN ('claimed','scanning','uploading') FROM scans WHERE id=NEW.scan_id)
BEGIN
 SELECT RAISE(ABORT,'report_limit_or_closed');
END;
CREATE TRIGGER chunks_account AFTER INSERT ON chunks BEGIN UPDATE scans SET bytes=bytes+NEW.bytes,state='uploading' WHERE id=NEW.scan_id; END;
CREATE TABLE audit (id INTEGER PRIMARY KEY AUTOINCREMENT, actor TEXT NOT NULL, action TEXT NOT NULL, scan_id TEXT, created INTEGER NOT NULL);
CREATE INDEX audit_time ON audit(created);
CREATE TABLE rate_limits (key TEXT PRIMARY KEY, count INTEGER NOT NULL, expires INTEGER NOT NULL);
