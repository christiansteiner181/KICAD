# Modul 1c – Gebäudehülle: Spezifikation

Stand 18.10.2026 (Hauptlauf). Start auf Christians Zuruf vom 18.10.2026 («gleich mit nächstem Modul starten»;
geplant 07.–11.12.2026, 5 Arbeitstage). Grundlage: Vorgabe Christian 17.10.2026 «Gebäudehülle erfassen» (Wissensspeicher
`hlks_cad.md`), Datenmodell 0a (abstrakte Klasse `Bauteil` mit `aufbau` und `angrenzung`, Schicht `huelle`).

## Ziel

Die Hülle ist **Teil der Raumstruktur** und liefert später die Eingaben für Heizlast (Modul 5) und die physikalische
Simulation (Modul 10a): Bauteile mit Schichtaufbau aus einer Materialbibliothek, Fenster mit Kennwerten, Angrenzung,
Orientierung, Wärmebrücken, Luftdichtheit. Die **Raumgrenzen** (Fläche je Raum ↔ Nachbar) rechnet der Kern selbst aus
Raumpolygonen und Bauteilen – nicht aus IFC-Space-Boundaries (selten brauchbar).

**Fertig, wenn …**
1. Das Beispielprojekt «MFH Musterweg» hat eine vollständige Hülle: Boden, Decken und Dach erzeugt, Aufbauten den
   Bauteilen zugewiesen, Fenster mit Kennwerten; die Raumgrenzen je Raum (Fläche, Angrenzung, Nachbar, Orientierung,
   Bauteil, Aufbau) stimmen mit einer Handrechnung überein.
2. Aus dem Architekten-IFC (BKW Archicad IFC4, Bernstrasse Archicad IFC2x3) sind die Materialschichten als Aufbauten
   und Materialien übernommen, mit Herkunft «ifc»; die Angrenzung ergibt sich automatisch.
3. In der Oberfläche: Aufbau-Editor (Schichten, R- und U-Wert live), Materialbibliothek, Darstellung «Hülle» im
   Grundriss und im 3D (Farbe nach Angrenzung), Liste der Raumgrenzen und der Hüllbauteile.
4. Alles über MCP-Werkzeuge bedienbar, mit Trockenlauf, rückgängig machbar, jeder Kennwert mit Herkunft und Quelle.

## Grundsätze

- **Keine Kennwerte aus dem Gedächtnis.** λ, ρ, c, Rsi/Rse, Ug/Uf/g, ψ/χ kommen aus einer genannten Quelle (Norm-
  Tabelle mit Ausgabe, Herstellerdatenblatt, IFC-Datei) oder von Christian. Ein Kennwert ohne Quelle wird abgelehnt.
  Fehlt ein Wert, rechnet der Kern, was geht (z. B. Summe d/λ der bekannten Schichten), und nennt, was fehlt.
- **Physik ist kein Normwert:** Formeln wie R = d/λ, U = 1/(Rsi + ΣR + Rse), Uw = (Ag·Ug + Af·Uf + lg·ψg)/(Ag + Af),
  Flächen und Orientierungen sind Rechnung (Herkunft «berechnung»), die eingesetzten Zahlen tragen ihre Quelle.
- **Projekt hält seine Daten:** Materialien und Aufbauten sind Elemente des Projekts (Herkunft, Pakete, Abgleich wie
  alles andere). Eine bürogemeinsame Bibliothek (`kataloge/materialien.json`) ist nur Vorlage: `material_uebernehmen`
  kopiert einen Eintrag samt Quelle ins Projekt.
- **Angrenzung wird gerechnet, nicht getippt:** je Raumgrenze aus der Geometrie (Nachbarraum beheizt/unbeheizt,
  aussen, Erdreich). `Bauteil.angrenzung` bleibt als ausdrückliche Vorgabe für Sonderfälle (überschreibt die Rechnung,
  mit Hinweis).

## Datenmodell (Hauptlauf, Fundament)

