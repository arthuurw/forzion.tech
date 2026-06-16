using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryLogPseudonimizarRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payload",
                table: "whatsapp_delivery_logs");

            migrationBuilder.DropColumn(
                name: "recipient_phone",
                table: "whatsapp_delivery_logs");

            migrationBuilder.DropColumn(
                name: "payload",
                table: "email_delivery_logs");

            migrationBuilder.DropColumn(
                name: "recipient_email",
                table: "email_delivery_logs");

            migrationBuilder.AddColumn<string>(
                name: "recipient_phone_hash",
                table: "whatsapp_delivery_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient_email_hash",
                table: "email_delivery_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE whatsapp_delivery_logs SET recipient_phone_hash = '(anonimizado)';");
            migrationBuilder.Sql("UPDATE email_delivery_logs SET recipient_email_hash = '(anonimizado)';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recipient_phone_hash",
                table: "whatsapp_delivery_logs");

            migrationBuilder.DropColumn(
                name: "recipient_email_hash",
                table: "email_delivery_logs");

            migrationBuilder.AddColumn<string>(
                name: "payload",
                table: "whatsapp_delivery_logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient_phone",
                table: "whatsapp_delivery_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "payload",
                table: "email_delivery_logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient_email",
                table: "email_delivery_logs",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");
        }
    }
}
