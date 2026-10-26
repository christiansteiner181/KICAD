# Hlks.Quest – Brillen-App «H+K XR» (Empfänger für alle Werkzeuge, H+K XR E1)

Spezifikation: `entwurf/xr-empfaenger.md` (freigegeben von Christian am 26.10.2026), besonders «Empfänger-Kern», «Was aus L0/L1 wird», Stufe E1 und «Schnittstellen im
Bau E1». Bis E1 war das die KICAD-Brillen-App «KICAD» (`ch.hkplanung.kicad.quest`; Modul XR P0–V1i, `entwurf/modul-xr.md`), daneben aus denselben Quellen die App
«XR-Labor» (`src/Hlks.Labor.Quest`, `ch.hkplanung.xrlabor`; XR-Labor L0/L1). Seit E1 gibt es **eine** App «H+K XR» für alle Werkzeuge am PC; der Ordner heisst
weiter `src/Hlks.Quest` (Entscheid im Bau E1: kein Umbenennen während laufender Sitzungen).

| | |
| --- | --- |
| Projekt (PC, Simulator) | `src/Hlks.Quest/Hlks.Quest.csproj` |
| Projekt (Brille) | `src/Hlks.Quest/Projects/Android/Hlks.Quest.Android.csproj` |
| App-Kennung | `ch.hkplanung.xr` (`Hlks.Xr.Netz.XrApp.Kennung`, Test `WerkzeugTests`) |
| Name | «H+K XR» (`values/strings.xml`, `XrApp.Name`) |
| Eingang der Kopplungen | `/sdcard/Android/data/ch.hkplanung.xr/files/kopplung_<werkzeug>.json` (ein altes `kopplung.json` gilt als `kicad`) |
| Ablage am PC (Simulator) | `%LOCALAPPDATA%\HLKS_CAD\hk_xr` bzw. `--speicher` |
| Vorsatz im Protokoll | `H+K XR:` |
| Werkzeuge (E1) | KICAD (`kicad`, Netzzugang 5140, Suche 5141), XR-Labor (`xrlabor`, Labor-PC `src/Hlks.Labor`, 5340/5341) |

Ports und Suchport stehen nirgends fest in der App; sie kommen aus dem Kopplungspaket des Werkzeugs (`Kopplungspaket.Port`, `.SuchPort`). Der Test
`WerkzeugTests.Ports_stehen_nicht_fest_in_der_App` hält das fest.

## Aufbau

| Teil | Dateien | Inhalt |
| --- | --- | --- |
| Empfänger-Kern | `QuestApp.Werkzeuge.cs`, `WerkzeugStartseite.cs`, `QuestApp.cs` (Hintergrund, Laden), `Optionen.cs`, `Plattform.*.cs` | Kopplungen je Werkzeug, Suche, Kanal je Werkzeug, Startseite, Wechsel per Auftrag, Zwischenspeicher je Werkzeug |
| Werkzeug-Modul «KICAD» | übriger Code: Begehen, Sitzungsraum, Messen, Marker, Ebenen, Desktop, Apps und Symbolfeld | wie bis V1i; das XR-Labor nutzt es mit dem Testraum statt eines Projekts |
| Werkzeug-Modul «Labor» | `QuestApp.Labor.cs`, `QuestApp.LaborPruefen.cs`, `AufgabenKarte.cs` | Katalog, Prüfaufgaben, Messprotokolle, Varianten; nur über die Einhängepunkte, nur mit Werkzeugen, die es führen |
| Regeln ohne StereoKit | `src/Hlks.Xr/Netz/Werkzeug.cs`, `WerkzeugWahl.cs`, `Kopplungspaket.cs`, `KanalVerbindung.cs`, `src/Hlks.Xr/Bedienung/Startseite.cs` | Werkzeug-Record, Start- und Wechselregeln, Paket Fassung 2, mehrere Kopplungen, `hallo.aktiv`, Lage der Startseite (Tests in `tests/Hlks.Xr.Tests`) |

### Werkzeug statt Marke

`Hlks.Xr.Netz.Werkzeug(Kennung, Name)` mit `MitProjekt` (Ebenen, Problem-Marker), `MitLabor` (Werkzeug-Modul «Labor»), `InWerkzeug` («in KICAD», «im XR-Labor»), `Koppeln`
(Hinweis) und `FarbId` (Farbe auf der Startseite aus `kataloge/xr_farben.json`). `Werkzeug.Aus(paket)` liefert die bekannten (`Kicad`, `XrLabor`) mit dem Namen aus dem
Paket, unbekannte allgemein (kein Projekt, kein Labor, Farbe «vorgabe»). `XrApp` sind die Werte der einen App (Kennung, Name, Speicher, Eingang).

| Werkzeug | Kennung | MitProjekt | MitLabor | Farbe |
| --- | --- | --- | --- | --- |
| KICAD | `kicad` | ja | ja, **bis E3** – KICAD führt das Interface-Labor aus P5b noch selbst (Katalog, `labor.aufgabe`, Messprotokolle; `werkzeuge/quest_pruefen.ps1` prüft die Messprotokolle am PC) | `status` |
| XR-Labor | `xrlabor` | nein (neutraler Testraum) | ja | `labor` |
| andere | aus dem Paket | nein | nein | `vorgabe` |

