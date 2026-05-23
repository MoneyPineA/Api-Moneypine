using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingTablesAndColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MONEYPINE-FIX: Notifications table was missing from Railway DB despite being in InitialCreate
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `Notifications` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `UsuarioId` int NOT NULL,
                    `Message` longtext CHARACTER SET utf8mb4 NOT NULL,
                    `Level` int NOT NULL,
                    `IsRead` tinyint(1) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    CONSTRAINT `PK_Notifications` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            // MONEYPINE-FIX: Description and UserId columns missing from ActivityLogs
            // (AddActivityLogDescriptionUserId migration was not applied on Railway DB)
            migrationBuilder.Sql(@"
                ALTER TABLE `ActivityLogs`
                    ADD COLUMN IF NOT EXISTS `Description` longtext CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `UserId` int NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `Notifications`;");
            migrationBuilder.Sql("ALTER TABLE `ActivityLogs` DROP COLUMN IF EXISTS `Description`, DROP COLUMN IF EXISTS `UserId`;");
        }
    }
}