| Klasse / Eigenschaft | Inhalt |
| --- | --- |
| **Material** (neu, Schicht `huelle`, ohne Geometrie) | `lambda` W/(m·K), `rohdichte` kg/m³, `waermekapazitaet` J/(kg·K), `emission` –, `absorption` –, `mu` – (Diffusionswiderstand), `kategorie` (Text); jeder Wert mit Herkunft + Quelle |
| **Aufbau** (neu, Schicht `huelle`, ohne Geometrie) | `art` (wand, decke, dach, boden, tuer); `schichten` (Liste {material: Element-Id, dicke mm}, von innen nach aussen); `uWert` W/(m²·K) – gerechnet, oder ausdrücklich (Datenblatt/IFC) mit Quelle; `rWert` m²·K/W (Summe d/λ, gerechnet); `waermestrom` (horizontal, aufwaerts, abwaerts – für Rsi/Rse) |
| **Bauteil** (Wand, Decke, Dach, Stütze, Unterzug, Fenster, Tür) | Bezug `aufbau` → Aufbau (statt Text); `angrenzung` nur noch Vorgabe (leer = gerechnet) |
| **Decke** | neu `art`: decke, boden (Bodenplatte bzw. Boden über unbeheizt/aussen); Dächer sind die eigene Klasse Dach |
| **Fenster**, **Tuer** | `ug`, `uf` W/(m²·K), `g` –, `rahmenanteil` –, `psiG` W/(m·K), `uw` W/(m²·K) gerechnet oder ausdrücklich, `sonnenschutz` (keiner, aussen, innen, zwischen), `fc` – (Abminderung des Sonnenschutzes) |
| **Raum** | `beheizt` (ja/nein; Vorgabe ja), `solltemperatur` °C (nur Angabe, Vorgabe leer) |
| **Gebaeude** | `n50` 1/h bzw. `q50` m³/(h·m²) Luftdichtheit mit Quelle; `terrain` Kote mm (Terrainhöhe für erdberührte Teile; Vorgabe ±0) |
| **Projektinformation** | `nordwinkel` Grad (Winkel von Projekt-Nord +y zu geografisch Nord, im Uhrzeigersinn; Vorgabe 0 = Plan genordet; aus IFC TrueNorth bzw. Landeskoordinaten, wenn vorhanden); `uebergang` = Rsi/Rse je Wärmestromrichtung und gegen Erdreich mit Quelle (Projektvorgabe; ohne sie gibt es keinen U-Wert, nur R) |
| **Waermebruecke** (neu, Schicht `huelle`) | `art` (deckenauflager, fensteranschlag, sockel, attika, balkon, ecke, punkt), `psi` W/(m·K) bzw. `chi` W/K mit Quelle, `laenge` m (gerechnet aus der Geometrie, wo möglich), Bezüge auf die Bauteile |

## Raumgrenzen (Rechenkern)

Abfrage `raumgrenzen` (alle Räume oder einer, Geschoss) und Ableitung «Raumgrenzen» (Liste). Je Raum:

- **Seiten:** jede Kante des Raumpolygons wird den Wänden zugeordnet, deren Band (Achse ± Dicke/2, Toleranz 20 mm) sie
  berührt; Teilstücke ohne Wand = «offen» (Hinweis). Je Teilstück: Nachbar = Raum auf der anderen Seite der Wand (Punkt
  um Dicke + 50 mm nach aussen versetzt) → `beheizt` bzw. `unbeheizt` nach `Raum.beheizt`; kein Raum → `aussen`; unter
  `Gebaeude.terrain` → `erdreich` (Teilfläche nach Höhe getrennt). Höhe = Raumhöhe wie in der Raumliste.
- **Öffnungen:** Fenster und Türen der Wand im Teilstück werden abgezogen und als eigene Raumgrenze geführt.
- **Boden und Decke:** Raumfläche gegen die Räume darunter bzw. darüber (Polygon-Schnitt); Rest im Wandband einer Wand
  des Nachbargeschosses → an der Wandachse geteilt, je Hälfte zum Raum auf dieser Seite (Abschluss 18.10.2026); übriger
  Rest unten: unterstes Geschoss bzw. unter Terrain → `erdreich`, sonst `aussen`; Rest oben: `aussen` (Dach). Bauteil = Decke/Dach, das die
  Fläche überdeckt.
