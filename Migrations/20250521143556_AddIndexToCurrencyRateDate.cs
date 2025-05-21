using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CurrencyExchangeRateAggregator.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexToCurrencyRateDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CurrencyRates_Date",
                table: "CurrencyRates",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurrencyRates_Date",
                table: "CurrencyRates");
        }
    }
}
