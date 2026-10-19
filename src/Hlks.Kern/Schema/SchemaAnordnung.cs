// Schema als verknüpfte Ansicht (Modul 3, Teil B3, umgebaut im Nachtrag B8 «Strangschema nach Beispiel», Abnahme Christian
// 19.10.2026): Anordnung des Strangschemas. Die Lagen werden bei jedem Lesen aus dem Schemagraphen gerechnet und nicht
// gespeichert (Entscheid Hauptlauf 18.10.2026); Ansicht.lagen hält nur die von Hand gesetzten Lagen { elementId: { x, y,
// manuell: true } }, die die automatischen ersetzen. Gleiche Modelle ergeben gleiche Lagen; alle Reihenfolgen laufen nach Lage,
// Netzabstand bzw. Fliessrang und bei Gleichstand nach Kennzeichen, nie nach der Speicherreihenfolge.
//
// Christian: «Ein Strangschema zeigt die Zusammenhänge der Anlagen als Übersicht. Es muss nicht massstäblich sein, jedoch muss
// die Reihenfolge der Abgänge dem System entsprechen. Üblicherweise wird das Strangschema strangweise abgebildet.»
//
// Koordinaten: Papier-mm der Schema-Ansicht, x nach rechts, y nach oben (Plangrafik mit Massstab 1). Lage im Gebäude: x Ost,
// y Nord (Projekt-Nord, wie das Modell).
//
// Regeln (für Handrechnungen, Raster siehe SchemaRaster):
//   Zeilen   je Geschoss eine, nach Kote aufsteigend von unten (Zeile i: y0 = i · Zeilenhöhe); Knoten ohne Geschoss in der
//            Zone «ohne Geschoss» unter allen Zeilen (y0 = −(Zeilenhöhe + Zonenabstand)).
//   Gruppen  je Steigzone (Strang.steigzone, nur von Hand) eine Gruppe mit allen Strängen dieses Werts (Bündel); ein Strang ohne
//            Steigzone ist seine eigene Gruppe. Gruppen nebeneinander nach ihrer Lage im Gebäude, quer zur Blickrichtung
//            (Ansicht.blickrichtung, ohne Angabe sued): von Süden gesehen x (links West, rechts Ost), von Norden −x, von Osten y,
//            von Westen −y; bei Gleichstand nach der Tiefe in Blickrichtung (nahe zuerst), dann nach Bezeichnung. Gruppen ohne Lage
//            (nichts platziert) folgen rechts nach Bezeichnung, am Schluss die Zonen «ohne Strang» (je System) und «ohne System»
//            nach der Zonenlücke. Blickrichtung gilt für Projekt-Nord (Katalog); der nordwinkel (geografisch Nord) spielt keine Rolle.
//            Lage eines Strangs (Grundriss x, y): senkrechter Strang = seine Steigachse, d. h. unter den steilen Leitungsabschnitten
//            (|Δz| ≥ waagrechte Länge) die Stelle im Achsraster mit der grössten Summe |Δz| (kurze Fallstücke zu den Apparaten
//            zählen so nicht), Mittel ihrer Abschnittsmitten gewichtet mit |Δz|; sonst und bei waagrechten Strängen das nach Länge
//            gewichtete Mittel aller Leitungsabschnitte; ohne Leitungen das Mittel der Punkte seiner übrigen Bauteile. Lage einer
//            Gruppe = Mittel ihrer Stränge. Breite einer Gruppe = Vielfaches der Spaltenbreite, das den Bedarf deckt.
//   Bündel   in der Gruppe zuerst die senkrechten Stränge, dann die waagrechten, je nach ihrer realen Lage quer zur Blickrichtung
//            (wie die Gruppen; «Medien je nach Lage in der Realität», Entscheid Christian). Stranglinie i bei x0 + Strangversatz +
//            i · Bündelabstand; waagrechte Stränge einer Gruppe mit senkrechten liegen auf x der Endgeräte (Bündelrand +
//            Endgerätversatz), sonst bei x0 + Strangversatz. Strang.art verteilleitung, sammelleitung, grundleitung,
//            anschlussleitung = waagrecht, sonst senkrecht. Über der obersten Zeile ein Kreis mit der Bezeichnung: Steigzone, sonst
//            Kennzeichen des Strangs (eindeutig und kurz; der Name steht klein daneben).
//   Strangknoten  Leitungsbauteile mit senkrechtem Strang (teilVon). Steigknoten = über eine Schemalinie mit einem Knoten in
//            einer anderen Zeile oder mit einem Leitungsbauteil eines anderen Strangs verbunden; Etagenknoten = die übrigen, sofern
//            sie über Etagenknoten von einem Steigknoten aus erreichbar sind (sonst gelten auch sie als Steigknoten).
//            Steigknoten stehen auf der Stranglinie nach Fliessrang (steigend, wenn der Strang aufwärts fliesst: steigstrang bzw.
//            Verbraucher stromab; fallend bei fallstrang bzw. Verbraucher stromauf), bei Gleichstand nach Kennzeichen. Im Bündel
//            verschränkt: Stufe j des Strangs i = j · n + i, belegte Stufen dicht gezählt, y = y0 + Knoten unten + Stufe ·
//            Stufenhöhe (ein Strang: Knotenschritt; Bündel: Knotenschritt / n, mindestens Bündelstufe; reicht der Platz bis
//            y0 + Zeilenhöhe − Knoten oben nicht, gleichmässig enger).
//   Netzweg  je Zelle (Gruppe × Geschoss) und Stranglinie ein Baum kürzester Wege (Dijkstra) ab den Steigknoten dieses Strangs
//            (bzw. den Knoten eines waagrechten Strangs) über seine Etagenknoten zu den Geräten des Geschosses; Länge = Summe der
//            Leitungslängen (Geometrie, mm), bei Gleichstand die Zahl der Schritte (nicht platzierte Leitungen zählen nur als
//            Schritte). Geräte leiten nicht weiter. Je Strang ein eigener Baum, damit Wege verschiedener Medien nicht vermischt
//            werden (ein Waschtisch hängt an KW und WW).
//   Endgeräte und Geräte (Quelle, Senke, Zentrale): bei der Gruppe des Strangs, an den sie angeschlossen sind (eigener Strang
//            bzw. nächster über das Netz, bei gleichem Abstand die erste Stranglinie), auf ihrem Geschoss nach Lage im Netz:
//            lexikografisch über die Stranglinien der Zelle, die mit den meisten erreichten Geräten zuerst (dann Platz), je
//            Stranglinie (a) erreicht vor nicht erreicht, (b) Stufe ihrer Wurzel von oben nach unten (verschiedene Etagenleitungen
//            kreuzen sich so nicht), (c) Abstand ihres Abzweigs (letzter Knoten mit zwei Ästen auf dem Weg), (d) eigener Abstand;
//            zuletzt Kennzeichen. Erstes bei Bündelrand + Endgerätversatz, dann im Endgerätabstand, y auf dem Boden der Zeile (B8d, siehe
//            «Apparate auf dem Boden»; bis B8c y0 + Endgerätehöhe). Linien verschiedener Bündelstränge enden am Endgerät um den
//            Anschlussversatz nebeneinander.
//   Etagenknoten  geteilt (Endgeräte von mehr als einem Platz dahinter) = auf der Etagenleitung (y der Wurzel) unter dem ersten
//            Platz dahinter; mehrere mit demselben ersten Platz gleichmässig zwischen dem Vorgänger und diesem Platz;
//            Abgang (nur ein Endgerät dahinter) = auf dem senkrechten Abgang über dem Endgerät, gleichmässig zwischen
//            Etagenleitung und Endgerät; ohne Endgerät dahinter = Etagenschritt rechts des Vorgängers.
//   Wohnungsblöcke  bei Ansicht.wohnungsbloecke: Endgeräte einer Zone (Raum.teilVon bzw. teilVon = Zone) je Gruppe und Geschoss
//            bilden einen Block (Platz in der Reihenfolge = erstes Endgerät des Blocks): Rechteck von y0 + Block unten bis y0 +
//            Zeilenhöhe − Block oben, Tabelle je Apparateart (Klassenkürzel, Reihenfolge des ersten Auftretens) mit Stückzahl
//            und Total; Spalten LU, KW, WW sind vorgesehen (Platz und Datenstruktur) und bleiben bis Modul 4 leer. Knoten, hinter
//            denen nur der Block liegt, sind Blockinneres (verdeckt, Linien enden am Blockrand auf der Höhe ihrer Anschlusslinie);
//            Armaturen am Eintritt in den Block (vor dem ersten anderen Knoten) stehen als Anschlussarmaturen auf der Anschlusslinie
//            (y der Wurzel) in den Armaturplätzen vor dem Block. Endgeräte ohne Zone bleiben einzeln.
//   Waagrechte Stränge  Knoten auf ihrer Spur (y0 + Spur unten + Spur · Spurabstand; Spuren je Zeile nach Art – grundleitung,
//            sammelleitung, verteilleitung, anschlussleitung – dann Stranglinie). Ist der Knoten über eine Schemalinie direkt mit
//            einem Knoten auf einer anderen senkrechten Stranglinie verbunden und liegt er im Grundriss höchstens die Ankerweite
//            neben diesem Strang (nicht platziert: ohne diese Bedingung), steht er darunter (nächster solcher Strang, bei Gleichstand
//            die erste Stranglinie; mehrere am selben Strang je um den Ankerversatz nach rechts); sonst ab der eigenen Linie im
//            Waagrechtschritt nach Fliessrang.
//   Zonen    «ohne Strang» (je System) bzw. «ohne System»: Knoten nebeneinander nach Kennzeichen, x0 + Strangversatz + i ·
//            Endgerätabstand, y0 + Endgerätehöhe (Endgeräte und Geräte auf dem Boden, B8d). Freie Züge (ohne Knoten) folgen in ihrer
//            Zelle nach den Knoten als kurze Linie.
//   Linien   orthogonal zwischen den Lagen ihrer Enden: gerade, wenn x oder y gleich; sonst ein Knick. Ein Abgang-Ende läuft
//            senkrecht auf seinem Abgang, ein Etagen-Ende waagrecht auf seiner Etagenleitung; ein Ende auf einer waagrechten Spur
//            verlässt diese waagrecht zu einer Stranglinie oder einer anderen Spur, senkrecht zu einem freien Knoten; zwei Enden
//            auf Stranglinien: zuerst senkrecht am Anfangsstrang; Stranglinie und freier Knoten in derselben Zeile: zuerst
//            waagrecht ab dem Strang, sonst zuerst senkrecht am Strang; sonst zuerst waagrecht ab dem Anfang. Spurknoten unter einem
//            Steigstrang und freier Knoten: der Spur entlang, dann senkrecht beim Gerät (nicht dem Steigstrang entlang). Offene
//            Enden liegen die Offenlänge neben ihrem Knoten (Richtung nach Anschlussnummer, auf einem Fallstrang umgekehrt: a oben).
//            Im Block verdeckte Knoten: Linienende am linken Blockrand auf der Höhe der Gegenseite (beide im Block: auf der
//            Anschlusslinie, die Linie hat dann die Länge 0). Von Hand verschobene Knoten gelten als frei; ihre Linien werden neu
//            geführt.
//   Dämmung  je Gruppe und Geschoss der Text «Dämmung d mm» aus daemmdicke der Leitungen der senkrechten Stränge in diesem
//            Geschoss, nur wenn gesetzt, gleiche Werte zusammengefasst (Strich über die Stranglinien mit diesem Wert), rechts des
//            Bündels auf y0 + Dämmhöhe, weitere Werte darunter. Belastung in Klammern (LU, DU, l/s) erst mit Modul 4.
//   Einlagen (Nachtrag B8c, Entscheid Christian 19.10.2026: «Leitungen, die im Beton eingelegt sind, müssen auch im Schema so
//            gezeigt werden»; Vorbild Schema Abwasser Bernstrasse) – Verlegung aus Leitungen/Verlegung.cs. Bänder: Band 0 unter der
//            untersten Zeile (Bodenplatte), Band i zwischen Zeile i−1 und i (Decke über dem Geschoss der Zeile i−1), Band n über der
//            obersten Zeile (Dach). Eine Schemalinie ist eine Einlage, wenn mehr als die Hälfte ihrer Leitungslänge (Geometrie; ohne
//            Geometrie je Leitung 1) Einlage in einer Decke, einem Dach bzw. einer Bodenplatte ist; ihr Band ist das mit der grössten
//            Einlagelänge: Decke bzw. Dach über Geschoss G = Band über der Zeile von G, Bodenplatte = Band unter der Zeile ihres
//            Geschosses. Einlagen in Wänden bleiben, wo sie sind (Hinweis). Knoten auf waagrechten Strängen (Spur) und
//            Etagenknoten (geteilt, ohne Endgerät, Anschlussarmatur) mit Verlegung einlage in einem solchen Bauteil stehen im Band
//            (nicht, wenn ihre Lage von Hand gesetzt ist). Spuren im Band: Einlagezüge = Einlage-Linien und -Knoten desselben Bands,
//            über Knoten im Band verbunden, mit ihrer x-Ausdehnung (Enden; offenes Ende ± Offenlänge); nach Anfang, Ende, kleinstem
//            Kennzeichen auf die oberste Spur, deren letzter Zug mindestens die Spurlücke davor endet, sonst eine neue Spur darunter –
//            sich überdeckende Züge liegen so übereinander, die übrigen auf einer Spur. Lage y = Bandmitte + ((n − 1)/2 − t) ·
//            Einlagespur. Höhe des Bands = max(Deckenband, 2 · Bandrand + (n − 1) · Einlagespur): ohne Einlagen und mit einer Spur
//            unverändert. Wächst ein Band, bleibt seine Oberkante (Band 0…n−1) bzw. Unterkante (Band n) gegenüber der Zeile darüber
//            bzw. darunter, alle Zeilen darüber rücken um den Zuwachs nach oben (die Zone «ohne Geschoss» nach unten um den Zuwachs
//            von Band 0) und die Lagen werden mit den neuen Zeilen gerechnet; sonst bleibt das Layout gleich. Linien einer Einlage
//            laufen senkrecht ins Band, waagrecht auf ihrer Spur und senkrecht zum anderen Ende (A → (xA, yB) → (xB, yB) → B),
//            ausgedünnt um doppelte und in einer Geraden liegende Punkte. Andere Linien mit genau einem Ende im Band:
//            waagrecht auf der Höhe des anderen Endes, senkrecht ins Band. Gemischte Linien (teils Einlage, teils frei) gelten nach
//            der Mehrheit der Länge (Hinweis); der Übergang zwischen Band und Spur zeigt sich als senkrechter Versatz am Knoten.
//
// Nachtrag B8d (Christian 19.10.2026: «Beim Schema sind die Apparate unter der Decke dargestellt. Diese sollen über dem Boden liegen.
// Gleiches auch bei den Kontrollschächten. Diese sind im oder unter dem Boden und müssten also eher als Schnitt gezeigt werden.»;
// Entscheide im Fragefenster: Grundleitungen unter dem untersten Deckenband zum Schacht, Bodenabläufe und Terrasseneinläufe im
// Deckenband mit Oberkante bündig zum Boden; Vorbild Schema Abwasser Bernstrasse). Alle Abstände sind Darstellungsannahmen (SchemaRaster).
//   Apparate auf dem Boden  Endgeräte und Geräte (nicht Abläufe, Schächte, im Block verdeckte) stehen unten in ihrer Zeile auf einem
//            gemeinsamen Boden. Boden der Zeile = das Höchste aus: Oberkante des Bands unter der Zeile; y der obersten Spur einer waagrechten
//            Leitung in der Zeile; Oberkante der Dämmungstexte (y0 + 15 + 0.75 · 1.6); je Netzweg einer Zelle der Zeile die Höhe seiner
//            Etagenleitung = y der Wurzel, wenn ihre Linie zu einem Platz führt – liegt diese Linie zum ersten Knoten dahinter (bzw. dieser
//            Knoten oder die Wurzel) als Einlage im Band unter der Zeile oder unter dem untersten Band, gilt die Bandoberkante –; y der
//            Anschlusslinien (unten). Unterkante der Symbole = Boden + Bodenabstand 3 mm, mit k Knoten auf dem Abgang zu einem Endgerät
//            mindestens Wurzel + (k + 1) · Abgangschritt 5 mm. Knoten-y = Unterkante + Abstand des Knotens über der Unterkante seines
//            Symbols (Sinnbild, Kästchen bzw. Schemasymbol der Familie: WC 4.375, Waschtisch 2.625, Dusche 2.5, Kästchen 2.25 …), höchstens
//            y0 + Endgerätehöhe 42 (die frühere Lage). Abgangsknoten gleichmässig zwischen Etagenleitung und Unterkante des Endgeräts. Steig-
//            und Etagenknoten (Fallleitungs-Abgänge) und Wohnungsblöcke bleiben, wo sie sind; das Kennzeichen steht weiter über dem Symbol.
//            Anschlusslinien: eine Linie von einem Knoten einer waagrechten Leitung (nicht unter einem Strang, nicht im Band) direkt zu einem
//            Endgerät der Zeile mit anderem x läuft senkrecht, waagrecht auf y = oberste Spur + (t + 1) · Anschlussstufe 3 mm, senkrecht zum
//            Gerät; sich in x überdeckende liegen übereinander (t wie die Einlage-Spuren nach Anfang, Ende, Kennzeichen, Spurlücke 3 mm).
//            Eine Linie von einem Gerät auf dem Boden zu einem Knoten im Band (keine Einlage) läuft senkrecht ins Band, dort waagrecht.
//   Abläufe  Bodenablauf und Dachablauf (der Katalog führt keinen Terrasseneinlauf; Terrassen- und Dacheinläufe = Dachablauf) ohne Hand-
//            Lage sitzen im Deckenband: Bodenablauf im Band unter seiner Zeile, Dachablauf im Band darüber (Decke bzw. Dach seines
//            Geschosses; liegt sein Punkt näher an der Kote als an Kote + Höhe seines Geschosses, im Band darunter). x wie ein Endgerät (Platz
//            in der Zelle); Körper = Quadrat 2.4 mm (Ablaufgrösse = Bandhöhe), Oberkante bündig mit der Bandoberkante, y = Oberkante − 1.2
//            (= Spur 0). Einlagezüge desselben Bands laufen auf Spur 0 nicht durch einen fremden Ablauf (± 1.2 + Spurlücke 3), sonst eine
//            Spur tiefer – das Band wächst dann wie bei den Einlagen. Linien vom Ablauf (keine Einlage, nicht unter dem Band): senkrecht aus
//            dem Ablauf – nach oben bis auf die Höhe des anderen Endes, nach unten bis 2 mm (Ablaufstich) unter das Band –, dann waagrecht
//            und senkrecht zum anderen Ende.
//   Schächte Klassen Kontrollschacht, Pumpenschacht, Abscheider, Hebeanlage (die der Katalog führt, samt abgeleiteten; heute nur
//            Kontrollschacht) ohne Hand-Lage stehen im Schnitt unter dem untersten Band, nicht in der Reihe der Plätze. Gruppe nach der Lage
//            im Gebäude: die letzte Gruppe mit Lage, deren Lage quer zur Blickrichtung (auf mm gerundet) nicht rechts des Schachts liegt,
//            sonst die erste – ein Schacht ausserhalb des Gebäudes steht so rechts bzw. links nach seiner Lage –; ohne Lage die Gruppe seines
//            Strangs. In der Gruppe nach Lage (quer, Tiefe), dann Kennzeichen: x = Bündelrand + 15 + Platz · 13 (Schachtabstand); die Gruppe
//            wird breit genug. Rechteck 8 mm breit von der Oberkante des untersten Bands bis zur Unterkante = die tiefere von Unterkante
//            Band 0 − max(12, Länge des Kennzeichens + 2) und tiefster Spur seiner Einläufe − 2 (Sohlenrand). Linien zu einem Schacht laufen
//            unter dem untersten Band und enden seitlich an der Wand zum anderen Ende hin; steht das andere Ende über dem Schacht (|dx| <
//            3.5) und ist die Linie keine Leitung im Erdreich, kommt sie senkrecht von oben bis zur Sohle (Unterkante + 2); ebenso eine
//            Einlage, die in einem Schacht endet (bei Mitte − 2). Schächte und Elemente im Erdreich ohne Geschoss gehören zum untersten
//            Geschoss (wie der Grundleitungsplan in Verlegung.cs).
//   Grundleitungen  Eine Schemalinie liegt unter dem untersten Band, wenn mehr als die Hälfte ihrer Leitungslänge (Geometrie; ohne je
//            Leitung 1) Verlegung erdreich hat (Leitungen/Verlegung.cs), wenn sie in einem Schacht endet oder wenn ein Ende ein Knoten im
//            Erdreich ist (Einlagen bleiben im Deckenband). Knoten auf waagrechten Strängen und Etagenknoten (geteilt, ohne Endgerät,
//            Anschlussarmatur) mit Verlegung erdreich ohne Hand-Lage stehen dort (ein waagrechter Strang, dessen Knoten in einer Zeile alle
//            im Erdreich liegen, belegt in der Zeile keine Spur): x unter dem Knoten einer Zeile, mit dem sie direkt
//            verbunden sind (höchstens die Ankerweite 1000 mm daneben im Grundriss), sonst unter dem Platz stromauf (Endgerät, Ablauf; über
//            Knoten im Erdreich), der dem Abfluss (erster Schacht bzw. Knoten einer Zeile stromab) am nächsten liegt – wie die geteilten
//            Etagenknoten; liegt dort schon ein Knoten im Erdreich stromauf, den Etagenschritt 6 weiter zum Abfluss –, sonst das x ihrer Spur;
//            nach Fliessrang. Spuren: y = Unterkante Band 0 − 3 − t · 4; je Linie ihre x-Ausdehnung (Enden; am Schacht die Wand; offenes Ende
//            ± Offenlänge); nach Stufe, Anfang, Ende, Kennzeichen auf die oberste freie Spur, wobei Linien desselben Zugs (über Knoten im
//            Erdreich verbunden) aneinanderstossen dürfen und fremde 3 mm Lücke halten. Eine Linie, die unter einem fremden Schacht
//            durchläuft, liegt mindestens 1.5 mm (Querabstand) unter dessen Unterkante: erst die Linien der Schächte, dann die querenden
//            (Stufe). Ein Knoten steht auf der Spur seiner abgehenden Linie (sonst der obersten seiner Linien). Linienführung: senkrecht
//            hinunter, waagrecht auf der Spur, senkrecht bzw. seitlich zum anderen Ende. Höhe der Fläche unter dem Band = max(3 + (n − 1) ·
//            4, tiefste Unterkante der Schächte) + 3 (Rand); die Zone «ohne Geschoss» rückt um sie nach unten. Ohne Schächte und Leitungen im
//            Erdreich gibt es die Fläche nicht.
//   Ohne Endgeräte, Abläufe, Schächte und Leitungen im Erdreich bleibt das Layout wie in B8c.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Raster des Strangschemas in Papier-mm. ANNAHME (Prüfliste Christian): alle Werte sind Darstellungsannahmen,
/// keine Normwerte; gewählt für Schrift 1.6–2.2 mm und Symbole von etwa 6 mm.</summary>
public static class SchemaRaster
{
    public const double RandLinks = 30;          // Geschossname, Kennzeichen und Kote links innerhalb des Schemarahmens
    public const double Spaltenbreite = 35;      // Raster der Gruppenbreite (eine Gruppe wächst in ganzen Vielfachen davon)
    public const double Zonenluecke = 10;        // Lücke vor den Zonen «ohne Strang» bzw. «ohne System»
    public const double Strangversatz = 10;      // erste Stranglinie ab dem linken Gruppenrand
    public const double Buendelabstand = 3;      // Stranglinien eines Bündels (gleiche Steigzone)
    public const double RandRechts = 5;          // Platz rechts in der Gruppe
    public const double Zeilenhoehe = 50;        // ein Geschoss
    public const double Zonenabstand = 10;       // Lücke über der Zone «ohne Geschoss»
    public const double Deckenband = 2.4;        // schraffiertes Deckenband auf jeder Geschossgrenze (Mitte auf y0)
    public const double SpurUnten = 4;           // erste waagrechte Spur über der Geschossgrenze
    public const double Spurabstand = 4;
    public const double KnotenUnten = 20;        // erster Knoten auf einer Stranglinie über der Geschossgrenze
    public const double KnotenOben = 14;         // Knoten auf der Stranglinie höchstens bis y0 + 36: unter den Endgeräten
    public const double Knotenschritt = 6;       // Knoten derselben Stranglinie (ein Strang allein)
    public const double Buendelstufe = 3.5;      // kleinste Stufe im Bündel (Anschlusslinien mit Armaturen nebeneinander)
    public const double Endgeraetehoehe = 42;    // Endgeräte über der Geschossgrenze, über allen Knoten der Stranglinie
    public const double Endgeraetversatz = 15;   // erstes Endgerät rechts der letzten Stranglinie
    public const double Endgeraetabstand = 13;
    public const double Anschlussversatz = 1.5;  // Linien verschiedener Bündelstränge am Endgerät nebeneinander
    public const double Etagenschritt = 6;       // Etagenknoten ohne Endgerät dahinter
    public const double Waagrechtschritt = 10;   // Knoten auf einer waagrechten Leitung ab der eigenen Linie
    public const double Ankerversatz = 5;        // weitere Knoten unter derselben Stranglinie
    public const double Achsraster = 50;         // Modell-mm: Raster, in dem die Steigachse eines Strangs gesucht wird
    public const double Ankerweite = 1000;       // Modell-mm: Knoten waagrechter Stränge höchstens so weit neben dem Steigstrang stehen darunter
    public const double Offenlaenge = 8;         // Linie bis zu einem offenen Ende
    public const double Freilaenge = 10;         // freier Zug (ohne Knoten)
    public const double KreisRadius = 4.5;       // kleinster Radius des Strangkreises (wächst mit der Bezeichnung)
    public const double KreisSchrift = 2.2;
    public const double Kopfabstand = 2;         // Lücke zwischen oberstem Deckenband und den Kreisen
    public const double Blockbreite = 38;        // App. · Stk. · LU · KW · WW (LU/KW/WW mit Modul 4) und Ränder
    public const double Blockunten = 17;         // Block über der Zone der waagrechten Spuren
    public const double Blockoben = 4;
    public const double Armaturschritt = 7;      // Anschlussarmaturen vor einem Block
    public const double Daemmhoehe = 15;         // Dämmungstext über der Geschossgrenze, unter den Strangknoten
    public const double Daemmabstand = 3;        // weitere Dämmungstexte darunter
    public const double Schrift = 1.6;           // Dämmungstext, Block-Tabelle
    public const double Einlagespur = 1.5;       // Abstand der Einlage-Spuren im Deckenband (Nachtrag B8c)
    public const double Bandrand = 1.2;          // Einlage-Spur bis Rand des Deckenbands (eine Spur liegt in der Mitte von 2.4)
    public const double Spurluecke = 3;          // kleinster x-Abstand zweier Einlagezüge auf derselben Spur
    // Nachtrag B8d: Apparate auf dem Boden, Abläufe im Band, Schächte im Schnitt, Grundleitungen unter dem untersten Band
    public const double Bodenabstand = 3;        // Unterkante Endgerät über der obersten Etagen- bzw. Anschlussspur der Zeile
    public const double Abgangschritt = 5;       // je Knoten auf dem Abgang zum Endgerät (Armatur) so viel Platz unter dem Endgerät
    public const double Anschlussstufe = 3;      // Anschlusslinien von waagrechten Leitungen zu Endgeräten über der obersten Spur
    public const double Ablaufgroesse = 2.4;     // Kante des Ablauf-Quadrats im Band (= Deckenband, wie im Vorbild)
    public const double Ablaufstich = 2;         // Linie von einem Ablauf nach unten: Knick so weit unter dem Band
    public const double Untergrundoben = 3;      // erste Grundleitungsspur unter der Unterkante des untersten Bands
    public const double Untergrundspur = 4;      // Abstand der Grundleitungsspuren
    public const double Untergrundrand = 3;      // Platz unter der tiefsten Spur bzw. Schachtsohle
    public const double Schachtbreite = 8;       // Rechteck des Schachts (Schachtwand)
    public const double Schachttiefe = 12;       // Schacht mindestens so weit unter die Unterkante des untersten Bands
    public const double Schachtabstand = 13;     // Schächte nebeneinander unter einer Gruppe
    public const double Sohlenrand = 2;          // Schachtsohle (Unterkante) unter dem tiefsten Einlauf
    public const double Querabstand = 1.5;       // Grundleitung, die einen Schacht unterquert, so weit unter seiner Unterkante
    public const double SchachtSchrift = 2.0;    // Kennzeichen senkrecht im Schacht

