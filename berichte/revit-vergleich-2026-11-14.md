# Funktionsmatrix KICAD – Revit MEP (Stand 14.11.2026)

Auftrag Christian 14.11.2026: «wie nah ist der funktionsumfang an revit?» → «Ja, Matrix erstellen».

**Vorgehen:** 123 Revit-MEP-Funktionen in elf Gebieten, je Funktion im Code von `main` (Stand `geprueft-20261114-1444`) gesucht.
Inventar durch zwei lesende Agenten (Explore, sonnet), Stichproben durch den Hauptlauf (Kühllast, Ventilauslegung, Revisionswolke,
tote Knöpfe, Gruppen, Deckenspiegel, Parallelleitungen, Geschoss anlegen, Anzahl MCP-Werkzeuge – alle bestätigt).
«Vorhanden» heisst: Befehl im Kern und Bedienung vorhanden, **nicht** im Praxisbetrieb erprobt. `entwurf/bearbeitungswerkzeuge.md`
(03.11.2026) ist in vielen Punkten überholt und wurde nur als Wegweiser benutzt.

Abkürzungen: K = `src/Hlks.Kern`, J = `src/Hlks.Web/wwwroot/js`, O = `src/Hlks.Web/OBERFLAECHE.md`, KL = `kataloge/klassen.json`.

## Ergebnis

| | Vorhanden | Teilweise | Fehlt | Abdeckung¹ |
|---|---:|---:|---:|---:|
| A Modellieren MEP (19) | 11 | 6 | 2 | 74 % |
| B Bearbeiten (19) | 13 | 5 | 1 | 82 % |
| C Architektur/Grundlagen (12) | 5 | 6 | 1 | 67 % |
| D Ansichten (16) | 8 | 5 | 3 | 66 % |
| E Familien (10) | 6 | 1 | 3 | 65 % |
| F Dokumentation (14) | 7 | 7 | 0 | 75 % |
| G Berechnungen (12) | 4 | 4 | 4 | 50 % |
| H Koordination/Austausch (8) | 5 | 2 | 1 | 75 % |
| I Zusammenarbeit/Projekt (6) | 2 | 3 | 1 | 58 % |
| J Erweiterung/Sonstiges (7) | 4 | 2 | 1 | 71 % |
| **Total (123)** | **65** | **41** | **17** | **70 %** |

¹ Vorhanden = 1, teilweise = ½, fehlt = 0; alle Funktionen gleich gewichtet. Die Zahl misst die **Breite**, nicht Reife, Tempo oder Bedienkomfort.

**Lesart:** In der Breite steht KICAD bei rund 70 % von Revit MEP. Der Kern ist dabei weiter als die Oberfläche: Fast jede Funktion ist ein
Kernbefehl mit Trockenlauf, Rückgängig und Herkunft (auch über KI/MCP nutzbar); die Lücken liegen meist in der Bedienung. Nicht in der Zahl
enthalten ist, was Revit gar nicht hat (siehe unten). Grösster Abstand bleibt die **Reife**: kein Projekt ist bisher vollständig in KICAD
geplant worden.

## Wo KICAD über Revit hinausgeht

