// Symbolsatz im Stil von Revit (Teil S, entwurf/menueband-revit.md) – zuschaltbar (Benutzereinstellung «oberflaeche.symbole» = «revit», Vorgabe «strich»).
//
// URHEBERRECHT: Alle Zeichnungen hier sind EIGENE Entwürfe von Hand in SVG; es wurde nichts aus Revit/Autodesk (Installation, Bildschirmfoto, Internet) übernommen
// oder abgezeichnet. «Im Stil von Revit» heisst nur: gleiche Bildidee je Befehl (Wand als isometrischer Block, Rohr als Zylinder in Medienfarbe, 3D-Würfel,
// Planblatt als Blatt …), gleiche Grössen (32 × 32 gross, 16 × 16 klein, je eigens gezeichnet, nicht verkleinert) und ähnliche Farbgebung
// (Grautöne für Bauteile, Blau als Hauptakzent, Medienfarben bei HLKS, Gelb/Orange für Hervorhebung, Rot für Löschen).
//
// FARBEN: Die Zeichnungen tragen keine festen Farben, nur Klassen (f1 f2 f3 fb fl fvl … siehe css/symbole-revit.css); die Werte stehen dort als CSS-Variablen
// --sy-* je für hell und dunkel. Dokumentierte Ausnahmen: keine.
//
// AUFBAU: revitSymbol(name, px) liefert das SVG (viewBox 0 0 32 32 ab px > 20, sonst die 16-er Zeichnung 0 0 16 16) oder null (dann gilt der Strich).
// REVIT_NAMEN sind alle Schlüssel mit Revit-Fassung; ZUORDNUNG ist die Tabelle unten. Die Schlüssel sind dieselben wie in oberflaeche/symbole.js und
// werkzeuge/symbole.js; kommt dort ein Symbol dazu, fehlt hier die Fassung, bis sie gezeichnet ist: dann gilt für dieses Symbol der Strich.
//
// ZUORDNUNG  Schlüssel | Befehl in KICAD | Revit-Befehl (nur der Name; «eigenes» = ohne Entsprechung) | Bildidee
const TABELLE = `
ordner | Projekt öffnen | Öffnen | gelber Aktenordner
neu | Projekt anlegen / Neue Familie | Neu | Blatt mit blauem Plus
beispiel | Beispielprojekt anlegen | eigenes | Haus mit gelbem Stern
schliessen | Projekt schliessen | Schliessen | Blatt mit rotem Kreuz
zuletzt | Zuletzt geöffnet | Zuletzt verwendet | Uhr mit blauem Rand
wand | Wand | Wand | isometrischer Mauerblock mit Fugen
tuer | Tür | Tür | Grundriss: Wandstücke, Türblatt, blauer Schwenkbogen
fenster | Fenster | Fenster | Fensteransicht mit hellblauen Scheiben und Fensterbank
raum | Raum | Raum | Raum mit Wandrahmen und Raumstempel
rohr | Rohr | Rohr | Zylinder in Medienfarbe (Heizung Vorlauf)
kanal | Kanal | Luftkanal | isometrischer Rechteckkanal mit Strömungspfeil in Luftfarbe
formteil | Formteil | Rohrformstück | T-Stück in Medienfarbe (Sanitär)
apparat | Apparat | Mechanische Geräte | Speicher mit blauem Band und Vor-/Rücklaufstutzen
armatur | Armatur | Rohrzubehör | Absperrventil mit blauem Handrad
auswaehlen | Ändern / Abbrechen | Ändern | Zeigerpfeil
loeschen | Löschen | Löschen | rotes Kreuz
rueckgaengig | Rückgängig | Rückgängig | blauer Bogenpfeil nach links
wiederholen | Wiederholen | Wiederherstellen | blauer Bogenpfeil nach rechts
eigenschaften | Eigenschaften | Eigenschaften | Tafel mit Schlüssel-Wert-Zeilen
grundriss | Grundriss | Grundrissansicht | Planblatt mit Grundriss
ansicht3d | 3D-Ansicht | 3D-Ansicht | isometrischer Würfel, Oberseite hellblau
liste | Liste / Materialliste | Bauteilliste | Tabelle mit blauer Kopfzeile
schnitt | Schnitt (Ansichtsart) | Schnitt | Ansichtsblatt mit blau gestrichelter Schnittlinie
schema | Schema | eigenes | Kästchen mit Leitungen
duplizieren | Ansicht duplizieren / Kopieren | Ansicht duplizieren | zwei gestaffelte Blätter
sichtbarkeit | Sichtbarkeit/Grafiken | Sichtbarkeit/Grafiken | Auge mit blauer Iris
vorlageSpeichern | Als Vorlage speichern | Ansichtsvorlage erstellen | blaue Diskette
vorlageAnwenden | Vorlage anwenden | Ansichtsvorlage anwenden | Blatt mit blauem Haken
nebeneinander | Fenster nebeneinander | Fenster nebeneinander | zwei Fenster mit blauer Titelleiste
reiter | Fenster als Reiter | Reiter | Fenster mit Reiterlasche
eigenesFenster | In eigenem Fenster öffnen | eigenes | zwei überlappende Fenster
zoom | Zoom | Zoomen | Lupe mit hellblauem Glas
zoomAlle | Zoom alles | Zoom auf alles | blaue Eckwinkel um ein Blatt
zoomAlleAnsichten | Zoom alle Ansichten | Zoom alle Ansichten | blaue Eckwinkel um zwei Blätter
info | Projektinformationen | Projektinformationen | blauer Kreis mit weissem i
haus | Startbildschirm | Startseite | Haus mit blauem Dach
synchronisieren | Synchronisieren | Mit Zentraldatei synchronisieren | zwei blaue Kreispfeile
freigeben | Meine Elemente freigeben | Alle freigeben | offenes Vorhängeschloss in Gelb
befunde | Prüfbefunde | Warnungen | gelbes Warndreieck
katalog | Katalog anzeigen | eigenes | aufgeschlagenes Buch mit blauem Einband
tastatur | Tastaturkürzel | Tastaturkürzel | Tastatur
netzAb | Stromab folgen | eigenes | Leitung mit Pfeil nach rechts
netzAuf | Stromauf folgen | eigenes | Leitung mit Pfeil nach links
warum | Warum | eigenes | blauer Kreis mit weissem Fragezeichen
planblatt | Planblatt | Planblatt | Blatt mit Zeichenfeld und blauem Schriftfeld
serienpdf | Serien-PDF | Drucken | gestapelte Blätter mit rotem PDF-Band
gebaeude | Gebäude | eigenes | isometrisches Hochhaus mit Fenstern
geschoss | Geschoss | Geschoss | zwei gestapelte Platten, Oberseite hellblau
anlage | Anlage | eigenes | Wärmeerzeuger mit Leitung zu zwei Verbrauchern
system | System | Rohrsystem | Knoten mit Verzweigung
strang | Strang | eigenes | Rohr mit blauen Endmarken
familie | Familie | Familie | isometrischer Würfel, Oberseite blau
vorlage | Vorlage | eigenes | Blatt mit blau gestrichelter Kante
suche | Suche | Suchen | Lupe mit blauem Ring
umbenennen | Umbenennen | Umbenennen | gelber Bleistift
oeffnen | Öffnen | Öffnen | Ordner mit blauem Pfeil
design | Design hell/dunkel | eigenes | Kreis, halb hell, halb dunkel
hilfe | Hilfe | Hilfe | blauer Kreis mit weissem Fragezeichen
anleitung | Bedienungsanleitung | Hilfe | blaue Sprechblase mit Fragezeichen
huelleAnsicht | Darstellung Hülle | eigenes | Grundriss mit orangem Streifen an der Aussenwand
hkapVerbinden | HKAP verbinden | eigenes | zwei Kästchen mit blauer Verbindung
hkapAbgleich | HKAP Abgleich | eigenes | zwei Tabellen mit blauen Gegenpfeilen
hkapBefunde | Prüfbefunde an HKAP senden | eigenes | Warndreieck mit blauem Pfeil
decke | Decke | Decke | isometrische Platte auf Stützen
boden | Boden | Boden | isometrische Platte, Stützen darüber
dach | Dach | Dach | geneigte Dachflächen auf Wänden
aufbau | Aufbau | Bauteilaufbau bearbeiten | Schichten im Schnitt (Putz, Dämmung, Beton)
zuweisen | Zuweisen | eigenes | Farbmuster mit blauem Pfeil
material | Material | Materialien | Mauerwerk aus orangen Steinen
fensterKennwerte | Fensterkennwerte | eigenes | Fenster mit Kennwertzeilen
uebergang | Wärmeübergang | eigenes | Wand mit rotem und blauem Wärmepfeil
huellePruefen | Hülle prüfen | Prüfen | Haus mit grünem Haken
waermebruecke | Wärmebrücke | eigenes | zwei Wandblöcke mit roter Brücke
raumgrenzen | Raumgrenzen | Raumbegrenzung | Raum mit orangem Grenzrahmen
huellbauteile | Hüllbauteile | eigenes | Haus mit orangem Hüllumriss
raumOffen | Raum noch offen | eigenes | gestrichelter Raum mit Fragezeichen
famParameterNeu | Parameter hinzufügen | Parameter hinzufügen | Tabelle mit blauem Plus
famParameter | Parameter | Familienparameter | Tabelle mit blauer Kopfzeile
famTypNeu | Typ hinzufügen | Neuer Typ | Typentabelle mit blauem Plus
famTypKopie | Typ kopieren | Typ duplizieren | Typentabelle, zweite dahinter
famTypWeg | Typ löschen | Typ löschen | Typentabelle mit rotem Kreuz
famTypen | Typen | Familientypen | Typentabelle
famKoerper | Körper | Körper | isometrischer Würfel
famKoerperWeg | Körper löschen | Löschen | Würfel mit rotem Kreuz
famQuader | Quader | Extrusion | isometrischer Quader
famZylinder | Zylinder | Extrusion (rund) | stehender Zylinder
famKegel | Kegel | Verschmelzung | Kegel
famKugel | Kugel | eigenes | Kugel mit Lichtpunkt
famExtrusion | Extrusion | Extrusion | L-Profil, extrudiert
famRotation | Rotation | Rotation | Profil um gestrichelte Achse
famSweep | Sweep | Sweep | Rohrkörper entlang blauem Pfad
famAnschluss | Anschluss hinzufügen | Verbinder | Gerät mit Anschlussstutzen und Pfeil
famAnschluesse | Anschlüsse | Verbinder | Gerät mit zwei Stutzen
famSinnbild | Sinnbild bearbeiten | Symbolische Linien | gestrichelter Rahmen mit Kreis
famVier | Vier Fenster | Kacheln | vier Ansichtskacheln, eine blau
famVorne | Ansicht Vorne | Vorne | Quader mit blauer Vorderfläche
famHinten | Ansicht Hinten | Hinten | Quader mit blauer Rückseite
famLinks | Ansicht Links | Links | Quader mit blauer Seite links
famRechts | Ansicht Rechts | Rechts | Quader mit blauer Seite rechts
famOben | Ansicht Oben | Oben | Quader mit blauer Oberfläche
famUnten | Ansicht Unten | Unten | Quader mit blauer Unterseite
famGrob | Detailgrad grob | Detailgrad Grob | einfaches Viereck
famMittel | Detailgrad mittel | Detailgrad Mittel | Viereck mit einer Teilung
famFein | Detailgrad fein | Detailgrad Fein | Viereck mit feiner Teilung
famPruefen | Familie prüfen | eigenes | Blatt mit grünem Haken
famPlatzieren | Platzieren | Bauteil platzieren | Gerät mit blauem Pfeil nach unten
famEditor | Bearbeiten | Familie bearbeiten | Blatt mit Bleistift
famUnterlagen | Familie aus Unterlagen | eigenes | Blatt mit blauem Pfeil nach oben
famEntwurf | Entwurf prüfen | eigenes | Blatt mit Haken
famBefunde | Unterlagen und Befunde | eigenes | Blatt mit Befundzeilen
famBibliothek | Familien im Explorer | Familien-Browser | Regal mit Familien
famAktualisieren | Aktualisieren | Neu laden | zwei blaue Kreispfeile
koVorgaben | Projektvorgaben | Projekteinstellungen | Regler
koTypenliste | Typenliste | Typenliste | drei Muster mit Namenszeilen
koPruefen | Kollisionen prüfen | Interferenzprüfung | zwei sich durchdringende Körper, orange Blitzmarke
koListe | Kollisionsliste | Interferenzbericht | Liste mit orangen Marken
koAusVorschlagen | Aussparungen vorschlagen | eigenes | Wand mit gestrichelter Öffnung und Pfeil
koAusNummer | Aussparungen nummerieren | eigenes | Wand mit Öffnung und Nummer
koAusListe | Aussparungsliste | eigenes | Liste mit Öffnungsmarken
koAbgleich | Abgleich | eigenes | zwei Felder mit Gegenpfeilen
koAusPlan | Aussparungsplan anlegen | eigenes | Planblatt mit Öffnung
koAusIfc | IFC für Architekt | IFC exportieren | Würfel mit gestrichelter Öffnung
koBcfExport | BCF exportieren | eigenes | Blatt mit Pfeil heraus
koBcfImport | BCF importieren | eigenes | Blatt mit Pfeil hinein
koPlan | Koordinationsplan | eigenes | Grundriss mit drei farbigen Leitungen und einer grauen Box
koBoxen | Aussparungsboxen | eigenes | graue Box mit drei Zeilen und Führungslinie zum Kreis
sanZusammenstellung | Sanitär-Zusammenstellung | eigenes | Tabelle mit blauem Σ-Kopf und grünem Wassertropfen
famFamilie | In das Projekt laden | In Projekt laden | Würfel mit blauem Punkt
palette | Palette | Paletten | Fenster mit blauer Titelleiste
projektbrowser | Projektbrowser | Projektbrowser | Baumstruktur
koordination | Koordination (Palette) | eigenes | Liste unten im Fenster
palettenZurueck | Paletten zurücksetzen | Benutzeroberfläche | Fenster mit Rückpfeil
claude | Claude (Palette) | eigenes | Sprechblase in Hellblau mit gelbem Funken
achse | Achse | Raster | Rasterkreis mit Strich-Punkt-Linie
bemassung | Bemassung | Bemaßung | Masslinie mit Pfeilen
text | Text | Text | Buchstabe A
linie | Detaillinie | Detaillinie | blauer Linienzug mit Endpunkten
fuellbereich | Füllbereich | Gefüllter Bereich | schraffierte Fläche
kote | Höhenkote | Höhenkote | Kotendreieck mit Linie
nordpfeil | Nordpfeil | Nordpfeil | Kompass mit rotem Nordpfeil
schnittlinie | Schnitt | Schnitt | Schnittlinie mit blauen Schnittköpfen
verschieben | Verschieben | Verschieben | blaues Pfeilkreuz
drehen | Drehen | Drehen | Kreispfeil um Drehpunkt
spiegeln | Spiegeln | Spiegeln | Spiegelachse mit zwei Dreiecken
ifcUebernehmen | IFC übernehmen | IFC verknüpfen | Würfel mit blauem Pfeil nach unten
cadEinfuegen | CAD einfügen | CAD verknüpfen | Blatt mit CAD-Linien
pdfEinfuegen | PDF einfügen | PDF importieren | Blatt mit rotem PDF-Band
bildEinfuegen | Bild einfügen | Bild | Landschaftsbild
kalibrieren | Kalibrieren | Massstab | Lineal zwischen zwei Punkten
unterlagen | Unterlagen verwalten | Verknüpfungen verwalten | gestapelte Blätter
schemaVerbinden | Schema: Verbinden | eigenes | zwei Kästchen mit Verbindung und Plus
schemaTrennen | Schema: Trennen | eigenes | zwei Kästchen, Verbindung durchtrennt
schemaEinfuegen | Schema: Element einfügen | eigenes | gestrichelter Kasten mit Plus
schemaAnordnen | Schema: Neu anordnen | eigenes | Baum aus Kästchen
koordinaten | Koordinaten | Koordinaten | Achsenkreuz mit Ursprung
einlageplan | Einlageplan | eigenes | Plan mit grauer Betonplatte, Abwasserleitung (braun) darin eingelegt
grundleitungsplan | Grundleitungsplan | eigenes | Plan mit Bodenplatte über Erdreich, Abwasser-Grundleitung (braun) mit Gefällepfeil
listeCsv | Liste als CSV | eigenes | Tabelle mit Blatt davor, blaue Kommas
ifcExport | IFC exportieren | IFC exportieren | Würfel mit blauem Pfeil heraus nach rechts unten
problemMarker | Problem-Marker (Palette, Quest App) | eigenes | orange Stecknadel mit weissem Ausrufezeichen
questVerbinden | Quest verbinden (Register XR) | eigenes | graue Brille mit hellblauem Visier, blauer Pfeil nach rechts darunter
appInstallieren | App installieren (Register XR) | eigenes | blauer Pfeil nach unten über der Brille
autostart | Autostart der Begehung (Register XR) | eigenes | grüner Kreis mit weissem Startdreieck
brilleZeigen | Brille zeigen (Register XR) | eigenes | Brille über einem Auge mit blauer Iris
anBrille | An die Brille senden (Register XR) | eigenes | blaue Funkwellen über der Brille
raumAr | Raum in AR (Register XR) | eigenes | grauer Raumquader in blauen Eckmarken
xrLabor | XR-Labor (Register XR) | eigenes | Messkolben mit blauer Füllung und weissen Bläschen
raumBegehen | Raum begehen / In VR begehen | eigenes | blaue Figur auf grauer Bodenplatte
sitzungstisch | Sitzungstisch (Quest App, App «Tisch») | eigenes | Tisch mit Modellwürfel, Seiten blau
gesten | Gesten (Quest App) | eigenes | gelbe offene Hand
haken | Erledigt (Quest App) | eigenes | grüner Kreis mit weissem Haken
bildschirm | Desktop in der Brille (Quest App V1c) | eigenes | grauer Bildschirm mit hellblauer Fläche auf dem Fuss
`;

