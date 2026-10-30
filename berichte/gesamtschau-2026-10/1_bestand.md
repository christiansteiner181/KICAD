# Gesamtschau Phase 0, Teil 1: Bestandsaufnahme je Modul

Stand 30.10.2026 · erstellt als reine Lesearbeit (kein Code geändert, keine Tests und Server gestartet, nichts committet).
`geprueft` = 7edee1a (27.10.2026 09:31); `main` = f39130d (zwei Commits dahinter, zuletzt nur `entwurf/vorhaben-erzeugendes-cad.md`).

## Methode und Grenzen

- Quellen: `entwurf/modul-*.md` («Fertig, wenn …»), `berichte/*testbericht*.md`, `modul-3c-sicherheitspruefung.md`, `modul-4a-pruefbericht.md`,
  `nachtbericht-2026-10-20/21.md`, `zuteilung.md` (gezielt), `nachbearbeitung.md`, `vorrat_freitag.md`, `berichte/anleitung/inventar*.json|md`,
  Wissensdatei `.claude/memory/hlks_cad.md` (Zeilen als `hlks_cad.md:n`), Code und Git (nur lesend).
- **Tests sind gezählt, nicht ausgeführt.** Gezählt werden Testmethoden: `[Fact]`/`[Theory]` (eine Theory zählt einmal, die ausgeführte Zahl ist höher)
  bzw. `test(`/`it(` am Zeilenanfang. Zuordnung zu Modulen nur nach Dateinamen, also ungefähr. Browser-Reihen sind nur als Reihen gezählt (42 Dateien `*.browser.mjs`).
  Letzte vollständig dokumentierte Gesamtläufe: Kern 4079, Mcp 65, Web 507, Node 902 (`zuteilung.md:192`, 20.10.2026); danach keine Gesamtzahl gefunden.
  Heutige Methodenzahl: Kern 2772, Mcp 82, Web 513, Node 1376 (zusammen 4743).
- «Lücke zu Fertig, wenn» = ein Punkt der Spezifikation, der laut Quelle nicht belegt erfüllt ist. «Feilpunkt» = offener Rest, der «Fertig, wenn» nicht berührt.
- Nicht geprüft: ob offene Punkte nach dem Datum ihrer Quelle erledigt wurden, wo kein späterer Beleg gefunden wurde (dann «Stand unbelegt»).
  Der Inhalt des Artifacts «Kontrollliste» ist von hier aus nicht lesbar (siehe Abschnitt Kontrollliste).

## Übersichtstabelle

Tests = Methoden (K Kern, W Web, M Mcp, N Node). «Attrappe/Beispiel» = Anteil, der nur gegen `ClaudeAttrappe`, `AdbAttrappe` bzw. nur am Beispielprojekt «MFH Musterweg» läuft.
F = Lücken zu «Fertig, wenn», P = Feilpunkte.

| Modul | gebaut | Abnahme | Tests (Anzahl) | davon nur Attrappe/Beispiel | Lücken F / P |
| --- | --- | --- | --- | --- | --- |
| 0a Kern-API, Ablage | ja | abgenommen 17.10.2026 | 374 (K 348, M 26) | KI-Test 10/10 echt (Sonnet, Haiku); sonst Beispielprojekt | 0 / 5 |
| 0b Gerüst, Explorer, live | ja | abgenommen 17.10.2026 | Oberfläche gesamt 525 (W 202, N 290, K 33), 7 Browser-Reihen | NAS-Test echt, zweiter PC nicht | 0 / 4 |
| 1a Grundriss, 2D | ja | abgenommen 17.10.2026 | 371 (K 220, N 151), 2 Reihen | alles Beispielprojekt | 0 / 8 |
| 1b IFC, Grundlagen | ja | abgenommen 18.10.2026 (mit Befund Bernstrasse) | 314 (K 292, M 3, N 19) | 36 K-Methoden «Echt» (lokale Dateien, überspringen sich sonst) | 0 / 7 |
| 1c Hülle | ja | abgenommen 18.10.2026, Kennwertquellen und Prüfliste offen | 220 (K 155, M 3, N 62), 2 Reihen | Kennwerte nur Testwerte | 1 / 6 |
| 1d HKAP | ja | abgenommen 18.10.2026; HKAP-Änderungen nicht ausgeliefert | 85 (K 43, N 42), 1 Reihe | nur HKAP-Testkopie 5098 | 0 / 5 |
| 2 Familien | ja | abgenommen 18.10.2026 | 473 (K 361, W 23, M 3, N 86), 3 Reihen | 3 W-Methoden gegen ClaudeAttrappe; 3 KI-Läufe echt | 1 / 6 |
| 2 Grundausstattung (10 Serien) | als Entwürfe | nicht freigegeben (nur Christian) | 132 (K 70, W 1, N 61), 3 Reihen | Arbeitsprojekt 5153, nicht Bürobibliothek | 4 / 5 |
| 3 Leitungen, Schema | ja | unter Vorbehalt 19.10.2026; Endfreigabe nicht protokolliert | 591 (K 497, M 12, N 82), 2 Reihen | Abnahmeprojekt synthetisch | 0 / 7 |
| 3b Kollision, Aussparung | ja | nur gebaut (Abnahme offen) | 467 (K 377, M 7, N 83), 2 Reihen | 7 K-Methoden «Echt»; Vorgaben fiktiv | 2 / 5 |
| 3c KI-Palette | ja | nur gebaut; Abnahmelauf mit echtem Claude bestanden 20.10.2026 | 192 (K 16, W 114, M 17, N 45), 1 Reihe | ca. 117 W-Methoden + Palette-Reihe gegen ClaudeAttrappe | 2 / 4 |
| 4 Teil A Sanitär (Belastungswerte) | ja | Prüfung «abnahmefähig mit Auflagen», Auflagen erledigt; Abnahme offen | 94 (K 60, M 3, N 31), 1 Reihe | Oberfläche nur Beispielprojekt | 1 / 4 |
| 7 Teil A Planblatt | ja | nur gebaut (Abnahme offen) | 92 (K 62, M 6, N 24), 2 Reihen | alles Beispielprojekt | 0 / 5 |
| 7 Teil B (B1 Boxen, B2 IFC-Export, B3 Umbruch) | ja | nur gebaut, kein Testbericht | 119 (K 90, W 4, M 2, N 23), 1 Reihe | Beispielprojekt; Echt-Rundreise unbelegt | 3 / 3 |
| Feilarbeiten N6–N22 | ja | von Christian am 21.10. teils geprüft, keine förmliche Abnahme | 251 (K 131, W 4, N 116), 8 Reihen | 6 K-Methoden «Echt» | 0 / 8 |
| G Gesten | Stufe 1 | Abnahme nicht abgeschlossen (26.10.2026) | 105 (W 16, N 89), 1 Reihe (36) | Testkamera/Punktfolgen, keine echte Hand | 3 / 4 |
| 5 Heizung | Datenmodell A-D auf Zweig | pausiert, ungeprüft | 36 K-Methoden nur auf dem Zweig | Beispielprojekt | – |
| XR-Reste in KICAD | Browser-VR, Endpunkte | nicht Teil dieser Erfassung | 325 (K 36, W 117, N 172), 6 Reihen | 74 W + 28 N gegen AdbAttrappe | – |

