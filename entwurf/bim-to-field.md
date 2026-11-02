# BIM to field – Modell auf die Baustelle und an alle Beteiligten

Stand 02.11.2026 · Hauptlauf · Auftrag Christian 02.11.2026: «das thema "BIM to field" muss bestandteil werden. heisst unser modell
muss auf eine webseite exportiert werden können. wir stellen diese den installateuren zur verfügung. die online plattform werden wir für
unterschiedliche Nutzerprofile ausbauen. Bsp, Monteur, Projektleiter, Bauleiter, Bauherr, Facility»

## Entscheide Christian 02.11.2026 (Fragefenster)

- **Erster Schritt:** Export-Paket zuerst. KICAD erzeugt je Projekt ein Web-Paket, das ohne Server läuft. Plattform mit Anmeldung folgt.
- **Betrieb der Plattform:** eigene Domain, Cloud Schweiz (z. B. `bim.hkplanung.ch`), Anmeldung je Person, H+K verwaltet die Zugänge.
- **Rückkanal Monteur:** alles – ansehen, montiert abhaken, Mängel und Fragen mit Foto, Abweichung einmessen. Dazu: «auch monteurspezifische
  themen sollen behandelt werden können. Bsp Materialauszug, Arbeitsplanung usw.»
- **Reihenfolge im Bauplan:** «wir brauchen als erstes den geometrischen und informativen gesamtharnes. dann machen wir mit den
  Inteligenzen weiter.» → Zuerst das vollständige Gerüst aus Geometrie und Information (Modell, Elementdaten, Ausgaben bis auf die Baustelle),
  danach die rechnenden und erzeugenden Teile (Rechenkerne Modul 4 B, 5, 6 ab Teil A-Rest, Generator Modul 12). Modul 6 Teil A ruht beim Stand
  vom 31.10.2026 (geprueft 2003c0f).

## Ziel

Jede Person am Bau sieht das Modell im Browser – auf dem Tablet auf der Baustelle auch ohne Netz – mit genau den Inhalten ihrer Rolle, und
meldet zurück, was sie gebaut, gefunden oder gemessen hat. Das Modell in KICAD bleibt die einzige Wahrheit: Rückmeldungen kommen als Vorschlag
mit Herkunft «baustelle» und werden in KICAD vom Menschen übernommen (wie die Pakete der KI).

## Rollen

| Rolle | Braucht vor allem | Meldet zurück |
| --- | --- | --- |
| **Monteur** | 3D und Grundriss seines Abschnitts, Höhenkoten, Dimensionen, Material, Dämmung, Artikel; Schema; Materialauszug je Abschnitt; Arbeitsplan | montiert/geprüft/gespült; Mängel und Fragen mit Foto; Ist-Lage |
| **Bauleiter** | Fortschritt je Abschnitt und Gewerk, offene Mängel, Aussparungen, Termine | Abnahme von Abschnitten, Mängel |
| **Projektleiter** | Gesamtstand, Rückmeldungen zum Übernehmen, Fragen, Abweichungen, Mehrmengen | Antworten, Freigaben (in KICAD) |
| **Bauherr** | Übersicht, Fortschritt, Pläne zum Stand, Dokumente | Fragen |
| **Facility** | As-built-Modell, Anlagen und Geräte mit Herstellerunterlagen, Wartung, Datenpunkte (Modul 8/11) | Störungen, Wartung erledigt |

## Stufe 0 – Informationsmodell vervollständigen (Teil des «Gesamtharness»)

Was die Rollen brauchen, muss am Element im Modell stehen, nicht erst im Export. Vor dem Bau eine **Bestandsaufnahme** (Agent hlks-modul,
lesend): welche Angaben gibt es heute je Klasse (Familie/Artikel, Dimension, Material, Dämmung, Höhenkote, System/Strang, Geschoss/Raum), welche
fehlen. Absehbar neu:
- **Montageabschnitt** (Klasse oder Eigenschaft: Geschoss × Gewerk × Strang bzw. von Hand), Grundlage für Materialauszug und Arbeitsplan.
- **Montagestatus** je Element: geplant · freigegeben zur Montage · montiert · geprüft (Druckprobe/Spülung) · abgenommen; mit Datum und Person.
- **Dokumente** am Element bzw. an der Familie (Datenblatt, Montageanleitung, Wartung) – Verweis, nicht Kopie.
- **Wartung** (Facility): Intervall und Herstellerangabe an der Familie, nur mit Quelle.

## Stufe 1 – Export-Paket (Web, ohne Server)

Befehl bzw. Ausgabe `baustelle_export { umfang?, rolle? }` → Ordner bzw. ZIP mit `index.html`, lauffähig von jedem Webspace, vom USB-Stick und
offline auf dem Tablet (installierbare Web-App).

