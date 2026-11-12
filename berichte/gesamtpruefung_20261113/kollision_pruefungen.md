# Gesamtprüfung Runde 4 – Bereich kollision_pruefungen

Geprüft (vollständig gelesen): `Kollision/Geometrie3.cs`, `Schmalphase.cs`, `Pruefnetz.cs`, `Raumindex.cs`, `Durchgaenge.cs`,
`Oeffnungsdeckung.cs`, `Kollisionspruefung.cs`, `Modell/Netz.cs`, `Pruefung/Pruefungen.cs`; aus `Pruefkoerper.cs` Kopf, Achskörper,
Prüfkörper, Leitung, Weglassen und `Pruefnetzbau` (Zeilen 1–220, 690–918); aus BCF nur die Kamera (`BcfExport.Viewpoint`,
`BcfMarker.MarkerKamera`, `ObenZu`).
**Nicht gelesen:** `Typenliste.cs`, `Kollisionsliste.cs`, `Vorgaben.cs` (nur `AbstandWeichFuer`), `BcfFormat/BcfLeser/BcfBefehle/BcfSchema`,
`Pruefkoerper.cs` 220–690 (Erzeugen, Öffnungen laden), `Leitungspruefungen.cs` (P13–P18: in `leitungen2.md` geprüft, nicht doppelt).

## 1. Kurzfazit

Die Geometrie der Kollisionsprüfung ist sauber gebaut und hält der Nachrechnung stand (Dreieck–Dreieck, Abstände, Quader/Zylinder–Dreieck,
Baum, Projektion der Durchgänge, BCF-Kamera). Drei echte Fehler sind korrigiert: die Netzverfolgung brach still nach 2000 Bauteilen ab
(betraf Rechnungen und Stranglisten grosser Netze), bündig überlappende Körper – z. B. zweimal dasselbe Gerät am selben Ort – galten nur als
Berührung, und Familienkörper aus überlappenden Grundkörpern mit gemeinsamen Ecken hatten im überlappten Bereich kein «innen».
Die Leistung der Kollisionsgeometrie ist für einige tausend Elemente unkritisch (6000 Kapseln: Baum und Paare 21 ms); langsam ist dagegen
`Netz.Verbraucher` (quadratisch, 12 s bei einem Strang mit 6000 Bauteilen). Offen bleiben vor allem die Kapselenden freier Leitungsenden
(Fehlalarm bis zum Radius vor einer Wand) und die Verlängerung der Rechteckkanäle an Knicken.

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Test (Filter, Ergebnis) | Commit |
|---|---|---|---|---|---|
| 1 | `Modell/Netz.cs` (Folgen), `Api/Abfragen.cs` (NetzFolgen) | `Folgen` hatte die Vorgabe `maxSchritte = 2000`; kein Aufrufer setzte sie. In Netzen mit mehr als 2000 Bauteilen in einer Richtung fehlten den Aufrufern die entferntesten Bauteile ohne Hinweis: `Netz.Verbraucher` (Strangliste, Systemtext, HKAP-Stränge), `SanitaerDimensionierung` (Wege stromauf/stromab, massgebende Längen), `LueftungNetz`, `KanalDruckverlust`. Nachgewiesen: Kette aus 2500 Rohren → 2000 statt 2499 Schritte. | Vorgabe `int.MaxValue` (die Suche endet von selbst, jedes Element wird einmal besucht). Nur die Abfrage `netz_folgen` kürzt weiter bei 2000 (Antwortgrösse) und sagt es jetzt mit `hinweis`. | Neu `NetzFolgenTests` (Kette 2500, Kreislauf endet): `FullyQualifiedName~NetzFolgenTests` 2/2; abhängig `SanitaerDimensionierungTests|HkapStraengeTests|AbfragenTests` 107/107 | `321493d` |
| 2 | `Kollision/Schmalphase.cs` (Netz gegen Netz) | Bündig überlappende Körper galten als «Berührung»: liegen alle Flächen aufeinander bzw. enden in der Ebene der anderen, durchdringt sich kein Dreieckspaar (Möller: Ecken «in der Ebene» = höchstens Berührung) und keine Ecke liegt innen. Handbeispiel Schrank 600 × 400 × 1800 zweimal am selben Ort: vorher Berührung, Tiefe 0; dito gleicher Querschnitt um 300 längs verschoben. Betrifft Geräte/Formteile/Bauteile als Netz (Leitungen als Achskörper waren nicht betroffen). | Bei Berührung ohne Schnitt und ohne Enthaltensein wird die Mitte des gemeinsamen Hüllquaders geprüft: liegt sie in beiden geschlossenen Netzen → Schnitt, Tiefe = kleinerer Abstand zu den beiden Oberflächen (Doppel: 200 = halbe kleinste Kante; versetzt: 150). Körper, die nur aneinander liegen, haben einen flachen gemeinsamen Hüllquader → weiterhin Berührung. | `NetzNetz_BuendigUeberlappt_IstSchnitt_NichtBeruehrung`; Lauf `KollisionGeometrie|Kollisionspruefung|Referenz3bKollision|KollisionTeileOeffnungen` 112/112 (dabei liefen einmal auch die Echt-Tests mit, 3 min – unverändert grün) | `23152be` |
| 3 | `Kollision/Pruefnetz.cs` (Topologie) | Mehrere Netze eines Körpers (`Pruefnetz.Aus`, Familienkörper aus Grundkörpern) wurden über gleich liegende Ecken zu einem Teil verschmolzen. Überlappen sich zwei geschlossene Grundkörper und teilen Ecken (Gerät 600 × 400 × 1800 und Sockel 600 × 400 × 100 auf denselben vier Bodenecken), trifft ein Strahl im überlappten Bereich zwei Flächen desselben Teils = gerade = «aussen» (nachgewiesen: `Lage(300, 200, 50)` = Aussen). Folge: Leitungsachse dort nicht «innen», Durchgänge/Enthaltensein falsch. | Für sich geschlossene Quellnetze bleiben eigene Teile (Ecken werden nur innerhalb des Quellnetzes verschweisst); für sich offene Netze werden wie bisher gemeinsam verschweisst (Flächenstücke, die sich erst zusammen schliessen). Netze aus der Netzdatei (ein Feld) unverändert. | `Pruefnetz_AusMehrerenNetzen_GeschlosseneBleibenEigeneTeile_OffeneSchliessenSichZusammen`; Lauf `(KollisionGeometrie|Kollisionspruefung|Referenz3bKollision|KollisionTeileOeffnungen|Aussparung)&!~Echt` 361/361 | `08cafd2` |

