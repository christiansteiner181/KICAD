// «In VR begehen» (Nachbearbeitung N23, Meta Quest über Air Link, KICAD in Chrome am PC): die Rechenregeln, rein und ohne DOM und three
// (in Node getestet: vr.test.mjs). Die Sitzung, die Controller und das Zeichnen stehen in vr3d.js.
//
// Koordinaten wie in begehen-logik.js: x = Ost, y = oben, z = −Nord (three), Meter. Die Blickrichtung zählt als Gieren (Winkel gegen den Uhrzeigersinn von oben
// ab Projekt-x, Bogenmass; 90° = Projekt-Nord), identisch mit dem Gieren des Begehens am Bildschirm.
//
// Das «Rig» ist eine Gruppe in der Szene, in der die Kamera hängt (WebXR liefert die Kopfpose relativ zum Referenzraum `local-floor`: Ursprung auf dem Boden, y = 0).
// Das Rig sitzt auf der Bodenhöhe (OK Boden des Raums) und ist um `gieren` (three: Drehung um y, rotation.y = θ) gedreht. Ein Punkt p im Referenzraum liegt in der Welt bei
//   welt = rig + R(θ)·p,   R(θ): x' = x·cosθ + z·sinθ,  z' = −x·sinθ + z·cosθ.
// Blickt der Kopf im Referenzraum mit dem Gieren g0, blickt er in der Welt mit g0 + θ.
//
// ANNAHMEN (Christian beim Test mit der Brille zu bestätigen):
//   linker Stick = gehen (vor/zurück/seitlich, 1.4 m/s in Blickrichtung des Kopfes, ohne Höhe), Stick gedrückt = dreifach schneller;
//   rechter Stick seitlich = Drehen in 30°-Schritten (Snap-Turn); Abzug (bzw. Pinch mit der Hand) halten = Zielstrahl, Loslassen = Teleport;
//   Taste A (rechts) = IFC-Unterlage ein/aus; keine Kollision.

import { GESCHWINDIGKEIT, SCHNELL, DURCHGANG_MM } from './begehen-logik.js';

export { DURCHGANG_MM };
const GRAD = Math.PI / 180;
const zahl = (x) => typeof x === 'number' && Number.isFinite(x);

/** Drehung der Schritte beim Snap-Turn (Grad). */
export const SNAP_GRAD = 30;
/** Ab dieser Auslenkung des rechten Sticks (0 … 1) dreht ein Schritt; erst unter SNAP_LOS ist der nächste möglich. */
export const SNAP_AN = 0.7;
export const SNAP_LOS = 0.3;
/** Totzone des linken Sticks. */
export const STICK_TOTZONE = 0.15;
/** Der Zielstrahl reicht so weit (m). */
export const TELEPORT_REICHWEITE = 15;
/** Ein Strahl, der weniger steil als dies nach unten zeigt, trifft den Boden nicht (Sinus des Winkels). */
export const TELEPORT_MIN_STEIGUNG = 0.02;
/** IFC-Unterlagen mit mehr Dreiecken sind in VR zuerst ausgeblendet (Bildrate vor Vollständigkeit); mit Taste A einblendbar. */
export const IFC_GRENZE_DREIECKE = 300000;
/** Texte der Knöpfe. */
export const VR_TEXT = Object.freeze({
  knopf: 'In VR begehen',
  beenden: 'VR beenden',
  startet: 'VR startet …',
  keineBrille: 'Keine VR-Brille verbunden (Quest Link/Air Link starten)',
  keinBrowser: 'Dieser Browser kann kein WebXR (Chrome am PC benutzen)',
});

// ---------------------------------------------------------------------------------------------------- Knopf

/**
 * Zustand des Knopfes «In VR begehen».
 * Eingabe { xrVorhanden, unterstuetzt (bool | null = noch nicht geprüft), aktiv, startet }.
 * Rückgabe { text, deaktiviert, hinweis } – ohne Brille ist der Knopf ausgegraut und trägt den Hinweis.
 */
export function knopfZustand({ xrVorhanden = false, unterstuetzt = null, aktiv = false, startet = false } = {}) {
  if (aktiv) return { text: VR_TEXT.beenden, deaktiviert: false, hinweis: '' };
  if (startet) return { text: VR_TEXT.startet, deaktiviert: true, hinweis: '' };
  if (!xrVorhanden) return { text: VR_TEXT.knopf, deaktiviert: true, hinweis: VR_TEXT.keinBrowser };
  if (unterstuetzt !== true) return { text: VR_TEXT.knopf, deaktiviert: true, hinweis: VR_TEXT.keineBrille };
  return { text: VR_TEXT.knopf, deaktiviert: false, hinweis: '' };
}

