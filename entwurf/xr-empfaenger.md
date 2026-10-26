# H+K XR – eine Empfänger-App in der Brille für alle Werkzeuge: Spezifikation

Stand 26.10.2026 · Sitzung «Labor-App» (Zweig `claude/labor-app-9c82bf`) · **Freigegeben von Christian am 26.10.2026** («Spezifikation H+K XR freigegeben, starte E1»). Bau ab E1.

> **Neuausrichtung des Labors 26.10.2026 (nach E1, vor E2):** Christian: «ich habe das gefühl du verstehst nicht was ich mit dem prüflabor
> meine.» Geklärt im Fragefenster: Das Labor ist eine **Spielwiese zum Anfassen**. Dort entwerfen wir **beides** – die **Grundbedienung** für alle
> Apps (App-Menü aufrufen, wählen, schliessen; danach Fenster …) und die **app-spezifische Bedienung** je App («nein. wir entwerfen appspezifisch im
> labor.»). Claude baut und bedient das Labor, Christian probiert in der Brille, «wir feilen dann nur an diesem grundsatz». Ergebnis ist der
> **Bedien-Grundsatz** `entwurf/bedien-grundsatz.md`. Start: App-Menü mit der heutigen Lösung («Nur die heutige Lösung»).
>
> Folgen für diese Spezifikation: E1 gilt (eine App «H+K XR», Werkzeuge, Startseite). **E2 (abstrakte Bedienaufgaben, Bedienmuster-Schicht) entfällt**
> («spezielles soll App-spezifisch entworfen werden»); die angefangene Arbeit bleibt ungenutzt liegen. Messung, Szenarien und Fragebogen (E3) sind
> zweitrangig. Neue Stufen: **S1** Spielwiese App-Menü (Platzhalter-Apps im XR-Labor, heutige Bedienung) → Brillentest und Kapitel 1 feilen → **S2**
> Fenster → app-spezifische Teile je App nach Zuruf. E7/E8 (Gesten) bleiben.
Ergänzt `entwurf/labor-app.md` (XR-Labor) und `entwurf/modul-xr.md` (Quest App); wo sie sich widersprechen, gilt diese Datei.

## Anlass

Christian, 26.10.2026, nach L1 des XR-Labors: «ich will im Prüflabor unterschiedliche nicht KICAD spezifische interfaceideen ausprobieren.
für mich fühlt es sich an, als sei das testlabor noch zu stark ein teil von KICAD» – und zur Frage nach einer eigenen App-Hülle für das
Labor: «eine app für alles, Idee eine reciverapp die für alle tools genutzt werden kann.»

Befund dazu: Die Labor-Brille (L0) ist heute die KICAD-App mit eingehängten Labor-Teilen; Prüfaufgaben und Bedienideen sind KICAD-Funktionen
(Springen, Etage, Messen, Symbolfeld, Glas-Apps). Eine neue Bedienidee lässt sich nur als Variante einer KICAD-Funktion erproben.

### Entscheide Christian 26.10.2026 (Fragefenster)

| Frage | Entscheid |
| --- | --- |
| Fokus des Labors | **Bedienmuster allgemein**: Menüs, Listen, Regler, Auswählen, Bestätigen, Objekte greifen und platzieren – je mit mehreren Ideen im Vergleich; Gesten sind ein Teil davon. |
| Brillen-App | **Eine App für alles, eine Empfänger-App für alle Werkzeuge.** Bestätigte Lesart: Die App verbindet sich mit dem jeweiligen Werkzeug am PC (KICAD, XR-Labor, später weitere wie HKAP). Das Werkzeug liefert Inhalt und Aufgaben; die Bedienmuster leben im Empfänger und gelten für alle Werkzeuge gleich. Das Labor ist das Werkzeug, mit dem man die Muster erprobt und festlegt. |
| Werkzeug wählen | **Beides**: Ein Werkzeug am PC schickt einen Auftrag (wie heute «In VR zeigen»), und der Empfänger wechselt dorthin; ohne Auftrag zeigt er eine Startseite mit den gekoppelten Werkzeugen. |
| Name | **«H+K XR»** (App-Bibliothek der Quest). |
| KICAD-Prüfaufgaben | **Ein Szenario im Labor** («KICAD-Bedienung», Testraum aus L1) neben allgemeinen Szenarien. |
| Neue Ideen | **Als Modul, Claude baut**: Christian beschreibt eine Idee, ein Agent baut sie als Modul mit Stellschrauben; im Labor ein-/ausschaltbar und vergleichbar. |
| Bis zur Freigabe | **Bau anhalten**; L1 bleibt, wie es ist (am PC weiter nützlich). Die zweite APK aus L0 wird in dieser Spezifikation aufgelöst. |

