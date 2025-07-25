using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RadiantHomes.Models; // adjust to match your namespace
using RadiantHomes.Models.HousingCompanies;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

public static class HouseSalesExportHelper
{
    public static byte[] ToExcel(List<HouseSaleTransaction> sales)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("House Sales");

        // Header
        worksheet.Cell(1, 1).Value = "House Title";
        worksheet.Cell(1, 2).Value = "Receiver";
        worksheet.Cell(1, 3).Value = "Home Owner";
        worksheet.Cell(1, 4).Value = "Transaction Date";
        worksheet.Cell(1, 5).Value = "Status";
        worksheet.Cell(1, 6).Value = "Notes";
        // Data
        for (int i = 0; i < sales.Count; i++)
        {
            var row = i + 2;
            worksheet.Cell(row, 1).Value = sales[i].House.Title;
            worksheet.Cell(row, 2).Value = sales[i].Receiver?.FirstName + sales[i].Receiver?.LastName;
            worksheet.Cell(row, 3).Value = sales[i].HomeOwner?.FirstName + sales[i].HomeOwner?.LastName;
            worksheet.Cell(row, 4).Value = sales[i].TransactionDate.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 5).Value = sales[i].Status.ToString();
            worksheet.Cell(row, 6).Value = sales[i].Notes ?? "-";
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] ToPdf(List<HouseSaleTransaction> sales)
    {
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);

                page.Header().Text("House Sale Transactions Report").FontSize(20).Bold().AlignCenter();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.ConstantColumn(100);
                        columns.ConstantColumn(70);
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("House Title").Bold();
                        header.Cell().Element(CellStyle).Text("Receiver").Bold();
                        header.Cell().Element(CellStyle).Text("Home Owner").Bold();
                        header.Cell().Element(CellStyle).Text("Date").Bold();
                        header.Cell().Element(CellStyle).Text("Status").Bold();
                        header.Cell().Element(CellStyle).Text("Notes").Bold();
                    });

                    foreach (var sale in sales)
                    {
                        table.Cell().Element(CellStyle).Text(sale.House?.Title ?? "N/A");
                        table.Cell().Element(CellStyle).Text(sale.Receiver?.FirstName ?? "N/A");
                        table.Cell().Element(CellStyle).Text(sale.HomeOwner?.FirstName ?? "N/A");
                        table.Cell().Element(CellStyle).Text(sale.TransactionDate.ToString("yyyy-MM-dd"));
                        table.Cell().Element(CellStyle).Text(sale.Status.ToString());
                        table.Cell().Element(CellStyle).Text(sale.Notes ?? "-");
                    }

                    static IContainer CellStyle(IContainer container)
                    {
                        return container
                            .PaddingVertical(5)
                            .PaddingHorizontal(3)
                            .BorderBottom(1)
                            .BorderColor("#E0E0E0");
                    }
                });

                page.Footer().AlignCenter().Text($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}");
            });
        });

        return document.GeneratePdf();
    }
    //public static byte[] ToPdfHouseSold(List<ApartmentHouse> sales)
    //{
    //    var document = Document.Create(container =>
    //    {
    //        container.Page(page =>
    //        {
    //            page.Margin(30);
    //            page.Size(PageSizes.A4);

    //            page.Header().Text("House Sale Transactions Report").FontSize(20).Bold().AlignCenter();

    //            page.Content().Table(table =>
    //            {
    //                table.ColumnsDefinition(columns =>
    //                {
    //                    columns.RelativeColumn();
    //                    columns.RelativeColumn();
    //                    columns.RelativeColumn();
    //                    columns.ConstantColumn(100);
    //                    columns.ConstantColumn(70);
    //                    columns.RelativeColumn();
    //                });

    //                table.Header(header =>
    //                {
    //                    header.Cell().Element(CellStyle).Text("House Title").Bold();
    //                    header.Cell().Element(CellStyle).Text("SalePrice").Bold();
    //                    header.Cell().Element(CellStyle).Text("Date Sold").Bold();
    //                    header.Cell().Element(CellStyle).Text("Status").Bold();
    //                });

    //                foreach (var sale in sales)
    //                {
    //                    table.Cell().Element(CellStyle).Text(sale?.Title ?? "N/A");
    //                    table.Cell().Element(CellStyle).Text(sale?.SalePrice.ToString());
    //                    table.Cell().Element(CellStyle).Text(sale?.CreatedOn.ToString("yyyy-MM-dd") ?? "N/A");
    //                    table.Cell().Element(CellStyle).Text(sale?.Status);
    //                    table.Cell().Element(CellStyle).Text(sale.Status.ToString());
    //                }

    //                static IContainer CellStyle(IContainer container)
    //                {
    //                    return container
    //                        .PaddingVertical(5)
    //                        .PaddingHorizontal(3)
    //                        .BorderBottom(1)
    //                        .BorderColor("#E0E0E0");
    //                }
    //            });

    //            page.Footer().AlignCenter().Text($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}");
    //        });
    //    });

    //    return document.GeneratePdf();
    //}
    public static byte[] ToPdfHouseSold<T>(IEnumerable<T> data, string? reportTitle=null) // Made generic <T>
    {
        if (data == null || !data.Any())
        {
            return new byte[0]; // Return empty bytes if no data
        }

        using (MemoryStream ms = new MemoryStream())
        {
            // Fix: Use a standard page size like A4, Letter, etc.
            // Or define a larger custom rectangle in points if needed.
            iTextSharp.text.Document document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 25, 25, 30, 30); // A4 with margins (left, right, top, bottom)

            PdfWriter writer = PdfWriter.GetInstance(document, ms);
            document.Open();

            // Uncomment and use your report title and spacer if you want them
            document.Add(new Paragraph(reportTitle) { Alignment = Element.ALIGN_CENTER, Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16) });
            document.Add(new Paragraph(" "));

            // Use reflection to get properties dynamically for generic export
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                    .Where(p => p.CanRead)
                                                    .ToArray();

            // You might want to calculate column widths to better fit the page
            // float[] columnWidths = new float[properties.Length];
            // // Example: Distribute width equally
            // for (int i = 0; i < properties.Length; i++) { columnWidths[i] = 1f; }
            // PdfPTable table = new PdfPTable(columnWidths);

            PdfPTable table = new PdfPTable(properties.Length);
            table.WidthPercentage = 100; // Table takes full width of the document's content area
            table.DefaultCell.Padding = 5;
            table.DefaultCell.BorderWidth = 1;

            // Add Headers
            foreach (var prop in properties)
            {
                // Ensure font is set for headers
                table.AddCell(new Phrase(prop.Name, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
            }

            // Add Data Rows
            foreach (var item in data)
            {
                foreach (var prop in properties)
                {
                    object value = prop.GetValue(item);
                    // Ensure font is set for data cells
                    table.AddCell(new Phrase(value?.ToString() ?? "", FontFactory.GetFont(FontFactory.HELVETICA, 9)));
                }
            }

            document.Add(table);
            document.Close();
            return ms.ToArray();
        }
    }
}
