using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoverMaxFichasAdicionarDescricaoPacote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_treino_alunos_treino_id",
                schema: "homolog",
                table: "treino_alunos");

            migrationBuilder.DropColumn(
                name: "max_fichas",
                schema: "homolog",
                table: "pacotes_aluno");

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                schema: "homolog",
                table: "pacotes_aluno",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_treino_id",
                schema: "homolog",
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
                schema: "homolog",
                table: "treino_alunos");

            migrationBuilder.DropColumn(
                name: "descricao",
                schema: "homolog",
                table: "pacotes_aluno");

            migrationBuilder.AddColumn<int>(
                name: "max_fichas",
                schema: "homolog",
                table: "pacotes_aluno",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_treino_id",
                schema: "homolog",
                table: "treino_alunos",
                column: "treino_id");
        }
    }
}
