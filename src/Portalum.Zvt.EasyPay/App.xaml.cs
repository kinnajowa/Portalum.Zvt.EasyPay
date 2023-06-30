using System;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Portalum.Zvt.EasyPay.Models;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;

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

            Parser.Default.ParseArguments<CommandLineOptions>(e.Args)
                .WithParsed(this.RunOptions)
                .WithNotParsed(this.HandleParseError);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            RegistryKey regkey = Registry.CurrentUser.OpenSubKey("Software", true)?
                .CreateSubKey("GUB", true)
                .CreateSubKey("ZVT", true) ?? throw new InvalidOperationException();
            
            regkey.SetValue("Aktiv", 0);
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
            this._logger.LogInformation($"{nameof(RunOptions)} - Startup successful, start payment process wiht an amount of {configuration.Amount}");

            var window = new MainWindow(this._loggerFactory, configuration);
            window.Show();
        }

        private void HandleParseError(IEnumerable<Error> errors)
        {
            Current.Shutdown(-2);
        }
    }
}