| Funktion | Fundstelle |
|---|---|
| Rechnen nach CH-Normen mit Quelle je Wert (SN 592000, SVGW W3, SIA 384.201), Register «Wie wird gerechnet» | `kataloge/rechenregister.json`, `kataloge/grundlagen.json`, O «Wie wird gerechnet» |
| Sanitär-Dimensionierung und Belastungswerte am gezeichneten Netz | K/Rechnen/SanitaerDimensionierung.cs, SanitaerBelastung.cs |
| Automatische Aussparungen mit Abgleich gegen das Architekten-IFC, Aussparungspläne | K/Aussparungen/AussparungVorschlag.cs, K/Grafik/AussparungGrafik.cs |
| Kollisionsprüfung und BCF 3.0 ohne Navisworks | K/Kollision/Kollisionspruefung.cs, BcfExport.cs |
| KI-Palette und MCP-Server mit über 220 Werkzeugen, Schutzregeln, Freigaben beim Menschen | src/Hlks.Mcp, K/Sicherheit/Ki*.cs |
| Jeder Befehl mit Trockenlauf und Herkunft («Warum»), Paket-Rückgängig | K/Api/Befehle.cs, K/Projekt.cs |
| XR-Begehung mit Quest, Messen, Problem-Marker | J/ansichten/vr3d.js, xr-*.js; HK_XR |
| Baustellen-Webseite mit Rollen, Rückmeldungen, Montagestatus | K/Baustelle/BaustelleExport.cs |
| HKAP-Abgleich (Räume, Anforderungen, Stränge, Befunde) | K/Austausch/Hkap*.cs |
| RVT direkt lesen (als Unterlage, nach IFC umgesetzt) | K/Unterlagen/RevitDatei.cs |
| Familien aus Herstellerunterlagen (PDF/Bild/IFC/STEP) mit Quellenbezug und Massvergleich | K/Familien/FamilienQuellen.cs, FamilienMasse.cs |
| PDF-Prüfung mit Markierungen (Wolke, Pfeil, Text, Haken; offen/erledigt) | J/ansichten/pruefung.js |

## Matrix

### A Modellieren MEP

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 1 | Rohr zeichnen | vorhanden | J/oberflaeche/aktionen-leitung.js:33; K/Leitungen/RoutingBefehle.cs:360 | Bögen/Reduktionen aus der Produktlinie, Abwasser- und Fallleitung |
| 2 | Kanal zeichnen rund/rechteckig | vorhanden | aktionen-leitung.js:34; K/Leitungen/Leitungsserie.cs, FreieGroesse.cs | Freie Grösse mit Grenzen und Schritt |
| 3 | Kabeltrasse/Leerrohr Elektro | **fehlt** | – | «elektro» nur als Gewerk/Koordinationsfarbe |
| 4 | Flexible Rohre/Kanäle | teilweise | K/Import/IfcHlks.cs:149 | Nur aus IFC gelesen; kein Zeichenwerkzeug |
| 5 | Formteile automatisch | teilweise | K/Leitungen/Routing.cs, FormteilTauschen.cs, EckeDehnen.cs | Bogen, Reduktion, T-Stück (+/−) ja; Übergang rund↔eckig, Kreuz, Hosenstück nicht automatisch |
| 6 | Routing-Präferenzen je Dimension | teilweise | Leitungsserie.cs; J/oberflaeche/aktionen-systeme.js:16 | Produktlinie je Grösse und «ab DN» je System; kein Präferenzdialog |
| 7 | Gefälle | vorhanden | K/Leitungen/GefaelleHoehe.cs:17 | Beim Zeichnen und nachträglich für den Zug |
| 8 | Dämmung | vorhanden | aktionen-leitung.js:147–155; K/Grafik/DaemmGrafik.cs | Dicke nach MuKEn aus DN |
| 9 | Systeme, Systembrowser | teilweise | aktionen-systeme.js; K/Systeme/Mustersysteme.cs | Systemtypen, Anlagen, VL/RL, Mustersysteme; Systembrowser nur über Projektbrowser/Prüfbefunde |
| 10 | Verbinden/Trennen | vorhanden | K/Api/Befehle.cs:623/725; J/oberflaeche/aktionen-verbinden.js | Grundriss (Kontextmenü, Griff) und Schema |
| 11 | Parallelleitungen | vorhanden | K/Leitungen/ParalleleLeitungen.cs:18 | Anzahl, Abstand, Lage, Gehrung |
| 12 | Automatisches Routing | **fehlt** | entwurf/vorhaben-erzeugendes-cad.md:163 | Nur Konzept |
| 13 | Höhenversatz/Steigleitung | vorhanden | J/werkzeuge/leitung.js (Taste H); K/Leitungen/Fallleitung.cs | |
| 14 | Armaturen | teilweise | J/familien/platzieren.js; K/Leitungen/LeitungEinsetzen.cs:94 | Setzen in die Leitung über «Familie platzieren»; Knopf «Armatur» (PA) ist gesperrt |
| 15 | Kanalzubehör (Klappen) | vorhanden | KL (Brandschutzklappe, Luftklappe, Schalldämpfer, VSR) | Über «Familie platzieren», teilt die Gerade |
| 16 | Luftauslässe | vorhanden | KL (Zu-/Abluftdurchlass) | Ohne eigene Schaltfläche und ohne Auslassauslegung |
| 17 | Geräte platzieren | vorhanden | K/Familien/FamilienBefehle.cs:454; aktionen-familien.js:90 | Knopf «Sanitärapparat» (PX) gesperrt |
| 18 | Hosting (Wirt) | teilweise | FamilienBefehle.cs:144 (`an_wand`); K/Architektur/WandBefehle.cs:110 | Nur Wand/Vorwand; nicht an Decke/Boden, kein Wirtwechsel |
| 19 | Leitung an Geräteanschluss | vorhanden | K/Api/Befehle.cs:750 (`leitung_ziehen`); J/werkzeuge/griffe.js | Anschlussfang; ohne Wegsuche (Nr. 12) |