export const ZUORDNUNG = TABELLE.trim().split('\n').map((z) => z.split('|').map((t) => t.trim()));

// ---------------------------------------------------------------------------------------------- Bausteine

const REVIT = {};   // Schlüssel → [SVG-Inhalt 32, SVG-Inhalt 16]
const s = (name, g32, g16) => { REVIT[name] = [g32, g16]; };

const DOC32 = '<path class="fp" d="M7.5 2.5h12l6 6v21h-18z"/><path class="f2" d="M19.5 2.5v6h6z"/>';
const DOC16 = '<path class="fp" d="M3.5 1.5h6l3 3v10h-9z"/><path class="f2" d="M9.5 1.5v3h3z"/>';
const PLUS32 = '<circle class="fb n" cx="23.5" cy="23.5" r="6.5"/><path class="sW w2" d="M23.5 20.5v6M20.5 23.5h6"/>';
const PLUS16 = '<circle class="fb n" cx="11.5" cy="11.5" r="4.5"/><path class="sW w15" d="M11.5 9.3v4.4M9.3 11.5h4.4"/>';
const KREUZ32 = '<circle class="fr n" cx="23.5" cy="23.5" r="6.5"/><path class="sW w2" d="M21 21l5 5M26 21l-5 5"/>';
const KREUZ16 = '<circle class="fr n" cx="11.5" cy="11.5" r="4.5"/><path class="sW w15" d="M9.7 9.7l3.6 3.6M13.3 9.7l-3.6 3.6"/>';
const HAKEN32 = '<circle class="fsa n" cx="23.5" cy="23.5" r="6.5"/><path class="sW w2" d="M20.5 23.5l2.4 2.5 4-4.8"/>';
const HAKEN16 = '<circle class="fsa n" cx="11.5" cy="11.5" r="4.5"/><path class="sW w15" d="M9.3 11.7l1.6 1.6 2.8-3.2"/>';
const QUADER32 = '<path class="f1" d="M16 3.5l11.5 6.5-11.5 6.5-11.5-6.5z"/><path class="f2" d="M4.5 10l11.5 6.5v12.5l-11.5-6.5z"/><path class="f3" d="M27.5 10v12.5l-11.5 6.5v-12.5z"/>';
const QUADER16 = '<path class="f1" d="M8 1.5l6 3.5-6 3.5-6-3.5z"/><path class="f2" d="M2 5l6 3.5v6l-6-3.5z"/><path class="f3" d="M14 5v6l-6 3.5v-6z"/>';
const TAB32 = '<rect class="fp" x="3.5" y="5.5" width="25" height="21"/><path class="fb" d="M3.5 5.5h25v5h-25z"/><path d="M3.5 15.5h25M3.5 20.5h25M12.5 10.5v16"/>';
const TAB16 = '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fb" d="M1.5 2.5h13v3h-13z"/><path d="M1.5 8.5h13M1.5 11h13M6.5 5.5v8"/>';

// ---------------------------------------------------------------------------------------------- Datei, Architektur, Gebäudetechnik

s('ordner',
  '<path class="fy" d="M3.5 6.5h8.5l3 3h13.5v17h-25z"/><path class="fo" d="M3.5 13.5h25v13h-25z"/><path class="sW op" d="M5.5 15.5h21"/>',
  '<path class="fy" d="M1.5 3.5h4.5l1.8 1.8h6.7v8.2h-13z"/><path class="fo" d="M1.5 7.5h13v6h-13z"/>');
s('neu', `${DOC32}<path class="op" d="M10.5 11.5h12M10.5 15.5h12M10.5 19.5h6"/>${PLUS32}`, `${DOC16}${PLUS16}`);
s('beispiel',
  '<path class="f3" d="M3.5 15.5L16 5.5l12.5 10z"/><path class="f1" d="M6.5 15.5h19v13h-19z"/><rect class="fl" x="9.5" y="18.5" width="4" height="4"/><rect class="fl" x="18.5" y="18.5" width="4" height="4"/><rect class="f2" x="14" y="22.5" width="4" height="6"/><path class="fy" d="M26 2.5l1.3 2.8 3 .4-2.2 2 .6 3-2.7-1.5-2.7 1.5.6-3-2.2-2 3-.4z"/>',
  '<path class="f3" d="M1.5 8L8 2.5 14.5 8z"/><path class="f1" d="M3.5 8v6.5h9V8"/><rect class="fl" x="5" y="9.5" width="2" height="2"/><rect class="f2" x="9" y="10.5" width="2" height="4"/><path class="fy" d="M13 0.8l.6 1.3 1.4.2-1 1 .3 1.4-1.3-.7-1.3.7.3-1.4-1-1 1.4-.2z"/>');
s('schliessen', `${DOC32}<path class="op" d="M10.5 11.5h12M10.5 15.5h12M10.5 19.5h6"/>${KREUZ32}`, `${DOC16}${KREUZ16}`);
s('zuletzt',
  '<circle class="fp sb w2" cx="16" cy="16" r="11.5"/><path class="w15" d="M16 9v7.5l5 3"/><circle class="fk n" cx="16" cy="16" r="1.4"/>',
  '<circle class="fp sb w15" cx="8" cy="8" r="6.2"/><path d="M8 4v4.3l3 1.7"/>');
s('wand',
  '<path class="f1" d="M2.5 21.5l22-12 5 2.5-22 12z"/><path class="f2" d="M2.5 21.5l5 2.5v5.5l-5-2.5z"/><path class="f3" d="M7.5 24l22-12v5.5l-22 12z"/><path class="op" d="M7.5 26.8l22-12M13.5 21.4v3.1M21.5 17.1v3.1M17.5 23.9v3.1M25.5 19.7v3.1"/>',
  '<path class="f1" d="M1.5 8.5l10-5.5 3 1.5-10 5.5z"/><path class="f2" d="M1.5 8.5l3 1.5v4.5l-3-1.5z"/><path class="f3" d="M4.5 10l10-5.5V9l-10 5.5z"/><path class="op" d="M4.5 12.3l10-5.5M8 8.1v2.4M11.5 6.2v2.4"/>');
s('tuer',
  '<path class="f3" d="M2.5 21.5h9v6h-9zM23.5 21.5h6v6h-6z"/><path class="sb w15" d="M11.5 9.5A12 12 0 0 1 23.5 21.5"/><path class="f1" d="M10.5 9.5h2v12h-2z"/>',
  '<path class="f3" d="M1.5 11.5h4v3.5h-4zM11.5 11.5h3v3.5h-3z"/><path class="sb" d="M5.5 5.5A6 6 0 0 1 11.5 11.5"/><path class="f1" d="M4.5 5.5h2v6h-2z"/>');
s('fenster',
  '<rect class="f2" x="4.5" y="3.5" width="23" height="23"/><path class="fl" d="M7.5 6.5h8v8h-8zM16.5 6.5h8v8h-8zM7.5 15.5h8v8h-8zM16.5 15.5h8v8h-8z"/><path class="f3" d="M2.5 26.5h27v3h-27z"/>',
  '<rect class="f2" x="2.5" y="1.5" width="11" height="11"/><path class="fl" d="M4.5 3.5h3v3h-3zM8.5 3.5h3v3h-3zM4.5 7.5h3v3h-3zM8.5 7.5h3v3h-3z"/><path class="f3" d="M1.5 12.5h13v2h-13z"/>');
s('raum',
  '<rect class="fl n" x="6.5" y="6.5" width="19" height="19"/><path class="f3 eo" d="M3.5 3.5h25v25h-25zM6.5 6.5v19h19v-19z"/><path class="fp" d="M10.5 12.5h11v7h-11z"/><path d="M10.5 12.5l11 7M21.5 12.5l-11 7"/>',
  '<rect class="fl n" x="3.5" y="3.5" width="9" height="9"/><path class="f3 eo" d="M1.5 1.5h13v13h-13zM3.5 3.5v9h9v-9z"/><path class="fp" d="M5.5 6h5v4h-5z"/><path d="M5.5 6l5 4M10.5 6l-5 4"/>');
s('rohr',
  '<path class="fvl" d="M8.5 11.5h15v10h-15a3 5 0 0 1 0-10z"/><ellipse class="f1" cx="23.5" cy="16.5" rx="3" ry="5"/><path class="sW op" d="M9 13.5h14"/>',
  '<path class="fvl" d="M4.5 5.5h9v6h-9a2 3 0 0 1 0-6z"/><ellipse class="f1" cx="13.5" cy="8.5" rx="2" ry="3"/>');
s('kanal',
  '<path class="f1" d="M3.5 13.5l5-5h18l-5 5z"/><path class="f2" d="M3.5 13.5h18v12h-18z"/><path class="f3" d="M21.5 13.5l5-5v12l-5 5z"/><path class="szu w2" d="M6.5 19.5h8"/><path class="fzu n" d="M14 15.5l5 4-5 4z"/>',
  '<path class="f1" d="M1.5 7.5l3-3h9l-3 3z"/><path class="f2" d="M1.5 7.5h9v6h-9z"/><path class="f3" d="M10.5 7.5l3-3v6l-3 3z"/><path class="szu w15" d="M3 10.5h3"/><path class="fzu n" d="M5.5 8.5l2.7 2-2.7 2z"/>');
s('formteil',
  '<path class="fsa" d="M3.5 7.5h25v8h-8.5v12h-7v-12h-9.5z"/><path d="M7.5 7.5v8M24.5 7.5v8M12.5 22.5h7"/><path class="sW op" d="M4.5 9.5h23"/>',
  '<path class="fsa" d="M1.5 2.5h13v4h-4.5v7h-4v-7h-4.5z"/><path d="M3.5 2.5v4M12.5 2.5v4"/>');
s('apparat',
  '<path class="f2" d="M7.5 8.5v15a8.5 3 0 0 0 17 0v-15z"/><ellipse class="f1" cx="16" cy="8.5" rx="8.5" ry="3"/><path class="fb" d="M7.5 14.5a8.5 3 0 0 0 17 0v3a8.5 3 0 0 1-17 0z"/><path class="fvl" d="M24.5 10.5h4v3h-4z"/><path class="frl" d="M3.5 21.5h4v3h-4z"/>',
  '<path class="f2" d="M3.5 4.5v7a4.5 1.6 0 0 0 9 0v-7z"/><ellipse class="f1" cx="8" cy="4.5" rx="4.5" ry="1.6"/><path class="fb" d="M3.5 7.5a4.5 1.6 0 0 0 9 0v2a4.5 1.6 0 0 1-9 0z"/><path class="fvl" d="M12.5 5.5h2.5v1.5h-2.5z"/><path class="frl" d="M1 9.5h2.5v1.5h-2.5z"/>');
s('armatur',
  '<path class="f2" d="M3.5 14.5l12.5 6.5-12.5 6.5zM28.5 14.5l-12.5 6.5 12.5 6.5z"/><path d="M16 21v-9"/><ellipse class="fb" cx="16" cy="10" rx="6.5" ry="2.5"/>',
  '<path class="f2" d="M1.5 7.5l6.5 3.5-6.5 3.5zM14.5 7.5l-6.5 3.5 6.5 3.5z"/><path d="M8 11V5"/><ellipse class="fb" cx="8" cy="4" rx="3.5" ry="1.4"/>');

// ---------------------------------------------------------------------------------------------- Ändern und Ansichten

s('auswaehlen',
  '<path class="fb n" d="M10.5 5.5l15 13.5-6.5 1 4 8.5-3.5 1.7-4-8.5-5 4.5z"/><path class="fp" d="M8.5 3.5l15 13.5-6.5 1 4 8.5-3.5 1.7-4-8.5-5 4.5z"/>',
  '<path class="fb n" d="M4.5 2.5l8 7.5-3.5.5 2.2 4.5-1.8.9-2.2-4.5-2.7 2.3z"/><path class="fp" d="M3.5 1.5l8 7.5-3.5.5 2.2 4.5-1.8.9-2.2-4.5-2.7 2.3z"/>');
s('loeschen', '<path class="sr w4" d="M7.5 7.5l17 17M24.5 7.5l-17 17"/>', '<path class="sr w3" d="M3.5 3.5l9 9M12.5 3.5l-9 9"/>');
s('rueckgaengig',
  '<path class="sb w3" d="M11 14.5h8a6.5 6.5 0 0 1 0 13h-6"/><path class="fb n" d="M3.5 14.5l9-8v16z"/>',
  '<path class="sb w2" d="M5.5 6.5h4a3.5 3.5 0 0 1 0 7h-3"/><path class="fb n" d="M1.5 6.5l5-4v8z"/>');
