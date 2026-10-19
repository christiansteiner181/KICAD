# Testbericht Modul 1b – Grundlagen, Räume, openBIM, IFC-Übertrag

Stand 18.10.2026 · gebaut am 17./18.10.2026 (geplant 19.11.–04.12.2026) · **von Christian abgenommen am 18.10.2026**

## Ergebnis

Die sieben Abnahmekriterien der Spezifikation (`entwurf/modul-1b.md`) sind mit den echten Dateien von Bernstrasse 42
und BKW Hauptsitz geprüft. Sechs sind ohne Einschränkung erfüllt. Kriterium 1 ist technisch erfüllt, zeigt aber einen
**Befund in den Bernstrasse-Daten**: Das H+K-IFC liegt nicht auf dem Architekten-IFC (siehe «Wichtigster Befund»).

| Kriterium | Nachweis |
| --- | --- |
| 1 Bernstrasse mit Räumen | 49 Räume automatisch aus den 389 Archicad-Wänden, 37 Räume aus dem H+K-IFC (Nummer, Name, lichte Höhe). Liegt ein echter Raum über einem abgeleiteten, übernimmt er dessen Id und Bezüge (mit Testdateien belegt). Bei Bernstrasse liegen die beiden Raumgruppen aber rund 30 m auseinander, weil die Dateien keine gemeinsamen Koordinaten haben |
| 2 Architektur als echte Elemente | Bernstrasse (Archicad 27, IFC2x3): 389 Wände, 73 Decken, 11 Dächer, 51 Stützen, 73 Unterzüge, 57 Türen und 15 Fenster in ihrer Wand. BKW (Archicad 28, IFC4): 2412 Wände, 753 Decken, 112 Dächer, 691 Stützen, 212 Unterzüge, 297 Türen, 610 Fenster, 498 Räume aus Wänden. Alles mit GlobalId, die exakte Geometrie aller Produkte in der Netzdatei (Bernstrasse 1690, BKW 889 weitere Objekte als Hintergrund) |
| 3 Fachplaner-IFC übernommen | BKW Nova Heizung/Kälte: 1521 Rohre, 978 Bögen, 712 Reduktionen, 218 T-Stücke, 238 Ventile, 32 Pumpen, 5 Wärmeerzeuger, 13 Wasserzähler, 3466 Verbindungen, 4 Systeme. Sanitär: 99 Rohre, 176 Verbindungen, 5 Systeme. Lüftung: 75 Kanäle, 126 Verbindungen, 4 Systeme. Bernstrasse H+K (Revit): 563 Rohre, 264 Kanäle, 493 Bögen, 1440 Verbindungen, 12 Systeme, 10 WC, 10 Waschtische |
| 4 IFC4 und IFC2x3 | alle 7 echten Dateien (4× IFC4, 3× IFC2x3) gelesen und übernommen; `kataloge/ifc.json` gegen die Schemaklassen beider Fassungen geprüft (973 Testfälle). IFC 4.3 und IFC2x2 werden mit Meldung abgelehnt |
| 5 Neue Fassung gleicht ab | Bernstrasse H+K Fassung 2024-09 → 2026-02 unter gleichem Namen: 2655 neu, 796 geändert, 535 gelöscht, 282 unverändert – ein Paket |
| 6 DWG, DXF, PDF, Bild als Unterlage | DWG direkt mit ACadSharp (Architekt EG 0.2 s), PDF-Seite und PNG kalibriert im Grundriss, im Browser geprüft (Bilder unten) |
| 7 Ausrichtung transparent | Lagebericht je Datei, nie eine stille Verschiebung. Jede Fachplaner-Datei wurde ohne Wahl abgelehnt, mit Zahlen und neu mit **Inhaltsvergleich**. BKW Nova: Ursprung 14.3 m / 176.9 m / −0.015° daneben, Inhalt mit «projekt» 97 % (Heizung), 85 % (Sanitär) und 89 % (Lüftung) im Archicad-Modell, mit «gebaeude» 0 %, daher Empfehlung «projekt». Bernstrasse: 0 % mit beiden Wahlen, darum der Hinweis auf Koordinationskörper oder ausdrücklichen Bezug |

