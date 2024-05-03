using System;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Portalum.Zvt.EasyPay.Models;

namespace Portalum.Zvt.EasyPay.Services;

public class ResultService
{
    private readonly ILogger<ResultService> _logger;
    private readonly RegistryKey _zvtReg;
    private readonly OutputModel _output;
    
    public ResultService(ILogger<ResultService> logger)
    {
        _output = new OutputModel();
        _logger = logger;
        _zvtReg = Registry.CurrentUser.OpenSubKey("Software", true)?
            .CreateSubKey("GUB", true)
            .CreateSubKey("ZVT", true) ?? throw new InvalidOperationException();
    }

    /// <summary>
    /// Set active to 1
    /// </summary>
    public void SetActive()
    {
        _zvtReg.SetValue("Aktiv", 1);
    }

    public void SetResult(CommandResponseState resultType, string errorMessage)
    {
        switch (resultType)
        {
            case CommandResponseState.Successful:
                SetResult(TransactionResultType.Success, errorMessage);
                break;
            case CommandResponseState.Abort:
                SetResult(TransactionResultType.ClosedByUser, errorMessage);
                break;
            case CommandResponseState.NotSupported:
            case CommandResponseState.Unknown:
            case CommandResponseState.Error:
                SetResult(TransactionResultType.PaymentNotSuccessful, errorMessage);
                break;
            case CommandResponseState.Timeout:
                SetResult(TransactionResultType.CannotConnect, errorMessage);
                break;
        }
    }

    private void SetResult(TransactionResultType resultType, string errorMessage)
    {
        _output.Ergebnis = (int) resultType * -1;
        _output.ErgebnisText = resultType.ToString();
        _output.ErgebnisLang = errorMessage;
    }

    public void SetPaymentStatus(int receiptId, decimal amount)
    {
        _output.BelegNr = receiptId;
        _output.Betrag = (int) (amount * 100);
    }

    /// <summary>
    /// Publishes transaction results
    /// </summary>
    public void PublishResult()
    {
        var propInfos = typeof(OutputModel).GetProperties();
        foreach (var propInfo in propInfos)
        {
            _zvtReg.SetValue(propInfo.Name, propInfo.GetValue(_output) ?? (propInfo.PropertyType == typeof(string) ? string.Empty : 0));
        }
    }

    public void Dispose()
    {
        _zvtReg.SetValue("Aktiv", 0);
        _zvtReg.Dispose();
    }
}