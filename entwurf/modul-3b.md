# Modul 3b – Kollisionsprüfung und automatische Aussparungen: Spezifikation

Stand 19.10.2026 (Hauptlauf). Grundlage: Konzept `entwurf/modul-3b-kollision.md` (17.10.2026), Modul 1b (Netzdatei der
IFC-Unterlagen mit exakter Geometrie und GlobalId), Modul 3 (Leitungen als Geraden mit Serie, massstäbliche Körper mit
Dämmung, Formteil-Familien). Gestartet ohne Abnahme von Modul 3 auf Christians Auftrag vom 18.10.2026 («mache bis morgen
8:00 ohne abnahmen weiter»). Christians Voraussetzungen aus dem Konzept sind **noch offen**: Projektvorgaben (Spiel um
Leitungen, Abstände für weiche Kollisionen, Grenze Kernbohrung/Aussparung, Zusammenfassen) und BCF-Version. Darum: die
Vorgaben werden einstellbare Projektdaten **ohne eingebaute Werte** (Tests mit fiktiven Werten). BCF: aktuellste Version (Christian 19.10.2026).

## Ziel und «Fertig, wenn …»

Ziel wie im Konzept: Kollisionen HLKS ↔ Architektur/Tragwerk und HLKS ↔ HLKS (auch Fremd-IFC) finden, mit Status über
Prüfläufe; Aussparungen in Wänden, Decken, Stützen und Unterzügen automatisch vorschlagen, zusammenfassen, nummerieren,
als Plan, Liste und IFC ausgeben; eine neue Architekten-Fassung setzt «angekommen» bzw. «fehlt».

**Fertig, wenn …** Für Bernstrasse 42 (H+K-Leitungen gegen das Architekten-IFC, echte Dateien nur lokal unter
`%USERPROFILE%\HLKS_CAD_testdaten`) gibt es eine Kollisionsliste mit Sprung in die Ansichten und Status über zwei
Prüfläufe; die Aussparungen sind vorgeschlagen, zusammengefasst, nummeriert, als Plan, Liste und IFC ausgegeben; eine
neue Architekten-Fassung setzt den Status. Alles über MCP mit Trockenlauf und Rückgängig. BCF folgt nach Christians
Entscheid.

## Grundsatzentscheide (Hauptlauf)

1. **Prüfkörper je Element:** Leitungen (Geraden) als **Kapsel bzw. Quader um die Achse** mit dem Aussendurchmesser bzw.
   Kanalmass aus der Serie (Modul 3) und einer zweiten Hülle mit Dämmdicke (weich); Altbestand ohne Serie mit den Massen
   der Darstellung. Formteile, Geräte, Familienelemente als Dreiecksnetze aus ihren Körpern (Grafik.Koerper). Architektur
   und Tragwerk: **unverändert übernommene IFC-Produkte mit dem exakten Netz aus der Netzdatei** (über GlobalId), in KICAD
   gezeichnete oder geänderte Bauteile mit ihrem KICAD-Körper; nicht übernommene Produkte (Hintergrund) und Fremd-IFC
   anderer Fachplaner ebenfalls aus der Netzdatei.
2. **Breit- und Schmalphase:** räumlicher Index (Hüllquader-Baum) über alle Prüfkörper; Schmalphase hart = Kapsel/Quader
   bzw. Netz gegen Dreiecke (Schnitt **oder** Enthaltensein über Strahltest bei geschlossenen Netzen), weich = Abstand
   kleiner als die Vorgabe. Tolerierte Berührung (z. B. Leitung, die an ihrem Gerät endet; verbundene Elemente) ist keine
   Kollision.
3. **Durchdringung mit Aussparung ist keine Kollision:** eine Leitung, die durch eine Wand/Decke geht, für die eine
   Aussparung mit Bezug auf diese Leitung besteht (und die Leitung liegt in der Öffnung), wird nicht gemeldet. Ohne
   Aussparung ist sie eine harte Kollision **und** ein Aussparungsvorschlag.
4. **Kollisionen sind Elemente** (neue Klasse `Kollision`, Schicht pruefung) mit stabiler Kennung aus den Beteiligten
   (Element-Ids bzw. GlobalIds, sortiert) und der auf 100 mm gerundeten Lage – damit bleiben Status und Kommentare über
   Prüfläufe, Pakete und Rechner erhalten (Synchronisation wie alles andere). Ein Prüflauf ist ein Befehl = ein Paket:
   neue Kollisionen «neu», wieder gefundene behalten ihren Status, nicht mehr gefundene werden «behoben» (nicht gelöscht).
5. **Projektvorgaben** als ein Element der neuen Klasse `Vorgaben` (Kennzeichen VG-1) mit Werten **mit Quelle**
   (z. B. «Bürostandard H+K 2026»): Spiel um Leitungen bei Aussparungen (mm), Abstand weich je Gewerkpaar (mm), Grenze
   Kernbohrung/Aussparung (Durchmesser mm), Abstand zum Zusammenfassen (mm), Mindestabstand Leitung–Leitung (mm). Keine
   Vorgabewerte im Code; fehlt eine Vorgabe, lehnt der Befehl ab und nennt sie.
