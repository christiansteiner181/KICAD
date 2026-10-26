# Modul G – Gesten am Arbeitsplatz (Kamera): Spezifikation

Stand 26.10.2026 · Sitzung «modul-gesten», Zweig `claude/modul-gesten` · **Stufe 1 gebaut, Abnahme nicht abgeschlossen** – Tuning im Labor (Christian 26.10.2026: «wir machen das tuning in der labor app»); Freigabe 26.10.2026;
Lesart G4/G7 bestätigt)

## Anlass

Christian, 26.10.2026: «ich denke an eine erweiterung der normalen bedienung, nicht diese zu ersetzen. bsp. Drehen von
objekten, durchblättern von fenster usw. wie eine 4. ebene nach Taststur, Maus, Touche» – und auf den Vorschlag (3D
drehen, Geschoss wechseln, Kupplung, Rückmeldesymbol als Stufe 1): «machen wir das.»

## Leitlinien

1. **Vierte Eingabeebene, kein Ersatz.** Maus und Tastatur bleiben vollständig. Jede Geste löst etwas aus, das es auch
   per Maus, Kürzel oder Menüband gibt.
2. **Maus zeigt *was*, Geste sagt *wie*.** Die nicht-dominante Hand (Vorgabe links) gestikuliert, die rechte bleibt an
   der Maus. Vorbild ist die SpaceMouse: Navigation mit links, Arbeit mit rechts.
3. **Fliessendes per Geste, Präzises per Maus.** Gesten drehen, rollen, zoomen, verschieben und blättern. Sie setzen
   keine Punkte und ändern das Modell nie (Entscheid Christian 26.10.2026: «nur in einer 3d ansicht das objekt drehen und
   rollen.»).
4. **Gleiche Gesten wie in der Quest App.** Eine Bewegung hat am Schreibtisch und in der Brille dieselbe Bedeutung
   (`entwurf/modul-xr.md`). Übernommen werden die Lehren aus den Brillentests 24.–25.10.2026:
   - Wisch mit einem Anstoss, auch mit lockerer Hand.
   - Pinch ist zu empfindlich, deshalb keine Pinch-Geste.
   - Wische nach aussen am eigenen Bildrand zählen nicht.
   - Grenzwerte sind Stellschrauben.
5. **Lieber keine Auslösung als eine falsche.** Die Gesten sind immer aktiv, sobald eine Hand im Bild ist (Entscheid
   26.10.2026). Deshalb gilt:
   - Fliessende Gesten brauchen eine geschlossene Faust.
   - Schritt-Gesten brauchen einen deutlichen Anstoss.
   - Was Gesten auslösen, geht ohne Nachdenken wieder zurück: nur Ansicht und Navigation.
6. **Das Bild bleibt im Browser.** Kein Speichern, kein Senden an Server, KI oder Datei.

## Ziel und «Fertig, wenn …»

**Fertig, wenn …** Christian im Desktop am realen Projekt Test_praxis mit der linken Hand vor der eingebauten Kamera
- in der 3D-Ansicht das Modell bzw. die Auswahl dreht, rollt, zoomt und verschiebt, während die rechte Hand an der
  Maus bleibt,
- zwischen den Ansichtsreitern blättert und in Listen und in der Hilfe rollt,
- in der Statusleiste jederzeit sieht, was erkannt wird,
- in 10 Minuten normaler Arbeit (tippen, telefonieren, zur Tasse greifen) höchstens eine Fehlauslösung hat,
- und die Bildrate der 3D-Ansicht mit Gesten höchstens 10 % unter der ohne Gesten liegt. Bei ausgeschalteten Gesten ist
  die Kamera aus und es gibt keine Last.

## Gestenwortschatz (Stufe 1)

Alle Gesten gelten für die Gestenhand (Vorgabe links, einstellbar). Die Grenzwerte sind Annahmen und werden im Bau
gemessen und als Stellschrauben geführt.

