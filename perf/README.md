# ElshazlyStore — Performance Test Harness

Uses [k6](https://k6.io/) for HTTP load testing.

## Prerequisites

Install k6: <https://k6.io/docs/get-started/installation/>

```bash
# macOS
brew install k6

# Windows (scoop)
scoop install k6

# Docker
docker pull grafana/k6
```

## Quick Start

```bash
# 1. Start the API (ensure DB is migrated + seeded)
dotnet run --project src/ElshazlyStore.Api

# 2. Smoke test (5 VUs, 30 s)
k6 run perf/load-test.js

# 3. Sustained load
k6 run --vus 20 --duration 60s perf/load-test.js

# 4. Custom base URL
k6 run -e BASE_URL=http://192.168.1.100:5000 perf/load-test.js
```

## Environment Variables

| Variable   | Default                | Description             |
|------------|------------------------|-------------------------|
| `BASE_URL` | `http://localhost:5000`| API base URL            |
| `USERNAME` | `admin`                | Login username           |
| `PASSWORD` | `Admin@123!`           | Login password           |

## Thresholds

| Metric              | Target         |
|---------------------|----------------|
| `http_req_duration` | p95 < 500 ms   |
| `errors`            | rate < 5 %     |

## Scenarios Covered

1. **Health** — `/api/v1/health` (unauthenticated)
2. **Product list** — paged listing with default sort
3. **Product search** — `q=shirt` trigram-backed search
4. **Stock balances** — inventory readout
5. **Barcode lookup** — POS hot path (cached)
6. **Customers list** — master data listing
7. **Dashboard summary** — aggregation query

## Interpreting Results

k6 prints a summary table. Key rows:

- **http_req_duration** — round-trip time percentiles
- **barcode_lookup_ms** — custom metric for POS barcode path
- **product_list_ms** — custom metric for product list
- **errors** — percentage of non-success responses
