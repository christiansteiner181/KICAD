# Testbericht Modul 3b – Kollision und Aussparungen (ENTWURF)

Stand 19.10.2026 · gebaut am 19.10.2026 · **Entwurf, Abnahme durch Christian offen**

Hinweis: Modul 3b wurde auf Christians Auftrag vom 18.10.2026 («mache bis morgen 8:00 ohne abnahmen weiter») ohne Abnahme
von Modul 3 gebaut. Entwurf von hlks-test (sonnet/medium); die Zahlen stammen aus meinen Läufen vom 19.10.2026
(`HLKS_BUILD = …\HLKS_CAD_build\test3bd`) bzw. aus `zuteilung.md` und `entwurf/modul-3b.md` und sind so gekennzeichnet.
Teil B4c (Schnitt/Ansicht, Beschriftungen freistellen) war vor diesem Gesamttest abgeschlossen (Hauptlauf: Gesamttest erst nach
der Übernahme von B4c gestartet). Browser-Reihen habe ich nicht gestartet (C1 und Hauptlauf, siehe Abschnitt 3).

## 1. Kurzfassung

KICAD prüft Leitungen gegen Architektur, Tragwerk und andere HLKS-Elemente (Kapsel/Quader um die Achse, exakte IFC-Netze),
führt Kollisionen als Elemente mit Status über Prüfläufe, schlägt Aussparungen und Kernbohrungen vor, fasst sie zusammen,
nummeriert sie je Geschoss, zeigt sie in Grundriss, Aussparungsplan und 3D und gibt sie als IFC4/IFC2X3 aus. BCF 3.0 wird
geschrieben und gelesen. Der Abgleich mit einer neuen Architekten-Fassung (angekommen/fehlt) ist nur durch Tests belegt.

Alle Projektvorgaben sind **ohne eingebaute Werte**; die Abnahmeläufe benutzen FIKTIVE Werte mit Quelle «FIKTIV». Die
Prüfliste (Abschnitt 7) beginnt deshalb mit den Werten, die nur Christian liefern kann.

Testlauf: Kern 3431 Tests, 3425 bestanden im Erstlauf, 6 Zeitgrenzen-Tests unter Last rot; Wiederholung: alles grün (3 der 6
namentlich belegt, siehe 3). MCP 42/42, Web 148/148, Node 446 bestanden + 10 übersprungen, 0 Fehler.

## 2. «Fertig, wenn …» Punkt für Punkt

Spezifikation: «Für Bernstrasse 42 gibt es eine Kollisionsliste mit Sprung in die Ansichten und Status über zwei Prüfläufe;
die Aussparungen sind vorgeschlagen, zusammengefasst, nummeriert, als Plan, Liste und IFC ausgegeben; eine neue
Architekten-Fassung setzt den Status. Alles über MCP mit Trockenlauf und Rückgängig. BCF folgt nach Christians Entscheid.»

