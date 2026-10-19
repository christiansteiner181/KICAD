# Testbericht Modul 2 – Familien und Familie aus Unterlagen

Stand 18.10.2026 · gebaut am 18.10.2026 (geplant 21.12.2026–22.01.2027) · **von Christian abgenommen am 18.10.2026**

## Ergebnis

Familien wie in Revit sind gebaut: Parameter mit Formeln, Typen, Körper, Anschlüsse mit Lage und Richtung, Sinnbild,
Familieneditor, Platzieren mit Typauswahl. Die KI erstellt Familien aus Herstellerunterlagen und recherchiert fehlende
Angaben im Internet (dein Wunsch vom 18.10.2026). Widersprüche landen in einer Konfliktliste, freigeben kann nur ein
Mensch. Geprüft wurde mit deinen drei Datenblättern, mit echten KI-Läufen über dein Abo und mit Internet-Recherche.

| Kriterium (Spezifikation `entwurf/modul-2.md`) | Nachweis |
| --- | --- |
| 1 Heizkörper im Editor gebaut, platziert, Lagen = Handrechnung | Der Browser-Test (110 Prüfungen) baut die Testfamilie per Klick nach: Parameter mit Formel `oberkante = bodenabstand + hoehe`, zwei Typen, Körper mit Detailgrad und `sichtbar`, Anschlüsse mit Formeln. Dann platziert er sie mit 90° bei [5000, 3000, 0]: vl (4950, 3950, 120) und rl (4950, 3900, 120) wie von Hand gerechnet; Typwechsel (vl y 4350) und Exemplarwert ebenso. Dazu 227 unabhängige Referenzfälle von Hand (Formeln, Volumen, Anschlusslagen bei 0/90/180/270/45°, Toleranzen). |
| 2 Drei echte Unterlagensätze, Geometrie, Anschlüsse, Typen | **FAM-1 FläktGroup Optivent Ultra** (18 Typen ULSA/ULDA), **FAM-2 Belimo R2..-S..** (6 Typen; Masstabelle aus dem eingebetteten Bild gelesen), **FAM-3 Klimaoprema RVP-P** (58 Typen). Gemischt: FläktGroup PDF + Typentabelle CSV; RVP-P Datenblatt + Herstellerkatalog-PDF + Webseiten, beides von der KI selbst aus dem Internet geholt. **Einschränkung:** Eine STEP-, DWG- oder DXF-Datei war bei keinem der drei Hersteller frei erhältlich (FläktGroup nur über MagiCAD Cloud, Belimo und Klimaoprema nicht ohne Anmeldung). Der STEP- und DXF-Weg ist mit Testdateien geprüft, mit einer echten Hersteller-CAD-Datei noch nicht. |
| 3 Widerspruch landet in der Konfliktliste | **Absichtlich:** CSV mit L = 499 statt 489 bei ULSA-125 ergibt einen Konflikt mit beiden Fundstellen (PDF S. 3 Zelle, CSV Zelle), Vorschlag 489. **Natürlich:** Im RVP-P-Datenblatt liegt die Trennlinie H 600/700 eine Zeile zu tief. Die KI fand im Herstellerkatalog aus dem Internet dieselbe Zeile unter H = 700 und legte den Konflikt mit beiden Quellen an, begründet (Aef = 0,85·0,7·0,7). Freigabe in beiden Fällen gesperrt (nachgewiesen); die KI kann weder entscheiden noch freigeben. |
| 4 Masse und Anschlusslagen stimmen mit jedem Massbild | Massvergleich je Typ: FAM-1 **90/90**, FAM-2 **24/24**, FAM-3 **290/290** bestanden. Weil Massbilder in Datenblättern schematisch sind, wird über die bemassten Werte je Typ verglichen; die Kontrollbilder (mit Passpunkten) sind Anschauung. Platziertes Belimo-Ventil R2025-S2, 90° gedreht: Anschlüsse (1000 / 1956,5 / 2500) und (1000 / 2043,5 / 2500) wie von Hand gerechnet, Kvs 26 und PN 40 am Element. |
| 5 MCP, Trockenlauf, rückgängig, Herkunft | Rund 30 neue MCP-Werkzeuge (Familien, Unterlagen, Prüfung); jeder Befehl mit Trockenlauf und als Paket rückgängig; jeder Wert nennt seine Befund-Id mit Datei, Seite und Zelle bzw. URL. |

