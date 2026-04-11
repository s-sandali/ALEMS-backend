# Dashboard Endpoint & UI Tests

## Overview

This document describes the dashboard endpoint and UI tests added to the ALEMS backend.

### New Dashboard Endpoint

The **Dashboard Controller** (`Controllers/DashboardController.cs`) provides authenticated endpoints for retrieving user analytics and learning progress data:

#### Endpoints

1. **GET `/api/dashboard/summary`**
   - Returns aggregated summary statistics for the current authenticated user
   - Requires JWT authentication
   - Response includes:
     - `userId`: Authenticated user ID
     - `totalQuizzesTaken`: Number of quizzes attempted
     - `totalProblemsSolved`: Count of solved problems
     - `currentXp`: Current experience points
     - `currentStreak`: Active learning streak
     - `lastActivityDate`: Timestamp of most recent activity

   **Example Response:**
   ```json
   {
     "userId": "clerk_user_id",
     "totalQuizzesTaken": 15,
     "totalProblemsSolved": 42,
     "currentXp": 1250,
     "currentStreak": 7,
     "lastActivityDate": "2024-04-10T14:30:00Z"
   }
   ```

2. **GET `/api/dashboard/stats`**
   - Provides detailed performance statistics
   - Requires JWT authentication
   - Response includes:
     - `userId`: Authenticated user ID
     - `averageQuizScore`: Average score across all quizzes
     - `problemAccuracy`: Accuracy percentage for solved problems
     - `algorithmsMastered`: List of mastered algorithms
     - `topicsProgress`: Dictionary of topic progress percentages

3. **GET `/api/dashboard/recent-activity`**
   - Returns recent user activities (quizzes, problems, simulations)
   - Requires JWT authentication
   - Returns array of activity items with:
     - `activityId`: Unique activity identifier
     - `activityType`: Type of activity ("quiz", "problem", "simulation")
     - `title`: Activity title
     - `completedDate`: Completion timestamp
     - `score`: Optional score value
     - `passed`: Boolean indicating success status

### UI Tests

The **DashboardUITests** (`Testing/E2ETests/DashboardUITests.cs`) provides comprehensive browser automation tests using Selenium WebDriver:

#### Test Cases

1. **TestDashboardPageLoads**
   - Validates dashboard page loads successfully for authenticated users
   - Checks for dashboard header visibility

2. **TestDashboardSummarySection**
   - Verifies summary statistics cards render correctly
   - Validates presence of quiz and XP metrics

3. **TestDashboardSummaryEndpoint**
   - Tests `/api/dashboard/summary` endpoint response
   - Validates no unauthorized errors display

4. **TestDashboardStatsSection**
   - Checks performance statistics section accessibility
   - Looks for score, accuracy, and performance elements

5. **TestDashboardRecentActivitySection**
   - Validates recent activity feed section
   - Checks for activity history rendering

6. **TestDashboardErrorHandling**
   - Tests error handling and graceful degradation
   - Validates page remains responsive on errors

7. **TestDashboardNavigation**
   - Validates navigation between dashboard sections
   - Checks for navigation UI elements

## Running Tests Locally

### Prerequisites

1. Ensure backend is running:
   ```bash
   cd ALEMS-backend
   dotnet run
   ```

2. Ensure frontend is running (required for UI tests):
   ```bash
   npm run dev  # or your frontend start command
   # Should be accessible at http://localhost:5173
   ```

3. Ensure MySQL is running with proper credentials

### Running Dashboard Unit/Integration Tests

These tests validate the controller logic without browser automation:

```bash
# Run only dashboard-related integration tests
dotnet test Testing/IntegrationTests/IntegrationTests.csproj \
  --filter "FullyQualifiedName~Dashboard"

# Run all tests with coverage
dotnet test Testing/IntegrationTests/IntegrationTests.csproj \
  --collect:"XPlat Code Coverage"
```

### Running Dashboard UI Tests

These tests require Chrome/Chromium browser:

