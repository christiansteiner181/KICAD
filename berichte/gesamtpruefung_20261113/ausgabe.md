# Gesamtprüfung KICAD – Bereich Ausgabe (Runde 4)

Zweig `worktree-agent-a74a9a92d37bc13ff`, Ausgangsstand `4ea7b00`. Prüfagent: Claude Fable 5.2.

**Gelesen und nachgerechnet:** `IfcExport/IfcExportGeometrie.cs`, `IfcExportSchreiber.cs`, `IfcExportDaten.cs`, `IfcExport.cs` (ganz);
`Plan/Planausgabe.cs`, `PdfZeichner.cs` (nur Abbildung, Drehung, Bogen, Bild, Text – Linienmuster stehen in grafik.md), `Plansatzausgabe.cs`,
`EinlagePlaene.cs`, `Schnittbereich.cs`, `RaumBegehen.cs`, `Revisionen.cs`, `Plannummernvorschlag.cs` (Rechenteil), `PlanBefehle.cs`
(ansicht_platzieren), `Listentabelle.cs` (Anordnen/Umbruch/Zeichnen); `Grafik/Beschriftung*.cs` (alle vier), `Leitungsbezeichnung.cs`,
`Ansichtsausrichtung.cs` (Textdrehung N9), die Freistellung in `EinlageGrafik.cs` (ab Zeile 664) und `AussparungGrafik.cs` (300–400);
`Api/Ableitungen.cs` (Längen, Materialliste, Mengen, Strang- und Raumliste), `Rechnen/SanitaerZusammenstellung.cs`.

**Nicht bzw. nur überflogen (ehrlich):** `IdsPruefer.cs`, `IfcExportBericht.cs`, `Plankopf.cs`, `Plansaetze.cs`, `PlansatzBefehle.cs`,
`PlanPruefung.cs`, `Blattverweis.cs`, `Ausgabeprotokoll.cs`, `Listentabelle.cs` Zeilen 1–440 (Aufbereiten der Abfragen), `PlanSvg.cs`
(grafik.md), `Baustelle/BaustelleExport.cs`, Raumstempel in `ArchitekturGrafik.cs`, die Massketten in `AussparungMassketten.cs`,
`SchemaGrafik.cs` (Freistellen im Strangschema). Kein Browser, kein PDF von Auge angesehen.

## 1. Kurzfazit

