# Gesamtprüfung KICAD – Bereich HKAP-Anbindung, Hülle, Lagemanager (Runde 5)

Zweig `worktree-agent-a25c8d566c90c6c8e`, Ausgangsstand `8cb84b8`. Gelesen: `src/Hlks.Kern/Austausch/Hkap*.cs` (Basis, Klient, Vertrag,
Abbildung, Abgleich, Befehle, Pendenzen, Stränge) vollständig, `src/Hlks.Mcp/WerkzeugeHkap.cs`, `js/oberflaeche/aktionen-hkap.js`,
`tests/Hlks.Kern.Tests/HkapTestkopieTests.cs`; `src/Hlks.Kern/Huelle/` vollständig (Aufbau, Fenster, Material, Raumgrenzen mit Abfragen und
Wärmebrücken); `Leitungen/Lagen.cs`, `js/werkzeuge/lagen-daten.js`, `js/oberflaeche/aktionen-lagen.js`. Nicht noch einmal geprüft:
Geometrie der Raumgrenzen im Einzelnen (Bericht `architektur_aussparungen.md`), Heizlast (`rechenkerne.md`), Sperren der KI für `hkap_*`
(`ki_schnittstelle.md`), `hkap-text.js` nur über seinen Test. Nichts auf W:, nichts an HKAP gesendet; die Testkopie-Tests laufen ohne
`HLKS_HKAP_TESTKOPIE=1` nicht.

## 1. Kurzfazit

Der HKAP-Abgleich ist sauber gebaut: Drei-Wege-Vergleich, nie automatisch löschen, Versionen beim Schreiben, Basis erst nach Erfolg, und ein
gescheitertes Kennungspaket fängt der nächste Abgleich über Basis bzw. `kicad.id` wieder auf – dort habe ich keinen Fehler gefunden, nur
Härtungen. Die Rechnung der Hülle stimmt (R = Σ d/λ, U = 1/(Rsi + R + Rse), Uw mit Glasrand), Kennwerte ohne Quelle gelten nicht. Der wichtigste
Befund ist fachlich: **der U-Wert einer Raumgrenze hängt nicht von ihrer Angrenzung ab** – die Gruppe «erdreich» der Übergangswiderstände kann
gesetzt werden, wird aber nirgends angewendet, und Innenwände bzw. Decken von oben rechnen mit den Rsi/Rse der Aussenseite; das geht so in die
Heizlast. Beim Lagemanager waren zwei echte Fehler: «12cm» galt als 12 mm, und die Optionsleiste behielt nach einem Projektwechsel die Lagen
des letzten Projekts. Vier Korrekturen gemacht, alle gefiltert grün.

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Test | Commit |
|---|---|---|---|---|---|
| 1 | `src/Hlks.Kern/Huelle/AufbauRechnung.cs` | Die Herkunft von R nennt die λ-Quellen je Material. Die Prüfung «schon genannt» suchte «MAT-1 » statt «MAT-1: » und traf nie – ein Material in zwei Schichten stand doppelt. | Vergleich auf «Kennzeichen: ». Rechenwert unverändert. | `HuelleAufbauTests` (Rechnung_MehrereSchichtenSummieren ergänzt) – grün | `7fb7ce5` |
| 2 | `src/Hlks.Kern/Leitungen/Lagen.cs` (`Lesen`) | «cm» und «mm» wurden nur gestrichen: «12cm, 24cm» (Christians eigene Schreibweise vom 05.11.2026) ergab 12 und 24 mm. Ein Wert mit Minus («-120», «UKD −12cm») fiel still weg; die Lagen dahinter rückten um eine Nummer vor, bei lauter Minuswerten galt wieder die Vorgabe. | «cm» × 10; ein Minus davor zählt als Abstand unter UK roh Decke. Zahlen ohne Einheit wie bisher mm. | `LagenTests.Lagen_EinheitCmUndMinus_WieGeschrieben` (neu) – grün | `4b5603d` |
| 3 | `src/Hlks.Kern/Huelle/Raumgrenzen.cs` (`Nordwinkel`) | Nordwinkel aus der ersten Projektinformation im Modell, Rsi/Rse und HKAP-Verknüpfung aus der ersten nach Kennzeichen – bei zwei Projektinformationen verschiedene Elemente. | `HuelleHilfe.Projektinfo` auch hier. | `RaumgrenzenTests` – grün | `ce77d14` |
| 4 | `src/Hlks.Web/wwwroot/js/werkzeuge/lagen-daten.js` | Der Stand der Lagen wurde nur beim Öffnen und Speichern des Lagemanagers geladen. Nach einem Projektwechsel galten die Lagen des letzten Projekts weiter (Treffer über das Kennzeichen «EG»); eine neue Decke aus dem IFC, eine geänderte Kote oder Rückgängig wirkten erst nach dem Neuladen der Seite. | Modell neu geladen → Stand leer; Geschoss, Gebäude oder Decke geändert bzw. etwas gelöscht → beim nächsten Zugriff neu geholt, bis dahin gilt der alte Stand (das Feld «Lage» bleibt gefüllt). Ein überholtes Laden schreibt nicht mehr. | `node --test js/werkzeuge/lagen-daten.test.mjs` (neu) – 1 von 1 grün | `0287d46` |

