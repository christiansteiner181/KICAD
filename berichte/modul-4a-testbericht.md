# Testbericht Modul 4 Teil A – Sanitär rechnen: Belastungswerte (ENTWURF)

Stand 20.10.2026 · gebaut am 20.10.2026 · Commit `6dc390e` (main; `3cb0641` darüber ändert nur das Protokoll) · **Entwurf, Abnahme durch Christian offen; unabhängige Prüfung des Rechenkerns läuft**

Hinweis: Entwurf von hlks-test (sonnet/medium) aus `entwurf/modul-4.md`, `zuteilung.md`, `src/Hlks.Web/OBERFLAECHE.md` (Abschnitte «Sanitär-Belastungswerte», Teil A-K und A-O)
und dem Code (Stellen genannt). Ich habe für diesen Bericht **keinen Test gestartet**; alle Zahlen stammen aus `zuteilung.md` (Läufe der Bauagenten und des Hauptlaufs).
Der Prüfbericht des Rechenkerns (`berichte/modul-4a-pruefbericht.md`, hlks-pruefer) **liegt noch nicht vor – Prüfung läuft**. Der Gesamtlauf auf `6dc390e` lief beim Schreiben noch.

## 1. Kurzfassung

Teil A des Moduls Sanitär rechnen ist normunabhängig: einzige fachliche Quelle ist dein Excel `LU_DU Zusammenstellung_.xlsx` (Objekt Walkringen, 14.03.2024), übertragen in
`kataloge/sanitaer_belastungswerte.json` (34 Apparatearten, Formeln mit Zellbezug, Referenzergebnis; gegen den HKAP-Katalog abgeglichen). Es gibt **keine Dimensionierung** (Teil B).

- Jeder Sanitärapparat hat eine **Apparateart** (Auswahl mit Namen; ohne Wahl gilt die Vorgabe der Klasse). Daraus folgen **LU KW, LU WW und DU**, im «?» mit Excel-Zeile.
- Jede **Leitung** des Trinkwasser- und Abwassernetzes kennt ihre Summe: ΣLU KW/WW und grösster Einzel-LU (stromab), ΣDU (stromauf). Das **Strangschema** beschriftet sie («ΣLU 12 / 8», «ΣDU 4.6»).
- Die Liste **«Sanitär-Zusammenstellung (W3)»** je Anlage sieht aus und rechnet wie dein Excel (Apparate je Geschoss, Total LU, Volumenstrom, Kurven «grösster LU 3» und «grösster LU 5»,
  Spezialinstallationen, ΣDU, Abflusskennzahl K, Q_WW, Q_tot), mit CSV-Ausgabe. Knopf: Gebäudetechnik › Apparate und Armaturen › «Sanitär-Zusammenstellung».
- Fehlende oder widersprüchliche Angaben erscheinen als **Befunde P21–P25** (Verwalten › Prüfbefunde).

Der Rechenkern erreicht die Excel-Referenz Walkringen auf 1e-9 (Kern-Test). Die Oberfläche rundet nur für die Anzeige.

## 2. «Fertig, wenn …» Punkt für Punkt

Spezifikation (Teil A): «Christian bei jedem Sanitärapparat die Apparateart (und damit LU KW, LU WW, DU) sieht und ändern kann; jede Leitung des Trinkwasser- und Abwassernetzes ihre
Belastungssumme kennt (im Strangschema und in den Eigenschaften); die Sanitär-Zusammenstellung je Anlage wie sein Excel aussieht und rechnet (Referenz Walkringen auf die letzte Stelle gleich);
fehlende oder widersprüchliche Angaben als Befund erscheinen.»