Summen der Zuordnung: 4730 von 4745 Methoden (ohne Zuordnung: `KaelteHeizungTests` 15).

---

## 0a Datenmodell und Kern-API (abgenommen 17.10.2026)

Fertig, wenn: KI ohne Vorwissen beantwortet Fragen und erzeugt Raum- und Strangliste. Erfüllt: Sonnet 5.5 und Haiku 5.5 je 10/10 (`modul-0a-testbericht.md:5-10`).
Keine Lücke zu «Fertig, wenn». Feilpunkte (`modul-0a-testbericht.md:98-110`):
1. BKP-Zuordnung im Katalog vorläufig, von Christian zu prüfen.
2. MCP kann Textfelder nicht leeren (Parameter «entfernen» nachzurüsten; Stand unbelegt, 3b-Vorgaben kennen «entfernen»).
3. P08 nennt Id statt Kennzeichen; MCP schreibt beim Beenden kein Standabbild (Stand unbelegt).
4. Ablage nur gegen lokalen Ordner getestet; Mac (Unicode «ä», SMB) und synchrone Uhren ungeprüft.
5. Zahlen im Bericht überholt: 50 Klassen/26 Werkzeuge/P01–P11 gegen heute 77 Klassen, Prüfungen bis P32+P45 (`Pruefungen.cs`; `inventar-notizen.md` Abschnitt 4.9).

## 0b Gerüst, Projektexplorer, Live-Abgleich (abgenommen 17.10.2026)

Beide Kriterien erfüllt, NAS-Test bestanden (0,31–0,36 s; `modul-0b-testbericht.md:5-17,32-50`). Keine Lücke zu «Fertig, wenn». Feilpunkte (`:97-113`):
1. Zweiter echter PC nie geprüft; der Test lief als zweiter Prozess auf demselben Rechner (Testordner auf W: bleibt dafür stehen).
2. Mac ungeprüft.
3. Skalierung ungelöst: Statusabfrage alle 3 s, Verbindungen und Prüfsumme über das ganze Modell, Ordner `aenderungen` wächst unbegrenzt.
4. Schwellen (Standabbild ab 200 Paketen, Wachhund 10 s, Takt 2 s) gewählt, nicht gemessen begründet.

Oberfläche allgemein (Start, Menüband, Paletten W1/W2, Hilfe F1, Projektbrowser-Gliederung, Symbole Revit): 6 Browser-Reihen, 290 Node-, 202 Web-Methoden. Die Wissensdatei nennt «Pop-ups im Desktop-Start `--app` ungeprüft»,
«Reiter in Palettengruppe nicht umsortierbar» (`hlks_cad.md:223-224`; Stand unbelegt). Diese Funktionen sind der einzige von Christian geprüfte Teil (siehe Kontrollliste).

## 1a Grundriss und 2D (abgenommen 17.10.2026)

Fertig, wenn (Wände, Türen, Fenster gezeichnet und im 3D richtig; Plan mit Bemassung gedruckt): erfüllt, Browser 72/72 (`modul-1a-testbericht.md:5-17`). Keine Lücke zu «Fertig, wenn». Feilpunkte (`:78-103`):
1. Planblatt: automatische Lage setzt nur die erste Ansicht; zweite Ansicht oft ohne Platz.
2. Schnitt: keine Durchdringungslinien; Leichtbau ohne Schraffur; Nachbarsuche quadratisch.
3. Grundriss: Bauteile oberhalb der Schnitthöhe fehlen als gestrichelte Darstellung (für Leitungen am 19.10. gelöst, `modul-3-testbericht.md:~190`; für Architektur unbelegt).
4. Ecken ziehen die Nachbarwand beim Verschieben nicht mit; keine Vorschau beim Raum-Werkzeug.
5. HLKS-Geräte ohne Masse als Würfel (Familien seit Modul 2; Stand unbelegt).
6. Kein Kürzel für Füllbereich, Nordpfeil, Schnitt (`inventar-notizen.md` 4.10).
7. Sieben Annahmen der Prüfliste (`:78-87`, u. a. Schnittfläche grau 25 %, Schraffuren Porenbeton/Erdreich/Kies); Bestätigung unbelegt.
8. PDF-Betrachter des Browsers ignoriert «ganze Seite».

## 1b Grundlagen, Räume, openBIM, IFC-Übertrag (abgenommen 18.10.2026)

Sieben Kriterien erfüllt mit echten Dateien (Bernstrasse, BKW; `modul-1b-testbericht.md:5-19`). Kriterium 1 zeigte das H+K-IFC 30 m neben dem Archicad-IFC; geklärt am 24.10.2026 (Drehung −42.72°),
gebaut als «IFC einpassen» (`entwurf/ifc-einpassen.md`, Commit 8a80f7f) und an Test_praxis geprüft (`zuteilung.md:410`: 99 % Überdeckung, WC/WT im Mittel 15 mm an Wandfläche). Keine Lücke zu «Fertig, wenn». Feilpunkte:
1. **N1 «IFC über zwei Punktpaare» steht in `nachbearbeitung.md:10` und `luecken.json` noch als «zurückgestellt/nicht gebaut»**, obwohl «einpassen» den Fall löst; Eintrag nicht nachgeführt, Entscheid ob N1 entfällt fehlt.
2. «Einpassen»: grosse Modelle (BKW) ungeprüft; Anleitungsbild `m1b-ifc-einpassen` fehlte (`hlks_cad.md:357`).
3. Boolesche Körper (Wand minus Öffnung) in der Netzdatei vereinfacht (`:159`); Fassaden als IfcWall ohne Achse werden keine KICAD-Wand (`:160`).
4. Nicht übernommene Klassen (Schalldämpfer, VAV-Box, Brandschutzklappe, Luftauslass) ohne Katalogklasse, «kommen mit Modul 2» (`:162`; Stand unbelegt).
5. Leistung: BKW im 3D ~0.6 Bilder/s, Server 1–2 s je Abfrage (`:166-168`); IFC bis 2 GB nur im Arbeitsspeicher (~5-fach).
6. Acht Annahmen der Prüfliste (`:144-155`); Bestätigung unbelegt.
7. Phase-2-relevant: Bernstrasse-Räume stammen teils aus Wänden abgeleitet (49) und teils aus dem H+K-IFC (37); Zusammenführung seit «einpassen» möglich, nicht dokumentiert.

## 1c Gebäudehülle (abgenommen 18.10.2026, Kennwertquellen und Prüfliste offen)

