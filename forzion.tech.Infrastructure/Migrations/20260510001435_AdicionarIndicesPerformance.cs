using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIndicesPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_vinculos_treinador_aluno_treinador_id_status",
                table: "vinculos_treinador_aluno",
                columns: new[] { "treinador_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_status",
                table: "treinadores",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vinculos_treinador_aluno_treinador_id_status",
                table: "vinculos_treinador_aluno");

            migrationBuilder.DropIndex(
                name: "ix_treinadores_status",
                table: "treinadores");
        }
    }
}
