# Nachbearbeitungsliste HLKS-CAD

Bewusst zurückgestellte Arbeiten (Entscheid Christian), die in keinem laufenden Modul gebaut werden. Jeder Eintrag ist
so beschrieben, dass er ohne den damaligen Chat aufgenommen werden kann. Vor dem Start: Einordnung (eigener Nachtrag
oder Teil eines Moduls) mit Christian klären, Spezifikation in `entwurf/` ergänzen, Zuteilung nach `CLAUDE.md`,
Protokoll in `zuteilung.md`. Erledigte Einträge mit Datum und Ort (Modul, Testbericht) als erledigt markieren.

| Nr. | Eingetragen | Thema | Herkunft | Stand |
| --- | --- | --- | --- | --- |
| N1 | 18.10.2026 | IFC über zwei Punktpaare ausrichten | Abnahme 1b, Befund Bernstrasse | zurückgestellt |
| N2 | 18.10.2026 | Eigenschaftenpalette und Projektbrowser verschiebbar | Wunsch Christian | **erledigt 19.10.2026** mit W1/W2 (`entwurf/fenster-revit.md`, geprüfter Stand 72b16dc) |
| N3 | 19.10.2026 | Fehlende Bedienorte aus dem Funktionsverzeichnis | Bedienungsanleitung, `berichte/anleitung/inventar-notizen.md` Abschnitt 3 | **erledigt 20.10.2026** (Merge N3, Bericht in `zuteilung.md`) |
| N4 | 20.10.2026 | PDF-Leser: Dezimalzahlen, Zeilenleser der Datenblatt-Tabellen | Grundausstattung R1, R2, R7 | **erledigt 20.10.2026** (Leser 2); Neubelegung der Familien offen |
| N5 | 20.10.2026 | KI-Palette: abgelehnte Werkzeugaufrufe als «ok» | Abnahmelauf B6 | **erledigt 20.10.2026** |

## N1 – IFC über zwei Punktpaare ausrichten

**Zurückgestellt** von Christian am 18.10.2026 («wir verschieben das auf eine Nachbearbeitungsliste»), nach der Abnahme
von Modul 1b.

**Anlass:** Befund der 1b-Abnahme (`berichte/modul-1b-testbericht.md`, «Wichtigster Befund»). Das H+K-IFC von
Bernstrasse 42 (Revit 2025, IFC2x3, `hlks_hk_2026-02-10.ifc`) hat denselben Gebäudeursprung wie das Archicad-IFC, sein
Inhalt liegt aber rund 30 m östlich und 20 m nördlich daneben. Weder `ausrichtung` = `projekt` noch `gebaeude` trifft
(Inhaltsvergleich 0 % / 0 %), und es gibt keinen Koordinationskörper. Heute geht es nur über den Parameter `bezug`
{x, y, z, drehung} mit Zahlen.

**Alternative, die das Werkzeug unnötig macht:** Der Fachplaner liefert gemeinsame Koordinaten oder einen
Koordinationskörper (Prüfliste 1b, Punkt 1).

**Umfang (Vorschlag):**
- **Kern** (`src/Hlks.Kern/Import/IfcAusrichtung.cs`, `IfcUebernahme.cs` – ändert nur der Hauptlauf, die Rechnung an
  hlks-kern): Aus zwei Punkten im bestehenden Modell (Projektkoordinaten, mm) und den zwei entsprechenden Punkten in der
  IFC-Datei (Weltkoordinaten der Datei) den Bezug {x, y, z, drehung} rechnen: Starrkörper in der Ebene, Drehung aus den
  Richtungen P1→P2, Verschiebung aus Punkt 1, Höhe aus Punkt 1 oder aus einer Kote. Restfehler aus Punkt 2
  (Abstandsdifferenz der Paare als Massstabsfehler, Lagefehler von Punkt 2 nach der Transformation, in mm), Warnung ab
  10 mm. Neue Ausrichtungsart `punktpaare` mit Lagebericht (Punkte, Restfehler, Inhaltsanteil danach), gespeichert in
  der Unterlage und für neue Fassungen wiederverwendet wie die anderen Wahlen.
- **MCP:** Punktpaare als Parameter von `ifc_pruefen` und `ifc_uebernehmen` (eine Tür, Trockenlauf zeigt das Ergebnis).
- **Oberfläche** (hlks-modul, Muster: Werkzeug «Kalibrieren» `src/Hlks.Web/wwwroot/js/werkzeuge/kalibrieren.js`): im
  IFC-Dialog bei Abweichung die Option «Über Punktpaare ausrichten»; Punkte im Grundriss bzw. 3D fangen (Wandecken der
  Architektur bzw. des IFC-Hintergrundnetzes), Vorschau, Restfehler anzeigen. Nichts wird ohne ausdrückliche
  Bestätigung übernommen.
- **Tests** (hlks-test): mit `tests/Hlks.Kern.Tests/IfcLageTestdatei.cs` (gedreht und verschoben) und dem echten Fall
  Bernstrasse (Gebäudeecken in beiden Modellen). Echte Dateien nur unter `%USERPROFILE%\HLKS_CAD_testdaten`, nie ins Repo.

**Fertig, wenn** das Bernstrasse-H+K-IFC über zwei Punktpaare auf dem Archicad-Modell liegt (Inhaltsanteil > 80 %,
Restfehler genannt), alle Tests grün sind und der Lagebericht die Punktpaare transparent zeigt.

