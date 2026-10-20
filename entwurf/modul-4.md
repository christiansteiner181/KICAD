# Modul 4 – Sanitär rechnen: Spezifikation Teil A (Belastungswerte)

Stand 20.10.2026, 00:30 · Hauptlauf · Start auf Christians Zuruf 19.10.2026, 22:50: «Beginne danach mit den Folgemodulen.
Arbeite bis 08:00 durch.»

## Ausgangslage und Abgrenzung

- **Normen fehlen weiterhin:** SN 592000, SN EN 12056-1…5 und SVGW W3 liegen nicht in `W:\05_Normen und Merkblätter`
  (Suche 20.10.2026 00:20: nur suissetec-Merkblätter, W3/E3-Übergabeprotokolle, «Temperaturen W3_E3.pdf», SN R592 014-1
  Apparate-Anforderungen). Ohne Normtexte **keine Dimensionierung** (DN je Teilstrecke, Belastungswerttabellen,
  Gefälle-/Füllungsnachweis, Abflusskennzahlen ausser dem Excel-Wert) – das bleibt Teil B, sobald Christian die Normen
  beschafft bzw. den Ablageort nennt.
- **Teil A ist normunabhängig gebaut:** einzige fachliche Quelle ist Christians Excel `LU_DU Zusammenstellung_.xlsx`
  (Objekt Walkringen, 14.03.2024), übertragen in `kataloge/sanitaer_belastungswerte.json` (34 Apparatearten, Formeln mit
  Zellbezug, Referenzergebnis). Dieselbe Quelle nutzt HKAP (Projektieren-Modul, 28.08.2026 gegen das Excel geprüft).
- Rechenkern → Prüfung durch hlks-pruefer vor der Abnahme (CLAUDE.md).

## Fertig, wenn … (Teil A)

Christian bei jedem Sanitärapparat die Apparateart (und damit LU KW, LU WW, DU) sieht und ändern kann; jede Leitung des
Trinkwasser- und Abwassernetzes ihre Belastungssumme kennt (im Strangschema und in den Eigenschaften); die
Sanitär-Zusammenstellung je Anlage wie sein Excel aussieht und rechnet (Referenz Walkringen auf die letzte Stelle gleich);
fehlende oder widersprüchliche Angaben als Befund erscheinen.

## Teil A-K – Kern (hlks-kern)

1. **Katalog laden** (`kataloge/sanitaer_belastungswerte.json`, nur lesen; Quelle bei jedem Wert mitführen).
2. **Apparateart** an der Klasse `Apparat` (kataloge/klassen.json): Eigenschaft `sanitaerArt` (Auswahl = Schlüssel des
   Katalogs, Anzeige mit Namen). **Vorgabe je Klasse** (wie HKAP, im Katalog als `vorgabeJeKlasse` ergänzen): WC → `wc_75`,
   Waschtisch → `waschtisch`, Dusche → `dusche`, Badewanne → `badewanne`, Spueltisch → `spuelbecken`, Waschmaschine →
   `waschmaschine_6`, Ausgussbecken → `waschtrog`; **Bodenablauf ohne Vorgabe** (DN unterschiedlich → Befund «Art wählen»).
   Abgeleitete Werte `luKw`, `luWw`, `du` (mit «?»-Herkunft: Katalog, Excel-Zeile). Familien dürfen die Art im Typ
   vorgeben (Familienparameter `sanitaerArt`).
3. **Belastungssummen im Netz** (`src/Hlks.Kern/Rechnen/SanitaerBelastung.cs`): auf dem Netz aus Modul 3 (Stränge,
   Fliessrichtung, «Stromab/Stromauf folgen»):
   - Trinkwasser (Medien KW, WW; Zirkulation ausgenommen): je Leitung bzw. Teilstrecke die Summe der LU aller Apparate
     **stromab** (getrennt KW und WW) und der **grösste Einzel-LU** stromab; je Strang und je Anlage.
   - Abwasser (SW bzw. die Schmutzwasser-Medien des Katalogs `medien.json`; Regenwasser ausgenommen): je Leitung die Summe
     der DU aller Apparate **stromauf**; je Strang, Grundleitung, Anlage.
   - Keine Normrechnung je Teilstrecke (kein Spitzendurchfluss, kein DN, kein Q_WW je Teilstrecke) – das ist Teil B.
   - Maschen/Ringleitungen: Apparat höchstens einmal zählen; Befund «Netz mit Masche – Summe nicht eindeutig».
