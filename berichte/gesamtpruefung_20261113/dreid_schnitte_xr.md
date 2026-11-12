# Gesamtprüfung KICAD 13.11.2026 – Bereich «dreid_schnitte_xr» (3D-Ansicht, Schnitte/Ansichten, XR-Export im Kern)

Geprüft (Runde 4, Stand `bc03c40`): `js/ansichten/ansicht3d.js`, `koerper3d.js`, `schnittbereich3d.js`, `schnittbereich-logik.js` (ganz gelesen),
`navigieren-logik.js`, `huelle3d.js`, `netzdatei.js` (Koordinaten, Stichproben); Kern `Grafik/SchnittRaum.cs`, `SchnittZerlegung.cs`, `SchnittNetz.cs`,
`SchnittTeile.cs`, `SchnittVerdeckung.cs`, `SchnittGeometrie.cs`, `SchnittBezug.cs`, `Schnittgrafik.cs` (ganz gelesen); `Xr/XrKoordinaten.cs`,
`XrNetzbau.cs`, `XrModell.cs`, `GlbSchreiber.cs`, `XrFarben.cs` (ganz gelesen). Nicht geprüft: `SchnitthoeheSia.cs` (Schnitthöhe des Grundrisses,
Normbezug, kein Geometriethema dieses Bereichs), Begehen/VR im Browser (`begehen3d.js`, `vr3d.js`, `xr-*.js`), Projekt `HK_XR`.

## 1. Kurzfazit

Die Koordinatenwechsel Modell → three.js und Modell → glTF stimmen und sind deckungsgleich (Handbeispiele unten), ebenso Umlaufsinn und Normalen aller
Grundkörper im XR-Export, die Ebenen des 3D-Schnittbereichs und die Rechnung der Schnittflächen. Ein echter Geometriefehler lag in Schnitt und Ansicht:
bei Prismen mit abgesetztem Grundriss (L-Form, z. B. Decken) fehlte die senkrechte Kante am Absatz – korrigiert. In der 3D-Ansicht waren drei Dinge
unsauber: «alles zeigen» schnitt das Modell im schmalen Fenster seitlich ab, jede Änderung von Schnittbereich oder Startansicht baute alle Körper neu,
und eine Farbe mit unlesbarer Länge machte Körper weiss – alle drei korrigiert. Grösser und offen: die 3D-Ansicht hat (anders als der XR-Export)
keinen Bezugspunkt für grosse Koordinaten, und die nahe Schnittebene der Kamera richtet sich nur nach der Modellgrösse.

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Test | Commit |
|---|---|---|---|---|---|
| 1 | `Grafik/SchnittZerlegung.cs` (`Prisma`) | Senkrechte Kanten wurden je Körper nur nach der Lage u entdoppelt. Liegen zwei Ecken in der Projektion übereinander, aber in verschiedener Tiefe (Absatz eines L-förmigen Grundrisses), blieb die zuerst gefundene. War das die hintere, verdeckte sie die vordere Fläche – die Kante am Absatz fehlte ganz (abhängig von der Reihenfolge der Ringpunkte). Handbeispiel: Grundriss (0,0)(4000,0)(4000,1000)(2000,1000)(2000,3000)(0,3000), Blick nach Süden: Flächen y = 1000 (w = 4000) und y = 3000 (w = 2000) stossen bei u = 3000 aneinander; vorher 0 mm Kante, richtig 250 mm (Plattendicke). | Schlüssel u **und** w (je 0.001 mm). Gemeinsame Ecken zweier Flächen (gleiches u und w) bleiben einfach. | neu `SchnittTests.Ansicht_PrismaMitAbsatz_KanteAmAbsatzSichtbar` (vorher rot: 0 statt 250). Lauf `--filter "FullyQualifiedName~SchnittTests|FullyQualifiedName~AnschlussWandDecke|FullyQualifiedName~FamilieAnsicht"`: 80 grün. | `d52f0c6` |
| 2 | `js/ansichten/ansicht3d.js` (`einpassWerte`), neu `navigieren-logik.js` `einpassAbstand` | Abstand der Kamera = r / sin(fov/2) mit dem **senkrechten** Öffnungswinkel. Ist das Fenster schmaler als hoch (geteilte Ansichten), ist der waagrechte Winkel kleiner: bei fov 45° und Breite : Höhe = 0.5 halber waagrechter Winkel atan(tan 22.5° · 0.5) = 11.7°; nötig 4.93 r, gerechnet 2.61 r · 1.05 → das Modell ragte links und rechts aus dem Bild. | Massgebend ist der kleinere der beiden halben Öffnungswinkel; breite Fenster unverändert. | neu in `navigieren.test.mjs` (r 10, 45°, 0.5 → 51.78 m): `node --test js/ansichten/navigieren.test.mjs` 11 grün. | `d4f1ad4` |
| 3 | `js/ansichten/ansicht3d.js` (`ansichtGeaendert`), neu `koerper3d.js` `darstellungSchluessel` | Jede Änderung des Ansichtselements baute **alle** Körper neu (`ausDatenNeu`: Geometrien, Kanten, Materialien, Netze neu anhängen) – auch wenn nur der Schnittbereich (beim Ziehen eines Griffs alle 600 ms und beim Loslassen) oder die Startansicht gespeichert wurde. Beides ändert keinen Körper (Clipping hängt an den vorhandenen Materialien). | Neuaufbau nur, wenn sich der Schlüssel aus Id, Geschoss und allen Eigenschaften der Ansicht ausser `schnittbereich` und `startansicht` ändert; sonst nur neu zeichnen. | neu in `koerper3d-netz.test.mjs`: `node --test js/ansichten/koerper3d-netz.test.mjs` grün. | `c6a932b` |
| 4 | `js/ansichten/koerper3d.js` (`hexOderNull`) | Liess `#` + 3 bis 8 Hex-Zeichen durch; three r160 liest nur 3 und 6 (`Color.setStyle`, sonst Warnung und Farbe bleibt weiss). Ein Teil mit `#rrggbbaa` war im 3D weiss, im XR-Export (`XrFarben.Normiert`) in der Materialfarbe. | Nur `#rgb` und `#rrggbb`, sonst Materialfarbe – wie im Kern. | neu in `koerper3d-netz.test.mjs`; Lauf wie oben, 7 grün. | `6a635e5` |

