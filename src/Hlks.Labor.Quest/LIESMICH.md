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
- **In L0 noch in `src/Hlks.Quest`:** die Labor-Teile `QuestApp.Labor.cs`, `QuestApp.LaborPruefen.cs` (Labor-Teil des Prüflaufs) und `AufgabenKarte.cs`. Sie
  gehören in L0 zu beiden Apps (keine Verhaltensänderung der KICAD-App) und ziehen in L1 hierher. Sie setzen die Einhängepunkte selbst um.

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
