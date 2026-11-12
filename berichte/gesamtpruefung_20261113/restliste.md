# Gesamtprüfung KICAD 13.11.2026 – Runde 3, Bereich «restliste»

Zweig `worktree-agent-a9e5098fe09617938`, Ausgangsstand `8a3203c` (Zusammenfassung Runde 2). Gelesen: alle Berichte der Runden 1 und 2 in diesem Ordner.
Abgearbeitet sind nur Punkte, die klein, eindeutig und ohne Entscheid Christians lösbar sind. Nicht angefasst (laut Auftrag): `Modell`,
`Api/Nachfuehrungen.cs`, `src/Hlks.Kern/Schema/`, Architektur und Aussparungen.

## 1. Kurzfazit

Sieben Commits mit neun kleinen Korrekturen. Die zwei wichtigsten: Beim Zeichnen einer Leitung ab einem Anschluss bzw. ab einer Leitung werden
Grösse, Höhe und Medium jetzt zuerst übernommen und die Pflichtfelder erst danach geprüft (Entscheid 02.11.2026); und an Kreuzungen behalten Kanäle,
Kanalrahmen und Fliesspfeil ihren Umriss samt Stirnkanten, statt ihn durch Ersatzränder über den ganzen Zug zu verlieren. Dazu Lücke in grob,
Zollbrüche, entartetes IFC-Achsensystem, Meldung ohne Familienbibliothek, Hinweis im Dämmdialog und zwei Schutzprüfungen. Ein Punkt aus Runde 2
(leitungen2.md 4.7) hat sich beim Nachlesen als nicht erreichbar erwiesen. Nichts im Browser geprüft (keine Browserreihen, kein Server): die
beiden Änderungen an der Oberfläche (Nr. 1 und 6) sind nur mit `node --check` und durch Lesen geprüft.

## 2. Korrigiert

