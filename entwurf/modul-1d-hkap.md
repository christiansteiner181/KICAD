# Modul 1d – HKAP-Universalschnittstelle: Konzept

Stand 17.10.2026 (Hauptlauf). Erweiterung Christian 17.10.2026: «Nimm auch noch eine Universalschnittstelle zu HKAP
auf.» Grundlage für die Spezifikation von Modul 1d. Bestandsaufnahme HKAP am Code vom 17.10.2026 (HKAP 1.5.1,
`AufgabenProtokolle.Web`), Grundsätze aus dem Konzept HKAP ↔ Revit vom 27.08.2026.

## Ziel

**Ein Austauschvertrag für alle Daten zwischen HKAP und den Planungsprogrammen** – nicht je Datenart eine eigene
Schnittstelle. KICAD ist der erste Partner; HK-AI-BIM (Revit) und andere Programme oder die KI benutzen später
denselben Vertrag. Neue Datenart = ein Eintrag im Austauschkatalog und eine Abbildung, kein neuer Transport.

## Grundsätze (wie HKAP ↔ Revit, 27.08.2026)

- **Zusammenhang und Werte, nicht Geometrie.** HKAP Projektieren zeigt den Anlagezusammenhang bewusst schematisch
  (Raumpolygone auf dem PDF-Grundriss). Geometrie führt KICAD; nach HKAP gehen Werte wie die Raumfläche.
- **Keine feste Reihenfolge.** Mal entsteht ein Projekt zuerst in HKAP, mal in KICAD. Wer führt, wird **je Datenart
  und Feld** festgelegt (Tabelle unten, Entscheid Christian).
- **Drei-Wege-Abgleich:** KICAD-Stand, HKAP-Stand und der **Basisstand vom letzten Abgleich**. Ohne Basis wären
  Löschungen nicht von «war noch nie da» zu unterscheiden.
- **Kein automatisches Mischen:** Wurde ein Wert auf beiden Seiten geändert, kommt er mit beiden Fassungen in eine
  **Konfliktliste**; der Mensch entscheidet.
- **Vorschau zuerst:** Jeder Abgleich läuft als Trockenlauf (neu / geändert / gelöscht / Konflikt), dann übernehmen.
  In KICAD ein Paket je Übernahme (rückgängig machbar), in HKAP datensatzweise mit Versionsprüfung.
- **Herkunft je Wert:** übernommene Werte tragen «hkap» bzw. «kicad» mit Datensatz, Feld und Zeitpunkt; «Warum?»
  zeigt sie (Leitprinzip 7).
- **Der Abgleich ist ein eigenes Stück**, nicht in die Fachmodule verwoben; die Module melden nur ihre Datenarten an.

## Identität und Verknüpfung

- Das KICAD-Projekt trägt in der Projektinformation die Verknüpfung `hkap { projekt: "P-005", projektierung:
  "PJ-2026-003" }` plus Projektname und Kürzel als Gegenprüfung. **Befund:** HKAP vergibt `P-NNN` als Maximum + 1 –
  nach dem Löschen des höchsten Projekts kann eine Id wiederkehren. Stimmen Name/Kürzel nicht mehr, warnt KICAD.
- Jedes ausgetauschte Element trägt beide Kennungen: KICAD-GUID und Bezug `hkap` (z. B. `PJ-2026-003/E1-S3`).
  **Befund:** HKAP-Raum-Ids (`E1-S3`) sind nur zusammen mit der Projektierungs-Id eindeutig (verbundene Kopien).
- **Namen sind nie Schlüssel.** Befund: Aufgaben, Protokolle, Ausschreibungen u. a. kennen ihr Projekt in HKAP nur über
  den Namen; für den Austausch zählen nur Ids.

## Transport (Vorschlag)

- Beide Programme laufen lokal: HKAP auf `localhost:5013`, KICAD auf `localhost:5130`. Der Austausch läuft über REST.
- **HKAP bekommt eine allgemeine Austauschstelle** (heute 403 Endpunkte, keiner allgemein):
  - `GET /api/austausch/katalog` – Datenarten, Felder, Einheiten, Richtung, führende Seite;
  - `GET /api/austausch/{projektId}/{datenart}` – Datensätze mit Version;
  - `POST /api/austausch/{projektId}/{datenart}?trockenlauf=true|false` – Änderungen **datensatzweise** mit
    erwarteter Version (Konflikt statt Überschreiben, wie heute `If-Match` bei Protokollen und Pendenzen).
  Nötig, weil HKAP heute Pendenzen nur als ganze Liste und Projektierungen nur als ganzes Objekt schreibt.
- **KICAD:** Befehle und Abfragen `hkap_verknuepfen`, `hkap_abgleich` (Trockenlauf → Vorschau), `hkap_uebernehmen`,
  `hkap_senden`, je als MCP-Werkzeug – damit kann die KI dasselbe (Leitprinzip 2).
- HKAP nicht erreichbar (nicht gestartet; HKAP beendet sich, wenn sein Browserfenster schliesst): klare Meldung, der
  Abgleich bleibt offen, nichts geht verloren, weil jede Seite ihren Stand führt.
