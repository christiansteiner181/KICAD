# Labor-App – VR-Steuerung und Standardgesten erproben: Spezifikation

Stand 26.10.2026 · Sitzung «Labor-App» (Zweig `claude/labor-app-9c82bf`) · **Entwurf, nicht gebaut**. Gebaut wird erst nach Freigabe
durch Christian. Offene Punkte stehen am Schluss.

## Anlass

Christian, 26.10.2026: «VR-Labor möchte ich als eigenständige App auslagern. Dies dient nur dazu die Funktionalität einer VR-steuerung
zu testen und anzupassen und Standardgesten zu entwikeln. dies nicht nur im bezug auf KICAD, diese erkenntnisse sollen dann für die
interaktion mit KICAD. Darin auch intergriert eine Gesten-Labor für das prüfen und entwerfen, von gesten für die PC nutzung.»

Grundlagen: `entwurf/modul-xr.md` (Interface-Labor, Gestenlabor, P5/P6), `entwurf/modul-gesten.md` (Modul G, Technik, Stufe 3),
`src/Hlks.Xr/Gesten/LIESMICH.md` (Erkennerkern V1b-1).

### Entscheide Christian 26.10.2026 (Fragefenster, gelten fest)

| Frage | Entscheid |
| --- | --- |
| Ort | Im KICAD-Repository als eigene App(s): eigene Quest-App (eigene APK neben `src/Hlks.Quest`) und eigener PC-Start (eigener Port, ohne offenes KICAD-Projekt). Den Gestenkern teilen sich Labor und KICAD (`src/Hlks.Xr/Gesten`, `js/gesten/…` aus Modul G). |
| Was KICAD behält | Nur einen Startknopf: Gruppe «Labor» im Register XR (`ansicht.xrLabor`, Kürzel XL) wird «Labor-App starten». Dialog «XR-Labor» und die Labor-Teile der KICAD-Brillen-App ziehen in die Labor-App um. KICAD liest danach nur noch die festgelegten Standardgesten und Stellschrauben (gemeinsame Datei, je Benutzer über OneDrive). |
| PC-Gesten | Nur entwerfen und prüfen: vorzeigen, messen, Verwechslungsgefahr, als Standard festlegen. Ausgelöst wird vorerst nur in KICAD; kein Windows-Hintergrundprogramm. |
| Zeitpunkt | Jetzt Spezifikation und Planung der VR-Seite. Das PC-Gesten-Labor wird erst gebaut, wenn Modul G Stufe 1 (`claude/modul-gesten`) in `main` ist; dessen Code wird wiederverwendet. |
| Desktop-Icon | Christian, 26.10.2026: «die labor app dann auch gleich mit desktop icon.» → eigenes Symbol «KICAD Labor» auf dem Desktop neben «KICAD», ab Stufe L0. |

## Leitlinien

1. **Das Labor entscheidet, KICAD wendet an.** Probieren, Vergleichen, Messen und Festlegen gibt es nur im Labor. KICAD kennt nur das
   Ergebnis: den **Bedienstandard** (Stellschrauben, Varianten, Gesten). Er gilt so lange, bis im Labor ein neuer festgelegt wird.
2. **Allgemein, nicht nur KICAD.** Die Prüfaufgaben und der Gestenwortschatz sind nach Bedeutungen geordnet (einblenden, ausblenden,
   blättern, wählen, greifen, zoomen …), nicht nach KICAD-Befehlen. Der Bedienstandard ist als lesbarer Leitfaden ausgebbar und taugt
   auch für andere Werkzeuge.
3. **Gleiche Bedeutung, eigene Bewegung je Gerät.** Eine Bedeutung (z. B. «blättern vor») hat in der Brille und am PC dieselbe Idee
   der Bewegung (Leitlinie 4 von Modul G). Die Aufnahmen bleiben aber getrennt: Kopf, Körperbezug und Gelenke unterscheiden sich
   zwischen Brille und Kamera; eine in der Brille vorgezeigte Geste wird nicht am PC erkannt und umgekehrt.
4. **Nichts doppelt bauen.** Die Labor-App nutzt dieselbe Bedienung wie die KICAD-Brillen-App (gemeinsame Quellen), denselben
   Erkennerkern (`Hlks.Xr/Gesten`), dieselbe Gestenlogik am PC (`js/gesten/` aus Modul G) und denselben Transport (Zugang, Kopplung,
   Kanal aus `Hlks.Web`).
5. **Keine Bilder.** Wie in Modul G: Kamerabilder bleiben im Browser. Ins Labor gehen nur Punktfolgen der Hände (wie
   `aufnahmeBeenden()` von Modul G) und Messwerte.

