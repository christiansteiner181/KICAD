// Prüfansicht (Christian 02.11.2026: «programm integrierte PDF anzeige» – «Pläne prüfen und markieren»): eine Ansicht der Art «pruefung» zeigt ein
// PDF (Planblatt oder PDF-Unterlage, Eigenschaft dokument) als Seitenbild des Kerns (/api/pruefung/seite) mit einer Zeichenebene darüber.
// Werkzeuge: Wolke, Rechteck, Pfeil, Linie, Text, Haken in wählbarer Farbe; jede Markierung ist ein Element der Klasse Pruefmarke (element_anlegen),
// Status offen/erledigt und Text über element_aendern, Löschen über element_loeschen – rückgängig, für alle sichtbar. Rechts die Liste aller
// Markierungen des Dokuments (offen zuerst); Antippen springt zur Seite und wählt die Markierung.
// Koordinaten der Markierungen: PDF-Punkte (pt), Ursprung unten links der Seite, y nach oben (wie im Kern). Die Zeichenebene ist ein SVG mit
// viewBox in pt, y nach unten – umgerechnet mit Y(y) = Seitenhöhe − y.
// Bedienen: Mausrad zoomt, Ziehen mit «Auswählen» bzw. mittlerer Maustaste verschiebt; Esc bricht ein Werkzeug ab.

import { zustand, derKlasse, element, auswaehlen } from '../zustand.js';
import { sende, ergebnisMelden } from '../oberflaeche/befehle.js';
import { esc } from './hilfen.js';

const FARBEN = [['#d0312d', 'Rot'], ['#e08a00', 'Orange'], ['#2e7d32', 'Grün'], ['#2c6baa', 'Blau']];
const WERKZEUGE = [['auswahl', '↖ Auswählen'], ['wolke', 'Wolke'], ['rechteck', 'Rechteck'], ['pfeil', 'Pfeil'], ['linie', 'Linie'], ['text', 'Text'], ['haken', '✓ Haken']];
const FORMTEXT = { wolke: 'Wolke', rechteck: 'Rechteck', pfeil: 'Pfeil', linie: 'Linie', text: 'Text', haken: 'Haken' };
const DPI = 110;

