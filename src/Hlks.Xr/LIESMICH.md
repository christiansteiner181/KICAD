# Hlks.Xr – Rechenregeln und Netz-Teil der Quest App (Modul XR, P1 und P4a)

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
| `HandBegehen`, `HandGelenke` | – (P4a, nach der Leitlinie «Hände zuerst») | Teleport mit Zeigen und Pinch, Drehen mit Pinch und Wisch; WebXR-Gelenknamen ↔ StereoKit |
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

## Offene Punkte

- Die Gelenknamen der WebXR-Hand-API bildet `HandGelenke` auf die 25 Gelenke von StereoKit ab (P4a).
- Die Schwellen (Pinch, Faust, Wisch, Totzone, Griffe, Tisch, Schnitt, Schulter des Zeigestrahls, Verzögerung des Teleports) sind die **Annahmen des Prototyps
  bzw. von P4a** und stehen in den Kommentaren der Konstanten; sie werden im Interface-Labor (P5) an der Brille nachgestellt. Als Stellschrauben sind sie hier
  noch feste `const`-Werte.
- Der JSON-Zugang für `Koerper` (`/api/koerper`) fehlt weiter; die App rechnet den Durchgang mit `DreieckForm` aus der GLB. `Standort` (`raum_begehen`) ersetzt
  `RaumStandort` aus dem Netz des Raums, bis der Server die Standorte der Räume mitliefert (siehe Bericht P4a).
