using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCortesiaEGracaLimiteAlunos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "preservar_no_limite",
                table: "vinculos_treinador_aluno",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "alunos_acima_do_cap_desde",
                table: "treinadores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "plano_cortesia_id",
                table: "treinadores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_plano_cortesia_id",
                table: "treinadores",
                column: "plano_cortesia_id");

            migrationBuilder.AddForeignKey(
                name: "fk_treinadores_planos_plataforma_plano_cortesia_id",
                table: "treinadores",
                column: "plano_cortesia_id",
                principalTable: "planos_plataforma",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_treinadores_planos_plataforma_plano_cortesia_id",
                table: "treinadores");

            migrationBuilder.DropIndex(
                name: "ix_treinadores_plano_cortesia_id",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "preservar_no_limite",
                table: "vinculos_treinador_aluno");

            migrationBuilder.DropColumn(
                name: "alunos_acima_do_cap_desde",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "plano_cortesia_id",
                table: "treinadores");
        }
    }
}
