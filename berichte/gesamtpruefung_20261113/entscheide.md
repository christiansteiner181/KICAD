# Entscheide Christian zu den Fragen der Gesamtprüfung (13.11.2026)

Nummern wie in `zusammenfassung.md`. Umsetzung offen, wo nicht anders vermerkt.

| Nr. | Thema | Entscheid |
|---|---|---|
| 1 | Gefälle 2 % gegen Toleranz 1° | Toleranz für Gefälleleitungen: atan(Gefälle) + 1° (2 % → 2.15°); Druckleitungen bleiben bei 1° |
| 20 | Massenstrom Heizungsrohrnetz | Aus dem Bedarf (Heizlast-Anteil / (c · Spreizung der Gruppe)); ohne Heizlast Rückfall auf Normleistung mit Hinweis |
| 21 | Hauptleitungen Erzeuger–Verteiler | Temperatur der höchsten Gruppe, «mit einstellbarer Hysterese» (Christian) – Hysterese = einstellbarer Zuschlag auf die höchste Vorlauftemperatur (Rückfrage bei Umsetzung, falls anders gemeint) |
| 22 | Schemasymbole | Nach Fliessrichtung drehen; ohne bekannte Richtung wie heute mit Hinweis |
| 27 | Beschriftung im gedrehten Grundriss | Umbauen: Platzierung und Freistellung in Ansichtsachsen; Masszahl immer über der Masslinie |
| 29 | Familie platzieren | Alle drei: Option «an Wand» (Rückseite an Wand-/Vorwandfläche, Drehung parallel), Höhenfeld mit Bezug (OK Geschoss / OKFB / UK roh Decke), Vorgabe-Montagehöhe je Familie (überschreibbar) |
| 30 | Anschlüsse der Montageelemente | Montageelemente als Familien mit eigenen Anschlüssen führen (Bibliothek) |
| 31 | Vorwand verschieben/drehen | Montageelemente gehen mit |
| 37 | KI-Verbote | Zentral im Kern: Liste «nur Mensch», für Herkunft «ki» abgelehnt (Palette, MCP, HTTP gleich) |
| 41 | Konflikte zwischen Rechnern | Projektordner nur auf dem NAS (W:) → Umbau vormerken, nicht dringend |
| 43 | U-Wert nach Angrenzung | Wie heute (U je Aufbau) |
| 48 | Ansicht löschen mit Beschriftungen | Mitlöschen im selben Paket, mit Hinweis «n Beschriftungen mitgelöscht» |
| 2 | Fliessrichtung umkehren mit Gefälle | Hoch-/Tiefpunkt tauschen: Höhen bleiben, nur die Richtung kehrt; danach Hinweis «Gegengefälle» bzw. Gefälle neu setzen |
| 3 | Zwei gebundene Enden mit Gefälle | Gefälle aus den beiden Höhen rechnen; unter dem gewählten Gefälle Hinweis bzw. Ablehnung mit Vorschlag |
| 4 | Baustellen-Zugang ohne Zugänge offen | «das ist nur ein prototyp» – so lassen |
| 5 | Verdeckung bei Steigleitungen | Oberes Ende zählt |
| 6 | Annahmen 12.11. | Bestätigt: Dämmkante/Achsen 0.10 mm, Abwasser-Kontur 0.20 mm, Lücke grob 0.5 mm je Seite, Kreis grob min. 0.5 mm, Deckkraft Dämmung 0.15 – Markierung ANNAHME entfernen |
| 8 | IFC «Nachzeichnen? Nein» | Trennen: Nein prüft Lage/Ausrichtung wie Ja (Abbruch bei Abweichung, Projektbezug), zeichnet nur nicht nach; «für Familie» bleibt ohne Lage |
| 9 | Raum in 3D | Bis Oberkante Geschoss (so lassen) |
| 10 | MapUnit | Nach buildingSMART IFC4 lesen (Quelle im Code), Prüfung mit echter Datei sobald vorhanden |
| 13 | Deckfläche bei Kreuzungen | Formteile und Steigleitungen verdecken ebenfalls (mit Dämmung) |
| 14 | Shift am Anschluss | Anschluss gewinnt in echter Lage; Endpunkt auf der Flucht heisst «Flucht», ohne Anschlussdaten |
| 11 | Parallele «oben/unten» mit Steigstück | Gehrung in 3D: überall derselbe Abstand (nach Skizze) |
| 12 | Nachführen beim Verschieben | Abweichung gegen die Anschlussachse messen; über der Toleranz Versatz bzw. Bogen statt Schieflage |
| 15 | Esc im Textfeld | Übernehmen (wie heute, passt zu «Esc legt an») |
| 16 | Esc bei hängendem Server | 5 s lassen |
| 7 | λ-Werte Dämmarten | Produkte swisspor – λ aus Herstellerunterlagen recherchieren (mit Quelle) |
| 17 | Familien spiegeln | Gespiegelte Ausführung (links/rechts-Variante der Familie, Anschlussseite wechselt) |
| 18 | Aussparungsvorschlag am Bogen | Zurückgestellt: «das Positionieren von Aussparungen werden wir neu denken» |
| 19 | Raum erkennen, freistehende Wände | Abziehen wie beim IFC-Weg (Inseln) |
| 23 | Strangschema eng | Zurückgestellt: «wir denken Schemas und Prinzipschemas in Kombination mit MSRL neu» |
| 24 | Nachführung an der Grenze | Befehl ablehnen, Meldung mit Grund |
| 25 | Biegeradius Ersatzbögen | Nur Parameter `biegeradius`, R ≥ Rohrradius (sonst anheben mit Hinweis); Bibliotheksfamilien prüfen und nachführen |
| 26 | Anschlussrichtung «nach aussen» | Auch bei Apparaten und Armaturen in der Freigabeprüfung |

