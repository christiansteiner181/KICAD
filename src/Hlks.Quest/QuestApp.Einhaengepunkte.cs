using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Messen;
using Hlks.Xr.Netz;
using StereoKit;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// XR-Labor L0 (entwurf/labor-app.md, «Trennung der Brillen-Apps aus gemeinsamen Quellen»): die Einhängepunkte des Labors in der gemeinsamen Bedienung.
/// Der allgemeine Code ruft das Labor nur über diese <c>partial void</c>-Methoden auf. Fehlt die Umsetzung (Probebau ohne Labor-Dateien), entfernt der
/// Übersetzer die Aufrufe samt ihren Argumenten; Werte, die der allgemeine Code braucht, gehen als <c>ref</c> mit einer Vorgabe des Aufrufers (Vorgabe = ohne
/// Labor). Umgesetzt sind sie in QuestApp.Labor.cs und QuestApp.LaborPruefen.cs.
/// H+K XR E1 (entwurf/xr-empfaenger.md, «Was aus L0/L1 wird»): die Einhängepunkte sind die Grenze zwischen Empfänger-Kern und Werkzeug-Modul «Labor»; das Modul
/// arbeitet nur, wenn das aktive Werkzeug es führt (Werkzeug.MitLabor: XR-Labor, KICAD bis E3). Probebau ohne Labor:
/// <c>dotnet build src/Hlks.Quest/Hlks.Quest.csproj -p:HlksOhneLabor=true</c>. Übersicht als Tabelle: src/Hlks.Quest/LIESMICH.md.
/// </summary>
sealed partial class QuestApp
{
    // ------------------------------------------------------------------------------------------------ Lebenslauf und Netz

    /// <summary>Konstruktor, nach dem Fenster der Apps: die Karte einer Prüfaufgabe anlegen und als Ziel des Wegwischens einhängen (nach der App, vor den Symbolen).</summary>
    partial void LaborAnlegen();

    /// <summary>
    /// H+K XR E1: ein Werkzeug wird aktiv (<see cref="WerkzeugWechseln"/>, vor <see cref="EinstellungenAnwenden"/>; bis E1 «LaborStarten» im Konstruktor): Zustand des
    /// Moduls leeren; führt das Werkzeug das Labor (Werkzeug.MitLabor), Katalog und wartende Messprotokolle aus seinem Speicher lesen.
    /// </summary>
    partial void LaborWerkzeugBetreten();

    /// <summary>H+K XR E1: das aktive Werkzeug wird verlassen (Wechsel): eine laufende Aufgabe bricht ab (Messprotokoll an das bisherige Werkzeug), die Karte geht.</summary>
    partial void LaborWerkzeugVerlassen();

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

    // ------------------------------------------------------------------------------------------------ XR-Labor Spielwiese S1 (QuestApp.Spielwiese.cs)

    /// <summary>
    /// Die Symbole des Felds (<see cref="SymboleBauen"/>, vor den Apps des Werkzeugs): die Spielwiese setzt <paramref name="ersatz"/> = ihre Platzhalter-Apps und
    /// «Werkzeuge» und <paramref name="reihenfolge"/> = ihre feste Reihenfolge der Plätze (Vorgaben null: die Apps des Werkzeugs, Reihenfolge von AppSymbole).
    /// </summary>
    partial void LaborSymbole(ref List<SymbolApps.Symbol>? ersatz, ref IReadOnlyList<string>? reihenfolge);

    /// <summary>Eine App wurde über ihr Symbol geöffnet (<see cref="AppOeffnen"/>): die Spielwiese beginnt ihre Liste oben bzw. ihr Dokument auf Seite 1.</summary>
    partial void LaborAppGeoeffnet(string id);

    /// <summary>
    /// Inhalt der gewählten App (<see cref="InhaltBauen"/>, vor den Apps des Werkzeugs): eine App der Spielwiese baut ihn selbst (<paramref name="gebaut"/> = true,
    /// <paramref name="seiten"/> = Seiten zum Blättern; die Tafel ohne Werkzeugleiste). Vorgabe: nicht gebaut.
    /// </summary>
    partial void LaborAppInhalt(ref GlasInhalt inhalt, ref bool gebaut, ref int seiten);

    /// <summary>Farbe der Tafel: eine App der Spielwiese nimmt die Farbe aus ihrem Katalog (Vorgabe: die Id der App).</summary>
    partial void LaborAppFarbe(ref string farbId);

    /// <summary>Ein Ziel der Tafel wurde gewählt (<see cref="AppEreignis"/>): ein Schalter der Spielwiese (<paramref name="erledigt"/> = true). Vorgabe: nicht erledigt.</summary>
    partial void LaborAppEreignis(string id, ref bool erledigt);

    /// <summary>Ein Wisch auf der Tafel (<see cref="BlaetternAufTafel"/>): die Liste der Spielwiese rollt (<paramref name="erledigt"/> = true). Vorgabe: Seiten.</summary>
    partial void LaborBlaettern(WischRichtung r, StereoKit.Vec3 ort, ref bool erledigt);

    /// <summary>Prüflauf «Spielwiese» (--pruefen mit --spielwiese; QuestApp.PruefenSpielwiese.cs): ein Bild davon; <paramref name="laeuft"/> = true. Ohne Labor: false.</summary>
    partial void LaborPruefenSpielwiese(ref bool laeuft);

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
