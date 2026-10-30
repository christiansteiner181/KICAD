# Gesamtschau Teil 4 – Technik

Stand 30.10.2026 · unabhängige Prüfung `hlks-pruefer` (nur gelesen, statisch, keine Builds/Tests), HEAD f39130d. Stichproben H1, H2 durch den
Hauptlauf bestätigt (`Projekt.cs:136-176`, `zustand.js:98-99`).

## (a) Befunde nach Schwere

**Hoch**
- **H1 – Jeder Befehl kopiert das ganze Modell und rechnet alle Prüfungen dreimal** (`src/Hlks.Kern/Projekt.cs:136` `Modell.Kopie()`, auch im
  Trockenlauf; `:160` und `:164` `Pruefungen.Alle(Modell)` zweimal auf dem alten Stand, `:161` einmal auf dem neuen). Mit IFC-Architektur im Modell
  volle Neuberechnung bei jeder kleinen Änderung. Für Generator-Varianten (hunderte Trockenläufe) zu teuer.
  → `vorher` je Stand zwischenspeichern, Prüfungen inkrementell, Kopie nur der betroffenen Elemente.
- **H2 – Oberfläche lädt nach jeder Änderung das ganze Modell** (`src/Hlks.Web/wwwroot/js/zustand.js:98`, nur wegen der Verbindungen;
  `Api/Darstellung.cs:40, 52-60` serialisiert alles samt Geometrie). Auch jedes fremde Paket (Abgleich alle 2 s). → Verbindungen der betroffenen
  Elemente in `/api/elemente` mitliefern.
- **(aus Teil 2) P7 – `trockenlauf` im Körper wird über HTTP übergangen, der Befehl wird ausgeführt** (`Program.cs:160` liest nur die Adresse);
  unbekannte Parameter werden nicht abgelehnt.

**Mittel**
- **M1 – «W: nur lesen» ist nicht erzwingbar:** Öffnen schreibt Standabbild (`ProjektDienst.cs:315, 505-518`), Reservierungen (`Projekt.cs:180`),
  `ki\gespraeche`, `vorschau.png/json` (`StartDienst.cs:401-425`), `austausch` (`OrdnerSpeicher.cs:313-318`). Kein Nur-Lese-Modus.
- **M2 – Eine Sperre für alles** (`ProjektDienst.cs:83, 155-164`): jede Abfrage und jeder Befehl, auch `Grafik.Koerper`/`Darstellung.Modell` des
  ganzen Modells. Zweistufig nur `leitungsserie`. → unveränderlicher Lesestand für Abfragen.
