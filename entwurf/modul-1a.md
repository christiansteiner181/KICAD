# Modul 1a – Grundriss und 2D: Spezifikation

Stand 17.10.2026 (Hauptlauf). Verbindlich für alle Bauaufträge von Modul 1a. Zuerst `LIESMICH.md` lesen.

## Ziel und Abnahme

**Fertig, wenn …** Wände, Türen und Fenster sind im Grundriss gezeichnet und stehen gleichzeitig richtig im 3D;
ein Plan mit Bemassung ist als Planblatt gedruckt.

Umfang laut Ablaufplan: Wände, Türen, Fenster, Räume selbst zeichnen (Projekte ohne Architekten-IFC); Linien,
Schraffuren, Texte, Bemassung, Koten, Symbole, Fangen, Raster (Achsen); Planblätter mit H+K-Plankopf; Schnitt und
Ansicht aus dem 3D. Bedienung wie Revit, 2D = 3D, alles live.

## Grundsatz: Der Kern erzeugt die Grafik

Entscheid (Christian, 17.10.2026): «Planvektoren erzeugt der Kern (auch für PDF/DWG)». Bildschirm, Planblatt-PDF
und später DWG zeichnen **dieselben Primitive**. Die Oberfläche rechnet nichts Fachliches, sie zeichnet, fängt und
schickt Befehle.

- Datentypen: `src/Hlks.Kern/Grafik/Grafiktypen.cs` – `Plangrafik` (Teile mit Primitiven: linie, polylinie,
  polygon mit Löchern, kreis, bogen, text; `Stil` mit Stift, Linienart, Farbe, Füllung, Muster), `Koerper`
  (quader, prisma, zylinder, kugel).
- Einstieg: `src/Hlks.Kern/Grafik/Grafik.cs` – `Grafik.Plan(projekt, ansicht)`, `Grafik.Koerper(projekt, ids)`,
  Sichtbarkeit (wie `js/ansichten/filter.js`), `GrafikKontext` (Massstab, Geschoss, Schnitthöhe absolut,
  Detailgrad, Gewerke, ausgeblendete Klassen, `Ablage` für Vorbereitetes).
- Quellen (je eine Datei, unabhängig baubar): `ArchitekturGrafik`, `HlksGrafik`, `BeschriftungGrafik`; Schnitt und
  Ansicht: `Schnittgrafik.Erzeugen(ctx)` aus den Körpern.
- REST (`src/Hlks.Web/Program.cs`): `GET /api/plangrafik/{ansicht}`, `GET /api/koerper[?ids=a,b]`,
  `GET /api/plan/{planblatt}.pdf`.
- Koordinaten: Grundriss in Modell-mm (x Ost, y Nord). Schnitt/Ansicht: u = Abstand entlang der Linie ab ihrem
  Anfang, v = z. Strichstärken und Schriftgrössen in **Papier-mm**; `ctx.AufModell(mm)` rechnet in Modell-mm um.

## Datenmodell (Katalog `kataloge/klassen.json`, schon erweitert)

| Klasse | Geometrie | Regeln |
| --- | --- | --- |
| Wand | linie: Achse Anfang → Ende (Wandmitte); `geometrie.dicke`, `geometrie.hoehe`; z = Unterkante | eine Wand je gerades Stück; `material` bestimmt die Schraffur; Ecken/Anschlüsse bereinigt der Kern |
| Tuer, Fenster | punkt: Mitte unten in der Achse | `bezuege.wand`; `abstand` (Mitte ab Wandanfang entlang der Achse) ist führend, der Punkt wird daraus abgeleitet; Tür: `seite` links/rechts (in Wandrichtung), `band` anfang/ende; Fenster: `bruestung` |
| Decke | polygon, z = OK Rohdecke, `geometrie.dicke` nach unten | |
| Raum | polygon, `geometrie.hoehe` | `nummer`, `nutzung`, Name |
| Achse | linie | `bezeichnung`; in allen Grundrissen sichtbar |
| Detaillinie, Fuellbereich, Textnotiz, Bemassung, Kote, Symbol | je Klasse | Schicht `beschriftung`, **ansichtsgebunden**: `bezuege.ansicht` Pflicht (Kern prüft); Koordinaten in der Ansicht |
| Ansicht (schnitt/ansicht) | linie = Schnitt-/Ansichtslinie im Grundriss | `blick` links/rechts der Linie, `tiefe` (Sichttiefe), `ausschnitt`, `bemassungEinheit` |
| Planblatt | – | `nummer`, `titel`, `format` A4…A0, `hochformat`, `datum`, `kontrolliert`, `aenderungen` |
| Ansichtsfenster | punkt = Mitte auf dem Blatt (Papier-mm, Ursprung unten links) | `teilVon` Planblatt, `bezuege.ansicht` |
| Projektinformation | – | `phase`, `zeile1`, `zeile2`, `projektnummer`; eine je Projekt |

