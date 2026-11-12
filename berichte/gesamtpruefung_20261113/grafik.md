# Gesamtprüfung KICAD 13.11.2026 – Bereich «grafik» (Plandarstellung 2D und 3D)

Geprüft: `src/Hlks.Kern/Grafik/` (HlksGrafik, DaemmGrafik, LeitungVerdeckung, FamilienGrafik, Achslinien, HlksKoerper, HlksLinien, Grafiktypen,
IfcDarstellung soweit betroffen), `src/Hlks.Kern/Plan/` (PdfZeichner, PlanSvg), `wwwroot/js/ansichten/plangrafik.js`, `grundriss.js`,
`ansicht3d.js`/`koerper3d.js` (nur Stichproben), `css/ansichten.css`. Stand: `3aed5cb` (main, 12.11.2026).

## 1. Kurzfazit

Die Neuerungen vom 12.11.2026 (Dämmung in Rohrfarbe, Stift «fein», Dämmung einzeln wählbar, Abwasser-Konturen, kreuzende Leitungen, Kreise in grob)
sind im Kern sauber umgesetzt; Bildschirm und PDF stimmen bei Stiften, Linienmustern, `NurBildschirm` und Deckkraft 0 überein. Abweichungen gab es
im SVG für die Baustelle (eigene Strichmuster, gedrehte Ansicht ragte aus dem Ausschnitt) und an vier Randstellen der neuen Logik – korrigiert in
drei Commits. Die wichtigste offene Schwäche ist geometrisch: «höher/verdeckt» vergleicht die Höhenspanne der ganzen Leitung statt der Höhe an der
Kreuzung – Leitungen mit Gefälle oder Etage werden deshalb an Kreuzungen oft gar nicht verdeckt dargestellt. Für grosse Pläne ist die Rechenzeit im
Kern unkritisch (Boxtests), teuer sind `Modell.VerbindungenVon` (linear je Aufruf) und im 3D ein Netz je Körperteil.
Nicht geprüft: Aussehen im Browser und im gedruckten PDF (keine Browserreihen, kein Server, laut Auftrag).

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Commit |
|---|---|---|---|---|
| 1 | `Grafik/HlksKoerper.cs` (`Daemmteile`) | Der Dämmkörper im 3D übernahm über `Roehren` eine eigene Achse. Schnitt und Ansicht (`Schnittgrafik` → `Achslinien.Zeichnen` je Teil mit Achse) zeichneten die Achslinie gedämmter Leitungen und – neu seit 12.11. – gedämmter Formteile doppelt. | Dämmteile ohne `Achse`/`AchseRadius`. | `164b6ba` |
| 2 | `Grafik/HlksGrafik.cs` (`Leitung`, verdeckter Lauf) | Dämmkanten unter dem Geschossboden blieben schwarz (`#000000`) mit Stift «hilf» 0.20 mm – Widerspruch zu Christian 12.11.2026 («aussenkontur in Rohrfarbe jedoch wesentlich feiner»), und im dunklen Design schwarz auf dunkel. | Rohrfarbe (`DaemmRand`), Stift «fein», weiter gestrichelt. | `164b6ba` |
| 3 | `Grafik/LeitungVerdeckung.cs` (Kreise) | `deck.Any(Innen)` zählte auch Punkte im **Loch** der Deckfläche als verdeckt (höhere Leitung als Ring: der Aussenring enthält den Punkt). | Gerade-ungerade über alle Ringe. | `164b6ba` |
| 4 | `Grafik/DaemmGrafik.cs` | Je Formteil `Modell.VerbindungenVon` = Durchlauf aller Verbindungen (Formteile × Verbindungen je Plan). Kommentar zu `Naht` stand an der falschen Konstante. | Index «Verbindungen je Element» einmal je Plan (gleiche Reihenfolge, gleiches Ergebnis); Kommentar berichtigt. | `164b6ba` |
| 5 | `Grafik/IfcDarstellung.cs` (`Ueberschreiben`) | `deckkraftNachbau` setzte die Deckkraft auch auf der unsichtbaren Trefferfläche der Dämmung (nur Bildschirm, Deckkraft 0) – sie wäre als Farbfläche sichtbar geworden. | Primitive «nur Bildschirm, Deckkraft 0» bleiben unverändert. | `97c8bda` |
| 6 | `Plan/PlanSvg.cs` | Eigene Strichmuster, kürzer als PDF/Bildschirm (strich 2/1 statt 3/1, strichpunkt 3/0.8/0.3/0.8 statt 6/1/0.5/1, punkt 0.3/0.8 statt 0.5/1, achse 1.5/0.4/0.2/0.4 statt 3/0.6/0.3/0.6); `Stil.Mm` (genaue Strichstärke der 2D-Zeichnung) übersehen. | Muster aus `PdfZeichner.Linienmuster` (eine Quelle), `Stil.Mm` vor dem Stift. | `eccce2b` |
| 7 | `Plan/PlanSvg.cs` | Gedrehte Ansicht (N9): gedreht wurde um die Mitte der **ungedrehten** Begrenzung, die viewBox blieb die ungedrehte – die Ecken eines gedrehten Plans lagen ausserhalb. | viewBox aus `BegrenzungAnsicht`, Drehung um den Ursprung (wie `Grafik.Begrenzung`). | `eccce2b` |
| 8 | `Plan/PdfZeichner.cs` | Eigene `Rang`-Suche mit überholtem Kommentar (doppelte Logik zu `Ebenen.Rang`), Kommentar der Achslinie an der falschen Zeile, «PDFsharp 6.5» im Kopf (ist 6.2.4). | `Ebenen.Rang`, Kommentare berichtigt. | `eccce2b` |