Gefilterte Läufe: `HuelleAufbauTests|LagenTests|RaumgrenzenTests` 64 von 64 grün; zum Stand des Bereichs einmal
`Hkap|HuelleAbfragenTests|HuelleFensterTests|HuelleMaterialTests|RaumgrenzenWaermebruecken` 120 von 120 grün, `js/oberflaeche/hkap.test.mjs`
41 grün, 0 rot (1 übersprungen). Kein Test war vorher rot.

## 3. Gefunden, nicht korrigiert

### Hülle

1. **U-Wert unabhängig von der Angrenzung; Gruppe «erdreich» wirkt nie** – `Huelle/AufbauRechnung.cs:76` (`Rechnen(m, aufbau, gruppe)`),
   `AufbauNachfuehrung.cs:73`, `AufbauAbfragen.cs:33/136`, `Raumgrenzen.cs:176–183` (`UWert`), Verwendung `Heizung/Heizlast.cs:286 ff.`
   Jeder Aufrufer rechnet ohne `gruppe`; nur der Test `Rechnung_ErdreichGruppeKannVerlangtWerden` gibt sie mit. `Aufbau.uWert` ist damit ein
   Wert je Aufbau mit den Rsi/Rse der Wärmestromrichtung aus der Art (Wand horizontal, Decke aufwärts, Boden abwärts), und die Raumgrenze liest
   ihn unverändert – auch wenn dieselbe Wand an einen unbeheizten Raum oder ans Erdreich grenzt, und auch wenn eine Decke vom oberen Raum aus
   ein Boden ist (Wärmestrom abwärts). `uebergang_setzen erdreich` und der Dialog «Übergang …» nehmen die Werte entgegen, sie ändern aber nichts.
   Handbeispiel mit den frei gewählten Testwerten der Tests (keine Norm): R = 2.94, Rsi = 0.13, Rse = 0.04 → U = 1/3.11 = 0.3215. Dieselbe Wand
   gegen einen Innenraum mit Rsi auf beiden Seiten: 1/3.20 = 0.3125 (−2.8 %); gegen Erdreich mit Rse = 0: 1/3.07 = 0.3257 (+1.3 %). Bei schlecht
   gedämmten Bauteilen (R klein) wird der Unterschied gross. Vorschlag: U je Raumgrenze aus `rWert` + Rsi/Rse der Gruppe rechnen, die aus Art
   **und** Angrenzung folgt (in `RaumgrenzenLauf.Neu`), `uQuelle` nennt die Gruppe; ein ausdrücklicher U-Wert bleibt, wie er ist. Welche
   Gruppen und Werte für «innen beidseitig» gelten, ist ein Normentscheid (Frage 1). Aufwand mittel, Priorität **hoch**.
2. **Wandfläche über die ganze Raumhöhe, auch wenn die Wand nur einen Teil überdeckt** – `Raumgrenzen.cs:415/528` (Kandidaten nach
   Höhenüberdeckung) und `:619–623` (`Seitenflaeche` mit `r.Unten … r.Oben`). Eine Brüstung von 1000 mm in einem 2600 mm hohen Raum zählt mit
   Länge × 2600 mm als diese Wand; der Teil darüber erscheint nicht als «offen». Gewollt für Split-Level (Kopfkommentar), für halbhohe Wände
   aber zu viel Fläche auf dem falschen Bauteil. Vorschlag: Hinweis «Wand überdeckt den Raum nur über x mm» oder Teilung nach Höhe. Aufwand
   mittel, Priorität mittel.
