# Testbericht Modul 1c – Gebäudehülle

Stand 18.10.2026 · gebaut am 18.10.2026 (geplant 07.–11.12.2026) · **von Christian abgenommen am 18.10.2026** (Kennwertquellen und Prüfliste offen)

## Ergebnis

Die vier Abnahmekriterien der Spezifikation (`entwurf/modul-1c.md`) sind erfüllt, mit einer Einschränkung, die nicht im
Code liegt: **Für echte Projekte fehlen gültige Kennwertquellen.** KICAD rechnet R, U, Uw und U·A korrekt (von Hand
nachgeprüft), setzt aber nur Kennwerte mit Quelle ein. Im Bestand gibt es keine aktuelle Tabelle für Baustoff-λ, Rsi/Rse,
Fenster und Wärmebrücken, und die Archicad-IFC von BKW und Bernstrasse enthalten keine λ-Werte. Ohne deinen Entscheid
(siehe «Offen: Quelle der Kennwerte») bleibt ein echtes Projekt deshalb bei Flächen, Angrenzungen und Längen – das U
fehlt, ausser es kommt aus der Datei (BKW: U je Bauteil aus dem IFC).

| Kriterium | Nachweis |
| --- | --- |
| 1 Beispielprojekt mit vollständiger Hülle, Raumgrenzen = Handrechnung | «MFH Musterweg»: Boden, Decken und Dach aus den Wänden erzeugt (je 127.82 m², von Hand nachgerechnet), Testaufbau zugewiesen (R = 4.25 m²·K/W, U = 0.226 W/(m²·K), **Testwerte**), 13 Räume mit 89 Raumgrenzen (Wand 667.2 m², Boden 360, Decke 240, Dach 120 m²); Bodenbilanz 0.00 m² Abweichung. Raumgrenzen, U, Uw, U·A und Wärmebrücken-Längen an **15 unabhängigen Referenzfällen von Hand** geprüft (Gebäude mit beheiztem/unbeheiztem Raum, Nordwinkel, Fenster und Tür, Terrain im Geschoss, Inselraum) – alle stimmen; dazu 18 Fälle von Hand zur Wandband-Regel (Befund 11). Das Beispielprojekt selbst hat keine Innenwände und keine Fenster; Fenster mit Kennwerten sind in den Referenzfällen und im Browser-Test angelegt |
| 2 Materialschichten aus dem Architekten-IFC | Bernstrasse (Archicad 27, IFC2x3): 19 Aufbauten, 27 Materialien. BKW (Archicad 28, IFC4): 34 Aufbauten, 43 Materialien, U aus der Datei für 800 Bauteile, Nordwinkel −18.325° aus TrueNorth. Herkunft «ifc» mit Fundstelle. Angrenzung automatisch: BKW 497 Räume, 9503 Raumgrenzen in 0.4 s (aussen 17 651 m², Erdreich 11 301 m², beheizt 46 139 m²), Bodenbilanz exakt (15 862.5 m²); Bernstrasse 49 Räume, 824 Raumgrenzen, Bodenbilanz exakt |
| 3 Oberfläche | Register **Hülle** (Aufbau-Editor mit Schichten, R/U live, fehlende Kennwerte rot; Material mit Quellenpflicht; Fensterkennwerte; Wärmeübergang; Hülle prüfen; Wärmebrücken vorschlagen; Listen), Architektur › Decke/Boden/Dach erzeugen, Projektbrowser «Hülle», Darstellung **«Hülle»** im Grundriss und 3D (Farbe nach Angrenzung, Legende mit Flächen, Klick zeigt die Raumgrenze), Listen «Raumgrenzen» und «Hüllbauteile» |
| 4 MCP, Trockenlauf, rückgängig, Herkunft | 15 MCP-Werkzeuge (`material_*`, `aufbau_*`, `fenster_kennwerte`, `uebergang_setzen`, `decke_erzeugen`, `waermebruecken_vorschlagen`, `raumgrenzen`, `huellbauteile`, `huelle_pruefen`, `materialien`); jeder Befehl mit Trockenlauf und als ein Paket rückgängig; jeder Kennwert mit Herkunft und Quelle, gerechnete Werte mit Rechenweg als Quelle; **Prüfung P12** meldet Kennwerte ohne Quelle |

Bilder (Beispielprojekt, fiktiv): `berichte/modul-1c/` – Grundriss EG und UG mit Hülle, 3D mit Hülle und Infofeld einer Raumgrenze.

## Offen: Quelle der Kennwerte (dein Entscheid)