- **F1:** Kennwerte kommen für echte Projekte aus keiner Quelle. `kataloge/materialien.json` enthält 0 Materialien; es fehlen λ für Beton/Mauerwerk/Putz/Holz/Estrich (SIA 2001 bzw. SN EN ISO 10456), Rsi/Rse in aktueller Ausgabe,
  Fensterkennwerte, Wärmebrückenkatalog (`modul-1c-testbericht.md:23-35`). Kriterien 1 und 4 («jeder Kennwert mit Herkunft und Quelle») sind nur mit Testwerten erfüllt (U 0.226 «Testwerte», `:15`).
Feilpunkte:
2. BKW-IFC liefert Wand-U-Werte bis 13.075 W/(m²·K), vermutlich Exportfehler; wird wie geliefert übernommen (`:37-41`).
3. Nur flache Dächer; 20 BKW-Wände unter geneigten Dächern doppelt gefüllt (`:162-163`).
4. Darstellung «Hülle» nur am Bildschirm, nicht im Plan-PDF.
5. Versetzter BKW-Gebäudeteil: ~1550 m² Decke ohne Platte (`:168-171`); `raum_erkennen` bildet keine Inselräume.
6. Quellenpflicht an drei Stellen (P12, Nachführung, Oberfläche), Vorschlag C1a vorgemerkt (`:175-176`).
7. U·A ohne Normfaktoren (kommt mit Heizlast, Phase 3).

## 1d HKAP-Universalschnittstelle (abgenommen 18.10.2026)

Alle Kriterien gegen die HKAP-Testkopie (5098) erfüllt, 25/25 Vertragsprüfungen, Kern 42/42, Browser 85 (`modul-1d-testbericht.md:6-42`). Keine Lücke zu «Fertig, wenn». Feilpunkte:
1. HKAP-Änderungen (`/api/austausch`, Versionsprüfung der Projektierungen) **nicht ausgeliefert**, Freigabe Christian (`:74`); gilt auch für die Datenart «straenge» (Modul 3).
2. Neue Geschosse aus HKAP bekommen 3000 mm als Platzhalter (`:65`).
3. Datenbefund Livedaten: HKAP P-005 hat PLZ und Ort vertauscht (`:54`); Korrektur in HKAP offen (Stand unbelegt).
4. Der Dialog «Mit HKAP verknüpfen» schlägt `localhost:5013` (HKAP im Betrieb) vor, die Spezifikation verlangt Tests nur an der Testkopie (`inventar-notizen.md` 4.11).
5. Die HKAP-Browser-Reihe braucht die Testkopie; letzter dokumentierter Lauf 83/83 am 19.10.2026 (`zuteilung.md:97`), danach «seit C1 nicht gelaufen» (`modul-3b-testbericht.md:195`); Stand unbelegt.

## 2 Familien und Familie aus Unterlagen (abgenommen 18.10.2026)

Fünf Kriterien erfüllt, Massvergleich 90/90, 24/24, 290/290; Konflikte im Abnahmeprojekt bewusst offen (`modul-2-testbericht.md:5-30`, `hlks_cad.md:127`).
- **F1:** Kriterium 2 verlangt einen gemischten Satz «PDF + STEP oder DWG/DXF». Erreicht wurde PDF + CSV bzw. PDF + Herstellerkatalog-PDF; eine echte Hersteller-CAD-Datei war nicht erhältlich, der STEP-/DXF-Weg ist nur mit Testdateien geprüft (`:15,126-127`).
Feilpunkte (`:124-133`):
2. RVP-P: Flanschtiefe ohne Quelle; FläktGroup: `volumenstromMin/Max` leer.
3. Webseiten, die per Skript laden, kann der HTML-Leser nicht lesen.
4. Prompt-Injection über Webinhalte technisch nicht erzwingbar ausgeschlossen (nur Schalter «ohne Internet»; Internet-Schalter seit 20.10. frei, `modul-3c-sicherheitspruefung.md:224`).
5. Familien FAM-1…16 (Abnahme-Serien) warten auf Prüfung/Freigabe durch Christian (`modul-3-testbericht.md:207`).
6. Mitgelieferte Familienbibliothek `kataloge/familien/` existiert nicht (`inventar-notizen.md` 3.9); die Bürobibliothek auf W: ist leer (Entscheid «keine Erstbefüllung», `hlks_cad.md:298`).

## 2 Grundausstattung Rohr- und Kanalserien (R1–R7, K1–K3)

Alle zehn Serien am 20.10.2026 als Entwürfe gebaut und stichprobengeprüft (`berichte/grundausstattung-serien.md:1-28`). «Fertig, wenn»: alle zehn **freigegeben in der Bürobibliothek** (`entwurf/grundausstattung-leitungsserien.md:55`). Nicht erfüllt:
- **F1:** keine einzige Serie freigegeben oder in die Bürobibliothek übernommen (nur Christian; Stand `nachtbericht-2026-10-21.md:36-44`).
- **F2:** K3 Flexrohr AR: Konflikt v_max 25 (Preisbuch) gegen 30 m/s (Broschüre 2025); Entscheid «Broschüre gilt» (`hlks_cad.md:569`), der Klick in der Oberfläche fehlt (`nachtbericht-2026-10-21.md:38`).
- **F3:** R1 Bogen: 11 Konflikte alte/neue Geberit-Masse; Entscheid «neue Masse», Freigabe offen (`:41`).
- **F4:** R7 d32 ohne Innendurchmesser/Wandstärke, nicht zeichenbar (`:43`). Das «Fertig, wenn» der Abwasser-Serie nennt ausdrücklich «ein Rohr d32» (`entwurf/abwasser-serie.md:51`). Entscheid (Unterlage oder innen/wand je Typ optional) offen (`:74`).
Feilpunkte:
5. FAM-21/22/23: Arbeitsprojekt 5153 auf Leser 3 neu bauen und `familien_quellen_nachfuehren` (`vorrat_freitag.md:24`, `nachbearbeitung.md:123-124`).
6. Schwache Quellen mit Vermerk: R6 DIBt 2019, R7 AU-Katalog, R2 T-Stück PL, K1 Lindab-Bögen (`grundausstattung-serien.md:~139`).
7. K2: freie Grösse 145…2500 mm gebaut (Kern 20687bf); Stand der reduzierten T-Stücke und Reduktionen je Grössenpaar unbelegt.
8. Flexrohr: Biegeradius fehlt; Rechteckkanal Hochkant-Typen auf Modul 6 zurückgestellt (`modul-3-testbericht.md:~195`).
9. Abzweig-Werkzeug: Serienwinkel (45°/88.5°) im Kern (`leitung_abzweigen`), Oberfläche fehlt (`nachtbericht-2026-10-21.md:26-28`; `luecken.json` Nr. 12); Stand seit 21.10. unbelegt.

