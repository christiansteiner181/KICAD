# Hlks.Labor.Quest – Brillen-App «XR-Labor» (XR-Labor, Stufe L0)

Spezifikation: `entwurf/labor-app.md` (freigegeben von Christian am 26.10.2026), besonders «Aufbau › Projekte» und «Trennung der Brillen-Apps aus gemeinsamen
Quellen». Die Brillen-App des XR-Labors entsteht aus **denselben Quellen** wie die KICAD-Brillen-App (`src/Hlks.Quest`). Sie hat eigene Kennung, eigenen Namen,
eigenes Symbol und eine eigene Kopplung. Beide Apps können gleichzeitig auf der Quest installiert sein; es läuft immer nur eine.

| | KICAD | XR-Labor |
| --- | --- | --- |
| Projekt (PC, Simulator) | `src/Hlks.Quest/Hlks.Quest.csproj` | `src/Hlks.Labor.Quest/Hlks.Labor.Quest.csproj` |
| Projekt (Brille) | `src/Hlks.Quest/Projects/Android/Hlks.Quest.Android.csproj` | `src/Hlks.Labor.Quest/Projects/Android/Hlks.Labor.Quest.Android.csproj` |
| App-Kennung | `ch.hkplanung.kicad.quest` | `ch.hkplanung.xrlabor` |
| Name | «KICAD» | «XR-Labor» |
| Marke (`hallo.marke`) | `kicad` | `xrlabor` |
| Eingang der Kopplung | `/sdcard/Android/data/ch.hkplanung.kicad.quest/files/kopplung.json` | `/sdcard/Android/data/ch.hkplanung.xrlabor/files/kopplung.json` |
| Ablage am PC (Simulator) | `%LOCALAPPDATA%\HLKS_CAD\quest_app` | `%LOCALAPPDATA%\HLKS_CAD\xrlabor_app` |
| Vorsatz im Protokoll | `KICAD Quest V1a:` | `XR-Labor L0:` |
| Gekoppelt mit | KICAD (Netzzugang 5140, Suche 5141) | Labor-PC `src/Hlks.Labor` (5340, 5341) |

Ports und Suchport stehen nirgends fest in der App. Sie kommen aus dem Kopplungspaket (`Kopplungspaket.Port`, `.SuchPort`) des PCs, der die App koppelt; der Test
`AppMarkeTests.Ports_stehen_nicht_fest_in_den_Apps` hält das fest.

## Aufbau

- **Gemeinsame Quellen:** Beide Projektdateien übersetzen alle `*.cs` von `src/Hlks.Quest` ohne deren `Marke.Kicad.cs`; Android-Teile (`*.Android.cs`,
  `Platforms/Android`) nur in der Brillen-Fassung, PC-Teile (`*.Net.cs`) nur am PC – wie bei `src/Hlks.Quest`. Ebenso von dort: `Assets` (Shader, Symbole),
  das Android-Manifest, `Fassung.targets` (Fassung und Commit in `hallo.app`) und aus `kataloge/` die `xr_farben.json`.
- **Eigene Dateien (L0):**
  - `Marke.XrLabor.cs` – wählt die Marke `Hlks.Xr.Netz.AppMarke.XrLabor` (Name, Kennung, `hallo.marke`, Texte zur Gegenstelle, Ablage am PC).
  - `Platforms/Android/Resources` – Name («XR-Labor», `values/strings.xml`) und App-Symbol (adaptiv: `mipmap-*/appicon_background.png`, `appicon_foreground.png`).
  - `Platforms/Net/App.ico` – Symbol der PC-Fassung.
  - `Platforms/symbol_erzeugen.mjs` – legt beide Symbole ab (siehe «Symbol»).
- **Marke:** Wo die App bis V1i fest «KICAD» voraussetzte, fragt sie `Marke` (`src/Hlks.Quest/Marke.cs`): Name in Texten (Fenster, App «Status», StereoKit),
  «Suche … auf …», «… neu koppeln», «Nicht gekoppelt – …», Vorsatz der Protokollzeilen, Ablage am PC, `hallo.marke`. Die KICAD-App zeigt dieselben Texte wie vorher.