Die frühere Marke der App (`AppMarke`, `Marke.cs`, `Marke.Kicad.cs`, `Marke.XrLabor.cs`) entfällt. `Hlks.Xr.Netz.AppMarke` bleibt nur als Übergangsfassade (Id und Name des
Werkzeugs, Kennung der einen App), bis `tests/Hlks.Labor.Tests` (LaborDatenTests) nachgeführt ist.

### Kopplungen

- **Paket Fassung 2** (`Kopplungspaket`): Fassung 1 plus `werkzeug` (Muster `[a-z][a-z0-9_-]{0,31}`) und `werkzeugName`; Fassung 1 ohne `werkzeug` gilt als `kicad`. Die App
  liest Fassung 1 und 2 und schreibt in ihre Ablage Fassung 2.
- **Eingang → privat** (`KopplungAblage.Importieren`, beim Start): jedes `kopplung_<werkzeug>.json` (und das alte `kopplung.json` als `kicad`) wird nach
  `kopplungen/<werkzeug>.json` übernommen und im Eingang gelöscht, auch ein ungültiges. Das Werkzeug im Paket muss zum Namen der Datei passen. Ein zweites Werkzeug fügt nur
  seine Kopplung hinzu; eine neue desselben Werkzeugs ersetzt die alte. Ein altes privates `kopplung.json` zieht nach `kopplungen/` um.
- **Am PC**: `--kopplung <datei>` mehrfach (je Werkzeug die erste); dann zählen nur diese Dateien.

### Speicher je Werkzeug

`werkzeuge/<kennung>/` im privaten Speicher: `zustand.json` (Adressen des PCs, verbrauchte Auftragsnummer, gemerkter Standpunkt), Zwischenspeicher der GLB, Ebenen,
`marker/` (wartende Marker, Listen je Projekt), `labor/katalog.json` und `labor/messprotokolle_warten.jsonl`. So geht «Start ohne PC» je Werkzeug wie bisher. Die Werte der
Stellschrauben (`LaborWerte`: Katalog › Profil › live) gehören ebenfalls dem Werkzeug – die Grenzen von Katalog (96) und Kanal (128) zählen damit je Werkzeug.

### Start und Startseite

- **Start** (`WerkzeugWahl.Start`): mit genau einer Kopplung direkt dort (wie bis E1, auch ohne PC aus dem Zwischenspeicher); am PC wählt `--werkzeug <kennung>` vor; sonst
  die **Startseite**. Ohne Kopplung: «Nicht gekoppelt – …».
- **Startseite** (`WerkzeugStartseite`, Lage `Hlks.Xr.Bedienung.Startseite`): die gekoppelten Werkzeuge als grosse Glas-Symbole (160 pt, 0.55 m vor den Augen, 0.12 m darunter,
  höchstens vier je Reihe – Annahmen E1) im Ton des Werkzeugs mit dem Namen auf dem Glas, einer Ampel (grün erreichbar, orange nicht erreichbar, rot Fehler) und einem
  Schild darunter (PC, «erreichbar»/«nicht erreichbar»/«sucht …»/Meldung); Titel «H+K XR · Werkzeug wählen». Wahl mit der ganzen Hand (wie das Symbolfeld,
  `HandBeruehrung`) bzw. mit der Pistolengeste auf Distanz. Wählen = Werkzeug aktiv, Inhalt laden.
- **Symbol «Werkzeuge»** im Symbolfeld (nur mit mehreren Kopplungen; Raster aus vier Punkten, Reserveplatz rechts): öffnet die Startseite über dem aktiven Werkzeug (es
  leuchtet dort als «aktiv»); dasselbe wählen schliesst sie, Wegwischen ebenso. Solange sie offen ist, ruht eine Aufgabe des Labors (ihre Karte stünde sonst vor den
  Werkzeugen); die Hände an der Startseite springen und greifen nicht.

### Kanäle und Wechsel

- Zu jedem gekoppelten, erreichbaren Werkzeug ist ein Kanal offen (`KanalVerbindung`). `hallo` trägt `marke` = Kennung des Werkzeugs und `aktiv`; beim Wechsel sendet die
  App `aktiv { aktiv }` an beide; wechselt sie während des Verbindens, folgt `aktiv` gleich nach `willkommen`. Posen gehen nur an das aktive Werkzeug.
- Ein **inaktives** Werkzeug merkt sich Stellschrauben (in seinen Werten); ein **Auftrag** holt die App dorthin: VR-Auftrag (KICAD «In VR zeigen», neue Nummer) bzw.
  Stellschraube `labor.aufgabe` mit einer Aufgabe (Labor «Aufgabe starten»; `null` holt nicht; eine in `willkommen` gemerkte Aufgabe holt beim Verbinden nicht). Dann:
  kurze schwebende Meldung «Wechsel zu …» (2.5 s, gross vor dem Blick), Wechsel, Auftrag ausführen (ein VR-Auftrag gilt als Startpunkt im neuen Modell).
