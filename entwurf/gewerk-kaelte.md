# Kälte ist Teil der Heizung – Entscheid und Umbau

Stand 20.10.2026 · Hauptlauf

## Entscheid

Christian, 20.10.2026 (nach dem Entscheid «Fach K = Koordination»): «Kälte ist Teil der Heizung mit anderem Medium.»

Folge für KICAD: **Kälte ist kein eigenes Gewerk.** Kältesysteme und ihre Elemente gehören zum Gewerk **Heizung**; sie
unterscheiden sich nur über Medium und Systemtyp (eigene Medienfarben bleiben).

## Umbau (41 Fundstellen in 22 Dateien, Stand 5a75cbc)

1. **Gewerk-Liste:** `kaelte` entfällt als Gewerk (Kataloge `gewerke.json`, `klassen.json` – Auswahllisten wie
   `Ansicht.gewerke`, `Planblatt.gewerk`, `Plansatz.gewerk` –, Kern `PlanGewerk`, Oberfläche). **Alias:** ältere Werte
   `kaelte` (in Projekten, Befehlen, MCP, BCF) werden als `heizung` gelesen, mit Hinweis «Kälte gehört zur Heizung»; nie Fehler.
2. **Zuordnung:** Medien und Systemtypen der Kälte (`medien.json`, `systemtypen.json`) bleiben, ihr Gewerk ist `heizung`.
   Alles, was das Gewerk aus Medium bzw. Systemtyp ableitet (Aussparungs-Kennzeichnung, Kollisionsvorgaben, BCF-Format,
   IFC-Systeme, HKAP-Stränge, Bemassungseinheit je Gewerk, Projektbrowser-Gliederung, Familienbibliothek-Filter,
   Koordination), liefert für Kälte `heizung`.
3. **Pläne:** Fachbuchstabe **H** (Plannummern-Vorschlag, `PlanGewerk`), Kälte-Blätter im automatischen Plansatz
   **Heizung**; Koordinationsplan zeichnet Kälteleitungen in der Koordinationsfarbe der Heizung; in Fachplänen bleibt die
   Medienfarbe.
4. **Projektbrowser:** Gliederung «Heizung» zeigt die Kältesysteme; ein eigener Block «Kälte» entfällt. Benutzereinstellungen
   mit `kaelte` werden wie `heizung` gelesen.
5. **Tests:** bestehende Tests mit Gewerk Kälte auf Heizung bzw. auf den Alias umstellen; neue Tests für den Alias (Projekt mit
   altem Wert `kaelte`, Befehl mit `gewerk: kaelte`, BCF), Plannummer H für ein Kälteblatt, Koordinationsfarbe.

**Fertig, wenn** im Beispielprojekt ein Kältesystem unter «Heizung» im Projektbrowser steht, sein Planblatt «H…» heisst und im
Plansatz Heizung liegt, und ein altes Projekt mit `kaelte` ohne Fehler öffnet.
