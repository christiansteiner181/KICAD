# Nachbearbeitungsliste HLKS-CAD

Bewusst zurückgestellte Arbeiten (Entscheid Christian), die in keinem laufenden Modul gebaut werden. Jeder Eintrag ist
so beschrieben, dass er ohne den damaligen Chat aufgenommen werden kann. Vor dem Start: Einordnung (eigener Nachtrag
oder Teil eines Moduls) mit Christian klären, Spezifikation in `entwurf/` ergänzen, Zuteilung nach `CLAUDE.md`,
Protokoll in `zuteilung.md`. Erledigte Einträge mit Datum und Ort (Modul, Testbericht) als erledigt markieren.

| Nr. | Eingetragen | Thema | Herkunft | Stand |
| --- | --- | --- | --- | --- |
| N1 | 18.10.2026 | IFC über zwei Punktpaare ausrichten | Abnahme 1b, Befund Bernstrasse | zurückgestellt |
| N2 | 18.10.2026 | Eigenschaftenpalette und Projektbrowser verschiebbar | Wunsch Christian | **erledigt 19.10.2026** mit W1/W2 (`entwurf/fenster-revit.md`, geprüfter Stand 72b16dc) |
| N3 | 19.10.2026 | Fehlende Bedienorte aus dem Funktionsverzeichnis | Bedienungsanleitung, `berichte/anleitung/inventar-notizen.md` Abschnitt 3 | **erledigt 20.10.2026** (Merge N3, Bericht in `zuteilung.md`) |
| N4 | 20.10.2026 | PDF-Leser: Dezimalzahlen, Zeilenleser der Datenblatt-Tabellen | Grundausstattung R1, R2, R7 | **erledigt 20.10.2026** (Leser 2); Neubelegung der Familien offen |
| N5 | 20.10.2026 | KI-Palette: abgelehnte Werkzeugaufrufe als «ok» | Abnahmelauf B6 | **erledigt 20.10.2026** |

## N1 – IFC über zwei Punktpaare ausrichten

**Zurückgestellt** von Christian am 18.10.2026 («wir verschieben das auf eine Nachbearbeitungsliste»), nach der Abnahme
von Modul 1b.

**Anlass:** Befund der 1b-Abnahme (`berichte/modul-1b-testbericht.md`, «Wichtigster Befund»). Das H+K-IFC von
Bernstrasse 42 (Revit 2025, IFC2x3, `hlks_hk_2026-02-10.ifc`) hat denselben Gebäudeursprung wie das Archicad-IFC, sein
Inhalt liegt aber rund 30 m östlich und 20 m nördlich daneben. Weder `ausrichtung` = `projekt` noch `gebaeude` trifft
(Inhaltsvergleich 0 % / 0 %), und es gibt keinen Koordinationskörper. Heute geht es nur über den Parameter `bezug`
{x, y, z, drehung} mit Zahlen.

**Alternative, die das Werkzeug unnötig macht:** Der Fachplaner liefert gemeinsame Koordinaten oder einen
Koordinationskörper (Prüfliste 1b, Punkt 1).

**Umfang (Vorschlag):**
- **Kern** (`src/Hlks.Kern/Import/IfcAusrichtung.cs`, `IfcUebernahme.cs` – ändert nur der Hauptlauf, die Rechnung an
  hlks-kern): Aus zwei Punkten im bestehenden Modell (Projektkoordinaten, mm) und den zwei entsprechenden Punkten in der
  IFC-Datei (Weltkoordinaten der Datei) den Bezug {x, y, z, drehung} rechnen: Starrkörper in der Ebene, Drehung aus den
  Richtungen P1→P2, Verschiebung aus Punkt 1, Höhe aus Punkt 1 oder aus einer Kote. Restfehler aus Punkt 2
  (Abstandsdifferenz der Paare als Massstabsfehler, Lagefehler von Punkt 2 nach der Transformation, in mm), Warnung ab
  10 mm. Neue Ausrichtungsart `punktpaare` mit Lagebericht (Punkte, Restfehler, Inhaltsanteil danach), gespeichert in
  der Unterlage und für neue Fassungen wiederverwendet wie die anderen Wahlen.
- **MCP:** Punktpaare als Parameter von `ifc_pruefen` und `ifc_uebernehmen` (eine Tür, Trockenlauf zeigt das Ergebnis).
- **Oberfläche** (hlks-modul, Muster: Werkzeug «Kalibrieren» `src/Hlks.Web/wwwroot/js/werkzeuge/kalibrieren.js`): im
  IFC-Dialog bei Abweichung die Option «Über Punktpaare ausrichten»; Punkte im Grundriss bzw. 3D fangen (Wandecken der
  Architektur bzw. des IFC-Hintergrundnetzes), Vorschau, Restfehler anzeigen. Nichts wird ohne ausdrückliche
  Bestätigung übernommen.
