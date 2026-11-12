# Gesamtprüfung 13.11.2026 – Bereich import_familien_baustelle

Zweig `worktree-agent-a07b98146ac20b6f1`, Ausgangsstand `3aed5cb`. Geprüft: IFC-Import und Ausrichtung (IfcDatei, IfcAusrichtung,
IfcReferenzpunkte, IfcUebernahme im Teil Ausrichtung/«keine», Unterlagen), IfcDarstellung/GrundlagenIfc, Familienbibliothek (Grundbestand,
familie_laden, Freigabe-Sperren; Dienst und Schreiben nur im Überblick), KICAD Baustelle (Export, Betrachter, Zugänge).
Nicht im Detail gelesen: IfcEinpassen ab Zeile 75 (nur Verfahren und Herleitung des Bezugs), IfcGeometrie*, IfcHlks*, IfcArchitektur,
FamilienBibliothekDienst ab «Index», FamilienQuellen*, FamilienKonflikte, FamilienKontrollbild.

## 1. Kurzfazit

Der Bereich ist im Kern sauber gebaut: die Koordinatenketten (IFC-Welt → Projekt, Koordinationskörper, Landeskoordinaten, glTF, Einbaulage der
Familien) habe ich von Hand nachgerechnet, sie stimmen. Drei echte Fehler sind korrigiert: (1) die neue Lage aus Referenzkegeln nahm bei zwei
Kegeln stillschweigend eine von zwei gleichwertigen Lagen (180° falsch möglich) und machte umgekehrt eine richtig liegende Datei «unsicher»;
(2) ein Monteur-Zugang sah Bauleitungs-Webseiten (Arbeitsstand), sobald der Projektname «_Monteur_» enthielt; (3) eine KI konnte über
`familie_laden` mit einer Datei, die sich selbst «freigegeben» nennt, die Freigabe durch den Menschen umgehen. Offen und von Christian zu
entscheiden ist vor allem «Nachzeichnen? Nein»: der Kern behandelt es wie «Familie aus IFC einlesen» und übernimmt dann auch bei Abweichung
oder nicht eindeutigem Einpassen ohne Abbruch.

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Test (Filter → Ergebnis) | Commit |
|---|---|---|---|---|---|
| 1 | `src/Hlks.Web/BaustelleZugaenge.cs` (DarfSehen) | Monteur darf sehen, wenn der Dateiname irgendwo «_monteur_» enthält. Exportname = `<Projekt>_<rolle>_<Zeit>.html`; Projekt «Halle Monteur Nord» → `Halle_Monteur_Nord_bauleitung_….html` war für den Monteur sichtbar (auch Arbeitsstand). | Die letzte Rollenangabe im Namen zählt (`_monteur_` nach dem letzten `_bauleitung_`). | `Hlks.Web.Tests` `BaustelleZugang` → 3/3 grün (neu: `Monteur_SiehtKeineBauleitungsseite_…`) | 0727ade |
| 2 | `src/Hlks.Web/BaustelleZugaenge.cs` (Lesen) | Unlesbare `zugaenge.json` → JsonException → Fehler 500 ohne Meldung. | HlksFehler mit klarer Meldung; der Zugang bleibt gesperrt (nicht «offen wie ohne Datei»). | wie 1 (im Test Zugänge ergänzt) | 0727ade |
| 3 | `src/Hlks.Web/BaustelleZugang.cs` (Rückmeldung) | Dateiname nur sekundengenau: zwei Meldungen derselben Person in derselben Sekunde überschrieben sich; Name unbegrenzt lang (Pfadlänge). | Name auf 40 Zeichen, bei vorhandener Datei `_2`, `_3` … | wie 1 | 0727ade |
| 4 | `src/Hlks.Kern/Import/IfcReferenzpunkte.cs` | Paarlage gab die **erste** passende Zuordnung zurück. Zwei Kegel bestimmen die Lage nur bis auf eine halbe Drehung (1→A, 2→B oder 1→B, 2→A) – je nach Reihenfolge in der Datei kam die um 180° gedrehte Lage als «eindeutig» und wurde beim Einpassen übernommen. Umgekehrt: Datei liegt richtig im Projektbezug, Kegel nur in anderer Reihenfolge → «projekt» (2 Treffer) und gedrehte Paarlage (2 Treffer) → «unsicher». | «projekt»/«gebaeude» gehen vor; Paarlagen nur, wenn keine davon trifft, und dann **alle** Zuordnungen; eindeutig nur die Lage, die allein die meisten Kegel trifft (ab 3 Kegeln). Gleichheit zweier Lagen wird an der Wirkung auf die Punkte gemessen (nicht an x/y/Drehung des weit entfernten Bezugspunkts). Bewertung ohne Datei rechenbar (`Bewerten`). | `Hlks.Kern.Tests` `IfcReferenzpunkte` → 4/4 grün (3 neu mit Handbeispiel, 1 Echt-Test BKW unverändert grün) | e275701 |
| 5 | `IfcReferenzpunkte.ProjektKegel` | Bezugsdatei aus `quelle.Split(" (")[0]` – bei «Modell (2).ifc» wurde der Name abgeschnitten, ältere Projekte fanden ihre Kegel nicht. | Trennstellen von hinten versuchen, bis eine Unterlage so heisst. | – (kein eigener Test, reine Namenssuche) | e275701 |
| 6 | `src/Hlks.Kern/Import/IfcDatei.cs` | `Referenzkegel()` lief je Ausrichtung mehrmals über alle Produkte und Typbeziehungen. | Ergebnis je Datei gemerkt. | wie 4 | e275701 |
| 7 | `src/Hlks.Kern/Familien/FamilienBefehle.cs` (DateiLaden) | `familie_laden { datei }` übernahm `status` aus der Datei. Eine KI mit einer Datei im Projektordner bekam so eine «freigegebene» Familie ohne `familie_freigeben` (im Test nachgewiesen: Status «freigegeben»). | Für Herkunft ≠ mensch und Dateien ausserhalb `kataloge/familien`: als Entwurf laden, Hinweis. Menschen und mitgelieferte Bibliothek unverändert. | `Hlks.Kern.Tests` `FamilieLadenKiTests` → 7/7 grün (1 neu) | ce46269 |
| 8 | `src/Hlks.Kern/Baustelle/BaustelleExport.cs` (box3d) | Kasten für «Raum in 3D» reichte nur bis zur lichten Höhe (+0.3 m Rand): Leitungen über der abgehängten Decke bzw. an der Rohdecke fielen aus dem Raumschnitt – genau das, was der Monteur sucht. | Höhe = grösserer Wert aus Raumhöhe und Geschosshöhe. **Bitte bestätigen** (siehe Frage 4). | `BaustelleExportTests` → grün, box3d gegen Handrechnung | 38da5cc |
| 9 | `src/Hlks.Kern/Grafik/GrundlagenIfc.cs` | `deckkraft` der Unterlage ungeprüft (Nachbau-Deckkraft und Oberfläche begrenzen auf 0…1). | `IfcDarstellung.Deckkraft` (0…1). | `IfcDarstellungTests` → grün | 99e5ed2 |
| 10 | `src/Hlks.Kern/Baustelle/betrachter.js` | Dokumentverweis (`dokumente[].url`) ungeprüft als `href` – `javascript:`-Adresse aus Modelldaten wäre auf dem Tablet ausführbar. | `javascript:`, `data:`, `vbscript:` werden nicht verlinkt. | – (Browserreihe nicht gelaufen, siehe unten) | 99e5ed2 |
| 11 | `src/Hlks.Kern/Familien/FamilienGrundbestand.cs` | Bibliothek nicht erreichbar und kein gemerkter Stand → neues Projekt still ohne Familien (`Hinweis` null). | Meldung der Bibliothek als Hinweis. | `FamilienBibliothekTests` Filter `Grundbestand` → grün (bestehender Test; der Offline-Fall selbst ist nicht getestet) | 99e5ed2 |

