# Serien-PDF und Plansätze – Spezifikation

Stand 19.10.2026 · Hauptlauf · vorgezogener Teil von Modul 7 (Ausgabe)

## Anlass und Entscheide

Christian, 19.10.2026: «Serien-PDF-Export muss möglich sein. Je Gewerk ein Plansatz, dass ein PDF aus mehreren Plänen
erstellt; welche beim Export enthalten sind, soll ausgewählt werden.»
Bisher: `plan_pdf` {planblatt, datei} → ein PDF je Planblatt (PDFsharp, `src/Hlks.Kern/Plan/Planausgabe.cs`); Planblätter
haben kein Gewerk, es gibt keine Plansätze.

| Frage | Entscheid Christian (Fragefenster 19.10.2026) |
| --- | --- |
| Ablage | **wie die H+K-Ausgabe:** Vorschlag `<Objekt>\c_Pläne und Modelle\d_Ausgang\b_Pläne\<TT.MM.JJJJ>_<Anlass>\<Gewerk>\` (wie `21.01.2026_Revisionspläne\Sanitär\`), Anlass und Ordner im Dialog änderbar |
| Dateien | **ein PDF je Plansatz**, Seiten je in ihrem Format; per Häkchen zusätzlich **je Plan ein PDF** mit Namen wie «1-0768-S01-Sanitär 1.Untergeschoss-.pdf» |
| Plansatz | **automatisch je Gewerk**, abwählbar; Auswahl im Plansatz gespeichert; weitere Plansätze von Hand |
| Reihenfolge | **nach Plannummer**, im Dialog verschiebbar, Reihenfolge gespeichert |

## Datenmodell (Hauptlauf, Katalog)

- `Planblatt.gewerk` (heizung, lueftung, sanitaer, kaelte, elektro, koordination, allgemein); ohne Angabe Vorschlag aus der
  Plannummer (Fachbuchstabe nach der Projektnummer: H, L, S, K) bzw. aus den Gewerken der Ansichten auf dem Blatt.
- Klasse **`Plansatz`** (PS): `name`, `gewerk`, `automatisch`, `blaetter` (Reihenfolge), `abgewaehlt`, `ausgabe`
  {anlass, ordner, einzeln, zeit}.

## Befehle und Abfragen

| Befehl / Abfrage | Zweck |
| --- | --- |
| Abfrage `plansaetze` | alle Plansätze; automatische je Gewerk, für das es Planblätter gibt, werden **angeboten** (noch nicht angelegt) bzw. nachgeführt; je Satz die Blätter in Reihenfolge mit Nummer, Titel, Format, Gewerk (mit Herkunft: gesetzt/Vorschlag), dabei/abgewählt, Vorschlag Ausgabeordner und Dateinamen |
| `plansatz_setzen` {plansatz?, name, gewerk?, automatisch?, blaetter?, abgewaehlt?, ausgabe?} | anlegen bzw. ändern (Trockenlauf, Rückgängig) |
| `planblatt_gewerk_setzen` {planblaetter[], gewerk} | Gewerk von Hand (Vorschläge bestätigen) |
| Abfrage `plansatz_pdf` {plansatz, ordner?, anlass?, einzeln?, blaetter?} | schreibt das Sammel-PDF (Seiten nacheinander, je Seite das Format des Blatts, Lesezeichen je Plan «S01 Sanitär 1.UG») und auf Wunsch die Einzel-PDFs; legt den Ordner an; überschreibt keine vorhandene Datei ohne `ueberschreiben: true` (sonst Fehler mit Vorschlag); speichert die Einstellung im Plansatz (eigener Befehl im selben Ablauf); Antwort: Dateien, Seiten, Grösse, Dauer, Hinweise (z. B. Blatt ohne Ansicht) |

- **Dateinamen:** Sammel-PDF `<Projektnummer>_<Plansatzname>_<JJJJ-MM-TT>.pdf` (z. B. «1-0768_Plansatz Sanitär_2026-10-19.pdf»);
  Einzel-PDF `<Projektnummer>-<Plannummer ohne Projektnummer>-<Titel>-.pdf` wie im H+K-Muster (ungültige Zeichen ersetzt).
- **Objektordner:** aus dem Projektordner (`…\<Objekt>\c_Pläne und Modelle\e_KICAD` → `<Objekt>`); liegt das Projekt
  anders, schlägt der Dialog den Projektordner `\ausgabe\` vor.
- PDF wie heute über `Planausgabe` (eine gemeinsame Seitenfunktion, kein zweiter Zeichner); PDFsharp-Version nicht ändern.

## Oberfläche

- Register **Ansicht** bzw. Datei: Knopf **«Serien-PDF …»** (und Kontextmenü am Knoten «Pläne» im Projektbrowser).
- Dialog: links die Plansätze (automatische je Gewerk, eigene), rechts die Blätter des gewählten Satzes mit Häkchen,
  Nummer, Titel, Format, Gewerk (Vorschlag grau mit «bestätigen»), Reihenfolge per Ziehen bzw. ↑↓; unten Anlass, Ordner
  (Vorschlag, änderbar), «zusätzlich Einzel-PDFs», «Exportieren»; Fortschritt je Blatt; danach Liste der Dateien mit
  «Ordner öffnen».
- Projektbrowser «Pläne»: Blätter gruppiert nach Gewerk (wie die Plansätze).

## Zuteilung

| Teil | Inhalt | Wer |
| --- | --- | --- |
| A | Spezifikation, Katalog (`Planblatt.gewerk`, Klasse `Plansatz`) | Hauptlauf (erledigt 19.10.2026) |
| P1 | Kern (Abfragen, Befehle, Sammel-PDF, Einzel-PDF, Namen, Ordner), MCP, Oberfläche (Dialog, Knopf, Browser), Tests, Bilder | hlks-modul (Exporte und Oberfläche nach fertiger Spezifikation) |

**Fertig, wenn …** im Projekt mit H-, S- und L-Plänen je Gewerk ein Plansatz angeboten wird, Christian Blätter abwählt
und umstellt, ein Klick ein PDF mit allen gewählten Plänen in der Reihenfolge nach Plannummer im vorgeschlagenen
Ausgabeordner schreibt (auf Wunsch zusätzlich Einzel-PDFs mit H+K-Namen) und die Auswahl beim nächsten Export wieder da
ist.
