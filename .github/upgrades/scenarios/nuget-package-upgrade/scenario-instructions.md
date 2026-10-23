# Actualización de NuGet: Refit.HttpClientFactory

## Strategy
Actualizar solo `Refit.HttpClientFactory` a `16.4.0`, mantener `Refit 16.4.0` y validar la integración actual de `IHttpClientFactory`.

## Preferences
- **Flow Mode**: Automatic
- **Scope**: `STEX.Inv.Desktop.Infrastructure`
- **Packages**: Mantener `Refit` en `16.4.0`; actualizar `Refit.HttpClientFactory` a la última versión estable compatible.
- **Compatibility rule**: No volver a `Refit 7`; adaptar la configuración y el uso de `IHttpClientFactory` a la versión actual.
- **Resolved version**: `Refit.HttpClientFactory 16.4.0` (unified; assessment quick mode)
- **Package management**: Per-project `PackageReference`; no `Directory.Packages.props` detected.
- **Breaking changes**: Ninguna API pública incompatible detectada; confirmar mediante build.

## Decisions
- Mantener `Refit 16.4.0` y alinear `Refit.HttpClientFactory` en `16.4.0` para evitar la combinación incompatible `16.4.0` + `7.0.0`.

## Source Control
- **Source Branch**: `master`
- **Working Branch**: `master` (branch context supplied by the workspace)
- **Commit Strategy**: After Each Task
- **Branch Sync**: Disabled
