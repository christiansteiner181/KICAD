# Kälte ist Teil der Heizung – Entscheid und Umbau

Stand 20.10.2026 · Hauptlauf

## Entscheid

Christian, 20.10.2026 (nach dem Entscheid «Fach K = Koordination»): «Kälte ist Teil der Heizung mit anderem Medium.»

Folge für KICAD: **Kälte ist kein eigenes Gewerk.** Kältesysteme und ihre Elemente gehören zum Gewerk **Heizung**; sie
unterscheiden sich nur über Medium und Systemtyp (eigene Medienfarben bleiben).

## Medien (Christian, 20.10.2026, Fragefenster und Nachricht)

«Medien für Heizung VL, RL, VL-Glyk, RL-Glyk» · «Medien Kälte VL, RL, VL-Glyk, RL-Glyk» · Kennung: «den Präfix KAE und HZ
beibehalten» · Kühlwasser «je nach Füllung» · Sole «VL-Glyk/RL-Glyk» · Farbe: «KAE VL = cyan, RL = magenta, Apparate = blau –
findest du schon in HKAP Prinzipschema».

| Kennung | Name | Gewerk | Farbe | Herkunft |
| --- | --- | --- | --- | --- |
| HZ-VL | Heizung Vorlauf | heizung | #FF0000 | HKAP `schema_systeme.json` HZ_VL |
| HZ-RL | Heizung Rücklauf | heizung | #0000FF | HKAP HZ_RL |
| HZ-VL-Glyk | Heizung Vorlauf Glykol | heizung | wie HZ-VL (Annahme, HKAP kennt kein Glykol) | – |
| HZ-RL-Glyk | Heizung Rücklauf Glykol | heizung | wie HZ-RL (Annahme) | – |
| KAE-VL | Kälte Vorlauf | heizung | #00CCCC | HKAP KAE_VL |
| KAE-RL | Kälte Rücklauf | heizung | #FF00CC | HKAP KAE_RL |
| KAE-VL-Glyk | Kälte Vorlauf Glykol | heizung | wie KAE-VL (Annahme) | – |
| KAE-RL-Glyk | Kälte Rücklauf Glykol | heizung | wie KAE-RL (Annahme) | – |

- Strichart: wie bisher (Vorlauf voll, Rücklauf gestrichelt); Glykol gleich wie die Grundleitung, Unterscheidung über Kennung
  und Beschriftung (Annahme, Christian bestätigt).
- Apparate: Hinterlegung Kälte #CCE0FF (HKAP `FlaecheApparatKaelte`, «blau»), Heizung #CCFFE0 (HKAP `FlaecheApparat`), wo
  KICAD Apparate hinterlegt (Schemas, später Prinzipschema Modul 5); die Strichfarbe eines Apparats folgt dem Medium seines Kreises.
- **Alias für alte Kennungen** (Projekte, Familien, Leitungsserien, Befehle, IFC-Zuordnung, HKAP-Abgleich): KUE-VL → KAE-VL,
  KUE-RL → KAE-RL, SOLE-VL → HZ-VL-Glyk, SOLE-RL → HZ-RL-Glyk; HZ-VL/HZ-RL bleiben. Beim Lesen umgeschrieben, Hinweis statt
  Fehler; HKAP-Schlüssel mit Unterstrich (HZ_VL, KAE_RL) bilden auf die Kennungen mit Bindestrich ab.
- Systemtypen in `systemtypen.json`: je Medium ein Systemtyp (Heizung/Kälte, Wasser/Glykol), Gewerk `heizung`.

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
