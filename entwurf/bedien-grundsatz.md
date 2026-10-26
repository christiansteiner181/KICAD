# Bedien-Grundsatz H+K XR

Stand 26.10.2026 · Sitzung «Labor-App» · **Lebender Entwurf** – wird im XR-Labor (Spielwiese) mit Christian gefeilt.

Christian, 26.10.2026: «entwurf eines bedieninterface standards. Aufrufen von Apps: Bsp. Appmenue aufrufen, apps wählen, menue schliessen.
das labor wird durch dich bedient, wir feilen dann nur an diesem grundsatz. Dann bsp.: Ansicht von fenster usw.» – und: «spezielles soll
App-spezifisch entworfen werden.»

## Wozu

- Dieser Grundsatz regelt die **Grundbedienung**, die in «H+K XR» überall gleich ist – egal ob KICAD, XR-Labor oder ein späteres Werkzeug.
- **Spezielles wird ebenfalls im Labor entworfen, aber app-spezifisch** (Christian, 26.10.2026: «nein. wir entwerfen appspezifisch im labor.»):
  Je App ein eigener Teil (z. B. KICAD: Begehen, Messen, Modell am Tisch), erprobt auf der Spielwiese und danach in die App übernommen. Er
  darf der Grundbedienung nicht widersprechen.
- **Bauprinzip für alle VR-Bedienschnittstellen:** Christian, 26.10.2026: «ev machen wir auch eine Bedienschnittstelle in vr für andere apps und
  themen. diese werden dann auch aus basis und spez. teilen aufgebaut.» Jede VR-Bedienschnittstelle – KICAD heute, später andere Apps und Themen –
  besteht aus der **Basis** (dieser Grundsatz, überall gleich) und ihren **spezifischen Teilen** (je App bzw. Thema, eigenes Kapitel). Beides wird
  im Labor entworfen.
- Erprobt wird auf der **Spielwiese** im XR-Labor: neutrale Platzhalter-Apps, ich (Claude) baue und schalte Varianten, Christian probiert in
  der Brille und sagt, was bleibt. Jede Regel trägt ihren Stand («erprobt am …» oder «Annahme»).
- Die Zahlenwerte sind Stellschrauben (`kataloge/xr_stellschrauben.json`); hier steht der festgelegte Wert.

## Kapitel 1 – App-Menü: aufrufen, wählen, schliessen

Ausgangspunkt ist die heutige Lösung aus den Brillentests V1a–V1i (23.–25.10.2026, Einzelheiten `entwurf/modul-xr.md`). Christian, 26.10.2026:
«Nur die heutige Lösung» – Alternativen nur auf Zuruf.

### 1.1 Aufrufen
- **Geste:** beide Handflächen nach oben, deutlich nach oben streichen – als würde man die Apps von unten hineinheben («Hebegeste»).
- Ein kurzer **Anstoss genügt**; die Bewegung läuft dann von selbst zu Ende (V1e: «alle ein und aus wisch gesten sollen nur einen anstoss brauchen»).
- Ist eine App offen, schliesst die Hebegeste sie, und die Symbole stehen frontal (V1i).
- Stand: erprobt 24.–25.10.2026.

### 1.2 Anordnung der App-Symbole
- Runde **Glas-Symbole**, eines je App, ohne Fenster; **verteilt**, keine Leiste, höchstens **3 Reihen**, locker versetzt.
- **Frontal** in Körperrichtung, rund **0,5 m** vor der Person, Feld rund **0,6 m** hoch; im Raum verankert, bei jedem Öffnen gleiche Lage.
- **Höhe nach Haltung:** im Sitzen etwas höher, am Tisch rund 0,20 m tiefer als im Stehen.
- Farben: App-Pastellreihe (`kataloge/xr_farben.json`), Symbolsprache wie im CAD; Glas im Stil der Apple Vision Pro.
- Stand: erprobt 24.–25.10.2026.

### 1.3 Wählen
- **Berühren mit der ganzen Hand** (Handfläche oder Finger), nicht nur mit der Fingerspitze.
- Beim Annähern: Zeiger am Finger, das Symbol hebt sich, der **Name erscheint als Tooltip**.
- Rückmeldung bei der Wahl: **Leuchten und Ton**.
- Nach der Wahl ist erneutes Wählen kurz **gesperrt**, damit beim Neuanordnen nicht gleich die nächste App aufgeht.
- Stand: erprobt 24.10.2026.

