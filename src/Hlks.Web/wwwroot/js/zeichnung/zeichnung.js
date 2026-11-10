// 2D-CAD «Zeichnung» (zeichnung.html, Stufe 1): Zeichenfläche für DWG/DXF. Der Server liefert die Darstellung eines Layouts
// (/api/zeichnung/grafik, Format in ZeichnungEndpunkte.cs); hier wird nur gezeichnet und bedient:
//   Mausrad zoomt am Zeiger · Mausrad gedrückt bzw. linke Taste ziehen verschiebt · Doppelklick Mausrad / Pos1: Grenzen
//   Auswahl: Klick → Eigenschaften · Messen: Punkte anklicken (mit Objektfang), Eingabetaste schliesst ab, Esc verwirft
// Koordinaten werden beim Laden um einen Ursprung verschoben (Landeskoordinaten sind zu gross für die Leinwand).

import { holen, senden } from '../api.js';
import { fangpunkte, Fangraster, FANGNAMEN, messwerte, laengeText, flaecheText } from './logik.js';

const $ = id => document.getElementById(id);
const leinwand = $('z-leinwand'), flaeche = $('z-flaeche'), ctx = leinwand.getContext('2d');

const z = {
  info: null,            // Überblick der geöffneten Zeichnung
  layout: 'Model',
  grafik: null,          // { teile: [{ fenster, stuecke, texte }], stile, ursprung, … }
  fang: null,            // Fangraster
  ansicht: { cx: 0, cy: 0, zoom: 1 },   // Mitte in verschobenen Koordinaten, Bildpunkte je Einheit
  werkzeug: 'auswahl',
  messpunkte: [], messFertig: false,
  maus: null, fangpunkt: null,
  gewaehlt: null,
  layer: [],
};

// ---------- Meldungen ----------

let meldungZeit = 0;
function melden(text, fehler = false) {
  const m = $('z-meldung');
  m.textContent = text; m.hidden = !text; m.classList.toggle('fehler', fehler);
  clearTimeout(meldungZeit);
  if (text && !fehler) meldungZeit = setTimeout(() => { m.hidden = true; }, 2500);
}
const fehlerText = e => (e?.vorschlag ? `${e.message} – ${e.vorschlag}` : e?.message || String(e));

// ---------- Laden ----------

const ZULETZT = 'hlks.zeichnung.zuletzt';
function zuletzt() { try { return JSON.parse(localStorage.getItem(ZULETZT)) || []; } catch { return []; } }
function merken(pfad) {
  const l = [pfad, ...zuletzt().filter(p => p.toLowerCase() !== pfad.toLowerCase())].slice(0, 8);
  try { localStorage.setItem(ZULETZT, JSON.stringify(l)); } catch { /* ohne Speicher keine Liste */ }
}

async function oeffnen(datei) {
  melden('Öffne ' + datei.split(/[\\/]/).pop() + ' …');
  try {
    z.info = await senden('/api/zeichnung/oeffnen', { datei });
  } catch (e) { melden(fehlerText(e), true); return; }
  merken(z.info.pfad);
  z.layout = 'Model';
  $('z-name').textContent = z.info.zeichnung;
  $('z-name').title = z.info.pfad;
  document.title = z.info.zeichnung + ' – KICAD Zeichnung';
  $('z-leer').hidden = true;
  layoutsZeigen();
  await Promise.all([grafikLaden(true), layerLaden()]);
  melden('');
}

async function grafikLaden(grenzen) {
  let g;
  try { g = await holen(`/api/zeichnung/grafik?zeichnung=${encodeURIComponent(z.info.pfad)}&layout=${encodeURIComponent(z.layout)}`); }
  catch (e) { melden(fehlerText(e), true); return; }
  aufbereiten(g);
  z.gewaehlt = null; eigenschaftenZeigen();
  messenLeeren();
  if (grenzen) zoomGrenzen(); else zeichnen();
  hinweiseZeigen();
  $('z-einheit').textContent = g.einheit;
}