## Darstellung (Plangrafik) – Regeln mit Quelle

Aus SIA 400:2000 (am Normtext gelesen am 16.10.2026, siehe Wissensspeicher `normen_nachschlagewerk.md`). Werte, die
nicht aus der Norm stammen, sind als **Annahme** markiert und stehen in Christians Prüfliste.

- Linienstärken B.3.1: drei Stifte `duenn` 0.18, `mittel` 0.35, `dick` 0.70 mm (`Stifte`).
- B.3.3 Tab. 4: dick = Schnittkanten der Hauptbauteile; dünn = Mass- und Hilfslinien, Schraffuren, Sichtkanten;
  Schnittlage strichpunktiert.
- Schnittflächen: Kontur `dick`, Schraffur nach `material` (Muster wie SIA 400 Fig. 34, Katalog im KI-Planungsbüro
  `KI_Planungsbuero/src/Kern/Bauteile.cs` → `Schraffuren()`, als Kopie übernehmen): beton Kreuz 45°, backstein 45°
  steigend, kalksandstein fallend, daemmung 90°, holz dichter 45°, glas waagrecht, putz Punkte. Material
  `unbestimmt`: Füllung «schnitt» mit Deckkraft 0.25 (**Annahme**; B.8.3.1 sagt bis 1:100 schwarz, Christian will
  im KI-Planungsbüro Schraffuren statt Schwarz).
- Schraffur folgt der Achse (Nachbearbeitung N8, Christian 21.10.2026 «schraffuren sollten mit wand rotieren», wie Revit
  «an Element ausrichten»): Die Winkel der Tabelle gelten gegen die Achse des Bauteils. Bauteile mit Achse im Grundriss (Wand,
  Unterzug) tragen `stil.musterWinkel` = Richtung der Achse auf [0, 180) gerundet auf 0.001° und `stil.musterUrsprung` = Fusspunkt
  des Lots vom Ursprung auf die Achse; Bildschirm und PDF addieren den Winkel zu denen der Schraffur und verankern das Muster im
  Anker – Bauteile auf derselben Achse haben ein durchlaufendes Muster. Ohne Achse (Decke, Raum, Füllbereich, Stütze im Grundriss)
  und im Schnitt (Achse waagrecht bzw. zum Betrachter) bleibt das Muster achsparallel zum Blatt. Gebogene Wände gibt es im Modell
  nicht (Kette gerader Wände, je Abschnitt eine Achse).
- Masslinien B.5.3: Masslinie dünnste Linie, Massstriche doppelt so dick (Schrägstrich 45°, `mittel`), Masszahl
  über der Linie, von unten bzw. rechts lesbar, Einheit auf dem Plan (Planblatt-Text «Masse in m»).
  Masszahl-Einheit (**Entscheid Christian 17.10.2026**: Grundriss/Architektur in m, Lüftung in mm, Heizung und
  Sanitär in cm, immer einstellbar): `Bemassungseinheit.Fuer` – Masskette `einheit` › Ansicht `bemassungEinheit` ›
  Projektinformation `bemassungEinheiten` { gewerk: einheit } › Vorgabe nach dem Gewerk der Ansicht (erstes
  HLKS-Gewerk in der Reihenfolge Lüftung, Heizung, Sanitär, Kälte, Elektro, MSRL; sonst Architektur). Kälte cm,
  Elektro und MSRL m sind **Annahmen**. Format: m mit zwei Dezimalen, cm ganz (eine Dezimale bei Bruchteil), mm ganz.
- Koten B.5.4: in m, ±0.00 meist OK FB EG; Format «+2.85», «±0.00», «−2.60». Kotensymbol: gleichschenkliges
  Dreieck mit Spitze auf der Bezugslinie (**Annahme**, Fig. 18 im Normtext nicht lesbar).