- **In L0 und L1 noch in `src/Hlks.Quest`:** die Labor-Teile `QuestApp.Labor.cs`, `QuestApp.LaborPruefen.cs` (Labor-Teil des Prüflaufs) und `AufgabenKarte.cs`. Sie
  gehören bis L2 zu beiden Apps (keine Verhaltensänderung der KICAD-App) und ziehen mit L2 hierher (Zuschnitt L1 in `entwurf/labor-app.md`: ohne sie lädt die
  KICAD-App keinen Katalog und kein Profil, bis `GET /api/xr/bedienung` kommt). Sie setzen die Einhängepunkte selbst um.

## Einhängepunkte (`src/Hlks.Quest/QuestApp.Einhaengepunkte.cs`)

Der allgemeine Code der App ruft das Labor nur noch über `partial void`-Methoden auf. Fehlt die Umsetzung, entfernt der Übersetzer die Aufrufe samt Argumenten.
Werte, die der allgemeine Code braucht, gehen als `ref` mit einer Vorgabe des Aufrufers (die Vorgabe gilt ohne Labor). Umgesetzt sind alle in
`QuestApp.Labor.cs` bzw. `QuestApp.LaborPruefen.cs`.

| Einhängepunkt | Aufgerufen in | Ohne Labor | Labor (L0) |
| --- | --- | --- | --- |
| `LaborAnlegen()` | Konstruktor, nach `WegwischZiele.Add(apps)` | – | Karte anlegen, als Ziel des Wegwischens einhängen (zwischen App und Symbolen) |
| `LaborStarten()` | Konstruktor, vor `EinstellungenAnwenden` | – | Katalog aus der Ablage, wartende Messprotokolle |
| `LaborStellschraube(s, wert, ref ergebnis)` | `StellschraubeAnwenden` | `null` → übliche Stellschraube | `labor.aufgabe` → `AufgabeVomPc` |
| `LaborVerbunden(quelle, ref warten)` | `Verbinden`, vor dem Modell | `null` | Katalog vom PC (wird abgewartet) |
| `LaborNachladen(ref warten)` | `Nachladen` (alle 30 s) | `null` | Katalog vom PC (ETag) |
| `LaborWillkommen()` | Kanal «willkommen» | – | wartende Messprotokolle nachsenden |
| `LaborNachricht(typ, inhalt)` | Kanal, jede Nachricht | – | Quittung `bezug=messprotokoll` zählen |
| `LaborBild(kopf)` | `Bild`, nach Messen und Marker | – | `AufgabeBild` (Karte, Messung, Markierung) |
| `LaborStick(kopf, kopfRef)` | `Haende` (Begehen) | – | `StickSchritt` (Variante «teleport» = «stick») |
| `LaborImTablet()` | `Box` (Variante «tablet») | – | `AufgabeImTablet` |
| `LaborHandAnKarte(ref nah)` → `HandAnKarte` | `Haende`, `RoentgenSchritt`, `HandFang`, `MessenBild`, `MarkerBild` | `false` | `karte.HandNah` |
| `LaborFlaechen(liste)` | `AppsBild`, `HinweisHindernisse`, `BedienFlaechen` | – | Karte als belegte Fläche |
| `LaborAufgabeOffen(ref offen)` | `WischAnwenden` | `false` | Aufgabe läuft |
| `LaborRaumZumZeigen(ref raum)` → `RaumZumZeigen()` | App «Tisch», Box, `TischAktion` | `null` → Raum am Stand, Raum des Auftrags | Raum der Aufgabe «raum-ar-oeffnen» |
| `LaborSprung(ziel)` | `SprungAusfuehren` | – | `PruefAufgaben.Sprung` |
| `LaborSprungAbgebrochen()` | `BegehenEreignisAnwenden` | – | `PruefAufgaben.SprungAbgebrochen` |
| `LaborDrehschritt(vorher, nachher)` | `BegehenEreignisAnwenden` | – | `PruefAufgaben.Drehschritt` |
| `LaborEtageGewechselt(geschoss)` | `EtageBegehen` | – | `PruefAufgaben.EtageGewechselt` |
| `LaborWischOhneWirkung()` | `EtageWechseln`, `WischAnwenden` | – | `PruefAufgaben.WischOhneWirkung` |
| `LaborRaumRueckschritt(grund)` | `SitzungEtage`, `TischAktion` | – | `PruefAufgaben.RaumRueckschritt` |
| `LaborGemessen(messung)` | `MessEreignis` | – | `PruefAufgaben.Gemessen` |
| `LaborMessenDaneben(grund)` | `MessEreignis` | – | `PruefAufgaben.MessenDaneben` |
| `LaborDaneben()` | `AppEreignis` | – | `PruefAufgaben.Daneben` |
| `LaborKennzahlen(ref text)` | Statuszeile «P4 Status» | `""` | `Katalog=…, Aufgabe=…, Messprotokolle=… ` |
| `LaborQuittungen(ref text)` | Zeile «P4 ok/Fehler» | `""` | `, Quittungen=n` |
| `LaborPruefRegeln(teile)` | `PruefP5bRegeln` (`QuestApp.PruefenP5b.cs`) | – | Katalog, Grenzen, Vorgaben, Prüfaufgaben (P5b) |
| `LaborPruefBilder(ordner)` | `PruefBilderP5b` | – | Prüfbild der Karte |
| `LaborPruefbildVorbereiten()` | `PruefbildVorbereiten` | – | fertige Aufgabe abschliessen und wegräumen |

