CREATE TABLE IF NOT EXISTS outbox(id uuid PRIMARY KEY,type text NOT NULL,payload jsonb NOT NULL,created_at timestamptz NOT NULL DEFAULT now(),published_at timestamptz);
CREATE INDEX IF NOT EXISTS outbox_pending ON outbox(created_at) WHERE published_at IS NULL;

CREATE TABLE IF NOT EXISTS roles(name text PRIMARY KEY);
CREATE TABLE IF NOT EXISTS teams(id text PRIMARY KEY,name text NOT NULL,manager_id uuid NOT NULL);
CREATE TABLE IF NOT EXISTS users(id uuid PRIMARY KEY,username text UNIQUE NOT NULL,display_name text NOT NULL,password_hash text NOT NULL,team_id text NOT NULL REFERENCES teams(id),manager_id uuid,roles text[] NOT NULL,role_version bigint NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS refresh_tokens(hash text PRIMARY KEY,user_id uuid NOT NULL REFERENCES users(id),expires_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS booking_audit(event_id uuid PRIMARY KEY,booking_id uuid NOT NULL,received_at timestamptz NOT NULL DEFAULT now());
