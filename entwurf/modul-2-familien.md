# Modul 2 – Familieneditor und Familie aus Datenblatt: Konzept

Stand 17.10.2026 (Hauptlauf). Grundlage für die Spezifikation von Modul 2, die vor dem Bau ausgearbeitet wird.

## Umfang

1. **Familieneditor** (Entscheid 17.10.2026): Familie = Parameter (Typ/Exemplar) + Formeln + Geometrie aus Primitiven
   (Quader, Zylinder, Extrusion einer Kontur, Rotation, Sweep entlang Pfad) + Anschlüsse (Lage, Richtung, Medium,
   DN, Fliessrichtung) + Kennwerte + 2D-Sinnbild (SIA) und Schemasymbol, je Detailgrad grob/mittel/fein. Kein
   Constraint-Solver. Begriffe 1:1 wie Revit (Kategorie, Typen, Typ-/Exemplarparameter, Typkatalog).
2. **Familie aus Datenblatt** (Erweiterung Christian 17.10.2026): Die KI erstellt aus einem Herstellerdatenblatt
   bzw. Massbild eine fertige Familie und **modelliert sie ab Zeichnung**.
3. ~~Revit-Import über ein Revit-Add-in~~ – **entfällt** (Entscheid Christian 17.10.2026: «Familie aus Datenblatt
   ersetzt den Familienimport aus Revit»). Damit braucht Modul 2 kein lizenziertes Revit. Das Format bleibt Revit-nah,
   ein Rückweg nach Revit (Add-in, FamilyManager/ConnectorElement) kann später nachgerüstet werden.

## KI-Familien aus technischen Unterlagen