/** Optionen für navigator.xr.requestSession('immersive-vr', …): Boden als Referenz verlangt, Hand-Tracking wenn vorhanden. */
export const sitzungsOptionen = () => ({ requiredFeatures: ['local-floor'], optionalFeatures: ['hand-tracking'] });

// ---------------------------------------------------------------------------------------------------- Rig

/** Dreht einen Punkt (x, z) im Referenzraum mit dem Rig-Gieren θ in die Welt (ohne Verschiebung). */
export function drehe(p, theta) {
  const c = Math.cos(theta), s = Math.sin(theta);
  return { x: p.x * c + p.z * s, z: -p.x * s + p.z * c };
}

/** Gieren (Bogenmass, Zählung des Begehens) einer Blickrichtung (x, z) in three-Koordinaten. */
export const gierenVonRichtung = (r) => Math.atan2(-r.z, r.x);

/** Weltposition des Kopfes bei Rig {x, y, z, gieren} und Kopf im Referenzraum {x, y, z}. */
export function kopfWelt(rig, kopf) {
  const d = drehe(kopf, rig.gieren);
  return { x: rig.x + d.x, y: rig.y + kopf.y, z: rig.z + d.z };
}

/** Blickgieren des Kopfes in der Welt. */
export const blickGierenWelt = (rig, kopfGieren) => kopfGieren + rig.gieren;

/**
 * Rig so, dass der Kopf an der Stelle des Begehens steht und in dessen Blickrichtung schaut.
 * zustand: Anfangszustand des Begehens (begehen-logik zustandAusStandort: x, z, boden, gieren in m bzw. rad);
 * kopf: Kopfpose im Referenzraum { x, z, gieren } (die Brille sitzt nicht genau im Nullpunkt und blickt nicht genau nach −z).
 * Die Höhe des Rigs ist OK Boden; die Augenhöhe ergibt sich aus dem Kopf (local-floor), nicht aus dem Zustand.
 */
export function rigStart(zustand, kopf = { x: 0, z: 0, gieren: Math.PI / 2 }) {
  const theta = zustand.gieren - (kopf.gieren ?? Math.PI / 2);
  const d = drehe({ x: kopf.x ?? 0, z: kopf.z ?? 0 }, theta);
  return { x: zustand.x - d.x, y: zustand.boden, z: zustand.z - d.z, gieren: theta };
}

/** Drehung um den Kopf (der Kopf bleibt an seiner Weltstelle): dGrad > 0 gegen den Uhrzeigersinn (von oben), < 0 nach rechts. */
export function rigDrehen(rig, kopf, dGrad) {
  const vorher = drehe(kopf, rig.gieren);
  const theta = rig.gieren + dGrad * GRAD;
  const nachher = drehe(kopf, theta);
  return { ...rig, x: rig.x + vorher.x - nachher.x, z: rig.z + vorher.z - nachher.z, gieren: theta };
}

/** Rig so, dass der Kopf über (ziel.x, ziel.z) steht; die Höhe des Rigs wird `boden`. */
export function rigAufZiel(rig, kopf, ziel, boden = rig.y) {
  const d = drehe({ x: kopf.x, z: kopf.z }, rig.gieren);
  return { ...rig, x: ziel.x - d.x, z: ziel.z - d.z, y: boden };
}

/** Rig um (dx, dz) in der Welt verschoben (Höhe bleibt). */
export const rigVersetzen = (rig, dx, dz) => ({ ...rig, x: rig.x + dx, z: rig.z + dz });

// ---------------------------------------------------------------------------------------------------- Sticks

const mitTotzone = (v, tot = STICK_TOTZONE) => {
  const a = Math.abs(v);
  if (!zahl(v) || a <= tot) return 0;
  return Math.sign(v) * Math.min(1, (a - tot) / (1 - tot));
};

/**
 * Bewegung des linken Sticks in der Welt (m) für dt Sekunden.
 * stick: { x, y } der Achsen (y < 0 = nach vorn, wie das Gamepad der Quest sie liefert); blickGieren: Blickrichtung des Kopfes in der Welt (rad).
 * Nur waagrecht – die Höhe ändert sich nie. Rückgabe { dx, dz }.
 */
export function stickBewegung(stick, blickGieren, dt, schnell = false) {
  const vor = -mitTotzone(stick?.y), seit = mitTotzone(stick?.x);
  if (vor === 0 && seit === 0) return { dx: 0, dz: 0 };
  const v = GESCHWINDIGKEIT * (schnell ? SCHNELL : 1) * Math.max(0, dt);
  const fx = Math.cos(blickGieren), fz = -Math.sin(blickGieren);   // vorwärts
  const rx = Math.sin(blickGieren), rz = Math.cos(blickGieren);    // rechts
  const laenge = Math.hypot(vor, seit);
  const f = laenge > 1 ? 1 / laenge : 1;                            // diagonal nicht schneller
  return { dx: v * f * (fx * vor + rx * seit), dz: v * f * (fz * vor + rz * seit) };
}

