// Quest App V1b-1 (Modul XR, Gestenlabor): alle Startwerte des Erkennerkerns an einer Stelle. ANNAHMEN V1b-1 (keine Normwerte, keine Messung in der Brille):
// gewählt an den synthetischen Reihen der Tests (tests/Hlks.Xr.Tests/Gesten*Tests.cs), im Labor mit echten Händen nachzustellen. Werden mit V1b-2 Stellschrauben.

namespace Hlks.Xr.Gesten;

/// <summary>Einstellungen des Gestenerkenners (Merkmale, Vergleich, Aufnahme, Live, Konflikte). Unveränderlich; Änderungen mit <c>with</c>.</summary>
public sealed record GestenEinstellungen
{
    // ------------------------------------------------------------------------------------------------ Merkmale (Annahmen V1b-1)

    /// <summary>Stützpunkte nach dem Neu-Abtasten (feste Länge jeder Folge). Annahme 32.</summary>
    public int Stuetzpunkte { get; init; } = 32;
    /// <summary>Breite des Bands im Dynamic Time Warping (Sakoe-Chiba) als Anteil der Stützpunkte. Annahme 0.2 (± 6 von 32).</summary>
    public double BandAnteil { get; init; } = 0.2;
    /// <summary>Gewicht der Lage der Handflächenmitte (relativ zum Start, auf die Ausdehnung normiert). Annahme 1.</summary>
    public double GewichtLage { get; init; } = 1.0;
    /// <summary>Gewicht der Handflächennormale (Einheitsvektor im Körperbezug). Annahme 0.5.</summary>
    public double GewichtNormale { get; init; } = 0.5;
    /// <summary>Gewicht der Krümmung je Finger ((Bogen − Sehne) / Handgrösse). Annahme 0.8.</summary>
    public double GewichtKruemmung { get; init; } = 0.8;
    /// <summary>Gewicht des Pinch (Abstand Daumen–Zeigefinger / Handgrösse, gekappt bei <see cref="PinchKappe"/>). Annahme 0.6.</summary>
    public double GewichtPinch { get; init; } = 0.6;
    /// <summary>Gewicht der Grösse der Bewegung (ln der Ausdehnung, gleich in jedem Stützpunkt). Annahme 0.1 (0.25 lehnte Grösse × 0.75 eines sehr gleichförmigen Wischs ab).</summary>
    public double GewichtGroesse { get; init; } = 0.1;
    /// <summary>Beidhändig: Gewicht des Abstands der Handflächen (m). Annahme 1 je m.</summary>
    public double GewichtHandabstand { get; init; } = 1.0;
    /// <summary>Beidhändig: Gewicht der Richtung von der linken zur rechten Handfläche (Einheitsvektor im Körperbezug). Annahme 0.3.</summary>
    public double GewichtHandrichtung { get; init; } = 0.3;
    /// <summary>Kleinere Ausdehnung der Bahn wird nicht hochskaliert (m): so bleibt das Zittern einer ruhenden Hand klein. Annahme 0.08 m.</summary>
    public double MindestAusdehnungM { get; init; } = 0.08;
    /// <summary>Bezug der Grösse (m): ln(Ausdehnung / Bezug). Nur der Unterschied zählt. Annahme 0.3 m.</summary>
    public double BezugAusdehnungM { get; init; } = 0.3;
    /// <summary>Pinch-Abstand in Handgrössen wird hier gekappt (offene Hand ≈ 1.4, Pinch ≈ 0.1). Annahme 1.</summary>
    public double PinchKappe { get; init; } = 1.0;
    /// <summary>Handgrösse (Handgelenk – Grundgelenk Mittelfinger, m), wenn die Gelenke dazu fehlen; wie XrTabletLogik («≈ 0.09 m»).</summary>
    public double HandGroesseVorgabeM { get; init; } = 0.09;

    // ------------------------------------------------------------------------------------------------ Schwelle und Entscheid (Annahmen V1b-1)

    /// <summary>Mindestzahl Beispiele einer gültigen Geste (Auftrag V1b-1: mindestens 3).</summary>
    public int MindestBeispiele { get; init; } = 3;
    /// <summary>Schwelle = Faktor × grösster Leave-one-out-Abstand der Beispiele. Annahme 1.8.</summary>
    public double SchwellenFaktor { get; init; } = 1.8;
    /// <summary>Untere Grenze der Schwelle (sehr gleichförmige Beispiele). Annahme 0.06.</summary>
    public double SchwelleMin { get; init; } = 0.06;
    /// <summary>Obere Grenze der Schwelle (sehr verschiedene Beispiele nehmen sonst alles an). Annahme 0.8.</summary>
    public double SchwelleMax { get; init; } = 0.8;
    /// <summary>«Unklar», wenn die zweitbeste Geste (eigene Schwelle erfüllt) im Verhältnis Abstand/Schwelle höchstens um diesen Anteil schlechter ist. Annahme 0.25.</summary>
    public double UnklarAnteil { get; init; } = 0.25;