- **Wechsel** (`WerkzeugWechseln`): das bisherige Werkzeug verlassen – das Labor bricht eine laufende Aufgabe ab (Messprotokoll an das bisherige), Standpunkt merken,
  `aktiv false`, Inhalt wegräumen (Sitzungsraum, PC-Bildschirm, Modell, Ebenen, Messungen, Marker) –, das neue aktiv setzen (`aktiv true`, Marker und Labor aus seinem
  Speicher, seine Stellschrauben) und im Hintergrund laden (suchen, wenn nötig; Modell vom PC bzw. aus dem Zwischenspeicher). Ergebnisse aus dem Hintergrund tragen die
  Nummer des Wechsels und gelten nur, solange sie stimmt. Nur ein Werkzeug ist aktiv.
- `--modell-lokal <port>` (nur PC, Prüfläufe) gilt nur für KICAD.

## Einhängepunkte (`QuestApp.Einhaengepunkte.cs`)

Die Grenze zwischen Empfänger-Kern und Werkzeug-Modul «Labor». Der allgemeine Code ruft das Labor nur über `partial void`-Methoden auf; fehlt die Umsetzung, entfernt der
Übersetzer die Aufrufe samt Argumenten. Werte, die der allgemeine Code braucht, gehen als `ref` mit einer Vorgabe des Aufrufers (die Vorgabe gilt ohne Labor). Umgesetzt
sind alle in `QuestApp.Labor.cs` bzw. `QuestApp.LaborPruefen.cs`; seit E1 arbeitet jede Umsetzung nur, wenn das aktive Werkzeug das Labor führt (`LaborAn`,
`Werkzeug.MitLabor`).

| Einhängepunkt | Aufgerufen in | Ohne Labor | Labor |
| --- | --- | --- | --- |
| `LaborAnlegen()` | Konstruktor, nach `WegwischZiele.Add(apps)` | – | Karte anlegen, als Ziel des Wegwischens einhängen (zwischen App und Symbolen) |
| `LaborWerkzeugBetreten()` (E1, bis E1 `LaborStarten`) | `WerkzeugWechseln`, vor `EinstellungenAnwenden` | – | Zustand leeren; Katalog und wartende Messprotokolle aus dem Speicher des Werkzeugs |
| `LaborWerkzeugVerlassen()` (E1) | `WerkzeugVerlassen` | – | laufende Aufgabe abbrechen (Messprotokoll an das bisherige Werkzeug), Karte weg |
| `LaborStellschraube(s, wert, ref ergebnis)` | `StellschraubeAnwenden` | `null` → übliche Stellschraube | `labor.aufgabe` → `AufgabeVomPc` |
| `LaborVerbunden(quelle, ref warten)` | `Laden`, vor dem Modell | `null` | Katalog vom PC (wird abgewartet) |
| `LaborNachladen(ref warten)` | `Nachladen` (alle 30 s) | `null` | Katalog vom PC (ETag) |
| `LaborWillkommen()` | Kanal «willkommen» des aktiven Werkzeugs | – | wartende Messprotokolle nachsenden |
| `LaborNachricht(typ, inhalt)` | Kanal des aktiven Werkzeugs, jede Nachricht | – | Quittung `bezug=messprotokoll` zählen |
| `LaborBild(kopf)` | `Bild`, nach Messen und Marker | – | `AufgabeBild` (Karte, Messung, Markierung) |
| `LaborStick(kopf, kopfRef)` | `Haende` (Begehen) | – | `StickSchritt` (Variante «teleport» = «stick») |
| `LaborImTablet()` | `Box` (Variante «tablet») | – | `AufgabeImTablet` |
| `LaborHandAnKarte(ref nah)` → `HandAnKarte` | `Haende`, `RoentgenSchritt`, `HandFang`, `MessenBild`, `MarkerBild` | `false` | `karte.HandNah` |
| `LaborFlaechen(liste)` | `AppsBild`, `HinweisHindernisse`, `BedienFlaechen` | – | Karte als belegte Fläche |
| `LaborAufgabeOffen(ref offen)` | `WischAnwenden`, Prüflauf «Wechsel» | `false` | Aufgabe läuft |
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
| `LaborKennzahlen(ref text)` | Statuszeile «P4 Status» | `""` | `Katalog=…, Aufgabe=…, Messprotokolle=… ` bzw. `Labor=aus (Werkzeug ohne Labor)` |
| `LaborQuittungen(ref text)` | Zeile «P4 ok/Fehler» | `""` | `, Quittungen=n` |
| `LaborPruefRegeln(teile)` | `PruefP5bRegeln` (`QuestApp.PruefenP5b.cs`) | – | Katalog, Grenzen, Vorgaben, Prüfaufgaben (P5b) |
| `LaborPruefBilder(ordner)` | `PruefBilderP5b` | – | Prüfbild der Karte, Bilder des Testraums |
| `LaborPruefbildVorbereiten()` | `PruefbildVorbereiten` | – | fertige Aufgabe abschliessen und wegräumen |

Röntgen-Hand (`QuestApp.Roentgen.cs`) und flache Hand (`QuestApp.Schnitt.cs`) gehören zur normalen Bedienung, ebenso der Teil «Fassung, Röntgen bzw. Flache Hand» der
Prüfzeile P5b (`QuestApp.PruefenP5b.cs`). Der Test `WerkzeugTests.Der_Empfaenger_ruft_das_Werkzeug_Modul_Labor_nur_ueber_Einhaengepunkte` hält die Grenze fest.

**Probebau ohne Labor** (Empfänger-Kern ohne Werkzeug-Modul «Labor»):

