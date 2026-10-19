# Modul 3 – Leitungen und Schema: Spezifikation

Stand 18.10.2026 (Hauptlauf). Start auf Christians Zuruf vom 18.10.2026 («Ja, starte mit Modul 3 Leitungen und
Schema»; geplant 25.01.–11.02.2027, 14 Tage + 1 Tag HKAP). Grundlagen: Factsheet (Modul 3 «Leitungen, Schema»),
Vorgaben Christian vom 17.10.2026 im Wissensspeicher (Schema als verknüpfte Ansicht, Elemente zuerst nur im Schema,
Konsistenzprüfung), Zusagen aus Modul 2 (Leitungsserien, Formteil-Familien, Formteil-Regeln; von Christian am
18.10.2026 bestätigt), Bestandsaufnahme vom 18.10.2026 (unten).

## Ziel

Leitungen zeichnen wie in Revit: Rohr- bzw. Kanaltyp mit Grösse wählen, Verlauf ziehen, die Formteile setzt KICAD selbst
(Bogen am Knick, T-Stück am Abzweig, Reduktion beim Grössenwechsel), angeschlossen wird an die Anschlüsse von Geräten
und Familien. Dazu das **Schema als verknüpfte Ansicht** desselben Modells: ein Ventil im Grundriss erscheint im Schema,
eine Verbindung im Schema ist eine Verbindung im Modell.

**Fertig, wenn …** (Factsheet, präzisiert)
1. Ein **Sanitärstrang** (Trinkwasser kalt/warm und Schmutzwasser) im Beispielprojekt ist mit echten Leitungsserien aus
   Herstellerunterlagen gezeichnet: Formteile automatisch, alle Anschlüsse verbunden, Gefälle im Abwasser; er stimmt in
   **Grundriss, 3D und Schema gleichzeitig** überein (gleiche Elemente, gleiche Verbindungen, Änderung an einer Stelle
   erscheint überall).
2. **Seine Stränge stehen in HKAP** (Austauschstelle, Datenart «straenge», gegen die HKAP-Testkopie).
3. Masse und Lagen (Einbaulängen, Bogenlagen, Gefälle, Längen je Serie und Grösse) stimmen mit einer Handrechnung.
4. Alles über MCP, mit Trockenlauf, rückgängig; Prüfungen melden offene Enden, Knicke ohne Formteil, Grössensprünge,
   falsches Gefälle, Schema ohne Modellleitung.

## Bestand (18.10.2026, Kurzfassung)

Leitung = eine Polylinie mit Anschlüssen anfang/ende; `leitung_ziehen` setzt keine Formteile, prüft weder Lage noch
Grösse; Formteile sind Punkte ohne Einbaulänge (im Beispielprojekt von Hand gesetzt); Anschlusslagen gibt es nur bei
Familien; der Fang kennt keine Anschlussnamen und kein z; keine Serien; Darstellung nicht massstäblich (Grundriss 0.6·DN,
3D 1.0·DN); `Ansicht.art = schema` und `Ansicht.lagen` nur als Katalogtext, Ansicht zeigt einen Platzhalter; HKAP kennt
keine Stränge (nur `strangIds` am Raum).

## Grundsatzentscheide (Hauptlauf)

1. **Ein Leitungselement je Gerade** (wie Revit). Am Knick sitzt ein Bogen, am Abzweig ein T-Stück; die Leitung endet an
   dessen Anschluss (Einbaulänge). Grund: richtige Längen und Mengen, Kollision (3b), IFC-Export (IfcPipeSegment gerade),
   Teilstrecken für die Hydraulik (Module 4–6). Bestehende Polylinien bleiben lesbar und zeichenbar; der Befehl
   `leitung_aufteilen` macht daraus Geraden mit Formteilen.
2. **Leitungsserie = Familie der Kategorie Rohr bzw. Kanal** («Rohrtyp» in Revit). Die Typen sind die Grössen (je DN bzw.
   Querschnitt: Aussen- und Innendurchmesser, Wandstärke, ggf. Gewicht und Rauheit, je mit Quelle), dazu ein Block
   `leitung` mit Querschnittsform, den Formteil-Familien je Art und den Regeln. So gelten Editor, KI aus Unterlagen,
   Quellenprüfung, Konflikte und Freigabe aus Modul 2 unverändert. Werte nur aus Unterlagen – Normwerte und
   Rohrabmessungen nie aus dem Gedächtnis, keine DN aus dem Aussendurchmesser.
3. **Formteil-Familien** (Bogen, T-Stück, Reduktion, Reinigungsöffnung; für Rohr und Kanal). Die Formteil-Klassen werden
   familienfähig. Familien bekommen eine **volle Ausrichtung im Raum** (`ausrichtung` {x, z}), damit Steig-, Fall- und
   Neigungsbögen gehen; `drehung` um z bleibt für Geräte.
   Lokale Lage der Formteile: Ursprung = Schnittpunkt der Achsen. Bogen: Anschluss a bei (−La, 0, 0), Richtung (−1, 0, 0);
   b bei Lb·(cos w, sin w, 0), Richtung (cos w, sin w, 0), w = Umlenkwinkel. T-Stück: a (−L, 0, 0)/(−1, 0, 0),
   b (L, 0, 0)/(1, 0, 0), c (0, Lc, 0)/(0, 1, 0). Reduktion: a im Ursprung/(−1, 0, 0), b (L, 0, 0)/(1, 0, 0).
4. **Anschlusslagen für alle Elemente**: Leitungen (Enden, Richtung nach aussen), Formteile und Familien (aus der
   Familie), Geräte ohne Familie (keine Lage – nur logisch verbindbar). `verbinden` prüft Lage (Abstand ≤ 1 mm,
   Richtungen gegenläufig), Medium, Fluss und Grösse; bei Abweichung abgelehnt mit Vorschlag.
5. **Routing im Kern**, nicht in der Oberfläche: `leitung_zeichnen` {serie, groesse, punkte, von?, nach?, gefaelle?,
   system/strang} legt Geraden und Formteile nach der Serie an, kürzt um die Einbaulängen, verbindet alles und meldet,
   was nicht passt (Winkel nicht in der Serie, kein Formteil für die Grösse). Dazu `leitung_abzweigen` (T-Stück in eine
   bestehende Leitung, Leitung teilen), `leitung_groesse` (Grössenwechsel samt Reduktionen), Nachführung beim Verschieben
   von Geräten und Formteilen (angeschlossene Leitungsenden folgen, Formteile werden neu gesetzt).