3. **Geschoss ohne Kote macht seine Räume zum «untersten Geschoss»** – `Raumgrenzen.cs:305` (nur Geschosse mit Kote werden geordnet) und
   `:340` (`Unterstes = geschoss is null || gu is null`). Fehlt einem Obergeschoss die Kote, wird jeder Bodenrest dort «erdreich» statt
   «aussen», ohne Hinweis. Vorschlag: Hinweis «Geschoss ohne Kote» und `Unterstes` nur bei bekannter Kote. Aufwand klein, Priorität mittel.
4. **Vorgabe «unbeheizt»/«beheizt» am Bauteil ohne Raum dahinter** – `Raumgrenzen.cs:752–759`. Die Angrenzung wird die Vorgabe, `Nachbar`
   bleibt «aussen» und `NachbarRaum` leer; die Heizlast rechnet dann mit bu = 1 (Annahme «sichere Seite»), weil kein Nachbarraum da ist.
   In der Liste steht «Nachbar aussen, Angrenzung unbeheizt». Vorschlag: Nachbar in diesem Fall «unbeheizt (Vorgabe)» und in der Heizlast
   den Hinweis nennen. Aufwand klein, Priorität tief.
5. **Terrain ist eine Kote je Gebäude** – `Raumgrenzen.cs:339, 622–623`. Am Hang ist jede Aussenwand bis zur selben Höhe «erdreich». Die
   Vorgabe `Bauteil.angrenzung` hilft nur für die ganze Wand, nicht für eine Teilhöhe. Nur genannt (Spezifikation: «Vorgabe ±0»). Priorität tief.
6. **Nur homogene Schichten** – `AufbauRechnung.cs:89–116`. Eine Schicht ohne λ (ruhende Luftschicht, Schicht mit bekanntem R aus dem
   Datenblatt) lässt sich nicht eingeben; der Aufbau hat dann kein R und kein U, es bleibt nur der ausdrückliche U-Wert. Vorschlag: Schicht mit
   `r` und Quelle statt Material. Aufwand mittel, Priorität tief (Frage 3).

### Lagemanager

7. **Betondecke nur über den IFC-Text erkannt** – `Lagen.cs:45` (`d.Text("aufbau")` enthält «beton»). Eine Decke, deren Aufbau über
   `aufbau_zuweisen`/`decke_erzeugen` als Bezug gesetzt ist (Aufbau AUF-n mit Schicht «Beton»), gilt nicht als Betondecke; dann zählt die
   grösste Decke. Vorschlag: zusätzlich Name und Schichtmaterialien des bezogenen Aufbaus prüfen. Aufwand klein, Priorität mittel.
8. **UK roh Decke je Rohr neu gerechnet** – `Lagen.cs:89–101` (`LageVon`), `:153–166` (`GleicheLageDavor`), `Grafik/Leitungsbezeichnung.cs:73, 89`.
   Jeder Aufruf geht über alle Decken des Modells und rechnet ihre Flächen; im Grundriss je Rohr ein- bis viermal. Bei einigen tausend Rohren
   und einigen hundert Decken sind das Millionen Flächenrechnungen je Plan (nicht gemessen, aus dem Code abgeschätzt). `Grafik.Plan` läuft nicht
   in `Wandgeometrie.Zwischenspeichern`, der vorhandene Zwischenspeicher greift dort also nicht. Vorschlag: UK roh Decke je Geschoss einmal je
   Planlauf. Aufwand klein bis mittel, Priorität mittel.
9. **Rundung der UK roh Decke** – `Lagen.cs:51`: `Math.Round(x / 10) * 10` rundet zur geraden Zahl (2645 → 2640, aber 2655 → 2660). Mit der
   Toleranz von 5 mm (`Lagen.Toleranz`, Annahme) kann ein Rohr genau an der Grenze seine Lage verlieren. Nicht geändert, weil bestehende
   Höhen sich um 10 mm verschieben könnten. Aufwand klein, Priorität tief.
10. **Lagemanager speichert je Geschoss ein eigenes Paket** – `aktionen-lagen.js:42–58`. Bei fünf geänderten Geschossen fünf Schritte in
    Rückgängig; scheitert das dritte, sind zwei gespeichert und der Dialog bleibt offen. Vorschlag: ein Sammelbefehl. Aufwand klein, Priorität tief.
    (Aus `leitungen.md` bekannt und nicht wiederholt: `LageVon` bei gemischten 2D/3D-Punkten und bei Gefälleleitungen.)

