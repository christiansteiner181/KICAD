# Änderungs- und Bearbeitungswerkzeuge – KICAD gegenüber Revit

Stand 03.11.2026, geprüfter Code `main` 66643d8 · **Entwurf zur Entscheidung, nichts gebaut.**

Christian, 03.11.2026: «zum zeichen braucht es änderungs und bearbeitungswerkzeuge vergleiche mit Revit».

Lesart der Spalte «Lücke»: **da** = gleichwertig vorhanden · **teilweise** = Kern oder Oberfläche fehlt bzw. Revit kann mehr ·
**fehlt** = weder Kernbefehl noch Oberfläche. Die Revit-Kürzel sind die Werkseinstellung (deutsche Oberfläche, Revit 2025).

## Kurzbefund

- **Die Grundlage steht:** Verschieben, Kopieren, Drehen, Spiegeln, Löschen, Rückgängig/Wiederholen mit denselben Kürzeln wie Revit;
  Mehrfachauswahl (Strg+Klick, Fenster/Kreuzen); Fangen mit Endpunkt, Schnittpunkt, Mitte, Lot, Nächster, Raster, Anschluss;
  Griffe an Wänden, Achsen, Detaillinien und Leitungsenden. Jeder Schritt ist ein Kernbefehl mit Trockenlauf, ein Paket, über MCP
  genauso nutzbar – das kann Revit so nicht.
- **Leitungsfachlich ist KICAD beim Zeichnen stark** (Serie, Bögen, Reduktionen, T-Stück, Gefälle, Steigleitung, Grösse/Serie
  für den ganzen Leitungszug), **beim nachträglichen Ändern schwach.** Das grösste Loch: Verschiebt man eine Gerade, folgen
  Formteile und Nachbarleitungen nicht (`src/Hlks.Kern/Leitungen/LeitungenNachfuehren.cs`, Annahme «Leitungen selbst ziehen
  nichts nach»). In Revit dehnen sich die Nachbarn mit, die Formteile wandern mit – das ist das tägliche Werkzeug.
- **Es fehlen die klassischen Geometriewerkzeuge** Stutzen/Dehnen, Teilen, Ausrichten, Versatz, Reihe, Fixieren – und für
  Leitungen deren fachliche Wirkung (Ecke → Bogen, auf Leitung dehnen → T-Stück, Teilen → Armatur einsetzen).
- **Verbinden/Trennen gibt es im Kern, in der Oberfläche nur im Schema** – im Grundriss nicht, und ein gezogenes Leitungsende
  verbindet sich nicht mit dem Anschluss, auf dem es losgelassen wird.

## 1 Allgemeine Ändern-Werkzeuge

