using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarNotificacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // lint-migrations:allow — o índice único parcial recai sobre "notificacoes",
            // criada nesta mesma migration (tabela vazia), logo sem risco de duplicatas pré-existentes.
            migrationBuilder.AddColumn<bool>(
                name: "notificacoes_engajamento_email_opt_out",
                table: "contas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "notificacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatario_conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    corpo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    link_relativo = table.Column<string>(type: "text", nullable: true),
                    dia_referencia = table.Column<DateOnly>(type: "date", nullable: true),
                    lida = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_notificacoes_contas_destinatario_conta_id",
                        column: x => x.destinatario_conta_id,
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_conta_lida_created",
                table: "notificacoes",
                columns: new[] { "destinatario_conta_id", "lida", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_dedup",
                table: "notificacoes",
                columns: new[] { "destinatario_conta_id", "tipo", "dia_referencia" },
                unique: true,
                filter: "dia_referencia IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacoes");

            migrationBuilder.DropColumn(
                name: "notificacoes_engajamento_email_opt_out",
                table: "contas");
        }
    }
}