    public static JsonObject AlsJson() => new()
    {
        ["einlagespur"] = Einlagespur, ["bandrand"] = Bandrand, ["spurluecke"] = Spurluecke,
        ["bodenabstand"] = Bodenabstand, ["abgangschritt"] = Abgangschritt, ["anschlussstufe"] = Anschlussstufe,
        ["ablaufgroesse"] = Ablaufgroesse, ["ablaufstich"] = Ablaufstich, ["untergrundoben"] = Untergrundoben,
        ["untergrundspur"] = Untergrundspur, ["untergrundrand"] = Untergrundrand, ["schachtbreite"] = Schachtbreite,
        ["schachttiefe"] = Schachttiefe, ["schachtabstand"] = Schachtabstand, ["sohlenrand"] = Sohlenrand, ["querabstand"] = Querabstand,
        ["einheit"] = "Papier-mm, y nach oben",
        ["randLinks"] = RandLinks, ["spaltenbreite"] = Spaltenbreite, ["zonenluecke"] = Zonenluecke, ["strangversatz"] = Strangversatz,
        ["buendelabstand"] = Buendelabstand, ["zeilenhoehe"] = Zeilenhoehe, ["zonenabstand"] = Zonenabstand, ["deckenband"] = Deckenband,
        ["spurUnten"] = SpurUnten, ["spurabstand"] = Spurabstand, ["knotenUnten"] = KnotenUnten, ["knotenschritt"] = Knotenschritt,
        ["buendelstufe"] = Buendelstufe, ["endgeraetehoehe"] = Endgeraetehoehe, ["endgeraetversatz"] = Endgeraetversatz,
        ["endgeraetabstand"] = Endgeraetabstand, ["anschlussversatz"] = Anschlussversatz, ["waagrechtschritt"] = Waagrechtschritt,
        ["ankerversatz"] = Ankerversatz, ["offenlaenge"] = Offenlaenge, ["blockbreite"] = Blockbreite, ["blockunten"] = Blockunten,
        ["blockoben"] = Blockoben, ["armaturschritt"] = Armaturschritt, ["daemmhoehe"] = Daemmhoehe,
    };

    /// <summary>Geschätzte Breite eines Texts in Papier-mm (0.6 × Schrifthöhe je Zeichen; für Platz und Kreisgrösse).</summary>
    public static double TextBreite(string text, double groesse) => 0.6 * groesse * text.Length;
}

/// <summary>Eine Gruppe des Strangschemas: Bündel der Stränge einer Steigzone, ein einzelner Strang oder eine Zone «ohne Strang»
/// bzw. «ohne System».</summary>
public sealed class SchemaGruppe
{
    public int Index { get; internal set; }
    /// <summary>straenge | ohneStrang | ohneSystem</summary>
    public required string Art { get; init; }
    /// <summary>Steigzone (Strang.steigzone) des Bündels; null = einzelner Strang bzw. Zone.</summary>
    public string? Steigzone { get; init; }
    /// <summary>System der Zone «ohne Strang».</summary>
    public Element? System { get; init; }
    /// <summary>Stranglinien bzw. die Spalte der Zone, im Bündel von links nach rechts.</summary>
    public List<SchemaSpalte> Spalten { get; } = [];
    public IEnumerable<SchemaSpalte> Senkrecht => Spalten.Where(s => s.Art == "senkrecht");
    public double X0 { get; internal set; }
    public double Breite { get; internal set; } = SchemaRaster.Spaltenbreite;
    /// <summary>Lage im Gebäude (Grundriss x, y in mm); null = nichts platziert.</summary>
    public double[]? Lage { get; internal set; }
    public bool IstZone => Art != "straenge";
    /// <summary>Bezeichnung im Kreis: Steigzone, sonst Kennzeichen des Strangs; Zonen: Titel.</summary>
    public string Bezeichnung => Steigzone ?? (IstZone ? Spalten[0].Titel : Spalten[0].Strang!.Kennzeichen);
    /// <summary>Kreis über der obersten Zeile (Gruppen mit senkrechten Strängen).</summary>
    public bool MitKreis => !IstZone && Spalten.Any(s => s.Art == "senkrecht");
    public double KreisX { get; internal set; }
    public double KreisY { get; internal set; }
    public double KreisRadius { get; internal set; }
    /// <summary>x der letzten senkrechten Stranglinie (bzw. der ersten Linie ohne senkrechte), Bezug der Endgeräte.</summary>
    public double BuendelRechts => Senkrecht.Any() ? Senkrecht.Max(s => s.LinieX) : X0 + SchemaRaster.Strangversatz;
}

/// <summary>Eine Stranglinie des Strangschemas (bzw. die Spalte einer Zone); x0 und Breite sind die ihrer Gruppe.</summary>
public sealed class SchemaSpalte
{
    public int Index { get; internal set; }
    /// <summary>senkrecht | waagrecht (Stranglinien) | ohneStrang | ohneSystem (Zonen)</summary>
    public required string Art { get; init; }
    public Element? Strang { get; init; }
    public Element? System { get; init; }
    public SchemaGruppe Gruppe { get; internal set; } = null!;
    /// <summary>Platz in der Gruppe (0 = links).</summary>
    public int Platz { get; internal set; }
    /// <summary>Lage des Strangs im Gebäude (Grundriss x, y in mm); null = nicht platziert.</summary>
    public double[]? Lage { get; internal set; }
    public double X0 => Gruppe.X0;
    public double Breite => Gruppe.Breite;
    public double LinieX { get; internal set; }
    public bool IstZone => Art is "ohneStrang" or "ohneSystem";
    public string Titel => Art switch
    {
        "ohneStrang" => $"{System?.Kennzeichen} ohne Strang",
        "ohneSystem" => "ohne System",
        _ => Strang!.Kennzeichen,
    };
}

/// <summary>Eine Zeile des Strangschemas (Geschoss bzw. Zone «ohne Geschoss»).</summary>
public sealed class SchemaZeile
{
    public int Index { get; internal set; }
    public Element? Geschoss { get; init; }
    public double Y0 { get; internal set; }
    public double Hoehe => SchemaRaster.Zeilenhoehe;
    public bool IstZone => Geschoss is null;
    public string Titel => Geschoss?.Kennzeichen ?? "ohne Geschoss";
}

/// <summary>Ein Deckenband des Strangschemas (Nachtrag B8c): Band 0 unter der untersten Zeile (Bodenplatte), Band i zwischen den
/// Zeilen i−1 und i (Decke über dem Geschoss der Zeile i−1), Band n über der obersten Zeile; mit den Einlage-Spuren darin.</summary>
public sealed class SchemaBand
{
    public int Index { get; init; }
    /// <summary>Zeile unter dem Band (Geschoss, dessen Decke es ist); null = Band 0 (Bodenplatte).</summary>
    public SchemaZeile? Unter { get; init; }
    /// <summary>Zeile über dem Band; null = oberstes Band.</summary>
    public SchemaZeile? Ueber { get; init; }
    public double Mitte { get; internal set; }
    public double Hoehe { get; internal set; } = SchemaRaster.Deckenband;
    public double Unten => Mitte - Hoehe / 2;
    public double Oben => Mitte + Hoehe / 2;
    /// <summary>Einlage-Spuren von oben nach unten, je Spur die Stränge (bzw. Medien) der Einlagezüge darauf, z. B. «SW-1, SW-2».</summary>
    public List<string> Spuren { get; } = [];
    /// <summary>y der Spur t (0 = oberste); ohne Spuren die Mitte.</summary>
    public double SpurY(int t)
    {
        int n = Spuren.Count;
        return n == 0 ? Mitte : Math.Round(Mitte + ((n - 1) / 2.0 - Math.Clamp(t, 0, n - 1)) * SchemaRaster.Einlagespur, 3);
    }
    /// <summary>Bezeichnung: «Bodenplatte», «Decke über UG», «Dach über 2.OG».</summary>
    public string Titel => Unter is null ? "Bodenplatte" : Ueber is null ? $"Decke über {Unter.Titel} (oberste)" : $"Decke über {Unter.Titel}";
}

/// <summary>Schacht im Schnitt (Nachtrag B8d): Rechteck (Schachtwand) von der Oberkante des untersten Bands nach unten unter das Band,
/// oben mit Deckelstrich, Kennzeichen senkrecht darin; Ein- und Ausläufe seitlich auf Sohlenhöhe bzw. von oben bis zur Sohle.</summary>
public sealed class SchemaSchacht
{
    public required SchemaKnoten Knoten { get; init; }
    /// <summary>Gruppe, unter der der Schacht nach seiner Lage im Gebäude steht, und sein Platz darin (0 = links).</summary>
    public SchemaGruppe Gruppe { get; internal set; } = null!;
    public int Platz { get; internal set; }
    public double X { get; internal set; }
    public double Breite => SchemaRaster.Schachtbreite;
    public double Links => X - Breite / 2;
    public double Rechts => X + Breite / 2;
    /// <summary>Oberkante (Deckel, = Oberkante des untersten Bands) und Unterkante.</summary>
    public double Oben { get; internal set; }
    public double Unten { get; internal set; }
    /// <summary>Sohlenhöhe: Einläufe von oben enden hier im Rechteck.</summary>
    public double Sohle => Math.Round(Unten + SchemaRaster.Sohlenrand, 3);
    /// <summary>Eine Linie kommt von oben in den Schacht (das Kennzeichen steht dann in der rechten Hälfte).</summary>
    public bool VonOben { get; internal set; }
    /// <summary>Unterkante relativ zur Unterkante des untersten Bands (negativ).</summary>
    internal double UntenRel { get; set; }
}

/// <summary>Fläche unter dem untersten Band (Nachtrag B8d): Grundleitungsspuren (Leitungen im Erdreich und Linien zu Schächten) und
/// Schächte im Schnitt. Nur vorhanden, wenn das Schema einen Schacht oder eine Leitung im Erdreich zeigt.</summary>
public sealed class SchemaUntergrund
{
    /// <summary>Oberkante = Unterkante des untersten Bands.</summary>
    public double Oben { get; internal set; }
    public double Hoehe { get; internal set; }
    public double Unten => Oben - Hoehe;
    /// <summary>Grundleitungsspuren von oben nach unten, je Spur die Stränge (bzw. Medien) der Züge darauf.</summary>
    public List<string> Spuren { get; } = [];
    public List<SchemaSchacht> Schaechte { get; } = [];
    /// <summary>y der Spur t (0 = oberste).</summary>
    public double SpurY(int t) => Math.Round(Oben - SchemaRaster.Untergrundoben - Math.Max(0, t) * SchemaRaster.Untergrundspur, 3);
}

/// <summary>Zeile der Apparatetabelle eines Wohnungsblocks. LU, KW und WW füllt Modul 4 (Werte nach SVGW W3 mit Quelle);
/// bis dahin null und nicht gezeigt.</summary>
public sealed class SchemaBlockZeile
{
    public required string Klasse { get; init; }
    public required string Kuerzel { get; init; }
    public List<Element> Elemente { get; } = [];
    public int Stueck => Elemente.Count;
    public double? Lu { get; set; }
    public double? Kw { get; set; }
    public double? Ww { get; set; }
}

/// <summary>Anschlusslinie eines Wohnungsblocks: Wurzel (Steigknoten), Höhe und die Armaturen am Eintritt in den Block.</summary>
public sealed class SchemaBlockAnschluss
{
    public required SchemaKnoten Wurzel { get; init; }
    public Element? Strang => Wurzel.Strang ?? Wurzel.Spalte?.Strang;
    public double Y { get; internal set; }
    public List<SchemaKnoten> Armaturen { get; } = [];
}

/// <summary>Wohnungsblock: Endgeräte einer Zone (Wohnung) je Gruppe und Geschoss als Rechteck mit Apparatetabelle.</summary>
public sealed class SchemaBlock
{
    public required Element Zone { get; init; }
    public required SchemaGruppe Gruppe { get; init; }
    public required SchemaZeile Zeile { get; init; }
    public List<SchemaKnoten> Endgeraete { get; } = [];
    public List<SchemaBlockZeile> Zeilen { get; } = [];
    public List<SchemaBlockAnschluss> Anschluesse { get; } = [];
    public string Titel => Zone.Name is { Length: > 0 } n ? n : Zone.Kennzeichen;
    public int Stueck => Zeilen.Sum(z => z.Stueck);
    /// <summary>Summen LU, KW, WW (Modul 4).</summary>
    public double? Lu { get; set; }
    public double? Kw { get; set; }
    public double? Ww { get; set; }
    /// <summary>Zahl der Armaturplätze vor dem Block (grösste Zahl Anschlussarmaturen einer Anschlusslinie).</summary>
    public int Armaturplaetze { get; internal set; }
    /// <summary>Eintritt in den Platz (x eines Endgeräts an dieser Stelle): Anker für geteilte Etagenknoten.</summary>
    public double EintrittX { get; internal set; }
    /// <summary>Unten links, Breite, Höhe des Rechtecks.</summary>
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Breite { get; internal set; } = SchemaRaster.Blockbreite;
    public double Hoehe { get; internal set; }
    /// <summary>Zeilenabstand der Tabelle (passt die Tabelle nicht, enger als 2.6).</summary>
    public double Zeilenabstand { get; internal set; } = 2.6;
    /// <summary>Spalten der Tabelle: Name, x ab dem linken Rand, Ausrichtung, gezeigt (LU, KW, WW erst mit Modul 4).</summary>
    public static readonly (string Name, string Titel, double X, string Ausrichtung, bool Gezeigt)[] Spalten =
    [
        ("app", "App.", 1.5, "links", true), ("stk", "Stk.", 14, "rechts", true),
        ("lu", "LU", 21.5, "rechts", false), ("kw", "KW", 29, "rechts", false), ("ww", "WW", 36.5, "rechts", false),
    ];
}