**Aufwand (Schätzung):** 1–2 Arbeitstage.

## N2 – Eigenschaftenpalette und Projektbrowser verschiebbar

**Eingetragen** auf Wunsch von Christian am 18.10.2026 («bitte in die Nachbearbeitung die Verschiebbarkeit von
Eigenschafts- und Projektbrowser aufnehmen»), während des Baus von Modul 2.

**Heute:** Beide Paletten stehen fest links, die Eigenschaftenpalette oben, der Projektbrowser unten
(`src/Hlks.Web/OBERFLAECHE.md`, Punkt 1 «Bedienung wie Revit»).

**Ziel (wie in Revit):**
- Paletten an ihrer Titelleiste ziehen: links oder rechts andocken, übereinander oder als **Reiter** zusammengelegt,
  oder **frei schwebend** über der Zeichenfläche; Doppelklick auf die Titelleiste dockt an den letzten Platz zurück.
- Grösse über Trennlinien ziehen (Breite der Seitenleiste, Aufteilung der Höhe zwischen den Paletten).
- Ein- und Ausblenden über Register Ansicht › Benutzeroberfläche; **«Anordnung zurücksetzen»**.
- Die Anordnung gilt je Benutzer und Rechner (Browserspeicher bzw. Benutzereinstellung, **nicht im Projekt**) und
  übersteht ein Neuladen.
- Prüfen: Palette in ein eigenes Fenster lösen (zweiter Bildschirm), synchron über den Ereignisstrom.
- Gilt auch für spätere Paletten (z. B. Befund-Browser aus Modul 2, Systembrowser aus Modul 3).

**Umfang (Vorschlag):** reine Oberfläche (hlks-modul), kein Kern. Dichte und Design nach `OBERFLAECHE.md`; Pflichtenheft
um den Abschnitt «Paletten anordnen» ergänzen. Tests: Node (Zustand der Anordnung, Speichern und Laden), Browser (ziehen,
andocken links/rechts, als Reiter zusammenlegen, schweben, Grösse ändern, Neuladen behält die Anordnung, Zurücksetzen);
alle bisherigen Browser-Reihen müssen grün bleiben.

**Fertig, wenn** Eigenschaftenpalette und Projektbrowser links und rechts angedockt, als Reiter zusammengelegt und
schwebend benutzt werden können, ihre Grösse ziehbar ist, die Anordnung ein Neuladen übersteht und sich zurücksetzen
lässt.

**Aufwand (Schätzung):** 1–2 Arbeitstage.
## N3 – Fehlende Bedienorte (aus dem Funktionsverzeichnis)

**Eingetragen** vom Hauptlauf am 19.10.2026: Beim Funktionsverzeichnis für die Bedienungsanleitung fanden sich Funktionen,
die im Kern gebaut, aber in der Oberfläche nicht erreichbar sind (`berichte/anleitung/inventar-notizen.md`, Abschnitt 3;
`berichte/anleitung/luecken.json`). Gebaut in der Nacht 19./20.10.2026 auf Christians Auftrag «arbeite bis 08:00 durch».

1. **Einlage- und Grundleitungspläne anlegen:** Knöpfe «Einlageplan» und «Grundleitungsplan» im Register Ansicht, Gruppe
   Erstellen (bzw. Flyout «Grundriss ▾»), rufen den Kernbefehl einlageplaene_anlegen (Dialog: Geschoss bzw. Bauteil
   Decke/Bodenplatte/Wand, Name; Vorschlag nach Planschlüssel «Einlagen D.ü.2.UG»).
2. **Projektinformationen bearbeitbar:** Der Dialog «Projektinformationen» wird bearbeitbar (heute nur lesend): alle
   Felder der Klasse Projektinformation aus dem Katalog, darunter Rohrbezeichnung (NW/DN), Masseinheiten je Gewerk,
   Projektnummer, Objektzeilen, Phase; Änderung über die Kernbefehle (Rückgängig, Herkunft). Das Element Projektinformation
   erscheint im Projektbrowser (Gebäude › Projektinformationen) bzw. wird über Verwalten › Projektinformationen erreicht.
3. **Kollisionsliste und Aussparungsliste** im Dialog «Liste anlegen» (die Listenansicht kann sie schon zeichnen) und
   **CSV-Ausgabe** jeder Listenansicht (Knopf «CSV …» in der Listenansicht bzw. Kontextmenü), UTF-8 mit BOM, Semikolon.
4. **Tastenkürzel-Liste (KS)** nach Register: «Beschriften», «Einfügen», «Hülle», «Koordination», «Familie» in
   REGISTER_NAMEN aufnehmen, sodass kein Kürzel unter «Allgemein» landet, das einem Register gehört.

**Fertig, wenn** alle vier Orte in der Oberfläche erreichbar sind, Tests (Node, Web, Browser) grün sind und das
Funktionsverzeichnis die Lücken nicht mehr führt.

## N4 – PDF-Leser: Dezimalzahlen mit getrennten Zeichenläufen

Befund 20.10.2026 (Grundausstattung R7, Geberit-HDPE-Katalog Ausgabe 2020, Händlerablage Reece): Der Unterlagen-Leser liest
Dezimalzahlen dieses PDF als «4 .5» statt 4.5 (Ganzzahl und Nachkommastelle in getrennten Zeichenläufen). Die KI konnte
solche Werte nur ohne Quellenverweis eintragen (Bogen, T-Stück, Reduktion, Rohr di/s). Ähnlich bei R1/R2: Werte aus
Datenblatt-Zeilen wurden dem falschen Typ zugeordnet («Zeilenleser», z. B. z45 d15/d18/d22 als 15/18/20 statt 16/17/21).

