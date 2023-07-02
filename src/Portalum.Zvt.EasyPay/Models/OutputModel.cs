using static System.String;

namespace Portalum.Zvt.EasyPay.Models;

public class OutputModel
{
    public int Ergebnis { get; set; }
    public string ErgebnisText { get; set; } = Empty;
    public string ErgebnisLang { get; set; } = Empty;
    public string Authorisierungsergebnis { get; set; } = Empty;
    public string PAN { get; set; } = Empty;
    public int Betrag { get; set; }
    public int Kartentyp { get; set; }
    public string KartentypLang { get; set; } = Empty;
    public string BLZ { get; set; } = Empty;
    public string Kontonummer { get; set; } = Empty;
    public string Drucktext { get; set; } = Empty;
    public string Drucktext2 { get; set; } = Empty;
    public string Haendlerbeleg { get; set; } = Empty;
    public int BelegNr { get; set; }
    public string Bezahlart { get; set; } = Empty;
    public string Authentifizierung { get; set; } = Empty;
    public string TID { get; set; } = Empty;
    public string Geraetetyp { get; set; } = Empty;
    public string Softwareversion { get; set; } = Empty;
    public string RefNr { get; set; } = Empty;
    public string AID { get; set; } = Empty;
    public int Altersverifikation { get; set; }
}