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
7. **λ-Werte der Dämmarten**: welche Produkte verwendet ihr (dann λ mit Quelle eintragen)?

**Prüfung Hauptlauf im Browser (18:00):** Rohr mit drei raschen Klicks und Esc wird vollständig angelegt (Klick-Reihe), Weiterzeichnen mit Bogen am Leitungsende kürzt die bestehende Leitung. Nebenbefund: Start an einem Anschluss verlangt zuerst Produktlinie und Höhe, obwohl der Anschluss beides vorgibt (pflichtPruefen vor dem Anschluss) – kleine Korrektur für Runde 3.
