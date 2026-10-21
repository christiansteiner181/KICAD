# Modul XR – KICAD in der VR-Brille (Meta Quest)

Stand 21.10.2026 · Hauptlauf · Christian: «wir fassen das zusammen als Modul XR». Hier sind die Ideen N23–N26 aus `vorrat_freitag.md`
zusammengefasst; alle Aussagen von Christian vom 21.10.2026.

## Rahmen
- **Gerät und Weg:** Meta Quest über **Air Link (WLAN)**. KICAD läuft in **Chrome am PC**; WebXR geht über die OpenXR-Schnittstelle der
  Meta-Quest-Link-App («Weg 1»). Der PC rechnet die Grafik.
- **Kein Zeichnen in VR:** «ich möchte nicht zeichnen sondern mit der hand greifen». Gezeichnet wird weiter am PC.
- **Bedienung** mit Controllern und mit den Händen (Hand-Tracking). Ob das Hand-Tracking über Air Link in Chrome ankommt, ist beim ersten
  Test mit der Brille zu prüfen; sonst bleibt dafür der Browser der Quest.
- **Eine Tür:** Jede Änderung in VR ist ein normaler Befehl des Kerns, mit Paket, Rückgängig und Abgleich wie am PC.

## Teile

### XR-A – Begehen in VR (N23, gebaut 21.10.2026; Start mit der Brille bestätigt 21.10.2026)
- **Erster Brillentest 21.10.2026 ~19:15 (Christian: «VR läuft in der Brille»):** Quest 3 im eigenen Browser, KICAD über `http://localhost:5130`, adb über WLAN (Weg 2). Bestätigt ist der Start der VR-Sitzung; Gehen, Drehen, Wände, Tablet, Greifen und Messen sind noch einzeln abzunehmen.
- VR-Startseite `/vr` mit Icon (21.10.2026, hlks-modul), Brillentest offen. Christian: «ich möchte in VR lediglich ein Icon anklicken um im Modell zu landen.» Ein Tipp startet VR; Startpunkt: `?raum=` › letzter Standpunkt (`vr.letzter_standpunkt`) › erster Raum im EG › 3D-Gesamtansicht. Hauptweg seit Christians Wunsch vom 21.10.2026 («die am pc eingestellten befehle vom VR gerät aufrufen»): **VR-Auftrag** vom PC (Rechtsklick Raum › «In VR begehen (Brille)» / «Im Sitzungsraum zeigen (Brille)», 3D «An die Brille senden»), die Brille liest ihn alle 2 s und wendet ihn auch live an. Einzelheiten: `OBERFLAECHE.md`, «Modul XR: VR-Startseite».
- Knopf «In VR begehen». Der Standpunkt ist der von «Raum begehen» (N15); die Augenhöhe kommt aus der echten Kopfhöhe.
- Bewegen: Stick zum Gehen, Snap-Turn zum Drehen, Teleport per Abzug bzw. Pinch auf den Boden. Keine Kollision.
- Wände sind undurchsichtig und werden nur unter 500 mm zum Kopf durchscheinend (N22).
- Umgesetzt in `ansichten/vr3d.js` und `vr-logik.js`. Einrichtung, Bedienung und Annahmen stehen in `OBERFLAECHE.md`, Abschnitt N23. KICAD muss dafür in Chrome über `http://localhost:<Port>` laufen.

- **VR-Auftrag vom PC** (Christian 21.10.2026: «ev. ist es einfacher die am pc eingestellten befehle (bsp. in sitzungsraum betrachten,
  oder in vr begehen) vom VR gerät aufzurufen. es bleibt ja immer die gleiche adresse auf localhost.»):
  - Am PC Rechtsklick auf einen Raum: «In VR begehen (Brille)» bzw. «Im Sitzungsraum zeigen (Brille)». KICAD merkt sich den Auftrag
    je Benutzer.
  - Die Startseite `/vr` in der Quest zeigt ihn, ein Tipp startet ihn.
  - Während der Sitzung wechselt ein neuer Auftrag vom PC Raum, Modus oder Etage ohne Tipp.
  - Im Bau mit der VR-Startseite (hlks-modul).

