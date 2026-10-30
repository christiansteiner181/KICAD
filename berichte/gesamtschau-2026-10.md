# KICAD – Gesamtschau Phase 0 (Oktober 2026)

Stand 30.10.2026 · Hauptlauf · main 3c52847 (geprueft 7edee1a). Auftrag: `entwurf/ablauf.md`, Phase 0. Teilberichte:

| Teil | Inhalt | Wer |
| --- | --- | --- |
| [1 Bestand](gesamtschau-2026-10/1_bestand.md) | Stand je Modul gegen «Fertig, wenn», Abnahmen, Tests, offene Punkte | hlks-modul (gelesen), Stichproben Hauptlauf |
| [2 Plausibilisierung](gesamtschau-2026-10/2_plausibilisierung.md) | Hauptfunktionen am echten Projekt Test_praxis | Hauptlauf |
| [3 Daten und Quellen](gesamtschau-2026-10/3_daten_quellen.md) | Kataloge, Normwerte, Grundausstattung, Konflikte | hlks-pruefer (unabhängig) |
| [4 Technik](gesamtschau-2026-10/4_technik.md) | Architektur, Leistung, Sicherheit, Tests, Andockstellen Vorhaben | hlks-pruefer (unabhängig) |

## Kurzbild

- **Gebaut ist viel, belegt am echten Projekt wenig.** 15 Module/Teile gebaut, ca. 4 700 automatische Tests (Kern 2 772, Web 513, MCP 82,
  Node ≈ 1 376). Am echten Projekt belegt sind nur «IFC einpassen», N6/N7/N17 und die Gesten am Desktop. Das alte Test_praxis enthielt keine
  Gebäudetechnik – Module 2–4, 3b, 7 liessen sich dort gar nicht prüfen.
- **Abgenommen:** 0a, 0b, 1a, 1b, 1c (mit offenen Kennwertquellen), 1d, 2. **Nicht förmlich abgenommen:** 3 (Vorbehalt nie aufgehoben), 3b, 3c,
  4A, 7A, 7B (ohne Testbericht), G (3 von 5 Kriterien offen).
- **Die Kontrollliste trägt nicht:** 11 von 358 Funktionen geprüft (3 %); `inventar.json` enthält noch 35 XR-Einträge.
- **Daten sind sauber, wo gerechnet wird** (keine Normzahl ohne Quelle im Kern, Referenz Walkringen stimmt). Lücken in Darstellungsquellen
  (Farben SIA 410/1, BKP, 6 Schemasymbole «offen»).
- **Grundausstattung nicht fertig:** 44 Familien alle Entwurf, nicht in der Bibliothek; K3 (gegen Christians Entscheid noch 25 m/s), R1 11 und R2 6
  Bogenkonflikte, R7 d32 nicht zeichenbar, kein Familienstand auf Leser 3.
- **Technik trägt im Grundsatz** (keine Umgehung des Befehlssystems, Sicherheit dicht), ist aber für grosse Modelle und Varianten zu teuer
  (H1/H2) und hat eine Trockenlauf-Falle über HTTP (P7).

## Befunde hoch (alle Teile)

| # | Befund | Teil |
| --- | --- | --- |
| 1 | Ohne VPN keine Familie; Grundausstattung nicht in der Bibliothek → am echten Projekt keine Leitung möglich | 2 P2, 3 |
| 2 | Trockenlauf im Körper wird über HTTP übergangen, Befehl wird ausgeführt; unbekannte Parameter nicht abgelehnt | 2 P7 |
| 3 | Jeder Befehl: volle Modellkopie + 3× alle Prüfungen; Oberfläche lädt nach jeder Änderung das ganze Modell | 4 H1, H2 |
| 4 | Räume aus IFC ohne IfcSpace decken nur einen Bruchteil der Geschossfläche (1.OG 53 m²), ohne Name, Höhe = Geschosshöhe | 2 P3 (Phase 2) |
| 5 | Hülle ohne Kennwerte (λ, Rsi/Rse, Fenster, ψ); `materialien.json` leer | 1, 2 P4, 3 (Phase 2) |
| 6 | Sieben Module ohne förmliche Abnahme; «Fertig, wenn … am realen Projekt» fast nirgends belegt | 1 |
| 7 | K3 v_max: Entscheid Christian 21.10. nie an der Familie umgesetzt (Kern schlägt 25 vor) | 3 |

