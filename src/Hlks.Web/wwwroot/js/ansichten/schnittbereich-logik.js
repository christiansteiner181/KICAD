// 3D-Schnittbereich (Nachbearbeitung N13): die Rechenregeln, rein und ohne DOM und three (in Node getestet: schnittbereich.test.mjs).
// Zeichnen, Clipping und Bedienen stehen in schnittbereich3d.js; gespeichert wird die Ansichtseigenschaft schnittbereich über den Kern.
//
// Ein Schnittbereich ist ein Quader { aktiv, min [u, v, z], max [u, v, z], winkel } in Modell-mm. Seine Achsen u, v sind um die Hochachse (Ursprung des
// Modells) um winkel (Grad, gegen den Uhrzeigersinn) gegen die Modellachsen x, y gedreht: u = x·cos + y·sin, v = −x·sin + y·cos; z ist absolut.
// Der Winkel richtet den Quader an der Gebäudeausrichtung aus (wuerfel-logik.js, gebaeudeAusrichtungGrad = −planwinkel).
//
// Koordinaten der 3D-Ansicht (three): x = Ost, y = oben, z = −Nord, Einheit Meter. Alle Funktionen mit «Welt» rechnen in diesen Koordinaten.

const GRAD = Math.PI / 180;
const M = 1 / 1000;

/** Kleinste Kantenlänge des Quaders in mm (beim Ziehen eines Griffs). */
export const MIN_KANTE = 100;
/** Rand um die Auswahl bei «An Auswahl anpassen» in mm (Entscheid Hauptlauf 21.10.2026). */
export const RAND_AUSWAHL = 300;
/** Rand um den Raum bei «Raum im 3D zeigen» in mm (N14; Benutzereinstellung, Vorgabe 1500). */
export const RAND_RAUM = 1500;
/** Schlüssel der Benutzereinstellung für den Rand von «Raum im 3D zeigen» (mm). */
export const RAND_RAUM_SCHLUESSEL = 'ansicht3d.raumrand';

const runden = (x, n = 1) => { const f = 10 ** n; const r = Math.round(x * f) / f; return r === 0 ? 0 : r; };
const zahl = (v) => typeof v === 'number' && Number.isFinite(v);
const dreier = (a) => Array.isArray(a) && a.length === 3 && a.every(zahl);

/** Prüft einen gespeicherten Schnittbereich; null, wenn er fehlt oder ungültig ist, sonst { aktiv, min, max, winkel } (Kopie). */
export function schnittbereichLesen(roh) {
  if (!roh || typeof roh !== 'object' || !dreier(roh.min) || !dreier(roh.max)) return null;
  for (let i = 0; i < 3; i++) if (!(roh.max[i] - roh.min[i] >= 1)) return null;
  return { aktiv: roh.aktiv !== false, min: [...roh.min], max: [...roh.max], winkel: zahl(roh.winkel) ? roh.winkel : 0 };
}

/** Wert für die Eigenschaft (auf 0.1 mm gerundet, so wie ihn der Kern speichert). */
export function fuerSpeichern(sb) {
  return { aktiv: !!sb.aktiv, min: sb.min.map((x) => runden(x)), max: sb.max.map((x) => runden(x)), winkel: runden(sb.winkel ?? 0, 4) };
}

/** Gleiche Quader (auf 0.1 mm)? */
export function gleich(a, b) {
  if (!a || !b) return a === b;
  const x = fuerSpeichern(a), y = fuerSpeichern(b);
  return x.aktiv === y.aktiv && x.winkel === y.winkel && x.min.every((v, i) => v === y.min[i]) && x.max.every((v, i) => v === y.max[i]);
}

// ---------------------------------------------------------------------------------------------------- Achsen und Ebenen

/** Einheitsvektoren der Quaderachsen in Welt-Koordinaten (three): u, v, z (= oben). */
export function achsen(winkelGrad) {
  const t = winkelGrad * GRAD, c = Math.cos(t), s = Math.sin(t);
  return { u: [c, 0, -s], v: [-s, 0, -c], z: [0, 1, 0] };
}

const NAMEN = { u: ['u−', 'u+'], v: ['v−', 'v+'], z: ['z−', 'z+'] };
const ACHSEN = ['u', 'v', 'z'];

/**
 * Die sechs Begrenzungsebenen für das Clipping (three: Punkte mit negativem Abstand werden abgeschnitten, also zeigt die Normale ins Innere).
 * Jede Ebene: { name, achse, seite (−1 min | +1 max), normale [x, y, z], konstante } in Metern; Abstand = normale · p + konstante.
 */