## Leitlinien

1. **Das Werkzeug sagt *was*, der Empfänger *wie*.** Ein Werkzeug beschreibt eine Bedienaufgabe abstrakt («Menü mit diesen Einträgen»,
   «Wert zwischen 0 und 100», «Objekt hier platzieren», «Ja/Nein»). Welches Bedienmuster sie darstellt (Kreismenü, Glas-Tafel, Wischliste …),
   bestimmt der Empfänger nach dem Bedienstandard. So bedient sich jedes Werkzeug gleich, und ein neues Muster wirkt überall.
2. **Ein Empfänger, viele Werkzeuge.** Die App kennt Werkzeuge nur über ein gemeinsames Protokoll (Kopplung, Kanal, Szenen, Bedienaufgaben,
   Ereignisse). Werkzeugeigenes wie Begehen, Sitzungsraum oder Messen am Modell bleibt als Werkzeug-Modul im Empfänger, bis das Protokoll es
   allgemein ausdrücken kann.
3. **Das Labor ist ein Werkzeug wie jedes andere** – mit einem Vorrecht: Es darf Muster und Stellschrauben live umschalten, Aufgaben stellen
   und messen. Alle übrigen Werkzeuge bekommen nur den festgelegten Standard.
4. **Nicht von vorn bauen.** Die heutige KICAD-App ist der Ausgangspunkt: ihre Bausteine (Hände, Gestenkern, Glas, Ton und Licht, Symbolfeld,
   Tafel, Wischen, Pistole) werden zu den ersten Bedienmustern, ihre KICAD-Funktionen zum Werkzeug-Modul «KICAD».

## Ziel und «Fertig, wenn …»

**Fertig, wenn …**
- auf der Quest **eine** App «H+K XR» installiert ist; KICAD und das XR-Labor installieren bzw. aktualisieren sie mit demselben Knopf; die
  bisherigen Apps «KICAD» und «XR-Labor» sind ersetzt.
- «H+K XR» ohne Auftrag eine Startseite mit den gekoppelten Werkzeugen zeigt (Name, PC, erreichbar ja/nein) und man eines mit der Hand wählt;
  «In VR zeigen» in KICAD bzw. eine Aufgabe im Labor den Empfänger direkt dorthin wechseln lässt.
- KICAD in der Brille alles kann, was es heute kann (Begehen, Sitzungsraum, Messen, Marker, Desktop) – unverändert in der Bedienung, solange der
  Standard nichts anderes festlegt.
- im XR-Labor allgemeine Szenarien laufen (Bedienaufgaben ohne Gebäude) und daneben das Szenario «KICAD-Bedienung» (Testraum aus L1); für jede
  Bedienaufgabe lassen sich mindestens zwei Muster live umschalten und mit Zeit, Fehlgriffen, Abbrüchen und Fragebogen vergleichen.
- ein neues Muster, das Christian beschreibt, als Modul dazukommt, ohne dass KICAD oder ein anderes Werkzeug geändert wird, und nach «Als
  Standard festlegen» in KICAD wirkt.

## Aufbau

