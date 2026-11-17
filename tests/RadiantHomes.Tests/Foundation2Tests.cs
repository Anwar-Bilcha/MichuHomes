using System.Net;
using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Kyc;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.Kyc;
using RadiantHomes.Services.Properties;
using RadiantHomes.Services.Security;
using RadiantHomes.Services.Settings;
using RadiantHomes.Tests.Infrastructure;

namespace RadiantHomes.Tests
{
    /// <summary>Foundation 2: reservation integrity and expiry, KYC, bulk upload review, visibility of unlisted apartments.</summary>
    [Collection(DatabaseCollection.Name)]
    public class Foundation2Tests
    {
        private readonly TestDatabase _database;
        public Foundation2Tests(TestDatabase database) => _database = database;

        // ---------- Reservations ----------

        [Fact]
        public async Task Two_customers_reserving_the_same_home_at_once_only_one_wins()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var first = await Seed.CustomerAsync(db);
            var second = await Seed.CustomerAsync(db);

            await using var a = new AppServices(_database);
            await using var b = new AppServices(_database);
            var results = await Task.WhenAll(
                a.Get<ReservationService>().ReserveAsync(listing.House.Id, first.Id, "First", null),
                b.Get<ReservationService>().ReserveAsync(listing.House.Id, second.Id, "Second", null));

            Assert.Single(results, r => r.Succeeded);
            Assert.Single(results, r => r.Outcome == ReservationOutcome.NotAvailable);
            await using var check = _database.NewContext();
            Assert.Equal(1, await check.ApartmentHouseSaleTransactions.CountAsync(t => t.ApartmentHouseId == listing.House.Id && (t.Status == HouseReserveStatuses.Pending || t.Status == HouseReserveStatuses.Approved)));
        }

        [Fact]
        public async Task A_reservation_gets_a_reference_an_answer_time_and_history()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var customer = await Seed.CustomerAsync(db);
            await using var s = new AppServices(_database);
            var result = await s.Get<ReservationService>().ReserveAsync(listing.House.Id, customer.Id, "Customer", "Call me");
            Assert.True(result.Succeeded, result.Message);
            Assert.Matches(@"^R-\d{4}-[2-9A-HJ-NP-Z]{5}$", result.Reference);