/// <summary>Dämmungstext an einem Strangabschnitt (Gruppe × Geschoss, ein Wert).</summary>
public sealed class SchemaDaemmung
{
    public required SchemaGruppe Gruppe { get; init; }
    public required SchemaZeile Zeile { get; init; }
    public double Dicke { get; init; }
    public List<SchemaSpalte> Spalten { get; } = [];
    public double Y { get; internal set; }
    public double X0 { get; internal set; }
    public double XText { get; internal set; }
    public string Text => "Dämmung " + Dicke.ToString("0.#", CultureInfo.InvariantCulture) + " mm";
}

/// <summary>Das angeordnete Schema einer Ansicht: Graph, Gruppen, Stranglinien, Zeilen, Lagen, Linien, Blöcke.</summary>
public sealed class Schemaplan
{
    public required SchemaGraph Graph { get; init; }
    public Element Ansicht => Graph.Ansicht;
    public string Schemaart { get; init; } = "strangschema";
    /// <summary>sued | nord | ost | west (Ansicht.blickrichtung, ohne Angabe sued).</summary>
    public string Blickrichtung { get; internal set; } = "sued";
    public bool Wohnungsbloecke { get; internal set; }
    public List<SchemaGruppe> Gruppen { get; } = [];
    /// <summary>Stranglinien und Zonen in Gruppenreihenfolge.</summary>
    public List<SchemaSpalte> Spalten { get; } = [];
    public List<SchemaZeile> Zeilen { get; } = [];
    public List<SchemaBlock> Bloecke { get; } = [];
    public List<SchemaDaemmung> Daemmungen { get; } = [];
    /// <summary>Deckenbänder von unten (Band 0 = Bodenplatte) bis über die oberste Zeile (Nachtrag B8c); leer ohne Geschoss.</summary>
    public List<SchemaBand> Baender { get; } = [];
    /// <summary>Verlegung der Leitungen und Formteile, aus der die Einlagen stammen (null, wenn das Schema nicht auf dem Modell des
    /// Projekts gerechnet wird).</summary>
    public Verlegungsstand? Verlegung { get; internal set; }
    /// <summary>Fläche unter dem untersten Band mit Grundleitungen und Schächten (Nachtrag B8d); null, wenn es keine gibt.</summary>
    public SchemaUntergrund? Untergrund { get; internal set; }
    public List<string> Hinweise { get; } = [];
    /// <summary>Hand-Lagen, deren Element nicht (mehr) im Schema steht (schema_anordnen räumt sie auf).</summary>
    public List<string> VerwaisteLagen { get; } = [];
    /// <summary>Oberkante des Kopfs (Kreise, Titel) über der obersten Zeile.</summary>
    public double KopfOben { get; internal set; }

    static readonly string[] Waagrecht = ["grundleitung", "sammelleitung", "verteilleitung", "anschlussleitung"];
    public static readonly string[] Blickrichtungen = ["sued", "nord", "ost", "west"];

    public static bool IstWaagrecht(Element strang) => strang.Text("art") is { } a && Waagrecht.Contains(a);

    /// <summary>Klassen, die im Strangschema als Schacht im Schnitt erscheinen (Nachtrag B8d). Im Katalog gibt es heute nur
    /// Kontrollschacht; die übrigen greifen, sobald der Katalog sie führt (samt abgeleiteten Klassen).</summary>
    public static readonly string[] SchachtKlassen = ["Kontrollschacht", "Pumpenschacht", "Abscheider", "Hebeanlage"];

    /// <summary>Klassen, deren Körper im Deckenband sitzt (Nachtrag B8d): Bodenablauf im Band unter, Dachablauf im Band über seinem
    /// Geschoss. Eine eigene Klasse Terrasseneinlauf führt der Katalog nicht (Terrassen- und Dacheinläufe = Dachablauf).</summary>
    public static readonly string[] AblaufKlassen = ["Bodenablauf", "Dachablauf"];

    public static bool IstSchacht(Katalog k, Element e) => SchachtKlassen.Any(c => k.Klassen.ContainsKey(c) && k.IstArt(e, c));
    public static bool IstAblauf(Katalog k, Element e) => AblaufKlassen.Any(c => k.Klassen.ContainsKey(c) && k.IstArt(e, c));

    /// <summary>Schema einer Ansicht (Kennzeichen oder Id) berechnen.</summary>
    public static Schemaplan Rechnen(Projekt p, string ansicht) => Rechnen(p, p.Modell.Hole(ansicht, "Ansicht"));

    public static Schemaplan Rechnen(Projekt p, Element ansicht)
    {
        var g = SchemaGraph.Bilden(p, ansicht);
        var plan = new Schemaplan { Graph = g, Schemaart = ansicht.Text("schemaart") ?? "strangschema" };
        if (plan.Schemaart != "strangschema")
            plan.Hinweise.Add($"Schemaart {plan.Schemaart} folgt; angeordnet wird wie im Strangschema.");
        new Anordnung(plan).Ausfuehren();
        return plan;
    }

    /// <summary>Hand-Lagen der Ansicht: elementId → (x, y). Einträge mit manuell = false oder ohne Zahlen zählen nicht.</summary>
    public static Dictionary<string, (double X, double Y)> Lagen(Element ansicht)
    {
        var d = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
        if (ansicht.Eigenschaft("lagen")?.Inhalt is not JsonObject o) return d;
        foreach (var (id, n) in o)
        {
            if (n is not JsonObject l || Json.Zahl(l["x"]) is not double x || Json.Zahl(l["y"]) is not double y) continue;
            if (l["manuell"] is JsonValue mv && mv.TryGetValue<bool>(out var manuell) && !manuell) continue;
            d[id] = (x, y);
        }
        return d;
    }

    /// <summary>Netzabstand: Leitungslänge (mm) und Schritte; verglichen zuerst nach Länge, dann nach Schritten.</summary>
    readonly record struct Abstand(double Laenge, int Schritte) : IComparable<Abstand>
    {
        public int CompareTo(Abstand o) { int c = Laenge.CompareTo(o.Laenge); return c != 0 ? c : Schritte.CompareTo(o.Schritte); }
        public static Abstand operator +(Abstand a, Abstand b) => new(a.Laenge + b.Laenge, a.Schritte + b.Schritte);
    }

    /// <summary>Rechnet Gruppen, Stranglinien, Zeilen, Lagen, Linien, Blöcke und Dämmung.</summary>
    sealed class Anordnung(Schemaplan plan)
    {
        readonly SchemaGraph g = plan.Graph;
        Modell M => g.Modell;
        Katalog K => g.Katalog;

        /// <summary>Weg im Netz einer Zelle ab einer Wurzel (Steigknoten bzw. Knoten eines waagrechten Strangs).</summary>
        sealed class Weg
        {
            public Abstand Abstand { get; init; }
            public SchemaKnoten? Vor { get; init; }
            public required SchemaKnoten Wurzel { get; init; }
            public int Kinder { get; set; }
        }

        sealed class Zelle
        {
            public required SchemaGruppe Gruppe { get; init; }
            public required SchemaZeile Zeile { get; init; }
            public List<SchemaKnoten> Strang { get; } = [];   // Knoten der senkrechten Stränge der Gruppe
            public List<SchemaKnoten> Spur { get; } = [];     // Knoten der waagrechten Stränge der Gruppe
            public List<object> Seite { get; } = [];          // SchemaKnoten (Endgeräte, Geräte, Zonen) oder SchemaLinie (freier Zug)
            public HashSet<SchemaKnoten> Steig { get; } = [];
            /// <summary>Netzwege je Stranglinie: Baum ab den Wurzeln dieses Strangs über seine Etagenknoten zu den Geräten.</summary>
            public Dictionary<SchemaSpalte, Dictionary<SchemaKnoten, Weg>> Baeume { get; } = [];
            /// <summary>Stranglinien für die Reihenfolge der Plätze: die mit den meisten erreichten Geräten zuerst, dann Platz.</summary>
            public List<SchemaSpalte> Strangfolge { get; } = [];
            /// <summary>Weg eines Etagen- bzw. Steigknotens in seinem eigenen Baum.</summary>
            public Weg? WegVon(SchemaKnoten k) => k.Spalte is { } s && Baeume.TryGetValue(s, out var b) && b.TryGetValue(k, out var w) ? w : null;
            public Dictionary<SchemaKnoten, int> Stufe { get; } = [];      // Platz eines Steigknotens auf seiner Stranglinie (j)
            public Dictionary<SchemaKnoten, string> Rolle { get; } = [];   // Etagenknoten: geteilt | abgang | anschluss | innen | tot
            public Dictionary<SchemaKnoten, HashSet<object>> Plaetze { get; } = [];   // Plätze (Endgerät bzw. Block) hinter einem Knoten
            public List<object> Folge { get; } = [];          // Plätze von links: SchemaKnoten | SchemaBlock | SchemaLinie
        }

        readonly Dictionary<(int, int), Zelle> zellen = [];
        readonly Dictionary<string, List<Element>> kinder = new(StringComparer.Ordinal);
        readonly Dictionary<string, SchemaSpalte> strangSpalte = new(StringComparer.Ordinal);
        readonly Dictionary<string, List<SchemaLinie>> linienAn = new(StringComparer.Ordinal);
        readonly Dictionary<string, SchemaSpalte> anker = new(StringComparer.Ordinal);

        Zelle ZelleVon(SchemaGruppe gr, SchemaZeile z) =>
            zellen.TryGetValue((gr.Index, z.Index), out var c) ? c : zellen[(gr.Index, z.Index)] = new Zelle { Gruppe = gr, Zeile = z };

        IEnumerable<Zelle> Zellen => zellen.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2).Select(x => x.Value);

        Element? SystemVonStrang(Element s) =>
            M.Finde(s.TeilVon) is { Klasse: "System" } sy ? sy : M.Finde(s.System) is { Klasse: "System" } sy2 ? sy2 : null;

        static bool IstGeraet(SchemaKnoten k) => k.Art is "endgeraet" or "geraet";

        public void Ausfuehren()
        {
            foreach (var e in M.Elemente)
                if (e.TeilVon is { } t) (kinder.TryGetValue(t, out var l) ? l : kinder[t] = []).Add(e);
            plan.Blickrichtung = plan.Ansicht.Text("blickrichtung") is { } b && Blickrichtungen.Contains(b) ? b : "sued";
            plan.Wohnungsbloecke = Grafik.Bool(plan.Ansicht, "wohnungsbloecke");

            // 1. Stränge: sichtbare Systeme und alle Stränge, auf die sichtbare Elemente zeigen
            var straenge = new Dictionary<string, Element>(StringComparer.Ordinal);
            foreach (var s in M.DerKlasse("Strang"))
                if (SystemVonStrang(s) is { } sy && g.SystemSichtbar(sy)) straenge[s.Id] = s;
            foreach (var e in g.Sichtbare)
                if (g.StrangVon(e) is { } st) straenge.TryAdd(st.Id, st);

            // 2. Gruppen (Steigzone) nach Lage im Gebäude, Bündel nach realer Lage
            foreach (var gr in Gruppieren(straenge.Values)) Dazu(gr);
            var strangRang = strangSpalte.ToDictionary(x => x.Key, x => x.Value.Index, StringComparer.Ordinal);

            // 3. Stranglinie je Knoten: eigener Strang, nächster Strang über das Netz (Geräte) oder Zone des Systems
            var naechster = NaechsterStrang(strangRang);
            var knotenSpalte = new Dictionary<string, SchemaSpalte>(StringComparer.Ordinal);
            var knotenZone = new Dictionary<string, Element?>(StringComparer.Ordinal);
            foreach (var kn in g.Knoten)
            {
                SchemaSpalte? sp = kn.Strang is { } s && strangSpalte.TryGetValue(s.Id, out var x) ? x : null;
                if (sp is null && IstGeraet(kn) && naechster.TryGetValue(kn.Id, out var ri)) sp = plan.Spalten[ri];
                if (sp is not null) knotenSpalte[kn.Id] = sp;
                else knotenZone[kn.Id] = ZonenSystem(kn.Element);
            }
            var freieZone = new Dictionary<SchemaLinie, (SchemaSpalte? Spalte, Element? System)>();
            foreach (var l in g.Linien.Where(l => l.Frei))
            {
                var e = l.Elemente[0];
                freieZone[l] = g.StrangVon(e) is { } st && strangSpalte.TryGetValue(st.Id, out var sp) ? (sp, null) : (null, ZonenSystem(e));
            }

            // 4. Zonen am Schluss: «ohne Strang» je System, dann «ohne System»
            var zonenSysteme = knotenZone.Values.Concat(freieZone.Values.Where(x => x.Spalte is null).Select(x => x.System)).ToList();
            var zonenSpalte = new Dictionary<string, SchemaSpalte>(StringComparer.Ordinal);
            foreach (var sy in zonenSysteme.OfType<Element>().DistinctBy(s => s.Id).OrderBy(s => s, SchemaGraph.Ordnung))
            {
                var gr = new SchemaGruppe { Art = "ohneStrang", System = sy };
                gr.Spalten.Add(new SchemaSpalte { Art = "ohneStrang", System = sy, Gruppe = gr });
                Dazu(gr); zonenSpalte[sy.Id] = gr.Spalten[0];
            }
            if (zonenSysteme.Any(z => z is null))
            {
                var gr = new SchemaGruppe { Art = "ohneSystem" };
                gr.Spalten.Add(new SchemaSpalte { Art = "ohneSystem", Gruppe = gr });
                Dazu(gr); zonenSpalte[""] = gr.Spalten[0];
            }

            // 5. Zeilen
            var geschosse = M.DerKlasse("Geschoss").OrderBy(x => x.Zahl("kote") ?? 0).ThenBy(x => x, SchemaGraph.Ordnung).ToList();
            var zeileVon = new Dictionary<string, SchemaZeile>(StringComparer.Ordinal);
            foreach (var ge in geschosse)
            {
                var z = new SchemaZeile { Geschoss = ge, Index = plan.Zeilen.Count, Y0 = plan.Zeilen.Count * SchemaRaster.Zeilenhoehe };
                plan.Zeilen.Add(z); zeileVon[ge.Id] = z;
            }
            SchemaZeile? ohneGeschoss = null;
            // Schächte und Elemente im Erdreich ohne Geschoss gehören zum untersten Geschoss (wie der Grundleitungsplan, Verlegung.cs; B8d)
            var verlegungFrueh = plan.Zeilen.Count > 0 && ReferenceEquals(M, g.Projekt.Modell) ? global::Hlks.Kern.Verlegung.Von(g.Projekt) : null;
            SchemaZeile ZeileVon(Element e)
            {
                if (e.Geschoss is not null && zeileVon.TryGetValue(e.Geschoss, out var z)) return z;
                if (e.Geschoss is null && plan.Zeilen.Count > 0 && plan.Zeilen[0] is { IstZone: false } unterste
                    && (IstSchacht(K, e) || verlegungFrueh?.Befund(e.Id) is { IstErdreich: true }))
                    return unterste;
                if (ohneGeschoss is null)
                {
                    ohneGeschoss = new SchemaZeile { Index = plan.Zeilen.Count, Y0 = -(SchemaRaster.Zeilenhoehe + SchemaRaster.Zonenabstand) };
                    plan.Zeilen.Add(ohneGeschoss);
                }
                return ohneGeschoss;
            }

            // 6. Zellen
            foreach (var kn in g.Knoten)
            {
                kn.Spalte = knotenSpalte.TryGetValue(kn.Id, out var sp) ? sp : zonenSpalte[knotenZone[kn.Id]?.Id ?? ""];
                kn.Zeile = ZeileVon(kn.Element);
                var c = ZelleVon(kn.Spalte.Gruppe, kn.Zeile);
                if (kn.Spalte.IstZone || IstGeraet(kn)) c.Seite.Add(kn);
                else if (kn.Spalte.Art == "senkrecht") c.Strang.Add(kn);
                else c.Spur.Add(kn);
            }
            foreach (var (l, (sp, sy)) in freieZone.OrderBy(x => x.Key.Elemente[0], SchemaGraph.Ordnung))
            {
                l.Spalte = sp ?? zonenSpalte[sy?.Id ?? ""];
                l.Zeile = ZeileVon(l.Elemente[0]);
                ZelleVon(l.Spalte.Gruppe, l.Zeile).Seite.Add(l);
            }
            foreach (var l in g.Linien)
                foreach (var en in new[] { l.Von, l.Nach })
                    if (en.Knoten is { } k) (linienAn.TryGetValue(k.Id, out var li) ? li : linienAn[k.Id] = []).Add(l);

            // 7. Anker der Knoten auf waagrechten Strängen
            var aufStrang = zellen.Values.SelectMany(c => c.Strang).Select(k => k.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var c in Zellen)
                foreach (var kn in c.Spur)
                {
                    SchemaSpalte? beste = null;
                    double besteD = double.MaxValue;
                    foreach (var l in linienAn.GetValueOrDefault(kn.Id) ?? [])
                    {
                        var o = Andere(l, kn);
                        if (o is null || o == kn || !aufStrang.Contains(o.Id) || o.Spalte == kn.Spalte) continue;
                        double d = Ankerabstand(kn, o.Spalte!);
                        if (d > SchemaRaster.Ankerweite) continue;
                        if (beste is null || d < besteD - 1e-6 || (Math.Abs(d - besteD) <= 1e-6 && o.Spalte!.Index < beste.Index)) { beste = o.Spalte; besteD = d; }
                    }
                    if (beste is not null) anker[kn.Id] = beste;
                }

            // 7b. Schächte und Abläufe (Nachtrag B8d): nur mit Geschossen (Bändern) und ohne Hand-Lage; Schächte nach Lage unter eine Gruppe
            if (plan.Zeilen.Any(z => !z.IstZone))
            {
                var hand = Lagen(plan.Ansicht);
                foreach (var kn in g.Knoten.Where(k => !hand.ContainsKey(k.Id)))
                {
                    if (IstSchacht(K, kn.Element)) kn.Schacht = new SchemaSchacht { Knoten = kn };
                    else if (IstAblauf(K, kn.Element)) ablaeufe.Add(kn);
                }
                SchaechteZuordnen();
            }

            // 8. je Zelle: Steig- und Etagenknoten, Netzwege, Reihenfolge der Plätze, Blöcke, Rollen der Etagenknoten
            foreach (var c in Zellen.Where(c => !c.Gruppe.IstZone)) NetzRechnen(c);
            foreach (var c in Zellen.Where(c => !c.Gruppe.IstZone)) Folge(c);
            foreach (var c in Zellen.Where(c => !c.Gruppe.IstZone)) Rollen(c);
            DaemmungSammeln();

            // 8b. Einlagen (Nachtrag B8c): Bänder, Einlage-Linien und -Knoten, Spuren, Höhen; die Zeilen rücken nach
            BaenderBilden(zeileVon);
            // 8c. Abläufe im Band, Leitungen im Erdreich, Linien zu Schächten (Nachtrag B8d)
            UntergrundBilden();

            // 9. Breiten und x der Gruppen, x der Stranglinien
            foreach (var gr in plan.Gruppen) gr.Breite = Breite(gr);
            double x0 = SchemaRaster.RandLinks;
            bool zonen = false;
            foreach (var gr in plan.Gruppen)
            {
                if (gr.IstZone && !zonen) { if (gr.Index > 0) x0 += SchemaRaster.Zonenluecke; zonen = true; }
                gr.X0 = x0;
                x0 += gr.Breite;
                int i = 0;
                foreach (var sp in gr.Spalten)
                    sp.LinieX = sp.Art == "senkrecht" ? gr.X0 + SchemaRaster.Strangversatz + i++ * SchemaRaster.Buendelabstand
                        : sp.Art == "waagrecht" ? gr.X0 + WaagrechtVersatz(gr)
                        : gr.X0 + SchemaRaster.Strangversatz;
            }

            // 10. Lagen: Steigknoten, Plätze, Spuren, Zonen, dann Etagenknoten (brauchen die Plätze aller Gruppen). Einlagen (B8c): die
            //     Spuren im Band folgen aus den x der Knoten; wächst dadurch ein Band, rücken die Zeilen und die Lagen werden neu gerechnet
            //     (x hängt nicht von den Zeilen ab, die Rechnung setzt alle Lagen neu).
            //     Nachtrag B8d: nach den Spuren stehen Endgeräte auf dem Boden ihrer Zeile und Abläufe im Band, Schächte unter ihrer Gruppe;
            //     die Grundleitungsspuren unter dem untersten Band folgen aus den x wie die Einlage-Spuren.
            void LagenSetzen()
            {
                foreach (var c in Zellen)
                {
                    if (c.Gruppe.IstZone) { ZoneSetzen(c); continue; }
                    SteigSetzen(c);
                    PlaetzeSetzen(c);
                }
                SpurenSetzen();
                SchaechteXSetzen();
                BodenSetzen();
                foreach (var c in Zellen.Where(c => !c.Gruppe.IstZone)) EtageSetzen(c);
                UntergrundXSetzen();
            }
            LagenSetzen();
            bool baender = EinlageSpurenSetzen();
            bool untergrund = UntergrundSetzen();
            if (baender || untergrund) LagenSetzen();
            foreach (var kn in g.Knoten) if (kn.Band is not null) kn.Y = einlageY[kn.Id];   // Einlage-Knoten auf ihre Spur im Band (B8c)
            UntergrundLagen();   // Knoten auf ihrer Grundleitungsspur, Schächte (B8d)

            // 11. Hand-Lagen ersetzen die automatischen
            foreach (var (id, (lx, ly)) in Lagen(plan.Ansicht).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (g.NachId.TryGetValue(id, out var kn))
                {
                    kn.X = lx; kn.Y = ly; kn.Manuell = true; kn.Ausrichtung = "frei"; kn.Band = null;
                    kn.Untergrund = false; kn.Unterkante = null;
                }
                else plan.VerwaisteLagen.Add(id);
            }

            // 12. Linien, Kopf, Dämmung
            foreach (var l in g.Linien) Fuehren(l);
            KopfSetzen();
            DaemmungSetzen();

            // 13. Hinweise
            HinweiseSammeln();
        }

