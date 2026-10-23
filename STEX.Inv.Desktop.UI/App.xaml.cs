namespace STEX.Inv.Desktop.UI;

using System;
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
using STEX.Inv.Desktop.Infrastructure.Storage;
using STEX.Inv.Desktop.UI.Services;

public partial class App : Application
{
    private const string DefaultApiBaseUrl = "http://localhost:3000";

    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                string apiBaseUrl = context.Configuration["Api:BaseUrl"] ?? DefaultApiBaseUrl;

                // Seguridad y almacenamiento
                services.AddSingleton<ISecureStorageService, DpapiStorageService>();
                services.AddSingleton<ILocalStore, EncryptedJsonLocalStore>();
                services.AddSingleton<IPinHasher, PinHasher>();
                services.AddSingleton<IDeviceSessionService, DeviceSessionService>();
                services.AddSingleton<IOperatorSession, OperatorSession>();
                services.AddSingleton<IDialogService, MessageDialogService>();
                services.AddTransient<JwtAuthenticationHandler>();

                // Resiliencia
                var retryPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

                // Clientes API
                services.AddRefitClient<IAuthApi>()
                    .ConfigureHttpClient(client =>
                    {
                        client.BaseAddress = new Uri(apiBaseUrl);
                        client.Timeout = TimeSpan.FromSeconds(30);
                    })
                    .AddHttpMessageHandler<JwtAuthenticationHandler>()
                    .AddPolicyHandler(retryPolicy);

                services.AddRefitClient<ISyncApi>()
                    .ConfigureHttpClient(client =>
                    {
                        client.BaseAddress = new Uri(apiBaseUrl);
                        client.Timeout = TimeSpan.FromSeconds(60);
                    })
                    .AddHttpMessageHandler<JwtAuthenticationHandler>()
                    .AddPolicyHandler(retryPolicy);

                // Servicios de aplicación
                services.AddSingleton<IAuthService, AuthService>();
                services.AddSingleton<IWarehouseSyncService, WarehouseSyncService>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<EnrollmentViewModel>();
                services.AddTransient<OperatorSelectionViewModel>();
                services.AddTransient<PinEntryViewModel>();
                services.AddTransient<ShellViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<PlaceholderViewModel>();

                services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<MainViewModel>());

                // Ventana
                services.AddSingleton<MainWindow>(sp => new MainWindow
                {
                    DataContext = sp.GetRequiredService<MainViewModel>()
                });
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await _host.StartAsync();

            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            var localStore = _host.Services.GetRequiredService<ILocalStore>();

            var state = await localStore.LoadAsync();

            if (state.Device is not null)
            {
                await mainViewModel.NavigateToAsync<OperatorSelectionViewModel>();
            }
            else
            {
                await mainViewModel.NavigateToAsync<EnrollmentViewModel>();
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo iniciar la aplicación:{Environment.NewLine}{ex.Message}",
                "STEX-INV",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
