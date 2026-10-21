// Modul XR (XR-B Masse zu Decke und Wand, XR-C Messwerkzeug mit Fang): Fang und Abstandsrechnung im Raum – rein, ohne DOM und three (in Node getestet: xr-b.test.mjs).
// Beide Teile benutzen dieselbe Fanglogik: das Messwerkzeug des Tablets setzt Punkte (Finger, Strahl), das Greifen einer Leitung zeigt die Masse zu Decke und Wand.
//
// Koordinaten: three wie die 3D-Ansicht (x = Ost, y = oben, z = −Nord), Meter, einfache Objekte {x, y, z}. Die Körper kommen vom Kern (/api/koerper) in Modell-mm
// (x Ost, y Nord, z oben) und werden hier umgerechnet. Ausgaben für den Menschen sind Modell-Masse: dx = Ost, dy = Nord, dz = oben, in mm.
//
// Fangdaten je Körper: punkte (Ecken; bei Rohren, Formteilen, Armaturen, Apparaten die Enden der Achse als «anschluss»), strecken (Kanten bzw. Achsen), flaechen
// (ebene Vielecke mit Normale nach aussen, auch mit Löchern). ANNAHMEN: Fangradius am Finger 3 cm, am Strahl 3 cm + 3 cm je Meter Abstand; Rangfolge
// Anschluss > Ecke > Mitte einer Kante > Kante/Achse > Fläche > frei; Anschlüsse von Formteilen sind die Enden ihrer Zylinderachse (der Kern liefert in den Körpern
// keine benannten Anschlüsse).

const M = 1 / 1000;
const GRAD = Math.PI / 180;
const EPS = 1e-9;

export const FANG_RADIUS_FINGER = 0.03;
export const FANG_RADIUS_STRAHL = 0.03;
export const FANG_RADIUS_JE_METER = 0.03;
export const FANGARTEN_XR = Object.freeze(['anschluss', 'ecke', 'mitte', 'achse', 'kante', 'flaeche', 'frei']);
export const FANGNAMEN_XR = Object.freeze({ anschluss: 'Anschluss', ecke: 'Ecke', mitte: 'Mitte', achse: 'Leitungsachse', kante: 'Kante', flaeche: 'Fläche', frei: 'frei' });
const ANSCHLUSS_EBENEN = new Set(['leitung', 'formteil', 'armatur', 'apparat']);

// ---------------------------------------------------------------------------------------------------- Vektoren
export const v3 = (x = 0, y = 0, z = 0) => ({ x, y, z });
export const plus = (a, b) => v3(a.x + b.x, a.y + b.y, a.z + b.z);
export const minus = (a, b) => v3(a.x - b.x, a.y - b.y, a.z - b.z);
export const mal = (a, s) => v3(a.x * s, a.y * s, a.z * s);
export const skalar = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
export const kreuz = (a, b) => v3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
export const laenge = (a) => Math.hypot(a.x, a.y, a.z);
export const abstand3 = (a, b) => laenge(minus(a, b));
export const einheit = (a) => { const l = laenge(a); return l < EPS ? v3(0, 0, 0) : mal(a, 1 / l); };
export const mitteVon3 = (a, b) => mal(plus(a, b), 0.5);
const zahl = (x) => typeof x === 'number' && Number.isFinite(x);
const gueltig = (p) => p && zahl(p.x) && zahl(p.y) && zahl(p.z);

/** Modellpunkt [x, y, z] in mm → three-Punkt in m. */
export const ausModell = (p) => v3(p[0] * M, (p[2] ?? 0) * M, -p[1] * M);