        void Dazu(SchemaGruppe gr)
        {
            gr.Index = plan.Gruppen.Count;
            plan.Gruppen.Add(gr);
            for (int i = 0; i < gr.Spalten.Count; i++)
            {
                var sp = gr.Spalten[i];
                sp.Index = plan.Spalten.Count; sp.Platz = i; sp.Gruppe = gr;
                plan.Spalten.Add(sp);
                if (sp.Strang is { } s) strangSpalte[s.Id] = sp;
            }
        }

        /// <summary>Abstand im Grundriss (mm) zwischen einem Knoten eines waagrechten Strangs und der Lage eines senkrechten Strangs;
        /// 0, wenn eines davon nicht platziert ist (dann gilt die Verbindung allein).</summary>
        static double Ankerabstand(SchemaKnoten k, SchemaSpalte s) =>
            k.Element.Geometrie?.Punkte is { Count: > 0 } p && s.Lage is { } l ? Math.Sqrt(Math.Pow(p[0][0] - l[0], 2) + Math.Pow(p[0][1] - l[1], 2)) : 0;

        static SchemaKnoten? Andere(SchemaLinie l, SchemaKnoten k) => l.Von.Knoten == k ? l.Nach.Knoten : l.Von.Knoten;

        static void Setzen(SchemaKnoten k, double x, double y, string ausrichtung)
        {
            k.X = Math.Round(x, 3); k.Y = Math.Round(y, 3); k.Ausrichtung = ausrichtung;
        }

        // ------------------------------------------------------------------ Lage im Gebäude, Gruppen

        /// <summary>Quer zur Blickrichtung (links → rechts) bzw. Tiefe in Blickrichtung (nahe zuerst) eines Grundrisspunkts.</summary>
        double Quer(double[] l) => plan.Blickrichtung switch { "nord" => -l[0], "ost" => l[1], "west" => -l[1], _ => l[0] };
        double Tiefe(double[] l) => plan.Blickrichtung switch { "nord" => -l[1], "ost" => -l[0], "west" => l[0], _ => l[1] };

        /// <summary>Lage eines Strangs im Grundriss (Regeln im Dateikopf); null = nichts platziert.</summary>
        double[]? StrangLage(Element strang)
        {
            bool senkrecht = !IstWaagrecht(strang);
            var achsen = new Dictionary<(long, long), (double X, double Y, double W)>();
            double ax = 0, ay = 0, aw = 0, px = 0, py = 0; int pn = 0;
            foreach (var e in (kinder.GetValueOrDefault(strang.Id) ?? []).OrderBy(e => e, SchemaGraph.Ordnung))
            {
                if (e.Geometrie?.Punkte is not { Count: > 0 } pts) continue;
                if (K.IstArt(e, "Leitung") && pts.Count >= 2)
                {
                    for (int i = 1; i < pts.Count; i++)
                    {
                        var a = pts[i - 1]; var b = pts[i];
                        double dx = b[0] - a[0], dy = b[1] - a[1], dz = a.Length > 2 && b.Length > 2 ? b[2] - a[2] : 0;
                        double h = Math.Sqrt(dx * dx + dy * dy), v = Math.Abs(dz), l = Math.Sqrt(h * h + v * v);
                        if (l < 1e-9) continue;
                        double mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2;
                        if (senkrecht && v >= h)
                        {
                            var schluessel = ((long)Math.Round(mx / SchemaRaster.Achsraster), (long)Math.Round(my / SchemaRaster.Achsraster));
                            var (qx, qy, qw) = achsen.GetValueOrDefault(schluessel);
                            achsen[schluessel] = (qx + mx * v, qy + my * v, qw + v);
                        }
                        ax += mx * l; ay += my * l; aw += l;
                    }
                }
                else { px += pts[0][0]; py += pts[0][1]; pn++; }
            }
            if (achsen.Count > 0)
            {
                var (bx, by, bw) = achsen.OrderByDescending(a => a.Value.W).ThenBy(a => a.Key.Item1).ThenBy(a => a.Key.Item2).First().Value;
                return [bx / bw, by / bw];
            }
            if (aw > 0) return [ax / aw, ay / aw];
            if (pn > 0) return [px / pn, py / pn];
            return null;
        }

