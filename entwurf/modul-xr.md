# Modul XR – KICAD in der VR-Brille (Meta Quest)

Stand 23.10.2026 · Sitzung Quest App · Christian 21.10.2026: «wir fassen das zusammen als Modul XR». Hier sind die Ideen N23–N26 aus
`vorrat_freitag.md` zusammengefasst.

## Übertheme «Quest App» (Entscheid Christian 22.10.2026)
Christian: «wir packen das vr thema zusammen, und behandeln das gesamtheitlich mit dem übertheme Quest App => App ist der weg.»

- **Ziel:** eine eigene KICAD-App auf der Meta Quest, mit Icon in der App-Bibliothek.
  - Sie verbindet sich mit dem KICAD am PC und zeigt Begehen und Sitzungsraum.
  - Sie bedient Tablet, Greifen, Messen, Marker und Sprache.
  - Ohne Quest-Browser, ohne Fernsteuerung des Browsers und ohne Pflicht-Tipp beim Start.
- **Der Browser-Weg (Stand 22.10.2026) ist der Prototyp.**
  - Er bleibt lauffähig, bis die App ihn ersetzt, und ist die Vorlage für Bedienung und Abläufe.
  - Neue Funktionen gibt es dort nicht mehr, nur noch Fehlerbehebungen.
- **Warum die App** (Erfahrungen aus dem Brillentest vom 21.10.2026):
  - Der Browser startet VR nur nach einem Tipp in der Brille; der Autostart braucht dafür das Fern-Debugging des Browsers.
  - adb über WLAN reisst im Ruhezustand ab. Nach einem Neustart der Quest ist es ganz weg, dann braucht es das Kabel.
  - Christians PC kann kein Link (Intel-Grafik), alles rechnet die Quest. Der Browser ist dafür nicht die schnellste Laufzeit; die
    IFC-Unterlage hat 840 000 Dreiecke.
  - PC-Oberfläche und VR im selben Browser der Brille verwirren. Die App zeigt nur VR.
- **Was bleibt:** Der KICAD-Server am PC ist die einzige Quelle für Modell, Befehle, Pakete, Rückgängig und Abgleich («eine Tür»). Die
  App ist ein weiterer Client wie die Oberfläche am PC.

### Grundsatzentscheide (Christian 23.10.2026)
Gefällt im Fragefenster am 23.10.2026, nach der Prüfung mit Quellen weiter unten.