Kein Test war vor den Änderungen rot.

## 3. Nachgerechnet und in Ordnung (Handbeispiele)

- **Dreieck–Dreieck (Möller mit Toleranz)**: Trennung über Ebenenabstände, Intervalle auf der Schnittgeraden, koplanar/fast parallel über den
  echten Abstand; Durchdringung erst ab 0.1 mm Überlappung und wenn beide Dreiecke die Ebene des anderen durchstossen. Stimmt; die Lücke
  «alles koplanar» ist Korrektur 2.
- **Zylinder–Dreieck**: Dreieck auf die Schicht zwischen den Endflächen beschnitten (höchstens 5 Ecken, Feld 8 reicht), Ursprung im projizierten
  Vieleck bzw. Kantenabstand ≤ r. Sonderfall Dreieck in einer Ebene durch die Achse (alle Kreuzprodukte 0) fällt richtig auf den Abstand 0.
- **Quader–Dreieck**: 13 Trennachsen vollständig; negative halbe Kanten (Quader kleiner als 2 × Toleranz) → kein Treffer.
- **Kapsel–Kapsel Enthaltensein**: beide Achsenden näher als R − r an der anderen Achse – richtig (Abstand längs einer Strecke ist konvex).
- **Umschriebener Zylinder im Prüfnetz**: 24-Eck, Eckradius r / cos(7.5°) = 1.0086 r → höchstens 0.86 % zu gross, wie im Kopf angegeben.
- **Durchgang und Projektion**: Rohr r = 50 mm, Wand 200 mm, 45° schräg: Tiefe (Achse im Bauteil) 282.8, Dicke = Tiefe · cos = 200 ✓, Winkel =
  asin(|n·d|) = 45° ✓, Ellipse a = r / cos = 70.7, b = 50, Kreis 141.4 ✓; Versatz Ein-/Austritt in der Ebene = 200 · tan 45° = 200 →
  DurchbruchKreis 141.4 + 200 = 341.4 ✓ (für rund exakt, für rechteckig obere Schranke – schon in `architektur_aussparungen.md`).
