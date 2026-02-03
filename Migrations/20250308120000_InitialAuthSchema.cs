using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanzaTuIdea.Api.Migrations;

public partial class InitialAuthSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppUsers",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Codigo_Empleado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Employees",
            columns: table => new
            {
                Codigo_Empleado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Apellido1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Apellido2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                E_Mail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Departamento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Estatus = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false, defaultValue: "A")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Employees", x => x.Codigo_Empleado);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Ideas",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                CodigoEmpleado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Detalle = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Clasificacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Via = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                AdminComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Ideas", x => x.Id);
                table.ForeignKey(
                    name: "FK_Ideas_AppUsers_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserRoles",
            columns: table => new
            {
                UserId = table.Column<int>(type: "int", nullable: false),
                RoleId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_UserRoles_AppUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "IdeaHistories",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                IdeaId = table.Column<int>(type: "int", nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                ChangeType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdeaHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_IdeaHistories_AppUsers_ChangedByUserId",
                    column: x => x.ChangedByUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.NoAction);
                table.ForeignKey(
                    name: "FK_IdeaHistories_Ideas_IdeaId",
                    column: x => x.IdeaId,
                    principalTable: "Ideas",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_UserName",
            table: "AppUsers",
            column: "UserName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IdeaHistories_ChangedByUserId",
            table: "IdeaHistories",
            column: "ChangedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_IdeaHistories_IdeaId",
            table: "IdeaHistories",
            column: "IdeaId");

        migrationBuilder.CreateIndex(
            name: "IX_Ideas_CreatedByUserId",
            table: "Ideas",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Name",
            table: "Roles",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_RoleId",
            table: "UserRoles",
            column: "RoleId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IdeaHistories");
        migrationBuilder.DropTable(name: "UserRoles");
        migrationBuilder.DropTable(name: "Ideas");
        migrationBuilder.DropTable(name: "Roles");
        migrationBuilder.DropTable(name: "Employees");
        migrationBuilder.DropTable(name: "AppUsers");
    }
}
