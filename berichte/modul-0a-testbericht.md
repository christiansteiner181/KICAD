# Testbericht Modul 0a – Datenmodell und Kern-API

Stand 17.10.2026 · gebaut am 17.10.2026 (geplant 19.–23.10.2026) · **von Christian abgenommen am 17.10.2026 – M1 erreicht**

## Ergebnis

Das Abnahmekriterium ist erfüllt: Eine KI ohne Vorwissen beantwortet Fragen zum Beispielprojekt richtig und erzeugt
Raum- und Strangliste. Sonnet 5.5 und Haiku 5.5 beantworten je **10 von 10** Fragen richtig, nur über die Werkzeuge
des HLKS-CAD und ohne das Modell zu verändern. Alle **310** automatischen Tests bestehen, keiner ist übersprungen.

## Was gebaut ist

| Baustein | Inhalt | Ort |
| --- | --- | --- |
| Datenmodell | Elemente mit Anschlüssen (Medium, Fliessrichtung), Verbindungen, Zuordnung zu Geschoss/Raum/System, Geometrie, Eigenschaften mit Herkunft; Schichten Raum, Hülle, System, Funktion (MSRL), Ansicht | `src/Hlks.Kern/Modell` |
| Kataloge | 50 Klassen mit IFC-Klasse, vorläufigem BKP und Rolle im Netz; 15 Medien mit Darstellung nach SIA 410/1; 15 Systemtypen | `kataloge/` |
| Modellbeschreibung | JSON-Schema mit deutschen Erklärungen | `schema/hlks-modell.schema.json` |
| Befehle | element_anlegen, element_aendern, element_loeschen, verbinden, trennen, leitung_ziehen – je mit Trockenlauf; Rückgängig und Wiederholen als neues Paket | `src/Hlks.Kern/Api/Befehle.cs`, `Projekt.cs` |
| Abfragen | 17, u. a. projekt_ueberblick, system_lesen, strang_text, netz_folgen, raum_inhalt, warum, aenderungen, datenpunkte, zeitreihe | `src/Hlks.Kern/Api/Abfragen.cs` |
| Ableitungen | Raumliste, Strangliste, Mengen – deterministisch, mit Modellstand, ohne Normwerte | `src/Hlks.Kern/Api/Ableitungen.cs` |
| Prüfungen | P01–P11 mit Vorschlag zur Behebung | `src/Hlks.Kern/Pruefung/Pruefungen.cs` |
| Ablage | Ordner wie auf dem NAS (`…\c_Pläne und Modelle\e_KICAD`): Änderungspakete nie überschrieben, Standabbilder, Reservierungen, Synchronisation zwischen Geräten mit einheitlichem Konfliktentscheid | `src/Hlks.Kern/Speicher` |
| Datenquelle | Schnittstelle für Mess- und Zählerwerte (nur lesend) mit deterministischem Simulator | `src/Hlks.Kern/Datenquellen` |
| Ereignisse | `Projekt.Geaendert` bei jeder eigenen und fremden Änderung (Grundlage für Live-Ansichten in 0b) | `Projekt.cs` |
| MCP-Server «hlks» | 26 Werkzeuge, 1:1 zu Befehlen und Abfragen; vor jedem Aufruf Synchronisieren; Fehler mit Vorschlag statt Absturz | `src/Hlks.Mcp` |
| Kommandozeile | `--beispiel`, `--abfrage`, `--befehl` (mit `--trockenlauf`), `--ki-auftrag`, `--ki-erwartung`, `--ki-bewerten` | `src/Hlks.Mcp` |
| Beispielprojekt | «MFH Musterweg» (fiktiv): 3 Geschosse, 13 Räume, Aussenwände, Schmutzwasser (2 Fallstränge, Sammel-, Grundleitung, Kontrollschacht) und Kaltwasser (Verteilleitung, 2 Steigstränge), 16 Apparate, Wasserzähler- und Leckage-Datenpunkte mit Verriegelung, 5 Ansichten – 175 Elemente, 138 Verbindungen, vollständig über Befehle erzeugt | `src/Hlks.Kern/Beispiel` |

