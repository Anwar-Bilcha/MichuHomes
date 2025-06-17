using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadiantHomes.Migrations
{
    /// <inheritdoc />
    public partial class RecreatedInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    AddressID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Woreda = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocationMain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationSub = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.AddressID);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommisionRates",
                columns: table => new
                {
                    CommisionRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CommisionServiceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommisionRateDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommisionPercent = table.Column<double>(type: "float", nullable: false),
                    CommissionType = table.Column<int>(type: "int", nullable: false),
                    OneTimeOrReccuring = table.Column<int>(type: "int", nullable: false),
                    CommisionFixedAmount = table.Column<double>(type: "float", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommisionRates", x => x.CommisionRateId);
                });

            migrationBuilder.CreateTable(
                name: "HomeOwnerRankings",
                columns: table => new
                {
                    RankingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    RankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneVerified = table.Column<bool>(type: "bit", nullable: true),
                    IdVerified = table.Column<bool>(type: "bit", nullable: true),
                    AddressVerified = table.Column<bool>(type: "bit", nullable: true),
                    PaymentVerified = table.Column<bool>(type: "bit", nullable: true),
                    SoldHouseBefore = table.Column<bool>(type: "bit", nullable: true),
                    BoughtHouseBefore = table.Column<bool>(type: "bit", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeOwnerRankings", x => x.RankingId);
                });

            migrationBuilder.CreateTable(
                name: "HouseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    RedirectUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecipientRole = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Translations",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmharicValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnglishValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfanOromoValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdditionalNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Page = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Translations", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HousingCompanies",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstablishedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyCategory = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingCompanies", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_HousingCompanies_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HousingCompanyFinancialDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnnualRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    TinNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    hasAuditedFinancialStatement = table.Column<bool>(type: "bit", nullable: false),
                    AuditedFinancialStatementUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BudgetYear = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingCompanyFinancialDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousingCompanyFinancialDetails_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    HousingCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalArea = table.Column<double>(type: "float", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_Projects_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Projects_HousingCompanies_HousingCompanyId",
                        column: x => x.HousingCompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RankingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerCategory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentUsed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdCardFileUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProfileStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HomeOwnerRankingRankingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartyAddressAddressID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressID");
                    table.ForeignKey(
                        name: "FK_UserProfiles_Addresses_PartyAddressAddressID",
                        column: x => x.PartyAddressAddressID,
                        principalTable: "Addresses",
                        principalColumn: "AddressID");
                    table.ForeignKey(
                        name: "FK_UserProfiles_HomeOwnerRankings_HomeOwnerRankingRankingId",
                        column: x => x.HomeOwnerRankingRankingId,
                        principalTable: "HomeOwnerRankings",
                        principalColumn: "RankingId");
                    table.ForeignKey(
                        name: "FK_UserProfiles_HomeOwnerRankings_RankingId",
                        column: x => x.RankingId,
                        principalTable: "HomeOwnerRankings",
                        principalColumn: "RankingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: false),
                    BuildingType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConstructionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Buildings_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Buildings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Discriminator = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HousingCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_HousingCompanies_HousingCompanyId",
                        column: x => x.HousingCompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId");
                    table.ForeignKey(
                        name: "FK_AspNetUsers_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTestimonials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestimonialText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTestimonials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerTestimonials_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerTestimonials_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Houses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HouseNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HouseOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalBedRooms = table.Column<int>(type: "int", nullable: false),
                    TotalRooms = table.Column<int>(type: "int", nullable: false),
                    NumberOfBathRooms = table.Column<int>(type: "int", nullable: false),
                    TotalArea = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HasOwnershipDocument = table.Column<bool>(type: "bit", nullable: false),
                    OwnershipDocumentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HouseCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailableFor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseOwnership = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsentofSaleOrRentDocUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LatestHouseTaxVoucherUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HouseHolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MainPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutOfCompoundViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrontCompoundViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HouseInternalViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainSalonViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MasterBedRoomViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KitchenViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RestRoomViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuxiSalonViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecBedRoomViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdditionalViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BackyardViewPhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Additional2PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Houses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Houses_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Houses_HouseCategories_HouseCategoryId",
                        column: x => x.HouseCategoryId,
                        principalTable: "HouseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Houses_UserProfiles_HouseHolderId",
                        column: x => x.HouseHolderId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Houses_UserProfiles_HouseOwnerId",
                        column: x => x.HouseOwnerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentHouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HouseNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SalePrice = table.Column<double>(type: "float", nullable: false),
                    ContactPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HouseCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailableFor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SalesStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FloorNumber = table.Column<int>(type: "int", nullable: false),
                    Bathrooms = table.Column<int>(type: "int", nullable: false),
                    TotalRooms = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<double>(type: "float", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LivingRooms = table.Column<int>(type: "int", nullable: false),
                    Bedrooms = table.Column<int>(type: "int", nullable: false),
                    HasBalcony = table.Column<bool>(type: "bit", nullable: false),
                    HasElevator = table.Column<bool>(type: "bit", nullable: false),
                    HasParking = table.Column<bool>(type: "bit", nullable: false),
                    IsFurnished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentHouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentHouses_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentHouses_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApartmentHouses_HouseCategories_HouseCategoryId",
                        column: x => x.HouseCategoryId,
                        principalTable: "HouseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApartmentHouses_UserProfiles_ContactPersonId",
                        column: x => x.ContactPersonId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HousePerFloors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FloorNumber = table.Column<int>(type: "int", nullable: false),
                    NumberOfHouses = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousePerFloors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousePerFloors_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseRentInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsOnRent = table.Column<bool>(type: "bit", nullable: false),
                    RentTerm = table.Column<int>(type: "int", nullable: false),
                    PricePerTerm = table.Column<double>(type: "float", nullable: false),
                    ExcepectedAdvancePayment = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseRentInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseRentInfos_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseRentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RentStartingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RentEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseRentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseRentTransactions_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseRentTransactions_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseRentTransactions_UserProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseSaleTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseSaleTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseSaleTransactions_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseSaleTransactions_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseSaleTransactions_UserProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentHouseReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentHouseReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentHouseReviews_ApartmentHouses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApartmentHouseReviews_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApartmentHouseReviews_UserProfiles_ReviewerCustomerId",
                        column: x => x.ReviewerCustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentHouseSaleTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentHouseSaleTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentHouseSaleTransactions_ApartmentHouses_ApartmentHouseId",
                        column: x => x.ApartmentHouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentHouseSaleTransactions_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentHouseSaleTransactions_UserProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentPhotos",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentPhotos", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_ApartmentPhotos_ApartmentHouses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentPhotos_Buildings_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Buildings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApartmentPhotos_Projects_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "HouseReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseReviews_ApartmentHouses_ApartmentHouseId",
                        column: x => x.ApartmentHouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseReviews_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseReviews_UserProfiles_ReviewerCustomerId",
                        column: x => x.ReviewerCustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseSpecifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecificationCriteria = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecificationDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseSpecifications_ApartmentHouses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IssuesOrCompliants",
                columns: table => new
                {
                    IssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    IssueName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueCategory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailedDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeverityLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupportingEvidenceUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupportingEvidence2Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplainantFullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HowDoWeReachYou = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneOrEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplainantAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuesOrCompliants", x => x.IssueId);
                    table.ForeignKey(
                        name: "FK_IssuesOrCompliants_ApartmentHouses_ApartmentHouseId",
                        column: x => x.ApartmentHouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IssuesOrCompliants_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssuesOrCompliants_UserProfiles_HouseOwnerId",
                        column: x => x.HouseOwnerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseRentPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentCommision = table.Column<double>(type: "float", nullable: false),
                    RentPricePerTerm = table.Column<double>(type: "float", nullable: false),
                    TotalRentPrice = table.Column<double>(type: "float", nullable: false),
                    SalersPreferredBank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BuyersPreferedBank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommisionedPreferedBank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseRentPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseRentPayments_HouseRentTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "HouseRentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseSalePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HousePrice = table.Column<double>(type: "float", nullable: false),
                    SalesCommision = table.Column<double>(type: "float", nullable: false),
                    SalersPreferredBank = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BuyersPreferedBank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommisionedPreferedBank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseSalePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseSalePayments_HouseSaleTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "HouseSaleTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseReviews_CustomerId",
                table: "ApartmentHouseReviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseReviews_HouseId",
                table: "ApartmentHouseReviews",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseReviews_ReviewerCustomerId",
                table: "ApartmentHouseReviews",
                column: "ReviewerCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouses_AddressId",
                table: "ApartmentHouses",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouses_BuildingId",
                table: "ApartmentHouses",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouses_ContactPersonId",
                table: "ApartmentHouses",
                column: "ContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouses_HouseCategoryId",
                table: "ApartmentHouses",
                column: "HouseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_ApartmentHouseId",
                table: "ApartmentHouseSaleTransactions",
                column: "ApartmentHouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_CustomerId",
                table: "ApartmentHouseSaleTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentHouseSaleTransactions_OwnerId",
                table: "ApartmentHouseSaleTransactions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentPhotos_HouseId",
                table: "ApartmentPhotos",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CustomerId",
                table: "AspNetUsers",
                column: "CustomerId",
                unique: true,
                filter: "[CustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_HousingCompanyId",
                table: "AspNetUsers",
                column: "HousingCompanyId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_AddressId",
                table: "Buildings",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_ProjectId",
                table: "Buildings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTestimonials_CompanyId",
                table: "CustomerTestimonials",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTestimonials_CustomerId",
                table: "CustomerTestimonials",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_HousePerFloors_BuildingId",
                table: "HousePerFloors",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseRentInfos_HouseId",
                table: "HouseRentInfos",
                column: "HouseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseRentPayments_TransactionId",
                table: "HouseRentPayments",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseRentTransactions_CustomerId",
                table: "HouseRentTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseRentTransactions_HouseId",
                table: "HouseRentTransactions",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseRentTransactions_OwnerId",
                table: "HouseRentTransactions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseReviews_ApartmentHouseId",
                table: "HouseReviews",
                column: "ApartmentHouseId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseReviews_HouseId",
                table: "HouseReviews",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseReviews_ReviewerCustomerId",
                table: "HouseReviews",
                column: "ReviewerCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_AddressId",
                table: "Houses",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_HouseCategoryId",
                table: "Houses",
                column: "HouseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_HouseHolderId",
                table: "Houses",
                column: "HouseHolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_HouseOwnerId",
                table: "Houses",
                column: "HouseOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseSalePayments_TransactionId",
                table: "HouseSalePayments",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseSaleTransactions_CustomerId",
                table: "HouseSaleTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseSaleTransactions_HouseId",
                table: "HouseSaleTransactions",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseSaleTransactions_OwnerId",
                table: "HouseSaleTransactions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseSpecifications_HouseId",
                table: "HouseSpecifications",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingCompanies_AddressId",
                table: "HousingCompanies",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingCompanyFinancialDetails_CompanyId",
                table: "HousingCompanyFinancialDetails",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuesOrCompliants_ApartmentHouseId",
                table: "IssuesOrCompliants",
                column: "ApartmentHouseId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuesOrCompliants_HouseId",
                table: "IssuesOrCompliants",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuesOrCompliants_HouseOwnerId",
                table: "IssuesOrCompliants",
                column: "HouseOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AddressId",
                table: "Projects",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_HousingCompanyId",
                table: "Projects",
                column: "HousingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AddressId",
                table: "UserProfiles",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_CompanyId",
                table: "UserProfiles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_HomeOwnerRankingRankingId",
                table: "UserProfiles",
                column: "HomeOwnerRankingRankingId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_PartyAddressAddressID",
                table: "UserProfiles",
                column: "PartyAddressAddressID");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_RankingId",
                table: "UserProfiles",
                column: "RankingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApartmentHouseReviews");

            migrationBuilder.DropTable(
                name: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropTable(
                name: "ApartmentPhotos");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CommisionRates");

            migrationBuilder.DropTable(
                name: "CustomerTestimonials");

            migrationBuilder.DropTable(
                name: "HousePerFloors");

            migrationBuilder.DropTable(
                name: "HouseRentInfos");

            migrationBuilder.DropTable(
                name: "HouseRentPayments");

            migrationBuilder.DropTable(
                name: "HouseReviews");

            migrationBuilder.DropTable(
                name: "HouseSalePayments");

            migrationBuilder.DropTable(
                name: "HouseSpecifications");

            migrationBuilder.DropTable(
                name: "HousingCompanyFinancialDetails");

            migrationBuilder.DropTable(
                name: "IssuesOrCompliants");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Translations");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "HouseRentTransactions");

            migrationBuilder.DropTable(
                name: "HouseSaleTransactions");

            migrationBuilder.DropTable(
                name: "ApartmentHouses");

            migrationBuilder.DropTable(
                name: "Houses");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "HouseCategories");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "HomeOwnerRankings");

            migrationBuilder.DropTable(
                name: "HousingCompanies");

            migrationBuilder.DropTable(
                name: "Addresses");
        }
    }
}
