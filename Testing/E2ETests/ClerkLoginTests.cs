using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace E2ETests;

/// <summary>
/// End-to-end tests for the Clerk login flow.
///
/// This test validates that an existing user can log in via the Clerk UI,
/// is correctly authenticated, redirected to the /dashboard, and can see
/// protected content (verifying session persistence and route access).
///
/// Prerequisites:
///   - Frontend (npm run dev) and Backend (dotnet run) must be running.
///   - Test user already exists in Clerk and local database.
///   - The frontend URL and credentials must be configured in testsettings.json.
///
/// NOTE: Chrome runs in VISIBLE (non-headless) mode so you can watch the flow.
/// </summary>
public class ClerkLoginTests : IDisposable
{
    // ─── Configuration ──────────────────────────────────────────────────
    private readonly IConfiguration _config;
    private readonly IWebDriver _driver;
    private readonly string _testEmail;
    private readonly string _testPassword;
    private const int WaitTimeoutSeconds = 30;

    public ClerkLoginTests()
    {
        // Load test settings from testsettings.json
        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("testsettings.json", optional: false)
            .Build();

        // Use the configured test account from testsettings.json
        _testEmail = _config["TestAccount:Email"]
            ?? throw new InvalidOperationException("TestAccount:Email must be configured in testsettings.json");
        _testPassword = _config["TestAccount:Password"]
            ?? throw new InvalidOperationException("TestAccount:Password must be configured in testsettings.json");

        // Configure Chrome in VISIBLE mode so the tester can watch the flow
        var options = new ChromeOptions();
        // NOTE: Headless mode is intentionally disabled
        // Anti-bot evasion to prevent Clerk's "failed security validations" block
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        options.AddUserProfilePreference("credentials_enable_service", false);
        options.AddUserProfilePreference("profile.password_manager_enabled", false);

        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--disable-web-security");
        options.AddArgument("--allow-running-insecure-content");

        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Authenticates an existing user and verifies access to the protected /dashboard.
    /// </summary>
    [Fact]
    public void ClerkLoginFlow_Should_RedirectToDashboard_And_AccessProtectedContent()
    {
        // ─── Arrange ────────────────────────────────────────────────────
        var baseUrl = _config["FrontendBaseUrl"] ?? "http://localhost:5173";
        var loginUrl = $"{baseUrl}/login";
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        // ═══════════════════════════════════════════════════════════════
        // ACT (Phase 1): AUTHENTICATION
        // Navigate to the login page and use Clerk's UI to sign in.
        // ═══════════════════════════════════════════════════════════════

        _driver.Navigate().GoToUrl(loginUrl);

        // Step 1: Wait for and enter the email (identifier-field)
        var emailInput = wait.Until(driver => FindElementSafe(driver,
            By.Id("identifier-field"),
            By.CssSelector(".cl-formFieldInput__identifier"),
            By.CssSelector("input[name='identifier']")));

        Assert.NotNull(emailInput);
        emailInput!.Clear();
        emailInput.SendKeys(_testEmail);

        // Click Continue to proceed to password entry
        var continueButton = wait.Until(driver => FindElementSafe(driver,
            By.CssSelector(".cl-formButtonPrimary"),
            By.CssSelector("button[data-localization-key='formButtonPrimary']")));
        
        Assert.NotNull(continueButton);
        continueButton!.Click();

        // Step 2: Wait for and enter the password
        var passwordInput = wait.Until(driver => FindElementSafe(driver,
            By.Id("password-field"),
            By.CssSelector(".cl-formFieldInput__password"),
            By.CssSelector("input[type='password']")));

        Assert.NotNull(passwordInput);
        passwordInput!.Clear();
        passwordInput.SendKeys(_testPassword);

        // Click Continue to submit the login form
        var submitButton = wait.Until(driver => FindElementSafe(driver,
            By.CssSelector(".cl-formButtonPrimary"),
            By.CssSelector("button[data-localization-key='formButtonPrimary']")));

        Assert.NotNull(submitButton);
        submitButton!.Click();

        // ═══════════════════════════════════════════════════════════════
        // INTERACTIVE DEVICE VERIFICATION (Conditional)
        // If Clerk detects a "new device", it prompts for a 6-digit code.
        // We will briefly wait to see if the code input appears.
        // ═══════════════════════════════════════════════════════════════
        
        bool requiresVerification = false;
        try
        {
            // Use a shorter wait to check for the verification code field
            var shortWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            var verificationInput = shortWait.Until(driver => FindElementSafe(driver,
                By.Id("code-field"),
                By.CssSelector(".cl-formFieldInput__code"),
                By.CssSelector("input[name='code']"),
                By.CssSelector("input[inputmode='numeric']")));

            requiresVerification = (verificationInput != null);
        }
        catch (WebDriverTimeoutException)
        {
            // If the code field doesn't appear within 5 seconds, assume no verification
            // is needed and proceed to checking for the dashboard redirect.
            Console.WriteLine("[E2E Info] No device verification prompt detected, proceeding...");
        }

        if (requiresVerification)
        {
            // ─── Prompt the tester to interact with the browser ─────────
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════╗");
            Console.WriteLine("║         DEVICE VERIFICATION REQUIRED              ║");
            Console.WriteLine("╠════════════════════════════════════════════════════╣");
            Console.WriteLine($"║  Email: {_testEmail,-41} ║");
            Console.WriteLine("║                                                    ║");
            Console.WriteLine("║  >> ACTION REQUIRED <<                             ║");
            Console.WriteLine("║  Please open the Chrome window launched by this    ║");
            Console.WriteLine("║  test and manually enter the 6-digit verification  ║");
            Console.WriteLine("║  code from your email.                             ║");
            Console.WriteLine("║                                                    ║");
            Console.WriteLine("║  Waiting up to 120 seconds for you to proceed...   ║");
            Console.WriteLine("╚════════════════════════════════════════════════════╝");

            // Increase the wait time to 120 seconds to give the user enough time
            // to check their email, get the code, and type it in.
            try
            {
                var longWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(120));
                longWait.Until(driver => driver.Url.Contains("/dashboard"));
                Console.WriteLine("[E2E Info] Verification successful, proceeding...");
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("[E2E Error] Timed out waiting 120 seconds for manual verification and redirect.");
                throw; // Fail the test explicitly
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ASSERT (Phase 2): REDIRECT & SESSION VERIFICATION
        // Wait until actual redirect to /dashboard occurs.
        // This verifies authentication succeeded and routing state updated,
        // confirming session persistence via Clerk.
        // ═══════════════════════════════════════════════════════════════

        wait.Until(driver => driver.Url.Contains("/dashboard"));
        Assert.Contains("/dashboard", _driver.Url);

        // ═══════════════════════════════════════════════════════════════
        // ASSERT (Phase 3): PROTECTED CONTENT VISIBILITY
        // By checking for elements that ONLY render when authenticated
        // and on the dashboard, we confirm end-to-end route protection.
        // ═══════════════════════════════════════════════════════════════

        // Verifies protected route access by checking for a specific dashboard element.
        // In the Dashboard.jsx, there is a span containing "/ Dashboard" and a h1 containing "Welcome back,"
        var dashboardHeader = wait.Until(driver => FindElementSafe(driver,
            By.XPath("//span[contains(text(), '/ Dashboard')]"),
            By.XPath("//h1[contains(text(), 'Welcome back,')]")));
            
        Assert.NotNull(dashboardHeader);
        Assert.True(dashboardHeader!.Displayed, "Protected content (Dashboard heading) was not visible.");
    }

    /// <summary>
    /// Safely attempts to find an element using multiple selectors in order.
    /// Returns the first visible match, or null if none found.
    /// Unlike FindElement, this does NOT throw NoSuchElementException.
    /// </summary>
    private static IWebElement? FindElementSafe(IWebDriver driver, params By[] selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var element = driver.FindElement(selector);
                if (element.Displayed)
                    return element;
            }
            catch (NoSuchElementException)
            {
                // Try the next selector
            }
            catch (StaleElementReferenceException)
            {
                // Element was found but became stale, try next
            }
        }

        return null;
    }

    /// <summary>
    /// Cleanup: Disposes the WebDriver to release browser resources cleanly.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E Cleanup Warning] Failed to dispose WebDriver: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }
}
