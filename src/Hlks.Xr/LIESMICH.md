# Hlks.Xr – Rechenregeln und Netz-Teil der Quest App (Modul XR, P1, P4a, P4b, P5a und P5b)

Reine C#-Bibliothek (`net10.0`) ohne StereoKit, ASP.NET und Hlks.Kern. Sie enthält die Regeln, die im Browser-Prototyp als JavaScript mit
Node-Tests stehen: Begehen, Start, Messen mit Fang, Sitzungsraum, Tablet, Wischgeste. Die App (`src/Hlks.Quest`) zeigt, die Bibliothek rechnet; ein Wechsel
der Technik der App trifft die Regeln nicht (Entscheid Christian 23.10.2026, `entwurf/modul-xr.md`). Seit P4a dazu der Netz-Teil der App (`Netz/`: Kopplung,
Pinning, Suche, Live-Kanal, Zwischenspeicher, Umrechnung) und das Modell aus den GLB (`Modell/`: Lesen, Szene, Durchgang auf Netzen, Räume, Start),
damit beides am PC getestet werden kann (siehe «Netz-Teil und Modell der App (P4a)» unten).

Die JavaScript-Dateien bleiben unverändert (Prototyp). Zu jeder Funktion steht im Kommentar die Quelle (`datei.js`, Funktionsname). Tests:
`tests/Hlks.Xr.Tests` (aus den `*.test.mjs` mit denselben Erwartungswerten; `bash werkzeuge/cloud/tests.sh xr`).

## Welche Klasse ersetzt welche JavaScript-Datei

| C# (src/Hlks.Xr) | JavaScript (src/Hlks.Web/wwwroot/js) | Inhalt |
| --- | --- | --- |
| `VrLogik` | `ansichten/vr-logik.js` | Knopf, Sitzungsoptionen, Rig (Start, Drehen, Teleport), Stick, Snap-Turn, Teleport-Ziel, IFC-Grenze, Bildzeit, Standort ohne Raum |
| `Begehen` | `ansichten/begehen-logik.js` | nur Konstanten, `ZustandAusStandort`, `Einblendung` |
| `Durchgang` | `ansichten/begehen-logik.js`, Abschnitt «Durchgang» | Regel «Wände nur unter 500 mm durchscheinend»: `FormAusTeil`, Abstände, `ImDurchgang`, `DurchgangDeckkraft` |
| `VrStartLogik` | `vr-start-logik.js` | Startpunkt (Auftrag, `?raum=`, letzter Standpunkt, EG, Gesamtansicht), gemerkter Standpunkt, VR-Auftrag vom PC |
| `XrMessLogik` | `ansichten/xr-mess-logik.js` | Fangdaten aus Körpern, Fangen mit Finger und Strahl, Masse (dx/dy/dz), Lot, Masse zu Decke und Wand |
| `XrSitzungLogik` | `ansichten/xr-sitzung-logik.js` | Tisch, Massstab, Griffe, Gesten mit einer und zwei Händen, Ausschnitt, Etagen und Schnitthöhe, Tablet-Seite |
| `SchnittbereichLogik` | `ansichten/schnittbereich-logik.js` | nur Lesen, Achsen, Ebenen, Weltpunkte und Grenzen des Quaders (für den Ausschnitt) |
| `XrTabletLogik` | `ansichten/xr-greifen-logik.js` | Faust-, Pinch- und Tablet-Geste aus Gelenken, Lage und Treffer auf dem Tablet, Tasten und Layout |
| `XrTabletRuhe` | `ansichten/xr-greifen-logik.js`, «Beruhigen des Tablets» | One-Euro-Filter, Totzone, Nachführen |
| `XrWischLogik` | `ansichten/xr-greifen-logik.js`, «Wischgeste» | Wisch erkennen, Treffer auf Masslinien, Sperre |
| `Vec3`, `Xz`, `Box3`, `Sortierung` | – | Grundbausteine (Vektor, Box, natürliche Sortierung) |
| `HandBegehen`, `HandGelenke` | – (P4a, nach der Leitlinie «Hände zuerst») | Teleport mit Zeigen und Pinch (Zeigestrahl des Systems, Ruhezeit 0.25 s, Abbruch beim Verlassen des Bodens), Drehen mit Pinch und Wisch; WebXR-Gelenknamen ↔ StereoKit |
| `DrehenSchalter` | – (P4a-Nachtrag, Christian 23.10.2026) | Haltung aus der Kopfhöhe über dem Boden (sitzend/stehend mit Hysterese und Haltezeit), Drehen ein/aus, Schalter übersteuert bis zum nächsten Haltungswechsel |
| `DreieckForm` (in `Durchgang`) | – (P4a) | Durchgang gegen Dreiecke aus der GLB (die App hat die Körper von `/api/koerper` nicht) |

## Konventionen

- **Koordinaten** wie im Prototyp, in WebXR und in StereoKit: Y nach oben, Meter, Blick nach −Z. Die Szene entspricht der 3D-Ansicht von KICAD
  (x = Ost, z = −Nord); Modellpunkte des Kerns (mm) sind x Ost, y Nord, z oben und werden in `XrMessLogik.AusModell` bzw. `Durchgang.FormAusTeil` umgerechnet.
