# Nachtbericht 21.10.2026

Auftrag Christian 21.10.2026, 00:35: «arbeite bis morgen 8:00 oder erreichen von 90% des wöchentlichen
nutzungsvolumen weiter.» Verbrauch beim Start: 66 % der Woche.

*(Entwurf, wird bis zum Morgen nachgeführt.)*

## Erledigt

- **Formteile im Grundriss ohne Anschlusskreise und ohne Etikett** (Bogen, T-Stück, Reduktion). Geprüft und auf dem Desktop
  seit 00:34 (Marke `geprueft-20261021-0034`).
- **Abwasser-Serie im Kern** zusammengeführt: Grösse und Beschriftung nach Aussendurchmesser, P30.
- **Grundausstattung K2 Rechteckkanal auf freie Grösse** umgestellt (Server 5153, Lauf 02:44, alle Prüfungen grün):
  - b × h frei 145 … 2500 mm, Schritt 1 mm; die 400 Normgrössen bleiben als Vorschlag.
  - Bogen, T-Stück und Reduktion rechnen ihre Masse aus der Formel; Probe 430 × 270: Bogen 365 mm, T-Stück 275/365 mm.
- **Abnahmeserver:** Die Ursache des Absterbens ist geklärt. Der Rechner wurde am 21.10. um 00:09:40 neu gestartet
  (Windows-Ereignisse 6006/6005), und die Server haben keinen Autostart. 5150–5153 laufen wieder.
- **Modul 5 Heizung:** Spezifikation Teil A geschrieben (`entwurf/modul-5.md`: Datenmodell, Prinzipschema, Oberfläche, ohne
  Normwerte). Datenmodell und Symbole sind im Bau.

## Gefunden

- **PDF-Leser 2 hat die Befund-Nummern verschoben.** 35 der 40 Grundausstattungs-Familien verweisen mit zusammen 1089
  Quellen auf falsche Stellen; jedes Ändern dieser Familien scheiterte. Die Korrektur im Kern ist im Bau: Die Familie merkt
  sich die Leserversion, und der Kern schlüsselt die Quellen automatisch um. Erst danach folgen die übrigen Datenarbeiten
  (R6/R7 Abwasser, Kürzel, 66,7 streichen, Quellenvermerke).

## Fragen an Christian

1. Modul 5: Dürfen Elemente direkt im Prinzipschema entstehen, noch ohne Lage im Grundriss (wie im Strangschema)?
2. Modul 5: Anordnung des Prinzipschemas: Erzeugung links, Verteilbalken waagrecht (VL oben, RL unten), Heizgruppen senkrecht
   nach oben, Verbraucher als Sammelsymbol?
3. Modul 5: Bausteine zuerst nur die drei aus HKAP (Heizgruppe gemischt, ungemischt, Sicherheitsgruppe)?
4. Modul 5: Kälteerzeuger als eigene Klasse jetzt, Rückkühler und Kühldecke später? Welche BKP für die Kälte?
5. Modul 5: Welche Plannummer bekommt das Prinzipschema Heizung?
6. Modul 5 Teil B: Wo liegen die Normen für Heizlast, Heizkörper, FBH und Rohrnetz, und welche gelten?
7. Server: Sollen die Abnahmeserver nach einem Neustart von selbst starten (geplante Aufgabe bei der Anmeldung)?