## Ziel und «Fertig, wenn …»

**Fertig (VR-Seite, Stufen L0–L3), wenn …**
- Christian die Labor-App über das **Desktop-Icon «KICAD Labor»** oder in KICAD im Register XR mit «Labor-App starten» (XL) startet
  und die Labor-Seite am PC aufgeht, **ohne** dass ein KICAD-Projekt offen sein muss; KICAD läuft daneben weiter bzw. muss gar nicht
  laufen.
- auf der Quest zwei Apps installiert sind: «KICAD» (wie heute) und «KICAD Labor» (eigenes Symbol); beide installiert der jeweilige
  PC-Teil per Knopf, beide sind getrennt gekoppelt.
- die Labor-App alles kann, was heute das XR-Labor kann: Stellschrauben live, Varianten A/B, Prüfaufgaben mit Messung, Fragebogen,
  Auswertung, und dabei die bisherigen Messprotokolle der Brillentests vom 23.–25.10.2026 im Verlauf zeigt.
- das Gestenlabor in der Brille läuft: Funktion wählen, Geste 3- bis 5-mal vorzeigen, Güte und Verwechslungsgefahr sehen, live
  testen, übernehmen (Pflichtenheft «Gestenlabor» in `modul-xr.md`).
- Christian im Labor «Als Standard festlegen» drückt und die KICAD-Brillen-App beim nächsten Verbinden genau diese Werte und Gesten
  nutzt; ohne laufendes Labor gilt in KICAD der zuletzt festgelegte Standard, ohne jeden Standard die Vorgaben der Kataloge.
- in KICAD kein Labor mehr steckt: kein Dialog «XR-Labor», keine Labor-Endpunkte, keine Prüfaufgaben, kein Messprotokoll, keine
  Live-Stellschrauben, keine Profile; die betroffenen Tests sind gefiltert grün.
- der Bedienstandard als Leitfaden (Tabelle der Bedeutungen mit Geste, Stellschrauben, Messwerten, Entscheid und Datum) als Seite
  angezeigt und als PDF ausgegeben werden kann.

**Fertig (PC-Seite, Stufe L4, nach Modul G Stufe 1), wenn …** Christian an der Labor-Seite mit der eingebauten Kamera
- die Gesten G2–G7 von Modul G vorzeigt und je Geste Erkennungsrate und Fehlauslösungen gemessen werden (Probe «10 Minuten normale
  Arbeit» aus Modul G als geführte Messung),
- die Empfindlichkeiten (Stellschrauben von `gesten-logik.js`) verstellt und die Wirkung sofort im Testfeld sieht,
- eine neue Geste für eine Bedeutung 3- bis 5-mal vorzeigt und Güte und Verwechslungsgefahr gegen G2–G7 und die übrigen eigenen
  Gesten sieht,
- und das Ergebnis als Standard festlegt; KICAD (Modul G) nutzt danach diese Stellschrauben. Eigene neue PC-Gesten löst KICAD erst mit
  Modul G Stufe 3 aus (siehe Stufen).

## Aufbau

### Übersicht

```
                    ┌──────────────────── OneDrive je Benutzer ─────────────────────┐
                    │ %OneDrive%\HLKS-CAD\bedienung\  standard.json, gesten_*.json   │
                    │ %OneDrive%\HLKS-CAD\labor\      Messungen, Fragebogen, Profile │
                    └──────▲ schreibt (nur Labor)                  ▲ liest           ┘
                           │                                       │
  Labor-PC  src/Hlks.Labor (localhost:5430)              KICAD  src/Hlks.Web (localhost:5130)
  Seite Labor, Kamera-Gesten (L4)                         Knopf «Labor-App starten» (XL)
  Netzzugang Brille 5340/5341 ──┐                         Netzzugang Brille 5140/5141 ──┐
                                │ TLS, Kopplung, Kanal                                  │
  Brille  «KICAD Labor»  src/Hlks.Labor.Quest            Brille  «KICAD»  src/Hlks.Quest
          gleiche Quellen wie Hlks.Quest + Labor-Teile            ohne Labor-Teile
                    └──────── beide: Hlks.Xr (Logik, Gesten, Netz) ────────┘
```

### Projekte

