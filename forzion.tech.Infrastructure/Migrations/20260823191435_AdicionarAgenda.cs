using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "agenda_antecedencia_minima_horas",
                table: "treinadores",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "agenda_horizonte_dias",
                table: "treinadores",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "fuso_horario",
                table: "treinadores",
                type: "text",
                nullable: false,
                defaultValue: "America/Sao_Paulo");

            migrationBuilder.AddColumn<int>(
                name: "capacidade_maxima",
                table: "pacotes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "bloqueios_agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    inicio_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fim_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dia_semana = table.Column<int>(type: "integer", nullable: true),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hora_fim = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bloqueios_agenda", x => x.id);
                    table.ForeignKey(
                        name: "fk_bloqueios_agenda_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bloqueios_agenda_treinador_id_inicio_utc_fim_utc",
                table: "bloqueios_agenda",
                columns: new[] { "treinador_id", "inicio_utc", "fim_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_bloqueios_agenda_treinador_id_tipo",
                table: "bloqueios_agenda",
                columns: new[] { "treinador_id", "tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bloqueios_agenda");

            migrationBuilder.DropColumn(
                name: "agenda_antecedencia_minima_horas",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "agenda_horizonte_dias",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "fuso_horario",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "capacidade_maxima",
                table: "pacotes");
        }
    }
}
