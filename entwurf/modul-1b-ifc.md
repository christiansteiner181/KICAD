# Modul 1b – Grundlagen, Räume, openBIM-Abgleich und IFC-Übertrag: Konzept

Stand 17.10.2026 (Hauptlauf). Erweiterung Christian 17.10.2026: «openBIM-Abgleich wäre noch wichtig. Auch ein
Übertrag von IFC in KICAD wäre sinnvoll.» Grundlage für die Spezifikation von Modul 1b.

## 1. openBIM-Abgleich (zuerst, Grundlage für Import und Export)

Heute (geprüft 17.10.2026): 46 Katalogklassen tragen eine IFC-Entität; PredefinedType, Property Sets, Ports,
Systemarten, Klassifikation und Version fehlen. Zweifelhaft: Waschmaschine, Hausanschluss, Wassererwärmer,
Regelkreis, Datenpunkt, Reinigungsöffnung.

**Version – Entscheid Christian 17.10.2026: «IFC 4 und alle zuvor».** Umsetzung (Vorschlag):
- **Import:** IFC4 und IFC2x3. Ältere Fassungen (IFC2x2 und früher, praktisch nicht mehr im Umlauf): Annahme –
  klare Meldung «Fassung nicht unterstützt, bitte als IFC2x3 oder IFC4 ausgeben»; auf Wunsch nachrüsten (Prüfliste).
- **Export:** IFC4 als Vorgabe, IFC2x3 wählbar (für ältere Programme und Viewer).
- **IFC 4.3 ist nicht vorgesehen**; eine Datei in IFC 4.3 ergibt dieselbe Meldung wie oben.
- Die Abbildung führt je Katalogklasse beide Fassungen, weil IFC2x3 einen Teil der IFC4-Entitäten nicht kennt (dort
  stehen allgemeinere Entitäten plus Typobjekt) – jede Zuordnung wird am Schema geprüft, nicht aus dem Gedächtnis.

Umfang – **geprüft an den offiziellen Schemas von buildingSMART (IFC4 ADD2 TC1 = ISO 16739-1:2018 und IFC2x3 TC1),
nicht aus dem Gedächtnis**:
- je Katalogklasse: IFC-Entität und **PredefinedType** (z. B. WC/Waschtisch/Badewanne unterscheiden);
- Eigenschaften → **Property Sets** (Pset_…Common) und Mengen (Qto_…), z. B. `dn` → Nennweite;
- Anschlüsse → **IfcDistributionPort** (Fliessrichtung rein/raus/beide ↔ SINK/SOURCE/SOURCEANDSINK, Systemart, Nennweite);
  Verbindungen ↔ IfcRelConnectsPorts;
- Systemtypen/Medien → **IFC-Systemarten** (IfcDistributionSystem, PredefinedType);
- Gliederung: Projekt/Grundstück/Gebäude/Geschoss/Raum ↔ IfcProject/IfcSite/IfcBuilding/IfcBuildingStorey/IfcSpace;
  Anlage/System/Strang ↔ IfcSystem/IfcDistributionSystem/IfcDistributionCircuit;
- Klassifikation: BKP als IfcClassificationReference; Verweise auf bSDD, wo vorhanden;
- Ablage: Abbildung als Katalogdaten (`kataloge/ifc.json` o. ä.), eine Quelle für Import und Export;
- **IDS** (Information Delivery Specification): eigene Prüfregeln für Exporte, Prüfung in Modul 7.

## 2. IFC-Übertrag in KICAD (IFC → eigene, bearbeitbare Elemente)

Bisher geplant war nur das Architekten-IFC als Grundlage (Hintergrund + Räume). Neu werden IFC-Elemente zu
KICAD-Elementen:
- **Architektur-IFC:** Geschosse, Räume, Wände (Achse, Dicke, Höhe aus der Geometrie), Decken, Dächer, Türen und
  Fenster (in ihrer Wand, mit Breite/Höhe/Brüstung) → Klassen Geschoss, Raum, Wand, Decke, Dach, Tuer, Fenster.
  Was sich nicht als Grundkörper fassen lässt, bleibt Grundlage (Netz als Hintergrund) mit Hinweis.
- **HLKS-IFC anderer Fachplaner:** Leitungen (IfcPipeSegment, IfcDuctSegment), Formteile, Armaturen, Apparate,
  Geräte, Systeme, Stränge → Katalogklassen über die Abbildung aus Teil 1; Anschlüsse aus den Ports, Verbindungen aus
  IfcRelConnectsPorts; Nennweiten und Querschnitte aus Psets bzw. Geometrie; unbekannte Typen als «sonst» mit Hinweis.
- Jedes übernommene Element behält die **GlobalId** (Bezug `ifc`) und die Herkunft «ifc» (Datei, Version, Entität).
  Neue Fassung derselben Datei → Abgleich über die GlobalId: neu, geändert, gelöscht als Vorschau, dann übernehmen
  (ein Paket je Übernahme, rückgängig machbar).
- Übernahme mit Trockenlauf (Liste: übernommen / als Grundlage / nicht verstanden), Auswahl je Geschoss, Gewerk,
  Klasse. Ein Import = ein Paket mit vielen Operationen (NAS-Messung 0b: Einzelpakete à 0.3 s wären zu langsam).

**Fertig, wenn …** Bernstrasse 42 ist mit Räumen geladen (ein IFC ohne Räume geht über gezeichnete Raumpolygone);
Wände, Decken, Türen und Fenster des Architekten-IFC und die Leitungen und Apparate eines Fachplaner-IFC sind als
KICAD-Elemente übernommen, mit GlobalId und Verbindungen; dieselben Dateien gelingen als IFC4 und als IFC2x3; der
Katalog ist gegen IFC4 und IFC2x3 abgeglichen. (Export beider Fassungen folgt in Modul 7.)

**Voraussetzungen:** ODA File Converter bis 19.11.2026 (DWG); ein HLKS-IFC eines Fachplaners als Testdatei bis
19.11.2026 (Architektur: Bernstrasse 42 liegt vor), möglichst je eine Datei in IFC4 und in IFC2x3. Die IFC-Version
ist seit 17.10.2026 entschieden.

**Aufwand:** + 7 Arbeitstage: Modul 1b = 19.11.–04.12.2026 (statt 19.–25.11.). Der Versionsentscheid ändert daran
nichts (IFC 4.3 fällt weg, dafür kommt der IFC2x3-Export in Modul 7 dazu). Die folgenden Richtwerte stehen seit
den Erweiterungen vom 17.10.2026 (Modul 1d HKAP, Modul 2 mehrere Dateitypen, Modul 3b Kollision und Aussparungen)
bei M2 18.12.2026, M3 02.04.2027, M4 16.04.2027, M5 25.05.2027. Bau und Spezifikation: `entwurf/modul-1b.md`.