### B Bearbeiten

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 20 | Verschieben MV | vorhanden | K/Architektur/Bearbeiten.cs:77 | Grundriss, Schnitt, Ansicht; Leitungen werden nachgeführt |
| 21 | Kopieren CO | vorhanden | Bearbeiten.cs:112 | Mit Anzahl, Zwischenablage |
| 22 | Drehen RO | vorhanden | Bearbeiten.cs:184 | |
| 23 | Spiegeln MM/DM | vorhanden | Bearbeiten.cs:229 | Beide Revit-Varianten |
| 24 | Reihe/Array | teilweise | J/werkzeuge/aendern.js:54 | Nur lineare Mehrfachkopie, nicht radial, nicht nachträglich änderbar |
| 25 | Ausrichten AL | vorhanden | K/Architektur/AusrichtenVersatz.cs:30 | Ohne Schloss; nur Grundriss |
| 26 | Versatz OF | vorhanden | AusrichtenVersatz.cs:67 | Nur Grundriss |
| 27 | Stutzen/Dehnen TR | vorhanden | K/Leitungen/EckeDehnen.cs:83/154 | Fachlich: Ecke → Bogen, Dehnen auf Leitung → T-Stück; nur Grundriss |
| 28 | Teilen SL | vorhanden | LeitungEinsetzen.cs:73 | Nur Leitungen, ohne Lücke |
| 29 | Gruppen | **fehlt** | – | Auch Fixieren (Pin) fehlt |
| 30 | Auswahlfilter, Mehrfachauswahl | vorhanden | J/oberflaeche/aktionen-auswahl.js:55/60; K/Api/Mehrfach.cs | Fenster/Kreuzen, alle Exemplare, Tab = Leitungszug |
| 31 | Griffe | teilweise | J/werkzeuge/griffe.js | Endpunkte an Wand, Achse, Rohr, Kanal, Tür; nicht an Geräten/Formteilen, kein Drehgriff |
| 32 | Temporäre Bemassung | vorhanden | J/werkzeuge/tempmass.js | Klick auf Zahl verschiebt |
| 33 | Objektfang | vorhanden | J/werkzeuge/fangen.js | Ohne Zentrum/Quadrant/Tangente und Einmal-Fang |
| 34 | Arbeitsebene | teilweise² | J/werkzeuge/leitung.js (Höhenfeld) | Nur Geschoss + Höhe; keine einstellbare Arbeitsebene |
| 35 | Bearbeiten in 3D | teilweise | J/ansichten/ansicht3d.js; xr-greifen.js | In 3D nur Auswählen, Eigenschaften; Verschieben nur in VR |
| 36 | Bearbeiten im Schnitt | teilweise | J/oberflaeche/aktionen-zeichnen.js:34 | MV/CO/RO/MM und Beschriften ja; Zeichnen, AL, OF, TR, SL nur im Grundriss |
| 37 | Rückgängig/Wiederholen | vorhanden | aktionen-bearbeiten.js:69/78 | Ohne Schrittliste |
| 38 | Eigenschaftenpalette | vorhanden | J/oberflaeche/eigenschaften.js; O «Eigenschaftenpalette» | Mit «Warum»; kein Eigenschaften-Übertragen (MA) |

