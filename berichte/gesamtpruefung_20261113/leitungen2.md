# Gesamtprüfung 13.11.2026 – Bereich Leitungen 2 (Modul 3, Runde 2)

Geprüft (vollständig gelesen): Kern `src/Hlks.Kern/Leitungen/` – Leitungsserie.cs, Verlegung.cs, Leitungspruefungen.cs, LeitungenNachfuehren.cs,
ParalleleLeitungen.cs, Fallleitung.cs, FormteilTauschen.cs, FreieGroesse.cs, Verbindungen.cs; dazu die benutzten Stellen in EckeDehnen.cs
(`Stutzen.Klick/Bleibt`, `EckeBilden`), `Api/Nachfuehrungen.cs` (Rundenzahl), `Grafik/HlksKoerper.cs` (Kanal senkrecht, `querDrehung`).
Oberfläche: `werkzeuge/leitung-bearbeiten.js`, `leitung-palette.js`, `leitung-frei.js`, `steigleitung.js`, `parallel.js`,
`oberflaeche/aktionen-leitung.js`. **Nur überflogen:** LeitungsserieAbfrage.cs (Kopf, Aufbau und die Abfrage `abzweigwinkel` gelesen; die
Reduktionsregel mit Stichproben nicht nachgerechnet). Nichts im Browser geprüft (keine Browserreihen, kein Server). Runde 1
(`leitungen.md`) nicht wiederholt, ihre offenen Fragen nicht entschieden.

## 1. Kurzfazit

Die Rechnungen stimmen grösstenteils (Gehrung der Parallelen, Versatz beim Nachführen, freie Grösse mit Grenzen/Schritt/Rundung, Zoll-Vergleich,
Steigzone). Zwei echte Geometriefehler sind korrigiert: **Parallele Leitungen** entstanden an einem Zug mit Knick auf der falschen Seite, wenn nicht
das erste Stück angeklickt wurde, und **eckige Kanäle einer Steigleitung** standen bei einer Reihe, die nicht in x läuft, quer zur Steigzone
(anders als der Entwurf der Oberfläche). Dazu zwei kleine Korrekturen (Dämmdialog, Meldung Bogenwinkel). Wichtigste offene Punkte: Parallelen
«oben/unten» an Zügen mit senkrechtem Stück liegen im Steigstück deckungsgleich auf dem Original; beim Nachführen kann eine Gerade in Schritten
unter 1° beliebig schief werden; die Nachführung bricht nach 5 Runden ohne Meldung ab.

## 2. Korrigiert

