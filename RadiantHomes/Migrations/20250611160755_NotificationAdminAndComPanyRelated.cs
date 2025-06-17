using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadiantHomes.Migrations
{
    /// <inheritdoc />
    public partial class NotificationAdminAndComPanyRelated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataKey",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Datatype",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiverAdminId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiverCompanyId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReceiverAdminId",
                table: "Notifications",
                column: "ReceiverAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReceiverCompanyId",
                table: "Notifications",
                column: "ReceiverCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_HousingCompanies_ReceiverCompanyId",
                table: "Notifications",
                column: "ReceiverCompanyId",
                principalTable: "HousingCompanies",
                principalColumn: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_UserProfiles_ReceiverAdminId",
                table: "Notifications",
                column: "ReceiverAdminId",
                principalTable: "UserProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_HousingCompanies_ReceiverCompanyId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_UserProfiles_ReceiverAdminId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ReceiverAdminId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ReceiverCompanyId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DataKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Datatype",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ReceiverAdminId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ReceiverCompanyId",
                table: "Notifications");
        }
    }
}