## Recherche zu Frage 7 (swisspor, 13.11.2026, Subagent, vor Katalogeintrag gegenprüfen)

swisspor führt für Rohre nur PIR-Schalen «swissporKISODUR» (Heizung, Kälte, Sanitär); für Lüftungskanäle, Steinwolle, Elastomer kein swisspor-Produkt gefunden.

- KISODUR PIR Schalen und KISODUR PIR Alu Schalen (glatt, Gitter, Grobkorn): λ = 0.027 W/(m·K) bei 10 °C, DL-s2,d0, VKF RF3; Dicken 20–80 mm; Alu: −40 bis +130 °C.
  Quellen: Produktdatenblätter (erstellt 21.01.2026) auf swisspor.com/api-media/8831 und /8827, Prospekt KISODUR Alu, Preisliste Haustechnik 2026 (nur Dicken, keine Preise übernehmen).
- Leistungserklärung (λ je Temperaturstufe, EN 14308) war nicht abrufbar – im Original beschaffen, bevor weitere Stufen eingetragen werden.
| 32 | Landeskoordinaten | Ja, oft LV95 → 3D-Ansicht mit Bezugspunkt wie der XR-Export (Rechnung bleibt in Landeskoordinaten) |
| 33 | Klick im 3D hinter Decke | Vorderstes Element zuerst, mit Tabulator zu den dahinterliegenden durchschalten |
| 34 | Kollision an freien Rohrenden | Ebene Enden rechnen (keine Halbkugel) |
| 28 | IFC-Export an Knicken | Eckige Kanäle mit Gehrung schliessen (wie die 3D-Ansicht) |
| 35 | Länge bis hier in Ringleitungen | Massgebend ist der Weg mit dem grössten Druckverlust |
| 36 | Doppelt platzierte Geräte | Eigener Befund «doppelt platziert» |
| 38 | Pfadgrenze KI über HTTP | Nach Aufruf: jeder KI-Aufruf bleibt im Projektordner, Menschen weiterhin frei |
| 39 | unterlage_einfuegen im Trockenlauf | Erst beim Ausführen kopieren; Trockenlauf prüft nur die Lesbarkeit |
| 40 | Text spiegeln (2D) | Lesbar halten (Position gespiegelt, Text aufrecht, wie MIRRTEXT=0); DWG mit Layout für PSLTSCALE reicht Christian nach |
| 42 | Reservierungen | Knopf «Reservierung übernehmen» (mit Name und Zeitpunkt), Dauer 2 h bleibt; alte Pakete in Monatsordner; neuerer Programmstand → nur lesend öffnen |
| 44 | HKAP-Antwort/-Adresse | Wie heute |
| 45 | Lagemanager Einheiten | Einheiten erlauben (mm, cm, m); ohne Einheit mm |
| 46 | Leistung je Befehl | Umbauen: Prüfungen nur für Geändertes und Nachbarn, Gesamtprüfung auf Knopfdruck |
| 47 | Gewerkefilter für Leitungen | Fremde Leitungen grau als Hintergrund zeigen (je Ansicht abschaltbar) |
| 49 | Ansichtsvorlagen | Verknüpfen wie Revit; einzelne Einstellung lösbar |
| 50 | MSRL in der Oberfläche | Zusammen mit dem Neudenken von Schemas/Prinzipschemas und MSRL (vgl. 23) |
| 51 | zeichnung_* in der KI-Palette | Alle freigeben (lesend und schreibend, mit Rückgängig) |
| 52 | Grundausstattung nicht rückgängig | So recht |
