# Gesamtprüfung 13.11.2026 – Bereich architektur_aussparungen (Runde 2)

Zweig `worktree-agent-aff091d6b9633dfc6`, Ausgangsstand `4dd8149`. Geprüft: `src/Hlks.Kern/Architektur/`, `Huelle/Raumgrenzen.cs`,
`Aussparungen/` (Vorschlag, Daten, Abgleich, Kennzeichnung, Befehle/Prüfung), `Kollision/Durchgaenge.cs` (Projektion),
`Grafik/ArchitekturGrafik.cs` (Zeilen 1–758), `Grafik/AussparungGrafik.cs` (Zeilen 1–400), `js/werkzeuge/wand.js`, `raum.js`,
`raumtrennung.js`, `oeffnung.js`.

**Nicht bzw. nur überflogen** (ehrlich): `AussparungMaterial.cs` (Wortliste), `AussparungIfc*.cs` (Export, Rundreise),
`AussparungPflege.cs`, Massketten und Sinnbild, `RaumgrenzenAbfragen.cs`/`RaumgrenzenBefehle.cs`, `Treppenlage.cs`, Stütze/Träger in
`ArchitekturGrafik.cs` (ab Zeile 759), die Freistellung der Beschriftung in `AussparungGrafik.cs` (ab Zeile 400).

## 1. Kurzfazit

Die Rechenkerne des Bereichs sind sorgfältig gebaut; die nachgerechneten Stellen (Projektion des Leitungsquerschnitts auf das Bauteil,
Koten des Aussparungskörpers, Kettenlücke, Deckung fester Aussparungen, Azimut und Flächen der Raumgrenzen, Planwinkel, Türaufschlag
beim Spiegeln) stimmen. Zwei echte Geometriefehler sind behoben: **schräge Wandecken** standen als Zacken über die Nachbarwand vor
(Wandumriss, Deckenumriss, 3D), und **`elemente_drehen` drehte platzierte Familien nicht mit** (nur der Einfügepunkt wanderte). Dazu
zwei Kleinigkeiten (Türvorschau um die halbe Wanddicke versetzt, Absturz statt Fehlermeldung). Offen bleiben vor allem das Spiegeln
von Familien, Inseln bei `raum_erkennen` und die Frage, ob eine Leitung, die im Bauteil an einem Bogen endet, eine Aussparung über die
ganze Bauteildicke bekommen soll.

## 2. Korrigiert

### 2.1 Schräge Wandecken ohne Zacken – `5a5a24e`
- **Datei:** `src/Hlks.Kern/Architektur/Wandgeometrie.cs` (`Verlaengerung`, neu `Eckverlaengerung`).
- **Fehler:** Ein Wandende an einer anderen Wand wurde immer um deren halbe Dicke verlängert. Das stimmt nur rechtwinklig. Handbeispiel:
  zwei Wände 200 mm, Ecke bei (5000, 0), Richtungswechsel 45° (stumpfe Ecke 135°). Die Aussenflächen y = −100 und y = x − 5141.4 schneiden
  sich bei x = 5041.4. Mit 100 mm Verlängerung reichte Wand 1 bis x = 5100 (Zacken 58.6 mm über die Fläche der Wand 2), Wand 2 bis
  y = −141.4 (Zacken 41.4 mm). Gemessen vorher: Umriss mit den Punkten (5100, −100), (5100, −41.4), (5000, −141.4), Fläche 1 603 430 mm²
  statt 1 600 000 mm². Sichtbar im Grundriss (dicker Wandumriss), im 3D-Körper der Wand und im Umriss von `decke_erzeugen`.
- **Korrektur:** Verlängerung = (Dicke der anderen / 2 − eigene Dicke / 2 · |cos|) / |sin|, nie negativ – die vordere Ecke des Wandendes
  liegt dann genau auf der abgewandten Fläche der anderen Wand (im Beispiel 41.42 mm, Ecke auf Gehrung geschlossen). Rechtwinklig
  (cos = 0) bleibt es exakt die halbe Dicke der anderen Wand (Spezifikation Modul 1a) – rechtwinklige Modelle ändern sich nicht. Aus dem
  IFC festgehaltene Enden (`verlAnfang`/`verlEnde`) gehen weiter vor.
- **Test:** neu `ArchitekturBefehleTests.SchraegeEcke_WandumrissOhneZacken` (Verlängerung 41.421, Fläche 1.6 m² ± 10 mm², kein Punkt
  ausserhalb der Aussenflächen). Filter `ArchitekturBefehleTests|FamilieBefehleTests|RaumBegehen|RaumgrenzenWaermebruecken|TuerDarstellung|VorwandTests|IfcArchitekturTests`:
  126 von 126 grün; dazu `Huelle|IfcArchitektur|Raumgrenzen|Wand|IfcRaeume|Planwinkel`: 357 von 357 grün; `ArchitekturB1Befunde|ArchitekturGrafikTests|DeckeBefehleTests|AnschlussWandDecke|…`: grün.

