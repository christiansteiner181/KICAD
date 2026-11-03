# IFC 1:1 – Bauteile aus IFC so zeigen, wie sie im IFC sind

Stand 02.11.2026 · Hauptlauf · Entwurf, Entscheide offen (unten)

## Auftrag

Christian, 02.11.2026: «auch das geometrische nachbauen von IFC familien muss passen» – «ein fenster muss 1:1 so aussehen wie im IFC.
mit allen details.» Dazu: «bei allen Familien eine detailgetreue Abbildung der Komponenten … in allen detaillierungsgraden», «Es muss
aussehen wie in den Datenblätter». Grundsatz vom selben Tag: KICAD so schnell wie möglich auf einen nutzbaren Stand wie Revit.

## Heutiger Stand (Bestandsaufnahme 02.11.2026, Stichproben im Code bestätigt)

| Stelle | Heute | Folge |
| --- | --- | --- |
| Import Tür/Fenster `IfcArchitektur.Oeffnung` (Import/IfcArchitektur.cs:528) | Element mit Punkt in der Wandachse, breite/hoehe/bruestung/seite/band | Rahmen, Flügel, Glas, Sprossen, Bank, Tiefe gehen verloren |
| Netzdatei `IfcNetz.cs` (Fassung 3) | je Produkt **ein** Netz, ohne Farben, ohne Teilkörper | Glas und Rahmen nicht unterscheidbar |
| Grundriss `ArchitekturGrafik.Tuer/Fenster` (Grafik/ArchitekturGrafik.cs:222–282) | festes Sinnbild (Aufschlag, Leibung, Rahmenquadrat, Glaslinie, Bank 50 mm) | sieht nie wie das IFC aus |
| Schnitt, Ansicht, 3D `OeffnungKoerper` (ArchitekturGrafik.cs:628) | ein Quader Breite × Tiefe × Höhe, Material tuer/glas | sieht nie wie das IFC aus |
| Original-Modus `ifcDarstellung original/beide` (Grafik/IfcDarstellung.cs, GrundlagenIfc.cs) | ganzes Netz einfarbig grau, dünne Schnittlinien, alles oder nichts je Unterlage | kein Ersatz für den Nachbau |
| `familie_netz_uebernehmen` (Familien/FamilienNetzUebernehmen.cs) | ein Körper «netz», ein Material, keine Detailgrade, Drehung nur ±45° | Familie aus IFC einfarbig und ohne Grob/Mittel |
| HLKS aus Hersteller-IFC (IfcHlks*.cs) | Klasse, Ports, Systeme; Körper aus Katalog/Familie | Herstellerform verloren |

## Vorschlag

1. **Netzdatei Fassung 4:** je Datensatz Teilnetze je IfcRepresentationItem mit Darstellungsstil (IfcStyledItem → IfcSurfaceStyle →
   Rendering/Shading: Farbe, Transparenz) und Material des Teils; Fassungen 1–3 weiter lesbar (ein Teilnetz, ohne Farbe).
2. **Element ↔ Netz:** übernommene Elemente tragen die GlobalId (`kennungen.ifc`, gibt es) und zeichnen ihr eigenes Netz aus der
   Netzdatei – Tür, Fenster, aber auch Wand, Decke, Stütze, Treppe, Geländer, sobald das IFC mehr zeigt als der Nachbau.
3. **Darstellung aus dem Netz** (statt Sinnbild/Quader), in allen Ansichten:
   - Grundriss: Schnitt durch die Teilnetze auf Schnitthöhe (Schnittlinien kräftig), Ansicht von oben unterhalb (Kanten dünn),
     Glas als eigene Linie; Türaufschlag bleibt als Sinnbild dazu (wie Revit).
   - Schnitt/Ansicht: Schnitt bzw. Projektion der Teilnetze.
   - 3D: Teilnetze mit ihren IFC-Farben und Transparenz.
4. **Familie aus IFC-Typ:** je IfcWindowType/IfcDoorType (bzw. Hersteller-Typ) einmal eine Familie mit den Teilnetzen als Körper
   (Material/Farbe je Teil), Lage und Drehung aus der IFC-Platzierung (nicht aus dem kleinsten Rechteck), Detailgrade gesetzt.
5. **Wandöffnung** aus dem Öffnungsnetz statt Rechteck, wo das IFC eine andere Leibung zeigt.
6. **Abnahme:** Vergleich KICAD ↔ IFC je Bauteil (Abweichung der Netze in mm, Bild nebeneinander) am Testprojekt und an Bernstrasse 42.

## Entscheide

- **Umfang (Christian 02.11.2026):** «dies gilt für alle importierten familien. nicht nur für fenster.» → alle aus IFC übernommenen
  Bauteile, Architektur und HLKS (Hersteller-IFC), zeichnen ihr IFC-Netz statt des Nachbaus.
