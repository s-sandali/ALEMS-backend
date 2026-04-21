# Performance Tests (JMeter)

This folder contains backend load tests for admin endpoints:

- `GET /api/admin/reports?format=csv`
- `GET /api/admin/reports?format=pdf`
- `GET /api/admin/stats`
- `GET /api/admin/leaderboard`

## NFR target

NFR-P03 target enforced in CI:

- p95 latency `< 5000 ms`
- zero failed requests
- dataset seeded to `1000` quiz-attempt records before test execution

## Local run

```bash
jmeter -n \
  -t Testing/PerformanceTests/staging-load-test.jmx \
  -l Testing/PerformanceTests/results/results.jtl \
  -e -o Testing/PerformanceTests/results/dashboard \
  -JBASE_URL=http://localhost:5181 \
  -JADMIN_JWT=<ADMIN_JWT> \
  -JSTART_DATE=2026-04-01 \
  -JEND_DATE=2026-04-30 \
  -JUSERS=20 \
  -JRAMP_UP=20 \
  -JLOOPS=10
```

Then summarize and enforce NFR threshold:

```bash
python3 Testing/PerformanceTests/scripts/summarize_jmeter_results.py \
  --input Testing/PerformanceTests/results/results.jtl \
  --output Testing/PerformanceTests/results/summary.md \
  --threshold-ms 5000
```