Bestandsaufnahme vom 18.10.2026 (Spezifikation, Abschnitt «Quellen für Kennwerte»):

- **Vorhanden:** SIA Merkblatt 2001:2005 «Wärmedämmstoffe» (Dämmstoffe nach Herstellerangabe, Stand 2005), SIA 180:2002,
  SIA 380/1 (2009, 2017), SIA 2024:2021 (Nutzungswerte), `W:\05_Normen und Merkblätter\Bauteile Neubau.pdf` (Inhalt
  nicht geprüft).
- **Fehlt:** Wärmeschutzdaten für Beton, Mauerwerk, Putz, Holz, Estrich (SIA 2001 «Wärmeschutzdaten» bzw. SN EN ISO
  10456), Rsi/Rse in aktueller Ausgabe (SIA 180:2014 bzw. SN EN ISO 6946), Fensterkennwerte, Wärmebrückenkatalog.

Fragen: Welche Ausgaben sollen gelten, und beschaffst du sie? Soll SIA 2001:2005 als Dämmstoff-Vorlage in die
Bürobibliothek (`kataloge/materialien.json`, heute leer)? Bis dahin trägt jedes Projekt Kennwerte aus Datenblättern oder
von dir ein, je mit Quelle – KICAD nimmt keinen Wert ohne Quelle.

## Datenbefund BKW

Das Architekten-IFC von BKW liefert Wand-U-Werte bis **13.075 W/(m²·K)** – für eine Aussenwand nicht plausibel
(vermutlich ein Export- oder Einheitenfehler im Archicad-Pset). KICAD übernimmt den Wert wie geliefert, mit der Datei als
Quelle. Vorschlag: beim Architekten nachfragen, bevor die U-Werte aus dieser Datei für eine Rechnung dienen.

## Was gebaut ist

| Baustein | Inhalt | Ort |
| --- | --- | --- |
| Datenmodell | Klassen Material (MAT), Aufbau (AUF), Waermebruecke (WB); Fenster/Tür-Kennwerte (ug, uf, psiG, g, rahmenanteil, uw, ud, sonnenschutz, fc); Raum.beheizt und Anforderungen; Gebäude terrain, n50, q50; Projektinformation nordwinkel, uebergang (Rsi/Rse je Wärmestromrichtung mit Quelle); Decke.art; Bürobibliothek `kataloge/materialien.json` (leer) | `kataloge/klassen.json`, `schema/`, `kataloge/ifc.json` |
| Raumgrenzen-Kern | je Raum Seiten (Wandband, Öffnungen abgezogen), Boden und Decke (Polygon-Schnitt mit den Nachbargeschossen), Angrenzung aussen/erdreich/beheizt/unbeheizt aus der Geometrie, Terrain teilt nach Höhe, Azimut mit Nordwinkel, Neigung, U und U·A; Innenmasse, Aussenmass-Fläche je Bauteil zusätzlich; Inselräume ohne Überlappung | `src/Hlks.Kern/Huelle/Raumgrenzen*.cs` |
| Material, Aufbau, Fenster | R = Σ d/λ, U = 1/(Rsi + R + Rse), Uw nach Flächen und Glasrand; Kennwerte ohne Quelle gelten als fehlend; Nachführung bei Änderung eines Materials; Wärmeübergang als Projektvorgabe | `Huelle/Aufbau*.cs`, `Material*.cs`, `Fenster*.cs` |
| Decke/Boden/Dach erzeugen | Umriss aus Wänden und Räumen eines Geschosses, Höfe gefüllt, Decke gehört zum Geschoss darüber | `Architektur/DeckeBefehle.cs` |
| Anschluss Wand–Decke | «Decke läuft durch»: Wand endet im Schnitt und im 3D an der Unterkante der Decke darüber | `Architektur/Wandgeometrie.cs` |
| IFC-Hülle | Materialschichten → Aufbauten (gleiche Schichtfolge = ein Aufbau), Materialien mit thermischen Psets, U aus Pset_*Common, TrueNorth → Nordwinkel | `Import/IfcHuelle.cs` |
| Wärmebrücken | Vorschläge Deckenauflager (Aussenseite samt Ecken) und Fenster-/Türanschlag (Umfang), ohne ψ (keine Quelle) | `Huelle/RaumgrenzenBefehle.cs` |
| Prüfung | P12 «Hüllenkennwert ohne Quelle» | `Pruefung/Pruefungen.cs` |
| Grafik | Ebene `huelle`: Streifen je Raumgrenze im Grundriss, Flächen im 3D, nur Bildschirm | `Grafik/HuelleGrafik.cs` |
| Oberfläche | Register Hülle, Darstellung Hülle 2D/3D, Listen, Projektbrowser | `wwwroot/js/…`, `OBERFLAECHE.md` |

