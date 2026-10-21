# Notizen zum Funktionsinventar (Stand: siehe «stand» in inventar.json; nachgeführt 20.10.2026 auf 636e78c)

Begleitdatei zu `inventar.json`. Das Inventar ist die Grundlage einer interaktiven Bedienungs- bzw. Testanleitung für das HLKS-CAD «KICAD».

## 1. Herkunft und Methode

- **Stand:** das Inventar beschreibt den Stand des Commits in «stand» (Zweig main mit W1 «Dialoge und Paletten verschiebbar» und W2 «Ansichten frei anordnen»; die erste Fassung beschrieb `bd513c0`, vor W1). Alle Zeilenangaben unter «quelle» sind Zeilen der Dateien in diesem Stand; beim Nachführen auf W1 wurden sie maschinell umgerechnet (jede Zeile wurde im alten Stand gelesen und im neuen Stand wiedergefunden, zur Gegenprobe über die Diff-Hunks abgebildet). `layout.js` ist gelöscht (die Paletten laufen über `paletten.js`).
- **Nachführung 20.10.2026 (Stand 6dc390e, hlks-test):** 36 Funktionen neu, 3 Hinweise angepasst. Neu: Kapitel «start» +5 (Hilfe «?», F1 über Knopf, F1 im Dialog, Knopf «Claude» in der Kopfleiste, Symbole im Revit-Stil), Kapitel «zusatz» +7 (N3: Einlageplan- und Grundleitungsplan-Knopf, Projektinformationen bearbeiten und im Projektbrowser, Kollisions-/Aussparungsliste, CSV, KS nach Register), neues Kapitel «m3c» (14) und «m4» (10). Reihenfolge der Kapitel: start, m0a … m3b, m3c, m4, zusatz. Orte, Kürzel und Zeilen (Feld «quelle») wurden im Code gesucht (Skript im Arbeitsordner der Sitzung, bricht ab, wenn ein Muster fehlt); Rechenwerte stehen nur dort, wo sie aus Tests und Katalog stammen (Beispielprojekt: Anlage A-SAN, 16 Apparate, K 0.5 fiktiv; Waschtisch Zeile 8, Badewanne Zeile 17 im Excel). Angepasst wurden die Hinweise von `m0b-projektinfo` (Dialog jetzt bearbeitbar), `zb-einlageplan` (Knopf seit N3) und `kuerzel-liste-ks` (Befund behoben). Nicht nachgeführt: die älteren Zeilenangaben der übrigen Funktionen (sie stammen aus 7c36986; einzelne Zeilen können durch spätere Änderungen verrutscht sein).
- **Nachführung 20.10.2026 (Stand 636e78c, hlks-test):** Kapitel «m7» «Planblatt wie H+K» (Modul 7 Teil A) mit 11 Funktionen neu, hinter «m4»; Reihenfolge der Kapitel: start, m0a … m3b, m3c, m4, m7, zusatz. Neu: `m7-planblatt-anlegen`, `m7-revision-hinzufuegen`, `m7-revisionsliste`, `m7-plankopf`, `m7-titelband`, `m7-stempel-eigenschaft`, `m7-stempel-serienpdf`, `m7-satz-revision`, `m7-schema-kopf`, `m7-liste-planblatt`, `m7-befund-p27`. Orte und Kürzel (Feld «wo») und Zeilen (Feld «quelle») wurden im Code geprüft (aktionen-plaene.js, eigenschaften.js, serienpdf-dialog.js, projektbrowser.js, Plankopf.cs, Revisionen.cs, Plannummernvorschlag.cs, PlanPruefung.cs, Listentabelle.cs, klassen.json, buero.json); die Annahmen des Bauagenten (Fach K, Absenderdaten, «Rev. Datum» als eigene Zeile, Titelband Vorgabe aus, Sonderblatt-Nummern, Listen im PDF) stehen in den Feldern «hinweis» und im Testbericht 7A. Angepasst wurden die Hinweise von `m4-liste` und `m4-csv` (Listen sind seit Teil A auf Planblättern platzierbar), `m1a-planblatt` (Erwartung: neuer Plankopf, Dialog mit Vorschlag) und `m1a-ansicht-platzieren` (Listen gehen auch). Keine Kürzel: alle Funktionen des Kapitels haben keines. Es gibt keine Funktion «Absender wählen»: die Standorte stehen in `kataloge/buero.json` und werden nicht in der Oberfläche bearbeitet.
- **Nachführung 20.10.2026 (Modul 7 Teil B2, Dialog «IFC exportieren», hlks-modul):** Kapitel «m7» +3 Funktionen (jetzt 18): `m7-ifc-export` (Hauptfunktion des Knopfs «IFC exportieren», Register Datei › Gruppe Ausgabe), `m7-ifc-umfang`, `m7-ifc-ids`; die Einleitung von «m7» nennt den Dialog. Orte aus `aktionen-ifc.js` und `menueband.js` (Gruppe «Ausgabe» im Register Datei), Zahlen des Beispielprojekts (148 Elemente, 298 Anschlüsse, 142 Verbindungen; Sanitär mit Räumen und IFC2X3: 125 Elemente, 13 Räume, 23 Übersprungene; fiktive Testregel «Pset_Bauherr.Freigabe»: 69 Rohre) aus einem Lauf des Browsertests `ifc-export.browser.mjs` am 20.10.2026 (Testwerte, keine Normwerte). Bilder: noch keine (Bildvorgaben im Feld «bild»). Die Zahlentabelle in Abschnitt 2 ist nicht nachgeführt.
- **Nachführung 21.10.2026 (N20 und N21, hlks-modul):** Kapitel «zusatz» +3 Funktionen: `zb-n20-grob` (Detailgrad «grob»: geschnittene Wände, Stützen und Unterzüge schwarz voll), `zb-n21-schnitthoehe` (Schnitthöhe des Grundrisses nach SIA 400 in den Eigenschaften der Ansicht ansehen: schreibgeschützte Zeile mit Herkunft und Hinweisen) und `zb-n21-schalter` (Haken `schnitthoeheNachSia`, Handwert). Orte aus `fenster.js` (Auswahl Detailgrad) und `eigenschaften.js`/`schnitthoehe-palette.js`; die Prüfbilder des Auftrags (`n21-palette-*.png`) liegen unter %TEMP%\HLKS_CAD\n21, Bilder für die Seite sind noch nicht erzeugt. Stand-Angabe in inventar.json unverändert.
- **Nachführung 21.10.2026 (N12, hlks-modul):** Kapitel «m1b» +2 Funktionen (jetzt 14): `m1b-ifc-darstellung` (Sichtbarkeit/Grafiken: IFC-Unterlagen zeigen als Nachbau, Original oder beide; Ansichtseigenschaft `ifcDarstellung`) und `m1b-ifc-farben` (Dialog «Farben …» an der IFC-Unterlage: Original und Nachbau je Farbe und Deckkraft); `m1b-3d-hintergrund` verweist darauf. Bilder noch nicht erzeugt (Projekt «bernstrasse»; Prüfbilder des Auftrags unter %TEMP%\HLKS_CAD\n12). Stand-Angabe in inventar.json unverändert.
- **Nachführung 21.10.2026 (N15, hlks-modul):** Kapitel «zb» +3 Funktionen: `zb-n15-begehen` (Kontextmenü «Raum begehen» im Grundriss, Ich-Perspektive im 3D, Beenden mit Esc/Haus-Knopf/Würfelfläche), `zb-n15-bewegen` (Tasten und Maus beim Begehen; Annahmen am Freitag zu bestätigen) und `zb-n15-augenhoehe` (Benutzereinstellung `ansicht3d.augenhoehe`). Prüfbilder `n15-begehen-raum.png`, `n15-begehen-ausserhalb.png`, `n15-begehen-l-raum.png` unter %TEMP%\HLKS_CAD\n15 (aus `begehen.browser.mjs`); Bilder für die Anleitung noch nicht erzeugt. Stand-Angabe in inventar.json unverändert.
- **W1 ist beschrieben** (14 neue Funktionen im Kapitel «start», Ids `dialog-…` und `palette-…`, Prüfpunkte aus «Fertig, wenn» in `entwurf/fenster-revit.md`). Angepasst wurden `eigenschaften-palette`, `eigenschaften-ansicht`, `projektbrowser-aufbau`, `anordnung-kacheln-reiter` (Gruppe «Fenster» hat vier Knöpfe), `m3b-kollisionsliste`, `m3b-aus-liste` (Palette «Koordination» ist eine Palette der Verwaltung) und `anordnung-reiter-ziehen` (Verweis); jede Funktion, die einen Dialog öffnet, trägt im «hinweis» den Satz, dass der Dialog verschiebbar, in der Grösse änderbar und einklappbar ist und der Plan dahinter nicht mehr abgedunkelt wird.
- **Quellen für Orte und Kürzel:** der Code. Die Aktionen stehen in `src/Hlks.Web/wwwroot/js/oberflaeche/aktionen-*.js` und `js/familien/aktionen-*.js`; Register, Gruppen und Gruppenreihenfolge in `menueband.js` (`REGISTER`, `GRUPPENFOLGE`). Die Namen der Knöpfe sind die `titel` der Aktionen (ohne die unsichtbaren Trennstriche). Spezifikationen, Testberichte und `OBERFLAECHE.md` lieferten Ablauf, Erwartung und Prüfpunkte; was dort steht, aber im Code keinen Ort hat, steht unten bei «Nicht gefunden».
- **Zeilenangaben** wurden maschinell aufgelöst (jede Quelle ist eine wirklich gefundene Zeile im Stand); der Generator liegt nur im Arbeitsordner der Sitzung, nicht im Repository.
- **Nichts erfunden / Testwerte:** Alle Eingaben stammen aus den Browsertests oder den Spezifikationen: Wand 200 mm, Tür 900 × 2100, Fenster 1200 × 1350 mit Brüstung 900 (`werkzeuge.browser.mjs`); Boden 300, Decke 250, Dach 200 (`huelle.browser.mjs`); Rohr DN 20 auf Höhe 300 im System KW, Strang KW-V1 (`leitung.browser.mjs`); drei Testrohre DN 50 durch die Südwand (`koordination.browser.mjs`). Die Testserien (tests/daten/familien) sind fiktiv. Von mir frei gewählt, ohne Quelle, nur als Platzhalter zum Ausprobieren und im Text als Testwert gekennzeichnet: Gefälle 2 %, Schichtdicke 200 mm im Aufbau-Beispiel, Kürzel «PE-S» und «CNS», Namen und Plannummern (E.10, 2317-101 aus dem Platzhalter des Dialogs, 1-0768-H01/S01/L01 nach dem Muster der Spezifikation Serien-PDF). Keine Normwerte, keine Herstellerwerte, keine Durchmesser ausser den fiktiven der Testserien.
- **Hintergrund (nur kurz):** MCP-Server «hlks», Kommandozeile und KI-Verständnistest (Modul 0a) sind nur im Kapitel 0a erwähnt. Die Gruppe «Gebäudetechnik › Leitungen/Apparate» zeigt gesperrte Platzhalterknöpfe Formteil (PF), Sanitärapparat (PX) und Armatur (PA) («folgt in Modul 2/3»); sie sind keine Funktionen und nicht aufgenommen.