1. Zeichenläufe innerhalb einer Zelle zusammenführen, wenn der Abstand kleiner als die Zeichenbreite ist (Punkt bzw. Komma
   zwischen Ziffern) – Test mit einem selbst erzeugten PDF, das Zahl und Nachkommastelle getrennt setzt.
2. Zeilenleser der Datenblatt-Tabellen (Geberit «Erstellt mit Geberit Produktdaten»): Zuordnung Zeile ↔ Artikel prüfen.

**Fertig, wenn** die Befunde der R7-Unterlage die Dezimalwerte als Zahl mit Zelle liefern und die Familien R1, R2, R7 mit
Quellenverweis neu belegt werden können.

**Erledigt: 20.10.2026** (Leserteil; PDF-Leser Version 2). Ursachen: (1) Die Schrift des HDPE-Katalogs bildet den Punkt auf « .» ab
(Leerraum plus Punkt in einer Glyphe) – PdfPig setzte «101 .4» und «Art . no .» zusammen, keine getrennten Läufe. Jetzt fällt der Leerraum
einer solchen Glyphe weg; zusätzlich werden echte getrennte Läufe («101» «.4», «4» «.» «3», «147» «,6») zusammengeführt, wenn jede Lücke
kleiner als eine Zeichenbreite ist (Gegenprobe: echte Spalten bleiben getrennt). (2) «Zeilenleser»: die Zeilen selbst waren richtig; die
Tabellenerkennung nahm den Fliesstext darüber («Verwendungszwecke | Eigenschaften», «Werkstoff | Stahl unlegiert …») in den Block, dessen
lange Zellen die Spalten verschmolzen («20603 15 18 mm» in einer Zelle, «1.7 cm 20 St.» in einer anderen, kein Kopf). Die KI musste die
ganze Zeile zitieren, und die Quellenprüfung nannte als «Datenblattwert» die nächstliegende Zahl der Zeile (15/18/20). Jetzt trennt eine
Fliesstextzeile über bzw. unter den vollen Zeilen den Block; die Tabelle hat 9 Spalten mit Kopf «Art.-Nr. | DN | d, ø | arc | L | Z | VE1 |
VE2 | VE3», z45 d15/d18/d22 = 1.6/1.7/2.1 cm. Tests: `tests/Hlks.Kern.Tests/UnterlagenPdfN4Tests.cs` (selbst erzeugte PDF, echte Unterlagen nur
lokal). **Offen:** Leserversion 2 liest zwischengespeicherte PDF-Befunde neu – Befund-Ids können sich verschieben (z. B. Bogen-Datenblatt ab
der Tabelle); Quellen der Familien R1, R2, R7 (und weiterer mit PDF-Quellen) mit familie_pruefen nachprüfen und neu belegen (Hauptlauf).
Kopf der HDPE-Rohrtabelle enthält weiter die Zeilen darüber («Technical data Product material Art. no.», «PE-HD DN»).

## N5 – KI-Palette: abgelehnte Werkzeugaufrufe als «ok» angezeigt

Befund Abnahmelauf B6 (20.10.2026): Lehnt der Kern `unterlagen_lesen` ab (Pfadprüfung bzw. «mit Internet nur Herstellerunterlagen»),
zeigt die Palette den Aufruf als «ok»; `ifc_pruefen` und `befunde` erscheinen richtig als «abgelehnt». Vermutlich liefert der
MCP-Server bei `unterlagen_lesen` den Fehler als normales Ergebnis (ohne Fehlerkennung). **Fertig, wenn** jeder abgelehnte Aufruf
in Palette und Auftragsprotokoll als abgelehnt erscheint (Test über alle Werkzeuge mit HlksFehler).

**Erledigt: 20.10.2026.** Ursache: Kein Werkzeug setzte isError; die Palette erkannte Ablehnungen nur an «fehler» oben im JSON.
`unterlagen_lesen` meldet Fehler je Unterlage (verschachtelt) und galt darum als «ok»; das Auftragsprotokoll vermerkte nur is_error. Lösung:
eine Regel im Kern (`Werkzeugergebnis.MeldetFehler`: «fehler» als nicht leerer Text bzw. Liste oder angenommen false), benutzt vom MCP-Filter
`Fehlerkennung` (isError für alle Werkzeuge), der Palette, dem Auftragsprotokoll («Werkzeug abgelehnt: name – Meldung») und der Kommandozeile;
`unterlagen_lesen` setzt «fehler»/«vorschlag» (und «gelesen») oben und zuerst, sobald eine Unterlage nicht gelesen wird. Nebenbefund behoben:
`synchronisieren` meldet «fehler»: [] und galt in der Palette als abgelehnt. Tests: `tests/Hlks.Mcp.Tests/FehlerkennungTests.cs` (alle 130
Werkzeuge über das Protokoll mit erzwungener Ablehnung), `tests/Hlks.Web.Tests/NachbearbeitungN5Tests.cs`, `WerkzeugergebnisTests.cs`.

## N6 – Türen aus Archicad-IFC 5,19 m hoch (ragen durch Decke und Dach)