| Frage | Entscheid | Folge |
| --- | --- | --- |
| Technik | **StereoKit** (C#/.NET, MIT, OpenXR) | Unity entfällt als Gegenprobe (Pflicht zur Lizenz Unity Industry). Gegenprobe **Godot 4** nur, wenn der Prototyp die Prüfkriterien K1 oder K2 verfehlt. Die Rechenregeln liegen in einer eigenen C#-Bibliothek ohne StereoKit (`Hlks.Xr`); ein Wechsel der Technik trifft die Logik nicht. |
| Verbindung | **Sofort WLAN mit Kopplung und TLS**, schon im Prototyp | adb nur noch für Installation und Notfall. KICAD bekommt einen zweiten Zugang im Büronetz nur für die App: eigener Port, HTTPS, nur gekoppelte Brillen, nur `/api/xr/…`. Alles andere bleibt auf localhost. Sicherheitsprüfung durch hlks-pruefer vor dem ersten Brillentest; Abgleich mit Phase 0 (Sicherheit). |
| Verteilung | **KICAD installiert die App** per adb | Knopf «App installieren/aktualisieren» im Dialog «Quest verbinden». Der Entwicklermodus bleibt nötig. Kein Store; die Geräteverwaltung von Meta nimmt seit 20.02.2026 keine Neukunden mehr an. |
| Mehrbenutzer | **Protokoll mehrbenutzerfähig, PC sieht die Brille** | Die Verbindung ist ein Live-Kanal mit Teilnehmern. Version 1: eine Brille; die 3D-Ansicht am PC zeigt Standort und Blickrichtung der Brille. Zweite Brille später (XR-G). |
| Bedienung | **Hände zuerst** (Leitlinie unten), **Leitbild schwebende Apps mit Wischgesten** | Jede Hauptfunktion direkt mit den Händen. Christian, 23.10.2026: «meine vision ist es bsp auch eine art wischgesten zu verwenden. ev. nicht ein tablet sondern schwebende apps die ich berühren kann. so dass die prezision weniger ein thema wird.» Statt eines Tablets gibt es grosse, schwebende Apps, die man direkt berührt und mit Wischgesten bedient. Das Tablet läuft im Labor als Vergleichsvariante. Ton und Licht melden jeden Fang, Griff und jede Berührung. |
| Umfang Version 1 | **Wie vorgeschlagen, plus Problem-Marker** | Verbinden und Kopplung, Auftrag vom PC und Autostart, Begehen (XR-A), Sitzungsraum (XR-H), Messen (XR-C), Problem-Marker (XR-E), PC sieht Brille. Danach XR-B, XR-D, XR-F, XR-G. |
| Sitzungsraum | **Beides, Passthrough prüfen** | Der virtuelle Raum bleibt. Im Prototyp wird geprüft, ob das Modell per Passthrough auf dem echten Tisch stehen kann (Umschalter). Christian, 23.10.2026: «dein vorschlag mit dem pass habe ich mir auch gedacht. Bsp. raum in VR zeigen. Es der raum erscheint auf dem tisch.» → «Raum zeigen (VR)» stellt den Raumausschnitt auf den echten Tisch. |
| Prüfung | **Kurz gemischt** | Die App misst jede Prüfaufgabe (Zeit, Fehlgriffe, Abbrüche). Danach am PC SUS, NASA-TLX und drei Fragen zu Komfort und Übelkeit (etwa 3 Minuten), dazu Christians Bemerkungen. Daraus entsteht eine Trendlinie je Version. Deutsche Fassungen der Fragebogen nur mit Quelle. |

### Leitlinie Bedienung (Christian 23.10.2026)
Christian: «nehme dies als grundsätzliche leitlinie». Quelle: Hughes, C. L.; Karwowski, W.: *Evaluating interaction design and user
experience in augmented reality: a systematic review.* Frontiers in Virtual Reality 6 (2025), doi:10.3389/frvir.2025.1710161
(86 Studien 2013–2024, abgerufen 23.10.2026).

Was daraus für die Quest App gilt:
- **Natürlich vor WIMP** (Fenster, Symbole, Menüs, Zeiger): Bedient wird durch Zeigen, Greifen, Gehen und direktes Verschieben. Fenster
  und Menüs (Tablet) gibt es nur als Hilfe, wo Genauigkeit, Erlernbarkeit oder ein Rückfallweg nötig sind (Abschnitte 7 und 8).
- **Mischsysteme wirken über ihren natürlichen Teil:** Die Vorteile der häufigsten Mischsysteme kommen aus Geste, Sprache und Blick,
  nicht aus dem Menü (Abschnitt 3.4).
- **Mehrere Sinne:** Rückmeldung mit Ton und Licht, mit Controllern auch mit Vibration (Abschnitt 7).
- **Auf den Zusammenhang bezogen:** Die Bedienung passt sich an Standort, Aufgabe und Umgebung an, z. B. die Eigenschaften des Elements,
  auf das man zeigt, statt einer festen Liste (Abschnitt 7).
- **Gemischt prüfen:** Fragebogen (SUS, NASA-TLX) zusammen mit gemessenen Aufgaben und Beobachtung. Komfort, Präsenz und
  Zusammenarbeit gehören auch dazu (Abschnitte 3.6, 6, 8).
- **Grenze:** Die Übersicht behandelt AR (vor allem HoloLens 2 und Handys). Für VR auf der Quest gilt sie sinngemäss; mit Passthrough ist
  die Quest 3 selbst ein AR-Gerät.

Folgen für das Pflichtenheft (XR-A bis XR-H):
- Teleport mit Zeigen und Pinch.
- Etage direkt am Modell wählen, z. B. mit einem Wisch nach oben oder unten.
- Messen als Massband, das man zwischen zwei Punkten aufzieht.
- Eigenschaften dort, wo man hinzeigt.
- **Schwebende Apps statt Tablet** (Christians Leitbild):
  - Grosse Kacheln schweben im Griffbereich und werden mit dem Finger berührt.
  - Wischen blättert, wechselt und schliesst.
  - Grosse Ziele und ein Fang zum nächsten Ziel machen Genauigkeit unnötig.
  - Welche Apps es gibt (z. B. Etagen, Messen, Marker, Eigenschaften, Rückgängig), klärt das Labor.
  - Das heutige Tablet (XR-C) bleibt Vergleichsvariante, bis das Labor entschieden hat.

### Prüfung der Technik (23.10.2026, mit Quellen)
- **Unity:**
  - Firmen aus Architektur, Planung und Bau gelten als «Industry Customer». Ab 1 Mio. USD «Total Finances» ist Unity Industry Pflicht,
    der Preis gibt es nur auf Anfrage.
  - Unity Personal ist für solche Firmen ausgeschlossen.
  - Quellen: Unity Support, «What is an Industry Customer»
    (https://support.unity.com/hc/en-us/articles/32263121729172-What-is-an-Industry-Customer); Unity, «Changes to Unity subscription plans
    and pricing» (https://unity.com/products/pricing-updates).
- **StereoKit:**
  - MIT, C#, OpenXR; unterstützt die Meta Quest mit Hand-Tracking.
  - Letzte Version: v0.3.11 vom 28.06.2025 (Fehlerbehebung), davor v0.3.10 vom 22.02.2025. Die Version v0.4 mit grösseren Änderungen ist
    seit 2024 in Arbeit.
  - Es gibt einen Hauptentwickler, und stabile Versionen erscheinen etwa einmal pro Jahr.
  - **Korrektur 23.10.2026:** Auf NuGet erscheinen laufend Vorversionen von 0.4.0 (Stand 23.10.2026 bis `0.4.0-preview.3866`, Quelle
    https://api.nuget.org/v3-flatcontainer/stereokit/index.json). Die Entwicklung ist also aktiv, nur stabile Versionen sind selten. Baut
    0.3.11 nicht mit .NET 10, ist eine Vorversion von 0.4 der Ausweg.
  - Quellen: https://github.com/StereoKit/StereoKit/releases, https://stereokit.net.
  - Folge: Version auf 0.3.11 festlegen und die Logik getrennt halten. Ob StereoKit 0.3.11 mit .NET 10 für Android baut, prüft P0.
- **Godot 4:**
  - MIT; Quest mit Hand-Tracking und Passthrough über das OpenXR-Vendors-Plugin (Godot-Doku «Deploying to Android»,
    https://docs.godotengine.org/en/4.1/tutorials/xr/deploying_to_android.html).
  - Stand von C# auf Android: nicht geprüft.
- **Leistung Quest 3:**
  - Meta empfiehlt höchstens 1,5 Mio. Dreiecke und 200 Zeichenaufrufe je Bild, bei mindestens 72 Hz (empfohlen 90 Hz).
  - Quelle: Meta, «Device optimization comparison» (https://developers.meta.com/horizon/resources/device-optimization-comparison/).
  - Test_praxis (843 000 Dreiecke) passt hinein, wenn die Geometrie je Geschoss und Material zusammengefasst wird. Mit einem Netz je
    Element sprengen die Zeichenaufrufe das Budget.
- **Verteilung:**
  - Meta verkauft seit 20.02.2026 keine «Horizon Managed Services» mehr und nimmt keine Neukunden an. Bestehende Kunden werden bis
    04.01.2030 betreut.
  - Quellen: heise (https://www.heise.de/en/news/Meta-Quest-Official-VR-office-app-and-B2B-program-discontinued-11144019.html);
    SiliconANGLE, 16.01.2026
    (https://siliconangle.com/2026/01/16/meta-stop-selling-quest-headsets-businesses-discontinue-multiple-vr-features/).
- Alle Quellen abgerufen am 23.10.2026.

### Prototyp: Interface-Labor (freigegeben von Christian am 23.10.2026)
Christian, 23.10.2026: «Plan Interface-Labor mit den Ergänzungen freigegeben. Start nach dem Kontingent-Reset mit P0; frag mich vor den
Downloads nach Quelle und Grösse.»

Christian, 23.10.2026: «ich möchte vor eine art testumgebung für das optimieren des interfaces.» Der Prototyp wird deshalb als
**Interface-Labor** gebaut. Es ist die erste Fassung der App, in der die Bedienung erprobt und verbessert wird, bevor die Funktionen der
Version 1 darauf aufbauen. Inhalt wie im Auftrag: Modell laden, Begehen, Hände; dazu das Labor.

**Was das Labor kann:**
- **Testraum mit Prüfaufgaben:** Die Aufgaben spielen an Test_praxis (ein Geschoss, mit IFC-Unterlage) und an einem neutralen Raum.
  Den Katalog liefert KICAD als JSON. Beispiele:
  - an eine Markierung teleportieren
  - um 90° drehen
  - das Modell greifen, drehen und auf einen Zielmassstab zoomen
  - eine Etage wählen
  - von Wand zu Wand messen
  - auf dem Tablet einen Wert ablesen
- **Schwebende Apps und Wischgesten** (Christians Leitbild) als Hauptvariante:
  - Grosse Kacheln im Griffbereich, die man direkt berührt.
  - Wischen nach links/rechts blättert, nach oben/unten wechselt die Etage, Wegwischen schliesst.
  - Fang zum nächsten Ziel.
  - Vergleich mit dem Tablet an der Hand aus dem Browser-Prototyp.
- **Varianten je Bedienung, umschaltbar (A/B):** z. B. schwebende Apps oder Tablet; Teleport mit Zeigen und Pinch oder mit Stick; Etage
  per Wisch am Modell oder per App; Messband aufziehen oder zwei Punkte setzen.
- **Stellschrauben live vom PC:** Die Seite «XR-Labor» in KICAD zeigt die Grössen, die die Bedienung bestimmen, z. B.:
  - Pinch-Schwelle und Fangradius
  - Grösse, Abstand und Anordnung der schwebenden Apps, Schriftgrösse
  - Wischgesten: Mindestweg, Mindesttempo und Richtungstoleranz
  - Winkel beim Drehen in Schritten, Ton an/aus
  - Änderungen wirken sofort in der Brille. Gute Werte werden als Profil gespeichert und gelten dann für die App.
- **Messung:** Die App protokolliert jede Aufgabe mit Zeit, Fehlgriffen, Abbrüchen, Variante und Stellschrauben. Die Seite «XR-Labor»
  vergleicht die Varianten und zeigt den Verlauf je Fassung. Nach dem Test folgt der Fragebogen am PC (Entscheid «Prüfung»).
- **Simulator am PC:** Dieselbe App läuft am PC im Simulator von StereoKit mit Maus-Händen. Damit lässt sie sich ohne Brille prüfen, und
  automatische Tests werden möglich. Laut StereoKit vorhanden; in P0 bestätigen.
- **Passthrough-Probe:** Umschalter «echter Tisch» für den Sitzungsraum (Entscheid «Sitzungsraum»). Prüffall nach Christian: Am PC
  «Raum zeigen (VR)» wählen, dann erscheint der Raumausschnitt auf dem echten Tisch vor der Brille.
  - Die Tischhöhe wird durch einmaliges Antippen des Tisches mit der Hand festgelegt.
  - Zu prüfen: Erkennt die Raumerkennung der Quest (Raumeinrichtung mit Tisch) den Tisch selbst?
  - **Marker am Tisch** (Christian, 23.10.2026: «beim tisch sind auch marker denkbar»): Ein gedrucktes Zeichen auf dem echten Tisch legt
    Lage und Drehung des Modells fest, statt es anzutippen. Die Leitlinie nennt physische Marker als noch wenig genutzten Weg
    (Abschnitt 6).
  - Zu prüfen: Dürfen Apps auf der Quest 3 die Kamerabilder dafür nutzen? Wie genau ist das?

**Darstellung im Stil der Apple Vision Pro** (Christian, 24.10.2026: «richte dich bei der Darstellung der apps und funktionen nach der vision.» – «sorry meine apple vision pro»):
- Quelle: Apple Human Interface Guidelines, Abschnitte visionOS der Seiten materials, windows, buttons, tab-bars, ornaments, eyes, spatial-layout, immersive-experiences (developer.apple.com/design/human-interface-guidelines, abgerufen 24.10.2026).
- Apps als aufrechte Glasflächen: durchscheinend, neutral getönt, stark gerundet, feiner Glanz und weicher Schatten. Schrift weiss in drei Stufen, nicht dünn, ohne Tiefe.
- Knöpfe als Kreis (Symbol), Kapsel (Text bzw. Symbol und Text) oder abgerundetes Rechteck im senkrechten Stapel. Mitten ≥ 60 pt auseinander bzw. 16 pt Rand. Weiss gefüllt nur für «eingeschaltet». Ton beim Antippen.
- Hover: Apple nutzt den Blick; die Quest 3 hat keine Augenverfolgung, darum Hervorheben bei Annäherung von Finger bzw. Strahl.
- Fensterteile: Kapsel-Griff unter der App, runder Schliessen-Knopf daneben, Werkzeugleisten als Ornament an der Unterkante, Register senkrecht am linken Rand.
- Tiefe sparsam, ruhige Bewegungen, im Raum verankert, Wichtiges in der Mitte des Blickfelds. Das Modell am Tisch wie ein «Volume» mit leuchtendem Rand der Bodenplatte.
- Massstab: 1 pt ist in visionOS ein Winkel (Fenster 1280 × 720 pt in rund 2 m etwa 3 m breit); abgeleitet ≈ 1,2 mm je Meter Abstand (eigene Rechnung, Annahme).
- Bleibt: Hände zuerst, Berühren und Wischgesten (Grundsatzentscheid «Bedienung»).
- Sitzungsraum ebenfalls im visionOS-Stil (Christian, 24.10.2026: «ruhige, helle Umgebung»); die Tron-Optik (XR-H) entfällt. Nach HIG «Immersive experiences» (Creating an environment): wenig Kontrast und Bewegung, weit statt eng, immer eine Bodenfläche, weiches Licht aus Formen statt 360°-Bild, Passthrough nur dezent tönen. Die alte Umgebung höchstens als Labor-Variante zum Vergleich.
- Ersetzt den Hologramm-Stil des Tablets (XR-C «Aussehen») und die grauen Textkacheln aus P5a. Keine Apple-Logos, SF-Schriften oder SF Symbols (Lizenz nur für Apple-Plattformen). Umsetzung mit V1a.

**App-Symbole per Hebegeste** (Christian, 24.10.2026, nach den Bildern V1a/01: «wird mit beiden handflächen nach oben eine eine deutliches nach obenstreichen gezeigt, soll dies frei schwebende Appsymbole von unten her einblenden. so wie ich sie von unten hinein heben würde. die symbole müssen in griff weite sein. angewählt werden sie durch berührung. finger zeigen den pointer mit tooltip» – «symbole nicht in einer leiste sondern verteilt.»):
- Geste: beide Handflächen nach oben, deutlich nach oben streichen; ersetzt «linke Handfläche zum Gesicht». Löst weder Etage (Pinch+Wisch) noch Teleport aus. Grenzwerte als Stellschrauben (Startwerte Annahme).
- Runde Glas-Symbole ohne Fenster, eines je App, steigen von unten an ihren Platz; locker im Raum verteilt (verschiedene Höhen, leicht gestaffelt, keine Leiste), in Griffweite, im Raum verankert, gleiche Lage bei jedem Öffnen. Lage (Christian, 24.10.2026: «frontal des nutzers in einem abstand von ca. 60cm und über eine höhe von ca. 0.6m verteilt … vorzugsweise maximal 3 reihig»): Feld frontal in Körperrichtung, ca. 0,60 m entfernt, ca. 0,60 m hoch, höchstens 3 locker versetzte Reihen; auch am Sitzungstisch. Mitte des Felds, Abstand, Höhe und Reihen als Stellschrauben.
- Anwahl durch Berührung mit der Fingerspitze (Leuchten, Ton), dann öffnet die Glas-App; der Zeigefinger zeigt beim Annähern einen Zeiger, das Symbol hebt sich, der Name erscheint als Tooltip.
- Die senkrechte Registerleiste entfällt (Christian, 24.10.2026: «Ja, die Registerleiste fällt weg; gewechselt wird über die Hebegeste und die Symbole.»). Ausblenden: einheitliche Wegwisch-Geste (Christian, 24.10.2026: «wir vereinheitlichen die wegwisch geste. auch da nutzen wir die wischbewegung von rechts nach links»): rechte Hand offen, Handfläche nach links, Wisch von rechts nach links; die Symbole gleiten nach links hinaus. Gilt auch für den PC-Bildschirm (XR-I). Sind mehrere Dinge sichtbar, trifft es das, was die Hand überstreicht, sonst das zuletzt Eingeblendete. Ersetzt das beidhändige Wegschieben aus der Mitte. Auch offene Glas-Apps und die Aufgabenkarte (= Aufgabe abbrechen) werden so geschlossen (Christian, 24.10.2026: «Ja, auch offene Glas-Apps werden mit dem Wegwischen geschlossen; Pinch + Wisch nach vorn entfällt.»); Reihenfolge ohne Überstreichen: zuletzt Geöffnetes zuerst. Die Symbole bleiben stehen (Christian, 24.10.2026: «werden die aps hineingehoben bleiben diese stehen.»): kein Ausblenden nach Zeit, auch nach der Wahl einer App nicht; bei offener App weicht das Symbolfeld seitlich neben das Fenster aus (Christian, 24.10.2026: «Das Symbolfeld weicht bei offener App seitlich neben das Fenster aus.»); «dahinter» bleibt als Labor-Variante.

**Brillentest V1 (24.10.2026, Stand d5a8df6, Test_praxis, Kabel)** – Christian: «das ist schon sehr gut. die apps solten beim hochwischen etwas weiter oben sein. die tafel mit informationen oder auswahlmöglichkeiten sollte immer zentrisch sein. das modell soll nur mit einem griff bewegt und gezoomt werden. nach tippen einer App sollte erneutes auswählen gesperrt sein, dass beim neuanordnen der apps nicht gleich ein nächstes gewählt wird.»
- Protokoll der Brille: Hebegeste 19×, Wegwischen 26×, 33 Etagenwechsel, ein Marker gesetzt und am PC angekommen, Sitzungstisch mit echtem Tisch (Passthrough).
- Befund Leistung: am Sitzungstisch mit «Alle Etagen» 600–740 Tsd. Dreiecke und 52–63 Aufrufe → 46–56 Hz (mit eigenem Geschoss 72 Hz). Fang beim Messen bzw. Zeigen im Mittel 12 ms, einmal 1 s → Ruckler.
- Nachtrag Christian: «die appanordnung muss bei erkanntem sitzen eher etwas weiter oben sein.» → im Sitzen zusätzlich höher (eigene Stellschraube).
- Nachtrag Christian: «wisch mit der linken hand von links nach rechts soll auch den monitor reinziehen. und wisch mit der linken hand von rechts nach links soll in wieder raus schieben. apps sind nun ca. 20cm zu hoch.» → Hineinziehen und Wegwischen mit beiden Händen; am Sitzungstisch Feld rund 0,20 m tiefer, beim Begehen höher (bestätigt: «Ja: beim Begehen etwas höher, am Sitzungstisch rund 20 cm tiefer.»).
- Nachtrag Christian: «eine swap bewegung mit der rechten oder linken hand soll in einem aktiven fenster blättern.» → Wisch auf bzw. dicht vor der aktiven Tafel blättert (links/rechts Seite, hoch/runter rollen), mit beiden Händen; ein Wisch im freien Raum behält seine Bedeutung (Hineinziehen, Wegwischen). Nicht auf dem PC-Bildschirm (Bedienung nur am PC).
- Brillentest V1d (24.10.2026, 20:55): «der etagenwechsel mit geste ist unpraktisch.» → gewählt «Nur App «Etagen»»: Vorgabe `labor.variante.etage` = app; Pinch + Wisch hoch/runter wechselt kein Geschoss mehr.
- Brillentest V1d: «alle ein und aus wisch gesten sollen nur einen anstoss brauchen. momentan brechen einblenden von apps und monitor, sowie der apps oft ab. weil die instinktive bewegung zu kurz ist.» und «die etagen auswahl geht über mehrere seiten da wäre besser die liste zu vergrössern.» → V1e: Ein- und Ausblend-Gesten lösen auf einen kurzen Anstoss aus und laufen selbst zu Ende; Etagenliste auf einer Seite.
- Brillentest V1d: «modell auf tisch soll einen verschieben punkt erhalten, und einen punkt für drehen und vergrössern diese bewegung dreht immer um das zentrum. beide punkte sollen farblich unterschiedlich sein, und ein kleines symbol beim greiffen erhalten.» → V1e: Punkt «Verschieben» und Punkt «Drehen und Vergrössern» (Drehung um das Modellzentrum, Massstab aus dem Abstand zum Zentrum), unterschiedliche Farben, Symbole aus symbole.js beim Greifen; der eine Griff aus V1d bleibt Labor-Variante.
- Brillentest V1d: «apps sollen etwas mehr glanz erhalten.» → V1e: stärkerer Lichtsaum und Glanzverlauf auf Glas-Apps, Symbolscheiben und Karte (Stellschraube anzeige.glanz), Lesbarkeit bleibt.
- Brillentest V1d: «apps mit mehr abstand zu den popup tafeln» → V1e: Symbolfeld neben offener Tafel mit rund 0,10 m Abstand statt 3 cm (Stellschraube). Dazu: «die apps bei grösseren abständen in einem kleineren radius um den betrachter anornen» → seitliche Symbole auf einem Bogen um den Betrachter mit kleinerem Radius (rund 0,50 m).
- Brillentest V1e (24.10.2026, 22:10): «der tisch stört. immer pass im sitzungszimmer und den tisch nie zeigen(nur das modell) pich ist zu sensibel.. hier braucht es eine andere geste. bsp. pistolengeste. zudem soll die tafel nicht mit dem blick wandern. dies soll mittig sein.» → Tafel fest (live umgeschaltet, Vorgabe fest); V1f: Sitzungsraum immer mit Passthrough ohne virtuellen Tisch, Pistolengeste statt Pinch als Auslöser.
- Brillentest V1e: «wisch geste soll auch gehen wenn die finger nicht voll gestrekt sind.» → V1f: Wischgesten auch mit lockerer Hand (nur Faust, Pistole, Pinch ausgenommen).
- Umsetzung V1d: Symbolfeld höher, App-Tafel immer mittig, ein Griff am Tisch, Sperre nach der Wahl einer App, Leistung (Tisch «Alle Etagen», Fang).

**XR-I – Desktop in der Brille** (Christian, 24.10.2026: «ein wisch mit der rechten hand mit der handfläche nach links gerichtet, zieht den verbundenen Desktop in das bild. vr bleibt starr im hintergrund.»; Inhalt: «Ganzer Bildschirm»; Bedienung: «ich habe an die ansicht von der metafunktion gedacht.»):
- Geste (Christian, 24.10.2026: «die wischbewegung natürlich von links nach rechts. heraus schieben von rechts nach links.»): rechte Hand offen, Handfläche nach links; Wisch von links nach rechts zieht den Bildschirm des gekoppelten PCs von links ins Bild (grosser, leicht gebogener virtueller Bildschirm), Wisch von rechts nach links schiebt ihn nach links hinaus. Die VR-Szene bleibt stehen (kein Drehen, kein Blättern).
- Wie die Desktop-Ansicht von Meta: ganzer Bildschirm (bei mehreren wählbar), live; bedient wird mit Maus und Tastatur des PCs, die Brille zeigt den Mauszeiger. Meta-eigene Apps lassen sich nicht in unsere laufende App einblenden; die Ansicht wird darum in der App nachgebaut.
- Übertragung nur auf Anforderung der gekoppelten Brille über den Netzzugang (TLS, Kopplung); am PC zeigt KICAD, solange die Brille mitsieht; keine Aufzeichnung.
- Gebaut mit V1c (24.10.2026): Erfassung per GDI mit Kacheln und Deflate (keine Pakete), 12 Bilder/s, bei ruhigem Bildschirm 2,5 Bilder/s (rund 12 % eines Kerns statt 60 %); Bildschirm 1,4 m breit in 1,1 m, Radius 2,0 m; Symbol «bildschirm» und Ton «Desktop» #D5ECEC (Vorschlag); wie eine App: nur ein Fenster offen, Symbolfeld weicht aus. Vorrat: DXGI Desktop Duplication, falls die Last am PC stört.

**Ganze Hand, Pastellfarben, Symbole wie im CAD** (Christian, 24.10.2026, nach den Bildern V1a/02: «die app berührung soll mit der ganzen hand erfolgen und nicht nur mit einem finger. das grau ist nicht so ansprechend. verwende unsere pastelfarben. nutze die gleiche symbolsprache wie im cad.»):
- App-Symbole wählt jeder Teil der Hand (Handfläche, Finger), nicht nur die Zeigefingerspitze; keine Wahl während Hebe- und Wegschiebegeste. Knöpfe in den Glas-Apps bleiben Fingerspitze (Annahme, eng beieinander).
- Statt Grau eine eigene App-Pastellreihe (Christian, 24.10.2026: «gewerke werden nur für gewerkeunterschiede gebraucht»; gewählt «Neue App-Reihe»): Etagen #D6E6F5, Messen #D9F0E6, Marker #F8E0D2, Tisch #E6DDF3, Begehen #F3ECD6, KICAD #DDE7EE; Akzent eine dunklere Stufe desselben Tons, Schrift dunkel. Nur im Katalog `kataloge/xr_farben.json`, nie im Code; Gewerkfarben nicht für Apps. Vorschlag, Christian beurteilt an den Bildern.
- Symbole aus derselben Quelle wie KICAD (`oberflaeche/symbole.js`, Revit-Fassung `symbole-revit.js` nach Einstellung); fehlende im gleichen Stil dort ergänzen.
- Diese Vorgaben gehen dem visionOS-Stil vor, wo sie sich widersprechen (Farbe, Symbole); Form, Glas und Abstände bleiben.

**Hinweistexte frei schwebend** (Christian, 24.10.2026: «hinweistexte frei schwebend.»): Hinweise und Statusmeldungen ohne Kasten oder Glas im Raum, dort, wo sie hingehören (Gestenhinweis bei der Hand, Meldung beim Objekt, Status unter dem Blickzentrum), lesbar durch weichen dunklen Schein hinter der Schrift, kurz, sanft ein- und ausgeblendet, nie über Apps oder Symbolen. Annahme: Die Aufgabenkarte bleibt eine kleine Glaskarte, nur ihre Hinweiszeile schwebt frei.

**Gestenlabor** (Christian, 24.10.2026: «ev. im Labor einen bereich für gestenerkennung machen. so dass ich dort eine geste für eine funktion vorzeigen kann.»):
- In der App eine Labor-App «Gesten»: Funktion wählen, Geste 3- bis 5-mal vorzeigen (Start per Knopf mit Countdown und Ton, Ende bei ruhigen Händen), Güte und Verwechslungsgefahr mit anderen Gesten sehen, live testen, übernehmen.
- In KICAD im Dialog «XR-Labor» ein Register «Gesten»: Liste mit Funktion, Beispielen, Güte, Konflikten; löschen, umbenennen, aktivieren, als JSON aus- und einlesen.
- Weiterhin eine Geste je Funktion in der ganzen App (feste Gestenbedeutungen): Eine vorgezeigte Geste ersetzt die bisherige dieser Funktion, sobald die Labor-Variante «eigene Gesten» aktiv ist.
- Erkennung ohne Training eines Netzes: Merkmale je Bild (Handflächenlage und -richtung im Körperbezug, Fingerkrümmung, Pinch, Abstand der Hände), Vergleich mit den Beispielen über Dynamic Time Warping, Ablehnung über eine Schwelle aus den Beispielen, Konfliktprüfung gegen andere und gegen die festen Gesten. Startwerte sind Annahmen und Stellschrauben.
- Bau in zwei Schritten: V1b-1 Erkennerkern in `Hlks.Xr/Gesten` mit Tests (parallel zu V1a, nur neue Dateien); V1b-2 App und KICAD-Register nach V1a.

**Aufgaben und Zuteilung** (Agenten nach `CLAUDE.md`; Bauagenten mit eigener Arbeitskopie, Zuteilung in `zuteilung.md`):

| Nr. | Aufgabe | Wer | Hängt ab von |
| --- | --- | --- | --- |
| P0 | **Bauumgebung und Durchstich:** .NET-Workload für Android, JDK, Android SDK; StereoKit 0.3.11 mit .NET 10 (sonst die App allein auf einer älteren .NET-Version); leere App auf der Quest und im Simulator am PC; Bau aus der Kommandozeile. Downloads und SDK-Lizenz nur mit Christians Zustimmung; die Firewall-Regel legt Christian selbst an (Anleitung, nur Profil «Privat») | Hauptlauf | Zustimmung Christian |
| P1 | **Logik `Hlks.Xr`** (net10.0, ohne StereoKit): Regeln aus `vr-logik.js`, `xr-mess-logik.js`, `xr-sitzung-logik.js` und den Teilen von `xr-greifen-logik.js` für Version 1 nach C#, mit den Tests aus den `*.test.mjs` | hlks-modul | – |
| P2 | **Modell als GLB je Geschoss:** aus denselben Körpern wie die 3D-Ansicht, zusammengefasst je Material (Ziel: unter 200 Zeichenaufrufe), IFC-Unterlage als eigenes GLB, Versionsschlüssel für den Zwischenspeicher der App; Test mit Test_praxis (Dreiecke, Zeichenaufrufe, Grösse). Bibliothek SharpGLTF (Lizenz prüfen) oder ein eigener schlanker GLB-Schreiber | hlks-kern | – |
| P3 | **Zugang im Büronetz:** eigener Port mit HTTPS (Zertifikat von KICAD), Kopplung (Schlüssel und Fingerabdruck des Zertifikats gelangen bei der Installation per adb in die App; Ersatzweg: Code am PC bestätigen), Suche des PCs im eigenen Netz, Live-Kanal (WebSocket) mit Teilnehmern, Kopf- und Handpositionen 20–30 je Sekunde, Aufträge, Stellschrauben | hlks-kern | – |
| P3p | **Sicherheitsprüfung von P3** vor dem ersten Brillentest | hlks-pruefer | P3 |
| P4 | **App-Gerüst:** verbinden, GLB laden und zwischenspeichern, Begehen mit Händen (Teleport mit Zeigen und Pinch, Drehen, Wände durchscheinend unter 500 mm), Rückmeldung mit Ton und Licht, Passthrough mit «Raum zeigen (VR)» auf dem echten Tisch; Start ohne PC aus dem Zwischenspeicher; Schnittstelle für Fachmodul-Ebenen mit einer Probe-Ebene | hlks-kern | P0, P1, P2, P3 |
| P5 | **Labor in der App:** schwebende Apps mit Berühren und Wischgesten, Tablet als Vergleich, feste Gestenbedeutungen mit Geisterhänden, Schnitt mit flacher Hand, Röntgen-Hand, Komfort-Stellschrauben, Prüfaufgaben, Varianten, Stellschrauben über den Kanal, Messprotokoll | hlks-kern | P4 |
| P6 | **KICAD-Seite «XR-Labor»:** Stellschrauben, Profile, Auswertung der Aufgaben, Fragebogen (SUS, NASA-TLX, Komfort; deutsche Fassung mit Quelle) | hlks-modul | P3 |
| P7 | **KICAD «Quest verbinden»:** Knopf «App installieren/aktualisieren» mit Kopplung; 3D-Ansicht am PC zeigt die Brille (Figur mit Blickrichtung) | hlks-modul | P3 |
| P8 | Tests ausführen, Testbericht | hlks-test | P1–P7 |

**Stand P0 (23.10.2026, Hauptlauf):**
- **Installiert** (Freigabe Christian 23.10.2026):
  - .NET-Workload «android» 36.1.118 (Workload-Fassung 10.0.402.1) in `C:\Program Files\dotnet`, mit Administratorrechten.
  - Microsoft OpenJDK 17.0.20.1 (SHA256 geprüft). Die Workload verlangt Java 17.0 bis 21.x.
  - Android SDK mit Build-Tools 36.0.0, Plattform Android 36, Platform-Tools und Kommandozeilenwerkzeugen. Die Lizenz
    «android-sdk-license» hat Christian angenommen.
  - JDK (`jdk-17`) und SDK (`android-sdk`) liegen unter `%USERPROFILE%\HLKS_CAD_werkzeuge` (nicht im Repo, nicht auf OneDrive), die
    Protokolle unter `logs`.
- **App-Gerüst** `src/Hlks.Quest`:
  - Aufbau nach der Vorlage StereoKit.Templates 0.10.0 (MIT). Sie zielt bereits auf .NET 10 mit StereoKit 0.3.11; damit ist die
    Frage zur Verträglichkeit geklärt.
  - Zwei Projekte mit denselben Quellen: `Hlks.Quest.csproj` (PC, Simulator) und `Projects/Android/Hlks.Quest.Android.csproj` (Brille).
  - Daten der Brillen-Fassung: android-arm64, minSdk 29, targetSdk 32, App-Kennung `ch.hkplanung.kicad.quest`, Name «KICAD», Recht
    INTERNET.
  - Bewusst nicht in `HLKS_CAD.slnx`, damit Bauten ohne Android-Workload (Cloud, `stand_pruefen`) nicht brechen.
- **Geprüft:**
  - Beide Bauten laufen ohne Warnungen aus der Kommandozeile.
  - Simulator am PC: `dotnet Hlks.Quest.dll --pruefen 120` meldet «P0 ok: Bilder=120, mittlere Bildzeit=17.5 ms, Gerät=Simulator».
  - Die APK `ch.hkplanung.kicad.quest-Signed.apk` (5,5 MB, Testschlüssel) liegt unter
    `%USERPROFILE%\HLKS_CAD_build\Hlks.Quest.Android\bin\Release\net10.0-android\publish`.
  - Damit ist **K4 am PC erfüllt**.
- **Fallstrick:** `DOTNET_ROOT` zeigt bei Christian dauerhaft auf `C:\Users\chris\.dotnet` (nur .NET 8). Die `.exe` der PC-Fassung findet
  deshalb .NET 10 nicht. Start mit `"C:\Program Files\dotnet\dotnet.exe" Hlks.Quest.dll`.
- **In der Brille (23.10.2026 ~17:59, Quest 3, Android 14, per USB-adb installiert):**
  - Die App startet als «KICAD» mit StereoKit v0.3.11 (Android ARM64, Adreno 740, OpenGL ES 3.2, XR-Modus).
  - Statuszeilen im Protokoll: «Gerät=Meta Quest 3, Laufzeit=Oculus, Darstellung=Opaque, Bildrate=72 Hz, Hände links=True,
    rechts=True».
  - Hand-Tracking ist aktiv (`XR_EXT_hand_tracking`, `XR_EXT_hand_interaction`).
  - Christian: Hände sichtbar, Würfel mit Pinch greifbar und verschiebbar («Ja, klappt gut»); Bild und Text «ruhig und lesbar».
  - **P0 abgeschlossen.**
- **Laufzeit der Quest bietet zusätzlich an** (für P4/P5 und die Ideen):
  - `XR_FB_passthrough` (Passthrough; StereoKit 0.3.11 schaltet es nicht selbst ein, das kommt in P4)
  - `XR_FB_display_refresh_rate` (90 Hz anfordern)
  - `XR_EXT_spatial_plane_tracking` (Tisch erkennen)
  - `XR_EXT_spatial_marker_tracking` (Marker am Tisch)
  - `XR_META_spatial_entity_room_mesh` (Raumscan, Idee «Bestand mit der Brille»)
  - Die App schreibt alle 5 s eine Statuszeile «P0 Status …» ins Protokoll (`adb logcat`, Kennung DOTNET). Das ist die Grundlage für
    die Messung K1.

**Stand P1 (23.10.2026):** Bibliothek `src/Hlks.Xr` (ohne StereoKit) mit den Regeln aus `vr-logik.js`, `begehen-logik.js` (Durchgang), `vr-start-logik.js`, `xr-mess-logik.js`, `xr-sitzung-logik.js`, `schnittbereich-logik.js` (Quader) und dem Hand-/Tablet-/Wischteil von `xr-greifen-logik.js`; 155 Tests (`tests/Hlks.Xr.Tests`, in `stand_pruefen.ps1` und `cloud/tests.sh`). Übersicht: `src/Hlks.Xr/LIESMICH.md`. Entscheid Hauptlauf: `Vec3` mit double (Koordinaten bis ~67 m, Toleranz 1e-9), Umrechnung auf float erst in der App; Gelenknamen von WebXR auf StereoKit bildet P4 ab; Schwellen als Stellschrauben bereitet P5 auf.

**Stand P2 (23.10.2026):**
- **Geometrie:** die Körper der 3D-Ansicht (`GET /api/koerper`, `Grafik.Koerper`).
  - Teile «netz» (IFC-Unterlage, nur Hintergrund wie `netzdatei.js`) bilden die GLB «unterlage», alles andere die GLB «modell».
  - Eigener schlanker GLB-Schreiber (`src/Hlks.Kern/Xr/GlbSchreiber.cs`), ohne Paket; geprüft von einem unabhängigen Leser in den Tests.
- **Netze:** je Gruppe (architektur, raum, hlks, sonst, unterlage), Farbe und Deckkraft.
  - Die Zuordnung Dreieck → Element-Id (bei der Unterlage die IFC-GlobalId) steht in `mesh.extras.kicad.elemente`.
  - Koordinaten wie die 3D-Ansicht: `(x, z, −y)/1000` relativ zum Projekt-Nullpunkt (`scene.extras.kicad.bezugspunktMm`).
  - Versionsschlüssel SHA-256 je Geschoss und Teil; ETag/304.
- **Endpunkte:** `GET /api/xr/geschosse`, `GET /api/xr/geschoss/{id}.glb?teil=modell|unterlage`; für die KI verboten (`xr_modell`).
- **Test_praxis** (lokale Kopie, nur gelesen):
  - alle 7 Geschosse zusammen 36 Netze, 863 223 Dreiecke, 27,9 MB
  - grösstes Geschoss EG: Unterlage 377 795 Dreiecke, 11,3 MB
  - Damit liegt das ganze Gebäude unter dem Quest-3-Budget (200 Zeichenaufrufe, 1,5 Mio. Dreiecke). K1 in der Brille ist noch zu
    messen.
- **Hinweise für P4:**
  - StereoKit dreht die GLB beim Laden um 180° um die Hochachse; die App muss das zurücksetzen.
  - Die extras liest die App selbst aus dem JSON-Chunk.
  - Räume sind mit Deckkraft 0,15 enthalten.
  - Kanten (Linien) sind nicht enthalten.
- **Nebenbefund Kern:** `FamilienNetz.GeschlossenFehler` nutzt dieselbe schwache Art von Kantenschlüssel (`Dictionary<long, …>`), die
  in P2 das EG von 30 s auf unter 1 s gebracht hat, als sie ersetzt wurde. Kommt in die Liste für Phase 1, Kern.

**Stand P3 (23.10.2026):**
- **Aufbau:**
  - Ein zweiter HTTPS-Server auf Port 5140. Er bedient nur Endpunkte unter `/api/xr`, die mit `.XrImNetz()` markiert sind.
  - Eine einzige Prüfstelle: `XrPforte.Pruefen`.
  - Zertifikat (ECDSA P-256) und Kopplungen je PC unter `%LOCALAPPDATA%\HLKS_CAD\xr`, geschützt mit DPAPI. Die Schlüssel liegen
    nur als HMAC-Prüfwert vor.
  - Suche über UDP 5141; KICAD antwortet nur auf signierte Anfragen.
  - Live-Kanal `/api/xr/kanal` mit den Nachrichten hallo, pose (höchstens 30 je Sekunde), zeigen, auftrag, stellschraube und
    messprotokoll.
  - Firewall-Skript `werkzeuge/xr_firewall.ps1`; Christian führt es selbst aus.
  - Einzelheiten: `OBERFLAECHE.md`, «Modul XR: Zugang im Büronetz».
- **Entscheide Christian 23.10.2026:**
  - `xr.zugang` gilt **je PC**.
  - Ersatzkopplung ohne Kabel: Den Code an Brille und PC **nur vergleichen** und am PC bestätigen. Der Hauptweg bleibt die
    Kopplung bei der Installation per Kabel.
- **Entscheide Hauptlauf:**
  - Kopplungen liegen je PC in `%LOCALAPPDATA%`.
  - Posen gehen im Kanal in KICAD-Koordinaten (mm, z nach oben) als gemeinsamer Bezug; die App rechnet um.

- **Nachtrag P3 (b696833):**
  - Der Schalter liegt je PC in `%LOCALAPPDATA%\HLKS_CAD\xr\zugang.json`.
  - Ersatzkopplung mit Verpflichtung und Vergleichscode, keine Eingabe in der Brille.
  - GLB über den Netzzugang erreichbar, nur GLB mit Brotli/gzip komprimiert.
- **Sicherheitsprüfung P3p (hlks-pruefer, 23.10.2026, Stand 9ea6459):** Urteil **«Ja, mit Auflagen»**, keine kritischen oder hohen
  Befunde.
  - Pfad-Tricks, falsche Methoden, Schlüssel im Query, TLS 1.1 und Klartext werden abgewiesen. Die Ersatzkopplung ist korrekt
    (Trefferchance 1 : 10⁶ je Anfrage). Die KI hat keinen Weg zum Zugang.
  - Mittel, alle zur Verfügbarkeit (Aussperren im WLAN): B1 «Ablehnen» gibt den Platz nicht frei; B2 Stundengrenze für Weg B
    global; B3 untätige Verbindungen sperren die Brille aus.
  - Gering: B4 Schlüsseldateien unter `%APPDATA%\Microsoft\Crypto\Keys`; B5 Suche bremst vor der Prüfung; B6 Format-Zeichen in
    Namen; B7 Messprotokolle ohne Gesamtgrenze.
  - Die Nacharbeit läuft beim P3-Agenten; danach prüft hlks-pruefer die geänderten Stellen nochmals.
- **Entscheide Hauptlauf zu den Fragen des Prüfers:**
  - Erster Brillentest mit Kopplung per Kabel (Weg A).
  - Firewall-Regel zusätzlich nur für `C:\Program Files\dotnet\dotnet.exe`. Der Desktop startet KICAD darüber, der Pfad ist also
    fest.
  - Brille und PC lagen am 21.10. beide in 192.168.1.x; beim Test nochmals prüfen.
  - Messprotokolle höchstens 200 MB je Tag.
- **Entscheid Christian 23.10.2026: «sicherheit im netzwerk ist kein problem.»** Das Büronetz gilt als vertrauenswürdig. Weitere Härtung des Zugangs (B1–B3, B5–B7, zusätzliche Sicherheitstests, Nachprüfung) entfällt; behoben werden nur B4 (liegengebliebene Schlüsseldateien) und die Texte H1/H2. TLS, Kopplung und Schalter bleiben, wie gebaut.
- **Auflagen für den ersten Test:** Den Zugang nur während der Testsitzung einschalten. Das Firewall-Skript nur ausführen, wenn
  `-Liste` das Netz als «Privat» zeigt.

**Erster Brillentest P4a (23.10.2026, ~20:15–20:40, Christian, Quest 3, Test_praxis-Arbeitskopie, `werkzeuge/quest_brillentest.ps1 -Kabel`):**
- **Verbindung:**
  - Gepinntes TLS zum Netzzugang, Modell über den Netzzugang, Live-Kanal mit Posen und VR-Auftrag vom PC, alles über das Kabel
    (adb reverse, 127.0.0.1 im Paket).
  - Das WLAN «s-net 3» ist in Windows «Öffentlich», und die Firewall-Regeln fehlten (Skript ohne Administratorrechte). Christian:
    «Jetzt über Kabel». **K3 (WLAN) ist offen.**
- **K1 Leistung:**
  - Mit allen Geschossen meist 72 Hz, zeitweise 54–60 Hz. Die Grafik ist der Engpass; die App rechnet 0,4 ms je Bild.
  - Christian: «beschränken auf eigenes und IFC» → es wird nur das Geschoss gezeichnet, in dem man steht, mit eigenen Bauteilen und
    IFC. Das Geschoss kommt aus dem Standboden, nicht aus der Kopfhöhe.
  - Danach im EG (382 055 Dreiecke, 5 Netze) eine Minute **stabil 72 Hz** (13,9 ms); ein Ausreisser von 24 ms beim Wechsel in den
    Durchgang. **K1 erfüllt.**
- **K2 Hände:**
  - Drehen (Pinch und Wisch) «klappt gut».
  - Springen «klappt, aber hakt»: «der strahl korrespondiert nicht mit dem feinen weissen basisstrahl», zielt schlecht, springt
    ungewollt → P4a-Nachtrag (Aim-Pose des Systems als Zielstrahl, keine Sprünge nach Drehgeste, Mindestruhe des Ziels).
- **Anzeige:** «text geht über box hinaus, text und box eher etwas klein». Box und Text sind jetzt um den Faktor 1,35 grösser, der
  Text bricht um. Christian: «Ja, passt».
- **Drehen: Automatik nach Haltung, Schalter übersteuert** (Christian, 23.10.2026: «drehen sollte nur beim sitzuen aktiviert sein.» – «der schalter soll drehen ein oder ausschalten. nicht sitzen oder stehen.» – «als präzisierung. damit kann die sitzposition übersteuert werden.»):
  - Sitzerkennung aus der echten Kopfhöhe über dem Boden (Start: unter 1,30 m während 2 s sitzend, über 1,45 m während 2 s stehend).
  - Automatik: sitzend → Drehen ein, stehend → Drehen aus.
  - Der Schalter «Drehen» ein/aus in der Box zeigt den wirksamen Zustand. Antippen übersteuert bis zum nächsten erkannten Haltungswechsel.
  - Annahme des Hauptlaufs: Die Dauer der Übersteuerung (bis zum Haltungswechsel) ist im Brillentest zu bestätigen. Umsetzung im P4a-Nachtrag.
  - **Richtung und Sperre** (Christian, 23.10.2026, nach dem zweiten Brillentest: «sehr gut, für drehen die richtung umkehren und schnellaufeinanderfolgende drehungen nur in eine richtung zulassen»):
    - Nach rechts gewischt dreht nach links; die Welt folgt der Hand.
    - Innerhalb von 1,0 s nach einer Drehung dreht ein Wisch in die Gegenrichtung nicht (Stellschraube `begehen.drehen_sperre_s`).
    - Umgesetzt vom Hauptlauf in `Hlks.Xr/HandBegehen.cs` mit Tests. Brillentest 23.10.2026 ~21:20: Christian «Das neue Drehen passt».
- **Folge der Beschränkung:** Andere Geschosse sind beim Begehen unsichtbar; der Wechsel geht bis P5 per Auftrag vom PC, danach per
  Wisch hoch/runter.

**Zweiter Brillentest P4a mit Nachtrag (23.10.2026, ~21:10–21:15, Kabel, Test_praxis):**
- Zwei Minuten **72 Hz** (13,9 ms, höchstens 16,5 ms) im 2.UG. Der Abfall auf 9 Hz am Schluss kam vom Absetzen bzw. vom Quest-Menü
  (Christian: «Ja, abgesetzt/Menü»).
- Haltung viermal richtig erkannt (sitzend → Drehen ein, stehend → Drehen aus). Christian: «Passt».
- Springen mit dem Systemzeiger (`Strahl=System`) und 0,25 s ruhigem Ziel: Christian: «Klappt gut».
- **K1 erfüllt, K2 erfüllt.** K3 (WLAN) offen: WLAN «s-net 3» auf «Privat» stellen und `werkzeuge/xr_firewall.ps1` als
  Administrator ausführen (Christian).
- Brillentest-Skript: Der Server startet jetzt per `Start-Process` ohne Umleitung, das Skript kehrt zurück. `losgeloest.ps1` (WMI)
  scheiterte hier mit Code 8.

**Stand P4b (23.10.2026, zusammengeführt):**
- **Sitzungsraum in der App:**
  - Aufträge «Im Sitzungsraum zeigen» (Etagenschnitt im Geschoss des Raums) und «Raum zeigen (VR)» (Raum ±1,5 m, oben unter der Decke).
  - Griffe mit zwei Händen, dunkler Raum mit Glastisch.
- **Passthrough** «Echter Tisch» über `XR_FB_passthrough`; Tischhöhe durch Antippen.
- **Probe-Ebene** `GET /api/xr/ebenen`, Format `hlks-xr-ebene` (Linie, Pfeil, Punkte, Partikel, Fläche); Test_praxis ohne Leitungen zeigt eine feste Probe.
- **Machbarkeit Tisch- und Markererkennung:** nur mit eigener OpenXR-Anbindung (`XR_EXT_spatial_entity`) und Spezifikation, also mit Download; nicht gebaut.
- **Tests:** Xr 295, Web 784 grün.
- **Brillentest offen:** Passthrough, Antippen, Griffe, Bildrate bei «Alle Etagen».
- **Fragen von P4b für den Brillentest:** «Im Sitzungsraum zeigen» schneidet im Geschoss des Raums statt «Alle Etagen»; die IFC-Unterlage ist im Sitzungsraum vorgabemässig ein.

**Stand P7 (23.10.2026, zusammengeführt):**
- **Dialog «Quest verbinden» › Teil «Quest App»:**
  - Netzzugang ein/aus mit Hinweisen zu Netzprofil und Firewall (nur gelesen).
  - «App installieren/aktualisieren» über WLAN oder Kabel; Kopplung Weg A per adb, Schlüssel nie auf der Platte.
  - Gekoppelte Brillen mit Widerruf; Ersatzkopplung mit Codevergleich.
- **Ablage der APK:** `%USERPROFILE%HLKS_CAD_werkzeugequest_app`, befüllt von `werkzeuge/quest_app_bereitstellen.ps1` (Hauptlauf).
- **«Brille zeigen»** (Register Ansicht › Fenster): Figur der Brille in der 3D-Ansicht; im Sitzungsraum nicht im Modell.
- **VR-Auftrag:** Ist eine App verbunden, startet KICAD keinen Browser in der Brille mehr.
- **Tests:** Web 844, Node 1247 (0 Fehler), Browser `xr-app` 77/77.
- **Offen:** echte Installation an der Brille; Frage des Agenten, ob KICAD sich den installierten Commit je Brille merken soll.

**Prüfkriterien für StereoKit** (bei K1 oder K2 rot: Gegenprobe Godot):
- **K1 Leistung:** Test_praxis mit IFC-Unterlage läuft auf der Quest 3 stabil mit mindestens 72 Hz (Meta-Minimum). Gemessen wird mit dem
  Bildzeit-Protokoll der App.
- **K2 Hände:** Teleport, Greifen und Pinch klappen zuverlässig. Massstab sind die Fehlgriffe in den Prüfaufgaben und Christians Urteil.
- **K3 Verbindung:** Nach der Installation verbindet die App im WLAN ohne adb, auch nach einem Neustart der Brille ohne Kabel.
- **K4 Bau:** Bau und Simulator laufen aus der Kommandozeile, damit Agenten bauen und testen können.

**Abnahme an der Brille:** Der Hauptlauf und Christian an Christians PC mit Test_praxis. Christian löst die Prüfaufgaben, dann folgt der
Fragebogen; die Befunde gehen in die Stellschrauben-Profile und ins Pflichtenheft. Erst danach geht es weiter mit Version 1.

**Regeln:**
- Die Quest nur nach Absprache benutzen, nie während Christian sie braucht.
- Vor jedem Agenten das Kontingent prüfen (Grenze 95 %).
- Der Code bleibt auf dem Zweig dieser Sitzung; zusammengeführt wird durch die Hauptsitzung mit Christians Zustimmung.
- Echte Daten (Test_praxis) nur lokal; die Brille lädt sie über den Zugang im Büronetz, nie über die Cloud.

### Ergänzungen (Vorschlag Sitzung Quest App, von Christian alle übernommen am 23.10.2026)
**Ins Interface-Labor (P5):**
- **Feste Gestenbedeutungen:** Jede Geste bedeutet in allen Apps dasselbe: Wisch links/rechts blättert, hoch/runter wechselt die Etage,
  Wegwischen schliesst, Pinch wählt, Faust greift, zwei Hände ändern den Massstab. Zögert man, zeigen Geisterhände die passende Geste.
  Die Bedeutungen stehen als Tabelle in der Hilfe (`/hilfe/`).
- **Schnitt mit der flachen Hand:** Im Sitzungsraum hält man die Hand waagrecht über das Modell, und der Schnitt folgt ihr. Senkrecht
  gehalten gibt sie einen Vertikalschnitt. Ergänzt die Etagenwahl.
- **Röntgen-Hand:** Wohin die offene Hand zeigt, werden Wände und Decken durchsichtig, dahinter erscheinen die Leitungen.
- **Komfort-Stellschrauben:** Beim Gehen engt sich das Blickfeld leicht ein (Stärke einstellbar), dazu ein Sitz- und ein Stehmodus.

**Von Anfang an mitdenken (Architektur, P2/P4):**
- **Brille ohne PC (Baustelle):** Die App startet auch ohne Verbindung aus dem Zwischenspeicher, mit dem zuletzt geladenen Modell.
  - Vor Ort wird das Modell per Marker oder über zwei angetippte Punkte an der echten Wand ausgerichtet.
  - Damit lassen sich Soll gegen Ist und die Aussparungen kontrollieren.
  - Marker und Notizen von der Baustelle gleicht die App ab, sobald sie wieder mit dem PC verbunden ist.
  - Zum Prototyp gehört davon nur der Zwischenspeicher, der ohne PC startet.
- **Schnittstelle für Fachmodule:** Fachmodule legen eigene Ebenen in die Szene, z. B. animierte Fliessrichtung, Luftströmung als Partikel
  (10a/10b) oder Temperaturfelder. Der Inhalt kommt vom Server; die App zeigt Linien, Pfeile, Partikel und Farbflächen. Im Prototyp
  genügt eine Probe-Ebene.
- **Claude sieht mit (XR-D):** «Hey Claude» bekommt Standpunkt, Blickrichtung und das Element, auf das man zeigt. Fragen wie «Was ist
  das hier?» beziehen sich darauf. Der Kanal (P3) überträgt dafür das gezeigte Element.

**Vorrat nach Version 1:**
- **System verfolgen:** Eine Leitung antippen, dann leuchtet der ganze Strang und die Fliessrichtung läuft mit. Mit dem Finger der
  Leitung entlangfahren. Der Kern kennt den Netzdurchlauf schon (`NetzSchritt`).
- **Kollisionen begehen:** Die Liste aus Modul 3b in VR. Ein Wisch springt zur nächsten Kollision, der Status wird per Geste gesetzt,
  das Ergebnis geht als BCF hinaus. Freiräume für die Wartung vor Armaturen und Geräten werden als eigene Körper geprüft.
- **Führung für Bauherr und Architekt:** vorbereitete Standpunkte, per Wisch durchblättert; ein Gast folgt mit (XR-G).
- **Bestand mit der Brille erfassen:** Die Raumeinrichtung der Quest 3 vermisst Wände und Möbel; als grobe Bestandsgrundlage für
  Phase 2 (Gebäudeerfassung). Die Genauigkeit ist unbekannt und zuerst zu prüfen.

### Pflichtenheft
Die Abschnitte XR-A bis XR-H und «Start ohne Link-fähigen PC» unten beschreiben, was der Browser-Prototyp kann und was Christian beim
Brillentest festgelegt hat. Sie gelten als Anforderungen an die App: Gesten, Masse, Schwellenwerte, Abläufe.

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
- **Räume nur bis unter die Decke** (Christian 23.10.2026, über die Hauptsitzung: «bei der anzeige von räumen in VR soll nur bis zur
  unterkannte der Decke angezeigt werden. so dass in den Raum geschaut werden kann.»):
  - Gilt für «Raum zeigen (VR)» und den Sitzungsraum, nicht fürs Begehen (Entscheid Christian 23.10.2026). Beim Begehen bleibt die
    Decke über einem sichtbar.
  - Der Ausschnitt eines Raums endet oben an der Unterkante der Decke des Raumgeschosses. Es gilt dieselbe Regel wie beim Etagenschnitt:
    `SchnittHoeheMm`, 50 mm unter der Unterkante der Decke (`SCHNITT_TIEFER_MM`). Ohne Decke gilt die Kote des Geschosses darüber
    minus 50 mm.
  - Seitlich und nach unten bleibt der Rand von 1,5 m.
  - Umgesetzt in der App (`SitzungAuswahl.Ausschnitt`, Test). Der Browser-Prototyp bleibt unverändert, da er nur noch
    Fehlerbehebungen bekommt.
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
  - **Autostart beim Aufsetzen gebaut 21.10.2026 (hlks-modul), mit der Brille bestätigt 21.10.2026 ~21:55** (Christian: «die Begehung startet beim Aufsetzen») (Christian: «ich möchte nach dem klick am pc nur noch die brille anziehen, und dann startet die begehung direkt»): `QuestStarter.cs`, Einstellung `quest.autostart` (Vorgabe an), `GET /api/quest/autostart`. Beschreibung: `OBERFLAECHE.md`, Abschnitt «Modul XR: Autostart».
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
Seit 22.10.2026 gilt das Übertheme «Quest App». Im Gesamtablauf (`entwurf/ablauf.md`) gehört die App in **Phase 1, Grundlagen** – Christian 22.10.2026: «dies nehmen wir in Grundlagen. alle anderen systeme bauen darauf auf.»
1. Grundsatzentscheide – gefällt am 23.10.2026 (oben).
2. Prototyp als Interface-Labor (P0–P8, oben), Abnahme an der Brille.
3. Version 1: Verbinden und Kopplung, Auftrag vom PC und Autostart, XR-A, XR-H, Messen, XR-E, PC sieht Brille – mit der Bedienung, die
   das Labor ergeben hat (schwebende Apps oder Tablet).
4. Danach XR-B, XR-D (mit «Claude sieht mit»), XR-F, XR-G; Vorrat: System verfolgen, Kollisionen begehen, Führung, Bestand mit der Brille
   (Abschnitt «Ergänzungen»).

Jeder Teil wird mit der Brille an Christians PC abgenommen. Der Browser-Prototyp bekommt nur noch Fehlerbehebungen.

## Offen (Freitag bzw. erster Brillentest)
- Welche schwebenden Apps es gibt und wie sie angeordnet sind (klärt das Labor)
- lokale Spracherkennung
- Marker als BCF
- Tastenbelegung bzw. Gesten aus N15/N23 bestätigen
- XR-H: Schnitt unter oder über der Decke des gewählten Geschosses, Aussehen des Raums, Platz in der Reihenfolge