Testläufe gesamt: Kern gefiltert 28 + 7 grün, Web gefiltert 3 grün. Nicht gelaufen: Browserreihen, Abnahmeserver, JS-Tests (kein JS der
Oberfläche geändert; `betrachter.js` hat keinen Node-Test).

Hinweis: Bei jedem `git commit` meldete Git «failed to delete …/.git/worktrees/wt_b1 bzw. wt_b2: Permission denied» (Aufräumen fremder,
verwaister Arbeitskopien; nicht von mir angelegt, die Commits sind sauber).

### Von Hand nachgerechnet (Auftrag «Logik hinterfragen, besonders Geometrie»)

1. **IFC-Welt → Projekt** (`IfcDatei.ZuModell`): Modell = R(−d)·(Welt − B). Mit B = (10 000, 5 000, 0), d = 90°: Weltpunkt (10 000, 6 000) →
   (0, 1000) gedreht um −90° → (1000, 0). Stimmt mit der Aussage «Projektachsen sind um +d gegen die Dateiachsen gedreht».
2. **Referenzkegel, Lage aus Kegelpaaren**: Bp = (2 600 800, 1 200 300) m, d = 0; P1 = (2 600 830, 1 200 380), P2 = (2 601 050, 1 200 610);
   Datei um 90° gedreht, D1 = (0, 0), D2 = (230, −220) m. θ = atan2(230, 220) − atan2(−220, 230) = 46.27° − (−43.73°) = 90°; d = 0 − 90 = −90°;
   B = D1 − R(−90°)·(30, 80) = (−80, 30) m. Probe D2: (310, −250) um +90° → (250, 310) = P2 − Bp. Formel richtig; als Test hinterlegt
   (`DreiKegel_…`). Die zweite Zuordnung (D1 → P2, D2 → P1) trifft ebenfalls beide Kegel → Fehler 4.
