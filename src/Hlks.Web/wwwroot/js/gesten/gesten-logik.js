// Modul G «Gesten am Arbeitsplatz» (Kamera), Stufe 1: die Rechenregeln, rein – ohne DOM, Kamera oder Worker (Node-Tests gesten-logik.test.mjs mit den
// Punktfolgen in testfolgen/*.json). Spezifikation: entwurf/modul-gesten.md («Gestenwortschatz», «Technik», «Schnittstellen im Bau»).
//
// EINGABE je Bild (gesten.js, nach dem Spiegeln; dasselbe Format hat eine Aufnahme, siehe bildAusAufnahme):
//   { t (ms), haende: [{ punkte, welt, haendigkeit, score }], letzteEingabe (ms der letzten Maus- bzw. Tastaturbetätigung), ansicht3d, seitenverhaeltnis,
//     ansichtSeitenverhaeltnis }
//   punkte = 21 × [x, y, z] im Bild WIE IM SPIEGEL: x, y 0 … 1 (Anteil der Bildbreite bzw. -höhe, x nach rechts aus Sicht des Benutzers, y nach unten), z = relative
//            Tiefe von MediaPipe (Massstab wie x, kleiner = näher an der Kamera). welt = 21 × [x, y, z] in m um die Handmitte, x ebenfalls gespiegelt, y nach unten,
//            z von der Kamera weg. haendigkeit = Hand des Benutzers ('links' | 'rechts'). seitenverhaeltnis = Bildhöhe / Bildbreite der Kamera (640 × 480: 0.75).
// AUSGABE: { zustand: 'bereit' | 'hand' | 'aktiv', geste: null | 'drehen' | 'rollen' | 'zoomen' | 'verschieben', ereignisse: [{ art: 'schritt', geste: 'G2' | 'G3' | 'G6',
//   richtung }], navigation: null | { drehen: [dx, dy], rollen, verschieben: [dx, dy], zoom } (Zuwachs dieses Bilds, Einheiten wie navigieren der Ansichten),
//   punkte: Gestenhand 21 × [x, y] oder null, grund: kurzer Text, warum nichts ausgelöst hat (Testfeld, Protokoll) }
//
// Merkmale wie Hlks.Xr/Gesten/GestenMerkmale.cs und XrTabletLogik: Krümmung je Finger (Bogen − Sehne) / Handgrösse, Faustverhältnis (mittlerer Abstand der vier
// Fingerspitzen zum Handgelenk in Handgrössen; offen ≈ 1.9, Faust ≈ 1.0), Streckung je Finger (Pistole.cs), Normale der Handfläche. Handgrösse = Handgelenk bis
// Grundgelenk des Mittelfingers (Weltpunkte, m). Den MediaPipe-Fingern fehlt der Ansatz der Mittelhand: die Ketten beginnen am Handgelenk.
// Wege im Bild werden über die Handfläche in Meter umgerechnet (Länge der Handflächenstrecken in der Welt / im Bild, mit Tiefe z): ein Weg der Hand zählt gleich,
// ob sie 40 oder 70 cm vor der Kamera ist, und eine Faust, die gerade auf die Kamera zu geht, verschiebt sich dabei nicht.
//
// GESTEN (Gestenhand, Vorgabe links; die andere Hand zählt nie):
//   Faust (G4, G5, G7, fliessend): die Faust wird IM BILD geschlossen (die Hand war höchstens FAUST_OFFEN_VOR_MS vorher offen bzw. locker zu sehen – wer schon mit Tasse
//     oder Telefon in der Hand ins Bild kommt, navigiert nicht) und steht «faust_ruhe_ms» still; dann wirken drei Kanäle unabhängig:
//     bewegen (3D drehen, Plan/Schema verschieben) ab «totbereich» (Anteil der Bildbreite), rollen (nur 3D, Winkel der Knöchellinie Zeige- bis Kleinfinger) ab
//     «rollen_min_grad», zoomen (scheinbare Grösse der Handfläche, ln) ab «zoom_totbereich». Bis zur Schwelle bewegt sich nichts; danach linear, mit einem kleinen Spiel
//     (Spielfilter) gegen das Zittern der Erkennung, geglättet. Öffnet sich die Faust (Faustverhältnis steigt), endet alles sofort, ohne Nachlauf.
//   Wisch (G2 links/rechts, G3 hoch/runter, Schritt): wie Hlks.Xr/Bedienung/Anstoss.cs ein ANSTOSS mit offener oder lockerer Hand (Weg «anstoss_weg» in m innert 0.2 s,
//     mittleres Tempo «anstoss_tempo_ms» in m/s, quer höchstens 0.4 des Wegs, Handfläche dreht höchstens 25°, Form der Hand bleibt, die Hand stand vorher 0.1 s in der
//     Haltung). Für den Schreibtisch dazu: die Hand ist vorher SICHTBAR_VOR_MS im Bild (nicht eben hereingekommen), der Strich beginnt nicht am Bildrand, ein Strich nach
//     aussen (linke Hand nach links, rechte nach rechts) beginnt nicht mehr als «randwinkel_grad» neben der Bildmitte auf der Seite der Hand, die Handfläche liegt
//     höchstens «handflaeche_grad» neben der Ebene aus Wischrichtung und Blickachse der Kamera (eine flache Hand, die zur Seite greift, wischt nicht), und der Strich
//     ENDET im Bild innert «wisch_dauer_max_ms» (die Hand bremst; wer die Hand sinken lässt oder aus dem Bild nimmt, wischt nicht). Hat in den «sperre_ms» vor dem
//     Anlauf die Maus geklickt oder eine Taste gedrückt, zählt er nicht. Danach ruhen die Wische die halbe, die Gegenrichtung die ganze «wisch_sperre_ms».
//   Pistole (G6, Schritt): Zeigefinger gestreckt, die übrigen eingerollt, Daumen oben (wie Pistole.cs), ruhig «pistole_min_ms» bis «pistole_max_ms» gezeigt; das Ereignis
//     kommt beim Lösen. Längeres Zeigen löst nichts aus.
// Alle Zahlen sind Annahmen (Stellschrauben bzw. benannte Konstanten), keine Normwerte; Christian misst sie am Arbeitsplatz nach.

const GRAD = Math.PI / 180;

/** Indizes der 21 Punkte des MediaPipe Hand Landmarker. */
export const PUNKT = Object.freeze({
  HANDGELENK: 0, DAUMEN_CMC: 1, DAUMEN_MCP: 2, DAUMEN_IP: 3, DAUMEN_SPITZE: 4,
  ZEIGE_MCP: 5, ZEIGE_PIP: 6, ZEIGE_DIP: 7, ZEIGE_SPITZE: 8, MITTEL_MCP: 9, MITTEL_PIP: 10, MITTEL_DIP: 11, MITTEL_SPITZE: 12,
  RING_MCP: 13, RING_PIP: 14, RING_DIP: 15, RING_SPITZE: 16, KLEIN_MCP: 17, KLEIN_PIP: 18, KLEIN_DIP: 19, KLEIN_SPITZE: 20,
});
export const PUNKTE = 21;
/** Gelenkketten vom Ansatz bis zur Spitze (GestenMerkmale.Ketten; Finger ab dem Handgelenk, da MediaPipe den Ansatz der Mittelhand nicht kennt). */
const KETTEN = [[1, 2, 3, 4], [0, 5, 6, 7, 8], [0, 9, 10, 11, 12], [0, 13, 14, 15, 16], [0, 17, 18, 19, 20]];
/** Zeige- bis Kleinfinger: Grundgelenk, Mittelgelenk, Endgelenk, Spitze (Pistole.Streckung). */
const FINGER = [[5, 6, 7, 8], [9, 10, 11, 12], [13, 14, 15, 16], [17, 18, 19, 20]];
const SPITZEN = [8, 12, 16, 20];
const HANDFLAECHE = [0, 5, 9, 13, 17];
const HANDFLAECHE_STRECKEN = [[0, 5], [0, 9], [0, 13], [0, 17], [5, 17]];
/** Seite der Hand für den Daumen (Pistole.Seite): Mittelhand und Grundglied des Zeigefingers, Grund- und Mittelglied des Mittelfingers. */
const SEITE_DER_HAND = [[0, 5], [5, 6], [9, 10], [10, 11]];