/** Darstellung für die Leinwand vorbereiten: Ursprung abziehen, Linien gleichen Stils zu einem Pfad zusammenfassen, Fangpunkte sammeln. */
function aufbereiten(g) {
  const u = g.umriss || g.papier || [0, 0, 1, 1];
  const ox = (u[0] + u[2]) / 2, oy = (u[1] + u[3]) / 2;
  const fangListen = [];
  const teile = [];
  for (const teil of g.teile) {
    const stuecke = [];
    let lauf = null;
    const strich = stil => { if (!lauf || lauf.stil !== stil) { lauf = { art: 's', stil, pfad: new Path2D() }; stuecke.push(lauf); } return lauf.pfad; };
    for (const p of teil.p) {
      switch (p[0]) {
        case 'l': {
          const k = p[2];
          for (let i = 0; i < k.length; i += 2) { k[i] -= ox; k[i + 1] -= oy; }
          const pfad = strich(p[1]);
          pfad.moveTo(k[0], k[1]);
          for (let i = 2; i < k.length; i += 2) pfad.lineTo(k[i], k[i + 1]);
          break;
        }
        case 'k': {
          p[2] -= ox; p[3] -= oy;
          const pfad = strich(p[1]);
          pfad.moveTo(p[2] + p[4], p[3]); pfad.arc(p[2], p[3], p[4], 0, 2 * Math.PI);
          break;
        }
        case 'b': {
          p[2] -= ox; p[3] -= oy;
          const a = p[5] * Math.PI / 180; let e = p[6] * Math.PI / 180;
          if (e <= a) e += 2 * Math.PI;
          const pfad = strich(p[1]);
          pfad.moveTo(p[2] + p[4] * Math.cos(a), p[3] + p[4] * Math.sin(a)); pfad.arc(p[2], p[3], p[4], a, e);
          break;
        }
        case 'g': {
          const pfad = new Path2D();
          for (const ring of p[2]) {
            for (let i = 0; i < ring.length; i += 2) { ring[i] -= ox; ring[i + 1] -= oy; }
            pfad.moveTo(ring[0], ring[1]);
            for (let i = 2; i < ring.length; i += 2) pfad.lineTo(ring[i], ring[i + 1]);
            pfad.closePath();
          }
          stuecke.push({ art: 'f', stil: p[1], pfad }); lauf = null;
          break;
        }
        case 't':
          p[2] -= ox; p[3] -= oy;
          stuecke.push({ art: 't', stil: p[1], x: p[2], y: p[3], h: p[4], winkel: p[5], ausr: p[6], text: p[7] }); lauf = null;
          break;
      }
    }
    const fenster = teil.fenster ? [teil.fenster[0] - ox, teil.fenster[1] - oy, teil.fenster[2] - ox, teil.fenster[3] - oy] : null;
    fangListen.push(fangpunkte(teil.p, fenster));
    teile.push({ fenster, stuecke });
  }
  const verschoben = b => (b ? [b[0] - ox, b[1] - oy, b[2] - ox, b[3] - oy] : null);
  z.grafik = { ...g, teile, ursprung: [ox, oy], umriss: verschoben(g.umriss), ausdehnung: verschoben(g.ausdehnung), papier: verschoben(g.papier) };
  // ein Raster über alle Fangpunkte
  let n = 0; for (const l of fangListen) n += l.length;
  const alle = new Float64Array(n); let o = 0;
  for (const l of fangListen) { alle.set(l, o); o += l.length; }
  const b = z.grafik.umriss || [0, 0, 1, 1];
  z.fang = new Fangraster(alle, Math.max(b[2] - b[0], b[3] - b[1]) / 400);
}

// ---------- Ansicht ----------

function groesse() {
  const r = flaeche.getBoundingClientRect(), dpr = window.devicePixelRatio || 1;
  const w = Math.max(1, Math.round(r.width * dpr)), h = Math.max(1, Math.round(r.height * dpr));
  if (leinwand.width !== w || leinwand.height !== h) { leinwand.width = w; leinwand.height = h; }
  return { w: r.width, h: r.height, dpr };
}
const zuBild = (x, y, g) => [(x - z.ansicht.cx) * z.ansicht.zoom + g.w / 2, g.h / 2 - (y - z.ansicht.cy) * z.ansicht.zoom];
const zuWelt = (px, py, g) => [(px - g.w / 2) / z.ansicht.zoom + z.ansicht.cx, (g.h / 2 - py) / z.ansicht.zoom + z.ansicht.cy];

function zoomGrenzen() {
  if (!z.grafik) return;
  const b = z.grafik.papier || z.grafik.umriss || z.grafik.ausdehnung;
  if (!b) { zeichnen(); return; }
  const g = groesse();
  // Fenster noch ohne Grösse (im Hintergrund geladen): nachholen, sobald es eine hat
  z.grenzenOffen = g.w < 10 || g.h < 10;
  if (z.grenzenOffen) return;
  z.ansicht.cx = (b[0] + b[2]) / 2; z.ansicht.cy = (b[1] + b[3]) / 2;
  z.ansicht.zoom = 0.94 * Math.min(g.w / Math.max(b[2] - b[0], 1e-9), g.h / Math.max(b[3] - b[1], 1e-9));
  zeichnen();
}