- **Winkel** im Bogenmass; das «Gieren» zählt gegen den Uhrzeigersinn von oben, ab +x (90° = Nord = −z). Standorte vom Kern tragen den Azimut in Grad.
- **Rig** (Ursprung des Referenzraums `local-floor` in der Welt): `Rig.ZuMatrix()` und `Rig.ZuQuaternion()` liefern die Drehung um die Hochachse für die App
  (Zeilenvektor-Schreibweise von `System.Numerics`, entspricht `rotation.y` in three).
- **Zahlen** in doppelter Genauigkeit (`Vec3`); die Umwandlung von und zu `System.Numerics.Vector3` geschieht an der Grenze zur App (implizit hinein,
  `(Vector3)v` bzw. `v.ZuVector3()` hinaus).
- **Sprache:** Deutsch (Schweiz) in Namen, Kommentaren und Texten wie im Rest des Repositorys; die Namen folgen den JavaScript-Namen (`SNAP_GRAD` → `SnapGrad`).
- **Eingaben** sind typisiert statt lose (`Standort`, `Koerper`/`KoerperTeil`, `VrAuftragRoh`, `SchnittbereichRoh` …). Wo die JavaScript-Fassung auf falsche Typen prüft,
  gibt es einen `JsonElement`-Zugang (`VrStartLogik.AuftragPruefen(JsonElement)`). `GemerktStandpunkt` und `VrAuftragRoh` tragen die JSON-Namen des Prototyps, damit
  Prototyp und App dieselbe Benutzereinstellung `vr.letzter_standpunkt` und denselben Auftrag lesen.
- **Zustand** ist unveränderlich (Records mit `with`), ausser dort, wo der Prototyp ihn ausdrücklich fortschreibt: `Wischer`, `TabletZustand`, `EinEuroZustand`, `Fangdaten`,
  `Bildzeit`.

## Abweichungen zur JavaScript-Fassung

1. **`Vec3` statt `Vector3` in der Schnittstelle.** Der Auftrag nannte `System.Numerics.Vector3`; das ist `float`. Die Erwartungswerte der Tests (1e-9) und Koordinaten im
   Gebäudemassstab (z. B. 67 m aus dem Brillentest) verlangen doppelte Genauigkeit. `Vec3` wandelt verlustfrei hinein und gerundet hinaus; `Matrix4x4` und `Quaternion`
   (float) nur an den Rig-Hilfen für die App.
2. **Natürliche Sortierung** (`Sortierung.cs`) ersetzt `localeCompare(…, { numeric: true })`: unabhängig von ICU und Gebietsschema, aber für Kennzeichen wie R-1, R-10, 1.OG
   gleich. Umlaute und Sonderzeichen ordnen nach Zeichenwert.
3. **`einpassen` liefert `Einpassung { Lage, Passt }`** statt eines Objekts mit zusätzlichem Feld `passt` an der Lage.
4. **Kote als Text:** `EgGeschoss.KoteAusText` (ein leerer Text ergibt keine Kote; JavaScript `Number('')` wäre 0).
5. **Auftragsnummer** ist eine ganze Zahl im Bereich von `long` (JavaScript bis 2^53); gespeichert wird als `double?`.
6. **Quader** ohne drei Masse oder ohne Mitte ergibt `null` (JavaScript rechnet mit `NaN` weiter).
7. **`Math.round`/`toFixed`** sind nachgebildet (`Num.Round`, `Num.ToFixed`): halbe Werte werden aufgerundet, minus null wird zu null.
8. **`StrahlTreffer`** heisst als Funktion `XrMessLogik.StrahlTrefferErster`, weil der Ergebnistyp den Namen `StrahlTreffer` trägt.

## Bewusst nicht übertragen (kommt später oder gehört nicht in die Bibliothek)

- **Greifen XR-B selbst** (`xr-greifen-logik.js`): Reichweite (`abstandZuElement`, `elementInReichweite`), Querverschiebung in 1-cm-Schritten (`querAchsen`, `aufSchritt`,
  `querVerschiebung`, `befehlsMasse`), Heranholen per Strahl (`heranholAnteil`). `XrMessLogik.DeckeUndWaende` ist übertragen, weil es in `xr-mess-logik.js` steht.
- **Begehen am Bildschirm** (`begehen-logik.js`): Tasten, Maus, Gehen, Drehen, Umsehen, Kamerawerte, Projektwerte, Tastenverlauf.
- **Schnittbereich bearbeiten** (`schnittbereich-logik.js`): Griffe, Flächen schieben, Quader aus Punkten, Speichern.
- **Zeichnen und Browser:** `vr3d.js`, `vr-start.js`, `xr-sitzung.js`, `xr-messen.js`, `xr-holo.js`, `xr-tablet.js`, `durchgang3d.js` (three-Materialien umschalten, Wiederherstellen); die
  Tests dazu (`vr.html`, Manifest, `quest-text.js`, Material-Umschalten, Leistung mit 5000 Bauteilen) und die Browserreihen (`*.browser.mjs`).
- **Sprache der Oberfläche:** Texte der Knöpfe und Zeilen (`VrText`, Tablet-Tasten, Kopfzeilen) sind übernommen, weil sie Teil der Regeln und der Tests sind.

## Netz-Teil und Modell der App (P4a)

