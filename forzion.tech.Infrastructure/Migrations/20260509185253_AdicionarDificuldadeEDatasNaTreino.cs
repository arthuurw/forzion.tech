using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDificuldadeEDatasNaTreino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "data_fim",
                table: "treinos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_inicio",
                table: "treinos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dificuldade",
                table: "treinos",
                type: "text",
                nullable: false,
                defaultValue: "Iniciante");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_fim",
                table: "treinos");

            migrationBuilder.DropColumn(
                name: "data_inicio",
                table: "treinos");

            migrationBuilder.DropColumn(
                name: "dificuldade",
                table: "treinos");
        }
    }
}
