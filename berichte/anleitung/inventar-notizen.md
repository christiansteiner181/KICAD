# Notizen zum Funktionsinventar (Stand bd513c0)

Begleitdatei zu `inventar.json`. Das Inventar ist die Grundlage einer interaktiven Bedienungs- bzw. Testanleitung für das HLKS-CAD «KICAD».

## 1. Herkunft und Methode

- **Stand:** Commit `bd513c0` (Zweig main, W2 zusammengeführt). Während der Arbeit ist main auf `f4738e1` weitergerückt (W1 «Dialoge und Paletten verschiebbar» zusammengeführt). Das Inventar beschreibt weiter `bd513c0`; alle Zeilenangaben unter «quelle» sind Zeilen der Dateien **in `bd513c0`** (mit `git show bd513c0:<Datei>` ermittelt) und stimmen in der Arbeitskopie nicht mehr überall: `OBERFLAECHE.md`, `aktionen-bearbeiten.js`, `aktionen.js`, `projektbrowser.js`, `dialoge.js`, `eigenschaften.js`, `koordination-panel.js`, `symbole.js`, `app.js`, `index.html` und mehrere CSS-Dateien haben sich verschoben; `layout.js` ist gelöscht.
- **W1 ist nicht beschrieben**, aber im Stand `f4738e1` sehen folgende Funktionen anders aus (vor dem Bildskript klären, welchen Stand Christian prüft): jeder Dialog (verschiebbar, Hintergrund nicht mehr abgedunkelt, Einklappen), `eigenschaften-palette` (PP läuft über die Palettenverwaltung; neuer Knopf Ansicht › Fenster › «Paletten»), `m3b-kollisionsliste` und `m3b-aus-liste` (Palette «Koordination» ist jetzt eine Palette der Verwaltung), `projektbrowser-aufbau` (Projektbrowser ist angemeldete Palette), `anordnung-kacheln-reiter` (Gruppe «Fenster» hat einen Knopf mehr).
- **Quellen für Orte und Kürzel:** der Code. Die Aktionen stehen in `src/Hlks.Web/wwwroot/js/oberflaeche/aktionen-*.js` und `js/familien/aktionen-*.js`; Register, Gruppen und Gruppenreihenfolge in `menueband.js` (`REGISTER`, `GRUPPENFOLGE`). Die Namen der Knöpfe sind die `titel` der Aktionen (ohne die unsichtbaren Trennstriche). Spezifikationen, Testberichte und `OBERFLAECHE.md` lieferten Ablauf, Erwartung und Prüfpunkte; was dort steht, aber im Code keinen Ort hat, steht unten bei «Nicht gefunden».
- **Zeilenangaben** wurden maschinell aufgelöst (jede Quelle ist eine wirklich gefundene Zeile in `bd513c0`); der Generator liegt nur im Arbeitsordner der Sitzung, nicht im Repository.
- **Nichts erfunden / Testwerte:** Alle Eingaben stammen aus den Browsertests oder den Spezifikationen: Wand 200 mm, Tür 900 × 2100, Fenster 1200 × 1350 mit Brüstung 900 (`werkzeuge.browser.mjs`); Boden 300, Decke 250, Dach 200 (`huelle.browser.mjs`); Rohr DN 20 auf Höhe 300 im System KW, Strang KW-V1 (`leitung.browser.mjs`); drei Testrohre DN 50 durch die Südwand (`koordination.browser.mjs`). Die Testserien (tests/daten/familien) sind fiktiv. Von mir frei gewählt, ohne Quelle, nur als Platzhalter zum Ausprobieren und im Text als Testwert gekennzeichnet: Gefälle 2 %, Schichtdicke 200 mm im Aufbau-Beispiel, Kürzel «PE-S» und «CNS», Namen und Plannummern (E.10, 2317-101 aus dem Platzhalter des Dialogs, 1-0768-H01/S01/L01 nach dem Muster der Spezifikation Serien-PDF). Keine Normwerte, keine Herstellerwerte, keine Durchmesser ausser den fiktiven der Testserien.
- **Hintergrund (nur kurz):** MCP-Server «hlks», Kommandozeile und KI-Verständnistest (Modul 0a) sind nur im Kapitel 0a erwähnt. Die Gruppe «Gebäudetechnik › Leitungen/Apparate» zeigt gesperrte Platzhalterknöpfe Formteil (PF), Sanitärapparat (PX) und Armatur (PA) («folgt in Modul 2/3»); sie sind keine Funktionen und nicht aufgenommen.

## 2. Zahl der Funktionen je Kapitel

| Kapitel | Modul | Titel | Funktionen |
| --- | --- | --- | --- |
| start | – | Start und Oberfläche | 40 |
| m0a | 0a | Datenmodell und Kern-API | 7 |
| m0b | 0b | Arbeitsplatz, Projektexplorer, Ansichten, Live-Abgleich | 19 |
| m1a | 1a | Grundriss und 2D | 22 |
| m1b | 1b | Grundlagen, Räume, openBIM, IFC-Übertrag | 12 |
| m1c | 1c | Gebäudehülle | 15 |
| m1d | 1d | HKAP-Universalschnittstelle | 10 |
| m2 | 2 | Familien und Familie aus Unterlagen | 23 |
| m3 | 3 | Leitungen und Schema | 26 |
| m3b | 3b | Kollision und Aussparungen | 18 |
| zusatz | – | Zusatzbausteine ausserhalb der Module | 20 |
| | | **Summe** | **212** |

Kapitel: 11. Bilder: 212 (eines je Funktion, siehe Abschnitt 6).

## 3. Nicht gefunden (in Spezifikation oder Testbericht versprochen, im Code kein Ort gefunden)