| Nr | Geste | Wirkung | Art |
|---|---|---|---|
| G2 | Wisch links/rechts (ein Anstoss, Hand offen oder locker) | vorheriger/nächster Ansichtsreiter; in Liste, Hilfe und Dialog mit Seiten: Seite zurück/vor | Schritt |
| G3 | Wisch hoch/runter | im aktiven Fenster rollen bzw. blättern (Liste, Hilfe, Dialog) um eine Bildschirmseite; in Plan, Schema und 3D ohne Wirkung. Das Geschoss wechselt nicht per Geste (Entscheid 26.10.2026, wie in der Brille). | Schritt |
| G4 | Faust schliessen und bewegen | 3D: drehen um Auswahl bzw. Modellmitte (gleiche Regel wie Umschalt + Mitte, `wuerfel-logik.js`); Plan und Schema: verschieben | fliessend |
| G5 | Faust zur Kamera hin/weg | ~~zoomen~~ **ausgeschaltet** (Entscheid Christian 26.10.2026 in der Abnahme: «zoom, ausschalten»); zoomen mit Mausrad oder G6 | – |
| G6 | Pistolengeste (Daumen hoch, Zeigefinger gestreckt), kurz | «Zoom alles» (`ansicht.zoom-alles`); in 3D stellt sie die Ansicht zugleich wieder aufrecht (nach G7) | Schritt |
| G7 | *nur 3D:* Faust um die Blickachse drehen (Handgelenk wie einen Drehknopf) | Ansicht rollen um die Blickachse, durch die Mitte der Auswahl bzw. des Modells | fliessend |

G1 (Ein/Aus-Geste) entfällt, weil die Gesten immer aktiv sind. Die Nummer bleibt frei, damit sie zur Spezifikation passt.

- **Lesart von «drehen und rollen» (bestätigt von Christian 26.10.2026: «Ja, so bauen»):**
  - Die Faust hält das Modell wie einen Gegenstand in der Hand. Bewegen dreht es (G4), das Handgelenk rollt es (G7).
  - Das Modell selbst ändert sich nicht; geändert wird nur die Ansicht.
  - Fürs Rollen braucht es eine freie Drehung um die Blickachse, die OrbitControls nicht kann (feste Hochrichtung). Die
    3D-Ansicht bekommt dafür eine eigene Drehung um die Blickachse.
  - Navigationswürfel und G6 stellen die Ansicht wieder aufrecht.
- **Faust lösen** beendet G4, G5 und G7 sofort. Die Bewegung hat keinen Nachlauf.
- **G4 und G7 aus derselben Faust:** Seitliche Bewegung zählt als Drehen, Drehung des Handgelenks als Rollen. Beides
  gleichzeitig ist erlaubt. Eine Mindestdrehung (Annahme 8°) trennt das Rollen vom Zittern.
- **Schwelle für fliessende Gesten:** Unter einem Totbereich (Annahme 1,5 % der Bildbreite) bewegt sich nichts, damit
  das Zittern der Erkennung nicht wackelt. Danach wirkt die Bewegung geglättet und linear.
- **Sperre neben der Maus:** Während der letzten 300 ms vor einem Wisch hat die Maus geklickt oder eine Taste wurde
  gedrückt? Dann zählt der Wisch nicht. Fliessende Gesten laufen dagegen bewusst parallel zur Maus.
- **Ruhebetrieb:** Ist 10 s lang keine Hand im Bild, sinkt die Erkennung auf 5 Bilder/s. Taucht eine Hand auf, läuft sie
  wieder voll. Geht das Fenster in den Hintergrund, wird die Kamera freigegeben und beim Zurückkehren wieder geöffnet.

## Bedienung und Rückmeldung

- **Grundschalter:** Die Gesten sind immer aktiv, solange der Grundschalter «Gesten» eingeschaltet ist.
  - Wo: Register Ansicht › Gruppe «Fenster» › Knopf «Gesten», dazu ein Tastenkürzel (beim Bau festlegen).
  - Der Schalter ist für Momente gedacht, in denen die Kamera nicht laufen soll, z. B. bei einer Bildschirmfreigabe.
  - Er bleibt je Benutzer gespeichert. Vorgabe: aus, bis man ihn einmal einschaltet. Erst dann fragt der Browser nach
    der Kamera.