// ---------- Zeichnen ----------

const STRICHE = { strich: [8, 4], strichpunkt: [12, 3, 2, 3], punkt: [2, 3], achse: [12, 3, 2, 3], strichfein: [4, 3] };
let warten = false;
function zeichnen() { if (!warten) { warten = true; requestAnimationFrame(() => { warten = false; malen(); }); } }

function malen() {
  const g = groesse(), a = z.ansicht;
  const hell = $('z-hell').checked || !!z.grafik?.papier;   // ein Blatt ist immer weiss
  flaeche.classList.toggle('hell', hell);
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.clearRect(0, 0, leinwand.width, leinwand.height);
  if (!z.grafik) return;
  const grund = z.grafik.papier ? '#ffffff' : hell ? '#ffffff' : '#212830';
  const planfarbe = hell ? '#000000' : '#ffffff';
  const welt = () => ctx.setTransform(a.zoom * g.dpr, 0, 0, -a.zoom * g.dpr, (g.w / 2 - a.cx * a.zoom) * g.dpr, (g.h / 2 + a.cy * a.zoom) * g.dpr);
  const bild = () => ctx.setTransform(g.dpr, 0, 0, g.dpr, 0, 0);

  welt();
  if (z.grafik.papier) {
    const p = z.grafik.papier;
    ctx.fillStyle = '#ffffff'; ctx.fillRect(p[0], p[1], p[2] - p[0], p[3] - p[1]);
  } else if (hell) {
    bild(); ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, g.w, g.h); welt();
  }
  const staerken = $('z-staerken').checked;
  const stile = z.grafik.stile;
  for (const teil of z.grafik.teile) {
    ctx.save();
    if (teil.fenster) { const f = teil.fenster; ctx.beginPath(); ctx.rect(f[0], f[1], f[2] - f[0], f[3] - f[1]); ctx.clip(); }
    for (const s of teil.stuecke) {
      const st = stile[s.stil] || {};
      if (s.art === 's') {
        if (st.s == null) continue;
        ctx.strokeStyle = st.f || planfarbe;
        ctx.lineWidth = (staerken ? Math.max(1, st.s / 0.25) : 1) / a.zoom;
        ctx.setLineDash(st.l && STRICHE[st.l] ? STRICHE[st.l].map(v => v / a.zoom) : []);
        ctx.stroke(s.pfad);
      } else if (s.art === 'f') {
        ctx.fillStyle = st.fl === '#ffffff' ? grund : st.fl || planfarbe;   // weisse Füllung deckt ab: Farbe des Hintergrunds
        ctx.fill(s.pfad, 'evenodd');
      } else {
        const px = s.h * a.zoom;
        if (px < 2.5) continue;   // unlesbar klein
        ctx.save();
        ctx.translate(s.x, s.y); ctx.rotate(s.winkel * Math.PI / 180); ctx.scale(1 / a.zoom, -1 / a.zoom);
        ctx.font = `${px}px Arial, sans-serif`;
        ctx.textAlign = s.ausr === 1 ? 'center' : s.ausr === 2 ? 'right' : 'left';
        ctx.textBaseline = 'alphabetic';
        ctx.fillStyle = st.f || planfarbe;
        ctx.fillText(s.text, 0, 0);
        ctx.restore();
      }
    }
    ctx.restore();
  }
  ctx.setLineDash([]);

  // Auswahl
  bild();
  if (z.gewaehlt) markieren(z.gewaehlt, g);
  // Messen
  const mp = z.messpunkte.map(p => zuBild(p[0], p[1], g));
  const zeiger = z.werkzeug === 'messen' && !z.messFertig && z.maus ? (z.fangpunkt ? zuBild(z.fangpunkt.x, z.fangpunkt.y, g) : [z.maus.px, z.maus.py]) : null;
  if (mp.length) {
    ctx.strokeStyle = '#ff8c1a'; ctx.fillStyle = 'rgba(255, 140, 26, .16)'; ctx.lineWidth = 1.6;
    ctx.beginPath(); mp.forEach((p, i) => (i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])));
    if (zeiger) ctx.lineTo(zeiger[0], zeiger[1]);
    if (mp.length + (zeiger ? 1 : 0) >= 3) { ctx.save(); ctx.closePath(); ctx.fill(); ctx.restore(); }
    ctx.stroke();
    ctx.fillStyle = '#ff8c1a';
    for (const p of mp) ctx.fillRect(p[0] - 2.5, p[1] - 2.5, 5, 5);
  }
  // Fangzeichen
  if (z.fangpunkt && z.werkzeug === 'messen') {
    const [x, y] = zuBild(z.fangpunkt.x, z.fangpunkt.y, g);
    ctx.strokeStyle = '#2ee66b'; ctx.lineWidth = 1.8; ctx.beginPath();
    if (z.fangpunkt.art === 0) ctx.rect(x - 6, y - 6, 12, 12);
    else if (z.fangpunkt.art === 1) { ctx.moveTo(x, y - 7); ctx.lineTo(x + 7, y + 6); ctx.lineTo(x - 7, y + 6); ctx.closePath(); }
    else ctx.arc(x, y, 7, 0, 2 * Math.PI);
    ctx.stroke();
  }
}

