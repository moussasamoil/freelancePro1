using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class RemoveOrderPostAcknowledgement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderPosts_Type_IsAcknowledged_CreatedAt",
                table: "OrderPosts");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                table: "OrderPosts");

            migrationBuilder.DropColumn(
                name: "AcknowledgedByUserId",
                table: "OrderPosts");

            migrationBuilder.DropColumn(
                name: "IsAcknowledged",
                table: "OrderPosts");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPosts_Type_CreatedAt",
                table: "OrderPosts",
                columns: new[] { "Type", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderPosts_Type_CreatedAt",
                table: "OrderPosts");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAt",
                table: "OrderPosts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedByUserId",
                table: "OrderPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAcknowledged",
                table: "OrderPosts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPosts_Type_IsAcknowledged_CreatedAt",
                table: "OrderPosts",
                columns: new[] { "Type", "IsAcknowledged", "CreatedAt" });
        }
    }
}
