# Quest App – Testablauf 24.10.2026

Stand: Zweig `claude/xr-quest-app-7c693c` (P0–P7, P5a, P6, P5b). Projekt: Arbeitskopie von Test_praxis. Brille: Quest 3 am Kabel.

## Vorbereitung (Hauptlauf)
1. APK bereitstellen: `werkzeuge/quest_app_bereitstellen.ps1`.
2. KICAD dieses Stands auf Port 5130 mit Test_praxis starten; die Seite öffnet sich im Browser. Das Desktop-KICAD muss geschlossen sein.

## 1. Installieren und verbinden
- [ ] Ansicht › Fenster › «Quest verbinden …»: Der Dialog passt ins Fenster; «Quest App» steht oben, «Browser-Prototyp» ist eingeklappt.
- [ ] «Netzzugang» ist an; «App aktualisieren» mit Weg «über Kabel». Alle Schritte sind grün.
- [ ] Brille aufsetzen: Die App verbindet sich (Ampel grün).
- [ ] Am PC Ansicht › «Brille zeigen»: Die Figur steht in der 3D-Ansicht und folgt dem Kopf.

## 2. Bedienung (P5a)
- [ ] Die schwebenden Apps liegen 50° links auf Brusthöhe, gut lesbar und mit dem Finger treffbar.
- [ ] Pinch und Wisch nach vorn blendet die Apps aus; die linke Handfläche zum Gesicht holt sie zurück.
- [ ] App «Etagen»: Antippen wechselt das Geschoss.
- [ ] Pinch und Wisch hoch bzw. runter wechselt die Etage (beim Begehen und am Sitzungstisch).
- [ ] Springen: ein Strahl; das Ziel wird grün; Loslassen springt.
- [ ] Drehen im Sitzen: Wisch nach rechts dreht nach links, die Gegenrichtung ist 1 s gesperrt.
- [ ] Tunnelblick beim Springen und Drehen ist angenehm.
- [ ] Geisterhand: Die Hand 1,5 s ruhig über einer Kachel halten zeigt die Geste.

## 3. Sitzungstisch und AR (P4b)
- [ ] Rechtsklick Raum › «Modell am Sitzungstisch»: Die Etage mit Schnitt unter der Decke ist **hell** (nicht schwarz).
- [ ] Rechtsklick Raum › «Raum in AR»: nur der Raum ±1,5 m, unter der Decke geschnitten, hell.
- [ ] Die Griffe sitzen an den vorderen Ecken des sichtbaren Gebäudes.
- [ ] Beide Griffe halten: drehen, zoomen (bis 1:5), verschieben in Breite, Tiefe und Höhe.
- [ ] «Echter Tisch»: Passthrough an, Tisch antippen, das Modell steht auf dem echten Tisch, danach wieder «Virtueller Raum».
- [ ] Probe-Ebene «fliessrichtung» ein/aus.

## 4. Labor (P6 und P5b)
- [ ] Ansicht › Fenster › «XR-Labor …»: Stellschrauben verstellen (z. B. `anzeige.skala`, `apps.groesse_m`); die Wirkung ist sofort in der Brille sichtbar.
- [ ] Ein Profil speichern, laden und als «gilt für die App» markieren.
- [ ] Varianten umschalten: Bedienung Apps/Tablet, Etage Wisch/App.
- [ ] Prüfaufgabe starten (z. B. «an eine Markierung springen»): Die Karte erscheint in der Brille, die Aufgabe ist lösbar, das Messprotokoll kommt in der Auswertung an.
- [ ] Schnitt mit flacher Hand am Sitzungstisch.
- [ ] Röntgen-Hand beim Begehen.
- [ ] Fragebogen: Der Komfortteil lässt sich ausfüllen; SUS und NASA-TLX sind noch ohne Wortlaut.

## Bekannt bzw. offen
- **SUS und NASA-TLX:** Wortlaut und Quelle der deutschen Fassungen fehlen (Christian).
- **WLAN (K3):** «s-net 3» in Windows auf «Privat» stellen und `werkzeuge/xr_firewall.ps1` als Administrator ausführen.
- **Begehen:** in der App etwas dunkler als am PC (Tönung linear statt sRGB); eine Korrektur ist möglich.
- **Tests:** 3 Web-Tests mit engen Zeitgrenzen sind auf diesem PC rot (langsame adb-Attrappe), kein Codefehler.
