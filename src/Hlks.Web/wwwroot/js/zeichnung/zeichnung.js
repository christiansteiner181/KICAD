// 2D-CAD «Zeichnung» (zeichnung.html, Stufen 1 und 2): Zeichenfläche für DWG/DXF. Der Server liefert die Darstellung eines Layouts
// (/api/zeichnung/grafik, Format in ZeichnungEndpunkte.cs) und führt alle Änderungen aus; hier wird gezeichnet und bedient:
//   Mausrad zoomt am Zeiger · Mausrad gedrückt bzw. linke Taste ziehen verschiebt · Doppelklick Mausrad / Pos1: Grenzen
//   Ohne Befehl: Klick wählt Objekte (Klick ins Leere: Fenster – nach rechts «ganz darin», nach links «berührt»), Esc hebt auf
//   Befehle (befehle.js) über Knöpfe oder die Befehlszeile; Punkte per Klick (Objektfang, Ortho) oder getippt
// Koordinaten werden beim Laden um einen festen Ursprung verschoben (Landeskoordinaten sind zu gross für die Leinwand).

import { holen, senden } from '../api.js';
import { fangpunkte, Fangraster, strecken, Streckenraster, fangSuchen, FANGNAMEN, messwerte, laengeText, flaecheText } from './logik.js';
import { symboleSetzen } from './symbole.js';
import { BEFEHLE, KUERZEL, NAMEN as BEFEHLSNAMEN, Abbruch, punktLesen, ortho } from './befehle.js';

const $ = id => document.getElementById(id);
const leinwand = $('z-leinwand'), flaeche = $('z-flaeche'), ctx = leinwand.getContext('2d'), eingabe = $('z-eingabe');

const z = {
  info: null,            // Überblick der geöffneten Zeichnung
  layout: 'Model',
  grafik: null,          // { teile: [{ fenster, stuecke }], stile, ursprung, … }
  ursprung: null,        // fester Ursprung des Layouts (bleibt beim Nachladen gleich)
  fang: null,            // Fangraster
  ansicht: { cx: 0, cy: 0, zoom: 1 },   // Mitte in verschobenen Koordinaten, Bildpunkte je Einheit
  werkzeug: 'auswahl',   // auswahl | messen
  messpunkte: [], messFertig: false,
  maus: null, fangpunkt: null,
  auswahl: new Map(),    // Kennung → Objekt (mit box; nach einem Klick mit allen Eigenschaften)
  fensterStart: null,    // erste Ecke des Auswahlfensters
  befehl: null, laufNr: 0, erwartet: null, letzterBefehl: null,
  merken: { texthoehe: null, radius: null },
  layer: [], stand: {},
};

// ---------- Meldungen ----------

let meldungZeit = 0;
function melden(text, fehler = false) {
  const m = $('z-meldung');
  m.textContent = text; m.hidden = !text; m.classList.toggle('fehler', fehler);
  clearTimeout(meldungZeit);
  if (text) meldungZeit = setTimeout(() => { m.hidden = true; }, fehler ? 7000 : 2500);
}
const fehlerText = e => (e?.vorschlag ? `${e.message} – ${e.vorschlag}` : e?.message || String(e));
const q = () => `zeichnung=${encodeURIComponent(z.info.pfad)}&layout=${encodeURIComponent(z.layout)}`;

// ---------- Laden ----------

const ZULETZT = 'hlks.zeichnung.zuletzt';
function zuletzt() { try { return JSON.parse(localStorage.getItem(ZULETZT)) || []; } catch { return []; } }
function merkenDatei(pfad) {
  const l = [pfad, ...zuletzt().filter(p => p.toLowerCase() !== pfad.toLowerCase())].slice(0, 8);
  try { localStorage.setItem(ZULETZT, JSON.stringify(l)); } catch { /* ohne Speicher keine Liste */ }
}

/** Neue Zeichnung: Pfad erfragen (Vorschlag im zuletzt benutzten Ordner), anlegen und öffnen. */
function neu() {
  let ordner = '';
  try { ordner = localStorage.getItem('hlks.zeichnung.ordner') || ''; } catch { /* egal */ }
  const pfad = prompt('Neue Zeichnung – voller Pfad (.dwg oder .dxf):', (ordner ? ordner.replace(/[\\/]$/, '') + '\\' : '') + 'Zeichnung1.dwg');
  if (pfad) oeffnen(pfad.trim().replace(/^"|"$/g, ''), true);
}

async function oeffnen(datei, anlegen = false) {
  if (z.stand.geaendert && !confirm('Die Zeichnung ist geändert und nicht gespeichert. Trotzdem eine andere öffnen?')) return;
  abbrechen();
  melden((anlegen ? 'Lege an: ' : 'Öffne ') + datei.split(/[\\/]/).pop() + ' …');
  try { z.info = await senden(anlegen ? '/api/zeichnung/neu' : '/api/zeichnung/oeffnen', { datei }); z.stand = {}; z.auswahl.clear(); }
  catch (e) { melden(fehlerText(e), true); return; }
  merkenDatei(z.info.pfad);
  z.layout = 'Model'; z.ursprung = null;
  $('z-leer').hidden = true;
  for (const id of ['z-speichern', 'z-speichern-unter']) $(id).disabled = false;
  layoutsZeigen();
  await Promise.all([grafikLaden(true), layerLaden(), standLaden()]);
  melden('');
  eingabe.focus();
}

async function grafikLaden(grenzen) {
  let g;
  try { g = await holen(`/api/zeichnung/grafik?${q()}`); }
  catch (e) { melden(fehlerText(e), true); return; }
  aufbereiten(g);
  if (grenzen) zoomGrenzen(); else zeichnen();
  hinweiseZeigen();
  $('z-einheit').textContent = g.einheit;
}

