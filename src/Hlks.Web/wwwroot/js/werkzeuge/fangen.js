// Fangen für die Zeichenwerkzeuge – reine Funktionen ohne Browser (in Node testbar).
// Eingabe: Mauspunkt in Modell-mm, Fangdaten der Ansicht ({ punkte: [{ p, id }], kanten: [{ a, b, id }] }), Fangradius in
// Modell-mm (aus Bildschirmpixeln umgerechnet), letzter gesetzter Punkt (für Lot und orthogonal), Raster.
// Ausgabe: gefangener Punkt mit Art. Priorität: Endpunkt > Schnittpunkt > Mitte > Lot > Nächster > Raster; seit Modul 3 geht ein
// Anschluss (extraPunkte-Eintrag { p, art: 'anschluss', daten }, Art 'anschluss', `anschluss` = daten) allem vor.
// Shift (ortho): die Richtung vom letzten Punkt wird auf 0°/90° gezwungen; gefangen wird dann nur, was auf dieser Linie liegt.
// Annahmen (nicht aus einer Norm, Bedienvorgabe): Fangradius 10 px, Raster 100 mm – beides wird vom Aufrufer vorgegeben.

export const FANGARTEN = ['endpunkt', 'schnittpunkt', 'mitte', 'lot', 'naechster', 'raster'];
export const FANGNAMEN = { endpunkt: 'Endpunkt', schnittpunkt: 'Schnittpunkt', mitte: 'Mitte', lot: 'Lot', naechster: 'Nächster', raster: 'Raster', anschluss: 'Anschluss', frei: '' };
export const FANGRADIUS_PX = 10;     // Annahme
export const RASTER_MM = 100;        // Annahme
const EPS = 1e-9;
const MAX_KANTEN_FUER_SCHNITT = 60;  // Begrenzung der Paarbildung (Schnittpunkte nur unter den Kanten nahe der Maus)

// ------------------------------------------------------------------------------------------------ Grundfunktionen

export const abstand = (a, b) => Math.hypot(b[0] - a[0], b[1] - a[1]);
export const mitteVon = (a, b) => [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
export const rund = (x, stellen = 3) => { const f = 10 ** stellen; return Math.round(x * f) / f; };
export const rundPunkt = (p, stellen = 3) => [rund(p[0], stellen), rund(p[1], stellen)];

/** Winkel der Richtung a → b in Grad, 0° = Ost, gegen den Uhrzeigersinn, 0 … < 360. */
export function winkelGrad(a, b) {
  const w = Math.atan2(b[1] - a[1], b[0] - a[0]) * 180 / Math.PI;
  const n = ((w % 360) + 360) % 360;
  return n >= 360 - 1e-9 ? 0 : n;
}

/** Fusspunkt von p auf der Geraden a–b; t = Parameter (0 = a, 1 = b, ungeklemmt). null bei Länge 0. */
export function fusspunkt(p, a, b) {
  const dx = b[0] - a[0], dy = b[1] - a[1];
  const l2 = dx * dx + dy * dy;
  if (l2 < EPS) return null;
  const t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2;
  return { punkt: [a[0] + t * dx, a[1] + t * dy], t };
}

/** Nächster Punkt von p auf der Strecke a–b. */
export function naechsterAufStrecke(p, a, b) {
  const f = fusspunkt(p, a, b);
  if (!f) return { punkt: [a[0], a[1]], t: 0 };
  const t = Math.min(1, Math.max(0, f.t));
  return { punkt: [a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])], t };
}

/** Schnittpunkt zweier Strecken (innerhalb beider, mit kleiner Toleranz); null bei parallel oder ausserhalb. */
export function schnittStrecken(a1, a2, b1, b2) {
  const d1x = a2[0] - a1[0], d1y = a2[1] - a1[1], d2x = b2[0] - b1[0], d2y = b2[1] - b1[1];
  const kreuz = d1x * d2y - d1y * d2x;
  const l = Math.hypot(d1x, d1y) * Math.hypot(d2x, d2y);
  if (l < EPS || Math.abs(kreuz) < 1e-9 * l) return null;
  const t = ((b1[0] - a1[0]) * d2y - (b1[1] - a1[1]) * d2x) / kreuz;
  const s = ((b1[0] - a1[0]) * d1y - (b1[1] - a1[1]) * d1x) / kreuz;
  const tol = 1e-9;
  if (t < -tol || t > 1 + tol || s < -tol || s > 1 + tol) return null;
  return [a1[0] + t * d1x, a1[1] + t * d1y];
}