// ---------------------------------------------------------------------------------------------------- Stellschrauben

const s = (vorgabe, min, max, schritt, einheit, gruppe, titel, erklaerung, herkunft) => Object.freeze({ vorgabe, min, max, schritt, einheit, gruppe, titel, erklaerung, herkunft });

/**
 * Stellschrauben der Gesten (Einstellungsdialog «Gesten», Benutzereinstellungen): je Schlüssel Vorgabe, Bereich (min … max, Schritt), Einheit, Gruppe, Titel,
 * Erklärung und Herkunft. Namen der Wisch-Regel wie in der Quest App (XrWischLogik.cs, Anstoss.cs: anstoss_weg, anstoss_tempo_ms, randwinkel_grad, handflaeche_grad).
 * Alle Werte sind Annahmen und werden am Arbeitsplatz gemessen.
 */
export const STELLSCHRAUBEN_VORGABE = Object.freeze({
  // ---- Wisch (G2, G3)
  anstoss_weg: s(0.045, 0.02, 0.2, 0.005, 'm', 'wisch', 'Wisch: Weg des Anstosses',
    'So weit (m) muss die Hand in Richtung des Wischs gehen, innert 0.2 s. Der Weg im Bild wird über die Grösse der Handfläche in Meter umgerechnet.',
    'Annahme, Wert der Quest App V1h (gesten.anstoss_weg_m 0.045 m, Brillentest 25.10.2026)'),
  anstoss_tempo_ms: s(0.35, 0.2, 3, 0.05, 'm/s', 'wisch', 'Wisch: Tempo des Anstosses',
    'Mittleres Tempo (m/s) über den Anstoss; ein gemächliches Ausholen oder Greifen bleibt darunter.',
    'Annahme, Wert der Quest App V1h (gesten.anstoss_tempo_ms 0.35 m/s, Brillentest 25.10.2026)'),
  randwinkel_grad: s(25, 5, 90, 1, '°', 'wisch', 'Wisch nach aussen: Bildrand',
    'Ein Strich nach aussen (linke Hand nach links, rechte nach rechts) zählt nicht, wenn er mehr als so viel (Grad) neben der Bildmitte auf der Seite der Hand beginnt (Zurücknehmen des Arms, Hand aus dem Bild). 90 = ohne.',
    'Annahme, Wert der Quest App V1h (gesten.randwinkel_grad 25°, Christian 25.10.2026); Winkel über «bildwinkel_grad» ins Bild umgerechnet'),
  handflaeche_grad: s(60, 10, 90, 1, '°', 'wisch', 'Wisch: Lage der Handfläche',
    'Die Handfläche (oder der Handrücken) liegt höchstens so weit (Grad) neben der Ebene aus Wischrichtung und Blickachse der Kamera: sie zeigt zur Kamera oder in die Wischrichtung. Eine flache Hand, die zur Seite greift, oder eine seitlich hängende Hand, die sinkt, wischt nicht. 90 = ohne.',
    'Annahme (Wert wie wegwischen.handflaeche_grad der Quest App V1h, 60°; Bezug für den Schreibtisch angepasst)'),
  offen_ab: s(1.4, 1.1, 1.9, 0.05, '', 'wisch', 'Wisch: lockere Hand offen ab',
    'Ab so viel gilt die Hand für den Wisch als offen bzw. locker (mittlerer Abstand der Fingerspitzen zum Handgelenk in Handgrössen: flach rund 1.9, halb gekrümmt rund 1.45, Faust rund 1.0). Faust, Pistolengeste und Pinch zählen nie.',
    'Annahme, Wert der Quest App V1f (gesten.offen_ab 1.40, Christian 24.10.2026: «wisch geste soll auch gehen wenn die finger nicht voll gestrekt sind»)'),
  wisch_dauer_max_ms: s(450, 200, 1500, 10, 'ms', 'wisch', 'Wisch: endet innert',
    'Der Strich muss so bald (ms nach dem Anlauf) im Bild enden – die Hand bremst. Längere Bewegungen (zur Tasse greifen, Hand heben oder sinken lassen) zählen nicht.',
    'Annahme Modul G (26.10.2026)'),
  wisch_sperre_ms: s(800, 200, 3000, 50, 'ms', 'wisch', 'Wisch: Sperre danach',
    'Nach einem Wisch zählt so lange (ms) kein Wisch in die Gegenrichtung (das Zurückholen der Hand), die halbe Zeit überhaupt keiner.',
    'Annahme, wie Wegwischen.SperreS bzw. TafelBlaettern.SperreS der Quest App (0.8 s)'),
  // ---- Faust (G4, G5, G7)
  faust_an: s(1.3, 1.0, 1.5, 0.05, '', 'faust', 'Faust: geschlossen unter',
    'Unter diesem Faustverhältnis gilt die Hand als Faust (gelöst erst 0.25 darüber). Der Zeigefinger darf dabei nicht gestreckt sein (sonst Pistole).',
    'Annahme, wie XrTabletLogik.FaustAn / FaustLos (1.3 / 1.55)'),
  faust_ruhe_ms: s(250, 0, 1000, 10, 'ms', 'faust', 'Faust: erst stillhalten',
    'So lange (ms) muss die geschlossene Faust ruhig stehen, bevor sie die Ansicht bewegt.',
    'Annahme Modul G (26.10.2026)'),
  totbereich: s(0.015, 0.002, 0.06, 0.001, 'Bildbreite', 'faust', 'Faust: Totbereich',
    'Unter diesem Weg (Anteil der Bildbreite) bewegt die Faust nichts, damit das Zittern der Erkennung nicht wackelt; danach linear und geglättet.',
    'Annahme der Spezifikation (1,5 % der Bildbreite, entwurf/modul-gesten.md)'),
  glaettung_ms: s(80, 0, 400, 10, 'ms', 'faust', 'Faust: Glättung',
    'Zeitkonstante (ms) der Glättung von Lage und Drehung der Faust; grösser = ruhiger, aber träger.',
    'Annahme Modul G (26.10.2026)'),
  drehen_grad_je_cm: s(6, 1, 30, 0.5, '°/cm', 'faust', 'Faust bewegen: Drehen (3D)',
    'So viel (Grad) dreht die 3D-Ansicht je Zentimeter Faustweg.',
    'Annahme Modul G (15 cm Weg = 90°)'),
  verschieben_je_cm: s(0.04, 0.005, 0.2, 0.005, 'Ansicht/cm', 'faust', 'Faust bewegen: Verschieben (Plan, Schema)',
    'So weit (Anteil der Ansichtsbreite) verschiebt sich Plan bzw. Schema je Zentimeter Faustweg.',
    'Annahme Modul G (25 cm Weg = eine Ansichtsbreite)'),
  rollen_min_grad: s(8, 2, 30, 1, '°', 'faust', 'Faust rollen: Mindestdrehung',
    'Erst ab so viel Drehung des Handgelenks (Grad) rollt die 3D-Ansicht; das trennt das Rollen vom Zittern.',
    'Annahme der Spezifikation (8°, entwurf/modul-gesten.md)'),
  rollen_faktor: s(1, 0.25, 4, 0.05, '×', 'faust', 'Faust rollen: Übersetzung',
    'So viel rollt die Ansicht je Grad Drehung des Handgelenks (1 = gleich viel).',
    'Annahme Modul G («die Faust hält das Modell»)'),
  zoom_totbereich: s(0.08, 0.02, 0.3, 0.01, 'ln', 'faust', 'Faust zur Kamera: Totbereich',
    'Erst wenn die Faust so viel grösser bzw. kleiner erscheint (natürlicher Logarithmus des Verhältnisses; 0.08 ≈ 8 %), zoomt die Ansicht.',
    'Annahme Modul G (26.10.2026)'),
  zoom_faktor: s(2.5, 0.5, 6, 0.1, 'Exponent', 'faust', 'Faust zur Kamera: Übersetzung',
    'Zoomfaktor = (scheinbare Grösse nachher / vorher) hoch diesen Wert; 2.5: Faust 10 % näher → 27 % näher.',
    'Annahme Modul G (26.10.2026)'),
  zoom_glaettung_ms: s(150, 0, 500, 10, 'ms', 'faust', 'Faust zur Kamera: Glättung',
    'Zeitkonstante (ms) der Glättung der scheinbaren Grösse der Faust.',
    'Annahme Modul G (26.10.2026)'),
  // ---- Pistole (G6)
  pistole_min_ms: s(200, 50, 1000, 10, 'ms', 'pistole', 'Pistole: mindestens',
    'So lange (ms) muss die Pistolengeste ruhig gezeigt sein; kürzer ist es ein Übergang zwischen zwei Haltungen.',
    'Annahme Modul G (26.10.2026)'),
  pistole_max_ms: s(1500, 300, 5000, 50, 'ms', 'pistole', 'Pistole: höchstens',
    'Wer länger zeigt, zeigt auf etwas: dann löst die Geste nichts aus. Das Ereignis kommt beim Lösen der Geste.',
    'Annahme Modul G (26.10.2026)'),
  // ---- Allgemein
  sperre_ms: s(300, 0, 2000, 10, 'ms', 'allgemein', 'Sperre neben Maus und Tastatur',
    'Hat die Maus geklickt oder wurde eine Taste gedrückt, zählen Wisch und Pistolengeste so lange (ms) nicht. Die Faust läuft bewusst parallel zur Maus.',
    'Spezifikation (300 ms, entwurf/modul-gesten.md)'),
  bildwinkel_grad: s(60, 40, 120, 1, '°', 'allgemein', 'Bildwinkel der Kamera',
    'Waagrechter Bildwinkel der Kamera (Grad); rechnet «randwinkel_grad» in eine Lage im Bild um.',
    'Annahme (übliche eingebaute Kamera eines Notebooks); am Arbeitsplatz zu prüfen'),
});