/** Darstellung für die Leinwand vorbereiten: Ursprung abziehen, Linien gleichen Stils zu einem Pfad zusammenfassen, Fangpunkte sammeln. */
function aufbereiten(g) {
  if (!z.ursprung) { const u = g.umriss || g.papier || [0, 0, 0, 0]; z.ursprung = [(u[0] + u[2]) / 2, (u[1] + u[3]) / 2]; }
  const [ox, oy] = z.ursprung;
  const fangListen = [], streckenListen = [];
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
    streckenListen.push(strecken(teil.p, fenster));
    teile.push({ fenster, stuecke });
  }
  const verschoben = b => (b ? [b[0] - ox, b[1] - oy, b[2] - ox, b[3] - oy] : null);
  z.grafik = { ...g, teile, ursprung: z.ursprung, umriss: verschoben(g.umriss), ausdehnung: verschoben(g.ausdehnung), papier: verschoben(g.papier) };
  let n = 0; for (const l of fangListen) n += l.length;
  const alle = new Float64Array(n); let o = 0;
  for (const l of fangListen) { alle.set(l, o); o += l.length; }
  const b = z.grafik.umriss || [0, 0, 1000, 1000];
  const zelle = Math.max(b[2] - b[0], b[3] - b[1], 1e-6) / 400;
  z.fang = new Fangraster(alle, zelle);
  let m = 0; for (const l of streckenListen) m += l.length;
  const linien = new Float64Array(m); o = 0;
  for (const l of streckenListen) { linien.set(l, o); o += l.length; }
  z.fangLinien = new Streckenraster(linien, zelle);
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
const echt = p => [p[0] + z.ursprung[0], p[1] + z.ursprung[1]];       // Leinwand → Zeichnung
const sicht = p => [p[0] - z.ursprung[0], p[1] - z.ursprung[1]];      // Zeichnung → Leinwand

function zoomGrenzen() {
  if (!z.grafik) return;
  const g = groesse();
  // Fenster noch ohne Grösse (im Hintergrund geladen): nachholen, sobald es eine hat
  z.grenzenOffen = g.w < 10 || g.h < 10;
  if (z.grenzenOffen) return;
  const b = z.grafik.papier || z.grafik.umriss || z.grafik.ausdehnung;
  if (!b || (b[2] - b[0] <= 0 && b[3] - b[1] <= 0)) {
    // leere Zeichnung (bzw. ein einzelner Punkt): ein Feld von 10 m bei Millimetern, sonst 100 Einheiten
    const feld = (z.grafik.mmJeEinheit || 1) >= 100 ? 100 : 10000;
    z.ansicht = { cx: b ? b[0] : 0, cy: b ? b[1] : 0, zoom: Math.min(g.w, g.h) / feld };
  } else {
    z.ansicht.cx = (b[0] + b[2]) / 2; z.ansicht.cy = (b[1] + b[3]) / 2;
    z.ansicht.zoom = 0.94 * Math.min(g.w / Math.max(b[2] - b[0], 1e-9), g.h / Math.max(b[3] - b[1], 1e-9));
  }
  zeichnen();
}

// ---------- Zeichnen der Leinwand ----------

const STRICHE = { strich: [8, 4], strichpunkt: [12, 3, 2, 3], punkt: [2, 3], achse: [12, 3, 2, 3], strichfein: [4, 3] };
let warten = false;
function zeichnen() { if (!warten) { warten = true; requestAnimationFrame(() => { warten = false; malen(); }); } }

/** Punkt unter dem Zeiger für Befehle und Messen: Fangpunkt, sonst Zeiger – mit Ortho ab dem letzten Punkt. Leinwand-Koordinaten. */
function zeigerPunkt() {
  if (!z.maus || !z.grafik) return null;
  if (z.fangpunkt) return [z.fangpunkt.x, z.fangpunkt.y];
  const p = zuWelt(z.maus.px, z.maus.py, groesse());
  const o = z.erwartet?.opt;
  return $('z-ortho').checked && o?.von && !o.ohneOrtho ? ortho(p, o.von) : p;
}

