-- Create query_history table
CREATE TABLE IF NOT EXISTS query_history (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    conversation_id UUID REFERENCES conversations(id) ON DELETE CASCADE,
    query TEXT NOT NULL,
    response TEXT NOT NULL,
    tokens_used INTEGER NOT NULL DEFAULT 0,
    response_time_ms BIGINT NOT NULL DEFAULT 0,
    chunks_retrieved INTEGER NOT NULL DEFAULT 0,
    retrieval_method VARCHAR(50) NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_query_history_workspace_id ON query_history(workspace_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_query_history_user_id ON query_history(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_query_history_conversation_id ON query_history(conversation_id);
CREATE INDEX IF NOT EXISTS idx_query_history_created_at ON query_history(created_at DESC);

-- Full-text search index on query and response
CREATE INDEX IF NOT EXISTS idx_query_history_search ON query_history USING gin(to_tsvector('english', query || ' ' || response));

-- Apply updated_at trigger
DROP TRIGGER IF EXISTS update_query_history_updated_at ON query_history;
CREATE TRIGGER update_query_history_updated_at
    BEFORE UPDATE ON query_history
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