² Im Inventar als «fehlt»; als teilweise gezählt, weil das Höhenfeld den häufigsten Fall abdeckt.

### C Architektur und Grundlagen

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 39 | Wand | vorhanden | K/Architektur/WandBefehle.cs:12 | Dicke, Höhe, Bezugslinie, Aufbau |
| 40 | Decke/Boden | vorhanden | K/Architektur/DeckeBefehle.cs:137 | Aus den Wänden erzeugt, nicht frei skizziert, ohne Löcher |
| 41 | Dach | teilweise | DeckeBefehle.cs | Nur flach; geneigt nur aus IFC |
| 42 | Tür | vorhanden | WandBefehle.cs:110; K/Api/Tuer.cs | |
| 43 | Fenster | vorhanden | WandBefehle.cs:110; K/Huelle/FensterBefehle.cs | |
| 44 | Raum mit Begrenzung | vorhanden | WandBefehle.cs:200; K/Huelle/Raumgrenzen.cs | Mit Angrenzung für die Heizlast |
| 45 | Treppe | teilweise | K/Architektur/Treppenlage.cs | Nur aus IFC |
| 46 | Stütze | teilweise | K/Import/IfcArchitektur.cs:780 | Nur aus IFC |
| 47 | Achsraster | teilweise | aktionen-zeichnen.js:55 (GR) | Einzelachsen; kein Rasterwerkzeug |
| 48 | Geschosse | teilweise | KL Geschoss; Projektbrowser | **Kein Werkzeug «Geschoss anlegen» in der Oberfläche** – Meldungen verlangen es, angelegt wird nur über IFC, KI oder `element_anlegen` |
| 49 | Verknüpfte Modelle | teilweise | J/oberflaeche/aktionen-einfuegen.js:504/508 | IFC/RVT übernommen und ausgerichtet; keine Live-Verknüpfung |
| 50 | Kopieren/Überwachen | **fehlt** | – | Ersatz: Abgleich über GlobalId, Aussparungsabgleich, BCF |

### D Ansichten

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 51 | Grundriss | vorhanden | J/ansichten/grundriss.js | Dazu Einlage- und Grundleitungsplan |
| 52 | Deckenspiegel | **fehlt** | – | Ansichtsarten in KL ohne Deckenspiegel |
| 53 | Schnitt | vorhanden | J/werkzeuge/schnitt.js; K/Grafik/Schnittgrafik.cs | Auch abgesetzt, SIA-Schraffuren |
| 54 | Ansicht (Fassade) | teilweise | KL Ansicht.art «ansicht»; O «Offen: Ansicht (Fassade)» | Kern kennt sie, kein Werkzeug zum Anlegen |
| 55 | 3D | vorhanden | J/ansichten/ansicht3d.js | Mit Begehen und VR |
| 56 | Schnittbereich | vorhanden | J/ansichten/schnittbereich3d.js | Mit Griffen |
| 57 | Ansichtsbereich | teilweise | KL Ansicht.schnitthoehe, tiefe | Schnitthöhe (auch nach SIA 400) und Tiefe; ohne Unter-/Obergrenze |
| 58 | Sichtbarkeit/Grafiken | teilweise | aktionen-ansicht.js:286 (VV) | Klassen und Gewerke ein/aus; keine Linien-/Farb-/Halbton-Übersteuerung |
| 59 | Regelbasierte Filter | **fehlt** | – | Nur Klassen-/Gewerkefilter |
| 60 | Ansichtsvorlagen | vorhanden | K/Plan/Ansichtsvorlagen.cs | Verknüpft |
| 61 | Detailausschnitt/Callout | teilweise | KL Ansicht.ausschnitt | Ausschnitt und Duplizieren; kein Callout mit Verweissymbol |
| 62 | Abhängige Ansichten | **fehlt** | – | |
| 63 | Zeichnungsansicht | teilweise | K/Zeichnung/ZeichnungDatei.cs; J/zeichnung/zeichnung.js | Eigenes 2D-CAD für Dateien; keine leere Zeichnungsansicht im Projekt |
| 64 | Strang-/Prinzipschema | vorhanden | K/Schema/SchemaBefehle.cs:204; J/ansichten/schema.js | Automatische Lage, auf Planblatt |
| 65 | Detailgrad grob/mittel/fein | vorhanden | KL Ansicht.detailgrad | |
| 66 | Projektbrowser | vorhanden | J/oberflaeche/projektbrowser.js | Umschaltbare Gliederung |

