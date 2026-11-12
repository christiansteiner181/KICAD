# Gesamtprüfung 13.11.2026 – Bereich Rechenkerne (Runde 3)

Zweig `worktree-agent-aa8a108a9ce24cd55`, Ausgangsstand `8a3203c`. Geprüft: die Rechenkerne der Module 4, 5, 6 (Verfahren aus
`kataloge/rechenregister.json`): Sanitär (Belastung, Zusammenstellung, Spitzendurchfluss, Dimensionierung Abwasser/Trinkwasser), Heizung
(Leistung/Massenstrom, Stoffwerte, Heizlast, Heizkörper, Rohr-Druckverlust), Lüftung (Luftmengen, Lüftungskette, Kanal-Druckverlust), Dämmdicke.
Nicht vertieft: Heizgruppen-Ableitung, Hülle (U-Wert, Raumgrenzen), Leitungs-Routing – eigene Bereiche.
**Norm-, Tabellen- und Stoffwerte wurden nicht verändert und nicht ergänzt.**

## 1. Kurzfazit

Die älteren Kerne (Sanitär Teil A/B, Heizlast, Stoffwerte) sind sauber: Einheiten, Formeln, Interpolation (geklemmt bzw. «kein Wert ausserhalb»)
und Summenbildung stimmen mit dem Rechenregister überein, die Handbeispiele gehen auf. Die Fehler sitzen in den jüngsten Kernen, den
**Druckverlusten (Modul 5/6 Teil B)**: Der Kanal-Druckverlust je Durchlass lief durch das Lüftungsgerät ins andere Netz (Zuluftweg zählte die
Abluftkanäle mit), und drei Abfragen brachen bei Randwerten mit einer JSON-Ausnahme ab (Division durch null → NaN/∞). Das ist korrigiert.
Offen und fachlich wichtig ist, **mit welcher Leistung das Heizungsrohrnetz rechnet**: heute mit der Normleistung der Heizkörper statt mit dem
Bedarf – Massenstrom und Druckverlust werden damit bei Niedertemperatur deutlich zu gross (Frage 1).

## 2. Korrigiert

Tests je Korrektur gefiltert; Schlusslauf
`dotnet test tests/Hlks.Kern.Tests --filter "FullyQualifiedName~LueftungMengenTests|…HeizlastTests|…HeizkoerperAuslegungTests|…RohrDruckverlustTests|…HeizungTests"`
→ **103 bestanden, 0 Fehler**. Die drei neuen Tests waren vor der Korrektur rot (nachgewiesen durch Lauf gegen den alten Code).

### Commit 7f93d29 – Kanal-Druckverlust (`src/Hlks.Kern/Rechnen/KanalDruckverlust.cs`)

1. **Weg je Durchlass lief durch das Lüftungsgerät.** `Netz.Folgen` ohne `stopp`: vom Zuluftdurchlass stromauf über `LG.zul` weiter über den
   Anschluss `LG.abl` (Fluss «rein») in **alle Abluftkanäle**, vom Abluftdurchlass stromab über `LG.zul` in alle Zuluftkanäle. Der «massgebende
   Durchlass» war damit Zuluft + Abluft gemischt. Nachweis am Testnetz: ZD-1 Soll 2.08 Pa (K-0 + K-1 + K-2), Ist 2.30 Pa (+ Abluftkanal K-4).
   Korrektur: gleicher `stopp` wie in `LueftungMengen` (am Gerät endet der Weg).
2. **Kanal ohne Volumenstrom → Ausnahme.** Alle Durchlässe dahinter ohne Angabe bzw. Raumrest 0: w = 0, Re = 0, λ = NaN → die ganze Abfrage
   `kanal_druckverlust` warf beim Schreiben («NaN cannot be written as valid JSON»). Jetzt Zeile mit Hinweis «kein Druckverlust», ohne Zahlen.
   Ebenso Querschnitt mit Breite/Höhe 0 (Division durch a + b = 0).
3. **Rohr einer Produktlinie im Luftnetz rechnete mit dem Aussendurchmesser.** `HlksMasse.Von` liefert das Mass der *Darstellung* (Rohr: da).
   Neu `Querschnitt()`: bei Kategorie Rohr der Innendurchmesser der Produktlinie; fehlt er, da mit Hinweis «Druckverlust zu klein».
   Beispiel fiktiv 75/63 mm: Fläche vorher 42 % zu gross.
4. **Rauheit «Kunststoff» traf Wortteile.** Muster `pe|pvc|pp` passte auf «S**pe**zial», «Do**pp**elwandig», «Kla**pp**e» → k = 0.007 statt
   0.15 mm (unsichere Seite). Jetzt nur als eigenes Wort (PE, PP, PVC, auch «PE-HD») bzw. «Kunststoff».