| Projekt | Inhalt | Neu / bestehend |
| --- | --- | --- |
| `src/Hlks.Labor` | PC-Teil der Labor-App: eigenes `Program.cs`, Port 5430 (nur localhost), eigene Oberfläche `wwwroot/`, Labor-Endpunkte, Ablage der Labor-Daten, Schreiben des Bedienstandards, Netzzugang für die Labor-Brille (Ports 5340/5341). Verweist auf `Hlks.Web` (Transport: `XrZugang`, `XrKopplung`, `XrKanal`, `XrZertifikat`, `XrSuche`, `XrModellEndpunkte`, `XrDesktop*`, `QuestApp`/`QuestDienst` für Installation per adb) und `Hlks.Kern` (Testwelt als GLB). Die Gestenlogik von Modul G (`js/gesten/`, `lib/mediapipe/`) und die gemeinsamen Teile der Oberfläche (Stil, Symbole) bindet es als verlinkte Dateien aus `src/Hlks.Web/wwwroot` ein, nicht als Kopie. | neu |
| `src/Hlks.Labor.Quest` | Brillen-App «KICAD Labor», App-Kennung `ch.hkplanung.kicad.labor`. Übersetzt **dieselben Quellen** wie `src/Hlks.Quest` (wie heute schon die Android-Fassung `..\..\**\*.cs` einbindet) plus die Labor-Teile; eigenes Symbol, eigener Name, eigene Kopplung. Wie `Hlks.Quest` zwei Projektdateien (PC-Simulator und Android), nicht in `HLKS_CAD.slnx`. | neu |
| `src/Hlks.Quest` | KICAD-Brillen-App. Verliert die Labor-Teile (siehe «Was aus KICAD entfernt wird»). | bestehend |
| `src/Hlks.Xr` | Reine Logik ohne StereoKit, von beiden Apps genutzt. `Labor/` (Katalog, Prüfaufgaben, Wandpaar), `Netz/LaborKatalogLader.cs`, `Netz/ProtokollAblage.cs` bleiben hier, werden aber nur noch von der Labor-App aufgerufen. `Gesten/` bekommt die Kamera-Quelle (L4). Neu `Bedienung/Bedienstandard.cs` (Lesen und Prüfen des Standards, für beide Apps und KICAD). | bestehend |
| `src/Hlks.Web` | KICAD-Server. Verliert die Labor-Endpunkte, liest den Bedienstandard und gibt ihn der Brille und der Oberfläche (Modul G). Die Transport-Teile bleiben hier und werden für `Hlks.Labor` aufrufbar gemacht (Abbildung der Endpunkte als Methoden statt nur in `Program.cs`). | bestehend |
| `tests/Hlks.Labor.Tests` | xUnit für `Hlks.Labor`; die Labor-Tests aus `tests/Hlks.Web.Tests/XrLaborTests.cs` ziehen hierher. | neu |

### Trennung der Brillen-Apps aus gemeinsamen Quellen

Heute ist das Labor kein eigener Teil der App, sondern ein Kanal quer durch sie: Etwa zwölf Stellen in `QuestApp*.cs` melden Ereignisse
an `PruefAufgaben` (Teleport, Etage, Greifen, Messen, Tafel), und `LaborWerte` verteilt alle 98 Schlüssel. Deshalb:

- **Einhängepunkte als `partial void`-Methoden** in `QuestApp` (z. B. `partial void LaborBild(float dt)`, `partial void
  LaborTeleportiert(...)`, `partial void LaborEtageGewechselt(...)`). In der KICAD-App gibt es keine Umsetzung; der Übersetzer entfernt
  die Aufrufe ganz. In der Labor-App setzen die Labor-Dateien sie um. So bleibt eine Quelle für die Bedienung, und KICAD enthält keinen
  Labor-Code.
- **Labor-Dateien wandern** von `src/Hlks.Quest` nach `src/Hlks.Labor.Quest`: `QuestApp.Labor.cs` (ohne Röntgen-Hand und flache Hand,
  die zur normalen Bedienung gehören und als `QuestApp.Roentgen.cs` bzw. `QuestApp.Schnitt.cs` in KICAD bleiben), `AufgabenKarte.cs`,
  der Labor-Teil von `QuestApp.PruefenV1a.cs` (Prüflauf «messen-wand-wand»), die Stick-Variante (`StickSchritt`).
- **Varianten:** Beide Optionen jeder Variante bleiben in den gemeinsamen Quellen; welche gilt, bestimmt in KICAD der Standard, im Labor
  die Live-Wahl. Eine Option, die das Labor endgültig verworfen hat, kann später aus den gemeinsamen Quellen entfernt werden (Aufräumen,
  nicht Teil dieser Stufen).
- **Werte in der App:** `LaborWerte` heisst in KICAD sinngemäss nur noch «Bedienwerte» mit zwei Schichten: Katalog-Vorgabe → Standard.
  Die Labor-App hat die dritte Schicht «live» (Stellschrauben und Varianten vom Labor-PC). Der Name der Klasse bleibt, wenn das Umbenennen
  mehr Konflikte als Nutzen bringt (Entscheid im Bau).