3. **Koordinationskörper** (`NachKoordinationskoerper`): aus R(−dr)·(kw − B) = k folgt B = kw − R(dr)·k, dr = Drehung(Datei) − Drehung(Projekt). Stimmt.
4. **Landeskoordinaten** (`Abweichung`): Ost = E0 + x·cos w − y·sin w mit w = atan2(XAxisOrdinate, XAxisAbscissa); Nordwinkel Projekt = w + d.
   Beispiel: Projektursprung (10, 5) m, gespeichert Ost 2 600 010; zweite Datei mit E0 um +2 m → Abweichung +2.000 m Ost. Stimmt. Offen: Einheiten, siehe 3.4.
5. **Einpassen, Bezug** (IfcEinpassen Ziffer 6): Projekt = R(θ)·R(−d0)·(W − B0) + t ⇒ d = d0 − θ, B = B0 − R(d)·t. Stimmt.
6. **box3d** (glTF gx = x, gy = z, gz = −y, Meter): Raum x 2000…6000, y 1000…4000 mm, Kote 3060, Höhe 2400 →
   [2, 3.06, −4, 6, 5.46, −1]: min < max in jeder Achse, weil −max y die kleinere gz ist. Schnittebenen im Betrachter (n·p + c ≥ 0 bleibt
   sichtbar): (1,0,0), −(x0 − 0.3) usw. – stimmt. Im Test gegen die Geometrie des Beispielraums geprüft.
7. **Einbaulage der Familien**: y = z × x. Drehung 90°: x = (0, 1, 0), y = (−1, 0, 0); Anschluss lokal (100, 0, 50), Ursprung (1000, 2000, 300) →
   (1000, 2100, 350). Steigbogen x = (1,0,0), z = (0,−1,0) → y = (0,0,1) (lokales y zeigt nach oben). Rechtshändig, stimmt.
