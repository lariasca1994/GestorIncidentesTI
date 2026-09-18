using GestorIncidentesTI.Models;
using Microsoft.AspNetCore.Identity;

namespace GestorIncidentesTI.Data;

public class ApplicationUser : IdentityUser
{
    public int? ProyectoId { get; set; } // null para el Admin, que no pertenece a un solo proyecto
    public Proyecto? Proyecto { get; set; }
}