## 3 Leitungen und Schema (abgenommen unter Vorbehalt, 19.10.2026)

Vier Kriterien erfüllt (119 Referenzfälle von Hand, 0 Befunde P13–P18, HKAP «straenge» 20/20; `modul-3-testbericht.md:9-22`). Vorbehalt: Nachträge B8 (Strangschema nach Beispiel), B9 (Beschriftung), Korrekturen 27/30 (`:3`, `hlks_cad.md:268-274`).
Alle Vorbehaltspunkte sind laut `zuteilung.md`/`hlks_cad.md:275-303` gebaut (B8 «passt», B8b–d, B9b, 27 und 30 erledigt). **Die «endgültige Freigabe nach Vorführung» ist nirgends protokolliert**; der Nachtbericht vom 20.10. führt sie als offen
(`nachtbericht-2026-10-20.md:54`). Keine Lücke zu «Fertig, wenn». Offene Punkte (`modul-3-testbericht.md:199-210`):
1. HKAP-Auslieferung «straenge» (zusammen mit 1d), Christian.
2. Schema auf A2: Titel und Geschossnamen abgeschnitten, Raumbeschriftung von Leitungen überdeckt (Stand unbelegt).
3. Offene Teile: `familie_aendern` ohne `schemasymbol_setzen`, Linien im Schema höchstens ein Knick, Laufzeit auf grossen Modellen, schräges T am Rand eines Zugs, `familie_platzieren` ohne Punkt (`entwurf/modul-3.md`).
4. Familien FAM-1…16 prüfen und freigeben (Christian); einzelne Reduktionspaare fehlen, Silent-db20-Reduktionen nur für d110.
5. Prinzipschema bewusst auf Modul 5/6 verschoben (`luecken.json` Nr. 4).
6. Strang von Hand einem HKAP-Strang zuordnen: kein Dialog, nur über die KI (`luecken.json` Nr. 5).
7. Zeitgrenzen-Tests lastabhängig, Messung auf ruhiger Maschine fehlt (`:215`).

## 3b Kollision und Aussparungen (nur gebaut, Abnahme offen)

Kollisionsliste Bernstrasse (2318–2964 harte Kollisionen, 100–129 Aussparungen, IFC4/IFC2X3, BCF 3.0) mit Sprung, Status über zwei Läufe, MCP: belegt (`modul-3b-testbericht.md:24-43`). Lücken zu «Fertig, wenn»:
- **F1:** «Eine neue Architekten-Fassung setzt den Status»: nur durch Kern-Tests belegt, keine echte zweite Fassung mit Durchbrüchen (`:36,188`).
- **F2:** Projektvorgaben (Spiel, Abstände, Zusammenfassen, `toleranz_hart`) tragen «ohne Wert bleibt die Prüfung unvollständig» (`:143-155`); die Abnahmeläufe benutzten fiktive Werte, bzw. Werte von Christian vom 19.10.2026 ohne dokumentierte Quelle («Bürostandard H+K 2026» ist nur ein Beispiel). Quelle unbelegt.
Feilpunkte (`:184-196`): BCF 2.1 lesen, Titel/Prioritäten/Zuweisungen, Koordinatenvorgabe; `Pset_ProvisionForVoid` und Geschoss-GlobalId; ~3000 Kollisionsmarken im 3D (Leistung);
Zeitgrenzen-Tests der Echt-Gruppe lastabhängig; Spezifikation↔Code-Abweichungen (Kernbohrungs-Grenze entfallen, Registerknöpfe 12 statt 10/11; `inventar-notizen.md` 4.1-4.2).

## 3c KI-Palette «Claude» (nur gebaut; Abnahmelauf echt bestanden)

Palette, Änderungen mit «Zurücknehmen», Normfrage, Verlauf nach Neustart: belegt mit Attrappe, **Abnahmelauf mit echtem Claude Code 2.1.299 am 20.10.2026 15:17–15:19 (Haiku)** und Bedienung im Browser mit echtem Claude (Sonnet) um 02:37 (`modul-3c-testbericht.md:63-79,159-168`, `modul-3c-sicherheitspruefung.md:206`).
Sicherheitsprüfung (hlks-pruefer): «mit Auflagen», B1 kritisch (CSRF) und B2–B15 erledigt (`modul-3c-sicherheitspruefung.md:195-222`). Die 3c-Annahmen A1–A4 sind bestätigt (`zuteilung.md:195`), die Abnahme steht aus. Lücken zu «Fertig, wenn»:
- **F1:** «Normfrage mit Zitat aus dem Normenverzeichnis»: Volltext nur für 62 von 315 Normen, SN 592000/SN EN 12056/SVGW W3 fehlen (`modul-3c-testbericht.md:131`).
- **F2:** «Gespräch läuft nach Neustart weiter»: auf einem zweiten Rechner (`--resume` schlägt fehl, neue Sitzung mit Verlauf) nur mit Attrappe belegt (`:133`).
Feilpunkte: mit echtem Claude nicht geprüft sind Internet-Lauf aus der Palette, Bild der Ansicht, Opus/Haiku-Wahl, Abbrechen, Zeitlimit (`:130`); «Rechner ohne Abo» nur Attrappe (`:134`);
Sitzungsablage von Claude Code (`%USERPROFILE%\.claude\projects\…`) kann Normtexte enthalten (`sicherheitspruefung.md:202`, «Offen»); Spezifikation weicht in Internet-Regel, Verbotsliste (23 Werkzeuge) und Knopf «Claude» vom Code ab (`inventar-notizen.md` 4.13-4.15).

## 4 Sanitär rechnen, Teil A Belastungswerte (Prüfung bestanden, Abnahme offen)

Unabhängige Prüfung (`modul-4a-pruefbericht.md`): alle 34 Excel-Zeilen, Formeln und Referenz Walkringen stimmen; «abnahmefähig mit Auflagen». Auflagen B1–B7 erledigt (`:265-300`), Kurvenwahl `bestaetigt: true` (`kataloge/sanitaer_belastungswerte.json:57`). Teil B (Dimensionierung) wartet auf SN 592000, SN EN 12056, SVGW W3 (Phase 3).
- **F1:** «Referenz Walkringen auf die letzte Stelle» ist nur im Kern-Test belegt; in der Oberfläche nicht nachgebaut (`modul-4a-testbericht.md:111-113`, Schritt 10: ΣKW 80, ΣWW 52, ΣDU 38.8, Total LU 132).
Feilpunkte: Abflusskennzahl K ohne Quellenfeld und ohne Auswahlliste nach Gebäudeart (bis SN 592000); zwei Tests aus dem Prüfbericht offen (zwei Anschlüsse desselben Mediums an zwei Leitungen; Apparat an zwei Anlagen, `modul-4a-pruefbericht.md:~290`);
Oberfläche hat «Walkringen: 0.5» und Kurven «grösster LU 3/5» fest im Text (`sanitaer-text.js`); Normenverzeichnis Volltext 62/315.