/** Werte der Stellschrauben: Vorgaben, überschrieben mit `teil` (nur bekannte Schlüssel, endliche Zahlen, auf den Bereich begrenzt). */
export function stellschraubenWerte(teil = {}) {
  const w = {};
  for (const [k, d] of Object.entries(STELLSCHRAUBEN_VORGABE)) {
    const v = Number(teil?.[k]);
    w[k] = teil && k in teil && teil[k] !== null && teil[k] !== '' && Number.isFinite(v) ? Math.min(d.max, Math.max(d.min, v)) : d.vorgabe;
  }
  return w;
}

// ---------------------------------------------------------------------------------------------------- feste Werte (Annahmen)

/** Anstoss innert so viel Zeit (Anstoss.ZeitS 0.2 s). */
export const ANSTOSS_ZEIT_MS = 200;
/** Spiel beim Anlauf (Anstoss.SpielM, m). */
export const ANSTOSS_SPIEL_M = 0.003;
/** Die Haltung steht so lange vor dem Anlauf (Anstoss HaltungVorS 0.1 s). */
export const HALTUNG_VOR_MS = 100;
/** Quer zum Weg höchstens so viel des Wegs (Anstoss QuerAnteil). */
export const QUER_ANTEIL = 0.4;
/** Die Handfläche dreht im Anstoss höchstens so viel (Anstoss DrehMaxGrad). */
export const DREH_MAX_GRAD = 25;
/** So viel darf sich die Form der Hand (Faustverhältnis) im Anstoss ändern (Anstoss FormSpiel 0.15; Annahme Kamera 0.2 wegen der Bewegungsunschärfe). */
export const FORM_SPIEL = 0.2;
/** Lockere Hand: die vier Finger liegen in der Streckung höchstens so weit auseinander (Anstoss SpreizungMax). */
export const SPREIZUNG_MAX = 0.25;
/** Ab diesem Faustverhältnis ist die Hand offen, ohne Prüfung der Spreizung (XrTabletLogik.OffenAb). */
export const OFFEN_GANZ = 1.65;
/** Faust gelöst erst so viel über «faust_an» (XrTabletLogik FaustLos − FaustAn). */
export const FAUST_LOS_PLUS = 0.25;
/** Pistole: Zeigefinger gestreckt ab, übrige eingerollt bis (Pistole.StreckAnteil, RollAnteil). */
export const STRECK_ANTEIL = 0.85, ROLL_ANTEIL = 0.7;
/** Pinch: Daumen- an Zeigefingerspitze näher als so viel (m; pinch.schwelle_an_m) – keine lockere Hand. */
export const PINCH_M = 0.025;
/** Pistole: Daumenspitze mindestens so weit von der Zeigefingerspitze (Pistole.PinchFreiM) und von der Seite der Hand (pistole.abzug_aus_m) – «Daumen oben». */
export const PINCH_FREI_M = 0.04, DAUMEN_OBEN_M = 0.04;
/** Pistole ruhig: Zeigefinger dreht höchstens so viel (Pistole.ZielRuheGrad), die Hand wandert höchstens so weit (Anteil der Bildbreite). */
export const PISTOLE_RUHE_GRAD = 20, PISTOLE_RUHE_WEG = 0.03;
/** Pistole gelöst, wenn sie so viele Bilder in Folge nicht mehr zu sehen ist (Entprellen). */
export const PISTOLE_ENTPRELLEN = 2;
/** Die Faust zählt nur, wenn die Hand höchstens so lange vorher offen bzw. locker im Bild war (im Bild geschlossen). */
export const FAUST_OFFEN_VOR_MS = 1500;
/** Steigt das Faustverhältnis so weit über das kleinste dieser Faust, öffnet sie sich: keine Bewegung mehr. */
export const OEFFNEN_SPIEL = 0.12;
/** Nach dem Lösen einer Faust zählt so lange kein Wisch und keine Pistole (die Hand öffnet und sinkt). */
export const FAUST_NACH_MS = 400;
/** Die Hand ist vor dem Anlauf eines Wischs mindestens so lange ohne Unterbruch im Bild. */
export const SICHTBAR_VOR_MS = 300;
/** Anlauf aus der Ruhe: in den HALTUNG_VOR_MS vor dem Anlauf ging die Hand höchstens so weit (m) in Wischrichtung (Annahme Modul G). */
export const RUHE_VOR_M = 0.01;
/** Lücken der Erkennung bis zu so viel zählen als «ohne Unterbruch». */
export const LUECKE_MS = 150;
/** Wisch beendet: Tempo in Wischrichtung unter so viel des höchsten Tempos bzw. unter STOPP_TEMPO_MS (m/s). */
export const STOPP_ANTEIL = 0.35, STOPP_TEMPO_MS = 0.1;
/** Abstand der Handflächenmitte zum Bildrand (Anteil) und Anteil der Punkte im Bild, unter denen die Hand «am Rand» ist. */
export const RAND_ANTEIL = 0.03, IM_BILD_ANTEIL = 0.9;
/** Spiel nach dem Überschreiten der Schwelle (Anteil der Schwelle): bewegen, rollen, zoomen. */
export const SPIEL_BEWEGEN = 1 / 3, SPIEL_ROLLEN = 0.25, SPIEL_ZOOM = 0.25;
/** Handgrösse, wenn die Weltpunkte unplausibel sind (GestenEinstellungen.HandGroesseVorgabeM, ≈ 0.09 m). */
export const HANDGROESSE_VORGABE_M = 0.09;
/** Bildhöhe / Bildbreite der Kamera (640 × 480). */
export const SEITENVERHAELTNIS_VORGABE = 0.75;
/** Vertrag mit MediaPipe: die Händigkeit gilt für ein gespiegeltes Bild (Selfie). gesten.js gibt das Kamerabild ungespiegelt in die Erkennung, darum wird sie getauscht
 *  (Annahme nach der Dokumentation von MediaPipe Hands; an der echten Kamera zu bestätigen – Testfeld «Hand: links»). */
export const MEDIAPIPE_ERWARTET_SPIEGELBILD = true;

// ---------------------------------------------------------------------------------------------------- Vektoren

