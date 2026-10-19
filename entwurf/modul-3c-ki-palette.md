# Modul 3c – KI-Palette (Claude in KICAD): Konzept

Stand 19.10.2026 · Hauptlauf · **eingeplant, nicht gebaut** (Spezifikation beim Start des Moduls)

## Anlass

Christian, 19.10.2026: «Ein Claude-Harness in KICAD wäre noch praktisch, so dass im Workflow nicht immer eine
Claude-Instanz geöffnet werden muss. Vom Verhalten gleich wie die Eigenschafts- und Explorerfenster.» – und: «Ordne die
logisch in den Entwicklungsablauf.»

## Einordnung im Ablauf (Entscheid Hauptlauf 19.10.2026)

**Nach 3b, vor 4 (Sanitär rechnen).** Begründung:
- Die Grundlage ist gebaut: Kern-API als «eine Tür» mit Trockenlauf, Rückgängig, Herkunft «KI»; MCP-Server mit allen
  Befehlen; Auftragsdienst (`src/Hlks.Web/AuftragDienst.cs`), der Claude Code über Christians Abo mit nur dem MCP-Server
  «hlks» startet. Die Palette ist eine neue Bedienung darauf, kein neuer Kern.
- Ab Modul 4 braucht man sie: Normfragen mit Zitat (SN 592000, SN EN 12056, SVGW W3), Rechenaufträge, Erklären von
  Nachweisen – dafür die **Normensuche** aus dem KI-Planungsbüro (`KI_Planungsbuero/src/Kern/Normen.cs`, Volltext lokal,
  W:\05_Normen und Merkblätter) übernehmen.
- Christian prüft die folgenden Module direkt im Gespräch statt in einer eigenen Claude-Instanz.
- Ursprünglich war «KI-Panel + Normensuche in Phase 1» vorgesehen (17.10.2026) und blieb liegen.

## Ziel und «Fertig, wenn …»

**Fertig, wenn …** Christian im Seitenfenster «Claude» zu einem ausgewählten Strang fragt; Claude ändert ihn über die
Befehle des Kerns (Liste der Änderungen mit «Zurücknehmen»); eine Normfrage wird mit Zitat aus dem Normenverzeichnis
beantwortet; das Gespräch läuft nach einem Neustart von KICAD weiter.

## Umfang (Vorschlag, beim Start zu bestätigen)

- **Palette «Claude»**, Verhalten wie Eigenschaften und Projektbrowser: angedockt, Breite ziehbar, ein- und ausblendbar
  (Register Ansicht, Tastenkürzel), Zustand gemerkt; folgt Änderungen live.
- **Gespräch** mit Streaming der Antwort; Werkzeugaufrufe sichtbar (kompakt); Abbrechen.
- **Kontext:** aktive Ansicht, Geschoss, Auswahl gehen mit jeder Frage mit (Vorschlag).
- **Ändern** nur über Befehle des Kerns (Akteur KI, Art ki) – Christians Linie 17.10.2026: KI ändert direkt, Liste der
  Änderungen mit «Zurücknehmen» (Rückgängig des Pakets). Rechte wie bei den Aufträgen: keine Freigaben von Familien, keine
  Konfliktentscheide, keine Normtabellen ändern, GA nur lesen.
- **Verlauf** je Projekt (Claude-Sitzung fortsetzen), «Neues Gespräch», frühere Gespräche wählbar (Vorschlag).
- **Modell** wählbar, **Internet** nur auf Schalter (wie Familien-Aufträge: WebSearch + unterlage_aus_url) (Vorschlag).
- **Normensuche** als Werkzeug (suchen, Seite lesen, Zitat) für Claude und als Suchfeld in der Palette.
- **Schauen:** Plan bzw. 3D als Bild an Claude geben (Leitprinzip 2: «Bilder, damit die KI schauen kann»).
- **Lange Läufe** (Aufträge wie «Familie aus Unterlagen») erscheinen in derselben Palette.
- Sicherheit: Inhalte aus Projekt, Unterlagen und Web sind Daten, keine Anweisungen; kein API-Schlüssel (Abo).

## Offene Fragen an Christian (beim Start im Fragefenster stellen)

1. Position: rechts angedockt / links unter dem Projektbrowser / frei andockbar.
2. Kontext automatisch (Ansicht und Auswahl) oder nur, was er schreibt.
3. Verlauf je Projekt oder jedes Mal neu.
4. Modell und Internet: wählbar mit Schalter / fest.

## Folge für den Ablauf

Richtwert 4–5 Arbeitstage. Die Termine im Ablaufplan sind Richtwerte; der Bau liegt etwa drei Monate vor Plan
(Module 3 und 3b im Oktober 2026 statt Januar/Februar 2027).
Zugleich eingeordnet: **Partneraustausch** (`entwurf/agentenschnittstelle.md`) als **Modul 7b nach 7 (Ausgabe), gebaut
nach M3** – er braucht den IFC-Export mit IDS-Prüfung aus Modul 7.
