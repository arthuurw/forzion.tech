using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CriarNotasFiscaisEDadosFiscaisTreinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_documento",
                table: "treinadores",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_endereco_bairro",
                table: "treinadores",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_endereco_cep",
                table: "treinadores",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_endereco_codigo_municipio_ibge",
                table: "treinadores",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_endereco_complemento",
                table: "treinadores",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_endereco_logradouro",
                table: "treinadores",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_endereco_numero",
                table: "treinadores",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_endereco_uf",
                table: "treinadores",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_inscricao_municipal",
                table: "treinadores",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_razao_social",
                table: "treinadores",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dados_fiscais_tipo_documento",
                table: "treinadores",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            // lint-migrations:allow
            migrationBuilder.CreateTable(
                name: "notas_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pagamento_treinador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    competencia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    competencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    chave_acesso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    numero_nfse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    numero_dps = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_emissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    danfse_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    codigo_erro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    motivo_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notas_fiscais");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_documento",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_endereco_bairro",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_endereco_cep",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_endereco_codigo_municipio_ibge",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_endereco_complemento",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_endereco_logradouro",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_endereco_numero",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_endereco_uf",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_inscricao_municipal",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_razao_social",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "dados_fiscais_tipo_documento",
                table: "treinadores");
        }
    }
}
