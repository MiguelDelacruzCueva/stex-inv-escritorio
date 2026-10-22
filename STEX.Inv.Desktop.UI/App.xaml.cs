namespace STEX.Inv.Desktop.UI;

using System;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Refit;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.ViewModels;
using STEX.Inv.Desktop.Infrastructure.Api;
using STEX.Inv.Desktop.Infrastructure.Http;
using STEX.Inv.Desktop.Infrastructure.Security;
using STEX.Inv.Desktop.Infrastructure.Services;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 1. Configuración Base
                string apiBaseUrl = "http://localhost:3000";

                // 2. Seguridad y Handlers
                services.AddSingleton<ISecureStorageService, DpapiStorageService>();
                services.AddTransient<JwtAuthenticationHandler>();

                // 3. Políticas de Resiliencia (Polly) - Reintentos exponenciales si falla la red
                var retryPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

                // 4. Clientes API (Refit)
                services.AddRefitClient<IAuthApi>()
                    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
                    .AddPolicyHandler(retryPolicy);

                // 5. Servicios de Aplicación
                services.AddSingleton<IAuthService, AuthService>();

                // 6. ViewModels y Navegación
                services.AddSingleton<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<DashboardViewModel>();

                // El MainViewModel actúa como nuestro motor de navegación
                services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<MainViewModel>());

                // 7. Ventanas
                services.AddSingleton<MainWindow>(sp => new MainWindow
                {
                    DataContext = sp.GetRequiredService<MainViewModel>()
                });
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Al iniciar, le decimos al enrutador que cargue la pantalla de Login
        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        mainViewModel.NavigateTo<LoginViewModel>();

        // Mostramos la ventana principal
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}