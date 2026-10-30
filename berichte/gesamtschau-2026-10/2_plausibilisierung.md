# Gesamtschau Teil 2 – Plausibilisierung am echten Projekt

Stand 30.10.2026 · Hauptlauf · Build `HLKS_CAD_build\desktop` (Commit 7edee1a), Prüfserver 5176, Projekt Test_praxis
(Arbeitskopie `%USERPROFILE%\HLKS_CAD_testdaten\test_praxis_arbeit`, Bernstrasse 42 Worb, Archicad-IFC 27, 102 Pakete).
W: war nicht erreichbar (kein VPN). Abfragen über die Kern-API (`/api/abfrage`, `/api/befehl`), dazu Grundriss und 3D in der Oberfläche.

## Was am echten Projekt trägt

- **Öffnen und Abfragen sind schnell.** Erster Aufruf nach dem Start ~2 s (alle Erstabfragen warten gemeinsam), danach fast alle
  Abfragen unter 40 ms: `projekt_ueberblick` 16 ms, `raumliste` 7 ms, `raumgrenzen` 35 ms (176 kB), `huellbauteile` 35 ms,
  `huelle_pruefen` 10 ms, `kollisionen` 11 ms.
- **3D mit Architekten-IFC lädt zügig:** Netz der Unterlage 17,5 MB in 59 ms (lokal), Prüfkörper 274 kB. Darstellung im Bild in
  Ordnung (Navigationswürfel, durchscheinende Wände, Öffnungen).
- **Grundriss 1.OG** zeichnet die übernommenen Wände, Türen und Fenster korrekt. Keine Fehler in der Browser-Konsole.
- **Befehle lehnen sauber ab** mit Grund und Vorschlag (z. B. fehlende Familie, fehlender Parameter).

## Was nicht trägt

| # | Schwere | Befund | Beleg |
| --- | --- | --- | --- |
| P1 | hoch | **Test_praxis enthält keine Gebäudetechnik.** 855 Elemente, alle Architektur (406 Wände, 74 Decken, 57 Türen, 49 Räume …); 0 Systeme, 0 Verbindungen, 0 Familien. Leitungen, Sanitär rechnen, Schema, Kollision, Aussparungen, Strangliste lassen sich am echten Projekt deshalb gar nicht plausibilisieren – der Grundsatz «bauen parallel mit realem Projekt» greift für Module 2–4 und 7 bisher nicht. | `projekt_ueberblick`; `sanitaer_zusammenstellung` → «Keine Anlage hat Sanitärapparate» |
| P2 | hoch | **Ohne VPN keine einzige Familie.** Die Familienbibliothek liegt auf `W:\03_CAD Bibliotheken\d_KICAD Familien`; offline meldet sie «letzter Stand vom .» (leeres Datum, also nie lokal zwischengespeichert), «mitgeliefert 0». `leitung_zeichnen` mit Serie R1 → «Familie «R1» gibt es nicht». Die Grundausstattung R1–R7/K1–K3 lebt nur im Arbeitsprojekt 5153, nicht in der Bibliothek. | `familien_bibliothek`, `familien` = [] |
| P3 | hoch (Phase 2) | **Räume decken nur einen kleinen Teil der Geschossfläche ab.** 1.OG: 7 Räume, zusammen 52 m²; grösste Decke im 1.OG allein 188 m². 1.UG: 4 Räume mit 17 m². Alle 49 Räume ohne Nummer, Name und Nutzung; Raumhöhe = Geschosshöhe (3,06 m) statt lichter Höhe, Volumen damit zu gross. Für jede Auswertung (Phase 2) und jeden Rechenkern unbrauchbar. Bestätigt beim Neuaufsetzen 30.10. (frische `ifc_uebernehmen`, 1.OG wieder 53 m²): Die IFC hat kein IfcSpace, KICAD bildet die Räume aus den Wänden («ab 1 m², ohne Aussenfläche»); 22 Wände und 19 Fenster (Wirt IfcBuildingElementProxy) landen im Hintergrund, die Umrisse schliessen sich nicht, grosse Wohnräume gelten vermutlich als Aussenfläche (unbelegt). | `raumliste`, Deckenflächen per `element_lesen` |
| P4 | hoch (Phase 2) | **Hülle ohne Kennwerte:** 495 Bauteile ohne Aufbau, 40 Aufbauten ohne U-Wert, 15 Fenster und 57 Türen ohne Kennwerte, 25 Materialien ohne λ, 3 Gruppen Rsi/Rse fehlen. Deckt sich mit der bekannten Lücke «Kennwertquellen fehlen». | `huelle_pruefen` |
| P5 | mittel | **HKAP-Abfrage blockiert 4,1 s**, wenn HKAP nicht läuft (`hkap_verbindung`). Projekt ist nicht mit HKAP verknüpft. | `hkap_verbindung` |
| P6 | mittel | **XR-Reste in KICAD trotz Umzug nach HK_XR:** Die 3D-Ansicht lädt 13 Module `xr-*`/`vr*` (~250 kB), die Oberfläche fragt laufend `/api/xr/desktop/zustand` (alle 2 s), `/api/xr/teilnehmer` und `/api/quest/automatik` ab; Register «XR», Statusfeld «Brille: nicht gefunden», Knopf «In VR begehen». Klären, was davon gewollt bleibt (Teilnehmer der HK_XR-Brille) und was Altlast ist. | Netzwerkprotokoll 3D-Ansicht |
| P7 | hoch | **`trockenlauf` im Körper wird über HTTP stillschweigend übergangen – der Befehl wird ausgeführt.** `/api/befehl/{name}` liest den Trockenlauf nur aus der Adresse (`?trockenlauf=true`, `src/Hlks.Web/Program.cs:160`); `faehigkeiten` sagt nur «mit trockenlauf=true». Belegt am 30.10.: `ifc_uebernehmen` mit `{"trockenlauf": true, …}` im Körper hat 775 Änderungen gespeichert. Unbekannte Parameter werden allgemein nicht abgelehnt. Gefährlich für jede KI bzw. jedes Skript über HTTP und für den Generator (Vorhaben «Erzeugendes CAD»). | `ifc_uebernehmen`, `leitung_zeichnen` |
| P8 | gering | `/api/status` wird mehrmals pro Sekunde abgefragt und braucht 150–280 ms je Aufruf. | Netzwerkprotokoll Start |
| P9 | gering | `plansaetze`: Projektnummer und Objektordner leer (bekannt: Projektnummer steht nicht in projekt.json). | `plansaetze` |

## Nicht gemessen

- **Bildrate 3D mit IFC:** Der eingebettete Browser drosselt auch den leeren Grundriss auf 1 Bild/s, die Messung sagt nichts. Am Desktop
  (Edge-App-Fenster) nachmessen.
- **Zwei PCs gleichzeitig, NAS-Betrieb:** ohne VPN nicht prüfbar (offen seit 0b).
- **Brille:** nicht Teil von KICAD-Phase 0 (HK_XR).

## Folgerung für Phase 1

1. Familien offline verfügbar machen (lokaler Zwischenstand der Bibliothek, Grundausstattung in die Bibliothek bzw. mitgeliefert).
2. In Test_praxis einen echten Gebäudetechnik-Anteil aufbauen (z. B. Sanitär-Strang einer Wohnung mit Apparaten), damit Module 2–4, 3b und
   7 am realen Projekt geprüft werden können. Umfang mit Christian festlegen.
3. Räume (P3) und Hüllkennwerte (P4) sind Phase 2, blockieren dort aber alles; Ursache von P3 früh klären.
