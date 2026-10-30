# KICAD – Vorhaben «Erzeugendes HLKS-CAD»: Datengrundlage, Mustersituationen, Generator

Stand 30.10.2026 · Chat-Sitzung mit Christian (claude.ai), Abgleich mit dem Repository auf Commit `7edee1a` · **Vorhaben, nicht
beauftragt, nicht gebaut**

## Zweck dieses Dokuments

Dieses Dokument gibt Claude-Code-Sitzungen den Zusammenhang eines grösseren Vorhabens, das Christian am 30.10.2026 im Chat entwickelt
hat. Es ist **Kontext, kein Bauauftrag.**

- Nichts daraus bauen, solange Christian keinen Auftrag gibt.
- `entwurf/ablauf.md` (Phasen 0–3) bleibt massgebend. Dieses Vorhaben ändert die Reihenfolge dort nicht.
- Bei jedem Bauauftrag, der Routing, Prüfungen, Pakete, Beispielprojekt oder Modul 6 berührt, dieses Dokument mitlesen und
  darauf achten, dass nichts verbaut wird, was das Vorhaben später braucht.

Entscheide von Christian sind als **Entscheid** markiert, Vorschläge aus dem Chat als **Vorschlag** (nicht entschieden).

## Anlass (Christian, 30.10.2026)

> «es geht darum unseren planungsprozess zu automatisieren. also, so dass ein modelle alle schritte autonom machen kann. dabei sollen
> unterschiedliche arbeiten aufgebrochen werden.»

> «viele dinge werden einfach so gemacht, ohne dass sie ein klarer prozesspunkt sind. es bräuchte eine art plan wie diese punkte
> erfasst werden können.»

Ausgangsfrage im Chat war, wie die Firma LEAP 71 mit ihrem System «Noyron» Raketentriebwerke autonom entwirft, und was es bräuchte,
um so ein System für Gebäudetechnik zu entwickeln. Ergebnis: KICAD ist bereits das Gerüst dafür. Es fehlen vor allem der erzeugende
Teil (Generator, Bewertung) und die Datengrundlage mit dem Fachwissen.

## Zielbild

### Arbeitsteilung in Schichten (Vorschlag)

Kein einzelnes Modell macht alles. Jede Arbeit geht dorthin, wo sie am besten hinpasst:

| Schicht | Aufgabe | Wer |
| --- | --- | --- |
| 1 Koordination / Projektleitung | Ziele zerlegen, Konflikte zwischen Gewerken, abwägen, Rückfragen | starkes Allgemeinmodell |
| 2 Fachagenten (je Gewerk) | Varianten bilden, gewichten, Empfehlung begründen, Werkzeuge aufrufen | starkes bis mittleres Modell mit Werkzeugen und Normensuche |
| 3 Spezialisierte Arbeiter | gleichförmige Einzelschritte (auslesen, zuordnen, prüfen, dokumentieren) | kleine, feinjustierte Modelle, später lokal |
| 4 Berechnungen und Geometrie | Dimensionierung, Druckverlust, Heizlast, Leitungsführung | **deterministischer Code im Kern, kein Sprachmodell** |
| Rückgrat | Datenmodell, über das alle Schichten lesen und schreiben | KICAD-Kern (Systemgraph, Pakete, Herkunft) |

### Vorbild LEAP 71 «Noyron»

Noyron ist kein Sprachmodell. Es ist ein «Large Computational Engineering Model»: Physik, Fertigungsregeln und Expertenwissen als
Code, der aus Anforderungen Geometrie erzeugt, prüft und iteriert. Testdaten aus realen Brennversuchen fliessen zurück ins Regelwerk.

Übertragen auf KICAD:
- Anforderungen (Räume, Luftmengen, Randbedingungen) → **Generator** erzeugt Varianten (z.B. Leitungsführung)
  → **Kern** setzt Formteile und prüft (Routing, Prüfungen, Kollision) → **Bewertung** gewichtet die Varianten
  → Agent wählt und begründet → Mensch gibt frei.
- Das Sprachmodell ist die Bedienschicht über MCP («Eine Tür für alles»). Geometrie und Physik bleiben im Kern.
- Statt Brennversuchen: Vergleich mit Referenzlösungen (Mustersituationen) und mit «Test_praxis».

