/**
 * ElshazlyStore — k6 load test harness.
 *
 * Install k6: https://k6.io/docs/get-started/installation/
 *
 * Usage:
 *   k6 run perf/load-test.js                            # quick smoke
 *   k6 run --vus 20 --duration 60s perf/load-test.js    # sustained load
 *
 * Environment variables (override via -e):
 *   BASE_URL   — API base (default http://localhost:5000)
 *   USERNAME   — login username (default admin)
 *   PASSWORD   — login password (default Admin@123!)
 */

import http from "k6/http";
import { check, group, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

// ── Custom metrics ─────────────────────────────────────────
const errorRate = new Rate("errors");
const barcodeLookupDuration = new Trend("barcode_lookup_ms", true);
const productListDuration = new Trend("product_list_ms", true);

// ── Options ────────────────────────────────────────────────
export const options = {
  scenarios: {
    smoke: {
      executor: "constant-vus",
      vus: 5,
      duration: "30s",
    },
  },
  thresholds: {
    http_req_duration: ["p(95)<500"],   // 95th percentile < 500 ms
    errors: ["rate<0.05"],              // < 5 % error rate
  },
};

// ── Config ─────────────────────────────────────────────────
const BASE = __ENV.BASE_URL || "http://localhost:5000";
const USERNAME = __ENV.USERNAME || "admin";
const PASSWORD = __ENV.PASSWORD || "Admin@123!";

// ── Setup: login once, share token across VUs ──────────────
export function setup() {
  const res = http.post(
    `${BASE}/api/v1/auth/login`,
    JSON.stringify({ username: USERNAME, password: PASSWORD }),
    { headers: { "Content-Type": "application/json" } }
  );

  check(res, { "login 200": (r) => r.status === 200 });

  if (res.status !== 200) {
    console.error("Login failed — aborting setup");
    return { token: "" };
  }

  const body = JSON.parse(res.body);
  return { token: body.accessToken };
}

// ── Default function: each VU iteration ────────────────────
export default function (data) {
  const headers = {
    Authorization: `Bearer ${data.token}`,
    "Content-Type": "application/json",
  };

  group("Health", () => {
    const res = http.get(`${BASE}/api/v1/health`);
    check(res, { "health 200": (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group("Product list (paged)", () => {
    const res = http.get(`${BASE}/api/v1/products?page=1&pageSize=25`, {
      headers,
    });
    check(res, { "products 200": (r) => r.status === 200 });
    productListDuration.add(res.timings.duration);
    errorRate.add(res.status !== 200);
  });

  group("Product search", () => {
    const res = http.get(
      `${BASE}/api/v1/products?q=shirt&page=1&pageSize=10`,
      { headers }
    );
    check(res, { "search 200": (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group("Stock balances", () => {
    const res = http.get(`${BASE}/api/v1/stock/balances?page=1&pageSize=25`, {
      headers,
    });
    check(res, { "stock 200": (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group("Barcode lookup", () => {
    // Use a deliberately unknown barcode to exercise the index scan path
    const res = http.get(`${BASE}/api/v1/barcodes/0000000000000`, { headers });
    // 404 is expected for unknown barcode — not an error
    check(res, { "barcode !5xx": (r) => r.status < 500 });
    barcodeLookupDuration.add(res.timings.duration);
    errorRate.add(res.status >= 500);
  });

  group("Customers list", () => {
    const res = http.get(`${BASE}/api/v1/customers?page=1&pageSize=25`, {
      headers,
    });
    check(res, { "customers 200": (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group("Dashboard summary", () => {
    const res = http.get(`${BASE}/api/v1/dashboard/summary`, { headers });
    check(res, { "dashboard !5xx": (r) => r.status < 500 });
    errorRate.add(res.status >= 500);
  });

  sleep(0.5); // pacing between iterations
}