6. **Aussparung** (neue Klasse `Aussparung`, Schicht architektur): Mitte, Achsrichtung, Form rund (d) bzw. rechteckig
   (b × h, Lage um die Achse), Tiefe = Bauteildicke an der Stelle, Art kernbohrung/aussparung (Kernbohrung nur von Hand, Entscheid Christian 19.10.2026),
   Koten UK/OK (rechteckig) bzw. Achse (rund) über ±0.00 und über OK Geschoss, Bezüge: bauteil (Element-Id bzw.
   GlobalId), leitungen (Liste); Nummer je Geschoss («A-EG-001»); Status vorgeschlagen → freigegeben → versandt →
   angekommen → ausgeführt, dazu veraltet (Leitung verschoben/geändert) und fehlt (nicht in der neuen Architekten-Fassung).
   Grösse = Aussenmass (+ Dämmung) + 2 × Spiel; schräge Querung → umschliessendes Rechteck bzw. Kreis der Projektion.
   Benachbarte Aussparungen im selben Bauteil mit Randabstand < Vorgabe → eine rechteckige Sammelaussparung.
7. **IFC-Ausgabe** der Aussparungen mit Xbim.Essentials 6.1.605 (schon im Projekt): IFC4 `IfcBuildingElementProxy` mit
   PredefinedType PROVISIONFORVOID, IFC2X3 `IfcBuildingElementProxy` mit ObjectType «ProvisionForVoid» – **vor dem Bau am
   Schema der Xbim-Klassen prüfen** (wie Modul 1b), nie aus dem Gedächtnis; Exportbericht wie Modul 1b.
8. **Abgleich mit einer neuen Architekten-Fassung** (Re-Import nach Modul 1b): je Aussparung angekommen, wenn an der Stelle
   eine IfcOpeningElement/Aussparung bzw. im Netz des Bauteils eine Öffnung liegt (Strahl entlang der Leitungsachse trifft
   das Bauteil innerhalb der Aussparung nicht), sonst fehlt.
9. **Leistung:** Bernstrasse 42 (≈ 4000 HLKS-Elemente gegen ≈ 100 000 Dreiecke) in Sekunden; gemessen im Echt-Test.

## Datenmodell (Hauptlauf, Phase A)

| Klasse | Eigenschaften (Auswahl) |
| --- | --- |
| Kollision | art (hart, weich), status (neu, bestaetigt, akzeptiert, erledigt, behoben), kennung, beteiligte (liste: Id bzw. GlobalId mit Quelle), mass (mm Überschneidung bzw. Abstand), pruefung (Lauf), kommentar; Geometrie punkt |
| Aussparung | form (rund, rechteckig), d, b, h, tiefe, art (kernbohrung, aussparung), status, nummer, kote_uk, kote_ok; bezuege bauteil, leitungen; Geometrie punkt + Achsrichtung |
| Vorgaben | spiel_aussparung, abstand_weich (objekt je Gewerkpaar), grenze_kernbohrung (nicht mehr verwendet), abstand_zusammenfassen, abstand_leitungen, toleranz_hart – je Wert mit Quelle |

## Befehle und Abfragen

| Name | Zweck |
| --- | --- |
| `kollision_pruefen` { paare, geschoss?, trockenlauf } | Prüflauf, legt Kollisionen an bzw. führt Status nach |
| `kollision_status` { kollisionen, status, kommentar? } | Status setzen |
| `aussparungen_vorschlagen` { geschoss?, bauteile?, leitungen? } | Vorschläge anlegen/aktualisieren, veraltete markieren |
| `aussparung_status`, `aussparungen_nummerieren`, `aussparungen_zusammenfassen` | Pflege |
| `aussparungen_ifc` { datei, fassung (IFC4, IFC2X3), geschoss? } | IFC-Ausgabe mit Bericht |
| `vorgaben_setzen` { werte mit quelle } | Projektvorgaben |
| Abfragen `kollisionen` { status?, art?, geschoss? }, `aussparungsliste` { geschoss? } | Listen (auch als Ansicht liste) |

Prüfungen: P19 Aussparung veraltet oder fehlt in der Architekten-Fassung (warnung), P20 Leitung durchdringt Bauteil ohne
Aussparung (fehler; erst nach einem Prüflauf bzw. Vorschlag).

## Phasen und Zuteilung

