using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenomearPlanoPacoteAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop cross-table FKs that reference the tables being renamed.
            migrationBuilder.DropForeignKey(
                name: "fk_pagamentos_assinaturas_assinatura_id",
                table: "pagamentos");

            migrationBuilder.DropForeignKey(
                name: "fk_treinadores_planos_treinador_plano_treinador_id",
                table: "treinadores");

            migrationBuilder.DropForeignKey(
                name: "fk_vinculos_treinador_aluno_pacotes_aluno_pacote_aluno_id",
                table: "vinculos_treinador_aluno");

            // --- Rename tables (data-preserving) ---
            migrationBuilder.RenameTable(
                name: "planos_treinador",
                newName: "planos_plataforma");

            migrationBuilder.RenameTable(
                name: "pacotes_aluno",
                newName: "pacotes");

            migrationBuilder.RenameTable(
                name: "assinaturas",
                newName: "assinaturas_aluno");

            // --- Rename primary keys (Postgres keeps old constraint names after table rename) ---
            migrationBuilder.Sql("ALTER TABLE planos_plataforma RENAME CONSTRAINT pk_planos_treinador TO pk_planos_plataforma;");
            migrationBuilder.Sql("ALTER TABLE pacotes RENAME CONSTRAINT pk_pacotes_aluno TO pk_pacotes;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT pk_assinaturas TO pk_assinaturas_aluno;");

            // --- pacotes: rename internal FK and index ---
            migrationBuilder.Sql("ALTER TABLE pacotes RENAME CONSTRAINT fk_pacotes_aluno_treinadores_treinador_id TO fk_pacotes_treinadores_treinador_id;");
            migrationBuilder.RenameIndex(
                name: "ix_pacotes_aluno_treinador_id",
                table: "pacotes",
                newName: "ix_pacotes_treinador_id");

            // --- assinaturas_aluno: rename column, indexes and internal FKs ---
            migrationBuilder.RenameColumn(
                name: "pacote_aluno_id",
                table: "assinaturas_aluno",
                newName: "pacote_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_aluno_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_aluno_aluno_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_pacote_aluno_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_aluno_pacote_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_treinador_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_aluno_treinador_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_vinculo_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_aluno_vinculo_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_status_data_proxima_cobranca",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_aluno_status_data_proxima_cobranca");

            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_alunos_aluno_id TO fk_assinaturas_aluno_alunos_aluno_id;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_pacotes_aluno_pacote_aluno_id TO fk_assinaturas_aluno_pacotes_pacote_id;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_treinadores_treinador_id TO fk_assinaturas_aluno_treinadores_treinador_id;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_vinculos_treinador_aluno_vinculo_id TO fk_assinaturas_aluno_vinculos_treinador_aluno_vinculo_id;");

            // --- vinculos_treinador_aluno: rename FK column + index ---
            migrationBuilder.RenameColumn(
                name: "pacote_aluno_id",
                table: "vinculos_treinador_aluno",
                newName: "pacote_id");

            migrationBuilder.RenameIndex(
                name: "ix_vinculos_treinador_aluno_pacote_aluno_id",
                table: "vinculos_treinador_aluno",
                newName: "ix_vinculos_treinador_aluno_pacote_id");

            // --- treinadores: rename FK column + index ---
            migrationBuilder.RenameColumn(
                name: "plano_treinador_id",
                table: "treinadores",
                newName: "plano_plataforma_id");

            migrationBuilder.RenameIndex(
                name: "ix_treinadores_plano_treinador_id",
                table: "treinadores",
                newName: "ix_treinadores_plano_plataforma_id");

            // --- pagamentos: rename FK column + indexes ---
            migrationBuilder.RenameColumn(
                name: "assinatura_id",
                table: "pagamentos",
                newName: "assinatura_aluno_id");

            migrationBuilder.RenameIndex(
                name: "ix_pagamentos_assinatura_id_status",
                table: "pagamentos",
                newName: "ix_pagamentos_assinatura_aluno_id_status");

            migrationBuilder.RenameIndex(
                name: "ix_pagamentos_assinatura_id_pendente_unique",
                table: "pagamentos",
                newName: "ix_pagamentos_assinatura_aluno_id_pendente_unique");

            // --- Re-add cross-table FKs pointing at the renamed tables ---
            migrationBuilder.AddForeignKey(
                name: "fk_pagamentos_assinaturas_aluno_assinatura_aluno_id",
                table: "pagamentos",
                column: "assinatura_aluno_id",
                principalTable: "assinaturas_aluno",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_treinadores_planos_plataforma_plano_plataforma_id",
                table: "treinadores",
                column: "plano_plataforma_id",
                principalTable: "planos_plataforma",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vinculos_treinador_aluno_pacotes_pacote_id",
                table: "vinculos_treinador_aluno",
                column: "pacote_id",
                principalTable: "pacotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop cross-table FKs that reference the renamed tables.
            migrationBuilder.DropForeignKey(
                name: "fk_pagamentos_assinaturas_aluno_assinatura_aluno_id",
                table: "pagamentos");

            migrationBuilder.DropForeignKey(
                name: "fk_treinadores_planos_plataforma_plano_plataforma_id",
                table: "treinadores");

            migrationBuilder.DropForeignKey(
                name: "fk_vinculos_treinador_aluno_pacotes_pacote_id",
                table: "vinculos_treinador_aluno");

            // --- pagamentos: revert FK column + indexes ---
            migrationBuilder.RenameColumn(
                name: "assinatura_aluno_id",
                table: "pagamentos",
                newName: "assinatura_id");

            migrationBuilder.RenameIndex(
                name: "ix_pagamentos_assinatura_aluno_id_status",
                table: "pagamentos",
                newName: "ix_pagamentos_assinatura_id_status");

            migrationBuilder.RenameIndex(
                name: "ix_pagamentos_assinatura_aluno_id_pendente_unique",
                table: "pagamentos",
                newName: "ix_pagamentos_assinatura_id_pendente_unique");

            // --- treinadores: revert FK column + index ---
            migrationBuilder.RenameColumn(
                name: "plano_plataforma_id",
                table: "treinadores",
                newName: "plano_treinador_id");

            migrationBuilder.RenameIndex(
                name: "ix_treinadores_plano_plataforma_id",
                table: "treinadores",
                newName: "ix_treinadores_plano_treinador_id");

            // --- vinculos_treinador_aluno: revert FK column + index ---
            migrationBuilder.RenameColumn(
                name: "pacote_id",
                table: "vinculos_treinador_aluno",
                newName: "pacote_aluno_id");

            migrationBuilder.RenameIndex(
                name: "ix_vinculos_treinador_aluno_pacote_id",
                table: "vinculos_treinador_aluno",
                newName: "ix_vinculos_treinador_aluno_pacote_aluno_id");

            // --- assinaturas_aluno: revert internal FKs, indexes and column ---
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_aluno_alunos_aluno_id TO fk_assinaturas_alunos_aluno_id;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_aluno_pacotes_pacote_id TO fk_assinaturas_pacotes_aluno_pacote_aluno_id;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_aluno_treinadores_treinador_id TO fk_assinaturas_treinadores_treinador_id;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT fk_assinaturas_aluno_vinculos_treinador_aluno_vinculo_id TO fk_assinaturas_vinculos_treinador_aluno_vinculo_id;");

            migrationBuilder.RenameColumn(
                name: "pacote_id",
                table: "assinaturas_aluno",
                newName: "pacote_aluno_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_aluno_aluno_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_aluno_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_aluno_pacote_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_pacote_aluno_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_aluno_treinador_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_treinador_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_aluno_vinculo_id",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_vinculo_id");

            migrationBuilder.RenameIndex(
                name: "ix_assinaturas_aluno_status_data_proxima_cobranca",
                table: "assinaturas_aluno",
                newName: "ix_assinaturas_status_data_proxima_cobranca");

            // --- pacotes: revert internal FK and index ---
            migrationBuilder.Sql("ALTER TABLE pacotes RENAME CONSTRAINT fk_pacotes_treinadores_treinador_id TO fk_pacotes_aluno_treinadores_treinador_id;");
            migrationBuilder.RenameIndex(
                name: "ix_pacotes_treinador_id",
                table: "pacotes",
                newName: "ix_pacotes_aluno_treinador_id");

            // --- Revert primary keys ---
            migrationBuilder.Sql("ALTER TABLE planos_plataforma RENAME CONSTRAINT pk_planos_plataforma TO pk_planos_treinador;");
            migrationBuilder.Sql("ALTER TABLE pacotes RENAME CONSTRAINT pk_pacotes TO pk_pacotes_aluno;");
            migrationBuilder.Sql("ALTER TABLE assinaturas_aluno RENAME CONSTRAINT pk_assinaturas_aluno TO pk_assinaturas;");

            // --- Revert table names ---
            migrationBuilder.RenameTable(
                name: "planos_plataforma",
                newName: "planos_treinador");

            migrationBuilder.RenameTable(
                name: "pacotes",
                newName: "pacotes_aluno");

            migrationBuilder.RenameTable(
                name: "assinaturas_aluno",
                newName: "assinaturas");

            // --- Re-add cross-table FKs pointing at the reverted tables ---
            migrationBuilder.AddForeignKey(
                name: "fk_pagamentos_assinaturas_assinatura_id",
                table: "pagamentos",
                column: "assinatura_id",
                principalTable: "assinaturas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_treinadores_planos_treinador_plano_treinador_id",
                table: "treinadores",
                column: "plano_treinador_id",
                principalTable: "planos_treinador",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vinculos_treinador_aluno_pacotes_aluno_pacote_aluno_id",
                table: "vinculos_treinador_aluno",
                column: "pacote_aluno_id",
                principalTable: "pacotes_aluno",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
