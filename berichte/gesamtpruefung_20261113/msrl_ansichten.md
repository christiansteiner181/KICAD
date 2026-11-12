# Gesamtprüfung KICAD – Bereich MSRL und Ansichtsverwaltung (Runde 5)

Prüfagent: Claude Fable 5.2 · Zweig `worktree-agent-ad11a7ffb8eacf3b9` · Stand vor der Prüfung `bbac868`.
Geprüft: `Msrl/Datenpunktliste.cs`, `Datenquellen/` (IDatenquelle, Simulator), Abfragen `datenpunkte`/`zeitreihe`/`datenpunktliste`, KI-Einordnung der
GA-Werkzeuge, Register MSRL; `Grafik.Sichtbar` und alle seine Aufrufer, `filter.js`, `SchemaGraph` (Filter), `Ansichtsausrichtung.cs`,
`SchnitthoeheSia.cs`, `Planausgabe.Gebiet` (Ausschnitt), `aktionen-ansicht.js`, `fenster.js` (Ansichtsleiste), `ansichtsreiter.js`,
`projektbrowser*.js` (Ansichts- und Vorlagenäste, Bedienung), `ElementLoeschen`, `ansicht_raum_zeigen`.

## 1. Kurzfazit

Der wichtigste Befund: **«sichtbar» hiess an drei Stellen drei verschiedene Dinge.** Der Gewerkefilter einer Ansicht wirkte im Grundriss, im Schnitt
und im PDF nur auf Klassen mit eigenem Gewerk – Rohre, Formteile, Armaturen, Pumpen und Fühler haben im Katalog keines und erschienen deshalb in
**jedem** Gewerkeplan (Kaltwasserrohr im Lüftungsplan). 3D filterte nach einem einzigen abgeleiteten Gewerk, das Schema nach der richtigen Regel
(alle Gewerke des Elements). Jetzt gilt überall die Regel des Schemas; derselbe Fehler steckte im Kriterium «Gewerk» der Montageabschnitte.
In der Ansichtsverwaltung liess sich ein duplizierter Schnitt nicht öffnen (Schnittlinie fehlte), und «Raum im 3D zeigen» konnte eine Ansichtsvorlage
als Ziel nehmen. MSRL ist ein sauberes Gerüst: die GA-Schnittstelle ist nur lesend (einzige Quelle ist der Simulator, kein Netzzugriff, kein
Schreibweg) – korrigiert sind dort die Anlagenzuordnung ohne eigenes System und eine still gekürzte Zeitreihe. Offen bleiben vor allem Entscheide
(Ansicht löschen mit Beschriftungen, Ansichtsvorlage als Kopie oder Verknüpfung, Bedienung MSRL).

## 2. Korrigiert