5. Je Durchlass neu `kanaeleOhneDp` + Hinweis, wenn Kanäle auf dem Weg keinen Druckverlust haben (vorher still zu kleine Summe).

Tests: `LueftungMengenTests.Druckverlust_Weg_endet_am_Geraet_und_Kanal_ohne_Volumenstrom_bleibt_ohne_NaN`,
`…Druckverlust_Querschnitt_Rohr_mit_Innendurchmesser_und_Kunststoff_nur_als_Wort` (Filter `FullyQualifiedName~LueftungMengenTests`, 5/5 grün).

### Commit 0b195ae – Rohr-Druckverlust (`src/Hlks.Kern/Heizung/RohrDruckverlust.cs`)

6. **Summe je Verbraucher verschwieg Lücken.** Leitungen ohne Temperatur (ausserhalb einer Heizgruppe, z. B. Hauptverteilung), ohne
   Innendurchmesser oder mit Glykol fehlten still in der Summe; der «massgebende Verbraucher» konnte so der falsche sein. Neu je Verbraucher
   `leitungenOhneDp` und Hinweis; dazu Hinweis bei **Masche** (Ring, Tichelmann: parallele Leitungen tragen in `HeizungLeistung` je den vollen
   Strom und werden beide addiert). Die Zahlen selbst sind unverändert. Kein neuer Test (nur Zusatzfelder; `RohrDruckverlustTests` grün).

### Commit 4291d47 – Heizlast und Heizkörper

7. **`Heizlast.cs`: θint ≤ θe.** bu (Gl. 6), fij (10), fg2 und fV teilen durch (θint − θe); bei Gleichheit ∞/NaN → Ausnahme der ganzen Abfrage
   `heizlast` (ein einziger falsch erfasster Raum genügt), bei θint < θe negative «Heizlast». Jetzt: Raum ohne Heizlast mit Annahme
   «Innentemperatur … nicht über der Aussentemperatur», nicht in der Gebäudesumme (Text der Summe und von `heizlast_uebernehmen` angepasst).
   Test `HeizlastTests.Heizlast_Innentemperatur_nicht_ueber_Aussentemperatur_…`.
8. **`HeizkoerperAuslegung.cs`: ΔTn ≤ 0** (z. B. normRaum irrtümlich 70 bei 75/65): (ΔT/ΔTn)^n = ∞ → Ausnahme der ganzen Abfrage. Jetzt
   Annahme «Herstellerangabe … ohne Übertemperatur», keine Umrechnung. Test in `HeizkoerperAuslegungTests` ergänzt.
9. `Heizlast.Rechnen`: Raumgrenzen je Raum über ein Lookup statt je Raum die ganze Liste zu filtern (O(R·G) → O(G)), ohne Verhaltensänderung.

## 3. Handbeispiele (nachgerechnet)

| Kern | Eingabe | von Hand | Code |
|---|---|---|---|
| Spitzendurchfluss (Excel Walkringen) | V = 13.2 l/s | 0.459·13.2^0.353 = 1.1412; 0.598·13.2^0.257 = 1.1606 l/s | gleich (Referenz im Katalog) |
| Massenstrom | 1000 W, 55/45 °C, c_p(50 °C) = 4.1811 | 1000/(4181.1·10)·3600 = 86.10 kg/h | Formel `HeizungLeistung.Stroeme` identisch |
| Rohr-Druckverlust | 86.1 kg/h, d_i 16 mm, VL 55 °C (ρ 985.66, η 5.063e-4), k 0.05 mm | w = 0.1207 m/s, Re = 3759, λ = 0.0436, R = 19.6 Pa/m | Formelkette Zeile 74–77 identisch (kein Endwert-Test vorhanden, siehe 4.10) |
| Kanal-Druckverlust | Ø 200, 300 m³/h, 20 °C (ν 14.9e-6, ρ 1.2013), k 0.15 mm | w = 2.653 m/s, Re = 35 605, λ = 0.0246, R = 0.52 Pa/m | Test `Druckverlust_Reibung_…` grün |
| Heizkörper | Φn 1000 W (75/65/20, n 1.3) bei 55/45/22 | ΔT_log = 27.70 K, f = (27.70/50)^1.3 = 0.464, Φ = 464 W | Test grün |
| Heizlast (Testraum 6×4×2.6 m) | U 0.25, Fenster 1.44 m² Uw 1.0, Δθ 30 K, n_min 0.5 (fiktiv) | H_T = 14.08 W/K → 422 W; V̇ = 31.2 m³/h · 0.3117 · 30 = 292 W | Test grün |
| Lüftungskette | 300 m³/h bei 3 m/s | A = 0.0278 m², d = 188 mm | Formel `Neu()` identisch |

