/**
 * Scenario 2 — Tracking GPS Location Spike Test
 *
 * Goal    : 10,000 location updates/second for 5 minutes
 * Target  : ingestion rate ≥ 1k events/sec at p95 < 500ms
 *           (alert fires below 1k/sec — see golden signals in testing rules)
 * Services: Gateway → Tracking Service → Redis (GPS cache) → Kafka → SignalR broadcast
 *
 * Architecture note: Each "driver" VU pushes GPS every 1s (realistic 1–5s interval).
 * At 10k VUs × 1 req/s = 10k updates/sec sustained.
 *
 * Run:
 *   k6 run --env GATEWAY_URL=http://localhost:8080 tests/LoadTests/k6/02-tracking-spike.js
 *
 * Pre-requisite: Drivers must exist with active shipments. This test uses pre-provisioned
 * driver tokens generated offline (see scripts/seed-load-drivers.sh) or falls back to
 * dynamic registration when SEED_TOKENS env is not set.
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';
import { registerAndLogin, bearerHeaders } from './lib/auth.js';

const BASE_URL = __ENV.GATEWAY_URL || 'http://localhost:8080';

// How many VUs to use — default 500 (realistic pre-warm), override to 10000 in full run
const PEAK_VUS = parseInt(__ENV.PEAK_VUS || '500');

// ── Custom metrics ────────────────────────────────────────────────────────────
const locationErrorRate = new Rate('location_update_errors');
const locationLatency = new Trend('location_update_latency', true);
const updatesPerSec = new Counter('location_updates_total');
const redisHitRate = new Rate('redis_cache_hit'); // inferred from fast response

// ── Test configuration ────────────────────────────────────────────────────────
export const options = {
  scenarios: {
    // Phase 1: ramp up to peak within 30s
    ramp_up: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: PEAK_VUS },
        { duration: '4m30s', target: PEAK_VUS },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    // p95 under 500ms for GPS ingestion (write path to Redis + Kafka publish)
    location_update_latency: ['p(95)<500', 'p(99)<1000'],
    // error rate under 1% (tracking must be highly reliable)
    location_update_errors: ['rate<0.01'],
    // sustained throughput: at least 1k updates/sec measured via total count
    // at 5 min = 300s → minimum 300_000 updates for 1k/sec average
    location_updates_total: ['count>300000'],
  },
};

// VU-local state
let _driverToken = null;
let _driverId = null;
let _shipmentId = null; // may be null if no active shipment; tracking still accepts updates

export default function () {
  if (!_driverToken) {
    const creds = registerAndLogin(`driver_spike`, 2); // role=2 Driver
    _driverToken = creds.accessToken;
    _driverId = creds.userId;
    // In a real pre-provisioned scenario, shipmentId would be loaded from seed data
    // Here we send without shipmentId (tracking service should accept GPS-only updates)
    _shipmentId = __ENV.SHIPMENT_ID || null;
  }

  const headers = bearerHeaders(_driverToken);

  const payload = {
    driverId: _driverId,
    latitude: 10.7769 + (Math.random() - 0.5) * 0.5,
    longitude: 106.7009 + (Math.random() - 0.5) * 0.5,
    speedKmh: Math.random() * 80,
    heading: Math.random() * 360,
    accuracy: 5 + Math.random() * 10,
    timestamp: new Date().toISOString(),
  };

  if (_shipmentId) {
    payload.shipmentId = _shipmentId;
  }

  const start = Date.now();
  const resp = http.post(
    `${BASE_URL}/api/v1/tracking/location`,
    JSON.stringify(payload),
    { headers, timeout: '2s' },
  );
  const elapsed = Date.now() - start;

  locationLatency.add(elapsed);
  updatesPerSec.add(1);

  const ok = check(resp, {
    'location update 2xx': (r) => r.status >= 200 && r.status < 300,
    'under 500ms': (r) => elapsed < 500,
  });

  locationErrorRate.add(!ok);

  // Fast response (<50ms) likely means Redis write only, not full Kafka round-trip
  redisHitRate.add(elapsed < 50);

  // 1s between GPS pushes per driver (realistic interval)
  sleep(1);
}

export function handleSummary(data) {
  const updates = data.metrics.location_updates_total?.values?.count ?? 0;
  const duration = 300; // seconds
  const throughput = (updates / duration).toFixed(0);

  console.log(`\n📍 Tracking Throughput: ${throughput} updates/sec (${updates} total in ${duration}s)`);
  console.log(`   p95 latency: ${data.metrics.location_update_latency?.values?.['p(95)']?.toFixed(0)}ms`);
  console.log(`   Error rate: ${(data.metrics.location_update_errors?.values?.rate * 100)?.toFixed(2)}%`);

  return {
    'tests/LoadTests/results/02-tracking-spike-summary.json': JSON.stringify(data, null, 2),
  };
}
