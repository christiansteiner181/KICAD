# Testbericht Modul 7 Teil A – Planblatt wie H+K (ENTWURF)

Stand 20.10.2026 · gebaut am 20.10.2026 · Commit `636e78c` (main; Bau `0c5ff5d` von hlks-modul, zusammengeführt vom Hauptlauf) · **Entwurf, Abnahme durch Christian offen**

Hinweis: Entwurf von hlks-test aus `entwurf/modul-7.md`, `zuteilung.md`, `src/Hlks.Web/OBERFLAECHE.md` (Abschnitt «Planblatt wie H+K (Modul 7 Teil A)»), dem Code (Stellen genannt) und dem
Bericht des Bauagenten. **Die Testzahlen in Abschnitt 3 stammen aus den Läufen des Bauagenten und des Hauptlaufs (Auftrag); ich habe nur die Web-Tests (475, alle grün) und den Node-Test `planblatt-hk.test.mjs` (10, grün)
auf dieser Arbeitskopie wiederholt, nicht den Kern, MCP, die übrigen Node-Tests und die Browser-Reihen.** Selbst gemacht habe ich: die Bilder der Anleitung (22 Bilder aus Szenen mit echten Klicks im Beispielprojekt, alle Szenen ohne Fehler) und deren Durchsicht; sie bestätigen die Abläufe
unten am laufenden Programm. Das Vergleichsbild Kopf KICAD gegen H03 (lokal `%TEMP%\HLKS_CAD\m7_vergleich_kopf.png`) hat der Hauptlauf angesehen: Aufbau stimmt.

## 1. Kurzfassung

Teil A macht das Planblatt des HLKS-CAD im PDF zu einem H+K-Plan. Alles Fachliche (Buchstaben, Datum, Nummern, Befund) macht der Kern; die Oberfläche sammelt Eingaben und zeigt Ergebnisse.

- **Plankopf wie H+K:** oben beide Standorte (Bolligen fett mit Mail, Thun) und das Logo; darunter «Projektphase:», «Projekt:» (zwei Zeilen: Objekt und Plantitel), «Plannummer:»; rechts «Erstelldatum:», «Rev. Index:»,
  «Rev. Datum:», «Kontrolliert:», «Massstab: 1 : 50»; unten die Tabelle «Planänderungen» mit beliebig vielen Zeilen (der Kopf wächst nach oben, höchstens zwölf Zeilen sichtbar). Absender aus `kataloge/buero.json`.
- **Schema-Kopf:** schmal, unten links, nur Bolligen mit Telefon und Web, ohne Logo, Massstab und Rev.-Felder; Vorgabe, wenn alle Ansichten des Blatts Schemas sind, sonst Eigenschaft `kopfart`.
- **Plannummern-Vorschlag** beim Anlegen (Dialog «Planblatt anlegen»: Gewerk, Geschoss, Art): «1-0768 H02» für Heizung EG im Beispielprojekt, mit Begründung des Kerns; Eingaben von Hand bleiben.
- **Revisionen:** Befehl «Revision hinzufügen» (Menüband, Kontextmenü, Eigenschaften), Revisionsliste als Tabelle in den Eigenschaften, «Revision …» für alle Pläne eines Plansatzes (Serien-PDF-Dialog); Rückgängig wie alle Befehle.
- **Revisionsstempel** (rot, schräg) als Eigenschaft `stempel` und im Serien-PDF; **Titelband «Planinhalt»** abschaltbar, Vorgabe aus.
- **Listen auf Planblättern:** `ansicht_platzieren` nimmt Listenansichten, das PDF zeigt eine Tabelle (damit entfällt die offene Grenze «Sanitär-Zusammenstellung nicht platzierbar» aus dem Testbericht 4A).
- **Befund P27:** Plannummer doppelt im Projekt (Warnung am späteren Blatt, Vorschlag: nächste freie Nummer).

Normwerte kommen nicht vor; die Plannummern-Konvention ist H+K-Brauch aus den Bernstrasse-Plänen (Annahmen unten), die Absenderdaten stammen aus echten Plänen.

## 2. «Fertig, wenn …» Punkt für Punkt