- **M3 – Abgleich listet alle Pakete alle 2 s** (`OrdnerSpeicher.cs:139-140`, über SMB), Pakete wachsen ohne Archiv. → Archiv `aenderungen\archiv\`,
  Listing nur ab letzter bekannter Id.
- **M4 – Browser-VR neben HK_XR-App:** `VrSeite.cs` (`/vr`), `VrAuftrag.cs`, `QuestEndpunkte.cs`/`QuestStarter.cs`, `vr.html`, `vr-start*.js`,
  `vr3d.js`, `vr-logik.js`, `xr-*.js` (≈ 4 900 Zeilen). U3 hat den Browser-Weg nicht entfernt. Entscheid nötig.
- **M5 – Dateiausgaben ohne Trockenlauf und ohne Paket:** `aussparungen_ifc`, `plansatz_pdf`, `plan_pdf` laufen als Abfragen an `Ausfuehren` vorbei
  (`AussparungBefehle.cs:90`, `PlansatzBefehle.cs:88`, `Api/Abfragen.cs:49`) – Ausgabe an Dritte ohne Protokoll im Modell.

**Gering**
- G1 Datei-Endpunkte ohne Pfadgrenze (`/api/ordner`, `/api/projekt/vorschau`, `/api/ordner/zeigen`), durch Herkunftsschutz (nur localhost, POST
  same-origin) abgedeckt; Traversal bei `/hilfe`, `/api/ifc/datei`, `/unterlagen/{id}/netz` dicht.
- G2 HK_XR-Netzzugang bindet 0.0.0.0 (nur bei Schalter «an»); KICAD selbst nur localhost.
- G3 KI-Läufe nur zeitlich begrenzt (10/20 min), keine Rundengrenze; Einordnung der Werkzeuge vollständig.
- G4 Doppelte Endpunkt-Hilfen (`Program.cs:88-105` neben `EndpunktHilfe.cs:12-31`) mit unterschiedlichem Verhalten.
- G5 Keine Geheimnisse im Repo.
- **Umgehungen des Befehlssystems: keine.**

## (b) Altlasten

1. Leere Ordner `src/Hlks.Labor`, `src/Hlks.Labor.Quest`, `src/Hlks.Quest`, `src/Hlks.Xr`, `tests/Hlks.Labor.Tests`, `tests/Hlks.Xr.Tests` (nicht in slnx/git).
2. `.claude/worktrees/`: 40 Worktrees, **1,8 GB in OneDrive**, 41 lokale Zweige; externe Worktrees `_labor`, `_vergleich`, `_grundausstattung` prüfen.
3. Ungetrackt: `.codex/agents/*.toml`, `AGENTS.md` (Codex) – einchecken oder ignorieren.
4. Browser-VR (M4), falls «nur App».
5. Tests, die Quelltext nachbuchstabieren: `LeitungenPruefungTests.cs:173-180`, `liste-3b.test.mjs:127`, `gesten-ausloesen.test.mjs:807-814`,
   `anordnung.test.mjs:620-627`, `hilfe.test.mjs:38-39`, `koordination.test.mjs:1199`, `nachbearbeitung-n3.test.mjs:314`,
   `plandarstellung-b1.test.mjs:32`, `xr-register.test.mjs:147,154`, `vr-start.test.mjs:209`.

## Stand der Tests (gezählt, nicht ausgeführt)

xUnit Kern 2 772 Fälle (259 Dateien), MCP 82, Web 513; Node ≈ 1 376 Fälle in 57 Dateien; 42 Browserreihen. Nur mit Attrappe: alle KI-Tests
(`Hlks.ClaudeAttrappe`), Quest (`Hlks.AdbAttrappe`). 28 Kern-Testdateien lesen Echtdaten und überspringen sich ohne. Dünn: `OrdnerSpeicher`
(NAS-kritisch), `Datenquellen`; keine Last-/Grössentests.

## (d) Andockstellen für das Vorhaben «Erzeugendes HLKS-CAD»

| Andockstelle | Befund |
| --- | --- |
| Trockenlauf | trägt für Befehle (`Projekt.cs:170-175` kehrt vor Reservieren, Paket, Bibliothek zurück); **nicht** für Dateiausgaben (M5); über HTTP nur per Adresse (P7); Kosten je Lauf hoch (H1) |
| `Routing.Planen` | trägt: `Leitungen/Routing.cs:290`, reine Eingabe (Serie, Grösse, Punkte, Enden, Optionen), Ergebnis `RoutingPlan` mit Teilen, Geraden, Längen; fertiges Kennzahlen-Objekt fehlt |
| Prüfkennzahlen P13–P18 | trägt: Abfrage `pruefen`, Trockenlauf liefert neue/behobene Befunde je Variante |
| Kollision | **nur teilweise:** `kollision_pruefen` ist ein Befehl auf Elementen im Modell; «Kollisionen für diese Prüfkörper» ohne Übernahme fehlt |
| `Paket`/`Herkunft` | trägt: optionale Felder `begruendung`/`prozessschritt` ohne Formatbruch (`Grundtypen.cs:179-189`, Json überspringt unbekannte Felder); einzige Schreibstelle `Projekt.cs:193-197`; `schema/hlks-modell.schema.json` setzt teils `additionalProperties: false` → nachführen |

## (c) Vorschlag Phase 1, nach Nutzen

1. H1 + H2 (spürbar bei jedem Klick; Voraussetzung Generator).
2. P7 Trockenlauf/unbekannte Parameter.
3. M1 Nur-Lese-Modus (oder Arbeitskopie).
4. M3 Paketarchiv, M2 Lesestand.
5. M4 Entscheid Browser-VR, Altlasten 1–3.
6. M5 Ausgaben als Befehle mit Paket.
7. G4, Altlast 5 bei Gelegenheit.
