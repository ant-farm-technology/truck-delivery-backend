# Load Tests — k6

Three scenarios matching the project testing rules.

## Prerequisites

```bash
# Install k6
brew install k6          # macOS
# or: https://k6.io/docs/get-started/installation/
```

## Scenarios

| Script | VUs | Duration | Target |
|---|---|---|---|
| `01-order-creation-load.js` | 100 | 10 min | p95 < 2s, errors < 5% |
| `02-tracking-spike.js` | 500–10k | 5 min | ≥ 1k updates/sec |
| `03-kafka-throughput.js` | 1200 combined | 5 min | consumer lag < 10k |

## Running

```bash
# Start services first
docker compose up -d

# Scenario 1 — Order creation (100 VUs, 10 min)
k6 run --env GATEWAY_URL=http://localhost:8080 \
  tests/LoadTests/k6/01-order-creation-load.js

# Scenario 2 — Tracking spike (default 500 VUs; set PEAK_VUS=10000 for full test)
k6 run --env GATEWAY_URL=http://localhost:8080 \
       --env PEAK_VUS=500 \
  tests/LoadTests/k6/02-tracking-spike.js

# Scenario 3 — Kafka throughput (200 order VUs + 1000 tracking VUs)
k6 run --env GATEWAY_URL=http://localhost:8080 \
       --env PROMETHEUS_URL=http://localhost:9090 \
  tests/LoadTests/k6/03-kafka-throughput.js
```

## Results

JSON summaries are written to `tests/LoadTests/results/` after each run.

## Thresholds (fail conditions)

| Metric | Threshold | Source |
|---|---|---|
| `order_create_latency` p95 | < 2s | Golden signals — p95 sync APIs |
| `order_errors` | < 5% | Golden signals — error rate |
| `location_update_latency` p95 | < 500ms | Tracking write path (Redis) |
| `location_updates_total` | > 300k in 5min | 1k/sec minimum (alert level) |
| `kafka_consumer_lag` | < 10k messages | Golden signals — Kafka lag alert |