// ---------------------------------------------------------------------------------------------------- Masse
/** Abstand und Unterschiede von a nach b: Abstand und dx (Ost), dy (Nord), dz (oben) in mm. */
export function masse(a, b) {
  const d = minus(b, a);
  return { abstandMm: laenge(d) * 1000, dx: d.x * 1000, dy: -d.z * 1000, dz: d.y * 1000 };
}
/** Länge für die Anzeige: ganze Millimeter, dazu Zentimeter mit einer Stelle («1234 mm · 123.4 cm»). */
export function massText(mm) {
  const m = Math.round(Math.abs(mm) * 10) / 10;
  const mmT = Math.abs(mm) < 100 ? (Math.round(mm * 10) / 10).toFixed(1) : String(Math.round(mm));
  return `${mmT} mm · ${(Math.round(mm) / 10).toFixed(1)} cm`.replace('-0.0', '0.0') + (m < 0 ? '' : '');
}
/** Kurzform ohne Zentimeter. */
export const massKurz = (mm) => `${Math.abs(mm) < 100 ? (Math.round(mm * 10) / 10).toFixed(1) : Math.round(mm)} mm`;

/**
 * Lotrechtes Mass zu einer Fläche (Decke, Wand, Boden): Abstand von b zur Ebene der Fläche, Fusspunkt auf der Ebene.
 * flaeche { n (Normale), p (ein Punkt der Ebene) }.
 */
export function lotMass(flaeche, b) {
  const n = einheit(flaeche.n);
  const d = skalar(minus(b, flaeche.p), n);
  return { abstandMm: Math.abs(d) * 1000, vorzeichen: d >= 0 ? 1 : -1, fuss: minus(b, mal(n, d)) };
}

// ---------------------------------------------------------------------------------------------------- Körper → Fangdaten
function box0() { return { min: v3(Infinity, Infinity, Infinity), max: v3(-Infinity, -Infinity, -Infinity) }; }
function boxPlus(b, p) {
  b.min = v3(Math.min(b.min.x, p.x), Math.min(b.min.y, p.y), Math.min(b.min.z, p.z));
  b.max = v3(Math.max(b.max.x, p.x), Math.max(b.max.y, p.y), Math.max(b.max.z, p.z));
}
const boxAbstand = (b, p) => Math.hypot(Math.max(b.min.x - p.x, 0, p.x - b.max.x), Math.max(b.min.y - p.y, 0, p.y - b.max.y), Math.max(b.min.z - p.z, 0, p.z - b.max.z));

function flaecheVon(ring, n, loecher = []) {
  return { ring, n: einheit(n), p: ring[0], loecher };
}

function quaderTeil(t, ebene, ziel) {
  if (!Array.isArray(t.mitte) || !Array.isArray(t.masse)) return;
  const c = ausModell(t.mitte), [lx, ly, lz] = t.masse.map((x) => x * M / 2);
  const th = (t.drehung ?? 0) * GRAD, co = Math.cos(th), si = Math.sin(th);
  const ex = v3(co, 0, -si), ey = v3(-si, 0, -co), ez = v3(0, 1, 0);
  const ecke = (a, b, d) => plus(c, plus(plus(mal(ex, a * lx), mal(ey, b * ly)), mal(ez, d * lz)));
  const E = [];
  for (const a of [-1, 1]) for (const b of [-1, 1]) for (const d of [-1, 1]) E.push(ecke(a, b, d));
  const idx = (a, b, d) => ((a > 0 ? 4 : 0) + (b > 0 ? 2 : 0) + (d > 0 ? 1 : 0));
  for (const e of E) ziel.punkte.push({ p: e, art: 'ecke' });
  for (let i = 0; i < 8; i++) for (const bit of [1, 2, 4]) if (!(i & bit)) ziel.strecken.push({ a: E[i], b: E[i | bit], art: 'kante' });
  const seiten = [[ex, 1], [ex, -1], [ey, 1], [ey, -1], [ez, 1], [ez, -1]];
  for (const [ax, vz] of seiten) {
    const n = mal(ax, vz);
    const ring = (ax === ex ? [[vz, -1, -1], [vz, 1, -1], [vz, 1, 1], [vz, -1, 1]] : ax === ey ? [[-1, vz, -1], [1, vz, -1], [1, vz, 1], [-1, vz, 1]] : [[-1, -1, vz], [1, -1, vz], [1, 1, vz], [-1, 1, vz]])
      .map(([a, b, d]) => E[idx(a, b, d)]);
    ziel.flaechen.push(flaecheVon(ring, n));
  }
}