## KI-Verständnistest

Eine fremde Claude-Instanz ohne Gesprächsverlauf, ohne CLAUDE.md und ohne Zugriff auf Dateien bekommt nur die
MCP-Werkzeuge und das Beispielprojekt. Die richtigen Antworten rechnet der Kern selbst
(`KiVerstaendnistest.Erwartung`), die Bewertung vergleicht automatisch.

| Frage | Sonnet 5.5 | Haiku 5.5 |
| --- | --- | --- |
| f1 Welche Endgeräte entwässern über SW-F2? | richtig | richtig |
| f2 Raum und Systeme von WT-E.02 | richtig | richtig |
| f3 Welche Endgeräte verlieren Kaltwasser, wenn MV-1 schliesst? | richtig | richtig |
| f4 Weg des Abwassers von WC-1.02 bis zum Kontrollschacht (Stränge in Reihenfolge) | richtig | richtig |
| f5 Meter Schmutzwasserleitung DN 100 | richtig | richtig |
| f6 Welcher Datenpunkt löst die Absperrung aus, wo wird gemessen? | richtig | richtig |
| f7 Was wäre offen, wenn WT-1.03 gelöscht würde (Trockenlauf)? | richtig | richtig |
| f8 Raumliste 1.OG | richtig | richtig |
| f9 Strangliste | richtig | richtig |
| f10 Räume im EG ohne Sanitäranschluss | richtig | richtig |
| **Total** | **10/10** in 18 s, 10 Runden | **10/10** in 30 s, 15 Runden |

Das Modell blieb in beiden Läufen unverändert (175 Pakete, keine Sperren übrig). Rohdaten: `ki-test/ergebnisse/`.
Wiederholen: `powershell -ExecutionPolicy Bypass -File ki-test\ki_test.ps1 -Modell claude-haiku-5-5`.

## Automatische Tests

| Bereich | Testklasse | Tests |
| --- | --- | --- |
| Befehle | BefehleTests | 56 |
| Trockenlauf, Rückgängig, Wiederholen, Ereignis | TrockenlaufUndoTests | 24 |
| Abfragen | AbfragenTests | 44 |
| Ableitungen | AbleitungenTests | 17 |
| Prüfungen P01–P11 | PruefungenTests | 46 |
| Schema | SchemaTests | 13 |
| Kataloge | KatalogTests | 20 |
| Simulator | SimulatorTests | 30 |
| Ablage und Synchronisation | SpeicherTests | 32 |
| Beispielprojekt, KI-Test-Erwartung, Rauchtest | BeispielTests, Rauchtest | 4 |
| MCP-Werkzeuge (inkl. Abdeckung «jede Funktion hat ihr Werkzeug») | WerkzeugTests | 17 |
| Kommandozeile (echte Prozesse, Umlaut-Pfad) | KommandozeileTests | 5 |
| MCP-Protokoll (JSON-RPC über stdio) | ProtokollTests | 2 |
| **Total** | | **310 bestanden, 0 übersprungen** |

Langsamste Tests: Kommandozeile (je rund 3 s, starten echte Prozesse). Öffnen des Beispiels mit Standabbild: unter 10 ms.

## Während des Baus gefunden und behoben

