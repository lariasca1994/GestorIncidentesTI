using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestorIncidentesTI.Migrations
{
    /// <inheritdoc />
    public partial class InicialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incidentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prioridad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NivelActual = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AsignadoA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaLimiteSla = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaIncumplido = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidentes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaDefiniciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    HorasRespuesta = table.Column<int>(type: "int", nullable: false),
                    HorasResolucion = table.Column<int>(type: "int", nullable: false),
                    UmbralEscalamiento = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaDefiniciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidenteId = table.Column<int>(type: "int", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "int", nullable: false),
                    EstadoNuevo = table.Column<int>(type: "int", nullable: false),
                    NivelAnterior = table.Column<int>(type: "int", nullable: false),
                    NivelNuevo = table.Column<int>(type: "int", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstados_Incidentes_IncidenteId",
                        column: x => x.IncidenteId,
                        principalTable: "Incidentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SlaDefiniciones",
                columns: new[] { "Id", "HorasResolucion", "HorasRespuesta", "Prioridad", "UmbralEscalamiento" },
                values: new object[,]
                {
                    { 1, 4, 1, 4, 0.69999999999999996 },
                    { 2, 8, 2, 3, 0.75 },
                    { 3, 24, 4, 2, 0.80000000000000004 },
                    { 4, 72, 8, 1, 0.84999999999999998 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstados_IncidenteId",
                table: "HistorialEstados",
                column: "IncidenteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialEstados");

            migrationBuilder.DropTable(
                name: "SlaDefiniciones");

            migrationBuilder.DropTable(
                name: "Incidentes");
        }
    }
}
