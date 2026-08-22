using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contato_tipo = table.Column<string>(type: "text", nullable: false),
                    contato_valor = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    interesse = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    consentimento_finalidade = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    consentimento_concedido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consentimento_registrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    origem_user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    origem_assistente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    motivo_descarte = table.Column<string>(type: "text", nullable: true),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    argumentos_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ultimo_toque_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    anonimizado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_leads_alunos_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leads_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lead_convites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invalidado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_convites", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_convites_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_interacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status_anterior = table.Column<string>(type: "text", nullable: true),
                    status_novo = table.Column<string>(type: "text", nullable: true),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    realizado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_interacoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_interacoes_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lead_convites_expira_em",
                table: "lead_convites",
                column: "expira_em");

            migrationBuilder.CreateIndex(
                name: "ix_lead_convites_lead_id",
                table: "lead_convites",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_convites_token_hash",
                table: "lead_convites",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lead_interacoes_lead_id",
                table: "lead_interacoes",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_aluno_id",
                table: "leads",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_contato_valor",
                table: "leads",
                column: "contato_valor");

            migrationBuilder.CreateIndex(
                name: "ix_leads_treinador_id_created_at",
                table: "leads",
                columns: new[] { "treinador_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_treinador_id_idempotency_key_unique",
                table: "leads",
                columns: new[] { "treinador_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_leads_treinador_id_status_created_at",
                table: "leads",
                columns: new[] { "treinador_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_leads_ultimo_toque_em",
                table: "leads",
                column: "ultimo_toque_em",
                filter: "anonimizado = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_convites");

            migrationBuilder.DropTable(
                name: "lead_interacoes");

            migrationBuilder.DropTable(
                name: "leads");
        }
    }
}
