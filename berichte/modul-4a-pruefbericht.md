# Prüfbericht Modul 4 Teil A-K – Sanitär-Belastungswerte (Rechenkern)

Prüfer: hlks-pruefer (unabhängig, nur lesend) · Datum: 20.10.2026 · Geprüfter Stand: Commit 632beb1 (Merge 6e892bd) auf `main`
Prüfgrundlage: `entwurf/modul-4.md` (Teil A-K), `kataloge/sanitaer_belastungswerte.json`, Original-Excel
`W:\02_Projektdateien Objekte\Worb_BES_Bernstrasse\f_Berechnungen\02_Sanitär\LU_DU Zusammenstellung_.xlsx` (nur gelesen: Kopie
entpackt, `xl/workbook.xml`, `xl/worksheets/sheet1.xml`, `xl/sharedStrings.xml`, `docProps/*.xml`), HKAP-Fassung
`AufgabenProtokolle.Web/Services/SanitaerKatalog.cs`, Leitprinzipien `LIESMICH.md`.

## Urteil

**Abnahmefähig mit Auflagen.**

Alle 34 Apparatearten, alle Zahlenwerte (LU KW, LU WW, DU, null-Felder), alle Formeln (Faktoren, Exponenten, Zellbezüge) und
die Referenzwerte Walkringen stimmen mit dem Excel überein; die Rechnung des Kerns liefert die Excel-Ergebnisse auf die letzte
Stelle (selbst nachgerechnet, Tests auf 1e-9). Die Netzsummen sind richtig gerichtet und doppelzählungsfrei. Die Auflagen
betreffen die Herkunftsangabe (falscher Blattname in jeder Quelle), einen stillen Nullbeitrag bei Richtungsfehlern im Netz und
die Bestätigung der Kurvenregel, die nicht aus dem Excel stammt.

Schwere: **kritisch** = falsch (Ergebnis oder Herkunft falsch) · **mittel** = riskant (stilles Fehlverhalten möglich, Annahme ohne
Quelle) · **gering** = Verbesserung.

## Tests

`dotnet test tests/Hlks.Kern.Tests --filter FullyQualifiedName~Sanitaer` (HLKS_BUILD pruefer4, HLKS_KATALOG = Repo-Kataloge,
HLKS_TEST=1): **91 bestanden, 0 Fehler, 12 s** (SanitaerKatalog-, SanitaerArt-, SanitaerNetz-, SanitaerZusammenstellung-,
SanitaerBeispiel-Tests sowie Abnahme3SanitaerTests).

## 1. Übertragung Excel → Katalog (alle 34 Zeilen geprüft)

Das Excel hat zwei Blätter: **«MFHA»** (sheet1.xml, die Zusammenstellung) und «Tabelle2» (leer). Zeilen 7–40, Spalten A–D,
Zelle für Zelle gegen den Katalog verglichen:

| Excel-Zeile | Excel A (Name) | B/C/D | Katalog | Befund |
| --- | --- | --- | --- | --- |
| 7–20, 22–25, 27, 29–34, 36–40 | identisch | identisch | identisch | ok |
| 21 | «Waschrog KW» (Tippfehler im Excel) | 2 / – / 0.8 | «Waschtrog KW» | Name stillschweigend korrigiert |
| 26 | «Waschmaschine bis 6kg» | 2 / – / 0.8 | «Waschmaschine bis 6 kg» | Leerzeichen ergänzt |
| 28 | «Waschmaschine 7 - 12kg» | 2 / – / 1.5 | «Waschmaschine 7 – 12 kg» | Gedankenstrich, Leerzeichen |
| 35 | «Waschmaschine 13 - 40kg» | 1 / 1 / 2.5 | «Waschmaschine 13 – 40 kg» | Gedankenstrich, Leerzeichen |

Zahlen: **keine Abweichung** in allen 34 Zeilen; leere Excel-Zellen sind im Katalog `null` (Z7, 14, 27, 30, 32: kein LU; Z10, 24,
25, 26, 28, 29, 31, 33, 38, 39, 40: kein LU WW; Z23, 24, 39, 40: kein DU). Reihenfolge und Excel-Zeilennummern lückenlos.
Spaltenköpfe B4 «LU KW», C4 «LU WW», D4 «DU» wie im Katalog.

Formeln (Excel → Katalog `rechnung`):