### Testwelt der Labor-App

Die Labor-App braucht für Begehen, Etagen, Messen und Sitzungstisch ein Modell, aber kein offenes KICAD-Projekt (Entscheid «Ort»).

- **Vorschlag (offene Frage 1):** Der Labor-PC baut die GLB selbst mit `Hlks.Kern` (`GlbSchreiber`, wie `XrModellEndpunkte`) aus einem
  Projekt, das er **nur liest**:
  - Vorgabe: das fiktive Beispielprojekt «MFH Musterweg» aus dem Repository, ergänzt um einen neutralen **Testraum** (leerer Raum mit
    Markierungen, Wänden im bekannten Abstand für «messen-wand-wand», zwei Etagen).
  - Wahlweise eine lokale Kopie von Test_praxis (`%USERPROFILE%\HLKS_CAD_testdaten`), nie im Repository.
- Der Labor-PC schreibt nie in ein Projekt. Marker aus der Labor-Brille bleiben im Labor (Messprotokoll), sie gehen nicht an KICAD.

### Ports und Prozesse

| Teil | Port | Hinweis |
| --- | --- | --- |
| Labor-Seite am PC | 5430, HTTP nur localhost | frei laut Suche im Repository 26.10.2026 (Annahme) |
| Netzzugang Labor-Brille | 5340 TCP (HTTPS), 5341 UDP (Suche) | eigene Kopplung je Brille; dasselbe Zertifikat je PC wie KICAD (`%LOCALAPPDATA%\HLKS_CAD\xr`); `werkzeuge/xr_firewall.ps1` bekommt die zwei Ports dazu (Christian führt es als Administrator aus) |
| KICAD | 5130 / 5140 / 5141 | unverändert |

- KICAD und Labor laufen gleichzeitig. In der Brille läuft immer nur eine der beiden Apps.
- **Desktop-Icon «KICAD Labor»** (Entscheid oben): neues Skript `werkzeuge/labor_starten.ps1` nach dem Muster von `kicad_starten.ps1`:
  - startet den letzten **geprüften** Stand aus derselben Arbeitskopie `%USERPROFILE%\HLKS_CAD_desktop` (Zweig `geprueft`), gebaut in
    einen eigenen Ordner `%USERPROFILE%\HLKS_CAD_build\labor`, damit ein Neubau von KICAD das laufende Labor nicht trifft und umgekehrt;
  - läuft das Labor schon mit dem neuesten geprüften Stand, öffnet es nur das Fenster; sonst bauen (falls nötig), starten, Fenster;
    schlägt der Bau fehl, zurück auf den vorigen Stand – wie bei KICAD;
  - eigenes Fenster in Edge (App-Fenster wie KICAD), eigenes Symbol `werkzeuge/labor.ico` (Kolben bzw. Hand mit Gestenbahn im Stil des
    KICAD-Symbols; Entwurf an Christian per Bild), Protokoll `%USERPROFILE%\HLKS_CAD_build\labor\start.log`;
  - zieht die Arbeitskopie nur nach, wenn weder KICAD noch Labor gerade arbeiten (sonst Hinweis wie bei KICAD).
  - Die Verknüpfung auf dem Desktop legt der Hauptlauf an (`powershell -WindowStyle Hidden -File …\labor_starten.ps1`, Symbol
    `labor.ico`), wie bei «KICAD».
- «Labor-App starten» in KICAD ruft dasselbe Skript auf (losgelöst, damit das Labor KICAD überdauert).
- `stand_pruefen.ps1` baut und prüft `Hlks.Labor` mit, damit «geprueft» auch für das Labor gilt.
- Die Labor-APK baut `werkzeuge/quest_app_bereitstellen.ps1` mit einem Schalter `-Labor` in eine eigene Ablage
  (`%USERPROFILE%\HLKS_CAD_werkzeuge\labor_app`).

## Gemeinsames Gestenformat VR/PC

### Bedeutungen statt Befehle

Neuer Katalog `kataloge/bedienung_bedeutungen.json` (Art `hlks-bedienung-bedeutungen`): die Bedeutungen, für die es Standardgesten gibt.
Jede mit Id, Name, Beschreibung, Gerät (`brille`, `pc`, beide) und der festen Regelgeste, die sie heute auslöst.