6. **Schema = Ansicht des Systemgraphen** mit eigener Lage je Element (`Ansicht.lagen` {element: {x, y, manuell}}):
   automatisch angeordnet (Strangschema: Spalte je Strang, Zeile je Geschoss nach Kote; Endgeräte am Strang auf ihrem
   Geschoss; Sammel- und Grundleitungen unten), von Hand verschobene Lagen bleiben (`manuell`). Leitungen erscheinen als
   orthogonale Linien zwischen den Lagen ihrer Enden; Symbole aus `schemasymbol` der Familie, sonst aus dem Sinnbild der
   Klasse. Bearbeiten im Schema (verbinden, trennen, Element einfügen) wirkt im Modell. Elemente dürfen **zuerst nur im
   Schema** stehen (ohne Geometrie = «nicht platziert», wie die Räume aus HKAP in 1d).
7. **Darstellung massstäblich**: Leitungen und Formteile mit Aussendurchmesser bzw. Kanalmass aus der Serie (fehlt die
   Serie: wie heute, mit Hinweis), Dämmung als zweite Kontur (`daemmdicke` mm), Formteile aus ihrer Familie
   (Grundriss, Schnitt, 3D), Leitungen von der Schnitthöhe erfasst (oberhalb gestrichelt wie Revit «über der
   Schnittebene»).
8. **HKAP**: neue Datenart «straenge» im Austauschvertrag: KICAD führt Stränge (Kennzeichen, Name, System, Systemtyp,
   Medium, Geschosse, Anzahl Endgeräte, Leitungslänge je Grösse); HKAP zeigt sie an der Projektierung, ein in HKAP
   geplanter Strang kommt als «nicht platziert» nach KICAD. HKAP-Code ändert nur der Hauptlauf, getestet gegen die
   Testkopie 5098, ausgeliefert erst nach Christians Freigabe (zusammen mit 1d).

## Datenmodell (Hauptlauf, Phase A)

| Klasse / Eigenschaft | Inhalt |
| --- | --- |
| Rohr, Kanal | familienfähig (Familie = Leitungsserie); `familientyp` = Grösse; neu `daemmdicke` (mm), `daemmung` (Art, Text); `gefaelle` (%) wie bisher; Geometrie: Gerade (zwei Punkte, Achse in mm absolut) |
| Formteil (Bogen, TStueck, Reduktion, Reinigungsoeffnung) | familienfähig; `winkel` am Bogen bleibt (Exemplarwert der Familie); Grösse je Anschluss (dn bzw. d, b/h) |
| Familienelement | neu `ausrichtung` {x: [..], z: [..]} (lokale x- und z-Achse in Weltkoordinaten, orthonormal) – überschreibt `drehung` |
| Familienformat | neuer Block `leitung` für Kategorie Rohr/Kanal: `querschnitt` (rund, rechteck), `groesse` (Parameternamen: dn bzw. d, b, h), `aussen` (Formel, z. B. «da»), `innen`, `wand`, `formteile` {bogen, tstueck, reduktion, reinigungsoeffnung: {familie, typ (Formel bzw. Regel nach Grösse)}}, `regeln` {winkel: [Liste] oder «frei», biegeradius?, abzweig: «tstueck»} |
| Ansicht (art schema) | `lagen` {element: {x, y, manuell}}; neu `schemaart` (strangschema, prinzipschema), Filter wie Grundriss (Gewerke, Systeme) |
| Element ohne Geometrie | erlaubt für Endgeräte, Armaturen, Formteile, Leitungen = «nicht platziert» (nur im Schema); Prüfung meldet es als Hinweis |

## Befehle und Abfragen (Auswahl)

| Name | Zweck |
| --- | --- |
| `leitung_zeichnen` | Verlauf mit Serie und Grösse → Geraden + Formteile, verbunden; Trockenlauf = Vorschau mit Längen und Formteilen |
| `leitung_abzweigen` | Abzweig an einem Punkt einer bestehenden Leitung (Leitung teilen, T-Stück, neue Leitung) |
| `leitung_aufteilen` | bestehende Polylinie in Geraden mit Formteilen |
| `leitung_groesse`, `leitung_serie` | Grösse bzw. Serie eines Leitungszugs ändern, Formteile nachführen |
| `verbinden` (erweitert) | mit Lage-, Grössen- und Richtungsprüfung |
| `schema_lage` | Lage eines Elements im Schema setzen (manuell) bzw. zurücksetzen |
| `schema_anordnen` | automatische Anordnung neu rechnen (manuelle Lagen bleiben, ausser `alle`) |
| Abfragen `anschlusslagen` (alle Elemente), `leitungszug` (zusammenhängende Leitungen mit Längen), `materialliste` (je Serie, Grösse: Länge, Formteile Stück) | lesen |

## Prüfungen (neu)

P13 Leitungsende nicht an seinem Anschluss (Lage) · P14 Grössensprung ohne Reduktion · P15 Knick ohne Formteil bzw.
Winkel nicht in der Serie · P16 Gefälle fehlt oder falsch (Schwerkraftsystem: z-Verlauf gegen `gefaelle`) · P17 nur im
Schema (nicht platziert, Hinweis) · P18 Leitung ohne Serie (Hinweis).

## Oberfläche

- Register Gebäudetechnik: **Rohr** (PI), **Kanal** (DT) mit Optionsleiste: Serie, Grösse, Höhe (Achse über OK Boden),
  Gefälle, System/Strang; Fangen an Anschlüssen (3D-Lage, Anschlussname, frei/belegt), Zifferneingabe der Länge,
  Höhenwechsel (Steigleitung) per Eingabe, Abzweig durch Klick auf eine bestehende Leitung.
- Griffe an Leitungsenden, Eigenschaften (Serie, Grösse, Dämmung, Gefälle), Formteile in der Palette.
- **Schema-Ansicht** (anlegen im Register Ansicht): Symbole, Leitungen, Beschriftung (Kennzeichen, Grösse), Element
  verschieben, Verbinden, «Im Grundriss zeigen» bzw. «Im Schema zeigen», nicht platzierte Elemente aus dem Schema
  platzieren.