### 2.2 `elemente_drehen` dreht platzierte Familien mit – `4ec0f1c`
- **Datei:** `src/Hlks.Kern/Architektur/Bearbeiten.cs` (`ElementeDrehen`).
- **Fehler:** Nur Textnotiz und Symbol bekamen den Drehwinkel. Bei einer platzierten Familie (Heizkörper, Apparat, Formteil; Eigenschaft
  `drehung` bzw. `ausrichtung`) wanderte nur der Einfügepunkt um den Drehpunkt, der Körper behielt seine Richtung. Gemessen: HK-1 mit
  Drehung 0 um (0, 0) um 90° gedreht → Punkt (−3000, 5000), `drehung` weiter 0.
- **Korrektur:** `drehung` + Winkel; steht eine `ausrichtung` (geht vor), werden ihre Achsen x und z um die Senkrechte gedreht. Türen
  und Fenster sind ausgenommen (sie folgen der Wand). Gilt auch für «Drehen mit Kopieren».
- **Test:** neu `FamilieBefehleTests.Drehen_PlatzierteFamilieDrehtIhreLageMit` (Handrechnung: Vorlauf-Anschluss (4950, 3950, 120) →
  (−3950, 4950, 120), Drehung 180°). Filter wie 2.1: grün.
- **Nicht nachgeführt:** die Beschreibung von `elemente_drehen` (Kern und gleichlautend `Hlks.Mcp/WerkzeugeArchitektur.cs`) nennt die
  Familien noch nicht; OBERFLAECHE.md hat dazu keinen eigenen Abschnitt.

### 2.3 Türvorschau mit Drehpunkt auf der Wandfläche – `3841769`
- **Datei:** `src/Hlks.Web/wwwroot/js/werkzeuge/oeffnung.js` (`oeffnungVorschau`).
- **Fehler:** Die Vorschau zeichnete Türblatt und Aufschlagbogen ab der Wandachse, der Kern (`ArchitekturGrafik.Tuer`) ab der Wandfläche der
  Aufschlagseite. Die gesetzte Tür sprang gegenüber der Vorschau um die halbe Wanddicke.
- **Korrektur:** Drehpunkt und Bogenanfang bei q = ± Dicke / 2. Nachgerechnet (Wand entlang +x, 200 mm, Tür 800 bei x = 1000, links,
  Band am Anfang): Drehpunkt (600, 100), Spitze (600, 900), Bogen ab (1400, 100) – wie der Kern.
- **Test:** kein neuer (reine Vorschau); mit `node` einmal ausgegeben und gegen die Kernwerte verglichen.

### 2.4 Drehen/Spiegeln mit `kopieren` ohne `elemente`: Fehler statt Absturz – `930cf27`
- **Datei:** `src/Hlks.Kern/Architektur/Bearbeiten.cs` (`Bearbeitung.ZielElemente`).
- **Fehler:** `p["elemente"]!.DeepClone()` – fehlte der Parameter, gab es eine NullReferenceException statt der Meldung mit Vorschlag.
- **Test:** `KopierenOptionenTests.Drehen_Kopieren_OriginalBleibt_KopieGedreht` um den Fall ergänzt; Filter `KopierenOptionen`: 3 von 3 grün.

