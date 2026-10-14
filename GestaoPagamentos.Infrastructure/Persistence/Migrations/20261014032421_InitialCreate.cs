using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoPagamentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiasAntecedenciaLembrete = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervaloEnvioLoteMs = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateLembrete = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    TemplateAtraso = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "devedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Telefone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dividas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DevedorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ValorEmprestado = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TaxaJuros = table.Column<decimal>(type: "TEXT", precision: 7, scale: 4, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DataPrimeiroVencimento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantidadeParcelas = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dividas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dividas_devedores_DevedorId",
                        column: x => x.DevedorId,
                        principalTable: "devedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parcelas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DividaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    ValorPrevisto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcelas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parcelas_dividas_DividaId",
                        column: x => x.DividaId,
                        principalTable: "dividas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DevedorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParcelaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Mensagem = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DataGeracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DataEnvio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notificacoes_devedores_DevedorId",
                        column: x => x.DevedorId,
                        principalTable: "devedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notificacoes_parcelas_ParcelaId",
                        column: x => x.ParcelaId,
                        principalTable: "parcelas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParcelaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pagamentos_parcelas_ParcelaId",
                        column: x => x.ParcelaId,
                        principalTable: "parcelas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_devedores_Nome",
                table: "devedores",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_dividas_DevedorId",
                table: "dividas",
                column: "DevedorId");

            migrationBuilder.CreateIndex(
                name: "IX_dividas_Status",
                table: "dividas",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_DevedorId",
                table: "notificacoes",
                column: "DevedorId");

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_ParcelaId_Tipo",
                table: "notificacoes",
                columns: new[] { "ParcelaId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_Status",
                table: "notificacoes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_ParcelaId",
                table: "pagamentos",
                column: "ParcelaId");

            migrationBuilder.CreateIndex(
                name: "IX_parcelas_DataVencimento",
                table: "parcelas",
                column: "DataVencimento");

            migrationBuilder.CreateIndex(
                name: "IX_parcelas_DividaId_Numero",
                table: "parcelas",
                columns: new[] { "DividaId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes");

            migrationBuilder.DropTable(
                name: "notificacoes");

            migrationBuilder.DropTable(
                name: "pagamentos");

            migrationBuilder.DropTable(
                name: "parcelas");

            migrationBuilder.DropTable(
                name: "dividas");

            migrationBuilder.DropTable(
                name: "devedores");
        }
    }
}