| Zelle | Excel-Formel | Katalog | Befund |
| --- | --- | --- | --- |
| B55 | `M43+N43` (M43 = SUM(M7:M40), N43 = SUM(N7:N40)) | «B55 = M43+N43» | ok |
| B56 | `B55*0.1` | «B56 = B55*0.1», Faktor 0.1 | ok |
| C57 | `B56^0.353*0.459` | faktor 0.459, exponent 0.353 | ok |
| I57 | `B56^0.257*0.598` | faktor 0.598, exponent 0.257 | ok |
| C59 / I59 | `IF(ISNUMBER(I51),(C57+I51),C57)` bzw. `…I57+I51…` | «C57+I51 bzw. I57+I51» | ok (Vereinfachung gleichwertig) |
| I51 / N51 | `SUM(I47:M49)` bzw. `SUM(N47:O49)` (3 Eingabezeilen 47–49) | «Zeilen 46–51» | ok (46 Titel, 51 Total) |
| M64 | `O43` (ΣDU) | – | ok |
| M65 | Konstante 0.5, Beschriftung I65 «Gebäudeart / Abflusskennzahl:» | abflusskennzahlBeispiel 0.5, M65 | ok |
| M66 | `M65*((M64)^(1/2))` | «M66 = M65*((M64)^(1/2))» | ok |
| M67 | `IF(ISNUMBER(N51),(M66+N51),M66)` | «M67 = M66+N51» | ok |

Texte C55 «Spitzenvolumenstrom bei 0.3 bis 300 l/s», C56 «Grösster LU: 3», I55 «… 0.5 bis 15 l/s», I56 «Grösster LU: 5», A57
«Total Spitzendurchfluss nach Diagramm 1 W3» – im Katalog sinngemäss richtig wiedergegeben.

Referenzwerte (Excel-Zellwerte, selbst nachgerechnet): B55 = 132, B56 = 13.200000000000001, C57 = 1.1412355278976094,
I57 = 1.1606163499508511, M43 = 80, N43 = 52, O43 = 38.8, M66 = 3.1144823004794873 – alle gleich dem Katalog `referenz`
(I57 im Katalog als 1.160616349950851 = derselbe double). Stückzahlen E–I je Zeile (WT 2+3+3+2 = 10, DU 7, BW 4, SP 8,
GWA 8, WC 10, Garten 2) = Katalog `referenz.anzahl`.

Dokumenteigenschaften (docProps/core.xml): creator «Hauptbenutzer», lastModifiedBy «daniele gulizia», modified
2024-03-14T08:53:17Z; Zelle O2 «ek», L2 = TODAY() mit gespeichertem Wert 45365 = 14.03.2024. Der Katalog nennt Stand 14.03.2024
(richtig, letzte Speicherung) und Verfasser «Christian Steiner, H+K Planungs AG» (nicht aus dem Dokument belegbar, siehe Annahme A4).

## Befunde

### B1 – mittel – Blattname in jeder Herkunftsangabe falsch («Tabelle1» statt «MFHA»)

- `kataloge/sanitaer_belastungswerte.json:2` (`$kommentar` «aus Tabelle1 A7:D40») und `:9` (`"bereich": "Tabelle1 A7:D40"`)
- `src/Hlks.Kern/Kataloge/SanitaerKatalog.cs:167` (`Blatt = bereich.Split(' ', 2)[0]`) und `:90`, `:93` (Quelle «{Blatt} Zeile n»)
- Nachweis: `xl/workbook.xml` und `docProps/app.xml` TitlesOfParts: Blätter «MFHA» und «Tabelle2»; ein Blatt «Tabelle1» gibt es
  nicht. Damit lautet jede «?»-Herkunft eines LU/DU-Werts («… Tabelle1 Zeile 31») und jede Formelquelle auf ein Blatt, das
  im Dokument nicht existiert. Leitprinzip 4 (jeder Wert hat eine Herkunft) ist in der Sache erfüllt, in der Angabe aber falsch.
- Vorschlag: `bereich` → «MFHA A7:D40», `$kommentar` anpassen; Tests `tests/Hlks.Kern.Tests/SanitaerKatalogTests.cs:34`
  (`Assert.Equal("Tabelle1", SKat.Blatt)`) und `:63`, `SanitaerArtTests.cs:125`, `:135`, `SanitaerZusammenstellungTests.cs:107`
  («Tabelle1 Zeile …») nachführen. Keine Codeänderung nötig, nur Katalog und Tests.

### B2 – mittel – Apparat am Netz, aber ohne erreichte Leitung: stiller Nullbeitrag ohne Befund

- `src/Hlks.Kern/Rechnen/SanitaerBelastung.cs:221` (`if (!Ausgang(start.Fluss, richtung)) return;`), `:226` (erstes Element kein
  Durchgang/Medium/Richtung → `return`), `:234–241` (Weg endet, wenn kein Nachbar mit passender Richtung).
- Gedankenbeispiel: HA.aus → R1 → T1.a; T1.b ↔ R2.**ende** (verkehrt gezogen; «beide» ↔ «raus» ist erlaubt, P07 greift nur bei
  rein↔rein/raus↔raus, `Pruefung/Pruefungen.cs:120`); R2.anfang ↔ T2.a; T2.b → R3 → WC.kw. Vom WC stromauf: R3 ok, T2, dann
  R2.anfang («rein») – `Eingang(rein, stromauf)` false → Weg endet. R1 und der Hausanschluss bekommen vom WC **nichts**, R2
  ebenfalls; kein Befund (P22 «nicht verbunden» greift nicht, der Anschluss ist verbunden; P23 nicht, die Anlage stimmt). Dasselbe
  bei einem Apparateanschluss mit falschem `fluss` in einer Familie oder bei Medium «*» am Apparat (`:174` überspringt still).