| Teil | Inhalt | Wer |
| --- | --- | --- |
| A | Spezifikation, Katalog (Kollision, Aussparung, Vorgaben), Einhängepunkte `Kollision/`, `Aussparungen/`, P19/P20 | Hauptlauf |
| B1 | Geometrie-Kern: Prüfkörper je Element, Hüllquader-Baum, Kapsel/Quader/Netz gegen Dreiecke (Schnitt, Enthaltensein, Abstand), Netzdatei-Produkte laden | hlks-kern |
| B2 | Kollisionsprüfung: Paare, hart/weich, Durchdringung mit Aussparung, stabile Kennung, Status über Läufe, Befehle, Abfrage, Leistung Bernstrasse | hlks-kern |
| B3 | Aussparungen: Erkennen, Grösse, Zusammenfassen, Nummern, Status, veraltet, Abgleich neue Fassung, P19/P20 | hlks-kern |
| B4 | Ausgabe: Grafik (Kollisionsmarken, Aussparungen Grundriss/Schnitt/3D, Aussparungsplan mit Koten), Listen, IFC-Ausgabe (am Xbim-Schema geprüft), MCP | hlks-modul |
| C1 | Oberfläche: Vorgaben-Dialog, Prüflauf, Kollisionsliste mit Sprung und Status, Aussparungen vorschlagen/freigeben, Liste, Plan | hlks-modul |
| B5 | Referenzfälle von Hand | hlks-test |
| D | Bernstrasse 42 (lokal), Gesamttest, Testbericht | Hauptlauf |
| B6 | BCF in der aktuellsten Version (3.0, Entscheid Christian 19.10.2026): Kollisionen als Topics mit Viewpoint (Kamera, Komponenten über IFC-GlobalId) schreiben, Rückmeldungen (Kommentare, Status) lesen; gegen die offiziellen XML-Schemas geprüft | hlks-kern |

Reihenfolge: A → B1 → B2, B3 parallel → B4, B6, C1 → B5 → D.

## Entscheide während des Baus

- **B1 Geometrie-Kern (19.10.2026):** Prüfkörper (Leitungen als Kapsel/Quader um die Achse, sonst Netze; übernommene
  unveränderte IFC-Bauteile mit exaktem Netz), Hüllquader-Baum, Schmalphase (Möller, Innen-Test über Strahlen),
  Durchgänge mit Tiefe/Dicke/Winkel, Projektion des Querschnitts, Öffnungsprüfung. Toleranz `Geometrie3.Toleranz` 0.1 mm
  (technisch, float32 der Netzdatei). Bernstrasse 42: 7696 Prüfkörper in 2.9 s, Paare in 0.005 s, Schmalphase hart 0.32 s
  parallel (Ziel «in Sekunden» erfüllt). **Lage Bernstrasse:** H+K-IFC und Archicad-IFC liegen rund 31 m auseinander
  (Ausrichtung «projekt») – für D muss der Bezug gesetzt werden; die Revit-Datei bringt eine deckungsgleiche Architektur mit.
- **Entscheid Hauptlauf zu den B1-Befunden:** Netzdatei **Fassung 2** (Nachtrag B1b): Öffnungen (IfcOpeningElement) als
  eigene Produkte mit Wirtsbauteil und Füllung, Bauteilteile mit der GlobalId ihres Ganzen (Teile nicht doppelt prüfen;
  Ganzes ohne eigenen Körper = Teile zusammen); Fassung 1 bleibt lesbar; Regel «unverändert übernommen» an einer Stelle.
  Neue Projektvorgabe `toleranz_hart` (Durchdringungen bis zu dieser Tiefe = Berührung, z. B. aufliegende Leitungen;
  ohne Wert alles melden) – Bernstrasse: 498 von 2888 harten Befunden sind höchstens 1 mm tief.
- **B6 BCF (19.10.2026):** BCF 3.0 gebaut: `bcf_export` (Abfrage, schreibt die Datei; ändert das Modell nicht – wie
  plan_pdf), `bcf_import` (Befehl, Status und neue Kommentare), `bcf_lesen`; jede XML vor dem Schreiben gegen die XSD
  geprüft. Annahmen (Prüfliste): Topic-Guid = UUID v5 aus Projekt-Id und Kollisionskennung; Statusabbildung in beide
  Richtungen (Tabelle in BcfFormat.cs); TopicType «Kollision hart/weich», Priorität hart «Hoch»; Kamera schräg von oben aus
  Südwest, Abstand 10 × Mass (3–30 m); Koordinaten in m, Vorgabe Projektkoordinaten (Parameter `koordinaten: ifc` rechnet in
  IFC-Welt um – **Frage an Christian, welche Vorgabe**); Komponenten mit IFC-GlobalId bzw. KICAD-Id (H+K-Leitungen beim
  Partner erst nach dem IFC-Export, Modul 7, auswählbar). Offen: BCF 2.1 lesen (Schemas fehlen), Titel/Prioritäten/
  Zuweisungen des Partners werden nicht übernommen. **Für B2:** Format von `beteiligte` (B6 liest Element-Id, Kennzeichen,
  «Unterlage-Id:GlobalId», GlobalId oder Objekte {element, globalId, unterlage}).

