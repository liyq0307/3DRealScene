using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealScene3D.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertEvents_AlertRules_AlertRuleId",
                table: "AlertEvents");

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_CreatedBy",
                table: "Dashboards",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_CreatedBy",
                table: "AlertRules",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertEvents_AlertRules_AlertRuleId",
                table: "AlertEvents",
                column: "AlertRuleId",
                principalTable: "AlertRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlertRules_Users_CreatedBy",
                table: "AlertRules",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dashboards_Users_CreatedBy",
                table: "Dashboards",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertEvents_AlertRules_AlertRuleId",
                table: "AlertEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_AlertRules_Users_CreatedBy",
                table: "AlertRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Dashboards_Users_CreatedBy",
                table: "Dashboards");

            migrationBuilder.DropIndex(
                name: "IX_Dashboards_CreatedBy",
                table: "Dashboards");

            migrationBuilder.DropIndex(
                name: "IX_AlertRules_CreatedBy",
                table: "AlertRules");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertEvents_AlertRules_AlertRuleId",
                table: "AlertEvents",
                column: "AlertRuleId",
                principalTable: "AlertRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
