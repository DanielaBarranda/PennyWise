using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PennyWise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetCarryOvers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetCarryOvers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromBudgetId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToBudgetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SavingsTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCarryOvers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetCarryOvers_Budgets_FromBudgetId",
                        column: x => x.FromBudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BudgetCarryOvers_Budgets_ToBudgetId",
                        column: x => x.ToBudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BudgetCarryOvers_SavingsTransactions_SavingsTransactionId",
                        column: x => x.SavingsTransactionId,
                        principalTable: "SavingsTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BudgetCarryOvers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCarryOvers_FromBudgetId",
                table: "BudgetCarryOvers",
                column: "FromBudgetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCarryOvers_SavingsTransactionId",
                table: "BudgetCarryOvers",
                column: "SavingsTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCarryOvers_ToBudgetId",
                table: "BudgetCarryOvers",
                column: "ToBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCarryOvers_UserId",
                table: "BudgetCarryOvers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetCarryOvers");
        }
    }
}