```
 Brille: «H+K XR» (src/Hlks.Quest → Empfänger, App-Kennung ch.hkplanung.xr)
 ┌────────────────────────────────────────────────────────────────────────────────────┐
 │ Empfänger-Kern: Kopplungen je Werkzeug, Startseite, Wechsel per Auftrag, Kanal,    │
 │                 Szenen (GLB), Ton/Licht, Hände, Gestenkern                         │
 │ Bedienmuster (Module): Symbolfeld, Glas-Tafel, Wischliste, Pistole, Hebegeste,     │
 │                 … neue: Kreismenü, Handflächen-Menü, Zwei-Hand-Regler …            │
 │ Werkzeug-Module: «KICAD» (Begehen, Sitzungsraum, Messen, Marker, Desktop)          │
 │                  «Labor» (Live-Umschalten, Aufgaben, Messprotokoll)                │
 │                  «allgemein» (jedes Werkzeug nur über Bedienaufgaben, ab E4)       │
 └──────────▲─────────────────────────────▲───────────────────────────────────────────┘
            │ TLS, Kopplung, Kanal        │
   KICAD (5130/5140/5141)          XR-Labor (5430/5340/5341)          später: HKAP u. a.
```

### Begriffe

| Begriff | Bedeutung |
| --- | --- |
| **Werkzeug** | Ein Programm am PC, mit dem der Empfänger gekoppelt ist (KICAD, XR-Labor, …). Kennung im Kopplungspaket und im `hallo`. |
| **Bedienaufgabe** | Was ein Werkzeug von der Person will, abstrakt: `auswahl` (aus N wählen), `menue` (Befehle), `liste` (blättern, rollen), `wert` (Zahl in Bereich), `bestaetigen` (ja/nein/abbrechen), `platzieren` (Ort im Raum), `greifen` (Objekt bewegen, drehen, skalieren), `text`/`zahl` (Eingabe, später). |
| **Bedeutung** | Wie in `labor-app.md` («blättern», «ausblenden», «auslösen» …): die Gesten- und Bedienwörter, die für alle Muster gleich sind. |
| **Bedienmuster** | Ein Modul im Empfänger, das eine oder mehrere Arten von Bedienaufgaben darstellt und bedient, mit Kennung, eigenen Stellschrauben und Messpunkten (z. B. «glas-tafel», «symbolfeld», «kreismenue»). |
| **Werkzeug-Modul** | Werkzeugeigener Teil im Empfänger, der (noch) nicht über Bedienaufgaben geht (KICAD: Begehen, Sitzungsraum, Messen; Labor: Aufgaben und Messung). |
| **Bedienstandard** | Wie in `labor-app.md`, erweitert: je Art von Bedienaufgabe das Muster und seine Stellschrauben, je Bedeutung die Geste. Gilt im Empfänger für alle Werkzeuge. |

### Empfänger-Kern

- **Eine App**, Kennung `ch.hkplanung.xr`, Name «H+K XR», eigenes Symbol (Entwurf an Christian). Quellen bleiben in `src/Hlks.Quest` (Umbenennen
  des Ordners nur, wenn es keine Konflikte mit laufenden Sitzungen gibt; Entscheid im Bau). `src/Hlks.Labor.Quest` (zweite APK aus L0) entfällt;
  die Marken `kicad`/`xrlabor` (`AppMarke`) werden zur **Werkzeug-Kennung**.
- **Mehrere Kopplungen:** je Werkzeug ein Kopplungspaket (KICAD 5140/5141, Labor 5340/5341). Das Paket bekommt ein Feld `werkzeug`
  (`kicad`, `xrlabor`, …) und einen Anzeigenamen. Installieren und Koppeln wie heute per Kabel über den Knopf des jeweiligen Werkzeugs; ein
  zweites Werkzeug fügt nur seine Kopplung hinzu, ohne die App neu zu installieren.
- **Startseite:** gekoppelte Werkzeuge als grosse Glas-Symbole im Bedienstil der Brille (Hebegeste, Berühren); erreichbar ja/nein über die Suche.
  Ein Werkzeug wählen = verbinden und dessen Inhalt zeigen; zurück zur Startseite über ein eigenes Symbol «Werkzeuge».
- **Auftrag vom PC:** Ein Werkzeug, das einen Auftrag schickt (KICAD «In VR zeigen», Labor «Aufgabe starten»), holt den Empfänger zu sich,
  auch wenn ein anderes Werkzeug offen ist (mit kurzer Meldung «Wechsel zu …»). Nur ein Werkzeug ist zur Zeit aktiv.
