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
  sp: 'speichern', speichern: 'speichern', save: 'speichern', qsave: 'speichern',
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

  async layer(io) {
    const name = await io.text('Layer – Name (neu oder vorhanden; wird der aktuelle Layer)');
    if (name) await io.layerAnlegen(name);
  },
};
