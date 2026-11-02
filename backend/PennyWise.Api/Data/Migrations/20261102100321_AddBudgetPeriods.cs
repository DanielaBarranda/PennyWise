using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PennyWise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Month",
                table: "Budgets");

            migrationBuilder.AddColumn<string>(
                name: "Period",
                table: "Budgets",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Budgets",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Every budget before this migration was a monthly one: it starts on the
            // 1st of its month. Filled in before the unique index below is created.
            migrationBuilder.Sql("UPDATE Budgets SET StartDate = Month || '-01';");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Month",
                table: "Budgets",
                columns: new[] { "UserId", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Period_StartDate",
                table: "Budgets",
                columns: new[] { "UserId", "Period", "StartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Month",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Period_StartDate",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Budgets");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Month",
                table: "Budgets",
                columns: new[] { "UserId", "Month" },
                unique: true);
        }
    }
}
