-- Road network tables for A* pathfinding.
-- Populated by an OSM import job (osm2pgsql or similar tool).

CREATE TABLE IF NOT EXISTS nodes (
    id      BIGINT PRIMARY KEY,
    geom    GEOMETRY(Point, 4326) NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_nodes_geom ON nodes USING GIST (geom);

CREATE TABLE IF NOT EXISTS roads (
    id            BIGINT PRIMARY KEY,
    geom          GEOMETRY(LineString, 4326) NOT NULL,
    length_m      DOUBLE PRECISION NOT NULL,
    speed_kmh     INTEGER NOT NULL DEFAULT 50,
    highway_type  VARCHAR(50)
);

CREATE INDEX IF NOT EXISTS idx_roads_geom ON roads USING GIST (geom);

CREATE TABLE IF NOT EXISTS edges (
    from_node  BIGINT NOT NULL REFERENCES nodes(id),
    to_node    BIGINT NOT NULL REFERENCES nodes(id),
    road_id    BIGINT REFERENCES roads(id),
    weight_m   DOUBLE PRECISION NOT NULL,
    weight_s   DOUBLE PRECISION NOT NULL,
    PRIMARY KEY (from_node, to_node)
);

CREATE INDEX IF NOT EXISTS idx_edges_from ON edges (from_node);
CREATE INDEX IF NOT EXISTS idx_edges_to   ON edges (to_node);
