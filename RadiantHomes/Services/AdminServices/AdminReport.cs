using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RadiantHomes.Services.AdminServices
{
    public enum ReportColumnKind { Text, Integer, Amount, Date }

    /// <summary>A price in its own currency, shown as "{amount:N0} {currency}"; the currency falls back to ETB.</summary>
    public readonly record struct Money(double Amount, string? Currency)
    {
        public const string DefaultCurrency = "ETB";
        public string CurrencyCode => string.IsNullOrWhiteSpace(Currency) ? DefaultCurrency : Currency.Trim();
        public override string ToString() => $"{Amount.ToString("N0", CultureInfo.InvariantCulture)} {CurrencyCode}";
    }

    /// <summary>A report column: its header and how its values are shown (amounts and dates stay typed in Excel).</summary>
    public sealed record ReportColumn(string Header, ReportColumnKind Kind = ReportColumnKind.Text)
    {
        public bool AlignEnd => Kind is ReportColumnKind.Integer or ReportColumnKind.Amount;

        /// <summary>Text shown on the page and in the PDF.</summary>
        public string Format(object? value) => value switch
        {
            null => string.Empty,
            Money money => money.ToString(),
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            double number when Kind == ReportColumnKind.Amount => number.ToString("#,##0.##", CultureInfo.InvariantCulture),
            decimal number when Kind == ReportColumnKind.Amount => number.ToString("#,##0.##", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>
    /// One admin report (Admin/Reports): the rows shown on the page are exactly the rows exported,
    /// so the page, the PDF and the Excel file always have the same columns.
    /// </summary>
    public sealed class AdminReport
    {
        public const string PdfContentType = "application/pdf";
        public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public required string Key { get; init; }
        public required string Title { get; init; }
        /// <summary>Bootstrap Icons class for the results card.</summary>
        public required string Icon { get; init; }
        /// <summary>File name without extension, e.g. "HousingCompaniesReport_20261115".</summary>
        public required string FileName { get; init; }
        /// <summary>Excel sheet name: at most 31 characters, none of : \ / ? * [ ].</summary>
        public required string SheetName { get; init; }
        /// <summary>Extra line under the title (e.g. the date range), or null.</summary>
        public string? Subtitle { get; init; }
        public required IReadOnlyList<ReportColumn> Columns { get; init; }
        public List<object?[]> Rows { get; } = new();

        public byte[] ToXlsx()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(SheetName);

            for (var c = 0; c < Columns.Count; c++)
            {
                sheet.Cell(1, c + 1).Value = Columns[c].Header;
            }
            var header = sheet.Range(1, 1, 1, Columns.Count);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");

            for (var r = 0; r < Rows.Count; r++)
            {
                for (var c = 0; c < Columns.Count; c++)
                {
                    var cell = sheet.Cell(r + 2, c + 1);
                    var value = Rows[r][c];
                    switch (value)
                    {
                        case null:
                            break;
                        case DateTime date:
                            cell.Value = date;
                            cell.Style.DateFormat.Format = "yyyy-mm-dd";
                            break;
                        case Money money:
                            // A real number in Excel, shown with its own currency (number format #,##0 "USD")
                            cell.Value = money.Amount;
                            cell.Style.NumberFormat.Format = $"#,##0 \"{money.CurrencyCode.Replace("\"", string.Empty)}\"";
                            break;
                        case int number:
                            cell.Value = number;
                            break;
                        case double number:
                            cell.Value = number;
                            if (Columns[c].Kind == ReportColumnKind.Amount) cell.Style.NumberFormat.Format = "#,##0.##";
                            break;
                        case decimal number:
                            cell.Value = number;
                            if (Columns[c].Kind == ReportColumnKind.Amount) cell.Style.NumberFormat.Format = "#,##0.##";
                            break;
                        default:
                            cell.Value = Columns[c].Format(value);
                            break;
                    }
                }
            }

            sheet.SheetView.FreezeRows(1);
            if (Rows.Count > 0) sheet.Range(1, 1, Rows.Count + 1, Columns.Count).SetAutoFilter();
            sheet.Columns(1, Columns.Count).AdjustToContents(1, Math.Min(Rows.Count + 1, 500), 8, 60);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] ToPdf()
        {
            var generatedOn = DateTime.Now;
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    // No ligatures: with them, text copied or searched in the PDF reads "Transac?ons" instead of "Transactions"
                    page.DefaultTextStyle(style => style.FontSize(9)
                        .DisableFontFeature(FontFeatures.StandardLigatures)
                        .DisableFontFeature(FontFeatures.ContextualLigatures));

                    page.Header().PaddingBottom(10).Column(column =>
                    {
                        column.Item().Text(Title).FontSize(16).Bold();
                        var details = $"{Rows.Count} row{(Rows.Count == 1 ? "" : "s")} · generated {generatedOn:yyyy-MM-dd HH:mm}";
                        if (!string.IsNullOrEmpty(Subtitle)) details = $"{Subtitle} · {details}";
                        column.Item().Text(details).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var column in Columns)
                            {
                                switch (column.Kind)
                                {
                                    case ReportColumnKind.Date: columns.ConstantColumn(64); break; // fits "yyyy-MM-dd" on one line
                                    case ReportColumnKind.Amount: columns.ConstantColumn(92); break; // fits "999,999,999 ETB" on one line
                                    case ReportColumnKind.Text: columns.RelativeColumn(2); break;
                                    default: columns.RelativeColumn(1); break;
                                }
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var column in Columns)
                            {
                                var cell = header.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                                (column.AlignEnd ? cell.AlignRight() : cell).Text(column.Header).Bold();
                            }
                        });

                        foreach (var row in Rows)
                        {
                            for (var c = 0; c < Columns.Count; c++)
                            {
                                var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
                                (Columns[c].AlignEnd ? cell.AlignRight() : cell).Text(Columns[c].Format(row[c]));
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