- **Masse:** **Innenmasse** (raumseitige Flächen, wie sie der Raum sieht) – für Simulation und Raumbilanz. Die
  Aussenmass-Hüllfläche je Bauteil kommt als zweite Grösse dazu (für Normverfahren, deren Massbezug Christian festlegt).
- **Orientierung:** Azimut der Aussennormalen (0° Nord, im Uhrzeigersinn, mit `nordwinkel`), Neigung (90° Wand, 0°
  Dach flach nach oben, 180° Boden).
- **Ergebnis je Raumgrenze:** raum, nachbar (Raum-Id | aussen | erdreich), art (wand, fenster, tuer, boden, decke,
  dach), bauteil, aufbau, flaeche m², azimut, neigung, u (wenn bekannt), `ua` = U·A W/K (wenn U bekannt).
- Wärmebrücken: `waermebruecken_vorschlagen` legt je Geschoss Deckenauflager (Länge Aussenwand) und je Fenster in einer
  Aussenwand den Fensteranschlag (Umfang) an – ohne ψ (Quelle fehlt: Hinweis).

## Decken, Böden, Dach erzeugen (Projekte ohne IFC)

Befehl `decke_erzeugen` {geschoss, art: decke | boden | dach, dicke, aufbau?}: Umriss = Aussenkontur der Wände des
Geschosses (Vereinigung der Wandbänder + Räume, Löcher gefüllt); Oberkante: decke = Kote des Geschosses darüber,
boden = Kote des Geschosses, dach (flach) = Kote + Geschosshöhe. Geneigte Dächer und Auskragungen: nicht in 1c
(Prüfliste). Erscheinen in Grundriss (geschnitten), Schnitt und 3D (bestehende Darstellung).

## IFC (Erweiterung des Architektur-Übertrags aus 1b)

- `IfcMaterialLayerSetUsage` / `IfcMaterialLayerSet` → Aufbau (Schichten mit Dicke, Reihenfolge innen → aussen nach
  Layer-Richtung und Wandseite); gleiche Schichtfolge = derselbe Aufbau (Kennung «aufbau:<Prüfsumme der Schichten>»).
- `IfcMaterial` → Material (Kennung «material:<Name>»); Kennwerte aus `Pset_MaterialThermal` (ThermalConductivity,
  SpecificHeatCapacity) und `Pset_MaterialCommon` (MassDensity), Herkunft ifc mit Fundstelle.
- `Pset_WallCommon` / `Pset_SlabCommon` / `Pset_RoofCommon`: `ThermalTransmittance` → Aufbau.uWert (ausdrücklich, Quelle
  = Datei), `IsExternal` → nur Hinweis bei Widerspruch zur gerechneten Angrenzung.
- `Pset_WindowCommon` / `Pset_DoorCommon`: `ThermalTransmittance` → uw, `GlazingAreaFraction` → rahmenanteil = 1 − f.
- `IfcSlab` BASESLAB → Decke art boden; `IfcRoof`/ROOF → Dach. TrueNorth des Kontexts → `nordwinkel` (Vorschlag beim
  ersten IFC, sonst Hinweis bei Abweichung).

## Oberfläche

- Register **Architektur**: «Decke erzeugen», «Boden», «Dach»; **neues Register «Hülle»**: Aufbauten (Editor mit
  Schichtentabelle: Material wählen, Dicke, R/U live, fehlende Kennwerte rot), Materialbibliothek (Projekt + Büro,
  «übernehmen»), Fensterkennwerte, Wärmebrücken vorschlagen, Raumgrenzen rechnen.
- Ansichtsdarstellung **«Hülle»** (Ansichtseigenschaft): Grundriss Wände nach Angrenzung eingefärbt, 3D Raumgrenzen
  als Flächen (aussen, erdreich, unbeheizt, beheizt je eigene Farbe), Legende.
