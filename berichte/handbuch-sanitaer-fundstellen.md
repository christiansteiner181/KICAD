# Fundstellenverzeichnis: Handbuch Gebäudetechnik Sanitär 2025

Stand: 02.11.2026. Zweck: Wo im Handbuch stehen die Grundlagen für die Sanitär-Dimensionierung im CAD-Rechenkern?
Dieses Verzeichnis enthält bewusst **keine Tabellenwerte und keine Normtexte** (Urheberrecht; Datei liegt im Repository).
Es sagt nur, wo was steht, in welcher Form und wie belastbar die Stelle für eine maschinelle Übertragung ist.

Quelle: `W:\05_Normen und Merkblätter\Handbuch Gebäudetechnik Sanitär 2025.pdf` (nur gelesen).

## 1. Impressum und Charakter des Dokuments

- PDF-Seite 2 (Impressum): «Handbuch Gebäudetechnik Sanitär», **1. Auflage 2025**, «gültig ab 1. Januar 2025» (Titelseite, PDF-S. 1).
  Autor Tom Renggli (Team Sanitär, gibb Grundbildung); Herausgeber **Renggli Consulting GmbH**; Korrektorat Fabian Kauer;
  Mediengestaltung WM Druck Sempacher Zeitung AG; Copyright 2025 Renggli Consulting GmbH. Auf der Titelseite Logos von
  Nussbaum, Geberit, GF/JRG.
- Es ist ein **Schul-/Lehrmittel** (gibb), keine Norm. Es ist eine Zusammenstellung von Auszügen und Arbeitsblättern aus
  Norm- und Richtliniendokumenten, mit Randnotizen (Notizseiten, handschriftliche Ergänzungen wie «produkteneutrale Tabellen
  aus der W3»). Das Impressum schränkt die Weitergabe ausdrücklich ein (nur Ausschnitte für den Unterricht).
  **Folgerung:** Für verbindliche Werte im Rechenkern ist die Originalnorm (SN 592000:2024, W3) die eigentliche Quelle; das
  Handbuch dient als Fundstellen- und Plausibilitätshilfe.
- **Umfang:** 137 PDF-Seiten. **Gedruckte Seitenzahl = PDF-Seitenzahl** (an Stichproben S. 3, 6, 21, 30, 45, 47, 52, 53, 70
  geprüft). Es gibt **kein Inhaltsverzeichnis und kein Register**; die Kapitel sind durch Randreiter getrennt
  («Abwasser», «Wasser», dann Gas, Z-Mass, X-Mass, Symbole, Grundriss, Schema …).
- Kapitel- bzw. Abschnittsnummern: Im Abwasserteil trägt das Handbuch **keine eigenen Nummern**; es verweist auf die
  Ziffern der SN 592000 (z. B. «Ziffer 7.2.6»). Die Abschnitte heissen nur nach Titel. Im Wasserteil stehen die W3-Ziffern
  (2.1.2, 2.2.1, 2.4.2 …).
- **Tabellennummern sind nicht eindeutig:** «Tabelle 5» kommt zweimal vor (S. 3 = Mindest-Innendurchmesser SN 592000;
  S. 46 = Hausanschluss-Richtwerttabelle W3, dort «Belastungswerttabelle 5»). Auch «Tabelle 3» (W3, S. 47) und
  Normtabellen 5–22/32/33 (SN 592000) überlappen sich im Nummernraum. Immer mit Seitenzahl zitieren.

## 2. Aufgeführte Normen, Richtlinien, Ausgaben

