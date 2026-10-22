# KICAD – Ablauf in Phasen (0 bis 3)

Stand 22.10.2026 · Hauptlauf. Entscheid Christian Steiner, 22.10.2026:

> «wir planen den ablauf so: 1. Grundmodule bereinigen, Nutzung, Anzeigen, Verhalten, Logik, Familien usw. 2. Gebäudeerfassung und
> Auswertung 3. Fachmodule, spez. Berechnungen, Systemerkennung, Darstellungen usw.»

Der Ablauf ersetzt die bisherige Reihenfolge nach Modulnummern aus dem Factsheet vom 17.10.2026. Module, «Fertig, wenn …» und Abnahme
bleiben. Neu ist, dass zuerst das Bestehende bereinigt wird, bevor neue Fachmodule kommen. Modul 5 (Heizung) bleibt pausiert, bis Phase 3
beginnt.

## Grundsatz: Bauen parallel mit einem realen Projekt (seit 22.10.2026)
Christian, 22.10.2026: «ab sofort bauen wir parallel mit einem realen projekt, wie ich bereits begonnen habe zu testen.»

- **Reales Projekt:** das Projekt, mit dem Christian seit dem 21.10.2026 testet: «Test_praxis» unter
  `W:\02_Projektdateien Objekte\Bolligen_HK_Intern\03_Pläne und Modelle`, mit Architekten-IFC als Unterlage. Ein Wechsel oder ein zweites
  Projekt legt Christian fest.
- **Jede Bauaufgabe endet mit einer Prüfung am realen Projekt.** Grüne Tests mit Beispielprojekt und Attrappen reichen nicht mehr. Der
  Hauptlauf spielt die geänderte Funktion am realen Projekt durch (Desktop bzw. Brille) und hält das Ergebnis in `zuteilung.md` fest.
  - **«Fertig, wenn …»** gilt erst als erfüllt, wenn es auch am realen Projekt trägt.
- **Befunde aus Christians Nutzung** gehen zuerst. Sie kommen in die Kontrollliste bzw. `nachbearbeitung.md` und werden vor neuen
  Wünschen behoben.
- **Echte Daten bleiben geschützt:**
  - **Auf W: nur lesen:** Agenten, Tests und Prüfskripte schreiben nie auf W:.
  - **Lokale Kopie für automatische Echt-Tests:** Sie lesen einen schreibgeschützten Stand unter `%USERPROFILE%\HLKS_CAD_testdaten\test_praxis`,
    den der Hauptlauf bei Bedarf nachführt; nie Repo, nie OneDrive.
  - **Bilder** aus dem Projekt bleiben lokal.
  - **Ändern im realen Projekt** tut nur Christian selbst über KICAD, oder der Hauptlauf auf ausdrücklichen Auftrag.
- **Cloud-Sitzungen** sehen das reale Projekt nicht. Deren Ergebnisse prüft der Hauptlauf am PC am realen Projekt, bevor `geprueft` gesetzt
  wird.

## Phase 0 – Gesamtschau und Plausibilisierung des Status quo
Christian, 22.10.2026: «als 0. machen wir noch vor den grundlagen eine gesamtschau/plausibilisierung, des status-quo.»

Ziel: ein ehrliches, überprüftes Bild dessen, was heute steht, bevor bereinigt wird. Vorschlag des Hauptlaufs:

1. **Bestandsaufnahme je Modul** (0a bis 7A, Feilarbeiten, XR-Prototyp):
   - was gebaut ist, gemessen an «Fertig, wenn …»;
   - was abgenommen ist, was unter Vorbehalt, was nur gebaut;
   - welche Tests es dazu gibt.
   - Quellen: `inventar.json` (Anleitung), Testberichte, `zuteilung.md`, `nachbearbeitung.md`, `vorrat_freitag.md`, Kontrollliste.
2. **Plausibilisierung am echten Projekt.** Jede Hauptfunktion einmal durchspielen, mit Test_praxis bzw. Bernstrasse 42 (nur lesend), statt nur
   mit dem Beispielprojekt. Was mit der Attrappe grün ist, aber am echten Projekt nicht trägt, kommt auf die Liste. Lehre vom 21.10.2026:
   Kopfpose und Schnittbereich fielen erst in der Brille auf.
3. **Daten und Quellen:**
   - Kataloge, Grundausstattung und Familien auf Widersprüche prüfen;
   - jeder Normwert mit Quelle (Norm, Ausgabe, Ziffer, Seite);
   - offene Konflikte auflisten.
4. **Technik:**
   - Architektur-Review (doppelte Wege, z. B. Browser-VR neben der App);
   - Leistung mit grossen IFC;
   - Sicherheit (Ports, Herkunftsschutz, KI-Einordnung, Fern-Debugging der Brille);
   - Stand der Tests (was nur mit Attrappen geprüft ist).
5. **Unabhängige Prüfung** durch `hlks-pruefer` (liest nur und schlägt vor) zu den Punkten 3 und 4.
6. **Ergebnis:**
   - **Gesamtschau-Bericht** (`berichte/gesamtschau-2026-10.md`): Stand je Modul, Befunde nach Schwere, Lücken zu «Fertig, wenn …», Risiken;
   - nachgeführte Kontrollliste;
   - **priorisierte Liste für Phase 1**, mit Christian festgelegt.

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
| **Quest App (Grundlage)** | Modul XR, Übertheme «Quest App» (`modul-xr.md`) | **Entscheid Christian 22.10.2026: «dies nehmen wir in Grundlagen. alle anderen systeme bauen darauf auf.»** Zuerst die Grundsatzentscheide (Technik, Verbindung, Verteilung), dann ein Prototyp, dann Version 1. Die Fachmodule (Phase 3) bauen ihre VR-Teile auf die App auf, z. B. Darstellung von Systemen und Berechnungen |
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
- Umfang der «Auswertung» in Phase 2.
- Die Termine im Factsheet (Meilensteine M3–M5) nachführen, sobald die Phasen geschätzt sind.