Nicht gelaufen: Browserreihen (`schnittbereich.browser.mjs`, `maus-wuerfel.browser.mjs` u. a.) – Korrekturen 2 und 3 sind dort nur indirekt betroffen
(Abstand im breiten Fenster unverändert; Körper nach Schnittbereich-Änderung bleiben dieselben Objekte). XR-Tests nicht gelaufen (kein XR-Code geändert).

### Nachgerechnet und in Ordnung (Handbeispiele)

- **Modell → three.js / glTF** (`koerper3d.js vek`, `netzdatei.js`, `huelle3d.js`, `XrKoordinaten.Punkt`): P = (1000, 2000, 3000) mm → (1, 3, −2) m in beiden.
  Drehung um x (Determinante +1): Dreieck (0,0,0)(1,0,0)(0,1,0) mit Normale +z wird (0,0,0)(1,0,0)(0,0,−1), Kreuzprodukt (0, 1, 0) = oben – Umlaufsinn und
  Normalen bleiben. `Zurueck` ist die genaue Umkehrung.
- **Quader im 3D**: `BoxGeometry(lx, lz, ly)` und `rotateY(drehung)`: x' = x·cos + z·sin, z' = −x·sin + z·cos; mit z = −y_Modell ergibt das
  x' = x·cos − y·sin, y' = x·sin + y·cos – gleicher Drehsinn wie der Kern. Prisma: `rotateX(−90°)` bildet (x, y, z) auf (x, z, −y) ab.
- **XR-Netzbau**: alle sechs Quaderflächen (Kreuzprodukt je Fläche = angegebene Normale), Prisma-Seiten (Aussenring im Gegenuhrzeigersinn, aussen = rechts
  der Kante), Zylinder (`Senkrechte`: e1 × e2 = e; Mantel Tangente × Achse = radial nach aussen; Deckel), Kugel (Pole und Streifen) – alle im
  Gegenuhrzeigersinn von aussen. `Orientieren`: Volumen relativ zum ersten Punkt des Stücks, Kanten mit mehr als zwei Dreiecken werden nicht überquert.
