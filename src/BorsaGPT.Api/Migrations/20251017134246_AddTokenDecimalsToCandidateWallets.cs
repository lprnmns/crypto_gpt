using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BorsaGPT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenDecimalsToCandidateWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "first_transfer_token_decimals",
                table: "candidate_wallets",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_transfer_token_decimals",
                table: "candidate_wallets");
        }
    }
}
