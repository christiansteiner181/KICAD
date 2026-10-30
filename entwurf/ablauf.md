# KICAD – Ablauf in Phasen (0 bis 4)

Stand 30.10.2026 · Hauptlauf. Nachtrag 30.10.2026: Vorhaben «Erzeugendes HLKS-CAD» aufgenommen (Abschnitt unten). Entscheid Christian Steiner, 22.10.2026:

> «wir planen den ablauf so: 1. Grundmodule bereinigen, Nutzung, Anzeigen, Verhalten, Logik, Familien usw. 2. Gebäudeerfassung und
> Auswertung 3. Fachmodule, spez. Berechnungen, Systemerkennung, Darstellungen usw.»

Der Ablauf ersetzt die bisherige Reihenfolge nach Modulnummern aus dem Factsheet vom 17.10.2026. Module, «Fertig, wenn …» und Abnahme
bleiben. Neu ist, dass zuerst das Bestehende bereinigt wird, bevor neue Fachmodule kommen. Modul 5 (Heizung) bleibt pausiert, bis Phase 3
beginnt.

## Nutzung von KICAD (Stand 22.10.2026)
Christian, 22.10.2026: «momentan gibt es nur testprojekte. die software wird nur durch uns zu entwicklungsprozess genutzt.»

- KICAD ist nicht im produktiven Einsatz. Es gibt nur Testprojekte; genutzt wird es von Christian und vom Hauptlauf im
  Entwicklungsprozess.
- **Folgerung:** Datenmodell, Kataloge, Formate und Abläufe dürfen sich in Phase 0 und 1 ändern, auch grundlegend. Rücksicht auf fremde
  Bestände oder Migrationen für Dritte ist nicht nötig.
  - Die Testprojekte, allen voran «Test_praxis», hält der Hauptlauf lauffähig: entweder mit einer einfachen Nachführung oder indem er sie
    neu aufbaut, nach Absprache mit Christian.
- Die Schutzregeln bleiben: W: nur lesen für Agenten und Tests, echte Unterlagen (IFC, Pläne, Bilder) nur lokal, HKAP nur gegen die
  Testkopie. «Test_praxis» liegt in der echten Ablagestruktur, und die IFC stammen aus echten Projekten.

## Grundsatz: Bauen parallel mit einem realen Projekt (seit 22.10.2026)
«Real» heisst hier: ein Testprojekt mit echten Unterlagen und echter Grösse, nicht das Beispielprojekt.
Christian, 22.10.2026: «ab sofort bauen wir parallel mit einem realen projekt, wie ich bereits begonnen habe zu testen.»

- **Reales Projekt:** das Projekt, mit dem Christian seit dem 21.10.2026 testet: «Test_praxis» unter
  `W:\02_Projektdateien Objekte\Bolligen_HK_Intern\03_Pläne und Modelle`, mit Architekten-IFC als Unterlage. Von Christian bestätigt am
  22.10.2026. Ein Wechsel oder ein zweites Projekt legt Christian fest.
  - **Neu aufgesetzt 30.10.2026** (Christian: «wir setzten das testprojekt neu auf. nur mit IFC und IFC nachbau. so dass es eine reine
    Architekturgrundlage ist.»): nur Architekten-IFC «440_Bernstrasse 42 Ausführung» (Archicad 27) mit `ifc_uebernehmen` (Architektur +
    Räume, Hintergrund), Grundrisse je Geschoss 1:50 und 3D; keine DWG, keine Gebäudetechnik. **Auf W: ersetzt 30.10.2026 16:45**; das alte
    Projekt (141 Pakete) liegt im selben Ordner unter `_KICAD_alt_20261030` (`projekt.json.alt`, damit es nicht als Projekt erscheint). Lokal:
    `test_praxis` schreibgeschützte Kopie für Echt-Tests, `test_praxis_arbeit` beschreibbar, alte Stände `*_alt_20261030` (Stand in `test_praxis_STAND.txt`).
    Gebäudetechnik kommt nur noch über die Bauaufgaben hinein, die sie prüfen.
- **Jede Bauaufgabe endet mit einer Prüfung am realen Projekt.** Grüne Tests mit Beispielprojekt und Attrappen reichen nicht mehr. Der
  Hauptlauf spielt die geänderte Funktion am realen Projekt durch (Desktop bzw. Brille) und hält das Ergebnis in `zuteilung.md` fest.
  - **«Fertig, wenn …»** gilt erst als erfüllt, wenn es auch am realen Projekt trägt.
- **Befunde aus Christians Nutzung** gehen zuerst. Sie kommen in die Kontrollliste bzw. `nachbearbeitung.md` und werden vor neuen
  Wünschen behoben.