### XR-B – Greifen (N24)
- **Stand:** gebaut 21.10.2026 (hlks-modul), Brillentest offen. Greifen mit Faust bzw. Grifftaste, Leitung quer in 1-cm-Schritten mit Massen zu Decke und Wand, Strahl holt Entferntes heran; **im Kern fehlt** ein Befehl, der eine verbundene Leitung quer versetzt (Verzüge mit Bögen der Serie), und eine Reservierung auf Zuruf. Einzelheiten: `OBERFLAECHE.md`, Abschnitt «Modul XR».
- Ein Element in Reichweite mit der Hand greifen: Es folgt der Hand mit Fang und bleibt beim Öffnen der Hand liegen.
- **Leitung:** antippen und greifen. Eine Bewegung nach oben, unten, vorne oder hinten verschiebt die ganze Leitung quer zur Längsachse; sie
  bleibt parallel. Dabei erscheinen die Masse lotrecht zur Decke und zu den Wänden. Die Verschiebung geht in **1-cm-Schritten**.
- **Nachbarn:** Anschliessende Formteile, Abzweige und Nachbarleitungen **bleiben an Ort, aber verbunden**. KICAD fügt Verzüge mit Bögen der
  Serie ein.
- Weit Entferntes mit Zeigen und Pinch heranholen.

### XR-C – Tablet an der linken Hand
- **Stand:** gebaut 21.10.2026 (hlks-modul), Brillentest offen. Tablet im Hologramm-Stil mit Geste bzw. Taste Y links, Eigenschaften, Rückgängig/Wiederholen, Augenhöhe, Schnittbereich, IFC, «VR beenden», Messwerkzeug mit Fang und Lot; Platzhalter «folgt».
- Wischgeste löscht die getroffene Masslinie (21.10.2026, hlks-modul), Brillentest offen.
- Die linke Hand öffnen und auf die Handfläche schauen öffnet ein Tablet mit interaktiver Bedienung und Werkzeugen. Bedient wird es mit dem
  Zeigefinger der rechten Hand; Hand schliessen bzw. wegdrehen schliesst es.
- Inhalt (Vorschlag, am Freitag festlegen):
  - Eigenschaften des gewählten bzw. gegriffenen Elements
  - Rückgängig/Wiederholen
  - Familie platzieren
  - Raum wechseln, Augenhöhe, Schnittbereich
  - Problem-Marker (XR-E)
  - zurück zum PC-Bild

  Technik: bestehende KICAD-Oberflächenteile als Fläche im VR-Raum.

- **Aussehen** (Christian 21.10.2026: «das tablet soll etwas futuristisch aussehen. mit transparenz, hologrammmässig»):
  - Durchscheinende Fläche mit leichtem Leuchten.
  - Helle Linien und Schrift in Cyan bzw. Blau, feines Raster bzw. Scanlinien, weicher Rand.
  - Kurzes Aufbauen beim Öffnen (Einblenden von der Hand aus) und Leuchten beim Antippen.
  - Lesbar bleiben: genug Kontrast vor hellen und dunklen Wänden, z. B. dunkler halbtransparenter Grund hinter der Schrift.
- **Messwerkzeug mit Fang** (Christian 21.10.2026: «noch ein messwerkzeug mit fängen. im tablet ergänzen»):
  - Auf dem Tablet «Messen» wählen.
  - Zwei Punkte mit Finger bzw. Strahl setzen. Die Punkte fangen auf Ecken, Kanten, Flächen, Leitungsachsen und Anschlüsse, mit
    sichtbarem Fangsymbol.
  - Angezeigt werden der Abstand und dx/dy/dz, in mm bzw. cm.
  - Weitere Funktionen: lotrecht zur Fläche messen (Decke, Wand, Boden), mehrere Messungen stehen lassen, «Messungen löschen».

### XR-D – «Hey Claude» (N25, nur VR)
- Das Weckwort öffnet die Claude-Eingabe; die Anweisung wird per Sprachaufnahme erfasst und geht wie getippt an Claude.
- Ändernde Befehle bestätigt man auf dem Tablet oder mit «ja».
- Spracherkennung und Weckwort möglichst lokal auf dem PC (Datenschutz; die Chrome-Erkennung sendet an Google). Entscheid am Freitag.

### XR-E – Problem-Marker mit Nachverfolgung (N26)
- Auf dem Tablet «Problem markieren» und auf die Stelle zeigen. Gespeichert werden Ort, Blickpunkt und Bild; die Notiz per Sprache ist
  freiwillig, Marker auch **ohne Beschreibung**.
- Liste zum Nachverfolgen (offen/erledigt, zuständig, Frist), am PC und auf dem Tablet. Ein Klick springt an die Stelle.
- Vorschlag: als BCF-Themen über den bestehenden BCF-Weg (`bcf_export`, `bcf_lesen`), damit Architekt und Fachplaner sie sehen.

