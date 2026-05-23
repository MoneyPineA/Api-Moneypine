using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteDomicilioFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ActivityLogs");

            migrationBuilder.RenameTable(
                name: "Notifications",
                newName: "notifications");

            migrationBuilder.AddColumn<string>(
                name: "cp",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "estado_domicilio",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "municipio",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "num_ext",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notifications",
                table: "notifications",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "cp",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "estado_domicilio",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "municipio",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "num_ext",
                table: "cliente");

            migrationBuilder.RenameTable(
                name: "notifications",
                newName: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ActivityLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ActivityLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications",
                column: "Id");
        }
    }
}