Befund Christian 21.10.2026 (3D, Projekt «Test_praxis», IFC «440_Bernstrasse 42 3D Modell.ifc», IFC2X3, Archicad 27): «türen über
gebäude». Hauptlauf:
- 23 von 57 `IfcDoor` tragen `OverallHeight` 5.19 m; die Öffnung (`IfcOpeningElement`, Rechteckprofil 0.94 × 5.19) ebenso.
- Der Türkörper selbst (Body, `MappedRepresentation`) ist nur 2.15 m hoch (z 0 … 2.15, Breite ±0.47).
- KICAD übernimmt `OverallHeight` («hoehe» 5190). Widersprüchlich ist hier das IFC, nicht der Leser.

**Fertig, wenn:**
- die Türhöhe aus dem Körper kommt, sobald er deutlich von `OverallHeight` abweicht, und die Herkunft beide Werte nennt;
- ein Prüf- bzw. Importhinweis die Abweichung je Tür meldet;
- die Türen in 3D und Grundriss richtig stehen;
- Türen ohne Körper weiter `OverallHeight` nehmen.

**Umgesetzt: 21.10.2026** (hlks-kern, vom Hauptlauf zusammengeführt).
- **Ursache:** Das IFC ist widersprüchlich. `OverallHeight` (5190) ist die Höhe der Öffnung; sie reicht 3,04 m unter die Tür. Der Türkörper
  selbst ist nur 0 … 2150 mm hoch. Dasselbe gilt im BKW-IFC (Archicad 28) für 15 Türen.
- **Regel** (`src/Hlks.Kern/Import/IfcArchitektur.cs`): Weicht die Körperhöhe um mehr als 400 mm **und** mehr als 15 % von `OverallHeight`
  ab, gilt der Körper. Die Herkunft nennt beide Werte, ein Importhinweis listet jede Tür bzw. jedes Fenster.
  - Die Schwelle stammt aus den echten Dateien: Rahmen, Futter und Bänke machen den Körper bis 298 mm bzw. 12,6 % höher. Die
    widersprüchlichen Fälle weichen 725 … 4160 mm bzw. 20,8 … 66,5 % ab.
  - Gilt auch für Fenster; dort kommt dann auch `bruestung` von der Unterkante des Körpers.
- **Wanddarstellung:** Die Aussparung ist jetzt 2150 mm hoch. Kollision und Aussparungen nutzen weiter das IfcOpeningElement; sein
  unterer Teil wirkt dort nicht.
- **Tests:** `NachbearbeitungN6N7Tests.cs` (synthetisch, IFC4 und IFC2X3) und `NachbearbeitungN6N7EchtTests.cs` (echtes IFC, wird
  übersprungen, wenn die Datei fehlt).
- **Für «Test_praxis»:** Das IFC muss neu übernommen werden (`ifc_uebernehmen`), damit die Türen nachgeführt werden.

## N7 – Zwei Stahl-Unterzüge ragen 3–4 m aus dem Gebäude

Befund Christian 21.10.2026 (gleiches Projekt): «unterzüge neben gebäude». Hauptlauf:
- Betroffen sind UZ-14 (1.OG) und UZ-62 (2.OG), beide «Unterzug-002», Material Stahl.
- Ihre Linie läuft von ca. [2972, −43351] nach [−3964, −36955] bzw. von [3266, −43622] nach [−3670, −37226]. Die Wände enden bei y −39903.
- Die übrigen 71 Träger liegen im Gebäude.
- Ursache offen: Achse bzw. Extrusionsrichtung, Länge aus dem Profil oder Platzierung.

**Fertig, wenn** die Ursache belegt ist, gegen die Geometrie im IFC gerechnet, und die Träger dort liegen, wo das IFC sie zeichnet. Zeichnet
das IFC sie tatsächlich ausserhalb, meldet ein Befund sie.

**Umgesetzt: 21.10.2026** (hlks-kern, vom Hauptlauf zusammengeführt). Es war ein **Leserfehler von KICAD**; das IFC zeichnet die Träger
im Gebäude.
- **Ursache:** UZ-14 = IfcBeam #28246, UZ-62 = #32964, ein lotrechtes H-Profil 220 × 220. Als Trägeranfang nahm KICAD den Schwerpunkt des
  Profils im Grundriss. Dort hat das Profil die Fläche 0, und weit vom Ursprung blieb ein Rundungsrest über der Schwelle. Der
  «Schwerpunkt» lag dadurch rund 15,5 m längs der Achse daneben.
- **Lösung:**
  - Der Trägeranfang ist die Mitte des lotrechten Profils.
  - `Polygone.Schwerpunkt` rechnet ab dem ersten Punkt und erkennt Fläche 0 relativ zur Ausdehnung; das behebt die Ursache auch für
    andere Aufrufer.
- **Tests:** Die alten Werte reproduzieren Christians Koordinaten genau. Alle 73 Träger liegen jetzt in ihrem IFC-Körper.
- **Für «Test_praxis»:** Das IFC muss neu übernommen werden.

## N8 – Schraffuren drehen mit der Wand

Befund Christian 21.10.2026 (Grundriss, schräge Wand mit Kreuzschraffur): «schraffuren sollten mit wand rotieren». Heute liegt das
Schraffurraster achsparallel zum Blatt.

**Fertig, wenn:**
- die Schraffur jeder Wand und jedes Bauteils mit Achse an der Wandachse ausgerichtet ist, auf dem Bildschirm, im PDF und im Schnitt;
- gebogene Wände je Abschnitt ausgerichtet sind;
- Flächen ohne Achse (Decken, Räume) wie bisher bleiben.

