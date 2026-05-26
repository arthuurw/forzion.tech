using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPagamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "stripe_connect_account_id",
                table: "treinadores",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "stripe_onboarding_completo",
                table: "treinadores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "assinaturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vinculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pacote_aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    data_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_proxima_cobranca = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_cancelamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assinaturas", x => x.id);
                    table.ForeignKey(
                        name: "fk_assinaturas_alunos_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assinaturas_pacotes_aluno_pacote_aluno_id",
                        column: x => x.pacote_aluno_id,
                        principalTable: "pacotes_aluno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assinaturas_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assinaturas_vinculos_treinador_aluno_vinculo_id",
                        column: x => x.vinculo_id,
                        principalTable: "vinculos_treinador_aluno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assinatura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    stripe_payment_intent_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pix_qr_code = table.Column<string>(type: "text", nullable: true),
                    pix_qr_code_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pix_expiracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_pagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagamentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagamentos_assinaturas_assinatura_id",
                        column: x => x.assinatura_id,
                        principalTable: "assinaturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_stripe_connect_account_id",
                table: "treinadores",
                column: "stripe_connect_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_aluno_id",
                table: "assinaturas",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_pacote_aluno_id",
                table: "assinaturas",
                column: "pacote_aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_status_data_proxima_cobranca",
                table: "assinaturas",
                columns: new[] { "status", "data_proxima_cobranca" });

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_treinador_id",
                table: "assinaturas",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_vinculo_id",
                table: "assinaturas",
                column: "vinculo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_assinatura_id",
                table: "pagamentos",
                column: "assinatura_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_assinatura_id_status",
                table: "pagamentos",
                columns: new[] { "assinatura_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_stripe_payment_intent_id",
                table: "pagamentos",
                column: "stripe_payment_intent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamentos");

            migrationBuilder.DropTable(
                name: "assinaturas");

            migrationBuilder.DropIndex(
                name: "ix_treinadores_stripe_connect_account_id",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "stripe_connect_account_id",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "stripe_onboarding_completo",
                table: "treinadores");
        }
    }
}