- Risiko: Teilstrecken- und Anlagensummen zu klein, ohne dass es jemand sieht; die Zusammenstellung (Stückzahl × Wert) zeigt
  gleichzeitig den vollen Wert – der Widerspruch fällt nur beim Vergleich auf.
- Vorschlag: in `Folgen` festhalten, ob der Weg mindestens eine Leitung erreicht bzw. an einer Quelle/Zentrale (Trinkwasser) oder
  Senke (Abwasser) endet; sonst Befund (neuer Code P26 «Apparat trägt zu keiner Leitung bei – Weg endet bei X» oder P22
  erweitern) und in `sanitaer_belastung` je Anlage eine Liste `ohneWeg`. Test: das Beispiel oben.

### B3 – mittel – Regel «grösster Einzel-LU > 3 → Kurve 5» stammt nicht aus dem Excel; Schwelle steht im Code

- `src/Hlks.Kern/Rechnen/SanitaerZusammenstellung.cs:168` (`a.GroessterLu > kat.Kurve3.Nummer ? kat.Kurve5 : kat.Kurve3`),
  `src/Hlks.Kern/Kataloge/SanitaerKatalog.cs:174–175` (`Kurve("spitzeKurve3", 3)`, `Kurve("spitzeKurve5", 5)` – die Nummern 3
  und 5 sind im Code gesetzt, nicht im Katalog), `src/Hlks.Kern/Rechnen/SanitaerAbfragen.cs:78` (Formeltext nennt «entwurf/modul-4.md,
  wie HKAP» – ehrlich).
- Nachweis: Das Excel wählt **nicht**; C56 «Grösster LU: 3» und I56 «Grösster LU: 5» stehen nebeneinander, beide Spitzen werden
  gerechnet. Die automatische Wahl kommt aus HKAP (`AufgabenProtokolle.Web/Services/SanitaerKatalog.cs:186`
  `a.MassgebendeKurve = a.GroessterLu > 3 ? 5 : 3`) ohne Normzitat. W3 Diagramm 1 liegt nicht vor.
- Beurteilung der Herleitung: Mit den LU-Werten des Katalogs (1, 2, 3, 5) ist «> 3» gleichbedeutend mit «= 5», die Lesart der
  Excel-Beschriftung ist plausibel. Offen bleibt: (a) grösster LU zwischen 3 und 5 oder über 5 (im Katalog nicht vorhanden,
  bei Nachführung möglich); (b) die zweite Unterscheidung der Beschriftung – Gültigkeitsbereich 0.3–300 l/s bzw. 0.5–15 l/s –
  wird weder geprüft noch gemeldet (Excel ebenso nicht). Der Kern verhält sich wie Excel + HKAP; die Regel ist aber eine
  Annahme, kein Normwert.
- Vorschlag: Regel in den Katalog (`rechnung.massgebendeKurve` mit `quelle`: «HKAP, Entscheid Christian», kein Normzitat) und
  den Code daraus lesen; Hinweis in der Zusammenstellung, wenn `volumenstrom` ausserhalb des im Katalogtext genannten Bereichs
  der gewählten Kurve liegt oder `groessterLu` > 5. Bestätigung durch Christian (Annahme A1).

### B4 – gering – Namen typografisch bereinigt, `$kommentar` behauptet reine Skriptübertragung

- `kataloge/sanitaer_belastungswerte.json:84` («Waschtrog KW», Excel A21 «Waschrog KW»), `:89` («bis 6 kg», Excel «bis 6kg»),
  `:91` («7 – 12 kg», Excel «7 - 12kg»), `:98` («13 – 40 kg», Excel «13 - 40kg»); `:2` «per Skript aus Tabelle1».
- Die vier Namen sind identisch mit HKAP (`SanitaerKatalog.cs:40, 43, 45, 52`), d. h. aus HKAP übernommen oder von Hand
  bereinigt. Fachlich richtig (Tippfehler), aber die Herkunft «wörtlich aus dem Excel» stimmt für diese vier Zeilen nicht.
- Vorschlag: im `$kommentar` festhalten: «Namen Z21 (Tippfehler), Z26/28/35 (Leerzeichen, Gedankenstrich) typografisch
  bereinigt, Zahlen unverändert.»

### B5 – gering – Werte und Texte ausserhalb des Katalogs

Fundstellen, an denen ein Fachwert oder eine Fachregel nicht aus dem Katalog gelesen wird (Normwerte-Regel, LIESMICH.md 4):

1. `src/Hlks.Kern/Kataloge/SanitaerKatalog.cs:174–175` Kurvennummern 3 und 5 (siehe B3).
2. `src/Hlks.Kern/Rechnen/SanitaerPruefung.cs:84` Literal «der Excel-Wert 0.5 gilt nur für Walkringen» im Vorschlag von P25 –
   Zahl fest im Text statt `kat.AbflusskennzahlBeispiel`; ebenso Kommentar `SanitaerZusammenstellung.cs:10`.