| Datei | Klassen | Inhalt |
| --- | --- | --- |
| `Netz/Kopplungspaket.cs` | `Kopplungspaket`, `KopplungAblage`, `XrNetzFehler` | Paket wie von `POST /api/xr/kopplung/paket` lesen und prüfen (auch `{ paket: … }`), Bearer, Suchschlüssel; Übernahme aus dem Eingang in den privaten Speicher |
| `Netz/XrPinning.cs` | `XrPinning` | TLS nur mit dem gepinnten SHA-256-Fingerabdruck (keine Kette); Handler für HTTP, Prüfung für den Kanal |
| `Netz/PcZugang.cs` | `PcZugang`, `PcAdresse`, `IModellQuelle`, `ModellHttp` | HTTPS zum Netzzugang: Probe `GET /api/xr/verbindung`, Liste und GLB (auch über localhost für Tests) |
| `Netz/PcSuche.cs` | `PcSuche`, `SucheOptionen`, `PcFund` | PC finden: gespeicherte Adressen, Adressen des Pakets, sonst UDP-Broadcast mit HMAC; Antwort prüfen |
| `Netz/KanalVerbindung.cs` | `KanalVerbindung`, `KanalText`, `KanalRegeln`, `PosenTakt`, `KicadPose` | Live-Kanal: hallo/willkommen, Posen im Takt, Auftrag, Stellschraube, Wiederverbinden (nicht nach 4001/4002/401) |
| `Netz/KicadUmrechnung.cs` | `KicadUmrechnung` | Szene der App (m, y oben, relativ zum Bezugspunkt) ↔ KICAD (mm, z oben); Quaternionen; Standorte |
| `Netz/GlbZwischenspeicher.cs` | `GlbZwischenspeicher`, `ModellListe` | GLB je Geschoss und Teil nach Versionsschlüssel, `If-None-Match`/304, zuletzt geladenes Projekt, Aufräumen |
| `Netz/ModellLader.cs` | `ModellLader`, `ModellStand` | Modell vom PC (mit Zwischenspeicher) bzw. ohne PC aus dem Zwischenspeicher |
| `Netz/AppAblage.cs` | `AppAblage` | Was die App behält: Adressen des PCs, verbrauchte Auftragsnummer, gemerkter Standpunkt |
| `Modell/GlbLeser.cs` | `GlbLeser`, `GlbDatei`, `GlbNetzDaten`, `ElementBereich` | GLB von KICAD lesen, mit extras (Gruppe, Bereiche je Element, Bezugspunkt); ohne die 180°-Drehung des Laders von StereoKit |
| `Modell/ModellSzene.cs` | `ModellSzene`, `SzenenElement` | alle GLB zusammen: Elemente mit Hüllbox, Räume, Bauteile für den Durchgang |
| `Modell/NetzDurchgang.cs` | `NetzDurchgang`, `DurchgangVerfolger` | Durchgang (500 mm) je Kopfposition; Netze in undurchsichtig und durchscheinend aufteilen |
| `Modell/RaumStandort.cs` | `RaumStandort` | Standort im Format von `raum_begehen` aus dem Netz eines Raums (Schwerpunkt bzw. innerster Punkt, Boden, Blick) |
| `Modell/AppStart.cs` | `AppStart` | Startpunkt nach `VrStartLogik` mit den Räumen der GLB und dem gemerkten Standpunkt der App |
| `Modell/GeschossWahl.cs` | `GeschossWahl` | Geschoss am Standboden (Kote höchstens 0.3 m darüber), die App zeichnet nur dieses mit IFC-Unterlage |
| `Modell/NetzBloecke.cs` | `NetzBloecke` | grosse Netze in Blöcke zu höchstens 20 000 Dreiecken (möglichst an Elementgrenzen); Bereiche je Block für den Durchgang |

- **Pinning:** Die App nimmt nur das Zertifikat an, dessen SHA-256 über die DER-Bytes dem `fingerabdruck` des Pakets gleicht (Vergleich in fester Zeit);
  Fehler der Kette und des Namens zählen nicht. Gilt für HTTP und WebSocket.
- **Suche:** Zuerst alle bekannten Adressen gleichzeitig mit `GET /api/xr/verbindung` (Frist 2.5 s). Antwortet keine, zwei UDP-Anfragen im Abstand von 1.5 s an
  255.255.255.255 und die Broadcast-Adressen der eigenen Netze; gültig ist nur eine Antwort mit eigenem Zufall, richtiger HMAC-Signatur und dem gepinnten
  Fingerabdruck. 401 heisst «neu koppeln» (dann nicht weiter probieren, der PC sperrt nach 10 Fehlversuchen).
- **Kanal:** hallo (Name, Gerät, App), willkommen abwarten, Posen höchstens 25 je Sekunde und nur neue; Wiederverbinden nach 1, 2, 4, 8, 15, dann 30 s, vor
  jedem Versuch die Adresse neu bestimmen; nicht nach 4001 (widerrufen), 4002 (ersetzt) und 401.
- **Zwischenspeicher:** Steht der Schlüssel der Liste schon im Speicher, gilt die Datei ohne Anfrage; sonst `If-None-Match` mit dem gespeicherten ETag
  (304: Datei gilt weiter, 200: ersetzen). Fällt das Netz aus, gilt die ältere Fassung. Höchstens drei Projekte, je Geschoss und Teil nur die neueste.
