# Phase 1 Rang 5 – Sanitärstrang einer Wohnung in Test_praxis (Arbeitsplan)

Stand 30.10.2026 · Hauptlauf. Entscheid Christian 30.10.2026: «Sanitärstrang einer Wohnung»; «sanitärobjekte müssen mit Geberit Duofix oder GIS
nachmodeliert werden»; «küche wird mit jrg wand dosen gelöst».

Zweck: Gebäudetechnik in Test_praxis (W:, reine Architekturgrundlage seit 30.10.2026), an der die offenen Abnahmen 3, 3b, 4A, 7A, 7B (und 3c als
Werkzeug) am echten Projekt durchgespielt werden. Der Hauptlauf schreibt als KI (`X-Hlks-Akteur-Art: ki`, `begruendung`/`prozessschritt` «Rang 5»).

## Wohnung

Ostwohnung 1.OG (Kote 3060). Lagen aus der Architekten-IFC (Archicad, Bibliotheksteile «… 27»), Projektkoordinaten mm, Drehung der Objekt-x-Achse:

| Objekt (IFC) | x | y | Raum | Drehung |
| --- | --- | --- | --- | --- |
| Wand WC | 10 397 | −47 226 | R-34/35 | 47.3° |
| Handwaschbecken | 10 000 | −47 125 | | −132.7° |
| Wand WC | 10 509 | −46 574 | | −132.7° |
| Handwaschbecken | 10 940 | −46 638 | | 47.3° |
| Badewanne | 10 585 | −48 011 | R-35 | −42.7° |
| Duschwanne | 10 814 | −46 243 | | −42.7° |
| Küche (Konzeptküche) | 7 181 | −53 122 | | 137.3° |

Die beiden WCs stehen Rücken an Rücken an der Wand zwischen R-34 und R-35 → **Steigzone dort** (um 10 450 / −46 900), Strang senkrecht vom
1.UG (Verteilung unter Decke) bis 1.OG; nur diese Wohnung angeschlossen. Echte Planung H+K (IFC 1-0768, Revit) hat «Vorwandinstallation WC» und
«Waschtisch mit Zählerstrecken» – bestätigt die Duofix-Vorgabe; ihre Stranglage wurde nicht übernommen (anderes Koordinatensystem).

## Familien

- Apparate: KI-Aufträge «Familie aus Unterlagen» auf 5153 (`werkzeuge/duofix_auftraege.ps1`, Protokoll `%USERPROFILE%\HLKS_CAD_build\grundausstattung\duofix_auftraege.log`):
  WC, Waschtisch, Badewanne, Dusche (Geberit Duofix), Spültisch (JRG Sanipex MT Wanddosen). Einstieg UL-90 (Duofix-Übersicht DAS 157935),
  UL-91 (111.300.00.5), UL-92 (111.430.00.2), UL-93 (111.780.00.2). Nach Stichprobe freigeben, in die Bürobibliothek, in Test_praxis laden.
- Leitungen aus der Bürobibliothek: Trinkwasser KW/WW (R5 JRG Sanipex MT bzw. R2 Mapress Edelstahl), Abwasser SW (R6 Silent-db20 bzw. R7 Geberit PE).

## Ablauf

1. Familien prüfen, freigeben, Bibliothek; in Test_praxis laden.
2. Anlage A-SAN, Systeme KW, WW, SW; Stränge.
3. Apparate an den IFC-Lagen platzieren (Duofix an der Wand).
4. Strang und Anschlussleitungen mit `leitung_zeichnen` (Gefälle SW), Verbindungen; Prüfbefunde abarbeiten.
5. Module durchspielen: 3 (Leitungen, Schema `schema_anlegen`), 3b (`kollision_pruefen`, `aussparungen_vorschlagen`), 4A (`sanitaer_zusammenstellung`),
   7A (Planblatt + `plan_pdf`), 7B (`ifc_export`, Listen); Ergebnis je Modul als Abnahmeliste für Christian.

## Ergebnis Nachtlauf 30./31.10.2026 – Abnahmeliste

Christian 30.10.2026 23:00: «arbeite bis morgen 08:00 weiter». Modell in Test_praxis (W:) über Server 5177 (Hauptlauf als KI,
Begründung «Rang 5»). Ausgaben lokal in `%USERPROFILE%\HLKS_CAD_build\rang5_abnahme` (nicht im Repository: echtes Projekt).

### Modell

- Familien (KI-Aufträge «Familie aus Unterlagen» auf 5153, alle **Entwurf**): Geberit Duofix WC 111.300.00.5 (FAM-17),
  Waschtisch 111.426/492/498.00.2 (FAM-18), Badewanne und Dusche 111.764/744.00.2 (FAM-19, FAM-20), Spültisch mit JRG-Wanddosen
  4610.xxx (FAM-21). Dazu aus der Bürobibliothek Sanipex MT, Mapress Edelstahl, Silent-db20, Geberit PE mit Formteilen.
- Platziert an den Lagen der Architekten-IFC (Einfügepunkt + Objekt-x-Achse, Duofix 50 mm hinter der Vorwandbeplankung):
  Bad R-35: WC-1, WT-2, BW-1; Bad R-34: WC-2, WT-1, DU-1; Küche: SP-1 (Wanddosen 500 mm über OKFB, Achsabstand 120 mm).