- Schnittsymbol B.6.1: Pfeil in Blickrichtung, dünnste Linie; Schnittlinie strichpunktiert dick (Tab. 4) an den
  Enden, Bezeichnung = Kennzeichen der Schnittansicht.
- Titelfeld B.1.3 unten rechts: Auftraggeber · Planinhalt · Planverfasser · Plan-Nr., Massstab, Index, Datum,
  Format, Visum. H+K-Plankopf als Kopie aus `AufgabenProtokolle.Web/Services/HkPlankopf.cs` (Logo
  `AufgabenProtokolle.Web/Assets/hk-logo.png`), dort mit PdfSharpCore, hier mit PDFsharp 6.5 (`XGraphics` fast gleich).
- Graphischer Massstab und Nordpfeil auf jeder Zeichnung (B.1.4, C.2).
- Schrift: Raumstempel 2.5 mm, Kennzeichen/Nebentext 2.0 mm, Masszahlen 2.0 mm, Titel 3.5 mm (**Annahme**,
  Schriftgrössentabelle im Normtext nicht lesbar).
- Bildschirmhilfen (Raumfarben, Kennzeichen-Etiketten) mit `Stil.NurBildschirm = true`, damit sie nicht gedruckt
  werden. Planfarbe `Farbe = null` (schwarz auf Papier, hell im dunklen Design); Medienfarben nach SIA 410/1 aus
  dem Katalog bleiben.

### Grundriss
- Wände: geschnitten, wenn Unterkante ≤ Schnitthöhe < Oberkante. Umriss aller geschnittenen Wände eines Geschosses
  **vereinigt** (Clipper2), Öffnungen ausgespart → gemeinsamer Teil (Id null, Ebene `wand`, Kontur dick). Je Wand
  ein eigener Teil mit ihrer Fläche (Füllung/Schraffur, ohne Kontur) zum Anklicken und Hervorheben, Fangkanten an
  beiden Wandflächen, Fangpunkte an Achs- und Flächenenden. Nicht geschnittene Wände (niedriger): Sichtkanten dünn.
- Tür: Öffnung, Blatt als Linie `mittel` in 90°-Stellung an der Bandseite, Aufschlagbogen dünn (Viertelkreis).
- Fenster: Öffnung, Glaslinie(n) dünn in der Wandmitte, Rahmen an den Leibungen, Fensterbank als dünne Linie auf
  der Aussenseite (Seite = `links` der Wandrichtung gilt als aussen, solange es keine Hülle gibt – **Annahme**).
- Raum: Begrenzung dünn, Stempel (Nummer fett, Nutzung/Name, Fläche in m² mit zwei Dezimalen), Füllung nach
  Nutzung nur am Bildschirm.
- Achse: strichpunktiert dünn, Kreis Ø 8 mm (Papier) mit Bezeichnung an beiden Enden.
- HLKS: Übertrag der heutigen Darstellung aus `js/ansichten/zeichnen2d.js`, `sinnbilder.js` (Sinnbilder der
  Apparate, Leitungen mit Medienfarbe und Linienart, Formteile, Armaturen), unverändert im Aussehen.
- Leitungsbezeichnung (**Entscheid Christian 17.10.2026**: Rohre in NW oder DN, Kanäle und Lüftungsrohre in mm):
  `Leitungsbezeichnung.Text` – Rohr «NW 50», auf Einstellung «DN 50» (Ansicht `rohrbezeichnung` › Projektinformation ›
  Vorgabe **NW**, Christian 17.10.2026), Rechteckkanal «400×200», Rundkanal/Lüftungsrohr «Ø 250». Lüftung = Klasse Kanal oder Medium mit
  Gewerk lueftung; diese Leitungen in echter Grösse (Grundriss: Umriss in Medienfarbe, Füllung 20 %; 3D: Quader je
  Segment bzw. Zylinder mit echtem Durchmesser).

### Körper (3D und Grundlage für Schnitte)
- Wand: Quader je Abschnitt zwischen Öffnungen (volle Höhe), unter Fenstern bis Brüstung, über Öffnungen ab
  Sturz; an Ecken um die halbe Dicke der anschliessenden Wand verlängert; Material `wand`.