```
$env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build\ohnelabor"
dotnet build src\Hlks.Quest\Hlks.Quest.csproj -p:HlksOhneLabor=true
```

`HlksOhneLabor` lässt die drei Labor-Dateien weg (`Hlks.Quest.csproj`). Ergebnis 26.10.2026 (E1): 0 Fehler, 0 Warnungen.

## Bau

Bauausgabe wie alle Projekte unter `HLKS_BUILD` bzw. `%USERPROFILE%\HLKS_CAD_build`. Ausführen mit `"C:\Program Files\dotnet\dotnet.exe"` (bei Christian zeigt
`DOTNET_ROOT` auf ein .NET 8, `entwurf/modul-xr.md`, «Stand P0»).

- PC: `dotnet build src\Hlks.Quest\Hlks.Quest.csproj -c Release` → `<bau>\Hlks.Quest\bin\Release\net10.0\Hlks.Quest.dll`
- Brille: `dotnet publish src\Hlks.Quest\Projects\Android\Hlks.Quest.Android.csproj -c Release -f net10.0-android`
  → `<bau>\Hlks.Quest.Android\bin\Release\net10.0-android\publish\ch.hkplanung.xr-Signed.apk` (Testschlüssel, nur für die eigene Brille). Abgelegt von
  `werkzeuge\quest_app_bereitstellen.ps1` in `%USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr\`; KICAD und das XR-Labor installieren dieselbe APK von dort. Voraussetzung:
  .NET-Workload «android», Android SDK und JDK 17 unter `%USERPROFILE%\HLKS_CAD_werkzeuge`.
- Bewusst nicht in `HLKS_CAD.slnx` (Android-Workload).
- Neu koppeln nach dem Wechsel der App-Kennung: einmal per Kabel (die alte App «KICAD» bzw. «XR-Labor» hat ihre Kopplung in ihrem eigenen Speicher; der Quest-Dialog am PC
  bietet «Alte Apps entfernen»).

## Simulator und Prüfläufe

Argumente (`Optionen.cs`): `--simulator`, `--pruefen <bilder>`, `--kopplung <datei>` (mehrfach), `--werkzeug <kennung>`, `--speicher <ordner>`, `--modell-lokal <port>`,
`--ohne-unterlage`, `--ohne-pc`, `--bilder <ordner>`, `--auftrag <json>`, `--desktop`, `--wechsel`.

| Lauf | Aufruf | Nachweis |
| --- | --- | --- |
| KICAD (Begehen, ohne PC, Sitzungsraum, Desktop) | `werkzeuge\quest_pruefen.ps1` (eine Kopplung `kicad`) | wie bis V1i, unverändert |
| Labor (Testraum, sieben Aufgaben) | `src\Hlks.Quest\Pruefen\hk_xr_pruefen.ps1 -Lauf Labor` | L1 Begehen und L2 Sitzungsraum «P4 ok» mit «Testraum: …», Messprotokolle der sieben Aufgaben am Labor-PC |
| Wechsel KICAD ↔ Labor | `src\Hlks.Quest\Pruefen\hk_xr_pruefen.ps1 -Lauf Wechsel` | siehe unten |

**Prüflauf «Wechsel»** (`--pruefen 5000 --wechsel --kopplung kopplung_kicad.json --kopplung kopplung_xrlabor.json`, `QuestApp.PruefenWechsel.cs`; das Skript startet einen
KICAD-Testserver mit dem Beispielprojekt und einen Labor-Server im Testlauf, koppelt beide und schickt zur rechten Zeit die Aufgabe):

1. Startseite mit KICAD und XR-Labor, beide erreichbar, keines aktiv (Prüfbild `startseite.jpg`).
2. KICAD mit einer gedachten flachen Hand wählen → Modell, Posen nur an KICAD («P4 Wechsel: KICAD bereit»; das Skript prüft am PC: KICAD `aktiv` mit Pose, Labor nicht).
3. Das Skript schickt `labor.aufgabe` = `drehen-90` an den inaktiven Kanal → «Wechsel zu XR-Labor …» (Prüfbild `wechsel_meldung.jpg`), Testraum, die Aufgabe läuft
   («P4 Wechsel: Labor bereit»; am PC: Labor `aktiv` mit Pose, KICAD nicht).
4. Symbol «Werkzeuge» → Startseite mit XR-Labor als aktiv (Prüfbild `startseite_zurueck.jpg`) → KICAD wählen: die Aufgabe bricht ab (Messprotokoll am Labor-PC), KICAD wieder
   aktiv mit Modell («P4 Wechsel: zurück bei KICAD»; am PC: KICAD `aktiv`, Labor nicht) → «P4 ok: Wechsel: …».

Prüfbilder nie ins Repository (Vorgabe `%USERPROFILE%\HLKS_CAD_testdaten\hk_xr\e1`).

**Ergebnis 26.10.2026 (E1, Simulator):** KICAD `quest_pruefen.ps1` ok (Lauf 1 mit PC «Auftrag=Nr 1 beim Start», Messprotokolle und Marker am PC; Lauf 2 ohne PC aus dem
Zwischenspeicher des Werkzeugs; Lauf 3 Sitzungsraum «Nr 2»; Lauf 4 Desktop). Labor: L1 und L2 «P4 ok», Messprotokolle der sieben Aufgaben am Labor-PC (7/7). Wechsel:
«P4 ok» – Wahl auf der Startseite mit der Hand nach 11 Bildern, Posen an das verlassene Werkzeug nach jedem Wechsel +0, PC-Prüfungen 3/3, Messprotokoll der
abgebrochenen Aufgabe am Labor-PC. APK `ch.hkplanung.xr-Signed.apk`: `aapt2 dump badging` – Paket `ch.hkplanung.xr`, Name «H+K XR», minSdk 29, targetSdk 32.

## Testraum (XR-Labor L1)

Entscheid Christian 26.10.2026: **«Nur neutraler Testraum»** – kein Projekt, keine Projektdaten (`entwurf/labor-app.md`, «Testwelt der Labor-App», «Zuschnitt L1»). Die App
löst darin alle Prüfaufgaben aus `kataloge/xr_pruefaufgaben.json`; seit E1 ist das das Szenario «KICAD-Bedienung» des Werkzeugs «XR-Labor» (Werkzeug-Modul «KICAD» mit
dem Testraum).

**Katalog** `kataloge/labor_testraum.json` (Art `hlks-labor-testraum`, Fassung 1; Aufbau im Kopf von `src/Hlks.Labor/TestraumKatalog.cs`), KICAD-mm absolut:

| Teil | Inhalt | Herkunft |
| --- | --- | --- |
| Etagen | EG Kote 0, OG Kote 2900, Geschosshöhe je 2900 mm | wie Beispielprojekt «MFH Musterweg» (`Beispielprojekt.cs`, EG und 1.OG; Test prüft es) |
| Raum je Etage | innen 8.0 × 6.0 m (x 0 … 8000, y 0 … 6000), lichte Höhe 2650 mm | **Annahme L1** (Vorschlag Auftrag «ca. 8 × 6 m») |
| Wände | 200 mm, Achse wie die Wandgeometrie «linie» von KICAD, von OK Boden bis UK Decke | **Annahme L1** |
| Boden, Decke | Platten 250 mm; die Platte zwischen EG und OG steht zweimal (Decke EG, Boden OG), gleiche Lage und Farbe | **Annahme L1**; doppelt, weil die App beim Begehen nur die Etage am Stand zeichnet |
| Markierungen | EG vier (2.5 m von der Raummitte), OG drei; flacher Ring 300 mm, innen 75 % | **Annahme L1**; Erfolg bis 0.5 m (`PruefAufgaben.MarkierungRadiusM`) |
| Farben | Wände `#E4E7EB` (xr_farben «vorgabe» ton), Platten `#D9DDE2`, Markierung `#6F3E59` (xr_farben «labor» akzent); Räume wie KICAD | nie Gewerkfarben (Test) |
| Ziele | `teleport-markierung`, `messen-wand-wand` (EG Süd–Nord 6000 mm, OG West–Ost 8000 mm), `raum-ar-oeffnen` (Raum EG), `tablet-wert-ablesen` (Wand Ost EG, Wand Nord OG, Höhe 2650 mm); `drehen-90`, `etage-wechseln`, `modell-greifen-zoomen` ohne Ziel | Sollmass und Höhe prüft der Labor-PC gegen die Geometrie (±0.5 mm) |

