# Gesamtprüfung 13.11.2026 – Bereich Familiengeometrie (Runde 3)

Zweig `worktree-agent-ae6436eefbdd444b8`, Ausgangsstand `e414b7b`. Geprüft: `src/Hlks.Kern/Familien/` (Familien.cs, FamilienAuswertung.cs mit
FamilienKoerper und FamilienUmriss, FamilienNetz.cs, FamilienFormel.cs, FamilienAnsicht.cs soweit Lage/Projektion), `Leitungen/Leitungsserie.cs`
(Formteilgeometrie, Formteilwahl), `Leitungen/Anschlusslagen.cs`, `Grafik/FamilienGrafik.cs`, dazu die Verwender der Formteilachsen
(`HlksKoerper.DaemmungFormteil`, `DaemmGrafik`) und die rechnenden Stellen des Familien-Editors (`js/familien/familien-ansicht.js`).

## 1. Kurzfazit

Die Kernrechnung stimmt: Rechtssystem (y = z × x), Drehsinn, Einbaulagen, Einbaulängen und Anschlussrichtungen der Formteile sowie die Umrisse
aus gedrehten Körpern liessen sich mit Handbeispielen bestätigen (siehe 1a). Gefunden und korrigiert sind vier echte Geometriefehler: das Netz des
Körpers «uebergang» war nach innen orientiert, die Achse eines schrägen Abzweigs lief im Lot statt in Abzweigrichtung (einlinige Darstellung und
Dämmung), `cos(90)` war nicht 0 (Division durch «fast 0» ohne Fehler) und der Umriss eines uneinheitlich orientierten übernommenen Netzes blieb
unvollständig bzw. leer. Offen bleiben vor allem Annahmen der Bogendarstellung (Parameter «r» als Biegeradius, Radius kleiner als Rohrradius) und
eine fehlende Prüfung «Anschlussrichtung zeigt nach aussen» bei Familien, die keine Formteile sind.

### 1a. Handbeispiele (nachgerechnet und mit einem Probelauf verglichen)

| Fall | Handrechnung | Ergebnis Kern |
|---|---|---|
| Bogen 90°, DN 20, da 25, r = 2·da = 50 (test_bogen) | la = lb = r·tan(45°) = 50; a (−50, 0, 0)/(−1, 0, 0), b (0, 50, 0)/(0, 1, 0) | identisch, Umlenkwinkel 90 |
| Bogen 45°, gleicher Typ | la = lb = 50·tan(22.5°) = 20.711; b = 20.711·(0.7071, 0.7071) = (14.645, 14.645) | identisch |
| Bogen 90°: Umriss von oben | Viertelkreisring π/4·(62.5² − 37.5²) = 1963.5 mm² | 1962.1 (48-Eck) |
| Bogen 90°: Volumen | π·12.5²·(π/2·50) = 38 553 mm³ | 38 416 (−0.36 %, Facetten) |
| Bogen 45°: Umriss | Bogenlänge 50·π/4 = 39.27 × 25 = 981.7 mm² | 981.0 |
| T-Stück DN 20 (l = lc = da = 25) | a (−25, 0, 0), b (25, 0, 0), c (0, 25, 0); Umriss 50·25 + 25·12.5 = 1562.5 mm² | identisch |
| T-Stück stehend (x = (1,0,0), z = (0,−1,0) → y = (0,0,1)) | Abzweig zeigt nach oben, Umriss nur Durchgang 50·25 = 1250 mm² | 1250 |
| Reduktion DN 25/20 (da 30/25, l = 40) | a (0,0,0)/(−1,0,0), b (40,0,0)/(1,0,0); Umriss Trapez (30+25)/2·40 = 1100 mm²; Volumen π·40/3·(15²+15·12.5+12.5²) = 23 824 mm³ | 1100; 23 756 (−0.29 %) |
| Steigbogen: Ausrichtung(durch (1,0,0), seitlich (0,0,1)) | x = (1,0,0), y = (0,0,1), z = x × y = (0,−1,0) | identisch; Welt(0,1,0) = (0,0,1) |
| Achsschnittpunkt (BogenMitRadius) für pa (−44,0,0)/(−1,0,0), pb (0,44,0)/(0,1,0) | s = t = −44, Schnittpunkt (0,0,0), Ablenkung 90°, R_max = 44 | stimmt (R wird auf 0.999·R_max begrenzt, nur Darstellung) |
| Sweep 90° mit R 60, Profil r 30, Schenkel 100 | π·30²·(40 + 40 + π/2·60) = 492 674 mm³ | 491 079 (−0.3 %) |
| Bulge (Facetten) | θ = 4·atan(w); Mittelpunkt links der Sehne bei w > 0, Abstand l/2/tan(θ/2) | Formel im Code stimmt, auch für w > 1 (Bogen > 180°) |
| Ansichten (FamilienAnsicht.Lage, JS `projektion`) | rechts = Blickrichtung × z: vorne x, hinten −x, links −y, rechts +y, unten −x | Kern und JS stimmen überein |