function flaecheRing(r, y) { return r.map((p) => v3(p[0] * M, y, -p[1] * M)); }
function prismaTeil(t, ziel) {
  const ring0 = (t.grundriss ?? []).filter((p) => Array.isArray(p) && p.length >= 2);
  if (ring0.length < 3 || !zahl(t.unten) || !zahl(t.oben)) return;
  const yu = t.unten * M, yo = t.oben * M;
  const unten = flaecheRing(ring0, yu), oben = flaecheRing(ring0, yo);
  const loecher = (t.loecher ?? []).map((l) => flaecheRing(l, 0));
  ziel.flaechen.push(flaecheVon(oben, v3(0, 1, 0), loecher));
  ziel.flaechen.push(flaecheVon(unten, v3(0, -1, 0), loecher));
  let a2 = 0;
  for (let i = 0; i < ring0.length; i++) { const p = ring0[i], q = ring0[(i + 1) % ring0.length]; a2 += p[0] * q[1] - q[0] * p[1]; }
  const vz = a2 >= 0 ? 1 : -1;   // gegen den Uhrzeigersinn (Modell): Aussennormale einer Kante (dx, dy) ist (dy, −dx)
  for (let i = 0; i < ring0.length; i++) {
    const p = ring0[i], q = ring0[(i + 1) % ring0.length];
    const dx = q[0] - p[0], dy = q[1] - p[1];
    if (Math.hypot(dx, dy) < 1e-6) continue;
    const nM = v3(dy * vz, -dx * vz, 0);   // Modell
    const n = einheit(v3(nM.x, 0, -nM.y));
    ziel.flaechen.push(flaecheVon([unten[i], unten[(i + 1) % ring0.length], oben[(i + 1) % ring0.length], oben[i]], n));
    ziel.strecken.push({ a: unten[i], b: oben[i], art: 'kante' });
    ziel.strecken.push({ a: oben[i], b: oben[(i + 1) % ring0.length], art: 'kante' });
    ziel.strecken.push({ a: unten[i], b: unten[(i + 1) % ring0.length], art: 'kante' });
    ziel.punkte.push({ p: oben[i], art: 'ecke' }, { p: unten[i], art: 'ecke' });
  }
}

/** Fangdaten eines Körpers des Kerns { id, ebene, teile[] }: { id, ebene, box, punkte, strecken, flaechen, radius }. */
export function fangdatenAusKoerper(k) {
  const z = { id: k.id, ebene: k.ebene ?? 'sonst', box: null, punkte: [], strecken: [], flaechen: [], radius: 0 };
  for (const t of k.teile ?? []) {
    if (t.art === 'quader') quaderTeil(t, z.ebene, z);
    else if (t.art === 'prisma') prismaTeil(t, z);
    else if (t.art === 'zylinder' && Array.isArray(t.von) && Array.isArray(t.nach)) {
      const a = ausModell(t.von), b = ausModell(t.nach);
      const art = ANSCHLUSS_EBENEN.has(z.ebene) ? 'anschluss' : 'ecke';
      z.strecken.push({ a, b, art: 'achse', radius: (t.radius ?? 0) * M });
      z.punkte.push({ p: a, art }, { p: b, art });
      z.radius = Math.max(z.radius, (t.radius ?? 0) * M);
    } else if (t.art === 'koerper' && Array.isArray(t.ecken)) {
      for (const e of t.ecken.slice(0, 300)) if (Array.isArray(e) && e.length >= 3) z.punkte.push({ p: ausModell(e), art: 'ecke' });
    }
  }
  const b = box0();
  for (const p of z.punkte) boxPlus(b, p.p);
  for (const s of z.strecken) { boxPlus(b, s.a); boxPlus(b, s.b); }
  for (const f of z.flaechen) for (const p of f.ring) boxPlus(b, p);
  if (b.min.x !== Infinity) z.box = b;
  return z;
}

