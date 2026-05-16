using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SegurancaPagamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pagamentos_assinatura_id",
                schema: "homolog",
                table: "pagamentos");

            migrationBuilder.DropIndex(
                name: "ix_pagamentos_stripe_payment_intent_id",
                schema: "homolog",
                table: "pagamentos");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_assinatura_id_pendente_unique",
                schema: "homolog",
                table: "pagamentos",
                column: "assinatura_id",
                unique: true,
                filter: "status = 'Pendente'");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_stripe_payment_intent_id",
                schema: "homolog",
                table: "pagamentos",
                column: "stripe_payment_intent_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pagamentos_assinatura_id_pendente_unique",
                schema: "homolog",
                table: "pagamentos");

            migrationBuilder.DropIndex(
                name: "ix_pagamentos_stripe_payment_intent_id",
                schema: "homolog",
                table: "pagamentos");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_assinatura_id",
                schema: "homolog",
                table: "pagamentos",
                column: "assinatura_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_stripe_payment_intent_id",
                schema: "homolog",
                table: "pagamentos",
                column: "stripe_payment_intent_id");
        }
    }
}
