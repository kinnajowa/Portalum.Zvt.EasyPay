namespace Portalum.Zvt.EasyPay.Models
{
    public class PaymentTerminalConfig
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int Password { get; set; }
        public TransactionType TransactionType { get; set; }
        public decimal Amount { get; set; }
        public int ReceiptNumber { get; set; }
        public ConfigType ConfigType { get; set; }
        
    }
}