Tests (ein Lauf, gefiltert): `dotnet test tests/Hlks.Kern.Tests --filter "FullyQualifiedName~Darstellung3SerieTests|FullyQualifiedName~PlanSvgTests|FullyQualifiedName~IfcDarstellungTests|FullyQualifiedName~LeitungsbezeichnungTests"`
→ **53 bestanden, 0 Fehler**. Neu: `Kreuzung_PunktImLochDerDeckflaeche_BleibtSichtbar`, `Verdeckt_Daemmkanten_InRohrfarbeFein`, Zusatz in
`Koerper_Daemmung_ZweiterHellerKoerperNurImDetailgradFein` (keine Achse an der Dämmung), `IfcDarstellungTests.Ueberschreiben_UnsichtbareTrefferflaeche_BleibtUnsichtbar`,
`PlanSvgTests` (2). JS wurde nicht geändert, deshalb kein Node-Lauf. `HlksGrafikTests`, `SchnittTests`, `FamilieGrafikTests` nicht gelaufen
(Korrektur 1 betrifft Schnitt/Ansicht nur über das Weglassen einer doppelten Linie; wer sicher gehen will: `--filter "FullyQualifiedName~SchnittTests"`).

Hinweis: Beim Commit meldet git «failed to delete …/.git/worktrees/wt_b1 bzw. wt_b2: Permission denied» (Aufräumen fremder, verwaister
Arbeitskopien, vermutlich OneDrive-Sperre). Die Commits sind trotzdem entstanden; die beiden Einträge sollte der Hauptlauf einmal mit `git worktree prune` bereinigen.

## 3. Geometrie nachgerechnet (Zusatzauftrag Christian 12.11.2026: «auch die Logik hinterfragen, besonders in bezug geometrie»)

**Richtig (Handbeispiel stimmt):**

- **Versatz an Knicken** (`HlksLinien.Versatz`, Ränder der Bänder): Punkt + (n₁ + n₂)/(1 + n₁·n₂)·d. 90°-Knick nach links, n₁ = (0, 1), n₂ = (−1, 0):
  (−1, 1)/1·d = (−d, d), Gehrungslänge √2·d – stimmt.
- **Papier-mm ↔ Modell-mm**, grob 1:50 (`LeitungVerdeckung`): Strichbreite 0.25 mm × 50 = 12.5 mm, Deckfläche 12.5 + 2 × 0.5 × 50 = 62.5 mm → Unterbruch
  1.25 mm Papier, Lücke je Seite 0.5 mm (rechnerisch). Gezeichnet wird die Linie aber mit Stift «mittel» 0.35 mm (17.5 mm), die sichtbare Lücke ist
  (62.5 − 17.5)/2 = 22.5 mm = 0.45 mm Papier – 0.05 mm unter der Angabe in OBERFLAECHE.md (unbedeutend, siehe 4.9).
