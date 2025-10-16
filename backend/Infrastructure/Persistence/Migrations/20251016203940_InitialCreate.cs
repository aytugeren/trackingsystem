using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    SiraNo = table.Column<int>(type: "integer", nullable: false),
                    MusteriAdSoyad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TCKN = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    SiraNo = table.Column<int>(type: "integer", nullable: false),
                    MusteriAdSoyad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TCKN = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OdemeSekli = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Invoices");
        }
    }
}