## 2. Korrigiert

1. **`src/Hlks.Kern/Familien/FamilienNetz.cs` (NetzBau.Uebergang)** – Das Netz war nach innen orientiert: die Ringe laufen um −w, die Mantel-Vierecke
   und beide Deckel hatten damit die Normale nach innen (Probe: Volumen −974 230 mm³ statt positiv). Alle anderen Körper rufen `Orientieren()`,
   der Übergang nicht. Folge: verkehrte Normalen in 3D/XR (XrNetzbau rechnet Knick-Normalen aus der Orientierung). Zudem ergab `oben` parallel zur
   Achse still einen Körper ohne Volumen (alle Ringpunkte auf der Achse), ein Radius ≤ 0 ein verkehrtes Netz, ein fehlendes Profil eine
   NullReference. Korrektur: `Orientieren()`, dazu Fehler mit Vorschlag für von = nach, fehlendes Profil, Radius/Rechteck ≤ 0 und `oben` in der Achse.
   Test: `FamilieGeometrieTests.Uebergang_ist_aussen_orientiert_und_lehnt_oben_in_der_Achse_ab` (Volumen Zylinder r 50 × 100 = 785 398 mm³ − Facetten).
   Filter `FamilieGeometrieTests|BogenRadiusTests|FamilieFormelTests|FamilieKoerperTests|FamilieGrafikTests|Modul3TestfamilienTests|AnschlusslagenTests|Formteil`: 232 grün.
   Commit `f0bf9ea`.
2. **`src/Hlks.Kern/Familien/FamilienAuswertung.cs` (FamilienKoerper.Achsen)** – Bei drei und mehr Anschlüssen endete die Achse des Abgangs im Lot
   vom Anschluss auf den Durchgang (auf dessen Länge begrenzt). Für den rechtwinkligen Abzweig ist das der Achsschnittpunkt, für den schrägen
   nicht. Handbeispiel Abzweig 45°, a (−100,0,0), b (100,0,0), c = 200·(−cos 45°, sin 45°) = (−141.4, 141.4): Fuss war (−100, 0, 0) (Lot bei
   x = −141.4, begrenzt auf das Ende a) – richtig ist (0, 0, 0). Die einlinige Darstellung (Detailgrad grob), die Familienansicht und die Dämmung
   (Grundriss und 3D) liefen damit quer statt schräg. Korrektur: Fuss = nächster Punkt der Durchgangsachse zur Geraden «Anschluss rückwärts entlang
   seiner Richtung»; parallel zur Achse weiter das Lot. Dabei den Kommentar von `Einbaulage.AlsJson` berichtigt (liefert x, y, z ohne Ursprung).
   Test: `BogenRadiusTests.Achse_des_schraegen_Abzweigs_endet_im_Achsschnittpunkt`. Lauf wie oben, 232 grün. Commit `021c95b`.
3. **`src/Hlks.Kern/Familien/FamilienFormel.cs` (sin, cos)** – `cos(90)` ergab 6.1e-17, `sin(180)` 1.2e-16. Damit wurde `l / cos(winkel)` bei 90°
   zu 1.6e16 statt «Division durch 0», und Lagen wie `lb * cos(winkel)` lagen um Rundungsreste neben der Achse. Korrektur: bei Vielfachen von 90°
   genau 0, 1, −1. Test: `FamilieFormelTests.SinCos_bei_Vielfachen_von_90_Grad_genau`. Weil sin/cos in allen Formteil-Familien stecken, zusätzlich
   einmal `(Familie|Routing|Einbaulage|Daemm)` ohne Bibliothek/Prüfung/Quellen: 399 grün. Commit `6a8c19a`.