Röntgen-Hand (`QuestApp.Roentgen.cs`) und flache Hand (`QuestApp.Schnitt.cs`) gehören zur normalen Bedienung und bleiben in der KICAD-App; ebenso der Teil
«Fassung, Röntgen bzw. Flache Hand» der Prüfzeile P5b (`QuestApp.PruefenP5b.cs`). `LaborWerte` (`werte`) bleibt im allgemeinen Code: in KICAD sind es nach L2 die
Bedienwerte (Katalog-Vorgabe → Standard); über das Umbenennen entscheidet der Hauptlauf.

**Probebau ohne Labor** (so übersetzt die KICAD-App nach dem Umzug in L1):

```
$env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build\l0-ohnelabor"
dotnet build src\Hlks.Quest\Hlks.Quest.csproj -p:HlksOhneLabor=true
```

`HlksOhneLabor` lässt die drei Labor-Dateien weg (`Hlks.Quest.csproj`). Ergebnis 26.10.2026: 0 Fehler, 0 Warnungen; Simulator «P4 ok». Der Test
`AppMarkeTests.Der_allgemeine_Code_ruft_das_Labor_nur_ueber_Einhaengepunkte` hält die Grenze auch ohne diesen Bau fest.

## Bau

Bauausgabe wie alle Projekte unter `HLKS_BUILD` bzw. `%USERPROFILE%\HLKS_CAD_build`. Ausführen mit `"C:\Program Files\dotnet\dotnet.exe"` (bei Christian zeigt
`DOTNET_ROOT` auf ein .NET 8, `entwurf/modul-xr.md`, «Stand P0»).

- PC: `dotnet build src\Hlks.Labor.Quest\Hlks.Labor.Quest.csproj` → `<bau>\Hlks.Labor.Quest\bin\Debug\net10.0\Hlks.Labor.Quest.dll`
- Brille: `dotnet publish src\Hlks.Labor.Quest\Projects\Android\Hlks.Labor.Quest.Android.csproj -c Release -f net10.0-android`
  → `<bau>\Hlks.Labor.Quest.Android\bin\Release\net10.0-android\publish\ch.hkplanung.xrlabor-Signed.apk` (Testschlüssel, nur für die eigene Brille).
  Voraussetzung wie bei `src/Hlks.Quest`: .NET-Workload «android», Android SDK und JDK 17 unter `%USERPROFILE%\HLKS_CAD_werkzeuge`.
