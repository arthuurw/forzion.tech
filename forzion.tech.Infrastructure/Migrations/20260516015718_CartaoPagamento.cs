using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CartaoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_secret",
                schema: "homolog",
                table: "pagamentos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metodo_pagamento",
                schema: "homolog",
                table: "pagamentos",
                type: "text",
                nullable: false,
                defaultValue: "Pix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_secret",
                schema: "homolog",
                table: "pagamentos");

            migrationBuilder.DropColumn(
                name: "metodo_pagamento",
                schema: "homolog",
                table: "pagamentos");
        }
    }
}
