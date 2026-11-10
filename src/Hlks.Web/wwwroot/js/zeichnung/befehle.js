// 2D-CAD «Zeichnung», Stufe 2: Befehle der Zeichenfläche (zeichnen und ändern) und die Eingabe der Befehlszeile.
// Ein Befehl ist eine async-Funktion, die über «io» nacheinander Punkte, Zahlen, Text oder eine Auswahl verlangt – gleich, ob der
// Mensch klickt oder tippt. Der Server führt aus (/api/zeichnung/zeichnen, /aendern); hier steht kein Rechenkern.
// Kürzel wie in AutoCAD (deutsch und englisch). Getestet (Eingabe, Kürzel): befehle.test.mjs.

/** Kürzel → Befehl. */
export const KUERZEL = {
  l: 'linie', linie: 'linie', line: 'linie',
  pl: 'polylinie', polylinie: 'polylinie', pline: 'polylinie',
  re: 'rechteck', rec: 'rechteck', rechteck: 'rechteck', rectang: 'rechteck',
  k: 'kreis', c: 'kreis', kreis: 'kreis', circle: 'kreis',
  b: 'bogen', a: 'bogen', bogen: 'bogen', arc: 'bogen',
  t: 'text', dt: 'text', text: 'text',
  s: 'schieben', m: 'schieben', schieben: 'schieben', move: 'schieben',
  ko: 'kopieren', co: 'kopieren', cp: 'kopieren', kopieren: 'kopieren', copy: 'kopieren',
  dr: 'drehen', ro: 'drehen', drehen: 'drehen', rotate: 'drehen',
  v: 'skalieren', sc: 'skalieren', varia: 'skalieren', skalieren: 'skalieren', scale: 'skalieren',
  lö: 'loeschen', lo: 'loeschen', e: 'loeschen', löschen: 'loeschen', loeschen: 'loeschen', erase: 'loeschen',
  z: 'zurueck', u: 'zurueck', zurück: 'zurueck', zurueck: 'zurueck', undo: 'zurueck',
  wi: 'wieder', wieder: 'wieder', redo: 'wieder',
  di: 'messen', messen: 'messen', abstand: 'messen', dist: 'messen',
  la: 'layer', layer: 'layer',
  zg: 'grenzen', grenzen: 'grenzen',
  // Stufe 2 Teil 2
  el: 'ellipse', ellipse: 'ellipse',
  pg: 'polygon', pol: 'polygon', polygon: 'polygon',
  pu: 'punkt', po: 'punkt', punkt: 'punkt', point: 'punkt',
  mt: 'absatztext', mtext: 'absatztext', absatztext: 'absatztext',
  h: 'schraffur', sch: 'schraffur', schraffur: 'schraffur', hatch: 'schraffur', gschraff: 'schraffur',
  bem: 'bemassung', bli: 'bemassung', dli: 'bemassung', bemassung: 'bemassung', bemlinear: 'bemassung', dimlinear: 'bemassung',
  bau: 'bemausg', dal: 'bemausg', bemausg: 'bemausg', dimaligned: 'bemausg',
  sp: 'spiegeln', mi: 'spiegeln', spiegeln: 'spiegeln', mirror: 'spiegeln',
  ve: 'versetzen', o: 'versetzen', versetz: 'versetzen', versetzen: 'versetzen', offset: 'versetzen',
  su: 'stutzen', tr: 'stutzen', stutzen: 'stutzen', trim: 'stutzen',
  de: 'dehnen', ex: 'dehnen', dehnen: 'dehnen', extend: 'dehnen',
  ar: 'abrunden', f: 'abrunden', abrunden: 'abrunden', fillet: 'abrunden',
  fa: 'fasen', cha: 'fasen', fasen: 'fasen', fase: 'fasen', chamfer: 'fasen',
  ur: 'ursprung', x: 'ursprung', ursprung: 'ursprung', explode: 'ursprung',
  vb: 'verbinden', j: 'verbinden', verbinden: 'verbinden', join: 'verbinden',
  rh: 'reihe', reihe: 'reihe', array: 'reihe',
  eü: 'uebertragen', eu: 'uebertragen', ma: 'uebertragen', eiganpass: 'uebertragen', matchprop: 'uebertragen', uebertragen: 'uebertragen',
  ksich: 'speichern', speichern: 'speichern', save: 'speichern', qsave: 'speichern',
};

