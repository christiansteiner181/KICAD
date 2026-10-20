# Modul 7 – Ausgabe: Spezifikation Teil A (Planblatt wie H+K)

Stand 20.10.2026, 03:30 · Hauptlauf · Start auf Christians Zuruf 19.10.2026, 22:50: «Beginne danach mit den Folgemodulen.
Arbeite bis 08:00 durch.» Modul 4 Teil B, 5 und 6 brauchen Normen, die nicht vorliegen (SN 592000, SN EN 12056, SVGW W3;
für 5/6 Heizlast- und Lüftungsnormen) – darum zuerst der normunabhängige Teil von Modul 7.

## Grundlage

`entwurf/planmuster.md` (echte H+K-Pläne Bernstrasse 42, Revisionspläne 21.01.2026) und der ausführliche Katalog im
Scratchpad des Hauptlaufs (`planmuster\katalog.md`, Abschnitt 0.1; Inhalt unten übernommen). Heutiger Plankopf:
`src/Hlks.Kern/Plan/Plankopf.cs` (Kopie der Firmenvorlage aus AufgabenProtokolle, mit zusätzlichem Titelband
«Planinhalt»), Planausgabe `src/Hlks.Kern/Plan/Planausgabe.cs`, Plansätze/Serien-PDF (`Plansaetze.cs`).

H+K-Plankopf (Grundrisse) laut Katalog, von oben nach unten:
1. Absenderblock links, zwei Spalten: «H+K Planungs AG, Bolligen / Dorfmärit 9 / 3065 Bolligen / Tel: 031 / 924 25 35 /
   Mail: infob@hkplanung.ch» (fett) und «H+K Planungs AG, Thun / Kasernenstrasse 17 / 3600 Thun / Tel: 033 / 225 25 45 /
   Mail: infot@hkplanung.ch» (normal); rechts das Logo.
2. Felder links: «Projektphase:», «Projekt:» (zwei Zeilen gross: Objekt und Plantitel «<Fach> <Geschoss>»), «Plannummer:»
   («1-0768 H01»); rechts: «Erstelldatum:», «Rev. Index:» (Buchstabe) mit Datum der letzten Revision darunter,
   «Kontrolliert:» (Kürzel), «Massstab:» («1 : 50»).
3. Tabelle «Planänderungen» (Index | Datum | Beschreibung), beliebig viele Zeilen, leer «-».
- Strangschemas: Kopf **unten links, schmal**, Absender nur Bolligen mit «www.hkplanung.ch», ohne Massstab.
- Plannummer «<Projekt-Nr> <Fach><NN>»: Fach H/K/L/S; NN je Geschoss von unten (2.UG, 1.UG, EG, 1.OG, 2.OG, Attika),
  bei S ab 00, sonst ab 01; Sonderblätter (H13–H16 FBH, H21 Steigzonen, S06 Grundleitungen, S07–S09 Einlagen, S10
  Kanalisation).
- Revisionsstempel: rot, schräg «Revisionsplan <Datum>» über dem Kopf (nur bei Revisionsplänen).

## Teil A – Umfang

1. **Absender aus dem Bürokatalog:** `kataloge/buero.json` neu `standorte` (Bolligen, Thun: Firma, Strasse, PLZ Ort,
   Telefon, Mail, Web) – Werte **aus einer echten Plan-PDF der Bernstrasse gelesen und mit dem Katalog oben verglichen**
   (Textauszug; Abweichung melden, nicht raten). Plankopf zeigt beide Standorte wie die Vorlage; Schema-Kopf nur Bolligen.
2. **Plankopf wie H+K** (`Plankopf.cs`): Aufbau 1–3 oben; «Projekt» = Objekt (Projektinformation Zeile 1) + Plantitel
   (Planblatt); «Rev. Index» und Datum der letzten Revision; «Massstab» im Format «1 : 50»; Planänderungen mit beliebig vielen
   Zeilen (Kopf wächst nach oben, Tabelle bleibt lesbar). Das bisherige Titelband «Planinhalt» (SIA 400 B.1.3) wird
   **abschaltbar, Vorgabe aus** (H+K-Pläne haben es nicht; Plantitel steht im Projektfeld) – Annahme zur Bestätigung.