| Kriterium | Ergebnis | Nachweis |
| --- | --- | --- |
| 1 Apparateart sehen und ändern, LU KW/WW und DU | erfüllt | Eigenschaftenpalette: Zeile «Apparateart» (alle 34 Arten mit Namen, erster Eintrag «Vorgabe der Klasse (…)»), darunter LU KW / LU WW / DU mit «?» (Quelle «LU_DU Zusammenstellung_.xlsx, Zeile n»). Beispiel Waschtisch WT-E.02: 1 / 1 / 0.5 (Zeile 8); nach Wahl «Badewanne» 3 / 3 / 0.8 (Zeile 17), live. Browser-Reihe `sanitaer-belastung` (114 Prüfungen, neu). |
| 2 Belastungssumme an jeder Leitung, im Schema und in den Eigenschaften | erfüllt | Beispielprojekt: KW-V1-0 ΣLU KW 24, ΣLU WW 0, grösster Einzel-LU 3, «15 Apparate (Apparate stromab)»; SW-G1-1 ΣDU 16.7; «?» mit Rechenweg und Tabelle der Beiträge (Apparat, Art, Excel-Zeile). Schema trägt «ΣLU 24» und «ΣDU 16.7»; Schalter `belastung` aus/ein; kein Text überdeckt einen anderen. Änderung der Apparateart ändert die Summen (24 → 26, 16.7 → 17). Kern: `SanitaerNetzTests`, `SchemaBelastungTests`. |
| 3 Zusammenstellung wie das Excel, Referenz Walkringen auf die letzte Stelle gleich | Kern erfüllt, Oberfläche nur am Beispielprojekt | Kern-Test `SanitaerZusammenstellungTests` mit der Walkringen-Referenz aus dem Katalog: ΣKW 80, ΣWW 52, ΣDU 38.8, Total LU 132, 13.2 l/s, Spitze 1.1412355…, 1.1606163…, Q_WW 3.1144823… (K 0.5) auf **1e-9**, einmal rein aus den Stückzahlen, einmal als Projekt mit Netz über die Befehle. Liste in der Oberfläche am **Beispielprojekt** (nicht an Walkringen): drei Tabellen, Spalten wie das Excel, Total LU 42, 4,20 l/s, Kurve 3 massgebend (0,762 l/s), ΣDU 16,7, Q_WW 2,04 l/s; Tooltips mit Formel und Excel-Zelle (z. B. B55 = M43+N43). |
| 4 Fehlende/widersprüchliche Angaben als Befund | erfüllt | P21 Apparat ohne Apparateart; P22 Apparateart passt nicht zu den Anschlüssen; P23 Apparat in keiner Anlage; P24 Netz mit Masche; P25 Abflusskennzahl fehlt (Kern `SanitaerPruefung.cs`, `Pruefungen.cs`). Erscheinen von selbst im Dialog «Prüfbefunde»; getestet für P25. |

Weitere Zusicherungen:

| Zusicherung | Nachweis |
| --- | --- |
| Keine Normwerte aus dem Gedächtnis; Werte nur mit Quelle | Jeder Wert führt Quelle und Excel-Zeile; Node-Test `sanitaer-belastung.test.mjs` prüft, dass in `sanitaer-*.js` weder Rechenkonstanten noch Wurzeln stehen. K ohne Auswahlliste, Hinweis «Wert aus SN 592000 nach Gebäudeart – Norm liegt nicht vor; Walkringen: 0.5». |
| Maschen: Apparat höchstens einmal zählen | Kern-Test (Masche), Befund P24 |
| Zirkulation und Regenwasser ausgenommen | Kern-Test |
| Spezialinstallationen von Hand (Excel Zeilen 46–51) | Tabelle in der Anlage; Browser: 0,5 l/s Versorgung → Spitze gesamt wächst um 0,5; negativer Wert vom Kern abgelehnt; kein «[object Object]». |
| Liste mit CSV | Knopf «CSV …»; alle drei Tabellen untereinander, zusätzliche Spalte «Formel (Excel-Zelle)». |
| Druck der Liste in Planblättern | **nicht erfüllt**: Listen sind im Kern nicht platzierbar (`ansicht_platzieren` nimmt nur Grundriss, Schnitt, Ansicht, Schema; gilt für alle Listen). Ausgabe ist die CSV-Datei. |
| MCP/Abfragen | `sanitaer_belastung`, `sanitaer_zusammenstellung`, `sanitaer_katalog`; `sanitaerArt` über `element_aendern`. MCP-Reihe 63 grün. |

## 3. Testzahlen (aus `zuteilung.md`, nicht von mir gestartet)

