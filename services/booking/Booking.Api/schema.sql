CREATE TABLE IF NOT EXISTS outbox(id uuid PRIMARY KEY,type text NOT NULL,payload jsonb NOT NULL,created_at timestamptz NOT NULL DEFAULT now(),published_at timestamptz);
CREATE INDEX IF NOT EXISTS outbox_pending ON outbox(created_at) WHERE published_at IS NULL;

CREATE TABLE IF NOT EXISTS bookings(id uuid PRIMARY KEY,employee_id uuid NOT NULL,manager_id uuid NOT NULL,team_id text NOT NULL,pickup jsonb NOT NULL,dropoff jsonb NOT NULL,distance_km double precision NOT NULL,region_id text NOT NULL,status text NOT NULL CHECK(status IN ('PendingApproval','Approved','Rejected','Assigned','Completed','Cancelled')),taxi_id text,note text NOT NULL DEFAULT '',created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS bookings_employee ON bookings(employee_id);
CREATE INDEX IF NOT EXISTS bookings_manager ON bookings(manager_id,status);
CREATE TABLE IF NOT EXISTS booking_audit(id bigserial PRIMARY KEY,booking_id uuid NOT NULL REFERENCES bookings(id),actor_id uuid NOT NULL,action text NOT NULL,at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS taxis(id text PRIMARY KEY,driver text NOT NULL,plate text NOT NULL);
CREATE OR REPLACE VIEW dispatcher_queue AS SELECT * FROM bookings WHERE status IN ('Approved','Assigned');
INSERT INTO taxis VALUES ('taxi-01','Eli Mizrahi','12-345-67'),('taxi-02','Yael Ben David','23-456-78'),('taxi-03','Ron Adler','34-567-89') ON CONFLICT DO NOTHING;
