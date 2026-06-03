using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddGerenteToGerencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gerente_id",
                table: "gerencia",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gerencia_gerente_id",
                table: "gerencia",
                column: "gerente_id");

            migrationBuilder.AddForeignKey(
                name: "FK_gerencia_usuario_gerente_id",
                table: "gerencia",
                column: "gerente_id",
                principalTable: "usuario",
                principalColumn: "usuario_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gerencia_usuario_gerente_id",
                table: "gerencia");

            migrationBuilder.DropIndex(
                name: "IX_gerencia_gerente_id",
                table: "gerencia");

            migrationBuilder.DropColumn(
                name: "gerente_id",
                table: "gerencia");
        }
    }
}
