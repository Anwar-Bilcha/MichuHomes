using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadiantHomes.Migrations
{
    /// <inheritdoc />
    public partial class TrustAndIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KycStatus",
                table: "UserProfiles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotSubmitted");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ApartmentHouseSaleTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "ApartmentHouseSaleTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "ApartmentHouseSaleTransactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ApartmentHouseSaleTransactions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "KycApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FullNameOnDocument = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentExpiresOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KycApplications_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ReservationStatusChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ChangedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationStatusChanges_ApartmentHouseSaleTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "ApartmentHouseSaleTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UploadBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UploadedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadBatches_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadBatches_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KycDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KycDocuments_KycApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "KycApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KycStatusChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ChangedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KycStatusChanges_KycApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "KycApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UploadBatchRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Resubmissions = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadBatchRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadBatchRows_ApartmentHouses_ApartmentHouseId",
                        column: x => x.ApartmentHouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadBatchRows_UploadBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "UploadBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---- Data (written by hand), before the unique indexes below ----

            // Existing reservations: a reference (from the id, unique) and the selling company. They get no expiry:
            // only reservations made from now on expire automatically.
            migrationBuilder.Sql(@"
UPDATE ApartmentHouseSaleTransactions
SET Reference = CONCAT(N'R-', UPPER(LEFT(REPLACE(CONVERT(nvarchar(36), Id), N'-', N''), 10)))
WHERE Reference = N'';

UPDATE t SET CompanyId = p.HousingCompanyId
FROM ApartmentHouseSaleTransactions t
JOIN ApartmentHouses a ON a.Id = t.ApartmentHouseId
JOIN Buildings b ON b.Id = a.BuildingId
JOIN Projects p ON p.ProjectId = b.ProjectId
WHERE t.CompanyId IS NULL;
");

            // Double bookings made before this protection existed: per apartment, keep the most advanced active
            // reservation (approved before pending, then the earliest) and close the others as Cancelled, with a note.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT Id, Status,
           ROW_NUMBER() OVER (PARTITION BY ApartmentHouseId ORDER BY Status DESC, TransactionDate, Id) AS n
    FROM ApartmentHouseSaleTransactions
    WHERE Status IN (0, 1)
)
INSERT INTO ReservationStatusChanges (TransactionId, FromStatus, ToStatus, Reason, ChangedByUserId, ChangedByName, ChangedOn)
SELECT Id, Status, 3, N'Duplicate active reservation of the same apartment, closed when double-booking protection was introduced.', NULL, N'System', SYSUTCDATETIME()
FROM ranked WHERE n > 1;

WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY ApartmentHouseId ORDER BY Status DESC, TransactionDate, Id) AS n
    FROM ApartmentHouseSaleTransactions
    WHERE Status IN (0, 1)
)
UPDATE t SET Status = 3
FROM ApartmentHouseSaleTransactions t JOIN ranked r ON r.Id = t.Id
WHERE r.n > 1;

-- Same for more than one completed sale of an apartment (kept: the earliest).
WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY ApartmentHouseId ORDER BY TransactionDate, Id) AS n
    FROM ApartmentHouseSaleTransactions
    WHERE Status = 2
)
UPDATE t SET Status = 3, Notes = CONCAT(t.Notes, N' [Duplicate sale closed when double-booking protection was introduced.]')
FROM ApartmentHouseSaleTransactions t JOIN ranked r ON r.Id = t.Id
WHERE r.n > 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_CompanyId",
                table: "ApartmentHouseSaleTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_OneActivePerApartment",
                table: "ApartmentHouseSaleTransactions",
                column: "ApartmentHouseId",
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_OneSalePerApartment",
                table: "ApartmentHouseSaleTransactions",
                column: "ApartmentHouseId",
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_Reference",
                table: "ApartmentHouseSaleTransactions",
                column: "Reference",
                unique: true,
                filter: "[Reference] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_Status_ExpiresAt",
                table: "ApartmentHouseSaleTransactions",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KycApplications_CustomerId_SubmittedOn",
                table: "KycApplications",
                columns: new[] { "CustomerId", "SubmittedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_KycApplications_Status_SubmittedOn",
                table: "KycApplications",
                columns: new[] { "Status", "SubmittedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_ApplicationId",
                table: "KycDocuments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_KycStatusChanges_ApplicationId",
                table: "KycStatusChanges",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationStatusChanges_TransactionId_ChangedOn",
                table: "ReservationStatusChanges",
                columns: new[] { "TransactionId", "ChangedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_BuildingId",
                table: "UploadBatches",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_CompanyId",
                table: "UploadBatches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_Reference",
                table: "UploadBatches",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_Status_UploadedOn",
                table: "UploadBatches",
                columns: new[] { "Status", "UploadedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatchRows_ApartmentHouseId",
                table: "UploadBatchRows",
                column: "ApartmentHouseId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatchRows_BatchId",
                table: "UploadBatchRows",
                column: "BatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KycDocuments");

            migrationBuilder.DropTable(
                name: "KycStatusChanges");

            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "ReservationStatusChanges");

            migrationBuilder.DropTable(
                name: "UploadBatchRows");

            migrationBuilder.DropTable(
                name: "KycApplications");

            migrationBuilder.DropTable(
                name: "UploadBatches");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentHouseSaleTransactions_CompanyId",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentHouseSaleTransactions_OneActivePerApartment",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentHouseSaleTransactions_OneSalePerApartment",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentHouseSaleTransactions_Reference",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentHouseSaleTransactions_Status_ExpiresAt",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ApartmentHouseSaleTransactions");
        }
    }
}