- Tür/Fenster: dünner Quader in der Öffnung (Tür `tuer`, Fenster `glas` mit Deckkraft 0.35).
- Decke/Dach: Prisma; Raum: Prisma mit `NichtSchneiden`, Deckkraft 0.15.
- HLKS: Übertrag aus `js/ansichten/bauteile3d.js` (Rohre als Zylinder je Segment, Kugeln an Knicken, Formteile,
  Armaturen, Apparate als Quader aus den Sinnbild-Massen – diese Masse gehören in den Katalog: Eigenschaft
  `darstellung` je Klasse { b, t, h } in mm).

### Schnitt und Ansicht (aus den Körpern)
Schnittebene senkrecht durch die Linie; Blick nach `blick`; Sichttiefe `tiefe`. Die waagrechte Bildachse läuft so, wie
der Betrachter sieht: bei Blick links ab A Richtung B, bei Blick rechts ab B Richtung A (sonst wäre das Bild
seitenverkehrt; Befund C1, korrigiert 17.10.2026). Geschnittene Körper → Schnittfläche
(Kontur dick, Schraffur nach Material des Elements); dahinter Sichtkanten dünn, verdeckte Kanten weglassen
(Malerverfahren: Flächen von hinten nach vorn deckend füllen, dann schneiden). Ansicht = gleiches Verfahren ohne
Schnittflächen. Achsen, die die Linie kreuzen, als senkrechte Achslinie mit Bezeichnung. Geschosskoten am Rand.

## Befehle (Kern, je mit Trockenlauf, Rückgängig über Pakete)

| Befehl | Parameter | Wirkung |
| --- | --- | --- |
| `wand_zeichnen` | geschoss, punkte [[x,y],…] (≥ 2), dicke, hoehe (Vorgabe Geschosshöhe), bezugslinie mitte/links/rechts, geschlossen (bool), material | eine Wand je Stück; bezugslinie verschiebt die Achse um die halbe Dicke |
| `oeffnung_setzen` | klasse Tuer/Fenster, wand, abstand oder punkt [x,y] (wird auf die Achse projiziert), breite, hoehe, bruestung, seite, band | prüft: passt in die Wand, überschneidet keine andere Öffnung |
| `raum_erkennen` | geschoss, punkt [x,y], nummer, nutzung, name | Raumpolygon aus den Wandflächen um den Punkt (wie Revit «Raum»); Fehler, wenn nicht geschlossen |
| `elemente_verschieben` | elemente [], dx, dy, dz | Öffnungen folgen ihrer Wand (Nachführung) |
| `elemente_kopieren` | elemente [], dx, dy, anzahl | Kopien mit neuen Kennzeichen; Öffnungen gewählter Wände mit; Verbindungen nur innerhalb der Kopie |
| `elemente_drehen` | elemente [], mitte [x,y], winkel | |
| `elemente_spiegeln` | elemente [], achse [[x1,y1],[x2,y2]] | Tür-`seite` wird gespiegelt |
| `planblatt_anlegen` | nummer, titel, format, hochformat | |
| `ansicht_platzieren` | planblatt, ansicht, lage [x,y] (Papier-mm) | legt ein Ansichtsfenster an; eine Ansicht nur auf einem Blatt (wie Revit) |
| Abfrage `plan_pdf` | planblatt, datei | schreibt das PDF; Vorgabe-Datei im Temp-Ordner |

Beschriftung entsteht mit `element_anlegen` (Klasse + `bezuege.ansicht`). Nachführung `OeffnungenNachfuehren`:
ändert sich eine Wand, werden ihre Öffnungen aus `abstand` neu gesetzt; wird sie gelöscht, auch ihre Öffnungen.
Jeder neue Befehl und jede neue Abfrage braucht ein MCP-Werkzeug (`src/Hlks.Mcp/Werkzeuge*.cs`, Klasse ist
`partial` – je Bereich eine eigene Datei) und einen Aufruf in den Abdeckungstests (`tests/Hlks.Mcp.Tests`).

## Schnittstelle Ansicht ↔ Werkzeug (Phase C, verbindlich für C2 und C3)

Jede Plan-Ansicht (Grundriss, Schnitt, Ansicht) gibt beim Erstellen eine Instanz zurück (Vertrag `fenster.js`:
`aktualisieren`, `ansichtGeaendert`, `auswahlGeaendert`, `zoomAlles`, `zerstoeren`) und zusätzlich:

| Mitglied | Bedeutung |
| --- | --- |
| `art` | `grundriss` \| `schnitt` \| `ansicht` |
| `ansicht` | Darstellung des Ansicht-Elements (Kennzeichen, werte, geschoss) |
| `massstab` | Massstabsnenner |
| `zuModell(clientX, clientY)` | Bildschirm → Modell-mm der Ansicht `[x, y]` |
| `zuBildschirm([x, y])` | Modell-mm → `{ x, y }` Client-Pixel |
| `pxJeMm()` | Bildschirm-Pixel je Modell-mm (aktueller Zoom) |
| `fangdaten()` | `{ punkte: [{ p:[x,y], id }], kanten: [{ a:[x,y], b:[x,y], id }] }` aus `Fangpunkte`/`Fangkanten` der Plangrafik |
| `werkzeugSchicht` | SVG-`<g>` in Modellkoordinaten (y wie im Modell, die Ansicht spiegelt) für Vorschau, Fangsymbol, Gummiband, temporäre Bemassung; liegt über allem |
| `setzeWerkzeug(w)` / `werkzeug` | aktives Werkzeug oder null. Ist eines aktiv, gibt die Ansicht `pointerdown`/`pointermove`/`pointerup`/`dblclick`/`keydown` zuerst an `w.ereignis(art, e, ansicht)`; liefert diese `true`, ist das Ereignis verbraucht (keine Auswahl, kein Verschieben). Mittlere Taste/Rad bleiben immer Zoom/Pan. |

Werkzeuge (C3) liegen in `js/werkzeuge/*.js`, registrieren sich als Aktionen (Menüband, Kürzel) und schicken nur
Befehle über `js/oberflaeche/befehle.js` (`sende`). Die Ansicht (C2) kennt keine Werkzeuglogik.

## Oberfläche (Phase C)
- Grundriss, Schnitt, Ansicht zeichnen die Plangrafik aus `/api/plangrafik` (SVG), Planblatt das Blatt im Massstab
  1:1 Papier; 3D baut aus `/api/koerper`. Nach jedem Ereignis «aenderung» Grafik der offenen Ansicht neu holen.
- Werkzeuge mit Revit-Kürzeln: Wand WA, Tür DR, Fenster WN, Raum RM, Achse GR, Bemassung DI, Text TX, Detaillinie
  DL, Füllbereich, Kote, Schnitt; Ändern: Verschieben MV, Kopieren CO, Drehen RO, Spiegeln MM, Löschen DE.
- Fangen: Endpunkt, Mitte, Schnittpunkt, Lot, Nächster (aus Fangpunkten/-kanten der Plangrafik), Raster; Fangsymbol;
  temporäre Bemassung beim Zeichnen; Länge per Tastatur; Esc bricht ab; Optionsleiste mit Dicke, Höhe, Bezugslinie,
  Kette. Griffe an Wandenden.
- Design: hell/dunkel nach Revit-Massen (erledigt 17.10.2026); Planblatt bleibt immer hell (Papier).

## Phasen und Zuteilung

| Phase | Auftrag | Agent |
| --- | --- | --- |
| A | Fundament: Katalog, Grafiktypen, Grafik-Einstieg, Nachführung, REST, diese Spezifikation | Hauptlauf (erledigt) |
| B1 | Architektur: Wand/Tür/Fenster/Decke/Raum/Achse – Plangrafik, Körper, Befehle, Nachführung | hlks-kern |
| B2 | HLKS-Grafik: Übertrag 2D und 3D aus der Oberfläche in den Kern, Masse in den Katalog | hlks-modul |
| B3 | Beschriftung: Detaillinie, Füllbereich, Text, Bemassung, Kote, Symbol, Schnittsymbol | hlks-modul |
| B4 | Planblatt und PDF: Befehle, Ansichtsfenster, H+K-Plankopf, PDF, `plan_pdf` | hlks-modul |
| C1 | Schnitt und Ansicht aus den Körpern | hlks-kern |
| C2 | Oberfläche: Plangrafik-Renderer, 3D aus Körpern, Schnitt-/Planblatt-Ansicht | hlks-modul |
| C3 | Oberfläche: Zeichenwerkzeuge, Fangen, Griffe, Ändern-Werkzeuge | hlks-modul |
| D | Gesamttest, Browserprüfung, KI-Test (Wand per MCP zeichnen), Bericht | hlks-test, Hauptlauf |

Dateigrenzen: Jeder Auftrag ändert nur seine Dateien (siehe Auftrag). `kataloge/klassen.json` ändert nur der
Hauptlauf und B2 (nur `darstellung` bei HLKS-Klassen).