    // ------------------------------------------------------------------------------------------------ Aufnahme (Annahmen V1b-1)

    /// <summary>Glättung der Bewegungsenergie (s, Fenster des gleitenden Mittels und Abstand der Differenz). Annahme 0.1 s.</summary>
    public double GlaettungS { get; init; } = 0.1;
    /// <summary>Bewegung, wenn die Energie (Tempo der Handfläche in m/s plus gewichtete Finger-, Dreh- und Pinch-Anteile) darüber liegt. Annahme 0.15.</summary>
    public double EnergieAn { get; init; } = 0.15;
    /// <summary>Anteil der Krümmungsänderung an der Energie (m/s je 1/s mittlerer Krümmungsänderung der Finger). Annahme 0.3.</summary>
    public double EnergieKruemmung { get; init; } = 0.3;
    /// <summary>Anteil der Drehung der Handfläche an der Energie (m/s je rad/s). Annahme 0.05.</summary>
    public double EnergieDrehung { get; init; } = 0.05;
    /// <summary>Anteil der Pinch-Änderung an der Energie (m/s je Handgrösse/s). Annahme 0.1.</summary>
    public double EnergiePinch { get; init; } = 0.1;
    /// <summary>Kürzere Ruhe innerhalb einer Bewegung trennt sie nicht (s). Annahme 0.15 s.</summary>
    public double LueckeS { get; init; } = 0.15;
    /// <summary>So lange ruhige Hände beenden eine Aufnahme (GestenRekorder) (s). Annahme 0.3 s.</summary>
    public double RuheS { get; init; } = 0.3;
    /// <summary>Vor- und Nachlauf des Zuschnitts um die Bewegung (s); so viel Ruhe muss vor und nach der Bewegung in der Aufnahme liegen. Annahme 0.05 s.</summary>
    public double RandS { get; init; } = 0.05;
    /// <summary>Kürzeste Geste (s). Annahme 0.2 s.</summary>
    public double MinDauerS { get; init; } = 0.2;
    /// <summary>Längste Geste (s); muss mit Rand in den Puffer des Live-Erkenners passen. Annahme 2.0 s.</summary>
    public double MaxDauerS { get; init; } = 2.0;
    /// <summary>GestenRekorder: so lange wartet er höchstens auf den Beginn der Bewegung (s). Annahme 5 s.</summary>
    public double WarteMaxS { get; init; } = 5.0;

    // ------------------------------------------------------------------------------------------------ Live (Annahmen V1b-1)

    /// <summary>Ringpuffer der letzten Bilder (s). Auftrag V1b-1: ~2.5 s.</summary>
    public double PufferS { get; init; } = 2.5;
    /// <summary>Geprüft wird höchstens alle SchrittS (s), nicht in jedem Bild. Annahme 0.05 s (20 je s).</summary>
    public double SchrittS { get; init; } = 0.05;
    /// <summary>Längen der Kandidaten: kürzestes Beispiel / (1 + Rand) bis längstes × (1 + Rand). Annahme 0.6 (deckt Tempo ± 40 %).</summary>
    public double LaengenRand { get; init; } = 0.6;
    /// <summary>Verhältnis zweier benachbarter Kandidatenlängen. Annahme 1.2.</summary>
    public double LaengenSchritt { get; init; } = 1.2;
    /// <summary>Ein Treffer gilt erst, wenn er so lange nicht besser wurde (s): so zählt das Ende der ganzen Geste, nicht ein Teil. Annahme 0.12 s.</summary>
    public double WarteS { get; init; } = 0.12;
    /// <summary>Sperrzeit nach einem Treffer (s, ab dem Ende der erkannten Geste); Bewegungen darin zählen nicht (wie XrWischLogik.WischSperreS). Annahme 0.5 s.</summary>
    public double SperreS { get; init; } = 0.5;
    /// <summary>Längere Lücke zwischen zwei Bildern gilt als Verlust der Hände (s). Annahme 0.2 s.</summary>
    public double BildLueckeS { get; init; } = 0.2;
    /// <summary>
    /// Bewegungstor des Live-Erkenners (spart Rechenzeit): Eine Hand «bewegt sich», sobald sie sich seit der letzten Bewegung um mehr als
    /// TorLageM verschoben hat (bzw. die mittlere Krümmung um TorKruemmung, der Pinch um TorPinch, die Normale um TorDrehungGrad). Geprüft werden nur Abschnitte, in
    /// denen das geschah; eine Geste ganz ohne merkliche Bewegung (unter allen vier Werten) erkennt er deshalb nicht. Annahme 0.03 m (bei 5 mm Rauschen praktisch
    /// nie), 0.15, 0.3, 25°.
    /// </summary>
    public double TorLageM { get; init; } = 0.03;
    public double TorKruemmung { get; init; } = 0.15;
    public double TorPinch { get; init; } = 0.3;
    public double TorDrehungGrad { get; init; } = 25;

