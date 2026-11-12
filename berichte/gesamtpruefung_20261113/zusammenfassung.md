# Gesamtprüfung KICAD – Nacht 12./13.11.2026 (Zusammenfassung des Hauptlaufs)

Auftrag Christian 12.11.2026: «Gesamtprüfung von KICAD … Verbesserungspotential und Optimierungsmöglichkeiten des heutigen Standes. alles
sollte möglichst wie vorgesehen funktionieren. kleine fehler und optimierungen können direkt korrigiert werden», «Fabel soll auch die Logik
hinterfragen, besonders in bezug geometrie». Prüfagenten: Claude Fable 5.2, je Bereich eine eigene Arbeitskopie; der Hauptlauf prüft,
führt zusammen und testet gefiltert. Einzelberichte in diesem Ordner.

## Kurzfassung für Christian (Stand 13.11.2026 früh)

- **Geprüft:** 20 Bereiche in 6 Runden (Leitungen ×2, Grafik, Verdeckung, Oberfläche ×2, Import/Familien/Baustelle, Architektur/
  Aussparungen, Restliste, Rechenkerne, Schema/API, Familiengeometrie, Ausgabe, Fachwerkzeuge, Kollision/Prüfregeln, 3D/Schnitte/XR,
  KI-Schnittstelle, Unterlagen/2D-Zeichnung, Datenhaltung, Leistung, HKAP/Hülle, MSRL/Ansichten), dazu eine unabhängige Gegenprüfung
  aller Änderungen und eine Korrekturrunde daraus.
- **Korrigiert:** rund 100 Fehler direkt, je mit gefiltertem Test; Geometrie mit Handbeispielen nachgerechnet. Norm-, Tabellen- und
  Stoffwerte wurden nicht verändert.
- **Wichtigste echte Fehler:** Kanal-Druckverlust lief durchs Lüftungsgerät ins andere Netz; Referenzkegel konnten eine um 180° gedrehte
  Lage als eindeutig melden; Monteur sah bei «_Monteur_» im Projektnamen Bauleitungsseiten; zwei Sicherheitslücken der KI-Schnittstelle
  (fremde Netzpfade, Rückmeldung übernehmen); Gewerkefilter liess alle Leitungen in jedem Gewerkeplan stehen; Netzsuche brach still nach
  2000 Schritten ab; defekte Änderungspakete blockierten Öffnen und Abgleich; Klicks während laufender Abfrage gingen verloren;
  Kalibrieren mit 20 mm Versatz bei Landeskoordinaten; schräge Wandecken mit Zacken; Übergangsstück mit verkehrten Flächen im 3D.
- **Leistung:** Verbraucher eines Strangs (6000 Bauteile) 41 s → 17 ms, neue Kennzeichen bei Massenbefehlen 428 → 0.6 ms.
- **Tests:** volle Reihe auf dem Endstand: Kern 5297 grün; die 14 roten in Mcp/Web/Node geklärt – 3 durch die Nacht verursacht
  (korrigiert), 11 schon vorher rot (9 veraltete Tests nachgeführt, 2 echte Produktfehler korrigiert: Grundausstattung nach «Projekt
  anlegen» liess sich rückgängig machen; 2D-CAD-Werkzeuge ohne KI-Einordnung). Danach alle betroffenen Klassen grün.
- **Freigegeben:** `geprueft` mit Marke, Desktop und Baustellen-Testserver auf dem Endstand.
- **Deine Entscheide:** 52 Fragen unten; die mit Priorität «hoch» zuerst ansehen: 1, 20, 21, 22, 27, 29, 30, 37, 41, 43, 48.

## Runde 1 (Start 17:40)

