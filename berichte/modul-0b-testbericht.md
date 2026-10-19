# Testbericht Modul 0b – Arbeitsplatz, Projektexplorer, Ansichten, Live-Abgleich

Stand 17.10.2026 · gebaut am 17.10.2026 (geplant 26.10.–04.11.2026) · **von Christian abgenommen am 17.10.2026**

## Ergebnis

Beide Abnahmekriterien sind erfüllt. Selbst im Browser geprüft und auf dem NAS gemessen:

| Kriterium | Nachweis |
| --- | --- |
| Menüband, Projektbrowser und Eigenschaftenpalette bedienen sich wie in Revit | Register Datei/Architektur/Gebäudetechnik/Ändern/Ansicht/Verwalten mit kontextbezogenem Register «Ändern \| …», Schnellzugriff, Optionsleiste, Tastaturkürzel aus zwei Buchstaben (VV, WT, PP, MD, DE …), Projektbrowser mit Ansichten, Vorlagen, Gebäude, Systemen und Familien, Eigenschaftenpalette mit Typauswahl, Gruppen und «Warum?» je Wert |
| Eine Änderung erscheint sofort in allen offenen Ansichten | Name von Raum 1.02 in der Eigenschaftenpalette geändert: Grundriss, 3D und Raumliste nach **rund 125 ms** nachgeführt (gemessen im Browser) |
| … und nach wenigen Sekunden auf einem zweiten PC | NAS-Test: Änderung eines zweiten Arbeitsplatzes (eigener Prozess) auf W: nach **0,31–0,36 s** ab Paket sichtbar, Median 0,32 s, 8 von 8 angekommen. Grenze 3 s. Lokal 4–46 ms |
| Projekt mit drei Geschossen im NAS-Testordner angelegt, synchronisiert und wieder geöffnet | Beispielprojekt (UG, EG, 1.OG) in `W:\02_Projektdateien Objekte\_Test HLKS-CAD\c_Pläne und Modelle\e_KICAD` angelegt, von zwei Arbeitsplätzen bearbeitet, geschlossen und wieder geöffnet |
| Ansichten im Explorer anlegen, filtern, als Vorlage speichern | Raumliste über Ansicht › Liste angelegt; über Sichtbarkeit/Grafiken (VV) Klasse Rohr ausgeblendet: 26 Rohre im 1.OG verschwinden, 56 → 30 Elemente; «Als Vorlage speichern» legt `VL-GR-1.OG` mit allen Einstellungen an, erscheint unter Ansichtsvorlagen |

