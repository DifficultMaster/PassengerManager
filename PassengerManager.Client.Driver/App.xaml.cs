using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using PassengerManager.Client.Core.Services;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Client.Driver.ViewModels;
using PassengerManager.Client.Driver.Stores;
using PassengerManager.Shared.Protos;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Services.Translators;
using PassengerManager.Client.Driver.Services;
using System.Globalization;
using PassengerManager.Client.Driver.Services.Location;
using PassengerManager.Client.Driver.ViewModels.Dashboard;
using PassengerManager.Client.Driver.ViewModels.Overlay;

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

                    services.AddGrpcClient<CommunicationService.CommunicationServiceClient>(options =>
                    {
                        options.Address = new Uri(baseUrl);
                    });

                    services.AddGrpcClient<DriverOpsService.DriverOpsServiceClient>(options =>
                        {
                            options.Address = new Uri(baseUrl);
                        })
                        .AddCallCredentials((context, metadata, serviceProvider) =>
                        {
                            DriverAccountStore driverAccountStore = serviceProvider.GetRequiredService<DriverAccountStore>();

                            if (!string.IsNullOrEmpty(driverAccountStore.Token))
                            {
                                metadata.Add("Authorization", $"Bearer {driverAccountStore.Token}");
                            }

                            return Task.CompletedTask;
                        });

                    services.AddSingleton<IAuthService, GrpcAuthService>();
                    services.AddSingleton<IAuthErrorTranslator, AuthErrorTranslator>();
                    services.AddSingleton<ICommunicationService, GrpcCommunicationService>();
                    services.AddSingleton<ITelemetryService, GrpcTelemetryService>();
                    #if DEBUG
                    services.AddSingleton<ILocationProvider, SmartDebugLocationProvider>();
                    #else
                    services.AddSingleton<ILocationProvider, NativeLocationProvider>();
                    #endif

                    services.AddSingleton<INavigationService, AppNavigationService>();
                    services.AddSingleton<NavigationStore>();

                    services.AddSingleton<DriverAccountStore>();
                    services.AddSingleton<AccountStore>(provider => provider.GetRequiredService<DriverAccountStore>());
                    services.AddSingleton<HardwareAccountStore>();
                    services.AddSingleton<ManifestStore>();
                    services.AddSingleton<SideBarStore>();
                    services.AddSingleton<StatusBarStore>();

                    services.AddSingleton<HeartbeatBackgroundService>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<DriverLoginViewModel>();
                    //services.AddSingleton<DriverDashboardViewModel>();

                    services.AddSingleton<NavigationMapViewModel>();
                    services.AddSingleton<ReportIncidentViewModel>();
                    services.AddSingleton<RouteSelectionViewModel>();
                    services.AddSingleton<TripSelectionViewModel>();
                    services.AddSingleton<SettingsViewModel>();

                    services.AddSingleton<SideBarViewModel>();
                    services.AddSingleton<StatusBarViewModel>();

                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            await _host.StartAsync();

            // Perform hardware login first to get the device token for telemetry
            await PerformHardwareLoginAsync();

            MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
            this.MainWindow = mainWindow;
            mainWindow.DataContext = _host.Services.GetRequiredService<MainViewModel>();

            INavigationService navigationService = _host.Services.GetRequiredService<INavigationService>();
            navigationService.NavigateTo<DriverLoginViewModel>();

            StatusBarStore statusBarStore = _host.Services.GetRequiredService<StatusBarStore>();
            SideBarStore sideBarStore = _host.Services.GetRequiredService<SideBarStore>();

            UpdateConnectionLevel(statusBarStore);
            statusBarStore.IsMicrophoneOn = false;
            statusBarStore.IsTrackerOn = false;

            sideBarStore.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SideBarStore.CallStatus))
                {
                    statusBarStore.IsMicrophoneOn = sideBarStore.CallStatus is CallStatus.Live or CallStatus.Outgoing;
                }
            };

            NetworkChange.NetworkAvailabilityChanged += (_, __) => UpdateConnectionLevel(statusBarStore);
            NetworkChange.NetworkAddressChanged += (_, __) => UpdateConnectionLevel(statusBarStore);

            // Start the heartbeat background service
            _heartbeatService = _host.Services.GetRequiredService<HeartbeatBackgroundService>();
            _heartbeatService.TrackingAvailabilityChanged += isTrackingOn => statusBarStore.IsTrackerOn = isTrackingOn;
            _heartbeatService.Start();

            ApplyDebugWindowSettings(mainWindow);
            mainWindow.Show();

            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            base.OnStartup(e);
        }

        private static void UpdateConnectionLevel(StatusBarStore statusBarStore)
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            bool hasUpInterface = interfaces.Any(interfaceItem =>
                interfaceItem.OperationalStatus == OperationalStatus.Up &&
                interfaceItem.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                interfaceItem.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            if (!hasUpInterface)
            {
                statusBarStore.ConnectionLevel = ConnectionLevel.None;
                return;
            }

            bool hasWireless = interfaces.Any(interfaceItem =>
                interfaceItem.OperationalStatus == OperationalStatus.Up &&
                interfaceItem.NetworkInterfaceType is NetworkInterfaceType.Wireless80211);

            statusBarStore.ConnectionLevel = hasWireless ? ConnectionLevel.High : ConnectionLevel.Medium;
        }

        private void ApplyDebugWindowSettings(Window window)
        {
#if !DEBUG
            return;
#else
            IConfiguration configuration = _host.Services.GetRequiredService<IConfiguration>();
            IConfigurationSection section = configuration.GetSection("DebugWindow");

            if (!section.GetValue("Enabled", false))
            {
                return;
            }

            double width = section.GetValue("Width", 1366.0);
            double height = section.GetValue("Height", 768.0);
            string? preset = section.GetValue<string>("Preset");

            if (!string.IsNullOrWhiteSpace(preset))
            {
                IConfigurationSection presetSection = section.GetSection($"Presets:{preset}");
                if (presetSection.Exists())
                {
                    width = presetSection.GetValue("Width", width);
                    height = presetSection.GetValue("Height", height);
                }
            }

            bool center = section.GetValue("Center", true);
            bool resizable = section.GetValue("Resizable", true);
            bool showWindowChrome = section.GetValue("ShowWindowChrome", true);

            window.WindowState = WindowState.Normal;
            window.Width = width;
            window.Height = height;
            window.ResizeMode = resizable ? ResizeMode.CanResize : ResizeMode.NoResize;
            window.WindowStyle = showWindowChrome ? WindowStyle.SingleBorderWindow : WindowStyle.None;
            window.WindowStartupLocation = center ? WindowStartupLocation.CenterScreen : WindowStartupLocation.Manual;
#endif
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