## Stichproben gegen deine Datenblätter (von mir nachgeprüft)

| Familie / Typ | Werte der KI | Datenblatt |
| --- | --- | --- |
| FAM-1 ULSA-125 | ØD 124, a 35, L 489 (Konflikt 499), 1,8 kg | S. 3: 124 / 35 / 489 / 1,8 ✓ |
| FAM-1 ULDA-400 | ØD 399, ØDy 500, a 60, L 950, 22,0 kg | S. 3: 399 / 500 / 60 / 950 / 22,0 ✓ |
| FAM-2 alle 6 Typen | L, L1, H, X, Y, kg, Kvs, PN (z. B. R2032-S3: 105, 19, 50,5, 240, 90, 0,75, 32, 25) | S. 1 und S. 3 (Bild): alle ✓ |
| FAM-3 200×100, 1000×1000 | Aef 0,0170 / 0,85, Vmin 130 / 6265, Vmax 720 / 36000 | S. 3 ✓ |

Die KI hat zusätzlich Unstimmigkeiten gemeldet, ohne sie zu korrigieren: RVP-P 1000×350 Aef 0,2955 statt 0,2975 nach
der Beziehung; 1000×400 Aef 0,3401 statt 0,3400.

## Abnahmeläufe (18.10.2026, über dein Abo, Standardmodell, mit Internet)

| Lauf | Dauer | Ergebnis | Internet | Abdeckung | Masse |
| --- | --- | --- | --- | --- | --- |
| 1 FläktGroup + CSV mit Widerspruch | 9 min | FAM-1, 18 Typen, 4 Körper, 2 Kanalanschlüsse rund, 1 Konflikt | 2 Suchen, 3 Seiten (flaktgroup.com), kein CAD frei | 161 geprüft, 18 ungeprüft (ja/nein), 1 Konflikt | nach Korrekturlauf (3 min, ohne Internet) 90/90 |
| 2 Belimo | 4 min | FAM-2, 6 Typen, Rohranschlüsse DN mit Gewinde Rp | 2 Suchen, Shopseite R2015-S1 | 24 geprüft, 43 aus Bild gelesen (ungeprüft) | 24/24 |
| 3 Klimaoprema RVP-P | 7 min | FAM-3, 58 Typen, Kanalanschlüsse rechteckig B × H, 1 Konflikt | 2 Suchen, 3 Abrufe, darunter der Herstellerkatalog als PDF (2,2 MB) | 290 geprüft, 1 Konflikt, 1 fehlt (Flanschtiefe) | 290/290 |

Der erste Lauf hat vier Fehler im Kern sichtbar gemacht (Befunde 11–14 unten); sie sind behoben, bevor Lauf 2 und 3
liefen. Die Berichte der KI sind sachlich und vollständig: gelesene Werte mit Fundstellen, Konflikte, offene Felder,
Annahmen (z. B. Gehäuseradius des Kugelhahns, der nicht bemasst ist), Kontrolle, nicht Übernommenes, Internet-Abrufe.

## Was gebaut ist

