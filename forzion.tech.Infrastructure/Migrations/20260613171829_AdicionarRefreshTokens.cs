using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // lint-migrations:allow — o índice UNIQUE em refresh_tokens.token_hash recai sobre
            // tabela CRIADA nesta mesma migration (sem linhas pré-existentes), logo não há risco
            // de falha por duplicata; o lint não distingue tabela nova de tabela populada.
            migrationBuilder.CreateTable(
                name: "refresh_token_families",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    absoluto_expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_revogacao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    rotulo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token_families", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    substituido_por_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_refresh_token_families_familia_id",
                        column: x => x.familia_id,
                        principalTable: "refresh_token_families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_families_conta_id",
                table: "refresh_token_families",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_families_revogada_em",
                table: "refresh_token_families",
                column: "revogada_em");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_familia_id",
                table: "refresh_tokens",
                column: "familia_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "refresh_token_families");
        }
    }
}
