# Grundausstattung Rohr- und Kanalserien – Übersicht zur Freigabe

Stand 20.10.2026 · Hauptlauf · Spezifikation `entwurf/grundausstattung-leitungsserien.md`

Arbeitsprojekt (lokal, nicht auf W:): `%USERPROFILE%\HLKS_CAD_build\grundausstattung\e_KICAD` – in KICAD über «Öffnen»
laden; Server der Läufe: Port 5153. Alle Familien sind **Entwürfe**. Konflikte entscheiden, freigeben und in die
Bürobibliothek übernehmen macht **Christian** (Familien-Explorer bzw. Familieneditor). Die KI-Läufe schreiben nur in eine
lokale Testbibliothek (`…\grundausstattung\bibliothek`), nie nach W:.

Jede Serie: KI-Auftrag «Familie aus Unterlagen» mit Internet (Herstellerunterlagen über `unterlage_aus_url`), danach eine
Stichprobe des Hauptlaufs gegen die Quelle.

## Stand je Serie

| Nr | Serie | Familien | Typen (Grössen) | Offene Konflikte | Stichprobe Hauptlauf | Zur Entscheidung |
| --- | --- | --- | --- | --- | --- | --- |
| R1 | Geberit Mapress C-Stahl | Serie, Bogen, T-Stück, Reduktion | 12 (d12 … d108) | 4 Familien mit je 1 Konflikt (Bogen 12 Werte) | bestanden (s. unten) | 66,7; Kürzel; neue Masse |
| R2 | Geberit Mapress Edelstahl | Serie, Bogen, T-Stück, Reduktion | 11 (d12 … d108), Kürzel CNS | Bogen 6 (neue Masse 2023/24) | bestanden | neue Masse; Auffälligkeit 45° |
| R5 | JRG Sanipex MT | Serie, Bogen, T-Stück (30 inkl. reduziert), Reduktion | 8 (12 × 1,7 … 63 × 4,5) | keine | bestanden | Kürzel; z1 Reduktion |
| R6 | Geberit Silent-db20 | Serie, Bogen, T-Stück (45°/88,5°), Reduktion | 7 (56 × 3,2 … 160 × 7), Kürzel PE-S | keine | bestanden | Quelle DIBt 2019; PE-S oder PE-S2 |
| R7 | Geberit PE | Serie, Bogen, T-Stück (50), Reduktion (34) | 13 (d32 … d315) | keine | bestanden | Kürzel; Quelle Ausgabe AU; d32 ohne Masse |
| R3 | Stahlrohr Gewinderohr | Serie, Bogen (Winkel Fig. 90/120), T-Stück (63), Reduktion (31) | 11 (1/4" … 4", EN 10255) | keine | bestanden | Kürzel; Rohrhersteller; Reihe M/H |
| R4 | Stahlrohr geschweisst | offen | | | | |
| K1 | Wickelfalzrohr rund | offen | | | | |
| K3 | Flexrohr | offen | | | | |
| K2 | Rechteckkanal | offen | | | | |

## R1 Geberit Mapress C-Stahl

- **Ergebnis:** Leitungsserie «Geberit Mapress C-Stahl» (12 Grössen d × s, DN nach Geberit, Bogenwinkel 45°/90°, Abzweig 90°)
  mit Formteilen Bogen (12 Typen, L/Z je Winkel), T-Stück (12), Reduktion (32 Paare). Beschreibung: Heizung und Kälte in
  geschlossenen Kreisläufen, mit und ohne Glykol; Ausführungen aussen verzinkt / kunststoffummantelt mit gleichen Massen.
- **Stichprobe Hauptlauf** gegen «Massänderungen Geberit Mapress C-Stahl April 2026 bis April 2027» (Geberit DE, Stand
  09.2025): Bogen d12 90° L 31 / Z 14, d12 45° L 23 / Z 6, d22 45° L 32 / Z 11 – stimmen. Bogen d22 90° Z 40 stimmt mit dem
  Datenblatt 2022; L 61 dort im Textauszug nicht eindeutig lesbar.
- **Zur Entscheidung:**
  1. **Grösse 66,7 × 1,5 (DN 65):** steht nur in den deutschen Geberit-Unterlagen; im Schweizer Katalog (catalog.geberit.ch,
     Bogen) fehlt sie (dort 76,1 für DN 65). Vorschlag: Typ 66,7 entfernen, falls er in der Schweiz nicht geführt wird.
  2. **Konflikte Bogen d12–d28:** alte Masse (Datenblatt 2022) gegen neue Masse ab April 2026 (Massänderungsliste). Vorschlag
     des Kerns = neue Masse.
  3. **Kürzel** für die Beschriftung fehlt (Werkstoff C-Stahl ist nicht in der Konvention CNS/PE-S) – z. B. «C-St»? Christian
     entscheidet.
  4. Quellen teils aus Händlerablagen (Geberit-Datenblätter bei heizung-billiger.de, unidomo.de, stober, sankom) – als «nicht
     vom Hersteller abgerufen» vermerkt; Werte inhaltlich Geberit-Dokumente.

## R2 Geberit Mapress Edelstahl

- **Ergebnis:** Serie (11 Grössen 12 × 1,0 … 108 × 2,0, DN nach Geberit, Kürzel **CNS** belegt mit Werkstoff CrNiMo 1.4401,
  Bogenwinkel 15/30/45/60/90°), Bogen (11), T-Stück (11, nur 90°), Reduktion (30 Paare wie Katalog Geberit Schweiz).
