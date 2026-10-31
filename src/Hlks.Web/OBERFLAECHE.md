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
| `js/werkzeuge/leitung-frei.js`, `frei-abwasser.test.mjs`, `frei-abwasser-hilfe.mjs`, `freie-groesse.browser.mjs`, `abwasser-serie.browser.mjs`; Ergänzungen in `leitung*.js`, `optionen.js`, `optionsleiste.js`, `eigenschaften.js`, `familien-text.js`, `editor-leitung.js`, `editor.js`, `api.js` | Freie Grösse (Rechteckkanal) und Abwasser-Serie: Optionsleiste, Palette, Familieneditor (Nachtrag Modul 3; Abschnitt «Freie Grösse und Abwasser-Serie: Oberfläche») | Agent «Frei/Abwasser» (hlks-modul) |
| `js/oberflaeche/startbildschirm.js`, `start-text.js`, `ordner-browser.js`, `vorschau.js`, `vorschau-logik.js`, `css/start.css`, Tests `start.test.mjs`, `start.browser.mjs`; Server `ZuletztListe.cs`, `StartDienst.cs`, `StartEndpunkte.cs`; Ergänzungen in `dialoge.js`, `app.js`, `aktionen-projekt.js`, `menueband.js`, `symbole.js` | Startbildschirm wie Revit Home, Ordner-Browser, 3D-Vorschaubild (Teil S1; Abschnitt am Ende) | Agent «Start» (hlks-modul) |
| `js/oberflaeche/koordination-*.js`, `aktionen-koordination.js`, `koordination.test.mjs`, `koordination.browser.mjs`; CSS-Block «Koordination» am Ende von `css/oberflaeche.css`; Ergänzungen in `menueband.js`, `aktionen.js`, `symbole.js` | Register Koordination: Projektvorgaben, Kollisionen, Aussparungen, BCF (Modul 3b, C1; Abschnitt am Ende) | Agent «Koordination» (hlks-modul) |
| `js/oberflaeche/koordinationsplan.js`, `koordinationsplan.test.mjs`, `koordinationsplan.browser.mjs`; Ergänzungen in `aktionen-koordination.js`, `menueband.js`, `symbole.js`, `symbole-revit.js` | Knöpfe «Koordinationsplan» und «Aussparungsboxen» im Register Koordination, Optionen `umbruch`/`schrift` der Listen (Modul 7 Teil B3; Abschnitt «Listenumbruch und Koordinationsplan-Knöpfe») | Agent «Listenumbruch» (hlks-modul) |
| `js/oberflaeche/ifc-export-dialog.js`, `ifc-export-text.js` (reine Anfrage, Bericht als HTML, Texte; in Node getestet), `aktionen-ifc.js`, `css/ifc-export.css`, Tests `ifc-export.test.mjs`, `ifc-export.browser.mjs`; Ergänzungen in `aktionen.js`, `menueband.js`, `index.html`, `symbole.js`, `symbole-revit.js` | Dialog «IFC exportieren» im Register Datei › Ausgabe (Modul 7 Teil B2; Abschnitt «IFC exportieren») | Agent «IFC-Dialog» (hlks-modul) |
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
(grundriss|schnitt|ansicht|3d|schema|liste), `massstab` (Nenner), `schnitthoehe` (Handwert; gilt nur bei `schnitthoeheNachSia` false, sonst schneidet der Grundriss nach SIA 400, N21), `detailgrad` (grob|mittel|fein; grob = Schnittflächen schwarz voll, N20),
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
| Koordination (seit Modul 3b, C1, zwischen Ändern und Ansicht) | Gruppen **Vorgaben** (Projektvorgaben), **Kollisionen** (Kollisionen prüfen, Kollisionsliste), **Aussparungen** (Aussparungen vorschlagen, Aussparungen nummerieren, Aussparungsliste, Abgleich, Aussparungsplan anlegen, IFC für Architekt), **Koordinationsplan** (Koordinationsplan, Aussparungsboxen; Modul 7 Teil B3), **BCF** (BCF exportieren, BCF importieren). Keine Kürzel. Beschreibung: Abschnitt «Koordination (Modul 3b, C1)» |
| Ändern | Ändern/Auswählen (MD), Löschen (DE, Entf) mit Trockenlauf-Vorschau der neuen Befunde und Bestätigung, Rückgängig (Strg+Z), Wiederholen (Strg+Y), Eigenschaften ein/aus (PP); seit 1a Verschieben (MV), Kopieren (CO), Drehen (RO), Spiegeln (MM), Mehrfachauswahl mit Strg+Klick |
| Ansicht | Grundriss anlegen, 3D-Ansicht anlegen (3D), Liste anlegen, Schnitt (Werkzeug), Ansicht duplizieren, Sichtbarkeit/Grafiken (VV, VG), Ansichtsvorlage speichern / anwenden, Fenster nebeneinander (WT), Fenster als Reiter (WC), Zoom alles (ZF, ZE), Zoom alle Ansichten (ZA), Design hell/dunkel, **Hülle** (seit 1c, Gruppe «Darstellung»: Umschalter für die Ansichtseigenschaft darstellung huelle/standard, nur Grundriss und 3D); **Schema** (seit Modul 3, C2: Knopf «Schema» in der Gruppe «Erstellen», Gruppe «Schema» mit Verbinden, Trennen, Element einfügen, Neu anordnen – Abschnitt «Schema (Modul 3, C2)»); Planblätter (`aktionen-plaene.js`): Planblatt, Ansicht platzieren (auch Schemas), PDF öffnen |
| XR (seit 26.10.2026, zwischen Ansicht und Verwalten) | alles zu Brille, Quest App und VR/AR: Gruppen **Brille** (Quest verbinden … XQ, App installieren … XI, Autostart XU, Brille zeigen XB), **Zeigen** (In VR begehen XV, Im Sitzungsraum zeigen XS, Raum in AR XR, An die Brille senden XA), **Koordination** (Problem-Marker XM) – Abschnitt «Register XR» |
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
- *Wahl der Ausrichtung:* vier Optionen (Projektbezug, Gebäudeursprung, Koordinationskörper, Einpassen – siehe unten) mit je einem erklärenden Satz; nur bei Abweichung (oder früherer Wahl) wählbar, sonst ausgegraut mit Begründung
  (erste Datei / passt); Koordinationskörper nur, wenn Datei und Projekt einen haben. «Übernehmen» ohne Wahl bei Abweichung lehnt die Oberfläche selbst ab; der Kern lehnt es auch ab.
  Je Option steht leise der Inhaltsanteil (`inhaltJeWahl`; Koordinationskörper: keiner), die vom Kern empfohlene (`lage.empfehlung`) trägt den Vermerk «empfohlen». **Es wird nie vorgewählt**: eine Wahl ist immer ausdrücklich (`vorgabeWahl` gilt nur für eine frühere Wahl).
- *Einpassen (automatisch)* (seit 24.10.2026, Entwurf `entwurf/ifc-einpassen.md`): vierte Wahl `ausrichtung = einpassen` mit dem Satz «KICAD sucht Drehung und Verschiebung selbst: Wände und Raumkanten der Datei werden auf Wände und Räume des Projekts gelegt. Übernommen wird nur bei eindeutiger Lage.»
  Wählbar, wenn die Wahl überhaupt möglich ist (Abweichung oder frühere Wahl, wie die anderen) **und** das Projekt Wände oder Räume hat (sonst gesperrt mit Begründung); bei der ersten Datei nur, wenn der Kern das Einpassen gerechnet hat (`lage.einpassen`).
  Der Anteil steht leise bei der Option (`inhaltJeWahl.einpassen`), «empfohlen» trägt sie bei `lage.empfehlung = "einpassen"`; **nie vorgewählt** (`vorgabeWahl` kennt sie nur als frühere Wahl). `WAHL_TEXT.einpassen` = «Eingepasst über Wände und Raumkanten» (Zeile «Ausrichtung» im Lagebericht und Spalte in «Koordinaten»).
  Gibt es `lage.einpassen`, hat der Lagebericht einen **Abschnitt «Einpassen»**: Ergebnis (`eindeutig`; `unsicher` und `keine Merkmale` **rot**, mit «es wird nichts übernommen»), Text des Kerns, Paarung, Drehung und Verschiebung (Abbildung von der Lage «Gebäudeursprung» ins Projekt), Bezug
  (bei nicht eindeutig als «Vorschlag, nicht übernommen»), Treffer (50/100/150 mm, Median, Proben), nächstbeste Lage, Merkmale (Datei und Projekt: Wände, Räume, Kanten), Gegenprobe (Überdeckung mit dem eingepassten Bezug) und eine Tabelle je Geschoss (Proben, Anteil 150 mm, Median).
  Im roten Kasten steht bei Empfehlung (Feld `empfehlung` oder der Satz «Empfehlung: ausrichtung = einpassen» im Kerntext) darunter der Hinweis «Empfehlung: Ausrichtung «Einpassen (automatisch)» wählen – damit liegen 97 % …», dazu die Zeile «Eingepasst: 97 % …»; ist das Einpassen nicht eindeutig, der Hinweis auf den Abschnitt.
  Die *Vorschau* (Trockenlauf) zeigt den Abschnitt aus dem Ergebnis (`lage.einpassen` oder `einpassen`; fehlt er dort, stehen die Zahlen in der Hinweis-Zeile des Kerns «Eingepasst über … : Drehung …°, Verschiebung … m; …»), der Ergebnisdialog nach der Übernahme ebenso (sonst aus der gespeicherten Lage der Unterlage).
  Ist die Lage nicht eindeutig, lehnt der Kern ab (auch im Trockenlauf): sein Text (der Lagebericht, mit Zeilenumbrüchen) und der Vorschlag (Koordinationskörper, `bezug`) stehen im roten Fehlerkasten des Dialogs; nichts wird übernommen. Die Oberfläche rechnet nichts, sie rundet nur.
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

**IFC: Original oder Nachbau zeigen (N12, 21.10.2026):** eine IFC-Unterlage kennt zwei Darstellungen: das **Original** (das Netz aller Produkte der Datei) und den **Nachbau** (die daraus übernommenen Elemente, `kennungen.quelle` = Id der Unterlage).
Welche eine Ansicht zeigt, steht an der Ansicht: Eigenschaft `ifcDarstellung` = `nachbau` (Vorgabe, wie bisher: Nachbau plus nicht übernommenes Original als Hintergrund) | `original` (Original aller Produkte; die Nachbau-Elemente dieser Unterlage
entfallen in der Ansicht) | `beide` (übereinander zum Vergleichen). Bedienung: Register Ansicht › Grafik › **Sichtbarkeit/Grafiken** (VV) – oben die Auswahl «IFC-Unterlagen zeigen» (nur wenn das Projekt eine IFC-Unterlage hat und die Ansicht Grundriss, Schnitt,
Ansicht oder 3D ist), oder die Eigenschaft in der Eigenschaftenpalette der Ansicht. Die Einstellung gehört zu den Einstellungen der Ansichtsvorlage (`VORLAGE_EINSTELLUNGEN`, ein Wert «nachbau» in der Vorlage setzt sie in der Ansicht zurück).
Farben an der **Unterlage**: `farbeOriginal` (#rrggbb, Vorgabe grau #8a8a8a) mit `deckkraft` (Vorgabe 0.5) für das Original; `farbeNachbau` und `deckkraftNachbau` für den Nachbau (Vorgabe: leer = normale Darstellung, keine Übersteuerung). Bedienung: Dialog **«Farben …»**
(Knopf in der Zeile der IFC-Unterlage in «Unterlagen verwalten», auch im Kontextmenü der Unterlage im Projektbrowser): Farbwähler und Deckkraft-Regler je für Original und Nachbau, beim Nachbau mit Haken «Eigene Farbe und Deckkraft» (ohne Haken: Eigenschaften leer).
Wirkung: *Grundriss* – `GrundlagenIfc.Plan` schneidet je nach Modus nur den Hintergrund oder alle Produkte auf Schnitthöhe (zwei Zwischenspeicher je Datei); der Kern zeichnet den Nachbau in der Farbe der Unterlage (Linie und Füllung, Schraffur entfällt, Deckkraft der Füllung bzw.
der Linie; der gemeinsame Wandumriss wird je Übersteuerung getrennt vereinigt). *Schnitt und Ansicht* – `Schnittgrafik`: das Original erscheint als Schnittlinien der Netze mit der Ebene der Schnittlinie (`GrundlagenIfc.SchnittSenkrecht`, dieselbe Schnittfunktion
`Netzflaechen.Schneiden`), nur bei `original` und `beide`; der Nachbau in der Ansicht wird wie im Grundriss gefärbt. *3D* – `ifc-darstellung.js` (rein): `netzAuswahl(ansicht)` wählt `leseNetz({ nur })` (`hintergrund` bzw. `alle`; Zwischenspeicher `netze` je Unterlage mit
`nur`), `filter.sichtbar(el, ansicht, nachId)` lässt den Nachbau bei `original` weg, `baueKoerper({ ueberschreiben })` färbt ihn; eine Änderung an der Unterlage baut nur deren Nachbau-Körper neu. Das Original in der Farbe der Unterlage kommt als Körperteil `netz` (`farbe`, `deckkraft`) vom Kern.
Die Einstellung gilt nur in der Darstellung «standard» (Hülle, Aussparungs-, Einlage-, Koordinationsplan haben eigene Farben) und nur für angezeigte Unterlagen. Kollision, Aussparungen und IFC-Export lesen das Modell und bleiben unverändert.
Offen: eine Projektion des Originals in der Ansicht (Fassade) statt der Schnittlinien; im Plan bleibt die Kontur einer gefüllten Fläche deckend, auch bei transparenter Füllung.
Prüfen: `node --test js/ansichten/ifc-darstellung.test.mjs`, Kern `IfcDarstellungTests`, Browser `js/ansichten/ifc-darstellung.browser.mjs` (Port 5500, CDP 9500; `IFC=<Datei>` für echte Daten).

**Koordinaten** (Register Verwalten, `verwalten.koordinaten`, nur lesend): ein Satz Erklärung, der Projektbezug (`ifcBezug` der Projektinformation) mit Quelle, Landeskoordinaten und Koordinationskörper,
darunter alle IFC-Unterlagen mit Fassung, Ausrichtung und Abweichung (Klick auf eine Zeile = Lagebericht). Bei `ausrichtung = einpassen` steht in der Spalte «Ausrichtung» «Eingepasst über Wände und Raumkanten» mit der Kurzfassung des Kerns darunter
(«eindeutig · Drehung -42.718° · 88 % innert 150 mm»); der Lagebericht der Unterlage (Klick, Kontextmenü «Lagebericht zeigen», «Unterlagen verwalten › Lage») zeigt den ganzen Abschnitt «Einpassen».

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
    Schraffur folgt der Achse (N8): Die Primitive tragen `stil.musterWinkel` (Richtung der Wandachse, Grad, 0 … 180; zu den Winkeln der Schraffur addiert) und
    `stil.musterUrsprung` (Fusspunkt des Lots vom Ursprung auf die Achse); `patternTransform="rotate(w) translate(tx ty)"`, die Verschiebung hält eine Musterlinie
    bzw. das Punktraster im Anker (auf 0.5 mm gerundet, damit nur wenige `<pattern>` entstehen). Ohne die Felder gilt das achsparallele Muster wie bisher.
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
    **nur die Hintergrund-Produkte (Kennzeichen 0; seit N12 bei `ifcDarstellung` original/beide alle Produkte, Option `nur`)** zu EINER Geometrie zusammen (`leseNetz`: Punkte gleich in three-Metern, Indizes mit Versatz; die übernommenen zeichnet der Kern als Körper). Eine
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
**Geometrie übernehmen** (30.10.2026; `familie.aus-geometrie` im Kontextmenü «Familien», `familie.netz-uebernehmen` im Register «Familie», Gruppe Körper): Unterlage (IFC, STL, OBJ, STEP), Objekt (IFC-GlobalId, Kennzeichen eines übernommenen Elements – vorbelegt aus der Auswahl – oder Id eines Befunds form3d), ohne Editor Kategorie und Name der neuen Familie, Drehung (leer: IFC automatisch) und Nullpunkt; dann `familie_netz_uebernehmen`. Der Körper «netz» trägt die Herkunft; Anschlüsse und Parameter ergänzt man danach. Körperarten im Register: dazu **Übergang** (rund/rechteckig) und **Netz**. Die 2D-Ansichten zeichnen die Masse der Familie (`masse` mit von/bis) als Masslinien.

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
- **Regeln:** erlaubte Bogenwinkel (Liste in Grad, getrennt durch Komma, Semikolon oder Leerzeichen, Dezimalpunkt; **«frei»**; leer = nicht angegeben, der Kern lässt dann jeden Winkel zu), Biegeradius (Zahl oder Formel), Abzweig (T-Stück), **Abzweigwinkel** (Liste in Grad, leer = nur 90°; Nachtrag B7), **Mischbar mit** (andere Leitungsserie der gleichen Kategorie, deren Formteile der Kern nimmt, wenn die eigene Formteil-Familie keinen passenden Typ hat – `regeln.mischbar`, 31.10.2026; Christian: «geberit silent und pe können gemischt werden»).
- **Kontrolle** (Abfrage `leitungsserie`, bei jeder Änderung und bei Änderungen anderer Familien des Projekts neu geholt): Tabelle je Grösse (Typ) mit Grösse, aussen, innen, wand und **Bogen, T-Stück, Reduktion, Reinigungsöffnung ja/nein** (Reduktion «n von m» auf die kleineren Grössen, «–» bei der kleinsten; Tooltip mit Familie und Typ bzw. dem Grund des Kerns). Darunter die Liste **«Nicht verfügbar»**: Art, betroffene Grössen und der Grund mit Vorschlag des Kerns (je Art zusammengefasst; nennt der Grund eine Grösse, steht der erste als «z. B.»).
  Ist die Definition ungültig oder ein Typ ohne Werte, steht der Kern-Fehler in der Tabelle («nicht auswertbar: … hat im Typ «Standard» keinen Wert → Werte auf der Seite «Typen» eintragen»).

**Fangkanten (Kern):** `HlksGrafik.Leitung` liefert für jede Leitung mit Lauf im Grundriss die **Achse als Fangkanten** (je Teilstück eine, wie die Achse einer Wand); die Fangpunkte (Enden, Knicke) bleiben. Das gilt für Leitungen mit und ohne Serie; senkrechte Leitungen (ein Punkt im Grundriss) haben keine Kante. Die Fangarten Schnittpunkt, Mitte, Lot und nächster Punkt greifen damit auch auf Leitungen.

**Liste anlegen** (Register Ansicht › Erstellen › «Liste»): die Auswahl enthält jetzt «Materialliste».

**Annahmen** (Prüfliste Christian, Bedienung, keine Normwerte): (1) Die Vorlage hat **keine Werte** und keine Formteil-Familien; der Typ heisst «Standard» (die Typen einer Serie sind ihre Grössen – du benennst und ergänzt sie aus der Unterlage). (2) Ein Rohr bietet nur «rund» an (der Kern lehnt rechteckig ab). Die abstrakte Basisklasse «Leitung» bietet alle drei Formen an (Rohr rund, Kanal rund, Kanal rechteckig). (3) Bei rechteckig steht `aussen` = `b` (der Block verlangt es, der Kern nutzt es dort nicht). (4) Die Ansichten rechts (Körper) entfallen bei Serien; eine Serie öffnet auf «Leitungsserie», solange nichts anderes gemerkt ist.
(5) Winkel und Abzweigwinkel: Dezimalpunkt, das Komma trennt («7,5» ist 7 und 5). Die Bereiche (0 < w ≤ 180 bzw. ≤ 90, doppelte Winkel) prüft der Kern. (6) Fangkanten gelten auch für Leitungen ohne Serie (Altbestand).

**Befunde:** (1) Der Kern nennt bei Reduktionen und Bögen den Grund je Grösse mit der Grösse im Text; die Liste fasst deshalb nach Art zusammen und zeigt den ersten Text (ein Feld «Stelle» bzw. die Grössen je Grund wären robuster). (2) `leitungsserie` meldet bei `winkelRegel: "keine"` trotzdem `winkel: "frei"` – die Kontrolle liest `winkelRegel`. (3) Die abstrakte Basisklasse «Leitung» ist als Kategorie wählbar (der Kern nimmt sie an); ob eine Serie dieser Kategorie von Rohr- oder Kanal-Elementen benutzt werden kann, ist offen.

**Prüfen:** `node --test js/familien/familien-leitung.test.mjs` (21 Tests: Vorlage Rohr und Kanal, Definition ↔ Formular, Validierung, HTML, Kontrolltabelle, Verdrahtung; mit `HLKS_TEST_SERVER` zusätzlich gegen den echten Kern, nur Trockenläufe: Vorlagen aller familienfähigen Kategorien, alle ops der Seite, Ablehnungen mit Stelle und Vorschlag) und alle bisherigen `*.test.mjs`; Kern: `LeitungFangkantenTests.cs`; Browser (Edge headless über CDP, der Test startet **seinen eigenen Server** mit frischem Beispielprojekt, Port 5164, Build-Ordner `%USERPROFILE%\HLKS_CAD_build\m3c3`, `HLKS_BAU` ändert ihn):
`SHOTS=<Ordner> node js/familien/familien-leitung.browser.mjs` (Testrohrserie öffnen: Seiten, Felder, Kontrolltabelle; Formteil-Familie wechseln; Winkel ändern, rückgängig, Fehler der Seite und des Kerns; Typregel; Neue Familie Rohr und Kanal ohne Kern-Fehler, Werte von Hand, Querschnitt wechseln; Liste «Materialliste» anlegen; Fangkanten in der Plangrafik; Bilder dunkel und hell). Alle Werte der Testserien sind FIKTIV (`tests/daten/familien/test_*.familie.json`).

## Freie Grösse und Abwasser-Serie: Oberfläche (Nachtrag Modul 3, `entwurf/freie-groesse-kanal.md` und `entwurf/abwasser-serie.md`, Abschnitte «Oberfläche»)

Die Oberfläche bedient die Kerne «freie Grösse» (Kanalserien mit `leitung.frei`, Prüfung P29) und «Abwasser-Serie» (`leitung.einsatz = abwasser`, Grösse `{ d }`, Prüfung P30). **Sie rechnet nichts nach:** ob eine Grösse innerhalb von Grenzen und Schritt liegt, welcher Formteil-Typ passt, wie beschriftet wird und welche Befunde entstehen, sagt der Kern (Trockenlauf der Befehle, Abfragen `familie`, `leitungsserie`, `pruefen`). Die Oberfläche liest, baut den Parameter `groesse` und zeigt Antworten verständlich an.

| Datei | Inhalt |
| --- | --- |
| `js/werkzeuge/leitung-frei.js` | rein: Zahlenfelder lesen, **leichte Serie** aus der Abfrage `familie`, Anzeige «frei» bzw. «Normgrösse», Parameter `groesse` ({ b, h } / { d } / Typname), Pflichtangaben, Hinweis- und Befundtexte |
| `js/werkzeuge/frei-abwasser.test.mjs` | Node-Tests der Logik, der Optionsleiste, der Palette, des Editors und (mit `HLKS_TEST_SERVER`) gegen den Kern |
| `js/werkzeuge/frei-abwasser-hilfe.mjs`, `freie-groesse.browser.mjs`, `abwasser-serie.browser.mjs` | Browser-Reihen (gemeinsame Hilfen: eigener Server, Edge über CDP, Aufzeichnung der Abfragen der Oberfläche) |
| Ergänzungen in `leitung-logik.js`, `leitung-daten.js`, `leitung.js`, `leitung-palette.js`, `leitung-bearbeiten.js`, `optionen.js`, `optionsleiste.js`, `oberflaeche/eigenschaften.js`, `familien/familien-text.js`, `editor-leitung.js`, `editor.js`, `api.js`, `css/oberflaeche.css`, `css/familien.css` | siehe unten |

**Leichte Serie (wichtig).** Die Abfrage `leitungsserie` rechnet alle Grössen mit allen Formteilen (Reduktionen auf jede kleinere Grösse) und dauert bei einer grossen freien Kanalserie bis 155 s; der Kern bedient andere Anfragen in dieser Zeit erst danach (Befund 21.10.2026: eine zweite Abfrage wartet auf die erste). Die Oberfläche stellt sie deshalb für freie Serien **nie ungefragt**: `serieLaden` (`leitung-daten.js`) liest zuerst die Abfrage `familie` (schnell: Block `leitung`, Werte je Typ); hat der Block `frei`, entsteht daraus die leichte Serie (Form der Antwort von `leitungsserie` – `groessen`, `winkel`, `winkelRegel`, `frei`, `einsatz` –, aber ohne `aussen`/`innen`/`wand`, Formteilverfügbarkeit, Beschriftung und Probe; `leicht: true`). Optionsleiste, Werkzeug, Palette und «Aufteilen mit Serie …» benutzen nur diese. Alle anderen Serien: Abfrage `leitungsserie` wie bisher (Winkel, Masse, Beschriftung).
Der Gummiband-Streifen zeigt bei einer freien Serie die gewünschte Breite (nur Anzeige).

**Optionsleiste Rohr (PI) und Kanal (DT).**
- *Freie Serie:* neben «Serie» die Felder **«Breite»** und **«Kanalhöhe»** (rund: «Durchmesser»), vorbelegt mit der Vorlage der Serie (beim Serienwechsel immer, beim Start des Werkzeugs nur leere Felder; Werte `freiB`, `freiH`, `freiD`, werden gemerkt). Die bisherige Typwahl heisst **«Normgrösse»** («Vorschlag …») und listet die Rastertypen als «400 × 200»; eine Wahl trägt ihre Grösse in die Zahlenfelder ein, geändert wird dort von Hand. Dahinter der Hinweis «frei b × h (145 … 2500 mm, Schritt 1 mm) – die Normgrössen sind Vorschläge; der Kern prüft Grenzen und Schritt» (Text wie `Leitungsserie.FreiText`). Pflicht sind Breite und Höhe statt der Typwahl (Feld orange, solange leer).
- *Abwasser-Serie:* die Grösse heisst **«d 110»** (Typname folgt in Klammern, wenn er anders lautet), keine Zahlenfelder; der Hinweis «Abwasser-Serie: Grösse nach Aussendurchmesser, Beschriftung «PE 110»; nur für Schmutz- und Regenwasser» folgt der gewählten Grösse (Beschriftung vom Kern aus `leitungsserie.groessen[].beschriftung`).
- *Zeichnen:* `leitung_zeichnen` und `leitung_abzweigen` bekommen bei einer freien Serie `groesse: { b, h }` bzw. `{ d }` (auch bei einer Rastergrösse – der Kern nimmt dann den Typ), sonst den Typnamen. Ein Abzweig übernimmt Breite und Höhe der angeklickten Leitung in die Zahlenfelder. **Abgelehnt** (Grenzen, Schritt) steht der Text des Kerns in der Vorschau-Zeile mit seinem Vorschlag («Vorschlag: 2500 × 270 …»; das Wort steht nur einmal); nie eine stille Rundung. Die Einzelheiten der Vorschau tragen «frei» (Gerade) bzw. «frei (Exemplarwerte)» (Formteil aus der Vorlage); **Befunde, die das Ausführen auslösen würde** (P29, P30, P13–P18 …), stehen unter der Zeile («Prüfbefund bei Ausführung: P30 RO-5: Abwasser-Serie … für Heizung Vorlauf …»), nach dem Ausführen als Warnung.

**Eigenschaftenpalette, Gruppe «Leitung».**
- *Freie Serie:* Zeile «Grösse» mit «430 × 270 (frei)» bzw. «400 × 200 (Normgrösse 400x200)» (eine Rastergrösse bleibt ein normaler Typ, jede andere heisst «frei»; verglichen wird mit den Rastertypen), **Zahlenfelder «Breite» und «Höhe»** (rund «Durchmesser»), «Normgrösse» als Auswahl (leer: «frei (keine Normgrösse)»), darunter Grenzen und Schritt. `breite`, `hoehe`, `durchmesser` stehen dann nicht noch einmal unter «Eigenschaften». Eine Änderung fragt wie bisher «Nur diese Leitung / Ganzer Leitungszug» und sendet `leitung_groesse { groesse: { b, h } }` bzw. den Typ; Ablehnungen des Kerns stehen mit Vorschlag am Feld (die Eingabe bleibt stehen), «keine Zahl» meldet die Palette selbst ohne Dialog.
- *Abwasser-Serie:* Auswahl «d 32 … d 110», Zeilen **Einsatz** «Abwasser», **Aussendurchmesser** (`durchmesser`), **DN (Hersteller)** (nur wo der Typ eine DN der Unterlage trägt, nie abgeleitet) und **Beschriftung** (vom Kern, «PE 110»); `durchmesser` und `dn` stehen nicht noch einmal unter «Eigenschaften».
- *Befunde:* bei Leitungen freier und Abwasser-Serien fragt die Palette die Prüfung des Kerns (`pruefen`, einmal je Modellstand zwischengespeichert) und zeigt **P29** (Grösse ausserhalb der Grenzen bzw. nicht im Schritt) und **P30** (Abwasser-Serie für ein anderes Medium) der Leitung als Zeile «Befund P30» mit Text und Vorschlag. Nach einer Änderung über die Palette erscheinen neue P29/P30 als Warnung.

**Familieneditor, Seite «Leitungsserie».**
- **Einsatz** (Rohre): Auswahl «(nicht angegeben)» / «Abwasser (Schmutz-, Regenwasser)» → `leitung.einsatz`; bei Abwasser zusätzlich **«Grösse geführt nach»** (Aussendurchmesser d / Nennweite DN): ein Wechsel nimmt den Typparameter mit diesem Namen oder legt ihn (ohne Werte) im selben Paket an; die Grösse heisst dann «Grösse Aussendurchmesser d». Ob d ohne Einsatz gilt, entscheidet der Kern (Einsatz leeren bei Grösse d: abgelehnt, nichts gespeichert).
- **Block frei** (nur Kanalserien): Gruppe «Freie Grösse (nach Mass gefertigt)» mit **Vorlage (Typ)** (Auswahl unter den Typen; leer entfernt den ganzen Block samt Grenzen und Schritt), **Grenzen je Seite** (kleinste/grösste Breite bzw. Höhe bzw. Ø; leer = keine Grenze, das Gegenstück bleibt) und **Schritt** (ganze mm, leer = 1). Ohne Vorlage sind Grenzen und Schritt gesperrt. Die Quellen der Grenzen (`frei.quellen`) bleiben unberührt; ein Hinweis nennt ihre Zahl bzw. «Grenzen ohne Quelle».
- **Vorlage der Formteile** (`formteile.<art>.vorlage`): je Art eine Auswahl unter den Typen der gewählten Formteil-Familie (leer = erster Typ), sichtbar bei freier Serie oder wenn die Vorlage gesetzt ist.
- **Kontrolle auf Abruf (freie Serien):** der Editor holt die Abfrage `leitungsserie` hier **nie ungefragt** (auch nicht nach Änderungen). Statt «wird geladen» steht der Knopf **«Kontrolltabelle und Probe berechnen»**; während der Kern rechnet zeigt die Seite Drehzeichen, **Laufzeit in Sekunden** und **«Abbrechen»** (bricht das Warten ab, der Kern rechnet im Hintergrund zu Ende; die Seite bleibt bedienbar, Seitenwechsel und Eingaben gehen, der Kern bedient sie aber erst nach der Berechnung); abgebrochen: «Noch einmal berechnen». Das Ergebnis ist die bekannte Kontrolltabelle plus die **Probe** der Abfrage (eine freie Grösse in der Mitte der Grenzen, sonst 430 × 270, mit Massen und Formteilverfügbarkeit; Tooltips nennen die Exemplarwerte der Formteile); nach einer Änderung der Definition steht «Stand vor der letzten Änderung der Definition» und der Knopf «Neu berechnen». Serien ohne freie Grösse (auch Abwasser-Serien) laden die Kontrolle wie bisher von selbst.
- **Kontrolltabelle bei Abwasser-Serien:** Grösse «d 110», zusätzlich die Spalten **«Beschriftung»** («PE 110», vom Kern) und **«DN Hersteller»** (– ohne DN); die Kopfzeile nennt «Einsatz Abwasser (Grösse nach Aussendurchmesser)».

**Annahmen** (Prüfliste Christian, Bedienung, keine Normwerte): (1) Eine Rastergrösse einer freien Serie wird in der Palette an der Übereinstimmung der Zahlen mit einem Rastertyp erkannt, nicht an `familientyp` (der steht bei freien Leitungen auf der Vorlage). (2) Die Zahlenfelder der Palette senden beide Werte zusammen; die Rückfrage «Nur diese Leitung / Ganzer Leitungszug» erscheint bei jeder Änderung. (3) Der Gummiband-Streifen zeigt bei freien Serien die Breite bzw. den Durchmesser aus der Optionsleiste, nicht das Aussenmass (das hat die leichte Serie nicht). (4) Die Prüfbefunde der Palette kommen aus `pruefen` (ganzes Projekt, einmal je Modellstand), nur für Leitungen freier und Abwasser-Serien. (5) Die Kontrolle wird bei freien Serien nur auf Knopfdruck geholt, unabhängig von der Zahl der Typen.

**Befunde / offen:** (1) `leitungsserie` blockiert den Kern für alle anderen Anfragen, solange sie rechnet (zu beheben im Kern; die Oberfläche vermeidet sie). Wird bei einer Serie ohne `frei` mit sehr vielen Typen dasselbe nötig, gilt dasselbe. (2) Das Abzweig-Werkzeug zweigt immer rechtwinklig ab; eine Serie mit `regeln.abzweigwinkel` ohne 90° (z. B. 45° und 88.5°) lehnt das der Kern ab («erlaubt Abzweigwinkel 45°, 88.5°»), die Vorschau zeigt seinen Text – Winkel der Serie anbieten ist nicht gebaut. (3) Die Palette zeigt P29/P30 erst, wenn der Kern sie meldet; ein Befund nach einer fremden Änderung erscheint beim nächsten Neuzeichnen. (4) Der Anschlussfang nennt die Grösse eines Abwasser-Anschlusses als «Ø110» (`anschlussGroesse` in `leitung-logik.js`), nicht als «d 110» – die Antwort `anschlusslagen` unterscheidet Aussendurchmesser und Kanal-Ø nicht.

**Prüfen:** `node --test js/werkzeuge/frei-abwasser.test.mjs` (39 Tests; mit `HLKS_TEST_SERVER` zusätzlich gegen den echten Kern: leichte Serie = `leitungsserie`, Parameter der Oberfläche im Trockenlauf, Grenzen und Schritt mit Vorschlag, P30, ops des Editors) und alle bisherigen `*.test.mjs`; Browser (Edge headless über CDP, jeder Test startet **seinen eigenen Server** mit frischem Beispielprojekt, Build-Ordner `%USERPROFILE%\HLKS_CAD_build\ui_frei_abwasser`, `HLKS_BAU` ändert ihn; Ports 5346 und 5347, Debug-Ports 9446 und 9447): `SHOTS=<Ordner> node js/werkzeuge/freie-groesse.browser.mjs` (Optionsleiste, 430 × 270 mit Bogen, Ablehnungen, Normgrösse, Abzweig 250 × 200, Palette mit Rückfrage, Ablehnung und P29, Editor mit Block frei und Vorlage der Formteile, Kontrolle auf Abruf mit Probe, grosse Serie mit 225 Grössen: nie ungefragt, Laufanzeige, Abbrechen; protokolliert, dass die Abfrage `leitungsserie` nie ungefragt kommt) und `SHOTS=<Ordner> node js/werkzeuge/abwasser-serie.browser.mjs` («d 110», Hinweis, d 110 und d 32, Bogen 45°, Abzweig, P30 in Vorschau, Meldung und Palette, Palette mit Einsatz, Aussendurchmesser, DN, Beschriftung, Editor mit Einsatz und Kontrolltabelle). Alle Werte der Testserien sind FIKTIV (`tests/daten/familien/test_kanalfrei*`, `test_abwasser_d_*`).

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
**Vorgaben** (Projektvorgaben) · **Kollisionen** (Kollisionen prüfen, Kollisionsliste) · **Aussparungen** (Aussparungen vorschlagen, Aussparungen nummerieren, Aussparungsliste, Abgleich, Aussparungsplan anlegen, IFC für Architekt) · **Koordinationsplan** (Koordinationsplan, Aussparungsboxen; Modul 7 Teil B3, Abschnitt «Listenumbruch und Koordinationsplan-Knöpfe») · **BCF** (BCF exportieren, BCF importieren). Gesperrt mit Hinweis im Tooltip: Nummerieren ohne Aussparung,
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

- **Eine Tabelle** (`js/oberflaeche/projektbrowser-gliederung.js`, `GLIEDERUNGEN`): je Gliederung die Äste in ihrer Reihenfolge (Quelle, Auswahl, Titel, Farbe). Eine weitere Gliederung (z. B. Elektro/MSRL) ist ein Eintrag
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

## Nachbearbeitung N10 und N11: Maustasten in allen Ansichten, Navigationswürfel (`nachbearbeitung.md`)

**N10 – mittlere Taste verschiebt überall** («mittlere maustaste = plan verschieben»). Die Zuordnung Taste → Aktion steht an einer Stelle: `ansichten/maus.js`
(`tasteAktion`, rein, in Node getestet). Die mittlere Taste geht nie an ein Werkzeug und löst nie den Autoscroll des Browsers aus (`mousedown`/`auxclick` verhindert).

| Ansicht | links | Mitte | Umschalt + Mitte | Rad | Doppelklick Mitte | rechts |
| --- | --- | --- | --- | --- | --- | --- |
| Grundriss, Schnitt, Ansicht (`grundriss.js`), auch bei aktivem Werkzeug | wählt bzw. Werkzeug | verschiebt | verschiebt | zoomt zum Zeiger | alles zeigen | Werkzeug beenden / –  |
| Schema (`schema.js`) | wählt, Knoten ziehen | verschiebt | verschiebt | zoomt zum Zeiger | alles zeigen | Kontextmenü |
| 3D (`ansicht3d.js`, OrbitControls) | dreht (wie bisher) | verschiebt | **dreht** (Orbit um die Auswahl bzw. Modellmitte) | zoomt zum Zeiger | alles zeigen | verschiebt (wie bisher) |
| Familieneditor 2D (`editor-ansicht.js`, Fenster2D) | verschiebt, Punktwahl | verschiebt | verschiebt | zoomt zum Zeiger | alles zeigen | – |
| Familieneditor 3D (Fenster3D) | dreht | verschiebt | dreht | zoomt zum Zeiger | alles zeigen | verschiebt |
| Unterlagen-Bild (`unterlagen-bild.js`) | Klick = Punkt wählen | rollt das vergrösserte Bild | rollt | Zoom über die Knöpfe | Zoom 1, ganz | – |
| Planblatt (`planblatt.js`, PDF-Betrachter des Browsers) | Betrachter | **Autoscroll des Betrachters** | – | Betrachter | – | – |
| Listen | – | (nicht zoombar; rollen mit dem Rad) | – | rollt | – | – |

- **Doppelklick Mitte** zählt nur, wenn der erste Klick ohne Ziehen blieb (Weg ≤ 4 px, Frist 400 ms nach dem Loslassen): zwei kurze Verschiebungen kurz nacheinander zeigen nicht alles
  (`mitteDoppelklick` in `maus.js`; `grundriss.js` und `schema.js` führen dafür `druck.weg` mit).
- **Gemeinsame Hilfen** (`maus.js`): `mitteVerschieben(el, { beiBeginn, beiBewegung, beiEnde, beiDoppelklick })` (Aufnahmephase, hält das Ereignis an; Familieneditor 2D, Unterlagen-Bild),
  `ORBIT_TASTEN` (Belegung für OrbitControls: links ROTATE, Mitte PAN, rechts PAN; die Steuerung vertauscht Drehen und Verschieben mit Umschalt/Strg), `orbitAktion` (Nachbildung dieser Regel,
  der Node-Test prüft sie gegen `tasteAktion`), `rollenderVorfahre`.
- **3D:** `steuerung.zoomToCursor = true`. Umschalt + Mitte: ein Aufnahme-Handler am Behälter setzt das Ziel der Steuerung auf der Blicklinie auf die Tiefe der Auswahl bzw. der Modellmitte
  (`schwenkZiel`, rein), damit der Blick beim Anfassen nicht springt und um den Drehpunkt gedreht wird. Prüfhilfe: `.d3-wurzel[data-kamera]` (JSON: `ziel`, `lage`, `blick`).
- **Planblatt (Befund):** das Blatt ist das PDF im eingebauten Betrachter des Browsers (iframe); dessen Ereignisse sieht KICAD nicht. Die mittlere Taste löst dort den Autoscroll des Betrachters aus
  (das Blatt läuft weiter, solange der Zeiger vom Druckpunkt entfernt ist), links zieht nicht. Ein eigener Betrachter (z. B. pdf.js) wäre nötig; nicht gebaut.
- **Prüfen:** `node --test src/Hlks.Web/wwwroot/js/ansichten/maus.test.mjs` (Zuordnung, Doppelklick, Würfelrechnung);
  `node src/Hlks.Web/wwwroot/js/ansichten/maus-wuerfel.browser.mjs` (Port 5460, CDP 9460; Edge headless, echte Maus über CDP; Bilder unter `%TEMP%\HLKS_CAD\n11\`).

**N11 – Navigationswürfel** (analog Revit ViewCube), `wuerfel.js` (zeichnen, bedienen) und `wuerfel-logik.js` (Rechenregeln, rein):

- **Aufbau:** oben rechts in der 3D-Ansicht (`.d3-wuerfel`, 128 px); eigene kleine Szene und Kamera, in einen Ausschnitt (Viewport/Scissor) der Leinwand der 3D-Ansicht gezeichnet (kein
  zweiter WebGL-Kontext). Die kleine Kamera folgt der Richtung der Hauptkamera (`d3-wuerfel` dreht mit). Darüber liegt der Bedienbereich (`.d3-wuerfel-flaeche`), der die Zeigerereignisse annimmt;
  Treffer über einen Strahl in der kleinen Szene. Flächen hellgrau, Kanten dunkel, Beschriftung deutsch (OBEN, UNTEN, VORNE, HINTEN, LINKS, RECHTS), Farben über `--wuerfel-*` in `hlks.css`
  (hell und dunkel). Würfelachsen: +x RECHTS, +y OBEN, +z VORNE.
- **26 Bereiche:** jede Fläche ist in 3 × 3 Teilflächen geteilt (Schwelle 0.62): Mitte = Fläche, Rand = Kante, Ecke = Ecke (`teilflaechen`, `bereichVonPunkt`). Hover färbt alle Teilflächen des Bereichs
  (`--wuerfel-hover`), `title` und `data-bereich` nennen ihn («VORNE-RECHTS (Kante)»). Klick = weiche Kamerafahrt (380 ms, `zwischenstand`; Azimut auf dem kurzen Weg) zur Standardansicht:
  Fläche senkrecht, Kante 45°, Ecke Isometrie; Abstand und Ziel bleiben. Ziehen am Würfel (> 4 px) dreht die Hauptkamera (`ziehenZuWinkel`), das Ziel bleibt; Rad über dem Würfel zoomt die Ansicht.
- **Kompassring** unter dem Würfel (SVG, flach gezeichnet, damit er immer bedienbar bleibt): N, O, S, W. **N steht bei Azimut + Nordwinkel** (`nordAufRing`; Nordwinkel aus
  `Projektinformation.nordwinkel` = Winkel von Projekt-Nord +y zu geografisch Nord im Uhrzeigersinn, ohne Angabe 0; `data-nord` am Ring zur Prüfung). Ziehen am Ring dreht um die Hochachse (`ringWinkel`,
  `ringZug`: Winkeländerung der Hand = Änderung des Azimuts).
- **Haus-Knopf** (links oben am Würfel) fährt zur Startansicht und zeigt dabei alles (`einpassWerte` in `ansicht3d.js`); Kontextmenü am Würfel (Rechtsklick): «Zur Startansicht», «Aktuelle Ansicht als
  Startansicht festlegen», «Startansicht zurücksetzen (Isometrie)». Vorgabe der Startansicht: Isometrie von VORNE-RECHTS-OBEN; «Alles zeigen» (Zoom alles, Doppelklick Mitte, erstes Laden) schaut aus
  derselben Richtung.
- **Startansicht speichern (seit N13 im Modell):** Ansichtseigenschaft `startansicht` der 3D-Ansicht `{ azimutGrad, polarGrad, position, ziel, projektion }` (Blickrichtung relativ zur
  Gebäudeausrichtung, damit sie einer Drehung des Gebäudes folgt; Position und Ziel der Kamera in Modell-mm zur Information; `projektion` immer `perspektive`). Sie wandert mit dem Projekt
  und gilt auf allen Rechnern. Der Haus-Knopf benutzt die Richtung und passt alles ein; Position und Ziel hält der Kern fest, die Oberfläche nutzt sie nicht. Früher lag sie im Browser
  (`localStorage`, `hlks.startansicht3d.<Projektort>.<Kennzeichen>`): `ansicht3d.js › startUebernehmen` übernimmt einen solchen Wert beim Öffnen **einmal** ins Modell, wenn dort noch keiner
  steht, und entfernt ihn dann (steht im Modell schon einer, wird der Browser-Wert nur entfernt; Regel `startansichtUebernahme` in `schnittbereich-logik.js`).
- **Ausrichtung von VORNE:** eine Funktion, `gebaeudeAusrichtungGrad(gebaeude, gerechnet)` in `wuerfel-logik.js`: θ = −`Gebaeude.planwinkel` (N9: Drehung des geraden Grundrisses im
  Gegenuhrzeigersinn, −45 … 45°). VORNE schaut damit dorthin, wo im geraden Grundriss «unten» ist – auf die lange Gebäudekante, wenn sie dort waagrecht liegt (geprüft in
  `ausrichtung.test.mjs`). Steht noch kein Winkel am Gebäude (Projekt von vor N9), holt `ansicht3d.js` den vom Kern gerechneten (Abfrage `planwinkel`, `bestimmt.winkel`);
  sonst 0 = Projektachsen (VORNE = Blick nach Norden).
- **Prüfen:** Node-Tests (Kamerarichtung je Fläche, Kante, Ecke, mit und ohne Ausrichtung, Nordwinkel, Ring, Startansicht) in `maus.test.mjs`; Browser-Reihe `maus-wuerfel.browser.mjs` (alle 26 Bereiche
  angeklickt, OBEN = Draufsicht, Ecke = Isometrie, VORNE, Haus-Knopf, Ziehen am Würfel und am Ring, Kontextmenü, Bilder hell und dunkel).

## KI-Palette: Schnittstelle (3c, Teil K)

Server und Kern der Palette «Claude» (Spezifikation `entwurf/modul-3c.md`, Teil K). Die Palette (Teil O) baut **nur** auf diesen Endpunkten und
Ereignissen auf; die Logik (Kontext auflösen, Auftragstext, Werkzeuge, Zurücknehmen, Normensuche) steckt im Server (`GespraechDienst.cs`,
`GespraechEndpunkte.cs`, `KiLaeufe.cs`) bzw. im Kern (`Projekt.PaketZuruecknehmen`, `Normen/Normenverzeichnis.cs`). Texte deutsch, Zeiten ISO 8601.
Fehler immer `{ fehler, vorschlag }` mit 400 (Eingabe), 404 (unbekannt), 409 (kein Projekt offen · Lauf belegt · schon beendet · spätere
Änderungen), 503 (Ablage nicht erreichbar).

**Endpunkte**
- `GET /api/ki/einstellungen` → `{ modelle: [{ id: "sonnet", name: "Sonnet" }, { id: "opus", … }, { id: "haiku", … }], vorgabeModell: "sonnet",
  internetVorgabe: false, internetHinweis: "Internet: ohne Modellzugriff; Unterlagen und Familien lesbar", internetWerkzeuge: ["WebSearch", …], verfuegbar, grund?, zeitlimitMinuten: 10,
  grenzen: { textZeichen: 8000, auswahl: 200, bildBytes: 4194304, titelZeichen: 200 } }`. `internetHinweis` gehört an den Schalter «Internet»
  (Entscheid Hauptlauf 20.10.2026, siehe unten). `verfuegbar: false` mit `grund` (z. B. «Claude Code wurde nicht gefunden …»), wenn claude.exe oder der MCP-Server fehlt; ein Abo- bzw.
  Anmeldeproblem zeigt sich erst bei der Frage (`fehlerart: "anmeldung"`).
- `GET /api/ki/gespraeche` → `{ gespraeche: [{ id, titel, erstellt, geaendert, fragen, laeuft, lesbar }] }`, neueste (`geaendert`) zuerst; 409 ohne Projekt.
  Eine beschädigte Gesprächsdatei erscheint mit `lesbar: false`, `datei` (voller Pfad) und `fehler` (Sicherheitsprüfung 3c, B10); die Palette zeigt sie
  grau als «nicht lesbar – Datei …» und wählt sie nicht.
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
  `internetHinweis: "Internet: ohne Modellzugriff; Unterlagen und Familien lesbar"` steht in beiden Nachrichten einer Frage mit Internet (zum Anzeigen bei
  der Frage). `warnung` (Claude): eine Anweisungsdatei (CLAUDE.md) in der Kette des Arbeitsordners (Sicherheitsprüfung 3c, B13).
- **Aenderung**: `{ paket, befehl, beschreibung, zeit, elemente: [kz…], ids: [id…], zurueckgenommen, zurueckgenommenPaket?, zurueckgenommenZeit?,
  zurueckgenommenVon? }` – je Paket des Akteurs KI, in der Reihenfolge der Pakete. `beschreibung` z. B. «element_aendern: WT-E.02 geändert»,
  «leitung_ziehen: R-12, R-13 angelegt; 2 Verbindungen gesetzt». `elemente` (Kennzeichen) und `ids` stehen in derselben Reihenfolge; zum Wählen
  im Modell die Id nehmen (gelöschte Elemente gibt es nicht mehr). `zurueckgenommen` gilt auch, wenn das Paket anderswo zurückgenommen wurde.
- `POST /api/ki/gespraeche/{id}/frage` `{ text, kontext: { ansicht?, geschoss?, auswahl?: [kz…], bild?: "data:image/png;base64,…" }, modell?, internet? }`
  → **202** `{ gespraech, frage, laeuft: true, nachricht: <Mensch> }`. Kennzeichen oder Id gehen; ohne `geschoss` gilt das Geschoss der Ansicht.
  Grenzen (sonst 400): Text 1–8000 Zeichen, `auswahl` ≤ 200 Texte, `ansicht`, `geschoss` und jeder Eintrag der `auswahl` ≤ 100 Zeichen im Muster
  Kennzeichen/Id (ohne Steuerzeichen, Zeilenumbrüche, Anführungszeichen, Klammern; Sicherheitsprüfung 3c, B9 – Unbekanntes geht nicht wörtlich in
  den Auftrag, nur «gibt es im Modell nicht» bzw. die Anzahl), Bild PNG/JPEG ≤ 4 MB als data-URL, `modell` aus der Liste, `internet` bool
  (Vorgabe aus; mit `true` arbeitet Claude **ohne Modellzugriff und ohne Projektkontext**, siehe unten – Projektfragen ohne Internet stellen;
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
IFC-Ausgaben, `unterlage_aus_url`); jedes Werkzeug ist ausdrücklich eingeordnet (`KiEinordnung`, Test), und der MCP-Server registriert mit
`--werkzeuge` nur die erlaubten (Sicherheitsprüfung 3c, B3c/B5). **Mit Internet: ohne Modellzugriff; Unterlagen und Familien lesbar** (Entscheid
Hauptlauf 20.10.2026, dieselbe enge Linie wie die Aufträge; lesbar sind nur Herstellerunterlagen mit zweck «familie» und aus dem Internet
abgerufene, `--nur-familienunterlagen`, B3b):
erlaubt sind nur WebSearch, `unterlage_aus_url`, `normen_suchen`, `norm_lesen`, `kontext_bild` und die Familien- und Unterlagenwerkzeuge der
Aufträge (`GespraechDienst.InternetWerkzeuge`); alle anderen Werkzeuge – Projektabfragen und ändernde Projektbefehle – stehen in
`--disallowedTools`. Claude weiss das (Systemteil) und verweist Projektfragen auf eine Frage ohne Internet. Grund: eingeschleuste Webinhalte sollen
keine Projektdaten über Suchanfragen oder Abrufe nach aussen tragen können. Ebenso geht mit Internet **kein Projektkontext** an Claude (Entscheid
Hauptlauf 20.10.2026): kein Projektname, kein Ordner, keine Ansicht, kein Geschoss, keine Auswahl, kein Bild – der Server verwirft den Kontext der
Anfrage selbst (`kontextVerworfen: true`), das Bild wird nicht abgelegt und `kontext_bild` antwortet «Kein Bild – mit Internet ohne
Projektkontext»; der Systemteil nennt nur «Gespräch in KICAD, ohne Modellzugriff». Rolle und Regeln gehen als `--append-system-prompt` mit (B7),
der Auftrag trägt nur Kontext und Frage. Eine Frage mit Internet läuft zudem in einer **eigenen
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

## Lüftungskette (Modul 6 Teil A, entwurf/modul-6.md)

Noch ohne Oberfläche; erreichbar über die Tür (REST `/api/abfrage/lueftung_varianten`, MCP, KI-Palette). Rechnet nur, das Modell bleibt unverändert.

- `lueftung_varianten { geschoss?, gleichzeitigkeit? (über 0 bis 1, Vorgabe 1), variante?, gewichte? { raumbedarf, geraete_wartung, brandschutzklappen } }` – `{ umfang, eingaben { raeume, mitLuftmenge, ohneLuftmenge,
  zuluftM3h, abluftM3h, gleichzeitigkeit }, werte { geschwindigkeit { quelle, apparateMs, kanal[] { bisM3h, ms }, ausnahmen }, hohlraumZuschlagMm,
  schachtZuschlagMm, laengenZuschlagProzent (je { wert, quelle, hinweis? }), wartungsflaeche, variantenQuelle }, varianten[] { variante, name,
  geraete[] { geraet, gruppe, raum, geschoss, lageAngenommen, zuluftM3h, abluftM3h, steigzone, raeume[] }, kanaele[] { abschnitt, art (anbindung |
  steigzone | verteilung), medium, geschoss, m3h, laengeM, ms, flaecheM2, durchmesserMm }, raumbedarf { zentralen[] { geraet, raum, geschoss,
  lageAngenommen, nettoflaecheMinM2, flaeche }, schaechte[] { geraet, geschoss, raum, lageAngenommen, kanaele, breiteMm, tiefeMm, flaecheM2 },
  hohlraum[] { geraet, geschoss, noetigMm, verfuegbarMm? | verfuegbar (Grund), fehlbetragMm? } }, kennzahlen { geraete, geraeteInNutzungseinheiten,
  schachtflaecheM2, hohlraumMaxMm, brandschutzklappen (null ohne Brandabschnitte), laengeGeschaetztM }, befunde[] }, bewertung { gewichte[] { kriterium,
  name, gewicht, kennzahlen[], herkunft, zaehlt }, rangfolge[] { rang, variante, name, punkte (0–100), kriterien { schluessel: 0–1 } | ausschluss[] },
  empfehlung { variante, name, punkte, entscheidend[], hinweis } | null, kriterienFehlen[], befunde[], verfahren }, befunde[], hinweis }`.
- Bewertung (`src/Hlks.Kern/Bewertung/Bewertung.cs`, gewerkneutral): je Kennzahl über die Varianten normiert (0 = beste), Kriterium = Mittel, Punkte =
  Σ Gewicht × (1 − Kriterium) ÷ Σ Gewicht. Fehlt eine Kennzahl (z. B. Brandschutzklappen ohne Brandabschnitte), zählt das Kriterium nicht. Gewichte aus
  `kataloge/lueftung.json` (Christian «Raumbedarf zuerst»: 50/25/25), je Abfrage mit `gewichte` überschreibbar; die Empfehlung entscheidet nicht.
- Eingaben am Raum: `zuluft`, `abluft` (aus HKAP oder von Hand), `nutzung` (Kandidaten «Technik» für die Zentrale, «Schacht» für die Steigzone; Katalog
  `kataloge/lueftung.json`), `lichteHoehe`, neu `brandabschnitt` und `nutzungseinheit` (oder `teilVon` einer Zone). Geschwindigkeiten nach MuKEn 2014
  Art. 1.19 Abs. 3 und Planungswerte von Christian stehen nur im Katalog.

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
- **Kopf:** Gesprächswahl (Liste «Titel · n Fragen · Zeit», erster Eintrag «＋ Neues Gespräch»; **Neu** und ✕ **Entfernen** mit Rückfrage), Modellwahl (Sonnet als Vorgabe, Opus, Haiku), Schalter **Internet** (aus als Vorgabe) mit dem Hinweis «ohne Modellzugriff; Unterlagen und Familien lesbar» daneben (Sicherheitsprüfung 3c, B3d). «Neu» zeigt eine leere Seite; das Gespräch entsteht auf dem Server erst mit der ersten Frage. Eine beschädigte Gesprächsdatei steht grau und nicht wählbar als «nicht lesbar – Datei …» in der Liste (Tooltip: voller Pfad und Fehler; B10).
- **Verlauf:** Mensch rechts (blau), Claude links; unter der Frage kleine Chips mit dem mitgeschickten Kontext. Antworttext formatiert (Absätze, Überschriften, Listen, Fett/Kursiv, `Code`, Codeblöcke, Zitate, einfache Tabellen, Links `[Text](https://…)` im neuen Fenster mit `rel="noopener noreferrer"`). **Werkzeugaufrufe** kompakt als «n Werkzeugaufrufe» (aufklappbar: ✓/✗, Name, Eingabe, Fehlermeldung; mit Internet auch die Abrufe). **Änderungen** als Liste je Paket: Beschreibung, die betroffenen Elemente als Knöpfe (**Klick wählt das Element im Modell**, gelöschte sind ausgegraut) und **«Zurücknehmen»**; danach «zurückgenommen» und die Beschreibung durchgestrichen. Wird abgelehnt (409), steht darunter die Ablehnung in Klartext («Danach wurde dasselbe noch einmal geändert … Es wurde nichts verändert») und die späteren Pakete (wer, was, woran, wann); die Palette zeigt weder Codes noch JSON. Während der Antwort gestreamt mit Schreibmarke, danach Dauer und Modell.
- **Eingabe:** mehrzeilig, **Strg+Enter** sendet (Enter = neue Zeile), «Senden» wird zu **«Abbrechen»**, solange Claude antwortet; Zähler ab 75 % der 8000 Zeichen. Meldungen unter dem Verlauf: Lauf belegt (409 «Claude arbeitet gerade …»), Fehler, zu lang; die Frage bleibt im Feld. Ein Fehler der Antwort steht in der Nachricht (Anmeldung: «unter Heute mit Claude anmelden», Zeitlimit, Start, MCP); `verfuegbar: false` zeigt ein rotes Band mit dem Grund und sperrt «Senden» (Normensuche geht weiter).
- **Kontext-Chips** (A2): **Ansicht**, **Geschoss** und **Auswahl** (n Elemente) gehen automatisch mit, je Frage per Klick abwählbar (durchgestrichen); **«Bild der Ansicht»** ist aus (Vorgabe) und wird auf Wunsch aus der aktiven Ansicht erzeugt. Nach dem Senden gelten wieder die Vorgaben. Gibt es keine Ansicht, kein Geschoss, keine Auswahl oder lässt sich die Ansicht nicht als Bild senden (Liste, Planblatt), ist der Chip gesperrt. Hinweis: Geht die Ansicht mit, ergänzt der Server ihr Geschoss selbst; um das Geschoss nicht zu schicken, muss auch die Ansicht abgewählt sein.
- **Internet (Entscheid Hauptlauf 20.10.2026):** mit Internet arbeitet Claude **ohne Modellzugriff und ohne Projektkontext**; Herstellerunterlagen (zweck «familie») und Familien bleiben lesbar. Die Palette zeigt es dreifach: Schalter mit «ohne Modellzugriff; Unterlagen und Familien lesbar» und Hinweisband; alle Kontext-Chips **ausgegraut** (gesperrt, durchgestrichen, Tooltip erklärt es) – es geht nichts mit (die Palette schickt dann einen leeren Kontext, auch kein Bild; der Server verwirft ihn ohnehin); Nachrichten mit Internet tragen die Marke **«Internet · ohne Modellzugriff; Unterlagen und Familien lesbar»** (Frage und Antwort, orange Rahmen), die Frage zusätzlich «ohne Projektkontext». Frühere Fragen ohne Internet bleiben im selben Gespräch; Claude erinnert sich bei einer Frage mit Internet nicht daran (eigene Sitzung).
- **Normen:** Reiter «Normen» mit Suchfeld und Reihenfilter (SIA, SWKI, VKF, Vorlage, suissetec, Sprinkler, Merkblatt); Treffer mit Norm, Ausgabe, **Seite, Ziffer** und Auszug (`GET /api/normen/suche`); «In die Frage übernehmen» setzt «Zu SIA …, Seite …, Ziffer …: » ins Eingabefeld. Meldet «Keine Treffer.» bzw. den Hinweis des Servers (kein Normenverzeichnis, Volltext noch im Aufbau).

**Reservierungen (Entscheid Hauptlauf 20.10.2026).** Wer ein Element in der Oberfläche ändert, hält es reserviert (Kern: je Element, Akteur und Gerät); Claude (Akteur KI) bekäme sonst «Elemente werden gerade von jemand anderem bearbeitet». Darum gibt der Server (`GespraechDienst.Fragen`, `Projekt.FreigebenFuer`) beim Start einer Frage **ohne Internet** die Reservierungen frei, die der Mensch **auf diesem Gerät** für die Elemente der mitgeschickten Auswahl, deren Kinder und – bei Teilen eines Strangs – den Strang samt seinen Teilen hält. Reservierungen anderer Arbeitsplätze bleiben; Claude bekommt dort wie bisher einen lesbaren Fehler (✗ in der Werkzeugliste). Ohne Auswahl in der Frage (Chip abgewählt) und mit Internet wird nichts freigegeben. Die Nachricht trägt `auswahlFreigegeben: [Kennzeichen]`; die Palette zeigt an der Frage den Hinweis **«Auswahl für Claude freigegeben»**. Die Oberfläche reserviert beim nächsten eigenen Ändern von selbst neu. Freigegeben wird erst unmittelbar vor dem Start; scheitert danach noch etwas (z. B. Speichern), holt der Server die Reservierungen zurück (Sicherheitsprüfung 3c, B14). Nach dem Lauf gibt er die Reservierungen der KI nur frei, solange die Sperrdatei `<Projekt>\ki\lauf.lock` noch diesem Lauf gehört (B8: ein KI-Lauf je Projekt auch über mehrere KICAD-Prozesse).

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
(Kanalisation) – nach den Bernstrasse-Plänen; Elektro hat keinen Fachbuchstaben (kein Vorschlag). Koordination trägt K (wie die echten Pläne); Kälte ist Teil der Heizung und trägt H (Entscheid Christian 20.10.2026, Abschnitt «Kälte als Teil der Heizung»).
Listen im PDF: Arial 2.2 mm (am Ansichtsfenster mit `schrift` einstellbar), Spalten ≤ 60 mm (längere Texte mit «…»); der Umbruch in Blöcken rechts daneben und der Befund P28 stehen im Abschnitt «Listenumbruch und Koordinationsplan-Knöpfe (Modul 7 Teil B3)» (ohne Blatt oder mit `umbruch` = `aus`: höchstens 45 Zeilen und 380 mm Breite je Liste).

**Prüfen:** `node --test js/oberflaeche/planblatt-hk.test.mjs`; Kern: `PlanblattHkTests.cs`; Browser: `HLKS_PH_PORT=5179 HLKS_CDP_PORT=9500 node js/oberflaeche/planblatt-hk.browser.mjs` (Bilder `%TEMP%\HLKS_CAD\ph_*.png`; `HLKS_KATALOG` auf die Kataloge
der Arbeitskopie setzen).

## Plandarstellung wie H+K (Modul 7 Teil B1)

Entwurf: `entwurf/modul-7.md` Teil B1, Muster `entwurf/planmuster.md` («Gemeinsam», «Koordination»). Die Oberfläche setzt nur Ansichtseigenschaften (`element_aendern`, Eigenschaftenpalette der Ansicht, aus dem Katalog); gerechnet und gezeichnet wird im Kern. Eigene Knöpfe im Menüband gibt es seit Teil B3 (Register Koordination, siehe unten).

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

**Menüband-Knöpfe:** «Koordinationsplan» und «Aussparungsboxen» stehen seit Teil B3 im Register **Koordination** (Gruppe «Koordinationsplan»); im Register Ansicht › Darstellung brachten zwei zusätzliche Knöpfe die Stufenprüfung `menueband.browser.mjs` («Ansicht bei 1920 px: Stufen nehmen nie zu») und `serienpdf.browser.mjs` (Knopf «Serien-PDF …») zum Fehlschlagen und blieben deshalb weg (siehe nächster Abschnitt).

**Prüfen:** `node --test js/oberflaeche/plandarstellung-b1.test.mjs`; Kern: `PlanDarstellungB1Tests.cs` (Abnahmebild mit `HLKS_M7B_BILD=%TEMP%\HLKS_CAD`, dann `werkzeuge\pdf_bild.ps1`).

## Listenumbruch und Koordinationsplan-Knöpfe (Modul 7 Teil B3)

Entwurf: `entwurf/modul-7.md` Teil B3 (und Teil A Punkt 7 «Listen auf Planblättern»). Gerechnet und gezeichnet wird im Kern; die Oberfläche schickt nur Befehle (`element_anlegen`, `element_aendern`).

**Kern:** `Plan/Listentabelle.cs` (`Anordnen`: Blöcke, wiederholte Kopfzeile, Summenzeilen nur am Ende, Markierung «… n weitere Zeilen»; Optionen `umbruch` und `schrift` des Ansichtsfensters; `Platzierung`) · `Plan/Planausgabe.cs` (`ListenBereich`: der freie
Bereich des Blatts, derselbe, in dessen Mitte die Platzsuche eine neue Ansicht zuerst versucht) · `Plan/PlanPruefung.cs` (Befund **P28**) · `Plan/PlanBefehle.cs` (`ansicht_platzieren` mit den Parametern `umbruch` und `schrift`, auch als MCP-Werkzeug).

**Orte in der Oberfläche**

| Funktion | Ort | Kern |
| --- | --- | --- |
| Koordinationsplan | Register **Koordination › Koordinationsplan › Koordinationsplan**: legt für das Geschoss der aktiven Ansicht einen Grundriss `AN-KO-<Geschoss>` («Koordination <Geschoss>») mit `darstellung = koordination` und `aussparungsboxen = true` an und öffnet ihn; gibt es ihn schon, öffnet der Knopf den vorhandenen. Massstab wie der aktive Grundriss (sonst 1:50), alle Gewerke sichtbar. Gesperrt ohne aktive Ansicht mit Geschoss (Hinweis im Tooltip) | `element_anlegen` (Ansicht); Grafik `KoordinationGrafik`; Bausteine `koordinationsplan.js` |
| Schalter Aussparungsboxen | Register **Koordination › Koordinationsplan › Aussparungsboxen**: schaltet `aussparungsboxen` der aktiven Grundrissansicht (ein: `true`, aus: Eigenschaft entfernt). Gedrückt, wo der Kern die Boxen zeichnet; gesperrt bei 3D, Liste, Schema, Vorlage, im Aussparungsplan (keine Boxen) und im Koordinationsplan (immer ein) – der Tooltip nennt den Grund | `element_aendern` (`aussparungsboxen`), Grafik `AussparungGrafik` |
| Umbruch der Liste | Eigenschaftenpalette des **Ansichtsfensters** (im Projektbrowser unter dem Planblatt anklicken) › `umbruch` = `spalten` (Vorgabe) oder `aus`; beim Platzieren über den Parameter `umbruch` von `ansicht_platzieren` | `element_aendern` (`umbruch`), `ansicht_platzieren` |
| Schrift der Liste | Eigenschaftenpalette des Ansichtsfensters › `schrift` (mm, 1.5 bis 4, Vorgabe 2.2); der Befund P28 nennt, wenn nötig, die Schrift, mit der die Liste passt | `element_aendern` (`schrift`) |
| Befund «Liste passt nicht aufs Blatt» | Verwalten › Modell › Prüfbefunde, **P28** (Warnung am Ansichtsfenster, Text und Vorschlag des Kerns) | `Pruefungen` (`PlanPruefung`) |

**Umbruch (Annahmen):** Bereich der Liste = ganze Breite im Zeichenrahmen und Höhe vom Rahmen oben bis über den Plankopf und den Ansichtstitel (Ansichtstitel 16 mm, wie die Platzsuche). Passt die Liste nicht in die Höhe, geht sie in Blöcken rechts daneben weiter; ein Block braucht mindestens
Kopfzeile und eine Zeile; die letzte Zeile steht stets zusammen mit den Summenzeilen; mehrere Tabellen (Mengen, Sanitär-Zusammenstellung) laufen durch die Blöcke, ihr Titel wiederholt sich mit «(Fortsetzung)». Reicht die Blattbreite nicht, bleiben nur die Blöcke, die noch aufs Blatt passen; eine Zeile
«… n weitere Zeilen (Liste passt nicht aufs Blatt)» steht unter dem letzten Block, und P28 meldet es. Der Vorschlag von P28: bei `umbruch = aus` zuerst den Umbruch einschalten, sonst die grösste kleinere Schrift aus 2.0 / 1.8 / 1.6 / 1.5 mm, mit der die Liste passt (die Untergrenze 1.5 mm ist eine Annahme), sonst ein
Folgeblatt (zweite Listenansicht, z. B. für ein anderes Geschoss) oder ein grösseres Format. Die Lage des Fensters (Mitte) folgt dem Umbruch nicht von selbst: wächst eine schon platzierte Liste um einen Block, bleibt ihre Mitte, wo sie war (Lage mit `element_aendern` anpassen).

**Prüfen:** `node --test js/oberflaeche/koordinationsplan.test.mjs`; Kern: `PlanListeUmbruchTests.cs` (Filter `PlanListeUmbruch`); Browser: `HLKS_B3_PORT=5240 HLKS_CDP_PORT=9580 node js/oberflaeche/koordinationsplan.browser.mjs` (Ports 5240–5249; Bilder `%TEMP%\HLKS_CAD\kp_*.png`; `HLKS_KATALOG` auf die Kataloge der Arbeitskopie setzen).

## IFC exportieren (Modul 7 Teil B2, Dialog)

Entwurf: `entwurf/modul-7.md` Teil B2 (Punkt 4 Exportbericht) und die Entscheide A1–A4 (Räume nur auf Wunsch, Dämmung als IfcCovering, Ablage `<Projektordner>\ausgabe\ifc`, Pset `HK_KICAD`). Gerechnet, geschrieben und geprüft wird im Kern (`src/Hlks.Kern/IfcExport/`, Abfrage `ifc_export`); die Oberfläche schickt die Auswahl und zeigt den Bericht. Keine Fachlogik im Browser.

**Ort:** Register **Datei › Gruppe Ausgabe › IFC exportieren** (Aktion `datei.ifc-export`, kein Kürzel, Symbol `ifcExport` in beiden Sätzen; aktiv nur mit offenem Projekt). Die Gruppenfolge im Register Datei ist `Projekt`, `Ausgabe`, `Zuletzt geöffnet` (`GRUPPENFOLGE.datei` in `menueband.js`). Es gibt kein neues Register.

**Dialog «IFC exportieren»** (`ifc-export-dialog.js`, verschiebbar wie alle Dialoge):

| Feld | Verhalten | Anfrage an `POST /api/ifc/export` |
| --- | --- | --- |
| Fassung | IFC4 (Vorgabe) oder IFC2X3 | `fassung` |
| Umfang | Gewerke als Häkchen aus dem Katalog (`gewerke.json`), Geschosse als Häkchen aus dem Modell, je «alle» als Vorgabe (die einzelnen Häkchen sind dann gesperrt; ein Klick darauf nimmt «alle» weg); ohne Häkchen und ohne «alle» meldet der Dialog es und schickt nichts | `umfang.gewerke`, `umfang.geschosse` (bei «alle» weggelassen) |
| Räume mitgeben | Vorgabe aus | `raeume` |
| IDS-Prüfung | die IDS-Basis von H+K wird immer geprüft; weitere IDS-Dateien als **vollständiger Pfad, eine Datei je Zeile** (ein Browser kennt den Pfad einer gewählten Datei nicht; ein Hochladen über «Unterlagen» gibt es dafür nicht – der Server läuft auf demselben Rechner) | `ids` (Liste der Pfade) |
| Ziel | nur Hinweis: `<Projektordner>\ausgabe\ifc` (nicht wählbar; Dateiname `<Projekt>_<Fassung>_<JJJJMMTT-hhmm>.ifc`) | – (der Kern bestimmt es, `datei` wird nie geschickt) |

**Ergebnis im selben Dialog** (`ifc-export-text.js`, alles Dynamische mit `esc()` maskiert; ein Test prüft, dass Schadtext aus dem Bericht nie als Tag oder Attribut erscheint): Datei (Name, Grösse, Fassung, Entitäten) mit Download-Link und Links zu den Berichten (`GET /api/ifc/export/datei?pfad=<Dateiname>`, nur Dateien aus `ausgabe\ifc` des offenen Projekts), drei Ergebnismarken (Kontrolle beim Zurücklesen, Schemaprüfung, IDS), Dauer mit Aufschlüsselung, Zähler (Übersicht, Tabelle je Klasse mit Entität, Typobjekt, PredefinedType, eingeklappt je Entität), übersprungene Elemente mit Grund, Schemaprüfung und Kontrolle, **IDS-Ergebnis je Datei und Regel** (bestanden, nicht bestanden, nicht anwendbar (Fassung); nicht bestandene Regeln sind aufgeklappt) mit den **Kennzeichen der betroffenen Elemente als Knöpfe** – ein Klick wählt das Element im Modell (`auswaehlen`; «Element.Anschluss» und «Dämmung Element» führen zum Element selbst, gibt es es nicht mehr, kommt eine Meldung), Warnungen und Hinweise. «Ordner öffnen» schickt den Ordner an `POST /api/ordner/zeigen` (wie beim Serien-PDF; im Testlauf öffnet der Server keinen Explorer), «Zurück zur Auswahl» führt zum Formular.

**Lauf und Fehler:** Während des Exports zeigt der Dialog «IFC wird geschrieben und geprüft …» mit der Zeit; er ist gesperrt, und ein Flag im Modul (`exportLaeuft`) verhindert einen zweiten Export, auch bei Doppelklick oder bei geschlossenem und neu geöffnetem Dialog. Wird der Dialog während des Laufs geschlossen, meldet am Ende eine Einblendung das Ergebnis. Fehler des Servers stehen im Dialog mit Vorschlag: **409** «Kein Projekt offen» (Vorschlag: zuerst ein Projekt öffnen), **400** «Der Export ist so nicht möglich» (z. B. eine IDS-Datei, die es nicht gibt; Vorschlag des Kerns); sonst «Nicht exportiert» mit der Meldung.

**Anleitung:** Kapitel «m7» in `berichte/anleitung/inventar.json`: `m7-ifc-export` (Hauptfunktion des Knopfs, Feld `hauptfunktion`), `m7-ifc-umfang`, `m7-ifc-ids`.

**Prüfen:** `node --test js/oberflaeche/ifc-export.test.mjs` (und `symbole-revit.test.mjs`, `menueband.test.mjs`); Browser: `HLKS_IFC_PORT=5301 HLKS_CDP_PORT=9651 node js/oberflaeche/ifc-export.browser.mjs` (zweiter Lauf mit `HLKS_SYMBOLE=revit`; Ports 5300–5309; Bilder `%TEMP%\HLKS_CAD\ie_*.png`; `HLKS_KATALOG` auf die Kataloge der Arbeitskopie setzen; der Test legt eine fiktive IDS-Datei unter `%TEMP%` an und räumt auf).

## Schutz gegen Fremdaufrufe (Sicherheitsprüfung 3c, B1, 20.10.2026)

Der Server bindet nur `localhost`, aber der Browser des Benutzers ist localhost: ohne Prüfung könnte jede fremde Webseite «einfache»
Anfragen (`text/plain`, ohne CORS) an `/api` schicken – z. B. eine KI-Frage starten – oder per DNS-Rebinding Antworten lesen.
`Herkunftsschutz.cs` steht als Middleware vor allen Endpunkten unter `/api` und `/hilfe` (statische Oberfläche unberührt):

- **Host** muss `localhost`, `127.0.0.1` oder `[::1]` sein (beliebiger Port), sonst 403 – auch beim Lesen.
- **Ändernde Methoden** (POST, PUT, PATCH, DELETE): `Sec-Fetch-Site`, falls vorhanden, nur `same-origin`/`none`; `Origin`, falls vorhanden,
  nur `http://localhost|127.0.0.1|[::1]:<Port dieses Servers>` – sonst 403 `{ fehler: "Anfrage von fremder Seite abgelehnt", vorschlag }`.
  Werkzeuge (PowerShell, Node, MCP, Tests) senden beides nicht und bleiben erlaubt.
- **Körper** nur als `application/json`; Ausnahmen: `POST /api/unterlagen` (`application/octet-stream`, `multipart/form-data`) und
  `POST /api/projekt/vorschau` (`image/png`). Sonst 415. Ohne Körper (z. B. `…/abbrechen`) ist kein Inhaltstyp nötig.
- Keine CORS-Kopfzeilen (kein `Access-Control-Allow-Origin`).

Eigene Aufrufe senden immer `Content-Type: application/json` (`api.js`, `ki-palette.js` `anfrage()`, Anleitung `vorlage.html`, alle
`*.browser.mjs`, `anleitung_bilder.mjs`, `werkzeuge/*.ps1`); Uploads `application/octet-stream` (`unterlagen-dialog.js`,
`aktionen-einfuegen.js`), Vorschau `image/png` (`vorschau.js`). Neue Endpunkte mit anderem Inhaltstyp gehören in `Herkunftsschutz.Uploads`.
Internet-Schalter der KI-Palette: seit dem Abnahmelauf B6 (20.10.2026) frei (`GespraechEinstellungen.InternetErlaubt`, Vorgabe an);
Notschalter `HLKS_KI_INTERNET=0` sperrt ihn wieder (Schalter ausgegraut, Grund im Tooltip; `POST …/frage` mit `internet: true` → 409).

**Prüfen:** `dotnet test tests/Hlks.Web.Tests --filter FullyQualifiedName~GespraechSicherheit`.

## Kälte als Teil der Heizung

Entscheid Christian Steiner, 20.10.2026: «Kälte ist Teil der Heizung mit anderem Medium.» (Spezifikation `entwurf/gewerk-kaelte.md`.) Kälte ist kein eigenes Gewerk;
Kältesysteme unterscheiden sich von der Heizung nur über Medium und Systemtyp. Die Oberfläche hat dafür keine eigene Logik – sie zeigt, was der Kern liefert.

- **Medien (Katalog `medien.json`, Farben und Strichart kommen vom Kern):** `HZ-VL`, `HZ-RL`, `HZ-VL-Glyk`, `HZ-RL-Glyk` und `KAE-VL`, `KAE-RL`, `KAE-VL-Glyk`, `KAE-RL-Glyk`, alle Gewerk
  Heizung. Farben aus HKAP `schema_systeme.json` (HZ_VL rot #FF0000, HZ_RL blau #0000FF, KAE_VL cyan #00CCCC, KAE_RL magenta #FF00CC); Glykol hat die Farbe der Grundleitung (Annahme), die
  Unterscheidung läuft über Kennung und Beschriftung. Vorlauf voll, Rücklauf gestrichelt. Je Medium ein Systemtyp in `systemtypen.json` («Kälte Vorlauf», «Heizung Rücklauf Glykol» …).
- **Projektbrowser:** die Gliederung «Heizung» zeigt auch die Kältesysteme, ihre Ansichten und Pläne; es gibt keinen eigenen Block «Kälte» und keine Kennung `kaelte` in der Tabelle
  `GLIEDERUNGEN`. Eine gespeicherte Benutzereinstellung `projektbrowser.gliederung` mit `kaelte` wird als `heizung` gelesen (`js/gewerk-alias.js`, `gliederungenNormieren`); der Server
  prüft bei den Einstellungen weiterhin nur die Form der Kennung.
- **Pläne:** ein Kälteplan trägt den Fachbuchstaben H («1-0768 H03») und liegt im automatischen Plansatz «Heizung»; es gibt keinen Plansatz «Kälte». Der Koordinationsplan zeichnet
  Kälteleitungen in der Koordinationsfarbe der Heizung, Fachpläne in der Medienfarbe (cyan/magenta).
- **Alte Werte werden gelesen, nie abgelehnt:** Gewerk `kaelte` → `heizung`; Medium `KUE-VL`/`KUE-RL` → `KAE-VL`/`KAE-RL`, `SOLE-VL`/`SOLE-RL` → `HZ-VL-Glyk`/`HZ-RL-Glyk`; Systemtyp
  «Kühlwasser …» → «Kälte …», «Sole …» → «Heizung … Glykol»; HKAP-Schlüssel mit Unterstrich (`HZ_VL`, `KAE_RL`) → Kennungen mit Bindestrich. Beim Öffnen eines Projekts steht
  im Status `aliasHinweis` («Kälte gehört zur Heizung – alte Kennungen umgeschrieben: …»); die Oberfläche zeigt ihn einmal als Meldung (`projektGeoeffnet`). Befehle liefern denselben
  Hinweis in `hinweise`. Kennzeichen von Elementen (z. B. ein System «KUE-VL») sind frei gewählte Namen und bleiben unverändert.
- **Apparate-Hinterlegung** (Kälte #CCE0FF, Heizung #CCFFE0, HKAP `FlaecheApparatKaelte` / `FlaecheApparat`): KICAD hinterlegt Apparate heute an keiner Stelle (weder Grundriss noch
  Schema); die Werte sind noch nirgends eingetragen und kommen erst mit dem Prinzipschema (Modul 5).
- Der Familienbibliothek-Filter (`bibliothek-text.js`), die Gewerkliste der Ansichten (`werkzeug.js`) und die Seiten der Abstandsvorgaben (`koordination-text.js`) kennen kein Gewerk Kälte mehr.
- Das Beispielprojekt «MFH Musterweg» hat einen kleinen Kältekreis (Anlage A-KAE, Systeme KAE-VL/KAE-RL, Pumpe, drei Ventile, zwei Rohre) mit dem Grundriss «UG Heizung/Kälte».

**Prüfen:** `dotnet test tests/Hlks.Kern.Tests --filter FullyQualifiedName~KaelteHeizung`, Node-Tests `projektbrowser.test.mjs` («Kälte:»), `bibliothek.test.mjs`.

## Nachbearbeitung N9: Ausrichtung der Grundrisse (`nachbearbeitung.md`, Abschnitt N9)

Christian, 21.10.2026: «bei den ansichten sollte eingestellt werden ob sie nach himmelsrichtung oder nach geometrie ausgerichtet sein sollen. standard ist geometrie
(längste Gebäudekante waagerecht)». Die Drehung rechnet der Kern (`Grafik/Ansichtsausrichtung.cs`, `Architektur/Planwinkel.cs`); die Oberfläche dreht nur.

| Funktion | Ort | Kürzel | Dateien |
| --- | --- | --- | --- |
| **Nach Himmelsrichtung** ein/aus (sonst Geometrie) | Ansicht › Darstellung › «Nach Himmelsrichtung» (gedrückt = Himmelsrichtung); Eigenschaften der Ansicht › `ausrichtung` | – | `aktionen-ansicht.js` (Aktion `ansicht.ausrichtung`) |
| **Planwinkel neu bestimmen** | Ansicht › Darstellung › «Planwinkel neu bestimmen»; Eigenschaften eines Gebäudes › Knopf unter `planwinkel` | – | `aktionen-ansicht.js` (`ansicht.planwinkel`), `ausrichtung-palette.js` |
| Drehung und Quelle anzeigen | Eigenschaften der Ansicht › Zeilen «Drehung», «Quelle» unter `ausrichtung`; unten links in der Ansicht «Geometrie, gedreht −37.0°» | – | `ausrichtung-palette.js`, `ansichten/grundriss.js` |

- **Plangrafik:** `/api/plangrafik/{ansicht}` liefert zusätzlich `drehung` (Grad, Gegenuhrzeigersinn, Modell → Ansicht), `nord` (Richtung von geografisch Nord auf dem Blatt,
  Grad im Uhrzeigersinn ab oben), `ausrichtung` (geometrie | himmelsrichtung; null bei Schnitt, Ansicht, Schema, Liste) und `begrenzungAnsicht` (Begrenzung in den Achsen der
  Ansicht). Die Primitive bleiben in Modellkoordinaten; Beschriftungen hat der Kern schon gegengedreht (waagrecht auf dem Blatt, Masszahlen und Leitungsbezeichnungen folgen
  ihrem Bauteil und bleiben lesbar).
- **Eine Stelle der Drehung am Bildschirm:** `grundriss.js` setzt `transform="scale(1 -1) rotate(drehung)"` an der Gruppe `gr-welt` (Plan und Werkzeugschicht). Der Ausschnitt
  (`viewBox`) liegt in den Achsen der Ansicht; `zuModell`/`zuBildschirm` rechnen über `ansichten/ausrichtung.js` (`svgAusModell`, `modellAusSvg`) ins Modell zurück, `zoomAuf`
  nimmt ein Modellrechteck und passt das umschliessende Rechteck der Ansicht ein. Ändert sich die Drehung (Umschalten), bleibt die Modellmitte des Ausschnitts; ohne eigenen Zoom
  wird neu eingepasst. Der Nordpfeil oben rechts dreht nach `nord`; unten links stehen die Ausrichtung und die **Mausposition in Modellkoordinaten** («x … · y … mm (Modell)»).
- **Werkzeuge:** `instanz.drehung` der Ansicht. `fangen.js › fangen({ …, drehung })` fängt in den Achsen der Ansicht (Umschalt = orthogonal und Raster; Endpunkte, Kanten
  und Abstände unverändert, Ergebnis in Modellkoordinaten, auf 1e-6 mm gerundet). Winkelfang der Leitungen (`richtungsKandidaten({ drehung })`), Bemassung
  horizontal/vertikal (`messrichtung(…, drehung)`, im Kern `GrafikKontext.Drehung`) und die Winkelangabe der temporären Masse gelten in den Achsen der Ansicht –
  waagrecht gezeichnet heisst parallel zur Gebäudekante. Texte, Fangsymbole und Griffe der Werkzeugschicht bleiben waagrecht (`schicht.js › drehungSetzen`, gesetzt in
  `basis.js` und `griffe.js` vor jedem Zeichnen, danach wieder 0 – der Familieneditor zeichnet ungedreht). Auswahl per Klick und Ziehen laufen über das SVG bzw. `zuModell` und brauchen nichts Eigenes.
- **PDF und Planblatt:** `PdfZeichner.Drehen` dreht alle Primitive eines Fensters um die Fenstermitte (eine `cm`-Matrix je Fenster); Ausschnitt und Begrenzung gelten in den
  Achsen der Ansicht; der Nordpfeil unter dem Fenster zeigt nach geografisch Nord. Die Planblatt-Ansicht zeigt das PDF.

**Prüfen:** Kern `dotnet test tests/Hlks.Kern.Tests --filter FullyQualifiedName~AnsichtAusrichtungTests`; Node `ansichten/ausrichtung.test.mjs`; Browser
`node ansichten/ausrichtung.browser.mjs` (Port 5440, CDP 5441; Bilder `n9_*.png` in `%TEMP%\HLKS_CAD\n9`).

## Nachbearbeitung N13 und N14: 3D-Schnittbereich, «Raum im 3D zeigen» (`nachbearbeitung.md`), Rest von N11

Christian, 21.10.2026: «in der 3d Ansicht sollte der 3d-Ansichtsbereich eingestellt werden können (es ist nur sichtbar was eingestellt ist, wie in revit)» und «ist im GR ein Raum
ausgewählt; Kontextmenü "Raum im 3D zeigen" springt zur 3D-Ansicht und zeigt den 3D-Ausschnitt des Raumes, +1.5 m in alle Richtungen».

**Daten (Kern, `kataloge/klassen.json`, `Plan/Schnittbereich.cs`):** zwei Eigenschaften der Klasse Ansicht, gespeichert mit `element_aendern` (ein Paket, zurücknehmbar; der Kern prüft die Form).

| Eigenschaft | Inhalt |
| --- | --- |
| `schnittbereich` | `{ aktiv, min [u, v, z], max [u, v, z], winkel }` in mm. Der Quader ist um die Hochachse (Ursprung des Modells) um `winkel` (Grad, Gegenuhrzeigersinn) gegen die Modellachsen gedreht: `u = x·cos + y·sin`, `v = −x·sin + y·cos`, `z` absolut über ±0.00. Bei Winkel 0 sind es Modellkoordinaten. **Winkel = −`planwinkel`** des Gebäudes (N9), also parallel zur Gebäudekante. `aktiv: false` schaltet aus und behält den Quader. Mindestens 1 mm je Kante. |
| `startansicht` | siehe N11 oben: `{ azimutGrad, polarGrad, position, ziel, projektion }`. |

**Befehl `ansicht_raum_zeigen { raum, ansicht?, rand = 1500 }`** (auch MCP-Werkzeug und KI, eingeordnet unter «Pläne einrichten», zur Bestätigung): setzt `schnittbereich` der Ansicht (aktiv) auf die Räume –
Umriss in den Achsen des Gebäudes plus `rand` in alle Richtungen, Höhe von Raumunterkante − `rand` bis Raumoberkante + `rand`. Raumunterkante = tiefster Punkt des Raum-Polygons, Raumoberkante =
Unterkante + Raumhöhe (`lichteHoehe` › Geometriehöhe › Geschosshöhe wie die Raumliste). `raum` ist ein Kennzeichen oder eine Liste (mehrere Räume = gemeinsamer Bereich in den Achsen des ersten Raums).
Ohne `ansicht`: die erste 3D-Ansicht nach Kennzeichen, sonst wird `AN-3D` angelegt (im selben Paket). Die Kamera stellt die Oberfläche ein; der Befehl ändert nur den Schnittbereich.

| Funktion | Ort | Dateien |
| --- | --- | --- |
| **3D-Schnittbereich** ein/aus | Ansicht › 3D-Schnitt › «3D-Schnittbereich» (gedrückt = ein); Eigenschaften der 3D-Ansicht › Haken «3D-Schnittbereich» | `aktionen-ansicht.js` (`ansicht.schnittbereich`), `schnittbereich-palette.js`, `eigenschaften.js` |
| **An Auswahl anpassen** | Ansicht › 3D-Schnitt; Palette › Knopf (bei gewähltem Element ist die Palette die des Elements: dann das Menüband) | `ansicht.schnittbereichAuswahl` |
| **Auf Geschoss** | Ansicht › 3D-Schnitt; Palette › Knopf; Geschoss der Auswahl, sonst Menü der Geschosse | `ansicht.schnittbereichGeschoss` |
| **Zurücksetzen** | Ansicht › 3D-Schnitt; Palette › Knopf | `ansicht.schnittbereichZurueck` |
| **Raum im 3D zeigen** | Grundriss › Rechtsklick auf einen Raum (oder ins Leere bei gewähltem Raum); Ansicht › 3D-Schnitt bei gewähltem Raum | `grundriss.js` (Kontextmenü), `ansicht.raum3d` |
| **Rand für «Raum im 3D zeigen» …** | Grundriss › Kontextmenü; Aktion `ansicht.raum3dRand` | Benutzereinstellung `ansicht3d.raumrand` (mm, 0 … 100000, Vorgabe 1500; `BenutzerEinstellungen.cs`) |

- **Clipping (`ansichten/schnittbereich3d.js`):** `renderer.localClippingEnabled`; die sechs Ebenen (`ebenen`, Normalen ins Innere) hängen als `material.clippingPlanes` an **allen** Materialien von `modell` (Körper, Kanten,
  IFC-Netze der Unterlagen) und der Hülle; Quader und Griffe sind nicht geclippt. `ansicht3d.js` meldet neue Objekte mit `schnitt.neueObjekte()` (Körper gebaut, Netz angehängt, Hülle gebaut), die Ebenen werden vor dem
  nächsten Zeichnen gesetzt. Die Ebenenobjekte sind dieselben, wenn der Quader sich ändert (kein Neuaufbau der Materialien).
- **Schnittflächen bleiben offen (Entscheid):** ein Deckel (Stencil-Kappe) kostet je Ebene zwei weitere Durchläufe über das ganze Modell, und die Wände sind durchscheinend (Deckkraft ~0.3) – ein
  undurchsichtiger Deckel würde sie verfälschen. Die Schnitte zeigen deshalb die Hohlform der Bauteile.
- **Griffe:** Kegel in der Mitte jeder Fläche, Spitze nach aussen, blau wie der Schnittbereich in Revit, auf dem Bildschirm gleich gross und immer sichtbar (`depthTest` aus). Ziehen (linke Taste auf einem Griff; Aufnahmephase vor den
  OrbitControls) verschiebt die Fläche entlang ihrer Achse: der Strahl der Maus wird mit der Achse geschnitten (`achsenParameter`, nächster Punkt), die Verschiebung ist der Unterschied zum
  Beginn (`flaecheSchieben`, Mindestkante 100 mm). Gespeichert wird nach einer Pause von 600 ms und beim Loslassen (ein Zug ergibt meist **ein** Paket); während des Ziehens und Speicherns gilt der angezeigte
  Stand (Echo des Servers wird ignoriert). Lehnt der Kern ab, gilt wieder der Stand des Modells. Bricht der Browser den Zeiger ab (`pointercancel`), wird der Zug verworfen.
- **«An Auswahl anpassen»:** alle Dreieckspunkte der Körper der Auswahl, in den Achsen des Quaders (`quaderSammler`) plus 300 mm. **«Auf Geschoss»:** nur die Höhe wechselt (OK/UK aus `Geschoss.kote` und `hoehe`), der
  Grundriss des Quaders bleibt (ohne Quader: ganzes Modell). **«Zurücksetzen»:** ganzes Modell in den Achsen der Gebäudeausrichtung; der Schalter bleibt, wie er ist. Beim ersten Einschalten gilt ebenfalls das ganze Modell.
- **Auswahl nur im Sichtbaren:** `koerperTreffer` und `huelleKlick` in `ansicht3d.js` lassen Treffer aus, deren Punkt ausserhalb des Quaders liegt (`schnitt.filtern`; der Raycaster kennt das Clipping nicht).
- **Kamera:** «Alles zeigen», Haus-Knopf und erstes Laden passen den aktiven Quader ein (umschliessende Kugel der acht Ecken) statt des Modells. «Raum im 3D zeigen» fährt nach dem Befehl `zeigeBereich()` an:
  Isometrie von vorne rechts oben (`zielWinkel(START_VORGABE, θ)`, unabhängig von der Startansicht), danach bleibt die Kamera stehen (`benutzt`, `hatGezoomt`).
- **Raum im 3D zeigen (Ablauf der Aktion `ansicht.raum3d`):** Räume aus `p.raeume` oder der Auswahl → `ansicht_raum_zeigen` mit der **zuletzt benutzten** 3D-Ansicht (Kennzeichen aus dem Ereignis `ansicht`;
  sonst eine offene, sonst die erste; fehlt jede, legt der Befehl sie an) und dem Rand aus `ansicht3d.raumrand` → wartet, bis die Eigenschaft in der Ansicht angekommen ist → öffnet bzw. holt die Ansicht
  nach vorn → `zeigeBereich()`. Der Rechtsklick wählt den Raum, wenn er noch nicht gewählt ist; mit aktivem Werkzeug hat er keine Bedeutung. Es gibt keine Mehrfachauswahl in der Oberfläche: mehrere Räume
  kommen über `p.raeume` bzw. den Befehl (KI).
- **Menüband:** neue Gruppe «3D-Schnitt» im Register Ansicht (nach «Darstellung», `menueband.js › GRUPPENFOLGE`), fünf Knöpfe; Symbole `schnittbereich*` und `raum3d` in `symbole.js` (im Revit-Satz fällt
  jedes Symbol ohne eigene Fassung auf den Strich zurück).
- **Prüfhilfen am DOM:** `.d3-wurzel[data-schnittbereich]` (JSON des angezeigten Quaders oder `null`), `[data-clipping]` (`{ materialien, geclippt }`), `[data-griffe]` (Bildschirmlage der Griffe in px der
  Leinwand), `[data-griff]` (während des Ziehens der Name des Griffs); Instanz `trefferBei(clientX, clientY)` (was dort gewählt würde).
- **Prüfen:** Kern `dotnet test tests/Hlks.Kern.Tests --filter FullyQualifiedName~SchnittbereichTests`, Web `…Hlks.Web.Tests --filter FullyQualifiedName~BenutzerEinstellungen`; Node
  `node --test src/Hlks.Web/wwwroot/js/ansichten/schnittbereich.test.mjs` (Ebenen aus dem Quader, Griffe, Ziehen, Quader aus Punkten, Übernahme der Startansicht, Palette); Browser
  `node src/Hlks.Web/wwwroot/js/ansichten/schnittbereich.browser.mjs` (Port 5480, CDP 9480; Prüfbilder `n13-*.png`, `n14-*.png` in `%TEMP%\HLKS_CAD\n13`).
- **Offen / nicht geprüft:** IFC-Netze der Unterlagen sind im Code geclippt (gleiche Schleife über alle Materialien), in der Browser-Reihe aber nicht belegt (das Beispielprojekt hat keine IFC-Unterlage);
  Schnittflächen offen (siehe oben).

## Nachbearbeitung N15: «Raum begehen» (Ich-Perspektive im 3D, `nachbearbeitung.md`)

Christian, 21.10.2026: «gleicher Ablauf, Kontextm. "Raum begehen", Ansicht wechselt in Egoperspektive in den Raum. Position 1.7 m über Boden, Zentrum Raum. Position kann mittels Pfeiltaste geändert werden.»

**Abfrage `raum_begehen { raum, augenhoehe? }`** (Kern `Plan/RaumBegehen.cs`; auch MCP-Werkzeug und KI, eingeordnet unter «Fachabfragen», nur lesend – sie ändert nichts am Modell; Werkzeugzahl 136):

| Ergebnis | Inhalt |
| --- | --- |
| `standpunkt` | `{ x, y, z, art, abstandZumRand }` in mm: Schwerpunkt des Raumpolygons (`art` «schwerpunkt»); liegt er ausserhalb (L-, U-Form), der innerste Punkt (grösster Abstand zum Rand, Quadbaum-Verfahren «Pole of Inaccessibility», auf 10 mm genau; `art` «innerster Punkt»). `z` = `bodenOk` + Augenhöhe. |
| `bodenOk`, `augenhoehe` | OK Boden nach derselben Regel wie N14 (tiefster Punkt des Polygons, sonst Kote des Geschosses); Augenhöhe Vorgabe 1700 mm (Parameter 300 … 5000). |
| `blick` | waagrecht (`nickenGrad` 0), `azimutGrad` = 90° − `planwinkel` des Gebäudes (Gegenuhrzeigersinn ab Projekt-x; 90° = Projekt-Nord) = die Richtung «oben» des ausgerichteten Grundrisses = Gegenrichtung von «VORNE» des Navigationswürfels (N9/N11); dazu `richtung` [dx, dy, 0]. |
| `raum`, `name`, `nummer`, `geschoss`, `geschossName` | für die Einblendung. |

**Benutzereinstellung `ansicht3d.augenhoehe`** (mm über OK Boden, 300 … 5000, Vorgabe 1700; `BenutzerEinstellungen.cs`, wie `ansicht3d.raumrand`): gilt für alle Projekte und Rechner des Benutzers.

| Funktion | Ort | Dateien |
| --- | --- | --- |
| **Raum begehen** | Grundriss › Rechtsklick auf einen Raum (direkt unter «Raum im 3D zeigen»); Ansicht › 3D-Schnitt (Symbol Figur im Raum, gesperrt ohne gewählten Raum) | `grundriss.js` (Kontextmenü), `aktionen-ansicht.js` (`ansicht.raumBegehen`) |
| **Augenhöhe für «Raum begehen» …** | Grundriss › Kontextmenü; Aktion `ansicht.begehenAugenhoehe` | Benutzereinstellung `ansicht3d.augenhoehe` |

**Ablauf der Aktion `ansicht.raumBegehen`:** Raum aus `p.raeume` oder der Auswahl → Abfrage `raum_begehen` mit der Augenhöhe aus der Benutzereinstellung → die zuletzt benutzte 3D-Ansicht (wie N14; fehlt jede, legt die Oberfläche
`AN-3D` mit `element_anlegen` an) wird geöffnet bzw. nach vorn geholt → `instanz.begehen(standort)`. Das Begehen selbst ist **kein Modellzustand** (nichts im Verlauf, nichts gespeichert); es lebt nur in der Instanz der 3D-Ansicht.

**Der Modus (`ansichten/begehen3d.js`, Rechenregeln rein in `ansichten/begehen-logik.js`):**
- **Kamera:** perspektivisch, Öffnungswinkel 75°, Nahebene 5 cm; `OrbitControls` aus (`enabled = false`). Zustand = Standpunkt (x, z in m), OK Boden, Augenhöhe, Gieren (= Azimut des Kerns) und Nicken; die Kamera sitzt auf `boden + augen`,
  das Ziel liegt 1 m voraus. Die Position der Steuerung (`target`) wird mitgeführt, damit der Navigationswürfel (liest `kamera − target`) mitdreht.
- **Schnittbereich (N13):** `schnittbereich3d.js` hat dafür `pausieren(an)`: keine Clipping-Ebenen, keine Griffe, alles wählbar; der gespeicherte Stand (Quader, `aktiv`) und die Eigenschaft im Modell bleiben unverändert.
  Beim Beenden gilt wieder alles wie vorher (Prüfung: Quader, Zahl der geclippten Materialien, gespeicherter Wert).
- **Anzeige:** oben links Raumkennzeichen · Nummer · Name, Geschoss, «Augenhöhe n m über OK Boden» und die Tastenhilfe (`.d3-begehen`); die Bedienhilfe der Umlaufansicht unten links entfällt.
- **Beenden:** Esc, Haus-Knopf des Würfels (nur beenden, keine Startansicht), Klick auf eine Würfelfläche (beendet und fährt dann zu dieser Fläche); stellt Kamera (Lage, Ziel, FOV, Schnittebenen), Steuerung und Schnittbereich wieder her.
  War die Kamera vor dem Begehen noch nicht vom Benutzer bewegt (z. B. Ansicht erst dafür geöffnet), passt sie sich danach wieder dem Modell an. Ziehen am Würfel und am Kompass bewirkt beim Begehen nichts; «Startansicht festlegen» ist dann ohne Wirkung.
- **Eingriffe in `ansicht3d.js` / `wuerfel.js` (schmal):** `erzeugeBegehen`, Option `begehen { aktiv, beenden }` am Würfel, `zoomAlles` tut beim Begehen nichts, mittlere Taste (Aufnahmephase) geht beim Begehen an `begehen3d.js`,
  Instanz-Methoden `begehen(standort, augenhoeheMm?)`, `beendeBegehen()`, `begehenAktiv`; `data-kamera` enthält jetzt auch `fov`.

**Bedienung – ANNAHMEN (Christian am Freitag bestätigen!):**

| Taste / Maus | Wirkung |
| --- | --- |
| Pfeil hoch / runter | vor / zurück: ein Tastendruck = 100 mm, gehalten (ab 250 ms) flüssig mit 1.4 m/s; nur waagrecht, die Höhe über Boden bleibt |
| Pfeil links / rechts | drehen: ein Tastendruck = 5°, gehalten 90°/s |
| Umschalt | dreifach schneller (Schritt, Drehung, Höhe, flüssig) |
| Bild auf / ab | Augenhöhe ± 100 mm (300 … 5000 mm; nur für das laufende Begehen, die Einstellung bleibt) |
| Maus ziehen (links) | umsehen: 0.25° je Pixel, Ziehen nach rechts = nach rechts schauen, nach oben = nach oben; Nicken auf ±80° begrenzt |
| Mitte ziehen | seitlich und hoch/runter verschieben wie Greifen (5 mm je Pixel): nach links ziehen = nach rechts, nach unten ziehen = höher |
| Esc | beenden |
| Keine Kollision | man geht durch Wände (wie Revit) |

Tastenhilfe, Schrittweiten und «keine Kollision» sind Annahmen des Auftrags; zu klären bleibt ausserdem, ob der Klick auf eine Würfelfläche nach dem Beenden zur Fläche fährt (so umgesetzt) oder nur beendet.

- **Prüfhilfen am DOM:** `.d3-wurzel[data-begehen]` (JSON `{ raum, x, y, z, augenhoehe, bodenOk, azimutGrad, nickenGrad, fov }` in Projekt-mm bzw. Grad; fehlt ausserhalb des Begehens), Klasse `d3-begehen-an`.
- **Prüfen:** Kern `dotnet test tests/Hlks.Kern.Tests --filter FullyQualifiedName~RaumBegehenTests` (11), Web `…Hlks.Web.Tests --filter FullyQualifiedName~BenutzerEinstellungen`; Node
  `node --test src/Hlks.Web/wwwroot/js/ansichten/begehen.test.mjs` (19: Bewegungsrechnung, Tastenbelegung, Tastenverlauf, Einblendung); Browser
  `node src/Hlks.Web/wwwroot/js/ansichten/begehen.browser.mjs` (Port 5540, CDP 9540; 79 Prüfungen; Prüfbilder `n15-*.png` in `%TEMP%\HLKS_CAD\n15`).
- **Anleitung:** Kapitel «zb» in `berichte/anleitung/inventar.json`: `zb-n15-begehen`, `zb-n15-bewegen`, `zb-n15-augenhoehe`.
## Nachbearbeitung N23: «In VR begehen» (Meta Quest über Air Link, WebXR in Chrome; `vorrat_freitag.md`)

Christian, 21.10.2026: «Weg 1»: KICAD läuft in **Chrome am PC**, die Quest hängt per **Air Link (WLAN)** an der Meta-Quest-Link-App; WebXR läuft über die OpenXR-Schnittstelle dieser App. **Ohne Gewähr:** gebaut und ohne Brille geprüft
(Attrappe für `navigator.xr`); der Test mit der Brille steht aus (Christian).

**Kern unverändert**, keine neue Abfrage, kein Modellzustand: VR lebt wie das Begehen am Bildschirm nur in der Instanz der 3D-Ansicht. Standort und Blick liefert weiter die Abfrage `raum_begehen` (N15).

| Funktion | Ort | Dateien |
| --- | --- | --- |
| **In VR begehen** (Knopf) | in der Einblendung des Begehens (oben links, unter der Tastenhilfe) und in der 3D-Ansicht unten rechts (dort nur ausserhalb des Begehens) | `ansichten/vr3d.js` (Knopf, Sitzung), `ansicht3d.js`, `begehen3d.js` (schmale Anschlüsse), `css/ansichten.css` |
| Rechenregeln (Rig, Sticks, Snap-Turn, Teleport, Knopfzustand, Standort ohne Raum) | – | `ansichten/vr-logik.js` (rein, Node-getestet) |

**Knopf:** sichtbar immer; **frei** nur, wenn `navigator.xr.isSessionSupported('immersive-vr')` wahr ist (geprüft beim Aufbau, bei `devicechange` und beim Klick auf den grauen Knopf). Sonst ausgegraut (`aria-disabled`, Deckkraft .55) mit dem Hinweis
«Keine VR-Brille verbunden (Quest Link starten oder KICAD im Browser der Quest öffnen)» (ohne `navigator.xr`: «Dieser Browser kann kein WebXR (Chrome am PC benutzen)»). In der Sitzung steht «VR beenden». Beide Knöpfe zeigen denselben Zustand (`data-vr-status`: aus, bereit, startet, aktiv).

**Ablauf (`vr3d.js`, `starten`):** `navigator.xr.requestSession('immersive-vr', { requiredFeatures: ['local-floor'], optionalFeatures: ['hand-tracking'] })` (als Erstes, im Klick – WebXR verlangt die Benutzeraktion) →
das Begehen am Bildschirm startet (Schnittbereich aus, Bauteile undurchsichtig; läuft es schon, bleibt es und gilt danach weiter) → die Kamera hängt in einem **Rig** (`THREE.Group` in der Szene), das auf **OK Boden** des Raums an den N15-Standpunkt gesetzt wird;
beim ersten gültigen Kopfwert wird das Rig so ausgerichtet, dass der **Kopf** (nicht der Nullpunkt des Referenzraums) am Standpunkt steht und in die N15-Blickrichtung schaut; die **Augenhöhe ist die echte Kopfhöhe** (`local-floor`), nicht die Einstellung →
`renderer.xr` übernimmt, `setAnimationLoop(bild)` zeichnet in jedem Bild (Schatten aus). Ohne Raum (3D-Ansicht, kein Begehen) gilt der **aktuelle Kamerastandpunkt** der Umlaufansicht (x, z), Blick zum Kamerazielpunkt (waagrecht), Boden = tiefste Stelle des Modells.

| Eingabe | Wirkung |
| --- | --- |
| Linker Stick | gehen in Blickrichtung des **Kopfes** (vor/zurück/seitlich), 1.4 m/s, nur waagrecht (Höhe ändert sich nie), Totzone 0.15, Stick gedrückt = dreifach schneller; **keine Kollision** |
| Rechter Stick seitlich | **Snap-Turn** 30° (nach rechts = rechtsherum) um den Kopf; ein Ausschlag = ein Schritt, der nächste erst nach Rückkehr in die Mitte (Ausschlag ab 0.7, zurück unter 0.3) |
| Abzug (Controller) bzw. **Pinch** (Hand) halten | Zielstrahl (blau) auf den Boden des Geschosses (waagrechte Ebene auf **OK Boden**, höchstens 15 m); Ring am Ziel; **Loslassen springt** (der Kopf steht danach über dem Ziel, Höhe und Blick bleiben) |
| Taste A (rechts) | IFC-Unterlage ein/aus |
| Quest-Menü / Esc am PC / Knopf «VR beenden» | Sitzung beenden |

Hände: die Seite bittet um `hand-tracking` (optional) und wertet das `select`-Ereignis aus, das Chrome beim Pinch der Hand auslöst (Zielstrahl = Pinch-Strahl der Hand). Ob die Handverfolgung über **Air Link in Chrome** ankommt, ist ohne Brille nicht prüfbar:
`data-vr` enthält `haende` (true, sobald eine Quelle `inputSource.hand` liefert) und `steuerung` (Zahl der Controller).

**N22 in VR:** Wände, Decken usw. sind undurchsichtig und werden nur **durchscheinend, wenn die Kopfposition** (Weltposition des Kopfes im Referenzraum + Rig, nicht die Kameraposition im Rig) **näher als 500 mm** am Körper ist; `begehen3d.js` bekommt dafür die Option `vr { kopf() }`
(Messpunkt des Durchgangs), `vr3d.js` ruft `begehen.pruefenDurchgang()` in jedem Bild.
**Solide Wände und Böden in VR** (Christian, 21.10.2026: «alle wände und boden sollten in vr solide sein»): Die Körper der Ebenen `wand` und `decke` (Wände, Decken, Böden, Dächer, Stützen, Unterzüge, Treppen) macht der Durchgang N22 undurchsichtig, auch den Nachbau einer IFC-Unterlage. Das **Netz der IFC-Unterlage** (`userData.netz === true`, sonst durchscheinend) setzt `vr3d.js` (`netzUndurchsichtig`, aus `netzeAnwenden`) für die Dauer der Sitzung auf Deckkraft 1, nicht transparent, `depthWrite` an – auch Netze, die erst später fertig geladen werden. Nach dem Ende kommen die gemerkten Werte exakt zurück (`netzeZurueck`). Der Knopf «IFC-Unterlage» blendet das Netz weiter ein und aus. Räume, Öffnungen und HLKS-Elemente bleiben, wie sie sind; im Durchgang (unter 500 mm) werden nur Kern-Körper durchscheinend, Netze nicht.

**Beenden** (Quest-Menü, Esc, Knopf, Schliessen der Ansicht): Kamera (Lage, Drehung, FOV, near/far, Elternobjekt), Schatten, Renderer-Zustand, IFC-Netze, Controller und Marker werden zurückgesetzt; lief das Begehen nur für VR, endet es (Kamera, Schnittbereich, Darstellung wie vorher),
sonst bleibt der Raum am Bildschirm stehen (Esc in VR beendet nur die Sitzung). Das Beenden ist idempotent (Ereignis `end` und Knopf dürfen beide kommen).

**Leistung:** Schatten aus, Pixeldichte und Auflösung bestimmt WebXR (Standard-Framebuffer, Foveation wie in three), kein Neuzeichnen auf Anforderung (`zeichnen()` tut in VR nichts), kein Navigationswürfel. Die **IFC-Unterlage** (ein durchscheinendes, doppelseitiges Netz) ist bei mehr als **300 000 Dreiecken**
in VR zuerst ausgeblendet (Taste A schaltet; weniger: sichtbar). Gemessen ohne Brille (Edge, Software-Grafik, Beispielprojekt «MFH Musterweg»): **92 Zeichenaufrufe, 19 460 Dreiecke je Bild** und rund 0.7 ms CPU je Bild für den Aufbau der Zeichenbefehle –
das ist **kein Mass für die Quest** (dort zählen GPU und Link-Übertragung, 72–90 Bilder/s verlangen unter 11–14 ms je Bild); Bernstrasse mit IFC-Unterlage und die Bildrate in der Brille misst Christian beim Test (`data-vr.bildMs`, `aufrufe`, `dreieckeBild`).

**Eingriffe (schmal):** `ansicht3d.js` (`erzeugeVr`, `zeichnen()` und `groesseAnpassen()` ruhen in VR, Knopf unten rechts, `netzeAnwenden` nach dem Laden eines Netzes, Instanz `vr` und `begehenVr(standort?)`), `begehen3d.js` (Option `vr`, `pruefenDurchgang()`, `standort`),
`css/ansichten.css` (`.d3-vr*`).

**Prüfhilfen am DOM:** `.d3-wurzel[data-vr]` (JSON `{ aktiv, rig {x, y, z, gieren}, kopf {x, y, z}, blickGrad, kamera {x, y, z}, haende, steuerung, ifc {sichtbar, dreiecke}, bildMs, bilder, aufrufe, dreieckeBild }`, Meter bzw. Grad; fehlt ausserhalb von VR), Klasse `d3-vr-an`,
`data-vr-status` am Knopf-Halter `.d3-vr`. Test-Nähte in `instanz.vr.api` (austauschbar): `anbinden`, `kopfPose`, `eingabe`, `controller`, `modell`.

**Einrichtung (Quest per Air Link mit Chrome; ohne Gewähr, Christian testet mit der Brille):**
1. Am PC die **Meta-Quest-Link-App** (früher «Oculus») installieren und mit der Quest verbinden; PC und Quest im selben WLAN (5 GHz), der PC wenn möglich per Kabel am Router.
2. In der App unter *Einstellungen › Allgemein* bei **«OpenXR-Runtime»** «Meta Quest Link» als aktiv festlegen (ohne das findet Chrome die Brille nicht).
3. Auf der Quest *Schnelleinstellungen › Quest Link* bzw. *Air Link* › den PC wählen › *Starten* (die Quest zeigt dann die PC-Umgebung).
4. In **Chrome** KICAD öffnen: Adresse `http://localhost:<Port>` (WebXR geht nur über `localhost` oder https, nicht über den Rechnernamen). Das Programmfenster «ohne Adressleiste» ist je nach Browser nicht Chrome: dann KICAD zusätzlich im Chrome-Tab öffnen.
5. Einen Raum begehen (Grundriss › Rechtsklick › «Raum begehen») oder die 3D-Ansicht öffnen, dann **«In VR begehen»** klicken. Chrome fragt einmal um Erlaubnis; danach setzt man die Brille auf.
6. Bleibt der Knopf grau: Quest Link läuft? Runtime aktiv? Seite neu laden (F5) und den Knopf anklicken (prüft erneut). Handverfolgung: in den Quest-Einstellungen aktivieren; ob sie über Air Link ankommt, zeigt `data-vr.haende`.

**Prüfen:** Node `node --test src/Hlks.Web/wwwroot/js/ansichten/vr.test.mjs` (18: Knopfzustand, Rig-Start aus `raum_begehen`, Snap-Turn, Stick, Teleport, Standort ohne Raum, IFC-Grenze, Bildzeit, N22 mit Kopfposition, Rig im three-Szenegraphen);
Browser `node src/Hlks.Web/wwwroot/js/ansichten/vr.browser.mjs` (Port 5600, CDP 9600; 70 Prüfungen mit eingespritztem `navigator.xr`; Prüfbilder `n23-*.png` in `%TEMP%\HLKS_CAD\n23`).
**Anleitung:** Kapitel «zb» in `berichte/anleitung/inventar.json`: `zb-n23-vr-einrichten`, `zb-n23-vr-bedienen`.

### Kontextmenü eines Raums (Benennung Christian 23.10.2026)
Christian: «benenne im Kontextm. Raum in 3D, Raum begehen, Raum in AR, Raum begehen in VR, Modell am Sitzungstisch». Reihenfolge wie aufgezählt.
Ein Trenner scheidet die Ansichten am PC (Raum in 3D, Raum begehen) von den VR-Ansichten an der Brille (Raum in AR, Raum begehen in VR,
Modell am Sitzungstisch); Christian 26.10.2026: «normale Ansichten und VR ansichten sollen im Kontextmenue getrennt sein».

| Eintrag | Früher | Wirkung |
| --- | --- | --- |
| Raum in 3D | Raum im 3D zeigen | 3D-Ansicht mit dem Raum plus Rand (N14) |
| Raum begehen | Raum begehen | Ich-Perspektive im 3D am PC (N15) |
| Raum in AR | Raum zeigen (VR) | Quest App: nur der Raum plus 1,5 m auf dem Tisch, oben unter der Decke geschnitten; mit «Echter Tisch» auf dem realen Tisch |
| Raum begehen in VR | In VR begehen (Brille) | Quest App: Begehen im Raum |
| Modell am Sitzungstisch | Im Sitzungsraum zeigen (VR) | Quest App: Etage des Raums mit Schnitt unter der Decke, alles darunter |

Die Befehle im Menüband behalten ihre Namen.

## Modul XR, Teile XR-B und XR-C: Greifen, Tablet an der linken Hand, Messwerkzeug (`entwurf/modul-xr.md`)

Christian, 21.10.2026 (Greifen, Tablet, «noch ein messwerkzeug mit fängen», «futuristisch … hologrammmässig»). **Ohne Gewähr:** gebaut und ohne Brille geprüft (Attrappen für `navigator.xr`, Gelenke und Controller); der Test mit der Brille steht aus.
**Kern unverändert** – jede Änderung ist ein normaler Befehl (`POST /api/befehl/<name>`, Paket, Rückgängig, Abgleich). Alle Teile hängen in der VR-Sitzung (`vr3d.js` ruft `erzeugeXr` nach dem Bau der Controller; je Bild `bild(dt)`).

| Datei | Inhalt |
| --- | --- |
| `ansichten/xr-greifen-logik.js` | rein, Node-getestet: Faust/Pinch aus Gelenken, Reichweite, Querverschiebung (1 cm), Handfläche zum Kopf, Lage und Treffer des Tablets, Tasten-Layout |
| `ansichten/xr-mess-logik.js` | rein, Node-getestet: **Fang im Raum** (Anschluss, Ecke, Mitte, Kante, Leitungsachse, Fläche; Finger und Strahl), **Abstand** (dx/dy/dz), **Lot** zur Fläche, **Masse zu Decke und Wand** – gemeinsam für Messwerkzeug und Greifen |
| `ansichten/xr-greifen.js` | Anschluss an die Sitzung: Hände/Controller lesen, Greifen, Befehl beim Loslassen, Zeigestrahl, Tablet-Geste, Messen |
| `ansichten/xr-tablet.js`, `xr-messen.js`, `xr-holo.js` | Tablet (Canvas-Textur, three r160), Messwerkzeug, Hologramm-Stil |

**Greifen (XR-B).** Ein Element in Reichweite (**0.6 m** von der Handfläche bzw. vom Controller bis zur Oberfläche) wird mit der **Faust** (Hand) bzw. der **Grifftaste** (Controller) gegriffen, folgt der Hand im **1-cm-Raster** (andere Elemente: zusätzlich Fang der eigenen Ecken an Ecken und Anschlüssen anderer Elemente bis 5 cm) und wird beim **Loslassen** mit **einem** Befehl verschoben (`elemente_verschieben` mit dx/dy/dz in mm; Paket, Rückgängig). Leitungen, Formteile, Armaturen, Apparate haben Vorrang vor Wänden; Räume, Decken und Öffnungen sind nicht greifbar (Decken ändert man am PC, Öffnungen wandern mit ihrer Wand).
- **Leitung:** *erst antippen, dann greifen* – der erste Griff (oder Abzug/Pinch auf die Leitung) wählt sie nur, der nächste greift. Die Leitung bewegt sich **nur quer zur Längsachse** (oben/unten und waagrecht quer; bei senkrechter Leitung Ost und quer dazu), **parallel**, in **1-cm-Schritten**; der Anteil der Handbewegung in Längsrichtung zählt nicht. Währenddessen stehen im Hologramm-Stil die **Masse lotrecht zur Decke** und **waagrecht zur Wand auf beiden Seiten** (lichter Abstand von der Rohroberfläche und Abstand der Achse) sowie der Versatz (hoch/quer in cm). Gerechnet wird aus den 3D-Körpern (`/api/koerper`, Ebenen decke und wand; der Kern hat dafür keine Abfrage); Öffnungen in Decken zählen als keine Decke.
- **Nachbarn:** Der Kern hat (Stand 21.10.2026) **keinen Befehl**, der eine Teilstrecke quer versetzt und die Anschlüsse mit Verzügen verbindet. Deshalb: hat die Leitung **keine Verbindungen**, verschiebt `elemente_verschieben` sie ganz; **hat sie Nachbarn** (Formteile, Abzweige, Nachbarleitungen), gibt es beim Loslassen einen **Hinweis** («Befehl leitung_versetzen fehlt – nichts verschoben») und nichts wird verschoben. Sobald die Abfrage `faehigkeiten` den Befehl `leitung_versetzen` kennt, ruft die Oberfläche ihn mit `{ leitung, dx, dy, dz }` auf (Schnittstelle siehe Bericht).
- **Reservierung:** Der Kern reserviert nur, während ein Befehl läuft (Sperrdatei je Element); es gibt keine Reservierung auf Zuruf. Beim Greifen prüft die Oberfläche `/api/status` (`reservierungen`): reserviert ein **anderer** das Element, gibt es einen Hinweis und kein Greifen. Der Befehl beim Loslassen wird vom Kern bei fremder Sperre abgelehnt (Hinweis, Element bleibt an Ort).
- **Weit Entferntes:** Zeigestrahl + Abzug (Controller) bzw. Pinch (Hand) auf ein Element **ausserhalb** der Reichweite wählt und **holt es heran** (Leitung: bleibt an Ort und folgt nur quer der Hand; Formteile, Armaturen, Apparate fliegen auf 0.4 m an die Hand); Loslassen = ein Befehl. Auf ein Element **in** Reichweite gezielt: nur antippen (wählen). Ein Strahl trifft immer die **erste** Fläche (nie durch eine Wand).

**Tablet (XR-C).** Linke Hand **offen, Handfläche zum Kopf** (Normale höchstens 60° neben dem Kopf) öffnet das Tablet: **15 Zoll im Hochformat** (Diagonale 381 mm, Seitenverhältnis 3:4, also **22.9 × 30.5 cm**; `TABLET_B`, `TABLET_H`, Textur `TABLET_PX` 480 × 640) **wie in der Hand gehalten**: Mitte **2.5 cm über der Handfläche** (`TABLET_ABSTAND`) und **8 cm nach oben versetzt** (`TABLET_VERSATZ_OBEN`, die Hand trägt den unteren Teil), dem Kopf zugewandt und aufrecht (Christian 21.10.2026 beim Brillentest: «tablett soll auf handdistanz sein. 15" hochformat»); **Faust oder Wegdrehen** (über 80°) schliesst es. Ersatzweise **Taste Y links** (die Menütaste der Quest gehört dem System und kommt nicht bei der Seite an): ein Druck öffnet, der nächste schliesst. Bedient wird mit der **Spitze des rechten Zeigefingers** (Berührung ab 1.5 cm vor der Fläche, ein Druck zählt beim Eintritt) bzw. dem **Strahl** (Abzug/Pinch auf die Taste; kein Teleport).
Kopf oben: Eigenschaften des gewählten bzw. gegriffenen Elements (lesend), bei «Messen» die Anweisung und die letzte Messung (Titel und bis zu drei Zeilen über die ganze Breite). Darunter die Tasten **in zwei Spalten** (sechs Zeilen; Beschriftungen ungekürzt), unten die Taste «VR beenden» über die ganze Breite. Tasten: **Rückgängig**, **Wiederholen** (Befehle `rueckgaengig`/`wiederholen`), **Messen**, **Augenhöhe +/−** (Rig ±10 cm), **Messungen löschen**, **Schnittbereich** (ein/aus; sonst in VR pausiert), **IFC-Unterlage**, **Lot zur Fläche**, **VR beenden**; **Sitzungsraum** (XR-H, wechselt in den Sitzungsraum, siehe unten); ausgegraut mit «folgt»: **Familie platzieren**, **Problem markieren** (XR-E).
**Ruhiges Tablet** (Christian, 21.10.2026 nach dem ersten Brillentest: «tablet ist etwas nervös»): Die Handflächen-Normale (Hebel 14 cm, Hauptquelle des Zitterns) wird vorab mit einem One-Euro-Filter geglättet, die Lage des Tablets mit zwei weiteren (Mitte, Blickrichtung). Dazu eine **Totzone**: weicht die gefilterte Lage um weniger als **1,5 cm** (Mitte) und **4°** (Normale) von der gezeigten ab, **bleibt das Tablet stehen**; darüber folgt es weich (Zeitkonstante 40 ms über dem gefilterten Ziel; grosse Sprünge von 10 cm sind in 0,3 s zu über 90 % eingeholt) und bleibt wieder stehen, sobald es angekommen ist (unter 30 % der Totzone). Beim Öffnen gilt die erste Lage sofort. Berührung und Strahl rechnen mit der gezeigten Lage. Alle Werte stehen als benannte Konstanten (`TABLET_TOTZONE_M`, `TABLET_TOTZONE_GRAD`, `TABLET_RUHE_ANTEIL`, `TABLET_FOLGE_ZEIT`, `TABLET_FILTER_*`) in `xr-greifen-logik.js` und sind nach dem nächsten Brillentest nachzustellen. **Hände bei offenem Tablet:** Die **linke** Hand (sie trägt das Tablet) ist ausgeblendet, die **rechte bleibt ganz sichtbar** (Christian 21.10.2026 nach dem Brillentest: «bei tablet rechte hand immer ganz zeigen»; der frühere Fingerpunkt entfällt). Schliesst das Tablet, erscheint auch die linke wieder; die Controller-Strahlen bleiben unverändert. Abstand des Tablets über der Handfläche zuerst 14 cm, dann 8 cm («tablet etwas weiter weg»), **seit dem 21.10.2026 (Handdistanz, 15 Zoll hochkant) 2.5 cm** (`TABLET_ABSTAND`) – mit dem kleinen Hebel bewegt eine Neigung der Hand das Tablet kaum noch (20° ≈ 1 cm, unter der Totzone): Filter und Totzone bleiben, die Werte sind nach dem nächsten Brillentest nachzustellen. Kontrolle: `data-xr.haende`. Node-Tests: `xr-tablet-ruhe.test.mjs`.
Technik: `HTMLMesh`/`InteractiveGroup` liegen nicht in `lib/three/addons` – das Tablet ist eine **Canvas-Textur** auf einer Ebene mit eigener Trefferlogik (keine CDN-Abhängigkeit).
**Aussehen** (Christian: «futuristisch, mit Transparenz, hologrammmässig»): dunkle, halbtransparente Fläche (hinter der Schrift: Lesbarkeit vor hellen und dunklen Wänden), Linien und Schrift in Cyan/Blau mit leichtem Leuchten, feines Raster und Scanlinien mit langsam laufendem Band, zweite **additiv** gemischte Fläche als weicher, leuchtender Rand; **Aufbau von der Hand aus** (0.35 s, Skalieren und Einblenden), beim Antippen **leuchtet die Taste kurz auf** (0.35 s). Messwerkzeug und Masseinblendungen (Etiketten, Linien, Fangsymbole) haben denselben Stil (`xr-holo.js`).

**Messwerkzeug (XR-C).** «Messen» auf dem Tablet: zwei Punkte mit **Zeigefingerspitze + Pinch** (Hand) bzw. **Strahl + Abzug** (rechter Controller). Die Punkte **fangen** (Radius am Finger 3 cm, am Strahl 3 cm + 3 cm je Meter) auf **Anschluss** (Enden der Achse von Leitungen, Formteilen, Armaturen, Apparaten; der Kern liefert in den Körpern keine benannten Anschlüsse) > **Ecke** > **Mitte** einer Kante > **Kante bzw. Leitungsachse** > **Fläche**, mit **sichtbarem Fangsymbol** (Kreis mit Punkt, Quadrat, Dreieck, Kreuz, Raute) und Namen. Angezeigt: **Abstand** in mm und cm und **dx/dy/dz** (Ost, Nord, oben; mm) – im Raum am Etikett, auf dem Tablet im Kopf. **Lot zur Fläche** («Lot» auf dem Tablet): einer der beiden Punkte liegt auf einer Fläche (Decke, Wand, Boden), gemessen wird lotrecht zu ihr. Messungen **bleiben stehen**, «Messungen löschen» entfernt sie alle. Nichts davon ändert das Modell.

**Wischgeste (XR-C, 21.10.2026, Rückmeldung Brillentest: «masslinien nach einem mass nach wischgeste löschen»).** Wer mit der **offenen Hand** (Hand-Tracking, beide Hände) schnell und überwiegend waagrecht durch eine stehende Masslinie wischt, löscht **diese Messung** («weggewischt wie an der Wandtafel»); die Linie leuchtet kurz auf und blendet in 0,2 s aus. Mehrere getroffene Linien verschwinden zusammen; ein Wisch ins Leere tut nichts, so gehen nicht versehentlich alle Masse weg. Erkannt wird ein Weg der Handflächenmitte von mindestens 15 cm in höchstens 0,4 s (mindestens 0,8 m/s), davon mindestens 80 % waagrecht; getroffen ist eine Linie, wenn der Handweg ihr näher als 10 cm kommt. Nach einem Wisch gilt 0,5 s Sperre. Nicht aktiv bei Faust, Pinch oder gegriffenem Element; bei offenem Tablet wischt nur die rechte Hand und nur mehr als 30 cm von der Tablet-Mitte, die linke trägt das Tablet. Controller kennen die Geste nicht, dort bleibt «Messungen löschen» auf dem Tablet. Die Schwellen sind Annahmen (`WISCH_*` in `xr-greifen-logik.js`) und werden nach dem Brillentest nachgestellt.

**Eingaben.** Über den Eingangspunkt `instanz.vr.api.xr.api.haende()` (Test-Naht): je Seite `{ art: 'hand' | 'controller', gelenke (Name der WebXR-Hand-API → {x, y, z} Welt), ort, griffTaste, abzug, menue, strahl }`. Ohne Naht liest der Standard `renderer.xr.getHand(i)` bzw. die XRInputSources. Kommt `hand` über Air Link nicht an, gelten nur die Controller (Grifftaste, Abzug, Strahl, Taste Y).
**Annahmen (am Brillentest zu bestätigen; alle Schwellen stehen benannt in `xr-greifen-logik.js`):** Faust: mittlere Entfernung der vier Fingerspitzen zum Handgelenk, geteilt durch die Handgrösse (Handgelenk bis Grundgelenk des Mittelfingers), Greifen unter 1.3, Loslassen über 1.55; Pinch: Daumen- und Zeigefingerspitze unter 2.5 cm (los ab 4 cm); die Seite der Handflächen-Normale (links/rechts) nach der Reihenfolge der Gelenke; Reichweite 0.6 m; Raster 1 cm.

**Prüfhilfen am DOM:** `.d3-wurzel[data-xr]` (JSON `{ links, rechts {art, griff, pinch, ort}, tablet {offen, anteil, sichtbar, lage, ursache, leuchten}, messen, messungen, greifen {id, kz, art, offset, oben, seitlich, masse}, befehl, hinweis, fangdaten }`; fehlt ausserhalb von VR).
**Prüfen:** Node `node --test src/Hlks.Web/wwwroot/js/ansichten/xr-b.test.mjs` (13: Greif-Erkennung aus Gelenk-Attrappen, Reichweite, Querverschiebung in 1-cm-Schritten, Befehlsmasse, Masse zu Decke und Wand, Fang, Abstand, Lot, Tablet-Geste, Tablet-Treffer, Layout);
Browser `node src/Hlks.Web/wwwroot/js/ansichten/xr-b.browser.mjs` (Port 5620, CDP 9620; 49 Prüfungen: Greifen und Loslassen = ein Befehl, Rückgängig, Reservierung, Nachbarn, Controller, Strahl, Tablet öffnet/schliesst, Tasten, Messen, Lot; Prüfbilder `xrb-*.png` in `%TEMP%\HLKS_CAD\xrb`).
**Anleitung:** Kapitel «zb» in `berichte/anleitung/inventar.json`: `zb-xr-greifen`, `zb-xr-tablet`.

## Modul XR, Teil XR-H: Sitzungsraum mit Modell auf dem Tisch (`entwurf/modul-xr.md`)

Christian, 21.10.2026: «eine art xr sitzungsraum …, wo das verkleinerte modell auf einem tisch liegt und mit dem tablet die etagen ausgewählt werden kann. das modell soll dann ebenfalls mit der hand gedreht und gezoomt werden können. wird bsp. 1. OG gewählt wird alles oberhalb der Decke dieser etage ausgeblendet. Der raum soll aussehen wie in einer blackmirror folge.»
**Erste Fassung, ohne Gewähr:** gebaut und ohne Brille geprüft (Attrappen für `navigator.xr`, Controller und Gelenke); der Brillentest steht aus. **Kern unverändert** – der Sitzungsraum ändert nichts am Modell und nichts an der Ansicht (auch der gespeicherte Schnittbereich N13 bleibt unberührt).

**Wechsel.** In VR auf dem Tablet **Sitzungsraum** antippen (vorher der Platz «Raum wechseln»). Der Raum steht um den Kopf beim Betreten, der Blick geht zum Tisch. Das **Begehen pausiert** (Sticks, Snap-Turn, Teleport und Durchgang ruhen; man steht am Tisch); das Rig bleibt, wo es war. Auf dem Tablet steht dann **Begehen**: es stellt Standpunkt und Darstellung **exakt** wieder her (Körper wieder an ihrem Platz in der Szene, Hintergrund, Nebel, Lichter, Clipping, ausgeblendete Objekte und IFC-Wahl wie vorher). «VR beenden» im Sitzungsraum stellt ebenso alles zurück.

**Modell auf dem Tisch.** Keine zweite Geometrie: die Körpergruppe der 3D-Ansicht wird skaliert in einen Halter auf dem Tisch umgehängt. Tisch **1.6 × 1.0 m**, Platte **0.75 m** hoch, Vorderkante **0.4 m** vor dem Kopf. Zu Beginn **1:50**, auf die Mitte der Platte eingepasst, Unterkante des Modells auf der Platte (passt es bei 1:50 nicht auf die nutzbare Platte von 1.4 × 0.8 m, wird der Massstab aufgerundet, höchstens 1:200). Die **IFC-Unterlage ist vorgabemässig aus** (Bildrate); die Taste **IFC-Unterlage** auf dem Tablet schaltet sie zu. Der **Massstab** steht als Hologramm-Schrift an der Tischkante (rechts vorn), dazu die gewählte Etage.

**Etagen (Tablet).** Das Tablet (15 Zoll hochkant) zeigt im Sitzungsraum eine andere Seite: **Alle Etagen** und die Geschosse des Modells **untereinander** (oberstes zuerst; **Kote** und **Höhe** am Geschoss, dieselbe Quelle wie «Auf Geschoss» im Register Ansicht; höchstens 8), darunter in zwei Spalten **Decke zeigen** | **Zurücksetzen** und **IFC-Unterlage** | **Begehen**, ganz unten **VR beenden**. Bei der Wahl eines Geschosses wird **alles oberhalb** ausgeblendet – mit einer waagrechten Clipping-Ebene an den Materialien wie beim Schnittbereich N13. **Annahme (Christian bestätigen):** der Schnitt liegt **unter der Decke** des gewählten Geschosses (= Unterkante der Decke), damit man von oben hineinsieht; **Decke zeigen** legt ihn **über die Decke** (Oberkante). Die Oberkante der Decke über einem Geschoss ist die **Kote des Geschosses darüber** (sonst Kote + Geschosshöhe), wie `decke_erzeugen` sie legt; die Dicke kommt von der Decke im Modell (Klasse Decke, Oberkante auf dieser Höhe). **Gibt es im Modell keine Decke, gilt die Oberkante** (der Kopf des Tablets sagt es). **«Unter der Decke» schneidet zusätzlich 50 mm tiefer** (`SCHNITT_TIEFER_MM`; Christian 21.10.2026: «etagenwahl zeigt decke. ev. 5cm weiter unten schneiden» – die Decke kommt oft nur aus der IFC-Unterlage, deren Dicke KICAD nicht kennt). **Zurücksetzen** stellt Massstab, Lage und Drehung des Modells wieder auf den Anfang (die gewählte Etage bleibt).

**Griffe und Hände** (Christian 21.10.2026 nach dem zweiten Brillentest: «das ist nicht praktikabel. es braucht griffe. halten nur mit daumen und zeigefinger.»; die frühere Faust- und Grifftasten-Geste ist ersetzt). Am Modell stehen **zwei sichtbare Griffe**: links und rechts der Modellmitte, **6 cm ausserhalb der Modellbreite** (`GRIFF_ABSTAND_M`) und **5 cm über der Tischplatte** (`GRIFF_HOEHE_M`); sie wandern mit Lage, Drehung und Massstab des Modells (beim Zoomen an die neue Breite). Aussehen im Hologramm-Stil: kleine Cyan-Kugel (2.5 cm, `GRIFF_DURCHMESSER_M`) mit Ring und weichem Schein; **nähert sich eine Hand bis 5 cm** (`GRIFF_HOVER_M`), wird der Griff **heller**; **gehalten** leuchtet er **deutlich** (weiss, grosser Schein).
- **Gehalten wird nur mit Daumen und Zeigefinger** (Pinch, die vorhandene Erkennung `istPinch`; eine **Faust greift nichts**): der Pinch muss **höchstens 5 cm** vom Griff entfernt beginnen (`GRIFF_FANG_M`, steigende Flanke, gemessen an der Mitte zwischen Daumen- und Zeigefingerspitze) und gilt bis zum Loslassen; ein Pinch, der weit weg begann und zum Griff wandert, fasst nicht. Eine Hand fasst den Griff, der ihr am nächsten ist; ein Griff gehört nur einer Hand. **Controller:** der Abzug am Griff (Nähe 5 cm **oder Zielstrahl-Treffer**) entspricht dem Pinch.
- **Bewegt wird nur, solange beide Griffe gehalten sind** (Christian 21.10.2026, erster Brillentest: «greifen … nur mit zwei händen … zoomen nur wenn gegriffen»): ein Griff allein – das Modell **ruht** (Zustand «wartet»). Mit beiden: der **Abstand der Pinch-Punkte zoomt um deren Mittelpunkt** (die Stelle unter dem Mittelpunkt bleibt dort), von **1:200 bis 1:20**; die Drehung der Verbindungslinie **dreht** das Modell um die Senkrechte; der Mittelpunkt **verschiebt** es (bleibt auf der Platte). Die Hände dürfen die Griffe beliebig nehmen (auch die rechte Hand am linken Griff).
- Das Modell bleibt **immer waagrecht** (gegen Übelkeit). Beim Wechsel von einem auf zwei Griffe (und zurück) springt nichts. Elemente greifen, Messen und Teleportieren gibt es im Sitzungsraum nicht.

**Schnittbereich (N13) nach dem Sitzungsraum** (Befund Brillentest 21.10.2026: «begehen zeigt nur schwarz» bei einem Schnittbereich, der den Standpunkt nicht enthält). Der Sitzungsraum gibt die Clipping-Ebenen der Materialien beim Verlassen **nicht mehr selbst zurück**: er nimmt nur seine eigenen Ebenen weg und übergibt an den Schnittbereich (`schnittNeu` → `neueObjekte()`), der neu bewertet, ob er gerade gilt – **im Begehen ist er pausiert** (keine Ebenen, solange das Begehen läuft), nach dem Ende der VR-Sitzung (ohne vorheriges Begehen) gilt er wieder wie vorher. Auch der **Durchgang N22** (undurchsichtige Wände) wird nach der Rückkehr neu bewertet. Vorher kamen die beim Betreten vorgefundenen – veralteten – Ebenen zurück, sobald sich der Zustand des Schnittbereichs im Sitzungsraum geändert hatte (Taste «Schnittbereich» des Begehens, Auftrag vom PC).

**Ausschnitt («Raum in AR», 3D-Ansicht in VR).** Der Sitzungsraum kann statt des ganzen Modells nur einen **Quader** zeigen (`api.sitzung.ausschnittSetzen(quader, text)`; Quader wie die Ansichtseigenschaft `schnittbereich`: `{ aktiv, min, max, winkel }` in mm). Geclippt wird mit **sechs Ebenen** an den Materialien (statt der Etagenebene); sie folgen dem Modell auf dem Tisch. Der **Massstab passt den Ausschnitt auf die Platte**: so fein wie möglich (**1:20**), gröber nur, wenn er sonst nicht auf die nutzbare Platte (1.4 × 0.8 m) passt, höchstens 1:200; die Mitte des Ausschnitts liegt auf der Plattenmitte, seine Unterkante auf der Platte. Auf dem Tablet: Kopf «Ausschnitt: …», **«Alle Etagen» steht dann nicht auf Ein** – die Taste zeigt wieder das ganze Modell, die Wahl einer Etage ersetzt den Ausschnitt, «Zurücksetzen» passt ihn neu ein. Aufträge siehe «VR-Auftrag vom PC» unten.

**Aussehen** («wie in einer Black-Mirror-Folge»; Vorschlag zum Abnehmen, keine Logos): dunkler Raum **8 × 6 m**, 3.2 m hoch, mattschwarze Wände und Decke, **glänzender, leicht spiegelnder Boden** (Standardmaterial mit niedriger Rauheit; keine Echtzeit-Spiegelung – die Lichtlinien spiegeln sich als Gegenstück unter dem durchscheinenden Boden), **schmale weisse Lichtlinien** in Decke und Wänden, **leichter Dunst** (Nebel), **Glastisch mit leuchtender Cyan-Kante** (Stil `xr-holo.js`) und weichem Schein auf dem Boden, Modell hell und matt (seine Materialien, gegenüber der 3D-Ansicht etwas gedämpftes Licht). Wenig Last: ein Punktlicht, keine Schatten, die Lichtlinien als zwei Instanz-Gruppen.

| Datei | Inhalt |
| --- | --- |
| `ansichten/xr-sitzung-logik.js` | rein, Node-getestet: Einpassen, Massstab und Grenzen, Griffe (Lage, Treffer, benannte Konstanten `GRIFF_*`), Gesten (zwei Griffe, Wechsel), Ausschnitt (Bezug, Massstab, Ebenen), Geschosse und Schnitthöhe, Ebene im Raum, Tablet-Seite |
| `ansichten/xr-sitzung.js` | Raum, Tisch, Halter, Griffe (Anzeige), Etagenschnitt und Ausschnitt, Hände, Übergabe an den Schnittbereich, Wiederherstellen |
| `vr3d.js`, `xr-greifen.js`, `xr-tablet.js` | schmale Anschlüsse (Hooks `sitzung`, `layout`, Taste `raum`) |

**Prüfhilfen am DOM:** `.d3-wurzel[data-xr-sitzung]` (JSON `{ aktiv, massstab, massstabText, drehungGrad, lage, modus ('zwei' | 'wartet' | null), geschoss, deckeZeigen, schnittMm, ebeneWelt, etagen, tisch, raum, ausschnitt { text, min, max, winkel } | null, griffe [{ seite, x, y, z (Welt), hover, gehalten }], material, geclippt, ifc }`; fehlt ausserhalb des Sitzungsraums); Test-Naht `instanz.vr.api.sitzung` (`ausschnittSetzen`).
**Prüfen:** Node `node --test src/Hlks.Web/wwwroot/js/ansichten/xr-sitzung.test.mjs` (23: Einpassen 1:50, Zoomgrenzen, Griffe und Treffer, Einhand-/Zweihandrechnung, Gestenablauf mit Griffen, Schnitthöhe, Ebene im Raum, «Alle Etagen», Zurücksetzen, Tablet-Seiten im Hochformat, Ausschnitt);
Browser `node src/Hlks.Web/wwwroot/js/ansichten/xr-sitzung.browser.mjs` (Port 5660, CDP 9660; 105 Prüfungen mit dem Beispielprojekt: Wechsel per Fingertipp, Modell 1:50 auf der Platte, Ebene gegen eine unabhängige Rechnung aus den Geschossdaten der API, Griffe (Hover, Faust, Pinch weit weg, ein Griff, beide Griffe mit Händen und Controllern, Zoom, Drehen, Zielstrahl), Zurücksetzen, exakte Rückkehr; **Schnittbereich ausserhalb des Standpunkts** (Begehen, direkter Start, Quest-Menü, Zustandswechsel im Sitzungsraum); Ausschnitt; Prüfbilder `xrh-*.png` in `%TEMP%\HLKS_CAD\xrh`).
**Nur mit der Brille beurteilbar:** Tischhöhe und Abstand, ob 1:50 gross genug wirkt, die Griffe (Grösse, Abstand 6 cm, Fangweite 5 cm, ob der Pinch bei fast geschlossenen Fingern als Faust gilt und deshalb nicht greift), Bildrate in der Quest, Aussehen des Raums (Boden, Dunst, Lichtlinien), Lesbarkeit der Etagenseite auf dem Tablet, Lage des 15-Zoll-Tablets über der Hand.
**Anleitung:** Kapitel «zb» in `berichte/anleitung/inventar.json`: `zb-xr-sitzungsraum`.

## Nachbearbeitung N17: Treppen im Grundriss (`nachbearbeitung.md`, Abschnitt N17)

Christian, 21.10.2026: «auch treppen sind im Grundriss noch nicht korrekt angezeigt. resp. nicht erkennbar.» Die Treppe ist ein Element der Klasse **Treppe** (Kürzel TR); der Kern
rechnet alles (`Grafik/TreppeGrafik.cs`, `Architektur/Treppenlage.cs`, `Import/IfcTreppe.cs`), die Oberfläche zeichnet nur die Primitive (`plangrafik.js` unverändert: `linie`, `polylinie`,
`polygon`, `kreis`, `text`, Linienart `strich`). Keine neue Bedienung: die Treppe entsteht beim IFC-Übernehmen; Zeichnen von Hand folgt später (heute `element_anlegen`).

| Funktion | Ort | Kürzel | Dateien |
| --- | --- | --- | --- |
| Treppe im Grundriss (Stufenkanten, Schnittlinie, Lauflinie mit Kreis und Pfeil, «n Stg. h/a», Podeste) | Grundriss jedes Geschosses mit einer Treppe | – | `TreppeGrafik.cs` |
| Treppe von oben im Geschoss darüber | Grundriss des Geschosses, in das sie hinaufführt | – | `TreppeGrafik.AusUnterGeschoss`, `Grafik.Sichtbar` |
| Werte lesen und ändern (stufen, steigung, auftritt, laufbreite, richtung, unterkante, oberkante, laufdicke, material) | Eigenschaften der Treppe (anklicken; Herkunft am Wert: IFC-Attribut, Pset oder «Geometrie …») | – | `klassen.json` (Treppe) |

- **Anwählen:** eine unsichtbare Fläche über den Trittflächen (`nurBildschirm`, Deckkraft 0.01) macht die Treppe anklickbar; Fangpunkte sind die Enden der Stufenkanten und der Lauflinie.
- **Beschriftung:** Text mit `mitdrehen` (folgt dem Lauf; im gedrehten Grundriss N9 bleibt er lesbar), Schrift 2.5 mm Papier, über der Lauflinie.
- **Verschieben, Kopieren, Drehen, Spiegeln** (`elemente_*`) führen Stufenkanten, Podeste, unterkante und oberkante mit der Lauflinie mit.

**Prüfen:** Kern `dotnet test tests/Hlks.Kern.Tests --filter FullyQualifiedName~Treppe`; Prüfbilder `node werkzeuge/treppe_pruefbild.mjs` (nach `TreppeEchtTests`; Bilder in `%TEMP%\HLKS_CAD\n17`).
## Nachbearbeitung N20 und N21: Detailgrad «grob» schwarz voll, Schnitthöhe nach SIA 400 (`nachbearbeitung.md`)

Christian, 21.10.2026: «detaillierungsgrad grob, alle wände schwarz solid.» und «schnitthöhe plan gem. sia». Beides rechnet der Kern; die Oberfläche zeigt nur.

**N20 (Kern `Grafik/ArchitekturGrafik.cs › GrobStil`, `Schnittgrafik.cs`):** Bei `detailgrad = grob` zeichnen geschnittene Wände, Stützen und Unterzüge eine volle Fläche
`{ fuellung: "schnitt", deckkraft: 1 }` ohne `muster`, ohne `farbe` und ohne Kreuz (N16) bzw. Holzbeige (N18); der Stift der Kontur bleibt dick. Gilt im Grundriss und im Schnitt (die Schnittfläche
der Ebene «wand»). Bildschirm (`plangrafik.js`, Füllung «schnitt» = Planfarbe, hell im dunklen Design) und PDF (`PdfZeichner`, Pinsel «schnitt») zeichnen die vorhandene Füllung –
nichts Neues in der Oberfläche. «mittel» und «fein» sind unverändert. Der Detailgrad wird wie bisher unten in der Ansicht oder in den Eigenschaften gewählt.

**N21 (Kern `Grafik/SchnitthoeheSia.cs`):**

| Eigenschaft der Ansicht | Inhalt |
| --- | --- |
| `schnitthoeheNachSia` (bool) | Ohne Angabe **ja**, auch für bestehende Ansichten. Ja: der Kern rechnet die Schnitthöhe des Grundrisses je Geschoss (SIA 400:2000 Ziff. C.3.1.1). Nein: es gilt `schnitthoehe` von Hand (Vorgabe 1000). |
| `schnitthoehe` (mm über OK Geschoss) | Handwert; gilt nur bei `schnitthoeheNachSia = false`. Er bleibt beim Einschalten gespeichert. |

- **Regel:** Höhenbereiche aller Türen (ab OK Boden) und Fenster (Brüstung … Brüstung + Höhe) der Wände des Geschosses; gemeinsamer Bereich → der Wert darin am nächsten bei 1000 mm; sonst die Höhe mit den
  meisten geschnittenen Öffnungen (Gleichstand: nächste bei 1000) mit Hinweis auf die übrigen; ohne Öffnungen 1000 mm mit Hinweis. Auf 10 mm gerundet, im Bereich bleibend. Zwischengespeichert je Stand des Modells.
  `Grafik.Kontext` setzt damit `GrafikKontext.Schnitthoehe`; alles Übrige (Wände, Öffnungen, Stützen, Unterzüge, IFC-Schnitt, Treppen, Aussparungen, PDF) liest nur diesen Wert.
- **Abfrage `schnitthoehe_sia { ansicht | geschoss }`** (auch MCP-Werkzeug und KI, lesend, zur Bestätigung): `wert`, `herkunft` (Text mit «SIA 400:2000 Ziff. C.3.1.1», Bereich und massgebenden Öffnungen), `hinweise[]`,
  `massgebend[]`, `oeffnungen`, `geschnitten`, `von`, `bis` (mm über OK Boden) und mit `ansicht` zusätzlich `nachSia`, `handwert`, `gilt`.
- **Eigenschaftenpalette** (`oberflaeche/schnitthoehe-palette.js`, eingehängt in `eigenschaften.js`): Ist der Haken `schnitthoeheNachSia` ein, ersetzt eine schreibgeschützte Zeile «schnitthoehe» («1000 mm über OK Geschoss
  (nach SIA 400)») das Eingabefeld, darunter «Herkunft», «Hinweis» (je nicht geschnittener Öffnung) und «Handwert». Ist der Haken aus (oder antwortet der Kern nicht), erscheint das Feld `schnitthoehe` wie bisher.
  Der Haken steht nur bei Grundrissen. Die Palette fragt beim Zeichnen (Wahl bzw. Wechsel der Ansicht, Änderung der Ansicht), nicht bei jeder Änderung an Türen und Fenstern.
- **Prüfen:** Kern `dotnet test tests/Hlks.Kern.Tests --filter "FullyQualifiedName~SchnitthoeheSiaTests|FullyQualifiedName~DetailgradGrobTests"`; Node
  `node --test src/Hlks.Web/wwwroot/js/oberflaeche/schnitthoehe-palette.test.mjs`; Browser `node src/Hlks.Web/wwwroot/js/oberflaeche/schnitthoehe.browser.mjs` (Port 5560, CDP 9560; Bilder `n21-*.png`
  in `%TEMP%\HLKS_CAD\n21`). Die echte Bernstrasse 42 prüft `SchnitthoeheSiaProjektTests.Bernstrasse42_Echt_…` (nur mit `HLKS_ABNAHME=1`).

## Modul XR: «Quest verbinden» (KICAD in der Quest ohne Meta Link; `entwurf/modul-xr.md`, «Start ohne Link-fähigen PC»)

Der Browser der Quest öffnet KICAD als `http://localhost:<Port>`; `adb reverse` leitet den Port der Quest auf diesen PC. KICAD bleibt nur lokal erreichbar, WebXR gilt auf localhost als sicher.
Der Dialog macht in KICAD, was `werkzeuge/quest_verbinden.ps1` macht (das Skript bleibt als Alternative). **Eine reine Rechner-Funktion** wie «Ordner im Explorer zeigen» (`POST /api/ordner/zeigen`): kein Befehl und keine Abfrage des Kerns,
das Modell wird nicht berührt, es entsteht kein Paket. Die Regel «jede Oberflächenaktion hat ihren Befehl» gilt für Modelländerungen; für diese Funktion gilt sie nicht – sie ist in `KiEinordnung.RechnerFunktionen` als **für die KI verboten**
eingetragen (Grund: startet ein Programm auf dem PC), es gibt kein MCP-Werkzeug dazu, und `QuestTests` hält das fest.

- **Öffnen:** Register **XR › Gruppe Brille › «Quest verbinden …»** (seit 26.10.2026; vorher im Register Ansicht; Abschnitt «Register XR») (Aktion `ansicht.questVerbinden`, immer verfügbar); in der 3D-Ansicht der **graue Knopf «In VR begehen»** («Keine VR-Brille verbunden …», ein Klick prüft erneut und öffnet sonst den Dialog)
  und daneben der Link **«Quest im Browser der Brille nutzen …»** (nur beim grauen Knopf sichtbar). Der Anschluss in `ansichten/vr3d.js` ist minimal (dynamischer Import von `oberflaeche/quest-dialog.js`, Funktion `questDialogOeffnen`).
- **Ampel** (vier Punkte, `quest-text.js` `ampel(zustand)`): *adb gefunden* · *Quest verbunden* · *Freigegeben* · *Weiterleitung aktiv*; grün = in Ordnung, rot = hier fehlt etwas, grau = noch nicht an der Reihe. Darüber «Erneut prüfen», darunter der nächste Schritt in einem Satz.
- **Knöpfe:** «Verbinden» (setzt `adb reverse tcp:<Port> tcp:<Port>`), «Auf WLAN umstellen» (einmal mit Kabel: `adb tcpip 5555`, `adb connect <Adresse der Quest>:5555`, Weiterleitung neu), Feld **WLAN-Adresse** mit «Über WLAN verbinden» (IPv4, optional `:Port`), «Trennen» (entfernt die Weiterleitung),
  Feld **Pfad zur adb.exe** mit «Speichern» (Benutzereinstellung `quest.adb`; leer speichern = wieder automatisch suchen). Während eines Aufrufs sind alle Knöpfe gesperrt (Spinner).
- **Zwei Wege derselben Quest** (Befund Meta Quest 3, 21.10.2026: nach «Auf WLAN umstellen» zeigt `adb devices` USB und `IP:5555` gleichzeitig als `device`, die Weiterleitung gilt je Weg): Eine gemeinsame Wahl gilt für Zustand, «Verbinden», «Trennen» und «Auf WLAN umstellen»:
  (1) der Weg, der die Weiterleitung für den Port schon hat; (2) sonst ein WLAN-Weg; (3) sonst USB (haben beide die Weiterleitung, gilt WLAN; gewählt wird nur unter Wegen im Zustand `device`). `seriennummer`, `wlan`, `ip` und `weiterleitung` des Zustands gehören zum gewählten Weg; die Liste `geraete` zeigt alle.
  **«Auf WLAN umstellen» wartet bis zu 15 s** (`QuestEinstellungen.WlanGrenze`): nach `adb tcpip` startet adbd in der Quest neu, `adb connect` wird im Abstand von 1,5 s (`WlanAbstand`) wiederholt, bis adb die Verbindung meldet **und** der WLAN-Weg in `adb devices` als `device` steht (zuerst ist er kurz `offline`); die Weiterleitung wird danach auf dem
  WLAN-Weg gesetzt und bei Fehler oder `offline` bis 6 s (`WeiterleitungGrenze`) wiederholt. Jeder einzelne adb-Aufruf behält die Zeitgrenze von 10 s. Ist die Grenze ausgeschöpft, meldet der Dialog die letzte Antwort von adb und den Hinweis «gleiches WLAN, Adresse von Hand». **Ein zweiter Klick bricht die WLAN-Verbindung nicht ab:**
  ist schon ein WLAN-Weg `device`, läuft kein weiteres `tcpip`, nur die Weiterleitung wird dort gesetzt (falls sie fehlt). «Trennen» entfernt die Weiterleitung auf allen Wegen im Zustand `device`; Fehler auf Wegen ohne Weiterleitung werden ignoriert.
- **Ist adb nicht gefunden (Ampel rot)**, zeigt der Dialog die Einrichtung Schritt für Schritt – **KICAD lädt nichts herunter und installiert nichts**: (1) Android Platform Tools von der offiziellen Seite holen (Link `https://developer.android.com/tools/releases/platform-tools`, öffnet im Browser mit
  `target="_blank" rel="noopener noreferrer"`), (2) die ZIP-Datei nach `%USERPROFILE%\platform-tools` entpacken (dieser Ordner steht in der Suche an erster Stelle der bekannten Orte), (3) «Erneut prüfen». Darunter stehen die gesuchten Orte (`suchorte` des Zustands).
- **Hinweise im Dialog** (einfache Worte, `quest-text.js` `HINWEISE`): Entwicklermodus über die Meta-Horizon-App (einmalig eine Entwickler-Organisation bei Meta), Datenkabel (USB-C), in der Brille «USB-Debugging erlauben» (immer von diesem Computer), bei «Keine Quest gefunden» trotz Kabel der
  Meta-ADB-Treiber (developers.meta.com, Administratorrechte; KICAD installiert ihn nicht), danach in der Quest `http://localhost:<Port>/vr` öffnen (die VR-Startseite, siehe «Modul XR: VR-Startseite»). Unten steht die Adresse mit dem Port dieses KICAD.
- **Schnittstelle** (`QuestEndpunkte.cs`, Dienst `QuestDienst.cs`; alles unter `/api`, also hinter dem Herkunftsschutz: nur localhost, POST nur `application/json`, kein fremder Ursprung):
  `GET /api/quest` → `{ adbGefunden, adbPfad?, adbQuelle? (einstellung|pfad|ort), adbHinweis?, suchorte? (nur ohne adb), geraete: [{ seriennummer, zustand, wlan }], questVerbunden, freigegeben, nichtFreigegeben, seriennummer?, wlan, ip?, weiterleitung, port, url, fehler? }` (liest nur: `adb devices`, `reverse --list`, WLAN-Adresse);
  `POST /api/quest/verbinden` · `/wlan` · `/ip { ip }` · `/trennen` → derselbe Zustand mit `meldung`; Fehler 400 `{ fehler, vorschlag }` (z. B. «Es wurde keine Quest gefunden», «noch nicht freigegeben», Zeitgrenze, `connect` fehlgeschlagen).
  Der **Port** ist der Port des Host-Kopfs der Anfrage (der Server, der sie bekam) – nie ein Wert aus dem Körper.
- **Sicherheit:** gestartet wird nur eine Datei namens `adb.exe` – aus der Einstellung `quest.adb` (vollständiger lokaler Pfad, kein Netzpfad, kein `..`; geprüft beim Speichern und vor jedem Start), sonst aus `PATH`, sonst von den bekannten Orten (`%USERPROFILE%\platform-tools`, `%LOCALAPPDATA%\Android\Sdk\platform-tools`,
  `Downloads`, `OneDrive\Downloads`, `C:\platform-tools`, `C:\Android\platform-tools`). Nur feste Argumente (`devices`, `reverse`, `tcpip 5555`, `connect`, `shell ip -f inet addr show wlan0`); eingesetzt werden nur der Port (Zahl), eine Seriennummer aus `adb devices` (beginnt mit Buchstabe oder Ziffer, nur `[A-Za-z0-9._:-]`)
  und eine geprüfte IPv4-Adresse (Oktette 0–255, erste 1–223, nicht 127, Port 1–65535). Keine Shell (`ArgumentList`), Zeitgrenze 10 s je Aufruf (danach wird nur der selbst gestartete Prozess über seine PID beendet), Ausgabe höchstens 64 KB, ein Aufruf zur Zeit.
- **Prüfen:** Web `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~QuestTests"` (Attrappe `tests/Hlks.AdbAttrappe` als `adb.exe` unter `%TEMP%`: kein Gerät, unauthorized, device, Weiterleitung, WLAN, IP-Prüfung, Zeitgrenze, nur adb.exe, Ursprungsprüfung, KI-Einordnung);
  Node `node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest.test.mjs`; Browser `node src/Hlks.Web/wwwroot/js/oberflaeche/quest.browser.mjs` (Port 5640, CDP 9640; Server mit Attrappe, nie ein echtes adb; Bilder `quest-*.png` in `%TEMP%\HLKS_CAD\quest`).
  **Anleitung:** Kapitel «zusatz» in `berichte/anleitung/inventar.json`: `zb-xr-quest-verbinden`.

## Modul XR: VR-Startseite (`/vr`; Christian Steiner 21.10.2026: «ich möchte in VR lediglich ein Icon anklicken um im Modell zu landen»)

Eigene Seite für die Meta Quest ohne PC-Oberfläche: kein Menüband, kein Projektbrowser. Ein Icon in der Quest öffnet sie, **ein Tipp (Pinch) irgendwo** startet VR im Modell. Der Weg «Projektbrowser › 3D-Ansichten › «3D Gesamt» › «In VR begehen»» entfällt.
Keine Fachlogik in der Oberfläche: die Seite lädt das **aktuell offene Projekt des Servers** über dieselben Schnittstellen wie die PC-Oberfläche (`/api/modell`, `/api/koerper`, Abfrage `raum_begehen`), baut die Szene mit der vorhandenen 3D-Ansicht (`ansichten/ansicht3d.js`) und startet VR mit `vr3d.js`
(`starten(standort)`). Neu sind nur Seite, Startpunktwahl und Merken des Standpunkts.

- **Aufruf:** `http://localhost:<Port>/vr` (Server `VrSeite.cs`; `wwwroot/vr.html`, `js/vr-start.js`, `js/vr-start-logik.js`, `css/vr-start.css`). `/vr/` führt auf `/vr` zurück. **Ursprungsprüfung:** `/vr`, `/vr.html` und `/vr.webmanifest` liegen wie `/api` und `/hilfe` hinter dem Herkunftsschutz (Host-Kopf nur `localhost`, `127.0.0.1`, `[::1]`).
- **Seite:** bildschirmfüllend ein grosser Knopf **«Eintreten»** mit Projektname, **Geschoss** und **Raum** des Startpunkts und der Quelle («Startpunkt: Erster Raum im Erdgeschoss» usw.). Der Knopf ist die ganze Fläche: ein Tipp irgendwo startet die Sitzung (`requestSession` im Klick, vor jedem `await`; der Standort ist vorher gewählt).
  Dahinter liegt die 3D-Szene ohne Bedienelemente. Ohne erkannte Brille steht ein Hinweis («Keine VR-Brille erkannt: diese Seite im Browser der Meta Quest öffnen»), der Tipp versucht es trotzdem. Ist am PC kein Projekt offen, steht «Kein Projekt offen» und die Seite lädt selbst neu, sobald eines geöffnet wird.
  Das Projekt bleibt aktuell: Änderungen am Modell kommen über den Ereignisstrom in die Szene; ein Projektwechsel lädt die Seite neu (nicht mitten in der Sitzung, sondern danach).
- **Startpunkt** (`vr-start-logik.js`, in dieser Reihenfolge; der Kern entscheidet, ob ein Raum taugt – `raum_begehen` lehnt Räume ohne Umriss ab, dann gilt der nächste Kandidat):
  0. **Auftrag vom PC** (siehe unten; Hauptweg): ein `begehen`-Auftrag mit Raum geht allem voraus.
  1. **Vom PC gesandter Raum:** Adresse `/vr?raum=<Id>` (Nebenweg). Geprüft gegen das Muster einer Element-Id des Kerns (Guid); das Kennzeichen eines Raums (z. B. `R-1`) geht auch. Ungültig oder unbekannt: wird ignoriert.
  2. **Zuletzt in VR benutzter Standpunkt:** Benutzereinstellung `vr.letzter_standpunkt` (je Benutzer, auf allen Rechnern wie die übrigen Einstellungen; Kleinbuchstaben, weil der Server nur solche Schlüssel annimmt). Gilt nur im selben Projekt (Ablageort wird mitgespeichert).
  3. **Erster Raum des untersten oberirdischen Geschosses** (Kote ab ±0.00, in der Regel das EG), Räume nach Kennzeichen geordnet, bis zu fünf Versuche.
  4. **Standpunkt der 3D-Gesamtansicht** (die 3D-Ansicht mit «Gesamt» im Namen, sonst `AN-3D`, sonst die erste).
- **VR-Auftrag vom PC** (Hauptweg; Christian 21.10.2026: «die am pc eingestellten befehle … vom VR gerät aufrufen, es bleibt ja immer die gleiche adresse auf localhost»): Der PC setzt einen Auftrag, die Brille holt ihn. Er geht vor dem gemerkten Standpunkt und vor `?raum=`.
  - **Am PC:** Grundriss › Rechtsklick auf einen Raum › **«In VR begehen (Brille)»** (Auftrag `begehen` mit dem Raum) und **«Geschoss im Sitzungsraum zeigen (Brille)»** (Auftrag `sitzungsraum` mit dem Geschoss des Raums); in der 3D-Ansicht der Link **«An die Brille senden»** unter dem Knopf «In VR begehen» (und in der Einblendung des Begehens):
    läuft das Begehen, geht dessen Raum an die Brille (`begehen`), sonst die 3D-Gesamtansicht als `sitzungsraum`. Danach die Meldung «Auftrag an die Brille: Begehen · EG · E.01 Wohnen/Küche – in der Quest KICAD VR öffnen.» (`oberflaeche/vr-auftrag.js`, `aktionen-vr-auftrag.js`).
  - **«Raum in AR»** (Christian 21.10.2026: «Raum Zeigen ebenfalls als VR funktion im Kontextmenue»): im selben Kontextmenü, das Gegenstück zu «Raum im 3D zeigen» (N14) für die Brille. Auftrag `{ modus: "sitzungsraum", raum, ausschnitt: true }` mit Autostart. In der Brille zeigt der Sitzungsraum auf dem Tisch **nur den Raum plus Rand** (Vorgabe 1.5 m wie in N14, Benutzereinstellung `ansicht3d.raumrand`): derselbe Quader, vom Kern berechnet – die Abfrage `raum_begehen` liefert mit dem Parameter `rand` zusätzlich `bereich` (`{ aktiv, min, max, winkel }`, dieselbe Rechnung wie `ansicht_raum_zeigen`, ändert **keine** Ansicht). Massstab passt den Ausschnitt auf die Platte (1:20, bei Bedarf gröber); die Etagenwahl auf dem Tablet ist aus, «Alle Etagen» zeigt wieder das ganze Modell.
  - **3D-Ansicht in VR** (Christian 21.10.2026: «die 3dAnzeige fenster auch in VR möglich machen»): der Knopf **«In VR begehen»** in einer 3D-Ansicht sendet, wenn dort **kein Begehen läuft**, den Auftrag `{ modus: "sitzungsraum", ansicht: <3D-Ansicht> }` mit Autostart (läuft das Begehen, wie bisher dessen Raum). Die Brille stellt die Szene auf diese Ansicht um (**Filter**: Gewerke und ausgeblendete Klassen der Ansicht) und zeigt auf dem Tisch den **Schnittbereich** der Ansicht (N13-Quader, falls aktiv) als Ausschnitt – dieselbe Technik wie «Raum in AR»; ohne aktiven Schnittbereich das ganze Modell mit den Filtern der Ansicht. Ein späterer Auftrag «Sitzungsraum» ohne Ansicht stellt Gesamtansicht und ganzes Modell wieder her.
  - **Server** (`VrAuftrag.cs`, hinter dem Herkunftsschutz wie alle `/api`): `GET /api/vr/auftrag` → `{ auftrag: { modus, raum?, geschoss?, ausschnitt, ansicht?, nr, zeit } | null }`; `POST /api/vr/auftrag` mit `{ modus: "begehen" | "sitzungsraum", raum?, geschoss?, ausschnitt?: bool, ansicht?: Id }` – `ausschnitt` braucht den Modus `sitzungsraum` und einen Raum, `ansicht` den Modus `sitzungsraum` ohne Raum und Ausschnitt; ist ein Projekt offen, muss `ansicht` eine Ansicht der Art `3d` sein (sonst 400). `nr` vergibt der Server und zählt bei jedem Setzen hoch (ein `nr` im Körper zählt nicht), `zeit` auch.
    Gespeichert im Speicher des Servers und zusätzlich in den Benutzereinstellungen (`vr.auftrag`), damit er einen Neustart übersteht. Modus nur aus der Liste; Raum und Geschoss gegen das Id-Muster (Guid, oder ein Kennzeichen wie `R-1`), sonst 400 mit Vorschlag und nichts wird gespeichert.
    **KI-Einordnung** (`KiEinordnung.Oberflaechenfunktionen`, nicht Rechner-Funktion): eine Ansichtsfunktion – sie ändert weder Modell noch Rechner und startet kein Programm, sie sagt der Brille nur, was sie beim nächsten Tipp zeigt; es gibt kein MCP-Werkzeug, für die KI gilt sie als verboten (was die Brille des Benutzers zeigt, löst der Mensch aus).
  - **In der Brille:** `/vr` liest den Auftrag beim Laden und danach alle 2 s. Der Knopf zeigt ihn («Auftrag vom PC: Begehen · EG · E.01 Wohnen/Küche» bzw. «Sitzungsraum · 1.OG»). Ein Tipp startet genau das: bei `begehen` am Raum des Auftrags, bei `sitzungsraum` öffnet sich nach dem Start der Sitzungsraum (XR-H, Tablet «Sitzungsraum»; `api.sitzung.betreten()`) mit der gewählten Etage (`aktion('g:<Kennzeichen>')`) – die vorhandene Funktion von `xr-sitzung.js`, nichts nachgebaut.
    Ohne Auftrag gilt die Reihenfolge letzter Standpunkt › EG › 3D. Ein ausgeführter Auftrag ist verbraucht (Nummer im `localStorage` der Brille) und kommt nach dem Neuladen nicht wieder.
  - **Live:** Kommt in der Sitzung ein Auftrag mit höherer `nr`, wendet ihn die Seite sofort an, ohne neuen Tipp: anderer Raum → Standpunkt wechseln (`vr.versetzen(standort)` in `vr3d.js`, ein offener Sitzungsraum schliesst sich), Begehen ↔ Sitzungsraum wechseln, Etage im Sitzungsraum setzen, Ausschnitt bzw. 3D-Ansicht auf den Tisch bringen (ein einfacher Auftrag «Sitzungsraum» im offenen Sitzungsraum zeigt wieder das ganze Modell). Dazu ein Hologramm-Etikett «Neuer Auftrag vom PC» (`vr.hinweisen`, etwa 1 m vor dem Kopf, 2 s).
  - **Nicht gebaut:** Der Auftrag im Tooltip der Statusanzeige «Brille» (`quest-status.js`, ein anderer Agent arbeitet an `quest-*`).
- **Ende der Sitzung** (Quest-Menü, Tablet «VR beenden»): zurück auf der Startseite mit «Eintreten», nicht in der PC-Oberfläche. Der Kopfstand und die Blickrichtung am Ende werden in `vr.letzter_standpunkt` gemerkt; das nächste Eintreten, auch nach neuem Laden, beginnt dort.
  Anschluss in `vr3d.js`: kleiner Rückruf `api.beiEnde({ x, z, boden, gieren })` nach einer gelaufenen Sitzung.
- **Icon in der Quest anlegen** (ohne Gewähr: die Menünamen des Quest-Browsers können sich ändern):
  1. In der Quest den Browser öffnen und `http://localhost:<Port>/vr` aufrufen (Port steht im Dialog «Quest verbinden»; dort ist die Adresse schon mit `/vr` angegeben). Die Seite muss einmal laden.
  2. Im Menü des Browsers (drei Punkte oben rechts) **«Zur Startseite hinzufügen»**, **«Zum Startbildschirm hinzufügen»** oder **«Als App installieren»** wählen (je nach Version; steht nichts davon da, die Seite als **Lesezeichen** speichern und über die Lesezeichen öffnen).
  3. Das Icon (Cyan, VR-Brille auf dunklem Grund, Name «KICAD VR») erscheint in der Mediathek der Quest bzw. in den Lesezeichen. Ein Tipp darauf öffnet `/vr`.
  Voraussetzung bleibt die Weiterleitung `adb reverse` (Dialog «Quest verbinden») bzw. die WLAN-Verbindung dazu; ohne sie ist `localhost` in der Brille nicht erreichbar.
- **Web-App-Manifest** `vr.webmanifest`: Name «KICAD VR», `start_url` `/vr`, `display` `standalone`, Symbole `vr-symbole/vr-192.png` und `vr-512.png` (eigene Zeichnung, erzeugt mit `werkzeuge/vr_symbol_erzeugen.mjs`).
- **Dialog «Quest verbinden»:** die anzugebende Adresse ist `http://localhost:<Port>/vr` (nur Text in `quest-text.js`: `urlText`, `HINWEISE`).
- **Prüfhilfen:** `#vr-eintreten[data-status]` (`laedt`, `bereit`, `startet`, `aktiv`, `kein-projekt`, `fehler`) und `[data-startquelle]` (`parameter`, `gemerkt`, `eg`, `gesamt`); Test-Naht `import('/js/vr-start.js').naht`.
- **Prüfen:** Node `node --test src/Hlks.Web/wwwroot/js/vr-start.test.mjs` (Parameter, Startpunktwahl, Merken, Erdgeschoss, Anzeige, Manifest); Web `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~VrSeiteTests"` (Auslieferung, Manifest, Ursprungsschutz);
  Browser `node src/Hlks.Web/wwwroot/js/vr-start.browser.mjs` (Port 5680, CDP 9680; `navigator.xr`-Attrappe, Beispielprojekt «MFH Musterweg», Bilder `vrstart-*.png` in `%TEMP%\HLKS_CAD\vrstart`).
  **Nur mit der Brille prüfbar:** Tipp mit dem Controller bzw. Pinch startet wirklich (Benutzeraktion im Quest-Browser), Icon anlegen (Menünamen), Rückkehr auf die Startseite nach dem Quest-Menü, Gefühl des Startpunkts (Raummitte, Blickrichtung). **Anleitung:** Kapitel «zusatz» in `berichte/anleitung/inventar.json`: `zb-xr-vr-start`.

## Modul XR: Autostart (Begehung beim Aufsetzen; Christian Steiner 21.10.2026: «ich möchte nach dem klick am pc nur noch die brille anziehen, und dann startet die begehung direkt»)

Nach dem Klick am PC (Kontextmenü «In VR begehen (Brille)» bzw. «Geschoss im Sitzungsraum zeigen (Brille)», 3D «An die Brille senden») öffnet KICAD die VR-Startseite im Browser der Quest und klickt dort selbst auf «Eintreten», sobald die Brille aufgesetzt ist.
Keine Logik in der Oberfläche: alles macht der Server (`QuestStarter.cs`); die Oberfläche setzt den Auftrag mit `autostart: true` und zeigt den Zustand.

- **Einstellung** `quest.autostart` (Ja/Nein, **Vorgabe ja**). Im Dialog «Quest verbinden» der Schalter **«Begehung beim Aufsetzen automatisch starten»** mit dem Hinweis auf die einmalige Bestätigung. Ist die Einstellung aus, geschieht nichts (der Auftrag wird trotzdem gesetzt; die Brille holt ihn mit dem Tipp wie bisher).
- **Auftrag:** `POST /api/vr/auftrag` nimmt zusätzlich `autostart` (bool) und antwortet mit `autostart: gestartet | aus | nicht_angefordert | nicht_verfuegbar`. Der Server startet den Autostart nach dem Speichern, **nur auf diesen Auftrag des Menschen**, nie aus dem Hintergrund-Takt. Ein Autostart zur Zeit: ein neuer Auftrag bricht den alten ab (der alte entfernt seine Weiterleitung, dann beginnt der neue).
- **Ablauf** (Phasen `verbinden` › `browser` › `aufsetzen` › `debugging` › `klick`):
  1. Brille verbinden wie die Automatik (Weg Weiterleitung › WLAN › USB, Weiterleitung für den KICAD-Port sicherstellen; `QuestDienst.BrilleFuerStart`, unter der Sperre des Quest-Verbinders).
  2. `adb -s <Serie> shell am start -a android.intent.action.VIEW -d http://localhost:<Port>/vr com.oculus.browser` (feste Argumente; der Port ist der des Servers).
  3. Warten, höchstens 10 min, alle 1,5 s: `dumpsys power` meldet `mWakefulness=Awake` **und** `dumpsys activity activities` nennt für `mResumedActivity` bzw. `topResumedActivity` das Paket `com.oculus.browser`. Reisst adb über WLAN ab (Ruhezustand), wird wie in 1 neu verbunden.
  4. `adb forward tcp:<freier lokaler Port> localabstract:chrome_devtools_remote` (der Port wird von KICAD gewählt, nur 127.0.0.1) und CDP über `ws://127.0.0.1:<Port>/devtools/browser`: Ziel ist nur ein Tab, dessen URL mit `http://localhost:<Port>/vr` beginnt (danach nichts oder `/ ? #`; bei mehreren der zuletzt geöffnete).
     Gibt es keinen, geht einmal `Page.navigate` auf genau diese Adresse im ersten Tab. Warten (höchstens 60 s), bis `#vr-eintreten` den Status `bereit` hat; dann **ein Klick** (`Runtime.evaluate` mit `userGesture: true`). Steht der Status schon auf `aktiv`, wird nicht geklickt.
     Nach 10 s ohne `aktiv` ein zweiter Versuch mit `Input.dispatchMouseEvent` in der Mitte des Knopfs; steht der Status auf `startet` (die Brille fragt «VR erlauben»), wird stattdessen auf den Menschen gewartet.
  5. Am Ende (auch bei Fehler, Zeitgrenze, Abbruch): `adb forward --remove tcp:<Port>`.
- **Einmalige Freigabe:** Beim ersten Mal fragt der Browser in der Brille, ob die Seite VR nutzen darf («VR erlauben»). Das muss der Mensch einmal bestätigen; danach startet es von selbst.
- **Rückmeldung am PC:** Statusleiste, Anzeige «Brille» (`quest-status.js`, Texte in `quest-text.js`): «Brille aufsetzen – die Begehung startet automatisch» (gelb) › «Begehung läuft» (grün) bzw. «Begehung nicht gestartet: …» (grau, Hinweis im Tooltip). `GET /api/quest/autostart` → `{ autostart, status: leer | laeuft | aktiv | fehler | abgebrochen, phase?, meldung?, hinweis?, nr, port, seit?, ende? }`; die Oberfläche fragt alle 2 s, solange er läuft.
- **Sicherheit** (Dateikopf von `QuestStarter.cs`): ausgewertet werden nur drei feste Ausdrücke (Status lesen, Knopf klicken, Mitte lesen), nie Text aus Anfragen oder aus der Brille; nur fünf CDP-Methoden (`Target.getTargets`, `Target.attachToTarget`, `Page.navigate`, `Runtime.evaluate`, `Input.dispatchMouseEvent`); nur Tabs mit der genauen `/vr`-Adresse; Fern-Debugging nur für die Dauer des Autostarts;
  jede CDP-Anfrage (5 s) und jeder adb-Aufruf (10 s) hat eine Zeitgrenze; WebSocket über `System.Net.WebSockets.ClientWebSocket`. **KI:** `KiEinordnung.RechnerFunktionen` › `quest_autostart` («steuert den Browser der Brille»), kein MCP-Werkzeug.
- **Prüfen:** Web `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~QuestAutostart"` (Attrappe `tests/Hlks.AdbAttrappe` mit `am start`, `forward`, `dumpsys power`/`activity`; CDP-Attrappe als kleiner WebSocket-Server in `QuestAutostartTests.cs`; nie ein echtes adb, nie die Brille); Node `node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest.test.mjs`.
  **Nur mit der Brille prüfbar:** ob `mWakefulness`/`mResumedActivity` auf der Quest 3 beim Aufsetzen so wechseln, ob der Klick per CDP im Quest-Browser als Benutzeraktion für `requestSession` genügt (sonst Mausklick), die Bestätigung «VR erlauben», ob `am start` bei offener Seite einen zweiten Tab öffnet. **Anleitung:** `zb-xr-autostart` in `berichte/anleitung/inventar.json`.

### «Brille automatisch verbinden» (MAC statt IP; Christian 21.10.2026: «dann müsste nur einmal eingerichtet werden»)

Die Quest wird über ihre **WLAN-MAC-Adresse** gefunden und ohne Klick verbunden. Die IP-Adresse ändert sich (DHCP), die MAC nicht. Einmal mit Kabel verbunden, merkt sich KICAD die Brille; danach genügt WLAN.
Auch dies ist eine reine Rechner-Funktion: **für die KI verboten** (`KiEinordnung.RechnerFunktionen`, Eintrag `quest_automatik`), kein MCP-Werkzeug.

- **Merken:** Bei jedem erfolgreichen «Verbinden», «Auf WLAN umstellen» und «Über WLAN verbinden» liest KICAD die MAC der Quest (`adb -s <Serie> shell ip link show wlan0`, Zeile `link/ether …`, nur `aa:bb:cc:dd:ee:ff` in Kleinbuchstaben; `00:00:…`, `02:00:00:00:00:00`, `ff:ff:…` und Gruppenadressen werden verworfen)
  und speichert sie je Benutzer als Einstellung **`quest.brillen`**: Liste `{ seriennummer, mac, letzteIp, zuletzt }` (höchstens 8; eine ungültige MAC lehnt der Server beim Speichern ab und ignoriert sie beim Lesen). Die USB-Seriennummer bleibt erhalten, wenn die Brille später über `ip:5555` verbunden wird.
- **Schalter:** Einstellung **`quest.automatisch`** (Ja/Nein, Vorgabe nein). Im Dialog «Quest verbinden» der Schalter **«Brille automatisch verbinden»** und darunter die gemerkte Brille (Seriennummer, MAC, letzte IP) mit dem Knopf **«Vergessen»**. Ohne die Einstellung läuft nichts im Hintergrund.
- **Ein Schritt** (`POST /api/quest/automatik { anlass }`, `QuestDienst.Automatik`, unter der Sperre, idempotent; ist die Sperre belegt, antwortet er `sucht`): (1) ist eine Quest schon `device` mit Weiterleitung, geschieht nichts; (2) besteht ein WLAN-Weg, wird nur die Weiterleitung gesetzt;
  (3) ist eine USB-Quest da, aber kein WLAN-Weg, stellt KICAD selbst auf WLAN um («Kabel kann ab»; geht das nicht, wenigstens die Weiterleitung über das Kabel); (4) sonst gemerkte MAC → IP → `adb connect` → Weiterleitung. Nicht gefunden: Status `nicht_gefunden` mit dem Hinweis,
  dass nach einem Neustart der Brille einmal das Kabel nötig ist (`adb tcpip` gilt nicht über den Neustart). Antwort: `{ automatisch, status: aus | kein_adb | bereit | sucht | nicht_gefunden | nicht_freigegeben | fehler, meldung?, hinweis?, ping: nein | gelaufen | gesperrt | keine_netze, ip?, seriennummer?, brille?, port, url }`.
- **IP über die MAC** (`QuestNetz.cs`, Naht `IQuestNetz`): zuerst die **Nachbartabelle** des PCs (`GetIpNetTable2` aus iphlpapi, nur lesend, keine Shell, kein Programm). Steht die MAC nicht darin oder gelingt `adb connect` nicht, folgt auf Anlass ein **Ping-Durchlauf** (`System.Net.NetworkInformation.Ping`,
  300 ms je Ping, höchstens 32 zugleich) **nur im eigenen /24-Netz** der echten LAN-/WLAN-Schnittstellen mit privater Adresse (10/8, 172.16/12, 192.168/16); VPN-, TAP-, Tunnel-, virtuelle Adapter (Hyper-V, WSL, VMware, Docker …) und Link-lokale Adressen bleiben aus; danach wird die Tabelle nochmals gelesen.
  **Höchstens ein Ping-Durchlauf je 2 Minuten**, nie laufend. Im Testlauf (`HLKS_TEST`, Testrunner) ist die Naht leer: weder echte Tabelle noch echte Pings.
- **Anlässe** (`anlass`): `takt` (nur `adb devices` und Nachbartabelle, nie ein Ping-Durchlauf), `start`, `ansicht3d`, `klick` (dürfen einen Ping-Durchlauf machen). Ist die Einstellung aus, antwortet der Server `aus` und startet nichts – ausser bei `klick` (ein ausdrücklicher Klick des Menschen).
- **Statusanzeige «Brille» in der Statusleiste** (`quest-status.js`, im Mittelteil neben «Bibliothek»): sichtbar nur, wenn die Automatik an ist oder eine Brille gemerkt ist. **Grün** «Brille bereit», **gelb** «Brille: sucht …», **grau** «Brille: nicht gefunden» (auch «adb fehlt», «nicht freigegeben», «Fehler», «Automatik aus»).
  **Klick** = ein Schritt mit Ping-Durchlauf (Anlass `klick`), **Rechtsklick** öffnet den Dialog «Quest verbinden». Die Oberfläche ruft den Schritt auf: beim Start (`start`), beim Aktivieren einer 3D-Ansicht (`ansicht3d`) und, solange nicht grün, alle 10 s (`takt`); bei Grün ruht der Takt. Die Entscheidungen stehen in `quest-text.js`
  (`brilleAnzeige`, `taktNoetig`, `schrittErlaubt`); die Oberfläche entscheidet nie über Pings.
- **Schnittstelle:** `GET /api/quest` liefert zusätzlich `automatisch` und `brillen`. Neue Einstellungen mit Prüfung in `BenutzerEinstellungen.Bekannt`: `quest.automatisch` (true/false) und `quest.brillen`.
- **Prüfen:** Web `--filter "FullyQualifiedName~QuestTests"` (Netz-Attrappe für Tabelle und Pings, `Hlks.AdbAttrappe` mit `mac.txt` und `verbindbar.txt`: MAC gemerkt, ungültige MAC verworfen, Tabelle → verbunden, Ping nur auf Anlass und einmal je 2 min, USB → WLAN, Einstellung aus, VPN/TAP ausgelassen);
  Node `quest.test.mjs` (Statuslogik); Browser `quest.browser.mjs` (Statusanzeige, Schalter, Vergessen). **Nur mit echter Brille und echtem Netz prüfbar:** die echte Nachbartabelle (`GetIpNetTable2`), der Ping-Durchlauf im Büronetz, `ip link show wlan0` der Quest, `adb connect` nach einem Neustart.
  **Anleitung:** `zb-xr-quest-automatisch` in `berichte/anleitung/inventar.json`.

## Modul XR: Zugang im Büronetz für die Quest App (Quest App P3; Entscheid Christian 23.10.2026: «Sofort WLAN mit Kopplung und TLS»)

Die native Quest App (`src/Hlks.Quest`) verbindet sich direkt über das Büro-WLAN mit KICAD am PC, ohne adb. Dafür startet KICAD **neben** dem
bisherigen Server einen eigenen kleinen HTTPS-Server. Der bisherige Server bleibt unverändert nur auf `http://localhost:<Port>` mit Herkunftsschutz.
Oberfläche dazu folgt mit P6 («XR-Labor») und P7 («Quest verbinden»: App installieren mit Kopplung, Brille in der 3D-Ansicht); hier steht die Schnittstelle.
**KI:** reine Rechner-Funktionen, verboten (`KiEinordnung.RechnerFunktionen` › `xr_zugang`, `xr_kopplung`, `xr_kanal`), kein MCP-Werkzeug.

- **Ein/Aus je PC** (Christian 23.10.2026: «Je PC»): Schalter **`an`** (Ja/Nein, **Vorgabe aus**), dazu `port` (Vorgabe 5140) und `suchPort` (Vorgabe 5141), je 1024–65535,
  in der Datei **`%LOCALAPPDATA%\HLKS_CAD\xr\zugang.json`** dieses PCs (`XrPcEinstellung`) – **keine Benutzereinstellung** (die gilt über OneDrive auf allen Rechnern).
  Gesetzt mit `POST /api/xr/zugang { an, port?, suchPort? }`; fehlt oder klemmt die Datei, ist der Zugang aus. Nur das KICAD auf dem **Desktop-Port 5130** öffnet den Zugang;
  Abnahmeserver (5150–5153) und Testserver (5131) auf demselben PC nie, auch wenn der Schalter an ist. Schalten wirkt ohne Neustart (sonst wird die Datei alle 10 s
  nachgelesen; geänderte Ports starten den Zugang neu). Ist ein Port belegt, bleibt der Zugang aus und `GET /api/xr/zugang` nennt den Grund; die Oberfläche läuft weiter.
- **Netzzugang** (`XrZugang.cs`): HTTPS auf allen IPv4-Adressen, Port 5140, TLS 1.2/1.3, nur HTTP/1.1, Zertifikat von KICAD. Er kennt **nur** Endpunkte des
  Hauptservers mit der Marke `.XrImNetz()` und einem Muster unter `/api/xr/` – sonst 404 (keine Oberfläche, keine Dateien, kein übriges `/api`).
  **Pforte** (`XrPforte.Pruefen`, eine Stelle): Pfad ausserhalb `/api/xr` → 404; Absender gesperrt oder zu viele Anfragen → 429; Schlüssel fehlt oder falsch → 401
  (`WWW-Authenticate: Bearer`; ohne Schlüssel nur `kopplung/anfrage`, `kopplung/aufdecken` und `kopplung/abholen`); Endpunkt ohne Marke (alle PC-Endpunkte) → 404;
  Körper > 64 KB → 413; nicht `application/json` → 415. Grenzen: 32 Verbindungen, Kopfzeilen 16 KB in 10 s, je IP 20 Anfragen/s (Vorrat 60), je IP 10 Fehlversuche (falscher oder fehlender Schlüssel) auf einmal, danach einer je Minute (sonst 429).
  **Freigegeben** sind `verbindung`, `kanal`, die drei Kopplungsschritte und das **Modell als GLB** (P2: `GET /api/xr/geschosse`, `GET /api/xr/geschoss/{id}.glb`, mit Schlüssel,
  ETag/304 wie auf localhost). **Neue Endpunkte für die Brille** unter `/api/xr/` anlegen und mit `.XrImNetz()` markieren; ohne Marke bleiben sie nur auf localhost.
  **Komprimierung:** nur `model/gltf-binary`, mit Brotli oder gzip je nach `Accept-Encoding` der App (schnellste Stufe); JSON – etwa das Paket mit dem Schlüssel – nie
  (über HTTPS unbedenklich, weil die GLB weder Geheimnisse noch Text des Absenders enthalten). Der localhost-Server komprimiert GLB wie IFC-Netze mit gzip (nur für fremde Rechner).
- **Zertifikat** (`XrZertifikat.cs`): ECDSA P-256, selbst erzeugt, 10 Jahre, je PC in `%LOCALAPPDATA%\HLKS_CAD\xr\zertifikat.dpapi` (privater Schlüssel mit DPAPI
  für den Benutzer verschlüsselt; nie OneDrive, nie Repository). **Fingerabdruck** = SHA-256 über das Zertifikat (DER), 64 Kleinbuchstaben hex; Kurzform für den
  Menschen «AB12 CD34 EF56 7890». Wird es ersetzt (Datei unlesbar, Ablauf in < 30 Tagen), meldet `zertifikatErsetzt` es: Brillen neu koppeln.
  Geladen legt Windows den Schlüssel als Datei unter `%APPDATA%\Microsoft\Crypto\Keys` ab (SChannel braucht das); KICAD gibt sie beim Beenden wieder frei (`XrNetzDienst.StopAsync`).
- **Kopplung** (`XrKopplung.cs`, je Brille, höchstens 8; eine neue derselben Brille ersetzt die alte). Schlüssel: 32 Zufallsbyte, base64url (43 Zeichen); die App
  sendet `Authorization: Bearer <kopplung>.<schluessel>`. Gespeichert (`%LOCALAPPDATA%\HLKS_CAD\xr\kopplungen.json`) werden nur
  Prüfwert = HMAC-SHA256(Schlüssel, `hlks-xr-zugang`) und Suchschlüssel = HMAC-SHA256(Schlüssel, `hlks-xr-suche`) (DPAPI), nie der Schlüssel.
  - **Weg A** (bei der Installation per Kabel, Knopf in P7): `POST /api/xr/kopplung/paket { brille, name? }` → `{ paket: { art: "hlks-xr-kopplung", fassung: 1, pc, ips[],
    port, suchPort, fingerabdruck, kopplung, schluessel, brille, name, angelegt } }` (`Cache-Control: no-store`; nur einmal ausgeben, per adb in die App, nie speichern).
  - **Weg B** (Ersatz ohne Kabel; Christian 23.10.2026: «Nur vergleichen»): Brille und PC zeigen denselben **6-stelligen Vergleichscode**; in der Brille wird nichts
    eingetippt, der Mensch vergleicht und **bestätigt nur am PC**. Der Code entsteht aus dem Fingerabdruck des Zertifikats und je einer Zufallszahl von Brille und PC
    (Short Authentication String); wer dazwischen sitzt, kann der Brille nur ein anderes Zertifikat zeigen, sie sieht also einen anderen Code.
    1. Brille `POST /api/xr/kopplung/anfrage { brille, name?, verpflichtung }` mit verpflichtung = base64url(SHA-256(zufallBrille)), zufallBrille = 32 Zufallsbyte
       → `{ anfrage, zufallPc, bis, versucheMax }` (zufallPc: 32 Byte, base64url).
    2. Brille `POST /api/xr/kopplung/aufdecken { anfrage, zufallBrille }` → 200 `{ status: "vergleichen" }` | 403 `falsch` (passt nicht zur Verpflichtung, mit `versucheUebrig`)
       | 409 `schon_aufgedeckt` | 410 `gesperrt`. Der Code steht nie in einer Antwort an die Brille.
    3. **Code** = SHA-256 über `hlks-xr-vergleich|1|<fingerabdruck>|<zufallBrille>|<zufallPc>` (UTF-8, Zufallszahlen als base64url), die ersten 8 Byte big-endian als
       vorzeichenlose Zahl modulo 1 000 000, sechsstellig. Die Brille setzt den SHA-256 des Zertifikats ein, das **sie** in TLS sieht; KICAD den eigenen
       (`XrKopplungen.Vergleichscode`). Der PC zeigt den Code ab dem Aufdecken (`GET /api/xr/zugang` › `anfragen[].code`, vorher null), daneben der Name der Brille.
    4. Am PC `POST /api/xr/kopplung/bestaetigen { anfrage }` (erst wenn ein Code da ist, sonst 409) bzw. bei verschiedenen Codes `…/ablehnen`.
    5. Brille `POST /api/xr/kopplung/abholen { anfrage }` → 202 `wartet` | 200 `{ status: "gekoppelt", paket }` (einmal) | 403 `abgelehnt` | 404 `unbekannt` |
       409 `aufdecken` | 410 `abgelaufen`. Die App prüft, dass `paket.fingerabdruck` dem in TLS gesehenen gleicht, und pinnt ihn ab dann.
    Frist 5 min, 5 falsche Aufdeckungen, höchstens 3 offene und 20 neue Anfragen je Stunde; aufdecken und abholen nur von der anfragenden IP-Adresse. Die Verpflichtung
    vor dem Aufdecken verhindert, dass jemand dazwischen die Zufallszahlen so wählt, dass beide Codes gleich werden (Trefferchance 1 : 1 000 000 je Anfrage).
  - **Widerruf:** `POST /api/xr/kopplung/widerrufen { kopplung }` → `{ widerrufen }`; der Kanal dieser Brille wird sofort getrennt (Code 4001).
- **Suche** (`XrSuche.cs`, UDP 5141, nur bei eingeschaltetem Zugang): Die Brille ruft per Broadcast `{ typ: "suche", fassung: 1, kopplung, zeit (Unix-ms), zufall (base64url,
  16 Byte), signatur }` mit signatur = HMAC-SHA256(Suchschlüssel, `hlks-xr-suche|1|<kopplung>|<zeit>|<zufall>`), base64url. KICAD antwortet nur bei gültiger Signatur,
  Zeit ±60 s, neuem `zufall`, höchstens 10 je Absender (dann 1/s), und nur an den Absender: `{ typ: "gefunden", fassung: 1, pc, ips[], port, fingerabdruck, zufall, signatur }`,
  signatur = HMAC-SHA256(Suchschlüssel, `hlks-xr-gefunden|1|<zufall>|<pc>|<port>|<fingerabdruck>|<ips mit Komma>`). KICAD sendet nie von selbst.
- **Live-Kanal** (`XrKanal.cs`, Schema `XrNachrichten.cs`): `wss://<PC>:5140/api/xr/kanal` mit Schlüssel; Textnachrichten, je ein JSON-Objekt mit `typ` und `fassung` (1).
  Positionen in **mm im Koordinatensystem des KICAD-Modells** (x, y waagrecht, z oben), Drehungen als Quaternion `[x, y, z, w]` im selben System; die App rechnet um.

  | Richtung | typ | Inhalt |
  | --- | --- | --- |
  | Brille → PC | `hallo` | `name`, `geraet` (je ≤ 40), `app?` – zuerst, innert 10 s |
  | PC → Brille | `willkommen` | `id` (Teilnehmer-Id «t…»), `teilnehmer[{ id, name, geraet }]`, `auftrag` (aktueller VR-Auftrag oder null), `stellschrauben{}`, `symbole` (V1a: Symbolsatz des Benutzers, `strich` oder `revit`, aus `oberflaeche.symbole`) |
  | PC → andere | `hallo` / `tschuess` | `id`, `name`, `geraet` / `id` |
  | Brille → PC | `pose` | `kopf{ pos[3], rot[4] }`, `links?`/`rechts?{ gelenk{ pos, rot }, zeigefinger[3], pinch, pinchStaerke? }`, `modus?` (begehen \| sitzungsraum), `massstab?` |
  | PC → andere | `pose` | wie oben plus `von`, `zeit` (Unix-ms); höchstens 30 je Sekunde und Teilnehmer weitergereicht (die letzte gilt für den PC) |
  | Brille → PC | `zeigen` | `element` (Id oder Kennzeichen, null = nichts), `punkt?[3]` – für «Claude sieht mit»; an die anderen mit `von`, `zeit` |
  | Brille → PC | `messprotokoll` | `aufgabe`, `variante?`, `zeitMs`, `fehlgriffe`, `abbrueche`, `erfolg?`, `stellschrauben?{}`, `notiz?` → Antwort `quittung { bezug, nr }` |
  | PC → Brille | `auftrag` | `auftrag{ modus, raum?, geschoss?, ausschnitt, ansicht?, nr, zeit }` – sofort, wenn `POST /api/vr/auftrag` ihn setzt |
  | PC → Brille | `stellschraube` | `schluessel`, `wert` (Zahl, Ja/Nein, Text ≤ 200; null = zurückgesetzt) |
  | PC → Brille | `fehler` | `meldung`, `bezug?` – ungültige Nachricht; die Verbindung bleibt |
  | Brille → PC | `tschuess` | – Verbindung endet (1000) |

  Grenzen: eine Verbindung je Kopplung (neue ersetzt alte: Code **4002**), höchstens 8 Teilnehmer (1013), Nachricht ≤ 16 KB (1009), nur Text (1003), 60 Nachrichten/s,
  bei Flut oder > 100 ungültigen getrennt (1008), Sendeschlange 256 (zu langsam: **4003**), Widerruf **4001**, Zugang aus **1001**. Der Server reicht nie Rohtext weiter,
  nur geprüfte, neu gebaute Nachrichten. **Messprotokolle:** je Verbindung eine Datei JSON-Zeilen `%LOCALAPPDATA%\HLKS_CAD\xr\messprotokolle\<JJJJMMTT-hhmmss>_<Teilnehmer>.jsonl` (≤ 5 MB).
- **Für den PC** (nur localhost, Herkunftsschutz): `GET /api/xr/zugang` → `{ an, schalter (Pfad von zugang.json), aktiv, adresse, port, suchPort, sucheAktiv, seit?, pc, ips[],
  fingerabdruck, fingerabdruckKurz, zertifikatBis, zertifikatErsetzt, fehler?, hinweis?, kopplungen[{ id, brille, name, weg, angelegt, zuletzt?, verbunden }],
  anfragen[{ anfrage, brille, name, ip?, code (null bis aufgedeckt), bis, bestaetigt, abgelehnt, versuche }], teilnehmer }`; `POST /api/xr/zugang { an, port?, suchPort? }`;
  `GET /api/xr/teilnehmer` → `{ teilnehmer[{ id, name, geraet, app?, kopplung, brille,
  brillenName, seit, pose?, poseZeit?, zeigen?, zeigenZeit?, posen, weitergereicht, verworfen }], stellschrauben{} }` (P7: Brille als Figur, etwa 5–10 Abfragen je Sekunde);
  `POST /api/xr/stellschraube { schluessel, wert, teilnehmer? }` → `{ gesendet, stellschrauben }` (ohne `teilnehmer`: an alle und für neue gemerkt).
  Für die Brille zusätzlich `GET /api/xr/verbindung` (Probe mit Schlüssel → `{ ok, fassung, pc, kopplung, zeit, teilnehmer }`).
- **Was die App tun muss** (P4): Paket speichern (Schlüssel im Android-Keystore bzw. privat im App-Ordner); jede Verbindung nur annehmen, wenn SHA-256 des Serverzertifikats
  gleich `fingerabdruck` ist (`ServerCertificateCustomValidationCallback` bzw. `RemoteCertificateValidationCallback`, keine Kette); PC suchen: zuerst die gespeicherten
  `ips`, sonst Broadcast an `suchPort` und die Antwort mit dem Suchschlüssel prüfen; Kanal öffnen, `hallo`, Posen 20–30 je Sekunde; nach 4001/4002 nicht neu verbinden.
  Weg B: beim ersten Kontakt das Zertifikat vorläufig annehmen und seinen SHA-256 merken, Zufallszahl erzeugen, anfragen, aufdecken, den Code selbst rechnen und gross
  zeigen («Am PC denselben Code bestätigen»), abholen bis 200; nur übernehmen, wenn `paket.fingerabdruck` gleich dem gemerkten ist. GLB mit `Accept-Encoding: br, gzip`
  und `If-None-Match` holen.
- **Firewall:** KICAD ändert nur auf Knopfdruck («Netz einrichten …», Abschnitt «H+K XR (Empfänger-App), E1 – PC-Seite», Unterabschnitt «Netz einrichten per Knopf»). `werkzeuge/xr_firewall.ps1` (als Administrator, von Christian; ersatzweise) legt zwei eingehende Regeln an: TCP 5140 und UDP 5141, nur Profil
  «Privat», nur aus dem eigenen Subnetz; `-Entfernen` nimmt sie weg, `-Liste` zeigt Regeln, Netzprofil und Sperren für dotnet.exe.
- **Netz einrichten per Doppelklick** (Christian 26.10.2026: «mache ein skript, dass der nutzer auslösen kann, um die beiden hinweise zu erfüllen»): `werkzeuge\xr_netz_einrichten.cmd` zeigt die Netzverbindungen, stuft nach Rückfrage (J/N) und Windows-Bestätigung (Administrator) die öffentlichen Verbindungen über echte Netzwerkkarten (WLAN/LAN; VPN und virtuelle bleiben) als «Privat» ein und ruft `xr_firewall.ps1` auf (Regeln für KICAD und XR-Labor). `-Trocken` zeigt nur, `-OhneFirewall` nur das Netzprofil. Die Hinweise im Teil «Quest App» bzw. «Brille» nennen das Skript (`QuestAppNetz.EinrichtenSkript`).
- **Prüfen:** Web `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~Hlks.Web.Tests.Xr"` (Netzzugang echt mit TLS, aber nur auf 127.0.0.1 mit freien Ports).
  **Nur mit Brille und Büronetz prüfbar:** Erreichbarkeit über WLAN und Firewall, Broadcast im Büronetz, Pinning in der App, Dauerlast 30 Posen/s über WLAN.
  **Anleitung:** `zb-xr-zugang` in `berichte/anleitung/inventar.json`.

## Modul XR: Quest App in KICAD (Quest App P7, `entwurf/modul-xr.md`, Zeile P7; Entscheide Christian 23.10.2026: «KICAD installiert die App», «PC sieht Brille»)

*Stand vor H+K XR E1 (26.10.2026): Seitdem heisst die App «H+K XR» (`ch.hkplanung.xr`, eine App für KICAD und Labor), die Ablage `…\hk_xr`, der Eingang `kopplung_<werkzeug>.json` und der Knopf «H+K XR installieren …»; siehe Abschnitt «H+K XR (Empfänger-App), E1 – PC-Seite» am Ende. Wo unten `ch.hkplanung.kicad.quest`, `quest_app` oder `kopplung.json` steht, gilt dieser Abschnitt.*

Der Dialog **«Quest verbinden»** (Register **XR › Gruppe Brille › «Quest verbinden …»** bzw. **«App installieren …»**) hat jetzt einen Teil **«Quest App»** (zwischen «Begehung beim Aufsetzen» und «Gut zu wissen»). Er ersetzt den Ablauf von Hand
(`werkzeuge/quest_brillentest.ps1`) durch Knöpfe. Der Browser-Weg darüber bleibt unverändert. **Reine Rechner-Funktionen** (kein Befehl, keine Abfrage des Kerns, kein Paket im Modell), für die KI **verboten**
(`KiEinordnung.RechnerFunktionen` › `quest_app`, `quest_app_netz`; die Anzeige der Brille am PC fällt unter `xr_kanal`), kein MCP-Werkzeug.

- **Netzzugang ein/aus:** Zeile mit Lampe (grün = aktiv, rot = eingeschaltet aber nicht aktiv, grau = aus), Port, Fingerabdruck in Kurzform («AB12 CD34 EF56 7890») und den Adressen des PCs; Knopf **«Netzzugang einschalten/ausschalten»**
  (`POST /api/xr/zugang { an }`, Schalter dieses PCs wie im Abschnitt «Zugang im Büronetz»). Der Zugang öffnet sich nur im KICAD auf Port 5130 (Desktop); sonst steht der Grund in der Zeile.
- **Hinweise zu Netz und Firewall (Lesen: nur lesend; Ändern seit 26.10.2026 nur über den Knopf «Netz einrichten …»):** `GET /api/quest/app/netz`. KICAD liest mit einem **festen PowerShell-Skript** (`Get-NetConnectionProfile`, `Get-NetFirewallRule -Group 'KICAD XR'`, `Get-NetFirewallPortFilter`; kein Schreibbefehl,
  Zeitgrenze 20 s, 30 s gemerkt, «?neu=1» liest neu) und bewertet (`QuestAppNetz.Bewerten`, getestet): **Warnung**, wenn ein Netz des PCs nicht «Privat» ist (Beispiel: «s-net 3» als «Öffentlich»; die Regeln gelten nur im Profil «Privat»),
  wenn die Regeln der Gruppe «KICAD XR» fehlen oder TCP 5140 nicht erlaubt ist (mit Verweis auf `werkzeuge\xr_firewall.ps1`, das **Christian selbst als Administrator** ausführt – KICAD ändert nie etwas) bzw. wenn keine LAN-/WLAN-Karte mit privater
  Adresse da ist; **Hinweis**, wenn nur UDP 5141 (Suche) fehlt oder Windows nicht lesbar war («über das Kabel geht es trotzdem»); **grün**, wenn alles in Ordnung ist. Im Testlauf wird Windows nicht befragt (`QuestAppSystemLeer`).
- **App installieren/aktualisieren:** Auswahl **«über WLAN»** oder **«über Kabel»** (Vorgabe nach der Brille: am USB-Kabel «über Kabel») und der Knopf (heisst «App aktualisieren», wenn die App in der Brille schon steht). Er ist frei, wenn der Netzzugang
  aktiv ist, die abgelegte App heil ist und eine Brille freigegeben ist; sonst steht darunter der Grund. `POST /api/quest/app/installieren { weg, name? }` (`QuestApp.cs`) macht in dieser Reihenfolge, jeder Schritt erscheint als Zeile:
  1. **zugang** – Netzzugang aktiv? 2. **apk** – abgelegte App heil? 3. **brille** – freigegebene Brille wählen (bei Kabel bevorzugt USB, bei WLAN der WLAN-Weg), Modell (`getprop ro.product.model`) und Seriennummer (`getprop ro.serialno`; sie
  ist auf beiden Wegen dieselbe und bestimmt die Kopplung: eine neue ersetzt die alte derselben Brille) lesen; 4. **installieren** – `adb install -r` der APK (Zeitgrenze 3 min, `QuestEinstellungen.InstallZeitlimit`; Meldungen wie
  `INSTALL_FAILED_UPDATE_INCOMPATIBLE` werden übersetzt, z. B. «App in der Brille deinstallieren»); 5. **kabel** (nur über Kabel) – `adb reverse tcp:5140 tcp:5140`, im Paket steht 127.0.0.1 als erste Adresse (`QuestDienst.AdressenFuerWeg`);
  6. **uebertragen** – Kopplungspaket (Weg A, mit Schlüssel) erzeugen und mit `adb push` nach `/sdcard/Android/data/ch.hkplanung.kicad.quest/files/kopplung.json` legen (die App übernimmt und löscht es); 7. **starten** – `resolve-activity` und `am start -n`.
  **Das Paket mit dem Schlüssel liegt am PC höchstens für die Dauer von `adb push`** in einer Datei in einem eigenen Ordner unter `%TEMP%\HLKS_CAD\quest_app` (danach mit Nullen überschrieben und gelöscht, auch bei Fehlern), steht **in keiner Antwort,
  keinem Protokoll und keiner Ausgabe**. Scheitert die Übertragung, wird die frisch angelegte Kopplung **widerrufen** (kein Schlüssel ohne Brille). Scheitert nur der Start, gilt die Installation (Schritt rot, Meldung «von Hand starten»).
  Antwort immer Status 200: `{ ok, schritte[{ id, text, ok }], meldung?, fehler?, vorschlag?, weg, brille?, name? }`; 400 nur bei unbrauchbaren Eingaben (Weg, Name). Es startet nur `adb.exe` mit festen Argumenten (Regeln wie «Quest verbinden»).
- **Installierte und abgelegte Fassung:** `GET /api/quest/app` zeigt **abgelegt** (`version.json`: Fassung, Commit, Bauzeit, Grösse, SHA-256; die APK wird dagegen geprüft: Datei da, Grösse, Prüfsumme – bei Abweichung steht der Grund da und die Installation ist gesperrt)
  und **in der Brille** (`adb shell dumpsys package ch.hkplanung.kicad.quest`: `versionName`, `versionCode`, `lastUpdateTime`) mit einem Satz zum Vergleich (`nicht_installiert` | `andere_nummer` | `gleiche_nummer` | `unbekannt`;
  gleiche Nummer sagt nichts über den Stand – das zeigt nur der Commit der abgelegten App).
- **Ablage der App:** `werkzeuge\quest_app_bereitstellen.ps1` baut die APK (`dotnet publish src\Hlks.Quest\Projects\Android\Hlks.Quest.Android.csproj -c Release -f net10.0-android`, Bauordner `HLKS_BUILD`) und legt sie mit `version.json`
  nach `%USERPROFILE%\HLKS_CAD_werkzeuge\quest_app\` (kein adb, nichts heruntergeladen; `-Ablage <Ordner>` und `-OhneBau` möglich). Ein anderer Ordner ist als Benutzereinstellung **`quest.app_ablage`** möglich (Feld «Ordner der App» im Dialog;
  vollständiger lokaler Pfad, kein Netzpfad, kein `..`).
- **Gekoppelte Brillen:** Liste aus `GET /api/xr/zugang` › `kopplungen` mit Name, «seit …», verbunden bzw. «zuletzt …» und **«Widerrufen»** (mit Rückfrage; `POST /api/xr/kopplung/widerrufen`, die Brille wird sofort getrennt).
- **Ersatzkopplung (Weg B):** offene Anfragen aus `anfragen` (Name, Kennung, Absender-IP, **6-stelliger Code in Dreiergruppen**, Rest der Frist) mit **«Codes stimmen überein – koppeln»** (erst frei, wenn die Brille aufgedeckt hat und die Frist läuft) und
  **«Ablehnen»**. Bei **mehr als einer offenen Anfrage** steht eine deutliche Warnung (nur bestätigen, wenn der Code genau dem in der eigenen Brille entspricht, im Zweifel alle ablehnen). Namen und Kennungen kommen aus dem Netz und gehen **nur über `esc()`** ins HTML.
  Der Zustand wird alle 2 s gelesen, solange der Dialog offen ist.
- **Aufträge an die App:** Die VR-Aufträge (Rechtsklick auf einen Raum: «In VR begehen (Brille)» usw., 3D-Ansicht: «An die Brille senden») gehen wie bisher an den Server (`POST /api/vr/auftrag`) und von dort **sofort über den Live-Kanal** an jede verbundene App.
  Damit nichts doppelt läuft: Ist eine App im Kanal, antwortet der Server `autostart: "app"` und **startet keinen Browser in der Brille** (der Autostart über `adb am start` und CDP würde über die laufende App dieselbe Begehung ein zweites Mal öffnen);
  die Meldung sagt «Auftrag an die App … die App führt ihn sofort aus». Ohne verbundene App gilt der Weg über den Browser unverändert («Brille aufsetzen, die Begehung startet» bzw. «App starten oder im Browser KICAD VR öffnen»);
  eine später gestartete App holt den Auftrag beim Verbinden (`willkommen.auftrag`).
- **PC sieht die Brille:** Die 3D-Ansicht zeigt jede verbundene Brille der App als einfache Figur an ihrer Pose aus `GET /api/xr/teilnehmer`: **Kopf** (Kugel) mit **Blickkegel und Blicklinie**, **Hände** als Punkte (Handgelenk, Zeigefingerspitze; bei Pinch grösser)
  und der **Name** als Etikett (Farbe je Brille; ohne Pose seit 5 s blass mit «keine Pose seit … s»). Koordinaten: KICAD-mm, z oben, Drehung als Quaternion `[x, y, z, w]` – in three wie alle Körper (x, z, −y)/1000
  (`ansichten/xr-teilnehmer-logik.js`, getestet; Blick ohne Drehung = Norden, wie `KicadUmrechnung`). Die Figuren hängen an der Szene, nicht am Modell: sie zählen nicht beim Einpassen, werden nicht angeklickt und nicht vom Schnittbereich beschnitten;
  sie sind für das Auge auch hinter Wänden sichtbar und bleiben bei jedem Abstand erkennbar (Punktmarken und Etikett mit fester Bildschirmgrösse). In VR im Browser sind sie aus.
  **Abfrage:** ein gemeinsamer Takt für alle offenen 3D-Ansichten – **mit Brille alle 200 ms**, ohne Brille nur alle 4 s ein winziger Blick auf die Liste, **ausgeschaltet oder bei verstecktem Fenster gar keine Anfrage**; ohne offene 3D-Ansicht läuft nichts.
  **Ein/aus:** Register **XR › Brille › «Brille zeigen»** (Aktion `ansicht.brilleZeigen`, Schalter mit Symbol) und das Kästchen im Teil «Quest App»; Benutzereinstellung **`ansicht3d.brillen_zeigen`** (Ja/Nein, Vorgabe ja).
- **Nachtrag nach dem Brillentest (Christian 23.10.2026, Quest 3 am Kabel, Test_praxis):**
  1. **Dialog im Fenster:** Höhe und Breite folgen dem Fenster (`max-height: calc(100vh − 24px)`, `width: min(640px, 100vw − 16px)`), der Inhalt scrollt, Titelleiste und «Schliessen» bleiben stehen; auch mit einer zu grossen gemerkten Lage (`dialog-merken.js`) und in 1280×720, 1024×600 und 800×500 (Browserreihe). Die **Quest App steht jetzt oben**, der alte Teil darunter als einklappbarer **«Browser-Prototyp»** (`<details>`); er klappt von selbst ein, sobald die App in der Brille installiert ist (offen bleibt er ohne App oder ohne adb, dort steht die Einrichtung; ein Klick des Menschen gilt bis zum Schliessen).
  2. **Kabelweg bleibt stehen:** Nach einer erfolgreichen Installation merkt KICAD je PC den Weg (`%LOCALAPPDATA%\HLKS_CAD\xr\quest_app_weg.json`: Kennung der Brille, `kabel`|`wlan`, Port; kein Schlüssel). **Solange die App per Kabel gekoppelt ist und die Kopplung besteht**, stellt die Automatik («Brille automatisch verbinden», Autostart) die Brille **nicht mehr auf WLAN-adb um** (`adb tcpip` startete adbd neu und nahm `adb reverse tcp:5140` weg, die App war getrennt); sie bleibt beim Kabel («Quest App ist per Kabel gekoppelt»). Nach jeder Änderung der adb-Verbindung (Verbinden, «Auf WLAN umstellen», «Über WLAN verbinden», jeder Schritt der Automatik, Lesen des Zustands im Dialog) stellt KICAD **`adb reverse tcp:<Port des Netzzugangs>`** wieder her (USB-Weg zuerst, nur wenn keiner der Wege sie hat; `QuestDienst.AppWeiterleitungSicher`). Der Zustand `GET /api/quest/app` nennt `appWeg: "kabel"`. Wer die App schon vor diesem Nachtrag installiert hat, installiert einmal neu («App aktualisieren»), damit der Weg gemerkt wird.
  3. **VR-Auftrag startet die App statt des Browsers:** Ist keine App im Kanal, aber hat KICAD die App installiert und gekoppelt (gemerkter Weg, bestehende Kopplung; ohne beides kein adb-Aufruf) und ist der Netzzugang an, stellt der Server die Weiterleitung sicher und **startet die App** (`am start -n`, wie bei der Installation; `QuestDienst.AppFuerAuftragStarten`); `POST /api/vr/auftrag` antwortet `autostart: "app_gestartet"`, der Auftrag kommt über den Kanal, sobald sie verbindet (`willkommen.auftrag`). Den Browser-Autostart gibt es nur noch, wenn keine App installiert oder gekoppelt ist.
  4. **Nach dem Widerruf «neu koppeln»:** Eine installierte, aber nicht mehr gekoppelte App (Kennung der Brille aus `getprop ro.serialno` gegen die Kopplungen) zeigt der Teil «Quest App» mit einer roten Meldung; der Knopf heisst **«App neu koppeln»**. Die Meldung nach «Widerrufen» sagt es ebenfalls. (Die Klassen des App-Teils heissen `qa-box` statt `qd-meldung`, damit der alte Teil sie nicht überschreibt.)
- **Prüfen:** Web `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~QuestAppTests"` (Attrappe `tests/Hlks.AdbAttrappe` mit `install`, `push`, `getprop`, `dumpsys package`, `resolve-activity`, `am start -n`; der Netzzugang echt mit TLS auf 127.0.0.1; Windows-Abfrage als Attrappe);
  Node `node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest-app.test.mjs ansichten/xr-teilnehmer.test.mjs`; Browser `node src/Hlks.Web/wwwroot/js/oberflaeche/xr-app.browser.mjs` (Port 5740, CDP 9740; der Netzzugang öffnet sich nur auf Port 5130, darum
  werden `/api/xr/zugang` und `/api/xr/teilnehmer` in der Seite nachgestellt). **Nur mit der Brille prüfbar:** die echte Installation per adb (Entwicklermodus, Freigabe, `install -r` einer 7,5 MB grossen APK über Kabel und WLAN, Scoped Storage beim `push` nach `Android/data`,
  Start der App), Pinning und Verbindung der App nach dem Paket, die echte Firewall und das Netzprofil «Privat» im Büronetz, Genauigkeit und Ausrichtung der Figur gegenüber der echten Haltung (Blickrichtung, Hände), Verhalten bei zwei Brillen.
  **Anleitung:** `zb-xr-quest-app-installieren`, `zb-xr-brille-am-pc`, `zb-xr-kopplungen` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App P2: Modell als GLB je Geschoss (`entwurf/modul-xr.md`, Prototyp Interface-Labor, Zeile P2)

Die native Quest App (StereoKit, `src/Hlks.Quest`) lädt das Modell nicht über `/api/koerper`, sondern als **glTF 2.0 binär (GLB) je Geschoss**, zusammengefasst je Material (Meta, Quest 3: höchstens
1.5 Mio. Dreiecke und 200 Zeichenaufrufe je Bild). **Keine Bedienung am PC** – eine Schnittstelle für die App; die Bedienung folgt mit P4 (App) und P7 («Quest verbinden»), dann auch der Eintrag in der Anleitung.
Kern: `Hlks.Kern/Xr/` (`XrModell`, `XrNetzbau`, `XrFarben`, `XrKoordinaten`, `GlbSchreiber`), Server: `XrModellEndpunkte.cs` (`app.MapXrModell(dienst)`).

- **Quelle der Geometrie:** dieselben Körper wie die 3D-Ansicht (`Grafik.Koerper`, wie `GET /api/koerper`): Architektur (Wände, Decken, Dächer, Türen, Fenster, Stützen, Träger, Treppen, Räume), Haustechnik
  (Leitungen, Formteile, Armaturen, Apparate, Geräte), Familien, Aussparungen, Kollisionsmarken. Teile `netz` (IFC-Unterlage) gehen in den eigenen Teil «unterlage»: der **Hintergrund** der Netzdatei (nicht übernommene
  Produkte, ohne Öffnungen) wie `netzdatei.js` in der Darstellung «nachbau»; ausgeblendete Unterlagen (`anzeigen = false`) fehlen wie im 3D. Die App hat keine Ansicht: alle Gewerke und Klassen.
- **Zerlegung:** Quader und Prismen mit Flächennormalen (Prismen über die Triangulierung des Kerns, mit Löchern), Zylinder und Kugeln mit **24 Strecken** je Vollkreis (3D-Ansicht 32; Kugel 24 × 12), allgemeine
  Netze (Familien, IFC) mit Knick-Normalen (**20°** wie `koerper3d.js`). Die IFC-Netze werden je zusammenhängendem Stück gleich gerichtet (Normalen nach aussen). Entartete Dreiecke entfallen. Technische Annahmen, keine Normwerte.
- **Netze:** je Geschoss und Teil **ein Netz je Gruppe, Farbe und Deckkraft** (Gruppen `architektur` = Wand, Decke, Öffnung · `raum` · `hlks` = Leitung, Formteil, Armatur, Apparat · `sonst` · `unterlage`), ein Knoten je Netz,
  undurchsichtige zuerst. Farben wie die 3D-Ansicht im Design «hell» (Teil.Farbe, sonst `--d3-<material>`), Nachbau einer IFC-Unterlage mit `farbeNachbau`/`deckkraftNachbau` (N12). **Deckkraft in VR:** Wände und Decken
  (Ebenen `wand`, `decke`) und die IFC-Unterlage undurchsichtig wie beim Begehen (N22, `vr3d.js`); die Deckkraft der 3D-Übersicht steht als `deckkraft3d` in den extras. Material: `baseColorFactor` linear (sRGB wie three r160),
  `metallicFactor` 0, `roughnessFactor` 1, `alphaMode` OPAQUE bzw. BLEND, `doubleSided`. Indizes uint16, ab 65 536 Ecken uint32.
- **Koordinaten:** glTF in Metern, y oben: `gx = (x − bx)/1000`, `gy = (z − bz)/1000`, `gz = −(y − by)/1000` (wie die 3D-Ansicht; eine Drehung, der Umlaufsinn bleibt). **Bezugspunkt** b = Projekt-Nullpunkt, ausser das
  Modell liegt in einer Achse weiter als 5 km davon (dann die Mitte, auf Meter gerundet; float32 bleibt unter 1 mm genau). Ein Bezugspunkt für alle Geschosse und Teile: die GLB passen ohne Versatz übereinander.
- **extras:** Szene `extras.kicad` = `{ fassung, teil, schluessel, projekt, geschoss: { id, kennzeichen, name, koteMm, hoeheMm }, bezugspunktMm, achsen, netze, dreiecke, elemente, hinweise }`; Netz `extras.kicad` =
  `{ gruppe, farbe, deckkraft (VR), deckkraft3d, dreiecke, elemente: { ids, ab, anzahl, ebenen | entitaeten } }` – die **Zuordnung Dreieck → Element** als Spalten: Dreiecke `[ab, ab + anzahl)` gehören zu `ids[i]`
  (Modell: Element-Id, Unterlage: GlobalId des IFC-Produkts; dazu `unterlage`, `kennzeichen`, `datei`). In der Datei statt daneben, damit Geometrie und Zuordnung einen Schlüssel haben und im Zwischenspeicher der App zusammenbleiben.
  StereoKit reicht `extras` nicht durch: die App liest das JSON des ersten Chunks (ab Byte 20, Länge in Byte 12–15) selbst.
- **Zuordnung zum Geschoss:** Element mit Geschoss dorthin; sonst (und jedes IFC-Produkt) nach der Höhe: Geschosskote der Netzdatei (±50 mm), sonst Unterkante → höchstes Geschoss mit Kote ≤ Höhe + 1 mm, darunter das unterste.
  Ohne Geschosse im Projekt ein Geschoss `ohne-geschoss` mit allem.
- **Versionsschlüssel** je Geschoss und Teil: SHA-256 (24 Zeichen) über Fassung, Projekt, Bezugspunkt, Geschoss und Inhalt (Modell: die Körper des Geschosses mit Farbe und Deckkraft; Unterlage: Netzdatei, Farbe,
  Deckkraft und die zugeordneten Produkte). Ändert sich ein Element, ändert sich nur der Schlüssel seines Geschosses; nach Rückgängig ist er wieder der alte.
- **Schnittstelle** (alles unter `/api`, also hinter dem Herkunftsschutz: nur localhost, nur GET):
  `GET /api/xr/geschosse` → `{ fassung, projekt, bezugspunktMm, achsen, geschosse: [{ id, kennzeichen, name, koteMm, hoeheMm, modell: { schluessel, elemente, url }, unterlage: { schluessel, produkte, dreiecke, url } | null }], hinweise }`;
  `GET /api/xr/geschoss/{id}.glb?teil=modell|unterlage` (Id oder Kennzeichen; ohne `teil` = modell) → `model/gltf-binary`, `ETag: "<schluessel>"`, `Cache-Control: no-cache`; `If-None-Match` mit diesem ETag (auch `W/…`, `*`) → **304**.
  Fehler `{ fehler, vorschlag }`: 400 Teil unbekannt, 404 Geschoss unbekannt bzw. keine Unterlage, 409 kein Projekt. Erfasst wird unter der Sperre des Projekts, zerlegt und geschrieben ohne Sperre; fertige GLB bleiben je Schlüssel im
  Speicher (höchstens 256 MB), gleichzeitige Anfragen rechnen einmal. **KI:** reine Rechner-Funktion `xr_modell` in `KiEinordnung.RechnerFunktionen` (für die KI verboten), kein MCP-Werkzeug.
  **Büronetz (P3-Nachtrag, 23.10.2026):** beide Endpunkte tragen `.XrImNetz()` und sind über den Zugang im Büronetz mit Schlüssel erreichbar (sonst 401); dort werden
  die GLB mit Brotli oder gzip komprimiert. `Program.cs` komprimiert `model/gltf-binary` wie `application/octet-stream` (gzip, nur für fremde Rechner). Abschnitt «Modul XR: Zugang im Büronetz».
- **Für die App (P4):** StereoKit 0.3.11 lädt die GLB (geprüft im Offscreen-Modus mit Test_praxis: alle Netze, Ecken, Dreiecke, Transparenz, doppelseitig). Die Netze behalten die glTF-Koordinaten, StereoKit setzt aber
  beim Laden eine **Drehung um 180° um die Hochachse** in die Knoten (`Model.Bounds`: x → −x, z → −z, `Mesh.Bounds` unverändert); die App muss die Knoten-Transformation zurücksetzen oder die Drehung beim Umrechnen
  berücksichtigen. Räume (Gruppe `raum`, Deckkraft 0.15) blendet die App beim Begehen nach Bedarf aus.
- **Prüfen:** Kern `--filter "FullyQualifiedName~Hlks.Kern.Tests.Xr"` (`XrGlbTests`: Schreiber, Koordinaten, Zerlegung; `XrModellTests`: Beispielprojekt und synthetische IFC; `XrModellEchtTests`: Test_praxis aus
  `%USERPROFILE%\HLKS_CAD_testdaten\test_praxis`, nur lesen, ohne die Daten übersprungen; Messwerte in der Testausgabe und `%TEMP%\HLKS_CAD\xr_p2_test_praxis.json`); Web `--filter "FullyQualifiedName~XrModellApiTests"`.
  Jede GLB prüft der unabhängige Leser `tests/Hlks.Kern.Tests/GlbPruefer.cs` (Kopf, Chunks, Ausrichtung, min/max, Indizes, Normalen, Material). **Nur mit der Brille prüfbar:** Bildrate mit Test_praxis (K1).

## Modul XR, Quest App P4a: die App verbindet, lädt das Modell und startet das Begehen (`entwurf/modul-xr.md`, Interface-Labor, Zeile P4)

Die native App «KICAD» auf der Quest (`src/Hlks.Quest`, StereoKit 0.3.11) mit dem Netz-Teil und dem Modell in `src/Hlks.Xr` (`Netz/`, `Modell/`, Übersicht `src/Hlks.Xr/LIESMICH.md`).
**Keine neue Bedienung am PC:** die App benutzt den Zugang im Büronetz (P3) und die GLB je Geschoss (P2). Sitzungsraum, Passthrough «Echter Tisch» und die Probe-Ebene: P4b (nächster Abschnitt).

- **Kopplung:** Die App liest das Kopplungspaket (wie `POST /api/xr/kopplung/paket`, auch die ganze Antwort `{ paket }`) nur aus ihrem privaten Speicher
  (`/data/user/0/ch.hkplanung.kicad.quest/files/kopplung.json`). P7 legt die Datei bei der Installation per adb in den Eingang
  `/sdcard/Android/data/ch.hkplanung.kicad.quest/files/kopplung.json`; die App übernimmt sie beim Start und löscht sie dort (auch eine ungültige). Ohne Paket zeigt sie
  «Nicht gekoppelt» und startet, wenn vorhanden, ohne PC. Keine Sicherung der App-Daten (`allowBackup` aus). Am PC: `--kopplung <datei>`.
- **Verbinden:** zuerst die zuletzt erfolgreichen Adressen und die `ips` des Pakets (Probe `GET /api/xr/verbindung`, gleichzeitig, Frist 2.5 s), sonst die Suche per
  UDP-Broadcast an den `suchPort` (Antwort mit HMAC geprüft). Jede Verbindung nur mit dem gepinnten `fingerabdruck`. 401 oder ein anderer Fingerabdruck in der Suche:
  «neu koppeln» (rot), keine weiteren Versuche. Ohne PC wird alle 30 s neu gesucht. Dann der **Live-Kanal**: `hallo` (Name aus dem Paket, Gerät, «KICAD Quest P4b» seit P4b),
  Posen 25 je Sekunde (Kopf, Handgelenke, Zeigefingerspitzen, Pinch und `pinchStaerke`, `modus: begehen`, in KICAD-mm), Empfang von `auftrag` und `stellschraube`
  (Stellschrauben: P4a `begehen.*` und `haltung.*`, seit P5a alle der Bedienung, Abschnitt «Quest App P5a»). Wiederverbinden nach 1, 2, 4, 8, 15, dann 30 s; nach 4001 (widerrufen) und 4002 (ersetzt) nicht.
- **Modell:** `GET /api/xr/geschosse`, dann alle Geschosse, Teil `modell` und `unterlage`. Der **Zwischenspeicher** in der App hält je Geschoss und Teil die GLB mit
  ihrem Versionsschlüssel: gleicher Schlüssel in der Liste = keine Anfrage, sonst `If-None-Match` (304 = gilt weiter). Verbunden fragt die App alle 30 s die Liste
  und lädt geänderte Geschosse nach (der Standpunkt bleibt). **Ohne PC** startet die App mit dem zuletzt
  geladenen Modell; die Anzeige sagt «Ohne PC: Modell vom …». Die App liest die GLB selbst (extras, Zuordnung Dreieck → Element) und baut die Netze ohne den Lader
  von StereoKit – die 180°-Drehung aus P2 entsteht so nicht (der Prüflauf bestätigt sie beim Lader von StereoKit als Gegenprobe). Räume ausgeblendet, Unterlage ein.
  **Nur das Geschoss am Stand** wird gezeichnet, mit seinen Bauteilen und seiner IFC-Unterlage (Brillentest 23.10.2026, Christian: «beschränken auf eigenes und IFC»;
  mit allen Geschossen zeitweise 54–60 Hz, nur EG stabil 72 Hz). Geschoss am Stand (`GeschossWahl`): das oberste, dessen Kote höchstens 0.3 m über dem Standboden
  liegt, darunter das unterste; Standboden = Höhe des Rigs, nicht die Kopfhöhe. Ein anderes Geschoss erreicht man seit P5a mit Pinch + Wisch hoch/runter bzw. der App «Etagen» (Abschnitt «Quest App P5a»), weiter auch über den Auftrag vom PC.
  Grosse Netze sind in **Blöcke** von höchstens 20 000 Dreiecken geteilt (`NetzBloecke`, möglichst an Elementgrenzen); der Durchgang lädt nur die betroffenen Blöcke
  neu (vorher ein Bild mit 24 ms beim Wechsel). Test_praxis: höchstens 25 Zeichenaufrufe je Geschoss (EG).
  Am PC gibt es `--modell-lokal <port>`: Liste und GLB über `http://localhost:<port>`, wenn der Netzzugang 404 meldet (vor dem Nachtrag P3; seither nicht mehr nötig).
- **Startpunkt** (`VrStartLogik`): Raum aus dem VR-Auftrag des PCs (`willkommen` bzw. `auftrag`, nur neue Nummern; Modus «begehen»), sonst der zuletzt in der App
  benutzte Standpunkt dieses Projekts (alle 10 s und beim Beenden gemerkt), sonst der grösste Raum des untersten Geschosses mit Kote ≥ 0, sonst 5 m vor der Südseite
  des Modells. Den Standort eines Raums rechnet die App aus seinem Netz (Schwerpunkt bzw. innerster Punkt, Boden, Augenhöhe 1700 mm, Blick nach der längsten Kante)
  – `raum_begehen` erreicht sie im Büronetz nicht. Ein neuer Auftrag während des Begehens setzt sie ohne Tipp in den Raum. Aufträge «Sitzungsraum»: siehe P4b (nächster Abschnitt).
- **Begehen mit Händen** (Leitlinie «Hände zuerst»; Schwellen sind Annahmen, Stellschrauben in P5):
  - *Strahl:* der Zeigestrahl des Systems (Zeiger der Hand von StereoKit aus der Aim-Pose der Laufzeit). Es ist nur ein Strahl sichtbar: der von StereoKit ist aus
    (`UI.EnableFarInteract`), die App zeichnet denselben – ohne Pinch fein und weiss, beim Zielen bis zum Ziel (cyan, grün sobald Loslassen springt). Nur ohne Zeiger
    des Systems gilt der Rückfall von der Schulter (15 cm unter und 17 cm neben dem Kopf) durch das Grundgelenk des Zeigefingers (Statuszeile `Strahl=System|Schulter`).
  - *Springen:* mit der Hand auf den Boden zeigen, Daumen und Zeigefinger zusammen (Pinch an unter 2.5 cm, los über 4 cm) und halten – Strahl und Ring zeigen das
    Ziel (bis 15 m) –, loslassen springt. Gegen ungewollte Sprünge (Brillentest 23.10.2026): gesprungen wird nur, wenn das Ziel 0.1 s vor dem Loslassen seit mindestens
    0.25 s ununterbrochen und ruhig auf gültigem Boden lag (im Umkreis 0.3 m + 10 % der Entfernung); verlässt der Strahl den Boden länger als 0.1 s, ist der Pinch
    abgebrochen (Klick); nach einem Wisch springt der Pinch nie. Stellschrauben vom PC: `begehen.ziel_ruhe_s`, `begehen.ziel_ruhe_radius_m`, `begehen.ziel_verzoegerung_s`.
  - *Drehen:* mit gehaltenem Pinch kurz und schnell seitlich wischen (mindestens 15 cm, 0.8 m/s, höchstens 0.4 s, überwiegend waagrecht): 30° **gegen** die Wischrichtung, die Welt folgt der Hand (nach rechts gewischt dreht nach links; Christian 23.10.2026: «für drehen die richtung umkehren»). Schnell hintereinander nur in eine Richtung: innerhalb 1.0 s nach einer Drehung dreht ein Wisch in die Gegenrichtung nicht, so dreht das Zurückholen der Hand nicht zurück (Stellschraube `begehen.drehen_sperre_s`; Christian: «schnellaufeinanderfolgende drehungen nur in eine richtung zulassen») –
    nur wenn **Drehen** wirkt (Christian 23.10.2026: «drehen sollte nur beim sitzen aktiviert sein», «damit kann die sitzposition übersteuert werden»): Die App erkennt
    die Haltung aus der echten Kopfhöhe über dem Boden (sitzend nach 2 s unter 1.30 m, stehend nach 2 s über 1.45 m; ohne Bodenbezug stehend) – sitzend Drehen ein,
    stehend aus. Der Schalter **«Drehen»** in der Box zeigt den wirksamen Zustand; Antippen mit dem Finger übersteuert bis zum nächsten erkannten Haltungswechsel.
    Nichts davon wird über einen Neustart gemerkt. Stellschrauben `haltung.schwelle_m`, `haltung.hysterese_m`, `haltung.haltezeit_s`.
  - *Durchgang:* Wände und Decken werden nur unter 500 mm zum Kopf durchscheinend (Deckkraft 0.3), auch die Wände, Decken, Stützen, Träger und Dächer der
    IFC-Unterlage (Annahme P4a, im Prototyp nicht).
  - *Ton und Licht:* Pinch = Klick und Aufleuchten an den Fingern; Sprung = steigender Ton, kurzes Abblenden, Ring am Ziel; Drehen = Doppelton und Abblenden;
    Abbruch = Klick beim Loslassen.
- **Anzeige** unten vor dem Blick (folgt dem Kopf träge, um 1.35 vergrössert, Text mit Umbruch): grüner Punkt «Verbunden mit <PC>», orange «Ohne PC: Modell vom …»,
  rot bei Kopplungsfehlern, grau bei der Suche; darunter Geschoss und Raum (nach einem Sprung das Kennzeichen des Geschosses), Meldungen, «Pinch auf den Boden:
  springen» (dazu «· Pinch + Wisch: drehen», wenn Drehen wirkt) mit Geschoss, Netzen, Dreiecken und Bildrate, und der Schalter «Drehen» mit «Drehen: ein (sitzend)»,
  «aus (stehend)» bzw. «ein/aus (von Hand)».
- **Protokoll** (`adb logcat`, Kennung DOTNET): alle 5 s «P4 Status: Gerät, Bildrate, Bildzeit (Mittel und höchstens), Rechenzeit der App, Geschoss (Kennzeichen),
  Netze und Aufrufe, Dreiecke (gezeichnet von allen), Durchgang (mit längstem Wechsel), Haltung=sitzend|stehend, Drehen=ein|aus, Hand=ja|nein, Strahl, Verbindung,
  Modell, Kanal, Posen, Hände» – die Grundlage für K1. Ohne Bodenbezug steht einmal ein Hinweis darin.
- **Prüfen am PC:** `powershell -ExecutionPolicy Bypass -File werkzeuge\quest_pruefen.ps1 [-Bilder 300] [-Projekt <ordner>] [-Port <n>]` startet einen KICAD-Testserver auf
  Port 5130 (Testlauf, alles unter `%TEMP%`). Läuft das Desktop-KICAD (5130 belegt), nimmt er seit V1a selbständig den ersten freien Port von 5139 abwärts und öffnet
  den Netzzugang dort (Umgebungsvariable `HLKS_XR_DESKTOP_PORT`, wirkt nur im Testlauf, nur 127.0.0.1); Schalter, Kopplungen und Zertifikat des Desktop-KICAD bleiben
  unberührt, es muss nicht beendet werden. Er legt das Beispielprojekt an bzw. öffnet eine Kopie des Projekts, erzeugt ein Paket und lässt
  die PC-Fassung zweimal laufen: mit PC (vorher ein VR-Auftrag für den ersten Raum, die App muss dort starten) und ohne PC (Start am gemerkten Standpunkt). Ergebnis
  je Lauf «P4 Laden: je Geschoss und Teil ms, MB, Dreiecke; Zwischenspeicher» und «P4 ok: Geschoss, gezeichnete Netze, Aufrufe, Dreiecke, Blöcke, Haltung und
  Drehen, mittlere Bildzeit, Rechenzeit, Modell nach, Auftrag, Durchgang (Wechsel an einer Wand des grössten Netzes im Vergleich zum Neuladen des ganzen Netzes), …».
  Einzeln: `dotnet Hlks.Quest.dll --pruefen <bilder> --kopplung <datei> [--speicher <ordner>] [--modell-lokal 5130] [--ohne-pc]`. Für Messungen am echten Projekt
  `-Projekt %USERPROFILE%\HLKS_CAD_testdaten\test_praxis` (Christian 23.10.2026). Tests: Xr `tests/Hlks.Xr.Tests`, Web `--filter "FullyQualifiedName~XrAppKlient"`
  (`XrAppKlientTests`: koppeln, finden, GLB mit 304, ohne PC, Kanal mit Pose, Auftrag, Stellschraube, 1001, 4001, 4002, 401; `XrAppKlientEchtTests`: Test_praxis
  mit Unterlage, Zeiten je Geschoss, Budget der Quest 3, Durchgang beim Gehen, Zahlen in `%TEMP%\HLKS_CAD\xr_p4a_test_praxis.json`; ohne die Kopie übersprungen).
- **Bau:** PC `dotnet build src\Hlks.Quest\Hlks.Quest.csproj -c Release`; Brille `dotnet publish src\Hlks.Quest\Projects\Android\Hlks.Quest.Android.csproj -c Release
  -f net10.0-android` → `<Build>\Hlks.Quest.Android\bin\Release\net10.0-android\publish\ch.hkplanung.kicad.quest-Signed.apk` (Testschlüssel). Installieren macht P7.
- **Nur mit der Brille prüfbar:** Bildrate mit Test_praxis (K1), Hände (K2: Strahl gleich dem Zeiger des Systems, Ruhezeit, Pinch, Wisch zum Drehen), Haltung
  sitzend/stehend und der Schalter, Dauer des Wechsels im Durchgang mit Blöcken, Verbinden im WLAN ohne Kabel (K3, Broadcast und Firewall), Ton und Licht, Übernahme
  der Kopplung aus dem Eingang.
- **Anleitung:** `zb-quest-app-begehen` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App P4b: Sitzungsraum, Passthrough «Echter Tisch» und Ebenen der Fachmodule (`entwurf/modul-xr.md`, XR-H, Interface-Labor Zeile P4, «Ergänzungen»)

Die App «KICAD» (`src/Hlks.Quest`) zeigt das Modell jetzt auch **auf einem Tisch** (XR-H) – im eigenen virtuellen Raum oder per Passthrough auf dem echten Tisch – und legt
**Ebenen der Fachmodule** in die Szene. Regeln in `src/Hlks.Xr` (`XrSitzungLogik` aus dem Prototyp, neu `Sitzung/` und `Ebenen/`, Übersicht `src/Hlks.Xr/LIESMICH.md`),
Server neu `XrEbenenEndpunkte.cs`. **Am PC keine neue Oberfläche:** Sitzungsraum und Etage kommen über den VR-Auftrag (Kontextmenü eines Raums), P7 baut parallel «Quest verbinden».

- **In den Sitzungsraum** (Auftrag vom PC, sofort über den Kanal; Regeln `VrStartLogik.AuftragSchritte`, `SitzungAuswahl`):
  - Rechtsklick Raum › **«Modell am Sitzungstisch»** (`modus: sitzungsraum`, Raum): das ganze Modell auf dem Tisch mit **Etagenschnitt im Geschoss des Raums**
    (Ergänzung P4b; der Prototyp zeigte hier «Alle Etagen»). Mit `geschoss` im Auftrag (`POST /api/vr/auftrag`) gilt dieses Geschoss; seit P5a wählt man die Etage auch in der Brille (Wisch hoch/runter, App «Etagen»).
  - Rechtsklick Raum › **«Raum in AR»** (`ausschnitt: true`): nur der **Raum ± 1.5 m** (Quader wie «Raum im 3D zeigen», N14), oben unter der Decke, als Ausschnitt so gross wie
    möglich, passend auf die Platte (ab 1:5).
  - `modus: sitzungsraum` ohne Raum: **alle Etagen**. Eine 3D-Ansicht (`ansicht`) auf dem Tisch kommt später (Hinweis, dann das ganze Modell).
  - In der Box beim Begehen: Knopf **«Sitzungsraum»** – Etagenschnitt im Geschoss, in dem man steht.
  - **Zurück ins Begehen:** Auftrag `modus: begehen` (mit Raum: in diesen Raum) oder Knopf **«Begehen»** in der Box; Standpunkt wie vorher.
- **Tisch und Modell** (Masse und Regeln aus dem Prototyp, `XrSitzungLogik`): Tisch 1.6 × 1.0 m, Platte 0.75 m, Vorderkante 0.4 m vor dem Kopf beim Betreten; das Modell zu Beginn
  **1:50** auf die Mitte der Platte eingepasst (grösser nur, wenn es sonst nicht passt, höchstens 1:200), Unterkante auf der Platte. Eingepasst wird **das Gebäude**
  (`SitzungAuswahl.Gebaeude`: Grundriss der Decken bzw. Platten, Höhe aller Bauteile; P4b-Nachtrag), nicht die Box aller Netze – in Test_praxis reichte sie mit einem
  Proxy im Projekt-Nullpunkt und Geländern 40 m vor das Gebäude (vorher 1:99, jetzt 1:50). Die Wahl einer Etage behält Lage, Massstab und Drehung; ein Ausschnitt passt neu
  ein. Massstab und Ansicht stehen an der Tischkante rechts vorn.
- **Etagenschnitt:** gezeichnet werden nur das gewählte Geschoss und die darunter; der Schnitt liegt **50 mm unter der Unterkante der Decke** (`SCHNITT_TIEFER_MM`, Christian
  21.10.2026), ohne Decke 50 mm unter der Kote des Geschosses darüber. Decken: Klasse Decke des Modells und – Ergänzung P4b – IfcSlab der IFC-Unterlage, wenn ihre Oberkante auf
  ±2 mm auf dieser Kote liegt. Fehlt die Höhe eines Geschosses in der Liste: Abstand zum Geschoss darüber, beim obersten 3000 mm (Annahme).
- **Nur zeichnen, was im Ausschnitt bzw. unter dem Schnitt liegt:** je Block (≤ 20 000 Dreiecke) die Hüllbox – ganz innen gewöhnlich, schneidet er den Quader mit dem Shader
  **«schnitt»** (`Assets/schnitt.hlsl`, verwirft alles ausserhalb), ganz aussen gar nicht. Für «Raum zeigen» stellt die App je Netz ein eigenes Netz nur aus den Elementen im
  Quader zusammen (Test_praxis: 45 688 statt 861 291 Dreiecke, 8 ms am PC). Schnittflächen bleiben offen (kein Deckel).
- **Griffe:** zwei Cyan-Kugeln an den **vorderen Ecken des sichtbar gezeichneten Gebäudes** (P4b-Nachtrag, Christian 23.10.2026: «die beiden greiffpunkte sollten an einer
  gebäudeecke sein», «griffe passen immer noch nicht»), je 6 cm schräg nach aussen, 5 cm über der Platte (`SitzungRaum.GriffPunkte`). «Sichtbar» ist ein Rechteck in den
  **Achsen des Gebäudes** (`SichtbarRahmen`; Ausrichtung aus den Kanten der Raumgrundrisse – Test_praxis steht um 43° gedreht, die Ecken einer achsparallelen Box lagen im
  Leeren): bei «Alle Etagen» und Etage die Platten der gezeichneten Geschosse (ohne Platten alle Bauteile; freistehende Wände zählen dann nicht), bei «Raum in AR» die
  Elemente im Quader, auf den Quader begrenzt. Vorne ist die Seite, die (in der Lage des Modells) am meisten zum Menschen zeigt. Die Griffe folgen Massstab, Drehung,
  Verschiebung und Höhe. **Beide mit Daumen und Zeigefinger fassen** (Pinch höchstens 5 cm vom Griff), dann: **drehen** um die Hochachse (Richtung der Hände), **verschieben**
  in Breite, Tiefe und **Höhe** (Mitte der Hände), **zoomen** um die Mitte zwischen den Händen (Abstand, **1:200 … 1:5**; `XrSitzungLogik.MassstabMin`, Christian
  23.10.2026: «die modellansichten sollten >1:20 gezoomt werden können»). Ein Griff allein bewegt nichts. Das Modell bleibt **waagrecht**. Nahe Hand: Griff heller; gehalten:
  weiss. Klick beim Fassen und Loslassen. **«Zurücksetzen»** in der Box: Startlage. **Seit V1d ein Griff** in der Mitte der vorderen Gebäudekante (siehe Abschnitt
  «Quest App V1d»); die zwei Griffe bleiben nur als Regel des Prototyps.
- **Shader «schnitt» und Farbe** (P4b-Nachtrag, Brillentest 23.10.2026: «Raum in AR» schwarz, «nur die oberste etage schwarz»): Der Shader las die Vertexfarbe unter OpenGL ES
  (Quest) von der Stelle 2 – dort liegen bei StereoKit die UV-Koordinaten (0, 0) –, weil seinem Eingang das Feld `uv : TEXCOORD0` fehlte; die Farbe wurde (0, 0, 0, 1), alles mit
  dem Shader schwarz. Unter Direct3D (PC) passen die Eingänge über die Namen, deshalb nicht im Simulator. Jetzt hat der Eingang den Vertex von StereoKit in seiner Reihenfolge
  (Position, Normale, UV, Farbe; im übersetzten GLSL Farbe an Stelle 3, Test `HintergrundUndShaderTests`). Zudem nimmt `Mesh.Draw` die Instanzfarbe **linear**, die Tönung des
  Materials aber als **Gamma**: die App rechnet die Instanzfarbe für den Shader jetzt gleich um. Prüfbild am PC (Test_praxis, gleiches Bild einmal ganz mit dem Shader): helle
  Pixel 135.9 gegen 135.8 (vorher 135.1 gegen 176.1).
- **Hintergrund** (P4b-Nachtrag): Löschfarbe und Himmel setzt nur noch `Hlks.Xr.Sitzung.HintergrundVerwalter` (Sitzungsraum dunkel, Passthrough durchsichtig, sonst der Zustand vor
  dem ersten Wechsel, genau einmal gemerkt); vorher merkten sich Sitzungsraum und Passthrough je den Zustand des anderen. Licht, Materialien und Tönung bleiben unberührt.
- **Aussehen** (XR-H, schlicht): dunkler Raum 8 × 6 m, 3.2 m hoch, mattschwarze Wände, dunkler Boden mit feinem Cyan-Raster, weisse Lichtlinien an Decke und Ecken, dunkle
  Glasplatte mit **leuchtender Cyan-Kante**, keine Effekte (kein Nebel, keine Spiegelung).
- **Schalter «Echter Tisch»** (Passthrough, `Passthrough.cs`; Entscheid Christian 23.10.2026 «Beides, Passthrough prüfen»): ein = Kamerabild der Quest statt des virtuellen
  Raums (OpenXR `XR_FB_passthrough` über die Schnittstelle von StereoKit 0.3.11, Ebene unter der Szene). Dann **mit dem Zeigefinger kurz auf die Tischplatte tippen**: die
  Fingerspitze bewegt sich nach unten (≥ 2 cm) und liegt 0.3 s ruhig (Umkreis 12 mm), 0.4 … 1.3 m über dem Boden, höchstens 1 m vom Kopf, kein Pinch
  (`TischAntippen`, Annahmen). Die Mitte der Platte liegt dann auf dem angetippten Punkt, Blick vom Kopf dorthin; das Modell steht dort, ein feiner Rahmen zeigt die Platte.
  Bis zum Antippen ist das Modell ausgeblendet und die Box bittet darum. **«Tisch neu antippen»** wiederholt es. Ohne Passthrough (Simulator, andere Laufzeit) ist der Schalter
  **grau mit Hinweis**.
- **IFC-Unterlage** (Schalter in der Box, Vorgabe **ein**, weil Test_praxis fast nur aus der Unterlage besteht).
- **Ebenen der Fachmodule** («Ergänzungen»: Schnittstelle für Fachmodule): je Ebene ein Schalter **«Ebene …»** in der Box (Begehen und Sitzungsraum, Vorgabe aus). Sichtbar ist,
  was zum gezeichneten Geschoss gehört (im Sitzungsraum auch nur im Quader); Breiten folgen dem Massstab, mindestens 1.5 mm. Linien und Pfeile als Linien, Punkte und laufende
  Partikel als Kugeln, Flächen durchscheinend.
  - **Server** (`XrEbenenEndpunkte.cs`, in `Program.cs` eine Zeile `app.MapXrEbenen(dienst)`):
    `GET /api/xr/ebenen` → `{ fassung: 1, projekt, ebenen: [{ name, titel, beschreibung, url, schluessel, elemente }] }`;
    `GET /api/xr/ebene/{name}` → `{ art: "hlks-xr-ebene", fassung: 1, name, titel, quelle, projekt, schluessel, einheit: "mm", elemente, hinweise }` mit `ETag`/304 wie die GLB;
    Element `{ typ: linie | pfeil | punkte | partikel | flaeche, geschoss (Id) | null, element (Id) | null, farbe "#rrggbb", deckkraft?, breite? (mm), groesse? (mm), tempo? (mm/s),
    abstand? (mm), punkte: [[x, y, z], …] }` in **KICAD-mm absolut** (wie die Posen im Kanal). Fehler `{ fehler, vorschlag }`: 404 Ebene unbekannt, 409 kein Projekt. Beide mit
    `.XrImNetz()` (Büronetz mit Schlüssel, sonst 401). Ein Fachmodul trägt sich als `XrEbenenAnbieter` in `XrEbenen.Anbieter` ein (liest nur, unter der Sperre des Projekts).
  - **Probe «fliessrichtung»:** je gerader Strecke einer Leitung (ab 200 mm) ein Pfeil in der Mitte (60 %, höchstens 500 mm, 15 mm breit) und je Leitung laufende Partikel
    (30 mm, 500 mm/s, alle 1 m). Richtung = Reihenfolge der Punkte (`klassen.json`, Leitung: «Fliessrichtung von anfang nach ende»), Farbe = Medium des Systems
    (`medien.json` farbeHex), über der Leitung (halbe Grösse + Dämmung + 50 mm; senkrechte seitlich) – Annahmen P4b. **Ohne Leitungen** (z. B. Test_praxis, nur IFC) eine
    **feste Probe**: je Geschoss ein Umlauf aus vier Pfeilen und Partikeln um die Räume, 2.4 m über der Kote (steht in `quelle` und `hinweise`).
  - Die App lädt die Ebenen nach dem Modell (bedingt mit ETag) und legt sie für den Start ohne PC ab (`EbenenLader`, `EbenenAblage`).
  - **KI:** reine Rechner-Funktion `xr_ebenen` (`KiEinordnung.RechnerFunktionen`, verboten), kein MCP-Werkzeug.
- **Posen an den PC:** im Sitzungsraum `modus: sitzungsraum`, `massstab` = Nenner (50 = 1:50); Kopf und Hände über die Umkehrung des Tisches, also der Punkt des Modells, über
  dem sie stehen (für die Figur am PC, P7).
- **Box im Sitzungsraum** (40° links vom Blick, etwas höher, damit der Tisch frei bleibt): Verbindung; «Sitzungsraum · Massstab 1:50» und die Ansicht (z. B. «EG · Schnitt unter
  der Decke (2.85 m)», «Alle Etagen», «Raum + 1.5 m»); Bedienhinweis zu den Griffen bzw. zum Antippen; Netze, Aufrufe, Dreiecke, Hz; Schalter «Echter Tisch», «IFC-Unterlage»,
  Knöpfe «Zurücksetzen», «Tisch neu antippen», «Begehen»; die Ebenen.
- **Protokoll** «P4 Status» zusätzlich: `Modus=begehen|sitzungsraum (Massstab, Ansicht, Schnitt-Blöcke, Griffe, IFC)`, `Passthrough=ein|bereit|nicht verfügbar (Tisch … m)`,
  `Ebenen=… (gezeichnet … Striche, … Kugeln)`; beim Start «Shader schnitt: geladen|fehlt».
- **Prüfen am PC:** `werkzeuge\quest_pruefen.ps1` hat einen **Lauf 3 «Sitzungsraum»**: Auftrag «Geschoss im Sitzungsraum zeigen» für den ersten Raum, dann drei Abschnitte (Etagenschnitt des
  Auftrags, alle Etagen, «Raum zeigen») mit Bildzeit, Netzen, Aufrufen, Blöcken mit Schnitt, Dreiecken und Ebenen; dazu die Regeln in der App (Griffe 1:50 → 1:25 und 30°,
  Tisch antippen 0.742 m, Modell auf dem Punkt, Passthrough im Simulator «nicht verfügbar» und Schalter aus, Shader geladen). `-Pruefbilder <ordner>` legt je Abschnitt zwei
  JPEG ab (nur lokal; dazu «senkrecht» von oben für die Griffe) und vergleicht zwei PNG desselben Bilds (gewöhnliches Material gegen Shader «schnitt», helle Pixel höchstens
  3 Stufen auseinander), `-OhneSitzungsraum` lässt den Lauf weg. Ist Port 5130 belegt (Brillentest): die App ohne PC aus einem Zwischenspeicher mit
  `--ohne-pc --speicher <ordner> --auftrag "{…}"` (nur PC; ein VR-Auftrag wie vom PC beim Start). Tests: Xr `tests/Hlks.Xr.Tests` (`SitzungRaumTests`, `SitzungAuswahlTests`
  mit Gebäudehülle, gedrehtem Gebäude, Griffen und den Netzen des Ausschnitts, `TischAntippenTests` mit dem Speicherbild von XR_FB_passthrough, `HintergrundUndShaderTests`,
  `XrEbeneTests`), Web `--filter "FullyQualifiedName~XrEbenenApiTests"`.
- **Nur mit der Brille prüfbar:** Passthrough (ob das Kamerabild unter der Szene erscheint und die Szene mit Alpha darüber liegt), Antippen des echten Tisches (Schwellen, Höhe),
  Griffe mit echten Händen, Bildrate im Sitzungsraum (Test_praxis «Alle Etagen» 861 291 Dreiecke in 73 Aufrufen – am PC nicht aussagekräftig), ob der Shader «schnitt» auf der
  Quest lädt, Lesbarkeit der Schrift am Tisch, Aussehen des Raums.
- **Anleitung:** `zb-quest-app-sitzungsraum` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App P5a: Bedienung mit schwebenden Apps, festen Gesten, Etagen per Wisch und Komfort (`entwurf/modul-xr.md`, Grundsatzentscheid «Bedienung», «Leitlinie Bedienung», «Ergänzungen», Interface-Labor Zeile P5)

Die App «KICAD» bedient man jetzt mit **schwebenden Apps** statt der Box (Christian 23.10.2026: «ev. nicht ein tablet sondern schwebende apps die ich berühren kann. so dass
die prezision weniger ein thema wird»), mit **festen Gestenbedeutungen** und **Geisterhänden**; der Geschosswechsel geht ohne PC (Wisch hoch/runter bzw. App «Etagen»).
Regeln ohne StereoKit in `src/Hlks.Xr` (`XrGesten.cs`, `Bedienung/`, `Modell/EtagenWechsel.cs`; Übersicht `src/Hlks.Xr/LIESMICH.md`), die App in
`src/Hlks.Quest/SchwebendeApps.cs` (Apps, Geisterhand, Tunnel), `QuestApp.cs`, `Sitzungsraum.cs`, `Rueckmeldung.cs`. **Am PC keine neue Oberfläche:** die Stellschrauben
kommen von der Seite «XR-Labor» (P6) über den Kanal. Prüfaufgaben, Messprotokoll, Katalog, flache Hand und Röntgen-Hand: Abschnitt «Quest App P5b» unten.

- **Wo die Apps stehen** (Annahmen P5a, im Brillentest einzustellen): grosse Kacheln (18 cm, `apps.groesse_m`) im **Bogen auf Brusthöhe** (30 cm unter den Augen), 45 cm vor der
  Brust (`apps.abstand_m`, Griffbereich), höchstens 4 je Reihe (5 Kacheln: 3 + 2), die **Mitte 50° links neben dem Blick** – so zeigt die rechte Hand frei nach vorn zum
  Springen, und das Modell auf dem Tisch bleibt frei (erstes Prüfbild: mitten vor dem Körper lagen die Kacheln genau dort, wo die Hand zum Springen zeigt, und über dem
  Tisch). Jede Kachel ist zum Auge gedreht. Sie **folgen dem Blick träge** (erst ab 30° bzw. 15 cm, dann weich) und **stehen still, solange eine Hand an ihnen ist**.
  `apps.anordnung`: `bogen` (Vorgabe), `reihe` (gerade Reihe vor der Brust), `an_hand` (halb so gross über der linken Handfläche, solange sie zum Gesicht zeigt).
- **Ein- und ausblenden – Entscheid P5a:** fester Platz plus Geste. Beim Start sind die Apps da. **Wegwischen** (Pinch + Wisch nach vorn) blendet sie aus; **linke Handfläche
  zum Gesicht drehen** (0.4 s halten) holt sie wieder an ihren Platz. Begründung: Einen festen Platz am Körper findet man ohne Suchen, und beide Hände bleiben frei zum
  Antippen (beim Tablet trägt eine Hand die Bedienung); die Handfläche kennt Christian vom Tablet des Prototyps (XR-C), und Wegwischen ist die feste Bedeutung «schliessen».
  Nach dem Ausblenden erinnert eine Zeile unten vor dem Blick 6 s lang daran. **Seit V1a abgelöst:** die Hebegeste holt die Apps, Wegwischen ist die offene rechte Hand
  von rechts nach links, Pinch + Wisch nach vorn entfällt (Abschnitt «Quest App V1a»).
- **Kacheln** (was die Box konnte, dazu «Etagen»):
  - Begehen: **Status** (Ampel grün verbunden / orange ohne PC / rot Fehler, Geschoss und Raum, Hz), **Etagen**, **Drehen** (Schalter, zeigt den wirksamen Zustand wie
    bisher), **Sitzungsraum**, **Ebenen** (wenn es welche gibt).
  - Sitzungsraum: Status (Massstab, Hz), Etagen, **Tisch** (öffnet: «Echter Tisch» ein/aus bzw. «nicht verfügbar», «IFC-Unterlage» ein/aus, «Zurücksetzen», «Tisch neu
    antippen»), **Begehen**, Ebenen. Mit «Echter Tisch» ein bzw. «Tisch neu antippen» schliesst die App, damit die Hand frei zum Antippen des Tisches ist.
  - Kacheln mit «›» öffnen ihre App in der Mitte: Einträge in 2 Spalten × bis 4 Zeilen, oben der Titel (mit «Seite 1/2», wenn es mehr Seiten gibt) und «×». **Etagen**:
    oberstes Geschoss zuerst, mit Name und Kote, das aktuelle grün (im Sitzungsraum zuerst «Alle Etagen»); Antippen wechselt die Etage (Regeln unten). **Ebenen**: je Ebene
    ein Schalter. **Status**: Verbindung, Modell, Ort, Netze/Dreiecke/Hz, letzter Hinweis und die Tabelle der Gesten.
- **Bedienen** (Regeln `Bedienung/AppBeruehrung.cs`, Annahmen P5a): **Antippen** = Zeigefingerspitze von vorn in die Kachel (sie löst aus, wenn sie 5 mm vor der Fläche
  ankommt; wer schon dahinter ist oder von der Seite kommt, löst nichts aus). **Fang** zum nächsten Ziel bis 3 cm neben der Kachel (`fang.radius_m`). **Leuchten**: ab
  10 cm vor der Kachel wird der Rand heller und dicker, beim Fang ein leiser Klick; beim Auslösen Klick und Aufleuchten. Nach dem Auslösen ist die Hand gesperrt, bis sie
  2.5 cm zurückgezogen ist (eine App, die sich unter dem Finger öffnet, löst nicht gleich ihren ersten Eintrag aus). **Pinch an einer Kachel** wählt beim Loslassen, wenn
  die Hand dabei höchstens 4 cm wanderte (sonst ist es ein Wisch). Eine Hand an den Apps zielt nicht zum Springen und greift am Tisch nicht.
- **Hinweise** (z. B. «Etage 1.OG – gleicher Punkt.», Stellschraube vom PC): eine Zeile unten vor dem Blick (8 s), über den Apps, solange man mit ihnen arbeitet.
- **Variante «tablet»** (`labor.variante.bedienung` = `tablet`): die Box aus P4a/P4b unverändert (Schalter und Knöpfe mit dem Finger), Grösse mit `anzeige.skala`. Die
  festen Gesten gelten in beiden Varianten.

**Feste Gestenbedeutungen** (Ergänzung «Ins Interface-Labor», Christian 23.10.2026; Regel `Hlks.Xr.Gestenbedeutung`, dieselbe Tabelle steht in der App «Status»):

| Geste | Bedeutung |
| --- | --- |
| Antippen (Zeigefinger in die Kachel) | wählt bzw. schaltet (Kachel, Eintrag einer App) |
| Pinch (Daumen + Zeigefinger) | wählt: an einer Kachel wie Antippen; beim Begehen auf den Boden zeigen und loslassen = springen; am Tisch einen Griff fassen |
| Pinch + Wisch links/rechts | blättert in einer offenen App (links = nächste Seite); beim Begehen ohne offene App drehen um 30° (nur wenn «Drehen» wirkt) |
| Pinch + Wisch hoch/runter | Etage darüber/darunter (Begehen: über demselben Punkt, sonst im grössten Raum; Sitzungstisch: Etagenschnitt, über dem obersten «Alle Etagen») |
| Pinch + Wisch nach vorn (wegwischen) – **seit V1a entfallen**, ersetzt durch das Wegwischen nach links | schloss die offene App, sonst (P5b) brach es eine laufende Prüfaufgabe ab, sonst blendete es die Apps aus |
| Linke Handfläche zum Gesicht drehen | holt die Apps (blendet sie ein) |
| Flache Hand über dem Modell, kurz ruhig (P5b) | Sitzungstisch: Handfläche nach unten = waagrechter Schnitt auf Höhe der Hand, senkrecht = Vertikalschnitt; Hand weg behält ihn, «Tisch» › «Zurücksetzen» hebt ihn auf |
| Offene Hand, Handfläche nach vorn (P5b) | Begehen: Röntgen-Hand – Wände und Decken im Kegel vor der Hand werden durchscheinend |
| Faust | greift (noch keine Funktion; Greifen kommt mit XR-B) |
| Zwei Hände an den Griffen – **seit V1d ein Griff** (Abschnitt «Quest App V1d») | Sitzungstisch: Massstab (auseinander/zusammen), Drehen, Verschieben |
| Zögern über einer App | eine Geisterhand zeigt die passende Geste |

Die aktuelle Tabelle (mit Wegwischen, Hineinziehen, Blättern auf der Tafel und dem einen Griff) steht in `Hlks.Xr.Gestenbedeutung.Tabelle` und in der App «Gesten».

- **Wisch** (Regeln `XrGesten`): mit **gehaltenem Pinch** schnell ziehen – mindestens 15 cm (`wisch.mindestweg_m`) mit mindestens 0.8 m/s (`wisch.mindesttempo_ms`) in
  höchstens 0.4 s, überwiegend waagrecht oder senkrecht (Abweichung höchstens ≈ 37°, `wisch.richtung_toleranz_grad`; Vorgabe genau «80 % waagrecht» des Prototyps).
  Eingeordnet zum Blick: senkrecht = hoch/runter; waagrecht innerhalb der Toleranz um die Blickrichtung = vor (bis V1a wegwischen, seither ohne Bedeutung) bzw. zurück (ohne Bedeutung); sonst
  links/rechts. **Richtungssperre:** innerhalb 1 s (`begehen.drehen_sperre_s`) nach einem Wisch zählt die Gegenrichtung nicht (das Zurückholen der Hand löst nichts aus) –
  wie beim Drehen (Christian 23.10.2026), jetzt für alle Wische.
  **Entscheid P5a:** überall mit Pinch, wie das abgenommene Drehen (Christian 23.10.2026: «Das neue Drehen passt»); ein Wisch mit offener Hand würde beim Senken des Arms
  leicht ungewollt die Etage wechseln. **Änderung zu P4a:** ein Wisch nach vorn oder hinten dreht nicht mehr (er war «wegwischen»; seit V1a löst er nichts aus).
- **Etage per Wisch hoch/runter bzw. App «Etagen»** (Regeln `Modell/EtagenWechsel`; Variante `labor.variante.etage` = `app`: nur über die App):
  - Begehen: nächstes Geschoss mit höherer bzw. tieferer Kote (gleiche Kote ±1 mm zählt nicht als anderes). Man landet **über demselben Punkt**, wenn er im Grundriss eines
    Raums dieses Geschosses liegt (auf dessen Boden), **sonst im grössten Raum** (Schwerpunkt bzw. innerster Punkt), ohne Räume über demselben Punkt auf der Kote. Die
    Blickrichtung bleibt. Liegt ein Raumboden so tief, dass man nach der Regel «Geschoss am Stand» (Kote höchstens 0.3 m darüber) im Geschoss darunter stünde, gilt die
    Kote. Steigender bzw. fallender Ton, kurzes Abblenden, Tunnelblick; über dem obersten bzw. unter dem untersten ein leiser Klick und ein Hinweis.
  - Sitzungstisch: Etagenschnitt des Geschosses darüber bzw. darunter (Lage, Massstab und Drehung bleiben); über dem obersten folgt «Alle Etagen», von dort nach unten das
    oberste; aus «Raum in AR» das Geschoss über bzw. unter dem Raum.
- **Geisterhände** (Regeln `Bedienung/Geisterhand`): liegt die Fingerspitze 1.5 s ruhig (Umkreis 3 cm) über einer Kachel, einem Eintrag, der offenen App oder nahe an
  einem Griff am Tisch, ohne dass etwas ausgelöst wird, führt eine halbdurchsichtige Hand (aus der Form der eigenen Hand) die Geste zweimal vor (1.8 s): **Antippen**
  (Kachel, Eintrag), **Blättern** (über der offenen App mit mehreren Seiten), **Wegwischen** (offene App mit einer Seite), **Greifen** (Griff). Dieselbe über demselben
  Ziel frühestens nach 6 s wieder.
- **Komfort, Tunnelblick** (`komfort.tunnelblick`, `komfort.tunnelblick_staerke`; Regeln `Bedienung/Tunnelblick`): beim Springen, Drehen und Etagenwechsel engt sich das
  Blickfeld kurz und weich ein (0.08 s ein, 0.12 s halten, 0.35 s aus); frei bleibt der Blick bis 60° (Stärke 0) bzw. 15° (Stärke 1) um die Blickachse, dann ein weicher
  Rand von 18°, aussen dunkel. **Entscheid P5a:** mit Tunnel entfällt beim Drehen das kurze Abblenden aus P4a; beim Springen bleibt es (es verdeckt den Schnitt). Ohne
  Tunnel gilt P4a unverändert.
- **Stellschrauben, die die App jetzt versteht** (live über den Kanal; die am PC gemerkten gelten schon beim Verbinden aus «willkommen», Paare in jeder Reihenfolge; Wert
  `null` setzt zurück; ungültige Werte bleiben unbeachtet, die App meldet «ungültig»). Pinch-Schwellen und Fangradius sind an einer Stelle geführt (`XrBedienEinstellungen`)
  und gelten für Begehen, Sitzungsraum und Apps.

  | Schlüssel | Bereich | Vorgabe | Wirkung |
  | --- | --- | --- | --- |
  | `anzeige.skala` | 0.5 … 3 | 1.35 | Schrift der Apps und Hinweise, Grösse der Box (Variante «tablet») |
  | `apps.groesse_m` | 0.08 … 0.4 | 0.18 | Kantenlänge einer Kachel (m) |
  | `apps.abstand_m` | 0.25 … 0.9 | 0.45 | Abstand der Kacheln von der Brust (m) |
  | `apps.anordnung` | `bogen`, `reihe`, `an_hand` | `bogen` | Anordnung |
  | `wisch.mindestweg_m` | 0.05 … 0.5 | 0.15 | kürzester Wisch (m) |
  | `wisch.mindesttempo_ms` | 0.2 … 3 | 0.8 | langsamster Wisch (m/s) |
  | `wisch.richtung_toleranz_grad` | 5 … 80 | ≈ 36.87 | Abweichung von der Hauptachse (Grad) |
  | `pinch.schwelle_an_m` | 0.01 … 0.06, kleiner als «aus» | 0.025 | Pinch an (Fingerabstand, m) |
  | `pinch.schwelle_aus_m` | 0.015 … 0.08, grösser als «an» | 0.04 | Pinch gelöst (m) |
  | `fang.radius_m` | 0.005 … 0.15 | 0.03 | Fang an den Kacheln (m) |
  | `komfort.tunnelblick` | true/false | true | Tunnelblick |
  | `komfort.tunnelblick_staerke` | 0 … 1 | 0.3 | wie eng |
  | `komfort.ton` | true/false | true | Ton bei Fang, Griff, Berührung, Sprung (das Licht bleibt) |
  | `begehen.drehwinkel_grad` | 5 … 90 | 30 | Winkel je Drehschritt |
  | `sitzung.zoom_min` | 5 … 100, kleiner als «max» | 5 | stärkster Zoom am Tisch (1:n) |
  | `sitzung.zoom_max` | 50 … 1000, grösser als «min» | 200 | schwächster Zoom am Tisch (1:n); auch das Einpassen nutzt ihn |
  | `labor.variante.bedienung` | `apps`, `tablet` | `apps` | schwebende Apps bzw. die Box |
  | `labor.variante.etage` | `wisch_modell`, `app` | `app` (seit Brillentest 24.10.2026) | Etage per Wisch hoch/runter bzw. nur über die App |

  Dazu weiter `begehen.ziel_ruhe_s`, `begehen.ziel_ruhe_radius_m`, `begehen.ziel_verzoegerung_s`, `begehen.drehen_sperre_s` (P4a) und `haltung.*` (P4a-Nachtrag).
  Seit P5b kommen Vorgaben und Grenzen aus dem Katalog (Abschnitt «Quest App P5b»); `sitzung.zoom_min` steht dort jetzt auf 5 (wie die App seit dem P4b-Nachtrag,
  Christian 23.10.2026: «die modellansichten sollten >1:20 gezoomt werden können»), `wisch.richtung_toleranz_grad` auf 36.87 (Schritt 0.01).
- **Protokoll** «P4 Status» zusätzlich: `Bedienung=apps|tablet (Apps sichtbar|aus, offen …, Anordnung), Geste=…, Etagenwechsel=n (…), Geisterhände=n, Tunnel=n Bilder`;
  die App heisst im Kanal «KICAD Quest P5a».
- **Prüfen am PC:** `werkzeuge\quest_pruefen.ps1` prüft in jedem Lauf zusätzlich die Bedienung, in der Zeile «P4 ok» als «P5a: Stellschrauben=ok (18 Schlüssel der
  Bedienung), Apps=ok (Kacheln …; Etagen n Einträge; Wegwischen schliesst ja, blendet aus ja), Etage=ok (EG → 1.OG (gleicher Punkt) → EG; Tunnel ja)» im Begehen bzw.
  «Etage=ok (EG · Schnitt … → UG · Schnitt … → EG · Schnitt …)» im Sitzungsraum; «FEHLER» lässt den Lauf scheitern. Mit `-Pruefbilder <ordner>` dazu
  `begehen_blick.jpg`, `apps_begehen.jpg`, `apps_sitzung.jpg`, `apps_etagen.jpg` (nur lokal). Tests: Xr `tests/Hlks.Xr.Tests` (`BedienungTests`: Wisch in allen
  Richtungen, Gestenbedeutung, Stellschrauben, Lage der Apps, Nachführen, Einblenden, Blättern, Antippen, Fang, Pinch, Zögern, Geisterhand, Tunnelblick, Zoomgrenzen;
  `EtagenWechselTests`; `HandBegehenTests` mit Wisch hoch/runter/vor, Apps und Schwellen).
- **Nur mit der Brille prüfbar:** ob man die Kacheln mit echten Händen trifft (Druckebene, Fang, Sperre nach dem Auslösen), Lesbarkeit mit 1.35, Platz der Apps (50° links,
  Brusthöhe) im Stehen und Sitzen, ob die Handfläche die Apps zuverlässig holt, Wisch hoch/runter ohne Fehlauslösung beim Zielen, das Wegwischen, Etagenwechsel
  (Landepunkt, Blickrichtung), Geisterhände (Zeitpunkt, Aussehen), Tunnelblick (Stärke 0.3 spürbar?), Ton und Licht, die Variante «tablet» im Vergleich.
- **Anleitung:** `zb-quest-app-bedienung` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App P5b: das Labor in der App – Katalog, Prüfaufgaben, Varianten, Schnitt mit der flachen Hand, Röntgen-Hand (`entwurf/modul-xr.md`, Interface-Labor Zeile P5, «Ergänzungen»)

Die App «KICAD» nimmt jetzt den Katalog des Labors von KICAD (Vorgaben, Grenzen, Profil), spielt die **Prüfaufgaben** ab, die man am PC im Dialog «XR-Labor» (P6)
startet, misst sie und sendet die Messprotokolle; dazu die **Varianten** «teleport» (Stick), «etage», «messen» und die zwei Ergänzungen, die Christian am 23.10.2026
übernommen hat: **Schnitt mit der flachen Hand** am Sitzungstisch und **Röntgen-Hand** beim Begehen. Regeln ohne StereoKit in `src/Hlks.Xr` (`Labor/LaborKatalog.cs`,
`Labor/LaborWerte.cs`, `Labor/PruefAufgaben.cs`, `Netz/LaborKatalogLader.cs`, `Netz/ProtokollAblage.cs`, `Sitzung/FlacheHand.cs`, `Modell/RoentgenHand.cs`,
`StickTeleport.cs`, `Bedienung/Textumbruch.cs`; Übersicht `src/Hlks.Xr/LIESMICH.md`), die App in `src/Hlks.Quest/QuestApp.Labor.cs`, `AufgabenKarte.cs`,
`Sitzungsraum.cs`, `Fassung.targets`. **Am PC keine neue Oberfläche:** gestartet, eingestellt und ausgewertet wird im Dialog «XR-Labor». Keine Normwerte – alle Zahlen
sind Einstellungen der Bedienung (Annahmen P5b bzw. P6, im Katalog je mit «herkunft»).

### Katalog in der App
- **Holen:** beim Verbinden zuerst `GET /api/xr/labor/katalog` (vor dem Modell), abgelegt im privaten Speicher der App (`labor/katalog.json`, mit ETag); beim nächsten Mal
  mit `If-None-Match` – 304 heisst «unverändert». Alle 30 s fragt die App nach (meist 304), so kommt ein neues Profil «Für die App festlegen» ohne Neustart. **Ohne PC**
  gilt der abgelegte Katalog; ohne Ablage (noch nie verbunden) die Vorgaben des Codes. Ein KICAD ohne Labor (404) bzw. ein Netzfehler: die Ablage, mit Hinweis.
- **Wert einer Stellschraube** (Vertrag P6): Vorgabe des Katalogs › `profil.werte` › live (`willkommen.stellschrauben`, Nachricht `stellschraube`). `null` setzt live zurück –
  dann gilt wieder das Profil bzw. die Vorgabe. **Grenzen aus dem Katalog** (`min`/`max`, Auswahl, true/false): ein Wert ausserhalb, vom falschen Typ oder einer, der ein
  Paar verletzt (Pinch an &lt; aus, Zoom min &lt; max), ist **«ungültig»** (Hinweis in der Brille), der bisherige Wert bleibt. Ein Schlüssel, den die App nicht versteht,
  ist «unbekannt». Kommt ein Katalog mit engeren Grenzen, fallen live gesetzte Werte, die nicht mehr gelten, auf Profil bzw. Vorgabe zurück (Hinweis).
- **Katalog angeglichen** (`kataloge/xr_stellschrauben.json`): alle Stellschrauben und beide Muster tragen `status: app`; `sitzung.zoom_min` Vorgabe **5** (1:5);
  `wisch.richtung_toleranz_grad` **36.87** mit Schritt 0.01 (arccos 0.8 = 36.8699°, im Code auf 0.01° gerundet; für genau diesen Wert gilt weiter die Regel «mindestens
  80 % waagrecht» des Prototyps); `apps.*` und `komfort.*` gleich den Vorgaben der App. Neu: `roentgen.winkel_grad` (40°, 10 … 120), `roentgen.reichweite_m` (6 m, 1 …
  20), `sitzung.schnitt_ruhe_s` (0.3 s, 0 … 2), `sitzung.schnitt_toleranz_grad` (25°, 5 … 45). `tests/Hlks.Web.Tests/XrLaborTests.cs` vergleicht jede Vorgabe, Grenze,
  Auswahl und Variante mit dem Code und prüft durchgehend, dass der ausgelieferte Katalog in der App genau die Vorgaben des Codes ergibt.
- **Fassung der App:** `hallo.app` = «Fassung+Commit», z. B. `0.1+680f9af` (höchstens 40 Zeichen). `src/Hlks.Quest/Fassung.targets` schreibt beim Bau
  `AppFassung.g.cs`: Fassung 0.1 (wie `ApplicationDisplayVersion` der Brillen-Fassung), Commit aus `git rev-parse --short HEAD` (ohne Netz; ohne Git «unbekannt»; von
  aussen `-p:HlksCommit=…`). KICAD schreibt sie in jedes Messprotokoll und wertet je Fassung aus.

### Prüfaufgaben: starten, was man in der Brille sieht, was gemessen wird
- **Starten:** am PC Register **XR › Labor › «XR-Labor …»**, Reiter **«Prüfaufgaben»**, bei der Aufgabe **«Aufgabe starten»** (Stellschraube `labor.aufgabe` = Id).
  Die App stellt den **Ort** her (Begehen bzw. Sitzungsraum mit dem Etagenschnitt im Geschoss am Stand), bereitet die Aufgabe vor und beginnt die Zeit, sobald alles passt
  (leiser Klick, Hinweis «Aufgabe … läuft.»).
- **Karte:** rechts neben dem Blick (28°; die Apps stehen links), 60 cm vor dem Auge, 8 cm unter den Augen, zum Auge gedreht, folgt dem Blick träge wie die Apps. Sie zeigt
  den **Titel** und die **Anleitung** aus dem Katalog, darunter den Abschnitt bzw. das Ziel mit Zeit und Fehlgriffen und den Knopf **«Aufgabe beenden»** (Antippen oder
  Pinch). Schrift nach `anzeige.skala`. Erfolg: Karte grün «Geschafft · Zeit · Fehlgriffe» (5 s), Klick. Abgebrochen: grau (5 s).
- **Ende:** Erfolg nach dem Kriterium unten; **Abbruch** mit «Aufgabe beenden», mit dem **Wegwischen** (seit V1a die offene rechte Hand von rechts nach links über die
  Karte bzw. ohne Ziel das Vorderste: eine offene App, sonst die Karte; bis V1a Pinch + Wisch nach vorn), am PC mit «Aufgabe beenden» (`null`) bzw. einer anderen Aufgabe. Dieselbe Aufgabe nochmals gestartet = **neu begonnen** (Abbrüche + 1, die Zeit läuft
  weiter, der Ablauf beginnt von vorn).
- **Messprotokoll** (Nachricht `messprotokoll`): `aufgabe`, `zeitMs` (vom Beginn bis Erfolg bzw. Abbruch), `fehlgriffe`, `abbrueche`, `erfolg`, `varianten` (die Varianten,
  die für die Aufgabe zählen – Katalog `aufgaben[].varianten`; ohne Katalog alle), `stellschrauben` (die wirksamen Werte aller Stellschrauben und Varianten aus Katalog,
  Profil und live) und `notiz` («Fehlgriffe: Sprung daneben ×2; …»). Ohne Verbindung bleibt es im privaten Speicher (`labor/messprotokolle_warten.jsonl`, höchstens 50,
  auch über einen Neustart) und geht mit dem nächsten «willkommen». Ein Lauf, der nur wartete (z. B. «tablet-wert-ablesen» in der Variante «apps») oder gesperrt war, sendet
  keines.
- **Je Aufgabe** (Regeln `Hlks.Xr.Labor.PruefAufgaben`; «Fehlgriff» steht auch im Katalog als Feld `fehlgriff`):

  | Aufgabe | Ort | Was man sieht, wann sie gelingt | Fehlgriff |
  | --- | --- | --- | --- |
  | `teleport-markierung` | Begehen | Leuchtender Ring am Boden (Radius 0.5 m = Erfolgskriterium) mit Lichtsäule, 2 … 4.5 m vor einem in einem Raum des Geschosses (sonst 3.5 m geradeaus). Gelingt mit einem Sprung höchstens 0.5 m neben die Mitte. | Sprung weiter daneben, abgebrochener Sprung (Pinch bzw. Stick losgelassen ohne Ziel, Strahl vom Boden), Antippen daneben |
  | `drehen-90` | Begehen | Pfeil am Boden in die verlangte Richtung. Zuerst 90° nach links (±15°, 0.5 s halten), dann zurück in die Ausgangsrichtung (±15°, 0.5 s) – körperlich oder per Drehschritt. | Drehschritt, der vom Ziel des Abschnitts wegführt; ungewollter Sprung; Antippen daneben |
  | `etage-wechseln` | Begehen | Die Karte nennt das Ziel: das Geschoss darüber (im obersten: darunter), dann zurück. Mit nur einem Geschoss wartet die Aufgabe (Hinweis). | Wechsel in ein falsches Geschoss; Wisch hoch/runter ohne Wirkung (Variante «app» bzw. kein Geschoss mehr); Antippen daneben |
  | `modell-greifen-zoomen` | Sitzungsraum | Ziel «1:n und eine Vierteldrehung»: der runde Massstab nahe der halben Startgrösse (z. B. 1:50 → 1:25, 1:137 → 1:75). Seit V1d mit dem einen Griff (drehen mit der Hand, zoomen mit der zweiten Hand bzw. als Drehknopf; Varianten `bedienung`, `zoom_ein_griff`). Gelingt, wenn der Griff losgelassen ist, der Massstab höchstens 10 % daneben liegt und das Modell 90° (±15°) gedreht ist, in beide Richtungen. | Pinch am Tisch ohne Griff in Reichweite; Antippen daneben |
  | `raum-ar-oeffnen` | Sitzungsraum | Der markierte Raum (der Raum, in dem man vor dem Sitzungsraum stand, sonst der des Auftrags, sonst der grösste des Geschosses) als Kiste auf dem Modell. Schritte (auf der Karte): App «Tisch» › **«Raum zeigen»** → «Echter Tisch» → auf die Tischplatte tippen. Ohne Passthrough (Simulator) genügt der Ausschnitt. | Rückschritt: «Echter Tisch» aus, «Tisch neu antippen», «Zurücksetzen», eine Etage statt des Raums; Antippen daneben |
  | `tablet-wert-ablesen` | Begehen, Variante «tablet» | Das markierte Bauteil (Wand bzw. Decke etwa 2 m vor dem Blick, höchstens 6 m weg) als Kiste; die Box (Tablet) zeigt Art, Höhe und Hüllbox und vier Antworten zur Frage «Welche Höhe hat das markierte Bauteil?». Gelingt mit der richtigen Antwort. In der Variante «apps» wartet die Aufgabe (Karte: «Nur in der Variante «Tablet an der Hand» …», die Zeit läuft nicht) und beginnt, sobald am PC «Tablet» gewählt ist. | falsche Antwort; Antippen daneben |
  | `messen-wand-wand` | Begehen (seit V1a) | Zwei gegenüberliegende Wände des Raums sind markiert (Rahmen 1 cm vor der Fläche), Messen ist eingeschaltet. Gelingt mit einer Messung von der einen zur anderen Wand (je höchstens 5 cm neben der Fläche), höchstens 2 cm neben dem Sollmass. Details im Abschnitt «Quest App V1a». | Messung nicht von Wand zu Wand bzw. mehr als 2 cm daneben; Messband ohne Ziel bzw. zu kurz; Antippen daneben |

  **Antippen daneben** (`Bedienung/AppBeruehrung`, alle Aufgaben): die Fingerspitze geht von vorn durch die Druckebene einer Kachel, eines Eintrags bzw. des Knopfs, liegt
  aber 3 … 9 cm neben ihr (ausserhalb des Fangs `fang.radius_m`, höchstens 6 cm weiter).

### Varianten
- **`labor.variante.teleport`:** `zeigen_pinch` (Vorgabe, P4a) | `stick`: am Controller den **Stick nach vorn** drücken (ab 0.7, überwiegend vorn) zielt mit dem Zielstrahl
  des Controllers (Ring und Strahl wie beim Pinch), **loslassen** (unter 0.3) springt auf das letzte gültige Ziel, wenn es höchstens `begehen.ziel_verzoegerung_s` (0.1 s)
  alt ist; **seitlich** dreht in Schritten (`begehen.drehwinkel_grad`, Stick rechts = nach rechts wie der Prototyp), nur wenn «Drehen» wirkt. In dieser Variante springt der
  Pinch nicht. Ohne Controller alle 20 s der Hinweis «einen Controller in die Hand nehmen».
- **`labor.variante.etage`:** `app` (Vorgabe seit dem Brillentest 24.10.2026, Christian: «der etagenwechsel mit geste ist unpraktisch.») | `wisch_modell` (Vorgabe P5a bis V1d) | `app`: Pinch + Wisch hoch/runter wechselt nicht (leiser Klick, Hinweis «… das Geschoss in der App «Etagen»
  wählen»), nur die App «Etagen».
- **`labor.variante.messen`:** `messband` | `zwei_punkte` – seit V1a wirksam (Abschnitt «Quest App V1a»); die App trägt die Wahl ins Messprotokoll ein.
- **`labor.variante.bedienung`:** `apps` | `tablet` wie P5a.

### Schnitt mit der flachen Hand (Sitzungsraum; Regeln `Sitzung/FlacheHand`)
- **Geste:** die Hand flach (Finger gestreckt, kein Pinch, nicht an den Apps, keinen Griff haltend) **über dem Modell** (im Grundriss des Modells auf dem Tisch, 5 cm Rand,
  höchstens 30 cm über seiner Oberkante) und **kurz ruhig** (`sitzung.schnitt_ruhe_s`, 0.3 s, im Umkreis von 2 cm). Eine Hand, die eben einen Griff losgelassen hat,
  zählt 1 s lang nicht (wer das Modell gezoomt hat, öffnet die Hand meist flach darüber).
  - **Handfläche nach unten** (höchstens `sitzung.schnitt_toleranz_grad` = 25° neben senkrecht nach unten): **waagrechter Schnitt** – alles über der Handfläche fällt weg, wie
    der Etagenschnitt, aber frei in der Höhe.
  - **Senkrecht gehalten** (Handfläche höchstens 25° neben der Waagrechten, nicht zum Gesicht – die Handfläche zum Gesicht holt die Apps): **Vertikalschnitt** durch die
    Mitte der Handfläche, parallel zu ihr; weg fällt die Seite zum Kopf hin (man blickt auf die Schnittfläche). Bis 10° neben einer Achse des Gebäudes rastet die Ebene dort
    ein; im Ausschnitt «Raum zeigen» liegt sie immer in einer Achse des Ausschnitts.
- **Folgen und behalten:** danach folgt der Schnitt der Hand in jedem Bild (Hysterese 10°, Hand offen bis Verhältnis 1.4); **Hand weg bzw. nicht mehr flach behält den
  Schnitt**. Ein heller Rahmen zeigt die Schnittfläche, solange er folgt, danach ein feiner. Hinweise: «Schnitt folgt der flachen Hand – Hand weg behält ihn.», «Schnitt
  bleibt – «Tisch» › «Zurücksetzen» hebt ihn auf.»
- **Aufheben:** App **«Tisch» › «Zurücksetzen»** (Lage, Massstab, Drehung und Schnitt der Hand); ebenso ein neuer Ausschnitt bzw. das Antippen des echten Tisches. Ein
  Wechsel der Etage behält ihn (es gilt die tiefere Oberkante). Gezeichnet mit dem vorhandenen Shader «schnitt» (Etagenschnitt bzw. Ausschnitt und Hand zusammen in einem
  Quader).

### Röntgen-Hand (Begehen; Regeln `Modell/RoentgenHand`)
- **Geste:** offene Hand (Finger gestreckt, kein Pinch), **Handfläche nach vorn** – ihre Normale höchstens 50° neben der Richtung vom Kopf zur Hand (die «Halt»-Hand auf
  Brusthöhe liegt bei etwa 40°; durch die Decke mit erhobener Hand, Handfläche nach oben). Ein nach 0.2 s, aus 0.25 s nach dem Loslassen (solange aktiv gilt bis 65° bzw.
  Verhältnis 1.45). Hängende Hände, die Handfläche zum Gesicht und der Pinch zählen nicht. Rechts vor links.
- **Wirkung:** Wände und Decken (die Bauteile des Durchgangs, auch die IFC-Unterlage) im **Kegel** um die Zeigerichtung der Handfläche – Öffnungswinkel
  `roentgen.winkel_grad` (40°, also ±20°), Reichweite `roentgen.reichweite_m` (6 m) – werden durchscheinend wie im Durchgang (dieselben Netze und Deckkraft), so dass die
  Leitungen und Räume dahinter sichtbar werden. Nur im Geschoss am Stand; der Boden unter den Füssen bleibt. Ein feiner Kegel (60 cm) an der Hand zeigt die Richtung;
  leiser Klick beim Einschalten, beim ersten Mal ein Hinweis. Neu gerechnet nur, wenn sich die Hand 3 cm bzw. 2° bewegt hat.

### Weiteres
- App **«Tisch»** (Sitzungsraum): neuer Eintrag **«Raum zeigen»** (Ausschnitt des markierten bzw. des Raums am Standort vor dem Sitzungsraum); in der Variante «tablet» ein
  Knopf in der Box.
- **Gesten** (Tabelle in der App «Status» und oben bei P5a): Pinch + Wisch nach vorn brach eine laufende Prüfaufgabe ab (nach der offenen App, vor dem Ausblenden; seit
  V1a das Wegwischen nach links); neu
  «Flache Hand über dem Modell» und «Offene Hand, Handfläche nach vorn».
- **Texte in der Brille** (Karte, Hinweiszeile) bricht die App selbst um (`Bedienung/Textumbruch`; Befund am Prüfbild: StereoKit verkleinerte lange Zeilen sonst auf eine
  Zeile).
- **Protokoll** «P4 Status» zusätzlich: `Fassung=…, Katalog=pc|pc, unverändert|zwischenspeicher|keiner (…), Aufgabe=…, Messprotokolle=n (Quittungen, wartend),
  Teleport=…, Röntgen=…, Schnitt Hand=…`; die App heisst im Protokoll «KICAD Quest P5b».

### Prüfen
- **Am PC:** `werkzeuge\quest_pruefen.ps1 [-Projekt <Test_praxis>] [-Pruefbilder <ordner>]` meldet in jeder Zeile «P4 ok» zusätzlich «P5b: Fassung=0.1+…, Katalog=ok
  (pc … bzw. zwischenspeicher …), Grenzen=ok, Vorgaben=ok, Aufgaben=ok (…), Röntgen=ok (…)» bzw. im Sitzungsraum «Aufgaben=ok (modell-greifen-zoomen …), Flache Hand=ok
  (…)»; Lauf 1 spielt «messen-wand-wand» (gesperrt), «drehen-90» (abgebrochen) und «teleport-markierung» (ein Sprung daneben, einer hinein), Lauf 3
  «modell-greifen-zoomen». Das Skript prüft mit `GET /api/xr/labor/auswertung`, dass die Messprotokolle am PC angekommen sind. Mit `-Pruefbilder` dazu
  `aufgabe_karte.jpg`, `roentgen.jpg`, `sitzung_flache_hand.jpg` (nur lokal).
- **Tests:** Xr `tests/Hlks.Xr.Tests` (`LaborKatalogTests`: Lesen, Prüfen, Schichten, Grenzen, Paare, Laden mit 304 und Ablage, wartende Messprotokolle;
  `PruefAufgabenTests`: jede Aufgabe mit Erfolg, Fehlgriffen, Abbruch, Neustart, Messprotokoll, Karte, Räume; `FlacheHandTests`; `RoentgenHandTests`; `StickTeleportTests`;
  `P5bBedienungTests`: Wegwischen der Karte, Antippen daneben, neue Stellschrauben, Textumbruch); Web `XrLaborTests` (Katalog = Code der App, `fehlgriff`).
- **Nur mit der Brille prüfbar:** ob die Karte gut liegt und lesbar ist; ob die flache Hand zuverlässig erkannt wird (waagrecht und senkrecht, 25°, Ruhezeit 0.3 s) und den
  Griffen nicht in die Quere kommt; ob die Röntgen-Hand ungewollt anspringt (Winkel 50°) und wie weit der Kegel sinnvoll ist; der Stick an echten Controllern (Richtung von
  `stick.y`, Zielstrahl); Antippen daneben (6 cm) als Mass für Fehlgriffe; Passthrough in «raum-ar-oeffnen»; Ton und Licht beim Start und Erfolg.
- **Anleitung:** `zb-quest-app-labor` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App V1a: Messen, Problem-Marker und die Apps im Stil von visionOS (`entwurf/modul-xr.md`, XR-C, XR-E; Christian 24.10.2026)

V1a bringt zwei Werkzeuge in die App – **Messen** (XR-C, nach der Leitlinie «Hände zuerst»: ein Massband, das man zwischen zwei Punkten aufzieht) und
**Problem-Marker** (XR-E, mit einer Liste am PC) – und stellt die Apps auf den Stil der Apple Vision Pro um (Christian 24.10.2026: «richte dich bei der Darstellung der
apps und funktionen nach der vision»; Quelle Apple Human Interface Guidelines, visionOS-Abschnitte «Materials», «Windows», «Buttons», «Tab bars», «Ornaments», «Eyes»,
«Spatial layout», «Immersive experiences», abgerufen 24.10.2026). Regeln ohne StereoKit in `src/Hlks.Xr` (`Messen/SzenenFang.cs`, `Messen/Messband.cs`,
`Marker/ProblemMarker.cs`, `Labor/WandPaar.cs`, `Bedienung/Glas.cs`, `Bedienung/GlasSymbole.cs`), die App in `src/Hlks.Quest` (`GlasApps.cs`, `GlasZeichner.cs`,
`Assets/glas.hlsl`, `QuestApp.Apps.cs`, `QuestApp.Messen.cs`, `QuestApp.Marker.cs`, `Umgebung.cs`, `AufgabenKarte.cs`), am PC `XrMarkerEndpunkte.cs`, die Klasse
`ProblemMarker` (`kataloge/klassen.json`), `Kollision/BcfMarker.cs` und die Palette `problemmarker-panel.js`. **Keine Normwerte:** alle Zahlen sind Einstellungen der
Bedienung bzw. Annahmen V1a. Nach den Prüfbildern 01 kamen am selben Tag dazu (Christian 24.10.2026): **Hebegeste und frei schwebende App-Symbole** statt der
Registerleiste, **Zeiger mit Tooltip**, **Hinweise frei schwebend** (Regeln `Bedienung/AppSymbole.cs`, `Bedienung/Hinweise.cs`; App `SymbolApps.cs`,
`QuestApp.Hinweise.cs`); nach den Prüfbildern 02: **Symbolfeld frontal**, **Berührung mit der ganzen Hand** (`Bedienung/HandBeruehrung.cs`), **Symbole bleiben stehen**,
**eine Geste Wegwischen** für alles (`Bedienung/Wegwischen.cs`, Einhängepunkt `Wegwischbar.cs`; Pinch + Wisch nach vorn entfällt), **App-Pastellreihe**
(`kataloge/xr_farben.json`, `Bedienung/AppFarben.cs`) und die **Symbole von KICAD** als Texturen (`werkzeuge/quest_symbole.mjs`, `Assets/symbole`); nach den
Prüfbildern 03 (Hauptlauf 24.10.2026): **Bedienung vor dem Modell** (Wände schneiden Symbole, Fenster und Karte nicht mehr an), **dunkle Fassung mit Farbe**, Variante
**Feld seitlich neben der App** (Entscheid Christian), **Luft über der Tischplatte**, **Geisterhand schräg**, **Marker hinter dem Fenster blass**.

### Apps holen: Hebegeste und App-Symbole
Christian 24.10.2026: «wird mit beiden handflächen nach oben eine deutliches nach obenstreichen gezeigt, soll dies frei schwebende Appsymbole von unten her einblenden. so wie
ich sie von unten hinein heben würde. die symbole müssen in griff weite sein. angewählt werden sie durch berührung. finger zeigen den pointer mit tooltip»; «symbole nicht
in einer leiste sondern verteilt»; «Ja, die Registerleiste fällt weg; gewechselt wird über die Hebegeste und die Symbole».
- **Hebegeste:** beide Hände offen, **Handflächen nach oben**, beide **deutlich und etwa gleichzeitig nach oben** streichen – je Hand mindestens `hebe.weg_m` (0.15 m) in
  höchstens `hebe.zeit_s` (0.9 s), Handfläche höchstens `hebe.handflaeche_grad` (45°) neben oben, die Hände beginnen höchstens `hebe.gleichzeitig_s` (0.35 s) auseinander,
  seitlich höchstens 60 % des Wegs. Alle Werte sind **Annahmen V1a** (Stellschrauben im XR-Labor). Ohne Pinch – die Geste löst weder Pinch + Wisch (Etage) noch einen
  Sprung aus. Nach einem Heben zählt die Geste 0.8 s nicht. Ersetzt «linke Handfläche zum Gesicht» (P5a; nur die Anordnung «an der Hand» im Labor nutzt sie noch).
- **Die Symbole:** runde Glas-Symbole ohne Fenster dahinter, eines je App, **im Pastellton der App** mit schmalem Ring im Akzent und dem **Symbol aus KICAD** –
  **KICAD** (Status, `info`, mit Ampel der Verbindung), **Etagen** (`geschoss`), **Messen** (`bemassung`), **Marker** (`problemMarker`), **Ort** (Begehen: Drehen,
  Sitzungsraum; `raumBegehen`) bzw. **Tisch** (Sitzungsraum: Echter Tisch, IFC-Unterlage, Zurücksetzen, Raum zeigen, Begehen; `sitzungstisch`), **Ebenen**
  (`sichtbarkeit`, wenn es welche gibt). Sie **steigen von unten ein**, folgen während der Geste der Höhe der Hände (jedes ein wenig nach dem vorigen) und setzen sich
  danach ruhig; ab dann stehen sie **fest im Raum** (nicht am Kopf), **zum Kopf gewandt**.
- **Anordnung: ein Feld frontal** (Christian 24.10.2026: «die appsymbole sollen frontal des nutzers in einem abstand von ca. 60cm und über eine höhe von ca. 0.6m
  verteilt sein. vorzugsweise maximal 3 reihig»): zentriert in Körperrichtung (Gierwinkel des Kopfs beim Auslösen, ohne Nicken), `symbole.abstand_m` (0.6 m) vor den
  Augen – **Bezugspunkt die Mitte zwischen den Augen, waagrecht gemessen** –, senkrechte Mitte `symbole.hoehe_m` (0.32 m; **seit V1d 0.22 m**, sitzend und am Tisch
  eigene Werte, siehe Abschnitt «Quest App V1d») unter den Augen, von der obersten zur untersten
  Reihe `symbole.feldhoehe_m` (0.6 m; die oberste Reihe bleibt 2 cm unter Augenhöhe), höchstens `symbole.reihen` (3) Reihen. Die Reihen sind **locker gegeneinander
  versetzt**, jedes Symbol leicht in Höhe und Tiefe verschoben (keine Leiste, keine starre Matrix); Spaltenabstand 0.2 m × `symbole.streuung`. **Je App immer derselbe
  Platz** (oben Etagen und Ort/Tisch, in der Mitte Messen und Marker, unten KICAD und Ebenen). **Am Sitzungstisch** liegt die Unterkante der untersten Symbole mindestens
  `symbole.tisch_abstand_m` (0.06 m, Annahme V1a; Befund Prüfbilder 03: «mindestens einige Zentimeter über der Tischplatte») über der Platte, auch mit der leichten
  Verschiebung je Platz; das Feld wird so niedriger (sitzend mit Augen auf 1.2 m und Tisch auf 0.75 m: etwa 0.33 m statt 0.6 m), die oberste Reihe bleibt unter Augenhöhe. Variante
  `labor.variante.symbole`: `verteilt` (Vorgabe) | `bogen` (eine Reihe im Bogen, zum Vergleich).
- **Vor dem Modell:** Symbole, Fenster, Karte und Tooltips zeichnen **ohne Tiefentest gegen das Modell** (Befund Prüfbilder 03: Treppenwände schnitten Symbole und die
  Karte an) – in fester Reihenfolge: das Symbolfeld, dann Fenster und Karte, dann die Tooltips, danach die Hände (sie verdeckt das Glas, wenn sie dahinter sind) und die
  Geisterhände. Das Modell selbst (Bodenplatte am Tisch, Beschriftungen, Marker) bleibt wie es ist.
- **Wählen durch Berührung mit der ganzen Hand** (Christian 24.10.2026: «die app berührung soll mit der ganzen hand erfolgen und nicht nur mit einem finger»): ein
  Symbol gilt als gewählt, sobald **irgendein Teil einer Hand** es berührt – Handfläche, Handrücken oder ein Finger (die Hand als Kapseln um die Knochen und eine Kugel
  um die Handfläche; das Symbol als Kugel plus `symbole.beruehr_rand_m` = 1 cm). **Schutz vor Fehlwahl:** während Hebegeste und Wegwischen und
  `symbole.beruehr_sperre_s` (0.6 s) danach zählt keine Berührung, und was dabei berührt wurde, erst nach dem Loslassen; streift die Hand mehrere Symbole zugleich, gilt
  das am **tiefsten** berührte; nach einer Wahl wählt nichts mehr, bis die Hand frei ist. Leuchten im Akzent und Klick. Die **Knöpfe in den Glas-Apps** bleiben bei der
  Fingerspitze (sie stehen eng beieinander; Annahme Hauptlauf 24.10.2026).
- **Nach der Wahl bleiben die Symbole stehen** (Christian 24.10.2026: «werden die aps hineingehoben bleiben diese stehen»): die gewählte **App öffnet frontal etwas
  näher** (0.15 m vor dem Feld, auf der Höhe seiner Mitte; am Sitzungstisch neben dem Tisch), und das **Symbolfeld weicht seitlich neben das Fenster aus** (Christian
  24.10.2026: «Das Symbolfeld weicht bei offener App seitlich neben das Fenster aus.»): die Symbole links der Mitte von Fenster und Ornamenten (Werkzeugleiste, Griff,
  Schliessen, Karte einer Aufgabe; im Blick vom Auge) drehen gemeinsam nach links, die übrigen nach rechts – **um die senkrechte Achse durch das Auge**, so dass ihr
  Abstand zum Auge und damit die **Griffweite bleibt**, ebenso Höhe und Anordnung je Seite –, je so weit, dass das innerste im Blick 3 cm neben der äussersten Kante
  steht; **kein Symbol überdeckt Fenster oder Ornamente** (Test). Wird das Fenster weggewischt, bleibt das Feld stehen, bis das Fenster weg ist; **schliesst die App,
  kehrt das Feld frontal zurück**. Wird das Fenster am Griff verschoben, weicht das Feld mit. Variante `labor.variante.feld_bei_app`: `seitlich` (Vorgabe) | `dahinter`
  (zum Vergleich, die frühere Auslegung): das Feld weicht 0.12 m in die Tiefe zurück und wird gedämpft (Deckkraft 45 %), Symbole, deren Mitte im Blick hinter dem Fenster
  steht, treten ganz zurück (sie schienen sonst als blasse Kreise durch das Glas). Variante `labor.variante.symbolwahl`: `stehen` (Vorgabe) | `ausblenden` (die Symbole
  blenden schnell aus, dann öffnet die App). Ein selbsttätiges Ausblenden gibt es nicht mehr (`symbole.wartezeit_s` = 0; ein Wert über 0 schaltet es wieder ein). Die
  Hebegeste bei offener App blendet die App aus und holt das Feld nach vorn.
- **Geisterhände:** gezögert (beide Handflächen oben, 1.5 s ruhig) – zwei Geisterhände führen die Hebegeste vor dem Körper vor (0.4 m vor den Augen, links und rechts),
  der Hinweis «Beide Handflächen nach oben – und deutlich anheben» steht **zwischen den Händen** in zwei Zeilen (Befund Bilder 02: der Text lief durch die Hände). Stehen
  die Symbole 4 s ohne Wahl (und ist keine App offen), zeigt eine Geisterhand einmal das Wegwischen, der Hinweis «Nach links wischen: schliessen (rechte Hand offen)»
  **unter der Hand**. Die Hand des Wegwischens ist **35° schräg zum Blick** gedreht (Handfläche nach links und etwas vom Auge weg; Befund Prüfbilder 03: genau nach
  links stand sie auf der Kante und war nur ein Strich) – noch innerhalb der Toleranz der Geste.
- **Zeiger mit Tooltip:** nähert sich ein **gestreckter Zeigefinger** einem Symbol bzw. einem Knopf der App (höchstens 12 cm davor) – oder zeigt der Zielstrahl darauf –,
  erscheint ein kleiner **Punkt** auf dem Ziel und eine **feine Linie** von der Fingerspitze (im Akzent der App); das Ziel hebt sich leicht. Nach `zeiger.tooltip_s` (0.6 s,
  Annahme nach HIG «Buttons»: «tooltip after people hover over a button for a moment») erscheint der **Name als Tooltip über dem Ziel** (Befund Bilder 02: darunter
  verdeckte ihn die zeigende Hand). In der App bekommen nur Knöpfe ohne Text einen Tooltip (Werkzeugleiste «Messen ein/aus», «Problem markieren», Schliessen
  «Ausblenden»).

### Wegwischen: eine Geste für alles
Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste. auch da nutzen wir die wischbewegung von rechts nach links»; «Ja, auch offene Glas-Apps werden mit dem
Wegwischen geschlossen; Pinch + Wisch nach vorn entfällt.»
- **Geste:** die **rechte Hand offen, Handfläche nach links** (höchstens `wegwischen.handflaeche_grad` = 50° daneben; seit V1h 60°), **von rechts nach links** wischen – mindestens
  `wegwischen.weg_m` (0.18 m) in höchstens `wegwischen.zeit_s` (0.6 s), die Bewegung höchstens `wegwischen.richtung_grad` (35°; seit V1h 45°) neben «nach links». Gemessen ab dem am
  weitesten rechts liegenden Punkt: **Ausholen nach rechts löst nichts aus**. Ohne Pinch – weder Pinch + Wisch (Blättern, Drehen, Etage) noch der Sprung noch die
  Hebegeste lösen aus (Tests); solange die Hand über ein Ziel wischt, löscht sie keine Messung. Nach dem Ereignis zählt die Geste 0.8 s nicht. Alle Werte **Annahmen V1a**.
- **Ziel**, wenn mehreres sichtbar ist: was die Hand beim Wischen **überstreicht bzw. vor dem sie wischt** (im Blick vom Auge); sonst das **zuletzt Eingeblendete**. So
  geht zuerst das Vorderste – die **offene App** bzw. die **Karte einer Prüfaufgabe** (= Aufgabe abbrechen, Messprotokoll mit «abbrueche» wie bisher) –, beim nächsten
  Wischen das **Symbolfeld**, ebenso der **PC-Bildschirm** (V1c) nach seinem Einblendzeitpunkt. Die Ziele hängen sich über einen Einhängepunkt an (`IWegwischbar`,
  `QuestApp.WegwischZiele`).
- **Wirkung:** das Ziel **läuft mit der Hand nach links mit** (1.4 × der Weg) und gleitet nach dem Ereignis mit einem Klick nach links aus dem Bild; bricht die Geste ab,
  gleitet es zurück. **Pinch + Wisch nach vorn löst nichts mehr aus.** Bei der ersten App steht einmal «Nach links wischen: schliessen (rechte Hand offen).»

### Die App: ein Fenster aus Glas
- **Ein Fenster** (statt der Kacheln aus P5a): Glas, durchscheinend **im Pastellton der App** (siehe «Farben»), stark gerundete Ecken, feiner Glanz an der Oberkante,
  weicher Schatten. Schrift in drei Stufen (dunkel auf hellem Pastell), Titel mittig (Name der App) mit schmalem Strich im Akzent darunter, Text linksbündig. Masse in pt
  wie visionOS (1 pt ≈ 1.2 mm je Meter Abstand, eigene Rechnung; im Griffbereich mit Fingerfaktor 1.5: 0.9 mm), Knöpfe 52 pt hoch, Mitten 60 pt auseinander.
  **Keine Registerleiste** – gewechselt wird über die Hebegeste und die Symbole.
- **Werkzeugleiste** (Ornament) an der Unterkante, leicht davor: **Messen** und **Marker** ein/aus (eingeschaltet im Akzent der App). Darunter der **Griff** (Kapsel:
  Pinch daran und ziehen verschiebt das Fenster) und links daneben der runde **Schliessen**-Knopf (blendet aus). Schliessen geht auch mit dem **Wegwischen**.
- **Farben** (Christian 24.10.2026: «das grau ist nicht so ansprechend. verwende unsere pastelfarben»; eigener Katalog, getrennt von den Gewerkfarben – «gewerke werden nur
  für gewerkeunterschiede gebraucht»): `kataloge/xr_farben.json` (App-Pastellreihe; die App liefert ihn mit, keine Farbwerte im Code). Je App ein Ton plus Akzent (eine
  dunklere Stufe desselben Tons, einmal berechnet und eingetragen), **hell** (Fläche im Pastellton, Schrift #1F1F1F) und **dunkel**: ein **kräftiger dunkler Ton der
  App** – der helle Akzent mit Deckkraft 0.75 über dem dunklen Grund #3E4452 (KICAD «--browser») –, Schrift #F5F5F5, Akzent = der Pastellton (Befund Prüfbilder 03: mit
  0.22 Pastell war dunkel alles grau). Die **App-Symbole bleiben auch dunkel hell** (Pastell-Scheibe mit dunklem Symbol), damit jede App an ihrer Farbe erkennbar bleibt.
  Hell gilt ab einer Helligkeit der Umgebung von 0.3 (Begehen, Passthrough, heller Sitzungsraum), dunkel im Sitzungsraum «tron». Kontrast der Schrift über dem Glas vor
  jedem Hintergrund mindestens 4.5 (WCAG, Test); dunkel sind die Apps mindestens so gut zu unterscheiden wie hell (Test).

  | App | Ton hell | Akzent | Ton dunkel | Quelle |
  | --- | --- | --- | --- | --- |
  | KICAD (Status) | #DDE7EE | #3E5B6F | #3E5568 | Christian 24.10.2026, App-Pastellreihe |
  | Etagen | #D6E6F5 | #32587C | #355372 | Christian 24.10.2026, App-Pastellreihe |
  | Messen | #D9F0E6 | #3C715A | #3D6658 | Christian 24.10.2026, App-Pastellreihe |
  | Marker | #F8E0D2 | #824B2B | #714935 | Christian 24.10.2026, App-Pastellreihe |
  | Tisch | #E6DDF3 | #513A74 | #4C3D6C | Christian 24.10.2026, App-Pastellreihe |
  | Begehen (Ort) | #F3ECD6 | #786835 | #6A5F3C | Christian 24.10.2026, App-Pastellreihe |
  | Ebenen | #DCEBD9 | #476F3E | #456443 | Vorschlag V1a (salbei), zu bestätigen |
  | Gesten | #E9E3EE | #593E6F | #524068 | Vorschlag V1a (flieder), zu bestätigen |
  | Labor (Aufgabenkarte) | #F1E6EC | #6F3E59 | #634057 | Vorschlag V1a (rosé), zu bestätigen |
  | Übrige (Vorgabe) | #E4E7EB | #3E536F | #3E4F68 | Vorschlag V1a (neutral), zu bestätigen |
- **Symbole wie in KICAD** (Christian 24.10.2026: «nutze die gleiche symbolsprache wie im cad»): eine Quelle – `wwwroot/js/oberflaeche/symbole.js` (Strich) bzw.
  `symbole-revit.js` mit `css/symbole-revit.css` (mehrfarbig, hell und dunkel), dazu `werkzeuge/symbole.js`. `node werkzeuge/quest_symbole.mjs` setzt sie mit Edge headless
  in Texturen um (`src/Hlks.Quest/Assets/symbole/<strich|revit_hell|revit_dunkel>/<name>.png`, 128 px, mit Prüfsumme der Quelle in `symbole.json`); der Node-Test
  `quest-symbole.test.mjs` meldet, wenn ein Symbol in KICAD geändert wurde und die Texturen neu zu bauen sind. Bestehende Schlüssel: `info`, `geschoss`, `bemassung`,
  `problemMarker`, `raumBegehen`, `sichtbarkeit`, `schliessen`, `loeschen`; neu gezeichnet (Strich und Revit): `sitzungstisch`, `gesten`, `haken`; Revit-Fassungen neu für
  `problemMarker` und `raumBegehen`. Die App folgt der Benutzereinstellung `oberflaeche.symbole` (der PC schickt sie mit «willkommen»; ohne PC Strich): Strich in der Farbe
  der Schrift getönt, Revit mit eigenen Farben (auf hellem Glas der helle, auf dunklem der dunkle Satz).
- **Bedienen:** Antippen mit dem Zeigefinger oder mit dem Strahl zeigen und Pinch (wählt beim Loslassen auf demselben Ziel). Nähert sich Finger bzw. Strahl, hebt sich das
  Ziel leicht an und hellt auf (statt des Blicks der Vision Pro), mit Zeiger wie bei den Symbolen; jeder Knopf klickt. Listen blättern mit Pinch + Wisch links/rechts,
  solange die Hand am Fenster ist (Seitenzahl im Titel, z. B. «Etagen · 1/2»); **seit V1d auch mit einem Wisch ohne Pinch auf der Tafel** (Abschnitt «Quest App V1d»).
- **Lage:** im Raum verankert (folgt dem Kopf nicht); kommt nur nach, wenn man weiter als 1.4 m weggeht. Beim Start steht es `apps.seitlich_grad` (30°) links neben dem
  Blick auf Brusthöhe mit «KICAD» (Status); über ein Symbol geöffnet dort, wo die Symbole standen; **am Sitzungstisch neben dem Tisch**, nie über der Platte (Befund
  Prüfbild 24.10.2026). Wechsel Begehen ↔ Sitzungsraum bzw. ein neu angetippter echter Tisch stellt ein sichtbares Fenster an den neuen Platz. **Seit V1d** öffnet es
  immer **mittig** in der Blickrichtung (auch am Tisch, über der Platte) und rückt träge nach (Abschnitt «Quest App V1d»).
- **Glas nach der Umgebung** (HIG «Materials»: «Glass is an adaptive material … to provide contrast»): Fassung (hell/dunkel) und Deckkraft richten sich nach dem
  Hintergrund: die helle Fassung wirkt über ihm mindestens 0.72 hell, die dunkle höchstens `anzeige.glas_helligkeit` (0.42); die Deckkraft ist mindestens
  `anzeige.glas_deckkraft` (0.75) und höchstens 0.95. Die Hintergründe je Umgebung sind Annahmen V1a (heller Sitzungsraum 0.88, Modell beim Begehen 0.55, echter Raum 0.6,
  «tron» 0.03). Die Glasfläche **verdeckt Weltobjekte** dahinter (sie schreibt die Tiefe): Marker, Linien und Beschriftungen hinter dem Fenster scheinen nur schwach durch
  (Befund Durchsicht 24.10.2026). Steht der Kopf einer **Marker-Nadel** im Blick hinter Fenster oder Karte (auch in der Lücke zwischen beiden), zeichnet die App die ganze
  Nadel blass (Befund Prüfbilder 03: der Kopf leuchtete in der Lücke über der Fensterkante, der Stiel schien durch das Glas).
  Weichzeichnung des Hintergrunds gibt es nicht (StereoKit 0.3.11 hat das Bild dahinter nicht; ein eigener Durchgang kostete auf der Quest zu viel Füllrate).
- **Variante «tablet»** (`labor.variante.bedienung`): die Box aus P4 wie bisher (mit den Hinweisen in der Box).

### Hinweise frei schwebend
Christian 24.10.2026: «hinweistexte frei schwebend.» Kurze Hinweise und Statusmeldungen stehen frei im Raum – nicht auf dem Glas, nicht in Kästen:
- **Allgemein** (Status, «Etage 2.OG …», «App geschlossen …»): ruhig **unterhalb der Blickmitte in Griffweite** (0.55 m vor dem Auge, 0.24 m tiefer), im Raum verankert;
  er gleitet erst nach, wenn man sich mehr als 25° wegdreht. Immer nur einer.
- **Bei einer Geste der Geisterhände** neben bzw. unter den Händen in zwei schmalen Zeilen (Befund Bilder 02: der Text lief durch die Hände); **beim Objekt** («Problem-Marker
  gesetzt («Prüfen»).» über dem Kennzeichen der Nadel, «Messung 3: …») knapp über der Stelle. **Am Sitzungstisch** wird eine Meldung, die auf dem Modell stünde, ein
  allgemeiner Hinweis; am Objekt bleiben nur die kurzen Beschriftungen (Messwert, Kennzeichen der Marker).
- **Lesbar ohne Kasten:** weisse Schrift mit weichem dunklem Schein hinter den Buchstaben (die Schrift dunkel in zwei Ringen leicht versetzt) – vor dem hellen Sitzungsraum
  und vor dunklen Wänden; ebenso die Werte an den Messlinien, die Kennzeichen der Marker (im Blick über dem Kopf der Nadel) und die Namen der Fangsymbole. **Im Blick
  gleich gross** (Schrifthöhe mit dem Abstand) und **mindestens 0.3 m vor dem Auge** (Befund Bilder 02: «Messung 3» lag riesig auf der Treppe).
- **Kurz** (höchstens zwei Zeilen, länger wird an einer Wortgrenze gekürzt), **sanft ein- und ausblenden**, nach `hinweis.dauer_s` (4 s) weg. **Nie über Apps, Symbolen,
  Tooltips, der Karte einer Aufgabe, dem Weg laufender Geisterhände und am Tisch dem Modell** – geprüft **im Blick** (Winkel vom Auge, nicht nur in gleicher Tiefe;
  Befund Bilder 02: Hinweise in verschiedener Tiefe überlagerten sich): ein Hinweis rückt darüber (solange unter der Blickmitte) bzw. darunter, sonst seitlich daneben.
  Beim Wechsel Begehen ↔ Sitzungsraum fallen die alten Hinweise weg.
- **Dauerhinweise:** «Echter Tisch: mit dem Zeigefinger kurz auf die Tischplatte tippen.» über dem Tisch, solange er wartet; ein Fehler der Verbindung, solange die App
  ausgeblendet ist. Die **Karte einer Prüfaufgabe** bleibt eine kleine Glaskarte (Ton «Labor»); nur ihr Gestenhinweis «Nach links wischen: abbrechen» schwebt frei darüber.

### Messen in der Brille
- **Einschalten:** App **Messen** (Symbol) › Schalter «Messen» oder in der Werkzeugleiste das Massband. Solange Messen an ist, springt der Pinch nicht (er misst) und kein
  Zielstrahl zum Springen erscheint.
- **Variante `messband`** (Vorgabe): auf den Anfangspunkt zeigen bzw. ihn mit den Fingern berühren, **Pinch halten, aufziehen, loslassen** am Endpunkt (mindestens 0.15 s
  und 2 cm Weg). **`zwei_punkte`:** je ein kurzer Pinch (höchstens 0.8 s, 4 cm Bewegung) auf Anfang und Ende. Die Variante wählt man im Register (Messband | Zwei Punkte)
  oder am PC (`labor.variante.messen`).
- **Lot:** Knopf «Lot» im Register: gemessen wird lotrecht zur Fläche des Endpunkts (sonst des Anfangs) – Abstand zu Decke, Wand oder Boden. Ohne Fläche: «Lot braucht
  eine Fläche (Decke, Wand, Boden)».
- **Fang** (Regeln `Messen/SzenenFang`, auf der Geometrie der GLB): zuerst der Finger (Pinch-Punkt, 3 cm), sonst der Zielstrahl des Systems (3 cm + 3 cm je Meter). Ecken,
  Mitten von Kanten, Kanten, Achsen von Rohren und Formstücken (Zylinder mit mindestens 12 Ecken), Kanäle (Mittelachse), Flächen. Ein **Fangsymbol** zeigt, was gefangen
  ist (Ecke Quadrat, Mitte Dreieck, Kante Raute, Achse Raute mit Strich, Fläche Kreuz), mit dem Namen darunter. Gefangen wird nur, was zu sehen ist: beim Begehen das
  Geschoss am Stand (die IFC-Unterlage, wenn sie geladen ist), am Tisch die gezeigten Geschosse im Schnitt.
- **Am Sitzungstisch** misst man im Modell: die Hand wird über die Lage des Tisches in die Szene gerechnet, Fangradien im Massstab; Werte in echten Millimetern. Eine Hand an
  einem Griff misst nicht.
- **Anzeige:** jede Messung bleibt als Linie mit Endpunkten stehen, der Wert auf einer kleinen Glas-Kapsel über der Mitte (zum Kopf gedreht), bei der neuesten mit
  dx/dy/dz. Die Liste im Register zeigt die Messungen (neueste oben, Nummer, Mass, dx/dy/dz).
- **Löschen:** offene Hand **schnell quer über eine Messlinie** wischen (wie der Prototyp) löscht diese Messung; «Messungen löschen» im Register löscht alle. Höchstens 50
  Messungen (die älteste fällt weg).
- **Kosten:** höchstens ein Strahl je Bild (die aufziehende Hand jedes Bild, sonst die Hände abwechselnd; neu beim Wechsel des Pinch). Am PC mit Test_praxis: beim
  Begehen 0.1 ms je Strahl, am Tisch 2.5 ms (der erste bis 23 ms, er bereitet die Elemente auf); am Tisch reicht der Strahl 4 m. **Seit V1d** mit räumlichem Index
  und im Hintergrund (Abschnitt «Quest App V1d», «Fang»).

### Problem-Marker
- **In der Brille:** Register **Marker** › «Problem markieren» (bzw. Werkzeugleiste), wahlweise vorher eine **Kurznotiz** (Kollision, Fehlt, Prüfen, Zu eng), dann auf die
  Stelle zeigen bzw. sie berühren und **kurz Pinch**. Der Marker steht dort als **Stecknadel** (offen orange, erledigt grau) mit Kennzeichen; Hinweis «Problem-Marker
  gesetzt». Die Liste im Register zeigt alle Marker des Projekts (offen zuerst, Zuständig, Frist).
- **Was gespeichert wird:** Ort (KICAD-mm), Blickpunkt (Kopf und Blickrichtung in KICAD-mm, Modus begehen bzw. sitzungsraum, Massstab), das getroffene Element (Id, GLB-Teil,
  Art), Geschoss, Zeit, Notiz; KICAD ergänzt Benutzer (Name der Kopplung) und Brille.
- **Ablage im Projekt:** KICAD legt je Marker ein Element der Klasse **`ProblemMarker`** an (`POST /api/xr/marker`, Kürzel PM, Schicht Prüfung; mit Paket, Rückgängig und
  Abgleich wie jede Änderung) – alle KICAD-Plätze sehen ihn, im Grundriss als Kreis mit «!» (nur am Bildschirm), in 3D als Kugel. Ohne Verbindung wartet der Marker in der
  Brille (`marker/wartend.jsonl`, höchstens 100) und geht mit der nächsten Verbindung; dieselbe Kennung legt keinen zweiten an. Die App holt die Liste mit
  `GET /api/xr/marker` (ETag, alle 30 s) und legt sie je Projekt ab (Start ohne PC). In den GLB der App stehen die Marker nicht (die App zeichnet sie selbst).
- **Am PC:** Register **XR › Gruppe Koordination › «Problem-Marker» (XM)** öffnet die Palette rechts: Filter (Alle, Offen, Erledigt), Zahl offen/erledigt/überfällig,
  die Liste (Kennzeichen, Status, Notiz, Geschoss, gesetzt von, Zuständig, Frist; überfällige Fristen hervorgehoben). **Klick auf einen Marker** wählt ihn und öffnet die
  3D-Ansicht **am Blickpunkt der Brille** (Kamera am Kopf, Blick auf die Stelle). Darunter Status, Zuständig, Frist und Notiz ändern («Speichern», ein Befehl
  `element_aendern`), «In 3D zeigen», «Löschen» (mit Rückfrage, Rückgängig möglich).
- **BCF:** «Als BCF exportieren …» schreibt die Marker des Filters als **BCF 3.0** (Befehl `bcf_export` mit `klasse: ProblemMarker`, wahlweise `marker: [Kennzeichen]`):
  je Marker ein Topic (Typ «Problem», Status Offen/Erledigt, Titel aus Kennzeichen und erster Zeile der Notiz, Zuständig, Frist, Beschreibung) mit Viewpoint am Blickpunkt
  der Brille und dem betroffenen Element ausgewählt; die Guid bleibt je Marker gleich. Schemaprüfung wie bei den Kollisionen. **Bilder** (Snapshots) enthält das BCF
  nicht – ein Bild der 3D-Ansicht vom Blickpunkt ist ein Vorschlag für später.
- **KI:** `/api/xr/marker` ist eine Rechner-Funktion (`KiEinordnung` › `xr_marker`, kein MCP-Werkzeug); die Marker selbst sind Projektdaten (`elemente_suchen` mit
  `klasse: ProblemMarker`).

### Prüfaufgabe «messen-wand-wand»
- Die App wählt im Raum am Stand (sonst dem 2 m vor dem Blick, sonst dem nächsten) zwei **gegenüberliegende, parallele Wände** (±3°, mindestens 0.8 m auseinander, 0.5 m
  nebeneinander; bevorzugt quer zum Blick), markiert sie mit einem Rahmen und schaltet Messen ein. Das **Sollmass** gilt zwischen den Flächen, die man vom Raum aus sieht:
  Strahlen aus der Mitte des Raums auf die Wände (Befund Prüflauf Test_praxis: lag der Grundriss auf der Aussenseite einer Wand im Raum, war das Mass vorher 250 mm zu
  gross). Gelingt mit einer Messung von Wand zu Wand (je höchstens 5 cm neben der Fläche) höchstens 2 cm neben dem Sollmass (Katalog, Annahme P6).

### Sitzungsraum hell
- Statt des dunklen Raums eine **ruhige, helle Umgebung** (HIG «Immersive experiences»): Kuppel (40 m) mit Verlauf vom hellen Horizont zu einem leicht bläulichen Zenit,
  immer eine Bodenfläche, ein heller Tisch mit weichem Kontaktschatten; Licht in den Farben der Netze, keine Bewegung. Unter dem Modell eine **Bodenplatte aus Glas** wie bei
  einem «Volume», die sanft leuchtet, wenn eine Hand nahe ist bzw. einen Griff hält. Kosten: gut 1 400 Dreiecke, 5 Zeichenaufrufe. Mit Passthrough nur ein dezenter heller
  Rahmen.
- **Variante `labor.variante.umgebung`:** `hell` (Vorgabe) | `tron` (die frühere dunkle Fassung zum Vergleich).
- **Befund «Raum in AR»** (dunkle Platte oben): der Ausschnitt «Raum zeigen» schneidet jetzt unter der Unterseite der Decke über dem Raum, aus der Geometrie (senkrechte
  Strahlen auf Platten, Decken, Dächer; vorher nur, wenn die Oberkante der Platte genau auf der Kote des Geschosses darüber lag).

### Karte der Prüfaufgaben, Geisterhand, Gesten
- **Karte** (Befund Durchsicht 24.10.2026): Glas im Ton «Labor», so klein wie der Text, Farbe nur als Akzent (Punkt vor dem Titel: läuft im Akzent, wartet orange,
  abgebrochen blass; Haken bei Erfolg). Mit Fenster steht sie als **Ornament über dem Fenster** (deckt die App nicht), sonst rechts neben dem Blick. Knopf «Aufgabe
  beenden» unten rechts; **Wegwischen** der Karte bricht ebenfalls ab (Messprotokoll mit «abbrueche»); der Hinweis «Nach links wischen: abbrechen» schwebt frei darüber.
- **Geisterhand** führt Gesten neben den Bedienelementen vor, nie über ihnen (Befund Durchsicht 24.10.2026; Regel `Geisterhand.Neben`), dazu die Hebegeste (zwei Hände)
  und das Wegwischen (rechte Hand, Handfläche nach links). Im Prüflauf am PC zeichnet die App die Maus-Hände des Simulators nicht (Befund Bilder 02: die weisse Hand über
  Werkzeugleiste und Griff war die Simulatorhand) und führt beim «Zögern» keine Hebegeste vor (die Simulatorhände stehen still).
- **Gesten** (Tabelle in der App KICAD › «Gesten»): neu «Pinch halten, ziehen, loslassen (Messen ein)», «Offene Hand schnell quer über eine Messlinie», «Pinch auf eine
  Stelle (Marker setzen)», «Pinch am Griff unter dem Fenster», «Beide Handflächen nach oben, deutlich anheben» (holt die Symbole; ersetzt «Linke Handfläche zum
  Gesicht drehen»), «Wegwischen: rechte Hand offen, Handfläche nach links, von rechts nach links» (schliesst das Vorderste); Pinch + Wisch links/rechts blättert, wenn die
  Hand am Fenster ist; **Pinch + Wisch nach vorn entfällt**.

### Stellschrauben und Varianten (neu bzw. geändert)
| Schlüssel | Vorgabe | Bereich | Wirkung |
| --- | --- | --- | --- |
| `apps.seitlich_grad` | 30° | 0 … 90 | so weit links neben dem Blick erscheint das Fenster beim Holen |
| `anzeige.glas_deckkraft` | 0.75 | 0.3 … 1 | Deckkraft des Glases (im hellen Raum deckt es bei Bedarf mehr) |
| `anzeige.glas_helligkeit` | 0.42 | 0.2 … 0.6 | so hell wirkt das Glas der dunklen Fassung höchstens über dem Hintergrund |
| `labor.variante.messen` | messband | messband, zwei_punkte | seit V1a wirksam |
| `labor.variante.umgebung` | hell | hell, tron | Umgebung des Sitzungsraums |
| `hebe.weg_m`, `hebe.zeit_s`, `hebe.handflaeche_grad`, `hebe.gleichzeitig_s` | 0.15 m, 0.9 s, 45°, 0.35 s | 0.05 … 0.5, 0.2 … 3, 10 … 80, 0.05 … 1.5 | Hebegeste |
| `wegwischen.weg_m`, `wegwischen.zeit_s`, `wegwischen.handflaeche_grad`, `wegwischen.richtung_grad` | 0.18 m, 0.6 s, 50°, 35° (seit V1h 60°, 45°) | 0.05 … 0.6, 0.2 … 2, 10 … 80, 5 … 80 | Wegwischen (ersetzt `schieben.*`) |
| `symbole.abstand_m`, `symbole.hoehe_m`, `symbole.feldhoehe_m`, `symbole.reihen` | 0.5 m (seit 25.10.2026, vorher 0.6), 0.32 m, 0.6 m, 3 | 0.3 … 1, 0 … 0.8, 0.2 … 1, 1 … 3 | Symbolfeld frontal |
| `symbole.streuung`, `symbole.wartezeit_s` | 1, 0 s (nie) | 0.6 … 1.6, 0 … 120 | Spaltenabstand × Faktor; selbsttätig ausblenden |
| `symbole.beruehr_rand_m`, `symbole.beruehr_sperre_s` | 0.01 m, 0.6 s | 0 … 0.05, 0 … 3 | Berührung mit der ganzen Hand |
| `zeiger.tooltip_s` | 0.6 s | 0 … 3 | Verzögerung des Tooltips |
| `hinweis.dauer_s` | 4 s | 1 … 15 | so lange steht ein schwebender Hinweis |
| `labor.variante.symbole` | verteilt | verteilt, bogen | Anordnung der Symbole |
| `labor.variante.symbolwahl` | stehen | stehen, ausblenden | was die Symbole nach der Wahl einer App tun |
| `labor.variante.feld_bei_app` | seitlich | seitlich, dahinter | wo das Symbolfeld bei offener App steht (Christian 24.10.2026: seitlich) |
| `symbole.tisch_abstand_m` | 0.06 m | 0 … 0.3 | am Sitzungstisch Luft unter der untersten Symbolreihe |

### Prüfen
- **Am PC:** `werkzeuge\quest_pruefen.ps1 [-Projekt <Test_praxis>] [-Pruefbilder <ordner>]` meldet in jeder Zeile «P4 ok» zusätzlich «V1a: Glas=ok, Umgebung=ok, Fang=ok
  (…, ms je Strahl), Messen=ok (zwei Punkte, Lot, Wisch löscht), Marker=ok (…)» und am Tisch «Fenster am Tisch=ok», «Decke über dem Raum=ok (Schnitt oben …, Unterseite
  der Decke …)»; Lauf 1 spielt «messen-wand-wand» (eine Messung daneben, eine von Wand zu Wand) und gescriptet die Symbole: Hebegeste mit gedachten Händen, eine gedachte
  Fingerspitze vor «Messen» (Zeiger, Tooltip), eine gedachte flache Hand berührt «Messen» mit der Handfläche, die App öffnet vor dem Feld, Wegwischen der App (das Feld
  kommt nach vorn), dann der Symbole, danach mit der Variante «dahinter» das Feld hinter dem Fenster – Zeile «Symbole=ok (…, Feld seitlich neben App und Ornamenten ja, …)»; «Apps=ok (Symbole …)» prüft die Wahl über
  die Regeln, dass Pinch + Wisch nach vorn nichts mehr auslöst und das Wegwischen über die Einhängepunkte. Vor den Prüfbildern räumt der Lauf Hinweise, Geisterhände
  und die Karte einer fertigen Aufgabe aus den Schritten davor weg (Befund Prüfbilder 03). Das Skript prüft mit `GET /api/xr/marker`, dass die Marker von Lauf 1, der wartende aus Lauf 2 (ohne PC) und der am Tisch aus
  Lauf 3 im Projekt angekommen sind. **Port:** ist 5130 belegt (Desktop-KICAD), nimmt der Prüfserver selbständig den ersten freien Port von 5139 abwärts und öffnet den
  Netzzugang dort (`HLKS_XR_DESKTOP_PORT`, nur im Testlauf; Schalter, Kopplungen und Zertifikat des Desktops bleiben unberührt). Mit `-Pruefbilder` dazu
  `glas_apps.jpg`, `glas_marker.jpg`, `messen.jpg`, `marker.jpg` (Bild ohne Apps), `hebe_halb.jpg`, `symbole.jpg` (Feld frontal, stehend), `symbole_revit.jpg` (Satz
  «revit»), `symbol_tooltip.jpg`, `symbol_hand.jpg` (Handfläche am Symbol), `app_nach_wahl.jpg` (App offen, Feld seitlich daneben), `app_wegwischen_halb.jpg`,
  `wegwischen_halb.jpg` (Symbole gleiten nach links), `feld_dahinter.jpg` (Variante «dahinter» zum Vergleich), `hinweis_begehen.jpg`, `glas_apps_sitzung.jpg`, `glas_tisch.jpg`, `marker_sitzung.jpg`, `symbole_sitzung.jpg`
  (Feld am Tisch, sitzend), `symbole_sitzung_seite.jpg` (dasselbe von der Seite: Luft über der Tischplatte), `hinweis_sitzung.jpg`, `farben_dunkel_symbole.jpg`, `farben_dunkel_etagen.jpg`, `farben_dunkel_tisch.jpg` (dunkle Fassung im Sitzungsraum
  «tron»), `sitzung_umgebung.jpg` (nur lokal).
- **Tests:** Xr `V1aMessenTests` (Fang: Ecke, Kante, Mitte, Achse von Rohr und Kanal, Fläche, Strahl, Sichtbarkeit; Messband und zwei Punkte, Lot, Löschen), `V1aLaborTests`
  (Wandpaar, Sollmass von der sichtbaren Fläche, Prüfung, Lauf), `V1aMarkerTests` (Format, Ablage, Liste), `V1aGlasTests` (Masse, Inhalt, Fensterteile, Verankerung,
  neben dem Tisch, Hinweise, Strahl, Umgebung, Geisterhand neben der Leiste, Stellschrauben), `V1aSymboleTests` (Hebegeste, Wegwischen und sein Ziel, Fehlauslösungen,
  weder Pinch + Wisch noch Drehen noch Sprung, Symbolfeld frontal, am Tisch über der Platte, nach der Wahl, Berührung mit der ganzen Hand, Tooltip über dem Ziel, Zeiger,
  Überdecken im Blick, Hinweise, Stellschrauben, Luft über der Tischplatte, Feld seitlich, Geisterhand schräg), `V1aFarbenTests` (Katalog der App-Farben, Kontrast,
  Fassung und Deckkraft nach der Umgebung, dunkle Fassung mit Farbe, Symbole immer hell, Texturen der Symbole),
  `V1aSitzungTests` (Decke über dem Raum, heller Hintergrund, Gestentabelle); Kern `ProblemMarkerTests` (Klasse, Grafik, BCF mit Schemaprüfung); Web `XrMarkerTests`
  (POST/GET, ETag, Kennung doppelt, ungültige Werte, Netzzugang, KI-Einordnung), `XrKanalTests` («willkommen» mit Symbolsatz), `XrZertifikatTests` (Port des
  Prüfservers); Node `problemmarker.test.mjs` (Liste, Filter, Zeilen, Frist, BCF-Parameter, Kamera), `quest-symbole.test.mjs` (Texturen passen zur Quelle),
  `symbole-revit.test.mjs` (neue Revit-Fassungen).
- **Nur mit der Brille prüfbar:** wie sich das Messband mit echten Händen anfühlt (Pinch halten, Zugweg 2 cm, Klick 0.8 s), ob der Fang am Strahl genug trifft (3 cm + 3 cm/m)
  und das Fangsymbol lesbar ist; die Wischgeste über eine Messlinie; Marker setzen mit kurzem Pinch; Lesbarkeit und Kontrast des Glases und der Pastelltöne im echten Raum
  (Passthrough), im hellen und im dunklen Sitzungsraum; Lage des Fensters (30°) und der Karte über dem Fenster; die Kosten des Fangs auf der Quest (am PC 2.5 ms je Strahl
  am Tisch) und die Bildrate mit Glas, Texturen und Umgebung; die Hebegeste, die Berührung mit der ganzen Hand (Fehlwahl beim Hineinheben?) und das Wegwischen mit echten
  Händen (Schwellen, Fehlauslösungen, Ausholen), ob das Feld in 0.6 m Abstand und 0.6 m Höhe gut erreichbar ist (oberste Reihe), Lesbarkeit der Hinweise ohne Kasten;
  ob die echten Hände vor Symbolen und Fenstern richtig erscheinen (sie zeichnen nach der Bedienung; im Simulator gibt es keine Hände).
- **Anleitung:** `zb-quest-v1a-symbole`, `zb-quest-v1a-messen`, `zb-quest-v1a-marker`, `zb-problemmarker-palette` in `berichte/anleitung/inventar.json`.

## Modul XR: XR-Labor (Quest App P6, `entwurf/modul-xr.md`, Prototyp Interface-Labor, Zeile P6; Entscheid Christian 23.10.2026: «Prüfung: kurz gemischt», «Deutsche Fassungen der Fragebogen nur mit Quelle»)

**Entfällt seit dem 26.10.2026:** Das XR-Labor ist ein eigenes Programm im Projekt HK_XR (Desktop-Icon «XR-Labor»); KICAD hat keinen Knopf und keinen Dialog mehr dafür. Der folgende Text beschreibt den Stand davor.

Der Dialog **«XR-Labor …»** (Register **XR › Gruppe Labor**, Kürzel XL, Aktion `ansicht.xrLabor`, Symbol `xrLabor`, immer verfügbar) ist die Werkbank für das Interface-Labor der Quest App: Er stellt die Bedienung der App live von KICAD aus ein, startet
Prüfaufgaben, wertet die Messprotokolle aus und nimmt den Fragebogen nach dem Test auf. **Reine Rechner-Funktionen** (kein Befehl, keine Abfrage des Kerns, das Modell wird nicht berührt), für die KI **verboten**
(`KiEinordnung.RechnerFunktionen` › `xr_labor`), kein MCP-Werkzeug. Code: `XrLaborKatalog.cs` (Kataloge, Prüfung beim Laden, Form der Profile), `XrLaborAuswertung.cs`, `XrFragebogen.cs` (Katalog, Regeln, Ablage), `XrLaborEndpunkte.cs` (Endpunkte);
Oberfläche `wwwroot/js/oberflaeche/xr-labor-dialog.js` (DOM), `xr-labor-text.js` (reine Hilfen), `css/quest.css` (Klassen `xl-…`). **Keine Normwerte:** alle Werte der Kataloge sind Einstellungen der Bedienung (Annahmen, je Eintrag mit «herkunft»).

### Der Vertrag für die App (P5): vier Kataloge in `kataloge/`

Alle vier tragen `art` (`hlks-xr-stellschrauben` | `-pruefaufgaben` | `-varianten` | `-fragebogen`) und `fassung: 1`; `XrLaborKatalog.Laden` prüft sie streng (unbekannte Art, doppelte Schlüssel, Vorgabe ausserhalb von min/max, unbekannte Gruppe, Querbezüge
zwischen Aufgaben und Varianten, Länge der Anleitung …) und wirft einen `HlksFehler` mit Datei und Stelle (der Endpunkt antwortet 400 mit dieser Meldung, nie ein stilles Weglassen). Neu gelesen wird, sobald sich eine Datei ändert.

- **`xr_stellschrauben.json`** – `gruppen[{ id, titel }]`, `muster[{ muster, beschreibung, quelle, status }]` (die Schlüssel `labor.variante.<name>` und `labor.aufgabe`, die nicht einzeln stehen) und `stellschrauben[]`. Je Stellschraube:
  `schluessel` (wie der Kanal: `^[a-z][a-z0-9_.-]{0,63}$`), `gruppe`, `titel`, `beschreibung`, `art` (`zahl` | `ja_nein` | `auswahl`), bei `zahl` `einheit`, `min`, `max`, `schritt`, bei `auswahl` `auswahl[{ wert, titel }]`, `vorgabe`, `herkunft`
  (z. B. «Annahme P4a», «Brillentest 23.10.2026»), `status`: **`app`** (die App versteht sie heute: `begehen.ziel_ruhe_s`, `begehen.ziel_ruhe_radius_m`, `begehen.ziel_verzoegerung_s`, `begehen.drehen_sperre_s`, `haltung.schwelle_m`, `haltung.hysterese_m`,
  `haltung.haltezeit_s`; Vorgaben und Grenzen gleichen dem Code in `HandBegehen.cs`/`DrehenSchalter.cs`, ein Test prüft das) oder **`folgt_p5`** (`begehen.drehwinkel_grad`, `anzeige.skala` 1.35, `apps.groesse_m`, `apps.abstand_m`, `apps.anordnung`, `wisch.mindestweg_m`,
  `wisch.mindesttempo_ms`, `wisch.richtung_toleranz_grad`, `pinch.schwelle_an_m`, `pinch.schwelle_aus_m`, `fang.radius_m`, `komfort.tunnelblick`, `komfort.tunnelblick_staerke`, `komfort.ton`, `sitzung.zoom_min`, `sitzung.zoom_max`). Vorgaben der neuen Werte
  stammen, wo es sie gibt, aus dem Code der Logik (`XrWischLogik`, `XrTabletLogik`, `XrMessLogik`, `XrSitzungLogik`); die übrigen (`apps.*`, `komfort.*`) sind **Annahmen P6**, die P5 beim Einbau bestätigt oder ändert.
  **Seit P5b** versteht die App alle: jede Stellschraube und beide Muster tragen `status: app`, die Vorgaben gleichen dem Code (ein Test vergleicht alle), dazu `roentgen.*` und `sitzung.schnitt_*` (Abschnitt «Quest App P5b»); die Marke «folgt P5» erscheint nicht mehr.
  Ein Wert `null` in der Nachricht `stellschraube` bedeutet: zurück auf die Vorgabe.
- **`xr_pruefaufgaben.json`** – `messgroessen[{ id: zeit | fehlgriffe | abbrueche, titel, einheit, beschreibung }]` und `aufgaben[]`: `id` (`^[a-z][a-z0-9-]{0,39}$`: `teleport-markierung`, `drehen-90`, `etage-wechseln`, `modell-greifen-zoomen`,
  `raum-ar-oeffnen`, `tablet-wert-ablesen`, `messen-wand-wand`), `titel`, `anleitung` (höchstens 300 Zeichen, für die Brille), `ort`, `varianten[]` (welche Varianten für die Aufgabe zählen), `messgroessen[]`, `erfolgskriterium` (Zahlen darin sind Annahmen P6), `fehlgriff` (P5b, optional, höchstens 300 Zeichen: was die App als Fehlgriff zählt),
  `herkunft`, `status` (`p5`: die App spielt sie ab; `folgt_v1`: gesperrt – seit V1a keine mehr, `messen-wand-wand` ist `p5`).
- **`xr_varianten.json`** – `varianten[{ name, titel, beschreibung, optionen[{ id, titel }] (mindestens zwei, A/B), vorgabe, herkunft }]`: `bedienung` (`apps` | `tablet`), `teleport` (`zeigen_pinch` | `stick`), `etage` (`wisch_modell` | `app`), `messen` (`messband` | `zwei_punkte`),
  seit V1a `umgebung` (`hell` | `tron`, Sitzungsraum).
- **`xr_fragebogen.json`** – nur am PC (die App braucht ihn nicht): Teile `komfort` (eigene Fragen mit Wortlaut), `sus` (10 Aussagen, `polaritaet` abwechselnd positiv/negativ) und `nasa_tlx` (6 Skalen `mental`, `physical`, `temporal`, `performance`, `effort`, `frustration`).
  Bei SUS und TLX ist `wortlaut` **null** und `platzhalter` «Wortlaut fehlt – Quelle durch Christian»; trägt jemand einen Wortlaut ein, verlangt der Katalog `wortlautQuelle` (sonst Fehler) und für **alle** Fragen des Teils einen Wortlaut.

**Wie die App sie erhält:** `GET /api/xr/labor/katalog` (Marke `.XrImNetz()`: auf localhost und im Büronetz mit Schlüssel, HTTPS). Antwort `{ art: "hlks-xr-labor-katalog", fassung: 1, schluessel, gruppen, stellschrauben, muster, messgroessen, aufgaben, varianten, profil }`
mit `ETag: "<schluessel>"` (SHA-256 über den Inhalt, 32 Zeichen), `Cache-Control: no-cache`; `If-None-Match` mit diesem ETag → 304 ohne Inhalt. `profil` ist `{ kennung, name, werte{} }` des Profils, das der Benutzer als «gilt für die App» markiert hat (bereinigt: nur Schlüssel des Katalogs
mit gültigem Wert, dazu Varianten mit bekannter Option), sonst `null`; es ändert den ETag. **Wert in der App, in dieser Reihenfolge:** Vorgabe des Katalogs › `profil.werte` › Stellschrauben live (`willkommen.stellschrauben`, danach jede Nachricht `stellschraube`).
Der Fragebogen steht nicht im Katalog der App.

**Varianten und Aufgaben** kommen nicht im Katalog der App «an», sondern **über den Kanal**: KICAD sendet die gewählte Variante als Stellschraube **`labor.variante.<name>`** mit der Option als Wert (an alle verbundenen Brillen, **gemerkt**, auch für neue Verbindungen im `willkommen`) und eine Aufgabe als Stellschraube
**`labor.aufgabe`** mit der Aufgaben-Id als Wert (**nur** an verbundene Brillen, **nicht gemerkt**; Wert `null` = beenden bzw. abbrechen). Kein neuer Nachrichtentyp.

**Messprotokoll (Brille → PC, Nachricht `messprotokoll`):** `aufgabe` (Id, Pflicht), **`varianten`** (neu, optional: Objekt `{ name: option }`, höchstens 12, Name `^[a-z][a-z0-9_]{0,31}$`, Option `^[a-z][a-z0-9_]{0,39}$`) oder wie bisher **`variante`** (Text), `zeitMs` (0 bis 86 400 000), `fehlgriffe`, `abbrueche`
(ganze Zahlen 0 bis 100 000), `erfolg` (true | false, optional), `stellschrauben` (optional, Objekt), `notiz` (optional). Der Server schreibt je Zeile zusätzlich `zeit`, `teilnehmer`, `name`, `geraet`, **`app`** (die **Fassung der App**, die die Brille in `hallo.app` meldet – höchstens 40 Zeichen, z. B. `0.1+70e9bae`:
Version und Commit), `kopplung`, `brille`, `nr`. Rückwärtsverträglich: ohne `varianten` gilt `variante`, ohne beides «ohne Angabe»; ohne `app` gilt «unbekannt». **P5 muss** `hallo.app` mit Version und Commit füllen und je Aufgabe ein `messprotokoll` mit `aufgabe`, `varianten`, `zeitMs`, `fehlgriffe`, `abbrueche`, `erfolg` senden.

### Die Bereiche des Dialogs

Der Dialog passt sich dem Fenster an (`width: min(760px, 100vw − 16px)`, `max-height: calc(100vh − 24px)`), der Inhalt scrollt, die Reiter bleiben oben stehen (`scroll-padding-top`), kein waagrechtes Scrollen; die Browserreihe prüft 900 × 560 und 640 × 420. Oben steht, welche Brille verbunden ist
(`GET /api/xr/teilnehmer`, alle 2 s, nur solange der Dialog offen ist) – Namen und Texte aus dem Netz gehen nur über `esc()` ins HTML. Der letzte Wert eines bewegten Reglers wird beim Schliessen noch gesendet.

1. **Stellschrauben:** Liste nach Gruppen. Je Zeile Titel, Schlüssel, Marke **«folgt P5»** (Hinweis: die App versteht sie erst ab P5; KICAD sendet sie schon), Beschreibung, Regler und Zahlenfeld (min, max, Schritt aus dem Katalog), Schalter bzw. Auswahl, «Gesetzt/Nicht gesetzt: … · Vorgabe: …», Herkunft und der Knopf **«Vorgabe»**
   (sendet `null`). Jede Änderung geht sofort per `POST /api/xr/stellschraube { schluessel, wert }` an die Brille (je Regler höchstens alle 120 ms, der letzte Wert kommt immer an). Eine Zahl ausserhalb von min/max wird nicht gesendet (Meldung mit dem Bereich, das Feld springt zurück). Der aktuelle Wert kommt aus `GET /api/xr/teilnehmer`
   › `stellschrauben` (KICAD merkt die gesetzten Werte für alle Brillen, höchstens 64, und sendet sie bei jeder neuen Verbindung, solange KICAD läuft; **ohne Brille** gilt das Setzen trotzdem, der Kopf sagt es).
2. **Profile:** Name eintragen und **«Aktuellen Satz speichern»**: alle Stellschrauben und Varianten (gesetzter Wert oder Vorgabe) als Benutzereinstellung **`labor.profil.<kennung>`** = `{ name, werte{} }` (Kennung aus dem Namen: Kleinbuchstaben, Ziffern, Unterstrich, höchstens 24 Zeichen; **nicht** unter «xr.», das ist für den Zugang je PC reserviert).
   Ein gleicher Name fragt vor dem Überschreiben zurück. **«Laden»** sendet alle Werte; **«Für die App festlegen»** setzt **`labor.profil_aktiv`** = Kennung (Marke «gilt für die App», der Katalog für die App liefert es); **«Löschen»** fragt zurück. Je Profil ein Schlüssel, weil jeder Wert höchstens 4000 Zeichen haben darf (`BenutzerEinstellungen`:
   Bekannt `labor.profil_aktiv`, Form der Profile über `XrLaborProfil.Pruefen`, höchstens 40 Zeichen Name und 64 Werte). Die Einstellungen gelten je Benutzer auf allen seinen Rechnern (OneDrive).
3. **Varianten:** je Variante zwei Optionen (A/B) als Auswahl; `POST /api/xr/labor/variante { name, option }` prüft gegen den Katalog (400 bei unbekanntem Namen oder Option) und sendet `labor.variante.<name>` (gemerkt).
4. **Prüfaufgaben:** Liste mit Titel, Anleitung, Ort, Erfolgskriterium und **Status aus den Messprotokollen** («Noch nicht gemessen» | «Gestartet – wartet auf die Messung der Brille» | «n Läufe · x von y erfolgreich · zuletzt …» | «folgt mit Version 1»). **«Aufgabe starten»** (`POST /api/xr/labor/aufgabe { aufgabe }`; 400 ohne verbundene Brille,
   bei unbekannter Id und bei `folgt_v1`) und **«Aufgabe beenden»** (`aufgabe: null`). Der Status zieht alle 4 s nach, solange der Reiter offen ist.
5. **Auswertung:** `GET /api/xr/labor/auswertung` (nur localhost) liest `%LOCALAPPDATA%\HLKS_CAD\xr\messprotokolle\*.jsonl` (die neuesten 300 Dateien, zusammen höchstens 64 MB; mehr wird gemeldet) und liefert `{ dateien, laeufe, ungueltigeZeilen, abgeschnitten, leer, aufgaben[], fassungen[], fragebogen[] }`:
   je Aufgabe `laeufe`, `erfolge`, `erfolgsquote`, `mittelZeitMs`, `fehlgriffe`/`fehlgriffeMittel`, `abbrueche`/`abbruecheMittel` – **je Variante** (Schlüssel «name=option, …» sortiert, sonst der Text `variante`, sonst «ohne Angabe») und **je Fassung der App** (nach erster Verwendung geordnet);
   `fassungen[]` ist der **Verlauf je Fassung** über alle Aufgaben, mit den Mittelwerten der Fragebogen (SUS, TLX, Übelkeit, Augen, natürlich). Aufgaben des Katalogs stehen auch ohne Läufe da; unbekannte Ids am Ende («nicht im Katalog»). **Ohne Protokolle und ohne Fragebogen** steht ein klarer Leerzustand. Ungültige Zeilen werden übersprungen und gezählt.
6. **Fragebogen:** `GET /api/xr/labor/fragebogen` liefert die Fragen mit `wortlautVorhanden` je Teil; `POST /api/xr/labor/fragebogen { name, app?, sitzung?, varianten?, komfort?{ uebelkeit, augen, natuerlich (je 0 bis 10), text }, sus?[10 × 1 bis 5], tlx?{ 6 × 0 bis 100 }, notiz? }` speichert eine Datei
   `%LOCALAPPDATA%\HLKS_CAD\xr\fragebogen\<JJJJMMTT-hhmmss>_<name>.json` (je PC; im Testlauf unter `%TEMP%`) und berechnet die Punkte. Der Dialog füllt die Fassung der App aus der verbundenen Brille und die gewählten Varianten aus den Stellschrauben.
   - **Komfort** (eigene Fragen, keine Quelle nötig): Übelkeit, Belastung der Augen, «Wie natürlich fühlte sich die Bedienung an?» je 0 bis 10, Bemerkung.
   - **SUS und NASA-TLX:** Die **Bewertungsregeln** sind umgesetzt und mit bekannten Beispielwerten getestet (`XrFragebogenRegeln`): **SUS** – ungerade Aussagen Antwort − 1, gerade 5 − Antwort, Summe × 2.5 (0 bis 100; alle Antworten 3 → 50, beste → 100); **Raw TLX** – ungewichtetes Mittel der sechs Skalen 0 bis 100.
     Quellen: Brooke, J. (1996), *SUS – a «quick and dirty» usability scale*, in: Jordan et al. (Hrsg.), *Usability Evaluation in Industry*, S. 189–194; Hart, S. G.; Staveland, L. E. (1988), *Development of NASA-TLX*, in: Hancock; Meshkati (Hrsg.), *Human Mental Workload*, S. 139–183 – **«Quelle prüfen»** (Angaben aus dem Gedächtnis, nicht gegen die Quellen geprüft).
     **Die Wortlaute fehlen** (Entscheid Christian: «Deutsche Fassungen der Fragebogen nur mit Quelle»): Der Dialog zeigt deutlich **«SUS und NASA-TLX noch ohne Wortlaut»**, die Fragen sind Platzhalter und gesperrt, und der Server nimmt dazu **keine** Antworten an (400), bis der Katalog Wortlaut **und** Quelle enthält. Der Komfortteil funktioniert trotzdem.

### Prüfen

- Web: `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~XrLaborTests"` (Kataloge laden und streng prüfen, Vorgaben gegen den Code der App, Endpunkte auf localhost und im Netzzugang mit ETag/304, Profile, Varianten und Aufgaben an eine Brille, Messprotokoll mit `varianten`, Auswertung aus Beispielprotokollen, Fragebogen, SUS/TLX mit Beispielwerten).
  **In einer Arbeitskopie ausserhalb des Hauptordners** `HLKS_KATALOG` auf `<Arbeitskopie>\kataloge` setzen (sonst nehmen die Tests die Kataloge des Hauptordners).
- Node: `node --test src/Hlks.Web/wwwroot/js/oberflaeche/xr-labor.test.mjs` (Gruppen, Werte, Eingaben, Profile, Status, Auswertung, Fragebogen, HTML-Sicherheit, Verdrahtung – gegen die echten Kataloge).
- Browser: `node src/Hlks.Web/wwwroot/js/oberflaeche/xr-labor.browser.mjs` (Port 5741, CDP 9741; kleines Fenster; nachgestellt sind nur die verbundene Brille, das Setzen der Stellschrauben und der Start einer Aufgabe, alles andere ist echt; der Testordner `%TEMP%\HLKS_CAD\xr_test` wird dabei geleert).
- **Nur mit Brille und App (P5) prüfbar:** ob die App die Stellschrauben, Varianten und Aufgaben versteht und die Messprotokolle mit `app`, `varianten`, `erfolg` sendet; wie sich die Werte anfühlen (die Vorgaben der Gruppen «Schwebende Apps» und «Komfort» sind Annahmen).
  Seit P5b prüft `werkzeuge\quest_pruefen.ps1` am PC, dass die App den Katalog nimmt, Aufgaben abspielt und die Messprotokolle mit `app`, `varianten`, `erfolg` ankommen (Abschnitt «Quest App P5b»); das Gefühl bleibt Sache der Brille.
- **Anleitung:** `zb-xr-labor-stellschrauben`, `zb-xr-labor-profile`, `zb-xr-labor-varianten-aufgaben`, `zb-xr-labor-auswertung`, `zb-xr-labor-fragebogen` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App V1c: Desktop in der Brille (XR-I; Christian 24.10.2026: «ein wisch mit der rechten hand mit der handfläche nach links gerichtet, zieht den verbundenen Desktop in das bild. vr bleibt starr im hintergrund.», «ich habe an die ansicht von der metafunktion gedacht.», «bedienung erfolgt über maus und tastatur des pc's»)

Wie die Desktop-Ansicht von Meta: Der **ganze Bildschirm** des gekoppelten PCs erscheint in der Brille live als grosser, leicht gebogener Bildschirm. **Bedient wird mit
Maus und Tastatur am PC**; die Brille zeigt nur an, der Mauszeiger ist im Bild. **Aus der Brille gehen keine Eingaben an den PC.** Meta-eigene Apps lassen sich nicht
in die App einblenden; die Ansicht ist darum in der App nachgebaut. Regeln ohne StereoKit in `src/Hlks.Xr/Desktop` (`DesktopGeste.cs`, `DesktopAnsicht.cs`,
`DesktopFormat.cs`, `DesktopEinstellungen.cs`, `PruefHand.cs`) und `src/Hlks.Xr/Netz/DesktopVerbindung.cs`; die App in `src/Hlks.Quest/DesktopBildschirm.cs` und
`QuestApp.Desktop.cs`; am PC `src/Hlks.Web/XrDesktop.cs`, `XrDesktopErfassung.cs`, `XrDesktopKodierung.cs`, Anzeige `wwwroot/js/oberflaeche/xr-desktop-status.js`
(Regeln `xr-desktop-text.js`). Keine Normwerte – alle Zahlen sind Einstellungen der Bedienung (Annahmen V1c).

### Gesten (Berichtigung Christian 24.10.2026: «die wischbewegung natürlich von links nach rechts. heraus schieben von rechts nach links.»)
| Geste | Wirkung |
| --- | --- |
| **Rechte Hand offen, Handfläche nach links, Wisch von links nach rechts** (Hineinziehen) | Die Handfläche «zieht» den Bildschirm mit: er kommt von **links** ins Bild, folgt der Hand und setzt sich **frontal** vor den Blick (Abstand `desktop.abstand_m`, 1.1 m; Mitte 10 cm unter Augenhöhe). Bricht die Hand ab, gleitet er zurück. |
| **Dieselbe Haltung, Wisch von rechts nach links** (das gemeinsame **Wegwischen** der App, V1a; Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste») | Der Bildschirm läuft mit der Hand nach links mit und gleitet hinaus; die Übertragung pausiert. Sind mehrere Dinge zu sehen, gilt die Regel von V1a: was die Hand überstreicht, sonst das zuletzt Eingeblendete. |
| **Symbol «Desktop»** im Symbolfeld (Hebegeste, dann berühren; nur mit Kopplung) | holt den Bildschirm frontal – wie eine App |
| **Runder Knopf «×»** links neben dem Griff (Fingerspitze) | wie Wegwischen |
| **Kapsel-Griff** unter dem Bildschirm (Pinch fassen) | Verschieben; der Bildschirm dreht sich dabei zum Kopf. Losgelassen bleibt er im Raum stehen. |
| **Runder Knopf «1/2»** rechts neben dem Griff (nur bei mehreren Monitoren) | nächster Monitor des PCs |

- **Regel Hineinziehen** (`DesktopGeste.cs`): Hand offen, kein Pinch, Normale der Handfläche höchstens `wegwischen.handflaeche_grad` (50°, seit V1h 60°; dieselbe Haltung wie das
  Wegwischen) neben «nach links»; Weg nach rechts mindestens `hineinziehen.weg_m` (0.20 m) in höchstens 0.7 s, mittleres Tempo mindestens `hineinziehen.tempo_ms`
  (0.45 m/s; oder über die letzten 0.20 m – wer halb zieht, kurz hält und zügig weiterzieht, löst aus), quer höchstens 60 % des Wegs, die **linke Hand** bewegt sich
  währenddessen höchstens 8 cm. Der Bildschirm folgt der Hand ab 20 % des Wegs.
- **Wegwischen:** die gemeinsame Geste von V1a (`src/Hlks.Xr/Bedienung/Wegwischen.cs`, Stellschrauben `wegwischen.*`); der Bildschirm meldet sich als wegwischbares Ziel an
  (`IWegwischbar`, Id «desktop», `DesktopAnsicht.Kandidat`). Er läuft mit der Hand so weit nach links mit wie das Symbolfeld (gleicher Winkel im Blick,
  `DesktopAnsicht.MitlaufAnteil`); am Griff gehalten ist er kein Ziel.
- **Gegensperren:** nach dem Hineinziehen ruht das Wegwischen 1 s (das Zurückholen der Hand nach links wischt nichts weg), nach jedem Wegwischen ruht das Hineinziehen 1 s
  (das Zurückholen nach rechts zieht ihn nicht herein).
- **Lösen nicht aus** (Tests `tests/Hlks.Xr.Tests/DesktopGesteTests.cs`, verdrahtet wie in der App): Ausholen vor dem Wisch, das Zurückholen in beide Richtungen, langsam
  oder kurz, beide Hände zugleich, die linke Hand, Pinch + Wisch (Drehen, Blättern, Etage), Röntgen-Hand (Handfläche nach vorn), Faust, die Hebegeste (beide Handflächen
  hoch) und das Wegschieben mit zwei Händen, die Testgesten des Gestenlabors, 80 Zufallsbewegungen. Umgekehrt lösen Hineinziehen und Wegwischen weder Pinch + Wisch noch
  (im Gestenlabor) Hebegeste und Wegschieben aus.
- **Die VR-Szene bleibt starr**, solange der Bildschirm da ist, kommt oder geht und während der Geste: kein Springen (auch kein Strahl), kein Drehen, kein Stick, keine Wische
  (Blättern, Etage), am Sitzungstisch keine Griffe und kein Antippen. Symbolfeld und Glas-Apps bleiben bedienbar.

### Der Bildschirm in der Brille
- **Form:** Bogenlänge `desktop.breite_m` (1.4 m), Höhe nach dem Seitenverhältnis des Monitors, Ausschnitt eines Zylinders mit Radius 2.0 m (fest; der Mittelpunkt liegt
  hinter dem Kopf – «leicht gebogen»).
- **Glas-Stil von V1a** (`GlasZeichner`, eigene Schicht «Hinten»: vor dem Modell – eine nahe Wand schneidet ihn nicht an –, hinter Symbolfeld, Fenstern und Hinweisen):
  gebogener **Glasrahmen** im Ton der App «Desktop» mit weichem Schatten, darunter der **Griff** (Kapsel), links der runde Knopf mit dem Symbol «schliessen», rechts «1/2».
- **Farbe** (`kataloge/xr_farben.json`, Eintrag «desktop», Vorschlag V1c nach der Regel von V1a, zu bestätigen): Ton **#D5ECEC** (ruhiges Petrol-Pastell, Farbton 180° in der
  Lücke zwischen «Messen» und «KICAD»), Akzent **#3E6F6F**, dunkle Fassung Ton **#3E6468**, Akzent #D5ECEC.
- **Symbol** «bildschirm» (Bildschirm auf dem Fuss), neu in `symbole.js` und als Revit-Fassung in `symbole-revit.js` (grauer Bildschirm mit hellblauer Fläche); Texturen
  mit `werkzeuge/quest_symbole.mjs`.
- **Wie eine App** (Auftrag V1c, Entscheid Hauptlauf): es ist immer nur ein Fenster offen – kommt der Bildschirm (Geste oder Symbol), geht die offene Glas-App; öffnet sich
  eine Glas-App, gleitet der Bildschirm hinaus. Steht das **Symbolfeld**, weicht es seitlich neben den Bildschirm aus (Variante `labor.variante.feld_bei_app` = «seitlich»;
  «dahinter» zum Vergleich); wird er weggewischt, kehrt das Feld schon zu Beginn der Geste frontal zurück (ebenso beim Wegwischen einer Glas-App). **Hinweise** stehen nie
  vor dem Bildschirm (sie weichen aus wie vor Apps). Der alte Hinweis «Apps: linke Handfläche zum Gesicht drehen» ist weg (Test).
- **Hinweise vor hellem Grund** (heller Sitzungsraum): der dunkle Schein hinter der Schrift ist kräftiger und hat einen dritten, weiteren Ring – ohne Kasten
  (`SchwebeHinweise.ScheinRinge`; Modell und Passthrough wie V1a).
- **Im Raum verankert**, nicht am Kopf. Beim nächsten Hineinziehen setzt er sich wieder frontal.
- **Schärfe:** breitere Monitore verkleinert der PC auf 1920 Punkte Breite (GDI HALFTONE); ohne Mipmaps, linear gefiltert – auf 1.4 m in 1.1 m wird ein 1920 breites
  Bild nur etwa 1.2-fach verkleinert (Annahme V1c, an der Brille zu prüfen).
- **Texturen:** je 512 × 512 Punkte eine Textur («Feld»; 1920 × 1080 = 12 Felder = 12 Zeichenaufrufe, dazu Rahmen mit Schatten, Griff und Knöpfe). Dekodiert wird im
  Hintergrund; je Bild lädt die App höchstens 2 geänderte Felder hoch, der Rest folgt im nächsten Bild.
- **Meldungen:** «Verbinde mit dem Bildschirm des PCs …», «PC nicht erreichbar – neuer Versuch …», «Nicht mit einem PC gekoppelt» auf der Fläche; «Der Bildschirm wurde
  am PC beendet. Zum neuen Verbinden wieder hineinziehen.» als schwebender Hinweis.

### Übertragung (`XrDesktop.cs`; nur über den Netzzugang)
- `GET /api/xr/desktop` – WebSocket, **nur über den Netzzugang** (TLS, Pforte, Kopplung, Marke `XrImNetz`); ohne gültigen Schlüssel 401, ohne WebSocket 400. Die App
  verbindet erst beim Hineinziehen und sendet `start { bildrate, ruheBildrate, maxBreite, monitor?, pausiert? }` (innert 10 s, sonst getrennt).
- **Nachrichten** (Text, `typ` und `fassung` 1): Brille → PC `start`, `pause`, `weiter`, `quittung { nr }`, `monitor { id }`, `bildrate { wert, ruhe? }`, `neu`, `tschuess`;
  PC → Brille `bildschirm { monitore[{ id, name, breite, hoehe, haupt }], monitor, breite, hoehe, kachel, bildrate }`, `fehler { meldung }`, binär die Bilder.
- **Bilder** (Format «HXD1», `src/Hlks.Xr/Desktop/DesktopFormat.cs`): Kacheln 64 × 64; gesendet werden nur Kacheln, die sich seit dem letzten gesendeten Bild geändert
  haben (das erste Bild und nach einem Monitorwechsel alle), je Kachel BGR mit Vorfilter «Sub», alles zusammen mit Deflate gepackt (Bordmittel, kein Paket).
- **Bildrate:** `desktop.bildrate` (12, Ziel 10–15), höchstens 30. Höchstens **2 Bilder ohne Quittung** unterwegs – so passt sich die Rate an WLAN und Brille an.
- **Ruhiger Bildschirm** (Nachtrag V1c: «Ändert sich nichts, erfasst sie seltener … bei Änderung sofort wieder voll»): ändert sich 2 s lang keine Kachel, erfasst der PC nur
  noch mit `desktop.ruhe_bildrate` (2.5/s); die erste Änderung geht sofort hinaus, danach wieder der volle Takt (`XrDesktopDienst.Takt`). Steht der Bildschirm still, geht
  nichts über das Netz.
- **Pause:** ausgeblendet `pause` – der PC erfasst nichts mehr; nach 60 s ohne Bildschirm trennt die App. **Ende** bei Trennung. **Ersetzen:** eine neue Verbindung derselben
  Brille ersetzt die alte (4002); höchstens 2 Brillen zugleich (sonst 1013); Widerruf der Kopplung trennt (4001); Netzzugang aus trennt (1001).
- **Erfassung** (`XrDesktopErfassung.cs`, hinter `IBildschirmQuelle`): unter Windows GDI (BitBlt bzw. StretchBlt in eine DIB-Section), DPI-bewusst je Monitor, Monitore
  über EnumDisplayMonitors (Hauptbildschirm zuerst), Mauszeiger mit DrawIconEx eingezeichnet. **Im Testlauf (`HLKS_TEST`) und mit `HLKS_XR_DESKTOP=testbild` nur ein
  Testbild** (Fenster, Schrift in drei Grössen, Linienmuster, Farbfelder, Zähler, Uhr, kreisender Zeiger; zwei Monitore). Auf Linux kein Bildschirm.
  Gemessen am 24.10.2026 an einem Notebook mit 2880 × 1800 (200 %): eine Erfassung mit Verkleinern auf 1920 × 1200 dauert rund 60 ms (BitBlt in voller Grösse allein
  40 ms) – bei 12 Bildern/s gut die Hälfte eines Prozessorkerns. **Ruhe-Bildrate, gemessen 24.10.2026** (Echt-Test, gleiches Notebook, je 8 s): vorher immer
  12 Erfassungen/s = **60 % eines Kerns**, nachher bei ruhigem Bildschirm 2.5 Erfassungen/s = **12 % eines Kerns**. GDI bleibt vorerst; «DXGI Desktop Duplication»
  (schneller, mit geänderten Rechtecken) steht im Vorrat.
- **Keine Aufzeichnung:** nichts geht auf die Platte; nur das zuletzt gesendete Bild steht im Speicher (für den Vergleich).

### Anzeige am PC
- **Statusleiste:** solange eine Brille mitsieht, **rot «● Brille sieht den Bildschirm»** mit pulsierendem Punkt, Knopf **«Beenden»** und ein **roter Rand** ums ganze
  Fenster; Tooltip mit Brille, Monitor, Bildern/s und kbit/s, bei ruhigem Bildschirm «ruhig – nur 2.5 Erfassungen/s». Ausgeblendet in der Brille: grau «Bildschirm in der
  Brille pausiert» (kein Rand). Ohne Brille: nichts.
- **«Beenden»** → `POST /api/xr/desktop/beenden` (nur am PC): trennt alle Übertragungen mit 4010 «am PC beendet»; die Brille schiebt den Bildschirm hinaus und verbindet
  erst nach dem nächsten Hineinziehen wieder. Meldung «Übertragung an die Brille beendet.».
- `GET /api/xr/desktop/zustand` (nur am PC): `{ erfassung, bildschirme[], verbunden, aktiv, stroeme[{ id, kopplung, brille, name, seit, gestartet, pausiert, monitor,
  monitorName, breite, hoehe, bildrate, ruheBildrate, ruhig, bilderJeSekunde, kbitJeSekunde, bilder, bytes, kacheln, erfassungen, erfassMs, kodierMs }] }`. Die
  Statusleiste fragt alle 2 s.

### Stellschrauben (Katalog `kataloge/xr_stellschrauben.json`, Gruppe «desktop»; im Dialog «XR-Labor»)
`hineinziehen.weg_m` 0.2, `hineinziehen.tempo_ms` 0.45, `desktop.breite_m` 1.4, `desktop.abstand_m` 1.1, `desktop.bildrate` 12, `desktop.ruhe_bildrate` 2.5. Das Wegwischen
stellt V1a ein (`wegwischen.*`, nur einmal im Katalog); die Lage der Handfläche gilt für beide Richtungen (`wegwischen.handflaeche_grad`). Feste Werte (Annahmen V1c,
`DesktopEinstellungen`): Radius 2.0 m, Zeitfenster 0.7 s, andere Hand 8 cm, Gegensperre 1 s, Ruhe nach 2 s, Höhe −0.10 m, grösste Breite 1920 Punkte, Mipmaps aus,
Trennen nach 60 s, 2 Felder je Bild. Grenze des Katalogs 56 Stellschrauben (mit V1a und V1c 55; mit 8 Varianten und der Aufgabe 64 = Grenze des Kanals; seit V1d 80 bzw.
96, Abschnitt «Quest App V1d»).

**KI:** reine Rechner-Funktion, verboten (`KiEinordnung.RechnerFunktionen` › `xr_desktop`), kein MCP-Werkzeug.

### Prüfen
- `tests/Hlks.Xr.Tests/DesktopGesteTests.cs` (Hineinziehen mit dem gemeinsamen Wegwischen wie in der App verdrahtet), `DesktopAnsichtTests.cs` (Lage, Mitlaufen, Griff,
  Knöpfe, Ziel, belegte Flächen, Glasnetz, Format in der App), `DesktopGlasTests.cs` (Farbe nach der Regel von V1a, Symbol, Schein, kein alter Hinweis);
  `tests/Hlks.Web.Tests/XrDesktopTests.cs` (Kodierung hin und zurück, nur Geänderte, Testbild, ohne Kopplung kein Bild, Flusskontrolle, Pause, **Ruhe-Bildrate**, Ende,
  Monitor, Ersetzen, Widerruf, Beenden, Ausschalten, App-Verbindung, Katalog, KI; Echt-Tests der GDI-Erfassung und der **Prozessorlast vorher/nachher** nur mit
  `HLKS_XR_DESKTOP_ECHT=1`); Node `xr-desktop.test.mjs`, `quest-symbole.test.mjs`; Browser `xr-desktop.browser.mjs` (Anzeige und «Beenden»).
- `werkzeuge/quest_pruefen.ps1` Lauf 4 (`-NurDesktop` bzw. `-OhneDesktop`; ist 5130 belegt, nimmt das Skript den Ausweichport 5139 … 5132): Prüflauf der App mit gedachter
  Hand (`--desktop`) gegen das Testbild des Servers, Zeile «P4 ok: Desktop: …» mit Bildzeit ohne und mit Bildschirm, Hochladen, Dekodieren, Bildern/s, kbit/s, Symbol im
  Feld, Feld seitlich, Hinweis nicht davor, «wie eine App»; dazu prüft das Skript die Anzeige am PC. Prüfbilder `desktop_0_…` bis `desktop_6_feld_seitlich.jpg`,
  `desktop_8_leiste_nah.jpg` und `desktop_7_anzeige_pc.png` (KICAD im Edge über CDP, `werkzeuge/seitenbild.mjs`).
- **Nur mit der Brille zu prüfen:** WLAN-Bandbreite, Lesbarkeit der Schrift, 72 Hz mit Bildschirm, wie sich Hineinziehen und Wegwischen anfühlen, ob der Ton «Desktop»
  neben den übrigen Apps passt.

## Modul XR, Quest App V1d: Befunde des ersten Brillentests (Christian 24.10.2026: «das ist schon sehr gut. die apps solten beim hochwischen etwas weiter oben sein. die tafel mit informationen oder auswahlmöglichkeiten sollte immer zentrisch sein. das modell soll nur mit einem griff bewegt und gezoomt werden. nach tippen einer App sollte erneutes auswählen gesperrt sein, dass beim neuanordnen der apps nicht gleich ein nächstes gewählt wird.»)

Dazu aus demselben Test: «die appanordnung muss bei erkanntem sitzen eher etwas weiter oben sein», «wisch mit der linken hand von links nach rechts soll auch den monitor
reinziehen. und wisch mit der linken hand von rechts nach links soll in wieder raus schieben. apps sind nun ca. 20cm zu hoch.» (bestätigt: «Ja: beim Begehen etwas höher,
am Sitzungstisch rund 20 cm tiefer.»), «eine swap bewegung mit der rechten oder linken hand soll in einem aktiven fenster blättern.» Regeln ohne StereoKit in `src/Hlks.Xr`
(`Bedienung/AppSymbole.cs`, `HandBeruehrung.cs`, `Tafel.cs`, `TafelBlaettern.cs`, `Wegwischen.cs`, `Desktop/DesktopGeste.cs`, `Sitzung/EinGriff.cs`,
`Modell/TischStufe.cs`, `Messen/BoxBaum.cs`, `Messen/SzenenFang.cs`, `Messen/FangImHintergrund.cs`), die App in `src/Hlks.Quest` (`SymbolApps.cs`, `GlasApps.cs`,
`QuestApp.Apps.cs`, `QuestApp.Desktop.cs`, `DesktopBildschirm.cs`, `Sitzungsraum.cs`, `ModellDarstellung.cs`, `QuestApp.Messen.cs`, `QuestApp.PruefenV1d.cs`). **Keine
Normwerte:** alle Zahlen sind Einstellungen der Bedienung bzw. Annahmen V1d (Stellschrauben im XR-Labor).

### Symbolfeld höher, sitzend höher, am Sitzungstisch tiefer
- **Begehen** (bzw. ohne Tisch): die senkrechte Mitte des Felds liegt `symbole.hoehe_m` = **0.22 m** unter den Augen (bis V1d 0.32 m; Annahme «etwas» = 0.10 m). Die
  oberste Reihe liegt damit rund 8 cm **über** Augenhöhe (erlaubt: «darf leicht über Augenhöhe liegen»).
- **Sitzend** (Haltung «sitzend» aus der bestehenden Erkennung, `haltung.*`): zusätzlich `symbole.hoehe_sitzend_m` = **0.08 m** höher.
- **Am Sitzungstisch:** Mitte `symbole.hoehe_tisch_m` = **0.52 m** unter den Augen (0.32 + 0.20 m). Die Tischregel greift nur noch, wenn die unterste Reihe in die Platte
  ragte: sie hebt dann die unterste Reihe auf `symbole.tisch_abstand_m` (6 cm) über die Platte und staucht das Feld (höchstens bis 0.24 m Feldhöhe).
- **Feldhöhen gegen das Auge** (Mitten der Symbole mit der leichten Verschiebung je Platz; Test `V1dTests`, Prüflauf «V1d»):

  | Lage | Auge | oberste Reihe | unterste Reihe | Mitte |
  | --- | --- | --- | --- | --- |
  | Begehen stehend | 1.65 m | +0.07 m (Prüflauf +0.08) | −0.55 m | −0.24 m |
  | Begehen sitzend | 1.20 m | +0.15 m (Prüflauf +0.16) | −0.47 m | −0.16 m |
  | Sitzungstisch virtuell (Platte 0.75 m) | 1.20 m | −0.12 m | −0.35 m | −0.23 m |
  | Sitzungstisch echt (Platte 0.639 m, Brillentest) | 1.20 m | −0.23 m | −0.46 m | −0.34 m |

  Am echten Tisch des Brillentests liegt das Feld damit rund 0.20 m tiefer als in V1a (Stand d5a8df6); am virtuellen Tisch (Platte 0.75 m) nur rund 0.09 m, weil dort die
  Platte die unterste Reihe hält.

### Nach der Wahl einer App gesperrt
- Nach dem Berühren eines Symbols zählt **keine Berührung**, bis die Neuanordnung des Felds fertig ist (die Symbole bewegen sich nicht mehr) **und** die Hand alle
  Symbole mindestens `symbole.wahl_frei_s` = **0.3 s** verlassen hat. Bleibt die Hand im Feld, gleitet das Feld unter ihr durch, ohne dass etwas gewählt wird. Gilt
  ebenso für das Symbol «Bildschirm».

### Tafel immer mittig (Glas-Fenster, PC-Bildschirm, Auswahltafeln)
- Jede Tafel öffnet **mittig in der aktuellen Blickrichtung** (Gierwinkel des Kopfs, ohne Nicken) im Abstand `apps.abstand_m`, die Mitte `tafel.hoehe_m` = **0.15 m**
  unter den Augen; am Tisch mindestens mit Werkzeugleiste, Griff und Schliessen über der Platte. Auch am Sitzungstisch mittig (bis V1d neben dem Tisch).
- **Träges Nachführen** (Variante `labor.variante.tafel_nachfuehren` = `traege`, Vorgabe): dreht man sich weiter als `tafel.nachfuehren_grad` = **25°** weg, rückt die
  Tafel ruhig in die Blickmitte zurück (in etwa 0.35 s, bis 2° daneben); nie starr am Kopf. Still, solange eine Hand an der Tafel ist; nicht mehr, wenn man sie am Griff
  hingestellt hat (bis zum nächsten Öffnen). `fest`: sie bleibt nach dem Öffnen stehen. Der PC-Bildschirm folgt derselben Regel.
- Das Symbolfeld weicht weiter seitlich aus; die Karte einer Prüfaufgabe bleibt über der Tafel.

### Ein Griff am Sitzungstisch
- **Ein Griff** (Kugel) in der **Mitte der vorderen Kante** des sichtbar gezeichneten Gebäudes (Hülle in den Achsen des Gebäudes wie P4b), 6 cm nach aussen, 5 cm über der
  Unterkante. Vorne ist die Seite, die in der Lage des Modells am meisten zum Menschen zeigt – nach einer Drehung kommt der Griff wieder nach vorn.
- **Fassen** mit Pinch (höchstens 5 cm vom Griff): das Modell folgt der Hand **in allen Richtungen, auch in der Höhe**, und **dreht mit dem Gierwinkel der Hand**
  (Handgelenk → Mittelfinger; Zittern unter 2° zählt nicht). Der Punkt unter der Hand bleibt unter der Hand.
- **Zoomen** (Variante `labor.variante.zoom_ein_griff`): `zweite_hand` (Vorgabe) – hält eine Hand den Griff, zoomt ein Pinch der anderen Hand über den Abstand der Hände
  (auseinander = grösser) um die Griffhand; `drehknopf` – das Handgelenk am Griff rollen, im Uhrzeigersinn grösser, `sitzung.drehknopf_grad` = **60°** je Verdoppelung
  (unter 3° nichts). Grenzen **1:5 … 1:200** (`sitzung.zoom_min/max`). Die Zweihandgeste mit zwei Griffen entfällt.
- **Geisterhand:** gezögert am Griff zeigt sie das Fassen, das Zoomen mit der zweiten Hand bzw. den Drehknopf (je nach Variante). **Prüfaufgabe**
  «Modell greifen, drehen und zoomen» mit dem einen Griff.

### Wegwischen und Hineinziehen mit beiden Händen
- **Linke Hand:** von links nach rechts **zieht den PC-Bildschirm herein**, von rechts nach links **wischt weg** (gemeinsames Wegwischen für alle Ziele). Die Handfläche
  der linken Hand darf **seitlich in beide Richtungen** zeigen (`wegwischen.handflaeche_grad`), die Richtung des Wischs entscheidet. Die rechte Hand wie bisher.
- Ohne Pinch; Ausholen löst die Gegengeste nicht aus; die Sperren gelten wie bisher und für beide Hände gemeinsam (zwei Hände zugleich lösen einmal aus). Beim
  Hineinziehen muss die andere Hand ruhen; beim Wegwischen darf sie sich nicht **gegenläufig** bewegen (mehr als 8 cm nach rechts: das ist das Wegschieben mit zwei
  Händen auseinander bzw. das Zusammenführen, kein Wegwischen). Kein Konflikt mit Pinch + Wisch, Drehen, Teleport und Hebegeste (Tests).

### Blättern auf der Tafel
- Ein **Wisch ohne Pinch, der auf der Tafel beginnt** (ein Teil der Hand höchstens `tafel.blaettern_abstand_m` = **5 cm** vor der Fläche bzw. 3 cm dahinter, im Umriss),
  **blättert** – rechte oder linke Hand: nach links bzw. oben die nächste Seite, nach rechts bzw. unten die vorige (Listen blättern seitenweise). Ab
  `tafel.blaettern_weg_m` = **8 cm** in höchstens 0.8 s ist es ein Wisch; kürzer bleibt es ein Antippen. Ein Strich, der auf der Tafel begann, bleibt Blättern bis zum
  Ende (Fang); ein Wisch, der im freien Raum begann, behält seine Bedeutung (Wegwischen, Hineinziehen), auch wenn er über die Tafel streicht.
- **Rückmeldung:** die Seite gleitet mit der Hand (halb so weit, höchstens ein Zehntel der Breite, und blendet dabei aus) und es klickt; die Gegenrichtung zählt 0.8 s
  nicht (das Zurückholen blättert nicht zurück). Der **PC-Bildschirm** blättert nicht (Maus und Tastatur am PC).
- **Pinch + Wisch** blättert vorerst weiter wie in P5a (nicht geändert).

### Leistung am Tisch: vereinfachte Unterlage («Tischstufe»)
- Befund Brillentest: «Alle Etagen» mit IFC-Unterlage 600–740 Tsd. Dreiecke, 46–56 Hz. Die Last tragen wenige grosse Netze der Unterlage (Möbel, Geländer), nicht viele
  kleine – ein Weglassen nach Grösse hilft darum kaum. Die App rechnet für die **IFC-Unterlage** im Hintergrund drei **vereinfachte Stufen** (Ecken auf ein Gitter von 1,
  2 bzw. 4 cm zusammengefasst, je Richtung der Normalen getrennt, damit Kanten bleiben) und zeichnet am Tisch die gröbste Stufe, deren Zelle auf dem Tisch höchstens
  `sitzung.vereinfachen_mm` = **1 mm** gross ist, gröber, solange mehr als `sitzung.dreiecke_max` = **350 000** Dreiecke gezeichnet würden (Hysterese 15 %). Die eigenen
  Bauteile von KICAD bleiben unvereinfacht; beim Begehen gilt das volle Netz. Die Stufen entstehen aus dem Zwischenspeicher der App (auch ohne PC) und werden je Bild
  einzeln hochgeladen.
- **Gemessen** (Test_praxis «Alle Etagen» mit Unterlage, PC-Simulator, Prüflauf «V1d Tisch»): vorher **861 291 Dreiecke in 73 Aufrufen** bei jedem Massstab; nachher
  1:19 **341 122 / 43**, 1:50 und 1:200 **153 989 / 34**. Die Bildzeit am PC (7 ms) sagt nichts über die Quest; das EG mit 382 000 Dreiecken lief im Brillentest P4a mit
  72 Hz.

### Fang beim Messen und Zeigen
- **Wer auslöst:** Messen (ein) und «Problem markieren» (bis gesetzt), je Hand der Pinch-Punkt (3 cm) und sonst der Zielstrahl; bis V1d im Bild, jede Hand abwechselnd.
- **Räumlicher Index:** einmal je Modell im Hintergrund (Fangdaten aller Elemente, Hüllkörper-Bäume für Stücke, Anschlüsse, Ecken und Strecken); bis er steht: «Fang wird
  vorbereitet …». **Im Hintergrund:** das Bild stellt höchstens eine Anfrage je Hand und nimmt das letzte fertige Ergebnis (in der Regel ein Bild alt); hat sich die
  Hand kaum bewegt (2 mm × Massstab, 0.3°), fragt es nicht neu. Gleiche Ergebnisse wie bis V1a (Test gegen die alte Suche).
- **Gemessen am PC** (Test_praxis, `XrV1dEchtTests`, 150 Abfragen je Art): am Tisch 1:50 je Strahl vorher ⌀ 40.9 ms (99 % unter 229 ms), mit Index ⌀ 0.17 ms
  (99 % unter 0.71 ms, warm ⌀ 0.12 ms, 99 % unter 0.50 ms); Finger am Tisch 2.5 → 0.03 ms; Strahl beim Begehen 1.07 → 0.08 ms; Index 1.0 s. Im Bild bleibt nur das
  Stellen der Anfrage (Prüflauf: ⌀ 0.006 ms).

### Stellschrauben und Varianten (neu bzw. geändert)
| Schlüssel | Vorgabe | Bereich | Wirkung |
| --- | --- | --- | --- |
| `symbole.hoehe_m` | 0.22 m (bis V1d 0.32) | 0 … 0.8 | Mitte des Felds unter den Augen beim Begehen |
| `symbole.hoehe_sitzend_m` | 0.08 m | 0 … 0.4 | sitzend so viel höher |
| `symbole.hoehe_tisch_m` | 0.52 m | 0 … 1 | Mitte unter den Augen am Sitzungstisch |
| `symbole.wahl_frei_s` | 0.3 s | 0 … 2 | Sperre nach der Wahl: so lange muss die Hand die Symbole verlassen haben |
| `tafel.hoehe_m` | 0.15 m | 0 … 0.6 | Mitte der Tafel unter den Augen |
| `tafel.nachfuehren_grad` | 25° | 5 … 90 | ab so viel Wegdrehen rückt die Tafel nach |
| `tafel.blaettern_abstand_m` | 0.05 m | 0.01 … 0.2 | Hand höchstens so weit vor der Tafel: Blättern |
| `tafel.blaettern_weg_m` | 0.08 m | 0.03 … 0.3 | Mindestweg des Blätterns (kürzer = Antippen) |
| `sitzung.drehknopf_grad` | 60° | 20 … 180 | Rollen je Verdoppelung (Variante «drehknopf») |
| `sitzung.vereinfachen_mm` | 1 mm | 0 … 5 | grösste Zelle der vereinfachten Unterlage auf dem Tisch |
| `sitzung.dreiecke_max` | 350 000 | 50 000 … 2 000 000 | Dreiecke am Tisch höchstens (sonst gröbere Stufe) |
| `labor.variante.tafel_nachfuehren` | traege | traege, fest | Tafel träge nachführen bzw. stehen lassen |
| `labor.variante.zoom_ein_griff` | zweite_hand | zweite_hand, drehknopf | Zoomen am einen Griff |

Feste Werte (Annahmen V1d): Feld mindestens 0.24 m hoch; Tafel folgt in 0.35 s bis 2°; Blättern ab 2 cm «läuft», 0.8 s Zeitfenster, Ende nach 0.35 s Ruhe bzw. 15 cm von
der Fläche, Gegenrichtung 0.8 s gesperrt, im freien Raum begonnen ab 3 cm seitlich im Vorlauf von 0.2 s; Griff Drehen-Totzone 2°, Rollen-Totzone 3°; Wegwischen
gegenläufige Hand 8 cm. Grenzen: Katalog höchstens **80** Stellschrauben (65 belegt), Kanal und Profil **96** Werte (mit 10 Varianten und der Aufgabe 76).

### Prüfen
- **Tests:** Xr `V1dTests` (Feldhöhen mit Tabelle, Sperre auch mit der Hand im gleitenden Feld, Tafel mittig und träge, PC-Bildschirm, Griff an der vorderen Kante, ein
  Griff bewegt und dreht, Zoom mit der zweiten Hand und als Drehknopf, linke Hand für Wegwischen und Hineinziehen, gegenläufige Hand, Blättern mit beiden Händen und
  gegen Antippen, Gestentabelle, Tischstufe, Fang mit Index gleich wie bis V1a, Fang im Hintergrund, Stellschrauben, Geisterhand), `DesktopGesteTests` (beide Hände
  verdrahtet wie in der App); Web `XrV1dEchtTests` (Test_praxis: Tischstufe unter dem Budget, Fang vorher/nachher), `XrLaborTests` (Grenzen 96, Vorgaben V1d).
- **Am PC:** `werkzeuge\quest_pruefen.ps1 -Projekt <Test_praxis> -Pruefbilder <ordner>` meldet in Lauf 1 und 2 «V1d=ok (Feld: oberste Reihe stehend …, sitzend …;
  Sperre nach der Wahl ja, Feld bewegt sich ja, kein zweites unter der Hand ja, danach frei ja; Tafel beim Öffnen 0.0° neben der Mitte, nach 50° Drehung 2.0° …; Blättern
  auf der Tafel ja …)» und in Lauf 3 «V1d Tisch=ok (ohne/mit Tischstufe je Massstab: Stufe, Dreiecke, Aufrufe, Bildzeit; ein Griff: fassen, heben, drehen ja, zweite Hand
  zoomt ja, Drehknopf ja; Fang am Tisch … im Hintergrund 100 gleich …)». In der V1d-Folge zählen nur die gedachten Hände (die Maus-Hand des Simulators liegt, wo die
  Maus ist). Prüfbilder `v1d_feld_stehend(_seite).jpg`, `v1d_feld_sitzend(_seite).jpg` (rote Linie = Augenhöhe), `symbole_sitzung(_seite).jpg` (sitzend am Tisch),
  `v1d_tafel_offen.jpg`, `v1d_tafel_folgt.jpg`, `v1d_tafel_mittig_nach_drehen.jpg`, `v1d_blaettern_halb.jpg`, `v1d_griff_gefasst_gedreht.jpg`,
  `v1d_griff_zoom_zweite_hand.jpg`, `v1d_griff_drehknopf.jpg`, `v1d_geist_zoomen.jpg`, `v1d_geist_drehknopf.jpg`, `v1d_alle_etagen_vorher|nachher_1zu19|1zu50.jpg`
  (nur lokal).
- **Nur mit der Brille prüfbar:** ob die neuen Höhen passen (stehend, sitzend, am echten und am virtuellen Tisch), ob die Sperre nach der Wahl sich natürlich anfühlt,
  das träge Nachführen (Schwelle 25°), der eine Griff mit echten Händen (Gierwinkel der Hand, Zittern, Drehknopf gegen zweite Hand), Blättern gegen Antippen und gegen
  Wegwischen mit echten Händen, die linke Hand (Handfläche seitlich), 72 Hz bei «Alle Etagen» mit der vereinfachten Unterlage und Passthrough, die Zeit des Fangs
  auf der Quest (Index im Hintergrund, Anfrage je Bild).
- **Anleitung:** `zb-quest-v1d-feld`, `zb-quest-v1d-tafel`, `zb-quest-v1d-griff`, `zb-quest-v1d-haende` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App V1e: Befunde des zweiten Brillentests (Christian 24.10.2026)

Christian: «alle ein und aus wisch gesten sollen nur einen anstoss brauchen. momentan brechen einblenden von apps und monitor, sowie der apps oft ab. weil die instinktive
bewegung zu kurz ist.» – «die etagen auswahl geht über mehrere seiten da wäre besser die liste zu vergrössern.» – «modell auf tisch soll einen verschieben punkt erhalten,
und einen punkt für drehen und vergrössern diese bewegung dreht immer um das zentrum. beide punkte sollen farblich unterschiedlich sein, und ein kleines symbol beim
greiffen erhalten.» – «apps sollen etwas mehr glanz erhalten.» – «apps mit mehr abstand zu den popup tafeln», «die apps bei grösseren abständen in einem kleineren radius
um den betrachter anornen». Regeln in `src/Hlks.Xr` (`Bedienung/Anstoss.cs`, `Bedienung/EtagenListe.cs`, `Sitzung/ZweiPunkte.cs`, `Bedienung/AppSymbole.cs`,
`Bedienung/Geisterhand.cs`), die App in `src/Hlks.Quest` (`Sitzungsraum.cs`, `QuestApp.Apps.cs`, `GlasApps.cs`, `GlasZeichner.cs`, `Assets/glas.hlsl`,
`QuestApp.PruefenV1d.cs`, `QuestApp.Desktop.cs`). **Keine Normwerte**, alle Zahlen sind Annahmen V1e (Stellschrauben im XR-Labor).

### Anstoss statt ganzer Bewegung (Hebegeste, Hineinziehen, Wegwischen)
- Ein **kurzer, deutlicher Anstoss** in die Richtung der Geste löst aus: Haltung wie bisher, mindestens `gesten.anstoss_weg_m` = **6 cm** innert 0.2 s mit einem
  mittleren Tempo von mindestens `gesten.anstoss_tempo_ms` = **0.5 m/s** (6 cm in höchstens 0.12 s bzw. 10 cm in 0.2 s); bei der Hebegeste beide Hände etwa gleichzeitig.
  Die ganze Bewegung wie bis V1d löst weiter aus.
- **Ablauf:** ist der Anstoss erkannt, gilt die Geste als ausgelöst – die Symbole steigen auf, der Bildschirm gleitet herein, das Fenster gleitet hinaus, mit Schwung
  zu Ende, auch wenn die Hand stehen bleibt oder zurückgeht. Bis dahin folgt die Bewegung der Hand; abbrechen nur vor dem Auslösen. Ton beim Auslösen wie bisher.
- **Gegen Fehlauslösungen** (feste Annahmen V1e): die Haltung steht schon 0.1 s vor dem Schub, die Handfläche dreht sich dabei höchstens 25°, die Hand ist deutlich
  offen (Verhältnis der Fingerspitzen ab 1.75; eine flache Hand rund 1.9), quer höchstens 40 % des Wegs; die andere Hand darf sich beim Anstoss höchstens halb so weit
  (mindestens 2 cm) gegenläufig bzw. überhaupt bewegen. Sperren, Ausholen, Pinch, Drehen, Teleport und Blättern auf der Tafel wie bisher.
- **Gemessen** (Test `V1eTests`, Generator des Gestenlabors: 900 Zufallsbewegungen, 10 × 3 s ruhige Hände, 84 Testgesten, zusammen 37 min): Fehlauslösungen vorher
  (ganze Bewegung) Hebegeste 0, Wegwischen 0, Hineinziehen 1 – nachher (mit Anstoss) 0, 0, 1 (dieselbe schnelle Zufallsbewegung, die schon die ganze Bewegung
  auslöst). Kurze Anstösse (6.5–12 cm in 0.1–0.25 s, rechte und linke Hand, Hebegeste): erkannt 115 von 120, mit der ganzen Bewegung bis V1d 0 von 120.
- **Geisterhand und Hinweise** zeigen den kurzen Anstoss («Kurz nach links wischen: schliessen», «Beide Handflächen nach oben – kurz anheben»): erst die Hand stellen,
  dann 9 cm schnell, dann steht sie.

### Etagenliste auf einer Seite
- Die App «Etagen» zeigt alle Geschosse (am Tisch dazu «Alle Etagen») auf **einer Seite**: die Tafel wird um so viele Zeilen höher, die Zeilen bleiben gleich gross und
  gleich weit auseinander (52 pt, Mitten 60 pt – mit der ganzen Hand bzw. Fingerspitze treffbar). Test_praxis: 7 Geschosse beim Begehen (3 Zeilen höher), 8 Einträge am
  Tisch (4 Zeilen höher).
- Erst über `etagen.zeilen_max` = **12** rollt die Liste: 12 Zeilen sichtbar, ein **Wisch auf der Tafel nach oben bzw. unten** rollt um 11 weiter bzw. zurück – keine Seiten.
- Das **aktuelle Geschoss** ist hervorgehoben (Haken, gedrückt) und beim Öffnen sichtbar (in der Mitte, wenn die Liste rollt).

### Zwei Punkte am Sitzungstisch (Variante `labor.variante.griffe_tisch` = `zwei_punkte`, Vorgabe | `ein_griff` – der Griff von V1d)
- **Punkt «Verschieben»** (blau, `verschieben` in `kataloge/xr_farben.json`): Mitte der vorderen Gebäudekante. Gefasst (Pinch) folgt das Modell der Hand in allen
  Richtungen, auch in der Höhe – **ohne** Drehen und ohne Massstab.
- **Punkt «Drehen und Vergrössern»** (himbeerrot, `drehen`): rechte vordere Ecke, schräg nach aussen. Das Modell dreht **immer um sein Zentrum** (Mitte des sichtbaren
  Rechtecks, beim Fassen festgehalten), nie um den Griff: der **Winkel der Hand um das Zentrum** in der Tischebene ergibt die Drehung, das **Verhältnis der Abstände**
  Hand ↔ Zentrum (jetzt zu beim Fassen) den Massstab (1:5 … 1:200). Kleine Ausschläge zählen nicht: `sitzung.dreh_totzone_grad` = **3°**, `sitzung.zoom_totzone` =
  **5 %** (weich abgezogen, ohne Sprung). Die Höhe bleibt.
- Es wirkt **immer nur ein Punkt**; eine Hand fasst nie beide. Nach dem Loslassen sitzen beide wieder an ihrem Platz am Modell (vorn bzw. vorn rechts).
- **Darstellung:** die Kugel im Akzent der App-Farbe, mit hellem Saum im Pastellton (gut sichtbar im hellen Raum und mit Passthrough); beim Annähern grösser und heller,
  beim Greifen das **Symbol** der Werkzeuge von KICAD darüber («Verschieben» Pfeilkreuz, «Drehen» Kreispfeil; Strich- und Revit-Fassung, Texturen mit
  `werkzeuge/quest_symbole.mjs`). Farben (Vorschlag V1e nach der Regel von V1a, zu bestätigen): Verschieben Ton **#D4DBF7**, Akzent **#2C3D81**; Drehen Ton **#F6D5DD**,
  Akzent **#7E2F43**.
- **Geisterhand:** gezögert an einem Punkt «Greifen»; am gehaltenen Punkt «Drehen» das «Kreisen» (auf dem Bogen um das Zentrum, etwas nach aussen). Prüfaufgabe
  «Modell greifen, drehen und zoomen» mit dem roten Punkt.

### Symbolfeld neben der Tafel: mehr Abstand, kleinerer Bogen
- Weicht das Feld neben eine offene Tafel aus (Glas-App, PC-Bildschirm, Karte), bleibt `symbole.abstand_tafel_m` = **10 cm** Luft (im Blick und als kleinster Raumabstand;
  bis V1d 3 cm im Blick) zu Tafel, Werkzeugleiste, Griff und Schliessen. Die Symbole stehen dabei auf einem Bogen mit `symbole.radius_seitlich_m` = **0.50 m** um den Kopf
  (waagrecht; das frontale Feld 0.6 m) – näher, in Griffweite, zum Kopf gewandt. Ohne offene Tafel bleibt das frontale Feld wie bisher.

### Mehr Glanz
- Glas-Apps, App-Symbole (Scheiben) und Aufgabenkarte: ein **Lichtsaum am ganzen Rand** (oben kräftiger, bei den runden Scheiben stärker), ein **weicher heller Schein
  oben**, der sich leicht mit dem Blickwinkel verschiebt. Aufgehellt im «Screen»-Verfahren (helles Pastell überstrahlt nicht, die dunkle Schrift bleibt lesbar).
  Stellschraube `anzeige.glanz` = **0.6** (0 = wie bis V1d). Kosten: einige Rechenschritte je Pixel im Shader `glas.hlsl`, keine Textur, kein Zeichenaufruf mehr – am
  PC ohne messbaren Unterschied der Bildzeit (Begehen 7.5 bzw. 7.3 ms, V1d 7.5 bzw. 7.4 ms; am PC durch den Bildtakt begrenzt, auf der Quest nur an der Brille messbar).

### Stellschrauben und Varianten (neu)
| Schlüssel | Vorgabe | Bereich | Wirkung |
| --- | --- | --- | --- |
| `gesten.anstoss_weg_m` | 0.06 m (seit V1h 0.045 m) | 0.02 … 0.2 | Weg des Anstosses (Hebegeste, Hineinziehen, Wegwischen) |
| `gesten.anstoss_tempo_ms` | 0.5 m/s (seit V1h 0.35 m/s) | 0.2 … 3 | mittleres Tempo des Anstosses |
| `etagen.zeilen_max` | 12 | 4 … 30 | bis so viele Etagen auf einer Seite, darüber rollt die Liste |
| `sitzung.dreh_totzone_grad` | 3° | 0 … 20 | Punkt «Drehen»: so viel Drehung zählt nicht |
| `sitzung.zoom_totzone` | 0.05 | 0 … 0.3 | Punkt «Drehen»: so viel Abstandsänderung zählt nicht |
| `symbole.abstand_tafel_m` | 0.10 m | 0.02 … 0.4 | Luft zwischen Symbolen und Tafel |
| `symbole.radius_seitlich_m` | 0.50 m | 0.3 … 1 | Bogen der Symbole neben der Tafel |
| `anzeige.glanz` | 0.6 | 0 … 1 | Glanz der Apps |
| `labor.variante.griffe_tisch` | zwei_punkte | zwei_punkte, ein_griff | Griffe am Sitzungstisch (`zoom_ein_griff` gilt nur für «ein Griff») |

Grenzen: Katalog 73 von 80 Stellschrauben, mit 11 Varianten und der Aufgabe 85 von 96 Werten im Kanal.

### Prüfen
- **Tests:** Xr `V1eTests` (Fehlauslösungen vorher/nachher, Anstoss je Geste, kein Anstoss bei langsam/kurz/Schwenk/Pinch/Faust/halb offen/einhändig/ohne Stellen,
  Ausholen und Zurückholen, gegenläufige Hand, Abgrenzung Pinch + Wisch, Sprung und Tafel, Stellschrauben; Etagenliste; zwei Punkte: Lage, Verschieben ohne Drehen,
  Drehen um das Zentrum mit Totzone, Massstab aus dem Abstand in den Grenzen, nur ein Punkt, Variante und Farben; Symbolfeld neben der Tafel; Geisterhand, Symbole,
  Glanz), angepasst `V1aSymboleTests`, `DesktopGesteTests`, `BedienungTests`; Web `XrLaborTests`; Node `xr-labor.test.mjs`, `quest-symbole.test.mjs`.
- **Am PC:** `werkzeuge\quest_pruefen.ps1 -Projekt <Test_praxis> -Pruefbilder <ordner>`: Lauf 3 «V1d Tisch=ok (…; V1e zwei Punkte: verschieben ohne Drehen ja, drehen um
  das Zentrum 37.0° (Hand 40°, Totzone 3°), 1:50 → 1:40.4 …; Etagen am Tisch 8 von 8 Einträgen auf einer Seite …)», Lauf 4 «Desktop: ok; … Anstoss 8 cm herein ja, Anstoss
  hinaus ja». Prüfbilder `v1e_punkte_beide.jpg`, `v1e_verschieben_gefasst.jpg`, `v1e_drehen_gefasst.jpg`, `v1e_geist_kreisen.jpg`, `v1e_etagen_tisch.jpg`,
  `desktop_1_anstoss_halb.jpg`, `desktop_9_anstoss_ausgeloest.jpg`, `desktop_10_anstoss_weg.jpg`, dazu für Glanz und Abstand `glas_apps.jpg`, `app_nach_wahl.jpg`,
  `glas_apps_sitzung.jpg`, `farben_dunkel_*.jpg` (nur lokal).
- **Nur mit der Brille prüfbar:** ob der Anstoss mit echten Händen zuverlässig auslöst und nichts ungewollt (Tempo 0.5 m/s, Haltung 0.1 s vorher, Drehung 25°,
  Offenheit 1.75), ob die hohe Etagentafel angenehm ist, Handhabung und Sichtbarkeit der zwei Punkte (auch mit Passthrough), ob 10 cm Abstand und der Bogen von 0.5 m
  passen, wie der Glanz im echten Raum wirkt.
- **Anleitung:** `zb-quest-v1e-anstoss`, `zb-quest-v1e-punkte`, `zb-quest-v1e-tafel` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App V1f: Befunde des dritten Brillentests (Christian 24.10.2026)

Christian: «der tisch stört. immer pass im sitzungszimmer und den tisch nie zeigen(nur das modell) pich ist zu sensibel.. hier braucht es eine andere geste. bsp.
pistolengeste. zudem soll die tafel nicht mit dem blick wandern. dies soll mittig sein.» – «wisch geste soll auch gehen wenn die finger nicht voll gestrekt sind.» –
«die app hineinheben geste scheint im letzten test weniger gut zu klappen.» Regeln in `src/Hlks.Xr` (`Bedienung/Pistole.cs`, `HandBegehen.cs`, `Bedienung/Anstoss.cs`,
`Bedienung/HebeGeste.cs`, `Bedienung/Wegwischen.cs`, `Desktop/DesktopGeste.cs`, `Bedienung/Tafel.cs`, `Sitzung/ZweiPunkte.cs`, `Sitzung/SitzungRaum.cs`,
`Bedienung/Geisterhand.cs`), die App in `src/Hlks.Quest` (`QuestApp.Pistole.cs`, `Sitzungsraum.cs`, `QuestApp.Apps.cs`, `QuestApp.Messen.cs`, `QuestApp.Marker.cs`,
`GlasApps.cs`, `SymbolApps.cs`, `QuestApp.PruefenV1f.cs`). **Keine Normwerte**, alle Zahlen sind Annahmen V1f (Stellschrauben im XR-Labor).

### Pistolengeste statt Pinch (Variante `labor.variante.ausloeser` = `pistole`, Vorgabe | `pinch` – wie bis V1e)
- **Haltung:** Zeigefinger gestreckt, Mittel-, Ring- und kleiner Finger eingerollt (die Daumenspitze nicht an der Zeigefingerspitze – das wäre ein Pinch). **Zielen:**
  der Daumen steht oben; der Strahl geht vom Grundgelenk des Zeigefingers durch die Spitze. **Abdrücken:** der Daumen klappt an die Seite der Hand – die Daumenspitze
  näher als `pistole.abzug_an_m` = **2.5 cm** an Zeigefinger (Mittelhand, Grundglied) bzw. Mittelfinger (Grund-, Mittelglied); gelöst erst über `pistole.abzug_aus_m` =
  **4 cm** (Hysterese).
- **Gegen Zittern und blosses Zeigen:** ein Abzug zählt nur, wenn der Daumen vorher `pistole.ruhe_s` = **0.15 s** oben stand und der Strahl dabei ruhig war (höchstens
  20°); danach ruht die Geste 0.3 s. Es gilt der Strahl bzw. Fang von **0.08 s vor dem Abzug** (das Zucken des Daumens verreisst das Ziel nicht). Zeigen mit locker
  anliegendem Daumen löst nie aus (der Daumen stand nie oben).
- **Wirkung:** **Springen** (zielen zeigt Strahl und Ring am Boden – grün, sobald das Ziel ruhig liegt; abdrücken springt, ohne gültiges Ziel Abbruch); **Messen**
  (Messband: abdrücken und halten, zum zweiten Punkt zielen, Daumen hoch; «zwei Punkte»: je ein Schuss; der Strahl zum Fang wird als feine Linie gezeigt); **Marker
  setzen** (zielen, abdrücken); **Wahl auf Distanz** (ein Knopf der Tafel bzw. ein App-Symbol unter dem Strahl). Antippen mit dem Finger bleibt. Der Pinch zielt und
  springt nicht mehr; **Pinch + Wisch** (Drehen, Blättern, Etage) und der Griff unter dem Fenster bleiben beim Pinch.
- **Hilfe:** die Geisterhand «Pistole» (Daumen oben, klappt an, geht wieder hoch) mit Hinweis beim ersten Messen bzw. Marker setzen und wenn jemand wie bisher mit
  gehaltenem Pinch (0.6 s) zu zielen versucht (höchstens alle 20 s). Hinweise und Prüfaufgaben (`teleport-markierung`, `messen-wand-wand`, `modell-greifen-zoomen`)
  nennen die Pistolengeste; die Aufgaben tragen die Variante «ausloeser» im Messprotokoll.

### Sitzungsraum: immer Passthrough, nur das Modell
- Im Sitzungsraum ist **Passthrough immer ein**; der virtuelle Raum, Tisch, Boden und die Tischkante werden **nie** gezeichnet – nur das Modell mit seiner Bodenplatte
  (Schatten). Ohne Passthrough (Simulator am PC) erscheint ein **heller Ersatzhintergrund**. Die Variante «umgebung» gilt am Tisch nicht mehr.
- **«Auf echten Tisch» ist der Normalfall** (App «Tisch», bis V1e «Echter Tisch»): beim Betreten wartet die App aufs Antippen – mit dem Zeigefinger kurz auf die
  Platte tippen, das Modell stellt sich dorthin («Neu antippen» wie bisher). Das Modell ist schon vorher sichtbar. Ohne echten Tisch (Schalter aus) steht es frei auf
  `sitzung.tisch_hoehe_m` = **0.75 m** über dem Boden (wie bisher der virtuelle Tisch), 0.9 m vor dem Kopf. Die Tischregel der App-Symbole und der Tafel bezieht sich auf
  die echte bzw. angenommene Platte.
- **Faustgriff** (Vorschlag V1f, mit der Pistolengeste): die zwei Punkte fasst die **ganze Hand** – Hand an den Punkt, Faust schliessen (innen in der Faust höchstens 8 cm
  vom Punkt), führen, Hand öffnen. Die Pistole (Zeigefinger gestreckt) greift nicht. Geisterhand «Faust» bzw. «Faust kreisen». Mit der Variante «pinch» wie bis V1e.

### Tafel fest (Variante `labor.variante.tafel_nachfuehren` = `fest`, Vorgabe | `traege`)
- Die Tafel (Glas-App, PC-Bildschirm) öffnet **mittig in der Blickrichtung und bleibt stehen**, auch wenn man den Kopf dreht; «träge» (V1d) bleibt als Variante.

### Wischgesten mit lockerer Hand, Hebegeste natürlicher
- **Lockere Hand:** Wegwischen, Hineinziehen und Hebegeste lösen auch mit halb gekrümmten Fingern aus – die Hand gilt ab `gesten.offen_ab` = **1.40** als offen
  (mittlerer Abstand der Fingerspitzen zum Handgelenk in Handgrössen; flache Hand rund 1.9, halb gekrümmt rund 1.45, Faust rund 1.0; bis V1e 1.65 bzw. 1.75 beim
  Anstoss). **Nie:** Faust, Pistolengeste, Pinch. Gegen Fehlauslösungen (feste Annahmen V1f): die **Form der Hand bleibt** während der Geste (höchstens 0.15 Änderung),
  die vier Finger sind bei einer lockeren Hand etwa **gleich gekrümmt** (Streckung höchstens 0.25 auseinander). Blättern auf der Tafel verlangte nie eine offene Hand.
- **Hebegeste** (Befund: mit echten Händen schlechter seit V1e) mit eigenen Schwellen: Handfläche bis `hebe.handflaeche_grad` = **60°** neben oben (bis V1e 45°), Drehung
  beim Anstoss bis `hebe.dreh_grad` = **45°** (sonst 25°), Haltung vorher `hebe.vorhalte_s` = **0 s** (die Hände drehen sich beim Anheben erst in die Haltung; sonst
  0.1 s), Gleichlauf `hebe.gleichzeitig_s` = **0.5 s** (bis V1e 0.35 s; der Anstoss der ersten Hand bleibt so lange gemerkt), dazu die lockere Hand.
- **Protokoll:** steigen beide Hände deutlich (5 cm in 0.35 s) und die Hebegeste löst nicht aus, steht einmal je Ansatz eine Zeile «Hebe verworfen: <Grund>» im
  Protokoll der Brille (höchstens jede Sekunde), z. B. «Handfläche links 72° (höchstens 60)», «Hand rechts: zu geschlossen (1.32, ab 1.40)», «links: Tempo 0.38 m/s
  (mind. 0.50)», «Hände 0.62 s auseinander (höchstens 0.50)»; im Status «Hebe verworfen n×».
- **Gemessen** (Test `V1fTests`, Generator des Gestenlabors: 900 Zufallsbewegungen, 10 × 3 s ruhige Hände, 84 Testgesten): Fehlauslösungen vorher (Satz V1e)
  Hebegeste 0, Wegwischen 0, Hineinziehen 1 – nachher (V1f) **0, 0, 1**. Gegenprobe mit 1800 weiteren Zufallsbewegungen (andere Saat, nicht im Test): V1e 0/4/1, V1f
  0/5/2 – zwei mehr, beide mit fast offener, gleichmässiger Hand am Rand der Schwelle. Lockere Hand (Krümmung 0.4/0.45/0.5, Verhältnis 1.61/1.54/1.46): V1f je 50 von 50
  kurzen Anstössen erkannt, V1e 0 von 50. Natürliches Anheben (Handflächen drehen mit, locker, bis 0.35 s versetzt, ohne Halten davor): V1f 54 von 54, V1e 3 von 54.
  Pistolengeste: 0 Abzüge in 1018 Bewegungen ohne Pistole.

### Stellschrauben und Varianten (neu bzw. geändert)
| Schlüssel | Vorgabe | Bereich | Wirkung |
| --- | --- | --- | --- |
| `labor.variante.ausloeser` | pistole | pistole, pinch | Auslöser der Zeige-Aktionen; Faustgriff an den Punkten |
| `pistole.abzug_an_m` | 0.025 m | 0.01 … 0.06 | Daumen an der Hand = abgedrückt |
| `pistole.abzug_aus_m` | 0.04 m | 0.015 … 0.1 | Daumen oben bzw. gelöst |
| `pistole.ruhe_s` | 0.15 s | 0 … 1 | so lange Daumen oben und ruhig gezielt vor dem Abzug |
| `sitzung.tisch_hoehe_m` | 0.75 m | 0.4 … 1.3 | Höhe des Modells ohne echten Tisch |
| `gesten.offen_ab` | 1.40 | 1.1 … 1.9 | ab so viel gilt die Hand als offen (Wischgesten) |
| `hebe.vorhalte_s` | 0 s | 0 … 0.5 | Haltung vor dem Anstoss der Hebegeste |
| `hebe.dreh_grad` | 45° | 10 … 90 | Drehung der Handfläche beim Anstoss der Hebegeste |
| `hebe.handflaeche_grad` | 60° (bis V1e 45°) | 10 … 80 | Handfläche neben oben |
| `hebe.gleichzeitig_s` | 0.5 s (bis V1e 0.35 s) | 0.05 … 1.5 | Gleichlauf der Hände |
| `labor.variante.tafel_nachfuehren` | fest (bis V1e träge) | fest, traege | Tafel bleibt stehen bzw. rückt nach |

Grenzen: Katalog 80 von 80 Stellschrauben, mit 12 Varianten und der Aufgabe 93 von 96 Werten im Kanal.

### Prüfen
- **Tests:** Xr `V1fTests` (Pistole: Haltung, Abzug und Strahl aus den Gelenken, zielen und abdrücken in jeder Körperrichtung, zweimal, Strahl von kurz davor; kein
  Abzug beim Zeigen, Bewegen, mit anliegendem Daumen, zu früh, beim Schwenk; Hysterese; Fehlauslösungen über 1018 Bewegungen; Springen, Pinch zielt nicht, Pinch + Wisch
  dreht; Wahl auf Distanz; Variante und Stellschrauben; Faustgriff und Geisterhand; Tischhöhe und Hintergrund; Tafel fest; lockere Hand mit Fehlauslösungen vorher/
  nachher; Faust, Pistole, Pinch wischen nicht; Blättern locker; natürliches Anheben; verworfene Ansätze mit Grund und gedrosselt), angepasst `V1eTests`, `V1dTests`,
  `P5bBedienungTests`, `LaborKatalogTests`; Web `XrLaborTests`; Node `xr-labor.test.mjs`.
- **Am PC:** `werkzeuge\quest_pruefen.ps1 -Projekt <Test_praxis> -Pruefbilder <ordner>`: Lauf 1 «V1f=ok (Auslöser pistole; Pistolengeste beim Begehen: zielen ja (Ziel
  1.4 m vor, bereit ja), blosses Zielen springt nicht ja, abgedrückt 1×, gesprungen ja; Tafel fest; …)» und «V1d=ok (… nach 50° Drehung 50.0° (nachgerückt 0×) …)»,
  Lauf 3 «V1f Tisch=ok (kein virtueller Tisch, Platte angenommen auf 0.750 m, Hintergrund heller Ersatz ja, Faustgriff ja)». Prüfbilder `v1f_pistole_zielen.jpg`,
  `v1f_pistole_abdruecken.jpg`, `v1f_sitzung_ohne_tisch.jpg`, `v1f_geist_faust_kreisen.jpg`, `v1f_tafel_bleibt_nach_drehen.jpg` (nur lokal).
- **Nur mit der Brille prüfbar:** ob die Pistolengeste mit echten Händen sicher erkannt wird (Daumen oben bzw. angelegt, 2.5/4 cm, 0.15 s) und nichts ungewollt
  auslöst, der Faustgriff an den Punkten, Passthrough im Sitzungszimmer mit dem Antippen des echten Tischs, die lockere Hand beim Wischen und das natürliche Anheben
  (die Zeilen «Hebe verworfen: …» im Protokoll zeigen die Gründe).
- **Anleitung:** `zb-quest-v1f-pistole`, `zb-quest-v1f-sitzung`, `zb-quest-v1f-gesten` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App V1g: Kippen am roten Punkt, Symbole blenden aus, keine Fläche unter dem Modell (Christian 24.10.2026)

Christian: «durch oben unten des roten punkte soll das modell auch rotieren.» (auf Rückfrage «Kippen») – «bei schliessen der tafel soll die hand keine app wählen. (app
ausblenden nach schliessen der Tafel) auch ausblenden nach wahl eines apps.» – «die weisse fläche unter dem modell stört immer noch.» Regeln in `src/Hlks.Xr`
(`Sitzung/Kippen.cs`, `Sitzung/ZweiPunkte.cs`, `Sitzung/SitzungRaum.cs`, `Bedienung/HandBeruehrung.cs`), die App in `src/Hlks.Quest` (`Sitzungsraum.cs`,
`QuestApp.Apps.cs`, `SymbolApps.cs`, `QuestApp.PruefenV1f.cs`). **Keine Normwerte**, alle Zahlen sind Annahmen V1g.

### Kippen am roten Punkt «Drehen und Vergrössern»
- **Auf/Ab** kippt das Modell um eine **waagrechte Achse durch das Zentrum** (Mitte des sichtbaren Rechtecks auf der Unterkante), quer zur Richtung vom Kopf zum Zentrum
  beim Greifen: die Hand hoch hebt die Seite zum Betrachter, die Hand tief senkt sie. Die Neigung folgt dem **Winkel der Hand über der Unterkante** (im waagrechten Abstand
  zum Zentrum beim Greifen gemessen), mit der Totzone von «sitzung.dreh_totzone_grad» = **3°**, höchstens `sitzung.kipp_max_grad` = **±60°** (0 = nicht kippen).
- **Unverändert:** links/rechts um das Modell dreht um die senkrechte Achse, nach aussen/innen zoomt (zoomen kippt nicht). Gekippt bleibt das Modell beim Drehen,
  Vergrössern und Verschieben gekippt (die Neigung bleibt zum Betrachter; Achse und Drehpunkt bleiben, bis sie wieder 0 ist).
- **Nie in den Tisch:** das gekippte Modell hebt sich so weit, dass die tiefste Ecke der Grundfläche auf der Unterkante ohne Kippung bleibt (bei 30° und dem Modell von
  Test_praxis rund 12 cm).
- **Zurücksetzen:** **Doppeltipp** am roten Punkt (zweimal kurz fassen: höchstens 0.4 s gehalten, innert 0.5 s wieder, höchstens 6 cm daneben) stellt die Neigung auf 0
  (Klick); «Tisch» › «Zurücksetzen» setzt Lage, Massstab, Drehung und Neigung zurück. Im Status «Neigung n° (zurückgesetzt n×)».

### Symbole blenden nach der Wahl aus (Variante `labor.variante.symbolwahl` = `ausblenden`, Vorgabe seit V1g | `stehen`)
- Nach der Wahl einer App blendet das Symbolfeld aus, die App öffnet. **Schliesst man die Tafel** (Wegwischen, «×», PC-Bildschirm hinaus), kommen die Symbole **nicht
  zurück** – eine andere App holt man mit der Hebegeste (einmal ein Hinweis «Andere App: beide Handflächen nach oben – kurz anheben»). So trifft die Hand beim Schliessen
  nie ein Symbol.
- Nach jedem Schliessen einer Tafel zählt `symbole.sperre_schliessen_s` = **0.5 s** lang keine Berührung eines Symbols (auch auf Distanz mit der Pistolengeste).
- «stehen» bleibt zum Vergleich (die Symbole weichen neben bzw. hinter die App, `labor.variante.feld_bei_app` wirkt nur dann).

### Keine Fläche unter dem Modell
- Die Bodenplatte (bis V1f eine helle Glasfläche) entfällt am Sitzungstisch, auch mit Passthrough und bei «Raum zeigen»: es wird nur das Modell gezeichnet.
- Höchstens mit `sitzung.umriss` (Vorgabe **aus**) deutet beim Annähern an einen Punkt bzw. beim Greifen ein dünner, blasser Umriss die Grundfläche an (er kippt mit).

### Stellschrauben und Varianten (neu bzw. geändert)
| Schlüssel | Vorgabe | Bereich | Wirkung |
| --- | --- | --- | --- |
| `sitzung.kipp_max_grad` | 60° | 0 … 85 | höchstens so weit kippt der rote Punkt das Modell |
| `symbole.sperre_schliessen_s` | 0.5 s | 0 … 3 | nach dem Schliessen einer Tafel keine Berührung eines Symbols |
| `sitzung.umriss` | aus | ein, aus | dünner Umriss der Grundfläche beim Greifen |
| `labor.variante.symbolwahl` | ausblenden (bis V1f stehen) | ausblenden, stehen | Symbole nach der Wahl einer App |

Grenzen: Katalog 83 von 96 Stellschrauben (bis V1f 80), mit 12 Varianten und der Aufgabe 96 von 128 Werten im Kanal (bis V1f 96; `XrLaborKatalog.StellschraubenMax`,
`XrNachrichten.StellschraubenMax`, `PruefAufgaben.StellschraubenMax`, Werte je Profil).

### Prüfen
- **Tests:** Xr `V1gTests` (Kippen um die waagrechte Achse durch das Zentrum quer zum Blick, die Seite zum Betrachter hebt sich, Matrix gleich den Punkten, nach unten,
  seitlich sitzend; Grenze, Totzone, Stellschraube; Drehen und Vergrössern bleiben, die Neigung bleibt beim Drehen, Zoomen und Verschieben; nie unter die Unterkante bei
  verschiedenen Drehungen und Winkeln; Doppeltipp, zu langsam, zu lang, ohne Zeit; Symbole ausblenden, Sperre nach dem Schliessen, Umriss), angepasst `V1aSymboleTests`,
  `LaborKatalogTests`; Web `XrLaborTests` (Grenze 128); Node `xr-labor.test.mjs`.
- **Am PC:** `werkzeuge\quest_pruefen.ps1 -Projekt <Test_praxis> -Pruefbilder <ordner>`: Lauf 1 «V1f=ok (…; V1g: nach der Wahl ausblenden: App offen und Symbole aus ja,
  nach dem Schliessen gesperrt ja, Symbole kommen nicht zurück ja)», Lauf 3 «V1g Tisch=ok (gekippt 30.0° (Hand 33°, Totzone 3°), Drehung und Massstab gleich, Zentrum
  versetzt 0.0 mm, angehoben 11.7 cm, tiefste Ecke +0.0 mm über der Unterkante ja; Doppeltipp Neigung 0 ja; Grenze 60° ja; …)». Die Folgen von V1a und V1d prüfen die
  Variante «stehen». Prüfbilder `v1g_gekippt.jpg`, `v1g_gekippt_seite.jpg`, `v1g_tisch_ohne_flaeche.jpg`, `v1g_app_nach_wahl.jpg` (nur lokal).
- **Nur mit der Brille prüfbar:** wie sich das Kippen mit der Faust bzw. dem Pinch anfühlt (Winkel, Grenze, Anheben über dem echten Tisch), ob der Doppeltipp
  zuverlässig ist, ob das Ausblenden der Symbole und die Sperre nach dem Schliessen passen.
- **Anleitung:** `zb-quest-v1g-kippen` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App V1h: Wischgesten leichter, Striche nach aussen und unter dem Blick (Christian 25.10.2026)

Christian (Brillentest V1g, die Werte live gelockert): «Ja, das Wegwischen geht jetzt gut, übernimm die Werte als Vorgabe.» – «rechts nach links gesten mit der
linken hand am linken bildrand nicht beachten. ebenso rechte hand links nach rechts gesten am rechten rand nicht beachten.» – «oder im normbereich der jeweiligen hand.»
Regeln in `src/Hlks.Xr` (`Bedienung/Anstoss.cs`, `Bedienung/Wegwischen.cs`, `Desktop/DesktopGeste.cs`, `Bedienung/XrBedienEinstellungen.cs`), die App in
`src/Hlks.Quest` (`QuestApp.Apps.cs`, `QuestApp.Desktop.cs`, `DesktopBildschirm.cs`). **Keine Normwerte**, alle Zahlen sind Annahmen bzw. Werte des Brillentests.

### Neue Vorgaben (für Hebegeste, Hineinziehen und Wegwischen)
- Anstoss `gesten.anstoss_weg_m` = **4.5 cm** (bis V1g 6 cm) mit `gesten.anstoss_tempo_ms` = **0.35 m/s** (bis V1g 0.5 m/s), innert 0.2 s; Handfläche
  `wegwischen.handflaeche_grad` = **60°** (bis V1g 50°; gilt auch für das Hineinziehen), Richtung `wegwischen.richtung_grad` = **45°** (bis V1g 35°).
- Folge: auch ein gemächlicheres Ausholen in der Haltung gilt als Anstoss – erst unter rund 12 cm in 0.8 s (bis V1g 0.45 s) bleibt es unter dem Tempo.

### Striche nach aussen: nicht vom Rand, nicht von der eigenen Seite
- **Linke Hand nach links** (Wegwischen) bzw. **rechte Hand nach rechts** (Hineinziehen) zählt nicht, wenn der Strich beginnt
  - am **Rand des Blicks**: mehr als `gesten.randwinkel_grad` = **25°** neben der Blickrichtung auf der Seite der Hand (das Zurücknehmen bzw. Ausschwingen des Arms), oder
  - auf der **eigenen Körperseite**: mehr als `gesten.mitte_toleranz_m` = **5 cm** neben der Körpermitte (linke Hand links, rechte Hand rechts). Die Körpermitte ist die
    senkrechte Ebene durch den Kopf in der Körperrichtung; diese folgt dem Blick träge (Zeitkonstante 1 s), ein kurzer Blick zur Seite verschiebt sie nicht.
- **Unverändert:** Striche aus der Mitte bzw. von der Gegenseite (auch die linke Hand nach links, wenn sie rechts der Mitte beginnt) und alle Striche nach innen
  (rechte Hand nach links, linke Hand nach rechts) – auch vom Rand. Es gilt für den Anstoss und für die ganze Bewegung.
- **Folge für absichtliche Wische:** mit der linken Hand nach links lässt sich das Symbolfeld nicht aus seiner linken Spalte bzw. die Tafel nicht von ihrer linken
  Hälfte wegwischen (Startpunkte frei: 4 von 7 Symbolen am Tisch bzw. mit 10° Neigung des Kopfs, 4 von 6 Punkten der Tafel); mit der rechten Hand bzw. aus der Mitte schon.

### Kein Wisch unter dem Blick (Gegenregel)
- Mit den lockereren Werten lösten zufällige Bewegungen aus der **Ruhelage am Körper** (Hand tief, Handfläche zum Körper) Wegwischen und Hineinziehen häufiger aus.
  Darum zählt ein Wisch quer zum Blick nicht, wenn er mehr als `gesten.unten_grad` = **45°** unter der Blickrichtung beginnt (die Neigung des Kopfs zählt mit). Die
  Hebegeste beginnt unten und ist ausgenommen.
- **Absichtliche Wische** vor Symbolfeld und Tafel bleiben frei: Tafel und Symbolfeld am Tisch liegen höchstens 34° unter dem waagrechten Blick, das Symbolfeld beim
  Begehen bis 47° (bei waagrechtem Kopf liegen 2 von 7 Symbolen der untersten Reihe darunter) – mit 10° Neigung des Kopfs alle innerhalb.

### Gemessen (Test `V1hTests`, Generator des Gestenlabors)
- **Fehlauslösungen** über je 900 Zufallsbewegungen mit drei Saaten, 10 × 3 s ruhige Hände und 84 Testgesten (2982 Bewegungen; Hebegeste / Wegwischen / Hineinziehen):
  V1g **0 / 5 / 3** – mit den neuen Werten ohne Regeln 0 / 34 / 22 – mit Rand und Körpermitte 0 / 29 / 2 – mit «unter dem Blick» (Vorgabe V1h) **0 / 4 / 0**.
- **Leichtes Auslösen:** leichte Anstösse (4.5–6.5 cm, 0.38–0.5 m/s, alle drei Gesten) V1g 13 von 60, V1h **60 von 60**; die deutlichen von V1e (6.5–12 cm) V1g 117,
  V1h 119 von 120 (der eine: linke Hand nach links, 5 cm links der Mitte begonnen – an der Grenze der Körpermitte).

### Stellschrauben (neu bzw. geändert)
| Schlüssel | Vorgabe | Bereich | Wirkung |
| --- | --- | --- | --- |
| `gesten.anstoss_weg_m` | 0.045 m (bis V1g 0.06 m) | 0.02 … 0.2 | Weg des Anstosses |
| `gesten.anstoss_tempo_ms` | 0.35 m/s (bis V1g 0.5 m/s) | 0.2 … 3 | mittleres Tempo des Anstosses |
| `wegwischen.handflaeche_grad` | 60° (bis V1g 50°) | 10 … 80 | Handfläche seitlich (Wegwischen, Hineinziehen) |
| `wegwischen.richtung_grad` | 45° (bis V1g 35°) | 5 … 80 | Richtung des Wischs |
| `gesten.randwinkel_grad` | 25° | 5 … 90 | Strich nach aussen: nicht vom Rand des Blicks (90 = ohne) |
| `gesten.mitte_toleranz_m` | 0.05 m | 0 … 0.5 | Strich nach aussen: nur ab der Körpermitte |
| `gesten.unten_grad` | 45° | 10 … 90 | kein Wisch, der so weit unter dem Blick beginnt (90 = ohne) |

Grenzen: Katalog 86 von 96 Stellschrauben, mit 12 Varianten und der Aufgabe 99 von 128 Werten im Kanal.

### Prüfen
- **Tests:** Xr `V1hTests` (Vorgaben in Code und Katalog, für alle drei Gesten; Rand und Körpermitte beider Hände: Rand, eigene Seite, Mitte, Gegenseite, nach innen,
  Körperrichtung; die ganze Bewegung; unter dem Blick mit und ohne Neigung, Hebegeste ausgenommen; Startpunkte vor Symbolfeld und Tafel; leichte und deutliche
  Anstösse; Fehlauslösungen vorher/nachher), angepasst `V1eTests`, `V1fTests`, `V1aSymboleTests`, `DesktopGesteTests` (die Schwellen: «zu kurz», «langsam»,
  «gemächliches Ausholen» auf die neuen Werte); Web `XrLaborTests`; Node `xr-labor.test.mjs`, `quest-symbole.test.mjs`.
- **Am PC:** `werkzeuge\quest_pruefen.ps1` wie bisher – der Prüflauf des Desktops zieht mit der gedachten Hand (Kopf, Körperrichtung und Neigung wie in der App)
  herein und wischt hinaus; neue Prüfbilder braucht es nicht (nichts Sichtbares geändert).
- **Nur mit der Brille prüfbar:** ob die neuen Vorgaben mit echten Händen leicht genug und ohne Fehlauslösungen sind, ob 25° und 5 cm für die Striche nach aussen
  passen, ob «unter dem Blick» (45°) keinen gewollten Wisch verschluckt (z. B. tief vor der untersten Symbolreihe bei waagrechtem Kopf).
- **Anleitung:** `zb-quest-v1h-wischen` in `berichte/anleitung/inventar.json`.

## Modul XR, Quest App V1i: Hebegeste schliesst die offene Tafel (Christian 25.10.2026)

Christian: «Wenn ich die Symbole bei offener Tafel anhebe, soll die Tafel schliessen und die Symbole frontal stehen.» Regel in `src/Hlks.Xr/Bedienung/AppSymbole.cs`
(`BeimHeben`, `WeichtAus`), die App in `src/Hlks.Quest` (`QuestApp.Apps.cs`, Prüflauf `QuestApp.Desktop.cs`).

- **Mit `labor.variante.symbolwahl` = `ausblenden` (Vorgabe):** ist beim Anheben eine Tafel offen, **gleitet sie hinaus wie beim Wegwischen** – die Glas-App nach
  links, der PC-Bildschirm zur Seite (danach ruht das Hineinziehen kurz wie nach dem Wegwischen). Die Symbole steigen **frontal** auf wie ohne Tafel. Die Karte einer
  Prüfaufgabe bleibt stehen. Nach dem Schliessen gilt wie seit V1g `symbole.sperre_schliessen_s` (0.5 s keine Berührung).
- **Mit `stehen` (Vergleich):** wie bis V1h – die Glas-App blendet aus, vor dem PC-Bildschirm weicht das Feld seitlich aus (`labor.variante.feld_bei_app`).

### Prüfen
- **Tests:** Xr `V1iTests` (beide Varianten: Glas-App, PC-Bildschirm, beide, keine Tafel; Ausweichen nur mit «stehen»; über die Stellschraube der Variante).
- **Am PC:** `werkzeuge\quest_pruefen.ps1`, Lauf 4: mit «ausblenden» zum Schluss die Hebegeste bei offener Glas-App und – nach dem Symbol «Desktop» – bei offenem
  Bildschirm: «V1i Hebegeste schliesst die Glas-App ja und den Bildschirm ja, Symbole frontal», eingeblendet 4× (2 Gesten, 2 Symbol); «Feld seitlich daneben» prüft
  er nur noch mit «stehen». Prüfbild `desktop_11_heben_schliesst.jpg` (nur lokal).
- **Nur mit der Brille prüfbar:** ob das Schliessen beim Anheben im Gebrauch stimmt (z. B. eine Tafel, die man behalten wollte).
- **Anleitung:** `zb-quest-v1i-heben` in `berichte/anleitung/inventar.json`.

## Register XR: alles zu Brille, Quest App und VR/AR in einem Register (Christian 26.10.2026)

Christian, 26.10.2026: «wir nehmen alles was VR/XR ist in einen eigenen Menue Tab im CAD». Das Register **XR** steht im Menüband zwischen **Ansicht** und **Verwalten** (`menueband.js`: `REGISTER`, `GRUPPENFOLGE`; Name im Kürzeldialog: `kuerzel-liste.js`).
Es ist nur eine neue Ordnung: keine neue Logik, kein neuer Befehl des Kerns, keine neue Abfrage. Jede Funktion steht **nur noch hier** im Menüband; die Kontextmenüs behalten ihre Einträge und lösen dieselben Aktionen aus.
Die Aktions-Ids bleiben (`ansicht.questVerbinden`, `koordination.problemMarker` usw.): sie sind Bezeichner für Anleitung und Tests, keine Orte.

| Gruppe | Eintrag (Aktion) | Symbol | Kürzel | Vorher | Wirkung |
| --- | --- | --- | --- | --- | --- |
| Brille | **Quest verbinden …** (`ansicht.questVerbinden`) | `questVerbinden` | XQ | Ansicht › Fenster | Dialog «Quest verbinden» (Ampel, adb, WLAN, Brille merken) |
| Brille | **H+K XR installieren …** (`xr.appInstallieren`, neu; bis E1 «App installieren …») | `appInstallieren` | XI | Teil des Dialogs | öffnet denselben Dialog; sein erster Teil «Quest App» hat «App installieren/aktualisieren», «Alte Apps entfernen …», Kopplung, Netzzugang (Abschnitt «H+K XR (Empfänger-App), E1») |
| Brille | **Autostart** (`xr.autostart`, neu) | `autostart` | XU | Kästchen im Dialog | Schalter (Knopf gedrückt = ein), Einstellung `quest.autostart`, Vorgabe ein: die Begehung startet, sobald die Brille aufgesetzt ist; derselbe Wert wie das Kästchen im Dialog |
| Brille | **Brille zeigen** (`ansicht.brilleZeigen`) | `brilleZeigen` | XB | Ansicht › Fenster | Schalter: verbundene Brillen (Teilnehmer) als Figuren in der 3D-Ansicht, Einstellung `ansicht3d.brillen_zeigen` |
| Zeigen | **In VR begehen** (`ansicht.vrBegehen`) | `raumBegehen` | XV | nur Kontextmenü «Raum begehen in VR» | Auftrag an die Brille: den **gewählten Raum** begehen |
| Zeigen | **Im Sitzungsraum zeigen** (`ansicht.vrSitzungsraum`) | `sitzungstisch` | XS | nur Kontextmenü «Modell am Sitzungstisch» | Auftrag: das Geschoss des gewählten Raums auf dem Tisch |
| Zeigen | **Raum in AR** (`ansicht.vrRaumZeigen`) | `raumAr` | XR | nur Kontextmenü «Raum in AR» | Auftrag: nur der gewählte Raum plus Rand auf dem Tisch |
| Zeigen | **An die Brille senden** (`xr.anBrille`, neu) | `anBrille` | XA | Knopf in der 3D-Ansicht | VR-Auftrag wie der Knopf in der 3D-Ansicht: läuft das Begehen, dessen Raum; sonst die aktive 3D-Ansicht als Sitzungsraum; sonst das ganze Modell |
| Koordination | **Problem-Marker** (`koordination.problemMarker`) | `problemMarker` | XM | Koordination › Kollisionen | öffnet die Palette «Problem-Marker» rechts (Abschnitt «Problem-Marker») |

- **Raumaktionen im Menüband:** «In VR begehen», «Im Sitzungsraum zeigen» und «Raum in AR» nehmen den **gewählten Raum** (Grundriss oder Projektbrowser); ohne gewählten Raum sind sie gesperrt («Zuerst einen Raum wählen.»). Im Kontextmenü gibt der Rechtsklick den Raum mit (`{ raeume: [Kennzeichen] }`); die Kontextmenüeinträge sind unverändert.
- **Kürzel:** neu, X plus Anfangsbuchstabe (zwei Buchstaben nacheinander tippen). Die Kürzel waren vorher alle leer; kein bestehendes Kürzel ändert sich. Ein Test (`xr-register.test.mjs`) prüft, dass kein Kürzel doppelt vorkommt.
- **Symbole:** neu gezeichnet (Strich in `symbole.js`, Revit-Fassung in `symbole-revit.js`, je 32 und 16): `appInstallieren`, `autostart`, `anBrille`, `raumAr`; die Revit-Fassungen von `questVerbinden`, `brilleZeigen` und `xrLabor` gab es noch nicht (vorher galt auch im Revit-Satz der Strich).
- **Palettenliste:** «Ansicht › Fenster › Paletten» zeigt weiterhin alle Paletten mit Häkchen, auch «Problem-Marker» (das ist die allgemeine Palettenverwaltung, kein zweiter Weg zur Funktion im Menüband).
- **Prüfen:** Node `node --test src/Hlks.Web/wwwroot/js/oberflaeche/xr-register.test.mjs` (Register vorhanden und an der richtigen Stelle, Einträge nur einmal, alte Orte leer, Kürzel eindeutig, Symbole mit Strich und Revit-Fassung, Kontextmenüs unverändert, Doku und Inventar).
- **Anleitung:** Wege in `berichte/anleitung/inventar.json` auf «XR › …» umgestellt.

## Modul G: Gesten am Arbeitsplatz (Stufe 1, Teil Oberfläche; `entwurf/modul-gesten.md`)

Christian, 26.10.2026: «eine erweiterung der normalen bedienung, nicht diese zu ersetzen … wie eine 4. ebene nach Tastatur, Maus, Touch.» Die Kamera erkennt die Hand im Browser (MediaPipe; `js/gesten/gesten.js`, `gesten-logik.js`, `gesten-worker.js`, gebaut von hlks-kern); die **Oberfläche** (dieser Abschnitt) schaltet sie, zeigt den Zustand, löst die Schritt-Gesten über das Aktionsregister aus und speichert die Einstellung. Keine Fachlogik in der Oberfläche: Gesten setzen nie Punkte und ändern das Modell nie, nur Ansicht und Navigation. **Das Kamerabild bleibt im Browser** – nichts davon wird gespeichert, protokolliert oder gesendet; ausgewertet und weitergegeben werden nur Zustände, die 21 Punkte der Hand und Schritt-Gesten.

### Wo

| Funktion | Ort | Kürzel |
| --- | --- | --- |
| Grundschalter «Gesten» (gedrückter Knopf) | Register **Ansicht › Fenster › Gesten**; auch Klick auf das Symbol in der Statusleiste | **GE** |
| Nächster / Voriger Reiter | Register **Ansicht › Fenster › Nächster Reiter / Voriger Reiter** (wechselt in der aktiven Gruppe, am Ende rundherum); Vorgabe der Geste G2 | **RV** / **RZ** |
| Symbol «Gesten» mit vier Zuständen | **Statusleiste**, Mitte, rechts neben «Brille»: aus · Gesten bereit (keine Hand) · Hand erkannt · «Geste: drehen» (Name der aktiven Geste); dazu kurz «Gesten starten …», solange der Browser die Kamera öffnet. Klick schaltet ein/aus, **Rechtsklick** öffnet den Dialog | – |
| Handskelett | kleines Feld **unten rechts** über der Statusleiste: die 21 Punkte der Hand als Linien, **ohne Kamerabild**; Vorgabe ein, abschaltbar im Dialog; nur sichtbar, solange die Gesten laufen. Die Einblendungen (Meldungen) rücken darüber | – |
| Dialog «Gesten» | Register **Verwalten › Hilfe › Gesten** (neben «Tastaturkürzel») oder Rechtsklick auf das Symbol | – |
| Kurzmeldung nach einer Schritt-Geste | schwebend unten rechts, 1,6 s, ersetzt die vorige («Reiter: 3D Lüftung», «Zoom alles», «Seite 2 von 3») | – |

**Kürzel (Begründung):** GE («Gesten»), RV («Reiter vor») und RZ («Reiter zurück») waren frei; alle Kürzel der Oberfläche haben genau zwei Zeichen, GE/RV/RZ sind weder doppelt noch Anfang eines anderen (Test `gesten-ausloesen.test.mjs`, wie der Kürzeltest in `ki-palette.test.mjs`). Alle Kürzel des Bestands: 3D, CM, CO, DE, DI, DL, DR, DT, EL, GR, KI, KS, MD, MM, MV, PA, PF, PI, PP, PX, RM, RO, TX, VV, WA, WC, WN, WT, ZA, ZF (dazu VG, ZE, Entf als zweites Kürzel). Strg+Tab ist Sache des Browsers und nicht abfangbar.

### Bedienung

1. **Einschalten:** GE tippen, Knopf «Gesten» im Register Ansicht oder Klick auf das Symbol in der Statusleiste. **Erst jetzt** lädt KICAD die Gestenerkennung und der Browser fragt nach der Kamera (Vorgabe: aus; ohne eingeschaltete Gesten gibt es keine Last und keine Kamera). Der Schalter bleibt je Benutzer gespeichert (OneDrive) und gilt beim nächsten Start. Die Gesten sind danach **immer aktiv**, solange eine Hand im Bild ist.
2. **Gesten** (Gestenhand: Vorgabe links, die rechte bleibt an der Maus):
   - **G2 Wisch links/rechts:** Ansichtsreiter wechseln (nach rechts nächster, nach links voriger); in einem Dialog, einer Liste oder der Hilfe mit Seiten blättert er die Seiten. Bei offenem Dialog **ohne** Seiten passiert nichts (kein Reiterwechsel hinter dem Dialog).
   - **G3 Wisch hoch/runter:** rollt eine Bildschirmseite (90 % der sichtbaren Höhe) im obersten Dialog oder in der Listenansicht; in Plan, Schema und 3D **ohne Wirkung** (das Geschoss wechselt nicht per Geste). Am Ende meldet «Ende erreicht» bzw. «Anfang erreicht».
   - **G6 Pistolengeste:** «Zoom alles» (in 3D stellt es die Ansicht zugleich wieder aufrecht); bei offenem Dialog ohne Wirkung.
   - **G4, G7** (Faust bewegen, Faust drehen: 3D drehen, um die Blickachse rollen; Plan und Schema verschieben). **G5** (Faust zur Kamera = zoomen) ist ausgeschaltet (Entscheid Christian 26.10.2026: «zoom, ausschalten»; `ZOOM_GESTE` in `gesten-logik.js`) – zoomen mit Mausrad oder G6. Die Gesten löst `gesten.js` selbst über `navigieren()` der aktiven Ansicht aus; die Statusleiste zeigt dabei «Geste: drehen» usw.
3. **Dialog «Gesten»:** oben der Grundschalter und der Zustand in Worten; **Liste der Gesten** (Nr, Geste, Wirkung, Art – wie die Liste der Tastaturkürzel; bei G2 und G6 steht die eingestellte Belegung); **Einstellungen**: Gestenhand links/rechts, Kamera wählen (Standardkamera oder eine der Liste; die Namen erscheinen, sobald der Browser die Kamera freigegeben hat), Handskelett ein/aus, **Belegung** (G2 nach rechts, G2 nach links, G6: je eine Aktion aus dem Aktionsregister oder «keine Wirkung»), «Auf Vorgabe zurück» (alles ausser dem Grundschalter); **Empfindlichkeit**: je Stellschraube der Gestenlogik eine Zeile mit Wert, Einheit, Bereich, Erklärung und «Vorgabe» (die Zeilen entstehen ohne Sonderfälle aus `STELLSCHRAUBEN_VORGABE`; jeder Wert wird auf seinen Bereich begrenzt); **Testfeld** (siehe unten); **Punktfolge aufnehmen**.
4. **Testfeld:** Solange der Dialog offen ist, steht die Steuerung im Testmodus (`testmodus(true)`): erkannte Gesten erscheinen im Testfeld mit dem, was sie auslösen *würden*, lösen aber nichts aus – weder die fliessenden Gesten noch die Schritt-Gesten. Beim Schliessen endet der Testmodus.
5. **Punktfolge aufnehmen:** «Aufnahme starten», Gesten ausführen, «Aufnahme beenden und speichern …»: der Browser bietet eine JSON-Datei `gestenfolge-<Datum>.json` an. Sie enthält **nur Punkte und Zeiten** (`version`, `hand`, `bildbreite`, `bildhoehe`, je Bild `t`, `punkte` mit Tiefe, `welt`, `haendigkeit`, `score`, `weitere` Hände, `eingaben` = Zeiten von Maus und Tasten für die Sperre), nie ein Bild – alle anderen Felder fallen weg (`aufnahmeDatei`). Die Dateien sind die Testfolgen für `gesten-logik.test.mjs`.

### Erwartung

- Ohne eingeschaltete Gesten: keine Kamera, kein Laden von `gesten.js`/MediaPipe, die Statusleiste zeigt «Gesten aus». Fehlt die Gestenerkennung in einer Fassung oder verweigert der Browser die Kamera, zeigt KICAD eine Meldung (deutsch, mit Hinweis), die Statusleiste «Gesten aus» (Tooltip «Zuletzt: …»), der Schalter springt zurück auf aus – der Start von KICAD bricht nie ab.
- Im eigenen Fenster einer Ansicht (Zweitfenster) laufen keine Gesten und es gibt kein Symbol; nur das Hauptfenster öffnet die Kamera (ein Gerät, eine Erkennung).
- Eine Schritt-Geste löst nur Aktionen der Liste «belegbar» aus (Ansicht und Navigation: Reiter vor/zurück, Zoom alles, Zoom alle Ansichten, Fenster nebeneinander, Fenster als Reiter); eine von Hand in die Einstellung geschriebene andere Aktion (z. B. Löschen) wird ignoriert.
- Das Menüband zeigt «Gesten» gedrückt, wenn die Gesten an sind – auch wenn der Schalter in der Statusleiste umgelegt wurde.

### Erkennung und Navigation (Teil hlks-kern)

- **Faust hält das Modell** (Lesart bestätigt von Christian 26.10.2026): Faust im Bild schliessen, kurz (250 ms) stillhalten, dann bewegen → die 3D-Ansicht dreht frei um die Bildschirmachsen um die Mitte der Auswahl bzw. des Modells; die Mitte bleibt im Bild stehen. Handgelenk wie einen Drehknopf drehen (ab 8°) → **Rolle um die Blickachse**. Faust zur Kamera hin/weg bewirkt nichts (G5 ausgeschaltet). Faust lösen → sofort Schluss, ohne Nachlauf. Im Grundriss und im Schema verschiebt die Faust.
- **Aufrichten:** Klick auf den Navigationswürfel bzw. den Haus-Knopf richtet weich auf, Ziehen am Würfel sofort; ebenso Zoom alles (ZF, G6), Begehen, VR und Blickpunkte. Der Würfel zeigt die Rolle mit. Die Maus dreht in einer gerollten Ansicht weiter um die Hochachse des Modells, die Rolle bleibt.
- **Gegen Fehlauslösungen:** Ein Wisch zählt nur aus der Ruhe, mit der Hand mindestens 300 ms im Bild, als Strich innert 450 ms, nicht nach aussen am Bildrand und nicht innert 300 ms nach Maus oder Taste. Eine Faust, die schon geschlossen ins Bild kommt (Tasse, Telefon), navigiert nicht. Die Pistolengeste löst beim Lösen aus, nach 200–1500 ms ruhigem Zeigen; wer länger zeigt, löst nichts aus.
- **Erkennung:** MediaPipe HandLandmarker 1.1.1 lokal unter `wwwroot/lib/mediapipe/` (kein CDN), im Worker `js/gesten/gesten-worker.js`, Kamera 640×480. Nach 10 s ohne Hand sinkt die Rate auf 5 Bilder/s; im Hintergrund gibt KICAD die Kamera frei – versteckt oder seit 2 s ohne Fokus (Befund 27.10.2026: ein Fenster hinter anderen hielt die Kamera fest und sperrte sie für andere Programme). Der Worker sperrt jede Netzanfrage ausser `/lib/mediapipe/` (MediaPipe versucht von sich aus, Nutzungsdaten an Google zu senden – das wird blockiert), dazu `Content-Security-Policy: connect-src 'self'`.
- **Schnittstelle** `navigieren({ drehen, rollen, verschieben, zoom })` jeder Ansicht und `navigierenArt` ('3d' | '2d'), Rechnung in `js/ansichten/navigieren-logik.js`; auch für Tests und die KI nutzbar.
- **Stellschrauben** (alle Annahmen, im Dialog einstellbar): u. a. `anstoss_weg` 0,045 m, `anstoss_tempo_ms` 0,35 m/s, `randwinkel_grad` 25°, `totbereich` 1,5 % der Bildbreite, `rollen_min_grad` 8°, `sperre_ms` 300, `bildwinkel_grad` 60°.

### Befunde der Abnahme 26.10.2026 und Abbruchprotokoll

- **Geändert nach Befunden von Christian:** Händigkeit nicht mehr getauscht (die linke Hand galt als rechte), Kamera mit voller Auflösung 1920 × 1080 (volles Sichtfeld), Erkennungsbild 960 Punkte breit, Lücken der Erkennung bis 350 ms überbrückt, alle fliessenden Gesten gedämpft (Drehen 3°/cm, Verschieben 2 %/cm, Rollen 1 : 2, Glättung 160 ms), **G5 (Zoom per Faust) ausgeschaltet**. Der Dialog «Gesten» zeigt oben eine Messzeile (Bildrate der Erkennung, Kamera- und Erkennungsbild).
- **Abbruchprotokoll:** Jedes Ende einer laufenden Faust meldet die Logik mit Grund (`faust gelöst`, `zeigefinger gestreckt`, `hand öffnet sich`, `hand verloren`, `hand neu im bild`), Dauer und einigen Massen; `js/oberflaeche/gesten-protokoll.js` schickt es an `POST /api/gesten/abbruch` (`GestenProtokoll.cs`), der Server hängt es an `%LOCALAPPDATA%\HLKS_CAD\gesten\abbrueche.jsonl` an (Ordner mit `HLKS_GESTEN_PROTOKOLL`). Angenommen wird nur `{ geste, dauerMs, grund, werte { Name: Zahl } }` – keine Punkte, keine Bilder.
- **Offen:** Das Tuning (Abbrüche nach 1–2 s, enger Bereich vor der Kamera) läuft im Labor (Entscheid Christian 26.10.2026: «wir machen das tuning in der labor app»); die «Fertig, wenn»-Liste ist am Desktop noch nicht erfüllt.

### Schnittstelle «Seiten» für Dialoge und Listen (Vertrag für G2/G3)

Ein Dialog oder eine Ansicht kann Seiten anbieten, die G2 blättert: ein Element mit `data-seiten`, darin Knöpfe `data-seite="zurueck"` und `data-seite="vor"` (optional ein Element `data-seite-text` mit «Seite 2 von 3»), oder eine Reiterliste `role="tablist"` mit `role="tab"` (`aria-selected`). Heute nutzt das noch kein Dialog; die Hilfe (`/hilfe/`) ist ein eigenes Browserfenster und hört nicht auf die Gesten dieses Fensters. G3 rollt die grösste rollbare Fläche (`overflow-y: auto|scroll`) des obersten Dialogs bzw. der Listenansicht.

### Speicherung und Dateien

- **Benutzereinstellung `gesten`** (je Benutzer über OneDrive, `BenutzerEinstellungen.cs`, Prüfung `GestenEinstellung.cs`): `{ an (Vorgabe false), hand ("links"), kameraId (null), skelett (true), stellschrauben { Name: Zahl } (nur Abweichungen), belegung { G2 { links, rechts }, G6 } }`; jedes Feld wahlweise, unbekannte Felder und Nicht-Einstellungen (Bilddaten) lehnt der Server ab. Der JS-Zugriff steht in `js/gesten/gesten-steuerung.js` (über `benutzer.js`).
- **Dateien** unter `wwwroot/js/gesten/` (Teil Oberfläche): `gesten-text.js` (Einstellung, Liste, Status, Stellschrauben, Skelett, Punktfolge – rein), `gesten-steuerung.js` (Steuerung mit Attrappe testbar; `import('./gesten.js')` nur beim Einschalten), `gesten-ausloesen-logik.js` und `gesten-ausloesen.js` (Entscheidung und Ausführung der Schritt-Gesten), `gesten-umgebung.js` (das Dokument lesen, Register bedienen), `gesten-status.js`, `gesten-skelett.js`, `gesten-dialog-text.js` und `gesten-dialog.js`, `gesten-oberflaeche.js` (Verdrahtung); dazu `css/gesten.css`. Geändert: `aktionen-ansicht.js` (Aktionen), `aktionen-projekt.js` (Dialog), `statusleiste.js`, `menueband.js` (Ereignis `hlks-aktionszustand`), `dialoge.js` (Meldung `ersetze`/`dauer`), `fenster.js` und `anordnung.js` (`reiterWechseln`, `reiterNachbar`).
- **Annahmen:** Wisch nach rechts bzw. runter ist vorwärts (nächster Reiter, nächste Seite, weiter nach unten); die Zuordnung steht allein in `gesten-ausloesen-logik.js` (`SCHRITT`). Schritt-Gesten im Testfeld löst die Oberfläche selbst nicht aus (auch wenn `gesten.js` sie weiter meldet).

### Prüfen
- **Tests:** Node `gesten-ausloesen.test.mjs` (Einstellung, Liste, Status, Stellschrauben, Skelett, Punktfolge, Auslösen mit Attrappe von `gesten.js`, Steuerung ohne Last, Dialogtexte, Kürzel ohne Kollision, keine Bilddaten, Anleitung); Web `GestenEinstellungTests` (Rundlauf, Vorgaben, Fehlerfälle).
- **Nur mit Kamera am Desktop prüfbar** (Hauptlauf, Abnahme am realen Projekt Test_praxis): Fehlauslösungen bei normaler Arbeit, Bildrate der 3D-Ansicht mit und ohne Gesten, ob Wisch nach rechts als «nächster» natürlich ist, die Namen der Kameras im Dialog.
- **Anleitung:** Kapitel «Gesten am Arbeitsplatz» (`mg`) in `berichte/anleitung/inventar.json`; Bilder folgen durch den Hauptlauf (Vorgaben im Feld «bild»).

## XR-Labor (Labor-App), L0: eigener Server neben KICAD (26.10.2026)

*Stand vor H+K XR E1 (26.10.2026): Seitdem gibt es keine eigene Labor-App mehr; das Labor installiert und koppelt dieselbe App «H+K XR» (`ch.hkplanung.xr`, Ablage `…\hk_xr`, Einstellung `quest.app_ablage`) als Werkzeug «XR-Labor» (Eingang `kopplung_xrlabor.json`). In der Tabelle unten gilt für die Zeile «App in der Brille» und in «Prüfen» der Abschnitt «H+K XR (Empfänger-App), E1 – PC-Seite» am Ende.*

Spezifikation `entwurf/labor-app.md` (freigegeben von Christian am 26.10.2026), Stufe L0 «Gerüst und Durchstich». Das XR-Labor ist eine **eigene App** neben KICAD:
PC-Teil `src/Hlks.Labor` (dieser Abschnitt), Brillen-App «XR-Labor» `src/Hlks.Labor.Quest` (App-Kennung `ch.hkplanung.xrlabor`). Es braucht **kein KICAD-Projekt**,
KICAD kann daneben laufen oder ganz fehlen. In der Brille läuft immer nur eine der beiden Apps. Die Labor-Funktionen (Stellschrauben live, Varianten, Prüfaufgaben,
Fragebogen, Auswertung) folgen mit L1 (Abschnitt «XR-Labor (Labor-App), L1» unten); in L1 bleibt der Dialog «XR-Labor» in KICAD unverändert.

**Start:** `dotnet Hlks.Labor.dll [--port 5430] [--katalog <ordner>]` (gebaut nach `<bau>\Hlks.Labor\bin\Debug\net10.0\Hlks.Labor.dll`; Desktop-Symbol «XR-Labor»
mit `werkzeuge\labor_starten.ps1`). Nur `http://localhost:<Port>`. Die Seite kommt aus `src/Hlks.Labor/wwwroot`, übersteuerbar mit `HLKS_LABOR_WEBROOT` (der Desktop
startet aus einer Arbeitskopie); Kataloge wie KICAD (`--katalog`, `HLKS_KATALOG`). Keine Dateiwächter, Konfiguration ohne Neuladen (wie KICAD, Befund R 19.10.2026).

| Teil | Labor | KICAD (unverändert) |
| --- | --- | --- |
| Seite am PC | 5430, nur localhost | 5130 |
| Netzzugang der Brille (HTTPS, TCP) | 5340 | 5140 |
| Suche (UDP) | 5341 | 5141 |
| Netzzugang öffnet sich nur auf | Labor auf 5430 (Testserver: `HLKS_LABOR_DESKTOP_PORT`) | KICAD auf 5130 (Testserver: `HLKS_XR_DESKTOP_PORT`) |
| Schalter, Kopplungen, Weg der App, Messprotokolle | `%LOCALAPPDATA%\HLKS_CAD\xr\labor\` (`zugang.json`, `kopplungen.json`, `quest_app_weg.json`) | `%LOCALAPPDATA%\HLKS_CAD\xr\` |
| Zertifikat | dasselbe wie KICAD: `%LOCALAPPDATA%\HLKS_CAD\xr\zertifikat.dpapi` | dort |
| App in der Brille | `ch.hkplanung.xrlabor`, Ablage `%USERPROFILE%\HLKS_CAD_werkzeuge\labor_app` (`werkzeuge\quest_app_bereitstellen.ps1 -Labor`), Einstellung `labor.app_ablage` (nur gelesen) | `ch.hkplanung.kicad.quest`, `…\quest_app`, `quest.app_ablage` |

- **Gleichzeitig mit KICAD:** beide Netzzugänge dürfen zugleich offen sein. Die Kopplungen sind getrennt: eine mit KICAD gekoppelte Brille kommt nicht ins Labor und
  umgekehrt (401). Das Labor schreibt KICADs `zugang.json`, `kopplungen.json` und `quest_app_weg.json` nie, auch nicht den VR-Auftrag (`vr.auftrag`) und keine
  Benutzereinstellungen (es gibt dafür keinen Endpunkt). Es liest nur `quest.adb` (adb wird in KICAD unter «Quest verbinden» eingerichtet), `labor.app_ablage` und den
  Symbolsatz. Im «willkommen» des Kanals steht nie KICADs VR-Auftrag.
- **Im Testlauf** (`HLKS_TEST` oder Testrunner): Netzzugang nur 127.0.0.1 mit freien Ports, Schalter und Kopplungen unter `%TEMP%\HLKS_CAD\xr_test\labor`, Zertifikat
  unter `%TEMP%\HLKS_CAD\xr_test`, Ablage der App `%TEMP%\HLKS_CAD\quest_ohne_labor_app`, nie ein echtes adb.
- **Transport aus KICAD, nicht kopiert:** `Hlks.Labor` verweist auf `Hlks.Web` und ruft dessen Teile mit eigenen Werten auf (`AddXrZugang`, `MapXr`, `MapQuestApp`,
  `UseHerkunftsschutz`; Werte in `src/Hlks.Labor/LaborDienste.cs`: `LaborXr`, `LaborMarke`, `LaborQuest`). In KICAD dafür neu, mit unveränderten Vorgaben:
  `XrEinstellungen.PortStandard`, `SuchPortStandard`, `ZertifikatOrdner`, `ServerName`; `QuestEinstellungen.AppMarke` (`QuestAppMarke`, Vorgabe `QuestAppMarke.Kicad`);
  `VrAuftragSpeicher(…, ablage: false)`; in der Oberfläche die Option `marke` von `questAppTeil` (`KICAD_MARKE` in `quest-app-text.js`).

**Endpunkte des Labors** (nur localhost, Herkunftsschutz wie KICAD):

- `GET /api/status` → `{ app: "xr-labor", fassung, commit, beschaeftigt: false }` (Fassung und Commit aus der Programmdatei, Version «0.1.0+&lt;Commit&gt;»; ohne
  Commit `null`). Das Startskript erkennt daran das laufende Labor.
- `/api/xr/…` wie in KICAD (Abschnitt «Modul XR: Zugang im Büronetz»): `zugang` (GET/POST), `kopplung/paket`, `kopplung/bestaetigen`, `kopplung/ablehnen`,
  `kopplung/widerrufen`, `teilnehmer`, `stellschraube`, `desktop/zustand`, `desktop/beenden`; für die Brille über den Netzzugang `verbindung`, `kanal`,
  `kopplung/anfrage|aufdecken|abholen`, `desktop`. Das Kopplungspaket nennt Port 5340 und Suchport 5341.
- `/api/quest/app`, `/api/quest/app/netz`, `POST /api/quest/app/installieren` wie in KICAD (Abschnitt «Modul XR: Quest App in KICAD»), mit der App «XR-Labor».
- **Nicht im Labor:** Projekt, Kern-Abfragen und -Befehle, `/api/benutzer/…`, `/api/vr/auftrag`, `/api/quest` (Quest verbinden), `/hilfe/`, Modell-GLB (Testraum folgt mit L1).

**Seite «XR-Labor»** (`src/Hlks.Labor/wwwroot`): Kopf mit Fassung und Commit; Bereich **Brille** mit dem Teil «Quest App» aus KICAD (dieselbe Datei
`js/oberflaeche/quest-app-dialog.js` mit der Marke des Labors: Netzzugang ein/aus, «App installieren/aktualisieren» über WLAN oder Kabel, gekoppelte Brillen mit
«Widerrufen», Ersatzkopplung mit Code; ohne Ordnerfeld und ohne Schalter «Brillen in der 3D-Ansicht») und darunter **Verbundene Brillen im Kanal** (alle 2 s); Bereich
**Labor** war in L0 ein Platzhalter; seit L1 stehen dort die Register (siehe unten). Stil, Design (hell/dunkel) und Module kommen aus `src/Hlks.Web/wwwroot` als zweitem Wurzelordner (nur `css/` und `js/`,
aus derselben Arbeitskopie wie die Labor-Seite), nicht als Kopie.

**Prüfen**
- **Tests:** `dotnet test tests/Hlks.Labor.Tests` (Werte des Labors und unveränderte KICAD-Vorgaben, Start als eigener Prozess auf freiem Port mit `HLKS_TEST`,
  `/api/status`, Seite und Auslieferung aus KICAD, Herkunftsschutz, Kopplungspaket 5340/5341, Desktop-Port-Sperre, Kanal ohne KICADs VR-Auftrag, KICAD und Labor
  gleichzeitig mit demselben Zertifikat, Installation mit der adb-Attrappe: Paket und Eingang `ch.hkplanung.xrlabor`). In `tests/Hlks.Web.Tests`: `XrZertifikatTests`,
  `QuestAppTests`, `VrAuftragTests` (KICAD-Vorgaben). Node: `node --test src/Hlks.Labor/wwwroot/labor/labor.test.mjs` und
  `src/Hlks.Web/wwwroot/js/oberflaeche/quest-app.test.mjs` (Marke, KICAD-Texte unverändert).
- **Am PC:** `dotnet <bau>\Hlks.Labor\bin\Debug\net10.0\Hlks.Labor.dll --port 5430`, dann `http://localhost:5430/` – die Seite zeigt «Quest App» mit «Netzzugang ist aus»;
  `http://localhost:5430/api/status` meldet `xr-labor`. KICAD auf 5130 läuft daneben weiter.
- **Nur mit der Brille prüfbar:** Installation und Kopplung der App «XR-Labor» (Kabel und WLAN), Verbinden mit 5340, beide Apps zugleich installiert.

## XR-Labor (Labor-App), L1: Server, Seite und Ablage (26.10.2026)

Stufe L1 «Umzug VR-Labor», Teil Server, Seite und Ablage (Zuschnitt L1 in `entwurf/labor-app.md`). Das Labor kann jetzt alles, was der Dialog «XR-Labor» in KICAD kann.
**KICAD behält in L1 seine Labor-Endpunkte und den Dialog unverändert (paralleler Betrieb)**; es wird kein Code kopiert: das Labor ruft den Code aus `Hlks.Web` mit eigenen
Werten auf. Das Entfernen aus KICAD folgt mit L2. Nicht Teil dieses Abschnitts: Testraum und Etagen des Labors (`/api/xr/geschosse`, `/api/xr/geschoss/{id}.glb`), die
Brillen-Apps.

**Seite** (`http://localhost:5430/`, Titel «XR-Labor»): ein Reiterband mit **Brille** (L0: Netzzugang, App installieren, Kopplung, Teilnehmer im Kanal), **Stellschrauben**,
**Profile**, **Varianten**, **Prüfaufgaben**, **Auswertung**, **Fragebogen** (die sechs Bereiche des KICAD-Dialogs, gleiche Optik) und **Daten**. Der Inhalt der sechs Bereiche ist
derselbe Code wie der Dialog (`js/oberflaeche/xr-labor-dialog.js`, `xr-labor-text.js`, CSS `xl-…`): `xrLaborAufbauen(opt)` baut ihn, `xrLaborDialog()` hängt ihn in einen Dialog
(KICAD, Optionen leer = Verhalten wie vor L1), `xrLaborSeite(behaelter, opt)` in einen Behälter der Labor-Seite. Optionen: `ort` (`kicad` | `labor`, Erklärtexte), `profile`
(Profilquelle: KICAD `benutzerProfilquelle()` = Benutzereinstellungen wie bisher; Labor `labor/labor-profile.js` = `/api/labor/profile`), `marken`, `zusatz` (eigene Reiter vorne
und hinten, hier Brille und Daten), `start`. Mit `?bereich=<id>` öffnet die Seite einen Reiter (`brille`, `stellschrauben`, `profile`, `varianten`, `aufgaben`, `auswertung`,
`fragebogen`, `daten`).

- **Marke der Brille:** Die Teilnehmerliste (Reiter Brille und Kopfzeile) nennt die App jeder Brille (`hallo.marke`: `kicad` bzw. `xrlabor`, `Hlks.Xr/Netz/AppMarke.cs`).
  Prüfaufgaben und Varianten gehen im Labor **nur an Brillen mit der Marke `xrlabor`**, wenn eine solche verbunden ist; sonst wie in KICAD an alle, und die Antwort trägt
  einen `hinweis` («Keine Brille mit der App «XR-Labor» verbunden: der Auftrag ging an alle n verbundenen Brillen»), den die Seite über den Bereichen und in der Meldung zeigt.
  Eine Variante wird für künftige Verbindungen immer gemerkt, auch wenn sie nur an die Labor-Brillen ging. Live-Stellschrauben (`POST /api/xr/stellschraube`) gehen an alle
  (Transport von `MapXr`, schon in L0).
- **Daten (Reiter):** Ablage mit Ordner, Quelle, Rechner und Zahl der Dateien; die bisherigen Daten dieses PCs; Stand der Übernahme mit Zahlen; Knopf **Erneut übernehmen**.

**Ablage der Labor-Daten** je Benutzer über OneDrive (`src/Hlks.Labor/LaborAblage.cs`):

```
%OneDrive%\HLKS-CAD\labor\
  messungen\<JJJJMMTT-hhmmss>_<rechner>_<teilnehmer>.jsonl   Messprotokolle der Brillen (Kanal), alle Rechner
  fragebogen\<JJJJMMTT-hhmmss>_<rechner>_<name>.json
  profile\<kennung>.json                                     { name, werte } wie bisher «labor.profil.<kennung>»
  aktiv.json                                                 { kennung | null, zeit, rechner } wie bisher «labor.profil_aktiv»
  uebernahme_<rechner>.json                                  Merker der Übernahme (Zeit, Zahlen)
```

Wahl wie `RechnerAblage`: Umleitung `HLKS_LABOR_DATEN` › OneDrive des Benutzers › `%APPDATA%\HLKS-CAD\labor`. Dateinamen tragen den Rechnernamen (zwei Rechner schreiben nie
dieselbe Datei), geschrieben wird über eine temporäre Datei im selben Ordner. **Schutz im Testlauf** (`HLKS_TEST` oder Testrunner), wie bei den Benutzereinstellungen: nur unter
`%TEMP%` gelesen und geschrieben; zeigt die Ablage woandershin, wird das Schreiben abgelehnt («… werden im Testlauf nicht beschrieben») und das Lesen sieht nichts. Die Auswertung liest
alle Dateien in `messungen` (alle Rechner; die neuesten 300, zusammen höchstens 64 MB).

**Endpunkte des Labors** (zusätzlich zu L0; nur localhost, Herkunftsschutz wie KICAD):

- `GET /api/xr/labor/katalog` (auch über den Netzzugang 5340 mit Schlüssel, ETag/304), `GET /api/xr/labor/auswertung`, `GET`/`POST /api/xr/labor/fragebogen`,
  `POST /api/xr/labor/variante`, `POST /api/xr/labor/aufgabe`: derselbe Code wie in KICAD (`XrLaborEndpunkte.cs`, Beschreibung im Abschnitt «Modul XR: XR-Labor»). Im Labor
  kommt das Profil «gilt für die App» aus `labor\profile`/`aktiv.json` (bereinigt wie bisher), die Fragebogen gehen nach `labor\fragebogen`, die Messprotokolle des Kanals nach
  `labor\messungen`.
- `GET /api/labor/profile` → `{ aktiv, profile: [{ kennung, name, anzahl, werte }] }` (nach Name); `POST /api/labor/profile` `{ kennung, profil: { name, werte } }` speichert
  (Prüfung wie `XrLaborProfil.Pruefen`: Kennung `[a-z0-9_]` bis 24 Zeichen, Name bis 40, höchstens 128 Werte; höchstens 100 Profile); `POST /api/labor/profile/aktiv` `{ kennung | null }`;
  `DELETE /api/labor/profile/{kennung}` (war es das aktive, gilt danach keines). Dateien mit ungültiger Kennung (Konfliktkopien von OneDrive) oder ungültigem Inhalt werden übergangen.
- `GET /api/labor/daten` → Ablage (Ordner, Quelle, Rechner, erlaubt, Zahl der Dateien), bisherige Daten, Merker der Übernahme; `POST /api/labor/uebernahme` übernimmt erneut.

**Übernahme der bisherigen Daten** (Entscheid Christian 26.10.2026 «Übernehmen»; `LaborUebernahme.cs`): beim ersten Start des Labors (kein Merker `uebernahme_<rechner>.json`) und
auf «Erneut übernehmen». **Kopiert, nie verschoben, nie gelöscht:** `%LOCALAPPDATA%\HLKS_CAD\xr\messprotokolle\*.jsonl` → `labor\messungen`, `…\xr\fragebogen\*.json` →
`labor\fragebogen` (der Rechnername wird nach dem Zeitstempel in den Namen eingefügt), die Benutzereinstellungen `labor.profil.*` → `labor\profile`, `labor.profil_aktiv` →
`aktiv.json` (nur, wenn im Labor noch kein Profil gilt); die Benutzereinstellungen werden nur gelesen. Doppelte: gleicher Inhalt (SHA-256) wie eine Datei im Ziel → übersprungen; eine
Messdatei, die seit der ersten Übernahme gewachsen ist (die Quelle beginnt mit dem Inhalt der Kopie), ersetzt die Kopie («aktualisiert»); ein im Labor anders gespeichertes Profil
bleibt («abweichend»). Der Merker nennt Zeit und Zahlen (gefunden, kopiert, aktualisiert, doppelt, Fehler). Die Quelle der Dateien lässt sich mit `HLKS_LABOR_ALTDATEN`
(XR-Ordner je PC) umleiten; im Testlauf wird nur unter `%TEMP%` gelesen.

**Neu in `Hlks.Web`** (die Vorgaben von KICAD sind unverändert; die bestehenden C#-Tests von KICAD laufen ohne Anpassung und prüfen jetzt zusätzlich die Dateinamen ohne Rechnername, im Node-Test des Dialogs wurde nur der Eintrag der Reiter in der Liste der geprüften Vorlagen nachgeführt):
`XrLaborQuelle` (Katalogordner, Profilquelle, Fragebogenspeicher, `NurMarke`/`MarkeName`) mit der Überladung `MapXrLabor(app, XrLaborQuelle)` – die bisherige `MapXrLabor(app, dienst)`
baut daraus die KICAD-Quelle; `XrMessAblage` (Ordner, Rechnername, Sperre; optionaler Dienst, den `XrKanal` nimmt); `XrFragebogenSpeicher(ordner, jetzt, rechner?, sperre?)`;
`XrKanal.StellschraubeSetzen(…, nurMarke)`, `XrKanal.AnzahlMarke`; `XrHallo.Marke` (`hallo.marke`, Kleinbuchstaben und Ziffern bis 16 Zeichen, sonst abgelehnt) und
`marke` in `GET /api/xr/teilnehmer`.

**Prüfen**
- **Tests:** `dotnet test tests/Hlks.Labor.Tests` (neu: `LaborDatenTests` – Wahl der Ablage, Schutz im Testlauf, Schreiben über temporäre Datei, Profile-Endpunkte, Katalog mit Profil aus
  dem Labor (lokal und im Netzzugang), Messprotokoll nach `labor\messungen`, Auswertung über mehrere Rechner, Fragebogen mit Rechnername, Marke und Ziel von Aufgabe und
  Variante, Live-Stellschrauben, Übernahme einmalig/erneut/Doppelte/gewachsene Dateien/geänderte Profile, nichts in KICADs Ablage; `LaborApiTests`: alle Module der Seite werden
  ausgeliefert). `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~XrLabor|FullyQualifiedName~XrKanal|FullyQualifiedName~XrZugang|FullyQualifiedName~BenutzerEinstellungen"`
  (KICAD-Vorgaben, `hallo.marke`). Node: `node --test src/Hlks.Web/wwwroot/js/oberflaeche/xr-labor.test.mjs src/Hlks.Labor/wwwroot/labor/labor.test.mjs`.
- **Am PC (Probe ohne echte Daten):** `HLKS_TEST=1`, `HLKS_LABOR_DATEN=%TEMP%\…\labor`, `HLKS_LABOR_ALTDATEN=%TEMP%\…\alt_xr`, `HLKS_BENUTZER=%TEMP%\…\benutzer`, `HLKS_KATALOG=<Arbeitskopie>\kataloge`;
  `dotnet <bau>\Hlks.Labor\bin\Debug\net10.0\Hlks.Labor.dll --port 5439`, dann `http://localhost:5439/?bereich=daten` (Zahlen der Übernahme) und die übrigen Reiter.
- **Im Betrieb prüfen (Christian):** Reiter **Daten** zeigt `%OneDrive%\HLKS-CAD\labor` und nach dem ersten Start die Zahlen der übernommenen Messprotokolle der Brillentests 23.–25.10.2026;
  **Auswertung** zeigt diese Läufe je Aufgabe, Variante und Fassung; ein Profil unter **Profile** speichern und «Für die App festlegen» – die App holt es beim nächsten Verbinden.
- **Nur mit der Brille prüfbar:** Marke im `hallo` der echten Labor-App (L0-Brillentest), Prüfaufgabe nur in der Labor-Brille, Messprotokoll der Labor-Brille in `labor\messungen`.

## XR-Labor (Labor-App), L1: Testraum (26.10.2026)

Christian 26.10.2026: Testwelt «Nur neutraler Testraum» – kein Projekt, keine Projektdaten (`entwurf/labor-app.md`, «Testwelt der Labor-App»).

- **Katalog** `kataloge/labor_testraum.json` (Art `hlks-labor-testraum`, Fassung 1): EG (Kote 0) und OG (Kote 2900), je ein Raum innen
  8,0 × 6,0 m, lichte Höhe 2650 mm, Wände 200 mm, Platten 250 mm (Masse = Annahmen, im Katalog gekennzeichnet). Keine Treppe: «etage-wechseln»
  läuft über die App «Etagen». Farben: Wände `#E4E7EB`, Platten `#D9DDE2`, Markierungen als Ring `#6F3E59` (xr_farben «labor»), nie Gewerkfarben.
- **Ziele der Prüfaufgaben** im Katalog: Markierungen (EG 4, OG 3), Wandpaare mit Sollmass (EG Süd–Nord 6000 mm, OG West–Ost 8000 mm), Raum
  für «raum-ar-oeffnen» (`testraum-eg`), Bauteil für «tablet-wert-ablesen» (Höhe 2,65 m). Der Labor-PC prüft Sollmass und Höhe gegen die Geometrie.
- **Endpunkte im Labor** (`src/Hlks.Labor/Testraum*.cs`, gleicher Vertrag wie `XrModellEndpunkte.cs`): `GET /api/xr/geschosse`,
  `GET /api/xr/geschoss/{id}.glb?teil=modell|unterlage` (ETag/304, auch über den Netzzugang 5340). Fehlt der Katalog oder ist er ungültig: 409 mit Stelle.
  Je Etage stehen die Ziele in `scene.extras.kicad.labor` (`raeume`, `markierungen`, `wandpaare`, `bauteile`, KICAD-mm).
- **Brille:** Die Labor-Dateien nehmen zuerst die Ziele aus den extras (`Hlks.Xr/Labor/LaborZiele.cs`), sonst wie bisher (KICAD-App unverändert).
  Ohne Ebenen und Marker (gibt es im Labor nicht, `AppMarke.MitProjekt`) meldet die Labor-App nichts.
- **Prüfen:** `dotnet test tests/Hlks.Labor.Tests --filter "FullyQualifiedName~Testraum"`, `dotnet test tests/Hlks.Xr.Tests --filter "FullyQualifiedName~LaborZiele"`;
  Simulator der Labor-App gegen einen Labor-Server im Testlauf löst alle sieben Prüfaufgaben (Bilder unter `%USERPROFILE%\HLKS_CAD_testdaten\labor\l1\`).

## H+K XR (Empfänger-App), E1 – PC-Seite (26.10.2026)

Spezifikation `entwurf/xr-empfaenger.md` (freigegeben von Christian am 26.10.2026: «eine app für alles, Idee eine reciverapp die für alle tools genutzt werden kann»), Stufe E1,
Abschnitt «Schnittstellen im Bau E1». In der Brille gibt es künftig **eine** App **«H+K XR»** (Kennung `ch.hkplanung.xr`) für alle Werkzeuge; die bisherigen Apps «KICAD»
(`ch.hkplanung.kicad.quest`) und «XR-Labor» (`ch.hkplanung.xrlabor`) sind ersetzt. Dieser Abschnitt beschreibt den **PC-Teil**: was KICAD und das XR-Labor dazu anders tun und zeigen.
Die App selbst (`src/Hlks.Quest`, `src/Hlks.Xr`) baut eine andere Sitzung; Startseite der Werkzeuge und Wechsel in der Brille gehören dazu und stehen nicht hier.
**Reine Rechner-Funktionen** wie bisher (für die KI verboten, `KiEinordnung.RechnerFunktionen` › `quest_app`); der neue Endpunkt `alte-entfernen` fällt unter dieselbe Gruppe.
Die Abschnitte «Quest App in KICAD» (P7) und «XR-Labor (Labor-App), L0» oben beschreiben den Stand vor E1; wo sie Paket, Ablage oder App-Namen nennen, gilt dieser Abschnitt.

### Wo

Register **XR › Gruppe Brille › «H+K XR installieren …»** (`xr.appInstallieren`, Kürzel **XI**, bisher «App installieren …»; Symbol unverändert) öffnet wie bisher den Dialog
«Quest verbinden» mit dem Teil «Quest App». Im XR-Labor: Seite `http://localhost:5430/`, Reiter **Brille**, derselbe Teil. Die Aktions-Ids und alle übrigen Kürzel bleiben.

### Was sich zeigt

| | vorher | jetzt (E1) |
| --- | --- | --- |
| Name der App im Dialog und in Meldungen | «KICAD» bzw. «XR-Labor» | **«H+K XR»** (Werkzeug daneben: «KICAD» bzw. «XR-Labor») |
| Einleitung im Teil «Quest App» | «Die App «KICAD» in der Brille ersetzt auf Dauer den Browser …» | «Die App «H+K XR» … verbindet sich … mit KICAD (und mit weiteren Werkzeugen wie dem XR-Labor) … Sie ersetzt die bisherigen Apps «KICAD» und «XR-Labor».» |
| Ablage der APK am PC | `%USERPROFILE%\HLKS_CAD_werkzeuge\quest_app` (KICAD), `…\labor_app` (Labor) | **`%USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr`** für beide (`ch.hkplanung.xr-Signed.apk`, `version.json`) |
| Einstellung des Ordners | `quest.app_ablage` (KICAD), `labor.app_ablage` (Labor, nur gelesen) | **`quest.app_ablage`** für beide; `labor.app_ablage` entfällt (das Labor liest `quest.app_ablage`, schreibt sie nie) |
| APK bauen | `werkzeuge\quest_app_bereitstellen.ps1` (KICAD) bzw. `… -Labor` | `werkzeuge\quest_app_bereitstellen.ps1` (baut `src\Hlks.Quest\Projects\Android\Hlks.Quest.Android.csproj`); `-Labor` gibt es nicht mehr (Hinweis, es wird die eine App gebaut) |
| Kopplungspaket | Fassung 1 | **Fassung 2**: Fassung 1 plus `werkzeug` (`kicad` \| `xrlabor`) und `werkzeugName` («KICAD» \| «XR-Labor»); Fassung 1 ohne `werkzeug` gilt in der App als `kicad` |
| Eingang auf der Brille | `…/ch.hkplanung.kicad.quest/files/kopplung.json` | **`/sdcard/Android/data/ch.hkplanung.xr/files/kopplung_<werkzeug>.json`** (ein Paket je Werkzeug; die App übernimmt jedes und löscht es im Eingang; ein altes `kopplung.json` gilt als `kicad`) |
| Teilnehmerliste `GET /api/xr/teilnehmer` | ohne Zustand | je Teilnehmer **`aktiv`** (false = die App zeigt gerade ein anderes Werkzeug); die 3D-Figur der Brille am PC erscheint nur für aktive Teilnehmer |
| `GET /api/quest/app` | `paket`, `ablage`, `brille` … | zusätzlich **`app`** («H+K XR»), **`werkzeug`**, **`werkzeugName`** (auch ohne adb und ohne Brille) und **`brille.alteApps`** (`[{ paket, name }]`) |

### Bedienung

1. **Installieren und koppeln:** wie bisher (Netzzugang einschalten, «über Kabel» oder «über WLAN», Knopf «App installieren» bzw. «App aktualisieren»). Es ist **dieselbe App** wie beim anderen
   Werkzeug: Wer KICAD schon eingerichtet hat, installiert im Labor nur noch dessen Kopplung dazu (der Knopf aktualisiert die App; das Paket legt `kopplung_xrlabor.json` neben `kopplung_kicad.json`).
   Die App in der Brille braucht nach dem Wechsel der Kennung **einmal** eine neue Kopplung per Kabel (Vorgabe der Spezifikation: kein Übertrag der alten Kopplung).
2. **Alte Apps entfernen:** Sind in der Brille noch «KICAD» (`ch.hkplanung.kicad.quest`) und/oder «XR-Labor» (`ch.hkplanung.xrlabor`) installiert (`brille.alteApps` nicht leer), erscheint im Teil
   «Quest App» ein Hinweis und der Knopf **«Alte Apps entfernen …»**. Klick → Rückfrage («Die bisherigen Apps «KICAD» und «XR-Labor» von der Brille entfernen? «H+K XR» ersetzt sie und bleibt; …»)
   → bei «Entfernen» `POST /api/quest/app/alte-entfernen { bestaetigt: true }`: KICAD ruft `adb uninstall` für **genau diese zwei Pakete**, soweit sie installiert sind, nie für ein anderes und nie für «H+K XR».
   Ohne `bestaetigt: true` antwortet der Server 400 und ruft kein adb. Ein Fehlschlag bei einer App hindert die andere nicht; die Antwort nennt je App einen Schritt (grün oder rot) und eine Meldung.
   Ist keine alte App mehr da, ist der Knopf nicht sichtbar; ruft man den Endpunkt trotzdem auf, antwortet der Server «Auf der Brille sind keine alten Apps installiert.» (ok, ohne `uninstall`).
3. **Zum Kanal:** Die App hält zu jedem gekoppelten, erreichbaren Werkzeug einen Kanal offen. `hallo` bekommt `aktiv` (bool; fehlt es, gilt `true`); beim Wechsel sendet die App `aktiv { aktiv }`.
   Ein inaktiver Teilnehmer bekommt Aufträge und Stellschrauben wie bisher, sendet aber keine Posen. Ein Wert, der nicht `true`/`false` ist, gibt eine Fehlermeldung des Kanals (die Verbindung bleibt;
   im ersten `hallo` wird sie wie bei jedem ungültigen `hallo` geschlossen).

### Erwartung

- Der Teil «Quest App» nennt überall die App «H+K XR»; der Ordner der App steht auf `…\hk_xr`; «Abgelegte App» zeigt die APK `ch.hkplanung.xr-Signed.apk`.
- «App installieren» schreibt im Kabelweg dieselben sieben Schritte wie bisher; auf der Brille entsteht die App `ch.hkplanung.xr` und im Eingang `kopplung_<werkzeug>.json` (KICAD `kopplung_kicad.json`, Labor `kopplung_xrlabor.json`).
- Das Paket Fassung 2 enthält `werkzeug` und `werkzeugName` des Servers, der es ausgibt (KICAD `kicad`/«KICAD», Labor `xrlabor`/«XR-Labor»); `POST /api/xr/kopplung/paket` gibt es ebenso aus.
- «Alte Apps entfernen …» ist nur sichtbar, wenn mindestens eine alte App installiert ist, fragt immer vorher und entfernt nur «KICAD» und «XR-Labor».
- Im 3D-Fenster am PC erscheint die Figur einer Brille nur, solange ihre App dieses Werkzeug vorn zeigt (`aktiv` fehlt oder `true`).

### Netz einrichten per Knopf (Christian 26.10.2026: «bitte alle netzkomponenten in der app integrieren»; ein Knopf in KICAD und im Labor)

Das Doppelklick-Skript `werkzeuge\xr_netz_einrichten.cmd` muss man nicht mehr suchen: der Teil «Quest App» (KICAD: Register XR › «H+K XR installieren …» bzw. Dialog «Quest verbinden»; Labor: Reiter **Brille**
– dieselbe Oberfläche `quest-app-dialog.js`) hat unter den Netz-Hinweisen den Knopf **«Netz einrichten …»**.

- **Wo / wann sichtbar:** sobald der Netzstand gelesen ist (`GET /api/quest/app/netz`, `gelesen: true`). Besteht einer der beiden Hinweise (Netz nicht «Privat», Regeln der Gruppe «KICAD XR» fehlen bzw. TCP/UDP unvollständig;
  Feld `einrichten.noetig`), steht er gross (hervorgehoben); sonst klein als **«Erneut einrichten …»**. Ist Windows nicht lesbar (Testlauf, kein Windows), fehlt der Knopf.
- **Bedienung:** Klick → Rückfrage: «Das Netz «<Name>» wird als «Privat» eingestuft» (nur die Netze, die nicht privat sind; sonst «Das Netzprofil bleibt, wie es ist»), «die Firewall-Regeln der Gruppe «KICAD XR» werden
  angelegt bzw. erneuert: KICAD TCP 5140/UDP 5141, XR-Labor TCP 5340/UDP 5341 – nur Profil «Privat», nur eigenes Subnetz», «Windows fragt danach nach Administratorrechten». Bei «Einrichten» ruft die Oberfläche
  `POST /api/quest/app/netz/einrichten { bestaetigt: true }`; Windows blendet die Rückfrage der Benutzerkontensteuerung ein, danach läuft `xr_netz_einrichten.ps1` im **eigenen Fenster** (Ergebnis dort sichtbar,
  «Enter zum Schliessen»). Die Oberfläche liest den Netzstand alle 3 s neu (`?neu=1`, höchstens 2 Minuten); sind die Hinweise weg, meldet sie «Netz eingerichtet», sonst nach 2 Minuten «noch nicht eingerichtet»
  mit Verweis auf das Ergebnisfenster und das Doppelklick-Skript.
- **Endpunkt** (`QuestAppEndpunkte.cs`, `QuestAppEinrichten.cs`): nur localhost, Herkunftsschutz, ohne `bestaetigt: true` 400 (nichts gestartet). Er startet `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
  "<Programmordner>\werkzeuge\xr_netz_einrichten.ps1" -Erhoeht -Ja` mit `Verb = runas` und wartet nicht auf das Ende. Antwort `{ gestartet: true }` bzw. `{ gestartet: false, grund }`: lehnt der Mensch die Windows-Rückfrage
  ab (Win32-Fehler 1223), lautet der Grund «Administratorrechte nicht erteilt. Es wurde nichts geändert.»; fehlt das Skript oder läuft schon eine offene Rückfrage, steht das im Grund. Ein zweiter Start bei offener
  Rückfrage wird nicht ausgelöst. Für die KI verboten (`KiEinordnung` › `quest_app_netz`, deckt auch diesen Endpunkt; kein MCP-Werkzeug).
- **Skripte im Programmordner:** `Hlks.Web.csproj` baut `xr_netz_einrichten.ps1` und `xr_firewall.ps1` als Inhalt in den Unterordner `werkzeuge\` des Bauordners (auch der des Labors, über den Projektverweis); der Server
  findet sie über `AppContext.BaseDirectory`, unabhängig von der Arbeitskopie (Desktop, Labor, Tests). Das Skript selbst ist unverändert (Doppelklick-Skript und `-Trocken` gelten weiter).
- **Prozessstart austauschbar:** `IQuestAppEinrichten` (Vorgabe im Testlauf `QuestAppEinrichtenLeer`: startet nie etwas; sonst `QuestAppEinrichtenEcht` mit austauschbarem Startdelegat). Tests starten nie PowerShell mit
  Administratorrechten.
- **Texte:** Die Hinweise nennen den Knopf; das Skript `xr_netz_einrichten.cmd` bleibt als Ersatzweg genannt (und `xr_firewall.ps1` für nur die Regeln). Statt «KICAD ändert die Firewall nie» steht: nur auf Knopfdruck, mit Bestätigung durch Windows.
- **Prüfen:** `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~NetzEinrichten|FullyQualifiedName~QuestAppTests|FullyQualifiedName~KiEinordnung"`; `dotnet test tests/Hlks.Labor.Tests --filter "FullyQualifiedName~QuestApp"`;
  `node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest-app.test.mjs src/Hlks.Labor/wwwroot/labor/labor.test.mjs`. **Nur am echten PC prüfbar:** die Windows-Rückfrage, das Fenster mit dem Ergebnis, die Änderung von Netzprofil und Firewall
  (der Endpunkt wurde im Bau nie echt ausgelöst).

### Prüfen

- **Tests:** `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~QuestAppTests|FullyQualifiedName~XrKopplung|FullyQualifiedName~XrKanalAktivTests"` (Fassung 2, Werkzeug je Server, Eingang je Werkzeug,
  Alte Apps entfernen: nur mit Bestätigung, nur die zwei Pakete, nur wenn installiert, Fehlschläge, Zeitüberschreitung, Zustand, `aktiv` im hallo und als Nachricht);
  `dotnet test tests/Hlks.Labor.Tests` (Marke des Labors: gleiche App und Ablage, Werkzeug `xrlabor`, Installation mit der Attrappe bis zum Start, «Alte Apps entfernen» vom Labor aus).
  Statt `adb.exe` läuft die Attrappe `tests/Hlks.AdbAttrappe` (neu: `pm list packages <Filter>` aus `alte_apps.txt`, `uninstall <Paket>` mit `uninstall.log`, Ziel `kopplung_<werkzeug>.json`).
  Node: `node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest-app.test.mjs src/Hlks.Web/wwwroot/js/oberflaeche/xr-register.test.mjs src/Hlks.Web/wwwroot/js/oberflaeche/xr-labor.test.mjs src/Hlks.Web/wwwroot/js/ansichten/xr-teilnehmer.test.mjs src/Hlks.Labor/wwwroot/labor/labor.test.mjs`.
- **Nur mit der Brille und der neuen App prüfbar:** Installation von `ch.hkplanung.xr` per Kabel, Übernahme beider Pakete in der App, Wechsel zwischen den Werkzeugen und `aktiv` im echten Kanal,
  «Alte Apps entfernen» an der echten Brille (adb gegen die echte Brille lief in diesem Bau nicht).
- **Offen:** Der Browser-Test `xr-app.browser.mjs` ist auf die neuen Namen nachgeführt, aber **nicht gelaufen** (keine Browserreihen). `berichte/anleitung/inventar.json` ist noch auf dem Stand vor E1
  (Texte zu «App installieren …», Ablage, Name der App); die Nachführung gehört zur Bedienungsanleitung nach Abschluss des Moduls.

## XR-Labor Spielwiese S1: App-Menü mit Platzhalter-Apps (26.10.2026)

Neuausrichtung des Labors (`entwurf/xr-empfaenger.md`, Kasten oben; `entwurf/bedien-grundsatz.md`, Kapitel 1). Christian, 26.10.2026: «Aufrufen von Apps: Bsp. Appmenue aufrufen,
apps wählen, menue schliessen. das labor wird durch dich bedient, wir feilen dann nur an diesem grundsatz.» Die heutige Bedienung (Hebegeste, Symbolfeld, Tafel, Blättern,
Wegwischen) ist im XR-Labor ohne KICAD-Inhalt mit neutralen Apps erlebbar. Einzelheiten der Brille: `src/Hlks.Quest/LIESMICH.md`, Abschnitt «XR-Labor Spielwiese S1».

### Wo

- **Brille**, App «H+K XR», Werkzeug **XR-Labor** aktiv, keine Prüfaufgabe läuft: **beide Handflächen nach oben – kurz anheben** holt das Symbolfeld mit **Notizen, Bilder, Uhr,
  Liste, Dokument, Einstellungen** und **Werkzeuge**. KICAD als Werkzeug zeigt unverändert seine Apps (Etagen, Messen, Marker …).
- **Labor-PC**: Katalog `kataloge/labor_spielwiese.json` (Art `hlks-labor-spielwiese`, Fassung 1), ausgeliefert unter `GET /api/xr/labor/spielwiese` (nur GET; auch über den
  Netzzugang 5340 mit Schlüssel; ETag/304; ungültig bzw. fehlend 409 `{ fehler, vorschlag }` mit der Stelle). Kein Knopf, kein Kürzel – die Datei ist die Bedienung.
- **Stellschrauben live**: Labor-Seite `http://localhost:5430/`, Register **Stellschrauben** (bzw. `POST /api/xr/stellschraube { schluessel, wert }`), z. B. `symbole.abstand_m`.

### Bedienung und Erwartung

1. Hebegeste → sieben runde Glas-Symbole (drei Reihen zu zwei, «Werkzeuge» rechts), je App ein eigener Pastellton (Vorschlag S1 in `kataloge/xr_farben.json`).
2. Ein Symbol mit der ganzen Hand berühren (Zeiger und Tooltip beim Annähern) → Leuchten und Ton, die Symbole blenden aus, die App öffnet als Glas-Tafel mittig vor einem und fest.
3. Auf der Tafel wischen: **Dokument** blättert seitlich (drei Seiten), **Liste** (40 Einträge, höhere Tafel wie die Etagen) rollt hoch/runter, **Einstellungen** blättert (zwei
   Seiten), Schalter schalten ohne Wirkung, ein Eintrag der Liste lässt sich antippen (Haken). **Bilder**: sechs Kacheln aus Farbflächen und Formen; **Uhr**: Zeit und Datum.
4. Wegwischen (Hand offen, von rechts nach links) schliesst die Tafel; die Symbole kommen nicht zurück. Hebegeste bei offener Tafel schliesst sie, die Symbole stehen frontal (V1i).
5. Claude ändert Apps und Inhalte in der Datei; die Brille übernimmt die neue Fassung beim nächsten Nachfragen (beim Verbinden, dann alle 30 s) ohne neue APK. Ohne Verbindung gilt
   die zuletzt geholte Fassung, sonst die beim Bau eingebettete.

### Prüfpunkte

- [ ] Im XR-Labor zeigt die Hebegeste die sechs Platzhalter-Apps und «Werkzeuge», keine KICAD-App; ohne laufende Aufgabe keine Aufgabenkarte.
- [ ] Jede App öffnet mittig als Tafel in ihrem Ton; Blättern, Rollen, Schalter und Wegwischen wirken wie in KICAD.
- [ ] Eine Änderung in `kataloge/labor_spielwiese.json` (z. B. ein Name) erscheint nach höchstens 30 s in der Brille; ein Fehler darin meldet `GET /api/xr/labor/spielwiese` mit 409 und Stelle.
- [ ] `symbole.abstand_m` auf der Labor-Seite verstellt das Symbolfeld sofort.

### Prüfen

- **Tests:** `dotnet test tests/Hlks.Xr.Tests --filter "FullyQualifiedName~SpielwieseTests|FullyQualifiedName~WerkzeugTests"`,
  `dotnet test tests/Hlks.Labor.Tests --filter "FullyQualifiedName~SpielwieseEndpunktTests"`.
- **Simulator:** `src\Hlks.Quest\Pruefen\hk_xr_pruefen.ps1 -Lauf Spielwiese` (Labor-Server im Testlauf); Prüfbilder unter `%USERPROFILE%\HLKS_CAD_testdaten\hk_xr\s1\`.
- **Nur mit der Brille prüfbar:** Gefühl der Gesten, Lesbarkeit der Platzhalter, Übernahme einer geänderten Datei über WLAN.
