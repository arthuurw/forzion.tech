using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeriesConfiguraveisExercicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carga",
                schema: "homolog",
                table: "treino_exercicios");

            migrationBuilder.DropColumn(
                name: "descanso",
                schema: "homolog",
                table: "treino_exercicios");

            migrationBuilder.DropColumn(
                name: "repeticoes",
                schema: "homolog",
                table: "treino_exercicios");

            migrationBuilder.DropColumn(
                name: "series",
                schema: "homolog",
                table: "treino_exercicios");

            migrationBuilder.CreateTable(
                name: "treino_exercicio_series",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treino_exercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    repeticoes_min = table.Column<int>(type: "integer", nullable: false),
                    repeticoes_max = table.Column<int>(type: "integer", nullable: true),
                    descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    carga = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    descanso = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treino_exercicio_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_treino_exercicio_series_treino_exercicios_treino_exercicio_",
                        column: x => x.treino_exercicio_id,
                        principalSchema: "homolog",
                        principalTable: "treino_exercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_treino_exercicio_series_treino_exercicio_id",
                schema: "homolog",
                table: "treino_exercicio_series",
                column: "treino_exercicio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "treino_exercicio_series",
                schema: "homolog");

            migrationBuilder.AddColumn<decimal>(
                name: "carga",
                schema: "homolog",
                table: "treino_exercicios",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "descanso",
                schema: "homolog",
                table: "treino_exercicios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "repeticoes",
                schema: "homolog",
                table: "treino_exercicios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "series",
                schema: "homolog",
                table: "treino_exercicios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
