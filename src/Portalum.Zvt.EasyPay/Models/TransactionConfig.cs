namespace Portalum.Zvt.EasyPay.Models
{
    public class TransactionConfig
    {
        public string IP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 22000;
        public string Passwort { get; set; } = "000000";
        public int KasseNr { get; set; } = 0;
        public TransactionType Funktion { get; set; } = TransactionType.Payment;
        public decimal Betrag { get; set; }
        public PrintType Kassedruck { get; set; }
        public int StornoBelegNr { get; set; }
        public decimal StornoBetrag { get; set; }
        public int TrinkgeldBelegNr { get; set; }
        public ConfigType ConfigType { get; set; }
    }
}