Einheiten geprüft und in Ordnung: W↔kW (`LeistungW`), kg/h↔l/h, mm→m (Längen, Durchmesser, Raumvolumen m²·mm/1000), ρ·c in Wh/(m³K)
= (1220 − 0.14·h)/3600, Luft η/ν ·10⁻⁶, Gefälle in %, Terrain/Koten in mm. Längen sind 3D-Achslängen (`Geometrie.Laenge`, senkrechte Stücke
zählen mit); Formteile sind Punkte am Achsschnitt, die Leitungen laufen bis dorthin – die abgewickelte Länge ist damit vollständig (Bogen leicht
überschätzt, sichere Seite).

## 4. Gefunden, nicht korrigiert

1. **`Heizung/HeizungLeistung.cs:115–128` (Stroeme) → `RohrDruckverlust.cs` – Massenstrom aus der Normleistung.** ṁ = Q/(c_p·ΔT) nimmt die Angabe
   «leistung» des Heizkörpers (Normleistung bei 75/65/20) mit der Spreizung der Heizgruppe. Beispiel oben: Heizkörper 1000 W Norm, Betrieb 55/45,
   Bedarf 400 W → Netz rechnet 86 kg/h statt 34 kg/h (Faktor 2.5), Reibungsdruckverlust rund Faktor 5–6. `heizkoerper_auslegung` rechnet den
   Massenstrom dagegen richtig aus dem Bedarf – zwei Abfragen, zwei Massenströme für denselben Heizkörper. Vorschlag: im Netz den Bedarf
   (Raum.heizleistung, verteilt wie in der Auslegung) verwenden, Normleistung nur als Rückfall mit Hinweis. Aufwand mittel, **Priorität hoch**
   (Entscheid Christian 02.11.2026 «Leistung → Massenstrom» stammt aus der Zeit vor Teil B – Frage 1).
2. **`RohrDruckverlust.cs:64–70` – Leitungen ausserhalb einer Heizgruppe haben keine Temperatur** → kein Druckverlust (Erzeuger bis Verteiler).
   Gerade die Hauptleitungen fehlen damit im massgebenden Weg. Jetzt sichtbar (Korrektur 6), aber nicht gerechnet. Vorschlag: Temperatur der
   Gruppe(n) dahinter (bei mehreren: Hinweis/Mischtemperatur – Annahme, Entscheid nötig). Aufwand mittel, Priorität hoch.
3. **Masche/Tichelmann (`HeizungLeistung.Folgen`, `SanitaerBelastung.Folgen`)**: in parallelen Wegen trägt jede Leitung den vollen Strom. Für
   Summen ΣLU/ΣW gekennzeichnet (P24/P39), für den Druckverlust physikalisch falsch (Aufteilung nach Widerstand nötig). Aufwand gross, Priorität
   mittel; bis dahin nur Hinweis.
4. **`KanalDruckverlust.cs` / `RohrDruckverlust.cs` – Rauheit aus dem Seriennamen per Stichwort.** Edelstahl/C-Stahl-Presssysteme (Mapress,
   Optipress) fallen auf «Stahl gewalzt» k = 0.05 mm (sichere Seite, aber nicht belegt); im Rohr-Muster steht `pb` ohne Wortgrenze. Sauberer:
   Feld «rauheit» (Schlüssel aus stroemung.json bzw. Herstellerwert mit Quelle) an der Produktlinie. Aufwand mittel, Priorität mittel.
5. **`HeizkoerperAuslegung.cs:77` – ΔT logarithmisch im Betrieb, ΔTn arithmetisch.** Ein Heizkörper exakt bei Normbedingungen (75/65/20) erhält
   f = (49.83/50)^1.3 = 0.9957 statt 1.000. Als Annahme im Ergebnis genannt («wie Büroblatt»); konsistent wäre beides logarithmisch oder
   beides arithmetisch. Wirkung < 0.5 %, Priorität tief – Frage 3.
6. **Spitzendurchfluss unter dem Kurvenstart uneinheitlich.** Teil A (`SanitaerKurve.Spitze`) rechnet die Kurve auch unter 0.3 l/s (V = 0.1 →
   Q_D = 0.204 l/s, grösser als der Gesamtvolumenstrom) und gibt nur einen Hinweis; Teil B (`SanitaerDimensionierung.Spitze`) setzt dort
   Q_D = Q_T. Vorschlag: Teil A wie Teil B. Aufwand klein, Priorität mittel (Auslegungsregel → Frage 4).
7. **`SanitaerDimensionierung.cs` – Laufzeit.** Je Abwasserleitung `NichtKleiner` → `Netz.Folgen` stromauf, darin je Leitung `Abwasser()` →
   `Leitungsart` → `SteilStromab`/`MuendetInAnschluss` → wieder `Netz.Folgen`; `AnschlussLaenge` ruft im `stopp` je besuchtem Element erneut
   `Leitungsart`. Schlimmstenfalls O(n³) Netzläufe; bei grossen importierten Netzen (einige tausend Leitungen) spürbar. Vorschlag: `Leitungsart`
   je Leitung einmal zwischenspeichern. Aufwand klein–mittel, Priorität mittel.