- **Deckungsprüfung**: Überstand bei schräger Querung R · cot(Winkel), gedeckelt – stimmt.
- **BCF-Kamera**: Blick (1, 1, −1)/√3 und Oben (1, 1, 2)/√6: Skalarprodukt 0, beide Länge 1; Standpunkt = Ziel − Abstand · Blick (Kamera im
  Südwesten) ✓. Marker: Oben = z ohne Blickanteil, bei senkrechtem Blick y ✓.
- **Hüllquader-Baum**: Paare innerhalb eines Baums gegen rohe Zählung geprüft (6000 Kapseln: 1712 = 1712). Stapel 128 reicht (Mediantiefe).
- **Netz.Folgen bei Maschen/Kreisläufen/mehreren Quellen**: jedes Element einmal, endet immer; der nicht verfolgte Eintrittsanschluss führt nur
  zum schon besuchten Vorgänger. Mehrere Quellen stromauf werden alle erreicht.
- **Leistung**: 6000 Kapseln – Baum + Paare 21 ms, Schmalphase der 1712 Paare 2 ms; 2000 Kapseln × 400 Wandnetze 0.5 s.
  `Pruefungen.Alle` auf 6000 Rohren 24 ms (Grundprüfungen).

## 4. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 1 | `Modell/Netz.cs` 99–118 (`Verbraucher`) | Je Bauteil des Strangs zwei vollständige Breitensuchen. Gemessen (ein Strang als Kette): 1000 Bauteile 0.4 s, 3000 → 2.8 s, 6000 → 12 s. Mit Korrektur 1 fällt die frühere Deckelung bei 2000 weg (richtig, aber bei sehr grossen Netzen bis 1.5 × langsamer). | Strangliste, Systemtext, HKAP-Stränge rufen das je Strang auf – in grossen Projekten Sekunden bis Minuten. | Eine Suche mit allen Strangbauteilen als Start (gesamt) und eine mit Halt an fremden Strängen (direkt). Nicht gebaut, weil das Ergebnis bei Anschlüssen «beide» an Leitungen von der Besuchsreihenfolge abhängen kann – braucht Vergleichstests. | mittel | mittel |
| 2 | `Kollision/Schmalphase.cs` 180–190, `Pruefkoerper.cs` 35 (ANNAHME Kapsel) | Runde Leitungen sind Kapseln; am freien Ende ragt die Halbkugel um r über das Ende. Nachgerechnet: Rohr d 110 endet 40 mm vor einer Wand → «Schnitt», Tiefe 15 mm (hart), obwohl 40 mm Luft. Endet es bündig an der Wand → Tiefe 55 mm, `AmEnde` = false (Punkt liegt genau in der Endebene, Bedingung > Toleranz). `AmEnde` unterdrückt nur bei gemeinsamem Nachbarn. | Fehlalarme an offenen Leitungsenden und an Leitungen, die stirnseitig vor Bauteilen enden (Anschlussstutzen vor der Wand). Als Annahme dokumentiert, aber nie Christian vorgelegt. | Ebene Enden: Treffer, die nur in der Halbkugel liegen (Fusspunkt jenseits der Endebene), nicht als hart werten bzw. mit `ZylinderDreieck` (gibt es schon) nachprüfen; `AmEnde` mit ≥ −Toleranz. | mittel | mittel |
| 3 | `Kollision/Pruefkoerper.cs` 758–763 | Rechteckkanal als Polylinie: jeder Quader wird am Knick um max(b, h)/2 verlängert. Kanal 1000 × 200, Knick in die Senkrechte (querDrehung 90): nötig wären h/2 = 100, verlängert wird um 500 → 400 mm «Geisterkanal» 1000 × 200 hinter dem Knick; bei h > b im Grundriss ebenso. | Fehlalarme hinter Knicken alter Polylinien-Kanäle (neue Kanäle sind Geraden mit Formteilen – daher selten). | Verlängerung = halbe Ausdehnung des Nachbarstücks in Achsrichtung (`Ausdehnung(d)` des Nachbarn), gilt bis 90°. | klein | tief |
| 4 | `Modell/Netz.cs` 70 (`LaengeBisHier`) | Die Länge folgt dem Weg mit den wenigsten Bauteilen (Breitensuche), nicht dem kürzesten oder längsten Weg. In Maschen (Ringleitung) ist das weder die kürzeste noch die massgebende Länge. | `SanitaerDimensionierung` nimmt daraus Längen bis zum entferntesten Verbraucher – in Ringnetzen möglicherweise zu kurz. In Bäumen exakt. | Entscheiden, welche Länge in Maschen gilt (siehe Frage 2); dann Dijkstra bzw. längster Weg. | mittel | mittel (nur Ringnetze) |
| 5 | `Kollision/Pruefnetz.cs` (Topologie) | Rest von Korrektur 3: überlappen sich geschlossene Schalen innerhalb EINES Quellnetzes mit gemeinsamen Ecken (ein Körperteil «koerper» bzw. ein IFC-Produkt aus mehreren Schalen), zählt der überlappte Bereich weiter als «aussen». | selten; Leitung ganz im überlappten Bereich wird nicht als «enthalten» erkannt (Oberflächentreffer bleiben). | Teile über gemeinsame Kanten statt Ecken bilden und je Schale zählen. | mittel | tief |
| 6 | `Kollision/Schmalphase.cs` 201–214 | Weiche Prüfung Rechteckkanal gegen Netz: 12 Quaderdreiecke × alle nahen Dreiecke, je Paar sechs Kante–Dreieck-Abstände. | Nur mit gesetzten Abständen (heute aus, Entscheid 19.10.2026); bei fein vernetzten Bauteilen spürbar. | Abstand über `NaechstesPaar` der beiden Bäume wie bei Netz–Netz. | klein | tief |
| 7 | `Kollision/Geometrie3.cs` 372–373, 411; `Schmalphase.cs` 277, 297 | Kleine Speicherlast in inneren Schleifen: zwei Lambda-Abschlüsse je `ZylinderDreieck`, `new[]` je Dreieck/Stück. | messbar erst bei sehr vielen Öffnungsprüfungen. | Halbraum als Parameter (Punkt, Richtung, Länge) statt Lambda; Schleifen ohne Feld. | klein | tief |
| 8 | `Kollision/Kollisionspruefung.cs` 75–77 (Kennung, Fang 50 mm) | Verschiebt sich eine Kollision um mehr als 50 mm (Leitung umgelegt, kollidiert weiter mit demselben Bauteil), entsteht eine neue KOL; die alte wird «behoben» bzw. bleibt als «akzeptiert» stehen – Status und Kommentar gehen nicht mit. | Akzeptierte Kollisionen müssen nach einer Verschiebung neu beurteilt werden (kann gewollt sein), verwaiste «akzeptiert»-Einträge bleiben. | Siehe Frage 3. | klein | tief |

