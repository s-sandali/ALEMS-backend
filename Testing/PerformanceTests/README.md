# Performance Testing (JMeter)

## Overview

This directory contains automated load testing scripts using Apache JMeter. Tests run automatically post-deployment in the CD pipeline to validate API performance against NFR targets.

---

## Test Plan: staging-load-test.jmx

**Configuration:**
- **Concurrent Users:** 100
- **Ramp-up Time:** 60 seconds (gradual increase)
- **Test Duration:** 300 seconds (5 minutes)
- **Endpoints Tested:**
  - ✅ `GET /api/health` (no auth, sanity check)
  - ✅ `POST /api/simulation/run` (auth required)
  - ✅ `POST /api/quizzes/{id}/attempts` (auth required)

---

## Setup

### 1. **Export Your JMeter Test Plan**

If you created the test plan in JMeter GUI:
1. Open JMeter GUI: `jmeter` (from `bin/` directory)
2. Load your test plan file
3. **File → Save As** → Save as `staging-load-test.jmx`
4. Place it in this directory

### 2. **CI/CD Integration**

The test plan runs automatically after every deployment to staging via GitHub Actions:

**File:** `.github/workflows/cd.yml`

**Trigger:** Post-deployment health check passes → Run JMeter

**Results uploaded to:** GitHub Artifacts (retained 30 days)

---

## Local Execution

### Run JMeter Test Locally

```bash
# Navigate to JMeter directory
cd /path/to/apache-jmeter-5.6.3/bin

# Run test in non-GUI mode
./jmeter -n \
  -t staging-load-test.jmx \
  -l results.jtl \
  -Jbase_url=https://alems-backend-dsh3f2ggawghatha.centralindia-01.azurewebsites.net \
  -Jclerk_token=YOUR_CLERK_JWT \
  -Jnum_threads=100 \
  -Jramp_up=60 \
  -Jduration=300 \
  -j jmeter.log
```

### Generate HTML Report

```bash
./jmeter -g results.jtl -o report_html
```

Open `report_html/index.html` in browser.

---

## Performance Baselines (NFR Targets)

| Endpoint | Target (ms) | Current |
|----------|------------|---------|
| Health Check | < 50ms | ✓ |
| Simulation Run | < 500ms | ? |
| Quiz Attempt | < 300ms | ? |

---

## CI/CD Pipeline Details

**GitHub Actions Step:** `Run JMeter Load Test (100 Concurrent Users)`

1. **Install JMeter** (Apache 5.6.3)
2. **Run test plan** against deployed staging URL
3. **Capture results** as JTL file
4. **Generate HTML report**
5. **Upload artifacts** (JTL + HTML report + logs)
6. **Fail pipeline** if error rate > 0%

---

## Troubleshooting

### "JMETER_SERVICE_TOKEN not set"
- Add `JMETER_SERVICE_TOKEN` to GitHub Secrets
- Token should be a valid Clerk JWT with user role

### "Connection refused"
- Verify staging URL is correct
- Wait 30 seconds after deployment before testing
- Check Clerk token is not expired

### "415 Unsupported Media Type"
- Verify `Content-Type: application/json` header is set
- Check request body is valid JSON

---

## Next Steps

1. **Export test plan** from your local JMeter GUI
2. **Save as** `staging-load-test.jmx` in this directory
3. **Commit & Push** to trigger CD pipeline
4. **Check GitHub Actions** for test results