1. **Einlage- und Grundleitungspläne anlegen** (`einlageplaene_anlegen`, `entwurf/modul-3.md` B10 Punkt 4, `src/Hlks.Kern/Plan/EinlagePlaene.cs`): kein Knopf, kein Menüeintrag (nur MCP). Im Inventar steht der Umweg: Grundriss anlegen und in der Palette `darstellung` = einlagen bzw. grundleitungen setzen (`zb-einlageplan`, `zb-grundleitungsplan`). Ob die Palette die Eigenschaften `darstellung`, `einlagen`, `masseKnicke` und `verlegung` tatsächlich anzeigt, ist nur aus `OBERFLAECHE.md` (Nachtrag C1c: «zeigt die Eigenschaftspalette von selbst aus dem Katalog») abgeleitet, nicht aus Code bestätigt.
2. **Projektweite Einstellungen** `Projektinformation.rohrbezeichnung` (NW/DN) und `Projektinformation.bemassungEinheiten` (Masseinheit je Gewerk): im Code kein Bedienort. Der Dialog «Projektinformationen» ist nur lesend; das Element Projektinformation steht nicht im Projektbrowser. Einstellbar ist nur `rohrbezeichnung` je Ansicht (Palette bei nichts gewählt). Betrifft `zb-rohr-nwdn`, `m1a-bemassung`.
3. **Listenansicht «Kollisionsliste» / «Aussparungsliste»** (`ansichten/liste.js` und `listenformat.js` können sie zeichnen): der Dialog «Liste anlegen» bietet nur Raumliste, Strangliste, Mengen und Materialliste. Ebenso fehlt ein Ort für die **CSV-Ausgabe** der beiden Listen, die der Testbericht 3b nennt (nur Abnahmeskript).
4. **`aussparungen_zusammenfassen`** (Testbericht 3b): kein eigener Knopf; das Zusammenfassen geschieht in «Aussparungen vorschlagen».
5. **IFC über zwei Punktpaare ausrichten** (`nachbearbeitung.md` N1, Testbericht 1b): nicht gebaut, zurückgestellt.
6. **Prinzipschema** (`schemaart = prinzipschema`, `entwurf/modul-3.md`): «Schema anlegen» legt nur Strangschemas an; das Prinzipschema folgt mit Modul 5/6.
7. **Strang von Hand mit HKAP zuordnen:** kein Dialog (`OBERFLAECHE.md`, Abschnitt HKAP: «kennt nur MCP»).
8. **Zonen** in der Projektbrowser-Gliederung «Energie und Umwelt» (`entwurf/projektbrowser-gliederung.md`: «Räume bzw. Zonen (mit Angrenzung)»): der Code hat keinen Zonen-Ast.
9. **Mitgelieferte Familienbibliothek `kataloge/familien/`** («Familie laden», `OBERFLAECHE.md`, `modul-2.md`): der Ordner existiert in `bd513c0` nicht. Das Inventar benutzt stattdessen die Testfamilien unter `tests/daten/familien` per Pfad.
10. **Symbolsatz im Revit-Stil (Teil S, `entwurf/menueband-revit.md`)** samt Kontaktbogen: nicht untersucht, nicht Funktion der Bedienung (Stand unklar).
11. **Verknüpfungen auf dem Desktop:** «KICAD» und «KICAD aktualisieren» liegen vor (`C:\Users\chris\Desktop`), das Skript `werkzeuge/kicad_starten.ps1` steht im Repository; ein Skript, das die .lnk-Dateien erzeugt, wurde nicht gefunden (nur das Protokoll `zuteilung.md`).
12. **Zweitrechner-Fälle** (angeheftetes Projekt erscheint auf dem zweiten Rechner, Gliederung gilt auf allen Rechnern des Benutzers): Code vorhanden (Ablage je Rechner), mit einem Rechner nicht prüfbar.

## 4. Widersprüche (Spezifikation sagt X, Code Y)

1. **Anzahl Knöpfe im Register Koordination:** `OBERFLAECHE.md` Zeile 699 («elf Knöpfe in vier Gruppen»), Testbericht 3b Prüfliste 22 («11 Knöpfe») und Kopfkommentar von `aktionen-koordination.js` Zeile 2 («zehn Knöpfe in vier Gruppen») gegen den Code: **zwölf Aktionen in fünf Gruppen** (Vorgaben, Typenliste, Kollisionen, Aussparungen, BCF; die Typenliste kam mit C1c).
2. **«Grenze Kernbohrung»:** Testbericht 3b Prüfliste 3 (Zeile 151) und `entwurf/modul-3b.md` (Zeilen 7, 267) kennen die Vorgabe; im Code und in `OBERFLAECHE.md` (Nachtrag C1b) ist sie entfallen (Entscheid 19.10.2026: Kernbohrungen wählt nur ein Mensch), ein alter Wert erscheint nur mit «nicht mehr verwendet».
3. **Beschriftung der Rohre:** `OBERFLAECHE.md` Zeile 666 («… lautet die Beschriftung «Kürzel Ø Aussendurchmesser» (z. B. «CNS Ø22»)») und Testbericht 3 Zeile 186 («CNS Ø22», «PEX Ø16») gegen den Code (`Leitungsbezeichnung.cs`, `Rohrgroesse.cs`, `familien-text.js` Zeile 750): Nicht-Abwasser «CNS NW 20» bzw. «CNS DN 20», Abwasser «PE-S 110» (B9b ersetzt B9).
4. **Kalibrieren:** Testbericht 1b Zeile 171 («wählt … über den Projektbrowser, nicht per Klick auf das Bild») gegen `kalibrieren.js` Zeilen 2–3 und 104 («Unterlage in der Optionsleiste wählen oder anklicken»). Im Inventar als Hinweis bei `m1b-kalibrieren`.
5. **Projektbrowser «Koordination»:** `entwurf/projektbrowser-gliederung.md` Zeile 28 («Unterlagen (IFC, BCF)») gegen `projektbrowser-gliederung.js`: nur Unterlagen der Art ifc.
6. **Serien-PDF:** `entwurf/serien-pdf.md` Zeile 43 («Register Ansicht bzw. Datei») gegen den Code: der Knopf steht nur im Register Ansicht, Gruppe «Planblätter» (dazu Kontextmenüs im Projektbrowser).
7. **Kürzel-Liste (KS):** `aktionen-projekt.js` Zeile 155 (`REGISTER_NAMEN`) kennt «Beschriften», «Einfügen», «Hülle», «Koordination» und «Familie» nicht; Kürzel wie TX, DI, DL, EL, CM stehen darum unter «Allgemein» (`OBERFLAECHE.md` verspricht die Liste «nach Register»).
8. **Schreibweise von Knopf- und Dialognamen:** `OBERFLAECHE.md` nennt «Familien im Explorer …», «Element einfügen …»; der Menüknopf und der Dialogtitel «Familien im Explorer» sowie der Menüknopf «Element einfügen» haben kein «…» (die Kontextmenüs und die Schema-Leiste haben es).
9. **Zahlen im Testbericht 0a** (50 Klassen, 15 Medien, 15 Systemtypen, Prüfungen P01–P11, 26 MCP-Werkzeuge, 175 Elemente/138 Verbindungen) sind überholt (Katalog und Beispielprojekt sind gewachsen, Prüfungen bis P20); im Inventar ohne Zahlen bzw. mit Hinweis.
10. **Kürzel Füllbereich/Schnitt/Kote:** `entwurf/modul-1a.md` Zeile 161–162 führt «Füllbereich, Kote, Schnitt» unter «Werkzeuge mit Revit-Kürzeln» auf; im Code hat nur die Kote ein Kürzel (EL), Füllbereich, Nordpfeil und Schnitt keines (Testbericht 1a nennt das ausdrücklich).
11. **HKAP-Vorgabeadresse:** Der Dialog «Mit HKAP verknüpfen» schlägt `http://localhost:5013` (HKAP im Betrieb) vor; Spezifikation 1d verlangt Tests nur gegen die Testkopie (5098). Kein Fehler, aber eine Falle für die Anleitung (steht in `m1d-verbinden`).

