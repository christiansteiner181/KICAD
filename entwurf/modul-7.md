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

## Zuteilung

| Teil | Wer |
| --- | --- |
| Spezifikation | Hauptlauf |
| A1–A7 | hlks-modul (worktree) |
| Abnahme-Bilder, Testbericht | hlks-test |

**Entscheid:** Christian, 20.10.2026 (Fragefenster): Fachbuchstabe **K = Koordination** (Kälte vorerst ohne Buchstaben). Nächster Schritt bis zur Normenlieferung: Teil B (IFC-Export mit IDS, Listenumbruch, Menüband-Knöpfe Koordinationsplan/Boxen).
