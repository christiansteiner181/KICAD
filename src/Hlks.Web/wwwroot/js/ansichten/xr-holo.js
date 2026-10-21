// Modul XR: gemeinsamer Hologramm-Stil für das Tablet (XR-C), das Messwerkzeug und die Masseinblendungen von XR-B.
// Christian 21.10.2026: «etwas futuristisch, mit Transparenz, hologrammmässig». Gestaltung: durchscheinende dunkle Fläche (hinter der Schrift ein dunkler,
// halbtransparenter Grund – Lesbarkeit geht vor), Linien und Schrift in Cyan/Blau, feines Raster und Scanlinien, weicher leuchtender Rand (zweite, additiv
// gemischte Fläche), kurzes Aufleuchten der Tasten. Nur Canvas-Texturen aus three (r160), keine externen Dateien.

import * as THREE from 'three';

export const HOLO = Object.freeze({
  cyan: '#38e8ff', blau: '#3d8bff', hell: '#c8f8ff', grund: 'rgba(4, 16, 30, 0.80)', grundTaste: 'rgba(8, 40, 64, 0.72)', warn: '#ffb347', aus: 'rgba(120, 150, 170, 0.55)',
});
const SCHRIFT = '600 20px "Segoe UI", system-ui, sans-serif';

function leinwand(b, h) {
  const c = typeof document !== 'undefined' ? document.createElement('canvas') : null;
  if (!c) return null;
  c.width = b; c.height = h;
  return c;
}

/** Abgerundetes Rechteck als Pfad. */
export function rundRechteck(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y); ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r); ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath();
}

/** Grund einer Fläche: dunkel durchscheinend, feines Raster, Scanlinien, Rand in Cyan; t = Zeit in s (läuft das Band über die Fläche). */
export function holoGrund(ctx, b, h, t = 0) {
  ctx.clearRect(0, 0, b, h);
  rundRechteck(ctx, 3, 3, b - 6, h - 6, 14);
  ctx.fillStyle = HOLO.grund; ctx.fill();
  ctx.save(); rundRechteck(ctx, 3, 3, b - 6, h - 6, 14); ctx.clip();
  ctx.strokeStyle = 'rgba(56, 232, 255, 0.07)'; ctx.lineWidth = 1;
  for (let x = 16; x < b; x += 32) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke(); }   // feines Raster
  for (let y = 16; y < h; y += 32) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(b, y); ctx.stroke(); }
  ctx.fillStyle = 'rgba(120, 220, 255, 0.05)';
  for (let y = 0; y < h; y += 4) ctx.fillRect(0, y, b, 1);   // Scanlinien
  const band = ((t * 0.35) % 1) * (h + 60) - 30;           // langsames helles Band
  const g = ctx.createLinearGradient(0, band - 30, 0, band + 30);
  g.addColorStop(0, 'rgba(56, 232, 255, 0)'); g.addColorStop(0.5, 'rgba(56, 232, 255, 0.10)'); g.addColorStop(1, 'rgba(56, 232, 255, 0)');
  ctx.fillStyle = g; ctx.fillRect(0, band - 30, b, 60);
  ctx.restore();
  rundRechteck(ctx, 3, 3, b - 6, h - 6, 14);
  ctx.strokeStyle = HOLO.cyan; ctx.lineWidth = 2.5; ctx.stroke();
  ctx.strokeStyle = 'rgba(61, 139, 255, 0.55)'; ctx.lineWidth = 1;
  rundRechteck(ctx, 9, 9, b - 18, h - 18, 10); ctx.stroke();
}

