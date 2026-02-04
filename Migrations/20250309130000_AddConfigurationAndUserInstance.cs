using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanzaTuIdea.Api.Migrations;

public partial class AddConfigurationAndUserInstance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Instancia",
            table: "AppUsers",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Classifications",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Activo = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Classifications", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Instances",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Activo = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Instances", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Classifications");
        migrationBuilder.DropTable(name: "Instances");
        migrationBuilder.DropColumn(name: "Instancia", table: "AppUsers");
    }
}
