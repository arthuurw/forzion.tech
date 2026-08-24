using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSolicitacoesAgendamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitacoes_agendamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pacote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_id = table.Column<string>(type: "text", nullable: false),
                    inicio_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fim_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    argumentos_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decidida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decidida_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitacoes_agendamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_solicitacoes_agendamento_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solicitacoes_agendamento_pacotes_pacote_id",
                        column: x => x.pacote_id,
                        principalTable: "pacotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solicitacoes_agendamento_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_agendamento_lead_id",
                table: "solicitacoes_agendamento",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_agendamento_pacote_id",
                table: "solicitacoes_agendamento",
                column: "pacote_id");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_agendamento_treinador_id_idempotency_key_unique",
                table: "solicitacoes_agendamento",
                columns: new[] { "treinador_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_agendamento_treinador_id_pacote_id_status_inicio_utc",
                table: "solicitacoes_agendamento",
                columns: new[] { "treinador_id", "pacote_id", "status", "inicio_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_agendamento_treinador_id_status_inicio_utc",
                table: "solicitacoes_agendamento",
                columns: new[] { "treinador_id", "status", "inicio_utc" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitacoes_agendamento");
        }
    }
}