| Kriterium | Nachweis |
| --- | --- |
| Kollisionsliste Bernstrasse 42 (H+K gegen Architektur), mit Sprung in die Ansichten | Abnahmeprojekt `…\HLKS_CAD_build\abnahme3b` (Skript `werkzeuge/abnahme3b_bernstrasse.ps1`, Hauptlauf, Vorgaben **FIKTIV**): **2964 harte Kollisionen** in 32 s, davon 703 Durchdringungen, 417 innerhalb `toleranz_hart` (1 mm). Sprung: C1, Liste 2964, Sprung EG mit Zoom, Hinweis bei fehlendem Grundriss (Hauptlauf, `zuteilung.md`). Referenz B2 (ohne Vorgaben): 3381 hart, 703 Durchdringungen. |
| Status über zwei Prüfläufe | Lauf 2: **1 Änderung** (nur der Zähler `kollisionslauf`), 3/3 bestätigte Kollisionen behalten ihren Status (`zuteilung.md`, D (Teil) und B2b). |
| Aussparungen vorgeschlagen, zusammengefasst, nummeriert | **129 Aussparungen** (B3: 76 Kernbohrungen, 34 Sammelaussparungen), nummeriert, 27 im EG freigegeben; zweiter Vorschlagslauf unverändert (B3, 7.9 s). Geschossfehler bei mehrgeschossigen Wänden im Abnahmelauf gefunden und behoben (1 Test). |
| Plan, Liste, IFC | Aussparungsplan EG A1 1:100 als PDF (Hauptlauf); Liste `aussparungsliste`; IFC4 und IFC2X3 je **129 Proxies** (in der Datei nachgezählt), Rundreise bestanden, Xbim-Validierung ohne Meldung (B4b). BCF: 2964 Topics. |
| Neue Architekten-Fassung setzt den Status | **Nicht mit echten Daten belegt** (keine zweite Fassung mit Durchbrüchen vorhanden). Nur Kern-Tests (B3: angekommen/fehlt, 47 Kern-Tests im Teil). |
| MCP mit Trockenlauf und Rückgängig | Befehle `vorgaben_setzen`, `kollision_pruefen`, `kollision_status`, `aussparungen_vorschlagen`, `aussparung_status`, `aussparungen_nummerieren`, `aussparungen_zusammenfassen`, `aussparungen_abgleichen`; Abfragen `kollisionen`, `aussparungsliste`, `aussparungen_ifc`, `bcf_export`, `bcf_lesen`; `bcf_import` als Befehl. MCP-Reihe 42/42 inkl. der Deckungstests Register ↔ Beschreibungen. Trockenlauf/Rückgängig je Teil in den Kern-Tests der Agenten. |
| BCF | BCF 3.0 (Entscheid Christian 19.10.2026), jede XML gegen die XSD geprüft (B6, 66 Kern- + 2 MCP-Tests). BCF 2.1 lesen offen. |
| **Abnahmelauf mit Christians Vorgaben (19.10.2026, nach der Abnahme-Vorbereitung)** | Vorgaben Christian: Spiel 50 mm je Seite, Zusammenfassen unter 100 mm, toleranz_hart 5 mm, keine Abstandsprüfung, Kernbohrung nur von Hand, Bekleidungen nicht gemeldet, Aussparungen nur in Beton/Mauerwerk. Ergebnis: **2318 harte Kollisionen** (703 Durchdringungen, 1615 Kontakte; 619 innerhalb 5 mm), Prüflauf 3.7 s; zweiter Lauf eine Änderung, bestätigte bleiben; **100 Aussparungen**, alle rechteckig (37 Sammelaussparungen, 4 durchgehend über mehrere Bauteile, **77 mit unbekanntem Material** – das Revit-IFC führt Darstellungsmaterialien, Bauart nur im Typnamen), 19 Durchgänge ohne Aussparung (Ausbau 10, Holz 9); Masse z. B. 130 × 130 (Rohr 30 + 2 × 50); IFC4/IFC2X3 Rundreise bestanden, BCF 2318 Topics, Aussparungsplan EG lesbar. |
| **Abnahmelauf Bernstrasse, neu** | Hauptlauf 19.10.2026 ~07:15 nach allen Korrekturen, Skript `werkzeuge/abnahme3b_bernstrasse.ps1` gegen Server 5151, Projekt und Ausgaben nur lokal `%USERPROFILE%\HLKS_CAD_build\abnahme3b` (Vorgaben FIKTIV: Spiel 20, Grenze Kernbohrung 150, Zusammenfassen 100, toleranz_hart 1 mm). Übertrag H+K-IFC 68 s (3704 Elemente, 5021 Objekte Hintergrund). **Lauf 1:** 2964 harte Kollisionen (703 Durchdringungen, 2261 Kontakte; HLKS↔Architektur 2534, HLKS↔HLKS 430), 417 innerhalb toleranz_hart, 36 s. **Lauf 2 ohne Änderung:** 1 Änderung (Laufzähler), 0 neu/behoben, 3 von 3 von Hand bestätigte bleiben. **Aussparungen:** 129 (34 Sammelaussparungen), nummeriert, je Geschoss 2.UG 36, 1.UG 2, EG 25, 1.OG 22, 2.OG 26, AT 10, DA 8 – Koten über OK Geschoss in jedem Geschoss im Höhenband; 25 EG freigegeben. **Ausgaben:** Aussparungs-IFC IFC4 und IFC2X3 (je 129 Proxies, in der Datei nachgezählt: IFC4 alle PROVISIONFORVOID, IFC2X3 alle ObjectType «ProvisionForVoid»; Rundreise bestanden, Xbim-Validierung 0 Meldungen), BCF 3.0 mit 2964 Topics, Kollisions- und Aussparungsliste als CSV, Aussparungsplan EG A1 1:100 als PDF (Beschriftungen freigestellt mit Führungslinien, keine Kollisionsmarken). **Oberfläche:** Register «Koordination», Kollisionsliste 2964, Sprung in den Grundriss EG mit Zoom, Hinweis bei fehlendem Grundriss (AT) – vom Hauptlauf im Browser geprüft. Der erste Lauf (vor den Korrekturen) liegt als `e_KICAD_lauf1` daneben; er zeigte die Befunde Geschoss mehrgeschossiger Wände und überlappende Beschriftungen. |