function markieren(o, g) {
  const [ox, oy] = z.grafik.ursprung;
  const b = (x, y) => zuBild(x - ox, y - oy, g);
  ctx.strokeStyle = '#1e90ff'; ctx.lineWidth = 2.2; ctx.setLineDash([]);
  ctx.beginPath();
  if (o.von && o.bis) { const p = b(...o.von), q = b(...o.bis); ctx.moveTo(p[0], p[1]); ctx.lineTo(q[0], q[1]); }
  else if (o.punkte?.length && o.punkte.length === o.anzahlPunkte && !o.punkte.some(p => p.length > 2)) {
    o.punkte.forEach((p, i) => { const q = b(p[0], p[1]); if (i) ctx.lineTo(q[0], q[1]); else ctx.moveTo(q[0], q[1]); });
    if (o.geschlossen) ctx.closePath();
  } else if (o.art === 'kreis') { const m = b(...o.mitte); ctx.arc(m[0], m[1], o.radius * z.ansicht.zoom, 0, 2 * Math.PI); }
  else if (o.box) {
    const p = b(o.box[0], o.box[1]), q = b(o.box[2], o.box[3]);
    const w = Math.max(q[0] - p[0], 10), h = Math.max(p[1] - q[1], 10);
    ctx.setLineDash([5, 3]); ctx.rect(p[0] - (w - (q[0] - p[0])) / 2, q[1] - (h - (p[1] - q[1])) / 2, w, h);
  }
  ctx.stroke(); ctx.setLineDash([]);
}

// ---------- Maus ----------

let zug = null;   // { px, py, cx, cy, bewegt, taste }
function mausOrt(e) { const r = leinwand.getBoundingClientRect(); return { px: e.clientX - r.left, py: e.clientY - r.top }; }

leinwand.addEventListener('wheel', e => {
  e.preventDefault();
  if (!z.grafik) return;
  const g = groesse(), m = mausOrt(e);
  const [wx, wy] = zuWelt(m.px, m.py, g);
  const f = Math.exp(-e.deltaY * (e.ctrlKey ? 0.01 : 0.0015));
  z.ansicht.zoom = Math.min(Math.max(z.ansicht.zoom * f, 1e-9), 1e9);
  // der Punkt unter dem Zeiger bleibt stehen
  z.ansicht.cx = wx - (m.px - g.w / 2) / z.ansicht.zoom;
  z.ansicht.cy = wy - (g.h / 2 - m.py) / z.ansicht.zoom;
  zeichnen();
}, { passive: false });

leinwand.addEventListener('pointerdown', e => {
  if (!z.grafik || (e.button !== 0 && e.button !== 1)) return;
  e.preventDefault();
  leinwand.setPointerCapture(e.pointerId);
  const m = mausOrt(e);
  zug = { ...m, cx: z.ansicht.cx, cy: z.ansicht.cy, bewegt: false, taste: e.button };
});