## Wichtigster Befund: Bernstrasse H+K-IFC liegt neben dem Architekten-IFC

Beide Dateien haben ihren Gebäudeursprung bei Ost 0 / Nord 0. Der Ursprungsvergleich meldete deshalb nur «+583.690 m
Höhe». Der Inhalt des H+K-IFC (Revit) liegt aber rund **30 m östlich und 20 m nördlich** des Archicad-Inhalts und
überdeckt ihn nicht. Genau das ist das Revit-Problem, das du beschrieben hast: Die Dateien teilen keine Koordinaten,
und ein Vergleich der Ursprünge allein zeigt es nicht.

**Behoben in KICAD:** Die Prüfung vergleicht jetzt zusätzlich den Inhalt (Stichprobe von bis zu 400 Bauteilkörpern)
mit dem bestehenden Modell, je für «projekt» und «gebaeude». Beruht die Lage nur auf dem Gebäudeursprung und liegt
weniger als 10 % des Inhalts im Modell, wird abgelehnt («ACHTUNG Inhalt …»), auch wenn der Ursprung passt. Trifft eine
Wahl klar (≥ 80 %), nennt der Bericht sie als Empfehlung; gewählt wird trotzdem ausdrücklich. Koordinationskörper und
Landeskoordinaten gelten weiter als Abmachung.

**Offen (deine Entscheidung):** Für Bernstrasse fehlt ein gemeinsamer Bezug. Entweder liefert das H+K-Modell
«Gemeinsame Koordinaten» bzw. einen Koordinationskörper, oder KICAD bekommt ein Werkzeug «IFC über zwei Punktpaare
ausrichten» (zwei Punkte im Architekten-Modell, zwei in der Fachplaner-Datei, daraus der Bezug mit Drehung). Ich schlage
das Werkzeug für Modul 1c vor; heute geht es nur über den Parameter `bezug` mit Zahlen.

Alle **1871** automatischen C#-Tests (Kern 1758, Web 88, MCP 25), **127** JavaScript-Tests und **154** Browsertests bestehen.

> **Hinweis zur Messung:** Der Rechner lief während der Tests gedrosselt, mit etwa 24 % der Prozessorleistung (Windows-Zähler
> «Prozessorleistung»; vermutlich Wärme nach den langen Läufen, Energieschema «Ausbalanciert», am Netz). Im Gesamtlauf mit allen drei
> Testpaketen gleichzeitig rissen deshalb drei Zeitgrenzen (Dateisperre, Nova-Übertrag 92 s, `ifc_pruefen` 117 s). Einzeln
> nacheinander bestanden alle zeitkritischen Tests (zuletzt 40 von 40, Nova 15 s). Alle Zeiten in diesem Bericht stammen vom gedrosselten Rechner.

## Was gebaut ist