| Id (Vorschlag) | Bedeutung | Brille heute | PC (Modul G) |
| --- | --- | --- | --- |
| `apps.einblenden` | Apps bzw. Symbole holen | Hebegeste | – |
| `ausblenden` | Zuletzt Geöffnetes wegschieben | Wegwischen rechts → links | – |
| `blaettern.vor` / `.zurueck` | Seite bzw. Reiter vor/zurück | Wisch auf der Tafel | G2 |
| `rollen.hoch` / `.runter` | in Liste/Fenster rollen | Wisch hoch/runter auf der Tafel | G3 |
| `ausloesen` | wählen, springen | Pistolengeste | – |
| `zoom.alles` | Ansicht zurücksetzen | – | G6 |
| `drehen`, `rollen`, `zoomen`, `verschieben` | fliessend navigieren | Griffpunkte am Tisch | G4, G5, G7 |
| `bildschirm.einblenden` / `.ausblenden` | PC-Bildschirm holen/wegschieben | Wisch links → rechts | – |

Die Liste ist ein Vorschlag aus den Brillentests vom 24.–25.10.2026 und Modul G; das Labor ergänzt sie. Das Feld `funktion` von
`EigeneGeste` (heute freier Text) erhält diese Ids.

### Datei der Gesten (`hlks-gesten`, Fassung 2)

Erweiterung des bestehenden Formats `hlks-xr-gesten` Fassung 1 (`GestenJson.cs`); Fassung 1 wird weiter gelesen.

```
{ art: "hlks-gesten", fassung: 2, stand, quelle: "brille" | "kamera",
  gesten: [{ id, name, funktion: <Id der Bedeutung>, haendigkeit, aktiv, schwelle,
             beispiele: [{ bilder: [ Bild ] }] }] }
Bild (brille) = { t, kopf: [x,y,z], gieren, links: Hand|null, rechts: Hand|null }        wie Fassung 1
Bild (kamera) = { t, links: KHand|null, rechts: KHand|null }
KHand = { bild: [42 Zahlen, 21 Punkte × x,y im Bild 0…1, gespiegelt wie Modul G],
          welt: [63 Zahlen, 21 Punkte × x,y,z in m um die Handmitte, MediaPipe «world landmarks»] }
```

- **Eine Datei je Quelle:** `gesten_brille.json` und `gesten_kamera.json`. Verglichen und auf Konflikte geprüft wird nur innerhalb
  einer Quelle.
- **Ein Erkenner:** `Hlks.Xr/Gesten` bekommt eine Abbildung der 21 MediaPipe-Punkte auf `HandBild` (Handgelenk, Daumen 4 Glieder,
  Finger je Grund-, Mittel-, End- und Spitzengelenk; die Mittelhandknochen der vier Finger fehlen und werden aus Handgelenk und
  Grundgelenk ergänzt bzw. bei der Krümmung weggelassen – im Bau zu prüfen, siehe `modul-gesten.md` Stufe 3). Der Körperbezug ist bei
  der Kamera fest (Blick in die Kamera, kein Kopf); die Lage kommt aus `bild`, die Grösse aus der scheinbaren Handgrösse.
- **Gleiche Namen der Stellschrauben:** Brille (`XrWischLogik`, `xr_stellschrauben.json`) und PC (`gesten-logik.js`,
  `STELLSCHRAUBEN_VORGABE`) behalten ihre Namen; der Standard führt sie getrennt nach Gerät.

## Datenfluss Labor → KICAD

### Bedienstandard

Ablage je Benutzer über OneDrive, getrennt von `BenutzerEinstellungen.cs` (deren Werte sind auf 4000 Zeichen begrenzt; eine Gestendatei
hat rund 1,6 Mio. Zeichen):

```
%OneDrive%\HLKS-CAD\bedienung\
  standard.json            gilt für KICAD (Art «hlks-bedienstandard»)
  gesten_brille.json       Standardgesten Brille (hlks-gesten, quelle brille)
  gesten_kamera.json       Standardgesten PC (hlks-gesten, quelle kamera, ab L4)
  verlauf\standard_<JJJJMMTT-hhmm>.json   jede festgelegte Fassung, für «zurück auf Fassung …»
Ohne OneDrive: %APPDATA%\HLKS-CAD\bedienung\ ; Umleitung für Tests: HLKS_BEDIENUNG (nur unter %TEMP%, wie HLKS_BENUTZER)
```

```
standard.json = { art: "hlks-bedienstandard", fassung: 1, stand, festgelegt: { zeit, rechner, labor_commit, notiz },
  brille: { stellschrauben: { "<schluessel>": Zahl }, varianten: { "<name>": "<option>" }, gesten: "gesten_brille.json" | null },
  pc:     { stellschrauben: { "<name>": Zahl }, gesten: "gesten_kamera.json" | null },
  bedeutungen: [{ id, brille: { art: "regel"|"eigen", geste? }, pc: { art, geste? }, messwerte: {…}, entscheid: { datum, text } }] }
```