s('wiederholen',
  '<path class="sb w3" d="M21 14.5h-8a6.5 6.5 0 0 0 0 13h6"/><path class="fb n" d="M28.5 14.5l-9-8v16z"/>',
  '<path class="sb w2" d="M10.5 6.5h-4a3.5 3.5 0 0 0 0 7h3"/><path class="fb n" d="M14.5 6.5l-5-4v8z"/>');
s('eigenschaften',
  '<rect class="fp" x="4.5" y="3.5" width="23" height="25"/><path class="f3" d="M4.5 3.5h23v5h-23z"/><path class="fl" d="M7.5 12h6v3h-6zM7.5 17h6v3h-6zM7.5 22h6v3h-6z"/><path d="M16.5 13.5h8M16.5 18.5h8M16.5 23.5h8"/>',
  '<rect class="fp" x="2.5" y="1.5" width="11" height="13"/><path class="f3" d="M2.5 1.5h11v3h-11z"/><path class="sb" d="M4 7h2.5M4 9.5h2.5M4 12h2.5"/><path d="M8 7h4M8 9.5h4M8 12h4"/>');
s('grundriss',
  '<rect class="fp" x="3.5" y="3.5" width="25" height="25"/><rect class="fl n" x="9.5" y="9.5" width="13" height="6"/><path class="w3" d="M8 8h16v16H8zM8 16.5h7M15 16.5v7.5"/><path class="sb" d="M19 16.5a5 5 0 0 1 5 5"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><rect class="fl n" x="5" y="5" width="6" height="3"/><path class="w2" d="M4 4h8v8H4zM4 8.5h3"/>');
s('ansicht3d',
  '<path class="fl" d="M16 3.5l11.5 6.5-11.5 6.5-11.5-6.5z"/><path class="f2" d="M4.5 10l11.5 6.5v12.5l-11.5-6.5z"/><path class="f3" d="M27.5 10v12.5l-11.5 6.5v-12.5z"/>',
  '<path class="fl" d="M8 1.5l6 3.5-6 3.5-6-3.5z"/><path class="f2" d="M2 5l6 3.5v6l-6-3.5z"/><path class="f3" d="M14 5v6l-6 3.5v-6z"/>');
s('liste', TAB32, TAB16);
s('schnitt',
  '<rect class="fp" x="3.5" y="5.5" width="25" height="21"/><path class="sb dd w15" d="M7 16h18"/><path class="fb" d="M3.5 12.5l4.5 3.5-4.5 3.5zM28.5 12.5l-4.5 3.5 4.5 3.5z"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="sb dd" d="M5 8h6"/><path class="fb" d="M1.5 5.8l3 2.2-3 2.2zM14.5 5.8l-3 2.2 3 2.2z"/>');
s('schema',
  '<rect class="f2" x="3.5" y="4.5" width="10" height="7"/><rect class="f2" x="18.5" y="4.5" width="10" height="7"/><rect class="fb" x="11" y="21.5" width="10" height="6"/><path class="svl w15" d="M13.5 8h5"/><path class="srl w15" d="M8.5 11.5v5h7.5M23.5 11.5v5h-7.5M16 16.5v5"/>',
  '<rect class="f2" x="1.5" y="1.5" width="5" height="4"/><rect class="f2" x="9.5" y="1.5" width="5" height="4"/><rect class="fb" x="5.5" y="10.5" width="5" height="4"/><path class="svl" d="M6.5 3.5h3"/><path class="srl" d="M4 5.5v2.5h4M12 5.5v2.5h-4M8 8v2.5"/>');
s('duplizieren',
  '<path class="f2" d="M11.5 3.5h11l5 5v15h-16z"/><path class="fl" d="M4.5 10.5h11l5 5v14h-16z"/><path class="f2" d="M15.5 10.5v5h5z"/>',
  '<path class="f2" d="M5.5 1.5h5l3 3v6h-8z"/><path class="fl" d="M2.5 5.5h5l3 3v6h-8z"/>');
s('sichtbarkeit',
  '<path class="fp" d="M2.5 16C6.5 9 11 6.5 16 6.5S25.5 9 29.5 16c-4 7-8.5 9.5-13.5 9.5S6.5 23 2.5 16z"/><circle class="fb" cx="16" cy="16" r="5.5"/><circle class="fk n" cx="16" cy="16" r="2.2"/>',
  '<path class="fp" d="M1.5 8C3.5 4.5 5.5 3.5 8 3.5S12.5 4.5 14.5 8c-2 3.5-4.5 4.5-6.5 4.5S3.5 11.5 1.5 8z"/><circle class="fb n" cx="8" cy="8" r="2.8"/><circle class="fk n" cx="8" cy="8" r="1.1"/>');
s('vorlageSpeichern',
  '<path class="fb" d="M4.5 4.5h20l3 3v20h-23z"/><path class="f1" d="M9.5 4.5h12v8h-12z"/><path class="f3" d="M17.5 6.5h2.5v4h-2.5z"/><path class="fp" d="M8.5 17.5h15v10h-15z"/><path class="op" d="M11 21h10M11 24h7"/>',
  '<path class="fb" d="M1.5 1.5h10l3 3v10h-13z"/><path class="f1" d="M4 1.5h6v4H4z"/><path class="fp" d="M3.5 9.5h9v5h-9z"/>');
s('vorlageAnwenden', `${DOC32}<path class="sb w3" d="M10.5 18l5 5 8.5-10"/>`, `${DOC16}<path class="sb w2" d="M5 9l2 2 3.5-4.5"/>`);
s('nebeneinander',
  '<rect class="fp" x="2.5" y="5.5" width="13" height="21"/><path class="fb" d="M2.5 5.5h13v4h-13z"/><rect class="fp" x="16.5" y="5.5" width="13" height="21"/><path class="fb" d="M16.5 5.5h13v4h-13z"/>',
  '<rect class="fp" x="1.5" y="2.5" width="6" height="11"/><path class="fb" d="M1.5 2.5h6v2.5h-6z"/><rect class="fp" x="8.5" y="2.5" width="6" height="11"/><path class="fb" d="M8.5 2.5h6v2.5h-6z"/>');
s('reiter',
  '<path class="fp" d="M3.5 11.5h25v16h-25z"/><path class="fb" d="M3.5 11.5v-5h9v5z"/><path class="f2" d="M13.5 11.5v-4h8v4z"/>',
  '<path class="fp" d="M1.5 6.5h13v8h-13z"/><path class="fb" d="M1.5 6.5v-3h5v3z"/><path class="f2" d="M7 6.5v-2.5h4v2.5z"/>');
s('eigenesFenster',
  '<rect class="f2" x="13.5" y="3.5" width="15" height="13"/><path class="fl" d="M13.5 3.5h15v3.5h-15z"/><rect class="fp" x="3.5" y="12.5" width="17" height="15"/><path class="fb" d="M3.5 12.5h17v4h-17z"/>',
  '<rect class="f2" x="7.5" y="1.5" width="7" height="6"/><rect class="fp" x="1.5" y="6.5" width="9" height="8"/><path class="fb" d="M1.5 6.5h9v2.5h-9z"/>');
s('zoom',
  '<circle class="fl" cx="13.5" cy="13.5" r="9"/><path class="f3" d="M19.5 21l2.5-2.5 7.5 7.5-2.5 2.5z"/><path class="sW op" d="M8.5 12a5.5 5.5 0 0 1 4-4"/>',
  '<circle class="fl" cx="6.5" cy="6.5" r="4.7"/><path class="f3" d="M10 11.5l1.5-1.5 3.5 3.5-1.5 1.5z"/>');
s('zoomAlle',
  '<rect class="fp" x="10.5" y="10.5" width="11" height="11"/><path class="sb w2" d="M3.5 10V3.5H10M22 3.5h6.5V10M28.5 22v6.5H22M10 28.5H3.5V22"/>',
  '<rect class="fp" x="5.5" y="5.5" width="5" height="5"/><path class="sb w15" d="M1.5 5V1.5H5M11 1.5h3.5V5M14.5 11v3.5H11M5 14.5H1.5V11"/>');
s('zoomAlleAnsichten',
  '<rect class="fp" x="6.5" y="6.5" width="8" height="8"/><rect class="fp" x="17.5" y="17.5" width="8" height="8"/><path class="sb w2" d="M3.5 10V3.5H10M22 3.5h6.5V10M28.5 22v6.5H22M10 28.5H3.5V22"/>',
  '<rect class="fp" x="4" y="4" width="3.5" height="3.5"/><rect class="fp" x="8.5" y="8.5" width="3.5" height="3.5"/><path class="sb w15" d="M1.5 5V1.5H5M11 1.5h3.5V5M14.5 11v3.5H11M5 14.5H1.5V11"/>');
s('info',
  '<circle class="fb n" cx="16" cy="16" r="12.5"/><path class="sW w3" d="M16 14.5v8"/><circle class="fW n" cx="16" cy="10" r="1.9"/>',
  '<circle class="fb n" cx="8" cy="8" r="6.5"/><path class="sW w2" d="M8 7v4.5"/><circle class="fW n" cx="8" cy="4.3" r="1.1"/>');
s('haus',
  '<path class="f1" d="M6.5 15.5v13h19v-13"/><path class="fb" d="M2.5 16.5L16 4.5l13.5 12z"/><rect class="fl" x="13" y="20" width="6" height="8.5"/>',
  '<path class="f1" d="M3 8v6.5h10V8"/><path class="fb" d="M1 8.5L8 2l7 6.5z"/><rect class="fl" x="6.5" y="10.5" width="3" height="4"/>');
s('synchronisieren',
  '<path class="sb w2" d="M7.4 12A9.5 9.5 0 0 1 24.2 11.25"/><path class="fb" d="M28.1 9L26.95 16 20.3 13.5z"/><path class="sb w2" d="M24.6 20A9.5 9.5 0 0 1 7.8 20.75"/><path class="fb" d="M3.9 23L5.05 16 11.7 18.5z"/>',
  '<path class="sb w15" d="M3.5 5.9A5 5 0 0 1 12.3 5.5"/><path class="fb n" d="M14.5 4.25L13.8 8.1 10.1 6.75z"/><path class="sb w15" d="M12.5 10.1A5 5 0 0 1 3.7 10.5"/><path class="fb n" d="M1.5 11.75L2.2 7.9 5.9 9.25z"/>');
s('freigeben',
  '<path class="w2" d="M10.5 14.5v-5a5.5 5.5 0 0 1 10.5-2.3"/><rect class="fy" x="6.5" y="14.5" width="19" height="13" rx="1.5"/><circle class="fk n" cx="16" cy="20" r="1.8"/><path class="w15" d="M16 21v3"/>',
  '<path d="M5 7.5V5a3 3 0 0 1 5.5-1.6"/><rect class="fy" x="2.5" y="7.5" width="11" height="7" rx="1"/><circle class="fk n" cx="8" cy="10.5" r="1"/>');
s('befunde',
  '<path class="fy" d="M16 3.5l13 23H3z"/><path class="w3" d="M16 11v8"/><circle class="fk n" cx="16" cy="23" r="1.7"/>',
  '<path class="fy" d="M8 1.5l7 12.5H1z"/><path class="w2" d="M8 6v4"/><circle class="fk n" cx="8" cy="12" r=".9"/>');
s('katalog',
  '<rect class="fb" x="6.5" y="3.5" width="20" height="25" rx="1.5"/><path d="M10.5 3.5v25"/><rect class="fp" x="13.5" y="8.5" width="10" height="7"/><path class="op" d="M13.5 20h10M13.5 23.5h7"/>',
  '<rect class="fb" x="3.5" y="1.5" width="10" height="13" rx="1"/><path d="M5.5 1.5v13"/><rect class="fp" x="7" y="4" width="5" height="4"/>');
s('tastatur',
  '<rect class="f2" x="2.5" y="8.5" width="27" height="16" rx="2"/><path class="f1" d="M5.5 11.5h3v3h-3zM10.5 11.5h3v3h-3zM15.5 11.5h3v3h-3zM20.5 11.5h3v3h-3zM25.5 11.5h1.5v3h-1.5zM7.5 16h3v3h-3zM12.5 16h3v3h-3zM17.5 16h3v3h-3zM22.5 16h4v3h-4zM9.5 20.5h13v2h-13z"/>',
  '<rect class="f2" x="1.5" y="4.5" width="13" height="8" rx="1"/><path class="f1 n" d="M3 6h2v2H3zM6 6h2v2H6zM9 6h2v2H9zM12 6h1v2h-1zM4.5 9h2v1.5h-2zM7.5 9h2v1.5h-2zM10.5 9h2v1.5h-2z"/>');
s('netzAb',
  '<circle class="fp" cx="6" cy="16" r="3.5"/><path class="fvl" d="M9.5 13.5h11v5h-11z"/><path class="fb" d="M20.5 9.5l8 6.5-8 6.5z"/>',
  '<circle class="fp" cx="3" cy="8" r="2"/><path class="fvl" d="M5 6.5h5v3H5z"/><path class="fb" d="M10 4.5l5 3.5-5 3.5z"/>');
s('netzAuf',
  '<circle class="fp" cx="26" cy="16" r="3.5"/><path class="fvl" d="M22.5 13.5h-11v5h11z"/><path class="fb" d="M11.5 9.5l-8 6.5 8 6.5z"/>',
  '<circle class="fp" cx="13" cy="8" r="2"/><path class="fvl" d="M11 6.5H6v3h5z"/><path class="fb" d="M6 4.5L1 8l5 3.5z"/>');
const FRAGE32 = '<circle class="fb n" cx="16" cy="16" r="12.5"/><path class="sW w2" d="M12.3 12.8a3.7 3.5 0 1 1 6.2 2.5c-1.3 1.1-2.5 1.6-2.5 3.7"/><circle class="fW n" cx="16" cy="22.8" r="1.5"/>';
const FRAGE16 = '<circle class="fb n" cx="8" cy="8" r="6.5"/><path class="sW w15" d="M5.9 6.5a2.1 2 0 1 1 3.6 1.4c-.8.7-1.5.9-1.5 2"/><circle class="fW n" cx="8" cy="11.6" r=".9"/>';
s('warum', FRAGE32, FRAGE16);
s('hilfe', FRAGE32, FRAGE16);
s('anleitung',
  '<path class="fb n" d="M16 3.5c7 0 12.5 4.4 12.5 10S23 23.5 16 23.5h-.5L9 28.5V22.3C6 20.2 3.5 17.2 3.5 13.5S9 3.5 16 3.5z"/><path class="sW w2" d="M12.7 11.3a3.4 3.1 0 1 1 5.7 2.2c-1.2 1-2.4 1.4-2.4 3"/><circle class="fW n" cx="16" cy="19.6" r="1.3"/>',
  '<path class="fb n" d="M8 1.5c3.6 0 6.5 2.3 6.5 5.2S11.6 11.9 8 11.9h-.3L4.5 14.5v-3.3C2.8 10.2 1.5 8.6 1.5 6.7 1.5 3.8 4.4 1.5 8 1.5z"/><path class="sW w15" d="M6.2 5.5a1.9 1.7 0 1 1 3.2 1.2c-.6.5-1.4.7-1.4 1.6"/><circle class="fW n" cx="8" cy="9.8" r=".8"/>');
