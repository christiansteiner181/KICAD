# Modul 1b – Grundlagen, Räume, openBIM, IFC-Übertrag: Spezifikation

Stand 17.10.2026 (Hauptlauf). Verbindlich für alle Bauaufträge von Modul 1b. Zuerst `LIESMICH.md` lesen, dann
`entwurf/modul-1a.md` (Grundsätze der Grafik), dann diese Datei. Konzept und Entscheide: `entwurf/modul-1b-ifc.md`.
Christian 17.10.2026: «mache alles in 1» – Modul 1b ganz, auch DWG und Fachplaner-IFC.

## Hauptfall (Christian 17.10.2026)

«Der Hauptfall wird sein, dass wir ein Architekten-IFC importieren und auf dieser Basis arbeiten. Revit behandelt dies
wie ein PDF. Wir können darin keine Aussparungen automatisieren, noch Räume vernünftig gebrauchen, noch eine korrekte
Kollisionsprüfung durchführen.» Daraus folgt für 1b:
- Das Architekten-IFC wird zu **echten Elementen** (Wand, Decke, Dach, Stütze, Unterzug, Tür, Fenster, Raum), nicht
  zu einem Hintergrund. Nur was wirklich keine Klasse hat (Treppe, Geländer, Möbel …), bleibt Hintergrund.
- Die **exakte IFC-Geometrie bleibt für jedes Produkt erhalten** (Netzdatei der Unterlage, auch für übernommene
  Elemente, mit Kennzeichnung «übernommen»): Grundlage für Kollisionsprüfung und automatische Aussparungen
  (neues Modul 3b) – die vereinfachte KICAD-Geometrie (Achse, Dicke, Höhe) dient zum Zeichnen und Bearbeiten. Wird ein
  übernommenes Element in KICAD geändert, gilt für dieses Element nur noch die KICAD-Geometrie (Hinweis).
- **Räume** sind vollwertig: aus IfcSpace, oder – wenn das IFC keine hat (Archicad) – **automatisch aus den
  übernommenen Wänden** je Geschoss (geschlossene Flächen; ohne Nummer, mit Hinweis «Nummer und Name ergänzen»).
  HLKS-Elemente bekommen ihren Raum aus der Lage.
- **Stützen und Unterzüge** sind eigene Klassen (`Stuetze`, `Traeger`) – Durchbrüche in Unterzügen sind typische
  Aussparungen.

## Ziel und Abnahme

**Fertig, wenn …**
1. Bernstrasse 42 ist mit Räumen geladen: die 37 Räume aus dem HLKS-IFC von H+K (Revit, IFC2x3); das Architekten-IFC
   (Archicad, IFC2x3) hat keine Räume – dort entstehen sie automatisch aus den übernommenen Wänden.
2. Wände, Decken, Stützen, Unterzüge, Türen und Fenster des Architekten-IFC sind als KICAD-Elemente übernommen
   (Bernstrasse IFC2x3 und BKW Hauptsitz Archicad IFC4), mit GlobalId und exakter IFC-Geometrie in der Netzdatei;
   Türen und Fenster sitzen in ihrer Wand.
3. Leitungen, Formteile, Armaturen und Apparate eines Fachplaner-IFC sind übernommen, mit Anschlüssen, Verbindungen
   und Systemen (BKW Hauptsitz Trimble Nova IFC4: Heizung/Kälte, Sanitär, Lüftung; Bernstrasse H+K Revit IFC2x3).
4. Dateien in IFC4 und IFC2x3 gelingen; der Katalog ist gegen IFC4 und IFC2x3 abgeglichen (`kataloge/ifc.json`).
5. Eine neue Fassung derselben Datei gleicht über die GlobalId ab (neu, geändert, gelöscht als Vorschau, ein Paket).
6. DWG, DXF, PDF und Bild liegen je Geschoss als kalibrierte Unterlage im Grundriss (DWG direkt mit ACadSharp, ohne
   ODA File Converter).
7. **Ausrichtung transparent** (Christian 17.10.2026, siehe unten): jede Datei hat einen Lagebericht, nie wird
   stillschweigend verschoben.

## Testdaten

Klein, im Repo: `tests/daten/minimal_ifc4.ifc`, `tests/daten/minimal_ifc2x3.ifc` (Projekt in mm, Gebäude, EG Kote 0,
1.OG Kote 3000, Wand 5000 × 200 × 2600 mit Achse (0,0)→(5000,0) im EG, Raum 1.01 «Wohnen» 4 × 3 m). Erzeugt mit xBIM;
weitere Testdateien so erzeugen (Muster: `IfcStore.Create(…, XbimSchemaVersion.Ifc4 | Ifc2X3, InMemoryModel)`, Klassen aus
`Xbim.Ifc4.*` bzw. `Xbim.Ifc2x3.*`) oder als STEP-Text in den Test schreiben.