Unterschied zur Rakete: Die Architektur ist vorgegeben, Produkte sind meist Katalogware, es gibt viele Schnittstellen zu anderen
Gewerken. Es geht weniger um Formfindung als um **Konfigurieren und Führen unter Randbedingungen**.

## Begriffe

| Begriff | Bedeutung |
| --- | --- |
| **Datengrundlage** | Oberbegriff (**Entscheid** Christian: «wir fassen dieses Thema mal als "Datengrundlage" zusammen.») für das systematisch erfasste Fach- und Prozesswissen: Mustersituationen, Musterprojekte, Protokolle, Referenzlösungen |
| **Mustersituation** | abgeschlossene Aufgabe aus einem einzelnen Arbeitsschritt, mit Eingaben, Referenzlösung und Begründung, ähnlich einer guten Prüfungsaufgabe. Idee von Christian: «könnte man das nicht für einzelne schritte machen? Bsp. Mustersituationen?» |
| **Musterprojekt** | fiktives, vollständig durchgeplantes Projekt je Sparte; Gesamtprobe für Ablauf, Reihenfolge und Störungen |
| **Referenzlösung** | Christians eingefrorene Lösung einer Situation bzw. eines Schritts |
| **Prüfset** | Teil der Referenzen, der nie für Training verwendet wird, nur zum Messen |
| **Generator** | Kernfunktion, die aus Anforderungen und Randbedingungen Varianten erzeugt (zuerst: Leitungsverläufe) |
| **Bewertung** | Kernfunktion, die Varianten nach gewichteten Kriterien vergleicht |

## Datengrundlage

### Entscheide Christian (30.10.2026)

- **Sparten:** je ein Musterprojekt für **Wohnungsbau, Gewerbe, Industrie** («es bräuchte jeweils ein Projekt aus unterschiedlichen
  Sparten. bsp. Wohnungsbau, Gewerbe und Industrie»).
- **Harness nur für Musterprojekte**, keine Erfassung laufender Arbeit («ich denke eher an einen harnes nur für das musterprojekt.»).
  Damit gibt es keine Überwachung, keine Sammler für Mails oder Dateien und keine Datenschutzfragen gegenüber Mitarbeitenden.
- **Mustersituationen** für einzelne Schritte (siehe Begriffe).

### Vorschläge (nicht entschieden)

- **Mustersituationen sind der Hauptbaustein**, das Musterprojekt je Sparte ist die spätere, kleinere Gesamtprobe.
- **Reihenfolge der Sparten:** Wohnungsbau zuerst (am stärksten standardisiert), dann Gewerbe, Industrie zuletzt (Prozess bestimmt
  alles; eher ein echtes, anonymisiertes Projekt als Grundlage).
- **Das Beispielprojekt «MFH Musterweg»** ist faktisch schon ein Musterprojekt Wohnungsbau und könnte der Ausgangspunkt sein.

### Aufbau einer Mustersituation (Vorschlag)

1. **Kontext:** Sparte, Gebäudetyp, Phase, relevante Vorentscheide (nur so viel wie nötig)
2. **Eingaben:** möglichst maschinenlesbar (Werte, Tabellen, Modellausschnitt), nicht nur PDF
3. **Aufgabe:** eine klare Frage oder ein klarer Auftrag
4. **Referenzlösung**
5. **Begründung:** Regel, Norm (mit Fundstelle), Erfahrung, verworfene Alternativen; getrennt von der Lösung
6. **Varianten:** welche Eingaben veränderbar sind und wie sich die Lösung dann ändert
7. **Bewertung:** wann eine andere Lösung trotzdem richtig ist (exakt, Toleranz, fachlich gleichwertig)
8. **Zuordnung:** Prozessschritt, Situationsart, Schicht

### Situationsarten (Vorschlag)