## 2. Zahl der Funktionen je Kapitel

| Kapitel | Modul | Titel | Funktionen |
| --- | --- | --- | --- |
| start | – | Start und Oberfläche | 59 |
| m0a | 0a | Datenmodell und Kern-API | 7 |
| m0b | 0b | Arbeitsplatz, Projektexplorer, Ansichten, Live-Abgleich | 19 |
| m1a | 1a | Grundriss und 2D | 22 |
| m1b | 1b | Grundlagen, Räume, openBIM, IFC-Übertrag | 14 |
| m1c | 1c | Gebäudehülle | 15 |
| m1d | 1d | HKAP-Universalschnittstelle | 10 |
| m2 | 2 | Familien und Familie aus Unterlagen | 23 |
| m3 | 3 | Leitungen und Schema | 26 |
| m3b | 3b | Kollision und Aussparungen | 18 |
| m3c | 3c | KI-Palette Claude | 14 |
| m4 | 4 | Sanitär rechnen – Belastungswerte | 10 |
| m7 | 7A | Planblatt wie H+K | 11 |
| zusatz | – | Zusatzbausteine ausserhalb der Module | 27 |
| | | **Summe** | **273** |

Kapitel: 14 (mit m7; Stand 636e78c: 273 Funktionen, vorher 262 auf 6dc390e). Vorher (7c36986 mit W1/W2): 226 Funktionen in 11 Kapiteln; neu 36 (start +5, zusatz +7, m3c 14, m4 10). Bilder: siehe Abschnitt 6 und 7 (eines je Funktion, wo ein Skript den Zustand herstellen kann; für die 36 neuen noch keines).