| # | Datei | Fehler | Korrektur |
|---|---|---|---|
| 1 | `Grafik/Grafik.cs` (`Sichtbar`), `Api/Ableitungen.cs` (neu `Gewerke`, `GewerkGewaehlt`) | Gewerkefilter prüfte nur `KlassenDef.Gewerk`. 18 Klassen haben keines (Leitung, Rohr, Formteil, Bogen, TStueck, Reduktion, Hosenstueck, Endkappe, Muffe, Verschraubung, Kreuz, Uebergang, Reinigungsoeffnung, Armatur, Ventil, Pumpe, Mischventil, Fuehler) → in jedem Gewerkeplan sichtbar. Handbeispiel: Ansicht `gewerke: [lueftung, architektur]`, Rohr KW-1 im System «Trinkwasser kalt» → vorher im Plan, jetzt nicht. | Ein Element hat die Gewerke seiner Klasse und seines Systems (Systemtyp, sonst Medium); ohne eigenes System die der direkt verbundenen Elemente; ohne jedes System das eigene Medium («leitungen gehören einem medium», 02.11.2026). Sichtbar, wenn keines bekannt, eines «allgemein» oder eines gewählt ist – dieselbe Regel wie `SchemaGraph.GewerkeVon`. Gilt damit für Grundriss, Schnitt, Ansicht, PDF, Dämmung, Verdeckung. |
| 2 | `Api/Darstellung.cs`, `js/ansichten/filter.js` (neu `gewerkGewaehlt`) | 3D filterte nach **einem** Gewerk (`Ableitungen.Gewerk`: erstes System nach Kennzeichen vor der Klasse). Ein Gerät zwischen zwei Gewerken verschwand je nach Kennzeichen der Systeme aus dem eigenen Plan (Ausdehnungsgefäss der Klasse Heizung am Kaltwasserrohr: Gewerk «sanitaer», im Heizungs-3D weg). | Der Kern liefert je Element `gewerke` (Liste); 3D filtert damit wie der Kern. Ältere Daten ohne `gewerke` fallen auf `gewerk` zurück. |
| 3 | `Api/Ableitungen.cs` (`Gewerk`) | Leitung ohne System, nur mit Medium: Gewerk `null` (keine Koordinationsfarbe, im Kollisionspaar «allgemein», Aussparungskürzel unbekannt). | Rückfall auf das Gewerk des Mediums, nur wo Systemtyp und Klasse nichts hergeben. |
| 4 | `Baustelle/BaustelleDaten.cs` (`Abschnitt`) | Montageabschnitt mit Kriterium `gewerke: [sanitaer]` enthielt Apparate, aber **keine einzige Leitung** (gleiche Ursache wie 1). | Kriterium über `Ableitungen.Gewerke`. |
| 5 | `js/oberflaeche/aktionen-ansicht.js`, neu `ansicht-logik.js` (`duplikatParameter`) | «Ansicht duplizieren» kopierte nur die Werte. Schnitt und Ansicht haben ihre Schnittlinie in der Geometrie → die Kopie liess sich nicht öffnen («schnitt … hat keine Schnittlinie»). | Geometrie geht mit (nicht bei Vorlagen). |
| 6 | `aktionen-ansicht.js` (Dialog «… anlegen» mit Vorlage), `ansicht-logik.js` (`vorlageEigenschaften`) | Vorlage ohne Gewerkefilter liess die Haken der Vorgabe (Sanitär + Architektur) stehen – die neue Ansicht bekam einen Filter, den die Vorlage nicht hat. `ifcDarstellung` ging beim Anlegen nicht mit, bei «Vorlage anwenden» schon. | Haken folgen der Vorlage; `ifcDarstellung` wird übernommen. |
| 7 | `Plan/Schnittbereich.cs` (`ansicht_raum_zeigen`), `aktionen-ansicht.js` (`ansicht3dFuerRaum`: Raum im 3D zeigen, Raum begehen) | Als Ziel galt jede 3D-Ansicht, auch eine Ansichtsvorlage (Koordination, Problemmarker und VR schliessen Vorlagen aus) – der Schnittbereich landete in der Vorlage und sie wurde geöffnet. | Vorlagen ausgeschlossen (Kern und Oberfläche). |
| 8 | `Msrl/Datenpunktliste.cs` (`AnlageVon`, neu `AnlageParameter`) | Anlage nur über das **eigene** System. Das System ist freiwillig: eine Pumpe ohne System am Rohr stand unter «ohne Anlage», `datenpunkte_vorschlagen { anlage }` übersprang sie. Der Parameter `anlage` nahm jedes Element (ein System ergab still 0 Datenpunkte). | Ohne eigenes System über das System der angeschlossenen Elemente; `anlage` muss eine Anlage sein (Meldung mit Vorschlag). |
| 9 | `Api/Abfragen.cs` (`Zeitreihe`) | Mehr als 2000 Werte wurden still abgeschnitten (`anzahl` 4320, `werte` 2000) – wer `werte` liest, hält den Zeitraum für vollständig. `aufloesungMinuten` NaN gab eine .NET-Ausnahme. | `hinweis` mit Anzahl und letzter gelieferter Zeit; Meldung bei NaN. |
| 10 | `js/fenster.js` (Ansichtsleiste) | Ansicht ohne `massstab`: Leiste zeigte 1:100, der Kern zeichnet 1:50 (`Grafik.Kontext`). | Vorgabe 50. Ohne eigenen Test (reine Anzeige). |

**Tests (gefiltert):**
- Kern, neu `AnsichtSichtbarkeitTests` (3), `DatenpunktlisteTests` (+1), `AbfragenTests.Zeitreihe_Durchfluss_UndStandardraster` (erweitert). Der neue
  Gewerketest war vor der Korrektur rot (belegt den Fehler).
