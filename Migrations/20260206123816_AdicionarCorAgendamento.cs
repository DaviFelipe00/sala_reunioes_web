using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalaReunioes.Web.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCorAgendamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cor",
                table: "Agendamentos",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cor",
                table: "Agendamentos");
        }
    }
}