| Werkzeug | Revit (Bedienung, Kürzel) | KICAD heute (Kern · Oberfläche · Fundstelle) | Lücke |
| --- | --- | --- | --- |
| Verschieben | Ändern › Verschieben **MV**; Basispunkt → Ziel, Zahl = Abstand; Optionen «Trennen», «Mehrfach», «Orthogonal»; auch direkt ziehen und Pfeiltasten | `elemente_verschieben` (dx, dy, dz) `src/Hlks.Kern/Architektur/Bearbeiten.cs:62` · Werkzeug MV, Basis → Ziel, Zahl = Abstand `src/Hlks.Web/wwwroot/js/werkzeuge/aendern.js:57` | **teilweise:** kein Ziehen des ganzen Elements mit der Maus, keine Pfeiltasten, dz nur über MCP, keine Option «Trennen» (Verbindungen lösen beim Verschieben) |
| Kopieren | **CO**/CC; Option «Mehrfach» (beliebig viele Ziele nacheinander); Strg+C / Strg+V, «Ausgerichtet einfügen» (auf andere Geschosse) | `elemente_kopieren` (dx, dy, anzahl) `Bearbeiten.cs:85` · Werkzeug CO mit Feld «Anzahl» `aendern.js:62` | **teilweise:** kein «Mehrfach» mit freien Zielen, keine Zwischenablage, kein Kopieren auf andere Geschosse, kein dz |
| Drehen | **RO**; Drehpunkt ziehbar, Winkel per Klicks oder Zahl, Option «Kopieren»; Leertaste beim Platzieren | `elemente_drehen` `Bearbeiten.cs:152` · RO mit Drehpunkt, zwei Strahlen oder Zahl `aendern.js:68` | **teilweise:** keine Option «Kopieren», Leertaste dreht beim Platzieren nicht |
| Spiegeln | **MM** (Achse wählen), **DM** (Achse zeichnen); Option «Kopieren» | `elemente_spiegeln` `Bearbeiten.cs:186` · MM, Achse durch zwei Punkte `aendern.js:98` | **teilweise:** nur «Achse zeichnen»; «Achse wählen» (Wand, Achse, Linie anklicken) und «Kopieren» fehlen |
| Reihe | **AR**; linear oder radial, Anzahl, «Zweites» oder «Letztes», gruppiert und nachträglich änderbar | über `elemente_kopieren` mit anzahl (nur linear, fester Abstand) | **teilweise:** kein radial, kein «Letztes» (Abstand verteilen), nicht nachträglich änderbar |
| Skalieren | **RE**; nur Linien, Wände, Bilder, Importe | – (Unterlagen werden über «Kalibrieren» massstäblich gesetzt) | fehlt – für HLKS ohne Nutzen, Unterlagen sind abgedeckt |
| Ausrichten | **AL**; Bezug wählen, dann Kante/Achse des Elements; Schloss sperrt die Ausrichtung | – | **fehlt:** zum Ausrichten von Leitungen auf Wand, Achse oder Nachbarleitung das meistgebrauchte Werkzeug nach MV |
| Versatz | **OF**; grafisch oder numerisch, Option «Kopieren» | – (nur `bezugslinie` beim Wandzeichnen, Abstand im Werkzeug) | **fehlt:** Leitung bzw. Wand um Abstand parallel versetzen oder parallel kopieren |
| Stutzen/Dehnen für Ecke | **TR**; zwei Elemente anklicken → Ecke; bei Rohren entsteht ein Bogen | – | **fehlt** (Kern und Oberfläche) |
| Einzelnes / mehrere Elemente stutzen/dehnen | Bezug wählen, dann Element(e); bei Rohren auf ein Rohr → T-Stück | – (Abzweig nur beim Zeichnen: Klick auf Gerade `leitung_abzweigen`) | **fehlt:** bestehende Leitung bis an eine andere dehnen und dort anschliessen |
| Element teilen | **SL**; Klick auf Element teilt; bei Rohren mit Muffe/Verbindung | `leitung_aufteilen` teilt nur Altbestand-Polylinien an Knicken `src/Hlks.Kern/Leitungen/RoutingBefehle.cs:709` | **fehlt** als Teilen an einem Punkt (Wand, Leitung) |
| Lücke teilen | Teilen mit Spaltbreite (Wände, Rohre) | – | **fehlt** – für HLKS nachrangig |
| Fixieren / Lösen | **PN** / **UP**; Pinnadel am Element, fixierte Elemente nicht verschieb- oder löschbar | – (kein Elementzustand «fixiert») | **fehlt:** wichtig für eingelesene IFC-Bauteile, Achsen, Steigzonen |
| Löschen | **DE**, Entf | `element_loeschen` mit Trockenlauf `src/Hlks.Kern/Api/Befehle.cs:479` · DE/Entf, Rückfrage mit Vorschau `src/Hlks.Web/wwwroot/js/oberflaeche/aktionen-bearbeiten.js:63` | **teilweise:** nur ein Element je Befehl (Mehrfachauswahl löscht nicht in einem Paket); verweisende Elemente lehnen ab (Revit löscht mit) |
| Eigenschaften anpassen | **MA** (Typeneigenschaften übertragen: Quelle, dann Ziele) | «Ähnliches platzieren» übernimmt Angaben für ein *neues* Element `oberflaeche/aktionen-aehnlich.js:42`; Eigenschaftenpalette PP | **teilweise:** kein Übertragen auf *bestehende* Elemente; Palette bearbeitet nur ein Element, nicht die Mehrfachauswahl |
| Gruppen | **GP** Gruppe erstellen, bearbeiten, alle Exemplare gleich | – | **fehlt** – für HLKS selten (Nasszellen-Installation, Verteiler); nachrangig |
| Rückgängig / Wiederholen | Strg+Z / Strg+Y, Liste der Schritte | Pakete mit Undo/Redo, Strg+Z / Strg+Y `aktionen-bearbeiten.js:82` | da (Liste der Schritte fehlt, nachrangig) |
| Letzten Befehl wiederholen | Enter bzw. Rechtsklick «Wiederholen» | – | **fehlt** – billig, viel gebraucht |
| Temporäre Masse | Am gewählten Element erscheinen Masse zu Nachbarn; Klick auf die Zahl → neuer Wert verschiebt | Masslinie nur während Zeichnen/Ziehen (`S.tempMass`, `werkzeuge/griffe.js:117`); Zahl = Länge beim Zeichnen | **teilweise:** keine Masse am *gewählten* Element, nicht anklickbar |
| Bearbeiten in 3D | alle Ändern-Werkzeuge auch in 3D- und Schnittansichten | nur Grundriss; in 3D nur Eigenschaften der Ansicht, in XR Greifen = Verschieben `ansichten/xr-greifen.js:393` | **fehlt** in 3D und Schnitt (Höhenlagen von Leitungen sieht man nur dort richtig) |

