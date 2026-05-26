using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarVerificacaoEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "email_verificado",
                table: "contas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "verificado_em",
                table: "contas",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: contas já existentes são consideradas verificadas para não
            // bloquear o login de usuários ativos. Apenas novos cadastros (inseridos
            // após esta migration) começam com email_verificado = false.
            migrationBuilder.Sql(
                "UPDATE contas SET email_verificado = true, verificado_em = now() WHERE email_verificado = false;");

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_verification_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_conta_id",
                table: "email_verification_tokens",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_token_hash",
                table: "email_verification_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_verification_tokens");

            migrationBuilder.DropColumn(
                name: "email_verificado",
                table: "contas");

            migrationBuilder.DropColumn(
                name: "verificado_em",
                table: "contas");
        }
    }
}
