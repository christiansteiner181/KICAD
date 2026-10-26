# Hlks.Xr.Gesten – Erkennerkern des Gestenlabors (Quest App V1b-1)

Christian, 24.10.2026: «ev. im Labor einen bereich für gestenerkennung machen. so dass ich dort eine geste für eine funktion vorzeigen kann.»
Pflichtenheft: `entwurf/modul-xr.md`, Abschnitt «Gestenlabor». Dieser Teil ist nur die Logik (rein, ohne StereoKit, wie der Rest von `Hlks.Xr`);
Labor-App «Gesten» in der Brille und Register «Gesten» im KICAD-Dialog «XR-Labor» folgen mit V1b-2.

Erkennung ohne Training eines Netzes: Merkmale je Bild, Vergleich mit den vorgezeigten Beispielen über Dynamic Time Warping (DTW), nächster Nachbar,
Ablehnung über eine Schwelle aus den Beispielen, Konfliktprüfung gegen die anderen eigenen und gegen die festen Gesten. Alle Startwerte sind Annahmen
(`GestenEinstellungen`), gewählt an synthetischen Reihen; kein Normwert ist betroffen.

## Aufbau

| Datei | Klassen | Inhalt |
| --- | --- | --- |
| `GestenDaten.cs` | `HandSeite`, `Haendigkeit`, `HandBild`, `GestenBild`, `Aufnahme`, `EigeneGeste`, `GestenSammlung` | Datenmodell; `HandBild.AusGelenken` aus den WebXR-Gelenken; Regel «eine Geste je Funktion» (`MitGeste`) |
| `GestenEinstellungen.cs` | `GestenEinstellungen` | alle Startwerte (Annahmen) mit Prüfung |
| `GestenMerkmale.cs` | `GestenMerkmale`, `Merkmalart`, `MerkmalFolge`, `RohFolge` (intern) | Merkmale je Bild, Körperbezug, Spiegelung, Neu-Abtasten; Ringpuffer der Rohwerte |
| `GestenDtw.cs` | `GestenDtw` | DTW mit Sakoe-Chiba-Band, untere Schranke (LB_Keogh), vorzeitiger Abbruch |
| `GestenErkenner.cs` | `GestenErkenner`, `VorbereiteteGeste`, `BeispielVorlage`, `GestenTreffer`, `GestenUrteil`, `ErkennungsArt` | Vorbereiten (Leave-one-out, Schwelle), Einordnen einer Aufnahme, Entscheid |
| `GestenLiveErkenner.cs` | `GestenLiveErkenner`, `GestenEreignis` | Live-Erkennung im Strom: Bilder hinein, Ereignisse heraus |
| `AufnahmeHilfe.cs` | `AufnahmeHilfe`, `Zuschnitt`, `AufnahmeRueckmeldung`, `GestenRekorder`, `RekorderZustand` | Bewegungsenergie, Zuschneiden, Rückmeldung; schrittweise Aufnahme |
| `GestenKonflikte.cs` | `GestenKonflikte`, `KonfliktBericht`, `GestenPaarKonflikt`, `KonfliktStufe`, `IFesteGestenRegel`, `WischRegel`, `FesteAusloesung` | Trennschärfe mit Warnstufe; Einhängepunkt für die festen Regelgesten, Umsetzung für den Wisch aus `XrGesten` |
| `GestenJson.cs` | `GestenJson`, `GestenGrenzen`, `GestenDatei`, `GestenDateiFehler` | Sammlung speichern und laden, Fassung und Grössengrenzen prüfen |

Tests: `tests/Hlks.Xr.Tests/Gesten*Tests.cs` mit einem synthetischen Generator plausibler Hände (`GestenGeneratorTests.cs`).

## Datenmodell

- **GestenBild:** Zeit (s, aufsteigend), Kopf (Lage) und Gierwinkel (Bogenmass wie `VrLogik`: gegen den Uhrzeigersinn von oben, ab +x), linke und
  rechte Hand.
