# Gesamtprüfung 13.11.2026 – Bereich Fachwerkzeuge (Runde 4)

Zweig `worktree-agent-a7158b4c3272c46dc`, Ausgangsstand `4ea7b00`. Geprüft: die Werkzeuge zum Platzieren und Ausrüsten – Geberit Vorwand
(`src/Hlks.Kern/Vorwand/`, `js/werkzeuge/vorwand*.js`, `oberflaeche/aktionen-vorwand.js`), Familie platzieren (`FamiliePlatzieren`,
`Familien.Einbaulage`, `Leitungen/Anschlusslagen.cs`, `js/familien/platzieren.js`, `familien-ansicht.js`, `typauswahl.js`), Höhenbezug beim Anlegen
(`Elementpflege.Geometrie/Zuordnen`), Kanalrahmen (`Grafik/KanalRahmen.cs` mit `HlksGrafik.RahmenPlan`, `HlksKoerper.Rahmen`), Lüftungsvariante
übernehmen und Raumforderung (`Rechnen/LueftungBefehle.cs`, nur die Lage), Sanitär-Zusammenstellung öffnen (`aktionen-sanitaer.js`,
`sanitaer-anlage.js`), Heizgruppen-Befehle (`Heizung/HeizungBefehle.cs`, quergelesen). Nicht erneut geprüft: Rechenkerne, Leitungen, Fall- und
Steigleitung, Verdeckung (Runden 1–3).

**Befund zum Umfang:** Eigene Fachwerkzeuge mit eigener Geometrie gibt es heute nur für die **Vorwand** (Vorwand, Montageelement, Bestücken,
Stückliste). Sanitärapparate, Armaturen, Heizkörper, Verteiler, Durchlässe und Lüftungsgeräte entstehen alle über dasselbe Werkzeug «Familie
platzieren» (Punkt + Drehung um z) bzw. `leitung_einsetzen`; ein Ausrichten an Wänden oder ein Höhenbezug (OKFB/UKD) ist dort nicht gebaut
(Punkt 3.1). MSRL hat kein Platzierwerkzeug (Datenpunkte hängen logisch am Element).

## 1. Kurzfazit

Die Vorwand ist geometrisch sauber aufgebaut: örtliches System (s, q, z), Seitenregel links/rechts, Aussenkante und Tiefe am Plan stimmen zwischen
Oberfläche und Kern überein, die Handbeispiele gehen auf. Drei echte Fehler sind korrigiert: eine sehr kurze Vorwand liess jede Ansicht abbrechen,
Spiegeln drehte die Rohbauwandseite auf die Raumseite, und Kanalrahmen überlappten am Kanalende; dazu stimmt die Vorschau des Montageelements jetzt
mit dem Ergebnis überein. Offen sind vor allem drei Dinge: **Montageelemente haben keine räumlichen Anschlüsse** (Leitungen lassen sich nur logisch
anschliessen, die Anschlusshöhen aus dem Katalog werden nur gezeichnet), **Vorwand und Montageelemente hängen beim Verschieben/Drehen nicht
zusammen**, und das Platzieren von Apparaten, Heizkörpern und Durchlässen kennt **weder Wandbezug noch Höhenbezug**.

## 2. Korrigiert

Tests gefiltert: `dotnet test tests/Hlks.Kern.Tests --filter "FullyQualifiedName~VorwandTests|FullyQualifiedName~KanalRahmenTests"` → **17 bestanden,
0 Fehler**; `node --test js/werkzeuge/werkzeuge.test.mjs` → **52 bestanden, 1 übersprungen (wie vorher), 0 Fehler**. Die Fehler 1 und 2 wurden vor
der Korrektur mit einem Wegwerftest nachgewiesen (Ausnahme bzw. «rueckseite rechts» nach dem Spiegeln).

### Commit 82fa884 – Vorwand: kurze Vorwand, Spiegeln

