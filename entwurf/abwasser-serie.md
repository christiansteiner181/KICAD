# Abwasser-Serien: Grösse und Beschriftung nach Aussendurchmesser (Nachtrag Modul 3) – Spezifikation

Stand 20.10.2026 · Hauptlauf · baut auf `entwurf/freie-groesse-kanal.md` auf (gleiche Dateien, deshalb danach)

## Anlass und Entscheid

Christian, 20.10.2026: «Geberit PE ist immer Abwasser.» Fragefenster «Abwasser-Serie»: Die Serie bekommt den Einsatz Abwasser –
Grösse und Beschriftung immer nach Aussendurchmesser (z. B. «PE 110»), keine DN nötig (d32 wird zeichenbar), Prüfbefund, wenn jemand
sie für Heizung, Kälte oder Trinkwasser verwendet; gilt ebenso für Silent-db20.

Heute: Rohrserien sind immer DN-geführt (`leitung.groesse = { dn }`, Rohr-Element `dn`); die Beschriftung «PE-S 110» wählt KICAD
über das Medium der Leitung (`beschriftungAbwasser`). Geberit PE d32 hat keine DN und ist deshalb nicht zeichenbar.

## Datenmodell

- Block `leitung` neu `einsatz` (optional): `"abwasser"` (weitere Werte später). Nur Kategorie Rohr.
- Bei `einsatz = abwasser` darf die Grösse der Aussendurchmesser sein: `groesse = { "d": "<Parameter>" }` (empfohlen) – `dn` bleibt
  erlaubt (bestehende Serien und Projekte gelten weiter). Ein DN der Herstellerunterlage darf zusätzlich als Typparameter stehen
  (z. B. für die Dimensionierung SN 592000 in Modul 4 Teil B), wird aber **nie** abgeleitet.
- Rohr-Element einer d-geführten Serie: `durchmesser` (Aussendurchmesser, mm) statt `dn`; Katalog `klassen.json` (Rohr bzw. Leitung)
  entsprechend, `dn` bleibt für DN-geführte Serien.

## Kern (hlks-kern)

1. `Leitungsgroesse` für Rohre auch über d; `Leitungsserie`, `Anschlusslagen`, Routing und Befehle (`leitung_zeichnen`, `leitung_groesse`,
   `leitung_serie`, `leitung_abzweigen`, `uebergang_setzen`, `leitung_aufteilen`) nehmen bei d-geführten Abwasser-Serien die Grösse
   als Aussendurchmesser (`{ "d": 110 }`, Meldungen «d 110»).
2. **Beschriftung:** Serien mit `einsatz = abwasser` immer nach Aussendurchmesser mit Kürzel («PE-S 110», ohne Kürzel «110»),
   unabhängig vom Medium.
3. **Prüfung** (nächste freie P-Nummer): Leitung einer Abwasser-Serie mit einem Medium, dessen Systemtyp nicht `fliessart =
   schwerkraft` ist (Abwasser laut `kataloge/systemtypen.json`) → fehler «Abwasser-Serie für <Medium>»; ohne Medium → hinweis.
4. **Formteile:** Formteilwahl über d (Anschlüsse der Formteil-Familien mit `d`); bestehende dn-Formteile einer dn-Serie unverändert.
5. **Abfrage `leitungsserie`:** `einsatz`, Grössen mit d; `beschriftung` = Abwasserform.
6. **IFC-Export/Import:** Abwasserrohre mit Aussendurchmesser (OuterDiameter) und, falls vorhanden, Nennweite des Herstellers;
   Import einer Abwasserleitung in eine d-geführte Serie über den Aussendurchmesser.
7. **Sanitär-Belastung (Modul 4A)** und Listen funktionieren mit d-geführten Leitungen (Tests).
8. **KI-Anleitung:** Abwassersysteme (Silent-db20, Geberit PE, PP-Hausabfluss …) als `einsatz = abwasser` mit Grösse d anlegen; DN nur
   als Herstellerangabe.
9. **Tests:** Testfamilie Abwasser d-geführt mit Formteilen; zeichnen d110 mit Bogen 45° und Abzweig d110/d56; Beschriftung «PE 110»;
   Prüfbefund bei Medium Heizung; d32 ohne DN zeichenbar; bestehende dn-Serien und Projekte unverändert (Rundreise); IFC.

## Oberfläche (hlks-modul, danach)

Rohr zeichnen bzw. Eigenschaften mit Abwasser-Serie: Grössenwahl «d 110» statt «DN 100»; Familieneditor: Einsatz anzeigen/setzen.

## Daten (Hauptlauf, danach)

R6 Silent-db20 und R7 Geberit PE im Arbeitsprojekt Grundausstattung auf `einsatz = abwasser`, Grösse d (DN als Herstellerangabe
behalten), Formteile mit Anschlüssen über d.

**Fertig, wenn** im Arbeitsprojekt ein Geberit-PE-Strang d110 mit Bogen und Abzweig d56 sowie ein Rohr d32 gezeichnet sind, die
Beschriftung «PE 110» lautet und dieselbe Serie an einer Heizungsleitung einen Prüfbefund ergibt.