- **B2 Kollisionsprüfung (19.10.2026):** hart = Kontakt ohne Dämmung tiefer als `toleranz_hart` (ohne Vorgabe alles), weich =
  Abstand mit Dämmung < Vorgabe (fehlt sie, entfällt die weiche Prüfung für das Paar und wird genannt – statt den Befehl
  abzulehnen); keine Kollision: verbunden, Teile desselben Elements, Leitung (Querschnitt mit Dämmung, abgetastet) ganz in
  Architektenöffnung bzw. gültiger Aussparung; Durchdringung = Querung von Wand/Decke/Dach/Stütze/Träger (IfcCovering,
  Geländer, Treppe, Fenster, Tür = Kontakt), Lage = Durchgangsmitte; Regel für offene Netze (`OffeneQuerungen`, Revit-Export
  nicht wasserdicht); Kennung mit Fang 50 mm. Bernstrasse 42 (HLKS gegen Revit-Architektur, ohne Vorgaben): 3381 hart, davon
  703 Durchdringungen, 506 höchstens 1 mm tief; Lauf ~32 s. **Entscheid Hauptlauf:** `pruefung` = erster Fund, Zähler
  `Projektinformation.kollisionslauf` – ein unveränderter Lauf schreibt nur den Zähler (Nachtrag B2b; Bernstrasse Lauf 2: 1 Operation). Damit der Zähler nicht die ganze Hülle nachrechnet, reagiert `HuelleNachfuehren` nur noch auf einen geänderten `uebergang` der Projektinformation (Hauptlauf). **Fragen an Christian:**
  Werte und Quelle der Projektvorgaben (v. a. `toleranz_hart`, Abstände weich); sollen Bekleidungen (IfcCovering) Aussparungen
  auslösen?

- **B3 Aussparungen (19.10.2026):** Grösse = Projektion mit Dämmung über die ganze Tiefe + 2 × Spiel (rund: umschliessender
  Kreis, Rechteckkanal: Rechteck), Achse = Bauteilnormale, d = Grenze noch Kernbohrung; kein Vorschlag in Architektenöffnung,
  bei Beginn/Ende im Bauteil, **längs im Bauteil oder flacher als 30°** (sonst Aussparungen bis 3.6 m in dünnen Platten);
  Zusammenfassen je Bauteil und Ebene wiederholt; Wiedererkennen über Bauteil + gemeinsame Leitung; ab freigegeben → veraltet,
  wenn eine Leitung nicht mehr hindurchpasst; Nummern «A-<Geschoss>-001» nach x, y, z; Dach ergänzt; Aussparung im Geschoss
  ihres Bauteils (Decke → Geschoss darüber). Bezug auf IFC-Hintergrund über `bezuege.unterlage` + `kennungen.bauteil`.
  Bernstrasse: 129 Aussparungen, 76 Kernbohrungen, 34 Sammelaussparungen. **Hauptlauf:** `element_loeschen` löst Bezüge
  von Aussparungen und setzt sie veraltet (statt abzulehnen). Katalog führt Aussparung in der Schicht «huelle» (nicht
  «architektur»). **Fragen an Christian:** über Bauteilgrenzen zusammenfassen (Revit-Schichten als eigene Wände ergeben
  Aussparungen nebeneinander)? Schräge Kernbohrung bzw. Rechteck statt grossem Kreis bei schräger Querung (DN 50 unter 45°
  durch 200 mm → Ø 317.8)?

- **B4b IFC-Ausgabe (19.10.2026):** Abfrage `aussparungen_ifc` (wie plan_pdf und bcf_export eine Abfrage, die eine Datei
  schreibt). Am Xbim-Schema geprüft: IFC4 `IfcBuildingElementProxy` PredefinedType PROVISIONFORVOID; IFC2X3 hat dort keinen
  PredefinedType → ObjectType «ProvisionForVoid». Körper als Extrusion um die Mitte, Pset `KICAD_Aussparung` (Nummer, Art,
  Status, Masse, Koten über ±0.00 und über OK Geschoss, Bauteil-GlobalId, Leitungen), GlobalId = UUID v5 aus Projekt und
  Element (stabil über Fassungen). Koordinaten: mit Projektbezug `ifc`, sonst `projekt` mit Warnung. Vorgabe-Filter Status
  freigegeben und versandt; «versandt» setzt nur `aussparung_status`. Datei erst nach bestandener Rundreise am Zielort.
  **Hauptlauf:** Aussparung bleibt in `kataloge/ifc.json` unter `ohneIfc` – die allgemeine Abbildung würde beim Import
  fremde Proxies als Aussparungen übernehmen; ob Aussparungen anderer Planer übernommen werden, entscheidet Modul 7.
  **Offen / Prüfliste:** Standard-Pset `Pset_ProvisionForVoid` erst nach Prüfung an der buildingSMART-Definition; Geschosse
  mit eigener GlobalId (nicht der des Architekten).
