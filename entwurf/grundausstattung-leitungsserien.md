# Grundausstattung Rohr- und Kanalserien – Spezifikation

Stand 20.10.2026 · Hauptlauf

## Anlass und Entscheide

Christian, 20.10.2026: «Die Rohr- und Kanalfamilien sollten generell integriert werden. Ist das vorgesehen?» – Heute gibt es
das Gerüst (Modul 3: Leitungsserie = Familie der Kategorie Rohr/Kanal, Grössen als Typen, Formteil-Familien und -Regeln) und
vier Serien nur im Abnahmeprojekt 3; die Bürobibliothek ist leer (keine Erstbefüllung, Entscheid 19.10.2026). Fragefenster:

- **Rohre:** Pressfitting Metall, Stahlrohr, Kunststoff Trinkwasser, Kunststoff Abwasser (FBH-Rohr «vorerst weglassen» → Modul 5).
- **Kanäle:** Wickelfalzrohr rund, Rechteckkanal, Flexrohr.
- **Quelle:** «Lüftung Schmidlin, andere Hersteller angeben»; Stahlrohr «recherchiere ev. Debrunner».
- **Ablage:** Bürobibliothek `W:\03_CAD Bibliotheken\d_KICAD Familien` (in jedem Projekt ohne Auswahl sichtbar).

## Serien

| Nr | Serie | Gewerk / Medien | Quelle der Abmessungen |
| --- | --- | --- | --- |
| R1 | Geberit Mapress C-Stahl | Heizung/Kälte (HZ-…, KAE-…) | Geberit, Herstellerunterlagen |
| R2 | Geberit Mapress Edelstahl | Sanitär (KW, WW, ZIR) | Geberit |
| R3 | Stahlrohr Gewinderohr | Heizung/Kälte | Debrunner Acifer (Katalog recherchieren; Hersteller/Norm im Datenblatt nennen) |
| R4 | Stahlrohr geschweisst | Heizung/Kälte | Debrunner Acifer |
| R5 | JRG Sanipex MT | Sanitär | Georg Fischer JRG |
| R6 | Geberit Silent-db20 | Abwasser (SW, RW) | Geberit |
| R7 | Geberit PE (PE-HD) | Abwasser, Grundleitungen | Geberit |
| K1 | Wickelfalzrohr rund | Lüftung (AUL, ZUL, ABL, FOL) | Schmidlin-Katalog Kanäle (W:\09_Preislisten HKAP) |
| K2 | Rechteckkanal | Lüftung | Schmidlin |
| K3 | Flexrohr | Lüftung | Schmidlin |

Je Serie die Formteil-Familien nach Modul 3 (Bogen, T-Stück, Reduktion, Muffe bzw. Kanal-Formteile), soweit die Quelle sie
mit Massen enthält; fehlt ein Mass, bleibt der Formteiltyp offen (Befund), nie geschätzt.

## Regeln

- **Abmessungen nur aus der genannten Quelle** (Herstellerunterlagen bzw. Schmidlin-Katalog), mit Quelle je Wert; **nie aus
  dem Gedächtnis**, nie DN aus Aussendurchmesser oder umgekehrt herleiten; **keine Preise** übernehmen (Schmidlin).
- Schmidlin-Kataloge nur lesen (lokale Kopie unter `%USERPROFILE%\HLKS_CAD_testdaten`), nie nach OneDrive/Repo.
- Weg: KI-Auftrag «Familie aus Unterlagen» (Modul 2, mit Internet für Herstellerunterlagen, `unterlage_aus_url`) in einem
  lokalen Arbeitsprojekt `%USERPROFILE%\HLKS_CAD_build\grundausstattung\e_KICAD` – **nicht direkt auf W:**. Ergebnis sind
  Entwürfe; Konflikte und **Freigabe nur durch Christian**; danach übernimmt **Christian** sie per Knopf in die Bürobibliothek.
- Kürzel je Serie für die Beschriftung (Abwasser Aussendurchmesser «PE-S 110», sonst NW/DN nach Projekteinstellung).
- **Grössen (Christian, 20.10.2026):** «DN ist immer vom Innendurchmesser abgeleitet» – nennt der Hersteller eine DN, gilt seine (z. B. Mapress 22 mm = DN 20,
  bestätigt 20.10.2026); nennt die Quelle keine DN, gilt DN = grösste
  Stufe ≤ di (für alle Normgrössen bis DN 300 gleich wie die Debrunner-Tabellen); **Lüftung: Grösse = Aussendurchmesser**; Rohrserien
  **bis DN 300** («die anderen können bis 300 berücksichtigt werden»); Zwischengrössen mit gleicher DN gestrichen, je DN die Normgrösse.
- Kälte: R1/R3/R4 decken HZ- und KAE-Medien (Wasser und Glykol) ab (`entwurf/gewerk-kaelte.md`).

## Ablauf

1. Nach dem Umbau «Kälte ist Teil der Heizung» (Medien) starten.
2. Läufe einzeln (Abo-Fenster), je Serie Protokoll; Hauptlauf prüft Stichproben gegen die Quelle.
3. Übersicht für Christian: je Serie Typen, Quelle, Konflikte → Freigabe → Übernahme in die Bibliothek.

**Fertig, wenn** alle zehn Serien als freigegebene Familien in der Bürobibliothek liegen und in einem neuen Projekt ohne Auswahl
im Explorer erscheinen; Rohr bzw. Kanal zeichnen bietet sie an.