/** Bogen durch drei Punkte als Punktfolge (Vorschau). */
function bogenPunkte(a, b, c) {
  const d = 2 * (a[0] * (b[1] - c[1]) + b[0] * (c[1] - a[1]) + c[0] * (a[1] - b[1]));
  if (Math.abs(d) < 1e-12) return [a, b, c];
  const a2 = a[0] ** 2 + a[1] ** 2, b2 = b[0] ** 2 + b[1] ** 2, c2 = c[0] ** 2 + c[1] ** 2;
  const mx = (a2 * (b[1] - c[1]) + b2 * (c[1] - a[1]) + c2 * (a[1] - b[1])) / d, my = (a2 * (c[0] - b[0]) + b2 * (a[0] - c[0]) + c2 * (b[0] - a[0])) / d;
  const r = Math.hypot(a[0] - mx, a[1] - my), wa = Math.atan2(a[1] - my, a[0] - mx), wc = Math.atan2(c[1] - my, c[0] - mx);
  const links = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) > 0;
  let sweep = links ? wc - wa : wa - wc;
  sweep = ((sweep % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
  const aus = [];
  for (let i = 0; i <= 48; i++) { const w = wa + (links ? 1 : -1) * sweep * i / 48; aus.push([mx + r * Math.cos(w), my + r * Math.sin(w)]); }
  return aus;
}

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
        ctx.fillStyle = st.fl === '#ffffff' ? grund : !st.fl || st.fl === 'schnitt' ? planfarbe : st.fl;   // weisse Füllung deckt ab: Farbe des Hintergrunds
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
  bild();

  // Auswahl
  let n = 0;
  for (const o of z.auswahl.values()) { if (++n > 800) break; markieren(o, g); }
  // Griffe des einzeln gewählten Objekts
  for (const [x, y] of griffe()) { const p = zuBild(x, y, g); ctx.fillStyle = '#1e90ff'; ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 1; ctx.fillRect(p[0] - 4, p[1] - 4, 8, 8); ctx.strokeRect(p[0] - 4, p[1] - 4, 8, 8); }
  const zp = zeigerPunkt();
  // Auswahlfenster: nach rechts ganz darin (durchgezogen, blau), nach links berührt (gestrichelt, grün)
  if (z.fensterStart && z.maus) {
    const p = zuBild(z.fensterStart[0], z.fensterStart[1], g), nachLinks = z.maus.px < p[0];
    ctx.strokeStyle = nachLinks ? '#2ee66b' : '#1e90ff'; ctx.fillStyle = nachLinks ? 'rgba(46, 230, 107, .12)' : 'rgba(30, 144, 255, .12)';
    ctx.lineWidth = 1; ctx.setLineDash(nachLinks ? [5, 4] : []);
    ctx.beginPath(); ctx.rect(p[0], p[1], z.maus.px - p[0], z.maus.py - p[1]); ctx.fill(); ctx.stroke(); ctx.setLineDash([]);
  }
  // Vorschau des laufenden Befehls
  const vorschau = z.erwartet?.opt?.vorschau;
  if (vorschau && zp) {
    ctx.strokeStyle = '#ffd24a'; ctx.lineWidth = 1.4; ctx.setLineDash([]);
    for (const f of vorschau(zp)) {
      ctx.beginPath();
      if (f.art === 'linie' || f.art === 'bogen3') {
        (f.art === 'bogen3' ? bogenPunkte(...f.p) : f.p).forEach((w, i) => { const s = zuBild(w[0], w[1], g); if (i) ctx.lineTo(s[0], s[1]); else ctx.moveTo(s[0], s[1]); });
      } else if (f.art === 'kreis') {
        const s = zuBild(f.m[0], f.m[1], g); ctx.arc(s[0], s[1], f.r * a.zoom, 0, 2 * Math.PI);
      } else if (f.art === 'box') {
        const p = zuBild(f.b[0] + f.d[0], f.b[1] + f.d[1], g), r = zuBild(f.b[2] + f.d[0], f.b[3] + f.d[1], g);
        ctx.setLineDash([5, 3]); ctx.rect(p[0], r[1], r[0] - p[0], p[1] - r[1]);
      }
      ctx.stroke(); ctx.setLineDash([]);
    }
  }
  // Messen
  const mp = z.messpunkte.map(p => zuBild(p[0], p[1], g));
  const zeiger = z.werkzeug === 'messen' && !z.messFertig && zp ? zuBild(zp[0], zp[1], g) : null;
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
  if (z.fangpunkt && willPunkt()) {
    const [x, y] = zuBild(z.fangpunkt.x, z.fangpunkt.y, g);
    ctx.strokeStyle = '#2ee66b'; ctx.lineWidth = 1.8; ctx.beginPath();
    if (z.fangpunkt.art === 0) ctx.rect(x - 6, y - 6, 12, 12);
    else if (z.fangpunkt.art === 1) { ctx.moveTo(x, y - 7); ctx.lineTo(x + 7, y + 6); ctx.lineTo(x - 7, y + 6); ctx.closePath(); }
    else if (z.fangpunkt.art === 3) { ctx.moveTo(x - 6, y - 6); ctx.lineTo(x + 6, y + 6); ctx.moveTo(x + 6, y - 6); ctx.lineTo(x - 6, y + 6); }
    else if (z.fangpunkt.art === 4) { ctx.moveTo(x - 7, y - 7); ctx.lineTo(x - 7, y + 7); ctx.lineTo(x + 7, y + 7); ctx.moveTo(x - 7, y); ctx.lineTo(x, y); ctx.lineTo(x, y + 7); }
    else ctx.arc(x, y, 7, 0, 2 * Math.PI);
    ctx.stroke();
  }
}

/** Griffe des einzeln gewählten Objekts in Leinwand-Koordinaten (leer, wenn ein Befehl läuft oder mehrere gewählt sind). */
function griffe() {
  if (z.befehl || z.werkzeug === 'messen' || z.auswahl.size !== 1) return [];
  const o = z.auswahl.values().next().value;
  return (o.griffe || []).map(sicht);
}

function markieren(o, g) {
  const b = (x, y) => zuBild(x - z.ursprung[0], y - z.ursprung[1], g);
  ctx.strokeStyle = '#1e90ff'; ctx.lineWidth = 2.2; ctx.setLineDash([]);
  ctx.beginPath();
  if (o.von && o.bis) { const p = b(...o.von), r = b(...o.bis); ctx.moveTo(p[0], p[1]); ctx.lineTo(r[0], r[1]); }
  else if (o.punkte?.length && o.punkte.length === o.anzahlPunkte && !o.punkte.some(p => p.length > 2)) {
    o.punkte.forEach((p, i) => { const r = b(p[0], p[1]); if (i) ctx.lineTo(r[0], r[1]); else ctx.moveTo(r[0], r[1]); });
    if (o.geschlossen) ctx.closePath();
  } else if (o.art === 'kreis' && o.mitte) { const m = b(...o.mitte); ctx.arc(m[0], m[1], o.radius * z.ansicht.zoom, 0, 2 * Math.PI); }
  else if (o.box) {
    const p = b(o.box[0], o.box[1]), r = b(o.box[2], o.box[3]);
    const w = Math.max(r[0] - p[0], 10), h = Math.max(p[1] - r[1], 10);
    ctx.setLineDash([5, 3]); ctx.rect(p[0] - (w - (r[0] - p[0])) / 2, r[1] - (h - (p[1] - r[1])) / 2, w, h);
  }
  ctx.stroke(); ctx.setLineDash([]);
}

// ---------- Befehle ----------

