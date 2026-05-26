using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueConstraintTreinoAlunoPorFicha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deactivate duplicate active bindings — keep only the most recent per treino
            migrationBuilder.Sql(@"
                UPDATE treino_alunos
                SET status = 'Inativo',
                    updated_at = NOW()
                WHERE id IN (
                    SELECT id
                    FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (PARTITION BY treino_id ORDER BY created_at DESC) AS rn
                        FROM treino_alunos
                        WHERE status = 'Ativo'
                    ) sub
                    WHERE rn > 1
                );
            ");

            migrationBuilder.DropIndex(
                name: "ix_treino_alunos_treino_id",
                table: "treino_alunos");

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_treino_id",
                table: "treino_alunos",
                column: "treino_id",
                unique: true,
                filter: "status = 'Ativo'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_treino_alunos_treino_id",
                table: "treino_alunos");

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_treino_id",
                table: "treino_alunos",
                column: "treino_id");
        }
    }
}