- **Tests** (hlks-test): mit `tests/Hlks.Kern.Tests/IfcLageTestdatei.cs` (gedreht und verschoben) und dem echten Fall
  Bernstrasse (Gebäudeecken in beiden Modellen). Echte Dateien nur unter `%USERPROFILE%\HLKS_CAD_testdaten`, nie ins Repo.

**Fertig, wenn** das Bernstrasse-H+K-IFC über zwei Punktpaare auf dem Archicad-Modell liegt (Inhaltsanteil > 80 %,
Restfehler genannt), alle Tests grün sind und der Lagebericht die Punktpaare transparent zeigt.

**Aufwand (Schätzung):** 1–2 Arbeitstage.

## N2 – Eigenschaftenpalette und Projektbrowser verschiebbar

**Eingetragen** auf Wunsch von Christian am 18.10.2026 («bitte in die Nachbearbeitung die Verschiebbarkeit von
Eigenschafts- und Projektbrowser aufnehmen»), während des Baus von Modul 2.

**Heute:** Beide Paletten stehen fest links, die Eigenschaftenpalette oben, der Projektbrowser unten
(`src/Hlks.Web/OBERFLAECHE.md`, Punkt 1 «Bedienung wie Revit»).

**Ziel (wie in Revit):**
- Paletten an ihrer Titelleiste ziehen: links oder rechts andocken, übereinander oder als **Reiter** zusammengelegt,
  oder **frei schwebend** über der Zeichenfläche; Doppelklick auf die Titelleiste dockt an den letzten Platz zurück.
- Grösse über Trennlinien ziehen (Breite der Seitenleiste, Aufteilung der Höhe zwischen den Paletten).
- Ein- und Ausblenden über Register Ansicht › Benutzeroberfläche; **«Anordnung zurücksetzen»**.
- Die Anordnung gilt je Benutzer und Rechner (Browserspeicher bzw. Benutzereinstellung, **nicht im Projekt**) und
  übersteht ein Neuladen.
- Prüfen: Palette in ein eigenes Fenster lösen (zweiter Bildschirm), synchron über den Ereignisstrom.
- Gilt auch für spätere Paletten (z. B. Befund-Browser aus Modul 2, Systembrowser aus Modul 3).

**Umfang (Vorschlag):** reine Oberfläche (hlks-modul), kein Kern. Dichte und Design nach `OBERFLAECHE.md`; Pflichtenheft
um den Abschnitt «Paletten anordnen» ergänzen. Tests: Node (Zustand der Anordnung, Speichern und Laden), Browser (ziehen,
andocken links/rechts, als Reiter zusammenlegen, schweben, Grösse ändern, Neuladen behält die Anordnung, Zurücksetzen);
alle bisherigen Browser-Reihen müssen grün bleiben.

**Fertig, wenn** Eigenschaftenpalette und Projektbrowser links und rechts angedockt, als Reiter zusammengelegt und
schwebend benutzt werden können, ihre Grösse ziehbar ist, die Anordnung ein Neuladen übersteht und sich zurücksetzen
lässt.

**Aufwand (Schätzung):** 1–2 Arbeitstage.
## N3 – Fehlende Bedienorte (aus dem Funktionsverzeichnis)

**Eingetragen** vom Hauptlauf am 19.10.2026: Beim Funktionsverzeichnis für die Bedienungsanleitung fanden sich Funktionen,
die im Kern gebaut, aber in der Oberfläche nicht erreichbar sind (erichte/anleitung/inventar-notizen.md, Abschnitt 3;
erichte/anleitung/luecken.json). Gebaut in der Nacht 19./20.10.2026 auf Christians Auftrag «arbeite bis 08:00 durch».

1. **Einlage- und Grundleitungspläne anlegen:** Knöpfe «Einlageplan» und «Grundleitungsplan» im Register Ansicht, Gruppe
   Erstellen (bzw. Flyout «Grundriss ▾»), rufen den Kernbefehl einlageplaene_anlegen (Dialog: Geschoss bzw. Bauteil
   Decke/Bodenplatte/Wand, Name; Vorschlag nach Planschlüssel «Einlagen D.ü.2.UG»).
2. **Projektinformationen bearbeitbar:** Der Dialog «Projektinformationen» wird bearbeitbar (heute nur lesend): alle
   Felder der Klasse Projektinformation aus dem Katalog, darunter Rohrbezeichnung (NW/DN), Masseinheiten je Gewerk,
   Projektnummer, Objektzeilen, Phase; Änderung über die Kernbefehle (Rückgängig, Herkunft). Das Element Projektinformation
   erscheint im Projektbrowser (Gebäude › Projektinformationen) bzw. wird über Verwalten › Projektinformationen erreicht.
