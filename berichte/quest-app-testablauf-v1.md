# Quest App – Testablauf Version 1 (V1a, V1c)

Stand: Zweig `claude/xr-quest-app-7c693c`. Ersetzt für die Bedienung den Testablauf vom 24.10.2026 (dort noch «Handfläche zum Gesicht»
und «Pinch und Wisch nach vorn»). Projekt: Arbeitskopie von Test_praxis. Brille: Quest 3 am Kabel. Alle Grenzwerte der Gesten sind
Annahmen und im XR-Labor einstellbar.

## Vorbereitung (Hauptlauf)
1. APK bereitstellen: `werkzeuge/quest_app_bereitstellen.ps1`.
2. KICAD dieses Stands mit Test_praxis starten (Port 5130; das Desktop-KICAD dafür schliessen).
3. In KICAD Ansicht › Fenster › «Quest verbinden …» › «App aktualisieren» (Weg «über Kabel»); alle Schritte grün.

## 1. Symbole und Apps
- [ ] Beide Handflächen nach oben, deutlich anheben: Die runden Pastell-Symbole steigen von unten auf und stehen frontal, etwa 60 cm
      entfernt, über etwa 60 cm Höhe in höchstens 3 lockeren Reihen.
- [ ] Die Symbole sind dieselben wie in KICAD (Strich bzw. Revit nach der Einstellung «Symbole»).
- [ ] Zeigefinger nähern: Zeigerpunkt und Name als Tooltip über dem Symbol.
- [ ] Ein Symbol mit der Hand (Handfläche oder Finger) berühren: Ton, die App öffnet frontal in ihrer Farbe; das Feld weicht seitlich aus.
- [ ] Die Symbole bleiben stehen, bis sie weggewischt werden (kein Ausblenden nach Zeit).
- [ ] Wegwischen (rechte Hand offen, Handfläche nach links, Wisch von rechts nach links): zuerst die offene App, beim nächsten Wischen
      das Symbolfeld. Pinch und Wisch nach vorn tut nichts mehr.
- [ ] Hinweise schweben frei ohne Kasten und sind vor hellen und dunklen Flächen lesbar.
- [ ] Geisterhand zeigt Hebegeste und Wegwischen neben den Bedienelementen.
- [ ] Am Sitzungstisch: Feld über der Tischplatte, nichts schneidet in den Tisch.

## 2. Messen und Problem-Marker (V1a)
- [ ] App «Messen»: Messband (Pinch halten, ziehen, loslassen), zwei Punkte, Lot. Fang auf Ecke, Kantenmitte, Kante, Rohrachse, Fläche.
- [ ] Der Messwert steht an der Linie; offene Hand quer über die Linie löscht sie.
- [ ] App «Marker»: Kurznotiz wählen (Kollision, Fehlt, Prüfen, Zu eng), zeigen, kurzer Pinch: Nadel im Modell.
- [ ] Am PC: Koordination › Kollisionen › «Problem-Marker»: der Marker steht in der Liste; Sprung in die 3D-Ansicht am Blickpunkt.
- [ ] BCF-Export der Marker lässt sich öffnen (ohne Bild der Ansicht).

## 3. PC-Bildschirm in der Brille (V1c)
- [ ] Rechte Hand offen, Handfläche nach links, Wisch von links nach rechts: der ganze Bildschirm des PCs gleitet von links ins Bild,
      leicht gebogen, frontal; das Modell bleibt dahinter starr.
- [ ] Bedienung mit Maus und Tastatur am PC; der Mauszeiger ist in der Brille sichtbar. Schrift lesbar.
- [ ] Am PC: roter Rand und «● Brille sieht den Bildschirm» mit «Beenden»; «Beenden» trennt sofort.
- [ ] Bei mehreren Bildschirmen wechselt «1/2» unter dem Bildschirm.
- [ ] Wegwischen schiebt den Bildschirm nach links hinaus.
- [ ] Die Brille hält dabei ruhig 72 Hz (keine Ruckler); die Verzögerung ist erträglich.

## 4. Bisheriges (Kurzprüfung)
- [ ] Springen mit dem Strahl, Drehen im Sitzen (Gegenrichtung 1 s gesperrt); die Etage wechselt nur in der App «Etagen» (Pinch + Wisch hoch/runter tut nichts).
- [ ] «Modell am Sitzungstisch» und «Raum in AR» hell, Griffe an den Ecken, zoomen bis 1:5.
- [ ] XR-Labor: Stellschrauben wirken sofort; Prüfaufgabe und Messprotokoll.

## Bekannt bzw. offen
- **Gestenlabor:** Der Erkennerkern ist gebaut und getestet (V1b-1); die App «Gesten» und das Register in KICAD folgen (V1b-2).
- **Pastelltöne** für Ebenen, Gesten, Labor und Vorgabe sind Vorschläge; 6 cm Luft über der Tischplatte ist eine Annahme.
- **SUS und NASA-TLX:** Wortlaut und Quelle der deutschen Fassungen fehlen (Christian).
- **WLAN (K3):** Netz auf «Privat» und `werkzeuge/xr_firewall.ps1` als Administrator.
- **Nur an der Brille prüfbar:** echte Hände vor Glas, Griffweite des seitlichen Felds, Lesbarkeit im Passthrough, Bandbreite und
  72 Hz mit dem PC-Bildschirm.