const zahl = (x) => typeof x === 'number' && Number.isFinite(x);
const minus = (a, b) => [a[0] - b[0], a[1] - b[1], (a[2] ?? 0) - (b[2] ?? 0)];
const skalar = (a, b) => a[0] * b[0] + a[1] * b[1] + (a[2] ?? 0) * (b[2] ?? 0);
const laenge = (a) => Math.hypot(a[0], a[1], a[2] ?? 0);
const abstand = (a, b) => laenge(minus(a, b));
const kreuz = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const einheit = (a) => { const l = laenge(a); return l > 1e-12 ? [a[0] / l, a[1] / l, (a[2] ?? 0) / l] : [0, 0, 0]; };
const mittel = (z) => z.reduce((a, b) => a + b, 0) / z.length;

/** Kleinster Winkelunterschied b − a im Bereich (−π, π]. */
export function winkelDiff(a, b) {
  let d = (b - a) % (2 * Math.PI);
  if (d > Math.PI) d -= 2 * Math.PI;
  if (d <= -Math.PI) d += 2 * Math.PI;
  return d;
}

/** Abstand eines Punkts zur Strecke a–b (Pistole.AbstandZurStrecke). */
export function abstandZurStrecke(p, a, b) {
  const ab = minus(b, a);
  const l2 = skalar(ab, ab);
  const t = l2 > 1e-12 ? Math.min(1, Math.max(0, skalar(minus(p, a), ab) / l2)) : 0;
  return abstand(p, [a[0] + ab[0] * t, a[1] + ab[1] * t, a[2] + ab[2] * t]);
}

/** Glättungsanteil eines Bilds nach dt (ms) bei der Zeitkonstante tau (ms); tau 0 = ungeglättet. */
export const glaettAnteil = (dt, tau) => (tau > 0 ? 1 - Math.exp(-Math.max(0, dt) / tau) : 1);

// ---------------------------------------------------------------------------------------------------- Merkmale

const punkteGueltig = (p) => Array.isArray(p) && p.length === PUNKTE && p.every((q) => Array.isArray(q) && q.length >= 2 && zahl(q[0]) && zahl(q[1]) && (q[2] === undefined || zahl(q[2])));

/**
 * Merkmale einer Hand (siehe Dateikopf); null, wenn Punkte fehlen. sv = Bildhöhe / Bildbreite. Bildlagen in «Bildbreiten» (x, y · sv, z), damit Abstände in beiden
 * Richtungen gleich zählen.
 */
export function merkmale(hand, sv = SEITENVERHAELTNIS_VORGABE) {
  const pk = hand?.punkte, wk = hand?.welt;
  if (!punkteGueltig(pk) || !punkteGueltig(wk)) return null;
  const bild = pk.map((q) => [q[0], q[1] * sv, q[2] ?? 0]);
  const welt = wk.map((q) => [q[0], q[1], q[2] ?? 0]);
  const gw = abstand(welt[0], welt[9]);
  const groesse = gw > 0.02 && gw < 0.3 ? gw : HANDGROESSE_VORGABE_M;
  let sb = 0, sw = 0;
  for (const [a, b] of HANDFLAECHE_STRECKEN) { sb += abstand(bild[a], bild[b]); sw += abstand(welt[a], welt[b]); }
  if (!(sb > 1e-6) || !(sw > 1e-6)) return null;
  const kruemmung = KETTEN.map((k) => {
    let bogen = 0;
    for (let j = 0; j + 1 < k.length; j++) bogen += abstand(welt[k[j]], welt[k[j + 1]]);
    return Math.max(0, bogen - abstand(welt[k[0]], welt[k.at(-1)])) / groesse;
  });
  const streckung = FINGER.map(([a, b, c, d]) => {
    const l = abstand(welt[a], welt[b]) + abstand(welt[b], welt[c]) + abstand(welt[c], welt[d]);
    return l > 1e-6 ? abstand(welt[a], welt[d]) / l : 1;
  });
  const cx = mittel(HANDFLAECHE.map((i) => pk[i][0])), cy = mittel(HANDFLAECHE.map((i) => pk[i][1]));
  const knoechel = minus(bild[5], bild[17]);
  return {
    bild, welt, groesse, sv,
    meterJeBild: sw / sb,                     // m je Bildbreite in der Tiefe der Hand
    groesseLn: Math.log(sb / sw),             // scheinbare Grösse (ln Bildbreiten je m): grösser = näher
    kruemmung,
    faust: mittel(SPITZEN.map((i) => abstand(welt[i], welt[0]))) / groesse,
    streckung,
    spreizung: Math.max(...streckung) - Math.min(...streckung),
    pinch: abstand(welt[4], welt[8]),
    daumenSeite: Math.min(...SEITE_DER_HAND.map(([a, b]) => abstandZurStrecke(welt[4], welt[a], welt[b]))),
    normale: einheit(kreuz(minus(welt[5], welt[0]), minus(welt[17], welt[0]))),
    zeige: einheit(minus(welt[8], welt[5])),
    mitte: [cx, cy * sv],                     // Handflächenmitte im Bild (Bildbreiten)
    rand: Math.min(cx, 1 - cx, cy, 1 - cy),   // Abstand der Handflächenmitte zum Bildrand (Anteil)
    imBild: pk.filter((q) => q[0] >= 0 && q[0] <= 1 && q[1] >= 0 && q[1] <= 1).length / PUNKTE,
    roll: Math.atan2(-knoechel[1], knoechel[0]),   // Winkel der Knöchellinie (Kleinfinger → Zeigefinger), gegen den Uhrzeiger positiv
  };
}

/** Lage der Handflächenmitte in m (seitlich zur Blickachse der Kamera, x rechts, y unten) bei der scheinbaren Grösse ln. */
export function lageMeter(mitte, groesseLn, sv) {
  const k = Math.exp(-groesseLn);
  return [(mitte[0] - 0.5) * k, (mitte[1] - 0.5 * sv) * k];
}

/** Faust (mit Hysterese: vorher = war schon Faust): Faustverhältnis unter «faust_an» (bzw. + FAUST_LOS_PLUS) und Zeigefinger nicht gestreckt. */
export function istFaust(m, vorher, w) {
  if (!m) return false;
  return m.faust < w.faust_an + (vorher ? FAUST_LOS_PLUS : 0) && m.streckung[0] < STRECK_ANTEIL;
}

/** Haltung der Pistole (Pistole.Haltung): Zeigefinger gestreckt, übrige eingerollt, Daumenspitze nicht an der Zeigefingerspitze – gleich, wo der Daumen ist. */
export function pistolenHaltung(m) {
  return !!m && m.streckung[0] >= STRECK_ANTEIL && m.streckung[1] <= ROLL_ANTEIL && m.streckung[2] <= ROLL_ANTEIL && m.streckung[3] <= ROLL_ANTEIL && m.pinch >= PINCH_FREI_M;
}

/** Pistolengeste G6: Haltung der Pistole mit dem Daumen oben (weg von der Seite der Hand). */
export const istPistole = (m) => pistolenHaltung(m) && m.daumenSeite >= DAUMEN_OBEN_M;

/** Lockere Hand für den Wisch (Anstoss.cs LockereHand): null, wenn sie passt, sonst der Grund. */
export function lockereHand(m, w) {
  if (!m) return 'keine Hand';
  if (pistolenHaltung(m)) return 'Pistolengeste';
  if (istFaust(m, false, w)) return 'Faust';
  if (m.pinch < PINCH_M) return 'Pinch';
  if (m.faust < w.offen_ab) return `zu geschlossen (${m.faust.toFixed(2)}, ab ${w.offen_ab.toFixed(2)})`;
  if (m.faust < OFFEN_GANZ && m.spreizung > SPREIZUNG_MAX) return `Finger ungleich gekrümmt (${m.spreizung.toFixed(2)})`;
  return null;
}

/** Winkel (Grad) der Handflächennormale zur Ebene aus Wischrichtung r (im Bild) und Blickachse der Kamera. */
export function handflaecheWinkel(normale, r) {
  const q = [r[1], -r[0], 0];   // r × Blickachse (0, 0, 1)
  return Math.asin(Math.min(1, Math.abs(skalar(normale, q)))) / GRAD;
}