| Baustein | Inhalt | Ort |
| --- | --- | --- |
| Fundament | xBIM Essentials 6.1.605 (CDDL-1.0), ACadSharp 3.8.0 (MIT), Docnet.Core 2.6.0 (MIT); Klasse `Unterlage` und `Element.Kennungen` (GlobalId, Quelle); Ablage `unterlagen` mit Prüfsumme; Übernahme als **ein Paket** mit Abgleich über die GlobalId (neu, geändert, gelöscht; Konflikte: KICAD gewinnt, wählbar) | `src/Hlks.Kern/Import/` |
| openBIM-Abbildung | jede Katalogklasse ↔ IFC4 und IFC2x3 (Entität, Typobjekt, PredefinedType, ObjectType), Property-Set-Kandidaten, Systemtypen ↔ IfcDistributionSystemEnum mit Namensmustern | `kataloge/ifc.json`, `Kataloge/IfcAbbildung.cs` |
| Ausrichtung transparent | Projektbezug, Lagebericht je Datei, Prüfung Koordinationskörper › Landeskoordinaten › Gebäudeursprung, Toleranz 10 mm / 0.01°, dazu Inhaltsvergleich mit dem bestehenden Modell (Anteil je Wahl, Empfehlung), Wahl `projekt`, `gebaeude`, `koordinationskoerper` | `IfcAusrichtung.cs` |
| Architektur aus IFC | Wände, Decken, Dächer, Stützen, Unterzüge, Türen und Fenster in ihrer Wand, Achsen als echte KICAD-Elemente; Räume aus IfcSpace oder **automatisch aus den Wänden** | `IfcArchitektur.cs`, `IfcRaeume.cs` |
| Exakte Geometrie | Netzdatei je IFC-Unterlage mit allen Produkten (übernommen und Hintergrund), Grundlage für Kollision und Aussparungen (Modul 3b) | `IfcGeometrie*.cs`, `IfcNetz.cs` |
| HLKS aus IFC | Leitungen, Formteile, Armaturen, Apparate mit Anschlüssen, Verbindungen und Systemen; Nennweite nur aus Psets, nie aus dem Aussendurchmesser | `IfcHlks*.cs` |
| Unterlagen 2D | DWG/DXF direkt, PDF-Seite und Bild als kalibrierte Unterlage je Geschoss, im Plan-PDF | `src/Hlks.Kern/Grundlagen/` |
| Oberfläche | Register «Einfügen»: Hochladen, IFC-Dialog mit Lagebericht und Ausrichtungswahl, Kalibrieren, Unterlagen im Projektbrowser; Rasterbild im Grundriss, IFC-Hintergrund im 3D (eine Geometrie je Unterlage) | `wwwroot/js/`, `OBERFLAECHE.md` |
| KI-Werkzeuge und REST | `ifc_pruefen`, `ifc_uebernehmen`, `unterlage_einfuegen`, `unterlage_kalibrieren`; `POST /api/unterlagen`, `GET /api/unterlagen/{id}/netz` und `/bild` | `src/Hlks.Mcp/`, `src/Hlks.Web/Program.cs` |

## Abnahmelauf mit den echten Dateien

`Abnahme1bTests` mit `HLKS_ABNAHME=1`, frisches Projekt je Gebäude, Zeiten auf dem gedrosselten Rechner (18.10.2026):

| Datei | Programm, Fassung | Ergebnis | Dauer |
| --- | --- | --- | --- |
| Bernstrasse `architekt_3d.ifc` (106 MB) | Archicad 27, IFC2x3 | erste Datei, setzt den Projektbezug; 718 Elemente, 49 Räume aus Wänden | 48 s |
| Bernstrasse H+K 2024-09 | Revit 2024, IFC2x3 | ohne Wahl abgelehnt (Ursprung +583.69 m Höhe; Inhalt 0 % / 0 %, rund 30 m Ost, 21 m Nord daneben); mit «projekt» zum Prüfen übernommen: 1613 Elemente, 1440 Verbindungen | 6 s / 10 s |
| Bernstrasse H+K 2026-02 (115 MB), gleicher Name | Revit 2025, IFC2x3 | Abgleich: 2655 neu, 796 geändert, 535 gelöscht, 282 unverändert | 55 s |
| Bernstrasse DWG Architekt EG | AutoCAD | Unterlage im EG | 4 s |
| BKW `ARCH_ARC.ifc` (90 MB) | Archicad 28, IFC4 | erste Datei; 5585 Elemente, 498 Räume aus Wänden | 99 s |
| BKW Nova HK | Nova 18.2, IFC4 | ohne Wahl abgelehnt, Empfehlung «projekt» (97 %); mit «projekt»: 3729 Elemente, 3466 Verbindungen | 10 s / 20 s |
| BKW Nova SA | Nova 18.2, IFC4 | wie HK (85 %); 212 Elemente, 176 Verbindungen | 2 s / 3 s |
| BKW Nova LU | Nova 18.2, IFC4 | wie HK (89 %); 158 Elemente, 126 Verbindungen | 2 s / 2 s |

Grafik danach: BKW 9667 Körper in **1.0 s** (vor der Behebung 32.5 s), Bernstrasse 4428 Körper in 0.3 s; Grundriss je
Geschoss 0.7–5 s.

## Automatische Tests

