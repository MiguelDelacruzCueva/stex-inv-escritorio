# 01-update-refit-httpclientfactory: Actualizar Refit.HttpClientFactory y validar HttpClientFactory

## Objective

Actualizar `Refit.HttpClientFactory` de `7.0.0` a `16.4.0` en `STEX.Inv.Desktop.Infrastructure.csproj`, manteniendo `Refit 16.4.0`. Revisar la configuración `AddRefitClient<IAuthApi>()` en `STEX.Inv.Desktop.UI/App.xaml.cs` y adaptar únicamente lo necesario para que el registro, la URL base, el handler de autenticación y la política Polly sigan funcionando con la API actual.

## Research findings

- El proyecto afectado es SDK-style y apunta a `net10.0`.
- `get_project_dependencies` confirmó que no se usa Central Package Management; las versiones están definidas directamente en `STEX.Inv.Desktop.Infrastructure/STEX.Inv.Desktop.Infrastructure.csproj`.
- Dependencias relevantes: `Refit 16.4.0`, `Refit.HttpClientFactory 7.0.0`, `Microsoft.Extensions.Http.Polly 8.0.0` y referencia a `STEX.Inv.Desktop.Core`.
- La evaluación resolvió `Refit.HttpClientFactory 16.4.0` como versión estable unificada.
- `apidiff/Refit.HttpClientFactory.apidiff.md` indica que no hay cambios públicos incompatibles entre `7.0.0` y `16.4.0`.
- `STEX.Inv.Desktop.UI/App.xaml.cs` registra `AddRefitClient<IAuthApi>()`, configura `BaseAddress` y aplica `AddPolicyHandler(retryPolicy)`; no se identificó una migración de API necesaria antes del build.

## Scope

- Proyecto modificado: `STEX.Inv.Desktop.Infrastructure`.
- Punto de integración a validar: `STEX.Inv.Desktop.UI/App.xaml.cs`.
- Cambio de paquete y validación de compilación de la solución.

**Done when**: El paquete queda en `16.4.0`, el registro de `IAuthApi` compila con la versión actual, la solución se restaura y compila sin errores ni advertencias nuevas, y las pruebas disponibles pasan.