/** Winkel (Grad) eines Punkts im Bild (x Anteil der Breite) neben der Bildmitte, waagrecht; negativ links. */
export const seitenWinkel = (x, bildwinkelGrad) => Math.atan((x - 0.5) * 2 * Math.tan(bildwinkelGrad * GRAD / 2)) / GRAD;

// ---------------------------------------------------------------------------------------------------- Hände: MediaPipe → Logik

/**
 * Ergebnis des HandLandmarker → nur Zahlen (Worker → Hauptfaden): je Hand Bildpunkte und Weltpunkte als [x, y, z], Händigkeit von MediaPipe ('Left' | 'Right') und
 * Sicherheit. Alles andere (Bild, Masken, Objekte) fällt weg; unvollständige Hände auch.
 */
export function ergebnisAusMediapipe(r) {
  const haende = [];
  const n = Array.isArray(r?.landmarks) ? r.landmarks.length : 0;
  for (let i = 0; i < n && i < 4; i++) {
    const p = r.landmarks[i], w = r.worldLandmarks?.[i], h = r.handedness?.[i]?.[0] ?? r.handednesses?.[i]?.[0];
    if (!Array.isArray(p) || !Array.isArray(w) || p.length !== PUNKTE || w.length !== PUNKTE) continue;
    const a = (q) => [Number(q?.x), Number(q?.y), Number(q?.z ?? 0)];
    const punkte = p.map(a), welt = w.map(a);
    if (!punkte.every((q) => q.every(zahl)) || !welt.every((q) => q.every(zahl))) continue;
    const name = h?.categoryName === 'Left' || h?.categoryName === 'Right' ? h.categoryName : null;
    haende.push({ punkte, welt, haendigkeitRoh: name, score: zahl(Number(h?.score)) ? Number(h.score) : 0 });
  }
  return { haende };
}

/**
 * Hände aus der Erkennung (ungespiegeltes Kamerabild) → Spiegelbild für Logik, Handskelett und Aufnahme: x → 1 − x (Bild) bzw. −x (Welt); die Händigkeit von MediaPipe
 * (für ein Selfie-Bild) → Hand des Benutzers. bildGespiegelt = das Bild für die Erkennung war schon gespiegelt.
 */
export function haendeImSpiegel(haende, { bildGespiegelt = false } = {}) {
  return (haende ?? []).map((h) => {
    const tauschen = MEDIAPIPE_ERWARTET_SPIEGELBILD && !bildGespiegelt;
    const benutzer = h.haendigkeitRoh === 'Left' ? (tauschen ? 'rechts' : 'links') : h.haendigkeitRoh === 'Right' ? (tauschen ? 'links' : 'rechts') : null;
    const spiegeln = !bildGespiegelt;
    return {
      punkte: h.punkte.map(([x, y, z]) => [spiegeln ? 1 - x : x, y, z]),
      welt: h.welt.map(([x, y, z]) => [spiegeln ? -x : x, y, z]),
      haendigkeit: benutzer,
      score: h.score ?? 0,
    };
  });
}

/** Die Gestenhand eines Bilds: Hand der Händigkeit `hand`; bei zweien die sicherere, bei Gleichstand die auf ihrer Seite des Bilds. */
export function gestenhand(haende, hand) {
  const k = (haende ?? []).filter((h) => h && h.haendigkeit === hand && punkteGueltig(h.punkte));
  if (k.length <= 1) return k[0] ?? null;
  const x = (h) => mittel(HANDFLAECHE.map((i) => h.punkte[i][0]));
  return k.sort((a, b) => (b.score ?? 0) - (a.score ?? 0) || (hand === 'links' ? x(a) - x(b) : x(b) - x(a)))[0];
}

// ---------------------------------------------------------------------------------------------------- Zustandsautomat

/** Neuer Zustand der Gestenerkennung: Gestenhand ('links' | 'rechts') und Stellschrauben (Teil; der Rest Vorgaben). */
export function neuerZustand({ hand = 'links', stellschrauben = {} } = {}) {
  return {
    hand: hand === 'rechts' ? 'rechts' : 'links',
    werte: stellschraubenWerte(stellschrauben),
    sichtbarSeit: null, zuletztGesehen: -Infinity, offenZuletzt: -Infinity, letzteT: null,
    spur: [], kandidat: null,
    sperre: { gleichBis: -Infinity, gegenBis: -Infinity, richtung: null },
    faust: null, faustEnde: -Infinity,
    pistole: null,
  };
}

/** Stellschrauben bzw. Gestenhand ändern; die laufenden Gesten enden (kein Sprung). */
export function einstellen(z, { hand, stellschrauben } = {}) {
  if (stellschrauben) z.werte = stellschraubenWerte({ ...z.werte, ...stellschrauben });
  if (hand && hand !== z.hand) {
    const neu = neuerZustand({ hand, stellschrauben: z.werte });
    Object.assign(z, neu);
  }
  return z;
}

const RICHTUNGEN = Object.freeze([
  { name: 'links', r: [-1, 0], geste: 'G2' }, { name: 'rechts', r: [1, 0], geste: 'G2' },
  { name: 'hoch', r: [0, -1], geste: 'G3' }, { name: 'runter', r: [0, 1], geste: 'G3' },
]);
const GEGEN = { links: 'rechts', rechts: 'links', hoch: 'runter', runter: 'hoch' };

/**
 * Ein Bild (siehe Dateikopf): führt Faust, Wisch und Pistole weiter und gibt Zustand, Ereignisse und den Zuwachs der Navigation zurück.
 */
export function schritt(z, bild) {
  const t = Number(bild?.t);
  const aus = { zustand: 'bereit', geste: null, ereignisse: [], navigation: null, punkte: null, grund: null };
  if (!zahl(t)) return aus;
  const sv = zahl(bild.seitenverhaeltnis) && bild.seitenverhaeltnis > 0 ? bild.seitenverhaeltnis : SEITENVERHAELTNIS_VORGABE;
  const eingabe = zahl(bild.letzteEingabe) ? bild.letzteEingabe : -Infinity;
  const dt = z.letzteT === null || t < z.letzteT ? 0 : t - z.letzteT;
  z.letzteT = t;
  const hand = gestenhand(bild.haende, z.hand);
  const m = hand ? merkmale(hand, sv) : null;
  if (!m) {
    verloren(z, t, eingabe, aus);
    if (z.sichtbarSeit !== null) aus.zustand = 'hand';   // kurze Lücke der Erkennung: die Anzeige bleibt ruhig
    return aus;
  }
  if (z.sichtbarSeit === null || t - z.zuletztGesehen > LUECKE_MS) {
    // neu im Bild: alles, was an der alten Hand hing, beginnt neu
    if (z.sichtbarSeit !== null) abbrechen(z, t);
    z.sichtbarSeit = t;
  }
  z.zuletztGesehen = t;
  aus.zustand = 'hand';
  aus.punkte = hand.punkte.map((q) => [q[0], q[1]]);
  const w = z.werte;
  const faust = istFaust(m, !!z.faust, w);
  faustSchritt(z, m, t, dt, faust, bild, aus);
  if (!faust && m.faust >= w.offen_ab && !pistolenHaltung(m)) z.offenZuletzt = t;   // offen bzw. locker zu sehen
  pistoleSchritt(z, m, t, faust, eingabe, aus);
  wischSchritt(z, m, t, faust, eingabe, aus);
  return aus;
}

/** Laufendes abbrechen (Hand verloren, andere Hand): Faust endet sofort, Kandidaten verfallen. */
function abbrechen(z, t) {
  if (z.faust) z.faustEnde = t;
  z.faust = null;
  z.kandidat = null;
  z.spur = [];
  z.pistole = null;
}

/** Bild ohne Gestenhand: kurze Lücken überbrücken; danach endet alles (eine gezeigte Pistole gilt beim Verschwinden als gelöst). */
function verloren(z, t, eingabe, aus) {
  if (z.sichtbarSeit === null) return;
  if (t - z.zuletztGesehen <= LUECKE_MS) return;
  if (z.pistole) pistoleEnde(z, z.pistole.zuletzt, eingabe, aus);
  abbrechen(z, t);
  z.sichtbarSeit = null;
}