- Listen: Materialliste je Serie, Strangliste mit Längen je Grösse.

## Phasen und Zuteilung

| Teil | Inhalt | Wer | Dateien |
| --- | --- | --- | --- |
| A | Spezifikation, Datenmodell (Katalog, Schema, Familienformat `leitung`, `ausrichtung`), fiktive Testserie und Formteil-Testfamilien, Einhängepunkte | Hauptlauf | `entwurf/modul-3.md`, `kataloge/`, `schema/`, `tests/daten/familien/` |
| B1 | Familien mit voller Ausrichtung; Formteil-Familien (Bogen mit Winkel, T, Reduktion); Anschlusslagen für alle Elemente; `verbinden` mit Lage-/Grössenprüfung; Prüfungen P13–P15 | hlks-kern | `src/Hlks.Kern/Familien/`, `src/Hlks.Kern/Leitungen/` |
| B2 | Routing: `leitung_zeichnen`, `leitung_abzweigen`, `leitung_aufteilen`, `leitung_groesse/serie`, Einbaulängen, Gefälle, Nachführung beim Verschieben, P16 | hlks-kern | `src/Hlks.Kern/Leitungen/` |
| B3 | Schema-Kern: Anordnung (Strangschema), Grafikquelle, `lagen`, `schema_lage/anordnen`, nicht platzierte Elemente, P17 | hlks-kern | `src/Hlks.Kern/Schema/` |
| B4 | Darstellung massstäblich (Serie, Dämmung, Formteile aus Familien, Schnitthöhe), Mengen/Strangliste/Materialliste | hlks-modul | `Grafik/Hlks*.cs`, `Api/Ableitungen.cs` |
| H | HKAP: Datenart «straenge» (Austauschstelle, Anzeige an der Projektierung) | Hauptlauf (HKAP-Code) | `AufgabenProtokolle.Web/` |
| B5 | KICAD-Seite «straenge» (Abbildung, Abgleich, nicht platzierte Stränge) | hlks-modul | `src/Hlks.Kern/Austausch/` |
| C1 | Oberfläche Leitungen zeichnen (Werkzeuge, Anschlussfang 3D, Optionsleiste, Griffe, Palette) | hlks-modul | `wwwroot/js/…` |
| C2 | Oberfläche Schema-Ansicht | hlks-modul | `wwwroot/js/…` |
| B6 | Referenzfälle von Hand (Einbaulängen, Bogenlagen, Gefälle, Mengen, Schema-Anordnung) | hlks-test | `tests/…` |
| D | Gesamttest, Sanitärstrang im Beispielprojekt mit echten Serien (KI aus Unterlagen), HKAP gegen Testkopie, Testbericht | Hauptlauf | |

Reihenfolge: A → B1, B3, H parallel → B2, B4, B5, C2 → C1, B6 → D. Vor dem Gesamttest: diese Tabelle gegen
`zuteilung.md` abgleichen.

## HKAP-Datenart «straenge» (Phase H, Hauptlauf, 18.10.2026)

HKAP kennt Stränge schon (`ProjStrang` je Projektierung: Id ST<n>, Nummer «S_01» nach Gewerk, Bezeichnung, Medium aus
Zuluft, Abluft, Zu-/Abluft, Fortluft, Aussenluft, Heizung, Kälte, Sanitär; Temperaturen VL/RL, Anschluss, Strangpunkte je
Etage). **Ein HKAP-Strang bündelt mehrere KICAD-Stränge**, weil KICAD je System (ein Medium) einen Strang führt: «Heizung
H_01» = KICAD-Stränge Vorlauf und Rücklauf, «Sanitär S_01» = Trinkwasser kalt, warm, Zirkulation, Schmutzwasser.

| Feld | Typ | Richtung | führt |
| --- | --- | --- | --- |
| id | text | beide | hkap |
| nummer, bezeichnung | text | beide | zuerst |
| medium | text (nur die acht HKAP-Medien) | beide | zuerst |
| tempVL, tempRL (°C), anschluss, anschlussEtageId | | hkap>kicad | hkap |
| etagen (Etagen-Ids mit Strangpunkt) | liste | hkap>kicad | hkap |
| kicad { straenge: [{ id, kennzeichen, name, system, systemtyp, geschosse [HKAP-Etagen-Ids], endgeraete, laengen [{ serie, groesse, laengeM }] }] } | objekt | kicad>hkap | kicad |

Schreiben ohne id legt einen HKAP-Strang an (Id und Nummer wie im Editor, Farbe aus der Palette). Der Block `kicad` wird
ganz ersetzt; gleicher Inhalt gilt als unverändert. HKAP setzt `kicad.felder` (welche Felder zuletzt aus KICAD kamen) und
`kicad.stand`. Im HKAP-Editor: Marke «KICAD» in der Strangliste, im Strang-Panel «aus KICAD» mit den KICAD-Strängen.
**KICAD-Seite (B5):** Zuordnung über die Kennung `hkap` am KICAD-Strang (mehrere KICAD-Stränge dürfen dieselbe HKAP-Id
tragen); Medium aus dem Systemtyp abbilden (Trinkwasser*, Zirkulation, Schmutzwasser, Regenwasser → Sanitär; Heizung
VL/RL → Heizung; Kühlwasser → Kälte; Zuluft/Abluft/Fortluft/Aussenluft gleichnamig; Sole → offen, Prüfliste); ein in
HKAP angelegter Strang ohne KICAD-Gegenstück kommt als nicht platzierter Strang (Element Strang ohne Leitungen, System nach
Medium wählbar) nach KICAD; Leitungslängen je Serie und Grösse aus den Mengen (B4).

## Nicht in Modul 3

Hydraulik und Dimensionierung (Module 4–6), Kollision und Aussparungen (3b), Leitungsführung automatisch zwischen zwei
Punkten (Wegsuche), Flexrohre und Schläuche als Bögen, Hosenstücke als eigene Klasse (über T-Stück-Familien), IFC-Export
(Modul 7).

