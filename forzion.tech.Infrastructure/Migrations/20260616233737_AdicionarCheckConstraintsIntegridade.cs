using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCheckConstraintsIntegridade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_treino_exercicio_series_quantidade_positivo",
                table: "treino_exercicio_series",
                sql: "\"quantidade\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_treino_exercicio_series_repeticoes_max_gte_min",
                table: "treino_exercicio_series",
                sql: "\"repeticoes_max\" IS NULL OR \"repeticoes_max\" >= \"repeticoes_min\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_treino_exercicio_series_repeticoes_min_positivo",
                table: "treino_exercicio_series",
                sql: "\"repeticoes_min\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_planos_plataforma_max_alunos_positivo",
                table: "planos_plataforma",
                sql: "\"max_alunos\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_planos_plataforma_preco_nao_negativo",
                table: "planos_plataforma",
                sql: "\"preco\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pagamentos_treinador_valor_nao_negativo",
                table: "pagamentos_treinador",
                sql: "\"valor\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pagamentos_valor_nao_negativo",
                table: "pagamentos",
                sql: "\"valor\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pacotes_preco_nao_negativo",
                table: "pacotes",
                sql: "\"preco\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_execucoes_exercicio_repeticoes_positivo",
                table: "execucoes_exercicio",
                sql: "\"repeticoes_executadas\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_execucoes_exercicio_series_positivo",
                table: "execucoes_exercicio",
                sql: "\"series_executadas\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assinaturas_treinador_valor_nao_negativo",
                table: "assinaturas_treinador",
                sql: "\"valor\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assinaturas_aluno_valor_nao_negativo",
                table: "assinaturas_aluno",
                sql: "\"valor\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_treino_exercicio_series_quantidade_positivo",
                table: "treino_exercicio_series");

            migrationBuilder.DropCheckConstraint(
                name: "ck_treino_exercicio_series_repeticoes_max_gte_min",
                table: "treino_exercicio_series");

            migrationBuilder.DropCheckConstraint(
                name: "ck_treino_exercicio_series_repeticoes_min_positivo",
                table: "treino_exercicio_series");

            migrationBuilder.DropCheckConstraint(
                name: "ck_planos_plataforma_max_alunos_positivo",
                table: "planos_plataforma");

            migrationBuilder.DropCheckConstraint(
                name: "ck_planos_plataforma_preco_nao_negativo",
                table: "planos_plataforma");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pagamentos_treinador_valor_nao_negativo",
                table: "pagamentos_treinador");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pagamentos_valor_nao_negativo",
                table: "pagamentos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pacotes_preco_nao_negativo",
                table: "pacotes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_execucoes_exercicio_repeticoes_positivo",
                table: "execucoes_exercicio");

            migrationBuilder.DropCheckConstraint(
                name: "ck_execucoes_exercicio_series_positivo",
                table: "execucoes_exercicio");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assinaturas_treinador_valor_nao_negativo",
                table: "assinaturas_treinador");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assinaturas_aluno_valor_nao_negativo",
                table: "assinaturas_aluno");
        }
    }
}