### E Familien

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 67 | Familieneditor | vorhanden | J/familien/editor*.js; O «Familien» | Parameter, Typen, Körper, Anschlüsse, Sinnbild |
| 68 | Typ-/Exemplarparameter | vorhanden | K/Familien/FamilienDefinition.cs:51 | |
| 69 | Formeln | vorhanden | K/Familien/FamilienFormel.cs | Mit Zirkelprüfung |
| 70 | Geometrische Abhängigkeiten | **fehlt** | – | Parametrik nur über Formeln |
| 71 | Verschachtelte Familien | **fehlt** | entwurf/modul-2.md:238 («später») | |
| 72 | Anschlüsse mit Medium/Dimension | vorhanden | FamilienDefinition.cs (`AnschluesseLesen`) | |
| 73 | Typenkataloge | vorhanden | K/Leitungen/Leitungsserie.cs | Leitungsserien mit allen Grössen und Formteilen |
| 74 | Gemeinsame/Projektparameter | **fehlt** | – | Eigenschaften nur je Klasse aus dem Katalog |
| 75 | Familien aus Herstellerunterlagen | vorhanden | K/Familien/FamilienQuellen.cs, FamilienNetz.cs | Freigabe nur durch Menschen |
| 76 | Bibliothek/Inhalte | teilweise | K/Familien/FamilienBibliothekDienst.cs | Bibliothek auf der NAS; Umfang weit unter Revit-Herstellerinhalten |

### F Dokumentation

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 77 | Planblätter | vorhanden | K/Plan/PlanBefehle.cs | Plansätze, Plannummer |
| 78 | Plankopf | vorhanden | K/Plan/Plankopf.cs | H+K nach SIA 400 |
| 79 | Bemassung, Höhenkote | vorhanden | K/Grafik/BeschriftungMass.cs | |
| 80 | Beschriftung/Tags | teilweise | K/Grafik/BeschriftungGrafik.cs, Leitungsbezeichnung.cs | Keine Tag-Familien mit frei wählbarem Inhalt |
| 81 | Automatische Beschriftung | teilweise | Leitungsbezeichnung.cs; K/Aussparungen/AussparungKennzeichnung.cs | Kein «Alle beschriften» |
| 82 | Text | vorhanden | O Register Beschriften | |
| 83 | Legenden | teilweise | K/Grafik/AussparungGrafik.cs:29 | Nur im Aussparungs-/Koordinationsplan |
| 84 | Bauteillisten | teilweise | K/Plan/Listentabelle.cs; J/ansichten/liste.js | Feste Listenarten mit Filter, Sortierung, CSV, Blatt; keine freien Felder/Gruppen/Formeln |
| 85 | Revisionen | teilweise | K/Plan/Revisionen.cs | Index und Tabelle; keine Revisionswolken auf Plänen |
| 86 | 2D-Linien, Füllbereiche | teilweise | K/Grafik/Schraffuren.cs | Keine 2D-Detailbauteil-Familien |
| 87 | PDF | vorhanden | K/Plan/Planausgabe.cs, Plansatzausgabe.cs | Vektor, Serien-PDF |
| 88 | DWG-Export | teilweise | K/Zeichnung/ZeichnungBearbeiten.cs:5 | Nur das 2D-CAD; **kein Export Plan/Modell → DWG** |
| 89 | DWG-Import | vorhanden | K/Grundlagen/CadLeser.cs | Mit Kalibrieren, Räume aus DWG |
| 90 | Aussparungspläne | vorhanden | K/Grafik/AussparungMassketten.cs | |