leinwand.addEventListener('pointermove', e => {
  const m = mausOrt(e);
  z.maus = m;
  if (zug) {
    if (!zug.bewegt && Math.hypot(m.px - zug.px, m.py - zug.py) > 4) zug.bewegt = true;
    if (zug.bewegt) {
      z.ansicht.cx = zug.cx - (m.px - zug.px) / z.ansicht.zoom;
      z.ansicht.cy = zug.cy + (m.py - zug.py) / z.ansicht.zoom;
      leinwand.style.cursor = 'grabbing';
    }
  }
  if (!z.grafik) return;
  const g = groesse();
  const [wx, wy] = zuWelt(m.px, m.py, g);
  z.fangpunkt = $('z-fang').checked && z.fang && !zug?.bewegt ? z.fang.suche(wx, wy, 10 / z.ansicht.zoom) : null;
  const ort = z.fangpunkt && z.werkzeug === 'messen' ? [z.fangpunkt.x, z.fangpunkt.y] : [wx, wy];
  const [ox, oy] = z.grafik.ursprung;
  $('z-koord').textContent = `${(ort[0] + ox).toFixed(3)}, ${(ort[1] + oy).toFixed(3)}`;
  $('z-fangtext').textContent = z.fangpunkt && z.werkzeug === 'messen' ? FANGNAMEN[z.fangpunkt.art] : '';
  if (z.werkzeug === 'messen' && z.messpunkte.length && !z.messFertig) massZeigen([...z.messpunkte, ort]);
  zeichnen();
});

leinwand.addEventListener('pointerup', e => {
  if (!zug) return;
  const war = zug; zug = null;
  leinwand.style.cursor = '';
  if (war.bewegt || war.taste !== 0 || !z.grafik) { zeichnen(); return; }
  const g = groesse();
  const [wx, wy] = zuWelt(war.px, war.py, g);
  if (z.werkzeug === 'messen') {
    if (z.messFertig) { z.messpunkte = []; z.messFertig = false; }
    const p = z.fangpunkt ? [z.fangpunkt.x, z.fangpunkt.y] : [wx, wy];
    z.messpunkte.push(p);
    massZeigen(z.messpunkte);
    zeichnen();
  } else waehlen(wx, wy);
});

leinwand.addEventListener('dblclick', e => { if (z.werkzeug === 'messen') { e.preventDefault(); messenAbschliessen(true); } });
leinwand.addEventListener('auxclick', e => { if (e.button === 1 && e.detail === 2) zoomGrenzen(); });
leinwand.addEventListener('contextmenu', e => { e.preventDefault(); if (z.werkzeug === 'messen') messenAbschliessen(false); });
leinwand.addEventListener('pointerleave', () => { z.maus = null; z.fangpunkt = null; zeichnen(); });

// ---------- Auswahl ----------

async function waehlen(wx, wy) {
  const [ox, oy] = z.grafik.ursprung;
  try {
    const r = await holen(`/api/zeichnung/treffer?zeichnung=${encodeURIComponent(z.info.pfad)}&layout=${encodeURIComponent(z.layout)}&x=${wx + ox}&y=${wy + oy}&r=${7 / z.ansicht.zoom}`);
    z.gewaehlt = r.objekt;
  } catch (e) { melden(fehlerText(e), true); return; }
  eigenschaftenZeigen();
  zeichnen();
}

const NAMEN = { kennung: 'Kennung', art: 'Art', layer: 'Layer', block: 'Block', text: 'Text', laenge: 'Länge', flaeche: 'Fläche', radius: 'Radius', hoehe: 'Höhe',
  masszahl: 'Masszahl', muster: 'Muster', drehungGrad: 'Drehung', geschlossen: 'Geschlossen', anzahlPunkte: 'Punkte', von: 'Von', bis: 'Bis', mitte: 'Mitte', ort: 'Ort', massstab: 'Massstab' };

function tabelle(zeilen) {
  const t = document.createElement('table');
  for (const [k, v] of zeilen) {
    const tr = t.insertRow();
    tr.insertCell().textContent = k;
    tr.insertCell().textContent = v;
  }
  return t;
}

function eigenschaftenZeigen() {
  const ziel = $('z-eigenschaften');
  ziel.replaceChildren();
  const o = z.gewaehlt;
  if (!o) { const p = document.createElement('p'); p.className = 'z-leise'; p.textContent = z.grafik ? 'Objekt anklicken.' : ''; ziel.append(p); return; }
  const bek = z.layout !== 'Model' || !/unbekannt/.test(z.grafik.einheit), mm = z.grafik.mmJeEinheit;
  const zeilen = [];
  for (const k of Object.keys(NAMEN)) {
    if (o[k] == null) continue;
    let v = o[k];
    if (k === 'laenge' || k === 'radius' || k === 'hoehe' || k === 'masszahl') v = laengeText(v, mm, bek);
    else if (k === 'flaeche') v = flaecheText(v, mm, bek);
    else if (k === 'drehungGrad') v = v + '°';
    else if (k === 'geschlossen') v = v ? 'ja' : 'nein';
    else if (Array.isArray(v)) v = v.join(', ');
    zeilen.push([NAMEN[k], String(v)]);
  }
  for (const [k, v] of Object.entries(o.attribute || {})) zeilen.push(['Attribut ' + k, v]);
  ziel.append(tabelle(zeilen));
}