/** Wartet der laufende Befehl (oder das Messen) auf einen Punkt per Klick? */
const willPunkt = () => z.werkzeug === 'messen' || z.erwartet?.art === 'punkt' || (z.erwartet?.art === 'zahl' && !!z.erwartet.opt?.von);

function promptZeigen() {
  const e = z.erwartet;
  let t = 'Befehl:';
  if (e) t = e.prompt + (e.art === 'zahl' && e.opt?.vorgabe != null ? ` <${e.opt.vorgabe}>` : '') + ':';
  else if (z.werkzeug === 'messen') t = 'Messen – Punkt (Eingabetaste schliesst ab):';
  $('z-prompt').textContent = t;
  for (const k of document.querySelectorAll('#z-werkzeuge button[data-befehl]'))
    k.classList.toggle('aktiv', k.dataset.befehl === (z.befehl || (z.werkzeug === 'messen' ? 'messen' : '')));
}

function erwarten(art, prompt, opt = {}) {
  return new Promise((resolve, reject) => { z.erwartet = { art, prompt, opt, resolve, reject }; promptZeigen(); zeichnen(); });
}
function erfuellen(wert) { const e = z.erwartet; z.erwartet = null; promptZeigen(); e?.resolve(wert); }

async function nachAenderung(r, { auswahlBehalten = false } = {}) {
  standZeigen(r);
  if (!auswahlBehalten) { z.auswahl.clear(); eigenschaftenZeigen(); }
  await grafikLaden(false);
}

const io = {
  merken: z.merken,
  punkt: (prompt, opt) => erwarten('punkt', prompt, opt),
  zahl: (prompt, opt) => erwarten('zahl', prompt, opt),
  text: prompt => erwarten('text', prompt),
  /** Ein Objekt anklicken: { kennung, objekt, punkt (Klick, Leinwand) } oder null (Eingabetaste). */
  objekt: prompt => erwarten('objekt', prompt),
  /** Schritt ausführen; ein Fehler des Servers wird gemeldet, der Befehl läuft weiter. */
  async versuchen(schritt) { try { await schritt(); } catch (e) { if (e instanceof Abbruch) throw e; melden(fehlerText(e), true); } },
  auswahlLeeren() { z.auswahl.clear(); eigenschaftenZeigen(); zeichnen(); },
  melden: text => melden(text),
  /** Objekte im Fenster a–b (berührt) wählen: Kennungen. */
  async imFenster(a, b) {
    const r = await holen('/api/zeichnung/auswahl?' + q() + '&fenster=' + [...echt(a), ...echt(b)].join(',') + '&kreuzen=1');
    z.auswahl.clear();
    for (const o of r.objekte) z.auswahl.set(o.kennung, o);
    eigenschaftenZeigen(); zeichnen();
    return r.objekte.map(o => o.kennung);
  },
  async bloecke() { return (await holen('/api/zeichnung/bloecke?zeichnung=' + encodeURIComponent(z.info.pfad))).bloecke.map(b => b.name); },
  async bemassungsstil(angaben) {
    const r = await senden('/api/zeichnung/bemassungsstil', { zeichnung: z.info.pfad, ...angaben });
    if (angaben.massstab != null || angaben.texthoehe != null) await nachAenderung(r);
    return r;
  },
  /** Der angeklickte Griff: { kennung, index, ort (Leinwand) }. */
  griff: () => z.griffWahl,
  async auswahl(prompt) {
    if (z.auswahl.size) return [...z.auswahl.keys()];
    return erwarten('auswahl', prompt);
  },
  /** Boxen der Auswahl in Leinwand-Koordinaten (Vorschau beim Schieben). */
  boxen: () => [...z.auswahl.values()].filter(o => o.box).slice(0, 300).map(o => [...sicht([o.box[0], o.box[1]]), ...sicht([o.box[2], o.box[3]])]),
  async zeichnen(objekte, opt = { auswahlBehalten: true }) {
    // Punkte der Befehle sind Leinwand-Koordinaten: für den Server zurück in die Zeichnung
    const um = o => {
      const n = { ...o };
      for (const k of ['von', 'bis', 'ecke1', 'ecke2', 'mitte', 'ort', 'hauptachse']) if (n[k]) n[k] = echt(n[k]);
      if (n.punkte) n.punkte = n.punkte.map(p => [...echt(p), ...p.slice(2)]);
      return n;
    };
    await nachAenderung(await senden('/api/zeichnung/zeichnen', { zeichnung: z.info.pfad, layout: z.layout, objekte: objekte.map(um) }), opt);
  },
  async aendern(kennungen, aktion, angaben, opt) {
    const a = { ...angaben };
    for (const k of ['um', 'seite', 'punkt', 'punkt2', 'basis', 'nach']) if (a[k]) a[k] = echt(a[k]);
    for (const k of ['punkte', 'achse']) if (a[k]) a[k] = a[k].map(echt);
    if (a.fenster) a.fenster = [...echt(a.fenster[0]), ...echt(a.fenster[1])];
    await nachAenderung(await senden('/api/zeichnung/aendern', { zeichnung: z.info.pfad, kennungen, aktion, angaben: a }), opt);
  },
  async layerAnlegen(name) {
    standZeigen(await senden('/api/zeichnung/layer-anlegen', { zeichnung: z.info.pfad, name, aktuell: true }));
    await layerLaden();
  },
};

function abbrechen() {
  const e = z.erwartet;
  z.erwartet = null; z.befehl = null; z.laufNr++; z.fensterStart = null;
  e?.reject(new Abbruch());
  promptZeigen(); zeichnen();
}