## Abnahmelauf mit den echten Dateien

`Abnahme1cTests` mit `HLKS_ABNAHME=1`, frisches Projekt je Gebäude, Lauf vom 18.10.2026 13:22 nach dem Wandband-Nachtrag (Rechner nicht mehr gedrosselt):

| | Beispielprojekt (fiktiv) | BKW Hauptsitz (Archicad 28, IFC4) | Bernstrasse 42 (Archicad 27, IFC2x3) |
| --- | --- | --- | --- |
| IFC übernehmen (Architektur und Räume) | – | 7.6 s | 3.3 s |
| Aufbauten / Materialien (davon mit λ) | 1 / 2 (Testwerte) | 34 / 43 (0) | 19 / 27 (0) |
| Aufbauten mit U | 1, gerechnet | 34, aus der Datei | 0 |
| Nordwinkel | 0° | −18.325° (TrueNorth) | 0° |
| Räume / Raumgrenzen / Rechenzeit | 13 / 89 / < 0.1 s | 497 / 9503 / 0.4 s | 49 / 824 / < 0.1 s |
| Raumgrenzen je Angrenzung | aussen 340.0, Erdreich 225.6, beheizt 821.6 m² | aussen 17 650.8, Erdreich 11 300.5, beheizt 46 139.2 m² | aussen 1031.4, Erdreich 486.5, beheizt 1217.2 m² |
| Bodenbilanz (Boden − Raumflächen) | 0.00 m² (360.0 m²) | 0.00 m² (15 862.5 m²) | 0.00 m² (430.5 m²) |
| Kantenstücke ohne Wand («offen») | 51 % (keine Innenwände) | 6.3 % | 1.0 % |
| Raumgrenzen mit U / U·A gegen aussen und Erdreich | 25 / 73.7 W/K (Testwerte) | 3268 / 15 407 W/K (U aus der Datei, siehe Datenbefund) | 0 / – |
| Hüllbauteile | nicht gemessen | 1490 | 228 |
| Wärmebrücken-Vorschläge (ohne ψ) | 8 Deckenauflager, 90.8 m | 919 Deckenauflager + 238 Fensteranschläge, 7630.8 m | 174 + 35, 867.7 m |
| 3D-Körper | – | 5580 in 0.8 s | 710 in 0.1 s |
| Prüfung P12 (Kennwert ohne Quelle) | 0 | 0 | 0 |

Die Kennwerte im Beispielprojekt sind **Testwerte** (Quelle «Testwert Abnahme 1c»), keine Materialdaten.
Die offenen Kantenstücke im Beispielprojekt (51 %) kommen daher, dass es zwischen den Räumen keine Innenwände hat.

## Automatische Tests

| Bereich | Testklassen | Tests |
| --- | --- | --- |
| Raumgrenzen (mit Wandband, Wärmebrücken, Befunden, BKW echt) | Raumgrenzen*Tests | 49 |
| Referenzfälle von Hand (B5) | Referenz1cTests | 15 |
| Material, Aufbau, Fenster, Abfragen | HuelleMaterial/Aufbau/Fenster/AbfragenTests | 56 |
| Decke/Boden/Dach erzeugen | DeckeBefehleTests | 17 |
| Anschluss Wand–Decke | AnschlussWandDeckeTests | 24 |
| IFC-Hülle | IfcHuelleTests | 36 |
| Darstellung «Hülle» | HuelleGrafikTests | 15 |
| Prüfung P12 | PruefungP12Tests | 3 |
| Abnahmelauf (nur mit HLKS_ABNAHME=1, sonst sofort fertig) | Abnahme1cTests | 1 |
| HKAP-Schnittstelle (Modul 1d, mit Integration gegen die Testkopie) | Hkap*Tests | 42 |
| Kern aus 0a–1b (teils angepasst) | | 1760 |
| **Total Kern** | | **2018** |
| MCP und Kommandozeile (davon 3 für die Hülle) | | 29 |
| REST, Unterlagen-API, Live-Abgleich | | 88 |
| **Total C#** | | **2135 bestanden** |
| Node gegen den laufenden Server | ansichten 58, werkzeuge 43, einfuegen 26, huelle 45, huelle-darstellung 17, hkap 34 | 223 bestanden |
| Browser (Edge headless, CDP, je frisches Beispielprojekt) | werkzeuge 72, einfuegen 82, huelle 79, huelle-darstellung 28, hkap 85 (gegen die HKAP-Testkopie 5098, nie 5013) | 346 bestanden |

