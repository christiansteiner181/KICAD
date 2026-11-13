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

## Detailfragen aus den Einzelberichten (N1–N41, gesammelt 13.11.2026)

| Nr. | Thema (Bericht) | Entscheid |
|---|---|---|
| N1 | Dämmen bei Mehrfachauswahl (leitungen2 §5.4) | Je Leitung nach Regel und DN; nur eine Handeingabe gilt einheitlich |
| N2 | Steigzone gleiche Abstände (leitungen2 §5.3) | Abstand bei Rohren immer auf die Achse beziehen |
| N4 | Vorgabe-Silent-Linie (leitungen §5.4) | Abwasser default Silent, wahlweise PE – mehr Linien gibt es nicht |
| N3 | Eckige Steigkanäle in Reihe (leitungen2 §5.5) | Wahlfeld «b/h entlang Reihe», Standard b |
| N6 | Rechtwinkliger Wandstoss (gegenpruefung §4.1, B1) | Wand schliesst auf der zugewandten Fläche der anderen Wand ab (keine Verlängerung) – Abweichung von Spezifikation Modul 1a |
| N5 | Spitze Wandecken (architektur §4.3) | Echte Spitze (Gehrung) statt Fase – Umbau der Wandkontur |
| N7 | Halbhohe Wände / Brüstung (hkap_huelle §4.2) | Heute zu hoch um überzählige Wandfläche × U_W (Christian). Wand mit wirklicher Höhe zählen; wo eine Wand übermessen wird, Fenster darin nur mit ΔU = U_F − U_W |
| N8 | Bezugsmasse Raumgrenzen (architektur §4.4) | Nach SIA 384/2 bzw. SIA 384.201 – Ziffer bei Umsetzung nachlesen (im Normenordner liegt «Wegleitung 384.201.pdf»), bis dahin Innenmasse |
| N9 | Akzeptierte Kollision nach Verschieben (kollision §5.3) | Neu beurteilen (wie heute) |
| N10 | Kreuzung < 1 mm Höhenunterschied (verdeckung §4.3) | So lassen, Kollisionsprüfung meldet |
| N11 | Massgebende Höhe an Kreuzungen (grafik §5.2) | Achshöhe an der Kreuzungsstelle |
| N14 | Stoss GIS-Segmente (fachwerkzeuge §5.4) | Rücken an Rücken, 0 mm |
| N13 | Montagewinkel beim Element (fachwerkzeuge §5.3) | Beidseits des Elements je ein Winkel, Höchstabstand eingehalten |
| N15 | Vorwand über Eck (fachwerkzeuge §5.5) | Die zuerst gezeichnete läuft durch, die neue schliesst stumpf an |
| N17 | Vorwand löschen (fachwerkzeuge §5.1) | Montageelemente mitlöschen, Hinweis «n Elemente mitgelöscht» |
| N16 | Bestücken, Apparat in der Vorwand (fachwerkzeuge §5.7) | KICAD-Apparat vor die Vorwand schieben und bestücken, Hinweis nennt die verschobenen |
| N18 | 180°-Umkehrbogen (familiengeometrie §4.2) | Nicht vorgesehen; Familie mit 180° wird gemeldet |
| N19 | Anschluss zeigt nach innen (familiengeometrie §4.3) | Freigabe sperren |
| N20 | Fremde freigegebene Familie laden (import §4.5) | Status übernehmen |
| N21 | Referenzkegel, nur zwei Kegel (import §4.2) | Überdeckung mit dem bestehenden Modell entscheidet über die halbe Drehung; Toleranz 50 mm bleibt |
| N22 | Dämmung im 3D (grafik §5.1) | An den Grundriss angleichen: Rohrfarbe, ab «mittel» |
| N23 | Dämmpunkt grob / verdeckte Dämmkanten (grafik §5.4) | Rand in Planfarbe, verdeckte Kanten einheitlich «strichfein» (0.8/0.5 mm) |
| N24 | Heizkörper ΔTn (rechenkerne §5.3) | Beides logarithmisch (bei Normbedingung f = 1.000); Abweichung vom Büroblatt |
| N25 | Spitzendurchfluss unter Kurvenstart (rechenkerne §5.4) | Q_D = Q_T unter dem Kurvenstart, Zusammenstellung angleichen |
| N26 | Rohrrauheit (rechenkerne §5.5) | Einen Standardwert übernehmen (statt Namensraten bzw. Herstellerwert je Linie); Wert mit Quelle bei Umsetzung |
| N27 | ζ-Werte Einzelwiderstände (rechenkerne §5.6) | Aus den HFGT16-Unterlagen (OneDrive\HFGT16) übernehmen, Dokument und Seite als Quelle |
| N28 | Luftschichten / Schichten mit R (hkap_huelle §4.3) | Feld «R» je Schicht, mit Quelle |
| N29 | Betondecke erkennen (hkap_huelle §4.6) | Auch «Beton» im Aufbaunamen oder in einer Schicht zählt |
| N30 | Materialliste am Systemübergang (ausgabe §4.4) | Formteil nur im eigenen System |
| N31 | Abgesetzte Schnitte (dreid §4.3) | Echter abgesetzter Schnitt |
| N32 | Gerät zwischen zwei Gewerken (msrl §4.1) | Gewerk der Geräteklasse |
| N33 | Ansicht duplizieren (msrl §4.4) | Wahl beim Duplizieren: nur Ansicht oder mit Beschriftungen |
| N34 | Detailgrad/Massstab im 3D (msrl §4.6) | Im 3D ausblenden (wie heute) |
| N35 | Menüband nach dem Zeichnen (oberflaeche §4.1) | Im Gewerkeregister bleiben |
| N36 | Netzbefehle bei Wand/Raum/Text (oberflaeche §4.4) | Ausblenden |
| N37 | Fang nahe am letzten Punkt (oberflaeche2 §4.3) | Mindestens ein Rasterschritt |
| N38 | Ja/Nein als Text (schema_api §5.5) | Annehmen und umwandeln («true», «ja», «1») |
| N40 | Lagen im Lagemanager (gegenpruefung §4.2) | Je Lage ein eigenes Textfeld (statt Liste in einem Feld) |
| N39 | Hinweis «nicht kleiner als zufliessend» (leistung §4.2) | Eine der gleich grossen nennen (kleinstes Kennzeichen), Rechnung in einem Durchgang |
| N41 | Schwellen automatisches Ausrichten (unterlagen §5.3) | So lassen, an echtem Plan (Bernstrasse) justieren |
| N12 | Steigleitung neben waagrechter Leitung (grafik §5.3) | Oberes Ende zählt auch für die Darstellung (wie 5): waagrechte im Überlappungsbereich verdeckt |

