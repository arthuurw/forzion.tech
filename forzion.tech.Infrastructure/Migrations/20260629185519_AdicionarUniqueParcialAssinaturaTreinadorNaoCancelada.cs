using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarUniqueParcialAssinaturaTreinadorNaoCancelada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assinaturas_treinador_treinador_id",
                table: "assinaturas_treinador");

            migrationBuilder.CreateIndex(
                name: "ux_assinaturas_treinador_nao_cancelada_por_treinador",
                table: "assinaturas_treinador",
                column: "treinador_id",
                unique: true,
                filter: "status <> 'Cancelada'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_assinaturas_treinador_nao_cancelada_por_treinador",
                table: "assinaturas_treinador");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_treinador_treinador_id",
                table: "assinaturas_treinador",
                column: "treinador_id");
        }
    }
}
