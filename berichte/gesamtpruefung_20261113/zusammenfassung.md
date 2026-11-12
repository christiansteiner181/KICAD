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
| Import, Familien, Baustelle | läuft | – |

Tests nach dem Zusammenführen (gefiltert): Kern 169 + 86 grün, Web 5 grün, Node 46 + 2 + 32 + 49 + 23 grün. Stand `geprueft-20261112-1748`.

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
7. **λ-Werte der Dämmarten**: welche Produkte verwendet ihr (dann λ mit Quelle eintragen)?