- **Zwischenspeicher je Werkzeug** (Modell, Katalog, wartende Protokolle), damit «Start ohne PC» wie heute je Werkzeug geht.

### Bedienmuster

- **Schnittstelle** (C#, in `src/Hlks.Xr/Bedienung` für die Logik, Darstellung in `src/Hlks.Quest`): Kennung, Name, welche Arten von
  Bedienaufgaben es kann, Stellschrauben (Namensraum `muster.<kennung>.…`), Bild für Bild: Bedienaufgabe + Hände + Blick hinein, Darstellung und
  Ereignisse (gewählt, geändert, abgebrochen, Fehlgriff) heraus. Die Messung (Zeit, Fehlgriffe, Abbrüche) zählt der Empfänger je Aufgabe gleich,
  egal welches Muster.
- **Erste Muster aus der heutigen App:** Symbolfeld mit Hebegeste (`auswahl`, `menue`), Glas-Tafel mit Blättern (`liste`, `auswahl`),
  Etagenliste (`liste`), Wegwischen (`bestaetigen`/abbrechen, schliessen), Pistole (auslösen in `auswahl` und `platzieren`), Zwei-Punkt-Griff
  (`greifen`). Sie verhalten sich wie heute; die Stellschrauben behalten ihre Namen (Zuordnung im Katalog).
- **Neue Muster** baut ein Agent nach Christians Beschreibung als eigenes Modul (eine Datei bzw. ein Ordner je Muster, Katalog-Eintrag mit
  Stellschrauben und Herkunft). Vorschläge zum Start, Auswahl durch Christian: Kreismenü um die Hand, Handflächen-Menü (linke Hand als Tafel),
  Wischliste ohne Tafel, Zwei-Hand-Regler, Zeigen und Halten statt Pinch, Raster im Raum.
- **Wahl des Musters:** Der Bedienstandard legt je Art von Bedienaufgabe das Muster fest; das Labor kann es live übersteuern (wie heute
  `labor.variante.*`; die zwölf heutigen Varianten werden zu Musterwahlen bzw. Stellschrauben).

### Bedienaufgaben im Protokoll

- **Kanal-Nachrichten** (Erweiterung von `XrNachrichten.cs`, Fassung erhöhen): `bedienaufgabe { id, art, titel, eintraege[] | bereich | ziel,
  vorgabe, abbrechbar }` vom Werkzeug, `bedienergebnis { id, ergebnis | abgebrochen, zeitMs, fehlgriffe, muster }` zurück; `bedienaufgabe.schliessen`.
- **Erste Nutzer:** das Labor (allgemeine Szenarien, ab E3) und KICAD für neue Dialoge; KICADs heutige Apps (Etagen, Messen, Marker …) ziehen
  schrittweise auf Bedienaufgaben um, ohne Eile (Stufe E5).
- **Später (E6):** Ein Werkzeug ohne eigenes Werkzeug-Modul (z. B. HKAP) bedient sich nur über Bedienaufgaben und Szenen (GLB) – dafür ist keine
  Änderung der App nötig.

### Das XR-Labor als Werkzeug

- PC-Teil wie L1 (`src/Hlks.Labor`, Labor-Seite, Ablage OneDrive, Übernahme). Neu auf der Seite: Register **Muster** (Liste der Bedienmuster mit
  Stellschrauben, live umschalten, A/B) und **Szenarien** (statt «Prüfaufgaben» nur für KICAD).
- **Szenario «Allgemein»** (neu, Vorschlag; Katalog `kataloge/labor_szenarien.json`): neutrale Umgebung (Testraum ohne Gebäudebezug oder heller
  Raum), Aufgaben aus Bedienaufgaben, z. B.:
  - aus 12 Einträgen den genannten wählen (`auswahl`)
  - in einer Liste mit 60 Einträgen zu Nummer 47 blättern (`liste`)
  - einen Wert auf 37 ± 1 einstellen (`wert`)
  - einen Würfel auf ein Ziel stellen und um 90° drehen (`platzieren`, `greifen`)
  - eine Meldung bestätigen bzw. abbrechen (`bestaetigen`)
  - ein Menü öffnen, einen Befehl wählen, wieder schliessen (`menue`)
- **Szenario «KICAD-Bedienung»:** die sieben Prüfaufgaben im Testraum aus L1 (Werkzeug-Modul KICAD mit dem Testraum statt eines Projekts).
- **Auswertung** je Aufgabe, Muster und Fassung der App (wie heute je Variante), Fragebogen wie heute.
- **Bedienstandard festlegen** wie in `labor-app.md` L2, erweitert um die Musterwahl je Art von Bedienaufgabe.

## Was aus L0/L1 wird

| Teil | Danach |
| --- | --- |
| `src/Hlks.Labor.Quest` (zweite APK «XR-Labor») | entfällt; der Labor-Teil wird Werkzeug-Modul «Labor» im Empfänger |
| `partial void`-Einhängepunkte in `QuestApp` | werden zur Grenze zwischen Empfänger-Kern und Werkzeug-Modulen (KICAD, Labor); der Probebau `HlksOhneLabor` bleibt als Prüfung, dass der Kern ohne Labor übersetzt |
| `AppMarke` (`kicad`, `xrlabor`) | wird Werkzeug-Kennung des verbundenen Werkzeugs, nicht mehr der App |
| `src/Hlks.Labor` (Server, Seite, Ablage, Testraum) | bleibt; Testraum wird Szenario «KICAD-Bedienung» |
| Desktop-Icon «XR-Labor», `labor_starten.ps1`, Netz-Skript | bleiben |
| `quest_app_bereitstellen.ps1 -Labor` | entfällt; eine APK für alle Werkzeuge |
| Bedienstandard (`labor-app.md`) | bleibt, erweitert um Musterwahl |
| Stufen L2–L4 aus `labor-app.md` | gehen in die Stufen unten auf (L2 → E3, L3 → E7, L4 → E8) |

## Stufen

| Stufe | Inhalt | Hängt ab von |
| --- | --- | --- |
| **E0** | Diese Spezifikation, Freigabe durch Christian | – |
| **E1 Empfänger-Hülle** | App «H+K XR» `ch.hkplanung.xr` aus `src/Hlks.Quest`; mehrere Kopplungen mit Werkzeug-Kennung (Paket-Feld `werkzeug`); Startseite mit Werkzeugen; Wechsel per Auftrag; Werkzeug-Module «KICAD» und «Labor» aus dem heutigen Code; `src/Hlks.Labor.Quest` entfällt; KICAD und Labor installieren dieselbe APK. Nachweis: KICAD-Prüflauf unverändert grün, Labor-Simulator 7/7 im Testraum, Wechsel KICAD ↔ Labor im Simulator. | E0 |
| **E2 Bedienmuster-Schicht** | Schnittstelle Bedienmuster und Bedienaufgabe (Logik in `Hlks.Xr`), Messung je Aufgabe im Kern, die heutigen Muster als erste Module (Verhalten unverändert), Wahl je Art über Standard bzw. live; Kanal-Nachrichten `bedienaufgabe`/`bedienergebnis`. | E1 |
| **E3 Labor allgemein** | Register «Muster» und «Szenarien» auf der Labor-Seite, Szenario «Allgemein» mit den sechs Aufgaben, Szenario «KICAD-Bedienung», Auswertung je Muster; Bedienstandard festlegen (bisher L2) mit Musterwahl; KICAD liest den Standard, Labor-Teile aus KICAD entfernen (bisher L2). | E2 |
| **E4 Neue Muster** | Zwei bis drei neue Muster nach Christians Wahl (Vorschläge oben), je als Modul; danach laufend auf Zuruf. | E2 |
| **E5 KICAD auf Bedienaufgaben** | KICADs Apps (Etagen, Messen, Marker, Ebenen) nutzen Bedienaufgaben, wo es passt; ohne Eile, je App einzeln. | E3 |
| **E6 Werkzeuge ohne eigenes Modul** | Protokoll so weit, dass ein neues Werkzeug (z. B. HKAP) nur mit Szenen und Bedienaufgaben auskommt; erstes Beispiel nach Absprache. | E5 |
| **E7 Gestenlabor Brille** | bisher L3 (`labor-app.md`), Gesten je Bedeutung für alle Werkzeuge. | E3 |
| **E8 PC-Gesten-Labor** | bisher L4 (`labor-app.md`, Übergabe Modul G); unabhängig vom Empfänger. | Modul G Stufe 1 (in `main`), E3 |

Zwischen den Stufen ein Brillentest mit Christian (Prüfbilder an jedem sichtbaren Zwischenstand).

## Zuteilung (nach Tabelle in `../CLAUDE.md`)

| Teil | Wer |
| --- | --- |
| Spezifikation, Protokoll (Bedienaufgaben, Kopplung mit Werkzeug), Abstimmung, Abnahme | Hauptlauf |
| E1 Empfänger-Hülle, mehrere Kopplungen, Werkzeug-Module; E2 Muster-Schnittstelle und erste Muster; E4 neue Muster | `hlks-kern` |
| E1 Knöpfe in KICAD und Labor (eine APK), Bauskripte; E3 Labor-Seite (Muster, Szenarien, Auswertung), Katalog der Szenarien | `hlks-modul` |
| Testläufe und Testbericht je Stufe | `hlks-test` |
| Architektur-Prüfung des Protokolls (Bedienaufgaben, Kopplung je Werkzeug, Vorrecht des Labors) vor E3 | `hlks-pruefer` |

## Abstimmung

- **Sitzung «Modul XR – Quest App»** (KICAD-Brillen-App): E1 und E2 bauen die App um. Vor E1 abklären, dass dort nichts an `src/Hlks.Quest` läuft;
  während E1/E2 ändert nur diese Sitzung die App.
- **Hauptsitzung:** L1 kann wie gemeldet zusammengeführt werden; es bleibt Grundlage. Neue Knöpfe in KICAD («H+K XR installieren») gehören zu E1.

### Schnittstellen im Bau E1 (Hauptlauf, 26.10.2026)

Damit Brille (`hlks-kern`) und PC-Seite (`hlks-modul`) gleichzeitig bauen, gilt:

- **App:** Kennung `ch.hkplanung.xr`, Name «H+K XR». Eine APK für alle Werkzeuge, abgelegt von `werkzeuge/quest_app_bereitstellen.ps1` (ohne
  `-Labor`) in `%USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr\` (`ch.hkplanung.xr-Signed.apk` + `version.json` wie bisher). KICAD und Labor installieren
  dieselbe APK von dort. Bauausgabe `<bau>\Hlks.Quest.Android\bin\Release\net10.0-android\publish\ch.hkplanung.xr-Signed.apk`.
- **Kopplungspaket** Fassung 2: wie Fassung 1 plus `werkzeug` (`kicad` | `xrlabor`, Muster `[a-z][a-z0-9_-]{0,31}`) und `werkzeugName` («KICAD» |
  «XR-Labor»). Fassung 1 ohne `werkzeug` gilt als `kicad`.
- **Eingang** der App: `/sdcard/Android/data/ch.hkplanung.xr/files/kopplung_<werkzeug>.json` (ein Paket je Werkzeug); die App übernimmt jedes in
  ihren privaten Speicher `kopplungen/<werkzeug>.json` und löscht es im Eingang. Ein altes `kopplung.json` gilt als `kicad`.
- **Kanal:** Die App hält zu jedem gekoppelten, erreichbaren Werkzeug einen Kanal offen. `hallo` bekommt `aktiv` (bool, fehlt = true); neue
  Nachricht der App `aktiv { aktiv }` beim Wechsel. Ein inaktiver Teilnehmer bekommt Aufträge, aber die App sendet ihm keine Posen; der PC
  führt ihn in der Teilnehmerliste mit `aktiv: false` (KICAD zeigt seine Figur dann nicht). Ein Auftrag an eine inaktive App lässt sie zu diesem
  Werkzeug wechseln.
- **Alte Apps:** Der Quest-Dialog bietet «Alte Apps entfernen» (`adb uninstall ch.hkplanung.kicad.quest` und `ch.hkplanung.xrlabor`, nur auf Knopfdruck
  und nur, wenn sie installiert sind).

### Prüfliste für E1 (Hinweise der Sitzung «Modul XR – Quest App», 26.10.2026)

1. Paketname `ch.hkplanung.kicad.quest` mitziehen in `werkzeuge/quest_app_bereitstellen.ps1`, `quest_brillentest.ps1`, `quest_pruefen.ps1`, im
   Quest-Dialog von KICAD (Installation und Start per adb, Prüfung gegen `version.json`) und im Kopplungspfad
   `/sdcard/Android/data/<paket>/files/kopplung.json`; die alte App auf der Brille deinstallieren lassen.
2. Netzzugang und Pinning hängen am PC (`zugang.json`, `XrPforte`); mehrere Werkzeuge = mehrere Kopplungseinträge in der App. Das `hallo` trägt
   Fassung und Commit der App.
3. Grenzen für Stellschrauben und Varianten (Katalog 96, Kanal 128) gelten je Kanal – bei mehreren Werkzeugen je Werkzeug zählen.
4. Die Prüfläufe V1a–V1i (`QuestApp.Pruefen*.cs`) setzen Varianten selbst; beim Umbau zu Modulen mitziehen.
5. Register «XR» im Menüband, Gruppe «Brille», «App installieren …» (Kürzel XI) zeigt auf den Quest-Dialog: neuen Namen «H+K XR» nachführen.

## Offene Punkte (im Bau zu klären, ohne Rückfrage)

- Ordnername `src/Hlks.Quest` behalten oder umbenennen (E1, nach Konfliktlage).
- Erhalt der bestehenden Kopplung beim Wechsel der App-Kennung: neu koppeln per Kabel (einmalig) ist die Vorgabe.
- Symbol «H+K XR»: Entwurf als Bild an Christian (E1).

### Entscheide Christian 26.10.2026 nach E1 (Fragefenster)

- Symbol «H+K XR»: **kräftig** (dunkles Blaugrau `#3E536F`, heller Brillenumriss mit «XR», darunter «H+K»).
- Knopf XI im Register XR: **«H+K XR installieren …»**.
- Übergang: Die KICAD-App führt das bisherige Interface-Labor (Prüfaufgaben aus dem KICAD-Dialog «XR-Labor») **bis E3** noch selbst aus
  (`Werkzeug.Kicad.MitLabor = true`); mit dem Bedienstandard in E3 wird das `false`.

### Schnittstellen im Bau E2 (Hauptlauf, 26.10.2026; Christian: «E2 (Bedienmuster-Schicht) jetzt planen und starten, den Brillentest machen wir danach.»)

Ziel E2: Werkzeuge können Bedienaufgaben schicken, der Empfänger stellt sie mit einem Bedienmuster dar und meldet das Ergebnis mit Messung.
KICADs heutige Apps (Etagen, Messen, Marker …) bleiben **unverändert** (Umzug erst E5). Brille/Logik `hlks-kern`, PC/Protokoll `hlks-modul`.

**Kanal-Nachrichten** (Hülle wie alle: `typ`, `fassung`; Prüfung in `XrNachrichten.cs` am PC und `KanalVerbindung.cs` in der App):
```
PC → App   bedienaufgabe            { id, art, titel, text?, eintraege?, bereich?, vorgabe?, ziel?, abbrechbar = true, muster? }
             id         [a-z0-9][a-z0-9_-]{0,39}, eindeutig je Werkzeug; eine neue Aufgabe mit gleicher id ersetzt die alte
             art        auswahl | menue | liste | wert | bestaetigen | platzieren | greifen
             titel      Text bis 120 Zeichen; text bis 400
             eintraege  [{ id, text, symbol? }] bis 200 (auswahl, menue, liste); symbol = Name aus symbole.js
             bereich    { min, max, schritt, einheit? } (wert)
             vorgabe    Eintrag-id bzw. Zahl
             ziel       { mm: [x, y, z] } (platzieren, greifen: Ort im Bezug des Werkzeugs, KICAD-mm wie Posen)
             muster     Kennung eines Bedienmusters – nur vom Labor (Werkzeug mit MitLabor), sonst übergangen
PC → App   bedienaufgabe.schliessen { id }
App → PC   bedienergebnis           { id, status: ok | abgebrochen | ersetzt | geschlossen, wert?, zeitMs, fehlgriffe, muster }
             wert       Eintrag-id (auswahl, menue, liste) | Zahl (wert) | true/false (bestaetigen) | { mm: [x,y,z] } (platzieren)
                        | { mm: [x,y,z], drehungGrad, massstab } (greifen)
```
- Höchstens eine offene Bedienaufgabe je Werkzeug; nur das aktive Werkzeug zeigt sie. Ein Wechsel bricht sie nicht ab, sie ruht.
- `zeitMs` vom Erscheinen bis zum Ergebnis; `fehlgriffe` zählt der Kern einheitlich (Berührung bzw. Auslösen ausserhalb eines Ziels, Wahl
  zurückgenommen); `muster` = Kennung des Musters, das die Aufgabe dargestellt hat.

**Am PC** (`src/Hlks.Web`, für alle Werkzeuge, nur localhost): `POST /api/xr/bedienaufgabe { teilnehmer?, aufgabe }` (ohne teilnehmer an alle
aktiven Brillen des Werkzeugs), `POST /api/xr/bedienaufgabe/schliessen { id }`, `GET /api/xr/bedienergebnisse?seit=<zeit>` (die letzten
100 Ergebnisse im Speicher des Kanals, mit Zeit und Teilnehmer). Für die KI verboten wie die übrigen XR-Endpunkte.

**Katalog der Muster** `kataloge/xr_bedienmuster.json` (Art `hlks-xr-bedienmuster`, Fassung 1; schreibt `hlks-kern`):
```
{ art, fassung, beschreibung,
  arten:  [{ id, name, beschreibung }],                       die sieben Arten oben
  muster: [{ kennung, name, beschreibung, arten: [...], herkunft, status: "app" }],
  vorgabe: { "<art>": "<kennung>" } }
```
Der Labor-Katalog (`GET /api/xr/labor/katalog`, `hlks-xr-labor-katalog`) liefert ihn zusätzlich als Feld `bedienmuster` (`hlks-modul`). Wahl je Art
als Stellschraube `muster.<art>` = Kennung (Muster wie `labor.variante.<name>`; Schicht Vorgabe → Profil/Standard → live). Stellschrauben eines
Musters in `kataloge/xr_stellschrauben.json`, Gruppe je Muster, Namensraum `muster.<kennung>.…`.

**Erste Muster (aus der heutigen App, Verhalten der Bausteine unverändert):**

| Kennung | Arten | Grundlage |
| --- | --- | --- |
| `glas-tafel` | auswahl, menue, liste, wert, bestaetigen | `Tafel`, `TafelBlaettern`, `Glas`; Einträge als Knöpfe auf der mittigen Tafel, Blättern per Wisch, Wert mit −/+ und Wisch, Bestätigen mit zwei Knöpfen, Wegwischen = abbrechen |
| `symbolfeld` | auswahl, menue | `AppSymbole`, `HebeGeste`, `AppBeruehrung`; Einträge als Glas-Symbole im Feld, Wahl durch Berühren |
| `pistole-zeigen` | platzieren | `Pistole`, Fang wie beim Springen; Ziel zeigen und abdrücken |
| `zwei-punkt-griff` | greifen | Griffe «Verschieben» und «Drehen und Vergrössern» vom Sitzungstisch, an einem neutralen Würfel |

**Prüfen:** Prüflauf «Bedienaufgaben» im Simulator: je Art eine Aufgabe, gelöst mit jedem passenden Muster über simulierte Hände; offline über
`--bedienaufgabe <datei>`, nach dem Zusammenführen auch über den Kanal (Hauptlauf). KICAD-Prüflauf und Labor-Prüflauf bleiben grün.