- Beide Projekte bewusst nicht in `HLKS_CAD.slnx` (Android-Workload; wie `Hlks.Quest`).

## Simulator

```
"C:\Program Files\dotnet\dotnet.exe" <bau>\Hlks.Labor.Quest\bin\Debug\net10.0\Hlks.Labor.Quest.dll --pruefen 120
```

Gleiche Argumente wie die KICAD-App (`src/Hlks.Quest/Optionen.cs`): `--simulator`, `--pruefen <bilder>`, `--kopplung <datei>`, `--speicher <ordner>` usw. Ohne
`--kopplung` endet die Prüfung mit «P4 ok: ohne Modell, …». Mit einem Kopplungspaket des Labor-PCs (Ports 5340/5341) verbindet sie sich mit dem Labor;
`hallo.marke` ist `xrlabor`.

## Testraum (L1)

Entscheid Christian 26.10.2026: **«Nur neutraler Testraum»** – kein Projekt, keine Projektdaten (`entwurf/labor-app.md`, «Testwelt der Labor-App», «Zuschnitt L1»). Die
Labor-App löst darin alle Prüfaufgaben aus `kataloge/xr_pruefaufgaben.json`.

**Katalog** `kataloge/labor_testraum.json` (Art `hlks-labor-testraum`, Fassung 1; Aufbau im Kopf von `src/Hlks.Labor/TestraumKatalog.cs`), KICAD-mm absolut:

| Teil | Inhalt | Herkunft |
| --- | --- | --- |
| Etagen | EG Kote 0, OG Kote 2900, Geschosshöhe je 2900 mm | wie Beispielprojekt «MFH Musterweg» (`Beispielprojekt.cs`, EG und 1.OG; Test prüft es) |
| Raum je Etage | innen 8.0 × 6.0 m (x 0 … 8000, y 0 … 6000), lichte Höhe 2650 mm | **Annahme L1** (Vorschlag Auftrag «ca. 8 × 6 m») |
| Wände | 200 mm, Achse wie die Wandgeometrie «linie» von KICAD, von OK Boden bis UK Decke | **Annahme L1** |
| Boden, Decke | Platten 250 mm; die Platte zwischen EG und OG steht zweimal (Decke EG, Boden OG), gleiche Lage und Farbe | **Annahme L1**; doppelt, weil die App beim Begehen nur die Etage am Stand zeichnet |
| Markierungen | EG vier (2.5 m von der Raummitte), OG drei; flacher Ring 300 mm, innen 75 % | **Annahme L1** (Ring statt Scheibe nach den ersten Prüfbildern); Erfolg bis 0.5 m (`PruefAufgaben.MarkierungRadiusM`) |
| Farben | Wände `#E4E7EB` (xr_farben «vorgabe» ton), Platten `#D9DDE2` (hell-neutral), Markierung `#6F3E59` (xr_farben «labor» akzent); Räume wie KICAD | nie Gewerkfarben (Test) |
| Ziele | `teleport-markierung` (alle Markierungen), `messen-wand-wand` (EG Süd–Nord 6000 mm, OG West–Ost 8000 mm), `raum-ar-oeffnen` (Raum EG), `tablet-wert-ablesen` (Wand Ost EG, Wand Nord OG, Höhe 2650 mm); `drehen-90`, `etage-wechseln`, `modell-greifen-zoomen` ohne Ziel (Erklärung) | Sollmass und Höhe prüft der Labor-PC gegen die Geometrie (±0.5 mm) |

Kein Durchgang und keine Treppe: «etage-wechseln» läuft über die App «Etagen» (Vorgabe `labor.variante.etage` = app).

**Labor-PC** (`src/Hlks.Labor/Testraum*.cs`, eingebunden mit `LaborTestraum.Abbilden(app)` in `LaborDienste.MapLabor`): derselbe Vertrag wie KICAD
(`src/Hlks.Web/XrModellEndpunkte.cs`), über den Netzzugang der Labor-Brille (`.XrImNetz()`):