/** Schnittpunkt der Strecke a–b mit der unendlichen Geraden durch l0 mit Richtung d; null bei parallel oder ausserhalb der Strecke. */
function schnittStreckeGerade(a, b, l0, d) {
  const ex = b[0] - a[0], ey = b[1] - a[1];
  const kreuz = ex * d[1] - ey * d[0];
  if (Math.abs(kreuz) < 1e-9 * Math.hypot(ex, ey) * Math.hypot(d[0], d[1])) return null;
  const t = ((l0[0] - a[0]) * d[1] - (l0[1] - a[1]) * d[0]) / kreuz;
  if (t < -1e-9 || t > 1 + 1e-9) return null;
  return [a[0] + t * ex, a[1] + t * ey];
}

/** Rundet auf das Raster (mm); raster ≤ 0 lässt den Punkt unverändert. */
export function rastern(p, raster) {
  if (!(raster > 0)) return [p[0], p[1]];
  return [Math.round(p[0] / raster) * raster, Math.round(p[1] / raster) * raster];
}

/** Zwingt die Richtung letzter → p auf waagrecht oder senkrecht (die grössere Verschiebung gewinnt). */
export function orthogonal(letzter, p) {
  const dx = p[0] - letzter[0], dy = p[1] - letzter[1];
  return Math.abs(dx) >= Math.abs(dy) ? [p[0], letzter[1]] : [letzter[0], p[1]];
}

/** Punkt im Abstand laenge vom Punkt von in Richtung nach (nach darf nicht auf von liegen; sonst nach Osten). */
export function punktNachLaenge(von, nach, laenge) {
  let dx = nach[0] - von[0], dy = nach[1] - von[1];
  const l = Math.hypot(dx, dy);
  if (l < EPS) { dx = 1; dy = 0; } else { dx /= l; dy /= l; }
  return [von[0] + dx * laenge, von[1] + dy * laenge];
}