// ---------- Messen ----------

function massZeigen(punkte) {
  const ziel = $('z-mass');
  ziel.replaceChildren();
  const w = messwerte(punkte);
  if (!w) { const p = document.createElement('p'); p.className = 'z-leise'; p.textContent = 'Nächsten Punkt anklicken.'; ziel.append(p); return; }
  const bek = z.layout !== 'Model' || !/unbekannt/.test(z.grafik.einheit), mm = z.grafik.mmJeEinheit;
  const L = v => laengeText(v, mm, bek);
  const zeilen = [['Strecke', L(w.strecke)], ['ΔX', L(w.dx)], ['ΔY', L(w.dy)], ['Richtung', w.richtung.toFixed(2) + '°']];
  if (punkte.length > 2) zeilen.push(['Winkel', w.winkel.toFixed(2) + '°'], ['Linienzug', L(w.laenge)], ['Umfang', L(w.umfang)], ['Fläche', flaecheText(w.flaeche, mm, bek)]);
  ziel.append(tabelle(zeilen));
  const p = document.createElement('p'); p.className = 'z-leise';
  p.textContent = z.messFertig ? 'Abgeschlossen. Klick beginnt eine neue Messung.' : 'Eingabetaste oder Doppelklick schliesst ab, Esc verwirft.';
  ziel.append(p);
}

function messenLeeren() {
  z.messpunkte = []; z.messFertig = false;
  const p = document.createElement('p'); p.className = 'z-leise'; p.textContent = 'Werkzeug «Messen» wählen und Punkte anklicken.';
  $('z-mass').replaceChildren(p);
}

/** doppelklick: der zweite Klick hat denselben Punkt noch einmal gesetzt – weg damit. */
function messenAbschliessen(doppelklick) {
  if (z.messFertig) return;
  if (doppelklick && z.messpunkte.length > 1) z.messpunkte.pop();
  if (z.messpunkte.length < 2) { messenLeeren(); zeichnen(); return; }
  z.messFertig = true;
  massZeigen(z.messpunkte);
  zeichnen();
}

function werkzeug(name) {
  z.werkzeug = name;
  $('z-auswahl').classList.toggle('aktiv', name === 'auswahl');
  $('z-messen').classList.toggle('aktiv', name === 'messen');
  if (name !== 'messen') messenLeeren();
  zeichnen();
}

// ---------- Layer ----------

async function layerLaden() {
  try { z.layer = (await holen(`/api/zeichnung/layer?zeichnung=${encodeURIComponent(z.info.pfad)}`)).layer; }
  catch (e) { melden(fehlerText(e), true); return; }
  layerZeigen();
}

// AutoCAD-Farbindex → Anzeige (nur die Grundfarben; übrige als Grau)
const ACI = { 1: '#ff0000', 2: '#ffff00', 3: '#00ff00', 4: '#00ffff', 5: '#0000ff', 6: '#ff00ff', 7: '#ffffff', 8: '#808080', 9: '#c0c0c0' };
const layerFarbe = f => (f?.startsWith('#') ? f : ACI[+String(f).replace('ACI ', '')] || '#9aa0a6');

function layerSichtbar() {
  const f = $('z-layer-filter').value.trim().toLowerCase();
  return z.layer.filter(l => !f || l.name.toLowerCase().includes(f));
}

function layerZeigen() {
  const ziel = $('z-layer');
  ziel.replaceChildren();
  const liste = layerSichtbar();
  $('z-layer-anzahl').textContent = `${z.layer.filter(l => l.ein && !l.gefroren).length} von ${z.layer.length} ein`;
  for (const l of liste) {
    const zeile = document.createElement('label');
    if (l.gefroren) { zeile.className = 'z-gefroren'; zeile.title = 'In der Datei gefroren'; }
    const haken = document.createElement('input'); haken.type = 'checkbox'; haken.checked = l.ein; haken.disabled = !!l.gefroren;
    haken.addEventListener('change', () => layerSetzen([l.name], haken.checked));
    const farbe = document.createElement('span'); farbe.className = 'z-farbe'; farbe.style.background = layerFarbe(l.farbe);
    const name = document.createElement('span'); name.className = 'z-lname'; name.textContent = l.name; name.title = l.name;
    const zahl = document.createElement('span'); zahl.className = 'z-leise'; zahl.textContent = l.objekte || '';
    zeile.append(haken, farbe, name, zahl);
    ziel.append(zeile);
  }
}