- Der Vertrag hängt nicht am Transport: Kommt die HKAP-Mehrbenutzerablage auf `W:\HKAP\` (Entwurf 16.10.2026), kann
  derselbe Austausch auch dateibasiert laufen.

## Datenarten (Vorschlag; «führt» entscheidet Christian)

| Datenart | HKAP heute | KICAD | Richtung | führt (Vorschlag) | Modul |
| --- | --- | --- | --- | --- | --- |
| Projekt | Projekt + Stammdaten: Id, Kürzel, Bezeichnung, Adresse, Bauherr (`kundeName`), Phase | Projektinformation, Plankopf | HKAP → KICAD | HKAP | 1d |
| Geschosse | Etagen der Projektierung (Name, Präfix, Kote) | Geschoss | beide | Kote KICAD, Name wer zuerst | 1d |
| Räume | Stempel: Nummer, Bezeichnung, Nutzung, Personen, Zu-/Abluft, Heiz-/Kühlleistung, Temperaturen, CO₂, Stränge | Raum: Polygon, Fläche, Höhe, Geschoss, Nutzung | beide | Lage/Fläche KICAD; Anforderungen (Personen, Temperaturen, CO₂) HKAP; Nummer/Bezeichnung wer zuerst | 1d |
| Pendenzen | Pendenzen im Projekt | Prüfbefunde, KI-Hinweise | KICAD → HKAP, Status zurück | HKAP | 1d |
| Stränge, Systeme | Stränge (`ST1`, «L_01», Medium als Freitext), Strangzuordnung der Räume, Geräte im Prinzipschema | Systemgraph (Anlage, System, Strang), Schema | beide | Zusammenhang wer zuerst; Medium/Systemtyp nach KICAD-Katalog | 3 |
| Rechenergebnisse | Stempelwerte (Heizleistung, Zu-/Abluft) | Rechenkerne Sanitär, Heizung, Lüftung | KICAD → HKAP | KICAD, mit Herkunft | 4–6 |
| Mengen, Apparate | KV (BKP-Gruppe → Position → Zeile mit Quelle), Ausschreibung | Mengen, Apparateliste | KICAD → HKAP | KICAD | 7 |
| Pläne | Upload in den Projektordner (eine Planliste fehlt in HKAP) | Planblätter als PDF | KICAD → Projektordner/HKAP | KICAD | 7 |
| Datenpunkte | – | Funktionsgraph | KICAD → HKAP (später) | KICAD | 8 |

Räume aus HKAP ohne Gegenstück in KICAD erscheinen dort als **noch nicht platzierte Räume** (wie Elemente, die nur im
Schema stehen), bis sie gezeichnet oder einem gezeichneten Raum zugeordnet sind. HKAPs KV holt Mengen heute schon aus
Prinzipschemas und Projektieren (`KvQuellen`); KICAD wird dort eine weitere Mengenquelle.

## HKAP-Seite

Änderungen am HKAP-Code macht der Hauptlauf selbst (CLAUDE.md: schreibende Zugriffe auf HKAP nur Hauptlauf).
Getestet wird gegen die bestehende **HKAP-Testkopie** (`_build\hkap_testkopie.ps1`, Port 5098: Datenkopie mit
geleerten Projektordnern, SMTP, Webhooks, Outlook-Handler und stillgelegter KI-Warteschlange), nie gegen die Livedaten
in `OneDrive\HK_AufgabenProtokolle\app\data`; nach jedem HKAP-Neubau bekommt Christian den Link auf die Testkopie.
Ausgeliefert wird erst nach Christians Freigabe (HKAP ist im Betrieb).
Umfang: Austauschstelle, datensatzweises Schreiben für Pendenzen und Räume, Herkunftsfeld, Anzeige «aus KICAD» bzw.
«an KICAD gesendet» in Projektieren und Pendenzen.

**Fertig, wenn …** Ein KICAD-Projekt ist mit einem HKAP-Testprojekt verknüpft; die Projektdaten stehen im Plankopf;
die Räume einer HKAP-Projektierung sind in KICAD übernommen, und eine in KICAD gezeichnete Fläche steht danach in
HKAP; ein Prüfbefund erscheint in HKAP als Pendenz; ein beidseitig geänderter Wert landet in der Konfliktliste; alles
mit Trockenlauf, in KICAD rückgängig machbar und von der KI über MCP bedienbar.

**Voraussetzungen:** bis 14.12.2026 Christians Entscheid, wer je Datenart führt (Tabelle oben), und ein HKAP-
Testprojekt (Kopie, keine Livedaten); Freigabe der HKAP-Änderungen vor der Auslieferung.

**Aufwand:** Modul 1d = 5 Arbeitstage (KICAD-Seite 2, HKAP-Seite 2, Abgleich und Tests 1), 14.–18.12.2026, M2 neu
18.12.2026. Spätere Datenarten im jeweiligen Modul: Stränge/Schema + 1 Tag in Modul 3, Mengen/Pläne + 1 Tag in Modul 7,
Rechenergebnisse im Aufwand der Module 4–6 enthalten.