**Prinzipschema → Heizung in Modul 5, Lüftung in Modul 6** (Entscheid Christian 18.10.2026): Anordnung nach dem
Fliessweg ohne Geschossraster (Modul 5: Erzeuger, Speicher, Verteiler, Gruppen mit Pumpen, Ventilen, Mischern; Modul 6:
Lüftungsgerät und Kanalnetz mit Klappen, auf der Anordnung aus Modul 5 aufbauend). In Modul 3 bleibt `schemaart: prinzipschema` wählbar und wird wie ein Strangschema angeordnet (mit Hinweis).
Modul 3 baut nur das Strangschema; die Sanitärschemas Wasser/Abwasser/kombiniert sind Strangschemas mit Systemfilter.

## Entscheide während des Baus

- **18.10.2026, Phase A (Hauptlauf):**
  - Einhängepunkte: `Leitungen/LeitungenBefehle.cs` (LeitungenBefehle, LeitungenAbfragen, LeitungenPruefung) und
    `Schema/SchemaBefehle.cs` (SchemaBefehle, SchemaAbfragen, SchemaPruefung, SchemaGrafik); Prüfcodes P13–P18 stehen in
    `Pruefungen.Codes`; `Grafik.Plan` reicht Ansichten mit `art = schema` an `SchemaGrafik.Plan` weiter.
  - Testdaten (alle Werte FIKTIV): `tests/daten/familien/test_rohrserie` (DN 15–50, da = DN + 5), `test_bogen`
    (Einbaulänge r·tan(w/2), r = 2·da), `test_tstueck` (nur gleiche Grössen, l = lc = da), `test_reduktion` (l = 40 mm).
  - **Formteiltyp:** ohne Regel `typ` der Typ, dessen ausgewertete Anschlussgrössen passen (Bogen a = b = Grösse,
    T-Stück a = b = Durchgang und c = Abzweig, Reduktion a = gross, b = klein) – nicht über Parameternamen.
  - **Leitungsfamilien** haben weder Körper noch Anschlüsse: die Nachführung setzt an Leitungen nur die Grösse und die
    Eigenschaften mit «ziel», Anschlüsse `anfang`/`ende` bleiben; gezeichnet werden Leitungen weiter von HlksGrafik (ab B4
    massstäblich aus der Serie), nicht von FamilienGrafik.
  - **Strenges `verbinden`** gilt für den Befehl (Mensch, KI, Oberfläche); der IFC-Import verbindet weiter nach seinen
    Ports – Abweichungen meldet dort P13.
  - **Nicht platzierte Elemente** (ohne Geometrie) haben Anschlüsse ohne Lage; sie verbinden logisch wie Geräte ohne
    Familie.
  - **Schemalagen:** die automatische Anordnung wird bei jedem Lesen deterministisch gerechnet und nicht gespeichert;
    `Ansicht.lagen` hält nur die von Hand gesetzten Lagen (`manuell: true`). `schema_anordnen` räumt auf (Lagen
    gelöschter Elemente) bzw. setzt mit `alle` alles auf automatisch zurück. Grund: das Modell ändert sich nicht bei jeder
    Änderung im Netz (keine Pakete und Synchronisationskonflikte wegen verschobener Automatik-Lagen).
  - **B1 (18.10.2026, vom Hauptlauf übernommen, Annahmen für die Prüfliste):** Rohrserien sind immer rund;
    Grössenparameter sind Typparameter mit Zahl. Typregeln ergeben den Typnamen als Text (Formelsprache ohne
    Textverkettung → `if()`-Ketten), Namen `dn`, `dn_2` usw. Passen mehrere Formteiltypen, gilt der erste (Hinweis);
    Reinigungsöffnung wie ein gerades Stück (a −x, b +x); fehlt `regeln.winkel`, ist jeder Winkel erlaubt. Ausrichtung:
    Toleranz 1e-6 nach Normieren, kleine Abweichungen werden gerade gerichtet, ungültig gespeicherte fallen auf `drehung`
    zurück; Sinnbild nur bei senkrechter lokaler z-Achse, sonst Umriss. Nicht platzierte Familienelemente werden
    nachgeführt. Elemente ohne Familie (ausser Leitungen) haben keine Anschlussgrösse → `verbinden` prüft dort weder Lage
    noch Grösse; Grösse wird auch geprüft, wenn eine Seite keine Lage hat. Knick zwischen zwei Leitungen = P15 (nicht
    zusätzlich P13); P18 meldet jede Leitung ohne gültige Serie (im Beispielprojekt je Rohr ein Hinweis). API für B2:
    `Leitungsserie` (Formteilwahl mit `FormteilWahl.Exemplar`/`Geometrie`), `Formteilgeometrie.Ausrichtung`,
    `Anschlusslagen.PasstRaeumlich`; Ablauf Bogen im Test `AusrichtungTests.Formteilausrichtung_AusRichtungen_FuerDasRouting`.
  - **B3 (18.10.2026, Annahmen für die Prüfliste):** Raster in Papier-mm (`SchemaRaster`: Rand 30, Spalte 70,
    Systemabstand 10, Stranglinie 10 ab Spaltenrand, Zeile 50, Knoten ab 20 im Schritt 6, Endgeräte auf 42, erstes 15
    rechts der Stranglinie, dann alle 13); Symbole und Schriften 1.6–2.5 mm, Grössen nur am längsten Abschnitt ≥ 12 mm
    («NW 25 / NW 20»); Schemasymbol der Familie in Papier-mm, auf senkrechten Strängen um 90° gedreht; Endgerät an mehreren
    Systemen steht am ersten Strang in Spaltenreihenfolge; Quelle/Senke/Zentrale wie Endgeräte; waagrecht sind
    verteil-, sammel-, grund- und anschlussleitung; alle Geschosse und Stränge sichtbarer Systeme erscheinen, auch leere;
    Prinzipschema vorläufig wie Strangschema (Hinweis). Hauptlauf dazu: Katalog Ansicht `systeme` (Systemfilter), P04
    meldet nicht platzierte Elemente nicht mehr (P17 genügt). **Offen:** `familie_aendern` ohne `schemasymbol_setzen`
    (nur über `familie_entwurf`), Linien höchstens ein Knick, Anordnung bei Endgeräten an mehreren Systemen,
    Prinzipschema, Laufzeit auf grossen Modellen.
  - **B4 (18.10.2026):** Schnitthöhe (oberhalb gestrichelt, geschnitten voll, Steigleitung als Ring mit Füllung
    «schnitt») gilt **vorerst nur für Leitungen mit Serie** – der Altbestand aus IFC (z. B. Kanäle unter der Decke bei
    Schnitthöhe 1000) bleibt wie abgenommen voll gezeichnet; eine Zeile (`schnittAktiv` in `HlksGrafik.Leitung`) schaltet
    es für alle ein → **Frage an Christian in der Abnahme**. Weitere Annahmen: Rohr mit Serie als Band in Medienfarbe ohne
    Kontur; Dämmung im Grundriss ab Detailgrad mittel als zwei gestrichelte Ränder, in 3D nur fein (heller Körper, nicht im
    Schnitt); Beschriftung mit Serie = Typname («DN 20»), Kanäle weiter b×h bzw. Ø d; 3D-Radius mit Serie exakt aussen/2.
    `Ableitungen.Leitungslaengen` (für B5) und `Materialliste` (Leitungen je Serie/Grösse mit Stück und m, Formteile je
    Familie/Typ/Winkel) gebaut; Mengen und Strangliste haben `laengen`.
  - **B2 (18.10.2026, Annahmen für die Prüfliste):** Winkeltoleranz ±0.5° (`Routing.WinkelToleranz`) – ein 90°-Bogen
    zwischen senkrecht und Gefälle passt damit nur bis 0.87 % Gefälle; für 1–2 % braucht die Serie passende Winkel aus
    ihren Unterlagen (oder «frei»), sonst wird abgelehnt (Frage an Christian: Toleranz 1° = bis 1.7 %?). Gefälle nur auf
    waagrecht gezeichneten Stücken, ab dem Anfang (bzw. ab dem Ende, wenn nur dieses gebunden ist); ≤ 1 mm zwischen zwei
    Formteilen → direkt verbunden; Abzweig rechtwinklig, Fliessrichtung nach System (Versorgung vom T weg, Schmutzwasser
    und Rücklauf zum T hin); `leitung_groesse`/`leitung_serie` behalten Elemente und Kennzeichen; Nachführung ohne
    Neuverlegung (Leitungsenden folgen Geräten und Formteilen, Formteile folgen nicht einer verschobenen Leitung);
    **P16 nur für Leitungen mit Serie** (Toleranz 0.1 %-Punkte, Fallstrecken ≥ 45° ausgenommen) – die Abwasserleitungen
    des Beispielprojekts widersprechen ihrem Gefälle (z. B. SW-G1-2 0.27 % statt 1 %), **offen: Beispielprojekt
    korrigieren (Hauptlauf)**. Rechteckkanal: Bögen mit b in der Biegeebene; Steig-/Fallbögen drehen den Querschnitt –
    «hochkant»-Typen und Grössenvergleich mit Lage → Modul 6. Offen: `familie_platzieren` ohne `teilVon` (Formteile
    werden nachträglich zugeordnet). Hauptlauf: zwei B1-Tests an die Nachführung angepasst (Lücke über das Rohrende,
    Drehung 11.1° mit Hinweis «Ende folgt»).
  - **B5 (18.10.2026, Annahmen):** Medium-Tabelle wie Vertrag, Sole und gemischte Gruppen ohne Medium (Hinweis); medium
    «wer zuerst», KICAD überschreibt nie einen HKAP-Wert; nummer/temperaturen/anschluss/etagen nur angezeigt; endgeraete
    = direkt angeschlossene (wie `anzahlDirekt` der Strangliste); geschosse = Geschosse der Bauteile und direkten
    Endgeräte; Bezeichnung einer Gruppe = häufigster Name; Bündeln beim Senden `buendeln` auto (gleicher Name im selben
    Medium, dann gleiche Nummer am Kennzeichen-Ende) | name | kennzeichen | nein; Heizung mit VL- und RL-System verlangt
    beim Übernehmen die Wahl `system` (auch als Liste); `hkap_senden`/`hkap_uebernehmen` schliessen Stränge jetzt ein; HKAP
    ohne Datenart «straenge» (Livedaten bis zur Auslieferung) → mit Hinweis übergangen.
  - **C2 (18.10.2026, Annahmen):** Trefferfläche Knoten ±4.5 × ±3.5 mm, Hand-Lagen auf 0.5 mm gerundet mit Einrasten
    (8 px) auf Stranglinien; beim Einfügen bekommen Endgeräte den Strang der Spalte als teilVon; Einfügen = zwei Pakete;
    «Im Grundriss zeigen» Ausschnitt ± 1.5 m; Plankopf reiner Schema-Blätter «Massstab: ohne». Offen: Param.Punkt trennt
    «WC-E.02» am letzten Punkt (Kern sollte zuerst das ganze Kennzeichen prüfen), familie_platzieren ohne Punkt,
    strukturierte Mehrdeutigkeit beim Verbinden.
  - **Lücke Abzweigwinkel (Hauptlauf, 19.10.2026, beim Planen von D gefunden):** Abwasser-Anschlussleitungen mit Gefälle
    münden schräg in Fallstränge und Sammelleitungen (Abzweige z. B. 45° oder knapp unter 90°); das T-Stück aus
    Grundsatzentscheid 3 kennt nur den rechtwinkligen Abzweig, `leitung_abzweigen` nur ±0.5° davon. **Entscheid:** T-Stück-
    Familien dürfen einen Parameter `winkel` (Abzweigwinkel w, Vorgabe 90) haben; lokale Lage des Abzweigs c bei
    Lc·(−cos w, sin w, 0), Richtung (−cos w, sin w, 0) – w = 90 ergibt die bisherige Lage (0, Lc, 0)/(0, 1, 0); bei w < 90
    zeigt der Abzweig gegen die Durchgangsrichtung a→b (Zulauf in Fliessrichtung, wie Abwasser-Abzweige). Die Serie nennt
    die erlaubten Abzweigwinkel in `regeln.abzweigwinkel` (fehlt = [90]). Nachtrag **B7 (hlks-kern)**: Formteilwahl und
    -prüfung mit Abzweigwinkel, `leitung_abzweigen` und `leitung_zeichnen` (Ende an einem T-Abzweig) mit schrägem Abzweig
    und Gefälle, fiktive Abwasser-Testserie (Bögen inkl. Winkel knapp unter 90° für Übergänge Fallstrang ↔ Gefälle).
  - **B7 (19.10.2026):** T-Stücke mit Abzweigwinkel gebaut (Formteilwahl, `leitung_abzweigen` zulaufend/zuführend
    gespiegelt, Neigung nach Gefälle, P15 für nicht erlaubte Abzweigwinkel), fiktive Abwasser-Testserie. **Entscheid
    Hauptlauf 19.10.2026 (Annahme, Prüfliste): Winkeltoleranz ±1° statt ±0.5°** (`Routing.WinkelToleranz`, gleich wie
    `verbinden`) – mit ±0.5° passte ein 88.5°-Abzweig am Fallstrang nur bei 1.75–3.49 % Gefälle und für 1–1.7 % gar nichts;
    mit ±1° deckt 90° bis 1.75 % und 88.5° 0.87–4.37 % ab. Direkt an der Grenze (≈ 1°) kann `verbinden` wegen Rundung
    ablehnen. Hauptlauf dazu: KI-Anleitung (T-Stück mit `winkel`, `regeln.abzweigwinkel`), Schema (`minItems`,
    `uniqueItems`), Katalog TStueck `winkel`. Offen: Materialliste zählt T-Stücke nicht je Abzweigwinkel; schräges T am Rand
    eines Zugs bei `leitung_serie`/`leitung_groesse`; Rechteck-T mit Winkel (Modul 6).
  - **Schemaknoten:** Netzelemente ausser Leitungen, Bögen und Reduktionen; Leitungen, Bögen und Reduktionen zwischen zwei Knoten
    werden zu einer Schemalinie zusammengefasst (Beschriftung mit den Grössen), T-Stücke sind Knotenpunkte.