async function layerSetzen(namen, ein) {
  if (!namen.length) return;
  try { await senden('/api/zeichnung/layer', { zeichnung: z.info.pfad, layer: namen, ein }); }
  catch (e) { melden(fehlerText(e), true); }
  for (const l of z.layer) if (namen.includes(l.name)) l.ein = ein;
  layerZeigen();
  await grafikLaden(false);
}

// ---------- Layouts, Hinweise ----------

function layoutsZeigen() {
  const ziel = $('z-layouts');
  ziel.replaceChildren();
  for (const l of z.info.layouts) {
    const k = document.createElement('button');
    k.textContent = l.name === 'Model' ? 'Modell' : l.name;
    k.classList.toggle('aktiv', l.name === z.layout);
    k.addEventListener('click', async () => { z.layout = l.name; layoutsZeigen(); await grafikLaden(true); });
    ziel.append(k);
  }
}

function hinweiseZeigen() {
  const ziel = $('z-hinweise');
  ziel.replaceChildren();
  const zeilen = Object.entries(z.grafik.nichtDargestellt || {}).map(([k, n]) => `${n} × ${k}`);
  const p = document.createElement('p');
  p.textContent = zeilen.length ? 'Nicht dargestellt: ' + zeilen.join(', ') + '.' : 'Alles dargestellt.';
  const q = document.createElement('p');
  q.textContent = `Fassung ${z.info.fassung}. Schriften als Arial, Linienarten vereinfacht.`;
  ziel.append(p, q);
}

// ---------- Öffnen-Dialog ----------

const dialog = $('z-dialog');
async function ordnerZeigen(ordner) {
  $('z-d-fehler').textContent = '';
  let d;
  try { d = await holen('/api/zeichnung/dateien' + (ordner ? '?ordner=' + encodeURIComponent(ordner) : '')); }
  catch (e) { $('z-d-fehler').textContent = fehlerText(e); return; }
  const liste = $('z-d-liste');
  liste.replaceChildren();
  const zeile = (text, rechts, klasse, klick) => {
    const div = document.createElement('div'); if (klasse) div.className = klasse;
    const n = document.createElement('span'); n.className = 'z-dname'; n.textContent = text;
    const r = document.createElement('span'); r.className = 'z-leise'; r.textContent = rechts || '';
    div.append(n, r); div.addEventListener('click', klick); liste.append(div);
  };
  if (d.laufwerke) {
    $('z-d-pfad').value = ''; dialog.dataset.eltern = '';
    for (const l of d.laufwerke) zeile(l.name, 'Laufwerk', 'z-ordner', () => ordnerZeigen(l.pfad));
    return;
  }
  $('z-d-pfad').value = d.ordner; dialog.dataset.eltern = d.eltern || '';
  try { localStorage.setItem('hlks.zeichnung.ordner', d.ordner); } catch { /* egal */ }
  for (const o of d.ordnerListe) zeile(o.name, '', 'z-ordner', () => ordnerZeigen(o.pfad));
  for (const f of d.dateien) zeile(f.name, `${f.kB} kB · ${f.geaendert}`, '', () => { dialog.close(); oeffnen(f.pfad); });
  if (!d.ordnerListe.length && !d.dateien.length) zeile('Keine Unterordner und keine DWG/DXF in diesem Ordner.', '', 'z-leise', () => {});
}

function dialogOeffnen() {
  let start = '';
  try { start = localStorage.getItem('hlks.zeichnung.ordner') || ''; } catch { /* egal */ }
  dialog.showModal();
  ordnerZeigen(start).then(() => { if ($('z-d-fehler').textContent) ordnerZeigen(''); });
}

function pfadGehen() {
  const p = $('z-d-pfad').value.trim().replace(/^"|"$/g, '');
  if (/\.(dwg|dxf)$/i.test(p)) { dialog.close(); oeffnen(p); } else ordnerZeigen(p);
}

function zuletztZeigen() {
  const ziel = $('z-zuletzt');
  ziel.replaceChildren();
  for (const p of zuletzt()) {
    const a = document.createElement('a'); a.textContent = p; a.title = p;
    a.addEventListener('click', () => oeffnen(p));
    ziel.append(a);
  }
}

// ---------- PDF ----------