Leistung (Spezifikation Entscheid 9, «in Sekunden»): B1 7696 Prüfkörper in 2.9 s, Schmalphase hart 0.32 s parallel; Lauf
Kollisionsprüfung ~32 s; Aussparungen 7.9 s. Alles aus `zuteilung.md`, von mir nicht neu gemessen.

## 3. Testzahlen je Reihe (meine Läufe 19.10.2026, Maschine unter Last, auch durch den Hauptlauf)

| Reihe | gesamt | grün | rot | übersprungen | Einordnung |
| --- | --- | --- | --- | --- | --- |
| Kern `Hlks.Kern.Tests`, Erstlauf (26 min 25 s) | 3431 | 3425 | 6 | 0 | 6 reine Zeitgrenzen (Echt-Tests Bernstrasse/BKW), unten |
| Kern, Wiederholung | 3431 | 3431 | 0 | 0 | siehe unten (Teilläufe) |
| MCP `Hlks.Mcp.Tests` | 42 | 42 | 0 | 0 | die 5 Register-Fehlschläge aus dem Bericht Modul 3 sind behoben |
| Web `Hlks.Web.Tests` | 148 | 148 | 0 | 0 | die 2 Abfragelisten-Fehlschläge aus Modul 3 sind behoben |
| **Total C#** | **3621** | **3621** (nach Wiederholung) | 0 (6 im Erstlauf) | 0 | |
| Node (16 `*.test.mjs`, `node --test`) | 456 | 446 | 0 | **10** | Übersprungen: brauchen `HLKS_TEST_SERVER`/Server («Echter Kern», «Server: …»); nicht ausführbar, da ich keinen Server starte |

Vergleich Modul 3: Kern 3236, MCP 38, Web 148, Node 412 (403 + 9 übersprungen). Neu in 3b: Kern +195, MCP +4, Node +44, übersprungen +1.

**Zeitgrenzen im Erstlauf.** Nach dem Erstlauf habe ich nur die letzten 60 Ausgabezeilen behalten; sichtbar sind drei der
sechs roten Tests, alle `IfcEchtRauchTests.Pruefen_Fassung_Programm_Einheit_Geschosse_Raeume_Lagebericht_Dauer`
(Datei `tests/Hlks.Kern.Tests/IfcEchtRauchTests.cs`, Zeile 103):

| Datei | Meldung im Erstlauf | Wiederholung |
| --- | --- | --- |
| `bernstrasse42/hlks_hk_2026-02-10.ifc` | 213.7 s | grün |
| `bernstrasse42/architekt_3d.ifc` | 142.2 s | grün |
| `bernstrasse42/hlks_hk_2024-09-17.ifc` | 87.6 s | grün |

Die übrigen drei Fehlschläge des Erstlaufs sind **nicht namentlich belegt**. Zur Eingrenzung habe ich zwei Teilläufe
wiederholt: (a) die Echt-Gruppe (`IfcHlksEchtTests`, `RaumgrenzenEchtTests`, `IfcEchtRauchTests`, 42 Tests, 14 min 22 s):
41 grün, 1 rot (`RaumgrenzenEchtTests.Bkw_AlleRaumgrenzen_Dauer_Summen`, Zeile 138, Zeitgrenze); dieser Test einzeln
(1 min 29 s): **grün**. (b) alle übrigen Kern-Tests (3389, 8 min 42 s): **alle grün**. Die drei nicht gesehenen Fehlschläge
liegen damit sehr wahrscheinlich in der Echt-Gruppe (im Modul-3-Lauf: `IfcHlksEchtTests` ×2 und `Bkw_AlleRaumgrenzen`),
das ist aber eine Schlussfolgerung, keine Beobachtung. Alle roten Tests sind **«unter Last»** (einzeln bzw. im
Teillauf grün), kein einziger blieb einzeln rot. Kein Befund am Produktcode; die inhaltlichen Prüfungen (Zahlen, Klassen)
sind nicht gescheitert (Meldung ist jeweils die Dauer).

