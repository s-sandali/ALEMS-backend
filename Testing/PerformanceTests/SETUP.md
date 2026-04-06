# JMeter CI/CD Setup Guide

## Step 1: Add Clerk Token to GitHub Secrets

**Why:** The JMeter test needs a valid JWT token to authenticate against protected endpoints.

### Generate a Test Token (Clerk Service Account)

1. **Open Clerk Dashboard:** https://dashboard.clerk.com
2. **Go to:** Settings → API Keys
3. Create or get a **JWT Template** for testing
4. Generate a **test token** (or use existing user's token)

### Add to GitHub Secrets

1. **Open your GitHub repo** → Settings → Secrets and Variables → Actions
2. **New Repository Secret** → Name: `JMETER_SERVICE_TOKEN`
3. **Value:** Paste your Clerk JWT token
4. **Save**

---

## Step 2: Commit JMeter Test Plan to Repo

**Location:** `Testing/PerformanceTests/staging-load-test.jmx`

### Export from JMeter GUI:

1. Open JMeter: `jmeter`
2. Load your test plan (File → Open Recent)
3. **File → Save As**
4. Save as: `staging-load-test.jmx`
5. Move to: `Testing/PerformanceTests/`

### Git Commands:

```bash
cd ALEMS-backend
git add Testing/PerformanceTests/staging-load-test.jmx
git commit -m "feat: Add JMeter staging load test plan (100 users)"
git push origin main
```

---

## Step 3: Verify CI/CD Pipeline Runs

1. **Push to main branch** (or create PR merged to main)
2. **GitHub Actions** will trigger:
   - Runs CI tests
   - Deploys to staging
   - Runs JMeter load test
   - Generates report

3. **Check Results:**
   - Go to: Actions tab → Latest workflow run
   - Scroll down to: "Run JMeter Load Test" step
   - Check artifacts: `jmeter-load-test-results`
   - Download and review: `jmeter-report.html`

---

## Expected Output

After deployment, JMeter will:

✅ Run 100 concurrent users ramping up over 60 seconds
✅ Hit 3 endpoints for 5 minutes
✅ Export results to `jmeter-results.jtl`
✅ Generate HTML report: `jmeter-report.html`
✅ Upload artifacts to GitHub (retained 30 days)

---

## Sample JMeter Test Execution Output

```
[21:42:05 INF] Using test script: Testing/PerformanceTests/staging-load-test.jmx
[21:42:05 INF] Base URL: https://alems-backend-dsh3f2ggawghatha.centralindia-01.azurewebsites.net
[21:42:05 INF] Clerk Token: Bearer eyJ0eXA...
[21:42:05 INF] Starting test with 100 threads...

Listener = Aggregate Report
  Label                Count  Avg    Min    Max    Err%   Throughput
  Health Check         100    42     25     156    0%     2.5/sec
  Simulation Run       100    280    150    892    0%     1.8/sec
  Quiz Attempt         100    215    90     654    0%     2.1/sec
  
✅ Test completed successfully
```

---

## Performance Baseline (Target NFRs)

Document your performance targets here:

| Endpoint | Concurrent Load | Target Response | Pass/Fail |
|----------|-----------------|-----------------|-----------|
| `/api/health` | 100 users | < 100ms | ✅ |
| `/api/simulation/run` | 100 users | < 500ms | ? |
| `/api/quizzes/{id}/attempts` | 100 users | < 300ms | ? |

---

## Troubleshooting

### Issue: "jq: command not found"
- GitHub Actions installs it automatically
- Local: `sudo apt-get install jq` (Linux) or `brew install jq` (macOS)

### Issue: "jmeter: command not found"
- JMeter is installed in GitHub Actions by the workflow
- Local: Download from https://jmeter.apache.org/

### Issue: "Connection refused"
- Deployment may still be in progress
- GitHub Actions waits 30 seconds, then 10 health check attempts (total 60 sec max)

---

## Next Steps

1. ✅ Add `JMETER_SERVICE_TOKEN` to GitHub Secrets
2. ✅ Export test plan as `staging-load-test.jmx`
3. ✅ Commit to `Testing/PerformanceTests/`
4. 🚀 Push to main → CI/CD runs automatically