- Listen: «Raumgrenzen» (je Raum, Summen je Angrenzung und Orientierung) und «Hüllbauteile» (Bauteil, Aufbau, U, Fläche).
- Projektbrowser: Knoten «Hülle» mit Materialien, Aufbauten, Wärmebrücken.

## Nicht in 1c

Verschattung durch Nachbarbauten und Terrain (swissBUILDINGS3D/swissALTI3D) → Modul 10a. Geneigte Dächer, Gauben,
Auskragungen, Bestand aus Punktwolke → später. Heizlast und U-Wert-Nachweis nach Norm → Modul 5 bzw. Ausgabe.

## Phasen und Zuteilung

| Teil | Inhalt | Wer | Dateien |
| --- | --- | --- | --- |
| A | Spezifikation, Datenmodell (Klassen, Eigenschaften, Schema), Rsi/Rse-Regel, Bibliotheksformat | Hauptlauf | `kataloge/klassen.json`, `schema/`, `entwurf/modul-1c.md` |
| B1 | Raumgrenzen-Kern, Angrenzung, Orientierung, Wärmebrücken-Längen | hlks-kern | `src/Hlks.Kern/Huelle/Raumgrenzen*.cs`, Abfrage/Ableitung, Tests `Raumgrenzen*Tests.cs` |
| B2 | Material/Aufbau/Fenster: Befehle, R/U/Uw-Rechnung, Bibliothek, `waermebruecken_vorschlagen` (nutzt B1-Längen über Schnittstelle) | hlks-modul | `src/Hlks.Kern/Huelle/Aufbau*.cs`, `Material*.cs`, MCP, Tests |
| B3 | Decke/Boden/Dach erzeugen (Umriss aus Wänden) | hlks-kern | `src/Hlks.Kern/Architektur/DeckeBefehle.cs`, Tests |
| B4 | IFC: Materialschichten, Materialien, Psets, TrueNorth | hlks-modul | `src/Hlks.Kern/Import/IfcHuelle*.cs` (eigener Übertrager im Bereich architektur), Tests |
| C1 | Oberfläche Register Hülle, Darstellung Hülle 2D/3D, Listen, Projektbrowser | hlks-modul | `wwwroot/js/…`, Grafik-Quelle Hülle im Kern |
| B5 | Referenzfälle von Hand (Raumgrenzen, U, Uw) | hlks-test | `tests/…` |
| D | Gesamttest, Abnahmelauf (Beispielprojekt, BKW, Bernstrasse), Bericht | Hauptlauf | |

Parallel läuft die Sitzung «IFC über zwei Punktpaare ausrichten» (Christian, 18.10.2026): sie ändert
`IfcAusrichtung.cs`, `IfcUebernahme.cs` und den IFC-Dialog (`einfuegen-text.js`, `aktionen-einfuegen.js`) – diese
Dateien fasst 1c nicht an.

## Quellen für Kennwerte (Bestandsaufnahme 18.10.2026)

Gesucht in `Claude_Code`, im Wissensspeicher und auf `W:\05_Normen und Merkblätter` (nur Titel gelesen):
- **Vorhanden:** SIA Merkblatt 2001:2005 «Wärmedämmstoffe» (Herstellerangaben λD, ρ, μ, c für Dämmstoffe, Stand 2005,
  als Text im Normenverzeichnis des KI-Planungsbüros), SIA 180 (Ausgabe 2002), SIA 380/1 (2009, 2017), SIA 2024:2021
  (Nutzungs-Standardwerte, auch Uop/Uw/g je Nutzung, als CSV in HK-AI-BIM), `W:\05_Normen und Merkblätter\Bauteile
  Neubau.pdf` (Inhalt unbekannt).
- **Fehlt:** Kennwerte für Beton, Mauerwerk, Putz, Holz, Estrich (Merkblatt SIA 2001 «Wärmeschutzdaten» bzw. SN EN ISO
  10456), Rsi/Rse in aktueller Ausgabe (SIA 180:2014 bzw. SN EN ISO 6946), Fensterkennwerte, Wärmebrückenkatalog.