Die Ausgabe ist im Kern rechnerisch sauber: Blattabbildung (Massstab, Ausschnitt, gedrehte Ansicht), IFC-Platzierungskette
(Gelände → Gebäude im Projektbezug → Geschoss mit Kote → Element → Port), Einheit mm, Hohlprofile der Dämmung, Kartenbezug und
3D-Achslängen der Listen stimmen in den Handbeispielen (Abschnitt 5). Zwei echte Fehler sind korrigiert: senkrechte Rechteckkanäle standen
im IFC ohne `querDrehung` (anders als 3D und Kollisionsprüfung), und die Materialliste je System zählte Leitungen fremder Systeme mit, wenn
sie angeschlossen waren (anders als die Mengen). Der wichtigste offene Punkt ist die Freistellung der Beschriftung im Einlage-, Grundleitungs-
und Aussparungsplan: sie rechnet mit achsparallelen Rechtecken in **Modell**achsen – bei gedrehtem Grundriss (N9, Planwinkel ≠ 0) und bei
schrägen Leitungen greift sie nicht, und die Führungslinien der Hinweistexte enden neben dem Text.

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Test | Commit |
|---|---|---|---|---|---|
| 1 | `IfcExport/IfcExportGeometrie.cs` (`LeitungsKoerper`), `IfcExportDaten.cs` (`Koerper`) | Senkrechte Teilstücke eckiger Kanäle: Breite b immer in x. Das 3D (`HlksKoerper.Kanal`) und die Kollisionsprüfung (`Achskoerper.KanalLage`) drehen b um die Eigenschaft `querDrehung` (seit Runde 2 setzt sie `steigleitung_platzieren`; der IFC-Import setzt sie auch). Ein Steigkanal 400 × 200 mit `querDrehung` 90 stand im IFC um 90° verdreht gegenüber dem 3D – samt Hohlprofil der Dämmung. | `LeitungsKoerper(punkte, q, querDrehung)`: senkrecht (Grundrisslänge < 0.001 mm, dieselbe Grenze wie das 3D) RefX = (cos w, sin w, 0); waagrechte und geneigte Stücke unverändert. | neu `IfcExportTests.Kanal_Senkrecht_QuerDrehungDrehtDieBreiteImGrundriss_AuchDieDaemmung`; Filter `FullyQualifiedName~IfcExportTests`: 45 von 45 grün | `70c213c` |
| 2 | `Api/Ableitungen.cs` (`Filter`) | Materialliste und `Leitungslaengen` mit Filter `system`: eine Leitung mit eigenem System zählte zusätzlich im System jeder direkt angeschlossenen Nachbarleitung (`Netz.SystemeVon`). Bei den Mengen wurde das in Modul 5 behoben («der Bypass … zählte sonst in beiden Systemen»), hier nicht – Materialliste und Mengen je System wichen ab. Handbeispiel: RO-1 (KW, 2 m) an RO-2 (KW2, 3 m) angeschlossen → Materialliste KW zeigte 5 m, Mengen KW 2 m. | Gleiche Regel wie `Mengen`: Leitung mit eigenem System gehört genau zu diesem; ohne eigenes System und bei Formteilen gelten die Systeme der Anschlüsse. | neu `MateriallisteTests.Materialliste_LeitungMitEigenemSystem_ZaehltNichtImSystemDerNachbarleitung`; Filter `FullyQualifiedName~MateriallisteTests`: 18 von 18 grün | `33ae98c` |

Hinweis zum Testlauf: der erste gemeinsame Lauf beider Filter brach mit «Testhostprozess ist abgestürzt» ab (37 Tests gelaufen, alle grün,
kein Fehlschlag). Die Wiederholung je Filter lief durch (45 bzw. 18 grün). Ursache nicht geklärt. Der Filter `~IfcExportTests` trifft auch
`AussparungIfcExportTests` samt Echt-Test Bernstrasse (33 s) – wer nur den HLKS-Export will, filtert `FullyQualifiedName~Tests.IfcExportTests`.
Git meldet bei jedem Commit «failed to delete …/.git/worktrees/wt_b1 / wt_b2: Permission denied» (Aufräumen alter Arbeitskopien, gesperrt
durch OneDrive oder einen Prozess) – die Commits sind trotzdem da.