- **B4a Darstellung (19.10.2026):** Aussparung im Grundriss nach Status gefärbt (vorgeschlagen orange, freigegeben/versandt
  blau, angekommen/ausgeführt grün, veraltet/fehlt magenta), Wand: Projektion d bzw. b × Tiefe mit Kreuz, Decke: b × h bzw.
  Kreis; Aussparungsplan (`darstellung = aussparungen`): Nummer, Mass, Koten über ±0.00 und über OK Geschoss, Legende,
  Gebäudetechnik grau; 3D halbtransparent. Kollisionsmarke (Kreis mit Kreuz, 3D Kugel) nur am Bildschirm, nie im PDF.
  Listen `kollisionen` und `aussparungsliste` mit Sprung (Zeile wählt das Element). Farben, Symbolgrössen, Schrift =
  Annahmen (Prüfliste). **Befund:** Schnitt und Ansicht lassen Körper mit NichtSchneiden aus → **Nachtrag B4c** (Symbol
  über der Schnittgrafik, gleicher Stil). Hinweis: rund 3000 Kollisionskugeln in 3D können den Aufbau bremsen
  (`klassenAusblenden: Kollision`).
- **D Bernstrasse 42 (19.10.2026, Hauptlauf):** Abnahmeprojekt mit `werkzeuge/abnahme3b_bernstrasse.ps1` (Server 5151, Projekt
  lokal, Vorgaben FIKTIV mit Quelle «FIKTIV»): 2964 harte Kollisionen in 32 s, zweiter Lauf 1 Änderung, Status bleiben;
  129 Aussparungen, IFC4/IFC2X3 und BCF geschrieben, Aussparungsplan EG A1 1:100. **Befunde:** (1) Beschriftungen dicht
  liegender Aussparungen überlappen → Freistellung mit Führungslinie (Zusatz B4c). (2) Revit-Wände über mehrere Geschosse
  gaben Aussparungen das Geschoss der Wand (A-EG-… auf +5.47) → **Entscheid Hauptlauf:** Wand, Stütze, Träger: Geschoss aus
  der Höhe der Aussparungsmitte, wenn sie nicht im Band des Bauteil-Geschosses liegt; Decke/Dach unverändert. Bereits
  vergebene Nummern bleiben (neu nummerieren nur auf Befehl). Nicht mit echten Daten prüfbar: Abgleich «angekommen/fehlt»
  (keine zweite Architekten-Fassung mit Durchbrüchen) – nur Tests.
  **Neuer Lauf nach den Korrekturen (B4c, Geschoss, keine Kollisionsmarken im Aussparungsplan):** 2964 hart, Lauf 2 eine
  Änderung; 129 Aussparungen je Geschoss im Höhenband (EG 25 statt 27); Plan EG mit freigestellten Beschriftungen. **Frage an
  Christian:** Kernbohrungen erscheinen mit gerechnetem Durchmesser (z. B. Ø 70.5) – auf eine Reihe von Bohrkronen bzw.
  Rechtecke auf 5/10 mm aufrunden (Reihe und Quelle von Christian)?
- **Entscheide Christian 19.10.2026 (nach dem Bau, Antwort auf die offenen Fragen):**
  - Spiel um Leitungen **5 cm je Seite** (Aussparung = Aussenmass mit Dämmung + 2 × 50 mm; Rückfrage beantwortet).
  - **Rund (Kernbohrung) nur durch manuelle Auswahl:** `aussparungen_vorschlagen` schlägt immer **rechteckige Aussparungen**
    vor (umschliessendes Rechteck der Projektion + 2 × Spiel, auch bei runden Rohren); eine Kernbohrung entsteht nur, wenn ein
    Mensch sie für eine Aussparung wählt. Die Vorgabe `grenze_kernbohrung` entfällt; damit erledigen sich die Fragen
    «schräge Kernbohrung oder Rechteck» (automatisch immer Rechteck) und weitgehend «Bohrkronen-Reihe» (nur noch bei den von
    Hand gewählten). → **Nachtrag B3b** (hlks-kern), danach Oberfläche (hlks-modul).
  - **Zusammenfassen:** Abstand zwischen Aussparungen mindestens 10 cm, darunter zusammenfassen → `abstand_zusammenfassen` 100 mm.
  - **toleranz_hart 5 mm.**
  - **Koordinaten BCF und Aussparungs-IFC: beide wählbar** (ist so gebaut). Vorgabe des Hauptlaufs: `ifc`, wenn das Projekt
    einen Bezug hat, sonst `projekt` – für BCF wie schon für `aussparungen_ifc` (B6 hatte `projekt`).
  - **Keine Abstandsprüfung** (keine Mindestabstände Leitung–Leitung bzw. Dämmung–Dämmung): die weiche Prüfung bleibt aus,
    ohne Meldung bei jedem Lauf; die Werte bleiben setzbar, falls später gewünscht.
  - **Bekleidungen nicht melden** (IfcCovering: Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen; Bernstrasse 429 von 2964
    Kollisionen): weder Kollision noch Aussparung; als Prüfpaar zuschaltbar. → **Nachtrag B3c**.
  - **Aussparungen nur in Beton und Mauerwerk** (Antwort auf «eine durchgehende Aussparung über Schichten?»): Auslegung des
    Hauptlaufs – Aussparungen gehören zum Rohbau; vorgeschlagen werden sie nur in Bauteilen aus Beton bzw. Mauerwerk, liegen
    solche Schichten hintereinander, wird es eine durchgehende Aussparung über diese Schichten; Dämmung, Leichtbau
    (Gipsständer, Holz) und Bekleidungen bekommen keine. Material aus dem IFC bzw. dem Aufbau; Erkennung und Verhalten bei
    unbekanntem Material = Annahme für die Prüfliste. → **Nachtrag B3c**.