3. **Kollisionsliste und Aussparungsliste** im Dialog «Liste anlegen» (die Listenansicht kann sie schon zeichnen) und
   **CSV-Ausgabe** jeder Listenansicht (Knopf «CSV …» in der Listenansicht bzw. Kontextmenü), UTF-8 mit BOM, Semikolon.
4. **Tastenkürzel-Liste (KS)** nach Register: «Beschriften», «Einfügen», «Hülle», «Koordination», «Familie» in
   REGISTER_NAMEN aufnehmen, sodass kein Kürzel unter «Allgemein» landet, das einem Register gehört.

**Fertig, wenn** alle vier Orte in der Oberfläche erreichbar sind, Tests (Node, Web, Browser) grün sind und das
Funktionsverzeichnis die Lücken nicht mehr führt.

## N4 – PDF-Leser: Dezimalzahlen mit getrennten Zeichenläufen

Befund 20.10.2026 (Grundausstattung R7, Geberit-HDPE-Katalog Ausgabe 2020, Händlerablage Reece): Der Unterlagen-Leser liest
Dezimalzahlen dieses PDF als «4 .5» statt 4.5 (Ganzzahl und Nachkommastelle in getrennten Zeichenläufen). Die KI konnte
solche Werte nur ohne Quellenverweis eintragen (Bogen, T-Stück, Reduktion, Rohr di/s). Ähnlich bei R1/R2: Werte aus
Datenblatt-Zeilen wurden dem falschen Typ zugeordnet («Zeilenleser», z. B. z45 d15/d18/d22 als 15/18/20 statt 16/17/21).

1. Zeichenläufe innerhalb einer Zelle zusammenführen, wenn der Abstand kleiner als die Zeichenbreite ist (Punkt bzw. Komma
   zwischen Ziffern) – Test mit einem selbst erzeugten PDF, das Zahl und Nachkommastelle getrennt setzt.
2. Zeilenleser der Datenblatt-Tabellen (Geberit «Erstellt mit Geberit Produktdaten»): Zuordnung Zeile ↔ Artikel prüfen.

**Fertig, wenn** die Befunde der R7-Unterlage die Dezimalwerte als Zahl mit Zelle liefern und die Familien R1, R2, R7 mit
Quellenverweis neu belegt werden können.

**Erledigt: 20.10.2026** (Leserteil; PDF-Leser Version 2). Ursachen: (1) Die Schrift des HDPE-Katalogs bildet den Punkt auf « .» ab
(Leerraum plus Punkt in einer Glyphe) – PdfPig setzte «101 .4» und «Art . no .» zusammen, keine getrennten Läufe. Jetzt fällt der Leerraum
einer solchen Glyphe weg; zusätzlich werden echte getrennte Läufe («101» «.4», «4» «.» «3», «147» «,6») zusammengeführt, wenn jede Lücke
kleiner als eine Zeichenbreite ist (Gegenprobe: echte Spalten bleiben getrennt). (2) «Zeilenleser»: die Zeilen selbst waren richtig; die
Tabellenerkennung nahm den Fliesstext darüber («Verwendungszwecke | Eigenschaften», «Werkstoff | Stahl unlegiert …») in den Block, dessen
lange Zellen die Spalten verschmolzen («20603 15 18 mm» in einer Zelle, «1.7 cm 20 St.» in einer anderen, kein Kopf). Die KI musste die
ganze Zeile zitieren, und die Quellenprüfung nannte als «Datenblattwert» die nächstliegende Zahl der Zeile (15/18/20). Jetzt trennt eine
Fliesstextzeile über bzw. unter den vollen Zeilen den Block; die Tabelle hat 9 Spalten mit Kopf «Art.-Nr. | DN | d, ø | arc | L | Z | VE1 |
VE2 | VE3», z45 d15/d18/d22 = 1.6/1.7/2.1 cm. Tests: `tests/Hlks.Kern.Tests/UnterlagenPdfN4Tests.cs` (selbst erzeugte PDF, echte Unterlagen nur
lokal). **Offen:** Leserversion 2 liest zwischengespeicherte PDF-Befunde neu – Befund-Ids können sich verschieben (z. B. Bogen-Datenblatt ab
der Tabelle); Quellen der Familien R1, R2, R7 (und weiterer mit PDF-Quellen) mit familie_pruefen nachprüfen und neu belegen (Hauptlauf).
Kopf der HDPE-Rohrtabelle enthält weiter die Zeilen darüber («Technical data Product material Art. no.», «PE-HD DN»).