### Browser-Reihen (nicht von mir gestartet; Zahlen aus `zuteilung.md`)

| Teil | neu | Stand |
| --- | --- | --- |
| C1 Koordination (Register, Palette, Dialoge, BCF, IFC für Architekt) | 117 Browser + 28 Node | alle Browser-Reihen ausser `hkap` (Port 5098 tabu) grün (Agentenbericht) |
| `familie-unterlagen.browser.mjs` | – | flackert (Wartezeit), Befund fremd zu 3b |
| B4a/B4c | – | B4a 16 Node; B4c 30 Kern-Tests (Filter Grafik/Schnitt/Aussparung/Tür/Plan/Schema 811/811) |

Ich habe keine Browser-Reihe wiederholt und kann sie nicht bestätigen.

## 4. Referenzfälle von Hand (Teil B5, hlks-test)

27 Fälle, alle grün, keine Abweichung Code ↔ Handrechnung: 11 Kollision (hart, weich, Fang der Kennung), 16 Aussparung
(Grösse senkrecht, schräg, Kanal, Zusammenfassen, Koten, Nummern, BCF-Topic-UUID v5, mit eigenem Skript nachgerechnet).
Stichproben des Hauptlaufs: F09 317.8/219.0, F10 540 × 340, F11 289 × 95. Die Fälle laufen im Kern-Total mit (grün).
Die Werte der Fälle sind **FIKTIV** (Vorgaben nicht festgelegt). Zwei Abweichungen vom Datenmodell sind Annahmen und stehen
auf der Prüfliste: Mass weich = Unterschreitung der Vorgabe (F02, F03), Aufrunden auf 0.1 mm (F09).

## 5. Befunde und ihre Behebung

| # | Befund | Fund | Behebung |
| --- | --- | --- | --- |
| 1 | Netzdatei ohne Öffnungen, Teile doppelt geprüft, Regel «unverändert» doppelt (B1) | B1 | Netzdatei Fassung 2 (B1b), Hauptlauf-Entscheid |
| 2 | Lage: H+K-IFC und Archicad-IFC 31 m auseinander | B1 | Bezug setzen; Revit-Datei als Architektur benutzt |
| 3 | 498 von 2888 harten Befunden höchstens 1 mm tief (aufliegende Leitungen) | B1b | Projektvorgabe `toleranz_hart` |
| 4 | Netzdatei-Schlüssel ohne Fassung | B1b | Hauptlauf, 154/154 |
| 5 | Jeder Lauf schreibt alle Kollisionen neu; Hüllen-Nachführung bei jeder Änderung der Projektinformation | B2 | B2b: `pruefung` = erster Fund, Zähler `kollisionslauf`; `HuelleNachfuehren` nur bei geändertem `uebergang` |
| 6 | Revit-Export nicht wasserdicht (383 → 703 Durchdringungen mit Regel für offene Netze) | B2 | Regel `OffeneQuerungen` |
| 7 | Aussparungen bis 3.6 m in dünnen Platten | B3 | Regel «längs im Bauteil bzw. flacher als 30° → kein Vorschlag» |
| 8 | `element_loeschen` blockiert bei Aussparungsbezug | B3 | Hauptlauf: Bezug lösen, veraltet (2 Tests) |
| 9 | Aussparungen fehlen in Schnitt/Ansicht (NichtSchneiden) | B4a | Nachtrag B4c – erledigt (Symbol in Schnitt/Ansicht, 30 Tests) |
| 10 | Plan-PDF-Pfad in Tür-/API-Tests (Feldreihenfolge) | B6 | Hauptlauf (83/83, 62/62) |
| 11 | Kollisionsmarken überdecken den Aussparungsplan | C1 | Hauptlauf: keine Marken in `darstellung aussparungen` (1 Test) |
| 12 | Beschriftungen dicht liegender Aussparungen überlappen | D | B4c-Zusatz (Freistellung mit Führungslinie) – erledigt, am Plan Bernstrasse EG geprüft |
| 13 | Aussparungen mehrgeschossiger Revit-Wände im falschen Geschoss (A-EG-… auf +5.47) | D | Hauptlauf: Geschoss aus der Höhe für Wand/Stütze/Träger (1 Test, 190/190); vergebene Nummern bleiben |
| 14 | Rund 3000 Kollisionskugeln in 3D können den Aufbau bremsen | B4a | Hinweis, `klassenAusblenden: Kollision` |
| 15 | `familie-unterlagen.browser.mjs` flackert | C1 | offen, fremd zu 3b |

