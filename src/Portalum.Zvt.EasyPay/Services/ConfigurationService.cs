using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Portalum.Zvt.EasyPay.Models;

namespace Portalum.Zvt.EasyPay;

public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly TransactionConfig _config;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Loading configuration");

        RegistryKey? regkey = null;
        try
        {
            regkey = Registry.CurrentUser.OpenSubKey("Software", true)?
                .CreateSubKey("GUB", true)
                .CreateSubKey("ZVT", true) ?? throw new InvalidOperationException();

            regkey.SetValue("Aktiv", 1);
            regkey.SetValue("START",
                Path.Combine(AppContext.BaseDirectory,
                    Process.GetCurrentProcess().MainModule?.FileName ?? "Portalum.Zvt.EasyPay.exe"));
        }
        catch (Exception)
        {
            regkey?.Dispose();
            _logger.LogError("Unable to read from registry.");
            Application.Current.Shutdown(-3);
        }

        //use reflection to load values from the registry 
        _config = new TransactionConfig();
        
        var propInfos = typeof(TransactionConfig).GetProperties();
        try {
            foreach (var propInfo in propInfos)
            {
                var type = propInfo.PropertyType;

                if (propInfo.Name.Equals("Betrag") | propInfo.Name.Equals("StornoBetrag"))
                {
                    propInfo.SetValue(_config, ParseEurocentToDecimal((int) (regkey?.GetValue(propInfo.Name) ?? 0)));
                }
                else if (type == typeof(int))
                {
                    propInfo.SetValue(_config, ((int?) regkey?.GetValue(propInfo.Name)) ?? propInfo.GetValue(_config));
                } else if (type == typeof(string))
                {
                    propInfo.SetValue(_config, ((string?) regkey?.GetValue(propInfo.Name)) ?? propInfo.GetValue(_config));
                } else if (type.IsEnum)
                {
                    propInfo.SetValue(_config, (int?) regkey?.GetValue(propInfo.Name) ?? propInfo.GetValue(_config));
                }
            }
        } catch (Exception)
        {
            Application.Current.Shutdown(-2);
        }

        _config.ConfigType = ConfigType.Registry;
    }

    public TransactionConfig GetConfiguration()
    {
        return _config;
    }
    
    private static decimal ParseEurocentToDecimal(int regAmount)
    {
        var amount = ((decimal) regAmount) / 100;
        return amount;
    }
}