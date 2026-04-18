using System.Globalization;
using backend.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace backend.Services;

/// <summary>
/// PDF export implementation for admin reports using QuestPDF.
/// </summary>
public class ReportPdfExportService : IReportPdfExportService
{
    private static readonly string HeaderBg = "#E9F2FF";
    private static readonly string GridBorder = "#D9E1EC";

    static ReportPdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc />
    public byte[] CreateAdminReportPdf(AdminReportBundleDto bundle, DateTime startDate, DateTime endDate)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().AlignCenter().Text("Performance Report").Bold().FontSize(18);
                    column.Item().AlignCenter().Text($"Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}").FontSize(10).FontColor(Colors.Grey.Darken2);
                    column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Element(SectionTitleStyle).Text("Summary").SemiBold().FontSize(12);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Metric").SemiBold();
                            header.Cell().Element(HeaderCellStyle).Text("Value").SemiBold();
                        });

                        SummaryRow(table, "Total Attempts", bundle.Summary.TotalAttempts.ToString(CultureInfo.InvariantCulture));
                        SummaryRow(table, "Total Students", bundle.Summary.TotalStudents.ToString(CultureInfo.InvariantCulture));
                        SummaryRow(table, "Average Score", bundle.Summary.AverageScore.ToString("F2", CultureInfo.InvariantCulture));
                        SummaryRow(table, "Total XP", bundle.Summary.TotalXp.ToString(CultureInfo.InvariantCulture));
                    });

                    column.Item().Element(SectionTitleStyle).Text("Per-Student Breakdown").SemiBold().FontSize(12);
                    column.Item().Element(c => BuildStudentTable(c, bundle));

                    column.Item().PageBreak();

                    column.Item().Element(SectionTitleStyle).Text("Per-Algorithm Breakdown").SemiBold().FontSize(12);
                    column.Item().Element(c => BuildAlgorithmTable(c, bundle));

                    column.Item().Element(SectionTitleStyle).Text("Per-Quiz Breakdown").SemiBold().FontSize(12);
                    column.Item().Element(c => BuildQuizTable(c, bundle));
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)).SemiBold();
                });
            });
        }).GeneratePdf();
    }

    private static IContainer SectionTitleStyle(IContainer container)
        => container.Padding(8).Background(HeaderBg).Border(1).BorderColor(GridBorder);

    private static IContainer HeaderCellStyle(IContainer container)
        => container.Padding(6).Background(HeaderBg).Border(1).BorderColor(GridBorder);

    private static IContainer BodyCellStyle(IContainer container)
        => container.Padding(6).Border(1).BorderColor(GridBorder);

    private static void SummaryRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(BodyCellStyle).Text(label);
        table.Cell().Element(BodyCellStyle).Text(value);
    }

    private static void BuildStudentTable(IContainer container, AdminReportBundleDto bundle)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(40);
                columns.RelativeColumn(2);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(50);
                columns.ConstantColumn(50);
                columns.ConstantColumn(70);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("ID").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Student").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Attempts").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Avg").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Best").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("XP").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Algorithms").SemiBold();
            });

            foreach (var item in bundle.PerStudent)
            {
                table.Cell().Element(BodyCellStyle).Text(item.StudentId.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.StudentName);
                table.Cell().Element(BodyCellStyle).Text(item.TotalAttempts.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.AverageScore.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.BestScore.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.TotalXp.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.AlgorithmsAttempted.ToString(CultureInfo.InvariantCulture));
            }
        });
    }

    private static void BuildAlgorithmTable(IContainer container, AdminReportBundleDto bundle)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.ConstantColumn(70);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("Algorithm Type").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Attempts").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Avg Score").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Pass Rate").SemiBold();
            });

            foreach (var item in bundle.PerAlgorithm)
            {
                table.Cell().Element(BodyCellStyle).Text(item.AlgorithmType);
                table.Cell().Element(BodyCellStyle).Text(item.AttemptCount.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.AverageScore.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.PassRate.ToString("F2", CultureInfo.InvariantCulture));
            }
        });
    }

    private static void BuildQuizTable(IContainer container, AdminReportBundleDto bundle)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.ConstantColumn(70);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("Quiz Title").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Attempts").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Avg Score").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Highest").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Lowest").SemiBold();
            });

            foreach (var item in bundle.PerQuiz)
            {
                table.Cell().Element(BodyCellStyle).Text(item.Title);
                table.Cell().Element(BodyCellStyle).Text(item.AttemptCount.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.AverageScore.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.HighestScore.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).Text(item.LowestScore.ToString(CultureInfo.InvariantCulture));
            }
        });
    }
}