export function ebenen(sb) {
  const a = achsen(sb.winkel ?? 0);
  const aus = [];
  ACHSEN.forEach((ach, i) => {
    const n = a[ach];
    aus.push({ name: NAMEN[ach][0], achse: ach, seite: -1, normale: [...n], konstante: -sb.min[i] * M });
    aus.push({ name: NAMEN[ach][1], achse: ach, seite: 1, normale: n.map((x) => (x === 0 ? 0 : -x)), konstante: sb.max[i] * M });
  });
  return aus;
}

/** Liegt der Weltpunkt p ({x, y, z} in m) im Quader (mit Toleranz eps in m)? */
export function enthaelt(sb, p, eps = 1e-3) {
  for (const e of ebenen(sb)) if (e.normale[0] * p.x + e.normale[1] * p.y + e.normale[2] * p.z + e.konstante < -eps) return false;
  return true;
}

/** Weltpunkt (m) aus Quaderkoordinaten (u, v, z in mm). */
export function weltPunkt(sb, u, v, z) {
  const a = achsen(sb.winkel ?? 0);
  const um = u * M, vm = v * M;
  return { x: a.u[0] * um + a.v[0] * vm, y: z * M, z: a.u[2] * um + a.v[2] * vm };
}

/** Die acht Ecken des Quaders in Welt-Koordinaten (m). */
export function ecken(sb) {
  const aus = [];
  for (const u of [sb.min[0], sb.max[0]]) for (const v of [sb.min[1], sb.max[1]]) for (const z of [sb.min[2], sb.max[2]]) aus.push(weltPunkt(sb, u, v, z));
  return aus;
}

/** Umschliessende Achsenbox (Welt) des Quaders: { min: {x, y, z}, max: {x, y, z} } in m. */
export function weltGrenzen(sb) {
  const e = ecken(sb);
  const f = (k, fn) => fn(...e.map((p) => p[k]));
  return { min: { x: f('x', Math.min), y: f('y', Math.min), z: f('z', Math.min) }, max: { x: f('x', Math.max), y: f('y', Math.max), z: f('z', Math.max) } };
}

// ---------------------------------------------------------------------------------------------------- Griffe

/** Die sechs Griffe, je einer in der Mitte einer Fläche: { name, achse, seite, mitte {x, y, z} (m), nach aussen [x, y, z] (Einheitsvektor, Welt) }. */
export function griffe(sb) {
  const a = achsen(sb.winkel ?? 0);
  const mid = [0, 1, 2].map((i) => (sb.min[i] + sb.max[i]) / 2);
  const aus = [];
  ACHSEN.forEach((ach, i) => {
    for (const seite of [-1, 1]) {
      const koord = [...mid];
      koord[i] = seite < 0 ? sb.min[i] : sb.max[i];
      aus.push({ name: NAMEN[ach][seite < 0 ? 0 : 1], achse: ach, seite, mitte: weltPunkt(sb, koord[0], koord[1], koord[2]), nachAussen: a[ach].map((x) => (seite < 0 ? (x === 0 ? 0 : -x) : x)) });
    }
  });
  return aus;
}

/** Verschiebt die Fläche (achse 'u' | 'v' | 'z', seite −1 | +1) um deltaMm nach aussen (negativ = nach innen); die Kante bleibt mindestens `mindest` lang. */
export function flaecheSchieben(sb, achse, seite, deltaMm, mindest = MIN_KANTE) {
  const i = ACHSEN.indexOf(achse);
  const neu = { ...sb, min: [...sb.min], max: [...sb.max] };
  if (i < 0) return neu;
  if (seite < 0) neu.min[i] = Math.min(sb.min[i] - deltaMm, sb.max[i] - mindest);
  else neu.max[i] = Math.max(sb.max[i] + deltaMm, sb.min[i] + mindest);
  return neu;
}

/**
 * Parameter t des Punkts auf der Achse P + t·d (d Einheitsvektor), der dem Strahl O + s·e am nächsten liegt – so liest man aus der Mausbewegung
 * die Verschiebung entlang einer Griffachse. null, wenn Strahl und Achse (fast) parallel sind. Alle Angaben [x, y, z].
 */
export function achsenParameter(strahlUrsprung, strahlRichtung, achsePunkt, achseRichtung) {
  const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const el = Math.hypot(...strahlRichtung) || 1;
  const e = strahlRichtung.map((x) => x / el);
  const d = achseRichtung;
  const w = [achsePunkt[0] - strahlUrsprung[0], achsePunkt[1] - strahlUrsprung[1], achsePunkt[2] - strahlUrsprung[2]];
  const b = dot(d, e), dd = dot(d, w), ee = dot(e, w);
  const nenner = 1 - b * b;
  if (nenner < 1e-6) return null;
  return (b * ee - dd) / nenner;
}