- **Tests:** `tests/Hlks.Xr.Tests` (Netz*, GlbLeser*, ModellSzene*, HandBegehen*); gegen den echten Netzzugang `tests/Hlks.Web.Tests/XrAppKlientTests.cs`, mit
  Test_praxis `XrAppKlientEchtTests.cs` (ohne die Kopie übersprungen). Die App selbst im Simulator: `werkzeuge/quest_pruefen.ps1`.

## Sitzungsraum, Passthrough und Ebenen der Fachmodule (P4b)

Die Regeln des Tisches selbst (Masse, Massstab, Griffe, Gesten mit zwei Händen, Schnitthöhe, Ausschnitt) bleiben in `XrSitzungLogik` (aus `xr-sitzung-logik.js`). Neu ist, was
die App zusätzlich braucht; die App selbst: `src/Hlks.Quest/Sitzungsraum.cs`, `Passthrough.cs`, `EbenenDarstellung.cs`, Shader `Assets/schnitt.hlsl`.

| Datei | Klassen | Inhalt |
| --- | --- | --- |
| `Sitzung/SitzungRaum.cs` | `SitzungRaumLage`, `SitzungRaum`, `SitzungBezug`, `SchnittQuader`, `SchnittLage`, `SichtbarRahmen` | Raum-lokal ↔ Raum der Brille (virtuell vor dem Kopf; echter Tisch: Mitte der Platte auf dem angetippten Punkt), Modell auf dem Tisch (Regel `ModellZuRaum` und dieselbe Abbildung als `ModellMatrix`, mit Höhe), Schnittquader mit Lage einer Box und Werten für den Shader; P4b-Nachtrag: Griffe an den vorderen Ecken des sichtbaren Gebäudes (`GriffPunkte` mit einem Rechteck in den Achsen des Gebäudes) |
| `Sitzung/SitzungAuswahl.cs` | `SitzungAnsicht`, `SitzungAuswahl` | Geschosse mit Decken aus der Szene (Ebene «decke», IfcSlab), Etagenschnitt (nur das Geschoss und die darunter, 50 mm unter der Decke), «Alle Etagen», «Raum in AR» (Raum ± 1.5 m, Achsen nach der längsten Kante, oben unter der Decke), Startlage, Etage zu einem Auftrag; P4b-Nachtrag: Gebäude ohne Ausreisser (`Gebaeude`: Grundriss der Platten, Höhe aller Bauteile), `Ausrichtung` (Kanten der Raumgrundrisse), `GebaeudeRahmen` und `AusschnittRahmen` für die Griffe, `AusschnittIndizes` für die Netze des Ausschnitts |
| `Sitzung/Hintergrund.cs` | `Hintergrund`, `HintergrundVerwalter` | P4b-Nachtrag: der eine Ort für Löschfarbe und Himmel (Sitzungsraum dunkel, Passthrough durchsichtig, sonst der gemerkte Zustand) |
| `Sitzung/TischAntippen.cs` | `TischAntippen`, `TischAntippenZustand`, `TischAntippenEinstellungen`, `TischTreffer` | Tischhöhe aus der Spitze des Zeigefingers: nach unten (≥ 2 cm), dann 0.3 s ruhig (12 mm), 0.4 … 1.3 m, ≤ 1 m vom Kopf, kein Pinch; Höhe = Median − Radius der Fingerspitze |
| `Sitzung/PassthroughFb.cs` | `PassthroughFb`, `EchterTisch` | Strukturen, Werte und Funktionsnamen von `XR_FB_passthrough` (64 Bit, geprüft in den Tests); Zustand des Schalters «Echter Tisch» (verfügbar, ein, wartet aufs Antippen) |
| `Ebenen/XrEbene.cs` | `XrEbeneFormat`, `XrEbene`, `EbenenElement`, `EbenenListe`, `EbenenEintrag` | Format einer Ebene (`hlks-xr-ebene`, Fassung 1: linie, pfeil, punkte, partikel, flaeche; KICAD-mm absolut) lesen und prüfen, Grenzen, unbekannte Typen mit Hinweis |
| `Ebenen/EbenenSzene.cs` | `EbenenSzene`, `EbenenTeil`, `EbenenStrich` | Ebene in der Szene: Striche (Pfeilspitze waagrecht), Partikel zur Zeit t, Flächen in Dreiecken (Ohrenschneiden), Hüllbox |
| `Netz/EbenenLader.cs` | `EbenenLader`, `EbenenAblage`, `IEbenenQuelle`, `TextAntwort`, `EbenenStand` | Ebenen vom PC (`GET /api/xr/ebenen`, je Ebene mit `If-None-Match`), Ablage im privaten Speicher für den Start ohne PC; `ModellHttp` ist auch `IEbenenQuelle`, `PcZugang.Ebenen` |

- **Annahmen P4b** (Stellschrauben im Labor P5, Christian beim Brillentest): Zoom bis 1:5 (`XrSitzungLogik.MassstabMin`, Prototyp 1:20), Platten als Umriss des Gebäudes, Schwellen des Antippens, Ersatzhöhe 3000 mm für Geschosse ohne Höhe, IfcSlab als Decke, Achsen des
  Ausschnitts nach der längsten Kante des Raums (der Kern nimmt den Planwinkel), Rand 1500 mm (die Benutzereinstellung `ansicht3d.raumrand` erreicht die App nicht), Aussehen der
  Pfeile (Spitze 40 %, 6 × Breite, mindestens 8 cm).