## 7 Teil A Planblatt wie H+K (nur gebaut)

Neun Kriterien als erfüllt belegt, Kopf gegen H03 verglichen (`modul-7a-testbericht.md:25-50`). Keine Lücke zu «Fertig, wenn». Feilpunkte (`:64-76,110-119`):
1. Revisionsstempel liegt schräg über «Erstelldatum:»/«Rev. Index:» (Gestaltungsfrage, Christian am echten Plan).
2. Feldbeschriftungen im Schema-Kopf ohne Doppelpunkt, im Grundriss-Kopf mit.
3. «Erstelldatum» zeigt das Abrufdatum, solange `datum` leer ist.
4. Plannummern-Schema (Annahme 5) nur an Bernstrasse als Vorlage geprüft; ein zweites Objekt fehlt. Schriftbild im Papierausdruck nicht geprüft.
5. Revisionstabelle in der Palette schmal; Listen auf A3 klein (Arial 2.2 mm) und mit «…» gekürzt (Umbruch seit B3 gelöst).

## 7 Teil B: B1 Plandarstellung, B2 IFC-Export mit IDS, B3 Listenumbruch (nur gebaut)

Gebaut am 20.10.2026 (`zuteilung.md:205-206,311`; Rundreise 131 Elemente/142 Verbindungen, eigener IDS-Prüfer, Dialog «IFC exportieren»). **Es gibt keinen Testbericht und keine Abnahme.** Lücken zu «Fertig, wenn»:
- **F1:** B2 «Datei öffnet in einem freien Viewer» (Revit/Solibri, Christian prüft auf Wunsch, `entwurf/modul-7.md:116-118`): kein Beleg.
- **F2:** B2-Leistung «Bernstrasse-Grösse < 30 s»: der Test verfehlt die Grenze unter Last (65–70 s alter wie neuer Stand), ruhige Messung fehlt (`nachtbericht-2026-10-21.md:61`; Test `IfcExportRundreiseTests.cs:153-189`).
- **F3:** Kein Bericht/Nachweis zu B1 (Aussparungsboxen, Koordinationsplan, Schnittmarken mit Blattverweis) und B3 am Bernstrasse-Projekt; geprüft nur im Beispielprojekt.
Feilpunkte: BKP-Klassifikation wird mit «ohne Angabe» als Ausgabe geschrieben, die BKP-Ausgabe steht nicht in `ifc.json` (`IfcExport.cs:66-67`); Architektur-Unterlage DWG/PDF im Plan (7A-Offen Nr. 1) Stand unbelegt;
IDS-Prüfer ist ein Eigenbau, weil Xbim.IDS.Validator AGPL ist (Entscheid Christian 21.10., `hlks_cad.md:569`).

## Feilarbeiten N1–N26 und weitere (`nachbearbeitung.md`, `vorrat_freitag.md`)

`nachbearbeitung.md` und `vorrat_freitag.md` sind seit 21.10.2026 nicht mehr nachgeführt (letzter Commit auf beide: 21.10./06d2190). «Freitag 23.10. 17:00» (Priorisierung des Vorrats) ist nirgends als erledigt belegt.

| Nr | Thema | Stand laut Quelle | Offen (Quelle) |
| --- | --- | --- | --- |
| N1 | IFC über Punktpaare ausrichten | zurückgestellt (`nachbearbeitung.md:10`) | faktisch abgelöst durch «IFC einpassen» (24.10.), Eintrag veraltet |
| N2 | Paletten verschiebbar | erledigt 19.10. (W1/W2) | Pop-ups im `--app`-Start ungeprüft (`hlks_cad.md:223`) |
| N3 | Fehlende Bedienorte | erledigt 20.10. (4 Teile) | – |
| N4 | PDF-Leser Dezimalzahlen | Leserteil erledigt 20.10. (Leser 2, 3) | Familien R1/R2/R7 mit Quelle neu belegen (`:123-124`) |
| N5 | KI: abgelehnte Aufrufe als «ok» | erledigt 20.10. | – |
| N6 | Türhöhe Archicad-IFC | umgesetzt 21.10. | IFC in Test_praxis neu übernehmen (`:168`); Stand unbelegt |
| N7 | Unterzüge ausserhalb | umgesetzt 21.10. (Leserfehler) | IFC in Test_praxis neu übernehmen (`:191`) |
| N8 | Schraffur dreht mit Wand | umgesetzt 21.10. | – |
| N9 | Ausrichtung Geometrie/Himmelsrichtung | umgesetzt 21.10. | längste Kante kann wegen ±45° senkrecht stehen (`:272`) |
| N10 | Mittlere Taste verschiebt | umgesetzt 21.10. | Planblatt: PDF-Betrachter statt pdf.js (Entscheid Christian offen, `:307-309`, `vorrat:14`) |
| N11 | Navigationswürfel | umgesetzt 21.10. | VORNE: Vorzeichen von `planwinkel` prüfen (`:312-313`) |
| N12 | IFC Original/Nachbau/beide | umgesetzt 21.10. | Schnitt nur Schnittlinien, keine Fassadenprojektion (`:347`) |
| N13 | 3D-Schnittbereich | umgesetzt 21.10. | Schnittflächen offen (kein Deckel); IFC-Clipping nicht in Browser-Reihe belegt (`:377-378`) |
| N14 | Raum im 3D zeigen | umgesetzt 21.10. | Annahmen: Raumunterkante, Isometrie (`:399`) |
| N15 | Raum begehen | umgesetzt 21.10. | Tastenbelegung und «durch Wände gehen» als Annahme, Bestätigung unbelegt (`:418-419`) |
| N16 | Stützen mit Kreuz | umgesetzt 21.10. | – |
| N17 | Treppen im Grundriss | umgesetzt 21.10. | 2 von 10 Bernstrasse-Treppen ohne Attribut um eine Steigung falsch; Schnittlinie, Kreis, Pfeil, Podestschwelle, Laufdicke 250 mm ohne Quelle (`:560-563`) |
| N18 | Holz beige | umgesetzt 21.10. | Farbton #d8bf94 und Deckkraft 0.6 sind Annahmen (`:477`) |
| N19 | Schnittbereich blau | umgesetzt 21.10. | – |
| N20 | Grob: Wände schwarz | umgesetzt 21.10. | Stützen/Unterzüge wie Wände: Annahme nach SIA 400 B.8.3.1 (`:491,569`) |
| N21 | Schnitthöhe nach SIA 400 | umgesetzt 21.10. | 1000 mm bevorzugt, Türen ohne Schwelle: Annahmen (`:569`) |
| N22 | Begehen: Wände undurchsichtig | umgesetzt 21.10. | – |
| N23–N26 | VR (Begehen, Greifen, Sprache, Marker) | nach `entwurf/modul-xr.md`, jetzt HK_XR | Teil bleibt als Browser-VR in KICAD (Abschnitt XR-Reste) |
| – | Startansicht 3D ins Modell | erledigt mit N13 (`:310`) | – |
| – | Hinweis «Öffnungen passen nicht in ihre Wand» | offen (`vorrat:15`) | wiederholt Text; BKW 358× «höher als die Wand» |
| – | Daten Grundausstattung | offen (`vorrat:21-24`) | siehe Abschnitt Grundausstattung |

