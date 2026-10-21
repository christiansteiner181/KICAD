# Nachtbericht 21.10.2026

Auftrag Christian 21.10.2026, 00:35: «arbeite bis morgen 8:00 oder erreichen von 90% des wöchentlichen
nutzungsvolumen weiter.» Verbrauch beim Start: 66 % der Woche, um 05:30: 76 %. Schluss 08:40.

## Auf dem Desktop (geprüft, Marke `geprueft-20261021-0608`)

- **Formteile im Grundriss** (Bogen, T-Stück, Reduktion) ohne Anschlusskreise und ohne Etikett.
- **Abwasser-Serie:** Grösse und Beschriftung nach Aussendurchmesser («PE 110»), Befund P30 an einem Nicht-Abwasser-Medium.
- **Freie Grösse für Rechteckkanäle:**
  - Breite × Höhe als Zahlen, Normgrössen als Vorschlag.
  - Befund P29 ausserhalb der Grenzen.
  - Familieneditor mit Block «Freie Grösse» und Einsatz.
- **Anleitung:** 27 neue Bilder für Modul 7 (Listenumbruch, P28, Koordinationsplan, Aussparungsboxen, IFC-Export) und für
  freie Grösse und Abwasser.

## In main, noch nicht geprüft

- **Quellen über Leserversionen:** Die Familie merkt sich, mit welcher Leserversion ihre Quellen gelesen wurden. Beim
  Speichern schlüsselt der Kern alte Befund-Nummern selbst um. Neu sind `familie_pruefen` und `familien_quellen_nachfuehren`.
- **PDF-Leser 3:** Der Gewinderohr-Katalog wird wieder als Tabellen gelesen. Ursache waren zwei Fehler aus N4. Die übrigen
  47 Hersteller-PDF lesen sich unverändert.
- **Serienkontrolle schnell:** K2 braucht 0,3–0,7 s statt bis 155 s, abbrechbar und ohne den Kern zu blockieren. Formteile
  freier Serien speichern ihre Grösse selbst (P45).
- **Abzweig mit Serienwinkel:** `leitung_abzweigen` kann 45° bzw. 88,5° (Geberit PE, Silent-db20). Die Oberfläche dafür
  fehlt noch.

## Grundausstattung (Arbeitsprojekt, Server 5153)

- **K2 Rechteckkanal:** freie Grösse 145 … 2500 mm, Schritt 1 mm; die 400 Normgrössen bleiben als Vorschlag.
- **Quellen umgeschlüsselt:** 25 Familien mit 844 Ids.
- **Datenpflege** (87 Prüfungen grün):
  - R6 Silent-db20 und R7 Geberit PE sind Abwasser-Serien.
  - Kürzel C-St, ST, PEX, PE und PE-S gesetzt.
  - R1 66,7 gestrichen.
  - Quellenvermerke bei R6, R7, beim R2-T-Stück und bei den K1-Bögen.
- **Für dich offen:**
  - **Flexrohr AR:** Der Konflikt v_max 25 (Preisbuch) gegen 30 m/s (Broschüre 2025) wartet auf deinen Klick «Broschüre».
    Ein Agent hatte ihn umgehen wollen, indem er die Preisbuch-Quelle entfernt; das habe ich zurückgestellt, weil Konflikte nur
    ein Mensch entscheidet.
  - **R1 Bogen:** 11 Konflikte mit den neuen Geberit-Massen (Liste im Lauf-Protokoll). Die Datenblattwerte im Familienbericht
    sind zuverlässiger als die Zeilenlesung.
  - **R7 d32** ist nicht zeichenbar: Die Unterlagen nennen weder Innendurchmesser noch Wandstärke.
  - **FAM-21/22/23:** Mit Leser 3 lassen sie sich wieder speichern. Auf 5153 muss dafür der Server neu gebaut und die
    Umschlüsselung noch einmal laufen.

## Modul 5 Heizung

- **Spezifikation Teil A** liegt vor (`entwurf/modul-5.md`): Datenmodell, Prinzipschema, Oberfläche, ohne Normwerte.
- **Datenmodell:** im Bau.
- **Symbole:** Deine Rückmeldung «die heizungssymbole sind falsch» stimmte. Ich hatte den Agenten auf die Grundsymbole im
  HKAP-Code geschickt statt auf deine Bibliothek (`data/schema_symbole.json`). Der Stand ist zurückgenommen. Die Übertragung
  deiner Bibliothek läuft neu: Übersteuerungen, eigene Symbole und ausgeblendete, als Katalog mit Werkzeug zum Nachziehen.
  Deine Entscheide sind eingearbeitet: Fernwärme und Wärmetauscher als Raute, Handabsperrung als Kugelhahn. Ein Prüfbild mit
  allen Symbolen und der Zuordnung bekommst du zum Abgleich.

## Gefunden

- **Abnahmeserver tot:** Der Rechner wurde am 21.10. um 00:09:40 neu gestartet, und die Server haben keinen Autostart.
  5150–5153 laufen wieder.
- **IFC-Exportleistung:** Der Test reisst die 30-s-Grenze nur unter Last. Alter und neuer Stand brauchen unter gleicher Last
  gleich lang (65–70 s).

## Fragen an dich

1. Modul 5: Dürfen Elemente direkt im Prinzipschema entstehen, noch ohne Lage im Grundriss (wie im Strangschema)?
2. Modul 5: Anordnung des Prinzipschemas: Erzeugung links, Verteilbalken waagrecht (VL oben, RL unten), Heizgruppen senkrecht
   nach oben, Verbraucher als Sammelsymbol?
3. Modul 5: Bausteine zuerst nur die drei aus HKAP (Heizgruppe gemischt, ungemischt, Sicherheitsgruppe)?
4. Modul 5: Kälteerzeuger als eigene Klasse jetzt, Rückkühler und Kühldecke später? Welche BKP für die Kälte?
5. Modul 5: Welche Plannummer bekommt das Prinzipschema Heizung?
6. Modul 5 Teil B: Wo liegen die Normen für Heizlast, Heizkörper, FBH und Rohrnetz, und welche gelten?
7. Server: Sollen die Abnahmeserver nach einem Neustart von selbst starten (geplante Aufgabe bei der Anmeldung)?
8. R7 d32: Gibt es eine Schweizer Unterlage mit Innendurchmesser bzw. Wandstärke, oder soll der Kern beides je Typ optional
   zulassen?
