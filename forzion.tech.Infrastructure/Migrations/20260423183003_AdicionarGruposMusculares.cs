using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarGruposMusculares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grupos_musculares",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_musculares", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grupos_musculares_nome",
                schema: "homolog",
                table: "grupos_musculares",
                column: "nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grupos_musculares",
                schema: "homolog");
        }
    }
}
