using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace E2ETests;

public class VisualizationFlowTests : IDisposable
{
    private readonly IConfiguration _config;
    private readonly IWebDriver _driver;
    private readonly string _testEmail;
    private readonly string _testPassword;
    private const int WaitTimeoutSeconds = 35;

    public VisualizationFlowTests()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("testsettings.json", optional: false)
            .Build();

        _testEmail = _config["TestAccount:Email"]
            ?? throw new InvalidOperationException("TestAccount:Email must be configured in testsettings.json");
        _testPassword = _config["TestAccount:Password"]
            ?? throw new InvalidOperationException("TestAccount:Password must be configured in testsettings.json");

        var options = new ChromeOptions();
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

    [Fact]
    public void E2E_VIS_01_BubbleSort_FullFlow_CodeHighlight_ChangesEachStep()
    {
        var wait = CreateWait();

        EnsureAuthenticatedAndOpenAlgorithms(wait);
        OpenAlgorithmDetail(wait, "Bubble Sort");
        EnsureVisualizationLoaded(wait);

        var initialHighlight = GetCodeHighlightSignature(wait);
        var changedCount = 0;

        for (var i = 0; i < 4; i++)
        {
            ClickStepForward(wait);
            WaitForStepAdvance(wait);

            var nextHighlight = GetCodeHighlightSignature(wait);
            if (!string.Equals(initialHighlight, nextHighlight, StringComparison.Ordinal))
            {
                changedCount++;
                initialHighlight = nextHighlight;
            }
        }

        Assert.True(changedCount >= 2, "Expected active code highlight to change across forward steps.");
    }

    [Fact]
    public void E2E_VIS_02_BinarySearch_TargetFound_FinalStep_ShowsFoundState()
    {
        var wait = CreateWait();

        EnsureAuthenticatedAndOpenAlgorithms(wait);
        OpenAlgorithmDetail(wait, "Binary Search");
        EnsureVisualizationLoaded(wait);

        StepUntilFinished(wait, maxSteps: 80);

        var foundBadge = wait.Until(driver => FindElementSafe(driver,
            By.XPath("//span[contains(normalize-space(.), 'Found')]"),
            By.XPath("//*[contains(normalize-space(.), 'Search complete')]"),
            By.XPath("//*[contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), 'found')]")
        ));

        Assert.NotNull(foundBadge);
        Assert.True(foundBadge!.Displayed, "Expected a visible found-state marker on final Binary Search step.");
    }

    [Fact]
    public void E2E_VIS_03_BubbleSort_LastStep_Replay_ResetsToStepOne_WithOriginalArray()
    {
        var wait = CreateWait();

        EnsureAuthenticatedAndOpenAlgorithms(wait);
        OpenAlgorithmDetail(wait, "Bubble Sort");
        EnsureVisualizationLoaded(wait);

        var originalArraySignature = GetArraySignature(wait);

        StepUntilFinished(wait, maxSteps: 120);
        ClickReplayOrReset(wait);

        wait.Until(_ => ReadStepProgress().current == 1);
        var replayedArraySignature = GetArraySignature(wait);

        Assert.Equal(originalArraySignature, replayedArraySignature);
    }

    private WebDriverWait CreateWait() => new(_driver, TimeSpan.FromSeconds(WaitTimeoutSeconds));

