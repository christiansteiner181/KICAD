// Rechnung für Aufbauten (Modul 1c, B2): R = Σ d/λ, U = 1/(Rsi + R + Rse). Das ist Physik (Herkunft «berechnung»), kein
// Normwert; die eingesetzten Zahlen tragen ihre Quelle. λ, Rsi und Rse ohne Quelle gelten als fehlend – der Kern rechnet
// nur, was belegt ist, und nennt, was fehlt. Ein ausdrücklich gesetzter U-Wert (Herkunft nicht «berechnung», z. B.
// Datenblatt oder IFC) geht vor und wird nie überschrieben.

using System.Text.Json.Nodes;
using static Hlks.Kern.HuelleHilfe;

namespace Hlks.Kern;

public sealed class SchichtErgebnis
{
    public int Nr { get; init; }
    public string? MaterialId { get; init; }
    public string? Material { get; init; }          // Kennzeichen
    public string? MaterialName { get; init; }
    public double? DickeMm { get; init; }
    public double? Lambda { get; init; }
    public string? LambdaQuelle { get; init; }
    public double? R { get; init; }
}

public sealed class AufbauErgebnis
{
    public required Element Aufbau { get; init; }
    public string Art { get; init; } = "";
    /// <summary>Wirksame Richtung des Wärmestroms (ausdrücklich oder aus der Art).</summary>
    public string Waermestrom { get; init; } = "horizontal";
    public bool WaermestromAusdruecklich { get; init; }
    /// <summary>Gruppe der Wärmeübergangswiderstände: Waermestrom, oder «erdreich» wenn ausdrücklich verlangt.</summary>
    public string Gruppe { get; init; } = "horizontal";
    public List<SchichtErgebnis> Schichten { get; } = [];
    public double? R { get; set; }
    public string? RQuelle { get; set; }
    public double? Rsi { get; set; }
    public double? Rse { get; set; }
    public string? UebergangQuelle { get; set; }
    public double? UGerechnet { get; set; }
    public string? UQuelle { get; set; }
    public double? UAusdruecklich { get; set; }
    public string? UAusdruecklichQuelle { get; set; }
    /// <summary>Wirksamer U-Wert: der ausdrückliche, sonst der gerechnete.</summary>
    public double? U => UAusdruecklich ?? UGerechnet;
    /// <summary>Was für die Rechnung fehlt (jede Zeile mit Hinweis, was zu tun ist).</summary>
    public List<string> Fehlt { get; } = [];
}

public static class HuelleRechnung
{
    public const string Horizontal = "horizontal", Aufwaerts = "aufwaerts", Abwaerts = "abwaerts", Erdreich = "erdreich";

    /// <summary>Wärmestrom ohne Angabe aus der Art: Wand und Tür horizontal, Decke und Dach aufwärts, Boden abwärts.</summary>
    public static string WaermestromAusArt(string? art) => art switch
    {
        "decke" or "dach" => Aufwaerts,
        "boden" => Abwaerts,
        _ => Horizontal,
    };

    /// <summary>Rsi/Rse einer Gruppe aus Projektinformation.uebergang. Null, wenn die Gruppe fehlt; Quelle null = ohne Quelle.</summary>
    public static (double? Rsi, double? Rse, string? Quelle, bool Vorhanden) Uebergang(Modell m, string gruppe)
    {
        var info = Projektinfo(m);
        var w = info?.Eigenschaft("uebergang");
        if (w?.Inhalt is not JsonObject o) return (null, null, null, false);
        var g = o.FirstOrDefault(x => x.Key.Equals(gruppe, StringComparison.OrdinalIgnoreCase)).Value as JsonObject;
        if (g is null) return (null, null, null, false);
        var quelle = Sauber(Param.Text(g, "quelle")) ?? Quelle(w);
        return (Json.Zahl(g["rsi"]), Json.Zahl(g["rse"]), quelle, true);
    }