```bash
cd Testing/E2ETests

# Configure test settings
# Edit testsettings.json with your test credentials

# Run only dashboard UI tests
dotnet test E2ETests.csproj \
  --filter "FullyQualifiedName~DashboardUITests" \
  --logger "console;verbosity=detailed"

# Run with Chrome in headless mode
# Update DashboardUITests.cs to use headless:
# var options = new ChromeOptions() { PageLoadStrategy = PageLoadStrategy.Normal };
# options.AddArgument("--headless");
```

### Configuring testsettings.json

Create `Testing/E2ETests/testsettings.json`:

```json
{
  "MySqlConnectionString": "Server=localhost;Port=3306;Database=bigodb;User Id=root;Password=root;",
  "TestAccount": {
    "Email": "test@example.com",
    "Password": "TestPassword123!"
  },
  "FrontendUrl": "http://localhost:5173",
  "BackendUrl": "http://localhost:5000"
}
```

## CI/CD Pipeline Integration

### Main CI Pipeline (`ci.yml`)

The main CI pipeline now includes a step to run E2E/UI tests:

```yaml
- name: Run E2E/UI Tests (Dashboard)
  run: dotnet test Testing/E2ETests/E2ETests.csproj --configuration Release --no-build --logger:"console;verbosity=normal" || true
  continue-on-error: true
```

**Note:** This runs as a non-blocking step (`continue-on-error: true`) since UI tests may require browser availability.

### Dedicated Dashboard UI Tests Workflow (`dashboard-ui-tests.yml`)

A separate GitHub Actions workflow (`dashboard-ui-tests.yml`) is triggered on:
- Pull requests to `develop`, `main`, `QA`, `devops` branches
- Direct pushes to those branches
- When dashboard-related files are modified

**Key Features:**
- Runs on Ubuntu with MySQL service
- Installs Chrome and ChromeDriver
- Starts backend in background
- Automatically generates test reports
- Uploads test artifacts for 30 days

## Architecture Notes

### Security
- All dashboard endpoints require JWT authentication (`[Authorize]` attribute)
- Endpoints extract user ID from JWT claims (`User.FindFirst("sub")`)
- No sensitive data is exposed without proper authentication

### Extensibility
- DTOs (`DashboardSummaryDto`, `DashboardStatsDto`, `DashboardActivityDto`) are ready for extension
- Backend currently returns placeholder data; integrate with actual repositories as needed
- UI tests can be expanded with additional scenarios

### Performance Considerations
- Dashboard endpoints should be optimized with caching (Redis) if returning large datasets
- Consider pagination for recent activity endpoint
- Implement rate limiting if dashboard is heavily accessed
- Add database indexes on user activity queries

## Next Steps

1. **Integrate Actual Data:**
   - Connect `DashboardController` to `UserRepository`, `QuizAttemptRepository`, etc.
   - Implement actual statistics calculations
   - Add caching layer for expensive queries

2. **Enhance UI Tests:**
   - Add data-driven tests with multiple scenarios
   - Implement performance/load testing
   - Add visual regression testing

3. **Monitor Dashboard Performance:**
   - Add APM (Application Insights) instrumentation
   - Log dashboard query execution times
   - Track endpoint response times in production

## Troubleshooting

### UI Tests Hang or Timeout

**Solution:** 
- Ensure frontend and backend are running
- Check Firebase/Clerk authentication is properly configured
- Verify testsettings.json has correct credentials
- Increase timeout values in DashboardUITests.cs if needed

### Chrome/ChromeDriver Issues

**Solution:**
- Install system chromium-browser: `sudo apt-get install chromium-browser`
- Ensure ChromeDriver version matches Chrome version
- Run with explicit ChromeDriver path if not in PATH

### Database Connection Errors

**Solution:**
- Verify MySQL is running
- Check connection string in testsettings.json
- Ensure database migrations are applied

## Related Files

- **Controller:** `Controllers/DashboardController.cs`
- **UI Tests:** `Testing/E2ETests/DashboardUITests.cs`
- **Main CI Pipeline:** `.github/workflows/ci.yml`
- **Dashboard UI Tests Workflow:** `.github/workflows/dashboard-ui-tests.yml`