| Bezug | Wo genannt | Hinweis |
|---|---|---|
| **SN 592 000 (Ausgabe 2024)** | Seitenkopf S. 3; Ziffernverweise 7.1.1, 7.1.2, 7.2.6–7.2.11, 7.3.5, 7.3.7, 7.3.8, 7.5, 3.4.8, 3.7.4, 3.7.6/7, 5.2.9/10 im ganzen Abwasserteil (S. 3–44) | Ausgabe nur auf S. 3 ausdrücklich genannt, gilt für den ganzen Abwasserteil. |
| **SVGW W3 «Ausgabe 2013»** | nur Seitenkopf S. 52 (Beispieltabellen «produkteneutrale Tabellen aus der W3») | Für S. 45–51 und 53–73 wird **keine Ausgabe genannt**; Seitenüberschrift nur «Rohrweitenbestimmung». S. 48 nennt die Quelle «Richtlinie» und den SVGW-Download der LU-Tabellen. Ob Text S. 45–48 exakt der Ausgabe 2013 oder einer späteren Fassung entspricht, ist aus dem Handbuch **nicht belegbar**. |
| SVGW W3/E3 (Prüfverfahren) | S. 71 | Druckprüfung, Erstbefüllung (nur Nebenpunkt). |
| SIA 385/2 **Ausgabe 2015** (Anhang G), SIA 385/1 | S. 130–133 | Ausstosszeiten Warmwasser. |
| SIA 410, SN EN 806-1 | S. 107, 115–119 | Symbole (nicht Dimensionierung). |
| MeteoSchweiz / klima-extreme.ch (Regenspenden, «zuletzt 2023 aktualisiert») | S. 32–38 | Informative Regenspenden-Tabellen. |
| **SN EN 12056-2** | **nicht erwähnt** | Das Handbuch stützt den Abwasserteil allein auf SN 592000:2024. |

## 3. Trinkwasser (Kapitel «Wasser», PDF-S. 45–73)

