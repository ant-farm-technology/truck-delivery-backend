/**
 * Scenario 1 — Order Creation Load Test
 *
 * Goal    : 100 concurrent users sustained for 10 minutes, creating orders
 * Target  : p95 latency < 2s, error rate < 5%
 * Services: Gateway → Order Service → Kafka → (Shipment saga starts async)
 *
 * Run:
 *   k6 run --env GATEWAY_URL=http://localhost:8080 tests/LoadTests/k6/01-order-creation-load.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { registerAndLogin, bearerHeaders } from './lib/auth.js';
import { createOrderPayload } from './lib/data.js';

const BASE_URL = __ENV.GATEWAY_URL || 'http://localhost:8080';

// ── Custom metrics ────────────────────────────────────────────────────────────
const errorRate = new Rate('order_errors');
const orderLatency = new Trend('order_create_latency', true);
const ordersCreated = new Counter('orders_created_total');
const cancelLatency = new Trend('order_cancel_latency', true);

// ── Test configuration ────────────────────────────────────────────────────────
export const options = {
  scenarios: {
    order_creation: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '2m', target: 100 },  // ramp up to 100 VUs over 2 min
        { duration: '6m', target: 100 },  // sustain 100 VUs for 6 min
        { duration: '2m', target: 0 },    // ramp down over 2 min
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    // p95 under 2s — from golden signals rule in testing rules
    order_create_latency: ['p(95)<2000'],
    // error rate under 5%
    order_errors: ['rate<0.05'],
    // p99 under 5s
    http_req_duration: ['p(99)<5000'],
  },
};

// ── Per-VU setup: register + login once per VU ─────────────────────────────
export function setup() {
  // Pre-register 1 admin to seed optimizer stub awareness (noop in real env)
  // Each VU will register its own customer on first iteration
  return {};
}

// VU-local state (not shared between VUs)
let _token = null;
let _userId = null;

export default function () {
  // Lazy auth: each VU registers once, then reuses token across iterations
  if (!_token) {
    const creds = registerAndLogin(`customer_load`, 1); // role=1 Customer
    _token = creds.accessToken;
    _userId = creds.userId;
  }

  const headers = bearerHeaders(_token);

  // ── Create order ──────────────────────────────────────────────────────────
  const createStart = Date.now();
  const createResp = http.post(
    `${BASE_URL}/api/v1/orders`,
    JSON.stringify(createOrderPayload()),
    { headers },
  );
  orderLatency.add(Date.now() - createStart);

  const createOk = check(createResp, {
    'create order 201': (r) => r.status === 201,
    'has orderId': (r) => {
      try {
        const body = JSON.parse(r.body);
        const data = body.data ?? body;
        return !!data.orderId;
      } catch {
        return false;
      }
    },
  });

  errorRate.add(!createOk);

  if (createOk) {
    ordersCreated.add(1);

    const body = JSON.parse(createResp.body);
    const data = body.data ?? body;
    const orderId = data.orderId;

    // ── Get order (read path) ───────────────────────────────────────────────
    const getResp = http.get(`${BASE_URL}/api/v1/orders/${orderId}`, { headers });
    check(getResp, { 'get order 200': (r) => r.status === 200 });

    // ── Occasionally cancel the order (~20% of time) ────────────────────────
    if (Math.random() < 0.2) {
      const cancelStart = Date.now();
      const cancelResp = http.del(`${BASE_URL}/api/v1/orders/${orderId}`, null, { headers });
      cancelLatency.add(Date.now() - cancelStart);
      check(cancelResp, { 'cancel order 204': (r) => r.status === 204 });
    }
  }

  // Think time: 1–3s between iterations (realistic user pacing)
  sleep(1 + Math.random() * 2);
}

export function handleSummary(data) {
  return {
    'tests/LoadTests/results/01-order-creation-summary.json': JSON.stringify(data, null, 2),
  };
}