| Nr. | Datei | Fehler | Korrektur | Test (Filter, Ergebnis) | Commit |
|---|---|---|---|---|---|
| 1 | `Leitungen/ParalleleLeitungen.cs` (Waagrecht) | Die Seite wurde gegenüber dem **ersten Stück des Zugs** bestimmt (`(seite − pkt[0]) · n[0]`), nicht gegenüber der angeklickten Geraden. Handbeispiel L-Zug (0,0) → (1000,0) → (1000,1000), Klick aussen neben dem zweiten Stück bei (1300, 500): gegenüber RO-1 ist y = 500 > 0 → «links» → Parallele **innen** bei (0,200)–(800,200)–(800,1000) statt aussen. Die Vorschau der Oberfläche zeigt dagegen die Seite der angeklickten Geraden. | Stück des Zugs suchen, auf dem die Mitte der angeklickten Leitung liegt, und die Seite dort bestimmen (die Normalen aller Stücke zeigen einheitlich nach links, das Vorzeichen gilt für den ganzen Zug). | neu `Waagrecht_LZug_KlickAmZweitenStueck_SeiteGegenueberDerAngeklicktenGeraden`; `FullyQualifiedName~ParalleleLeitungenTests` 4/4 | f02c3c0 |
| 2 | `Leitungen/Fallleitung.cs` (SteigleitungPlatzieren) | Eckige Kanäle: `Breite()` und der Entwurf (`steigleitung.js`) rechnen mit b **entlang der Reihe**; gezeichnet wurde der senkrechte Kanal aber ohne `querDrehung`, also immer mit b in x (HlksKoerper.Kanal, Kollisionskörper). Bei `richtung` ≠ x (z. B. Reihe entlang einer Wand in y) stand der Kanal 90° verdreht, die lichten Abstände der Steigzone stimmten nicht. | Nach dem Zeichnen `querDrehung = atan2(uy, ux)` an den eckigen Kanälen setzen (ab 0.5°, wie der IFC-Import). | Zusicherungen in `Steigleitung_Lueftung_EckigUndRund_RichtungNachZentrale` (Reihe in y → 90, Reihe in x → keine); `FullyQualifiedName~FallleitungTests` 5/5 | d467599 |
| 3 | `js/oberflaeche/aktionen-leitung.js` (Dämmdialog) | Die Prüfung liess 0 < Dicke < 0.5 zu; gespeichert wird gerundet → `daemmdicke: 0`, gemeldet «Dämmung … entfernt», die Dämmart blieb gesetzt. Meldung nannte die Obergrenze 500 nicht. | Prüfung auf gerundet ≥ 1 und ≤ 500, Text «Dämmdicke in ganzen mm eingeben (1 bis 500).» | kein Test (Dialog läuft nur im Browser), `node --check` | 3b36725 |
| 4 | `Leitungen/Leitungsserie.cs` (FormteilEigen, Bogen) | Reihenfolge: zuerst `WinkelErlaubt`, dann der Bereich. Bei Winkelregel «frei» kam für 0° bzw. > 180° «Ein Bogen mit 0° ist … nicht erlaubt (erlaubt: frei)»; die Bereichsprüfung dahinter war nie erreichbar. | Bereich 0 < w ≤ 180 zuerst. | `FullyQualifiedName~LeitungsserieTests` 24/24 (kein neuer Test – reine Meldung) | 5c8336c |

Kein Test war vor meinen Änderungen rot.

## 3. Geometrie – nachgerechnet

