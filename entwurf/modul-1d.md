# Modul 1d – HKAP-Universalschnittstelle: Spezifikation

Stand 18.10.2026 (Hauptlauf). Start auf Christians Zuruf vom 18.10.2026 («danach gleich alle 1er Module»; geplant
14.–18.12.2026). Grundlage: Konzept `entwurf/modul-1d-hkap.md` (17.10.2026) und die Bestandsaufnahme des HKAP-Codes vom
18.10.2026 (HKAP 1.5.1, `AufgabenProtokolle.Web`, nur gelesen). Dieses Dokument ist verbindlich; das Konzept bleibt als
Begründung.

## Ziel und Abnahme

Ein Austauschvertrag für alle Daten zwischen HKAP und Planungsprogrammen; KICAD ist der erste Partner.
**Fertig, wenn …** ein KICAD-Projekt ist mit einem Projekt der HKAP-Testkopie verknüpft; die Projektdaten stehen im
Plankopf; die Räume einer HKAP-Projektierung sind in KICAD übernommen, und eine in KICAD gezeichnete Fläche steht danach
in HKAP (mit Marke «aus KICAD»); ein Prüfbefund erscheint in HKAP als Pendenz; ein beidseitig geänderter Wert landet in
der Konfliktliste; alles mit Trockenlauf, in KICAD rückgängig machbar und über MCP bedienbar. Getestet nur gegen die
HKAP-Testkopie (Port 5098), nie gegen Livedaten.

## Befunde aus dem HKAP-Code (18.10.2026), die den Entwurf ändern

1. **Projektierungen haben keinen Konfliktschutz**, und der offene Editor speichert alle 2.5 s das ganze Objekt (PUT).
   Ein von KICAD geschriebener Raumwert würde vom offenen Editor überschrieben. → HKAP bekommt für Projektierungen die
   Versionsprüfung, die es für Pendenzen schon hat (`X-Version`/`If-Match`, 409 bei Konflikt); der Editor lädt bei 409 neu
   und meldet es.
2. **Die Raumfläche ist kein gespeicherter Wert** (aus PDF-Polygon und Massstab gerechnet), eine Raumhöhe gibt es nicht.
   → neues Feld am Raumstempel: `kicad` { id, flaecheM2, hoeheMm, stand } – die HKAP-Fläche bleibt, wie sie ist.
3. **SIA-Felder werden im Browser nachgeführt** (`siaFelder`): ein extern gesetzter Wert in einem SIA-Feld würde
   überschrieben. → Der Austausch entfernt das Feld aus `siaFelder` (wie der KI-Weg) und vermerkt die Herkunft.
4. **Pendenzen**: werden nur als ganze Liste geschrieben, haben aber `X-Version`/`If-Match`. Kein Herkunfts- und Raumfeld.
   → neue Felder `quelle` («KICAD»), `quelleId` (Schlüssel des Prüfbefunds), `raum` (Text); datensatzweises Schreiben
   über die Austauschstelle unter derselben Sperre.
5. **Kein CORS**: die KICAD-Oberfläche kann HKAP nicht direkt aufrufen → der Austausch läuft **Server zu Server** (KICAD-
   Kern ruft HKAP), die Oberfläche spricht nur mit KICAD.
6. Ids: Projekt `P-NNN` (Maximum + 1, kann wiederkehren), Projektierung `PJ-JJJJ-NNN`, Etage `E<n>`, Raum `<Etage>-S<n>`
   (nur mit PJ-Id eindeutig). Kürzel nicht eindeutig. Etagen-Koten sind live alle 0. «Bauherr» heisst in HKAP «Kunde»
   (kundeName) – ob gleichbedeutend, entscheidet Christian.
7. HKAP beendet sich 12 s nach dem Schliessen seines Browserfensters; HKAP hat keine automatischen Tests.

## Austauschvertrag (HKAP-Seite, Hauptlauf)

- `GET /api/austausch/katalog` – Fassung, Datenarten, je Feld: Name, Typ, Einheit, Richtung, führende Seite.
- `GET /api/austausch/{projektId}/{datenart}?projektierung=PJ-…` – Datensätze `{ id, version, felder }`; `version` =
  Prüfsumme des Datensatzes (16 Hex wie `X-Version`).
- `POST /api/austausch/{projektId}/{datenart}?projektierung=…&trockenlauf=true|false` – Liste `{ id?, version?, felder,
  quelle }`: anlegen (ohne id), ändern (mit erwarteter version), löschen (`loeschen: true`). Antwort je Datensatz: neu,
  geändert, gelöscht, **konflikt** (version passt nicht → aktueller Stand mit), abgelehnt (Grund). Schreiben unter der
  bestehenden Sperre, atomar je Aufruf, Historie im HKAP-Log («aus KICAD»).
- Datenarten 1d:

| Datenart | Felder | Richtung | führt (Annahme bis Christians Entscheid) |
| --- | --- | --- | --- |
| projekt | id, kuerzel, bezeichnung, adresse, plz, ort, kunde, phase | HKAP → KICAD | HKAP |
| geschosse | id (E…), name, praefix, koteMm, reihenfolge | beide | Kote KICAD; Name/Präfix wer zuerst |
| raeume | id (E…-S…), etage, raumNummer, bezeichnung, nutzung, personen, zuluft, abluft, heizleistung, kuehlleistung, tempWinter, tempSommer, co2Max, strangIds; neu `kicad` { id, flaecheM2, hoeheMm } | beide | Lage/Fläche/Höhe KICAD; Anforderungen (Personen, Temperaturen, CO₂, Luftmengen) HKAP; Nummer/Bezeichnung/Nutzung wer zuerst |
| pendenzen | id, aufgabe, verantwortlich, kategorie, termin, prioritaet, status, bemerkung, quelle, quelleId, raum | KICAD → HKAP, Status zurück | HKAP (Status), KICAD (Inhalt der eigenen) |

