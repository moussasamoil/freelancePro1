using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddOrderPinFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinnedByUserId",
                table: "Orders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IsPinned_PinnedAt",
                table: "Orders",
                columns: new[] { "IsPinned", "PinnedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_IsPinned_PinnedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PinnedByUserId",
                table: "Orders");
        }
    }
}