| Stelle | Handbeispiel | Ergebnis |
|---|---|---|
| Parallele, Gehrung `pkt + (a + b) · d / (1 + a·b)` | Knick 90°: a = (0,−1)·s, b = (1,0)·s → \|a + b\| = √2, 1 + a·b = 1 → Versatz √2·d auf der Winkelhalbierenden = d / cos 45° ✓; Knick 0°: (2a)·d/2 = a·d ✓; Steigbogen im Grundriss kollinear: wie 0° ✓ | richtig; Umkehr (a·b = −1) wird abgelehnt |
| Parallele, Seite | siehe 2.1 | **falsch → korrigiert** |
| Parallele «oben/unten» | Zug waagrecht → senkrecht → waagrecht, alle Knoten + (0,0,d): das senkrechte Stück bleibt auf derselben Achse | **falsch**, siehe 4.1 |
| Nachführen, Versatz (`LeitungenNachfuehren.Versatz`) | Gerade fest (0,0) → (2000,0), Ende 200 quer verschoben, 45°: schraeg = 200/tan 45° = 200, rest = (2000 − 200)/2 = 900; a = (900,0), b = (1100,200), Ende (2000,200); Winkel der Schräge atan(200/200) = 45° ✓, letztes Stück 900 ✓; 90°: schraeg = 0 ✓ | richtig (Einbaulängen prüft danach `Routing.Planen`) |
| Nachführen, Schwelle «gedreht» | Gerade 3000 mm, Gerät 50 mm quer: atan(50/3000) = 0.955° < 1° → kein Versatz, die Gerade wird schief | siehe 4.2 |
| Formteil tauschen, Bogen → T | Bogen 90° bei (1000,0), La = Lb = 50: x (0,0)–(950,0) wird bis (1300,0) verlängert (Stummel 300), y dehnt an x → T bei (1000,0), Hilfsstück nach dem T entfällt, b offen ✓; Abzweigwinkel = Umlenkwinkel (y läuft weg: gespiegeltes T, c = cos w·d + sin w·s) ✓ | richtig; Stummel siehe 4.6 |
| Formteil tauschen, T → Bogen an senkrechter Leitung | `bleibt_a/b` nur [x, y]: an einer senkrechten Geraden liefert `Stutzen.Klick` null → es bleibt das fernere Ende ✓ | richtig |
| Fallleitung/Steigleitung, Höhen | Geschosse −3000/0/3000, Höhe 3000: Rohre −3000…0, 0…3000, 3000…6000 (oberstes: Kote + Höhe); abwärts von oben gezeichnet, `von` = tiefstes Rohr davor ✓ | richtig |
| Steigzone | 2 Rohre da 25 in 300 mm: licht = (300 − 50)/3 = 83.33; Achsen bei 95.83 und 204.17 ✓; quer = Tiefe/2 nach links (−uy, ux) ✓ wie der Entwurf | richtig (Dämmung nicht eingerechnet, siehe 4.8) |
| Freie Grösse `Gerundet` | Schritt 5, Grenzen 145…2500: 143 → 145; 147.5 → 150 (weg von 0); 121 → 120 → begrenzt 145; 2503 → 2500 ✓ | richtig |
| `ImSchritt` | \|x/s − rund(x/s)\|·s ≤ 1e-6: 430 bei Schritt 5 ✓, 432 ✗ | richtig |
| Zoll-Vergleich | «Rp 1 1/4», «G1-1/4», «1 1/4"» → «1 1/4»; «Rp 3/4» → «3/4» ✓; «5/4"» ≠ «1 1/4» | siehe 4.9 |
| P16 Gefälle | g = −dz / Länge im Grundriss · 100 (wie das Routing); Stücke ≥ 45° und senkrechte ausgenommen ✓ | richtig |
| Verlegung, quer/längs | Platte: \|Richtung.z\| ≥ sin 45° = 0.707 = quer ✓; Vereinigung der Intervalle je Stück ✓; Erdreich in Stücken ≤ 100 mm ✓ | richtig |
| Bogen-/Abzweigwinkel aus der Richtung | b = (cos w, sin w) → atan2 = w; c = (−cos w, sin w) → atan2(y, −x) = w ✓ | richtig |

## 4. Gefunden, nicht korrigiert

1. **Parallele «oben/unten» an Zügen mit senkrechtem oder geneigtem Stück** – `ParalleleLeitungen.cs:56`. Alle Knoten werden nur in z versetzt.
   Ein senkrechtes Stück der Parallele liegt damit **deckungsgleich** auf dem Original (nur länger/kürzer), ein 45°-Stück hat nur
   d · cos 45° = 0.71 · d Abstand. «waagrecht» lehnt senkrechte Stücke ab, «oben/unten» nicht. Vorschlag: Gehrung in 3D (Versatzrichtung je Stück =
   Lot auf die Achse in der senkrechten Ebene, Knoten mit derselben Formel `(m_a + m_b) · d / (1 + m_a·m_b)`), senkrechte Stücke bekommen die
   Richtung des Nachbarstücks – oder vorerst ablehnen. Aufwand mittel, **Priorität hoch** (erzeugt heute still übereinanderliegende Rohre).
2. **Nachführen: schleichende Schieflage** – `LeitungenNachfuehren.cs:105–110`. «Gedreht» wird gegen die **bisherige Richtung der Geraden**
   gemessen, nicht gegen die Achse des Anschlusses. Eine Gerade von 3 m nimmt 50 mm Querversatz als 0.955° Schieflage auf (10 m: bis 174 mm) – ohne
   Versatz, nur mit Hinweis. Mehrere kleine Schritte summieren sich unbegrenzt (5 × 40 mm an 3 m = 3.8°), weil jeder Schritt wieder von der
   schon schiefen Lage aus misst; erst die Prüfung (P13/P15) meldet es. Vorschlag: gegen die Richtung des Anschlusses am festen Ende messen
   oder den Querversatz in mm begrenzen (z. B. > 2 mm → Versatz bzw. ganze Gerade folgt). Achtung Abwasser 2 % (Runde 1, Punkt 4.1) – beide
   Toleranzen zusammen entscheiden. Aufwand mittel, **Priorität hoch**.
