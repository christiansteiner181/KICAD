// Bild der aktiven Ansicht für die KI-Palette «Claude» (Modul 3c, Teil O): aus der Zeichenfläche (SVG der Plan-Ansichten und Schemas, WebGL-Fläche der 3D-Ansicht) ein PNG
// als data-URL, höchstens 4 MB (sonst verkleinern, zuletzt als JPEG). Das Bild geht nur mit der Frage an den Server (Kontext-Chip «Bild der Ansicht»), wird dort nach der
// Frage gelöscht und nie im Browser gespeichert. Die Rechengrössen (Verkleinern, Prüfen) stehen in ki-text.js (bildGroesse, dataUrlBytes) und sind in Node getestet;
// hier steht nur, was einen Browser braucht.

import * as fenster from '../fenster.js';
import { bildGroesse, dataUrlBytes, GRENZEN } from './ki-text.js';

const STIL = ['fill', 'fill-opacity', 'fill-rule', 'stroke', 'stroke-width', 'stroke-opacity', 'stroke-dasharray', 'stroke-dashoffset', 'stroke-linecap', 'stroke-linejoin',
  'opacity', 'display', 'visibility', 'font-family', 'font-size', 'font-weight', 'font-style', 'text-anchor', 'dominant-baseline', 'vector-effect', 'paint-order', 'letter-spacing'];
const SVGNS = 'http://www.w3.org/2000/svg';

/** Die Zeichenfläche der aktiven Ansicht: { art: 'svg' | '3d', el } oder null (Liste, Planblatt, Familieneditor). */
export function quelle() {
  const kachel = fenster.aktive?.()?.kachel;
  if (!kachel) return null;
  const c = kachel.querySelector('canvas.d3-leinwand');
  if (c && c.width > 1) return { art: '3d', el: c };
  const s = kachel.querySelector('svg.gr-svg');
  if (s) return { art: 'svg', el: s };
  return null;
}

export const bildMoeglich = () => quelle() !== null;

function laden(url) {
  return new Promise((ok, fehl) => {
    const img = new Image();
    img.onload = () => ok(img);
    img.onerror = () => fehl(new Error('Das Bild der Ansicht konnte nicht erzeugt werden.'));
    img.src = url;
  });
}

/** SVG mit aufgelösten Stilen (die Ansicht färbt über CSS-Klassen und Variablen, ein eigenständiges SVG kennt sie nicht). */
async function svgAlsBild(svg) {
  const r = svg.getBoundingClientRect();
  const b = Math.max(2, Math.round(r.width)), h = Math.max(2, Math.round(r.height));
  const klon = svg.cloneNode(true);
  const orig = [svg, ...svg.querySelectorAll('*')];
  const kopie = [klon, ...klon.querySelectorAll('*')];
  for (let i = 0; i < orig.length && i < kopie.length; i++) {
    if (!(kopie[i] instanceof SVGElement)) continue;
    const cs = getComputedStyle(orig[i]);
    // Verweise auf Muster (Schraffuren) im selben SVG: aus der absoluten Adresse der Seite wieder «#id» machen
    const stil = STIL.filter((p) => !(i === 0 && p === 'display')).map((p) => `${p}:${cs.getPropertyValue(p).replace(/url\(["']?[^"')#]*#([^"')]+)["']?\)/g, 'url(#$1)')}`).join(';');
    kopie[i].setAttribute('style', stil);
    kopie[i].removeAttribute('class');
  }
  klon.setAttribute('xmlns', SVGNS);
  klon.setAttribute('width', String(b));
  klon.setAttribute('height', String(h));
  klon.removeAttribute('tabindex');
  const grund = getComputedStyle(svg.parentElement ?? svg).backgroundColor;
  const hintergrund = grund && grund !== 'rgba(0, 0, 0, 0)' ? grund : getComputedStyle(document.documentElement).getPropertyValue('--zeichenflaeche').trim() || '#ffffff';
  const xml = new XMLSerializer().serializeToString(klon);
  const url = URL.createObjectURL(new Blob([xml], { type: 'image/svg+xml;charset=utf-8' }));
  try {
    const img = await laden(url);
    return { zeichnen: (ctx, zb, zh) => ctx.drawImage(img, 0, 0, zb, zh), b, h, hintergrund };
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 2000);
  }
}

/** WebGL-Fläche: ein neu gezeichnetes Bild im selben Rahmen abgreifen (ohne preserveDrawingBuffer ist der Puffer sonst leer). */
function dreiDBild(canvas) {
  return new Promise((ok) => {
    const kopie = document.createElement('canvas');
    kopie.width = canvas.width; kopie.height = canvas.height;
    window.dispatchEvent(new Event('resize'));   // die 3D-Ansicht zeichnet im nächsten Rahmen neu (ansicht3d.js › groesseAnpassen → anfordern)
    requestAnimationFrame(() => {
      try { kopie.getContext('2d').drawImage(canvas, 0, 0); } catch { /* leer */ }
      ok({ zeichnen: (ctx, zb, zh) => ctx.drawImage(kopie, 0, 0, zb, zh), b: canvas.width, h: canvas.height, hintergrund: getComputedStyle(document.documentElement).getPropertyValue('--d3-hintergrund').trim() || '#eef1f5' });
    });
  });
}

/** PNG der aktiven Ansicht als data-URL (≤ 4 MB) oder null (keine Ansicht mit Zeichenfläche). Wirft bei einem Fehler des Browsers. */
export async function bildDerAnsicht() {
  const q = quelle();
  if (!q) return null;
  const roh = q.art === 'svg' ? await svgAlsBild(q.el) : await dreiDBild(q.el);
  const versuche = [{ f: 1, typ: 'image/png' }, { f: 0.75, typ: 'image/png' }, { f: 0.5, typ: 'image/png' }, { f: 0.5, typ: 'image/jpeg', q: 0.85 }, { f: 0.35, typ: 'image/jpeg', q: 0.8 }, { f: 0.2, typ: 'image/jpeg', q: 0.75 }];
  for (const v of versuche) {
    const g = bildGroesse(roh.b, roh.h, 1600, v.f);
    const c = document.createElement('canvas');
    c.width = g.b; c.height = g.h;
    const ctx = c.getContext('2d');
    ctx.fillStyle = roh.hintergrund;
    ctx.fillRect(0, 0, g.b, g.h);
    roh.zeichnen(ctx, g.b, g.h);
    const url = c.toDataURL(v.typ, v.q);
    if (url.startsWith('data:image/') && dataUrlBytes(url) <= GRENZEN.bildBytes) return url;
  }
  return null;
}