/** Taste: dunkler Grund, Cyan-Rand; aufleuchten 0 … 1 = kurzes Aufleuchten nach dem Antippen; an = Umschalter eingeschaltet; platzhalter = ausgegraut. */
export function holoTaste(ctx, t, { aufleuchten = 0, an = false, platzhalter = false, text = t.text, zusatz = '' } = {}) {
  rundRechteck(ctx, t.x, t.y, t.w, t.h, 8);
  ctx.fillStyle = platzhalter ? 'rgba(20, 30, 40, 0.65)' : an ? 'rgba(20, 110, 150, 0.78)' : HOLO.grundTaste; ctx.fill();
  if (aufleuchten > 0) { ctx.fillStyle = `rgba(150, 245, 255, ${0.65 * aufleuchten})`; ctx.fill(); }
  ctx.lineWidth = an ? 2.5 : 1.5;
  ctx.strokeStyle = platzhalter ? HOLO.aus : an || aufleuchten > 0 ? HOLO.hell : HOLO.cyan; ctx.stroke();
  ctx.font = SCHRIFT; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.shadowColor = platzhalter ? 'transparent' : HOLO.cyan; ctx.shadowBlur = platzhalter ? 0 : 6;
  ctx.fillStyle = platzhalter ? HOLO.aus : aufleuchten > 0.3 ? '#ffffff' : HOLO.hell;
  const f = ctx.measureText(text).width > t.w - 10 ? '600 16px "Segoe UI", system-ui, sans-serif' : SCHRIFT;
  ctx.font = f;
  ctx.fillText(text, t.x + t.w / 2, t.y + t.h / 2 - (zusatz ? 7 : 0));
  ctx.shadowBlur = 0;
  if (zusatz) { ctx.font = '500 14px "Segoe UI", system-ui, sans-serif'; ctx.fillStyle = platzhalter ? HOLO.aus : HOLO.warn; ctx.fillText(zusatz, t.x + t.w / 2, t.y + t.h / 2 + 13); }
}

/** Textzeile auf dem Tablet (linksbündig). */
export function holoZeile(ctx, text, x, y, { gross = false, farbe = HOLO.hell, max = 480 } = {}) {
  ctx.font = gross ? '700 24px "Segoe UI", system-ui, sans-serif' : '500 17px "Segoe UI", system-ui, sans-serif';
  ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  ctx.shadowColor = HOLO.cyan; ctx.shadowBlur = gross ? 8 : 3; ctx.fillStyle = farbe;
  ctx.fillText(text, x, y, max);
  ctx.shadowBlur = 0;
}

/** Weiche Randtextur für die zweite, additiv gemischte Fläche (Leuchten um das Tablet). */
export function glowTextur(b = 256, h = 192) {
  const c = leinwand(b, h);
  if (!c) return null;
  const ctx = c.getContext('2d');
  ctx.clearRect(0, 0, b, h);
  const farben = ['rgba(56, 232, 255, 0.55)', 'rgba(61, 139, 255, 0.0)'];
  for (let i = 0; i < 14; i++) {
    const a = (1 - i / 14) ** 2;
    rundRechteck(ctx, 18 + i * 1.4, 18 + i * 1.4, b - 36 - i * 2.8, h - 36 - i * 2.8, 12);
    ctx.strokeStyle = `rgba(56, 232, 255, ${0.10 * a})`; ctx.lineWidth = 3; ctx.stroke();
  }
  rundRechteck(ctx, 18, 18, b - 36, h - 36, 12);
  ctx.strokeStyle = farben[0]; ctx.lineWidth = 2; ctx.shadowColor = '#38e8ff'; ctx.shadowBlur = 16; ctx.stroke();
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

/** Etikett im Raum (Mass, Fangname): dunkler halbtransparenter Grund, Cyan-Schrift, leuchtender Rand. Rückgabe { sprite, setzen(text), freigeben() }. */
export function etikett(text = '', { breite = 0.2, farbe = HOLO.hell } = {}) {
  const B = 512, H = 96;
  const c = leinwand(B, H);
  const tex = c ? new THREE.CanvasTexture(c) : null;
  if (tex) tex.colorSpace = THREE.SRGBColorSpace;
  const mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, depthWrite: false });
  const sprite = new THREE.Sprite(mat);
  sprite.scale.set(breite, breite * H / B, 1);
  sprite.renderOrder = 1000;
  sprite.userData.text = '';
  const setzen = (neu) => {
    if (neu === sprite.userData.text || !c) { sprite.userData.text = neu; return; }
    sprite.userData.text = neu;
    const ctx = c.getContext('2d');
    ctx.clearRect(0, 0, B, H);
    rundRechteck(ctx, 4, 4, B - 8, H - 8, 16);
    ctx.fillStyle = 'rgba(4, 16, 30, 0.82)'; ctx.fill();
    ctx.shadowColor = HOLO.cyan; ctx.shadowBlur = 10; ctx.strokeStyle = HOLO.cyan; ctx.lineWidth = 3; ctx.stroke(); ctx.shadowBlur = 0;
    const zeilen = String(neu).split('\n').slice(0, 2);
    ctx.font = `700 ${zeilen.length > 1 ? 32 : 40}px "Segoe UI", system-ui, sans-serif`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.shadowColor = HOLO.cyan; ctx.shadowBlur = 8; ctx.fillStyle = farbe;
    zeilen.forEach((z, i) => ctx.fillText(z, B / 2, H / 2 + 2 + (zeilen.length > 1 ? (i - 0.5) * 36 : 0), B - 28));
    ctx.shadowBlur = 0;
    tex.needsUpdate = true;
  };
  setzen(text);
  return { sprite, setzen, freigeben() { tex?.dispose(); mat.dispose(); } };
}