- **Ausnahme Test_praxis (Christian, 23.10.2026: «dies ist nur ein testprojekt. es kann ohne einschränkung genutzt werden.»):** Für
  Test_praxis gelten die Schutzregeln unten nicht.
  - Lesen und Schreiben sind erlaubt, auch im Projektordner auf W:.
  - Arbeitskopien und Änderungen sind erlaubt.
  - Zahlen, Geometrie und Bilder dürfen in Testausgaben stehen.
  - Nicht ins Repository (es geht nach GitHub, und es braucht die Dateien nicht).
  - Andere Projekte, z. B. die Bernstrasse, bleiben geschützt.
- **Echte Daten bleiben geschützt** (alle Projekte ausser Test_praxis):
  - **Auf W: nur lesen:** Agenten, Tests und Prüfskripte schreiben nie auf W:.
  - **Lokale Kopie für automatische Echt-Tests:** Sie lesen einen schreibgeschützten Stand unter `%USERPROFILE%\HLKS_CAD_testdaten\test_praxis`,
    den der Hauptlauf bei Bedarf nachführt; nie Repo, nie OneDrive. Erste Kopie 22.10.2026 09:45: 99 Dateien, 230 MB, ohne
    `sperren`; Stand in `test_praxis_STAND.txt` daneben.
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

**Reihenfolge freigegeben 30.10.2026** (Christian: «Ja, freigeben», Vorschlag aus `berichte/gesamtschau-2026-10.md`):
1 Sofortkorrekturen (P7 Trockenlauf/unbekannte Parameter, Altlasten, Arbeitskopien aus OneDrive, `inventar.json` ohne XR) ·
2 Familien offline + Grundausstattung abschliessen · 3 Leistung Kern/Oberfläche (H1, H2, M2, M3) · 4 Protokoll B + Dateiausgaben als Befehle (M5) ·
5 offene Abnahmen 3, 3b, 3c, 4A, 7A, 7B, G am neuen Test_praxis · 6 Datenqualität Kataloge · 7 Kontrollliste neu · 8 Rest (M1, G4, Quelltext-Tests,
Zweig Modul 5 neu aufsetzen). Die Tabelle unten bleibt die Sammlung der offenen Punkte je Bereich.

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
| **Kern tauglich für Varianten** (Vorarbeit Vorhaben, Gesamtschau 30.10.) | Kern, Web | H1 Befehl ohne volle Modellkopie und dreifache Prüfungen (Prüfungen inkrementell, Befunde je Stand zwischenspeichern); H2 Oberfläche lädt nur Betroffenes; P7 Trockenlauf auch im Körper, unbekannte Parameter ablehnen; M5 Dateiausgaben als Befehle mit Paket und Trockenlauf |
| **Protokoll im Kern** (Baustein B) | 0a | optionale Felder `begruendung` und `prozessschritt`/`situation` am Paket (Durchreichen in `Projekt.cs`, Schema `hlks-modell.schema.json` nachführen); jetzt, solange Formate frei sind |
| **Familien offline** | 2 | lokaler Zwischenstand der Bibliothek W:; Grundausstattung abschliessen (Leser 3, Entscheide K3/R1/R2, R7 d32) und in die Bibliothek bzw. mitgeliefert – ohne Familien kann weder Mensch noch Generator eine Leitung legen |

## Phase 2 – Gebäudeerfassung und Auswertung
Das Gebäude kommt vollständig und richtig ins Modell: Räume, Hülle, Anforderungen. Es lässt sich auswerten.

