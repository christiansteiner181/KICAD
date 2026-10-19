# Modul 2 – Familien und Familie aus Unterlagen: Spezifikation

Stand 18.10.2026 (Hauptlauf). Start auf Christians Zuruf vom 18.10.2026 («Ja, starte mit Modul 2 Familien aus
Datenblatt»; geplant 21.12.2026–22.01.2027, 18 Arbeitstage). Grundlage: Konzept `entwurf/modul-2-familien.md`
(Entscheide Christian 17.10.2026: Familieneditor, Familie aus Datenblatt mit mehreren Dateitypen, **kein Revit-Import**).

## Ziel

Heute ist jedes Gerät ein Kasten: Die Katalogklasse gibt Breite, Tiefe, Höhe (`darstellung`) und Anschlüsse ohne Lage
vor, Sinnbilder stehen fest im Code (`HlksSinnbilder`). Modul 2 bringt **Familien** wie in Revit: parametrische
Geometrie, Typen, Anschlüsse mit Lage und Richtung, Kennwerte mit Herkunft. Eine Familie entsteht im **Editor** oder
durch die **KI aus Herstellerunterlagen** (PDF, DWG/DXF, STEP, Tabellen, Bilder) – Widersprüche gehen an Christian.

**Fertig, wenn …** (aus dem Konzept, präzisiert)
1. Ein Heizkörper ist im Familieneditor gebaut (Parameter, Formeln, Typen, Körper, Anschlüsse) und im Beispielprojekt
   platziert: Grundriss, Schnitt, 3D und Anschlusslagen stimmen mit einer Handrechnung.