- **GLB**: Kopf, Auffüllen auf 4 Byte, `UNSIGNED_SHORT` bis 65 535 Ecken, min/max, sRGB → linear wie three.
- **Schnittraum**: A (0,0) → B (10,0), Blick links: N = (0, 1) (Norden), rechts vom Betrachter N × z = (1, 0) = D – u wächst nach rechts, Bild nicht gespiegelt;
  Blick rechts tauscht A und B. Das u-v-w-System ist linkshändig, die Normalen der Netze werden deshalb im Modell gerechnet (`SchnittNetz.cs`) – richtig.
- **Schnittfläche mit Löchern**: Prisma über gerade/ungerade Kreuzungen der Grundrisslinie mit w = 0; Netz über verkettete Schnittzüge und Clipper
  (EvenOdd) – Löcher bleiben Löcher. Ecken genau auf der Ebene zählen einheitlich zur Seite w ≥ 0.
- **Verdeckung**: Zylinder (Strahl (u, v, t), quadratisch quer zur Achse, axial begrenzt), Kugel, ebene Fläche (linear in t) nachgerechnet.
- **3D-Schnittbereich**: Achsen u = (cos, 0, −sin), v = (−sin, 0, −cos) in three; Ebenen mit Normale nach innen, 0.5 mm nach aussen gesetzt;
  `achsenParameter`: Achse P = 0, d = (1,0,0), Strahl O = (2,5,0), e = (0,−1,0) → t = 2 (richtig).
- **Torus-Bögen**: kommen als Körperteil «koerper» (Netz) aus dem Kern (familiengeometrie.md); 3D und XR mitteln die Normalen unter 20° Knick gleich.

