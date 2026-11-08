using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PennyWise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SavingsFundsAndSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpenseId",
                table: "SavingsTransactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FromOutside",
                table: "SavingsTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetAmount",
                table: "SavingsGoals",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsTransactions_ExpenseId",
                table: "SavingsTransactions",
                column: "ExpenseId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SavingsTransactions_Expenses_ExpenseId",
                table: "SavingsTransactions",
                column: "ExpenseId",
                principalTable: "Expenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavingsTransactions_Expenses_ExpenseId",
                table: "SavingsTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SavingsTransactions_ExpenseId",
                table: "SavingsTransactions");

            migrationBuilder.DropColumn(
                name: "ExpenseId",
                table: "SavingsTransactions");

            migrationBuilder.DropColumn(
                name: "FromOutside",
                table: "SavingsTransactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetAmount",
                table: "SavingsGoals",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