## 3. Nicht gefunden (in Spezifikation oder Testbericht versprochen, im Code kein Ort gefunden)

**Nachtrag 20.10.2026 (6dc390e):** erledigt sind Punkt 1 (Einlage-/Grundleitungsplan: Knöpfe in Ansicht › Erstellen, N3), Punkt 2 (Projektinformationen bearbeitbar, Knoten im Projektbrowser, N3), Punkt 3 (Kollisions-/Aussparungsliste und CSV, N3) und Punkt 10 (Symbolsatz im Revit-Stil, Teil S, jetzt Funktion «start-symbole-revit»). Offen bleiben 4–9, 11, 12 und diese neuen Lücken (für `luecken.json` vorgeschlagen; die Datei habe ich nicht verändert):

- **13. Einlageplan/Grundleitungsplan:** im Dialog keine Wahl von Name und Bauteil (der Kern kennt sie nicht).
- **14. Sanitär-Zusammenstellung:** Listen sind nicht auf Planblättern platzierbar (`ansicht_platzieren` nimmt nur Grundriss, Schnitt, Ansicht, Schema; gilt für alle Listen); Ausgabe nur als CSV. Abflusskennzahl K hat kein Quellenfeld und keine Auswahlliste (SN 592000 fehlt).
- **15. Modul 4 Teil B (Dimensionierung):** DN je Teilstrecke, Gefälle, Füllung, Hausanschluss: wartet auf SN 592000, SN EN 12056, SVGW W3 (liegen im Normenverzeichnis nicht vor). Entsprechend keine Funktion im Inventar.
- **16. Normenverzeichnis:** Volltext erst für 62 von 315 Normen; die Palette weist darauf hin.
- **17. KI-Palette im Zweitfenster:** Paletten sind dort ausgeblendet; es gibt keinen Knopf «Claude» (Absicht, W2).
- **18. Echter Claude-Lauf:** Das Inventar beschreibt die Palette nach den Abläufen mit der Attrappe und dem Abnahmelauf vom 20.10.2026 (Gespräch 20261020-020626-b3ef); Bilder entstehen nur mit der Attrappe.
- **19. Rechner ohne Abo:** die Meldung «Claude nicht verfügbar» (`m3c-meldungen`) ist nur durch Tests mit der Attrappe belegt.

