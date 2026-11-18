using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadiantHomes.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationAndSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AgreedPrice",
                table: "ApartmentHouseSaleTransactions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementFileName",
                table: "ApartmentHouseSaleTransactions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Discount",
                table: "ApartmentHouseSaleTransactions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeadId",
                table: "ApartmentHouseSaleTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "ApartmentHouseSaleTransactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "ApartmentHouseSaleTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedOn",
                table: "ApartmentHouseSaleTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoldByUserId",
                table: "ApartmentHouseSaleTransactions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoldOn",
                table: "ApartmentHouseSaleTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CallLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Room = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnsweredOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AgentUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AgentName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AgentUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_ApartmentHouses_ApartmentHouseId",
                        column: x => x.ApartmentHouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Bedrooms = table.Column<int>(type: "int", nullable: true),
                    Budget = table.Column<double>(type: "float", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Campaign = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Temperature = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LostReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AgentUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    NextFollowUpOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastContactOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SaleTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_ApartmentHouses_ApartmentHouseId",
                        column: x => x.ApartmentHouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AssignedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EndedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAgentAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApartmentHouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PreferredDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreferredTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScheduledStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AgentUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteVisits_ApartmentHouses_ApartmentHouseId",
                        column: x => x.ApartmentHouseId,
                        principalTable: "ApartmentHouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteVisits_HousingCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "HousingCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteVisits_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteVisits_UserProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FromCustomer = table.Column<bool>(type: "bit", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SentOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadActivities_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteVisitStatusChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteVisitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteVisitStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteVisitStatusChanges_SiteVisits_SiteVisitId",
                        column: x => x.SiteVisitId,
                        principalTable: "SiteVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_CompanyId_RequestedOn",
                table: "CallLogs",
                columns: new[] { "CompanyId", "RequestedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_Room",
                table: "CallLogs",
                column: "Room",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_Status_RequestedOn",
                table: "CallLogs",
                columns: new[] { "Status", "RequestedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_ClientMessageId",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_Id",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AgentUserId",
                table: "Conversations",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ApartmentHouseId",
                table: "Conversations",
                column: "ApartmentHouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_Status_LastMessageOn",
                table: "Conversations",
                columns: new[] { "CompanyId", "Status", "LastMessageOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CustomerId",
                table: "Conversations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ProjectId",
                table: "Conversations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_LeadId_OccurredOn",
                table: "LeadActivities",
                columns: new[] { "LeadId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AgentUserId",
                table: "Leads",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ApartmentHouseId",
                table: "Leads",
                column: "ApartmentHouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CompanyId_Stage",
                table: "Leads",
                columns: new[] { "CompanyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CustomerId",
                table: "Leads",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_NextFollowUpOn",
                table: "Leads",
                column: "NextFollowUpOn");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ProjectId",
                table: "Leads",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Reference",
                table: "Leads",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentAssignments_ProjectId_UserId",
                table: "ProjectAgentAssignments",
                columns: new[] { "ProjectId", "UserId" },
                unique: true,
                filter: "[EndedOn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentAssignments_UserId_EndedOn",
                table: "ProjectAgentAssignments",
                columns: new[] { "UserId", "EndedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_AgentUserId_ScheduledStart",
                table: "SiteVisits",
                columns: new[] { "AgentUserId", "ScheduledStart" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_ApartmentHouseId",
                table: "SiteVisits",
                column: "ApartmentHouseId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_CompanyId_Status_PreferredDate",
                table: "SiteVisits",
                columns: new[] { "CompanyId", "Status", "PreferredDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_CustomerId",
                table: "SiteVisits",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_ProjectId",
                table: "SiteVisits",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_Reference",
                table: "SiteVisits",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisitStatusChanges_SiteVisitId",
                table: "SiteVisitStatusChanges",
                column: "SiteVisitId");

            // Existing completed sales: the price at the time and the sale date (payment status unknown: left empty).
            // Run through EXEC: an idempotent script is one batch, compiled before the columns above exist.
            migrationBuilder.Sql(@"
EXEC(N'UPDATE t SET AgreedPrice = a.SalePrice, SoldOn = COALESCE(t.UpdatedOn, t.TransactionDate)
FROM ApartmentHouseSaleTransactions t JOIN ApartmentHouses a ON a.Id = t.ApartmentHouseId
WHERE t.Status = 2 AND t.AgreedPrice IS NULL;');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallLogs");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "LeadActivities");

            migrationBuilder.DropTable(
                name: "ProjectAgentAssignments");

            migrationBuilder.DropTable(
                name: "SiteVisitStatusChanges");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "SiteVisits");

            migrationBuilder.DropColumn(
                name: "AgreedPrice",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "AgreementFileName",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "ReversedOn",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "SoldByUserId",
                table: "ApartmentHouseSaleTransactions");

            migrationBuilder.DropColumn(
                name: "SoldOn",
                table: "ApartmentHouseSaleTransactions");
        }
    }
}