function erstellen(behaelter, ansichtElement) {
  let ansicht = ansichtElement;
  const dokument = () => ansicht.werte?.dokument;
  let info = null, seite = 1, werkzeug = 'auswahl', farbe = FARBEN[0][0], gewaehlt = null;
  let sicht = { s: 1, x: 0, y: 0 };
  let zerstoert = false;

  behaelter.innerHTML = `<div class="pr-wurzel">
    <div class="pr-leiste">
      ${WERKZEUGE.map(([w, t]) => `<button class="pr-w" data-w="${w}">${t}</button>`).join('')}
      <select class="pr-farbe" title="Farbe">${FARBEN.map(([f, n]) => `<option value="${f}">${n}</option>`).join('')}</select>
      <span class="pr-trenner"></span>
      <button class="pr-zurueck" title="Vorherige Seite">‹</button><span class="pr-seite">–</span><button class="pr-vor" title="Nächste Seite">›</button>
      <button class="pr-einpassen">Einpassen</button>
      <span class="pr-titel"></span>
    </div>
    <div class="pr-haupt">
      <div class="pr-flaeche"><div class="pr-blatt"><img class="pr-bild" alt="" draggable="false"><svg class="pr-ebene" xmlns="http://www.w3.org/2000/svg"></svg></div>
        <div class="pr-hinweis" hidden></div></div>
      <aside class="pr-liste"></aside>
    </div>
  </div>`;
  const q = (s) => behaelter.querySelector(s);
  const flaeche = q('.pr-flaeche'), blatt = q('.pr-blatt'), bild = q('.pr-bild'), ebene = q('.pr-ebene');

  // ---------------------------------------------------------------- Laden
  async function laden() {
    const hinweis = q('.pr-hinweis');
    if (!dokument()) { hinweis.hidden = false; hinweis.textContent = 'Dieser Prüfansicht fehlt das Dokument (Eigenschaft dokument).'; return; }
    try {
      const r = await fetch(`/api/pruefung/info?dokument=${encodeURIComponent(dokument())}`, { cache: 'no-store' });
      const j = await r.json();
      if (!r.ok) throw new Error(j.fehler || `Fehler ${r.status}`);
      info = j;
      q('.pr-titel').textContent = `${dokument()} · ${info.titel}`;
      seiteZeigen(Math.min(seite, info.seiten));
    } catch (e) { hinweis.hidden = false; hinweis.textContent = `Das PDF lässt sich nicht laden: ${e.message}`; }
  }
  function seiteZeigen(n) {
    if (!info) return;
    seite = Math.max(1, Math.min(info.seiten, n));
    const m = info.masse[seite - 1];
    q('.pr-seite').textContent = `${seite} / ${info.seiten}`;
    const k = DPI / 72;
    blatt.style.width = `${m.breite * k}px`; blatt.style.height = `${m.hoehe * k}px`;
    ebene.setAttribute('viewBox', `0 0 ${m.breite} ${m.hoehe}`);
    bild.src = `/api/pruefung/seite?dokument=${encodeURIComponent(dokument())}&seite=${seite}&dpi=${DPI}&s=${encodeURIComponent(zustand.stand?.pruefsumme ?? '')}`;
    einpassen();
    zeichnen();
  }

  // ---------------------------------------------------------------- Zoom und Verschieben
  function setzen() { blatt.style.transform = `translate(${sicht.x}px,${sicht.y}px) scale(${sicht.s})`; }
  function einpassen() {
    if (!info) return;
    const r = flaeche.getBoundingClientRect(); const b = blatt.offsetWidth || 1, h = blatt.offsetHeight || 1;
    if (r.width < 2) return;
    sicht.s = Math.min(r.width / b, r.height / h) * 0.96; sicht.x = (r.width - b * sicht.s) / 2; sicht.y = (r.height - h * sicht.s) / 2; setzen();
  }
  flaeche.addEventListener('wheel', (ev) => {
    ev.preventDefault();
    const r = flaeche.getBoundingClientRect(), px = ev.clientX - r.left, py = ev.clientY - r.top, f = Math.exp(-ev.deltaY * 0.0015);
    sicht.x = px - (px - sicht.x) * f; sicht.y = py - (py - sicht.y) * f; sicht.s *= f; setzen();
  }, { passive: false });

  // Bildschirm → pt (y nach oben)
  function pt(ev) {
    const m = ebene.getScreenCTM(); if (!m || !info) return null;
    const p = new DOMPoint(ev.clientX, ev.clientY).matrixTransform(m.inverse());
    return [Math.round(p.x * 10) / 10, Math.round((info.masse[seite - 1].hoehe - p.y) * 10) / 10];
  }
  const Y = (y) => info.masse[seite - 1].hoehe - y;

  // ---------------------------------------------------------------- Zeichnen der Markierungen
  const marken = () => derKlasse('Pruefmarke').filter((e) => e.werte?.dokument === dokument());
  function wolkePfad(x0, y0, x1, y1) {
    const ax = Math.min(x0, x1), bx = Math.max(x0, x1), ay = Math.min(y0, y1), by = Math.max(y0, y1);
    const bogen = Math.max(4, Math.min(16, Math.min(bx - ax, by - ay) / 4));
    let d = `M${ax},${ay}`;
    const kante = (xa, ya, xb, yb) => {
      const l = Math.hypot(xb - xa, yb - ya), n = Math.max(1, Math.round(l / bogen));
      for (let i = 1; i <= n; i++) { const x = xa + (xb - xa) * i / n, y = ya + (yb - ya) * i / n; d += ` A${l / n / 2},${l / n / 2} 0 0 1 ${x},${y}`; }
    };
    kante(ax, ay, bx, ay); kante(bx, ay, bx, by); kante(bx, by, ax, by); kante(ax, by, ax, ay);
    return d;
  }
  function form(w, pkte, f, text, extra = '') {
    const s = `stroke="${f}" stroke-width="1.6" fill="none" vector-effect="non-scaling-stroke"${extra}`;
    const p = pkte.map(([x, y]) => [x, Y(y)]);
    switch (w) {
      case 'wolke': return `<path d="${wolkePfad(p[0][0], p[0][1], p[1][0], p[1][1])}" ${s}/>`;
      case 'rechteck': return `<rect x="${Math.min(p[0][0], p[1][0])}" y="${Math.min(p[0][1], p[1][1])}" width="${Math.abs(p[1][0] - p[0][0])}" height="${Math.abs(p[1][1] - p[0][1])}" ${s}/>`;
      case 'linie': return `<line x1="${p[0][0]}" y1="${p[0][1]}" x2="${p[1][0]}" y2="${p[1][1]}" ${s}/>`;
      case 'pfeil': {
        const [a, b] = p, l = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1, ux = (b[0] - a[0]) / l, uy = (b[1] - a[1]) / l, k = Math.min(10, l / 3);
        return `<line x1="${a[0]}" y1="${a[1]}" x2="${b[0]}" y2="${b[1]}" ${s}/><polygon points="${b[0]},${b[1]} ${b[0] - ux * k - uy * k / 2},${b[1] - uy * k + ux * k / 2} ${b[0] - ux * k + uy * k / 2},${b[1] - uy * k - ux * k / 2}" fill="${f}" stroke="none"${extra}/>`;
      }
      case 'haken': { const [x, y] = p[0]; return `<path d="M${x - 6},${y - 1} L${x - 1},${y + 5} L${x + 8},${y - 7}" ${s.replace('1.6', '2.6')}/>`; }
      case 'text': { const [x, y] = p[0]; return `<text x="${x}" y="${y}" font-size="9" font-family="Arial" fill="${f}" stroke="none"${extra}>${esc(text || '')}</text>`; }
      default: return '';
    }
  }
  function zeichnen() {
    if (!info) return;
    let html = '';
    for (const m of marken().filter((e) => (e.werte?.seite ?? 1) === seite)) {
      const erl = m.werte?.status === 'erledigt';
      const f = erl ? '#8a8a8a' : (m.werte?.farbe || FARBEN[0][0]);
      const extra = ` data-id="${m.id}"${erl ? ' opacity="0.55"' : ''}`;
      html += `<g class="pr-marke${m.id === gewaehlt ? ' gewaehlt' : ''}" data-id="${m.id}">${form(m.werte?.form, m.werte?.punkte ?? [], f, m.werte?.text, extra)}`;
      // Bemerkung neben der Markierung (nicht beim Text selbst)
      if (m.werte?.text && m.werte?.form !== 'text') {
        const p = m.werte.punkte[m.werte.punkte.length - 1];
        html += `<text x="${p[0] + 4}" y="${Y(p[1]) - 4}" font-size="8" font-family="Arial" fill="${f}">${esc(m.werte.text)}</text>`;
      }
      html += '</g>';
    }
    ebene.innerHTML = html + '<g class="pr-vorschau"></g>';
    liste();
  }

  // ---------------------------------------------------------------- Liste
  function liste() {
    const alle = marken().sort((a, b) => (a.werte?.status === 'erledigt') - (b.werte?.status === 'erledigt') || (a.werte?.seite ?? 1) - (b.werte?.seite ?? 1) || a.kennzeichen.localeCompare(b.kennzeichen, 'de', { numeric: true }));
    const offen = alle.filter((m) => m.werte?.status !== 'erledigt').length;
    q('.pr-liste').innerHTML = `<div class="pr-liste-kopf"><b>Markierungen</b> ${offen} offen · ${alle.length - offen} erledigt</div>` + (alle.length === 0
      ? '<div class="leise" style="padding:8px">Noch keine. Werkzeug wählen und auf dem Plan zeichnen.</div>'
      : alle.map((m) => `<div class="pr-zeile${m.id === gewaehlt ? ' gewaehlt' : ''}${m.werte?.status === 'erledigt' ? ' erledigt' : ''}" data-id="${m.id}">
          <input type="checkbox" class="pr-erledigt" title="erledigt"${m.werte?.status === 'erledigt' ? ' checked' : ''}>
          <span class="pr-punkt" style="background:${esc(m.werte?.farbe || FARBEN[0][0])}"></span>
          <span class="pr-text">${esc(m.werte?.text || FORMTEXT[m.werte?.form] || '')}</span>
          <span class="leise">S. ${m.werte?.seite ?? 1} · ${esc(m.kennzeichen)}</span>
          <button class="pr-bearbeiten" title="Bemerkung ändern">✎</button><button class="pr-loeschen" title="Löschen">✕</button></div>`).join(''));
  }
  q('.pr-liste').addEventListener('click', async (ev) => {
    const z = ev.target.closest('.pr-zeile'); if (!z) return;
    const m = element(z.dataset.id); if (!m) return;
    if (ev.target.closest('.pr-erledigt')) {
      ergebnisMelden(await sende('element_aendern', { element: m.kennzeichen, eigenschaften: { status: ev.target.checked ? 'erledigt' : 'offen' } }));
      return;
    }
    if (ev.target.closest('.pr-loeschen')) { ergebnisMelden(await sende('element_loeschen', { element: m.kennzeichen })); return; }
    if (ev.target.closest('.pr-bearbeiten')) {
      const t = window.prompt('Bemerkung', m.werte?.text ?? '');
      if (t !== null) ergebnisMelden(await sende('element_aendern', { element: m.kennzeichen, eigenschaften: { text: t.trim() || null } }));
      return;
    }
    gewaehlt = m.id; auswaehlen(m.id);
    if ((m.werte?.seite ?? 1) !== seite) seiteZeigen(m.werte?.seite ?? 1); else zeichnen();
  });

  // ---------------------------------------------------------------- Werkzeuge
  function werkzeugSetzen(w) {
    werkzeug = w;
    for (const b of behaelter.querySelectorAll('.pr-w')) b.classList.toggle('aktiv', b.dataset.w === w);
    flaeche.style.cursor = w === 'auswahl' ? 'grab' : 'crosshair';
  }
  for (const b of behaelter.querySelectorAll('.pr-w')) b.addEventListener('click', () => werkzeugSetzen(b.dataset.w));
  q('.pr-farbe').addEventListener('change', (e) => { farbe = e.target.value; });
  q('.pr-zurueck').addEventListener('click', () => seiteZeigen(seite - 1));
  q('.pr-vor').addEventListener('click', () => seiteZeigen(seite + 1));
  q('.pr-einpassen').addEventListener('click', einpassen);
  werkzeugSetzen('auswahl');

  async function anlegen(f, punkte, text) {
    const eig = { dokument: dokument(), seite, form: f, punkte, farbe, status: 'offen' };
    if (text) eig.text = text;
    ergebnisMelden(await sende('element_anlegen', { klasse: 'Pruefmarke', eigenschaften: eig }));
  }

  let zug = null;   // { art: 'schieben' | 'form', start, sicht, p0 }
  flaeche.addEventListener('pointerdown', (ev) => {
    if (!info) return;
    const schieben = werkzeug === 'auswahl' || ev.button === 1;
    if (schieben) {
      const treffer = ev.target.closest?.('[data-id]');
      if (treffer && ev.button === 0) { gewaehlt = treffer.dataset.id; auswaehlen(gewaehlt); zeichnen(); return; }
      zug = { art: 'schieben', start: [ev.clientX, ev.clientY], sicht: { ...sicht } };
    } else if (ev.button === 0) {
      const p = pt(ev); if (!p) return;
      if (werkzeug === 'text' || werkzeug === 'haken') {
        const t = window.prompt(werkzeug === 'text' ? 'Text' : 'Bemerkung zum Haken (leer lassen geht)', '');
        if (t === null || (werkzeug === 'text' && !t.trim())) return;
        anlegen(werkzeug, [p], t.trim());
        return;
      }
      zug = { art: 'form', p0: p };
    }
    flaeche.setPointerCapture(ev.pointerId);
  });
  flaeche.addEventListener('pointermove', (ev) => {
    if (!zug) return;
    if (zug.art === 'schieben') { sicht.x = zug.sicht.x + ev.clientX - zug.start[0]; sicht.y = zug.sicht.y + ev.clientY - zug.start[1]; setzen(); return; }
    const p = pt(ev); if (!p) return;
    ebene.querySelector('.pr-vorschau').innerHTML = form(werkzeug, [zug.p0, p], farbe, '', ' stroke-dasharray="4 3"');
  });
  flaeche.addEventListener('pointerup', async (ev) => {
    const z = zug; zug = null; if (!z || z.art !== 'form') return;
    const p = pt(ev); ebene.querySelector('.pr-vorschau').innerHTML = '';
    if (!p || Math.hypot(p[0] - z.p0[0], p[1] - z.p0[1]) < 3) return;
    const t = window.prompt('Bemerkung (freiwillig)', '');
    if (t === null) return;
    anlegen(werkzeug, [z.p0, p], t.trim());
  });
  const taste = (ev) => { if (ev.key === 'Escape') { zug = null; ebene.querySelector('.pr-vorschau')?.replaceChildren(); werkzeugSetzen('auswahl'); } };
  behaelter.addEventListener('keydown', taste);

  laden();
  return {
    aktualisieren() { if (!zerstoert) zeichnen(); },
    ansichtGeaendert(a) { const neu = a.werte?.dokument !== dokument(); ansicht = a; if (neu) laden(); },
    auswahlGeaendert(id) { if (element(id)?.klasse === 'Pruefmarke') { gewaehlt = id; zeichnen(); } },
    zoomAlles: einpassen,
    zerstoeren() { zerstoert = true; behaelter.removeEventListener('keydown', taste); },
  };
}

export default { erstellen };