// ---------------------------------------------------------------------------------------------------- Faust (G4, G5, G7)

/** Spielfilter einer Grösse (Skalar): bis zur Schwelle ab dem Anker nichts, dann folgt die Referenz mit dem Spiel; Rückgabe der Zuwachs. */
export function spielSkalar(k, wert, schwelle, spiel) {
  if (!k.aktiv) {
    const d = wert - k.anker;
    if (Math.abs(d) <= schwelle) return 0;
    k.aktiv = true;
    k.ref = wert - Math.sign(d) * spiel;
    return 0;
  }
  const e = wert - k.ref;
  if (Math.abs(e) <= spiel) return 0;
  const neu = wert - Math.sign(e) * spiel;
  const d = neu - k.ref;
  k.ref = neu;
  return d;
}

/** Spielfilter einer Lage (2D), wie spielSkalar mit Kreisen. */
export function spielLage(k, wert, schwelle, spiel) {
  if (!k.aktiv) {
    const d = [wert[0] - k.anker[0], wert[1] - k.anker[1]];
    const l = Math.hypot(d[0], d[1]);
    if (l <= schwelle) return [0, 0];
    k.aktiv = true;
    k.ref = [wert[0] - (d[0] / l) * spiel, wert[1] - (d[1] / l) * spiel];
    return [0, 0];
  }
  const e = [wert[0] - k.ref[0], wert[1] - k.ref[1]];
  const l = Math.hypot(e[0], e[1]);
  if (l <= spiel) return [0, 0];
  const neu = [wert[0] - (e[0] / l) * spiel, wert[1] - (e[1] / l) * spiel];
  const d = [neu[0] - k.ref[0], neu[1] - k.ref[1]];
  k.ref = neu;
  return d;
}

function faustSchritt(z, m, t, dt, faust, bild, aus) {
  const w = z.werte;
  if (!faust) {
    if (z.faust) { z.faust = null; z.faustEnde = t; }   // Faust gelöst: sofort aus, ohne Nachlauf
    return;
  }
  let f = z.faust;
  if (!f) {
    // im Bild geschlossen: die Hand war kurz vorher offen bzw. locker zu sehen (seit sie im Bild ist)
    const imBild = z.offenZuletzt >= (z.sichtbarSeit ?? Infinity) && t - z.offenZuletzt <= FAUST_OFFEN_VOR_MS;
    f = z.faust = { seit: t, gueltig: imBild, scharf: false, oeffnet: false, minFaust: m.faust, glatt: null, ruhe: [], rollRoh: m.roll, rollSumme: 0, kanaele: null, geste: null };
    if (!imBild) aus.grund = 'Faust nicht im Bild geschlossen';
  }
  if (!f.gueltig) return;
  f.minFaust = Math.min(f.minFaust, m.faust);
  if (m.faust > f.minFaust + OEFFNEN_SPIEL) f.oeffnet = true;   // die Hand öffnet sich: keine Bewegung mehr
  if (f.oeffnet) return;
  // Rollwinkel ohne Sprung bei ±180°
  f.rollSumme += winkelDiff(f.rollRoh, m.roll);
  f.rollRoh = m.roll;
  // Glätten: die Lage in m (seitlich zur Blickachse; eine Faust, die gerade auf die Kamera zu geht, bleibt so an ihrem Ort), der Rollwinkel und die scheinbare Grösse
  const roh = { lage: lageMeter(m.mitte, m.groesseLn, m.sv), roll: f.rollSumme, ln: m.groesseLn };
  if (!f.glatt) f.glatt = { lage: [...roh.lage], roll: roh.roll, ln: roh.ln };
  else {
    const a = glaettAnteil(dt, w.glaettung_ms), b = glaettAnteil(dt, w.zoom_glaettung_ms);
    f.glatt.lage[0] += (roh.lage[0] - f.glatt.lage[0]) * a;
    f.glatt.lage[1] += (roh.lage[1] - f.glatt.lage[1]) * a;
    f.glatt.roll += (roh.roll - f.glatt.roll) * a;
    f.glatt.ln += (roh.ln - f.glatt.ln) * b;
  }
  const g = f.glatt;
  const lage = [g.lage[0], g.lage[1]];
  const mjb = Math.exp(-g.ln);   // m je Bildbreite in der Tiefe der Faust
  if (!f.scharf) {
    // erst stillhalten: im Fenster «faust_ruhe_ms» höchstens der Totbereich bzw. die halbe Mindestdrehung
    f.ruhe.push({ t, x: lage[0], y: lage[1], roll: g.roll });
    while (f.ruhe.length > 1 && t - f.ruhe[0].t > w.faust_ruhe_ms) f.ruhe.shift();
    const lang = t - f.seit >= w.faust_ruhe_ms;
    const xs = f.ruhe.map((p) => p.x), ys = f.ruhe.map((p) => p.y), rs = f.ruhe.map((p) => p.roll);
    const ruhig = Math.hypot(Math.max(...xs) - Math.min(...xs), Math.max(...ys) - Math.min(...ys)) <= w.totbereich * mjb && (Math.max(...rs) - Math.min(...rs)) <= w.rollen_min_grad * GRAD / 2;
    if (!(lang && ruhig)) { aus.grund = 'Faust: stillhalten'; return; }
    f.scharf = true;
    f.kanaele = { bewegen: { aktiv: false, anker: lage, ref: null }, rollen: { aktiv: false, anker: g.roll, ref: null }, zoom: { aktiv: false, anker: g.ln, ref: null } };
  }
  const k = f.kanaele;
  const schwelle = w.totbereich * mjb;
  const dLage = spielLage(k.bewegen, lage, schwelle, schwelle * SPIEL_BEWEGEN);
  const ansicht3d = bild.ansicht3d !== false;
  const dRoll = ansicht3d ? spielSkalar(k.rollen, g.roll, w.rollen_min_grad * GRAD, w.rollen_min_grad * GRAD * SPIEL_ROLLEN) : 0;
  const dLn = spielSkalar(k.zoom, g.ln, w.zoom_totbereich, w.zoom_totbereich * SPIEL_ZOOM);
  const nav = {};
  const cm = [dLage[0] * 100, dLage[1] * 100];
  if (cm[0] !== 0 || cm[1] !== 0) {
    if (ansicht3d) nav.drehen = [cm[0] * w.drehen_grad_je_cm * GRAD, cm[1] * w.drehen_grad_je_cm * GRAD];
    else {
      const asv = zahl(bild.ansichtSeitenverhaeltnis) && bild.ansichtSeitenverhaeltnis > 0 ? bild.ansichtSeitenverhaeltnis : 1;
      nav.verschieben = [cm[0] * w.verschieben_je_cm, cm[1] * w.verschieben_je_cm * asv];
    }
  }
  if (dRoll !== 0) nav.rollen = dRoll * w.rollen_faktor;
  if (dLn !== 0) nav.zoom = Math.exp(dLn * w.zoom_faktor);
  // welche Geste gerade wirkt (für die Statusleiste): die stärkste dieses Bilds, sonst die letzte
  const staerke = [
    [ansicht3d ? 'drehen' : 'verschieben', Math.hypot(cm[0], cm[1]) / 0.5],
    ['rollen', Math.abs(dRoll) / (3 * GRAD)],
    ['zoomen', Math.abs(dLn) / 0.03],
  ].sort((a, b) => b[1] - a[1]);
  if (staerke[0][1] > 0) f.geste = staerke[0][0];
  if (k.bewegen.aktiv || k.rollen.aktiv || k.zoom.aktiv) {
    aus.zustand = 'aktiv';
    aus.geste = f.geste ?? (k.bewegen.aktiv ? (ansicht3d ? 'drehen' : 'verschieben') : k.rollen.aktiv ? 'rollen' : 'zoomen');
  }
  if (Object.keys(nav).length) aus.navigation = nav;
}

// ---------------------------------------------------------------------------------------------------- Pistole (G6)

