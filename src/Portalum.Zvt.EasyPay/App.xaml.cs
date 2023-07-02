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
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        
        private const string _pkey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEkS3M9UNxjJ9CjGWs80dtqRbQAJBM2y3SRJnFmSnZ4mjGFA4B+9tCwNact4f+V1MBCLHsTaqKJKk5KCKa9fk5AA==";


        public App()
        {
            this._loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddFile("default.log", LogLevel.Debug, outputTemplate: "{Timestamp:HH:mm:ss.fff} {Level:u3} {SourceContext} {Message:lj}{NewLine}{Exception}").SetMinimumLevel(LogLevel.Debug);
            });

            this._logger = this._loggerFactory.CreateLogger<App>();
            this._logger.LogInformation($"{nameof(App)} - Start");
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
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
                    Parser.Default.ParseArguments<CommandLineOptions>(e.Args)
                        .WithParsed(this.RunOptions)
                        .WithNotParsed(this.HandleParseError);
                }
                else
                {
                    this._logger.LogError($"{nameof(Application_Startup)} - License not valid. please refer to your vendor.");
                    var window = new LicenseWindow(this._loggerFactory);
                    window.Show();
                }
            }
            else
            {
                var window = new LicenseWindow(this._loggerFactory);
                window.Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            RegistryKey regkey = Registry.CurrentUser.OpenSubKey("Software", true)?
                .CreateSubKey("GUB", true)
                .CreateSubKey("ZVT", true) ?? throw new InvalidOperationException();
            
            regkey.SetValue("Aktiv", 0);
            regkey.SetValue("Ergebnis", e.ApplicationExitCode * -1);
            regkey.Dispose();
            
            this._loggerFactory.Dispose();
            this._logger.LogInformation($"{nameof(Application_Exit)} - Exit");
        }

        private PaymentTerminalConfig GetConfiguration(CommandLineOptions options)
        {
            PaymentTerminalConfig terminalConfig = new PaymentTerminalConfig();
            var isRegistryInput = true;
            RegistryKey? regkey = null;
            try
            {
                regkey = Registry.CurrentUser.OpenSubKey("Software", true)?
                    .CreateSubKey("GUB", true)
                    .CreateSubKey("ZVT", true) ?? throw new InvalidOperationException();
                
                regkey.SetValue("Aktiv", 1);
                regkey.SetValue("START", Path.Combine(AppContext.BaseDirectory, System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "Portalum.Zvt.EasyPay.exe"));
            }
            catch (Exception)
            {
                regkey?.Dispose();
                _logger.LogError("Unable to read from registry. Try config file instead.");
                isRegistryInput = false;
            }


            if (isRegistryInput)
            {
                terminalConfig.IpAddress = (string) (regkey?.GetValue("IP") ?? string.Empty);
                terminalConfig.Port = (int) (regkey?.GetValue("Port") ?? 22000);
                terminalConfig.Password = (int) (regkey?.GetValue("Passwort") ?? 0);
                terminalConfig.Amount = ParseEurocentToDecimal((int) (regkey?.GetValue("Betrag") ?? 0));
                terminalConfig.TransactionType = (TransactionType) (regkey?.GetValue("Funktion") ?? TransactionType.Payment);
                if (terminalConfig.TransactionType == TransactionType.Reversal)
                    terminalConfig.ReceiptNumber = (int) (regkey?.GetValue("StornoBelegNr") ?? 0);
                terminalConfig.ConfigType = ConfigType.Registry;
            }
            else
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(this._configurationFile, optional: false);

                IConfigurationRoot configuration = builder.Build();

                if (!int.TryParse(configuration["Port"], out var port))
                {
                    this._logger.LogError($"{nameof(GetConfiguration)} - Cannot parse port from configuration file");
                    Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(-2); });
                }
                
                if (!int.TryParse(configuration["Password"], out var password))
                {
                    this._logger.LogError($"{nameof(GetConfiguration)} - Cannot parse password from configuration file");
                    Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(-2); });
                }

                terminalConfig.IpAddress = configuration["IpAddress"];
                terminalConfig.Port = port;
                terminalConfig.Password = password;
                terminalConfig.Amount = options.Amount;
                terminalConfig.TransactionType = TransactionType.Payment;
                terminalConfig.ConfigType = ConfigType.ConfigFile;
            }

            return terminalConfig;
        }

        private decimal ParseEurocentToDecimal(int regAmount)
        {
            if (regAmount == 0)
            {
                _logger.LogError($"{nameof(ParseEurocentToDecimal)} - Payment amount cannot be 0.");
                Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(-2); });
            }

            var amount = ((decimal) regAmount) / 100;
            return amount;
        }

        private void RunOptions(CommandLineOptions options)
        {
            var configuration = this.GetConfiguration(options);
            this._logger.LogInformation($"{nameof(RunOptions)} - Startup successful, start payment process with an amount of {configuration.Amount}");

            var window = new MainWindow(this._loggerFactory, configuration);
            window.Show();
        }

        private void HandleParseError(IEnumerable<Error> errors)
        {
            Current.Shutdown(-2);
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