## 3. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 1 | `Grafik/EinlageGrafik.cs:668` (`Hinweistext`), `:822` (`Leitungsbeschriftung`), `:867` (`Freistellung`); gleiches Muster `AussparungGrafik.cs:305 ff.` | Die Freistellung rechnet mit achsparallelen Rechtecken in **Modell**achsen und kennt `ctx.Drehung` nicht. (a) Leitungsbeschriftung an einer schrägen Leitung: die Hülle des gedrehten Texts enthält die eigene Leitung (die Leitung ist in `linien`, `Frei` schliesst die eigene Id nicht aus – das Feld `Id` wird nie gelesen). Handbeispiel 1:50, Text 20 Zeichen, Schrift 2.5 mm → Text 1375 × 131 mm im Modell; Leitung unter 30°: Hülle ±343 mm quer zur Mitte, die Achse liegt rund 120 mm daneben → nie «frei», es bleibt immer die erste Lage (Mitte, oben), ohne Ausweichen. (b) Gedrehter Grundriss (N9): alle gebäudeparallelen Leitungen sind im Modell schräg → (a) gilt für den ganzen Plan. (c) Hinweistexte (Bausteine, Sohlenkoten) werden waagrecht im Modell gesetzt und dann um die Blockmitte gegengedreht (`Anker`); die Führungslinie beginnt aber an der ungedrehten Ecke. Bei 30° und 1375 mm Textbreite liegt das Linienende 2 · 690 · sin 15° ≈ 357 mm (7 mm auf dem Papier) neben dem Unterstrich; die belegte Fläche stimmt nicht mit der gezeichneten überein. | Überdeckte Texte und abgesetzte Führungslinien in Einlage-, Grundleitungs- und Aussparungsplänen, sobald der Planwinkel ≠ 0 ist oder Leitungen schräg laufen. | Platzierung in den Achsen der Ansicht rechnen (Ziele und belegte Flächen mit +Drehung drehen, platzieren, zurückdrehen; Text an seinem Einfügepunkt verankern) und für die Leitungsbeschriftung ein gedrehtes Rechteck prüfen bzw. die eigene Leitung ausnehmen. Test mit Planwinkel 30°. | mittel | hoch |
| 2 | `Grafik/Leitungsbezeichnung.cs:84`–`:88` (`SohleUkd`) | «Höchster Gefällspunkt» = Punkt mit dem grössten z. Hat die Polylinie ein senkrechtes Anfangsstück (Fallstück und liegende Leitung in einem Element), ist das der Kopf des Fallstücks; der Nachbar liegt senkrecht darunter → keine Sohlenbeschriftung. | Solche Leitungen bleiben ohne «Sohle = … UKD». Selten (Leitungen werden am Formteil getrennt). | Höchsten Punkt nur unter den Enden nicht senkrechter Stücke suchen. | klein | tief |
| 3 | `Grafik/Leitungsbezeichnung.cs:66`–`:68` (`HoeheUkd`) | «erstes waagrechtes Stück» prüft nur «nicht senkrecht»; bei geneigtem erstem Stück gilt die Höhe an seinem Anfang. Eckiger Kanal: OK = Achse + h/2 (bei Neigung h/(2 cos α)). | Vernachlässigbar bei Lüftung (kaum Gefälle); Kommentar und Code weichen ab. | Kommentar anpassen oder Stücke mit dz ≈ 0 bevorzugen. | klein | tief |
| 4 | `IfcExport/IfcExportGeometrie.cs:72` (`LeitungsKoerper`) | Je Teilstück eine stumpfe Extrusion: an Knicken einer Polylinie bleibt aussen ein Keil offen, innen überlappen die Körper. Das 3D schliesst eckige Kanäle am Knick mit einem Quader b × b × h; geneigte Kanäle zeichnet das 3D als waagrechten Quader mit mittlerer Höhe, das IFC als echte geneigte Extrusion (IFC ist hier genauer). | IFC und 3D sehen an Knicken und Gefällen leicht verschieden aus; Kollisionsprüfungen fremder Programme können am Knick eine Lücke sehen. | Entscheiden, ob Knicke im IFC geschlossen werden sollen (Kugel bzw. Quader wie im 3D) – oder so lassen, weil Bögen eigene Formteile sind. | klein | tief |
| 5 | `IfcExport/IfcExportSchreiber.cs:378`, `IfcExport.cs:119` | `IfcMapConversion.Scale` wird fest 1.0 geschrieben, der Massstab der Bezugsdatei (`IfcKarte.Massstab`) geht nicht in den Kartenbezug ein. | Nur bei Architekten-IFC mit Scale ≠ 1: Landeskoordinaten des Exports um (Massstab − 1) × Abstand zum Ursprung daneben. Hängt mit Frage 10 der Zusammenfassung (MapUnit) zusammen. | Mit der MapUnit-Frage zusammen klären, Massstab im Projektbezug speichern. | klein | tief |
| 6 | `IfcExport/IfcExportSchreiber.cs:495` | IFC2X3: Ports hängen nur an `IIfcDistributionElement` (IfcRelConnectsPortToElement). Hat ein Element mit Anschlüssen eine andere Entität (z. B. Proxy), stehen seine Ports ohne Träger in der Datei – ohne Hinweis im Bericht. | Verwaiste Ports in IFC2X3 in Sonderfällen. | Hinweis im Exportbericht bzw. solche Ports in IFC2X3 nicht schreiben. | klein | tief |
| 7 | `Grafik/BeschriftungMass.cs:107`–`:118` mit `Ansichtsausrichtung.cs:72` | Masszahl im gedrehten Grundriss: «oben» wird in Modellachsen bestimmt; klappt der Text wegen der Ansichtsdrehung um, bleibt er (bewusst) auf derselben Seite der Linie – auf dem Blatt steht die Zahl dann **unter** der Masslinie. Handbeispiel: Drehung 45°, Masslinie im Modell unter 60° → auf dem Blatt 105°, lesbar −75°; die Zahl liegt auf der Seite 195°, «oben» in Leserichtung wäre 15°. | Betrifft Massketten mit Modellwinkel zwischen 90° − Drehung und 90°; SIA 400 B.5.3 verlangt die Zahl über der Linie. | Leserichtung und «oben» in den Achsen der Ansicht bestimmen (winkel + ctx.Drehung normieren). | klein | mittel |
| 8 | `Grafik/BeschriftungSymbole.cs:101`–`:108` | Schnittsymbol: ob der Blick «waagrecht» ist (Text neben statt über der Spitze) und der Versatz der Grundlinie (−0.35 × Schrift in y) werden in Modellachsen entschieden, der Text selbst steht auf dem Blatt waagrecht. | Im gedrehten Grundriss sitzt die Bezeichnung leicht versetzt bzw. auf der falschen Seitenregel. | Blickrichtung mit ctx.Drehung in Ansichtsachsen drehen, Text an der Pfeilspitze verankern. | klein | tief |
| 9 | `Plan/Planausgabe.cs:231`–`:237` (`FreieLage`) | Für jedes vorhandene Fenster des Blatts wird die ganze Plangrafik neu gerechnet, nur um seine Grösse zu kennen. Meldung «liegt in der Blattmitte», gesetzt wird die Mitte des freien Bereichs. | Wartezeit beim Platzieren auf vollen Blättern mit grossen Grundrissen; Text ungenau. | Grösse aus Ausschnitt bzw. gemerkter Begrenzung; Text «in der Mitte des freien Bereichs». | klein | tief |
| 10 | `Plan/EinlagePlaene.cs:167`–`:169` (`Nummer`) | `int.Parse` der Ziffern einer Plannummer: mehr als 9 Ziffern nach dem Fachbuchstaben → OverflowException ohne Meldung. | Nur bei unüblichen Nummern; dann scheitert `einlageplaene_anlegen` mit einem internen Fehler. | Muster auf `\d{1,6}` begrenzen. | klein | tief |
| 11 | `Api/Ableitungen.cs:117` (`Materialliste`) | `stueckJe` gruppiert nach (Serie, Grösse), die Längenzeilen nach (Serie, Grösse, Mass). Gleiche Bezeichnung mit verschiedenem Mass ergäbe zwei Zeilen mit derselben (summierten) Stückzahl. Zeilen sind je für sich auf 0.01 m gerundet, das Total getrennt – die Summe der Zeilen kann um Hundertstel vom Total abweichen. | Kosmetisch; kein Fall im Beispielprojekt gefunden. | Stück in derselben Gruppierung zählen wie die Länge. | klein | tief |
| 12 | `Plan/PdfZeichner.cs:205 ff.` (`Schraffieren`) | Kommentar «Schraffur im Papier-Raster»: im gedrehten Fenster dreht die Schraffur ohne Achse mit der Ansicht mit (das Raster liegt im Modell, nicht auf dem Papier). Nur Darstellung, Nachbarflächen bleiben lückenlos. Nicht am PDF angesehen. | Schraffurwinkel auf dem Blatt = Winkel + Planwinkel. | Wenn 45° auf dem Blatt gewünscht: Winkel um −Drehung korrigieren. | klein | tief |