    private string ResolveBaseUrl()
    {
        var configured = _config["FrontendBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        // Fallback when Vite moves to the next port.
        return "http://localhost:5174";
    }

    private void EnsureAuthenticatedAndOpenAlgorithms(WebDriverWait wait)
    {
        var baseUrl = ResolveBaseUrl();
        // Resolve login state first, then navigate to the protected algorithms page.
        PerformLogin(wait, baseUrl);
        _driver.Navigate().GoToUrl($"{baseUrl}/algorithms");

        wait.Until(driver =>
            driver.Url.Contains("/algorithms", StringComparison.OrdinalIgnoreCase)
            || driver.Url.Contains("/login", StringComparison.OrdinalIgnoreCase));

        if (_driver.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            PerformLogin(wait, baseUrl);
            _driver.Navigate().GoToUrl($"{baseUrl}/algorithms");
            wait.Until(driver => driver.Url.Contains("/algorithms", StringComparison.OrdinalIgnoreCase));
        }

        wait.Until(driver => FindElementSafe(driver,
            By.XPath("//h1[contains(., 'algorithms') or contains(., 'Algorithm') ]"),
            By.CssSelector("button.algo-card")) is not null);
    }

    private void PerformLogin(WebDriverWait wait, string baseUrl)
    {
        _driver.Navigate().GoToUrl($"{baseUrl}/login");

        var loginStateWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        loginStateWait.Until(driver =>
            driver.Url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase)
            || driver.Url.Contains("/algorithms", StringComparison.OrdinalIgnoreCase)
            || driver.Url.Contains("/login", StringComparison.OrdinalIgnoreCase));

        // Already authenticated and redirected away from login.
        if (!_driver.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var emailInput = wait.Until(driver => FindElementSafe(driver,
            By.Id("identifier-field"),
            By.CssSelector("input[name='identifier']"),
            By.CssSelector("input[type='email']")));

        Assert.NotNull(emailInput);
        emailInput!.Clear();
        emailInput.SendKeys(_testEmail);

        var passwordInput = FindElementSafe(_driver,
            By.Id("password-field"),
            By.CssSelector("input[name='password']"),
            By.CssSelector("input[type='password']"));

        if (passwordInput is null)
        {
            var continueButton = wait.Until(driver => FindElementSafe(driver,
                By.CssSelector(".cl-formButtonPrimary"),
                By.XPath("//button[contains(., 'Continue') or contains(., 'Next')]")
            ));

            Assert.NotNull(continueButton);
            continueButton!.Click();

            var followUpWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
            followUpWait.Until(driver =>
                FindElementSafe(driver,
                    By.Id("password-field"),
                    By.CssSelector("input[name='password']"),
                    By.CssSelector("input[type='password']")) is not null
                || FindElementSafe(driver,
                    By.Id("code-field"),
                    By.CssSelector("input[name='code']"),
                    By.CssSelector("input[inputmode='numeric']")) is not null
                || driver.Url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase)
                || driver.Url.Contains("/algorithms", StringComparison.OrdinalIgnoreCase));

            passwordInput = FindElementSafe(_driver,
                By.Id("password-field"),
                By.CssSelector("input[name='password']"),
                By.CssSelector("input[type='password']"));

            if (passwordInput is null)
            {
                TryHandleDeviceVerification(wait);

                wait.Until(driver =>
                    driver.Url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase)
                    || driver.Url.Contains("/algorithms", StringComparison.OrdinalIgnoreCase));
                return;
            }
        }

        Assert.NotNull(passwordInput);
        passwordInput!.Clear();
        passwordInput.SendKeys(_testPassword);

        var submitButton = wait.Until(driver => FindElementSafe(driver,
            By.CssSelector(".cl-formButtonPrimary"),
            By.XPath("//button[contains(., 'Continue') or contains(., 'Sign in') or contains(., 'Sign In')]")
        ));

        Assert.NotNull(submitButton);
        submitButton!.Click();

        TryHandleDeviceVerification(wait);