| Stand | Kern | MCP | Web | Node | Browser |
| --- | --- | --- | --- | --- | --- |
| Teil A-K, `632beb1` (hlks-kern), zusammengeführt `6e892bd` | 3933 (1 Zeitverhalten, einzeln grün) | 54; nach Zusammenführen 63 | 433 | 816 | schema 93, leitung 114, koordination 191; Kern-Auswahl 189 grün |
| Teil A-O, `51a0234` (hlks-modul), zusammengeführt `6dc390e` | 3982 (1 Zeitgrenze IFC unter Last, einzeln grün) | 63 | 471 | 844; nach Zusammenführen **875 von 887** (12 übersprungen) | **sanitaer-belastung 114 (neu)**, menueband 191 (beide Symbolstile), schema 93, leitung 114, koordination 191, n3 88, serienpdf 67 |
| `e791723` (enthält A-K; `stand_pruefen` grün) | 3965 | 63 | 471 | 817 | 20 Reihen / 21 Läufe grün ohne Wiederholung → geprüfter Stand `e791723` |
| **Endstand-Kandidat `6dc390e`** | Lauf offen | | | | läuft (hlks-test, Ports 5190–5199) |

Neu sind im Teil A-K die Kern-Tests `SanitaerKatalogTests`, `SanitaerArtTests`, `SanitaerNetzTests`, `SanitaerZusammenstellungTests`, `SanitaerBeispielTests`, im Teil A-O
`SchemaBelastungTests`, Node `sanitaer-belastung.test.mjs` und die Browser-Reihe `sanitaer-belastung` (114). Die genaue Zahl nur der neuen Tests ist aus `zuteilung.md` nicht abzuleiten, weil
die Läufe auf verschiedenen Zweigen mit anderen Ständen liefen. Summen für `6dc390e`: **stehen aus** (Abschnitt 7).

## 4. Befunde und ihre Behebung

| # | Befund | Fund | Behebung |
| --- | --- | --- | --- |
| 1 | Zusammenführen von A-K mit 3c-K: beide Seiten in Konflikt (Werkzeugzahl 125) | Hauptlauf | beide Seiten übernommen; MCP 63 und Kern-Auswahl 189 grün |
| 2 | Zusammenführen von A-O mit 3c: Konflikte in `symbole-revit.js` und `OBERFLAECHE.md` | Hauptlauf | beide Seiten übernommen |
| 3 | 1 Kern-Test (Zeitverhalten bzw. IFC-Zeitgrenze) unter Last rot, einzeln grün | A-K, A-O | kein Befund am Produktcode, lastabhängig |
| 4 | **Offen:** Listen sind nicht auf Planblättern platzierbar (alle Listen) | A-O | Kern-Erweiterung für alle Listenarten, Entscheid Christian |
| 5 | **Offen:** K ohne Quellenfeld | A-O | mit Teil B (SN 592000) |

Aus meiner Sicht **keine weiteren Befunde am Produktcode**; ich habe keine Tests ergänzt oder geändert. Befunde des Prüfers kommen mit dessen Bericht.

## 5. Annahmen zur Bestätigung

Bitte mit Ja/Nein oder Wert beantworten.