| Grundlage | Fundstelle (gedruckt = PDF) | Form | Inhalt in eigenen Worten | Lesbarkeit / maschinell übertragbar? |
|---|---|---|---|---|
| Anwendungsgrenzen vereinfachte Methode vs. Berechnungsmethode | S. 45, Abschn. 2.1.2; S. 51 (Ablaufschema «Vorbereitende Arbeiten») | Text, Aufzählung | Bedingungen (max. abgewickelte Länge, Stockwerk-Teilstrecke, verfügbarer Druckverlust, Ausschluss von Dauer-/Spitzenentnahme, erhöhter Gleichzeitigkeit). | Text, sauber. Ja (Regeln als Prüfbedingungen). |
| Zulässige Fliessgeschwindigkeiten | S. 45, Abschn. 2.1.3 | Aufzählung | Grenzwerte je Leitungsart (Ausstoss-, Stockwerk-, Verteil-, Hausanschlussleitung, Hausanschluss mit Brandschutz). | Sauber. Ja. |
| Ruhedruck, Mindestfliessdruck | S. 46, Abschn. 2.1.4 | Aufzählung | Grenzdrücke an Entnahmestellen, Sonderfall Garten/Garage, Druckerhöhung. | Sauber. Ja. |
| **Hausanschluss / Wasserzähler** («Tabelle 5») | S. 46, Abschn. 2.4.1–2.4.3 | Tabelle (materialunabhängige Richtwerttabelle) | Spalten: max. abgewickelte Länge (5 Stufen); Zeilen: LU-Stufen (6 Stufen); Zellen: DN. Dazu Text: Druckverlust bis Zähler, Mindest-DN extern/intern, v ≤ Grenzwert. | Gut lesbar (Text-/Scan-Mischseite, bildlich geprüft). **Ja**, klare Matrix. |
| **Belastungswerte (LU) je Apparat** («Tabelle 3») | S. 47, Abschn. 2.2.1 | Tabelle | Spalten: Verwendungszweck, Entnahmedurchfluss kalt/warm (l/s), LU kalt/warm; nur Anschlüsse DN 15 (1/2"). Hinweise: Heizungsfüllventile nicht berücksichtigen; grössere Anschlüsse nach Herstellerangabe. 1 LU = 0,1 l/s (Text S. 47). | Sauber (bildlich geprüft). **Ja.** |
| **ΣLU → Spitzendurchfluss** («Diagramm 1», Kurve) | S. 70, Querformat | **Diagramm** (doppelt-logarithmisch) mit **zwei Funktionsgleichungen** (Potenzfunktionen QD = a·QT^b mit je eigenem Gültigkeitsbereich) und Markierung «grösster Einzelanschluss» | Spitzendurchfluss QD in l/s als Funktion des Summendurchflusses QT (= ΣLU·0,1 l/s). Titel im PDF mit Tippfehler «Digramm 1». | Vektor-/Scan-Grafik, Gleichungen im Kopf lesbar (bildlich geprüft). **Ja über die Gleichungen**, nicht über Kurvenablesung. Welche der beiden Gleichungen wann gilt, steht nur über die Bereichsangaben im Diagrammkopf – Eindeutigkeit der Überlappung (0,5–15 l/s) mit der Norm klären. |
| **Direkt ΣLU → Nennweite je Werkstoff/System** («Belastungswerttabellen», nicht «Kurve 1–5») | S. 48 (Regeln 2.2.2/2.2.3), S. 50–51 (Skizze Leitungsarten V/A/T, Ablaufschema), S. 52 (**neutrale W3-Beispiele 4.1–4.3**), S. 53–69 (Herstellertabellen mit SVGW-Zertifikat-Nr.) | Tabellen | Je Werkstoff/Verteilsystem eine Tabelle: Zeilen LU-Stufen; Spalten max. abgewickelte Länge, bei Ausstoss-/T-Stück-Tabellen zusätzlich «ohne/mit Wasserzähler»; Zellen Rohr da×s. Fusszeile: da×s, di, Armatur. Getrennt nach Ausstossleitung (A, v max. 4 m/s), T-Stück Stockwerk (3 m/s) und Verteilleitung (2 m/s). Vermischung nicht zulässig: bei Mischinstallation die T-Stück-Tabelle. | Scans mit OCR-Textschicht (Textschicht unzuverlässig, Spalten verschoben). Bildlich klar lesbar. **Übertragbar nur per Bild/Handtranskription mit Gegenprüfung**; Struktur sehr regelmässig. Kurven 1–5 im Sinne anderer Hilfsmittel gibt es **nicht**. |
| Werkstoffe/Systeme der Herstellertabellen | S. 53 Nussbaum Optipress (rostbeständiger Stahl); S. 54 Geberit Mapress (Stahl); S. 55–56 Geberit FlowFit PE-RT/Al; S. 57–58 FlowFit PB; S. 59 PushFit Metallverbund; S. 60 PushFit PB; S. 61 GF Sanipex MT; S. 62 GF iFIT; S. 63 GF Sanipex Classic PE-Xa; S. 64–65 Optiflex-p fix; S. 66–67 Optiflex-Flowpress; S. 68 Optiflex-Flowpress PB; S. 69 Instaflex PB | Tabellen | Auswahl einzelner Hersteller. **Kupfer und weitere Systeme fehlen.** | Wie oben. Nur diese Systeme abgedeckt. |
| SVGW-Ablageort aller LU-Tabellen | S. 48 | Text | Verweis, dass Tabellen beim SVGW kostenlos zum Download stehen. | Verweis. Vollständige Daten dort holen. |
| Druckverlustbudget der LU-Tabellen | S. 48, Abschn. 2.2.2 | Text | Aufteilung des Gesamtdruckverlusts auf Stockwerk-, Verteilleitung und Gruppenzähler; nicht berücksichtigt sind Einbauten (Filter etc.). Optimierung bei rechnerischem Nachweis des Mindestfliessdrucks erlaubt. | Sauber. Ja als Randbedingung. |
| Wasserzähler Druckverlust | S. 46 | Text | Grenzwert für Druckverlust Versorgungsleitung bis Zähler. | Sauber. Ja. |
| **Druckverlustberechnung** (Reibung, Zeta, Rauigkeit, Rohrdurchmesser-Tabellen) | **nicht enthalten** (nur Verweis «Berechnungsmethode»/«Herstellerangabe») | – | Das Handbuch gibt kein Berechnungsverfahren mit Reibungsbeiwerten und Zeta-Werten. | Lücke. |
| Warmwasser: Ausstosszeiten, Warmhaltung | S. 130–133 | Tabelle, Skizzen, Formel/Rechenbeispiel | Höchste Ausstosszeit je Apparategruppe ohne/mit Warmhaltung (nach SIA 385/1 bzw. 385/2:2015), Leitungsinhalte je Rohr, Berechnungsbeispiel. | Teils Scan; für Prüfregel (Ausstosszeit ≤ Grenze) nutzbar. |
| Warmwasser: Sicherheitsventil-Grössen | S. 47 unten, «6.4.2» | Tabelle | Ventilgrösse nach Wasserinhalt/Leistung. | Sauber. |
| **Zirkulation / Zirkulationsbemessung** | S. 130, 133, 135–136 nur als Begriff/Hygiene (Wärmesiphons, Steigzone, Temperaturen) | Text, Skizzen | **Keine Dimensionierungsregeln** (Volumenstrom, Temperaturspreizung, Rohrnetzabgleich). | Lücke. |
| Trinkwasserhygiene | S. 134–137 | Text, Skizzen | Betriebsregeln (Temperaturen, Dämmung, Stagnation). | Nebenpunkt. |
| Druckprüfung, Erstbefüllung | S. 71–73 | Text | Verfahren nach W3/E3. | Nebenpunkt. |

## 4. Abwasser (Kapitel «Abwasser», PDF-S. 3–44; Grundlage SN 592 000:2024)

| Grundlage | Fundstelle | Form | Inhalt in eigenen Worten | Lesbarkeit / maschinell übertragbar? |
|---|---|---|---|---|
| Bemessungsgrundsatz, Mindest-Innendurchmesser | S. 3, «Tabelle 5» | Tabelle | DN → Mindest-Innendurchmesser IDmin (Reihe DN 32 bis DN 300). Grundlage aller Tabellenwerte. | Sauber, nur 2 Zeilen. **Ja.** |
| **Gefälle** nach Leitungsart | S. 3, «Tabelle 6» | Tabelle | Je Leitungsart (Grund-/Grundstücksanschluss SW ≤/> DN 200, NW, Sammelleitung, Anschlussleitung SW/NW, Sickerleitung, Lüftungsleitung): min., ideal, max. in %. Hinweis zum Gefälle von 88,5°-Formstücken. | Bildlich sauber; Textschicht verrutscht. **Ja**, klare Matrix. |
| **Schmutzwasserabfluss Q_ww = K·√ΣDU** | S. 4 | Formel + Text | Näherungsformel; Q_ww mindestens so gross wie grösstes Einzelgerät; bei Zusammenführung mit höherem K weiterrechnen. | Sauber. Ja. |
| **Abflusskennzahl K je Nutzung** («Tabelle 7») | S. 4 | Tabelle | Drei Nutzungsstufen (unregelmässig / regelmässig / häufig) mit je einem K. | Sauber (OCR liest einzelne Zeichen falsch). **Ja.** |
| **Anschlusswerte DU je Apparat** («Tabelle 8») | S. 6 | Tabelle mit **gruppierten (zusammengefassten) Zeilen** | Pro DU-Stufe eine Zeilengruppe: Apparate, Kürzel, DU, Geruchsverschluss-Ausgang DN. Fussnoten 1–4: Dusche nach Zulaufleistung, Waschtrog am Bodenablauf-Hinterlauf, Geschirrspüler am Siphon zählen nicht, gewerbliche Apparate nach Herstellerangabe. Ergänzende Zeile: Abflussleistung von Sicherheitsventilen usw. | Bildlich sauber, Textschicht **verrutscht** (Zuordnung Apparat→DU nur über Zeilengruppen). **Mit Vorsicht:** Zuordnung im Bild prüfen; Fussnotenlogik braucht Programmregeln. |
| Anschlussleitungen: massgebende Länge, Auswahllogik | S. 7 (Figur 10, Ablaufschema) | Text + Flussdiagramm | Abgewickelte Länge ≤ 4 m bzw. 4–10 m bzw. > 10 m; entsprechende Tabelle wählen, 4–10 m teils eine Nennweite grösser. | Sauber. Ja als Regelwerk. |
| **Unbelüftete Anschlussleitungen** («Tabelle 9», Ziffer 7.2.6) | S. 8 | Tabelle | Höchstzulässige DU-Summe und grösster Einzel-DU → Nennweite DN; Fussnoten (Einzelapparat, max. 2 WC, DN 90 nur über Rohboden). | Sauber. Ja. |
| **Belüftete Anschlussleitungen / Umlüftung** («Tabelle 10», Ziffer 7.2.7) | S. 8 | Tabelle | Höchstzulässige DU, grösster Einzel-DU → DN und Umlüftungs-DN; Fussnote zur Sammellüftung. | Sauber. Ja. |
| Anschlussleitungen: Sonderfälle (Achsverschiebung, Fall-/Sturzstrecke, Anschluss an Sammel-/Grundleitung) | S. 9–12 (Figuren 19–24), S. 13 (Einführung) | Text + Skizzen | Ausnahmen mit «eine Nennweite grösser» bis max. DN 90/100, Längen 4/10 m, Höhenverschiebung bis 1,0 m. | Skizzen mit Textregeln. Programmierbar, aber viele Fälle; Auszug erfordert Sorgfalt. |
| Fallleitungen: Grundregeln, Achsverschiebung, Schleifung, Sonderfälle | S. 14–17 (Figuren 25–30) | Text + Skizzen | Verbotene Anschlussbereiche, Schleifung ab Höhe 10 m (Unterschied ≤/> 10 m), Sonderfall WC + Waschtisch mit Lüftung DN 56. | Text lesbar, S. 16 praktisch nur Skizze. |
| Lüftungssysteme | S. 18–20 (Figuren 4–7) | Text + Skizzen | Hauptlüftung (Standard), Nebenlüftung direkt/indirekt, Umlüftung; Sonderformstücke nach Hersteller. | Skizzen. Konzeptionell; Auswahl liegt beim Planer. |
| **Fallleitungen: zulässige Belastung/Nennweite, Hauptlüftung** («Tabellen 11, 12», Ziffern 7.2.8, 7.2.9) | S. 21 | Tabellen (mit Figuren 90, 91) | Je Abzweigart (Winkel / Bogen): max. DU-Summe bei K = 0,5, grösster Einzel-DU, Q_max, Nennweite. Ablaufschema oben: erst Lüftungssystem, dann Abzweigart. | Bildlich **sauber**; Textschicht Spalten verdreht. **Ja** nach Bildabgleich. |
| **Fallleitungen: Nebenlüftung** («Tabellen 13, 14», Ziffern 7.2.10, 7.2.11) | S. 22 | Tabellen | Wie oben, zusätzlich Nennweite der Nebenlüftung. Fussnote: Mindest-DN bei WC-Anschluss. | Textschicht ungenau (verschobene Spalten). Bildabgleich zwingend. |
| Sammelleitungen: Regeln, Mindestnennweite, verbotener Anschlussbereich | S. 23–24 (Figuren 37, 38) | Text + Skizzen | Mindest-DN, nicht kleiner als Fallleitung; Anschlussverbot nahe Umlenkung (Fallleitungshöhe ≤/> 10 m). | Text sauber. Ja. |
| Grundleitungen: Leitungsführung, Beruhigungsstrecke, Richtungsänderungen, Dimensionswechsel | S. 25–27 (Figuren 48–57) | Text + Skizzen | Frostsichere Tiefe, Beruhigungsstrecke vielfaches DN, Bogen/Abzweige max. 45°, keine Reduktion in Fliessrichtung, kein Doppelabzweig. | Sauber. Teilweise als Prüfregeln nutzbar. |
| **Anschluss ohne zusätzliche Lüftung an belüftete Grundleitung** (maximale Längen) | S. 28 (Figur 33) | Text + Skizze | Fälle A–D mit maximalen Distanzen je Apparatezahl/-art; andere Fälle mind. mit DN 56 lüften. | Sauber. **Ja** (zentral für «maximale Längen ohne Lüftung»). |
| **Berechnungsablauf Sammel-/Grund-/Grundstücksanschlussleitung** | S. 29 | Flussdiagramm + Text | Reihenfolge Q_ww, Q_c (Dauerabfluss), Q_p (Pumpe), Q_R (Regen), Summe Q_tot; Gefälle-Rahmenwerte; **Mindestdurchmesser-Liste** (Sammelleitung ohne/mit WC, Grundleitung nach Fallleitungshöhe/WC, Grundstücksanschluss EFH); K = Wohnhaus-Vorgabe. | Text sauber, Layout gebrochen. **Ja**, aber Zahlenwerte aus Bild prüfen. Hinweis: Wiederholung von Tabelle-6-Werten (Konsistenz prüfen). |
| **Zulässige Belastung Sammel-/Grund-/Grundstücksanschlussleitungen** («Tabelle 22», Ziffer 7.5) | S. 30 (Hauptseite) und S. 42 (Wiederholung) | Tabelle | Zeilen DN (Sammelleitung ab kleinen Nennweiten, Grundleitung ab grösseren), Spalten Gefälle in 0,5-%-Schritten von 1 bis 5 %; Zellen Q_max in l/s. Grundlage: Prandtl-Colebrook, Betriebsrauigkeit k_b, Füllungsgrad h/ID, IDmin aus Tab. 5. Fussnote: kleinste DN nur für Sammelleitungen. | Bildlich sauber; **Textschicht verrutscht**. Ja nach Bildabgleich. Hinweis: Formel erlaubt Neuberechnung statt Tabelle (Parameter im Text genannt). |
| Gesamtabwasserabfluss Q_tot | S. 29, S. 42 | Formel | Summe aus Schmutzwasser-, Dauer-, Pumpen-, Regenabfluss ohne Abzug. | Sauber. Ja. |
| Mindestnennweiten (Geruchsverschluss, Sammel-/Grundleitung, WC) | S. 6 (Geruchsverschluss-DN je Apparat), S. 23, S. 29, Fussnoten S. 22/30/42 | verteilt | Keine zentrale Mindest-DN-Tabelle; Werte stehen verstreut. | Zusammenstellung nötig. |

### Regenwasser (Nebenpunkt)

| Grundlage | Fundstelle | Form | Inhalt | Lesbarkeit / übertragbar? |
|---|---|---|---|---|
| Niederschlagswasserabfluss Q_R = A·r·C_s | S. 31 | Formel + Text | Teilflächen einzeln, Summierung; Wiederkehrperiode höher bei hohem Schadenpotenzial. | Sauber. Ja. |
| Regendauer und Wiederkehrperiode («Tabellen 15, 16») | S. 32 | Tabellen | Zuordnung Schadenpotenzial/Fläche → Regendauer, Wiederkehrperiode (Dach/Terrasse/Notentwässerung und Umgebungsflächen). | Textschicht bei Zuordnung unklar; Bildabgleich nötig. |
| Regenspenden je Station (5 und 10 Minuten) («Tabellen 32, 33») + Klimaregionen-Karte (Fig. 119) | S. 32–38 | Tabellen (lang, viele Stationen/Wiederkehrperioden), Karte | Geschätzte Regenspenden je MeteoSchweiz-Station, mit Konfidenzbereich; «informativ». | Scan/OCR schlecht (Zahlen verrutscht). **Nicht übertragen**; stattdessen Original (klima-extreme.ch / Norm Anhang). |
| Spitzenabflussbeiwerte («Tabellen 17, 18»), Kolmationsgrade («Tabelle 19») | S. 39–40 | Tabellen | C_s für Dach-/Umgebungsflächen, je Belag und Kolmationsgrad; Einteilung der Kolmationsgrade nach Nutzung; Sonderregel Gefälle > 7,5 %. | Lesbar, Textschicht gemischt. Ja nach Bildabgleich. |
| Anschluss- und Fallleitungen Niederschlag («Tabellen 20, 21», Ziffern 7.3.7, 7.3.8) | S. 41 | Tabellen + Formel (Wyly-Eaton, Rauigkeit, Füllungsgrad) | Q_Rmax je DN (Anschlussleitung nach Gefälle, nur kleine DN; Fallleitung je DN). | Sauber. Ja. |
| Wirksame beregnete Fläche, Schlagregen | S. 40 (Figur 94) | Text | Horizontalprojektion; Schlagregen meist unberücksichtigt. | Sauber. |

## 5. Sonstiges für die automatische Dimensionierung

- Mindestgefälle: Tabelle 6 (S. 3), Wiederholung der Rahmenwerte S. 29.
- Maximale Längen ohne Lüftung: S. 7 (4/10 m, Ablaufschema), S. 8–12, S. 28 (Anschlüsse an belüftete Grundleitung, Fälle A–D).
- Anschlussregeln: S. 13 (Einführung in die Fallleitung), S. 24, 26–27 (Zusammenführung/Abzweige), S. 28.
- Verbotene Anschlussbereiche, Beruhigungsstrecken, Bogenradien: S. 14–15, 24–26.
- Symbole (SIA 410/W3) und Platzbedarf: S. 104–119, 129 (nicht für Rechenkern).

## 6. Einschätzung der Lesbarkeit insgesamt

- Das PDF ist ein **Scan mit OCR-Textschicht**. Fliesstext ist brauchbar; **Tabellenspalten sind in der Textschicht oft verschoben**
  (besonders Tabellen 8, 11–14, 22, 32/33 und die Herstellertabellen). Beim Lesen der gerenderten Seiten (Bild) waren die Tabellen
  S. 3, 6, 21, 30, 46, 47, 52, 53 dagegen klar lesbar.
- **Empfehlung für Übertragung:** Daten aus dem Bild transkribieren und Zeile für Zeile mit der Seite abgleichen; nach Möglichkeit
  gegen die Originalnorm (SN 592000:2024) bzw. SVGW-Download gegenprüfen. Reine Textextraktion ist für Tabellen **nicht ausreichend**.
- Eindeutig und gut formalisierbar: K-Tabelle, IDmin, Gefälle, Tab. 9/10, Tab. 11–14 (nach Bildabgleich), Tab. 22, Formeln Q_ww,
  Q_tot, Q_R, Fliessgeschwindigkeits- und Druckgrenzen, LU-Tabelle (S. 47), Hausanschluss (S. 46).
- Mit Rückfrage zu klären: Diagramm 1 (zwei Gleichungen), DU-Tabelle mit Fussnotenlogik, Regenspendentabellen.

## 7. Offene Punkte / Lücken

1. **W3-Ausgabe**: nur S. 52 nennt «Ausgabe 2013»; für die übrigen Wasserseiten keine Ausgabeangabe. Gegen aktuelle W3-Fassung prüfen,
   bevor Werte in den Kern wandern.
2. **Keine Kurven 1–5**; ΣLU→Q nur über Diagramm 1 (S. 70); ΣLU→DN nur über **Herstellertabellen** (S. 53–69) und drei neutrale
   Beispiele (S. 52). Kupfer und weitere Systeme fehlen; vollständige LU-Tabellen beim SVGW (Verweis S. 48).
3. **Druckverlustrechnung (Berechnungsmethode)** nicht enthalten: keine Reibungsbeiwerte, Zeta-Werte, Rauigkeiten, DN↔Innendurchmesser für
   Versorgungsrohre (nur in Herstellertabellen).
4. **Warmwasser/Zirkulation**: nur Ausstosszeiten und Hygiene; **keine Dimensionierung** der Zirkulation.
5. **Fallleitungstabellen** nennen die maximale DU-Summe nur für K = 0,5 und zusätzlich Q_max; wie bei anderem K zu verfahren ist
   (Vergleich von Q_ww mit Q_max), ist im Handbuch nicht ausformuliert und gegen die Norm zu klären.
6. **Lüftungsleitungen**: Nennweiten von Hauptlüftung/Dachdurchführung nicht als eigene Tabelle; nur Nebenlüftung- und Umlüftungs-DN in Tab. 10/13/14 sowie Sonderregeln (S. 15, 17). Prüfen, ob Handbuch Auszug unvollständig ist.
7. **Mindestnennweiten** verstreut (S. 6, 22–23, 29, 30, 42); Zentraltabelle fehlt.
8. **Regenspenden**: nur ausgewählte Stationen; Rest über klima-extreme.ch.
9. **Doppelte Tabellennummern** (siehe Abschnitt 1); Seitenzahl immer mitführen.
10. Seite 42 wiederholt Q_tot und Tabelle 22 (identisch zu S. 29–30); Seiten 5, 43, 44, 49 sind Notizseiten.
11. Urheberrecht: Das Handbuch erlaubt nur Ausschnitte; Werte im Repository nur aus Originalnormen mit Lizenz beziehen, nicht aus diesem Handbuch kopieren.
