using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarWhatsAppDeliveryLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "whatsapp_delivery_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meta_message_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_whatsapp_delivery_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_delivery_logs_event_type",
                table: "whatsapp_delivery_logs",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_delivery_logs_meta_message_id",
                table: "whatsapp_delivery_logs",
                column: "meta_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_delivery_logs");
        }
    }
}