## 3. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 3.1 | `js/ansichten/koerper3d.js:21` (`vek`), `netzdatei.js:166`, `huelle3d.js:52` | Die 3D-Ansicht schreibt absolute Koordinaten in Metern als float32 in die Geometrien, ohne Bezugspunkt. Der XR-Export hat dafür `XrKoordinaten.Bezugspunkt` (ab 5 km je Achse). Handbeispiel: x = 2 600 000 m (Landeskoordinaten LV95) → Abstand zweier float32-Werte 0.25 m; bei 5 km 0.5 mm. | Ein Modell in Landeskoordinaten erscheint im 3D gezackt bzw. flimmert; Treffer und Schnittbereich ungenau. Bei Modellen nahe am Nullpunkt kein Problem. | Gleicher Bezugspunkt wie XR: Gruppe `modell` verschieben, Geometrien relativ dazu bauen (auch Netze, Hülle, Schnittbereich, Blickpunkte, Begehen). | mittel–gross | mittel (hoch, sobald ein Projekt in Landeskoordinaten geführt wird) |
| 3.2 | `js/ansichten/ansicht3d.js:468` | Nahe Schnittebene der Kamera = Einpass-Abstand / 500, nur beim Einpassen gesetzt. Gebäude r = 30 m: 0.16 m (gut). Areal r = 500 m: Abstand 1370 m → 2.7 m; ein verirrtes Element in 100 km Entfernung: rund 540 m. | Beim Hineinzoomen wird alles näher als dieser Wert weggeschnitten (Räume «öffnen» sich, Leitungen verschwinden vor der Kamera). | Nahe Ebene beim Zeichnen aus dem Abstand Kamera–Ziel nachführen (z. B. Abstand / 100, mindestens 0.02 m, höchstens der heutige Wert); ferne Ebene bleibt. | klein | mittel |
| 3.3 | `js/ansichten/ansicht3d.js:354–366` (`elementNetz`) | IFC 1:1: je übernommenem Element wird die ganze Netzdatei erneut durchlaufen (`leseNetz(b, { ids })` liest alle Produktköpfe) – Elemente × Produkte. Der Puffer der Datei bleibt bis zum Schliessen der Ansicht im Speicher. | Bei grossen IFC (Zehntausende Produkte) langsamer Aufbau im Modus «IFC 1:1». | Einmal je Datei ein Verzeichnis GlobalId → Lage in der Datei aufbauen und je Element nur dessen Abschnitt lesen. | mittel | mittel |
| 3.4 | `js/ansichten/ansicht3d.js:416–431` (`holenUndEinsetzen`) | Wird «alles» geholt (jede Änderung an Wand, Tür, Fenster, Decke, Dach, Geschoss, Familie), werden alle Körper neu gebaut, auch die unveränderten. Ergänzt grafik.md 4.8 (ein Netz je Körperteil). | Kurzer Hänger nach jeder Architekturänderung bei grossen Modellen. | Körper-JSON mit dem gespeicherten vergleichen und unveränderte Körper stehen lassen (ausser die Id steht im Ereignis). | mittel | mittel |
| 3.5 | `js/ansichten/koerper3d.js:185` | Normalen je Ecke quadratisch in der Zahl der Dreiecke an einem Punkt. `XrNetzbau` hat dafür `ValenzGrenze` 64 (Befund Test_praxis, 30 s). | Nur bei Familienkörpern mit Fächern/Spitzen aus sehr vielen Dreiecken. | Gleiche Gruppierung wie im Kern ab 64 Dreiecken je Punkt. | klein | tief |
| 3.6 | `Grafik/SchnittNetz.cs:157–171` (`Schnittzuege`) | Bei einem nicht geschlossenen Netz läuft die Verkettung vom Startpunkt nur in eine Richtung; die beiden Hälften eines offenen Zugs werden als zwei geschlossene Züge behandelt. | Falsche Schnittfläche nur bei offenen Netzen (laut Typ «geschlossen»; importierte Familien könnten offen sein). | Offene Züge erkennen (Endpunkt mit einem Nachbarn) und weglassen oder als Linie zeichnen. | klein | tief |
| 3.7 | `Xr/XrNetzbau.cs:167` | Scheitert die Triangulierung eines Prismas, entsteht es ohne Deckel und Boden – ohne Hinweis in der GLB. | Offener Körper in der Brille, Ursache nicht sichtbar. | Hinweis «Deckel nicht trianguliert» in die Hinweise der Szene. | klein | tief |
| 3.8 | `Xr/XrModell.cs:213–221` | Elemente ohne Geschoss werden nach ihrer Unterkante zugeordnet; eine Decke, die unter der Geschosskote hängt, landet im Geschoss darunter. | Betrifft nur Elemente ohne Geschoss; in der Brille im falschen Geschoss-Paket. | Für Ebene «decke» die Oberkante nehmen. | klein | tief |
| 3.9 | `js/ansichten/ansicht3d.js:602–615` (`koerperTreffer`) | Leitungen, Formteile, Armaturen und Apparate gewinnen den Klick vor Wänden und Decken – unabhängig vom Abstand, auch hinter einer undurchsichtigen Decke oder weit hinten im Gebäude. | Klick auf eine Decke wählt die Leitung zwei Geschosse darunter. | Vorrang nur innerhalb eines Abstands hinter dem vordersten Treffer (z. B. Bauteildicke) oder nur durch durchscheinende Körper – siehe Frage 1. | klein | mittel |
| 3.10 | `Grafik/SchnittRaum.cs:9` | Abgesetzte Schnittlinien (mehr als zwei Punkte) gelten als gerade Linie vom ersten zum letzten Punkt (dokumentierte Annahme). | Ein geknickt gezeichneter Schnitt zeigt etwas anderes als gezeichnet, ohne Meldung. | Hinweis an der Ansicht oder beim Zeichnen nur zwei Punkte zulassen. | klein | tief |

## 4. Offene Fragen an Christian

1. **Klick im 3D** (3.9): Soll eine Leitung auch dann gewählt werden, wenn sie hinter einer Decke oder weit hinter der angeklickten Wand liegt? Heute ja,
   ohne Grenze.
2. **Landeskoordinaten** (3.1): Werden Projekte in KICAD je in Landeskoordinaten (Millionen Meter) geführt, oder liegt das Modell immer nahe am
   Projekt-Nullpunkt (IFC-Ausrichtung)? Davon hängt ab, ob die 3D-Ansicht einen Bezugspunkt wie der XR-Export braucht.
3. **Abgesetzte Schnitte** (3.10): Sollen geknickte Schnittlinien einmal echt abgesetzt geschnitten werden, oder genügt die gerade Linie mit Hinweis?