- A-SAN, Systeme KW/WW/SW; Stränge Bad KW-S1/WW-S1/SW-F1 (Installationszone W-157/W-182), Küche KW-S2/WW-S2/SW-F2.
  SW: Fallstrang Silent-db20 d110, Sammelleitung in Bodenebene 1.5 %, Anschlüsse PE (WC d90, übrige d50); KW/WW Sanipex MT
  DN 25 bis 4250/4330 über die Apparate, Anschlüsse DN 15 (WC DN 12).

### Annahmen des Hauptlaufs (bitte bestätigen oder ersetzen)

- [ ] DN 15 aus Gewinde R 1/2 an allen Duofix-kw/ww; WC-Spülkasten mit PEX 16 (DN 12) angeschlossen.
- [ ] Waschtisch-Ablauf d 50; Wannen- und Duschablauf d 50 als Exemplarlage (`sw_x/sw_y/sw_z`), Lage geschätzt.
- [ ] Spültisch: Dosenhöhe 500 mm, Achsabstand 120 mm, Ablauf d 50 150 mm unter den Dosen.
- [ ] Stranggrössen (SW d110 bzw. d75, KW/WW DN 25 bzw. DN 12) bis zur Bemessung (4B fehlt: SN 592000, SVGW W3).
- [ ] Duofix-Elemente: Konflikte H_max (Badewanne/Dusche 112 gegenüber 130 cm) und offene Punkte der KI-Berichte – erst danach freigeben.

### Je Modul

- **2 Familien** – [ ] Duofix-/JRG-Entwürfe prüfen und freigeben (nur Mensch). Neu in der Nacht: Körper `uebergang` und `netz`,
  `familie_netz_uebernehmen` (fehlende Familien aus IFC-Geometrie; am IFC geprüft: WC 565 × 380, Wanne 1810 × 800, Dusche
  1260 × 800, Drehung −42.7° erkannt), Masslinien in den Familienansichten (skaliert mit der Familiengrösse).
- **3 Leitungen/Schema** – Silent-db20 hat keine 88.5°-Abzweige d110/110 und d110/90 → neu `leitung.regeln.mischbar`
  (Christian: «geberit silent und pe können gemischt werden»), in Test_praxis Silent ↔ PE gesetzt; Materialliste zeigt die
  PE-T-Stücke im Silent-Strang. Schemas «Wasser» und «Abwasser» angelegt (PB-2). [ ] **Entscheid:** Steigstränge, die
  unter dem 1.OG keinen Knoten haben (Verteilung im UG nicht modelliert), erscheinen nur als kurzer offener Stummel im 1.OG –
  Regel B8 «offene Enden die Offenlänge neben ihrem Knoten». Soll ein offenes Strangende im Geschoss seiner Lage gezeichnet
  werden? [ ] Beschriftungen teils übereinander. [ ] **Befund:** `leitung_abzweigen` liess zwischen T-Stück und Bogen ein
  Rohrstück von 18.7 mm zu (KW-S1-3-2-2); 3b meldet dort Kontakt der Formteile (TS-10/BO-10, TS-8/BO-18, TS-9/BO-19) –
  Mindestlänge zwischen Formteilen (Einstecktiefe/Pressbacke) beim Abzweigen fehlt; Wert und Quelle von dir.
- **3b Kollision/Aussparungen** – Vorgaben VG-1 aus Christians Entscheiden vom 19.10.2026 (Spiel 50, Zusammenfassen 100,
  toleranz_hart 5). Lauf ganzes Projekt: 77 harte Kollisionen (19 Durchdringungen, 58 Kontakte): Decken bei den Strängen,
  Leitungen/Duofix in Wänden ohne modellierte Vorwand (W-169, W-153), **DU-1 ragt in die Aussenwand W-199/W-174** (Element
  ~150 mm Richtung Waschtisch schieben), Kontakte zwischen Formteilen an kurzen Rohrstücken. Befund Kern behoben (e66e6ca):
  Stränge mit T-Stück im Deckenbereich erhielten keine Aussparung. Jetzt 3 Vorschläge: AUS-1 Steigzone Bad in D-20 (270 × 280,
  Sammelaussparung), AUS-2 Duschablauf D-20 (150 × 150), AUS-3 Küchenstrang D-6 (330 × 430). [ ] Aussparungen freigeben
  (Mensch), danach `aussparungen_ifc`. BCF mit den Kollisionen: `rang5_kollisionen.bcf`.
- **4A Sanitär-Zusammenstellung** – 7 Apparate, ΣLU KW 11 / WW 9, ΣDU 7.2, Spitzendurchfluss 0.59 l/s (Kurve grösster LU 3);
  Strang KW-S1 9 LU (6 Apparate), KW-S2 2 LU. [ ] Abflusskennzahl K fehlt (Projektvorgabe bzw. SN 592000).
- **7A Pläne** – PB-1 S03 «Sanitär 1.OG» A1 1:50 (Grundriss gedreht nach Planwinkel), PB-2 Strangschemas A2; PDFs in
  `rang5_abnahme`. [ ] Projektinformation (Projektname, Projektnummer) fehlt im Plankopf; [ ] Schema Wasser auf PB-2 überlappt
  den Plankopf (Lage vom Hauptlauf gesetzt).
- **7B Ausgaben** – IFC4-Export: Kontrolle bestanden, Schema fehlerfrei, IDS bestanden (`rang5_sanitaer.ifc`); Materialliste
  (Leitungslängen je Serie/Grösse, Formteile je Typ und Winkel).