s('planblatt',
  '<rect class="fp" x="3.5" y="3.5" width="25" height="25"/><rect class="fl" x="6.5" y="6.5" width="19" height="13"/><path class="f2" d="M3.5 22.5h25v6h-25z"/><path class="fb" d="M18.5 22.5h10v6h-10z"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><rect class="fl" x="3" y="3" width="10" height="6"/><path class="f2" d="M1.5 10.5h13v4h-13z"/><path class="fb" d="M9.5 10.5h5v4h-5z"/>');
s('serienpdf',
  '<path class="f2" d="M12.5 2.5h11l5 5v17h-16z"/><path class="f1" d="M9.5 5.5h11l5 5v17h-16z"/><path class="fp" d="M6.5 8.5h11l5 5v17h-16z"/><path class="fr n" d="M6.5 21.5h16v6h-16z"/><path class="sW w15" d="M9 24.5h3M14 24.5h3M19 24.5h2"/>',
  '<path class="f2" d="M6.5 1.5h5l3 3v8h-8z"/><path class="fp" d="M3.5 3.5h5l3 3v9h-8z"/><path class="fr n" d="M3.5 10.5h8v4h-8z"/><path class="sW" d="M5 12.5h1.5M7.5 12.5h1.5"/>');
s('gebaeude',
  '<path class="f1" d="M16 2.5l10 5-10 5-10-5z"/><path class="f2" d="M6 7.5l10 5v16l-10-5z"/><path class="f3" d="M26 7.5v16l-10 5v-16z"/><path class="fl" d="M8.5 13l3 1.5v3L8.5 16zM8.5 19l3 1.5v3L8.5 22zM12.5 15l2.5 1.2v3L12.5 18zM12.5 21l2.5 1.2v3L12.5 24zM17.5 17v3l3-1.5v-3zM17.5 23v3l3-1.5v-3zM22 14.5v3l2.5-1.2v-3zM22 20.5v3l2.5-1.2v-3z"/>',
  '<path class="f1" d="M8 1.5l5 2.5-5 2.5L3 4z"/><path class="f2" d="M3 4l5 2.5v8L3 12z"/><path class="f3" d="M13 4v8l-5 2.5v-8z"/><path class="fl n" d="M4.5 7.5l2 1v1.5l-2-1zM4.5 10.5l2 1v1.3l-2-1zM9.5 8.5l2-1v1.5l-2 1zM9.5 11.5l2-1v1.4l-2 1z"/>');
s('geschoss',
  '<path class="f2" d="M3.5 19l12.5 6.5v3l-12.5-6.5z"/><path class="f3" d="M28.5 19v3l-12.5 6.5v-3z"/><path class="f2" d="M3.5 10l12.5 6.5v3l-12.5-6.5z"/><path class="f3" d="M28.5 10v3l-12.5 6.5v-3z"/><path class="fl" d="M16 3.5l12.5 6.5-12.5 6.5-12.5-6.5z"/>',
  '<path class="f2" d="M1.5 10l6.5 3.5v2L1.5 12z"/><path class="f3" d="M14.5 10v2L8 15.5v-2z"/><path class="f2" d="M1.5 5l6.5 3.5v2L1.5 7z"/><path class="f3" d="M14.5 5v2L8 10.5v-2z"/><path class="fl" d="M8 1.5l6.5 3.5-6.5 3.5-6.5-3.5z"/>');
s('anlage',
  '<rect class="f2" x="3.5" y="9.5" width="11" height="17" rx="1.5"/><path class="fo n" d="M9 24.5c-2.5 0-3.5-2-3-4 .8 1 1.5 1 2 .8C7.5 19 8.5 17.5 10 16.5c0 2 2 3 2 5.3 0 1.6-1 2.7-3 2.7z"/><path class="svl w2" d="M14.5 13h3V8h3M17.5 13v10h3"/><rect class="f1" x="20.5" y="4.5" width="8" height="7"/><rect class="f1" x="20.5" y="19.5" width="8" height="7"/><path class="fl n" d="M22.5 6.5h1.5v3h-1.5zM25 6.5h1.5v3H25zM22.5 21.5h1.5v3h-1.5zM25 21.5h1.5v3H25z"/>',
  '<rect class="f2" x="1.5" y="4.5" width="5" height="9" rx="1"/><path class="fo n" d="M4 12.5c-1.2 0-1.7-1-1.5-2 .4.5.7.5 1 .4-.1-.9.4-1.6 1.1-2.1 0 1 1 1.5 1 2.6 0 .7-.5 1.1-1.6 1.1z"/><path class="svl" d="M6.5 6.5H9V3.5h1.5M9 6.5v5h1.5"/><rect class="f1" x="10.5" y="1.5" width="4" height="3.5"/><rect class="f1" x="10.5" y="10.5" width="4" height="3.5"/>');
s('system',
  '<path class="svl w2" d="M13 16h5l3.5-6.5M18 16l3.5 6.5"/><circle class="fb" cx="9" cy="16" r="4.5"/><circle class="f1" cx="24" cy="8" r="3.5"/><circle class="f1" cx="24" cy="24" r="3.5"/>',
  '<path class="svl w15" d="M6 8h2.5l2-3.2M8.5 8l2 3.2"/><circle class="fb" cx="3.5" cy="8" r="2.5"/><circle class="f1" cx="12" cy="3.5" r="2"/><circle class="f1" cx="12" cy="12.5" r="2"/>');
s('strang',
  '<path class="fvl" d="M5.5 12.5h21v7h-21z"/><path class="sW op" d="M7 14.5h18"/><path class="sb w2" d="M3.5 8v16M28.5 8v16"/>',
  '<path class="fvl" d="M2.5 6h11v4h-11z"/><path class="sb w15" d="M1 4v8M15 4v8"/>');
s('familie', QUADER32.replace('class="f1"', 'class="fb"'), QUADER16.replace('class="f1"', 'class="fb"'));
s('vorlage',
  '<rect class="fp sb dd w15" x="5.5" y="3.5" width="21" height="25"/><path class="op" d="M9.5 10h13M9.5 15h13M9.5 20h8"/>',
  '<rect class="fp sb dd" x="2.5" y="1.5" width="11" height="13"/><path class="op" d="M4.5 5h7M4.5 8h7M4.5 11h4"/>');
s('suche',
  '<circle class="fp sb w2" cx="13.5" cy="13.5" r="8.5"/><path class="f3" d="M19.5 21l2.5-2.5 7.5 7.5-2.5 2.5z"/>',
  '<circle class="fp sb w15" cx="6.5" cy="6.5" r="4.5"/><path class="f3" d="M10 11.5l1.5-1.5 3.5 3.5-1.5 1.5z"/>');
s('umbenennen',
  '<path class="fy" d="M7 25l1-5 12-12 4 4-12 12z"/><path class="f3" d="M7 25l1-5 4 4z"/><path class="fr" d="M20.5 8.5l2-2a2.2 2.2 0 0 1 3.2 0l.8.8a2.2 2.2 0 0 1 0 3.2l-2 2z"/><path d="M4.5 29h14"/>',
  '<path class="fy" d="M2.5 12.5l.6-3 6.5-6.5 2.4 2.4-6.5 6.5z"/><path class="fr" d="M10 2.5l.8-.8a1.2 1.2 0 0 1 1.7 0l.5.5a1.2 1.2 0 0 1 0 1.7l-.8.8z"/><path d="M1.5 14.5h7"/>');
s('oeffnen',
  '<path class="fy" d="M3.5 6.5h8.5l3 3h13.5v17h-25z"/><path class="fo" d="M3.5 13.5h25v13h-25z"/><path class="fb" d="M10 19h8v-4l7 7-7 7v-4h-8z"/>',
  '<path class="fy" d="M1.5 3.5h4.5l1.8 1.8h6.7v8.2h-13z"/><path class="fo" d="M1.5 7.5h13v6h-13z"/><path class="fb" d="M4 9.5h4V8l4 3.2L8 14v-1.5H4z"/>');
s('design',
  '<circle class="fy" cx="16" cy="16" r="11.5"/><path class="fb" d="M16 4.5a11.5 11.5 0 0 0 0 23z"/>',
  '<circle class="fy" cx="8" cy="8" r="6.5"/><path class="fb" d="M8 1.5a6.5 6.5 0 0 0 0 13z"/>');
s('huelleAnsicht',
  '<rect class="fp" x="3.5" y="3.5" width="25" height="25"/><path class="fo eo" d="M6.5 6.5h19v19h-19zM10.5 10.5v11h11v-11z"/><path d="M16 3.5v3M16 25.5v3"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><path class="fo eo" d="M3 3h10v10H3zM5.5 5.5v5h5v-5z"/>');
s('hkapVerbinden',
  '<path class="sb w2" d="M12.5 10h11v7.5"/><rect class="f2" x="3.5" y="5.5" width="9" height="9"/><rect class="f2" x="19.5" y="17.5" width="9" height="9"/><circle class="fb n" cx="12.5" cy="10" r="2"/><circle class="fb n" cx="24" cy="17.5" r="2"/>',
  '<path class="sb w15" d="M6.5 5h6v4.5"/><rect class="f2" x="1.5" y="2.5" width="5" height="5"/><rect class="f2" x="9.5" y="9.5" width="5" height="5"/>');
s('hkapAbgleich',
  '<rect class="fp" x="2.5" y="4.5" width="9" height="23"/><path class="f3" d="M2.5 4.5h9v4h-9z"/><path class="op" d="M4.5 13h5M4.5 17h5M4.5 21h5"/><rect class="fp" x="20.5" y="4.5" width="9" height="23"/><path class="f3" d="M20.5 4.5h9v4h-9z"/><path class="op" d="M22.5 13h5M22.5 17h5M22.5 21h5"/><path class="sb w2" d="M13 12h4M19 21h-4"/><path class="fb n" d="M16.5 9.2l3.5 2.8-3.5 2.8zM15.5 18.2L12 21l3.5 2.8z"/>',
  '<rect class="fp" x="1.5" y="2.5" width="4.5" height="11"/><path class="f3" d="M1.5 2.5h4.5v2.5H1.5z"/><rect class="fp" x="10" y="2.5" width="4.5" height="11"/><path class="f3" d="M10 2.5h4.5v2.5H10z"/><path class="sb" d="M6.8 6.5h1.6M9.2 10.5H7.6"/><path class="fb n" d="M8 5l2 1.5L8 8zM8 9L6 10.5 8 12z"/>');
s('hkapBefunde',
  '<path class="fy" d="M12 3.5l10 18H2z"/><path class="w2" d="M12 10v6"/><circle class="fk n" cx="12" cy="19" r="1.3"/><path class="fb" d="M19.5 21.5h6v-3.5l5.5 6-5.5 6v-3.5h-6z"/>',
  '<path class="fy" d="M6.5 1.5l5.5 10H1z"/><path d="M6.5 5.5v3"/><circle class="fk n" cx="6.5" cy="10.2" r=".7"/><path class="fb" d="M9.5 11h3V9l3 3.5-3 3.5v-2h-3z"/>');

// ---------------------------------------------------------------------------------------------- Hülle

s('decke',
  '<path class="f2" d="M5.5 15.5h3v12h-3zM23.5 15.5h3v12h-3zM14.5 21h3v8h-3z"/><path class="f1" d="M3.5 11l12.5-6.5 12.5 6.5-12.5 6.5z"/><path class="f2" d="M3.5 11l12.5 6.5v3.5l-12.5-6.5z"/><path class="f3" d="M28.5 11v3.5l-12.5 6.5v-3.5z"/>',
  '<path class="f2" d="M2.5 8h2v6.5h-2zM11.5 8h2v6.5h-2z"/><path class="f1" d="M1.5 5.5L8 2l6.5 3.5L8 9z"/><path class="f2" d="M1.5 5.5L8 9v2L1.5 7.5z"/><path class="f3" d="M14.5 5.5v2L8 11V9z"/>');
s('boden',
  '<path class="f1" d="M3.5 19l12.5-6.5 12.5 6.5-12.5 6.5z"/><path class="f2" d="M3.5 19l12.5 6.5v3.5l-12.5-6.5z"/><path class="f3" d="M28.5 19v3.5l-12.5 6.5v-3.5z"/><path class="f2" d="M6 7h3.5v12H6zM22.5 7H26v12h-3.5z"/><path class="f1" d="M6 7h3.5v1.5H6zM22.5 7H26v1.5h-3.5z"/>',
  '<path class="f1" d="M1.5 10.5L8 7l6.5 3.5L8 14z"/><path class="f2" d="M1.5 10.5L8 14v1.5L1.5 12z"/><path class="f3" d="M14.5 10.5V12L8 15.5V14z"/><path class="f2" d="M2.5 2.5h2v7h-2zM11.5 2.5h2v7h-2z"/>');
s('dach',
  '<path class="f2" d="M5.5 18.5h3v10h-3zM23.5 18.5h3v10h-3z"/><path class="f3" d="M2.5 17.5l13.5-11 13.5 11v3.5l-13.5-11-13.5 11z"/><path class="sb w15" d="M10 6.5l-3 3M22 6.5l3 3"/>',
  '<path class="f2" d="M2.5 9h2v5.5h-2zM11.5 9h2v5.5h-2z"/><path class="f3" d="M1.5 8.5L8 3l6.5 5.5v2L8 5 1.5 10.5z"/>');
s('aufbau',
  '<path class="fp" d="M4.5 4.5h4v23h-4z"/><path class="fy" d="M8.5 4.5h7v23h-7z"/><path class="f3" d="M15.5 4.5h8v23h-8z"/><path class="fo" d="M23.5 4.5h4v23h-4z"/><path class="op" d="M9.5 8l5 3-5 3 5 3-5 3 5 3-5 3M15.5 24l8-8M15.5 16l8-8"/>',
  '<path class="fp" d="M2.5 2.5h2.5v11H2.5z"/><path class="fy" d="M5 2.5h4v11H5z"/><path class="f3" d="M9 2.5h3v11H9z"/><path class="fo" d="M12 2.5h2v11h-2z"/>');
s('zuweisen',
  '<rect class="fo" x="2.5" y="12.5" width="6" height="7"/><path class="sb w2" d="M9.5 16h6"/><path class="fb" d="M14 12l5.5 4-5.5 4z"/><rect class="f2" x="21.5" y="6.5" width="8" height="19"/><path class="op" d="M21.5 14l8-5M21.5 22l8-8"/>',
  '<rect class="fo" x="1.5" y="5.5" width="3" height="5"/><path class="sb" d="M5 8h2.5"/><path class="fb" d="M7 6l3 2-3 2z"/><rect class="f2" x="11.5" y="2.5" width="3" height="11"/>');
s('material',
  '<rect class="fp" x="3.5" y="5.5" width="25" height="21"/><path class="fo" d="M4.5 6.5h8v5h-8zM13.5 6.5h8v5h-8zM22.5 6.5h5v5h-5zM4.5 12.5h4v5h-4zM9.5 12.5h8v5h-8zM18.5 12.5h8v5h-8zM4.5 18.5h8v5h-8zM13.5 18.5h8v5h-8zM22.5 18.5h5v5h-5z"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fo n" d="M2.5 3.5h5v2.5h-5zM8.5 3.5h5v2.5h-5zM2.5 7h2.5v2.5H2.5zM6 7h5v2.5H6zM12 7h1.5v2.5H12zM2.5 10.5h5v2.5h-5zM8.5 10.5h5v2.5h-5z"/>');
