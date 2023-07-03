namespace Portalum.Zvt.EasyPay.Models;

public enum TransactionType
{
    Payment = 0,
    Diagnose = 1,
    EndOfDay = 2,
    ReversalLastPayment = 3,
    Credit = 4,
    RepeatReceipt = 5,
    RepeatReceiptVendor = 51,
    RepeatReceiptCustomer = 52,
    RepeatReceiptEndOfDay = 53,
    TaxFree = 6,
    CheckBalanceAvsCard = 7,
    Reservation = 8,
    BookReservation = 9,
    AbortReservation = 10,
    Tip = 11,
    SelectLanguage = 12,
    ReadCardMagnetic = 13,
    ReservationPartialAbort = 14,
    ReadCardChip = 15
}