8. **Matrix4.Achsen** (IfcAxis2Placement3D): y = z × x, x rechtwinklig zu z gemacht – wie IFC. Randfall siehe 3.9.

## 3. Gefunden, nicht korrigiert

| # | Stelle | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 1 | `src/Hlks.Kern/Import/IfcUebernahme.cs:132`, `:150`–`:162` | «Nachzeichnen? Nein» (11.11.2026) sendet `bereiche = ["keine"]`. Im Kern ist «keine» das «IFC einlesen» für Familie aus IFC (03.11.2026) mit «die Lage zählt nicht»: bei Bewertung «abweichung» **und** bei gewähltem, aber nicht eindeutigem Einpassen kein Abbruch – die Unterlage kommt mit dem Projektbezug herein; die erste Datei setzt keinen Projektbezug. | Die Oberfläche verlangt die Wahl zwar selbst; über MCP/KI oder bei «einpassen nicht eindeutig» liegt ein Fachmodell als Hintergrund still an der Vergleichslage. Widerspricht «Kein Modell wird stillschweigend verschoben» (17.10.2026) bzw. «Lage und Ausrichtung gelten» (Dialogtext). Ein zuerst nur eingelesenes Fachmodell und ein später nachgezeichnetes Architekturmodell haben keinen gemeinsamen Bezug. | Zwei Fälle trennen: «nur als Unterlage» (Lage zählt, Abbruch wie sonst, erste Datei setzt den Projektbezug) und «für Familie einlesen» (Lage zählt nicht), z. B. eigener Wert `bereiche = ["unterlage"]`. | mittel | hoch |
| 2 | `src/Hlks.Web/BaustelleZugaenge.cs:83` (DarfSehen) | Die Rolle einer Webseite steht nur im Dateinamen. Eine mit `datei` frei benannte Bauleitungs-Seite, die auf `_monteur_….html` endet, gilt als Monteur-Seite. | Nur bei bewusst so benannter Datei; der Stand (freigegeben/Arbeitsstand) wird nicht geprüft. | Rolle und Stand aus dem Kopf der Seite lesen (`"rolle":"monteur","stand":"freigegeben"` in den ersten kB, je Datei nach Änderungszeit merken) oder beim Export eine kleine Begleitdatei schreiben. | klein–mittel | mittel |
| 3 | `src/Hlks.Web/BaustelleZugang.cs:102` ff. | Persönlicher Link: kein Zähler für Fehlversuche, Cookie ohne `Secure` (kein HTTPS), Schlüssel 12 Zeichen (≈ 59 Bit), gilt ein Jahr, Seiten ohne `Cache-Control: no-store`. | Im Büronetz vertretbar (so beschrieben: Testserver). Für einen Zugang von der Baustelle (Internet) nicht ausreichend. | Vor einem Betrieb ausserhalb des Büronetzes: HTTPS, Drosselung, kürzere Laufzeit, `no-store`. | mittel | mittel (hoch, sobald öffentlich) |
| 4 | `src/Hlks.Kern/Import/IfcDatei.cs:253`–`:275` (Karte) | Eastings/Northings/OrthogonalHeight werden mit der Längeneinheit des Projekts umgerechnet, `IfcProjectedCRS.MapUnit` wird nicht gelesen; `Scale` wird zusätzlich angewendet. Handbeispiel: mm-Datei, MapUnit Meter, Eastings 2 600 830, Scale 0.001 → Ost = 2 600.83 m statt 2 600 830 m; ein Punkt bei x = 10 m → 0.01 m statt 10 m. | Abweichung «Landeskoordinaten» wäre bei solchen Dateien um Faktor 1000 falsch (bricht mit ACHTUNG ab, verschiebt also nichts still, führt aber in die Irre). Eigene Exporte (MapUnit mm) und Meter-Dateien sind nicht betroffen. | MapUnit lesen; gegen eine echte Revit-Datei mit `ePSet_MapConversion` prüfen. Quelle (buildingSMART IFC4, IfcMapConversion/IfcProjectedCRS) muss nachgeschlagen werden – ich habe sie hier nicht zur Hand, darum nicht geändert. | klein | mittel |
| 5 | `src/Hlks.Kern/Import/IfcDatei.cs:199` (Referenzkegel) | Lage des Kegels = Ursprung seiner Platzierung. Trimble Nova legt Bauteile in den Ursprung und schreibt die Lage in die Geometrie (Befund in IfcAusrichtung.InhaltsPunkte). | Kegel aus solchen Dateien lägen alle bei (0, 0) → «kein Treffer» (kein Schaden, aber die Prüfung greift nicht). | Mitte der Grundfläche bzw. Begrenzung des Körpers statt der Platzierung. | klein | mittel |
| 6 | `src/Hlks.Kern/Import/IfcReferenzpunkte.cs` | (a) Zwei Kegel in eigenen Koordinaten bleiben jetzt «unsicher». (b) Die Höhe wird nicht geprüft (nur Grundriss). (c) Wortliste «Kegel/cone/…» trifft auch andere Bauteile mit dem Wort im Namen. | (a) Bei nur zwei Kegeln keine automatische Lage, wenn die Datei nicht schon im Projektbezug liegt. | (a) Die halbe Drehung über die Überdeckung mit dem bestehenden Modell entscheiden (ist in `MitReferenzpunkten` schon gerechnet): gilt, wenn eine Lage ≥ 0.8 und die andere < 0.1 hat. (b) z der Kegel vergleichen und melden. | klein | mittel |
| 7 | `src/Hlks.Kern/Import/IfcAusrichtung.cs:340` (MitReferenzpunkten) | Ist das Einpassen über Wände «eindeutig», werden widersprechende Referenzpunkte nur in den Bericht geschrieben. | Symmetrisches Gebäude: Einpassen kann um 180° daneben liegen und die Kegel wüssten es. | Bei eindeutigem Einpassen und eindeutiger, anderer Lage der Referenzpunkte: «nicht eindeutig» mit beiden Zahlen. | klein | mittel |
| 8 | `src/Hlks.Kern/Import/IfcReferenzpunkte.cs:42` | Ältere Projekte: die Bezugsdatei wird ganz geöffnet, nur um Kegel zu suchen; ist sie unlesbar, scheitert `ifc_pruefen` der neuen Datei. | Wartezeit (grosses Architektur-IFC: rund 30 s) beim ersten Prüfen. | Fehler abfangen (dann «unbekannt»); Kegel beim nächsten Schreiben in `ifcBezug.referenzkegel` nachtragen. | klein | tief |
| 9 | `src/Hlks.Kern/Import/Matrix.cs:17` | RefDirection parallel zur Achse (ungültiges IFC): x wird (0,0,1), y = 0-Vektor. | Entartete Lage einzelner Produkte fehlerhafter Dateien. | In dem Fall wie «ohne RefDirection» behandeln. | klein | tief |
| 10 | `src/Hlks.Kern/Grafik/GrundlagenIfc.cs:20` | Höchstens 3 Netzdateien im Speicher, Schlüssel je Datei **und** Art (Hintergrund/alle). Mit «Darstellung je Unterlage» und mehr als drei Kombinationen wird bei jedem Neuzeichnen neu geladen. | Träger Grundriss bei Projekten mit 4+ IFC bzw. «beide». | Grenze nach Speicher statt Anzahl, oder 6. | klein | mittel |
| 11 | `src/Hlks.Kern/Familien/FamilienGrundbestand.cs:27` | Ein Befehl und ein Paket je Familie, mit Herstellerunterlagen vom NAS. | Neues Projekt dauert mit wachsender Bibliothek spürbar; das Protokoll beginnt mit N Paketen. | Ein Sammelbefehl (ein Paket) bzw. Fortschritt in der Oberfläche; Unterlagen erst beim ersten Gebrauch. | mittel | mittel |
| 12 | `src/Hlks.Kern/Familien/FamilienBefehle.cs` (DateiLaden) | Ein Mensch lädt eine beliebige Datei mit `status: freigegeben` → Familie freigegeben ohne Prüfung und ohne `freigabe {akteur, zeit}`. | Freigabe ohne Nachweis, wer freigegeben hat. | Nur mitgelieferte Dateien und die Bibliothek dürfen «freigegeben» mitbringen; sonst Entwurf. | klein | mittel |
| 13 | `src/Hlks.Kern/Baustelle/betrachter.js:145`, `:189` | (a) Meldungen liegen im Browser unter Projekt + Modellprüfsumme: nach einem neuen Export sind noch nicht gesendete Meldungen des alten Stands unsichtbar. (b) Mit Zugängen verlangt die Seite trotzdem einen eingetippten Namen, obwohl der Server den Namen des Zugangs setzt. | (a) Meldungen können liegen bleiben. (b) unnötiger Schritt für den Monteur. | (a) Beim Start ältere Schlüssel desselben Projekts anzeigen/übernehmen. (b) Server schreibt den Namen als `meta` in die Seite, Feld gesperrt. | klein | mittel |
| 14 | `src/Hlks.Kern/Baustelle/BaustelleExport.cs:147` (box3d) | Der Kasten ist die achsparallele Begrenzung: bei L-förmigen oder schrägen Räumen zeigt der Raumschnitt auch Nachbarräume; Höhe ab Geschosskote (Räume mit eigener Höhenlage, Podeste). | Ungenauer Raumschnitt in solchen Räumen. | Vorerst hinnehmen; später Schnitt am Raumpolygon. | mittel | tief |
| 15 | `src/Hlks.Web/BaustelleZugaenge.cs:58` | Nach dem Entfernen des letzten Zugangs bleibt die Datei (leer) – der Zugang ist dann für alle gesperrt, nicht «offen wie ohne Datei». | Sicher, aber vielleicht unerwartet. | So lassen und in der Oberfläche sagen, oder Datei löschen. | klein | tief |
| 16 | `src/Hlks.Kern/Baustelle/betrachter.js:505` | `decodeURIComponent(location.hash)` wirft bei kaputter Adresse. | Nur `window.kicad` fehlt dann. | try/catch. | klein | tief |