3. **Schema-Plankopf:** schmal, unten links, für Planblätter mit Schema-Ansichten (bzw. Eigenschaft `kopfart` am
   Planblatt: grundriss | schema; Vorgabe aus dem Inhalt).
4. **Revisionen am Planblatt:** Eigenschaft `revisionen` (Liste {index, datum, beschreibung}); Befehl «Revision
   hinzufügen» (nächster Buchstabe A, B, C …, Datum heute, Beschreibung Pflicht) über Kern und Oberfläche (Planblatt-Palette
   bzw. Kontextmenü im Projektbrowser); Rückgängig wie alle Befehle. Plansatz-Befehl «Revision für alle Blätter des
   Satzes» (gleiche Beschreibung).
5. **Plannummern-Vorschlag:** beim Anlegen eines Planblatts aus Projektnummer (Projektinformation, seit N3 bearbeitbar),
   Fach (Gewerk des Plansatzes bzw. der Ansichten) und Geschoss nach dem Schema oben; Sonderblätter (Einlagen,
   Grundleitungen) nach der Tabelle; überschreibbar; Befund bei doppelter Plannummer im Projekt.
6. **Revisionsstempel:** Planblatt-/Plansatz-Eigenschaft `stempel` (Text, Vorgabe leer = kein Stempel); Serien-PDF-Dialog
   bietet «Revisionsplan <Datum>» an; rot, schräg über dem Kopf, Farbe/Winkel wie Katalog (#E8202A, ~12°) – Näherung.
7. **Listen auf Planblättern:** `ansicht_platzieren` nimmt auch Listenansichten (Raumliste, Strangliste, Mengen,
   Materialliste, Kollisionen, Aussparungen, Sanitär-Zusammenstellung W3) und zeichnet sie im PDF als Tabelle (Schrift wie
   Plankopf, Spaltenbreiten aus dem Inhalt, Umbruch auf weitere Blattbereiche ist Teil B).

Nicht in Teil A (Teil B): Schnitt-/Ansichtsmarken mit Blattverweis, Aussparungsboxen in Fachplänen, Koordinationsplan in
Gewerkefarben, Architektur-Unterlage DWG/PDF, IFC-Export mit IDS und Exportbericht.

## Fertig, wenn …

Ein Planblatt «Heizung Erdgeschoss» des Beispielprojekts als PDF im Kopf aussieht wie der H+K-Revisionsplan (Vergleich mit
dem Kopf von Bernstrasse H03, Bild nebeneinander): beide Absender, Logo, Projekt zweizeilig, Plannummer automatisch
vorgeschlagen, Revision B mit Datum und Planänderungstabelle (A, B), Stempel zuschaltbar; ein Schema-Blatt mit schmalem
Kopf unten links; eine Liste auf einem Planblatt.

## Teil B1 – Plandarstellung wie H+K (20.10.2026, 05:00)

Aus `entwurf/planmuster.md` (Gemeinsam: «Aussparungen in Fachplänen», «Schnitt-/Ansichtsmarken»; Plantyp «Koordination»):
1. **Aussparungsboxen in Fachplänen** (Option je Ansicht `aussparungsboxen`, Vorgabe aus): graue Box 3-zeilig an jeder
   Aussparung im Grundriss: Gewerk / «WD 35 x35» bzw. «DD …» (Grösse in cm wie Aussparungsplan, ganze cm) / «BR -2.63» (Brüstung
   = Unterkante ab ±0.00 in m) bzw. «Achse -3.49»; Werte aus den Aussparungen von Modul 3b; Führungslinie, kollisionsarm
   platziert (Muster Beschriftungen).
2. **Koordinationsplan in Gewerkefarben** (Ansichtseigenschaft `darstellung` = `koordination`): alle Gewerke in der
   Koordinationsfarbe aus `kataloge/gewerke.json` statt Medienfarben (Heizung rot, Lüftung blau, Sanitär grün …), Architektur
   grau, Aussparungsboxen ein; Planblatt-Fach K.
3. **Schnitt- und Ansichtsmarken mit Blattverweis:** schwarzes Dreieck auf der Schnittlinie, Text «<Titel> / <Plannummer>»
   des Planblatts, auf dem die Schnittansicht platziert ist (leer, solange sie auf keinem Blatt liegt); folgt beim Verschieben.

Fertig, wenn … im Beispielprojekt ein Koordinationsplan des EG in Gewerkefarben mit Aussparungsboxen und einer Schnittmarke
«Schnitt … / <Plannummer>» als PDF entsteht.

## Teil B2 – IFC-Export mit IDS und Exportbericht (20.10.2026, 10:30)

Grundlage: `entwurf/modul-1b-ifc.md` (Version: **Export IFC4 als Vorgabe, IFC2x3 wählbar**, IFC 4.3 nicht), Abbildung
`kataloge/ifc.json` (eine Quelle für Import und Export: Entität, Typobjekt, PredefinedType, Psets, Systemtypen, Fluss,
Klassifikation), Muster `src/Hlks.Kern/Aussparungen/AussparungIfcExport.cs` + `AussparungIfcSchreiber.cs` (Xbim.Essentials
6.1.605, IFC4 und IFC2x3 aus einer Beschreibung, Schemaprüfung). **Jede Zuordnung am Schema prüfen, nie aus dem Gedächtnis.**

1. **Kern:** Befehl `ifc_export { datei, fassung: "IFC4"|"IFC2X3", umfang?: { gewerke?, geschosse?, klassen? }, raeume?: false,
   ids?: [Pfade] }` → Datei + Bericht; MCP-Werkzeug gleich; Web `POST /api/ifc/export` (JSON, Herkunftsschutz) legt unter
   `<Projekt>\ausgabe\ifc\<Projekt>_<Fassung>_<JJJJMMTT-hhmm>.ifc` ab, Bericht daneben (`.bericht.json` und `.bericht.md`).
2. **Inhalt:** IfcProject/IfcSite/IfcBuilding/IfcBuildingStorey aus Gebäude und Geschossen (Koten), Einheiten mm, gemeinsame
   Koordinaten wie beim Import (IFC4: IfcMapConversion, wenn das Projekt sie führt); **nur Elemente, die KICAD führt** – nie
   Grundlagen/Hintergründe und nie aus fremden IFC übernommene Elemente ohne eigene Änderung (Hinweis im Bericht); Räume nur mit
   `raeume: true` (Annahme A1). Je Katalogklasse Entität, Typobjekt und PredefinedType aus `ifc.json`; Typobjekt je Familie und
   Typ (Name «<Familie> <Typ>»). Geometrie: Leitungen als Extrusion entlang der Achse (Aussendurchmesser bzw. b × h); **Dämmung als eigene
   Geometrie** (IfcCovering um Leitung bzw. Formteil mit Dämmdicke, Zuordnung zum Element am Schema prüfen; Entscheid A2), Formteile und Apparate aus den Familienkörpern wie die 3D-Darstellung (gleiche Quelle),
   ohne Körper ein Hüllquader mit Hinweis. Anschlüsse → IfcDistributionPort (Fluss nach `ifc.json fluss`, Systemart, Nennweite),
   Verbindungen → IfcRelConnectsPorts; Systeme → IfcDistributionSystem (PredefinedType aus `ifc.json systemtypen`), Stränge →
   IfcDistributionCircuit (nur IFC4), Anlagen → Gruppe der Systeme (Darstellung am Schema wählen und begründen); räumliche
   Zuordnung zum Geschoss. Psets: der erste Eintrag je Eigenschaft in `ifc.json psets` (Standard-Pset), Mengen (Länge der
   Leitungen in Qto_…BaseQuantities), Klassifikation BKP, dazu ein eigenes Pset `HK_KICAD` (Kennzeichen, Klasse, Medium, System,
   Familie, Typ, Element-Id). **GlobalId stabil** je Element (aus der Element-Id abgeleitet wie bei den Aussparungen), damit ein
   Partner neue Fassungen abgleichen kann.
3. **IDS:** eigene Regeln `kataloge/ids/hk-hlks-basis.ids` (IDS 1.0; Schema von buildingSMART unter `schema/ids-1.0`, nie
   ändern): Leitungen mit Nennweite bzw. Abmessung, jedem Element ein System und ein Geschoss, Systeme mit PredefinedType aus der
   Liste, Apparate mit Typobjekt. Fremde IDS (Bauherr, Architekt) lassen sich angeben. Prüfer: zuerst ein passendes Paket zu
   Xbim 6.1.605 suchen (Version und Lizenz im Bericht des Agenten); passt keines, eigener Prüfer für die genutzten Facetten
   (entity, attribute, property, classification, partOf) – Entscheid Hauptlauf nach Vorschlag.
4. **Exportbericht:** Zähler je Klasse und Entität, übersprungene Elemente mit Grund, Schemaprüfung (Xbim), IDS-Ergebnis je Regel
   (bestanden / nicht bestanden mit Kennzeichen der Elemente), Dauer. Oberfläche: Dialog «IFC exportieren» (Register Datei,
   Gruppe Ausgabe; Fassung, Umfang, Räume, IDS-Dateien, Ziel) mit Bericht und «Ordner öffnen».
5. **Tests:** Rundreise Export → eigener IFC-Import (Modul 1b) im Beispielprojekt, je Fassung: gleiche Elemente, Verbindungen,
   Systeme, Nennweiten, Lage ±1 mm, GlobalIds stabil über zwei Exporte; Schemaprüfung ohne Fehler; IDS: ein absichtlich
   unvollständiges Element erscheint als Befund; Leistung: Bernstrasse-Grösse < 30 s.

**Entscheide Christian 20.10.2026 (Fragefenster):** A1 Räume nur auf Wunsch (Vorgabe ohne IfcSpace), A2 **Dämmung als eigene
Geometrie** (IfcCovering), A3 Datei in den Projektordner `ausgabe\ifc`, A4 eigenes Pset `HK_KICAD`.

Fertig, wenn … das Beispielprojekt als IFC4 und IFC2x3 exportiert, die Schemaprüfung fehlerfrei ist, die IDS-Basis besteht (und
ein absichtlich fehlerhaftes Element als Befund erscheint), die Rundreise gleich bleibt und die Datei in einem freien Viewer
öffnet (Christian prüft auf Wunsch in Revit oder Solibri).

## Teil B3 – Listenumbruch und Menüband-Knöpfe

1. **Listen auf Planblättern umbrechen:** passt eine platzierte Liste nicht in ihren Bereich, geht sie in Blöcken rechts daneben
   weiter (Kopfzeile wiederholt, Summen nur am Ende); reicht das Blatt nicht, Befund «Liste passt nicht aufs Blatt» mit Vorschlag
   (Massstab der Schrift oder Folgeblatt). Option je platzierter Liste `umbruch`: `spalten` (Vorgabe) | `aus`.
2. **Menüband:** Register Koordination: «Koordinationsplan» (legt für das aktive Geschoss eine Ansicht `darstellung =
   koordination` mit Aussparungsboxen an bzw. öffnet die vorhandene), Schalter «Aussparungsboxen» (aktive Ansicht);
   Register Ansicht: «Schnittmarke» bleibt beim Schnitt-Werkzeug. Kapitel in der Anleitung.

Fertig, wenn … die Raumliste des Beispielprojekts auf einem A3-Blatt in zwei Blöcken steht und der Koordinationsplan über einen
Knopf entsteht.

## Zuteilung

| Teil | Wer |
| --- | --- |
| Spezifikation | Hauptlauf |
| A1–A7 | hlks-modul (worktree) |
| B2 Kern (Export, Geometrie, IDS-Prüfer) | hlks-kern (worktree) |
| B2 Dialog, B3 | hlks-modul (worktree) |
| Abnahme-Bilder, Testbericht | hlks-test |

**Entscheid:** Christian, 20.10.2026 (Fragefenster): Fachbuchstabe **K = Koordination** (Kälte vorerst ohne Buchstaben). Nächster Schritt bis zur Normenlieferung: Teil B (IFC-Export mit IDS, Listenumbruch, Menüband-Knöpfe Koordinationsplan/Boxen).