- **Detailgrade:** offen (Frage gestellt, unbeantwortet). Bis zum Entscheid: in jedem Detailgrad das volle IFC mit Farben, nur die
  Strichstärken unterscheiden sich (wie eine Revit-Verknüpfung). Alternative: grob = Hülle, mittel = Hauptteile, fein = alles.

## Stand 02.11.2026

- **Schritt 1 gebaut (2e15c60):** Netzdatei Fassung 4 mit Farbabschnitten je Teilkörper (IfcStyledItem: Farbe, Transparenz; Grundfarbe aus
  der Materialdarstellung), C#- und JS-Leser; 3D zeigt das Original einer Unterlage in IFC-Farben, solange `farbeOriginal` nicht gesetzt ist.
  Bestehende Unterlagen bekommen die Farben erst mit erneutem `ifc_uebernehmen` (Fassung im Schlüssel der Netzdatei).
- **Schritt 2, 3D gebaut:** übernommene, unveränderte Elemente (dieselbe Regel wie die Kollision: `IfcUebernehmen.UnveraendertUebernommen`,
  verteilte Produkte «GlobalId:2» nur gemeinsam) zeigen in der 3D-Ansicht ihr eigenes IFC-Netz mit Farben (`Grafik.Koerper(…, ifcNetz: true)`,
  nur `/api/koerper`; `IfcOriginal.cs`). Kollision und XR/GLB behalten den Nachbau.
- **Offen:** Grundriss/Schnitt/Ansicht aus dem Elementnetz (Schnitt auf Schnitthöhe, Ansicht von oben, Türaufschlag als Sinnbild dazu);
  XR/GLB und Baustellen-Webseite mit dem IFC-Netz; Familie aus IFC-Typ mit Teilkörpern und Farben (`familie_netz_uebernehmen`), Lage aus der
  IFC-Platzierung; Wandöffnung aus dem Öffnungsnetz; Detailgrad-Entscheid.

## Stand 03.11.2026 – Deckung und xBIM-Geometrie

Christian 02.11.2026: «nach dem import müssen die beiden geometrien deckungsgleich sein», «jeder nachbau muss jeweils mit der ifc darstellung
verifiziert und korrigiert werden», «der ifc import ist die basis für alles folgende, dieser muss immer 100% passen»; Entscheid «xBIM-Geometrie
(OpenCascade)».

- **Prüfung:** Abfrage `ifc_deckung` (Nachbau ↔ IFC-Netz je Element, im System des Bauteils, Toleranz 5 mm) und Nachlauf jeder `ifc_uebernehmen`
  («Prüfung Nachbau ↔ IFC …» in den Hinweisen).
- **xBIM-Geometrie** (`IfcGeometrieXbim.cs`, Xbim.Geometry 6.3.891-netcore, OCCT 7.8.1; nur Windows, Abschalter `HLKS_XBIM_GEOMETRIE=0`): rechnet
  die Booleans der Darstellungen exakt. Netzdatei **Fassung 5**: Hauptnetz = Körper samt Zuschnitten, Öffnungen nicht ausgeschnitten (Kollision,
  Aussparungen); Anzeigenetz mit ausgeschnittenen Öffnungen (3D, Original im Grundriss).
- **Korrekturen des Nachbaus:** Tür/Fenster-Körper aus dem IFC (`koerperLinks/Rechts/Unten/Oben`, `tiefe`, `versatz`, negative `bruestung`,
  Türschwelle); Wandenden `verlAnfang/verlEnde` aus dem Körper (auch Wände aus Teilen); Decke/Dach aus mehreren Flächen: je Fläche ein Element.
- **Bernstrasse 42 (Archicad, IFC2X3), 03.11.2026: 598 von 598 deckungsgleich** (±5 mm) – Dach 11, Decke 72, Fenster 15, Tür 56, Wand 342,
  Stütze 43, Träger 49, Treppe 10.
- **Korrekturen dafür (03.11.2026):** xBIM-Netze in Weltkoordinaten, je Bezug abgebildet (Einpassen rechnet im Bezugsvorschlag); Umlaufsinn
  der Dreiecke nach dem Volumen (xBIM dreht bei gespiegelter Platzierung nicht mit); **Rückfall** für Körper, die xBIM im Ganzen verwirft
  («Boolean Result returned an empty shape» bei sich nur berührenden Flächen, 98 Archicad-Trägerteile): Differenz um Differenz mit der
  Modelltoleranz, eine, die den Körper leeren würde, ausgelassen (`IfcGeometrieXbim.Rueckfall`); Träger mit gestufter Unterseite nicht
  «geneigt» (Achse entscheidet); Stützen-Unter-/Oberkante aus dem Körper statt der Extrusion; Wandenden: kein Zusammenfallen beider Enden
  in derselben Wand; Treppe: Laufdicke gemessen (nicht gerundet), Austrittsstufe mit Umriss `austrittUmriss` (Grundriss des Körpers
  hinter der letzten Kante).