1. **Vorgabe-Apparateart je Klasse** (wie HKAP, im Katalog `vorgabeJeKlasse`): WC → Klosettanlage bis 7.5 Liter Spülwassermenge (`wc_75`), Waschtisch → Waschtisch/Wandbecken, Dusche → Dusche nicht staubar, Badewanne, Spültisch → Spülbecken 1- und 2-teilig, Waschmaschine → bis 6 kg (`waschmaschine_6`), Ausgussbecken → Waschtrog. **Bodenablauf ohne Vorgabe** (Nennweite unterschiedlich → Befund P21 «Art wählen»). Stimmen die Zuordnungen?
2. **Kaltwasserleitung vor dem Wassererwärmer trägt auch die ΣLU WW** (Warmwasser wird dort aus Kaltwasser bereitet). Richtig, oder soll vor dem Erwärmer nur KW zählen?
3. **Beispielprojekt:** K = 0.5 ist fiktiv (der Excel-Wert gilt nur für Walkringen), der Bodenablauf BA-U.02 ist auf «Bodenablauf DN 70» (`bodenablauf_70`) gesetzt. Nur für Tests und Anleitung, nie als Vorgabe. Einverstanden?
4. **Rundung der Anzeige** (der Kern rechnet ungerundet, «?» und Tooltips zeigen den genauen Wert): LU ganze Zahl, sonst eine Stelle; DU immer eine Stelle («2,0», «38,8»); l/s zwei Stellen, unter 1 l/s drei («1,14», «0,762»; Walkringen: 1.1412… → 1,14, Q_WW 3.1144… → 3,11, Volumenstrom 13,20). Dezimaltrenner: Palette Punkt, Liste und CSV Komma, Strangschema Punkt («ΣDU 4.6»). Passt das, oder soll überall Komma gelten?
5. **Listen nicht auf Planblättern platzierbar:** Die Zusammenstellung geht vorerst nur als CSV hinaus. Soll das Platzieren von Listen auf Planblättern (für alle Listen) als Kern-Erweiterung gebaut werden?
6. **Massgebende Kurve automatisch** aus dem grössten Einzel-LU (> 3 → «grösster LU 5», sonst «grösster LU 3», wie HKAP); die andere Kurve steht klein daneben. Einverstanden?
7. **Beschriftung im Strangschema** «ΣLU 12 / 8» (Kalt-/Warmwasser), «ΣLU 12», «ΣLU – / 8», «ΣDU 4.6»; Vorgabe ein in jedem Schema mit Sanitärleitungen, nicht im Detailgrad «grob». Lesbar?
8. **Spezialinstallationen** als Tabelle an der Anlage (Bezeichnung, Versorgung l/s, Entsorgung l/s), von Hand, ohne Auswahl. **K** als freie Zahl ohne Auswahlliste. Einverstanden?
9. **Übertragung des Excel in den Katalog** (Tabelle1 A7:D40, Formeln C57/I57/M66, Referenz Walkringen): bitte stichprobenweise gegen dein Excel prüfen (Abschnitt 6, Schritt 1–2).
10. **IFC-Export der Belastungswerte** erst mit Modul 7. Zurückstellen in Ordnung?

## 6. Abnahme-Prüfschritte für Christian

Voraussetzung: KICAD auf dem geprüften Stand (Zweig «geprueft»; `6dc390e` erst nach grünem Endlauf), Beispielprojekt «MFH Musterweg» offen.

1. Waschtisch **WT-E.02** wählen: «Apparateart» steht auf der Vorgabe; LU KW 1, LU WW 1, DU 0.5. Auf das «?» klicken: Quelle «LU_DU Zusammenstellung_.xlsx», **Zeile 8** – mit deinem Excel vergleichen.
2. Badewanne **BW-1.02**: Zeile 17. Die Art von WT-E.02 auf «Badewanne» stellen: Werte 3 / 3 / 0.8; mit Strg+Z zurück.
3. Kaltwasserleitung **KW-V1-0** wählen: ΣLU KW 24, grösster Einzel-LU 3, «15 Apparate»; «?» öffnen und die Beiträge der Apparate gegen das Excel prüfen. Abwasser **SW-G1-1**: ΣDU 16.7.
4. Strangschema **AN-SCHEMA-SAN** öffnen: «ΣLU 24» und «ΣDU 16.7» an den Leitungen; in den Eigenschaften der Ansicht «belastung» aus- und einschalten.
5. Gebäudetechnik › Apparate und Armaturen › **«Sanitär-Zusammenstellung»**: die Liste öffnen; Spalten, Totalzeile (16 Apparate, ΣLU 24 / 18, ΣDU 16,7), Kurven, Q_WW mit dem Excel vergleichen; mit dem Zeiger über Zeilen fahren (Formel und Excel-Zelle).
6. Anlage **A-SAN** wählen: Spezialinstallation «Garteneinrichtung», Versorgung 0,5 eintragen – Spitze gesamt wächst um 0,5 l/s; Zeile mit «×» löschen.
7. K an der Anlage löschen: die Liste zeigt «K fehlt»; Verwalten › Prüfbefunde zeigt P25. Wert wieder eintragen.
8. Am Bodenablauf BA-U.02 die Art entfernen: Befund P21. Die Befunde P21–P25 im Dialog «Prüfbefunde» durchsehen.
9. In der Liste «CSV …» wählen und die Datei in Excel öffnen (drei Tabellen, Spalte «Formel (Excel-Zelle)», Dezimalkomma).
10. Vorschlag: Walkringen mit den Stückzahlen aus deinem Excel in einem Projekt nachbauen, damit die Oberfläche (nicht nur der Kern-Test) dieselben Zahlen zeigt: ΣKW 80, ΣWW 52, ΣDU 38.8, Total LU 132.