- Lauf 1 (Sichtbarkeit und Nachbarn): `AnsichtSichtbarkeitTests|DatenpunktlisteTests|AbfragenTests.Zeitreihe|SchnittbereichTests|HlksGrafikTests|PlanDarstellungB1Tests|EinlageplanTests|KanalRahmenTests|LeitungsbezeichnungTests|KaelteHeizungTests|AussparungGrafikTests|BaustelleExportTests|AbleitungenTests|HuelleGrafikTests|LeitungMediumTests|AussparungKennzeichnung|Koordination`
  → **272 bestanden, 0 Fehler**.
- Lauf 2 nach allen Korrekturen (Gewerk in Kollision, BCF, IFC-Export, Baustelle): `AnsichtSichtbarkeitTests|DatenpunktlisteTests|AbfragenTests|SchnittbereichTests|Kollision|Bcf|IfcExportTests|BaustelleExportTests|RaumBegehenTests` → **363 bestanden, 0 Fehler**.
- Node: `ansicht-logik.test.mjs` (neu, 3 Tests: Duplikat, Vorlage, Gewerkefilter 3D), `nachbearbeitung-n3.test.mjs`, `plandarstellung-b1.test.mjs`, `ifc-darstellung.test.mjs` → **38 bestanden, 0 Fehler**.

**Commits:** `13fe7b1` Ansichten (1–7), `64ca7f3` MSRL (8–9), dazu Leiste und Bericht (10).

### Nachgerechnet und in Ordnung
- **Ausrichtung der Ansicht** (`Ansichtsausrichtung.Fuer`): `nordwinkel` n = Winkel von +y zu geografisch Nord im Uhrzeigersinn, Nord im Modell also
  (sin n, cos n). Um n im Gegenuhrzeigersinn gedreht: x′ = sin n·cos n − cos n·sin n = 0, y′ = sin² n + cos² n = 1 → Nord oben, Drehung = n stimmt.
  Ausrichtung «geometrie» mit Planwinkel pw: Nord liegt n − pw im Uhrzeigersinn ab «oben» – stimmt mit `Nord = Norm(n − pw)`.
- **Ausschnitt im PDF** (`Planausgabe.Gebiet`): Mitte des Ausschnitts (Ansichtsachsen) wird mit −Drehung ins Modell zurückgedreht – richtig herum.
- **Schnitthöhe nach SIA** (`SchnitthoeheSia.Rechnen`, nur die Logik, keine Normwerte): Tür 0…2100, Fenster 900…2100 → Abschnitte [0, 900) n = 1,
  [900, 2100) n = 2, [2100, ∞) n = 0 → Bereich 900…2100, Wert 1000. Stimmt.
- **GA-Schnittstelle:** `IDatenquelle` hat nur `Zeitreihe` (lesen); einzige Implementierung `Simulator` (deterministisch, kein Netz, keine Datei).
  `datenpunkte`, `zeitreihe`, `datenpunktliste` sind als «Gebäudeautomation lesen» eingeordnet, `datenpunkte_vorschlagen` als Modellarbeit ohne
  Verbindung zur Anlage. Kein Weg nach aussen gefunden. Die Antwort nennt `quelle: Simulator`.
- **Simulator:** Zählerstand monoton (Tagessumme + linear in der Minute), Fensteranteil je Block richtig überlappt, Obergrenze 100 000 Punkte mit Vorschlag.