## 2 Gebäudetechnik

| Werkzeug | Revit (Bedienung, Kürzel) | KICAD heute (Kern · Oberfläche · Fundstelle) | Lücke |
| --- | --- | --- | --- |
| Rohr / Kanal ziehen | **PI** / **DT**; Typ, System, Durchmesser, Mittelhöhe, Gefälle in der Optionsleiste; Bögen automatisch | `leitung_zeichnen` `RoutingBefehle.cs:354` · PI/DT mit Serie, Grösse, Höhe (Taste H), Gefälle, Winkelfang, Anschlussfang, Trockenlauf `werkzeuge/leitung.js` | da (stärker: Trockenlauf, Einbaulängen der Serie) |
| Abzweig | beim Ziehen auf ein Rohr → T-Stück | Klick auf Gerade → `leitung_abzweigen` `RoutingBefehle.cs:396` | da beim Zeichnen; nachträglich siehe «Stutzen/Dehnen» |
| Gefälle ändern | Ändern › Rohre › **Gefälle**; Wert, Richtung, ganzer Zug wird neu geneigt; Gefällepfeil mit Griff | Gefälle beim Zeichnen; nachträglich nur Eigenschaft `gefaelle` in der Palette (`werkzeuge/leitung-palette.js:95`), **die Geometrie bleibt** – P16/P17 melden die Abweichung `src/Hlks.Kern/Leitungen/Leitungspruefungen.cs:206` | **teilweise:** kein Befehl, der einen Leitungszug ab einem Punkt neu neigt (Abwasser!) |
| Grösse / Typ ändern | Optionsleiste bzw. Eigenschaften; Formteile werden ersetzt | `leitung_groesse`, `leitung_serie` für Gerade oder ganzen Zug `RoutingBefehle.cs:775`, `:792` · Palette mit Rückfrage «nur diese / ganzer Zug» `werkzeuge/leitung-bearbeiten.js` | da |
| Höhe ändern | Mittelhöhe in Optionsleiste/Eigenschaften, Verbindungen passen sich an | über Griffe nur in x/y; Höhe nur über MCP (`element_aendern` geometrie bzw. `elemente_verschieben` dz) | **teilweise:** kein Feld «Höhe» für gewählte Leitung bzw. Zug in der Oberfläche |
| Formteil einfügen / tauschen | **PF** / **DF**; am Formteil Plus/Minus-Steuerelement: Bogen ↔ T-Stück ↔ Kreuz, Kappe; Typ in Eigenschaften | Formteile entstehen nur im Kern (zeichnen, abzweigen, Grösse, Serie); `familie_platzieren` setzt frei, ohne einzuschneiden | **fehlt:** Bogen zu T-Stück erweitern, T-Stück zurückbauen, Formteiltyp tauschen |
| Armatur / Zubehör in Leitung setzen | **PA** / **DA**; auf ein Rohr klicken → Rohr wird geteilt, Armatur sitzt in der Leitung, Grösse übernommen | `familie_platzieren` legt die Armatur frei ab `src/Hlks.Kern/Familien/FamilienBefehle.cs:416`, verbinden danach von Hand | **fehlt** – täglicher Fall (Absperrventil, Strangregulierventil, Brandschutzklappe) |
| Verbindung herstellen | «Verbinden mit» (Connect Into); Ende auf Anschluss ziehen verbindet | `verbinden` mit Prüfung von Lage, Richtung, Grösse `Befehle.cs:514` · Oberfläche nur im Schema `ansichten/schema.js:40` | **teilweise:** im Grundriss kein Werkzeug; Griff-Ende auf Anschluss loslassen verbindet nicht `werkzeuge/griffe.js:185` |
| Verbindung trennen | Formteil löschen bzw. Ende wegziehen; Option «Trennen» bei MV | `trennen` `Befehle.cs:614` · nur im Schema | **teilweise:** im Grundriss fehlt es |
| Leitung anschliessen | Gerät wählen › «Verbinden mit» → Leitung wird gesucht und angeschlossen | `leitung_zeichnen` mit `von`/`nach`; Anschlussfang im Werkzeug | **teilweise:** kein «Gerät an bestehende Leitung anschliessen» mit Wegsuche |
| Rohr verschieben mit nachgeführten Anschlüssen | Gerade verschieben: verbundene Geraden dehnen/stauchen, Formteile wandern mit; Gerät verschieben: Leitungen folgen | Gerät/Formteil verschieben → nur die *direkt* angeschlossenen Leitungsenden folgen `LeitungenNachfuehren.cs`; **Gerade verschieben → nichts folgt** | **teilweise, wichtigste Lücke:** Netz reisst beim Verschieben einer Geraden auf |
| Parallele Leitungen | Systeme › **Parallele Rohre** / Kanäle: Anzahl, Abstand, waagrecht/senkrecht, entlang eines Zugs | – (Vorlauf/Rücklauf je einzeln zeichnen; `system_paar_anlegen` ordnet nur das Systempaar) | **fehlt:** VL/RL, Trinkwasser kalt/warm/Zirkulation sind fast immer parallel |
| Systeme prüfen | Systembrowser (F9), «Fehlende Verbindungen anzeigen», Systeminspektor, Warnungen | Prüfungen P13–P18 bei jedem Befehl, Problemmarker, Netz stromab/stromauf folgen `oberflaeche/aktionen-bearbeiten.js:150`, Kollisionsprüfung | da (Darstellung offener Enden im Grundriss als Marker wäre ein Plus) |
| Offene Enden abschliessen | «Offene Enden abschliessen» (Kappen) | – | fehlt – nachrangig |
| Justieren | Leitungszug an Ober-/Unterkante oder Mitte ausrichten (Kanäle mit Reduktionen) | – | fehlt – nachrangig, bei Kanälen mit Reduktionen später nützlich |