- **Statusleiste:** Symbol mit vier Zuständen:
  - aus
  - bereit, keine Hand
  - Hand erkannt
  - Geste aktiv, mit Namen (z. B. «drehen»)

  Ein Klick schaltet die Gesten ein/aus, Rechtsklick öffnet die Einstellungen.
- **Handskelett** (Vorgabe ein): kleines Feld unten rechts mit den 21 Punkten der Hand als Linien, **ohne Kamerabild**.
  Daran sieht man, warum etwas nicht erkannt wird.
- **Meldung nach Schritt-Gesten:** kurz und schwebend wie in der Brille (z. B. «Reiter: 3D Lüftung»).
- **Einstellungen** (Dialog «Gesten», Verwalten › Hilfe neben «Tastaturkürzel»):
  - Gestenhand links/rechts
  - Kamera wählen
  - Empfindlichkeit je Geste (Stellschrauben)
  - Belegung: welche Aktion G2 und G6 auslösen, Auswahl aus dem Aktionsregister
  - Handskelett ein/aus
  - Testfeld, das erkannte Gesten anzeigt, ohne sie auszulösen
- **Liste der Gesten** wie die Liste der Tastaturkürzel (KS), und ein Kapitel in der Bedienungsanleitung `/hilfe/`.

## Technik

- **Erkennung im Browser:** MediaPipe Tasks Vision `HandLandmarker` (Apache 2.0) liefert 21 Punkte je Hand im Bild,
  dazu Weltkoordinaten in m um die Handmitte und die Händigkeit.
  - WASM und Modelldatei liegen lokal unter `wwwroot/lib/mediapipe/` mit fester Version. Kein CDN, damit KICAD offline
    läuft.
  - `getUserMedia` funktioniert, weil localhost ein sicherer Kontext ist.
  - Läuft in einem eigenen Worker mit OffscreenCanvas, damit die 3D-Ansicht nicht stockt.
  - Ziel sind 30 Bilder/s, Rückfall auf 15. Die Auflösung der Kamera ist klein (640×480).
- **Gestenlogik** `js/gesten/gesten-logik.js` als reine Funktionen wie die übrigen `*-logik.js`, mit Tests
  `gesten-logik.test.mjs` auf aufgezeichneten Punktfolgen. Testfolgen: einmal von Christian aufnehmen, ohne Bild,
  nur Punkte.
  - Merkmale wie `Hlks.Xr/Gesten/GestenMerkmale.cs`: Fingerkrümmung, Handflächenrichtung, alles geteilt durch die
    Handgrösse (Handgelenk bis Grundgelenk Mittelfinger).
  - Wisch-Regel und Stellschrauben mit denselben Namen wie in `XrWischLogik.cs`: `anstoss_weg`, `anstoss_tempo_ms`,
    `randwinkel_grad`, `handflaeche_grad`.
  - Zoom (G5) über die scheinbare Handgrösse im Bild, geglättet.
- **Auslösen:**
  - Schritt-Gesten rufen nur Aktionen aus `aktionen.js` auf, wie Tastenkürzel. Die Belegung ist eine Zuordnung
    Geste → Aktions-Id in den Benutzereinstellungen (`BenutzerEinstellungen.cs`, über OneDrive je Benutzer).
  - Fliessende Gesten gehen über eine neue kleine Schnittstelle der aktiven Ansicht
    `navigieren({ drehen: [dx, dy], rollen: winkel, verschieben: [dx, dy], zoom: f })`. Sie wird in `ansicht3d.js` (bisher
    OrbitControls, fürs Rollen erweitert), im Grundriss und im Schema umgesetzt und ist auch für Tests und die KI
    brauchbar.
- **Neue Aktionen** (auch per Kürzel nützlich):
  - `ansicht.reiter-vor` / `ansicht.reiter-zurueck`
  - `ansicht.gesten` (Grundschalter)
- **Kamera:** gebaut und abgenommen mit der eingebauten Kamera oben am Bildschirm (Entscheid 26.10.2026). Eine andere
  Kamera ist in den Einstellungen wählbar.
- **Datenschutz:** Kamerabilder werden nie gespeichert, protokolliert, an den Server, an Claude oder in Dateien gegeben.
  Weiter gehen nur Gestenereignisse. Die Kamera-LED zeigt den Betrieb. Die Tests prüfen, dass kein Endpunkt Bilddaten
  annimmt.