- **HandBild:** verfolgt ja/nein, 25 Gelenke in der Reihenfolge von `HandGelenke.WebXr`, Handflächenmitte und -normale (aus der Handfläche heraus),
  Pinch-Abstand (Daumen- zu Zeigefingerspitze, m).
- **Aufnahme:** Folge von Bildern (ein Beispiel bzw. eine Rohaufnahme).
- **EigeneGeste:**
  - Id (`[a-z0-9][a-z0-9_-]{0,39}`) und Name.
  - Funktion als Schlüssel-Text, bewusst nicht an `GestenAktion` gebunden; neue Funktionen wie «AppsEinblenden» sind möglich.
  - Händigkeit:
    - `Links`, `Rechts`;
    - `Beliebig`: gespiegelt, was rechts nach rechts geht, geht links nach links;
    - `Beidhaendig`.
  - Beispiele (mindestens 3 für eine gültige Geste).
  - Schwelle: null = aus den Beispielen, sonst fest.
  - Aktiv.
- **GestenSammlung:**
  - `Stand`: Änderungszähler.
  - `GestenSammlung.Fassung`: Fassung des Dateiformats, jetzt 1.
  - `MitGeste` ersetzt die bisherige Geste derselben Id **und** derselben Funktion (eine Geste je Funktion in der ganzen App).
  - Weiter: `Ohne`, `Aktiviert`, `Umbenannt`, `Aktive`, `FuerFunktion`.

## Merkmale

Je Hand und Bild (`RohFolge`):
- Handflächenmitte und -normale.
- **Krümmung** je Finger = (Bogen − Sehne) / Handgrösse.
  - Bogen = Knochenlängen vom Ansatz («metacarpal», Daumen ab «thumb-metacarpal») bis zur Spitze.
  - Handgrösse = Handgelenk bis Grundgelenk des Mittelfingers, wie `XrTabletLogik.HandGroesse`.
  - Gestreckt 0, Faust ≈ 0.6 … 1.
- **Pinch** = Pinch-Abstand / Handgrösse, gekappt bei 1.

Je Abschnitt (Merkmalsfolge, neu abgetastet auf 32 Stützpunkte in gleichen Zeitabständen):
- **Körperbezug:** Achsen rechts, oben, vorn aus dem Gierwinkel des Kopfs im **letzten** Bild des Abschnitts; Nicken zählt nicht. Eine Hebegeste nach oben
  ist so «nach oben», egal wohin man schaut, und eine Körperdrehung ändert nichts.
  - Das letzte Bild, weil alle Kandidaten des Live-Erkenners jetzt enden: Die Drehung geschieht so einmal je Auswertung.
- **Lage** = Handflächenmitte relativ zum ersten Stützpunkt, geteilt durch die Ausdehnung der Bahn (grösster Abstand vom Start, mindestens 8 cm). Grössere
  und kleinere Ausführungen sind damit gleich; das Zittern einer ruhenden Hand bleibt klein.
- **Normale** im Körperbezug, Krümmung (5), Pinch (1).
- **Grösse** = ln(Ausdehnung / 0.3 m), schwach gewichtet.
- **Beliebig:** Die linke Hand wird gespiegelt (x von Lage und Normale umgekehrt). Beispiele der linken Hand liegen so in der Form der rechten.
- **Beidhändig:** beide Hände, dazu Abstand der Handflächen (m) und Richtung von links nach rechts.
- 13 Werte je Stützpunkt (eine Hand) bzw. 29 (beidhändig). Jeder Wert ist mit seinem Gewicht vervielfacht; der Vergleich rechnet euklidisch.

## Vergleich und Entscheid

- **DTW** zweier Folgen mit Band ± 20 % (± 6 Stützpunkte), Ergebnis = mittlerer Abstand je Stützpunkt.
- **Untere Schranke** (LB_Keogh) gegen die Hülle eines Beispiels und vorzeitiger Abbruch: Damit fallen die meisten Kandidaten ohne DTW weg.
- **Schwelle** je Geste (Leave-one-out): je Beispiel der Abstand zum nächsten anderen Beispiel; Schwelle = 1.8 × grösster davon, begrenzt auf
  [0.06, 0.8]. Streuung = Mittel.
