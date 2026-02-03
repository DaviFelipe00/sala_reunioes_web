using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalaReunioes.Web.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarConfiguracoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuracoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoraAbertura = table.Column<int>(type: "integer", nullable: false),
                    HoraFechamento = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracoes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configuracoes");
        }
    }
}