## 3. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 3.1 | `Api/Befehle.cs` (`ElementLoeschen`, Abhängigkeitsprüfung) | Eine Ansicht mit Beschriftungen (Bemassung, Textnotiz, Kote, Symbol – `bezuege.ansicht`) lässt sich nicht löschen: «wird noch von n Element(en) verwendet … zuerst diese Elemente löschen». | Bei einem bemassten Grundriss muss jede Masskette einzeln weg, bevor die Ansicht gelöscht werden kann. | Beschriftungen, die nur an dieser Ansicht hängen, mitlöschen (wie Türen mit der Wand), mit Hinweis «n Beschriftungen mitgelöscht»; Ansichtsfenster auf Planblättern weiter als Hindernis melden. Entscheid nötig (Frage 2). | klein | hoch |
| 3.2 | `js/oberflaeche/aktionen-ansicht.js:42` (`VORLAGE_EINSTELLUNGEN`) | Eine Ansichtsvorlage ist eine Kopie im Moment des Anwendens: spätere Änderungen der Vorlage erreichen die Ansichten nicht; `ansichtsvorlage` ist nur ein gemerktes Kennzeichen (Text, bricht beim Umbenennen oder Löschen der Vorlage still). Übertragen werden 7 Einstellungen; **nicht** `darstellung`, `ausrichtung`, `bemassungEinheit`, `rohrbezeichnung`, `aussparungsboxen`, `legende`, `systeme`, obwohl die Vorlage sie speichert. Massstab und Schnitthöhe gehen auch auf 3D-Ansichten. | Wer die Vorlage «Koordination 1:50» ändert, muss sie auf jede Ansicht neu anwenden; ein Teil der Darstellung kommt gar nicht mit. | Entweder Liste vervollständigen und nach Art filtern (klein) oder Vorlage als Verknüpfung führen (Kern: Werte der Vorlage gelten, solange zugewiesen – mittel). Frage 3. | klein bis mittel | mittel |
| 3.3 | `Plan/Planausgabe.cs:177–190`, `kataloge/klassen.json` (Ansicht.ausschnitt) | Der Ausschnitt gilt in den Achsen der **gedrehten** Ansicht. Ändert die Drehung (Planwinkel neu bestimmt, «Nach Himmelsrichtung»), zeigt derselbe Ausschnitt einen anderen Teil des Modells. Handbeispiel: Ausschnitt 0…10 000 × 0…5000 bei Drehung 0; nach Umschalten auf Himmelsrichtung mit Nordwinkel 90° liegt der Modellpunkt (5000, 2500) in der Ansicht bei (−2500, 5000) – ausserhalb. Zudem gibt es kein Werkzeug, den Ausschnitt im Grundriss zu ziehen (nur `element_aendern`). | Zugeschnittene Pläne zeigen nach einer Änderung der Ausrichtung das Falsche bzw. nichts. | Ausschnitt beim Wechsel der Drehung umrechnen (umschliessendes Rechteck) oder in Modellachsen speichern; Zuschnitt-Werkzeug im Grundriss. | mittel | mittel |
| 3.4 | `js/fenster.js:171–179`, `Grafik/FamilienGrafik.cs:31` | Die 3D-Ansicht zeigt in Leiste und Anlegedialog «1:» und «Detailgrad». Beides hat in 3D keine Wirkung (Körper immer «fein», kein Massstab). | Einstellung ohne sichtbare Folge. | In 3D ausblenden – oder den Detailgrad der Ansicht an `Grafik.Koerper` durchreichen (Familien grob/mittel im 3D). | klein / mittel | tief |
| 3.5 | `js/oberflaeche/menueband.js:45`, `kataloge/klassen.json` (Ansicht.liste) | Register MSRL hat nur «Schema» und «Familien». `datenpunkte_vorschlagen` und `datenpunktliste` gibt es nur über die KI bzw. MCP; die Listenansicht kennt keine Liste «Datenpunkte». | Das Gerüst von Modul 8 ist in der Oberfläche nicht bedienbar. | Knopf «Datenpunkte vorschlagen» (Anlage wählen) und Listenart `datenpunktliste` (je Anlage, Summen je Signal). | mittel | mittel |
| 3.6 | `Msrl/Datenpunktliste.cs:86–103`, `kataloge/datenpunkte.json` | (a) Fühler: `messgroesse` kommt aus `art` des Fühlers klein geschrieben («temperatur») und ohne Einheit – die übrigen Datenpunkte schreiben «Temperatur» und «°C». (b) Trägt ein anderes Element schon das Kennzeichen `DP-<Bauteil>-<kurz>`, zählt es als «schon vorhanden», auch wenn es kein Datenpunkt ist. (c) Die Regeln gelten je Klasse genau (`DerKlasse`), nicht für abgeleitete Klassen. | Uneinheitliche Liste; (b) und (c) heute ohne Fall. | Einheit je Fühlerart in `datenpunkte.json` (Quelle: Bürovorlage, offen); (b) Klasse prüfen. | klein | tief |
| 3.7 | `js/ansichten/filter.js:33` gegen `Grafik/Grafik.cs:177` | `klassenAusblenden`: der Kern prüft die ganze Basiskette (`Linie`), 3D nur die direkte Basis (`el.basis`); der Kern vergleicht Gross/Klein genau, das Schema nicht. | Heute ohne Folge (keine Klasse ist tiefer als eine Stufe abgeleitet), bricht mit der ersten zweistufigen Klasse: im Grundriss weg, in 3D da. | `Darstellung.Element` liefert die ganze Linie, `filter.js` prüft sie. | klein | tief |
| 3.8 | `Api/Ableitungen.cs` (`Gewerk`, ein Wert) | Für Koordinationsfarbe, Kollisionsmatrix, BCF-Label und Mengenliste zählt weiter **ein** Gewerk: Systemtyp des ersten Systems (nach Kennzeichen) vor dem Gewerk der Klasse. Ein Wassererwärmer bzw. Ausdehnungsgefäss zwischen zwei Gewerken wechselt sein Gewerk mit dem Kennzeichen der Systeme. | Kollision «Heizung – Sanitär» kann als «Sanitär – Sanitär» gelten (anderer Toleranzwert bzw. Zuständigkeit). | Für Geräte mit eigenem Gewerk der Klasse dieses vor dem Systemtyp nehmen (Systemtyp nur für Leitungsklassen). Frage 1. | klein | mittel |
| 3.9 | `Api/Ableitungen.cs:276` (Strangliste), `Api/Abfragen.cs:222, 317, 356` | Je Strang bzw. Element wird `DerKlasse("Datenpunkt")` neu über alle Elemente gesucht (`DerKlasse` ist ein Durchlauf über das ganze Modell) und gegen die eigenen Elemente mit `Any` verglichen. | Strangliste O(Stränge × Elemente × Strangelemente); bei grossen Modellen spürbar. | Datenpunkte einmal nach `bezuege.an` gruppieren. | klein | tief |
| 3.10 | `Api/Abfragen.cs` (`Zeitreihe`) | `DateTimeOffset.TryParse` ohne Kultur und ohne Zeitzone: «2026-10-19T00:00» gilt als Ortszeit des Servers; die Ausgabe hat keine Zone. | Mit einer echten Anlage (Modul 9) eine Stunde Versatz bei Sommerzeit möglich. | Vor Modul 9 festlegen: Ortszeit der Anlage oder UTC, Zone in der Antwort. | klein | tief (vor Modul 9 hoch) |
| 3.11 | `js/ansichten/filter.js:37` | Geschossregel für `art === 'grundriss'` in `filter.js` wird nicht mehr gebraucht (der Grundriss kommt als Plangrafik vom Kern, der zusätzlich Achsen, Unterlagen ohne Geschoss und Treppen von unten zeigt). | Tote, abweichende Regel – Falle für künftige Aufrufer. | Entfernen oder auf den Kern verweisen. | klein | tief |