export const NAMEN = { linie: 'Linie', polylinie: 'Polylinie', rechteck: 'Rechteck', kreis: 'Kreis', bogen: 'Bogen', text: 'Text', schieben: 'Schieben',
  kopieren: 'Kopieren', drehen: 'Drehen', skalieren: 'Skalieren', loeschen: 'Löschen', layer: 'Layer' };

const zahl = t => { const v = Number(String(t).trim().replace(',', '.')); return Number.isFinite(v) ? v : null; };

/** Getippte Punktangabe lesen. von = letzter Punkt (für @ und Abstand), maus = Zeiger (Richtung bei reinem Abstand).
 *  «x,y» absolut · «@dx,dy» relativ · «@l<w» bzw. «l<w» polar ab von · «l» Abstand in Richtung des Zeigers. null = keine Punktangabe. */
export function punktLesen(text, von, maus) {
  let t = text.trim();
  if (!t) return null;
  const rel = t.startsWith('@');
  if (rel) t = t.slice(1);
  if (t.includes('<')) {
    const [l, w] = t.split('<').map(zahl);
    if (l == null || w == null || !von) return null;
    return [von[0] + l * Math.cos(w * Math.PI / 180), von[1] + l * Math.sin(w * Math.PI / 180)];
  }
  // Trenner der Koordinaten: Komma oder Strichpunkt; Dezimalzeichen: Punkt
  const teile = t.split(/[;,]/).map(s => s.trim());
  if (teile.length === 2) {
    const x = Number(teile[0]), y = Number(teile[1]);
    if (!Number.isFinite(x) || !Number.isFinite(y) || teile[0] === '' || teile[1] === '') return null;
    return rel ? (von ? [von[0] + x, von[1] + y] : null) : [x, y];
  }
  if (teile.length === 1 && von && maus) {
    const l = Number(teile[0]);
    const d = Math.hypot(maus[0] - von[0], maus[1] - von[1]);
    if (!Number.isFinite(l) || teile[0] === '' || d === 0) return null;
    return [von[0] + (maus[0] - von[0]) / d * l, von[1] + (maus[1] - von[1]) / d * l];
  }
  return null;
}

/** Ortho: Punkt waagrecht oder senkrecht zu von legen (die grössere Abweichung gewinnt). */
export function ortho(p, von) {
  if (!von) return p;
  return Math.abs(p[0] - von[0]) >= Math.abs(p[1] - von[1]) ? [p[0], von[1]] : [von[0], p[1]];
}

export class Abbruch extends Error { constructor() { super('abgebrochen'); } }

/** Stutzen und Dehnen: ein Objekt nach dem anderen anklicken; ein Fehlschlag (kein Schnitt) beendet den Befehl nicht. */
async function jeObjekt(io, aktion, prompt) {
  for (;;) {
    const o = await io.objekt(prompt);
    if (!o) return;
    await io.versuchen(() => io.aendern([o.kennung], aktion, { punkt: o.punkt }));
  }
}

/** Bemassung: zwei Masspunkte und die Lage der Masslinie. richtung null = waagrecht oder senkrecht je nach Lage der Masslinie (wie BEMLINEAR). */
async function mass(io, richtung) {
  const a = await io.punkt('Bemassung – erster Masspunkt');
  if (!Array.isArray(a)) return;
  const b = await io.punkt('Zweiter Masspunkt', { von: a, vorschau: m => [{ art: 'linie', p: [a, m] }] });
  if (!Array.isArray(b)) return;
  const wahl = q => richtung || (Math.abs(q[1] - (a[1] + b[1]) / 2) >= Math.abs(q[0] - (a[0] + b[0]) / 2) ? 'waagrecht' : 'senkrecht');
  const linie = q => {
    const r = wahl(q);
    if (r === 'waagrecht') return [[a[0], a[1]], [a[0], q[1]], [b[0], q[1]], [b[0], b[1]]];
    if (r === 'senkrecht') return [[a[0], a[1]], [q[0], a[1]], [q[0], b[1]], [b[0], b[1]]];
    const l = Math.hypot(b[0] - a[0], b[1] - a[1]), nx = -(b[1] - a[1]) / l, ny = (b[0] - a[0]) / l, d = (q[0] - a[0]) * nx + (q[1] - a[1]) * ny;
    return [a, [a[0] + nx * d, a[1] + ny * d], [b[0] + nx * d, b[1] + ny * d], b];
  };
  const ort = await io.punkt('Lage der Masslinie', { ohneOrtho: true, vorschau: q => [{ art: 'linie', p: linie(q) }] });
  if (!Array.isArray(ort)) return;
  await io.zeichnen([{ art: 'bemassung', von: a, bis: b, ort, richtung: wahl(ort) }]);
}

