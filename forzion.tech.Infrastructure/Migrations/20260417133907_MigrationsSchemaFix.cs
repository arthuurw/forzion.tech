using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrationsSchemaFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_execucoes_exercicio_treino_exercicio_treino_exercicio_id",
                schema: "homolog",
                table: "execucoes_exercicio");

            migrationBuilder.AddForeignKey(
                name: "fk_execucoes_exercicio_treino_exercicios_treino_exercicio_id",
                schema: "homolog",
                table: "execucoes_exercicio",
                column: "treino_exercicio_id",
                principalSchema: "homolog",
                principalTable: "treino_exercicios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_execucoes_exercicio_treino_exercicios_treino_exercicio_id",
                schema: "homolog",
                table: "execucoes_exercicio");

            migrationBuilder.AddForeignKey(
                name: "fk_execucoes_exercicio_treino_exercicio_treino_exercicio_id",
                schema: "homolog",
                table: "execucoes_exercicio",
                column: "treino_exercicio_id",
                principalSchema: "homolog",
                principalTable: "treino_exercicios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