## 3 Ausführung

| Werkzeug | Revit (Bedienung, Kürzel) | KICAD heute (Kern · Oberfläche · Fundstelle) | Lücke |
| --- | --- | --- | --- |
| Tastenkürzel | zwei Buchstaben, frei belegbar (Tastaturbefehle) | Revit-Kürzel für MV, CO, RO, MM, DE, PP, MD, PI, DT, WA, DR, WN, RM, GR, DI, EL, TX, DL; Liste KS `oberflaeche/kuerzel-liste.js` | **teilweise:** nicht frei belegbar; Kürzel der neuen Werkzeuge (AL, OF, TR, SL, AR, PN/UP, MA) kommen mit ihnen |
| Griffe | Endpunkte, Formgriffe, Drehsteuerung, Richtungspfeile, Plus/Minus an Formteilen, Gefällepfeil | Endpunkte an Wand, Achse, Detaillinie, Rohr, Kanal; Tür: Aufschlag/Band umdrehen `werkzeuge/griffe.js:60` | **teilweise:** keine Griffe an Geräten, Armaturen, Formteilen; kein Drehgriff, kein Gefällepfeil, Wandecken ziehen verbundene Wände nicht mit |
| Mehrfachauswahl | Strg+Klick ergänzt, Shift+Klick nimmt heraus, Fenster/Kreuzen, **Tab** wechselt bzw. wählt die ganze Kette, «Alle Exemplare auswählen» (SA), Auswahl speichern | Strg+Klick ergänzt/nimmt heraus `werkzeuge/auswahl.js`, Fenster/Kreuzen `ansichten/fensterauswahl.js`, Netz stromab/stromauf | **teilweise:** kein Tab (Leitungszug in einem Zug wählen), kein «Alle Exemplare», keine gespeicherte Auswahl |
| Auswahlfilter | Knopf «Filter» bei Mehrfachauswahl: Kategorien an/aus mit Anzahl | – (nur Sichtbarkeitsfilter der Ansicht `ansichten/filter.js`) | **fehlt** – nötig, sobald Fenster/Kreuzen Wände, Räume und Leitungen mischt |
| Fangpunkte | Endpunkt, Mittelpunkt, Schnittpunkt, Lot, Nächster, Zentrum, Quadrant, Tangente, Arbeitsebene; einmaliges Überschreiben **SE, SM, SI, SP, SN, SC, SO**; Fangeinstellungen (Dialog) | Endpunkt > Schnittpunkt > Mitte > Lot > Nächster > Raster, Anschluss vor allem; Shift = orthogonal `werkzeuge/fangen.js:4`; nur Raster einstellbar `werkzeuge/optionen.js:20` | **teilweise:** keine Fang-Überschreibung per Kürzel, keine Fangarten an/aus, kein Fang auf IFC-/PDF-Unterlagen-Kanten geprüft (nicht im Umfang dieses Vergleichs) |
| Richtung / Fliessrichtung umkehren | Pfeile am Element, Leertaste | «Fliessrichtung ab hier» `oberflaeche/aktionen-leitung.js:84` | da |

