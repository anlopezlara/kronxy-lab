# KRONXY Repository Guidance

## Identidad del repositorio

KRONXY es una plataforma empresarial desarrollada con .NET 8 y ASP.NET Core Web API. Usa PostgreSQL, Clean Architecture, DDD, CQRS mediante MediatR, EF Core para escrituras, Dapper para consultas y FluentValidation.

## Estructura

```text
Kronxy.Api
├── Kronxy.Application
│   └── Kronxy.Domain
└── Kronxy.Infrastructure
    └── Kronxy.Application
        └── Kronxy.Domain
```

- `Kronxy.Domain`: modelo y reglas de dominio.
- `Kronxy.Application`: casos de uso y contratos de aplicación.
- `Kronxy.Infrastructure`: persistencia e integraciones.
- `Kronxy.Api`: composition root y exposición HTTP.

## Reglas arquitectónicas

- Domain no depende de Application, Infrastructure ni API.
- Application depende únicamente de Domain y las abstracciones necesarias.
- Infrastructure implementa persistencia, repositorios e integraciones.
- API es el composition root y la capa de exposición.
- No colocar lógica de negocio en controladores.
- Mantener Commands, Queries, Handlers y Validators en Application.
- Mantener entidades, value objects, errores y eventos en Domain.
- Mantener EF Core, Dapper, repositorios y servicios externos en Infrastructure.
- Respetar la arquitectura existente antes de introducir patrones nuevos.

## Convenciones de trabajo

- Inspeccionar primero los archivos relacionados.
- Limitar los cambios al objetivo solicitado y no modificar archivos no relacionados.
- Preservar los cambios existentes del usuario.
- No ejecutar operaciones Git destructivas.
- No crear commits ni hacer push salvo instrucción explícita.
- No agregar dependencias NuGet sin justificación y autorización.
- No editar archivos generados en `bin/obj`.
- Usar rutas relativas al repositorio en reportes.
- Para cambios complejos, presentar o mantener un plan antes de implementar.

## Seguridad

- No mostrar ni registrar secretos.
- No copiar connection strings, tokens, PAT, contraseñas ni claves.
- Usar `***REDACTED***` en reportes.
- No modificar User Secrets.
- No incluir secretos en pruebas, documentación ni paquetes de contexto.
- Revisar configuraciones sensibles antes de compartir resultados.

## Validación

Comandos normales de referencia:

```powershell
dotnet restore Kronxy.sln
dotnet build Kronxy.sln --no-restore
dotnet test Kronxy.sln --no-build
```

- Ejecutar únicamente las validaciones relevantes al cambio.
- Si no existen proyectos de prueba aplicables, indicarlo.
- Reportar cada comando, su código de salida y su resultado.
- No presentar una validación como exitosa si el comando falló.
- Ejecutar `git diff --check` antes de cerrar cambios de código.

## Criterio de terminado

Un cambio está terminado cuando cumple el objetivo solicitado, respeta la arquitectura, no expone secretos, ejecuta las validaciones aplicables, reporta resultados y limitaciones, revisa el diff y no modifica archivos fuera del alcance.