/** Sammlung der Fangdaten; ausschluss = Ids, die nie gefangen werden (das gegriffene Element). */
export function erzeugeFangdaten() {
  const karte = new Map();
  return {
    setzen(k) { const z = fangdatenAusKoerper(k); if (z.box) karte.set(k.id, z); else karte.delete(k.id); },
    entfernen(id) { karte.delete(id); },
    leeren() { karte.clear(); },
    get anzahl() { return karte.size; },
    get(id) { return karte.get(id) ?? null; },
    alle() { return [...karte.values()]; },
  };
}

// ---------------------------------------------------------------------------------------------------- Geometrie
/** Nächster Punkt auf der Strecke a–b zu p: { p, t }. */
export function naechsterAufStrecke3(p, a, b) {
  const ab = minus(b, a), l2 = skalar(ab, ab);
  const t = l2 < EPS ? 0 : Math.min(1, Math.max(0, skalar(minus(p, a), ab) / l2));
  return { p: plus(a, mal(ab, t)), t };
}

/** Nächste Punkte von Strahl (u, r) und Strecke a–b: { tStrahl, punktStrecke, abstand }. */
export function strahlStrecke(u, r, a, b) {
  const d = minus(b, a), w = minus(u, a);
  const A = skalar(r, r), B = skalar(r, d), C = skalar(d, d), D = skalar(r, w), E = skalar(d, w);
  const N = A * C - B * B;
  let s, t;
  if (N < 1e-12) { s = 0; t = C < EPS ? 0 : Math.min(1, Math.max(0, E / C)); }
  else { t = Math.min(1, Math.max(0, (A * E - B * D) / N)); }
  const q = plus(a, mal(d, t));
  s = Math.max(0, skalar(minus(q, u), r) / A);
  const pr = plus(u, mal(r, s));
  return { tStrahl: s, punktStrecke: q, abstand: abstand3(pr, q) };
}

function dominanteAchsen(n) {
  const ax = Math.abs(n.x), ay = Math.abs(n.y), az = Math.abs(n.z);
  if (ay >= ax && ay >= az) return ['x', 'z'];
  if (ax >= az) return ['y', 'z'];
  return ['x', 'y'];
}
function imVieleck(punkt, ring, achsen) {
  const [A, B] = achsen;
  let innen = false;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const pi = ring[i], pj = ring[j];
    if ((pi[B] > punkt[B]) !== (pj[B] > punkt[B]) && punkt[A] < ((pj[A] - pi[A]) * (punkt[B] - pi[B])) / (pj[B] - pi[B]) + pi[A]) innen = !innen;
  }
  return innen;
}
/** Liegt der Punkt (auf der Ebene der Fläche) innerhalb des Vielecks (Löcher zählen als aussen)? */
export function imFlaeche(punkt, f) {
  const ax = dominanteAchsen(f.n);
  if (!imVieleck(punkt, f.ring, ax)) return false;
  for (const l of f.loecher ?? []) if (l.length >= 3 && imVieleck(punkt, l, ax)) return false;
  return true;
}
/** Parameter t (≥ 0), bei dem der Strahl die Fläche von vorn trifft (n·r < 0), sonst null. */
export function strahlFlaeche(u, r, f, beidseitig = false) {
  const nr = skalar(f.n, r);
  if (Math.abs(nr) < 1e-9 || (!beidseitig && nr > 0)) return null;
  const t = skalar(minus(f.p, u), f.n) / nr;
  if (!(t >= 0)) return null;
  return imFlaeche(plus(u, mal(r, t)), f) ? t : null;
}
/** Fusspunkt von p auf der Ebene der Fläche, wenn er im Vieleck liegt: { p, abstand } oder null. */
export function fusspunktFlaeche(p, f) {
  const d = skalar(minus(p, f.p), f.n);
  const fuss = minus(p, mal(f.n, d));
  return imFlaeche(fuss, f) ? { p: fuss, abstand: Math.abs(d) } : null;
}