## N5 – KI-Palette: abgelehnte Werkzeugaufrufe als «ok» angezeigt

Befund Abnahmelauf B6 (20.10.2026): Lehnt der Kern `unterlagen_lesen` ab (Pfadprüfung bzw. «mit Internet nur Herstellerunterlagen»),
zeigt die Palette den Aufruf als «ok»; `ifc_pruefen` und `befunde` erscheinen richtig als «abgelehnt». Vermutlich liefert der
MCP-Server bei `unterlagen_lesen` den Fehler als normales Ergebnis (ohne Fehlerkennung). **Fertig, wenn** jeder abgelehnte Aufruf
in Palette und Auftragsprotokoll als abgelehnt erscheint (Test über alle Werkzeuge mit HlksFehler).

**Erledigt: 20.10.2026.** Ursache: Kein Werkzeug setzte isError; die Palette erkannte Ablehnungen nur an «fehler» oben im JSON.
`unterlagen_lesen` meldet Fehler je Unterlage (verschachtelt) und galt darum als «ok»; das Auftragsprotokoll vermerkte nur is_error. Lösung:
eine Regel im Kern (`Werkzeugergebnis.MeldetFehler`: «fehler» als nicht leerer Text bzw. Liste oder angenommen false), benutzt vom MCP-Filter
`Fehlerkennung` (isError für alle Werkzeuge), der Palette, dem Auftragsprotokoll («Werkzeug abgelehnt: name – Meldung») und der Kommandozeile;
`unterlagen_lesen` setzt «fehler»/«vorschlag» (und «gelesen») oben und zuerst, sobald eine Unterlage nicht gelesen wird. Nebenbefund behoben:
`synchronisieren` meldet «fehler»: [] und galt in der Palette als abgelehnt. Tests: `tests/Hlks.Mcp.Tests/FehlerkennungTests.cs` (alle 130
Werkzeuge über das Protokoll mit erzwungener Ablehnung), `tests/Hlks.Web.Tests/NachbearbeitungN5Tests.cs`, `WerkzeugergebnisTests.cs`.

## N6 – Türen aus Archicad-IFC 5,19 m hoch (ragen durch Decke und Dach)

Befund Christian 21.10.2026 (3D, Projekt «Test_praxis», IFC «440_Bernstrasse 42 3D Modell.ifc», IFC2X3, Archicad 27): «türen über
gebäude». Hauptlauf: 23 von 57 IfcDoor tragen OverallHeight 5.19 m, die Öffnung (IfcOpeningElement, Rechteckprofil 0.94 × 5.19) ebenso,
der Türkörper selbst (Body, MappedRepresentation) ist aber nur 2.15 m hoch (z 0 … 2.15, Breite ±0.47). KICAD übernimmt OverallHeight
(«hoehe» 5190) – das IFC ist hier widersprüchlich, nicht der Leser. **Fertig, wenn** die Türhöhe aus dem Körper kommt, sobald er von
OverallHeight deutlich abweicht (Herkunft nennt beides), ein Prüf- bzw. Importhinweis die Abweichung je Tür meldet, die Türen in 3D und
Grundriss richtig stehen und Türen ohne Körper weiter OverallHeight nehmen.

## N7 – Zwei Stahl-Unterzüge ragen 3–4 m aus dem Gebäude

Befund Christian 21.10.2026 (gleiches Projekt): «unterzüge neben gebäude». Hauptlauf: UZ-14 (1.OG) und UZ-62 (2.OG), beide «Unterzug-002»,
Material Stahl, Linie von ca. [2972, −43351] nach [−3964, −36955] bzw. [3266, −43622] nach [−3670, −37226]; die Wände enden bei y −39903.
Die übrigen 71 Träger liegen im Gebäude. Ursache offen (Achse/Extrusionsrichtung, Länge aus Profil, Platzierung). **Fertig, wenn** die
Ursache belegt ist (gegen die Geometrie im IFC gerechnet) und die Träger dort liegen, wo das IFC sie zeichnet – bzw. ein Befund sie meldet,
falls das IFC sie tatsächlich ausserhalb zeichnet.

## N8 – Schraffuren drehen mit der Wand

Befund Christian 21.10.2026 (Grundriss, schräge Wand mit Kreuzschraffur): «schraffuren sollten mit wand rotieren». Heute liegt das
Schraffurraster achsparallel zum Blatt. **Fertig, wenn** die Schraffur jeder Wand (und jedes Bauteils mit Achse) an der Wandachse
ausgerichtet ist – auf dem Bildschirm, im PDF und im Schnitt –, bei gebogenen Wänden je Abschnitt, und Flächen ohne Achse (Decken,
Räume) bleiben wie bisher.