async function befehlStarten(name) {
  if (!z.info) { melden('Zuerst eine Zeichnung öffnen.', true); return; }
  if (z.befehl) abbrechen();
  if (z.werkzeug === 'messen' && name !== 'messen') werkzeug('auswahl');
  switch (name) {
    case 'zurueck': return journal('rueckgaengig');
    case 'wieder': return journal('wiederholen');
    case 'grenzen': return zoomGrenzen();
    case 'speichern': return speichern();
    case 'messen': return werkzeug(z.werkzeug === 'messen' ? 'auswahl' : 'messen');
  }
  if (!BEFEHLE[name]) { melden(`Den Befehl «${name}» gibt es nicht.`, true); return; }
  const lauf = ++z.laufNr;
  z.befehl = name; z.letzterBefehl = name; z.fensterStart = null;
  promptZeigen();
  try { await BEFEHLE[name](io); }
  catch (e) { if (!(e instanceof Abbruch)) melden(fehlerText(e), true); }
  finally { if (z.laufNr === lauf) { z.befehl = null; z.erwartet = null; promptZeigen(); zeichnen(); } }
}

async function journal(was) {
  try { await nachAenderung(await senden('/api/zeichnung/' + was, { zeichnung: z.info.pfad })); await layerLaden(); }
  catch (e) { melden(fehlerText(e), true); }
}

/** Eingabetaste (oder Leertaste) in der Befehlszeile. */
function eingabeAbschicken() {
  const t = eingabe.value; eingabe.value = '';
  const e = z.erwartet;
  if (e) {
    if (e.art === 'text') return erfuellen(t.trim() || null);
    if (e.art === 'auswahl') return erfuellen([...z.auswahl.keys()]);
    if (e.art === 'objekt') return erfuellen(null);
    if (e.art === 'punkt') {
      if (!t.trim()) return erfuellen(null);
      const zp = zeigerPunkt();
      const p = punktLesen(t, e.opt.von ? echt(e.opt.von) : null, zp ? echt(zp) : null);
      return erfuellen(p ? sicht(p) : t.trim());
    }
    if (e.art === 'zahl') {
      if (!t.trim()) return erfuellen(e.opt.vorgabe ?? null);
      const v = Number(t.trim().replace(',', '.'));
      if (!Number.isFinite(v)) { melden('Hier wird eine Zahl erwartet.', true); return; }
      return erfuellen(v);
    }
  }
  if (z.werkzeug === 'messen' && !t.trim()) return messenAbschliessen(false);
  if (!t.trim()) { if (z.letzterBefehl) befehlStarten(z.letzterBefehl); return; }
  const name = KUERZEL[t.trim().toLowerCase()];
  if (name) befehlStarten(name); else melden(`Unbekannter Befehl «${t.trim()}». Kürzel stehen an den Knöpfen (z. B. L, PL, K, S, KO, SU, DE, AR, VE, SP, BEM).`, true);
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
  z.ansicht.cx = wx - (m.px - g.w / 2) / z.ansicht.zoom;   // der Punkt unter dem Zeiger bleibt stehen
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
  z.fangpunkt = $('z-fang').checked && z.fang && !zug?.bewegt && willPunkt() && !z.erwartet?.opt?.ohneFang ? fangSuchen(z.fang, z.fangLinien, wx, wy, 10 / z.ansicht.zoom, z.erwartet?.opt?.von || (z.werkzeug === 'messen' && !z.messFertig ? z.messpunkte.at(-1) : null)) : null;
  const ort = zeigerPunkt() || [wx, wy];
  const [ox, oy] = z.ursprung;
  $('z-koord').textContent = `${(ort[0] + ox).toFixed(3)}, ${(ort[1] + oy).toFixed(3)}`;
  $('z-fangtext').textContent = z.fangpunkt ? FANGNAMEN[z.fangpunkt.art] : '';
  if (z.werkzeug === 'messen' && z.messpunkte.length && !z.messFertig) massZeigen([...z.messpunkte, ort]);
  zeichnen();
});

leinwand.addEventListener('pointerup', e => {
  if (!zug) return;
  const war = zug; zug = null;
  leinwand.style.cursor = '';
  eingabe.focus();
  if (war.bewegt || war.taste !== 0 || !z.grafik) { zeichnen(); return; }
  const g = groesse();
  const frei = zuWelt(war.px, war.py, g);
  const erw = z.erwartet;
  if (erw?.art === 'punkt' || (erw?.art === 'zahl' && erw.opt?.von)) {
    const p = zeigerPunkt() || frei;
    if (erw.art === 'punkt') return erfuellen(p);
    const von = erw.opt.von;
    return erfuellen(erw.opt.art === 'winkel' ? Math.round(Math.atan2(p[1] - von[1], p[0] - von[0]) * 180 / Math.PI * 1e6) / 1e6 : Math.hypot(p[0] - von[0], p[1] - von[1]));
  }
  if (erw?.art === 'objekt') { objektKlick(frei); return; }
  if (erw && erw.art !== 'auswahl') return;   // Zahl bzw. Text wird getippt
  if (z.werkzeug === 'messen') {
    if (z.messFertig) { z.messpunkte = []; z.messFertig = false; }
    z.messpunkte.push(zeigerPunkt() || frei);
    massZeigen(z.messpunkte);
    zeichnen();
  } else {
    // Klick auf einen Griff des gewählten Objekts: Griff ziehen
    const gr = griffe(), i = gr.findIndex(p => { const s = zuBild(p[0], p[1], g); return Math.abs(s[0] - war.px) <= 7 && Math.abs(s[1] - war.py) <= 7; });
    if (i >= 0) { z.griffWahl = { kennung: z.auswahl.keys().next().value, index: i, ort: gr[i] }; befehlStarten('griff'); return; }
    waehlen(frei[0], frei[1], war.px);
  }
});

leinwand.addEventListener('dblclick', e => { if (z.werkzeug === 'messen') { e.preventDefault(); messenAbschliessen(true); } });
leinwand.addEventListener('auxclick', e => { if (e.button === 1 && e.detail === 2) zoomGrenzen(); });
leinwand.addEventListener('contextmenu', e => { e.preventDefault(); if (z.erwartet || z.werkzeug === 'messen') eingabeAbschicken(); });
leinwand.addEventListener('pointerleave', () => { z.maus = null; z.fangpunkt = null; zeichnen(); });

