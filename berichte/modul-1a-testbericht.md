# Testbericht Modul 1a – Grundriss und 2D

Stand 17.10.2026 · gebaut am 17.10.2026 (geplant 05.–18.11.2026) · **von Christian abgenommen am 17.10.2026**

## Ergebnis

Das Abnahmekriterium ist erfüllt. Selbst geprüft im Browser (Edge headless mit echter Maus und Tastatur) und am
gedruckten Plan:

| Kriterium | Nachweis |
| --- | --- |
| Wände, Türen und Fenster sind im Grundriss gezeichnet … | Browser-Testreihe 72/72: Wandkette mit Fangen und Schliessen, Zifferneingabe, Tür und Fenster setzen, Griffe (Wandende ziehen, Tür umdrehen), Verschieben/Kopieren/Drehen/Spiegeln. Bild `modul-1a/grundriss.png` |
| … und stehen gleichzeitig richtig im 3D | 3D aus den Körpern des Kerns: Wände, Tür, Fenster, Räume, Apparate, gezeichnete Wände am richtigen Ort. Bild `modul-1a/3d.png` |
| Ein Plan mit Bemassung ist als Planblatt gedruckt | Planblatt A1 mit H+K-Plankopf, Grundriss 1.OG (Bemassung in cm, Raumstempel, Achse, Schnittsymbol, Nordpfeil) und Schnitt 1 (geschnittene Wände schraffiert, Apparate und Leitungen in Ansicht, Koten). `modul-1a/abnahme-plan-1a-01.pdf` |
| Schnitt seitenrichtig | Schnitt Süd → Nord mit Blick nach Westen: Süden links, Norden rechts; Tür in der Südfassade an der richtigen Stelle. Bild `modul-1a/schnitt.png` |

Alle **583** automatischen C#-Tests, **84** JavaScript-Tests und **72** Browsertests bestehen, keiner übersprungen.

## Was gebaut ist

| Baustein | Inhalt | Ort |
| --- | --- | --- |
| Grafik aus dem Kern | Plangrafik-Primitive in Modell-mm, Stifte nach SIA 400 B.3.1, Ebenen, Körper für 3D; je Bereich eine Quelle (Architektur, HLKS, Beschriftung) | `src/Hlks.Kern/Grafik/` |
| Architektur | Wände (Bezugslinie, Ecken, Material-Schraffur nach SIA 400), Türen und Fenster in ihrer Wand (folgen ihr im selben Paket), Räume aus Wänden, Achsen; Befehle `wand_zeichnen`, `oeffnung_setzen`, `raum_erkennen`, `elemente_verschieben/kopieren/drehen/spiegeln` | `Grafik/Architektur*.cs`, `Architektur/` |
| HLKS-Grafik | Sinnbilder und Leitungen aus dem Kern statt aus JavaScript; Kanäle und Lüftungsrohre in echter Grösse; Leitungsbezeichnung «NW 50» (Vorgabe) bzw. «DN 50», «400×200», «Ø 250» | `Grafik/Hlks*.cs`, `Leitungsbezeichnung.cs` |
| Beschriftung | Bemassung (Einheit je Gewerk: Architektur m, Lüftung mm, Heizung/Sanitär cm, einstellbar), Höhenkote, Text, Detaillinie, Füllbereich, Nordpfeil, Schnittsymbol | `Grafik/Beschriftung*.cs`, `Bemassungseinheit.cs` |
| Schnitt und Ansicht | aus den Körpern: Schnittflächen schraffiert, Sichtkanten dünn, Verdeckung über Sichtkanten; Blick links/rechts seitenrichtig | `Grafik/Schnitt*.cs` |
| Planblatt und PDF | Planblatt A4–A0, Ansichtsfenster, H+K-Plankopf mit Logo, PDF über PDFsharp 6.2.4 (MIT); Befehle `planblatt_anlegen`, `ansicht_platzieren`, Abfrage `plan_pdf` | `src/Hlks.Kern/Plan/` |
| Oberfläche | zeichnet die Kern-Grafik (Plan-Ansicht, 3D, Planblatt), Revit-Dichte und dunkles Design (umschaltbar), Zeichenwerkzeuge mit Fangen (Endpunkt › Schnittpunkt › Mitte › Lot › Nächster › Raster), Shift orthogonal, Zifferneingabe, Griffe, Register Beschriften, Kürzel WA, DR, WN, RM, GR, DI, TX, DL, EL, MV, CO, RO, MM | `wwwroot/js/ansichten/`, `js/werkzeuge/`, `OBERFLAECHE.md` |
| KI-Werkzeuge | 36 MCP-Werkzeuge, eines je Befehl und Abfrage (vorher 26) | `src/Hlks.Mcp/Werkzeuge*.cs` |
| REST | `/api/plangrafik/{ansicht}`, `/api/koerper`, `/api/schraffuren`, `/api/plan/{planblatt}.pdf` | `src/Hlks.Web/Program.cs` |

