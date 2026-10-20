using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoPagamentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDataEmprestimo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataEmprestimo",
                table: "dividas",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(2026, 11, 5));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataEmprestimo",
                table: "dividas");
        }
    }
}