Aus meinem Gesamtlauf: **keine neuen Befunde am Produktcode**; keine Tests mit `Skip` ergänzt, keine Tests geändert.

## 6. Zuteilung und Modellwahl je Teil (aus `zuteilung.md`)

| Teil | Inhalt | Agent | Ergebnis |
| --- | --- | --- | --- |
| A | Spezifikation, Katalog, Einhängepunkte, P19/P20 | Hauptlauf | ok |
| B1 | Geometrie-Kern | hlks-kern | ok, 57 Tests, keine Nacharbeit |
| B1b | Netzdatei Fassung 2 | hlks-kern | ok, 22 + 1 Echt-Tests, keine Nacharbeit |
| B2 | Kollisionsprüfung | hlks-kern | ok, 28 Kern- + 1 MCP-Test, keine Nacharbeit |
| B2b | `pruefung`/Zähler | hlks-kern | ok, 2 neue + 4 geänderte Tests, keine Nacharbeit |
| B3 | Aussparungen | hlks-kern | ok, 47 Kern- + 1 MCP-Test, keine Nacharbeit |
| B4a | Darstellung, Listen | hlks-modul | ok, 36 C#- + 16 Node-Tests, keine Nacharbeit |
| B4b | IFC-Ausgabe | hlks-modul | ok, 21 Kern- + 2 MCP-Tests, keine Nacharbeit |
| B4c | Schnitt/Ansicht, Freistellen | hlks-modul | ok, keine Nacharbeit |
| B6 | BCF 3.0 | hlks-kern | ok, 66 Kern- + 2 MCP-Tests, keine Nacharbeit |
| C1 | Oberfläche Koordination | hlks-modul | ok, 28 Node + 117 Browser, keine Nacharbeit |
| B5 | Referenzfälle | hlks-test | ok, 27 Fälle, kein Befund, keine Nacharbeit |
| D (Teil) | Abnahmeprojekt Bernstrasse | Hauptlauf | ok, Befunde 12 und 13 |
| D | Gesamttest, Entwurf Testbericht | hlks-test | dieser Bericht |

Nacharbeit bei Agentenaufträgen: keine. Die Befunde 5, 8, 10, 11, 13 behob der Hauptlauf selbst (Lücken in Spezifikation bzw.
Hauptlauf-Code, keine Agentenfehler). Vorschlag nach der Regel «dreimal ohne Nacharbeit → probeweise tiefer»: für hlks-kern
(B1, B1b, B2, B2b, B3, B6) wie in Modul 3 **keine Änderung** (Geometrie, Statuslogik, Normnähe sind fachlich teuer);
hlks-modul (B4a, B4b, C1) und hlks-test (B5) bleiben. Entscheid beim Hauptlauf.

## 7. Prüfliste für Christian

Bitte mit Ja/Nein oder Wert beantworten. Alle Werte der Testläufe sind FIKTIV.

### A. Projektvorgaben (zuerst; ohne Wert bleibt die Prüfung unvollständig)

Die Vorgaben sind ein Element `VG-1` mit je einem Wert **und Quelle** (z. B. «Bürostandard H+K 2026»). Im Code stehen keine Werte.