```
GET /api/xr/geschosse                    { fassung, projekt: "labor-testraum", bezugspunktMm: [0, 0, 0], achsen, geschosse: [{ id, kennzeichen, name, koteMm, hoeheMm,
                                           modell: { schluessel, elemente, url }, unterlage: null }], hinweise: [] }
GET /api/xr/geschoss/{id}.glb?teil=…     GLB mit ETag "<schluessel>", no-cache, If-None-Match → 304; 400 Teil unbekannt, 404 Geschoss unbekannt bzw. «unterlage»
                                           (der Testraum hat keine IFC-Unterlage); Katalog fehlt oder ungültig → 409 { fehler, vorschlag } mit Ort im Katalog
```

Die GLB schreibt der `GlbSchreiber` des Kerns aus Körperteilen des Kerns (quader, prisma) – Koordinaten, Netze je Gruppe/Farbe/Deckkraft, `mesh.extras.kicad.elemente`,
`scene.extras.kicad` und Versionsschlüssel wie `XrModell` (Fassung 1). Der Katalog wird neu gelesen, sobald sich Zeit oder Grösse der Datei ändern; der Schlüssel einer
Etage ändert sich nur, wenn sich ihr Inhalt ändert.

**Ziele in `scene.extras.kicad.labor`** (je Etage nur ihre Ziele; Form Fassung 1, KICAD-mm absolut, Normalen in KICAD-Achsen):

```
labor = { art: "hlks-labor-testraum", fassung: 1, geschoss: <Id>,
          raeume:       [{ id, kennzeichen, name, bodenMm, raumArOeffnen }],
          markierungen: [{ id, mitteMm: [x, y, z], radiusMm }],
          wandpaare:    [{ id, raum, sollMm, a: Fläche, b: Fläche }]      Fläche = { wand, vonMm: [x, y, z], bisMm: [x, y, z], normale: [x, y, z] }
                                                                          (Innenfläche der Wand am Boden, Normale in den Raum)
          bauteile:     [{ id, aufgabe: "tablet-wert-ablesen", hoeheMm }] }
```

**App** (Labor-Dateien `QuestApp.Labor.cs`, `QuestApp.LaborPruefen.cs`; Regeln in `Hlks.Xr/Labor/LaborZiele.cs`): trägt das Modell `labor`, nehmen die Aufgaben diese
Ziele – fehlt eines in der Etage am Stand, wählt die App wie bisher; ohne `labor` (KICAD) ändert sich nichts:

| Aufgabe | Ziel im Testraum (`LaborZiele`) | sonst (wie bis L0) |
| --- | --- | --- |
| teleport-markierung | Markierung der Etage, 2 … 4.5 m entfernt, kleinster Winkel zum Blick (0.01°), dann Reihenfolge im Katalog | `PruefAufgaben.MarkierungWaehlen` |
| messen-wand-wand | Wandpaar der Etage (zuerst das des Raums am Stand) mit dem Sollmass des Katalogs | `WandPaare.Waehlen` |
| raum-ar-oeffnen | Raum mit `raumArOeffnen` (zuerst in der Etage) | Raum beim Betreten, «Raum zeigen», grösster Raum |
| tablet-wert-ablesen | Bauteil der Etage (Element der Szene) | `PruefAufgaben.BauteilWaehlen` |

**Was es im Labor nicht gibt** (kein Projekt): `/api/xr/marker`, `/api/xr/ebenen`, `/api/xr/ebene/{name}` antworten 404 (Test). Die App übergeht das ohne Hinweis:
Ebenen – `EbenenLader.VomPc(…, fehlenMelden: Marke.MitProjekt)`, `AppMarke.XrLabor.MitProjekt = false` (KICAD meldet weiter «älterer Stand»); Marker – unverändert, die Liste
fehlt nur in der Statuszeile («Liste nicht geholt (404)»), ein gesetzter Marker wartet in der App (er geht nicht an KICAD). Der Katalog des Labors
(`/api/xr/labor/katalog`) kommt mit dem Auftrag «L1 Endpunkte»; bis dahin steht die 404 nur im Protokoll.