1. **Einlage- und Grundleitungspläne anlegen** (`einlageplaene_anlegen`, `entwurf/modul-3.md` B10 Punkt 4, `src/Hlks.Kern/Plan/EinlagePlaene.cs`): kein Knopf, kein Menüeintrag (nur MCP). Im Inventar steht der Umweg: Grundriss anlegen und in der Palette `darstellung` = einlagen bzw. grundleitungen setzen (`zb-einlageplan`, `zb-grundleitungsplan`). Ob die Palette die Eigenschaften `darstellung`, `einlagen`, `masseKnicke` und `verlegung` tatsächlich anzeigt, ist nur aus `OBERFLAECHE.md` (Nachtrag C1c: «zeigt die Eigenschaftspalette von selbst aus dem Katalog») abgeleitet, nicht aus Code bestätigt.
2. **Projektweite Einstellungen** `Projektinformation.rohrbezeichnung` (NW/DN) und `Projektinformation.bemassungEinheiten` (Masseinheit je Gewerk): im Code kein Bedienort. Der Dialog «Projektinformationen» ist nur lesend; das Element Projektinformation steht nicht im Projektbrowser. Einstellbar ist nur `rohrbezeichnung` je Ansicht (Palette bei nichts gewählt). Betrifft `zb-rohr-nwdn`, `m1a-bemassung`.
3. **Listenansicht «Kollisionsliste» / «Aussparungsliste»** (`ansichten/liste.js` und `listenformat.js` können sie zeichnen): der Dialog «Liste anlegen» bietet nur Raumliste, Strangliste, Mengen und Materialliste. Ebenso fehlt ein Ort für die **CSV-Ausgabe** der beiden Listen, die der Testbericht 3b nennt (nur Abnahmeskript).
4. **`aussparungen_zusammenfassen`** (Testbericht 3b): kein eigener Knopf; das Zusammenfassen geschieht in «Aussparungen vorschlagen».
5. **IFC über zwei Punktpaare ausrichten** (`nachbearbeitung.md` N1, Testbericht 1b): nicht gebaut, zurückgestellt.
6. **Prinzipschema** (`schemaart = prinzipschema`, `entwurf/modul-3.md`): «Schema anlegen» legt nur Strangschemas an; das Prinzipschema folgt mit Modul 5/6.
7. **Strang von Hand mit HKAP zuordnen:** kein Dialog (`OBERFLAECHE.md`, Abschnitt HKAP: «kennt nur MCP»).
8. **Zonen** in der Projektbrowser-Gliederung «Energie und Umwelt» (`entwurf/projektbrowser-gliederung.md`: «Räume bzw. Zonen (mit Angrenzung)»): der Code hat keinen Zonen-Ast.
9. **Mitgelieferte Familienbibliothek `kataloge/familien/`** («Familie laden», `OBERFLAECHE.md`, `modul-2.md`): der Ordner existiert in `7c36986` nicht. Das Inventar benutzt stattdessen die Testfamilien unter `tests/daten/familien` per Pfad.
10. **Symbolsatz im Revit-Stil (Teil S, `entwurf/menueband-revit.md`)** samt Kontaktbogen: nicht untersucht, nicht Funktion der Bedienung (Stand unklar).
11. **Verknüpfungen auf dem Desktop:** «KICAD» und «KICAD aktualisieren» liegen vor (`C:\Users\chris\Desktop`), das Skript `werkzeuge/kicad_starten.ps1` steht im Repository; ein Skript, das die .lnk-Dateien erzeugt, wurde nicht gefunden (nur das Protokoll `zuteilung.md`).
12. **Zweitrechner-Fälle** (angeheftetes Projekt erscheint auf dem zweiten Rechner, Gliederung gilt auf allen Rechnern des Benutzers): Code vorhanden (Ablage je Rechner), mit einem Rechner nicht prüfbar.

## 4. Widersprüche (Spezifikation sagt X, Code Y)

1. **Anzahl Knöpfe im Register Koordination:** `OBERFLAECHE.md` Zeile 699 («elf Knöpfe in vier Gruppen»), Testbericht 3b Prüfliste 22 («11 Knöpfe») und Kopfkommentar von `aktionen-koordination.js` Zeile 2 («zehn Knöpfe in vier Gruppen») gegen den Code: **zwölf Aktionen in fünf Gruppen** (Vorgaben, Typenliste, Kollisionen, Aussparungen, BCF; die Typenliste kam mit C1c).
2. **«Grenze Kernbohrung»:** Testbericht 3b Prüfliste 3 (Zeile 151) und `entwurf/modul-3b.md` (Zeilen 7, 267) kennen die Vorgabe; im Code und in `OBERFLAECHE.md` (Nachtrag C1b) ist sie entfallen (Entscheid 19.10.2026: Kernbohrungen wählt nur ein Mensch), ein alter Wert erscheint nur mit «nicht mehr verwendet».
3. **Beschriftung der Rohre:** **`OBERFLAECHE.md` ist berichtigt** (19.10.2026, Entscheid Christian, B9b: Abwasser mit Aussendurchmesser «PE-S 110», alle anderen Rohre mit NW/DN nach Projekteinstellung, z. B. «CNS NW 20»; die Zeile steht im Abschnitt «Seite Leitungsserie»). Offen ist nur noch der Testbericht 3 Zeile 186 («CNS Ø22», «PEX Ø16»); der Code (`Leitungsbezeichnung.cs`, `Rohrgroesse.cs`, `familien-text.js`) beschriftet Nicht-Abwasser «CNS NW 20» bzw. «CNS DN 20», Abwasser «PE-S 110».
4. **Kalibrieren:** Testbericht 1b Zeile 171 («wählt … über den Projektbrowser, nicht per Klick auf das Bild») gegen `kalibrieren.js` Zeilen 2–3 und 104 («Unterlage in der Optionsleiste wählen oder anklicken»). Im Inventar als Hinweis bei `m1b-kalibrieren`.
5. **Projektbrowser «Koordination»:** `entwurf/projektbrowser-gliederung.md` Zeile 28 («Unterlagen (IFC, BCF)») gegen `projektbrowser-gliederung.js`: nur Unterlagen der Art ifc.
6. **Serien-PDF:** `entwurf/serien-pdf.md` Zeile 43 («Register Ansicht bzw. Datei») gegen den Code: der Knopf steht nur im Register Ansicht, Gruppe «Planblätter» (dazu Kontextmenüs im Projektbrowser).
7. **Kürzel-Liste (KS):** `aktionen-projekt.js` Zeile 155 (`REGISTER_NAMEN`) kennt «Beschriften», «Einfügen», «Hülle», «Koordination» und «Familie» nicht; Kürzel wie TX, DI, DL, EL, CM stehen darum unter «Allgemein» (`OBERFLAECHE.md` verspricht die Liste «nach Register»).
8. **Schreibweise von Knopf- und Dialognamen:** `OBERFLAECHE.md` nennt «Familien im Explorer …», «Element einfügen …»; der Menüknopf und der Dialogtitel «Familien im Explorer» sowie der Menüknopf «Element einfügen» haben kein «…» (die Kontextmenüs und die Schema-Leiste haben es).
9. **Zahlen im Testbericht 0a** (50 Klassen, 15 Medien, 15 Systemtypen, Prüfungen P01–P11, 26 MCP-Werkzeuge, 175 Elemente/138 Verbindungen) sind überholt (Katalog und Beispielprojekt sind gewachsen, Prüfungen bis P20); im Inventar ohne Zahlen bzw. mit Hinweis.
10. **Kürzel Füllbereich/Schnitt/Kote:** `entwurf/modul-1a.md` Zeile 161–162 führt «Füllbereich, Kote, Schnitt» unter «Werkzeuge mit Revit-Kürzeln» auf; im Code hat nur die Kote ein Kürzel (EL), Füllbereich, Nordpfeil und Schnitt keines (Testbericht 1a nennt das ausdrücklich).
11. **HKAP-Vorgabeadresse:** Der Dialog «Mit HKAP verknüpfen» schlägt `http://localhost:5013` (HKAP im Betrieb) vor; Spezifikation 1d verlangt Tests nur gegen die Testkopie (5098). Kein Fehler, aber eine Falle für die Anleitung (steht in `m1d-verbinden`).
12. **Kürzel-Liste (KS), Nachtrag 20.10.2026:** Punkt 7 ist durch N3 behoben (`kuerzel-liste.js › REGISTER_NAMEN` kennt alle Register).
13. **Spezifikation 3c (Internet):** `entwurf/modul-3c.md` Teil K sagt «mit `internet=true` zusätzlich WebSearch und unterlage_aus_url»; der Code (Entscheid Hauptlauf 20.10.2026, GespraechDienst) lässt mit Internet **keinen Projektzugriff** und keinen Projektkontext zu. Im Inventar (`m3c-internet`) gilt der Code.
14. **Spezifikation 3c (Verbotsliste):** die Spezifikation nennt Familien freigeben, Konflikte, HKAP, Freigaben; der Code verbietet 23 Werkzeuge (zusätzlich Status, BCF, Plan-/IFC-Ausgaben, Bibliothek). Im Inventar (`m3c-antwort`) gilt der Code.
15. **Spezifikation 3c (Knopf):** die Spezifikation kennt nur die Palette mit Häkchen und Kürzel KI; den Knopf «Claude» in der Kopfleiste (neben «?») ergänzte der Hauptlauf bei der Nacharbeit. Im Inventar als `start-claude-knopf` und in `m3c-ein-aus`.
16. **Spezifikation Modul 4:** «Register Gebäudetechnik **bzw. Analysieren**: Knopf Sanitär-Zusammenstellung» – der Code hat den Knopf nur in Gebäudetechnik › Apparate und Armaturen (ein Register «Analysieren» gibt es nicht). «Druck in Planblätter wie andere Listen» ist nicht umgesetzt (Listen sind im Kern nicht platzierbar); Ausgabe ist die CSV-Datei.

