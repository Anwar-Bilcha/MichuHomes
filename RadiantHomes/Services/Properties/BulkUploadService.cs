using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Pages.ApartmentHouses;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Properties
{
    public sealed record UploadResult(bool Succeeded, IReadOnlyList<string> Errors, UploadBatch? Batch);

    /// <summary>
    /// Bulk apartment upload (Excel template) into one building. Every row is validated before anything is saved; a valid
    /// file becomes an upload batch of Pending apartments that the platform reviews row by row (houses.approve) on
    /// Staff/Uploads. Rejected rows carry a reason; the company corrects the apartment and sends the row back.
    /// </summary>
    public sealed class BulkUploadService
    {
        public const long MaxBytes = 5 * 1024 * 1024;
        public const int MaxRows = 1000;
        private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        // Template columns (wwwroot/templates/ModifiedApartmentHouseTemplate.xlsx), 1-based.
        private const int ColTitle = 1, ColHouseNumber = 2, ColDescription = 3, ColSalePrice = 4, ColAvailableFor = 5, ColFloor = 7,
            ColBathrooms = 8, ColTotalRooms = 9, ColIsAvailable = 10, ColCurrency = 11, ColLivingRooms = 12, ColBedrooms = 13,
            ColBalcony = 14, ColElevator = 15, ColParking = 16, ColFurnished = 17, ColHouseType = 20;

        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public BulkUploadService(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public static string NewReference()
        {
            Span<char> code = stackalloc char[5];
            for (var i = 0; i < code.Length; i++) code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            return $"B-{DateTime.UtcNow:yyMM}-{code}";
        }

        public async Task<UploadResult> UploadAsync(CompanyScope scope, UserAccess actor, string? actorName, Guid buildingId, IFormFile? file)
        {
            var errors = new List<string>();
            UploadResult Fail(string message) { errors.Add(message); return new UploadResult(false, errors, null); }

            if (file is null || file.Length == 0) return Fail("Choose the filled-in Excel template (.xlsx).");
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) return Fail("Upload an Excel workbook (.xlsx) based on the template.");
            if (file.Length > MaxBytes) return Fail("The file is larger than 5 MB.");
            if (!await scope.CanAccessBuildingAsync(_db, buildingId)) return Fail("Choose one of your buildings for the uploaded apartments.");

            var building = await _db.Buildings.Include(b => b.Project).ThenInclude(p => p!.Company).FirstAsync(b => b.Id == buildingId);
            var company = building.Project?.Company;
            if (company is null) return Fail("The building isn't linked to a company project.");
            var category = await _db.HouseCategories.FirstOrDefaultAsync(c => c.Name.ToLower() == "serviced apartment")
                           ?? await _db.HouseCategories.OrderBy(c => c.Name).FirstOrDefaultAsync();
            if (category is null) return Fail("No house categories are set up yet. Ask Michu Homes to add one.");

            XLWorkbook workbook;
            try
            {
                await using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer);
                buffer.Position = 0;
                workbook = new XLWorkbook(buffer);
            }
            catch
            {
                return Fail("The file isn't a valid Excel workbook. Save it as .xlsx and try again.");
            }

            using (workbook)
            {
                var sheet = workbook.Worksheets.FirstOrDefault();
                var lastRow = sheet?.LastRowUsed()?.RowNumber() ?? 0;
                if (sheet is null || lastRow < 2) return Fail("The file has no apartment rows (row 1 is the header).");
                if (lastRow - 1 > MaxRows) return Fail($"Upload at most {MaxRows} apartments per file.");

                var taken = (await _db.ApartmentHouses.Where(a => a.BuildingId == buildingId).Select(a => a.HouseNumber).ToListAsync())
                    .Where(n => !string.IsNullOrWhiteSpace(n)).Select(Normalize).ToHashSet();
                var seen = new Dictionary<string, int>();
                var houses = new List<(int Row, ApartmentHouse House)>();

                for (var row = 2; row <= lastRow; row++)
                {
                    if (sheet.Row(row).Cells(1, ColHouseType).All(c => string.IsNullOrWhiteSpace(c.GetString()))) continue; // blank line
                    var rowErrors = new List<string>();
                    string Text(int col) => sheet.Cell(row, col).GetString().Trim();

                    var title = Text(ColTitle);
                    if (title.Length == 0) rowErrors.Add("title is missing");
                    else if (title.Length > 200) rowErrors.Add("title is longer than 200 characters");

                    var number = Text(ColHouseNumber);
                    if (number.Length == 0) rowErrors.Add("house number is missing");
                    else if (number.Length > 50) rowErrors.Add("house number is longer than 50 characters");
                    else if (taken.Contains(Normalize(number))) rowErrors.Add($"house number {number} already exists in this building");
                    else if (seen.TryGetValue(Normalize(number), out var firstRow)) rowErrors.Add($"house number {number} is also on row {firstRow}");
                    else seen[Normalize(number)] = row;

                    var price = Number(sheet.Cell(row, ColSalePrice), "sale price", 1, 1_000_000_000_000, rowErrors);
                    var floor = Whole(sheet.Cell(row, ColFloor), "floor", -5, Math.Max(building.NumberOfFloors, 1), rowErrors,
                        building.NumberOfFloors > 0 ? $"floor must be between -5 and {building.NumberOfFloors} (the building has {building.NumberOfFloors} floors)" : null);
                    var bedrooms = Whole(sheet.Cell(row, ColBedrooms), "bedrooms", 0, 50, rowErrors);
                    var totalRooms = Whole(sheet.Cell(row, ColTotalRooms), "total rooms", 1, 200, rowErrors);
                    var bathrooms = Whole(sheet.Cell(row, ColBathrooms), "bathrooms", 0, 50, rowErrors);
                    var livingRooms = Whole(sheet.Cell(row, ColLivingRooms), "living rooms", 0, 50, rowErrors, optional: true);
                    if (totalRooms > 0 && bedrooms > totalRooms) rowErrors.Add("total rooms can't be fewer than bedrooms");

                    var currency = Text(ColCurrency).ToUpperInvariant();
                    if (currency.Length == 0) currency = ApartmentHouseForm.DefaultCurrency;
                    else if (!ApartmentHouseForm.Currencies.Contains(currency)) rowErrors.Add($"currency must be one of {string.Join(", ", ApartmentHouseForm.Currencies)}");

                    var availableFor = Text(ColAvailableFor);
                    var option = ApartmentHouseForm.AvailableForOptions.FirstOrDefault(o => o.Value.Equals(availableFor, StringComparison.OrdinalIgnoreCase) || o.Text.Equals(availableFor, StringComparison.OrdinalIgnoreCase));
                    if (availableFor.Length == 0) option = ApartmentHouseForm.AvailableForOptions.First(o => o.Value == "Sell");
                    else if (option.Value is null) rowErrors.Add("available for must be Sale, Rent or Both");

                    var typeText = Text(ColHouseType);
                    var houseType = HouseTypesCateg.Residential;
                    if (typeText.Length > 0 && !Enum.TryParse(typeText.Replace(" ", string.Empty), true, out houseType)) rowErrors.Add($"house type must be one of {string.Join(", ", Enum.GetNames<HouseTypesCateg>())}");

                    bool Flag(int col, string label)
                    {
                        var cell = sheet.Cell(row, col);
                        if (cell.DataType == XLDataType.Boolean) return cell.GetBoolean();
                        var t = cell.GetString().Trim().ToLowerInvariant();
                        if (t is "" or "no" or "false" or "0" or "n") return false;
                        if (t is "yes" or "true" or "1" or "y") return true;
                        rowErrors.Add($"{label} must be Yes or No");
                        return false;
                    }
                    var isAvailable = sheet.Cell(row, ColIsAvailable).GetString().Trim().Length == 0 || Flag(ColIsAvailable, "available");

                    var house = new ApartmentHouse
                    {
                        Id = Guid.NewGuid(),
                        Title = title,
                        HouseNumber = number,
                        Description = Text(ColDescription),
                        SalePrice = price,
                        Currency = currency,
                        AvailableFor = option.Value ?? "Sell",
                        Status = "Pending", // listed only after the platform approves it
                        BuildingId = buildingId,
                        FloorNumber = floor,
                        Bathrooms = bathrooms,
                        TotalRooms = totalRooms,
                        Bedrooms = bedrooms,
                        LivingRooms = livingRooms,
                        IsAvailable = isAvailable,
                        HasBalcony = Flag(ColBalcony, "balcony"),
                        HasElevator = Flag(ColElevator, "elevator"),
                        HasParking = Flag(ColParking, "parking"),
                        IsFurnished = Flag(ColFurnished, "furnished"),
                        SalesStatus = "None",
                        HouseType = houseType.ToString(),
                        AddressId = building.AddressId,
                        HouseCategoryId = category.Id,
                    };
                    if (rowErrors.Count > 0) errors.Add($"Row {row}: {string.Join("; ", rowErrors)}.");
                    else houses.Add((row, house));
                }

                if (errors.Count > 0) return new UploadResult(false, errors, null);
                if (houses.Count == 0) return Fail("The file has no apartment rows (row 1 is the header).");

                var batch = new UploadBatch
                {
                    Id = Guid.NewGuid(),
                    Reference = NewReference(),
                    CompanyId = company.CompanyId,
                    BuildingId = buildingId,
                    FileName = Path.GetFileName(file.FileName) is { Length: <= 260 } n ? n : "upload.xlsx",
                    UploadedByUserId = actor.UserId,
                    UploadedByName = actorName,
                    UploadedOn = DateTime.UtcNow,
                };
                foreach (var (row, house) in houses)
                {
                    _db.ApartmentHouses.Add(house);
                    batch.Rows.Add(new UploadBatchRow { Id = Guid.NewGuid(), BatchId = batch.Id, RowNumber = row, ApartmentHouseId = house.Id });
                }
                _db.UploadBatches.Add(batch);
                _db.Notifications.Add(new Notification
                {
                    Title = $"{houses.Count} apartments to review",
                    Message = $"{company.Name} uploaded {houses.Count} apartments into {building.BuildingName} (batch {batch.Reference}).",
                    ReceiverCompanyId = company.CompanyId,
                    RecipientRole = MichuHomesRoles.MichuAdmin,
                    Datatype = EntityAffecteds.ApartmentHouses.ToString(),
                    DataKey = batch.Id.ToString(),
                    RedirectUrl = $"/Staff/Uploads/Details?id={batch.Id}",
                    CreatedAt = DateTime.Now,
                });
                _audit.Add(AuditCategories.Uploads, "UploadSubmitted", $"Batch {batch.Reference}: {houses.Count} apartments uploaded into {building.BuildingName}.",
                    company.CompanyId, nameof(UploadBatch), batch.Id.ToString());
                await _db.SaveChangesAsync();
                return new UploadResult(true, errors, batch);
            }
        }

        /// <summary>Platform decision on rows of a batch: approve some, reject others (reason required).</summary>
        public async Task<AccessResult> DecideAsync(UserAccess actor, string? actorName, Guid batchId, IReadOnlyCollection<Guid> approve, IReadOnlyCollection<Guid> reject, string? reason)
        {
            if (!(actor.IsPlatform && actor.Has(Perm.HousesApprove))) return AccessResult.Fail("You don't have permission to review uploads.");
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (reject.Count > 0 && reason is null) return AccessResult.Fail("Give a reason for the rejected apartments. The company will see it.");
            if (reason is { Length: > 1000 }) return AccessResult.Fail("Keep the reason under 1000 characters.");
            if (approve.Count == 0 && reject.Count == 0) return AccessResult.Fail("Choose the apartments to approve or reject.");
            if (approve.Intersect(reject).Any()) return AccessResult.Fail("An apartment can't be approved and rejected at once.");

            var batch = await _db.UploadBatches.Include(b => b.Company).Include(b => b.Rows).ThenInclude(r => r.ApartmentHouse).FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch is null) return AccessResult.Fail("That upload doesn't exist.");
            var chosen = batch.Rows.Where(r => approve.Contains(r.Id) || reject.Contains(r.Id)).ToList();
            if (chosen.Count != approve.Count + reject.Count) return AccessResult.Fail("Some of the chosen apartments aren't part of this upload.");
            if (chosen.Any(r => r.Status != UploadRowStatuses.Pending)) return AccessResult.Fail("Some of the chosen apartments were already decided.");

            foreach (var row in chosen)
            {
                var approved = approve.Contains(row.Id);
                row.Status = approved ? UploadRowStatuses.Approved : UploadRowStatuses.Rejected;
                row.RejectReason = approved ? null : reason;
                row.DecidedOn = DateTime.UtcNow;
                row.DecidedByName = actorName;
                // Only a house still waiting changes; a deliberate state (e.g. Hidden) is kept.
                if (row.ApartmentHouse.Status is "Pending" or "New" or "Rejected") row.ApartmentHouse.Status = approved ? "Approved" : "Rejected";
            }
            batch.Status = BatchStatus(batch.Rows);
            batch.ReviewedByUserId = actor.UserId;
            batch.ReviewedByName = actorName;
            batch.ReviewedOn = DateTime.UtcNow;

            _db.Notifications.Add(new Notification
            {
                Title = $"Upload {batch.Reference} reviewed",
                Message = $"{approve.Count} apartment(s) approved{(reject.Count > 0 ? $", {reject.Count} rejected: {reason}" : "")}.",
                ReceiverCompanyId = batch.CompanyId,
                RecipientRole = MichuHomesRoles.RealEstateAdmin,
                Datatype = EntityAffecteds.ApartmentHouses.ToString(),
                DataKey = batch.Id.ToString(),
                RedirectUrl = $"/Staff/Uploads/Details?id={batch.Id}",
                CreatedAt = DateTime.Now,
            });
            _audit.Add(AuditCategories.Uploads, "UploadReviewed", $"Batch {batch.Reference} of {batch.Company.Name}: {approve.Count} approved, {reject.Count} rejected{(reason is null ? "" : $" ({reason})")}.",
                batch.CompanyId, nameof(UploadBatch), batch.Id.ToString());
            await _db.SaveChangesAsync();
            return AccessResult.Ok($"{approve.Count} approved, {reject.Count} rejected.");
        }

        /// <summary>
        /// Apartments created one at a time (not through an upload) wait in the same review queue: approve or reject one.
        /// </summary>
        public async Task<AccessResult> DecideSingleAsync(UserAccess actor, Guid apartmentId, bool approve, string? reason)
        {
            if (!(actor.IsPlatform && actor.Has(Perm.HousesApprove))) return AccessResult.Fail("You don't have permission to review apartments.");
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (!approve && reason is null) return AccessResult.Fail("Give a reason for the rejection. The company will see it.");
            var house = await _db.ApartmentHouses.Include(a => a.Building!).ThenInclude(b => b.Project!).FirstOrDefaultAsync(a => a.Id == apartmentId);
            if (house is null || house.Status is not ("Pending" or "New")) return AccessResult.Fail("That apartment isn't waiting for review.");
            house.Status = approve ? "Approved" : "Rejected";
            var companyId = house.Building?.Project?.HousingCompanyId;
            _db.Notifications.Add(new Notification
            {
                Title = approve ? "Apartment approved" : "Apartment rejected",
                Message = approve ? $"\"{house.Title}\" was approved and is listed." : $"\"{house.Title}\" was rejected: {reason}",
                ReceiverCompanyId = companyId,
                RecipientRole = MichuHomesRoles.RealEstateAdmin,
                Datatype = EntityAffecteds.ApartmentHouses.ToString(),
                DataKey = house.Id.ToString(),
                RedirectUrl = $"/ApartmentHouses/Edit?id={house.Id}",
                CreatedAt = DateTime.Now,
            });
            _audit.Add(AuditCategories.Uploads, approve ? "ApartmentApproved" : "ApartmentRejected",
                $"\"{house.Title}\" {(approve ? "approved" : "rejected")}{(reason is null ? "" : $" ({reason})")}.", companyId, nameof(ApartmentHouse), house.Id.ToString());
            await _db.SaveChangesAsync();
            return AccessResult.Ok(approve ? $"\"{house.Title}\" approved." : $"\"{house.Title}\" rejected.");
        }

        /// <summary>The company sends a corrected, rejected row back for review.</summary>
        public async Task<AccessResult> ResubmitAsync(UserAccess actor, Guid rowId)
        {
            var row = await _db.UploadBatchRows.Include(r => r.Batch).Include(r => r.ApartmentHouse).FirstOrDefaultAsync(r => r.Id == rowId);
            if (row is null || !actor.Can(Perm.HousesUpdate, row.Batch.CompanyId)) return AccessResult.Fail("That apartment isn't yours to resubmit.");
            if (row.Status != UploadRowStatuses.Rejected) return AccessResult.Fail("Only rejected apartments can be sent back for review.");

            row.Status = UploadRowStatuses.Pending;
            row.Resubmissions++;
            if (row.ApartmentHouse.Status == "Rejected") row.ApartmentHouse.Status = "Pending";
            var rows = await _db.UploadBatchRows.Where(r => r.BatchId == row.BatchId).ToListAsync();
            row.Batch.Status = BatchStatus(rows);
            _db.Notifications.Add(new Notification
            {
                Title = "Corrected apartment to review",
                Message = $"\"{row.ApartmentHouse.Title}\" from upload {row.Batch.Reference} was corrected and sent back.",
                ReceiverCompanyId = row.Batch.CompanyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                Datatype = EntityAffecteds.ApartmentHouses.ToString(),
                DataKey = row.BatchId.ToString(),
                RedirectUrl = $"/Staff/Uploads/Details?id={row.BatchId}",
                CreatedAt = DateTime.Now,
            });
            _audit.Add(AuditCategories.Uploads, "UploadRowResubmitted", $"\"{row.ApartmentHouse.Title}\" (batch {row.Batch.Reference}) sent back for review.",
                row.Batch.CompanyId, nameof(UploadBatch), row.BatchId.ToString());
            await _db.SaveChangesAsync();
            return AccessResult.Ok("Sent back for review.");
        }

        public static string BatchStatus(IEnumerable<UploadBatchRow> rows)
        {
            var list = rows.ToList();
            if (list.Any(r => r.Status == UploadRowStatuses.Pending)) return UploadBatchStatuses.PendingReview;
            if (list.All(r => r.Status == UploadRowStatuses.Approved)) return UploadBatchStatuses.Approved;
            if (list.All(r => r.Status == UploadRowStatuses.Rejected)) return UploadBatchStatuses.Rejected;
            return UploadBatchStatuses.PartlyApproved;
        }

        private static string Normalize(string houseNumber) => houseNumber.Trim().ToUpperInvariant();

        private static double Number(IXLCell cell, string label, double min, double max, List<string> errors)
        {
            var text = cell.GetString().Trim();
            if (text.Length == 0) { errors.Add($"{label} is missing"); return 0; }
            if (!(cell.DataType == XLDataType.Number ? cell.TryGetValue(out double v) : double.TryParse(text.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out v)))
            {
                errors.Add($"{label} must be a number"); return 0;
            }
            if (v < min || v > max) { errors.Add($"{label} must be between {min:N0} and {max:N0}"); return 0; }
            return v;
        }

        private static int Whole(IXLCell cell, string label, int min, int max, List<string> errors, string? rangeMessage = null, bool optional = false)
        {
            var text = cell.GetString().Trim();
            if (text.Length == 0)
            {
                if (!optional) errors.Add($"{label} is missing");
                return 0;
            }
            if (!double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) || v != Math.Floor(v)) { errors.Add($"{label} must be a whole number"); return 0; }
            if (v < min || v > max) { errors.Add(rangeMessage ?? $"{label} must be between {min} and {max}"); return 0; }
            return (int)v;
        }
    }
}