## 5. Offene Punkte und Unsicherheiten (im Bild oder am Gerät klären)

- **Reihenfolge der Gruppen im Register Gebäudetechnik:** es gibt keine feste `GRUPPENFOLGE`; sie folgt der Ladereihenfolge (vermutlich «Apparate und Armaturen» vor «Leitungen»). `OBERFLAECHE.md` nennt «Leitungen» zuerst. Im Register Ansicht stehen «Planblätter» und «Schema» nach «Darstellung».
- **Kontextregister:** die Gruppen heissen Netz, Bearbeiten, Eigenschaften, Herkunft, Leitung, Schema; ihre Reihenfolge ist nicht festgelegt.
- **Palette zeigt `verlegung`, `darstellung` usw.:** siehe Abschnitt 3 Punkt 1.
- **Abgeleitete Werte mit «?»** (m0a-warum): `OBERFLAECHE.md` verspricht das «?» neben jedem Wert; ob auch die Gruppe «Abgeleitet» (Fläche) eines hat, nicht gegen Code geprüft.
- **Typname der Testfamilie beim Platzieren** (`m2-platzieren`): «Testheizkörper Typ 22 (fiktiv): 22-600-1000»; die Koordinaten vl (4950, 3950, 120) und rl (4950, 3900, 120) stammen aus dem Browsertest für Drehung 90° bei (5000, 3000, 0).
- **Beispielprojekt:** hat keine Planblätter, Familien, Fenster, Türen, Einlagen; die Beispiele legen sie selbst an. `kataloge/materialien.json` ist leer.
- **Echte Dateien:** Bernstrasse liegt lokal unter `%USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\` (architekt_3d.ifc, architekt_eg.dwg, hlks_hk_2024-09-17.ifc, hlks_hk_2026-02-10.ifc, sanitaer_eg.dwg, schema_wasser.dxf) und für 3b als Abnahmeprojekt `%USERPROFILE%\HLKS_CAD_build\abnahme3b` (Skript `werkzeuge/abnahme3b_bernstrasse.ps1`). Nie nach W: schreiben.
- **Schreiben auf W:** `m2-freigeben` (Bibliothek speichern), `zb-bib-explorer`, `zb-spdf-ausgabe` (Ordner unter %TEMP% lassen) können auf W: schreiben.

## 6. Bilder (Vorlage für ein Bildskript)

Ein Bild je Funktion; «id» = Id der Funktion in `inventar.json`. Projekt `beispiel` = frisches Beispielprojekt «MFH Musterweg» (Fenstergrösse wie die Browsertests, 1600 × 950; Design dunkel, wo nichts anderes steht), `bernstrasse` = Projekt Bernstrasse 42 (IFC-Übernahme bzw. Abnahmeprojekt `abnahme3b`). Der Server des Bildskripts startet wie die Browsertests mit `HLKS_TEST=1` und Ablagen unter %TEMP%.

**Gemeinsame Vorbereitung (Zustand «V» in der Spalte «zustand» bedeutet: zusätzlich zum frischen Beispielprojekt):**

- Testfamilien aus `tests/daten/familien` mit `familie_laden`: test_rohrserie, test_bogen, test_tstueck, test_reduktion (Rohr); test_abwasserserie, test_abwasserbogen, test_abwasser_t, test_abwasserreduktion; test_kanalserie, test_kanalbogen, test_kanal_t, test_kanalreduktion; test_heizkoerper.
- 3b-Aufbau «SETUP»: Testrohrserie geladen, Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von (x, −1000, 1000) bis (x, 1000, 1000) quer durch W-EG-S, System KW, Typ DN 50; Projektvorgaben mit Quelle «FIKTIV» gesetzt (siehe `koordination.browser.mjs`).
- HKAP-Bilder (Kapitel 1d, `m3-hkap-straenge`): HKAP-Testkopie auf Port 5098 laufen lassen, nie 5013.
- Bildschirmfotos von Hand (kein Skript herstellt sie): `start-icon`, `start-aktualisieren`, `start-ordner-explorer`; zweites Programmfenster (`anordnung-eigenes-fenster`) nur mit Edge `--app` und Pop-ups; KI-Lauf (`m2-ki-lauf`) nur mit der Attrappe des Browsertests `familie-unterlagen.browser.mjs`; Bibliothek auf W: (`start-familien`, `start-bibliothek`, `zb-bib-*`) nur mit `HLKS_FAMILIENBIBLIOTHEK` auf einen Ordner unter %TEMP% und vorbereiteten Familien (Browsertest `bibliothek.browser.mjs` baut das auf).

| id | projekt | ansicht | zustand |
| --- | --- | --- | --- |
| start-icon | beispiel | Desktop mit Verknüpfungen und Programmfenster | Bildschirmfoto von Hand: Desktop-Ausschnitt mit «KICAD» und «KICAD aktualisieren», danach das Programmfenster mit Startbildschirm (kann kein Skript herstellen) |
| start-aktualisieren | beispiel | Hinweisfenster «KICAD läuft bereits mit dem neuesten geprüften Stand …» | Bildschirmfoto von Hand nach Klick auf «KICAD aktualisieren» ohne neueren Stand |
| start-aufbau | beispiel | Startbildschirm | Startbildschirm ohne offenes Projekt; Beispielprojekt MFH Musterweg einmal geöffnet (eine Kachel), Ablage erreichbar |
| start-kacheln | beispiel | Startbildschirm › Zuletzt geöffnet | Zwei Kacheln: MFH Musterweg mit 3D-Vorschaubild und eine angeheftete Kachel mit Nadel |
| start-anheften | beispiel | Startbildschirm › Kachelmenü | Kachelmenü offen mit den vier Einträgen, Kachel MFH Musterweg markiert |
| start-ordner-explorer | beispiel | Windows-Explorer | Explorer-Fenster im Ordner e_KICAD des Beispielprojekts (von Hand) |
| start-alle-projekte | bernstrasse | Startbildschirm › Alle KICAD-Projekte auf W: | Tabelle mit mindestens den Bernstrasse-Projekten und der Hinweiszeile; Ablage erreichbar |
| start-suche | beispiel | Startbildschirm | Suchfeld mit «Muster» gefüllt, nur die Kachel MFH Musterweg sichtbar |
| start-familien | beispiel | Startbildschirm › Familien | Rechte Spalte mit zwei Familien (Name, Status Entwurf, Version, Projekt) |
| start-bibliothek | beispiel | Dialog «Familienbibliothek (nur lesen)» | Dialog offen mit der Tabelle der Bibliothek, kein Projekt offen |
| start-oeffnen | bernstrasse | Dialog «Projekt öffnen» | Ordner-Browser im Ordner W:\02_Projektdateien Objekte, ein Objektordner mit Marke «enthält Projekt» markiert |
| start-neu-beispiel | beispiel | Dialog «Beispielprojekt anlegen» | Dialog mit eingetragenem Zielordner unter %TEMP%\HLKS_CAD\beispiel\e_KICAD |
| start-haus-strgd | beispiel | Startbildschirm über offenem Projekt | Startbildschirm mit «Offen: MFH Musterweg» und sichtbarem Knopf «Zurück zum Projekt» |
| start-vorschaubild | beispiel | Startbildschirm › Kachel | Kachel MFH Musterweg mit 3D-Vorschaubild (Isometrie von Südwesten) |
| menueband-register | beispiel | Menüband | Registerzeile mit allen zehn Registern, Register «Ändern» aktiv |
| menueband-schnellzugriff | beispiel | Kopfzeile | Kopfzeile mit den vier Symbolen, Tooltip «Rückgängig (Strg+Z)» sichtbar |
| menueband-stufen | beispiel | Menüband (Gebäudetechnik) | Dreimal dasselbe Register bei 1920, 1366 und 1024 px Fensterbreite |
| menueband-flyout | beispiel | Menüband mit offenem Flyout | Fenster 800 px breit, Register Ansicht, Flyout einer zusammengeklappten Gruppe offen |
| menueband-minimieren | beispiel | Menüband minimiert | Zwei Bilder: nur Gruppentitel; nur Register mit offener Überlagerung |
| menueband-kontextregister | beispiel | Menüband › Ändern \| Waschtisch | Grundriss AN-GR-EG offen, WT-E.02 gewählt, Register «Ändern \| Waschtisch» aktiv |
| menueband-gesperrt | beispiel | Menüband › Ändern | Nichts gewählt, Tooltip von «Verschieben (MV)» mit Hinweis sichtbar |
| optionsleiste | beispiel | Optionsleiste | Grundriss AN-GR-EG, Werkzeug Wand aktiv, Optionsleiste mit allen Wand-Feldern |
| statusleiste | beispiel | Statusleiste | WT-E.02 gewählt, Statusleiste mit Auswahltext, Abgleichzeit und Stand |
| ansichtsreiter | beispiel | Ansichtsreiter | Fünf Ansichten offen, Fenster schmal, Liste «▾ 2 weitere» geöffnet |
| projektbrowser-aufbau | beispiel | Projektbrowser | Gliederung Gebäude, Äste Ansichten, Gebäude und Systeme aufgeklappt, Grundriss EG hervorgehoben |
| projektbrowser-suche | beispiel | Projektbrowser | Suchfeld «WT-E» mit den zwei Waschtischen E.02 und E.03 als Treffer |
| projektbrowser-gliederung | beispiel | Projektbrowser | Gliederung Sanitär gewählt, Äste Ansichten, Pläne, Anlagen, Familien, Räume sichtbar, «Weitere» zugeklappt |
| projektbrowser-mehrere | beispiel | Projektbrowser | Gliederungen Sanitär + Heizung gewählt, Liste noch offen mit zwei Häkchen |
| projektbrowser-gewerkfarben | beispiel | Projektbrowser | Gliederung Sanitär, hell und dunkel nebeneinander |
| projektbrowser-merken | beispiel | Projektbrowser | Zweites Projekt offen, Gliederung weiterhin Lüftung |
| eigenschaften-palette | beispiel | Eigenschaftenpalette | WT-E.02 gewählt, Palette mit allen Gruppen offen |
| eigenschaften-ansicht | beispiel | Eigenschaftenpalette | Grundriss EG aktiv, nichts gewählt, Palette zeigt AN-GR-EG |
| anordnung-reiter-ziehen | beispiel | Ansichtsfläche | Beim Ziehen des Reiters «3D Gesamt»: Kreuz der Andockziele mit Vorschau rechts; danach zwei Gruppen nebeneinander |
| anordnung-trenner | beispiel | Ansichtsfläche | Zwei Gruppen, Trenner nach links verschoben |
| anordnung-kacheln-reiter | beispiel | Ansichtsfläche | Drei Ansichten als Kacheln (Grundriss EG, Grundriss 1.OG, 3D) |
| anordnung-reitermenue | beispiel | Ansichtsreiter | Kontextmenü am Reiter mit den vier Einträgen |
| anordnung-eigenes-fenster | beispiel | Zweitfenster | Hauptfenster mit Grundriss EG, daneben das Zweitfenster mit 3D Gesamt, derselbe Raum in beiden gewählt |
| kuerzel-tippen | beispiel | Statusleiste | Statusleiste links mit «Kürzel: W…» nach dem ersten Buchstaben |
| kuerzel-liste-ks | beispiel | Dialog «Tastaturkürzel» | Dialog offen, Suchfeld leer, obere Tabellenzeilen sichtbar |
| kuerzel-esc | beispiel | Grundriss | Vor und nach Esc: WT-E.02 gewählt, danach nichts gewählt |
| m0a-beispielprojekt-inhalt | beispiel | Projektbrowser | Beispielprojekt frisch angelegt, Äste Ansichten, Gebäude (UG, EG, 1.OG mit Räumen) und Systeme aufgeklappt |
| m0a-katalog | beispiel | Dialog «Katalog» | Dialog offen, Klasse Waschtisch gewählt, Eigenschaftenliste sichtbar |
| m0a-pruefbefunde | beispiel | Dialog «Prüfbefunde» | Dialog offen mit allen Befunden des Beispielprojekts, Zeile angeklickt |
| m0a-netz-folgen | beispiel | Dialog «Netz folgen – stromab» | WC-E.02 gewählt, Dialog mit der Liste der Elemente stromab |
| m0a-warum | beispiel | Dialog «Warum – Herkunft des Werts» | Raum E.02 gewählt, Warum-Dialog zur Fläche |
| m0a-loeschen-vorschau | beispiel | Dialog «WT-E.03 löschen» | WT-E.03 gewählt, Vorschau-Dialog mit Tabelle der Änderungen und Befunde |
| m0a-rueckgaengig | beispiel | Grundriss EG | Raum E.04 mit geändertem Namen «Zimmer Süd» im Stempel, Meldung «Rückgängig: …» eingeblendet |
| m0b-projekt-oeffnen | beispiel | Dialog «Projekt öffnen» | Ordner-Browser im Ordner des Beispielprojekts, Knopf «Öffnen» |
| m0b-projekt-anlegen | beispiel | Dialog «Projekt anlegen» | Dialog mit Projektname «Testprojekt» und Zielordner |
| m0b-projekt-schliessen | beispiel | Startbildschirm | Nach dem Schliessen: Startbildschirm, Projektbrowser leer |
| m0b-zuletzt-geoeffnet | beispiel | Menüband › Datei | Register Datei mit der Gruppe «Zuletzt geöffnet» und zwei Einträgen |
| m0b-projektinfo | beispiel | Dialog «Projektinformationen» | Dialog offen mit allen Zeilen |
| m0b-synchronisieren | beispiel | Statusleiste | Statusleiste mit «● Abgleich <Zeit>» und Meldung «Mit der Projektablage abgeglichen.» |
| m0b-reservierung | beispiel | Dialog «Reservierungen» | Dialog mit einer eigenen Reservierung auf E.04 |
| m0b-live | beispiel | drei Ansichten nebeneinander | Grundriss EG, 3D und Raumliste nebeneinander, Raum E.02 gewählt, neuer Name sichtbar |
| m0b-ansicht-grundriss | beispiel | Dialog «Grundriss anlegen» | Dialog mit Geschoss 1.OG, Massstab 100, Gewerke Sanitär und Architektur angehakt |
| m0b-ansicht-3d | beispiel | 3D-Ansicht | 3D Gesamt mit WC-E.02 gewählt (Auswahlfarbe), Fassade durchscheinend |
| m0b-ansicht-liste | beispiel | Liste | Raumliste des Beispielprojekts mit allen Räumen und Flächen |
| m0b-ansicht-duplizieren | beispiel | Projektbrowser | Ansichten › Grundrisse mit der neuen Kopie |
| m0b-vorlage | beispiel | Projektbrowser | Ast «Ansichtsvorlagen» mit VL-GR-1.OG |
| m0b-sichtbarkeit | beispiel | Dialog «Sichtbarkeit/Grafiken» | Grundriss 1.OG, Dialog mit angekreuztem Rohr |
| m0b-zoom | beispiel | Grundriss EG | Vor und nach ZF: eingezoomter Ausschnitt, danach ganzes Gebäude |
| m0b-design | beispiel | Oberfläche | Derselbe Grundriss EG hell und dunkel |
| m0b-plan-bedienung | beispiel | Grundriss EG | Ausschnitt Bad E.02 mit gewähltem Waschtisch und Hover-Tipp |
| m0b-steuerleiste | beispiel | Grundriss EG | Steuerleiste unten mit Massstab 100 und Detailgrad fein |
| m0b-umbenennen | beispiel | Projektbrowser | Kontextmenü auf einer Ansicht mit Eintrag «Umbenennen» hervorgehoben |
| m1a-wand | beispiel | Grundriss EG Sanitär | Wandwerkzeug aktiv, Dicke 200, Gummiband von (14000\|2000) nach (18000\|2000) mit Länge 4000 mm; daneben fertiger Zug aus vier Wänden |
| m1a-fangen-ziffern | beispiel | Grundriss EG Sanitär | Wandwerkzeug aktiv, Fangsymbol (Quadrat) an einer Ecke, temporäre Bemassung «3000 mm» |
| m1a-tuer | beispiel | Grundriss EG Sanitär | Türwerkzeug mit Vorschau der Öffnung in der Südwand bei x 6000, Aufschlag rechts |
| m1a-fenster | beispiel | Grundriss EG Sanitär | Nordwand mit gesetztem Fenster bei x 2500 und Glaslinie |
| m1a-raum | beispiel | Grundriss EG Sanitär | Wandzug (14000..18000 × 2000..6000) mit erkanntem Raum E.10 «Lager» und Stempel |
| m1a-achse | beispiel | Grundriss EG Sanitär | Achse A links vom Gebäude mit Kreis und Beschriftung, Optionsleiste «Nächste: B» |
| m1a-bemassung | beispiel | Grundriss EG Sanitär | Bemassung unterhalb der neuen Wand mit Masszahl und Schrägstrichen |
| m1a-kote | beispiel | Schnitt 1 | Kotensymbol an der Oberkante Boden mit «±0.00» |
| m1a-text | beispiel | Grundriss EG Sanitär | Textnotiz «Bad» im neuen Raum |
| m1a-detaillinie | beispiel | Grundriss EG Sanitär | Detaillinie aus drei Punkten im neuen Raum |
| m1a-fuellbereich | beispiel | Grundriss EG Sanitär | Gefüllte Fläche mit Muster im Raum E.10, Wandlinien sichtbar |
| m1a-nordpfeil | beispiel | Grundriss EG Sanitär | Nordpfeil rechts oben neben dem Gebäude |
| m1a-schnitt | beispiel | Schnitt 1 | Schnitt durch das Beispielhaus von Süden: geschnittene Aussenwände schraffiert, Apparate in Ansicht |
| m1a-verschieben | beispiel | Grundriss EG Sanitär | Zwei gewählte Wände (Auswahlfarbe) vor dem Verschieben, Gummiband vom Basis- zum Zielpunkt |
| m1a-kopieren | beispiel | Grundriss EG Sanitär | Original- und Kopiewand im Abstand von 2000 mm |
| m1a-drehen | beispiel | Grundriss EG Sanitär | Wand vor und nach der Drehung um 90° um ihr Ende |
| m1a-spiegeln | beispiel | Grundriss EG Sanitär | Wand mit Tür vor dem Spiegeln, Spiegelachse x 19000 als Gummiband |
| m1a-griffe | beispiel | Grundriss EG Sanitär | Gewählte Wand mit Griffen an beiden Enden |
| m1a-planblatt | beispiel | Planblatt | Leeres Blatt A3 quer mit Plankopf, Nummer 2317-101 |
| m1a-ansicht-platzieren | beispiel | Planblatt | Blatt A3 quer mit Grundriss EG Sanitär 1:50, Bemassung und Nordpfeil |
| m1a-pdf | beispiel | Planblatt-PDF | Browser-PDF-Betrachter mit dem Blatt 2317-101 |
| m1a-2d-ist-3d | beispiel | 3D Gesamt | Neuer Wandzug mit Tür und Fenster im 3D neben dem Beispielhaus |
| m1b-ifc-datei | bernstrasse | Dialog «IFC übernehmen» (Übersicht) | Dialog mit architekt_3d.ifc geladen, Übersicht mit Geschossen und Bereichen |
| m1b-ifc-lage | bernstrasse | Dialog «IFC übernehmen» (Lagebericht) | Dialog mit hlks_hk_2026-02-10.ifc, roter Kasten, Optionen der Ausrichtung ohne Wahl |
| m1b-ifc-uebernehmen | bernstrasse | Dialog «IFC übernommen» | Ergebnisdialog mit Tabellen je Klasse nach Übernahme von architekt_3d.ifc |
| m1b-ifc-fassung | bernstrasse | Dialog «IFC übernehmen» (Vorschau Abgleich) | Vorschau mit Spalten neu/geändert/gelöscht für die zweite Fassung |
| m1b-cad | bernstrasse | Grundriss EG | Grundriss EG mit DWG-Unterlage des Architekten (graue Linien) unter den KICAD-Wänden |
| m1b-pdf-bild | beispiel | Grundriss EG | Grundriss EG mit eingefügter PDF-Seite als Hintergrund, noch nicht kalibriert |
| m1b-kalibrieren | beispiel | Grundriss EG | Kalibrieren aktiv, zwei gewählte Punkte auf der PDF-Unterlage und zwei Zielpunkte |
| m1b-unterlagen-verwalten | beispiel | Dialog «Unterlagen verwalten» | Liste mit zwei Unterlagen, eine ausgeblendet |
| m1b-unterlage-menue | beispiel | Projektbrowser › Unterlagen | Kontextmenü auf einer Unterlage mit allen Einträgen |
| m1b-3d-hintergrund | bernstrasse | 3D Gesamt | 3D mit grau durchscheinendem IFC-Hintergrund von Bernstrasse und den übernommenen Wänden |
| m1b-koordinaten | bernstrasse | Dialog «Koordinaten» | Dialog mit zwei IFC-Unterlagen (Architekt, H+K) und Abweichung |
| m1b-raeume-aus-waenden | bernstrasse | Grundriss EG | Grundriss EG Bernstrasse mit automatisch erkannten Räumen (Füllung) und Raumliste daneben |
| m1c-decke | beispiel | Dialog «Boden erzeugen» | Geschoss UG, Dicke 300, Vorschau mit Fläche 127.82 m² und Oberkante |
| m1c-material | beispiel | Dialog «Material» | Formular mit Name Testputz, λ-Testwert und Quelle «Testwert», links Liste mit dem neuen Material |
| m1c-aufbau | beispiel | Dialog «Aufbau» | Aufbau AW-Test mit zwei Schichten, R-Wert angezeigt, U-Hinweis rot «Rsi/Rse fehlen» |
| m1c-zuweisen | beispiel | Dialog «Aufbau zuweisen» | Dialog mit Aufbau AW-Test gewählt und Wand W-EG-S angehakt |
| m1c-fenster-kennwerte | beispiel | Dialog «Fensterkennwerte» | Dialog mit Testwerten und Vorschau des gerechneten Uw |
| m1c-uebergang | beispiel | Dialog «Übergang Rsi / Rse» | Dialog mit vier Gruppen, Testwerte und Quelle |
| m1c-huelle-pruefen | beispiel | Dialog «Hülle prüfen» | Beispielprojekt ohne Aufbauten: Abschnitt «Bauteile ohne Aufbau» mit Wänden |
| m1c-waermebruecken | beispiel | Dialog «Wärmebrücken vorschlagen» | Vorschau mit Zahlen und Hinweis zur fehlenden Quelle für ψ |
| m1c-raumgrenzen | beispiel | Liste Raumgrenzen | Liste mit allen Zeilen und den Fusszeilen der Summen |
| m1c-huellbauteile | beispiel | Liste Hüllbauteile | Liste der Aussenbauteile mit Fusszeile |
| m1c-darstellung-huelle | beispiel | Grundriss EG Sanitär | Darstellung Hülle eingeschaltet: Aussenwände rot, Innenseiten, Legende unten links |
| m1c-huelle-3d | beispiel | 3D Gesamt | Hüllflächen eingeschaltet, Infofeld einer Raumgrenze unten rechts geöffnet |
| m1c-projektbrowser-huelle | beispiel | Projektbrowser | Ast Hülle aufgeklappt mit Materialien, Aufbauten, Wärmebrücken |
| m1c-ohne-quelle | beispiel | Eigenschaftenpalette | Material mit Kennwert λ und der Marke «ohne Quelle» |
| m1c-ifc-huelle | bernstrasse | Projektbrowser › Hülle | Bernstrasse nach IFC-Übernahme: Aufbauten und Materialien aufgeklappt |
| m1d-verbinden | beispiel | Dialog «Mit HKAP verknüpfen» | Dialog mit Adresse http://localhost:5098, Projekt P-005 gewählt, Trockenlauf-Zusammenfassung sichtbar |
| m1d-abgleich | beispiel | Dialog «Abgleich mit HKAP» | Dialog mit Zusammenfassung und Räume-Tabelle nach Verknüpfung mit P-005 |
| m1d-konflikte | beispiel | Dialog «Abgleich mit HKAP» | Konflikt-Tabelle mit zwei Zeilen, bei einer «KICAD» gewählt |
| m1d-uebernehmen | beispiel | Dialog «Abgleich mit HKAP» | Ergebniskasten nach der Übernahme mit Tabelle der Änderungen |
| m1d-senden | beispiel | Dialog «Abgleich mit HKAP» | Ergebniskasten mit «Vorschau (Trockenlauf – nichts wurde geändert)» und Knopf «Jetzt senden» |
| m1d-befunde | beispiel | Dialog «Prüfbefunde an HKAP senden» | Dialog mit Vorschau von zwei neuen Pendenzen |
| m1d-nicht-platziert | beispiel | Projektbrowser | Ast «Nicht platzierte Räume (n)» aufgeklappt mit Kontextmenü |
| m1d-raum-platzieren | beispiel | Grundriss EG Sanitär | Raum-Werkzeug aktiv mit vorbelegter Nummer, Dialog «Gezeichneten Raum zuordnen» |
| m1d-raum-zuordnen | beispiel | Dialog «Einem gezeichneten Raum zuordnen» | Liste mit zwei Räumen, der passende zuerst |
| m1d-plankopf | beispiel | Planblatt | Planblatt A3 mit gefülltem Plankopf (Projekt, Phase aus HKAP) |
| m2-browser-familien | beispiel | Projektbrowser | Ast Familien mit einer geladenen Testfamilie (Marke Entwurf) und dem Knoten Katalog |
| m2-neue-familie | beispiel | Dialog «Neue Familie» | Dialog mit Kategorie Heizkörper und Name «Heizkörper Typ 22» |
| m2-familie-laden | beispiel | Dialog «Familie laden» | Dialog mit Pfad zur Testfamilie und Vorschau des Kerns |
| m2-editor-oeffnen | beispiel | Familieneditor | Editor der Testfamilie mit Seite Parameter links und vier Ansichten rechts |
| m2-register-familie | beispiel | Menüband › Familie | Register Familie im Editor, alle Gruppen sichtbar |
| m2-parameter | beispiel | Familieneditor › Parameter | Parametertabelle mit Formel oberkante = bodenabstand + hoehe und «→»-Wert |
| m2-typen | beispiel | Familieneditor › Typen | Typkatalog mit zwei Typen und einer rosa Zelle |
| m2-koerper | beispiel | Familieneditor › Körper | Körperliste mit zwei Quadern, einer gewählt und im 3D hervorgehoben |
| m2-anschluesse | beispiel | Familieneditor › Anschlüsse | Anschlusstabelle mit vl und rl, Pfeile in der 3D-Ansicht |
| m2-sinnbild | beispiel | Familieneditor › Sinnbild | Sinnbildliste mit einem Kreis und einer Linie, Vorschau |
| m2-ansicht | beispiel | Familieneditor | Vier Fenster der Testfamilie mit Achsen und Anschlussmarken |
| m2-pruefen | beispiel | Familieneditor › Prüfen | Fehlerliste mit zwei Meldungen |
| m2-platzieren | beispiel | Grundriss EG Sanitär | Platzieren aktiv, Vorschau des Heizkörpers mit Pfeil, Optionsleiste mit Typ und Drehung |
| m2-palette-element | beispiel | Eigenschaftenpalette | Platziertes Element mit Gruppe Familie und Gruppe Anschlüsse |
| m2-aus-unterlagen | beispiel | Dialog «Familie aus Unterlagen» | Schritt 1 mit datenblatt_testventil.pdf in der Liste und Kategorie Ventil |
| m2-ki-lauf | beispiel | Dialog «Familie aus Unterlagen» | Schritt 2 während des Laufs (Attrappe des Browsertests) mit Protokollzeilen und Knopf «KI-Lauf abbrechen» |
| m2-pruefansicht | beispiel | Familieneditor › Entwurf prüfen | Prüfansicht einer Familie mit Abdeckungstabelle, einem Konflikt und gesperrter Freigabe (Testfamilie der Attrappe) |
| m2-konflikt | beispiel | Familieneditor › Entwurf prüfen | Konfliktliste mit zwei Fundstellen (PDF Seite 3, CSV) und hervorgehobenem Vorschlag |
| m2-wert-bestaetigen | beispiel | Familieneditor › Entwurf prüfen | Abdeckungstabelle mit gelber Zelle und Knopf «Wert bestätigen» |
| m2-masse-kontrolle | beispiel | Familieneditor › Entwurf prüfen | Abschnitt Massvergleich mit Zählung und ein Kontrollbild |
| m2-freigeben | beispiel | Familieneditor › Entwurf prüfen | Freigabebereich mit aktivem Knopf «Freigeben» und Feld «In Bibliothek speichern» |
| m2-befunde-browser | beispiel | Familieneditor › Unterlagen | Befund-Browser mit PDF-Seitenbild und markierten Boxen |
| m2-quelle-ansehen | beispiel | Dialog «Quelle ansehen» | Ausschnitt einer PDF-Tabellenzelle mit rot markierter Box |
| m3-serie-laden | beispiel | Grundriss EG Sanitär | Rohrwerkzeug aktiv ohne Serie: Optionsleiste mit Hinweis und Knopf «Leitungsserie laden …» |
| m3-rohr-zeichnen | beispiel | Grundriss EG Sanitär | Rohrwerkzeug mit L-Zug von (14000\|3000) über (16000\|3000) nach (16000\|5000), Vorschau-Zeile oben «2 Geraden, 3900 mm · 1 Bogen 90°» |
| m3-winkelfang | beispiel | Grundriss EG Sanitär | Rohrwerkzeug, Gummiband rastet auf 45.0°; zweites Bild: rote Ablehnung für 60° mit Vorschlag |
| m3-steigleitung | beispiel | Grundriss EG Sanitär | Rohrwerkzeug mit Höhenwechsel: senkrechtes Stück als Ring, Vorschau-Zeile mit Bogen |
| m3-anschluss-fang | beispiel | Grundriss EG Sanitär | Rohrwerkzeug nahe am Heizkörper mit Anschlussmarken und Beschriftung am gefangenen Anschluss |
| m3-abzweig | beispiel | Grundriss EG Sanitär | Abzweig im rechten Winkel von einer Geraden, T-Stück an der Abzweigstelle |
| m3-kanal | beispiel | Grundriss EG Sanitär | Kanal 400×200 mit Beschriftung im Grundriss |
| m3-gefaelle-daemmung | beispiel | Grundriss EG Sanitär | Abwasserleitung mit Gefälle und Dämmung, Palette «Leitung» offen |
| m3-griffe-leitung | beispiel | Grundriss EG Sanitär | Gewähltes Rohr mit Griffen an den Enden und am Knick |
| m3-palette-leitung | beispiel | Eigenschaftenpalette | Palette mit Gruppe «Leitung» und Rückfrage «Ganzer Leitungszug» |
| m3-aufteilen | beispiel | Dialog «Aufteilen mit Serie» | Rohr SW-E.02-WC gewählt, Dialog mit Serie, Grösse und Vorschau |
| m3-materialliste | beispiel | Materialliste | Liste mit Testrohrserie DN 20 und Testbogen |
| m3-serien-editor | beispiel | Familieneditor › Leitungsserie | Seite Leitungsserie der Testrohrserie mit Kontrolltabelle |
| m3-neue-serie | beispiel | Dialog «Neue Familie» | Kategorie Kanal gewählt, Auswahl Querschnitt sichtbar |
| m3-schema-anlegen | beispiel | Dialog «Schema anlegen» | Dialog mit Sanitär und SW angekreuzt |
| m3-schema-ansehen | beispiel | Schema AN-SCHEMA-SAN | Strangschema des Beispielprojekts, WT-E.02 gewählt, Banner oben |
| m3-schema-verschieben | beispiel | Schema AN-SCHEMA-SAN | WT-E.02 mit Punkt (Hand-Lage), Fanglinien während des Ziehens |
| m3-schema-verbinden | beispiel | Schema AN-SCHEMA-SAN | Dialog «Anschlüsse wählen» mit vier Paaren, zwei Ventile im Schema |
| m3-schema-trennen | beispiel | Schema AN-SCHEMA-SAN | Trennen aktiv, Menü «Trennen: …» mit zwei Einträgen |
| m3-schema-einfuegen | beispiel | Schema AN-SCHEMA-SAN | Neuer Waschtisch WT-NEU mit gestricheltem Rahmen, Banner aufgeklappt |
| m3-schema-neu-anordnen | beispiel | Schema AN-SCHEMA-SAN | Rückfrage «Alle Hand-Lagen entfernen?» mit drei Knöpfen |
| m3-schema-grundriss | beispiel | Grundriss EG Sanitär | Nach «Im Grundriss zeigen»: WC-E.02 gewählt und eingezoomt |
| m3-im-schema-zeigen | beispiel | Schema AN-SCHEMA-SAN | Nach «Im Schema zeigen»: WT-E.02 gewählt und eingezoomt |
| m3-schema-planblatt | beispiel | Planblatt | Blatt A2 mit dem Strangschema Sanitär |
| m3-pruefungen | beispiel | Dialog «Prüfbefunde» | Befundliste gefiltert auf P18 im Beispielprojekt |
| m3-hkap-straenge | beispiel | Dialog «Abgleich mit HKAP» | Abschnitt Stränge mit gebündeltem Heizungsstrang |
| m3b-register | beispiel | Menüband › Koordination | Register Koordination mit allen fünf Gruppen |
| m3b-vorgaben | beispiel | Dialog «Projektvorgaben» | Dialog mit zwei gesetzten und einer fehlenden Vorgabe, Vorschau des Kerns unten |
| m3b-typenliste | bernstrasse | Dialog «Typenliste» | Reiter «Unbekannte Typen» mit mehreren Revit-Typnamen, Kategorie-Auswahl und Quelle |
| m3b-kollisionen-pruefen | beispiel | Dialog «Kollisionen prüfen» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Ergebnis des Prüflaufs im Dialog |
| m3b-kollisionsliste | beispiel | Palette «Koordination» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Palette mit den drei Durchdringungen, Grundriss darüber auf die Lage gezoomt |
| m3b-kollision-status | beispiel | Palette «Koordination» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; zwei Zeilen angehakt, Status akzeptiert, Kommentar sichtbar |
| m3b-aus-vorschlagen | beispiel | Dialog «Aussparungen vorschlagen» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Vorschau mit Zahlen und Liste |
| m3b-aus-nummerieren | beispiel | Dialog «Aussparungen nummerieren» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Dialog mit Vorschau der Nummern |
| m3b-aus-liste | beispiel | Palette «Koordination» › Aussparungen | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Aussparungsliste mit nummerierten Zeilen |
| m3b-aus-status | beispiel | Palette «Koordination» › Aussparungen | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Zeile freigegeben, rote Ablehnung für einen unzulässigen Übergang |
| m3b-aus-form | beispiel | Palette «Koordination» › Aussparungen | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Vorschau «rechteckig → rund Ø …» mit den Knöpfen Ausführen/Abbrechen |
| m3b-abgleich | bernstrasse | Dialog «Abgleich mit der Architekten-Fassung» | Dialog mit gewählter IFC-Unterlage und Vorschau |
| m3b-aus-plan | beispiel | Aussparungsplan AN-AUS-EG | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Aussparungsplan EG mit grauer Architektur, Aussparungssymbolen und Massketten |
| m3b-aus-ifc | beispiel | Dialog «IFC für den Architekten» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Dialog mit Pfad, Fassung IFC4 und Antwort des Kerns |
| m3b-bcf-export | beispiel | Dialog «BCF exportieren» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Dialog mit Pfad, Filtern und Ergebnis |
| m3b-bcf-import | beispiel | Dialog «BCF importieren» | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Dialog mit Lesen-Ergebnis und Vorschau der Übernahme |
| m3b-gliederung | beispiel | Projektbrowser | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Gliederung Koordination, Äste Kollisionen und Aussparungen aufgeklappt |
| m3b-kollisionsmarken | beispiel | Grundriss EG Sanitär | Beispielprojekt, Testrohrserie (fiktiv, DN 50) geladen, drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf Höhe 1000 quer durch die Südwand W-EG-S gezeichnet (Rohr-Werkzeug PI), Projektvorgaben gesetzt; Kollisionsmarken an den drei Rohren in der Südwand |
| zb-einlage-verlegung | beispiel | Grundriss EG Sanitär | Ein Rohr mit Verlegung «einlage»: im Grundriss weg, im 3D daneben sichtbar |
| zb-einlageplan | bernstrasse | Einlageplan D.ü.2.UG | Neu angelegter Grundriss 2.UG mit darstellung = einlagen, einlagen = decke: grauer Hintergrund, braune Einlagen, magentafarbene Bausteine und Massketten |
| zb-grundleitungsplan | bernstrasse | Grundleitungsplan 2.UG | Grundriss des untersten Geschosses mit Darstellung grundleitungen: Leitungen im Erdreich und Sohlenkoten |
| zb-einlage-schema | bernstrasse | Schema Abwasser | Strangschema mit Einlagen im Deckenband und Kontrollschacht als Schnitt |
| zb-bib-explorer | beispiel | Dialog «Familien im Explorer» | Dialog mit eingeschaltetem Schalter, zwei Filtern, Liste mit Häkchen und Fusszeile |
| zb-bib-marken | beispiel | Projektbrowser | Ast Familien mit einer grauen Bibliotheksfamilie und einer Projektfamilie mit Marke «neuere Version» |
| zb-bib-platzieren | beispiel | Projektbrowser | Kontextmenü auf einer Bibliotheksfamilie mit den fünf Einträgen |
| zb-bib-aktualisieren | beispiel | Dialog «Aktualisieren …» | Dialog mit Vorschau einer neueren Version und Hinweis zu betroffenen Elementen |
| zb-bib-status | beispiel | Statusleiste | Statusleiste mit «Bibliothek erreichbar» und geöffnetem Dialog «Familienbibliothek» |
| zb-spdf-oeffnen | beispiel | Dialog «Serien-PDF» | Dialog nach Anlegen der drei Planblätter, Plansatz Sanitär gewählt |
| zb-spdf-plansaetze | beispiel | Dialog «Serien-PDF» | Linke Spalte mit drei automatischen Plansätzen |
| zb-spdf-abwaehlen | beispiel | Dialog «Serien-PDF» | Tabelle mit einem abgewählten Plan und geänderter Reihenfolge |
| zb-spdf-gewerk | beispiel | Dialog «Serien-PDF» | Tabelle mit grauem Gewerk-Vorschlag und Knopf «bestätigen» |
| zb-spdf-ausgabe | beispiel | Dialog «Serien-PDF» | Ausgabebereich mit Anlass, Ordner, Einzel-PDF-Häkchen und Sammel-PDF-Name |
| zb-spdf-exportieren | beispiel | Dialog «Serien-PDF» | Ansicht «Fertig: Plansatz Sanitär» mit Dateiliste und Knopf «Ordner öffnen» |
| zb-spdf-eigener-satz | beispiel | Dialog «Serien-PDF» | Eigener Plansatz «Plansatz Bauherr» mit zwei Plänen |
| zb-spdf-browser | beispiel | Projektbrowser | Ast Pläne mit drei Gewerkgruppen und Kontextmenü auf einem Planblatt |
| zb-spdf-merken | beispiel | Dialog «Serien-PDF» | Dialog nach erneutem Öffnen mit der gespeicherten Auswahl |
| zb-rohr-abwasser | beispiel | Grundriss EG Sanitär | Abwasserrohr im System SW mit Beschriftung «PE-S 110» |
| zb-rohr-nwdn | beispiel | Grundriss EG Sanitär | Zwei Rohre: KW-Rohr «CNS NW 20» und nach Umstellung der Ansicht «CNS DN 20» |
