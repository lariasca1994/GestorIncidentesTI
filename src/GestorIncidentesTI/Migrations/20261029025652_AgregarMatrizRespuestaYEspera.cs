using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorIncidentesTI.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMatrizRespuestaYEspera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaLimiteRespuesta",
                table: "Incidentes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaPrimeraRespuesta",
                table: "Incidentes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HorasEnEspera",
                table: "Incidentes",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Impacto",
                table: "Incidentes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InicioEspera",
                table: "Incidentes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RespuestaIncumplida",
                table: "Incidentes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Subcategoria",
                table: "Incidentes",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Urgencia",
                table: "Incidentes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaLimiteRespuesta",
                table: "Incidentes");

            migrationBuilder.DropColumn(
                name: "FechaPrimeraRespuesta",
                table: "Incidentes");

            migrationBuilder.DropColumn(
                name: "HorasEnEspera",
                table: "Incidentes");

            migrationBuilder.DropColumn(
                name: "Impacto",
                table: "Incidentes");

            migrationBuilder.DropColumn(
                name: "InicioEspera",
                table: "Incidentes");

            migrationBuilder.DropColumn(
                name: "RespuestaIncumplida",
                table: "Incidentes");

            migrationBuilder.DropColumn(
                name: "Subcategoria",
                table: "Incidentes");

            migrationBuilder.DropColumn(
                name: "Urgencia",
                table: "Incidentes");
        }
    }
}