## 4 Vorschlag: Reihenfolge des Baus

Massstab: Was ein HLKS-Zeichner in Revit stündlich braucht und was heute das Netz zerreisst, kommt zuerst. Jede Stufe ist für
sich nutzbar. Rechnung im Kern (Befehl mit Trockenlauf, MCP gleich), die Oberfläche bleibt dünn – wie bisher.

### Stufe A – Leitungsnetz bleibt beim Ändern ganz (zuerst)

1. **A1 Verschieben mit Nachführung im Netz.** Gerade verschieben (MV, Griff in der Mitte ziehen, Pfeiltasten): Formteile an
   ihren Enden wandern mit, die anschliessenden Geraden dehnen bzw. stauchen sich; bei Richtungswechsel des Nachbarn wird der Bogen
   ersetzt oder es entsteht ein Versatz mit zwei Bögen (Serie, Winkel nach `regeln.winkel`), sonst Ablehnung mit Vorschlag. Dazu
   Option «Trennen» bei MV. Kern: Erweiterung `LeitungenNachfuehren` bzw. eigener Befehl; Prüfung durch `hlks-pruefer` (Geometrie).
2. **A2 Verbinden und Trennen im Grundriss.** Werkzeug «Verbinden» (zwei Anschlüsse bzw. Ende → Anschluss), «Trennen» im
   Kontextregister; Griff-Ende auf einem freien Anschluss loslassen verbindet (gleiche Prüfung wie `verbinden`). Kernbefehle
   gibt es schon – reine Oberfläche.