3. `src/Hlks.Kern/Kataloge/SanitaerKatalog.cs:45–46` Medienmengen Trinkwasser {KW, WW}, Abwasser {SW} fest im Code; die
   Spezifikation (Punkt 3) nennt «die Schmutzwasser-Medien des Katalogs medien.json». Mit dem heutigen `medien.json` (nur SW
   als Schmutzwasser) gleichwertig.
4. `src/Hlks.Kern/Kataloge/SanitaerKatalog.cs:97, 148–149, 173` Faktor 0.1 wird per Regex aus dem Freitext «B56 = B55*0.1»
   gelesen – funktioniert, ist aber zerbrechlich; besser ein Feld `faktor` wie bei den Kurven.
5. `src/Hlks.Kern/Beispiel/Beispielprojekt.cs:89–94` K = 0.5 aus `AbflusskennzahlBeispiel`, Quelle «fiktiv – Wert wie … M65
   (Walkringen)»: erlaubt laut Auftrag, zu bestätigen (A3). `tests/Hlks.Kern.Tests/SanitaerHilfe.cs:19–27` K 0.5 bzw. 0.7 mit
   Quelle «Testwert (fiktiv)»: in Ordnung.
6. `SanitaerNachfuehren` (`SanitaerAbfragen.cs:426`) K > 0: Plausibilitätsregel, kein Normwert – in Ordnung.

Keine K-Liste, keine Kurvengrenzen, kein stiller Default 0.5 im Kern (anders als HKAP `SanitaerKatalog.cs:190`). Die
Spezifikation ist hier eingehalten.

### B6 – gering – Masche: Kennzeichnung breiter als die Unsicherheit, gemeldetes Element hängt von der Suchreihenfolge ab

- `src/Hlks.Kern/Rechnen/SanitaerBelastung.cs:186–187` (`MaschenLeitungen` = jede Leitung, die irgendeinen Apparat einer
  Masche enthält) und `:238` (`masche(nb)` beim zweiten Erreichen).
- Im Ringtest ist auch R4 (einziger Weg T2 → WC-1) als `masche: true` markiert, obwohl ihre Summe eindeutig ist; P24 nennt das
  Element, das die Breitensuche zufällig zuerst zweimal erreicht (T1), nicht die Verzweigung.
- Vorschlag: `masche` nur für Leitungen setzen, die in der Masche liegen (zwischen Verzweigung und Zusammenführung), oder die
  Formulierung «enthält Apparate einer Masche» in der Antwort mitgeben. Verhalten ist nicht falsch, nur grob.

### B7 – gering – Zusammenstellung und Netzsummen können voneinander abweichen, ohne dass die Zusammenstellung das sagt

- `src/Hlks.Kern/Rechnen/SanitaerZusammenstellung.cs:156–163`: Anzahl × Wert je Art für alle Apparate der Anlage, unabhängig
  davon, ob der jeweilige Anschluss verbunden ist (wie Excel). Beispielprojekt: ΣLU WW = 18 in der Zusammenstellung, obwohl
  kein Warmwassernetz existiert (Netz ΣLU WW = 0); kein Hinweis, weil P22 «nicht verbunden» nur bei vorhandenem WW-System
  greift (`SanitaerPruefung.cs:56`).
- Vorschlag: in `hinweise` der Zusammenstellung die Differenz zu den Netzsummen der Anlage nennen («ΣLU WW 18 in der
  Zusammenstellung, 0 im Netz: kein System WW»). Fachlich ist das Excel-Verhalten gewollt (Annahme A5).

### B8 – gering – HKAP weicht vom Excel ab (zur Kenntnis, nicht Prüfgegenstand)

`AufgabenProtokolle.Web/Services/SanitaerKatalog.cs:22–59`: Reihenfolge «nach DU aufsteigend» (Getränkeautomat an Position 7,
Waschtrog über BA / GWA über Spültisch / Entnahmearmaturen am Ende) entspricht nicht den Excel-Zeilen 7–40; Name wc_75 «7.5 l»
statt «7.5 Liter»; K-Default 0.5 still. Der Kern folgt dem Excel und ist hier richtig.

## 2. Rechnung gegen Spezifikation und Excel

- Total LU, Volumenstrom, beide Kurven, Spitze gesamt, ΣDU, Q_WW, Q_tot: Formeln wörtlich wie Excel (B55, B56, C57, I57, C59,
  I59, M66, M67), selbst nachgerechnet: 13.2^0.353·0.459 = 1.1412355278976094, 13.2^0.257·0.598 = 1.1606163499508511,
  0.5·√38.8 = 3.1144823004794873 – identisch mit Excel-Zellwerten und Katalog.