### G Berechnungen

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 91 | Heizlast | vorhanden | K/Heizung/Heizlast.cs | SIA 384.201; **ohne Oberfläche**, nur KI/MCP; braucht HLKS_NORMTABELLEN |
| 92 | Kühllast | **fehlt** | `kataloge/rechenregister.json` («Keine Kühllastberechnung») | |
| 93 | Druckverlust Rohr | vorhanden | K/Heizung/RohrDruckverlust.cs | Colebrook, ζ aus Skript C2; Regulierventile (kVS) nicht gerechnet; ohne Oberfläche |
| 94 | Druckverlust Kanal | teilweise | K/Rechnen/KanalDruckverlust.cs | Nur Reibung, ζ-Katalog Luft fehlt |
| 95 | Rohrdimensionierung | teilweise | K/Rechnen/SanitaerDimensionierung.cs | Sanitär automatisch; Heizung/Kälte von Hand |
| 96 | Kanaldimensionierung | teilweise | K/Rechnen/LueftungKette.cs | Nur Vordimensionierung als Variante, nicht am gezeichneten Netz |
| 97 | Sanitär-Belastungswerte | vorhanden | K/Rechnen/SanitaerBelastung.cs | |
| 98 | Hydraulischer Abgleich | **fehlt** | – | Keine Ventilvoreinstellung/kVS-Auslegung |
| 99 | Elektrolasten | **fehlt** | – | |
| 100 | Systeminspektor | teilweise | K/Heizung/HeizungLeistung.cs; `netz_folgen`, `warum` | Werte je Leitung abfragbar; keine Netzfarbdarstellung |
| 101 | Energieanalyse | **fehlt** | entwurf/ablauf.md:158 | |
| 102 | CH-Normbezug mit Quellen | vorhanden | rechenregister.json, grundlagen.json | Über Revit |

### H Koordination und Austausch

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 103 | Kollisionsprüfung | vorhanden | K/Kollision/Kollisionspruefung.cs | |
| 104 | BCF | vorhanden | K/Kollision/BcfExport.cs, BcfLeser.cs | BCF 3.0 hin und zurück |
| 105 | Automatische Aussparungen | vorhanden | K/Aussparungen/AussparungVorschlag.cs | Vorschlag, Bestätigung durch Menschen |
| 106 | IFC-Import | vorhanden | K/Import/Ifc*.cs | 1:1, Abgleich über GlobalId, Netzdatei Fassung 6 |
| 107 | IFC-Export | vorhanden | K/IfcExport/IfcExport.cs, IdsPruefer.cs | Mit IDS-Prüfung |
| 108 | Georeferenz | teilweise | K/Import/IfcDatei.cs:27; js/ansichten/bezugspunkt.js | LV95 gelesen; Koordinaten nicht setzbar/veröffentlichbar |
| 109 | NWC-Export | **fehlt** | – | Ersatz IFC/BCF |
| 110 | RVT lesen | teilweise | K/Unterlagen/RevitDatei.cs | Nur lesend, unvollständig |