4. **`src/Hlks.Kern/Familien/FamilienAuswertung.cs` (FamilienUmriss.Rechnen)** – Der Umriss von oben nahm bei Netzkörpern nur Dreiecke mit positiver
   projizierter Fläche. Das ist richtig für geschlossene, einheitlich orientierte Netze; ein übernommenes Netz (Körper «netz» aus IFC/STL wird weder
   orientiert noch auf Geschlossenheit geprüft) verlor damit Stücke des Umrisses. Handbeispiel Tetraeder mit Grundfläche 2500 mm² und einem
   verkehrten Dreieck: Umriss war leer. Korrektur: heben sich die projizierten Flächen nicht auf (Abweichung > 0.1 %), zählen alle Dreiecke; für
   saubere Netze bleibt der Aufwand gleich. Test: `FamilieGeometrieTests.Umriss_eines_uneinheitlich_orientierten_Netzes_ist_vollstaendig`.
   Filter `FamilieGeometrieTests|FamilieGrafikTests|FamilieKoerperTests|IfcFamilienTests|BogenRadiusTests`: 62 grün. Commit `4e7165a`.

## 3. Gefunden, nicht korrigiert

1. **FamilienAuswertung.cs:499–502 und 601–603 (BogenMitRadius, BogenAchse)** – Der Biegeradius wird aus dem ersten Parameter namens
   `biegeradius`, `radius`, `r` oder `R` gelesen. In einer Bogen-Familie aus zwei Zylindern kann `r` ebenso gut der Rohrradius sein (da / 2); dann
   wird der Bogen mit Achsradius = Rohrradius gezeichnet. Ausserdem gibt es keine Untergrenze: ist R kleiner als der Profilradius (kleines z-Mass),
   durchdringt sich der Torus selbst (Netz bleibt geschlossen, Probe R 10 / r 30, aber Innenseite gefaltet). Auswirkung: Darstellung, keine
   Masse. Vorschlag: nur `biegeradius`/`radius` bzw. einen Parameter mit `ziel` «biegeradius» gelten lassen und R < Profilradius als Gehrung
   zeichnen. Aufwand klein, Priorität mittel – ändert die Darstellung bestehender Familien, deshalb Entscheid.
2. **FamilienAuswertung.cs:503 (BogenMitRadius, Profil)** – Das Profil des Ersatzbogens ist der grösste Zylinderradius. Hat die Familie Muffen als
   eigene, dickere Zylinder im selben Detailgrad, wird der ganze Bogen mit Muffenradius gezeichnet. Vorschlag: Radius der Zylinder nehmen, die am
   Achsschnittpunkt enden; Muffen als gerade Stücke behalten. Aufwand mittel, Priorität tief bis mittel (abhängig von den Bibliotheksfamilien, die
   ich nicht einsehen konnte – die Bibliothek liegt nicht im Repository).
3. **FamilienAuswertung.cs:221–251 (Anschlüsse)** – Die Richtung «aus dem Gerät heraus» wird nur für Formteile geprüft (Formteilgeometrie.AufAchse).
   Bei Apparaten und Armaturen kann ein Anschluss nach innen zeigen, ohne dass es jemand merkt; verbunden wird dann gegenläufig, die Leitung läuft
   durch den Apparat. Vorschlag: in der Freigabeprüfung ein Hinweis, wenn ein Strahl vom Anschluss in seiner Richtung die Hülle der Körper
   durchquert (bzw. Richtung · (Lage − Mitte der Hülle) < 0). Aufwand klein bis mittel, Priorität mittel.
4. **Familien.cs:36 (Einbaulage.Achsen)** – Sind x und z parallel, werden Y und Z zu (0, 0, 0) (V3.Einheit lässt den Nullvektor stehen); alle Punkte
   fallen dann auf eine Gerade. `Familien.AusrichtungLesen` prüft das vorher, ein direkter Aufrufer (Routing, künftiger Code) nicht. Vorschlag:
   in `Achsen` einen HlksFehler werfen. Aufwand klein, Priorität tief.
