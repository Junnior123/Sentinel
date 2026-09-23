CREATE INDEX chunks_findings ON chunks(scan_id,seq) WHERE json_array_length(data,'$.findings')>0;
CREATE INDEX chunks_artifacts ON chunks(scan_id,seq) WHERE json_array_length(data,'$.artifacts')>0;
CREATE INDEX chunks_coverage ON chunks(scan_id,seq) WHERE json_array_length(data,'$.coverage')>0;
CREATE INDEX auth_expiry ON auth_sessions(expires);
CREATE INDEX oauth_expiry ON oauth_states(expires);
CREATE INDEX rate_expiry ON rate_limits(expires);
