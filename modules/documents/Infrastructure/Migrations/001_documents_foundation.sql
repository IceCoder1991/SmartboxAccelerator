BEGIN;
CREATE SCHEMA IF NOT EXISTS documents;
CREATE TYPE documents.registry_lifecycle AS ENUM ('draft', 'published', 'archived');
CREATE TABLE documents.document_type_definition (
 tenant_id text NOT NULL, id uuid NOT NULL, version integer NOT NULL CHECK (version > 0), lifecycle documents.registry_lifecycle NOT NULL,
 technical_name varchar(64) NOT NULL, display_name varchar(200) NOT NULL, description text, classification_instructions text NOT NULL,
 extraction_schema jsonb NOT NULL, prompt text NOT NULL, validation_rules jsonb NOT NULL, review_policy jsonb NOT NULL,
 model_configuration jsonb NOT NULL, evaluation_dataset_reference text, output_mapping jsonb NOT NULL,
 created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, updated_by text NOT NULL,
 PRIMARY KEY (tenant_id, id, version), UNIQUE (tenant_id, technical_name, version)
);
CREATE TABLE documents.document (
 tenant_id text NOT NULL, id uuid NOT NULL, lifecycle text NOT NULL, lineage jsonb NOT NULL,
 created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id)
);
CREATE TABLE documents.document_page (
 tenant_id text NOT NULL, document_id uuid NOT NULL, id uuid NOT NULL, page_number integer NOT NULL CHECK(page_number > 0),
 original_object_name text NOT NULL, normalised_object_name text, original_checksum char(64) NOT NULL, normalised_checksum char(64), metadata jsonb NOT NULL DEFAULT '{}',
 PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, document_id, page_number),
 FOREIGN KEY (tenant_id, document_id) REFERENCES documents.document(tenant_id, id)
);
CREATE TABLE documents.document_package (tenant_id text NOT NULL, id uuid NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY(tenant_id,id));
CREATE TABLE documents.document_package_item (tenant_id text NOT NULL, package_id uuid NOT NULL, document_id uuid NOT NULL, position integer NOT NULL, PRIMARY KEY(tenant_id,package_id,document_id));
CREATE TABLE documents.processing_run (tenant_id text NOT NULL, id uuid NOT NULL, document_id uuid, package_id uuid, run_number integer NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY(tenant_id,id));
CREATE TABLE documents.processing_stage (tenant_id text NOT NULL, id uuid NOT NULL, run_id uuid NOT NULL, kind text NOT NULL, state text NOT NULL, attempts integer NOT NULL DEFAULT 0, started_at timestamptz, completed_at timestamptz, error_code text, provider_metadata jsonb NOT NULL DEFAULT '{}', PRIMARY KEY(tenant_id,id), FOREIGN KEY(tenant_id,run_id) REFERENCES documents.processing_run(tenant_id,id));

-- The application sets app.tenant_id at transaction start. RLS is defence in depth.
DO $$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['document_type_definition','document','document_page','document_package','document_package_item','processing_run','processing_stage'] LOOP
 EXECUTE format('ALTER TABLE documents.%I ENABLE ROW LEVEL SECURITY', t);
 EXECUTE format('ALTER TABLE documents.%I FORCE ROW LEVEL SECURITY', t);
 EXECUTE format('CREATE POLICY tenant_isolation ON documents.%I USING (tenant_id = current_setting(''app.tenant_id'', true)) WITH CHECK (tenant_id = current_setting(''app.tenant_id'', true))', t);
END LOOP; END $$;

-- Reject modifications of immutable versions even if a caller bypasses the application.
CREATE FUNCTION documents.enforce_published_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD.lifecycle = 'archived' OR (OLD.lifecycle = 'published' AND (TG_OP = 'DELETE' OR NOT (NEW.lifecycle = 'archived' AND (to_jsonb(NEW) - ARRAY['lifecycle','updated_at','updated_by']) = (to_jsonb(OLD) - ARRAY['lifecycle','updated_at','updated_by'])))) THEN RAISE EXCEPTION 'published and archived document type versions are immutable'; END IF;
 RETURN NEW; END $$;
CREATE TRIGGER document_type_immutable BEFORE UPDATE OR DELETE ON documents.document_type_definition FOR EACH ROW EXECUTE FUNCTION documents.enforce_published_immutable();
COMMIT;