Kein Durchgang und keine Treppe: «etage-wechseln» läuft über die App «Etagen» (Vorgabe `labor.variante.etage` = app).

**Labor-PC** (`src/Hlks.Labor/Testraum*.cs`): derselbe Vertrag wie KICAD (`src/Hlks.Web/XrModellEndpunkte.cs`) über den Netzzugang des Labors:

```
GET /api/xr/geschosse                    { fassung, projekt: "labor-testraum", bezugspunktMm: [0, 0, 0], achsen, geschosse: [{ id, kennzeichen, name, koteMm, hoeheMm,
                                           modell: { schluessel, elemente, url }, unterlage: null }], hinweise: [] }
GET /api/xr/geschoss/{id}.glb?teil=…     GLB mit ETag "<schluessel>", no-cache, If-None-Match → 304; 400 Teil unbekannt, 404 Geschoss unbekannt bzw. «unterlage»
```

**Ziele in `scene.extras.kicad.labor`** (je Etage nur ihre Ziele; Form Fassung 1, KICAD-mm absolut, Normalen in KICAD-Achsen):

```
labor = { art: "hlks-labor-testraum", fassung: 1, geschoss: <Id>,
          raeume:       [{ id, kennzeichen, name, bodenMm, raumArOeffnen }],
          markierungen: [{ id, mitteMm: [x, y, z], radiusMm }],
          wandpaare:    [{ id, raum, sollMm, a: Fläche, b: Fläche }]      Fläche = { wand, vonMm, bisMm, normale } (Innenfläche am Boden, Normale in den Raum)
          bauteile:     [{ id, aufgabe: "tablet-wert-ablesen", hoeheMm }] }
```

**App** (Werkzeug-Modul «Labor»; Regeln in `Hlks.Xr/Labor/LaborZiele.cs`): trägt das Modell `labor`, nehmen die Aufgaben diese Ziele – fehlt eines in der Etage am Stand,
wählt die App wie bisher; ohne `labor` (KICAD) ändert sich nichts:

| Aufgabe | Ziel im Testraum (`LaborZiele`) | sonst |
| --- | --- | --- |
| teleport-markierung | Markierung der Etage, 2 … 4.5 m entfernt, kleinster Winkel zum Blick (0.01°), dann Reihenfolge im Katalog | `PruefAufgaben.MarkierungWaehlen` |
| messen-wand-wand | Wandpaar der Etage (zuerst das des Raums am Stand) mit dem Sollmass des Katalogs | `WandPaare.Waehlen` |
| raum-ar-oeffnen | Raum mit `raumArOeffnen` (zuerst in der Etage) | Raum beim Betreten, «Raum zeigen», grösster Raum |
| tablet-wert-ablesen | Bauteil der Etage (Element der Szene) | `PruefAufgaben.BauteilWaehlen` |

**Was es im Labor nicht gibt** (kein Projekt): `/api/xr/marker`, `/api/xr/ebenen`, `/api/xr/ebene/{name}` antworten 404. Die App übergeht das ohne Hinweis
(`Werkzeug.XrLabor.MitProjekt = false`); ein gesetzter Marker wartet im Speicher des Werkzeugs.

**Prüflauf** (`-Lauf Labor`): mit Zielen prüft die Zeile «P5b … Aufgaben» zusätzlich «Testraum: Wandpaar … aus dem Katalog, drehen-90 Erfolg, etage-wechseln Erfolg (EG → OG →
EG), tablet-wert-ablesen Erfolg, Markierung … aus dem Katalog» und im Sitzungsraum (`--auftrag`) «raum-ar-oeffnen Erfolg». Mit `--bilder` legt er beim Begehen
`testraum_eg.jpg`, `testraum_markierung.jpg`, `testraum_wandpaar.jpg`, `testraum_og.jpg`, `testraum_og_wandpaar.jpg` ab (das OG ab 220 Bildern).

## XR-Labor Spielwiese S1 (26.10.2026)

Neuausrichtung des Labors (`entwurf/xr-empfaenger.md`, Kasten oben; `entwurf/bedien-grundsatz.md`, Kapitel 1). Christian, 26.10.2026: «Aufrufen von Apps: Bsp. Appmenue
aufrufen, apps wählen, menue schliessen. das labor wird durch dich bedient, wir feilen dann nur an diesem grundsatz.» Die heutige Bedienung von KICAD wird im XR-Labor ohne
KICAD-Inhalt mit neutralen Platzhalter-Apps erlebbar und live verstellbar.

| Teil | Dateien | Inhalt |
| --- | --- | --- |
| Katalog | `kataloge/labor_spielwiese.json` (Art `hlks-labor-spielwiese`, Fassung 1) | die Apps: Kennung, Name, Symbol, Farbe, Inhalt (`text`, `seiten`, `liste`, `schalter`, `kacheln`, `uhr`); Aufbau und Grenzen im Kopf von `src/Hlks.Xr/Labor/Spielwiese.cs` |
| Regeln ohne StereoKit | `src/Hlks.Xr/Labor/Spielwiese.cs` (`SpielwieseKatalog`, `SpielwieseTafel`), `src/Hlks.Xr/Netz/SpielwieseLader.cs` | Lesen und Prüfen (Fehler mit Stelle), Seiten, Zeilen, Zeit und Datum; Laden vom Labor-PC mit ETag/304, Ablage je Werkzeug, eingebaute Vorgabe |
| Labor-PC | `src/Hlks.Labor/SpielwieseEndpunkt.cs` | `GET /api/xr/labor/spielwiese` (auch über den Netzzugang 5340): der Text der Datei unverändert, ETag/304; ungültig bzw. fehlend 409 `{ fehler, vorschlag }` |
| Werkzeug-Modul «Labor» | `QuestApp.Spielwiese.cs`, `QuestApp.PruefenSpielwiese.cs` | Symbole und Inhalt der Tafel über sieben neue Einhängepunkte (`LaborSymbole`, `LaborAppGeoeffnet`, `LaborAppInhalt`, `LaborAppFarbe`, `LaborAppEreignis`, `LaborBlaettern`, `LaborPruefenSpielwiese`); im Probebau ohne Labor weggelassen |
| Kern (allgemein) | `SymbolApps.cs` (Farbe je Symbol, eigene Reihenfolge), `GlasZeichen.cs` (selbst gezeichnete Zeichen «@…»), `GlasApps.cs` (Kacheln, grosse Schrift), `Hlks.Xr/Bedienung/Glas.cs` (`GlasInhalt.Kacheln`, `Gross`, `FreieHoehe`), `AppSymbole.Raster` (eigene Reihenfolge) | ohne Wirkung auf KICAD |

**Wann:** Das Symbolfeld zeigt die Spielwiese, wenn das aktive Werkzeug das XR-Labor ist (`Werkzeug.MitSpielwiese`), ein Katalog da ist und **keine Prüfaufgabe läuft**.
Eine Aufgabe (Szenario «KICAD-Bedienung» im Testraum) bringt für ihre Dauer die KICAD-Apps zurück, die sie braucht (Etagen, Messen, Tisch …); danach kehrt die Spielwiese
zurück. KICAD als Werkzeug zeigt weiter seine eigenen Apps. Prüfläufe ohne `--spielwiese` (Labor L1/L2, Wechsel) lassen die Spielwiese aus und prüfen unverändert.

