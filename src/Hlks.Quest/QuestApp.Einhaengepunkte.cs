using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Messen;
using Hlks.Xr.Netz;
using StereoKit;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// XR-Labor L0 (entwurf/labor-app.md, «Trennung der Brillen-Apps aus gemeinsamen Quellen»): die Einhängepunkte des Labors in der gemeinsamen Bedienung.
/// Der allgemeine Code ruft das Labor nur über diese <c>partial void</c>-Methoden auf. Fehlt die Umsetzung (KICAD-App ohne Labor-Dateien), entfernt der
/// Übersetzer die Aufrufe samt ihren Argumenten; Werte, die der allgemeine Code braucht, gehen als <c>ref</c> mit einer Vorgabe des Aufrufers (Vorgabe = ohne
/// Labor). Umgesetzt sind sie in QuestApp.Labor.cs und QuestApp.LaborPruefen.cs (L0 noch in src/Hlks.Quest, ab L1 in src/Hlks.Labor.Quest).
/// Probebau ohne Labor: <c>dotnet build src/Hlks.Quest/Hlks.Quest.csproj -p:HlksOhneLabor=true</c>. Übersicht als Tabelle: src/Hlks.Labor.Quest/LIESMICH.md.
/// </summary>
sealed partial class QuestApp
{
    // ------------------------------------------------------------------------------------------------ Lebenslauf und Netz

    /// <summary>Konstruktor, nach dem Fenster der Apps: die Karte einer Prüfaufgabe anlegen und als Ziel des Wegwischens einhängen (nach der App, vor den Symbolen).</summary>
    partial void LaborAnlegen();

    /// <summary>Konstruktor, vor <see cref="EinstellungenAnwenden"/>: Katalog des Labors aus der Ablage, wartende Messprotokolle lesen.</summary>
    partial void LaborStarten();

    /// <summary>Verbunden, vor dem Modell: Katalog vom PC. <paramref name="warten"/>: was der Aufrufer abwartet, bevor er das Modell lädt (Vorgabe null).</summary>
    partial void LaborVerbunden(IEbenenQuelle quelle, ref Task? warten);

    /// <summary>Alle 30 s, vor dem Modell: Katalog vom PC nachfragen (ETag, meist 304). <paramref name="warten"/> wie <see cref="LaborVerbunden"/>.</summary>
    partial void LaborNachladen(ref Task? warten);

    /// <summary>Kanal «willkommen» (Hintergrund): wartende Messprotokolle nachsenden.</summary>
    partial void LaborWillkommen();

    /// <summary>Kanal, jede Nachricht des PCs (Hintergrund): Quittung «messprotokoll».</summary>
    partial void LaborNachricht(string typ, JsonElement inhalt);

    /// <summary>Stellschraube vom PC, vor allen anderen: «labor.aufgabe». Setzt <paramref name="ergebnis"/> («ok», «ungültig»), wenn das Labor sie übernimmt.</summary>
    partial void LaborStellschraube(string schluessel, JsonElement wert, ref string? ergebnis);

    // ------------------------------------------------------------------------------------------------ Bild

    /// <summary>Ein Bild, nach Messen und Marker: die laufende Prüfaufgabe (vorbereiten, messen, Karte, Markierung).</summary>
    partial void LaborBild(Pose kopf);

    /// <summary>Beim Begehen nach den Händen (nicht, solange der Bildschirm des PCs da ist): Variante «teleport» = «stick».</summary>
    partial void LaborStick(Pose kopf, XVec3 kopfRef);

    /// <summary>Box (Variante «tablet»), beim Begehen: Werte und Antworten der Aufgabe «tablet-wert-ablesen».</summary>
    partial void LaborImTablet();

    /// <summary>Ist eine Hand an der Karte der Prüfaufgabe? (Vorgabe false.)</summary>
    partial void LaborHandAnKarte(ref bool nah);

    /// <summary>Belegte Flächen ergänzen (die Karte, solange eine Aufgabe steht): das Symbolfeld weicht aus, Hinweise meiden sie, Marker dahinter treten zurück.</summary>
    partial void LaborFlaechen(List<TabletPose> flaechen);

