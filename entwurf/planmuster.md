# Planmuster H+K – Darstellungskonventionen je Plantyp

Stand 19.10.2026 · Hauptlauf · Grundlage für die Plandarstellung in KICAD (Module 3, 3b, 4–7)

## Quelle und Umgang

Christian, 19.10.2026: «gebe dir div. muster.» – echte H+K-Pläne des Objekts MFH Bernstrasse 42, Worb (Projekt 1-0768):
Revisionspläne 21.01.2026 (34 Blätter H, K, L, S), Aussparungsplan 1.UG (24.09.2024), Kanalisation Baueingabe, Koordination
1.OG (17.09.2024) und 2.UG (Ausschreibung 08.05.2024, mit Schnittlinien), Sanitär-Ausführung 1.UG/1.OG, Einlagen D.ü.2.UG,
Heizung/FBH (11.06.2025), Strangschemas Wasser/Abwasser. Christian: «Für Bernstrasse ist ein komplettes Projekt inkl. aller IFC
und GR-Pläne vorhanden, du kannst dich bedienen» (W:\02_Projektdateien Objekte\Worb_BES_Bernstrasse\c_Pläne und Modelle).

- **Bilder echter Pläne nie ins Repo** – Renderings nur lokal (Scratchpad bzw. `%USERPROFILE%\HLKS_CAD_testdaten`).
  Ausführlicher Katalog mit wörtlichen Beispielen: Scratchpad `planmuster\katalog.md` (Sonnet-Agent, vom Hauptlauf
  stichprobenweise geprüft: FBH-Zone H13, Einlagen S09, Aussparungsplan 1.UG, Schema Abwasser).
- Farben sind aus Renderings geschätzt (Näherung), nicht aus den PDF-Objekten gelesen.
- Für KICAD gilt der Massstab aus dem Plankopf (1:50; Sanitär-Grundrisse 1:25).

## Gemeinsam über alle Plantypen

