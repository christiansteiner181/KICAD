# Oberfläche HLKS-CAD (Modul 0b) – Pflichtenheft für alle, die daran bauen

Lies zuerst `HLKS_CAD/LIESMICH.md` (Leitprinzipien). Für die Oberfläche gilt zusätzlich:

1. **Bedienung wie Revit.** Wer Revit kennt, findet sich sofort zurecht: Menüband mit Registern und Kontextregister,
   links Eigenschaftenpalette (oben) und Projektbrowser (unten; Paletten lassen sich andocken, lösen und als Reiter legen, Abschnitt «Fenster: Dialoge und Paletten»), Optionsleiste unter dem Menüband, Statusleiste,
   Revit-Tastaturkürzel (zwei Buchstaben). Eigene Symbole (Inline-SVG), **keine Grafiken oder Namen von Autodesk**.
2. **Keine Fachlogik in der Oberfläche.** Ändern nur über `befehl()`, lesen über `abfrage()` bzw. den Zustand.
   Nie den Zustand direkt verändern; nach einem Befehl kommt die Änderung als Ereignis zurück und alle Ansichten
   aktualisieren sich (auch bei Änderungen eines anderen Arbeitsplatzes oder der KI).
3. **2D ist 3D.** Grundriss und 3D zeigen dieselben Elemente; eine Eigenschaft wird an einer Stelle geändert und ist
   überall neu. Nur Beschriftungen gehören zu einer Ansicht (ab Modul 1a).
4. Deutsch, Schweizer Schreibweise (ss statt ß). Farben und Grössen nur über die CSS-Variablen in `css/hlks.css` (Design und Masse: Abschnitt «Design und Masse»).
   Keine externen Bibliotheken ausser `lib/three` (lokal). Kein CDN. ES-Module ohne Build-Schritt.
5. Jede Datei hat einen Kopfkommentar, was sie tut. Kommentare deutsch, knapp.

## Dateien und Zuständigkeit

| Datei | Inhalt | Zuständig |
| --- | --- | --- |
| `index.html`, `css/hlks.css`, `js/app.js`, `js/api.js`, `js/zustand.js`, `js/ereignisse.js`, `js/fenster.js`, `js/ansichten/filter.js` | Gerüst und Verträge | Hauptlauf |
| `js/oberflaeche/paletten.js`, `paletten-daten.js`, `paletten-ziele.js`, `aktionen-paletten.js`, `dialoge-fenster.js`, `dialog-merken.js`, `fenstergeometrie.js`, `fensterbewegung.js`, `css/fensterrahmen.css`, Tests `paletten.test.mjs`, `dialog-fenster.test.mjs`, `dialoge-paletten.browser.mjs` | Fenster wie in Revit: Dialoge verschiebbar, Paletten andocken, lösen, Reiter (W1; Abschnitt «Fenster: Dialoge und Paletten») | Agent «W1» |
| `js/oberflaeche/aktionen.js`, `menueband.js`, `menueband-stufen.js`, `ansichtsreiter.js`, `projektbrowser.js`, `eigenschaften.js`, `statusleiste.js`, `kuerzel.js`, `dialoge.js`, `css/oberflaeche.css` | Bedienung | Agent «Oberfläche» |
| `js/ansichten/grundriss.js` (Plan-Ansicht), `plangrafik.js`, `ansicht3d.js`, `koerper3d.js`, `planblatt.js`, `liste.js`, `css/ansichten.css`, `js/oberflaeche/aktionen-plaene.js`; seit 1c (C1b) `huelle-darstellung.js` (Farben, Legende, Infotext; rein), `huelle3d.js` (3D-Flächen der Raumgrenzen), `huelle-darstellung.test.mjs`, `huelle-darstellung.browser.mjs` | Ansichten | Agent «Ansichten» |
| `js/oberflaeche/aktionen-einfuegen.js`, `einfuegen-text.js` (reine Texte und Parameter, in Node getestet), `js/werkzeuge/kalibrieren.js`, `kalibrierung.js` (reine Rechnung), Endpunkte `/api/unterlagen` in `Program.cs` | Register «Einfügen», Unterlagen, Koordinaten (Modul 1b, C1) | Agent «Einfügen» |
| `js/oberflaeche/aktionen-hkap.js`, `hkap-text.js` (reine Texte, HTML und Parameter, in Node getestet), `hkap.test.mjs`, `hkap.browser.mjs` | Register Verwalten › HKAP, nicht platzierte Räume (Modul 1d, C) | Agent «HKAP» |
| `js/oberflaeche/aktionen-huelle.js`, `huelle-text.js` (reine Texte, HTML, Prüfungen und Parameter, in Node getestet), `huelle.test.mjs`, `huelle.browser.mjs` | Register Architektur › Decke, Boden, Dach und Register Hülle, Projektbrowser-Knoten «Hülle» (Modul 1c, C1a) | Agent «Hülle» |
| `js/familien/*` (Editor, Texte, Ansicht, Palette, Platzieren, Aktionen, Tests), `css/familien.css` | Familieneditor, Projektbrowser «Familien», Platzieren, Eigenschaften platzierter Elemente (Modul 2, C1) | Agent «Familien» |
| `js/familien/unterlagen-*.js`, `pruefansicht*.js`, `befunde-browser.js`, `quelle-ansehen.js`, `auftrag.js`, `aktionen-unterlagen.js`, `css/familien-unterlagen.css` | Familie aus Unterlagen (Modul 2, C2; Abschnitt am Ende) | Agent «Familien» |
| `js/familien/bibliothek-*.js`, `aktionen-bibliothek.js`, `browser-knoten.js`, `css/familien-bibliothek.css`, Tests `bibliothek.test.mjs`, `bibliothek.browser.mjs`; Ergänzungen in `projektbrowser.js`, `statusleiste.js`, `menueband.js`, `symbole.js`, `aktionen-familien.js` | Familienbibliothek: Explorer, Aktualisieren, Familie laden, Statusleiste (Modul 2, C3; Abschnitt «Familienbibliothek») | Agent «Familien» (hlks-modul) |
| `js/ansichten/schema*.js`, `js/oberflaeche/schema-bearbeiten.js`, `aktionen-schema.js`, `css/schema.css`, Tests `schema.test.mjs`, `schema.browser.mjs` | Schema-Ansicht (Modul 3, C2; Abschnitt am Ende) | Agent «Schema» (hlks-modul) |
| `js/werkzeuge/leitung*.js`, `js/oberflaeche/aktionen-leitung.js`, Tests `leitung.test.mjs`, `leitung.browser.mjs`; Ergänzungen in `optionen.js`, `optionsleiste.js`, `fangen.js`, `griffe.js`, `eigenschaften.js` | Rohr und Kanal zeichnen (Modul 3, C1; Abschnitt am Ende) | Agent «Leitungen» (hlks-modul) |
| `js/oberflaeche/startbildschirm.js`, `start-text.js`, `ordner-browser.js`, `vorschau.js`, `vorschau-logik.js`, `css/start.css`, Tests `start.test.mjs`, `start.browser.mjs`; Server `ZuletztListe.cs`, `StartDienst.cs`, `StartEndpunkte.cs`; Ergänzungen in `dialoge.js`, `app.js`, `aktionen-projekt.js`, `menueband.js`, `symbole.js` | Startbildschirm wie Revit Home, Ordner-Browser, 3D-Vorschaubild (Teil S1; Abschnitt am Ende) | Agent «Start» (hlks-modul) |
| `js/oberflaeche/koordination-*.js`, `aktionen-koordination.js`, `koordination.test.mjs`, `koordination.browser.mjs`; CSS-Block «Koordination» am Ende von `css/oberflaeche.css`; Ergänzungen in `menueband.js`, `aktionen.js`, `symbole.js` | Register Koordination: Projektvorgaben, Kollisionen, Aussparungen, BCF (Modul 3b, C1; Abschnitt am Ende) | Agent «Koordination» (hlks-modul) |
| `js/oberflaeche/ki-palette.js`, `ki-text.js`, `ki-zustand.js`, `ki-darstellung.js`, `ki-bild.js`, `aktionen-ki.js`, `css/ki-palette.css`, Tests `ki-palette.test.mjs`, `ki-palette.browser.mjs`; Ergänzungen in `ereignisse.js`, `zustand.js`, `aktionen.js`, `index.html`, `symbole.js`, `symbole-revit.js` | Palette «Claude»: Gespräch, Änderungen zurücknehmen, Normensuche (Modul 3c, Teil O; Abschnitt am Ende) | Agent «KI-Palette» (hlks-modul) |
| `src/Hlks.Web/*.cs` | Server | Hauptlauf / Agent «Abgleich» (ProjektDienst, AbgleichDienst) |

Fremde Dateien nicht ändern. Fehlt etwas im Gerüst, im Schlussbericht melden.

## Verträge (Gerüst)

**`js/api.js`**: `abfrage(name, parameter)` → Objekt oder Text · `befehl(name, parameter, { trockenlauf })` →
BefehlsErgebnis `{ angenommen, befehl, trockenlauf, paketId, meldung, fehler, vorschlag, aenderungen[], neueBefunde[], behobeneBefunde[], hinweise[], konflikte[], gesperrt[] }`
(abgelehnt ist kein Ausnahmefall) · `holen(pfad)`, `senden(pfad, koerper)` · Fehler als `ApiFehler(meldung, vorschlag)`.
Abfragen und Befehle: `abfrage('faehigkeiten')` listet alle mit Parametern. Wichtig: `element_anlegen`, `element_aendern`
(`{ element, name?, kennzeichen?, geschoss?, raum?, system?, teilVon?, eigenschaften: { name: wert | null }, geometrie?, bezuege? }`),
`element_loeschen`, `verbinden`, `trennen`, `leitung_ziehen`, `rueckgaengig`, `wiederholen`, `synchronisieren`;
Abfragen `element_lesen`, `warum {element, eigenschaft}`, `raumliste {geschoss?}`, `strangliste {system?}`, `mengen`,
`pruefen`, `katalog {klasse?}`, `strang_text`, `system_lesen`, `aenderungen`.
Unterlagen (Modul 1b): `POST /api/unterlagen?name=<Dateiname>` (Datei roh im Body oder multipart mit einem Formularfeld; bis 500 MB; legt sie über `Unterlagen.Ablegen` im Ordner `unterlagen` des offenen Projekts ab; Antwort `{ name, stand, bytes }`, `name` = Prüfsumme + «_» + Dateiname; fachliche Fehler als 400 mit `{ fehler, vorschlag }`, ohne Projekt 409), `GET /api/unterlagen/{id}/netz` (Id oder Kennzeichen einer Unterlage; die Datei der Eigenschaft «netz» roh als `application/octet-stream`, mit gzip, wenn der Browser es annimmt und auf einem anderen Rechner läuft (lokal roh, gzip kostet dort mehr als es spart) – die Antwortkomprimierung gilt nur für `application/octet-stream`; 404 mit Text, wenn es die Unterlage, das Netz oder die Datei nicht gibt), `GET /api/unterlagen/{id}/bild` (PDF- und Bild-Unterlagen: die PNG-Seite bzw. das Bild über `UnterlagenBild.Pfad`, `image/png` oder `image/jpeg`, `Cache-Control: no-cache` mit Änderungsdatum → 304; 404 mit Text, wenn es die Unterlage oder das Bild nicht gibt oder die Art kein Bild hat).
Weitere Endpunkte: `GET /api/status`, `POST /api/projekt/oeffnen {ordner}`, `POST /api/projekt/anlegen {ordner, name, beispiel}`,
`POST /api/projekt/schliessen`, `GET /api/zuletzt` (erweitert, siehe «Startbildschirm»), `POST /api/freigeben`.
Status zusätzlich: `ablage` (`verbunden|getrennt|null`), `abgleich {…}` (Wächter, Takt, letzte Dauer, unlesbare Pakete),
`beschaeftigt` (true, solange der Abgleich die Ablage liest; dann gilt der zuletzt bekannte Status).

**Ereignisstrom `/api/ereignisse`** (SSE, `js/ereignisse.js`): `aenderung {ids, quelle, akteur, akteurArt, paket, stand}`
– viele Pakete auf einmal kommen als ein Sammelereignis mit zusätzlich `pakete` (Anzahl) · `projekt {projekt, ort}`
nach Öffnen/Anlegen/Schliessen, `projekt {neuLaden: true, grund}` wenn der Puffer übergelaufen ist (dann alles neu
laden) · `abgleich {fehler, ablage, seit?, wiederVerbunden?, unlesbar?}` – nur beim Wechsel getrennt/verbunden, nicht
in jedem Takt · `konflikt {konflikte}` · `auftrag {auftrag: {id, status, schritte, …}}` (KI-Läufe, Modul 2; `js/familien/auftrag.js`). Nach einer Unterbrechung des Stroms lädt die Oberfläche Status und Modell neu.

**`js/zustand.js`**: `zustand` (status, katalog, elemente Map id→Darstellung, auswahl, aktiveAnsicht, stand,
verbindungen) · `abonnieren(art, fn)` → Abmelden · `element(idOderKennzeichen)`, `alleElemente()`, `derKlasse(k)`,
`klasse(name)` (Katalogdefinition aus `zustand.katalog.klassen`) · `auswaehlen(id|null)`, `ansichtAktivieren(kz)` ·
`projektLeeren()` (Schliessen/Wechsel: Modell, Katalog, Auswahl leeren; `zustand.geladen` = Ablageort des geladenen Projekts) ·
`merken.lesen/schreiben` (localStorage, sicher). Ereignisarten: `modell` ({alles} oder {ids, geloescht, quelle,
akteur, akteurArt, paket}; quelle `lokal|fremd|rueckgaengig|wiederholen`), `auswahl`, `ansicht`, `status`,
`abgleich` ({fehler}), `konflikt`, `verbindung` ({ok}).

**`js/fenster.js`**: `ansichtOeffnen(kz)` (auch Planblätter), `aktivePlanansicht()` (Instanz der aktiven Plan-Ansicht mit Werkzeug-Schnittstelle oder null), `schliessen(id)`, `aktivieren(id)`, `nebeneinander()` (WT), `alsReiter()`,
`fenstermodus()`, `aktive()`, `alle()`, `zoomAlles()` (aktive), `zoomAlleAnsichten()`; seit W2 frei anordenbar in Gruppen und im eigenen Fenster (Abschnitt «Ansichten frei anordnen» am Ende). Jede offene Ansicht hat unten
eine Steuerleiste (Massstab, Detailgrad) – die ändert das Ansicht-Element per Befehl.

**Ansichtsmodul** (`js/ansichten/*.js`), Standardexport `{ erstellen(behaelter, ansicht) → instanz }`;
instanz: `aktualisieren(ereignis)`, `ansichtGeaendert(ansicht)`, `auswahlGeaendert(id)`, `zoomAlles()`, `zerstoeren()`.
Zuordnung Art → Modul in `fenster.js`: `grundriss`, `schnitt`, `ansicht` → `grundriss.js` (Plan-Ansicht), `3d` → `ansicht3d.js`, `liste` → `liste.js`, `schema` → `schema.js` (seit Modul 3, C2),
Planblatt (Klasse Planblatt) → `planblatt.js`. Die Grafik kommt vom Kern, nicht aus dem Browser: Plan-Ansicht `GET /api/plangrafik/{kz}`,
3D `GET /api/koerper[?ids=]`, Planblatt `GET /api/plan/{kz}.pdf`, Schraffuren `GET /api/schraffuren`. Die Ansicht meldet Klicks
mit `auswaehlen(id)` (Id des Teils bzw. Körpers). Bei `aktualisieren` nur betroffene Elemente neu zeichnen, wenn `ereignis.ids` da ist; bei
`ereignis.alles` alles. Fremde Änderungen (`ereignis.quelle === 'fremd'`) kurz hervorheben (CSS-Klasse/Farbe `--fremd`).
Auf `window`-Ereignis `resize` die Zeichenfläche anpassen.

## Darstellungsdaten (vom Kern, `/api/modell` bzw. `zustand.elemente`)

```json
{"id":"…","kennzeichen":"WC-E.02","klasse":"WC","name":null,"schicht":"system","rolle":"verbraucher","gewerk":"sanitaer",
 "basis":"Apparat","medium":null,"farbe":null,"linie":null,"geschoss":"EG","raum":"E.02","system":null,"teilVon":null,
 "version":1,"geometrie":{"art":"punkt","punkte":[[7500,9500,0]]}}
{"kennzeichen":"SW-E.02-WC","klasse":"Rohr","schicht":"system","rolle":"leitung","gewerk":"sanitaer","basis":"Leitung",
 "medium":"SW","farbe":"#8b5a2b","linie":"voll","geschoss":"EG","system":"SW","teilVon":"SW-F1",
 "geometrie":{"art":"polylinie","punkte":[[7500,9500,200],[8800,9700,170]]},"werte":{"dn":100,"gefaelle":2,"material":"PE"}}
{"kennzeichen":"E.02","klasse":"Raum","name":"Bad","schicht":"raum","gewerk":"architektur","geschoss":"EG",
 "geometrie":{"art":"polygon","punkte":[[7000,6000,0],[10000,6000,0],[10000,10000,0],[7000,10000,0]]},
 "werte":{"lichteHoehe":2500,"nummer":"E.02","nutzung":"Bad"}}
{"kennzeichen":"W-EG-N","klasse":"Wand","schicht":"huelle","gewerk":"architektur","basis":"Bauteil","geschoss":"EG",
 "geometrie":{"art":"linie","punkte":[[12000,10000,0],[0,10000,0]],"hoehe":2900,"dicke":350},"werte":{"angrenzung":"aussen","aufbau":"AW-01"}}
{"kennzeichen":"TS-F1-EB","klasse":"TStueck","rolle":"leitung","basis":"Formteil","medium":"SW","farbe":"#8b5a2b","geschoss":"EG",
 "system":"SW","teilVon":"SW-F1","geometrie":{"art":"punkt","punkte":[[9000,9800,300]]},"werte":{"dn":100}}
{"kennzeichen":"AN-GR-EG","klasse":"Ansicht","name":"Grundriss EG Sanitär","schicht":"ansicht","geschoss":"EG",
 "werte":{"art":"grundriss","detailgrad":"mittel","gewerke":["sanitaer","architektur"],"massstab":50,"schnitthoehe":1000}}
{"kennzeichen":"DP-MV1","klasse":"Datenpunkt","schicht":"funktion","gewerk":"msrl","werte":{"signal":"DO","art":"schaltung",…},
 "bezuege":{"an":"MV-1","station":"AS-1"}}
```
Koordinaten in mm: x Ost, y Nord, z Höhe über ±0.00 (absolut; Geschoss EG Kote 0, 1.OG 2900, UG −2700).
Leitungen fliessen vom ersten zum letzten Punkt. `linie`: voll | gestrichelt. Ansicht-Werte: `art`
(grundriss|schnitt|ansicht|3d|schema|liste), `massstab` (Nenner), `schnitthoehe`, `detailgrad` (grob|mittel|fein),
`gewerke` (Liste), `klassenAusblenden` (Liste), `vorlage` (bool), `ansichtsvorlage` (Kennzeichen), `liste`
(raumliste|strangliste|mengen bei art liste; seit 1c auch raumgrenzen|huellbauteile, siehe «Liste» und «Gebäudehülle»). Katalog: `abfrage('katalog')` → `{ klassen: [{name, schicht, basis,
gewerk, ifc, beschreibung, anschluesse[], eigenschaften[] (Text «name [einheit] *»)}], medien, systemtypen }`;
die volle Klassendefinition (Eigenschaften mit typ/einheit/auswahl/pflicht/beschreibung) liefert `abfrage('katalog', {klasse})`.

## Menüband (Register) und Kürzel – Pflichtumfang Modul 0b

Aktionen sind zentral in `aktionen.js` registriert (`id`, `titel`, `symbol` (SVG), `kuerzel`, `ausfuehren()`,
`aktiv()` → bool, `hinweis` für noch nicht verfügbare). Menüband, Kontextmenüs und Tastaturkürzel lösen nur Aktionen aus.

| Register | Gruppen und Aktionen (Kürzel) |
| --- | --- |
| Datei (blau, wie Revit) | Projekt öffnen, Projekt anlegen (leer / Beispielprojekt), Projekt schliessen, zuletzt geöffnet |
| Architektur | Wand (WA), Tür (DR), Fenster (WN), Raum (RM), Achse (GR) – Werkzeuge seit Modul 1a (`aktionen-zeichnen.js`, `js/werkzeuge/`) |
| Architektur, Gruppe «Decke, Boden, Dach» (seit 1c) | Decke, Boden, Dach → Dialog `decke_erzeugen`; keine Kürzel. Beschreibung: Abschnitt «Gebäudehülle» |
| Hülle (seit 1c, zwischen Architektur und Gebäudetechnik) | Gruppen **Aufbauten** (Aufbau …, Zuweisen …), **Materialien** (Material …), **Fenster** (Kennwerte …), **Übergang** (Übergang …), **Prüfen** (Hülle prüfen, Wärmebrücken vorschlagen), **Listen** (Raumgrenzen, Hüllbauteile). Keine Kürzel. Beschreibung: Abschnitt «Gebäudehülle» |
| Gebäudetechnik | Gruppe «Leitungen»: **Rohr (PI)** und **Kanal (DT)** (Werkzeuge seit Modul 3, C1 – Abschnitt «Leitungen zeichnen»), Formteil (PF) – deaktiviert; Gruppe «Apparate und Armaturen»: Familie platzieren (CM), Familie aus Unterlagen, Familien im Explorer … (Modul 2, C3), Sanitärapparat (PX), Armatur (PA) – deaktiviert, «folgt in Modul 2/3» |
| Einfügen (seit 1b) | Gruppe «Verknüpfen / Übernehmen»: IFC übernehmen · «Importieren»: CAD einfügen (DWG/DXF), PDF einfügen, Bild einfügen · «Unterlagen»: Kalibrieren (Werkzeug), Unterlagen verwalten. Reihenfolge wie in Revit: dort steht «Einfügen» nach «Systeme» (hier «Gebäudetechnik») und vor «Beschriften». Keine Kürzel (Revit hat für diese Befehle keine). Beschreibung: Abschnitt «Register Einfügen» |
| Beschriften (seit 1a, wie Revit «Beschriften») | Bemassung (DI), Text (TX), Detaillinie (DL), Höhenkote (EL), Füllbereich, Nordpfeil |
| Koordination (seit Modul 3b, C1, zwischen Ändern und Ansicht) | Gruppen **Vorgaben** (Projektvorgaben), **Kollisionen** (Kollisionen prüfen, Kollisionsliste), **Aussparungen** (Aussparungen vorschlagen, Aussparungen nummerieren, Aussparungsliste, Abgleich, Aussparungsplan anlegen, IFC für Architekt), **BCF** (BCF exportieren, BCF importieren). Keine Kürzel. Beschreibung: Abschnitt «Koordination (Modul 3b, C1)» |
| Ändern | Ändern/Auswählen (MD), Löschen (DE, Entf) mit Trockenlauf-Vorschau der neuen Befunde und Bestätigung, Rückgängig (Strg+Z), Wiederholen (Strg+Y), Eigenschaften ein/aus (PP); seit 1a Verschieben (MV), Kopieren (CO), Drehen (RO), Spiegeln (MM), Mehrfachauswahl mit Strg+Klick |
| Ansicht | Grundriss anlegen, 3D-Ansicht anlegen (3D), Liste anlegen, Schnitt (Werkzeug), Ansicht duplizieren, Sichtbarkeit/Grafiken (VV, VG), Ansichtsvorlage speichern / anwenden, Fenster nebeneinander (WT), Fenster als Reiter (WC), Zoom alles (ZF, ZE), Zoom alle Ansichten (ZA), Design hell/dunkel, **Hülle** (seit 1c, Gruppe «Darstellung»: Umschalter für die Ansichtseigenschaft darstellung huelle/standard, nur Grundriss und 3D); **Schema** (seit Modul 3, C2: Knopf «Schema» in der Gruppe «Erstellen», Gruppe «Schema» mit Verbinden, Trennen, Element einfügen, Neu anordnen – Abschnitt «Schema (Modul 3, C2)»); Planblätter (`aktionen-plaene.js`): Planblatt, Ansicht platzieren (auch Schemas), PDF öffnen |
| Verwalten | Projektinformationen, **Koordinaten** (seit 1b, nur lesend), Synchronisieren, meine Elemente freigeben, Prüfbefunde, Katalog anzeigen, Tastaturkürzel (KS); **Gruppe «HKAP»** (seit 1d, zwischen «Modell» und «Hilfe»): Verbinden, Abgleich, Prüfbefunde senden – Beschreibung: Abschnitt «Verwalten › HKAP» |
| Kontextregister «Ändern \| <Klasse>» | erscheint bei Auswahl: Löschen, Eigenschaften, im Netz folgen (stromab/stromauf hervorheben, Abfrage netz_folgen), Warum (Herkunft des gewählten Werts) |

Kürzel wie in Revit: zwei Buchstaben nacheinander tippen, wenn kein Eingabefeld den Fokus hat; Esc bricht ab und hebt
die Auswahl auf; Strg+Z/Strg+Y. Kürzel-Liste im Dialog KS anzeigen (änderbar später).

**Werkzeug Kalibrieren (Modul 1b, C1)** steht im Abschnitt «Register Einfügen».

**Werkzeuge (Modul 1a):** Ein aktives Werkzeug zeigt in der Optionsleiste seine Eingaben (Modus `werkzeug`,
`js/werkzeuge/optionsleiste.js`): Wand Dicke/Höhe/Bezugslinie/Kette/Material, Öffnung Breite/Höhe/Brüstung/Seite/Band,
Bemassung Richtung/Kette, Schnitt Tiefe, Raster. Fangen (`js/werkzeuge/fangen.js`): Endpunkt (Quadrat) > Schnittpunkt
(X) > Mitte (Dreieck) > Lot (⊥) > Nächster (Sanduhr) > Raster, Fangradius 10 px; Shift = orthogonal; Ziffern = Länge in
mm (Drehen: Grad); Enter schliesst ab, Esc/Rechtsklick beendet. Griffe an Wand- und Linienenden sind ein passives
Werkzeug (`passiv: true`, kein Fadenkreuz). Die Werkzeuge senden nur Befehle über `befehle.js`.

## Projektbrowser – Pflichtumfang

Baum mit Suchfeld oben: **Ansichten** (gruppiert: Grundrisse, 3D-Ansichten, Schnitte, Ansichten, Schemas, Listen;
Doppelklick öffnet), **Ansichtsvorlagen** (Ansichten mit `vorlage: true`), **Pläne** (Planblätter), **Gebäude**
(Gebäude → Geschoss → Räume; Klick wählt aus; dazu der Knoten **«Nicht platzierte Räume (n)»** für HKAP-Räume ohne Geometrie, Beschreibung im Abschnitt «Verwalten › HKAP»), **Systeme** (Anlage → System → Strang; Klick wählt aus),
**Hülle** (seit 1c: Äste «Materialien (n)», «Aufbauten (n)», «Wärmebrücken (n)»; Doppelklick auf Material bzw. Aufbau öffnet den Dialog, Kontextmenü beim Aufbau: Bearbeiten …, Zuweisen …; Beschreibung im Abschnitt «Gebäudehülle»), **Unterlagen** (seit 1b: IFC, DWG/DXF, PDF, Bild mit Art und Geschoss, «ausgeblendet» bei `anzeigen = false`; Kontextmenü im Abschnitt «Register Einfügen»), **Familien** (seit Modul 2: die Familien des Projekts je Kategorie mit Typen, dazu der Knoten «Katalog» = Katalog nach Schicht → Klasse, nur lesen; siehe Abschnitt «Familien (Modul 2, C1)»).
Kontextmenü auf Ansichten: Öffnen, Umbenennen, Duplizieren, Löschen, Als Vorlage speichern, Vorlage anwenden.
Live: neue/gelöschte/umbenannte Elemente erscheinen sofort.

## Register «Einfügen», Unterlagen und Koordinaten (Modul 1b, C1)

Alle Dialoge sind Hüllen um Befehle und Abfragen des Kerns; HTML und Texte stehen in `einfuegen-text.js` (reine Funktionen, `einfuegen.test.mjs`),
die Dialoge und Aktionen in `aktionen-einfuegen.js`. Fehlt ein Befehl im Kern, zeigt die Oberfläche dessen Fehlermeldung.

**Hochladen:** Datei wählen (`<input type=file>`, hängt kurz mit der Id `einfuegen-datei` im Dokument – so setzt der Browser-Test die Datei), dann `POST /api/unterlagen`
per XMLHttpRequest mit Fortschritt («12 von 98 MB»). Die Datei liegt danach im Ordner `unterlagen`; alle weiteren Befehle bekommen den Namen der Ablage (`name` der Antwort).
Die Oberfläche zeigt Dateinamen ohne die vorangestellte Prüfsumme (`ohnePruefsumme`).

**IFC übernehmen** (Aktion `einfuegen.ifc`): Hochladen → Abfrage `ifc_pruefen` → Dialog (Esc schliesst; ein laufender Befehl wird trotzdem zu Ende gemeldet):
- *Übersicht:* Datei, Fassung, Programm, Einheit, Räume; Geschosse mit Häkchen (Kote, Objekte); Bereiche mit Häkchen (Architektur, Räume, HLKS); Tabelle «Was die Datei enthält» (`zuordnung`);
  Tabelle «Nicht verstanden» (`ohneKlasse`, bleibt als Hintergrund). Bei doppelten Geschossnamen (Archicad: zweimal «OG») sind die Häkchen gesperrt, weil der Kern Geschosse über den Namen wählt.
- *Lagebericht* (Tabelle aus `lage`): Gelände, Gebäude, ±0.00 in der Datei, ±0.00 über Meer, geografische Lage, Landeskoordinaten, Koordinationskörper, eigener Bezug, Projektbezug mit Quelle.
  Bei `bewertung = abweichung` steht oben ein **grosser roter Kasten** mit dem Text und den Zahlen des Kerns («Ost +1.250 · Nord …»).
  *Inhaltsvergleich (C3, Kern: `lage.inhalt`, `inhaltJeWahl`, `empfehlung`):* Gibt es `lage.inhalt`, steht in der Tabelle eine Zeile «Inhalt» («97 % der Bauteile im bestehenden Modell»; unter 50 % dazu «Mitte +29.7 m Ost, +21.1 m Nord daneben»), auch bei «passt» und «gewählt».
  Beginnt `lage.text` mit «ACHTUNG Inhalt» (Ursprung passt, Inhalt liegt daneben), trägt der Kasten den Titel «Achtung: Inhalt liegt neben dem bestehenden Modell» und zeigt als Text den ersten Satz des Kerntextes (nicht den Ursprungsvergleich `abweichung.text`; dessen Zahlen stehen darunter);
  sonst bleibt der Titel «Lage weicht vom Projektbezug ab». Unter den Zahlen steht je Wahl «Projektbezug: 97 % der Bauteile im bestehenden Modell» und «Gebäudeursprung: …» (Wahl nicht berechenbar = Zeile entfällt);
  trifft keine Wahl (Kerntext enthält «Keine der beiden Lagen …», sonst beide < 10 %), steht der Hinweis auf Koordinationskörper oder ausdrücklichen Bezug (aus dem Kerntext übernommen). Fehlen die Felder (erste Datei, ältere Unterlagen), bleibt alles wie vorher.
- *Wahl der Ausrichtung:* drei Optionen (Projektbezug, Gebäudeursprung, Koordinationskörper) mit je einem erklärenden Satz; nur bei Abweichung (oder früherer Wahl) wählbar, sonst ausgegraut mit Begründung
  (erste Datei / passt); Koordinationskörper nur, wenn Datei und Projekt einen haben. «Übernehmen» ohne Wahl bei Abweichung lehnt die Oberfläche selbst ab; der Kern lehnt es auch ab.
  Je Option steht leise der Inhaltsanteil (`inhaltJeWahl`; Koordinationskörper: keiner), die vom Kern empfohlene (`lage.empfehlung`) trägt den Vermerk «empfohlen». **Es wird nie vorgewählt**: eine Wahl ist immer ausdrücklich (`vorgabeWahl` gilt nur für eine frühere Wahl).
- *Weitere Optionen:* Hintergrund (`grundlage`), Konflikte (`konflikte` kicad | ifc), Name des Koordinationskörpers.
- *Vorschau* = `ifc_uebernehmen` als Trockenlauf (auch ohne Wahl: der Kern rechnet dann mit dem Projektbezug und sagt es in den Hinweisen): Tabelle je Klasse mit neu / geändert / gelöscht
  (Verbindungen: neu = verbunden, gelöscht = getrennt), Summe, Hinweise des Kerns. *Übernehmen* = echter Lauf, danach ein Ergebnisdialog mit denselben Tabellen und allen Hinweisen.
  Lange Läufe zeigen ein Wartesymbol; die Oberfläche friert nicht ein.
- Parameter: `bereiche`/`geschosse` nur bei Teilauswahl, `ausrichtung` nur bei Wahl, `grundlage`/`konflikte`/`koordinationskoerper` nur bei Abweichung von der Vorgabe.

**CAD, PDF, Bild einfügen** (`einfuegen.cad`, `.pdf`, `.bild`): Datei wählen → Dialog mit Geschoss (Vorgabe: Geschoss der aktiven Ansicht), bei PDF Seite, Massstab (1 : n, leer = Vorgabe des Kerns) und dpi,
bei Bild Massstab → Hochladen → `unterlage_einfuegen { datei, geschoss, art, seite?, massstab?, dpi? }`. Fehler des Kerns stehen im Dialog (mit Vorschlag); nach Erfolg wird die Unterlage gewählt.

**Kalibrieren** (Werkzeug `kalibrieren`, `js/werkzeuge/kalibrieren.js`, nur im Grundriss): Optionsleiste «Unterlage» (vorbelegt mit der gewählten, sonst der einzigen des Geschosses; eine Unterlage lässt sich auch anklicken,
solange keine gewählt ist) und «Nur verschieben» (bei jedem Start aus). Ablauf: 2 Punkte auf der Unterlage anklicken (Fangen auf Endpunkt, Schnittpunkt, Mitte, Lot, Nächster – ohne Raster),
dann 2 Zielpunkte anklicken (Fangen mit Raster) **oder Enter** → Dialog «Zielpunkt eingeben» (x, y in mm); mit «Nur verschieben» je ein Punkt.
Die Klickpunkte rechnet die Oberfläche aus Modell-mm in Unterlagen-Koordinaten zurück (`kalibrierung.js`): Modell = Drehung(faktor · S(u)) + [dx, dy] mit der `transformation` der Unterlage;
S spiegelt y bei PDF und Bild (Pixel, y nach unten). Befehl `unterlage_kalibrieren { unterlage, von: [[u,v]…], nach: [[x,y]…], nurVerschieben? }`; Faktor, Drehung und Verschiebung rechnet der Kern
und meldet sie. Danach endet das Werkzeug.

**Projektbrowser «Unterlagen»:** Kontextmenü: Auswählen, Ausblenden/Anzeigen (`element_aendern { anzeigen }`), Deckkraft … (Dialog 0–100 %, `deckkraft`), Lagebericht zeigen
(IFC: Tabelle aus `lage`; andere Arten: Transformation), Kalibrieren (nicht bei IFC), Entfernen (der übliche Löschdialog mit Vorschau der Folgen).
«Unterlagen verwalten» (Register Einfügen) zeigt dieselben Möglichkeiten als Liste.

**Koordinaten** (Register Verwalten, `verwalten.koordinaten`, nur lesend): ein Satz Erklärung, der Projektbezug (`ifcBezug` der Projektinformation) mit Quelle, Landeskoordinaten und Koordinationskörper,
darunter alle IFC-Unterlagen mit Fassung, Ausrichtung und Abweichung (Klick auf eine Zeile = Lagebericht).

**Prüfen:** `node --test js/ansichten/ansichten.test.mjs js/oberflaeche/einfuegen.test.mjs js/werkzeuge/werkzeuge.test.mjs`; Browser (Edge headless über CDP, frischer Server mit Beispielprojekt):
`HLKS_TEST_SERVER=http://localhost:5137 SHOTS=<Ordner> node js/oberflaeche/einfuegen.browser.mjs` (mit `ECHT=1` zusätzlich die echte DWG und das echte Architekten-IFC aus `%USERPROFILE%\HLKS_CAD_testdaten`).

## Verwalten › HKAP (Modul 1d, Auftrag C)

Austausch mit HKAP (Räume, Geschosse, Stränge, Projektdaten, Pendenzen). Die Dialoge sind Hüllen um die Abfragen `hkap_verbindung`, `hkap_abgleich` und die Befehle
`hkap_verknuepfen`, `hkap_uebernehmen`, `hkap_senden`, `hkap_befunde_senden`, `hkap_raum_zuordnen`, `hkap_strang_zuordnen` (Stränge, Modul 3) (Spezifikation: `entwurf/modul-1d.md`, Abschnitt «KICAD-Seite»).
**Der Kern ruft HKAP auf (Server zu Server, HKAP hat kein CORS); die Oberfläche spricht nur mit KICAD** und rechnet nichts. Texte, HTML und Parameter stehen in
`hkap-text.js` (reine Funktionen, `hkap.test.mjs`), die Dialoge und Aktionen in `aktionen-hkap.js`. Texte des Kerns, die für MCP geschrieben sind, zeigt die Oberfläche
mit den Namen ihrer Knöpfe («mit hkap_senden …» → «mit «Nach HKAP senden» …», `hinweisFuerMenschen`).

**Register Verwalten, Gruppe «HKAP»:** **Verbinden** (`hkap.verbinden`, immer aktiv bei offenem Projekt), **Abgleich** (`hkap.abgleich`) und **Prüfbefunde senden** (`hkap.befunde`) – die
beiden letzten gesperrt, solange `Projektinformation.hkap` fehlt (Tooltip: «Zuerst das Projekt mit HKAP verknüpfen»). Symbole `hkapVerbinden`, `hkapAbgleich`, `hkapBefunde` in `symbole.js`.