## Umsetzung

- **13.11.2026 Paket Leitungen/Geometrie** (geprueft-20261113-1534, f1f327e): 1, 2, 3 (07fa027) · 5, 13, N12 (a67fb6f) · 12, 24 (4ccc7ce) ·
  14, N2, N3, N37 (8b55bcf) · 25, 34, 28 (6284f14) · N1, N22, N23 (459ad94). N10, N11 waren schon so.
  Offen aus diesem Paket: 11 (Parallele oben/unten mit Gehrung in 3D), N5 (echte Spitze an Wandecken), N6 (Wandstoss endet an der zugewandten Fläche).
- **13.11.2026 Nachtrag Geometrie:** 11 (Parallele oben/unten mit Gehrung in 3D), N5 (spitze Wandecken als Spitze), N6 (T-Stoss endet an der
  zugewandten Fläche, Achse auf Achse gezeichnet → gekürzt) umgesetzt.
- **13.11.2026 weitere:** 48 (Ansicht löschen mit Beschriftungen, 58805d9), 37 (Liste «nur Mensch» zentral im Kern, KiVerbote.cs), 38 (Pfadgrenze
  nach Art des Aufrufs, Projekt.AkteurArtJetzt), 51 (zeichnung_* für die KI freigegeben) umgesetzt.
