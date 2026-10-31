# Seguridad y publicación

No subas archivos `appsettings.Development.json`, `appsettings.Production.json`, `.env`, certificados ni exportaciones de base de datos. Antes de publicar, revisa `git status --ignored` y confirma que solo los archivos fuente esperados aparecen en `git status`.

Las cadenas de conexión y cualquier clave de servicio se configuran fuera del repositorio, en la plataforma de despliegue; el despliegue desde GitHub no guarda contraseñas en el repositorio.

Si alguna credencial llegó a estar en un commit, revócala y rota su valor antes de publicar. Eliminar el archivo de un commit posterior no elimina su contenido del historial.
