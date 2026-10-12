# 📘 Manual de usuario · Gestor de Incidentes TI

Guía para usar la plataforma paso a paso: qué significa cada campo, qué datos se ingresan y qué
protege la plataforma. Para la parte técnica vuelve al [README](../README.md).

**Demo:** [Gestor de Incidentes TI (Azure Container Apps)](https://gestorincidentesti.livelywater-fe29fe0b.australiaeast.azurecontainerapps.io/)

## Contenido

1. [En pocas palabras](#1-en-pocas-palabras)
2. [Antes de empezar](#2-antes-de-empezar)
3. [Paso a paso](#3-paso-a-paso)
4. [Qué información ingresas y dónde queda](#4-qué-información-ingresas-y-dónde-queda)
5. [Seguridad: qué se garantiza](#5-seguridad-qué-se-garantiza)
6. [Límites y recomendaciones](#6-límites-y-recomendaciones)
7. [Preguntas frecuentes](#7-preguntas-frecuentes)

---

## 1. En pocas palabras

Una mesa de ayuda: alguien reporta una falla (un **incidente**), el sistema le calcula una fecha
límite según su prioridad (**SLA**) y, si no se resuelve a tiempo, lo **escala solo** al siguiente
nivel de soporte: N1 → N2 → N3. Cada cambio queda en el historial.

## 2. Antes de empezar

- Solo necesitas un navegador.
- La aplicación y su base de datos se apagan sin uso. Si ves **"El servicio está en una ventana de
  mantenimiento"**, la base se está encendiendo: espera uno o dos minutos y pulsa **Reintentar**.
  Ningún dato se pierde.
- Las capturas de este manual se tomaron con una cuenta **Admin**, que ve todos los proyectos. Una
  cuenta normal ve solo los incidentes de su proyecto.

## 3. Paso a paso

### 3.1 Crear una cuenta

![Formulario de registro con sus campos numerados](img/manual/01-registro.png)

| # | Campo | Qué escribir | Reglas |
|:-:|---|---|---|
| 1 | Correo | El correo con el que vas a entrar | Formato válido y único |
| 2 | Proyecto | El proyecto al que perteneces | Solo verás y gestionarás los incidentes de ese proyecto |
| 3 | Contraseña | Tu contraseña | Mínimo 6 caracteres, con al menos una mayúscula, una minúscula y un número |
| 4 | Confirmar contraseña | La misma contraseña | Debe coincidir con el campo 3 |
| 5 | Crear cuenta | Botón | Crea la cuenta con rol **Usuario** |

El rol **Admin** no se puede pedir desde el formulario: las cuentas administradoras son fijas y se
configuran en el servidor.

### 3.2 Iniciar sesión

![Formulario de inicio de sesión](img/manual/02-login.png)

| # | Campo | Qué escribir |
|:-:|---|---|
| 1 | Correo | El correo de tu cuenta |
| 2 | Contraseña | Tu contraseña |
| 3 | Recordarme en este equipo | Márcalo solo en tu equipo personal: la sesión se mantiene al cerrar el navegador |
| 4 | Ingresar | Entra a la aplicación |
| 5 | ¿Olvidaste tu contraseña? | Inicia la recuperación (ver [Límites](#6-límites-y-recomendaciones)) |

### 3.3 Dashboard

![Dashboard con indicadores y lista de incidentes](img/manual/03-dashboard.png)

| Indicador | Qué significa |
|---|---|
| Incidentes abiertos | Incidentes que aún no se han resuelto ni cerrado |
| SLA incumplido | Incidentes que pasaron su fecha límite |
| Cumplimiento de SLA | Porcentaje de incidentes atendidos dentro del plazo |

La tabla muestra cada incidente con su **Prioridad**, **Estado**, **Nivel** (N1, N2 o N3) y el
tiempo restante o vencido de su **SLA**.

### 3.4 Incidentes

![Lista de incidentes](img/manual/04-incidentes.png)

| # | Elemento | Qué hace |
|:-:|---|---|
| 1 | + Nuevo incidente | Abre el formulario para reportar una falla |
| 2 | Ver | Abre el detalle del incidente |

### 3.5 Reportar un incidente

![Formulario de nuevo incidente con sus campos numerados](img/manual/05-crear-incidente.png)

| # | Campo | Qué escribir |
|:-:|---|---|
| 1 | Proyecto | Proyecto afectado (solo el Admin elige; un Usuario reporta en su propio proyecto) |
| 2 | Título | Resumen corto de la falla |
| 3 | Descripción | Qué pasó, desde cuándo y cómo se manifiesta |
| 4 | Categoría | Tipo de falla: Red, Aplicaciones, Infraestructura… |
| 5 | Prioridad | Baja, Media, Alta o Crítica; define el plazo del SLA |
| 6 | Solicitante | Quién reporta la falla |
| 7 | Crear incidente | Guarda el incidente y calcula su fecha límite |

Plazos por prioridad:

| Prioridad | Respuesta | Resolución | Escala al consumir |
|---|---|---|---|
| Crítica | 1 h | 4 h | 70 % del plazo |
| Alta | 2 h | 8 h | 75 % del plazo |
| Media | 4 h | 24 h | 80 % del plazo |
| Baja | 8 h | 72 h | 85 % del plazo |

### 3.6 Dar seguimiento a un incidente

![Detalle de un incidente con su historial](img/manual/06-detalle.png)

| # | Campo | Qué escribir |
|:-:|---|---|
| 1 | Nuevo estado | Abierto, En progreso, Escalado, Resuelto o Cerrado; pulsa **Actualizar estado** |
| 2 | Comentario (opcional) | Qué se hizo o por qué cambia el estado |

En **Agregar comentario** dejas notas sin cambiar el estado. El **Historial** registra cada cambio
con fecha y comentario, incluidos los escalamientos automáticos.

### 3.7 Proyectos (solo Admin)

![Lista de proyectos para el administrador](img/manual/07-proyectos-admin.png)

El administrador crea los proyectos a los que se unen las cuentas, con **Nombre** y
**Descripción**. No crea usuarios: cada persona se registra sola.

## 4. Qué información ingresas y dónde queda

| Dato | Para qué se usa | Dónde se guarda | Quién lo ve |
|---|---|---|---|
| Correo | Identificar tu cuenta y avisarte | Azure SQL | Tú y los administradores |
| Contraseña | Iniciar sesión | Azure SQL, **solo su huella** (PBKDF2 de ASP.NET Identity) | Nadie: no se puede leer ni recuperar |
| Proyecto | Limitar lo que ves | Azure SQL | Tú y los administradores |
| Incidentes, comentarios y nombre del solicitante | Gestionar la falla | Azure SQL | Las cuentas del mismo proyecto y los administradores |
| Sesión | Mantenerte conectado | Cookie de tu navegador | Cifrada por el servidor e invisible para JavaScript |

## 5. Seguridad: qué se garantiza

Comprobado en el código del proyecto (`Program.cs`, `Controllers/IncidentesController.cs`,
`Pages/`):

- **ASP.NET Core Identity** para las cuentas: contraseñas con PBKDF2 y sal propia por usuario.
- **Cookie de sesión `HttpOnly`** protegida por el servidor; sin sesión, toda página interna
  redirige al inicio de sesión.
- **Aislamiento por proyecto:** el dashboard, la lista, el detalle y la API filtran por el proyecto
  de la cuenta. Cambiar el identificador en la URL no muestra incidentes de otros proyectos.
- **Roles en el servidor:** solo el rol Admin entra a **Proyectos**. Las cuentas Admin son fijas,
  se definen como secretos del servidor y cualquier otra cuenta con ese rol vuelve a ser Usuario.
- **Protección contra CSRF:** los formularios de Razor Pages llevan y validan un token antifalsificación.
- **HTTPS obligatorio** con redirección y HSTS en producción.
- **Historial de auditoría** de cada cambio de estado o nivel.
- **Secretos fuera del repositorio:** la cadena de conexión, las cuentas Admin y la clave de Brevo
  viven en los secretos de la Container App.

## 6. Límites y recomendaciones

Es una demo de portafolio:

- **La recuperación de contraseña no envía correos reales** (el envío de Identity está simulado):
  guarda tu contraseña en un gestor.
- No hay bloqueo de cuenta por intentos fallidos ni segundo factor activo.
- No registres datos personales reales en los incidentes: las cuentas del mismo proyecto los ven.
- Usa **Recordarme en este equipo** solo en dispositivos personales.

## 7. Preguntas frecuentes

**¿Por qué no veo todos los incidentes?** Una cuenta Usuario solo ve los de su proyecto.

**¿Quién escala los incidentes?** El sistema, cada vez que alguien entra a la aplicación: revisa
los plazos y sube de nivel los que superaron su umbral.

**¿Puedo cambiarme de proyecto?** No desde la aplicación; crea una cuenta nueva eligiendo el otro
proyecto.