### HKAP

11. **Antwort von HKAP einem falschen Datensatz zugeordnet, wenn die Id fehlt** – `Austausch/HkapAbgleich.cs:676–677`,
    `HkapBefehle.cs:405–406`. Zuerst gilt die Antwort an derselben Stelle; passt ihre Id nicht, wird nach der Id gesucht, und wird nichts
    gefunden, gilt wieder die an derselben Stelle – auch wenn sie die Id eines anderen Datensatzes trägt. Liefert HKAP einmal weniger oder
    anders geordnete Ergebnisse, bekommt ein Datensatz den Status eines anderen (Basis und Zähler falsch); neue Datensätze (ohne Id) hängen
    ganz an der Reihenfolge. Vorschlag: Rückfall auf die Stelle nur, wenn deren Id leer ist; sonst «keine Antwort zu diesem Datensatz». Mit der
    Attrappe testbar. Aufwand klein, Priorität mittel.
12. **HKAP-Adresse frei wählbar** – `HkapKlient.cs:39–46`, `HkapBefehle.cs:144` (`hkap_verknuepfen url`), `:463` (`hkap_verbindung url`).
    Jede http(s)-Adresse wird angenommen und in der Verknüpfung gespeichert; `hkap_senden` schickt Geschosse, Räume und Stränge danach
    dorthin. Die Gegenprüfung (Kürzel, Name) schützt vor dem falschen Projekt, nicht vor dem falschen Server. Vorschlag: ohne Rückfrage nur
    localhost bzw. eine Liste erlaubter Rechner; sonst Bestätigung im Dialog «Verbinden» (Frage 4). Aufwand klein, Priorität mittel.
13. **Befund-Schlüssel der Pendenzen enthält den Befundtext** – `HkapPendenzen.cs:2, 63` (`Code|Element|Text`). Ändert sich eine Zahl im Text
    (z. B. eine Länge), ist es für HKAP ein neuer Befund: die alte Pendenz bekommt «Befund in KICAD behoben – Vorschlag: erledigt», daneben
    entsteht eine neue. Vorschlag: Schlüssel ohne die veränderlichen Teile (Code + Element + fester Teil) – berührt alle Prüfungen, darum
    nur genannt. Aufwand mittel, Priorität mittel.
14. **`hkap_raum_zuordnen` prüft nicht, dass der Raum gezeichnet ist** – `HkapBefehle.cs:266`. Beschreibung und Dialog sagen «gezeichneter
    Raum»; der Kern nimmt auch einen zweiten nicht platzierten Raum, der Platzhalter geht darin auf. Vorschlag: ablehnen ohne Polygon.
    Aufwand klein, Priorität tief.
15. **Testkopie-Tests kehren früh zurück statt zu überspringen** – `HkapTestkopieTests.cs:29, 97`. Ohne `HLKS_HKAP_TESTKOPIE=1` zählen sie
    als «bestanden», obwohl nichts geprüft wurde. Vorschlag: `Assert.Skip` bzw. `SkippableFact`, damit die Zahl ehrlich ist. Aufwand klein,
    Priorität tief.
16. **`HkapKlient.Aufruf`** – `HkapKlient.cs:137–152`: scheitert das Lesen des Antworttexts (Zeitlimit mitten in der Antwort), wird die
    Antwort nicht freigegeben; der Aufruf ist synchron und hält einen Server-Thread bis 5 s. Bei einem Arbeitsplatz ohne Folge. Priorität tief.

### Sonstiges

17. Bei jedem Commit meldet Git `failed to delete '…/.git/worktrees/wt_b1'` und `wt_b2` (Permission denied) – verwaiste Einträge früherer
    Arbeitskopien im gemeinsamen `.git`. Die Commits sind trotzdem da. Der Hauptlauf kann sie mit `git worktree prune` aufräumen, sobald
    kein Prozess die Ordner mehr hält.

### Geprüft und in Ordnung (Handrechnungen)

- **Aufbau:** Schichten 200 mm λ 0.5, 100 mm λ 0.04, 20 mm λ 0.5 (Testwerte): R = 0.4 + 2.5 + 0.04 = 2.94; mit Rsi 0.13, Rse 0.04:
  U = 1/3.11 = 0.3215 W/(m²·K) – stimmt mit dem Kern. Dicke in mm wird durch 1000 geteilt, λ ≤ 0 und Kennwerte ohne Quelle fallen heraus,
  ein ausdrücklicher U-Wert geht vor und wird nie überschrieben.