Spezifikation: «Ein Planblatt «Heizung Erdgeschoss» des Beispielprojekts als PDF im Kopf aussieht wie der H+K-Revisionsplan (Vergleich mit dem Kopf von Bernstrasse H03, Bild nebeneinander): beide Absender, Logo,
Projekt zweizeilig, Plannummer automatisch vorgeschlagen, Revision B mit Datum und Planänderungstabelle (A, B), Stempel zuschaltbar; ein Schema-Blatt mit schmalem Kopf unten links; eine Liste auf einem Planblatt.»

| Kriterium | Ergebnis | Nachweis |
| --- | --- | --- |
| 1 Beide Absender und Logo im Kopf | erfüllt | Anleitungsbild `m7-plankopf_2`: «H+K Planungs AG, Bolligen» (fett, mit Mail) und «… Thun» (Kasernenstrasse 17, Tel., Mail), Logo rechts. Kern-Test `Plankopf_ZeigtBeideStandorteMitAdresseTelefonMail`, `Standorte_AusBueroJson_…`; Vergleichsbild H03 (Hauptlauf). Thun ohne Web (steht auf keinem Plan). |
| 2 Projekt zweizeilig | erfüllt | Bild: «Testobjekt Worb (fiktiv)» und «Heizung Erdgeschoss»; Test `Plankopf_ProjektZweizeilig_ObjektUndPlantitel`. |
| 3 Plannummer automatisch vorgeschlagen | erfüllt | Dialog «Planblatt anlegen»: Heizung + EG → «1-0768 H02», Titel «Heizung Erdgeschoss», Begründung «Geschoss 2 von 3 (von unten, nach Kote) – H02 (Zählung ab 01)» (Bild `m7-planblatt-anlegen`). Von Hand getippte Nummer bleibt; zweites Blatt bekommt H03 und die Meldung «schon vergeben». Browser planblatt-hk, Kern `Vorschlag_…` (8 Tests) und `PlanblattAnlegen_…` (2). |
| 4 Revision B mit Datum und Planänderungstabelle (A, B) | erfüllt | Bild: «Rev. Index: B», «Rev. Datum: 21.01.2026», Tabelle mit A und B. Tests `Revision_NaechsterBuchstabeDatumHeuteBeschreibungPflicht`, `Plankopf_RevIndexDatumUndPlanaenderungstabelle`, `Plankopf_WaechstNachObenMitJederZeile_UndZeigtNurDieLetztenZwoelf`; Rückgängig und Trockenlauf getestet. |
| 5 Stempel zuschaltbar | erfüllt, Lage zur Beurteilung | Eigenschaft `stempel` am Blatt/Plansatz und Häkchen «Revisionsstempel auf alle Pläne» im Serien-PDF (Vorbelegung «Revisionsplan <Datum>»); rot, schräg, im Sammel-PDF auf jeder Seite (Browser planblatt-hk: Farbe #E8202A, 2 Seiten). **Beobachtung:** der Stempel liegt auf der Mitte des Kopfs und überdeckt «Erstelldatum:» und «Rev. Index:» (Bild `m7-stempel-eigenschaft_2`) – Annahme 9. |
| 6 Schema-Blatt mit schmalem Kopf unten links | erfüllt | Bild `m7-schema-kopf_2` und `_3`: nur Bolligen mit Tel. und Web, kein Massstab, Planänderungen rechts; Tests `SchemaKopf_NurSchemaBlatt_SchmalUntenLinks_NurBolligenMitWeb_OhneMassstab`, `SchemaKopf_PositionLinksGegenGrundrissKopfRechts`, `FreieLage_RechnetMitDerKopfgroesse_SchemaKopfLinks`. |
| 7 Liste auf einem Planblatt | erfüllt, ohne Umbruch | Bild `m7-liste-planblatt_2`: Raumliste als Tabelle (13 Räume, Gesamtfläche 380,00 m²). Tests `Liste_AlleListenartenErgebenEineTabelleMitTitelUndLinien`, `Liste_SanitaerZusammenstellungHatDieDreiBloeckeMitAbgerundetenZahlen`, `Liste_UnbekannteListeUndZuVieleZeilenWerdenBegrenzt`. Grenzen: Arial 2.2 mm, höchstens 45 Zeilen, 380 mm, Spalten ≤ 60 mm; Umbruch in Teil B. |
| 8 Befund doppelte Plannummer (Spezifikation Punkt 5) | erfüllt | Bild `m7-befund-p27`: «Plannummer «1-0768 H02» steht auch auf PB-1», Vorschlag «Nächste freie Plannummer: 1-0768 H03». Tests `P27_DoppelteNummerNenntNaechsteFreie_…`, `P27_DreifachUndOhneFachbuchstabe`. |
| 9 Revision für alle Blätter des Satzes (Punkt 4) | erfüllt | Knopf «Revision …» im Kopf des Plansatzes (Bild `m7-satz-revision`); Browser planblatt-hk: beide Pläne bekommen dieselbe Beschreibung und dasselbe Datum, Index je Blatt (PB-1 A, PB-2 C). Kern `PlansatzRevision_…` (2 Tests). |

Weitere Zusicherungen:

| Zusicherung | Nachweis |
| --- | --- |
| Keine Logik in der Oberfläche, alles über die Kern-API | Index, Datum, Plannummer, Titel und Befund kommen vom Kern (Befehle `revision_hinzufuegen`, `plansatz_revision_hinzufuegen`, Abfrage `plannummer_vorschlag`, `planblatt_anlegen` ohne Nummer); die Oberfläche (`planblatt-hk.js`) stellt nur dar und sammelt ein (Node-Test). |
| Keine Normwerte erfunden | Plannummern-Schema aus den Bernstrasse-Plänen (H+K-Konvention, Annahmen unten); Absenderdaten mit Quelle in `buero.json`; Stempelfarbe/-winkel als Näherung gekennzeichnet. |
| Rückgängig, Trockenlauf, Herkunft | `Revision_TrockenlaufAendertNichts_RueckgaengigNimmtSieZurueck_HerkunftStehtDran`, `PlansatzRevision_TrockenlaufRueckgaengigEinPaket`. |
| MCP | MCP-Reihe 65 grün (vorher 63). |
| Bedienungsanleitung | Kapitel «m7» (11 Funktionen, 22 neue Bilder; zusätzlich `m1a-planblatt`, `m1a-ansicht-platzieren`, `m1a-pdf`, `m3-schema-planblatt` neu erzeugt); die Anleitung baut mit 273 Funktionen, 259 mit Bild. |

## 3. Testzahlen (aus Auftrag und `zuteilung.md`, nicht von mir gestartet)

| Bereich | Stand `636e78c` (Bauagent/Hauptlauf) |
| --- | --- |
| Kern | **4051** (5 Zeitgrenzen unter Last, einzeln grün); darin `PlanblattHkTests` **54** |
| MCP | 65 |
| Web | 475 |
| Node | 886 |
| Browser | planblatt-hk **41** (neu), serienpdf 67, menueband 191 (beide Symbolstile), projektbrowser 128, nachbearbeitung-n3 88, sanitaer-belastung 122 |

Die Gruppen der Kern-Tests (Standorte, Plankopf, Titelband, Revisionen, Plansatz-Revision, Vorschlag, P27, Stempel, Schema-Kopf, Listen) stehen in `tests/Hlks.Kern.Tests/PlanblattHkTests.cs`. Die fünf Zeitgrenzen sind in meinem Auftrag nicht namentlich genannt.

## 4. Befunde und ihre Behebung

| # | Befund | Fund | Behebung |
| --- | --- | --- | --- |
| 1 | Der Revisionsstempel steht schräg auf der Mitte des Kopfs und überdeckt «Erstelldatum:» und «Rev. Index:» (Quelltext: «über dem Kopf (Mitte des Kopfs, …)»; Bild `m7-stempel-eigenschaft_2`) | Bilddurchsicht | offen, Entscheid Christian (Lage: über dem Kopf oder darauf) – Annahme 9 |
| 2 | Feldbeschriftungen im Schema-Kopf ohne Doppelpunkt («Projektphase», «Plannummer»), im Grundriss-Kopf mit («Projektphase:», «Plannummer:»); «Projekt:» und «Erstelldatum:» haben ihn in beiden | Bilddurchsicht (`m7-schema-kopf_3`) | offen, kosmetisch; am echten Strangschema Wasser (S_Strangschema_Wasser-A0.pdf) prüfen |
| 3 | «Erstelldatum:» zeigt das Datum des PDF-Abrufs, solange die Eigenschaft `datum` leer ist (Kern: leer = heute); im Bild 20.10.2026 | Bilddurchsicht, Quelltext `HkPlankopfDaten.Erstelldatum` | offen, Frage: soll das Datum beim Anlegen des Blatts fest gesetzt werden? |
| 4 | Revisionstabelle in der Eigenschaftenpalette ist schmal; die Beschreibung wird abgeschnitten dargestellt (nur Darstellung, bearbeitbar) | Bilddurchsicht (`m7-revisionsliste`) | offen, gering |
| 5 | Listen auf A3 erscheinen klein (Arial 2.2 mm) und kürzen lange Texte mit «…» (Spalte «Endgeräte» der Raumliste); Umbruch fehlt | Bilddurchsicht (`m7-liste-planblatt_2`) | bekannte Grenze, Teil B |
| 6 | Hinweise der Anleitung zu «Listen nicht platzierbar» (`m4-liste`, `m4-csv`) und die Beschreibung des alten Plankopfs (`m1a-planblatt`) waren überholt | Anleitung | nachgeführt (Hinweise und Bilder) |

Aus meiner Sicht **keine Befunde, die die Tests des Bauagenten widerlegen**; Befund 1 bis 3 sind Fragen zur Gestaltung, die nur Christian am echten Plan entscheiden kann. Ich habe keinen Produktcode und keine Tests geändert.

## 5. Annahmen zur Bestätigung

Bitte mit Ja/Nein oder Wert beantworten (Annahmen des Bauagenten, 1 bis 8; 9 und 10 aus meiner Durchsicht).

1. **Fach K:** in den echten H+K-Plänen steht «K» für **Koordination**, im Kern heisst K bisher **Kälte** (`PlanGewerk`). Der Plannummern-Vorschlag verwendet K für Koordination; Kälte und Elektro bekommen keinen Fachbuchstaben und keinen Vorschlag. **Entscheid nötig:** Koordination = K, oder Kälte = K (wie bisher im Kern)? Dann wäre ein anderer Buchstabe für die Koordination zu wählen.
2. **Absenderdaten** (Bolligen: Dorfmärit 9, 3065 Bolligen, 031 / 924 25 35, infob@hkplanung.ch, www.hkplanung.ch; Thun: Kasernenstrasse 17, 3600 Thun, 033 / 225 25 45, infot@hkplanung.ch): aus `1-0768-H03-Heizung Erdgeschoss-.pdf` Seite 1 bzw. den Strangschemas (Web) gelesen. **Thun ohne Web** (auf keinem Plan). Stimmen die Werte, und soll Thun eine Web-Adresse bekommen?
3. **«Rev. Datum» als eigene Zeile** unter «Rev. Index» (die Spezifikation hatte das Datum darunter als Zusatz). Passt das?
4. **Titelband «Planinhalt»: Vorgabe aus**, einschaltbar am Planblatt und in der Projektinformation (H+K-Pläne haben es nicht; der Plantitel steht im Projektfeld). Einverstanden?
5. **Plannummern-Regeln** (H+K-Brauch aus den Bernstrasse-Plänen, keine Norm): Reihenfolge nach der Kote der Geschosse; Sanitär ab 00, sonst ab 01; FBH ab EG aufwärts (H13 ff.); Steigzonen H21; Sanitär-Sonderblätter: Grundleitungen = Zahl der Geschosse, Einlagen die drei folgenden, Kanalisation = Zahl der Geschosse + 4. Gelten diese Regeln auch für andere Objekte als die Bernstrasse?
6. **Proportionen des Plankopfs** (Zeilenhöhen, Schriftgrössen, Spaltenbreiten, Schema-Kopf 150 mm, Titelband 10 mm) sind aus den Renderings geschätzt, nicht aus den PDF-Objekten gelesen. Das Vergleichsbild H03 gegen KICAD zeigt den richtigen Aufbau; Mass und Schrift bitte am Papier beurteilen.
7. **Listen im PDF:** Arial 2.2 mm, höchstens 45 Zeilen (danach «… n weitere Zeilen»), höchstens 380 mm Breite, Spalten ≤ 60 mm; Umbruch in Teil B. Reicht das für Teil A?
8. **Revisionsindex je Blatt** (nicht je Plansatz): hat ein Plan A und B und ein anderer keine, bekommen sie bei «Revision für alle» C bzw. A. Einverstanden?
9. **Revisionsstempel:** Farbe #E8202A, 12° und die Lage (Mitte des Kopfs, überdeckt Felder) sind Näherungen. Soll der Stempel **über** dem Kopf (oberhalb der Absenderzeile) stehen, damit keine Felder verdeckt werden?
10. **Plannummer des Strangschemas Abwasser** steht auf dem echten Plan als «1-0759-4» (die anderen «1-0768 S…»): Tippfehler im echten Plan? Und `einlageplaene_anlegen` (Knopf «Einlageplan», N3) nutzt eine **eigene** Nummernlogik: soll sie den neuen Vorschlag (Plannummern-Vorschlag, Art «Einlagen») verwenden?

## 6. Abnahme-Prüfschritte für Christian

Voraussetzung: KICAD auf dem geprüften Stand (Zweig «geprueft», erst nach grünem Endlauf auf `636e78c`), Beispielprojekt «MFH Musterweg» offen. Zum Vergleich der Kopf von `1-0768-H03-Heizung Erdgeschoss-.pdf`
(Revisionspläne Bernstrasse, Ordner Heizung) und das Strangschema Wasser A0 neben dem Bildschirm. Die Bedienungsanleitung (Kapitel «Planblatt wie H+K») führt zu jedem Schritt mit Bildern und abhakbaren Punkten.

1. Verwalten › Projektinformationen: Projektnummer «1-0768», Objekt und Phase eintragen (ohne Projektnummer schlägt der Kern nur «H02» vor und sagt es).
2. Ansicht › Planblätter › «Planblatt»: Gewerk Heizung, Geschoss EG: Vorschlag «1-0768 H02», «Heizung Erdgeschoss», Begründung unter dem Feld. Geschoss 1.OG wählen (H03); Nummer von Hand tippen, Geschoss wechseln: die Nummer bleibt. «Anlegen».
3. Weitere Vorschläge ansehen: Art «Fussbodenheizung (FBH)» (H13), «Steigzonen Heizung» (H21), Sanitär mit Grundleitungen, Einlagen, Kanalisation: stimmen die Nummern nach Annahme 5? Gewerk Koordination und Kälte: Fach K (Annahme 1).
4. Mit «Ansicht platzieren» den Grundriss EG auf das Blatt setzen; dann «Revision …» (Menüband): Beschreibung «Beschriftungen ergänzt» → Revision A mit heutigem Datum. Ein zweites Mal mit Datum «21.01.2026» → B. Strg+Z nimmt B zurück, danach B wieder hinzufügen.
5. Rechtsklick auf das Planblatt im Projektbrowser › «Revision …»; im Eigenschaftenfeld «revisionen» eine Beschreibung ändern, eine Zeile mit «×» löschen, «+ Revision hinzufügen».
6. «PDF öffnen»: Kopf mit dem von H03 vergleichen: beide Absender, Logo, Projekt zweizeilig, Plannummer, «Rev. Index: B», «Rev. Datum», «Massstab: 1 : 50», Tabelle «Planänderungen» A und B (Annahmen 2, 3, 6). Mehr als zwölf Revisionen hinzufügen: der Kopf wächst nach oben, es erscheinen die letzten zwölf.
7. Eigenschaft «titelband» des Blatts ankreuzen: Band «Planinhalt» über dem Kopf; wieder entfernen (Annahme 4).
8. Eigenschaft «stempel» mit «Revisionsplan 21.01.2026» füllen: PDF ansehen, Lage des Stempels beurteilen (Annahme 9). Text löschen: kein Stempel.
9. Zweites Blatt «Heizung 1.OG» anlegen. Ansicht › Planblätter › «Serien-PDF …», Plansatz Heizung: «Revisionsstempel auf alle Pläne» ankreuzen, exportieren (Ordner unter dem Benutzerordner wählen, nicht auf W:), Sammel-PDF öffnen: Stempel auf beiden Seiten. «Revision …» im Kopf des Satzes: beide Blätter bekommen dieselbe Beschreibung (Annahme 8).
10. Planblatt «Strangschema Sanitär» (Gewerk Sanitär, Nummer S00) anlegen, das Schema platzieren, PDF öffnen: schmaler Kopf unten links, nur Bolligen mit Tel. und Web, ohne Massstab, Planänderungen rechts; mit dem Strangschema Wasser A0 vergleichen (Doppelpunkte, Befund 2). Eigenschaft «kopfart» = grundriss: voller Kopf unten rechts.
11. Eine Raumliste (Ansicht › Erstellen › «Liste») anlegen, dazu ein Planblatt; «Ansicht platzieren» › die Liste wählen: im PDF steht die Tabelle. Ebenso die «Sanitär-Zusammenstellung (W3)» (Gebäudetechnik › Apparate und Armaturen): Zahlen mit der Bildschirmliste vergleichen.
12. Zwei Blätter auf dieselbe Plannummer setzen (Eigenschaft «nummer»): Verwalten › Prüfbefunde zeigt P27 am späteren Blatt mit dem Vorschlag der nächsten freien Nummer; «Planblatt anlegen» mit schon vergebener Nummer wird abgelehnt.

## 7. Offene Punkte

| # | Punkt | Wer / wann |
| --- | --- | --- |
| 1 | **Teil B:** Schnitt- und Ansichtsmarken mit Blattverweis, Aussparungsboxen in Fachplänen, Koordinationsplan in Gewerkefarben, Architektur-Unterlage DWG/PDF, **IFC-Export mit IDS und Exportbericht** (auch der IFC-Export der Belastungswerte aus Modul 4A), **Umbruch langer Listen** auf weitere Blattbereiche | Hauptlauf (Spezifikation Teil B) |
| 2 | Entscheide zu den Annahmen 1 bis 10 (zuerst Fach K) | Christian |
| 3 | Befund 1 bis 3 (Stempellage, Doppelpunkte im Schema-Kopf, festes Erstelldatum) am echten Plan klären und bei Bedarf als Auftrag an den Kern | Christian / Hauptlauf |
| 4 | Endlauf `stand_pruefen` auf `636e78c` und Zeitgrenzen (5 Kern-Tests unter Last, einzeln grün): Zahlen aus Abschnitt 3 gegenprüfen; Marke «geprueft» setzen | Hauptlauf |
| 5 | Plannummern der Annahme 5 an einem zweiten Objekt prüfen (heute nur Bernstrasse als Vorlage) | Christian |
| 6 | Bedienungsanleitung: Kapitel «m7» steht (11 Funktionen, Bilder lokal); für die echten Pläne gibt es keine Bilder (nur Beispielprojekt) | erledigt / bei Bedarf |

## 8. Zuteilung und Modellwahl (aus `zuteilung.md`)

| Teil | Inhalt | Agent | Ergebnis |
| --- | --- | --- | --- |
| – | Spezifikation Teil A (`entwurf/modul-7.md`; Modul 4B/5/6 brauchen fehlende Normen) | Hauptlauf | ok |
| A1–A7 | Absender, Plankopf, Schema-Kopf, Revisionen, Plannummern-Vorschlag, Stempel, Listen, Oberfläche | hlks-modul (worktree, Ports 5177/5179–5189) | ok – Commit `0c5ff5d`, zusammengeführt `636e78c`; Nacharbeit in meinen Unterlagen nicht vermerkt |
| – | Bedienungsanleitung Kapitel «m7» und Testbericht (dieser Entwurf) | hlks-test | Entwurf, Prüfung durch den Hauptlauf |

Für hlks-modul wird keine Änderung der Zuteilungstabelle vorgeschlagen (umfangreicher Routinebau nach Spezifikation, hier ohne vermerkte Nacharbeit); Entscheid beim Hauptlauf.

## Bekannte Grenzen dieser Prüfung

- Neu gestartet habe ich nur die Web-Tests (475 von 475) und den Node-Test der Oberfläche `planblatt-hk.test.mjs` (10 von 10); die übrigen Testzahlen sind die des Bauagenten und des Hauptlaufs. Die Anleitungsbilder (22 neue, 4 nachgeführte) liefen am Bau von `636e78c` im Beispielprojekt und sind eine zusätzliche Bestätigung der Bedienwege, ersetzen aber keinen Test.
- Alle Bilder und Abläufe nutzen das fiktive Beispielprojekt (Projektnummer 1-0768, «Testobjekt Worb (fiktiv)», erfundene Revisionsdaten); der Vergleich mit dem echten H03 stammt vom Hauptlauf (Vergleichsbild), nicht von mir.
- Das PDF wird in der Anleitung mit Windows.Data.Pdf gerendert (der Betrachter des Browsers lässt sich nicht aufnehmen); Schriftbild und Proportionen im Papierausdruck sind damit nicht geprüft (Annahme 6).
- Die Plannummern-Regeln habe ich nicht gegen weitere echte Pläne geprüft; welche fünf Kern-Tests unter Last die Zeitgrenze rissen, ist unbekannt.
