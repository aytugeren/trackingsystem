using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    public partial class AddGoldFormulaEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresFormula",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultFormulaId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoldFormulaTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    FormulaType = table.Column<int>(type: "integer", nullable: false),
                    DslVersion = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldFormulaTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoldProductFormulaBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoldProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormulaTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldProductFormulaBindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoldFormulaTemplates_Code",
                table: "GoldFormulaTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldProductFormulaBindings_Product_Direction_Active",
                table: "GoldProductFormulaBindings",
                columns: new[] { "GoldProductId", "Direction", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldProductFormulaBindings_Template",
                table: "GoldProductFormulaBindings",
                column: "FormulaTemplateId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldProductFormulaBindings");

            migrationBuilder.DropTable(
                name: "GoldFormulaTemplates");

            migrationBuilder.DropColumn(
                name: "RequiresFormula",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DefaultFormulaId",
                table: "Products");
        }
    }
}