    /// <summary>Läuft eine Prüfaufgabe (für die Bedeutung eines Wischs)? (Vorgabe false.)</summary>
    partial void LaborAufgabeOffen(ref bool offen);

    /// <summary>Raum für «Raum zeigen» vor dem allgemeinen: der markierte der Aufgabe «raum-ar-oeffnen» bzw. der für sie gemerkte (Vorgabe null).</summary>
    partial void LaborRaumZumZeigen(ref string? raum);

    // ------------------------------------------------------------------------------------------------ Ereignisse der Bedienung (Messung der Prüfaufgabe)

    /// <summary>Sprung auf <paramref name="ziel"/> (Szene).</summary>
    partial void LaborSprung(Xz ziel);

    /// <summary>Ein Sprung wurde abgebrochen.</summary>
    partial void LaborSprungAbgebrochen();

    /// <summary>Ein Drehschritt beim Begehen (Blickrichtung in der Welt vorher und nachher).</summary>
    partial void LaborDrehschritt(double blickVorher, double blickNachher);

    /// <summary>Beim Begehen in ein anderes Geschoss gewechselt.</summary>
    partial void LaborEtageGewechselt(ListenGeschoss? geschoss);

    /// <summary>Ein Wisch hoch/runter ohne Wirkung (kein Geschoss darüber bzw. darunter, Variante «Etage per App»).</summary>
    partial void LaborWischOhneWirkung();

    /// <summary>Am Tisch ein Schritt weg vom Ziel der Aufgabe «raum-ar-oeffnen» (andere Etage, Zurücksetzen, Echter Tisch aus, Tisch neu antippen).</summary>
    partial void LaborRaumRueckschritt(string grund);

    /// <summary>Eine Messung ist fertig.</summary>
    partial void LaborGemessen(Messung messung);

    /// <summary>Messband abgebrochen bzw. Pinch ohne Ziel.</summary>
    partial void LaborMessenDaneben(string grund);

    /// <summary>Am Fenster der Apps daneben getippt.</summary>
    partial void LaborDaneben();

    // ------------------------------------------------------------------------------------------------ Protokoll und Prüflauf

    /// <summary>Statuszeile «P4 Status»: Teil des Labors nach «Fassung=…» (Katalog, Aufgabe, Messprotokolle), mit «, » am Ende (Vorgabe leer).</summary>
    partial void LaborKennzahlen(ref string text);

    /// <summary>Schluss der Zeile «P4 ok/Fehler»: «, Quittungen=n» (Vorgabe leer).</summary>
    partial void LaborQuittungen(ref string text);

    /// <summary>Prüflauf P5b (einmal): Katalog, Grenzen, Vorgaben, Prüfaufgaben – nach «Fassung=…», vor Röntgen-Hand bzw. flacher Hand. «FEHLER» lässt den Lauf scheitern.</summary>
    partial void LaborPruefRegeln(List<string> teile);

    /// <summary>Prüfbilder P5b (--bilder): die Karte der Aufgabe.</summary>
    partial void LaborPruefBilder(string ordner);

    /// <summary>Vor Prüfbildern: die Karte einer fertigen Aufgabe wegräumen (Messprotokoll einmal).</summary>
    partial void LaborPruefbildVorbereiten();

    // ------------------------------------------------------------------------------------------------ Werte aus den Einhängepunkten

    /// <summary>Ist eine Hand an der Karte einer Prüfaufgabe (dann zielt ihr Pinch nicht, sie misst, markiert und röntgt nicht)? Ohne Labor nie.</summary>
    bool HandAnKarte
    {
        get
        {
            var nah = false;
            LaborHandAnKarte(ref nah);
            return nah;
        }
    }

    /// <summary>Teil des Labors in der Statuszeile (ohne Labor leer).</summary>
    string LaborKennzahlenText()
    {
        var text = "";
        LaborKennzahlen(ref text);
        return text;
    }

    /// <summary>Quittungen der Messprotokolle am Schluss der Prüfzeile (ohne Labor leer).</summary>
    string LaborQuittungenText()
    {
        var text = "";
        LaborQuittungen(ref text);
        return text;
    }
}