        /// <summary>Gruppen je Steigzone (sonst je Strang) in der Reihenfolge nach der Lage im Gebäude; Bündel nach realer Lage.</summary>
        List<SchemaGruppe> Gruppieren(IEnumerable<Element> straenge)
        {
            var gruppen = new List<SchemaGruppe>();
            var nachZone = new Dictionary<string, SchemaGruppe>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in straenge.OrderBy(x => x, SchemaGraph.Ordnung))
            {
                var sz = s.Text("steigzone")?.Trim();
                if (string.IsNullOrEmpty(sz)) sz = null;
                SchemaGruppe gr;
                if (sz is not null && nachZone.TryGetValue(sz, out var vorhanden)) gr = vorhanden;
                else
                {
                    gr = new SchemaGruppe { Art = "straenge", Steigzone = sz };
                    gruppen.Add(gr);
                    if (sz is not null) nachZone[sz] = gr;
                }
                gr.Spalten.Add(new SchemaSpalte { Art = IstWaagrecht(s) ? "waagrecht" : "senkrecht", Strang = s, System = SystemVonStrang(s), Gruppe = gr, Lage = StrangLage(s) });
            }
            foreach (var gr in gruppen)
            {
                var sortiert = gr.Spalten.OrderBy(s => s.Art == "senkrecht" ? 0 : 1).ThenBy(s => s.Lage is null ? 1 : 0)
                    .ThenBy(s => s.Lage is { } l ? Math.Round(Quer(l)) : 0).ThenBy(s => s.Lage is { } l ? Math.Round(Tiefe(l)) : 0)
                    .ThenBy(s => s.Strang!, SchemaGraph.Ordnung).ToList();
                gr.Spalten.Clear();
                gr.Spalten.AddRange(sortiert);
                var mit = sortiert.Where(s => s.Lage is not null).ToList();
                gr.Lage = mit.Count == 0 ? null : [mit.Average(s => s.Lage![0]), mit.Average(s => s.Lage![1])];
            }
            return gruppen.OrderBy(gr => gr.Lage is null ? 1 : 0)
                .ThenBy(gr => gr.Lage is { } l ? Math.Round(Quer(l)) : 0).ThenBy(gr => gr.Lage is { } l ? Math.Round(Tiefe(l)) : 0)
                .ThenBy(gr => gr.Bezeichnung, StringComparer.OrdinalIgnoreCase).ThenBy(gr => gr.Spalten[0].Strang!.Id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Fliesst ein senkrechter Strang aufwärts? steigstrang ja, fallstrang nein, sonst nach dem Systemtyp
        /// (Verbraucher stromab = aufwärts). ANNAHME: ohne Strang.art fliessen Systeme mit Verbrauchern stromab aufwärts.</summary>
        bool Aufwaerts(Element? strang)
        {
            if (strang is null) return true;
            var art = strang.Text("art");
            if (art == "steigstrang") return true;
            if (art == "fallstrang") return false;
            var sys = SystemVonStrang(strang);
            return sys is null || global::Hlks.Kern.Netz.VerbraucherRichtung(M, K, sys) == "stromab";
        }

        /// <summary>System der Zone eines Elements: sein System, sonst das erste der angeschlossenen Systeme; null = ohne System.</summary>
        Element? ZonenSystem(Element e) => g.SystemeVon(e).FirstOrDefault();

        /// <summary>Nächste Stranglinie je Gerät (Endgerät, Quelle, Senke, Zentrale ohne teilVon-Strang): Breitensuche ab allen
        /// Leitungsbauteilen mit Strang über die sichtbaren Elemente ohne Strang (nicht durch Geräte); das Gerät nimmt den
        /// Strang des nächsten Nachbarn, bei gleichem Abstand die erste Stranglinie (Gruppen- und Bündelreihenfolge).</summary>
        Dictionary<string, int> NaechsterStrang(Dictionary<string, int> strangRang)
        {
            bool Geraet(Element e) => global::Hlks.Kern.Netz.IstNetzgeraet(K, e);
            int? Quelle(Element e) => !Geraet(e) && g.StrangVon(e) is { } st && strangRang.TryGetValue(st.Id, out var r) ? r : null;
            var dist = new Dictionary<string, int>(StringComparer.Ordinal);
            var best = new Dictionary<string, int>(StringComparer.Ordinal);
            var q = new Queue<Element>();
            foreach (var e in g.Sichtbare)
                if (Quelle(e) is int r) { dist[e.Id] = 0; best[e.Id] = r; q.Enqueue(e); }
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                foreach (var (_, n, _, _) in g.Nachbarn(u))
                {
                    if (!g.IstSichtbar(n) || Geraet(n) || Quelle(n) is not null) continue;
                    if (!dist.TryGetValue(n.Id, out var dn)) { dist[n.Id] = dist[u.Id] + 1; best[n.Id] = best[u.Id]; q.Enqueue(n); }
                    else if (dn == dist[u.Id] + 1 && best[u.Id] < best[n.Id]) best[n.Id] = best[u.Id];
                }
            }
            var ergebnis = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kn in g.Knoten)
            {
                if (!IstGeraet(kn)) continue;
                (int D, int R)? b = null;
                foreach (var (_, n, _, _) in g.Nachbarn(kn.Element))
                    if (g.IstSichtbar(n) && dist.TryGetValue(n.Id, out var d) && (b is null || d < b.Value.D || (d == b.Value.D && best[n.Id] < b.Value.R)))
                        b = (d, best[n.Id]);
                if (b is not null) ergebnis[kn.Id] = b.Value.R;
            }
            return ergebnis;
        }

        // ------------------------------------------------------------------ Netz je Zelle

        /// <summary>Steigknoten: Schemalinie zu einem Knoten (kein Gerät) in einer anderen Zeile oder zu einem Leitungsbauteil eines
        /// anderen Strangs.</summary>
        bool IstSteig(SchemaKnoten k)
        {
            foreach (var l in linienAn.GetValueOrDefault(k.Id) ?? [])
            {
                var o = Andere(l, k);
                if (o is null || o == k || IstGeraet(o)) continue;
                if (o.Zeile != k.Zeile || o.Spalte != k.Spalte) return true;
            }
            return false;
        }

        void NetzRechnen(Zelle c)
        {
            foreach (var k in c.Strang) if (IstSteig(k)) c.Steig.Add(k);
            Baeume(c);
            // nicht erreichbare Strangknoten stehen auf der Stranglinie (wie Steigknoten); dann die Wege neu
            var rest = c.Strang.Where(k => !c.Steig.Contains(k) && c.WegVon(k) is null).ToList();
            if (rest.Count > 0)
            {
                foreach (var k in rest) c.Steig.Add(k);
                Baeume(c);
            }
            // Platz der Steigknoten je Stranglinie nach Fliessrang
            foreach (var sp in c.Gruppe.Senkrecht)
            {
                bool auf = Aufwaerts(sp.Strang);
                var reihe = c.Steig.Where(k => k.Spalte == sp).OrderBy(k => auf ? k.Rang : -k.Rang).ThenBy(k => k.Element, SchemaGraph.Ordnung).ToList();
                for (int j = 0; j < reihe.Count; j++) c.Stufe[reihe[j]] = j;
            }
            // Stranglinie für die Reihenfolge: die mit den meisten erreichten Geräten, dann nach Platz
            c.Strangfolge.AddRange(c.Baeume.Keys
                .OrderByDescending(s => c.Baeume[s].Keys.Count(k => IstGeraet(k) && k.Zeile == c.Zeile))
                .ThenBy(s => s.Art == "senkrecht" ? 0 : 1).ThenBy(s => s.Platz));
        }

        static Abstand Gewicht(SchemaLinie l, Katalog k) =>
            new(l.Elemente.Where(e => k.IstArt(e, "Leitung") && e.Geometrie is not null).Sum(e => e.Geometrie!.Laenge()), l.Elemente.Count + 1);

        /// <summary>Je Stranglinie der Gruppe kürzeste Wege (Dijkstra) ab ihren Wurzeln (Steigknoten bzw. Knoten des waagrechten
        /// Strangs in dieser Zeile) über ihre Etagenknoten zu den Geräten des Geschosses; Geräte leiten nicht weiter.</summary>
        void Baeume(Zelle c)
        {
            c.Baeume.Clear();
            foreach (var sp in c.Gruppe.Spalten)
            {
                var wurzeln = c.Steig.Where(k => k.Spalte == sp).Concat(c.Spur.Where(k => k.Spalte == sp)).OrderBy(k => k.Element, SchemaGraph.Ordnung).ToList();
                if (wurzeln.Count == 0) continue;
                var kandidat = c.Strang.Where(k => k.Spalte == sp && !c.Steig.Contains(k)).ToHashSet();
                bool Endpunkt(SchemaKnoten k) => k.Zeile == c.Zeile && IstGeraet(k) && !k.Spalte!.IstZone;
                var baum = new Dictionary<SchemaKnoten, Weg>();
                var offen = new SortedSet<(Abstand A, SchemaKnoten K)>(Comparer<(Abstand A, SchemaKnoten K)>.Create((a, b) =>
                {
                    int r = a.A.CompareTo(b.A);
                    return r != 0 ? r : SchemaGraph.Ordnung.Compare(a.K.Element, b.K.Element);
                }));
                foreach (var w in wurzeln) { baum[w] = new Weg { Abstand = new Abstand(), Wurzel = w }; offen.Add((new Abstand(), w)); }
                var istWurzel = wurzeln.ToHashSet();
                while (offen.Count > 0)
                {
                    var (a, u) = offen.Min;
                    offen.Remove(offen.Min);
                    var wu = baum[u];
                    if (Endpunkt(u) && !istWurzel.Contains(u)) continue;   // Geräte leiten nicht weiter
                    foreach (var l in linienAn.GetValueOrDefault(u.Id) ?? [])
                    {
                        var o = Andere(l, u);
                        if (o is null || o == u || istWurzel.Contains(o) || !(kandidat.Contains(o) || Endpunkt(o))) continue;
                        var nd = a + Gewicht(l, K);
                        if (baum.TryGetValue(o, out var wo))
                        {
                            if (wo.Abstand.CompareTo(nd) <= 0) continue;
                            offen.Remove((wo.Abstand, o));
                        }
                        baum[o] = new Weg { Abstand = nd, Vor = u, Wurzel = wu.Wurzel };
                        offen.Add((nd, o));
                    }
                }
                foreach (var w in baum.Values) if (w.Vor is { } v) baum[v].Kinder++;
                c.Baeume[sp] = baum;
            }
        }

        /// <summary>Abstand des Abzweigs eines Knotens im Baum: letzter Knoten auf seinem Weg mit zwei Ästen (bzw. die Wurzel).</summary>
        static Abstand Abzweig(Dictionary<SchemaKnoten, Weg> baum, SchemaKnoten k)
        {
            var p = baum[k].Vor;
            while (p is not null)
            {
                var wp = baum[p];
                if (wp.Vor is null || wp.Kinder >= 2) return wp.Abstand;
                p = wp.Vor;
            }
            return default;
        }

        /// <summary>Schlüssel der Wurzel: Steigknoten von oben nach unten (−Stufe), Knoten waagrechter Stränge nach Fliessrang.</summary>
        static double WurzelSchluessel(Zelle c, SchemaKnoten w) => c.Stufe.TryGetValue(w, out var j) ? -j : w.Rang;

        /// <summary>Reihenfolge der Geräte einer Zelle: lexikografisch über die Stranglinien der Strangfolge (erreicht vor nicht
        /// erreicht, Wurzel, Abzweig, eigener Abstand), dann Kennzeichen – so vergleicht jede Stufe nur Wege desselben Strangs.</summary>
        sealed class FolgeVergleich(Zelle c) : IComparer<SchemaKnoten>
        {
            public int Compare(SchemaKnoten? a, SchemaKnoten? b)
            {
                foreach (var s in c.Strangfolge)
                {
                    var baum = c.Baeume[s];
                    bool ea = baum.TryGetValue(a!, out var wa), eb = baum.TryGetValue(b!, out var wb);
                    if (ea != eb) return ea ? -1 : 1;
                    if (!ea) continue;
                    int r = WurzelSchluessel(c, wa!.Wurzel).CompareTo(WurzelSchluessel(c, wb!.Wurzel));
                    if (r == 0) r = Abzweig(baum, a!).CompareTo(Abzweig(baum, b!));
                    if (r == 0) r = wa.Abstand.CompareTo(wb.Abstand);
                    if (r != 0) return r;
                }
                return SchemaGraph.Ordnung.Compare(a!.Element, b!.Element);
            }
        }

        /// <summary>Zone eines Endgeräts: teilVon = Zone, sonst die Zone seines Raums (Raum.teilVon).</summary>
        Element? ZoneVon(Element e)
        {
            if (M.Finde(e.TeilVon) is { } z && K.IstArt(z, "Zone")) return z;
            if (M.Finde(e.Raum) is { } r && M.Finde(r.TeilVon) is { } z2 && K.IstArt(z2, "Zone")) return z2;
            return null;
        }

        /// <summary>Plätze der Zelle von links: Endgeräte und Geräte nach Netzreihenfolge, Endgeräte einer Zone als Block.</summary>
        void Folge(Zelle c)
        {
            var vergleich = new FolgeVergleich(c);
            // Schächte stehen nicht in der Reihe der Plätze, sondern im Schnitt unter dem untersten Band (Nachtrag B8d)
            var knoten = c.Seite.OfType<SchemaKnoten>().Where(k => k.Schacht is null).OrderBy(k => k, vergleich).ToList();
            var bloecke = new Dictionary<string, SchemaBlock>(StringComparer.Ordinal);
            foreach (var k in knoten)
            {
                if (plan.Wohnungsbloecke && k.Art == "endgeraet" && ZoneVon(k.Element) is { } zone)
                {
                    if (!bloecke.TryGetValue(zone.Id, out var b))
                    {
                        b = new SchemaBlock { Zone = zone, Gruppe = c.Gruppe, Zeile = c.Zeile };
                        bloecke[zone.Id] = b;
                        plan.Bloecke.Add(b);
                        c.Folge.Add(b);
                    }
                    b.Endgeraete.Add(k);
                    k.Block = b; k.Verdeckt = true;
                    var kuerzel = K.Klassen.TryGetValue(k.Element.Klasse, out var d) ? d.Kuerzel : k.Element.Klasse;
                    var zeile = b.Zeilen.FirstOrDefault(z => z.Klasse == k.Element.Klasse);
                    if (zeile is null) b.Zeilen.Add(zeile = new SchemaBlockZeile { Klasse = k.Element.Klasse, Kuerzel = kuerzel });
                    zeile.Elemente.Add(k.Element);
                }
                else c.Folge.Add(k);
            }
            c.Folge.AddRange(c.Seite.OfType<SchemaLinie>());
        }

        /// <summary>Rollen der Etagenknoten je Baum: geteilt, Abgang, Anschlussarmatur bzw. Inneres eines Blocks, tot.</summary>
        void Rollen(Zelle c)
        {
            foreach (var (_, baum) in c.Baeume)
            {
                // Plätze hinter jedem Knoten (Kinder haben stets einen grösseren Abstand als ihr Vorgänger)
                var reihe = baum.OrderByDescending(x => x.Value.Abstand).ThenBy(x => x.Key.Element, SchemaGraph.Ordnung).ToList();
                var plaetze = reihe.ToDictionary(x => x.Key, _ => new HashSet<object>());
                foreach (var (k, w) in reihe)
                {
                    // ein Schacht ist kein Platz der Zeile (er steht unter dem untersten Band, Nachtrag B8d)
                    if (IstGeraet(k) && k.Schacht is null && w.Vor is not null) plaetze[k].Add(k.Block is { } b && k.Verdeckt ? b : k);
                    if (w.Vor is { } v) plaetze[v].UnionWith(plaetze[k]);
                }
                foreach (var (k, w) in reihe.AsEnumerable().Reverse())
                {
                    if (c.Steig.Contains(k) || IstGeraet(k) || w.Vor is null) continue;
                    var pl = plaetze[k];
                    c.Plaetze[k] = pl;
                    if (pl.Count == 0) c.Rolle[k] = "tot";
                    else if (pl.Count >= 2) c.Rolle[k] = "geteilt";
                    else if (pl.First() is SchemaBlock b)
                    {
                        var v = w.Vor!;
                        bool eintritt = v.Block != b || c.Rolle.GetValueOrDefault(v) is not ("anschluss" or "innen");
                        bool anschluss = k.Art == "armatur" && (eintritt || c.Rolle.GetValueOrDefault(v) == "anschluss");
                        c.Rolle[k] = anschluss ? "anschluss" : "innen";
                        k.Block = b;
                        k.Verdeckt = !anschluss;
                        var an = b.Anschluesse.FirstOrDefault(x => x.Wurzel == w.Wurzel);
                        if (an is null) b.Anschluesse.Add(an = new SchemaBlockAnschluss { Wurzel = w.Wurzel });
                        if (anschluss) { an.Armaturen.Add(k); b.Armaturplaetze = Math.Max(b.Armaturplaetze, an.Armaturen.Count); }
                    }
                    else c.Rolle[k] = "abgang";
                }
                // Anschlüsse auch für Blöcke, deren Endgeräte direkt am Steigknoten hängen
                foreach (var (k, w) in reihe)
                    if (IstGeraet(k) && k.Verdeckt && k.Block is { } b && w.Vor is not null && !b.Anschluesse.Any(x => x.Wurzel == w.Wurzel))
                        b.Anschluesse.Add(new SchemaBlockAnschluss { Wurzel = w.Wurzel });
            }
        }

        // ------------------------------------------------------------------ Dämmung

        void DaemmungSammeln()
        {
            foreach (var c in Zellen.Where(c => !c.Gruppe.IstZone && !c.Zeile.IstZone))
            {
                var werte = new SortedDictionary<double, List<SchemaSpalte>>();
                foreach (var sp in c.Gruppe.Senkrecht)
                    foreach (var d in (kinder.GetValueOrDefault(sp.Strang!.Id) ?? [])
                                 .Where(e => K.IstArt(e, "Leitung") && e.Geschoss == c.Zeile.Geschoss!.Id && g.IstSichtbar(e))
                                 .Select(e => e.Zahl("daemmdicke")).OfType<double>().Where(d => d > 0).Distinct())
                    {
                        if (!werte.TryGetValue(d, out var l)) werte[d] = l = [];
                        if (!l.Contains(sp)) l.Add(sp);
                    }
                foreach (var (d, sps) in werte)
                {
                    var dm = new SchemaDaemmung { Gruppe = c.Gruppe, Zeile = c.Zeile, Dicke = d };
                    dm.Spalten.AddRange(sps.OrderBy(s => s.Platz));
                    plan.Daemmungen.Add(dm);
                }
            }
        }

        void DaemmungSetzen()
        {
            foreach (var grp in plan.Daemmungen.GroupBy(d => (d.Gruppe.Index, d.Zeile.Index)))
            {
                int i = 0;
                foreach (var d in grp.OrderBy(x => x.Dicke))
                {
                    d.Y = Math.Round(d.Zeile.Y0 + SchemaRaster.Daemmhoehe - i++ * SchemaRaster.Daemmabstand, 3);
                    d.X0 = Math.Round(d.Spalten.Min(s => s.LinieX) - 1.5, 3);
                    d.XText = Math.Round(d.Gruppe.BuendelRechts + 1.5, 3);
                }
            }
        }

        // ------------------------------------------------------------------ Einlagen im Deckenband (Nachtrag B8c)

        readonly Dictionary<string, double> einlageY = new(StringComparer.Ordinal);
        readonly List<SchemaLinie> gemischt = [];
        readonly List<SchemaLinie> wandEinlagen = [];
        List<SchemaZeile> normale = [];

        /// <summary>Bänder anlegen, Einlage-Linien und -Knoten ihrem Band zuordnen (Regeln im Dateikopf, «Einlagen»); Höhen vorerst 2.4.</summary>
        void BaenderBilden(Dictionary<string, SchemaZeile> zeileVon)
        {
            normale = plan.Zeilen.Where(z => !z.IstZone).OrderBy(z => z.Index).ToList();
            int n = normale.Count;
            if (n == 0) return;
            for (int i = 0; i <= n; i++)
                plan.Baender.Add(new SchemaBand { Index = i, Unter = i > 0 ? normale[i - 1] : null, Ueber = i < n ? normale[i] : null });
            if (ReferenceEquals(M, g.Projekt.Modell))   // die Verlegung gilt für das Modell des Projekts
            {
                var vs = plan.Verlegung = global::Hlks.Kern.Verlegung.Von(g.Projekt);
                SchemaBand? BandVon(Verlegungsbefund? b)
                {
                    if (b is null || !b.IstEinlage || b.Geschoss is null || !zeileVon.TryGetValue(b.Geschoss, out var z) || z.IstZone) return null;
                    return b.Bauteilart switch
                    {
                        "decke" or "dach" => plan.Baender[z.Index + 1],
                        "bodenplatte" => plan.Baender[z.Index],
                        _ => null,
                    };
                }
                // Linien: Einlage, wenn mehr als die Hälfte der Leitungslänge Einlage in einer Platte ist; Band mit der grössten Länge
                foreach (var l in g.Linien.Where(l => !l.Frei))
                {
                    var leit = l.Elemente.Where(e => K.IstArt(e, "Leitung")).ToList();
                    if (leit.Count == 0) continue;
                    var laengen = leit.Select(e => e.Geometrie?.Laenge() ?? 0).ToList();
                    bool ohneLaenge = laengen.Sum() <= 1e-6;
                    double total = 0, einlage = 0, wand = 0;
                    var jeBand = new Dictionary<SchemaBand, double>();
                    for (int i = 0; i < leit.Count; i++)
                    {
                        double w = ohneLaenge ? 1 : laengen[i];
                        total += w;
                        var bf = vs.Befund(leit[i].Id);
                        if (bf is not { IstEinlage: true }) continue;
                        einlage += w;
                        if (BandVon(bf) is { } bd) jeBand[bd] = jeBand.GetValueOrDefault(bd) + w;
                        else wand += w;
                    }
                    if (einlage <= 0 || total <= 0) continue;
                    l.Einlageanteil = Math.Round(einlage / total, 4);
                    double platte = jeBand.Values.Sum();
                    if (platte * 2 > total) l.Band = jeBand.OrderByDescending(x => x.Value).ThenBy(x => x.Key.Index).First().Key;
                    else if (wand * 2 > total) wandEinlagen.Add(l);
                    if (einlage < total - 1e-6 || (l.Band is not null && platte < total - 1e-6)) gemischt.Add(l);
                }
                // Knoten auf waagrechten Strängen und Etagenknoten (geteilt, ohne Endgerät, Anschlussarmatur) mit Verlegung einlage; von
                // Hand gesetzte Lagen gelten (der Knoten ist dann frei)
                var hand = Lagen(plan.Ansicht);
                foreach (var c in Zellen.Where(c => !c.Gruppe.IstZone))
                    foreach (var k in c.Spur.Concat(c.Rolle.Where(r => r.Value is "geteilt" or "tot" or "anschluss").Select(r => r.Key)).Distinct())
                        if (!hand.ContainsKey(k.Id) && BandVon(vs.Befund(k.Id)) is { } bd) k.Band = bd;
            }
            HoehenSetzen();
        }

        /// <summary>Höhen der Bänder aus ihren Spuren, Zeilen nachrücken, Mitten der Bänder. true = eine Zeile hat sich verschoben.</summary>
        bool HoehenSetzen()
        {
            int n = normale.Count;
            if (n == 0) return false;
            double D = SchemaRaster.Deckenband, zuwachs = 0;
            bool verschoben = false;
            foreach (var b in plan.Baender)
                b.Hoehe = b.Spuren.Count <= 1 ? D : Math.Max(D, 2 * SchemaRaster.Bandrand + (b.Spuren.Count - 1) * SchemaRaster.Einlagespur);
            void Y0(SchemaZeile z, double y)
            {
                y = Math.Round(y, 3);
                if (Math.Abs(z.Y0 - y) > 1e-9) verschoben = true;
                z.Y0 = y;
            }
            for (int i = 0; i < n; i++)
            {
                if (i > 0) zuwachs += plan.Baender[i].Hoehe - D;
                Y0(normale[i], i * SchemaRaster.Zeilenhoehe + zuwachs);
            }
            // die Zone «ohne Geschoss» rückt um den Zuwachs von Band 0 und die Fläche unter dem untersten Band (B8d) nach unten
            foreach (var z in plan.Zeilen.Where(z => z.IstZone))
                Y0(z, -(SchemaRaster.Zeilenhoehe + SchemaRaster.Zonenabstand) - (plan.Baender[0].Hoehe - D) - (plan.Untergrund?.Hoehe ?? 0));
            for (int i = 0; i < n; i++) plan.Baender[i].Mitte = Math.Round(normale[i].Y0 + D / 2 - plan.Baender[i].Hoehe / 2, 3);
            plan.Baender[n].Mitte = Math.Round(normale[n - 1].Y0 + SchemaRaster.Zeilenhoehe - D / 2 + plan.Baender[n].Hoehe / 2, 3);
            return verschoben;
        }

        /// <summary>Spuren im Band (nach den x der Knoten): Einlagezüge = Einlage-Linien und -Knoten desselben Bands, über Knoten im Band
        /// verbunden; Ausdehnung = x der Enden (offenes Ende ± Offenlänge). Je Band nach Anfang, Ende und kleinstem Kennzeichen sortiert;
        /// jeder Zug auf die oberste Spur, deren letzter Zug mindestens die Spurlücke vor seinem Anfang endet, sonst eine neue Spur
        /// darunter. Dann Höhen, Zeilen, y der Spuren. true = eine Zeile hat sich verschoben (die Lagen werden neu gerechnet).</summary>
        bool EinlageSpurenSetzen()
        {
            var linien = g.Linien.Where(l => l.Band is not null).ToList();
            var knoten = g.Knoten.Where(k => k.Band is not null).ToList();
            if (linien.Count == 0 && knoten.Count == 0) return false;
            var eltern = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            object Wurzel(object x)
            {
                if (!eltern.TryGetValue(x, out var e)) { eltern[x] = x; return x; }
                while (!ReferenceEquals(e, x)) { x = e; e = eltern[x]; }
                return x;
            }
            void Verbinden(object a, object b) { var ra = Wurzel(a); var rb = Wurzel(b); if (!ReferenceEquals(ra, rb)) eltern[ra] = rb; }
            foreach (var k in knoten) Wurzel(k);
            foreach (var l in linien)
            {
                Wurzel(l);
                foreach (var en in new[] { l.Von, l.Nach })
                    if (en.Knoten is { } k && k.Band == l.Band) Verbinden(l, k);
            }
            var zuege = new Dictionary<object, (SchemaBand Band, double Min, double Max, string Ordnung, List<object> Teile)>(ReferenceEqualityComparer.Instance);
            void Dazu(object teil, SchemaBand band, double x0, double x1, string kz)
            {
                var w = Wurzel(teil);
                if (!zuege.TryGetValue(w, out var z)) z = (band, double.MaxValue, double.MinValue, kz, []);
                z.Teile.Add(teil);
                zuege[w] = (band, Math.Min(z.Min, x0), Math.Max(z.Max, x1), string.CompareOrdinal(kz, z.Ordnung) < 0 ? kz : z.Ordnung, z.Teile);
            }
            foreach (var k in knoten) Dazu(k, k.Band!, k.X, k.X, k.Kennzeichen);
            foreach (var l in linien)
            {
                var xs = new[] { l.Von, l.Nach }.Select(en => en.Knoten?.X).OfType<double>().ToList();
                double x0 = xs.Min(), x1 = xs.Max();
                if (l.Von.Knoten is null || l.Nach.Knoten is null) { x0 -= SchemaRaster.Offenlaenge; x1 += SchemaRaster.Offenlaenge; }
                Dazu(l, l.Band!, x0, x1, l.Elemente.Count > 0 ? l.Elemente[0].Kennzeichen : "");
            }
            var spurVon = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            foreach (var jeBand in zuege.GroupBy(z => z.Value.Band.Index).OrderBy(x => x.Key))
            {
                var band = plan.Baender[jeBand.Key];
                var enden = new List<double>();
                var namen = new List<SortedSet<string>>();
                // Abläufe sitzen oben im Band auf der Höhe von Spur 0 (B8d): ein fremder Zug läuft dort nicht durch ihren Körper
                var ab = ablaeufe.Where(a => a.Ablaufband == band).ToList();
                const double halb = SchemaRaster.Ablaufgroesse / 2, luecke = SchemaRaster.Spurluecke;
                foreach (var (w, z) in jeBand.OrderBy(x => x.Value.Min).ThenBy(x => x.Value.Max).ThenBy(x => x.Value.Ordnung, StringComparer.Ordinal)
                             .Select(x => (x.Key, x.Value)))
                {
                    var eigene = z.Teile.OfType<SchemaLinie>().SelectMany(l => new[] { l.Von.Knoten, l.Nach.Knoten }).ToHashSet();
                    bool frei0 = ab.All(a => eigene.Contains(a) || a.X + halb + luecke <= z.Min + 1e-9 || z.Max + luecke <= a.X - halb + 1e-9);
                    int t = -1;
                    for (int i = 0; i < enden.Count && t < 0; i++)
                        if (enden[i] + luecke <= z.Min + 1e-9 && (i > 0 || frei0)) t = i;
                    if (t < 0)
                    {
                        if (enden.Count == 0 && !frei0) { enden.Add(double.MinValue); namen.Add(new SortedSet<string>(StringComparer.Ordinal)); }
                        t = enden.Count; enden.Add(double.MinValue); namen.Add(new SortedSet<string>(StringComparer.Ordinal));
                    }
                    enden[t] = Math.Max(enden[t], z.Max);
                    spurVon[w] = t;
                    foreach (var l in z.Teile.OfType<SchemaLinie>()) namen[t].Add(l.Strang?.Kennzeichen ?? l.Medium ?? "?");
                }
                band.Spuren.Clear();
                band.Spuren.AddRange(namen.Select(x => string.Join(", ", x)));
            }
            bool verschoben = HoehenSetzen();
            foreach (var l in linien) l.BandY = l.Band!.SpurY(spurVon[Wurzel(l)]);
            foreach (var k in knoten) einlageY[k.Id] = k.Band!.SpurY(spurVon[Wurzel(k)]);
            return verschoben;
        }


        /// <summary>Linie einer Einlage: senkrecht ins Band, waagrecht auf der Spur, senkrecht zum anderen Ende.</summary>
        static List<double[]> DurchsBand(SchemaEnde a, SchemaEnde b, double y) => Vereinfacht([[a.X, a.Y], [a.X, y], [b.X, y], [b.X, b.Y]]);

        /// <summary>Linie mit genau einem Ende im Band (keine Einlage): waagrecht auf der Höhe des anderen Endes, senkrecht ins Band.</summary>
        static List<double[]> EinEndeImBand(SchemaEnde a, SchemaEnde b, bool aImBand) =>
            Vereinfacht(aImBand ? [[a.X, a.Y], [a.X, b.Y], [b.X, b.Y]] : [[a.X, a.Y], [b.X, a.Y], [b.X, b.Y]]);

        /// <summary>Doppelte und in einer Geraden liegende Zwischenpunkte entfernen (achsparallele Züge).</summary>
        static List<double[]> Vereinfacht(List<double[]> pts)
        {
            var aus = new List<double[]>();
            foreach (var q in pts)
            {
                var r = new[] { Math.Round(q[0], 3), Math.Round(q[1], 3) };
                if (aus.Count > 0 && Math.Abs(aus[^1][0] - r[0]) < 1e-6 && Math.Abs(aus[^1][1] - r[1]) < 1e-6) continue;
                if (aus.Count >= 2)
                {
                    var a = aus[^2]; var m = aus[^1];
                    bool senkrecht = Math.Abs(a[0] - m[0]) < 1e-6 && Math.Abs(m[0] - r[0]) < 1e-6;
                    bool waagrecht = Math.Abs(a[1] - m[1]) < 1e-6 && Math.Abs(m[1] - r[1]) < 1e-6;
                    if (senkrecht || waagrecht) aus.RemoveAt(aus.Count - 1);
                }
                aus.Add(r);
            }
            if (aus.Count == 1) aus.Add([aus[0][0], aus[0][1]]);
            return aus;
        }

        // ------------------------------------------------------------------ Apparate, Abläufe, Schächte, Grundleitungen (Nachtrag B8d)

        readonly List<SchemaKnoten> ablaeufe = [];
        readonly Dictionary<object, int> untergrundSpur = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<string, double> symbolUnten = new(StringComparer.Ordinal);
        readonly List<SchemaLinie> schachtKreuzungen = [];

        IEnumerable<SchemaSchacht> Schaechte => g.Knoten.Where(k => k.Schacht is not null).Select(k => k.Schacht!);

        /// <summary>Schächte nach ihrer Lage im Gebäude unter eine Gruppe (Regeln im Dateikopf, «Schächte»): die letzte Gruppe mit Lage, die
        /// quer zur Blickrichtung nicht rechts des Schachts liegt (sonst die erste); ohne Lage die Gruppe seines Strangs. In der Gruppe
        /// nach Lage (quer, dann Tiefe), dann Kennzeichen.</summary>
        void SchaechteZuordnen()
        {
            var mitLage = plan.Gruppen.Where(gr => !gr.IstZone && gr.Lage is not null).ToList();
            double[]? Punkt(SchemaKnoten k) => k.Element.Geometrie?.Punkte is { Count: > 0 } p ? p[0] : null;
            var liste = g.Knoten.Where(k => k.Schacht is not null).ToList();
            foreach (var k in liste)
            {
                var p = Punkt(k);
                if (p is not null && mitLage.Count > 0)
                {
                    double q = Math.Round(Quer(p));
                    k.Schacht!.Gruppe = mitLage.LastOrDefault(gr => Math.Round(Quer(gr.Lage!)) <= q) ?? mitLage[0];
                }
                else k.Schacht!.Gruppe = k.Spalte!.Gruppe;
            }
            foreach (var jeGruppe in liste.GroupBy(k => k.Schacht!.Gruppe))
            {
                int i = 0;
                foreach (var k in jeGruppe.OrderBy(k => Punkt(k) is null ? 1 : 0).ThenBy(k => Punkt(k) is { } p ? Math.Round(Quer(p)) : 0)
                             .ThenBy(k => Punkt(k) is { } p ? Math.Round(Tiefe(p)) : 0).ThenBy(k => k.Element, SchemaGraph.Ordnung))
                    k.Schacht!.Platz = i++;
            }
        }

        /// <summary>x der Schächte: Bündelrand der Gruppe + Endgerätversatz + Platz · Schachtabstand.</summary>
        void SchaechteXSetzen()
        {
            foreach (var s in Schaechte)
            {
                s.X = Math.Round(s.Gruppe.BuendelRechts + SchemaRaster.Endgeraetversatz + s.Platz * SchemaRaster.Schachtabstand, 3);
                Setzen(s.Knoten, s.X, plan.Baender.Count > 0 ? plan.Baender[0].Unten - SchemaRaster.Schachttiefe / 2 : s.Knoten.Y, "frei");
            }
        }

        /// <summary>Abläufe ihrem Band zuordnen, Linien und Knoten im Erdreich bestimmen, Linien zu Schächten und Knoten im Erdreich unter
        /// das unterste Band legen (Regeln im Dateikopf).</summary>
        void UntergrundBilden()
        {
            if (plan.Baender.Count == 0) return;
            // Abläufe: Bodenablauf im Band unter seiner Zeile, Dachablauf im Band darüber (liegt er näher am Boden seines Geschosses: darunter)
            foreach (var k in ablaeufe)
            {
                if (k.Zeile is not { IstZone: false } z) continue;
                bool oben = K.IstArt(k.Element, "Dachablauf");
                if (oben && k.Element.Geometrie?.Punkte is { Count: > 0 } p && p[0].Length > 2 && z.Geschoss is { } ge
                    && ge.Zahl("kote") is double kote && ge.Zahl("hoehe") is double h && Math.Abs(p[0][2] - kote) < Math.Abs(p[0][2] - (kote + h)))
                    oben = false;
                k.Ablaufband = plan.Baender[z.Index + (oben ? 1 : 0)];
            }
            // Leitungen im Erdreich (Verlegung): Linie, wenn mehr als die Hälfte ihrer Leitungslänge erdreich ist; Knoten auf waagrechten
            // Strängen und Etagenknoten mit Verlegung erdreich (nicht mit Hand-Lage)
            if (plan.Verlegung is { } vs)
            {
                foreach (var l in g.Linien.Where(l => !l.Frei && l.Band is null))
                {
                    var leit = l.Elemente.Where(e => K.IstArt(e, "Leitung")).ToList();
                    if (leit.Count == 0) continue;
                    var laengen = leit.Select(e => e.Geometrie?.Laenge() ?? 0).ToList();
                    bool ohneLaenge = laengen.Sum() <= 1e-6;
                    double total = 0, erd = 0;
                    for (int i = 0; i < leit.Count; i++)
                    {
                        double w = ohneLaenge ? 1 : laengen[i];
                        total += w;
                        if (vs.Befund(leit[i].Id) is { IstErdreich: true }) erd += w;
                    }
                    if (erd <= 0 || total <= 0) continue;
                    l.Erdreichanteil = Math.Round(erd / total, 4);
                    if (erd * 2 > total) l.Untergrund = true;
                }
                var hand = Lagen(plan.Ansicht);
                foreach (var c in Zellen.Where(c => !c.Gruppe.IstZone))
                    foreach (var k in c.Spur.Concat(c.Rolle.Where(r => r.Value is "geteilt" or "tot" or "anschluss").Select(r => r.Key)).Distinct())
                        if (!hand.ContainsKey(k.Id) && k.Band is null && vs.Befund(k.Id) is { IstErdreich: true }) k.Untergrund = true;
            }
            // Linien zu einem Schacht bzw. zu einem Knoten im Erdreich laufen unter dem untersten Band (Einlagen bleiben im Deckenband)
            foreach (var l in g.Linien.Where(l => !l.Frei && l.Band is null && !l.Untergrund))
                if (l.Von.Knoten is { } a && (a.Schacht is not null || a.Untergrund) || l.Nach.Knoten is { } b && (b.Schacht is not null || b.Untergrund))
                    l.Untergrund = true;
            // eine Einlage, die in einem Schacht endet, kommt von oben
            foreach (var l in g.Linien.Where(l => l.Band is not null))
                foreach (var en in new[] { l.Von, l.Nach })
                    if (en.Knoten?.Schacht is { } s) s.VonOben = true;
            if (g.Knoten.Any(k => k.Schacht is not null || k.Untergrund) || g.Linien.Any(l => l.Untergrund)) plan.Untergrund = new SchemaUntergrund();
        }

        /// <summary>x des Linienendes an einem Schacht: die Wand auf der Seite des anderen Endes.</summary>
        static double WandX(SchemaSchacht s, double andereX) => andereX <= s.X ? s.Links : s.Rechts;

        /// <summary>Kommt eine Linie (keine Leitung im Erdreich) senkrecht von oben in den Schacht (das andere Ende steht über ihm)?</summary>
        static bool VonOben(SchemaLinie l, SchemaSchacht s, SchemaKnoten? andere) =>
            andere is not null && andere.Schacht is null && !andere.Untergrund && l.Erdreichanteil * 2 <= 1 && Math.Abs(andere.X - s.X) < s.Breite / 2 - 0.5;

        /// <summary>Grundleitungsspuren unter dem untersten Band (nach den x der Knoten, Regeln im Dateikopf «Grundleitungen»), Tiefe der
        /// Schächte, Höhe der Fläche; dann Höhen und Zeilen. true = eine Zeile hat sich verschoben.</summary>
        bool UntergrundSetzen()
        {
            var u = plan.Untergrund;
            if (u is null) return false;
            u.Schaechte.Clear();
            u.Schaechte.AddRange(Schaechte.OrderBy(s => s.X).ThenBy(s => s.Knoten.Element, SchemaGraph.Ordnung));
            // Einlauf von oben: das andere Ende steht über dem Schacht → keine Spur, senkrecht bis zur Sohle
            foreach (var l in g.Linien.Where(l => l.Untergrund))
                foreach (var (en, gegen) in new[] { (l.Von, l.Nach), (l.Nach, l.Von) })
                    if (en.Knoten?.Schacht is { } s && VonOben(l, s, gegen.Knoten)) { l.Untergrund = false; s.VonOben = true; }
            var linien = g.Linien.Where(l => l.Untergrund).ToList();
            var knoten = g.Knoten.Where(k => k.Untergrund).ToList();
            // Züge: Linien und Knoten im Erdreich, über Knoten im Erdreich verbunden (ein Schacht verbindet nicht)
            var eltern = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            object Wurzel(object x)
            {
                if (!eltern.TryGetValue(x, out var e)) { eltern[x] = x; return x; }
                while (!ReferenceEquals(e, x)) { x = e; e = eltern[x]; }
                return x;
            }
            void Verbinden(object a, object b) { var ra = Wurzel(a); var rb = Wurzel(b); if (!ReferenceEquals(ra, rb)) eltern[ra] = rb; }
            foreach (var k in knoten) Wurzel(k);
            foreach (var l in linien)
            {
                Wurzel(l);
                foreach (var en in new[] { l.Von, l.Nach })
                    if (en.Knoten is { Untergrund: true } k) Verbinden(l, k);
            }
            // Stücke: je Linie ihre x-Ausdehnung (Enden; am Schacht die Wand; offenes Ende ± Offenlänge); Knoten ohne Linie allein
            var stuecke = new List<UStueck>();
            foreach (var l in linien)
            {
                var st = new UStueck { Teil = l, Ordnung = l.Elemente.Count > 0 ? l.Elemente[0].Kennzeichen : "" };
                var xs = new List<double>();
                foreach (var (en, gegen) in new[] { (l.Von, l.Nach), (l.Nach, l.Von) })
                {
                    if (en.Knoten is not { } k) continue;
                    if (k.Schacht is { } s) { xs.Add(WandX(s, gegen.Knoten?.X ?? s.X)); st.Eigene.Add(s); }
                    else xs.Add(k.X);
                }
                st.Min = xs.Min(); st.Max = xs.Max();
                if (l.Von.Knoten is null || l.Nach.Knoten is null) { st.Min -= SchemaRaster.Offenlaenge; st.Max += SchemaRaster.Offenlaenge; }
                stuecke.Add(st);
            }
            var mitLinie = linien.SelectMany(l => new[] { l.Von.Knoten, l.Nach.Knoten }).OfType<SchemaKnoten>().ToHashSet();
            foreach (var k in knoten.Where(k => !mitLinie.Contains(k))) stuecke.Add(new UStueck { Teil = k, Min = k.X, Max = k.X, Ordnung = k.Kennzeichen });
            foreach (var st in stuecke) st.Zug = Wurzel(st.Teil);
            // ein Stück, das unter einem fremden Schacht durchläuft, liegt tiefer als dessen Unterkante: erst die Stücke der Schächte, dann
            // die querenden (Stufen), je Stufe nach Anfang, Ende, kleinstem Kennzeichen
            foreach (var st in stuecke)
                st.Quert.AddRange(u.Schaechte.Where(s => !st.Eigene.Contains(s) && s.Links < st.Max - 1e-6 && s.Rechts > st.Min + 1e-6));
            for (int r = 0; r < stuecke.Count; r++)
            {
                bool anders = false;
                foreach (var st in stuecke)
                    foreach (var s in st.Quert)
                        foreach (var o in stuecke.Where(o => o != st && o.Eigene.Contains(s)))
                            if (o.Stufe + 1 > st.Stufe && o.Stufe + 1 <= stuecke.Count) { st.Stufe = o.Stufe + 1; anders = true; }
                if (!anders) break;
            }
            static double Y(int t) => -SchemaRaster.Untergrundoben - t * SchemaRaster.Untergrundspur;   // relativ zur Unterkante Band 0
            double MindestTiefe(SchemaSchacht s) =>
                Math.Max(SchemaRaster.Schachttiefe, SchemaRaster.TextBreite(s.Knoten.Kennzeichen, SchemaRaster.SchachtSchrift) + 2);
            double SchachtUnten(SchemaSchacht s)
            {
                double unten = -MindestTiefe(s);
                foreach (var o in stuecke.Where(o => o.Eigene.Contains(s) && o.Spur >= 0)) unten = Math.Min(unten, Y(o.Spur) - SchemaRaster.Sohlenrand);
                return unten;
            }
            // Spuren: die oberste freie ab der Mindestspur; Stücke desselben Zugs dürfen aneinanderstossen, fremde halten die Spurlücke
            var belegt = new List<List<UStueck>>();
            var namen = new List<SortedSet<string>>();
            static bool Frei(UStueck a, UStueck b)
            {
                double luecke = ReferenceEquals(a.Zug, b.Zug) ? 0 : SchemaRaster.Spurluecke;
                return a.Max + luecke <= b.Min + 1e-6 || b.Max + luecke <= a.Min + 1e-6;
            }
            foreach (var st in stuecke.OrderBy(s => s.Stufe).ThenBy(s => s.Min).ThenBy(s => s.Max).ThenBy(s => s.Ordnung, StringComparer.Ordinal))
            {
                int tmin = 0;
                foreach (var s in st.Quert)
                    tmin = Math.Max(tmin, (int)Math.Ceiling((-SchachtUnten(s) + SchemaRaster.Querabstand - SchemaRaster.Untergrundoben) / SchemaRaster.Untergrundspur - 1e-9));
                int t = tmin;
                while (t < belegt.Count && belegt[t].Any(b => !Frei(st, b))) t++;
                while (belegt.Count <= t) { belegt.Add([]); namen.Add(new SortedSet<string>(StringComparer.Ordinal)); }
                belegt[t].Add(st);
                st.Spur = t;
                if (st.Teil is SchemaLinie l) namen[t].Add(l.Strang?.Kennzeichen ?? l.Medium ?? "?");
            }
            untergrundSpur.Clear();
            foreach (var st in stuecke) untergrundSpur[st.Teil] = st.Spur;
            // Knoten: auf der Spur seiner abgehenden Linie (in Fliessrichtung), sonst der obersten seiner Linien
            foreach (var k in knoten.Where(mitLinie.Contains))
            {
                var eigene = linien.Where(l => l.Von.Knoten == k || l.Nach.Knoten == k).ToList();
                var raus = eigene.Where(l => l.Von.Knoten == k).ToList();
                untergrundSpur[k] = (raus.Count > 0 ? raus : eigene).Min(l => untergrundSpur[l]);
            }
            foreach (var s in u.Schaechte) s.UntenRel = Math.Round(SchachtUnten(s), 3);
            // querende Linien, die trotzdem durch einen Schacht laufen (Kreis von Schächten), für den Hinweis
            schachtKreuzungen.Clear();
            foreach (var st in stuecke)
                if (st.Teil is SchemaLinie l && st.Quert.Any(s => Y(st.Spur) > s.UntenRel - 1e-6)) schachtKreuzungen.Add(l);
            u.Spuren.Clear();
            u.Spuren.AddRange(namen.Select(x => string.Join(", ", x)));
            double tief = belegt.Count > 0 ? SchemaRaster.Untergrundoben + (belegt.Count - 1) * SchemaRaster.Untergrundspur : 0;
            foreach (var s in u.Schaechte) tief = Math.Max(tief, -s.UntenRel);
            u.Hoehe = Math.Round(tief + SchemaRaster.Untergrundrand, 3);
            return HoehenSetzen();
        }

        /// <summary>Ein Stück unter dem untersten Band (Linie bzw. Knoten ohne Linie): Zug, x-Ausdehnung, eigene und unterquerte Schächte,
        /// Stufe und Spur.</summary>
        sealed class UStueck
        {
            public required object Teil { get; init; }
            public object Zug { get; set; } = null!;
            public double Min { get; set; }
            public double Max { get; set; }
            public required string Ordnung { get; init; }
            public HashSet<SchemaSchacht> Eigene { get; } = [];
            public List<SchemaSchacht> Quert { get; } = [];
            public int Stufe { get; set; }
            public int Spur { get; set; } = -1;
        }

        /// <summary>x der Knoten im Erdreich (Regeln im Dateikopf «Grundleitungen»): unter dem Knoten einer Zeile, mit dem er direkt verbunden
        /// ist (höchstens die Ankerweite daneben im Grundriss); sonst unter dem Platz stromauf (Endgerät, Ablauf; über Knoten im Erdreich),
        /// der dem Abfluss (erster Schacht bzw. Knoten einer Zeile stromab) am nächsten liegt – wie die geteilten Etagenknoten –, und liegt
        /// dort schon ein Knoten im Erdreich stromauf, den Etagenschritt weiter zum Abfluss hin; ohne beides bleibt das x seiner Spur. Nach
        /// Fliessrang, stromauf zuerst.</summary>
        void UntergrundXSetzen()
        {
            var uk = g.Knoten.Where(k => k.Untergrund).OrderBy(k => k.Rang).ThenBy(k => k.Element, SchemaGraph.Ordnung).ToList();
            if (uk.Count == 0) return;
            IEnumerable<(SchemaLinie L, SchemaKnoten? O, bool Raus)> Nachbarn(SchemaKnoten k) =>
                (linienAn.GetValueOrDefault(k.Id) ?? []).Select(l => (l, Andere(l, k), l.Von.Knoten == k));
            double? Abfluss(SchemaKnoten k)
            {
                var besucht = new HashSet<SchemaKnoten> { k };
                var q = new Queue<SchemaKnoten>([k]);
                while (q.Count > 0)
                    foreach (var (_, o, raus) in Nachbarn(q.Dequeue()))
                    {
                        if (!raus || o is null || !besucht.Add(o)) continue;
                        if (o.Schacht is { } s) return s.X;
                        if (!o.Untergrund) return o.X;
                        q.Enqueue(o);
                    }
                return null;
            }
            static double Abstand(SchemaKnoten a, SchemaKnoten b) =>
                a.Element.Geometrie?.Punkte is { Count: > 0 } p && b.Element.Geometrie?.Punkte is { Count: > 0 } q
                    ? Math.Sqrt(Math.Pow(p[0][0] - q[0][0], 2) + Math.Pow(p[0][1] - q[0][1], 2)) : 0;
            foreach (var k in uk)
            {
                SchemaKnoten? oben = null;
                double besteD = double.MaxValue;
                foreach (var (_, o, _) in Nachbarn(k))
                {
                    if (o is null || o.Untergrund || o.Schacht is not null || IstGeraet(o) || o.Ablaufband is not null || o.Zeile is null) continue;
                    double d = Abstand(k, o);
                    if (d <= SchemaRaster.Ankerweite && (oben is null || d < besteD - 1e-6)) { oben = o; besteD = d; }
                }
                if (oben is not null) { k.X = oben.X; continue; }
                var plaetze = new List<double>();
                var besucht = new HashSet<SchemaKnoten> { k };
                var q = new Queue<SchemaKnoten>([k]);
                while (q.Count > 0)
                    foreach (var (_, o, raus) in Nachbarn(q.Dequeue()))
                    {
                        if (raus || o is null || !besucht.Add(o)) continue;
                        if (IstGeraet(o) && o.Schacht is null) plaetze.Add(o.X);
                        else if (o.Untergrund) q.Enqueue(o);
                    }
                if (plaetze.Count == 0) continue;
                double? xd = Abfluss(k);
                double x = xd is double d0 ? plaetze.OrderBy(p => Math.Abs(p - d0)).ThenBy(p => p).First() : plaetze.Max();
                if (Nachbarn(k).Any(n => !n.Raus && n.O is { Untergrund: true } v && Math.Abs(v.X - x) < 1e-6))
                    x += (xd is double d1 && d1 < x ? -1 : 1) * SchemaRaster.Etagenschritt;
                k.X = Math.Round(x, 3);
            }
        }

        /// <summary>Endgültige Lagen unter dem untersten Band (nach den letzten Zeilen): Spur-y der Linien und Knoten, Rechtecke der Schächte.</summary>
        void UntergrundLagen()
        {
            if (plan.Untergrund is not { } u || plan.Baender.Count == 0) return;
            u.Oben = Math.Round(plan.Baender[0].Unten, 3);
            foreach (var l in g.Linien.Where(l => l.Untergrund)) l.UntergrundY = u.SpurY(untergrundSpur.GetValueOrDefault(l));
            foreach (var k in g.Knoten.Where(k => k.Untergrund)) k.Y = u.SpurY(untergrundSpur.GetValueOrDefault(k));
            foreach (var s in u.Schaechte)
            {
                s.Oben = Math.Round(plan.Baender[0].Oben, 3);
                s.Unten = Math.Round(u.Oben + s.UntenRel, 3);
                Setzen(s.Knoten, s.X, (u.Oben + s.Unten) / 2, "frei");
            }
        }

        /// <summary>Unterkante des Symbols eines Knotens unter dem Knoten (mm, positiv): Sinnbild, Kästchen oder Schemasymbol der Familie.</summary>
        double SymbolUnten(SchemaKnoten k)
        {
            if (symbolUnten.TryGetValue(k.Id, out var u)) return u;
            var detail = plan.Ansicht.Text("detailgrad") ?? "mittel";
            var (prims, _, hy) = SchemaSymbole.Zeichnen(g.Projekt, k.Element, 0, 0, false, null, detail);
            var b = Grafik.Begrenzung([new Grafikteil { Primitive = prims.Where(x => x.Art != "text").ToList() }]);
            return symbolUnten[k.Id] = b is null ? hy : Math.Round(Math.Max(0, -b[1]), 3);
        }

        /// <summary>Steht das Element als Endgerät bzw. Gerät auf dem Boden seiner Zeile (nicht Ablauf, Schacht, im Block verdeckt)?</summary>
        static bool AufBoden(SchemaKnoten k) => IstGeraet(k) && k.Schacht is null && k.Ablaufband is null && !k.Verdeckt && k.Zeile is not null;

        /// <summary>Linie zwischen zwei Knoten (erste nach Reihenfolge der Linien).</summary>
        SchemaLinie? LinieZwischen(SchemaKnoten a, SchemaKnoten b) =>
            (linienAn.GetValueOrDefault(a.Id) ?? []).FirstOrDefault(l => Andere(l, a) == b);

        /// <summary>Boden je Zeile (Regeln im Dateikopf «Apparate auf dem Boden»), Anschlusslinien von waagrechten Leitungen zu Endgeräten,
        /// y der Endgeräte und der Abläufe.</summary>
        void BodenSetzen()
        {
            foreach (var l in g.Linien) l.AnschlussY = null;
            var jeZeile = g.Knoten.Where(AufBoden).GroupBy(k => k.Zeile!).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var (z, geraete) in jeZeile.OrderBy(x => x.Key.Index).Select(x => (x.Key, x.Value)))
            {
                var band = !z.IstZone && z.Index < plan.Baender.Count && plan.Baender[z.Index].Ueber == z ? plan.Baender[z.Index] : null;
                double bandOben = band?.Oben ?? z.Y0 + SchemaRaster.Deckenband / 2;
                var kand = new List<double> { bandOben };
                // Spuren der waagrechten Leitungen in der Zeile und Dämmungstexte (Oberkante der Schrift)
                var spur = g.Knoten.Where(k => k.Zeile == z && k.Ausrichtung == "waagrecht" && k.Band is null && !k.Untergrund).Select(k => k.Y).ToList();
                double spurOben = spur.Count > 0 ? spur.Max() : bandOben;
                kand.Add(spurOben);
                if (plan.Daemmungen.Any(d => d.Zeile == z)) kand.Add(z.Y0 + SchemaRaster.Daemmhoehe + 0.75 * SchemaRaster.Schrift);
                // Etagen- und Anschlusslinien: Höhe der Wurzel, deren Leitung zu einem Platz führt; liegt sie als Einlage im Band unter der
                // Zeile bzw. im Erdreich, gilt die Bandoberkante
                var abgaenge = new Dictionary<(SchemaKnoten Ziel, SchemaKnoten Wurzel), int>();
                foreach (var c in Zellen.Where(c => c.Zeile == z && !c.Gruppe.IstZone))
                {
                    foreach (var (_, baum) in c.Baeume)
                        foreach (var (k, w) in baum)
                        {
                            if (w.Vor is not { } v || baum[v].Vor is not null) continue;   // nur Kinder der Wurzel
                            bool platz = IstGeraet(k) && k.Schacht is null || c.Plaetze.TryGetValue(k, out var pl) && pl.Count > 0;
                            if (!platz) continue;
                            var l = LinieZwischen(v, k);
                            bool unten = band is not null && (l?.Band == band || k.Band == band || v.Band == band)
                                         || l?.Untergrund == true || k.Untergrund || v.Untergrund;
                            if (unten) kand.Add(bandOben);
                            else if (v.Ausrichtung == "waagrecht" && !anker.ContainsKey(v.Id) && AufBoden(k) && k.Zeile == z) { }   // Anschlusslinie (unten)
                            else kand.Add(v.Y);
                        }
                    // Knoten auf dem Abgang zu einem Endgerät: je Kette (Endgerät, Wurzel) ihre Zahl
                    foreach (var (k, _) in c.Rolle.Where(r => r.Value == "abgang"))
                        if (c.Plaetze[k].First() is SchemaKnoten ziel && AufBoden(ziel))
                        {
                            var schluessel = (ziel, c.WegVon(k)!.Wurzel);
                            abgaenge[schluessel] = abgaenge.GetValueOrDefault(schluessel) + 1;
                        }
                }
                // Anschlusslinien: Spurknoten (nicht unter einem Strang) direkt zu einem Endgerät der Zeile; überlappende übereinander
                var anschluss = new List<(SchemaLinie L, double Min, double Max)>();
                foreach (var d in geraete)
                    foreach (var l in linienAn.GetValueOrDefault(d.Id) ?? [])
                        if (Andere(l, d) is { Ausrichtung: "waagrecht" } s && s.Zeile == z && s.Band is null && !s.Untergrund && !anker.ContainsKey(s.Id)
                            && l.Band is null && !l.Untergrund && Math.Abs(s.X - d.X) > 1e-6 && !anschluss.Any(a => a.L == l))
                            anschluss.Add((l, Math.Min(s.X, d.X), Math.Max(s.X, d.X)));
                var enden = new List<double>();
                foreach (var (l, x0, x1) in anschluss.OrderBy(a => a.Min).ThenBy(a => a.Max).ThenBy(a => a.L.Elemente.Count > 0 ? a.L.Elemente[0].Kennzeichen : "", StringComparer.Ordinal))
                {
                    int t = enden.FindIndex(e => e + SchemaRaster.Spurluecke <= x0 + 1e-9);
                    if (t < 0) { t = enden.Count; enden.Add(double.MinValue); }
                    enden[t] = Math.Max(enden[t], x1);
                    l.AnschlussY = Math.Round(spurOben + (t + 1) * SchemaRaster.Anschlussstufe, 3);
                    kand.Add(l.AnschlussY.Value);
                }
                double unterkante = kand.Max() + SchemaRaster.Bodenabstand;
                foreach (var ((_, wurzel), n) in abgaenge) unterkante = Math.Max(unterkante, wurzel.Y + (n + 1) * SchemaRaster.Abgangschritt);
                foreach (var d in geraete)
                {
                    double ab = SymbolUnten(d), y = Math.Min(z.Y0 + SchemaRaster.Endgeraetehoehe, unterkante + ab);
                    d.Y = Math.Round(y, 3);
                    d.Unterkante = Math.Round(y - ab, 3);
                }
            }
            // Abläufe: Quadrat im Band, Oberkante bündig mit der Bandoberkante
            foreach (var k in ablaeufe.Where(k => k.Ablaufband is not null))
                k.Y = Math.Round(k.Ablaufband!.Oben - SchemaRaster.Ablaufgroesse / 2, 3);
        }

        // ------------------------------------------------------------------ Breiten

        static double BuendelRel(SchemaGruppe gr) =>
            SchemaRaster.Strangversatz + Math.Max(0, gr.Senkrecht.Count() - 1) * SchemaRaster.Buendelabstand;

        static double WaagrechtVersatz(SchemaGruppe gr) =>
            gr.Senkrecht.Any() ? BuendelRel(gr) + SchemaRaster.Endgeraetversatz : SchemaRaster.Strangversatz;

        /// <summary>Lage eines Blocks ab dem Platzanfang s: Eintritt, linker und rechter Rand.</summary>
        static (double Eintritt, double Links, double Rechts) BlockMasse(double s, int armaturplaetze)
        {
            double eintritt = s + 3, links = eintritt + (armaturplaetze + 1) * SchemaRaster.Armaturschritt - 3;
            return (eintritt, links, links + SchemaRaster.Blockbreite);
        }

        double Breite(SchemaGruppe gr)
        {
            double bedarf = 0, buendel = BuendelRel(gr);
            foreach (var z in plan.Zeilen)
            {
                if (!zellen.TryGetValue((gr.Index, z.Index), out var c)) continue;
                if (gr.IstZone)
                {
                    int n = c.Seite.Count;
                    if (n > 0) bedarf = Math.Max(bedarf, SchemaRaster.Strangversatz + (n - 1) * SchemaRaster.Endgeraetabstand + SchemaRaster.RandRechts);
                    continue;
                }
                double s = buendel + SchemaRaster.Endgeraetversatz - 3, rechts = 0;
                foreach (var platz in c.Folge)
                {
                    if (platz is SchemaBlock b)
                    {
                        var (_, _, r) = BlockMasse(s, b.Armaturplaetze);
                        rechts = r; s = r + SchemaRaster.Endgeraetabstand - 6;
                    }
                    else { rechts = s + 3; s += SchemaRaster.Endgeraetabstand; }
                }
                if (rechts > 0) bedarf = Math.Max(bedarf, rechts + SchemaRaster.RandRechts);
                int m = c.Spur.Count(k => !anker.ContainsKey(k.Id));
                if (m > 0) bedarf = Math.Max(bedarf, WaagrechtVersatz(gr) + (m - 1) * SchemaRaster.Waagrechtschritt + SchemaRaster.RandRechts);
            }
            if (plan.Daemmungen.Any(d => d.Gruppe == gr))
                bedarf = Math.Max(bedarf, buendel + 1.5 + SchemaRaster.TextBreite("Dämmung 50 mm", SchemaRaster.Schrift) + SchemaRaster.RandRechts);
            // Schächte unter der Gruppe (Nachtrag B8d)
            int schaechte = g.Knoten.Count(k => k.Schacht?.Gruppe == gr);
            if (schaechte > 0)
                bedarf = Math.Max(bedarf, buendel + SchemaRaster.Endgeraetversatz + (schaechte - 1) * SchemaRaster.Schachtabstand
                                          + SchemaRaster.Schachtbreite / 2 + SchemaRaster.RandRechts);
            if (gr.MitKreis)
                bedarf = Math.Max(bedarf, (SchemaRaster.Strangversatz + buendel) / 2 + Kreisradius(gr) + 1);
            return SchemaRaster.Spaltenbreite * Math.Max(1, Math.Ceiling(bedarf / SchemaRaster.Spaltenbreite - 1e-9));
        }

        static double Kreisradius(SchemaGruppe gr) =>
            Math.Max(SchemaRaster.KreisRadius, SchemaRaster.TextBreite(gr.Bezeichnung, SchemaRaster.KreisSchrift) / 2 + 1.2);

        // ------------------------------------------------------------------ Lagen

        void ZoneSetzen(Zelle c)
        {
            var s = c.Gruppe.Spalten[0];
            var seite = c.Seite.OfType<SchemaKnoten>().OrderBy(k => k.Element, SchemaGraph.Ordnung).Cast<object>().Concat(c.Seite.OfType<SchemaLinie>()).ToList();
            for (int i = 0; i < seite.Count; i++)
            {
                double xs = s.LinieX + i * SchemaRaster.Endgeraetabstand, ys = c.Zeile.Y0 + SchemaRaster.Endgeraetehoehe;
                if (seite[i] is SchemaKnoten k) Setzen(k, xs, ys, "frei");
                else if (seite[i] is SchemaLinie l) { l.X = xs; l.Y = ys; }
            }
        }

        void SteigSetzen(Zelle c)
        {
            var senk = c.Gruppe.Senkrecht.ToList();
            int n = senk.Count;
            var ebenen = new List<(SchemaKnoten K, int L)>();
            foreach (var (k, j) in c.Stufe) ebenen.Add((k, j * Math.Max(1, n) + k.Spalte!.Platz));
            var stufen = ebenen.Select(e => e.L).Distinct().Order().ToList();
            double stufe = n <= 1 ? SchemaRaster.Knotenschritt : Math.Max(SchemaRaster.Knotenschritt / n, SchemaRaster.Buendelstufe);
            double platz = SchemaRaster.Zeilenhoehe - SchemaRaster.KnotenOben - SchemaRaster.KnotenUnten;
            if (stufen.Count > 1) stufe = Math.Min(stufe, platz / (stufen.Count - 1));
            foreach (var (k, l) in ebenen)
                Setzen(k, k.Spalte!.LinieX, c.Zeile.Y0 + SchemaRaster.KnotenUnten + stufen.IndexOf(l) * stufe, "senkrecht");
        }

        void PlaetzeSetzen(Zelle c)
        {
            double s = c.Gruppe.BuendelRechts + SchemaRaster.Endgeraetversatz - 3, y0 = c.Zeile.Y0;
            foreach (var platz in c.Folge)
            {
                switch (platz)
                {
                    case SchemaBlock b:
                    {
                        var (eintritt, links, rechts) = BlockMasse(s, b.Armaturplaetze);
                        b.EintrittX = Math.Round(eintritt, 3);
                        b.X = Math.Round(links, 3);
                        b.Y = Math.Round(y0 + SchemaRaster.Blockunten, 3);
                        b.Hoehe = SchemaRaster.Zeilenhoehe - SchemaRaster.Blockunten - SchemaRaster.Blockoben;
                        double frei = b.Hoehe - 11;   // Titel, Kopfzeile, Strich, Total, Rand
                        b.Zeilenabstand = Math.Round(Math.Min(2.6, frei / Math.Max(1, b.Zeilen.Count)), 3);
                        s = rechts + SchemaRaster.Endgeraetabstand - 6;
                        break;
                    }
                    case SchemaKnoten k:
                        Setzen(k, s + 3, y0 + SchemaRaster.Endgeraetehoehe, "frei");
                        s += SchemaRaster.Endgeraetabstand;
                        break;
                    case SchemaLinie l:
                        l.X = Math.Round(s + 3, 3); l.Y = y0 + SchemaRaster.Endgeraetehoehe;
                        s += SchemaRaster.Endgeraetabstand;
                        break;
                }
            }
        }

        /// <summary>Spuren der waagrechten Stränge je Zeile nach Art, dann Stranglinie; Knoten unter ihrem Anker oder ab der eigenen
        /// Linie im Waagrechtschritt nach Fliessrang.</summary>
        void SpurenSetzen()
        {
            var jeSpalte = zellen.Values.SelectMany(c => c.Spur).GroupBy(k => (k.Spalte!.Index, k.Zeile!.Index))
                .ToDictionary(x => x.Key, x => x.ToList());
            foreach (var z in plan.Zeilen)
            {
                var alle = plan.Spalten.Where(s => s.Art == "waagrecht" && jeSpalte.ContainsKey((s.Index, z.Index))).ToList();
                // ein Strang, dessen Knoten in der Zeile alle im Erdreich liegen (unter dem untersten Band, B8d), belegt hier keine Spur
                var spuren = alle.Where(s => jeSpalte[(s.Index, z.Index)].Any(k => !k.Untergrund))
                    .OrderBy(s => Array.IndexOf(Waagrecht, s.Strang!.Text("art"))).ThenBy(s => s.Index).ToList();
                double platz = SchemaRaster.KnotenUnten - SchemaRaster.Spurabstand - SchemaRaster.SpurUnten;
                double abstand = spuren.Count > 1 ? Math.Min(SchemaRaster.Spurabstand, platz / (spuren.Count - 1)) : 0;
                var reihe = spuren.Select((s, t) => (S: s, Y: z.Y0 + SchemaRaster.SpurUnten + t * abstand))
                    .Concat(alle.Except(spuren).Select(s => (S: s, Y: z.Y0 + SchemaRaster.SpurUnten)));
                foreach (var (s, y) in reihe)
                {
                    var knoten = jeSpalte[(s.Index, z.Index)].OrderBy(k => k.Rang).ThenBy(k => k.Element, SchemaGraph.Ordnung).ToList();
                    int j = 0;
                    var jeAnker = new Dictionary<int, int>();
                    foreach (var k in knoten)
                    {
                        if (anker.TryGetValue(k.Id, out var a))
                        {
                            int n = jeAnker.GetValueOrDefault(a.Index);
                            jeAnker[a.Index] = n + 1;
                            Setzen(k, a.LinieX + n * SchemaRaster.Ankerversatz, y, "waagrecht");
                            k.Anker = a;
                        }
                        else Setzen(k, s.LinieX + j++ * SchemaRaster.Waagrechtschritt, y, "waagrecht");
                    }
                }
            }
        }

        /// <summary>Versatz der Linie eines Bündelstrangs am Endgerät (Linien verschiedener Stränge nebeneinander).</summary>
        static double Versatz(SchemaKnoten endgeraet, SchemaSpalte? strang)
        {
            if (strang is null || endgeraet.Spalte?.Gruppe is not { } gr || strang.Gruppe != gr || strang.Art != "senkrecht") return 0;
            int n = gr.Senkrecht.Count();
            if (n <= 1) return 0;
            double schritt = Math.Min(SchemaRaster.Anschlussversatz, 4.5 / (n - 1));
            return (strang.Platz - (n - 1) / 2.0) * schritt;
        }

        /// <summary>x, auf dem ein Etagenknoten der Stranglinie <paramref name="sp"/> einen Platz anfährt.</summary>
        static double EintrittX(object platz, SchemaSpalte? sp) => platz switch
        {
            SchemaBlock b => b.EintrittX,
            SchemaKnoten k => k.X + Versatz(k, sp),
            _ => 0,
        };

        void EtageSetzen(Zelle c)
        {
            Weg W(SchemaKnoten k) => c.WegVon(k)!;
            double Yw(SchemaKnoten k) => W(k).Wurzel.Y;
            var etage = c.Rolle.Keys.OrderBy(k => W(k).Abstand).ThenBy(k => k.Element, SchemaGraph.Ordnung).ToList();

            // geteilt: auf der Etagenleitung unter dem ersten Platz dahinter; Ketten mit demselben Anker gleichmässig verteilt
            var geteilt = etage.Where(k => c.Rolle[k] == "geteilt")
                .Select(k => (K: k, A: Math.Round(c.Plaetze[k].Min(p => EintrittX(p, k.Spalte)), 3)))
                .GroupBy(x => (x.A, W(x.K).Wurzel)).OrderBy(x => x.Key.A).ThenBy(x => x.Key.Wurzel.Element, SchemaGraph.Ordnung);
            foreach (var kette in geteilt)
            {
                var ks = kette.Select(x => x.K).OrderBy(k => W(k).Abstand).ToList();
                var vor = W(ks[0]).Vor;
                double xp = vor?.X ?? c.Gruppe.BuendelRechts, a = kette.Key.A;
                for (int i = 0; i < ks.Count; i++)
                    Setzen(ks[i], xp + (a - xp) * (i + 1) / ks.Count, Yw(ks[i]), "etage");
            }
            // Abgang: senkrecht über dem Endgerät, gleichmässig zwischen Etagenleitung und Endgerät
            foreach (var kette in etage.Where(k => c.Rolle[k] == "abgang").GroupBy(k => (P: c.Plaetze[k].First(), W(k).Wurzel)))
            {
                var ks = kette.ToList();
                var ziel = (SchemaKnoten)kette.Key.P;
                // Endgerät auf dem Boden (B8d): zwischen Etagenleitung und Unterkante seines Symbols
                double x = ziel.X + Versatz(ziel, ks[0].Spalte), yw = Yw(ks[0]), yz = ziel.Unterkante ?? ziel.Y;
                for (int i = 0; i < ks.Count; i++)
                    Setzen(ks[i], x, yw + (yz - yw) * (i + 1) / (ks.Count + 1), "abgang");
            }
            // Blöcke: Anschlussarmaturen in den Armaturplätzen vor dem Block, Inneres am Blockrand
            foreach (var k in etage.Where(k => c.Rolle[k] is "anschluss" or "innen"))
            {
                var b = k.Block!;
                var an = b.Anschluesse.First(x => x.Wurzel == W(k).Wurzel);
                if (c.Rolle[k] == "anschluss")
                {
                    int platz = b.Armaturplaetze - an.Armaturen.Count + an.Armaturen.IndexOf(k);
                    Setzen(k, b.EintrittX + (platz + 1) * SchemaRaster.Armaturschritt, Yw(k), "etage");
                }
                else Setzen(k, b.X, Math.Clamp(Yw(k), b.Y, b.Y + b.Hoehe), "frei");
            }
            // ohne Endgerät dahinter: Etagenschritt rechts des Vorgängers
            foreach (var k in etage.Where(k => c.Rolle[k] == "tot"))
            {
                var vor = W(k).Vor;
                double xp = vor is null || c.Steig.Contains(vor) ? c.Gruppe.BuendelRechts : vor.X;
                Setzen(k, xp + SchemaRaster.Etagenschritt, Yw(k), "etage");
            }
            // Endgeräte im Block: am Blockrand auf ihrer Anschlusslinie; Höhen der Anschlusslinien
            foreach (var b in plan.Bloecke.Where(b => b.Gruppe == c.Gruppe && b.Zeile == c.Zeile))
            {
                foreach (var e in b.Endgeraete)
                {
                    var w = c.Strangfolge.Select(s => c.Baeume[s].GetValueOrDefault(e)).FirstOrDefault(x => x is not null);
                    Setzen(e, b.X, w is not null ? Math.Clamp(w.Wurzel.Y, b.Y, b.Y + b.Hoehe) : b.Y + 2, "frei");
                }
            }
            foreach (var b in plan.Bloecke)
                foreach (var an in b.Anschluesse) an.Y = an.Wurzel.Y;
        }

        // ------------------------------------------------------------------ Kopf

        void KopfSetzen()
        {
            var normale = plan.Zeilen.Where(z => !z.IstZone).ToList();
            double oben = normale.Count == 0 ? SchemaRaster.Zeilenhoehe : normale.Max(z => z.Y0 + z.Hoehe);
            var kreise = plan.Gruppen.Where(gr => gr.MitKreis).ToList();
            double rmax = kreise.Count == 0 ? 0 : kreise.Max(Kreisradius);
            // Oberkante des obersten Deckenbands (wächst es mit Einlagen, rückt der Kopf nach; Nachtrag B8c)
            double bandOben = plan.Baender.Count > 0 ? plan.Baender[^1].Oben : oben + SchemaRaster.Deckenband / 2;
            double y = bandOben + SchemaRaster.Kopfabstand + rmax;
            foreach (var gr in kreise)
            {
                gr.KreisRadius = Math.Round(Kreisradius(gr), 3);
                gr.KreisX = Math.Round(gr.Senkrecht.Average(s => s.LinieX), 3);
                gr.KreisY = Math.Round(y, 3);
            }
            plan.KopfOben = Math.Round(Math.Max(bandOben - SchemaRaster.Deckenband / 2 + 12, kreise.Count == 0 ? 0 : y + rmax + 1), 3);
        }

        // ------------------------------------------------------------------ Linien

        static readonly (double X, double Y)[] RichtungSenkrecht = [(0, -1), (0, 1), (1, 0), (-1, 0)];
        static readonly (double X, double Y)[] RichtungWaagrecht = [(-1, 0), (1, 0), (0, 1), (0, -1)];
        static readonly (double X, double Y)[] RichtungFrei = [(1, 0), (0, 1), (-1, 0), (0, -1)];

        /// <summary>Lage eines Linienendes am Knoten: im Block am Blockrand auf der Höhe der Gegenseite, am Endgerät um den
        /// Anschlussversatz des Bündelstrangs verschoben, sonst die Lage des Knotens.</summary>
        (double X, double Y) Endpunkt(SchemaKnoten k, SchemaKnoten? gegen, Element? strang)
        {
            if (!k.Manuell && k.Verdeckt && k.Block is { } b)
            {
                // Gegenseite ausserhalb: auf ihrer Höhe; beide im Block: auf der Anschlusslinie des Knotens, der kein Endgerät ist
                bool beideImBlock = gegen is not null && gegen.Verdeckt && gegen.Block == b && !gegen.Manuell;
                double y = beideImBlock ? (IstGeraet(gegen!) ? k.Y : gegen!.Y)
                    : gegen is not null && !gegen.Manuell && gegen.Zeile == k.Zeile ? gegen.Y : k.Y;
                return (b.X, Math.Round(Math.Clamp(y, b.Y, b.Y + b.Hoehe), 3));
            }
            if (!k.Manuell && k.Ausrichtung == "frei" && IstGeraet(k) && strang is not null && strangSpalte.TryGetValue(strang.Id, out var sp))
                return (Math.Round(k.X + Versatz(k, sp), 3), k.Y);
            return (k.X, k.Y);
        }

        /// <summary>Lage der Enden und orthogonaler Linienzug einer Linie.</summary>
        void Fuehren(SchemaLinie l)
        {
            if (l.Frei)
            {
                l.Von.X = l.X - SchemaRaster.Freilaenge / 2; l.Von.Y = l.Y;
                l.Nach.X = l.X + SchemaRaster.Freilaenge / 2; l.Nach.Y = l.Y;
                l.Punkte = [[l.Von.X, l.Von.Y], [l.Nach.X, l.Nach.Y]];
                return;
            }
            foreach (var (en, gegen) in new[] { (l.Von, l.Nach), (l.Nach, l.Von) })
            {
                if (en.Knoten is { } k) { (en.X, en.Y) = Endpunkt(k, gegen.Knoten, l.Strang); continue; }
                // offenes Ende: neben dem Knoten am anderen Ende, Richtung nach dessen Anschlussnummer
                var kn = gegen.Knoten!;
                int i = Math.Max(0, kn.Element.Anschluesse.FindIndex(a => a.Name.Equals(gegen.Anschluss, StringComparison.OrdinalIgnoreCase)));
                var richtungen = kn.Ausrichtung switch { "senkrecht" or "abgang" => RichtungSenkrecht, "waagrecht" or "etage" => RichtungWaagrecht, _ => RichtungFrei };
                var (dx, dy) = richtungen[i % richtungen.Length];
                if (kn.Ausrichtung == "senkrecht" && !Aufwaerts(kn.Spalte?.Strang)) dy = -dy;   // Fallstrang: Anschluss a oben
                en.X = Math.Round(kn.X + dx * SchemaRaster.Offenlaenge, 3);
                en.Y = Math.Round(kn.Y + dy * SchemaRaster.Offenlaenge, 3);
            }
            // Schacht (Nachtrag B8d): Einlauf seitlich an der Wand auf der Grundleitungsspur bzw. von oben bis zur Sohle
            foreach (var (en, gegen) in new[] { (l.Von, l.Nach), (l.Nach, l.Von) })
                if (en.Knoten is { Manuell: false, Schacht: { } s })
                {
                    if (l.Untergrund) { en.X = WandX(s, gegen.X); en.Y = l.UntergrundY; }
                    else { en.X = Math.Abs(gegen.X - s.X) < s.Breite / 2 - 0.5 ? gegen.X : Math.Round(s.X - s.Breite / 4, 3); en.Y = s.Sohle; }
                }
            // Einlagen (Nachtrag B8c): durchs Band; andere Linien mit genau einem Ende im Band: waagrecht beim anderen Ende
            if (l.Band is not null) { l.Punkte = DurchsBand(l.Von, l.Nach, l.BandY); return; }
            // unter dem untersten Band (B8d): senkrecht hinunter, waagrecht auf der Grundleitungsspur, senkrecht bzw. seitlich zum anderen Ende
            if (l.Untergrund) { l.Punkte = DurchsBand(l.Von, l.Nach, l.UntergrundY); return; }
            // Anschlusslinie von einer waagrechten Leitung zu einem Endgerät auf dem Boden (B8d): senkrecht, waagrecht unter den Endgeräten, senkrecht
            if (l.AnschlussY is double ya) { l.Punkte = Vereinfacht([[l.Von.X, l.Von.Y], [l.Von.X, ya], [l.Nach.X, ya], [l.Nach.X, l.Nach.Y]]); return; }
            bool va = l.Von.Knoten is { Band: not null, Manuell: false }, vb = l.Nach.Knoten is { Band: not null, Manuell: false };
            if (va != vb)
            {
                // steht das andere Ende als Endgerät auf dem Boden (B8d), verlässt die Linie es senkrecht nach unten ins Band
                var anderes = va ? l.Nach : l.Von;
                l.Punkte = anderes.Knoten is { Unterkante: not null, Manuell: false } ? GeraetInsBand(l.Von, l.Nach, va) : EinEndeImBand(l.Von, l.Nach, va);
                return;
            }
            // Ablauf im Band (B8d): die Linie verlässt den Ablauf senkrecht
            var ablauf = l.Von.Knoten is { Manuell: false, Ablaufband: not null } ? l.Von : l.Nach.Knoten is { Manuell: false, Ablaufband: not null } ? l.Nach : null;
            if (ablauf is not null) { l.Punkte = AblaufZug(l, ablauf); return; }
            l.Punkte = Orthogonal(l.Von, l.Nach);
        }

        /// <summary>Linie von einem Endgerät auf dem Boden zu einem Knoten im Band (keine Einlage): senkrecht vom Gerät ins Band, dort
        /// waagrecht zum Knoten (Nachtrag B8d; sonst liefe sie auf der Höhe des Geräts durch die Nachbarn).</summary>
        static List<double[]> GeraetInsBand(SchemaEnde a, SchemaEnde b, bool aImBand) =>
            Vereinfacht(aImBand ? [[a.X, a.Y], [b.X, a.Y], [b.X, b.Y]] : [[a.X, a.Y], [a.X, b.Y], [b.X, b.Y]]);

        /// <summary>Linie an einem Ablauf im Band (Nachtrag B8d): senkrecht aus dem Ablauf – nach oben bis auf die Höhe des anderen Endes,
        /// nach unten bis die Ablaufstichlänge unter das Band –, dann waagrecht und senkrecht zum anderen Ende.</summary>
        static List<double[]> AblaufZug(SchemaLinie l, SchemaEnde ablauf)
        {
            var o = ablauf == l.Von ? l.Nach : l.Von;
            var bd = ablauf.Knoten!.Ablaufband!;
            double yk = bd.Unten - SchemaRaster.Ablaufstich;
            List<double[]> pts = Math.Abs(ablauf.X - o.X) < 1e-6 ? [[ablauf.X, ablauf.Y], [o.X, o.Y]]
                : o.Y >= yk - 1e-6 ? [[ablauf.X, ablauf.Y], [ablauf.X, o.Y], [o.X, o.Y]]
                : [[ablauf.X, ablauf.Y], [ablauf.X, yk], [o.X, yk], [o.X, o.Y]];
            if (ablauf == l.Nach) pts.Reverse();
            return Vereinfacht(pts);
        }

        static string Ausrichtung(SchemaEnde e) => e.Knoten?.Ausrichtung ?? "frei";

        /// <summary>Orthogonaler Zug zwischen zwei Enden (Regeln im Dateikopf).</summary>
        static List<double[]> Orthogonal(SchemaEnde a, SchemaEnde b)
        {
            double[] p = [a.X, a.Y], q = [b.X, b.Y];
            if (Math.Abs(a.X - b.X) < 1e-6 || Math.Abs(a.Y - b.Y) < 1e-6) return [p, q];
            string ra = Ausrichtung(a), rb = Ausrichtung(b);
            bool gleicheZeile = a.Knoten?.Zeile is { } za && za == b.Knoten?.Zeile;
            // Spurknoten unter einem Steigstrang und freier Knoten: der Spur entlang, dann senkrecht zum Gerät (nicht am Strang)
            bool AnkerUndFrei(SchemaEnde x, SchemaEnde y) => x.Knoten?.Anker is not null && Ausrichtung(x) == "waagrecht" && Ausrichtung(y) == "frei";
            if (AnkerUndFrei(a, b)) return [p, [b.X, a.Y], q];
            if (AnkerUndFrei(b, a)) return [p, [a.X, b.Y], q];
            double[] ecke =
                ra == "abgang" ? [a.X, b.Y] :
                rb == "abgang" ? [b.X, a.Y] :
                ra == "etage" ? [b.X, a.Y] :
                rb == "etage" ? [a.X, b.Y] :
                ra == "waagrecht" && rb == "waagrecht" ? [b.X, a.Y] :
                ra == "waagrecht" ? (rb == "senkrecht" ? [b.X, a.Y] : [a.X, b.Y]) :
                rb == "waagrecht" ? (ra == "senkrecht" ? [a.X, b.Y] : [b.X, a.Y]) :
                ra == "senkrecht" && rb == "senkrecht" ? [a.X, b.Y] :
                ra == "senkrecht" ? (gleicheZeile ? [b.X, a.Y] : [a.X, b.Y]) :
                rb == "senkrecht" ? (gleicheZeile ? [a.X, b.Y] : [b.X, a.Y]) :
                [b.X, a.Y];
            return [p, ecke, q];
        }

        void HinweiseSammeln()
        {
            static string Liste(IEnumerable<Element> l)
            {
                var kz = l.OrderBy(e => e, SchemaGraph.Ordnung).Select(e => e.Kennzeichen).ToList();
                return string.Join(", ", kz.Take(12)) + (kz.Count > 12 ? $" … ({kz.Count})" : "");
            }
            var ohneGeschoss = g.Knoten.Where(k => k.Zeile?.IstZone == true).Select(k => k.Element).ToList();
            if (ohneGeschoss.Count > 0)
                plan.Hinweise.Add($"Ohne Geschoss (Zone unten): {Liste(ohneGeschoss)} – geschoss setzen (element_aendern).");
            var ohneStrang = g.Knoten.Where(k => k.Spalte?.IstZone == true && !IstGeraet(k)).Select(k => k.Element).ToList();
            if (ohneStrang.Count > 0)
                plan.Hinweise.Add($"Ohne Strang (Spalte «ohne Strang»): {Liste(ohneStrang)} – teilVon auf einen Strang setzen (element_aendern).");
            var lose = g.Knoten.Where(k => k.Spalte?.IstZone == true && IstGeraet(k)).Select(k => k.Element).ToList();
            if (lose.Count > 0)
                plan.Hinweise.Add($"An keinen Strang angeschlossen: {Liste(lose)} – mit einer Leitung des Strangs verbinden.");
            var nurSchema = g.Knoten.Where(k => !k.Platziert).Select(k => k.Element)
                .Concat(g.Linien.SelectMany(l => l.Elemente).Where(e => e.Geometrie is null)).ToList();
            if (nurSchema.Count > 0)
                plan.Hinweise.Add($"Nur im Schema (nicht platziert): {Liste(nurSchema)} – im Grundriss platzieren (element_aendern mit geometrie bzw. familie_platzieren).");
            // Modul 3, B9b: Rohre ohne Grösse (Abwasser ohne da, übrige ohne DN) bekommen keine Beschriftung – hier steht warum
            foreach (var h in Rohrgroessen.Hinweise(g.Projekt, g.Linien.SelectMany(l => l.Elemente).Where(e => g.Katalog.IstArt(e, "Leitung")).Distinct()))
                plan.Hinweise.Add(h);
            var ohneLage = plan.Spalten.Where(s => s.Strang is not null && s.Lage is null).Select(s => s.Strang!).ToList();
            if (ohneLage.Count > 0)
                plan.Hinweise.Add($"Ohne Lage im Gebäude (rechts nach Bezeichnung angeordnet): {Liste(ohneLage)} – Leitungen des Strangs im Grundriss platzieren.");
            var eng = plan.Bloecke.Where(b => b.Zeilenabstand < 1.8).ToList();
            if (eng.Count > 0)
                plan.Hinweise.Add($"Wohnungsblock mit vielen Apparatearten eng gesetzt: {string.Join(", ", eng.Select(b => $"{b.Titel} ({b.Zeile.Titel})"))}.");
            if (plan.VerwaisteLagen.Count > 0)
                plan.Hinweise.Add($"{plan.VerwaisteLagen.Count} Hand-Lage(n) ohne Element im Schema – schema_anordnen räumt sie auf.");
            // Einlagen (Nachtrag B8c)
            var mitEinlagen = plan.Baender.Where(b => b.Spuren.Count > 0).ToList();
            if (mitEinlagen.Count > 0)
                plan.Hinweise.Add($"Einlagen im Deckenband: {g.Linien.Count(l => l.Band is not null)} Linie(n) – " +
                                  string.Join(", ", mitEinlagen.Select(b => $"{b.Titel} ({b.Spuren.Count} Spur{(b.Spuren.Count == 1 ? "" : "en")})")) +
                                  " (Verlegung: Abfrage verlegung, von Hand: element_aendern verlegung).");
            if (gemischt.Count > 0)
                plan.Hinweise.Add($"Teils Einlage, teils frei – nach der Mehrheit der Länge gezeigt: {Liste(gemischt.Select(l => l.Elemente.First(e => K.IstArt(e, "Leitung"))))}.");
            if (wandEinlagen.Count > 0)
                plan.Hinweise.Add($"Einlagen in Wänden bleiben an ihrer Lage (kein Deckenband): {Liste(wandEinlagen.Select(l => l.Elemente.First(e => K.IstArt(e, "Leitung"))))}.");
            // Grundleitungen und Schächte (Nachtrag B8d)
            if (plan.Untergrund is { } ug)
            {
                var sch = ug.Schaechte.Select(s => s.Knoten.Element).ToList();
                int n = g.Linien.Count(l => l.Untergrund), m = ug.Spuren.Count(x => x.Length > 0);   // belegte Spuren
                plan.Hinweise.Add($"Unter dem untersten Band: {n} Linie(n) auf {m} Grundleitungsspur{(m == 1 ? "" : "en")}" +
                                  (sch.Count > 0 ? $", Schächte im Schnitt: {Liste(sch)}" : "") + " (Erdreich: Abfrage verlegung).");
                if (schachtKreuzungen.Count > 0)
                    plan.Hinweise.Add($"Linie unter dem Band quert einen Schacht: {Liste(schachtKreuzungen.Distinct().Select(l => l.Elemente.Count > 0 ? l.Elemente[0] : l.Von.Element))} – Schacht von Hand verschieben (schema_lage).");
            }
            plan.Hinweise.AddRange(g.Hinweise);
        }
    }

    // ------------------------------------------------------------------ Ausgabe (Abfrage «schema»)

    static double R(double v) => Math.Round(v, 2);
    static JsonArray Punkt(double x, double y) => new(R(x), R(y));
    static JsonArray Feld(IEnumerable<JsonNode?> n) => new(n.ToArray());

    /// <summary>Das Schema als JSON für Oberfläche und KI (Abfrage «schema»).</summary>
    public JsonObject AlsJson()
    {
        var m = Graph.Modell;
        string? Kz(string? id) => id is null ? null : m.Finde(id)?.Kennzeichen;
        var o = new JsonObject
        {
            ["ansicht"] = Ansicht.Kennzeichen,
            ["schemaart"] = Schemaart,
            ["blickrichtung"] = Blickrichtung,
            ["wohnungsbloecke"] = Wohnungsbloecke,
            ["massstab"] = 1,
            ["stand"] = m.Pruefsumme(),
            ["raster"] = SchemaRaster.AlsJson(),
            ["zeilen"] = Feld(Zeilen.OrderBy(z => z.Y0).Select(z => (JsonNode)new JsonObject
            {
                ["geschoss"] = z.Geschoss?.Id, ["kennzeichen"] = z.Geschoss?.Kennzeichen, ["name"] = z.Geschoss?.Name ?? (z.IstZone ? "ohne Geschoss" : null),
                ["kote"] = z.Geschoss?.Zahl("kote"), ["y"] = R(z.Y0), ["hoehe"] = R(z.Hoehe), ["zone"] = z.IstZone,
            })),
            ["gruppen"] = Feld(Gruppen.Select(gr =>
            {
                var go = new JsonObject
                {
                    ["index"] = gr.Index, ["art"] = gr.Art, ["bezeichnung"] = gr.Bezeichnung, ["steigzone"] = gr.Steigzone,
                    ["x"] = R(gr.X0), ["breite"] = R(gr.Breite),
                    ["straenge"] = Feld(gr.Spalten.Where(s => s.Strang is not null).Select(s => (JsonNode)JsonValue.Create(s.Strang!.Kennzeichen))),
                    ["lage"] = gr.Lage is { } l ? Punkt(l[0], l[1]) : null,
                };
                if (gr.MitKreis) go["kreis"] = new JsonObject { ["x"] = R(gr.KreisX), ["y"] = R(gr.KreisY), ["radius"] = R(gr.KreisRadius) };
                return (JsonNode)go;
            })),
            ["spalten"] = Feld(Spalten.Select(s => (JsonNode)new JsonObject
            {
                ["strang"] = s.Strang?.Id, ["kennzeichen"] = s.Strang?.Kennzeichen, ["name"] = s.Strang?.Name ?? s.Titel,
                ["system"] = s.System?.Kennzeichen, ["art"] = s.Art, ["strangart"] = s.Strang?.Text("art"),
                ["gruppe"] = s.Gruppe.Index, ["platz"] = s.Platz, ["steigzone"] = s.Gruppe.Steigzone,
                ["x"] = R(s.X0), ["breite"] = R(s.Breite), ["linie"] = R(s.LinieX),
                ["lage"] = s.Lage is { } l ? Punkt(l[0], l[1]) : null,
            })),
            ["knoten"] = Feld(Graph.Knoten.Select(k =>
            {
                var ko = new JsonObject
                {
                    ["element"] = k.Id, ["kennzeichen"] = k.Kennzeichen, ["klasse"] = k.Element.Klasse, ["art"] = k.Art,
                    ["x"] = R(k.X), ["y"] = R(k.Y), ["manuell"] = k.Manuell, ["platziert"] = k.Platziert,
                    ["symbol"] = SchemaSymbole.Schluessel(Graph.Projekt, k.Element), ["ausrichtung"] = k.Ausrichtung,
                    ["strang"] = k.Spalte?.Strang?.Kennzeichen, ["system"] = Kz(k.Element.System) ?? k.Spalte?.System?.Kennzeichen,
                    ["geschoss"] = k.Zeile?.Geschoss?.Kennzeichen, ["spalte"] = k.Spalte?.Index, ["zeile"] = k.Zeile?.Index,
                    ["gruppe"] = k.Spalte?.Gruppe.Index,
                };
                if (k.Block is { } b) { ko["block"] = b.Zone.Kennzeichen; ko["verdeckt"] = k.Verdeckt && !k.Manuell; }
                if (k.Band is { } bd) ko["einlage"] = bd.Index;
                if (k.Ablaufband is { } ab) ko["ablauf"] = ab.Index;
                if (k.Untergrund) ko["untergrund"] = true;
                if (k.Unterkante is double uk) ko["unterkante"] = R(uk);
                if (k.Schacht is { } s)
                    ko["schacht"] = new JsonObject { ["x"] = R(s.X), ["oben"] = R(s.Oben), ["unten"] = R(s.Unten), ["breite"] = R(s.Breite), ["sohle"] = R(s.Sohle) };
                return (JsonNode)ko;
            })),
            ["linien"] = Feld(Graph.Linien.Select(l =>
            {
                var md = l.Medium is not null && Graph.Katalog.Medien.TryGetValue(l.Medium, out var x) ? x : null;
                var lo = new JsonObject
                {
                    ["elemente"] = Feld(l.Elemente.Select(e => (JsonNode)JsonValue.Create(e.Id))),
                    ["kennzeichen"] = Feld(l.Elemente.Select(e => (JsonNode)JsonValue.Create(e.Kennzeichen))),
                    ["von"] = l.Von.Knoten?.Id, ["vonAnschluss"] = l.Von.Knoten is null ? null : l.Von.Anschluss,
                    ["nach"] = l.Nach.Knoten?.Id, ["nachAnschluss"] = l.Nach.Knoten is null ? null : l.Nach.Anschluss,
                    ["punkte"] = Feld(l.Punkte.Select(p => (JsonNode)Punkt(p[0], p[1]))),
                    ["groessen"] = Feld(l.Groessen.Select(t => (JsonNode)JsonValue.Create(t))),
                    ["medium"] = l.Medium, ["farbe"] = HlksEinordnung.Farbe(md), ["linienart"] = HlksEinordnung.Linienart(md),
                    ["strang"] = l.Strang?.Kennzeichen,
                };
                if (l.Verbindung is not null) lo["verbindung"] = l.Verbindung;
                if (l.Band is { } bd) lo["einlage"] = new JsonObject { ["band"] = bd.Index, ["y"] = R(l.BandY), ["anteil"] = Math.Round(l.Einlageanteil * 100) };
                else if (l.Einlageanteil > 0) lo["einlageanteil"] = Math.Round(l.Einlageanteil * 100);
                if (l.Untergrund) lo["untergrund"] = new JsonObject { ["y"] = R(l.UntergrundY), ["erdreichanteil"] = Math.Round(l.Erdreichanteil * 100) };
                else if (l.Erdreichanteil > 0) lo["erdreichanteil"] = Math.Round(l.Erdreichanteil * 100);
                if (l.AnschlussY is double ay) lo["anschlussY"] = R(ay);
                var offen = new JsonArray();
                foreach (var (name, en) in new[] { ("von", l.Von), ("nach", l.Nach) })
                    if (en.Knoten is null)
                        offen.Add(new JsonObject { ["ende"] = name, ["element"] = en.Element.Id, ["kennzeichen"] = en.Element.Kennzeichen, ["anschluss"] = en.Anschluss, ["grund"] = en.Offen, ["x"] = R(en.X), ["y"] = R(en.Y) });
                if (offen.Count > 0) lo["offen"] = offen;
                return (JsonNode)lo;
            })),
            ["bloecke"] = Feld(Bloecke.Select(b => (JsonNode)new JsonObject
            {
                ["zone"] = b.Zone.Id, ["kennzeichen"] = b.Zone.Kennzeichen, ["titel"] = b.Titel,
                ["geschoss"] = b.Zeile.Geschoss?.Kennzeichen, ["gruppe"] = b.Gruppe.Index,
                ["x"] = R(b.X), ["y"] = R(b.Y), ["breite"] = R(b.Breite), ["hoehe"] = R(b.Hoehe),
                ["spalten"] = Feld(SchemaBlock.Spalten.Select(s => (JsonNode)new JsonObject { ["name"] = s.Name, ["titel"] = s.Titel, ["x"] = s.X, ["gezeigt"] = s.Gezeigt })),
                ["zeilen"] = Feld(b.Zeilen.Select(z => (JsonNode)new JsonObject
                {
                    ["klasse"] = z.Klasse, ["kuerzel"] = z.Kuerzel, ["stueck"] = z.Stueck, ["lu"] = z.Lu, ["kw"] = z.Kw, ["ww"] = z.Ww,
                    ["elemente"] = Feld(z.Elemente.Select(e => (JsonNode)JsonValue.Create(e.Kennzeichen))),
                })),
                ["total"] = new JsonObject { ["stueck"] = b.Stueck, ["lu"] = b.Lu, ["kw"] = b.Kw, ["ww"] = b.Ww },
                ["anschluesse"] = Feld(b.Anschluesse.Select(a => (JsonNode)new JsonObject
                {
                    ["strang"] = a.Strang?.Kennzeichen, ["wurzel"] = a.Wurzel.Kennzeichen, ["y"] = R(a.Y),
                    ["armaturen"] = Feld(a.Armaturen.Select(k => (JsonNode)JsonValue.Create(k.Kennzeichen))),
                })),
            })),
            ["daemmung"] = Feld(Daemmungen.Select(d => (JsonNode)new JsonObject
            {
                ["text"] = d.Text, ["dicke"] = d.Dicke, ["gruppe"] = d.Gruppe.Index, ["geschoss"] = d.Zeile.Geschoss?.Kennzeichen,
                ["straenge"] = Feld(d.Spalten.Select(s => (JsonNode)JsonValue.Create(s.Strang!.Kennzeichen))), ["x"] = R(d.XText), ["y"] = R(d.Y),
            })),
            ["baender"] = Feld(Baender.Select(b => (JsonNode)new JsonObject
            {
                ["index"] = b.Index, ["titel"] = b.Titel, ["unter"] = b.Unter?.Geschoss?.Kennzeichen, ["ueber"] = b.Ueber?.Geschoss?.Kennzeichen,
                ["y"] = R(b.Mitte), ["hoehe"] = R(b.Hoehe),
                ["spuren"] = Feld(b.Spuren.Select(s => (JsonNode)JsonValue.Create(s))),
            })),
            ["hinweise"] = Feld(Hinweise.Select(h => (JsonNode)JsonValue.Create(h))),
        };
        if (Untergrund is { } u)
            o["untergrund"] = new JsonObject
            {
                ["y"] = R(u.Oben), ["hoehe"] = R(u.Hoehe),
                ["spuren"] = Feld(u.Spuren.Select(s => (JsonNode)JsonValue.Create(s))),
                ["schaechte"] = Feld(u.Schaechte.Select(s => (JsonNode)JsonValue.Create(s.Knoten.Kennzeichen))),
            };
        if (VerwaisteLagen.Count > 0) o["verwaisteLagen"] = Feld(VerwaisteLagen.Select(i => (JsonNode)JsonValue.Create(i)));
        return o;
    }
}
