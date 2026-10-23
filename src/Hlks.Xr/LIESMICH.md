# Hlks.Xr – Rechenregeln der Quest App (Modul XR, P1)

Reine C#-Bibliothek (`net10.0`) ohne StereoKit, ASP.NET und Hlks.Kern. Sie enthält die Regeln, die im Browser-Prototyp als JavaScript mit
Node-Tests stehen: Begehen, Start, Messen mit Fang, Sitzungsraum, Tablet, Wischgeste. Die App (`src/Hlks.Quest`) zeigt, die Bibliothek rechnet; ein Wechsel
der Technik der App trifft die Regeln nicht (Entscheid Christian 23.10.2026, `entwurf/modul-xr.md`).

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

## Offene Punkte

- Die Gelenknamen sind die der WebXR-Hand-API; StereoKit liefert 25 Gelenke in anderer Schreibweise. Die Abbildung gehört in die App (P4/P5).
- Die Schwellen (Pinch, Faust, Wisch, Totzone, Griffe, Tisch, Schnitt) sind die **Annahmen des Prototyps** und stehen in den Kommentaren der Konstanten; sie werden im Interface-Labor
  (P5) an der Brille nachgestellt. Als Stellschrauben sind sie hier noch feste `const`-Werte.
- Der JSON-Zugang für `Koerper` (`/api/koerper`) und `Standort` (`raum_begehen`) fehlt; er entsteht mit dem Modellformat (P2/P3).