- **Stichprobe Hauptlauf** gegen «Massänderungen Geberit Mapress Edelstahl April 2023 bis April 2024» (Geberit Schweiz): Bogen
  90° d12 L 31 / Z 14, d15 L 38 / Z 18, d22 L 47 / Z 26 – stimmen.
- **Zur Entscheidung:** 6 Konflikte Bogen 90° (alt Datenblatt 2021 gegen neue Masse 2023/24; Vorschlag neue Masse).
  Auffällig laut KI: 45° d22/d28 kürzer als 30°/15° – Datenblatt prüfen. d12/d18 nur 45°/90°. Formteilmasse teils aus
  Händlerablagen (unidomo, sankom; T-Stück aus dem Geberit-Katalog Polen).

## R5 JRG Sanipex MT

- **Ergebnis:** Serie (8 Grössen 12 × 1,70 … 63 × 4,50, DN 8–50), Winkel 90°/45°, T-Stück 30 Typen (7 egal, 23 reduziert),
  Reduktion 16. Quelle GF Lieferprogramm Schweiz Februar 2025, Gegenprobe GF Deutschland 2017 – gleich.
- **Stichprobe Hauptlauf** gegen das GF-Sortiment (DE): Rohr 16 × 2,25 (DN 12) … 63 × 4,5 (DN 50), Winkel 90° 4670 d16 l 46 /
  z 27, d20 55 / 31, d63 142 / 79 – stimmen.
- **Zur Entscheidung:** Kürzel fehlt (Mehrschichtverbund PE-X/AL/PE-X, keine Konvention). Reduktion: Mass z1 im Massbild nicht
  eindeutig (l1 − z1 passt nicht zu den Einstecktiefen) – prüfen. Unstimmigkeit Herstellertabelle T-Stück 50-50-50 (z 64 statt
  63). d12 ohne Formteile, d16 ohne 45°-Winkel (laut Hersteller).

## R6 Geberit Silent-db20

- **Ergebnis:** Serie (7 Grössen 56 × 3,2 … 160 × 7, DN 56–150, Kürzel **PE-S**), Bogen 15/30/45/88,5°, T-Stück 45°/88,5° (19),
  Reduktion exzentrisch (13).
- **Stichprobe Hauptlauf** gegen geberit.ch (Produktseite Silent-db20): alle 7 Grössen d × s mit DN und Werkstoff PE-S2 – stimmen.
- **Zur Entscheidung:** Formteilmasse und s/di aus der **DIBt-Zulassung Z-42.1-265 (2019, abgelaufen 2022)** in einer
  Händlerablage (Heinze) – aktuelle Geberit-Datenblätter nur teilweise. Kürzel «PE-S» oder «PE-S2»? 67°-Bögen (nur DE) nicht
  übernommen. Lücken: Bogen 88,5° d160, T-Stück 88,5° d110/90 und d110/110.

## R7 Geberit PE

- **Ergebnis:** Serie (13 Grössen d32 … d315, DN 40–300), Bogen 15/30/45/88,5/90° (12), T-Stück 45°/88,5° (50), Reduktion (34).
- **Stichprobe Hauptlauf** gegen den Geberit-HDPE-Katalog (Rohrtabelle): d110 di 101,4 / s 4,3 (DN 100), d160 147,6 / 6,2,
  d315 295,4 / 9,8 – stimmen; Artikelnummern wie im Schweizer Katalog (Sortiment d40 … d315 bestätigt).
- **Zur Entscheidung:** Quelle ist die **australische Katalogausgabe 2020** (Händler Reece), Schweizer Massblätter nicht gefunden.
  d32 ohne DN/di/s (nicht platzierbar). Kürzel fehlt (PE-HD). Bogen-, T-Stück- und Reduktionsmasse in cm (als Formel × 10).
- **Befund KICAD (Produkt):** Der PDF-Leser liest Dezimalzahlen dieses Katalogs nicht als Zahl («4 .5») – die KI hat solche
  Werte ohne Quellenverweis eingetragen. → Nachbearbeitung im Unterlagen-Leser (Kern).

## R3 Stahlrohr Gewinderohr

- **Ergebnis:** Serie (11 Grössen 1/4" … 4", DN 8–100, Aussendurchmesser a und Wandstärke t aus Debrunner Acifer M100602
  rohschwarz und M100614 verzinkt, EN 10255, S195T; Werte beider Blätter gleich), Bogen = +GF+ Temperguss-Winkel Fig. 90 (90°) und
  Fig. 120 (45°), T-Stück Fig. 130 egal/reduziert (63), Reduktion = Muffe reduziert Fig. 240 (31); Fittings aus GF-Katalog
  770.971.132 (05.2024), Fig. 90 gegengeprüft mit Debrunner M105123 – gleich.
- **Stichprobe Hauptlauf** gegen Debrunner M100602: 1/2" 21,3 × 2,6 (DN 15), 1" 33,7 × 3,2 (DN 25), 2 1/2" 76,1 × 3,6 (DN 65) – stimmen.
- **Zur Entscheidung:** Kürzel fehlt (Stahl S195T). Debrunner nennt den Rohrhersteller und die Reihe der EN 10255 (mittel/schwer)
  nicht – Wandstärken entsprechen dem Debrunner-Sortiment. 45° nur 3/8" … 3". 1/8"-Fittings weggelassen (kein Rohr).