- **B3c (19.10.2026):** Bekleidungen (IfcCovering) nicht im Paar hlks-architektur, eigenes Paar `hlks-bekleidung` (zuschaltbar);
  alte Bekleidungs-Kollisionen bleiben bei einem Lauf ohne das Paar «ausserhalb» und unverändert (Status bleibt). Aussparungen
  nur in Beton/Mauerwerk: Wortliste DE/EN/FR in `AussparungMaterial.cs` (ANNAHME), Material aus KICAD-Aufbau bzw. aus der
  Netzdatei **Fassung 3** (Material je Produkt aus IfcRelAssociatesMaterial, Exemplar sonst Typ); durchgehende Aussparung über
  Bauteile direkt hintereinander (Lücke ≤ 1 mm), `bauteil` = dickstes, weitere `bauteil2…`; unbekanntes Material → Vorschlag
  mit `materialUnbekannt`; P20 nur bei Rohbau ja/unbekannt (Kollision.material/rohbau). Abstandsprüfung aus, `abstand_weich`/
  `abstand_leitungen` optional. **Befund Bernstrasse:** Das Revit-IFC führt fast nur Darstellungsmaterialien («Farbe, weiss»,
  «<Unnamed>»); die Bauart steht im Typnamen → 86 von 112 Aussparungen «Material unbekannt»; 42 der 59 unbekannten Bauteile
  heissen «2.3_IW 1.5/8/1.5 cm 110» bzw. «_IW GIS 1.8/3.2 cm 50». 77 der 80 gequerten «Bekleidungen» sind die Kanaldämmungen
  des H+K-Modells selbst (Revit exportiert Dämmung als IfcCovering) – durch B3c nicht mehr gemeldet. **Fragen an Christian:**
  (1) Typname als Rückfall für das Material (eigene Annahme des Agenten)? (2) Was sind «IW 1.5/8/1.5» und «IW GIS» (vermutlich
  Gipsständerwände = Leichtbau, keine Aussparung)? (3) Projektliste «Typname → Material» zum Nachpflegen? Offen: P20 meldet
  Durchdringungen von IfcBuildingElementPart (70 bei Bernstrasse), obwohl Teile keine Aussparung bekommen.
- **SIA 400 / SIA 410/2 (Befund Christian 19.10.2026: «Darstellung der Aussparungen nicht nach SIA»):** nachgelesen in SIA
  400:2000 B.10 (S. 41–42, Quelle Empfehlung SIA 410/2) und SIA 410/2 (in 410_1_2d.pdf S. 11–16). Regeln: Sinnbild =
  **massstäbliches Rechteck ohne Kreuz**; zusätzliche Kennzeichnung durch **Kennfarbe** (horizontale Bauteile – Decke, Boden –
  **gelb**, vertikale – Wand, Pfeiler – **braun**, beides gelb/braun diagonal geteilt) oder **zeichnerische Ergänzung**
  (innerer dicker Strich oben+links bei Decke/Boden, unten+rechts bei Wand/Pfeiler, beides + Diagonale);
  **Kurzzeichen** WD/DD/BD/FD (Durchbrüche), WFR/DFR/BFR/FFR (Futterrohre), WS/DS/BS/FS (Schlitze), VM, AS, mit
  **Gewerk-Kurzzeichen** E, H, L (Lüftung und Klima), K, S davor («S DD»); ohne Gewerk = allgemein («DD»);
  **Bemassung** (Tab. 6): Durchbruch horizontal Breite/Länge, vertikal Breite/Höhe + Lage (Kote OK Durchbruch), Futterrohr
  Ø innen/Ø aussen/Länge, Werkstoff, Kote Achse; **horizontal ab Fixpunkten** (rohe Tragmauern, Brandmauern, Aufzug- und
  Treppenhauswände, tragende Pfeiler/Säulen) bis zur nächstliegenden Seitenkante bzw. Futterrohrachse, Massketten möglichst
  bis AK Fassadenrohbau, **nie ab Plankoordinaten**; **vertikal ab Fertigkote des Geschosses bis OK** Aussparung bzw.
  Futterrohrachse, Höhenkote angeben; **Deckenaussparungen im Grundriss des Geschosses direkt unter der Decke**,
  Bodenkonstruktion im zugehörigen Geschoss, Fundamentaussparungen im Fundamentplan. → Geschoss-Regel vom Hauptlauf sofort
  umgesetzt (Decke/Dach: Geschoss unter der Unterkante; ohne Geschoss darunter das des Bauteils); **Nachtrag B4d** (Sinnbild,
  Kurzzeichen, Beschriftung, Massketten, Kennfarben; Status nur am Bildschirm). Kernbohrung ist in SIA 400/410 nicht
  eigens geregelt → ANNAHME: runder Durchbruch, Kreis massstäblich, Ø, Kote Achse (wie Futterrohr).