    // ------------------------------------------------------------------------------------------------ Konflikte (Annahme V1b-1)

    /// <summary>Warnung, wenn die Trennschärfe (kleinster Abstand zu fremden Beispielen / Schwelle) unter diesem Faktor liegt; bis 1 = Konflikt. Annahme 1.5.</summary>
    public double KonfliktWarnFaktor { get; init; } = 1.5;

    public static readonly GestenEinstellungen Vorgabe = new();

    /// <summary>Breite des Bands in Stützpunkten.</summary>
    public int Band => Math.Max(1, (int)Math.Round(BandAnteil * Stuetzpunkte, MidpointRounding.AwayFromZero));

    /// <summary>Fehler in den Einstellungen (null = in Ordnung).</summary>
    public string? Pruefen()
    {
        static bool Pos(double x) => double.IsFinite(x) && x > 0;
        static bool NichtNeg(double x) => double.IsFinite(x) && x >= 0;
        if (Stuetzpunkte is < 8 or > 128) return "Stützpunkte ausserhalb 8 … 128.";
        if (!(BandAnteil > 0 && BandAnteil <= 1)) return "Bandanteil ausserhalb (0, 1].";
        if (!NichtNeg(GewichtLage) || !NichtNeg(GewichtNormale) || !NichtNeg(GewichtKruemmung) || !NichtNeg(GewichtPinch) || !NichtNeg(GewichtGroesse)
            || !NichtNeg(GewichtHandabstand) || !NichtNeg(GewichtHandrichtung)) return "Gewichte müssen endlich und nicht negativ sein.";
        if (!Pos(MindestAusdehnungM) || !Pos(BezugAusdehnungM) || !Pos(PinchKappe) || !Pos(HandGroesseVorgabeM)) return "Bezugsgrössen müssen positiv sein.";
        if (MindestBeispiele is < 2 or > 50) return "Mindestbeispiele ausserhalb 2 … 50.";
        if (!Pos(SchwellenFaktor) || !Pos(SchwelleMin) || !Pos(SchwelleMax) || SchwelleMin > SchwelleMax) return "Schwellenwerte ungültig.";
        if (!NichtNeg(UnklarAnteil)) return "Unklar-Anteil ungültig.";
        if (!Pos(GlaettungS) || !Pos(EnergieAn) || !NichtNeg(EnergieKruemmung) || !NichtNeg(EnergieDrehung) || !NichtNeg(EnergiePinch)) return "Energiewerte ungültig.";
        if (!NichtNeg(LueckeS) || !Pos(RuheS) || !NichtNeg(RandS) || !Pos(MinDauerS) || !(MaxDauerS > MinDauerS) || !Pos(WarteMaxS)) return "Aufnahmewerte ungültig.";
        if (!Pos(PufferS) || MaxDauerS > PufferS) return "Der Puffer muss mindestens so lang wie die längste Geste sein.";
        if (!Pos(SchrittS) || !NichtNeg(LaengenRand) || !(LaengenSchritt > 1.01) || !NichtNeg(WarteS) || !NichtNeg(SperreS) || !Pos(BildLueckeS))
            return "Live-Werte ungültig.";
        if (!Pos(TorLageM) || !NichtNeg(TorKruemmung) || !NichtNeg(TorPinch) || !NichtNeg(TorDrehungGrad)) return "Werte des Bewegungstors ungültig.";
        if (!(KonfliktWarnFaktor >= 1)) return "Warnfaktor muss mindestens 1 sein.";
        return null;
    }

    internal GestenEinstellungen Geprueft()
    {
        if (Pruefen() is { } f) throw new ArgumentException("Gesteneinstellungen: " + f);
        return this;
    }
}
