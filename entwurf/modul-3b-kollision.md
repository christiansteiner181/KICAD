# Modul 3b – Kollisionsprüfung und automatische Aussparungen: Konzept

Stand 17.10.2026 (Hauptlauf). Anlass, Christian 17.10.2026: «Der Hauptfall wird sein, dass wir ein Architekten-IFC
importieren und auf dieser Basis arbeiten. Revit behandelt dies wie ein PDF. Wir können darin keine Aussparungen
automatisieren, noch Räume vernünftig gebrauchen, noch eine korrekte Kollisionsprüfung durchführen.» Räume und echte
Bauteile kommen mit Modul 1b; Kollision und Aussparungen brauchen zusätzlich die Leitungen (Modul 3) – darum ein
eigenes Modul direkt danach, vor «Planung produktiv» (M3).

## Grundlage aus Modul 1b

- Architekten-IFC wird zu echten Elementen: Wand, Decke, Dach, Stütze, Unterzug, Tür, Fenster, Raum.
- Die **exakte IFC-Geometrie jedes Produkts** liegt in der Netzdatei der Unterlage (auch Treppen, Geländer usw., die
  keine Klasse haben). Für unveränderte übernommene Bauteile zählt sie; in KICAD geänderte oder gezeichnete Bauteile
  prüfen mit ihrem KICAD-Körper.
- Lage aller Modelle transparent abgeglichen (Ausrichtung, Modul 1b) – Voraussetzung für eine richtige Prüfung.

## Kollisionsprüfung

- **Paare:** HLKS ↔ Architektur/Tragwerk, HLKS ↔ HLKS (auch zwischen Gewerken und gegen Fremd-IFC anderer
  Fachplaner), wählbar je Prüflauf.
- **Arten:** hart (Durchdringung) und weich (Abstand unterschritten: Dämmung, Montage-/Wartungsraum, Mindestabstand
  zwischen Leitungen). Durchdringungen von Wänden/Decken, für die es eine Aussparung gibt, sind keine Kollision.
- **Abstände und Zuschläge** sind Projektvorgaben, die Christian festlegt (Bürostandard, je Gewerk/Bauteil) – keine
  Normwerte aus dem Gedächtnis. Dämmdicken kommen aus der Eigenschaft der Leitung.
- **Ergebnis:** Kollisionen als Befunde mit Lage, Beteiligten (Kennzeichen bzw. GlobalId der Fremddatei), Art,
  Mass der Überschneidung; Sprung in Grundriss, Schnitt und 3D; Status neu / bestätigt / akzeptiert / erledigt, der
  über neue Prüfläufe und neue Modellfassungen erhalten bleibt (stabile Kennung aus den Beteiligten und der Lage).
- **Austausch:** BCF (openBIM-Format für Befunde, Version zu entscheiden) an Architekt und Fachplaner; Rückmeldungen
  lesen. Die KI kann Kollisionen erklären und Lösungen vorschlagen (Leitung verschieben), ändert aber nur über Befehle
  mit Trockenlauf.
- **Leistung:** räumlicher Index (Hüllquader-Baum), damit Bernstrasse 42 (≈ 4000 HLKS-Elemente gegen ≈ 100 000
  Dreiecke Architektur) in Sekunden geprüft ist.

## Automatische Aussparungen

- **Erkennen:** wo eine Leitung oder ein Kanal eine Wand, Decke, Stütze oder einen Unterzug quert (Achse ∩ Bauteil,
  exakte Geometrie), entsteht ein Aussparungsvorschlag.
- **Form und Grösse:** rund oder rechteckig nach Leitung; Grösse = Aussenmass der Leitung + Dämmung + Spiel aus der
  Projektvorgabe; benachbarte Leitungen werden zu einer Sammelaussparung zusammengefasst (Abstand aus der Vorgabe).
  Kernbohrung oder Aussparung nach Grösse und Bauteil (Vorgabe).
- **Neue Klasse «Aussparung»** mit Bezug auf Bauteil und Leitungen, Nummer je Geschoss, Status vorgeschlagen →
  freigegeben → an Architekt/Ingenieur → im Architekturmodell angekommen → ausgeführt. IFC: IFC4
  IfcBuildingElementProxy PROVISIONFORVOID (IFC2x3: IfcBuildingElementProxy mit ObjectType) – vor dem Bau am Schema
  prüfen wie in Modul 1b.
- **Ausgabe:** Aussparungsplan je Geschoss (Grundriss mit Massen und Koten UK/OK, Schnitt bei Unterzügen), Liste,
  IFC für Architekt und Bauingenieur (ausdrücklicher Bezug, Exportbericht wie in Modul 1b festgelegt).
- **Abgleich:** Kommt eine neue Fassung des Architekten-IFC, prüft KICAD, ob die Aussparung dort angekommen ist
  (Öffnung bzw. Aussparungskörper an der Stelle) → Status «angekommen»; verschiebt sich die Leitung, wird die
  Aussparung «veraltet» und neu vorgeschlagen.

**Fertig, wenn …** Für Bernstrasse 42 (H+K-Leitungen gegen das Architekten-IFC) gibt es eine Kollisionsliste mit
Sprung in die Ansichten und Status über zwei Prüfläufe; die Aussparungen in Wänden, Decken und Unterzügen sind
automatisch vorgeschlagen, zusammengefasst, nummeriert, als Plan, Liste und IFC ausgegeben; eine neue
Architekten-Fassung setzt den Status «angekommen» bzw. «fehlt».

**Voraussetzungen:** Christian legt die Projektvorgaben fest (Spiel um Leitungen, Abstände für weiche Kollisionen,
Grenze Kernbohrung/Aussparung, Zusammenfassen) bis Beginn Modul 3b; Entscheid BCF-Version.

**Aufwand:** 8 Arbeitstage (Kollision 4, Aussparungen 4). Direkt nach Modul 3 (Leitungen), vor Modul 4.
