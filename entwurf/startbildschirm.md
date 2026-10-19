# Startbildschirm (wie Revit) – Spezifikation

Stand 19.10.2026 · Hauptlauf

## Anlass und Entscheide

Christian, 19.10.2026: «Es sollte einen Startbildschirm geben mit den letzten Projekten, wie bei Revit.»
Bisher: ohne offenes Projekt ein kleiner Dialog «Projekt wählen» (`dialoge.projektStart`) mit Textliste «zuletzt
geöffnet» (`/api/zuletzt`, %APPDATA%\HLKS-CAD\zuletzt.json je Rechner), Ordnerpfad zum Eintippen, Beispielprojekt.

| Frage | Entscheid Christian (Fragefenster 19.10.2026) |
| --- | --- |
| Was zeigt das Desktop-Icon? | **Immer den Startbildschirm.** Nur beim Neustart wegen eines neuen Stands öffnet KICAD direkt wieder das Projekt, das offen war |
| Inhalt ausser den letzten Projekten | **Familien** (zuletzt bearbeitete, Einstieg Bibliothek) und **alle KICAD-Projekte auf W:** (`W:\02_Projektdateien Objekte\<Objekt>\c_Pläne und Modelle\e_KICAD`) |
| Vorschaubild | **Immer 3D** des ganzen Projekts |
| Liste «zuletzt geöffnet» | **Je Benutzer, mit Anheften** (gleich auf allen Rechnern des Benutzers) |

## Aufbau (Vorbild Revit «Home»)

- **Ganze Fläche** statt Dialog, im Design von KICAD (dunkel/hell). Links eine schmale Leiste: **Projekte** (Öffnen …,
  Neu …, Beispielprojekt), **Familien** (Bibliothek …), darunter Version/Stand von KICAD (Build-Datum).
- **Zuletzt geöffnet:** Kacheln mit **3D-Vorschaubild**, Name, Objektordner (kurz), zuletzt geöffnet (Datum, Rechner),
  Marke **angeheftet**; angeheftete zuerst, dann nach Zeit. Je Kachel Menü: Öffnen, Anheften/Lösen, Aus der Liste
  entfernen, Ordner im Explorer zeigen. Suchfeld filtert alle Bereiche.
- **Alle KICAD-Projekte auf W:** Liste (Name, Objekt, Projektnummer, zuletzt geändert) aus der Suche nach `projekt.json`
  unter `projektablage` (`kataloge/buero.json`: Pfad + `kicadOrdner`); Suche im Hintergrund, Zwischenspeicher, Knopf
  «Neu suchen»; nicht erreichbar → Hinweis, Rest geht.
- **Familien:** zuletzt bearbeitete Familien aus der Bibliothek (Index/Log aus B7: Name, Kategorie, Hersteller, Status,
  Version, Projekt der letzten Änderung, Zeit); Klick öffnet das Projekt der letzten Änderung und darin die Familie im
  Editor. «Bibliothek …» öffnet bei offenem Projekt den Dialog «Familien im Explorer …» (C3), sonst die Liste der
  Bibliothek nur lesend.
- **Öffnen …:** statt Pfad-Eintippen ein Ordner-Browser über den Server (Laufwerke, W:, zuletzt benutzte Ordner); erkennt
  KICAD-Projekte (`projekt.json`) und zeigt sie hervorgehoben. Pfad eintippen bleibt möglich.
- **Neu …:** wie heute (Ordner, Name), mit dem Ordner-Browser.
- **Haus-Symbol** oben links (Schnellzugriff/Titelzeile) und **Strg+D** (wie Revit) zeigen den Startbildschirm auch bei
  offenem Projekt; das Projekt bleibt im Hintergrund offen, Esc bzw. Klick auf das offene Projekt kehrt zurück.

## Daten

- **Liste je Benutzer, auf allen Rechnern gleich:** Ablage im OneDrive des Benutzers (`%OneDrive%\HLKS-CAD\zuletzt\`),
  **je Rechner eine Datei** (`<Rechner>.json`, nie dieselbe Datei von zwei Rechnern → keine OneDrive-Konfliktkopien); die
  Liste ist die Vereinigung (je Projektordner der neueste Eintrag; «angeheftet» und «entfernt» mit Zeit, der neuere Eintrag
  gilt). Ohne OneDrive: %APPDATA%\HLKS-CAD\zuletzt.json wie bisher (wird beim ersten Start übernommen).
- **3D-Vorschaubild:** Datei `vorschau.png` im Projektordner (z. B. 640×400). Erzeugt im Browser aus dem 3D-Modell (ganzes
  Projekt, feste Isometrie von Südwesten, Architektur grau, HLKS in Systemfarben), wenn ein Projekt geöffnet und geladen ist
  und danach höchstens alle 10 min bei geändertem Stand; über `POST /api/projekt/vorschau` gespeichert. Projekte ohne
  Bild: Platzhalter mit Anfangsbuchstaben.
- **Projektsuche:** `GET /api/projekte/ablage` (Liste mit Name aus `projekt.json`, Objektordner, Zeit der letzten Änderung),
  Suche nur in der festen Tiefe `<pfad>\*\<kicadOrdner>\projekt.json`, Zwischenspeicher 10 min.
- **Ordner-Browser:** `GET /api/ordner?pfad=` (Unterordner, Kennzeichen «KICAD-Projekt»); nur lesen.
- **Familien:** `GET /api/familien/zuletzt` aus dem Bibliotheksindex (ohne offenes Projekt lesbar).

## Desktop-Start

`werkzeuge/kicad_starten.ps1` (Hauptlauf): kein Vorgabeprojekt mehr – läuft kein Server, startet er ohne Projekt
(Startbildschirm); läuft er aktuell, öffnet das Fenster mit `?start=1` (Startbildschirm über dem offenen Projekt); Neustart
wegen neuem Stand öffnet das bisherige Projekt direkt (ohne `?start=1`).

## Zuteilung

| Teil | Inhalt | Wer |
| --- | --- | --- |
| A | Spezifikation, `kataloge/buero.json` (projektablage), Desktop-Start | Hauptlauf |
| S1 | Startbildschirm, Endpunkte, Liste je Benutzer, Vorschaubild, Ordner-Browser, Haus-Symbol/Strg+D, Tests, Bilder | hlks-modul |

**Fertig, wenn …** das Desktop-Icon den Startbildschirm mit den letzten Projekten als 3D-Kacheln zeigt; ein angeheftetes
Projekt auf dem zweiten Rechner ebenfalls angeheftet erscheint; die KICAD-Projekte auf W: gelistet sind; eine Familie aus
«zuletzt bearbeitet» sich im richtigen Projekt im Editor öffnet; Strg+D bei offenem Projekt zum Startbildschirm und zurück
führt.
