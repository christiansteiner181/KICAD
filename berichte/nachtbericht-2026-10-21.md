# Nachtbericht 21.10.2026

Auftrag Christian 21.10.2026, 00:35: «arbeite bis morgen 8:00 oder erreichen von 90% des wöchentlichen
nutzungsvolumen weiter.» Verbrauch beim Start: 66 % der Woche, um 05:30: 76 %.

*(Entwurf, wird bis zum Morgen nachgeführt.)*

## Erledigt und in main

- **Formteile im Grundriss ohne Anschlusskreise und ohne Etikett** (Bogen, T-Stück, Reduktion). Geprüft und seit 00:34 auf
  dem Desktop.
- **Abwasser-Serie im Kern:** Grösse und Beschriftung nach Aussendurchmesser, P30. Geprüft (`geprueft` = 1f3f1b6, 04:22).
- **Oberfläche freie Grösse und Abwasser-Serie** (acbd473):
  - Kanal: Breite × Höhe frei, Normgrössen als Vorschlag.
  - Rohr: «d 110» statt DN, P29/P30 in Vorschau und Palette.
  - Familieneditor: Block «Freie Grösse» und Einsatz; die Kontrolle der Serie läuft nur auf Knopfdruck und ist abbrechbar.
- **Anleitungsbilder:** 27 neue Bilder für Modul 7 (Listenumbruch, P28, Koordinationsplan, Aussparungsboxen, IFC-Export,
  -Umfang, -IDS) und für freie Grösse und Abwasser-Serie. Sie liegen lokal, Stichprobe angesehen.
- **Quellen über Leserversionen** (580b9a6):
  - Die Familie merkt sich, mit welcher Leserversion ihre Quellen gelesen wurden (`leser`). Beim Speichern schlüsselt der Kern
    alte Ids selbst um.
  - Neu: `familie_pruefen` und der Sammelbefehl `familien_quellen_nachfuehren`.
- **Modul 5 Heizung:**
  - Spezifikation Teil A (`entwurf/modul-5.md`).
  - Die 59 Heizungs- und Kältesymbole sind aus HKAP übernommen (62357e3), gleiche Geometrie; die Referenz wurde maschinell aus
    dem HKAP-Quelltext gezogen.

## Grundausstattung (Arbeitsprojekt, Server 5153)

- **K2 Rechteckkanal auf freie Grösse** umgestellt (02:44, alle Prüfungen grün):
  - b × h frei 145 … 2500 mm, Schritt 1 mm; die 400 Normgrössen bleiben als Vorschlag.
  - Probe 430 × 270: Bogen 365 mm, T-Stück 275/365 mm.
- **Quellen umgeschlüsselt** (05:00): 25 Familien mit 844 Ids gespeichert, 16 waren schon richtig.
- **Offen:**
  - FAM-21/22/23 (Gewinderohr-Formteile): Leser 2 erkennt die Tabellen in diesem Katalog nicht mehr; die Korrektur
    (Leser 3) ist im Bau.
  - Datenpflege im Bau: R6/R7 als Abwasser-Serie, Kürzel, 66,7 streichen, Quellenvermerke, K3 AR.

## Gefunden

- **PDF-Leser 2 hat die Befund-Nummern verschoben.** Jedes Ändern von 28 Grundausstattungs-Familien scheiterte. Behoben durch
  die Leserversion in der Familie, siehe oben.
- **Abnahmeserver tot:** Der Rechner wurde am 21.10. um 00:09:40 neu gestartet (Windows-Ereignisse 6006/6005), und die Server
  haben keinen Autostart. 5150–5153 laufen wieder.
- **IFC-Exportleistung:** Der Test reisst die 30-s-Grenze nur unter Last. Der alte und der neue Stand brauchen unter gleicher
  Last gleich lang (65–70 s), es gibt also keine Verlangsamung.
- **Nebenwirkung der neuen Symbole:**
  - Absperrventile im Sanitär-Strangschema erscheinen jetzt als H+K-Sanduhr.
  - Der Heizkörper zeigt sein Symbol statt des «HK»-Kästchens.

## Fragen an Christian

1. Modul 5: Dürfen Elemente direkt im Prinzipschema entstehen, noch ohne Lage im Grundriss (wie im Strangschema)?
2. Modul 5: Anordnung des Prinzipschemas: Erzeugung links, Verteilbalken waagrecht (VL oben, RL unten), Heizgruppen senkrecht
   nach oben, Verbraucher als Sammelsymbol?
3. Modul 5: Bausteine zuerst nur die drei aus HKAP (Heizgruppe gemischt, ungemischt, Sicherheitsgruppe)?
4. Modul 5: Kälteerzeuger als eigene Klasse jetzt, Rückkühler und Kühldecke später? Welche BKP für die Kälte?
5. Modul 5: Welche Plannummer bekommt das Prinzipschema Heizung?
6. Modul 5 Teil B: Wo liegen die Normen für Heizlast, Heizkörper, FBH und Rohrnetz, und welche gelten?
7. Server: Sollen die Abnahmeserver nach einem Neustart von selbst starten (geplante Aufgabe bei der Anmeldung)?
8. Symbole: Ist die H+K-Sanduhr als Absperrventil auch im Sanitär-Strangschema richtig?