3. **A3 Armatur in Leitung setzen.** Familie (Armatur, Zubehör, Klappe) auf eine Gerade platzieren: Gerade teilen, um die
   Einbaulänge kürzen, Grösse aus der Leitung, beidseitig verbinden, Reduktionen wenn die Familie nur andere Grössen hat.
   Im selben Zug **Teilen (SL)** an einem Punkt für Wand und Leitung (Leitung mit Verbindungsstück der Serie, wenn sie eines hat).
4. **A4 Stutzen/Dehnen (TR) für Leitungen.** Ecke aus zwei Geraden → Bogen der Serie; Gerade bis an eine andere dehnen → T-Stück
   (wie `leitung_abzweigen`); für Wände und Linien die geometrische Ecke. Einzeln und mehrere.
5. **A5 Gefälle und Höhe nachträglich ändern.** Befehl «Gefälle setzen» für einen Leitungszug ab einem gewählten Fixpunkt (Wert in %,
   Richtung aus der Fliessrichtung), Senkrechte behalten ihre Länge, Formteile wandern mit (baut auf A1); Feld «Höhe» (Achse,
   Unterkante) für gewählte Leitung bzw. Zug in der Palette.

### Stufe B – zügig zeichnen wie in Revit

6. **B1 Ausrichten (AL) und Versatz (OF)** mit Option «Kopieren»; Leitungen nachgeführt über A1.
7. **B2 Parallele Leitungen:** aus einem gewählten Zug (bzw. beim Zeichnen) n Leitungen im Abstand, waagrecht oder übereinander,
   je mit eigenem System (VL/RL über das Systempaar, Trinkwasser kalt/warm/Zirkulation).
8. **B3 Formteil tauschen:** Plus/Minus am Bogen bzw. T-Stück (Bogen → T-Stück → Kreuz, zurück), Formteiltyp der Serie wählen.
9. **B4 Auswahl:** Tab wählt den Leitungszug bzw. das Netz bis zum nächsten Gerät, Shift+Klick nimmt heraus, Auswahlfilter nach
   Klasse mit Anzahl, «Alle Exemplare wählen» (Ansicht/Projekt); Löschen und Eigenschaftenpalette für die ganze Mehrfachauswahl
   in einem Paket.
10. **B5 Temporäre Masse am gewählten Element**, Zahl anklicken und neu eingeben → Verschieben; dazu Fang-Überschreibung per
    Kürzel (SE, SM, SI, SP, SN, SO) und Fangarten an/aus.
11. **B6 Kleinigkeiten mit grosser Wirkung:** Enter = letzten Befehl wiederholen, Kopieren «Mehrfach», Drehen/Spiegeln «Kopieren»,
    Spiegeln «Achse wählen» (DM/MM getrennt), Leertaste dreht beim Platzieren, Strg+C/Strg+V inkl. auf andere Geschosse.

### Stufe C – später bzw. nur auf Wunsch

12. Fixieren/Lösen (PN/UP) – zuerst für IFC-Bauteile, Achsen, Unterlagen; Reihe radial und mit «Letztes»; Eigenschaften
    übertragen (MA); Griffe an Geräten und Formteilen (Drehgriff); Bearbeiten in 3D und Schnitt; Lücke teilen, Gruppen,
    Offene Enden abschliessen, Justieren, frei belegbare Kürzel. Skalieren entfällt (Unterlagen über «Kalibrieren»).

### Entscheide Christian (03.11.2026)

Christian, 03.11.2026: «ja passt» – Vergleich und Reihenfolge A → B → C wie vorgeschlagen. Dazu:

- **Reihenfolge:** A2 zuerst (reine Oberfläche), dann A1, danach A3–A5.
- **A1 bei Richtungswechsel des Nachbarn:** automatisch Versatz mit zwei Bögen der Serie einsetzen (Revit-Verhalten); passt kein
  Winkel der Serie, ablehnen mit Vorschlag.