**Umgesetzt: 21.10.2026** (hlks-modul, vom Hauptlauf geprüft und zusammengeführt).
- **Kern:**
  - `Stil.MusterWinkel`: Richtung der Achse, [0, 180), 0.001°.
  - `Stil.MusterUrsprung`: Lot vom Ursprung auf die Achse.
  - Beides kommt über `Schraffuren.AnAchse`, für Wände und Unterzüge im Grundriss. Die Winkel der Tabelle gelten gegen die Achse.
- **Anwendung:** auf dem Bildschirm in `plangrafik.js` (`patternTransform`), im PDF in `PdfZeichner.Schraffieren`.
- **Bleiben achsparallel:**
  - Decken, Räume, Füllbereiche;
  - Stützen, deren Achse senkrecht zur Zeichenebene steht;
  - der Schnitt, weil die Wandachse dort waagrecht liegt.
- **Gebogene Wände** kennt das Modell nicht; IFC-Bogenwände gehen als Hintergrund. Bei einer Kette gerader Wände hat jeder Abschnitt seine eigene
  Achse.
- **Nebenbefund behoben:** Die Muster-Ids der offenen Ansichten waren nicht eindeutig. Die Schraffur einer sichtbaren Ansicht blieb leer, wenn
  eine verborgene Ansicht früher im Dokument dasselbe Muster (Massstab, Winkel) hatte. Lösung: `neuerMusterPraefix`.
- **Tests:**
  - `SchraffurAchseTests.cs` (35)
  - Node-Tests «N8 Muster» (8)
  - Browser-Test `js/ansichten/schraffur-achse.browser.mjs` (23 Prüfungen am gerenderten Bild)

## N9 – Ausrichtung der Ansichten: nach Geometrie (Vorgabe) oder nach Himmelsrichtung

Christian 21.10.2026: «bei den ansichten sollte eingestellt werden ob sie nach himmelsrichtung oder nach geometrie ausgerichtet sein
sollen. standard ist geometrie (längste Gebäudekante waagerecht)».

Heute zeigt jede Ansicht das Projektsystem (Projekt-Nord = +y oben). Bei schräg liegenden Gebäuden wie der Bernstrasse steht der Grundriss
deshalb schief.

Entscheide Hauptlauf:
- **Neue Ansichtseigenschaft `ausrichtung`:** `geometrie` (Vorgabe) | `himmelsrichtung`.
  - Gilt für Grundriss-artige Ansichten: Grundriss, Koordination, Einlage- und Aussparungsplan …
  - Schnitt und Ansicht folgen ihrer Linie; 3D sowie Schema und Liste sind nicht betroffen.
- **`geometrie`:** Der Drehwinkel kommt aus der längsten geraden Gebäudekante.
  - Massgebend ist die Achse der längsten Aussenwand des Gebäudes (Angrenzung aussen, über alle Geschosse). Gibt es keine, gilt die längste
    Wand, sonst 0.
  - Der Winkel wird so gewählt, dass diese Kante waagrecht liegt und die Drehung klein bleibt (−45° … 45°).
  - Gespeichert wird er am Gebäude als `planwinkel`, mit Herkunft «längste Gebäudekante W-…, Länge, Richtung». Das geschieht beim Import bzw.
    sobald Wände da sind.
  - Danach wird er nur auf Befehl neu bestimmt oder von Hand gesetzt. Die Ansichten springen also nicht, wenn eine Wand dazukommt.
- **`himmelsrichtung`:** Geografisch Nord liegt oben. Der Drehwinkel kommt aus `Projektinformation.nordwinkel`; ohne Angabe gilt 0, mit
  Hinweis.
- **Nur Darstellung:** Die Modellkoordinaten bleiben.
  - Alles in der Ansicht dreht mit, auch Unterlagen, Schnittlinien und Ausschnitt.
  - Beschriftungen bleiben lesbar, also auf dem Bildschirm waagrecht.
  - Der Nordpfeil zeigt in beiden Fällen geografisch Nord.
- **Zeichnen in einer gedrehten Ansicht:**
  - Orthogonal- und Winkelfang sowie die Koordinateneingabe beziehen sich auf die Ansichtsachsen. Waagrecht gezeichnet heisst damit parallel
    zur Gebäudekante.
  - Die Statusanzeige nennt Modellkoordinaten.
- **Plan-PDF und Planblatt** übernehmen die Ausrichtung der Ansicht.

**Fertig, wenn:**
- Christian im Bernstrasse-Projekt den Grundriss gerade sieht (längste Kante waagrecht);
- er auf «Himmelsrichtung» umschalten kann (Nord oben);
- Zeichnen, Fang und PDF in beiden Fällen stimmen;
- der Nordpfeil richtig zeigt.

**Umgesetzt: 21.10.2026** (hlks-kern, vom Hauptlauf zusammengeführt).
- **Planwinkel** (`Architektur/Planwinkel.cs`): Er kommt aus der längsten Aussenwand und wird beim IFC-Import bzw. bei der ersten Wand
  gesetzt. Neu sind die Abfrage `planwinkel` und der Befehl `planwinkel_bestimmen`.
- **Drehung** je Ausgabeweg an einer Stelle:
  - Bildschirm: Gruppe `gr-welt` in `grundriss.js`, Rückrechnung in `ansichten/ausrichtung.js`.
  - PDF: `PdfZeichner.Drehen`.