// ---------------------------------------------------------------------------------------------------- Fangen
function ergebnis(art, p, id, extra = {}) { return { art, name: FANGNAMEN_XR[art], p, id: id ?? null, ...extra }; }

/**
 * Fängt einen Punkt (Finger): daten = Fangdaten, p = Punkt (m), opt { radius, ausser: Set von Ids }.
 * Rückgabe { art, name, p, id, n? } – bei «frei» ist p der Eingangspunkt.
 */
export function fangenPunkt(daten, p, opt = {}) {
  const R = opt.radius ?? FANG_RADIUS_FINGER;
  const ausser = opt.ausser;
  let bP = null, bPd = R, bPrio = 99;
  let bM = null, bMd = R;
  let bS = null, bSd = R, bSArt = 'kante';
  let bF = null, bFd = R;
  for (const z of daten.alle()) {
    if (ausser?.has(z.id) || boxAbstand(z.box, p) > R + z.radius) continue;
    for (const q of z.punkte) {
      const d = abstand3(p, q.p), prio = q.art === 'anschluss' ? 0 : 1;
      if (d <= R && (prio < bPrio || (prio === bPrio && d < bPd))) { bP = { q, z }; bPd = d; bPrio = prio; }
    }
    for (const s of z.strecken) {
      const m = mitteVon3(s.a, s.b), dm = abstand3(p, m);
      if (dm <= bMd) { bM = { p: m, z }; bMd = dm; }
      const n = naechsterAufStrecke3(p, s.a, s.b), d = abstand3(p, n.p);
      const dEff = Math.max(0, d - (s.radius ?? 0));   // an der Rohroberfläche fängt die Achse
      if (dEff <= bSd) { bS = { p: n.p, z, s }; bSd = dEff; bSArt = s.art; }
    }
    for (const f of z.flaechen) {
      const h = fusspunktFlaeche(p, f);
      if (h && h.abstand <= bFd) { bF = { p: h.p, z, f }; bFd = h.abstand; }
    }
  }
  if (bP) return ergebnis(bP.q.art, bP.q.p, bP.z.id);
  if (bM && bMd <= R * 0.5) return ergebnis('mitte', bM.p, bM.z.id);
  if (bS) return ergebnis(bSArt, bS.p, bS.z.id);
  if (bF) return ergebnis('flaeche', bF.p, bF.z.id, { n: bF.f.n, flaeche: { n: bF.f.n, p: bF.f.p } });
  return ergebnis('frei', p, null);
}

/** Erster Treffer des Strahls mit einer Fläche der Fangdaten: { t, p, id, f } oder null. */
export function strahlTreffer(daten, u, r, opt = {}) {
  let best = null;
  for (const z of daten.alle()) {
    if (opt.ausser?.has(z.id) || z.ebene === 'raum') continue;
    for (const f of z.flaechen) {
      const t = strahlFlaeche(u, r, f);
      if (t !== null && t <= (opt.reichweite ?? 30) && (!best || t < best.t)) best = { t, p: plus(u, mal(r, t)), id: z.id, f };
    }
  }
  return best;
}

/**
 * Fängt mit dem Zielstrahl: Anschluss/Ecke/Kante/Achse nahe am Strahl (Radius wächst mit dem Abstand), sonst der Treffer auf einer Fläche.
 * opt { ausser, reichweite }. Rückgabe wie fangenPunkt, plus t (Weg entlang des Strahls) – bei «frei» ohne Treffer ist p null.
 */