1. **`Vorwand/VorwandGestell.cs` (Ausweichen) – sehr kurze Vorwand bricht jede Ansicht ab.** `vorwand_zeichnen` nimmt jede Länge ab 1 mm an. Bei GIS
   kürzer als 2 × 15 + 32 = 62 mm bzw. Duofix kürzer als der Ständer (50 mm) rief `Ausweichen` `Math.Clamp(s, min, max)` mit min > max auf →
   `ArgumentException` («'15' cannot be greater than 3»). `Grafik.Plan` fängt das nicht: Grundriss, 3D und `vorwand_stueckliste` des ganzen
   Geschosses brachen ab (zwei nahe Klicks oder ein mit dem Griff zusammengeschobenes Ende genügen). Korrektur: Grenze geklemmt, Befund «Vorwand …
   mm ist kürzer als ein Profil … – kein baubares Gestell»; Montagewinkel bzw. Schienenbefestigung kurzer Vorwände (< 200 mm) liegen in der Mitte
   statt 100 mm ausserhalb. Für Vorwände ab 200 mm ändert sich nichts. Test `VorwandTests.SehrKurzeVorwand_BefundStattAbsturz` (GIS 50 mm, Duofix 30 mm).
2. **`Architektur/Bearbeiten.cs` (ElementeSpiegeln) – gespiegelte Vorwand steht verkehrt.** `rueckseite` zählt links/rechts in Zeichenrichtung; beim
   Spiegeln bleibt die Punktreihenfolge, der Drehsinn kehrt um. Handbeispiel: Vorwand (100,100)→(2100,100), Rückseite rechts = Wandfläche y = 100,
   Vorderseite y = 300. Gespiegelt an x = 0: Achse (−100,200)→(−2100,200), Richtung −x; «rechts» davon ist jetzt +y → Rückseite y = 300 (im Raum),
   Vorderseite y = 100 (in der Wand). Korrektur: `rueckseite` wird beim Spiegeln umgestellt (auch bei «kopieren»). Test
   `VorwandTests.Spiegeln_RueckseiteBleibtAnDerRohbauwand`.

### Commit 5f4f310 – Kanalrahmen

3. **`Grafik/KanalRahmen.cs` (Lagen) – Stoss- und Endrahmen überlappen.** Bedingung `s < l − t − 1` liess einen Stoss zu, dessen Doppelprofil
   (s − t … s + t) in den Endrahmen (l − t … l) ragt. Handbeispiel M20 (t = 20, Normkanal 1770), Kanal 1800 mm: Stoss 1750–1790 und Endrahmen
   1780–1800, 10 mm Überlappung, dazu ein «Kanalstück» von 30 mm. Jetzt `s < l − 2t − 1`: der Stoss entfällt, wenn sein Profil nicht vor dem
   Endrahmen Platz hat (Grundriss und 3D). Test `KanalRahmenTests.Rahmen_UeberlappenNichtAmKanalende` (1800 → 2 Rahmen, 2000 → 3).

### Commit e2b8526 – Vorschau Montageelement

4. **`js/werkzeuge/vorwand.js`, `vorwand-typen.js` – Vorschau wich vom Ergebnis ab.** Die Vorschau zeichnete fest ±250 mm um den Mauspunkt. Der
   Kern setzt die Breite des Typs (WC 424 bzw. 500 mm, andere Typen anders) und begrenzt die Mitte auf B/2 … L − B/2. Neu `elementVorschau(vw, breite)`
   mit derselben Begrenzung und `elementBreite(id)` aus den geladenen Elementtypen. Test in `werkzeuge.test.mjs` («Montageelement: die Vorschau …»).

Dazu `src/Hlks.Web/OBERFLAECHE.md` (Abschnitt Geberit Vorwand) um drei Zeilen nachgeführt (im Commit des Berichts).

## 3. Handbeispiele (nachgerechnet, in Ordnung)

