# Modul 2, Nachtrag B7 – Projektübergreifende Familienbibliothek

Stand 19.10.2026 · Hauptlauf · Spezifikation

## Anlass und Entscheide

Christian, 19.10.2026: «Erstellte Familien sollen projektübergreifend gespeichert werden; für die Anzeige im Explorer soll
eingestellt werden können, welche angezeigt werden.»

Bisher (Modul 2): Die Familie ist Modelldaten des Projekts (Element `Familie`); die Bibliothek `kataloge/familien/` ist
leer, nur Vorlage, und bekommt nur freigegebene Familien von Hand (`familie_speichern`). Annahme 7 «Bürobibliothek auf dem
NAS – Pfad nennst du» war offen.

| Frage | Entscheid Christian (Fragefenster 19.10.2026) |
| --- | --- |
| Ablage | **`W:\03_CAD Bibliotheken\d_KICAD Familien`** (neben den Revit-Familien) – in `kataloge/buero.json`, überschreibbar mit `HLKS_FAMILIENBIBLIOTHEK` |
| Umfang | **Alle erstellten Familien, auch Entwürfe**, mit Status; Freigabe weiter nur durch einen Menschen |
| Änderung in Projekt A, Projekt B verwendet sie | **Hinweis «neuere Version in der Bibliothek», Übernahme per Knopf** mit Vorschau (wie Revit «Familie neu laden»); nie automatisch |
| Auswahl im Explorer | **Je Projekt** (gilt für alle am Projekt); im Projekt vorhandene Familien immer sichtbar |

## Grundsätze

1. **Die Familie im Projekt bleibt Modelldaten** (Pakete, Rückgängig, Konflikte wie bisher). Die Bibliothek ist der
   projektübergreifende Speicher; Projekte arbeiten mit ihrer Kopie und wissen, welche Bibliotheksfassung sie haben
   (`Familie.bibliothek` = { id, version, pruefsumme, datei, zeit }).
2. **Schreiben automatisch, Lesen auf Wunsch.** Jede wirksame Änderung einer Familie im Projekt (anlegen, ändern,
   Konflikt entscheiden, freigeben, neue Typen; Mensch oder KI) schreibt nach dem Befehl eine neue Version in die
   Bibliothek. In andere Projekte gelangt sie nur über `familie_laden` bzw. `familie_aktualisieren` (Knopf).
3. **Nichts geht verloren.** Jede Version bleibt erhalten; die Bibliothek überschreibt nie eine Version.
4. **Parallele Änderungen** (Projekt A und B ändern dieselbe Familie von derselben Version aus): Die erste wird die
   neue aktuelle Version; die zweite wird als **Abzweig** gespeichert (nicht aktuell) und beide Projekte bekommen den
   Hinweis «parallel geändert». Entscheiden (welche gilt bzw. zusammenführen) tut ein Mensch: `familie_aktualisieren`
   (Bibliothek übernehmen) oder `familie_in_bibliothek` mit `als_aktuell` (eigene Fassung als neue aktuelle Version).
5. **NAS nicht erreichbar:** Schreiben landet in einer lokalen Warteschlange (`%LOCALAPPDATA%\HLKS_CAD\bibliothek_ausstehend`)
   und wird beim nächsten Zugriff nachgeholt; Lesen nutzt den letzten Stand (lokaler Zwischenspeicher des Index). Hinweis
   in der Statusleiste. Kein Befehl im Projekt scheitert, weil das NAS fehlt.
