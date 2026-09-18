using GestorIncidentesTI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorIncidentesTI.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute(typeof(ApplicationDbContext))]
[Migration("20260917000000_AgregarAuditoriaDeUsuarios")]
public partial class AgregarAuditoriaDeUsuarios : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CreadoPorUserId", table: "Incidentes", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ModificadoPorUserId", table: "HistorialEstados", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CreadoPorUserId", table: "Incidentes");
        migrationBuilder.DropColumn(name: "ModificadoPorUserId", table: "HistorialEstados");
    }
}