## Stufen

1. **Stufe 1 (dieses Modul):** G2–G7, Statusleiste, Handskelett, Einstellungen, neue Aktionen, Anleitungskapitel.
2. **Stufe 2:** beidhändig zoomen, Belegung je Ansichtsart. Gesten, die das Modell ändern, sind ausgeschlossen
   (Entscheid 26.10.2026).
3. **Stufe 3:** **eigene Gesten über das Gestenlabor** – vorzeigen statt programmieren.
   - Erkenner `Hlks.Xr/Gesten` (DTW) mit einer Abbildung der 21 MediaPipe-Punkte auf `HandBild`. Den Fingern fehlen dort
     die Mittelhandknochen; das ist in Stufe 3 zu prüfen.
   - Dieselbe Gestensammlung für Brille und Arbeitsplatz.
4. **Ausserhalb von KICAD (nicht Teil des Moduls):** Gesten für Windows allgemein (Fenster wechseln, PDF blättern).
   Dafür bräuchte es ein kleines Hintergrundprogramm, das Gesten in Tastendrücke übersetzt. Später als eigenes Werkzeug
   denkbar, wenn sich Stufe 1 bewährt.

## Zuteilung (Vorschlag nach Tabelle in CLAUDE.md)

| Teil | Agent |
|---|---|
| Gestenlogik mit Glättung, Wisch-Regel, Faust-/Pistolenerkennung und Tests; Worker mit MediaPipe; Schnittstelle `navigieren` in 3D/Grundriss/Schema | `hlks-kern` |
| Statusleiste, Knopf, Einstellungsdialog, Gestenliste, neue Aktionen, Hilfe-Einträge | `hlks-modul` |
| Testlauf und Testbericht, Messung Bildrate mit/ohne Gesten | `hlks-test` |
| Abnahme am realen Projekt Test_praxis (Desktop), Messung der Fehlauslösungen mit Christian | Hauptlauf |

Richtwert 4–6 Arbeitstage für Stufe 1.

## Entscheide Christian 26.10.2026 (Fragefenster)

| Frage | Entscheid |
|---|---|
| Wisch hoch/runter | «Nur rollen/blättern»: kein Geschosswechsel per Geste |
| Objekt per Geste drehen | «nur in einer 3d ansicht das objekt drehen und rollen.» → nur Ansicht in 3D, Modell unverändert (Lesart oben, zu bestätigen) |
| Kupplung | «Immer aktiv»: keine Ein/Aus-Geste, nur Grundschalter; strengere Gesten statt Kupplung |
| Kamera | eingebaute Kamera |

| Lesart G4/G7 (26.10.2026, Sitzung modul-gesten) | «Ja, so bauen»: Faust hält das Modell, Bewegen dreht, Handgelenk rollt um die Blickachse; freie Hochrichtung, Würfel und G6 richten auf |

**Nach Stufe 1 (Entscheid Christian 26.10.2026, Sitzung «Labor-App», `entwurf/labor-app.md`):** Die Empfindlichkeiten der
PC-Gesten werden «nur im Labor» eingestellt. Mit Stufe L4 des XR-Labors entfallen sie im Dialog «Gesten» und das Feld
`stellschrauben` der Benutzereinstellung; KICAD liest dann den Bedienstandard (`pc.stellschrauben`) über
`STELLSCHRAUBEN_VORGABE`. Deshalb: Namen der Stellschrauben in `gesten-logik.js` stabil halten, jede mit Bereich und
Erklärung. Aufnahme und `testmodus` nutzt das PC-Gesten-Labor wieder.

## Schnittstellen im Bau (Hauptlauf modul-gesten, 26.10.2026)

Damit `hlks-kern` und `hlks-modul` gleichzeitig bauen können, gilt diese Aufteilung. Alles unter
`src/Hlks.Web/wwwroot/js/gesten/`.