/** Fangsymbol je Fangart (Zeichen auf einer kleinen Fläche): Anschluss = Kreis mit Punkt, Ecke = Quadrat, Mitte = Dreieck, Kante/Achse = Kreuz, Fläche = Raute. */
export function fangSymbol(art) {
  const S = 128;
  const c = leinwand(S, S);
  const tex = c ? new THREE.CanvasTexture(c) : null;
  if (c) {
    const ctx = c.getContext('2d');
    ctx.lineWidth = 9; ctx.strokeStyle = HOLO.cyan; ctx.shadowColor = HOLO.cyan; ctx.shadowBlur = 14; ctx.lineJoin = 'round';
    const m = S / 2, r = 40;
    ctx.beginPath();
    if (art === 'anschluss') { ctx.arc(m, m, r, 0, Math.PI * 2); ctx.stroke(); ctx.beginPath(); ctx.arc(m, m, 9, 0, Math.PI * 2); ctx.fillStyle = HOLO.cyan; ctx.fill(); }
    else if (art === 'ecke') { ctx.rect(m - r, m - r, 2 * r, 2 * r); ctx.stroke(); }
    else if (art === 'mitte') { ctx.moveTo(m, m - r); ctx.lineTo(m + r, m + r * 0.8); ctx.lineTo(m - r, m + r * 0.8); ctx.closePath(); ctx.stroke(); }
    else if (art === 'kante' || art === 'achse') { ctx.moveTo(m - r, m - r); ctx.lineTo(m + r, m + r); ctx.moveTo(m + r, m - r); ctx.lineTo(m - r, m + r); ctx.stroke(); }
    else if (art === 'flaeche') { ctx.moveTo(m, m - r); ctx.lineTo(m + r, m); ctx.lineTo(m, m + r); ctx.lineTo(m - r, m); ctx.closePath(); ctx.stroke(); }
    else { ctx.arc(m, m, 14, 0, Math.PI * 2); ctx.stroke(); }
  }
  if (tex) tex.colorSpace = THREE.SRGBColorSpace;
  const mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, depthWrite: false });
  const sprite = new THREE.Sprite(mat);
  sprite.scale.set(0.05, 0.05, 1);
  sprite.renderOrder = 1001;
  return { sprite, freigeben() { tex?.dispose(); mat.dispose(); } };
}

/** Leuchtende Linie (Cyan) zwischen zwei Punkten; setzen(a, b) verschiebt sie. */
export function holoLinie() {
  const g = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]);
  const m = new THREE.LineBasicMaterial({ color: 0x38e8ff, transparent: true, opacity: 0.95, depthTest: false });
  const l = new THREE.Line(g, m);
  l.renderOrder = 999; l.frustumCulled = false;
  return {
    linie: l,
    setzen(a, b) {
      const p = g.attributes.position;
      p.setXYZ(0, a.x, a.y, a.z); p.setXYZ(1, b.x, b.y, b.z); p.needsUpdate = true;
      g.computeBoundingSphere();
    },
    freigeben() { g.dispose(); m.dispose(); },
  };
}
