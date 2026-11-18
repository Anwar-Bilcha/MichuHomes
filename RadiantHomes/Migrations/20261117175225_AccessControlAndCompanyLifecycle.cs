using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadiantHomes.Migrations
{
    /// <inheritdoc />
    public partial class AccessControlAndCompanyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessRoles_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyStatusChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ChangedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyStatusChanges_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AssignedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserAccessRoles_AccessRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AccessRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserAccessRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessRolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRolePermissions", x => new { x.RoleId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_AccessRolePermissions_AccessRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AccessRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessRolePermissions_Permissions_PermissionKey",
                        column: x => x.PermissionKey,
                        principalTable: "Permissions",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AccountType",
                table: "AspNetUsers",
                column: "AccountType");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRolePermissions_PermissionKey",
                table: "AccessRolePermissions",
                column: "PermissionKey");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRoles_CompanyId_Name",
                table: "AccessRoles",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CompanyId_OccurredOn",
                table: "AuditLogs",
                columns: new[] { "CompanyId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredOn",
                table: "AuditLogs",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyStatusChanges_CompanyId_ChangedOn",
                table: "CompanyStatusChanges",
                columns: new[] { "CompanyId", "ChangedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessRoles_RoleId",
                table: "UserAccessRoles",
                column: "RoleId");

            // ---- Data: move existing accounts and companies onto the new model (written by hand) ----

            // Account types. Michu Admins become platform staff; Real Estate Admins become their company's staff
            // (their company is taken from their contact profile when the account itself has none).
            migrationBuilder.Sql(@"
UPDATE AspNetUsers SET AccountType = 0 WHERE AccountType IS NULL;

UPDATE u SET HousingCompanyId = p.CompanyId
FROM AspNetUsers u JOIN UserProfiles p ON p.Id = u.CustomerId
WHERE u.HousingCompanyId IS NULL AND p.CompanyId IS NOT NULL
  AND EXISTS (SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE ur.UserId = u.Id AND r.Name = N'Real Estate Admin');

UPDATE u SET AccountType = 2
FROM AspNetUsers u
WHERE u.HousingCompanyId IS NOT NULL
  AND EXISTS (SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE ur.UserId = u.Id AND r.Name = N'Real Estate Admin');

UPDATE u SET AccountType = 1
FROM AspNetUsers u
WHERE EXISTS (SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE ur.UserId = u.Id AND r.Name = N'Michu Admin');

UPDATE u SET FullName = LEFT(LTRIM(RTRIM(CONCAT(p.FirstName, N' ', p.LastName))), 150)
FROM AspNetUsers u JOIN UserProfiles p ON p.Id = u.CustomerId
WHERE u.AccountType IN (1, 2) AND u.FullName IS NULL AND LTRIM(RTRIM(CONCAT(p.FirstName, N' ', p.LastName))) <> N'';
");

            // Company approval statuses. Decision (17 Nov 2026): companies that already have an account linked to them or
            // listed homes start as Approved; all others start as Pending. The old value is kept in the history.
            migrationBuilder.Sql(@"
INSERT INTO CompanyStatusChanges (CompanyId, FromStatus, ToStatus, Reason, ChangedByUserId, ChangedByName, ChangedOn)
SELECT c.CompanyId, LEFT(c.Status, 30), x.NewStatus,
       LEFT(CONCAT(N'Company approval introduced. Previous status: ""', c.Status, N'"".'), 1000), NULL, N'System', SYSUTCDATETIME()
FROM HousingCompanies c
CROSS APPLY (SELECT CASE
    WHEN EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.HousingCompanyId = c.CompanyId)
      OR EXISTS (SELECT 1 FROM ApartmentHouses a
                 JOIN Buildings b ON b.Id = a.BuildingId
                 JOIN Projects p ON p.ProjectId = b.ProjectId
                 WHERE p.HousingCompanyId = c.CompanyId AND a.Status = N'Approved' AND a.IsAvailable = 1)
    THEN N'Approved' ELSE N'Pending' END AS NewStatus) x;

UPDATE c SET Status = h.ToStatus
FROM HousingCompanies c JOIN CompanyStatusChanges h ON h.CompanyId = c.CompanyId;
");

            // Built-in roles and the first assignments. Their permissions are filled in from the catalog at start-up
            // (Services/Security/AccessControlSeeder.cs).
            migrationBuilder.Sql(@"
INSERT INTO AccessRoles (Id, Name, Description, Scope, CompanyId, IsSystem, IsActive, CreatedBy, CreatedOn)
VALUES ('6f1c2b8e-3d4a-4f55-9a61-0c7e2d9b1a01', N'Platform Super Admin', N'Built-in. Every platform permission.', 1, NULL, 1, 1, N'System', SYSDATETIME());

INSERT INTO AccessRoles (Id, Name, Description, Scope, CompanyId, IsSystem, IsActive, CreatedBy, CreatedOn)
SELECT NEWID(), N'Company Super Admin', N'Built-in. Every company permission, including managing the company''s users and roles.', 2, c.CompanyId, 1, 1, N'System', SYSDATETIME()
FROM HousingCompanies c;

INSERT INTO UserAccessRoles (UserId, RoleId, AssignedBy, AssignedOn)
SELECT u.Id, '6f1c2b8e-3d4a-4f55-9a61-0c7e2d9b1a01', N'System', SYSUTCDATETIME() FROM AspNetUsers u WHERE u.AccountType = 1;

INSERT INTO UserAccessRoles (UserId, RoleId, AssignedBy, AssignedOn)
SELECT u.Id, r.Id, N'System', SYSUTCDATETIME()
FROM AspNetUsers u JOIN AccessRoles r ON r.CompanyId = u.HousingCompanyId AND r.IsSystem = 1 AND r.Name = N'Company Super Admin'
WHERE u.AccountType = 2;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore each company's status from before approval was introduced (its first history entry).
            migrationBuilder.Sql(@"
UPDATE c SET Status = h.FromStatus
FROM HousingCompanies c
CROSS APPLY (SELECT TOP 1 FromStatus FROM CompanyStatusChanges WHERE CompanyId = c.CompanyId ORDER BY Id) h
WHERE h.FromStatus IS NOT NULL;
");

            migrationBuilder.DropTable(
                name: "AccessRolePermissions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CompanyStatusChanges");

            migrationBuilder.DropTable(
                name: "UserAccessRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "AccessRoles");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AccountType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");
        }
    }
}
