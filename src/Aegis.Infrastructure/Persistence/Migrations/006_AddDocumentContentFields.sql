-- Add content and external_id columns to documents table for data connector support
ALTER TABLE documents
ADD COLUMN IF NOT EXISTS content TEXT,
ADD COLUMN IF NOT EXISTS external_id VARCHAR(500);

-- Create index on external_id for fast lookups
CREATE INDEX IF NOT EXISTS idx_documents_external_id ON documents(data_source_id, external_id);

-- Add comment
COMMENT ON COLUMN documents.content IS 'Inline content for documents synced from data connectors';
COMMENT ON COLUMN documents.external_id IS 'External identifier for tracking source records from data connectors';