- **Dämmbreite**: DN 20 der Testserie, da 25, Dämmung 20 → 25 + 2 × 20 = 65 mm, Rand bei ± 32.5 mm (im neuen Test geprüft).
- **Drehung der Ansicht**: PDF dreht um den Papierpunkt der Fenstermitte mit −Drehung (y nach unten) – richtig. SVG: Modellpunkt (x, y) um D im
  Gegenuhrzeigersinn = `rotate(−D)` im y-gespiegelten SVG: x′ = x·cos D − y·sin D, −y′ = −(x·sin D + y·cos D) – stimmt mit `Grafik.Begrenzung(…, D)`.
  Beispiel 20000 × 5000 um 90°: Begrenzung [−5000, 0, 0, 20000], viewBox `-5400 -20400 5800 20800` (Test).
- **Clipper**: Deckfläche/Dämmung `InflatePaths(Round, Butt)` + `Union(NonZero)`; Aussparen der Füllung mit `Difference(EvenOdd)` ist richtig, weil die
  vereinigte Deckfläche Löcher gegenläufig führt und das Subjekt (Aussenring + Löcher) gerade-ungerade gemeint ist. Offene Linien über `AddOpenSubject` richtig.
- **Bogen im PDF/SVG** (`-(von + sweep)`, sweep-flag 0/1) und `Begrenzung` der Bögen: nachgerechnet für von 30°, bis 330° (Ausschnitt 60°) – stimmt.

**Fragwürdig bzw. falsch (nicht korrigiert, siehe 4.1–4.6).**