- **Beschriftungen** bleiben waagrecht (`Ansichtsausrichtung.Uebernehmen`, Merkmale `Anker` und `Mitdrehen`).
- **Bedienung:**
  - Menüband: Ansicht › Darstellung › «Nach Himmelsrichtung» und «Planwinkel neu bestimmen».
  - Eigenschaften der Ansicht bzw. des Gebäudes.
  - Fang und Bemassung richten sich nach den Ansichtsachsen.
- **Probe:** Das Archicad-IFC der Bernstrasse ergibt 42,7° (W-70), Grundriss und Plan stehen gerade.
- **Navigationswürfel:** VORNE folgt jetzt dem Planwinkel.
- **Grenze:** Wegen ±45° kann die längste Kante auch senkrecht stehen. – Mittlere Maustaste verschiebt in allen Ansichten

Christian 21.10.2026: «mittlere maustaste = plan verschieben».

Befund Hauptlauf:
- Grundriss (`ansichten/grundriss.js`) und Schema verschieben mit der mittleren Taste bereits. Doppelklick mit der mittleren Taste zeigt
  alles.
- Im **3D** (`ansichten/ansicht3d.js`, OrbitControls) zoomt die mittlere Taste (MIDDLE = DOLLY); Verschieben liegt auf der rechten Taste.

**Fertig, wenn:**
- in jeder Ansicht Ziehen mit gedrückter mittlerer Taste verschiebt, auch während ein Werkzeug aktiv ist. Gemeint sind Grundriss, Schnitt bzw.
  Ansicht, 3D, Schema, Plan bzw. Planblatt, Familieneditor, Unterlagen-Ansicht …;
- das 3D wie Revit bedient wird: Mitte verschiebt, Shift + Mitte dreht (Orbit), das Rad zoomt;
- ein Doppelklick mit der mittleren Taste überall gleich alles zeigt.

## N11 – Navigationswürfel in der 3D-Ansicht (analog Revit ViewCube)

Christian 21.10.2026: «In der 3d ansicht ein navigationswürfel anzeigen (analog revit)».

**Fertig, wenn** oben rechts in der 3D-Ansicht ein Würfel steht, der die Kamera mitdreht:
- Flächen OBEN, UNTEN, VORNE, HINTEN, LINKS, RECHTS; Flächen, Kanten und Ecken anklickbar für Standardansichten, mit weichem Übergang;
- Ziehen am Würfel dreht;
- ein Kompassring mit N/O/S/W; geografisch Nord kommt aus `Projektinformation.nordwinkel`;
- ein Haus-Knopf für die Startansicht und das Kontextmenü «Aktuelle Ansicht als Startansicht»;
- «VORNE» richtet sich nach der Gebäudeausrichtung (N9, `planwinkel` des Gebäudes); ohne Wert gelten die Projektachsen.

**N10 und N11 umgesetzt: 21.10.2026** (hlks-modul, vom Hauptlauf zusammengeführt).
- **Mittlere Taste:** In allen Ansichten verschiebt sie, auch während ein Werkzeug aktiv ist. Im 3D gilt: Mitte verschiebt, Shift + Mitte
  dreht, das Rad zoomt zum Zeiger. Ein Doppelklick mit der Mitte zeigt alles, aber nur, wenn dabei nicht gezogen wurde.
- **Würfel:** oben rechts, mit 26 anklickbaren Bereichen (Flächen, Kanten, Ecken) und weicher Kamerafahrt; Ziehen am Würfel dreht.
  - Kompassring mit Nordwinkel.
  - Haus-Knopf und Kontextmenü für die Startansicht.
- **Code und Tests:** `ansichten/maus.js`, `wuerfel-logik.js`, `wuerfel.js`; Node-Tests `maus.test.mjs`, Browser-Reihe
  `maus-wuerfel.browser.mjs` (76 Prüfpunkte).

**Offen:**
1. **Planblatt:** Das PDF läuft im Betrachter des Browsers; dort löst die mittlere Taste dessen Autoscroll aus. Abhilfe wäre ein eigener
   PDF-Betrachter (pdf.js).
2. **Startansicht:** Sie liegt vorerst je Rechner im Browser (`localStorage`). Sie soll als Ansichtseigenschaft ins Modell, zusammen mit
   dem 3D-Schnittbereich (N13).
3. **VORNE:** Nach dem Zusammenführen von N9 Vorzeichen und Herkunft von `planwinkel` in `gebaeudeAusrichtungGrad`
   (`wuerfel-logik.js`) prüfen.

## N12 – IFC: Original oder Nachbau als Unterlage zeigen, je eigene Farbe (transparent)

Christian 21.10.2026: «Es soll gewählt werden können ob das originale IFC oder der nachbau als Unterlage abgebildet werden soll.» – «für beide
muss eine unterschiedliche farbe wählbar sein (transparent)».

Bestand (Bestandsaufnahme 21.10.2026):
- Die Netzdatei der IFC-Unterlage enthält die Originalgeometrie aller Produkte, mit Kennzeichen `uebernommen`.
- Gezeichnet wird heute fest nur das nicht Übernommene, als Hintergrund: im Kern `GrundlagenIfc.Laden` mit `nurHintergrund: true`, im Web
  `leseNetz` mit Vorgabe `hintergrund`.
- Der Nachbau (Wand, Tür, Decke, Träger …) wird in eigener Darstellung gezeichnet.
- Im Schnitt fehlt das Original ganz, weil `Schnittzerlegung` die Art `netz` nicht kennt.