### XR-F – Skizzenfläche per Rahmen-Geste
- **Geste:** Beide Hände bilden mit Daumen und Zeigefinger je ein L und damit zwei gegenüberliegende Ecken eines Quadrats. Dann wird eine
  durchscheinende Zeichenfläche im Raum vor der aktuellen Ansicht verankert. Grösse und Lage ergeben sich aus den beiden Ecken.
- **Zeichnen:** Mit dem Finger wird auf der Fläche frei skizziert. Das ist eine Skizze bzw. Notiz zum Modell, kein Modellieren; «nicht
  zeichnen» in XR-B meint Bauteile.
- **Vorschlag, am Freitag festlegen:**
  - Die Skizzenfläche bleibt im Raum stehen und gehört zu einem Problem-Marker (XR-E).
  - Am PC und im BCF erscheint sie als Bild mit Blickpunkt.
  - Löschen, Farbe und Strichstärke über das Tablet (XR-C).

### XR-G – Mehrere Personen (Frage Christian 21.10.2026: «ist das multiuserfähig?»)
- **Schon heute:**
  - Änderungen aus VR sind normale Befehle mit Paketen. Andere KICAD-Plätze sehen sie beim Abgleich, im Takt von etwa 2 s.
  - Gegriffene Elemente werden reserviert (Sperrdatei), damit niemand gleichzeitig daran arbeitet.
  - Marker und Liste (XR-E) sind für alle sichtbar.
  - Am PC lässt sich mitschauen, was die Brille zeigt (Spiegelbild von Air Link).
- **Fehlt für eine gemeinsame Begehung** (mehrere Brillen bzw. PC-Teilnehmer im selben Raum, mit Kopf und Händen als Figuren, Zeigestrahl,
  Sprache): ein Live-Kanal mit 20–30 Positionen je Sekunde. Der NAS-Abgleich ist dafür zu langsam.
- **Vorschlag:** Ein KICAD-Server im Büronetz führt die Begehung (WebSocket), die anderen treten bei. Gezeigt werden Figuren mit Namen, ein
  «Folgen»-Modus und ein Zeigestrahl, der für alle sichtbar ist. Ton läuft über die Brille bzw. Teams oder über einen eigenen Sprachkanal
  (am Freitag klären).

### XR-H – Sitzungsraum mit Modell auf dem Tisch (Christian 21.10.2026)
- **Brillentest 21.10.2026:**
  - Christian: «etwas weniger hart, aber tron optik ist gut» → weicher gemacht.
  - Christian: «greifen soll nur mit zwei händen funktionieren und nur wenn gegriffen. zoomen nur wenn gegriffen» → umgesetzt.
  - Christian: «etagenwahl zeigt decke, ev. 5cm weiter unten schneiden» → `SCHNITT_TIEFER_MM` = 50.
  - Fehler: Tisch und Modell standen 68 m daneben, weil der Kopf doppelt um das Rig versetzt war. Korrigiert in 0720e14; Christian: «Tisch und Modell stehen vor mir».
Christian: «eine art xr sitzungsraum …, wo das verkleinerte Modell auf einem tisch liegt und mit dem tablet die etagen ausgewählt werden
kann. das modell soll dann ebenfalls mit der hand gedreht und gezoomt werden können. wird bsp. 1. OG gewählt wird alles oberhalb der Decke
dieser etage ausgeblendet. Der raum soll aussehen wie in einer blackmirror folge.»
- **Stand:** erste Fassung gebaut 21.10.2026 (hlks-modul), Brillentest offen. Annahmen: Schnitt unter der Decke (Unterkante der Decke, ohne Decke im Modell die Oberkante), Aussehen des Raums, Tischmasse, Gesten-Schwellen. Einzelheiten: `OBERFLAECHE.md`, Abschnitt «Modul XR, Teil XR-H». Nicht in dieser Fassung: «Hier hineingehen», Mehrbenutzer (XR-G).
- **Ort:** Ein eigener virtueller Raum, kein Begehen. Er wird auf dem Tablet über «Sitzungsraum» bzw. «Begehen» gewechselt (XR-C).
  In der Mitte steht ein Tisch, darauf das ganze Modell verkleinert, zu Beginn 1:50 und auf die Tischplatte eingepasst.
- **Etagen:** Das Tablet listet die Geschosse aus dem Modell. Bei der Wahl z. B. «1. OG» wird alles oberhalb der Decke dieses Geschosses
  ausgeblendet. Die Technik ist dieselbe Schnittebene wie beim 3D-Schnittbereich (N13). Dazu gibt es «Alle Etagen».
  - Vorschlag: Der Schnitt liegt unter der Decke des gewählten Geschosses, damit man von oben hineinsieht; «Decke zeigen» schaltet um.
  - Festlegen am Freitag.