- **Entscheid:**
  - Je Geste gilt der kleinste Abstand über ihre Merkmalarten und Beispiele, Verhältnis = Abstand / Schwelle.
  - Abgelehnt, wenn keine Geste ≤ 1 erreicht.
  - Unklar, wenn eine zweite Geste es auch erreicht und höchstens 25 % darüber liegt.
  - Sonst erkannt.

## Aufnahme-Hilfe

- **Bewegungsenergie** je Hand und Bild (m/s): Änderung zwischen zwei gleitenden Mitteln über je 0.1 s. Sie setzt sich zusammen aus:
  - Tempo der Handfläche;
  - 0.3 × Krümmungsänderung;
  - 0.05 × Drehung der Normale;
  - 0.1 × Pinch-Änderung.

  Bestimmbar nur, wo beide Fenster ganz in den Daten liegen. Bewegung = Energie über 0.15.
- **Zuschneiden:**
  - Abschnitte mit kurzer Ruhe (< 0.15 s) dazwischen gelten als eine Bewegung; es gilt die stärkste. Der Schnitt reicht ± 0.05 s um sie.
  - Rückmeldungen: keine Bewegung, Hand verloren, zu lang (> 2 s), zu kurz (< 0.2 s), keine Ruhe vorher, keine Ruhe nachher.
  - Texte für die Brille: `AufnahmeHilfe.Text`.
  - Beliebig: Die bewegtere Hand zählt (`Zuschnitt.AktiveHand`).
- **GestenRekorder** (für «Start per Knopf mit Countdown und Ton, Ende bei ruhigen Händen»): nimmt nach dem Ton Bild für Bild auf.
  - Er wartet auf Bewegung (höchstens 5 s).
  - Er endet nach 0.3 s ruhiger Hände bzw. bei zu langer Bewegung.
  - Dann schneidet er zu; das Ergebnis steht in `Ergebnis`.

## Live-Erkennung

`GestenLiveErkenner.Bild(bild)` je Bild:
1. **Ringpuffer** 2.5 s.
2. **Bewegungstor:** Ohne merkliche Bewegung einer Hand wird nicht gerechnet. Merklich heisst: 3 cm, Krümmung 0.15, Pinch 0.3 oder 25° Drehung der
   Normale.
3. **Prüfrhythmus:** höchstens alle 0.05 s, nicht in der Sperrzeit.
4. **Kandidaten:** Abschnitte, die jetzt enden.
   - Ihre Längen bilden ein Raster im Verhältnis 1.2: kürzestes Beispiel / 1.6 bis längstes × 1.6, das deckt Tempo ± 40 %.
   - Jede Geste prüft nur ihre Längen.
   - Je Länge und Merkmalart gibt es eine Folge für alle Gesten.
   - Zuerst die Schranke gegen die gemeinsame Hülle aller Beispiele der Geste, dann je Beispiel, erst dann DTW.
5. **Offener Treffer:** Er gilt erst, wenn er 0.12 s nicht mehr besser wurde. So zählt das Ende der ganzen Geste, nicht ein Teil.
6. **Ereignis:** `Erkannt` oder `Unklar`, mit beiden besten Gesten, Hand und Abschnitt.
7. **Sperrzeit** 0.5 s ab dem Ende der Geste; ein neuer Abschnitt muss danach beginnen (wie `XrWischLogik`).

## Konflikte und feste Gesten

- **Trennschärfe** gegen jede andere eigene Geste. Sie ist das Kleinere von zwei Verhältnissen:
  - kleinster Abstand der fremden Beispiele zur neuen Geste / deren Schwelle;
  - kleinster Abstand der eigenen Beispiele zur fremden Geste / deren Schwelle.

  Die Schwelle ist das Vielfache der eigenen Streuung. Stufen:
  - ≤ 1 Konflikt: ein Beispiel der einen löst die andere aus;
  - < 1.5 Warnung;
  - sonst keine.

  Die Geste, die die neue ersetzt (gleiche Id oder Funktion), wird nicht verglichen.