s('fensterKennwerte',
  '<rect class="f2" x="3.5" y="4.5" width="15" height="20"/><path class="fl" d="M5.5 6.5h5v7h-5zM11.5 6.5h5v7h-5zM5.5 14.5h5v8h-5zM11.5 14.5h5v8h-5z"/><path class="sb w2" d="M21.5 9.5h7M21.5 15h7M21.5 20.5h7"/>',
  '<rect class="f2" x="1.5" y="2.5" width="7" height="10"/><path class="fl n" d="M2.5 3.5h2.5v3.5H2.5zM5.5 3.5H8v3.5H5.5zM2.5 7.5h2.5v4H2.5zM5.5 7.5H8v4H5.5z"/><path class="sb" d="M10.5 5h4M10.5 8h4M10.5 11h4"/>');
s('uebergang',
  '<path class="f3" d="M3.5 4.5h4v23h-4zM24.5 4.5h4v23h-4z"/><path class="svl w2" d="M9.5 11h8"/><path class="fvl n" d="M17 7.5l5 3.5-5 3.5z"/><path class="srl w2" d="M22.5 21h-8"/><path class="frl n" d="M15 17.5l-5 3.5 5 3.5z"/>',
  '<path class="f3" d="M1.5 1.5h2v13h-2zM12.5 1.5h2v13h-2z"/><path class="svl w15" d="M4.5 5h3"/><path class="fvl n" d="M7.5 3l2.5 2-2.5 2z"/><path class="srl w15" d="M11.5 11h-3"/><path class="frl n" d="M8.5 9L6 11l2.5 2z"/>');
s('huellePruefen',
  '<path class="f3" d="M2.5 15L14 4.5 25.5 15z"/><path class="f1" d="M5.5 15v12.5h17V15"/><rect class="fl" x="9" y="19" width="5" height="5"/>' + HAKEN32,
  '<path class="f3" d="M1 8L7 2.5 13 8z"/><path class="f1" d="M2.5 8v6.5h9V8"/>' + HAKEN16);
s('waermebruecke',
  '<path class="f3" d="M3.5 5.5h9v21h-9zM19.5 5.5h9v21h-9z"/><path class="fvl" d="M12.5 13.5h7v5h-7z"/><path class="sW op" d="M13.5 16h5"/>',
  '<path class="f3" d="M1.5 2.5h4v11h-4zM10.5 2.5h4v11h-4z"/><path class="fvl" d="M5.5 6.5h5v3h-5z"/>');
s('raumgrenzen',
  '<rect class="fl n" x="8.5" y="8.5" width="15" height="15"/><path class="f3 eo" d="M5.5 5.5h21v21h-21zM8.5 8.5v15h15v-15z"/><rect class="so w2 dd" x="2" y="2" width="28" height="28"/>',
  '<rect class="fl n" x="5" y="5" width="6" height="6"/><path class="f3 eo" d="M3 3h10v10H3zM5 5v6h6V5z"/><rect class="so dd" x="1" y="1" width="14" height="14"/>');
s('huellbauteile',
  '<path class="f1" d="M6.5 28.5V10.5l9.5-6 9.5 6v18z"/><rect class="fl" x="12" y="14" width="8" height="8"/><path class="so w2" d="M4.5 28.5V9.5L16 2.5l11.5 7v19"/><path d="M2.5 29.5h27"/>',
  '<path class="f1" d="M3.5 14.5V5.5L8 2.5l4.5 3v9z"/><rect class="fl n" x="6" y="7.5" width="4" height="4"/><path class="so" d="M2.5 14.5V5L8 1.5 13.5 5v9.5"/>');
s('raumOffen',
  '<rect class="fl dd" x="4.5" y="4.5" width="23" height="23"/><path class="w2" d="M12.7 13a3.5 3.3 0 1 1 6 2.2c-1.2 1-2.2 1.5-2.2 3.3"/><circle class="fk n" cx="16.5" cy="22.8" r="1.4"/>',
  '<rect class="fl dd" x="1.5" y="1.5" width="13" height="13"/><path class="w15" d="M5.9 6.5a2.1 2 0 1 1 3.6 1.3c-.7.6-1.5.9-1.5 2"/><circle class="fk n" cx="8" cy="11.7" r=".8"/>');

// ---------------------------------------------------------------------------------------------- Familien (Modul 2)

const TABK32 = '<rect class="fp" x="3.5" y="4.5" width="21" height="18"/><path class="fb" d="M3.5 4.5h21v5h-21z"/><path d="M3.5 14h21M10.5 9.5v13"/>';
const TABK16 = '<rect class="fp" x="1.5" y="1.5" width="10" height="9"/><path class="fb" d="M1.5 1.5h10v2.5h-10z"/><path d="M1.5 7h10M5.5 4v6.5"/>';
const TYP32 = '<rect class="fp" x="3.5" y="4.5" width="21" height="18"/><path class="fb" d="M3.5 4.5h21v4h-21z"/><path class="f2" d="M3.5 8.5h7v14h-7z"/><path d="M3.5 13h21M3.5 17.5h21M10.5 8.5v14"/>';
const TYP16 = '<rect class="fp" x="1.5" y="1.5" width="10" height="9"/><path class="fb" d="M1.5 1.5h10v2.5h-10z"/><path class="f2" d="M1.5 4h4v6.5h-4z"/><path d="M1.5 7h10M5.5 4v6.5"/>';
s('famParameterNeu', TABK32 + PLUS32, TABK16 + '<circle class="fb n" cx="11.5" cy="11.5" r="4"/><path class="sW w15" d="M11.5 9.5v4M9.5 11.5h4"/>');
s('famParameter',
  '<rect class="fp" x="3.5" y="5.5" width="25" height="21"/><path class="fb" d="M3.5 5.5h25v5h-25z"/><path class="f2" d="M3.5 10.5h9v16h-9z"/><path d="M3.5 15.5h25M3.5 20.5h25M12.5 10.5v16"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fb" d="M1.5 2.5h13v3h-13z"/><path class="f2" d="M1.5 5.5h5v8h-5z"/><path d="M1.5 8.5h13M1.5 11h13M6.5 5.5v8"/>');
s('famTypNeu', TYP32 + PLUS32, TYP16 + '<circle class="fb n" cx="11.5" cy="11.5" r="4"/><path class="sW w15" d="M11.5 9.5v4M9.5 11.5h4"/>');
s('famTypKopie',
  '<rect class="f2" x="11.5" y="3.5" width="17" height="15"/><rect class="fp" x="3.5" y="10.5" width="21" height="17"/><path class="fb" d="M3.5 10.5h21v4h-21z"/><path class="f2" d="M3.5 14.5h7v13h-7z"/><path d="M3.5 19h21M3.5 23h21M10.5 14.5v13"/>',
  '<rect class="f2" x="6.5" y="1.5" width="8" height="7"/><rect class="fp" x="1.5" y="5.5" width="10" height="9"/><path class="fb" d="M1.5 5.5h10v2.5h-10z"/><path d="M1.5 10h10M5.5 8v6.5"/>');
s('famTypWeg', TYP32 + KREUZ32, TYP16 + '<circle class="fr n" cx="11.5" cy="11.5" r="4"/><path class="sW w15" d="M9.8 9.8l3.4 3.4M13.2 9.8l-3.4 3.4"/>');
s('famTypen',
  '<rect class="fp" x="3.5" y="5.5" width="25" height="21"/><path class="fb" d="M3.5 5.5h25v4h-25z"/><path class="f2" d="M3.5 9.5h8v17h-8z"/><path d="M3.5 14.5h25M3.5 19.5h25M3.5 23h25M11.5 9.5v17M19.5 9.5v17"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fb" d="M1.5 2.5h13v2.5h-13z"/><path class="f2" d="M1.5 5h4.5v8.5H1.5z"/><path d="M1.5 8h13M1.5 10.7h13M6 5v8.5M10 5v8.5"/>');
s('famKoerper', QUADER32, QUADER16);
s('famKoerperWeg', QUADER32 + KREUZ32, QUADER16 + KREUZ16);
s('famQuader',
  '<path class="f1" d="M3.5 12.5l7-7h17l-7 7z"/><path class="f2" d="M3.5 12.5h17v14h-17z"/><path class="f3" d="M20.5 12.5l7-7v14l-7 7z"/>',
  '<path class="f1" d="M1.5 7.5l3-3h9l-3 3z"/><path class="f2" d="M1.5 7.5h9v7h-9z"/><path class="f3" d="M10.5 7.5l3-3v7l-3 3z"/>');
s('famZylinder',
  '<path class="f2" d="M7.5 8.5v15a8.5 3.5 0 0 0 17 0v-15z"/><ellipse class="f1" cx="16" cy="8.5" rx="8.5" ry="3.5"/><path class="sW op" d="M11 14v11"/>',
  '<path class="f2" d="M3.5 4.5v7a4.5 2 0 0 0 9 0v-7z"/><ellipse class="f1" cx="8" cy="4.5" rx="4.5" ry="2"/>');
s('famKegel',
  '<path class="f2" d="M16 3.5l10 20a10 4 0 0 1-20 0z"/><path class="f1 n" d="M16 5l-3 6.5 3 1.5z"/>',
  '<path class="f2" d="M8 1.5l6 11.5a6 2.5 0 0 1-12 0z"/><path class="f1 n" d="M8 3l-2 4.5 2 .8z"/>');
s('famKugel',
  '<circle class="f2" cx="16" cy="16" r="12"/><circle class="f1 n" cx="12.5" cy="12.5" r="5"/>',
  '<circle class="f2" cx="8" cy="8" r="6.5"/><circle class="f1 n" cx="6" cy="6" r="2.4"/>');
s('famExtrusion',
  '<path class="f1" d="M4.5 16.5h6l5-5h-6z"/><path class="f1" d="M10.5 10.5h8l5-5h-8z"/><path class="f3" d="M18.5 10.5l5-5v16l-5 5z"/><path class="fl" d="M4.5 26.5v-10h6v-6h8v16z"/>',
  '<path class="f1" d="M5 5.5l2-2h5l-2 2z"/><path class="f3" d="M10 5.5l2-2v8l-2 2z"/><path class="fl" d="M2 13V8h3V5.5h5V13z"/>');
s('famRotation',
  '<path class="f2" d="M9 7C9 12 12 14 12 17S9 22 9 27a7 2.5 0 0 0 14 0C23 22 20 20 20 17S23 12 23 7a7 2.5 0 0 1-14 0z"/><ellipse class="f1" cx="16" cy="7" rx="7" ry="2.5"/><path class="sb dp w15" d="M16 1.5v28"/><path class="sb w15" d="M26 15a4 2 0 0 1 3.5 2.5"/><path class="fb n" d="M31 16l-3.5 3-.5-4z"/>',
  '<path class="f2" d="M4 3C4 6 6 7 6 8.5S4 11 4 13a4 1.4 0 0 0 8 0C12 11 10 10 10 8.5S12 6 12 3a4 1.4 0 0 1-8 0z"/><ellipse class="f1" cx="8" cy="3" rx="4" ry="1.4"/><path class="sb dp" d="M8 .5v15"/>');
s('famSweep',
  '<path class="w6 sk" d="M7 26h10a7 7 0 0 0 7-7V8"/><path class="w4 s2" d="M7 26h10a7 7 0 0 0 7-7V8"/><ellipse class="fl" cx="6" cy="26" rx="2.6" ry="4.4"/><path class="sb dd w15" d="M7 26h10a7 7 0 0 0 7-7V8"/>',
  '<path class="w4 sk" d="M3.5 12.5h5a3 3 0 0 0 3-3V3"/><path class="w2 s2" d="M3.5 12.5h5a3 3 0 0 0 3-3V3"/><path class="sb dd" d="M3.5 12.5h5a3 3 0 0 0 3-3V3"/>');
s('famAnschluss',
  '<rect class="f2" x="9.5" y="3.5" width="13" height="11"/><path class="f3" d="M13.5 14.5h5v6h-5z"/><path class="fb" d="M12 20.5h8L16 28z"/>',
  '<rect class="f2" x="4.5" y="1.5" width="7" height="5"/><path class="f3" d="M6.5 6.5h3v3h-3z"/><path class="fb" d="M5.5 9.5h5L8 14.5z"/>');
s('famAnschluesse',
  '<rect class="f2" x="3.5" y="4.5" width="11" height="8"/><rect class="f2" x="17.5" y="4.5" width="11" height="8"/><path class="f3" d="M7 12.5h4v5H7zM21 12.5h4v5h-4z"/><path class="fb" d="M5.5 17.5h8L9.5 24zM19.5 17.5h8L23.5 24z"/>',
  '<rect class="f2" x="1.5" y="1.5" width="5" height="4"/><rect class="f2" x="9.5" y="1.5" width="5" height="4"/><path class="fb" d="M2 8h4l-2 5zM10 8h4l-2 5z"/>');
s('famSinnbild',
  '<rect class="fp dd" x="4.5" y="7.5" width="23" height="17"/><circle class="fl" cx="12" cy="16" r="4"/><path class="sb w15" d="M18.5 11l6 10M18.5 21l6-10"/>',
  '<rect class="fp dd" x="1.5" y="3.5" width="13" height="9"/><circle class="fl" cx="5.5" cy="8" r="2"/><path class="sb" d="M9 5.5l3.5 5M9 10.5l3.5-5"/>');
s('famVier',
  '<rect class="fp" x="3.5" y="3.5" width="11" height="11"/><rect class="fp" x="17.5" y="3.5" width="11" height="11"/><rect class="fp" x="3.5" y="17.5" width="11" height="11"/><rect class="fl" x="17.5" y="17.5" width="11" height="11"/><path class="fb" d="M3.5 3.5h11v3h-11zM17.5 3.5h11v3h-11zM3.5 17.5h11v3h-11zM17.5 17.5h11v3h-11z"/>',
  '<rect class="fp" x="1.5" y="1.5" width="5.5" height="5.5"/><rect class="fp" x="9" y="1.5" width="5.5" height="5.5"/><rect class="fp" x="1.5" y="9" width="5.5" height="5.5"/><rect class="fl" x="9" y="9" width="5.5" height="5.5"/>');
// Ansichten: Würfel in der Schrägsicht; sichtbare Fläche voll blau, abgewandte Fläche hellblau gestrichelt (Vorne/Rechts/Oben sichtbar; Hinten/Links/Unten dahinter)
const WUERFEL = (obenK, linksK, rechtsK, o, l, r) => `<path class="${obenK}" d="M16 3.5l11.5 6.5-11.5 6.5-11.5-6.5z"/><path class="${linksK}" d="M4.5 10l11.5 6.5v12.5l-11.5-6.5z"/><path class="${rechtsK}" d="M27.5 10v12.5l-11.5 6.5v-12.5z"/>`;
const W16 = (obenK, linksK, rechtsK) => `<path class="${obenK}" d="M8 1.5l6 3.5-6 3.5-6-3.5z"/><path class="${linksK}" d="M2 5l6 3.5v6l-6-3.5z"/><path class="${rechtsK}" d="M14 5v6l-6 3.5v-6z"/>`;
s('famVorne', WUERFEL('f1', 'fb', 'f3'), W16('f1', 'fb', 'f3'));
s('famHinten', WUERFEL('f1', 'fl dd', 'f3'), W16('f1', 'fl dd', 'f3'));
s('famRechts', WUERFEL('f1', 'f2', 'fb'), W16('f1', 'f2', 'fb'));
s('famLinks', WUERFEL('f1', 'f2', 'fl dd'), W16('f1', 'f2', 'fl dd'));
s('famOben', WUERFEL('fb', 'f2', 'f3'), W16('fb', 'f2', 'f3'));
s('famUnten', WUERFEL('fl dd', 'f2', 'f3'), W16('fl dd', 'f2', 'f3'));
s('famGrob',
  '<rect class="fl" x="6.5" y="6.5" width="19" height="19"/>',
  '<rect class="fl" x="3.5" y="3.5" width="9" height="9"/>');
