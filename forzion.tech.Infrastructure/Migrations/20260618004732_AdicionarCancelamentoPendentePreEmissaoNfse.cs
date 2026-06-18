using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCancelamentoPendentePreEmissaoNfse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cancelamento_pendente_pre_emissao",
                table: "notas_fiscais",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "motivo_cancelamento_pendente",
                table: "notas_fiscais",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancelamento_pendente_pre_emissao",
                table: "notas_fiscais");

            migrationBuilder.DropColumn(
                name: "motivo_cancelamento_pendente",
                table: "notas_fiscais");
        }
    }
}
