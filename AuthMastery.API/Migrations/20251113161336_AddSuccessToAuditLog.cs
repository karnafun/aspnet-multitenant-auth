using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthMastery.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSuccessToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Success",
                table: "AuditLogs");
        }
    }
}