function pistoleSchritt(z, m, t, faust, eingabe, aus) {
  const p = !faust && !z.faust && istPistole(m);
  const st = z.pistole;
  if (p) {
    if (!st) { z.pistole = { seit: t, zuletzt: t, mitte: m.mitte, zeige: m.zeige, ruhig: true, fehlt: 0 }; return; }
    st.zuletzt = t;
    st.fehlt = 0;
    const weg = Math.hypot(m.mitte[0] - st.mitte[0], m.mitte[1] - st.mitte[1]);
    const dreh = Math.acos(Math.min(1, Math.max(-1, skalar(m.zeige, st.zeige)))) / GRAD;
    if (weg > PISTOLE_RUHE_WEG || dreh > PISTOLE_RUHE_GRAD) st.ruhig = false;
    return;
  }
  if (!st) return;
  if (++st.fehlt < PISTOLE_ENTPRELLEN) return;
  pistoleEnde(z, st.zuletzt, eingabe, aus);
}

function pistoleEnde(z, ende, eingabe, aus) {
  const st = z.pistole, w = z.werte;
  z.pistole = null;
  if (!st) return;
  const dauer = ende - st.seit;
  const grund = !st.ruhig ? 'Pistole: nicht ruhig'
    : dauer < w.pistole_min_ms ? 'Pistole: zu kurz'
    : dauer > w.pistole_max_ms ? 'Pistole: zu lange gezeigt'
    : eingabe >= st.seit - w.sperre_ms ? 'Sperre: Maus oder Taste'
    : st.seit - z.faustEnde < FAUST_NACH_MS ? 'Pistole: gleich nach der Faust'
    : null;
  if (grund) { aus.grund = grund; return; }
  aus.ereignisse.push({ art: 'schritt', geste: 'G6', richtung: null });
}

// ---------------------------------------------------------------------------------------------------- Wisch (G2, G3)

/**
 * Anstoss auf der Spur (Anstoss.Pruefen): Spur = [{ t (ms), p: [x, y] (m), n (Normale), o (Faustverhältnis), locker }], ältester zuerst; r = Richtung im Bild
 * (Einheitsvektor, x rechts, y unten). Rückgabe { aus, weg (m), tempo (m/s), anlauf (Index), quer (m), grund }.
 */
export function anstossPruefen(spur, t, r, w) {
  const nichts = (grund, extra = {}) => ({ aus: false, weg: 0, tempo: 0, anlauf: -1, quer: 0, grund, ...extra });
  if (spur.length < 2) return nichts('erst in der Haltung');
  let ab = spur.length - 1;
  while (ab > 0 && spur[ab - 1].t >= t - ANSTOSS_ZEIT_MS - 1e-9) ab--;
  if (ab >= spur.length - 1) return nichts('erst in der Haltung');
  const dot = (p) => p[0] * r[0] + p[1] * r[1];
  const jetzt = spur.at(-1).p;
  let kleinst = Infinity;
  for (let i = ab; i < spur.length; i++) kleinst = Math.min(kleinst, dot(spur[i].p));
  let anlauf = spur.length - 1;
  for (let i = spur.length - 1; i >= ab; i--) if (dot(spur[i].p) <= kleinst + ANSTOSS_SPIEL_M) { anlauf = i; break; }
  const weg = dot(jetzt) - kleinst;
  const dauer = (t - spur[anlauf].t) / 1000;
  if (weg <= 0 || dauer <= 1e-6) return nichts('kein Weg in Richtung der Geste');
  const d = [jetzt[0] - spur[anlauf].p[0], jetzt[1] - spur[anlauf].p[1]];
  const quer = Math.abs(d[0] * -r[1] + d[1] * r[0]);
  const tempo = weg / dauer;
  const werte = { weg, tempo, anlauf, quer };
  if (weg < w.anstoss_weg) return nichts(`Weg ${(weg * 100).toFixed(1)} cm (mind. ${(w.anstoss_weg * 100).toFixed(1)})`, werte);
  if (tempo < w.anstoss_tempo_ms) return nichts(`Tempo ${tempo.toFixed(2)} m/s (mind. ${w.anstoss_tempo_ms.toFixed(2)})`, werte);
  if (quer > QUER_ANTEIL * weg) return nichts(`quer ${(quer * 100).toFixed(1)} cm`, werte);
  // erst die Hand stellen, dann der Schub: HALTUNG_VOR_MS vor dem Anlauf locker in der Spur
  const ta = spur[anlauf].t;
  if (ta - spur[0].t < HALTUNG_VOR_MS - 1e-9) return nichts('Haltung erst kurz vor dem Schub', werte);
  for (const q of spur) if (q.t >= ta - HALTUNG_VOR_MS - 1e-9 && q.t <= ta + 1e-9 && !q.locker) return nichts('Hand vor dem Schub nicht locker', werte);
  // Anlauf aus der Ruhe (Schreibtisch): vor dem Anlauf ging die Hand nicht schon in Wischrichtung – sonst ist es eine durchgehende Bewegung (Greifen, Heben, Sinken)
  let vor = 0;
  while (vor + 1 < spur.length && spur[vor + 1].t <= ta - HALTUNG_VOR_MS + 1e-9) vor++;
  if (dot(spur[anlauf].p) - dot(spur[vor].p) > RUHE_VOR_M) return nichts('Hand war schon unterwegs', werte);
  // die Handfläche bleibt in ihrer Lage, die Form der Hand bleibt
  const nj = spur.at(-1).n;
  let minCos = 1, oMin = Infinity, oMax = -Infinity;
  for (let i = ab; i < spur.length; i++) minCos = Math.min(minCos, Math.abs(skalar(spur[i].n, nj)));
  for (const q of spur) if (q.t >= Math.min(t - ANSTOSS_ZEIT_MS, ta - HALTUNG_VOR_MS) - 1e-9) { oMin = Math.min(oMin, q.o); oMax = Math.max(oMax, q.o); }
  if (minCos < Math.cos(DREH_MAX_GRAD * GRAD) - 1e-9) return nichts(`Handfläche drehte ${Math.round(Math.acos(Math.min(1, minCos)) / GRAD)}°`, werte);
  if (oMax - oMin > FORM_SPIEL + 1e-9) return nichts(`Hand öffnete bzw. schloss sich (${(oMax - oMin).toFixed(2)})`, werte);
  return { aus: true, ...werte, grund: null };
}

function wischSchritt(z, m, t, faust, eingabe, aus) {
  const w = z.werte;
  // Spur: die Hand ohne Faust und ohne Pistole (die lockere Haltung prüft der Anstoss vor dem Anlauf)
  if (faust || z.faust || pistolenHaltung(m)) { z.spur = []; z.kandidat = null; return; }
  z.spur.push({ t, p: lageMeter(m.mitte, m.groesseLn, m.sv), n: m.normale, o: m.faust, locker: lockereHand(m, w) === null, x: m.mitte[0], rand: m.rand, imBild: m.imBild });
  while (z.spur.length > 2 && t - z.spur[0].t > 1000) z.spur.shift();
  if (z.kandidat) { wischBestaetigen(z, m, t, eingabe, aus); return; }
  if (t - z.faustEnde < FAUST_NACH_MS) return;
  let beste = null, grund = null;
  for (const R of RICHTUNGEN) {
    const a = anstossPruefen(z.spur, t, R.r, w);
    if (!a.aus) { if (a.weg >= w.anstoss_weg * 0.5 && !grund) grund = a.grund; continue; }
    const g = wischHindernis(z, R, a, m, t, eingabe);
    if (g) { grund ??= g; continue; }
    if (!beste || a.weg > beste.a.weg) beste = { R, a };
  }
  if (!beste) { if (grund) aus.grund = grund; return; }
  const an = z.spur[beste.a.anlauf];
  z.kandidat = { richtung: beste.R.name, geste: beste.R.geste, r: beste.R.r, anlaufT: an.t, vMax: beste.a.tempo };
  wischBestaetigen(z, m, t, eingabe, aus);
}