**Eingang – mehrere Dokumente, mehrere Dateitypen** (Erweiterung Christian 17.10.2026: «mit mehreren Dokumenten und
Dokumenttypen, PDF, STEP, DWG, DXF usw.»): Eine Familie entsteht aus einem **Unterlagensatz** – beliebig viele
Dateien gemischter Art, z. B. Datenblatt (PDF) + Massbild (DWG) + 3D-Modell (STEP) + Typentabelle (Excel). Die
Unterlagen werden unverändert mit der Familie abgelegt (Prüfsumme), im Projekt unter `unterlagen\familien\` bzw. in
der zentralen Familienbibliothek.

| Art | Formate | Was daraus genommen wird | Leser (Vorschlag, Lizenz vor Einsatz prüfen) |
| --- | --- | --- | --- |
| Datenblatt, Massbild, Typentabelle | PDF | Text, Tabellen, Masse; Seiten als Bild; Vektorlinien des Massbilds | PDF-Text-/Vektorleser in C#, Seitenbild |
| Scan, Foto | JPG, PNG, TIFF | Seite als Bild, Masse über Bildverständnis | Bild direkt an die KI |
| 2D-Zeichnung | DWG, DXF | Linien, Bögen, Blöcke; Masswerte exakt aus den Bemassungsobjekten | DXF-Leser; DWG über ODA File Converter → DXF (Modul 1b) |
| 3D-Herstellermodell | STEP (AP203/214/242), IGES | exakte Hüllform, Stutzen und Anschlussflächen | OpenCascade als eigener Prozess |
| Netzmodell | STL, OBJ, glTF | Hüllform (nur grob, ohne Masse) | einfacher Netzleser |
| Herstellermodell BIM | IFC (Typen mit Ports und Psets) | Geometrie, Anschlüsse, Kennwerte | IFC-Leser aus Modul 1b |
| Produktdatensatz | VDI 3805 (später) | Typenreihen, Kennwerte, Anschlüsse strukturiert | eigener Leser, Format vorher prüfen |
| Tabelle | XLSX, CSV, Revit-Typkatalog (.txt) | Typenreihe, Abmessungen, Leistungsdaten | Tabellenleser |

RFA bleibt ausgeschlossen (proprietär; Revit-Import entfällt, siehe oben). Unbekannte Formate: Meldung, Datei wird als
Referenz abgelegt.

**Zusammenführen:** Jede Datei wird zuerst in **Befunde** übersetzt (Wert, Einheit, Quelle = Datei + Seite/Layer/
Fläche + Stelle, Sicherheit). Daraus entsteht *ein* Familienentwurf:
- Die Unterlagen ergänzen sich: z. B. Form aus STEP, Typenreihe aus der PDF-Tabelle, Anschlusslage aus dem DWG.
- Vorrang je Wertart (Vorschlag): Geometrie STEP › DWG/DXF-Bemassung › PDF-Vektor › Bild; Kennwerte Datenblatt-
  Tabelle › Fliesstext; Typenreihe Tabelle (XLSX/CSV/PDF) › Text.
- **Widersprüche werden nie stillschweigend aufgelöst:** Weichen zwei Unterlagen über die Toleranz hinaus ab (z. B.
  Höhe 600 im Datenblatt, 605 im STEP), kommt der Wert mit beiden Quellen in eine **Konfliktliste zur Entscheidung
  durch Christian**.
- Abdeckungsliste: je Parameter, aus welcher Unterlage er stammt und was fehlt.
- Neue Fassung einer Unterlage nachreichen → erneut lesen, Änderungsliste, neue Familienversion nach Freigabe.

**Ablauf** (im Familieneditor: «Familie aus Unterlagen», mehrere Dateien auf einmal hineinziehen, oder die KI über MCP):
1. *Lesen:* je Datei mit ihrem Leser – Text, Tabellen (Typenreihe, Abmessungen je Typ, Leistungsdaten,
   Anschlussgrössen), Seiten als Bild, Zeichnungsgeometrie, 3D-Form.
2. *Zeichnung verstehen:* Ansichten des Massbilds (vorne, seitlich, oben) erkennen, Masse und Masslinien zuordnen,
   Anschlüsse mit Lage, Nennweite, Medium und Richtung bestimmen; bei STEP Stutzen und Anschlussflächen erkennen.
3. *Zusammenführen:* siehe oben; Konflikte gehen an Christian.
4. *Modellieren ab Zeichnung:* Körper aus den Ansichtskonturen (Extrusion der Kontur einer Ansicht, Schnitt mit der
   zweiten Ansicht; Rundteile als Zylinder/Rotation) bzw. aus der STEP-Form vereinfacht je Detailgrad (grob:
   Hüllquader, mittel: Grundkörper, fein: vereinfachte Hülle), parametrisch: Masse werden Parameter, Typen aus der
   Typentabelle (z. B. Heizkörper Typ 22, Höhe 600, Längen 400…3000). 2D-Sinnbild, Schemasymbol.
5. *Prüfen:* Der Kern zeichnet die neue Familie in denselben Ansichten wie jedes Massbild (PDF, DWG, Bild); die KI legt
   sie übereinander, vergleicht Masse und Anschlusslagen und korrigiert. Gegen STEP: Vergleich der Hüllformen.
   Abweichungsliste mit Toleranz, je Unterlage.
6. *Freigeben:* Christian prüft im Editor (Vorschau 2D/3D, Kontrollbilder, Liste der gelesenen Werte, Konfliktliste)
   und gibt frei; erst dann steht die Familie in der Bibliothek.

**Grundsätze:**
- Jeder Wert hat seine Herkunft: Unterlage, Seite, Position im Massbild bzw. Tabelle (Leitprinzip 4) – im Editor
  per «Warum?» sichtbar.
- Nichts erfinden: Kennwerte (Leistung, Druckverlust, LU/DU …) nur aus der Unterlage; fehlt etwas, bleibt das Feld
  leer mit Hinweis. Normwerte nie aus dem Gedächtnis.
- Freiformen (z. B. WC-Keramik) werden nach Detailgrad vereinfacht; die Anschlusslagen sind immer exakt.
- Die KI arbeitet über dieselbe Tür wie alle (Befehle mit Trockenlauf). Der Knopf im Editor startet Claude lokal
  über Claude Code (`claude -p` mit MCP, Christians Abo, kein API-Schlüssel); Alternative später: API-Schlüssel.

**KI-Werkzeuge (MCP, Vorschlag):** `unterlagen_lesen` {dateien[], seiten?} → Befunde je Datei (Text, Tabellen,
Seitenbilder, Zeichnungsgeometrie, 3D-Form) · `familie_zusammenfuehren` {befunde} → Entwurf, Abdeckungsliste,
Konfliktliste · `familie_entwurf` {definition} Trockenlauf → Vorschaubilder je Ansicht und Prüfbefunde ·
`familie_vergleichen` {familie, unterlage, seite|ansicht} → Überlagerung und Abweichungen · `familie_speichern`
(erst nach Freigabe in der Bibliothek).

**Fertig, wenn …** Ein Heizkörper ist im Editor gebaut, und die KI hat aus drei Unterlagensätzen (z. B. Heizkörper,
Pumpe, Waschtisch) je eine Familie mit Geometrie ab Massbild, Anschlüssen und Typen erstellt; mindestens ein Satz ist
gemischt (PDF-Datenblatt + STEP oder DWG/DXF + Typentabelle), und ein absichtlich widersprüchlicher Wert landet in der
Konfliktliste statt im Modell; Masse und Anschlusslagen stimmen im Kontrollbild mit jedem Massbild überein.

**Voraussetzungen:** drei Unterlagensätze als Referenz bis 14.12.2026 – je Gerät, was der Hersteller anbietet
(Datenblatt-PDF, Massbild DWG/DXF, STEP-Modell, Typentabelle); Familienformat (Teil 1 dieses Moduls); für DWG der
ODA File Converter (Modul 1b). Kein Revit nötig.

**Aufwand:** 18 Arbeitstage (15 + 3 für mehrere Dateitypen: STEP/IGES-Leser, DXF/DWG, Tabellen, Zusammenführen mit
Konfliktliste). Die Zeit des weggefallenen Revit-Imports geht in Editor und «Familie aus Datenblatt». Mit Modul 1d
(HKAP-Schnittstelle, 17.10.2026) davor liegt Modul 2 bei **21.12.2026–22.01.2027** mit Feiertagspause 24.12.–03.01.
Folgende Richtwerte (mit Ostern, Auffahrt und Pfingsten; seit 17.10.2026 mit Modul 3b Kollision und Aussparungen):
M3 02.04.2027, M4 16.04.2027, M5 25.05.2027.