            await using var check = _database.NewContext();
            var t = await check.ApartmentHouseSaleTransactions.Include(x => x.History).SingleAsync(x => x.Id == result.TransactionId);
            Assert.Equal(listing.Company.CompanyId, t.CompanyId);
            Assert.InRange(t.ExpiresAt!.Value, DateTime.UtcNow.AddHours(71), DateTime.UtcNow.AddHours(73)); // default 72 hours
            var h = Assert.Single(t.History);
            Assert.Null(h.FromStatus);
            Assert.Equal(HouseReserveStatuses.Pending, h.ToStatus);
        }

        [Fact]
        public async Task Declining_needs_a_reason_and_lists_the_home_again()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var customer = await Seed.CustomerAsync(db);
            var staff = await Seed.StaffAsync(db, AccountType.CompanyStaff, listing.Company.CompanyId, (await Seed.CompanySuperAdminAsync(db, listing.Company.CompanyId)).Id);
            await using var s = new AppServices(_database, staff.Id);
            var reservations = s.Get<ReservationService>();
            var reserved = await reservations.ReserveAsync(listing.House.Id, customer.Id, "C", null);
            var scope = await CompanyScope.ForAsync(Principal(staff.Id), s.Db);

            Assert.False((await reservations.ChangeStatusAsync(reserved.TransactionId!.Value, HouseReserveStatuses.Rejected, scope)).Succeeded);
            Assert.True((await reservations.ChangeStatusAsync(reserved.TransactionId!.Value, HouseReserveStatuses.Rejected, scope, "Already promised to another buyer")).Succeeded);

            await using var check = _database.NewContext();
            var house = await check.ApartmentHouses.SingleAsync(x => x.Id == listing.House.Id);
            Assert.True(house.IsAvailable);
            Assert.Equal("Approved", house.Status);
            Assert.True(await check.Notifications.AnyAsync(n => n.Title == "Reservation declined" && n.Message.Contains("Already promised")));
        }

        [Fact]
        public async Task Overdue_reservations_expire_but_a_hidden_home_stays_hidden()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var hiddenListing = await Seed.ListingAsync(db);
            var customer = await Seed.CustomerAsync(db);
            await using var s = new AppServices(_database);
            var reservations = s.Get<ReservationService>();
            var r1 = await reservations.ReserveAsync(listing.House.Id, customer.Id, "C", null);
            var r2 = await reservations.ReserveAsync(hiddenListing.House.Id, customer.Id, "C", null);
            await using (var admin = _database.NewContext())
            {
                var h = await admin.ApartmentHouses.SingleAsync(x => x.Id == hiddenListing.House.Id);
                h.Status = "Hidden"; // an admin hid it while the reservation was pending
                await admin.SaveChangesAsync();
            }

            await using (var job = new AppServices(_database)) Assert.Equal(0, await job.Get<ReservationService>().ExpireDueAsync(DateTime.UtcNow.AddHours(1))); // not due yet
            await using (var job = new AppServices(_database)) Assert.True(await job.Get<ReservationService>().ExpireDueAsync(DateTime.UtcNow.AddHours(80)) >= 2); // each run uses a fresh scope, like the job

            await using var check = _database.NewContext();
            Assert.Equal(HouseReserveStatuses.Expired, (await check.ApartmentHouseSaleTransactions.SingleAsync(t => t.Id == r1.TransactionId)).Status);
            Assert.Equal(HouseReserveStatuses.Expired, (await check.ApartmentHouseSaleTransactions.SingleAsync(t => t.Id == r2.TransactionId)).Status);
            Assert.True((await check.ApartmentHouses.SingleAsync(x => x.Id == listing.House.Id)).IsAvailable);
            Assert.Equal("Hidden", (await check.ApartmentHouses.SingleAsync(x => x.Id == hiddenListing.House.Id)).Status);
        }

        [Fact]
        public async Task The_answer_time_is_a_platform_setting_and_zero_means_no_expiry()
        {
            await using var db = _database.NewContext();
            var role = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.SettingsManage);
            var admin = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, role.Id);
            var listing = await Seed.ListingAsync(db);
            var customer = await Seed.CustomerAsync(db);
            await using var s = new AppServices(_database, admin.Id);
            var actor = await s.Get<AccessControlService>().LoadAsync(admin.Id);
            var settings = s.Get<PlatformSettings>();
            try
            {
                Assert.False((await settings.SetIntAsync(actor, SettingKeys.ReservationPendingExpiryHours, -1)).Succeeded);
                Assert.True((await settings.SetIntAsync(actor, SettingKeys.ReservationPendingExpiryHours, 0)).Succeeded);
                await using var r = new AppServices(_database);
                var result = await r.Get<ReservationService>().ReserveAsync(listing.House.Id, customer.Id, "C", null);
                await using var check = _database.NewContext();
                Assert.Null((await check.ApartmentHouseSaleTransactions.SingleAsync(t => t.Id == result.TransactionId)).ExpiresAt);
                Assert.True(await check.AuditLogs.AnyAsync(a => a.Category == AuditCategories.Settings));
            }
            finally
            {
                await settings.SetIntAsync(actor, SettingKeys.ReservationPendingExpiryHours, 72);
            }
        }

        [Fact]
        public async Task The_database_refuses_double_bookings_double_sales_and_lost_updates()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var customer = await Seed.CustomerAsync(db);
            ApartmentHouseSaleTransaction New(HouseReserveStatuses status) => new()
            {
                Id = Guid.NewGuid(), ApartmentHouseId = listing.House.Id, CustomerId = customer.Id, OwnerId = listing.ContactPerson.Id,
                Status = status, Notes = string.Empty, Reference = ReservationService.NewReference(),
            };

            await using (var c1 = _database.NewContext())
            {
                c1.ApartmentHouseSaleTransactions.AddRange(New(HouseReserveStatuses.Pending), New(HouseReserveStatuses.Approved));
                await Assert.ThrowsAsync<DbUpdateException>(() => c1.SaveChangesAsync());
            }
            await using (var c2 = _database.NewContext())
            {
                c2.ApartmentHouseSaleTransactions.AddRange(New(HouseReserveStatuses.Completed), New(HouseReserveStatuses.Completed));
                await Assert.ThrowsAsync<DbUpdateException>(() => c2.SaveChangesAsync());
            }

            var one = New(HouseReserveStatuses.Pending);
            db.ApartmentHouseSaleTransactions.Add(one);
            await db.SaveChangesAsync();
            await using var x = _database.NewContext();
            await using var y = _database.NewContext();
            var tx = await x.ApartmentHouseSaleTransactions.SingleAsync(t => t.Id == one.Id);
            var ty = await y.ApartmentHouseSaleTransactions.SingleAsync(t => t.Id == one.Id);
            tx.Status = HouseReserveStatuses.Approved;
            await x.SaveChangesAsync();
            ty.Status = HouseReserveStatuses.Rejected;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => y.SaveChangesAsync());
        }

        // ---------- KYC ----------

        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };
        private static IFormFile File(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);

        private static KycSubmission Valid() => new()
        {
            DocumentType = "Passport", DocumentNumber = "EP1234567", FullNameOnDocument = "Test Customer",
            DateOfBirth = new DateTime(1990, 5, 1), DocumentExpiresOn = DateTime.Today.AddYears(3),
            Front = File("front.jpg", Jpeg), Selfie = File("me.jpg", Jpeg),
        };

        [Fact]
        public async Task Kyc_submissions_are_validated_and_stored_for_review()
        {
            await using var db = _database.NewContext();
            var customer = await Seed.CustomerAsync(db);
            var account = await Seed.CustomerAccountAsync(db, customer);
            await using var s = new AppServices(_database, account.Id);
            var kyc = s.Get<KycService>();

            var young = Valid(); young.DateOfBirth = DateTime.Today.AddYears(-16);
            Assert.False((await kyc.SubmitAsync(customer.Id, account.Id, young)).Succeeded);
            var fake = Valid(); fake.Front = File("front.jpg", "not an image"u8.ToArray());
            Assert.Contains("doesn't match", (await kyc.SubmitAsync(customer.Id, account.Id, fake)).Message);
            var expired = Valid(); expired.DocumentExpiresOn = DateTime.Today.AddDays(-1);
            Assert.False((await kyc.SubmitAsync(customer.Id, account.Id, expired)).Succeeded);

            var ok = await kyc.SubmitAsync(customer.Id, account.Id, Valid());
            Assert.True(ok.Succeeded, ok.Message);
            Assert.False((await kyc.SubmitAsync(customer.Id, account.Id, Valid())).Succeeded); // already being processed

            await using var check = _database.NewContext();
            Assert.Equal(KycStatuses.Pending, (await check.UserProfiles.SingleAsync(p => p.Id == customer.Id)).KycStatus);
            var application = await check.KycApplications.Include(a => a.Documents).SingleAsync(a => a.Id == Guid.Parse(ok.Id!));
            Assert.Equal(2, application.Documents.Count);
            Assert.All(application.Documents, d => Assert.True(System.IO.File.Exists(Path.Combine(kyc.Folder(application.Id), d.StoredFileName))));
            Assert.DoesNotContain("wwwroot", kyc.Folder(application.Id));
            Assert.True(await check.Notifications.AnyAsync(n => n.DataKey == ok.Id && n.Title == "Identity verification to review"));
        }

        [Fact]
        public async Task Only_reviewers_decide_and_only_approved_customers_are_verified()
        {
            await using var db = _database.NewContext();
            var customer = await Seed.CustomerAsync(db);
            var account = await Seed.CustomerAccountAsync(db, customer);
            var reviewerRole = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.KycView, Perm.KycReview);
            var reviewer = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, reviewerRole.Id);
            var revokerRole = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.KycRevoke);
            var revoker = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, revokerRole.Id);
            var company = await Seed.CompanyAsync(db);
            var companyAdmin = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, (await Seed.CompanySuperAdminAsync(db, company.CompanyId)).Id);

            await using var c = new AppServices(_database, account.Id);
            var app1 = Guid.Parse((await c.Get<KycService>().SubmitAsync(customer.Id, account.Id, Valid())).Id!);

            await using var s = new AppServices(_database, reviewer.Id);
            var kyc = s.Get<KycService>();
            var reviewerAccess = await s.Get<AccessControlService>().LoadAsync(reviewer.Id);
            var companyAccess = await s.Get<AccessControlService>().LoadAsync(companyAdmin.Id);
            Assert.False((await kyc.DecideAsync(companyAccess, app1, KycAction.Approve, null, "x")).Succeeded);
            Assert.False((await kyc.DecideAsync(reviewerAccess, app1, KycAction.RequestResubmission, " ", "r")).Succeeded);
            Assert.True((await kyc.DecideAsync(reviewerAccess, app1, KycAction.RequestResubmission, "Photo is blurry", "r")).Succeeded);
            Assert.False(await kyc.IsVerifiedAsync(customer.Id));

            await using var c2 = new AppServices(_database, account.Id); // a new request
            var app2 = Guid.Parse((await c2.Get<KycService>().SubmitAsync(customer.Id, account.Id, Valid())).Id!); // resubmission
            Assert.False((await kyc.DecideAsync(reviewerAccess, app1, KycAction.Approve, null, "r")).Succeeded); // only the newest counts
            Assert.True((await kyc.DecideAsync(reviewerAccess, app2, KycAction.Approve, null, "r")).Succeeded);
            Assert.True(await kyc.IsVerifiedAsync(customer.Id));

            Assert.False((await kyc.DecideAsync(reviewerAccess, app2, KycAction.Revoke, "Fraud suspected", "r")).Succeeded); // needs kyc.revoke
            var revokerAccess = await s.Get<AccessControlService>().LoadAsync(revoker.Id);
            Assert.True((await kyc.DecideAsync(revokerAccess, app2, KycAction.Revoke, "Fraud suspected", "x")).Succeeded);
            Assert.False(await kyc.IsVerifiedAsync(customer.Id));

            await using var check = _database.NewContext();
            Assert.Equal(new[] { KycStatuses.Pending, KycStatuses.Approved, KycStatuses.Revoked },
                await check.KycStatusChanges.Where(h => h.ApplicationId == app2).OrderBy(h => h.Id).Select(h => h.ToStatus).ToListAsync());
        }

        [Fact]
        public async Task Kyc_documents_open_only_for_reviewers_or_their_owner_and_openings_are_audited()
        {
            await using var db = _database.NewContext();
            var customer = await Seed.CustomerAsync(db);
            var other = await Seed.CustomerAsync(db);
            var account = await Seed.CustomerAccountAsync(db, customer);
            var viewerRole = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.KycView);
            var viewer = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, viewerRole.Id);
            await using var s = new AppServices(_database, account.Id);
            var kyc = s.Get<KycService>();
            var appId = Guid.Parse((await kyc.SubmitAsync(customer.Id, account.Id, Valid())).Id!);
            var docId = (await db.KycDocuments.FirstAsync(d => d.ApplicationId == appId)).Id;

            var nobody = UserAccess.Anonymous;
            Assert.Null(await kyc.OpenDocumentAsync(nobody, docId, other.Id));
            Assert.NotNull(await kyc.OpenDocumentAsync(nobody, docId, customer.Id));
            Assert.NotNull(await kyc.OpenDocumentAsync(await s.Get<AccessControlService>().LoadAsync(viewer.Id), docId, null));
            await using var check = _database.NewContext();
            Assert.Equal(1, await check.AuditLogs.CountAsync(a => a.Action == "KycDocumentViewed" && a.EntityId == appId.ToString()));
        }

        // ---------- Bulk upload ----------

        private static IFormFile Workbook(params object?[][] rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Apartments");
            ws.Cell(1, 1).Value = "Title";
            for (var r = 0; r < rows.Length; r++)
                for (var c = 0; c < rows[r].Length; c++)
                    ws.Cell(r + 2, c + 1).Value = rows[r][c] switch { null => Blank.Value, double d => d, int i => (double)i, bool b => b, var o => o.ToString() };
            var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;
            return new FormFile(ms, 0, ms.Length, "ExcelFile", "apartments.xlsx");
        }

        // Template columns: title, number, description, price, available for, status, floor, baths, rooms, available, currency, living, bedrooms, balcony, elevator, parking, furnished, rent, photo, type
        private static object?[] Row(string? title, string number, double price, int floor) =>
            new object?[] { title, number, "Nice", price, "Sell", "Approved", floor, 1, 4, "Yes", "ETB", 1, 2, "Yes", "No", "No", "No", 0, "", "Residential" };

        [Fact]
        public async Task Every_invalid_upload_row_is_reported_and_nothing_is_saved()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var admin = await Seed.StaffAsync(db, AccountType.CompanyStaff, listing.Company.CompanyId, (await Seed.CompanySuperAdminAsync(db, listing.Company.CompanyId)).Id);
            await using var s = new AppServices(_database, admin.Id);
            var access = await s.Get<AccessControlService>().LoadAsync(admin.Id);
            var scope = await CompanyScope.ForAsync(Principal(admin.Id), s.Db);
            var before = await db.ApartmentHouses.CountAsync(a => a.BuildingId == listing.Building.Id);

            var result = await s.Get<BulkUploadService>().UploadAsync(scope, access, "a", listing.Building.Id, Workbook(
                Row(null, "B1", 100, 1),                      // no title
                Row("Dup", "B2", 100, 1), Row("Dup", "B2", 100, 1), // duplicate number in the file
                Row("High", "B3", 100, 99),                   // floor above the building's 10 floors
                Row("Cheap", "B4", -5, 1),                    // negative price
                Row("Taken", listing.House.HouseNumber!, 100, 1))); // number already in the building

            Assert.False(result.Succeeded);
            Assert.Equal(5, result.Errors.Count);
            Assert.Contains(result.Errors, e => e.Contains("Row 2") && e.Contains("title"));
            Assert.Contains(result.Errors, e => e.Contains("Row 5") && e.Contains("floor must be between -5 and 10"));
            Assert.Equal(before, await db.ApartmentHouses.CountAsync(a => a.BuildingId == listing.Building.Id));
            Assert.False((await s.Get<BulkUploadService>().UploadAsync(scope, access, "a", listing.Building.Id, File("x.xlsx", "garbage"u8.ToArray()))).Succeeded);
        }

        [Fact]
        public async Task A_valid_upload_waits_for_review_and_rejected_rows_can_be_resubmitted()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var companyAdmin = await Seed.StaffAsync(db, AccountType.CompanyStaff, listing.Company.CompanyId, (await Seed.CompanySuperAdminAsync(db, listing.Company.CompanyId)).Id);
            var reviewer = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, PermissionCatalog.PlatformSuperAdminRoleId);

            await using var c = new AppServices(_database, companyAdmin.Id);
            var companyAccess = await c.Get<AccessControlService>().LoadAsync(companyAdmin.Id);
            var scope = await CompanyScope.ForAsync(Principal(companyAdmin.Id), c.Db);
            var upload = await c.Get<BulkUploadService>().UploadAsync(scope, companyAccess, "c", listing.Building.Id, Workbook(Row("One", "U1", 100, 1), Row("Two", "U2", 100, 2), Row("Three", "U3", 100, 3)));
            Assert.True(upload.Succeeded, string.Join(" | ", upload.Errors));
            var batchId = upload.Batch!.Id;

            await using (var check = _database.NewContext())
            {
                var houses = await check.ApartmentHouses.Where(a => a.BuildingId == listing.Building.Id && a.HouseNumber!.StartsWith("U")).ToListAsync();
                Assert.All(houses, h => Assert.Equal("Pending", h.Status)); // status column in the file is ignored
            }

            await using var r = new AppServices(_database, reviewer.Id);
            var reviewerAccess = await r.Get<AccessControlService>().LoadAsync(reviewer.Id);
            var rows = await r.Db.UploadBatchRows.Where(x => x.BatchId == batchId).OrderBy(x => x.RowNumber).ToListAsync();
            var uploads = r.Get<BulkUploadService>();
            Assert.False((await uploads.DecideAsync(companyAccess, "c", batchId, new[] { rows[0].Id }, Array.Empty<Guid>(), null)).Succeeded); // companies can't approve
            Assert.False((await uploads.DecideAsync(reviewerAccess, "r", batchId, new[] { rows[0].Id, rows[1].Id }, new[] { rows[2].Id }, null)).Succeeded); // reason needed
            Assert.True((await uploads.DecideAsync(reviewerAccess, "r", batchId, new[] { rows[0].Id, rows[1].Id }, new[] { rows[2].Id }, "Price missing VAT")).Succeeded);

            await using (var check = _database.NewContext())
            {
                Assert.Equal(UploadBatchStatuses.PartlyApproved, (await check.UploadBatches.SingleAsync(b => b.Id == batchId)).Status);
                Assert.Equal("Rejected", (await check.ApartmentHouses.SingleAsync(a => a.Id == rows[2].ApartmentHouseId)).Status);
                Assert.Equal("Approved", (await check.ApartmentHouses.SingleAsync(a => a.Id == rows[0].ApartmentHouseId)).Status);
            }

            await using var c2 = new AppServices(_database, companyAdmin.Id); // a new request
            Assert.True((await c2.Get<BulkUploadService>().ResubmitAsync(companyAccess, rows[2].Id)).Succeeded);
            await using (var check = _database.NewContext())
            {
                Assert.Equal(UploadBatchStatuses.PendingReview, (await check.UploadBatches.SingleAsync(b => b.Id == batchId)).Status);
                Assert.Equal("Pending", (await check.ApartmentHouses.SingleAsync(a => a.Id == rows[2].ApartmentHouseId)).Status);
            }
        }

        // ---------- Pages ----------

        [Fact]
        public async Task Unlisted_apartments_are_hidden_from_the_public_but_not_from_their_company()
        {
            await using var db = _database.NewContext();
            var listing = await Seed.ListingAsync(db);
            var pending = await Seed.ApartmentAsync(db, listing.Building, listing.Company.AddressId, listing.House.HouseCategoryId, "P1", status: "Pending");
            var staff = await Seed.StaffAsync(db, AccountType.CompanyStaff, listing.Company.CompanyId, (await Seed.CompanySuperAdminAsync(db, listing.Company.CompanyId)).Id);
            await using var app = new AppFactory(_database);

            Assert.Equal(HttpStatusCode.OK, (await app.ClientFor(null).GetAsync($"/ApartmentHouses/Details?id={listing.House.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await app.ClientFor(null).GetAsync($"/ApartmentHouses/Details?id={pending.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await app.ClientFor(staff.Id).GetAsync($"/ApartmentHouses/Details?id={pending.Id}")).StatusCode);
            var building = await (await app.ClientFor(null).GetAsync($"/Buildings/Details?id={listing.Building.Id}")).Content.ReadAsStringAsync();
            Assert.Contains(listing.House.Title, building);
            Assert.DoesNotContain(pending.Title, building);
        }

        [Fact]
        public async Task Video_calls_and_kyc_review_pages_follow_verification_and_permissions()
        {
            await using var db = _database.NewContext();
            var unverified = await Seed.CustomerAccountAsync(db, await Seed.CustomerAsync(db));
            var verified = await Seed.CustomerAccountAsync(db, await Seed.CustomerAsync(db, kycStatus: KycStatuses.Approved));
            var reviewerRole = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.KycView);
            var reviewer = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, reviewerRole.Id);
            await using var app = new AppFactory(_database);

            Assert.Contains("Video calls are for verified customers", await (await app.ClientFor(unverified.Id).GetAsync("/LiveVideoMeet")).Content.ReadAsStringAsync());
            Assert.DoesNotContain("Video calls are for verified customers", await (await app.ClientFor(verified.Id).GetAsync("/LiveVideoMeet")).Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Forbidden, (await app.ClientFor(unverified.Id).GetAsync("/Admin/Kyc")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await app.ClientFor(reviewer.Id).GetAsync("/Admin/Kyc")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await app.ClientFor(unverified.Id).GetAsync("/Portal/Kyc")).StatusCode);
        }

        private static ClaimsPrincipal Principal(string userId) =>
            new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test"));
    }
}