- **Strangschema – Erläuterung und Entscheide Christian (Abnahme 19.10.2026):** «Ein Strangschema zeigt die Zusammenhänge der
  Anlagen als Übersicht. Es muss nicht massstäblich sein, jedoch muss die Reihenfolge der Abgänge dem System entsprechen.
  Üblicherweise wird das Strangschema strangweise abgebildet.» Abgleich Hauptlauf mit dem gebauten Stand: nicht massstäblich,
  Geschosse als Zeilen und Abgänge am Steig-/Fallstrang nach Fliessrang **passen**; **passen nicht:** (1) Gruppierung nach
  System statt nach Strang (Strang Dusche/WC über das Blatt verteilt, Apparate am ersten Strang, lange Querlinien), (2) Apparate
  je Geschoss nach Kennzeichen statt nach Abgang, (3) Stränge nach Kennzeichen statt nach Abgang an der Verteilleitung.
  Entscheide: **Abgänge nach Lage im Netz** (entlang der Leitung vom Strang bzw. von Zentrale/Hausanschluss weg);
  **Medien innerhalb eines Strangs je nach Lage in der Realität**; **Zugehörigkeit von Leitungssträngen zu einem Strang nur von
  Hand** (keine Ableitung aus dem Kennzeichen). **Beispiel Christian:** Bernstrasse Ausführung Sanitär 21.01.2025,
  «Schema Wasser» (1-0768) und «Schema Abwasser» (1-0759-4) auf W: (nur gelesen, Bilder nur lokal). Daraus und aus den
  Antworten vom 19.10.2026 → **Nachtrag B8 «Strangschema nach Beispiel»**:
  1. **Je Gewerkgruppe ein Schema** (Wasser: KW, WW, Zirkulation; Abwasser: Schmutz- und Regenwasser) – über `systeme` der
     Ansicht, mit Vorlagen «Schema Wasser» und «Schema Abwasser».
  2. **Strangweise:** jeder Strang eine senkrechte Linie durch alle Geschosse, oben ein Kreis mit der Bezeichnung (Steigzone
     bzw. Strang, z. B. «PW 1», «WAS-H 03»). Leitungsstränge mit gleicher `Strang.steigzone` (nur von Hand) laufen als
     **Bündel** nebeneinander; die Medien im Bündel nach ihrer **Lage in der Realität**.
  3. **Reihenfolge der Stränge** nebeneinander nach der **Lage im Gebäude** (Entscheid Christian), Blickrichtung je Schema
     (`Ansicht.blickrichtung`, Vorgabe von Süden); die Verteil-/Sammelleitungen im UG führen zu den Strängen.
  4. **Abgänge nach Lage im Netz:** Endgeräte je Geschoss in der Reihenfolge entlang der Etagenleitung vom Strang weg;
     Abgänge an Verteil-/Sammelleitungen nach Fliessrang.
  5. **Wohnungsblöcke** (`Ansicht.wohnungsbloecke`, Entscheid Christian «Block mit Tabelle»): Endgeräte einer Zone (Wohnung)
     je Geschoss als Block mit Titel (Zonenname) und Tabelle App./Stk.; Spalten LU/KW/WW erst mit Modul 4 (Werte nach SVGW W3
     mit Quelle); Wasserzähler und Absperrungen am Anschluss des Blocks.
  6. **Geschosse** als schraffierte Deckenbänder, Name am linken Rand innerhalb des Blatts (behebt das Abschneiden auf A2).
  7. **Beschriftung:** Dimension wie bisher; **Dämmung jetzt** (Text «Dämmung 50 mm» bzw. Produkt, aus `daemmdicke`) am
     Strangabschnitt; **Belastung in Klammern** (LU, DU, l/s) erst mit Modul 4.
  **B8 gebaut 19.10.2026, Vorführung: «passt»** (Christian); Nachtrag **B8b**: Apparate als **Sinnbilder** wie in seinem
  Schema Abwasser (WC, Waschtisch, Dusche, Badewanne, Spüle, Bodenablauf …) statt Kästchen; Beschriftungen auf gleicher Spur
  freistellen; Übergang Warmwasser → Zirkulation (Formteil zwischen zwei Systemen) klären.
  **Symbolquelle (Christian 19.10.2026):** für B8b und alle künftigen Schemas die **HKAP-Symbolbibliothek** (eingebaut
  `AufgabenProtokolle.Web/Services/SchemaSymbole.cs`, eigene `data/schema_symbole.json` – nur lesen, Testkopie).
  Prüfliste 8, 9, 19, 23 sind damit beantwortet bzw. ersetzt (19: Endgerät an mehreren Strängen steht im Bündel seiner
  Steigzone).
  **B8b gebaut 19.10.2026:** Sinnbilder aus der HKAP-Bibliothek (10 Klassen), Grössentexte freigestellt.