Echt, nur lokal (nie ins Repo, nie nach OneDrive): `%USERPROFILE%\HLKS_CAD_testdaten\` – Tests damit überspringen sich,
wenn die Datei fehlt (`IfcHilfe.Echt(...)`). Kopien vom NAS (nur lesen), Stand 17.10.2026:

| Datei | Inhalt | Fassung, Programm | Befund Probelauf |
| --- | --- | --- | --- |
| `bernstrasse42\architekt_3d.ifc` (101 MB) | Architektur Bernstrasse 42 | IFC2x3, Archicad 27, Meter | 402 IfcWall (380 mit Achse, Körper CSG/SweptSolid/Brep), 73 IfcSlab, 57 IfcDoor, 36 IfcWindow, 113 Füllungen, 49 Schichtaufbauten, 3691 IfcBuildingElementPart, **keine IfcSpace** |
| `bernstrasse42\hlks_hk_2026-02-10.ifc` (109 MB) | H+K HLKS + verknüpfte Architektur | IFC2x3, Revit 2025, mm | 1952 IfcFlowSegment (SweptSolid), 1426 IfcFlowFitting (Mapped), 140 IfcFlowTerminal, 64 IfcFlowController, 7971 Ports, 3845 Verbindungen, 196 Systeme, 37 IfcSpace, Geschosse doppelt (zwei Gebäude) |
| `bernstrasse42\hlks_hk_2024-09-17.ifc` (15 MB) | ältere Fassung davon | IFC2x3, Revit 2024 | für den Abgleich alter → neuer Fassung |
| `bernstrasse42\architekt_eg.dwg`, `sanitaer_eg.dwg`, `schema_wasser.dxf` | Grundriss EG Architekt (AC1024, m), Sanitär EG (AC1032, mm), Schema (DXF) | | ACadSharp liest alle drei (Line, LwPolyline, Hatch, MText, Dimension, Arc, Insert) |
| `bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc` (86 MB) | Architektur BKW Hauptsitz | IFC4, Archicad 28, Meter | 2411 IfcWall (Achse IndexedPolyCurve, Körper PolygonalFaceSet), 804 IfcSlab, 621 IfcWindow, 299 IfcDoor, 1185 Füllungen, keine IfcSpace |
| `bkw_hauptsitz\BKW-HS_1_HK-GEBT_NOV.ifc` (24 MB) | Heizung/Kälte | IFC4, Trimble Nova, Meter | 1521 IfcPipeSegment, 1910 IfcPipeFitting, 238 IfcValve, 32 IfcPump, 13 IfcFlowMeter, 7 IfcHeatExchanger, 4 IfcTank; Geometrie MappedItem → FaceBasedSurfaceModel; 6966 Ports, 3483 Verbindungen, 15 Systeme |
| `bkw_hauptsitz\BKW-HS_1_SA-GEBT_NOV.ifc` (1.3 MB) | Sanitär | IFC4, Nova | 99 IfcPipeSegment, 112 IfcPipeFitting, 1 IfcTank, 53 Proxy, 362 Ports |
| `bkw_hauptsitz\BKW-HS_1_LU-GEBT_NOV.ifc` (0.9 MB) | Lüftung | IFC4, Nova | 75 IfcDuctSegment, 81 IfcDuctFitting, 429 Proxy, 260 Ports |

## Grundsätze

- **Eine Tür:** Übernahme nur über den Befehl `ifc_uebernehmen` (Trockenlauf, ein Paket, rückgängig). Prüfen ohne
  Änderung: Abfrage `ifc_pruefen`. Unterlagen: Befehle `unterlage_einfuegen`, `unterlage_kalibrieren` (B3).
- **Kennungen:** Jedes übernommene Element trägt `kennungen.ifc` (GlobalId) und `kennungen.quelle` (Id der Unterlage).
  Abgleich ausschliesslich über die GlobalId. Herkunft der Werte: Art `ifc`, Quelle «Datei (Fassung, Programm), Entität
  #Nummer, Pset.Eigenschaft».
- **Nichts erfinden:** Was die Datei nicht sagt, bleibt leer (mit Hinweis). Insbesondere **keine Nennweite aus einem
  Aussendurchmesser raten** (das wäre ein Normwert aus einer Rohrreihe) – Aussendurchmesser als Hinweis melden.
- **Nicht Verstandenes** wird nicht weggeworfen: es bleibt als Hintergrund-Netz der Unterlage sichtbar (Grundriss-Schnitt
  und 3D) und erscheint in der Übersicht «nicht verstanden» je Entität.
- **Der Kern erzeugt die Grafik** (wie 1a): Unterlagen zeichnen sich als Plangrafik-Primitive (Ebene `grundlage`, ganz
  unten) und als Körper; PDF/Bild über ein neues Primitiv `bild`.
- **Grosse Dateien:** ein Import = ein Paket. xBIM lädt 100 MB in 5–8 s; Ziel: Übernahme Bernstrasse Architektur unter
  2 min, ohne den Arbeitsplatz zu blockieren (Fortschritt über Hinweise genügt in 1b).

## Ausrichtung – transparent (Entscheid Christian 17.10.2026)

«Mit Revit haben wir immer Probleme bei der Ausrichtung der Modelle, besonders beim Export. Dies muss transparent
aufgebaut werden.» Umsetzung (`src/Hlks.Kern/Import/IfcAusrichtung.cs`, gebaut in Phase A):

1. **Projektbezug** (Projektinformation `ifcBezug`): IFC-Weltpunkt (mm), der im Projekt Ursprung und ±0.00 ist, und
   Drehung; dazu `quelle` (Datei), `landes` (Landeskoordinaten des Projektursprungs und Nordwinkel, wenn die Datei
   georeferenziert ist: IFC4 IfcMapConversion bzw. ePSet_MapConversion) und `koordinationskoerper` (Lage in
   Projektkoordinaten, wenn die Datei einen hat). Die erste IFC-Datei setzt ihn – mit Hinweis und Zahlen.
2. **Lagebericht je Datei** (Unterlage `lage`, auch in `ifc_pruefen`): Gelände und Gebäude in den Weltkoordinaten der
   Datei, ±0.00, geografische Breite/Länge, Landeskoordinaten, Koordinationskörper, eigener Bezug, Abweichung.
3. **Prüfung jeder weiteren Datei** gegen den Projektbezug, genaueste Grundlage zuerst: Koordinationskörper ›
   Landeskoordinaten › Gebäudeursprung (nur Hinweis). Toleranz 10 mm / 0.01°.
4. **Weicht sie ab, wird nichts übernommen**, sondern mit Zahlen gemeldet («−1.250 m Ost, …») und eine ausdrückliche
   Wahl verlangt: `ausrichtung` = `projekt` (Weltkoordinaten der Datei wie sie sind) | `gebaeude` (Gebäudeursprung der
   Datei auf den Projektursprung) | `koordinationskoerper` (Koordinationskörper aufeinander). Der Trockenlauf zeigt die
   Warnung und rechnet mit dem Projektbezug. Die Wahl steht in der Unterlage und gilt für neue Fassungen, solange die
   Abweichung gleich bleibt; ändert sie sich, wird erneut gefragt.
4a. **Inhaltsvergleich** (Befund Abnahme 18.10.2026): Ursprünge allein genügen nicht – das H+K-IFC von Bernstrasse hat
   denselben Gebäudeursprung wie das Archicad-IFC, der Inhalt liegt aber gut 30 m daneben. Darum vergleicht die Prüfung
   zusätzlich die Begrenzung des Datei-Inhalts (Platzierungen der Bauteile, ohne Ausreisser) mit der des bestehenden
   Modells: Anteil im Modell und Versatz der Mitten, je für «projekt» und «gebaeude» (Bericht `inhalt`, `inhaltJeWahl`).
   Beruht die Lage nur auf dem Gebäudeursprung und liegen weniger als 10 % des Inhalts im Modell, wird abgelehnt
   («ACHTUNG Inhalt …»), auch wenn der Ursprung passt. Koordinationskörper und Landeskoordinaten gelten als
   ausdrückliche Abmachung: dort meldet der Bericht die Überdeckung nur (Nebengebäude dürfen woanders liegen). Trifft
   keine Wahl, empfiehlt der Text Koordinationskörper oder einen ausdrücklichen `bezug`.
5. **Koordinationskörper:** Objekt, dessen Name oder ObjectType «Koordinationskörper» enthält (anderer Name: Parameter
   `koordinationskoerper`). Empfehlung an die Fachplaner (Prüfliste Christian): in jedem Modell denselben Körper.
6. **Export (Modul 7, hier nur festgelegt):** schreibt denselben Bezug ausdrücklich – IfcSite-Lage und (IFC4)
   IfcMapConversion/IfcProjectedCRS mit den gespeicherten Landeskoordinaten, wahlweise «Projektursprung» oder
   «Landeskoordinaten» als Weltursprung; legt einen **Exportbericht** mit allen Zahlen bei (Ursprung, ±0.00 über Meer,
   Nordwinkel, Koordinationskörper) samt Hinweis für Revit («Verknüpfen: Automatisch – Gemeinsame Koordinaten» bzw.
   «Ursprung zu Ursprung», je nach Wahl); prüft sich selbst (eigenen Export wieder lesen → Abweichung 0).
7. **Oberfläche (C1):** Lagebericht im Übernahme-Dialog sichtbar (Tabelle, Warnung rot, Wahl der Ausrichtung mit
   Erklärung); in der Projektinformation ein Bereich «Koordinaten» mit Projektbezug und Quelle.

## Phase A – Fundament (Hauptlauf, erledigt 17.10.2026)

| Datei | Inhalt |
| --- | --- |
| `kataloge/ifc.json`, `Kataloge/IfcAbbildung.cs` | Abbildung Klasse ↔ IFC4/IFC2x3 (Entität, Typobjekt, PredefinedType, ObjectType, Psets), Systemtypen ↔ IfcDistributionSystemEnum + Namensmuster, Fluss ↔ IfcFlowDirectionEnum; `KlasseFuer`, `SystemtypFuer`, `FlussAus` |
| `kataloge/klassen.json` | neue Klasse **Unterlage** (Schicht `grundlage`), Projektinformation `ifcBezug`; IFC-Entitäten an ifc.json angeglichen |
| `Modell/Grundtypen.cs` | `Element.Kennungen` (ifc, quelle; später hkap) |
| `Import/Matrix.cs` | 4×4-Lagen |
| `Import/IfcDatei.cs` | Öffnen (nur IFC4/IFC2x3), Einheit, `Welt`, `Lage(produkt)` (Projektkoordinaten), `Achsen`, `Punkt`, Bezug, `Geschosse()` (zusammengefasst), `GeschossVon`, `Eltern`, `Typ`, `PredefinedType`, `ObjectType`, `Eigenschaften` (Psets + Mengen, «Pset.Name»), `LaengeMm`, `Erster`, `Lage()` (Lagebericht), `Karte()` |
| `Import/IfcAusrichtung.cs` | siehe oben |
| `Import/IfcLauf.cs` | `IIfcUebertrag` (Bereich, Klassen, Uebertragen), `IIfcGrundlage` (Netz), `IfcLauf` (Neu, Wert, Verbinden, GeschossId, ImFilter, Ref, NichtVerstandenZaehlen) |
| `Import/IfcUebernahme.cs` | Befehl `ifc_uebernehmen`, Abfrage `ifc_pruefen`; Übertrager und Netz-Erzeuger werden per Reflexion gefunden (eigene Datei je Bereich, keine Registrierung nötig) |
| `Import/Unterlagen.cs` | Ablage im Ordner `unterlagen` (Prüfsumme + Name, nie überschreiben), `Pfad`, `Ableitung` (abgeleitete Dateien einmal erzeugen) |
| `Grundlagen/GrundlagenBefehle.cs` | leere Registrierung für B3 |
| `Hlks.Mcp/WerkzeugeImport.cs` | MCP `ifc_pruefen`, `ifc_uebernehmen` |
| `tests/Hlks.Kern.Tests/IfcHilfe.cs`, `IfcGrundgeruestTests.cs` | Hilfen und 7 Rauchtests |

**Schnittstelle für die Übertrager** (verbindlich): Ein Übertrager ist eine Klasse mit parameterlosem Konstruktor, die
`IIfcUebertrag` umsetzt. In `Uebertragen(lauf)`:
- Produkte suchen (`lauf.Datei.Modell.Instances.OfType<IIfc…>()`), nur `lauf.ImFilter(x)`;
- Element planen: `var e = lauf.Neu("Wand", x);` – setzt Id `ifc:<GlobalId>`, Name, Geschoss, Kennungen, Herkunft und
  markiert das Produkt als übernommen; dann `e.Geometrie = …` (Projektkoordinaten über `lauf.Datei.Lage(x)` bzw.
  `Modellpunkt`), `lauf.Wert(e, "material", "beton", "Pset_…")`, Verweise mit `IfcLauf.Ref(anderes)` (z. B.
  `e.Bezuege["wand"] = IfcLauf.Ref(wand)`, `e.System = IfcLauf.Ref(system)`), Anschlüsse `e.Anschluesse`;
- Verbindungen: `lauf.Verbinden(a, "anschlussA", b, "anschlussB")`;
- Nicht Verstandenes: `lauf.NichtVerstandenZaehlen(x)` (bleibt unbehandelt → Hintergrund), Hinweise `lauf.Hinweise`.
- Kennzeichen: leer lassen (der Kern vergibt sie) – ausser es gibt ein sprechendes (Raumnummer, Achsbezeichnung,
  Systemkürzel); bei Doppelung vergibt der Kern ein neues.
`IfcUebernehmen` löst die Verweise auf, gleicht ab und übernimmt; gelöscht werden nur Elemente der `Klassen` des
Übertragers aus derselben Unterlage.

## Phasen und Zuteilung

| Phase | Auftrag | Agent | Dateien (nur diese) |
| --- | --- | --- | --- |
| A | Fundament (oben) | Hauptlauf | erledigt |
| B1 | IFC-Geometrie und Netzdatei; Übertrag Architektur (Wand, Decke, Dach, Stütze, Unterzug, Tür, Fenster, Achse) und Räume (auch aus Wänden) | hlks-kern | `src/Hlks.Kern/Import/IfcGeometrie*.cs`, `IfcNetz*.cs`, `IfcArchitektur*.cs`, `IfcRaeume*.cs`, `src/Hlks.Kern/Grafik/GrundlagenIfc.cs` (eine `IGrundlagenArt` für «ifc»), `Grafik/ArchitekturGrafik.cs` (nur Stuetze, Traeger ergänzen), Tests `tests/Hlks.Kern.Tests/IfcArchitektur*Tests.cs`, `IfcNetz*Tests.cs`, `IfcRaeume*Tests.cs` |
| B2 | Übertrag HLKS (Systeme, Leitungen, Formteile, Armaturen, Apparate, Anschlüsse, Verbindungen) | hlks-kern | `src/Hlks.Kern/Import/IfcHlks*.cs`, Tests `tests/Hlks.Kern.Tests/IfcHlks*Tests.cs` |
| B3 | Unterlagen DWG/DXF, PDF, Bild: einfügen, kalibrieren, Plangrafik (Primitiv `bild`, schon in Grafiktypen), PDF-Ausgabe | hlks-modul | `src/Hlks.Kern/Grundlagen/*` (darin eine `IGrundlagenArt` für dwg, dxf, pdf, bild), `Plan/PdfZeichner.cs` (nur `bild`), `src/Hlks.Mcp/WerkzeugeGrundlagen.cs`, Tests `tests/Hlks.Kern.Tests/Grundlagen*Tests.cs`, `tests/Hlks.Mcp.Tests/WerkzeugTests.cs` (nur Zählung und Aufrufe der neuen Werkzeuge) |
| B4 | Tests: Abbildung gegen die Schemaklassen, Lage/Ausrichtung, echte Dateien (Rauchtests) | hlks-test | nur `tests/` |
| C1 | Oberfläche: Register «Einfügen» (IFC übernehmen mit Vorschau und Lagebericht, CAD/PDF/Bild einfügen mit Hochladen, Kalibrieren), Unterlagen im Projektbrowser, Netz im 3D, REST | hlks-modul | `wwwroot/js/…`, `src/Hlks.Web/Program.cs` (nur neue Endpunkte), `OBERFLAECHE.md` |
| D | Gesamttest, Abnahme mit den echten Dateien, Bericht | Hauptlauf, hlks-test | |

`kataloge/*.json`, `Import/IfcDatei.cs`, `IfcLauf.cs`, `IfcUebernahme.cs`, `IfcAusrichtung.cs`, `IfcAbbildung.cs` ändert
nur der Hauptlauf. Braucht ein Auftrag dort etwas, meldet er es (mit Vorschlag) – nicht selbst ändern.

## B1 – Geometrie, Hintergrund, Architektur, Räume

**Geometrie** (`IfcGeometrie`): aus der Body-Repräsentation eines Produkts ein Dreiecksnetz in Projektkoordinaten (mm).
Abdecken (Probelauf): IfcExtrudedAreaSolid (Profile Rectangle, Circle, CircleHollow, ArbitraryClosed/ArbitraryProfileDefWithVoids
mit Polyline, IndexedPolyCurve, CompositeCurve, TrimmedCurve/Kreisbogen), IfcFacetedBrep, IfcFaceBasedSurfaceModel,
IfcShellBasedSurfaceModel, IfcPolygonalFaceSet, IfcTriangulatedFaceSet, IfcMappedItem (mit
IfcCartesianTransformationOperator), IfcBooleanResult/IfcBooleanClippingResult (erster Operand, Hinweis «vereinfacht»),
IfcCsgSolid soweit einfach. Unbekanntes → leeres Netz + Zählung. Dazu Achse (Repräsentation «Axis»: Polyline,
IndexedPolyCurve, TrimmedCurve auf Linie) und Profil-Umriss (für Decken, Räume) als Polygon, wo die Geometrie es direkt
hergibt (Extrusion senkrecht) – sonst aus dem Netz (Grundfläche: Vereinigung der nach oben zeigenden Dreiecke, Clipper2).

**Netzdatei** (`IIfcGrundlage`): je Unterlage eine Datei (`Unterlagen.Ableitung`, Format binär und kompakt, Format im
Dateikopf dokumentieren: je Produkt EntityLabel, GlobalId, Entität, Geschoss-Kote, Kennzeichen «übernommen» ja/nein,
Punkte float32 in Projekt-mm, Dreiecke uint32). **Alle Produkte mit Geometrie** kommen hinein, auch die übernommenen
(exakte Geometrie für Kollision und Aussparungen, Modul 3b); `IfcUebernehmen` übergibt dafür alle Produkte und die
Menge der übernommenen (Hauptlauf passt die Schnittstelle an: `Erzeugen(datei, produkte, uebernommen, ziel)`). Lesen:
eine kleine Klasse `IfcNetzdatei` (öffnen, Produkte nach GlobalId, Dreiecke) für Grafik und später Kollision. Grafikquelle für Unterlagen der Art `ifc` (`GrundlagenGrafik`, IFC-Teil): Grundriss =
Schnitt der **nicht übernommenen** Netze mit der Ebene z = Schnitthöhe (Liniensegmente, Stift dünn, Farbe grau #8a8a8a, Ebene `grundlage`,
`Deckkraft` aus der Unterlage), schnell genug für 100 000 Dreiecke je Geschoss (Ergebnis je Höhe zwischenspeichern);
Körper = ein Koerperteil Art `netz` mit `Quelle` = Id der Unterlage (das 3D lädt die Netzdatei über REST, C1; Art
`netz` und `Quelle` stehen schon in `Grafiktypen.cs`). Die Grafikquelle `GrundlagenGrafik` (Phase A) ruft für Unterlagen
der Art «ifc» die `IGrundlagenArt` aus `GrundlagenIfc.cs` (B1) auf.

**Architektur** (Bereich `architektur`, Klassen Wand, Decke, Dach, Stuetze, Traeger, Tuer, Fenster, Achse):
- Wand: Achse aus der Axis-Repräsentation (sonst Mittellinie des Netzes in Längsrichtung), Dicke aus
  IfcMaterialLayerSetUsage (Summe der Schichten; OffsetFromReferenceLine und DirectionSense beachten, damit die Achse
  in der Wandmitte liegt), sonst Qto/Pset (Width), sonst Netz quer zur Achse; Höhe und Unterkante aus dem Netz
  (z-Bereich) bzw. Qto Height; je gerades Stück eine Wand (gekrümmte Wände → Hintergrund mit Hinweis); `material` aus
  dem Namen des dicksten tragenden Materials auf die Auswahl der Klasse abgebildet (beton, backstein, kalksandstein,
  porenbeton, holz, leichtbau; sonst nicht setzen) mit Fundstelle;
  `aufbau` = Schichten als Text («Beton 200 / Dämmung 160 / Putz 15»). Mehrfach-Wände (IfcBuildingElementPart) gehören
  zu ihrer Wand (Aggregat) und werden nicht einzeln übernommen.
- Decke/Dach: Umriss als Polygon (Profil oder Netz-Grundfläche, mit Löchern zulässig als Ring ohne Löcher, Löcher
  als Hinweis), z = Oberkante, `geometrie.dicke`.
- Tür/Fenster: über IfcRelFillsElement → IfcOpeningElement → IfcRelVoidsElement → Wand. `bezuege.wand`, `abstand` =
  Projektion der Öffnungsmitte auf die Achse, `breite` = OverallWidth (sonst Öffnungsbreite), `hoehe` = OverallHeight,
  `bruestung` = Unterkante − Geschosskote; Tür `seite`/`band` aus der Lage des Türblatts bzw. OperationType (sonst
  Vorgabe, Hinweis). Ohne Wirtswand → Hintergrund + Hinweis. Die Nachführung (1a) setzt den Punkt aus `abstand`.
- Stütze: Querschnitt (Profil bzw. Netz-Grundfläche) als Polygon, Höhe, Unterkante; Unterzug/Träger: Achse
  (Axis-Repräsentation bzw. Längsrichtung des Netzes), Breite, Höhe, Unterkante; `material` wie bei der Wand. Dazu
  Plangrafik (Schnitt dick mit Schraffur, wenn geschnitten; sonst Sichtkante bzw. Unterzug gestrichelt über der
  Schnitthöhe) und Körper in `ArchitekturGrafik` ergänzen (nur diese zwei Klassen; bestehendes nicht ändern).
- Achse: IfcGrid → je IfcGridAxis eine Achse (`bezeichnung` = AxisTag, Linie in Projektkoordinaten).
- Treppen, Geländer, Bekleidungen, Möbel, Vorhangfassaden: kein Übertrag in 1b → Hintergrund.

**Räume** (Bereich `raeume`, Klasse Raum, eigener Übertrager): IfcSpace → Polygon (Profil der Extrusion bzw.
Netz-Grundfläche), `nummer` = Name, Elementname = LongName, `lichteHoehe` = Höhe; Geschoss über die Zerlegung;
Kennzeichen = Raumnummer, wenn eindeutig. Nutzung nur, wenn die Datei sie nennt (Pset_SpaceCommon bzw. ObjectType).
**Ohne IfcSpace** (Archicad): Räume automatisch aus den Wänden des Geschosses bilden (dieselbe Logik wie
`raum_erkennen`, aber für alle geschlossenen Flächen; Flächen unter 1 m² und die Aussenfläche weglassen), Kennzeichen
R-n, ohne Nummer, Hinweis «Nummer und Name ergänzen»; Kennung `quelle` = Unterlage, `ifc` = abgeleitete Kennung
(«raum:<Geschoss-GlobalId>:<Schwerpunkt gerundet auf 100 mm>»), damit eine neue Fassung abgleicht.
**Echte und abgeleitete Räume nie übereinander** (Befund Abnahme 18.10.2026): Liegt schon ein echter Raum an der Stelle,
entsteht kein abgeleiteter. Kommt ein IfcSpace-Raum später, übernimmt er den abgeleiteten Raum, dessen innerer Punkt in
ihm liegt (Id und Bezüge bleiben; Nummer, Name, Umriss und Kennungen aus dem IFC); weitere abgeleitete Räume darin werden
ersetzt (Bezüge umgehängt). Ein in KICAD geänderter abgeleiteter Raum bleibt unangetastet, mit Hinweis.

Tests: synthetische Dateien (IFC4 und IFC2x3) für jeden Fall; echte Dateien als Rauchtest mit Zahlen (Anzahl Wände,
Türen in Wänden, Räume) – überspringen, wenn sie fehlen. Abnahmefall Bernstrasse: Archicad-IFC übernehmen, danach
`raum_erkennen` in einem Raum des EG gelingt.

## B2 – HLKS

Bereich `hlks`, Klassen: alle Katalogklassen mit Rolle (Leitung, Formteil, Armatur, Apparat, Geräte) sowie Anlage und
System (Strang nur aus IfcDistributionCircuit).
- Klasse über `Abbildung.KlasseFuer(schema, entität, typEntität, predefinedType, objectType)`; ohne Treffer (z. B.
  IfcFlowTerminal ohne Typobjekt, Proxy) → Name/ObjectType-Heuristik nur mit eindeutigen Mustern (WC, Waschtisch,
  Dusche …), sonst Hintergrund + `NichtVerstandenZaehlen`.
- Leitung (Rohr/Kanal): Verlauf aus den Lagen der beiden Ports (IfcDistributionPort über IfcRelConnectsPortToElement
  bzw. IFC4 IfcRelNests); fehlen Ports, aus der Extrusionsachse. `dn` nur aus Psets (`ifc.json` → psets, Port-Psets)
  – **nie aus dem Aussendurchmesser raten**; Kanal `breite`/`hoehe`/`durchmesser` aus dem Profil (geometrisch, erlaubt).
- Formteile, Armaturen, Apparate: Punkt = Lage des Produkts; `darstellung` bleibt Katalog.
- Anschlüsse: aus den Ports (Name, Fluss aus FlowDirection über `FlussAus`, Medium aus dem System) auf die
  Anschlussnamen der Katalogklasse abbilden (Reihenfolge/Richtung; Rest als zusätzliche Anschlüsse nur, wenn die
  Klasse das zulässt – sonst Hinweis). Verbindungen aus IfcRelConnectsPorts.
- Systeme: IfcSystem/IfcDistributionSystem → System (Systemtyp über `SystemtypFuer`, Medium aus systemtypen.json),
  Mitglieder über IfcRelAssignsToGroup → `e.System`. Revit zerlegt Systeme («H VL NT 6»): gleiche Basisnamen (ohne
  Nummer am Ende) zu einem System zusammenfassen. Anlage je Gewerk, wenn keine da ist. Unbekannter Systemtyp → System
  ohne Systemtyp + Hinweis (Pflichteigenschaft: dann das System nicht anlegen, Mitglieder ohne System, Hinweis).
- Die Datei des Probelaufs zuerst ansehen (Ports, Psets «Pset MEP» von Nova, Pset_FlowSegmentPipeSegment von Revit) und
  die tatsächlich vorhandenen Eigenschaftsnamen in den Bericht schreiben; neue Pset-Kandidaten dem Hauptlauf melden
  (ifc.json ändert nur er).

Tests: synthetische Netze (Rohr – Bogen – Rohr – WC mit Ports, IFC4 und IFC2x3), echte Dateien als Rauchtest (Anzahl
Leitungen, Verbindungen, Systeme; Anteil verbundener Anschlüsse).

## B3 – Unterlagen DWG/DXF, PDF, Bild

- Befehl `unterlage_einfuegen` { datei, geschoss, art?, seite?, massstab?, dpi? (PDF, Vorgabe 150) }: legt ab
  (`Unterlagen.Ablegen`), legt die Unterlage an (Geometrie = Umriss im Modell), erste Lage: DWG/DXF nach Einheit des
  Kopfs (INSUNITS → mm), PDF/Bild nach `massstab` (Pixel → mm über dpi), Ursprung unten links auf (0,0).
- Befehl `unterlage_kalibrieren` { unterlage, von: [[u1,v1],[u2,v2]] (Unterlagen-Koordinaten), nach: [[x1,y1],[x2,y2]]
  (Modell-mm), nurVerschieben? }: Ähnlichkeitstransformation (Faktor, Drehung, Verschiebung) in `transformation`;
  meldet Faktor und Drehung (Hinweis bei Faktor ≠ Erwartung um mehr als 1 %).
- Plangrafik (`GrundlagenGrafik`, Teil DWG/PDF): DWG/DXF → Linien, Polylinien, Bögen, Kreise, Ellipsen (als Polylinie),
  Text/MText, Bemassungen als Linien+Text, Blöcke (Insert) aufgelöst, Schraffuren als Umriss; Layer `ebenenAus`
  ausblenden; Stift dünn, Farbe grau, Deckkraft. Ergebnis je Unterlage zwischenspeichern (Ableitung `.dwg.json`).
  PDF → Seite als PNG (Docnet.Core, Ableitung `.seiteN.png`), Bild direkt; neues Primitiv **`bild`** (Punkte = vier
  Ecken im Modell, `Quelle` = Unterlage-Id), gezeichnet am Bildschirm (C1) und im Plan-PDF (`PdfZeichner`).
- MCP-Werkzeuge in `WerkzeugeGrundlagen.cs`, Befehle in `GrundlagenBefehle.Registrieren`.

## C1 – Oberfläche

Register **«Einfügen»** (Revit: «Einfügen»): IFC übernehmen (Datei wählen → Hochladen in `unterlagen` → `ifc_pruefen`
→ Dialog mit Übersicht: Fassung, Programm, Geschosse, Zuordnung, nicht Verstandenes, **Lagebericht** mit Warnung und
Wahl der Ausrichtung, Bereiche, Geschosse; «Vorschau» = Trockenlauf mit Liste neu/geändert/gelöscht; «Übernehmen»),
CAD einfügen (DWG/DXF), PDF/Bild einfügen, Kalibrieren (zwei Punkte auf der Unterlage, zwei Zielpunkte mit Fangen oder
Zahlen). Projektbrowser-Knoten «Unterlagen» (anzeigen/ausblenden, Deckkraft, entfernen). 3D lädt Netze (`netz`).
REST: `POST /api/unterlagen` (Hochladen, Antwort Name), `GET /api/unterlagen/{id}/netz`, `GET /api/unterlagen/{id}/bild`.

## Annahmen für Christians Prüfliste

- Ausrichtung: Toleranz 10 mm / 0.01°; Erkennung Koordinationskörper am Namen.
- Wassererwärmer/Speicher IfcTank STORAGE (IFC2x3 PRESSUREVESSEL), Wärmepumpe IfcUnitaryEquipment «Waermepumpe»,
  Hausanschluss IfcPipeFitting ENTRY, weitere in `ifc.json` → `annahmen`.
- IFC2x2 und älter: Meldung «nicht unterstützt».
- Mehrschichtige Wände als eine Wand mit Gesamtdicke; Material = dickste Schicht.
- Hintergrundfarbe grau, Deckkraft 0.5.