- Keine Rundung im Kern (double); `Z()` «0.###» nur in Befund- und Rechenwegtexten. Einheiten im JSON: LU, DU, l/s – richtig.
- Spitze bei Volumenstrom 0 → 0 (Excel ebenso). Ohne K: kein Q_WW, kein Q_tot, Befund P25 – wie Spezifikation Punkt 4.
- Spezialinstallationen: Σ Versorgung zu beiden Kurven, Σ Entsorgung zu Q_tot – wie C59/I59/M67. Eingabeprüfung (Zahl ≥ 0,
  Pflichtfeld bezeichnung) sinnvoll.
- Geschossspalten nach Kote aufsteigend (UG zuerst) = Excel E–I (UG, EG, 1.OG, 2. OG, Attika). Annahme, im Kopf der Datei
  benannt.

## 3. Netzsummen (Grenzfälle gegen den Code durchgegangen)

- Richtung: Trinkwasser wird ab dem Apparat **stromauf** verfolgt, jede erreichte Leitung hat den Apparat stromab – richtig;
  Schmutzwasser stromab, Leitung hat den Apparat stromauf – richtig. Eine Leitung wird nur über ihr «ende» (Trinkwasser) bzw.
  «anfang» (Abwasser) betreten; Seitenäste eines T-Stücks werden dadurch nicht mitgezählt (geprüft am Verzweigungstest:
  R5.anfang «rein» → `Eingang(rein, stromauf)` false).
- Wassererwärmer: Weg ww → WE (zentrale) → kw → Kaltwasser-Zuleitung; ZIR-Anschluss durch Medium ausgeschlossen. Die
  KW-Leitung vor dem WE trägt ΣLU WW; Hausanschluss = ΣKW + ΣWW wie Excel B55. Richtig und mit Walkringen-Netz getestet
  (LK-0: 80 / 52).
- Doppelzählung: `gezaehlt` «Leitung|Apparat|Medium» und `Summe` dedupliziert je Apparat und Medium; Apparat mit zwei
  Anschlüssen desselben Mediums zählt einmal; an der Anlage zählt WT.ww einmal, obwohl er auf WW- und KW-Leitungen steht.
- Zirkulation, Regenwasser, Heizung: `LeitungsMedium` null → nicht im Netz, Abfrage antwortet «ausgenommen».
- Formteile, Armaturen: Durchgang ohne Summe – wie der Bauagent angibt (Summen nur je Leitung). Formteil direkt am Apparat
  ohne Rohr: funktioniert.
- Nicht angeschlossene Apparate: kein Netzbeitrag; Zusammenstellung zählt sie, wenn irgendein Anschluss an ein System der
  Anlage führt (siehe B7); ganz unverbundene → P23.
- Masche: Apparat je Leitung einmal, P24 – richtig (siehe B6 zur Breite der Markierung).
- Offen: stiller Nullbeitrag bei Richtungsfehler mitten im Weg (B2).
- Zwischenspeicher je `Aenderungszaehler`: Trockenlauf und Rückgängig im Test abgedeckt; Rechnung ändert die Prüfsumme nicht.

## 4. Befunde P21–P25