- **Einlagen und Grundleitungen (Christian 19.10.2026, beim Ansehen des Schemas):** «Leitungen, die im Beton eingelegt sind,
  müssen auch im Schema so gezeigt werden. Zudem werden diese Leitungen nur im Einlageplan gezeigt.» Vorbild: sein Schema
  Abwasser Bernstrasse (Einlagen laufen **im** schraffierten Deckenband, z. B. Decke über 1.UG) und Plan S07 «Sanitär Einlagen
  D.ü.2.UG» (PE-S 110 kräftig braun, Massketten magenta ab Rohbau, «Schalungsschoner», «BWA Varino horizontal» magenta
  angeschrieben, Aussparungen grau im Hintergrund). Entscheide im Fragefenster:
  1. **Erkennung automatisch, übersteuerbar:** Achse überwiegend im Beton einer Decke, Bodenplatte oder Wand → Einlage
     (Material wie Modul 3b: Typenliste > IFC-Material > Typname); unter der Bodenplatte → Erdreich; Feld
     `Leitung.verlegung` / `Formteil.verlegung` = auto | frei | einlage | erdreich (Katalog vom Hauptlauf), von Hand gesetzt
     bleibt.
  2. **Schema: nur Einlagen ins Deckenband**, alle übrigen waagrechten Leitungen bleiben, wie sie sind. Nicht massstäblich;
     das **Deckenband darf vergrössert werden**, damit Einlagen und Beschriftung Platz haben.
  3. **Grundleitungen** (im Erdreich unter der Bodenplatte) **nur im Grundleitungsplan** (wie S06), nicht im Grundriss.
  4. **Einlageplan wie S07:** Grundriss des Geschosses unter der Decke als Hintergrund (Bodenplatte: eigenes Blatt wie S08),
     Aussparungen grau, Einlagen kräftig in der Medienfarbe, Beschriftung wie B9, Massketten magenta ab Rohbau-Fixpunkten
     (Mechanik aus B4d), Endstücke angeschrieben. Einlagen erscheinen **nur** dort, nicht im normalen Grundriss.
  → **Nachtrag B8c** (Erkennung + Schema, hlks-kern), danach **B10** (Grundriss ohne Einlagen/Erdreich, Darstellungen
  «einlagen» und «grundleitungen», Planvorlagen; nach B4d wegen der Massketten). Prüfung am echten Projekt Bernstrasse
  (Christian: «komplettes Projekt inkl. aller IFC und GR-Pläne vorhanden, du kannst dich bedienen»): die Einlagen aus S07,
  S08, S09 müssen erkannt werden.
  **B8c gebaut 19.10.2026:** Erkennung `Leitungen/Verlegung.cs` (≥ 80 % der Achse im Beton, Querung > 45° zählt nicht;
  Erdreich > 50 % unter der Bodenplatte), Abfrage `verlegung` mit Planschlüsseln, Schema mit Einlagen im wachsenden
  Deckenband; Bernstrasse findet S06–S09.