## Während des Baus gefunden und behoben

| # | Fund | gefunden von | Behebung |
| --- | --- | --- | --- |
| 1 | Räume aus Wänden (1b): Fundament- und Sockelwände unter dem Boden teilten Räume | hlks-kern (B1) | Wände mit Oberkante ≤ Kote + 20 mm trennen nicht; die von mir vorgegebene strengere Regel hätte bei BKW 216 echte Räume gelöscht – B1 wich bewusst ab, übernommen |
| 2 | Inselräume (Raum im Raum) doppelt gezählt (BKW 28 Überlappungen) | hlks-kern (B1) | Insel abgezogen, 0 Überlappungen |
| 3 | Wand und Decke überlappen in Schnitt und 3D | hlks-kern (B3) | Anschluss «Decke läuft durch» (B6) |
| 4 | Schnitt durch BKW 395 s | hlks-kern (B6) | Zwischenspeicher je Lauf: 7 s |
| 5 | Wandverlängerung an Ecken 0.70 s je Lauf | hlks-kern (B1) | Raster: < 0.01 s, Ergebnis gleich |
| 6 | IFC-Materialverweise in Schichten wurden beim Abgleich nicht aufgelöst | hlks-modul (B4) | Hauptlauf (Übernahme) |
| 7 | Gerechnete Werte machten jede erneute IFC-Übernahme zu einem Paket | hlks-modul (B4) | selbst abgefangen |
| 8 | Kürzel AB doppelt (Aufbau / Ausgussbecken) | Hauptlauf | Aufbau = AUF |
| 9 | Kennwert ohne Quelle fiel nirgends auf | hlks-modul (B2, Vorschlag) | Prüfung P12 |
| 10 | Spitzen im Raumumriss bei Inselräumen | hlks-modul (C1b) | behoben |
| 11 | **Boden- bzw. Deckenstreifen über einer Innenwand des Nachbargeschosses galt als «aussen»** (Lücke meiner Spezifikation) | hlks-test (B5, Referenzfall) | Regel festgelegt (Streifen an der Wandachse teilen, je Hälfte zum Raum dort), umgesetzt von hlks-kern mit 18 Fällen von Hand; **BKW: 588.6 m² gingen vorher fälschlich an aussen, Erdreich oder Dach** |
| 12 | Bei BKW: Feld einer Stichwand an einer Querwand blieb ungeteilt; eine 1 mm schiefe Wand teilte mit Splittern eine Querwand | hlks-kern (B1-W) | Suchlinie über das Stück hinaus, kleine Abschnitte zuerst zusammengefasst |
| 13 | Browser-Prüfung suchte noch den alten Kerntext «Der Ursprung passt» (in 1b bereinigt) | Hauptlauf (Abschluss) | Prüfung angepasst |

## Zuteilung von Modell und Denktiefe

Ausgewertet für 1c und 1d zusammen (beide am 18.10.2026 parallel gebaut):

| Agent | Aufträge | Nacharbeit |
| --- | --- | --- |
| hlks-kern (Opus, xhigh) | 1c: B1 Raumgrenzen (mit Nachtrag), B3 Decke erzeugen, B6 Anschluss Wand–Decke, B1-W Streifen über Wandband; 1d: B HKAP-Klient und Abgleich (mit Nachtrag) | keine |
| hlks-modul (Sonnet, high) | 1c: B2 Material/Aufbau/U, B4 IFC-Hülle (Import-Adapter, Probe seit 1b), C1a Register Hülle (mit Nachtrag), C1b Darstellung Hülle; 1d: C Oberfläche HKAP | eine leichte: der HKAP-Browser-Test war nicht in sich geschlossen (ein Test-Strang stand nur im eigenen Arbeitsprojekt) – von mir ergänzt |
| hlks-test (Sonnet, medium) | 1c: B5 Referenzfälle von Hand | keine – fand die Lücke im Streifen über Innenwänden |

Die «Nachträge» waren zusätzliche Aufträge nach Befunden in meinem Code oder meiner Spezifikation, keine Korrekturen
an der Arbeit der Agenten. Eigene Fehler im Hauptlauf: B5 hatte ich bis zum Abschluss nicht vergeben (beim Abschluss
nachgeholt; künftig gleiche ich die Phasentabelle der Spezifikation vor dem Gesamttest gegen das Protokoll ab), und eine
Browser-Prüfung suchte noch einen Text, den ich in 1b geändert hatte.

