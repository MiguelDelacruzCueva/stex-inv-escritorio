# Progreso: Actualizar Refit.HttpClientFactory

## Cambios realizados

- Actualizado `STEX.Inv.Desktop.Infrastructure/STEX.Inv.Desktop.Infrastructure.csproj`:
  - `Refit.HttpClientFactory`: `7.0.0` → `16.4.0`.
  - `Refit`: se mantuvo en `16.4.0`.
- No fue necesario modificar `STEX.Inv.Desktop.UI/App.xaml.cs`: `AddRefitClient<IAuthApi>()`, `BaseAddress` y `AddPolicyHandler` compilaron con la versión alineada.

## Validación

- Build dirigido de `STEX.Inv.Desktop.Infrastructure`: correcto, sin errores ni advertencias reportadas.
- Build de la solución `STEX.Inv.Desktop.slnx`: correcto, sin errores ni advertencias reportadas.
- Verificación del proyecto: ambas referencias Refit están en `16.4.0`.
- Descubrimiento de proyectos de pruebas: no se encontraron proyectos de pruebas disponibles para ejecutar.

## Incidencias

- La evaluación de API no detectó cambios públicos incompatibles entre `Refit.HttpClientFactory 7.0.0` y `16.4.0`; no aparecieron errores de compilación que requirieran adaptar el código de `IHttpClientFactory`.
