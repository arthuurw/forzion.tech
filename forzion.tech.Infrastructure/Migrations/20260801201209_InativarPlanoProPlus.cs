using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InativarPlanoProPlus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE planos_plataforma SET is_ativo = false, updated_at = now() WHERE tier = 'ProPlus' AND is_ativo = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE planos_plataforma SET is_ativo = true, updated_at = now() WHERE tier = 'ProPlus' AND is_ativo = false;");
        }
    }
}