### 1.4 Nach der Wahl
- Die **Symbole blenden aus** (V1g).
- Die App öffnet als **Glas-Tafel**, immer **mittig vor der Person und fest** – sie wandert nicht mit dem Blick (V1e).
- Stand: erprobt 24.–25.10.2026.

### 1.5 Schliessen
- **Eine Wegwisch-Geste für alles:** Hand offen (auch locker), Handfläche zur Seite, **von rechts nach links** wischen; die linke Hand
  sinngemäss. Ein Anstoss genügt.
- Geschlossen wird, was die Hand überstreicht; sonst **das zuletzt Geöffnete zuerst**.
- Gilt für App-Tafeln, das Symbolfeld und die Aufgabenkarte.
- **Keine Fehlauslösung:** Wische nach aussen am eigenen Rand des Blickfelds zählen nicht; ein Wisch nach aussen gilt nur, wenn er an oder
  jenseits der Körpermitte beginnt; kein Wisch weit unter dem Blick.
- Nach dem Schliessen einer Tafel kommen die Symbole **nicht** zurück, und eine Wahl ist kurz gesperrt.
- Stand: erprobt 24.–25.10.2026 («Ja, das Wegwischen geht jetzt gut»).

### 1.6 Blättern in einer offenen App
- Wisch **auf oder dicht vor der Tafel**: links/rechts eine Seite, hoch/runter rollen; mit beiden Händen.
- Ein Wisch im freien Raum behält seine Bedeutung (Schliessen, Hineinziehen).
- Stand: erprobt 24.10.2026.

### Werte (Stand 26.10.2026)

| Stellschraube | Wert | Bedeutung |
| --- | --- | --- |
| `hebe.weg_m` | 0,15 m | Weg der Hebegeste |
| `hebe.zeit_s` | 0,9 s | Zeit der Hebegeste |
| `hebe.handflaeche_grad` | 60° | Handfläche nach oben |
| `hebe.gleichzeitig_s` | 0,5 s | beide Hände gleichzeitig |
| `gesten.anstoss_weg_m` | 0,045 m | Anstoss: Weg |
| `gesten.anstoss_tempo_ms` | 0,35 m/s | Anstoss: Tempo |
| `gesten.offen_ab` | 1,4 | lockere Hand gilt als offen ab |
| `symbole.abstand_m` | 0,5 m | Abstand des Symbolfelds |
| `symbole.hoehe_m` | 0,22 m | Mitte des Symbolfelds (Stehen) |
| `symbole.feldhoehe_m` | 0,6 m | Höhe des Symbolfelds |
| `symbole.reihen` | 3 | Reihen |
| `symbole.hoehe_sitzend_m` | 0,08 m | im Sitzen höher |
| `symbole.hoehe_tisch_m` | 0,52 m | Mitte am Tisch |
| `symbole.wahl_frei_s` | 0,3 s | Sperre nach der Wahl |
| `symbole.beruehr_sperre_s` | 0,6 s | Sperre nach einer Geste |
| `symbole.sperre_schliessen_s` | 0,5 s | Sperre nach dem Schliessen |
| `zeiger.tooltip_s` | 0,6 s | Verzögerung des Tooltips |
| `tafel.hoehe_m` | 0,15 m | Höhe der Tafel |
| `wegwischen.weg_m` | 0,18 m | Wegwischen: Weg |
| `wegwischen.zeit_s` | 0,6 s | Wegwischen: Zeit |
| `wegwischen.handflaeche_grad` | 60° | Handfläche seitlich |
| `wegwischen.richtung_grad` | 45° | Richtung |
| `gesten.randwinkel_grad` | 25° | Wisch nach aussen: Rand des Blicks |
| `gesten.mitte_toleranz_m` | 0,05 m | Wisch nach aussen: ab Körpermitte |
| `gesten.unten_grad` | 45° | kein Wisch unter dem Blick |
| `tafel.blaettern_abstand_m` | 0,05 m | Blättern: Hand auf der Tafel |
| `tafel.blaettern_weg_m` | 0,08 m | Blättern: Mindestweg |

### Offen für die Spielwiese
- Wie sieht das Menü aus, wenn es **mehr Apps** gibt, als in 3 Reihen passen?
- Braucht es eine **Startseite der Werkzeuge** (E1) zusätzlich zum App-Menü, oder wird sie Teil davon?

## Kapitel 2 – Fenster (folgt)

Ansicht, Lage, Grösse, mehrere Fenster, Schliessen – wird nach Kapitel 1 auf der Spielwiese entworfen.