Nicht geprüft: Browserreihen (`projektbrowser.browser.mjs` u. a.), Verhalten im Browser (Dialog «Sichtbarkeit/Grafiken», Reiterleiste), echte Projekte.

## 4. Offene Fragen an Christian

1. **Gewerkefilter jetzt wirksam für Leitungen:** Ein Plan mit `gewerke: [sanitaer, architektur]` zeigt keine Heizungs- und Lüftungsleitungen mehr
   (vorher alle). Ein Gerät zwischen zwei Gewerken (Wassererwärmer, Wärmetauscher) erscheint in beiden. So gewollt? Und: soll ein solches Gerät in
   Kollisionsmatrix und Koordinationsfarbe das Gewerk seiner Klasse behalten (heute: das des ersten angeschlossenen Systems, Punkt 3.8)?
2. **Ansicht löschen:** sollen die Beschriftungen der Ansicht mitgelöscht werden (Rückgängig mit Strg+Z), statt das Löschen abzulehnen?
3. **Ansichtsvorlage:** Kopie wie heute (einmal anwenden) oder Verknüpfung (Änderung der Vorlage wirkt auf alle zugewiesenen Ansichten)? Und welche
   Einstellungen gehören dazu (Darstellung, Ausrichtung, Masseinheit, Rohrbezeichnung)?
4. **Duplizieren:** nur die Ansicht (heute) oder wahlweise «mit Beschriftungen»?
5. **MSRL in der Oberfläche:** Knopf «Datenpunkte vorschlagen» und eine Datenpunktliste als Listenansicht im Register MSRL? Die Grundausstattung
   (`kataloge/datenpunkte.json`) ist weiterhin eine Annahme – gibt es eine Bürovorlage (Datenpunkt- bzw. Funktionsliste)?
6. **3D-Ansicht:** Detailgrad und Massstab ausblenden oder den Detailgrad im 3D wirken lassen?