- **Folge für 1c:** Die Bürobibliothek startet leer. Kennwerte kommen aus dem IFC (Pset_MaterialThermal), aus
  Datenblättern oder von Christian, je mit Quelle. Die Richtwerte im KI-Planungsbüro (`app.js`, «nicht bestätigte
  Normwerte») werden **nicht** übernommen.

## Entscheide während des Baus

- **Decke erzeugen (B3):** Höfe werden gefüllt (Decke hat kein Loch), mit Hinweis je Fläche ohne Raum ab 1 m²; eine
  Decke gehört zum Geschoss darüber (sie ist dessen Boden), Boden und Dach zum gewählten; doppelt = Überschneidung in
  der Höhe und > 50 % der neuen Fläche; Oberkante = Geschosskote (OK fertig Boden; ein Bodenaufbau wird nicht
  abgezogen); `bezuege.umrissAus` = Geschoss, aus dessen Wänden der Umriss stammt.
- **Anschluss Wand–Decke (B6, Hauptlauf):** «Decke läuft durch» – im Schnitt und im 3D endet die Wand an der
  Unterkante einer darüberliegenden Decke; die Wand-Geometrie selbst bleibt unverändert.
- **IFC-Hülle (B4):** Bauteile ohne Schichten, aber mit U aus der Datei, bekommen einen Aufbau ohne Schichten, der nur
  das U trägt (BKW); gleiche Schichtfolge = derselbe Aufbau; Materialien gleichen Namens aus zwei Dateien bleiben
  getrennt (Kennung mit «@Unterlage»).
- **Material/Aufbau (B2):** `material_aendern` als eigener Befehl (verlangt Quelle); Kennwerte ohne Quelle gelten in der
  Rechnung als fehlend, auch wenn sie über element_aendern kamen; Uw-Annahme: Verglasung dem Fenster ähnlich.
- **Reststück im Wandband (Abschluss, Befund B5):** Ein Stück von Boden oder Decke ohne Raum gegenüber, das im Wandband
  einer Wand des Nachbargeschosses liegt (Innenwand darunter bzw. darüber), wird an der Wandachse geteilt und je Hälfte
  dem Raum auf dieser Seite zugeschlagen (Angrenzung wie dieser Raum). Ohne Raum auf einer Seite gilt dort die alte Regel
  (aussen/erdreich). Vorher galt der Streifen als «aussen» – Lücke der Spezifikation, nicht des Baus. Umsetzung (B1-W):
  Wände des Nachbargeschosses nach demselben Höhenfenster wie die Räume; die Wand mit der grössten Überdeckung zuerst;
  Suchlinie halbe grösste Wanddicke + 90 mm über das Stück hinaus (sonst bleibt das Feld einer Stichwand an der Querwand
  ungeteilt); Abschnitte unter 40 mm vor der Querwand-Teilung zusammengefasst (sonst teilen Splitter einer 1 mm schiefen
  Wand eine Querwand). BKW: 588.6 m² gehen von aussen/Erdreich/Dach zu den Räumen.
- **Darstellung «Hülle» (C1b):** eigene Ebene `huelle` zwischen Decke und Wand, nur am Bildschirm (nicht im PDF);
  Streifen 2 Papier-mm auf der Raumseite jeder Raumgrenze, Farbe nach Angrenzung; beheizt dezent; im 3D Flächen 12 mm
  (Wand) bzw. 24 mm (Decke/Boden) in den Raum versetzt, Architektur blasser; Legende mit Flächen je Angrenzung in
  Innenmass; Klick auf eine Fläche zeigt Raum, Nachbar, Bauteil, Angrenzung, Fläche.

## Annahmen für Christians Prüfliste

