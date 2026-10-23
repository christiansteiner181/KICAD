// «PC sieht die Brille» (Modul XR, Quest App P7; Entscheid Christian 23.10.2026: «Die 3D-Ansicht am PC zeigt Standort und Blickrichtung der Brille»): reine Regeln ohne three.js
// und ohne DOM (Node-Test: xr-teilnehmer.test.mjs). Aus der Antwort von GET /api/xr/teilnehmer wird je Brille eine einfache Figur: Kopf mit Blickrichtung, Hände als Punkte, Name.
// Die Darstellung (three.js) steht in xr-teilnehmer.js; die Abfrage im Takt dort ebenfalls.
//
// Koordinaten: Der Kanal liefert KICAD-Millimeter (x Ost, y Nord, z oben) und Drehungen als Quaternion [x, y, z, w] im selben System (KicadUmrechnung.cs der App). Die 3D-Ansicht
// rechnet wie alle Körper: three-Meter x = x, y = z (oben), z = −y (koerper3d.js). Ein Quaternion wechselt mit demselben Achsentausch (x, z, −y) – derselbe Wechsel ist eine echte
// Drehung (Determinante 1), darum gilt für den Vektorteil dieselbe Regel.
// Blickrichtung: In der App schaut der Kopf ohne Drehung nach −Z (StereoKit, y oben). KicadUmrechnung bildet (x, y, z) der App auf (x, −z, y) in KICAD ab: ohne Drehung zeigt der
// Blick also nach +y (Norden), und oben ist +z. Die Drehung des Kopfes dreht diese beiden Richtungen.

/** mm → m. */
export const MM_ZU_M = 0.001;
/** Blickrichtung des Kopfes ohne Drehung, in KICAD-Achsen (die App schaut nach −Z, KicadUmrechnung bildet das auf +y ab). */
export const BLICK_LOKAL = Object.freeze([0, 1, 0]);
/** «Oben» des Kopfes ohne Drehung, in KICAD-Achsen. */
export const OBEN_LOKAL = Object.freeze([0, 0, 1]);
/** Eine Pose gilt als veraltet, wenn sie älter ist (ms): die Brille sendet nicht mehr (abgesetzt, App im Hintergrund). */
export const VERALTET_MS = 5000;
/** Abfrageabstand (ms), solange mindestens eine Brille verbunden ist (Auftrag P7: alle 200 ms). */
export const TAKT_MIT_BRILLE_MS = 200;
/** Abfrageabstand (ms) ohne Brille: nur ein winziger Blick auf die Liste, damit eine neu verbundene Brille bald erscheint («keine Last ohne Brille»). */
export const TAKT_OHNE_BRILLE_MS = 4000;
/** Abstand (ms), in dem bei ausgeschalteter Anzeige oder verstecktem Fenster nur noch die Einstellung geprüft wird (keine Anfrage an den Server). */
export const TAKT_AUS_MS = 3000;
/** Namen im Etikett höchstens so lang. */
export const NAME_MAX = 40;

/** Farben der Brillen (gut unterscheidbar vom Modell, hell und dunkel). */
export const FARBEN = Object.freeze(['#e8590c', '#1c7ed6', '#2f9e44', '#ae3ec9', '#f08c00', '#0b7285', '#c92a2a', '#5f3dc4']);

const endlich = (v) => typeof v === 'number' && Number.isFinite(v);
const vektor = (a, n) => Array.isArray(a) && a.length === n && a.every(endlich);

/** KICAD-Punkt (mm, z oben) → three-Punkt (m, y oben): (x, z, −y). Ungültig → null. */
export function punktNachThree(p) {
  return vektor(p, 3) ? [p[0] * MM_ZU_M, p[2] * MM_ZU_M, -p[1] * MM_ZU_M] : null;
}

/** KICAD-Richtung (ohne Einheit) → three-Richtung: (x, z, −y). */
export function richtungNachThree(v) {
  return vektor(v, 3) ? [v[0], v[2], -v[1]] : null;
}

/** Dreht den Vektor v mit dem Quaternion q = [x, y, z, w] (Einheitsquaternion; ein anderes wird normiert, Null ergibt v). */
export function dreheVektor(q, v) {
  if (!vektor(q, 4) || !vektor(v, 3)) return null;
  const l = Math.hypot(q[0], q[1], q[2], q[3]);
  if (!(l > 1e-9)) return [...v];
  const [x, y, z, w] = [q[0] / l, q[1] / l, q[2] / l, q[3] / l];
  // v' = v + 2w (u × v) + 2 u × (u × v), mit u = (x, y, z)
  const tx = 2 * (y * v[2] - z * v[1]);
  const ty = 2 * (z * v[0] - x * v[2]);
  const tz = 2 * (x * v[1] - y * v[0]);
  return [v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx)];
}

const einheit = (v) => { const l = Math.hypot(v[0], v[1], v[2]); return l > 1e-12 ? [v[0] / l, v[1] / l, v[2] / l] : v; };
const rund = (a) => a.map((x) => Math.round(x * 1e6) / 1e6);

/** Kopf einer Pose (KICAD) → { pos (three, m), blick (three, Einheitsvektor), oben (three, Einheitsvektor) }; ungültig → null. */
export function kopfFigur(kopf) {
  const pos = punktNachThree(kopf?.pos);
  if (!pos || !vektor(kopf?.rot, 4)) return null;
  const blick = dreheVektor(kopf.rot, BLICK_LOKAL);
  const oben = dreheVektor(kopf.rot, OBEN_LOKAL);
  return { pos: rund(pos), blick: rund(einheit(richtungNachThree(blick))), oben: rund(einheit(richtungNachThree(oben))) };
}

