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

## Teil B – Dimensionierung (gebaut 03.11.2026, Entwurf zur Prüfung)

DN je Teilstrecke Trinkwasser (Belastungswerttabellen W3), Abwasser (SN 592000/SN EN 12056: Q_WW je Teilstrecke, Füllung,
Gefälle, Lüftung), Hausanschluss (Tabelle 5 W3). Erst mit den Normtexten im Normenverzeichnis; Werte nur mit Zitat
(Norm, Ausgabe, Ziffer, Seite), Normtabellen als versionierte JSON, Christian prüft die Übertragung gegen.

**Bestätigt:** Christian, 20.10.2026 (Fragefenster): Kurvenwahl «grösster LU > 3 → Kurve 5» (aus HKAP), KW vor dem Wassererwärmer trägt ΣLU WW, Vorgabe-Apparateart je Klasse, Beispielprojekt K = 0.5 fiktiv. **Teil B** startet, sobald Christian den Ablageort der Normen nennt.

### Stand 03.11.2026 (Nacht; Christian: «Sanitär KI modul weiter bearbeiten … du hast da gestern neue daten von mir bekommen», «ohne rückfragen»)

- **Quelle:** Handbuch Gebäudetechnik Sanitär 2025 (W:\05_Normen und Merkblätter; Lehrmittel mit SN 592000:2024- und W3-Auszügen), Fundstellen
  in berichte/handbuch-sanitaer-fundstellen.md; Tabellen von zwei hlks-modul-Agenten mit Befund-Id je Wert übertragen (Abwasser: T5–T14, T22;
  Trinkwasser: Leitungsarten, Hausanschluss, 48 Herstellertabellen S. 53–69, Diagramm 1 – gleich der Büro-Excel «Diagramm 1 (2013)»).
- **Ablage der Werte (Entscheid Hauptlauf, freie Hand):** NICHT im Repository («Normtexte nie ins Repository», Impressum schränkt Weitergabe ein),
  sondern im Büroordner `%OneDrive%\HLKS-CAD\normtabellen\sanitaer\{abwasser,trinkwasser}.json` (Umgebungsvariable HLKS_NORMTABELLEN bzw.
  buero.json › normtabellen.pfad); Tests mit fiktiven Tabellen.
- **Gebaut:** Abfrage `sanitaer_dimensionierung { anlage?, leitung? }` (Kern SanitaerDimensionierung.cs, MCP, KI-Gruppe), Rechenweg im Register
  (sanitaer.abwasser_dimensionierung, sanitaer.trinkwasser_dimensionierung).
- **Unabhängige Prüfung (hlks-pruefer, 03.11.2026) eingearbeitet:** Übertragung fehlerfrei; behoben F1 (KW vor dem Wassererwärmer mit ΣLU WW),
  F2 (abgewickelte Länge je Abschnitt V/A/T), F3 (K ohne Anlage aus der Projektinformation), L1 (Strang vor Ein-Apparat-Regel), L2 (DN in
  Abflussrichtung nie kleiner, Handbuch S. 26), L3 (Tabellenende mit Hinweis), L4 (Schleifung als Annahme), L5 (K = 0.5 angenommen genannt),
  L6 (Gefälle gegen Tabelle 6), L7 (Anschlussleitung nach Länge: T9 ≤ 4 m, ein Apparat ohne WC bis 10 m eine DN grösser, sonst T10 mit Umlüftung),
  L8 (mehrdeutige Systemtabelle gemeldet).
- **Probelauf Praxisprojekt (Kopie Test_praxis):** Die importierten Stränge fassen alles zusammen (Zuleitungen ab Etagenverteiler im «steigstrang»,
  Fallstrecken und Fuss im «fallstrang»). Darum: Verteiler stromauf = Schnittstelle (dahinter A/T); im Fallstrang waagrecht mit Fallleitung
  stromab = Anschlussleitung, ohne = Fuss (Sammelleitung), steil in eine Anschlussleitung mündend = Fallstrecke. Ergebnis: Einzelzuleitungen A
  16x2.25 = gezeichnet, Steigstrang V 32x3 = gezeichnet; Fallleitungen mit WC DN 100; Waschtisch-/Dusch-/Badewannenanschlüsse d50 kleiner als
  T9/T5 verlangt (DN 56/60) – Befund für Christian. Verzüge (Achsverschiebung) mitten im Fallstrang würden die obere Fallleitung als Anschluss lesen.
- **Offene Fragen an Christian:** Stufen aufrunden?; Gruppenwasserzähler; Schnittstelle V/A/T = Verteiler bzw. Strangende (Gruppenabsperrung);
  Fallleitung Haupt-/Nebenlüftung und Abzweigart je Strang (Eigenschaft?); Mindest-DN Grundleitung (Handbuch 100/150
  widersprüchlich); K ≠ 0.5 bei Fallleitungen; DU-Abweichungen Katalog (wc_90, ausguss_faekalien, steckbeckenapparat 2.5 statt 2.0); Gegenprüfung
  der Bildwerte (S. 52–69) und gegen die Originalnormen.