## Bereits erledigt am 30.10.2026

- **Test_praxis neu** als reine Architekturgrundlage (nur Architekten-IFC + Nachbau, Grundrisse 1:50, 3D) – auf W: ersetzt, altes Projekt in
  `_KICAD_alt_20261030`, lokale Kopien nachgeführt.
- **Vorhaben «Erzeugendes HLKS-CAD»** technisch geprüft und in `ablauf.md` eingebaut (Phase 1 Vorarbeiten, Phase 2 Räume/Zonen, Modul 12
  Generator und Bewertung, Phase 4).

## Vorschlag: Reihenfolge Phase 1 (Festlegung durch Christian)

| Rang | Paket | Inhalt | Warum jetzt |
| --- | --- | --- | --- |
| 1 | **Sofortkorrekturen** | P7 Trockenlauf/unbekannte Parameter ablehnen; Altlasten: leere Ordner, 40 Worktrees (1,8 GB) aus OneDrive, `inventar.json` ohne XR | klein, Risiko für jede KI-Nutzung; räumt auf vor allem anderen |
| 2 | **Familien und Grundausstattung** | lokaler Bibliotheksstand (offline), Leser 3, Umschlüsselung 44 Familien, Entscheide K3/R1/R2 (Christian), R7 d32, Freigabe, in Bibliothek | ohne Familien keine Leitung am echten Projekt |
| 3 | **Leistung Kern/Oberfläche** | H1 (Prüfungen inkrementell, keine Vollkopie), H2 (nur Betroffenes nachladen), M2 Lesestand, M3 Paketarchiv | jeder Klick am echten Projekt; Voraussetzung Generator |
| 4 | **Protokoll (Baustein B)** | `begruendung`, `prozessschritt` am Paket; M5 Dateiausgaben als Befehle mit Paket | klein, Formate jetzt noch frei |
| 5 | **Offene Abnahmen am echten Projekt** | 3, 3b, 3c, 4A, 7A, 7B, G: je Modul die Gebäudetechnik in Test_praxis aufbauen, die es braucht (z. B. Sanitär-Strang einer Wohnung), dann Abnahme mit Christian | Grundsatz «bauen parallel mit realem Projekt» einlösen |
| 6 | **Datenqualität Kataloge** | Quellen Farben (SIA 410/1 Ausgabe/Seite oder Annahme), BKP-Ausgabe, 6 Schemasymbole «offen», Einheiten | klein, vor Ausgaben an Dritte |
| 7 | **Kontrollliste neu** | aus aktuellem `inventar.json` (358 → ohne XR), dann punktuelle Prüfung durch Christian | Mängelbasis für den Rest von Phase 1 |
| 8 | **Rest** | M1 Nur-Lese-Modus, G4 Endpunkt-Hilfen, Tests, die Quelltext nachbuchstabieren, Zweig `pause/modul5-datenmodell` (486 Commits zurück) neu aufsetzen statt zusammenführen | bei Gelegenheit |

## Entscheide, die bei Christian liegen

1. Reihenfolge oben freigeben oder ändern.
2. **Browser-VR in KICAD** (`/vr`, Quest-Browserweg, ≈ 4 900 Zeilen, 325 Testmethoden) streichen – oder bewusst als zweiten Weg neben der H+K-XR-App behalten.
3. **Familienkonflikte** im Familieneditor: K3 v_max (30 laut Entscheid 21.10.), R1 11 und R2 6 Bogenkonflikte; R7 d32 (Unterlage mit di/s oder Typ streichen).
4. **6 Schemasymbole «offen»** (Druckreduzier-, Überström-, Thermostatventil, Entlüftung, 4-Weg-Mischer, Strömungswächter).
5. **Normen** (SN 592000, SN EN 12056, SVGW W3, Kennwerte Hülle) – beschaffen und Ablageort nennen; blockiert 4B und Phase 2/3.
6. **`AGENTS.md` / `.codex/`** im Repo (ungetrackt, 26.10.): einchecken oder ignorieren?
