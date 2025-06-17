using ClosedXML.Excel;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using System.IO;

public static class ExcelExportService
{
    public static byte[] GenerateEmptyBuildingTemplate()
    {
        var columnHeaders = new[]
        {
            "ProjectName",
            "BuildingName",
            "NumberOfFloors",
            "BuildingType",
            "ConstructionStatus",
            "CreatedBy",
            "CreatedOn",
            "UpdatedBy",
            "UpdatedOn"
        };

        using var package = new XLWorkbook();
        var worksheet = package.Worksheets.Add("Buildings Details");

        // Set the column headers
        for (int i = 0; i < columnHeaders.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = columnHeaders[i];
        }

        // Save the workbook to a MemoryStream
        using var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0; // Ensure the position is at the beginning for proper reading.

        return stream.ToArray();
    }

    public static List<Building> ParseBuildingsFromExcel(Stream stream, ApplicationDbContext context)
    {
        var buildings = new List<Building>();

        // Load all project names and IDs into memory for quick lookup
        var projectLookup = context.Projects
            .ToDictionary(p => p.ProjectName.Trim().ToLower(), p => p.ProjectId);

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        var rows = worksheet.RowsUsed().Skip(1); // Skip header row

        foreach (var row in rows)
        {
            try
            {
                var projectName = row.Cell(1).GetString().Trim().ToLower();
                var buildingName = row.Cell(2).GetString().Trim();
                var numberOfFloorsText = row.Cell(3).GetString().Trim();
                var buildingType = row.Cell(4).GetString().Trim();
                var constructionStatus = row.Cell(5).GetString().Trim();

                if (!projectLookup.TryGetValue(projectName, out Guid projectId))
                    continue; // Skip unknown project names

                if (!int.TryParse(numberOfFloorsText, out var numberOfFloors))
                    continue;

                buildings.Add(new Building
                {
                    ProjectId = projectId,
                    BuildingName = buildingName,
                    NumberOfFloors = numberOfFloors,
                    //BuildingType = buildingType,
                    //ConstructionStatus = constructionStatus,
                    CreatedBy = "Admin",
                    CreatedOn = DateTime.UtcNow
                });
            }
            catch
            {
                // Optional: Log or track skipped row
                continue;
            }
        }

        return buildings;
    }
}