Hinweis für den Hauptlauf: Beim Commit meldete Git «failed to delete …/.git/worktrees/wt_b1 bzw. wt_b2: Permission denied» (Aufräumen fremder
Arbeitskopien, ohne Folgen für diesen Zweig).

## 5. Offene Fragen an Christian

1. **Leitungsenden**: Soll eine Leitung, die stirnseitig vor einer Wand endet (Abstand kleiner als der Rohrradius), als Kollision gelten?
   Heute ja (Kapsel mit Halbkugel am Ende). Vorschlag: nein – ebene Enden.
2. **Ringleitungen**: Welche Länge gilt bei der Dimensionierung in einer Masche – kürzester Weg, längster Weg, oder werden Maschen bewusst
   nicht gerechnet (dann Befund statt Zahl)?
3. **Akzeptierte Kollision verschoben**: Soll «akzeptiert» mitwandern, wenn dieselben zwei Bauteile nach einer Verschiebung an anderer
   Stelle kollidieren, oder ist die neue Stelle neu zu beurteilen (heutiges Verhalten)?
4. **Doppelt platzierte Geräte** werden seit Korrektur 2 als harte Kollision gemeldet (Tiefe = halbe kleinste Abmessung). Reicht das, oder
   soll es dafür einen eigenen, klareren Befund geben («zweimal am selben Ort»)?