| Bereich | Korrekturen | Bericht |
|---|---|---|
| Leitungen | 7 (Weiterzeichnen mit Gefälle bzw. Reduktion, Winkelfang geradeaus am Leitungsende, Teilen behält IFC-Kennung nur einmal, Silent-Wahl, Meldung Produktlinie, Teilen-Vorschau 3D-Länge) | leitungen.md |
| Grafik | 3 Commits (doppelte Achslinie an gedämmten Leitungen, verdeckte Dämmkanten in Rohrfarbe fein, Kreis im Loch der Deckfläche, Verbindungsindex in DaemmGrafik, IFC-Nachbau Trefferfläche, SVG wie PDF: Strichmuster/Stärke/gedrehte Ansicht) | grafik.md |
| Oberfläche | 5 (Kürzelliste, Kontextbefehle nur am passenden Bauteil, Rückmeldungen überschreiben sich nicht, unlesbare zugaenge.json mit Meldung, Fehlerschutz für /api) | oberflaeche.md |
| Import, Familien, Baustelle | 5 Commits (Referenzkegel: 180°-Verwechslung bei zwei Kegeln, Monteur sah bei «_Monteur_» im Projektnamen Bauleitungsseiten, familie_laden übernahm «freigegeben» aus Dateien auch für KI/Import, Raum in 3D bis Oberkante Geschoss, IFC-Deckkraft 0…1, keine javascript:-Links, Grundbestand meldet fehlende Bibliothek) | import_familien_baustelle.md |

Tests nach dem Zusammenführen (gefiltert): Kern 169 + 86 grün, Web 5 grün, Node 46 + 2 + 32 + 49 + 23 grün. Nach Import/Familien/Baustelle: Kern 56, Web 6 grün (Konflikt in den Baustellen-Zugängen mit der Fassung der Oberfläche gelöst).

## Runde 2 (Start 17:50)

| Bereich | Korrekturen | Bericht |
|---|---|---|
| Leitungen 2 | 4 (Parallele Leitung auf der Seite des angeklickten Stücks statt des ersten, eckige Lüftungs-Steigleitungen mit querDrehung, Dämmdicke < 0.5 mm nicht als «entfernt», Meldung Bogenwinkel) | leitungen2.md |
| Verdeckung | gebaut: Höhe an der Kreuzung statt Höhenspanne der ganzen Leitung (Gefälle, Etagen; neu Kreuzungshoehe.cs, auch für Dämmkanten), Index Element → Verbindungen im Modell (Leistung) | verdeckung.md |
| Oberfläche 2 | 4 (Klick während laufender Abfrage wird eingereiht statt verworfen, weiteres Esc beendet bei hängendem Server nach 5 s, Raster rundet nicht auf Länge 0, Mehrfachauswahl «verschieden» auch bei Auswahl/Häkchen, Kennzeichen bleibt bei «System weitergeben», Serien-PDF «Fertig», Lesbarkeit gelbe Leiste) | oberflaeche2.md |
| Architektur, Aussparungen | 4 (schräge Wandecken ohne Zacken, elemente_drehen dreht platzierte Familien mit, Türvorschau mit Drehpunkt auf der Wandfläche, Meldung statt Absturz beim Drehen/Spiegeln ohne Elemente) | architektur_aussparungen.md |

## Runde 3 (Start 18:05)

| Bereich | Korrekturen | Bericht |
|---|---|---|
| Restliste | 7 (Start an Anschluss übernimmt zuerst Dimension/Höhe/Medium, dann Pflichtfelder; Kanalumriss und Fliesspfeil an Kreuzungen; Lücke grob ab Strichrand; Gewinde 5/4" = 1 1/4"; IFC-Achsensystem x ∥ z; Meldung ohne Familienbibliothek; Hinweis Dämmdialog bei gemischten DN; Baustellen-Webseite startet trotz kaputter Adresse) | restliste.md |
| Rechenkerne | 7 (Kanal-Druckverlust: Weg endet am Gerät statt durchs andere Netz – ZD-1 2.08 statt 2.30 Pa, kein Abbruch ohne Volumenstrom, Rohre im Luftnetz mit Innendurchmesser, «Kunststoff» nur als eigenes Wort; Summen nennen Leitungen ohne Druckverlust; Heizlast und Heizkörper ohne Division durch null). Norm-, Tabellen- und Stoffwerte unberührt; Diff vom Hauptlauf gelesen | rechenkerne.md |

| Schema, Kern-API | 4 (Nachführung läuft bis keine Regel mehr greift statt fest 5 Runden, mit Hinweis an der Grenze; Wiederholen überschreibt keine spätere fremde Änderung; Kennzeichen mit Punkt, Punkt mit einer Koordinate, leeres Kennzeichen; Schema: «Element einfügen» im Bündel trifft den richtigen Strang) | schema_api.md |

| Familiengeometrie | 4 (Übergang-Körper nach aussen orientiert – vorher negatives Volumen/verkehrte Normalen in 3D/XR; Achse des schrägen Abzweigs endet im Achsschnittpunkt – einlinige Darstellung und Dämmung liefen quer; sin/cos bei Vielfachen von 90° genau; Umriss übernommener Netze vollständig). Rechtssystem, Drehsinn, Einbaulängen mit Handbeispielen bestätigt | familiengeometrie.md |