- **Tests:** `SitzungRaumTests` (Lage vor dem Kopf und am Tisch, Matrix gegen Regel, Punkt unter den Händen bleibt bei der Zweihandgeste, Quader gegen `SchnittbereichLogik`),
  `SitzungAuswahlTests` (Decken, Etagenschnitt, Lage der Blöcke, Ausschnitt, Etage zum Auftrag), `TischAntippenTests` (Antippen, Zittern, in der Luft, Pinch, Grenzen, Speicherbild
  XR_FB_passthrough), `XrEbeneTests` (Format, Szene, Laden mit 304 und Ablage); gegen den Server `tests/Hlks.Web.Tests/XrEbenenApiTests.cs`.

## Bedienung: schwebende Apps, feste Gesten, Etagen, Komfort (P5a)

Regeln der Bedienung nach der Leitlinie (Christians Leitbild «schwebende Apps mit Wischgesten», feste Gestenbedeutungen mit Geisterhänden, Komfort-Stellschrauben;
`entwurf/modul-xr.md`). Die App: `src/Hlks.Quest/SchwebendeApps.cs`, `QuestApp.cs`, `Sitzungsraum.cs`; Bedienung und Tabelle der Gesten in `src/Hlks.Web/OBERFLAECHE.md`,
Abschnitt «Modul XR, Quest App P5a».

| Datei | Klassen | Inhalt |
| --- | --- | --- |
| `XrGesten.cs` | `WischRichtung`, `WischEinstellungen`, `PinchEinstellungen`, `XrGesten`, `PinchWischZustand`, `GestenAktion`, `GestenKontext`, `Gestenbedeutung` | Wisch in allen Richtungen (waagrecht genau wie der Prototyp, neu senkrecht), Einordnen zum Blick (links, rechts, vor, zurück, hoch, runter), Wisch mit gehaltenem Pinch mit Sperre der Gegenrichtung, Bedeutung eines Wischs im Zusammenhang, Tabelle der Gesten |
| `Bedienung/XrBedienEinstellungen.cs` | `XrBedienEinstellungen`, `AppEinstellungen`, `KomfortEinstellungen`, `Anordnungen`, `Varianten` | alle Stellschrauben der Bedienung (Schlüssel wie im Katalog von P6) mit Grenzen, Paarbedingungen (Pinch an &lt; aus, Zoom min &lt; max), Zurücksetzen mit `null`, mehrere auf einmal in zwei Durchgängen («willkommen») |
| `Bedienung/AppBogen.cs` | `Kachel`, `KachelArt`, `ListenEintrag`, `AppAnker`, `AnkerZustand`, `EinblendZustand`, `AppLayout`, `AppListe` | Lage der Kacheln (Bogen, Reihe, an der Hand), offene App mit Einträgen und «×», träges Nachführen zum Blick (Totzone, still bei Hand an den Apps), Einblenden mit der linken Handfläche, Blättern |
| `Bedienung/AppBeruehrung.cs` | `AppBeruehrung`, `BeruehrHand`, `BeruehrErgebnis` | Antippen (Druckebene von vorn), Fang zum nächsten Ziel, Leuchten nach der Nähe, Pinch wählt beim Loslassen, Sperre bis zum Zurückziehen |
| `Bedienung/Geisterhand.cs` | `Geisterhand`, `GeisterGeste`, `ZoegernZustand`, `GeisterBild` | Zögern erkennen, Geisterhand (Antippen, Blättern, Wegwischen, Greifen) aus der Form der eigenen Hand, Knochen zum Zeichnen |
| `Bedienung/Tunnelblick.cs` | `Tunnelblick`, `TunnelZustand`, `TunnelForm` | Tunnelblick beim Springen, Drehen und Etagenwechsel: Verlauf (weich ein, halten, weich aus), Öffnung nach der Stärke |
| `Modell/EtagenWechsel.cs` | `EtagenWechsel`, `EtagenZiel`, `SitzungEtagenZiel` | Geschoss darüber/darunter nach der Kote; Begehen: gleicher Punkt im Raum, sonst grösster Raum, ohne Räume auf der Kote (mit Probe gegen `GeschossWahl`); Sitzungstisch: Etagen und «Alle Etagen» |

- **Geändert für P5a:** `XrWischLogik.WischSchritt` nimmt optional `WischEinstellungen` (dann alle Richtungen; ohne bleibt es der Prototyp für die Masslinien);
  `XrTabletLogik.IstPinch` nimmt optional `PinchEinstellungen`; `HandBegehen` meldet jeden Wisch (`BegehenArt.Wisch` mit Richtung), dreht nur seitlich (ein Wisch nach
  vorn oder hinten dreht nicht mehr), die Richtungssperre gilt für alle Wische, `springen = false` an den Apps, Pinch- und Wischschwellen aus `HandBegehenEinstellungen.Pinch`
  bzw. `.Wisch`; `XrSitzungLogik` mit `ZoomGrenzen` (Stellschrauben `sitzung.zoom_min/max`) in `MassstabBegrenzen`, `LageBegrenzen`, `Einpassen`, `AusschnittLage`,
  `ZweihandSchritt`, `GestenSchritt`, `MassstabText` und `SitzungAuswahl.Startlage` (ohne Angabe 1:5 … 1:200 wie bisher).