| Bereich | Testklassen | Tests |
| --- | --- | --- |
| Kern aus 0a–1a (teils angepasst) | | 490 |
| openBIM-Abbildung gegen die Schemaklassen | IfcAbbildungTests | 973 |
| Ausrichtung (mit Inhaltsvergleich), Matrix | IfcAusrichtungTests, MatrixTests | 93 |
| Fundament (Fassung, Geschosse, Abgleich, Ablage, Einheiten) | IfcGrundgeruestTests | 9 |
| Architektur, Netz, Räume aus IFC (mit Übernahme abgeleiteter Räume) | IfcArchitektur*, IfcNetzTests, IfcRaeumeTests | 58 |
| HLKS aus IFC | IfcHlksTests, IfcHlksEchtTests | 38 |
| Unterlagen DWG/DXF/PDF/Bild | Grundlagen*Tests | 55 |
| Echte Dateien (Rauchtests) | IfcEchtRauchTests | 36 |
| Befunde B1 (Brüstung, Hinweise, Zwischenspeicher) | ArchitekturB1BefundeTests | 5 |
| Abnahmelauf (nur mit HLKS_ABNAHME=1, sonst sofort fertig) | Abnahme1bTests | 1 |
| MCP-Server und Kommandozeile | WerkzeugTests u. a. | 25 |
| REST, Unterlagen-API, Live-Abgleich | ApiTests, UnterlagenApiTests u. a. | 88 |
| **Total C#** | | **1871 bestanden** |
| Ansichten, Werkzeuge, Einfügen (Node, inkl. Tests gegen den laufenden Server) | ansichten.test.mjs, werkzeuge.test.mjs, einfuegen.test.mjs | 127 bestanden |
| Browser (Edge headless, CDP, je frisches Beispielprojekt) | werkzeuge.browser.mjs (72), einfuegen.browser.mjs (82) | 154 bestanden |

## Während des Baus gefunden und behoben

| # | Fund | gefunden von | Behebung |
| --- | --- | --- | --- |
| 1 | Zweite Übernahme derselben Datei erzeugte ein leeres Paket (Lagebericht wechselte, Umriss neu gerechnet) | Hauptlauf (Phase A) | Bezugsdatei erkannt, Umriss bleibt |
| 2 | IFC2x3-Wände (IfcWall) nicht als Wand erkannt | hlks-test (B4) | `importAuch` |
| 3 | Mehrdeutiges Typobjekt gab still die erste Klasse | hlks-test (B4) | mehrdeutig → Übertrager entscheidet über Ports und Namen |
| 4 | IfcRoof mit eigenem Typobjekt verworfen; Ventil- und Fensterwerte ohne Klasse | hlks-test (B4) | Abbildung ergänzt |
| 5 | Name des Koordinationskörpers beim ersten Bezug ignoriert; Nova schreibt Landeskoordinaten in Breite/Länge | hlks-test (B4) | berücksichtigt, Plausibilitätsprüfung |
| 6 | Unterlagen wurden nie gezeichnet (Reihenfolge der Grafikquellen) | hlks-modul (B3) | Reihenfolge korrigiert |
| 7 | Neue Fassung über die Ablage nicht als dieselbe Unterlage erkannt; doppelte Geschossnamen | hlks-modul (C1) | Name ohne Prüfsumme; Wahl über die Kote |
| 8 | Abgleich der Verbindungen: alte IFC-Verbindungen blieben, neue scheiterten an «belegt» | hlks-kern (B2) | erst bereinigen, dann verbinden |
| 9 | Systemtypen: Kälte, Regen vor Schmutz, Warmwasser vor Heizung | hlks-kern (B2) | Namensmuster und Reihenfolge |
| 10 | Sanitärapparat ohne Typwert wurde nach Einführung der Typwertlisten still nicht mehr als möglicher WC erkannt | Hauptlauf | Regel: ohne Typwert regulär nur, wenn die Liste NOTDEFINED nennt |
| 11 | Wände mit Typwert PARTITIONING, SOLIDWALL … fielen durch | hlks-kern (B1) | alle Typwerte für Wand und Stütze |
| 12 | Brüstung zählte ab Wandunterkante statt ab Geschossboden (IFC-Wände beginnen oft auf der Rohdecke darunter) | hlks-kern (B1) | Bezug OK Boden |
| 13 | 3D-Körper BKW 32.5 s (Wandlagen je Wand neu gerechnet) | hlks-kern (B1) | Zwischenspeicher je Lauf |
| 14 | 521 Einzelhinweise «passt nicht mehr» bei BKW | hlks-kern (B1) | ab 6 ein Hinweis mit Zahl, Grund und Beispielen |
| 15 | Nova schreibt Masse als reine Zahl in mm in eine Meter-Datei | hlks-kern (B2) | Plausibilität bei reinen Zahlen |
| 16 | Graue Unterlagenlinien voll deckend statt halb (PDF und Bildschirm) | Hauptlauf | Deckkraft gilt ohne Füllung der Linie |
| 17 | **IFC-Dateien über 100 MB öffnete xBIM über eine Festplatten-Datenbank: 70 s statt 34 s** | Hauptlauf (Abnahmeprüfung) | im Arbeitsspeicher bis 2 GB |
| 18 | Verbindungsabgleich suchte für jede Verbindung alle Verbindungen ab | Hauptlauf | Anschluss-Index des Modells (3.6 s → 0.1 s) |
| 19 | gzip der Netzdatei auf dem eigenen Rechner langsamer als roh | hlks-modul (C2) | nur für fremde Rechner |
| 20 | Abgeleitete Räume (aus Wänden) und echte Räume (IfcSpace) lagen übereinander, wenn die Datei mit Räumen später kam | Hauptlauf (Abnahmelauf) | echter Raum übernimmt den abgeleiteten (Id und Bezüge bleiben) |
| 21 | **Gleicher Ursprung, Inhalt 30 m daneben – der Lagebericht sah es nicht** | Hauptlauf (Abnahmelauf) | Inhaltsvergleich in der Ausrichtung, Ablehnung und Empfehlung |
| 22 | Inhaltsvergleich zuerst über Platzierungen: Nova legt alle Bauteile in den Ursprung | Hauptlauf | Mitten der Bauteilkörper statt Platzierungen, Anteil statt Rechteck |

