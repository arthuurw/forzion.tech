using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarUniqueParcialResetTokenPendente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_password_reset_tokens_conta_id",
                table: "password_reset_tokens");

            migrationBuilder.Sql(
                """
                UPDATE password_reset_tokens t
                SET used_at = created_at
                WHERE used_at IS NULL
                  AND id <> (
                    SELECT id FROM password_reset_tokens t2
                    WHERE t2.conta_id = t.conta_id AND t2.used_at IS NULL
                    ORDER BY created_at DESC, id DESC
                    LIMIT 1
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "ux_password_reset_tokens_conta_id_pendente",
                table: "password_reset_tokens",
                column: "conta_id",
                unique: true,
                filter: "used_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_password_reset_tokens_conta_id_pendente",
                table: "password_reset_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_conta_id",
                table: "password_reset_tokens",
                column: "conta_id");
        }
    }
}