2. Die KI hat aus **drei echten Unterlagensätzen** je eine Familie mit Geometrie ab Massbild, Anschlüssen und Typen
   erstellt; mindestens ein Satz ist gemischt (PDF + STEP oder DWG/DXF + Typentabelle). Die Sätze hat Christian am
   18.10.2026 geliefert (lokal unter `%USERPROFILE%\HLKS_CAD_testdaten\familien\`, nie ins Repo):
   - **Belimo R2..-S..** (4 S.): Auf/Zu-Kugelhahn 2-Weg, Innengewinde Rp, 6 Typen DN 15–50; Typenübersicht (DN, Rp,
     Kvs, PN), schematisches Massbild mit Tabelle L, L1, H, X, Y, Gewicht → Klasse **Ventil**.
   - **FläktGroup Optivent Ultra ULSA/ULDA** (8 S.): runde VAV-Klappe ohne/mit Isolierung, 9 Grössen 100–630;
     Massbilder mit Tabellen ØD, ØDy, a, L, Gewicht, Regler 70 mm über dem Gehäuse → Klasse **Volumenstromregler**.
   - **Klimaoprema RVP-P** (29 S.): rechteckiger Volumenstromregler, 58 Grössen B × H 200×100 bis 1000×1000 (nachgezählt 18.10.2026; die Schattierung bedeutet Motorantrieb LM 5 Nm). **Datenbefund im PDF:** Die Trennlinie zwischen H 600 und H 700 liegt eine Zeile zu tief; dadurch steht B 700 in der Gruppe H 600 zweimal (Aef 0,3570 und 0,4165), nach Aef = 0,85·B·H gehört die zweite Zeile zu H 700 – ein natürlicher Widerspruch, den die KI melden muss,
     Gehäuselänge 350 mm, Aef, Vmin, Vmax je Grösse → Klasse **Volumenstromregler**.
   Alle drei sind reine PDF. Für den gemischten Satz fehlt noch eine zweite Dateiart (STEP oder DWG/DXF eines der drei
   Produkte) – offen bei Christian.
   Dazu (Christian 18.10.2026, «alles Lüftungsmaterialien»): **Schmidlin-Preisbücher** Kanäle (76 S.), Komponenten
   (74 S.), Rohre (64 S.) aus `W:\09_Preislisten HKAP\` (lokal kopiert unter `…\familien\preisbuecher\`): Kataloge mit
   Code, schematischem Massbild (Buchstaben d, l, s …) und Tabelle je Grösse. Sie dienen als Prüfstein für den PDF-Leser
   (grosse Kataloge, viele Tabellen) und als Unterlagen für Lüftungskomponenten (z. B. Regenhut, Aufzugsschacht-
   Entlüftung). Kanal- und Rohrformteile (Bögen, T-Stücke) werden Familien mit Modul 3. **Preise übernimmt KICAD nicht**
   (Preise bleiben in HKAP; Befunde mit Preisen bleiben in der lokalen Befund-Ablage des Projekts).
3. Ein **absichtlich widersprüchlicher Wert** landet in der Konfliktliste statt im Modell; Freigabe ist gesperrt,
   bis Christian entschieden hat.
4. Masse und Anschlusslagen stimmen im **Kontrollbild** mit jedem Massbild überein (Abweichungsliste innerhalb der
   Toleranz).
5. Alles über MCP bedienbar, mit Trockenlauf und rückgängig; jeder Wert mit Herkunft bis auf Datei, Seite und Stelle.

## Grundsätze

- **Nichts erfinden.** Abmessungen, Anschlüsse und Kennwerte (Leistung, Druckverlust, Förderhöhe, LU/DU …) nur aus einer
  Unterlage oder von Christian; fehlt etwas, bleibt das Feld leer mit Hinweis. Normwerte nie aus dem Gedächtnis.
- **Widersprüche nie still auflösen.** Weichen zwei Quellen über die Toleranz ab, entsteht ein Konflikt mit beiden
  Quellen; der Vorrang je Wertart bestimmt nur, welcher Wert vorgeschlagen wird.
- **Freigabe nur durch einen Menschen.** Die KI erstellt Entwürfe; `familie_freigeben` lehnt den Akteur «ki» ab.
- **Eine Tür.** Editor, KI und Skripte benutzen dieselben Befehle (Trockenlauf, rückgängig, Pakete).
- **Familie im Projekt ist Modelldaten** (wie Material und Aufbau in 1c): Element der Klasse `Familie`, synchronisiert
  über Pakete. Die Bibliothek ist nur Vorlage; `familie_laden` kopiert ins Projekt.
- Unterlagen bleiben unverändert (Prüfsumme) im Ordner `unterlagen\` des Projekts.

## Familienformat (Hauptlauf, Fundament)

Schema: `schema/hlks-familie.schema.json`. Eine Familie ist ein JSON-Objekt (im Projekt als Eigenschaft `definition` des
Elements `Familie`, in der Bibliothek als Datei `<name>.familie.json`).

**Koordinaten der Familie:** mm; Ursprung = Einfügepunkt; **x** nach rechts (Breite, von vorn gesehen), **y** nach
hinten (Tiefe, von der Vorderseite weg zur Wand), **z** nach oben. Die Vorderseite zeigt nach −y. Platziert wird mit
Einfügepunkt (Weltkoordinaten) und `drehung` um z (Grad, Gegenuhrzeigersinn).

| Teil | Inhalt |
| --- | --- |
| Kopf | `format` (1), `name`, `kategorie` (= KICAD-Klasse, z. B. Heizkoerper, Pumpe, Waschtisch), `hersteller`, `produkt`, `beschreibung`, `version`, `status` (entwurf, freigegeben) |
| `parameter` | je Parameter: `name`, `art` (typ, exemplar), `datentyp` (laenge, winkel, zahl, text, ja_nein, medium, dn), `einheit`, `gruppe` (abmessung, anschluss, kennwert, sonstiges), `formel` **oder** `wert` (Vorgabe), `ziel` (Eigenschaft der KICAD-Klasse, die der Wert setzt, z. B. `leistung`), `beschreibung` |
| `typen` | je Typ: `name`, `werte` {Parameter: Wert}, `quellen` {Parameter: [Befund-Id …]} – wie der Revit-Typkatalog |
| `koerper` | Liste der Grundkörper (unten), je mit `id`, `name`, `detailgrad` ([grob, mittel, fein], Vorgabe alle), `sichtbar` (Formel, Vorgabe wahr), `material` (Darstellungsmaterial des Kerns: geraet, apparat, armatur, rohr, sonst) |
| `anschluesse` | je Anschluss: `name`, `medium` (medien.json oder «*»), `fluss` (rein, raus, beide), `art` (rohr, kanal, elektro), `form` (rund, rechteck), `dn` bzw. `b`/`h` (Formel), `lage` [x, y, z] (Formeln), `richtung` [dx, dy, dz] (aus dem Gerät heraus, Einheitsvektor), `beschreibung` |
| `masse` | optional: Massbezüge {`name` (L, H, ØD …), `ansicht`, `von`/`bis` bzw. `parameter`, `richtung`, `quellen`} – wo ein Mass der Unterlage an der Familie gemessen wird (siehe «Zusammenführen») |
| `sinnbild` | optional: 2D-Grundriss-Linien in Familienkoordinaten (linie, polylinie, polygon, kreis, bogen, text; mit Stift/Linie/Füllung wie `Stil`, je Detailgrad). Ohne Sinnbild zeichnet der Grundriss den **Umriss der Körper von oben** |
| `schemasymbol` | optional, gleiches Format, für das Schema (Modul 3) |
| `unterlagen` | [{`unterlage` (Element-Id), `datei`, `pruefsumme`, `art`}] |
| `konflikte` | [{`id`, `parameter`, `typ`?, `werte` [{`wert`, `quellen`}], `toleranz`, `entscheid`? {`wert`, `akteur`, `zeit`, `begruendung`}}] |
| `abdeckung` | {Parameter: {`quellen` [Befund-Id], `fehlt` bool}} – vom Kern gerechnet |

**Grundkörper** (alle Zahlen als Zahl oder Formel-Text):
- `quader` {`von` [x, y, z], `bis` [x, y, z]} (achsparallel in Familienkoordinaten)
- `zylinder` {`von` [x, y, z], `nach` [x, y, z], `r`} · `kegel` {`von`, `nach`, `r1`, `r2`} · `kugel` {`mitte`, `r`}
- `extrusion` {`ebene` (xy, xz, yz), `kontur` [[u, v] | [u, v, wölbung] …], `loecher`?, `von`, `bis`} – Kontur in der
  Ebene, gezogen entlang der Normalen von `von` bis `bis` (Wölbung = DXF-Bulge für Bögen)
- `rotation` {`ebene`, `kontur`, `achse` {`punkt` [u, v], `richtung` (u, v)}, `winkel` (Vorgabe 360)}
- `sweep` {`profil` {`kreis` r | `rechteck` b, h}, `pfad` [[x, y, z] …], `biegeradius`?}

Keine Abzugskörper (Voids) in Modul 2 – Hohlformen über Konturen mit Löchern (Annahme, Prüfliste).

**Formeln** (wie Revit, damit vertraut): `+ - * / ^`, Klammern, Vergleiche `< > <= >= = <>`, Funktionen `if(b, a, c)`,
`and`, `or`, `not`, `min`, `max`, `abs`, `sqrt`, `round`, `roundup`, `rounddown`, `sin`, `cos`, `tan`, `asin`, `acos`,
`atan` (Grad), `pi()`. Parameternamen gross/klein genau, Buchstaben (mit Umlauten), Ziffern, `_`. Längen in mm,
Winkel in Grad, ja_nein als 1/0, Text nur mit `=`/`<>`. Zirkuläre Formeln, unbekannte Namen und Division durch 0 sind
Fehler mit Stelle und Vorschlag. Reihenfolge: Exemplarwerte › Typwerte › Formeln › Vorgabewerte. Wie in Revit: Ein
Parameter mit Formel ist nicht pro Typ oder Exemplar einstellbar, und **Formeln von Typparametern dürfen nur
Typparameter verwenden**. Testfamilie (fiktive Werte): `tests/daten/familien/test_heizkoerper.familie.json`.

## Datenmodell im Projekt (Hauptlauf)

| Klasse / Eigenschaft | Inhalt |
| --- | --- |
| **Familie** (neu, Schicht `familie`, ohne Geometrie, Kürzel FAM) | `kategorie`, `definition` (objekt, Format oben), `status` (entwurf, freigegeben), `version`, `bibliothek` (Herkunft in der Bibliothek: Datei und Prüfsumme), `freigabe` (objekt: wer, wann) |
| Klassen mit `"familie": true` (Armatur, Apparat, Heizkörper, Wärmeerzeuger, Speicher, Verteiler, Wassererwärmer, Lüftungsgerät, Luftdurchlässe, Hausanschluss, Kontrollschacht, Dachablauf – und alle abgeleiteten) | bekommen automatisch `familientyp` (text), `parameter` (objekt: Exemplarwerte), `drehung` (zahl, Grad) und den Bezug `familie` |
| **Unterlage** | `art` neu auch step, iges, stl, obj, xlsx, csv, typkatalog, ifc-typ; Bezug `familie` = Unterlage einer Familie (nicht in Plänen gezeichnet) |
| Herkunft | neue Arten `familie` (Wert kommt aus der Familie, Quelle = Familie und Typ) und `unterlage` (Wert aus einer Herstellerunterlage, Quelle = Befund-Id mit Datei, Seite, Stelle) |

Ein Element mit Familie:
- übernimmt die **Anschlüsse** der Familie (Name, Medium, Fluss, DN) statt der Klassenvorgabe;
- bekommt die Werte der Parameter mit `ziel` als Eigenschaften (Herkunft `familie`), nachgeführt bei Typwechsel und
  neuer Familienversion;
- wird aus der Familie gezeichnet (Grundriss, Schnitt, Ansicht, 3D); ohne Familie bleibt alles wie heute.

Anschlusslagen sind **abgeleitet** (Familie + Einfügepunkt + Drehung + Parameter), nicht gespeichert: Abfrage
`anschlusslagen` {element} → je Anschluss Lage, Richtung, DN; Grundlage für das Verbinden in Modul 3.

## Befehle und Abfragen

| Befehl / Abfrage | Zweck |
| --- | --- |
| `familie_entwurf` {definition, familie?} | prüft eine Definition (Formeln, Körper, Anschlüsse, Quellen gegen die Befunde, Konflikte, Abdeckung) und legt sie als Familie im Status entwurf an bzw. ändert sie; Trockenlauf = Vorschau ohne Änderung |
| `familie_aendern` {familie, ops} | gezielte Änderungen (Parameter, Typ, Körper, Anschluss hinzufügen/ändern/löschen) – der Weg des Editors |
| `familie_laden` {datei} | Familie aus der Bibliothek ins Projekt (Kopie, Status bleibt) |
| `familie_platzieren` {familie, typ, punkt, drehung?, parameter?, kennzeichen?} | Element der Kategorie mit Familie anlegen |
| `konflikt_entscheiden` {familie, konflikt, wert, begruendung} | Entscheid zu einem Konflikt (nur Mensch) |
| `familie_freigeben` {familie} | Status freigegeben; nur Mensch, nur ohne offene Konflikte und ohne Fehler |
| `familie_speichern` {familie} | freigegebene Familie in die Bibliothek schreiben |
| `unterlagen_lesen` {unterlagen[]} | Befunde je Datei (siehe unten), zwischengespeichert je Prüfsumme und Leserversion |
| `unterlage_bild` {unterlage, seite?, ausschnitt?} | Seite bzw. Ansicht als PNG (für die KI als Bild) |
| `familie_ansicht` {familie, typ, ansicht (vorne, hinten, links, rechts, oben, unten, 3d), detailgrad} | Plangrafik bzw. Körper der Familie allein (Editor, Kontrollbild) |
| `familie_vergleichen` {familie, typ, unterlage, seite, ansicht, passpunkte} | Kontrollbild (Unterlage mit überlagerter Familie, PNG) und Abweichungsliste |
| `familie_aus_unterlagen` {unterlagen[], kategorie?, hinweis?} | startet den KI-Lauf als Auftrag (Status abfragbar, abbrechbar) |
| Abfragen `familien`, `familie`, `anschlusslagen`, `befunde`, `auftraege` | lesen |

## Unterlagen lesen → Befunde

Jede Datei wird in **Befunde** übersetzt: {`id` = «<Prüfsumme 12>#<n>», `datei`, `art`, `wert`, `einheit`, `seite`
bzw. `layout`, `layer`, `lage` (Unterlagen-Koordinaten: PDF in pt mit y nach oben, DWG in Zeichnungseinheiten, Bild in
Pixeln), `box`, `sicherheit` (0–1), `leser` (Name und Version)}. Befunde liegen als Datei
`unterlagen\befunde\<prüfsumme>.json` (abgeleitet, jederzeit neu lesbar), nicht im Modell.

| Art | Formate | Leser | Befund-Arten |
| --- | --- | --- | --- |
| Datenblatt, Massbild, Tabelle | PDF | **PdfPig** (Apache-2.0) für Wörter mit Lage, Zeilen und Vektorpfade; Seitenbild über Docnet (vorhanden) | text, wort, zeile, tabelle, zelle, linie, bogen, kreis, mass (Zahl neben Masslinie, Sicherheit < 1) |
| 2D-Zeichnung | DWG, DXF | ACadSharp (vorhanden) | linie, bogen, kreis, polylinie, text, **mass** (Bemassungsobjekt: Messwert exakt, Definitionspunkte), block, je Layout |
| 3D-Modell | STEP, IGES | **occt-import-js** (LGPL-2.1, OpenCascade als WebAssembly) in einem Node-Prozess | form3d (Netz, Hüllquader), flaeche, **stutzen** (kreisförmige ebene Ränder: Mitte, Normale, Durchmesser), ansicht (Umriss vorne/seitlich/oben) |
| Netz | STL, OBJ | eigener Leser | form3d, ansicht |
| Herstellermodell | IFC (Typen) | xBIM (vorhanden) | form3d, port (Lage, Richtung, System), pset |
| Tabelle | XLSX, CSV, Revit-Typkatalog (.txt) | **ExcelDataReader** (MIT); CSV und Typkatalog eigen | tabelle, zelle, typ (Zeile des Typkatalogs) |
| Bild | PNG, JPG | – | bild (Grösse, dpi); Inhalt liest die KI aus dem Bild |

TIFF, RFA und unbekannte Formate: Meldung, Datei wird als Referenz abgelegt (Annahme). **Lizenzen geprüft
18.10.2026:** PdfPig 0.1.16 (NuGet-Id «PdfPig», Namensraum UglyToad.PdfPig; Apache-2.0, 22.08.2026; die Id «UglyToad.PdfPig» ist ein fremdes Paket), ExcelDataReader 3.9.0 (MIT, 16.06.2026), occt-import-js
0.0.23 (LGPL-2.1, 03.12.2024, 11.6 MB) – genau diese Fassungen festschreiben. occt-import-js wird unverändert als eigener
Node-Prozess benutzt (LGPL-konform); `node_modules` liegt ausserhalb von OneDrive unter
`%USERPROFILE%\HLKS_CAD_build\step\`, im Repo nur `werkzeuge/step/package.json` und `package-lock.json`.

## Zusammenführen, Konflikte, Abdeckung (Kern)

Die KI schlägt Werte vor und nennt je Wert ihre Befunde (`quellen`). Der Kern prüft beim `familie_entwurf`:
1. **Quelle stimmt:** Jeder Wert mit Quelle muss zum Befund passen (Zahl innerhalb der Toleranz, Einheit umgerechnet);
   sonst Fehler «Wert weicht von der genannten Quelle ab».
2. **Konflikt:** Nennen mehrere Befunde für denselben Wert Zahlen, die über die Toleranz auseinanderliegen, entsteht ein
   Konflikt mit allen Werten und Quellen. Der vorgeschlagene Wert (nach Vorrang) steht im Entwurf, aber die Familie ist
   bis zum Entscheid nicht freigebbar.
3. **Abdeckung:** je Parameter und Typ: Quelle(n) oder «fehlt».

Toleranzen (Annahme, Prüfliste): Längen max(1 mm; 0.5 %), Anschlusslage 2 mm, Durchmesser 1 mm, Kennwerte gleich auf die
Stellen der Unterlage. Vorrang (Konzept): Geometrie STEP › DWG/DXF-Bemassung › PDF-Vektor › Bild; Kennwerte Datenblatt-
Tabelle › Fliesstext; Typenreihe Tabelle › Text.

**Massbilder in Datenblättern sind schematisch** (Befund an den drei echten Sätzen, 18.10.2026): Eine Zeichnung gilt für
die ganze Typenreihe, die Masse stehen als Buchstaben (L, H, ØD …) in einer Tabelle je Typ, und die Zeichnung ist nicht
massstäblich. Die Prüfung gegen ein solches Massbild läuft deshalb **über die bemassten Werte**: Jeder Massbuchstabe
der Zeichnung ist einem Mass der Familie zugeordnet (Abstand zwischen zwei Kanten bzw. Punkten in einer Ansicht), und der
Kern misst dieses Mass an der Familie je Typ und vergleicht es mit der Tabellenzeile. Die Überlagerung ist bei
schematischen Massbildern nur Anschauung (je Typ an einem Mass eingepasst). Massstäbliche Zeichnungen (DWG/DXF, STEP)
werden zusätzlich geometrisch verglichen.

**Kontrollbild** (`familie_vergleichen`): Die Unterlagenseite wird mit Passpunkten (zwei Punktpaare oder ein DWG-Mass)
auf die Familienansicht gelegt; der Kern zeichnet die Familie rot darüber und rechnet:
- je Kante der Zeichnung (Befunde linie, bogen im Ausschnitt) den Abstand zur nächsten Kante der Familienansicht,
- je Mass-Befund, ob seine Definitionspunkte auf Kanten der Familie liegen,
- je Anschluss die Lage gegen den Stutzen- bzw. Anschluss-Befund.
Ergebnis: PNG und Abweichungsliste (Wert, Soll, Ist, Toleranz, bestanden).

**Massbezüge in der Familie:** Damit der Kern ein Mass wie «L» an der Familie messen kann, trägt die Familie optional
`masse` [{`name` («L»), `ansicht` (vorne, oben, links …), `von` und `bis` (Punkt in Familienkoordinaten, Formeln) bzw.
`parameter` (der Parameter, der das Mass ist), `quellen` (Befund der Tabellenspalte bzw. der Masslinie)}]. Die
Abweichungsliste vergleicht je Typ jedes Mass mit dem Tabellenwert des Typs.

## KI-Lauf «Familie aus Unterlagen»

`familie_aus_unterlagen` startet Claude Code auf diesem Rechner (Weg wie HKAP `ClaudeCode.cs`: Christians Abo, ohne
API-Schlüssel), mit dem MCP-Server «hlks» des offenen Projekts als einzigem Werkzeug (`--strict-mcp-config`), Akteur
«KI», ohne Dateizugriff ausserhalb. Auftrag aus der Vorlage `ki/familie_aus_unterlagen.md`:
lesen → Bilder der Seiten ansehen → Ansichten und Masse zuordnen → Definition mit Quellen → `familie_entwurf` im
Trockenlauf → `familie_vergleichen` je Massbild → korrigieren → Entwurf speichern → Bericht (gelesene Werte,
Konflikte, offene Felder, Kontrollbilder). Der Lauf ist ein **Auftrag** mit Status (läuft, fertig, Fehler,
abgebrochen), Protokoll und Zeitlimit (Vorgabe 20 min).

## Internet-Recherche (Wunsch Christian 18.10.2026)

«Der Familien-Assistent sollte fehlende Informationen im Internet recherchieren können.» Umsetzung so, dass jeder Wert
nachvollziehbar bleibt:
- **Finden** mit der Websuche von Claude Code (`WebSearch`; Suchtext nur Hersteller, Produkt, Typ – nie Projektdaten).
  **Kein `WebFetch`** (Entscheid Hauptlauf 18.10.2026, Befund B5: der eingebaute Seitenabruf umgeht den Schutz gegen
  interne Adressen und wäre ein Abflussweg); Seiten und Dateien kommen nur über `unterlage_aus_url`, Webseiten werden über
  ihre Links (Befunde art link) weiterverfolgt. Mit Internet sieht die KI nur Familien- und Unterlagen-Werkzeuge, keine
  allgemeinen Projektabfragen; jede Suche und jeder Abruf steht im Auftragsprotokoll.
- **Belegen** nur über `unterlage_aus_url` {url, art?, familie?}: Der Kern lädt die Datei (PDF, DWG/DXF, STEP/IGES, STL/OBJ,
  IFC, XLSX/CSV/TXT, PNG/JPG), die Webseite (HTML) oder ein **ZIP-Archiv** (Christian 18.10.2026: «auch PDF, DWG usw.
  aus dem Internet holen»; entpackt werden nur erlaubte Arten, jede als eigene Unterlage mit `original` «URL#Pfad»;
  höchstens 200 Einträge, 200 MB entpackt, keine Pfade ausserhalb, ZIP höchstens eine Ebene verschachtelt) herunter und
  legt sie als Unterlage mit `zweck: familie` ab
  (`original` = URL, `abgerufen` = Zeit, Prüfsumme). Dann liest der gewohnte Leser sie in Befunde; Webseiten liest ein
  HTML-Leser (Text, Zeilen, Tabellen, Links). So hat jeder Wert aus dem Internet eine Befund-Quelle in einer abgelegten
  Datei.
- **Schutz:** nur http/https; **keine Adressen im eigenen Netz** (localhost, 127/8, 10/8, 172.16/12, 192.168/16, 169.254/16,
  ::1, fc00::/7, Rechnername ohne Punkt) – auch nach Weiterleitungen und DNS-Auflösung geprüft, damit z. B. HKAP (Port 5013)
  oder das NAS nie erreichbar sind; höchstens 50 MB je Datei, Zeitlimit 60 s, nur die genannten Dateiarten (nach Inhalt
  geprüft, nicht nur nach Endung). Keine Anmeldung, keine Formulare, keine Captchas, keine Cookies; was dahinter liegt,
  meldet die KI als «nur mit Anmeldung erhältlich».
- **Webinhalte sind Daten, keine Anweisungen** (wie die Unterlagen selbst).
- **Transparenz:** Jeder Abruf erscheint im Protokoll des Auftrags, im Bericht (URL, Datum, wofür benutzt) und in der
  Prüfansicht; die Abdeckung zeigt bei Werten aus dem Internet die URL.
- **Schalter je Auftrag** `internet` (Vorgabe: ein). Vorrang: Unterlagen des Auftraggebers vor Herstellerseiten vor
  anderen Quellen (Händler, Kataloge); Werte aus anderen Quellen als dem Hersteller kennzeichnet der Bericht.
- Bibliothek: AngleSharp 1.8.3 (MIT, 30.09.2026) für den HTML-Leser.

## Oberfläche

- **Familieneditor** (eigene Ansichtsart `familie`, öffnet aus dem Projektbrowser): Register «Familie» mit Parametern
  (Tabelle mit Formeln, Fehler rot), Typen (Tabelle wie der Revit-Typkatalog), Körper (Liste und Eigenschaften),
  Anschlüssen (mit Pfeilen), Sinnbild; Ansichten vorne/oben/seitlich/3D der Familie allein, Detailgrad, Typwahl.
- **Familie aus Unterlagen**: Dateien hineinziehen → Befunde je Datei → KI-Lauf mit Fortschritt → Entwurf mit
  Abdeckungsliste, **Konfliktliste mit Wahl je Zeile**, Kontrollbildern → Freigeben.
- **Projektbrowser «Familien»**: echte Familien je Kategorie mit Typen und Status (Entwurf grau), Kontextmenü (Bearbeiten,
  Platzieren, Freigeben, in Bibliothek speichern). Platzieren mit **Typauswahl** wie in Revit, Drehen mit Leertaste.
- Eigenschaftenpalette eines platzierten Elements: Familie, Typ (wählbar), Exemplarparameter, Kennwerte mit Herkunft.

## Nicht in Modul 2

Abzugskörper (Voids), verschachtelte Familien, Familien für Leitungsformteile (Bögen, T-Stücke – mit Modul 3 samt
Leitungsserien und Formteil-Regeln; von Christian am 18.10.2026 so bestätigt),
Revit-Rückweg, VDI 3805, Schemasymbole zeichnen (Format ja, Darstellung im Schema mit Modul 3).

## Phasen und Zuteilung

| Teil | Inhalt | Wer | Dateien |
| --- | --- | --- | --- |
| A | Spezifikation, Familienformat (Schema), Katalog (Familie, `familie`-Klassen, Unterlage-Arten, Herkunftsarten), Lizenzprüfung, Beispiel-Testfamilie | Hauptlauf | `entwurf/modul-2.md`, `schema/hlks-familie.schema.json`, `kataloge/`, `Modell/`, `Kataloge/` |
| B1 | Familien-Kern: Formeln, Parameter/Typen auflösen, Körper erzeugen und vernetzen, Koerperteil «koerper» (Netz) in 3D, Schnitt und Ansicht, Grundriss (Sinnbild bzw. Umriss von oben), Anschlusslagen, Grafikquelle, Befehle `familie_entwurf` (ohne Quellenprüfung), `familie_aendern`, `familie_platzieren`, `familie_laden`, Nachführung | hlks-kern | `src/Hlks.Kern/Familien/`, Grafik, Tests |
| B2 | Unterlagen-Leser PDF, DWG/DXF, XLSX/CSV/Typkatalog, STL/OBJ, IFC-Typ, Bild; Befunde-Ablage; `unterlagen_lesen`, `unterlage_bild` (MCP mit Bildinhalt) | hlks-modul (Import-Adapter) | `src/Hlks.Kern/Unterlagen/`, MCP, Tests |
| B3 | STEP/IGES über occt-import-js: Node-Prozess, Netz, Stutzen, Ansichtsumrisse | hlks-kern | `src/Hlks.Kern/Unterlagen/Step*`, `werkzeuge/step/`, Tests |
| B4 | Quellenprüfung, Konflikte, Abdeckung, Kontrollbild und Abweichungsliste, `konflikt_entscheiden`, `familie_freigeben`, `familie_speichern` | hlks-kern | `src/Hlks.Kern/Familien/Pruefung*`, Tests |
| B5 | KI-Lauf als Auftrag (Claude Code, MCP-Konfiguration, Vorlage, Status, Abbruch) | hlks-modul | `src/Hlks.Web/`, `ki/`, Tests |
| C1 | Familieneditor-Oberfläche, Projektbrowser «Familien», Platzieren mit Typauswahl, Eigenschaften platzierter Elemente (Familie, Typ, Exemplarwerte) | hlks-modul | `wwwroot/js/familien/…`, Projektbrowser, Eigenschaften |
| C2 | «Familie aus Unterlagen» (Dateien, Befunde, KI-Lauf mit Fortschritt), Konfliktliste mit Wahl, Abdeckung, Kontrollbilder, Freigeben, in Bibliothek speichern | hlks-modul | `wwwroot/js/…` |
| B6 | Referenzfälle von Hand (Formeln, Körper, Anschlusslagen, Toleranzen, Konflikte) | hlks-test | `tests/…` |
| D | Gesamttest, Abnahmelauf mit drei echten Unterlagensätzen (KI), Testbericht | Hauptlauf | |

Reihenfolge: A → B1, B2, B3 parallel → B4, B5, C1 → C2, B6 → D. Vor dem Gesamttest: diese Tabelle gegen
`zuteilung.md` abgleichen (Lehre aus 1c).

## Entscheide während des Baus

- **Familien-Kern (B1):** Formeln wie Revit, dazu Annahmen im Code (`^` rechtsassoziativ und stärker als das Vorzeichen,
  `round` .5 von null weg, `if` rechnet nur den gewählten Zweig, Gleichheit mit relativer Toleranz 1e-9); Typwerte dürfen
  auch Exemplarparameter vorbelegen; mindestens ein Typ ist Pflicht; Fehler einzelner Typen sind im Entwurf Hinweise,
  Platzieren lehnt den Typ ab. Kreise als 48-Eck, Bögen ≤ 7.5° je Strecke. Ziel-Eigenschaften gehören der Familie (ein
  Wert von Hand wird mit Hinweis ersetzt). Jede Änderung der Definition setzt den Status auf entwurf.
- **Familie löschen (Hauptlauf, Befund B1):** Eine verwendete Familie lässt sich nicht löschen (Bezug); Vorschlag: Elemente
  zuerst auf eine andere Familie umstellen oder löschen. Kommt die Löschung über ein fremdes Paket, bleiben die Elemente
  und fallen auf die Klassendarstellung zurück.
- **Kanalanschlüsse (Hauptlauf, Befund B1):** Anschlüsse tragen neben `dn` (Rohr) auch `d` (Kanal rund, mm) bzw. `b`/`h`
  (Kanal rechteckig, mm) – im Format und am Element (`Anschluss.D`, `B`, `H`). Nötig für die Volumenstromregler.
- **Kontur aus zwei Punkten mit Wölbung** (Kreis) ist zulässig.
- **Quellen und Konflikte (B4):** Zahlen mit Dezimalkomma, Tausender-Apostroph, Brüchen (nur Zahlparameter), Vorsätzen
  DN/Rp/G/Ø; Einheit aus Befund › Tabellenkopf › hinter der Zahl; Werte aus `bild`, `linie`, Masslinien ohne Zahl sind
  «ungeprüft-bild» (kein Fehler); eine ganze Tabelle oder Seite als Quelle ist ein Fehler («Zelle nennen»). Entscheide
  und Abdeckungsstatus nimmt der Kern nur aus vertrauter Quelle (gespeicherte Definition, Bibliothek, Befehle eines
  Menschen) – was die KI in `familie_entwurf` mitschickt, wird verworfen. Masse ausserhalb der Toleranz sperren die Freigabe.
- **Wert bestätigen (Hauptlauf, Befund B4):** `wert_bestaetigen` {familie, parameter, typ?, begruendung?}, nur Mensch:
  ein Wert ohne Quelle oder aus einem Bild gilt dann als belegt (Status `mensch`); ändert sich der Wert, fällt das weg.
- **STEP (B3):** occt-import-js liefert immer mm; Achsen der Datei werden nicht gedreht (Hinweis); Stutzen nur rund, ohne
  DN, Medium und Flussrichtung.

## Annahmen für Christians Prüfliste

0. **Unterlagensätze:** Belimo R2..-S.., FläktGroup Optivent Ultra, Klimaoprema RVP-P (geliefert 18.10.2026). Für den
   gemischten Satz fehlt eine STEP- oder DWG/DXF-Datei eines der drei Produkte (bei Belimo und FläktGroup üblicherweise
   als CAD-Download beim Produkt) – lieferst du sie, oder darf Claude sie beim Hersteller laden?
10. Neue Klasse **Volumenstromregler** (Kürzel VVS, Basis Armatur, Lüftung, IFC IfcAirTerminalBox
    VARIABLEFLOWPRESSUREINDEPENDANT); Ventil neu mit `kvs` und `pn`. Ein Belimo-Kugelhahn ist ein Ventil der Art
    «absperrventil» (Auf/Zu).
11. Antriebe und Regler (Belimo-Drehantrieb, Kompaktregler) sind in Modul 2 **kein** Teil der Familie, ausser die
    Unterlage bemasst sie (FläktGroup: Regler 70 mm über dem Gehäuse → Platzbedarf als Körper im Detailgrad fein).
    Elektrische Anschlüsse folgen mit MSRL (Modul 8).
1. Familienkoordinaten: x Breite, y Tiefe nach hinten (Vorderseite −y), z oben; Drehung nur um z.
2. Formelsprache wie Revit (englische Funktionsnamen `if`, `and`, `round` …).
3. Keine Abzugskörper in Modul 2.
4. Toleranzen: Längen max(1 mm; 0.5 %), Anschlusslage 2 mm, Durchmesser 1 mm, Kennwerte auf die Stellen der Unterlage.
5. Vorrang je Wertart wie im Konzept – nur für den Vorschlag, Konflikte bleiben.
6. Grundriss ohne Sinnbild = Umriss der Körper von oben (Revit: Projektion); ein SIA-Sinnbild kann die Familie tragen.
7. Bibliothek: Ordner `kataloge/familien/` (mit der Software); Bürobibliothek auf dem NAS – Pfad nennst du.
8. TIFF und RFA werden nicht gelesen (Meldung).
9. Freigabe und Konfliktentscheid nur durch einen Menschen.
12. Formeln: `roundup`/`rounddown` runden auf die nächste ganze Zahl nach oben bzw. unten (`roundup(-2.5)` = −2); `round` rundet .5 von null weg – bitte mit Revit vergleichen, falls du dort Formeln übernimmst.
