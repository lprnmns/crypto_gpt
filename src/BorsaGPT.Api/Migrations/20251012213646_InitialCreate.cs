using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BorsaGPT.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_wallets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    wallet_address = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    first_transfer_amount_eth = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                    first_transfer_token = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: true),
                    block_number = table.Column<long>(type: "bigint", nullable: false),
                    analyzed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_wallets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_wallets_analyzed",
                table: "candidate_wallets",
                column: "analyzed");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_wallets_block_number",
                table: "candidate_wallets",
                column: "block_number");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_wallets_detected_at",
                table: "candidate_wallets",
                column: "detected_at");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_wallets_wallet_address",
                table: "candidate_wallets",
                column: "wallet_address",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_wallets");
        }
    }
}
