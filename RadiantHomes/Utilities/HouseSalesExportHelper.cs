using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RadiantHomes.Models; // adjust to match your namespace
using System.IO;

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
        var document = Document.Create(container =>
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
}