/** Blickwinkel waagrecht in Grad (0 = Norden, im Uhrzeigersinn: 90 = Osten) – für die Anzeige im Dialog und die Prüfung. Aus dem Blick in KICAD-Achsen. */
export function blickGrad(kopf) {
  const b = vektor(kopf?.rot, 4) ? dreheVektor(kopf.rot, BLICK_LOKAL) : null;
  if (!b || Math.hypot(b[0], b[1]) < 1e-6) return null;
  return Math.round(((Math.atan2(b[0], b[1]) * 180 / Math.PI) + 360) % 360 * 10) / 10;
}

/** Eine Hand → { gelenk, spitze (oder null), pinch } in three-Metern; nicht erkannt oder ungültig → null. */
export function handFigur(hand, seite) {
  if (!hand || typeof hand !== 'object') return null;
  const gelenk = punktNachThree(hand.gelenk?.pos);
  if (!gelenk) return null;
  return { seite, gelenk: rund(gelenk), spitze: punktNachThree(hand.zeigefinger) ? rund(punktNachThree(hand.zeigefinger)) : null, pinch: hand.pinch === true };
}

/** Farbe einer Brille: stabil nach Teilnehmer-Id («t…»), unabhängig von der Reihenfolge der Liste. */
export function farbeFuer(id) {
  let h = 0;
  for (const c of String(id ?? '')) h = (h * 31 + c.charCodeAt(0)) >>> 0;
  return FARBEN[h % FARBEN.length];
}

/** Name für das Etikett: ohne Steuerzeichen, gekürzt; leer → «Brille». */
export function etikettText(name) {
  const t = String(name ?? '').replace(/[\u0000-\u001f\u007f-\u009f]/g, ' ').replace(/\s+/g, ' ').trim();   // \s fasst auch die Zeilen- und Absatztrenner zusammen
  const k = t.length > NAME_MAX ? `${t.slice(0, NAME_MAX - 1)}…` : t;
  return k || 'Brille';
}

/**
 * Eine Brille für die Darstellung: t = Eintrag aus GET /api/xr/teilnehmer, jetztMs = Uhr des PCs (Date.now()).
 * Ohne Pose (noch keine gesendet) → null. veraltet: die letzte Pose ist älter als VERALTET_MS (die Figur bleibt, blass).
 */
export function teilnehmerFigur(t, jetztMs = Date.now()) {
  if (!t || typeof t !== 'object' || !t.pose) return null;
  const kopf = kopfFigur(t.pose.kopf);
  if (!kopf) return null;
  const zeit = Date.parse(t.poseZeit ?? '');
  const alter = Number.isFinite(zeit) ? Math.max(0, jetztMs - zeit) : null;
  const haende = [handFigur(t.pose.links, 'links'), handFigur(t.pose.rechts, 'rechts')].filter(Boolean);
  const name = etikettText(t.brillenName || t.name);
  return {
    id: String(t.id ?? ''), name, farbe: farbeFuer(t.id), kopf, haende,
    etikett: { text: name, pos: rund([kopf.pos[0], kopf.pos[1] + 0.3, kopf.pos[2]]) },
    modus: t.pose.modus === 'sitzungsraum' ? 'sitzungsraum' : 'begehen',
    amTisch: t.pose.modus === 'sitzungsraum',   // Sitzungsraum: die Pose gilt am verkleinerten Tisch, nicht im Gebäude – die 3D-Ansicht zeichnet sie nicht ins Modell
    veraltet: alter === null ? false : alter > VERALTET_MS,
    alterMs: alter,
  };
}

/** Alle Figuren der Antwort von GET /api/xr/teilnehmer (Brillen ohne Pose fallen weg). Ungültige Antwort → leere Liste. */
export function figurenAus(antwort, jetztMs = Date.now()) {
  const liste = Array.isArray(antwort?.teilnehmer) ? antwort.teilnehmer : [];
  return liste.map((t) => teilnehmerFigur(t, jetztMs)).filter(Boolean);
}

/**
 * Wann die nächste Abfrage kommt und ob überhaupt eine nötig ist. an: Anzeige eingeschaltet, versteckt: Fenster nicht sichtbar, anzahl: Brillen in der letzten Antwort.
 * → { frage: bool, nach: ms }. Ausgeschaltet oder versteckt: keine Anfrage an den Server. Keine Brille: selten. Mit Brille: alle 200 ms.
 */
export function abfragePlan({ an = true, versteckt = false, anzahl = 0 } = {}) {
  if (!an || versteckt) return { frage: false, nach: TAKT_AUS_MS };
  return { frage: true, nach: anzahl > 0 ? TAKT_MIT_BRILLE_MS : TAKT_OHNE_BRILLE_MS };
}

/** Zeigt die Anzeige etwas? Ausgeschaltet immer nein; sonst ja, wenn mindestens eine Figur da ist. */
export const sichtbar = (an, figuren) => an !== false && (figuren?.length ?? 0) > 0;

/** Etikett mit Hinweis, wenn die Pose veraltet ist: «Quest Christian» bzw. «Quest Christian (keine Pose seit 12 s)». */
export function figurText(f) {
  if (!f) return '';
  return f.veraltet && f.alterMs !== null ? `${f.name} (keine Pose seit ${Math.round(f.alterMs / 1000)} s)` : f.name;
}