1. **Spiel um Leitungen bei Aussparungen** (mm je Seite). Wert und Quelle?
2. **Abstand weich je Gewerkpaar** (mm): Lüftung–Lüftung, Lüftung–Heizung/Kälte, Lüftung–Sanitär, Heizung–Sanitär, HLKS–Architektur/Tragwerk usw. Welche Paare, welche Werte, welche Quelle? Ohne Wert entfällt die weiche Prüfung für das Paar.
3. **Grenze Kernbohrung / Aussparung** (Durchmesser mm, bis dahin Kernbohrung). Wert und Quelle?
4. **Abstand zum Zusammenfassen** benachbarter Aussparungen im selben Bauteil (Randabstand mm). Wert und Quelle?
5. **Mindestabstand Leitung–Leitung** (mm). Wert und Quelle?
6. **`toleranz_hart`** (mm; Durchdringungen bis dahin = Berührung, z. B. aufliegende Leitungen). Im Abnahmelauf FIKTIV 1 mm: 417 von 2964 harten Befunden liegen darunter. Wert und Quelle?

### B. Annahmen aus Spezifikation und Agentenberichten

7. **Statusfarben im Grundriss:** vorgeschlagen orange, freigegeben/versandt blau, angekommen/ausgeführt grün, veraltet/fehlt magenta. Passt das?
8. **Symbolgrössen und Schrift** (Kollisionsmarke Kreis mit Kreuz, 3D Kugel; Aussparung Projektion mit Kreuz; Beschriftung im Aussparungsplan). Gefällt die Darstellung, Änderungswünsche?
9. **Mass einer weichen Kollision** = Unterschreitung der Vorgabe (Vorgabe − Abstand der Dämmhüllen), nicht der Abstand selbst. Einverstanden? (Abweichung vom Datenmodell; hart = Eindringtiefe als kürzester Weg zum Herausschieben.)
10. **Aufrunden der Aussparungsmasse auf 0.1 mm.** Richtig, oder auf 5/10 mm?
11. **Kernbohrungen auf Bohrkronenreihe:** Kernbohrungen erscheinen mit dem gerechneten Durchmesser (z. B. Ø 70.5). Sollen sie auf eine Reihe von Bohrkronen und Rechtecke auf 5/10 mm aufgerundet werden? **Reihe und Quelle müsste Christian liefern.**
12. **Geschoss bei mehrgeschossigen Wänden** (Revit-Wände über mehrere Geschosse): Geschoss aus der Höhe der Aussparungsmitte, wenn diese nicht im Band des Bauteil-Geschosses liegt (Wand, Stütze, Träger); Decke/Dach: Aussparung im Geschoss darüber bzw. des Bauteils. Bereits vergebene Nummern bleiben. Einverstanden?
13. **Bekleidungen (IfcCovering):** lösen keine Aussparung aus (Kontakt, nicht Durchdringung; ebenso Geländer, Treppe, Fenster, Tür). Richtig?
14. **Zusammenfassen über Bauteilgrenzen:** heute nur je Bauteil. Revit-Schichten als eigene Wände ergeben Aussparungen nebeneinander. Über Bauteilgrenzen zusammenfassen: ja/nein?
15. **Schräge Querung:** Kreis mit Ø = D/cos θ + t · tan θ + 2 × Spiel (z. B. DN 50 unter 45° durch 200 mm → Ø 317.8, Referenzfall F09). Alternativ schräge Kernbohrung bzw. Rechteck statt grossem Kreis. Welche Variante?
16. **Keine Aussparung bei Leitungen längs im Bauteil oder flacher als 30°.** Grenze richtig?
17. **BCF-Koordinaten:** Vorgabe `projekt` oder `ifc` (IFC-Welt)? Die Aussparungs-IFC-Ausgabe benutzt mit Projektbezug `ifc` die IFC-Koordinaten, sonst `projekt` mit Warnung. Welche Vorgabe?
18. **BCF-Annahmen:** Topic-Guid = UUID v5 aus Projekt-Id und Kollisionskennung; TopicType «Kollision hart/weich», Priorität hart «Hoch»; Kamera schräg von oben aus Südwest, Abstand 10 × Mass (3–30 m); Statusabbildung gemäss Tabelle in `BcfFormat.cs`. Einverstanden? Titel, Prioritäten und Zuweisungen des Partners werden nicht übernommen: ausreichend?
19. **BCF 2.1 lesen:** nötig (Schemas fehlen bisher)? Oder nur BCF 3.0?
20. **`Pset_ProvisionForVoid`:** Das Standard-Pset wird erst nach Prüfung an der buildingSMART-Definition ergänzt; ausgegeben wird bis dahin das Pset `KICAD_Aussparung`. Einverstanden?
21. **Geschoss-GlobalId:** Aussparungen im IFC hängen an Geschossen mit eigener GlobalId (nicht der des Architekten). Oder soll die des Architekten übernommen werden?
22. **Register-Lage und Palette:** Koordination als eigenes Register (Vorgaben, Kollisionen, Aussparungen, BCF; 11 Knöpfe) und angedockte Palette Kollisionen/Aussparungen. Passt die Anordnung?
23. **Kollisionsmarken nicht im Aussparungsplan** (und nie im PDF). Richtig?
24. **Kollisionen als Elemente im Modell**, «behoben» statt löschen; Status über Rechner hinweg. Einverstanden?
25. **Durchdringung mit zugeordneter Aussparung ist keine Kollision.** Einverstanden?
26. **Kennung:** Kollisionen werden über Beteiligte und auf 50 mm gefangene Lage wiedererkannt (Spezifikation nennt 100 mm; B2 benutzt 50 mm). Reicht das?
27. **Aussparungsnummer** «A-<Geschoss>-<laufend>», sortiert nach x, y, z, neu vergeben nur auf Befehl. Passt das Nummernschema?
28. **IFC-Ausgabe nur freigegeben und versandt** (Vorgabe-Filter); «versandt» setzt nur `aussparung_status`. Richtig?
29. **Aussparungen anderer Planer** (IfcBuildingElementProxy PROVISIONFORVOID aus fremden IFC) werden im Import nicht als Aussparungen übernommen (bleibt bei Modul 7). Zurückstellen in Ordnung?
30. **Rund 3000 Kollisionskugeln in 3D** (Bernstrasse 2964 hart): Standard sichtbar oder ausgeblendet?
31. **Bernstrasse mit Revit-Architektur:** Die Revit-Datei ist deckungsgleich mit dem H+K-IFC, das Archicad-IFC liegt 31 m versetzt. Welche Datei gilt als Architektur-Referenz?