/**
 * Snap-Turn: rechter Stick seitlich. zustand { bereit } (am Anfang true) → { bereit, schritt }; schritt ist +1 (links, gegen den Uhrzeigersinn), −1 (rechts) oder 0.
 * Ein Ausschlag dreht genau einmal; erst wenn der Stick wieder zurück in der Mitte ist, dreht der nächste.
 */
export function snapPruefen(zustand, x) {
  const bereit = zustand?.bereit !== false;
  if (!zahl(x)) return { bereit, schritt: 0 };
  if (bereit && Math.abs(x) >= SNAP_AN) return { bereit: false, schritt: x > 0 ? -1 : 1 };   // Stick nach rechts = nach rechts drehen
  if (!bereit && Math.abs(x) <= SNAP_LOS) return { bereit: true, schritt: 0 };
  return { bereit, schritt: 0 };
}

// ---------------------------------------------------------------------------------------------------- Teleport

/**
 * Zielstrahl auf den Boden: ursprung/richtung in der Welt; Boden = waagrechte Ebene auf der Höhe `boden` (OK Boden des Geschosses).
 * Rückgabe { x, y, z, distanz } oder null (Strahl zeigt nicht genug nach unten, trifft zu weit weg, ist unvollständig).
 */
export function teleportZiel(ursprung, richtung, boden, reichweite = TELEPORT_REICHWEITE) {
  if (!ursprung || !richtung || ![ursprung.x, ursprung.y, ursprung.z, richtung.x, richtung.y, richtung.z, boden].every(zahl)) return null;
  const l = Math.hypot(richtung.x, richtung.y, richtung.z);
  if (l === 0) return null;
  const dy = richtung.y / l;
  if (dy > -TELEPORT_MIN_STEIGUNG) return null;
  const t = (boden - ursprung.y) / dy;   // Weg entlang des normierten Strahls
  if (!(t > 0) || t > reichweite) return null;
  return { x: ursprung.x + (richtung.x / l) * t, y: boden, z: ursprung.z + (richtung.z / l) * t, distanz: t };
}

// ---------------------------------------------------------------------------------------------------- IFC-Unterlage

/** Ist die IFC-Unterlage in VR anfangs sichtbar? Bei mehr als IFC_GRENZE_DREIECKE Dreiecken nicht (Bildrate). */
export const ifcAnfangsSichtbar = (dreiecke) => !(zahl(dreiecke) && dreiecke > IFC_GRENZE_DREIECKE);

// ---------------------------------------------------------------------------------------------------- Bildzeit

/** Gleitender Mittelwert der Zeit je Bild (ms); gibt auch die Bilder pro Sekunde, die diese Zeit zulässt. */
export function bildzeit(fenster = 90) {
  const werte = [];
  return {
    neu(ms) { if (zahl(ms) && ms >= 0) { werte.push(ms); if (werte.length > fenster) werte.shift(); } },
    get mittel() { return werte.length ? werte.reduce((a, b) => a + b, 0) / werte.length : 0; },
    get maximum() { return werte.length ? Math.max(...werte) : 0; },
    get anzahl() { return werte.length; },
    leeren() { werte.length = 0; },
  };
}

// ---------------------------------------------------------------------------------------------------- Standort ohne Raum

/**
 * Standort im Format der Abfrage raum_begehen für den Start ohne Raum: am aktuellen Kamerastandpunkt der 3D-Ansicht, Blick zum Ziel der Kamera (waagrecht).
 * kamera { position, ziel } in three-Metern; boden = Höhe von OK Boden in m (die Ansicht nimmt die tiefste Stelle des Modells).
 */
export function standortAusKamera(kamera, boden, augenhoeheMm = null) {
  const p = kamera?.position, z = kamera?.ziel;
  if (!p || ![p.x, p.y, p.z].every(zahl) || !zahl(boden)) return null;
  let azimut = 90;   // sonst nach Norden
  if (z && [z.x, z.z].every(zahl) && Math.hypot(z.x - p.x, z.z - p.z) > 1e-6) azimut = gierenVonRichtung({ x: z.x - p.x, z: z.z - p.z }) / GRAD;
  const mm = (m) => Math.round(m * 1000);
  return {
    raum: null, name: 'Aktueller Standpunkt', nummer: '', geschoss: '', geschossName: '',
    standpunkt: { x: mm(p.x), y: mm(-p.z), z: mm(boden), art: 'kamera' },
    bodenOk: mm(boden),
    ...(zahl(augenhoeheMm) ? { augenhoehe: augenhoeheMm } : {}),
    blick: { azimutGrad: Math.round(azimut * 100) / 100, nickenGrad: 0 },
  };
}