Die Annahmen N15/N17/N20/N21 sollten «am Freitag bestätigt» werden (`nachbearbeitung.md:418,569`); eine Bestätigung ist nirgends dokumentiert, 3c/4A/Fach-K sind es.

## G Gesten am Arbeitsplatz (Stufe 1, Abnahme nicht abgeschlossen)

Gebaut (18 Dateien unter `wwwroot/js/gesten`, MediaPipe-Hand-Landmarker lokal), in main zusammengeführt (7edee1a, dc32c48). Christian am 26.10.: «wir beenden hier. wir machen das tuning in der labor app» (`modul-gesten-testbericht-2026-10-26.md:99-101`).
«Fertig, wenn» (`entwurf/modul-gesten.md:36-43`):
- **F1:** höchstens eine Fehlauslösung in 10 min normaler Arbeit: nicht gemessen (`:101`).
- **F2:** Bildrate 3D mit Gesten höchstens 10 % unter der ohne: nur headless mit Software-Rendering gemessen, −8,3 % (knapp), mit GPU/60 Hz nicht gemessen (`:28-57,90-92`).
- **F3:** Drehen/Rollen/Verschieben am realen Projekt zuverlässig: «Gesten brechen nach 1–2 s ab» (Ursache unbelegt), Bereich vor der eingebauten Kamera eng (`hlks_cad.md:576`). Zoom per Faust (G5) auf Entscheid Christian ausgeschaltet; das «Fertig, wenn» nennt «zoomt» weiterhin.
Feilpunkte: Händigkeit rechts/Umschaltung, Bildwinkel 60° (Annahme), Kamera-LED und fremd belegte Kamera ungeprüft (`:80-88`); die Tuning-Heimat (Labor-App L4) liegt nach U3 in HK_XR, ob dort ein PC-Gestenlabor existiert, ist unbelegt.
Tests: nur synthetische Punktfolgen (`wwwroot/js/gesten/testfolgen`) und Testkamera `--use-fake-device-for-media-stream`; keine echte Hand ausser der Abnahme am 26.10.

## 5 Heizung (pausiert, Zweig `pause/modul5-datenmodell`)

- Spezifikation Teil A vorhanden (`entwurf/modul-5.md`, 252 Zeilen, ohne Normwerte); Teil B wartet auf Normen (SIA 384/1, 384/2, SN EN 12831-1, SN EN 442, SN EN 1264, SN EN 12828; `:6-11`).
- In main: Heizsymbole (Katalog `kataloge/schemasymbole.json`, aus Christians HKAP-Bibliothek; `SchemaSymbolKatalog.cs`, Tests 14+19).
- Zweig: 3 Commits vom 21.10.2026 (861df2a, 511a26c, bb77abf; Datenmodell Heizgruppe, Befunde P33–P39, 50 Dateien, +3859 Zeilen, 36 Testmethoden), **ungeprüft, nie zusammengeführt**.
  Abstand zu main 486 Commits; `git merge-tree` meldet 21 Stellen «beidseitig geändert/Konflikt». main belegt inzwischen P31, P32, P45 (Zweig P33–P39).
- 8 Fragen an Christian vom 21.10. offen (`nachtbericht-2026-10-21.md:64-75`), u. a. Anordnung des Prinzipschemas, Plannummer, Ablageort der Normen.

## XR-Reste in KICAD (nach U3, Commit 896d6a3 vom 26.10.2026)

Nur festgehalten, was noch in KICAD liegt (nicht deren Bestand):
- **Ordner:** `src/Hlks.Labor`, `src/Hlks.Labor.Quest`, `src/Hlks.Quest`, `src/Hlks.Xr`, `tests/Hlks.Labor.Tests`, `tests/Hlks.Xr.Tests` existieren als **leere, nicht verfolgte Verzeichnisse** (0 Dateien; Reste der OneDrive-Arbeitskopie).
  `HLKS_CAD.slnx` führt sie nicht mehr. `tests/Hlks.AdbAttrappe` bleibt (nur von QuestTests gebraucht).
- **Verbindung:** `Hlks.Web.csproj:11` verweist auf `HkXr.Verbindung` (Ordner `..\HK_XR` bzw. `HLKS_HKXR`, Stand `extern/hk_xr.commit` = 5e21c8f). `KicadXr.cs`, `Program.cs:48,287` (`UseHkXrOberflaeche`, `MapHkXrVerbindung`).
- **Endpunkte/Dienste in `src/Hlks.Web`:** `QuestEndpunkte.cs`, `QuestStarter.cs` (/api/quest), `VrAuftrag.cs`, `VrSeite.cs` (/vr), `XrModellEndpunkte.cs` (GLB je Geschoss), `XrEbenenEndpunkte.cs`, `XrMarkerEndpunkte.cs`; `Herkunftsschutz.cs` schützt `/vr`.
  Im Kern: GLB-Schreiber und Problem-Marker (Tests `XrGlbTests` 18, `XrModellTests` 12, `ProblemMarkerTests` 5).
- **Oberfläche:** Browser-VR (`ansichten/vr3d.js`, `vr-logik.js`, `xr-*.js` 12 Dateien, `vr-start*.js`, `vr.html`, `vr.webmanifest`), Dialog/Aktionen (`aktionen-quest.js`, `aktionen-vr-auftrag.js`, `quest-dialog.js` …), **Register «XR»** im Menüband (`menueband.js:34`), `css/quest.css`, `css/vr-start.css`. 414 Zeilen in `OBERFLAECHE.md` erwähnen XR/Quest/Brille.
- **Werkzeuge/Kataloge/Entwürfe:** `werkzeuge/quest_verbinden.ps1`, `vr_symbol_erzeugen.mjs`, `hk_xr_stand.ps1`; Kataloge `kataloge/xr_*.json` (5) und `labor_*.json` (2), in `src`/`tests` von nichts referenziert (HK_XR-Nutzung unbelegt);
  Entwürfe `modul-xr.md` (631 Zeilen), `xr-empfaenger.md` (284), `labor-app.md` (382), `bedien-grundsatz.md` (106).
