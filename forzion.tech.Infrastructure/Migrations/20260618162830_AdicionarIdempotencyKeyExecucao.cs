using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIdempotencyKeyExecucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "execucoes_treino",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // lint-migrations:allow — UNIQUE parcial; coluna nova nullable, linhas pré-existentes NULL fora do filtro.
            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_aluno_id_idempotency_key_unique",
                table: "execucoes_treino",
                columns: new[] { "aluno_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_execucoes_treino_aluno_id_idempotency_key_unique",
                table: "execucoes_treino");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "execucoes_treino");
        }
    }
}