3. **Nachführung endet nach 5 Runden ohne Meldung** – `Api/Nachfuehrungen.cs:27–40`. «Formteile folgen einander» braucht je Glied eine Runde. Hängen
   mehr als fünf Formteile/Armaturen direkt aneinander (Reduktion – Ventil – Schmutzfänger – Regulierventil – Bogen – …), folgt das nächste Glied
   bzw. die Leitung dahinter nicht mehr; das Netz reisst ohne Hinweis (erst P13). Vorschlag: Hinweis bzw. Ablehnung, wenn nach der letzten Runde
   noch Änderungen offen sind, oder Ketten in einer Runde abarbeiten. Aufwand klein, Priorität mittel. (Datei ausserhalb meines Bereichs.)
4. **Dämmdialog bei Mehrfachauswahl** – `aktionen-leitung.js:84–122`. Die vorgeschlagene Dicke kommt aus der Regel mit der DN der **ersten**
   Leitung und wird auf alle gewählten Leitungen geschrieben – bei gemischten DN bekommen die anderen eine Dicke, die nicht zur Regel passt (der Text
   «Regel: …, DN 25 → 40 mm» nennt nur die erste). DN wird aus `dn` bzw. «DN xx» im Typnamen gelesen; bei Typen ohne DN im Namen (Abwasser «d 110»,
   Pressrohr «22 × 1», Kanäle) ist DN 0 → kein Vorschlag, ohne Erklärung. Vorschlag: Wahl «Dicke nach Regel je Leitung» (Kern rechnet je DN) und
   Hinweis bei gemischten Grössen. Aufwand mittel, Priorität mittel. Die Regelwerte selbst (MuKEn) habe ich nicht geprüft.
5. **Fallleitungs-/Steigleitungsmeldungen und Begriffe** – `leitung-palette.js:62,63,125,127`, `leitung-bearbeiten.js:77`. Tooltips und eine Meldung
   sagen noch «Leitungsserie», die Felder heissen «Produktlinie». `leitung-palette.js:125` hat eine tote Auswahl (`art === 'Kanal' ? 'Leitung' :
   'Leitung'`), Zeile 110 ein doppeltes `;`. Nicht geändert, weil `leitung.browser.mjs:202` den Text «Keine Leitungsserie der Kategorie» erwartet
   (gleicher Wortlaut an anderer Stelle) – einheitlich mit der Browserreihe umstellen. Aufwand klein, Priorität tief.
6. **Bogen → T-Stück: Hilfsverlängerung bei grossen Kanälen** – `FormteilTauschen.cs:37–38`. Stummel = max(300, 3 × aussen); `Aussen` ist bei
   eckigen Kanälen vermutlich leer → 300 mm. Ist die Einbaulänge b des T-Stücks grösser (grosse Kanäle), lehnt `dehnen_bis` mit «zu kurz» ab, obwohl
   das Stück danach ohnehin entfällt. Nicht nachgestellt (keine Testfamilie). Vorschlag: `max(aussen, d, b, h)` bzw. Einbaulänge aus der Formteilwahl.
   Dazu `neu.Single(…)`/`neu.First(…)` (Zeilen 49–50): setzt `dehnen_bis` einmal mehr als ein Rohr (z. B. Reduktion am Abzweig), gibt es eine
   Ausnahme ohne deutsche Meldung. Aufwand klein, Priorität tief.
7. **Parallele: Meldung bei anderem Formteil im Zug** – `ParalleleLeitungen.cs:108`. Liegt z. B. eine Armatur oder Reinigungsöffnung im Zug,
   lautet die Meldung «… parallele Züge gibt es nur in einer Grösse» (stimmt nur für die Reduktion). Vorschlag: eigener Text «… im Zug liegt ein
   Formteil, das nicht mitkopiert wird». Aufwand klein, Priorität tief.