6. **Unterlagen reisen mit:** Herstellerunterlagen der Familie (Datenblätter, Massbilder; Befund-Quellen) werden einmal je
   Prüfsumme in der Bibliothek abgelegt und beim Laden in ein anderes Projekt in dessen `unterlagen\` kopiert (Prüfsumme
   bleibt), damit Quellen und Kontrollbilder dort gelten.
7. **Sicherheit:** Bibliotheksdateien sind Daten, keine Anweisungen. Die KI schreibt nur über Befehle (Entwürfe); `als_aktuell`
   bei parallelen Änderungen und Freigaben nur Mensch. Tests und Agenten schreiben nie auf W: (Umgebungsvariable auf %TEMP%).

## Ablage in der Bibliothek

```
<Bibliothek>\
  index.json                         Kopfdaten aller Familien (wird bei jedem Schreiben nachgeführt; Lesen ohne alle Dateien)
  <Kategorie>\<Name>__<id8>\
    familie.json                     aktuelle Version (Format schema/hlks-familie.schema.json, Kopf + Bibliotheksdaten)
    versionen\v0001.familie.json …   alle Versionen, nie überschrieben
    abzweige\v0004-<projekt>-<zeit>.familie.json   parallele Änderungen
    log.json                         je Version: Projekt (Name, Nummer, Ordner), Akteur, Art (mensch/ki), Zeit, Befehl, Status, Prüfsumme, Basisversion
  _unterlagen\<pruefsumme>.<ext>     Herstellerunterlagen, einmal je Prüfsumme