function pdfOeffnen() {
  if (!z.grafik) return;
  const blatt = z.layout !== 'Model';
  $('z-p-hinweis').textContent = blatt ? 'Ein Layout wird ohne weitere Angabe 1:1 auf seinem Blatt ausgegeben.'
    : /unbekannt/.test(z.grafik.einheit) ? 'Die Datei nennt keine Einheit: nur «eingepasst» ist möglich.' : '';
  $('z-pdf-dialog').showModal();
}

function pdfLos() {
  const q = new URLSearchParams({ zeichnung: z.info.pfad, layout: z.layout, farbig: $('z-p-farbig').value });
  if ($('z-p-massstab').value) q.set('massstab', $('z-p-massstab').value);
  if ($('z-p-blatt').value) q.set('blatt', $('z-p-blatt').value);
  if ($('z-p-bereich').value === 'ansicht') {
    const g = groesse(), [ox, oy] = z.grafik.ursprung;
    const a = zuWelt(0, g.h, g), b = zuWelt(g.w, 0, g);
    q.set('fenster', [a[0] + ox, a[1] + oy, b[0] + ox, b[1] + oy].map(v => v.toFixed(4)).join(','));
  }
  $('z-pdf-dialog').close();
  pdfHolen('/api/zeichnung/pdf?' + q);
}

async function pdfHolen(url) {
  melden('PDF wird erzeugt …');
  try {
    const r = await fetch(url, { cache: 'no-store' });
    if (!r.ok) { const f = await r.json().catch(() => ({})); throw Object.assign(new Error(f.fehler || 'PDF fehlgeschlagen'), { vorschlag: f.vorschlag }); }
    const blob = await r.blob();
    window.open(URL.createObjectURL(blob), '_blank');
    melden('');
  } catch (e) { melden(fehlerText(e), true); }
}

// ---------- Bedienung ----------

$('z-oeffnen').addEventListener('click', dialogOeffnen);
$('z-oeffnen2').addEventListener('click', dialogOeffnen);
$('z-d-hoch').addEventListener('click', () => ordnerZeigen(dialog.dataset.eltern || ''));
$('z-d-los').addEventListener('click', pfadGehen);
$('z-d-pfad').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); pfadGehen(); } });
$('z-auswahl').addEventListener('click', () => werkzeug('auswahl'));
$('z-messen').addEventListener('click', () => werkzeug('messen'));
$('z-grenzen').addEventListener('click', zoomGrenzen);
$('z-pdf').addEventListener('click', pdfOeffnen);
$('z-p-los').addEventListener('click', pdfLos);
for (const id of ['z-fang', 'z-staerken', 'z-hell']) $(id).addEventListener('change', zeichnen);
$('z-layer-filter').addEventListener('input', layerZeigen);
$('z-layer-alle').addEventListener('click', () => layerSetzen(layerSichtbar().filter(l => !l.ein && !l.gefroren).map(l => l.name), true));
$('z-layer-keine').addEventListener('click', () => layerSetzen(layerSichtbar().filter(l => l.ein && !l.gefroren).map(l => l.name), false));

window.addEventListener('keydown', e => {
  if (e.target instanceof HTMLInputElement && e.target.type !== 'checkbox') return;
  if (dialog.open || $('z-pdf-dialog').open) return;
  if (e.key === 'Escape') { if (z.werkzeug === 'messen' && z.messpunkte.length) { messenLeeren(); zeichnen(); } else { werkzeug('auswahl'); z.gewaehlt = null; eigenschaftenZeigen(); zeichnen(); } }
  else if (e.key === 'Enter' && z.werkzeug === 'messen') messenAbschliessen(false);
  else if (e.key === 'Home') zoomGrenzen();
  else if (e.key === 'F3') { e.preventDefault(); $('z-fang').checked = !$('z-fang').checked; zeichnen(); }
  else if ((e.key === 'm' || e.key === 'M') && !e.ctrlKey) werkzeug('messen');
  else if ((e.key === 'o' || e.key === 'O') && e.ctrlKey) { e.preventDefault(); dialogOeffnen(); }
});

new ResizeObserver(() => (z.grenzenOffen ? zoomGrenzen() : zeichnen())).observe(flaeche);
zuletztZeigen();
// Für Prüfungen von aussen (Browserprüfung, spätere Befehlszeile)
window.hlksZeichnung = { zustand: z, oeffnen, zoomGrenzen, werkzeug };
const start = new URLSearchParams(location.search).get('datei');
if (start) oeffnen(start);