## 4. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 4.1 | `Grafik/LeitungVerdeckung.cs:25–39, 73` und `Grafik/DaemmGrafik.cs:39–40` | «Höher» vergleicht die **Höhenspanne der ganzen Leitung** (ZMin der einen > ZMax der anderen + 1 mm), nicht die Höhe **an der Kreuzung**. Beispiel: Abwasser 10 m mit 2 % Gefälle, Achse 2600 → 2400; eine Leitung auf 2500 kreuzt am hohen Ende (dort 2590) darunter durch: Spannen überlappen → «gleich hoch», nichts wird verdeckt. Ebenso eine Leitung mit Etage (500 → 2500 in einem Element): ZMin 500/ZMax 2500, sie verdeckt nie etwas. | Der Entscheid 12.11. («müssen verdeckt dargestellt werden») greift gerade bei Gefälleleitungen und Polylinien mit Höhenwechsel nicht. | Höhe je Zug bzw. am Schnittpunkt interpolieren (Deckfläche je waagrechtem Zug mit eigener Spanne; Vergleich der Achshöhen an der Kreuzung). | mittel | hoch |
| 4.2 | `Grafik/DaemmGrafik.cs:39–40` | Mit einer Steig-/Fallleitung entscheidet die **mittlere** Höhe. Beispiel: Steigleitung 0 → 3000 (Mitte 1500), waagrechte gedämmte Leitung auf 2500 daneben: die waagrechte gilt als höher, der Ring der Steigleitung wird gestrichelt – von oben gesehen liegt aber das obere Ende der Steigleitung (3000) über der Leitung. | Verdeckte Dämmkante am falschen Teil, wenn eine Steigleitung über die waagrechte hinausläuft. | Für senkrechte Teile ZMax (oberes Ende) statt Mitte vergleichen. | klein | mittel (Entscheid Darstellung, Frage 3) |
| 4.3 | `Grafik/HlksLinien.cs:153–155` | Gehrung: Nenner auf 0.25 begrenzt. Bei Umlenkung über 138.6° (1 + cos θ < 0.25) liegt der Versatzpunkt auf keiner der beiden Parallelen mehr, bei 180° fällt er auf die Achse. Kommentar «auf das Vierfache begrenzt» stimmt nicht (grösste Länge 2.83 × d bei 138.6°, darüber kürzer). Zudem: an der Innenseite kurzer Stücke (kürzer als d·tan(θ/2)) überschlägt die Parallele. | Ränder (gestrichelte Bänder, Abwasser-Konturen, verdeckte Ränder) schneiden bei sehr spitzen Knicken ins Band. Selten (Haarnadel, FBH-Wendepunkt ohne Biegeradius). | Ränder aus `Clipper.InflatePaths` (wie Deckfläche) ableiten oder Gehrung abflachen (zwei Punkte). | mittel | tief |
| 4.4 | `Grafik/LeitungVerdeckung.cs:96–104, 122–131` | Jedes veränderte Polygon mit Stift verliert seinen Umriss, ersetzt wird nur der Bandrand über **alle** Züge mit dem Stift des **ersten** veränderten Polygons. Trifft die Deckfläche z. B. nur einen Kanalrahmen (Stift «hilf»), bekommt der ganze Kanal zusätzliche «hilf»-Ränder; Rahmen und Fliesspfeil verlieren unter der Kreuzung ihren Umriss ersatzlos; Stirnkanten eines Kanalstücks fallen weg. Sichtbares Band rund an Knicken (`Band`), Ersatzränder mit Gehrung (`Versatz`). | Kleine Zeichenfehler an Kreuzungen von Kanälen. | Nur das Band-Polygon (erstes Polygon je Lauf) als «Umriss» behandeln, übrige Polygone mit Stift über offene Umrisslinien schneiden. | mittel | mittel |
| 4.5 | `Grafik/HlksKoerper.cs:180–181` | Kanal-Knick im 3D: Füllquader b × b × h mit Drehung 0. Beispiel b = 400, Kanal unter 45°: Ecken des Quaders 283 mm von der Mitte, halbe Kanalbreite 200 → er steht 83 mm über. Geneigte Kanalstücke: waagrechter Quader auf mittlerer Höhe (als Annahme vermerkt). | Sichtbar bei geknickten Kanal-Polylinien ohne Formteil und bei Schrägen. | Quader in Winkelhalbierende drehen bzw. Prisma aus dem vereinigten Grundriss; Schrägen als gedrehter Körper (`koerper`). | mittel | tief |
| 4.6 | `Grafik/HlksKoerper.cs:211–215` | Dämmkugeln an **jedem** Zwischenpunkt, ohne den Abstand zum Ende zu prüfen (das Rohr selbst prüft ihn, Zeile 45–54): Kugel r 32.5 an einem Knick 10 mm vor dem Ende ragt 22.5 mm über das Rohrende. Bei gebogenen Leitungen (5°-Stücke) je Bogen 18 Kugeln. | Dämmung ragt an kurzen Endstücken vor; viele Körperteile. | Gleiche Regel wie beim Rohr, bei Umlenkung < 10° keine Kugel. | klein | tief |
| 4.7 | `Modell/Modell.cs:64` (genutzt in `HlksKoerper.cs:225`, `HlksGrafik.cs:491`, `FamilienGrafik` über `HlksKoerper.DaemmungFormteil`) | `VerbindungenVon` durchsucht alle Verbindungen. `DaemmungFormteil` (neu 12.11.) ruft es je Formteil im 3D und in jedem Schnitt: 5000 Formteile × 10000 Verbindungen = 50 Mio. Vergleiche je Körperabruf. | Spürbar bei grossen Modellen (3D-Aufbau, Schnitte). | Index Element → Verbindungen im Modell führen (wie `anAnschluss`); nicht in meinem Bereich, deshalb nur in DaemmGrafik lokal gelöst. | klein | hoch |
| 4.8 | `js/ansichten/koerper3d.js:226–284` | Je Körperteil ein eigenes Netz, eigene Geometrie und eigenes Material (auch je Kugel/Zylinder der Dämmung). | Viele Zeichenaufrufe bei grossen Modellen; Dämmung verdoppelt die Zahl. | Geometrien je Körper und Material zusammenführen, Materialien teilen. | gross | mittel |
| 4.9 | `Grafik/LeitungVerdeckung.cs:52–53` | Lücke in grob aus der Strichbreite 0.25 mm gerechnet, gezeichnet wird Stift «mittel» 0.35 mm (siehe 3). | Lücke 0.45 statt 0.5 mm Papier. | Breite aus `Stifte.Mm(HlksEinordnung.Stift(…))`. | klein | tief |
| 4.10 | `js/ansichten/plangrafik.js:85–96` | Der Bildschirm kennt `stil.mm` und `stil.strichEinheiten` nicht (PDF ja, SVG seit heute `mm`). | 2D-Zeichnung (DWG-Linienstärken/-typen) am Bildschirm nur mit Stift und Standardmuster, im PDF genau. | `kontur()` um beide Felder ergänzen (CSS-Klasse dann weglassen, sonst gewinnt die Klasse). | klein | mittel |
| 4.11 | `Grafik/Schnittgrafik.cs:144–147` | Achslinien in Schnitt/Ansicht mit festem Massstab 50 für den Überstand; «ohne Überstand» nur für Klasse `Rohr`/`Kanal` wörtlich statt `IstArt("Leitung")`. | Überstand bei 1:20/1:100 nicht 3 mm Papier; Unterklassen von Leitungen bekämen Überstand. | Massstab der Ansicht durchreichen, `IstArt`. | klein | tief |
| 4.12 | `Grafik/HlksGrafik.cs:288` | Dämmpunkt in grob: Rand `#000000` fest (nicht Planfarbe). | Im dunklen Design schwarzer Rand auf dunklem Grund. | `Farbe = null` (Planfarbe). Nicht geändert, weil «gelber Punkt» ein Entscheid ist und der Rand dazu gehört – Frage 4. | klein | tief |
| 4.13 | `Grafik/HlksGrafik.cs:516–538` (`RichtungenBei`), `:85, 150, 661` | `RichtungenBei` läuft je senkrechtes Stück ohne Verbindung über alle Elemente; `HlksMasse.Von` wird je Leitung bis zu siebenmal je Plan gerechnet (Plan, Leitung, TextBreite 2×, Decken, DaemmGrafik, Steigpunkte). | Bei grossen Plänen messbar, nicht kritisch. | Masse je Kontext zwischenspeichern (`ConditionalWeakTable` wie `Decken`). | klein | tief |
| 4.14 | `js/ansichten/grundriss.js:176–190` | `knotenVon` durchsucht je Id alle Knoten; `fremdAnwenden` ruft es je fremder Id. | Grosse fremde Änderung (z. B. 2000 Elemente) × 10000 Knoten ruckelt kurz. | Einmal eine Map Id → Knoten je Neuzeichnen. | klein | tief |
| 4.15 | `Grafik/HlksGrafik.cs:309–310, 414–418` | Doppelte `<summary>`-Blöcke (Text zu `Raender` steht über `AchseAnthrazit`, Text zu `Steigleitung` über `SymbolRadius`). | Nur Lesbarkeit. | Kommentare zuordnen. | klein | tief |
| 4.16 | `Grafik/HlksKoerper.cs:34, 210–216` und `DaemmGrafik` | Dämmung im 3D nur im Detailgrad fein, hell aufgehellt mit Deckkraft 0.35; im Grundriss mittel und fein, Rohrfarbe mit 0.15. | Uneinheitlich seit 12.11.; kein Fehler gegen einen Entscheid. | Frage 1. | klein | mittel |