8. **`LueftungKette.cs:~262` – Brandschutzklappen.** `eigen = geraetRaum.Brandabschnitt`; der Geräteraum (Kandidat «Technik», oft ohne
   Luftmenge) wird bei der Prüfung «alle Räume haben einen Brandabschnitt» nicht mitgeprüft. Hat er keinen, zählt auch der Abschnitt, in dem
   das Gerät tatsächlich steht, als Klappe (eine je Medium zu viel). Vorschlag: Befund «Brandabschnitt des Geräteraums fehlt». Aufwand klein,
   Priorität tief.
9. **`LueftungKette.cs:~200` – Steigzone über mehrere Gebäude.** `stockwerke` nimmt alle Geschosse des Modells zwischen den Koten, auch die
   eines zweiten Gebäudes (zusätzliche Abschnitte, falsche Schachthöhe). Aufwand klein, Priorität tief (nur bei mehreren Gebäuden).
10. **Kein Endwert-Test für `rohr_druckverlust`.** Der einzige Test prüft die Wasserwerte und «ohne Serie kein Druckverlust»; die Kette
    ṁ → w → Re → λ → Δp ist nie mit einer Zahl abgesichert (Testnetz mit Produktlinie fehlt). Vorschlag: ein Test mit Serie und dem Handbeispiel
    oben. Aufwand klein–mittel, Priorität mittel.
11. **NaN/∞ allgemein.** Dieselbe Ausnahme droht überall, wo eine Normtabelle unvollständig ist (z. B. `Heizlast.cs:343` fg1 = NaN ohne
    ND.5-Zeile, `:382` n_min fehlt, `:406` e fehlt). Vorschlag: an der Tür (`Tuer.Abfrage`) einmal alle nicht-endlichen Zahlen durch null +
    Hinweis ersetzen. Aufwand klein, Priorität mittel.
12. **`Heizlast.cs:85/94` – f nach ND.5.7** wird «mechanisch», sobald *ein* Raum Zu-/Abluft hat. Annahme, im Ergebnis nicht genannt. Priorität tief.
13. **`Heizlast.cs:211–220` – B′** summiert Bodenfläche und Umfang über alle Räume des Modells (auch unbeheizte, auch mehrere Gebäude), mit
    Innenmassen. Als Annahme V1 vermerkt; bei mehreren Gebäuden falsch. Priorität tief.
14. **Kanal-Druckverlust: Formteile ohne Länge.** Bögen/T-Stücke zählen weder als ζ (fehlt bewusst) noch als Länge – korrekt dokumentiert;
    der Wert ist als «nur Reibung gerade Strecken» zu lesen, typ. 30–60 % des Gesamtdruckverlusts. Kein Fehler, aber im Ergebnis deutlicher
    zu nennen (Frage 2).

## 5. Offene Fragen an Christian

1. **Rohrnetz Heizung:** Soll der Massenstrom je Heizkörper aus dem **Bedarf** (Raum-Heizlast, verteilt) statt aus der Normleistung kommen,
   sobald eine Heizlast vorliegt? (Heute Faktor 2–3 zu viel bei Niedertemperatur.)
2. **Leitungen vor dem Verteiler** (ohne Heizgruppe): mit welcher Temperatur rechnen – Gruppe dahinter, höchste VL-Temperatur, oder Eingabe am Strang?
3. **Heizkörper:** ΔTn arithmetisch (wie Büroblatt) belassen oder ebenfalls logarithmisch (dann Faktor 1.000 bei Normbedingungen)?
4. **Spitzendurchfluss unter 0.3 bzw. 0.5 l/s:** In der Zusammenstellung (Teil A) wie in Teil B Q_D = Q_T setzen?
5. **Rauheit:** je Produktlinie als Feld mit Quelle führen (Hersteller) statt aus dem Namen zu raten? Welcher Wert für Edelstahl-/C-Stahl-Pressrohr?
6. **ζ-Werte:** Ohne Einzelwiderstände ist der Druckverlust nicht für die Pumpen-/Ventilatorwahl brauchbar. Gibt es eine Quelle (Hersteller-
   Unterlagen, Lehrmittel HFGT16), die übertragen werden darf?

## Hinweis zum Ablauf

Bei jedem Commit meldete Git «failed to delete …/.git/worktrees/wt_b1 / wt_b2: Permission denied» (Aufräumen fremder, verwaister Arbeitskopien);
die Commits selbst sind in Ordnung. Nichts gepusht, keine Marken.