| Thema | H+K-Konvention | KICAD heute | Wohin |
| --- | --- | --- | --- |
| Blatt | A0 quer (Grundrisse), dünner Rahmen | Planblatt mit Formaten (1a) | – |
| Plankopf | unten rechts: Absender Bolligen **und** Thun, Logo, Projektphase, Projekt (2 Zeilen: Objekt + Plantitel), Plannummer «1-0768 H01», Erstelldatum, Rev. Index + Datum, Kontrolliert (Kürzel), Massstab; Tabelle Planänderungen (Index, Datum, Beschreibung) | H+K-Kopf (1a), nur Bolligen, Felder teils leer | Modul 7 |
| Plankopf Schema | schmal unten links, ohne Massstab | wie Grundriss | Modul 7 |
| Plannummer | «<Projekt-Nr> <Fach><Nr>»; Fach H/K/L/S; Nr. je Geschoss (S ab 00, sonst ab 01), Sonderblätter H13–H16 FBH, H21 Steigzonen, S06 Grundleitungen, S07–S09 Einlagen, S10 Kanalisation | frei | Modul 7 |
| Revisionsstempel | rot schräg «Revisionsplan 21.01.2026» über dem Kopf | – | Modul 7 (abschaltbar) |
| Architektur | hellgrau, voll bemasst, Raumstempel des Architekten | IFC-Schnitt bzw. Unterlage grau | Unterlage DWG/PDF prüfen (A10–A16 als DWG vorhanden) |
| Hinweise und Masse H+K | **Magenta** (#FF00FF), Massketten mit langen Schrägstrichen, cm ohne Einheit | Aussparungs-Massketten so (3b B4d) | allgemeiner Stil Modul 7 |
| Rohrbeschriftung Sanitär | **schwarz** mit Führungslinie, «ø28 / RA -12 cm ab UKD_2.UG», «PE-S 110» | «{kuerzel} Ø{da}» (B9) | Höhenzusatz → Beschriftungsvorlagen |
| Höhenbezug | Text, nie Linienart: «RA -12 cm ab UKD_<Geschoss>», «A=-24.7 cm ab UKD_EG roh», «OK=-5.0 cm ab 0.00», «S= -6.37 m ab 0.0» (Sohle) | Koten in Plänen teilweise | Beschriftungsvorlagen mit Bezug (UKD roh, ±0.00, OK FB) |
| Aussparungen in Fachplänen | graue Box 3-zeilig: Gewerk / «WD 35 x35» / «BR -2.63» bzw. «Achse -3.49» | Symbol + Nummer | Option in Fachplänen (Modul 7) |
| Schnitt-/Ansichtsmarken | schwarzes Dreieck auf Linie, Text «Titel / Plankürzel» | Schnittlinie | Modul 7 (Verweis auf Blatt) |
| Legende | auf keinem Blatt | Legenden je Darstellung | Vorgabe aus (wie Aussparungsplan B4e) |

## Je Plantyp

### Aussparungsplan (Vorlage 1.UG) → Modul 3b, B4d/B4e
Architektur grau mit Bemassung, nur Aussparungen und magenta Massketten (ganze cm), **keine HLKS-Leitungen**, keine Legende,
kein Text am Symbol, 1:50. Kennfarbe Decke gelb-orange mit innerem Winkel, Wand braun mit Strich (SIA 400 B.10). Entscheide
Christian 19.10.2026: ganze cm (Grösse aufrunden), keine HLKS.

### Einlagen (S07–S09) und Grundleitungen (S06) → Modul 3, B10
- Plantitel «Sanitär Einlagen D.ü.2.UG» (Decke über), «… Bodenplatte 2.UG», «Grundleitungen 2.UG»; 1:50; Grundriss des
  Geschosses darunter grau, Aussparungen grau im Hintergrund.
- Einlagen kräftig in Medienfarbe (Abwasser braun mit Muffen/Formteilen, KW grün und WW rot dünn); Beschriftung schwarz mit
  Führungslinie «PE-S 110», «SaniPex 16».
- Magenta Bausteine an Leitungsenden und Bauteilen: «B.n.o» / «B.n.u» (**Bogen nach oben/unten**, Christian), «B.n.o Ø16», «2x B.n.o», «KW: B.n.o Ø16», «Sagex 40x20x20» (Aussparungskörper), «Schalungsschoner»,
  «BWA Varino horizontal/vertikal»; Pfeil auf das Leitungsende.
- Magenta Massketten von Rohbaukanten zu Leitungsenden und Knicken (cm).
- Grundleitungen: Sohlenkote «S= -6.37 m ab 0.0» mit Pfeil, «ø110»; Schacht grüner Kreis mit «E1 / A / E2».
- Einlagen erscheinen **nur** im Einlageplan, Grundleitungen nur im Grundleitungsplan (Entscheid 19.10.2026).

### Strangschema (Wasser, Abwasser) → Modul 3 B8/B8b/B8c, Werte Modul 4
Geschossbänder kreuzschraffiert, Geschossname links; Strangkreise oben («WAS-H 01», «WAS-R 01», «PW 1»); Beschriftung in
Leitungsfarbe «PE-S Ø110 (3.3)» mit schwarzem Punkt und Führungslinie; Wohnungstabelle App./Stk./LU/KW/WW; Apparate cyan;
Einlagen im Deckenband (B8c). Belastungswerte in Klammern und Terrasseneinlauf-Rechnung → Modul 4.

### Sanitär Grundriss (S00–S05) → Modul 3 (Darstellung), Modul 7 (Blatt)
1:25; Abwasser braun, KW grün, WW rot, Zirkulation orange, Doppellinien mit schwarzer Kontur; Strangkreise «WAS-H / 1»,
«PW / 1» mit Führungslinie zum Strang; Vorwandelemente hellgrün; schwarze Rohrbeschriftung mit Höhe ab UKD; S01 zusätzlich
**Schachtansicht** und **3D-Detail** auf dem Blatt mit Höhenmarken «-0.59 m UKD_1.UG ▲» / «-3.06 m 1. Untergeschoss ▼».

### Kanalisation (S10; Baueingabe) → Modul 4
Katasterhintergrund grau; Leitungen braun breit; Schächte grün mit magenta Datenblock (Grösse, Deckel, Einlauf/Auslauf/Sohle
in m ü. M. mit Differenz zum Nullpunkt); Leitungsbeschriftung «PP125 / Qr1.23l/s», Flächenrechnung «A = 52.5m2 / C = 1.0 /
…= 1.58l/s», Sohlenpfeile, Gefälle «← 2.9%»; Plankopf-Zusatz «Verantwortlicher Fachplaner».

### Heizung Grundriss (H01–H06) → Modul 5
Vorlauf orange-rot, Rücklauf violettblau, Doppellinie; Speicher rote Kreise, Verteiler grün mit Kollektor; Beschriftung
magenta «DN50 / RA -12 cm ab UKD_2.UG»; **Verteilerbox** (Fabrikat, Typ, Abgänge, Leistung, Durchfluss, Kasten) mit
Nummer je Geschoss (001/002, 101/102 …); Strangkreis «FBH / 1».

### Fussbodenheizung (H13–H16) → Modul 5
Zone je Raum als abgerundetes Rechteck mit Pastellfüllung (Lachs/Gelb/Cyan, Bedeutung offen), Verlegeart-Symbol (Mäander
«Schlangenförmig», Spirale «Schneckenförmig»); **Datenbox je Kreis** «012 - Zimmer / 21.0 °C», Verlegeabstand, Ringlänge,
Leistung, Durchfluss, Randzone, Verlegeart – in Reihen ausserhalb des Gebäudes mit Führungslinie; VL/RL-Bündel zum Verteiler;
Standardhinweis «FBH-Vorläufe beim Verteiler und im Korridor sind zu isolieren». Keine Heizkreistabelle.

### Steigzonen Heizung (H21) → Modul 5
Schnitte «① Heizung Steigzone 1 / 1 : 50» auf hellgrauem Gebäudeschnitt; Verteiler je Geschoss mit Box; Beschriftung gedreht
«DN25 / 350 l/h» (Nennweite und Volumenstrom).

### Lüftung (L01–L06) → Modul 6
Zuluft rot, Abluft gelb, Aussenluft hellgrün, Fortluft braun; Pastell = hinten/verdeckt bzw. gedämmt (offen); Rundrohr «ø125»,
Rechteckkanal als Bruch «250 / 100»; Höhe «A=-24.7 cm ab UKD_EG roh»; Dämmung «EI30 / 50 mm»; Zubehör «RSD 50 - 1000mm»;
**Luftmengenbox** (Volumenstrom, L/B/H, Anschluss) am Durchlass; BSK-Box; **Ansichten aus dem Modell neben dem Grundriss**
mit magenta Titel («Ansicht Zentrale», «Ansicht Steigzone links»).

### Koordination (K01–K06; Ausschreibung 2.UG) → Modul 3b/7
Alle Gewerke in **Gewerkefarben** statt Medienfarben: Heizung rot, Lüftung blau, Sanitär grün; Architektur grau mit
Aussparungsboxen; magenta Hinweise («Türe luftdicht + BS», «2x Lichtschacht ergänzen»); Schnitt- und Ansichtsmarken mit
Verweis auf Fachblätter; Ausschreibungsstand zusätzlich mit vielen nummerierten Schnittlinien «Schnitt 23 / —».

## Folgerungen für den Bau (Vorschlag Hauptlauf)

1. **Jetzt (Modul 3, B10):** Einlageplan und Grundleitungsplan nach S06–S09 (Bausteine B.n.o, Sagex, Schalungsschoner,
   Massketten zu Leitungsenden, Sohlenkote).
2. **Beschriftungsvorlagen mit Höhenbezug** («ø{da} / RA {z} cm ab UKD_{Geschoss}») – klein, passt zu B9; mit B10 oder als
   eigener Nachtrag.
3. **Modul 7 (Ausgabe):** Plankopf vollständig (zwei Standorte, Projektphase, Revisionen, Planänderungen, Revisionsstempel,
   Schema-Kopf links), Plannummern-Schema, Schnitt-/Ansichtsmarken mit Blattverweis, Aussparungsboxen in Fachplänen,
   Koordinationsplan mit Gewerkefarben, Architektur-Unterlage als DWG/PDF (A10–A16).
4. **Module 4–6:** Datenboxen (Verteiler, FBH-Kreis, Luftmenge, Schacht) als ein gemeinsamer Baustein «Datenbox mit
   Führungslinie», gespeist aus Rechenergebnissen; Platzierung in Reihen ausserhalb des Grundrisses wie H13–H16.

## Antworten Christian (19.10.2026, Fragefenster)

- **«B.n.o» / «B.n.u» = Bogen nach oben / nach unten** (Leitungsende tritt senkrecht aus Decke bzw. Bodenplatte aus).
- **«BR» = Brüstung = Unterkante der Aussparung** (Kote ab ±0.00).
- **FBH-Zonenfarben ohne Bedeutung** – nur zur Unterscheidung, KICAD wählt frei (benachbarte Zonen verschieden).
- **Pastelltöne bei Lüftungskanälen = Dämmung** (Dämmhülle um den Kanal).

Offen: «CA» im Schema Wasser.
