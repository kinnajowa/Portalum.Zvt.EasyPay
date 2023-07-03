using Microsoft.Extensions.Logging;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Portalum.Zvt.EasyPay.Services;

namespace Portalum.Zvt.EasyPay
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private readonly ILogger _logger;
        private readonly ServiceProvider _serviceProvider;
        private readonly LicenseService _licenseService;
        


        public App()
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            _licenseService = _serviceProvider.GetService<LicenseService>()!;
            _logger = _serviceProvider.GetService<ILogger<App>>()!;
            _logger.LogInformation($"{nameof(App)} - Start");
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton<MainWindow>();
            services.AddSingleton<LicenseWindow>();
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<ResultService>();
            services.AddSingleton<LicenseService>();
            
            services.AddLogging(loggerBuilder =>
            {
                loggerBuilder.ClearProviders();
                loggerBuilder.AddFile("default.log", LogLevel.Debug, outputTemplate: "{Timestamp:HH:mm:ss.fff} {Level:u3} {SourceContext} {Message:lj}{NewLine}{Exception}").SetMinimumLevel(LogLevel.Debug);
            });
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            _serviceProvider.GetService<ResultService>()!.SetActive();

            if (_licenseService.LicensePresent())
            {
                if (_licenseService.LicenseValid())
                {
                    _logger.LogInformation($"Startup successful, start transaction process");

                    var window = _serviceProvider.GetService<MainWindow>()!;
                    window.Show();
                }
                else
                {
                    this._logger.LogError($"{nameof(Application_Startup)} - License not valid. please refer to your vendor.");
                    var window = _serviceProvider.GetService<LicenseWindow>()!;
                    window.Show();
                }
            } 
            else
            {
                var window = _serviceProvider.GetService<LicenseWindow>()!;
                window.Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var resultService = _serviceProvider.GetService<ResultService>()!;
            resultService.PublishResult();
            resultService.Dispose();
            
            this._logger.LogInformation($"{nameof(Application_Exit)} - Exit");
        }
    }
}