- **Nicht nachgebaut (als IFC-Original im Hintergrund):** IfcFlowTerminal 57 (HLKS-Bereich), IfcCovering 44, IfcRailing 40 (keine
  KICAD-Klasse), Archicad-Fensterelemente «Fenster-008» (IfcWall 22 ohne Achse, IfcWindow 19 mit Wirt IfcBuildingElementProxy),
  IfcWindow 2 ohne Öffnung.
- **Offen:** Grundriss/Schnitt der übernommenen Elemente aus ihrem IFC-Netz (Schritt 3), Detailgrad-Entscheid.

## Stand 03.11.2026 – Installations-IFC (Bernstrasse 42, H+K Revit 2025, IFC2X3)

- **Rohre 1634/1634, Kanäle 318/318 deckungsgleich** (Prüfprojekt nur Bereich hlks; das Revit-Modell liegt 31 m neben dem Archicad-Modell, ins
  Architekturprojekt übernimmt KICAD es nur mit ausdrücklichem Bezug).
- **Korrekturen:** Rohre ohne Serie mit ihrem Aussendurchmesser `da` (aus dem IFC-Profil) in 3D und Kollision statt Mindest-Ø 30 bzw. DN;
  Rechteckkanal-Profil nur dann Querschnitt, wenn die Extrusion längs der Achse läuft (Revit extrudiert waagrechte Kanäle auch senkrecht um
  die Höhe); gebogene **Flexkanäle** (Revit «Flexkanal rund», Brep-Schlauch) als Polylinie durch die Ringmitten (Mitte je Mantelpunkt = Punkt − r · Normale, Radius aus den Deckflächen, gerade Muffen an den Enden; `IfcHlksKoerper.Schlauch`); senkrechte Rechteckkanäle mit `querDrehung`
  (Richtung der Breite im Grundriss; Benennung bleibt «grössere Seite = Breite»); Prüfung misst Zylinder genau (r·√(1−u²) quer zur Achse).
- **Formteile, Armaturen, Apparate, Geräte** (Bogen 1375, Reduktion 107, T-Stück 105, Ventil 52, FBH-Kreis 45, Durchlässe 49 …): in KICAD
  ein Punkt mit Sinnbild, ohne Lage und Grösse – kein Nachbau messbar. In 3D und Kollision zeigen sie ihr IFC-Netz, solange unverändert;
  `ifc_deckung` zählt sie eigens («Körper aus dem IFC»). **Offen:** Familie aus dem IFC-Typ (Teilnetze, Lage aus den Ports), damit auch
  verschobene oder neu gesetzte Bauteile wie im Datenblatt aussehen.
- **Regressionsprüfung:** `IfcDeckungEchtTests` (beide Dateien, nur lokal).

## Stand 03.11.2026 – Familie aus dem IFC, unser Standard wählbar

Christian 03.11.2026: «werden die importierten objekte als familie erstellt?» – «Ja, bauen»; «standard komponenten wie rohre, kanäle usw.
werden mit unseren standard familien nachgezeichnet. alles andere wird übernommen»; «es soll wählbar sein welche bauteile mit unserem
standart erstellt werden. bsp. FBH verteiler und standard armaturen»; «es soll möglich sein, zu wählen, welche importierten bauteile dauerhaft
in den standard aufgenommen werden sollen»; «auch bei der Architektur sollte das so sein».

- **Gebaut (`Import/IfcFamilien.cs`):** beim HLKS-Import bekommt jedes Bauteil mit eigener Form, das nicht zum Standard gehört, eine Familie
  mit seinem IFC-Netz in Bauteilkoordinaten (Einfügepunkt, Achsen der IFC-Platzierung) und den Anschlüssen aus den IFC-Ports; gleiche Form und
  Anschlüsse = dieselbe Familie (Kennung `ifc-` + Prüfsumme, Name «‹IFC-Typ› (IFC)»). Das Element verweist mit `familientyp`, `drehung` bzw.
  `ausrichtung` darauf. Bernstrasse 42: die 10 WCs u. a. decken sich mit dem IFC (Prüfung misst jetzt den Familienkörper).
- **Wählbar:** `Projektinformation.ifcStandard` (Liste von Klassen samt Unterklassen) – Vorgabe Leitung, Formteil, Verteiler, Armatur; diese
  zeichnet unser Standard nach, alles andere wird übernommen.
- **Dauerhaft in den Standard:** ein Mensch schreibt die gewählte Familie mit `familie_in_bibliothek` in die Bürobibliothek (und gibt sie frei);
  ein späterer Import findet sie dort über ihre Kennung und lädt sie, statt neu zu bauen.
- **Offen:** Standardteile mit unseren Serien nachzeichnen (z. B. Ablaufrohr «Silent-db20» → Serie Geberit Silent-db20, Typ nach dem
  Durchmesser; Zuordnung IFC-Typ → Serie wählbar); Flow «Familie aus IFC: IFC einlesen › Teile wählen › Familie nachmodellieren» in der
  Oberfläche; dasselbe für die Architektur (Türen, Fenster u. a. als Familie aus dem IFC – braucht die Familiengrafik für Bauteile in einer Wand).