- **Nur das Labor schreibt**, und nur auf «Als Standard festlegen» (offene Frage 3). Es schreibt über eine temporäre Datei und legt die
  Fassung zugleich im Verlauf ab. Fehlende Schlüssel gelten als Katalog-Vorgabe; unbekannte werden mit Hinweis übergangen (wie
  `GestenJson`).
- **KICAD liest** beim Start und wenn sich die Datei ändert (Abfrage alle 30 s über Zeitstempel und Grösse, kein Dateiwächter auf
  OneDrive – Befund R 19.10.2026 in `Program.cs`):
  - **Brille:** Der bestehende Endpunkt `GET /api/xr/labor/katalog` wird zu `GET /api/xr/bedienung`: Katalog-Vorgaben, überlagert vom
    Standard, dazu die Gestendatei der Brille. Die KICAD-Brillen-App holt ihn wie heute beim Verbinden und alle 30 s (ETag/304).
    Ist eine eigene Geste für eine Bedeutung festgelegt, führt die KICAD-App sie mit `GestenLiveErkenner` aus; sonst die Regelgeste.
  - **PC (Modul G):** Die Oberfläche holt `GET /api/bedienung/pc` und setzt die Stellschrauben über `STELLSCHRAUBEN_VORGABE`. Reihenfolge:
    Vorgabe im Code → Standard → persönliche Abweichung im Dialog «Gesten» (offene Frage 2).
- **Konfliktkopien von OneDrive** (`standard-<Rechner>.json`): KICAD nimmt die Datei mit dem neueren `festgelegt.zeit` und meldet die
  Kopie in der Statusleiste; das Labor bietet das Zusammenführen an.

### Labor-Daten

Ebenfalls je Benutzer über OneDrive, damit der Verlauf auf jedem Rechner gleich ist (Annahme; heute je PC in `%LOCALAPPDATA%`):

```
%OneDrive%\HLKS-CAD\labor\
  messungen\<JJJJMMTT-hhmmss>_<rechner>_<teilnehmer>.jsonl   Messprotokolle (heute xr\messprotokolle)
  fragebogen\<JJJJMMTT-hhmmss>_<rechner>_<name>.json         (heute xr\fragebogen)
  profile\<kennung>.json                                     (heute Benutzereinstellung labor.profil.*)
  entwuerfe\gesten_brille.json, gesten_kamera.json          Gesten in Arbeit, noch nicht Standard
```

Dateinamen mit Rechnername, damit zwei Rechner nie dieselbe Datei schreiben. Beim ersten Start übernimmt das Labor einmalig die
bestehenden Messprotokolle, Fragebogen und Profile (offene Frage 4) und schreibt aus den heutigen Vorgaben der Kataloge den **Standard
Fassung 1**, damit KICAD nach dem Umbau genau so bedient wird wie vorher.

## Was aus KICAD entfernt wird

| Teil | Heute | Danach |
| --- | --- | --- |
| Register XR › Gruppe «Labor» | `ansicht.xrLabor` (XL) öffnet Dialog «XR-Labor» | gleiche Id und Kürzel, Text «Labor-App starten», startet `Hlks.Labor` und öffnet die Labor-Seite |
| Dialog «XR-Labor» | `xr-labor-dialog.js`, `xr-labor-text.js`, `xr-labor.test.mjs`, `xr-labor.browser.mjs`, CSS `xl-…` in `quest.css` | zieht als Labor-Seite nach `src/Hlks.Labor/wwwroot` |
| Labor-Endpunkte | `XrLaborEndpunkte.cs` (Katalog, Auswertung, Fragebogen, Variante, Aufgabe), `XrLaborKatalog.cs`, `XrLaborAuswertung.cs`, `XrFragebogen.cs` | nach `src/Hlks.Labor`; in KICAD bleibt nur `GET /api/xr/bedienung` (lesend) |
| Live-Stellschrauben | `POST /api/xr/stellschraube`, Kanal-Nachricht `stellschraube` von KICAD | in KICAD entfernt; KICAD sendet im `willkommen` nur den Standard, bei geändertem Standard ein neues `willkommen.stellschrauben` |
| Messprotokoll | Kanal-Nachricht `messprotokoll`, Ablage `xr\messprotokolle` | nur noch im Labor |
| Profile | Benutzereinstellungen `labor.profil.*`, `labor.profil_aktiv` | im Labor (`labor\profile`), aus `BenutzerEinstellungen.Bekannt` entfernt |
| Kataloge | `xr_pruefaufgaben.json`, `xr_fragebogen.json` | nur noch vom Labor gelesen (bleiben in `kataloge/`) |
| Brillen-App | `QuestApp.Labor.cs`, `AufgabenKarte.cs`, Prüfaufgaben-Haken, Stick-Variante, Messprotokoll, `ProtokollAblage` | nach `src/Hlks.Labor.Quest`; Röntgen-Hand und flache Hand bleiben in KICAD |
| Tests | `XrLaborTests.cs`, Labor-Teile in `XrKanalTests.cs`, `XrZugangTests.cs` | nach `tests/Hlks.Labor.Tests`; `GestenGenerator` bleibt in `tests/Hlks.Xr.Tests` (auch von V1e/V1f/V1h genutzt) |
| Doku | `OBERFLAECHE.md` und `inventar.json` (Kapitel XR-Labor) | Kapitel «Labor-App» in derselben Anleitung `/hilfe/`; die Labor-Seite öffnet sie mit «?»/F1 |