- **Entscheide Christian 19.10.2026 (Material):** Material darf aus dem **Typnamen** gelesen werden (Rückfall, Herkunft
  «aus Typname»); «2.3_IW 1.5/8/1.5 cm 110» = **Gips/Leichtbau** (keine Aussparung); «_IW GIS 1.8/3.2 cm 50» =
  **Sanitärvorwand (Geberit GIS)**; **Typenliste «Typname → Material» je Projekt**. → **Nachtrag B3d**.
- **B3d (19.10.2026):** Vorrang **Typenliste > IFC-Material (wenn aussagekräftig) > Typname nach Wortliste > unbekannt**, Herkunft
  im Text («Beton: aus Typname «…»», «Leichtbau: Typenliste «…»»). Typenliste als eigene Klasse `Typmaterial` (TM-n; Typname,
  Kategorie aus 8, Quelle Pflicht; eigene Klasse statt Eigenschaft an VG-1, damit jeder Eintrag Herkunft und Konfliktlösung
  hat), Befehl `typmaterial_setzen`, Abfrage `typmaterial` (Wirkung, unbekannte Typen mit Anzahlen, «aus Typname» zum
  Bestätigen). Kategorie `installationsvorwand`: weder Aussparung noch Kollision im Vorgabelauf, eigenes Paar `hlks-vorwand`
  zuschaltbar (getrennt von `hlks-bekleidung`, weil aus anderem Grund zugeschaltet); Kategorie `bekleidung` zählt wie
  IfcCovering. **ANNAHMEN:** (a) IFC-Material ist aussagekräftig, wenn es eine Bauart nennt (Putz/Farbe/Belag allein nicht);
  (b) ein Eintrag ohne «:» gilt auch für «Familie:Typ» (Revit «DirectShape:2.3_IW …»); (c) Wortliste Vorwand «GIS»,
  «Vorwand», «Installationswand». **Bernstrasse** (Vorgaben fiktiv wie B3c): 112 → 88 Aussparungen ohne, **67 mit den zwei
  Typen** Christians; «Material unbekannt» 86 → 41; 127 Vorwand-Kollisionen fallen aus dem Vorgabelauf; P20 505 → 348.
  Grösste unbekannte Typen: «5.1_Geschossdecke UB 10/3/6/1.5 cm 205» (154 Durchgänge), «5.0_Geschossdecke Tragkonstruktion
  16/1.5 cm 175», «5.2_UB EG …», «Decke-002» (IfcRoof), Luftschichten «Luft, horizontal/vertikal» (Teile). Oberfläche → C1c.
  Klein offen für den Hauptlauf nach B4d: Beschreibung von `aussparungen_vorschlagen` (Typenliste), Hinweisliste auf 100
  Zeilen begrenzt.
- **Entscheide Christian 19.10.2026 zu B3d (Fragefenster):** die drei ANNAHMEN (a)–(c) **bestätigt**; **Unterdecken**, die
  Revit als IfcSlab exportiert («abgehängte Decke»), **wie Bekleidung** (Wortliste, Paar `hlks-bekleidung`); **Luftschichten
  ignorieren** (weder Kollision noch Aussparung noch P20); Bernstrasse **«5.1_/5.2_… UB …» = Bodenaufbau** (Unterlagsboden,
  Dämmung – keine Aussparung, keine Kollision), **«5.0_Geschossdecke Tragkonstruktion 16/1.5 cm 175» = Beton**. → **Nachtrag
  B3e** (neue Kategorien `bodenaufbau`, `luftschicht`; Wortliste Unterdecke/Luft; die zwei Kleinigkeiten oben). Die Einträge
  der Bernstrasse kommen ins Abnahmeskript (Quelle «Christian Steiner, H+K Planungs AG, 19.10.2026»).