## 4. Offene Fragen an Christian

1. **Nachzeichnen «Nein»**: Soll ein nur eingelesenes Modell bei Abweichung bzw. nicht eindeutigem Einpassen genauso abbrechen wie beim
   Nachzeichnen, und soll es als erste Datei den Projektbezug setzen? (3.1)
2. **Referenzkegel**: Darf bei nur zwei Kegeln die Überdeckung mit dem bestehenden Modell über die halbe Drehung entscheiden, oder bleibt es bei
   «unsicher» bis zum dritten Kegel bzw. Koordinationskörper? Gilt die Toleranz 50 mm (Annahme Hauptlauf) weiter? (3.6)
3. **Zugänge**: Bleibt KICAD Baustelle im Büronetz, oder soll der Monteur von der Baustelle (Internet) zugreifen? Davon hängt 3.3 ab.
4. **Raum in 3D**: Ist die Höhe «bis Oberkante Geschoss» richtig (Korrektur 8), oder soll auch die Decke darunter (Abwasser unter der Decke
   des unteren Geschosses) dazugehören?
5. **Freigegebene Familien aus Dateien** (3.12): Darf ein Mensch eine fremde `.familie.json` mit Status «freigegeben» laden, oder immer Entwurf?
6. **Landeskoordinaten in mm-Dateien** (3.4): Gibt es eine Revit- oder andere mm-Datei mit Kartenbezug zum Nachprüfen?