- **Tests:** 325 Methoden (K 36, W 117, N 172) und 6 Browser-Reihen (`quest`, `vr`, `vr-start`, `xr-b`, `xr-desktop`, `xr-sitzung`); QuestTests (60) und QuestAutostartTests (14) laufen gegen `AdbAttrappe`.
- **Anleitung:** `inventar.json` führt 35 XR-Einträge (`zb-xr-*` 15, `zb-quest-*` 17, `zb-n23-*` 2, `zb-problemmarker-palette`); nur die XR-Labor-Einträge wurden bei U3 entfernt. Die 17 `zb-quest-*` beschreiben die Brillen-App und gehören inhaltlich nach HK_XR.
- Browser-VR (Prototyp, «nur noch Fehlerbehebungen», `hlks_cad.md:563`) und die App «H+K XR» sind zwei Wege zur selben Funktion; ein Entscheid, den Browser-VR-Weg zu entfernen, ist nicht belegt.

---

## Kontrollliste (Artifact «Kontrollliste», Anleitung `inventar.json`)

`berichte/anleitung/inventar.json` enthält heute **358 Funktionen in 15 Kapiteln** (Zählung per Skript in `%TEMP%`, nicht im Repo). Das Artifact wurde am 21.10.2026 mit 290 Funktionen gebaut (`hlks_cad.md:566`); die Differenz (358 gegen 290) ist von hier aus nicht zu erklären (Gesten +15 am 26.10.; Rest unbelegt).
Geprüft hat Christian laut Auftrag 11 Funktionen, alle «ok» (Start, Menüband, Projektbrowser). **347 von 358 (97 %) sind ungeprüft**; auch die 11 sind nur mit der Kapitelangabe aus dem Auftrag zugeordnet, ihre Ids sind nicht lesbar (unbelegt).

| Kapitel | Modul | Funktionen | Kapitel | Modul | Funktionen |
| --- | --- | --- | --- | --- | --- |
| start (Start und Oberfläche) | – | 59 | m3 Leitungen und Schema | 3 | 28 |
| m0a Datenmodell | 0a | 7 | m3b Kollision, Aussparung | 3b | 18 |
| m0b Arbeitsplatz | 0b | 19 | m3c KI-Palette | 3c | 14 |
| m1a Grundriss, 2D | 1a | 22 | m4 Sanitär Belastungswerte | 4 | 10 |
| m1b IFC, Grundlagen | 1b | 15 | m7 Planblatt | 7A | 18 |
| m1c Hülle | 1c | 15 | mg Gesten | G | 15 |
| m1d HKAP | 1d | 10 | zusatz (Einlage, Bibliothek, Serien-PDF, Rohr, N3–N23, XR) | – | 85 |
| m2 Familien | 2 | 23 | **Summe** | | **358** |

- Kapitel `start` (59) gliedert sich: start-* 19, menueband 7, projektbrowser 6, palette 8, dialog 6, anordnung 5, kuerzel 3, eigenschaften 2, optionsleiste/statusleiste/ansichtsreiter je 1. Die 11 geprüften liegen darin; mindestens 48 Gerüstfunktionen (Dialoge, Paletten, Anordnung, Kürzel, Start-Kacheln) sind selbst im geprüften Kapitel ungeprüft.
- Kapitel `zusatz` (85): 35 XR-Einträge (siehe XR-Reste), 5 Bibliothek, 9 Serien-PDF, 7 N3, 5 N9, 3 N11, 3 N13, 3 N15, 2 N17, 3 N20/N21, 3 Einlage, 2 Rohr.
- Das Inventar kennt keine Einträge zu N6, N7, N8, N16, N18, N19, N22; sie stehen nur im Zusatzkapitel «Feilarbeiten 21.10.2026» des Artifacts (N6, N7, N8, N16; `werkzeuge/kontrollliste/bauen.mjs:30-60`).
- Veraltet: `inventar.json:stand` = 636e78c (20.10.), heute `geprueft` = 7edee1a; `luecken.json` und `inventar-notizen.md` (Stand 636e78c) führen N1 als «nicht gebaut».
- `bauen.mjs` setzt den Status von Modul 2 auf «Gebaut» (`status.m2`), obwohl 18.10.2026 abgenommen (`modul-2-testbericht.md:3`); 3b/3c/4/7 stehen dort mit «Abnahme offen», 1b–1d mit «Abgenommen».
- Das Feld `hauptfunktion` ist bei 15 Funktionen gesetzt (mehrfach belegte Knöpfe; die Wissensdatei nannte 12).

---

## Befundliste nach Schwere

### Hoch (falsch oder blockiert Phase 1/2)

1. **Keine belegte Mängelbasis für Phase 1.** Von 358 Funktionen sind 11 (3 %) geprüft, alle im Gerüstkapitel. Ablauf Phase 1 verweist für 0a/0b auf «Mängel aus der Kontrollliste» (`entwurf/ablauf.md:86`); diese Liste ist für 1a bis G leer. Quelle: Auftrag, `inventar.json`.
2. **«Fertig, wenn … am realen Projekt» ist für fast alle Module nicht belegt.** Grundsatz 22.10.2026 (`ablauf.md:31-33`). Real geprüft sind nur 1b «einpassen» (`zuteilung.md:410`), N6/N7/N17 mit Bernstrasse-Echtdaten (`NachbearbeitungN6N7EchtTests`, `TreppeEchtTests`) und Gesten (Abnahme am Desktop, nicht abgeschlossen).
   3c, 4A, 7A, 7B, die Erkennung neuer Architektenfassungen (3b), IFC-Clipping (N13) und die Referenz Walkringen in der Oberfläche laufen nur am Beispielprojekt, gegen Attrappe oder als Kern-Test. Quellen: je Modulabschnitt.
3. **Hülle (1c) ohne Kennwertquellen blockiert Phase 2 «Gebäudehülle/Auswertung».** `kataloge/materialien.json`: 0 Materialien; SIA 2001/SN EN ISO 10456, Rsi/Rse aktuell, Fenster, Wärmebrücken fehlen; BKW-IFC liefert U bis 13.075. Quelle: `modul-1c-testbericht.md:23-41`. Ablauf Phase 2 verlangt «Normwerte nur mit Quelle».
4. **Grundausstattung nicht freigegeben, drei Entscheide hängen an Christian.** K3 v_max, R1 11 Bogenkonflikte, R7 d32; ohne sie ist das «Fertig, wenn» der Serien (und der Abwasser-Serie) verfehlt und Phase-1-Zeile «Familien» (`ablauf.md:88`) blockiert. Quelle: `nachtbericht-2026-10-21.md:36-44,74`.
5. **Sieben Module ohne förmliche Abnahme, eines ohne Testbericht.** 3 (Endfreigabe), 3b, 3c, 4A, 7A, 7B (kein Bericht), G. Ohne Abnahme bleibt offen, was in Phase 1 «bereinigt» heisst. Quellen: `nachtbericht-2026-10-20.md:54`, `hlks_cad.md:207`.

### Mittel (Lücke zu «Fertig, wenn» oder Prozessrisiko)