## Automatische Tests

| Bereich | Testklassen | Tests |
| --- | --- | --- |
| Kern aus 0a/0b (teils angepasst) | 14 Testklassen | 303 |
| Architektur (Befehle, Grafik, Nachführung) | ArchitekturBefehleTests, ArchitekturGrafikTests | 44 |
| HLKS-Grafik | HlksGrafikTests | 55 |
| Beschriftung | BeschriftungTests | 26 |
| Einheiten, Leitungsbezeichnung | BemassungseinheitTests, LeitungsbezeichnungTests | 13 |
| Planblatt, PDF | PlanTests | 33 |
| Schnitt und Ansicht | SchnittTests | 16 |
| MCP-Server und Kommandozeile | WerkzeugTests, KommandozeileTests, ProtokollTests | 24 |
| REST, Live-Abgleich, Rauchtest Web | ApiTests, AbgleichTests, Rauchtest | 69 |
| Ansichten und Werkzeuge (Node, inkl. 3 Tests gegen den laufenden Server) | ansichten.test.mjs, werkzeuge.test.mjs | 84 |
| **Total automatisch** | | **667 bestanden, 0 übersprungen** |
| Browser (Edge headless, echte Maus/Tastatur, nicht Teil von `node --test`) | werkzeuge.browser.mjs | 72 bestanden |

## Während des Baus gefunden und behoben

| # | Fund | gefunden von | Behebung |
| --- | --- | --- | --- |
| 1 | Löschen einer Wand liess ihre Türen und Fenster stehen | hlks-kern (B1) | Öffnungen gehen mit ihrer Wand |
| 2 | Bezugsprüfung ohne Lösungsvorschlag | hlks-kern (B1) | Vorschlag ergänzt |
| 3 | Begrenzung von Bögen als Vollkreis gerechnet | Hauptlauf | exakte Bogengrenzen |
| 4 | `Ebenen.Rang` warf einen Fehler (falscher Cast) | hlks-modul (B4) | Nachschlagetabelle |
| 5 | PDFsharp 6.5 steht unter AGPL und zieht eine Bibliothek mit Sicherheitslücken nach | hlks-modul (B4) | PDFsharp 6.2.4 (MIT), Pkcs 10.0.13 |
| 6 | Füllbereich verdeckte Linien | hlks-modul (B3) | eigene Ebene `fuellung` unter den Linien |
| 7 | 3D ohne Materialfarben, Räume ohne Farbe, Tür schwer zu treffen | hlks-modul (C2) | Farben je Material/Nutzung, Trefferfläche |
| 8 | **Schnitt mit Blick nach rechts war seitenverkehrt** (Fehler in der Spezifikation) | hlks-kern (C1) | Blick rechts läuft von B nach A; Spezifikation korrigiert |
| 9 | Schnitt-Leistungstest wackelte unter Last | Hauptlauf | Median über mehrere Läufe |
| 10 | Testserver lief unbemerkt mit altem Build | Hauptlauf (Abnahmeprüfung) | `start.ps1` baut neu, sobald eine C#-Quelle neuer ist als der Build |

## Zuteilung von Modell und Denktiefe

| Agent | Aufträge | Nacharbeit |
| --- | --- | --- |
| hlks-kern (Opus, xhigh) | 2 (B1 Architektur, C1 Schnitt) | keine – der Schnittbefund lag in meiner Spezifikation |
| hlks-modul (Sonnet, high) | 6 (Revit-Dichte, B2 HLKS-Grafik, B3 Beschriftung, B4 Planblatt/PDF, C2 Oberfläche zeichnet Kern-Grafik, C3 Zeichenwerkzeuge) | eine leichte (C3: Platzhalter, Importe, passives Werkzeug aufgeräumt) |

