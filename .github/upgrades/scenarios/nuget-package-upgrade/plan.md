# Actualización de NuGet: Refit.HttpClientFactory Plan

## Overview

**Target**: Alinear `Refit.HttpClientFactory` con `Refit 16.4.0` y verificar la integración de `IHttpClientFactory` en la aplicación WPF.
**Scope**: 1 proyecto de infraestructura y su punto de registro de servicios en la UI; cambio pequeño sin rupturas de API detectadas.

## Tasks

### 01-update-refit-httpclientfactory: Actualizar Refit.HttpClientFactory y validar HttpClientFactory

Actualizar `Refit.HttpClientFactory` de `7.0.0` a `16.4.0` en `STEX.Inv.Desktop.Infrastructure.csproj`, manteniendo `Refit 16.4.0`. Revisar la configuración `AddRefitClient<IAuthApi>()` en `STEX.Inv.Desktop.UI/App.xaml.cs` y adaptar únicamente lo necesario para que el registro, la URL base, el handler de autenticación y la política Polly sigan funcionando con la API actual. La referencia de API `apidiff/Refit.HttpClientFactory.apidiff.md` no detecta cambios públicos incompatibles.

**Done when**: El paquete queda en `16.4.0`, el registro de `IAuthApi` compila con la versión actual, la solución se restaura y compila sin errores ni advertencias nuevas, y las pruebas disponibles pasan.
