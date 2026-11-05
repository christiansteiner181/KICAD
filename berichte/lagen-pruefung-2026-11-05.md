# Prüfung Lagen (Lagemanager) – 05.11.2026

Prüfprojekt (fiktiv): `%USERPROFILE%\HLKS_CAD_lagen_pruefung\e_KICAD` – «Lagen Prüfung (fiktiv)», Geschoss EG (Kote 0, Höhe 3000,
**UK roh Decke 2800**, Lagen **120, 240, 360** → Achshöhen 2680 / 2560 / 2440), Testrohrserie, Ansichten AN-EG-GROB und AN-EG-FEIN (1:50).

| Leitung | Verlauf | erwartet |
|---|---|---|
| KW DN 20 | 1. Lage über zwei Bögen, Lagewechsel (−120 mm) auf die 2. Lage | ein «/» am Anfang, keines nach den Bögen; nach dem Wechsel «//»; Kreis am Wechsel, höhere Leitung läuft in die Mitte, tiefere endet am Kreis |
| WW DN 15 | 2. Lage, Lagewechsel auf die 3. Lage | «//» am Anfang, nach dem Wechsel «///» |
| KW DN 20 | Höhe 2000 (keine Lage) | kein Lagezeichen |

Prüfpunkte
1. Register Heizung/Sanitär › Leitungen › **Lagemanager**: EG zeigt UK roh Decke 2800 (von Hand) und L1–L3 mit 2680 / 2560 / 2440.
2. Werkzeug **Rohr**: Feld «Lage» – L2 wählen setzt die Höhe auf 2560.
3. Grundriss grob und fein: Schrägstriche wie in der Tabelle, schwarz, 45° zur Leitung, Anzahl = Lage, nur bis zum nächsten Wechsel.
4. Lagemanager: UK roh Decke leeren → «aus dem Modell» (hier ohne Decke: «fehlt»), die Lagezeichen verschwinden.
5. Lagen ändern (z. B. «150, 300») → Rohre auf 2680 haben keine Lage mehr, kein Zeichen.

Offen: Darstellung der Lagezeichen am Bildschirm im Desktop erst mit dem nächsten geprüften Stand (Prüfung läuft).