## 3. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 1 | `Architektur/Bearbeiten.cs` ~274 (`ElementeSpiegeln`) | Platzierte Familien werden beim Spiegeln nur am Punkt gespiegelt; `drehung`/`ausrichtung` bleiben. | Gespiegelte Nasszelle: Apparate stehen am richtigen Ort, schauen aber in die alte Richtung; Anschlüsse liegen falsch. | Entscheid nötig: Eine Spiegelung ist keine Drehung – entweder nur die Richtung spiegeln (drehung' = 2 φ − drehung, Körper bleibt «rechts»), oder gespiegelte Ausführung der Familie (links/rechts) zulassen. | mittel | mittel |
| 2 | `Architektur/WandBefehle.cs` ~236 (`RaumErkennen`) | Freistehende Wände im Raum werden nicht abgezogen (nur Hinweis), obwohl `RaeumeAusWaenden.MitInseln` (IFC-Weg) das über einen Steg der Breite 0 kann. | Raumfläche um die Wandfläche zu gross; die Raumgrenzen kennen die Innenwand nicht. | `MitInseln` auch hier verwenden (nach `AufLinien`); Bänder von Raumtrennlinien dabei nicht als Insel zählen. `Raumtrennungen.Teilen` (Zeile ~90) wirft Löcher ebenfalls weg. | mittel | mittel |
| 3 | `Aussparungen/AussparungVorschlag.cs` 742–764, 781 (`Fortgesetzt`, `Verlaengert`) | Endet die Leitung im Bauteil an irgendeinem Formteil, wird die Achse 5 m verlängert und die Aussparung über die ganze Bauteildicke vorgeschlagen – auch bei einem **Bogen** (Leitung tritt in die Wand und geht darin hoch). | Aussparung durch die ganze Wand, obwohl die Leitung auf der anderen Seite nicht austritt. Gewollt ist der Fall T-Stück im Deckenbereich (Rang 5, 31.10.2026). | Nur verlängern, wenn das Formteil bzw. die nächste Leitung in derselben Richtung weiterführt (Abzweig, Muffe, Reduktion); sonst wie bisher «beginnt/endet im Bauteil». | klein | mittel (Entscheid) |
| 4 | `Architektur/Wandgeometrie.cs` 44–46 (`FreiVon`/`FreiBis`) | Die freie Länge für Öffnungen ist «Länge − Verlängerung». Bei schrägem Anschluss liegt die Wand auf einer Seite bis (D_andere/2)/sin + (D_eigen/2)/tan in der anderen Wand (45°, 200 mm: 241 mm), und endet die Wand an der Fläche statt an der Achse der anderen, wird zu viel abgezogen. | Eine Öffnung dicht an einer schrägen Ecke wird angenommen, obwohl sie in die Nachbarwand reicht (bzw. rechtwinklig grundlos abgelehnt). Selten. | Freie Länge aus dem Schnitt des Wandbands mit den Nachbarbändern statt aus der Verlängerung. | mittel | tief |
| 5 | `Architektur/Wandgeometrie.cs` (`Eckverlaengerung`) | Spitze Ecken (unter 90°) werden mit rechteckigen Wandenden stumpf geschlossen (Fase statt Spitze); ungleich dicke Wände schräg: die Ecke der dickeren steht weiter vor. | Nur Darstellung; kein Vorstehen mehr über die Nachbarfläche bei gleicher Dicke. | Echte Gehrung als Polygonecke statt Rechteck (grösserer Umbau von `Wandlage.Band`). | gross | tief |
| 6 | `Architektur/Raumtrennung.cs` 46–60 (`AufLinien`) | Eckpunkte im Band werden lotrecht auf die Linie gezogen. Kreuzt die Linie die Raumkante schräg, verlässt der Punkt die Raumkante (bis 0.5 mm), die beiden Teile haben dort nicht denselben Eckpunkt. | Fehlfläche unter 0.1 mm² je Kreuzung – in der Fläche unsichtbar; Fang auf die Ecke trifft zwei Punkte. | Punkt auf den Schnitt Linie × Raumkante setzen statt auf das Lot. | klein | tief |
| 7 | `Aussparungen/AussparungVorschlag.cs` 1054–1071 (`Zusammenfassen`) | Nach jeder Vereinigung beginnt die Paarsuche von vorn (O(n³) im schlechtesten Fall je Bauteil), `Umriss` wird je Vergleich neu gerechnet. | Bei einer Decke mit einigen hundert Durchgängen spürbar, sonst nicht. Nicht gemessen. | Umriss je Cluster halten, nach dem Vereinigen bei i weitermachen. | klein | tief |
| 8 | `Grafik/ArchitekturGrafik.cs` 126–133 (`Wandteile`) | `belegt` wird je Wand mit allen bisherigen Wänden neu vereinigt und jede Wand gegen das Ganze abgezogen. | Je Geschoss quadratisch in der Zahl der Wände (grosse IFC-Geschosse). Nicht gemessen. | Nur Nachbarn im Raster (wie `Verlaengern`) abziehen. | mittel | tief |
| 9 | `Kollision/Durchgaenge.cs` 150 (`DurchbruchKreis`, rechteckig) | Für Rechteckkanäle ist der «umschliessende Kreis über die Tiefe» = Kreis + 2 × Verschiebung, also eine obere Schranke, nicht der kleinste Kreis. | Von Hand gewählte Kernbohrung um einen schräg querenden Rechteckkanal fällt etwas zu gross aus (sichere Seite). | Kleinsten Kreis um die acht Eckpunkte rechnen. | klein | tief |
| 10 | `Grafik/ArchitekturGrafik.cs` 288 (Fensterbank) | Annahme «links der Wandrichtung = aussen». | Fensterbank bei der Hälfte der Wände innen. | Seit es die Raumgrenzen gibt: Aussenseite aus der Raumgrenze der Wand nehmen (Rückfall wie heute). | klein | tief |
| 11 | `Architektur/Treppenanalyse.cs` ~150 | Regionen werden nur nach Höhe geordnet; zwei Flächen gleicher Höhe (zwei Läufe in einem Körper, geteiltes Podest) kommen in zufälliger Folge, die Gehrichtung springt. | Stufenkanten bzw. Lauflinie solcher Treppen falsch. Heuristik, durch Echtdaten gedeckt (Bernstrasse). | Bei gleicher Höhe nach Nähe zum Vorgänger ordnen. | klein | tief |
| 12 | `Architektur/WandBefehle.cs` ~230 | Klick genau auf eine Raumtrennlinie: Meldung «Die Wände … umschliessen den Punkt nicht». | Irreführend. | Eigene Meldung «Der Punkt liegt auf einer Raumtrennlinie». | klein | tief |
| 13 | `js/werkzeuge/wand.js` 50–53 | Vorschau mit Bezugslinie links/rechts versetzt jedes Stück einzeln; an Ecken fehlt der Schnittpunkt, den der Kern rechnet (`Versetzen`). | Vorschau an Ecken mit Lücke bzw. Überstand; das Ergebnis stimmt. | Eckpunkte in der Vorschau wie im Kern schneiden. | klein | tief |
| 14 | Repository | Bei jedem `git commit` in der Arbeitskopie: `error: failed to delete '.git/worktrees/wt_b1'` bzw. `wt_b2` (Permission denied). | Harmlos (verwaiste Einträge, vermutlich von OneDrive gesperrt), stört aber jede Ausgabe. | Hauptlauf: `git worktree prune` bei ruhendem OneDrive bzw. die zwei Ordner von Hand entfernen. | klein | tief |

