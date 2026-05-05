using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows;
using PassengerManager.Client.Core.Services;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Client.Driver.ViewModels;
using PassengerManager.Shared.Protos;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Services.Translators;
using PassengerManager.Client.Driver.Services;
using System.Globalization;

namespace PassengerManager.Client.Driver
{
    public partial class App : Application
    {
        private readonly IHost _host;
        private HeartbeatBackgroundService? _heartbeatService;

        public App()
        {
            // SET UP TO TEST IN UKRAINIAN (UK)
            CultureInfo culture = new System.Globalization.CultureInfo("uk");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            //

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    string baseUrl = context.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5142";

                    services.AddGrpcClient<AuthService.AuthServiceClient>(options =>
                    {
                        options.Address = new Uri(baseUrl);
                    });

                    services.AddGrpcClient<TelemetryService.TelemetryServiceClient>(options =>
                    {
                        options.Address = new Uri(baseUrl);
                    })
                    .AddCallCredentials((context, metadata, serviceProvider) =>
                    {
                        // Use hardware token for telemetry (always available)
                        // Fall back to driver token if only driver is logged in
                        HardwareAccountStore hardwareStore = serviceProvider.GetRequiredService<HardwareAccountStore>();
                        DriverAccountStore driverAccountStore = serviceProvider.GetRequiredService<DriverAccountStore>();

                        string token = !string.IsNullOrEmpty(hardwareStore.Token) 
                            ? hardwareStore.Token 
                            : driverAccountStore.Token;

                        if (!string.IsNullOrEmpty(token))
                        {
                            metadata.Add("Authorization", $"Bearer {token}");
                        }

                        return Task.CompletedTask;
                    });

                    services.AddSingleton<IAuthService, GrpcAuthService>();
                    services.AddSingleton<IAuthErrorTranslator, AuthErrorTranslator>();
                    services.AddSingleton<ITelemetryService, GrpcTelemetryService>();

                    services.AddSingleton<INavigationService, AppNavigationService>();
                    services.AddSingleton<NavigationStore>();

                    services.AddSingleton<DriverAccountStore>();
                    services.AddSingleton<AccountStore>(provider => provider.GetRequiredService<DriverAccountStore>());
                    services.AddSingleton<HardwareAccountStore>();

                    services.AddSingleton<HeartbeatBackgroundService>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<DriverLoginViewModel>();
                    //services.AddSingleton<DriverDashboardViewModel>();

                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {          
            await _host.StartAsync();

            // Perform hardware login first to get the device token for telemetry
            await PerformHardwareLoginAsync();

            INavigationService navigationService = _host.Services.GetRequiredService<INavigationService>();
            navigationService.NavigateTo<DriverLoginViewModel>();

            MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _host.Services.GetRequiredService<MainViewModel>();

            // Start the heartbeat background service
            _heartbeatService = _host.Services.GetRequiredService<HeartbeatBackgroundService>();
            _heartbeatService.Start();

            mainWindow.Show();
            base.OnStartup(e);
        }

        /// <summary>
        /// Performs hardware authentication on app startup.
        /// This provides a token for continuous telemetry regardless of driver login status.
        /// </summary>
        private async Task PerformHardwareLoginAsync()
        {
            try
            {
                var configuration = _host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                var authService = _host.Services.GetRequiredService<IAuthService>();
                var hardwareStore = _host.Services.GetRequiredService<HardwareAccountStore>();

                string vehicleId = configuration["TerminalSettings:VehicleId"] ?? "UNKNOWN";
                string hardwareHash = configuration["TerminalSettings:HardwareHash"] ?? "UNSET";

                var request = new HardwareLoginRequest
                {
                    VehicleId = vehicleId,
                    HardwareHash = hardwareHash
                };

                var response = await authService.AuthenticateHardwareAsync(request);

                if (response.Success)
                {
                    hardwareStore.Login(response, vehicleId);
                    System.Diagnostics.Debug.WriteLine($"Hardware login successful for vehicle {vehicleId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Hardware login failed: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during hardware login: {ex.Message}");
                // Continue even if hardware login fails - telemetry will use driver token if available
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // Stop the heartbeat background service gracefully
            if (_heartbeatService != null)
            {
                await _heartbeatService.StopAsync();
                _heartbeatService.Dispose();
            }

            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}
