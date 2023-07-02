using System;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Portalum.Zvt.EasyPay.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Standard.Licensing;
using Standard.Licensing.Validation;

namespace Portalum.Zvt.EasyPay
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly string _configurationFile = "appsettings.json";
        private readonly ILogger _logger;
        private readonly ServiceProvider _serviceProvider;
        
        private const string _pkey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEkS3M9UNxjJ9CjGWs80dtqRbQAJBM2y3SRJnFmSnZ4mjGFA4B+9tCwNact4f+V1MBCLHsTaqKJKk5KCKa9fk5AA==";


        public App()
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            
            _logger = _serviceProvider.GetService<ILogger<App>>();
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
            _serviceProvider.GetService<ResultService>().SetActive();

            if (!File.Exists(this._configurationFile))
            {
                this._logger.LogError($"{nameof(Application_Startup)} - Configuration file not available, {this._configurationFile}");
                Current.Shutdown(-3);
                return;
            }

            //check license
            var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EasyPay");
            Directory.CreateDirectory(dataPath);
            var licensePath = Path.Combine(dataPath, "license.lic");

            if (File.Exists(licensePath))
            {
                if (CheckLicense(File.ReadAllText(licensePath)))
                {
                    _logger.LogInformation($"Startup successful, start transaction process");

                    var window = _serviceProvider.GetService<MainWindow>();
                    window.Show();
                }
                else
                {
                    this._logger.LogError($"{nameof(Application_Startup)} - License not valid. please refer to your vendor.");
                    var window = _serviceProvider.GetService<LicenseWindow>();
                    window.Show();
                }
            }
            else
            {
                var window = _serviceProvider.GetService<LicenseWindow>();
                window.Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var resultService = _serviceProvider.GetService<ResultService>();
            

            resultService.Dispose();
            
            this._logger.LogInformation($"{nameof(Application_Exit)} - Exit");
        }



        private bool CheckLicense(string licenseText)
        {
            var guid = Assembly.GetExecutingAssembly().GetCustomAttribute<GuidAttribute>().Value.ToLower();
            var license = License.Load(licenseText);
            var validationFailures = license.Validate()
                .ExpirationDate()
                .When(lic => lic.AdditionalAttributes.Contains("AppID"))
                .And()
                .Signature(_pkey)
                .And()
                .AssertThat(lic => 
                    lic.AdditionalAttributes.Get("AppID").ToLower().Equals(guid),
                    new GeneralValidationFailure()
                    {
                        Message = "The provided license is not valid for this product.",
                        HowToResolve = "Please contact your vendor to obtain a valid license for this product."
                    })
                .AssertValidLicense();

            var failures = validationFailures.ToList();
            if (failures.Any())
            {
                _logger.LogError($"{nameof(CheckLicense)} - License check failed:");
                foreach (var f in failures)
                {
                    _logger.LogInformation($"{f.Message} | {f.HowToResolve}");
                }
                return false;
            }
            return true;
        }
        
    }
}