8. **Steigzone ohne Dämmung** – `Fallleitung.cs:142–147` (Breite). «Gleiche lichte Abstände» rechnet mit dem Aussendurchmesser des Rohrs; die
   Dämmung (bei KW/WW/Heizung die Regel) ist nicht eingerechnet, die lichten Abstände zwischen den gedämmten Rohren sind also kleiner bzw. negativ.
   Vorschlag: Dämmdicke des Systems (bzw. Dämmregel) zur Breite zählen. Entscheid Christian nötig (welche Dicke gilt beim Platzieren). Aufwand
   klein bis mittel, Priorität mittel.
9. **Zoll-Schreibweisen** – `Verbindungen.cs:40–50`. «5/4"» und «1 1/4"» bzw. «6/4"» und «1 1/2"» gelten als verschieden (kein Kürzen/Umrechnen der
   Brüche); «1.5"» wird zu «1». Auswirkung: Übergang wird nicht gefunden («kein Übergang passt»), nie ein falscher gesetzt. Vorschlag: Bruch in eine
   Zahl umrechnen und vergleichen. Aufwand klein, Priorität tief.
10. **Fallleitung endet an der Oberkante des obersten Geschosses** – `Fallleitung.cs:43`. Keine Entlüftung über Dach, kein Stück unter dem
    untersten Geschoss (Übergang in die Grundleitung). Kein Fehler, aber im Werkzeug nicht wählbar. Aufwand klein, Priorität tief (Wunsch).
11. **Verlegung: Bodenplatte nur unter dem untersten Geschoss** – `Verlegung.cs:407–412`. Eine Bodenplatte auf Erdreich in einem höheren
    Geschoss (teilunterkellertes Gebäude) gilt als «decke»; Leitungen darunter sind dann «frei» statt «erdreich» (ausser sie liegen tiefer als
    alle Bodenplatten). Vorschlag: Hand-Kennzeichen «Bodenplatte» am Bauteil oder Erkennung «kein Raum darunter». Aufwand mittel, Priorität tief.
12. **Leistung** – `Verlegung.Signatur` (`Verlegung.cs:241–263`) serialisiert bei jedem Modellstand alle Bauteile, Geschosse und Unterlagen als
    JSON und fragt die Netzdateien ab; `Senkrechte.Zeichnen` (`Fallleitung.cs:54–56`) baut je Geschoss und Leitung die Menge aller Element-Ids neu
    (statt die neuen Elemente aus `k.Operationen` zu lesen wie `ParalleleLeitungen`). Bei heutigen Modellgrössen unkritisch. Priorität tief.
13. **Neigung-Dialog** – `aktionen-leitung.js:148`. Ein leeres Feld gilt als 0 (waagrecht); die Palette behandelt «leer» dagegen als «Angabe
    entfernen, Lage bleibt». Vorschlag: leer ablehnen oder gleich behandeln. Aufwand klein, Priorität tief.

## 5. Offene Fragen an Christian

1. Parallele «oben/unten» an Zügen mit Steigstück (4.1): echte Parallele im Raum rechnen (Steigstück seitlich versetzt – in welche Richtung?)
   oder solche Züge ablehnen und «Nur Gerade» verlangen?
2. Nachführen (4.2): Ab welchem Querversatz soll statt «schief ziehen» ein Versatz mit zwei Bögen entstehen – fester Wert in mm, oder nie schief
   (jede Querbewegung ergibt einen Versatz bzw. die Gerade folgt ganz)?
3. Steigzone (4.8): Sollen die lichten Abstände zwischen den **gedämmten** Rohren gleich sein (Dämmdicke aus der Dämmregel des Systems)?
4. Dämmung bei Mehrfachauswahl (4.4): eine Dicke für alle (wie Revit) oder je Leitung nach Regel und DN?
5. Steigleitung eckig (2.2): Die Breite b liegt jetzt entlang der Reihe. Soll im Werkzeug wählbar sein, ob b oder h entlang der Reihe steht?