// ---------------------------------------------------------------------------------------------------- Quader aus Punkten

/**
 * Sammelt Weltpunkte (m) und liefert den kleinsten Quader in den Achsen des Winkels: add(x, y, z) je Punkt, quader(randMm) → Schnittbereich (aktiv)
 * oder null ohne Punkte. Der Rand wirkt in allen sechs Richtungen.
 */
export function quaderSammler(winkelGrad) {
  const a = achsen(winkelGrad);
  const lo = [Infinity, Infinity, Infinity], hi = [-Infinity, -Infinity, -Infinity];
  let n = 0;
  return {
    add(x, y, z) {
      const u = x * a.u[0] + z * a.u[2], v = x * a.v[0] + z * a.v[2];
      if (u < lo[0]) lo[0] = u; if (u > hi[0]) hi[0] = u;
      if (v < lo[1]) lo[1] = v; if (v > hi[1]) hi[1] = v;
      if (y < lo[2]) lo[2] = y; if (y > hi[2]) hi[2] = y;
      n++;
    },
    get anzahl() { return n; },
    quader(randMm = 0) {
      if (n === 0) return null;
      return fuerSpeichern({ aktiv: true, min: lo.map((x) => x / M - randMm), max: hi.map((x) => x / M + randMm), winkel: winkelGrad });
    },
  };
}

/** Quader mit anderer Höhe: OK/UK eines Geschosses (kote = OK fertig Boden, hoehe = Geschosshöhe, mm); Grundriss bleibt. */
export function aufGeschoss(sb, kote, hoehe) {
  return fuerSpeichern({ ...sb, min: [sb.min[0], sb.min[1], kote], max: [sb.max[0], sb.max[1], kote + hoehe] });
}

/** Standard-Winkel: Gebäudeausrichtung, auf −180 … 180 gebracht. */
export function winkelNormal(grad) {
  const w = ((((grad ?? 0) + 180) % 360) + 360) % 360 - 180;
  return runden(w, 4);
}

/** Kurztext der Masse für die Palette, z. B. «12.80 × 8.80 × 5.60 m, 37.0°». */
export function massText(sb) {
  const f = (x) => (x / 1000).toFixed(2);
  return `${f(sb.max[0] - sb.min[0])} × ${f(sb.max[1] - sb.min[1])} × ${f(sb.max[2] - sb.min[2])} m${sb.winkel ? `, ${runden(sb.winkel, 1).toFixed(1).replace('-', '−')}°` : ''}`;
}

// ---------------------------------------------------------------------------------------------------- Startansicht (Rest N11)

/** Weltpunkt (three, m) → Modellpunkt [x, y, z] in mm (x Ost, y Nord, z oben). */
export const modellPunkt = (p) => [runden(p.x / M), runden(-p.z / M), runden(p.y / M)];

/**
 * Startansicht als Eigenschaft der Ansicht: Blickrichtung (Winkel relativ zur Gebäudeausrichtung, wie wuerfel-logik.startansichtAusRichtung) plus
 * Position und Ziel der Kamera in Modell-mm und die Projektion (die 3D-Ansicht hat nur die perspektivische Kamera).
 */
export function startansichtWert(richtung, positionWelt, zielWelt) {
  return { azimutGrad: richtung.azimutGrad, polarGrad: richtung.polarGrad, position: modellPunkt(positionWelt), ziel: modellPunkt(zielWelt), projektion: 'perspektive' };
}

/**
 * Übernahme der alten Startansicht aus dem Browser (localStorage, bis N11) ins Modell – einmal. modell = Wert der Ansichtseigenschaft startansicht
 * (oder undefined/null), lokal = gemerkter Wert im Browser (oder null). Ergebnis:
 *   wert      die Startansicht, die gilt (Modell vor Browser)
 *   schreiben true, wenn der Browser-Wert ins Modell soll
 *   aufraeumen true, wenn der Browser-Wert danach entfernt werden soll (er ist übernommen oder vom Modell überholt)
 */
export function startansichtUebernahme(modell, lokal, gueltig) {
  const m = gueltig(modell), l = gueltig(lokal);
  if (m) return { wert: m, schreiben: false, aufraeumen: lokal !== null && lokal !== undefined };
  if (l) return { wert: l, schreiben: true, aufraeumen: true };
  return { wert: null, schreiben: false, aufraeumen: lokal !== null && lokal !== undefined };
}