s('famMittel',
  '<rect class="fp" x="6.5" y="6.5" width="19" height="19"/><path class="fl" d="M6.5 16h19v9.5h-19z"/>',
  '<rect class="fp" x="3.5" y="3.5" width="9" height="9"/><path class="fl" d="M3.5 8h9v4.5h-9z"/>');
s('famFein',
  '<rect class="fp" x="6.5" y="6.5" width="19" height="19"/><path d="M6.5 12.5h19M6.5 19h19M13 6.5v19M19.5 6.5v19"/><path class="fl" d="M6.5 6.5h6.5v6H6.5zM19.5 19h6v6.5h-6z"/>',
  '<rect class="fp" x="3.5" y="3.5" width="9" height="9"/><path d="M3.5 6.5h9M3.5 9.5h9M6.5 3.5v9M9.5 3.5v9"/><path class="fl" d="M3.5 3.5h3v3h-3zM9.5 9.5h3v3h-3z"/>');
s('famPruefen', DOC32 + '<path class="ssa w3" d="M10.5 18l5 5 8.5-10"/>', DOC16 + '<path class="ssa w2" d="M5 9l2 2 3.5-4.5"/>');
s('famPlatzieren',
  '<path class="fb" d="M13 3.5h6v6h3.5L16 16l-6.5-6.5H13z"/><path class="f2" d="M4.5 20.5h16v7h-16z"/><path class="f1" d="M4.5 20.5l5-4h16l-5 4z"/><path class="f3" d="M20.5 20.5l5-4v7l-5 4z"/>',
  '<path class="fb" d="M6 1.5h4v4h2.5L8 10 3.5 5.5H6z"/><path class="f2" d="M2.5 11h7v3.5h-7z"/><path class="f1" d="M2.5 11l2.5-2.5h7L9.5 11z"/><path class="f3" d="M9.5 11l2.5-2.5v3.5l-2.5 2.5z"/>');
s('famEditor',
  DOC32 + '<path class="fy" d="M15 27l1-4 9-9 3 3-9 9z"/><path class="f3" d="M15 27l1-4 3 3z"/>',
  DOC16 + '<path class="fy" d="M7 14l.5-2 5-5 1.5 1.5-5 5z"/>');
s('famUnterlagen',
  DOC32 + '<path class="fb" d="M16.5 12l6 6.5H19V26h-5v-7.5h-3.5z"/>',
  DOC16 + '<path class="fb" d="M8 5.5l3.2 3.5H9.4v3.5H6.6V9H4.8z"/>');
s('famEntwurf',
  '<path class="fp dd" d="M7.5 2.5h12l6 6v21h-18z"/><path class="f2" d="M19.5 2.5v6h6z"/><path class="ssa w3" d="M10.5 18l5 5 8.5-10"/>',
  '<path class="fp dd" d="M3.5 1.5h6l3 3v10h-9z"/><path class="f2" d="M9.5 1.5v3h3z"/><path class="ssa w2" d="M5 9l2 2 3.5-4.5"/>');
s('famBefunde',
  DOC32 + '<path class="fo" d="M10.5 12h11v4h-11z"/><path class="op" d="M10.5 20h11M10.5 24h7"/>',
  DOC16 + '<path class="fo" d="M5 5.5h6v2H5z"/><path class="op" d="M5 9.5h6M5 12h3"/>');
s('famBibliothek',
  '<path class="fl" d="M5.5 5.5h6v8h-6z"/><path class="f2" d="M13 7.5h5v6h-5z"/><path class="fb" d="M19.5 4.5h7v9h-7z"/><path class="f3" d="M3.5 13.5h25v2h-25z"/><path class="f2" d="M5.5 18.5h7v8h-7z"/><path class="fl" d="M14 19.5h6v7h-6z"/><path class="f1" d="M21.5 17.5h5v9h-5z"/><path class="f3" d="M3.5 26.5h25v2h-25z"/>',
  '<path class="fl" d="M2.5 2.5h4v4h-4z"/><path class="fb" d="M8 1.5h5v5H8z"/><path class="f3" d="M1.5 6.5h13v1.5h-13z"/><path class="f2" d="M2.5 9.5h4v4h-4z"/><path class="f1" d="M8 10h5v3.5H8z"/><path class="f3" d="M1.5 13.5h13v1.5h-13z"/>');
s('famAktualisieren',
  '<path class="sb w2" d="M23.7 9.6A10 10 0 1 0 25.4 19.4"/><path class="fb" d="M30.1 21.1L27.4 13.8 20.7 17.7z"/>',
  '<path class="sb w15" d="M11.8 4.8A5 5 0 1 0 12.7 9.7"/><path class="fb n" d="M15 10.5L13.7 6.9 10.35 8.85z"/>');

// ---------------------------------------------------------------------------------------------- Koordination (Modul 3b), Paletten

s('koVorgaben',
  '<path class="w15" d="M4.5 9h23M4.5 16h23M4.5 23h23"/><circle class="fb" cx="11" cy="9" r="3"/><circle class="fb" cx="21" cy="16" r="3"/><circle class="fb" cx="13" cy="23" r="3"/>',
  '<path d="M1.5 3.5h13M1.5 8h13M1.5 12.5h13"/><circle class="fb" cx="5" cy="3.5" r="1.9"/><circle class="fb" cx="10.5" cy="8" r="1.9"/><circle class="fb" cx="6.5" cy="12.5" r="1.9"/>');
s('koTypenliste',
  '<path class="fo" d="M3.5 5.5h8v6h-8z"/><path class="op" d="M3.5 9.5l4-4M7.5 11.5l4-4"/><path class="w15" d="M14.5 8.5h13"/><path class="f3" d="M3.5 13.5h8v6h-8z"/><path class="w15" d="M14.5 16.5h13"/><path class="fl" d="M3.5 21.5h8v6h-8z"/><path class="w15" d="M14.5 24.5h9"/>',
  '<path class="fo" d="M1.5 2h4v3.5h-4z"/><path d="M8 3.8h6.5"/><path class="f3" d="M1.5 6.3h4v3.5h-4z"/><path d="M8 8h6.5"/><path class="fl" d="M1.5 10.6h4v3.5h-4z"/><path d="M8 12.3h4.5"/>');
s('koPruefen',
  '<rect class="f2" x="3.5" y="10.5" width="16" height="16"/><rect class="fl" x="12.5" y="5.5" width="16" height="16"/><path class="fo" d="M12.5 10.5h7v11h-7z"/><path class="sr w15" d="M6 4l2 3M3 8l3 1.5M30 26l-2.5-2M27 29.5l-.5-3"/>',
  '<rect class="f2" x="1.5" y="6.5" width="8" height="8"/><rect class="fl" x="6.5" y="1.5" width="8" height="8"/><path class="fo" d="M6.5 6.5h3v3h-3z"/>');
s('koListe',
  '<rect class="fp" x="4.5" y="4.5" width="23" height="23"/><path class="f3" d="M4.5 4.5h23v4h-23z"/><path d="M4.5 13h23M4.5 18.5h23M4.5 24h23M11.5 8.5v19"/><circle class="fo n" cx="8" cy="15.8" r="1.8"/><circle class="fo n" cx="8" cy="21.3" r="1.8"/><path class="op" d="M14 15.8h10M14 21.3h7"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><path class="f3" d="M1.5 1.5h13v3h-13z"/><path d="M1.5 7.5h13M1.5 11h13M5.5 4.5v10"/><circle class="fo n" cx="3.5" cy="9.3" r="1"/><circle class="fo n" cx="3.5" cy="12.6" r=".8"/>');
const WANDAUS32 = '<path class="f3" d="M3.5 10.5h25v15h-25z"/>';
s('koAusVorschlagen',
  WANDAUS32 + '<circle class="fp so w15 dd" cx="16" cy="18" r="4.5"/><path class="fb" d="M13.5 2.5h5v4h2.5L16 11.5 11 6.5h2.5z"/>',
  '<path class="f3" d="M1.5 6.5h13v8h-13z"/><circle class="fp so dd" cx="8" cy="10.5" r="2.5"/><path class="fb" d="M6.5 1.5h3v2h1.5L8 6 5 3.5h1.5z"/>');
s('koAusNummer',
  WANDAUS32 + '<circle class="fp so w15" cx="16" cy="18" r="4.5"/><path class="w15" d="M14.8 16.3l1.8-1.1v6"/><path class="fb" d="M13 3.5h6v4.5h-6z"/><path class="sW" d="M14.5 5.8h3"/>',
  '<path class="f3" d="M1.5 6.5h13v8h-13z"/><circle class="fp so" cx="8" cy="10.5" r="2.5"/><path class="fb n" d="M6.5 1.5h3v2.5h-3z"/>');
s('koAusListe',
  '<rect class="fp" x="4.5" y="4.5" width="23" height="23"/><path class="f3" d="M4.5 4.5h23v4h-23z"/><path d="M4.5 13h23M4.5 18.5h23M4.5 24h23M11.5 8.5v19"/><circle class="fp so" cx="8" cy="15.8" r="1.8"/><circle class="fp so" cx="8" cy="21.3" r="1.8"/><path class="op" d="M14 15.8h10M14 21.3h7"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><path class="f3" d="M1.5 1.5h13v3h-13z"/><path d="M1.5 7.5h13M1.5 11h13M5.5 4.5v10"/><circle class="fp so" cx="3.5" cy="9.3" r="1"/><circle class="fp so" cx="3.5" cy="12.6" r=".8"/>');
s('koAbgleich',
  '<rect class="f1" x="2.5" y="6.5" width="10" height="19"/><circle class="fo" cx="7.5" cy="16" r="2.8"/><rect class="f1" x="19.5" y="6.5" width="10" height="19"/><circle class="fp so dd" cx="24.5" cy="16" r="2.8"/><path class="sb w2" d="M13.5 12h5M18.5 20h-5"/><path class="fb n" d="M17.5 9.2l3 2.8-3 2.8zM14.5 17.2l-3 2.8 3 2.8z"/>',
  '<rect class="f1" x="1.5" y="3.5" width="4.5" height="9"/><circle class="fo n" cx="3.75" cy="8" r="1.4"/><rect class="f1" x="10" y="3.5" width="4.5" height="9"/><circle class="fp so" cx="12.25" cy="8" r="1.4"/><path class="fb n" d="M7 5.2l2 1.5L7 8.2zM9 9.2L7 10.7l2 1.5z"/>');
s('koAusPlan',
  '<rect class="fp" x="3.5" y="3.5" width="25" height="25"/><rect class="fl" x="6.5" y="6.5" width="19" height="13"/><path class="f2" d="M3.5 22.5h25v6h-25z"/><path class="fb" d="M18.5 22.5h10v6h-10z"/><circle class="fp so w15" cx="12" cy="13" r="3.2"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><rect class="fl" x="3" y="3" width="10" height="6"/><path class="f2" d="M1.5 10.5h13v4h-13z"/><circle class="fp so" cx="6" cy="6" r="1.6"/>');
s('koAusIfc',
  QUADER32 + '<circle class="fp so w15 dd" cx="10" cy="19" r="3.4"/>',
  QUADER16 + '<circle class="fp so" cx="5" cy="10" r="1.6"/>');
s('koBcfExport',
  DOC32 + '<path class="op" d="M10.5 11.5h12M10.5 15h12"/><path class="fb" d="M10.5 22h7v-3.5l6 5-6 5V25h-7z"/>',
  DOC16 + '<path class="fb" d="M4.5 9.5h3.2V8l3.3 3L7.7 14v-1.5H4.5z"/>');
s('koBcfImport',
  DOC32 + '<path class="op" d="M10.5 11.5h12M10.5 15h12"/><path class="fb" d="M23 22h-7v-3.5l-6 5 6 5V25h7z"/>',
  DOC16 + '<path class="fb" d="M11 9.5H7.8V8L4.5 11l3.3 3v-1.5H11z"/>');
s('koPlan',
  '<rect class="fp" x="3.5" y="3.5" width="25" height="25"/><path class="w15" d="M3.5 18.5h11v10M14.5 18.5h14"/><path class="svl w2" d="M7 8h13"/><path class="szu w2" d="M7 12.5h18"/><path class="ssa w2" d="M22 8v7"/><path class="f3" d="M20.5 21.5h6v5h-6z"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><path d="M1.5 9.5h6v5M7.5 9.5h7"/><path class="svl" d="M3.5 4h6"/><path class="szu" d="M3.5 6.5h9"/><path class="f3" d="M10 10.5h3v3h-3z"/>');
s('koBoxen',
  '<rect class="f2" x="12.5" y="4.5" width="15" height="12"/><path d="M15.5 8.5h9M15.5 11h9M15.5 13.5h5"/><circle class="fp so w15" cx="8" cy="23" r="4.5"/><path class="w15" d="M11 19.8l3-3.3"/>',
  '<rect class="f2" x="6.5" y="1.5" width="8" height="6"/><path d="M8 3.5h5M8 5.5h5"/><circle class="fp so" cx="4" cy="11.5" r="2.5"/><path d="M5.8 9.7l2.2-2.2"/>');
s('famFamilie', QUADER32 + '<circle class="fb" cx="16" cy="16.5" r="3.2"/>', QUADER16 + '<circle class="fb n" cx="8" cy="8.3" r="1.8"/>');
s('palette',
  '<rect class="fp" x="3.5" y="4.5" width="25" height="23"/><path class="fb" d="M3.5 4.5h25v5h-25z"/><path class="f2" d="M3.5 9.5h8v18h-8z"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fb" d="M1.5 2.5h13v2.5h-13z"/><path class="f2" d="M1.5 5h4v8.5h-4z"/>');
s('projektbrowser',
  '<rect class="fp" x="3.5" y="4.5" width="25" height="23"/><path class="fb" d="M3.5 4.5h25v5h-25z"/><path d="M8 13.5v9M8 14h4M8 18.5h4M8 23h4"/><path class="f3" d="M6.5 11.5h3v3h-3z"/><path class="w15" d="M15 14h10M15 18.5h8M15 23h10"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fb" d="M1.5 2.5h13v2.5h-13z"/><path d="M4 6.5v5M4 7h2M4 9h2M4 11h2"/><path d="M8 7h5M8 9h4M8 11h5"/>');
s('koordination',
  '<rect class="fp" x="3.5" y="4.5" width="25" height="23"/><path class="fb" d="M3.5 4.5h25v5h-25z"/><path class="f2" d="M3.5 17.5h25v10h-25z"/><path d="M3.5 22.5h25M12.5 17.5v10"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fb" d="M1.5 2.5h13v2.5h-13z"/><path class="f2" d="M1.5 9h13v4.5h-13z"/><path d="M1.5 11.3h13M6 9v4.5"/>');