- **B10 Einlageplan und Grundleitungsplan (Spezifikation Hauptlauf 19.10.2026):**
  1. **Grundriss-Filter:** In Grundrissen mit `darstellung` standard bzw. huelle erscheinen Elemente mit wirksamer Verlegung
     einlage oder erdreich **nicht** (samt ihren Beschriftungen); Schnitt, Ansicht, 3D und Schema zeigen sie weiter.
  2. **Darstellung «einlagen»** (Grundriss mit Geschoss G, `Ansicht.einlagen` = decke | bodenplatte | wand, Vorgabe decke):
     Architektur des Geschosses G grau; eigene Aussparungen des betroffenen Bauteils grau (ohne Kennfarbe); nur die
     Einlagen des Plans («Einlagen D.ü.G», «Einlagen Bodenplatte G», «Einlagen Wände G») kräftig in der Medienfarbe mit
     Formteilen; Beschriftung schwarz mit Führungslinie wie B9; magenta Bausteine: **«B.n.o» / «B.n.u»** (Bogen nach oben /
     unten, Christian 19.10.2026) am Leitungsende, das senkrecht aus dem Bauteil austritt, bei Wasser mit Medium und Ø
     («KW: B.n.o Ø16»), mehrere am selben Ort «2x B.n.o»; offenes Ende am Plattenrand «Schalungsschoner»; Ende an einem
     Endgerät bzw. Formteil im Bauteil mit dessen Namen (z. B. Bodenablauf); Massketten magenta in ganzen cm ab
     Rohbau-Fixpunkten (Mechanik B4d/B4e) zu Leitungsenden und Knicken; keine Legende.
  3. **Darstellung «grundleitungen»** (Geschoss = unterstes): Architektur grau, Leitungen im Erdreich in Medienfarbe,
     Beschriftung «ø110» schwarz, **Sohlenkote «S= -6.37 m ab 0.0»** mit Pfeil an Enden und Knicken (Sohle = Achse − di/2;
     ohne Innendurchmesser der Familie keine Sohle, sondern Achse «A=» und Hinweis – nie raten), Schächte mit Kennzeichen,
     Massketten wie oben.
  4. **Pläne anlegen:** Befehl, der je Planschlüssel aus `verlegung` eine Ansicht und ein Planblatt (1:50) mit Titel
     «<Gewerk> Einlagen D.ü.2.UG» bzw. «Grundleitungen 2.UG» anlegt (Gewerk nur, wenn alle Einlagen eines Gewerks); bestehende
     nicht doppelt.
  5. Prüfung am echten Projekt: Bilder der Pläne D.ü.2.UG, D.ü.1.UG, Bodenplatte 2.UG, Grundleitungen 2.UG neben S06–S09
     (nur lokal).