**In der Brille:** Beim Betreten des Labors schliesst eine offene Tafel des Werkzeugs (Status), einmal erscheint der Hinweis «XR-Labor Spielwiese: beide Handflächen nach oben
– kurz anheben holt die Apps.» Der Testraum EG bleibt Boden und Hintergrund (hell). Die **Hebegeste** holt sechs Platzhalter-Apps und «Werkzeuge»:

| App | Symbol | Farbe (xr_farben, Vorschlag S1) | Tafel |
| --- | --- | --- | --- |
| Notizen | `@notizen` (Zeilen) | `notizen` #F2EEDC | kurzer Text (über mehrere Seiten, wenn er nicht passt) |
| Bilder | `@bilder` (Rahmen, Berge, Sonne) | `bilder` #F7DCD8 | sechs Kacheln: Farbfläche mit Kreis, Quadrat, Kapsel, Ring bzw. Streifen und Titel |
| Uhr | `@uhr` (Zifferblatt) | `uhr` #E9F1DA (lindgrün) | Zeit gross, Datum («Montag, 26. Oktober 2026»), Zeit der Brille |
| Liste | `@liste` (Punkte und Zeilen) | `liste` #D8EEDB | 40 Einträge wie die Etagen: höhere Tafel bis `etagen.zeilen_max` (12) Zeilen, Wisch hoch/runter rollt um eine Zeile weniger als sichtbar; Antippen markiert einen Eintrag |
| Dokument | `@dokument` (Seite mit Eselsohr) | `dokument` #EFE6DA | drei Seiten, Wisch nach links/rechts blättert |
| Einstellungen | `@einstellungen` (drei Regler) | `einstellungen` #E0E3EA | fünf Schalter ohne Wirkung (zwei Seiten zu vier) |
| Werkzeuge | `@werkzeuge` (vier Punkte, E1) | vorgabe | Startseite der Werkzeuge (in der Spielwiese immer da, auch mit nur einer Kopplung – Annahme S1) |

Die Platzhalter liegen in drei Reihen zu zwei Spalten in der Reihenfolge des Katalogs, «Werkzeuge» rechts daneben (`AppSymbole.Raster` mit eigener Reihenfolge; ohne sie
lägen sie alle in Reservespalten rechts der leeren Plätze von KICAD). Bedienung **unverändert** (Kapitel 1 des Grundsatzes): Berühren mit der ganzen Hand, Zeiger und
Tooltip, Leuchten und Ton, Sperren, Symbole blenden nach der Wahl aus, Tafel mittig und fest, Blättern auf der Tafel, Wegwischen (die Symbole kommen nicht zurück),
Hebegeste bei offener App schliesst sie (V1i). Die Tafel trägt keine Werkzeugleiste (Messen, Marker gehören zu KICAD). Alle Stellschrauben wirken live vom Labor-PC
(Labor-Seite, Register Stellschrauben, bzw. `POST /api/xr/stellschraube`).

**Apps und Inhalte ändern (ohne neue APK):** `kataloge/labor_spielwiese.json` am Labor-PC bearbeiten (Ordner der Kataloge des Labors, `--katalog` bzw. `HLKS_KATALOG`),
mit `GET http://localhost:5430/api/xr/labor/spielwiese` prüfen (200 = gültig; 409 nennt die Stelle, z. B. `apps[uhr].farbe «rot» steht nicht in kataloge/xr_farben.json`).
Die Brille fragt beim Verbinden und alle 30 s mit ETag nach und übernimmt die neue Fassung im nächsten Bild (Protokoll «Spielwiese pc (…)»); ohne Verbindung gilt die zuletzt
geholte Fassung (`werkzeuge/xrlabor/labor/spielwiese.json` im Speicher der App), sonst die beim Bau eingebettete. Neue Farben zuerst in `kataloge/xr_farben.json` eintragen;
neue Symbole sind Zeichen «@…» (`GlasSymbole.Gezeichnet`, gezeichnet in `GlasZeichen.cs` – das braucht eine neue APK) oder die Texturen von KICAD (`GlasSymbole.Alle`).