s('claude',
  '<path class="fl" d="M4.5 4.5h23v18H14l-6 5v-5H4.5z"/><path class="fy" d="M16 8.5l1.6 4 4 1.6-4 1.6-1.6 4-1.6-4-4-1.6 4-1.6z"/>',
  '<path class="fl" d="M1.5 1.5h13v9.5H8l-3.5 3.5V11h-3z"/><path class="fy" d="M8 3.4l.9 2.2 2.2.9-2.2.9-.9 2.2-.9-2.2-2.2-.9 2.2-.9z"/>');
s('palettenZurueck',
  '<rect class="fp" x="3.5" y="4.5" width="25" height="23"/><path class="fb" d="M3.5 4.5h25v5h-25z"/><path class="f2" d="M3.5 9.5h8v18h-8z"/><circle class="fo n" cx="23" cy="22.5" r="6.5"/><path class="sW w2" d="M20 22.5a3 3 0 1 0 1.2-2.4M20.5 18.5v2.2h2.2"/>',
  '<rect class="fp" x="1.5" y="2.5" width="13" height="11"/><path class="fb" d="M1.5 2.5h13v2.5h-13z"/><path class="f2" d="M1.5 5h4v8.5h-4z"/><circle class="fo n" cx="11.5" cy="10.5" r="4"/><path class="sW w15" d="M9.8 10.5a1.7 1.7 0 1 0 .7-1.4"/>');

// ---------------------------------------------------------------------------------------------- Zeichenwerkzeuge (werkzeuge/symbole.js)

s('achse',
  '<path class="sb dp w15" d="M16 8.5v15"/><circle class="fp sb w15" cx="16" cy="5" r="3.5"/><circle class="fp sb w15" cx="16" cy="27" r="3.5"/><path class="w15" d="M14.8 4.2L16 3.2v3.6"/>',
  '<path class="sb dp" d="M8 4.5v7"/><circle class="fp sb" cx="8" cy="2.6" r="1.8"/><circle class="fp sb" cx="8" cy="13.4" r="1.8"/>');
s('bemassung',
  '<path class="sb w15" d="M4.5 20.5h23M4.5 14v12M27.5 14v12"/><path class="sk w15" d="M2.5 23l4-5M25.5 23l4-5"/><path class="w2" d="M11 12.5h10"/><path class="op" d="M13 9.5h6"/>',
  '<path class="sb" d="M2.5 11h11M2.5 7v8M13.5 7v8"/><path class="w15" d="M5.5 4.5h5"/>');
s('text',
  '<path class="w3" d="M7.5 25.5L16 5.5l8.5 20M10.5 19h11"/><path class="sb w2" d="M4.5 29h23"/>',
  '<path class="w2" d="M3 12.5L8 2.5l5 10M4.8 9h6.4"/><path class="sb" d="M1.5 15h13"/>');
s('linie',
  '<path class="sb w2" d="M4.5 25.5l9-14 7 9 7.5-13"/><circle class="fp sb w15" cx="4.5" cy="25.5" r="2.4"/><circle class="fp sb w15" cx="28" cy="7.5" r="2.4"/>',
  '<path class="sb w15" d="M2.5 12.5l4-6 3 4 4-6"/><circle class="fb n" cx="2.5" cy="12.5" r="1.4"/><circle class="fb n" cx="13.5" cy="4.5" r="1.4"/>');
s('fuellbereich',
  '<rect class="fl" x="5.5" y="6.5" width="21" height="20"/><path class="sb" d="M5.5 14.5l8-8M5.5 22.5l16-16M8.5 26.5l18-18M16.5 26.5l10-10M24.5 26.5l2-2"/><rect x="5.5" y="6.5" width="21" height="20"/>',
  '<rect class="fl" x="2.5" y="3.5" width="11" height="9"/><path class="sb" d="M2.5 7.5l4-4M2.5 11.5l8-8M5.5 12.5l8-8M9.5 12.5l4-4"/><rect x="2.5" y="3.5" width="11" height="9"/>');
s('kote',
  '<path class="fb" d="M16 21.5l-6-9h12z"/><path class="w15" d="M3.5 21.5h25M22 12.5h6.5"/><path class="op" d="M24 16h4.5"/>',
  '<path class="fb" d="M8 11l-3-4.5h6z"/><path d="M1.5 11h13M11.5 6.5h3"/>');
s('nordpfeil',
  '<circle class="fp" cx="16" cy="16" r="12"/><path class="fr" d="M16 4.5l4 11.5h-8z"/><path class="f3" d="M16 27.5l-4-11.5h8z"/>',
  '<circle class="fp" cx="8" cy="8" r="6.5"/><path class="fr" d="M8 2l2.2 6H5.8z"/><path class="f3" d="M8 14L5.8 8h4.4z"/>');
s('schnittlinie',
  '<path class="sb dp w15" d="M3.5 22h25"/><path class="fb" d="M6 5.5l4.5 6.5h-9z"/><path class="fb" d="M26 5.5l4.5 6.5h-9z"/><path class="w15" d="M6 12v10M26 12v10"/>',
  '<path class="sb dp" d="M1.5 12h13"/><path class="fb" d="M3 2.5L5.5 6h-5z"/><path class="fb" d="M13 2.5L15.5 6h-5z"/><path d="M3 6v6M13 6v6"/>');
s('verschieben',
  '<path class="fb" d="M16 3l4.5 5h-3v6h-3V8h-3zM16 29l-4.5-5h3v-6h3v6h3zM3 16l5-4.5v3h6v3H8v3zM29 16l-5 4.5v-3h-6v-3h6v-3z"/>',
  '<path class="fb" d="M8 1.5L11 4.8H9.2V7H6.8V4.8H5zM8 14.5L5 11.2h1.8V9h2.4v2.2H11zM1.5 8L4.8 5v1.8H7v2.4H4.8V11zM14.5 8L11.2 11V9.2H9V6.8h2.2V5z"/>');
s('drehen',
  '<path class="sb w3" d="M25.9 19.2A9.5 9.5 0 1 1 20.8 7.8"/><path class="fb" d="M18.3 12.1L25.1 10.3 23.3 3.5z"/><circle class="fk n" cx="16" cy="16" r="2"/>',
  '<path class="sb w2" d="M12.7 9.7A5 5 0 1 1 10.5 3.7"/><path class="fb n" d="M9.25 5.9L13.1 5.2 11.75 1.5z"/><circle class="fk n" cx="8" cy="8" r="1.1"/>');
s('spiegeln',
  '<path class="sb dd w15" d="M16 3v26"/><path class="f2" d="M13 8L4.5 24H13z"/><path class="fl dd" d="M19 8l8.5 16H19z"/>',
  '<path class="sb dd" d="M8 1v14"/><path class="f2" d="M6.5 4L2 12h4.5z"/><path class="fl dd" d="M9.5 4L14 12H9.5z"/>');
s('ifcUebernehmen',
  '<path class="f1" d="M16 2.5l8 4.5-8 4.5-8-4.5z"/><path class="f2" d="M8 7l8 4.5v9L8 16z"/><path class="f3" d="M24 7v9l-8 4.5v-9z"/><path class="fb" d="M13 22.5h6v3h3.5L16 31.5l-6.5-6h3.5z"/>',
  '<path class="f1" d="M7 1.5l4 2.2-4 2.2-4-2.2z"/><path class="f2" d="M3 3.7l4 2.2v4.2l-4-2.2z"/><path class="f3" d="M11 3.7v4.2l-4 2.2V5.9z"/><path class="fb" d="M5.5 11h3v1.5h1.5L7 15.5 4 12.5h1.5z"/>');
s('cadEinfuegen',
  DOC32 + '<path class="sb w15" d="M10.5 25l4-9 4 6 3-4"/><path class="op" d="M10.5 25h11"/>',
  DOC16 + '<path class="sb" d="M4.5 12l2-4.5 2 3 1.5-2"/>');
s('pdfEinfuegen',
  DOC32 + '<path class="op" d="M10.5 11.5h12M10.5 14.5h9"/><path class="fr n" d="M7.5 19h18v7h-18z"/><path class="sW w15" d="M10 22.5h3M15 22.5h3M20 22.5h3"/>',
  DOC16 + '<path class="fr n" d="M3.5 8h9v4h-9z"/><path class="sW" d="M5 10h1.5M7.5 10h1.5M10 10h1"/>');
s('bildEinfuegen',
  '<rect class="fl" x="3.5" y="5.5" width="25" height="21"/><path class="f3" d="M4 26l7-9 5 6 5-7 7 10z"/><circle class="fy" cx="22" cy="12" r="2.7"/><rect x="3.5" y="5.5" width="25" height="21"/>',
  '<rect class="fl" x="1.5" y="2.5" width="13" height="11"/><path class="f3" d="M2 13l4-5 3 3.5 2.5-3 3 4.5z"/><circle class="fy n" cx="11" cy="5.5" r="1.3"/><rect x="1.5" y="2.5" width="13" height="11"/>');
s('kalibrieren',
  '<path class="fy" d="M3.5 22.5l19-19 6 6-19 19z"/><path d="M8 22l2.5 2.5M12 18l2.5 2.5M16 14l2.5 2.5M20 10l2.5 2.5"/><circle class="fb n" cx="5" cy="27" r="2"/><circle class="fb n" cx="27" cy="5" r="2"/>',
  '<path class="fy" d="M1.5 11l8.5-8.5 3.5 3.5-8.5 8.5z"/><path d="M4.5 10.5l1.5 1.5M7 8l1.5 1.5"/>');
s('unterlagen',
  '<path class="f2" d="M16 16.5l12.5 6-12.5 6-12.5-6z"/><path class="fp" d="M16 10.5l12.5 6-12.5 6-12.5-6z"/><path class="fl" d="M16 4.5l12.5 6-12.5 6-12.5-6z"/>',
  '<path class="f2" d="M8 8.5l6.5 3-6.5 3-6.5-3z"/><path class="fp" d="M8 5l6.5 3-6.5 3-6.5-3z"/><path class="fl" d="M8 1.5l6.5 3-6.5 3-6.5-3z"/>');
s('schemaVerbinden',
  '<rect class="f2" x="2.5" y="14.5" width="9" height="10"/><rect class="f2" x="20.5" y="14.5" width="9" height="10"/><path class="svl w2" d="M11.5 19.5h9"/><circle class="fb n" cx="16" cy="8" r="5.5"/><path class="sW w2" d="M16 5.5v5M13.5 8h5"/>',
  '<rect class="f2" x="1.5" y="6.5" width="4.5" height="6"/><rect class="f2" x="10" y="6.5" width="4.5" height="6"/><path class="svl w15" d="M6 9.5h4"/><circle class="fb n" cx="8" cy="3" r="2.4"/>');
s('schemaTrennen',
  '<rect class="f2" x="2.5" y="11.5" width="9" height="10"/><rect class="f2" x="20.5" y="11.5" width="9" height="10"/><path class="svl w2" d="M11.5 16.5h3M17.5 16.5h3"/><path class="sr w2" d="M13 25l6-17"/>',
  '<rect class="f2" x="1.5" y="5.5" width="4.5" height="6"/><rect class="f2" x="10" y="5.5" width="4.5" height="6"/><path class="svl" d="M6 8.5h1.3M8.7 8.5H10"/><path class="sr w15" d="M7 13l2-9"/>');
s('schemaEinfuegen',
  '<rect class="fl sb dd w15" x="3.5" y="8.5" width="14" height="10"/><circle class="fb n" cx="23.5" cy="21.5" r="6.5"/><path class="sW w2" d="M23.5 18.5v6M20.5 21.5h6"/><path class="svl w2" d="M10.5 18.5v4"/>',
  '<rect class="fl sb dd" x="1.5" y="3.5" width="8" height="6"/><circle class="fb n" cx="11.5" cy="11.5" r="4"/><path class="sW w15" d="M11.5 9.5v4M9.5 11.5h4"/>');
s('schemaAnordnen',
  '<rect class="f2" x="3.5" y="3.5" width="8" height="7"/><rect class="f2" x="20.5" y="3.5" width="8" height="7"/><rect class="fb" x="12" y="21.5" width="8" height="7"/><path class="svl w15" d="M11.5 7h9M7.5 10.5v5.5h8.5M24.5 10.5v5.5h-8.5M16 16v5.5"/>',
  '<rect class="f2" x="1.5" y="1.5" width="4.5" height="4"/><rect class="f2" x="10" y="1.5" width="4.5" height="4"/><rect class="fb" x="5.8" y="10.5" width="4.5" height="4"/><path class="svl" d="M6 3.5h4M3.7 5.5V8h4.3M12.3 5.5V8H8M8 8v2.5"/>');
s('koordinaten',
  '<path class="sb w2" d="M7.5 25.5V6M7.5 25.5H26"/><path class="fb n" d="M7.5 3l-3.2 5.5h6.4zM29.5 25.5l-5.5-3.2v6.4z"/><path class="sb dt" d="M20 25.5V12M7.5 12H20"/><circle class="fp" cx="7.5" cy="25.5" r="2.4"/><path class="so w2" d="M17.5 12h5M20 9.5v5"/>',
  '<path class="sb w15" d="M3.5 12.5V3.5M3.5 12.5H12"/><path class="fb n" d="M3.5 1.5L1.6 4.5h3.8zM14.5 12.5L11.5 10.6v3.8z"/><circle class="fp" cx="3.5" cy="12.5" r="1.4"/><path class="so" d="M8.5 6.5h3M10 5v3"/>');

// ---------------------------------------------------------------------------------------------- Nachbearbeitung N3: Einlageplan, Grundleitungsplan, Liste als CSV

s('einlageplan',
  '<rect class="fp" x="3.5" y="3.5" width="25" height="25"/><path class="fb" d="M3.5 3.5h25v4h-25z"/><path class="f2" d="M6.5 10.5h19v13h-19z"/><path class="s3 op" d="M6.5 15.5l5-5M6.5 21.5l11-11M9.5 23.5l13-13M15.5 23.5l10-10M21.5 23.5l4-4"/><path class="sk w4" d="M8.5 20.5h6l3-6h5"/><path class="sab w2" d="M8.5 20.5h6l3-6h5"/><circle class="fp sab w15" cx="22.5" cy="14.5" r="2.2"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><path class="fb" d="M1.5 1.5h13v2.5h-13z"/><path class="f2" d="M3 5.5h10v7H3z"/><path class="sk w3" d="M4.5 10.5h2.5l1.5-3h2"/><path class="sab w15" d="M4.5 10.5h2.5l1.5-3h2"/><circle class="fp sab" cx="11" cy="7.5" r="1.3"/>');
s('grundleitungsplan',
  '<rect class="fp" x="3.5" y="3.5" width="25" height="25"/><path class="fb" d="M3.5 3.5h25v4h-25z"/><path class="fy op" d="M6.5 14.5h19v11h-19z"/><path class="f3" d="M6.5 10.5h19v4h-19z"/><path class="sk w4" d="M9 18.5h4.5l5 4.5h2.5"/><path class="sab w2" d="M9 18.5h4.5l5 4.5h2.5"/><circle class="fp sab w15" cx="9" cy="18.5" r="2.2"/><path class="fb" d="M21 20.5l4.5 2.5-4.5 2.5z"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><path class="fb" d="M1.5 1.5h13v2.5h-13z"/><path class="fy op" d="M3 7.5h10v5H3z"/><path class="f3" d="M3 5.5h10v2H3z"/><path class="sk w3" d="M4.5 9.5h2.5l2 1.5h1"/><path class="sab w15" d="M4.5 9.5h2.5l2 1.5h1"/><circle class="fp sab" cx="4.5" cy="9.5" r="1.2"/><path class="fb n" d="M10.5 9.5l2.5 1.5-2.5 1.5z"/>');
