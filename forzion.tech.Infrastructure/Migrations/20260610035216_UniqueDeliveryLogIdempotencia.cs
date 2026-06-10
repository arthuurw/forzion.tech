using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueDeliveryLogIdempotencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CAVEAT: a criação do índice UNIQUE falha se já existirem linhas duplicadas em
            // (resend_message_id, event_type) / (meta_message_id, event_type). Em ambiente já populado,
            // deduplicar antes de aplicar. Em produção atual as tabelas estão vazias (feature nova).
            migrationBuilder.DropIndex(
                name: "ix_whatsapp_delivery_logs_meta_message_id",
                table: "whatsapp_delivery_logs");

            migrationBuilder.DropIndex(
                name: "ix_email_delivery_logs_resend_message_id",
                table: "email_delivery_logs");

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_delivery_logs_meta_message_id_event_type",
                table: "whatsapp_delivery_logs",
                columns: new[] { "meta_message_id", "event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_delivery_logs_resend_message_id_event_type",
                table: "email_delivery_logs",
                columns: new[] { "resend_message_id", "event_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_whatsapp_delivery_logs_meta_message_id_event_type",
                table: "whatsapp_delivery_logs");

            migrationBuilder.DropIndex(
                name: "ix_email_delivery_logs_resend_message_id_event_type",
                table: "email_delivery_logs");

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_delivery_logs_meta_message_id",
                table: "whatsapp_delivery_logs",
                column: "meta_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_delivery_logs_resend_message_id",
                table: "email_delivery_logs",
                column: "resend_message_id");
        }
    }
}