export function fangenStrahl(daten, u, r0, opt = {}) {
  const r = einheit(r0);
  const treffer = strahlTreffer(daten, u, r, opt);
  const reichweite = opt.reichweite ?? 30;
  let bP = null, bPrio = 99, bPd = Infinity;
  let bS = null, bSd = Infinity;
  for (const z of daten.alle()) {
    if (opt.ausser?.has(z.id)) continue;
    for (const q of z.punkte) {
      const w = minus(q.p, u), t = skalar(w, r);
      if (t <= 0 || t > reichweite || (treffer && t > treffer.t + 0.05)) continue;   // nichts hinter der ersten Fläche
      const d = laenge(minus(w, mal(r, t))), tol = FANG_RADIUS_STRAHL + FANG_RADIUS_JE_METER * t, prio = q.art === 'anschluss' ? 0 : 1;
      if (d <= tol && (prio < bPrio || (prio === bPrio && d < bPd))) { bP = { q, z, t }; bPrio = prio; bPd = d; }
    }
    for (const s of z.strecken) {
      const h = strahlStrecke(u, r, s.a, s.b);
      if (h.tStrahl <= 0 || h.tStrahl > reichweite || (treffer && h.tStrahl > treffer.t + 0.05)) continue;
      const tol = FANG_RADIUS_STRAHL + FANG_RADIUS_JE_METER * h.tStrahl + (s.radius ?? 0);
      if (h.abstand <= tol && h.abstand < bSd) { bS = { p: h.punktStrecke, z, s, t: h.tStrahl }; bSd = h.abstand; }
    }
  }
  if (bP) return { ...ergebnis(bP.q.art, bP.q.p, bP.z.id), t: bP.t };
  if (bS) return { ...ergebnis(bS.s.art, bS.p, bS.z.id), t: bS.t };
  if (treffer) return { ...ergebnis('flaeche', treffer.p, treffer.id, { n: treffer.f.n, flaeche: { n: treffer.f.n, p: treffer.f.p } }), t: treffer.t };
  return { ...ergebnis('frei', null, null), t: null };
}

// ---------------------------------------------------------------------------------------------------- Decke und Wände (XR-B)
const DECKEN_EBENEN = new Set(['decke']);
const WAND_EBENEN = new Set(['wand']);

/**
 * Masse einer Stelle p zur nächsten Decke darüber und zur nächsten Wand je Seite: seiten = [Richtung1, Richtung2] (waagrechte Einheitsvektoren), ausser = Ids.
 * Rückgabe { decke, wandA, wandB }, jeweils { abstandMm, abstandRandMm (minus radius), punkt, id } oder null. Gerechnet wird gegen die 3D-Körper (Ebenen decke und wand);
 * der Kern hat dafür keine Abfrage (Stand 21.10.2026).
 */
export function deckeUndWaende(daten, p, seiten, opt = {}) {
  const radius = opt.radius ?? 0;
  const treffe = (richtung, ebenen, ok) => {
    let best = null;
    for (const z of daten.alle()) {
      if (opt.ausser?.has(z.id) || !ebenen.has(z.ebene)) continue;
      for (const f of z.flaechen) {
        if (!ok(f.n)) continue;
        const t = strahlFlaeche(p, richtung, f);
        if (t !== null && (!best || t < best.t)) best = { t, f, id: z.id };
      }
    }
    return best ? { abstandMm: best.t * 1000, abstandRandMm: Math.max(0, best.t - radius) * 1000, punkt: plus(p, mal(richtung, best.t)), id: best.id } : null;
  };
  const decke = treffe(v3(0, 1, 0), DECKEN_EBENEN, (n) => n.y < -0.7);
  const wand = (s) => (s ? treffe(einheit(s), WAND_EBENEN, (n) => Math.abs(n.y) < 0.3) : null);
  return { decke, wandA: wand(seiten?.[0]), wandB: wand(seiten?.[1]) };
}