| Stelle | Eingabe | von Hand | Code |
|---|---|---|---|
| `VorwandZeichnen` + `WandZeichnen.Versetzen` | Punkte (100,100)→(2100,100), bezugslinie rechts, Tiefe 200 | Normale links = (0,1), q = +100 → Achse y = 200; Rückseite rechts: Q = (−u_y, u_x) = (0,1) zeigt zur Vorderseite → Rückseite y = 100, Vorderseite y = 300 | gleich (Test Gis_ProfileNachHandbuch: Achse 200, ME auf 300) |
| Aussenkante | Punkte y = 300, aussenkante rechts, Tiefe 200 | q = −100 → Achse y = 200, rueckseite = rechts | gleich |
| `tiefeAmPlan` (JS) | Kante (0,0)→(3000,0), Klick (1200,−220) | Kreuzprodukt (3000·(−220) − 0)/3000 = −220 < 0 → rechts, Tiefe 220 | gleich; `S.band` versetzt die Vorschau auf dieselbe Seite wie der Kern |
| `VorwandAufbau.Punkt/Oertlich` | s = 1000, q = Tiefe | A + u·s + Q·(q − T/2) → (1100, 300); Rückrechnung s = 1000, q = 200 | gleich, beide Richtungen zueinander invers |
| GIS-Horizontalprofile | Höhe 1140, Bodenaufbau 100, Beplankung 18, Profil 32 | Rohboden −100 → Oberkanten −50, 550, (1200, 1250 über der Ablage entfallen), oben 1140 − 18 = 1122 | gleich |
| Familie: Drehung | Punkt (0,−40) (Vorderseite −y), drehung 90° | x′ = 0·cos − (−40)·sin = 40, y′ = 0 → Vorderseite nach +x | `Einbaulage.Gedreht` und `weltPunkt` (JS) rechnen dieselbe Matrix; Vorschau = Ergebnis |
| Segment durch die Tür | Segment L × 1104 × 182, Tür 800 × 2000 | 182 ≤ 800, 1104 ≤ 2000 → Länge frei; Höhe > 2000 → L ≤ 2000 | gleich (`Segmentieren`) |

## 4. Gefunden, nicht korrigiert

1. **Platzieren ohne Wand- und Höhenbezug – `js/familien/platzieren.js:83–99`, `typauswahl.js:66` (platzierParameter), `Api/Befehle.cs:300`.**
   Das Werkzeug sendet nur [x, y] und die Drehung der Optionsleiste (Leertaste 90°); z wird immer die Geschosskote. Es gibt kein Ausrichten an
   einer Wand (Drehung aus der Wandrichtung, Rückseite auf die Wandfläche bzw. Vorwand-Vorderseite, Abstand) und keine Höhe über OKFB bzw. unter
   UKD. Heizkörper, Waschtische, Durchlässe an der Decke und alles an schrägen Wänden muss danach von Hand gedreht und in z verschoben werden
   (bzw. über einen Exemplarparameter der Familie, falls sie einen hat). Vorschlag: Option «an Wand» (Fang auf Wand- bzw. Vorwandfläche setzt
   Drehung und Punkt) und Feld «Höhe» mit Bezug OKFB / UKD (UK Rohdecke gibt es am Geschoss schon: `ukRohdecke`). Aufwand mittel, **Priorität hoch**
   – das ist der grösste Hebel für alle drei Gewerke.
2. **Montageelemente haben keine Anschlusslage – `Leitungen/Anschlusslagen.cs:40–41`, `Vorwand/VorwandGrafik.cs:76, 121–128`.** Ein Montageelement
   hat keine Familie → `Anschlusslagen.Von` liefert Lage und Richtung null («ohne_familie»). Die Anschlüsse aus dem Katalog (s, z, Medium; WC
   Abwasser 230 mm über OKFF) werden nur gezeichnet (Stutzen in 3D, Fangpunkt im Grundriss ohne Höhe). Folge: eine Leitung kann am Montageelement
   nicht räumlich beginnen bzw. enden (kein Übernehmen von Höhe/Dimension/Medium wie an Familienanschlüssen, P13–P15 prüfen nichts), nur logisch
   verbunden werden. Vorschlag: `Anschlusslagen.Von` um den Fall Montageelement erweitern (Lage aus `VorwandAufbau.Punkt(s + a.S, …)`, z = OKFF + a.Z,
   Richtung fachlich festlegen – siehe Frage 2). Aufwand mittel, **Priorität hoch**.
