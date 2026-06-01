using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarHealthReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "error_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origem = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_report_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    hora_envio_utc = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    destinatarios = table.Column<string>(type: "text", nullable: false),
                    incluir_liveness = table.Column<bool>(type: "boolean", nullable: false),
                    incluir_kpis = table.Column<bool>(type: "boolean", nullable: false),
                    incluir_entregabilidade = table.Column<bool>(type: "boolean", nullable: false),
                    incluir_erros = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_envio_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_report_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capturado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ambiente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status_geral = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_error_logs_ocorrido_em",
                table: "error_logs",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "ix_health_snapshots_capturado_em",
                table: "health_snapshots",
                column: "capturado_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "error_logs");

            migrationBuilder.DropTable(
                name: "health_report_config");

            migrationBuilder.DropTable(
                name: "health_snapshots");
        }
    }
}