/** Die Befehle. io: punkt(prompt, {von, vorschau}) → [x, y] | null (Eingabetaste) | Wort (getippt, kein Punkt) · zahl(prompt, {von, art, vorgabe, vorschau})
 *  · text(prompt) · auswahl(prompt) → [Kennung] · zeichnen([Objekt]) · aendern(kennungen, aktion, angaben) · boxen() der Auswahl · merken. */
export const BEFEHLE = {
  async linie(io) {
    let a = await io.punkt('Linie – erster Punkt');
    if (!Array.isArray(a)) return;
    for (;;) {
      const b = await io.punkt('Nächster Punkt (Eingabetaste beendet)', { von: a, vorschau: m => [{ art: 'linie', p: [a, m] }] });
      if (!Array.isArray(b)) return;
      if (b[0] === a[0] && b[1] === a[1]) continue;
      await io.zeichnen([{ art: 'linie', von: a, bis: b }]);
      a = b;
    }
  },

  async polylinie(io) {
    const p = [];
    const a = await io.punkt('Polylinie – erster Punkt');
    if (!Array.isArray(a)) return;
    p.push(a);
    let geschlossen = false;
    for (;;) {
      const b = await io.punkt('Nächster Punkt (S schliesst, Eingabetaste beendet)', { von: p[p.length - 1], vorschau: m => [{ art: 'linie', p: [...p, m] }] });
      if (typeof b === 'string') { if (/^(s|c|schliessen|close)$/i.test(b) && p.length >= 3) { geschlossen = true; break; } continue; }
      if (!b) break;
      p.push(b);
    }
    if (p.length >= 2) await io.zeichnen([{ art: 'polylinie', punkte: p, geschlossen }]);
  },

  async rechteck(io) {
    const a = await io.punkt('Rechteck – erste Ecke');
    if (!Array.isArray(a)) return;
    const b = await io.punkt('Gegenüberliegende Ecke', { von: a, ohneOrtho: true, vorschau: m => [{ art: 'linie', p: [a, [m[0], a[1]], m, [a[0], m[1]], a] }] });
    if (!Array.isArray(b)) return;
    await io.zeichnen([{ art: 'rechteck', ecke1: a, ecke2: b }]);
  },

  async kreis(io) {
    const m = await io.punkt('Kreis – Mittelpunkt');
    if (!Array.isArray(m)) return;
    const r = await io.zahl('Radius', { von: m, art: 'abstand', vorgabe: io.merken.radius, vorschau: q => [{ art: 'kreis', m, r: Math.hypot(q[0] - m[0], q[1] - m[1]) }, { art: 'linie', p: [m, q] }] });
    if (r == null || !(r > 0)) return;
    io.merken.radius = r;
    await io.zeichnen([{ art: 'kreis', mitte: m, radius: r }]);
  },

  async bogen(io) {
    const a = await io.punkt('Bogen – Anfangspunkt');
    if (!Array.isArray(a)) return;
    const b = await io.punkt('Punkt auf dem Bogen', { von: a, ohneOrtho: true, vorschau: m => [{ art: 'linie', p: [a, m] }] });
    if (!Array.isArray(b)) return;
    const c = await io.punkt('Endpunkt', { von: b, ohneOrtho: true, vorschau: m => [{ art: 'bogen3', p: [a, b, m] }] });
    if (!Array.isArray(c)) return;
    await io.zeichnen([{ art: 'bogen', punkte: [a, b, c] }]);
  },

  async text(io) {
    const ort = await io.punkt('Text – Einfügepunkt');
    if (!Array.isArray(ort)) return;
    const h = await io.zahl('Texthöhe', { vorgabe: io.merken.texthoehe });
    if (h == null || !(h > 0)) return;
    io.merken.texthoehe = h;
    const w = await io.zahl('Drehung in Grad', { vorgabe: 0 });
    const t = await io.text('Text');
    if (!t) return;
    await io.zeichnen([{ art: 'text', ort, text: t, hoehe: h, drehungGrad: w ?? 0 }]);
  },

  async schieben(io) {
    const k = await io.auswahl('Schieben – Objekte wählen (Eingabetaste bestätigt)');
    if (!k.length) return;
    const boxen = io.boxen();
    const a = await io.punkt('Basispunkt');
    if (!Array.isArray(a)) return;
    const b = await io.punkt('Zielpunkt', { von: a, vorschau: m => [{ art: 'linie', p: [a, m] }, ...boxen.map(q => ({ art: 'box', b: q, d: [m[0] - a[0], m[1] - a[1]] }))] });
    if (!Array.isArray(b)) return;
    await io.aendern(k, 'verschieben', { dx: b[0] - a[0], dy: b[1] - a[1] });
  },

  async kopieren(io) {
    const k = await io.auswahl('Kopieren – Objekte wählen (Eingabetaste bestätigt)');
    if (!k.length) return;
    const boxen = io.boxen();
    const a = await io.punkt('Basispunkt');
    if (!Array.isArray(a)) return;
    for (;;) {
      const b = await io.punkt('Zielpunkt (Eingabetaste beendet)', { von: a, vorschau: m => [{ art: 'linie', p: [a, m] }, ...boxen.map(q => ({ art: 'box', b: q, d: [m[0] - a[0], m[1] - a[1]] }))] });
      if (!Array.isArray(b)) return;
      await io.aendern(k, 'kopieren', { dx: b[0] - a[0], dy: b[1] - a[1] }, { auswahlBehalten: true });
    }
  },

  async drehen(io) {
    const k = await io.auswahl('Drehen – Objekte wählen (Eingabetaste bestätigt)');
    if (!k.length) return;
    const a = await io.punkt('Basispunkt');
    if (!Array.isArray(a)) return;
    const w = await io.zahl('Drehwinkel in Grad (Gegenuhrzeigersinn)', { von: a, art: 'winkel', vorschau: m => [{ art: 'linie', p: [a, m] }] });
    if (w == null) return;
    await io.aendern(k, 'drehen', { um: a, winkelGrad: w });
  },

  async skalieren(io) {
    const k = await io.auswahl('Skalieren – Objekte wählen (Eingabetaste bestätigt)');
    if (!k.length) return;
    const a = await io.punkt('Basispunkt');
    if (!Array.isArray(a)) return;
    const f = await io.zahl('Faktor', {});
    if (f == null || !(f > 0)) return;
    await io.aendern(k, 'skalieren', { um: a, faktor: f });
  },

  async loeschen(io) {
    const k = await io.auswahl('Löschen – Objekte wählen (Eingabetaste bestätigt)');
    if (k.length) await io.aendern(k, 'loeschen', {});
  },

  // ---------- Stufe 2 Teil 2: weitere Objekte ----------

  async ellipse(io) {
    const m = await io.punkt('Ellipse – Mittelpunkt');
    if (!Array.isArray(m)) return;
    const h = await io.punkt('Endpunkt der Hauptachse', { von: m, vorschau: q => [{ art: 'linie', p: [m, q] }] });
    if (!Array.isArray(h)) return;
    const a = Math.hypot(h[0] - m[0], h[1] - m[1]);
    const b = await io.zahl('Halbe Nebenachse', { von: m, art: 'abstand', vorschau: q => [{ art: 'linie', p: [m, h] }, { art: 'linie', p: [m, q] }] });
    if (b == null || !(b > 0) || !(a > 0)) return;
    // die längere Achse ist die Hauptachse
    if (b <= a) await io.zeichnen([{ art: 'ellipse', mitte: m, hauptachse: h, verhaeltnis: b / a }]);
    else await io.zeichnen([{ art: 'ellipse', mitte: m, hauptachse: [m[0] - (h[1] - m[1]) / a * b, m[1] + (h[0] - m[0]) / a * b], verhaeltnis: a / b }]);
  },

  async polygon(io) {
    const n = await io.zahl('Polygon – Anzahl Seiten', { vorgabe: io.merken.seiten ?? 6 });
    if (n == null || n < 3) return;
    io.merken.seiten = Math.round(n);
    const m = await io.punkt('Mittelpunkt');
    if (!Array.isArray(m)) return;
    const ecken = q => { const r = Math.hypot(q[0] - m[0], q[1] - m[1]), w = Math.atan2(q[1] - m[1], q[0] - m[0]); return Array.from({ length: io.merken.seiten + 1 }, (_, i) => [m[0] + r * Math.cos(w + 2 * Math.PI * i / io.merken.seiten), m[1] + r * Math.sin(w + 2 * Math.PI * i / io.merken.seiten)]); };
    const e = await io.punkt('Eckpunkt (oder Radius tippen)', { von: m, vorschau: q => [{ art: 'linie', p: ecken(q) }] });
    if (!Array.isArray(e)) return;
    await io.zeichnen([{ art: 'polygon', mitte: m, seiten: io.merken.seiten, radius: Math.hypot(e[0] - m[0], e[1] - m[1]), drehungGrad: Math.atan2(e[1] - m[1], e[0] - m[0]) * 180 / Math.PI }]);
  },

  async punkt(io) {
    for (;;) {
      const p = await io.punkt('Punkt – Ort (Eingabetaste beendet)');
      if (!Array.isArray(p)) return;
      await io.zeichnen([{ art: 'punkt', ort: p }]);
    }
  },

  async absatztext(io) {
    const ort = await io.punkt('Absatztext – Einfügepunkt (oben links)');
    if (!Array.isArray(ort)) return;
    const h = await io.zahl('Texthöhe', { vorgabe: io.merken.texthoehe });
    if (h == null || !(h > 0)) return;
    io.merken.texthoehe = h;
    const t = await io.text('Text (\\n trennt Zeilen)');
    if (t) await io.zeichnen([{ art: 'absatztext', ort, text: t.replace(/\\n/g, '\n'), hoehe: h }]);
  },

  async schraffur(io) {
    const k = await io.auswahl('Schraffur – geschlossene Polylinien oder Kreise wählen (Eingabetaste bestätigt)');
    if (!k.length) return;
    const m = ((await io.text('Muster [voll/linien/kreuz] <linien>')) || 'linien').toLowerCase();
    const o = { art: 'schraffur', objekte: k, muster: m.startsWith('v') ? 'voll' : m.startsWith('k') ? 'kreuz' : 'linien' };
    if (o.muster !== 'voll') {
      o.abstand = await io.zahl('Abstand der Linien', { vorgabe: io.merken.schraffurAbstand });
      if (o.abstand == null || !(o.abstand > 0)) return;
      io.merken.schraffurAbstand = o.abstand;
      o.winkelGrad = (await io.zahl('Winkel in Grad', { vorgabe: 45 })) ?? 45;
    }
    await io.zeichnen([o], { auswahlBehalten: false });
  },

  async bemassung(io) { await mass(io, null); },
  async bemausg(io) { await mass(io, 'ausgerichtet'); },

  // ---------- Stufe 2 Teil 2: ändern mit Geometrie ----------

  async spiegeln(io) {
    const k = await io.auswahl('Spiegeln – Objekte wählen (Eingabetaste bestätigt)');
    if (!k.length) return;
    const a = await io.punkt('Erster Punkt der Spiegelachse');
    if (!Array.isArray(a)) return;
    const b = await io.punkt('Zweiter Punkt der Spiegelachse', { von: a, vorschau: m => [{ art: 'linie', p: [a, m] }] });
    if (!Array.isArray(b)) return;
    const loeschen = /^j/i.test((await io.text('Quellobjekte löschen? [J/N] <N>')) || 'n');
    await io.aendern(k, 'spiegeln', { achse: [a, b], kopie: !loeschen });
  },

  async versetzen(io) {
    const d = await io.zahl('Versetzen – Abstand', { vorgabe: io.merken.versatz });
    if (d == null || !(d > 0)) return;
    io.merken.versatz = d;
    for (;;) {
      const o = await io.objekt('Objekt wählen (Eingabetaste beendet)');
      if (!o) return;
      const s = await io.punkt('Seite anklicken', { ohneOrtho: true });
      if (!Array.isArray(s)) return;
      await io.versuchen(() => io.aendern([o.kennung], 'versetzen', { abstand: d, seite: s }));
    }
  },

  async stutzen(io) { await jeObjekt(io, 'stutzen', 'Stutzen – Teil anklicken, der weg soll (Eingabetaste beendet)'); },
  async dehnen(io) { await jeObjekt(io, 'dehnen', 'Dehnen – Objekt nahe dem Ende anklicken, das verlängert wird (Eingabetaste beendet)'); },

  async abrunden(io) {
    const r = await io.zahl('Abrunden – Radius (0 = scharfe Ecke)', { vorgabe: io.merken.rundung ?? 0 });
    if (r == null || r < 0) return;
    io.merken.rundung = r;
    const a = await io.objekt('Erste Linie (auf dem Teil, der bleibt)');
    if (!a) return;
    const b = await io.objekt('Zweite Linie (auf dem Teil, der bleibt)');
    if (!b) return;
    await io.aendern([a.kennung, b.kennung], 'abrunden', { radius: r, punkte: [a.punkt, b.punkt] });
  },

  async fasen(io) {
    const d1 = await io.zahl('Fasen – Abstand auf der ersten Linie', { vorgabe: io.merken.fase1 });
    if (d1 == null || d1 < 0) return;
    const d2 = await io.zahl('Abstand auf der zweiten Linie', { vorgabe: io.merken.fase2 ?? d1 });
    if (d2 == null || d2 < 0) return;
    io.merken.fase1 = d1; io.merken.fase2 = d2;
    const a = await io.objekt('Erste Linie (auf dem Teil, der bleibt)');
    if (!a) return;
    const b = await io.objekt('Zweite Linie (auf dem Teil, der bleibt)');
    if (!b) return;
    await io.aendern([a.kennung, b.kennung], 'fasen', { abstand1: d1, abstand2: d2, punkte: [a.punkt, b.punkt] });
  },

  async ursprung(io) {
    const k = await io.auswahl('Ursprung – Polylinien oder Blöcke wählen (Eingabetaste bestätigt)');
    if (k.length) await io.aendern(k, 'ursprung', {});
  },

  async verbinden(io) {
    const k = await io.auswahl('Verbinden – Linien, Bögen und offene Polylinien wählen (Eingabetaste bestätigt)');
    if (k.length) await io.aendern(k, 'verbinden', {});
  },

  async reihe(io) {
    const k = await io.auswahl('Reihe – Objekte wählen (Eingabetaste bestätigt)');
    if (!k.length) return;
    const zeilen = Math.round((await io.zahl('Anzahl Zeilen', { vorgabe: 1 })) ?? 1), spalten = Math.round((await io.zahl('Anzahl Spalten', { vorgabe: 2 })) ?? 2);
    const dy = zeilen > 1 ? await io.zahl('Zeilenabstand', {}) : 0, dx = spalten > 1 ? await io.zahl('Spaltenabstand', {}) : 0;
    if (dy == null || dx == null) return;
    await io.aendern(k, 'reihe', { zeilen, spalten, dx, dy });
  },

  async uebertragen(io) {
    const q = await io.objekt('Eigenschaften übertragen – Quellobjekt anklicken');
    if (!q) return;
    io.auswahlLeeren();
    const k = await io.auswahl('Zielobjekte wählen (Eingabetaste bestätigt)');
    if (k.length) await io.aendern(k, 'uebertragen', { quelle: q.kennung });
  },

  async layer(io) {
    const name = await io.text('Layer – Name (neu oder vorhanden; wird der aktuelle Layer)');
    if (name) await io.layerAnlegen(name);
  },
};