Entscheide Hauptlauf:
- **Ansichtseigenschaft `ifcDarstellung`:**
  - `nachbau` (Vorgabe, wie heute): Nachbau plus nicht übernommenes Original;
  - `original`: Original aller Produkte; Elemente mit `kennungen.quelle` = diese Unterlage werden ausgeblendet;
  - `beide`: übereinander, zum Vergleichen.
- **Farben an der Unterlage:**
  - Original: `farbeOriginal` und `deckkraft`, heute grau #8a8a8a mit 0.5.
  - Nachbau: `farbeNachbau` und `deckkraftNachbau`. Vorgabe ist keine Übersteuerung, also die normale Darstellung.
  - Beide sind wählbar und transparent. In `beide` müssen sie sich unterscheiden lassen.
- **Geltungsbereich:** Grundriss (Schnitt auf Schnitthöhe), 3D sowie Schnitt und Ansicht; dort ist das Original-Netz neu. Kollision,
  Aussparungen und IFC-Export bleiben unverändert.

**Fertig, wenn** Christian in «Test_praxis»:
- je Ansicht zwischen Original, Nachbau und beiden umschalten kann;
- für beide Farbe und Transparenz wählen kann, im Dialog Sichtbarkeit/Grafiken bzw. an der Unterlage;
- den Unterschied sieht, z. B. die Türen mit 5,19 m im Original gegenüber dem Nachbau.

## N13 – 3D-Schnittbereich einstellbar (wie Revit)

Christian 21.10.2026: «in der 3d Ansicht, sollte der 3dansichtsbereich eingestellt werden können. (es ist nur sichtbar was eingestellt
ist, wie in revit)». Heute gibt es im 3D (`ansichten/ansicht3d.js`) keinen Schnittbereich, keine Clipping-Ebenen und keinen Ausschnitt.

Entscheide Hauptlauf:
- **Eigenschaft der 3D-Ansicht `schnittbereich`:** ein/aus, dazu ein Quader (min/max in Modell-mm).
  - Ausgerichtet an der Gebäudeausrichtung (`planwinkel`, N9); ohne Wert an den Projektachsen.
  - Gespeichert im Modell über die Befehle des Kerns, nicht nur im Browser.
- **Sichtbarkeit:** Es ist nur sichtbar, was im Quader liegt. Geschnittene Bauteile werden an den Flächen des Quaders abgeschnitten (Clipping),
  auch Unterlagen-Netze (IFC). Die Schnittflächen werden geschlossen dargestellt, soweit es mit vertretbarem Aufwand geht; sonst offen, mit Hinweis.
- **Bedienung wie Revit:**
  - Schalter «3D-Schnittbereich» in den Eigenschaften der 3D-Ansicht.
  - Der Quader erscheint mit Griffen je Fläche; Ziehen an einem Griff verschiebt diese Fläche.
  - «Schnittbereich an Auswahl anpassen» (Auswahl → Quader mit Rand).
  - «Schnittbereich auf Geschoss» (OK/UK des Geschosses).
  - Zurücksetzen auf das ganze Modell.
- **Navigationswürfel (N11)** und mittlere Taste (N10) funktionieren unverändert mit eingeschaltetem Bereich.

**Fertig, wenn** Christian in einer 3D-Ansicht den Bereich einschalten und an den Griffen auf einen Gebäudeteil oder ein Geschoss verkleinern
kann, nur dieser Teil sichtbar und sauber abgeschnitten ist, die Einstellung mit der Ansicht gespeichert bleibt (auch nach Neustart und auf
einem anderen Rechner) und die Auswahl darin weiter funktioniert.

## N14 – «Raum im 3D zeigen» aus dem Grundriss

Christian 21.10.2026: «ist im GR ein Raum ausgewählt; Kontextmenue "raum im 3D zeigen" springt zur 3d ansicht und zeigt den 3d ausschnitt
des raumes, +1.5m in alle richtungen.» (Vorrat für Freitag; baut auf N13 auf.)

**Fertig, wenn:**
- Bei einem gewählten Raum bietet das Kontextmenü des Grundrisses «Raum im 3D zeigen» an; es gibt auch einen Befehl dafür, damit die KI es
  ebenso kann.
- Ein Klick wechselt in eine 3D-Ansicht. Ist eine offen, die zuletzt benutzte; sonst wird eine angelegt.
- Der 3D-Schnittbereich (N13) wird auf den Raum gesetzt, plus 1,5 m in alle Richtungen:
  - Grundriss-Umriss des Raums plus 1,5 m;
  - Höhe von Raumunterkante − 1,5 m bis Raumoberkante + 1,5 m;
  - ausgerichtet am Planwinkel (N9).
- Die Kamera zeigt den Bereich ganz: Isometrie von vorne rechts, wie Revit «Auswahlbereich».
- Mehrere gewählte Räume ergeben einen gemeinsamen Bereich.
- Der Rand von 1,5 m ist eine Benutzereinstellung mit Vorgabe 1,5 m.

## N15 – «Raum begehen» aus dem Grundriss (Ich-Perspektive)

Christian 21.10.2026: «gleicher ablauf, Kontextm. "Raum begehen" ansicht wechselt in egoperspektive in den raum. position 1.7m über boden,
zentrum raum. position kann mittels pfeiltaste geändert werden.» (Vorrat für Freitag.)