**Nachjustiert:** keine Stufe geändert.
- hlks-kern liefert seit 1a achtmal ohne Nacharbeit. Die Regel würde eine Stufe tiefer verlangen; die Import-Adapter sind
  seit 1b schon abgegeben. Was bei hlks-kern bleibt (Geometrie, Rechenkerne, Synchronisation), ist fachlich teuer, und
  in 1c zeigte sich der Nutzen der Stufe: B1 wich begründet von meiner Vorgabe ab und verhinderte, dass bei BKW 216
  echte Räume gelöscht wurden.
- Import-Adapter bei hlks-modul (Probe): B4 ohne Nacharbeit → Probe läuft weiter.
- Oberfläche bei hlks-modul: eine leichte Nacharbeit in drei Aufträgen (C1a, C1b, 1d C) → bleibt.
- Routinebau im Kern nach Spezifikation bei hlks-modul (B2): ohne Nacharbeit → bleibt (eigene Entscheide nötig, z. B. Uw-Annahme).
- Testfälle schreiben bei hlks-test (Probe seit 0b): zweimal ohne Nacharbeit, beide Male mit echten Befunden → Probe läuft weiter.

## Annahmen zum Prüfen (Prüfliste)

Vollständig in `entwurf/modul-1c.md`, Abschnitt «Annahmen für Christians Prüfliste» (0–16). Die wichtigsten:

0. **Quelle der Kennwerte** (siehe oben).
1. Raumgrenzen in Innenmassen; Aussenmass-Fläche je Bauteil zusätzlich. Welcher Massbezug für welche Norm gilt, legst du fest.
2. `Raum.beheizt` Vorgabe «ja».
3. Erdreich: unterstes Geschoss und alles unter `Gebaeude.terrain` (Vorgabe ±0); Wände werden am Terrain geteilt.
6. Rsi/Rse und alle Kennwerte nur mit Quelle (ohne: kein U, nur R).
8. Anschluss Wand–Decke «Decke läuft durch».
9. Erzeugte Decke: Oberkante = OK fertig Boden des Geschosses darüber; Höfe gefüllt.
14. Farben der Darstellung «Hülle» (aussen rot, Erdreich braun, unbeheizt blau, beheizt grün), nur Bildschirm – soll sie auf den Plan?
15. Deckenauflager nur an Wänden beheizter Räume gegen aussen oder Erdreich; Anschlag auch für Aussentüren.
16. Streifen über/unter einer Innenwand des Nachbargeschosses: an der Wandachse geteilt.

## Bekannte Grenzen

- Nur flache Dächer; geneigte Dächer, Gauben, Auskragungen später. Bei BKW bleiben 20 Wände unter geneigten IFC-Dächern
  ganz (Doppelfüllung im Schnitt sichtbar).
- U·A ohne Normfaktoren (keine b-Faktoren, keine Temperaturkorrekturen) – das Normverfahren folgt mit der Heizlast (Modul 5).
- Darstellung «Hülle» nur am Bildschirm, nicht im Plan-PDF.
- «Zoom alles» ist bei BKW zu klein (Ausreisser im Modell, bekannt aus 1b).
- `raum_erkennen` beim Zeichnen bildet keine Inselräume; die entstehen nur beim IFC-Übertrag.
- BKW, versetzter Gebäudeteil: Räume aus Wänden reichen von Kote zu Kote (3.00 m), Wände und Platten liegen dort aber auf
  4.761 m. Rund 1550 m² Decke finden deshalb keine Platte (z. B. R-202 mit 118 m²); Fläche und Angrenzung stimmen, nur das
  Bauteil fehlt. Kommt mit der Geschosszuordnung versetzter Gebäudeteile (später).
- Seiten-Nachbarn: Die beiden Korrekturen aus B1-W (Suchlinie, Splitter) wären auch für die Wandseiten denkbar (BKW: aussen
  +71 m², beheizt −89 m²); ob das besser ist, ist ungeklärt – bewusst nicht geändert.
- Quellenpflicht steht heute an drei Stellen (Prüfung P12, Nachführung, Oberfläche); Vorschlag C1a: als Katalogangabe je
  Eigenschaft führen – vorgemerkt für Modul 2.

## Selbst ausprobieren

KICAD-Testserver mit dem Beispielprojekt: **http://localhost:5131**. Ansicht › Darstellung › **Hülle** schaltet Grundriss
oder 3D um; Architektur › Decke erzeugen (je Geschoss, Dicke angeben); Register **Hülle** › Material anlegen (mit Quelle) ›
Aufbau anlegen (R und U erscheinen live) › zuweisen › Liste «Raumgrenzen».