## 5. Offene Fragen an Christian

1. **Dämmung im 3D** wie im Grundriss? Heute: 3D nur «fein», hell aufgehellte Farbe, Deckkraft 0.35; Grundriss «mittel» und «fein», Rohrfarbe, Deckkraft 0.15.
2. **Kreuzende Leitungen mit Gefälle bzw. Etage** (4.1): Soll die Höhe an der Kreuzung gelten (empfohlen)? Und sollen Formteile und Steigleitungen ebenfalls verdecken
   (heute ausdrücklich «noch nicht»)?
3. **Steigleitung neben waagrechter Leitung** (4.2): Was liegt im Grundriss oben – die Steigleitung, sobald sie über die waagrechte hinausläuft?
4. **Dämmpunkt in grob**: Rand schwarz fest oder Planfarbe (im dunklen Design hell)? Verdeckte Dämmkanten unter dem Boden sind jetzt in Rohrfarbe fein mit Muster
   «strich» (3/1 mm); die verdeckten Kanten in DaemmGrafik nutzen «strichfein» (0.8/0.5 mm) – einheitlich «strichfein»?
5. **Stift «fein» 0.10 mm, Abwasser 0.20 mm, Lücke 0.5 mm, Deckkraft 0.15** sind als ANNAHME Hauptlauf vermerkt – so belassen?