**Fertig, wenn:**
- Bei einem gewählten Raum bietet das Kontextmenü des Grundrisses «Raum begehen» an, mit eigenem Befehl wie bei N14.
- Ein Klick wechselt in die 3D-Ansicht im Modus «Begehen», mit perspektivischer Kamera:
  - Standort in der Mitte des Raums. Bei L-förmigen Räumen gilt ein Punkt, der sicher im Raum liegt (Schwerpunkt, sonst der innerste Punkt).
  - Augenhöhe 1,7 m über OK Boden des Raums.
  - Blick waagrecht, auf die Gebäudeausrichtung (N9).
- **Bewegen:**
  - Pfeil hoch/runter = vor/zurück, Pfeil links/rechts = drehen. Mit Umschalt schneller.
  - Maus ziehen = umsehen.
  - Bild auf/ab = Augenhöhe ändern.
  - Die Höhe über Boden bleibt beim Gehen gleich, auch über Geschossgrenzen am selben Geschoss.
  - **Annahmen, am Freitag mit Christian bestätigen:** die Tastenbelegung, und dass man durch Wände gehen kann (wie Revit, keine Kollision).
- **Anzeige:** Raumname und Geschoss stehen eingeblendet.
- **Verlassen:** Esc bzw. Haus-Knopf führt zurück zur normalen 3D-Ansicht.
- **Navigationswürfel (N11) und Schnittbereich (N13)** bleiben nutzbar bzw. ausgeschaltet; das am Freitag festlegen.

## N16 – Stützen im Grundriss mit Kreuz, nur Beton schraffiert

Christian 21.10.2026: «stützen werden im Grundriss mit einem Kreuz angezeigt. nur bei betton bleibt die schraffur.»

**Fertig, wenn:** Geschnittene Stützen aus Stahl, Holz, Mauerwerk oder ohne Material zeigen ihren Umriss mit beiden Diagonalen und ohne
Schraffur. Beton behält die Schraffur. Nicht geschnittene Stützen bleiben wie bisher, auf dem Bildschirm und im PDF.

**Umgesetzt: 21.10.2026** (Hauptlauf).
- `ArchitekturGrafik.Stuetze`: Bei Schnittlage 0 und einem Material, das nicht Beton ist, wird der Umriss papierweiss mit dickem Stift
  gezeichnet. Dazu kommen zwei dünne Linien:
  - beim Viereck Ecke zu Ecke;
  - bei runden Querschnitten und Vielecken durch die Mitte von Rand zu Rand.
- `IstBeton`: Als Beton gilt jedes Material mit «beton» im Namen, ausser Porenbeton. Die Materialliste kennt heute «beton».
- **Tests:** `StuetzeKreuzTests.cs` (11).

## N17 – Treppen im Grundriss erkennbar darstellen

Christian 21.10.2026: «auch treppen sind im Grundriss noch nicht korrekt angezeigt. resp. nicht erkennbar.»

Bestand: KICAD hat keine Klasse für Treppen. Aus dem IFC bleiben IfcStair und IfcStairFlight Hintergrund-Netz der Unterlage. Der Grundriss
schneidet dieses Netz auf Schnitthöhe (`GrundlagenIfc`); dabei entsteht ein Gewirr aus Stufenkanten statt einer lesbaren Treppe.

Entscheide Hauptlauf:
- **Neue Klasse `Treppe`** (Kürzel TR, Schicht Architektur, IFC IfcStair bzw. IfcStairFlight), je Lauf ein Element. Inhalt:
  - Lauflinie als Polylinie;
  - Laufbreite, Stufenanzahl, Steigung und Auftritt;
  - Unterkante und Oberkante bzw. Geschoss von–bis;
  - Stufenkanten als Liste von Strecken;
  - Podeste als Umriss.
- **IFC-Übernahme:** IfcStairFlight bzw. IfcStair werden zu Treppen-Elementen.
  - Stufenanzahl, Steigung und Auftritt aus den Attributen (NumberOfRisers, RiserHeight, TreadLength) bzw. aus den Psets.
  - Fehlen sie, kommen sie aus der Geometrie: waagrechte Trittflächen des Körpers, Vorderkanten, Lauflinie durch die Mitten.
  - Die Originalgeometrie bleibt in der Netzdatei; übernommene Treppen gelten dort als `uebernommen`, wie Wände.
- **Grundriss** (Darstellung wie Revit bzw. H+K-Planbild):
  - Stufenkanten unterhalb der Schnitthöhe durchgezogen, oberhalb gestrichelt bzw. ausgeblendet.
  - Schnittlinie als schräge Doppellinie bzw. Zickzack an der Schnitthöhe.
  - Lauflinie mit Anfangspunkt (Kreis) und Pfeil in Gehrichtung aufwärts.
  - Beschriftung «n Stg. h/a» (z. B. «16 Stg. 17.5/28»).
  - Podeste als Umriss.
  - Im Geschoss darüber erscheint die Treppe von oben (unterer Teil, ab Schnitt gestrichelt), wie im Planbild üblich.
- **Schnitt und 3D:** Körper aus den Stufen bzw. weiter das Original-Netz.
- **Zeichnen von Hand** (Werkzeug Treppe) ist später; zuerst Darstellung und IFC.

**Fertig, wenn** die Treppen der Bernstrasse (Archicad-IFC) im Grundriss jedes Geschosses als Treppe erkennbar sind:
- Stufen, Schnittlinie, Lauflinie mit Pfeil und Beschriftung;
- im PDF gleich wie auf dem Bildschirm;
- eine Treppe ohne Attribute wird aus ihrer Geometrie richtig gezählt.
