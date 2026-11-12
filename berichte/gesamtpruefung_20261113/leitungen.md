# Gesamtprüfung 13.11.2026 – Bereich Leitungen (Modul 3)

Geprüft: `src/Hlks.Kern/Leitungen/` (Routing.cs, RoutingBefehle.cs, LeitungEinsetzen.cs, Fliessrichtung.cs, GefaelleHoehe.cs, Lagen.cs,
Leitungszug.cs, EckeDehnen.cs vollständig gelesen; Formteilgeometrie.Ausrichtung und die Toleranzen in Leitungsserie.cs/Anschlusslagen.cs),
Oberfläche `werkzeuge/leitung.js`, `leitung-logik.js`, `leitung-daten.js`, `teilen.js`, `fallleitung.js`, `lagen-daten.js`, Leitungsfelder in
`optionen.js`. **Nicht gelesen** (Zeit): Leitungsserie.cs (ausser Formteilgeometrie), LeitungsserieAbfrage.cs, Verlegung.cs, Leitungspruefungen.cs,
LeitungenNachfuehren.cs, ParalleleLeitungen.cs, Fallleitung.cs, FormteilTauschen.cs, FreieGroesse.cs, Verbindungen.cs, `leitung-bearbeiten.js`,
`leitung-palette.js`, `leitung-frei.js`, `steigleitung.js`, `aktionen-leitung.js`. Nichts im Browser geprüft (keine Browserreihen, kein Server).

## 1. Kurzfazit

Die Geometrie des Routings (Einbaulängen, Ausrichtung der Bögen und T-Stücke, Projektionen, 2 × 45°, Gefälle) ist nach Handrechnung richtig.
Zwei echte Fehler steckten im neuen Weiterzeichnen mit Bogen (`Routing.AnGeradeKuerzen`, 12.11.2026): an einer Abwasserleitung mit 2 % wurde
**geradeaus** weiterzeichnen abgelehnt («Knick von 1.15°»), und mit anderer Grösse wurde der Zug als zu kurz abgelehnt (Reduktion nicht eingerechnet) –
beide korrigiert und mit Tests abgesichert. Dazu fünf kleine Korrekturen (Winkelfang, Silent-Wahl, Meldungstext, IFC-Kennung beim Teilen, Längen der
Teilen-Vorschau). Die wichtigste Schwachstelle bleibt das Zusammenspiel von 2 % Gefälle (1.146°) mit der Verbindungstoleranz von 1°: wo eine waagrechte
und eine geneigte Achse zusammentreffen, wird abgelehnt.

## 2. Korrigiert