    /// <summary>Rechnet R und U eines Aufbaus nach dem Stand des Modells (ohne etwas zu ändern).</summary>
    /// <param name="gruppe">Gruppe der Übergangswiderstände ausdrücklich (z. B. «erdreich» für Raumgrenzen gegen Erdreich);
    /// null = aus dem Wärmestrom des Aufbaus.</param>
    public static AufbauErgebnis Rechnen(Modell m, Element aufbau, string? gruppe = null)
    {
        var art = aufbau.Text("art") ?? "";
        bool ausdruecklich = Sauber(aufbau.Text("waermestrom")) is not null;
        var strom = ausdruecklich ? aufbau.Text("waermestrom")!.Trim() : WaermestromAusArt(art);
        var erg = new AufbauErgebnis { Aufbau = aufbau, Art = art, Waermestrom = strom, WaermestromAusdruecklich = ausdruecklich, Gruppe = gruppe ?? strom };

        // ---- Schichten: R = Σ d/λ (d in m)
        var liste = Schichten(aufbau);
        if (liste.Count == 0) erg.Fehlt.Add("Schichten fehlen (aufbau_aendern mit schichten).");
        bool vollstaendig = liste.Count > 0;
        double summe = 0;
        var terme = new List<string>(); var lambdaQuellen = new List<string>();
        int nr = 0;
        foreach (var s in liste)
        {
            nr++;
            var mat = s.Material is null ? null : m.Finde(s.Material);
            if (mat is not null && mat.Klasse != "Material") mat = null;
            double? lambda = null; string? lq = null; double? r = null;
            if (mat is null)
            {
                erg.Fehlt.Add($"Schicht {nr}: Material nicht gefunden (Id {s.Material ?? "–"}).");
                vollstaendig = false;
            }
            else
            {
                var (l, hatQuelle) = ZahlMitQuelle(mat, "lambda");
                if (l is null) { erg.Fehlt.Add($"Schicht {nr}: λ von {mat.Kennzeichen}{Namen(mat)} fehlt (material_aendern mit werte.lambda und Quelle)."); vollstaendig = false; }
                else if (!hatQuelle) { erg.Fehlt.Add($"Schicht {nr}: λ von {mat.Kennzeichen}{Namen(mat)} hat keine Quelle und gilt nicht (material_aendern mit werte.lambda und Quelle)."); vollstaendig = false; }
                else if (l <= 0) { erg.Fehlt.Add($"Schicht {nr}: λ von {mat.Kennzeichen}{Namen(mat)} ist nicht grösser als 0."); vollstaendig = false; }
                else { lambda = l; lq = Quelle(mat.Eigenschaft("lambda")); }
            }
            if (s.Dicke is not > 0) { erg.Fehlt.Add($"Schicht {nr}: Dicke fehlt oder ist nicht grösser als 0."); vollstaendig = false; }
            else if (lambda is double lam)
            {
                r = s.Dicke.Value / 1000.0 / lam;
                summe += r.Value;
                terme.Add($"{Z(s.Dicke.Value / 1000.0)} m / {Z(lam)} W/(m·K)");
                if (lq is not null && mat is not null && !lambdaQuellen.Any(x => x.StartsWith(mat.Kennzeichen + " ", StringComparison.Ordinal)))
                    lambdaQuellen.Add($"{mat.Kennzeichen}: {lq}");
            }
            erg.Schichten.Add(new SchichtErgebnis
            {
                Nr = nr, MaterialId = s.Material, Material = mat?.Kennzeichen, MaterialName = mat?.Name, DickeMm = s.Dicke,
                Lambda = lambda, LambdaQuelle = lq, R = r,
            });
        }
        if (vollstaendig)
        {
            erg.R = summe;
            erg.RQuelle = $"R = Σ d/λ = {string.Join(" + ", terme)} = {Z(summe)} m²·K/W; λ-Quellen: {string.Join("; ", lambdaQuellen)}";
        }

        // ---- ausdrücklicher U-Wert (Datenblatt, IFC): geht vor
        var uw = aufbau.Eigenschaft("uWert");
        if (uw?.AlsZahl() is double ue && uw.Herkunft.Art != "berechnung")
        {
            erg.UAusdruecklich = ue;
            erg.UAusdruecklichQuelle = Quelle(uw);
        }

        // ---- Rsi/Rse und U = 1/(Rsi + R + Rse)
        var (rsi, rse, uq, vorhanden) = Uebergang(m, erg.Gruppe);
        if (!vorhanden)
            erg.Fehlt.Add($"Rsi/Rse für «{erg.Gruppe}» fehlen (uebergang_setzen mit Quelle).");
        else if (rsi is null || rse is null)
            erg.Fehlt.Add($"Rsi oder Rse für «{erg.Gruppe}» sind unvollständig (uebergang_setzen).");
        else if (uq is null)
            erg.Fehlt.Add($"Rsi/Rse für «{erg.Gruppe}» haben keine Quelle und gelten nicht (uebergang_setzen mit Quelle).");
        else { erg.Rsi = rsi; erg.Rse = rse; erg.UebergangQuelle = uq; }

        if (erg.R is double rr && erg.Rsi is double a && erg.Rse is double b)
        {
            double summeR = a + rr + b;
            if (summeR > 0)
            {
                erg.UGerechnet = 1.0 / summeR;
                erg.UQuelle = $"U = 1/(Rsi + R + Rse) = 1/({Z(a)} + {Z(rr)} + {Z(b)}) = {Z(erg.UGerechnet.Value)} W/(m²·K); Rsi/Rse {erg.Gruppe}: {erg.UebergangQuelle}; {erg.RQuelle}";
            }
        }
        return erg;
    }

    static string Namen(Element mat) => mat.Name is null ? "" : $" «{mat.Name}»";
}