        wait.Until(driver =>
            driver.Url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase)
            || driver.Url.Contains("/algorithms", StringComparison.OrdinalIgnoreCase));
    }

    private void TryHandleDeviceVerification(WebDriverWait wait)
    {
        IWebElement? verificationInput = null;

        try
        {
            var shortWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(6));
            verificationInput = shortWait.Until(driver => FindElementSafe(driver,
                By.Id("code-field"),
                By.CssSelector("input[name='code']"),
                By.CssSelector("input[inputmode='numeric']")));
        }
        catch (WebDriverTimeoutException)
        {
            return;
        }

        if (verificationInput is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Device verification is required for Clerk login.");
        Console.Write("Enter the 6-digit code: ");

        var code = Console.ReadLine()?.Trim();
        Assert.False(string.IsNullOrWhiteSpace(code), "Verification code is required to continue E2E login.");

        verificationInput.Clear();
        verificationInput.SendKeys(code);

        var verifyButton = wait.Until(driver => FindElementSafe(driver,
            By.CssSelector(".cl-formButtonPrimary"),
            By.XPath("//button[contains(., 'Continue') or contains(., 'Verify')]")
        ));

        Assert.NotNull(verifyButton);
        verifyButton!.Click();
    }

    private void OpenAlgorithmDetail(WebDriverWait wait, string algorithmName)
    {
        var card = wait.Until(driver => FindElementSafe(driver,
            By.XPath($"//h3[contains(normalize-space(.), '{algorithmName}')]/ancestor::button[1]"),
            By.XPath($"//button[contains(., '{algorithmName}')]")));

        Assert.NotNull(card);
        card!.Click();

        wait.Until(driver => Regex.IsMatch(driver.Url, @"/algorithms/\d+", RegexOptions.IgnoreCase));
    }

    private void EnsureVisualizationLoaded(WebDriverWait wait)
    {
        var marker = wait.Until(driver => FindElementSafe(driver,
            By.XPath("//h2[contains(., 'Simulation controls')]"),
            By.XPath("//section[@aria-label='Algorithm step visualizer']"),
            By.XPath("//*[contains(., 'Step') and contains(., 'of')]")
        ));

        Assert.NotNull(marker);
    }

    private void ClickStepForward(WebDriverWait wait)
    {
        var stepForwardButton = wait.Until(driver => FindElementSafe(driver,
            By.CssSelector("button[aria-label='Step forward']"),
            By.XPath("//button[@aria-label='Step forward']"),
            By.XPath("//button[contains(., 'Step') and contains(., 'Forward')]")
        ));

        Assert.NotNull(stepForwardButton);
        stepForwardButton!.Click();
    }

    private void ClickReplayOrReset(WebDriverWait wait)
    {
        var replayButton = wait.Until(driver => FindElementSafe(driver,
            By.XPath("//button[contains(., 'Replay')]"),
            By.XPath("//button[contains(., 'Reset')]")
        ));

        Assert.NotNull(replayButton);
        replayButton!.Click();
    }

    private void WaitForStepAdvance(WebDriverWait wait)
    {
        var previous = ReadStepProgress().current;
        wait.Until(_ => ReadStepProgress().current > previous);
    }

    private (int current, int total) ReadStepProgress()
    {
        var stepLabel = FindElementSafe(_driver,
            By.XPath("//span[contains(normalize-space(.), 'Step') and contains(normalize-space(.), 'of')]")
        );

        if (stepLabel is null)
        {
            return (0, 0);
        }

        var match = Regex.Match(stepLabel.Text, @"Step\s+(\d+)\s+of\s+(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return (0, 0);
        }

        var current = int.Parse(match.Groups[1].Value);
        var total = int.Parse(match.Groups[2].Value);
        return (current, total);
    }

    private void StepUntilFinished(WebDriverWait wait, int maxSteps)
    {
        for (var i = 0; i < maxSteps; i++)
        {
            var progress = ReadStepProgress();
            if (progress.total > 0 && progress.current >= progress.total)
            {
                return;
            }

            ClickStepForward(wait);
            Thread.Sleep(180);
        }

        var finalProgress = ReadStepProgress();
        Assert.True(finalProgress.total > 0 && finalProgress.current >= finalProgress.total,
            $"Simulation did not reach final step within {maxSteps} forward actions.");
    }

    private string GetCodeHighlightSignature(WebDriverWait wait)
    {
        var activeLine = wait.Until(driver => FindElementSafe(driver,
            By.XPath("//section[.//h2[contains(., 'Implementation')]]//button[contains(@class,'shadow-[inset_0_0_0_1px')]"),
            By.XPath("//section[.//h2[contains(., 'Implementation')]]//button[.//span[contains(@class,'bg-accent/80')]]")
        ));

        Assert.NotNull(activeLine);
        var text = activeLine!.Text.Trim();
        return text;
    }

    private string GetArraySignature(WebDriverWait wait)
    {
        wait.Until(driver => FindElementSafe(driver,
            By.XPath("//section[@aria-label='Algorithm step visualizer']")) is not null);

        var bars = _driver.FindElements(By.XPath("//section[@aria-label='Algorithm step visualizer']//*[starts-with(@aria-label, 'Index ')]"));
        var values = new List<string>();

        foreach (var bar in bars)
        {
            var label = bar.GetDomAttribute("aria-label") ?? string.Empty;
            var match = Regex.Match(label, @"value\s+(\-?\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                values.Add(match.Groups[1].Value);
            }
        }

        return string.Join("|", values);
    }

    private static IWebElement? FindElementSafe(IWebDriver driver, params By[] selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var element = driver.FindElement(selector);
                if (element.Displayed)
                {
                    return element;
                }
            }
            catch (NoSuchElementException)
            {
                // Try next selector.
            }
            catch (StaleElementReferenceException)
            {
                // Try next selector.
            }
            catch (InvalidSelectorException)
            {
                // Skip selector if unsupported by current browser driver.
            }
        }

        return null;
    }

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