4. **Zusammenstellung je Anlage** (wie Excel): Zeilen = vorkommende Apparatearten in Katalogreihenfolge, Spalten =
   Geschosse (Reihenfolge des Gebäudes), Anzahl, ΣLU KW, ΣLU WW, ΣDU; darunter Total LU, Volumenstrom (×0.1), Spitze Kurve
   «grösster LU 3» und «grösster LU 5» (Formeln aus dem Katalog), massgebende Kurve automatisch aus dem grössten
   Einzel-LU (> 3 → Kurve 5, wie HKAP; die andere klein daneben), Spezialinstallationen (Versorgung/Entsorgung l/s, von Hand
   je Anlage, wie Excel Zeilen 46–51), Gesamt-Spitzendurchfluss, ΣDU, Abflusskennzahl K, Q_WW, Q_tot.
   **K** ist eine Eigenschaft der Anlage (bzw. Projektinformation als Vorgabe) ohne Auswahlliste; Vorgabe leer → Befund
   «Abflusskennzahl fehlt» (der Excel-Wert 0.5 gilt nur für Walkringen; weitere Werte stehen in SN 592000, die fehlt).
5. **Befunde** (Prüfsystem des Kerns): Apparat ohne Art; Art mit LU KW, aber kein KW-Anschluss verbunden (und WW, DU
   analog); Anschluss verbunden, Art ohne diesen Wert; Apparat in keiner Anlage; Masche.
6. **MCP/Abfragen:** `sanitaer_belastung {anlage?|leitung?}` (Summen), `sanitaer_zusammenstellung {anlage}`; Eigenschaft
   `sanitaerArt` über die bestehenden Befehle änderbar.
7. **Tests:** Referenzfall Walkringen (Katalog `referenz`): Projekt mit den Excel-Stückzahlen → ΣKW 80, ΣWW 52, ΣDU 38.8,
   Total LU 132, 13.2 l/s, Spitze 1.1412355…, 1.1606163…, Q_WW 3.1144823… (K 0.5) – auf 1e-9; Netztests (Verzweigung,
   Stromab-/Stromauf-Summen, Masche, fehlende Art, Zirkulation/Regenwasser ausgenommen); Beispielprojekt MFH Musterweg.

## Teil A-O – Oberfläche (hlks-modul, nach A-K)

- Eigenschaften: Apparateart als Auswahl (Name), darunter LU KW / LU WW / DU (abgeleitet, «?» zeigt Quelle «LU_DU
  Zusammenstellung_.xlsx, Zeile n»); bei Leitungen die Belastungssummen (abgeleitet).
- Strangschema: Beschriftung je Teilstrecke «ΣLU 12 / 8» bzw. «ΣDU 4.6» (Ansichtseigenschaft `belastung` ein/aus,
  Vorgabe ein in Sanitär-Schemas).
- Listenansicht «Sanitär-Zusammenstellung (W3)» je Anlage (Liste anlegen), Aufbau wie das Excel, mit CSV (N3); Druck in
  Planblätter wie andere Listen.
- Register Gebäudetechnik bzw. Analysieren: Knopf «Sanitär-Zusammenstellung» öffnet die Liste der aktiven Anlage.
- Befunde im Befund-Browser bzw. Koordination wie andere Prüfungen.
- Tests: Browser-Reihe `sanitaer-belastung.browser.mjs`, Bilder.

## Teil B – Dimensionierung (wartet auf Normen)

DN je Teilstrecke Trinkwasser (Belastungswerttabellen W3), Abwasser (SN 592000/SN EN 12056: Q_WW je Teilstrecke, Füllung,
Gefälle, Lüftung), Hausanschluss (Tabelle 5 W3). Erst mit den Normtexten im Normenverzeichnis; Werte nur mit Zitat
(Norm, Ausgabe, Ziffer, Seite), Normtabellen als versionierte JSON, Christian prüft die Übertragung gegen.

**Bestätigt:** Christian, 20.10.2026 (Fragefenster): Kurvenwahl «grösster LU > 3 → Kurve 5» (aus HKAP), KW vor dem Wassererwärmer trägt ΣLU WW, Vorgabe-Apparateart je Klasse, Beispielprojekt K = 0.5 fiktiv. **Teil B** startet, sobald Christian den Ablageort der Normen nennt.
