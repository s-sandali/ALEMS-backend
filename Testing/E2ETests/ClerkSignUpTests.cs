using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace E2ETests;

/// <summary>
/// End-to-end tests for the Clerk authentication flow.
///
/// This test performs a full sign-up → email verification → dashboard redirect
/// → database verification flow. Because Clerk requires email verification,
/// the test pauses and prompts the tester to enter the verification code
/// received via email.
///
/// Prerequisites:
///   - Frontend (npm run dev) and Backend (dotnet run) must be running.
///   - The MySQL connection string must be configured in testsettings.json.
///
/// NOTE: Chrome runs in VISIBLE (non-headless) mode so you can watch the flow.
///       The test will pause in the terminal waiting for you to enter the
///       verification code from your email.
/// </summary>
public class ClerkSignUpTests : IDisposable
{
    // ─── Configuration ──────────────────────────────────────────────────
    private readonly IConfiguration _config;
    private readonly IWebDriver _driver;
    private readonly string _testEmail;
    private readonly string _testPassword;
    private const int WaitTimeoutSeconds = 30;

    public ClerkSignUpTests()
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
        // NOTE: Headless mode is intentionally disabled so you can observe
        //       the Clerk UI and enter the verification code.
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
    /// Full end-to-end test:
    ///   1. Navigate to the sign-up page and fill out the Clerk form
    ///   2. Submit the form → verification code screen appears
    ///   3. Prompt tester to enter the verification code from their email
    ///   4. Enter the code and complete sign-up
    ///   5. Wait for redirect to /dashboard
    ///   6. Verify user record exists in MySQL with correct defaults
    /// </summary>
    [Fact]
    public async Task ClerkSignUpFlow_Should_CreateUser_And_RedirectToDashboard()
    {
        // ─── Arrange ────────────────────────────────────────────────────
        var baseUrl = _config["FrontendBaseUrl"] ?? "http://localhost:5173";
        var signUpUrl = $"{baseUrl}/register";
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        // ═══════════════════════════════════════════════════════════════
        // PHASE 1: SIGN-UP FORM
        // Fill in the Clerk sign-up form and submit it.
        // ═══════════════════════════════════════════════════════════════

        _driver.Navigate().GoToUrl(signUpUrl);

        // Wait for the email field to confirm the form has loaded
        var emailInput = wait.Until(driver => FindElementSafe(driver,
            By.Id("emailAddress-field"),
            By.CssSelector(".cl-formFieldInput__emailAddress"),
            By.CssSelector("input[type='email']")));

        Assert.NotNull(emailInput);

        // Optionally fill in First Name (may not be present in all configurations)
        var firstNameInput = FindElementSafe(_driver,
            By.Id("firstName-field"),
            By.CssSelector(".cl-formFieldInput__firstName"),
            By.CssSelector("input[name='firstName']"));

        if (firstNameInput != null)
        {
            firstNameInput.Clear();
            firstNameInput.SendKeys("Test");
        }

        // Optionally fill in Last Name
        var lastNameInput = FindElementSafe(_driver,
            By.Id("lastName-field"),
            By.CssSelector(".cl-formFieldInput__lastName"));

        if (lastNameInput != null)
        {
            lastNameInput.Clear();
            lastNameInput.SendKeys("User");
        }

        // Enter the email address
        emailInput!.Clear();
        emailInput.SendKeys(_testEmail);

        // Enter the password
        var passwordInput = wait.Until(driver => FindElementSafe(driver,
            By.Id("password-field"),
            By.CssSelector(".cl-formFieldInput__password"),
            By.CssSelector("input[type='password']")));

        Assert.NotNull(passwordInput);
        passwordInput!.Clear();
        passwordInput.SendKeys(_testPassword);

        // Submit the sign-up form
        var submitButton = wait.Until(driver => FindElementSafe(driver,
            By.CssSelector(".cl-formButtonPrimary"),
            By.CssSelector("button[data-localization-key='formButtonPrimary']")));

        Assert.NotNull(submitButton);
        submitButton!.Click();

        // ═══════════════════════════════════════════════════════════════
        // PHASE 2: EMAIL VERIFICATION (Interactive)
        // Wait for the verification code screen to appear, then prompt
        // the tester to enter the code they received via email.
        // ═══════════════════════════════════════════════════════════════

        // Wait for the verification code input to appear
        var verificationInput = wait.Until(driver => FindElementSafe(driver,
            By.Id("code-field"),
            By.CssSelector(".cl-formFieldInput__code"),
            By.CssSelector("input[name='code']"),
            By.CssSelector("input[inputmode='numeric']")));

        Assert.NotNull(verificationInput);

        // ─── Prompt the tester for the verification code ─────────────
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════╗");
        Console.WriteLine("║         VERIFICATION CODE REQUIRED                ║");
        Console.WriteLine("╠════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Email: {_testEmail,-41} ║");
        Console.WriteLine("║                                                    ║");
        Console.WriteLine("║  Check your email for the Clerk verification code  ║");
        Console.WriteLine("║  and enter it below.                               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════╝");
        Console.Write(">>> Enter verification code: ");

        var verificationCode = Console.ReadLine()?.Trim();
        Assert.False(string.IsNullOrEmpty(verificationCode),
            "Verification code cannot be empty.");

        // Enter the verification code
        verificationInput!.Clear();
        verificationInput.SendKeys(verificationCode);

        // Click the verify / continue button
        var verifyButton = wait.Until(driver => FindElementSafe(driver,
            By.CssSelector(".cl-formButtonPrimary"),
            By.CssSelector("button[data-localization-key='formButtonPrimary']")));

        Assert.NotNull(verifyButton);
        verifyButton!.Click();

        // ═══════════════════════════════════════════════════════════════
        // PHASE 3: DASHBOARD REDIRECT
        // After successful verification, Clerk completes the sign-up
        // and the app should redirect to /dashboard.
        // ═══════════════════════════════════════════════════════════════

        wait.Until(driver => driver.Url.Contains("/dashboard"));
        Assert.Contains("/dashboard", _driver.Url);

        // ═══════════════════════════════════════════════════════════════
        // PHASE 4: DATABASE VERIFICATION
        // After sign-up, the frontend calls POST /api/users/sync which
        // upserts the user record in the Users table.
        // ═══════════════════════════════════════════════════════════════

        // Allow time for the backend sync to complete
        await Task.Delay(3000);

        var connectionString = _config.GetConnectionString("DefaultConnection");
        Assert.False(string.IsNullOrEmpty(connectionString),
            "MySQL connection string must be configured in testsettings.json");

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        // Query the Users table for the test email
        const string sql = @"
            SELECT Email, Role, IsActive
            FROM Users
            WHERE Email = @Email
            LIMIT 1;";

        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Email", _testEmail);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        // Assert: User record must exist in the database
        Assert.True(await reader.ReadAsync(),
            $"Expected user with email '{_testEmail}' to exist in the Users table.");

        // Assert: Default role should be "Student" as defined in the User model
        var role = reader.GetString("Role");
        Assert.Equal("Student", role);

        // Assert: New users should be active by default (IsActive = true)
        var isActive = reader.GetBoolean("IsActive");
        Assert.True(isActive, "Newly registered user should have IsActive = true.");
    }

    /// <summary>
    /// Safely attempts to find an element using multiple selectors in order.
    /// Returns the first visible match, or null if none found.
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
    /// Cleanup: Dispose the WebDriver and remove the test user from the database.
    /// </summary>
    public void Dispose()
    {
        // Clean up the test user from the database
        try
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();

                const string deleteSql = "DELETE FROM Users WHERE Email = @Email;";
                using var cmd = new MySqlCommand(deleteSql, connection);
                cmd.Parameters.AddWithValue("@Email", _testEmail);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E Cleanup Warning] Failed to delete test user '{_testEmail}': {ex.Message}");
        }

        // Dispose the WebDriver
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