- **Annahmen P5a** (Stellschrauben, wo der Katalog sie vorsieht; sonst benannte Konstanten): Kachel 18 cm, 45 cm vor der Brust, Brusthöhe 30 cm unter den Augen, Mitte 50° links
  vom Blick, 4 je Reihe, Reihenabstand 1.4 × Kachel, Totzone 30° / 15 cm, Folgezeit 0.35 s, Handfläche 0.4 s; Druckebene 5 mm, Leuchten ab 10 cm, Loslassen 2.5 cm, Fang 3 cm,
  Pinch wählt bis 4 cm Weg; Zögern 1.5 s in 3 cm, Wiederholung nach 6 s, Vorführung 1.8 s; Tunnel 0.08 / 0.12 / 0.35 s, Öffnung 60° … 15°, Rand 18°.
- **Tests:** `BedienungTests` (Wisch, Gestenbedeutung, Stellschrauben und Paare, Pinch an einer Stelle, Zoomgrenzen, Bogen und Reihe und an der Hand, offene App, Blättern,
  Nachführen, Einblenden, Antippen, Fang, Pinch wählt, Sperre nach dem Öffnen, Zögern, Geisterhand, Tunnelblick), `EtagenWechselTests`, `HandBegehenTests` (Wisch hoch/runter,
  seitlich, vor, offene App, an den Apps, Schwellen, Sperre beim Etagenwisch). Die App selbst: `werkzeuge/quest_pruefen.ps1` (Zeile «P5a»).

## Labor in der App: Katalog, Prüfaufgaben, Varianten, flache Hand, Röntgen-Hand (P5b)

Das Interface-Labor in der App (`entwurf/modul-xr.md`, Zeile P5, «Ergänzungen»; Vertrag mit KICAD in `src/Hlks.Web/OBERFLAECHE.md`, «Modul XR: XR-Labor»). Die App:
`src/Hlks.Quest/QuestApp.Labor.cs`, `AufgabenKarte.cs`, `Sitzungsraum.cs`, `Fassung.targets`; Bedienung in `OBERFLAECHE.md`, Abschnitt «Quest App P5b».

| Datei | Klassen | Inhalt |
| --- | --- | --- |
| `Labor/LaborKatalog.cs` | `LaborKatalog`, `LaborStellschraube`, `LaborAufgabeDef`, `LaborVarianteDef`, `LaborProfil`, `WertUrteil` | Antwort von `GET /api/xr/labor/katalog` lesen (streng beim Kopf, tolerant bei den Einträgen, Hinweise), Grenzen, Werte nach dem Katalog prüfen (Zahl, Ja/Nein, Auswahl, Varianten) |
| `Labor/LaborWerte.cs` | `LaborWerte` | wirksame Werte in der Reihenfolge Vorgabe des Katalogs › Profil › live; Grenzen aus dem Katalog, Paare in zwei Durchgängen, «ok/ungültig/unbekannt», neuer Katalog verwirft ungültige live-Werte; Varianten und Stellschrauben für das Messprotokoll |
| `Labor/PruefAufgaben.cs` | `PruefAufgaben`, `PruefLauf`, `LaufZustand`, `AufgabeOrt`, `TabletFrage` | Lauf einer Prüfaufgabe (gesperrt, wartet, läuft, Erfolg, abgebrochen, neu begonnen), Regeln je Aufgabe (Markierung, Drehen, Etage, Zielmassstab, Raum in AR, Frage zum Bauteil), Fehlgriffe, Messprotokoll, Lage der Karte |
| `Netz/LaborKatalogLader.cs` | `LaborKatalogLader`, `LaborKatalogAblage`, `LaborKatalogStand` | Katalog vom PC mit `If-None-Match`/304, Ablage im privaten Speicher, Start ohne PC; 404 und Netzfehler ergeben die Ablage |
| `Netz/ProtokollAblage.cs` | `ProtokollAblage` | Messprotokolle ohne Verbindung (höchstens 50, über einen Neustart) |
| `Sitzung/FlacheHand.cs` | `FlacheHand`, `FlacheHandZustand`, `FlacheHandEinstellungen`, `HandSchnitt`, `HandSchnittArt`, `FlacheHandEreignis` | Schnitt mit der flachen Hand: Lage der Hand (waagrecht, senkrecht), über dem Modell, Ruhe, Folgen, Behalten; Schnitt in der Szene, Fang an die Achsen, zusammen mit dem Schnitt der Ansicht (Quader für den Shader), Rahmen |
| `Modell/RoentgenHand.cs` | `RoentgenHand`, `RoentgenZustand`, `RoentgenKegel`, `RoentgenEinstellungen`, `RoentgenVerfolger` | Röntgen-Hand: Geste (offen, Handfläche nach vorn) mit Ein- und Ausschaltzeit, Kegel, Treffer gegen die Dreiecke (Kette von Kugeln), Bauteile im Kegel (ohne Boden, nach Geschoss, nur bei Bewegung neu) |
| `StickTeleport.cs` | `StickTeleport`, `StickZustand` | Variante «teleport» = «stick»: zielen, springen, Abbruch, Snap-Turn |
| `Bedienung/Textumbruch.cs` | `Textumbruch` | Zeilenumbruch an Wortgrenzen für Texte in der Brille |