## 7. Offene Punkte

| # | Punkt | Wer / wann |
| --- | --- | --- |
| 1 | **Teil B Dimensionierung** (DN je Teilstrecke Trinkwasser, Abwasser Q_WW je Teilstrecke, Füllung, Gefälle, Lüftung, Hausanschluss Tabelle 5 W3) wartet auf **SN 592000, SN EN 12056 und SVGW W3**; sie liegen in `W:\05_Normen und Merkblätter` nicht vor (Suche 20.10.2026 00:20: nur suissetec-Merkblätter, W3/E3-Übergabeprotokolle, «Temperaturen W3_E3.pdf», SN R592 014-1). Werte nur mit Zitat, Normtabellen als versionierte JSON, du prüfst die Übertragung | Christian beschafft die Normen oder nennt den Ablageort |
| 2 | **Normenverzeichnis:** Volltext erst für 62 von 315 Normen; die Suche kann Stellen übersehen | Hauptlauf / Aufbau |
| 3 | **Unabhängige Prüfung** des Rechenkerns (Übertragung Excel → Katalog, Formeln, Netzsummen, Befunde, Normwerte-Regel, Tests) durch hlks-pruefer: Bericht `berichte/modul-4a-pruefbericht.md` steht aus – Abnahme erst danach | hlks-pruefer |
| 4 | Endlauf `6dc390e` (Kern, MCP, Web, Node, alle Browser-Reihen, `stand_pruefen`) – Zahlen in Abschnitt 3 nachtragen | hlks-test / Hauptlauf |
| 5 | Listen auf Planblättern (alle Listen) – Kern-Erweiterung, Entscheid Christian (Annahme 5) | Christian |
| 6 | K ohne Quellenfeld; keine Auswahlliste nach Gebäudeart bis SN 592000 vorliegt | mit Teil B |
| 7 | Referenz Walkringen in der Oberfläche (statt nur im Kern-Test) nachbauen (Schritt 10) | Hauptlauf auf Wunsch |
| 8 | Bedienungsanleitung: Kapitel «Sanitär rechnen – Belastungswerte» (10 Funktionen) ist im Verzeichnis, Bilder folgen mit Szenen im Bildskript | Hauptlauf / hlks-modul |

## 8. Zuteilung und Modellwahl (aus `zuteilung.md`)

| Teil | Inhalt | Agent | Ergebnis |
| --- | --- | --- | --- |
| – | Normenlage, Spezifikation, Katalog aus dem Excel | Hauptlauf | ok |
| A-K | Apparateart, Belastungssummen, Zusammenstellung, Befunde, MCP | hlks-kern | ok, keine Nacharbeit |
| A-O | Eigenschaften, Strangschema, Liste, Knopf | hlks-modul | ok, keine Nacharbeit |
| – | Unabhängige Prüfung des Rechenkerns | hlks-pruefer (fable/high) | läuft |
| – | Testbericht (dieser Entwurf) | hlks-test | Entwurf |

Keine Nacharbeit: für hlks-kern und hlks-modul **keine Änderung** der Zuteilungstabelle vorgeschlagen (Rechenkern nach Spezifikation bleibt bei hlks-kern, Prüfer vor der Abnahme). Entscheid beim Hauptlauf.

## Bekannte Grenzen dieser Prüfung

- Ich habe keinen Test gestartet; alle Testzahlen stammen aus `zuteilung.md`. Der Endlauf auf `6dc390e` und der Prüfbericht des Rechenkerns fehlen.
- Die Oberfläche ist nur am fiktiven Beispielprojekt geprüft (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv); die Referenz Walkringen steht nur im Kern-Test.
- Excel-Zeilen, Formeln und Referenzwerte stammen aus Katalog, Spezifikation und Tests; ich habe sie nicht am Excel selbst nachgeprüft und keine Normwerte eingetragen.
- Die Anzahl nur der neuen Tests ist aus dem Protokoll nicht ableitbar (verschiedene Zweige und Stände).
