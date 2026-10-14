using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoPagamentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JurosNaDivida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxaJuros",
                table: "dividas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorEmprestado",
                table: "dividas",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("UPDATE dividas SET \"ValorEmprestado\" = \"ValorTotal\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxaJuros",
                table: "dividas");

            migrationBuilder.DropColumn(
                name: "ValorEmprestado",
                table: "dividas");
        }
    }
}