| # | Art | Beispiele | Schicht | Eignung |
| --- | --- | --- | --- | --- |
| 1 | Erfassen / Auslesen | Datenblatt in Tabelle, Raumprogramm aus Plänen, Vorgaben aus Protokoll | 3 | hoch |
| 2 | Bestimmen / Berechnen | Luftmengen, Heizleistung, Warmwasserbedarf, Dimension | 4 + 2 | hoch, wenn Rechenkern vorhanden |
| 3 | Wählen / Variantenentscheid | Lüftungssystem, Wärmeerzeugung, Warmwasser zentral/dezentral | 2 | mittel |
| 4 | Platzieren / Führen | Gerätestandort, Steigzonen, Leitungsführung, Zentrale | 2 + 4 (Generator) | schwierig |
| 5 | Prüfen / Kontrollieren | Plan vollständig, Offerte gegen Ausschreibung, Brandschutz | 3 / 2 | hoch bis mittel |
| 6 | Reagieren auf Änderungen | Nutzungsänderung, Steigzone verschoben, Produkt nicht lieferbar | 2 / 1 | mittel |
| 7 | Koordinieren / Kommunizieren | Kollision mit Elektro, Anfrage an Architekt, Pendenzen | 1 | schwierig |
| 8 | Dokumentieren / Ausgeben | Anlagebeschrieb, Ausschreibung, Schema, Planliste | 3 | hoch |

Start mit 1, 2, 5, 8. Dann 3 und 6 (dort steckt das Erfahrungswissen). Zuletzt 4 und 7.

### Störungen

Musterprojekte und Situationen sollen bewusst Störungen enthalten (Planänderung Architekt, Bauherrenwunsch, Produkt nicht lieferbar),
damit sie nicht zum glatten Idealprozess werden. Erfasst wird, wie reagiert wird und welche Schritte wiederholt werden müssen.

### Verwendung der Daten

| Verwendung | Ergebnis |
| --- | --- |
| Prozessmodell | Schritte, Reihenfolge, Ein- und Ausgaben, Entscheidungspunkte |
| Regeln und Wissen | Entscheidungsregeln, Bürostandards, Kennwerte → Code im Kern oder Wissensbasis |
| Trainingsdaten | Eingabe-Ergebnis-Paare für spezialisierte kleine Modelle (Schicht 3) |
| Messlatte | Prüfset für Agenten und Generator |

Das Prüfset wird nie für Training verwendet. Beim Erzeugen von Trainingsdaten mit fremden Modellen deren Nutzungsbedingungen beachten.