s('listeCsv',
  '<rect class="fp" x="3.5" y="4.5" width="21" height="18"/><path class="fb" d="M3.5 4.5h21v5h-21z"/><path d="M3.5 14h21M10.5 9.5v13"/><path class="fp" d="M15.5 14.5h9l4 4v11h-13z"/><path class="f2" d="M24.5 14.5v4h4z"/><path class="op" d="M17.5 21.5h2.5M25 21.5h1.5M17.5 26h2.5M25 26h1.5"/><circle class="fb n" cx="22.6" cy="21.8" r="1.1"/><path class="sb w15" d="M22.4 22.5l-.7 1.6"/><circle class="fb n" cx="22.6" cy="26.3" r="1.1"/><path class="sb w15" d="M22.4 27l-.7 1.6"/>',
  '<rect class="fp" x="1.5" y="1.5" width="10" height="9"/><path class="fb" d="M1.5 1.5h10v2.5h-10z"/><path d="M1.5 7h10M5.5 4v6.5"/><path class="fp" d="M8.5 6.5h3.5l2.5 2.5v5.5h-6z"/><path class="f2" d="M12 6.5v2.5h2.5z"/><circle class="fb n" cx="11.5" cy="11.6" r="1.1"/><path class="sb w15" d="M11.4 12.3l-.6 1.5"/>');

// ---------------------------------------------------------------------------------------------- Ausgabe IFC (Modul 7 Teil B2)

s('ifcExport',
  '<path class="f1" d="M13 3.5l8.5 5-8.5 5-8.5-5z"/><path class="f2" d="M4.5 8.5l8.5 5v10l-8.5-5z"/><path class="f3" d="M21.5 8.5v10l-8.5 5v-10z"/><path class="fb" d="M17 23.5h6v-3.5l7 6-7 6v-3.5h-6z"/>',
  '<path class="f1" d="M6.5 1.5l4 2.5-4 2.5-4-2.5z"/><path class="f2" d="M2.5 4l4 2.5v5l-4-2.5z"/><path class="f3" d="M10.5 4v5l-4 2.5v-5z"/><path class="fb" d="M8.5 11.5h3V9.8l3.5 3-3.5 3v-1.7h-3z"/>');

// ---------------------------------------------------------------------------------------------- Sanitär (Modul 4 Teil A-O)

s('sanZusammenstellung',
  '<rect class="fp" x="3.5" y="4.5" width="25" height="23"/><path class="fb" d="M3.5 4.5h25v6.5h-25z"/><path d="M3.5 17h12M3.5 22h12M15.5 11v16.5"/><path class="sW w15" d="M12 6.2H8.5l2.5 2.3-2.5 2.3H12"/><path class="fsa" d="M22 13.5c-2.8 3.4-3.8 4.8-3.8 6.4a3.8 3.8 0 0 0 7.6 0c0-1.6-1-3-3.8-6.4z"/>',
  '<rect class="fp" x="1.5" y="1.5" width="13" height="13"/><path class="fb" d="M1.5 1.5h13v4h-13z"/><path d="M1.5 9h6M1.5 12h6M7.5 5.5v9"/><path class="fsa" d="M11 6.8c-1.5 1.8-2 2.5-2 3.4a2 2 0 0 0 4 0c0-.9-.5-1.6-2-3.4z"/>');

// ---------------------------------------------------------------------------------------------- Quest App V1a: Problem-Marker, Begehen, Sitzungstisch, Gesten, Haken; V1c: Bildschirm

s('problemMarker',
  '<path class="fo" d="M16 29.5s-8.5-8.6-8.5-15A8.5 8.5 0 0 1 24.5 14.5c0 6.4-8.5 15-8.5 15z"/><path class="sW w2" d="M16 9v6"/><circle class="fW n" cx="16" cy="19" r="1.4"/>',
  '<path class="fo" d="M8 15s-4.5-4.4-4.5-8A4.5 4.5 0 0 1 12.5 7c0 3.6-4.5 8-4.5 8z"/><path class="sW w15" d="M8 4.6v3"/><circle class="fW n" cx="8" cy="9.6" r="0.8"/>');
s('raumBegehen',
  '<path class="f2" d="M3 24l13-6 13 6-13 6z"/><circle class="fb n" cx="16" cy="5.5" r="2.8"/><path class="sb w2" d="M16 9v8M12 12.5l4 2 4-2M16 17l-3 5M16 17l3 5"/>',
  '<path class="f2" d="M1.5 12l6.5-3 6.5 3-6.5 3z"/><circle class="fb n" cx="8" cy="3" r="1.6"/><path class="sb w15" d="M8 5v4.5M6 7l2 1 2-1M8 9.5l-1.5 2.5M8 9.5l1.5 2.5"/>');
s('sitzungstisch',
  '<path class="f2" d="M3 16.5h26v3H3z"/><path class="w15" d="M7 19.5v8M25 19.5v8"/><path class="f1" d="M16 5l6 3.2-6 3.2-6-3.2z"/><path class="fl" d="M10 8.2l6 3.2v5.1l-6-3.2z"/><path class="fb" d="M22 8.2v5.1l-6 3.2v-5.1z"/>',
  '<path class="f2" d="M1.5 8.5h13v2h-13z"/><path d="M3.5 10.5v4M12.5 10.5v4"/><path class="f1" d="M8 2l3.5 1.8L8 5.6 4.5 3.8z"/><path class="fl" d="M4.5 3.8L8 5.6v2.9L4.5 6.7z"/><path class="fb" d="M11.5 3.8v2.9L8 8.5V5.6z"/>');
s('gesten',
  '<path class="fy" d="M9 17V9.5a1.8 1.8 0 0 1 3.6 0V15V6.5a1.8 1.8 0 0 1 3.6 0V15V7.5a1.8 1.8 0 0 1 3.6 0V15.5V10a1.8 1.8 0 0 1 3.6 0v9c0 5.5-3.5 9.5-8.5 9.5c-3.5 0-5.5-1.8-7.5-4.5l-3.5-5.2a1.8 1.8 0 0 1 3-2z"/>',
  '<path class="fy" d="M4.5 9V5a1 1 0 0 1 2 0v3.5V3.5a1 1 0 0 1 2 0V8V4a1 1 0 0 1 2 0v4.5V6a1 1 0 0 1 2 0v4.5c0 2.8-1.8 4.5-4.2 4.5c-1.8 0-2.8-1-3.8-2.3L2.6 10.3a1 1 0 0 1 1.6-1.1z"/>');
s('haken',
  '<circle class="fsa n" cx="16" cy="16" r="12"/><path class="sW w3" d="M10 16.5l4 4 8-9"/>',
  '<circle class="fsa n" cx="8" cy="8" r="6.5"/><path class="sW w2" d="M5 8.2l2 2 4-4.5"/>');
s('bildschirm',
  '<rect class="f3" x="2.5" y="4.5" width="27" height="18" rx="1.5"/><rect class="fl n" x="4.5" y="6.5" width="23" height="14"/><path class="sb w15" d="M7.5 17.5h7"/><path class="f2" d="M14 22.5h4v4h-4z"/><path class="f3" d="M9.5 26.5h13v2.5h-13z"/>',
  '<rect class="f3" x="1" y="2" width="14" height="9.5" rx="1"/><rect class="fl n" x="2.2" y="3.2" width="11.6" height="7.1"/><path class="f2" d="M7 11.5h2v2H7z"/><path class="f3" d="M4.5 13.5h7V15h-7z"/>');

// ---------------------------------------------------------------------------------------------- Register XR (26.10.2026)
// Brille, App, Labor und Zeigen im Register «XR»; eigene Entwürfe, dieselbe Bildsprache wie Sitzungstisch und Raum begehen (graue Körper, blaue Akzente).

s('questVerbinden',
  '<rect class="f2" x="1.5" y="4.5" width="29" height="13" rx="3.5"/><rect class="fl n" x="4" y="7" width="24" height="8" rx="2"/><path class="fp" d="M12 17.5l1.5-3.5h5l1.5 3.5z"/><path class="fb n" d="M5 23.5h13V20l8 5.75L18 31.5V28H5z"/>',
  '<rect class="f2" x="0.8" y="1.8" width="14.4" height="6.4" rx="1.8"/><rect class="fl n" x="2" y="3" width="12" height="3.6" rx="1"/><path class="fp" d="M6 8.2l.8-1.8h2.4l.8 1.8z"/><path class="fb n" d="M2.5 11.3h6.2V9.7l4.8 2.9-4.8 2.9v-1.6H2.5z"/>');
s('appInstallieren',
  '<path class="fb n" d="M12.5 2h7v6h4L16 14.5 8.5 8h4z"/><rect class="f2" x="1.5" y="16" width="29" height="12" rx="3.5"/><rect class="fl n" x="4" y="18.5" width="24" height="7" rx="2"/><path class="fp" d="M12 28l1.5-3h5l1.5 3z"/>',
  '<path class="fb n" d="M6 0.8h4v3.4h2.2L8 8 3.8 4.2H6z"/><rect class="f2" x="0.8" y="8.8" width="14.4" height="6" rx="1.8"/><rect class="fl n" x="2" y="10" width="12" height="3.3" rx="1"/><path class="fp" d="M6 14.8l.8-1.7h2.4l.8 1.7z"/>');
s('autostart',
  '<circle class="fsa n" cx="16" cy="16" r="12"/><path class="fW n" d="M13 10.5l9 5.5-9 5.5z"/>',
  '<circle class="fsa n" cx="8" cy="8" r="6.5"/><path class="fW n" d="M6.3 5l4.6 3-4.6 3z"/>');
s('brilleZeigen',
  '<rect class="f2" x="5" y="2.5" width="22" height="10" rx="3"/><rect class="fl n" x="7" y="4.5" width="18" height="6" rx="1.5"/><path class="fp" d="M3 22.5c3.5-5 8-7.5 13-7.5s9.5 2.5 13 7.5c-3.5 5-8 7.5-13 7.5S6.5 27.5 3 22.5z"/><circle class="fb n" cx="16" cy="22.5" r="4"/><circle class="fk n" cx="16" cy="22.5" r="1.7"/>',
  '<rect class="f2" x="3" y="1" width="10" height="5" rx="1.5"/><rect class="fl n" x="4" y="2" width="8" height="3" rx="0.8"/><path class="fp" d="M1 11c1.8-2.6 4-3.8 7-3.8s5.2 1.2 7 3.8c-1.8 2.6-4 3.8-7 3.8S2.8 13.6 1 11z"/><circle class="fb n" cx="8" cy="11" r="2"/><circle class="fk n" cx="8" cy="11" r="0.8"/>');
s('anBrille',
  '<circle class="fb n" cx="16" cy="13" r="1.8"/><path class="sb w2" d="M12 10.5a5.5 5.5 0 0 1 8 0"/><path class="sb w2" d="M8.5 7.5a10 10 0 0 1 15 0"/><rect class="f2" x="1.5" y="17" width="29" height="12" rx="3.5"/><rect class="fl n" x="4" y="19.5" width="24" height="7" rx="2"/><path class="fp" d="M12 29l1.5-3h5l1.5 3z"/>',
  '<circle class="fb n" cx="8" cy="6" r="1"/><path class="sb w15" d="M5.8 4.6a3 3 0 0 1 4.4 0"/><path class="sb w15" d="M3.4 2.6a6 6 0 0 1 9.2 0"/><rect class="f2" x="0.8" y="9" width="14.4" height="6" rx="1.8"/><rect class="fl n" x="2" y="10.2" width="12" height="3.3" rx="1"/><path class="fp" d="M6 15l.8-1.7h2.4L10 15z"/>');
s('raumAr',
  '<path class="f1" d="M16 7.5l8.5 4.8-8.5 4.8-8.5-4.8z"/><path class="f2" d="M7.5 12.3l8.5 4.8v9.4l-8.5-4.8z"/><path class="f3" d="M24.5 12.3v9.4l-8.5 4.8v-9.4z"/><path class="sb w2" d="M3 9V3h6M23 3h6v6M29 23v6h-6M9 29H3v-6"/>',
  '<path class="f1" d="M8 3.8l4.2 2.4L8 8.6 3.8 6.2z"/><path class="f2" d="M3.8 6.2L8 8.6v4.7l-4.2-2.4z"/><path class="f3" d="M12.2 6.2v4.7L8 13.3V8.6z"/><path class="sb w15" d="M1.5 4.5v-3h3M11.5 1.5h3v3M14.5 11.5v3h-3M4.5 14.5h-3v-3"/>');
s('xrLabor',
  '<path class="fp" d="M12 3h8v8l7.5 14A2.5 2.5 0 0 1 25.3 28.6H6.7A2.5 2.5 0 0 1 4.5 25L12 11z"/><path class="fb" d="M9.2 19h13.6l3 5.6a2.5 2.5 0 0 1-2.2 3.6H8.4A2.5 2.5 0 0 1 6.2 24.6z"/><circle class="fW n" cx="13.5" cy="23.5" r="1.3"/><circle class="fW n" cx="19" cy="22" r="1"/>',
  '<path class="fp" d="M6.5 1.5h3v4l4.3 7a1.2 1.2 0 0 1-1 1.8H3.2a1.2 1.2 0 0 1-1-1.8l4.3-7z"/><path class="fb" d="M3.8 10h8.4l1.6 2.5a1.2 1.2 0 0 1-1 1.8H3.2a1.2 1.2 0 0 1-1-1.8z"/><circle class="fW n" cx="6.5" cy="12.2" r="0.7"/>');

// ---------------------------------------------------------------------------------------------- Ausgabe

/** Alle Schlüssel mit Revit-Fassung. */
export const REVIT_NAMEN = Object.keys(REVIT);

/** Gibt es zu dem Namen eine Revit-Fassung? */
export const hatRevitFassung = (name) => Object.prototype.hasOwnProperty.call(REVIT, name);

/** Innenleben der Zeichnung (SVG-Elemente) in der Grösse 32 oder 16; null, wenn es keine Fassung gibt. */
export function revitInhalt(name, kleinste) {
  const e = hatRevitFassung(name) ? REVIT[name] : null;
  return e ? e[kleinste ? 1 : 0] : null;
}

/**
 * SVG-Text der Revit-Fassung oder null (dann gilt der Strich). Ab px > 20 die 32-er Zeichnung, sonst die 16-er (eigens gezeichnet, nicht verkleinert);
 * px ist die angezeigte Grösse. Das SVG trägt data-sym (Name), damit der Satz ohne Neuladen getauscht werden kann.
 */
export function revitSymbol(name, px = 28) {
  if (!hatRevitFassung(name)) return null;
  const klein = !(px > 20);
  const k = klein ? 16 : 32;
  return `<svg class="sym sym-rv" data-sym="${name}" viewBox="0 0 ${k} ${k}" width="${px}" height="${px}" aria-hidden="true">${REVIT[name][klein ? 1 : 0]}</svg>`;
}
