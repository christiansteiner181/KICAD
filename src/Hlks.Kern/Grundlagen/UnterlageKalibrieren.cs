// Befehl unterlage_kalibrieren (Modul 1b, B3): Unterlage auf bekannte Punkte ausrichten. Zwei Punkte der Unterlage
// (von, Unterlagen-Koordinaten) und ihre Sollpunkte im Modell (nach, mm) bestimmen eine Ähnlichkeitstransformation
// aus Faktor, Drehung und Verschiebung (UnterlagenLage). Mit nurVerschieben bleiben Faktor und Drehung, es wird nur
// verschoben. Weicht der Faktor um mehr als 1 % von der Erwartung ab (Einheit der Datei bzw. Massstab), kommt eine Warnung.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class UnterlageKalibrieren : IBefehl
{
    /// <summary>Abweichung des Faktors von der Erwartung, ab der gewarnt wird.</summary>
    public const double Toleranz = 0.01;

    public string Name => "unterlage_kalibrieren";
    public const string BeschreibungText = "Richtet eine Unterlage (DWG/DXF, PDF, Bild) auf bekannte Punkte aus: zwei Punkte der Unterlage (von) und ihre Sollpunkte im Modell (nach) bestimmen Massstab, Drehung und Verschiebung. " +
        "Mit nurVerschieben bleiben Massstab und Drehung, die Unterlage wird nur verschoben (ein Punktpaar genügt). Meldet Faktor und Drehung und warnt, wenn der Faktor um mehr als 1 % von der Erwartung (Einheit bzw. Massstab) abweicht.";
    public string Beschreibung => BeschreibungText;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("unterlage", "text", true, "Unterlage (Kennzeichen oder Id)"),
        new("von", "liste", true, "Zwei Punkte der Unterlage [[u1, v1], [u2, v2]] in Unterlagen-Koordinaten: DWG/DXF in Zeichnungseinheiten; PDF/Bild in Bildpunkten (x nach rechts, y von oben nach unten)"),
        new("nach", "liste", true, "Die zugehörigen Sollpunkte im Modell [[x1, y1], [x2, y2]] in mm"),
        new("nurVerschieben", "bool", false, "true = nur verschieben (Faktor und Drehung bleiben); dann genügt ein Punktpaar"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        // Zahlen in den Hinweisen immer mit Punkt, unabhängig von der Spracheinstellung des Rechners
        var kultur = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try { Ausfuehren2(k, p); }
        finally { CultureInfo.CurrentCulture = kultur; }
    }

    void Ausfuehren2(Kontext k, JsonObject p)
    {
        var ul = k.Arbeit.HoleDerKlasse(Param.Text(p, "unterlage", true), "Unterlage");
        var art = ul.Text("art");
        if (ul.Text("zweck") == "familie")
            throw new HlksFehler($"{ul.Kennzeichen} ist eine Herstellerunterlage einer Familie und hat keine Lage im Projekt.", "Nur Unterlagen mit zweck «plan» (Grundlagen im Grundriss) werden kalibriert.");
        if (art == "ifc")
            throw new HlksFehler($"{ul.Kennzeichen} ist ein IFC; IFC-Dateien werden nicht kalibriert.",
                "Die Ausrichtung eines IFC steht in ifc_uebernehmen (ausrichtung: projekt | gebaeude | koordinationskoerper | einpassen) und im Lagebericht von ifc_pruefen.");
        bool raster = UnterlagenLage.IstRaster(art);
        bool nurVerschieben = Param.Bool(p, "nurVerschieben");
        var von = Paare(p, "von", raster);
        var nach = Paare(p, "nach", false);
        if (von.Count != nach.Count)
            throw new HlksFehler($"«von» hat {von.Count} Punkt(e), «nach» {nach.Count}.", "Zu jedem Punkt der Unterlage gehört ein Sollpunkt im Modell.");
        if (nurVerschieben ? von.Count < 1 : von.Count != 2)
            throw new HlksFehler(nurVerschieben ? "Zum Verschieben braucht es mindestens ein Punktpaar." : $"Zum Kalibrieren braucht es genau zwei Punktpaare, angegeben sind {von.Count}.",
                nurVerschieben ? "von: [[u, v]], nach: [[x, y]]" : "von: [[u1, v1], [u2, v2]], nach: [[x1, y1], [x2, y2]] – oder nurVerschieben = true mit einem Punktpaar.");

        var alt = UnterlagenLage.Lesen(ul);
        var bisher = ul.Eigenschaft("transformation")?.Inhalt as JsonObject;
        UnterlagenLage neu;
        var hinweise = new List<string>();
        if (nurVerschieben)
        {
            var d = von.Select((v, i) => { var (x, y) = alt.Auf(v.U, v.V); return (X: nach[i].U - x, Y: nach[i].V - y); }).ToList();
            double dx = d.Average(q => q.X), dy = d.Average(q => q.Y);
            neu = alt with { Dx = alt.Dx + dx, Dy = alt.Dy + dy };
            double rest = d.Max(q => Math.Sqrt((q.X - dx) * (q.X - dx) + (q.Y - dy) * (q.Y - dy)));
            hinweise.Add($"{ul.Kennzeichen} verschoben um {Zahl(dx)} mm Ost, {Zahl(dy)} mm Nord; Faktor {Zahl(alt.Faktor)} und Drehung {Zahl(alt.Drehung)}° bleiben.");
            if (rest > 1)
                hinweise.Add($"ACHTUNG: Die Punktpaare passen nicht zu einer reinen Verschiebung (Restabweichung {Zahl(rest)} mm). Ohne nurVerschieben werden auch Massstab und Drehung berechnet.");
        }
        else
        {
            neu = UnterlagenLage.ZweiPunkte(von[0], von[1], nach[0], nach[1]);
            double laenge = Math.Sqrt(Math.Pow(nach[1].U - nach[0].U, 2) + Math.Pow(nach[1].V - nach[0].V, 2));
            hinweise.Add($"{ul.Kennzeichen} kalibriert: Faktor {Zahl(neu.Faktor)} ({(raster ? "mm je Bildpunkt" : "mm je Zeichnungseinheit")}), Drehung {Zahl(neu.Drehung)}°, " +
                         $"Verschiebung {Zahl(neu.Dx)} / {Zahl(neu.Dy)} mm; Abstand der Passpunkte {Zahl(laenge)} mm.");
            var warnung = Plausibilitaet(bisher, neu.Faktor);
            if (warnung is not null) hinweise.Add(warnung);
            else if (bisher?["erwartung"] is null) hinweise.Add("Zum Faktor gibt es keine Erwartung (Einheit bzw. Massstab fehlen) – bitte selbst prüfen.");
        }

        // Umriss mitführen: alter Umriss → Unterlagen-Koordinaten → neue Lage
        var e = ul.Kopie();
        if (e.Geometrie is { } g)
            g.Punkte = g.Punkte.Select(q => { var (u, v) = alt.Zurueck(q[0], q[1]); var (x, y) = neu.Auf(u, v); return q.Length > 2 ? new[] { x, y, q[2] } : new[] { x, y }; }).ToList();
        e.Eigenschaften["transformation"] = new Wert { Inhalt = neu.Zu(bisher), Herkunft = k.Herkunft };
        if (Math.Abs(neu.Faktor - alt.Faktor) < 1e-9 && Math.Abs(neu.Drehung - alt.Drehung) < 1e-9 && Math.Abs(neu.Dx - alt.Dx) < 1e-6 && Math.Abs(neu.Dy - alt.Dy) < 1e-6)
        { k.Hinweise.Add($"{ul.Kennzeichen}: Keine Änderung – die Unterlage liegt schon so."); return; }
        k.Setzen(e);
        k.Hinweise.AddRange(hinweise);
    }

    /// <summary>Warnung, wenn der neue Faktor mehr als 1 % von der Erwartung (Einheit der Datei, Massstab) abweicht; sonst null.</summary>
    static string? Plausibilitaet(JsonObject? transformation, double faktor)
    {
        if (Json.Zahl(transformation?["erwartung"]) is not double erwartung || erwartung <= 0) return null;
        double abw = faktor / erwartung - 1;
        if (Math.Abs(abw) <= Toleranz) return null;
        var grundlage = transformation?["grundlage"]?.GetValue<string>();
        return $"ACHTUNG: Der Faktor {Zahl(faktor)} weicht um {Zahl(abw * 100)} % von der Erwartung {Zahl(erwartung)} ab" +
               (grundlage is null ? "" : $" ({grundlage})") +
               ". Mögliche Ursachen: falsche Einheit der Zeichnung, anderer Druckmassstab des PDF (z. B. «auf Seite einpassen») oder falsche Passpunkte – Punkte und Sollmasse prüfen.";
    }

    /// <summary>Punkte [[a, b], …]; bei Rasterunterlagen zählt die Pixelzeile von oben und wird gespiegelt (−y).</summary>
    static List<(double U, double V)> Paare(JsonObject p, string name, bool spiegeln)
    {
        var liste = new List<(double, double)>();
        foreach (var q in Param.Punkte(p, name)) liste.Add((q[0], spiegeln ? -q[1] : q[1]));
        return liste;
    }

    static string Zahl(double d) => d.ToString("0.######", CultureInfo.InvariantCulture);
}
