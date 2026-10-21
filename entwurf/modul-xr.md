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

### XR-A – Begehen in VR (N23, im Bau seit 21.10.2026)
- Knopf «In VR begehen». Der Standpunkt ist der von «Raum begehen» (N15); die Augenhöhe kommt aus der echten Kopfhöhe.
- Bewegen: Stick zum Gehen, Snap-Turn zum Drehen, Teleport per Abzug bzw. Pinch auf den Boden. Keine Kollision.
- Wände sind undurchsichtig und werden nur unter 500 mm zum Kopf durchscheinend (N22).

### XR-B – Greifen (N24)
- Ein Element in Reichweite mit der Hand greifen: Es folgt der Hand mit Fang und bleibt beim Öffnen der Hand liegen.
- **Leitung:** antippen und greifen. Eine Bewegung nach oben, unten, vorne oder hinten verschiebt die ganze Leitung quer zur Längsachse; sie
  bleibt parallel. Dabei erscheinen die Masse lotrecht zur Decke und zu den Wänden. Die Verschiebung geht in **1-cm-Schritten**.
- **Nachbarn:** Anschliessende Formteile, Abzweige und Nachbarleitungen **bleiben an Ort, aber verbunden**. KICAD fügt Verzüge mit Bögen der
  Serie ein.
- Weit Entferntes mit Zeigen und Pinch heranholen.

### XR-C – Tablet an der linken Hand
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

### XR-D – «Hey Claude» (N25, nur VR)
- Das Weckwort öffnet die Claude-Eingabe; die Anweisung wird per Sprachaufnahme erfasst und geht wie getippt an Claude.
- Ändernde Befehle bestätigt man auf dem Tablet oder mit «ja».
- Spracherkennung und Weckwort möglichst lokal auf dem PC (Datenschutz; die Chrome-Erkennung sendet an Google). Entscheid am Freitag.

### XR-E – Problem-Marker mit Nachverfolgung (N26)
- Auf dem Tablet «Problem markieren» und auf die Stelle zeigen. Gespeichert werden Ort, Blickpunkt und Bild; die Notiz per Sprache ist
  freiwillig, Marker auch **ohne Beschreibung**.
- Liste zum Nachverfolgen (offen/erledigt, zuständig, Frist), am PC und auf dem Tablet. Ein Klick springt an die Stelle.
- Vorschlag: als BCF-Themen über den bestehenden BCF-Weg (`bcf_export`, `bcf_lesen`), damit Architekt und Fachplaner sie sehen.

## Reihenfolge
XR-A, dann XR-B mit XR-C, dann XR-E, dann XR-D. Jeder Teil wird mit der Brille an Christians PC abgenommen.

## Offen (Freitag bzw. erster Brillentest)
- Hand-Tracking über Air Link in Chrome
- Inhalt des Tablets
- lokale Spracherkennung
- Marker als BCF
- Tastenbelegung bzw. Gesten aus N15/N23 bestätigen