3. **Vorwand und Montageelement hängen beim Bearbeiten nicht zusammen – `Api/Befehle.cs:491`, `Architektur/Bearbeiten.cs`.** Nachgewiesen:
   Vorwand um (300, 1000) verschoben → das Montageelement behält seinen Punkt (700, 300); gezeichnet wird es über die Projektion auf die neue Achse
   (s 600 → 300), der gespeicherte Punkt liegt 900 mm neben der Vorwand (Auswahl, Raumzuordnung, spätere Drehung gehen vom alten Punkt aus). Beim
   Drehen der Vorwand ergibt die Projektion eine zufällige Lage. Löschen der Vorwand wird abgelehnt («wird noch von 1 Element(en) verwendet: ME-1»)
   – man muss zuerst jedes Montageelement einzeln löschen. Die Klassenbeschreibung sagt «Punkt = Mitte der Elementvorderkante». Vorschlag: wie Türen
   an der Wand – Lage des Elements als Abstand s an der Vorwand führen und den Punkt nachführen; gemeinsam gewählte Elemente nicht doppelt bewegen;
   Löschen der Vorwand nimmt ihre Montageelemente mit. Ob das Element beim Verschieben längs der Vorwand mitgeht oder hinter dem Apparat bleibt,
   ist ein Entscheid (Frage 1). Aufwand mittel, Priorität hoch.
4. **Montagewinkel: Abstand nach dem Ausweichen grösser als erlaubt – `VorwandGestell.cs:263` (AusserhalbElemente).** Der Winkel, der unter einem
   Element läge, wird an dessen Kante geschoben, die Lücke aber nicht nachgeteilt. Beispiel aus dem bestehenden Test (Vorwand 2000 mm, WC 788–1212,
   massiv max. 1000 mm): Winkel bei 100, 1233, 1900 → Abstand 100–1233 = **1133 mm > 1000 mm** (S. 251). Der Test schreibt genau diese Lagen fest.
   Vorschlag: nach dem Schieben beidseits des Elements je ein Winkel bzw. die Lücken wie bei `Auffuellen` nachteilen – oder der Winkel darf unter
   dem Element sitzen (das unterste Profil läuft dort durch). Braucht den Blick ins Handbuch (Frage 3). Aufwand klein, Priorität mittel (Stückliste).
5. **Vorgefertigte GIS-Segmente: 30 mm Lücke am Stoss – `VorwandGestell.cs:214, 231`.** Je Segment gilt `s0 = a + 15`, `s1 = b − 15` – der Abzug
   «lichtes Mass minus 3 cm» (Abstand zu den angrenzenden Wänden) wird auch an inneren Stössen angewandt. Beispiel 4000 mm in zwei Segmenten:
   Rahmen 15–1985 und 2015–3985, zwischen den Vertikalprofilen der beiden Segmente 30 mm Luft, Zuschnitt je Segment 1970 statt 1985 mm; die
   «Verbindungslasche» überbrückt die Lücke. Bei einem Doppelstoss stehen die Profile Rücken an Rücken. Vorschlag: Rand nur an den Enden der
   Vorwand, am Stoss 0 (oder das Fugenmass aus dem Handbuch). Aufwand klein, Priorität mittel (Zuschnittlängen der Stückliste) – Frage 4.
6. **Vorwand über Eck (Kette) – `VorwandBefehle.cs:78` (Versetzen mit Gehrung der Achse).** Je gerades Stück entsteht eine rechteckige Vorwand
   zwischen den Gehrungspunkten der Achse. Handbeispiel Aussenkante (0,0)→(2000,0)→(2000,2000), Rohbauwand rechts, Tiefe 200: Achse (0,−100)→
   (2100,−100)→(2100,2000). Vorwand 1 deckt x 0…2100, y −200…0; Vorwand 2 x 2000…2200, y −100…2000. Beide überlappen in 100 × 100 mm, das Eckstück
   x 2100…2200, y −200…−100 an der Rohbauecke fehlt; Vorderseite 1 ist 2100 statt 2000 mm lang, Rückseite 2100 statt 2200. Profile, Beplankung und
   Stückliste sind je Ecke um die halbe Tiefe falsch, Ständer beider Vorwände stehen ineinander. Vorschlag: an Innenecken eine Vorwand bis an die
   Rohbauwand führen, die andere stumpf anschliessen (Regel des Handbuchs für Eckausbildung). Aufwand mittel, Priorität mittel.