Alle **396** automatischen Tests (C#) und **44** Ansichtstests (JavaScript) bestehen, keiner ist übersprungen.

## Was gebaut ist

| Baustein | Inhalt | Ort |
| --- | --- | --- |
| Arbeitsplatz-Server | ASP.NET Core nur auf localhost; REST als dünne Hülle über die Kern-Tür (`Tuer.Abfrage`, `Tuer.Befehl`) – Oberfläche und KI benutzen dieselben Befehle | `src/Hlks.Web/Program.cs`, `src/Hlks.Kern/Api/Tuer.cs` |
| Darstellung | Ein Element für alle Ansichten: Kennzeichen, Klasse, Schicht, Gewerk, Medium mit Farbe nach SIA 410/1, Geometrie, Werte, Bezüge | `src/Hlks.Kern/Api/Darstellung.cs` |
| Live-Abgleich | Ereignisstrom (SSE) an alle Fenster; Dateiwächter auf `aenderungen` plus Takt 2 s; Wachhund 10 s; Abgleich in Etappen ausserhalb der Sperre (Oberfläche blockiert nie); Meldung nur beim Wechsel getrennt/verbunden; Sammelereignis bei vielen Paketen; Standabbild ab 200 neuen Paketen und beim Schliessen | `src/Hlks.Web/ProjektDienst.cs`, `AbgleichDienst.cs` |
| Oberfläche wie Revit | Menüband, Schnellzugriff, Optionsleiste, Projektbrowser, Eigenschaftenpalette, Statusleiste (Abgleich, Reservierungen, Stand), Dialoge, Startdialog «Zuletzt geöffnet», Kürzel | `src/Hlks.Web/wwwroot/js/oberflaeche/` |
| Ansichten | Grundriss (SVG, Massstab, Detailgrad, Schnitthöhe, SIA-Darstellung), 3D (three.js, Orbit), Liste (Raumliste, Strangliste, Mengen); nebeneinander (WT) oder als Reiter; Sichtbarkeit/Grafiken; Ansichtsvorlagen | `src/Hlks.Web/wwwroot/js/ansichten/`, `fenster.js` |
| Start als Programmfenster | `werkzeuge\start.ps1 -Browser`: eigenes Fenster ohne Adressleiste im Vollbild (Edge, sonst Chrome; F11 = Fenster, `-Fenster` = maximiert) | `werkzeuge/start.ps1` |
| NAS-Test | Misst Anlegen, Öffnen mit/ohne Standabbild, fremde Änderung, Dateiwächter über SMB, Reservierungskonflikt, Schliessen | `werkzeuge/nas_test.ps1`, `berichte/nas-test-20261017-185530.json` |
| Pflichtenheft | Verträge zwischen Server, Zustand, Ansichten und Oberfläche | `src/Hlks.Web/OBERFLAECHE.md` |

## Messungen auf dem NAS (W:, über VPN, 17.10.2026 18:55)

| Messung | Wert |
| --- | --- |
| Fremde Änderung bis Anzeige, ab Paket | 301 / 316 / 356 ms (min / median / max), 8 Durchgänge, 0 verpasst |
| davon nach dem Schreiben des Pakets | 0 / 4 / 17 ms – die übrige Zeit ist das Schreiben über VPN, nicht das Erkennen |
| Dateiwächter über SMB | meldet zuverlässig (8 von 8), der 2-s-Takt ist nur Rückfallebene |
| Öffnen mit Standabbild | 110–126 ms |
| Öffnen ohne Standabbild (175 Pakete) | 4,2 s beim ersten Mal, 0,5 s danach |
| Arbeitsplatz starten bis Projekt offen | 0,7 s |
| Ein Befehl über die Kommandozeile (inkl. Programmstart) | 0,55–0,70 s |
| Beispielprojekt anlegen (175 Befehle = 175 Pakete) | 51 s |
| Reservierung | A ändert → B abgelehnt («NAS-A@nas-a bis 20:56») → A gibt frei → B angenommen, A zeigt es |
| Schliessen | 98 ms, Reservierungen frei |

Das langsame Anlegen liegt daran, dass das Beispiel jedes Element mit einem eigenen Befehl und Paket schreibt (rund
0,3 s je Paket über VPN). Im Alltag ist das unkritisch. Für Importe (IFC in Modul 1b) gilt deshalb: ein Import = ein
Paket mit vielen Operationen.

## Automatische Tests

| Bereich | Testklasse | Tests |
| --- | --- | --- |
| Kern (aus 0a, angepasst) | 11 Testklassen | 286 |
| Tür und Darstellung | TuerDarstellungTests | 17 |
| MCP-Server und Kommandozeile | WerkzeugTests, KommandozeileTests, ProtokollTests | 24 |
| REST-Schnittstelle (inkl. «nie Fehler 500 bei fachlichen Fehlern») | ApiTests | 42 |
| Live-Abgleich (zweiter Arbeitsplatz, Kommandozeile als KI, Ablage weg/hängend/kaputt, Ereignisstrom) | AbgleichTests | 26 |
| Rauchtest Web | Rauchtest | 1 |
| Ansichten (Geometrie, Sinnbilder, Filter, Listenformat) | ansichten.test.mjs (Node) | 44 |
| **Total** | | **440 bestanden, 0 übersprungen** |

## Während des Baus gefunden und behoben

| # | Fund | gefunden von | Behebung |
| --- | --- | --- | --- |
| 1 | Öffnen eines Ordners ohne Projekt gab keinen Vorschlag | hlks-modul (API-Test) | Meldung mit Vorschlag |
| 2 | Leerer Ordnername ergab Fehler 500 | hlks-modul (API-Test) | Fehler 400 mit Text |
| 3 | Ein fehlgeschlagenes Öffnen schloss das offene Projekt | hlks-modul (API-Test) | erst das neue öffnen, dann ersetzen |
| 4 | Element-Ids, die kein Text sind, ergaben Fehler 500 | hlks-modul (API-Test) | Fehler 400 mit Text |
| 5 | Eine fremde Datei mit unzulässigem Namen in `aenderungen` brach Öffnen und Synchronisieren ab | hlks-kern (Test) | als unlesbar melden und überspringen |
| 6 | Ein vertippter Projektpfad legte auf dem NAS leere Ordner an | hlks-kern (Test) | Ordner entstehen nur noch beim Anlegen eines Projekts |
| 7 | Nach «Beispielprojekt anlegen» waren 56 Elemente zwei Stunden reserviert | hlks-kern (Test) | Generator gibt am Ende frei |
| 8 | Nach «Projekt schliessen» blieben die Familien im Projektbrowser | hlks-modul (gemeldet) | Zustand wird ganz geleert |
| 9 | Projektwechsel aus einem anderen Fenster oder von der KI lud nur halb nach; Startdialog blockierte weitere Ereignisse | Hauptlauf (Browser) | vollständig neu laden, Startdialog von aussen schliessbar |
| 10 | Nach einer Unterbrechung des Ereignisstroms fehlten die Änderungen dazwischen | hlks-kern (gemeldet) | beim Wiederverbinden alles neu laden |
| 11 | Wartende im Ereignisstrom stauten sich alle 15 s | hlks-kern (gemeldet) | ein Warten über die Runden |
| 12 | Testläufe füllten die Liste «Zuletzt geöffnet» | hlks-modul (gemeldet) | Tests schalten sie ab, Pfade unter %TEMP% werden nie gemerkt |
| 13 | OrbitControls am falschen Pfad | hlks-modul | verschoben, Import statisch |

Gemeldet, aber kein Fehler: Rückgängig im Trockenlauf – wirkt über REST richtig.

## Zuteilung von Modell und Denktiefe

| Agent | Aufträge | Nacharbeit |
| --- | --- | --- |
| hlks-kern (Opus, xhigh) | 1 (Live-Abgleich härten, Tests, NAS-Testskript) | keine |
| hlks-modul (Sonnet, high) | 3 (Oberfläche, Ansichten, API-Tests) | keine |

**Nachjustiert:** «Testfälle zu bestehendem Code schreiben» lief dreimal ohne Nacharbeit (0a Befehle/Abfragen, 0a
P01–P11, 0b API). Nach der Regel geht diese Aufgabenart **probeweise eine Stufe tiefer, von hlks-modul (Sonnet/high)
zu hlks-test (Sonnet/medium)**. hlks-test darf dafür neu unter `tests/` schreiben, nie in den Produktcode. Rückweg bei
zwei Nacharbeiten. Geändert: Tabelle in `CLAUDE.md`, `.claude/agents/hlks-test.md`, `hlks-modul.md`. Protokoll:
`zuteilung.md`.

## Bekannte Grenzen und offene Punkte

- **Zweiter echter PC noch nicht geprüft.** Gemessen ist ein zweiter Arbeitsplatz als eigener Prozess auf demselben
  Rechner über W:. Der Testordner auf W: bleibt dafür stehen (siehe unten).
- Bearbeiten in den Ansichten selbst (zeichnen, verschieben, Leitungen ziehen) folgt mit Modul 1a; in 0b ändert man
  über Eigenschaftenpalette, Befehle und KI.
- Schnitt und Ansicht zeigen einen Platzhalter (Modul 1a), das Schema ebenso (Modul 3).
- Die Statusleiste fragt `/api/status` alle 3 s ab (lokal, billig); später über den Ereignisstrom.
- Bei jeder Änderung lädt die Oberfläche die Verbindungen des ganzen Modells nach – bei grossen Modellen später gezielt.
- Die Prüfsumme wird bei jeder Statusabfrage über das ganze Modell gerechnet; bei grossen Modellen zwischenspeichern.
- Der Ordner `aenderungen` wächst unbegrenzt; Archivieren alter Pakete folgt später.
- Solange ein Arbeitsplatz ein Projekt offen hat, lässt sich der Projektordner auf dem NAS nicht umbenennen
  (der Dateiwächter hält ihn).
- Mac ungeprüft (Fenster-Start nur für Windows gebaut, Pfade mit «ä», SMB).
- Die Uhren der Geräte müssen synchron laufen (Paketreihenfolge, Ablauf der Reservierungen).
- Schwellen und Zeiten (Standabbild ab 200 Paketen, Wachhund 10 s, Takt 2 s usw.) sind gewählt, nicht gemessen
  begründet; alle in `Einstellungen` anpassbar.

## Selbst ausprobieren

Programmfenster mit dem NAS-Testprojekt (auf diesem oder einem zweiten PC mit .NET 10 SDK; baut beim ersten Mal selbst):

```powershell
powershell -ExecutionPolicy Bypass -File "$env:OneDrive\Claude_Code\HLKS_CAD\werkzeuge\start.ps1" -Browser -Projekt "W:\02_Projektdateien Objekte\_Test HLKS-CAD\c_Pläne und Modelle\e_KICAD"
```

Auf zwei PCs gleichzeitig geöffnet zeigt jede Änderung des einen nach Bruchteilen einer Sekunde auch auf dem anderen
(Statusleiste «Änderung von …»). Ein Element, das der eine geändert hat, ist für den anderen bis «Verwalten ›
Meine Elemente freigeben» oder bis zum Schliessen gesperrt – wie das Ausleihen in Revit.

Nützliche Kürzel: WT Fenster nebeneinander · VV Sichtbarkeit/Grafiken · PP Eigenschaften · MD Ändern · DE Löschen ·
Strg+Z / Strg+Y · F11 Vollbild ein/aus.