## 4. Offene Fragen an Christian

1. **Freistellung im gedrehten Grundriss** (Punkt 1): Einlage-, Grundleitungs- und Aussparungspläne mit Planwinkel ≠ 0 – ist das schon aufgefallen
   (Bernstrasse)? Freigabe, die Platzierung auf Ansichtsachsen umzubauen (mittlerer Aufwand, betrifft drei Plandarstellungen)?
2. **Masszahl über der Linie** (Punkt 7): soll die Zahl im gedrehten Grundriss immer über der Masslinie stehen (SIA 400 B.5.3), auch wenn sie
   dafür gegenüber der ungedrehten Darstellung die Seite wechselt?
3. **Knicke im IFC** (Punkt 4): Leitungen mit Knick ohne Formteil – im IFC schliessen wie im 3D oder offen lassen?
4. **Materialliste je System** (Korrektur 2): Formteile zählen weiterhin in jedem System ihrer Anschlüsse (wie bei den Mengen). Ein T-Stück
   am Übergang zweier Systeme steht damit in beiden Listen – so gewollt, oder nur im eigenen System?

## 5. Handbeispiele (nachgerechnet, stimmen)

- **Blatt ↔ Modell** (`Abbildung.Fuer`, `Planausgabe.Gebiet`): Papier = L + R(D) · (q − c) / M. Fenster L = (200, 150), 1:50, Drehung D = 90°,
  Ausschnittmitte in Ansichtsachsen c′ = (10 000, 5 000) → c = R(−90°) c′ = (5 000, −10 000). Modellpunkt q = c + (0, 1000): Papier =
  L + R(90°)(0, 1000)/50 = L + (−20, 0). In Ansichtsachsen q′ = c′ + (−1000, 0) → 20 mm links der Mitte. Gleich. Zuschnitt vor der Drehung,
  Drehung im PDF mit −D (y nach unten) – richtig.