**`gesten-logik.js` (hlks-kern):** reine Funktionen ohne DOM, Kamera oder Worker. Merkmale aus 21 Punkten, Faust- und
Pistolenerkennung, Wisch-Regel, Totbereich, Glättung, Zustandsautomat. Eingabe je Bild: Punkte, Weltpunkte, Händigkeit,
Zeit, Zeit der letzten Maus-/Tastaturbetätigung. Ausgabe: Zustand und Ereignisse wie unten. Stellschrauben als ein
Objekt `STELLSCHRAUBEN_VORGABE` (Namen wie `XrWischLogik.cs`, dazu `totbereich`, `rollen_min_grad`, `zoom_*`,
`sperre_ms` …), jede mit Bereich und Erklärung, damit der Einstellungsdialog sie ohne Sonderfälle anzeigen kann.

**`gesten.js` (hlks-kern):** Laufzeit im Hauptfaden, startet Kamera (`getUserMedia`, 640×480) und Worker
(`gesten-worker.js`, MediaPipe aus `/lib/mediapipe/`).

- `starten({ hand, kameraId, stellschrauben }) → Promise` (wirft mit deutscher Meldung, wenn die Kamera fehlt oder
  verweigert ist), `stoppen()`, `einstellen(teil)`, `laeuft()`, `kameras() → [{ id, name }]`
- `abonnieren(fn) → abmelden`; Ereignisse:
  - `{ art: 'zustand', zustand: 'aus'|'bereit'|'hand'|'aktiv', geste: null|'drehen'|'rollen'|'zoomen'|'verschieben', fehler? }`
  - `{ art: 'punkte', punkte: [[x, y], …21] | null }` – Bildkoordinaten 0…1, wie im Spiegel; höchstens 15/s
  - `{ art: 'schritt', geste: 'G2'|'G3'|'G6', richtung: 'links'|'rechts'|'hoch'|'runter'|null }`
- `testmodus(an)`: Ereignisse laufen weiter, aber nichts wird ausgelöst (Testfeld im Dialog).
- `aufnahmeStarten()` / `aufnahmeBeenden() → { version, hand, bilder: [{ t, punkte, welt, haendigkeit }] }` – nur
  Punkte, nie Bilder (Testfolgen von Christian).
- Fliessende Gesten löst `gesten.js` selbst aus: `fenster.aktive()?.instanz?.navigieren?.({...})`. Schritt-Gesten löst
  **nicht** `gesten.js` aus, sondern `gesten-ausloesen.js`.
- Ruhebetrieb, Freigabe der Kamera bei verstecktem Fenster und die Sperre neben der Maus liegen hier.

**`navigieren({ drehen: [dx, dy], rollen, verschieben: [dx, dy], zoom })` je Ansicht (hlks-kern):** alle Felder
wahlweise. `drehen` in Bogenmass (dx um die Hochachse des Bildschirms, dy um die waagrechte), `rollen` in Bogenmass um
die Blickachse (positiv = gegen den Uhrzeiger, wie der Betrachter sieht), `verschieben` als Anteil der Ansichtsgrösse,
`zoom` als Faktor (> 1 näher), zur Mitte des Bildausschnitts. 3D (`ansicht3d.js`), Grundriss und Schema; Grundriss und
Schema ignorieren `drehen` und `rollen`. `zoomAlles()` stellt in 3D die Hochrichtung wieder auf.

**`gesten-ausloesen.js` (hlks-modul):** abonniert `gesten.js`, führt Schritt-Gesten aus: G2 → Seite vor/zurück im
obersten Dialog bzw. in Liste/Hilfe mit Seiten, sonst die belegte Aktion (Vorgabe `ansicht.reiter-vor`/`-zurueck`);
G3 → im aktiven Fenster (oberster Dialog, Hilfe, Liste) eine Bildschirmseite rollen, in Plan, Schema und 3D nichts;
G6 → belegte Aktion (Vorgabe `ansicht.zoom-alles`). Danach die schwebende Meldung.

**Oberfläche (hlks-modul):** Grundschalter `ansicht.gesten`, Statusleistensymbol, Handskelett, Dialog «Gesten»,
Gestenliste, Aktionen `ansicht.reiter-vor`/`-zurueck`, Speicherung in `BenutzerEinstellungen.cs`, Doku. Bis
`gesten.js` da ist, baut hlks-modul gegen eine Attrappe mit genau dieser Schnittstelle (nur in Tests).