| # | Fund | gefunden von | Behebung |
| --- | --- | --- | --- |
| 1 | Zwei Geräte konnten bei gleichzeitiger Änderung desselben Elements auf verschiedenen Ständen landen | hlks-kern (Test) | Jedes Element trägt sein Herkunftspaket, Löschungen hinterlassen Vermerke; bei Konflikt gewinnt auf allen Geräten die neuere Änderung; Prüfsumme über den Inhalt |
| 2 | Synchronisieren ohne Netz oder mit kaputtem Paket warf Ausnahmen bzw. blockierte dauerhaft | hlks-kern | Fehler im Ergebnis, Paket bleibt für den nächsten Versuch offen |
| 3 | Reservieren und Freigeben ohne Netz warfen Ausnahmen | hlks-kern | Meldung «Projektablage nicht erreichbar» mit Vorschlag |
| 4 | Mehrfaches Rückgängig am selben Element wurde abgelehnt | hlks-modul (Test) | Inhalt statt Versionszähler vergleichen |
| 5 | Textansichten zeigten je nach Spracheinstellung «2,70» statt «2.70» | hlks-modul | einheitliches Zahlenformat |
| 6 | Filter mit Element der falschen Klasse lieferten still leere Listen | hlks-modul | klare Fehlermeldung «ist kein Geschoss/Raum/System» |
| 7 | Prüfsumme wich nach Rückgängig ab, obwohl der Inhalt gleich war | hlks-modul (MCP-Test) | Herkunftspaket nicht in der Prüfsumme |
| 8 | Ganzzahlen aus dem Programm wurden nicht als Zahl erkannt | Hauptlauf (Beispielaufbau) | einheitliche Zahlenlesung |
| 9 | Prüfungen P02–P11 waren nicht getestet | Hauptlauf (Gegenlesen) | 46 Tests ergänzt |

## Zuteilung von Modell und Denktiefe

| Agent | Aufträge | Nacharbeit |
| --- | --- | --- |
| hlks-kern (Opus, xhigh) | 1 (Ablage) | keine |
| hlks-modul (Sonnet, high) | 3 (Simulator + Tests, MCP-Server, Prüfungstests) | keine |
| hlks-test (Sonnet, medium) | 1 (Gesamtlauf, Berichtsentwurf) | keine |

Die gefundenen Fehler lagen im Kern des Hauptlaufs, nicht in den Lieferungen der Agenten. Nachjustieren: keine
Änderung, Herabstufen erst nach drei Aufträgen derselben Art ohne Nacharbeit. Protokoll: `zuteilung.md`.

## Bekannte Grenzen und offene Punkte

- Noch keine Oberfläche; die folgt in Modul 0b.
- Keine Normwerte. Die BKP-Zuordnung im Katalog ist vorläufig und von Christian zu prüfen.
- Die Ablage ist nur gegen einen lokalen Ordner mit NAS-Struktur getestet, nicht auf W:, weil die Sandbox von Claude kein SMB erlaubt. Den ersten Test auf dem NAS macht Modul 0b.
- Mac ungeprüft: Unicode-Schreibweise von «ä» in Pfaden und atomares Umbenennen auf SMB.
- Die Uhren der Geräte müssen synchron laufen (Ablauf der Reservierungen, Reihenfolge der Pakete).
- Ein Speichertest arbeitet mit festen Wartezeiten und könnte auf stark belasteten Rechnern wackeln.
- Über MCP lassen sich Textfelder wie Name, teilVon oder Raum nicht leeren; dafür kommt später ein Parameter «entfernen».
- P08 nennt bei fehlerhaften Verbindungen die Verbindungs-Id statt eines Kennzeichens.
- Der MCP-Server schreibt beim Beenden kein Standabbild, und liegengebliebene Temp-Dateien werden nicht aufgeräumt.

## Selbst ausprobieren (PowerShell)

Das Programm ist gebaut, ein Beispielprojekt liegt unter `%USERPROFILE%\HLKS_CAD_build\probe\`.

```powershell
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$dll = "$env:USERPROFILE\HLKS_CAD_build\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"
$p = (Get-ChildItem "$env:USERPROFILE\HLKS_CAD_build\probe" -Recurse -Directory -Filter e_KICAD).FullName
& "C:\Program Files\dotnet\dotnet.exe" $dll --projekt $p --abfrage strangliste
& "C:\Program Files\dotnet\dotnet.exe" $dll --projekt $p --abfrage strang_text "{strang:SW-F1}"
& "C:\Program Files\dotnet\dotnet.exe" $dll --projekt $p --abfrage netz_folgen "{von:WC-1.02,richtung:stromab}"
& "C:\Program Files\dotnet\dotnet.exe" $dll --projekt $p --abfrage warum "{element:SW-F1-2,eigenschaft:laenge}"
& "C:\Program Files\dotnet\dotnet.exe" $dll --projekt $p --befehl element_loeschen "{element:WT-1.03}" --trockenlauf
```