- P21 (ohne Art / unbekannter Schlüssel): Auslösung richtig, Bodenablauf ohne Vorgabe → «Art wählen». Schwere warnung passend.
- P22 (Art ↔ Anschlüsse): drei Fälle richtig; «nicht verbunden» nur mit System des Mediums im Projekt und Apparat am Netz –
  vermeidet Fehlalarme im Beispielprojekt (kein WW-System). Nach dem Abnahmeszenario 3 (WW-System vorhanden) erscheinen sechs
  P22 für Apparate *.02/*.01 mit offenem ww – fachlich richtig, zu erwarten.
- P23 (keine Anlage): hinweis, richtig; Grundmodell der Prüfungstests hat deshalb jetzt einen P23 (Test angepasst).
- P24 (Masche): warnung, Element = erstes doppelt erreichtes Element (B6).
- P25 (K fehlt): nur für Anlagen mit DU-Apparaten, Anlage vor Projektinformation – richtig.
- Beispielprojekt: 0 Sanitärbefunde (Test `Straenge_Anlage_PassenZurZusammenstellung_OhneSanitaerbefund`), keine Fehlalarme.
- Ohne Katalogdatei: keine Sanitärbefunde, Auswahl leer, Abfragen melden das Fehlen – richtig.

## 5. Herkunft / «?»

Jeder LU/DU-Wert nennt Katalogdatei, Dokument, Objekt, Stand und Excel-Zeile (`warum`, `element_lesen`, Beiträge je Leitung);
Formeln nennen die Zelle; die Kurvenregel nennt «entwurf/modul-4.md, wie HKAP». Mangel: Blattname (B1). Verfasser nicht aus
dem Dokument belegbar (A4).

## 6. Normwerte-Regel

Siehe B5. Kein Wert, der nicht aus dem Katalog oder als gekennzeichneter Testwert/fiktiver Beispielwert stammt. Offene Punkte:
Kurvennummern/Schwelle im Code (B3), Literal 0.5 im P25-Text.

## 7. Tests – Abdeckung und Lücken

Abgedeckt: Katalog Zeile für Zeile gegen die JSON-Datei, Formelfelder, fehlerhafter Katalog, fehlende Datei; Vorgabe je Klasse,
Art von Hand/Familie/unbekannt; Referenz Walkringen rein und als Netz (1e-9); Spezialinstallationen; K-Herkunft und
-Ablehnung; Kurvenwahl <= 3 / 5; Geschosse/ohne Geschoss/ohne Art; Verzweigung KW/SW; Strang/System/Anlage; Masche KW und SW;
Wassererwärmer + ZIR; Regenwasser; P21–P25; Trockenlauf/Rückgängig/Prüfsumme; Beispielprojekt Handrechnung; MCP und REST.

Fehlende Tests:
1. Richtungsfehler mitten im Weg (B2): Apparat verbunden, Leitung davor verkehrt → heute still 0.
2. Apparat (Familie) mit zwei Anschlüssen desselben Mediums an zwei Leitungen → einmal je Leitung und an der Anlage.
3. Apparat an Systemen zweier Anlagen → Hinweis «zählen dort ebenfalls» (Code `SanitaerZusammenstellung.cs:139–141` ungetestet).
4. Apparat direkt an Formteil ohne Rohr; Apparat direkt am Hausanschluss (kein Beitrag, kein Befund – ist das gewollt?).
5. Katalog gegen das Original-Excel: nicht automatisierbar (W: nur lesen) – stattdessen Zeilenvergleich in diesem Bericht.
6. Nach B1: Tests mit «Tabelle1» auf «MFHA» umstellen.

## Annahmen, die Christian bestätigen muss

- **A1** Kurvenregel: grösster Einzel-LU der vorkommenden Arten > 3 → Spitze nach Kurve «grösster LU 5», sonst Kurve «grösster
  LU 3» (aus HKAP; das Excel wählt nicht). Dazu: Verhalten bei grösstem LU 4 oder > 5 und bei Volumenstrom ausserhalb 0.3–300
  bzw. 0.5–15 l/s.
- **A2** Kaltwasserleitung vor dem Wassererwärmer trägt ΣLU WW seiner Warmwasser-Apparate (Hausanschluss = ΣKW + ΣWW wie B55).
- **A3** Beispielprojekt: K = 0.5 (fiktiv, Wert wie Walkringen M65) an A-SAN; BA-U.02 = bodenablauf_70.
- **A4** Katalog `quelle.verfasser` «Christian Steiner, H+K Planungs AG»: Dokument zeigt creator «Hauptbenutzer», zuletzt
  geändert von «daniele gulizia», Zelle O2 «ek», erstellt 19.09.2014.
- **A5** Zusammenstellung zählt Anzahl × Wert je Apparat der Anlage auch für nicht verbundene Anschlüsse (wie Excel);
  Netzsummen nur über verbundene Wege.
- **A6** Vorgabe je Klasse (WC → wc_75, Waschtisch, Dusche, Badewanne, Spueltisch → spuelbecken, Waschmaschine →
  waschmaschine_6, Ausgussbecken → waschtrog; Bodenablauf ohne Vorgabe).
- **A7** Namensbereinigung der vier Zeilen (B4) gegenüber dem Excel.
- **A8** Masche: Apparat je Leitung einmal, Befund P24 warnung; keine Aufteilung.
- **A9** Spezialinstallationen von Hand je Anlage (3 Excel-Zeilen 47–49 → beliebig viele Einträge).
- **A10** Abflusskennzahl ohne Auswahlliste; Vorgabe in der Projektinformation; leer → P25 (kein stiller Default).

## Auflagen für die Abnahme

1. B1 beheben (Katalog `bereich`/`$kommentar` auf «MFHA», Tests nachführen).
2. B2: Befund oder Liste `ohneWeg` für Apparate ohne erreichte Leitung, mit Test.
3. A1 bestätigen und die Regel mit Quelle in den Katalog (B3); die übrigen Annahmen A2–A10 bestätigen.

## Umsetzung der Auflagen (20.10.2026)

Bauagent hlks-kern, eigener Zweig ab `8217547` (main). Katalog `kataloge/sanitaer_belastungswerte.json` **unverändert** übernommen (vom
Hauptlauf korrigiert: Blatt «MFHA», `nameExcel`, `grosserLu`/`bereichLs`/`bereichZelle`, `kurvenwahl`); kein Wert ergänzt.

| Befund | Stand | Wie |
| --- | --- | --- |
| B1 | erledigt | Kein Blattname mehr im Code. `SanitaerKatalog.Blatt` kommt aus `quelle.blatt` (optionales Feld, geht vor) bzw. aus «Blatt «…»» in `quelle.bereich`; ist keines lesbar, lehnt das Laden ab (nicht raten, auch nicht das erste Wort wie bisher). Jede «?»-Quelle lautet «Katalog sanitaer_belastungswerte.json: LU_DU Zusammenstellung_.xlsx, Blatt MFHA, Zeile n (Walkringen Überbauung Vielmattstrasse, Stand 14.03.2024)»; bei den vier bereinigten Namen zusätzlich «, im Excel «Waschrog KW»» usw. (`nameExcel`, B4). `quelle.blatt` steht in `sanitaer_katalog` und `sanitaer_zusammenstellung`, `nameExcel` in `sanitaer_katalog.arten`. Tests von «Tabelle1» auf den Katalogwert umgestellt (SanitaerKatalog-, SanitaerArt-, SanitaerZusammenstellungTests, Node-Testdaten). |
| B2 | erledigt | Neuer Befund **P26** «Apparat ohne Weg im Netz – Belastung fehlt stromab/stromauf» (warnung, am Apparat, je Anschluss mit Medium und der Stelle, an der der Weg endet), z. B. «Apparat ohne Weg im Netz – Belastung fehlt stromauf: kw (KW) ist verbunden, der Weg endet bei T2 (T2.a ist mit R2.anfang «rein» verbunden – Fliessrichtung verkehrt)». `sanitaer_belastung` hat `ohneWeg[] { apparat, anschluss, medium, fehlt, endetBei, grund }` je Anlage und gesamt; die Zusammenstellung sagt «ΣLU KW enthält 1 Apparat ohne Weg im KW-Netz: … (Befund P26)». Erkannt werden: Gegenseite verkehrt gerichtet oder anderes Medium, Apparateanschluss mit falscher Fliessrichtung, Apparateanschluss ohne Medium «*» an einer KW-/WW-/SW-Leitung. Keine Sackgasse sind ein offenes Ende (Netz nicht fertig, P01) und ein Ende an einem Gerät (z. B. Apparat direkt am Hausanschluss: kein Beitrag, kein Befund). Regel: ein erreichtes Element ohne jede Fortsetzung, an dem ein verbundener Anschluss nur wegen der Gegenseite nicht weiterführt; Formteile, die ungerichtet («beide» ↔ «beide») aneinander hängen, zählen dabei als ein Knoten – sonst wäre jede Reduktion am Anfang eines Seitenasts ein Fehlalarm (im Abnahmeszenario 3 an RD-2/RD-4 aufgefallen und so behoben). `leitung_ziehen` lehnt rein↔rein/raus↔raus schon ab; der stille Fall entsteht über ungerichtete Formteile, `verbinden` an Formteilen und Familienanschlüsse. Tests: Gedankenbeispiel des Berichts genau (R3 trägt den WC, R2/R1 nicht, kein P07, P26, `ohneWeg`, Hinweis; neu gezogen → behoben), dasselbe im Abwasser, verkehrt hinter einer Reduktion, Reduktion am Seitenast ohne Fehlalarm, Apparateanschluss «raus» und «*», offenes Ende, direkt am Hausanschluss, Verzweigungsnetz ohne P26, `BehobeneBefunde`. P-Code-Liste P01–P26 (PruefungenTests). |
| B3 | erledigt, Bestätigung offen | Kurvennummern = `grosserLu` des Katalogs (Pflicht, ganze Zahl > 0, kleinere < grössere), Schwelle der Kurvenwahl = `grosserLu` der kleineren Kurve (`SanitaerKatalog.MassgebendeKurve`); kein «3», «5» oder «== 5» mehr im Code (Test mit abgewandeltem Katalog 4/6). `rechnung.kurvenwahl` ist Pflicht (`regel`, `quelle`, `bestaetigt`). `formeln.massgebendeKurve` = Regel – «Herkunft: » + Quelltext des Katalogs wörtlich + «Nicht bestätigt.»; neues Feld `kurvenwahl { regel, quelle, bestaetigt, kurven[] { grosserLu, bereichLs, bereichZelle, … } }` in `sanitaer_zusammenstellung` und `sanitaer_katalog`; solange `bestaetigt` false, steht in `hinweise[]` «Kurvenwahl nicht im Excel, aus HKAP, nicht bestätigt: massgebend ist die Kurve «grösster LU n» nach der Regel «…»». Volumenstrom ausserhalb `bereichLs` einer Kurve → Hinweis je Kurve (die massgebende gekennzeichnet, mit `bereichZelle`), grösster Einzel-LU über dem `grosserLu` der grösseren Kurve → Hinweis; keine Befunde. **Offen:** A1 bestätigt Christian; dann `bestaetigt: true` im Katalog – der Hinweis entfällt von selbst. |
| B4 | erledigt (Hauptlauf, Katalog) | Der Kern liest `nameExcel` und nennt ihn in der Quelle (siehe B1). |
| B5.1 | erledigt | siehe B3. |
| B5.2 | erledigt | P25 lautet «Abflusskennzahl K fehlt – Wert nach Gebäudeart aus SN 592000 (liegt nicht vor) bzw. Projektvorgabe eintragen», der Vorschlag nennt keine Zahl; Kommentar in `SanitaerZusammenstellung.cs` ohne «0.5». Test prüft Text und «kein 0.5». |
| B5.3 | erledigt (Technik mit Kommentar) | Medienmengen nicht mehr eigens festgelegt: eine einzige technische Zuordnung `SanitaerKatalog.Spalten` (Spaltenköpfe B4 «LU KW», C4 «LU WW», D4 «DU» ↔ Medienkennungen aus `medien.json`); Trinkwasser = Medien der LU-Spalten, Abwasser = Medium der DU-Spalte. `medien.json` kennt kein Merkmal «Schmutzwasser», daraus ist nichts lesbar; stattdessen prüft `Katalog.Laden`, dass die Medien in `medien.json` stehen und ihre Systemtypen in `systemtypen.json` passen (LU: verbraucherRichtung stromab, DU: stromauf) – sonst Fehler beim Laden (Test). |
| B5.4 | bewusst offen (Technik mit Kommentar) | Der Faktor 0.1 steht im Katalog nur in der Zellformel «B56 = B55*0.1»; der reguläre Ausdruck liest die Zahl von dort (kommentiert). Ein eigenes Feld `rechnung.volumenstromFaktor` wäre robuster – nicht ergänzt (Auftrag: nichts am Katalog ergänzen), Vorschlag an den Hauptlauf. |
| B5.5/5.6 | unverändert | laut Bericht in Ordnung. |
| B6 | erledigt | `masche` nur noch an den Leitungen **in** der Masche (beide Wege zwischen Verzweigung und Treffpunkt, aus dem Baum der Breitensuche); Leitungen davor und dahinter tragen den Apparat eindeutig und sind nicht mehr gekennzeichnet. P24 bleibt am Treffpunkt, der Text nennt die Verzweigung («… erreichen T1 über mehrere Wege ab T2 …»). Tests: Ring (R2/R3 ja, R1/R4 nein), Ring mit Abzweig in der Masche. |
| B7 | erledigt | Hinweis je Spalte, wenn Apparate mit Wert keinen verbundenen Anschluss des Mediums haben: «ΣLU WW enthält n Apparate ohne WW-Verbindung: … – die Zusammenstellung zählt Stückzahl × Wert wie das Excel, die Netzsummen (sanitaer_belastung) zählen sie nicht.» (Beispielprojekt: 10 Apparate ohne WW-Verbindung). Apparate ohne Weg (P26) stehen im eigenen Hinweis, nicht doppelt. |

**Oberfläche:** Die Liste «Sanitär-Zusammenstellung (W3)» zeigt `hinweise[]` als vierte Tabelle «Hinweise» unter den Blöcken (nur wenn es
welche gibt, auch in der CSV); der Kopf nennt nur ihre Zahl. P26 erscheint ohne Eingriff im Dialog Prüfbefunde (Browser-Reihe, Abschnitt 7).
Die Browser-Reihe `sanitaer-belastung.browser.mjs` nimmt jetzt die Ports 5200–5219.

**Fehlende Tests aus Abschnitt 7:** 1 (Richtungsfehler mitten im Weg), 4 (Apparat direkt am Formteil bzw. am Hausanschluss) und 6 (Tabelle1 →
MFHA) erledigt; 2 (zwei Anschlüsse desselben Mediums an zwei Leitungen) und 3 (Apparat an Systemen zweier Anlagen) bleiben offen – nicht Teil
dieser Auflagen.

**Weiter offen (ausserhalb der Auflagen):** Oberfläche – Hinweis unter K in der Palette (`sanitaer-text.js` `K_HINWEIS` «… Walkringen: 0.5»)
nennt die Zahl fest statt aus `sanitaer_katalog`; die Liste beschriftet die Kurven fest mit «grösster LU 3/5» (die Wahl selbst kommt vom
Kern) – beides nachziehen, wenn sich `grosserLu` im Katalog ändern sollte. Annahmen A1–A10 weiter von Christian zu bestätigen.

**Tests** (Build `%USERPROFILE%\HLKS_CAD_build\m4a-auflagen`, HLKS_KATALOG = Kataloge der Arbeitskopie, HLKS_TEST=1, alle HLKS_*-Ablagen
unter %TEMP%): Kern Sanitär-Klassen + Prüfungen 153/153; Kern gesamt 3997, davon 3994 im Gesamtlauf grün, 3 rot nur wegen Zeitgrenzen bzw.
Dateisperre unter Last (IfcEchtRauchTests …Dauer ×2, SpeicherTests.PaketLesenWartetAufGesperrteDatei) – einzeln wiederholt grün (zusammen mit
den Zeitgrenzen-Tests des ersten Laufs 10/10); MCP 63/63; Web 475/475; Node gesamt 888 (876 grün, 12 übersprungen – brauchen einen Server, 0 rot),
davon sanitaer-belastung.test.mjs 29/29; Browser-Reihen sanitaer-belastung 121/0, koordination 191/0, schema 93/0 (Ports 5200–5202, CDP 9550–9552).