- **Hand:**
  - Mit einer Hand greifen: Das Modell dreht sich um die senkrechte Achse und verschiebt sich auf dem Tisch.
  - Mit zwei Händen greifen: Auseinanderziehen bzw. Zusammenschieben zoomt um den Mittelpunkt zwischen den Händen, etwa von 1:200 bis 1:20.
  - Das Modell bleibt waagrecht (gegen Übelkeit).
  - Der Massstab steht am Tisch; «Zurücksetzen» ist auf dem Tablet.
- **Zeigen:** Mit Strahl oder Finger auf ein Element bzw. einen Raum zeigen; die Eigenschaften erscheinen auf dem Tablet. Vorschlag:
  «Hier hineingehen» springt im Massstab 1:1 an diese Stelle ins Begehen (XR-A), «Zurück an den Tisch» führt zurück.
- **Aussehen** («wie in einer Black-Mirror-Folge», Vorschlag zum Abnehmen):
  - kühl, minimalistisch, technisch
  - dunkler Raum mit mattschwarzen Wänden und glänzendem, leicht spiegelndem Boden
  - schmale weisse Lichtlinien in Decke und Wänden, leichter Dunst
  - Glastisch mit leuchtender Kante
  - Modell hell und matt («Architekturmodell»); die HLKS-Leitungen in den Gewerkfarben der H+K-Koordinationspläne (Heizung rot,
    Lüftung blau, Sanitär grün), leicht leuchtend
  - keine Logos und keine Anlehnung an konkrete Szenen
- **Mehrere Personen:** Der Sitzungsraum ist der natürliche Ort für XR-G. Mehrere Personen stehen um den Tisch, jemand führt die Wahl
  der Etage, die anderen sehen sie mit. Dafür braucht es den Live-Kanal aus XR-G.
- **Leistung:** Im Quest-Browser (Weg 2) rechnet die Brille. Das verkleinerte Modell hat dieselbe Geometrie wie 1:1, deshalb gilt: IFC-Unterlage
  aus, nur sichtbare Geschosse zeichnen. Über Air Link rechnet der PC.

### Start ohne Link-fähigen PC (Frage Christian 21.10.2026: «dieser ist nicht mit meta link kompatibel»)
- **Empfehlung:** Weg 2 über den Browser der Quest, mit ADB-Portweiterleitung, ohne Meta Link und ohne zweiten Rechner.
  - Der KICAD-Server auf dem PC bleibt auf localhost.
  - `adb reverse tcp:5130 tcp:5130` macht ihn in der Quest als `http://localhost:5130` erreichbar. WebXR gilt dort als sicher, und die
    Ursprungsprüfung von KICAD bleibt unverändert.
  - Die Verbindung läuft per USB oder per ADB über WLAN.
  - **In KICAD eingebaut (21.10.2026): Dialog «Quest verbinden»** (Register Ansicht › Fenster, sowie Link «Quest im Browser der Brille nutzen …» beim grauen Knopf «In VR begehen»): Ampel, Verbinden, WLAN, Trennen, Pfad zur adb.exe, Einrichtung Schritt für Schritt. Das Skript `werkzeuge/quest_verbinden.ps1` bleibt als Alternative. Reine Rechner-Funktion, für die KI verboten (`KiEinordnung.RechnerFunktionen`); Beschreibung in `src/Hlks.Web/OBERFLAECHE.md`, Abschnitt «Modul XR: Quest verbinden».
  - Die Quest rechnet selbst, deshalb ist die IFC-Unterlage bei grossen Modellen aus. Hand-Tracking geht im Quest-Browser sicher.
  - **Idee MAC (Christian 21.10.2026: «könnte man nicht über MACadr. anstelle IP. dann müsste nur einmal eingerichtet werden», «ist die brillen Mac gespeichert, könnte man nach jedem programmstart neu suchen. automatisiert oder ein icon ohne manuellen eingriff»):** KICAD merkt sich die WLAN-MAC der Brille (`quest.brillen`), findet ihre IP in der Nachbartabelle des PCs bzw. mit einem Ping-Durchlauf im eigenen /24-Netz (höchstens einmal je 2 Minuten, nie VPN/TAP) und verbindet selbst (`quest.automatisch`, Statusanzeige «Brille» in der Statusleiste). Grenze: `adb tcpip` gilt nicht über den Neustart der Brille, danach einmal das Kabel.
    **Gebaut 21.10.2026 (hlks-modul), Brillentest offen** (echte Nachbartabelle, Ping-Durchlauf im Büronetz und `ip link show wlan0` der Quest nur mit der Brille prüfbar). Beschreibung: `src/Hlks.Web/OBERFLAECHE.md`, Abschnitt «Brille automatisch verbinden».
  - **Autostart beim Aufsetzen gebaut 21.10.2026 (hlks-modul), Brillentest offen** (Christian: «ich möchte nach dem klick am pc nur noch die brille anziehen, und dann startet die begehung direkt»): `QuestStarter.cs`, Einstellung `quest.autostart` (Vorgabe an), `GET /api/quest/autostart`. Beschreibung: `OBERFLAECHE.md`, Abschnitt «Modul XR: Autostart».
