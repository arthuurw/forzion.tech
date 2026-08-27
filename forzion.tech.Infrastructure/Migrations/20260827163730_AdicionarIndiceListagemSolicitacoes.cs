using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIndiceListagemSolicitacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_agendamento_treinador_id_inicio_utc_id",
                table: "solicitacoes_agendamento",
                columns: new[] { "treinador_id", "inicio_utc", "id" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_solicitacoes_agendamento_treinador_id_inicio_utc_id",
                table: "solicitacoes_agendamento");
        }
    }
}