| Baustein | Inhalt | Ort |
| --- | --- | --- |
| Familienformat | Parameter (typ/exemplar, Formeln wie Revit), Typen, Körper (quader, zylinder, kegel, kugel, extrusion, rotation, sweep), Anschlüsse mit Lage/Richtung (Rohr DN/Gewinde, Kanal rund d, rechteckig b/h), Sinnbild, Masse, Konflikte, Abdeckung | `schema/hlks-familie.schema.json` |
| Familien-Kern | Formelsprache, Auswertung, Netze, Grundriss/Schnitt/Ansicht/3D, Anschlusslagen, Nachführung, Befehle `familie_entwurf/aendern/laden/platzieren` | `src/Hlks.Kern/Familien/` |
| Unterlagen lesen | PDF (Wörter, Zeilen, Tabellen, Vektoren, Masse), DWG/DXF (Bemassungen exakt), XLSX/CSV/Revit-Typkatalog, STL/OBJ, Hersteller-IFC, Bild, HTML, **STEP/IGES** (OpenCascade, Stutzen-Erkennung) | `src/Hlks.Kern/Unterlagen/`, `werkzeuge/step/` |
| Prüfung | Wert gegen Quelle (Zahlen, Einheiten, Toleranzen), Konflikte, Abdeckung, Massvergleich je Typ, Kontrollbild, `konflikt_entscheiden`, `wert_bestaetigen`, `familie_freigeben`, `familie_speichern` (nur Mensch) | `src/Hlks.Kern/Familien/Familien{Quellen,Konflikte,Masse,Kontrollbild,Freigabe}.cs` |
| Internet | `unterlage_aus_url`: Download mit Sperre interner Adressen (auch nach DNS und Weiterleitung), 50 MB, Dateiart nach Inhalt, ZIP mit Schutz, URL = Identität; Websuche nur zum Finden | `UnterlagenDownload.cs` |
| KI-Lauf | Claude Code über dein Abo als Auftrag mit Status, Protokoll, Abbruch, Zeitlimit; nur Projekt-MCP mit Akteur «KI»; mit Internet nur Familien- und Unterlagen-Werkzeuge, kein WebFetch | `src/Hlks.Web/AuftragDienst.cs`, `ki/familie_aus_unterlagen.md` |
| Oberfläche | Familieneditor (Register «Familie», vier Ansichten), Projektbrowser «Familien», Platzieren mit Typauswahl, Dialog «Familie aus Unterlagen», Prüfansicht (Bericht, Abdeckung, Konflikte, Masse, Kontrollbilder, Freigeben), Befund-Browser | `src/Hlks.Web/wwwroot/js/familien/` |
| Katalog | Klasse Familie, familienfähige Klassen, neue Klasse **Volumenstromregler** (VVS), Ventil mit Kvs/PN | `kataloge/` |

Bilder (synthetische Daten): `berichte/c1m2-bilder/` (Editor, Platzieren, Palette) und `berichte/c2m2-bilder/`
(Dialog, Prüfansicht, Befund-Browser, Kontrollbilder).

## Automatische Tests

| Bereich | Tests |
| --- | --- |
| Kern (alle Module) | 2642 |
| MCP | 30 |
| Web (REST, Aufträge mit Attrappe statt Claude) | 148 |
| **Total C#** | **2820 bestanden** |
| Node | 294 bestanden |
| Browser (Edge headless, je frisches Beispielprojekt) | 603 bestanden: werkzeuge 72, einfuegen 82, huelle 79, huelle-darstellung 28, hkap 83, familien 110, familie-unterlagen 149 |

Davon neu in Modul 2 u. a.: Familien-Kern 163 + Nachträge, Leser 58 + Download 51, STEP 33, Prüfung 46, Referenzfälle
von Hand 227, KI-Auftrag 50 + Nachträge, Oberfläche 66 Node und 259 Browser-Prüfungen.

## Während des Baus gefunden und behoben