/** Zahl aus einer Eingabe: Komma oder Punkt als Dezimalzeichen, Apostroph/Leerzeichen als Tausendertrenner. null wenn ungültig. */
export function zahlAusText(text) {
  const t = String(text ?? '').trim().replace(/['’\s]/g, '').replace(',', '.');
  if (t === '' || t === '-' || t === '.' || !/^-?\d*\.?\d*$/.test(t)) return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/** Länge als Text: ganze mm («3250 mm»). */
export const laengeText = (mm) => `${Math.round(mm)} mm`;
/** Winkel als Text mit einer Dezimalstelle («45.0°»). */
export const winkelText = (grad) => `${(Math.round(grad * 10) / 10).toFixed(1)}°`;

// ------------------------------------------------------------------------------------------------ Kandidaten

function kantenNahe(kanten, p, radius) {
  const r = [];
  for (const k of kanten) {
    const minx = Math.min(k.a[0], k.b[0]) - radius, maxx = Math.max(k.a[0], k.b[0]) + radius;
    if (p[0] < minx || p[0] > maxx) continue;
    const miny = Math.min(k.a[1], k.b[1]) - radius, maxy = Math.max(k.a[1], k.b[1]) + radius;
    if (p[1] < miny || p[1] > maxy) continue;
    r.push(k);
  }
  return r;
}

function besser(alt, abst) { return alt === null || abst < alt.abst; }

/** Alle Fangpunkte (Daten der Ansicht + eigene Zusatzpunkte). Anschlüsse (Modul 3: { p, art: 'anschluss', daten }) fängt der Anschlussfang. */
function punkteListe(daten, extraPunkte) {
  const l = [];
  for (const q of daten?.punkte ?? []) if (q?.p) l.push({ p: q.p, id: q.id ?? null });
  for (const p of extraPunkte ?? []) if (Array.isArray(p)) l.push({ p, id: null });
  return l;
}

/** Anschluss-Fangpunkte aus extraPunkte: Einträge { p: [x, y], art: 'anschluss', daten } (Modul 3, Leitungen). */
const anschlussPunkte = (extraPunkte) => (extraPunkte ?? []).filter((q) => q && !Array.isArray(q) && q.art === 'anschluss' && Array.isArray(q.p));

function kantenListe(daten, extraKanten) {
  const l = [...(daten?.kanten ?? [])];
  for (const k of extraKanten ?? []) l.push(k);
  return l.filter((k) => k?.a && k?.b);
}

// ------------------------------------------------------------------------------------------------ Hauptfunktion

/**
 * Fängt den Mauspunkt.
 * opt: { punkt, daten, radius, letzter?, raster?, ortho?, extraPunkte?, extraKanten?, arten? }
 *   arten: Menge der erlaubten Arten (Vorgabe alle); 'raster' schaltet das Raster, raster = 0 ebenso.
 * Rückgabe: { punkt: [x, y], art, id?, kante?: { a, b, id }, ortho: bool }; art 'frei' ohne Treffer und ohne Raster.
 */
export function fangen(opt) {
  const { punkt, daten = null, radius = 0, letzter = null, raster = 0, ortho = false, extraPunkte = [], extraKanten = [] } = opt;
  const erlaubt = new Set(opt.arten ?? [...FANGARTEN, 'anschluss']);
  const punkte = punkteListe(daten, extraPunkte);
  const kanten = kantenListe(daten, extraKanten);
  const anschluesse = erlaubt.has('anschluss') ? anschlussPunkte(extraPunkte) : [];

  if (ortho && letzter) return fangenOrtho({ punkt, punkte, kanten, radius, letzter, raster, erlaubt, anschluesse });

  // 0. Anschluss (Modul 3): Anschlusspunkte von Geräten, Familien und Leitungsenden gehen allen anderen Fängen vor
  {
    let best = null;
    for (const q of anschluesse) { const d = abstand(q.p, punkt); if (d <= radius && besser(best, d)) best = { abst: d, q }; }
    if (best) return { punkt: [best.q.p[0], best.q.p[1]], art: 'anschluss', anschluss: best.q.daten, ortho: false };
  }

  const nahe = kantenNahe(kanten, punkt, radius);

  // 1. Endpunkt: Fangpunkte und Kantenenden
  if (erlaubt.has('endpunkt')) {
    let best = null;
    const pruefe = (p, id) => { const d = abstand(p, punkt); if (d <= radius && besser(best, d)) best = { abst: d, p, id }; };
    for (const q of punkte) pruefe(q.p, q.id);
    for (const k of nahe) { pruefe(k.a, k.id ?? null); pruefe(k.b, k.id ?? null); }
    if (best) return { punkt: [best.p[0], best.p[1]], art: 'endpunkt', id: best.id ?? undefined, ortho: false };
  }

  // 2. Schnittpunkt zweier Kanten
  if (erlaubt.has('schnittpunkt') && nahe.length >= 2) {
    const dist = (k) => abstand(naechsterAufStrecke(punkt, k.a, k.b).punkt, punkt);
    const liste = nahe.length > MAX_KANTEN_FUER_SCHNITT ? [...nahe].sort((x, y) => dist(x) - dist(y)).slice(0, MAX_KANTEN_FUER_SCHNITT) : nahe;
    let best = null;
    for (let i = 0; i < liste.length; i++)
      for (let j = i + 1; j < liste.length; j++) {
        const s = schnittStrecken(liste[i].a, liste[i].b, liste[j].a, liste[j].b);
        if (!s) continue;
        const d = abstand(s, punkt);
        if (d <= radius && besser(best, d)) best = { abst: d, p: s };
      }
    if (best) return { punkt: best.p, art: 'schnittpunkt', ortho: false };
  }

  // 3. Mitte einer Kante
  if (erlaubt.has('mitte')) {
    let best = null;
    for (const k of nahe) {
      const m = mitteVon(k.a, k.b), d = abstand(m, punkt);
      if (d <= radius && besser(best, d)) best = { abst: d, p: m, k };
    }
    if (best) return { punkt: best.p, art: 'mitte', id: best.k.id ?? undefined, kante: best.k, ortho: false };
  }

  // 4. Lot vom letzten Punkt auf eine Kante
  if (erlaubt.has('lot') && letzter) {
    let best = null;
    for (const k of kanten) {
      const f = fusspunkt(letzter, k.a, k.b);
      if (!f || f.t < -1e-9 || f.t > 1 + 1e-9) continue;
      const d = abstand(f.punkt, punkt);
      if (d <= radius && besser(best, d)) best = { abst: d, p: f.punkt, k };
    }
    if (best) return { punkt: best.p, art: 'lot', id: best.k.id ?? undefined, kante: best.k, ortho: false };
  }

  // 5. Nächster Punkt auf einer Kante
  if (erlaubt.has('naechster')) {
    let best = null;
    for (const k of nahe) {
      const n = naechsterAufStrecke(punkt, k.a, k.b), d = abstand(n.punkt, punkt);
      if (d <= radius && besser(best, d)) best = { abst: d, p: n.punkt, k };
    }
    if (best) return { punkt: best.p, art: 'naechster', id: best.k.id ?? undefined, kante: best.k, ortho: false };
  }

  // 6. Raster
  if (erlaubt.has('raster') && raster > 0) return { punkt: rastern(punkt, raster), art: 'raster', ortho: false };
  return { punkt: [punkt[0], punkt[1]], art: 'frei', ortho: false };
}

/** Fangen mit Shift: nur Punkte auf der waagrechten/senkrechten Linie durch den letzten Punkt. */
function fangenOrtho({ punkt, punkte, kanten, radius, letzter, raster, erlaubt, anschluesse = [] }) {
  const dx = Math.abs(punkt[0] - letzter[0]), dy = Math.abs(punkt[1] - letzter[1]);
  const waagrecht = dx >= dy;
  const d = waagrecht ? [1, 0] : [0, 1];
  const auf = (q) => (waagrecht ? [q[0], letzter[1]] : [letzter[0], q[1]]);
  const quer = (q) => (waagrecht ? Math.abs(q[1] - letzter[1]) : Math.abs(q[0] - letzter[0]));
  const wunsch = auf(punkt);
  const treffer = (art, p, extra = {}) => ({ punkt: p, art, ortho: true, ...extra });

  {
    let best = null;
    for (const q of anschluesse) {
      if (quer(q.p) > radius) continue;
      const dist = abstand(auf(q.p), wunsch);
      if (dist <= radius && besser(best, dist)) best = { abst: dist, q };
    }
    if (best) return treffer('anschluss', auf(best.q.p), { anschluss: best.q.daten });
  }
  if (erlaubt.has('endpunkt')) {
    let best = null;
    const pruefe = (p, id) => {
      if (quer(p) > radius) return;
      const q = auf(p), dist = abstand(q, wunsch);
      if (dist <= radius && besser(best, dist)) best = { abst: dist, p: q, id };
    };
    for (const q of punkte) pruefe(q.p, q.id);
    for (const k of kanten) { pruefe(k.a, k.id ?? null); pruefe(k.b, k.id ?? null); }
    if (best) return treffer('endpunkt', best.p, { id: best.id ?? undefined });
  }
  if (erlaubt.has('schnittpunkt')) {
    let best = null;
    for (const k of kantenNahe(kanten, wunsch, radius)) {
      const s = schnittStreckeGerade(k.a, k.b, letzter, d);
      if (!s) continue;
      const dist = abstand(s, wunsch);
      if (dist <= radius && besser(best, dist)) best = { abst: dist, p: s };
    }
    if (best) return treffer('schnittpunkt', best.p);
  }
  if (erlaubt.has('mitte')) {
    let best = null;
    for (const k of kanten) {
      const m = mitteVon(k.a, k.b);
      if (quer(m) > radius) continue;
      const q = auf(m), dist = abstand(q, wunsch);
      if (dist <= radius && besser(best, dist)) best = { abst: dist, p: q, k };
    }
    if (best) return treffer('mitte', best.p, { id: best.k.id ?? undefined, kante: best.k });
  }
  // Raster: die Länge entlang der Linie wird gerundet
  if (erlaubt.has('raster') && raster > 0) {
    const s = waagrecht ? punkt[0] - letzter[0] : punkt[1] - letzter[1];
    const sr = Math.round(s / raster) * raster;
    return treffer('raster', waagrecht ? [letzter[0] + sr, letzter[1]] : [letzter[0], letzter[1] + sr]);
  }
  return treffer('frei', wunsch);
}