| Nr. | Quelle im Bericht | Datei | Fehler | Korrektur | Test (Filter, Ergebnis) | Commit |
|---|---|---|---|---|---|---|
| 1 | zusammenfassung.md, Nebenbefund Hauptlauf 18:00 | `js/werkzeuge/leitung.js` (`klick`, `abzweigStarten`) | `pflichtPruefen` lief vor der Übernahme: Start an einem Anschluss verlangte Produktlinie, Grösse und Höhe, obwohl der Punkt sie liefert. Gleich beim Start an einer Leitung (Abzweig). | Erster Klick: an Anschluss bzw. Leitung zuerst übernehmen (`punktUebernehmen` / `abzweigStarten`), dann prüfen; fehlt danach noch etwas (z. B. Produktlinie am Gerät), Meldung und Start verwerfen – die übernommenen Werte bleiben in der Optionsleiste. Freier Startpunkt und alle weiteren Klicks prüfen wie bisher zuerst. «Von Hand gewählte Höhe» gilt nur, wenn eine Höhe eingetragen ist (leeres Feld: Höhe des Punktes). | kein Test (`klick` läuft nur mit Optionsleiste und DOM); `node --check`. **Im Browser nicht geprüft.** | `3dbf017` |
| 2a | grafik.md 4.4, verdeckung.md 3.5 | `Grafik/LeitungVerdeckung.cs` | Jedes angeschnittene Polygon mit Stift verlor seinen ganzen Umriss; ersetzt wurden nur Bandränder über **alle** Züge mit dem Stift des ersten angeschnittenen Polygons. Folge: Kanal ohne Stirnkanten, Rahmen und Fliesspfeil ohne Umriss, zusätzliche «hilf»-Ränder am ganzen Kanal, wenn nur ein Rahmen getroffen war. | Je Polygon: Füllung ausgespart (wie bisher), der eigene Umriss (geschlossene Ringe) bleibt ausserhalb der Deckfläche als Linie mit dem eigenen Stift und der eigenen Linienart. Die Ersatzränder entfallen; die gestrichelten verdeckten Ränder des Bandes bleiben. | neu `Kreuzung_KanalMitKontur_UmrissBleibtAusserhalbDerDeckflaeche` (Kanal 400 × 200, Rohr kreuzt bei x = 1000: Stirnkanten bei 0 und 2000 vorhanden, Umriss endet bei 987.5, keine Linie über die Kreuzung) | `d50ee83` |
| 2b | grafik.md 4.9, verdeckung.md 3.5 | `Grafik/LeitungVerdeckung.cs` (`Decken`) | Lücke in grob aus der Strichbreite 0.25 mm gerechnet, gezeichnet wird Stift «mittel» 0.35 mm → sichtbare Lücke 0.45 statt 0.5 mm. | Breite der Deckfläche aus `Stifte.Mm(HlksEinordnung.Stift(…))`. Handrechnung 1:50: 0.35 + 2 × 0.5 = 1.35 mm Papier = 67.5 mm (vorher 62.5). Die 0.5 mm selbst bleiben ANNAHME Hauptlauf (Frage 6 der Zusammenfassung). | neu `Kreuzung_Grob_LueckeAbRandDesGezeichnetenStrichs`; zusammen `FullyQualifiedName~Darstellung3SerieTests\|KanalRahmenTests\|HlksGrafikTests`: **85 von 85 grün** | `d50ee83` |
| 3 | leitungen2.md 4.9 | `Leitungen/Verbindungen.cs` (`Zoll`) | «5/4"» und «1 1/4"», «6/4"» und «1 1/2"» galten als verschiedene Grössen → «kein Übergang passt». | Bruch kürzen und unechte Brüche umschreiben (5/4 → 1 1/4, 6/4 → 1 1/2, 2/4 → 1/2, 4/4 → 1). Dezimalzoll («1.5"») bleibt unverändert (siehe 3.3). | `Zoll_Normalisiert` um fünf Fälle erweitert | `a4368a0` |
| 4a | import_familien_baustelle.md 3 Nr. 9 | `Import/Matrix.cs` (`Achsen`) | RefDirection parallel zur Achse (ungültiges IFC): x = z, y = Nullvektor → entartete Lage. | In dem Fall die Vorgabe wie ohne RefDirection. | neu `MatrixTests.Achsen_XParallelZuZ_WieOhneXRichtung` | `c1eac12` |
| 4b | leitungen.md 4.10 | `Leitungen/Lagen.cs` (`LageVon`) | Geprüft wurde `pts[i].Length > 2`, gelesen `pts[i − 1][2]` (Absturz bei gemischten 2D/3D-Punkten). | Den gelesenen Punkt prüfen. | kein neuer Test (reine Schutzprüfung); Lauf Nr. 3 bis 5 zusammen: `VerbindungenTests\|MatrixTests\|FamilienBibliothekTests.Grundbestand\|LagenTests\|LagemanagerTests`: **61 von 61 grün** (ob die beiden Lagen-Filter eine Klasse treffen, habe ich nicht nachgesehen) | `c1eac12` |
| 5 | oberflaeche.md 3.6 «Meldung ohne Bibliothek» | `Familien/FamilienGrundbestand.cs` (`Hinweis`) | Ohne Bibliothek: «0 von 0 freigegebenen Familien geladen. Nicht geladen: Familienbibliothek nicht eingestellt». Bei vielen Fehlern beliebig lange Meldung. | Eigener Satz «Das Projekt ist ohne Familien angelegt: … Familien lassen sich später laden (…)»; Fehlerliste auf fünf gekürzt mit «und n weitere». | neu `Grundbestand_Hinweis_OhneBibliothekEigenerSatz_VieleFehlerGekuerzt` | `ad05f72` |
| 6 | leitungen2.md 4.4 (nur der Hinweis, nicht die Frage 4) | `js/oberflaeche/aktionen-leitung.js` (Dämmdialog) | Bei Mehrfachauswahl mit gemischten DN nannte der Dialog nur die erste DN; ohne DN im Typ kam kein Vorschlag, ohne Erklärung. | Hinweis «verschiedene Grössen (DN …) – die eingetragene Dicke gilt für alle, der Vorschlag ist für DN x gerechnet» bzw. «Kein Vorschlag: die Grösse nennt keine DN». Verhalten (eine Dicke für alle) unverändert – das entscheidet Christian. | kein Test (Dialogtext); `node --check`. **Im Browser nicht geprüft.** | `8795e09` |
| 7 | import_familien_baustelle.md 3 Nr. 16 | `Baustelle/betrachter.js` | `decodeURIComponent(location.hash)` wirft bei kaputter Adresse und bricht den Start ab. | try/catch, Adresse dann roh. | kein Test; `node --check` | `cb07d53` |

`OBERFLAECHE.md` nachgeführt (Kreuzende Leitungen; Leitung an Anschluss beginnen; Dämmung bei Mehrfachauswahl) – mit dem Bericht committet.
Kein Test war vor meinen Änderungen rot.

## 3. Gefunden, nicht korrigiert

1. **leitungen2.md 4.7 ist nicht erreichbar** – `Leitungen/ParalleleLeitungen.cs:108`. Die Meldung «… parallele Züge gibt es nur in einer Grösse» für
   «anderes Formteil im Zug» kann nur bei einer Reduktion kommen: `Leitungszug.cs:28` nimmt in einen Zug ausschliesslich Leitungen, Bögen und
   Reduktionen (mit zwei Anschlüssen) auf; an einer Armatur endet der Zug. Kein Fehler, nichts geändert. Aufwand –, Priorität –.
2. **Start am Anschluss eines Geräts ohne gewählte Produktlinie** – `js/werkzeuge/leitung.js` (`punktUebernehmen`). Ein Geräteanschluss liefert DN
   und Medium, aber keine Produktlinie; ohne Produktlinie gibt es keine Grössenliste, die DN wird dann nicht in eine Grösse übersetzt. Nach der
   Meldung «Die Produktlinie fehlt» muss der Anschluss noch einmal angeklickt werden. Vorschlag: Produktlinie aus der Dimensionsstufe des Systems
   bzw. die zuletzt benutzte vorschlagen. Aufwand klein, Priorität mittel (berührt «Produktlinien je Dimension»).
3. **Dezimalzoll** – `Leitungen/Verbindungen.cs` (`ZollMuster`). «1.5"» wird weiter zu «1» gelesen (der Punkt trennt). Auswirkung: «1.5"» und «1"»
   gälten als gleich – das ist die falsche Richtung (ein falscher Übergang wäre möglich). In den Katalogen habe ich keine solche Schreibweise
   gesucht. Vorschlag: Dezimalzahl erkennen und in einen Bruch umrechnen. Aufwand klein, Priorität tief.
4. **Abzweig an einer Abwasserleitung übernimmt das Gefälle nicht** (leitungen.md 4.11) – nicht gemacht: Ich habe nicht geprüft, ob das Gefälle als Wert an der
   Leitung steht oder aus der Geometrie gerechnet werden müsste; sinnvoll zusammen mit der offenen Frage 1 (2 % gegen 1°). Aufwand klein, Priorität mittel.
5. **Fallleitungsregel ohne Richtungsprüfung** (leitungen.md 4.7), **Neigung-Dialog leer = 0** (leitungen2.md 4.13), **Dämmkugeln an Endstücken**
   (grafik.md 4.6), **Achslinien-Überstand im Schnitt mit festem Massstab 50** (grafik.md 4.11), **`stil.mm` am Bildschirm** (grafik.md 4.10):
   klein, aber jeweils mit sichtbarer Verhaltensänderung, die ohne Browser bzw. ohne Entscheid nicht sicher zu beurteilen ist – offen gelassen.
6. **Begriffe «Leitungsserie» / «Produktlinie»** (leitungen2.md 4.5): unverändert, weil `leitung.browser.mjs:202` den alten Wortlaut erwartet;
   zusammen mit der Browserreihe umstellen. Aufwand klein, Priorität tief.

## 4. Offene Fragen an Christian

Keine neuen. Die Fragen der Zusammenfassung (1 bis 16) bleiben; Nr. 6 (Lücke grob 0.5 mm) ist jetzt genau 0.5 mm ab Strichrand, der Wert selbst
bleibt eine Annahme.