- **Fenster:** b = 1.0 m, h = 1.5 m, Rahmenanteil 0.25, Ug 0.6, Uf 1.2, ψg 0.04 (Testwerte): A = 1.5, Af = 0.375, Ag = 1.125,
  lg = 2·2.5·√0.75 = 4.330 m; Uw = (0.675 + 0.45 + 0.1732)/1.5 = 0.8655. Die Formel für lg (ähnliche Verglasung: Umfang × √(1 − Rahmenanteil))
  stimmt geometrisch; Rahmenanteil 1 gibt Uw = Uf, 0 gibt Ug + Umfang·ψg/A.
- **Öffnungen abziehen:** je Kantenstück und je Höhenteil (über/unter Terrain) auf Stück und Raumhöhe abgeschnitten, als eigene Raumgrenze
  mit derselben Normale geführt; Wandfläche nie negativ (Hinweis).
- **Orientierung:** Kante in +x, Raum links → Normale (0, −1) → Azimut 180° (Süd); mit Nordwinkel 30° → 150°. Stimmt mit der Definition
  (Nordwinkel = Winkel von Projekt-Nord zu geografisch Nord im Uhrzeigersinn).
- **Höhen zwischen Geschossen:** EG-Raum 0 … 2600 (lichte Höhe), OG-Raum ab 2900: der EG-Raum liegt «darunter» (beginnt tiefer, endet
  ≤ 2900 + 1300, endet über der EG-Kote); ein UG-Raum −2700 … −100 nicht (endet nicht über der EG-Kote) – zwei Geschosse tiefer zählt nie.
- **Lagen:** Kote 2900, Decke OK 5800, Dicke 250 → UK roh Decke 2650 über Kote; Lage 1 (120) = 2530 über Kote; Rohrachse auf 5430 absolut →
  Abstand 2900 + 2650 − 5430 = 120 → Lage 1. Geschosshöhe ohne Angabe = Abstand zur nächsten Kote, sonst 3000.
- **HKAP-Geschoss anlegen:** Geschosshöhe = Abstand zur nächsthöheren HKAP-Etage, sonst 3000 mm als ausdrücklich bezeichneter Platzhalter.
- **HKAP-Abgleich:** ohne Basis gilt eine leere Seite als unverändert; einseitig geführte Felder überschreiben nie mit leer; ein mit «KICAD»
  entschiedener Konflikt wird erst mit dem nächsten Senden geschrieben; Rückgängig einer Übernahme stellt die Basis zurück; nach einem
  gescheiterten Kennungspaket findet der nächste Abgleich die Elemente über Basis (Geschoss, Raum) bzw. `kicad.id`/`kicad.straenge`.

## 4. Offene Fragen an Christian

1. **U-Wert je Angrenzung (Punkt 1):** Soll KICAD den U-Wert je Raumgrenze mit den Übergangswiderständen ihrer Angrenzung rechnen (aussen,
   Erdreich, innen gegen unbeheizt bzw. beheizt; Decke von unten und von oben)? Wenn ja: Welche Norm und Tabelle gibt die Gruppen vor –
   reicht «horizontal, aufwärts, abwärts, Erdreich» oder braucht es «innen beidseitig» als eigene Gruppe? Ich habe keine Werte gesetzt.
2. **Halbhohe Wände (Punkt 2):** Soll eine Wand, die den Raum nur zum Teil überdeckt, nur mit ihrer Höhe zählen und der Rest als «offen»
   gemeldet werden?
3. **Luftschichten und Schichten mit bekanntem R (Punkt 6):** Kommen sie in deinen Aufbauten vor, oder genügt dafür der ausdrückliche
   U-Wert aus dem Datenblatt?
4. **HKAP-Adresse (Punkt 12):** Darf KICAD an jede Adresse senden, oder nur an diesen Rechner (und allenfalls den Server im Büro)?
5. **Lagen-Schreibweise (Korrektur 2):** «−12cm» gilt jetzt als 120 mm unter UK roh Decke. Ist das so gemeint, oder soll der Lagemanager
   nur Zahlen in mm annehmen und alles andere ablehnen?
6. **Betondecke (Punkt 7):** Genügt «Beton» im Namen bzw. in den Schichten des Aufbaus als Erkennung, auch für von Hand zugewiesene Aufbauten?
