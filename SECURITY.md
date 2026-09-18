# Seguridad y publicación

No subas archivos `appsettings.Development.json`, `appsettings.Production.json`, `.env`, certificados ni exportaciones de base de datos. Antes de publicar, revisa `git status --ignored` y confirma que solo los archivos fuente esperados aparecen en `git status`.

Las cadenas de conexión, la cuenta administradora inicial y cualquier clave de servicio se configuran exclusivamente en Azure App Service o Key Vault. GitHub Actions usa OpenID Connect; no requiere guardar una contraseña de Azure ni un publish profile en el repositorio.

Si alguna credencial llegó a estar en un commit, revócala y rota su valor antes de publicar. Eliminar el archivo de un commit posterior no elimina su contenido del historial.
