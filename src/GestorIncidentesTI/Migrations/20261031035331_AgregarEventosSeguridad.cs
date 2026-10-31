using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorIncidentesTI.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEventosSeguridad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventosSeguridad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosSeguridad", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosSeguridad_Fecha",
                table: "EventosSeguridad",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_EventosSeguridad_UsuarioId",
                table: "EventosSeguridad",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosSeguridad");
        }
    }
}