- **Geändert für P5b:** `XrBedienEinstellungen` mit `roentgen.*`, `sitzung.schnitt_*`, `labor.variante.teleport`, `labor.variante.messen` (`Varianten.Bekannt`, `Variante(name)`,
  `MitStick`) und dem Parameter `grenzen` (Grenzen aus dem Katalog) in `MitStellschraube`/`MitStellschrauben`, ebenso `HandBegehenEinstellungen` und
  `HaltungEinstellungen`; `WischEinstellungen.ToleranzVorgabe` = 36.87 (arccos 0.8 auf 0.01° gerundet, der Katalog führt sie genau; `CosToleranz` bleibt dafür 0.8);
  `GestenKontext.AufgabeOffen` und `GestenAktion.AufgabeAbbrechen` (Wegwischen der Karte), Gestentabelle mit flacher Hand und Röntgen-Hand; `BeruehrErgebnis.Daneben`
  («Antippen daneben», `AppBeruehrung.DanebenM` 6 cm); `KanalText.Messprotokoll`; `DurchgangVerfolger.BereicheJeNetz(szene, schlüssel)` (Durchgang und Röntgen-Hand
  zusammen); `RaumStandort.RaumBei`, `RaumStandort.GroessterRaum`.
- **Annahmen P5b** (Stellschrauben, wo der Katalog sie führt; sonst benannte Konstanten): Markierung 2 … 4.5 m vor einem (Radius 0.5 m aus dem Katalog), Haltezeit
  beim Drehen 0.5 s, Zielmassstab = runder Massstab nahe der halben Startgrösse, Frage nach der Höhe mit Antworten im Abstand 0.2 m; flache Hand: offen ab 1.65 (halten
  1.4), Toleranz 25° (+10° Hysterese), nicht zum Gesicht (45°), Rand 5 cm, bis 30 cm über dem Modell, Ruhe 0.3 s in 2 cm, Fang an die Achsen 10°, 1 s nach einem Griff
  gesperrt (die App); Röntgen-Hand: vorn
  bis 50° (halten 65°), ein 0.2 s, aus 0.25 s, Kegel 40° und 6 m, Kugeln alle 0.25 m, Boden 5 cm; Stick: zielen ab 0.7, los unter 0.3, Ziel höchstens 0.1 s alt; Karte
  0.30 × 0.17 m, 60 cm vor dem Auge, 28° rechts, 8 cm unter den Augen; Antippen daneben bis 6 cm neben dem Fang; Zeichenbreite 0.55 × Höhe.
- **Tests:** `LaborKatalogTests`, `PruefAufgabenTests`, `FlacheHandTests`, `RoentgenHandTests`, `StickTeleportTests`, `P5bBedienungTests` (Hilfe `HandHilfen`: Hand in
  beliebiger Lage); gegen den Server und die echten Kataloge `tests/Hlks.Web.Tests/XrLaborTests.cs` (Katalog = Code der App). Die App selbst: `werkzeuge/quest_pruefen.ps1`
  (Zeile «P5b»).

## Offene Punkte

- Die Gelenknamen der WebXR-Hand-API bildet `HandGelenke` auf die 25 Gelenke von StereoKit ab (P4a).
- Die Schwellen (Faust, Totzone des Tablets, Griffe, Tisch, Schnitt, Schulter des Zeigestrahls) sind die **Annahmen des Prototyps bzw. von P4a/P4b** und stehen in den
  Kommentaren der Konstanten. Seit P5a sind Pinch, Fang, Wisch, Zoom, Drehwinkel, Komfort und die Apps Stellschrauben (`XrBedienEinstellungen`), dazu Springen und
  Haltung (P4a), seit P5b Röntgen-Hand und flache Hand (Ruhezeit, Winkel); die übrigen bleiben feste `const`-Werte, bis das Labor sie braucht.
- Nicht gebaut: Faust greift (XR-B); Messen (Version 1, Prüfaufgabe «messen-wand-wand» gesperrt). «tablet-wert-ablesen» fragt nach der Höhe aus der Hüllbox des Bauteils
  (die GLB kennt keine Eigenschaften); echte Eigenschaften kommen mit «Eigenschaften dort, wo man hinzeigt».
- Der JSON-Zugang für `Koerper` (`/api/koerper`) fehlt weiter; die App rechnet den Durchgang mit `DreieckForm` aus der GLB. `Standort` (`raum_begehen`) ersetzt
  `RaumStandort` aus dem Netz des Raums, bis der Server die Standorte der Räume mitliefert (siehe Bericht P4a).
- P4b: Den Tisch findet die App noch nicht selbst (`XR_EXT_spatial_plane_tracking`) und erkennt keine Marker (`XR_EXT_spatial_marker_tracking`); beides nur auf Machbarkeit
  geprüft (Bericht P4b). Die Fliessrichtung der Probe-Ebene folgt der Reihenfolge der Leitungspunkte, nicht dem Netzdurchlauf (`netz_folgen`).

## Desktop in der Brille (XR-I, V1c)

