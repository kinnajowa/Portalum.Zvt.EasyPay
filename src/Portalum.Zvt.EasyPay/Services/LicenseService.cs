using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Standard.Licensing;
using Standard.Licensing.Validation;

namespace Portalum.Zvt.EasyPay.Services;

public class LicenseService
{
    private readonly ILogger<LicenseService> _logger;
    private readonly string _appGuid;
    private readonly string _licensePath;
    
    private const string PubKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEkS3M9UNxjJ9CjGWs80dtqRbQAJBM2y3SRJnFmSnZ4mjGFA4B+9tCwNact4f+V1MBCLHsTaqKJKk5KCKa9fk5AA==";

    public LicenseService(ILogger<LicenseService> logger)
    {
        _logger = logger;
        _appGuid = Assembly.GetExecutingAssembly().GetCustomAttribute<GuidAttribute>()!.Value.ToLower();
        _licensePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EasyPay", "license.lic");
    }
    
    public bool LicensePresent()
    {
        return File.Exists(_licensePath);
    }

    public bool LicenseValid()
    {
        if (!LicensePresent()) return false;

        var license = LoadLicense();
        var failures = new List<IValidationFailure>();
        
        try
        {
            var validationFailures = license.Validate()
                .ExpirationDate()
                .When(lic => lic.AdditionalAttributes.Contains("AppID"))
                .And()
                .Signature(PubKey)
                .And()
                .AssertThat(lic =>
                        lic.AdditionalAttributes.Get("AppID").ToLower().Equals(_appGuid),
                    new GeneralValidationFailure()
                    {
                        Message = "The provided license is not valid for this product.",
                        HowToResolve = "Please contact your vendor to obtain a valid license for this product."
                    })
                .AssertValidLicense();
            
            failures.AddRange(validationFailures);
        }
        catch (Exception)
        {
            return false;
        }
        
        if (failures.Any())
        {
            _logger.LogError($"LicenseValidation - License check failed:");
            foreach (var f in failures)
            {
                _logger.LogInformation($"{f.Message} | {f.HowToResolve}");
            }
            return false;
        }
        return true;
    }

    public void SubmitLicense(string newLicensePath)
    {
        try
        {
            File.Copy(newLicensePath, _licensePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{nameof(LicenseWindow)} - Can't copy license file: {ex.Message}");
            throw;
        }
        _logger.LogInformation($"{nameof(LicenseWindow)} - License file copied. Restart Application");
    }

    private License LoadLicense()
    {
        return License.Load(File.ReadAllText(_licensePath));
    }
}