7. **Bestücken übersieht Apparate in bzw. hinter der Vorderseite – `VorwandBefehle.cs:182`.** Bedingung `q ≥ Tiefe − 1`. Steht der Apparat an
   der Rohbauwand (üblich, wenn die Architektur zuerst gezeichnet ist und die Vorwand danach kommt: q ≈ 0) oder 2 mm in der Beplankung, heisst es
   «kein Apparat vor der Vorwand». Vorschlag: Apparate ab q ≥ 0 nehmen und im Hinweis nennen, dass sie in der Vorwand stehen (oder auf die
   Vorderseite schieben – Entscheid). Aufwand klein, Priorität mittel.
8. **Vertikalprofil am Vorwandende fällt weg – `VorwandGestell.cs` (Auffuellen, «einen nahen dorthin schieben»).** Liegt eine Elementkante
   weniger als 2 Profilbreiten (64 mm GIS, 80–100 mm Duofix) vom Endprofil, wird das **Endprofil** an die Elementkante geschoben; am Vorwandende
   steht dann keines mehr, die Horizontalprofile enden frei. Beispiel GIS: Element ab s = 87 → Kantenprofil 55, Endprofil 15 → nur noch 55.
   Vorschlag: Endprofile nie verschieben, stattdessen das Kantenprofil weglassen. Aufwand klein, Priorität tief.
9. **Apparate ohne Familie lassen sich nicht drehen – `Grafik/HlksKoerper.cs:88, 103`, `HlksGrafik` (kein «drehung»).** Der Klassenquader
   (darstellung b × t × h) wird immer achsparallel gezeichnet; `elemente_drehen` bewegt nur den Punkt. Mit Familie stimmt es. Priorität tief
   (Apparate kommen in der Praxis aus Familien).
10. **`raumhoch` ohne Angabe – `VorwandGestell.cs:115`.** Ohne Eigenschaft `ausfuehrung` gilt raumhoch erst ab Geschosshöhe − 100 mm (OK–OK).
    Eine Vorwand mit der lichten Höhe (z. B. 2400 bei 2800 Geschosshöhe) über MCP ohne `ausfuehrung` wird teilhoch mit Ablage auf 2.4 m gebaut.
    Die Oberfläche setzt `ausfuehrung` immer. Vorschlag: gegen die lichte Höhe des Raums vergleichen. Aufwand klein, Priorität tief.
11. **`Segmentieren`: Stoss vor dem Vorwandanfang – `VorwandGestell.cs:376–384`.** Wird ein Stoss an die linke Elementkante geschoben und liegt
    das Element am Anfang (S0 < 47 mm), wird s negativ; `grenzen` ist dann nicht mehr geordnet (0, −47, …) und ein Segment beginnt vor der
    Vorwand. Nur bei Einbringlängen unter etwa einer Elementbreite. Aufwand klein, Priorität tief.
12. **Höhe ohne Geschoss – `Api/Befehle.cs:300`.** `Geometrie()` läuft vor `Zuordnen()`: ein Punkt [x, y] ohne Geschoss bekommt z = 0, nicht
    «die Kote des Geschosses»; das Geschoss wird danach aus dieser Höhe 0 abgeleitet (`familie_platzieren` mit raum im OG, ohne geschoss und z →
    Element auf ±0.00 im EG, Raum im OG; aus dem Code gelesen, nicht ausgeführt). Die Oberfläche gibt das Geschoss immer mit. Aufwand klein, Priorität tief.
