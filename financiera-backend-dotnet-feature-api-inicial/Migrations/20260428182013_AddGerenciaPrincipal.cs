using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddGerenciaPrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_principal",
                table: "gerencia",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "es_principal",
                table: "gerencia");
        }
    }
}
