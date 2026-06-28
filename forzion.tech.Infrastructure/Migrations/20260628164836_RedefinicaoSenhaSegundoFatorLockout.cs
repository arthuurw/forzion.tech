using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedefinicaoSenhaSegundoFatorLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "redefinicao_senha_segundo_fator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    janela_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_redefinicao_senha_segundo_fator", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_redefinicao_senha_segundo_fator_conta_id",
                table: "redefinicao_senha_segundo_fator",
                column: "conta_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "redefinicao_senha_segundo_fator");
        }
    }
}
