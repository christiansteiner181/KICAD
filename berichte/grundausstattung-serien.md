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
| R2 | Geberit Mapress Edelstahl | läuft | | | | |
| R5 | JRG Sanipex MT | offen | | | | |
| R6 | Geberit Silent-db20 | offen | | | | |
| R7 | Geberit PE | offen | | | | |
| R3 | Stahlrohr Gewinderohr | offen | | | | |
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