## Zuteilung von Modell und Denktiefe

| Agent | Aufträge | Nacharbeit |
| --- | --- | --- |
| hlks-kern (Opus, xhigh) | 2 (B1 Geometrie/Architektur/Räume, B2 HLKS-Übertrag) | keine (B1 nach Unterbruch durch die Nutzungsgrenze fortgesetzt) |
| hlks-modul (Sonnet, high) | 4 (B3 Unterlagen, C1 Register Einfügen, C2 Bild und 3D-Netz, C3 Inhaltsvergleich im Dialog) | keine |
| hlks-test (Sonnet, medium) | 1 (B4 Testfälle Abbildung, Ausrichtung, echte Dateien) | keine – erste Probe der Herabstufung «Testfälle schreiben» bestanden |

Die Befunde am Fundament (Tabelle oben, Nr. 2–10) lagen in meinem Code, nicht in den Aufträgen.

Belegbilder: Grundriss mit PNG- und PDF-Unterlage und 3D mit IFC-Hintergrund unter `%TEMP%\hlks_c2\`, IFC-Dialog mit Inhaltsvergleich (Bernstrasse echt, Empfehlung, Inhalt daneben) unter `%TEMP%\hlks_c3\`, Browser-Testreihen unter `%TEMP%\hlks_abnahme1b_browser\`.

**Nachjustiert:** hlks-kern hat jetzt viermal ohne Nacharbeit geliefert (1a: B1, C1; 1b: B1, B2). Nach der Regel geht
probeweise eine Stufe tiefer: **umfangreiche Import- und Export-Adapter nach fertiger Spezifikation** (wie B2 HLKS-Übertrag,
später der IFC-Export in Modul 7) gehen ab sofort an hlks-modul. Geometrie, Rechenkerne (Module 4, 5, 6, 10) und
Synchronisation bleiben bei hlks-kern, weil Fehler dort fachlich teuer sind. Oberfläche bleibt bei hlks-modul (mechanik
ist für Arbeit ohne eigene Entscheide gedacht), Testfälle schreiben bleibt probeweise bei hlks-test. Tabelle in
`CLAUDE.md` angepasst; Protokoll `zuteilung.md`.

## Annahmen zum Prüfen (Prüfliste)

1. Ausrichtung: Toleranz 10 mm / 0.01°; Koordinationskörper wird am Namen erkannt.
2. Wassererwärmer/Speicher IfcTank STORAGE (IFC2x3 PRESSUREVESSEL), Wärmepumpe IfcUnitaryEquipment «Waermepumpe»,
   Hausanschluss IfcPipeFitting ENTRY; weitere in `kataloge/ifc.json` → `annahmen`.
3. Mehrschichtige Wände als eine Wand mit Gesamtdicke; Material = dickste Schicht.
4. Unterlagen grau, Deckkraft 0.5; IFC-Hintergrund im 3D grau durchscheinend.
5. Räume aus Wänden ab 1 m², ohne Aussenfläche; Nummer und Name bleiben leer (ergänzen).
6. Sanitärapparat ohne Typwert und ohne Ports wird nicht geraten: bleibt Hintergrund mit Hinweis «mehrdeutig».
7. Masse als reine Zahl ab 10 m in einer Meter-Datei gelten als mm.
8. Inhaltsvergleich: unter 10 % der Bauteile im bestehenden Modell gilt als «daneben» (nur wenn die Lage allein auf dem
   Gebäudeursprung beruht); ab 80 % mit der einen und unter 10 % mit der anderen Wahl kommt eine Empfehlung.

## Bekannte Grenzen und offene Punkte

- Bernstrasse: H+K-IFC und Architekten-IFC haben keinen gemeinsamen Bezug (siehe oben); Ausrichten über Punktpaare fehlt
  noch – nach der Abnahme zurückgestellt auf die Nachbearbeitungsliste (`nachbearbeitung.md`, N1).

- Boolesche Körper (Wand minus Öffnung) sind in der Netzdatei vereinfacht; für die Kollisionsprüfung (Modul 3b) braucht es
  echte Differenzen.
- Fassaden, die als IfcWall ohne Achse kommen (Bernstrasse), werden keine KICAD-Wand, bleiben aber im Hintergrund.
- Nicht übernommene Bauteile aus den echten Dateien (z. B. Schalldämpfer, VAV-Boxen, Brandschutzklappen, Luftauslass
  ENTRY/EXIT) haben noch keine Katalogklasse und bleiben Hintergrund – sie kommen mit den Familien (Modul 2).
- Grosse Projekte: Der Server braucht für Status, Modell und Körper bei BKW (6400 Körper) je 1–2 s, das ganze BKW-Modell dreht
  im 3D nur mit ~0.6 Bildern/s (viele Einzelkörper; das Hintergrundnetz allein schafft ~40 Bilder/s). Beschleunigen folgt mit
  dem 3D-Ausbau.
- Einpassen im 3D: Ausreisser im Hintergrundnetz können die Ansicht klein machen.
- Kalibrieren wählt eine PDF- oder Bild-Unterlage über den Projektbrowser, nicht per Klick auf das Bild.
- IFC-Dateien bis 2 GB öffnen im Arbeitsspeicher (rund das Fünffache der Dateigrösse).

## Selbst ausprobieren

Programmfenster mit dem NAS-Testprojekt (Port 5130):

```powershell
powershell -ExecutionPolicy Bypass -File "$env:OneDrive\Claude_Code\HLKS_CAD\werkzeuge\start.ps1" -Browser -Projekt "W:\02_Projektdateien Objekte\_Test HLKS-CAD\c_Pläne und Modelle\e_KICAD"
```

Zum Ausprobieren: Register **Einfügen** › IFC › Datei wählen (z. B. das Architekten-IFC) › Lagebericht lesen › übernehmen ·
Einfügen › DWG/PDF › Geschoss wählen › Kalibrieren (zwei Punkte, zwei Masse) · Ansicht 3D zeigt den IFC-Hintergrund grau.