## 8. Offene Punkte

| # | Punkt | Wer / wann |
| --- | --- | --- |
| 1 | ~~Abnahmelauf Bernstrasse neu~~ – erledigt (Abschnitt 2) | Hauptlauf |
| 2 | ~~B4c Schnitt/Ansicht und Beschriftungen~~ – erledigt | hlks-modul |
| 3 | Abgleich «angekommen/fehlt» mit echter Architekten-Fassung nicht prüfbar (keine zweite Fassung mit Durchbrüchen) | Christian, neue Fassung |
| 4 | Projektvorgaben (Prüfliste A) | Christian |
| 5 | BCF: 2.1 lesen, Titel/Prioritäten/Zuweisungen des Partners, Koordinatenvorgabe | Christian / Hauptlauf |
| 6 | `Pset_ProvisionForVoid`, Geschoss-GlobalId | Hauptlauf nach Prüfung |
| 7 | Leistung auf grossen Modellen: rund 3000 Kollisionsmarken in 3D | Hauptlauf |
| 8 | `hkap.browser` seit C1 nicht gelaufen (braucht die Testkopie 5098); `familie-unterlagen.browser` vom Hauptlauf entflackert (149/149) | Hauptlauf |
| 9 | Zeitgrenzen-Tests der Echt-Gruppe sind lastabhängig; Messung auf ruhiger Maschine fehlt | Hauptlauf |

## Bekannte Grenzen dieser Prüfung

- Browser-Reihen habe ich nicht gestartet; die 10 übersprungenen Node-Tests brauchen einen Server.
- Drei der sechs Erstlauf-Fehlschläge im Kern sind nicht namentlich belegt (Ausgabe gekürzt); Einordnung als Zeitgrenze durch Teilläufe (Echt-Gruppe, Rest) abgeleitet.
- Bernstrasse-Zahlen sind aus `zuteilung.md`, nicht von mir neu gerechnet; Abnahmeprojekt `abnahme3b` nicht von mir geöffnet (die Zeile «Abnahmelauf Bernstrasse, neu» in Abschnitt 2 ist vom Hauptlauf).
- Normwerte habe ich weder geprüft noch eingetragen; alle Vorgabewerte der Tests sind FIKTIV.
- Der Abgleich mit einer neuen Architekten-Fassung ist nur durch Tests belegt.