**Prüflauf** (`--pruefen`, mit Kopplungspaket des Labor-PCs): mit Zielen prüft die Zeile «P5b … Aufgaben» zusätzlich «Testraum: Wandpaar … aus dem Katalog, drehen-90
Erfolg, etage-wechseln Erfolg (EG → OG → EG), tablet-wert-ablesen Erfolg, Markierung … aus dem Katalog» und im Sitzungsraum (`--auftrag`) «raum-ar-oeffnen Erfolg». Mit
`--bilder` legt er beim Begehen `testraum_eg.jpg`, `testraum_markierung.jpg`, `testraum_wandpaar.jpg`, `testraum_og.jpg`, `testraum_og_wandpaar.jpg` ab (Apps und
Symbole aus; die Markierung bzw. die Wände zeichnet ein Lauf nur für das Bild, ohne Messprotokoll; das OG ab 220 Bildern).

Simulator-Lauf am PC (Testlauf, Ablagen unter `%TEMP%`, Bilder nie ins Repository):

```
$env:HLKS_TEST = '1'; $env:HLKS_BENUTZER = <Temp>; $env:HLKS_ZULETZT = <Temp>; $env:HLKS_LABOR_DATEN = <Temp>
$env:HLKS_KATALOG = <repo>\kataloge; $env:HLKS_LABOR_DESKTOP_PORT = <port>
dotnet <bau>\Hlks.Labor\bin\Debug\net10.0\Hlks.Labor.dll --port <port>
POST /api/xr/zugang { an: true }; POST /api/xr/kopplung/paket { brille, name } → kopplung.json
"C:\Program Files\dotnet\dotnet.exe" <bau>\Hlks.Labor.Quest\bin\Release\net10.0\Hlks.Labor.Quest.dll --pruefen 300 --kopplung kopplung.json --speicher <Temp> --bilder <ordner>
… --auftrag '{"modus":"sitzungsraum","raum":"testraum-eg","nr":1}'      (Sitzungsraum)
```

Stand 26.10.2026 (Simulator, ohne Katalog-Endpunkt): alle sieben Aufgaben mit Erfolg (messen-wand-wand 6000 mm gegen Soll 6000 mm); die Zeile endet trotzdem mit «P4 Fehler»,
weil «Katalog» ohne `/api/xr/labor/katalog` FEHLER ist und die Prüfungen von messen-wand-wand und teleport-markierung aus L0 `stellschrauben` im Messprotokoll verlangen (die
gibt es nur mit Katalog). Nach der Zusammenführung mit dem Auftrag «L1 Endpunkte» erneut prüfen.

## Symbol

Entscheid Christian 26.10.2026: gleiche Gestaltung wie das Desktop-Symbol «XR-Labor», Variante «kraeftig». Das ergibt ein abgerundetes Quadrat in der Fläche
`#6F3E59` mit Zeichen in `#F1E6EC`: ein Erlenmeyerkolben mit «XR» in der Flüssigkeit, darunter eine Punktreihe als Bewegungsbahn. Die Farben stammen aus
`kataloge/xr_farben.json` (App «labor»). Gezeichnet wird mit `werkzeuge/labor_symbol_erzeugen.mjs` (Hauptlauf, Zweig `claude/labor-app-9c82bf`). Die Dateien legt
`Platforms/symbol_erzeugen.mjs` ab:

- `Platforms/Net/App.ico`: dieselbe ICO wie die Desktop-Verknüpfung.
- Android, adaptiv: Hintergrund = Fläche; Vordergrund = nur das Zeichen, auf 60 % verkleinert, damit es in der sicheren Zone bleibt. Die Maske des Launchers
  ergibt die Kachel.

```
node src\Hlks.Labor.Quest\Platforms\symbol_erzeugen.mjs [--erzeuger <labor_symbol_erzeugen.mjs>] [--vorschau <ordner ausserhalb des Repos>]
```