- **Befund 21.10.2026 an Christians PC** (Intel Core Ultra 9 386H, nur integrierte Grafik): Meta Horizon Link 1.115 ist installiert, lehnt die Grafik aber ab (Protokoll: «Failed to find a compatible GPU … USB Link disabled», GPU «UNSUPPORTED_CLASS»). Chrome findet deshalb keine VR-Laufzeit, «In VR begehen» bleibt grau. Was in der Brille den Windows-Desktop zeigt, ist Mixed Reality Link bzw. Meta Remote Desktop – nur ein Bildschirmbild, kein WebXR. **An diesem PC gilt nur Weg 2.**
- **Idee Christian 21.10.2026:** «könnte man nicht über MACadr. anstelle IP. dann müsste nur einmal eingerichtet werden.»
  - Vorschlag: Beim ersten Verbinden per Kabel merkt sich KICAD die WLAN-MAC der Quest (`adb shell cat /sys/class/net/wlan0/address`)
    zusammen mit der Seriennummer, je Benutzer.
  - Später findet «Verbinden» die aktuelle IP über die Nachbartabelle des PCs (Get-NetNeighbor). Steht sie dort nicht, folgt ein kurzer
    Ping über das eigene /24-Netz, dann `adb connect`. Die IP muss niemand mehr eintippen.
  - Grenze: Nach einem **Neustart** der Quest ist adb über WLAN aus, denn `adb tcpip` gilt nicht über den Neustart. Dann braucht es
    einmal das Kabel. Ruhezustand ist kein Problem.
  - Alternative ohne Code: feste IP für die Quest im Router (DHCP-Reservierung).
  - **Ergänzung Christian 21.10.2026:** «ist die brillen Mac gespeichert, könnte man nach jedem programmstart neu suchen. automatisiert
    oder ein icon ohne manuellen eingriff.» Vorschlag:
    - Einstellung «Brille automatisch verbinden» (je Benutzer).
    - Ist sie an, sucht KICAD beim Start und beim Öffnen einer 3D-Ansicht die gespeicherte MAC, verbindet und setzt die Weiterleitung
      ohne Klick.
    - Dazu eine Statusanzeige «Brille» in der Statusleiste: grün = bereit, grau = nicht gefunden. Ein Klick darauf sucht erneut.
    - Nach einem Neustart der Quest genügt es, das Kabel einzustecken: KICAD erkennt das USB-Gerät, stellt selbst auf WLAN um und meldet
      «Kabel kann ab».
    - Suche zuerst in der Nachbartabelle. Ein Ping nur im eigenen /24-Netz und nur auf Anlass, nie laufend; Rücksicht aufs Büronetz.
  - Am Freitag priorisieren.
- **Nicht empfohlen:** KICAD für das Büronetz öffnen (`http://<PC>:5130`). Ohne HTTPS gibt es kein WebXR, und der Server hat keine
  Anmeldung.
- Anderer, Link-fähiger PC mit eigenem KICAD: möglich. Dafür braucht es dort .NET und den Start aus einer eigenen Kopie. Die Regel «nie zwei
  Rechner gleichzeitig am Repository» gilt.

## Reihenfolge
XR-A, dann XR-B mit XR-C, dann XR-E, dann XR-D. Jeder Teil wird mit der Brille an Christians PC abgenommen.

## Offen (Freitag bzw. erster Brillentest)
- Hand-Tracking über Air Link in Chrome
- Inhalt des Tablets
- lokale Spracherkennung
- Marker als BCF
- Tastenbelegung bzw. Gesten aus N15/N23 bestätigen
- XR-H: Schnitt unter oder über der Decke des gewählten Geschosses, Aussehen des Raums, Platz in der Reihenfolge
