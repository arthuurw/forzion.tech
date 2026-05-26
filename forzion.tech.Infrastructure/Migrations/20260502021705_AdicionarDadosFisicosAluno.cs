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
                table: "alunos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doencas",
                table: "alunos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "finalidade",
                table: "alunos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "foco_treino",
                table: "alunos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "limitacoes_fisicas",
                table: "alunos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nivel_condicionamento",
                table: "alunos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes_adicionais",
                table: "alunos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tempo_disponivel_minutos",
                table: "alunos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dias_disponiveis",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "doencas",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "finalidade",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "foco_treino",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "limitacoes_fisicas",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "nivel_condicionamento",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "observacoes_adicionais",
                table: "alunos");

            migrationBuilder.DropColumn(
                name: "tempo_disponivel_minutos",
                table: "alunos");
        }
    }
}
