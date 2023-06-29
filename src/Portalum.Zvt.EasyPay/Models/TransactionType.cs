namespace Portalum.Zvt.EasyPay.Models;

public enum TransactionType
{
    Payment = 0,
    Diagnose = 1,
    EndOfDay = 2,
    ReversalLastPayment = 3,
    Reversal = 4,
    RepeatReceipt = 5
}