**Nicht nachjustiert, mit Begründung:** Nach der Regel ginge «Routinebau nach Muster» (B2, B3, B4 je ohne Nacharbeit)
probeweise eine Stufe tiefer zu hlks-mechanik (Haiku, low). Das setze ich nicht um: hlks-mechanik ist für Arbeit «nur
aus mitgegebener Quelle» ohne eigene Entscheide gedacht, und B2–B4 enthielten solche Entscheide (Schriftgrössen,
PDF-Bibliothek samt Lizenz). Oberfläche bleibt bei hlks-modul (eine Nacharbeit), Geometrie bei hlks-kern (zweimal ohne
Nacharbeit). Willst du die Herabstufung trotzdem probieren, sag es. Protokoll: `zuteilung.md`.

## Annahmen zum Prüfen (Prüfliste)

1. Schnittfläche einer Wand ohne Material: grau 25 %.
2. Schriftgrössen von Raumstempel, Bemassung, Koten und Ansichtstitel.
3. Kotensymbol (Dreieck mit Wert) und Masse von Nordpfeil, Achskreis und Schnittsymbol.
4. Fensterbank: Seite links der Wandrichtung gilt als aussen.
5. Schraffuren Porenbeton, Erdreich und Kies (nicht in SIA 400 Fig. 34, von mir angenommen).
6. Bemassungseinheit Kälte cm, Elektro m, MSRL m (für Architektur, Lüftung, Heizung, Sanitär hast du entschieden).
7. Schnitt: abgesetzte Schnittlinien gelten vorerst als gerade Linie vom ersten zum letzten Punkt; ohne `tiefe` reicht
   die Sicht bis ans Modellende; ohne Zuschnitt zeigt die Ansicht das ganze Modell entlang der Linie.

## Bekannte Grenzen und offene Punkte

- Planblatt: Die automatische Lage setzt die erste Ansicht in die Blattmitte; für eine zweite bleibt dann oft kein
  Platz (es kommt ein Hinweis, Lage von Hand). Ansichten ohne Zuschnitt können über den Blattrand ragen.
- Schnitt: keine Durchdringungslinien; Decken erscheinen erst mit Modul 1c (Hülle); Leichtbau ohne Schraffur; die
  Nachbarsuche wächst quadratisch (bei grossen Modellen später beschleunigen).
- Grundriss: Bauteile oberhalb der Schnitthöhe fehlen (keine gestrichelte Darstellung); Ecken ziehen die Nachbarwand
  beim Verschieben nicht mit; keine Vorschau beim Raum-Werkzeug.
- HLKS: Geräte ohne eigene Masse erscheinen als Würfel, der Fussbodenheizkreis als Kasten (richtige Formen mit den
  Familien in Modul 2).
- Kein Kürzel für Füllbereich, Nordpfeil und Schnitt (Knöpfe im Menüband vorhanden).
- Der eingebaute PDF-Betrachter des Browsers ignoriert die Vorgabe «ganze Seite» beim Öffnen.
- Der Test `PaketLesenWartetAufGesperrteDatei` ist zeitabhängig und kann unter starker Last wackeln.
- Plankopf: «Projekt» und «Projektphase» sind im Beispiel leer (Projektinformation nicht ausgefüllt; später aus HKAP,
  Modul 1d).

## Selbst ausprobieren

Testserver: http://localhost:5131 (Probeprojekt). Programmfenster mit dem NAS-Testprojekt (läuft auf Port 5130):

```powershell
powershell -ExecutionPolicy Bypass -File "$env:OneDrive\Claude_Code\HLKS_CAD\werkzeuge\start.ps1" -Browser -Projekt "W:\02_Projektdateien Objekte\_Test HLKS-CAD\c_Pläne und Modelle\e_KICAD"
```

Zum Ausprobieren: Architektur › Wand (WA) zwei Punkte klicken, Esc · Tür (DR) auf eine Wand · Beschriften › Bemassung
(DI) zwei Punkte und Lage · Ansicht › Schnitt zwei Punkte und Blickseite · Ansicht › Planblätter: Planblatt
anlegen, Ansicht platzieren, PDF.
