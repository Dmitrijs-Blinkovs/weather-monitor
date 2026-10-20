using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherMonitor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFetchAttemptResultIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FetchAttempts_IsSuccess_AttemptedAtUtc",
                table: "FetchAttempts",
                columns: new[] { "IsSuccess", "AttemptedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FetchAttempts_IsSuccess_AttemptedAtUtc",
                table: "FetchAttempts");
        }
    }
}