13. **Kanalrahmen an Formteilen und Knicken – `HlksGrafik.RahmenPlan`, `HlksKoerper.Rahmen`.** Rahmen entstehen je Teilstrecke einer Leitung;
    bei einem Kanal mit mehreren Punkten (Knick ohne Formteil) sitzt an jedem Knick ein Rahmenpaar, die Stossteilung beginnt je Teilstrecke neu.
    Geneigte Kanäle haben in 3D keine Rahmen, im Grundriss schon (projizierte Länge). Kein Fehler im Normalfall (Kanal = gerades Stück zwischen
    Formteilen), nur zur Kenntnis. Priorität tief.
14. **Lüftungsvariante/Raumforderung – `Rechnen/LueftungBefehle.cs:170, 188`.** Schacht und Deckenhohlraum sind achsparallele Rechtecke
    (Schacht b × t um den Punkt, Hohlraum = umschliessendes Rechteck aller versorgten Räume). In einem gedrehten Gebäude bzw. bei L-förmigen
    Raumgruppen ist die Hohlraumfläche deutlich zu gross und der Schacht steht schräg zu den Wänden. Als Forderung an die Architektur brauchbar,
    aber im Bedarfstext nicht als «umschliessendes Rechteck» benannt. Aufwand klein (Text) bis mittel (am Planwinkel ausrichten), Priorität tief.
15. **Heizung.** Kein eigenes Platzier- oder Anschliesswerkzeug: Heizkörper und Verteiler sind Familien, angeschlossen wird mit dem Rohrwerkzeug
    am Familienanschluss; FBH-Kreise sind Rohre einer Serie mit Biegeradius. `HeizungBefehle.cs` (Heizgruppe anlegen/ändern, Systempaar) ist reine
    Netzlogik ohne Geometrie; beim Querlesen kein Fehler gefunden. Die Punkte 1 und 2 der Rechenkern-Prüfung (Massenstrom) bleiben massgebend.

## 5. Offene Fragen an Christian

1. **Vorwand verschieben/drehen:** Sollen die Montageelemente fest zur Vorwand gehören (gehen immer mit, Abstand ab Vorwandanfang bleibt) oder
   beim Verschieben längs der Vorwand hinter ihrem Apparat stehen bleiben? Und: darf «Vorwand löschen» ihre Montageelemente mitlöschen?
2. **Anschlüsse der Montageelemente:** Wo sollen Leitungen räumlich anschliessen – an der Vorderseite (Apparateanschluss, wie heute gezeichnet)
   oder im Element (Abwasserbogen nach unten bzw. hinten, Spülkastenzulauf)? Richtung und Lage je Elementtyp stehen nicht im Katalog; für das WC
   nennt die Montageanleitung nur die Höhe 23 cm.
3. **Montagewinkel unter einem Element:** Darf ein Montagewinkel am durchlaufenden untersten Profil unter dem Element sitzen? Wenn nein, wird der
   Höchstabstand (100 bzw. 40 cm) heute überschritten (Beispiel 113 cm) – dann beidseits des Elements je einen setzen?
4. **Stoss vorgefertigter GIS-Segmente:** stehen die Vertikalprofile Rücken an Rücken (keine Fuge) oder mit Fuge? Heute 30 mm Abstand.
5. **Vorwand über Eck:** welche Vorwand läuft durch, welche schliesst stumpf an (bzw. Regel aus dem Planungshandbuch)?
6. **Platzieren an der Wand und Höhenbezug (Punkt 4.1):** gewünscht als Option im Werkzeug «Familie platzieren» (Fang auf Wand/Vorwand dreht und
   setzt an; Höhe über OKFB bzw. unter UK Decke)? Welche Höhen sollen je Klasse vorgeschlagen werden (nur aus der Familie bzw. von Hand – der
   Kern setzt keine Normhöhen aus dem Gedächtnis)?
7. **Bestücken:** Apparate, die an der Rohbauwand statt vor der Vorwand stehen – mit bestücken, mit Hinweis, oder auf die Vorderseite schieben?

## Hinweis zum Ablauf

Bei jedem Commit meldet Git «failed to delete …/.git/worktrees/wt_b1 / wt_b2: Permission denied» (verwaiste fremde Arbeitskopien); die Commits
sind in Ordnung. Nichts gepusht, keine Marken, kein Server gestartet, keine Browserreihe.