| Bereich | Module | Inhalt |
| --- | --- | --- |
| Grundlagen, Räume, IFC-Übertrag, openBIM | 1b | Architekten-IFC sauber übernehmen (Türen, Treppen, Räume, Decken), Nachbau und Original, Raumerkennung |
| Gebäudehülle | 1c | Hülle, Raumgrenzen, Bauteile mit Aufbau |
| HKAP-Schnittstelle | 1d | Raumbuch, Anforderungen (Personen, Temperaturen) aus HKAP |
| Auswertung | neu | Flächen, Volumen, Hüllflächen, Raumlisten als Grundlage der Fachmodule. Den Umfang legt Christian fest; Normwerte nur mit Quelle |
| **Räume vollständig aus IFC ohne IfcSpace** (Voraussetzung Generator) | 1b | Test_praxis 30.10.: 1.OG 53 m² Raumfläche bei ~190 m² Geschoss; Wände/Fenster im Hintergrund (Proxy-Wirte, Wände ohne Achse) schliessen die Umrisse nicht. Fertig, wenn die Räume von Test_praxis die Geschossfläche abdecken und lichte Höhen haben |
| **Zonen und Hindernisse für die Leitungsführung** (Vorarbeit D) | 1b, 1c, 3b | Kollision für Prüfkörper ohne Übernahme (heute nur Befehl auf Elementen); bevorzugte Zonen (Korridor, abgehängte Decke, Schacht) als Raum- bzw. Bauteilangabe – Herkunft legt Christian fest (Frage 7 im Vorhaben) |

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
| **12 (neu)** | **Generator und Bewertung** (Bausteine D, E): Varianten der Leitungsführung, Kennzahlen, gewichteter Vergleich, Übernahme mit einem Befehl | erster Schritt G1 (Lüftungskanäle in einem Geschoss, ohne Normwerte) gleich zu Beginn von Phase 3; Druckverlust in E erst mit Modul 6 bzw. Normen |
| F | **Prüfstand**: Agent bzw. Generator löst Mustersituationen, Vergleich mit Referenz; Ausbau `ki-test` | sobald die ersten Mustersituationen (A) und das Protokoll (B) stehen |

## Phase 4 – Agenten und Spezialisierung (Vorhaben, Bausteine F/G)
Erster Fachagent (Lüftung, starkes Modell über MCP) am Prüfstand messen und nachbessern; gleichförmige Schritte (Situationsarten 1, 5, 8) an
kleine lokale Modelle (Baustein G, Hardware offen); Schattenbetrieb in einem echten Projekt (nur vergleichen); Ausbau auf weitere Sparten und
Gewerke. Reihenfolge nach `vorhaben-erzeugendes-cad.md`, Abschnitt «Phasen des Vorhabens» 4–8.

## Vorhaben «Erzeugendes HLKS-CAD» (aufgenommen 30.10.2026)
Christian, 30.10.2026: «wir nehmen das komplettpaket in den bauplan. bite prüfe das technisch, und passe den plan nötigenfalls an.»
Inhalt und Begriffe: `entwurf/vorhaben-erzeugendes-cad.md`. Technische Prüfung: `berichte/gesamtschau-2026-10/4_technik.md` Abschnitt (d).

**Technische Prüfung (30.10.2026):** Das Rückgrat trägt – ein Befehlssystem ohne Umgehungen, Trockenlauf ohne Seiteneffekte für Befehle,
`Routing.Planen` nimmt einen Verlauf als reine Eingabe, Prüfbefunde je Variante abfragbar, Paket lässt sich ohne Formatbruch erweitern.
Vor dem Generator fehlen:
1. **Kosten je Trockenlauf** (H1): heute volle Modellkopie und dreimal alle Prüfungen – für hunderte Varianten am echten Projekt zu teuer → Phase 1.
2. **Trockenlauf über HTTP** (P7) und für Dateiausgaben (M5) → Phase 1.
3. **Familien offline** (P2): ohne Serie keine Leitung → Phase 1.
4. **Räume und Zonen** (P3): Wegsuche braucht geschlossene Räume, lichte Höhen und bevorzugte Zonen → Phase 2.
5. **Kollision für Prüfkörper ohne Übernahme** und ein **Kennzahlen-Objekt** am `RoutingPlan` (Länge, Formteile, Befunde) → Phase 2 bzw. Beginn Modul 12.
6. **Normen** für Rechenkerne (C) und Druckverlust in der Bewertung (E) → Beschaffung durch Christian, unverändert offen.

**Einordnung:**
- **A Datengrundlage** läuft ab sofort parallel als Fachkopfarbeit (Christian mit Claude im Chat); keine Software nötig, bis das Schema einer
  Mustersituation steht.
- **B Protokoll** in Phase 1 (Formate sind jetzt noch frei).
- **D/E** als eigenes **Modul 12** statt in Modul 6: Leitungsführung betrifft Sanitär, Heizung und Lüftung gleich; Modul 6 liefert später den
  Druckverlust als Kriterium. G1 Lüftung bleibt erster Schritt, weil er ohne Normwerte auskommt.
- **C** Rechenkerne bleiben Phase 3 (Module 4B, 5, 6) und hängen an den Normen.
- **F** ab erster Mustersituation, **G** und Agenten in Phase 4.
- Bei jeder Arbeit an Routing, Prüfungen, Paketen und Beispielprojekt: Andockstellen nicht verschlechtern (Trockenlauf, Verlauf als Eingabe,
  Kennzahlen abfragbar).

## Offen
- Umfang der «Auswertung» in Phase 2.
- Die Termine im Factsheet (Meilensteine M3–M5) nachführen, sobald die Phasen geschätzt sind.
