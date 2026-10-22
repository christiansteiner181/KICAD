# KICAD – Ablauf in drei Phasen

Stand 22.10.2026 · Hauptlauf. Entscheid Christian Steiner, 22.10.2026:

> «wir planen den ablauf so: 1. Grundmodule bereinigen, Nutzung, Anzeigen, Verhalten, Logik, Familien usw. 2. Gebäudeerfassung und
> Auswertung 3. Fachmodule, spez. Berechnungen, Systemerkennung, Darstellungen usw.»

Der Ablauf ersetzt die bisherige Reihenfolge nach Modulnummern aus dem Factsheet vom 17.10.2026. Module, «Fertig, wenn …» und Abnahme
bleiben. Neu ist, dass zuerst das Bestehende bereinigt wird, bevor neue Fachmodule kommen. Modul 5 (Heizung) bleibt pausiert, bis Phase 3
beginnt.

## Phase 1 – Grundmodule bereinigen
Nutzung, Anzeigen, Verhalten, Logik, Familien. Was gebaut ist, soll sich so verhalten, wie Christian es von Revit und HKAP gewohnt ist.

| Bereich | Module | Offene Punkte (Stand 22.10.2026) |
| --- | --- | --- |
| Datenmodell, Gerüst, Explorer, live | 0a, 0b | Mängel aus der Kontrollliste (Artifact, Sammlung `pruefung`) |
| Grundriss, 2D, 3D, Ansichten | 1a; Feilarbeiten N6–N22 | Ideenvorrat `vorrat_freitag.md`; Restpunkte `nachbearbeitung.md` |
| Familien, Grundausstattung, Bibliothek | 2 | Konflikte K3 (v_max 25/30) und 11 Bogen-Konflikte R1 (Entscheid Christian); R7 d32 ohne innen/Wand; Grundausstattung auf PDF-Leser 3 nachführen |
| Leitungen, Schema | 3 | Vorbehalt der Abnahme: Strangschema nach Christians Beispiel (B8) |
| Kollision, Aussparungen | 3b | Abnahme mit Christian |
| KI-Palette | 3c | Abnahme mit Christian |
| Ausgabe Planblatt | 7 Teil A | Abnahme mit Christian |
| Bedienung in VR | Modul XR, Übertheme «Quest App» (`modul-xr.md`) | Grundsatzentscheide (Technik, Verbindung, Verteilung); Platz im Ablauf durch Christian zu bestätigen |
| Kern-Lücken aus XR-B | Kern | `leitung_versetzen` (verbundene Leitung quer versetzen, Verzüge mit Bögen der Serie); Reservierung beim Greifen |

## Phase 2 – Gebäudeerfassung und Auswertung
Das Gebäude kommt vollständig und richtig ins Modell: Räume, Hülle, Anforderungen. Es lässt sich auswerten.

| Bereich | Module | Inhalt |
| --- | --- | --- |
| Grundlagen, Räume, IFC-Übertrag, openBIM | 1b | Architekten-IFC sauber übernehmen (Türen, Treppen, Räume, Decken), Nachbau und Original, Raumerkennung |
| Gebäudehülle | 1c | Hülle, Raumgrenzen, Bauteile mit Aufbau |
| HKAP-Schnittstelle | 1d | Raumbuch, Anforderungen (Personen, Temperaturen) aus HKAP |
| Auswertung | neu | Flächen, Volumen, Hüllflächen, Raumlisten als Grundlage der Fachmodule. Den Umfang legt Christian fest; Normwerte nur mit Quelle |

## Phase 3 – Fachmodule
Spezielle Berechnungen, Systemerkennung, Darstellungen.

| Modul | Inhalt | Stand |
| --- | --- | --- |
| 4 | Sanitär rechnen | Teil A (Belastungswerte) gebaut; Teil B wartet auf SN EN 12056 und SVGW W3 |
| 5 | Heizung, Hydraulik | Teil A begonnen (Datenmodell, Heizsymbole, Zweig `pause/modul5-datenmodell`), pausiert |
| 6 | Lüftung, Kanalnetz | offen |
| 7 | Ausgabe: Planset PDF/DWG, Listen, Ausschreibung, IFC mit IDS | Teil A gebaut; IFC-Export und IDS teilweise |
| 7b | Partneraustausch | offen |
| 8 | MSRL, Messkonzept | offen |
| 9 | GA-Anbindung | offen |
| 10a, 10b | Physiksimulation, Raumluft (CFD) | offen |
| 11 | Monitoring | offen |

## Offen
- Platz der Quest App im Ablauf; Vorschlag: Phase 1, Bereich «Anzeigen», beginnend mit den Grundsatzentscheiden.
- Umfang der «Auswertung» in Phase 2.
- Die Termine im Factsheet (Meilensteine M3–M5) nachführen, sobald die Phasen geschätzt sind.