Nachgerechnet und in Ordnung (Auswahl):
- **Aussparungsgrösse** (`Durchgaenge.Projektion`): rund schräg unter θ durch die Dicke t: D/cos θ + t · tan θ + 2 × Spiel. Beispiel Rohr Ø 110
  mit 20 mm Dämmung (D = 150), Wand 200 mm, θ = 30°: 150/0.866 + 200 · 0.577 = 288.7 mm in Neigungsrichtung, quer 150 mm, je + 2 × Spiel,
  dann auf ganze cm aufgerundet. Die Dämmdicke kommt aus der Leitung selbst (`daemmdicke`), nicht aus der Grafik «Dämmung bis Formteile»
  vom 12.11.2026 – die Änderung berührt die Aussparung nicht.
- **Koten** (`Aussparungslage.Koten`): halbe Ausdehnung = |U_z|·b/2 + |V_z|·h/2 + |Achse_z|·t/2 (Quader) bzw. √(1 − a_z²)·d/2 + |a_z|·t/2 (Zylinder).
- **Kette** (`Luecke`, `Kettenbedarf`): Lücke axial × |cos|, Tiefe = |Austritt − Eintritt| × |cos| zur Eintrittsnormalen.
- **Längs im Bauteil** (`Laengs`): |n · Richtung| < 0.5 = flacher als 30° zur Fläche.
- **Raumgrenzen:** Aussennormale (e_y, −e_x) bei Ring im Gegenuhrzeigersinn; Azimut = atan2(n_x, n_y) − Nordwinkel; Löcher im Uhrzeigersinn
  (Raum bleibt links); Terrain-Teilung und Öffnungsabzug je Höhenteil.
- **Planwinkel** (`AusRichtung`): 30° → −30°, 60° → +30°, 45° → +45°, 135° → +45° (Bereich −45 < w ≤ 45).
- **Spiegeln von Türen:** Aufschlag- und Bandseite über die gespiegelten Vektoren gegen die neue Wandrichtung – stimmt für Wand + Tür und
  für die Tür allein.

## 4. Offene Fragen an Christian

1. **Spiegeln von Apparaten/Familien** (Punkt 1): Soll eine gespiegelte Familie nur gedreht werden (Körper bleibt gleich) oder gibt es
   gespiegelte Ausführungen (links/rechts)?
2. **Leitung endet im Bauteil an einem Bogen** (Punkt 3): Aussparung über die ganze Dicke (heute) oder keine bzw. nur Hinweis «Schlitz von Hand»?
3. **Schräge Ecken:** Die neue Verlängerung schliesst stumpfe Ecken auf Gehrung und spitze Ecken stumpf (Fase). Reicht das, oder soll die
   spitze Ecke als Spitze gezeichnet werden (Punkt 5)?
4. **Raumgrenzen in Innenmassen** (Kantenlänge × lichte Raumhöhe, Spezifikation Modul 1c): Welche Bezugsmasse verlangt die Heizlastrechnung
   für Aussenbauteile? Ich habe dazu keine Normstelle zur Hand und nichts geändert – nur als Frage, falls die Flächen später in die
   Heizlast gehen.
5. **`raum_erkennen` mit freistehenden Wänden** (Punkt 2): Fläche abziehen wie beim IFC-Weg?
