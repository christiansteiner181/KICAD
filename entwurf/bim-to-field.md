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

**Entscheide Christian 02.11.2026 zu den offenen Fragen (zweites Fragefenster):**
1. **Montageabschnitte frei wählbar:** «Bsp. einen Strang, Nur Heizkörper, nur eine Trasse usw.» → ein Abschnitt ist eine gespeicherte Auswahl
   nach beliebigen Kriterien (Strang, System, Klasse, Geschoss, Raum) oder von Hand gewählten Elementen (Trasse). Ein Element kann in mehreren
   Abschnitten stehen (Strang 1 und «alle Heizkörper»).
2. **Arbeitsplan:** Termine und Kolonnen von Hand in KICAD **und** aus einem importierten Bauprogramm der Bauleitung.
3. **Materialauszug:** «gleich wie arbeitsplanung, auswählbar» → je Abschnitt bzw. jeder beliebigen Auswahl nach denselben Kriterien.
4. **Montagestatus:** wie vorgeschlagen – geplant · freigegeben zur Montage · montiert · geprüft (Druckprobe/Spülung) · abgenommen, je mit
   Datum und Person.
5. **Freigabe je Rolle:** Monteur nur freigegebene Stände; Projektleiter und Bauleiter auch Arbeitsstände (deutlich vermerkt).
6. **Architektur:** das Architekten-IFC ist immer im Export (Hintergrund in 3D und Grundriss), dazu Wände, Decken und Räume des eigenen Modells.

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
- **Montageabschnitt** (neue Klasse): gespeicherte Auswahl nach Kriterien (Strang, System, Klasse, Geschoss, Raum) bzw. von Hand gewählte
  Elemente, mehrfache Zugehörigkeit erlaubt; mit Termin, Kolonne, Reihenfolge (von Hand bzw. aus dem Bauprogramm). Grundlage für Materialauszug
  und Arbeitsplan.
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
  - **Materialauszug** je Abschnitt bzw. frei gewählter Auswahl (vorhanden: `materialliste`, `mengen`), als Tabelle und CSV.
  - **Arbeitsplan** je Abschnitt (Reihenfolge, Umfang, Status, Termin, Kolonne; von Hand bzw. aus dem importierten Bauprogramm).
- **Stand:** Kopf mit Projekt, Modellprüfsumme, Commit/Datum, Planstand – jede Seite zeigt, wovon sie stammt.
- **Stand und Rolle:** Monteur-Export nur aus freigegebenem Stand; für Projektleiter und Bauleiter auch Arbeitsstände mit Vermerk auf jeder Seite.
- **Architektur:** Architekten-IFC immer dabei (Entscheid 02.11.2026). Ein Export ist eine Ausgabe an Dritte und kommt ins Ausgabeprotokoll
  (Modul 7, Rang 4).
- **Rückkanal in Stufe 1:** offline erfasst (abhaken, Mangel mit Foto, Mass), als Datei exportiert (BCF 3.0 bzw. JSON) und in KICAD
  eingelesen (BCF-Import vorhanden, Modul 3b B6). Ohne Server geht nichts verloren und nichts wird automatisch übernommen.

### Baustand Stufe 1 (02.11.2026)

Gebaut: Abfrage `baustelle_export` (eine HTML-Datei, offline), Knopf «Baustelle (Webseite)» im Register Datei › Ausgabe, SVG-Ausgabe der Plangrafik
(`Plan/PlanSvg.cs`), 3D aus den GLB der Quest App mit Architekten-IFC, Elementinfo mit Höhe aus den Punkten, Materialauszug der Auswahl (auch
Armaturen und Apparate). Noch nicht: Montageabschnitte, Arbeitsplan, Rückkanal, Planblätter als PDF, Dokumente; Stufe 0 (Datenmodell).

**Stufe 0 gebaut (02.11.2026, Christian «Erst Datenmodell-Lücken»):** Eigenschaften jedes Bauteils im Netz (Katalog.BauteilEigenschaften:
`montagestatus` mit Datum/Person aus der Herkunft, `hersteller`, `artikelnummer`, `dokumente`, `wartung`); Klasse **Montageabschnitt** (MA, Kriterien
und von Hand gewählte Elemente, Termin, Kolonne, Reihenfolge); Material und Dämmung an Formteil und Armatur; Herkunftsart `baustelle`; Kern
`Baustelle/BaustelleDaten.cs` (Abschnitt, Artikel Element vor Familie mit Typparameter `artikelnummer`, Höhenkote Achse/UK/OK), Abfrage
`montageabschnitte`, Befehl `montagestatus_setzen`. Die Webseite zeigt Abschnitt- und Statusfilter, Reiter «Arbeitsplan», Artikel, UK/OK.
Offen: Wartung und Dokumente an der Familie (Kopf), Import des Bauprogramms, Rückkanal.

**Bestandsaufnahme Stufe 0 (Agent hlks-modul, 02.11.2026) – Lücken im Informationsmodell:**
1. Artikelnummer und Wartung fehlen ganz (Hersteller/Produkt nur als freier Text im Familienkopf; Typ = Name des Familientyps).
2. Hersteller und Typ am Element nur bei Apparaten; kein typisierter Zugriff Element → Hersteller.
3. Dokumente nur an der Familie (`definition.unterlagen`, Rolle z. B. datenblatt), nicht am Element; Rollen Montageanleitung und Wartung fehlen.
4. Montagestatus, Montageabschnitt, Termin, Kolonne und die Herkunft «baustelle» fehlen.
5. Keine Höhenkote je Element (Achse/UK/OK) im Kern – der Export rechnet sie vorerst aus den Punkten.
6. Material und Dämmung nur an Leitungen, nicht an Formteilen und Armaturen.
7. Materialliste (Modul 3) ohne Armaturen/Apparate und ohne freie Auswahl – der Export rechnet den Auszug selbst.

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

1. **Bauprogramm-Import:** in welchem Format kommt das Terminprogramm der Bauleitung (Excel, MS Project, PDF)? Ein Beispiel genügt.
2. **Lieferantenformat** für den Materialauszug später (Datanorm, IDS-Connect, Händler)? Vorerst Tabelle/CSV.
3. **Anbieter und Domain** für Stufe 2 (vor Stufe 2).