| # | Fund | gefunden von | Behebung |
| --- | --- | --- | --- |
| 1 | Klassenname «Befund» schon für Prüfbefunde vergeben | Hauptlauf | `UnterlagenBefund` |
| 2 | Bernstrasse-Revit: 8 VAV-Boxen hatten keine Klasse | Hauptlauf (Katalog) | Klasse Volumenstromregler |
| 3 | Anschluss ohne Kanalmasse | hlks-kern (B1) | Anschluss D, B, H |
| 4 | Jeder Plan rechnete die Prüfsumme neu (250 ms bei 500 Elementen) | hlks-kern (B1) | zwischengespeichert: Grundriss 283 → 104 ms |
| 5 | Oberfläche bekam die ganze Familiendefinition je Element | hlks-kern (B1) | Zusammenfassung |
| 6 | Belimo-Masstabelle ist ein eingebettetes Rasterbild | hlks-modul (B2) | KI liest aus dem Bild, Werte als «ungeprüft» markiert |
| 7 | **RVP-P-Datenblatt: Trennlinie H 600/700 eine Zeile zu tief** (mein Verdacht auf einen Leserfehler war falsch) | hlks-modul (B2) | als Datenbefund geführt, KI meldet ihn als Konflikt |
| 8 | **WebFetch umgeht die Sperre interner Adressen und wäre ein Abflussweg** | hlks-modul (B5) | WebFetch entfernt, mit Internet nur Familien-/Unterlagen-Werkzeuge |
| 9 | Unterlage mit Bezug auf eine Familie wurde abgelehnt | hlks-modul (C2) | Hauptlauf: Nachführung lässt Unterlagen aus |
| 10 | KI-Lauf «fertig» trotz abgelehnter Änderung | hlks-modul (C2) | Liste `abgelehnt`, Warnung |
| 11 | Massvergleich ohne Sollwert (72 von 90) | Hauptlauf (Abnahmelauf 1) | Soll aus gleichnamigem Parameter, Hinweis im Trockenlauf, Vorlage verlangt `parameter` |
| 12 | Textwerte nur bei wörtlicher Gleichheit belegt | Hauptlauf (Abnahmelauf 1) | Vergleich nach Wörtern |
| 13 | Massquelle aus der falschen Tabelle (ULSA statt ULDA) | KI im Bericht (Lauf 1) | Zeile nur aus eigenen Quellen oder eindeutigem Typnamen |
| 14 | Drei Internet-Abrufe überschrieben dieselbe Unterlage | KI im Bericht (Lauf 1) | URL ist die Identität |
| 15 | Gewinde des Anschlusses als Text statt Wert des Typs | Hauptlauf (Abnahme Belimo) | `gewinde` darf der Name eines Textparameters sein; am Anschluss steht dessen Wert des Typs (B1, 3 Tests) |
| 16 | HKAP-Browser-Test hängt vom Zustand der Testkopie ab | Hauptlauf (Gesamttest) | frische Datenkopie vor dem Lauf |

## Zuteilung von Modell und Denktiefe

| Agent | Aufträge | Nacharbeit |
| --- | --- | --- |
| hlks-kern (Opus, xhigh) | B1 Familien-Kern (2 Nachträge), B3 STEP, B4 Prüfung (3 Nachträge) | keine |
| hlks-modul (Sonnet, high) | B2 Leser (Import-Adapter, 3 Nachträge), B5 KI-Lauf (3 Nachträge), C1 Editor, C2 Unterlagen-Oberfläche | keine; B5 machte vier statt eines echten Probelaufs (zusammen rund 0.06 USD) |
| hlks-test (Sonnet, medium) | B6 Referenzfälle (227) | keine, kein Befund |

Die Nachträge kamen aus Befunden in meinem Code, meiner Spezifikation, aus der Abnahme oder aus deinen neuen Wünschen
(Internet, ZIP), nicht aus Fehlern der Agenten. Eigene Fehler: eine falsche Zeilenzahl (66 statt 58) im Auftrag an B2,
ein falscher Verdacht gegen den Leser, eine Textänderung ohne den zugehörigen Test.