**Prüflauf «Spielwiese»** (`src\Hlks.Quest\Pruefen\hk_xr_pruefen.ps1 -Lauf Spielwiese`; die App mit `--pruefen 5000 --spielwiese --kopplung kopplung_xrlabor.json`):
XR-Labor aktiv, Testraum, Spielwiese vom Labor-PC, keine Aufgabe und keine Karte → Hebegeste: sechs Platzhalter und «Werkzeuge» → «Dokument» mit einer gedachten flachen
Hand → Symbole aus, Tafel mittig → Wisch auf der Tafel: Seite 2 → Wegwischen: Tafel zu, Symbole nach 2 s nicht zurück → Hebegeste → «Liste» → Wisch nach oben rollt →
Hebegeste bei offener Liste: Tafel zu, Symbole frontal (V1i) → Notizen, Bilder, Uhr, Einstellungen je offen (ein Schalter umgeschaltet) → Zeile «P4 Spielwiese: Stellschraube
bereit», das Skript setzt am Labor-PC `symbole.abstand_m` = 0.7, das Symbolfeld rückt von 0.50 m auf 0.70 m. Prüfbilder (nie ins Repository) in
`%USERPROFILE%\HLKS_CAD_testdaten\hk_xr\s1\`: `symbolfeld.jpg`, `app_dokument.jpg`, `dokument_seite2.jpg`, `nach_wegwischen.jpg`, `app_liste.jpg`, `liste_gerollt.jpg`,
`hebe_bei_offener_app.jpg`, `app_notizen.jpg`, `app_bilder.jpg`, `app_uhr.jpg`, `app_einstellungen.jpg`, `stellschraube_vorher.jpg`, `stellschraube_nachher.jpg`.

**Ergebnis 26.10.2026 (S1, Simulator):** `-Lauf Spielwiese` ok – Spielwiese vom Labor-PC, 7 Symbole, Dokument mit der flachen Hand nach 32 Bildern gewählt, Tafel 0.0° neben
dem Blick, Seite 2/3, nach dem Wegwischen Symbole nicht zurück, Liste ab Eintrag 12 gerollt, V1i ok, die übrigen vier Apps offen, `symbole.abstand_m` live 0.50 → 0.70 m;
13 Prüfbilder. Probebau ohne Labor: 0 Fehler, 0 Warnungen. APK gebaut (Katalog der Spielwiese eingebettet). KICAD `werkzeuge\quest_pruefen.ps1`: ok (der erste Lauf
war «nicht ok» in Lauf 2, `messen-wand-wand` traf eine Leitung 1341 mm vor der Wand – das Skript nimmt als Auftrag den «ersten Raum des Modells», der je nach den Ids des
Beispielprojekts im UG liegt; derselbe Befund steht in Protokollen anderer Sitzungen vom 26.10.2026 14:06 und 14:13, der Ausgangsstand 5437e29 lief im Vergleich ok mit
Start im EG; zweiter Lauf mit Start im EG ok).

**Annahmen S1:** Symbole als selbst gezeichnete Zeichen (in `symbole.js` gibt es keine Uhr, keine Notiz und kein Bild; neue Texturen bräuchten `werkzeuge/quest_symbole.mjs`);
Farben der sechs Apps als Vorschlag nach der Regel von V1a; «Werkzeuge» immer im Feld; Grenzen des Katalogs (11 Apps, Texte 1200 Zeichen, Listen 200 Einträge);
Kacheln im Querformat 0.72, drei je Reihe; Uhr in 56 pt.

## Symbol

Entwurf E1 (Christian beurteilt; `entwurf/xr-empfaenger.md`: «eigenes Symbol (Entwurf an Christian)»): gleiche Grundform wie `werkzeuge/kicad.ico` und das Symbol des XR-Labors
(abgerundetes Quadrat), Zeichen neutral ohne Bezug zu einem Werkzeug – der Umriss einer Brille (Visier mit Nasenausschnitt, Bügelansätze) mit «XR» im Glas, darunter «H+K»;
bis 32 px ohne Schrift, dafür zwei Linsen. Farben aus `kataloge/xr_farben.json`, Eintrag «vorgabe» (neutral), Variante «kraeftig» wie das Labor: Fläche `#3E536F`, Zeichen
`#E4E7EB`. Erzeuger `werkzeuge/hk_xr_symbol_erzeugen.mjs` (Muster `werkzeuge/labor_symbol_erzeugen.mjs`); er legt ab:

- `Platforms/Net/App.ico` (256/64/48/32/16 px),
- Android, adaptiv: `mipmap-*/appicon_background.png` = Fläche, `appicon_foreground.png` = nur das Zeichen, auf 60 % verkleinert (sichere Zone); `values/ic_launcher_background.xml`,
- Vorschau ausserhalb des Repositorys (`%USERPROFILE%\HLKS_CAD_testdaten\hk_xr`): `symbol_vorschau.png`, `symbol_vorschau_hell.png`, `app_symbol_quadrat.png`,
  `app_symbol_kreis.png`, `symbol_vergleich.png` (KICAD, XR-Labor, H+K XR kräftig und hell).

```
node werkzeuge\hk_xr_symbol_erzeugen.mjs [--variante kraeftig|hell] [--vorschau <ordner ausserhalb des Repos>] [--ohne-app] [--ohne-vorschau]
```

## Annahmen und offene Punkte (E1)

- **KICAD führt das Labor-Modul bis E3** (`Werkzeug.Kicad.MitLabor = true`): Der Auftrag nannte das Modul «nur aktiv, wenn das aktive Werkzeug `xrlabor` ist»; KICAD hat aber das
  Interface-Labor aus P5b noch selbst (Katalog, Prüfaufgaben, Messprotokolle), und `werkzeuge/quest_pruefen.ps1` verlangt die Messprotokolle am PC. Mit E3 («Labor-Teile aus
  KICAD entfernen») wird es `false`.
- Masse der Startseite (160 pt, 0.55 m, 0.12 m, Lücke 0.10 m), Dauer der Meldung «Wechsel zu …» (2.5 s) und das Bild des Symbols «Werkzeuge» (Raster aus vier Punkten):
  Annahmen E1, Stellschrauben folgen mit dem Bedienstandard (E2/E3).
- Unbekannte Werkzeuge (z. B. HKAP) erscheinen auf der Startseite und verbinden sich; was sie zeigen, regeln erst die Bedienaufgaben (E6).