/** Was einen erkannten Anstoss am Schreibtisch trotzdem nicht zählen lässt (siehe Dateikopf); null = nichts. */
function wischHindernis(z, R, a, m, t, eingabe) {
  const w = z.werte;
  const an = z.spur[a.anlauf];
  if (eingabe >= an.t - w.sperre_ms) return 'Sperre: Maus oder Taste';
  if (t < z.sperre.gleichBis) return 'Sperre nach dem letzten Wisch';
  if (t < z.sperre.gegenBis && GEGEN[R.name] === z.sperre.richtung) return 'Gegenrichtung gesperrt';
  if (z.sichtbarSeit === null || an.t - z.sichtbarSeit < SICHTBAR_VOR_MS) return 'Hand eben erst ins Bild gekommen';
  if (an.rand < RAND_ANTEIL || an.imBild < IM_BILD_ANTEIL) return 'Hand am Bildrand';
  const winkel = seitenWinkel(an.x, w.bildwinkel_grad);
  if (w.randwinkel_grad < 90 && ((z.hand === 'links' && R.name === 'links' && winkel < -w.randwinkel_grad) || (z.hand === 'rechts' && R.name === 'rechts' && winkel > w.randwinkel_grad))) {
    return 'Wisch nach aussen am Bildrand';
  }
  if (w.handflaeche_grad < 90 && (handflaecheWinkel(an.n, R.r) > w.handflaeche_grad || handflaecheWinkel(m.normale, R.r) > w.handflaeche_grad)) return 'Handfläche passt nicht zur Wischrichtung';
  return null;
}

/** Der Kandidat wird zum Wisch, wenn die Hand im Bild bremst; verlässt sie das Bild oder läuft sie zu lange weiter, verfällt er. */
function wischBestaetigen(z, m, t, eingabe, aus) {
  const k = z.kandidat, w = z.werte;
  if (t - k.anlaufT > w.wisch_dauer_max_ms) { z.kandidat = null; aus.grund = 'Wisch: Bewegung zu lang'; return; }
  if (m.rand < RAND_ANTEIL || m.imBild < IM_BILD_ANTEIL) { z.kandidat = null; aus.grund = 'Wisch: Hand verlässt das Bild'; return; }
  if (eingabe >= k.anlaufT - w.sperre_ms) { z.kandidat = null; aus.grund = 'Sperre: Maus oder Taste'; return; }
  const spur = z.spur, jetzt = spur.at(-1);
  let i = spur.length - 2;
  while (i > 0 && jetzt.t - spur[i].t < 60) i--;
  const a = spur[i];
  if (!a || jetzt.t - a.t < 30) return;
  const v = ((jetzt.p[0] - a.p[0]) * k.r[0] + (jetzt.p[1] - a.p[1]) * k.r[1]) / ((jetzt.t - a.t) / 1000);
  k.vMax = Math.max(k.vMax, v);
  if (v >= Math.max(STOPP_TEMPO_MS, STOPP_ANTEIL * k.vMax)) return;   // noch unterwegs
  z.kandidat = null;
  z.spur = [];
  z.sperre = { gleichBis: t + w.wisch_sperre_ms / 2, gegenBis: t + w.wisch_sperre_ms, richtung: k.richtung };
  aus.ereignisse.push({ art: 'schritt', geste: k.geste, richtung: k.richtung });
}

// ---------------------------------------------------------------------------------------------------- Aufnahmen

/** Format der Aufnahmen (aufnahmeBeenden in gesten.js, Testfolgen): Version. */
export const AUFNAHME_VERSION = 1;

/** Bild einer Aufnahme → Eingabe für schritt (ohne letzteEingabe). Ein Bild: { t, punkte, welt, haendigkeit, score?, weitere?: [{ punkte, welt, haendigkeit, score }] }. */
export function bildAusAufnahme(b) {
  const haende = [];
  if (b?.punkte && b?.welt) haende.push({ punkte: b.punkte, welt: b.welt, haendigkeit: b.haendigkeit ?? null, score: b.score ?? 1 });
  for (const h of b?.weitere ?? []) if (h?.punkte && h?.welt) haende.push({ punkte: h.punkte, welt: h.welt, haendigkeit: h.haendigkeit ?? null, score: h.score ?? 1 });
  return { t: b?.t, haende };
}

/** Hände eines Bilds (Logik-Format) → Bild der Aufnahme (Zahlen gerundet). */
export function bildFuerAufnahme(t, haende) {
  const r = (x, n = 4) => Math.round(x * 10 ** n) / 10 ** n;
  const h = (x) => ({ punkte: x.punkte.map((q) => q.map((v) => r(v))), welt: x.welt.map((q) => q.map((v) => r(v, 5))), haendigkeit: x.haendigkeit ?? null, score: r(x.score ?? 0, 3) });
  const [erste, ...weitere] = haende ?? [];
  const b = { t: r(t, 1), punkte: null, welt: null, haendigkeit: null };
  if (erste) Object.assign(b, h(erste));
  if (weitere.length) b.weitere = weitere.map(h);
  return b;
}

/**
 * Spielt eine Aufnahme durch den Zustandsautomaten (Tests, Testfeld): Rückgabe Ereignisse (mit t), Summe der Navigation (drehen und verschieben addiert, rollen
 * addiert, zoom multipliziert), Gesten und Zustände, die vorkamen. optionen: hand (Vorgabe die der Aufnahme), stellschrauben, ansicht3d, ansichtSeitenverhaeltnis.
 */
export function aufnahmeAbspielen(a, { hand, stellschrauben, ansicht3d = true, ansichtSeitenverhaeltnis = 1.6 } = {}) {
  const z = neuerZustand({ hand: hand ?? a?.hand ?? 'links', stellschrauben: stellschrauben ?? {} });
  const sv = zahl(a?.bildhoehe) && zahl(a?.bildbreite) && a.bildbreite > 0 ? a.bildhoehe / a.bildbreite : SEITENVERHAELTNIS_VORGABE;
  const eingaben = [...(a?.eingaben ?? [])].filter(zahl).sort((x, y) => x - y);
  let ie = 0, letzte = -Infinity;
  const ereignisse = [], gesten = new Set(), zustaende = new Set(), gruende = new Set();
  const summe = { drehen: [0, 0], verschieben: [0, 0], rollen: 0, zoom: 1, bilderMitNavigation: 0, letzteNavigation: null };
  for (const b of a?.bilder ?? []) {
    while (ie < eingaben.length && eingaben[ie] <= b.t) letzte = eingaben[ie++];
    const r = schritt(z, { ...bildAusAufnahme(b), letzteEingabe: letzte, ansicht3d, seitenverhaeltnis: sv, ansichtSeitenverhaeltnis });
    for (const e of r.ereignisse) ereignisse.push({ t: b.t, ...e });
    const n = r.navigation;
    if (n) {
      summe.bilderMitNavigation++;
      summe.letzteNavigation = b.t;
      if (n.drehen) { summe.drehen[0] += n.drehen[0]; summe.drehen[1] += n.drehen[1]; }
      if (n.verschieben) { summe.verschieben[0] += n.verschieben[0]; summe.verschieben[1] += n.verschieben[1]; }
      if (n.rollen) summe.rollen += n.rollen;
      if (n.zoom) summe.zoom *= n.zoom;
    }
    if (r.geste) gesten.add(r.geste);
    zustaende.add(r.zustand);
    if (r.grund) gruende.add(r.grund);
  }
  return { ereignisse, summe, gesten: [...gesten], zustaende: [...zustaende], gruende: [...gruende] };
}

// ---------------------------------------------------------------------------------------------------- Datenschutz

/**
 * Netzsperre des Workers: nur die eigenen Dateien der Erkennung (gleicher Ursprung, /lib/mediapipe/) dürfen geladen werden. MediaPipe Tasks Vision 1.1.1 meldet sonst
 * Nutzungsdaten an https://odml.pa.googleapis.com/v1/log (Befund 26.10.2026 im Bündel); KICAD schickt nichts nach aussen.
 */
export function netzErlaubt(url, herkunft) {
  try {
    const h = new URL(String(herkunft));
    const u = new URL(typeof url === 'string' ? url : String(url?.url ?? url), h);
    return u.origin === h.origin && (u.protocol === 'http:' || u.protocol === 'https:') && u.pathname.startsWith('/lib/mediapipe/') && !u.pathname.includes('..');
  } catch { return false; }
}