**Nachjustiert:** Testfälle schreiben (hlks-test) ist jetzt dreimal ohne Nacharbeit gelaufen (1b, 1c, 2) und hat jedes
Mal echte Befunde gefunden → die Probe ist bestanden, die Zuteilung gilt fest (CLAUDE.md angepasst). Tiefer geht es
nicht, weil Handrechnungen Urteilsvermögen brauchen. Alles andere bleibt.

## Annahmen zum Prüfen (Prüfliste)

Vollständig in `entwurf/modul-2.md`, Abschnitt «Annahmen für Christians Prüfliste». Die wichtigsten:

1. Familienkoordinaten x Breite, y Tiefe (Vorderseite −y), z oben; Drehung nur um z.
2. Formeln wie Revit; `round` .5 von null weg, `roundup`/`rounddown` = auf- bzw. abrunden.
3. Toleranzen: Längen max(1 mm; 0.5 %), Anschlusslage 2 mm, Durchmesser 1 mm, Kennwerte ± halbe letzte Stelle.
4. Werte aus Bildern (z. B. Belimo-Masstabelle) gelten als «ungeprüft»; die Freigabe nennt ihre Anzahl. Du kannst sie mit «Wert bestätigen» annehmen.
5. Internet: nur Websuche zum Finden, Werte nur aus abgelegten Dokumenten, interne Adressen gesperrt, kein WebFetch.
6. Bibliothek `kataloge/familien/`; ein Pfad für die Bürobibliothek auf dem NAS ist offen.
7. Antriebe und Regler werden nur modelliert, wenn bemasst (FläktGroup-Regler nur im Grob-Hüllquader).
8. Leitungsformteile (Bögen, T-Stücke) und Leitungsserien kommen mit Modul 3 (von dir bestätigt).

## Bekannte Grenzen und offene Punkte

- **Keine echte Hersteller-CAD-Datei** im Test (keine frei erhältlich). Hast du über ein Herstellerkonto eine STEP- oder
  DWG-Datei zu einem der drei Produkte, zieh sie in den Dialog: Der Weg ist gebaut, nur nicht mit echter Datei geprüft.
- Kontrollbilder gegen schematische PDF-Massbilder brauchen Passpunkte (die KI setzt sie); gemessen wird über die Werte.
- FläktGroup nennt keinen Volumenstrom in m³/h, nur Geschwindigkeiten → `volumenstromMin/Max` leer (offenes Feld).
- RVP-P: Flanschtiefe ohne Quelle → vor der Freigabe bestätigen oder belegen.
- Webseiten, die ihren Inhalt erst per Skript laden, kann der HTML-Leser nicht lesen (Hinweis).
- Ein Rest bleibt beim Internetzugang: Dass die KI Webinhalte als Daten und nicht als Anweisungen behandelt, steht in der Vorlage, technisch erzwingen lässt es sich nicht. Den Abflussweg habe ich klein gehalten (kein WebFetch, wenige Werkzeuge, jede Suche im Protokoll); ganz ausschliessen lässt er sich nur mit dem Schalter «ohne Internet».
- Ein KI-Lauf dauert 4–9 Minuten und braucht dein Abo-Fenster; mehrere Läufe nacheinander, nicht gleichzeitig.

## Selbst ausprobieren (Abnahme)

Abnahmeprojekt mit den drei Familien: **http://localhost:5149**
1. Projektbrowser › Familien › FAM-1 «Prüfen»: Konflikt L bei ULSA-125 entscheiden (489 laut Datenblatt; die CSV war
   absichtlich falsch) → freigeben.
2. FAM-3 «Prüfen»: Konflikt H bei «RVP-P 700×600/700» entscheiden und die Flanschtiefe bestätigen oder belegen → freigeben.
3. FAM-2: die 43 aus dem Bild gelesenen Werte ansehen («Quelle ansehen» zeigt den Bildausschnitt) → freigeben.
4. Familieneditor öffnen, Platzieren mit Typauswahl (Leertaste dreht), oder einen eigenen Unterlagensatz in
   «Familie aus Unterlagen» ziehen.