### I Zusammenarbeit und Projekt

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 111 | Mehrere Bearbeiter | vorhanden | K/Speicher/IProjektSpeicher.cs:29; OrdnerSpeicher.cs | Reservierungen, Pakete, Synchronisieren über NAS; unerprobt mit mehreren Personen |
| 112 | Cloud-Zusammenarbeit | teilweise | K/Austausch/Webablage.cs | Gerüst ohne Netzzugriff |
| 113 | Phasen | **fehlt** | – | Nur Textfeld «phase» für den Plankopf |
| 114 | Entwurfsoptionen | teilweise | `lueftung_varianten`; K/Bewertung/Bewertung.cs | Nur Lüftungsvarianten |
| 115 | Projektinformationen | vorhanden | KL Projektinformation | |
| 116 | Projektvorlagen | teilweise | Ansichtsvorlagen, Mustersysteme | Kein «Projekt aus Vorlage» gefunden |

### J Erweiterung und Sonstiges

| Nr | Funktion | Stand | Fundstelle | Bemerkung |
|---|---|---|---|---|
| 117 | API/Skripte | vorhanden | K/Api/Befehle.cs; src/Hlks.Mcp/Kommandozeile.cs; REST | |
| 118 | Visuelle Programmierung | **fehlt** | – | |
| 119 | KI-Assistent | vorhanden | src/Hlks.Mcp; O «KI-Palette» | Über Revit |
| 120 | Rendering | teilweise | J/ansichten/ansicht3d.js | three.js, nicht fotorealistisch |
| 121 | VR/XR | vorhanden | J/ansichten/vr3d.js; HK_XR | Über Revit |
| 122 | Mengen/Kosten/Ausschreibung | teilweise | K/Austausch/Hkap*.cs; `mengen`, `materialliste` | Mengen ja; keine Kosten/Ausschreibungstexte |
| 123 | Baustellen-Webfreigabe | vorhanden | K/Baustelle/BaustelleExport.cs | Über Revit |

Tests im Repository (Dateien): Kern 331 (rund 3100 Tests), Mcp 26, Web 42, Node 66 `*.test.mjs` und 42 Browserreihen.

## Lücken nach Nutzen für H+K

Vorschlag zur Reihenfolge, Entscheid bei Christian. «Klein» = ein Arbeitsblock.

1. **Geschoss anlegen in der Oberfläche** (Nr. 48) – klein. Ohne IFC kann man heute kein neues Projekt sauber beginnen.
2. **Gesperrte Knöpfe Formteil/Sanitärapparat/Armatur** (PF/PX/PA, `aktionen.js:59–61`) auf «Familie platzieren» mit Kategorie umleiten – klein.
3. **Oberfläche für Heizlast, Rohr- und Kanal-Druckverlust, Heizkörper** (Nr. 91, 93) – die Rechenkerne sind da, nur ohne Oberfläche.
4. **Zeichnen und Ändern im Schnitt** (Nr. 36), danach in 3D (Nr. 35).
5. **Plan → DWG-Export** (Nr. 88) – im Austausch mit Architekten und Unternehmern oft verlangt.
6. **Regelbasierte Filter mit Grafikübersteuerung** (Nr. 58, 59) – z. B. VL rot, RL blau, nach Dimension.
7. **Freie Bauteillisten und Tags** (Nr. 80, 84).
8. **Rechenkerne Lüftung/Heizung vervollständigen:** ζ-Katalog Luft (Nr. 94), Kanaldimensionierung am Netz (Nr. 96),
   DN-Wahl Heizung und hydraulischer Abgleich über kVS (Nr. 95, 98) – braucht belegte Grundlagen.
9. **Deckenspiegel** (Nr. 52) für Luftauslässe und Decken.
10. **Gruppen, Fixieren, echte Reihe** (Nr. 24, 29).

Eher nicht nötig oder später: Kabeltrasse/Elektro (ausser für MSRL gewünscht), automatisches Routing, Phasen, Entwurfsoptionen,
Energieanalyse, NWC, Rendering, visuelle Programmierung. Kühllast nur, wenn H+K Kälteplanung rechnen will.

## Offene Fragen an Christian

- Gehört Elektro (Leerrohre/Trassen für MSRL) in den Umfang von KICAD?
- Soll `entwurf/bearbeitungswerkzeuge.md` auf diesen Stand nachgeführt werden? Viele «fehlt» dort sind überholt.