- **Einhängepunkt** `IFesteGestenRegel`: Die Prüfung spielt jedes Beispiel durch die Regel; jede Auslösung ist ein Konflikt. `WischRegel` setzt das mit
  der öffentlichen API von `XrGesten`, `XrWischLogik` und `XrTabletLogik` um:
  - `new WischRegel()` = mit gehaltenem Pinch (`XrGesten.PinchWischSchritt` am Pinch-Punkt, Pinch mit Hysterese), wie die App seit P5a wischt;
  - `new WischRegel(mitPinch: false)` = offene Hand in alle Richtungen (Prototyp, Masslinien).
- **Verhalten an den synthetischen Reihen** (dokumentiert in `GestenKonfliktTests`):

  | Geste | Pinch-Wisch der App | Wisch mit offener Hand |
  | --- | --- | --- |
  | Hebegeste | löst nicht aus (kein Pinch) | «Wisch hoch» in 4 von 5 Beispielen, beide Hände → Konflikt |
  | Auseinander | löst nicht aus | in allen Beispielen links «Wisch links» und rechts «Wisch rechts» → Konflikt |
  | Wisch hoch mit Pinch | in allen 5 Beispielen «Pinch + Wisch hoch» → Konflikt | – |

  Mit dem Pinch-Wisch der App vertragen sich Hebegeste und Auseinander also; ein offener Wisch (falls er als Labor-Variante wiederkommt) nicht.

## JSON (`GestenJson`)

```
{ art: "hlks-xr-gesten", fassung: 1, stand,
  gesten: [{ id, name, funktion, haendigkeit: "links"|"rechts"|"beliebig"|"beidhaendig", aktiv, schwelle: Zahl|null,
             beispiele: [{ bilder: [{ t, kopf: [x,y,z], gieren, links: Hand|null, rechts: Hand|null }] }] }] }
Hand = { mitte: [x,y,z], normale: [x,y,z], pinch, gelenke: [75 Zahlen, 25 Gelenke × x,y,z wie HandGelenke.WebXr; fehlend null] }
```

- Gespeichert wird auf 0.1 mm bzw. 0.1 ms gerundet. `Speichern` schreibt über eine temporäre Datei.
- **Streng** beim Kopf (art, fassung) und bei den Grössengrenzen. Grenzen (`GestenGrenzen`):
  - 48 Mio. Zeichen;
  - 64 Gesten;
  - 10 Beispiele je Geste;
  - 400 Bilder je Beispiel;
  - 20 000 Bilder insgesamt.

  Eine Verletzung wirft `GestenDateiFehler`.
- **Tolerant** bei Einträgen: Eine Geste mit ungültiger Id, Funktion, Name oder Händigkeit fällt mit Hinweis weg, ebenso eine doppelte Id oder Funktion.
  Ein Beispiel mit ungültigen Bildern fällt mit Hinweis weg.
- Grösse: 10 Gesten × 5 Beispiele zu etwa 0.5 s bei 72 Hz sind rund 1.6 Mio. Zeichen (gemessen im Budget-Test).

## Einbindung in die App (Vorschlag für V1b-2)

Schnittstelle: **Bilder hinein, Ereignisse heraus.** Je Bild liefert die App:

| Wert | Quelle in StereoKit | Hinweis |
| --- | --- | --- |
| Zeit `T` (s) | `Time.Total` | aufsteigend; ein Sprung zurück um mehr als 1 s beginnt neu |
| Kopf und `Gieren` | `Input.Head`; Gieren = `VrLogik.GierenVonRichtung(new Xz(forward.x, forward.z))` | Bei steilem Blick nach unten (waagrechter Anteil des Blicks klein) besser aus der Oben-Achse des Kopfs (`-up`) bzw. den letzten Wert halten |
| je Hand `HandBild` | `HandBild.AusGelenken(seite, HandGelenke.AusStereoKit((f, j) => …, wrist))` | nicht verfolgt bzw. unvollständig: `HandBild.Fehlt` |

**Wichtig:** Hände und Kopf im **Raum der Brille** (Referenzraum, nicht in der Szene nach Teleport und Drehen des Rigs). Sonst springt die Bahn beim
Teleport.