6. Gesten: drei von fünf Kriterien offen (Fehlauslösungen, Bildrate mit GPU, Abbrüche nach 1–2 s); Tuning-Ort nach U3 unklar. `modul-gesten-testbericht-2026-10-26.md:80-101`.
7. 7B: Viewer-Öffnung (Revit/Solibri), Leistung < 30 s (65–70 s unter Last), BKP-Ausgabe «ohne Angabe», kein Testbericht. `nachtbericht-2026-10-21.md:61`, `IfcExport.cs:66`.
8. 3b: Fassungsabgleich nur durch Tests belegt; Vorgaben ohne dokumentierte Quelle. `modul-3b-testbericht.md:188,143-155`.
9. 3c: Normfrage nur für 62 von 315 Normen mit Volltext; zweiter Rechner und «ohne Abo» nur mit Attrappe; Sitzungsablage von Claude Code kann Normtexte enthalten. `modul-3c-testbericht.md:131-134`, `sicherheitspruefung.md:202`.
10. 2: Gemischter Familiensatz nur mit Testdateien (keine echte Hersteller-CAD-Datei). `modul-2-testbericht.md:15,126`.
11. **Doppelte Wege/Altlast XR:** Browser-VR, `/vr`, `/api/quest`, Register XR, 35 Inventareinträge, 325 Testmethoden, 4 Entwürfe (1403 Zeilen) liegen neben der App «H+K XR» in HK_XR; Architektur-Entscheid (entfernen/behalten) offen. Abschnitt XR-Reste.
12. **Cloud-Sitzungen können KICAD nicht mehr bauen** (HK_XR fehlt dort). Quelle: `CLAUDE.md` (Repo-Wurzel), Abschnitt «HK_XR». Wirkt auf jede Phase-1-Arbeit, die Cloud-Sitzungen nutzen soll.
13. Tests mit Aussagegrenzen: ca. 117 Web-Methoden nur gegen `ClaudeAttrappe`, 74 + 28 gegen `AdbAttrappe`, 59 «Echt»-Methoden (20 Dateien) überspringen sich ohne lokale Dateien (Cloud, anderer Rechner = stilles «grün»). Zeitgrenzen-Tests (`IfcEchtRauchTests` 68.8 s > 60 s, `SpeicherTests.PaketLesenWartetAufGesperrteDatei`) reissen unter Last in fast jedem Bericht (3, 3b, 4A, 7A).
14. **Status-Dokumente widersprechen sich:** N1 (`nachbearbeitung.md:10`, `luecken.json`), `inventar.json:stand`, `bauen.mjs` (m2 «Gebaut»), `vorrat_freitag.md` (Freitag-Priorisierung ohne Ergebnis), `modul-0a-testbericht.md` (Zahlen). Eine Gesamtschau braucht eine geführte Quelle für den Abnahmestatus.
15. 1d/3: HKAP-Änderungen und Datenart «straenge» nicht ausgeliefert; HKAP-Browser-Reihe seit 19.10. nicht dokumentiert. `modul-1d-testbericht.md:74`, `modul-3b-testbericht.md:195`.
16. Modul 5: Zweig 486 Commits hinter main, 21 Konfliktstellen im Probelauf, Prüfcodes kollidieren künftig (P33–P39 gegen P31/P32/P45 in main); Teil B und Prinzipschema von Normen/Beispielplan abhängig. Phase 3; hier nur als Merkposten.
17. Teil B von Modul 4 und Phase-3-Module warten auf SN 592000, SN EN 12056, SVGW W3 (nicht in `W:\05_Normen`). `modul-4a-testbericht.md:104`.

### Gering (Feilpunkt)

18. 0a: BKP vorläufig; Textfelder per MCP leeren; P08 Id statt Kennzeichen; kein Standabbild beim Beenden (`modul-0a-testbericht.md:98-110`).
19. 0b: zweiter PC, Mac, Skalierung (Status alle 3 s, Prüfsumme ganzes Modell, Paketarchiv), Schwellen nicht gemessen (`modul-0b-testbericht.md:97-113`).
20. 1a: Planblatt-Lage, Schnitt-Durchdringung, Ecken ziehen Nachbarwand nicht mit, Raum-Vorschau, Kürzel Füllbereich/Nordpfeil/Schnitt, sieben unbestätigte Annahmen (`modul-1a-testbericht.md:78-103`).
21. 1b: Boolesche Körper vereinfacht, IfcWall-Fassaden, Klassen ohne Katalog, BKW-Leistung (~0.6 B/s), IFC bis 2 GB im Speicher (`modul-1b-testbericht.md:157-172`).
22. 1c: nur flache Dächer, Hülle nicht im PDF, versetzter Gebäudeteil, Inselräume (`modul-1c-testbericht.md:161-176`).
23. 7A: Stempellage, Doppelpunkte, Erstelldatum, Plannummern nur Bernstrasse (`modul-7a-testbericht.md:64-76`).
24. N10 (pdf.js), N11 (VORNE), N13 (Schnittflächen offen), N15/N17/N18/N20/N21 (Annahmen), Öffnungs-Hinweis (`nachbearbeitung.md`, `vorrat_freitag.md`; Tabelle oben).
25. Aufräumen: leere Ordner `src/Hlks.Labor*`, `src/Hlks.Quest`, `src/Hlks.Xr`, `tests/Hlks.Labor.Tests`, `tests/Hlks.Xr.Tests`; 34 Agenten-Zweige `worktree-agent-*` und 4 `claude/*`-Zweige im Repository (`git branch -a`).
26. Inventar: 17 Brillen-App-Einträge `zb-quest-*` und `mg-empfindlichkeit` (Empfindlichkeiten sollen laut Entscheid 26.10. ins Labor, `hlks_cad.md:573`) passen möglicherweise nicht mehr zur Oberfläche; Stand unbelegt.

---

## Offen / nicht geklärt

- Inhalt des Artifacts «Kontrollliste» (nicht lesbar von hier): welche 11 Ids geprüft sind, woher 290 gegen 358.
- Ob Annahmen und Prüflisten von 1a, 1b, 2, 3, 3b, 7A nach dem jeweiligen Bericht von Christian bestätigt wurden: nur für 3c, 4A, Fach K (20.10.2026) belegt.
- Ob die Vorgaben von 3b (Spiel 50 mm usw.) eine dokumentierte Quelle haben.
- Aktuelle Testzahlen der Gesamtläufe nach dem 27.10.2026 und ob `geprueft` (7edee1a) alle Browser-Reihen einschliesst (`stand_pruefen` deckt sie laut `hlks_cad.md:210` nicht ab).
- Ob Test_praxis nach N6/N7 neu übernommen wurde (Architektur-IFC); belegt ist nur die Neuübernahme des Leitungs-IFC am 24.10.2026.
- Ob die Gesten-Tuning-Funktion (Labor L4) in HK_XR existiert.
- Zuordnung der Testzahlen nach Dateinamen ist grob; Browser-Prüfpunkte wurden nicht gezählt.