/** Klick, wenn ein Befehl ein einzelnes Objekt verlangt: Objekt am Punkt suchen und dem Befehl geben. */
async function objektKlick(p) {
  const [ox, oy] = z.ursprung;
  try {
    const r = await holen(`/api/zeichnung/treffer?${q()}&x=${p[0] + ox}&y=${p[1] + oy}&r=${7 / z.ansicht.zoom}`);
    if (z.erwartet?.art !== 'objekt') return;
    if (r.objekt) erfuellen({ kennung: r.objekt.kennung, objekt: r.objekt, punkt: p }); else melden('An dieser Stelle liegt kein Objekt.');
  } catch (e) { melden(fehlerText(e), true); }
}

// ---------- Auswahl ----------

// Klicks nacheinander abarbeiten: der zweite Klick eines Fensters darf die Antwort auf den ersten nicht überholen
let wahlKette = Promise.resolve();
function waehlen(wx, wy, px) { wahlKette = wahlKette.then(() => waehlenJetzt(wx, wy, px)); return wahlKette; }

async function waehlenJetzt(wx, wy, px) {
  const [ox, oy] = z.ursprung;
  try {
    if (z.fensterStart) {
      const a = z.fensterStart; z.fensterStart = null;
      const kreuzen = px < zuBild(a[0], a[1], groesse())[0];   // nach links gezogen: auch berührte Objekte
      const r = await holen(`/api/zeichnung/auswahl?${q()}&fenster=${[a[0] + ox, a[1] + oy, wx + ox, wy + oy].join(',')}&kreuzen=${kreuzen ? 1 : 0}`);
      for (const o of r.objekte) if (!z.auswahl.has(o.kennung)) z.auswahl.set(o.kennung, o);
      if (r.anzahl > r.objekte.length) melden(`${r.anzahl} Objekte im Fenster, die ersten ${r.objekte.length} sind gewählt.`);
    } else {
      const r = await holen(`/api/zeichnung/treffer?${q()}&x=${wx + ox}&y=${wy + oy}&r=${7 / z.ansicht.zoom}`);
      if (!r.objekt) z.fensterStart = [wx, wy];
      else if (z.auswahl.has(r.objekt.kennung)) z.auswahl.delete(r.objekt.kennung);
      else z.auswahl.set(r.objekt.kennung, r.objekt);
    }
  } catch (e) { melden(fehlerText(e), true); return; }
  eigenschaftenZeigen();
  zeichnen();
}

const EIGNAMEN = { kennung: 'Kennung', art: 'Art', layer: 'Layer', block: 'Block', text: 'Text', laenge: 'Länge', flaeche: 'Fläche', radius: 'Radius', hoehe: 'Höhe',
  masszahl: 'Masszahl', muster: 'Muster', drehungGrad: 'Drehung', geschlossen: 'Geschlossen', anzahlPunkte: 'Punkte', von: 'Von', bis: 'Bis', mitte: 'Mitte', ort: 'Ort', massstab: 'Massstab' };

function tabelle(zeilen) {
  const t = document.createElement('table');
  for (const [k, v] of zeilen) { const tr = t.insertRow(); tr.insertCell().textContent = k; tr.insertCell().textContent = v; }
  return t;
}
const absatz = text => { const p = document.createElement('p'); p.className = 'z-leise'; p.textContent = text; return p; };
const einheitBekannt = () => z.layout !== 'Model' || !/unbekannt/.test(z.grafik.einheit);

function eigenschaftenZeigen() {
  const ziel = $('z-eigenschaften');
  ziel.replaceChildren();
  if (!z.auswahl.size) { ziel.append(absatz(z.grafik ? 'Objekt anklicken. Klick ins Leere beginnt ein Auswahlfenster.' : '')); return; }
  if (z.auswahl.size > 1) {
    const jeArt = {};
    for (const o of z.auswahl.values()) jeArt[o.art] = (jeArt[o.art] || 0) + 1;
    ziel.append(tabelle([['Gewählt', z.auswahl.size + ' Objekte'], ...Object.entries(jeArt).map(([k, n]) => [k, String(n)])]));
    ziel.append(absatz('Entf löscht, S schiebt, KO kopiert, DR dreht. Esc hebt die Auswahl auf.'));
    return;
  }
  const o = z.auswahl.values().next().value, bek = einheitBekannt(), mm = z.grafik.mmJeEinheit;
  const zeilen = [];
  for (const k of Object.keys(EIGNAMEN)) {
    if (o[k] == null) continue;
    let v = o[k];
    if (k === 'laenge' || k === 'radius' || k === 'hoehe' || k === 'masszahl') v = laengeText(v, mm, bek);
    else if (k === 'flaeche') v = flaecheText(v, mm, bek);
    else if (k === 'drehungGrad') v += '°';
    else if (k === 'geschlossen') v = v ? 'ja' : 'nein';
    else if (Array.isArray(v)) v = v.join(', ');
    zeilen.push([EIGNAMEN[k], String(v)]);
  }
  for (const [k, v] of Object.entries(o.attribute || {})) zeilen.push(['Attribut ' + k, v]);
  ziel.append(tabelle(zeilen));
}

// ---------- Messen ----------

