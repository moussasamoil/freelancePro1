using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class transfertostring : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the primary key constraint
            migrationBuilder.DropPrimaryKey(
                name: "PK_SocialMediaMessages",
                table: "SocialMediaMessages"
            );

            // Drop the current ID column
            migrationBuilder.DropColumn(
                name: "Id",
                table: "SocialMediaMessages"
            );

            // Recreate the ID column as a non-IDENTITY string
            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "SocialMediaMessages",
                type: "nvarchar(450)",
                nullable: false
            );

            // Re-add the primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_SocialMediaMessages",
                table: "SocialMediaMessages",
                column: "Id"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the primary key constraint
            migrationBuilder.DropPrimaryKey(
                name: "PK_SocialMediaMessages",
                table: "SocialMediaMessages"
            );

            // Drop the string ID column
            migrationBuilder.DropColumn(
                name: "Id",
                table: "SocialMediaMessages"
            );

            // Recreate the original ID column as an IDENTITY bigint
            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "SocialMediaMessages",
                type: "bigint",
                nullable: false
            )
            .Annotation("SqlServer:Identity", "1, 1");

            // Re-add the primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_SocialMediaMessages",
                table: "SocialMediaMessages",
                column: "Id"
            );
        }
    }
}
