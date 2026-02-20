using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows;
using PassengerManager.Client.Core.Services;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Shared.Protos;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Services.Translators;

namespace PassengerManager.Client.Driver
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    string baseUrl = context.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";

                    services.AddGrpcClient<AuthService.AuthServiceClient>(options =>
                    {
                        options.Address = new Uri(baseUrl);
                    });

                    services.AddSingleton<IAuthService, GrpcAuthService>();
                    services.AddSingleton<IAuthErrorTranslator, AuthErrorTranslator>();

                    services.AddSingleton<INavigationService, AppNavigationService>();
                    services.AddSingleton<NavigationStore>();

                    services.AddSingleton<DriverAccountStore>();
                    services.AddSingleton<AccountStore>(provider => provider.GetRequiredService<DriverAccountStore>());

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

            INavigationService navigationService = _host.Services.GetRequiredService<INavigationService>();
            navigationService.NavigateTo<DriverLoginViewModel>();

            MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _host.Services.GetRequiredService<MainViewModel>();

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
}
