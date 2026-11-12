# Gesamtprüfung KICAD – Nacht 12./13.11.2026 (Zusammenfassung des Hauptlaufs)

Auftrag Christian 12.11.2026: «Gesamtprüfung von KICAD … Verbesserungspotential und Optimierungsmöglichkeiten des heutigen Standes. alles
sollte möglichst wie vorgesehen funktionieren. kleine fehler und optimierungen können direkt korrigiert werden», «Fabel soll auch die Logik
hinterfragen, besonders in bezug geometrie». Prüfagenten: Claude Fable 5.2, je Bereich eine eigene Arbeitskopie; der Hauptlauf prüft,
führt zusammen und testet gefiltert. Einzelberichte in diesem Ordner.

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

Browser (Hauptlauf 18:13): Rohr am freien Leitungsende mit leerer Höhe begonnen – Höhe und Grösse des Anschlusses übernommen, Zug angelegt.

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
7. **λ-Werte der Dämmarten**: welche Produkte verwendet ihr (dann λ mit Quelle eintragen)?

**Prüfung Hauptlauf im Browser (18:00):** Rohr mit drei raschen Klicks und Esc wird vollständig angelegt (Klick-Reihe), Weiterzeichnen mit Bogen am Leitungsende kürzt die bestehende Leitung. Nebenbefund: Start an einem Anschluss verlangt zuerst Produktlinie und Höhe, obwohl der Anschluss beides vorgibt (pflichtPruefen vor dem Anschluss) – kleine Korrektur für Runde 3.

Browser (Hauptlauf 18:28): 3D Gesamt im Beispielprojekt nach Runde 4 dargestellt, ohne Skriptfehler.
