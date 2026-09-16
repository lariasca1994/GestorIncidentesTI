# Gestor de Incidentes TI (mini ITSM)

Proyecto de práctica en **ASP.NET Core 8 / C#**. Gestiona incidentes de soporte técnico con
prioridad, SLA, escalamiento automático N1 → N2 → N3 y trazabilidad completa de cada cambio
de estado.

> Nota: C# / .NET no forma parte de mis certificaciones formales actuales — este es un
> proyecto autodidacta para aplicar en código la experiencia real en gestión de incidentes,
> SLA e ITIL v4.

## Funcionalidades

- Registro de incidentes con categoría, prioridad y solicitante
- Cálculo automático de fecha límite según SLA definido por prioridad
- Escalamiento automático cuando se supera el umbral de tiempo de SLA (`BackgroundService`)
- Dashboard con incidentes abiertos, % de cumplimiento de SLA y SLA incumplidos
- Historial de auditoría por cada cambio de estado o nivel

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core 8 (Web API + Razor Pages) |
| ORM | Entity Framework Core 8 |
| Base de datos | SQL Server / Azure SQL |
| Background jobs | `IHostedService` (in-process, sin infraestructura extra) |

## Estructura

```
GestorIncidentesTI/
├── GestorIncidentesTI.sln
└── src/GestorIncidentesTI/
    ├── Controllers/       # API REST
    ├── Data/               # DbContext + seed de SLA
    ├── Models/             # Entidades, enums y DTOs
    ├── Pages/              # Dashboard (Razor Pages)
    ├── Services/           # Lógica de SLA y escalamiento
    └── Program.cs
```

## Correr localmente

1. Instala el [.NET 8 SDK](https://dotnet.microsoft.com/download) y SQL Server (o usa el
   contenedor: `docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=TuPasswordAqui" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`).
2. Copia `appsettings.Development.json.example` a `appsettings.Development.json` y pon tu
   connection string real ahí (ese archivo está en `.gitignore`, nunca se sube).
3. Restaura y aplica migraciones:
   ```bash
   cd src/GestorIncidentesTI
   dotnet restore
   dotnet ef migrations add InicialSchema
   dotnet ef database update
   ```
4. Corre la app:
   ```bash
   dotnet run
   ```
5. Dashboard en `https://localhost:5001`, API en `https://localhost:5001/api/incidentes`.

## Despliegue en Azure (plan gratuito)

1. **Azure SQL Database** (oferta free): crea la base, copia la connection string.
2. **Azure App Service** (plan F1, gratis):
   ```bash
   az webapp up --name gestor-incidentes-ti --resource-group <tu-rg> --sku F1 --runtime "DOTNETCORE:8.0"
   ```
3. Configura la connection string real como **Application Setting** en el portal de Azure
   (App Service → Configuration → Connection strings) — nunca en `appsettings.json`.
4. Al iniciar, la app aplica migraciones automáticamente (`db.Database.Migrate()` en
   `Program.cs`), así que no hace falta correr `dotnet ef database update` manualmente en Azure.

## Pendiente / próximos pasos

- Autenticación (por ahora no hay control de acceso — usar Azure AD o Identity antes de
  considerarlo terminado para un entorno real)
- Notificaciones (email/Teams) cuando un incidente escala
- Pruebas unitarias de `SlaService` (cálculo de fecha límite y umbral de escalamiento)