5. **Leitungsserie.cs:947 (Bogen 180°)** – Zugelassen ist 0 < w ≤ 180. Bei 180° zeigen a und b in dieselbe Richtung und liegen auf derselben Achse
   (Lage Lb·(cos w, sin w, 0) = (−Lb, 0, 0)): ein «Bogen» ohne Achsabstand, die Achsen schneiden sich nicht. Ein echter 180°-Bogen (Umkehrbogen) hat
   einen Achsabstand 2·R und passt nicht in das Schema «Ursprung = Achsschnittpunkt». Vorschlag: w < 180 verlangen oder den Umkehrbogen eigens
   beschreiben. Aufwand klein, Priorität tief (fachlicher Entscheid).
6. **FamilienAuswertung.cs:474 ff. (BogenMitRadius ausserhalb try/catch)** – `NetzBau.Sweep` kann HlksFehler werfen; der Aufruf in `Rechnen` steht
   nicht in einem try. Mit den heutigen Schranken (R ≤ 0.999·R_max, Ablenkung > 0.001) sehe ich keinen Weg dorthin; trotzdem wäre ein Fehler hier
   eine Ausnahme aus `Aufloesen` statt ein FamFehler. Vorschlag: abfangen und die Zylinder stehen lassen. Aufwand klein, Priorität tief.
7. **FamilienNetz.cs (Sweep mit Biegeradius)** – Kein Vergleich Biegeradius gegen Profil: Rechteckprofil b mit Biegeradius < b/2 bzw. Kreisprofil r
   mit Biegeradius < r faltet die Innenseite. Vorschlag: Fehler mit Vorschlag «Biegeradius ≥ halbe Profilbreite». Aufwand klein, Priorität tief –
   könnte bestehende Familien ungültig machen, deshalb zuerst die Bibliothek daraufhin durchsehen.
8. **FamilienAuswertung.cs:454–462 (Körper «netz»)** – Übernommene Netze werden weder orientiert noch auf Geschlossenheit geprüft. Der Umriss ist
   jetzt robust (Korrektur 4), Schnitt und 3D verlassen sich aber auf die Orientierung der Quelle. Vorschlag: beim Übernehmen
   (FamilienNetzUebernehmen) einmal `Orientieren()` und `GeschlossenFehler()` als Hinweis. Aufwand klein, Priorität mittel.
9. **FamilienAuswertung.cs:119 (Zwischenspeicher)** – Bei mehr als 1000 Auswertungen je Familie wird der ganze Speicher geleert. Eine freie
   Kanalserie mit vielen Grössen kann das im Grundriss wiederholt auslösen (alles wird neu gerechnet). Vorschlag: nur die ältesten Einträge
   verwerfen. Aufwand klein, Priorität tief.
10. **FamilienGrafik.cs:228–235** – Im Detailgrad grob wird der Umriss gerechnet, obwohl einlinig gezeichnet wird; er dient dann nur den Fangpunkten
    (Umrissecken eines Körpers, der nicht gezeichnet ist). Vorschlag: bei einliniger Darstellung die Achspunkte als Fangpunkte nehmen. Aufwand
    klein, Priorität tief.
11. **FamilienFormel.cs (tan)** – `tan(45)` ergibt 0.9999999999999999; `r * tan(winkel / 2)` für 90° damit 49.99999999999999 statt 50. Durch das
    Runden der Lagen (0.001 mm) ohne Folge; nur genannt, falls später Gleichheit ohne Toleranz geprüft wird.

## 4. Offene Fragen an Christian

1. Biegeradius der Ersatzbögen (Punkt 3.1): Soll ein Parameter `r` weiter als Biegeradius gelten, oder nur `biegeradius`/`radius`? Und was soll
   gezeichnet werden, wenn der Radius kleiner als der Rohrradius wäre – Gehrung?
2. Bogen 180° (Punkt 3.5): Kommt ein Umkehrbogen als Formteil vor? Wenn ja, braucht er eine eigene Lagebeschreibung (Achsabstand).
3. Anschlussrichtung nach innen (Punkt 3.3): Hinweis in der Freigabeprüfung oder Fehler?

## Hinweis zum Ablauf

Bei jedem `git commit` meldete Git «failed to delete …/.git/worktrees/wt_b1» und «wt_b2» (Permission denied). Das sind fremde, verwaiste
Arbeitskopie-Einträge, die Git aufräumen wollte; die Commits selbst sind in Ordnung. Ich habe daran nichts geändert.
