using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDadosFisicosAluno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dias_disponiveis",
                schema: "homolog",
                table: "alunos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doencas",
                schema: "homolog",
                table: "alunos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "finalidade",
                schema: "homolog",
                table: "alunos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "foco_treino",
                schema: "homolog",
                table: "alunos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "limitacoes_fisicas",
                schema: "homolog",
                table: "alunos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nivel_condicionamento",
                schema: "homolog",
                table: "alunos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes_adicionais",
                schema: "homolog",
                table: "alunos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tempo_disponivel_minutos",
                schema: "homolog",
                table: "alunos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dias_disponiveis",
                schema: "homolog",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "doencas",
                schema: "homolog",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "finalidade",
                schema: "homolog",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "foco_treino",
                schema: "homolog",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "limitacoes_fisicas",
                schema: "homolog",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "nivel_condicionamento",
                schema: "homolog",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "observacoes_adicionais",
                schema: "homolog",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "tempo_disponivel_minutos",
                schema: "homolog",
                table: "alunos");
        }
    }
}
