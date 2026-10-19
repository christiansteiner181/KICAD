# Nachbearbeitungsliste HLKS-CAD

Bewusst zurückgestellte Arbeiten (Entscheid Christian), die in keinem laufenden Modul gebaut werden. Jeder Eintrag ist
so beschrieben, dass er ohne den damaligen Chat aufgenommen werden kann. Vor dem Start: Einordnung (eigener Nachtrag
oder Teil eines Moduls) mit Christian klären, Spezifikation in `entwurf/` ergänzen, Zuteilung nach `CLAUDE.md`,
Protokoll in `zuteilung.md`. Erledigte Einträge mit Datum und Ort (Modul, Testbericht) als erledigt markieren.

| Nr. | Eingetragen | Thema | Herkunft | Stand |
| --- | --- | --- | --- | --- |
| N1 | 18.10.2026 | IFC über zwei Punktpaare ausrichten | Abnahme 1b, Befund Bernstrasse | zurückgestellt |
| N2 | 18.10.2026 | Eigenschaftenpalette und Projektbrowser verschiebbar | Wunsch Christian | zurückgestellt |

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