function massZeigen(punkte) {
  const ziel = $('z-mass');
  ziel.replaceChildren();
  const w = messwerte(punkte);
  if (!w) { ziel.append(absatz('Nächsten Punkt anklicken.')); return; }
  const bek = einheitBekannt(), mm = z.grafik.mmJeEinheit;
  const L = v => laengeText(v, mm, bek);
  const zeilen = [['Strecke', L(w.strecke)], ['ΔX', L(w.dx)], ['ΔY', L(w.dy)], ['Richtung', w.richtung.toFixed(2) + '°']];
  if (punkte.length > 2) zeilen.push(['Winkel', w.winkel.toFixed(2) + '°'], ['Linienzug', L(w.laenge)], ['Umfang', L(w.umfang)], ['Fläche', flaecheText(w.flaeche, mm, bek)]);
  ziel.append(tabelle(zeilen));
  ziel.append(absatz(z.messFertig ? 'Abgeschlossen. Klick beginnt eine neue Messung.' : 'Eingabetaste oder Doppelklick schliesst ab, Esc verwirft.'));
}

function messenLeeren() {
  z.messpunkte = []; z.messFertig = false;
  $('z-mass').replaceChildren(absatz('«Messen» wählen und Punkte anklicken.'));
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
  if (name !== 'messen') messenLeeren();
  z.fensterStart = null;
  promptZeigen();
  zeichnen();
}

// ---------- Stand, Speichern ----------

function standZeigen(s) {
  if (!s || s.geaendert === undefined) return;
  z.stand = s;
  $('z-zurueck').disabled = !s.rueckgaengig; $('z-zurueck').title = s.rueckgaengig ? `Rückgängig: ${s.rueckgaengig} (Strg+Z)` : 'Rückgängig (Strg+Z)';
  $('z-wieder').disabled = !s.wiederholen; $('z-wieder').title = s.wiederholen ? `Wiederholen: ${s.wiederholen} (Strg+Y)` : 'Wiederholen (Strg+Y)';
  const name = z.info.zeichnung + (s.geaendert ? ' *' : '');
  $('z-name').textContent = name; $('z-name').title = z.info.pfad + (s.geaendert ? ' (geändert, nicht gespeichert)' : '');
  document.title = name + ' – KICAD Zeichnung';
  if (s.aktuellerLayer) $('z-aktueller-layer').value = s.aktuellerLayer;
}

async function standLaden() {
  try { standZeigen(await holen(`/api/zeichnung/stand?zeichnung=${encodeURIComponent(z.info.pfad)}`)); } catch (e) { melden(fehlerText(e), true); }
}

async function speichern(datei, fassung) {
  if (!z.info) return false;
  abbrechen();
  melden('Speichere …');
  try {
    const r = await senden('/api/zeichnung/speichern', { zeichnung: z.info.pfad, datei: datei || undefined, fassung: fassung || undefined });
    z.info.pfad = r.datei; z.info.zeichnung = r.zeichnung;
    merkenDatei(r.datei);
    standZeigen(r);
    const zusatz = [...(r.hinweise || []), ...(r.meldungen || [])];
    melden(`Gespeichert: ${r.zeichnung} (${r.fassung}, ${r.objekte} Objekte${r.geprueft ? ', wiedergelesen' : ''}).` + (zusatz.length ? ' ' + zusatz.join(' ') : ''), !r.geprueft);
    return true;
  } catch (e) { melden(fehlerText(e), true); return false; }
}

// ---------- Layer ----------

async function layerLaden() {
  try { z.layer = (await holen(`/api/zeichnung/layer?zeichnung=${encodeURIComponent(z.info.pfad)}`)).layer; }
  catch (e) { melden(fehlerText(e), true); return; }
  layerZeigen();
  const wahl = $('z-aktueller-layer'), jetzt = z.stand.aktuellerLayer || wahl.value;
  wahl.replaceChildren(...z.layer.map(l => { const o = document.createElement('option'); o.textContent = l.name; return o; }));
  if (jetzt) wahl.value = jetzt;
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
  $('z-layer-anzahl').textContent = `${z.layer.filter(l => l.ein && !l.gefroren).length} von ${z.layer.length} ein`;
  for (const l of layerSichtbar()) {
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
  z.auswahl.clear(); eigenschaftenZeigen();
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
    k.addEventListener('click', async () => {
      abbrechen(); z.layout = l.name; z.ursprung = null; z.auswahl.clear(); eigenschaftenZeigen(); messenLeeren();
      layoutsZeigen(); await grafikLaden(true);
    });
    ziel.append(k);
  }
}

function hinweiseZeigen() {
  const ziel = $('z-hinweise');
  const zeilen = Object.entries(z.grafik.nichtDargestellt || {}).map(([k, n]) => `${n} × ${k}`);
  ziel.replaceChildren(absatz(zeilen.length ? 'Nicht dargestellt: ' + zeilen.join(', ') + '.' : 'Alles dargestellt.'),
    absatz(`Fassung ${z.info.fassung}. Schriften als Arial, Linienarten vereinfacht.`));
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
  $('z-p-hinweis').textContent = z.layout !== 'Model' ? 'Ein Layout wird ohne weitere Angabe 1:1 auf seinem Blatt ausgegeben.'
    : /unbekannt/.test(z.grafik.einheit) ? 'Die Datei nennt keine Einheit: nur «eingepasst» ist möglich.' : '';
  $('z-pdf-dialog').showModal();
}

async function pdfLos() {
  const p = new URLSearchParams({ zeichnung: z.info.pfad, layout: z.layout, farbig: $('z-p-farbig').value });
  if ($('z-p-massstab').value) p.set('massstab', $('z-p-massstab').value);
  if ($('z-p-blatt').value) p.set('blatt', $('z-p-blatt').value);
  if ($('z-p-bereich').value === 'ansicht') {
    const g = groesse(), a = echt(zuWelt(0, g.h, g)), b = echt(zuWelt(g.w, 0, g));
    p.set('fenster', [...a, ...b].map(v => v.toFixed(4)).join(','));
  }
  $('z-pdf-dialog').close();
  melden('PDF wird erzeugt …');
  try {
    const r = await fetch('/api/zeichnung/pdf?' + p, { cache: 'no-store' });
    if (!r.ok) { const f = await r.json().catch(() => ({})); throw Object.assign(new Error(f.fehler || 'PDF fehlgeschlagen'), { vorschlag: f.vorschlag }); }
    window.open(URL.createObjectURL(await r.blob()), '_blank');
    melden('');
  } catch (e) { melden(fehlerText(e), true); }
}

