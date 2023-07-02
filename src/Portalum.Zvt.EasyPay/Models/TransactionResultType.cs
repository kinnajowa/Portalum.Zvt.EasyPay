namespace Portalum.Zvt.EasyPay.Models;

public enum TransactionResultType
{
    Success = 0,
    PaymentNotSuccessful = -1,
    InvalidConfiguration = -2,
    ConfigurationNotFound = -3,
    CannotConnect = -4,
    ClosedByUser = -5,
    LicenseNotValid = -6
}