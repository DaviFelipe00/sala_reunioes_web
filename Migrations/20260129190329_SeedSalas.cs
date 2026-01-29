using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SalaReunioes.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedSalas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Salas",
                columns: new[] { "Id", "Capacidade", "Nome" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), 12, "Sala 1" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), 12, "Sala 2" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), 12, "Sala 3" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), 8, "Sala 4" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), 8, "Sala 5" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), 8, "Sala 6" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"));
        }
    }
}