// ---------- Bedienung ----------

$('z-oeffnen').addEventListener('click', dialogOeffnen);
$('z-oeffnen2').addEventListener('click', dialogOeffnen);
$('z-neu').addEventListener('click', neu);
$('z-d-hoch').addEventListener('click', () => ordnerZeigen(dialog.dataset.eltern || ''));
$('z-d-los').addEventListener('click', pfadGehen);
$('z-d-pfad').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); pfadGehen(); } });
$('z-grenzen').addEventListener('click', zoomGrenzen);
$('z-pdf').addEventListener('click', pdfOeffnen);
$('z-p-los').addEventListener('click', pdfLos);
$('z-zurueck').addEventListener('click', () => befehlStarten('zurueck'));
$('z-wieder').addEventListener('click', () => befehlStarten('wieder'));
$('z-speichern').addEventListener('click', () => speichern());
$('z-speichern-unter').addEventListener('click', () => { if (!z.info) return; $('z-su-pfad').value = z.info.pfad; $('z-su-fehler').textContent = ''; $('z-su-dialog').showModal(); });
$('z-su-los').addEventListener('click', async () => {
  const pfad = $('z-su-pfad').value.trim().replace(/^"|"$/g, '');
  if (!/\.(dwg|dxf)$/i.test(pfad)) { $('z-su-fehler').textContent = 'Die Datei muss auf .dwg oder .dxf enden.'; return; }
  $('z-su-dialog').close();
  await speichern(pfad, $('z-su-fassung').value);
});
for (const k of document.querySelectorAll('#z-werkzeuge button[data-befehl]')) k.addEventListener('click', () => { befehlStarten(k.dataset.befehl); eingabe.focus(); });
$('z-aktueller-layer').addEventListener('change', async e => {
  try { standZeigen(await senden('/api/zeichnung/layer-anlegen', { zeichnung: z.info.pfad, name: e.target.value, aktuell: true })); }
  catch (f) { melden(fehlerText(f), true); }
  eingabe.focus();
});
for (const id of ['z-fang', 'z-ortho', 'z-staerken', 'z-hell']) $(id).addEventListener('change', () => { zeichnen(); eingabe.focus(); });
$('z-layer-filter').addEventListener('input', layerZeigen);
$('z-layer-alle').addEventListener('click', () => layerSetzen(layerSichtbar().filter(l => !l.ein && !l.gefroren).map(l => l.name), true));
$('z-layer-keine').addEventListener('click', () => layerSetzen(layerSichtbar().filter(l => l.ein && !l.gefroren).map(l => l.name), false));

eingabe.addEventListener('keydown', e => {
  if (e.key === 'Enter' || (e.key === ' ' && z.erwartet?.art !== 'text')) { e.preventDefault(); eingabeAbschicken(); }
});

window.addEventListener('keydown', e => {
  if (dialog.open || $('z-pdf-dialog').open || $('z-su-dialog').open) return;
  const feld = e.target instanceof HTMLInputElement && e.target !== eingabe && e.target.type !== 'checkbox' || e.target instanceof HTMLSelectElement;
  if (e.ctrlKey || e.metaKey) {
    const k = e.key.toLowerCase();
    if (k === 'z') { e.preventDefault(); befehlStarten('zurueck'); }
    else if (k === 'y') { e.preventDefault(); befehlStarten('wieder'); }
    else if (k === 's') { e.preventDefault(); speichern(); }
    else if (k === 'o') { e.preventDefault(); dialogOeffnen(); }
    return;
  }
  if (e.key === 'Escape') {
    eingabe.value = '';
    if (z.befehl || z.erwartet) abbrechen();
    else if (z.werkzeug === 'messen') { if (z.messpunkte.length) { messenLeeren(); zeichnen(); } else werkzeug('auswahl'); }
    else if (z.fensterStart) { z.fensterStart = null; zeichnen(); }
    else { z.auswahl.clear(); eigenschaftenZeigen(); zeichnen(); }
    return;
  }
  if (e.key === 'F3') { e.preventDefault(); $('z-fang').checked = !$('z-fang').checked; zeichnen(); return; }
  if (e.key === 'F8') { e.preventDefault(); $('z-ortho').checked = !$('z-ortho').checked; zeichnen(); return; }
  if (feld) return;
  if (e.key === 'Home') zoomGrenzen();
  else if (e.key === 'Delete' && !z.befehl && z.auswahl.size && eingabe.value === '') befehlStarten('loeschen');
  else if (e.target !== eingabe && (e.key.length === 1 || e.key === 'Enter')) {
    eingabe.focus();   // Tippen irgendwo landet in der Befehlszeile
    if (e.key === 'Enter') { e.preventDefault(); eingabeAbschicken(); }
  }
});

window.addEventListener('beforeunload', e => { if (z.stand.geaendert) { e.preventDefault(); e.returnValue = ''; } });
new ResizeObserver(() => (z.grenzenOffen ? zoomGrenzen() : zeichnen())).observe(flaeche);
symboleSetzen();
zuletztZeigen();
promptZeigen();
// Für Prüfungen von aussen (Browserprüfung)
window.hlksZeichnung = { zustand: z, oeffnen, zoomGrenzen, befehlStarten, befehlsnamen: BEFEHLSNAMEN,
  /** Ort eines Zeichnungspunkts im Browserfenster (Bildpunkte). */
  zuFenster(x, y) { const p = zuBild(...sicht([x, y]), groesse()), r = leinwand.getBoundingClientRect(); return [p[0] + r.left, p[1] + r.top]; } };
const start = new URLSearchParams(location.search).get('datei');
if (start) oeffnen(start);