- **Bogen im PDF**: Start −(von + sweep) = −bis, Lauf im Uhrzeigersinn um sweep bis −von: entspricht dem Bogen von → bis im Gegenuhrzeigersinn.
- **Grafischer Massstab**: 1:50, Fensterbreite 200 mm → höchstens 100 mm → 5 m = 100 mm, Mitte 2.5.
- **Nordpfeil am Fenster**: Punkt um −Nord gedreht = im Uhrzeigersinn um Nord (Nord = Winkel ab «oben» im Uhrzeigersinn). Richtig.
- **Umklappen mitdrehender Texte**: Anker q → q + oben · 0.7 · Schrift · M, links/rechts getauscht, Winkel + 180°: der Text belegt dieselbe Fläche.
- **IFC-Achsen Kanal**: Achse (1, 0, 0): RefX = z × Achse = (0, 1, 0), y = Achse × RefX = (0, 0, 1) → b waagrecht quer, h nach oben, Rechtssystem.
  Senkrecht nach unten (0, 0, −1): RefX = x, y = (0, −1, 0); Profil symmetrisch → gleich.
- **IFC-Kette**: Welt = B + R(d) · (Modell − (0, 0, 0)), Geschoss z = Kote, Element z − Kote, Port relativ zum Element (ungedreht). Mit dem
  Import (Modell = R(−d)(Welt − B)) eine Rundreise.
- **Kartenbezug**: landes.ost = O + R(k)·B, nordwinkel = k + d; Export θ = nordwinkel − d = k, Eastings = ost − (B_x cos k − B_y sin k). Mit
  B = (10 000, 5 000) mm, k = 90°, ost = 2 600 000 m: Eastings = 2 600 005 000 mm; Probe: 2 600 005 000 + (10 000 · 0 − 5 000 · 1) = 2 600 000 000. Stimmt.
- **Dämmung**: Kreis-Hohlprofil Radius r + s, Wand s; Rechteck (b + 2s) × (h + 2s), Wand s – Innenkontur = Leitung.
- **Schnittbereich**: winkel = −Planwinkel D: u = x cos D − y sin D = x-Achse der gedrehten Ansicht. **Raum begehen**: Azimut 90° − D = «oben» der Ansicht.
- **Längen der Listen**: `Geometrie.Laenge` rechnet mit dz (3D-Achslänge); (0,0,0) → (3000,0,0) → (3000,0,4000) = 7.00 m.
- **Sohle**: Sohle = Achse − Innendurchmesser/2, Mass = UK roh Decke − Sohle, positiv → «−…mm UKD». Vorzeichen richtig.
