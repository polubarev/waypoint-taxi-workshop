CREATE TABLE IF NOT EXISTS outbox(id uuid PRIMARY KEY,type text NOT NULL,payload jsonb NOT NULL,created_at timestamptz NOT NULL DEFAULT now(),published_at timestamptz);
CREATE INDEX IF NOT EXISTS outbox_pending ON outbox(created_at) WHERE published_at IS NULL;

CREATE EXTENSION IF NOT EXISTS postgis;
CREATE TABLE IF NOT EXISTS zones(id uuid PRIMARY KEY,name text NOT NULL,type text NOT NULL CHECK(type IN ('Service','Restricted')),active boolean NOT NULL,region_id text NOT NULL,geom geometry(Polygon,4326) NOT NULL);
CREATE INDEX IF NOT EXISTS zones_geom ON zones USING gist(geom);
CREATE TABLE IF NOT EXISTS live_trips(row_id bigserial PRIMARY KEY,booking_id uuid NOT NULL,event_id uuid NOT NULL,pickup jsonb NOT NULL,dropoff jsonb NOT NULL,region_id text NOT NULL,occurred_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS closed_trips(booking_id uuid PRIMARY KEY,closed_at timestamptz NOT NULL);
INSERT INTO zones VALUES
('10000000-0000-0000-0000-000000000001','Tel Aviv service area','Service',true,'central',ST_GeomFromText('POLYGON((34.75 32.02,34.85 32.02,34.85 32.14,34.75 32.14,34.75 32.02))',4326)),
('10000000-0000-0000-0000-000000000002','Haifa service area','Service',true,'north',ST_GeomFromText('POLYGON((34.95 32.75,35.08 32.75,35.08 32.86,34.95 32.86,34.95 32.75))',4326)),
('10000000-0000-0000-0000-000000000003','Central restricted area','Restricted',true,'central',ST_GeomFromText('POLYGON((34.785 32.075,34.795 32.075,34.795 32.085,34.785 32.085,34.785 32.075))',4326)),
('10000000-0000-0000-0000-000000000004','Imported inactive boundary','Restricted',false,'central',ST_GeomFromText('POLYGON((34.80 32.10,34.82 32.12,34.80 32.12,34.82 32.10,34.80 32.10))',4326))
ON CONFLICT DO NOTHING;
