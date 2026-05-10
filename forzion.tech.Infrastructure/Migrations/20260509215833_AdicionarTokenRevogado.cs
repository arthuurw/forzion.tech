using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTokenRevogado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tokens_revogados",
                schema: "homolog",
                columns: table => new
                {
                    jti = table.Column<Guid>(type: "uuid", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens_revogados", x => x.jti);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tokens_revogados_expira_em",
                schema: "homolog",
                table: "tokens_revogados",
                column: "expira_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tokens_revogados",
                schema: "homolog");
        }
    }
}