## Runde 4 (Start 18:15)

| Bereich | Korrekturen | Bericht |
|---|---|---|
| Ausgabe | 2 (IFC-Export: senkrechte Rechteckkanäle mit querDrehung wie 3D und Kollision; Materialliste und Leitungslängen zählen eine Leitung nur in ihrem eigenen System – vorher KW 5 m statt 2 m) | ausgabe.md |
| Fachwerkzeuge | 3 (sehr kurze Vorwand brach Grundriss/3D/Stückliste ab; Spiegeln stellt die Rückseite der Vorwand um; Kanalrahmen: Stoss- und Endrahmen überlappen nicht; Montageelement-Vorschau in Elementbreite) | fachwerkzeuge.md |
| 3D, Schnitte, XR | 4 (Kante am Absatz eines Prismas – L-förmige Decke – fehlte in Schnitt/Ansicht; «alles zeigen» passt im schmalen Fenster ein; Schnittbereich ziehen baut nicht mehr alle Körper neu; Farbe #rrggbbaa im 3D wie im XR-Export). Koordinatenwechsel Modell → three.js/glTF nachgerechnet | dreid_schnitte_xr.md |
| Kollision, Prüfregeln | 3 (Netz.Folgen hatte eine stille Grenze von 2000 Schritten – Stranglisten, Sanitär-Dimensionierung, Lüftungsnetz, Kanal-Druckverlust; doppelt platzierte bzw. bündig überlappende Körper sind ein Schnitt statt «Berührung»; Familienkörper aus überlappenden Grundkörpern richtig innen/aussen). Leistung gemessen: 6000 Kapseln 21 ms, Pruefungen.Alle 6000 Rohre 24 ms; Netz.Verbraucher quadratisch (3000 Bauteile 2.8 s) | kollision_pruefungen.md |
| KI-Schnittstelle | 2 Sicherheitsfehler (rueckmeldung_lesen nahm jeden Dateipfad der KI, auch fremde Netzpfade; rueckmeldung_uebernehmen liess eine KI über HTTP durch) und Abgleich vor 8 MCP-Fachabfragen (KI bekam sonst den alten Stand) | ki_schnittstelle.md |
| Unterlagen, 2D-Zeichnung | 3 (Kalibrieren: Verschiebung zu den gerundet gespeicherten Werten – bei LV95 vorher 20 mm Versatz; DWG ausrichten aus den wirklichen Winkeln – 12.4° statt 12.0°, 43 mm an der Ecke; verdrehtes Ansichtsfenster im Layout lag ausserhalb) | unterlagen_zeichnung.md |
| Datenhaltung, Abgleich | 5 (Paket mit unbrauchbarem Inhalt wurde halb angewandt und blockierte Öffnen/Abgleich; OneDrive-Konfliktkopie eines Pakets wurde doppelt angewandt; Ablage fällt beim Öffnen weg → Meldung statt still Teilstand; atomares Schreiben wiederholt; kein Modellkopieren je Takt während ein Standabbild geschrieben wird) | datenhaltung.md |
| HKAP, Hülle, Lagen | 4 (λ-Quelle in der Herkunft von R nur einmal; Lagemanager liest «12cm» als 120 mm, ein Minus lässt keine Lage wegfallen; Nordwinkel der Raumgrenzen aus derselben Projektinformation wie HKAP; Lagen der Optionsleiste folgen dem Modell). HKAP-Drei-Wege-Abgleich ohne Fehlerbefund | hkap_huelle.md |
| Leistung | 3 Optimierungen mit Vergleichstest alt = neu: Netz.Verbraucher 6000 Bauteile 41 s → 17 ms; NaechstesKennzeichen 2000 neue Rohre 428 → 0.6 ms; Sanitär-Dimensionierung 2880 Leitungen 987 → 631 ms. Je Befehl tiefe Kopie + alle Prüfungen: 5769 Elemente 338 ms (nur gemessen) | leistung.md |
| MSRL, Ansichten | 10 (Gewerkefilter gilt auch für Leitungen, Formteile, Armaturen – 18 Klassen ohne Gewerk erschienen in jedem Gewerkeplan, z. B. Kaltwasser im Lüftungsplan; 3D filtert gleich; Montageabschnitt nach Gewerk enthält Leitungen; Schnitt/Ansicht duplizieren behält die Schnittlinie; Vorlage ohne Gewerkefilter; Ansichtsvorlage nicht als Ziel; Datenpunkt-Anlage über Nachbarsysteme; Zeitreihe kürzt nicht still; Ansichtsleiste 1:50). GA-Schnittstelle nur lesend, kein Befund | msrl_ansichten.md |

Browser (Hauptlauf 18:13): Rohr am freien Leitungsende mit leerer Höhe begonnen – Höhe und Grösse des Anschlusses übernommen, Zug angelegt.

## Gegenprüfung (hlks-pruefer, 18:50)

Unabhängige Prüfung aller Änderungen 3aed5cb..91deac6: **beim Zusammenführen nichts verloren oder doppelt**. In den Korrekturen
selbst 10 Befunde (4 mittel, 6 tief, keiner hoch): schräge Wand mit Ende auf der Wandfläche bekommt einen neuen Zacken (B1); Klick-Reihe
führt einen ungeduldigen zweiten Klick bei Einzelklick-Werkzeugen aus – zwei Bauteile am selben Ort (B2); eingereihte Längeneingabe
nimmt den alten Fang (B3); baustelle_export/ifc_export ohne KI-Pfadprüfung (B4); Link-Filter der Baustellen-Webseite umgehbar (B5);
Paketkopie vor dem Original doppelt (B6); Vorwand spiegeln ohne gesetzte Rückseite (B7); Lagen nehmen jede Ziffernfolge (B8); pendelnde
Nachführung bis zur Grenze (B9); Kleinigkeiten (B10). Dazu Verhaltensänderungen: Dämmdialog wählt ohne bisherige Art die erste
Katalogart vor und speichert sie. → Korrekturrunde.

**Korrekturrunde (Runde 6, 19:10):** alle elf Punkte korrigiert (gegenpruefung_korrekturen.md): B1 Eckverlängerung mit Abstand des
Wandendes zur Achse (nur schräge Anschlüsse; Handbeispiel e = 182.84 mm), B2 Klick-Reihe nur für Mehrpunkt-Werkzeuge, B3 eingereihte
Längeneingabe mit neuem Fang, B4 KI-Pfadprüfung für baustelle_export/ifc_export, B5 Links nur http/https/file, B6 Paketkopie vor dem
Original, B7 Vorwand spiegeln mit Vorgabe-Rückseite, B8 Lagen je Wert als Ganzes (Unlesbares gemeldet; Werte nur mit Leerzeichen
getrennt gelten jetzt als unlesbar), B9 Pendeln der Nachführung erkannt, B10 Kleinigkeiten, Dämmdialog «– keine Angabe –».
Gefiltert: Kern 230, Web 3, Node 54 grün.

## Volle Testreihe auf dem Endstand (stand_pruefen 032efb7, 19:13–20:26)

Kern 5297 Tests grün (8 Zeitüberschreitungen im ersten Lauf, in der Wiederholung grün). Rot: Mcp 4 von 82, Web 7 von 789, Node 3 von
1447 – alle 14 geklärt und grün (rote_tests.md): 3 durch die Nacht (Fehlerschutz machte 400 zu 500 – korrigiert; Kürzeltest an die
neue Reihenfolge), 11 schon vorher rot (9 veraltete Tests nachgeführt; Grundausstattung nach «Projekt anlegen» war rückgängig machbar –
korrigiert; 19 Werkzeuge der 2D-Zeichnung ohne KI-Einordnung – vorsorglich für die KI-Palette verboten).

## Fragen an Christian (gesammelt)

1. **Gefälle 2 % gegen Toleranz 1°** (Leitungen): 2 % = 1.146° – jede Verbindung einer waagrechten Achse mit einem Stück mit 2 % wird
   abgelehnt (Abwasser an waagrechtem Anschluss, Weiterzeichnen mit Bogen, 2 % → senkrecht ohne 88.5°-Bogen). Toleranz für
   Gefälleleitungen anheben oder Gefälle erst nach dem Anschluss beginnen lassen?
2. **Fliessrichtung umkehren mit Gefälle**: heute bleibt der neue Anfang auf seiner Höhe, die Leitung sinkt als Ganzes (1000 → 940 wird
   940 → 880). Sollen Hoch- und Tiefpunkt tauschen (Höhen bleiben)?
3. **Zwei gebundene Enden mit Gefälle**: Zug wird abgelehnt, wenn die Höhen nicht auf 1 mm aufgehen. Darf KICAD das Gefälle anpassen?
4. **Baustellen-Zugang ohne angelegte Zugänge** ist im Büronetz für alle offen (Webseiten sehen, Rückmeldungen senden). Sperren, sobald
   der Server im Netz läuft, oder bewusst offen für den Test?
5. **Steigleitungen und Verdeckung**: entscheidet die mittlere Höhe oder das obere Ende?
6. Annahmen von heute bestätigen: Dämmkante/Achsen 0.10 mm, Abwasser-Kontur 0.20 mm, Lücke grob 0.5 mm, Kreis grob min. 0.5 mm,
   Dämmung Deckkraft 0.15.
8. **«Nachzeichnen? Nein» beim IFC-Import** (hoch): der Kern behandelt das wie «Einlesen für Familie aus IFC» – keine Prüfung der Lage, kein Abbruch bei Abweichung, die erste Datei setzt keinen Projektbezug. Widerspricht «kein Modell wird stillschweigend verschoben». Vorschlag: zwei Fälle trennen. Freigabe?
9. **Raum in 3D**: der Schnitt reicht jetzt bis Oberkante Geschoss (vorher lichte Höhe) – so gewollt?
10. **Landeskoordinaten in mm-Dateien**: MapUnit wird nicht gelesen (Faktor 1000 möglich, bricht dann ab statt still zu verschieben); braucht Quelle IFC4 und eine echte Prüfdatei.
11. **Parallele «oben/unten» an Zügen mit Steigstück**: heute nur in z versetzt – senkrechtes Stück deckungsgleich, 45°-Stück nur 0.71 · Abstand. Gehrung in 3D oder solche Züge ablehnen?
12. **Nachführen beim Verschieben**: «gedreht» wird gegen die bisherige Richtung gemessen, nicht gegen die Anschlussachse – viele kleine Schritte (je < 1°) summieren sich zu einer Schieflage (hängt mit Frage 1 zusammen).
13. **Formteile und Steigleitungsringe als Deckfläche** bei Kreuzungen (heute verdecken nur waagrechte Leitungen)?
14. **Shift + Anschluss**: Shift zieht einen Anschluss bis 10 px auf die Flucht und übernimmt trotzdem die Anschlussdaten. Anschluss immer in echter Lage, Fluchtpunkt mit eigenem Namen?
15. **Esc im Textfeld des Textwerkzeugs** übernimmt den Text statt abzubrechen – abbrechen?
16. **Esc bei hängendem Server**: zweites Esc beendet das Werkzeug nach 5 s offener Anfrage – passt 5 s auch über VPN?
17. **elemente_spiegeln an Familien**: nur die Richtung spiegeln oder gespiegelte Ausführung (links/rechts)?
18. **Aussparungsvorschlag am Formteil im Bauteil**: endet die Leitung im Bauteil an einem Bogen, wird über die ganze Bauteildicke
    ausgespart (gemeint war das T-Stück im Deckenbereich). Nur T-Stücke so behandeln?
19. **raum_erkennen** zieht freistehende Wände im Raum nicht ab (der IFC-Weg kann es) – angleichen?
20. **Massenstrom im Heizungsrohrnetz** (hoch): kommt aus der Normleistung (75/65/20) mit der Spreizung der Gruppe statt aus dem Bedarf –
    Heizkörper 1000 W Norm, Bedarf 400 W bei 55/45 ergibt 86 statt 34 kg/h, Reibungsdruckverlust grob fünfmal zu gross. heizkoerper_auslegung
    rechnet aus dem Bedarf. Entscheid 02.11.2026 stammt aus der Zeit vor Teil B – auf Bedarf umstellen?
21. **Hauptleitungen ausserhalb einer Heizgruppe** (Erzeuger bis Verteiler) haben keine Temperatur und damit keinen Druckverlust; in Maschen
    trägt jeder parallele Weg den vollen Strom. Mit welcher Temperatur dort rechnen?
22. **Schemasymbole und Fliessrichtung** (hoch): Symbole drehen nur nach «Leitung senkrecht/waagrecht» – Rückschlagventil, Pumpe,
    Schmutzfänger können gegen die Fliessrichtung zeigen. Nach Fliessrichtung drehen?
23. **Strangschema eng**: ab 9 Steigknoten in einer Zelle 2 mm Abstand bei 4 × 3 mm Symbolen, ohne Hinweis; Geräte an Strängen
    verschiedener Gruppen führen Linien quer durch fremde Gruppen. Zeile wachsen lassen oder Hinweis?
24. **Nachführung an der Grenze**: Befehl ablehnen statt nur Hinweis?
25. **Biegeradius der Ersatzbögen in Familien**: gilt heute der erste Parameter `biegeradius`, `radius`, `r` oder `R` – `r` kann der
    Rohrradius sein; ohne Untergrenze faltet sich der Bogen innen. Regel schärfen (nur `biegeradius`, R ≥ Profilradius)? Betrifft bestehende
    Familien der Bibliothek.
26. **Anschlussrichtung «nach aussen»** auch bei Apparaten und Armaturen in der Freigabeprüfung prüfen (heute nur Formteile)?
27. **Beschriftung im gedrehten Grundriss** (hoch): Freistellung rechnet in Modellachsen ohne Drehung der Ansicht – bei schrägen Leitungen
    bzw. Planwinkel ≠ 0 überdeckt der Text die eigene Leitung, Führungslinien liegen bis ~7 mm daneben. Platzierung in Ansichtsachsen
    umbauen (mittel)? Dazu Masszahl unter statt über der Linie, wenn sie wegen der Ansichtsdrehung umklappt.
28. **IFC-Export an Knicken**: eckige Kanäle als stumpfe Extrusionen (3D schliesst sie) – im IFC schliessen?
29. **Familie platzieren ohne Wand- und Höhenbezug** (hoch): z ist immer die Geschosskote, kein Ausrichten an Wand/Vorwand, keine Höhe
    über OKFB bzw. unter UKD. Option «an Wand» und Höhenfeld mit Bezug einbauen?
30. **Montageelemente ohne räumliche Anschlusslage** (hoch): Leitungen lassen sich nur logisch anschliessen. Wo und in welcher Richtung
    sollen die Anschlüsse liegen (aus dem Katalog, z. B. WC-Abwasser 230 mm über OKFF)?
31. **Vorwand verschieben/drehen**: Montageelemente bleiben liegen. Mitgehen lassen oder hinter dem Apparat bleiben?
    (Weitere 7 Fragen in fachwerkzeuge.md Abschnitt 5: Montagewinkel-Abstand, GIS-Segmentstoss, Vorwand über Eck u. a.)
32. **3D-Ansicht bei Landeskoordinaten**: Geometrie absolut als float32 in m – bei 2 600 000 m nur 0.25 m Auflösung (der XR-Export hat
    einen Bezugspunkt, die 3D-Ansicht nicht). Werden Projekte je in Landeskoordinaten geführt? Sonst nicht nötig.
33. **Klick im 3D**: eine Leitung gewinnt den Klick auch hinter einer undurchsichtigen Decke – so gewollt?
34. **Kollision an freien Leitungsenden**: die Kapsel hat ein halbrundes Ende – ein Rohr d 110, das 40 mm vor der Wand endet, gilt als
    Schnitt mit 15 mm Tiefe. Ebene Enden rechnen?
35. **Länge bis hier in Ringleitungen**: folgt dem Weg mit den wenigsten Bauteilen, nicht dem kürzesten bzw. längsten – die massgebende
    Länge der Sanitär-Dimensionierung kann in Maschen zu kurz sein. Welche Länge soll gelten?
36. **Doppelt platzierte Geräte** als eigener Befund melden?
37. **KI-Verbote im Kern** (hoch): der Kern sperrt für «ki» nur wenige Befehle (Familienfreigabe, Rückmeldung übernehmen, Baustelle
    freigegeben, Aussparungsnummern). Ein MCP-Server mit `--art ki` ohne `--werkzeuge` gibt der KI alle 206 Werkzeuge (u. a. hkap_*,
    Kollisions-/Aussparungsstatus, Revisionen, Plansatz, Bibliothek, IFC-Export). Sperre zentral im Kern oder Standardliste bei `--art ki`?
38. **Pfadgrenze für KI über HTTP**: gilt heute nach der Art des Projekts, nicht des Aufrufs – umstellen (Sitzungen, die heute Dateien
    von ausserhalb übernehmen, würden abgelehnt)?
39. **unterlage_einfuegen im Trockenlauf** kopiert die Datei schon – darf das so bleiben?
40. **Text spiegeln an waagrechter Achse** (2D-Zeichnung): steht danach auf dem Kopf, der Kommentar sagt «bleibt lesbar» – lesbar
    halten? Dazu PSLTSCALE: Strichmuster im Layout-PDF bei 1:100 hundertfach zu fein – eine echte DWG mit Layout zur Prüfung zustellen?
41. **Konflikte zwischen Rechnern** (hoch, Architektur): Konflikte werden am Versionszähler je Gerät erkannt – nach zwei Konfliktrunden am
    selben Element zeigen zwei Rechner verschiedene Stände, ohne Meldung (im Test nachgewiesen). Liegt `e_KICAD` nur auf dem NAS (dann
    selten, Reservierungen) oder auch in OneDrive-Ordnern (dann dringend)? Umbau: Version aus dem gewinnenden Paket ableiten.
42. **Reservierungen**: nach Absturz/VPN-Abbruch bis 2 h gesperrt – Knopf «Reservierung übernehmen» oder kürzere Dauer? Alte Pakete in
    Monatsordner verschieben (Ordner aenderungen wächst unbegrenzt)? Projekte eines neueren Programmstands ablehnen oder nur lesend öffnen?
43. **U-Wert unabhängig von der Angrenzung** (hoch, Normentscheid): die Gruppe «erdreich» bzw. «unbeheizt» wird nie angewendet – Wände
    gegen Erdreich/unbeheizt und Decken als Boden rechnen mit Rsi/Rse nach Art des Aufbaus, das geht so in die Heizlast. Welche
    Rsi/Rse je Angrenzung gelten (mit Quelle)?
44. **HKAP-Antwort** kann bei fehlender Id dem Datensatz an derselben Stelle zugeordnet werden; die HKAP-Adresse ist frei wählbar. Absichern?
45. **Lagemanager**: «−12cm» gilt jetzt als 120 mm unter UK roh Decke – oder nur Zahlen in mm?
46. **Leistung je Befehl** (Architektur): jeder Befehl kopiert das Modell und rechnet alle Prüfungen – bei ~6000 Elementen 0.34 s je
    Befehl. Prüfungen nur für Geändertes und Nachbarn umbauen?
47. **Gewerkefilter jetzt auch für Leitungen**: Pläne mit Gewerkefilter zeigen fremde Leitungen nicht mehr (vorher erschienen alle
    Rohre/Formteile in jedem Gewerkeplan). So gewollt?
48. **Ansicht löschen mit Beschriftungen** (hoch): abgelehnt, solange Bemassungen/Texte/Koten daran hängen – mitlöschen mit Hinweis?
49. **Ansichtsvorlagen** wirken nur beim Anlegen (Kopie, 7 Einstellungen); spätere Änderungen erreichen die Ansichten nicht – verknüpfen
    wie Revit?
50. **MSRL in der Oberfläche**: Datenpunkte vorschlagen und Datenpunktliste nur über KI – eigene Bedienung (Liste «Datenpunkte»)?
51. **2D-CAD in der KI-Palette**: die 19 Werkzeuge `zeichnung_*` sind vorsorglich verboten – lesende freigeben?
52. **Rückgängig nach «Projekt anlegen»**: die Grundausstattung (Hauptanlagen, Mustersysteme, Familien) ist jetzt nicht mehr
    rückgängig machbar – so recht?
7. **λ-Werte der Dämmarten**: welche Produkte verwendet ihr (dann λ mit Quelle eintragen)?

**Prüfung Hauptlauf im Browser (18:00):** Rohr mit drei raschen Klicks und Esc wird vollständig angelegt (Klick-Reihe), Weiterzeichnen mit Bogen am Leitungsende kürzt die bestehende Leitung. Nebenbefund: Start an einem Anschluss verlangt zuerst Produktlinie und Höhe, obwohl der Anschluss beides vorgibt (pflichtPruefen vor dem Anschluss) – kleine Korrektur für Runde 3.

Browser (Hauptlauf 18:28): 3D Gesamt im Beispielprojekt nach Runde 4 dargestellt, ohne Skriptfehler.

Browser (Hauptlauf 19:12): Dämmung hinzufügen an ungedämmter Leitung zeigt «– keine Angabe –».