| Nr. | Datei | Fehler | Korrektur | Test (Filter, Ergebnis) | Commit |
|---|---|---|---|---|---|
| 1 | `Leitungen/Routing.cs` (AnGeradeKuerzen), `RoutingBefehle.cs` | Der Knick am Leitungsende wurde aus den gezeichneten Punkten **vor** dem Gefälle gerechnet. Die Oberfläche zeichnet auf der Höhe des Anschlusses waagrecht weiter; an einer Leitung mit 2 % ist das 1.146° zur Achse > WinkelMax 1° → als Knick behandelt → «Am Ende von RO-1 lässt sich kein Bogen setzen: Knick von 1.15°». Abwasser geradeaus verlängern ging nicht mehr. | Richtung des ersten Stücks wie in `Vorbereiten` mit dem Gefälle rechnen (`MitGefaelle`), neuer Parameter `gefaelle`. | neu `Weiterzeichnen_MitGefaelle_GeradeausUndMitBogen` (vorher rot, jetzt grün); `FullyQualifiedName~RoutingZeichnenTests` 16/16 | be2141c |
| 2 | `Leitungen/Routing.cs` (AnGeradeKuerzen) | Weiterzeichnen mit Bogen und **anderer Grösse**: gekürzt wurde nur um den Bogen; `Planen` setzt dort zusätzlich eine Reduktion → «Die Gerade … ist 50 mm lang – die Formteile brauchen 90 mm». Dazu rechnete die Prüfung «zu kurz» mit einem Betrag (`(neu − andere).Laenge`), der bei zu kurzer Leitung wieder gross wird. | Einbaulänge der Reduktion mit frei machen; «zu kurz» entlang der Achse (Länge − Kürzung ≤ 1 mm). | neu `Weiterzeichnen_MitBogenUndAndererGroesse_KuerztUmBogenUndReduktion` (vorher rot); RoutingZeichnenTests 16/16 | aeb0cd1 |
| 3 | `js/werkzeuge/leitung-logik.js`, `leitung.js` | Winkelfang am freien Ende einer Geraden (seit 12.11.): Achsen der Ansicht und Bogenwinkel, aber **nicht mehr die Achse der Leitung selbst**, wenn sie schräg liegt (z. B. 30°) – geradeaus rastete nicht ein. | `richtungsKandidaten({ geradeaus: true })`. | `node --test js/werkzeuge/leitung.test.mjs` 46/46 (eine Zusicherung ergänzt) | 2db8759 |
| 4 | `Leitungen/LeitungEinsetzen.cs` (Teilen) | `leitung_teilen`/`leitung_einsetzen`: das zweite Stück übernahm `Kennungen` (IFC-GlobalId, Quelle) → dieselbe GlobalId zweimal im Modell (Abgleich Aussparungen, IFC). `leitung_abzweigen` leert sie schon. | `zweites.Kennungen = null`. | `FullyQualifiedName~LeitungEinsetzenTests` 4/4 (Zusicherung im Teilen-Test) | 6ab24f0 |
| 5 | `js/werkzeuge/leitung.js` | Abwasserleitung: beim Start wurde immer die **erste** Silent-Linie gesetzt, auch wenn schon eine andere Silent-Linie gewählt/gemerkt war (mehrere Silent-Linien im Projekt). | Gewählte Silent-Linie bleibt. | leitung.test.mjs 46/46 (kein eigener Test – Start des Werkzeugs läuft nur im Browser) | 65b5939 |
| 6 | `js/werkzeuge/leitung-logik.js`, `fallleitung.js` | Meldung «Die Produktlinie fehlt. In der Optionsleiste eine Produktlinie wählen.» – die Produktlinie steht seit 09.11.2026 nur im Eigenschaftsfenster. | Text: «Im Eigenschaftsfenster oben (Typenwahl) …». | leitung.test.mjs 46/46 | 74409d6 |
| 7 | `js/werkzeuge/teilen.js` | Vorschau «RO-1: 400 \| 550 mm» rechnete im Grundriss; bei geneigten Leitungen (Steigbogen 45°: Faktor 1.41) falsch gegenüber dem Kern. | Längen entlang der Achse (mit z). | kein Test (reine Anzeige), `node --check` | 7e0c0bd |

Zusätzlicher Gegenlauf nach den Kernänderungen: `AbwasserSerieTests|Referenz3GefaelleTests|LeitungsendeAnschliessenTests` 67/67 grün.
Kein Test war vor meinen Änderungen rot.

## 3. Geometrie – nachgerechnet (Zusatzauftrag «Logik hinterfragen»)

| Stelle | Handbeispiel | Ergebnis |
|---|---|---|
| Bogen-Ausrichtung `Formteilgeometrie.Ausrichtung(a, b)` | Steigbogen a = (1,0,0), b = (0,0,1): x = a, y = b ⊥ a = (0,0,1), z = x × y = (0,−1,0); Anschluss b bei P + Lb·(cos w·x + sin w·y) | richtig (b zeigt nach oben) |
| T-Stück `c = −cos w·x + sin w·y` | zulaufend: Fliessrichtung Abzweig −u, Winkel(d, −u) = w ⇒ u = −cos w·d + sin w·s = c ✓; zuführend w < 90: x = −d (gespiegelt), u = cos w·d + sin w·s = −cos w·x + sin w·y ✓; Einbaulängen vor/nach dann lb/la vertauscht ✓ | richtig |
| Fallstrang 2 % | d = (0,0,−1), Zulauf waagrecht gezeichnet, steigt vom T-Stück weg: Winkel = arccos(sin α) = 90° − 1.146° = 88.854° → T 88.5°, Abweichung 0.354° ≤ 1°; c-Achse 1.5° = 2.62 % | richtig; bei **0.5 %** (89.71°) greift nur noch die Fallleitungsregel (88.5° erzwungen), das erste Stück hat dann 2.6 % statt 0.5 % |
| 2 × 45° (`ZweiBoegen`) | Knick θ, neue Punkte Ecke ∓ c·a/b, c = (la + lb)/\|a + b\|; 90°: \|a+b\| = √2; Zwischenstück = la + lb, Richtung = Winkelhalbierende (je θ/2) | richtig |
| Gefälle | 3000 mm waagrecht, 2 % → 60 mm (über die waagrechte Länge, nicht die schräge) | richtig |
| Projektion ohne z (Abzweig, Teilen) | t = (p − A)ₓᵧ·dₓᵧ / \|dₓᵧ\|² | richtig (3D-Parameter, Fusspunkt im Grundriss) |
| `Schnitt2D`/`Schnitt3D` (Ecke, Dehnen) | Standardformeln nachgerechnet | richtig |
| Knick 2 % waagrecht → senkrecht | 90° − 1.146° = 88.854° | passt nur zu einem 88.5°-Bogen (±1°); mit einer Linie, die nur 90° hat: Differenz 1.146° > 1° → abgelehnt (siehe 4.1) |
| Knick 90° im Grundriss mit 2 % | cos = 0.0004/1.0004 → 89.977° | Linie mit 88.5° ohne 90°: Differenz 1.48° → wird als 2 × 45° ausgeführt (richtig, üblich) |

