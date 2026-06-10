using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarOutboxEfeitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_efeitos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    proxima_tentativa = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultimo_erro = table.Column<string>(type: "text", nullable: true),
                    chave_idempotencia = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_efeitos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_efeitos_chave_idempotencia_unique",
                table: "outbox_efeitos",
                column: "chave_idempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_efeitos_status_proxima_tentativa",
                table: "outbox_efeitos",
                columns: new[] { "status", "proxima_tentativa" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_efeitos");
        }
    }
}
