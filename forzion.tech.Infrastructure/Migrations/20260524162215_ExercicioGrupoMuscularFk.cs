using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExercicioGrupoMuscularFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "grupo_muscular_id",
                schema: "homolog",
                table: "exercicios",
                type: "uuid",
                nullable: true);

            // Garante que todo grupo referenciado por exercícios exista como entidade
            // (o grupo era armazenado como string do enum; o nome casa 1:1 com grupos_musculares).
            migrationBuilder.Sql(@"
                INSERT INTO homolog.grupos_musculares (id, nome, created_at)
                SELECT gen_random_uuid(), d.grupo_muscular, now()
                FROM (SELECT DISTINCT grupo_muscular FROM homolog.exercicios) d
                WHERE NOT EXISTS (
                    SELECT 1 FROM homolog.grupos_musculares g WHERE g.nome = d.grupo_muscular
                );");

            // Backfill do FK a partir do nome do grupo.
            migrationBuilder.Sql(@"
                UPDATE homolog.exercicios e
                SET grupo_muscular_id = g.id
                FROM homolog.grupos_musculares g
                WHERE g.nome = e.grupo_muscular;");

            migrationBuilder.AlterColumn<Guid>(
                name: "grupo_muscular_id",
                schema: "homolog",
                table: "exercicios",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_exercicios_grupo_muscular_id",
                schema: "homolog",
                table: "exercicios",
                column: "grupo_muscular_id");

            migrationBuilder.AddForeignKey(
                name: "fk_exercicios_grupos_musculares_grupo_muscular_id",
                schema: "homolog",
                table: "exercicios",
                column: "grupo_muscular_id",
                principalSchema: "homolog",
                principalTable: "grupos_musculares",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "grupo_muscular",
                schema: "homolog",
                table: "exercicios");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grupo_muscular",
                schema: "homolog",
                table: "exercicios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE homolog.exercicios e
                SET grupo_muscular = g.nome
                FROM homolog.grupos_musculares g
                WHERE g.id = e.grupo_muscular_id;");

            migrationBuilder.DropForeignKey(
                name: "fk_exercicios_grupos_musculares_grupo_muscular_id",
                schema: "homolog",
                table: "exercicios");

            migrationBuilder.DropIndex(
                name: "ix_exercicios_grupo_muscular_id",
                schema: "homolog",
                table: "exercicios");

            migrationBuilder.DropColumn(
                name: "grupo_muscular_id",
                schema: "homolog",
                table: "exercicios");
        }
    }
}
