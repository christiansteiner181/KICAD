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
