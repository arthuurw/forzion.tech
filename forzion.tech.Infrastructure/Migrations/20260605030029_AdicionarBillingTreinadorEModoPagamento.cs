using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarBillingTreinadorEModoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "modo_pagamento_aluno",
                table: "treinadores",
                type: "text",
                nullable: false,
                defaultValue: "Plataforma");

            migrationBuilder.CreateTable(
                name: "assinaturas_treinador",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plano_plataforma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    data_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_proxima_cobranca = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_cancelamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tentativas_falhas_consecutivas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    plano_plataforma_id_agendado = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assinaturas_treinador", x => x.id);
                    table.ForeignKey(
                        name: "fk_assinaturas_treinador_planos_plataforma_plano_plataforma_id",
                        column: x => x.plano_plataforma_id,
                        principalTable: "planos_plataforma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assinaturas_treinador_planos_plataforma_plano_plataforma_id1",
                        column: x => x.plano_plataforma_id_agendado,
                        principalTable: "planos_plataforma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assinaturas_treinador_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos_treinador",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assinatura_treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    metodo_pagamento = table.Column<string>(type: "text", nullable: false, defaultValue: "Pix"),
                    finalidade = table.Column<string>(type: "text", nullable: false),
                    plano_alvo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stripe_payment_intent_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pix_qr_code = table.Column<string>(type: "text", nullable: true),
                    pix_qr_code_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pix_expiracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    client_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data_pagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagamentos_treinador", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagamentos_treinador_assinaturas_treinador_assinatura_trein",
                        column: x => x.assinatura_treinador_id,
                        principalTable: "assinaturas_treinador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pagamentos_treinador_planos_plataforma_plano_alvo_id",
                        column: x => x.plano_alvo_id,
                        principalTable: "planos_plataforma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pagamentos_treinador_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_treinador_plano_plataforma_id",
                table: "assinaturas_treinador",
                column: "plano_plataforma_id");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_treinador_plano_plataforma_id_agendado",
                table: "assinaturas_treinador",
                column: "plano_plataforma_id_agendado");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_treinador_status_data_proxima_cobranca",
                table: "assinaturas_treinador",
                columns: new[] { "status", "data_proxima_cobranca" });

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_treinador_treinador_id",
                table: "assinaturas_treinador",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_treinador_assinatura_id_pendente_unique",
                table: "pagamentos_treinador",
                column: "assinatura_treinador_id",
                unique: true,
                filter: "status = 'Pendente'");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_treinador_assinatura_id_status",
                table: "pagamentos_treinador",
                columns: new[] { "assinatura_treinador_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_treinador_plano_alvo_id",
                table: "pagamentos_treinador",
                column: "plano_alvo_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_treinador_stripe_payment_intent_id",
                table: "pagamentos_treinador",
                column: "stripe_payment_intent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_treinador_treinador_id",
                table: "pagamentos_treinador",
                column: "treinador_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamentos_treinador");

            migrationBuilder.DropTable(
                name: "assinaturas_treinador");

            migrationBuilder.DropColumn(
                name: "modo_pagamento_aluno",
                table: "treinadores");
        }
    }
}