## KICAD-Seite

- Projektinformation `hkap` { projekt, projektierung, kuerzel, name, url } – Verknüpfung, Name/Kürzel als Gegenprüfung
  (Warnung bei Abweichung, Id-Wiederkehr).
- Element-Kennung `hkap` = «PJ-…/E1-S3» (Raum), «E1» mit PJ (Geschoss), Pendenz-Id.
- **Basisstand** je Datenart, Datensatz und Feld vom letzten Abgleich in der Projektablage (`austausch/hkap.json` im
  e_KICAD-Ordner, wird mit dem Projekt geteilt). Drei-Wege-Abgleich: KICAD-Wert, HKAP-Wert, Basis → unverändert / nur
  KICAD geändert → senden / nur HKAP geändert → übernehmen / beide → **Konflikt** (beide Fassungen, Mensch entscheidet).
  Führende Seite entscheidet nur, wohin ein Wert fliessen darf (ein HKAP-geführtes Feld wird nie gesendet).
- Befehle/Abfragen (je MCP-Werkzeug): `hkap_verbindung` (Abfrage: erreichbar? Fassung, Projekte), `hkap_verknuepfen`
  {projekt, projektierung}, `hkap_abgleich` (Abfrage: Vorschau neu/geändert/gelöscht/Konflikt je Datenart und Richtung),
  `hkap_uebernehmen` {datenarten?, konflikte: {schluessel: kicad|hkap}} (ein Paket in KICAD), `hkap_senden` {datenarten?,
  trockenlauf} (datensatzweise, mit Version), `hkap_befunde_senden` {schwere?} (Prüfbefunde → Pendenzen, Abgleich über
  quelleId, erledigte Befunde → Status erledigt vorschlagen, nicht löschen).
- Räume aus HKAP ohne Gegenstück in KICAD werden **nicht platzierte Räume** (Raum ohne Geometrie, Hinweis), bis sie
  gezeichnet oder einem gezeichneten Raum zugeordnet sind (`hkap_raum_zuordnen` {raum, hkapRaum}).
- HKAP nicht erreichbar → klare Meldung, nichts geht verloren (Basis bleibt).
- Oberfläche: Register «Verwalten» › «HKAP»: Verbindung, Verknüpfen (Projekt/Projektierung wählen), Abgleich-Vorschau
  (Tabelle je Datenart, Konflikte mit beiden Fassungen und Wahl), Übernehmen/Senden; Projektbrowser zeigt nicht
  platzierte Räume.

## Zuteilung

| Teil | Inhalt | Wer |
| --- | --- | --- |
| A | Spezifikation | Hauptlauf |
| H | HKAP: Austauschstelle, Versionsprüfung Projektierungen (Editor lädt bei 409 neu), neue Felder Raum `kicad` und Pendenz `quelle/quelleId/raum`, SIA-Feld-Regel, Marke «aus KICAD» in Raumdetail, Stempelkarte und Pendenzen; Test auf der Testkopie | **Hauptlauf** (HKAP-Code nur Hauptlauf) |
| B | KICAD: HKAP-Klient (HttpClient, Zeitlimits), Basisstand, Drei-Wege-Abgleich, Befehle/Abfragen, MCP; Tests mit Attrappe (lokaler Testserver im Test) und gegen die Testkopie (übersprungen, wenn nicht erreichbar) | hlks-kern (Synchronisation) |
| C | KICAD-Oberfläche HKAP-Dialog, nicht platzierte Räume im Projektbrowser | hlks-modul |
| D | Gesamttest gegen die Testkopie, Bericht | Hauptlauf |

## Annahmen für Christians Prüfliste

1. Führende Seite je Datenart und Feld wie in der Tabelle (Vorschlag aus dem Konzept).
2. «Kunde» in HKAP = Bauherr im Plankopf.
3. Projektierungen bekommen in HKAP die Versionsprüfung (Verhaltensänderung im Betrieb: der Editor lädt bei einem
   Konflikt neu statt zu überschreiben). Auslieferung erst nach deiner Freigabe.
4. Prüfbefunde ab Schwere «fehler» werden Pendenzen; «warnung»/«hinweis» nur auf Wunsch.
5. Basisstand liegt im Projektordner (`austausch/hkap.json`), nicht in HKAP.
6. HKAP-Anforderungen (Personen, Luftmengen, Leistungen, Temperaturen, CO₂) kommen als eigene Raum-Eigenschaften
   (personen, zuluft, abluft, heizleistung, kuehlleistung, tempWinter, tempSommer, co2Max), HKAP führt; tempWinter geht
   **nicht** auf `solltemperatur`.
7. HKAP kennt keine Geschosshöhe: neue Geschosse aus HKAP bekommen 3000 mm als Platzhalter (mit Hinweis), bis sie in
   KICAD gesetzt ist.
8. Geschosse werden beim ersten Abgleich über eindeutigen Namen bzw. Präfix zugeordnet, Räume nur über
   `hkap_raum_zuordnen` oder eine schon bekannte KICAD-Id.

## Befunde während des Baus

- Vertrag ergänzt: `GET /api/austausch/projekte` (Projekte mit Projektierungen); Ergebnisse kommen in der Reihenfolge der
  Eingänge (Zuordnung neuer Datensätze); «etage» führt KICAD.
- Pendenzen-`quelleId` enthält die KICAD-Projektkennung, damit zwei KICAD-Projekte am selben HKAP-Projekt einander
  nicht die Pendenzen als erledigt vorschlagen.
- **Datenbefund HKAP (Livedaten):** bei P-005 sind PLZ und Ort vertauscht («Bern» / «3013») – in HKAP korrigieren.