- **Inhalt:**
  - 3D je Geschoss als GLB (vorhanden: XR-Auslieferung `XrModellEndpunkte`), Betrachter mit three.js (liegt in `wwwroot/lib/three`).
  - Grundrisse, Schnitte, Schemas als Vektor (SVG aus der Plangrafik des Kerns – gleiche Grafik wie Bildschirm und PDF).
  - Planblätter als PDF (Modul 7 Teil A), Stand und Revision sichtbar.
  - Elementinfo beim Antippen: Kennzeichen, Klasse, Dimension, Material, Dämmung, Höhenkote, System/Strang, Artikel, Dokumente.
  - Suche (Kennzeichen, Raum, Strang), Filter nach Geschoss, Gewerk, System, Abschnitt.
  - **Materialauszug** je Abschnitt (vorhanden: `materialliste`, `mengen`), als Tabelle und CSV.
  - **Arbeitsplan** je Abschnitt (Reihenfolge, Umfang, Status; Termine erst mit Quelle, siehe offene Fragen).
- **Stand:** Kopf mit Projekt, Modellprüfsumme, Commit/Datum, Planstand – jede Seite zeigt, wovon sie stammt.
- **Datenschutz:** nur freigegebene Inhalte; keine Unterlagen Dritter (Architekten-IFC) ohne Freigabe; ein Export ist eine Ausgabe an Dritte
  und kommt ins Ausgabeprotokoll (Modul 7, Rang 4).
- **Rückkanal in Stufe 1:** offline erfasst (abhaken, Mangel mit Foto, Mass), als Datei exportiert (BCF 3.0 bzw. JSON) und in KICAD
  eingelesen (BCF-Import vorhanden, Modul 3b B6). Ohne Server geht nichts verloren und nichts wird automatisch übernommen.

## Stufe 2 – Plattform (eigene Domain, Cloud CH)

- Dasselbe Paket, ausgeliefert von einem Server unter eigener Domain; Anmeldung je Person, Rollen und Projektzugang verwaltet H+K.
- Rückmeldungen laufen direkt auf den Server; KICAD holt sie ab und zeigt sie zum Übernehmen (Projektleiter).
- Benachrichtigungen (neue Planstände, Antworten), Fotos auf dem Server, Verlauf je Element.
- Rollen nach der Tabelle oben; jede Rolle eigene Startseite.
- Anbieter, Domain, Kosten, Datenschutzvertrag: Entscheid Christian vor Stufe 2.

## Stufe 3 – As-built und Facility

- Abweichungen einmessen (Mass, später Scan/Brille Modul XR) → KICAD passt das Modell an, Element mit Herkunft «baustelle».
- Übergabe an Facility: As-built-Modell, Anlagenliste, Wartung, Datenpunkte; Verbindung zu Modul 8 (MSRL), 9 (GA), 11 (Monitoring).

## Bezüge zu Vorhandenem

- XR: GLB je Geschoss, Ebenen, ProblemMarker von der Brille (`modul-xr.md`) – gleiche Daten, anderes Gerät.
- Modul 7: Planblatt, Serien-PDF, IFC-Export, Ausgabeprotokoll.
- Modul 3b: BCF-Export/-Import, Aussparungen (für Bauleiter und Baumeister).
- KI-Palette 3c: Übernahme von Vorschlägen mit Herkunft – Muster für die Übernahme von Rückmeldungen.

## Reihenfolge (Vorschlag)

1. Stufe 0 Bestandsaufnahme Informationsmodell (Agent, lesend) → Liste der Lücken an Christian.
2. Stufe 1 Grundgerüst: Export mit 3D, Grundrissen, Elementinfo, Suche, Stand (Beispielprojekt «MFH Musterweg»).
3. Stufe 1 Monteur: Abschnitte, Materialauszug, Arbeitsplan, Rückkanal als Datei.
4. Stufe 2 Plattform nach Entscheid zu Anbieter und Domain.
5. Stufe 3 As-built und Facility.

## Offene Fragen an Christian

1. **Montageabschnitte:** automatisch (Geschoss × Gewerk × Strang) oder von Hand je Projekt? Vorschlag: Vorschlag automatisch, Mensch passt an.
2. **Arbeitsplanung:** woher kommen Termine und Kolonnen (HKAP, Bauprogramm des Bauleiters, von Hand)? Was gehört für den Monteur hinein?
3. **Materialauszug:** in welcher Form bestellt der Installateur (Artikelnummern des Lieferanten, Gliederung nach Abschnitt/Geschoss)?
4. **Montagestatus:** Stufen wie oben oder anders (z. B. Druckprobe und Spülung getrennt)?
5. **Was ist «freigegeben»** für den Export: nur Stände mit Planfreigabe (Revision) oder jeder Arbeitsstand?
6. **Architektur im Export:** Wände/Decken aus dem eigenen Modell zeigen, das Architekten-IFC nur mit Freigabe?