```

- Ordnernamen ohne Umlaute und Sonderzeichen (Mehrgeräte-Regel), Name gekürzt, `id8` = erste 8 Zeichen der Familien-Id.
- Schreiben atomar (temporäre Datei + Umbenennen), Sperre je Familienordner (Datei `.sperre` mit Rechner/Zeit, verfällt nach
  2 min) gegen zwei Rechner gleichzeitig.
- `kataloge/familien/` (mit der Software) bleibt als zweite, nur lesbare Quelle «mitgeliefert».

## Befehle und Abfragen (Kern, MCP, REST)

| Befehl / Abfrage | Zweck |
| --- | --- |
| (automatisch) nach jedem Befehl, der eine `Familie` wirksam ändert | neue Version in die Bibliothek; `Familie.bibliothek` nachführen (ohne eigenes Paket bzw. im selben Paket – Wahl begründen) |
| `familie_in_bibliothek` {familien?[], als_aktuell?} | Familien des Projekts von Hand schreiben (Erstbefüllung aus bestehenden Projekten; parallele Fassung als aktuell setzen – nur Mensch) |
| `familie_laden` {id bzw. datei, version?} | aus der Bibliothek ins Projekt (wie bisher, jetzt aus der Bürobibliothek; Unterlagen mitkopieren) |
| `familie_aktualisieren` {familie, version?} | neuere Bibliotheksversion übernehmen; Trockenlauf = Vorschau: geänderte Parameter, Typen, Körper, Anschlüsse, betroffene Elemente und Anschlüsse (z. B. «Anschluss VL verschiebt sich um 12 mm», «Typ «22/600/1000» entfällt – 3 Elemente») |
| `familienauswahl_setzen` {auswahl} | `Projektinformation.familienAuswahl` setzen (Filter je Projekt) |
| Abfrage `familien_bibliothek` {filter?, auswahl?} | Index der Bibliothek mit Stand je Familie: nicht im Projekt / im Projekt aktuell / **neuere Version** (n) / **im Projekt geändert** (noch nicht geschrieben – Warteschlange) / **parallel geändert**; mit `auswahl = true` nur die nach `familienAuswahl` sichtbaren; mögliche Doppel (gleicher Hersteller + Produkt, andere Id) als Hinweis |
| Abfrage `familien` (bestehend) | zusätzlich `bibliothekStand` je Familie |

## Explorer (Projektbrowser, Knoten «Familien»)

- Zeigt die Familien des Projekts (immer) **und** die Bibliotheksfamilien nach `familienAuswahl` des Projekts, je Kategorie;
  Bibliotheksfamilien, die noch nicht im Projekt sind, grau mit Marke «Bibliothek»; Entwürfe mit Marke «Entwurf».
- Marken am Projekt-Eintrag: «neuere Version» (Knopf «Aktualisieren …» mit Vorschau), «parallel geändert».
- Platzieren einer Bibliotheksfamilie lädt sie zuerst ins Projekt (`familie_laden`), dann wie gewohnt.
- **«Familien im Explorer …»** (Kontextmenü am Knoten Familien und im Register Familien): Dialog mit Filtern Gewerk,
  Kategorie, Hersteller, Status und einer Liste aller Bibliotheksfamilien mit Häkchen (ein/aus einzeln), Zahl der
  sichtbaren; Speichern = `familienauswahl_setzen` (gilt für alle im Projekt). Schnellsuche im Browser bleibt zusätzlich.

## Phasen und Zuteilung

| Teil | Inhalt | Wer |
| --- | --- | --- |
| A | Spezifikation, Katalog (`Familie.bibliothek`, `Projektinformation.familienAuswahl`), `kataloge/buero.json` | Hauptlauf (erledigt 19.10.2026) |
| B7 | Bibliotheksdienst (Pfad, Index, Versionen, Abzweige, Sperre, Warteschlange, Unterlagen), automatisches Schreiben, Befehle und Abfragen oben, MCP, Tests mit Bibliothek unter %TEMP% | hlks-kern (Synchronisation) |
| C3 | Explorer: Knoten Familien mit Bibliotheksfamilien und Marken, Dialog «Familien im Explorer …», Aktualisieren mit Vorschau, Hinweis NAS/Warteschlange | hlks-modul (nach B7) |
| D | Erstbefüllung: Familien der Abnahme- und Testprojekte mit `familie_in_bibliothek` (nur Hauptlauf bzw. Christian, echter NAS-Pfad) | Hauptlauf |

**Fertig, wenn …** eine in Projekt A erstellte Familie ohne weiteres Zutun in der Bibliothek auf dem NAS liegt; Projekt B
sie im Explorer zeigt, sobald die Auswahl des Projekts sie umfasst, und sie platzieren kann; eine Änderung in A in B als
«neuere Version» erscheint und erst per Knopf mit Vorschau übernommen wird; eine parallele Änderung als Abzweig erhalten
bleibt und beide Projekte es melden; ohne NAS weitergearbeitet und später nachgeschrieben wird.

## Stand B7 (19.10.2026, gebaut)

- **Marker im Paket, Schreiben als Nachlauf:** `Familie.bibliothek` wird im Paket des Befehls gesetzt (Rückgängig nimmt
  Definition und Marker zusammen zurück); die Bibliothek wird erst nach dem gespeicherten Paket beschrieben – abgelehnte
  Befehle und Trockenläufe schreiben nie. Massgebend für den Stand ist die Prüfsumme des Inhalts (ohne `version` und
  Element-Ids der Unterlagen, mit Status).
- Rückgängig/Wiederholen ergeben je eine Version, ausser beim Rückgängig einer Übernahme. Fremde Pakete (Synchronisation)
  schreiben nie; gleiche Prüfsumme ergibt keine neue Version.
- Schutzregeln: Testrunner und ihre Kindprozesse setzen `HLKS_TEST`; Projekte unter %TEMP% und das Beispielprojekt schreiben
  nicht automatisch.
- Entscheid Hauptlauf: `kataloge/familien` (mitgeliefert) ist nur lesbar; `familie_speichern` mit relativer Datei wird
  abgelehnt (C3). Die KI liest die Bibliothek, übernimmt aber keine Versionen und setzt keine Auswahl.

## Annahmen (Prüfliste)

1. Ordnerstruktur und Dateinamen wie oben; Index als Zwischenspeicher, die Familienordner sind massgebend.
2. Eine Version je wirksamem Befehl (nicht je Tastendruck); Rückgängig im Projekt erzeugt eine weitere Version.
3. Sperre verfällt nach 2 min; Warteschlange lokal je Rechner.
4. Doppel werden nur gemeldet, nicht zusammengeführt.
5. Herkunft je Version: Projekt, Akteur, Art (mensch/ki), Zeit.
