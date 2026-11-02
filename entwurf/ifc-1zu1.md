# IFC 1:1 – Bauteile aus IFC so zeigen, wie sie im IFC sind

Stand 02.11.2026 · Hauptlauf · Entwurf, Entscheide offen (unten)

## Auftrag

Christian, 02.11.2026: «auch das geometrische nachbauen von IFC familien muss passen» – «ein fenster muss 1:1 so aussehen wie im IFC.
mit allen details.» Dazu: «bei allen Familien eine detailgetreue Abbildung der Komponenten … in allen detaillierungsgraden», «Es muss
aussehen wie in den Datenblätter». Grundsatz vom selben Tag: KICAD so schnell wie möglich auf einen nutzbaren Stand wie Revit.

## Heutiger Stand (Bestandsaufnahme 02.11.2026, Stichproben im Code bestätigt)

| Stelle | Heute | Folge |
| --- | --- | --- |
| Import Tür/Fenster `IfcArchitektur.Oeffnung` (Import/IfcArchitektur.cs:528) | Element mit Punkt in der Wandachse, breite/hoehe/bruestung/seite/band | Rahmen, Flügel, Glas, Sprossen, Bank, Tiefe gehen verloren |
| Netzdatei `IfcNetz.cs` (Fassung 3) | je Produkt **ein** Netz, ohne Farben, ohne Teilkörper | Glas und Rahmen nicht unterscheidbar |
| Grundriss `ArchitekturGrafik.Tuer/Fenster` (Grafik/ArchitekturGrafik.cs:222–282) | festes Sinnbild (Aufschlag, Leibung, Rahmenquadrat, Glaslinie, Bank 50 mm) | sieht nie wie das IFC aus |
| Schnitt, Ansicht, 3D `OeffnungKoerper` (ArchitekturGrafik.cs:628) | ein Quader Breite × Tiefe × Höhe, Material tuer/glas | sieht nie wie das IFC aus |
| Original-Modus `ifcDarstellung original/beide` (Grafik/IfcDarstellung.cs, GrundlagenIfc.cs) | ganzes Netz einfarbig grau, dünne Schnittlinien, alles oder nichts je Unterlage | kein Ersatz für den Nachbau |
| `familie_netz_uebernehmen` (Familien/FamilienNetzUebernehmen.cs) | ein Körper «netz», ein Material, keine Detailgrade, Drehung nur ±45° | Familie aus IFC einfarbig und ohne Grob/Mittel |
| HLKS aus Hersteller-IFC (IfcHlks*.cs) | Klasse, Ports, Systeme; Körper aus Katalog/Familie | Herstellerform verloren |

## Vorschlag

1. **Netzdatei Fassung 4:** je Datensatz Teilnetze je IfcRepresentationItem mit Darstellungsstil (IfcStyledItem → IfcSurfaceStyle →
   Rendering/Shading: Farbe, Transparenz) und Material des Teils; Fassungen 1–3 weiter lesbar (ein Teilnetz, ohne Farbe).
2. **Element ↔ Netz:** übernommene Elemente tragen die GlobalId (`kennungen.ifc`, gibt es) und zeichnen ihr eigenes Netz aus der
   Netzdatei – Tür, Fenster, aber auch Wand, Decke, Stütze, Treppe, Geländer, sobald das IFC mehr zeigt als der Nachbau.
3. **Darstellung aus dem Netz** (statt Sinnbild/Quader), in allen Ansichten:
   - Grundriss: Schnitt durch die Teilnetze auf Schnitthöhe (Schnittlinien kräftig), Ansicht von oben unterhalb (Kanten dünn),
     Glas als eigene Linie; Türaufschlag bleibt als Sinnbild dazu (wie Revit).
   - Schnitt/Ansicht: Schnitt bzw. Projektion der Teilnetze.
   - 3D: Teilnetze mit ihren IFC-Farben und Transparenz.
4. **Familie aus IFC-Typ:** je IfcWindowType/IfcDoorType (bzw. Hersteller-Typ) einmal eine Familie mit den Teilnetzen als Körper
   (Material/Farbe je Teil), Lage und Drehung aus der IFC-Platzierung (nicht aus dem kleinsten Rechteck), Detailgrade gesetzt.
5. **Wandöffnung** aus dem Öffnungsnetz statt Rechteck, wo das IFC eine andere Leibung zeigt.
6. **Abnahme:** Vergleich KICAD ↔ IFC je Bauteil (Abweichung der Netze in mm, Bild nebeneinander) am Testprojekt und an Bernstrasse 42.

## Entscheide

- **Umfang (Christian 02.11.2026):** «dies gilt für alle importierten familien. nicht nur für fenster.» → alle aus IFC übernommenen
  Bauteile, Architektur und HLKS (Hersteller-IFC), zeichnen ihr IFC-Netz statt des Nachbaus.
- **Detailgrade:** offen (Frage gestellt, unbeantwortet). Bis zum Entscheid: in jedem Detailgrad das volle IFC mit Farben, nur die
  Strichstärken unterscheiden sich (wie eine Revit-Verknüpfung). Alternative: grob = Hülle, mittel = Hauptteile, fein = alles.