## 4. Gefunden, nicht korrigiert

1. **Toleranz 1° gegen 2 % Gefälle (1.146°)** – `Routing.cs:261–278` (Vorbereiten), `Anschlusslagen.WinkelMax`, `Routing.WinkelToleranz`.
   Problem: jede Verbindung «waagrechte Achse ↔ Stück mit 2 %» liegt 0.146° über der Toleranz. Fälle: (a) Abwasser mit 2 % an einer **waagrechten**
   bestehenden Leitung weiterzeichnen (geradeaus oder – neu – mit Bogen: das eingefügte Stück Leitungsende → Knick wird als «waagrecht gezeichnet»
   mit 2 % geneigt und verlässt die Achse unter 1.146°) → abgelehnt; (b) an einem waagrechten Apparate- oder T-Stück-Anschluss; (c) Übergang 2 % →
   senkrecht mit einer Produktlinie ohne 88.5°. Bis 1.7 % geht alles. Auswirkung: Ablehnungen genau beim Abwasser-Standard 2 %.
   Vorschlag: Christian entscheiden lassen – WinkelMax für Gefälleleitungen auf 1.5° (deckt bis 2.6 %) oder am gebundenen Ende das erste Stück in der
   Achse des Anschlusses lassen und das Gefälle erst danach beginnen. Aufwand mittel, **Priorität hoch**.
2. **Verlauf zwischen zwei gebundenen Enden mit Gefälle** – `Routing.cs:251–256`. Sind `von` und `nach` gebunden, muss die Höhe auf 1 mm aufgehen,
   sonst Ablehnung («Unterschied x mm»). In der Praxis geht das fast nie auf. Vorschlag: Rest über das Gefälle ausgleichen (effektives Gefälle melden)
   oder senkrechtes Ausgleichsstück vorschlagen und auf Wunsch setzen. Aufwand mittel, Priorität mittel (Entscheid Christian).
3. **AnGeradeKuerzen mit Übergang (andere Verbindungstechnik)** – `Routing.cs` (AnGeradeKuerzen). Die Reduktion ist jetzt eingerechnet, ein Übergang
   aus der Verbindungstabelle nicht (praktisch nur an Geräten, nicht an Geraden). Ebenso: reicht die Gerade für 2 × 45° nicht, kommt die Meldung
   «Knick von 90° – die Produktlinie hat …» statt «zu kurz für den Bogen». Aufwand klein, Priorität tief.
4. **Winkelfang am schrägen Leitungsende** – `leitung.js:95` (kandidaten). Angeboten werden auch die Achsen der Ansicht (0/90/180/270); an einer
   Leitung unter z. B. 20° ergibt das Knicke von 20° bzw. 70°, die die Produktlinie nicht hat → Ablehnung erst im Kern. Vorschlag: am Leitungsende nur
   geradeaus und ± Bogenwinkel. Aufwand klein, Priorität mittel.
5. **Fliessrichtung umkehren mit Gefälle** – `Fliessrichtung.cs:140–146`. Beispiel A (z 1000) → B (z 940), 2 %: nach dem Umkehren ist B der Anfang
   und bleibt auf 940, A sinkt auf 880 – die ganze Leitung liegt 60 mm tiefer, statt dass Hoch- und Tiefpunkt tauschen (B 1000 → A 940 bzw. um die
   Mitte gekippt). Vorschlag/Frage: welcher Punkt soll fest bleiben? Aufwand klein, Priorität mittel.
