using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Utilities;

namespace RadiantHomes.Pages.Buildings
{
    /// <summary>
    /// Reads the building Excel template (wwwroot/templates/BuildingTemplate.xlsx): ProjectName, BuildingName,
    /// NumberOfFloors, BuildingType, ConstructionStatus (later columns are ignored). Every row is checked first;
    /// buildings are returned only when the whole file is valid, so the caller saves all of them or none.
    /// Used by the building Create pages (root and HousingCompanyAdmin area).
    /// </summary>
    public static class BuildingImport
    {
        private static readonly string[] ExcelExtensions = { ".xlsx" };
        private const int MaxListedErrors = 50;

        /// <param name="projects">The projects rows may name: a Real Estate Admin's own company's projects only.</param>
        /// <returns>The buildings to add (empty when there are errors) and the problems found.</returns>
        public static async Task<(List<Building> Buildings, List<string> Errors)> ReadAsync(IFormFile? file, IQueryable<HouseProject> projects)
        {
            var errors = new List<string>();
            var buildings = new List<Building>();
            if (file == null)
            {
                errors.Add("Please choose an Excel file (.xlsx) to upload.");
                return (buildings, errors);
            }
            if (SafeUpload.Validate(file, ExcelExtensions) is string fileError)
            {
                errors.Add(fileError);
                return (buildings, errors);
            }

            XLWorkbook workbook;
            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;
                workbook = new XLWorkbook(stream);
            }
            catch (Exception)
            {
                errors.Add($"\"{file.FileName}\" couldn't be read as an Excel workbook (.xlsx). Please use the template.");
                return (buildings, errors);
            }

            using (workbook)
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                var lastRow = worksheet?.LastRowUsed()?.RowNumber() ?? 0;
                if (worksheet == null || lastRow < 2)
                {
                    errors.Add("The file has no building rows (row 1 is the header row).");
                    return (buildings, errors);
                }

                // Projects by name, loaded once (names are matched ignoring case and surrounding spaces)
                var projectsByName = (await projects
                        .Select(p => new { p.ProjectId, p.ProjectName, p.AddressId })
                        .ToListAsync())
                    .GroupBy(p => (p.ProjectName ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                for (var row = 2; row <= lastRow; row++)
                {
                    if (worksheet.Row(row).IsEmpty()) continue;

                    var projectName = worksheet.Cell(row, 1).GetString().Trim();
                    var buildingName = worksheet.Cell(row, 2).GetString().Trim();
                    var floorsText = worksheet.Cell(row, 3).GetString().Trim();
                    var typeText = worksheet.Cell(row, 4).GetString().Trim();
                    var statusText = worksheet.Cell(row, 5).GetString().Trim();
                    var rowErrors = new List<string>();

                    var matches = projectName.Length == 0 ? null : projectsByName.GetValueOrDefault(projectName);
                    if (projectName.Length == 0) rowErrors.Add("the project name is missing");
                    else if (matches == null) rowErrors.Add($"project \"{projectName}\" was not found among your projects");
                    else if (matches.Count > 1) rowErrors.Add($"there is more than one project named \"{projectName}\"");

                    if (buildingName.Length == 0) rowErrors.Add("the building name is missing");

                    if (!int.TryParse(floorsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floors) || floors < 1 || floors > 200)
                        rowErrors.Add($"the number of floors \"{floorsText}\" is not a whole number from 1 to 200");

                    if (!BuildingOptions.TryParseType(typeText, out var buildingType))
                        rowErrors.Add($"the building type \"{typeText}\" is not one of: {Allowed<BuildingTypes>()}");

                    if (!BuildingOptions.TryParseStatus(statusText, out var constructionStatus))
                        rowErrors.Add($"the construction status \"{statusText}\" is not one of: {Allowed<ConstructionStatuses>()}");

                    if (rowErrors.Count > 0)
                    {
                        errors.Add($"Row {row}: {string.Join("; ", rowErrors)}.");
                        continue;
                    }

                    buildings.Add(new Building
                    {
                        ProjectId = matches![0].ProjectId,
                        BuildingName = buildingName,
                        NumberOfFloors = floors,
                        BuildingType = buildingType.ToString(),
                        ConstructionStatus = constructionStatus.ToString(),
                        AddressId = matches[0].AddressId, // the project's address; Id and CreatedOn/CreatedBy are set by SaveChanges
                    });
                }
            }

            if (errors.Count > 0)
            {
                if (errors.Count > MaxListedErrors)
                {
                    var more = errors.Count - MaxListedErrors;
                    errors.RemoveRange(MaxListedErrors, more);
                    errors.Add($"…and {more} more row{(more == 1 ? "" : "s")} with problems.");
                }
                buildings.Clear();
            }
            else if (buildings.Count == 0)
            {
                errors.Add("The file has no building rows (row 1 is the header row).");
            }
            return (buildings, errors);
        }

        private static string Allowed<TEnum>() where TEnum : struct, Enum =>
            string.Join(", ", Enum.GetValues<TEnum>().Select(v => v.GetDisplayName()));
    }
}