**Dialog «Mit HKAP verknüpfen»** (kein Befehl beim Öffnen, nur Abfragen): HKAP-Adresse (Vorgabe: `Projektinformation.hkap.url`, sonst `http://localhost:5013`, änderbar; Enter oder «Verbindung prüfen»)
→ `hkap_verbindung`. Beim Öffnen prüft der Dialog die Vorgabe-Adresse sofort. **Nicht erreichbar** (Kern: `erreichbar = false`, Meldung und Vorschlag) steht als dicker gelb-brauner
Hinweiskasten `.hkap-kasten` im Dialog – deutlich, aber ohne Fehlerrot, ohne Fehlerzeile (HKAP beendet sich 12 s nach dem Schliessen seines Fensters; das ist ein Normalfall). Erreichbar: grüne Zeile mit Fassung,
darunter links die **Projektliste** (Id, Projektname, Phase; Suchfeld oben; die bestehende Verknüpfung ist mit «verknüpft» markiert und vorgewählt), rechts die **Projektierungen** des gewählten Projekts
(`hkap_verbindung { projekt }`). Eine Wahl rechnet sofort einen **Trockenlauf von `hkap_verknuepfen`** und zeigt dessen Zusammenfassung («Verknüpft mit HKAP P-005 «Name» (Kürzel), Projektierung …»).
Warnungen (Kasten): Wechsel von Projekt oder Projektierung (der Basisstand des alten Abgleichs gilt dann nicht mehr), Name oder Kürzel in HKAP weichen von der Verknüpfung ab (Vergleich mit den Angaben des Kerns,
nicht mit der Projektliste), keine Projektierung gewählt; bei bestehender Verknüpfung dazu die `warnungen` der Gegenprüfung aus `hkap_abgleich { datenarten: ['projekt'] }`. «Verknüpfen» sendet
`hkap_verknuepfen { projekt, projektierung?, url? }` – die Adresse nur, wenn sie von der bisher wirksamen abweicht. Kürzel, Bezeichnung und die Zahl der Etagen und Räume liefert `hkap_verbindung` nicht
(nur Id, Name, Phase bzw. Id, Name); Kürzel und Bezeichnung stehen erst im Trockenlauf.

**Dialog «Abgleich mit HKAP»** (`hkap_abgleich`, beim Öffnen und mit «Neu laden»): Kopf mit Verknüpfung, Adresse, Fassung, letztem Abgleich, Zusammenfassung («← 3 aus HKAP · → 2 nach HKAP · 2 Konflikte · 1 Vorschlag»),
den Warnungen des Kerns und den Datenarten-Häkchen (Projekt, Geschosse, Räume – gelten für Übernehmen und Senden; beim Senden entfällt Projekt, das führt HKAP). Nicht verknüpft oder nicht erreichbar: nur der Hinweiskasten mit Kern-Meldung
und Vorschlag. Darunter **ein Abschnitt je Datenart** (Projekt, Geschosse, Räume, Pendenzen):
- *Änderungstabelle:* Richtung (← aus HKAP / → nach HKAP), Art (neu / geändert / gelöscht (Vorschlag)), Datensatz (Element und HKAP-Id), Feld, **Bisher (am Ziel)**, **Neu**, Bemerkung («überschreibt den Wert der anderen Seite · HKAP führt»). Löschen ist immer nur ein Vorschlag.
- *Konflikte:* Tabelle mit KICAD-Wert, HKAP-Wert, Stand beim letzten Abgleich und einer **Auswahl je Zeile** (offen lassen | KICAD | HKAP). **Vorgabe: keine Wahl** – der Konflikt bleibt offen, nichts wird übernommen. «KICAD» behält den KICAD-Wert (er geht mit «Nach HKAP senden» an HKAP),
  «HKAP» übernimmt den HKAP-Wert. Die Knöpfe **«alle übrigen: KICAD» / «alle übrigen: HKAP»** setzen `"*"` (gelten für alle Zeilen ohne eigene Wahl, ein zweiter Klick nimmt die Wahl zurück); der Zähler nennt die Konflikte ohne Wahl.
- *Zuordnungsvorschläge* (Räume mit gleicher Nummer auf beiden Seiten) mit Knopf «Zuordnen» (`hkap_raum_zuordnen`), die *nicht platzierten Räume* (bis zwölf mit Namen, Rest als Zahl), einklappbar «Abweichend, aber ohne Fluss» und «Nur zum Lesen: Anforderungen aus HKAP».
- *Stränge* (Modul 3, B5, Datenart `straenge`; der Dialog ist sonst datenartneutral): ein HKAP-Strang bündelt mehrere KICAD-Stränge (Kennung `hkap`), die Zeile nennt sie mit Komma («HZ-RL-1, HZ-VL-1»); der Block `kicad.straenge`
  erscheint als Kurztext («HZ-VL-1 (Heizung Vorlauf, 2 Endgeräte, 5.45 m) · …», `strangListeText`). Zuordnungsvorschläge (gleiche Bezeichnung) tragen `data-strang` und der Knopf «Zuordnen» ruft `hkap_strang_zuordnen`;
  *nicht platzierte Stränge* (aus HKAP, ohne Leitungen) stehen im Abschnitt; hat KICAD zu einem neuen HKAP-Strang kein oder mehrere passende Systeme, zeigt die **Systemwahl** (`systemWahlHtml`, Haken je System) die Kandidaten,
  die Wahl geht als `system` an `hkap_uebernehmen`. Ein Strang von Hand zuordnen kennt nur MCP (`hkap_strang_zuordnen`) und die Vorschläge – einen eigenen Dialog dafür gibt es noch nicht.
- *Pendenzen:* Vorschau von `hkap_befunde_senden` mit Schwere «fehler» (neu, geändert, erledigt als Vorschlag); gesendet wird über «Prüfbefunde senden».

Fussknöpfe (Enter schliesst nur): **Aus HKAP übernehmen** = `hkap_uebernehmen { datenarten? (nur bei Teilauswahl), konflikte? (nur mit Wahl) }`, ein Paket, rückgängig mit Strg+Z; **Nach HKAP senden …** = zuerst **Trockenlauf** von `hkap_senden`
(Ergebnis im Dialog), dann erscheint **Jetzt senden** (vorher verborgen) für den echten Lauf. Das **Ergebnis** steht in einem Kasten oben im Dialog: Titel («Vorschau (Trockenlauf – nichts wurde geändert)» bzw. «Ergebnis»), die **Zusammenfassung aus `hinweise[0]`**
(`hkap_senden` meldet für KICAD «Keine Änderung.», weil es höchstens Kennungen ändert – die Oberfläche zeigt diese Meldung nicht, sondern Hinweise[0]), bei Übernahme die Tabelle der KICAD-Änderungen (`aenderungenTabelle`), die übrigen Hinweise als Liste. Zeilen, die mit
«KONFLIKT» beginnen (HKAP hat den Datensatz inzwischen geändert, nichts überschrieben), und abgelehnte Datensätze sind hervorgehoben, dazu ein gelber Kasten «n Datensätze in HKAP inzwischen geändert – nichts überschrieben». Danach lädt der Dialog den Abgleich neu.
HKAP-Ausfall während eines Befehls (Meldung «nicht erreichbar» / «antwortet nicht») erscheint ebenfalls als gelber Hinweiskasten, andere Ablehnungen als roter Fehlerkasten mit Vorschlag.

**Prüfbefunde senden** (`hkap_befunde_senden`): Auswahl «Ab Schwere» (fehler = Vorgabe | warnung | hinweis, die Schwere geht immer ausdrücklich mit), beim Öffnen und bei jedem Wechsel eine **Vorschau (Trockenlauf)**, «Senden» erst nach einer gültigen Vorschau,
dann das Ergebnis; «Vorschau neu» zeigt danach «0 neue Pendenzen» (dieselben Pendenzen werden nicht doppelt angelegt).

**Nicht platzierte Räume** (Raum aus HKAP, noch ohne Geometrie): Projektbrowser-Knoten **«Nicht platzierte Räume (n)»** unter «Gebäude» (nur wenn es welche gibt), die Räume stehen dort statt unter ihrem Geschoss (Beschriftung «Nummer Name», Geschoss leise).
Die Darstellung des Kerns liefert die Kennungen nicht mit; die Oberfläche zählt darum jeden Raum **ohne Geometrie** (mit Kennungen nur solche mit Kennung `hkap`). Kontextmenü: Auswählen · **Platzieren …** (`hkap.raum-platzieren`: öffnet den Grundriss des Geschosses, setzt Nummer und Nutzung im Raum-Werkzeug (RM)
und startet es; nach dem Zeichnen fragt ein Dialog, ob der gezeichnete Raum dem nicht platzierten zugeordnet werden soll – sonst entstünde ein zweiter Raum; ohne Grundriss des Geschosses ein Hinweis) · **Einem gezeichneten Raum zuordnen …** (`hkap.raum-zuordnen`: Liste der gezeichneten Räume, gleiche Nummer und gleiches Geschoss zuerst, dann `hkap_raum_zuordnen { raum, hkapRaum }`;
der Platzhalter geht im gezeichneten Raum auf). Die HKAP-Raum-Id (E1-S3) steht in der Kennung `hkap` («PJ-…/E1-S3»); solange die Darstellung sie nicht liefert, liest die Oberfläche sie aus der Herkunft der übernommenen Werte (`element_lesen`: «… HKAP P-005/PJ-2026-001/E1-S3, Feld …»).

**Prüfen:** `node --test js/ansichten/ansichten.test.mjs js/werkzeuge/werkzeuge.test.mjs js/oberflaeche/einfuegen.test.mjs js/oberflaeche/hkap.test.mjs`; Browser (Edge headless über CDP, frischer Server mit Beispielprojekt, **nur gegen die HKAP-Testkopie**):
`HLKS_TEST_SERVER=http://localhost:5142 HKAP_TEST=http://localhost:5098 SHOTS=<Ordner> node js/oberflaeche/hkap.browser.mjs` (nie gegen Port 5013 = HKAP im Betrieb; der Test lenkt eine Anfrage an 5013 auf einen ungenutzten Port um und schreibt nur Testdaten «TEST-C1D …»).

## Gebäudehülle: Register Architektur › Decke, Boden, Dach und Register Hülle (Modul 1c, Auftrag C1a)

Alle Dialoge sind Hüllen um Befehle und Abfragen des Kerns (Spezifikation: `entwurf/modul-1c.md`, Abschnitt «Oberfläche»). **Grundsatz «keine Kennwerte ohne Quelle»:** jeder Kennwert geht mit seiner Quelle
(Feld daneben, Pflicht; Hinweis «Kennwerte nur mit Quelle (Norm mit Ausgabe, Datenblatt)»), die Oberfläche setzt nie einen Wert selbst und kennt keine Vorgaben (Dicke, λ, Rsi/Rse, Ug … sind beim Öffnen leer);
**R, U und Uw zeigt sie nur an, was der Kern liefert** (Trockenlauf bzw. `aufbau_rechnen`), sie rechnet nichts. Texte, HTML, Prüfungen und Parameter stehen in `huelle-text.js` (reine Funktionen, `huelle.test.mjs`),
die Dialoge in `aktionen-huelle.js`. Die Hinweise des Kerns erscheinen mit den Namen der Knöpfe statt der MCP-Befehle («(material_aendern mit …)» → «(Dialog «Material …»)», `hinweisFuerMenschen`).
Ein Fehlertext im Dialog verschwindet, sobald man etwas ändert.

**Decke, Boden, Dach** (Register Architektur, Gruppe «Decke, Boden, Dach»; aktiv bei offenem Projekt mit Geschoss): Dialog `decke_erzeugen` mit Geschoss (Vorgabe: Geschoss der aktiven Ansicht, sonst EG), **Dicke in mm (Pflicht, keine Vorgabe)**
und Aufbau (optional, passend zur Art). Sobald die Eingabe stimmt, rechnet der **Trockenlauf** (entprellt) und zeigt Fläche und Oberkante der neuen Platten (grün) und alle Hinweise des Kerns (Löcher gefüllt, Fläche ohne Raum, doppelt …);
«Decke/Boden/Dach erzeugen» führt aus. Ohne Dicke lehnt der Dialog ab.

**Aufbau …** (Aufbau-Editor, öffnet auch per Doppelklick im Projektbrowser): Name, Art (wand, decke, dach, boden, tür), Wärmestrom (leer = aus der Art); **Schichtentabelle** von innen nach aussen (Material aus den Projektmaterialien, Dicke mm, ↑ ↓ verschieben,
＋ davor einfügen, ✕ löschen, «Schicht aussen anfügen», «Material …» öffnet den Materialdialog); je Schicht **λ** (aus `materialien`; fehlt es: rot «λ fehlt» bzw. «Quelle von λ fehlt») und **R** (aus `aufbau_rechnen`, nur für Zeilen, die dem gespeicherten Stand
entsprechen – R einer geänderten Schicht rechnet der Kern nach dem Speichern). **Rechnung des Kerns:** R und U gross; bei Änderungen der Trockenlauf von `aufbau_anlegen` bzw. `aufbau_aendern` (R und U stehen im Detail der Änderung: «rWert: – → 3.075 m²·K/W; uWert: …»),
sonst `aufbau_rechnen`. **Fehlende Kennwerte stehen rot mit dem Text des Kerns** («Schicht 3: λ von MAT-3 «Testputz» fehlt …», «Rsi/Rse für «horizontal» fehlen …»). Optional **U-Wert ausdrücklich** (geht vor der Rechnung) mit Quelle (Pflicht; ohne Quelle kein Speichern;
Leeren des Feldes entfernt ihn über `element_aendern { uWert: null }`) und die Quelle der Schichtfolge. «Speichern» = `aufbau_anlegen` / `aufbau_aendern` (nur Geändertes), der Dialog bleibt offen und zeigt die Hinweise des Kerns; «Zuweisen …» öffnet den Zuweisen-Dialog (erst nach dem Speichern).
**Zuweisen …** (`aufbau_zuweisen`): Aufbau wählen, darunter alle Wände, Decken, Dächer und Türen mit Häkchen (vorgewählt: die Auswahl der Ansicht, Strg+Klick für mehrere), Suchfeld, «alle sichtbaren wählen/abwählen»; die **Art-Prüfung macht der Kern**, sein Text steht im Dialog («Aufbau … hat die Art … braucht …»; alles oder nichts).

**Material …** (`material_anlegen`, `material_aendern`, `material_uebernehmen`; auch per Doppelklick im Projektbrowser): links die Projektmaterialien (λ fehlt oder ohne Quelle rot) und die **Bürobibliothek** (Abfrage `materialien`; heute leer: Hinweis des Kerns; Einträge mit «übernehmen»),
rechts das Formular: Name, Kategorie und je Kennwert (λ, ρ, c, ε, α, μ) **Wert + Quelle (Pflicht)**; «Quelle in leere Zeilen übernehmen» kopiert die erste ausgefüllte Quelle. Ohne Quelle kein Speichern. Ändert sich ein Wert bei gleicher Quelle, warnt der Dialog
(«gilt sie auch für den neuen Wert?»). Beim Ändern gehen nur geänderte Kennwerte an den Kern, ein geleerter Wert entfernt den Kennwert.

**Kennwerte …** (Fenster; aktiv bei gewählten Fenstern oder Türen, `fenster_kennwerte`): Ug, Uf, ψg, g, Rahmenanteil, fc, Uw ausdrücklich (Fenster) bzw. Ud (Türen; bei gemischter Auswahl wählt man die Klasse), Sonnenschutz (ohne Quelle) und **eine Quelle (Pflicht, sobald ein Zahlenwert da ist)**. Leere Felder bleiben unverändert,
daneben steht der jetzige Wert. Eine **Vorschau des Kerns (Trockenlauf)** zeigt die Änderungen mit dem gerechneten **Uw** und den Hinweis «Annahme: Verglasung dem Fenster ähnlich».

**Übergang …** (`uebergang_setzen`): Rsi/Rse für horizontal, aufwärts, abwärts und gegen Erdreich, **Quelle je Gruppe (Pflicht)**, vorbelegt aus der Projektinformation. Der Dialog sagt: «Ohne Rsi/Rse rechnet KICAD nur R, keinen U-Wert.» Der Kern kennt eine Quelle je Aufruf; verschiedene Quellen gehen darum als je ein Paket.

**Hülle prüfen** (`huelle_pruefen`): Abschnitte nur mit Inhalt (Bauteile ohne Aufbau, Aufbauten ohne U-Wert, Rsi/Rse fehlen, Fenster und Türen ohne Kennwerte, Materialien ohne λ, Aufbau der falschen Art) mit den Texten des Kerns in Rot; **Klick auf eine Zeile springt zum Element** (wählt es aus, der Dialog schliesst),
«Neu prüfen». **Wärmebrücken vorschlagen** (`waermebruecken_vorschlagen`): Geschoss (Vorgabe alle), Trockenlauf mit Zahlen und Hinweisen des Kerns, «Ausführen»; der Satz «ψ fehlt …» steht dabei.

**Listen:** «Hüllbauteile» und «Raumgrenzen» öffnen eine Listenansicht (Ansicht art `liste`, `liste` = `huellbauteile` bzw. `raumgrenzen`; die Ansicht wird beim ersten Mal angelegt, `AN-LI-HUELLBAUTEILE` / `AN-LI-RAUMGRENZEN`) – **aber nur, wenn die Listenansicht (`listenformat.aufbereiten`) alle Zeilen der Ableitung zeigt**
(Prüfung zur Laufzeit, `ansichtZeigtZeilen`); sonst zeigt ein Dialog die Abfrage als Tabelle (Klick auf eine Zeile wählt Bauteil bzw. Raum). Seit der Nachführung von `listenformat.js` (18.10.2026) nehmen beide Aktionen die Listenansicht; der Ersatzdialog bleibt für den Fall, dass eine künftige Fassung des Kerns die Zeilen anders liefert.

**Listenansicht Raumgrenzen und Hüllbauteile** (`listenformat.js`, `liste.js`; Nachführung zu C1a): **Raumgrenzen** – eine Zeile je Raumgrenze (nicht `summenJeRaum`), Spalten Raum, Art, Nachbar, Angrenzung, Bauteil, Aufbau, Fläche m², Azimut °, Neigung °, U, U·A (Hilfsfelder Länge, Höhe, offen, vorgabe, gerechnet, uQuelle verborgen); über der Tabelle die Zeilen des Kopfes («92 Raumgrenzen in 13 Räumen») und alle Hinweise des Kerns; **Fusszeilen** «Summe aussen (ohne U: 3,29 m²)», «Summe erdreich», «Summe beheizt» aus `summen.jeAngrenzung` (Fläche und U·A); Klick auf eine Zeile wählt den Raum. **Hüllbauteile** – Bauteil, Klasse, Geschoss, Aufbau, U, Fläche innen, Fläche aussen, Orientierung (Azimut °), Neigung, Angrenzung, Räume, Hinweis; Fusszeilen «Summe aussen (12 Bauteile)» aus `summen`; Klick wählt das Bauteil. U mit 3 Nachkommastellen. `liste.js` zeichnet Fusszeilen (`tabelle.fuss`, Klasse `li-fuss`) und gibt bei beiden Listen das Geschoss der Ansicht an die Abfrage weiter.

**Eigenschaftenpalette: Hinweise des Kerns und «ohne Quelle»** (`eigenschaften.js`, reine Teile in `huelle-text.js`): Liefert ein Befehl der Palette (`element_aendern`) Hinweise zurück, zeigt die Palette sie in einem Kasten oben im Hinweisstil von «Reserviert von …» (gelb, mit ✕; die Befehlsnamen des Kerns heissen wie die Knöpfe; «Keine Änderung.» entfällt). Der Kasten bleibt bis zur nächsten Eingabe, zur nächsten Auswahl oder bis ✕, auch wenn die Palette sich nach dem Ereignis neu zeichnet. **Kennwerte ohne Quelle** (Material: λ, ρ, c, ε, α, μ; Aufbau: uWert; Fenster: ug, uf, psiG, g, rahmenanteil, fc, uw; Tür: ud – Spiegel der Nachführung im Kern, vom Test gegen `AufbauNachfuehrung.cs` geprüft) tragen am Feld die Marke **«ohne Quelle»** (Zeile unter dem Feld, Rand des Feldes in Warnfarbe). Gerechnete Werte (Herkunft «berechnung») und alle anderen Eigenschaften nie. Die Marke liest die Quelle aus dem Herkunftstext von `element_lesen` («mensch, T, 2026-10-18 12:07[, Quelle]»); die Darstellung (`/api/modell`) liefert die Herkunft nicht – Befund unten.

**Befunde (offen):**
- Die Darstellung (`/api/modell`, `zustand.elemente`) liefert `bezuege` mit Kennzeichen (z. B. `aufbau`), aber keine Herkunft und keine Quelle der Werte. Ob ein Wert eine Quelle hat oder ein U-Wert ausdrücklich ist, erfährt die Oberfläche nur aus `element_lesen` (Herkunftstext, der Palette) bzw. `aufbau_rechnen` (`uAusdruecklich`). Die Marke «ohne Quelle» hängt deshalb am Format des Herkunftstexts (`Abfragen.ElementLesen`); ein strukturiertes Feld `quelle` je Eigenschaft wäre robuster.
- Die Liste der Kennwerte mit Quellenpflicht steht im Kern (`HuelleNachfuehren`) und in `huelle-text.js` (`QUELLE_PFLICHT`); der Test vergleicht beide. Kommt ein Kennwert dazu (z. B. ψ und χ der Wärmebrücke), müssen beide nachgeführt werden – besser lieferte der Katalog «quelle: pflicht» je Eigenschaft.

**Prüfen:** `node --test js/ansichten/ansichten.test.mjs js/werkzeuge/werkzeuge.test.mjs js/oberflaeche/einfuegen.test.mjs js/oberflaeche/hkap.test.mjs js/oberflaeche/huelle.test.mjs`; Browser (Edge headless über CDP, frischer Server mit Beispielprojekt, alle Kennwerte «Testwert C1a»):
`HLKS_TEST_SERVER=http://localhost:5143 SHOTS=<Ordner> node js/oberflaeche/huelle.browser.mjs` (legt Decken für alle Geschosse, Materialien, Aufbauten, Fenster und Wärmebrücken an; mit gesetztem `HLKS_TEST_SERVER` prüft `huelle.test.mjs` zusätzlich die echten Antworten des Kerns, nur lesend).

## Eigenschaftenpalette – Pflichtumfang

Oben Typauswahl (Klasse mit Symbol; Typwechsel folgt später), darunter Gruppen wie in Revit: Identität (Kennzeichen,
Name, Klasse), Zuordnung (Geschoss, Raum, System, teilVon – Auswahllisten aus passenden Elementen), Eigenschaften
laut Katalog (zahl → Zahlfeld mit Einheit, auswahl → Liste, bool → Häkchen, text → Feld, liste → Mehrfachauswahl bzw.
Kommaliste), Abgeleitet (Länge, Fläche, Volumen aus `element_lesen`), Herkunft. Ändern mit Enter/Fokuswechsel →
`element_aendern`; abgelehnt → Meldung mit Vorschlag am Feld. Neben jedem Wert ein kleines «?» → Dialog mit
`warum`. Ist nichts gewählt: Eigenschaften der aktiven Ansicht (wie Revit). Reservierung anzeigen, wenn das Element
von jemand anderem reserviert ist (`zustand.status.reservierungen`).

## Statusleiste – Pflichtumfang

Links: Auswahl («WC-E.02 · WC · Raum E.02») bzw. Hinweis zur aktiven Aktion. Mitte: Abgleich («Abgleich 18:02:31»
grün; bei `abgleich.fehler` rot mit Text; Ereignisstrom getrennt → «Verbindung zum Server unterbrochen»),
Reservierungen («3 eigene reserviert» → Klick: Liste + Freigeben), seit Modul 2, C3 die Familienbibliothek («Bibliothek erreichbar» / «Bibliothek offline · n in Warteschlange» → Klick: Hinweis mit Pfad, Quelle, nachgeholt; Abschnitt «Familienbibliothek»). Rechts: Akteur@Gerät, Stand (Pakete).
Fremde Änderungen kurz melden («Änderung von KI: 3 Elemente»).

## Ansichten – Pflichtumfang