6. **OhneGeradePunkte verschiebt den Verlauf** – `Routing.cs:314–329`. Ein Zwischenpunkt mit Knick < 1° entfällt; bei zwei Schenkeln von je 5 m
   wandert die Achse dort bis ca. 40 mm (5000 · sin 0.45°). Es gibt einen Hinweis, aber keine Grenze für den Versatz. Vorschlag: zusätzlich
   Versatz ≤ wenige mm verlangen, sonst ablehnen. Aufwand klein, Priorität tief.
7. **Fallleitungsregel ohne Richtungsprüfung** – `RoutingBefehle.cs:630`. `Math.Abs(d.Z) > 0.99` gilt auch für eine senkrechte Abwasserleitung,
   die nach **oben** fliesst (falsch gezeichnet); dann wird 88.5° gegen die Schwerkraft gesetzt. Vorschlag: `d.Z < −0.99` und sonst Hinweis
   «Fliessrichtung umkehren». Aufwand klein, Priorität tief.
8. **Dehnen bis Leitung mit schrägem Abzweig** – `EckeDehnen.cs:192–215`. Das Ende der gedehnten Leitung springt auf die Lage von c; passt der
   Abzweigwinkel der Produktlinie nicht zur Leitung (z. B. 60° gezeichnet, Linie 45°/88.5°), setzt `leitung_abzweigen` 45° und `verbinden` lehnt
   danach mit einer Winkelmeldung ab, die den Grund nicht nennt. Nicht geprüft: ob die Fliessrichtung der gedehnten Leitung (anfang/ende) zur
   Richtung des Abzweigs passt. Aufwand mittel, Priorität tief.
9. **Teilen an senkrechter Leitung** – `teilen.js` klick. Die Oberfläche sendet `[x, y]`; der Kern lehnt ab («Punkt mit Höhe angeben: [x, y, z]»),
   was im Werkzeug nicht geht. Vorschlag: Höhe abfragen oder im Schnitt teilen. Aufwand klein, Priorität tief.
10. **Lagen.LageVon** – `Lagen.cs:86`. Prüft `pts[i].Length > 2`, liest aber `pts[i − 1][2]` (Absturz bei gemischten 2D/3D-Punkten, kommt bei
    gezeichneten Leitungen nicht vor). Die Lage einer Gefälleleitung richtet sich nur nach ihrem Anfangspunkt. Aufwand klein, Priorität tief.
11. **Abzweig vom Rohr-Werkzeug an einer Abwasserleitung** – `leitung.js` abzweigStarten. Produktlinie und Grösse werden übernommen, das Gefälle
    nicht (Feld erscheint leer in der Optionsleiste). Vorschlag: `gefaelle` der Leitung mit übernehmen. Aufwand klein, Priorität mittel.
12. **Leistung** – `leitung-daten.js:127`: jede Löschung im Modell leert den Zwischenspeicher aller Produktlinien (`leitungsserie` wird neu
    geholt). `teilen.js`: je Mausbewegung über alle Leitungen samt Grafikteilen. Beides bei heutigen Modellgrössen unkritisch. Priorität tief.

## 5. Offene Fragen an Christian

1. Abwasser 2 % an waagrechten Anschlüssen/Leitungen (Punkt 4.1): Toleranz für Gefälleleitungen anheben (bis welches Gefälle?) oder das Gefälle
   erst nach dem Anschluss beginnen lassen?
2. Fliessrichtung umkehren (4.5): Soll der bisherige Hochpunkt seine Höhe behalten (Leitung kippt), oder ist «neuer Anfang bleibt, Rest sinkt» gewollt?
3. Zwei gebundene Enden mit Gefälle (4.2): darf KICAD das Gefälle an die Höhen anpassen (und das tatsächliche Gefälle melden), oder soll es bei
   der Ablehnung bleiben?
4. Abwasserleitung «immer Silent»: Gibt es im Büro mehrere Silent-Linien (z. B. db20 und PP)? Welche ist die Vorgabe, wenn noch keine gewählt ist
   (heute: die erste nach Name)?