- **Apparate, Abläufe und Schächte im Schema (Christian 19.10.2026):** «Beim Schema sind die Apparate unter der Decke
  dargestellt. Diese sollen über dem Boden liegen. Gleiches auch bei den Kontrollschächten. Diese sind im oder unter dem
  Boden und müssten also eher als Schnitt gezeigt werden.» Entscheide im Fragefenster: **Grundleitungen (Erdreich) unter dem
  untersten Deckenband** zum Schacht (wie zum Pumpenschacht im Schema Abwasser); **Bodenabläufe und Terrasseneinläufe im
  Deckenband** (Oberkante bündig mit dem Boden). → **Nachtrag B8d**: Apparate stehen auf dem Boden ihres Geschosses (über
  der Etagenleitung bzw. dem Band); Schächte (Kontrollschacht, Pumpenschacht, Abscheider) als **Schnitt** von OK Boden bis
  unter die Bodenplatte, Einlauf/Auslauf seitlich, Kennzeichen senkrecht; Dachabläufe im obersten Band.
  **B10 gebaut 19.10.2026:** `Grafik/EinlageGrafik.cs` (Filter, «einlagen», «grundleitungen», Bausteine, Koten),
  `Plan/EinlagePlaene.cs` (`einlageplaene_anlegen`); Bernstrasse gegen S06–S09: Verlauf, Schalungsschoner, B.n.o/B.n.u,
  «3x B.n.o», «KW:/WW: B.n.o» wie Vorlage. Befunde Hauptlauf am Bild: Regenwasser-Einlage gestrichelt (Vorlage
  ausgezogen), D.ü.1.UG mit Massketten zu jedem Knick überladen (S09 bemasst die Austrittspunkte) → **B10b**; ohne
  Rohrgrössen aus dem IFC keine Beschriftung und keine Sohle.
- **Abwasserrohre mit Aussendurchmesser (Christian 19.10.2026):** «Praxisorientiert werden Ablaufrohre immer mit dem
  Aussendurchmesser definiert. Diese müssten so beschrieben werden (PE-S 110, 90, …). Dies ist auch bei den Exporten so zu
  übernehmen. DA = 110 → 110.» → Katalog `Leitung.da` (Hauptlauf); Beschriftung Abwasser «{kuerzel} {da}» ohne Ø und ohne
  NW/DN in Grundriss, Schema, Einlageplan; Grösse = da in Materialliste, HKAP-Längen, CSV, IFC; IFC-Import übernimmt da
  bei Rohren aus der Datei (Pset bzw. Rohrkörper = Geometrie, kein Normwert) → **B9b**. **Christian 19.10.2026 ergänzend:**
  «Alle anderen Rohre können mit ihrer NW oder DN definiert werden» – Fragefenster: Wasser- und Heizungsrohre **«CNS NW 20»
  bzw. «CNS DN 20» nach Projekteinstellung bzw. Ansicht** (`rohrbezeichnung`, Vorgabe NW), auch in den Exporten; **ersetzt
  «CNS Ø22» aus B9** für alle Nicht-Abwasser-Rohre. NW/DN nur aus Serie bzw. `Leitung.dn`, nie aus da.
  **B8d gebaut 19.10.2026** (Bild `berichte/modul-3/strangschema-b8d-abwasser.png`); Schacht-Klassen: nur Kontrollschacht im
  Katalog – Pumpenschacht, Abscheider, Hebeanlage im Code vorbereitet, Katalogklassen bei Bedarf (Modul 4).
## Annahmen für Christians Prüfliste

0. **Echte Leitungsserien für die Abnahme** – Antwort Christian 18.10.2026: «Geberit, Mapress Heizung, Mapress sanitär,
   JRG PEX». Daraus vier Serien, deren Unterlagen die KI beim Hersteller holt (Internet) und die Christian freigibt:
   Geberit Mapress für Heizung (C-Stahl, Annahme), Geberit Mapress für Sanitär (Edelstahl, Annahme), JRG PEX für
   Trinkwasser (genaues Produkt, z. B. JRG Sanipex, aus den Unterlagen), **Geberit Silent-db20** für Schmutzwasser
   (Christian 18.10.2026). **Bestätigt bei der Abnahme 19.10.2026:** Mapress C-Stahl (Heizung), Mapress Edelstahl (Sanitär),
   JRG Sanipex MT, Silent-db20; Familien freigeben als eigener Schritt. Punkte 1–3 und Winkeltoleranz ±1° wie empfohlen.
1. Ein Leitungselement je Gerade; Formteile mit Einbaulänge (Revit-Verhalten).
2. Leitungsserie = Familie der Kategorie Rohr/Kanal (Grössen als Typen).
3. Verbinden verlangt Lage ≤ 1 mm und gegenläufige Richtung; sonst abgelehnt.
4. Schema: Strangschema mit Spalte je Strang und Zeile je Geschoss; Hand-Lagen bleiben.
5'''. **Entscheide Christian 19.10.2026 (Prüfliste 22–26):** Rechteckkanal-Details auf Modul 6; HKAP-Bündelung über die von
   Hand gesetzte Steigzone (`buendeln` Vorgabe `steigzone`); P18 je Strang zusammengefasst; Auslieferung HKAP «straenge» separat mit 1d.
5''. **Entscheid Christian 19.10.2026 (Prüfliste 18):** Rohre mit Serie werden wie in Christians Schemas beschriftet –
   Werkstoff-Kürzel und Aussendurchmesser («CNS Ø22», «PE-S Ø110», «PEX Ø16») –, im Schema und im Grundriss. Das Kürzel ist
   ein Feld der Leitungsserie (Block `leitung.kuerzel`); fehlt es, bleibt der Typname. NW/DN gilt weiter für Rohre ohne Serie.
   → **Nachtrag B9**. Prüfliste 15–17, 20, 21 wie empfohlen.
5'. **Entscheid Christian 19.10.2026 (ersetzt 5):** über der Schnitthöhe ausgezogen, gestrichelt nur Verdecktes (Auslegung:
   ganz unter der Kote des dargestellten Geschosses), für alle Leitungen. Händlerablagen als Quelle akzeptiert, wenn unverändert
   vom Hersteller und Quelle vermerkt.
5. Leitungen oberhalb der Schnittebene gestrichelt, unterhalb voll (wie Revit) – vorerst nur Leitungen mit Serie; soll
   das auch für den Altbestand aus IFC gelten (dann z. B. Kanäle unter der Decke gestrichelt, wenn die Schnitthöhe
   tiefer liegt)?
6. Gefälle in % entlang der Leitung, Prüfung gegen den z-Verlauf mit Toleranz 0.1 %-Punkte (Annahme).
7. Winkeltoleranz für Bögen und Abzweige ±1° (Annahme Hauptlauf 19.10.2026, vorher ±0.5°) – reicht das, oder sollen nur
   Winkel aus der Serie ganz genau passen (dann braucht jede Gefällestufe eigene Formteilwinkel)?
