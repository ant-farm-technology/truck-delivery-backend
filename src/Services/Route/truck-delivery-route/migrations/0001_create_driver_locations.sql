CREATE EXTENSION IF NOT EXISTS postgis;

CREATE TABLE IF NOT EXISTS driver_locations (
    driver_id     UUID        PRIMARY KEY,
    location      GEOMETRY(Point, 4326) NOT NULL,
    status        VARCHAR(20) NOT NULL DEFAULT 'Offline',
    max_weight_kg DECIMAL(10, 3) NOT NULL DEFAULT 0,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Spatial index for ST_DWithin / ST_DistanceSphere queries.
CREATE INDEX IF NOT EXISTS idx_driver_locations_gist
    ON driver_locations USING GIST (location);

-- Index for filtering Available drivers quickly.
CREATE INDEX IF NOT EXISTS idx_driver_locations_status
    ON driver_locations (status);
