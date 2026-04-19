using System.Net;
using System.Net.Http.Headers;
using System.Text;
using backend.DTOs;
using backend.Services;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace IntegrationTests.Admin;

public class AdminReportsEndpointTests
{
    [Fact(DisplayName = "BE-IT-REP-01 — GET /api/admin/reports returns CSV download for Admin")]
    public async Task GetReports_CsvFormat_WithAdminToken_ReturnsCsvFile()
    {
        var bundle = CreateNonEmptyBundle();

        using var factory = new AdminReportsEndpointWebApplicationFactory(
            bundle,
            csvContent: "section,value\nsummary,totalAttempts=12\n");

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);
        var response = await client.GetAsync("/api/admin/reports?format=csv&startDate=2026-04-01&endDate=2026-04-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("admin-report-20260401-20260430.csv");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("summary,totalAttempts=12");

        factory.CsvExportCalls.Should().Be(1);
        factory.PdfExportCalls.Should().Be(0);
    }

    [Fact(DisplayName = "BE-IT-REP-02 — GET /api/admin/reports returns PDF download for Admin")]
    public async Task GetReports_PdfFormat_WithAdminToken_ReturnsPdfFile()
    {
        var bundle = CreateNonEmptyBundle();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        using var factory = new AdminReportsEndpointWebApplicationFactory(
            bundle,
            csvContent: "unused",
            pdfContent: pdfBytes);

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);
        var response = await client.GetAsync("/api/admin/reports?format=pdf&startDate=2026-04-01&endDate=2026-04-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("admin-report-20260401-20260430.pdf");

        var content = await response.Content.ReadAsByteArrayAsync();
        content.Should().Equal(pdfBytes);

        factory.CsvExportCalls.Should().Be(0);
        factory.PdfExportCalls.Should().Be(1);
    }

    [Fact(DisplayName = "BE-IT-REP-03 — GET /api/admin/reports returns 403 for Student token")]
    public async Task GetReports_WithStudentToken_ReturnsForbidden()
    {
        using var factory = new AdminReportsEndpointWebApplicationFactory(CreateNonEmptyBundle());
        var client = BuildAuthorizedClient(factory, TestAuthHandler.UserToken);

        var response = await client.GetAsync("/api/admin/reports?format=csv&startDate=2026-04-01&endDate=2026-04-30");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-REP-04 — GET /api/admin/reports returns 401 when unauthenticated")]
    public async Task GetReports_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new AdminReportsEndpointWebApplicationFactory(CreateNonEmptyBundle());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/reports?format=csv&startDate=2026-04-01&endDate=2026-04-30");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-REP-05 — GET /api/admin/reports returns 400 for invalid date range")]
    public async Task GetReports_WithInvalidDateRange_ReturnsBadRequest()
    {
        using var factory = new AdminReportsEndpointWebApplicationFactory(CreateNonEmptyBundle());
        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync("/api/admin/reports?format=csv&startDate=2026-04-30&endDate=2026-04-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "BE-IT-REP-06 — GET /api/admin/reports empty period returns graceful empty file")]
    public async Task GetReports_WithEmptyPeriod_ReturnsEmptyFile()
    {
        var emptyBundle = new AdminReportBundleDto
        {
            Summary = new SummaryStatisticsDto { TotalAttempts = 0 },
            PerStudent = Array.Empty<PerStudentReportDto>(),
            PerAlgorithm = Array.Empty<PerAlgorithmReportDto>(),
            PerQuiz = Array.Empty<PerQuizReportDto>()
        };

        using var factory = new AdminReportsEndpointWebApplicationFactory(emptyBundle);
        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync("/api/admin/reports?format=csv&startDate=2026-05-01&endDate=2026-05-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("admin-report-20260501-20260531.csv");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().BeEmpty();

        factory.CsvExportCalls.Should().Be(0);
        factory.PdfExportCalls.Should().Be(0);
    }

    private static HttpClient BuildAuthorizedClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static AdminReportBundleDto CreateNonEmptyBundle()
    {
        return new AdminReportBundleDto
        {
            Summary = new SummaryStatisticsDto
            {
                TotalAttempts = 12,
                TotalStudents = 4,
                AverageScore = 81.5m,
                TotalXp = 240
            },
            PerStudent =
            [
                new PerStudentReportDto
                {
                    StudentId = 101,
                    StudentName = "student-alpha",
                    TotalAttempts = 3,
                    AverageScore = 84.0m,
                    BestScore = 96,
                    TotalXp = 60,
                    AlgorithmsAttempted = 2
                }
            ],
            PerAlgorithm =
            [
                new PerAlgorithmReportDto
                {
                    AlgorithmType = "Quick Sort",
                    AttemptCount = 5,
                    AverageScore = 79.5m,
                    PassRate = 60.0m
                }
            ],
            PerQuiz =
            [
                new PerQuizReportDto
                {
                    Title = "Sorting Fundamentals",
                    AttemptCount = 6,
                    AverageScore = 82.0m,
                    HighestScore = 100,
                    LowestScore = 45
                }
            ]
        };
    }
}

public sealed class AdminReportsEndpointWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly AdminReportBundleDto _bundle;
    private readonly byte[] _pdfContent;
    private readonly string _csvContent;

    public int CsvExportCalls { get; private set; }
    public int PdfExportCalls { get; private set; }

    public AdminReportsEndpointWebApplicationFactory(
        AdminReportBundleDto bundle,
        string csvContent = "header1,header2\nvalue1,value2\n",
        byte[]? pdfContent = null)
    {
        _bundle = bundle;
        _csvContent = csvContent;
        _pdfContent = pdfContent ?? new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clerk:Authority"] = "https://test.clerk.example.com",
                ["Clerk:SecretKey"] = "sk_test_dummy_value_for_integration_tests",
                ["SkipMigrations"] = "true",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<IUserService>();
            services.AddScoped<IUserService, StubUserService>();

            var reportServiceMock = new Mock<IReportService>();
            reportServiceMock
                .Setup(s => s.GetAdminReportBundleAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(_bundle);

            var csvExportMock = new Mock<IReportCsvExportService>();
            csvExportMock
                .Setup(s => s.CreateAdminReportCsv(It.IsAny<AdminReportBundleDto>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(() =>
                {
                    CsvExportCalls++;
                    return new MemoryStream(Encoding.UTF8.GetBytes(_csvContent));
                });

            var pdfExportMock = new Mock<IReportPdfExportService>();
            pdfExportMock
                .Setup(s => s.CreateAdminReportPdf(It.IsAny<AdminReportBundleDto>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(() =>
                {
                    PdfExportCalls++;
                    return _pdfContent;
                });

            services.RemoveAll<IReportService>();
            services.AddScoped(_ => reportServiceMock.Object);

            services.RemoveAll<IReportCsvExportService>();
            services.AddScoped(_ => csvExportMock.Object);

            services.RemoveAll<IReportPdfExportService>();
            services.AddScoped(_ => pdfExportMock.Object);
        });
    }
}