- **B4d (19.10.2026):** Sinnbild nach SIA 400 B.10 (Rechteck ohne Kreuz, Kennfarbe Decke #ffc000 / Wand #7f3f00 aus der
  H+K-Vorlage, Winkel oben/links bzw. Strich unten/rechts), Kurzzeichen WD/DD/FD mit Gewerk (auch in Liste und IFC-Pset),
  Massketten magenta ab Rohbau-Fixpunkten (Beton/Mauerwerk/tragende Wände, Stützen; auch aus dem IFC), Vorgabe wie Vorlage
  ohne Text am Symbol (Option `beschriftungAmSymbol`). Vergleich Hauptlauf mit Vorlage 1.UG: Symbolik passt; Abweichungen
  Dezimalen, HLKS-Leitungen sichtbar, überdeckte Zahlen kurzer Felder, lange Ketten (Wände mit unbekanntem Material sind kein
  Fixpunkt), Massstab (Skript 1:100, Vorlage 1:50). **Entscheide Christian 19.10.2026:** **Aussparungsgrösse auf ganze cm
  aufrunden** (im Modell, nie kleiner), **Massketten in ganzen cm**; **keine HLKS-Leitungen im Aussparungsplan** («wie
  Vorlage»). Eigene Entscheide Hauptlauf: Legende optional, Vorgabe aus (Vorlage ohne); Plan 1:50. → **B4e**.
- **B3e (19.10.2026):** Materialgruppen `Unterdecke`, `Bodenaufbau` (beide wie Bekleidung: keine Aussparung, Kollision nur im
  Paar `hlks-bekleidung`) und `Luftschicht` (in keinem Paar; alte Kollisionen «behoben»); Typenlisten-Kategorien `bodenaufbau`,
  `luftschicht`. Wortlisten: Unterdecke und Luftschicht = Entscheid; Bodenaufbau («Unterlagsboden», «Estrich», «Trittschall»
  …), «Kühldecke» = ANNAHME; «UB» bewusst nicht (Typenliste je Projekt). Nur Unterdecken aus der Wortliste zählen wie
  Bekleidung (nicht jeder Ausbau, sonst fielen Glas und Geländer weg); «Estrich» nur bei durchquerbaren Bauteilen (CH: auch
  Dachboden). Bernstrasse mit 6 Typen: Vorgabelauf 2722 → 1133 Kollisionen, P20 348 → 134, «Material unbekannt» 41 → 20.
  **Typenliste Bernstrasse nach Christian (19.10.2026):** «2.3_IW 1.5/8/1.5» und beide «IW Vorsatzschale» = Leichtbau,
  «_IW GIS» = Installationsvorwand, «5.1/5.1.1/5.2_… UB» und «6.0_Boden Loggia warm» = Bodenaufbau, «5.0_Geschossdecke
  Tragkonstruktion», «4.0_DA Flachdach Tragkonstruktion», «Unterzug 24/28» = Beton, «1.2_AW WDVS 25/15» = Rohbau → im
  Abnahmeskript `werkzeuge/abnahme3b_bernstrasse.ps1` ($TypRegeln).
## Annahmen für Christians Prüfliste

0. **Projektvorgaben** (Spiel, Abstände weich je Gewerkpaar, Grenze Kernbohrung/Aussparung, Zusammenfassen,
   Mindestabstand Leitungen) – Werte und Quelle von Christian; bis dahin keine Vorgaben im Projekt, Tests fiktiv.
1. **BCF: die aktuellste Version** (Entscheid Christian 19.10.2026, «bcf gehen wir auf das aktuellste») – das ist nach
   Stand der Spezifikation BCF 3.0 von buildingSMART; gebaut wird gegen die offiziellen XML-Schemas (BCF-XML), nie aus dem
   Gedächtnis. Teil B6 (BCF schreiben und lesen) nach B2.
2. Kollisionen als Elemente im Modell (Status über Rechner hinweg), «behoben» statt löschen.
3. Leitungen als Kapsel bzw. Quader um die Achse (exakt für gerade Rohre und Kanäle), alles andere als Netz.
4. Aussparungsnummer je Geschoss «A-<Geschoss>-<laufend>», neu vergeben nur auf Befehl.
5. Durchdringung mit zugeordneter Aussparung gilt nicht als Kollision.
6. **Mass einer Kollision:** hart = Eindringtiefe als kürzester Weg zum Herausschieben (Rohr mitten in der Wand: Radius +
   halbe Wanddicke); weich = **Unterschreitung der Vorgabe** (Vorgabe − Abstand der Dämmhüllen), nicht der Abstand selbst
   (Abweichung vom Datenmodell, B2; Referenzfälle B5 F02, F03).
7. ~~Aussparungsmasse werden auf 0.1 mm aufgerundet~~ → **Entscheid Christian 19.10.2026: auf ganze cm aufgerundet** (B4e;
   Tiefe = Bauteildicke ungerundet; freigegebene Aussparungen behalten ihre Masse); schräge Querung → Ø = D/cos θ + t · tan θ
   + 2 × Spiel, dann aufgerundet (Referenzfall B5 F09: 219.0 × 95 → 220 × 100).