Der Bildschirm des gekoppelten PCs als grosser, leicht gebogener Bildschirm im Raum, wie die Desktop-Ansicht von Meta (Christian 24.10.2026; `entwurf/modul-xr.md`,
«XR-I – Desktop in der Brille»). Bedient wird mit Maus und Tastatur am PC, die Brille zeigt nur an. Die App: `src/Hlks.Quest/DesktopBildschirm.cs`,
`QuestApp.Desktop.cs`; am PC `src/Hlks.Web/XrDesktop.cs` (Server), `XrDesktopKodierung.cs` (eigene Umsetzung des Formats), `XrDesktopErfassung.cs`; Bedienung
in `src/Hlks.Web/OBERFLAECHE.md`, Abschnitt «Modul XR, Quest App V1c».

| Datei | Klassen | Inhalt |
| --- | --- | --- |
| `Desktop/DesktopGeste.cs` | `SeitenWisch`, `SeitenWischZustand`, `SeitenWischStand`, `DesktopHaende`, `IWegwischErkennung`, `DesktopWegwischen`, `DesktopGesten`, `DesktopGestenStand` | Hineinziehen (rechte Hand offen, Handfläche nach links, links → rechts) und Wegwischen (dieselbe Haltung, rechts → links) mit Fortschritt (der Bildschirm folgt der Hand), andere Hand ruhig, Sperre und Gegensperre. **Einhängepunkt** für V1a's gemeinsames Wegwischen: `IWegwischErkennung` (Parameter `weg` von `DesktopGesten`) |
| `Desktop/DesktopAnsicht.cs` | `DesktopMasse`, `DesktopFlaeche`, `DesktopNetz`, `DesktopPhase`, `DesktopLage`, `DesktopAnsicht`, `WegwischZiel`, `DesktopBedienStand` | Zylinderausschnitt (Bogen, Höhe, Radius), Netze je Feld und Glasrahmen mit gerundeten Ecken, Platz frontal, Kommen von links und Hinausgleiten nach links (Drehung um die Augen), Setzen, verankert, Griff (verschieben, zum Kopf drehen), Schliessen- und Monitor-Knopf, wegwischbares Ziel |
| `Desktop/DesktopFormat.cs` | `DesktopFormat`, `DesktopKopf`, `DesktopRaster`, `DesktopPuffer`, `DesktopFormatFehler` | Bild «HXD1» (Kopf 32 Byte, Kacheln 64 px, BGR mit Vorfilter «Sub», Deflate) lesen und in Felder RGBA (512 px, je eine Textur) schreiben; Stand je Feld, Sperre je Feld |
| `Desktop/DesktopEinstellungen.cs` | `DesktopEinstellungen`, `SeitenWischEinstellungen` | Stellschrauben `gesten.hineinziehen.*`, `gesten.wegwischen.*`, `desktop.*` mit Grenzen; feste Werte (Zeitfenster, andere Hand, Sperre, Höhe, Breite in Punkten, Mipmaps, Trennen, Hochladen) |
| `Desktop/PruefHand.cs` | `PruefHand` | gedachte offene Hand für den Prüflauf am PC (`--desktop`) |
| `Netz/DesktopVerbindung.cs` | `DesktopVerbindung`, `DesktopZustand`, `DesktopMonitor`, `DesktopOptionen`, `DesktopRegeln` | WebSocket `/api/xr/desktop` mit Pinning und Schlüssel: start, Pause/weiter, Quittung nach dem Dekodieren (Flusskontrolle), Monitor, Bildrate, neu; Dekodieren im Hintergrund; Wiederverbinden, nicht nach 4010 (am PC beendet), 4001, 4002, 401 |

- **Geändert für V1c:** `Labor/LaborWerte.cs` kennt die Schlüssel von `DesktopEinstellungen` (vierter Satz `Desktop`, Schichten Katalog › Profil › live wie die übrigen).
- **Annahmen V1c** (Stellschrauben, wo der Katalog sie führt; sonst benannte Konstanten): Weg 0.20 m, Tempo 0.45 m/s, Handfläche 45°, Zeitfenster 0.7 s, andere Hand
  8 cm, Sperre 1 s, Folgen ab 20 % des Wegs; Bildschirm 1.4 m breit, 1.1 m vor den Augen, 10 cm unter Augenhöhe, Radius 2.0 m, ganz draussen 95° links, Setzen 0.35 s,
  Hinausgleiten 0.3 s; Griff 16 × 2.2 cm, Knöpfe Radius 2.2 cm; 12 Bilder/s, höchstens 1920 Punkte breit, ohne Mipmaps, 2 Felder je Bild, Trennen nach 60 s.
- **Tests:** `DesktopGesteTests` (Auslösen in jeder Körperrichtung, Folgen, Abbruch, Ausholen, Zurückholen, zwei Hände, linke Hand, Pinch, Handlagen, Hebegeste und
  Wegschieben, Testgesten des Labors, 80 Zufallsbewegungen, umgekehrt Pinch + Wisch und Gestenlabor, gedachte Hand des Prüflaufs, Stellschrauben), `DesktopAnsichtTests`
  (Form, Netze, Platz, Kommen und Gehen, Griff, Knöpfe, Ziel, Format und Puffer); gegen den Server `tests/Hlks.Web.Tests/XrDesktopTests.cs`.
