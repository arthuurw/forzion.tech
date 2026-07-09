using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoverNotasFiscais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notas_fiscais");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // lint-migrations:allow — UNIQUE só no Down (recria tabela vazia).
            migrationBuilder.CreateTable(
                name: "notas_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelamento_pendente_pre_emissao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    chave_acesso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_erro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    competencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    competencia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    danfse_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data_emissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento_pendente = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motivo_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    numero_dps = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    numero_nfse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pagamento_treinador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notas_fiscais", x => x.id);
                    table.CheckConstraint("ck_notas_fiscais_valor_nao_negativo", "\"valor\" >= 0");
                    table.ForeignKey(
                        name: "fk_notas_fiscais_pagamentos_treinador_pagamento_treinador_id",
                        column: x => x.pagamento_treinador_id,
                        principalTable: "pagamentos_treinador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notas_fiscais_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_pagamento_treinador_id_unique",
                table: "notas_fiscais",
                column: "pagamento_treinador_id",
                unique: true,
                filter: "pagamento_treinador_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_treinador_id",
                table: "notas_fiscais",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_treinador_tipo_competencia_unique",
                table: "notas_fiscais",
                columns: new[] { "treinador_id", "tipo", "competencia_inicio" },
                unique: true,
                filter: "competencia_inicio IS NOT NULL");
        }
    }
}
