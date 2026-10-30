# Gesamtschau Teil 3 – Daten und Quellen

Stand 30.10.2026 · unabhängige Prüfung `hlks-pruefer` (nur gelesen), main 7edee1a. Grundausstattung geprüft an den lokalen Familiendaten
`%USERPROFILE%\HLKS_CAD_build\grundausstattung\bibliothek` (Index 21.10.2026, 44 Familien). W: nicht erreichbar.
Stichproben durch den Hauptlauf bestätigt (K3 AR v_max 25 mit Konflikt `k-660c745dd6`, übrige Flexrohre 30; medien.json-Quelle; SchemaAnordnung.cs:377).

## (a) Befunde nach Schwere

**Hoch: keine.** Nachgerechnet: Referenzfall Walkringen (`kataloge/sanitaer_belastungswerte.json:61-85`) – ΣLU KW 80, WW 52, ΣDU 38.8,
Spitzen Kurve 3/5, Q_WW stimmen mit den Excel-Ergebnissen überein; jeder Apparatewert mit Excel-Zeile, der Code hält keine Zahl selbst.
Hülle rechnet nur mit λ/Rsi/Rse, die mit Quelle eingegeben wurden (`AufbauRechnung.cs:137-153`, `AufbauBefehle.cs:224`); keine eingebauten
Normzahlen im Kern.

**Mittel**
1. `kataloge/medien.json:2-6, 15-18` – Farben mit Quelle «SIA 410/1 Ziff. 3, übernommen aus KI-Planungsbüro»: ohne Ausgabe und Seite, Herkunft
   ein anderes Projekt. Nur Darstellung. → gegen Norm prüfen oder als Annahme kennzeichnen.
2. `kataloge/klassen.json:256…316` (BKP 242/243/244/251/254), `kataloge/ifc.json:130` – BKP ohne Ausgabe, laut 0a-Testbericht «vorläufig»;
   fliesst in IFC-Klassifikation und Listen.
3. `kataloge/schemasymbole.json:202, 238, 247, 265, 319, 382` – sechs Zuordnungen `"quelle": "offen"` (Druckreduzier-, Überström-, Thermostatventil,
   Entlüftung, 4-Weg-Mischer, Strömungswächter), elf «Annahme», zwei «Vorbereitung» für Arten, die `klassen.json` nicht kennt. Entscheid Christian.
4. `src/Hlks.Kern/Schema/SchemaAnordnung.cs:377` – Kommentar «Werte nach SVGW W3 mit Quelle», tatsächlich aus Christians Excel (W3 liegt nicht vor).
5. `kataloge/sanitaer_belastungswerte.json:48-53` – Abflusskennzahl K nur für Walkringen; ohne SN 592000 bleibt K Projektvorgabe (bekannte Normlücke).
6. Grundausstattung: elf Familien ohne `unterlagen[].leser` (u. a. FAM-21/22/23 Gewinderohr-Formteile, Stahlrohr geschweisst, Rechteckkanal,
   Mapress Edelstahl); alle übrigen auf `pdf-2`, **keine auf `pdf-3`** → Befund-Ids nach Umbau auf Leser 3 mehrdeutig.

**Gering**
7. `kataloge/klassen.json` – Einheiten uneinheitlich: `grad`/`Grad`/`°`, `m3/h`/`m³/h`.
8. Farbpalette nicht abgestimmt: zwei Rot (WW `#d0312d`, HZ-VL `#FF0000`), zwei Grün (KW `#2e8b3a`, Koordination Sanitär `#00B000`).
9. `berichte/grundausstattung-serien.md:17, 148` nennt 12 R1-Bogenkonflikte, die Daten haben 11.
10. `berichte/grundausstattung-serien.md:17-26` auf Stand 20.10.; am 21.10. Erledigtes steht dort noch als offen.
11. Flexrohr AR p_max: Beschreibung «Preisbuch 01.2014 S. 21» bei Datei `…_07_2026.pdf` – stimmig, aber verwirrend (unbelegt ohne PDF).

## (b) Stand der bekannten Konflikte

| Konflikt | Stand | Wer |
| --- | --- | --- |
| K3 AR v_max 25/30, p_max 3000 | **offen**: v_max 25 mit Konflikt `k-660c745dd6`, Kern schlägt 25 vor – gegen Entscheid Christian 21.10. («Broschüre 2025 gilt, 30 m/s»); p_max 3000 gesetzt | Christian (`konflikt_entscheiden`) |
| R1 Bogen | offen, 11 Konflikte (L/z 45°/90°, d12–d28) | Christian |
| R2 Bogen | offen, 6 Konflikte | Christian |
| R7 d32 ohne innen/Wand | offen, nur da = 32, nicht zeichenbar | Unterlage oder Kern-Ausbau |
| Nachführung PDF-Leser 3 | offen, keine Familie auf pdf-3; Server 5153 auf 580b9a6 | Hauptlauf |
| Umschlüsselung FAM-21/22/23 | offen | Hauptlauf nach Leser 3 |
| Schwache Quellen mit Vermerk (R6, R7, R2 T-Stück, K1) | erledigt | Christian bei Freigabe |
| R6/R7 Abwasser, Kürzel, R1 66,7, K2 frei | erledigt (K2: Obergrenze 2500 und Schritt sind Annahmen) | Christian bestätigt Annahmen |
| Übernahme in Bürobibliothek W: | offen, alle 44 Familien `entwurf` | Christian |

Katalog-Querbezüge (17 Medien = 17 Systemtypen = 17 IFC-Systemtypen; Anschluss-Medien der Klassen; Gewerke): **kein Widerspruch.**

## (c) Vorschlag Phase 1

1. Quellenpflicht in den Katalogen durchziehen (medien.json, BKP, Schemasymbole «offen», Einheiten, Kommentar SchemaAnordnung).
2. Normbeschaffung als Voraussetzung führen (SN 592000, SN EN 12056, SVGW W3, Kennwerte Rsi/Rse, Fenster, ψ); bis dahin in der Oberfläche als
   «Projektvorgabe ohne Norm» sichtbar.
3. Grundausstattung abschliessen: Server auf Leser 3, alle 44 Familien umschlüsseln, Entscheide K3/R1/R2 im Familieneditor, R7 d32 klären,
   Übersicht nachführen, freigeben und in die Bibliothek übernehmen.
4. Ein Ort für Entscheide: Ein Entscheid von Christian gilt erst, wenn er als `konflikt_entscheiden` an der Familie steht (K3 zeigt, was sonst passiert).

Nicht geprüft: Inhalte der Hersteller-PDFs, W:-Normenordner.