## 5. Offene Punkte und Unsicherheiten (im Bild oder am Gerät klären)

- **Reihenfolge der Gruppen im Register Gebäudetechnik:** es gibt keine feste `GRUPPENFOLGE`; sie folgt der Ladereihenfolge (vermutlich «Apparate und Armaturen» vor «Leitungen»). `OBERFLAECHE.md` nennt «Leitungen» zuerst. Im Register Ansicht stehen «Planblätter» und «Schema» nach «Darstellung».
- **Kontextregister:** die Gruppen heissen Netz, Bearbeiten, Eigenschaften, Herkunft, Leitung, Schema; ihre Reihenfolge ist nicht festgelegt.
- **Palette zeigt `verlegung`, `darstellung` usw.:** siehe Abschnitt 3 Punkt 1.
- **Abgeleitete Werte mit «?»** (m0a-warum): `OBERFLAECHE.md` verspricht das «?» neben jedem Wert; ob auch die Gruppe «Abgeleitet» (Fläche) eines hat, nicht gegen Code geprüft.
- **Typname der Testfamilie beim Platzieren** (`m2-platzieren`): «Testheizkörper Typ 22 (fiktiv): 22-600-1000»; die Koordinaten vl (4950, 3950, 120) und rl (4950, 3900, 120) stammen aus dem Browsertest für Drehung 90° bei (5000, 3000, 0).
- **Beispielprojekt:** hat keine Planblätter, Familien, Fenster, Türen, Einlagen; die Beispiele legen sie selbst an. `kataloge/materialien.json` ist leer.
- **Echte Dateien:** Bernstrasse liegt lokal unter `%USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\` (architekt_3d.ifc, architekt_eg.dwg, hlks_hk_2024-09-17.ifc, hlks_hk_2026-02-10.ifc, sanitaer_eg.dwg, schema_wasser.dxf) und für 3b als Abnahmeprojekt `%USERPROFILE%\HLKS_CAD_build\abnahme3b` (Skript `werkzeuge/abnahme3b_bernstrasse.ps1`). Nie nach W: schreiben.
- **Schreiben auf W:** `m2-freigeben` (Bibliothek speichern), `zb-bib-explorer`, `zb-spdf-ausgabe` (Ordner unter %TEMP% lassen) können auf W: schreiben.
- **Nachtrag 20.10.2026:** `m4-befunde` und `m4-k` brauchen einen hergestellten Zustand im Beispielprojekt (Art am Bodenablauf BA-U.02 entfernen bzw. K löschen); im frischen Beispielprojekt ist BA-U.02 auf bodenablauf_70 gesetzt und K = 0.5 (beides fiktive Annahme des Hauptlaufs).
- `zb-n3-einlageplan-knopf`/`-grundleitungsplan-knopf`: im Beispielprojekt gibt es keine Einlagen und keine Leitungen im Erdreich (der Dialog zeigt die leere Liste); aussagekräftig nur in der Bernstrasse-Kopie.
- `m3c-*`: Bilder nur mit der Attrappe (`tests/Hlks.ClaudeAttrappe`; Marken in der Frage: `[attrappe:antwort]`, `[attrappe:aendern=<Kennzeichen>]`, `[attrappe:langsam]`, `[attrappe:zweimal=<Kennzeichen>]`); die Normbilder mit dem Test-PDF (`tests/daten/normen`, `HLKS_NORMEN` auf eine Kopie).
- `m7-*` (Nachtrag 20.10.2026): alle Bilder im Beispielprojekt (Projektnummer 1-0768, Objekt «Testobjekt Worb (fiktiv)», Revisionsdaten 14.01. und 21.01.2026 erfunden). Die PDF-Bilder (`m7-plankopf`, `_2`, `m7-titelband_2`, `m7-stempel-eigenschaft_2`, `m7-schema-kopf_2`, `_3`, `m7-liste-planblatt_2`) sind mit `werkzeuge/pdf_bild.ps1` (Windows.Data.Pdf) gerendert und in einer Seite des Browserfensters gezeigt (wie `m1a-pdf`); die Ausschnitte (Kopf unten rechts bzw. unten links) schneidet die Seite per CSS aus dem Rendering (3000 px Breite). Das Erstelldatum im Plankopf ist das Datum des Bildlaufs (Systemdatum), Revisionsdaten sind fest gesetzt. Die Bilder entstehen in einer eigenen Szene je Zustand, weil jede Szene das Projekt neu anlegt.
- `start-f1-*`: Hilfefenster (zweites Browserfenster) und Tooltips sind in Edge headless nicht aufnehmbar; `window.open` ist im Browsertest ein Stub.

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

Die Tabelle gilt für den Stand `7c36986`; die Bilder der W1-Funktionen (Ids `dialog-…`, `palette-…`) stehen in `inventar.json` (Feld «bild») und in `bilder.json`.

### Nachtrag 20.10.2026 (Stand 6dc390e): Bildvorgaben der 36 neuen Funktionen

Gleiche Spalten wie oben (Quelle ist das Feld «bild» in `inventar.json`). Beim Herstellen: Build `%USERPROFILE%\HLKS_CAD_build\anleitung2`, `HLKS_KATALOG` auf `kataloge` des Repositorys. KI-Palette nur mit Attrappe.

| id | projekt | ansicht | zustand |
| --- | --- | --- | --- |
| start-hilfe-symbol | beispiel | Kopfleiste rechts | Beispielprojekt offen; Ausschnitt der Kopfleiste rechts mit den Knöpfen «Claude» und «?» (Tooltip des «?» sichtbar, nachgebildet) – das Fenster der Anleitung als zweites Bild, falls herstellbar |
| start-f1-knopf | beispiel | Menüband › Ansicht | Register Ansicht, Zeiger über «Liste» (Tooltip mit letzter Zeile «F1: Hilfe zu diesem Befehl», nachgebildet, da Tooltips in Edge headless nicht aufnehmbar sind); zweites Bild: die Anleitung mit hervorgehobener Funktion (falls herstellbar) |
| start-f1-dialog | beispiel | Dialog «Tastaturkürzel» und Anleitung | Dialog «Tastaturkürzel» offen; zweites Bild: Hilfefenster mit Suchfeld «tastaturkuerzel» (falls herstellbar) |
| start-claude-knopf | beispiel | Kopfleiste und Palette «Claude» | Beispielprojekt, Palette «Claude» eingeblendet (Attrappe, nie echter Claude-Lauf): Knopf «Claude» gedrückt neben dem «?», Palette rechts angedockt mit leerem Verlauf |
| start-symbole-revit | beispiel | Menüband › Gebäudetechnik | Zwei Bilder desselben Registers: Strichsymbole (Vorgabe) und Revit-Stil; Liste «Paletten» mit Häkchen vor dem Eintrag als drittes Bild |
| m3c-ein-aus | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Liste des Knopfs «Paletten» offen mit Häkchen bei Claude, Palette im Hintergrund eingeblendet (zwei Bilder) |
| m3c-frage | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Frage «Was ist ausgewählt?» gestellt, Antwort der Attrappe im Verlauf |
| m3c-kontext | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Grundriss EG offen, Strang KW-S1 gewählt: Chips «Ansicht: …», «Geschoss: EG», «Auswahl: KW-S1» an, «Bild der Ansicht» aus |
| m3c-antwort | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Antwort mit aufgeklapptem Werkzeugaufruf element_aendern (✓) |
| m3c-aenderungen | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Antwort mit Änderungsliste (Paket, Element KW-S1, Knopf «Zurücknehmen»); zweites Bild nach dem Zurücknehmen (durchgestrichen, «zurückgenommen») |
| m3c-freigabe | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Frage mit Hinweis «Auswahl für Claude freigegeben» unter der Frage, Antwort mit einer Änderung |
| m3c-gespraeche | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Auswahlliste der Gespräche offen mit zwei Gesprächen und «＋ Neues Gespräch» |
| m3c-modell | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Kopf mit geöffneter Modellwahl |
| m3c-internet | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Schalter «Internet» an, Hinweisband und ausgegraute Chips, eine Frage mit Marke «Internet · ohne Projektzugriff» |
| m3c-bild | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Grundriss EG, Chip «Bild der Ansicht» an (Haken) neben den anderen Chips |
| m3c-normen | beispiel | Palette «Claude» › Normen | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); mit der Test-Norm (tests/daten/normen, HLKS_NORMEN auf eine Kopie): Reiter «Normen» mit Treffern |
| m3c-normfrage | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Normfrage mit der Test-Norm, Antwort der Attrappe mit Zitat (Layout der Antwort) – kein echter Claude-Lauf |
| m3c-neustart | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf); Palette nach dem Neuladen mit dem vorherigen Gespräch (zwei Fragen und Antworten) |
| m3c-meldungen | beispiel | Palette «Claude» | Beispielprojekt, Palette «Claude» rechts angedockt (Server mit Attrappe statt claude.exe, nie echter Claude-Lauf) mit Einstellung «verfuegbar: false» (Attrappe nicht gefunden): rotes Band mit Grund |
| m4-apparateart | beispiel | Eigenschaftenpalette | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): WT-E.02 gewählt, Auswahlliste «Apparateart» geöffnet (Vorgabe der Klasse, Badewanne …) |
| m4-lu-du | beispiel | Eigenschaftenpalette | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): WT-E.02 gewählt, Dialog «?» zu LU KW mit Quelle und Excel-Zeile 8 |
| m4-summen-leitung | beispiel | Eigenschaftenpalette | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): KW-V1-0 gewählt, Gruppe «Abgeleitet» mit ΣLU; Dialog «?» mit Tabelle der Beiträge |
| m4-schema-belastung | beispiel | Strangschema AN-SCHEMA-SAN | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): Strangschema Sanitär, Beschriftungen «ΣLU 24 / …» und «ΣDU 16.7» an den Leitungen |
| m4-liste | beispiel | Listenansicht «Sanitär-Zusammenstellung (W3)» | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): Liste AN-LI-SANITAER-A-SAN offen, alle drei Tabellen sichtbar |
| m4-knopf | beispiel | Menüband › Gebäudetechnik | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): Register Gebäudetechnik, Knopf «Sanitär-Zusammenstellung» hervorgehoben |
| m4-spezial | beispiel | Eigenschaftenpalette der Anlage | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): Anlage A-SAN gewählt, Tabelle «Spezialinstallationen» mit einer Zeile «Garteneinrichtung 0,5 / –» |
| m4-k | beispiel | Eigenschaftenpalette der Anlage | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): Anlage A-SAN, Zeile Abflusskennzahl mit Hinweis; zweites Bild: Liste mit «K fehlt» |
| m4-csv | beispiel | Dialog «Liste als CSV» | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): Dialog «Liste als CSV» zur Sanitär-Zusammenstellung |
| m4-befunde | beispiel | Dialog «Prüfbefunde» | Beispielprojekt MFH Musterweg (Anlage A-SAN, 16 Apparate, K 0.5 fiktiv): Apparat BA-U.02 ohne Art (P21) und Anlage ohne K (P25); Dialog «Prüfbefunde» offen |
| zb-n3-einlageplan-knopf | bernstrasse | Dialog «Einlageplan anlegen» | Bernstrasse (Kopie des Abnahmeprojekts), Dialog «Einlageplan anlegen» offen: Tabelle der erkannten Pläne, Vorschau (Trockenlauf) unten |
| zb-n3-grundleitungsplan-knopf | bernstrasse | Dialog «Grundleitungsplan anlegen» | Bernstrasse (Kopie des Abnahmeprojekts «abnahme3»), Dialog «Grundleitungsplan anlegen» offen mit Tabelle und Vorschau |
| zb-n3-projektinfo | beispiel | Dialog «Projektinformationen» | Beispielprojekt, Dialog «Projektinformationen» offen mit allen drei Gruppen; Überblick eingeklappt |
| zb-n3-projektinfo-browser | beispiel | Projektbrowser | Beispielprojekt, Projektinformationen einmal übernommen (Phase gesetzt); Projektbrowser, Ast «Gebäude» aufgeklappt, Eintrag «Projektinformationen» hervorgehoben, Kontextmenü offen |
| zb-n3-listen-kollision | bernstrasse | Listenansicht «Kollisionsliste» | Bernstrasse (Kopie), Listenansicht AN-LI-KOLLISIONEN geöffnet, obere Zeilen sichtbar |
| zb-n3-csv | beispiel | Dialog «Liste als CSV» | Beispielprojekt, Strangliste angelegt und geöffnet; Dialog «Liste als CSV» offen mit Dateiname und Formathinweis |
| zb-n3-ks-register | beispiel | Dialog «Tastaturkürzel» | Dialog offen, Abschnitt «Beschriften» sichtbar |

## 7. Bildskript `werkzeuge/anleitung_bilder.mjs`

Das Skript erzeugt die Bilder zu den Funktionen des Inventars (Fenster 1600 × 950, helles Farbschema, WebP Qualität 85) und legt sie **nur lokal** unter `%USERPROFILE%\HLKS_CAD_testdaten\anleitung\bilder\` ab (zusammen mit `bilder.json`); nie ins Repository und nie nach OneDrive, weil die Bilder der Bernstrasse echte Projektdaten zeigen. Aufruf und Ports stehen im Kopfkommentar der Datei:

```
node werkzeuge/anleitung_bilder.mjs [--nur <id,id,…>] [--projekt beispiel|bernstrasse] [--liste]
```

- **`bilder.json`** (`stand`, `erzeugt`, `bilder`, `ohneBild`): je Bild die Funktions-Ids, die Datei, das Projekt, die Grösse, der rote Rahmen der Markierung als `{x,y,b,h}` (Pixel im Bild; `null`, wenn nichts zu markieren war, mit `markierungGrund`) und ein Titel, der den hergestellten Zustand nennt. Mehrere Bilder einer Funktion heissen `<id>.webp`, `<id>_2.webp` usw. («vorher/nachher», hell/dunkel, mehrere Fensterbreiten). Teilläufe führen die Datei nach.
- **Markierung «wo»:** der Rahmen (3 px, abgerundet, leicht abgesetzt) ist ein eingespritztes Element, das nach der Aufnahme wieder verschwindet. Bei Funktionen mit Register und Knopf sucht das Skript den Knopf im Menüband (und wählt dafür das Register), sonst steht die Markierung ausdrücklich im Katalog des Skripts (Palette, Feld, Dialog, Zeile …).
- **Zustände** entstehen über die Befehle des Kerns und über echte Maus- und Tastenereignisse (wie die `*.browser.mjs`-Tests); die Testwerte sind dieselben wie dort (alle fiktiv). Das Beispielprojekt «MFH Musterweg» und ein zweites fiktives Projekt («Schulhaus Ostring», für die angeheftete Kachel und die Bibliotheksbilder) liegen unter `%TEMP%`; die Bernstrasse ist eine **Kopie** der Abnahmeprojekte `abnahme3b` (Koordination) und `abnahme3` (Leitungen).
- **Bilder, die ein Skript nicht herstellen kann** (in `bilder.json` unter `ohneBild` mit Grund): Desktop-Verknüpfung und Hinweisfenster (`start-icon`, `start-aktualisieren`), Windows-Explorer (`start-ordner-explorer`), KI-Lauf über das Abo (`m2-ki-lauf`), alles mit einer echten HKAP-Gegenstelle (`m1d-abgleich`, `m1d-konflikte`, `m1d-uebernehmen`, `m1d-senden`, `m1d-befunde`, `m1d-nicht-platziert`, `m1d-raum-platzieren`, `m1d-raum-zuordnen`, `m1d-plankopf`, `m3-hkap-straenge`). Gegen HKAP verbindet sich das Skript nie: Anfragen an `hkap_…` fängt der Browser ab (Antwort 503), `m1d-verbinden` zeigt den Dialog leer. Das Zweitfenster (`anordnung-eigenes-fenster`) ist als Bild des zweiten Browserfensters allein erfasst.
- **Abweichungen vom Plan in Abschnitt 6** (der Plan blieb unverändert, die Titel in `bilder.json` nennen, was wirklich zu sehen ist): Tooltips sind in Edge headless nicht aufnehmbar, das Skript bildet sie nach (`menueband-schnellzugriff`, `menueband-gesperrt`); der Windows-Dateidialog fehlt, die Datei wird dem Dateifeld der Seite direkt übergeben; die Prüfansicht der Familie (`m2-pruefansicht` usw.) zeigt dieselbe fiktive Testfamilie, die sonst die Attrappe des Browsertests anlegt, angelegt mit dem Akteur «KI» über die Kommandozeile des Kerns; das PDF des Planblatts (`m1a-pdf`) wird mit `werkzeuge/pdf_bild.ps1` gerendert und in einer schlichten Seite gezeigt, weil sich der PDF-Betrachter des Browsers nicht aufnehmen lässt; das Menüband-Flyout (`menueband-flyout`) entsteht bei etwa 280–300 px Fensterbreite im Register Architektur, weil im Register Ansicht keine Gruppe zum Flyout wird.

### Nachtrag 20.10.2026: Bilder der 36 neuen Funktionen

Erledigt mit Commit 8cecea1 (Bildskript): Szenen für alle 36 neuen Funktionen (Hilfe/F1, Knopf «Claude», Symbole-Probe, N3,
Kapitel m3c mit der Attrappe statt echtem Claude, Kapitel m4), 48 Bilddateien; die Anleitung baut mit 262 Funktionen, davon
248 mit Bild (14 ohne Bild wie oben begründet). Portwächter des Skripts sperrt nur noch die festen Ports (5013, 5098, 5130,
5131, 5149–5152); Ports über --port/HLKS_ANLEITUNG_PORT und --cdp/HLKS_CDP_PORT. Nachgebildet (Edge headless zeichnet
sie nicht): Auswahllisten (Apparateart, Modell, Gespräche) und der F1-Tooltip – die Bildtitel sagen das. Gesprächsbilder
zeigen die Marker [attrappe:…] der Attrappe bewusst. zb-n3-grundleitungsplan-knopf stammt aus der Kopie von bnahme3
(fiktives MFH Musterweg), steht in ilder.json aber unter ernstrasse_leitungen.

### Nachtrag 20.10.2026 (Stand 636e78c): Bilder des Kapitels «m7» und nachgeführte Bilder

Szenen im Bildskript für alle 11 Funktionen von «m7» (10 Szenen, 22 Bilder; Aufruf `node werkzeuge/anleitung_bilder.mjs --nur m7 --port 5210 --cdp 9560`). Wie sie entstehen: `m7Setup` legt über die Befehle des Kerns die Projektinformation, die Planblätter PB-1 «1-0768 H02 Heizung Erdgeschoss» (AN-GR-EG) bzw. PB-2 «H03 Heizung 1. Obergeschoss» und die Revisionen A und B an (`revision_hinzufuegen` mit festem Datum); die Dialoge und Paletten werden wie in `planblatt-hk.browser.mjs` bedient (echte Mausklicks, Auswahlfelder gesetzt). Nachgeführt wurden wegen des neuen Plankopfs `m1a-planblatt`, `m1a-ansicht-platzieren`, `m1a-pdf` und `m3-schema-planblatt` (Bilder neu erzeugt; `m1d-plankopf` bleibt ohne Bild: HKAP). Die Anleitung baut mit 273 Funktionen. Beim Ansehen der Bilder fiel auf: der Revisionsstempel steht auf der Mitte des Kopfs und überdeckt dort «Erstelldatum:» und «Rev. Index:» (Lage laut Quelltext «Mitte des Kopfs»; Frage an Christian im Testbericht 7A); die Eigenschaftenpalette ist schmal, die Beschreibung der Revisionstabelle wird abgeschnitten dargestellt.
