using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BorsaGPT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateAnalysisTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_analysis",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    candidate_wallet_id = table.Column<long>(type: "bigint", nullable: false),
                    wallet_address = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                    t0_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    t1_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    t0_block = table.Column<long>(type: "bigint", nullable: false),
                    t1_block = table.Column<long>(type: "bigint", nullable: false),
                    value_t0_usd = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                    value_t1_usd = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                    simple_return = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    net_cash_flow_usd = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                    adjusted_return = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    funding_heavy = table.Column<bool>(type: "boolean", nullable: false),
                    stable_heavy = table.Column<bool>(type: "boolean", nullable: false),
                    price_missing = table.Column<bool>(type: "boolean", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    analyzed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_analysis", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_analysis_candidate_wallets_candidate_wallet_id",
                        column: x => x.candidate_wallet_id,
                        principalTable: "candidate_wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_analysis_adjusted_return",
                table: "candidate_analysis",
                column: "adjusted_return");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_analysis_simple_return",
                table: "candidate_analysis",
                column: "simple_return");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_analysis_unique_timeframe",
                table: "candidate_analysis",
                columns: new[] { "candidate_wallet_id", "t0_timestamp", "t1_timestamp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_analysis");
        }
    }
}