Ausführlich: Claude-Doc «Musterprojekt-Harness – Konzept» (https://claude.ai/artifact/AKFJKBU96StzDErV84Ew2F).

## Bestand in KICAD (Abgleich 30.10.2026, Commit 7edee1a)

| Baustein | Stand | Wo |
| --- | --- | --- |
| Datenmodell (Systemgraph) | vorhanden, tragfähig | `Modell/`, Leitprinzip 1 |
| Geometrie, IFC, Kollision | vorhanden | `Grafik/`, `Kollision/`, `Import/`, Modul 3b |
| Regelwerk | teilweise: Prüfungen P01–P19, Serien, Winkel, Gefälle, Verlegung | `Pruefung/`, `Leitungen/` |
| Produktdaten | teilweise, offene Konflikte (K3, R1, R7) | Modul 2, `Familien/` |
| Rechenkerne | nur Sanitär Teil A; Heizung, Lüftung fehlen (Normen fehlen) | `Rechnen/`, Modul 4/5 |
| Generator | **fehlt.** `Routing.Planen` setzt Formteile entlang eines vom Menschen vorgegebenen Verlaufs | `Leitungen/Routing.cs` |
| Bewertung / Varianten | **fehlt** | – |
| Prüfschleife | vorhanden: Prüfungen, Kollision, `ki-test`, Prüfung an Test_praxis | `ablauf.md` |
| KI-Bedienschicht | vorhanden: MCP, KI-Palette, Normensuche | `Hlks.Mcp`, Modul 3c |
| Protokoll | teilweise: jedes Paket mit Akteur, Art (mensch/ki/…), Zeit, Befehl, Parameter; Werte mit Herkunft und Quelle | `Modell/Grundtypen.cs` (`Paket`, `Herkunft`) |
| Agentenaustausch | Konzept, nicht gebaut | `entwurf/agentenschnittstelle.md` |

Wichtig für spätere Arbeiten:
- **Trockenlauf** jedes Befehls (Vorschau ohne Änderung) ist die Grundlage, um viele Generator-Varianten durchzuspielen.
  Nicht verschlechtern.
- **Pakete mit Herkunft** sind ein halbes Schritt-Protokoll. Es fehlen die **Begründung** (Warum) und die **Zuordnung zu
  Prozessschritt / Mustersituation**.

## Bausteine des Vorhabens (alle Vorschlag)

| Baustein | Inhalt | Abhängig von |
| --- | --- | --- |
| A Datengrundlage | Mustersituationen und Musterprojekte erfassen (Fachkopfarbeit Christian, mit Claude im Chat) | – |
| B Protokoll im Kern | Paket um optionale Felder `begruendung` und `prozessschritt` / `situation` ergänzen; Erfassung von Mustersituationen in KICAD | A (Schema der Situation) |
| C Rechenkerne | Lüftung, Heizung, Sanitär Teil B | **Normen** (siehe offene Fragen) |
| D Generator Leitungsführung | Wegsuche im 3D-Raum mit Hindernissen und erlaubten Zonen → Verlauf → bestehendes `Routing` im Trockenlauf | Kollision 3b, Phase 2 (Räume, Hülle) |
| E Bewertung | Varianten nach gewichteten Kriterien vergleichen (Länge, Formteile, Kollisionen, später Druckverlust, Platz, Kosten) | D; für Druckverlust C |
| F Prüfstand | Agent oder Generator löst Mustersituationen; Vergleich mit Referenz; Ausbau von `ki-test` | A, B |
| G Spezialisierte Modelle | kleine lokale Modelle für Situationsarten 1, 5, 8; Trainingsdaten aus A/B | A, B, F |

## Phasen des Vorhabens (Vorschlag)

1. **Datengrundlage:** Mustersituationen (Wohnungsbau zuerst), Erfassungsschema
2. **Auswerten und Zuordnen:** Prozesslandkarte, jede Situation einer Schicht zuordnen
3. **Werkzeuge und Wissen:** Rechenkerne (mit Normen), Regeln als Code, Wissensbasis
4. **Erste Agenten:** Fachagent Lüftung mit starkem Modell über MCP
5. **Prüfstand:** Agent an Mustersituationen messen, nachbessern
6. **Spezialisieren:** stabile, gleichförmige Schritte an kleine lokale Modelle
7. **Schattenbetrieb:** Agent arbeitet parallel in einem echten Projekt, Ergebnisse werden nicht verwendet, nur verglichen
8. **Ausbau:** weitere Sparten, Gewerke, Koordination, KI-Projektleiter (Bezug: KI-Planungsbüro)

## Einordnung in `entwurf/ablauf.md` (Vorschlag, Entscheid Christian offen)

- Phase 0 und 1 laufen unverändert weiter. Das Vorhaben drängt nichts vor.
- **Datengrundlage (A)** ist vor allem Fachkopfarbeit und kann parallel laufen. Software dafür (B) erst auf Auftrag.
- **Generator (D) und Bewertung (E)** gehören fachlich zu **Modul 6 (Lüftung, Kanalnetz)** in Phase 3 oder werden ein eigenes
  Modul. Sie brauchen Phase 2 (Räume, Hülle aus IFC) als Grundlage.
- Bei Arbeiten an Routing, Kollision, Paketen und Beispielprojekt in Phase 1: Schnittstellen so halten, dass D, E und B später
  andocken können (Verlauf als Eingabe, Trockenlauf, Kennzahlen der Prüfungen abfragbar).

## Möglicher erster Bauschritt (Vorschlag, nicht beauftragt)

**G1 – Generator Lüftungskanäle in einem Geschoss (ohne Normwerte)**

Umfang:
- Eingabe: ein Geschoss des Beispielprojekts, ein Anschluss an der Steigzone, die Luftdurchlässe der Räume, eine vorgegebene
  Kanalserie und Grösse (keine Dimensionierung, weil die Lüftungsnormen fehlen).
- Wegsuche in einem Raster mit Hindernissen (Wände, Decken, Stützen aus den Prüfkörpern von 3b) und bevorzugten Zonen (Korridor,
  abgehängte Decke; woher diese kommen, ist offen).
- Ergebnis: mehrere Verläufe → `Routing.Planen` im Trockenlauf → Kennzahlen je Variante (Länge, Anzahl Formteile, Kollisionen,
  Befunde P13–P18).
- Befehl über die Kern-API (eine Tür), damit Oberfläche und KI ihn gleich nutzen.

Fertig, wenn …
- am «MFH Musterweg» mindestens drei unterschiedliche, kollisionsfreie Varianten entstehen, mit Kennzahlen, ohne das Projekt zu ändern;
- eine gewählte Variante mit einem Befehl übernommen und in einem Schritt rückgängig gemacht werden kann;
- dasselbe an Test_praxis trägt (Prüfung durch den Hauptlauf, siehe `ablauf.md`);
- Christian die Varianten fachlich beurteilt hat.

## Verbindliche Regeln (aus `CLAUDE.md`, für dieses Vorhaben besonders wichtig)

- **Normwerte nie aus dem Gedächtnis**, nur mit Quelle (Norm, Ausgabe, Ziffer, Seite). Fehlt die Norm: Annahme kennzeichnen, als
  offene Frage melden.
- **Keine echten Projektdaten** in Mustersituationen im Repository. Mustersituationen sind fiktiv oder anonymisiert und von Christian
  freigegeben.
- **Keine Erfassung der Arbeit von Mitarbeitenden.** Der Harness gilt nur für Musterprojekte und Mustersituationen.
- Fachliche Regeln, die in Code übergehen, stammen von Christian oder aus einer Norm mit Fundstelle, nie aus Vermutung.

## Kontext: lokale Modelle (Stand 30.10.2026, nur zur Information)

- Christians Laptop: Core Ultra 9 386H, 32 GB RAM, nur integrierte Grafik. Geeignet zum Ausprobieren kleiner Modelle, nicht zum Training.
- Geplant bzw. erwogen: Rechner mit RTX 5060 Ti 16 GB (lokaler Betrieb mittlerer Modelle, Training kleiner Modelle mit QLoRA).
- Für vertrauliche Daten ohne eigene Hardware: Schweizer Anbieter (z.B. Infomaniak) als Option besprochen, nicht entschieden.
- Diese Punkte betreffen Baustein G und sind für den Kern ohne Bedeutung.

## Offene Fragen an Christian

1. **Normen:** Welche Normen für Lüftung, Heizung und Sanitär werden beschafft, und wo liegen sie? (Blockiert C und damit E mit
   Druckverlust.)
2. **Einordnung:** Generator als Teil von Modul 6 oder als eigenes Modul? Ab welcher Phase?
3. **Harness:** Erfassung der Mustersituationen in KICAD (Baustein B) oder zuerst ausserhalb (Doc, Tabelle)?
4. **Erstes Musterprojekt:** «MFH Musterweg» ausbauen oder neues Musterprojekt Wohnungsbau?
5. **Erste Mustersituationen:** Welche Routinearbeiten kosten am meisten Zeit? (Frage im Chat gestellt, Antwort offen.)
6. **Bewertung:** Welche Kriterien und Gewichte gelten für Varianten (Länge, Formteile, Platz, Druckverlust, Kosten, Montage)?
7. **Bevorzugte Zonen für die Leitungsführung:** woher (Raumtyp, Deckenaufbau, von Hand gezeichnet)?

## Bezüge

- `entwurf/ablauf.md`, `entwurf/modul-3.md` (Routing), `entwurf/modul-3b-kollision.md`, `entwurf/agentenschnittstelle.md`
- Claude-Doc «Musterprojekt-Harness – Konzept»: https://claude.ai/artifact/AKFJKBU96StzDErV84Ew2F
- Claude-Doc «KI-Planungsbüro – Konzeptstand» (Prozessidee: Vorgaben → Varianten mit gewichteten Eigenschaften → Empfehlung →
  Detail; KI-Koordinator und KI-Gesamtprojektleiter)
- LEAP 71, Noyron: https://www.voxelmatters.com/leap-71-hot-fires-3d-printed-aerospike-rocket-engine/ ·
  https://gigazine.net/gsc_news/en/20240628-leap-71-rocket-engine
