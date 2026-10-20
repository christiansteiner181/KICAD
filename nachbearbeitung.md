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