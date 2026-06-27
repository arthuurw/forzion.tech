using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIndiceExecucaoAlunoData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_execucoes_treino_aluno_id",
                table: "execucoes_treino");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_aluno_id_data_execucao",
                table: "execucoes_treino",
                columns: new[] { "aluno_id", "data_execucao" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_execucoes_treino_aluno_id_data_execucao",
                table: "execucoes_treino");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_aluno_id",
                table: "execucoes_treino",
                column: "aluno_id");
        }
    }
}