- **Plan-Ansicht** (`grundriss.js`, SVG) für Grundriss, Schnitt und Ansicht: holt die Plangrafik des Kerns und zeichnet die Teile in der Reihenfolge
  `Ebenen.Reihenfolge` (Spiegel `EBENEN` in `plangrafik.js`, vom Test gegen `Grafiktypen.cs` geprüft). `plangrafik.js` (reine Funktionen, in Node
  getestet) macht aus Primitiven SVG; alles liegt in Modell-mm in einer an y gespiegelten Gruppe (`scale(1 -1)`), Text hebt die Spiegelung auf.
  - Jedes Teil mit Id ist ein `<g class="el" data-id>` (anklickbar, Hover-Tipp, Auswahl, Aufleuchten fremder Änderungen `.fremd`); Teile ohne Id
    (`.e-ohne`) sind nicht anklickbar. Treffer und Hervorhebung über unsichtbare, bildschirmfeste Konturen (`.th`).
  - Strichstärke = `Stifte.Mm` (0.18 / 0.35 / 0.70 mm Papier) × Massstab in Modell-mm als Attribut; CSS (`.sd/.sm/.sk`) macht daraus
    `max(--m × Stift, --minpx)` – nie dünner als ein Bildschirmpixel (`--minpx` setzt `grundriss.js` beim Zoomen). Linienarten als `stroke-dasharray`
    mit den Längen aus `PdfZeichner.cs`. Text: Papier-mm × Massstab, Ausrichtung → `text-anchor`, Winkel, fett.
  - Farben nur über Klassen: `pl` Planfarbe (Linie), `pf` Planfarbe (Füllung), `pp` Papier (Füllung «papier»), `bf` Bildschirmfüllung
    (Raumfarben, im dunklen Design über `--gr-raum-deckung` gedämpft); die Planfarbe ist `--gr-text` (im dunklen Design hell), das Papier `--zeichenflaeche`.
    Farben aus den Daten (#rrggbb, Medienfarben) bleiben Attribute. `nurBildschirm` wird gezeigt.
  - Muster: SVG-`<pattern>` aus `/api/schraffuren` (Abstand mm Papier × Massstab, im Ursprung verankert wie im PDF, Linienscharen je Winkel, Punkte, voll).
  - Nach jedem Ereignis «modell» wird die Grafik neu geholt (entprellt 150 ms); Zoom und Auswahl bleiben; eingepasst wird beim ersten Zeigen,
    beim Wechsel von Geschoss/Art und bei «Zoom alles». Zoom mit Mausrad an der Mausposition, Verschieben mit mittlerer Taste, Leertaste+Ziehen oder
    Ziehen auf leerer Fläche, Doppelklick mittlere Taste = alles. Massstabsbalken, Nordpfeil (nur im Grundriss), Ansichtssteuerleiste unten.
  - **Unterlagen (Modul 1b, C2):** Ebene `grundlage` liegt ganz unten (`EBENEN[0]`). DWG/DXF/IFC-Unterlagen sind normale Primitive (graue Linien); PDF und Bild kommen als
    Primitiv `bild` (Punkte = vier Ecken im Modell: unten links, unten rechts, oben rechts, oben links; `quelle` = Id der Unterlage). `plangrafik.js` macht daraus
    `<image href="/api/unterlagen/{quelle}/bild">` der Grösse 1 × 1 mit `transform="matrix(…)"` (`bildMatrix`: affin aus drei Ecken – oben links = Ursprung, oben rechts, unten links; im y-gespiegelten
    Gruppenraum steht das Bild damit aufrecht, auch gedreht), `opacity` = `stil.deckkraft`, `pointer-events="none"`. Ein Teil, das nur aus Bildern besteht, ist `.e-ohne` (nie anklickbar, kein Treffer);
    DWG-Teile mit Id bleiben wählbar (Kalibrieren wählt so). Ausgeblendete Unterlagen (`anzeigen = false`) liefert der Kern gar nicht erst. Der Browser fragt das Bild bei jedem Zeichnen
    per `no-cache` nach (304), innerhalb der Seite wird es aber nicht neu geladen oder neu gezeichnet (kein Flackern).
  - **Deckkraft:** `stil.deckkraft` gilt der Füllung (`fill-opacity`); hat das Primitiv **keine Füllung**, gilt sie der Linie (`stroke-opacity`, in `kontur()`) – so erscheinen graue Unterlagenlinien mit 0.5
    durchscheinend, wie im Plan-PDF. Linien mit Füllung bleiben voll deckend (z. B. Kanal mit Deckkraft 0.2 auf der Füllung).
  - **Werkzeug-Schnittstelle** (Spezifikation «Schnittstelle Ansicht ↔ Werkzeug»): `art`, `ansicht`, `massstab`, `zuModell(clientX, clientY)`,
    `zuBildschirm([x, y])`, `pxJeMm()`, `fangdaten()`, `werkzeugSchicht` (leeres `<g>` in Modellkoordinaten über allem; die Ansicht leert es nie),
    `setzeWerkzeug(w)` / `werkzeug`. Ist ein Werkzeug gesetzt, gehen `pointerdown`, `pointermove`, `pointerup`, `dblclick` (am SVG) und `keydown`
    (am Fenster, Aufnahmephase, nur in der aktiven Kachel und nicht aus Eingabefeldern) zuerst an `w.ereignis(art, e, ansicht)`; `true` = verbraucht.
    Mittlere Taste und Rad bleiben Zoom/Pan. Klasse `werkzeug-aktiv` am SVG (Fadenkreuz, kein Hover-Hervorheben).
- **3D** (`ansicht3d.js`, `koerper3d.js`; three.js aus `lib/three`, OrbitControls): baut die Körper des Kerns – Quader (Drehung um z), Prisma
  (ExtrudeGeometry mit Löchern), Zylinder von/nach, Kugel; 32 Segmente. Farbe = `farbe` des Teils, sonst Materialfarbe `--d3-<material>`
  (wand, decke, raum, glas, tuer, rohr, apparat, geraet, armatur, sonst; fehlt die Variable, gilt eine Vorgabe je Design in `koerper3d.js`), Deckkraft
  = `deckkraft` des Teils, sonst Materialvorgabe (`--d3-wand-deckung`). Auswahl, Hover, Aufleuchten fremder Änderungen, Designwechsel (`hlks-design`),
  Klickvorrang (Haustechnik vor Wand/Decke vor Raum). Aktualisieren: geänderte Ids neu holen; Wand, Tür, Fenster, Decke, **Dach** oder **Geschoss** betroffen (auch
  gelöscht) → alle Körper neu (`WIRKT_AUF_NACHBARN_KLASSEN`; seit 1c: Dach, weil «Decke läuft durch» die Wände auch unter einem Dach kürzt, und Geschoss, weil Kote und Höhe Wände, Decken und Dächer ändern). Koordinaten: three-y = Höhe z.
  - **Hintergrund-Netz (Modul 1b, C2):** Körperteil Art `netz` (`quelle` = Id der IFC-Unterlage). `baueKoerper` setzt nur einen leeren Halter (`userData.netz`); `ansicht3d.js` lädt die Netzdatei einmal je Unterlage
    (`GET /api/unterlagen/{id}/netz?v=<Dateiname>`, `fetch` → `arrayBuffer`), `netzdatei.js` (rein, ohne three und DOM; Format im Dateikopf und in `Hlks.Kern/Import/IfcNetz.cs`) liest sie mit `DataView` und führt
    **nur die Hintergrund-Produkte (Kennzeichen 0)** zu EINER Geometrie zusammen (`leseNetz`: Punkte gleich in three-Metern, Indizes mit Versatz; die übernommenen zeichnet der Kern als Körper). Eine
    `BufferGeometry` je Unterlage im Zwischenspeicher `netze` (Schlüssel = Dateiname aus `werte.netz`); neu geladen wird nur, wenn sich der Dateiname ändert oder die Unterlage verschwindet (Ausblenden
    behält das Netz im Speicher). Mesh (`netzMesh`): Farbe und Deckkraft aus dem Körperteil (Vorgabe 0.5), `MeshLambertMaterial` doppelseitig, `flatShading` (keine Normalen nötig), `depthWrite` aus, `raycast` aus
    (nie ein Treffer, kein Hover – auch aus Leistungsgründen), `renderOrder −1`; `freigeben()` lässt die geteilte Geometrie stehen. Das Lesen läuft im Hauptstrang (Bernstrasse, 8 MB, 305 000 Dreiecke: ca. 80 ms);
    bis die Datei da ist, zeigt der Körper nichts, danach wird eingepasst (solange die Kamera nicht bewegt wurde). Fehler (404, kaputte Datei) stehen in der Konsole und werden nach 10 s erneut versucht.
    Prüfhilfe: `.d3-wurzel[data-netz]` (JSON mit Produkten, Dreiecken, Bytes, Lade- und Lesezeit).
- **Darstellung «Hülle»** (seit 1c, Auftrag C1b; Ansichtseigenschaft `darstellung` = `huelle`, Vorgabe `standard`; umgeschaltet mit dem Knopf «Hülle» im Register Ansicht, Gruppe «Darstellung»,
  Aktion `ansicht.huelle` in `aktionen-ansicht.js` = `element_aendern { darstellung: "huelle" | null }`, rückgängig mit Strg+Z; in der Eigenschaftenpalette ist `darstellung` die generische Auswahl). Die Oberfläche rechnet nichts:
  Angrenzung, Geometrie und Flächen liefert der Kern (`Raumgrenzen`, Abfrage `raumgrenzen`). Die Farben sind frei gewählt (Annahme Farbe, kein Normwert): **aussen** #e0482f, **Erdreich** #8a6a3b, **unbeheizt** #3f7fd1, **beheizt** #5aa469
  (Konstanten in `HuelleGrafik.cs`, Spiegel `HUELLE_FARBEN` in `huelle-darstellung.js`, der Node-Test vergleicht beide).
  - **Grundriss:** Die Streifen zeichnet der Kern (`HuelleGrafik`, Grafikquelle ohne eigene Elemente, nur `Gemeinsam()`): je Raumgrenze Wand, Fenster, Tür des Geschosses ein Streifen auf der Raumseite der Kante, 2 Papier-mm breit,
    Mitte 1 Papier-mm im Raum (liegt an der Wandfläche an; × Massstab). Beheizt nur dezent (1 mm, Deckkraft 0.4: Innenwände sind die Mehrheit und würden die Hülle übertönen). Fenster, Türen und offene Kantenstücke (keine Wand) heller
    (Deckkraft 0.45) mit gestrichelter Kontur; der Wandstreifen spart sie aus. Böden gegen Erdreich oder aussen als dezente Fläche im Raum (Deckkraft 0.22). Ebene `huelle` zwischen `decke` und `wand` (über den Räumen, unter den Wänden),
    alles `nurBildschirm` (nicht im Plan-PDF; Vorschlag), ohne Id (nicht anklickbar); `plangrafik.js` dämpft diese Füllungen im dunklen Design nicht (`bf` entfällt für die Ebene huelle).
  - **Legende** unten links (HTML, `.gr-legende` bzw. `.d3-legende`): vier Farben mit Namen und Flächensumme je Angrenzung in m² (Innenmass der Raumgrenzen, `summen.jeAngrenzung` der Abfrage `raumgrenzen`; Grundriss: `{ geschoss }`, 3D: ganzes Modell),
    nur bei `darstellung = huelle`; Fehler der Abfrage stehen als Text in der Legende. `legendeDaten`/`legendeHtml` in `huelle-darstellung.js`.
  - **3D:** `ansicht3d.js` lädt `abfrage('raumgrenzen', { geometrie: true })` (nach Modelländerungen entprellt 600 ms neu) und `huelle3d.js` baut daraus **eine Geometrie je Angrenzung** (vier Meshes, `MeshBasicMaterial` doppelseitig, durchscheinend 0.55; beheizt 0.1;
    `depthWrite` aus, `renderOrder` 5): Seiten als senkrechte Vierecke Von/Bis × unten/oben, 12 mm in den Raum versetzt (Raum links der Laufrichtung; Fenster und Türen 24 mm); Boden, Decke, Dach als Ringe (Löcher über Stege der Breite 0,
    `THREE.ShapeUtils`/earcut) auf der Raumhöhe ± 12 mm. Die Architekturkörper bleiben sichtbar, aber blasser (`BLASS_HUELLE`: Wand und Öffnung ×0.5, Raum ×0.4, Decke ×0.22; `baueKoerper(k, { blasser })`). Die Hüllflächen liegen ausserhalb der Gruppe «modell»
    (zählen nicht beim Einpassen) und sind nur beim Klick Treffer (kein Hover, aus Leistungsgründen). **Klick auf eine Fläche** zeigt im Infofeld (`.d3-info`, unten rechts) Raum, Nachbar, Bauteil, Angrenzung, Fläche, Aufbau und U (wenn vorhanden),
    umrandet die Fläche und wählt das Bauteil aus (Eigenschaftenpalette); Haustechnikkörper vor der Fläche haben Vorrang, ein Klick ins Leere schliesst das Infofeld. Prüfhilfe: `.d3-wurzel[data-huelle]` (JSON mit Zeilen, Flächen, Hol- und Bauzeit).
- **Raumumriss mit Insel** (Befund B1, 1c): Ein Raum mit Insel hat in den Daten einen Ring mit Steg der Breite 0. `ArchitekturGrafik` zerlegt ihn (Clipper-Vereinigung) in Aussenring und Löcher: Raumfarbe und Umriss als Polygon mit Loch (`loecher`), Fangpunkte und
  -kanten ohne Steg, 3D-Raumkörper als Prisma mit Loch; kein Steg mehr als Haarlinie. Räume ohne Steg bleiben unverändert.
  Ringe mit doppelten Punkten (Steg, Spitze der Breite 0) laufen durch die Vereinigung, Ringe ohne doppelte Punkte bleiben unverändert (BKW: 497 Räume, davon 39 mit Loch, kein Steg und keine Spitze mehr in den Umrissen).
- **Prüfen (Darstellung Hülle):** `node --test js/ansichten/huelle-darstellung.test.mjs` (Farben gegen `HuelleGrafik.cs`, Legende, Infotext, 3D-Flächen und Treffer, blassere Körper, Ebene huelle); Kern: `HuelleGrafikTests.cs`; Browser (Edge headless über CDP, frischer Server mit Beispielprojekt):
  `HLKS_TEST_SERVER=http://localhost:5144 SHOTS=<Ordner> node js/ansichten/huelle-darstellung.browser.mjs` (legt Boden, Decken und Dach für alle Geschosse an, schaltet Grundriss und 3D auf Hülle, klickt eine Fläche; mit `BKW=1` zusätzlich das echte
  Architekten-IFC (BKW Hauptsitz, 9708 Raumgrenzen, 33 234 Dreiecke) und Drehen des 3D. Leistung BKW (Kern und Browser auf dem gedrosselten Entwicklungsrechner, headless = Software-Rendering): Abfrage `raumgrenzen` mit Geometrie 0.25 s / 3.1 MB, Plangrafik
  Grundriss EG mit Hülle 0.24 s (ohne 0.20 s), Aufbau der Geometrien im Browser 20 ms, 3D mit Hülle 6.8 Bilder/s gegen 8.0 ohne Hülle beim Drehen).
- **Planblatt** (`planblatt.js`): zeigt `/api/plan/{kz}.pdf` in einem `<iframe>` (der PDF-Betrachter des Browsers zoomt und druckt), neu geladen
  nach Änderungen (entprellt 1 s, verdeckt erst beim Anzeigen). Planblätter sind immer hell (`color-scheme: light`). Fehler des Kerns erscheinen als Text.
- **Liste**: zeigt die Ableitung laut `werte.liste` (Vorgabe raumliste) als Tabelle, frischt bei Modelländerungen
  auf (entprellt), zeigt den Modellstand, Klick auf Zeile wählt das Element aus.
- **Schema** (`schema.js`, seit Modul 3, C2): zeichnet die Plangrafik der Schema-Ansicht und macht sie bedienbar – Abschnitt «Schema (Modul 3, C2)» am Ende.
- **Projektbrowser «Pläne»**: Planblätter (Nummer und Titel) mit ihren Ansichtsfenstern darunter; Doppelklick öffnet das Blatt (ein Ansichtsfenster
  öffnet die platzierte Ansicht). Aktionen im Register Ansicht, Gruppe «Planblätter» (`aktionen-plaene.js`): «Planblatt» (Befehl
  `planblatt_anlegen`), «Ansicht platzieren» (`ansicht_platzieren`), «PDF öffnen» (neues Fenster); «Öffnen» im Kontextmenü.

## Design und Masse (Dichte wie Revit 2026, hell und dunkel)

**Masse** (CSS-Pixel bei 100 %, gemessen an Revit 2026; Variablen `--hoehe-…` in `hlks.css`): Schrift überall Segoe UI 12 px
(nur Festbreitenschrift 11 px); fett nur für Aktives (aktiver Reiter, aktive Ansicht, Titel), nicht für Ordner im Browser.
Titelleiste mit Schnellzugriff 32 · Registerzeile 22 · Menüband-Gruppe 70 + Titel 18 · Optionsleiste 24 · Statusleiste 22 ·
Ansichtsreiter 22 · Ansichtssteuerleiste 20 · Palettenkopf 18 (Titel «Eigenschaften», «Projektbrowser», «Koordination» aus der Anmeldung bei der Palettenverwaltung, `paletten.js`) ·
Zeilen in Palette und Browser 20 (Gruppenköpfe auch 20) · Eingabefelder in der Palette 18 · Suchfeld 22 · Einrückung im Browser
16 je Stufe · Palettenbreite 280. Grosse Knöpfe: Symbol 32 mit Beschriftung darunter; mittlere: Symbol 16, Beschriftung rechts,
drei Zeilen à 23; kleine: nur Symbol 16 (siehe «Menüband passt sich der Breite an»). Abstände 2–4 px. **Kein CSS `zoom`** (verschiebt die Klickkoordinaten im SVG-Grundriss).

**Design:** `:root` = hell, `:root[data-design="dunkel"]` = dunkel (Vorgabe), gleiche Variablennamen. Bereichsvariablen
(`--menueband`, `--palette`, `--browser`, `--eingabe`, `--steuerleiste`, `--ansichtsreiter-zeile`, `--zeichenflaeche` …),
Grundriss (`--gr-…`) und 3D (`--d3-…`) stehen alle in `hlks.css`; in `oberflaeche.css` und `ansichten.css` gibt es
keine festen Farben. `plangrafik.js` setzt nur Klassen (`pl`, `pf`, `pp`, `bf`, `sd`/`sm`/`sk` …, siehe «Ansichten»), die Planfarbe folgt `--gr-text`, das Papier `--zeichenflaeche`;
Medienfarben (SIA 410/1) und Raumfarben aus den Daten bleiben Attribute – im dunklen Design werden Raumfüllungen über
`--gr-raum-deckung` gedämpft. 3D liest Farben über `cssVar` (`koerper3d.js`, `ansicht3d.js`).
**Umschalten:** Aktion «Design hell/dunkel» (Register Ansicht, Gruppe Darstellung, `aktionen-ansicht.js`), Logik in
`js/oberflaeche/design.js` (gemerkt unter `hlks.design` über `merken`); `design-start.js` setzt das Attribut im `<head>` vor dem
ersten Zeichnen (kein Flackern). Das Fensterereignis `hlks-design` sagt den Ansichten Bescheid (3D färbt neu ein; der
Grundriss braucht nichts, die Klassen folgen den Variablen). Neue Ansichten, die Farben in Objekte einbauen, hören ebenfalls
auf `hlks-design`. Planblätter und Druck bleiben später immer hell (Papier): dafür eigene Zeichenfläche mit festen hellen
`--gr-…`-Werten, nicht die Designvariablen der Bildschirmansicht.

## Prüfen ohne Browser

Der Testserver läuft auf http://localhost:5131 (Beispielprojekt «MFH Musterweg»), die Oberfläche wird direkt aus dem
Quellordner ausgeliefert (Änderungen ohne Neubau sichtbar). Daten ansehen: `Invoke-RestMethod http://localhost:5131/api/modell`.
Syntax prüfen: `node --check <datei.js>`. Die Sichtprüfung im Browser macht der Hauptlauf.

**Browser-Tests (`*.browser.mjs`), Stand Q2 (19.10.2026):** Sie starten ihren Server aus dem **gemeinsamen Build** (`HLKS_BAU`, sonst `HLKS_BUILD`, sonst
`%USERPROFILE%\HLKS_CAD_build`, wie `Directory.Build.props`); die früheren Standardordner je Auftrag (`m3c3`, `c2m2`, `g1` …) sind überholt, wo sie in diesem Dokument
noch stehen. Ein alter Ordner liess den Test gegen einen veralteten Kern laufen (Befund Q2: «42 von 43» in `familien-leitung`, «128 von 129» in `familie-unterlagen`).
`familie-unterlagen` braucht zusätzlich die gebaute Attrappe (`dotnet build tests/Hlks.ClaudeAttrappe`, im selben Build-Ordner). Die Ports sind je Test über Umgebungsvariablen
wählbar (`HLKS_M3C3_PORT`, `HLKS_C2_PORT`, `HLKS_M3BC1B_PORT`, `HLKS_C3_PORT`, `HLKS_P1_PORT`, `HLKS_G1_PORT`, `HLKS_M3C1_PORT`, `HLKS_M3C2_PORT`; `start.browser.mjs`: `HLKS_S1_PORT`,
`menueband.browser.mjs`: `HLKS_R_PORT`; Edge-Port `HLKS_CDP_PORT`). Beide beenden vor dem Start den Prozess, der ihren Port hält. `familien.browser.mjs` läuft gegen einen vorbereiteten Server
mit frischem Beispielprojekt (`HLKS_TEST_SERVER`). Tests, die nach einem Befehl auf die Oberfläche warten (Projektbrowser: entprellt neu geholte Familienliste; Palette der Familie
holt die Definition nach), warten auf die Zeile bzw. das Feld, statt sofort zu prüfen.

## Familien (Modul 2, C1)

Oberfläche für Familien auf dem Familien-Kern (`entwurf/modul-2.md`, Abschnitte «Familienformat», «Befehle und Abfragen», «Oberfläche»). **Der Editor rechnet nichts:** er macht aus Eingaben Zahl, Formel-Text oder Wert und aus Tabellenzeilen die `ops` für `familie_aendern`; ob eine Formel gilt, sagt der Kern (Trockenlauf bei jeder Eingabe, Fehler rot mit seiner Meldung).
Die Definition liest die Oberfläche **nur über die Abfrage `familie`** (die Darstellung des Modells trägt sie nur als Zusammenfassung), die Liste über `familien`. Reine Logik steht in `familien-text.js` (Tabellen ↔ ops, Formel-Felder, Typkatalog-Zellen, Körper und Sinnbild, Fehler des Kerns), `familien-ansicht.js` (Projektion, Achsen, Vorschau), `familien-palette.js` (Palette) und `typauswahl.js` (Platzieren); alles in Node getestet (`familien.test.mjs`).

**Editor** (eigene Ansichtsart `familie`; `fenster.js` kennt sie über `MODULE_ZUSATZ`, ein «Ansicht»-Element gibt es nicht – das Fenster gehört dem Element der Klasse `Familie`; Reiter «Familie <Name>»). Links die Seiten (Reiter im Editor, merkt sich die letzte), rechts die Ansichten (Teiler ziehbar):
- **Parameter:** Tabelle Name · Art (typ/exemplar) · Datentyp · Einheit · Gruppe · **Wert oder Formel** · Ziel · Beschreibung. Eine Zahl ist der Vorgabewert, alles, was bei Zahlfeldern keine Zahl ist (oder mit «=» beginnt), ist eine Formel (Anzeige «= bodenabstand + hoehe», bei Formeln daneben «→ Wert» für den ersten Typ). «＋ Parameter» öffnet einen Dialog mit Prüfung des Kerns beim Tippen.
  **Umbenennen** führt Formeln, Körper, Anschlüsse, Sinnbild und Typwerte nach (ein Paket aus ops; der alte Name wird zuletzt gelöscht) und **fragt vorher**, wenn der Parameter verwendet wird (Liste der Stellen). **Löschen** fragt ebenfalls und nennt das Urteil des Kerns (Trockenlauf): Wird der Parameter in einer Formel gebraucht, lehnt der Kern ab, die Rückfrage hat dann nur «OK».
- **Typen:** der Typkatalog wie in Revit – Zeilen = Typen, Spalten = Typparameter ohne Formel; Zellen bearbeitbar (Zahl, ja/nein, leer = Vorgabe des Parameters), fehlende Werte rosa; «＋ Typ» (leer), «Typ kopieren», «Typ löschen», Typname umbenennbar (Rückfrage bei platzierten Elementen).
- **Körper:** Liste (Art, Name, Detailgrad g m f) und Eigenschaften des gewählten Körpers je Art: Name, Material, Detailgrad (Häkchen), Sichtbar (Formel) und die Felder der Art; **jede Zahl darf eine Formel sein**. Neu: Quader, Zylinder, Kegel, Kugel, Extrusion, Rotation, Sweep (Ribbon und Seite); Konturen, Löcher und Pfade sind Punkttabellen (u, v, Wölbung bzw. x, y, z); Duplizieren, Löschen.
- **Anschlüsse:** Tabelle name · medium (Auswahl: «*», Katalog, Parameter vom Datentyp medium) · fluss · art · form · Grösse (rohr dn · kanal rund d · kanal rechteckig b, h) · gewinde · Lage x y z (Formeln) · Richtung · Beschreibung. Ein neuer Anschluss hat keine erfundene Grösse (sie kommt nur aus der Unterlage).
- **Sinnbild:** Liste und Formular wie bei den Körpern (Linie, Polylinie, Polygon, Kreis, Bogen, Text; Stift, Linienart, Füllung, Detailgrad, Sichtbar), Vorschau der Teile mit Zahlenwerten, «Sinnbild entfernen».
- **Prüfen:** Fehlerliste aus der Abfrage `familie` (Definitionsfehler und Fehler je Typ); **Klick springt zum Feld** (fehlt einem Typ ein Wert, zur Zelle im Typkatalog). Der Reiter trägt die Zahl der Meldungen.
- **Ansicht:** vier Fenster (Vorne, Oben, Links, 3D) oder ein Fenster (Vorne, Hinten, Links, Rechts, Oben, Unten, 3D); Typ und Detailgrad (grob, mittel, fein; Vorgabe fein) über `familie_ansicht`. 2D mit dem Plangrafik-Zeichner (`plangrafik.js`, Ids entfernt: nicht anklickbar), 3D mit `koerper3d.js` (ein Körperteil je Gruppe). **Ursprung (Einfügepunkt) und Achsen x (rot), y (grün), z (blau)** sind in 2D und 3D markiert,
  die **Anschlüsse mit Pfeil und Namen** (2D vom Kern, 3D hier). Der **gewählte Körper** ist hervorgehoben: 3D in der Auswahlfarbe, 2D mit gestricheltem Rahmen (Projektion der Netzdaten, `teilBox`).
- **Alles ändert über `familie_aendern`** (ein Paket je Änderung, rückgängig mit Strg+Z; der Editor führt Rückgängig und Änderungen anderer – KI, anderer Arbeitsplatz – nach). Eine Eingabe wird mit Enter, Tab oder Fokuswechsel übernommen, Escape verwirft; der Fokus bleibt nach dem Neuzeichnen beim Schlüssel (`data-fk`) des Feldes.

**Register «Familie»** (Kontextregister, erscheint und wird gewählt, solange ein Editor aktiv ist; `menueband.js`, `GRUPPENFOLGE.familie`): **Parameter** (Parameter hinzufügen, Parameter), **Typen** (Typ hinzufügen, Typ kopieren, Typ löschen, Typen), **Körper** (Körper; Quader, Zylinder, Kegel, Kugel, Extrusion, Rotation, Sweep, Körper löschen), **Anschlüsse** (Anschluss hinzufügen, Anschlüsse), **Sinnbild** (Sinnbild bearbeiten),
**Ansicht** (Vier Fenster, Vorne … Unten, 3D, Detail grob/mittel/fein), **Prüfen** (Familie prüfen). Alle Knöpfe rufen `editor.aktion(…)` des aktiven Editors (`aktiv.js`); Symbole `fam…` in `symbole.js`.

**Projektbrowser «Familien»:** Knoten «Familien (n)» → Kategorie → Familie (Name, Kennzeichen, Marke **«Entwurf»** grau bzw. «Fehler» bei ungültiger Definition) → Typen; am Ende der Knoten **«Katalog»** (bisheriger Inhalt: Schicht → Klasse). Liste aus `familien` (`daten.js`, bei Änderungen an Familien entprellt neu geholt). Doppelklick auf eine Familie öffnet den Editor (`familie.bearbeiten`), auf einen Typ platziert (`familie.platzieren-aus-browser`);
Kontextmenü: Bearbeiten, Platzieren, Neue Familie …, Familie laden …, Auswählen, Löschen (auf «Familien»: Neue Familie …, Familie laden …). **Neue Familie** (`familie.neu`): Kategorie (nur familienfähige Klassen: die Klasse hat die Eigenschaft «familientyp») und Name → Entwurf mit einem Typ «Standard» (`familie_entwurf`), der Editor öffnet.
**Familie laden** (`familie.laden`): Datei aus `kataloge/familien/` oder voller Pfad, Vorschau des Kerns (Trockenlauf von `familie_laden`), «Laden».

**Platzieren** (Register Gebäudetechnik, Gruppe «Apparate und Armaturen», **«Familie platzieren»**, Kürzel **CM**; Werkzeug `familie` in `platzieren.js`, Felder in `optionen.js` über `typauswahl.js`): Optionsleiste **Typ «Familie: Typ»**, **Drehung** (Zahl in Grad) und die **Exemplarparameter** der gewählten Familie (nur ohne Formel; leer = Wert des Typs, ja/nein mit «(Typ)»).
Klick im Grundriss setzt den Einfügepunkt (mit Fangen und Raster), **die Leertaste dreht um 90°** (Umschalt+Leertaste zurück), dann `familie_platzieren` (mit `geschoss` der aktiven Ansicht); mehrfach setzen bis Esc oder Rechtsklick. Die **Vorschau** am Mauszeiger ist der Umriss aus `familie_ansicht` (oben, Detailgrad mittel), gedreht und verschoben, mit Pfeil zur Vorderseite.
Ohne offenen Grundriss öffnet die Aktion den ersten. Der Grundriss zeigt nur die Gewerke seiner Ansicht: für Heizkörper muss «heizung» in den Gewerken der Ansicht stehen.

**Eigenschaftenpalette:** Platziertes Element (Bezug `familie`): Gruppe **«Familie»** – Familie mit Knopf «Editor» (Sprung in den Editor), **Typ** (Auswahl; Wechsel über `element_aendern { familientyp }`, der Kern führt Anschlüsse und Kennwerte nach), **Exemplarparameter** (Eingabe ändert `element_aendern { parameter }` als ganzes Objekt, leeres Feld entfernt den Wert; Platzhalter = Wert des Typs; Formelparameter nur lesend),
Gruppe **«Anschlüsse»** (Medium, Fluss, Grösse und **Weltlage** aus `anschlusslagen`, Verbindung). Kennwerte mit Herkunft «familie» tragen die Marke «Familie». Die Rohfelder `familientyp` und `parameter` stehen dann nicht zusätzlich in «Eigenschaften». Das Element **Familie** selbst (Editor aktiv oder im Projektbrowser gewählt): Name, Hersteller, Produkt, Beschreibung (`kopf_setzen`), Inhalt, Verwendungen, Prüfung.

**Annahmen** (Prüfliste): Neue Körper, Konturen und Sinnbildteile bekommen **Platzhaltermasse** (Würfel 100 mm …), damit der Entwurf gültig bleibt – keine Herstellerwerte; ein neuer Anschluss hat Lage [0, 0, 0], Richtung [0, 0, −1] und keine Grösse. Ein Typ ohne Wert für einen Parameter ohne Vorgabe ist im Entwurf erlaubt (Hinweis des Kerns, rosa Zelle, Fehler unter «Prüfen»).
Ein **umbenannter Parameter wandert ans Ende** der Parameterliste. Der Rahmen des gewählten Körpers ist der achsparallele Rahmen seiner Eckpunkte (Zylinder: Achse ± Radius; Anzeigehilfe). Die Zuordnung Körperliste ↔ Körperteile der 3D-Antwort läuft **über die Reihenfolge** (nur Körper im Detailgrad; ein durch «sichtbar» ausgeblendeter Körper macht sie unscharf – dann steht «Hervorheben nicht eindeutig»).
Die Typauswahl steht in der Optionsleiste, nicht zusätzlich in der Eigenschaftenpalette. Die Dateien der Bibliothek für «Familie laden» liest die Oberfläche aus dem Vorschlag des Kerns bei einer unbekannten Datei (Vorschlagsliste; ein Pfad geht immer).

**Befunde am Kern** (nicht von C1 gebaut): (1) `familie_ansicht` liefert keine Körper-Id je Körperteil – die Hervorhebung rät über die Reihenfolge; eine Id am Teil wäre robuster. (2) Die 2D-Ansichten der Abfrage zeichnen nur Körper, **nicht das Sinnbild** – der Editor kann es nur für Zahlenwerte selbst vorschauen; eine Ansicht «grundriss» mit Sinnbild wäre besser. (3) Es gibt keine Abfrage für die Dateien der Bibliothek (`kataloge/familien`).
(4) Umbenennen eines Parameters führt die **Exemplarwerte platzierter Elemente** (`parameter`) nicht nach; der Editor ändert nur die Definition. (5) `parameter_setzen` mit neuem Namen hängt an: die Reihenfolge der Parameter bleibt beim Umbenennen nicht erhalten. (6) Die Kurzform des Katalogs hat kein Feld «familie»; die Oberfläche erkennt familienfähige Klassen an der Eigenschaft «familientyp».

**Prüfen:** `node --test js/familien/familien.test.mjs` (42 Tests; mit `HLKS_TEST_SERVER` zusätzlich gegen den echten Kern, nur Trockenläufe: alle Platzhalter und ops der Oberfläche nimmt der Kern an) und alle bisherigen `*.test.mjs`; Browser (Edge headless über CDP, frischer Server mit Beispielprojekt, Port 5146):
`HLKS_TEST_SERVER=http://localhost:5146 SHOTS=<Ordner> node js/familien/familien.browser.mjs`. Der Test baut im Editor von Hand einen Heizkörper nach (11 Parameter mit Formel oberkante = bodenabstand + hoehe, zwei Typen, zwei Quader, zwei Zylinder, Anschlüsse vl/rl mit Lage als Formel), platziert ihn mit Drehung 90° und prüft die Weltlage gegen die Handrechnung (punkt [5000, 3000, 0], Typ 22-600-1000: vl (4950, 3950, 120), rl (4950, 3900, 120)),
Typwechsel und Exemplarwerte in der Palette, Rückgängig und – als Gegenprobe – die geladene Testfamilie (gleiche Anschlusslagen).

## Familie aus Unterlagen (Modul 2, C2)

Oberfläche für den Weg «Herstellerunterlagen → Entwurf → Prüfen → Freigeben» (`entwurf/modul-2.md`: «Zusammenführen, Konflikte, Abdeckung», «KI-Lauf», «Internet-Recherche»). **Die Oberfläche entscheidet und rechnet nichts:** Status, Konflikte, Abdeckung, Masse und Kontrollbild kommen vom Kern; Entscheide gehen als Befehle an ihn (`konflikt_entscheiden`, `wert_bestaetigen`, `familie_freigeben`, `familie_speichern` – der Kern lehnt die KI dort ab). Dateien unter `js/familien/`, Stil `css/familien-unterlagen.css` (Klassen `fu-…`, Farben der Status nur als Variablen `--fu-st-…` in `:root`, hell und dunkel).

| Datei | Inhalt |
| --- | --- |
| `unterlagen-text.js` | reine Logik (Node): erlaubte Dateien, Zeile je Datei, Auftragsparameter, Stand der Aufträge (Ereignis «auftrag»), Status der Abdeckung, Tabelle Parameter × Typ, Gründe der gesperrten Freigabe, Konflikt-Wahl → Parameter, Passpunkte → Parameter, Boxen und Klicks im Bild, «als Quelle übernehmen» → ops, Unterlagen aus dem Internet, Texte zu Quellen |
| `auftrag.js` | Zugang zu `/api/auftraege` (starten, lesen, abbrechen) und Stand der Aufträge aus dem Ereignis «auftrag» (`ereignisse.js` meldet die Art an `zustand.js`; die Oberfläche ignoriert sie nicht mehr) |
| `unterlagen-dialog.js` | Dialog «Familie aus Unterlagen» (Schritt 1 Dateien, Schritt 2 Lauf) |
| `pruefansicht.js`, `pruefansicht-html.js` | Register «Entwurf prüfen» im Familieneditor (Controller und reines HTML) |
| `befunde-browser.js` | Register «Unterlagen»: Befund-Browser |
| `quelle-ansehen.js`, `unterlagen-bild.js` | Dialog «Quelle ansehen», Bildbetrachter mit Marken und Klick → Unterlagen-Koordinaten |
| `aktionen-unterlagen.js` | Aktionen (Register Gebäudetechnik › Apparate und Armaturen, Register «Familie» › Gruppe «Unterlagen», Projektbrowser), Meldung am Ende eines Laufs |
| `unterlagen.test.mjs`, `unterlagen-internet.test.mjs`, `familie-unterlagen.browser.mjs`, `familie-unterlagen-def.mjs` | Tests und Testdefinition |

**Einstieg:** Projektbrowser «Familien»: Knopf **«＋ Unterlagen»** und Kontextmenü «Familie aus Unterlagen …»; Kontextmenü einer Familie: «Entwurf prüfen», «Aus Unterlagen ersetzen …» (ersetzen angehakt); Entwurf-Zeilen tragen den Knopf **«Prüfen»**. Register Gebäudetechnik: «Familie aus Unterlagen»; Register «Familie», Gruppe «Unterlagen»: «Familie aus Unterlagen» (Familie vorgewählt, nicht angehakt), «Entwurf prüfen», «Unterlagen und Befunde».

**Dialog, Schritt 1:** Ablagefläche (Ziehen oder «Dateien wählen …», Feld `#fu-datei`, mehrere Dateien); erlaubt pdf, dwg, dxf, step, stp, iges, igs, stl, obj, ifc, xlsx, csv, txt (Typkatalog), png, jpg. Je Datei: Hochladen (`POST /api/unterlagen`), `unterlage_einfuegen { datei, zweck: familie }`, `unterlagen_lesen`; eine Zeile mit Art, Seiten (nur PDF), Befunden je Art, Tabellen und den Hinweisen des Lesers, Fehler rot. Dazu **Kategorie** (Klassen mit `familie: true`), **Hinweis an die KI**, **«Bestehende Familie ersetzen»** (Auswahl der Familie) und der Schalter **«Fehlende Angaben im Internet recherchieren»** (Vorgabe ein; geht als `internet: true|false` mit; Text darunter: Werte zählen nur aus heruntergeladenen und abgelegten Dokumenten mit Quelle, interne Adressen sind gesperrt). Hinweis: der Lauf geht über Christians Claude-Abo und dauert einige Minuten.
**Schritt 2:** `POST /api/auftraege`; Anzeige läuft/fertig/Fehler/abgebrochen, Dauer, **letzte Schritte** des Protokolls (Ereignis «auftrag», dazu alle 4 s `GET /api/auftraege/{id}` als Sicherheitsnetz), **«KI-Lauf abbrechen»**. Der Dialog lässt sich jederzeit schliessen; der Lauf geht weiter, beim Wiederöffnen zeigt er ihn, am Ende meldet eine Einblendung das Ergebnis. Am Ende: Bericht (`ergebnis.bericht`), die angelegten Familien mit offenen Konflikten, Knopf **«Prüfen»**, die Liste **«Aus dem Internet abgerufen»**.

**Prüfansicht** (Register «Entwurf prüfen»; eine eigene Seite des Editors, sie bleibt beim Nachführen stehen – `eigen: true` in `editor.js`): Kopf mit Status und Summen der Abdeckung; **Freigabe** («Freigeben» nur ohne offene Konflikte, Fehler, Pflichtwerte ohne Quelle und Masse ausserhalb der Toleranz; sonst die Gründe als Liste, Klick springt zur Stelle; danach «In Bibliothek speichern» mit freiem Dateinamen); **Bericht** der KI und Unterlagen; **Konfliktliste** (alle Werte mit Quellen und «Quelle ansehen», Vorschlag nach Vorrang hervorgehoben, nie vorgewählt, «anderer Wert», Begründung, «Entscheiden» / «Neu entscheiden»); **Abdeckung** (Parameter × Typ; geprüft grün, ungeprüft-bild gelb, fehlt rot, Konflikt violett, Mensch blau; Klick zeigt die Quellen mit Datei, Seite, Stelle; **«Wert bestätigen»** bei fehlt und ungeprüft-bild); **Massvergleich** (`familie_masse`, Abweichungen rot); **Kontrollbilder** (`familie_vergleichen`: Unterlage, Seite, Ansicht, Typ, Detailgrad; Passpunkte als Zahlen oder **angeklickt**: «Punkte anklicken …» wechselt viermal zwischen Familienansicht (rastet auf Eckpunkte, 12 px) und Unterlagenbild; PDF-Massbilder tragen den Vermerk **«schematisch»**, DXF/STEP «massstäblich» mit geometrischem Vergleich). Fehler und Hinweise erscheinen als Streifen oben (✕), Erfolge als Einblendung.
**Quelle ansehen:** Befund (Datei, Art, Stelle, Wert, Box) mit `unterlage_bild` im Ausschnitt der Box plus Rand (so gross wie der Befund, PDF/Bild mindestens 80, sonst 10 Einheiten); der Befund ist im Bild markiert; auf Wunsch die ganze Seite.
**Befund-Browser** (Register «Unterlagen»): Unterlagen der Familie (Definition) und «weitere im Projekt»; Filter Seite, Art, Text; Liste und Seitenbild mit allen Boxen (Klick ins Bild wählt den kleinsten Befund darunter, Zoom −/＋/↔); **«als Quelle übernehmen»**: die zuletzt im Typkatalog (oder in der Parametertabelle) markierte Zelle bekommt die Befund-Id in `quellen` (`familie_aendern typ_setzen` bzw. `parameter_setzen`), auf Wunsch auch den Wert des Befunds; der Kern prüft, ob beides zusammenpasst. «＋ Unterlage hinzufügen …» legt eine weitere Unterlage ab.

**Internet-Recherche (Oberfläche):** Schalter im Dialog; Liste «Aus dem Internet abgerufen» (Kennzeichen, URL als Link, Art, Grösse, Zeit) im Ergebnis des Auftrags und in der Prüfansicht; bei Quellen aus dem Internet steht die URL in der Abdeckung und bei «Quelle ansehen». Unterlagen der Art `html` haben kein Seitenbild: «Quelle ansehen» und der Befund-Browser zeigen den Befundtext samt Tabellenzelle und den Link. Eine Unterlage gilt als «aus dem Internet», wenn ihr `original` eine http(s)-Adresse ist.

**Annahmen** (Prüfliste): (1) Die Gründe der gesperrten Freigabe entsprechen `FamilienFreigabe.Pruefen`; der Kern entscheidet bei der Freigabe noch einmal. (2) Rand um den Befund im «Quelle ansehen» siehe oben. (3) Die Unterlage wird **nicht** mit `bezuege.familie` an die Familie gebunden (siehe Befund 1); die Zuordnung ergibt sich aus `definition.unterlagen`, nicht verwendete Unterlagen stehen im Befund-Browser unter «weitere im Projekt». (4) Grösse und Zeit einer Unterlage aus dem Internet liest die Oberfläche aus `werte.bytes` bzw. `werte.groesse` und `werte.abgerufen`; fehlen sie, zeigt die Liste «–». (5) Passpunkt-Klicks in der Familienansicht rasten auf Eckpunkte der Plangrafik (12 Pixel), die Zahlen sind gerundet (0.1 mm bzw. 0.01 Einheiten). (6) Ein Entscheid lässt sich ändern, solange nichts anderes dagegen spricht (der Kern erlaubt es). (7) «Wert übernehmen» im Befund-Browser setzt nur Zahlen bzw. Texte ohne Einheitenumrechnung; Abweichungen meldet der Kern.

**Befunde am Kern:** (1) `unterlage_einfuegen` mit `bezuege.familie` einer **bestehenden** Familie scheitert («UL-1 (Unterlage) kann keine Familie tragen»): `FamilienNachfuehrung.Element` hält jedes Element mit `bezuege.familie` für ein platziertes Familienelement; die B2-Tests prüfen nur Fehlerfälle. Vorschlag: Elemente der Klasse Unterlage dort überspringen. (2) Eine KI-Änderung an einer vom Menschen (Editor) reservierten Familie lehnt der Kern ab; der Lauf endet trotzdem «fertig» ohne Familie – nur das Protokoll nennt den Grund (die Oberfläche weist im Dialog darauf hin). (3) `unterlagen_lesen` meldet bei DWG/DXF `seiten` (Zahl der Layouts); die Oberfläche zeigt Seiten nur für PDF. (4) `familie_entwurf` mit gleicher Definition ändert nichts: das Ergebnis des Auftrags nennt dann keine Familie. (5) Das Ereignis «auftrag» trägt den Bericht nicht (`Zustand(a, false)`); die Oberfläche holt ihn mit `GET /api/auftraege/{id}`. (6) Die Katalogklasse Unterlage kennt `abgerufen` und die Art `html` noch nicht (Stand dieser Arbeit; B2 baut sie).

**Prüfen:** `node --test js/familien/*.test.mjs` und alle bisherigen `*.test.mjs`; Browser (Edge headless über CDP, der Test startet **seinen eigenen Server mit der Attrappe statt claude.exe**, Port 5148, Build-Ordner `%USERPROFILE%\HLKS_CAD_build\c2m2`, `HLKS_BAU` ändert ihn):
`SHOTS=<Ordner> node js/familien/familie-unterlagen.browser.mjs`. Synthetische Unterlagen: `tests/daten/unterlagen/datenblatt_testventil.pdf`, `massbild_testventil.dxf` (Erzeuger: `tests/daten/unterlagen/erzeuger`, C#) und `massbild_testventil.png` (`erzeuger/png-erzeugen.mjs`). Die Attrappe (`tests/Hlks.ClaudeAttrappe`) legt auf Wunsch eine mitgegebene Definition an (Umgebung `HLKS_ATTRAPPE_DEFINITION`, Datei wird erst beim Lauf gelesen; `[attrappe:familie=FAM-n]` im Hinweis ersetzt eine Familie). Der Server braucht für die Attrappe-`.exe` `DOTNET_ROOT`, dazu `HLKS_CLAUDE_EXE` und `HLKS_MCP_DLL`. Der Test schreibt `kataloge/familien/test_c2m2_testventil.familie.json` und löscht die Datei (und den Ordner, wenn er ihn angelegt hat) am Ende.

## Familienbibliothek (Modul 2, C3)

Oberfläche für die projektübergreifende Familienbibliothek (`entwurf/modul-2-bibliothek.md`, Kern: Nachtrag B7). **Die Oberfläche entscheidet und rechnet nichts:** Stand (`stand.art`), Version, Vorschau einer Übernahme, Doppel und was «sichtbar» ist liefert der Kern (Abfrage `familien_bibliothek`, Feld `bibliothekStand` der Abfrage `familien`); gelesen wird nie eine Datei. Geändert wird nur über `familie_laden`, `familie_aktualisieren`, `familie_in_bibliothek` und `familienauswahl_setzen` – jeder Dialog zeigt zuerst den Trockenlauf, Fehler des Kerns stehen mit Vorschlag im Dialog. Die einzige Spiegelung einer Kernregel ist `sichtbarNach()` (Vorschau der Häkchen im Dialog, solange die Auswahl nicht gespeichert ist); `bibliothek.browser.mjs` vergleicht sie für acht Auswahlen mit dem Feld `sichtbar` des Kerns.

| Datei | Inhalt |
| --- | --- |
| `js/familien/bibliothek-text.js` | reine Texte, HTML und Parameter (Node getestet): Marken und Knoten, Auswahl (lesen, schreiben, Filter, Einzelhäkchen, Zahlen), Wege und Versionswahl von «Aktualisieren …», Vorschau-HTML, Liste «Familie laden», Statusleiste |
| `js/familien/bibliothek-daten.js` | Antwort von `familien_bibliothek` einmal geholt und geteilt (Projektbrowser, Dialoge, Statusleiste); neu bei Änderungen an Familien, beim Zurückkehren ins Fenster und alle 15 s; merkt die zuletzt gesehene Nachholung der Warteschlange |
| `js/familien/bibliothek-dialoge.js` | Dialoge «Familien im Explorer …», «Aktualisieren …», «Familie laden» und der Hinweis zur Bibliothek |
| `js/familien/aktionen-bibliothek.js` | Aktionen `familie.explorer` (Register Gebäudetechnik › Apparate und Armaturen, Kontextregister Familie › Bibliothek, Kontextmenü und Knopf ⚙ am Knoten «Familien»), `familie.aktualisieren`, `familie.bibliothek-laden`, `familie.bibliothek-platzieren` |
| `js/familien/browser-knoten.js` | Baumknoten: Projektfamilien mit Marken nach `bibliothekStand`, Bibliotheksfamilien (grau), Knoten «Familien» |
| `css/familien-bibliothek.css` | Marken, Dialoge, Statusleiste (Variablen `--bib-…`, hell und dunkel) |

**Projektbrowser, Knoten «Familien».** Je Kategorie die Familien des Projekts (immer) und die Bibliotheksfamilien, die der Kern nach `familienAuswahl` des Projekts als `sichtbar` meldet und die noch nicht im Projekt sind: **grau, kursiv, Marke «Bibliothek»** (Entwürfe zusätzlich «Entwurf», mögliche Doppel «Doppel?»); sie haben keine Typen im Baum. Marken an Projektfamilien nach `bibliothekStand`: **«neuere Version»** und **«parallel geändert»** (dazu der Knopf ↻ «Aktualisieren …»; das Kontextmenü «Aktualisieren …» gilt auch bei «noch nicht gespeichert»), **«noch nicht gespeichert»** (Fassung nicht in der Bibliothek, meist Warteschlange). Marken und Knöpfe stehen in einem Rahmen (`.pb-zusatz`): reicht die Breite des Browsers nicht, rutscht er in eine zweite Zeile, der Name bleibt lesbar (Browser-Tests klicken Zeilen deshalb in der ersten Zeile). **Doppelklick auf eine Bibliotheksfamilie** (bzw. Kontextmenü «Platzieren») lädt sie mit `familie_laden { id | datei }` und startet dann das gewohnte Platzieren (Typauswahl in der Optionsleiste); «In das Projekt laden» lädt nur. Knoten «Familien»: Knöpfe «＋ Unterlagen» und ⚙ (Explorer), Kontextmenü «Familien im Explorer …».

**Dialog «Familien im Explorer …».** Schalter «Familien der Bibliothek im Explorer zeigen» (aus = `auswahl: null`, nur die Familien des Projekts), Filter als Schaltflächen (Gewerk, Kategorie, Hersteller, Status; kein Wert gewählt = keine Einschränkung, alle gewählten Filter müssen treffen), Liste **aller** Bibliotheksfamilien beider Quellen (Bibliothek und mitgeliefert) mit Häkchen; ein einzelnes Häkchen übersteuert die Filter (`ein`/`aus`; passt der Wunsch wieder zum Filter, entfällt der Eintrag), Familien des Projekts sind fest angehakt («im Projekt – immer sichtbar»). Fuss: «n von m Bibliotheksfamilien sichtbar», der Hinweis **«Gilt für alle, die an diesem Projekt arbeiten.»**, die Antwort des Kerns auf den Trockenlauf von `familienauswahl_setzen` (bei Ablehnung rot) und die **Doppel-Hinweise** des Kerns. Die Suche über der Liste ändert nur die Anzeige. «Speichern»: Trockenlauf, dann `familienauswahl_setzen`.

**Dialog «Aktualisieren …».** Erklärung zum Stand, Wahl der Fassung (neuere Versionen und offene parallele Fassungen anderer Projekte; Vorgabe die aktuelle Version), **Vorschau** aus dem Trockenlauf von `familie_aktualisieren` (erste Zeile = Zusammenfassung) und der strukturierten Vorschau aus `familien_bibliothek { familie, version }` (Änderungen an Kopf, Parametern, Typen, Körpern, Anschlüssen; betroffene Elemente mit «Anschluss rl verschiebt sich um 12 mm»; entfallende Typen). **Lehnt der Kern ab** (ein benutzter Typ entfällt), stehen Fehler und Vorschlag rot im Dialog, darunter die Vorschau «Was sich geändert hätte»; «Aktualisieren» ist gesperrt. Bei **«parallel geändert»** zwei Wege: **«Bibliothek übernehmen»** (`familie_aktualisieren`; die eigene Fassung bleibt als Abzweig erhalten und gilt als verworfen) oder **«Meine Fassung wird aktuell»** (`familie_in_bibliothek` mit `als_aktuell`, nur ein Mensch; deutlicher Hinweis, dass die Fassung der Bibliothek erhalten bleibt, aber nicht mehr aktuell ist). Bei «noch nicht gespeichert»: Bibliothek übernehmen (die eigene Fassung wird ersetzt) oder die eigene Fassung schreiben. Übernommen wird nie automatisch.

**«Familie laden».** Liste aus `familien_bibliothek` (Bibliothek vor mitgeliefert, mit Version; Familien im Projekt als «wird aktualisiert» gekennzeichnet), Versionswahl, freies Feld für Datei bzw. vollen Pfad; Trockenlauf von `familie_laden` in der Vorschau. Der Vorschlag des Kerns zu einer unbekannten Datei wird nicht mehr gelesen.

**Statusleiste.** «Bibliothek erreichbar» (grün), «Bibliothek offline» (rot), «· n in Warteschlange» (orange bzw. rot), «Bibliothek gesperrt» (Testlauf ohne `HLKS_FAMILIENBIBLIOTHEK`). Klick öffnet den Hinweis mit Pfad, Quelle, Zustand, Stand der Liste, Warteschlange und «nachgeholt» (der Kern meldet das nur in der Antwort, die die Warteschlange nachgeschrieben hat; die Oberfläche merkt es); «Jetzt prüfen» holt neu und stösst das Nachholen an. Ohne NAS bleibt alles im Projekt möglich, der Kern meldet die Warteschlange.

**KI.** Der KI-Lauf (`AuftragDienst.ErlaubteWerkzeuge`) darf `familien_bibliothek` lesen, aber weder `familie_aktualisieren` noch `familie_in_bibliothek` noch `familienauswahl_setzen`: Versionen fremder Projekte übernehmen und die Auswahl je Projekt entscheidet ein Mensch. **`familie_speichern` mit relativer `datei`** lehnt der Kern ab (Entscheid Hauptlauf 19.10.2026: die mitgelieferte Bibliothek `kataloge/familien` ist nur lesbar); ohne `datei` geht die Familie in die Familienbibliothek, mit vollem Pfad als Ausfuhr in eine Datei (Prüfansicht, «In Bibliothek speichern»).

**Prüfen:** `node --test js/familien/*.test.mjs` (`bibliothek.test.mjs`: 20 Tests) und alle bisherigen `*.test.mjs`; Kern: `FamiliePruefungFreigabeTests` (relative Datei), Web: `AuftragVorlageTests` (Werkzeugliste der KI); Browser (Edge headless über CDP, der Test startet **seinen eigenen Server (Port 5166) und ein zweites Projekt über die Kommandozeile**, Build-Ordner `%USERPROFILE%\HLKS_CAD_build\c3`, `HLKS_BAU` ändert ihn): `SHOTS=<Ordner> node js/familien/bibliothek.browser.mjs` (Bilder `c3_*.png`, Vorgabe `%TEMP%\HLKS_CAD`). Projekt A (Kommandozeile, Akteur Anna) legt fünf fiktive Familien an und ändert sie, Projekt B (Browser) zeigt sie im Explorer, lädt und platziert, meldet «neuere Version», aktualisiert mit Vorschau, lehnt eine Übernahme mit entfallendem Typ ab, entscheidet beide Wege bei «parallel geändert» und arbeitet ohne Bibliothek weiter (Ordner unter `%TEMP%` umbenannt, Warteschlange, nachgeholt). **Der Test setzt `HLKS_FAMILIENBIBLIOTHEK`, `HLKS_BIBLIOTHEK_AUSSTEHEND` (beide unter `%TEMP%`) und `HLKS_TEST=1` und prüft am Ende, dass unter `W:\03_CAD Bibliotheken` kein Ordner «d_KICAD Familien» entstanden ist.** Alle Browser-Tests, die einen Server starten oder gegen einen starten, müssen diese Umgebung setzen (sonst gälte die Bibliothek des Büros); `familie-unterlagen.browser.mjs` tut es seit C3.

## Schema (Modul 3, C2)

Oberfläche der Schema-Ansicht (`entwurf/modul-3.md`, Grundsatzentscheid 6 und «Entscheide während des Baus»: Schemalagen, Schemaknoten). **Das Schema ist eine Sicht auf dasselbe Modell:** die Anordnung rechnet der Kern bei jedem Lesen
(Abfrage `schema` und `GET /api/plangrafik/{kz}` einer Ansicht mit `art = schema`, Papier-mm, Massstab 1, y nach oben, jedes Teil mit seiner Element-Id); gespeichert werden nur die Hand-Lagen (`Ansicht.lagen`). Die Oberfläche
rechnet nichts: Zeichnen, Zoomen, Auswählen, Klicks deuten, Befehle aufbauen – ob ein Verbinden, Trennen oder Anlegen gilt, sagt der Kern (Ablehnungen mit Vorschlag erscheinen als Meldung). Dateien:

| Datei | Inhalt |
| --- | --- |
| `js/ansichten/schema.js` | die Ansicht (SVG, `planSvg` aus `plangrafik.js`): Daten holen, Zoom und Verschieben wie im Grundriss, Auswahl, Ziehen, Kontextmenü, Leiste, Banner, Werkzeug-Überlagerung |
| `js/ansichten/schema-logik.js` | reine Logik (Node getestet): Rundung und Einrasten, Treffer auf Knoten und Linien, Zelle unter dem Zeiger, Parameter aller Befehle, Trennoptionen, einfügbare Klassen und Familien, Einträge der Kontextmenüs, Grundriss zum Zeigen |
| `js/ansichten/schema-text.js` | reine Texte und HTML (Banner, Tooltips, Listen der Dialoge «Element einfügen» und «Anschlüsse wählen», Rückfrage «Neu anordnen») |
| `js/oberflaeche/schema-bearbeiten.js` | Abläufe mit Dialogen und Befehlen: Lage setzen und zurücksetzen, Neu anordnen, Verbinden (mit Auswahl der Anschlüsse), Trennen, Element einfügen |
| `js/oberflaeche/aktionen-schema.js` | Aktionen: Schema anlegen, Werkzeuge, Schema ↔ Grundriss, Kontextmenü im Grundriss und 3D |
| `css/schema.css` | Stil (nur Variablen aus `hlks.css`, hell und dunkel) |
| `schema.test.mjs`, `schema.browser.mjs` | Tests |

**Zeichnen.** Die Plangrafik des Kerns geht durch denselben Zeichner wie die Plan-Ansichten (Klassen `gr-svg`, `pl`, `pf`, `sd/sm/sk`; Zoom mit dem Rad an der Mausposition, Verschieben mit mittlerer Taste, Leertaste+Ziehen oder Ziehen auf leerer Fläche,
Doppelklick mittlere Taste = alles, «Alles zeigen» in der Leiste, ZF). **Kein Massstab und kein Massstabsbalken** (ein Schema ist nicht massstäblich); die Steuerleiste unten zeigt «Schema, ohne Massstab» und nur den Detailgrad (ändert die Ansicht per Befehl).
Nach jedem Ereignis «modell» holt die Ansicht Plangrafik und Abfrage neu (entprellt 150 ms; stimmt der `stand` beider Antworten nicht überein, wird nochmals geholt); Zoom und Auswahl bleiben, fremde Änderungen leuchten kurz auf. **Nicht platzierte Elemente** zeichnet der Kern
(gestrichelter Rahmen am Knoten, punktierte Überlagerung an Linienteilen, nur Bildschirm); dazu zeigt das **Banner** oben (unter der Leiste) die **Hinweise des Kerns** («n Hinweise» + erster Hinweis, Klick klappt die Liste auf) und das Feld rechts in der Leiste «n nicht platziert · n von Hand angeordnet».
**Hand-Lagen** tragen am Symbol einen kleinen Punkt in der Akzentfarbe. Der Ansicht-Eintrag im Projektbrowser steht in der Gruppe «Schemas» (war schon so).

**Auswahl und Palette.** Klick wählt: auf einem Knoten (Trefferfläche ±4.5 × ±3.5 mm, mindestens 5 Bildschirmpixel) das Element, auf einer Linie das **angeklickte Teilelement** (Leitung, Bogen, Reduktion; Teile mit Id), auf Geschosslinie oder Strangkopf Geschoss bzw. Strang, auf leerer Fläche hebt er die
Auswahl auf. Die Auswahl ist die gemeinsame (`auswaehlen`): Eigenschaftenpalette, Kontextregister «Ändern | Klasse» und alle anderen Ansichten folgen. Die Hover-Tipps nennen Kennzeichen, Klasse, Strang, Geschoss, «nicht platziert», «Hand-Lage» (Linien: Teil, Grössen, Medium).

**Knoten verschieben.** Ziehen eines Knotens (nach 4 Pixeln Bewegung) zeigt den Knoten am Zeiger mit Koordinaten und Fanglinien; beim Loslassen geht `schema_lage { ansicht, element, x, y }` an den Kern (Papier-mm, auf 0.5 mm gerundet; rastet innerhalb von 8 Bildschirmpixeln auf die Stranglinie eines senkrechten Strangs und auf die x- bzw. y-Achse
anderer Knoten ein). Die **Linien folgen erst nach der Antwort** (der Kern führt sie neu, die Ansicht zeichnet neu); wird abgelehnt, springt der Knoten zurück und die Meldung des Kerns steht mit Vorschlag da. Rückgängig mit Strg+Z (ein Paket je Verschieben). Kontextmenü **«Automatische Lage»** = `schema_lage { zuruecksetzen: true }`
(ausgegraut ohne Hand-Lage, mit Hinweis). **«Neu anordnen»** (Leiste, Register Ansicht › Schema, Kontextmenü auf leerer Fläche) fragt «Alle Hand-Lagen entfernen?» mit den Wahlen **Alle Hand-Lagen entfernen** (`schema_anordnen { alle: true }`), **Nur Verwaiste aufräumen** (ohne `alle`) und Abbrechen; ohne Hand-Lagen nur ein Hinweis.

**Werkzeuge.** Leiste oben links in der Ansicht und – als dieselben Aktionen – Register **Ansicht › Gruppe «Schema»** (Verbinden, Trennen, Element einfügen, Neu anordnen; frei, solange eine Schema-Ansicht aktiv ist). Ein Werkzeug ist ein Modus der Ansicht (Knopf gedrückt, Hinweis in der Statusleiste, Fadenkreuz), **Esc oder ein Wechsel der aktiven Ansicht beendet ihn**.
«Ändern» ist der Normalmodus.
- **Verbinden:** zwei Knoten nacheinander anklicken (Gummiband vom ersten, derselbe Knoten nochmals nimmt die Wahl zurück) → `verbinden { von: { element }, nach: { element } }` **ohne Anschluss** – der Kern wählt den freien, passenden. Es gehen immer Objekte `{ element, anschluss? }` mit der Element-Id (die Textform «KZ.anschluss» des Kerns trennt am letzten Punkt und
  zerlegt Kennzeichen wie «WC-E.02»). Gibt es mehrere passende Paare (Ablehnung «Mehrere passende Anschlusspaare zwischen A und B: ein–ein, ein–aus, …»), öffnet ein kleiner Dialog **«Anschlüsse wählen»** mit den Kandidaten des Kerns (gelingt das Lesen nicht: zwei Listen mit den freien Anschlüssen aus `element_lesen`) und sendet
  `verbinden` mit den gewählten Anschlussnamen. Jede andere Ablehnung (Medium, Fliessrichtung, Lage, Grösse, «Kein freies, passendes Anschlusspaar …») erscheint als Meldung mit Text und Vorschlag des Kerns; das Werkzeug bleibt für den nächsten Versuch aktiv.
- **Trennen:** Linie oder Knoten anklicken → `trennen { verbindung: <Id> }`. Eine Linie ohne Teil (zwei direkt verbundene Knoten, im Plan ohne Id) trifft die Ansicht nach der Geometrie (5 Bildschirmpixel). Hat die Linie Teile, gelten die Verbindungen, die das angeklickte Teil berühren (davor, danach; `trennOptionen` aus `zustand.verbindungen`):
  eine wird sofort getrennt, mehrere zeigt ein Menü «Trennen: TS-2.c ↔ R-2.anfang». Auf einem Knoten: seine Verbindungen. Dieselben Einträge stehen im Kontextmenü (Rechtsklick auf Knoten oder Linie).
- **Element einfügen:** Dialog mit Suchfeld und den **Klassen** (Endgeräte: Rolle verbraucher; Geräte: quelle, senke, zentrale; Armaturen und Formteile ohne Bogen und Reduktion, weil der Kern sie als Teil einer Linie zeichnet; die Rolle liest die Oberfläche je Klasse aus `katalog { klasse }`, die Kurzform kennt sie nicht) und den gültigen **Familien** mit Typen («Name: Typ»), dazu
  optional Kennzeichen und Name. Danach in die **Zelle** (Spalte × Zeile, hervorgehoben; Status zeigt «Geschoss EG × Strang KW-S2») klicken: `element_anlegen` **ohne Geometrie** mit `geschoss` (Kennzeichen der Zeile) und `teilVon` (Strang der Spalte; Zone «ohne Strang»: `system`; Zone «ohne Geschoss»/«ohne System»: nichts), bei einer Familie mit
  `bezuege.familie` und `eigenschaften.familientyp` (`familie_platzieren` verlangt einen Punkt und scheidet aus), danach `schema_lage` an die Klickstelle (gerundet, mit Einrasten). Zwei Pakete, rückgängig in zwei Schritten. Das neue Element ist gewählt, steht nicht platziert im Schema (Rahmen, Banner, P17 in der Prüfung).

**Schema ↔ Grundriss.**
- **«Im Grundriss zeigen»** (Kontextmenü auf Knoten und Linienteil, Kontextregister «Ändern | Klasse», Aktion `schema.im-grundriss-zeigen`): öffnet bzw. holt den Grundriss des Geschosses nach vorn (gleiches Geschoss, keine Vorlage; offene und solche, die das Gewerk zeigen, zuerst; blendet er das Element aus, sagt es die Meldung), zoomt auf das Element (Punkt ± 1.5 m, sonst Begrenzung der Geometrie; `zoomAuf` in `grundriss.js`) und wählt es.
  **Nicht platzierte Elemente: ausgegraut mit Hinweis.** Ohne Grundriss des Geschosses eine Meldung mit Vorschlag.
- **«Im Grundriss platzieren …»** (nur bei nicht platzierten Elementen): öffnet den Grundriss und startet das Werkzeug «Im Grundriss platzieren» (Klick setzt den Punkt, mit Fangen und Raster) → `element_aendern { element, geometrie: { art: punkt, punkte: [[x, y]] } }` (z folgt der Kote des Geschosses; auch für Familienelemente, der Kern führt sie nach). Elemente, deren Klasse keine Punktgeometrie hat (**Leitungen**), bekommen nur den Hinweis,
  dass sie im Grundriss mit dem Leitungswerkzeug (Teil C1, folgt) gezeichnet werden.
- **«Im Schema zeigen»** (Rechtsklick im Grundriss auf ein Netzelement – gewählt wird das Element unter dem Zeiger –, Rechtsklick im 3D für das gewählte Element, Kontextregister «Ändern | Klasse»): der Kern wird je Schema-Ansicht gefragt (`schema`); das Menü zeigt den Eintrag, sobald eine Schema-Ansicht das Element zeigt (mehrere: je Ansicht einer), sonst
  ausgegraut mit Hinweis; auf Wand, Raum usw. gibt es kein Menü. Die Schema-Ansicht öffnet, wählt das Element und zoomt darauf. Im 3D verschiebt die rechte Taste die Ansicht: das Menü öffnet nur nach einem Klick ohne Bewegung.

**Schema anlegen** (Register Ansicht › Erstellen › **«Schema»**): Dialog mit Name, Kennzeichen (Vorschlag AN-SCHEMA bzw. AN-SCHEMA-<System>), **Gewerken** und **Systemen** (Mehrfachwahl; nichts gewählt = alle) → `element_anlegen { klasse: Ansicht, eigenschaften: { art: schema, schemaart: strangschema, detailgrad, gewerke?, systeme? } }`; die neue Ansicht öffnet.

**Schema im Planblatt** (Kern, `src/Hlks.Kern/Plan/`): `ansicht_platzieren` nimmt jetzt auch `art = schema` (die Aktion «Ansicht platzieren» listet sie). Ein Schema steht in Papier-mm, 1:1: im **Ansichtstitel kein Massstab**, **kein grafischer Massstab**, im **Plankopf** zählt es beim Massstab und bei den Masseinheiten nicht mit; steht nur Schema auf dem Blatt, lautet der Massstab «ohne» und der
Masshinweis «Schema, ohne Masse». Gemischte Blätter zeigen den Massstab der Grundrisse wie bisher.

**Annahmen** (Prüfliste Christian, Anzeige und Bedienung, keine Normwerte): (1) Trefferfläche eines Knotens ±4.5 × ±3.5 mm, Hand-Lagen auf 0.5 mm gerundet, Einrasten innerhalb von 8 Bildschirmpixeln. (2) Beim Einfügen bekommen **auch Endgeräte und Geräte `teilVon` = Strang der Spalte** (der Kern übernimmt das System und stellt sie in diese Spalte); mit `system` allein
entstünde eine Zone «ohne Strang», die das ganze Raster verschiebt, während die Hand-Lagen absolut bleiben. (3) Ein Werkzeug endet beim Wechsel der Ansicht. (4) «Im Grundriss zeigen» wählt den Grundriss nach Geschoss, dann offene und sichtbare zuerst; Ausschnitt Punkt ± 1.5 m. (5) Plankopf eines reinen Schema-Blatts: Massstab «ohne», Masse «Schema, ohne Masse». (6) Ein Rechtsklick im 3D meint das gewählte Element.

**Befunde ausserhalb dieses Auftrags:** (1) `familie_platzieren` verlangt einen Punkt und legt immer neu an – ein Familienelement ohne Geometrie entsteht nur über `element_anlegen` (Oberfläche tut das), nicht platzierte Elemente platziert die Oberfläche mit `element_aendern`. (2) `Param.Punkt` (verbinden, trennen, leitung_ziehen) trennt «KZ.anschluss» am **letzten Punkt**: «WC-E.02» wird zu Element «WC-E» und Anschluss «02»; die Oberfläche sendet darum immer Objekte. Vorschlag: zuerst
das ganze Kennzeichen prüfen. (3) Die Mehrdeutigkeit beim Verbinden steht nur im Fehlertext («Mehrere passende Anschlusspaare …: a–b, …»); die Oberfläche liest ihn (Test gegen `Befehle.cs`) – ein strukturiertes Feld wäre robuster. (4) Der Kurzkatalog (`katalog`) hat die Rolle im Netz nicht; die Oberfläche holt sie je Klasse (rund 35 Abfragen beim ersten Einfügen). (5) Kleine Ergänzungen in Dateien anderer
Zuständiger: `grundriss.js` (`zoomAuf`), `fenster.js` (Art schema, Steuerleiste), `aktionen.js` (ein Import), `aktionen-plaene.js` (Schema platzierbar), `werkzeuge/symbole.js` (vier Symbole), `index.html` (`schema.css`), `platzhalter.js`, `src/Hlks.Mcp/WerkzeugePlan.cs` (Beschreibung von `ansicht_platzieren`).

**Prüfen:** `node --test js/ansichten/schema.test.mjs` (mit `HLKS_TEST_SERVER` zusätzlich gegen den echten Kern, nur lesend und Trockenläufe) und alle bisherigen `*.test.mjs`; Kern: `SchemaPlanblattTests.cs`; Browser (Edge headless über CDP, der Test startet **seinen eigenen Server** mit frischem Beispielprojekt, Port 5162, Build-Ordner `%USERPROFILE%\HLKS_CAD_build\m3c2`, `HLKS_BAU` ändert ihn):
`SHOTS=<Ordner> node js/ansichten/schema.browser.mjs` (öffnet AN-SCHEMA-SAN, wählt, zieht, fügt ein, verbindet mit Anschlusswahl, trennt, wechselt zwischen Schema, Grundriss und 3D, legt ein Schema an, platziert es auf einem Planblatt; Bilder hell und dunkel).

## Leitungen zeichnen (Modul 3, C1)

Oberfläche für Rohr und Kanal auf dem Routing des Kerns (`entwurf/modul-3.md`, Grundsatzentscheide 1–5 und «Entscheide während des Baus»). **Das Werkzeug rechnet nichts Fachliches:** Geraden, Bögen, Reduktionen, T-Stücke, Einbaulängen, Winkel der Serie und Verbindungen entscheidet der Kern
(`leitung_zeichnen`, `leitung_abzweigen`, `leitung_aufteilen`, `leitung_groesse`, `leitung_serie`; Abfragen `leitungsserie`, `anschlusslagen`, `leitungszug`, `materialliste`). Die Oberfläche sammelt Punkte, hilft beim Fangen (Winkel, Anschlüsse) und zeigt, was der Kern meldet (Trockenlauf, Ablehnung mit Vorschlag).
Reine Logik und Texte stehen in Dateien ohne Browser-Abhängigkeit (in Node getestet), die Bedienung in den übrigen.

| Datei | Inhalt |
| --- | --- |
| `js/werkzeuge/leitung-logik.js` | rein: Winkelfang nach den erlaubten Winkeln der Serie, Höhenwechsel, Anschlüsse (Text, Richtung, Fangpunkte), Fliessrichtung (Umdrehen), Parameter von `leitung_zeichnen`/`leitung_abzweigen`/`leitung_groesse`/`leitung_serie`/`leitung_aufteilen`, Abzweig-Treffer, Zustand der Optionsleiste, Texte der Vorschau-Zeile aus den Hinweisen des Kerns |
| `js/werkzeuge/leitung-daten.js` | Zwischenspeicher: Antworten von `leitungsserie` und `anschlusslagen` (leeren sich bei Änderungen am Modell), Haken zum laufenden Werkzeug |
| `js/werkzeuge/leitung.js` | das Werkzeug (`leitungWerkzeug('Rohr' | 'Kanal')`): Klicks, Gummiband, Vorschau-Zeile, Anschlussfang, Abzweig, Tasten H, Rückschritt, Esc |
| `js/werkzeuge/leitung-palette.js` | rein: Gruppen der Eigenschaftenpalette (Leitung, Altbestand, Formteil) |
| `js/werkzeuge/leitung-bearbeiten.js` | Abläufe mit Dialogen: Grösse bzw. Serie ändern (Rückfrage «nur diese Leitung / ganzer Zug»), «Aufteilen mit Serie …» |
| `js/oberflaeche/aktionen-leitung.js` | Aktionen: Rohr (PI), Kanal (DT), Aufteilen mit Serie (Kontextregister), Materialliste |
| Ergänzungen in `optionen.js`, `optionsleiste.js` (Felder `hinweis`, `knopf`, `sichtbar`), `fangen.js` (Fangart `anschluss`), `griffe.js`, `eigenschaften.js`, `aktionen.js`, `projektbrowser.js`, `css/oberflaeche.css` | siehe unten |
| `leitung.test.mjs`, `leitung.browser.mjs` | Tests |

**Werkzeuge.** Register Gebäudetechnik › Gruppe «Leitungen»: **Rohr (PI)** und **Kanal (DT)**, nur im Grundriss (die Platzhalter sind ersetzt; Formteil bleibt gesperrt, die Formteile setzt der Kern). Bedienung wie Revit:
- **Punkte anklicken** in Fliessrichtung (Fangen wie bei der Wand, Umschalt = orthogonal, Raster). Das Gummiband zeigt Band in der Aussenbreite der gewählten Grösse (Rechteckkanal: Breite), Länge und Winkel; senkrechte Stücke erscheinen als Ring.
- **Zifferneingabe:** Länge in mm, Enter setzt den Punkt (in der gefangenen Richtung, auch Winkelfang).
- **Winkelfang** (Optionsleiste «Winkelfang», vorgabemässig an): die Richtung rastet innerhalb von 3° ein auf die Achsen und auf die Richtung der letzten waagrechten Geraden ± die **erlaubten Winkel der Serie** (`leitungsserie.winkel`; ohne Liste ±45° und ±90°), ohne Kehrtwende. Starke Fänge (Endpunkt, Schnittpunkt, Mitte, Lot), Umschalt und Anschlüsse gehen vor. Die Länge bleibt (aufs Raster gerundet).
  Ob ein Winkel wirklich gilt, sagt der Kern: ein Knick, den kein Fang trifft, kommt als Ablehnung mit Vorschlag zurück.
- **Höhenwechsel (Steigleitung):** Feld «Höhe» in der Optionsleiste (Achse über OK Geschoss in mm; **Pflichtfeld ohne Vorgabe**, z = Kote des Geschosses + Höhe). Ändert man es, nachdem schon ein Punkt gesetzt ist (Eingabe + Enter), hängt das Werkzeug einen Punkt **senkrecht auf die neue Höhe** an – ab da gilt die neue Höhe.
  Taste **H** springt in das Feld. Der Kern setzt am Übergang einen Steigbogen.
- **Abschluss:** Enter oder Doppelklick. Zuerst der **Trockenlauf des Kerns**, die Vorschau-Zeile oben in der Ansicht zeigt «2 Geraden, 3900 mm · 1 Bogen 90°» (▾ klappt die Einzelheiten auf: Gerade mit Länge, Formteil mit Typ und Winkel, weitere Hinweise des Kerns); **Enter oder «Ausführen»** führt aus (ein Paket, Strg+Z macht es rückgängig), Esc oder «Weiter zeichnen» geht zurück.
  **Abgelehnt** (z. B. «Knick von 60.26° – die Serie hat Bögen mit 15°, 30°, 45°, 90°», «Gerade kürzer als die Einbaulängen») steht der Text des Kerns rot in der Zeile, darunter sein Vorschlag; «Neu prüfen» (Enter) rechnet nach einer Korrektur erneut.
- **Esc** verwirft zuerst die laufende Leitung (bzw. die Vorschau), erst ein zweites Esc beendet das Werkzeug; **Rückschritt** nimmt den letzten Punkt zurück; Rechtsklick beendet. Nach dem Ausführen bleibt das Werkzeug für die nächste Leitung aktiv.

**Optionsleiste** (je Werkzeug eigene Werte, `optionen.js`): **Serie** (nur Familien der Kategorie Rohr bzw. Kanal, gültig und mit Typen; die einzige Serie wird vorgewählt), **Grösse** (die Typen der Serie, aus `leitungsserie`), **Höhe**, **Gefälle** %, **System** und **Strang** (aus dem Modell; die Stränge werden aufs System eingeengt, ein Strang setzt sein System), **Dämmdicke** mm (→ `eigenschaften.daemmdicke`), **Winkelfang**, Raster.
Gemerkte Werte, die nicht mehr stimmen (Serie weg, Grösse nicht in der Serie, System aus einem anderen Projekt), werden beim Start geleert bzw. ersetzt. **Ohne Serie der Kategorie** zeigt die Leiste «Keine Leitungsserie der Kategorie Rohr im Projekt.» und den Knopf **«Leitungsserie laden …»** (öffnet «Familie laden»; ein Klick ins Leere sagt dasselbe als Meldung). Gefälle und Dämmdicke werden nicht über Sitzungen gemerkt.

**Anschlussfang in 3D-Lage.** Beim Bewegen liest das Werkzeug `anschlusslagen` der Elemente des Geschosses in der Nähe (3 m um den Mauszeiger, Zwischenspeicher je Element) und zeigt deren Anschlüsse mit Lage als **Marken** (offen = frei, gestrichelt grau = belegt; im Umkreis von 50 px). Der gefangene Anschluss (Radius 10 px, **geht allen anderen Fängen vor**) trägt die Beschriftung
«HK-1.rl · HZ-RL · raus · DN 15 · frei» bzw. «… · belegt (RO-5.anfang)». Fangpunkte ohne Lage (Geräte ohne Familie) gibt es nicht.
- **Start auf einem Anschluss:** `von` = `{ element, anschluss }` (Kennzeichen, nie die Textform «KZ.anschluss»), die **Höhe des Anschlusses** wird übernommen (Feld «Höhe»). Zeigt die Achse senkrecht (Heizkörper nach unten), steht in der Statusleiste: zuerst die Höhe ändern (H), dann weiterzeichnen; der Kern verlangt den Verlauf entlang der Anschlussachse (≤ 1°).
  Am Anschluss mit waagrechter Achse rastet die erste Gerade auf diese Achse. **Offene Enden von Leitungen** sind ebenfalls Anschlüsse (`ende`/`anfang`): so zeichnet man eine Leitung weiter.
- **Ende auf einem Anschluss:** `nach`, die Leitung endet dort und die Vorschau erscheint sofort; liegt der Anschluss höher oder tiefer als die Zeichenhöhe, geht der Verlauf zuerst auf der eigenen Höhe heran, dann senkrecht an den Anschluss.
- **Fliessrichtung:** der Kern verlangt den Verlauf in Fliessrichtung. Beginnt die Zeichnung an einem Anschluss mit Fluss «rein» (nimmt auf) oder endet sie an einem mit «raus» (speist), **dreht das Werkzeug die Punktfolge um** und vertauscht von und nach (nur wenn das alles in Ordnung bringt; sonst meldet der Kern).
- **Belegter Anschluss:** Klick darauf = Meldung «… ist schon verbunden (mit …)» mit Vorschlag, die Leitung beginnt nicht.
- Medium, Grösse und Lage prüft der Kern (`verbinden`-Regeln); eine kleinere Grösse am Anschluss ergibt eine Reduktion, die Vorschau nennt sie.

**Abzweig.** Klick (als erster Punkt) auf eine **bestehende Gerade mit Serie** (Rohr bzw. Kanal, zwei Punkte; Treffer im Umkreis von 8 px, mindestens 1 mm von den Enden) beginnt einen Abzweig: Serie, Grösse und Höhe der Leitung werden übernommen (änderbar), der Fusspunkt liegt auf der Achse (bei einer senkrechten Leitung gilt die Höhe der Optionsleiste, sie muss im Bereich der Leitung liegen).
Der erste Punkt des Abzweigs rastet **rechtwinklig** zur Leitung (auch ohne Winkelfang), weitere Punkte sind der Abzweigverlauf → `leitung_abzweigen { leitung, punkt, punkte, serie, groesse, gefaelle?, geschoss }`; System und Strang übernimmt der Kern von der Leitung. **Enter ohne weiteren Punkt** setzt nur das T-Stück (`richtung` = rechtwinklig auf der Seite des Mauszeigers, ohne `laenge`);
von dessen freiem **Anschluss c** zeichnet man danach mit dem Anschlussfang weiter (so bietet es der Kern an). Endet der Abzweig auf einem Anschluss, geht er als `ende` mit.

**Griffe** (`griffe.js`): an einer gewählten Leitung (Rohr, Kanal) erscheinen Griffe an den Punkten; Ziehen (Fangen, Umschalt, Esc bricht ab) sendet `element_aendern` mit der neuen Geometrie (z der Punkte bleibt). Verbundene Elemente führt der Kern nach; neue Befunde P13–P18 meldet die Oberfläche als Meldung.

**Eigenschaftenpalette** (`eigenschaften.js` mit `leitung-palette.js`):
- *Gerade mit Serie:* Gruppe **«Leitung»** ersetzt die Typauswahl der Gruppe «Familie»: **Serie** (Auswahl der Serien der Kategorie, Knopf «Editor»), **Grösse** (Typen der Serie), Mass (aussen, innen, Wand aus der Serie), **Gefälle**, **Dämmung** (Art), **Dämmdicke**, Zeile mit den Summen des Leitungszugs (Abfrage `leitungszug`). `dn`, `gefaelle`, `daemmung`, `daemmdicke` stehen nicht noch einmal unter «Eigenschaften».
  Änderung von Serie oder Grösse fragt **«Nur diese Leitung» / «Ganzer Leitungszug»** (mit den Summen des Zugs) → `leitung_serie` bzw. `leitung_groesse` (`zug: true` beim ganzen Zug); abgelehnt erscheint der Text des Kerns mit Vorschlag am Feld, Abbrechen stellt die Auswahl zurück. Gefälle, Dämmung und Dämmdicke gehen als `element_aendern` (Eigenschaften).
- *Altbestand (Leitung ohne Serie, z. B. aus IFC):* Hinweis «Serie: keine (Polylinie aus dem Bestand)» und Knopf **«Aufteilen mit Serie …»** (auch im Kontextregister «Ändern | Rohr», Gruppe «Leitung», sonst ausgegraut): Dialog mit Serie und Grösse (leer = aus der Leitung), **Vorschau des Kerns** (Trockenlauf von `leitung_aufteilen`, Geraden und Bögen), «Aufteilen».
- *Formteile (Bogen, T-Stück, Reduktion, Reinigungsöffnung):* Gruppe **«Formteil»** mit Art, Familie, Typ und Winkel (nur lesen); die Gruppen «Familie» und «Anschlüsse» bleiben.

**Listen.** **Materialliste** (Register Ansicht › Gruppe «Erstellen», Aktion `ansicht.materialliste`; Kontextmenü auf dem Projektbrowser-Knoten «Listen»): öffnet die Listenansicht `AN-LI-MATERIALLISTE` (Ansicht `art = liste`, `liste = materialliste`; die Darstellung ist die von B4) oder legt sie beim ersten Mal an. Der Dialog «Liste anlegen» kennt die Materialliste seit C3 (Kennzeichen-Vorschlag `AN-LI-MATERIALLISTE`, Name «Materialliste»); die eigene Aktion bleibt.

**Annahmen** (Prüfliste Christian, Bedienung, keine Normwerte): (1) Winkelfang 3°, Kandidaten wie oben; ohne Winkelliste der Serie ±45°/±90°. (2) Die Höhe ist ein Pflichtfeld ohne Vorgabe (Revit-Offset 0 wäre eine erfundene Annahme); z = Kote + Höhe. (3) Fangradius der Anschlüsse 10 px, Marken im Umkreis 50 px, gelesen werden Elemente im Umkreis von 3 m um den Mauszeiger.
(4) Am Ende auf einem Anschluss endet die Leitung von selbst (Vorschau). (5) Zeichnen gegen die Fliessrichtung wird umgedreht (nur Anschlüsse mit «rein»/«raus»). (6) Ein Abzweig übernimmt Serie, Grösse und Höhe der Leitung; der erste Abzweigpunkt rastet immer rechtwinklig. (7) Die Rückfrage bei Grössen- und Serienwechsel ist immer da; «Ganzer Leitungszug» ist vorgewählt.
(8) Esc verwirft zuerst die Leitung (wie Revit), nicht das Werkzeug. (9) Leitungen mit Serie und zwei Punkten sind abzweigbar; Altbestand zuerst aufteilen.

**Befunde** (nicht von C1 gebaut; **(1) und (2) behoben in C3**, siehe den Abschnitt «Leitungsserie im Familieneditor»): (1) **«Neue Familie» für Rohr und Kanal scheiterte im Kern** («Eine Leitungsserie braucht den Block «leitung»»); `familieEntwurfMinimal` und der Familieneditor kannten den Block `leitung` nicht. Behoben: Vorlage und Seite «Leitungsserie».
(2) Die Plangrafik lieferte für Leitungen mit Serie nur Fangpunkte, keine Fangkanten (seit C3 liefert sie die Achse als Fangkante); den Abzweig-Treffer bestimmt das Werkzeug weiter aus den Darstellungsdaten (Gerade im Grundriss). (3) `anschlusslagen` verlangt `element` (kein «alle»); die Oberfläche liest je Element in der Nähe. (4) Die Kategorie «Formteil» hat im Menüband weiter keinen Knopf (PF gesperrt); Formteile entstehen nur über die Leitungsbefehle bzw. «Familie platzieren».
(5) Das Kontextregister zeigt die Gruppe «Leitung» bei jeder Auswahl (ausgegraut, mit Hinweis), weil sich Register nicht nach der Klasse filtern lassen.

**Prüfen:** `node --test js/werkzeuge/leitung.test.mjs` (45 Tests: Winkelfang, Anschlussfang, Höhenwechsel, Fliessrichtung, Parameter von/nach, Zifferneingabe, Optionsleiste, Vorschau-Texte, Palette; dazu Lesetests gegen `RoutingBefehle.cs`, `Routing.cs`, `Leitungsserie.cs`) und alle bisherigen `*.test.mjs`; Browser (Edge headless über CDP, der Test startet **seinen eigenen Server** mit frischem Beispielprojekt, Port 5163,
Build-Ordner `%USERPROFILE%\HLKS_CAD_build\m3c1`, `HLKS_BAU` ändert ihn): `SHOTS=<Ordner> node js/werkzeuge/leitung.browser.mjs` (ohne Serie, Serien und Heizkörper laden, L-Zug mit Vorschau und Rückgängig, Winkelfang, Ablehnungen, Steigleitung mit Höhenwechsel und Zifferneingabe, Start und Ende an Anschlüssen, belegter Anschluss, Abzweig und T-Stück mit Weiterzeichnen von TS.c,
Palette mit Rückfrage, Serienwechsel, Dämmung, Altbestand aufteilen, Griffe, 1.OG mit Gefälle, Kanal, Materialliste; Bilder dunkel und hell). Alle Werte der Testserie sind FIKTIV (`tests/daten/familien/test_*.familie.json`).

## Leitungsserie im Familieneditor (Modul 3, C3)

Der Familieneditor (Modul 2, C1) kennt jetzt Leitungsserien: Familien der Kategorie Rohr bzw. Kanal mit dem Block `leitung` (`schema/hlks-familie.schema.json`, `entwurf/modul-3.md` Grundsatzentscheid 2). **Der Editor rechnet nichts:** er macht aus den Feldern den ganzen Block und sendet ihn mit der op `leitung_setzen` über `familie_aendern` (ein Paket je Änderung, rückgängig mit Strg+Z);
ob Parameter, Formeln, Typregeln und Winkel gelten, sagt der Kern (Trockenlauf bei jeder Eingabe, Fehler rot mit seiner Meldung und Vorschlag; Klick auf eine Zeile in «Prüfen» springt zum Feld). Reine Logik (Vorlage, Definition ↔ Formular, Kontrolltabelle) steht in `familien-text.js`, das HTML der Seite in `editor-leitung.js` (beide ohne DOM, in Node getestet).

**Neue Familie** (Projektbrowser › Familien › «Neue Familie …»): bei Kategorie Rohr bzw. Kanal (auch abgeleitete Klassen, über `basis` der Kurzform des Katalogs) entsteht ein Entwurf mit gültigem Block `leitung`, den **Typparametern ohne Werte** und einem Typ «Standard» ohne Werte (der Kern nimmt das an und meldet nur den Hinweis «lässt sich so nicht platzieren»; die Kontrolltabelle sagt es verständlich):
Rohr rund mit `dn` (Datentyp DN, ziel `dn` wie die Testserie) und `da` (Aussendurchmesser, Länge), Kanal mit **Auswahl «Querschnitt»** rund (`d`, `da`) oder rechteckig (`b`, `h`; `aussen` = `b` wie die Testserie, bei rechteckig nicht genutzt). Die Werte (Grössen, Aussendurchmesser) trägt der Mensch bzw. die KI aus der Herstellerunterlage ein – die Vorlage hat keine Zahlen. Der Editor öffnet auf der Seite «Leitungsserie».

**Seite «Leitungsserie»** (Reiter an zweiter Stelle, nur bei Familien mit Block `leitung`; **Körper und Anschlüsse entfallen**, ebenso die Ansichten rechts – eine Serie hat keine Körper, die Knöpfe «Quader» usw. im Register Familie sagen es; das Sinnbild bleibt):
- **Querschnitt und Grösse:** Querschnitt (Rohr nur rund, Kanal rund/rechteckig); Grössenparameter dn bzw. d bzw. b und h (Auswahl unter den Typparametern mit Zahl). **Ein Wechsel des Querschnitts** ändert den Block und legt fehlende Grössenparameter (ohne Werte) im selben Paket an.
- **Masse:** `aussen` (Pflicht), `innen`, `wand` als Zahl oder Formel aus den Parametern (leer entfernt das optionale Feld).
- **Beschriftung (Nachtrag B9):** «Kürzel (Werkstoff)» (`kuerzel`, nur bei Rohren; Hinweis «für die Beschriftung, z. B. CNS, PE-S, PEX»). Mit Kürzel lautet die Beschriftung der Rohre im Schema und im Grundriss bei **Abwasser** (Schmutz- und Regenwasser) «Kürzel Aussendurchmesser» (z. B. «PE-S 110», ohne Ø, ohne NW/DN), bei **allen anderen Rohren** «Kürzel NW|DN Zahl» nach der Projekteinstellung (z. B. «CNS NW 20»; Entscheid Christian Steiner 19.10.2026, B9b, ersetzt die frühere Form «CNS Ø22»), ohne Kürzel nur die Grösse («NW 20» bzw. «110»). Länge (höchstens 12 Zeichen) und Zeichen prüft der Kern; leer entfernt das Feld.
- **Formteile:** je Art (Bogen, T-Stück, Reduktion, Reinigungsöffnung) die Auswahl unter den **Familien des Projekts** der passenden Kategorie (Abfrage `familien`; die Auswahl führt auf die Definitions-id wie im Block), dazu die optionale **Typregel** (Formel, die den Typnamen ergibt; erst mit gewählter Familie). Eine Familie, die der Block nennt und im Projekt fehlt, bleibt sichtbar («nicht im Projekt»). «(keine)» entfernt die Art samt Typregel.
- **Regeln:** erlaubte Bogenwinkel (Liste in Grad, getrennt durch Komma, Semikolon oder Leerzeichen, Dezimalpunkt; **«frei»**; leer = nicht angegeben, der Kern lässt dann jeden Winkel zu), Biegeradius (Zahl oder Formel), Abzweig (T-Stück), **Abzweigwinkel** (Liste in Grad, leer = nur 90°; Nachtrag B7).
- **Kontrolle** (Abfrage `leitungsserie`, bei jeder Änderung und bei Änderungen anderer Familien des Projekts neu geholt): Tabelle je Grösse (Typ) mit Grösse, aussen, innen, wand und **Bogen, T-Stück, Reduktion, Reinigungsöffnung ja/nein** (Reduktion «n von m» auf die kleineren Grössen, «–» bei der kleinsten; Tooltip mit Familie und Typ bzw. dem Grund des Kerns). Darunter die Liste **«Nicht verfügbar»**: Art, betroffene Grössen und der Grund mit Vorschlag des Kerns (je Art zusammengefasst; nennt der Grund eine Grösse, steht der erste als «z. B.»).
  Ist die Definition ungültig oder ein Typ ohne Werte, steht der Kern-Fehler in der Tabelle («nicht auswertbar: … hat im Typ «Standard» keinen Wert → Werte auf der Seite «Typen» eintragen»).

**Fangkanten (Kern):** `HlksGrafik.Leitung` liefert für jede Leitung mit Lauf im Grundriss die **Achse als Fangkanten** (je Teilstück eine, wie die Achse einer Wand); die Fangpunkte (Enden, Knicke) bleiben. Das gilt für Leitungen mit und ohne Serie; senkrechte Leitungen (ein Punkt im Grundriss) haben keine Kante. Die Fangarten Schnittpunkt, Mitte, Lot und nächster Punkt greifen damit auch auf Leitungen.

**Liste anlegen** (Register Ansicht › Erstellen › «Liste»): die Auswahl enthält jetzt «Materialliste».

**Annahmen** (Prüfliste Christian, Bedienung, keine Normwerte): (1) Die Vorlage hat **keine Werte** und keine Formteil-Familien; der Typ heisst «Standard» (die Typen einer Serie sind ihre Grössen – du benennst und ergänzt sie aus der Unterlage). (2) Ein Rohr bietet nur «rund» an (der Kern lehnt rechteckig ab). Die abstrakte Basisklasse «Leitung» bietet alle drei Formen an (Rohr rund, Kanal rund, Kanal rechteckig). (3) Bei rechteckig steht `aussen` = `b` (der Block verlangt es, der Kern nutzt es dort nicht). (4) Die Ansichten rechts (Körper) entfallen bei Serien; eine Serie öffnet auf «Leitungsserie», solange nichts anderes gemerkt ist.
(5) Winkel und Abzweigwinkel: Dezimalpunkt, das Komma trennt («7,5» ist 7 und 5). Die Bereiche (0 < w ≤ 180 bzw. ≤ 90, doppelte Winkel) prüft der Kern. (6) Fangkanten gelten auch für Leitungen ohne Serie (Altbestand).

**Befunde:** (1) Der Kern nennt bei Reduktionen und Bögen den Grund je Grösse mit der Grösse im Text; die Liste fasst deshalb nach Art zusammen und zeigt den ersten Text (ein Feld «Stelle» bzw. die Grössen je Grund wären robuster). (2) `leitungsserie` meldet bei `winkelRegel: "keine"` trotzdem `winkel: "frei"` – die Kontrolle liest `winkelRegel`. (3) Die abstrakte Basisklasse «Leitung» ist als Kategorie wählbar (der Kern nimmt sie an); ob eine Serie dieser Kategorie von Rohr- oder Kanal-Elementen benutzt werden kann, ist offen.

**Prüfen:** `node --test js/familien/familien-leitung.test.mjs` (21 Tests: Vorlage Rohr und Kanal, Definition ↔ Formular, Validierung, HTML, Kontrolltabelle, Verdrahtung; mit `HLKS_TEST_SERVER` zusätzlich gegen den echten Kern, nur Trockenläufe: Vorlagen aller familienfähigen Kategorien, alle ops der Seite, Ablehnungen mit Stelle und Vorschlag) und alle bisherigen `*.test.mjs`; Kern: `LeitungFangkantenTests.cs`; Browser (Edge headless über CDP, der Test startet **seinen eigenen Server** mit frischem Beispielprojekt, Port 5164, Build-Ordner `%USERPROFILE%\HLKS_CAD_build\m3c3`, `HLKS_BAU` ändert ihn):
`SHOTS=<Ordner> node js/familien/familien-leitung.browser.mjs` (Testrohrserie öffnen: Seiten, Felder, Kontrolltabelle; Formteil-Familie wechseln; Winkel ändern, rückgängig, Fehler der Seite und des Kerns; Typregel; Neue Familie Rohr und Kanal ohne Kern-Fehler, Werte von Hand, Querschnitt wechseln; Liste «Materialliste» anlegen; Fangkanten in der Plangrafik; Bilder dunkel und hell). Alle Werte der Testserien sind FIKTIV (`tests/daten/familien/test_*.familie.json`).

## Koordination (Modul 3b, C1)

Oberfläche für Kollisionsprüfung, Aussparungen und BCF auf dem Kern aus Modul 3b (`entwurf/modul-3b.md`). **Die Oberfläche rechnet und entscheidet nichts:** Kollisionen, Aussparungen, Grössen, Status-Übergänge, Abgleich und BCF-Dateien kommen vom Kern; ändern geht nur über seine Befehle
(`vorgaben_setzen`, `kollision_pruefen`, `kollision_status`, `aussparungen_vorschlagen`, `aussparungen_nummerieren`, `aussparung_status`, `aussparung_form`, `aussparungen_abgleichen`, `bcf_import`; `element_anlegen`, `planblatt_anlegen`, `ansicht_platzieren` für den Aussparungsplan), lesen über die Abfragen
`vorgaben`, `kollisionen`, `aussparungsliste`, `bcf_export`, `bcf_lesen`, `faehigkeiten` (und später `aussparungen_ifc`). **Keine Vorgabewerte in der Oberfläche:** die Felder der Projektvorgaben sind leer, wo der Kern keinen Wert mit Quelle hat; jeder Wert geht mit seiner Quelle (Pflicht).
Texte des Kerns, die für MCP geschrieben sind, zeigt die Oberfläche mit den Namen ihrer Knöpfe («mit vorgaben_setzen anlegen» wird «mit «Projektvorgaben …» anlegen», `fuerMenschen`).

| Datei | Inhalt |
| --- | --- |
| `koordination-text.js` | rein (Node getestet): Listen der Auswahlen, Formular ↔ Parameter der Projektvorgaben (Quelle Pflicht), Prüfungen, Tabellen und Einzelheiten der Kollisions- und Aussparungsliste, Mehrfachauswahl, Form der Aussparung (Schalter, Vorschau, Meldungen), Parameter aller Befehle, Aussparungsplan, IFC, BCF (Pfade, Vorschau, Import), Koordinaten |
| `koordination-panel.js` | Palette «Koordination» (Kollisionsliste, Aussparungsliste), Sprung in Grundriss und 3D, Status setzen, Form wählen |
| `koordination-dialoge.js` | Dialoge: Projektvorgaben, Kollisionen prüfen, Aussparungen vorschlagen / nummerieren, Abgleich, Aussparungsplan, IFC für Architekt, BCF exportieren / importieren |
| `aktionen-koordination.js` | Register «Koordination» und seine Aktionen (`koordination.*`) |
| `koordination.test.mjs`, `koordination.browser.mjs` | Tests |

**Register «Koordination»** (eigenes Register zwischen «Ändern» und «Ansicht», nicht eine Gruppe in Gebäudetechnik: elf Knöpfe in vier Gruppen, kein Zeichenwerkzeug und keine Verwaltung – wie «Hülle» ein eigenes Modul; Revit führt Prüfen und Austausch ebenfalls in einem eigenen Register «Zusammenarbeit», und zwar unmittelbar vor «Ansicht»; die Reihenfolge Gebäudetechnik › Einfügen › Beschriften › Ändern bleibt, ältere Tests prüfen sie):
**Vorgaben** (Projektvorgaben) · **Kollisionen** (Kollisionen prüfen, Kollisionsliste) · **Aussparungen** (Aussparungen vorschlagen, Aussparungen nummerieren, Aussparungsliste, Abgleich, Aussparungsplan anlegen, IFC für Architekt) · **BCF** (BCF exportieren, BCF importieren). Gesperrt mit Hinweis im Tooltip: Nummerieren ohne Aussparung,
BCF exportieren ohne Kollision, Abgleich ohne IFC-Unterlage, IFC für Architekt, solange die Abfrage `aussparungen_ifc` im Kern fehlt (Teil B4b; die Oberfläche liest `faehigkeiten` einmal je Projekt – kommt die Abfrage, wird der Knopf aktiv und sein Dialog zeigt Geschoss, Status und Koordinaten nur, wenn der Kern sie kennt).

**Projektvorgaben** (`vorgaben_setzen`, Abfrage `vorgaben`; Nachtrag C1b nach den Entscheiden Christian 19.10.2026): Tabelle der drei Zahlenwerte **spiel_aussparung** und **abstand_zusammenfassen** (*Pflicht für Aussparungen*) und **toleranz_hart** (*wählbar*, wie bisher; mm) mit **Wert, Quelle (Pflicht) und Zustand** – «gesetzt» mit Herkunft oder «fehlt» mit der **Wirkung**, die der Kern nennt; Werte ohne Quelle (gelten nicht) stehen rot.
**Kein Feld «Grenze Kernbohrung» mehr** (die Vorgabe `grenze_kernbohrung` entfällt: Kernbohrungen wählt nur ein Mensch, siehe Aussparungsliste). Steht in einem alten Projekt noch ein Wert (Abfrage `vorgaben`: `verwendet: false`, `nichtMehrVerwendet[].gesetzt`; auch ein Wert ohne Quelle), zeigt der Dialog einen gelben Hinweis «**nicht mehr verwendet (Entscheid Christian 19.10.2026)**» mit dem alten Wert, dem Grund des Kerns und dem Knopf **«entfernen»**: er merkt das Entfernen vor (bis zum Speichern «doch behalten»), die Vorschau des Kerns zeigt «grenze_kernbohrung entfernt», «Speichern» sendet `vorgaben_setzen { werte: { grenze_kernbohrung: null } }`. Neu gesetzt wird die Grenze nie.
**«Abstandsprüfung (aus)»:** `abstand_leitungen` (Mindestabstand Leitung – Leitung) und `abstand_weich` (**Tabelle je Gewerkpaar**: Seite A, Seite B aus `abstandWeichSeiten` des Kerns, Abstand mm; die Reihenfolge der Seiten zählt nicht, doppelte Paare lehnt der Dialog ab, eine Quelle für alle Paare) stehen in einem **eingeklappten Bereich** (`<details>`; er bleibt über das Neuzeichnen hinweg auf oder zu, die Felder sind nur aufgeklappt bearbeitbar) mit der Erklärung «keine Abstandsprüfung, die weiche Prüfung bleibt aus, die Werte bleiben setzbar». Fehlende Abstände zählen nicht als «fehlt» (Zustand «nicht gesetzt – Abstandsprüfung aus»); sind welche gesetzt, steht in der Überschrift «Abstandsprüfung (n Wert(e) gesetzt)».
«Quelle in leere Zeilen übernehmen» kopiert die erste ausgefüllte Quelle. Beim Tippen rechnet der Kern einen **Trockenlauf** (Hinweise oder Ablehnung); «Speichern» sendet nur Geändertes, ein geleerter Wert wird mit `null` entfernt; ohne Quelle sendet der Dialog nichts, der Kern lehnt es ebenfalls ab. Ändert sich ein Wert bei gleicher Quelle, warnt der Dialog («gilt sie auch für den neuen Wert?»).

**Kollisionen prüfen** (`kollision_pruefen`): Paare (HLKS–Architektur und HLKS–HLKS vorgewählt wie beim Kern, HLKS–Fremd, und – seit C1b – **«Bekleidungen (Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen)»** = Paar `hlks-bekleidung`, **standardmässig aus**, nicht in der Vorgabe; Kern-Teil B3c: kennt der Kern das Paar noch nicht, meldet er den Fehler «Paar … gibt es nicht» mit Vorschlag und der Dialog zeigt ihn), Geschoss, IFC-Unterlagen (nichts angekreuzt = alle), **weiche Prüfung**: **aus, solange keine Abstände gesetzt sind** (Hinweis «Keine Abstände in den Projektvorgaben gesetzt – die Abstandsprüfung ist aus …»); sind `abstand_leitungen` oder `abstand_weich` gesetzt, ist sie vorgewählt, solange man das Häkchen nicht selbst anfasst. `paare`, `unterlagen`, `geschoss` gehen nur bei Abweichung von den Vorgaben des Kerns mit, **`weich` immer ausdrücklich** (true oder false), damit der Lauf nicht von der Vorgabe des Kerns abhängt. Der Dialog nennt die **fehlenden Vorgaben** (toleranz_hart; abstand_weich und abstand_leitungen nur bei eingeschalteter weicher Prüfung) mit Wirkung
und öffnet «Projektvorgaben …» (verschachtelt). **«Vorschau (Trockenlauf)»** zeigt die Hinweise des Kerns (Zahlen je Art und Paar, Status, was entfällt – in Warnfarbe), «Prüflauf ausführen» führt aus; danach «Kollisionsliste». Die Vorschau läuft nur auf Knopfdruck (ein Lauf kann bei grossen Modellen eine halbe Minute dauern).

**Palette «Koordination»** (seit W1 eine Palette der Palettenverwaltung: Vorgabe unten angedockt unter der Ansichtsfläche (Bereich «unten», `#dock-unten`); nicht modal, die Ansichten darüber schrumpfen mit; Höhe am oberen Rand ziehbar, gemerkt; ✕ am Palettenkopf schliesst; sie lässt sich auch lösen, rechts oder links andocken oder mit anderen Paletten als Reiter legen; sie führt Änderungen von aussen – KI, anderer Arbeitsplatz – ohne Neuladen nach). Zwei Reiter:
- **Kollisionen:** Filter Status (Häkchen, alle vorgewählt), Art, Typ, Geschoss, Element → Abfrage `kollisionen`; Kopfzeile mit Zahlen und letztem Prüflauf; Tabelle Kollision, Art, Typ, Status, Mass, Geschoss, Beteiligte, Kommentar (bis 500 Zeilen). **Klick auf eine Zeile springt** (dieselbe Aktion wie «Im Grundriss zeigen» aus Modul 3: öffnet den Grundriss des Geschosses, zoomt auf die Lage ±1.5 m und wählt die Kollision),
  «In 3D zeigen» öffnet die 3D-Ansicht und wählt sie; in den Einzelheiten springt ein Klick auf einen Beteiligten (Element des Modells) zu diesem. **Status für mehrere:** Häkchen (Kopf-Häkchen = alle sichtbaren) oder, ohne Häkchen, die aktive Zeile → Status wählen, Kommentar (leer = unverändert; «Kommentar entfernen») → `kollision_status`; die Rückmeldung des Kerns steht in der Palette.
- **Aussparungen:** Filter Status (Häkchen) und Geschoss → Abfrage `aussparungsliste`; Nummer, Aussparung, Geschoss, Bauteil, Form und Mass, Tiefe, Kote ±0.00 und über OK Geschoss (UK/OK bzw. Achse), Art, Status, Leitungen. Sprung wie oben; **Status setzen** (alle sieben Status wählbar, **Freigeben** als Kurzweg) → `aussparung_status`:
  den Übergang prüft der Kern, eine Ablehnung («Übergang nach … nicht zulässig», mit Vorschlag) steht in der Palette. Knöpfe für Vorschlagen, Nummerieren, Abgleich, Aussparungsplan und IFC.
  **Form (Nachtrag C1b, `aussparung_form`):** Automatisch gibt es nur Rechtecke (`aussparungen_vorschlagen`, auch für runde Rohre); eine **Kernbohrung (rund) wählt nur ein Mensch**. Spalte «Form» zeigt je Zeile einen **Schalter «rechteckig | rund»** (die aktuelle Form ist gewählt), Spalte «Mass» das Mass (215 × 95 bzw. Ø 95); **«von Hand»** (gelbe Marke) kennzeichnet eine von Hand gewählte Form (`formVonHand`), die Kopfzeile zählt sie. Der Schalter ist **nur bei `formWaehlbar` aktiv**; sonst sind beide Knöpfe gesperrt und **`formSperre`** (Text des Kerns, z. B. «Status versandt – zuerst aussparung_status vorgeschlagen», «Sammelaussparung (2 Leitungen) – bleibt rechteckig», «ohne Leitung – Mass nicht rechenbar») steht als Tooltip und in den Einzelheiten. Für die **markierten Zeilen** gibt es die Knöpfe **«Als Kernbohrung»** und **«Als Rechteck»**; gesendet werden nur wählbare Zeilen, die übrigen stehen mit ihrem Grund unter «Nicht wählbar, nicht gesendet»; ist keine wählbar, geht nichts an den Kern.
  Jede Wahl läuft **erst als Trockenlauf**: die Palette zeigt «Vorschau (Trockenlauf – nichts wurde geändert)» mit den Hinweisen des Kerns (z. B. «rechteckig 95 × 95 → rund Ø 95, Kernbohrung (von Hand gewählt)», **Nachbar unter `abstand_zusammenfassen`**, **«Status bleibt veraltet»**, **«ist schon …»**) und den Knöpfen «Ausführen» / «Abbrechen» (ohne Änderung ist «Ausführen» gesperrt). **Ablehnungen des Kerns** (Status nicht vorgeschlagen/veraltet, rund bei Sammelaussparung, ohne Leitung, Leitung verschoben, fehlende Vorgaben) zeigt die Palette immer mit **Fehler und Vorschlag**.
  **Material (Kern-Teil B3c):** Liefert die Liste `material` / `materialUnbekannt`, erscheint die Spalte «Material» (bei `materialUnbekannt` «Material unbekannt» in Warnfarbe; die Kopfzeile zählt sie, die Einzelheiten nennen es); ohne diese Felder gibt es weder Spalte noch Hinweis.

**Aussparungen vorschlagen** (`aussparungen_vorschlagen`; schlägt **nur rechteckige Aussparungen** vor, der Dialogtext verweist für Kernbohrungen auf «Als Kernbohrung» in der Aussparungsliste): Geschoss, auf Wunsch Leitungen und Bauteile; **Vorschau des Kerns** (Trockenlauf, entprellt: «Aussparungen: 2 neu, … Sammelaussparung …» und die Liste je Aussparung), dann «Vorschläge anlegen». Fehlen Vorgaben, lehnt der Kern ab und der Dialog zeigt seinen Text; «Projektvorgaben …» steht im Dialog.
**Nummerieren** (`aussparungen_nummerieren`): Geschoss, «alle neu» (mit Warnung: Nummern können schon beim Architekten sein), Vorschau, Nummerieren. **Abgleich** (`aussparungen_abgleichen`): IFC-Unterlage der neuen Architekten-Fassung wählen, Vorschau, Abgleichen (angekommen bzw. fehlt je Aussparung, Hinweise des Kerns).

**Aussparungsplan anlegen:** je gewähltem Geschoss ein **Grundriss** `AN-AUS-<Geschoss>` mit `darstellung = aussparungen` (Massstab, Gewerke wählbar; gibt es die Ansicht schon, wird sie weiterverwendet) und auf Wunsch ein **Planblatt** (`planblatt_anlegen`) mit der Ansicht (`ansicht_platzieren`). **Plannummern vergibt der Mensch** – ohne Nummer kein Planblatt, doppelte Nummern lehnt der Dialog ab.
Vorgewählt sind die Geschosse mit Aussparungen. Wie die Darstellung `aussparungen` gezeichnet wird (Marken, Nummern, Koten), bestimmt der Kern (Teil B4a); bis dahin ist die Ansicht ein gewöhnlicher Grundriss.

**IFC für Architekt:** Dialog mit Datei (vollständiger Pfad, .ifc), Fassung (IFC4, IFC2X3) und – soweit der Kern sie kennt – Geschoss, Status und **Koordinaten** (Auswahl wie bei BCF, siehe dort); Antwort des Kerns (Hinweise und Rohantwort) im Dialog. Gesperrt, solange `aussparungen_ifc` fehlt.

**BCF exportieren** (Abfrage `bcf_export`, schreibt eine Datei, ändert das Modell nicht): **vollständiger Pfad** der Zieldatei (.bcf oder .bcfzip; ein Name allein würde im Ordner des Servers landen – die Oberfläche lehnt ihn ab; der letzte Ordner wird gemerkt, Vorschlag «Kollisionen_<Projekt>_<Datum>.bcf»), Filter Status (Häkchen), Art, Autor, **Koordinaten** (seit C1b: «**Vorgabe (IFC mit Projektbezug, sonst Projekt)**» vorgewählt / «IFC» / «Projekt»; **«Vorgabe» sendet nichts** – der Kern wählt `ifc`, wenn das Projekt einen Projektbezug hat, sonst `projekt` und sagt es im Hinweis –, **eine ausdrückliche Wahl geht immer mit, auch «projekt»**; dieselbe Auswahl hat «IFC für Architekt»; «IFC» ohne Projektbezug lehnt der Kern ab, der Dialog zeigt Fehler und Vorschlag)
und – sind in der Kollisionsliste Häkchen gesetzt – «Nur die n gewählten Kollisionen». Die Zeile «n Kollision(en) werden exportiert (hart …, weich …)» kommt aus der Abfrage `kollisionen` mit demselben Filter; nach dem Export zeigt der Dialog Datei, BCF-Version, Topics, Grösse, Koordinaten und das Ergebnis der Schemaprüfung des Kerns.
**BCF importieren:** Pfad, dann «Lesen» (`bcf_lesen`: Version, Topics, zugeordnet, unbekannt, Tabelle Topic ↔ Kollision mit Status BCF, **Status KICAD → nach Import**, Zahl neuer Kommentare; fremdes Projekt gelb) plus **Trockenlauf von `bcf_import`**; «Übernehmen» erst nach dem Lesen (ein Paket, rückgängig mit Strg+Z).

**Annahmen** (Prüfliste Christian, Bedienung, keine Normwerte): (1) Eigenes Register statt Gruppe unter Gebäudetechnik, zwischen Ändern und Ansicht (Revit: «Zusammenarbeit» vor «Ansicht»). (2) Die Palette ist angedockt (nicht modal), damit man beim Abarbeiten der Liste im Grundriss arbeiten kann; ein Dialog würde die Ansicht verdecken. (3) Sprung = Kollision bzw. Aussparung wählen und den Grundriss des Geschosses auf ±1.5 m zoomen (Aktion aus Modul 3); die 3D-Ansicht hat keine Zoom-Funktion, «In 3D zeigen» wählt nur.
(4) Filter Status der Kollisionsliste: alle fünf vorgewählt, nichts wird als «erledigt» ausgeblendet. (5) Die Vorschau des Prüflaufs läuft nur auf Knopfdruck, die der Aussparungen, Nummern und des Abgleichs sofort. (6) Dateien wählt man durch Eintippen eines vollständigen Pfads (der Server läuft auf demselben Rechner, ein Datei-Dialog des Browsers kennt den Pfad nicht).
(7) Der Aussparungsplan bekommt Massstab 1:50 und die Gewerke der aktiven Ansicht (sonst Sanitär und Architektur) als Vorgabe wie «Grundriss anlegen»; Plannummer und Titel gibt der Mensch ein bzw. bestätigt sie.
(8, C1b) Die weiche Prüfung ist im Dialog «Kollisionen prüfen» **aus, solange keine Abstände gesetzt sind**, und vorgewählt, sobald welche gesetzt sind (der Auftrag sagt «standardmässig aus, mit Hinweis, solange keine Abstände gesetzt sind»; mit gesetzten Abständen wäre «aus» ein Rückschritt für den, der sie bewusst gesetzt hat). (9) `weich` geht immer ausdrücklich an den Kern, damit eine spätere Änderung seiner Vorgabe (Teil B3c) den Lauf nicht verändert. (10) Die Form wird erst nach der Vorschau des Kerns gesetzt, auch beim Einzelklick im Schalter (Auftrag: «Vorschau mit Trockenlauf, dann ausführen»). (11) Nicht wählbare Zeilen (`formWaehlbar` false) werden nicht an den Kern geschickt (er lehnte sonst den ganzen Befehl ab); sie stehen mit ihrem Grund in der Meldung. (12) «entfernen» der alten Grenze wirkt erst mit «Speichern» (wie jede andere Änderung im Dialog), nicht sofort.

**Befunde:** (1) `aussparungen_ifc` fehlt noch im Kern (B4b): Knopf und Dialog sind fertig, die Oberfläche liest die Parameter aus `faehigkeiten` und ist ohne Test gegen die echte Abfrage. (2) Die Listenansicht (`art liste`) für `kollisionen` und `aussparungsliste` baut B4a; die Palette ersetzt sie nicht (Status setzen und Sprung gibt es nur hier). (3) Der Kern liefert für BCF nur Dateipfade; ein Download im Browser (wie `plan_pdf`) bräuchte einen Endpunkt in `Program.cs`.
(4) Die 3D-Ansicht hat keine Funktion «auf Element zoomen». (5) `kollision_status` ersetzt den eigenen Kommentar aller gewählten Kollisionen: bei Mehrfachauswahl gilt ein neuer Kommentar für alle.
(6, C1b) Das Paar `hlks-bekleidung` und die Listenfelder `material` / `materialUnbekannt` kennt der Kern erst mit Teil B3c; bis dahin meldet er das Paar mit Fehler, den der Dialog zeigt, und die Spalte «Material» fehlt (der Browsertest prüft beide Zustände). (7, C1b) Die Texte des Kerns nennen `aussparung_status` und `aussparung_form` mit Befehlsnamen; `fuerMenschen` ersetzt sie durch «Aussparungsliste …» bzw. «Als Kernbohrung / Als Rechteck …» – bei «zuerst aussparung_status vorgeschlagen» liest sich das holprig (Satzbau im Kern).

**Prüfen:** `node --test js/oberflaeche/koordination.test.mjs` (39 Tests, davon einer nur mit Server; mit `HLKS_TEST_SERVER` zusätzlich gegen den echten Kern, nur lesend und Trockenläufe) und alle bisherigen `*.test.mjs`; Browser (Edge headless über CDP, der Test startet **seinen eigenen Server** mit frischem Beispielprojekt, Port 5168 (C1b; C1 hatte 5167), Build-Ordner `%USERPROFILE%\HLKS_CAD_build\m3bc1b`, `HLKS_BAU` ändert ihn):
`SHOTS=<Ordner> node js/oberflaeche/koordination.browser.mjs` (Testrohrserie `tests/daten/familien/test_rohrserie`, drei Rohre durch die Südwand; Projektvorgaben und Quellen tragen den Vermerk «FIKTIV»; Vorgaben ohne Quelle abgelehnt, Prüflauf mit Vorschau, Liste mit Filter, Sprung und Status für mehrere, Aussparungen vorschlagen und nummerieren, Status und Ablehnung des Kerns, Abgleich, Aussparungsplan mit Planblatt, BCF-Export und -Import mit Dateien im Temp-Ordner – die Rückmeldung baut der Test mit `tar` (bsdtar von Windows) aus dem Export; Bilder dunkel und hell; seit C1b zusätzlich: kein Feld «Grenze Kernbohrung», eingeklappter Bereich «Abstandsprüfung (aus)», alter Wert der Grenze mit «entfernen», Paar «Bekleidungen», weiche Prüfung aus bis Abstände gesetzt, Form-Schalter mit Vorschau, Sperre und Ablehnung des Kerns (Leitung verschoben), Koordinaten «Vorgabe/IFC/Projekt» mit den wirklich gesendeten Anfragen).

### Nachtrag C1c (Modul 3b, Kern B3d und B4d)

- **Typenliste «Typname → Material»:** Knopf «Typenliste» (eigene Gruppe im Register Koordination) öffnet einen **Dialog** (`typenDialog` in `koordination-dialoge.js`, reine Teile `typen*` in `koordination-text.js`): drei Reiter «Unbekannte Typen» (nach Durchgängen absteigend, Kategorie je Zeile aus `kategorien` des Kerns, eine Quelle für alle, Pflicht und leer), «Material aus Typname» (zum Bestätigen) und «Typenliste» (Kategorie und Quelle ändern, entfernen); Schalter «nur betroffene»; Kasten «Zu prüfen» mit den Hinweisen des Kerns; Vorschau = Trockenlauf von `typmaterial_setzen`, gesendet wird nur, was die letzte Vorschau zeigte; nach einer Änderung Knöpfe «Aussparungen neu vorschlagen …» und «Kollisionen prüfen …» (öffnen die vorhandenen Dialoge). Dialog statt Palette: Tabellen mit Auswahl je Zeile, Quelle und Vorschau brauchen Platz; das Muster ist «Projektvorgaben».
- **Paar `hlks-vorwand`** in `T.PAARE` (zuschaltbar, nicht in der Vorgabe). **Kollisionsliste:** Paar-Filter, Spalte «Paar», Schalter «auch nicht geprüfte Paare (n ausgeblendet)»; Paare des letzten Laufs liest die Oberfläche aus `kollisionslauf` (`T.laufPaare`), sonst Vorgabe plus die zuletzt im Dialog gewählten. Wirkt im Browser (`T.kollisionSicht`), die Abfrage bleibt unverändert.
- **Zusatz B4d-UI:** Spalte «Kurzzeichen» (Feld `kurzzeichen`) in der Aussparungsliste (Palette und Listenansicht); `beschriftungAmSymbol`, `legende` und die Darstellungen `einlagen`, `grundleitungen` zeigt die Eigenschaftspalette von selbst aus dem Katalog (Bool als Schalter, Auswahl als Liste), nichts Eigenes gebaut.

## Startbildschirm (Teil S1, `entwurf/startbildschirm.md`)

Wie Revit «Home»: ohne offenes Projekt der Start (statt des früheren kleinen Dialogs «Projekt wählen»), mit offenem Projekt über dem Projekt (Haus-Symbol im Schnellzugriff, **Strg+D**, Adresse `/?start=1` des Desktop-Icons); Esc, ein Klick auf das offene Projekt, «Zurück zum Projekt» oder nochmals Strg+D kehren zurück, das Projekt bleibt im Hintergrund offen. Tasten erreichen das Projekt darunter nicht (Kürzel wie `DE`, Strg+Z). Ohne Projekt lässt sich der Startbildschirm nicht schliessen.

| Datei | Inhalt |
| --- | --- |
| `js/oberflaeche/startbildschirm.js` | die Fläche: linke Leiste (Projekte: Öffnen …, Neu …, Beispielprojekt; Familien: Bibliothek …; Version und Build-Datum), Kacheln «Zuletzt geöffnet», Liste «Alle KICAD-Projekte auf W:», Familien «zuletzt bearbeitet», Suchfeld (filtert alle Bereiche), Tastatur |
| `js/oberflaeche/start-text.js` | reine Funktionen (Zeittexte, Anfangsbuchstaben, Sortierung, Suche, HTML aller Bereiche und des Ordner-Browsers, Ziel von «Öffnen»); Test `start.test.mjs` |
| `js/oberflaeche/ordner-browser.js` | Ordner-Browser über den Server (Laufwerke, Projektablage, zuletzt benutzte Ordner; KICAD-Projekte hervorgehoben; Pfad eintippen geht weiter); wird von «Projekt öffnen» und «Zielordner» benutzt |
| `js/oberflaeche/vorschau.js`, `vorschau-logik.js` | 3D-Vorschaubild (siehe unten); die Entscheidungen und Farben sind rein und in Node getestet |
| `css/start.css` | Gestaltung, nur Variablen aus `hlks.css` (hell und dunkel) |
| `ZuletztListe.cs`, `StartDienst.cs`, `StartEndpunkte.cs` | Server: Liste je Benutzer, Projektsuche, Ordner, Familien, Vorschaubild |
| Tests | `start.test.mjs` (Node, 35 Tests), `tests/Hlks.Web.Tests/StartApiTests.cs` und `ZuletztListeTests.cs` (xUnit), `start.browser.mjs` (Edge über CDP, baut seine Umgebung selbst auf, Port 5168) |

**Endpunkte** (alle lesend, ausser Anheften/Entfernen der Liste und dem Vorschaubild des offenen Projekts):
`GET /api/zuletzt?max=10&pruefen=1` – `[{ ordner, name, zeit, zeitIso, rechner, angeheftet, objekt, vorhanden?, vorschau?, vorschauV? }]`, angeheftete zuerst, dann nach Zeit (mit `pruefen=1` prüft der Server mit Zeitlimit, ob das Projekt noch da ist und ein Bild hat; `vorhanden: null` heisst unbekannt) ·
`POST /api/zuletzt/anheften { ordner, an?, name? }`, `POST /api/zuletzt/entfernen { ordner }` ·
`GET /api/projekte/ablage?neu=1` – `{ pfad, kicadOrdner, erreichbar, hinweis, gesucht, dauerMs, zwischengespeichert, laeuft, projekte: [{ name, objekt, ordner, nummer, geaendert, vorschau, vorschauV }] }`; nur die feste Tiefe `<pfad>\*\<kicadOrdner>\projekt.json`, Zwischenspeicher 10 min, eine Suche für alle Aufrufer, **nicht erreichbar ist kein Fehler** (`erreichbar: false` mit Hinweis) ·
`GET /api/ordner?pfad=` – Unterordner mit `projekt` (projekt.json im Ordner) und `enthaeltProjekt` (projekt.json unter `<kicadOrdner>`); ohne `pfad` die Laufwerke; fehlender Pfad → 400 mit Vorschlag; `POST /api/ordner/zeigen { pfad }` öffnet den Explorer (im Testlauf nicht) ·
`GET /api/familien/zuletzt?n=12` – `{ bibliothek, erreichbar, offline, hinweis, anzahl, familien: [{ id, name, kategorie, hersteller, status, version, projekt, projektNummer, projektOrdner, zeit, zeitIso }] }` aus dem Bibliotheksindex (ohne Schreiben; ohne NAS der gemerkte Stand) ·
`POST /api/projekt/vorschau?stand=&ort=` (Body PNG) und `GET /api/projekt/vorschau?ordner=` · `GET /api/start/info` (Version, Build-Datum, Rechner, Ablage der Liste).

**Liste «zuletzt» je Benutzer:** `%OneDrive%\HLKS-CAD\zuletzt\<Rechner>.json`, **je Rechner eine Datei** (ein Rechner schreibt nie die eines anderen, also keine Konfliktkopien); die Liste ist die Vereinigung: je Projektordner die neueste Öffnen-Zeit, «angeheftet» und «entfernt» mit Zeit (der neuere Vermerk gilt; erneutes Öffnen holt Entferntes zurück). Die alte `%APPDATA%\HLKS-CAD\zuletzt.json` wird mitgelesen (nie geschrieben); ohne OneDrive gilt sie als die einzige Datei. Testprojekte unter `%TEMP%` werden nie gemerkt. **Umleitung für Tests:** `HLKS_ZULETZT` (Ordner) bzw. `Einstellungen.ZuletztOrdner`; im Testlauf schreibt die Liste nur unter `%TEMP%`. Die Reihenfolge der Zeiten setzt gleiche Uhren auf den Rechnern voraus (der spätere Zeitstempel gewinnt).

**3D-Vorschaubild:** entsteht im Browser aus `/api/koerper` (ganzes Projekt, unabhängig von der 3D-Ansicht): feste Isometrie von Südwesten, Hintergrund durchsichtig (passt in hell und dunkel), Architektur grau (Wände und Decken durchscheinend), Gebäudetechnik in den Systemfarben des Kerns (`Teil.farbe`), ohne Räume und Hintergrund-Netze, 640 × 400. Wann: 5 s nach dem Laden eines Projekts und 20 s nach Änderungen; danach **höchstens alle 10 min und nur bei geändertem Stand** (Prüfsumme, gemerkt je Projekt in `localStorage` «vorschau»); ein Fehlversuch wartet ebenfalls den Mindestabstand ab. Gespeichert wird `vorschau.png` und `vorschau.json` (Name, Projektnummer aus der Projektinformation, Stand) im Ordner des **offenen** Projekts, atomar; Projekte ohne Bild zeigen einen Platzhalter mit den Anfangsbuchstaben. Im Testlauf schreibt der Server nur unter `%TEMP%`.

**Annahmen:** (1) Die Projektnummer steht nicht in `projekt.json`; die Liste zeigt sie aus `vorschau.json` (nach dem ersten Öffnen mit Bild), sonst «–». (2) «Zuletzt geändert» der Projektliste ist die jüngste Änderungszeit von `projekt.json`, `aenderungen` und `stand`. (3) Der Ordner-Browser kennt Projekte an `projekt.json`; ein Objektordner mit «enthält Projekt» öffnet das Projekt unter `c_Pläne und Modelle\e_KICAD`. (4) Überschriften des Startbildschirms sind grösser (14 und 20 px) als die 12 px der Bedienoberfläche, Kacheln 16:10 und mindestens 236 px breit. (5) Eine Familie öffnet das Projekt der letzten Änderung (Ordner aus `log.json` der Bibliothek) und darin den Editor; fehlt die Familie im Projekt, gibt es einen Hinweis. (6) Bei offenem Projekt ruft «Bibliothek …» die Aktion `familie.explorer` (C3) auf, sonst zeigt es die Bibliothek nur lesend.

**Prüfen:** `node --test js/oberflaeche/start.test.mjs`; `dotnet test tests/Hlks.Web.Tests --filter "StartApiTests|ZuletztListeTests"`; Browser `SHOTS=<Ordner> node js/oberflaeche/start.browser.mjs` (legt unter `%TEMP%\HLKS_CAD\s1_test` drei Beispielprojekte, eine Bibliothek und die Liste an und startet einen Server auf Port 5168 mit `HLKS_TEST=1`, `HLKS_PROJEKTABLAGE`, `HLKS_FAMILIENBIBLIOTHEK`, `HLKS_ZULETZT`; nie die echte Ablage auf W:).

## Menüband passt sich der Breite an (Teil R, `entwurf/menueband-revit.md`)

Das Menüband rollt nie (kein `overflow-x: auto` mehr). Wie in Revit verkleinern sich die **Gruppen** stufenweise und klappen
zuletzt zu einem Knopf mit Flyout zusammen. Dateien: `js/oberflaeche/menueband-stufen.js` (reine Funktionen, in Node
getestet), `menueband.js` (Zeichnen, Messen, Flyout, Minimieren), `ansichtsreiter.js` (Ansichtsreiter), CSS-Block «Menüband» in
`css/oberflaeche.css`, Zeilen in `css/hlks.css`; Tests `menueband.test.mjs` (Node), `menueband.browser.mjs` (Edge, Port 5171).

**Stufen je Gruppe:** **G** gross (32-px-Symbol, Text darunter; Aktionen mit `groesse: 'klein'` stehen wie bisher als Spalten
zu je drei daneben) · **M** mittel (16 px + Text, drei übereinander, Klasse `mittel`) · **K** klein (nur 16-px-Symbol, drei
übereinander, Klasse `klein`; Text als Tooltip `title` und `aria-label`, mit Kürzel und Sperrhinweis wie bisher) · **Z**
zusammengeklappt (Knopf `.mb-zu` mit Gruppensymbol, Name und ▾; Klick öffnet die Gruppe als `.mb-flyout` in Stufe G unter dem
Knopf). Das Symbol der zusammengeklappten Gruppe ist das der ersten `immerGross`-Aktion, sonst der ersten grossen, sonst der
ersten Aktion der Gruppe (Annahme; Teil S kann eigene Gruppensymbole liefern).

**Reihenfolge (`stufenWaehlen`):** Breiten rein, Stufen raus. Passt alles in G, bleibt es so. Sonst drei Durchgänge, je von
rechts nach links: Durchgang 1 bringt jede Gruppe höchstens auf M, Durchgang 2 auf K, Durchgang 3 auf Z; nach jedem einzelnen
Schritt wird geprüft, ob es passt – die erste Kombination, die passt, gilt. Die erste Gruppe eines Registers wird also in jedem
Durchgang zuletzt verkleinert, und keine Gruppe springt über eine Stufe. Stufen, die nicht mindestens 1 px schmaler machen
(M bei nur einem Knopf, Z bei langem Namen), werden übersprungen; die Gruppe wartet dann auf den nächsten Durchgang. Mehr Platz
ergibt nie eine kleinere Stufe (Test), dasselbe Ergebnis bei gleichen Breiten (Test, keine Zufälle, Eingabe unverändert).

**`immerGross`:** Aktionen, die in M und K gross (32 px mit Text) bleiben, bis ihre Gruppe zu Z zusammenklappt. Gesetzt wird es
zentral in `aktionen.js` (Liste `IMMER_GROSS`), Dokumentation in `register.js`: `aendern.auswaehlen` («Ändern»),
`arch.wand` (Wand), `gt.rohr` (Rohr, PI), `gt.kanal` (Kanal, DT), `familie.platzieren` (CM). Eine Gruppe nur aus `immerGross`-Aktionen
sieht in G, M und K gleich aus (M und K machen sie nicht schmaler) und klappt erst in Durchgang 3 zu; in «Leitungen» (Rohr, Kanal, Formteil)
schrumpft nur das gesperrte Formteil.

**Messung:** je Gruppe und Stufe wird die Breite einmal in einer unsichtbaren Messfläche (`.mb-messung`, eine einzige
Layoutberechnung) gemessen und zwischengespeichert; Schlüssel: Register, Design (hell/dunkel), Schriftfamilie und -grösse,
Inhalt der Gruppe (Aktions-Ids, Titel, Symbole, Grössen, `immerGross`, Ordner bei «Zuletzt») und Stufe. Ein `ResizeObserver`
auf dem Band prüft bei jeder Breitenänderung die Stufen; **neu gezeichnet wird nur, wenn sich eine Stufe oder der Inhalt
ändert** (Test: Gebäudetechnik 1700 → 1300 px zeichnet kein einziges Mal neu; Wechsel hell/dunkel bei gleichen Stufen auch
nicht). Das gilt für alle Register, auch das Kontextregister «Ändern | Klasse» und «Familie»; «Datei» hat «Zuletzt geöffnet» als
Gruppe mit Knöpfen, die einen Projektordner statt einer Aktion tragen (in K nur das Symbol, Tooltip = Name und Ordner).

**Flyout:** schliesst bei Klick ausserhalb, Esc (der Esc geht nicht zusätzlich an die Kürzel), erneutem Klick auf den Knopf und
beim **Ausführen** eines verfügbaren Knopfs. Ein gesperrter Knopf im Flyout zeigt wie überall Tooltip mit Sperrhinweis und beim Klick
die Meldung, das Flyout bleibt offen. `zustandAnwenden` (gesperrt, gedrückt, Tooltip) gilt für Band, Flyout und Schnellzugriff; die
Tastenkürzel hängen nur am Aktionsregister und gelten in allen Stufen. Per Tastatur geöffnet (Enter) springt der Fokus auf den ersten
verfügbaren Knopf.

**Notfall «» weitere»:** Reicht «alles auf der letzten Stufe» nicht, wandern die Gruppen von rechts in ein Flyout «» weitere» am
Ende des Bands (Gruppen dort in Stufe G, umgebrochen); im Extremfall steht nur noch dieser Knopf.

**Menüband minimieren:** Knopf ⌃ am rechten Ende der Registerzeile schaltet voll → nur Gruppentitel → nur Register → voll. Doppelklick
auf ein Register wechselt zwischen voll und dem zuletzt gewählten minimierten Zustand (Vorgabe «nur Register»). Die Höhe der Bandzeile
folgt `body[data-menueband]` (`--hoehe-menueband-titel` 41 px, `--hoehe-menueband-register` 23 px, voll 111 px), die Ansichten rücken nach. Minimiert öffnet ein Klick auf ein
Register das Band als Überlagerung (über der Arbeitsfläche, verschiebt nichts); sie schliesst bei Klick ausserhalb, Esc, erneutem Klick auf
das Register und beim Ausführen. «Nur Gruppentitel»: jeder Titel öffnet seine Gruppe als Flyout. Gemerkt unter `menuebandMinimiert` und
`menuebandMinLetzte` (wie das aktive Register). Die Registerreiter selbst werden beim Klick nicht neu gezeichnet, solange sich ihre Menge nicht ändert
(sonst entsteht kein Doppelklick).

**Ansichtsreiter (`ansichtsreiter.js`):** kein Rollbalken. Passen nicht alle, werden sie schmaler (`reiterPlan`: kurze bleiben, die langen
teilen sich den Rest zu gleicher Breite, Titel kürzt mit «…»), nie unter **96 px** Mindestbreite; danach steht rechts «▾ n weitere» mit
der Liste **aller** offenen Ansichten (aktive markiert, Wahl aktiviert, schliesst bei Esc und Klick ausserhalb). Der aktive Reiter bleibt
immer sichtbar (er tritt an die Stelle des letzten sichtbaren). Die Reiter legt weiter `fenster.js` an; das Modul beobachtet
`#ansichtsreiter` (ResizeObserver, MutationObserver) und ändert nur Klassen und Breiten.

**Optionsleiste:** `flex-wrap: wrap`, Zeilenhöhe mindestens 24 px (Gitterzeile `minmax(24px, auto)`); Texte brechen um, Felder und
Beschriftungen bleiben zusammen. Der Hinweis in der Registerzeile kürzt nicht mehr mit «…».

## Ansichten frei anordnen und eigenes Fenster (Teil W2, `entwurf/fenster-revit.md`)

Dateien: `js/oberflaeche/anordnung.js` (reine Daten und Rechnung, in Node getestet), `anordnung-ziehen.js` (Zeigerbedienung: Reiter und Trenner ziehen, Andockziele),
`zweitfenster.js` (eigenes Fenster, `BroadcastChannel`), `js/fenster.js` (zeichnet, merkt, stellt wieder her), `css/anordnung.css`, Tests `anordnung.test.mjs` (Node) und
`anordnung.browser.mjs` (Edge über CDP). `ansichtsreiter.js` läuft je Gruppe; `fenster.js` behält seine Schnittstelle (`ansichtOeffnen`, `aktive`, `alle`, `aktivePlanansicht`, `schliessen`,
`aktivieren`, `nebeneinander`, `alsReiter`, `fenstermodus`, `zoomAlles`, `zoomAlleAnsichten`); neu: `wiederherstellen()` (ersetzt `gemerkteOeffnen`), `eigenesFensterOeffnen(kz)`, `andocken(id, ort)`,
`zurueckInsHauptfenster()`, `imZweitfenster()`, `anordnungDaten()`, `rechtecke()`.

- **Datenformat** (Baum aus Gruppen und Teilungen): `Gruppe { art: 'gruppe', id: 'g1', ansichten: [name …], aktiv: name }` (eigene Reiterleiste, eine Ansicht davon sichtbar), `Teilung { art: 'teilung',
  richtung: 'nebeneinander' | 'untereinander', kinder: [Knoten …], anteile: [0.5, 0.5] }` (Anteile > 0, Summe 1, je mindestens 0.05), `Anordnung { version: 1, wurzel: Knoten | null, aktiveGruppe: id | null,
  ausgelagert: [name …] }` (`ausgelagert` = Ansichten, die in einem eigenen Fenster offen sind). Im Speicher heissen die Namen Ids der Ansichten, **gemerkt werden Kennzeichen** (`umbenennen`). Jede Funktion ist rein
  und liefert eine saubere Anordnung (keine leere Gruppe, keine Teilung mit einem Kind, keine gleichrichtige Teilung in einer Teilung, jede Ansicht einmal): `hinzufuegen`, `entfernen`, `auslagern`,
  `aktivieren`, `gruppeAktivieren`, `verschieben` (Ziel `{ gruppe, ort: 'mitte'|'links'|'rechts'|'oben'|'unten', index? }` oder `{ rand }`), `trennerSetzen`, `kacheln`, `zusammenlegen`,
  `normalisieren` (macht aus Müll, unbekannten Ansichten, falschen Anteilen, zu tiefer Verschachtelung wieder eine gültige Anordnung), `layoutBerechnen` (Rechtecke in ganzen px, lückenlos), `dropZiel`
  (was passiert beim Loslassen bei x, y) und `zielAnwenden`.
- **Aufbau im DOM:** `#ansichtsflaeche` ist die Wurzel; **alle** Kacheln, Reiterleisten (`.reiterleiste`, 22 px) und Trenner (`.ag-trenner`, 4 px) liegen flach darin und werden absolut gesetzt, beim Umordnen wird
  nichts umgehängt (ein PDF im Rahmen lädt nicht neu). Die Leiste der **ersten Gruppe trägt die Id `ansichtsreiter`** (bestehende Tests); ohne offene Ansicht steht eine leere Leiste mit dieser Id da. Aktiv ist genau
  eine Ansicht: Klasse `aktiv` an Kachel und Reiter (die Plan-Ansichten und `aktivePlanansicht()` fragen sie ab), `gruppen-aktiv` am sichtbaren Reiter jeder Gruppe, `aktive-gruppe` an der Leiste. Ein Klick in eine Kachel
  macht ihre Gruppe aktiv. Werkzeuge (Zeichnen, Fangen, Leitung) arbeiten in der aktiven Ansicht der aktiven Gruppe.
- **Bedienung:** Reiter ziehen (ab 6 px): auf der Fläche erscheinen die Andockziele der Gruppe unter dem Zeiger (Kreuz: Mitte, links, rechts, oben, unten), drei Streifen am Rand der ganzen Fläche (links, rechts, unten,
  14 px) und eine Vorschau; über einer Reiterleiste ein Strich an der Einfügestelle. Die Randzonen einer Gruppe (äusserstes Viertel) wirken wie die Ziele. Loslassen setzt um, **Esc bricht ab**. Die einzige Ansicht einer
  Gruppe lässt sich nicht an die eigene Gruppe andocken. Trenner ziehen: live, mindestens 90 px je Gruppe; gemerkt beim Loslassen. Kontextmenü am Reiter: «In eigenem Fenster öffnen», «Nach rechts/unten teilen», «Schliessen».
  Register Ansicht › Fenster: **«Fenster nebeneinander» (WT) = Kacheln** (⌈√n⌉ Spalten, gleich gross), **«Fenster als Reiter» (WC)** legt alles in eine Gruppe, **«In eigenem Fenster öffnen»**. Öffnet man eine Ansicht,
  kommt sie als Reiter in die aktive Gruppe. «▾ n weitere» (Teil R) gilt je Gruppe (`ansichtsreiter.einrichten` je Leiste, der sichtbare Reiter der Gruppe bleibt immer sichtbar).
- **Merken:** `localStorage`-Schlüssel `hlks.anordnung.<ort des Projekts, klein>` (je Projekt und Rechner), verzögert 250 ms und sofort bei `pagehide`; nie eine leere Anordnung (der Projektwechsel schliesst erst alle
  Ansichten). Beim Start: gemerkte Anordnung normalisieren, unbekannte Kennzeichen entfallen, sonst Rückfall auf die frühere Liste `hlks.offeneAnsichten` (eine Gruppe), sonst der erste Grundriss.
- **Eigenes Fenster** (`zweitfenster.js`): `window.open('/?ansicht=<kz>&fenster=<n>', 'hlks-fenster-<n>', 'popup=yes,…')` an der Stelle des Loslassens (n = 2 … 9). Das zweite Fenster ist dieselbe Oberfläche im Modus
  «nur Ansicht» (`body[data-fenster]`): Kopf mit «↩ Zurück ins Hauptfenster», Optionsleiste, Ansichtsfläche, Statusleiste; **Menüband und Seitenleiste ausgeblendet** (Kürzel und Werkzeuge der Ansicht gehen weiter; ein
  Reiter lässt sich dort nicht auslagern). **Live-Abgleich:** Änderungen, Rückgängig und alles Modellseitige über den Ereignisstrom des Servers (`/api/ereignisse`, je Browserfenster ein Strom; der Server brauchte keine Änderung:
  gleicher Akteur und Rechner, Reservierungen gelten für beide Fenster), **Auswahl** und die Frage «wer hält welche Ansicht» über `BroadcastChannel('hlks-fenster')`. Nachrichten: `auswahl`, `wer`/`da` (Hauptfenster fragt beim
  Start, welche Zweitfenster leben; deren Ansichten bleiben dort und werden nicht doppelt geöffnet), `weg`/`zurueck` (**Schliessen oder Neuladen des Zweitfensters gibt die Ansicht zurück**: das Hauptfenster wartet 1.2 s, ob sie
  mit `da` wieder auftaucht (Neuladen), sonst öffnet es sie als Reiter; der Knopf «Zurück ins Hauptfenster» tut es sofort), `fokus`. Schliesst man im Zweitfenster die letzte Ansicht (✕) oder wechselt das Projekt, schliesst
  das Fenster. Ist ein Pop-up blockiert, meldet das Hauptfenster es und behält die Ansicht. Auch das Ziehen eines Reiters **aus dem Browserfenster heraus** öffnet ein eigenes Fenster (Schatten «Loslassen: eigenes Fenster»).
- **Prüfen:** `node --test js/oberflaeche/anordnung.test.mjs` (55 Tests: Baum, Ziele, Rechtecke, Merken/Laden mit ungültigen Werten, Adresse des Zweitfensters, CSS-Einbindung); Browser (Edge headless über CDP, eigener
  Server, Port 5178, CDP 9510): `node js/oberflaeche/anordnung.browser.mjs` (87 Prüfungen: Ziehen mit der Maus, Andockziele, Trenner, drei Gruppen, Werkzeug-Ansicht je Gruppe, Neuladen, kaputter Eintrag, Kacheln,
  «weitere» je Gruppe, Zweitfenster mit Live-Abgleich von Auswahl und Änderung in beide Richtungen, Rückgängig, Rückgabe, Neuladen von Haupt- und Zweitfenster, Wand zeichnen im Zweitfenster; Bilder `%TEMP%\HLKS_CAD\w2_*.png`).
- **Grenzen und Annahmen:** Im Raster (`hlks.css`) ist nur die Ansichtsfläche geändert (`#ansichtsflaeche` ohne Raster, `#ansichtsbereich` Zeilen `minmax(0, 1fr) auto`); die Seitenbereiche und Paletten gehören W1.
  Im Zweitfenster blendet `css/anordnung.css` Menüband, `#seitenleiste` und `#seitenleiste-griff` aus und setzt die Spalten von `#arbeitsbereich` auf `0 0 1fr` – kommen im Raster weitere Seitenbereiche dazu, dort mit
  eintragen. Ein Reiter lässt sich nicht zwischen zwei Browserfenstern ziehen (nur auslagern und zurückgeben).

**Prüfen:** `node --test js/oberflaeche/menueband.test.mjs`; Browser `node js/oberflaeche/menueband.browser.mjs` (frisches Beispielprojekt, Server auf
5171 mit `HLKS_TEST=1` und Ablagen unter `%TEMP%`; Bilder `r_*.png` unter `%TEMP%\HLKS_CAD`). Der Test liefert die Oberfläche aus einer Kopie (`HLKS_WEBROOT`) aus: der Dateiwächter des Servers
stürzt ab (`PhysicalFilesWatcher.OnRenamed`), wenn im Quellordner Dateien umbenannt werden – das beendet gelegentlich auch andere Browser-Reihen mitten im Lauf. **Annahmen:** (1) Drei Durchgänge statt «eine Gruppe
bis Z, dann die nächste» (Office-Empfehlung: erst alle Gruppen mittel, dann klein, zuletzt zusammenklappen). (2) Mindestbreite eines Reiters 96 px,
Mindestbreite einer Stufe 1 px Gewinn. (3) Doppelklick auf ein Register merkt sich den letzten minimierten Zustand. (4) Reichen die Registerreiter selbst nicht
(unter etwa 700 px), werden ihre Namen mit «…» gekürzt (kein Umbruch, kein Rollbalken).


## Projektbrowser: umschaltbare Gliederung (Teil G1, `entwurf/projektbrowser-gliederung.md`)

Der Projektbrowser hat oben ein **Auswahlfeld** (Symbol + Name): Gebäude (wie bisher), Sanitär, Heizung, Lüftung, Koordination, Energie und Umwelt. Die gewählte ist die **primäre Gliederung des Benutzers**
und gilt in allen Projekten und auf allen seinen Rechnern; gewechselt wird nur von Hand. Vorgabe ohne Einstellung ist «Gebäude» (auch für Tests).

- **Eine Tabelle** (`js/oberflaeche/projektbrowser-gliederung.js`, `GLIEDERUNGEN`): je Gliederung die Äste in ihrer Reihenfolge (Quelle, Auswahl, Titel, Farbe). Eine weitere Gliederung (Kälte, Elektro/MSRL) ist ein Eintrag
  dort, ohne neuen Code (Test «Erweiterbar ohne neuen Code»). **«Weitere»** wird nicht beschrieben, sondern abgeleitet: alles, was kein Ast der Gliederung aufnimmt, steht eingeklappt darunter – die Summe der Elemente
  ist in jeder Gliederung die von «Gebäude» (Node-Test «Weitere ist vollständig»). Unter «Weitere» stehen zuerst die Äste der anderen Gewerke (je Gewerk eine Gruppe in dessen Farbe, «Ohne Gewerk» zuletzt), dann Vorlagen, Gebäude/Räume, Hülle, Unterlagen, Katalog.
- **Zuordnung zum Gewerk:** Klasse → `gewerk` aus dem Katalog; System → Systemtyp, sonst Medium; Strang → sein System; Anlage → die Gewerke ihrer Systeme (eine gemischte Anlage steht in jedem ihrer Gewerke, jedes System einmal);
  Ansicht → `gewerke` und die Systeme der Ansicht; Planblatt → Gewerk des Plansatzes (Abfrage `plansaetze`), sonst `gewerk`; Familie → Gewerk ihrer Kategorie. Ohne Gewerk → «Weitere».
- **Äste** (`projektbrowser-aeste.js`, rein, in Node getestet): die Knoten der Quellen wie bisher (`projektbrowser.js` zeichnet und bedient nur). Zahlen hinter den Ästen; die **Suche** durchsucht die ganze Gliederung samt «Weitere» und klappt Treffer auf;
  der **Aufklappzustand** wird je Gliederung gemerkt (Browser-Speicher `browserOffen` für Gebäude wie bisher, sonst `browserOffen.<gliederung>`).
- **Koordination:** Kollisionen nach Status, darunter das Paar; Aussparungen nach Geschoss (oben zuerst), darunter der Status; Typenliste; Unterlagen (IFC); Pläne und Ansichten der Koordination. Die Daten kommen über **dieselben Abfragen** wie die
  Palette (`kollisionen` mit derselben Sicht ohne nicht geprüfte Paare, `aussparungsliste`, `typmaterial`; `koordination-daten.js`) und werden live nachgeführt (Ereignis `modell`, entprellt), solange die Gliederung gewählt ist.
  Klick wählt, **Doppelklick** zeigt Kollision/Aussparung im Grundriss (wie ein Klick in der Palette), Doppelklick auf einen Ast öffnet Liste bzw. Typenliste; Kontextmenü: Im Grundriss zeigen, In 3D zeigen, Auswählen, Liste.
- **Farben der Gewerke** (`kataloge/gewerke.json`, über die Abfrage `katalog` in `gewerke` und `gewerkeDunkelDeckkraft`; keine Farbe im Code): Ast bzw. Gruppenkopf eines Gewerks in seinem Pastellton, schmaler Streifen links in der Koordinationsfarbe; dunkel: Pastellton
  mit `dunkelDeckkraft` über dem Grund. Einzelne Elemente unter einem Gewerk-Ast werden nicht gefärbt, nur ein 2-px-Streifen zieht sich durch den Ast (`.pb-streifen`). Die Gruppen «Pläne» (Gebäude), die Kategorien der Familien und Anlagen mit nur einem Gewerk tragen die Farbe ebenfalls. CSS: `css/projektbrowser.css`.
- **Benutzereinstellungen:** `GET /api/benutzer/einstellungen` → `{ werte, zeiten, rechner, quelle, ort }`, `POST /api/benutzer/einstellungen { schluessel, wert }` (wert `null` setzt zurück). Schlüssel heute `projektbrowser.gliederung`.
  Ablage wie «zuletzt» (gemeinsamer Unterbau `RechnerAblage.cs`): `%OneDrive%\HLKS-CAD\einstellungen\<Rechner>.json` je Rechner eine Datei, Vereinigung nach Zeit je Schlüssel (der neueste Wert gilt), ohne OneDrive `%APPDATA%\HLKS-CAD\einstellungen.json`;
  Umleitung für Tests `HLKS_BENUTZER` (Ordner). **Im Testlauf (`HLKS_TEST`) ohne Umleitung nach `%TEMP%` wird weder gelesen noch geschrieben** (leere Einstellungen, Schreiben mit 400 abgelehnt). Die Oberfläche liest beim Start und nach jedem Laden eines Projekts, schreibt beim Wechsel (`benutzer.js`).

**Prüfen:** `node --test js/oberflaeche/projektbrowser.test.mjs` (Tabelle, Gewerk-Zuordnung, Vollständigkeit, Suche, Koordination, Farben, Kontrast); Server: `BenutzerEinstellungenTests.cs` (Vereinigung zweier Rechner, Umleitung, Fehlerfälle, Endpunkte);
Browser (Edge headless über CDP, eigener Server, Port 5176, Build-Ordner `%USERPROFILE%\HLKS_CAD_build\g2`): `node js/oberflaeche/projektbrowser.browser.mjs` – Umschalten, gemerkt nach Neuladen, zweiter Rechner, anderer Benutzer, Suche, Doppelklick, Kontextmenü, live, hell und dunkel; Bilder `%TEMP%\HLKS_CAD\g1_*.png`.

### Mehrere Gliederungen zugleich (Teil G2, Nachtrag in `entwurf/projektbrowser-gliederung.md`)

Das Auswahlfeld ist eine **Liste mit Häkchen** (`role="listbox"`, `aria-multiselectable`; sie bleibt nach einem Klick offen, Escape oder ein Klick daneben schliesst sie; Tastatur: Pfeiltasten, Leertaste bzw. Enter setzen das Häkchen).

- **Regeln** (`projektbrowser-gliederung.js`, rein, in Node getestet): «Gebäude» ist `exklusiv` (zeigt ohnehin alles): es hebt alle übrigen auf, jede Fachgliederung hebt «Gebäude» auf; mindestens eine bleibt gewählt (das letzte Häkchen lässt sich nicht entfernen).
  `gliederungenNormieren` macht aus dem gespeicherten Wert die Liste (alter Einzeltext = Liste mit einem Eintrag, Reihenfolge der Tabelle, Unbekannte entfallen), `gliederungUmschalten` setzt oder entfernt ein Häkchen, `gliederungAnzeige` liefert den Text im Feld («Sanitär», «Sanitär + Heizung», «Sanitär + 2»).
- **Blöcke** (`baumBauenListe` in `projektbrowser-aeste.js`): mehrere Fachgliederungen stehen **untereinander in Listenreihenfolge**, je Block ein Kopf in der Gewerkfarbe (Schlüssel `b:<kennung>`, anfangs offen) mit seinen Fachästen (Schlüssel `b:<kennung>:<ast>`). Äste ohne Auswahl, die nicht an ein Gewerk gebunden sind
  («Räume»; auch die Gebäude-Zeile von «Energie und Umwelt», wenn ein anderer Block «Räume» hat), stehen **einmal nach den Blöcken**, ungefärbt. «Weitere» wird aus allen Ästen aller gewählten Blöcke abgeleitet: nur, was in keinem Block vorkommt. Ein Element steht nur dann in mehreren Blöcken, wenn
  seine Merkmale zur Auswahl beider passen (eine Ansicht mit Sanitär und Heizung; eine gemischte Anlage mit je ihrem System). Die Summe aller Elemente ist in jeder Kombination die von «Gebäude» (Node-Test über alle Kombinationen).
- **Einstellung:** `projektbrowser.gliederung` ist jetzt eine **Liste** von Kennungen (`["sanitaer","heizung"]`); der Server prüft nur die Form (nicht leer, höchstens 16, jede Kennung einmal, Kleinbuchstaben) und nimmt einen alten Einzeltext weiter an. Aufklappzustand mehrerer Gliederungen: Browser-Speicher `browserOffen.mehrere`
  (dazu `browserGesehen.mehrere`: ein dazugewählter Block ist anfangs offen, ein zugeklappter bleibt zu). Das Feld trägt bei mehreren Gliederungen einen Streifen mit einem Abschnitt je Koordinationsfarbe (`.pb-mehrere`).
- **Prüfen:** Node `projektbrowser.test.mjs` (G2-Tests am Ende), Server `BenutzerEinstellungenTests.cs` (Liste als Wert), Browser `projektbrowser.browser.mjs` (Abschnitte «G2: …», Port 5176, Bilder `%TEMP%\HLKS_CAD\g2_*.png`).
## Fenster: Dialoge und Paletten (Teil W1, `entwurf/fenster-revit.md`)

Christian, 19.10.2026: «Es wäre gut, wenn die Verschiebbarkeit der Fenster umgesetzt würde. Sonst ist es schwer zum Testen.» Keine Fachlogik: alles ist Darstellung und wird je Rechner im Browser gemerkt (`merken`, localStorage).

**Dialoge** (alle über `zeigen` in `dialoge.js`, nie ein eigenes Fenster; Verhalten in `dialoge-fenster.js`):
- An der **Titelleiste** verschiebbar, am **Rand und an den Ecken** in der Grösse änderbar (acht Griffe; Mindestgrösse = Titel mit Knöpfen, Knopfleiste und Fehlerzeile lesbar, `mindestGroesse`), immer **ganz im sichtbaren Bereich** (`begrenzen`; beim Verkleinern des Browserfensters und wenn der Inhalt wächst werden sie zurückgeholt).
- **Ohne gemerkte Lage in der Mitte wie bisher** (Raster von `#dialogschicht`, wächst mit dem Inhalt). Erst nach dem Ziehen gilt die freie Lage (Klasse `verschoben`, `left`/`top`). **Doppelklick auf die Titelleiste** = zurück zur Mitte (die Grösse bleibt).
- **Nicht mehr abgedunkelt**, der Plan bleibt sichtbar; die Schicht fängt weiter alle Klicks ab (modal). Liegen mehrere Dialoge übereinander, ist nur der oberste bedienbar (die übrigen `inert`, Klasse `gesperrt`).
- Knopf **«Einklappen»** (▴/▾) in der Titelleiste: der Dialog schrumpft auf die Titelleiste (Klasse `eingeklappt`, Stelle und Breite bleiben, auch eingeklappt verschiebbar); der eingeklappte Zustand wird nicht gemerkt.
- **Gemerkt je Dialogart** (Schlüssel `dialoge`: `{ "<Art>": { x, y, b, h } }`; x, y fehlen = Mitte, b, h fehlen = Grösse wie vorgegeben). Die Art ist `zeigen({ art })`, sonst der Titel ohne Namen in «…» und ohne Ziffern (`dialogArt` in `dialog-merken.js`); höchstens 80 Arten, ungültige Werte werden beim Lesen verworfen (`bereinigen`).

**Paletten** (Verwaltung `paletten.js`, Daten `paletten-daten.js`, Andockziele `paletten-ziele.js`; Eigenschaften, Projektbrowser und Koordination melden sich mit `anmelden` an):
- Drei **Andockbereiche**: `links` (`#seitenleiste`) und `rechts` (`#dock-rechts`) als Spalten, `unten` (`#dock-unten`, unter der Ansichtsfläche, die Gruppen nebeneinander). Leere Bereiche und ihre Griffe nehmen keinen Platz. Breite bzw. Höhe am Griff ziehbar (`--dock-links`, `--dock-rechts`, `--dock-unten` am Raster `#arbeitsbereich`, `css/fensterrahmen.css`); zu grosse Werte werden dem Fenster angepasst, die Ansichtsfläche behält mindestens 260 px. Die **Ansichtsfläche** (`#ansichtsbereich`) ist ein eigener Bereich im Raster (Spalte 3, Zeile 1); W2 füllt ihn.
- In einem Bereich stehen Paletten **untereinander** (Trenner ziehbar, `trennerSetzen`) oder **als Reiter** in einer Gruppe (Kopf mit Reitern, ✕ blendet die aktive aus). Ziehen am **Palettenkopf** (ab 5 px): beim Ziehen erscheinen die **Andockziele** (`#palettenziele`): je Gruppe «Darüber» (oberer Rand, im Bereich unten «Links davon»), «Als Reiter» (Mitte), «Darunter» («Rechts davon»), je leerem Bereich eine Fläche («Links/Rechts/Unten andocken»); das Ziel unter der Maus ist hervorgehoben, ein Umriss zeigt, wo die Palette sonst schwebt. **Loslassen ausserhalb jeder Zone = schwebend**; Esc bricht ab. Ein Reiter lässt sich einzeln herausziehen, ein Zug am Kopf einer Reitergruppe nimmt alle mit. Doppelklick auf den Kopf löst bzw. dockt zurück (Vorgabebereich).
- **Schwebende Paletten** liegen in `#palettenschicht` über der Zeichenfläche (unter den Dialogen), am Kopf verschiebbar, an Rand und Ecken in der Grösse änderbar (`fensterbewegung.js`, dieselben Regeln wie bei Dialogen), immer ganz im Fenster; ein Klick holt sie nach vorn.
- **Ein- und Ausblenden:** Register **Ansicht › Fenster › Paletten** (`aktionen-paletten.js`: ein Knopf öffnet wie in Revit eine Liste mit allen angemeldeten Paletten und Häkchen, dazu «Paletten zurücksetzen»; kein eigener Gruppenkasten, weil das Register Ansicht schon voll ist, siehe `menueband.browser.mjs`), ✕ am Palettenkopf, `aendern.eigenschaften` (PP) und `kontext.eigenschaften`. Eine ausgeblendete Palette kommt an ihren letzten Ort zurück. **Paletten zurücksetzen** stellt die Vorgabe her: Eigenschaften oben, Projektbrowser unten, links (wie vor W1); eine geöffnete Koordination bleibt offen und steht wieder unten.
- **Neue Palette anmelden** (z. B. KI-Palette 3c): `anmelden({ id, titel, ort, reihenfolge, vorgabeSichtbar, fluechtig, lazy, scroll, elementId, inhaltId, symbol, bauen(slot), abbauen(slot), oeffnen(), aktiv() })`. `bauen` füllt den Inhaltsknoten (einmal; bei `lazy` bei jedem Einblenden, dann räumt `abbauen` auf). `fluechtig` = wird nach dem Neuladen nicht sichtbar wiederhergestellt (braucht ein offenes Projekt, so die Koordination). Der Eintrag unter Ansicht › Paletten entsteht von selbst; sonst ist nichts zu tun. Kopf, Reiter, Ziehen, Zonen, Grössen und Merken liefert die Verwaltung.
- **Datenformat** (Schlüssel `paletten`, Version 1; jede Palette genau einmal):
  `{ version: 1, bereiche: { links: { groesse: 280|null, gruppen: [Gruppe] }, rechts: { … }, unten: { … } }, schwebend: [Gruppe + { x, y, b, h }], versteckt: ['koordination'], zuletzt: { koordination: { bereich: 'unten' } | { schwebend: { x, y, b, h } } } }`, `Gruppe = { paletten: ['eigenschaften', …], aktiv: 'eigenschaften', anteil: 1 }` (mehrere Paletten = Reiter; `anteil` = Gewicht der Gruppe im Bereich). Beim Laden (`normalisieren`) fallen unbekannte und doppelte Paletten, ungültige Zahlen und Gruppen weg, neu angemeldete Paletten kommen an ihren Vorgabeort; nichts Brauchbares = Vorgabe. Ohne gemerkte Anordnung werden die früheren Werte der festen Seitenleiste (`seitenleisteBreite`, `seitenleisteAnteil`, `paletteSichtbar`) übernommen.
- Beim alten `#seitenleiste.ohne-eigenschaften` (Bilder des Projektbrowsers) bleibt die Wirkung erhalten; sonst gibt es keinen Sonderweg mehr (`layout.js` und `paletteAnwenden` sind entfallen).

**Prüfen:** Node `paletten.test.mjs` (Anordnung als Daten: Vorgabe, andocken, lösen, Reiter, ein-/ausblenden, zurücksetzen, merken/laden, ungültige Werte; Andockziele; Verdrahtung) und `dialog-fenster.test.mjs` (Begrenzung im sichtbaren Bereich, Verschieben, Grösse, gemerkte Werte, Verdrahtung); Browser `dialoge-paletten.browser.mjs` (Port 5177, `HLKS_W1_PORT`; echte Mausbedienung per CDP, Bilder `w1_*_hell.png` und `w1_*_dunkel.png`). Die ✕-Schaltfläche der Koordination steht jetzt am Palettenkopf (`#palette-koordination .pal-zu`); `koordination.browser.mjs` und `projektbrowser.browser.mjs` klicken sie dort.

## Hilfe (F1)

Wie in Revit (InfoCenter «?» oben rechts, F1 über einem Befehl): Die Hilfe ist die **Bedienungsanleitung als Webseite, die KICAD selbst unter `/hilfe/` ausliefert** (Entscheid Christian Steiner 19.10.2026: ohne Anmeldung, ohne Internet; Häkchen und Notizen speichert KICAD je Benutzer über OneDrive; Bilder bleiben lokal auf diesem Rechner; die Claude-Seite entfällt). Die Oberfläche öffnet sie nur. Keine Logik in der Oberfläche über das Öffnen hinaus.

- **Adresse und Ordner:** Die Oberfläche öffnet `/hilfe/` auf demselben Server (`window.open('/hilfe/' + Sprungziel, 'kicad-hilfe')`). `GET /api/start/info` liefert `anleitung { url: '/hilfe/', vorhanden, hinweis, ordner }`; `vorhanden` heisst: `index.html` liegt im Anleitungsordner (`StartDienst.Info`, `AnleitungDienst.Vorhanden`). Der **Ordner** steht im Bürowert `kataloge/buero.json › anleitung.ordner` (`%USERPROFILE%\HLKS_CAD_anleitung`, `%…%` wird aufgelöst; lokal, nie OneDrive, weil die Bilder echte Projekte zeigen); die Umgebungsvariable `HLKS_ANLEITUNG` übersteuert ihn (Tests). Gebaut wird er mit `werkzeuge/anleitung_bauen.mjs` (der Desktop-Start `kicad_starten.ps1` baut ihn zum geprüften Stand mit, `stand.txt` merkt den Stand). `hilfe-oberflaeche.js` fragt beim Start einmal und merkt nur eine vorhandene Anleitung; wird sie später gebaut, gilt sie beim nächsten Klick oder F1.
- **Auslieferung** (`AnleitungEndpunkte.cs`, `AnleitungDienst.cs`): `GET /hilfe/` und `/hilfe/<datei>` liefern `index.html` und `bilder/*` (html, webp, png, jpg, svg, css, js, json; `Cache-Control: no-cache`), nur GET, nur Dateien **innerhalb** des Ordners (`..`, absolute Pfade, Laufwerke, Datenströme, ungültige Zeichen und Junctions/Symlinks → 400; fehlende Datei oder nicht freigegebener Typ → 404). `/hilfe` leitet auf `/hilfe/` weiter. Gibt es den Ordner oder `index.html` nicht, zeigt `/hilfe/` eine kleine Seite «Die Bedienungsanleitung ist auf diesem Rechner noch nicht erstellt» mit dem Befehl zum Erzeugen.
- **Prüfstand** (Häkchen ✓/✗ und Notizen der Anleitungsseite; `Pruefstand` in `AnleitungDienst.cs`): `GET /api/hilfe/pruefung` → `{ quelle, ort, rechner, eintraege: { <funktionId>: { titel, punkte, notiz, stand, geaendert, texte, rechner } } }`; `PUT /api/hilfe/pruefung/{id}` speichert den Eintrag einer Funktion (id ≤ 120 Zeichen aus `[A-Za-z0-9_.~:@+-]`, ≤ 64 Punkte, Notiz ≤ 4000 Zeichen, Körper ≤ 64 KB, sonst 400; `geaendert` setzt der Server). Ablage wie die Benutzereinstellungen (`RechnerAblage`): `%OneDrive%\HLKS-CAD\anleitung\<Rechner>.json`, je Rechner eine Datei; gelesen werden alle, je Funktion gewinnt der jüngste `geaendert`; ohne OneDrive `%APPDATA%\HLKS-CAD\anleitung.json`; Umleitung `HLKS_ANLEITUNG_PRUEFUNG`; im Testlauf ohne Umleitung nach `%TEMP%` weder gelesen noch geschrieben. Die Seite lädt beim Start, speichert nach 700 ms Ruhe je Funktion (eine Schreibung je Funktion zugleich) und fällt auf den Browserspeicher zurück, wenn sie als Datei geöffnet ist oder KICAD nicht antwortet («Nur in diesem Browser gespeichert»; bei Erfolg «Gespeichert in KICAD»).
- **Symbol «?»:** rechts in der Kopfleiste (`#kopf-rechts`, Knopf `#hilfe-knopf`), eigenes Symbol `anleitung` in `symbole.js` (Sprechblase mit Fragezeichen), Tooltip «Hilfe – Bedienungsanleitung (F1)». Der Kopf bleibt auch über dem Startbildschirm und im Zweitfenster sichtbar, dort steht es neben «Zurück ins Hauptfenster». Klick = Anleitung ohne Sprungziel. Kein neues Register, keine neue Gruppe im Menüband.
- **F1** (global, `keydown` in der Aufnahmephase mit `preventDefault()`, damit Edge nicht seine eigene Hilfe öffnet; Strg/Alt/Umschalt+F1 bleiben unberührt). Kontext in dieser Reihenfolge:
  1. **Dialog offen** (`zeigen` in `dialoge.js`, der oberste; `obersterDialogTitel()`) → `/hilfe/#s-<Dialogtitel>`.
  2. **Zeiger über einem Knopf des Menübands** (`.mb-knopf:hover`, auch Flyouts zusammengeklappter Gruppen, Kontextregister, Schnellzugriff) oder **Tastaturfokus** auf einem solchen Knopf (der Fokus eines Mausklicks zählt nicht, solange danach keine Taste gedrückt wurde) → `/hilfe/#k-<Register>~<Knopf>` mit Register und Knopftitel wie angezeigt (`knopfHilfe()` in `menueband.js`: Reiter des gewählten Registers, im Kontextregister «Ändern | Klasse»; Titel der Aktion; im Schnellzugriff das Register der Aktion). Die Einträge der Liste «Zuletzt geöffnet» (Ordner, keine Befehle) haben keinen Sprung.
  3. sonst die Anleitung ohne Sprungziel.
- **Fenster:** `window.open(adresse, 'kicad-hilfe')`, immer dasselbe. Liefert es `null` (Pop-up gesperrt): Meldung «Das Hilfefenster wurde blockiert – Adresse: …»; ist die Anleitung nicht erstellt (`vorhanden: false`): nur die Meldung «Die Bedienungsanleitung ist auf diesem Rechner noch nicht erstellt.», es öffnet sich nichts (beide über `meldung` in `dialoge.js`, Art Warnung).
- **Tooltips:** Jeder Knopf des Menübands (auch Schnellzugriff und Flyout) endet mit der Zeile «F1: Hilfe zu diesem Befehl» (`zustandAnwenden` in `menueband.js`, nach einem etwaigen Sperrhinweis).
- **Schlüssel** (`hilfe.js › hilfeSchluessel`, **Zeichen für Zeichen identisch** mit der Anleitungsseite `werkzeuge/anleitung/vorlage.html`; ein Node-Test vergleicht den Text): klein, ä→ae ö→oe ü→ue ß→ss, Akzente weg, alles andere zu «-». «Serien-PDF …» → `serien-pdf`, «Planblätter» → `planblaetter`. Die Seite springt bei `#k-…` zur Funktion (Register nur zur Unterscheidung, sonst nur der Knopf; `article.funktion.ziel`; tragen mehrere Funktionen denselben Knopf, markiert sie alle, zeigt die Suche nach dem Knopf und springt zur ersten) und fällt sonst auf die Suche zurück. Reine Funktionen (Schlüssel, Kontextwahl, Adressbau, Texte) stehen in `hilfe.js` ohne DOM, die Verdrahtung in `hilfe-oberflaeche.js`.

**Prüfen:** Node `node --test js/oberflaeche/hilfe.test.mjs` (Schlüssel, Kontext, Adressen `/hilfe/…`, fehlende Anleitung, Schlüsseltext gegen die Anleitungsseite); Web `AnleitungApiTests.cs` (`/api/start/info`: vorhanden, Ordner aus `buero.json` mit `%…%`, Umgebungsvariable, Testsperre), `AnleitungAuslieferungTests.cs` (Datei, Bild, Weiterleitung, fehlender Ordner, Pfad-Ausbruch, Junction, nur GET) und `PruefstandTests.cs` (Grenzen, Zusammenführen zweier Rechnerdateien, Testlauf-Sperre, GET/PUT); Browser (Edge headless über CDP, der Test baut die Anleitung mit `anleitung_bauen.mjs` unter `%TEMP%` und startet **zwei Server** – den zweiten mit nicht erstellter Anleitung –, Ports 5196/5197, `HLKS_H_PORT`, CDP 9540; für die Abläufe von F1 ist `window.open` ein Stub, der Abschnitt «Echte Hilfeseite» öffnet das Fenster wirklich und prüft Seite, Sprungziel, Häkchen und Notiz im Hilfefenster): `SHOTS=<Ordner> node js/oberflaeche/hilfe.browser.mjs` (Bilder `hilfe_*.png` und `hilfe_lokal_*.png`, Vorgabe `%TEMP%\HLKS_CAD`).

## Symbole im Revit-Stil (Teil S)

Zuschaltbarer zweiter Symbolsatz (`entwurf/menueband-revit.md`, Teil S): **eigene** mehrfarbige Zeichnungen in der Bildsprache von Revit. Es wurde nichts aus Revit oder von Autodesk übernommen (weder aus einer Installation noch aus Bildschirmfotos oder dem Internet, keine Pfade abgezeichnet): gleiche Bildidee je Befehl (Wand als isometrischer Block, Rohr als Zylinder in Medienfarbe, 3D-Würfel, Planblatt als Blatt), gleiche Grössen, ähnliche Farbgebung. **Probe**: die heutigen Strichsymbole bleiben die Vorgabe, bis Christian den Kontaktbogen freigibt.

- **Schalter:** Benutzereinstellung `oberflaeche.symbole` = `"strich"` (Vorgabe) | `"revit"` (`benutzer.js`, Server `BenutzerEinstellungen`: der Wert wird geprüft, alles andere ergibt 400 mit Vorschlag). Bedienung: Register **Ansicht › Fenster › Paletten**, Eintrag «Symbole im Revit-Stil (Probe)» mit Häkchen am Ende der Liste (`aktionen-paletten.js`; kein neues Register, keine neue Gruppe). Die Einstellung gilt für den Benutzer überall (alle Projekte und Rechner); zusätzlich merkt der Browser sie unter `hlks.symbolstil`, damit die Seite beim Neuladen nicht flackert (massgebend bleibt der Server).
- **Umschalten ohne Neuladen:** `symbolstil-umschalten.js › symbolstilWaehlen` setzt den Satz (`symbolstil.js`), tauscht alle `svg.sym[data-sym]` im Dokument (Menüband, Schnellzugriff, Projektbrowser, Eigenschaften, Kopf) gegen die Fassung des gewählten Satzes in gleicher Grösse und schreibt die Einstellung. `symbol()` und `symbolWz()` fragen zuerst `ersatzSymbol()` (`symbolstil.js`) und fallen auf den Strich zurück, wenn es zu einem Namen keine Revit-Fassung gibt (ein neues Symbol in `symbole.js` bleibt also bis zu seiner Zeichnung ein Strichsymbol). Bekannte Symbole tragen `data-sym="<Name>"`; unbekannte Namen ergeben wie bisher das gestrichelte Quadrat. Das Menüband und seine Stufen bleiben unverändert: die Symbole haben dieselben Masse (32 × 32 gross, 16 × 16 klein; bis 20 px die 16-er, darüber die 32-er Zeichnung).
- **Dateien:** `js/oberflaeche/symbole-revit.js` (Zeichnungen, Tabelle `ZUORDNUNG` im Kopf: Schlüssel · Befehl in KICAD · Revit-Befehl bzw. «eigenes» · Bildidee; `revitSymbol(name, px)`), `js/oberflaeche/symbolstil.js` (Wahl, Zwischenspeicher), `js/oberflaeche/symbolstil-umschalten.js` (Tauschen, Speichern, Start), `css/symbole-revit.css` (Farben). Die Schlüssel sind dieselben wie in `symbole.js` und `werkzeuge/symbole.js` (155 Symbole, je 32 und 16 eigens gezeichnet, nicht verkleinert; Stand 20.10.2026 mit «claude», «einlageplan», «grundleitungsplan» und «listeCsv»).
- **Farben:** Die Zeichnungen tragen keine festen Farben, nur Klassen (`f1 f2 f3` Grautöne der Bauteile, `fp` Papier, `fb fl` Blau als Hauptakzent, `fy fo` Gelb/Orange zur Hervorhebung, `fr` Rot für Löschen, Medienfarben `fvl frl fsa fab fzu fal fau`, entsprechend `s…` für Linien); die Werte stehen als CSS-Variablen `--sy-*` in `css/symbole-revit.css` je für hell (`:root`) und dunkel (`:root[data-design="dunkel"]`). Medienfarben wie in `kataloge/medien.json`: Heizung Vorlauf rot, Rücklauf blau, Sanitär grün, Abwasser braun, Luft nach Luftart (Zuluft rot, Abluft gelb, Aussenluft grün); im dunklen Design etwas heller. Dokumentierte Ausnahmen von «keine festen Farben»: keine. Neue Farbe = neue Variable für beide Designs, nie ein fester Wert im Symbol.
- **Neues Symbol:** erst in `symbole.js` (Strich, 32 × 32), dann in `symbole-revit.js` eine Fassung in 32 und 16 (`s('name', '<32>', '<16>')`, nur `path rect circle ellipse` mit Klassen) und eine Zeile in der Tabelle; `symbole-revit.test.mjs` prüft Vollständigkeit, gültiges SVG, Zeichenfeld, Klassen und Variablen.
- **Kontaktbogen** (zur Freigabe): `node werkzeuge/symbole_kontaktbogen.mjs` erzeugt unter `%USERPROFILE%\HLKS_CAD_kontaktbogen\` die Seite `kontaktbogen.html` und PNG-Bilder: Raster aller Symbole (Name, alt und neu in 32 und 16, hell und dunkel, dazu die 16-er Zeichnung vierfach und die 32-er zweifach vergrössert) und das Menüband der Register Ändern, Architektur, Gebäudetechnik, Ansicht und Koordination bei 1920 px, alt und neu (Edge headless über CDP, eigener Server auf Port 5196 mit Beispielprojekt unter `%TEMP%`; `--ohne-menue` lässt die Menübänder weg).
- **Prüfen:** Node `symbole-revit.test.mjs` (Vollständigkeit gegen `symbole.js` und `werkzeuge/symbole.js`, 32 und 16, gültiges SVG und Zeichenfeld, keine festen Farben, Klassen und Variablen für hell und dunkel, Tabelle, Umschalten von `symbol()`/`symbolWz()`), Web `BenutzerEinstellungenTests` (Wert von `oberflaeche.symbole`), Browser `symbole.browser.mjs` (Umschalten ohne Neuladen, Einstellung bleibt nach dem Neuladen; Ports 5198, CDP 9542, `HLKS_S_PORT`, `HLKS_S_CDP`) und `menueband.browser.mjs` (mit `HLKS_SYMBOLE=revit` bzw. strich bei 1920, 1366, 1024 und 800 px kein Rollbalken).

## Nachbearbeitung N3: fehlende Bedienorte (`nachbearbeitung.md`)

Vier Funktionen, die der Kern schon konnte und die in der Oberfläche nicht erreichbar waren (Funktionsverzeichnis `berichte/anleitung/inventar-notizen.md`, Abschnitt 3 und 4). Kein neues Register, kein neuer Kernbefehl, kein neuer Server-Endpunkt: alles läuft über `befehl()` und `abfrage()` (`einlageplaene_anlegen`, `verlegung`, `element_aendern`, `element_anlegen`, `kollisionen`, `aussparungsliste`).

| Funktion | Ort | Kürzel | Dateien |
| --- | --- | --- | --- |
| **Einlageplan** anlegen | Ansicht › Erstellen › Knopf «Einlageplan» (nur Symbol, Name im Tooltip; neben «Grundriss») | – | `einlageplaene-dialog.js`, `einlageplaene-text.js`, Aktion `ansicht.einlageplan` |
| **Grundleitungsplan** anlegen | Ansicht › Erstellen › Knopf «Grundleitungsplan» | – | derselbe Dialog, Aktion `ansicht.grundleitungsplan` |
| **Projektinformationen** bearbeiten | Verwalten › Projekt › «Projektinformationen»; Projektbrowser › Gebäude › «Projektinformationen» (Doppelklick, Kontextmenü «Bearbeiten …»; erscheint, sobald das Element da ist) | – | `projektinfo-dialog.js`, `projektinfo-text.js`, Aktion `verwalten.projektinfo` |
| **Kollisionsliste, Aussparungsliste** | Ansicht › Erstellen › «Liste» (Dialog «Liste anlegen», zwei neue Einträge) | – | `aktionen-ansicht.js` (`LISTEN`) |
| **CSV** jeder Listenansicht | Knopf «CSV …» rechts in der Kopfzeile der Liste; Projektbrowser › Kontextmenü einer Liste › «CSV …» (Aktion `liste.csv`) | – | `ansichten/liste-csv.js`, `ansichten/liste-csv-dialog.js`, `ansichten/liste.js` |
| **Tastaturkürzel (KS)** nach Register | Verwalten › Hilfe › «Tastaturkürzel» | KS | `kuerzel-liste.js` |

- **Einlageplan und Grundleitungsplan:** Der Dialog fragt `verlegung { art }` und zeigt die erkannten Pläne (Planschlüssel «Einlagen D.ü.2.UG» = Name, Bauteil Decke/Bodenplatte/Wand, Geschoss, Leitungen, Formteile, Länge). Wählbar sind Geschoss, Massstab und Blattformat; ohne Wahl gelten die Vorgaben des Kerns (nichts wird mitgeschickt). Darunter steht als Vorschau der Trockenlauf des Befehls (was an Ansichten und Planblättern entsteht, Hinweise des Kerns, «nichts Neues», wenn alles schon da ist). «Anlegen» sendet `einlageplaene_anlegen { art, geschoss?, massstab?, format? }` (ein Befehl, ein Rückgängig) und öffnet die erste neue Ansicht. **Name, Kennzeichen, Plannummer und Gewerk bestimmt der Kern** (nach dem Planschlüssel); der Befehl kennt weder einen Namen noch eine Auswahl des Bauteils, deshalb bietet der Dialog beides nicht an (er legt immer alle erkannten Pläne des Geschosses an; ein Plan lässt sich danach in der Ansicht umbenennen).
- **Knöpfe nur mit Symbol:** Die zwei Knöpfe haben `nurSymbol: true` (Aktionseigenschaft, `menueband.js › knopfHtml`): auch in den Stufen G und M steht nur das 16-px-Symbol, der Name im Tooltip (mit «F1: Hilfe zu diesem Befehl»). Das spart Breite; mit Text liesse die Gruppe «Erstellen» bei 1920 px zwei Gruppen mehr verkleinern, als `menueband.browser.mjs` zulässt. Symbole `einlageplan`, `grundleitungsplan`, `listeCsv` in `symbole.js`.
- **Projektinformationen:** Die Felder kommen aus der Katalogdefinition der Klasse (`abfrage('katalog', { klasse: 'Projektinformation' })`): **Projekt** (Objekt Zeile 1 und 2, Projektnummer, Phase), **Beschriftung und Bemassung** (Rohrbezeichnung NW/DN, Masseinheit je Gewerk m/cm/mm; die Auswahllisten stehen im Katalog – `Ansicht.bemassungEinheit` –, die Vorgabe je Gewerk im Beschreibungstext des Katalogs, im Dialog steht «Vorgabe»), **Lage** (Nordwinkel in °). Felder, die eine andere Funktion schreibt (letzter Kollisionslauf, IFC-Bezug, Wärmeübergang, HKAP-Verknüpfung, Familienauswahl), erscheinen nur lesend, mit dem Ort, wo man sie ändert. Neue Katalogfelder erscheinen von selbst unter «Weitere» (Text, Zahl, Auswahl bearbeitbar). «Übernehmen» sendet nur die geänderten Felder mit `element_aendern` (leer = Eintrag entfernen); gibt es noch kein Element der Klasse, legt `element_anlegen` eines an (Kennzeichen vergibt der Kern, «PI-1»). Ohne Änderung wird nichts gesendet; eine ungültige Zahl bleibt im Dialog mit Fehlertext. Rückgängig und Herkunft (`warum`) sind die des Kerns. Der bisherige Überblick über das Projekt steht ausklappbar im Dialog.
- **Listen und CSV:** «Liste anlegen» bietet Kollisionsliste (`liste = kollisionen`, Kennzeichen `AN-LI-KOLLISIONEN`) und Aussparungsliste (`aussparungsliste`, `AN-LI-AUSSPARUNGEN`); die Listenansicht zeichnet sie seit Modul 3b (B4a). **CSV:** `liste-csv.js` macht aus der aufbereiteten Liste (`listenformat.aufbereiten`) eine Datei mit **UTF-8 mit BOM, Semikolon, Dezimalkomma, CRLF, Kopfzeile = Spaltentitel**: dieselben Spalten und Zellentexte wie in der Liste, in der Reihenfolge der gerade gewählten Sortierung; Listen mit mehreren Tabellen (Mengen, Materialliste) stehen untereinander mit Titelzeile und Leerzeile; Summenzeilen (Raumgrenzen, Hüllbauteile) folgen unter ihrer Tabelle; eine Liste ohne Zeilen ergibt eine leere Datei. Felder mit Semikolon, Anführungszeichen oder Zeilenumbruch stehen in Anführungszeichen. Der Dialog «Liste als CSV» fragt nur den Dateinamen (Vorschlag «Titel_Projekt_Datum.csv», «.csv» wird ergänzt); die Datei lädt der **Browser** in seinen Download-Ordner (Blob, kein Server-Endpunkt).
- **Tastaturkürzel nach Register:** `kuerzel-liste.js › REGISTER_NAMEN` enthält alle Register des Menübands (Datei, Architektur, Hülle, Gebäudetechnik, Einfügen, Beschriften, Ändern, Koordination, Ansicht, Verwalten) sowie Auswahl und Familie; unter «Allgemein» steht nur noch, was zu keinem Register gehört (Esc, Strg+D).

**Prüfen:** Node `nachbearbeitung-n3.test.mjs` (CSV, Kürzel, Felder der Projektinformation aus dem echten Katalog, Planzeilen und Parameter, Knoten im Projektbrowser, Verdrahtung); Browser `node nachbearbeitung-n3.browser.mjs` (Port 5183, CDP 9503; alle vier Punkte mit echten Klicks, CSV-Datei im Download-Ordner nachgelesen; Bilder hell/dunkel `n3_*.png` in `%TEMP%\HLKS_CAD`); `menueband.browser.mjs` bleibt grün (bei 1920 px keine Gruppe zusammengeklappt).

## KI-Palette: Schnittstelle (3c, Teil K)

Server und Kern der Palette «Claude» (Spezifikation `entwurf/modul-3c.md`, Teil K). Die Palette (Teil O) baut **nur** auf diesen Endpunkten und
Ereignissen auf; die Logik (Kontext auflösen, Auftragstext, Werkzeuge, Zurücknehmen, Normensuche) steckt im Server (`GespraechDienst.cs`,
`GespraechEndpunkte.cs`, `KiLaeufe.cs`) bzw. im Kern (`Projekt.PaketZuruecknehmen`, `Normen/Normenverzeichnis.cs`). Texte deutsch, Zeiten ISO 8601.
Fehler immer `{ fehler, vorschlag }` mit 400 (Eingabe), 404 (unbekannt), 409 (kein Projekt offen · Lauf belegt · schon beendet · spätere
Änderungen), 503 (Ablage nicht erreichbar).

**Endpunkte**
- `GET /api/ki/einstellungen` → `{ modelle: [{ id: "sonnet", name: "Sonnet" }, { id: "opus", … }, { id: "haiku", … }], vorgabeModell: "sonnet",
  internetVorgabe: false, internetHinweis: "Internet: ohne Projektzugriff", internetWerkzeuge: ["WebSearch", …], verfuegbar, grund?, zeitlimitMinuten: 10,
  grenzen: { textZeichen: 8000, auswahl: 200, bildBytes: 4194304, titelZeichen: 200 } }`. `internetHinweis` gehört an den Schalter «Internet»
  (Entscheid Hauptlauf 20.10.2026, siehe unten). `verfuegbar: false` mit `grund` (z. B. «Claude Code wurde nicht gefunden …»), wenn claude.exe oder der MCP-Server fehlt; ein Abo- bzw.
  Anmeldeproblem zeigt sich erst bei der Frage (`fehlerart: "anmeldung"`).
- `GET /api/ki/gespraeche` → `{ gespraeche: [{ id, titel, erstellt, geaendert, fragen, laeuft }] }`, neueste (`geaendert`) zuerst; 409 ohne Projekt.
- `POST /api/ki/gespraeche` `{ titel? }` → **201** mit dem Gespräch (Form wie GET unten). Ohne Titel heisst es «Neues Gespräch» und bekommt mit
  der ersten Frage deren Anfang (höchstens 60 Zeichen) als Titel.
- `GET /api/ki/gespraeche/{id}` → `{ id, titel, erstellt, geaendert, modell, laeuft, frage, nachrichten: [Nachricht] }`. Während einer Frage ist
  `laeuft: true`, `frage` die Id der laufenden Frage und die letzte Nachricht die Antwort im Entstehen (`status: "laeuft"`, Text bis jetzt) – so
  holt ein neu geladenes Fenster den Stand nach und folgt dann den Ereignissen.
- **Nachricht** Mensch: `{ rolle: "mensch", frage, text, zeit, akteur, kontext: { ansicht?: { kennzeichen, name, art, massstab } | { kennzeichen,
  gefunden: false }, geschoss?: { kennzeichen, name }, auswahl?: [kz…], bild: bool }, modell, internet, internetHinweis?, kontextVerworfen? }`.
  Mit Internet ist `kontext` immer leer (`{}`) und `kontextVerworfen: true`: der Server hat Ansicht, Geschoss, Auswahl und Bild verworfen.
  Claude: `{ rolle: "claude", frage, status: "laeuft" | "fertig" | "fehler" | "abgebrochen", text, zeit, dauerSekunden, modell, internet, internetHinweis?,
  werkzeuge: [{ id, name, kurz, ok: null | true | false, meldung? }], aenderungen: [Aenderung], fehler?, fehlerart?, sitzungNeu?, abrufe?, runden?, kostenUsd? }`.
  `name` ohne Präfix (z. B. `element_aendern`, `WebSearch`), `kurz` = Eingabe einzeilig (≤ 140 Zeichen), `ok: null` = läuft noch.
  `fehlerart`: `anmeldung` (Claude nicht angemeldet → Hinweis «unter Heute mit Claude anmelden»), `start`, `mcp`, `claude`, `zeitlimit`,
  `abgebrochen`, `intern`. `sitzungNeu: true` = die frühere Claude-Sitzung war nicht verfügbar (anderer Rechner, gelöscht); Claude bekam die
  letzten Nachrichten als Verlauf (Hinweis in der Palette sinnvoll). `abrufe` (mit Internet): `[{ zeit, art: "suche" | "abruf", text | url, erfolg }]`.
  `internetHinweis: "Internet: ohne Projektzugriff"` steht in beiden Nachrichten einer Frage mit Internet (zum Anzeigen bei der Frage).
- **Aenderung**: `{ paket, befehl, beschreibung, zeit, elemente: [kz…], ids: [id…], zurueckgenommen, zurueckgenommenPaket?, zurueckgenommenZeit?,
  zurueckgenommenVon? }` – je Paket des Akteurs KI, in der Reihenfolge der Pakete. `beschreibung` z. B. «element_aendern: WT-E.02 geändert»,
  «leitung_ziehen: R-12, R-13 angelegt; 2 Verbindungen gesetzt». `elemente` (Kennzeichen) und `ids` stehen in derselben Reihenfolge; zum Wählen
  im Modell die Id nehmen (gelöschte Elemente gibt es nicht mehr). `zurueckgenommen` gilt auch, wenn das Paket anderswo zurückgenommen wurde.
- `POST /api/ki/gespraeche/{id}/frage` `{ text, kontext: { ansicht?, geschoss?, auswahl?: [kz…], bild?: "data:image/png;base64,…" }, modell?, internet? }`
  → **202** `{ gespraech, frage, laeuft: true, nachricht: <Mensch> }`. Kennzeichen oder Id gehen; ohne `geschoss` gilt das Geschoss der Ansicht.
  Grenzen (sonst 400): Text 1–8000 Zeichen, `auswahl` ≤ 200 Texte, Bild PNG/JPEG ≤ 4 MB als data-URL, `modell` aus der Liste, `internet` bool
  (Vorgabe aus; mit `true` arbeitet Claude **ohne Projektzugriff und ohne Projektkontext**, siehe unten – Projektfragen ohne Internet stellen;
  die Palette kann die Kontext-Chips dann ausgrauen, der Server verwirft den Kontext ohnehin). 409 «Claude arbeitet gerade an einer Frage im Gespräch «…»» bzw. «… an dem KI-Auftrag … (Familie aus Unterlagen)», wenn im
  Projekt schon ein KI-Lauf läuft (Gespräch oder Auftrag; `vorschlag` nennt den Abbrechen-Endpunkt). Das Bild liegt nur im Arbeitsordner der KI
  und wird nach der Frage gelöscht; Claude sieht es über das Werkzeug `kontext_bild`.
- `POST /api/ki/gespraeche/{id}/abbrechen` → 200 mit dem Gespräch (nach dem Ende, höchstens 20 s Wartezeit); 409, wenn nichts läuft.
- `DELETE /api/ki/gespraeche/{id}` → `{ geloescht: true, id, ablage }`; die Dateien wandern nach `ki\geloescht\`. 409 während einer Frage.
- `POST /api/ki/aenderungen/{paket}/zuruecknehmen` → 200 `{ zurueckgenommen: true, paket, rueckgaengig: <neues Paket>, gespraech, ergebnis }`.
  Abgelehnt mit `{ fehler, vorschlag, ablehnung, spaeter?, gesperrt? }`: **409** `spaeter_geaendert` (ein späteres Paket hat dieselben Elemente
  oder Verbindungen berührt; `spaeter: [{ paket, akteur, akteurArt, zeit, befehl, elemente }]`; nichts geändert), `schon_zurueckgenommen`,
  `verwendet` (ein von der KI angelegtes Element wird inzwischen verwendet), `gesperrt` (reserviert), 409 auch während eines KI-Laufs im Projekt;
  **404** `unbekannt`; **400** `fremd` (kein Paket der KI); **503** `ablage`. Mehrere Änderungen lassen sich von hinten nach vorn zurücknehmen
  (ein zurückgenommenes späteres Paket hindert nicht mehr). Das Zurücknehmen ist selbst ein neues Paket (Akteur = der Mensch).
- `GET /api/normen/suche?q=…&reihe=…` → `{ suche, begriffe, quelle, stand, vollstaendig, ohneVolltext, treffer: [{ id, reihe, norm, titel, ausgabe,
  datei, seite, ziffer, auszug }], hinweis }` – dieselben Treffer wie `normen_suchen` (Auszug ≤ 600 Zeichen, kein Volltext; `seite: 0` = nur der
  Titel passt). `q` leer → 400. `reihe`: SIA, SWKI, VKF, Vorlage, suissetec, Sprinkler, Merkblatt. Im Testlauf ohne `HLKS_NORMEN` unter %TEMP%
  keine Treffer, `hinweis` nennt den Grund. `GET /api/normen` → `{ quelle, verfuegbar, gesperrt?, zustand, normen, mitVolltext, aufbau: { laeuft,
  fertig, gesamt } }`; `POST /api/normen/aufbauen` startet den Volltext-Aufbau (202). Sonst startet er von selbst bei der ersten Suche bzw. Frage
  (einmal; Zwischenspeicher `%LOCALAPPDATA%\HLKS_CAD\normen`, nie OneDrive – lizenzierte Texte).

**Ereignisse** über `/api/ereignisse` (bisher übergeht `ereignisse.js` die Art «ki»; Teil O ergänzt z. B. `case 'ki': melden('ki', d)`):
`{ art: "ki", gespraech, frage, typ, … }` mit
- `typ: "frage"`, `nachricht` (Mensch) – zu Beginn, auch für andere Fenster;
- `typ: "text"`, `teil` – Teilstück der Antwort (gebündelt, höchstens etwa alle 120 ms); die Teile ergeben aneinandergereiht genau `text` der
  fertigen Nachricht (Absätze zwischen Textblöcken als `"\n\n"` im Teil);
- `typ: "werkzeug"`, `werkzeug: { id, name, kurz, ok, meldung? }` – einmal beim Aufruf (`ok: null`), einmal mit dem Ergebnis (gleiche `id`);
- `typ: "aenderung"`, `aenderung` (wie oben) – sobald das Paket gespeichert ist, spätestens vor dem Ende;
- zuletzt genau eines von `typ: "fertig" | "fehler" | "abgebrochen"` mit `nachricht` (vollständige Claude-Nachricht); bei `fehler` zusätzlich
  `fehler` und `fehlerart`.
- Ohne Frage: `{ art: "ki", typ: "gespraeche" }` (Liste geändert: angelegt, gelöscht, Titel, Beginn und Ende einer Frage) und
  `{ art: "ki", gespraech, typ: "zurueckgenommen", paket, rueckgaengig }`.
Die Modelländerungen der KI kommen zusätzlich wie immer als `art: "aenderung"` mit `akteurArt: "ki"` (Abgleich).

**Hinter den Kulissen (für Hinweise in der Palette):** Claude Code läuft über Christians Abo (kein API-Schlüssel) mit nur dem MCP-Server «hlks»
(Akteur KI, Art ki). **Ohne Internet** (Vorgabe) sind alle Werkzeuge erlaubt ausser der Verbotsliste `GespraechDienst.Verbote` (Freigaben,
Konfliktentscheide, Werte bestätigen, Bürobibliothek schreiben bzw. übernehmen, alles `hkap_*`, Aussparungs- und Kollisionsstatus, BCF, Plan- und
IFC-Ausgaben, `unterlage_aus_url`). **Mit Internet: ohne Projektzugriff** (Entscheid Hauptlauf 20.10.2026, dieselbe enge Linie wie die Aufträge):
erlaubt sind nur WebSearch, `unterlage_aus_url`, `normen_suchen`, `norm_lesen`, `kontext_bild` und die Familien- und Unterlagenwerkzeuge der
Aufträge (`GespraechDienst.InternetWerkzeuge`); alle anderen Werkzeuge – Projektabfragen und ändernde Projektbefehle – stehen in
`--disallowedTools`. Claude weiss das (Systemteil) und verweist Projektfragen auf eine Frage ohne Internet. Grund: eingeschleuste Webinhalte sollen
keine Projektdaten über Suchanfragen oder Abrufe nach aussen tragen können. Ebenso geht mit Internet **kein Projektkontext** an Claude (Entscheid
Hauptlauf 20.10.2026): kein Projektname, kein Ordner, keine Ansicht, kein Geschoss, keine Auswahl, kein Bild – der Server verwirft den Kontext der
Anfrage selbst (`kontextVerworfen: true`), das Bild wird nicht abgelegt und `kontext_bild` antwortet «Kein Bild – mit Internet ohne
Projektkontext»; der Systemteil nennt nur «Gespräch in KICAD, ohne Projektzugriff». Eine Frage mit Internet läuft zudem in einer **eigenen
Claude-Sitzung ohne Verlauf** (die Sitzung des Gesprächs enthält Projektdaten früherer Fragen); die nächste Frage ohne Internet setzt die Sitzung
des Gesprächs fort, und der Verlauf für eine neue Sitzung lässt Fragen mit Internet aus. In der Palette stehen beide Arten Fragen im selben
Gespräch; Claude erinnert sich bei einer Frage mit Internet aber nicht an das bisherige Gespräch. Neue Werkzeuge: `normen_suchen`, `norm_lesen`,
`kontext_bild`. Zeitlimit je Frage 10 min (`HLKS_KI_FRAGE_ZEITLIMIT_MIN`). Ablage `<Projekt>\ki\gespraeche\<id>.json` und `<id>.log`;
Arbeitsordner von Claude stabil je Projekt unter `%LOCALAPPDATA%\HLKS_CAD\ki\<Kennung>` (Testlauf `%TEMP%\HLKS_CAD\ki_test`), damit `--resume`
die Sitzung nach einem Neustart findet.

**Testen (Teil O):** Server mit `HLKS_TEST=1`, `HLKS_CLAUDE_EXE` = `Hlks.ClaudeAttrappe.exe` (Gesprächsmodus mit `--session-id`/`--resume`; Modus in
der Frage als `[attrappe:antwort | aendern=KZ | zweimal=KZ | ueberblick | bild | normen | werkzeugfehler | internet | langsam | haengt | fehler | anmeldung]`,
siehe Kopf von `tests/Hlks.ClaudeAttrappe/Program.cs`; die Attrappe prüft die Werkzeuge wie Claude Code gegen `--allowedTools`/`--disallowedTools`
und antwortet sonst «Werkzeug verweigert: …»), `HLKS_MCP_DLL`, `HLKS_NORMEN` auf eine Kopie von `tests/daten/normen` unter %TEMP% (Test-PDF
mit erfundenem Text: «Mindestgefälle Prüfleitung» → SIA 9999:2026, Seite 3, Ziffer 4.2.1). Web-Tests: `GespraechTests`, `GespraechLaufTests`,
`GespraechZuruecknehmenTests` (mit `GespraechVerbotslisteTests`).

## Sanitär-Belastungswerte (Modul 4 Teil A-K)

Kern fertig (`entwurf/modul-4.md`, Teil A-K); die Oberfläche baut Teil A-O. Einzige fachliche Quelle ist `kataloge/sanitaer_belastungswerte.json`
(Christians Excel «LU_DU Zusammenstellung_.xlsx», Walkringen 14.03.2024): die Oberfläche kennt **keine** LU-/DU-Werte, Formeln oder Vorgaben, sie
zeigt nur, was der Kern liefert. Keine Dimensionierung (Spitzendurchfluss, DN, Q_WW je Teilstrecke) – das ist Teil B.

**Eigenschaften (Katalog):**
- Apparat (alle Unterklassen): `sanitaerArt` (auswahl). `abfrage('katalog', { klasse })` liefert dazu `auswahl` (Schlüssel in Katalogreihenfolge) und
  **`auswahlNamen`** (Schlüssel → Name, z. B. `wc_75` → «Klosettanlage bis 7.5 Liter Spülwassermenge»): die Auswahl zeigt den Namen, gesendet wird der
  Schlüssel (`element_aendern { eigenschaften: { sanitaerArt } }`, `null` = zurück zur Vorgabe der Klasse). Ohne gesetzte Eigenschaft gilt die Vorgabe der
  Klasse – die Palette zeigt das Feld dann leer; die wirksame Art steht in `element_lesen.sanitaer` (unten).
- Anlage: `abflusskennzahl` (zahl, Wert mit Quelle), `spezialinstallationen` (liste von `{ bezeichnung, versorgung, entsorgung }` in l/s; ungültige
  Einträge lehnt der Kern ab). **Befund für A-O:** die Palette zeigt `liste` als Kommaliste – Objekte erscheinen dort als «[object Object]»; A-O braucht
  eine kleine Tabelle (Bezeichnung, Versorgung l/s, Entsorgung l/s) wie das Excel Zeilen 46–51.
- Projektinformation: `abflusskennzahl` (Vorgabe für Anlagen ohne eigenen Wert; erscheint im Dialog Projektinformationen unter «Weitere»).

**element_lesen** (Palette, Gruppe «Abgeleitet»):
- Apparat: `abgeleitet.luKw`, `abgeleitet.luWw`, `abgeleitet.du` – nur die Werte, die die Art hat (WC: kein `luWw`, nicht 0). Dazu Block
  `sanitaer: { art, name, woher (eigenschaft | familie | vorgabe | keine | unbekannt), herkunft, luKw, luWw, du (null = Anschluss fehlt), zeile, quelle, anlagen[] }`.
- Leitung des Trinkwassernetzes (KW, WW): `abgeleitet.summeLuKw`, `summeLuWw`, `groessterLu`; des Schmutzwassernetzes (SW): `abgeleitet.summeDu`. Dazu
  `sanitaer: { netz (trinkwasser | abwasser), medium, richtung, summe…, apparate, ohneWert[]?, masche?, quelle }`. Zirkulation, Regenwasser, Heizung: kein Block.
- «?» (`warum`) kennt `luKw`, `luWw`, `du`, `sanitaerArt` (Vorgabe/Familie mit `begruendung`) am Apparat und `summeLuKw`, `summeLuWw`, `groessterLu`,
  `summeDu` an der Leitung: `{ wert, einheit (LU | DU), rechenweg, quelle (Katalog, Dokument, Excel-Zeile), beitraege[]? }`. Die Schlüssel haben kein «_»,
  die Palette zeigt sie ohne Einheit (Einheit steht in `warum`).

**Abfragen** (`abfrage(name, par)`, gleich in MCP):
- `sanitaer_belastung { anlage? | strang? | leitung? }` – ohne Parameter `{ anlagen[], systemeOhneAnlage[]?, maschen[]?, ohneWeg[]?, quelle, hinweis }`; je Anlage
  `{ anlage, name, trinkwasser { summeLuKw, summeLuWw, groessterLu, apparate }, abwasser { summeDu, apparate }, systeme[], ohneWeg[]? }` – `ohneWeg[] { apparat,
  anschluss, medium, fehlt (stromauf | stromab), endetBei, grund }` = verbundene Apparateanschlüsse, deren Weg im Netz nicht weiterkommt (Befund P26); je System
  `{ system, name, systemtyp, medium, netz, richtung, summen…, straenge[], leitungen[] }`; je Strang `{ strang, name, art, system, medium, leitungen (Anzahl), summen… }`;
  je Leitung `{ leitung, strang, medium, summeLuKw, summeLuWw, groessterLu | summeDu, apparate, ohneWert[]?, masche? }`. Mit `strang`: der Strang mit
  `leitungen[]`. Mit `leitung`: `{ leitung, klasse, medium, netz, richtung, system, strang, anlage, summen…, masche, beitraege[] { apparat, klasse, anschluss,
  medium, spalte, wert, art, artName, artWoher, zeile, quelle }, rechenweg, quelle, hinweis }`; ausgenommene Leitung: `{ leitung, medium, ausgenommen }`.
  → Strangschema-Beschriftung «ΣLU 12 / 8» bzw. «ΣDU 4.6» aus den Leitungen einer Abfrage je Anlage.
- `sanitaer_zusammenstellung { anlage? }` (leer = die einzige Anlage mit Apparaten) – `{ anlage, name, quelle, geschosse[] { kennzeichen (null = ohne
  Geschoss), name, kote }, zeilen[] { art, name, zeile (Excel), luKw, luWw, du, jeGeschoss[], anzahl, summeLuKw, summeLuWw, summeDu (null = Spalte leer),
  apparate[], quelle }, apparate, summeLuKw, summeLuWw, summeDu, totalLu, volumenstrom, spitzeKurve3, spitzeKurve5, groessterLu, massgebendeKurve (3 | 5),
  spitzendurchfluss, spezialinstallationen[], spezialVersorgung, spezialEntsorgung, spitzeGesamtKurve3, spitzeGesamtKurve5, spitzeGesamt, abflusskennzahl,
  abflusskennzahlHerkunft, schmutzwasser, gesamtabfluss (beide null ohne K), einheiten, formeln { totalLu, volumenstrom, spitzeKurve3, spitzeKurve5,
  massgebendeKurve, spitzeGesamt, schmutzwasser, gesamtabfluss } (Text mit Excel-Zelle; massgebendeKurve = Regel, Herkunft und Bestätigung aus dem Katalog),
  kurvenwahl { regel, quelle, bestaetigt, kurven[] { schluessel, grosserLu, bereichLs [von, bis], bereichZelle, zelle } }, ohneArt[], hinweise[], hinweis }`.
  Zahlen ungerundet – runden nur in der Anzeige. Die nicht massgebende Kurve klein daneben (Spezifikation A-O). `hinweise[]` (Prüfbericht 4A): u. a.
  «Kurvenwahl nicht im Excel, aus HKAP, nicht bestätigt …» (solange `kurvenwahl.bestaetigt` false), Volumenstrom ausserhalb des Bereichs einer Kurve,
  «ΣLU WW enthält n Apparate ohne WW-Verbindung …», «ΣLU KW enthält n Apparate ohne Weg im KW-Netz … (Befund P26)», K fehlt (P25).
- `sanitaer_katalog {}` – `{ quelle { …, bereich, blatt }, arten[] { schluessel, name, luKw, luWw, du, zeile, nameExcel? }, vorgabeJeKlasse, ohneVorgabe[], formeln,
  kurvenwahl, hinweis }`. Jede Quelle eines Werts lautet «Katalog …: LU_DU Zusammenstellung_.xlsx, Blatt MFHA, Zeile n (Objekt, Stand)».

**Befunde** (Prüfsystem, wie P01–P20 in Befund-Browser und Koordination): P21 Apparat ohne Apparateart (warnung), P22 Apparateart passt nicht zu den
Anschlüssen (warnung), P23 Apparat in keiner Anlage (hinweis), P24 Netz mit Masche – Summe nicht eindeutig (warnung, am Treffpunkt der Wege, Text nennt die
Verzweigung; `masche` nur an den Leitungen in der Masche), P25 Abflusskennzahl fehlt (warnung, an der Anlage), P26 Apparat ohne Weg im Netz – Belastung fehlt
stromab/stromauf (warnung, am Apparat, mit Anschluss, Medium und Stelle, an der der Weg endet).

## KI-Palette (3c, Teil O)

Palette «Claude» in der Oberfläche (Spezifikation `entwurf/modul-3c.md`, Teil O; baut nur auf `/api/ki/*`, `/api/normen/*` und den Ereignissen `{ art: "ki" }` des vorigen Abschnitts auf). Keine Fachlogik in der Oberfläche: gefragt, abgebrochen und zurückgenommen wird über die Endpunkte; **geändert wird das Modell nur von Claude über die Befehle des Kerns**, die Palette zeigt die Änderungen und nimmt sie zurück.

| Was | Wo | Dateien |
| --- | --- | --- |
| Palette (Verwaltung W1, Id `claude`) | rechts angedockt (Vorgabe), nur auf Verlangen sichtbar; Häkchen **Ansicht › Fenster › Paletten › Claude**; Kürzel **KI** blendet ein und aus (frei: bei keiner anderen Aktion belegt); **Knopf «Claude»** in der Kopfleiste rechts (`#claude-knopf` in `#kopf-rechts`, links neben dem «?» der Hilfe, Symbol «claude», gedrückt solange die Palette sichtbar ist; im Zweitfenster keiner) – keine Menübandschaltfläche, das Register Ansicht ist voll, kein neues Register, keine neue Gruppe | `js/oberflaeche/ki-palette.js`, `aktionen-ki.js`, `css/ki-palette.css` |
| Reine Teile (in Node getestet) | Formatierung der Antworten, Links, Kontext-Chips, Texte, Merken, Bildgrössen | `ki-text.js` |
| Zustand des Verlaufs und Ereignisse | Streaming zusammensetzen, doppelte und verspätete Ereignisse | `ki-zustand.js` |
| Darstellung der Antworten | Blöcke → DOM, nie `innerHTML` mit fremdem Text | `ki-darstellung.js` |
| Bild der Ansicht | SVG der Plan-Ansichten und Schemas, WebGL-Fläche der 3D-Ansicht → PNG ≤ 4 MB | `ki-bild.js` |
| Symbol «claude» | Sprechblase mit vierzackigem Funken: Strich (`symbole.js`) und Revit-Fassung 32/16 (`symbole-revit.js`, eigene Zeichnung) | `symbole.js`, `symbole-revit.js` |

**Aufbau (Reiter «Gespräch» und «Normen»).**
- **Kopf:** Gesprächswahl (Liste «Titel · n Fragen · Zeit», erster Eintrag «＋ Neues Gespräch»; **Neu** und ✕ **Entfernen** mit Rückfrage), Modellwahl (Sonnet als Vorgabe, Opus, Haiku), Schalter **Internet** (aus als Vorgabe) mit dem Hinweis «ohne Projektzugriff» daneben. «Neu» zeigt eine leere Seite; das Gespräch entsteht auf dem Server erst mit der ersten Frage.
- **Verlauf:** Mensch rechts (blau), Claude links; unter der Frage kleine Chips mit dem mitgeschickten Kontext. Antworttext formatiert (Absätze, Überschriften, Listen, Fett/Kursiv, `Code`, Codeblöcke, Zitate, einfache Tabellen, Links `[Text](https://…)` im neuen Fenster mit `rel="noopener noreferrer"`). **Werkzeugaufrufe** kompakt als «n Werkzeugaufrufe» (aufklappbar: ✓/✗, Name, Eingabe, Fehlermeldung; mit Internet auch die Abrufe). **Änderungen** als Liste je Paket: Beschreibung, die betroffenen Elemente als Knöpfe (**Klick wählt das Element im Modell**, gelöschte sind ausgegraut) und **«Zurücknehmen»**; danach «zurückgenommen» und die Beschreibung durchgestrichen. Wird abgelehnt (409), steht darunter die Ablehnung in Klartext («Danach wurde dasselbe noch einmal geändert … Es wurde nichts verändert») und die späteren Pakete (wer, was, woran, wann); die Palette zeigt weder Codes noch JSON. Während der Antwort gestreamt mit Schreibmarke, danach Dauer und Modell.
- **Eingabe:** mehrzeilig, **Strg+Enter** sendet (Enter = neue Zeile), «Senden» wird zu **«Abbrechen»**, solange Claude antwortet; Zähler ab 75 % der 8000 Zeichen. Meldungen unter dem Verlauf: Lauf belegt (409 «Claude arbeitet gerade …»), Fehler, zu lang; die Frage bleibt im Feld. Ein Fehler der Antwort steht in der Nachricht (Anmeldung: «unter Heute mit Claude anmelden», Zeitlimit, Start, MCP); `verfuegbar: false` zeigt ein rotes Band mit dem Grund und sperrt «Senden» (Normensuche geht weiter).
- **Kontext-Chips** (A2): **Ansicht**, **Geschoss** und **Auswahl** (n Elemente) gehen automatisch mit, je Frage per Klick abwählbar (durchgestrichen); **«Bild der Ansicht»** ist aus (Vorgabe) und wird auf Wunsch aus der aktiven Ansicht erzeugt. Nach dem Senden gelten wieder die Vorgaben. Gibt es keine Ansicht, kein Geschoss, keine Auswahl oder lässt sich die Ansicht nicht als Bild senden (Liste, Planblatt), ist der Chip gesperrt. Hinweis: Geht die Ansicht mit, ergänzt der Server ihr Geschoss selbst; um das Geschoss nicht zu schicken, muss auch die Ansicht abgewählt sein.
- **Internet (Entscheid Hauptlauf 20.10.2026):** mit Internet arbeitet Claude **ohne Projektzugriff und ohne Projektkontext**. Die Palette zeigt es dreifach: Schalter mit «ohne Projektzugriff» und Hinweisband; alle Kontext-Chips **ausgegraut** (gesperrt, durchgestrichen, Tooltip erklärt es) – es geht nichts mit (die Palette schickt dann einen leeren Kontext, auch kein Bild; der Server verwirft ihn ohnehin); Nachrichten mit Internet tragen die Marke **«Internet · ohne Projektzugriff»** (Frage und Antwort, orange Rahmen), die Frage zusätzlich «ohne Projektkontext». Frühere Fragen ohne Internet bleiben im selben Gespräch; Claude erinnert sich bei einer Frage mit Internet nicht daran (eigene Sitzung).
- **Normen:** Reiter «Normen» mit Suchfeld und Reihenfilter (SIA, SWKI, VKF, Vorlage, suissetec, Sprinkler, Merkblatt); Treffer mit Norm, Ausgabe, **Seite, Ziffer** und Auszug (`GET /api/normen/suche`); «In die Frage übernehmen» setzt «Zu SIA …, Seite …, Ziffer …: » ins Eingabefeld. Meldet «Keine Treffer.» bzw. den Hinweis des Servers (kein Normenverzeichnis, Volltext noch im Aufbau).

**Reservierungen (Entscheid Hauptlauf 20.10.2026).** Wer ein Element in der Oberfläche ändert, hält es reserviert (Kern: je Element, Akteur und Gerät); Claude (Akteur KI) bekäme sonst «Elemente werden gerade von jemand anderem bearbeitet». Darum gibt der Server (`GespraechDienst.Fragen`, `Projekt.FreigebenFuer`) beim Start einer Frage **ohne Internet** die Reservierungen frei, die der Mensch **auf diesem Gerät** für die Elemente der mitgeschickten Auswahl, deren Kinder und – bei Teilen eines Strangs – den Strang samt seinen Teilen hält. Reservierungen anderer Arbeitsplätze bleiben; Claude bekommt dort wie bisher einen lesbaren Fehler (✗ in der Werkzeugliste). Ohne Auswahl in der Frage (Chip abgewählt) und mit Internet wird nichts freigegeben. Die Nachricht trägt `auswahlFreigegeben: [Kennzeichen]`; die Palette zeigt an der Frage den Hinweis **«Auswahl für Claude freigegeben»**. Die Oberfläche reserviert beim nächsten eigenen Ändern von selbst neu.

**Was im Browser bleibt (je Rechner, Schlüssel `ki`):** nur die Wahl des Gesprächs, des Modells und des Internet-Schalters **je Projekt** (`{ "<Ablageort>": { gespraech, modell, internet } }`, höchstens 30 Projekte). Der Verlauf kommt immer vom Server; keine Nachrichten, Fragen oder Kontexte im Browserspeicher. Beim Neuladen zeigt die Palette (bleibt eingeblendet) das gemerkte Gespräch bzw. das neueste des Projekts; mitten in einer Antwort wird der Stand nachgeladen und dem Ereignisstrom weiter gefolgt (Server: `ab` im Text-Ereignis, siehe unten).

**Ereignisse (`ereignisse.js › case 'ki'` → `melden('ki', d)`, `ki-zustand.js`).** Reihenfolge frage → text / werkzeug / aenderung → fertig | fehler | abgebrochen; jede Wirkung ist wiederholbar (Werkzeuge nach `id`, Änderungen nach `paket` ersetzt, `frage` nur einmal, Teile nach dem Ende ignoriert; die fertige Nachricht ersetzt den gestreamten Text). Ereignisse vor `frage` (Palette mitten im Lauf geöffnet) legen die Antwort als Platzhalter an; Ereignisse anderer Gespräche werden übergangen, `{ typ: "gespraeche" }` holt die Liste, `zurueckgenommen` markiert das Paket (auch aus einem anderen Fenster). **Kleine Korrektur am Server (Teil K):** Text-Ereignisse tragen zusätzlich `ab` (Zeichen vor dem Teil) und die Antwort im Entstehen (`GET`, `status: "laeuft"`) steht ungekürzt, damit ein nachgeladener Stand und der Strom sich ohne Doppeltes oder Lücken verbinden (Test `GespraechLaufTests.Langsam_TextKommtInTeilen`). Ältere Server ohne `ab` funktionieren (Teile werden angehängt).

**Fenster.** Die Palette ist wie jede andere frei andockbar (links, rechts, unten, als Reiter, schwebend – Verlauf, Eingabe und Rollposition bleiben beim Umsetzen erhalten); beim ersten Einblenden bekommt der rechte Bereich 380 px (statt 280). Ist sie unten rechts angedockt oder schwebt sie dort, rücken die Einblendungen der Oberfläche (`#meldungen`, «Änderung von KI: …») links neben sie, damit sie «Senden» nicht verdecken (`--meldungen-rechts`). Ist sie sehr niedrig, rollt die Seite der Palette. **Im Zweitfenster** (W2) sind alle Paletten ausgeblendet (`css/anordnung.css`, die Bereiche sind verborgen); die Palette baut dort nichts und lädt nichts.

**Annahmen A1–A4 (Testbericht zur Bestätigung):** A1 rechts angedockt, frei andockbar; A2 Kontext automatisch als abwählbare Chips (Bild nur auf Wunsch); A3 Verlauf je Projekt (Server), «Neues Gespräch» und frühere Gespräche wählbar, Wahl wird gemerkt; A4 Modell wählbar (Vorgabe Sonnet), Internet aus als Vorgabe, Schalter je Frage (gilt bis zum Umschalten, je Projekt gemerkt).

**Prüfen:** Node `node --test js/oberflaeche/ki-palette.test.mjs` (Formatierung mit Einschleusungsversuchen `<script>`, `<img onerror>`, `javascript:`-Links, Kontext und Chips auch mit Internet, Ablehnungstexte, Merken, Zustand und Ereignisse, Verdrahtung); Browser `SHOTS=<Ordner> node js/oberflaeche/ki-palette.browser.mjs` (**ki-palette.browser.mjs**: Server mit der **Attrappe** `tests/Hlks.ClaudeAttrappe` statt claude.exe – `HLKS_CLAUDE_EXE`, `DOTNET_ROOT`, `HLKS_MCP_DLL` – und `HLKS_NORMEN` auf eine Kopie von `tests/daten/normen`; Ports 5181 und 5182 (`HLKS_KI_PORT`, `HLKS_KI_PORT2`), CDP 9506 (`HLKS_CDP_PORT`); echte Maus und Tasten per CDP; Bilder `ki_*_hell.png` / `ki_*_dunkel.png` in `%TEMP%\HLKS_CAD`).

## Sanitär-Belastungswerte: Oberfläche (Modul 4 Teil A-O)

Oberfläche zu `entwurf/modul-4.md` Teil A-O, gebaut auf der Schnittstelle des Abschnitts «Sanitär-Belastungswerte (Modul 4 Teil A-K)». Wie dort: die Oberfläche kennt keine LU-/DU-Werte, Formeln,
Vorgaben je Klasse oder Normwerte – sie zeigt, was der Kern liefert (`element_lesen`, `warum`, `sanitaer_katalog`, `sanitaer_zusammenstellung`, Plangrafik), rundet nur für die Anzeige und sendet
Eingaben als `element_aendern`. **Keine Dimensionierung** (Teil B), **keine Auswahlliste für K**. Der Node-Test `sanitaer-belastung.test.mjs` prüft, dass in `sanitaer-*.js` weder Rechenkonstanten noch
Wurzeln stehen. Quelle jedes Werts: «LU_DU Zusammenstellung_.xlsx» mit Excel-Zeile (im «?», in den Tooltips der Liste und in der CSV-Datei).

**Dateien:** `oberflaeche/sanitaer-text.js` (Rundung, Auswahl, Spezialtabelle als reine Funktionen), `oberflaeche/sanitaer-palette.js` (HTML-Bausteine der Palette), `oberflaeche/sanitaer-anlage.js` (aktive Anlage, Liste
der Anlage), `oberflaeche/aktionen-sanitaer.js` (Knopf), `ansichten/sanitaer-liste.js` (Listenaufbau), `css/sanitaer.css`; geändert: `eigenschaften.js`, `dialoge.js` (Beiträge im «?»), `aktionen-ansicht.js` (Dialog
«Liste anlegen»), `ansichten/liste.js`, `listenformat.js`, `liste-csv.js`, `symbole.js`, `symbole-revit.js`, `kataloge/klassen.json` (Ansicht), Kern `Schema/SchemaGrafik.cs` und `Schema/SchemaBelastung.cs` (nur die Beschriftung).

**Rundung (nur Anzeige; der Kern rechnet ungerundet, das «?» und die Tooltips zeigen den genauen Wert)** – Regeln in `sanitaer-text.js`, im Code begründet:
- LU: ganze Zahl, sonst eine Stelle (Excel «Standard»; der Katalog hat nur ganze LU). DU: immer eine Stelle («2,0», «38,8»; Katalogwerte haben höchstens eine Stelle, die feste Stelle hält die Spalten bündig).
- l/s: zwei Nachkommastellen, unter 1 l/s drei («1,14», «0,762»; Referenz Walkringen 1.1412… → 1,14, Q_WW 3.1144… → 3,11, Volumenstrom 13,20). Von Hand eingetragene Spezialinstallationen: wie eingegeben, bis drei Stellen
  ohne Nachnullen («0,5»).
- Dezimaltrenner: Palette Punkt (wie ihre übrigen Felder, «0.5 DU»), Liste und CSV Komma (wie die anderen Listen), Beschriftung im Strangschema Punkt wie die übrigen Texte der Plangrafik («ΣDU 4.6»).

**Eigenschaften-Palette** (`eigenschaften.js`, Bausteine `sanitaer-palette.js`):
- Apparat: Zeile «Apparateart» (Eigenschaft `sanitaerArt`) als Auswahl mit Namen (`auswahlNamen`); erster Eintrag «Vorgabe der Klasse (Name)» = nicht gesetzt (`null`; die Vorgabe liest die Palette aus `sanitaer_katalog.vorgabeJeKlasse`,
  Bodenablauf: «Vorgabe der Klasse: keine – Art wählen»). Darunter ein Satz zur wirksamen Art («gilt: … (Vorgabe der Klasse | am Apparat gesetzt | aus der Familie)», sonst Warnung P21) und die Zeilen LU KW / LU WW / DU
  (nur, was die Art hat) mit «?» (`warum`: Quelle «LU_DU Zusammenstellung_.xlsx … Zeile n»). Die Werte stehen nicht mehr in der allgemeinen Gruppe «Abgeleitet».
- Leitung (KW, WW, SW): Gruppe «Abgeleitet»: ΣLU KW, ΣLU WW, Grösster Einzel-LU bzw. ΣDU mit «?» (Rechenweg und Beiträge der Apparate als Tabelle in `dialoge.js`, Spalte «Excel-Zeile»), Zeile «n Apparate (Richtung)» mit
  Hinweis bei Masche (P24) bzw. Apparaten ohne Wert.
- Anlage: `abflusskennzahl` als freie Zahl, darunter der Hinweis «Wert aus SN 592000 nach Gebäudeart – Norm liegt nicht vor; Walkringen: 0.5» (ebenso bei der Projektinformation); `spezialinstallationen` als Tabelle
  (Bezeichnung, Versorgung l/s, Entsorgung l/s; «+ Zeile hinzufügen», «×» löscht sofort). Eine Zeile geht erst an den Kern, wenn Bezeichnung und mindestens eine Zahl da sind (sonst Hinweis an der Tabelle); Komma und Punkt
  gehen; Zahlen < 0 lehnt der Kern ab (Meldung am Feld); leere Tabelle sendet `null`. Kein «[object Object]» mehr.
- Ansicht: `belastung` (Schalter, Vorgabe ein: das Häkchen steht, solange nicht `false` gesetzt ist) und `anlage` (Auswahl der Anlagen, bei der Liste Sanitär-Zusammenstellung).

**Strangschema (Kern, `Schema/SchemaGrafik.cs`, `Schema/SchemaBelastung.cs`):** an jeder Sanitärleitung steht neben der Rohrbezeichnung ein zweiter Text – «ΣLU 12 / 8» (Kalt-/Warmwasser, beide > 0), «ΣLU 12» (nur Kaltwasser),
«ΣLU – / 8» (nur Warmwasser), «ΣDU 4.6» (Abwasser); LU und DU auf eine Stelle gerundet, ohne Nachnull; Summe 0 = kein Text; Zirkulation, Regenwasser, Heizung usw. ohne. Die Summen sind die der Abfrage
`sanitaer_belastung` (gleicher Netzkern `SanitaerBelastung`). Ansichtseigenschaft `belastung` (bool): ohne Angabe **ein** (also in jedem Schema mit Sanitärleitungen; Heizungs- und Lüftungsschemas haben keine Sanitärleitungen
und damit nichts zu beschriften), `false` blendet aus; im Detailgrad grob keine Beschriftung (wie die Grössen). Platz: der Text sitzt am selben Abschnitt wie die Grösse und wird nach den Grössentexten freigestellt
(`GroessenTexte.Setzen`): zuerst andere Seite der Linie, verschoben, dann reihenweise weiter von der Linie (`GroessenText.Weicht`, `MaxReihen` 4); die Lage der Grössentexte bleibt wie bisher.

**Liste «Sanitär-Zusammenstellung (W3)»:** Katalog `Ansicht.liste` = `sanitaer_zusammenstellung` (+ Eigenschaft `anlage`); Dialog «Liste anlegen» zeigt bei dieser Liste die Zeile «Anlage» (Kennzeichen- und Namensvorschlag mit der Anlage:
`AN-LI-SANITAER-<Anlage>`). `liste.js` fragt `sanitaer_zusammenstellung { anlage }` ab und frischt bei Modelländerungen auf; `sanitaer-liste.js` baut drei Tabellen: **Apparate** (Apparateart · LU KW · LU WW · DU je Stück · eine
Spalte je Geschoss · Anzahl · ΣLU KW · ΣLU WW · ΣDU, Totalzeile als Fusszeile), **Trinkwasser** (Total LU, Volumenstrom, Spitze Kurve «grösster LU 3» und «grösster LU 5» – die massgebende fett mit «– massgebend», die andere klein
«(nicht massgebend)» –, grösster Einzel-LU, Spezialinstallationen mit Summe, Spitzendurchfluss gesamt, daneben klein die andere Kurve gesamt), **Entwässerung** (ΣDU, K, Q_WW, Spezialinstallationen, Q_tot). Ohne K steht «K fehlt» (rot) statt K,
Q_WW und Q_tot. Die Hinweise des Kerns (`hinweise[]`, z. B. K fehlt, Kurvenwahl nicht bestätigt, Apparate ohne WW-Verbindung bzw. ohne Weg im Netz – P26) stehen
seit dem Prüfbericht 4A als vierte Tabelle **Hinweise** unter den Blöcken (nur wenn es welche gibt; Klasse `li-hinweise`, auch in der CSV), der Kopf nennt nur ihre Zahl. Tooltip je Zeile: Formel mit Excel-Zelle (aus `formeln` des Kerns) und genauer Wert; Tooltip der Apparateart: Quelle mit Excel-Zeile und Kennzeichen der Apparate. Zeilen-/Zellklassen
`li-massgebend`, `li-nebenwert`, `li-fehlt`. Erweiterungen der Listenaufbereitung (für alle Listen nutzbar): Spalte `format` (Funktion), `nurCsv`, `tooltip`; Zeile `klasse`, `zellklassen`, `titel`; Tabelle `klasse`, `ohneSortieren`;
gemeinsame Zellenfunktion `zelle()` in `listenformat.js` für Ansicht und CSV. **CSV** (Knopf «CSV …», N3): alle drei Tabellen untereinander mit Titelzeilen, die Blöcke mit zusätzlicher Spalte «Formel (Excel-Zelle)», Zahlen gerundet wie in
der Ansicht. **Planblätter:** seit Modul 7 Teil A platziert `ansicht_platzieren` auch Listen (Abschnitt «Planblatt wie H+K (Modul 7 Teil A)» am Ende); die CSV-Datei bleibt der zweite Weg.

**Knopf:** Register Gebäudetechnik › Gruppe «Apparate und Armaturen» › «Sanitär-Zusammenstellung» (`sanitaer.zusammenstellung`, kein Kürzel; Symbol `sanZusammenstellung`, Strich und Revit-Stil). Öffnet die Liste der aktiven Anlage
(Anlage des gewählten Elements über `system`/`teilVon`, sonst Anlage der offenen Sanitärliste, sonst die einzige Anlage; bei mehreren fragt ein Dialog) und legt sie beim ersten Mal an. Bei 1920 px bleibt in allen Registern jede Gruppe in Stufe G
(Test `menueband.browser.mjs`, Strich und `HLKS_SYMBOLE=revit`).

**Befunde P21–P26:** erscheinen von selbst im Dialog Verwalten › Modell › «Prüfbefunde» (Abfrage `pruefen` liefert alle Codes des Kerns) mit Code, Element, Text und Vorschlag; Klick wählt das Element. Kein Eingriff nötig (getestet für P25 und P26).

**Prüfen:** `node --test js/oberflaeche/sanitaer-belastung.test.mjs` (Rundung, Palette, Tabelle, aktive Anlage, Liste mit Referenzfall Walkringen, CSV, Verdrahtung); Kern: `SchemaBelastungTests.cs`; Browser:
`HLKS_M4_PORT=5211 HLKS_CDP_PORT=9560 node js/oberflaeche/sanitaer-belastung.browser.mjs` (Ports 5200–5219) (Bilder hell/dunkel nach `%TEMP%\HLKS_CAD\m4_*.png`; `HLKS_KATALOG` auf die Kataloge der Arbeitskopie setzen).

## Planblatt wie H+K (Modul 7 Teil A)

Spezifikation: `entwurf/modul-7.md` (Teil A, Punkte 1–7); Muster: `entwurf/planmuster.md`, echte H+K-Revisionspläne Bernstrasse 21.01.2026. Alles läuft über die Kern-Tür; die Oberfläche setzt weder Buchstaben noch Datum noch Plannummern selbst.

**Kern (Überblick):** `kataloge/buero.json › standorte` (Absender, mit Quelle) · `Plan/Plankopf.cs` (Grundriss-Kopf mit beiden Standorten, Logo, `Projekt:` Objekt + Plantitel, `Rev. Index`/`Rev. Datum`, `Massstab: 1 : 50`, Tabelle
Planänderungen mit beliebig vielen Zeilen – der Kopf wächst nach oben, höchstens zwölf Zeilen sichtbar; Schema-Kopf schmal unten links, nur Bolligen mit Web, ohne Massstab; Revisionsstempel rot, 12°) · `Plan/Revisionen.cs`
(Befehle `revision_hinzufuegen`, `plansatz_revision_hinzufuegen`) · `Plan/Plannummernvorschlag.cs` (Abfrage `plannummer_vorschlag`; `planblatt_anlegen` ohne Nummer/Titel) · `Plan/PlanPruefung.cs` (Befund **P27**, Plannummer doppelt) ·
`Plan/Listentabelle.cs` (Listen als Tabelle im PDF; Ansicht `art liste` auf dem Blatt). Das Titelband «Planinhalt» ist abschaltbar (Eigenschaft `titelband` am Planblatt bzw. in der Projektinformation, Vorgabe **aus**).

**Orte in der Oberfläche**

| Funktion | Ort | Kern |
| --- | --- | --- |
| Planblatt anlegen mit Plannummern-Vorschlag | Register **Ansicht › Planblätter › Planblatt**; Dialog: Gewerk, Geschoss, Art (Geschossplan, FBH, Steigzonen, Grundleitungen, Einlagen, Kanalisation) → Nummer, Titel und Begründung des Kerns; von Hand eingegebene Werte werden nicht überschrieben | Abfrage `plannummer_vorschlag`, Befehl `planblatt_anlegen` (setzt `gewerk`) |
| Revision hinzufügen | Register **Ansicht › Planblätter › Revision …**; Kontextmenü des Planblatts im Projektbrowser **Revision …**; Eigenschaften › `revisionen` › **+ Revision hinzufügen**; Dialog: Planblatt, Beschreibung (Pflicht), Datum (leer = heute) | Befehl `revision_hinzufuegen` (Index = nächster Buchstabe, Datum heute) |
| Revisionsliste | Eigenschaftenpalette des Planblatts: Tabelle Index · Datum · Beschreibung, Zeilen bearbeiten (Enter/Fokuswechsel) und löschen (×) – Muster: Spezialinstallationen (`sanitaer-palette.js`); Bausteine in `planblatt-hk.js` | `element_aendern` (`revisionen`) |
| Revision für alle Pläne eines Satzes | Serien-PDF-Dialog › Kopf des Satzes › **Revision …** (gilt für die gewählten Pläne, gleiche Beschreibung, Index je Plan) | Befehl `plansatz_revision_hinzufuegen` (mit `blaetter`) |
| Revisionsstempel | Serien-PDF-Dialog › **Revisionsstempel auf alle Pläne** (Häkchen, Text «Revisionsplan TT.MM.JJJJ» vorbelegt); sonst Eigenschaft `stempel` am Planblatt bzw. am Plansatz | Parameter `stempel` von `plansatz_pdf`; Vorrang: Ausgabe › Blatt › Plansatz |
| Liste auf dem Planblatt | Register **Ansicht › Planblätter › Ansicht platzieren** (Listenansichten stehen jetzt in der Auswahl); das Planblatt zeigt die Tabelle im PDF-Betrachter | `ansicht_platzieren`, PDF `Plan/Listentabelle.cs` |
| Befund doppelte Plannummer | Verwalten › Modell › Prüfbefunde, **P27** (Warnung am späteren Blatt, Vorschlag: nächste freie Nummer) | `Pruefungen` (`PlanPruefung`) |
| Titelband, Kopfart | Eigenschaften des Planblatts: `titelband` (bool), `kopfart` (grundriss oder schema); Projektinformation: `titelband` | Katalog `Planblatt`, `Projektinformation` |

**Annahmen:** Plannummern-Reihenfolge nach der Kote der Geschosse des Gebäudes (S ab 00, sonst ab 01); FBH ab EG aufwärts (H13 ff.), Steigzonen H21, Sanitär-Sonderblätter = Zahl der Geschosse (Grundleitungen), +1 … +3 (Einlagen, erste freie), +4
(Kanalisation) – nach den Bernstrasse-Plänen; Kälte und Elektro haben keinen Fachbuchstaben (kein Vorschlag). Koordination trägt K (wie die echten Pläne); das bestehende Gewerk-Raster der Plansätze (`PlanGewerk`) kennt K als Kälte – nicht geändert.
Listen im PDF: Arial 2.2 mm, höchstens 45 Zeilen und 380 mm Breite je Liste, Spalten ≤ 60 mm (längere Texte mit «…»); der Umbruch auf weitere Blattbereiche folgt in Teil B.

**Prüfen:** `node --test js/oberflaeche/planblatt-hk.test.mjs`; Kern: `PlanblattHkTests.cs`; Browser: `HLKS_PH_PORT=5179 HLKS_CDP_PORT=9500 node js/oberflaeche/planblatt-hk.browser.mjs` (Bilder `%TEMP%\HLKS_CAD\ph_*.png`; `HLKS_KATALOG` auf die Kataloge
der Arbeitskopie setzen).

## Plandarstellung wie H+K (Modul 7 Teil B1)

Entwurf: `entwurf/modul-7.md` Teil B1, Muster `entwurf/planmuster.md` («Gemeinsam», «Koordination»). Die Oberfläche setzt nur Ansichtseigenschaften (`element_aendern`, Eigenschaftenpalette der Ansicht, aus dem Katalog); gerechnet und gezeichnet wird im Kern. Eigene Knöpfe im Menüband gibt es noch nicht (siehe Offen).

**Kern:** `Grafik/KoordinationGrafik.cs` (Darstellung `koordination`: Gewerke in der Koordinationsfarbe aus `kataloge/gewerke.json`, Architektur grau, Weiche `ImPlan` wie bei den Einlageplänen) · `Grafik/AussparungGrafik.cs`
(Aussparungsboxen: `Boxen`, `BoxZeilen`, Platzierung wie die Beschriftungen mit Führungslinie) · `Plan/Blattverweis.cs` (Text der Schnitt-/Ansichtsmarke «Titel / Plankürzel») · `Grafik/BeschriftungSymbole.cs` (Schnittsymbol) ·
`Plan/Plannummernvorschlag.cs` und `Plan/Plansaetze.cs` (Koordinationsplan = Fach K).

**Orte in der Oberfläche**

| Funktion | Ort | Kern |
| --- | --- | --- |
| Koordinationsplan (Gewerkefarben) | Eigenschaftenpalette der Grundrissansicht › `darstellung` = `koordination` (Auswahlliste aus dem Katalog) | `element_aendern` (`darstellung`), Grafik `KoordinationGrafik` |
| Aussparungsboxen im Fachplan | Eigenschaftenpalette der Grundrissansicht › `aussparungsboxen` (Schalter); im Koordinationsplan immer ein, im Aussparungsplan nie | `element_aendern` (`aussparungsboxen`), Grafik `AussparungGrafik` |
| Schnitt-/Ansichtsmarke mit Blattverweis | keine Bedienung: die Marke in jedem Grundriss zeigt «Titel / Plankürzel» des Planblatts, auf dem die Schnittansicht steht («—» ohne Blatt); folgt dem Verschieben des Ansichtsfensters auf ein anderes Blatt | Ansichten › `ansicht_platzieren`, `element_aendern` (Ansichtsfenster `teilVon`) |
| Planblatt-Fach K | Planblatt anlegen (Ansicht › Planblätter › Planblatt): bei einer Ansicht mit `darstellung koordination` schlägt der Kern «K» vor | Abfrage `plannummer_vorschlag` |

**Darstellung (Annahmen, Prüfliste Christian):** Box = Rahmen, Schrift und Führungslinie im Grau des Gewerks «architektur» (gewerke.json, #808080), Fläche weiss, 2.0 mm Schrift; Zeile 1 Gewerk-Kurzzeichen (S, H, L, K, E; mehrere «HLS»; ohne Leitung «allgemein»),
Zeile 2 «WD 35 x35» bzw. «WD Ø 10» (ganze cm wie der Aussparungsplan), Zeile 3 nur bei Wanddurchbrüchen: «BR +1.88» (Unterkante ab ±0.00, in m) bzw. «Achse −3.49»; Deckendurchbrüche haben wie in der Vorlage («H / DD 15 x15») keine dritte Zeile. Marke: Text mit dem Plankürzel (Plannummer ohne
Projektnummer, wie die Vorlage «Heizung Steigzone 2 / H21»); bei waagrechtem Blick steht der Text neben der Pfeilspitze vom Pfeil weg.

**Offen:** Menüband-Knöpfe «Koordination» und «Aussparungsboxen» (Register Ansicht › Darstellung) – zwei zusätzliche Knöpfe in dieser Gruppe brachten die Stufenprüfung `menueband.browser.mjs` («Ansicht bei 1920 px: Stufen nehmen nie zu») und `serienpdf.browser.mjs` (Knopf «Serien-PDF …») zum Fehlschlagen und wurden deshalb zurückgenommen; sie brauchen eine Anpassung der Gruppenbreiten und eigene Symbole.

**Prüfen:** `node --test js/oberflaeche/plandarstellung-b1.test.mjs`; Kern: `PlanDarstellungB1Tests.cs` (Abnahmebild mit `HLKS_M7B_BILD=%TEMP%\HLKS_CAD`, dann `werkzeuge\pdf_bild.ps1`).