Bleibt in KICAD unverändert: Register XR sonst, «Quest verbinden», Installation der KICAD-App, Netzzugang, Kopplung, Kanal mit Posen,
Aufträgen und Markern, Desktop in der Brille, alle Bedienung in der Brille.

## Stufen

| Stufe | Inhalt | Hängt ab von |
| --- | --- | --- |
| **L0 Gerüst und Durchstich** | `src/Hlks.Labor` startet auf 5430 mit leerer Seite, ruft die Transport-Endpunkte aus `Hlks.Web` auf (Abbildung als Methoden); `src/Hlks.Labor.Quest` baut als PC-Simulator und APK `ch.hkplanung.kicad.labor` aus den gemeinsamen Quellen; `partial void`-Einhängepunkte in `QuestApp` ohne Verhaltensänderung; Labor-Brille koppelt und verbindet sich mit 5340 (Kabel); Desktop-Icon «KICAD Labor» mit `labor_starten.ps1`. Nachweis: beide APKs gleichzeitig installiert, KICAD-App unverändert (Prüfbilder V1i gleich). | Abstimmung mit Sitzung Quest App (Dateien `src/Hlks.Quest`) |
| **L1 Umzug VR-Labor** | Labor-Endpunkte, Katalog, Auswertung, Fragebogen, Labor-Seite (aus dem Dialog), Testwelt (MFH Musterweg + Testraum), Labor-Teile der Brille in die Labor-APK, Labor-Daten in OneDrive, Übernahme der bisherigen Messprotokolle. KICAD behält in dieser Stufe noch alles (paralleler Betrieb). | L0 |
| **L2 Bedienstandard** | Format `hlks-bedienstandard` mit Prüfung (`Hlks.Xr/Bedienung/Bedienstandard.cs`), Register «Standard» im Labor (Vergleich Standard ↔ Entwurf, festlegen, Verlauf, zurück, Leitfaden als Seite/PDF), Standard Fassung 1 aus den heutigen Vorgaben; KICAD liest den Standard (`/api/xr/bedienung`, `/api/bedienung/pc`); **dann** Entfernen des Labors aus KICAD (Tabelle oben) und Knopf «Labor-App starten». | L1 |
| **L3 Gestenlabor Brille** (V1b-2) | Labor-App «Gesten» in der Brille (wählen, vorzeigen mit Countdown, Güte, Konflikte gegen Regel- und eigene Gesten, live testen, übernehmen), Register «Gesten» auf der Labor-Seite (Liste, löschen, umbenennen, aktivieren, aus-/einlesen), Katalog der Bedeutungen, Format Fassung 2; KICAD-Brillen-App führt eigene Standardgesten aus. | L2 |
| **L4 PC-Gesten-Labor** | Labor-Seite «Gesten am PC» mit `js/gesten/gesten.js` aus Modul G (Testmodus, Aufnahme nur Punkte), geführte Messung von G2–G7 (Erkennungsrate, Fehlauslösungen, Bildrate), Stellschrauben live, eigene PC-Gesten vorzeigen und gegen G2–G7 prüfen (Abbildung 21 → `HandBild` in `Hlks.Xr/Gesten`, Auswertung im Labor-Server – nur Punkte), festlegen in `standard.json` › `pc`. | **Modul G Stufe 1 in `main`**, L2 |
| *später* | KICAD löst eigene PC-Gesten aus = Modul G Stufe 3 (Live-Erkenner im Browser: Übertragung der DTW-Erkennung nach JS mit gemeinsamen Prüffolgen, damit C# und JS gleich entscheiden). Gesten ausserhalb von KICAD (Windows) bleiben ausgeschlossen (Entscheid «PC-Gesten»). | Modul G Stufe 3 |

Zwischen den Stufen ein Brillentest mit Christian (Prüfbilder an jedem sichtbaren Zwischenstand, `feedback_zwischenbilder`).

## Zuteilung (nach Tabelle in `../CLAUDE.md`)

Bauagenten mit `isolation: "worktree"`, Zuteilung in `zuteilung.md`, Tests nur gefiltert auf das Geänderte.

| Teil | Wer |
| --- | --- |
| Spezifikation, Abstimmung mit den Sitzungen Quest App und Modul G, Zusammenführen auf dem eigenen Zweig, Abnahme mit Christian | Hauptlauf (diese Sitzung) |
| L0: `partial void`-Einhängepunkte in `QuestApp`, zweites App-Projekt aus gemeinsamen Quellen, Durchstich Android; Transport aus `Hlks.Web` als aufrufbare Abbildung | `hlks-kern` |
| L0: `Hlks.Labor`-Gerüst (Program.cs, Port, verlinkte Oberflächendateien), Bauskripte (`labor_starten.ps1` mit Symbol `labor.ico`, `stand_pruefen.ps1`, `quest_app_bereitstellen.ps1 -Labor`), Firewall-Ports | `hlks-modul` |
| L0: Verknüpfung «KICAD Labor» auf dem Desktop anlegen, erster Start mit Christian | Hauptlauf |
| L1: Umzug der Labor-Teile der Brille (QuestApp.Labor.cs, AufgabenKarte, Haken), Testwelt-GLB | `hlks-kern` |
| L1: Umzug Endpunkte, Labor-Seite aus dem Dialog, Ablage OneDrive, Übernahme alter Messungen | `hlks-modul` |
| L2: Bedienstandard (Format, Prüfung, Lesen in KICAD, Schreiben im Labor), Entfernen aus KICAD, Knopf, Leitfaden | `hlks-modul` |
| L3: Gestenlabor in der Brille, Ausführen eigener Gesten in der KICAD-App | `hlks-kern` |
| L3: Register «Gesten» auf der Labor-Seite | `hlks-modul` |
| L4: Abbildung MediaPipe → `HandBild`, Merkmale für die Kamera | `hlks-kern` |
| L4: Seite «Gesten am PC», geführte Messung | `hlks-modul` |
| Testläufe und Testbericht je Stufe | `hlks-test` |
| Prüfung des Datenflusses (Standard überschreibt nie unbemerkt, Konfliktkopien, kein Bild verlässt den Browser) vor L2-Abschluss | `hlks-pruefer` |

Richtwert: L0 1 Tag, L1 2 Tage, L2 2 Tage, L3 2–3 Tage, L4 2 Tage.

## Abstimmung mit anderen Sitzungen

- **Sitzung «Modul XR – Quest App»** (`claude/xr-quest-app-7c693c`, Dateien `src/Hlks.Quest`, `src/Hlks.Xr`, `kataloge/xr_*`): L0 und L1
  greifen tief in `QuestApp*.cs`. Vor L0 abklären, ob dort gerade gebaut wird; während L0/L1 ändert nur eine Sitzung `src/Hlks.Quest`.
- **Sitzung «Modul G»** (`claude/modul-gesten`, Dateien `js/gesten/`, `GestenEinstellung.cs`, `BenutzerEinstellungen.cs`): L2 fügt das
  Lesen des Standards für die PC-Stellschrauben in die Gesten-Oberfläche ein, L4 nutzt `gesten.js`. Beides erst, wenn Modul G Stufe 1 in
  `main` ist; bis dahin keine Änderung an `js/gesten/`.

## Offene Fragen an Christian

1. **Testwelt der Labor-App:** MFH Musterweg plus neutraler Testraum (Vorschlag), wahlweise eine lokale Kopie von Test_praxis?
2. **Empfindlichkeit der PC-Gesten:** Legt das Labor sie fest und KICAD (Dialog «Gesten» von Modul G) erlaubt nur eine persönliche
   Feinanpassung darüber (Vorschlag)? Oder nur noch im Labor?
3. **Wann ein Standard gilt:** nur auf ausdrückliches «Als Standard festlegen» mit Verlauf und «zurück» (Vorschlag), oder jeder im
   Labor gespeicherte Wert sofort?
4. **Bisherige Messungen:** Messprotokolle, Fragebogen und Profile der Brillentests übernehmen (Vorschlag) oder neu beginnen?
5. **Name der Brillen-App:** «KICAD Labor» (Vorschlag) oder ein Name ohne KICAD, weil das Labor nicht nur für KICAD gilt?