```csharp
var live = new GestenLiveErkenner(sammlung.Aktive, einstellungen);   // einmal, neu bei geänderter Sammlung
// je Bild:
var bild = new GestenBild(zeit, kopf, gieren, HandBild.AusGelenken(HandSeite.Links, gL), HandBild.AusGelenken(HandSeite.Rechts, gR));
if (live.Bild(bild) is { Art: ErkennungsArt.Erkannt } ev && variante == "eigene")
    Ausfuehren(ev.Beste.Funktion);   // eigene Geste ersetzt die feste derselben Funktion
```

- Ein Erkenner je Strom (nicht threadsicher). Rechenzeit am PC (10 Gesten × 5 Beispiele, 40 s Strom ohne Ruhe, 24.10.2026): im Mittel 0.017 ms je Bild
  (Release; Debug 0.06 ms), langsamstes Bild 0.6 ms (Debug 2.1 ms); in Ruhe fast null
  (Bewegungstor).
- Ablauf der Labor-App «Gesten»:
  1. Funktion wählen.
  2. Je Beispiel einen `GestenRekorder` starten (Countdown, Ton) und `Zuschnitt.Rueckmeldung` zeigen.
  3. Nach 3–5 Beispielen `GestenErkenner.Vorbereiten` (Güte: Streuung, Schwelle, Hinweise) und
     `GestenKonflikte.Pruefen(neu, sammlung.Gesten, [new WischRegel()])` zeigen.
  4. Live testen mit einem `GestenLiveErkenner` nur aus der neuen bzw. allen Gesten.
  5. Übernehmen: `sammlung.MitGeste(neu)`, dann `GestenJson.Speichern`.
- Die Startwerte kommen mit V1b-2 als Stellschrauben in den Katalog des Labors (`kataloge/xr_stellschrauben.json`, Gruppe z. B. «gesten»).

## Ergebnisse der synthetischen Reihen (24.10.2026)

- **Testgesten:**
  - Hebegeste (beidhändig), Wisch links, Kreis, Stoss nach vorn, Faust schliessen, Wisch hoch mit Pinch.
  - Auseinander (beidhändig, Handflächen nach aussen; Ergänzung Christian 24.10.2026).
  - Fürs Budget zusätzlich Wisch rechts, Wisch runter, Heranziehen.
  - Je 5 Beispiele: Tempo ± 15 %, Grösse ± 10 %, Drehung ± 20°, Rauschen 5 mm.
- **Prüfungen:**
  - Je Geste 9 Abwandlungen: Tempo × 0.6/1/1.4, Grösse × 0.75/1/1.25, Körperdrehung −90° bis 180°, Rauschen 5 mm.
  - Dazu 3 gespiegelt.
- **Ergebnisse:**
  - 78/78 erkannt, 0 falsch erkannt.
  - Gespiegelte Rechts-Gesten 6/6 abgelehnt.
  - Zufallsbewegungen: 80/80 abgelehnt, nächstes Verhältnis 2.5.
  - Ruhende Hände: abgelehnt, im Live-Erkenner ohne Auswertung.
  - Live-Strom mit Zufallsbewegungen dazwischen: 7/7 bzw. 10/10 erkannt, 0 Fehlauslösungen in 95 s Zufall.
  - Trennschärfe der Testgesten untereinander ≥ 4.
- Synthetische Hände streuen weniger als echte. Die Schwellen entstehen aber aus den eigenen Beispielen, sie passen sich echten Händen an. Nachstellen
  im Labor.

## Offen

- Werte mit echten Händen in der Brille prüfen: Rauschen, Schwellenfaktor, Gewichte, Bewegungstor.
- Gegenrichtung fester Gesten (löst das Vorzeigen einer festen Geste eine eigene aus?) braucht Aufnahmen der festen Gesten: im Labor mit V1b-2.
- Händigkeit «beliebig» spiegelt. Eine Richtungsgeste (Wisch links) mit beiden Händen ohne Spiegelung gibt es noch nicht (offene Frage an Christian).