- **Einordnung:** eigener Nachtrag «Bearbeitungswerkzeuge» (Stufen A–C, Spezifikation in diesem Entwurf); Modul 3 bleibt abgeschlossen.
- Bedienungsanleitung: je Stufe ein Kapitel nach Abschluss (Regel seit 19.10.2026).

### Stand

- **A2 gebaut 03.11.2026:** Werkzeug «Verbinden» (Ändern › Verändern, Kontextregister › Netz, Rechtsklick), «Trennen …» (Kontextregister,
  Rechtsklick), Griff-Ende auf Anschluss verbindet (Kern: `element_aendern` mit `verbinden`, ein Paket). Bedienung in
  `src/Hlks.Web/OBERFLAECHE.md` (Abschnitt «Verbinden und Trennen im Grundriss»), Anleitung `m3-verbinden`, `m3-trennen`.
  Grenze: Anschlüsse ohne Lage (Geräte und Formteile ohne Familie – im Beispielprojekt alle Formteile) verbindet weiterhin nur das Schema.
- **A1 gebaut 03.11.2026** (Spezifikation unten): Kern `LeitungenNachfuehren` (Formteile folgen, Dehnen, ganz folgen, Versatz), `elemente_verschieben` mit `trennen`; Oberfläche Mittelgriff, Pfeiltasten, MV-Option «Trennen». Bisheriges Verhalten «Nachbar dreht, Hinweis» gilt nur noch für Altbestand ohne Serie und gedrehte Formteile (Tests angepasst: RoutingNachfuehrungTests, LeitungenPruefungTests.P13_NachVerschiebenDesRohrendes).
- **Nächstes: A3** Armatur in Leitung setzen und Teilen (SL).

## A1 – Verschieben mit Netznachführung (Spezifikation, 03.11.2026)

Regeln im Kern (Nachführung `LeitungenNachfuehren`, gilt für jeden Befehl, der Leitungen bzw. Formteile bewegt – MV, Griffe, MCP):

1. **Formteile folgen der Leitung.** Hat sich ein Ende einer Geraden bewegt und ihre Richtung nicht geändert (Verschieben, Dehnen
   entlang der Achse), wandert das dort angeschlossene Formteil bzw. die Armatur (Rolle «leitung», mit Familie) um dieselbe Strecke mit –
   sofern es noch an der alten Stelle sass. Direkt aneinander hängende Formteile folgen einander ebenso. Geräte (Heizkörper, Apparate)
   folgen nicht; dort löst sich das Ende wie bisher (P13).
2. **Nachbarleitungen** am bewegten Formteil: Ende auf die neue Lage. Bleibt die Richtung (±1°), wird sie nur länger bzw. kürzer. Sonst:
   - anderes Ende frei → die ganze Leitung verschiebt sich mit (Richtung bleibt);
   - anderes Ende verbunden und Leitung mit Serie → **Versatz mit zwei Bögen der Serie** (Entscheid Christian): Winkel 45°, wenn die Serie
     ihn hat, sonst 90°, sonst der grösste erlaubte Winkel unter 90°; der Versatz liegt in der Mitte der Leitung (Annahme). Passt er nicht
     (zu kurz, Einbaulängen), wird der ganze Befehl abgelehnt mit Vorschlag;
   - Altbestand ohne Serie → wie bisher drehen, Hinweis, P13/P15.
3. Dreht sich eine Gerade (Drehen, Griff quer), folgen die Formteile nicht (Annahme; P13 meldet es).
4. **«Trennen» bei MV:** `elemente_verschieben` mit `trennen: true` löst zuerst die Verbindungen zu nicht mitverschobenen Elementen;
   dann folgt nichts nach.

Oberfläche: Optionsleiste MV «Trennen»; Griff in der Mitte einer Geraden verschiebt sie (Fangen, Umschalt = orthogonal); Pfeiltasten
verschieben die Auswahl um den Rasterwert in den Achsen der Ansicht (ohne laufendes Werkzeug).