0. **Quelle der Kennwerte (Entscheid):** Welche Tabellen gelten für Baustoffe, Rsi/Rse, Fenster und Wärmebrücken
   (aktuelle Ausgaben beschaffen?), und soll SIA 2001:2005 als Dämmstoff-Vorlage in die Bürobibliothek?

1. Raumgrenzen in Innenmassen; Aussenmass-Fläche zusätzlich je Bauteil. Welcher Massbezug für welche Norm gilt,
   legst du fest.
2. `Raum.beheizt` Vorgabe «ja».
3. Erdreich: unterstes Geschoss und alles unter `Gebaeude.terrain` (Vorgabe ±0).
4. Toleranz Raumkante ↔ Wandband 20 mm; Versatz zur Nachbarsuche Dicke + 50 mm.
5. Flache Dächer nur; Dach-Oberkante = Kote + Geschosshöhe des obersten Geschosses.
6. Rsi/Rse und alle Kennwerte nur mit Quelle (ohne: kein U, nur R).
7. Projekt-Nord = +y, solange kein `nordwinkel` gesetzt ist.
8. Darstellung Anschluss Wand–Decke: «Decke läuft durch» (Wand endet an UK Decke) in Schnitt und 3D.
9. Erzeugte Decke: Oberkante = OK fertig Boden des Geschosses darüber (kein Bodenaufbau abgezogen); Höfe gefüllt.
10. Boden ohne Raum darunter, Unterkante auf oder unter Terrain (± 20 mm) → Erdreich, auch wenn nicht unterstes Geschoss.
11. Wärmebrücken-Vorschläge werden mit ihrer Wand gelöscht; Deckenauflager an der Aussenseite samt Ecken gemessen.
12. Räume aus Wänden beim IFC-Übertrag (1b; `raum_erkennen` beim Zeichnen bildet keine Inseln): Wände, deren Oberkante höchstens 20 mm über der Geschosskote liegt (Fundament, Sockel unter
    dem Boden), trennen keine Räume; ein Raum aus allen Wänden bleibt zusätzlich, wenn er in keinem so gebildeten liegt.
    (Die strengere Regel «mehr als halbe Geschosshöhe im Geschoss» hätte bei BKW 216 echte Räume gelöscht – versetzte
    Gebäudeteile.) Inselräume werden vom umschliessenden Raum abgezogen (Steg der Breite 0), keine Überlappungen.
    Wärmebrücken-Vorschläge gehen auch mit einer gelöschten Öffnung oder Decke mit.
13. Anschluss: Eine Wand, die ganz in einer Platte läge (BKW: Wände unter geneigten IFC-Dächern, die als Kasten kommen),
    bleibt ganz – dort bleibt die Doppelfüllung sichtbar, bis geneigte Dächer importiert werden. Nur Wände werden gekürzt,
    Stützen und Unterzüge nicht.
14. Farben der Darstellung «Hülle»: aussen rot (#e0482f), Erdreich braun (#8a6a3b), unbeheizt blau (#3f7fd1), beheizt
    grün (#5aa469, dezent). Nur Bildschirm; soll die Hülle auch auf den Plan (z. B. als Nachweisplan)?
15. Wärmebrücken-Vorschläge: Deckenauflager nur an Wänden, an denen ein **beheizter** Raum gegen aussen oder Erdreich
    grenzt (thermische Hülle; Kelleraussenwände zählen mit), kein Auflager unter einem Dach (Attika nicht in 1c);
    Anschlag («fensteranschlag») auch für Aussentüren, Länge = Umfang 2 × (Breite + Höhe).
16. Boden-/Deckenstreifen über bzw. unter einer Innenwand des Nachbargeschosses: an der Wandachse geteilt, je Hälfte zum
    Raum auf dieser Seite (siehe Entscheide); über einer dickeren Aussenwand darunter geht die Innenhälfte zum Raum, die
    Aussenhälfte bleibt aussen. Fugen zwischen Raum und Wand über 20 mm liegen ausserhalb des Bands und bleiben
    aussen/Erdreich (BKW: 160 Stücke, zusammen 3.95 m²) – grössere Toleranz wäre dein Entscheid.
