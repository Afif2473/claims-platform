using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLiabilityAndInformationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: true); // Backfill only: existing users stay active. No model-level default (see task-05 doc).

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Claims",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "Claims",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "Claims",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReserveAmount",
                table: "Claims",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Claims",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "InformationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Response = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InformationRequests_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InformationRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InformationRequests_ClaimId",
                table: "InformationRequests",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_InformationRequests_RequestedByUserId",
                table: "InformationRequests",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InformationRequests");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReserveAmount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Claims");
        }
    }
}
