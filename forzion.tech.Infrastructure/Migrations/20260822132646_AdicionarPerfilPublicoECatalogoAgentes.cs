using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPerfilPublicoECatalogoAgentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_endereco_bairro",
                table: "treinadores",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_endereco_cep",
                table: "treinadores",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_endereco_cidade",
                table: "treinadores",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_endereco_complemento",
                table: "treinadores",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_endereco_estado",
                table: "treinadores",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_endereco_numero",
                table: "treinadores",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_endereco_rua",
                table: "treinadores",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "perfil_publico_is_publicado",
                table: "treinadores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_nome_fantasia",
                table: "treinadores",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "perfil_publico_politicas",
                table: "treinadores",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "perfil_publico_updated_at",
                table: "treinadores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "categoria",
                table: "pacotes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duracao_minutos",
                table: "pacotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_publico",
                table: "pacotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "trial_disponivel",
                table: "pacotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "horarios_funcionamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_semana = table.Column<int>(type: "integer", nullable: false),
                    abre_as = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    fecha_as = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_horarios_funcionamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_horarios_funcionamento_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_horarios_funcionamento_treinador_id",
                table: "horarios_funcionamento",
                column: "treinador_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "horarios_funcionamento");

            migrationBuilder.DropColumn(
                name: "perfil_publico_endereco_bairro",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_endereco_cep",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_endereco_cidade",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_endereco_complemento",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_endereco_estado",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_endereco_numero",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_endereco_rua",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_is_publicado",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_nome_fantasia",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_politicas",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "perfil_publico_updated_at",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "categoria",
                table: "pacotes");

            migrationBuilder.DropColumn(
                name: "duracao_minutos",
                table: "pacotes");

            migrationBuilder.DropColumn(
                name: "is_publico",
                table: "pacotes");

            migrationBuilder.DropColumn(
                name: "trial_disponivel",
                table: "pacotes");
        }
    }
}
