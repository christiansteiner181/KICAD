// Ablage eines Projekts. Auf dem NAS: <Projekt>\c_Pläne und Modelle\e_KICAD\ mit projekt.json, stand\, aenderungen\,
// sperren\, unterlagen\. Grundsatz: gemeinsame Dateien werden nie überschrieben – jede Änderung ist ein neues Paket.

namespace Hlks.Kern;

public interface IProjektSpeicher
{
    /// <summary>Ordner der Ablage (für Meldungen).</summary>
    string Ort { get; }

    /// <summary>Legt die Ablage eines neuen Projekts an (Ordner samt Unterordnern). Nur Projekt.Anlegen ruft das auf;
    /// alle anderen Zugriffe legen einen fehlenden Projektordner nie an (vertippter Pfad, NAS getrennt).</summary>
    void Anlegen() { }

    ProjektKopf? KopfLesen();
    void KopfSchreiben(ProjektKopf kopf);

    /// <summary>Neuestes Standabbild oder null.</summary>
    StandAbbild? StandLesen();
    /// <summary>Schreibt ein neues Standabbild (eigene Datei, ältere bleiben liegen).</summary>
    void StandSchreiben(StandAbbild stand);

    /// <summary>Ids aller Pakete in der Ablage, aufsteigend sortiert (ordinal).</summary>
    IReadOnlyList<string> PaketListe();
    Paket PaketLesen(string id);
    /// <summary>Schreibt ein Paket als neue Datei. Gibt es die Datei schon, wird IOException geworfen (nie überschreiben).</summary>
    void PaketSchreiben(Paket paket);

    /// <summary>Reserviert Elemente für akteur@geraet. Rückgabe: fremde, noch gültige Reservierungen, die im Weg stehen
    /// (dann wird nichts reserviert). Eigene oder abgelaufene Reservierungen werden übernommen bzw. verlängert.</summary>
    IReadOnlyList<Reservierung> Reservieren(IEnumerable<string> elementIds, string akteur, string geraet);
    /// <summary>Gibt Reservierungen von akteur@geraet frei (alle oder die genannten).</summary>
    void Freigeben(string akteur, string geraet, IEnumerable<string>? elementIds = null);
    IReadOnlyList<Reservierung> Reservierungen();

    /// <summary>Ordner der Unterlagen (Modul 1b: IFC, DWG, PDF, Bilder und daraus abgeleitete Dateien) oder null, wenn
    /// die Ablage keinen hat. Dateien darin werden nie überschrieben: Name = Prüfsumme + ursprünglicher Name.</summary>
    string? UnterlagenOrdner => null;

    /// <summary>Austauschdatei lesen (Modul 1d: Ordner austausch, z. B. «hkap.json» = Basisstand des HKAP-Abgleichs);
    /// null, wenn es sie noch nicht gibt. Ablagen ohne Austauschordner werfen NotSupportedException.</summary>
    byte[]? AustauschLesen(string name) => throw new NotSupportedException("Diese Ablage hat keinen Austauschordner.");

    /// <summary>Austauschdatei atomar ersetzen (Temp-Datei, dann Umbenennen): Lesende sehen alt oder neu, nie halb.
    /// Ablagen ohne Austauschordner werfen NotSupportedException.</summary>
    void AustauschSchreiben(string name, byte[] inhalt) => throw new NotSupportedException("Diese Ablage hat keinen Austauschordner.");
}
