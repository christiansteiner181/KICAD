// Schema-Ansicht (Modul 3, Teil C2): zeichnet die Plangrafik einer Ansicht mit art schema (Papier-mm, Massstab 1, y nach oben) mit dem
// Plangrafik-Zeichner der Plan-Ansichten (plangrafik.js) und macht sie bedienbar. Das Schema ist eine Sicht auf dasselbe Modell:
// die Anordnung rechnet der Kern (Abfrage «schema», GET /api/plangrafik/{kz}); hier steht nur Zeichnen, Zoomen, Auswählen und die
// Bedienung. Geändert wird ausschliesslich mit Befehlen des Kerns (schema_lage, schema_anordnen, verbinden, trennen, element_anlegen);
// das Ergebnis kommt als Ereignis «modell» zurück und die Ansicht zeichnet sich neu (entprellt, Zoom und Auswahl bleiben).
//
// Bedienung: Klick wählt (Knoten, Linienteil; die Auswahl gilt für alle Ansichten), Ziehen eines Knotens setzt seine Hand-Lage
// (schema_lage, Papier-mm gerundet, rastet auf Stranglinien und Achsen anderer Knoten ein; die Linien folgen nach der Antwort des
// Kerns), Ziehen auf leerer Fläche, mittlere Taste oder Leertaste+Ziehen verschiebt, Mausrad zoomt, Rechtsklick öffnet das
// Kontextmenü. Werkzeuge in der Leiste oben links: Verbinden, Trennen, Element einfügen (Modus; Esc beendet), Neu anordnen.
// Es gibt keinen Massstab und keinen Massstabsbalken: ein Schema ist nicht massstäblich.
//
// Ansichtsmodul-Vertrag (fenster.js): erstellen(behaelter, ansicht) → { aktualisieren, ansichtGeaendert, auswahlGeaendert, zoomAlles,
// zerstoeren } und für die Aktionen: istSchema, kennzeichen, modus, setzeModus, verbindenVon, neuAnordnen, zeigen, daten.

import { zustand, element, auswaehlen, abonnieren } from '../zustand.js';
import { holen, abfrage } from '../api.js';
import { esc } from './hilfen.js';
import { planSvg, begrenzungVon } from './plangrafik.js';
import * as L from './schema-logik.js';
import * as T from './schema-text.js';
import { kontextmenue, meldung } from '../oberflaeche/dialoge.js';
import { ausfuehren, hinweisSetzen } from '../oberflaeche/register.js';
import * as bearbeiten from '../oberflaeche/schema-bearbeiten.js';

const SVGNS = 'http://www.w3.org/2000/svg';
const KLICK_TOLERANZ = 4;      // px, darunter gilt eine Mausbewegung noch als Klick
const ENTPRELLUNG = 150;       // ms zwischen Ereignis und Neuholen
const FREMD_DAUER = 1500;
const MIN_PX = 1;
const MIN_BREITE = 20;         // mm Papier: weiter hinein zoomt man nicht
const MAX_BREITE = 5e5;
const OBEN_PX = 56;            // Platz über dem Schema bei «Alles zeigen» (Leiste und Banner)
const warte = (ms) => new Promise((r) => setTimeout(r, ms));

/** Werkzeuge der Leiste: Id, Beschriftung, Tooltip. */
const WERKZEUGE = [
  ['auswahl', 'Ändern', 'Auswählen und Knoten verschieben (Esc)'],
  ['verbinden', 'Verbinden', 'Zwei Knoten nacheinander anklicken'],
  ['trennen', 'Trennen', 'Eine Linie oder einen Knoten anklicken'],
  ['einfuegen', 'Element einfügen …', 'Klasse oder Familie wählen, dann in die Zelle klicken'],
];

function erstellen(behaelter, ansichtElement) {
  let ansicht = ansichtElement;
  let plan = null;               // Plangrafik des Kerns
  let daten = null;              // Antwort der Abfrage «schema»
  let vb = null;                 // { x, y, w, h } in SVG-Koordinaten (y = −y des Schemas)
  let gewaehlt = null;
  let benutzt = false;           // Zoom oder Verschieben durch den Benutzer: dann bleibt der Ausschnitt
  let nochNichtGezeigt = true;
  let letzteBreite = 0;
  let ladeNr = 0;
  let ladeTimer = null;
  let zerstoert = false;
  let modus = 'auswahl';         // auswahl | verbinden | trennen | einfuegen
  let werk = {};                 // Zustand des Werkzeugs: { erster } beim Verbinden, { wahl } beim Einfügen
  let wunsch = null;             // Id, auf die gezoomt werden soll, sobald die Daten da sind
  let bannerOffen = false;
  let beschaeftigt = false;
  const fremdOffen = new Set();
  const timer = new Set();

  behaelter.innerHTML = `<div class="gr-wurzel sc-wurzel">
      <svg class="gr-svg sc-svg" xmlns="${SVGNS}" preserveAspectRatio="xMidYMid meet" tabindex="-1" data-art="schema">
        <g class="gr-welt" transform="scale(1 -1)"><g class="gr-plan"></g><g class="sc-ueber"></g><g class="sc-werk"></g></g>
      </svg>
      <div class="sc-leiste" role="toolbar" aria-label="Schema bearbeiten">
        ${WERKZEUGE.map(([id, text, tip]) => `<button type="button" class="sc-knopf" data-werkzeug="${id}" title="${esc(tip)}">${esc(text)}</button>`).join('')}
        <span class="sc-trenner"></span>
        <button type="button" class="sc-knopf" data-aktion="neu-anordnen" title="Hand-Lagen aufräumen oder alle entfernen (schema_anordnen)">Neu anordnen</button>
        <button type="button" class="sc-knopf" data-aktion="zoom-alles" title="Das ganze Schema zeigen">Alles zeigen</button>
        <span class="sc-status" aria-live="polite"></span>
      </div>
      <div class="sc-banner" hidden></div>
      <div class="gr-tip" hidden></div>
      <div class="gr-leer" hidden></div>
    </div>`;
  const wurzel = behaelter.querySelector('.sc-wurzel');
  const svg = wurzel.querySelector('.gr-svg');
  const planGruppe = svg.querySelector('.gr-plan');
  const ueber = svg.querySelector('.sc-ueber');
  const werkSchicht = svg.querySelector('.sc-werk');
  const leiste = wurzel.querySelector('.sc-leiste');
  const statusFeld = wurzel.querySelector('.sc-status');
  const banner = wurzel.querySelector('.sc-banner');
  const tip = wurzel.querySelector('.gr-tip');
  const leer = wurzel.querySelector('.gr-leer');

  const kz = () => ansicht?.kennzeichen;
  const groesse = () => ({ W: svg.clientWidth, H: svg.clientHeight });
  const pxJeMm = () => { const { W } = groesse(); return vb && W > 0 ? W / vb.w : 1; };
  const idVon = (ziel) => ziel?.closest?.('[data-id]')?.getAttribute('data-id') ?? null;
  const neu = (name, attr = {}) => { const e = document.createElementNS(SVGNS, name); for (const [k, v] of Object.entries(attr)) e.setAttribute(k, String(v)); return e; };

  // ---------- Daten holen und zeichnen ----------
  async function laden() {
    const nr = ++ladeNr;
    const kennz = kz();
    if (!kennz || zerstoert) return;
    try {
      let p = null, d = null;
      // Plangrafik und Abfrage kommen in getrennten Anfragen: stimmt der Stand nicht überein, ist dazwischen etwas geändert worden
      for (let versuch = 0; versuch < 3; versuch++) {
        [p, d] = await Promise.all([holen('/api/plangrafik/' + encodeURIComponent(kennz)), abfrage('schema', { ansicht: kennz })]);
        if (nr !== ladeNr || zerstoert) return;
        if (!p?.stand || !d?.stand || p.stand === d.stand) break;
        await warte(60);
      }
      const warLeer = !plan || (plan.teile ?? []).length === 0;
      plan = p; daten = d;
      planGruppe.innerHTML = planSvg(plan);
      svg.style.setProperty('--m', '1');
      const hat = (plan.teile ?? []).length > 0;
      leer.hidden = hat;
      if (!hat) leer.innerHTML = 'Das Schema ist leer.<br>Es zeigt Netzelemente (Endgeräte, Geräte, Armaturen, Formteile, Leitungen) der gewählten Gewerke und Systeme – Filter der Ansicht prüfen.';
      ueberZeichnen();
      bannerZeichnen();
      hervorheben(gewaehlt);
      fremdAnwenden();
      if (nochNichtGezeigt || !vb || (warLeer && hat && !benutzt)) { nochNichtGezeigt = false; zoomAlles(); } else vbAnwenden();
      wunschAnwenden();
      werkZeichnen();
    } catch (e) {
      if (nr !== ladeNr || zerstoert) return;
      console.error('Schema-Ansicht: Daten nicht geladen', e);
      plan = null; daten = null;
      planGruppe.replaceChildren();
      ueber.replaceChildren();
      leer.hidden = false;
      leer.innerHTML = `Das Schema konnte nicht geladen werden:<br>${esc(e.message ?? e)}${e.vorschlag ? '<br>' + esc(e.vorschlag) : ''}`;
      if (!vb) zoomAlles();
    }
  }

  function planen(ms = ENTPRELLUNG) { clearTimeout(ladeTimer); ladeTimer = setTimeout(laden, ms); }

  /** Marken, die der Kern nicht zeichnet: ein kleiner Punkt an Knoten mit Hand-Lage. */
  function ueberZeichnen() {
    ueber.replaceChildren();
    for (const k of daten?.knoten ?? []) {
      if (!k.manuell) continue;
      ueber.appendChild(neu('circle', { class: 'sc-hand', cx: k.x + L.KNOTEN_HALB_X - 0.8, cy: k.y + L.KNOTEN_HALB_Y - 0.4, r: 0.75 }));
    }
  }

  function bannerZeichnen() {
    const b = L.hinweisBanner(daten);
    banner.hidden = b.anzahl === 0;
    banner.innerHTML = T.bannerHtml(b, bannerOffen);
    banner.querySelector('.sc-banner-kopf')?.addEventListener('click', () => { bannerOffen = !bannerOffen; bannerZeichnen(); });
    const np = L.nichtPlatziert(daten, element);
    const teile = [];
    if (np.knoten.length + np.linien > 0) teile.push(`${np.knoten.length + np.linien} nicht platziert`);
    const hand = (daten?.knoten ?? []).filter((k) => k.manuell).length;
    if (hand > 0) teile.push(`${hand} von Hand angeordnet`);
    statusFeld.textContent = teile.join(' · ');
    statusFeld.title = 'Nicht platzierte Elemente stehen nur im Schema (gestrichelt); Hand-Lagen sind mit einem Punkt markiert.';
  }

  function fremdAnwenden() {
    for (const id of fremdOffen) knotenVon(id).forEach((g) => {
      g.classList.add('fremd');
      const t = setTimeout(() => { g.classList.remove('fremd'); timer.delete(t); }, FREMD_DAUER);
      timer.add(t);
    });
    fremdOffen.clear();
  }

  const knotenVon = (id) => (id ? [...planGruppe.querySelectorAll('[data-id]')].filter((g) => g.getAttribute('data-id') === id) : []);

  function hervorheben(id) {
    for (const g of planGruppe.querySelectorAll('.gewaehlt')) g.classList.remove('gewaehlt');
    gewaehlt = id ?? null;
    for (const g of knotenVon(gewaehlt)) g.classList.add('gewaehlt');
  }

  // ---------- Ansichtsfenster (viewBox) ----------
  function vbAnwenden() {
    if (!vb) return;
    svg.setAttribute('viewBox', `${vb.x} ${vb.y} ${vb.w} ${vb.h}`);
    const { W } = groesse();
    if (W > 1) svg.style.setProperty('--minpx', `${MIN_PX * vb.w / W}px`);
  }

  /** Passt ein Rechteck {minx, miny, maxx, maxy} (Papier-mm) ein; mindestens «mindest» mm breit. */
  function einpassen(b, mindest = 0) {
    const { W, H } = groesse();
    if (W < 2 || H < 2) { vb = null; return; }
    if (!b) b = { minx: 0, miny: 0, maxx: 200, maxy: 150 };
    const rand = Math.max(b.maxx - b.minx, b.maxy - b.miny, 100) * 0.04;
    const x0 = b.minx - rand, x1 = b.maxx + rand, y0 = b.miny - rand, y1 = b.maxy + rand;
    let w = Math.max(x1 - x0, mindest), h = y1 - y0;
    const cx = (x0 + x1) / 2, cy = -(y0 + y1) / 2;
    if (w / h < W / H) w = Math.max(h * W / H, w); else h = w * H / W;
    vb = { x: cx - w / 2, y: cy - h / 2, w, h };
    letzteBreite = W;
    vbAnwenden();
  }

  /** Alles zeigen; oben bleibt Platz für Leiste und Banner (sonst deckten sie die Strangköpfe zu). */
  function zoomAlles() {
    benutzt = false;
    const b = begrenzungVon(plan);
    einpassen(b);
    if (b && vb) { const px = pxJeMm(); einpassen({ ...b, maxy: b.maxy + OBEN_PX / px }); }
  }

  function groesseGeaendert() {
    const { W, H } = groesse();
    if (W < 2 || H < 2) return;
    if (wunsch && daten) { wunschAnwenden(); return; }
    if (!vb || !benutzt) { zoomAlles(); return; }
    const px = vb.w / (letzteBreite || W);
    const cx = vb.x + vb.w / 2, cy = vb.y + vb.h / 2;
    vb.w = px * W; vb.h = px * H;
    vb.x = cx - vb.w / 2; vb.y = cy - vb.h / 2;
    letzteBreite = W;
    vbAnwenden();
  }

  function zuSvg(clientX, clientY) {
    const r = svg.getBoundingClientRect();
    return { x: vb.x + ((clientX - r.left) / r.width) * vb.w, y: vb.y + ((clientY - r.top) / r.height) * vb.h };
  }
  /** Bildschirm → Papier-mm des Schemas (y nach oben). */
  function zuPapier(clientX, clientY) { const p = zuSvg(clientX, clientY); return { x: p.x, y: -p.y }; }

  function zoomen(faktor, clientX, clientY) {
    if (!vb) return;
    benutzt = true;
    const r = svg.getBoundingClientRect();
    const p = zuSvg(clientX, clientY);
    const w = Math.min(Math.max(vb.w / faktor, MIN_BREITE), MAX_BREITE);
    const f = vb.w / w, h = vb.h / f;
    vb = { x: p.x - ((clientX - r.left) / r.width) * w, y: p.y - ((clientY - r.top) / r.height) * h, w, h };
    vbAnwenden();
  }

  function wunschAnwenden() {
    if (!wunsch || !daten) return;
    const { W, H } = groesse();
    if (W < 2 || H < 2) return;   // Ansicht noch verborgen: der Wunsch bleibt, bis sie eine Grösse hat
    const id = wunsch;
    const b = L.umrandung(daten, id);
    wunsch = null;
    if (!b) return;
    benutzt = true;
    einpassen(b, 90);
    hervorheben(id);
  }

  // ---------- Werkzeuge: Zustand, Hinweis, Überlagerung ----------
  function leisteAnzeigen() {
    for (const k of leiste.querySelectorAll('[data-werkzeug]')) k.classList.toggle('gedrueckt', k.dataset.werkzeug === modus);
    svg.dataset.modus = modus;
  }

  function hinweisAktualisieren() {
    if (modus === 'verbinden') hinweisSetzen(werk.erster ? T.TOOL_HINWEIS.verbinden1(werk.erster.kennzeichen) : T.TOOL_HINWEIS.verbinden0);
    else if (modus === 'trennen') hinweisSetzen(T.TOOL_HINWEIS.trennen);
    else if (modus === 'einfuegen') hinweisSetzen(T.TOOL_HINWEIS.einfuegen(werk.wahl?.titel ?? ''));
    else hinweisSetzen('');
  }

  function setzeModus(m, opt = {}) {
    if (zerstoert) return;
    modus = ['verbinden', 'trennen', 'einfuegen'].includes(m) ? m : 'auswahl';
    werk = {};
    if (modus === 'verbinden' && opt.von) werk.erster = (daten?.knoten ?? []).find((k) => k.element === opt.von) ?? null;
    if (modus === 'einfuegen') werk.wahl = opt.wahl ?? null;
    leisteAnzeigen();
    hinweisAktualisieren();
    werkZeichnen();
  }

  /** Überlagerung des Werkzeugs (Markierung des ersten Knotens, Gummiband, Zielzelle). */
  function werkZeichnen(maus = null) {
    werkSchicht.replaceChildren();
    if (modus === 'verbinden' && werk.erster) {
      const k = (daten?.knoten ?? []).find((x) => x.element === werk.erster.element) ?? werk.erster;
      werkSchicht.appendChild(neu('rect', { class: 'sc-markiert', x: k.x - L.KNOTEN_HALB_X - 0.8, y: k.y - L.KNOTEN_HALB_Y - 0.8, width: 2 * L.KNOTEN_HALB_X + 1.6, height: 2 * L.KNOTEN_HALB_Y + 1.6, rx: 0.8 }));
      if (maus) werkSchicht.appendChild(neu('line', { class: 'sc-gummi', x1: k.x, y1: k.y, x2: maus.x, y2: maus.y }));
    }
    if (modus === 'einfuegen' && maus) {
      const z = L.zelleBei(daten, maus.x, maus.y);
      if (z) werkSchicht.appendChild(neu('rect', { class: 'sc-zelle', x: z.spalte.x, y: z.zeile.y, width: z.spalte.breite, height: z.zeile.hoehe }));
      werkSchicht.appendChild(neu('rect', { class: 'sc-geist', x: maus.x - L.KNOTEN_HALB_X, y: maus.y - L.KNOTEN_HALB_Y, width: 2 * L.KNOTEN_HALB_X, height: 2 * L.KNOTEN_HALB_Y }));
      hinweisSetzen(`${T.TOOL_HINWEIS.einfuegen(werk.wahl?.titel ?? '')} · ${L.zelleText(z)}`);
    }
  }

  // ---------- Bedienung ----------
  let druck = null;            // { t, p, x, y, bewegt, zieht, taste }
  let schiebt = false;
  let leertaste = false;
  let imFenster = false;
  let letzteMitte = 0;

  function tooltipZeigen(e, t) {
    let text = '';
    if (t?.art === 'knoten') text = T.knotenTip(t.knoten);
    else if (t?.art === 'linie') text = T.linieTip(t.linie, t.element ? element(t.element) : null);
    else if (t?.art === 'sonst') { const el = element(t.id); text = el ? [el.kennzeichen, el.klasse].join(' · ') : ''; }
    if (!text) { tip.hidden = true; return; }
    tip.textContent = text;
    const r = wurzel.getBoundingClientRect();
    tip.hidden = false;
    tip.style.left = `${Math.min(e.clientX - r.left + 14, r.width - tip.offsetWidth - 4)}px`;
    tip.style.top = `${Math.min(e.clientY - r.top + 16, r.height - tip.offsetHeight - 4)}px`;
  }

  const trefferBei = (e) => (vb && daten ? L.treffer(daten, zuPapier(e.clientX, e.clientY), L.TREFFER_PX / pxJeMm(), idVon(e.target)) : { art: 'leer' });

  function schiebenStart(e) {
    schiebt = true;
    benutzt = true;
    try { svg.setPointerCapture(e.pointerId); } catch { /* egal */ }
    svg.classList.add('schiebt');
  }
  function schiebenEnde(e) {
    if (!schiebt) return;
    schiebt = false;
    svg.classList.remove('schiebt');
    try { svg.releasePointerCapture(e.pointerId); } catch { /* egal */ }
  }

  function beiPointerDown(e) {
    if (!vb || beschaeftigt) return;
    tip.hidden = true;
    if (e.button === 1) {
      e.preventDefault();
      if (letzteMitte > 0 && performance.now() - letzteMitte < 400) { letzteMitte = 0; zoomAlles(); return; }   // Doppelklick mittlere Taste (zählt nur ohne Ziehen)
      letzteMitte = 0;
      druck = { t: { art: 'leer' }, x: e.clientX, y: e.clientY, bewegt: true, taste: 1, weg: 0 };
      schiebenStart(e);
      return;
    }
    if (e.button !== 0) return;
    druck = { t: trefferBei(e), p: zuPapier(e.clientX, e.clientY), x: e.clientX, y: e.clientY, bewegt: false, zieht: false, taste: 0 };
    if (leertaste) { druck.bewegt = true; schiebenStart(e); }
  }

  /** Während des Ziehens: Knoten (nur die Gruppe des Knotens) folgt dem Zeiger; die Linien führt der Kern nach dem Loslassen neu. */
  function ziehenAnzeigen(e) {
    const k = druck.t.knoten;
    const q = zuPapier(e.clientX, e.clientY);
    const ziel = L.lageRunden({ x: k.x + (q.x - druck.p.x), y: k.y + (q.y - druck.p.y) }, { daten, ausser: k.element, toleranz: L.FANG_PX / pxJeMm() });
    druck.ziel = ziel;
    for (const g of knotenVon(k.element)) g.setAttribute('transform', `translate(${ziel.x - k.x} ${ziel.y - k.y})`);
    werkSchicht.replaceChildren();
    const b = vb;
    if (ziel.fangX) werkSchicht.appendChild(neu('line', { class: 'sc-fang', x1: ziel.fangX.wert, y1: -b.y - b.h, x2: ziel.fangX.wert, y2: -b.y }));
    if (ziel.fangY) werkSchicht.appendChild(neu('line', { class: 'sc-fang', x1: b.x, y1: ziel.fangY.wert, x2: b.x + b.w, y2: ziel.fangY.wert }));
    tip.textContent = `${k.kennzeichen}  x ${ziel.x} · y ${ziel.y}${ziel.fangX ? ' · Strang ' + (ziel.fangX.kennzeichen ?? '') : ''}`;
    const r = wurzel.getBoundingClientRect();
    tip.hidden = false;
    tip.style.left = `${Math.min(e.clientX - r.left + 14, r.width - tip.offsetWidth - 4)}px`;
    tip.style.top = `${Math.min(e.clientY - r.top + 16, r.height - tip.offsetHeight - 4)}px`;
  }

  function beiPointerMove(e) {
    if (druck?.taste === 1 || (druck && schiebt)) {
      if (druck.taste === 1) druck.weg += Math.abs(e.clientX - druck.x) + Math.abs(e.clientY - druck.y);
      if (vb) {
        const { W, H } = groesse();
        vb.x -= (e.clientX - druck.x) * vb.w / W; vb.y -= (e.clientY - druck.y) * vb.h / H;
        druck.x = e.clientX; druck.y = e.clientY;
        vbAnwenden();
      }
      return;
    }
    if (druck) {
      const weg = Math.hypot(e.clientX - druck.x, e.clientY - druck.y);
      if (!druck.bewegt && weg > KLICK_TOLERANZ) {
        druck.bewegt = true;
        if (druck.t.art === 'knoten' && modus === 'auswahl') {
          druck.zieht = true;
          try { svg.setPointerCapture(e.pointerId); } catch { /* egal */ }
          svg.classList.add('zieht');
        } else schiebenStart(e);
      }
      if (druck.zieht) ziehenAnzeigen(e);
      return;
    }
    if (!vb) return;
    const t = trefferBei(e);
    svg.classList.toggle('treffer-knoten', t.art === 'knoten' && modus === 'auswahl');
    if (modus === 'verbinden' && werk.erster) werkZeichnen(zuPapier(e.clientX, e.clientY));
    else if (modus === 'einfuegen') werkZeichnen(zuPapier(e.clientX, e.clientY));
    tooltipZeigen(e, t);
  }

  async function beiPointerUp(e) {
    if (e.button === 1 || druck?.taste === 1) {
      if (druck?.taste === 1 && druck.weg <= 4) letzteMitte = performance.now();   // Klick ohne Ziehen: zählt für den Doppelklick
      druck = null;
      schiebenEnde(e);
      return;
    }
    if (!druck) return;
    const d = druck;
    druck = null;
    schiebenEnde(e);
    if (d.zieht) {
      svg.classList.remove('zieht');
      try { svg.releasePointerCapture(e.pointerId); } catch { /* egal */ }
      await ziehenAbschliessen(d);
      return;
    }
    if (d.taste === 0 && !d.bewegt) await klick(d.t, e);
  }

  async function ziehenAbschliessen(d) {
    const k = d.t.knoten;
    const ziel = d.ziel;
    tip.hidden = true;
    werkSchicht.replaceChildren();
    const zurueck = () => { for (const g of knotenVon(k.element)) g.removeAttribute('transform'); };
    if (!ziel || (ziel.x === k.x && ziel.y === k.y)) { zurueck(); return; }
    beschaeftigt = true;
    try {
      const ok = await bearbeiten.lageSetzen(kz(), k.element, ziel.x, ziel.y);
      if (!ok) zurueck();
      else { hinweisSetzen(`Hand-Lage: ${k.kennzeichen} (${ziel.x}; ${ziel.y})`, 3000); planen(0); }
    } finally { beschaeftigt = false; }
  }

  /** Linksklick ohne Bewegung: je nach Werkzeug. */
  async function klick(t, e) {
    if (modus === 'auswahl') {
      auswaehlen(t.art === 'leer' ? null : (t.id ?? null));
      return;
    }
    if (modus === 'verbinden') {
      if (t.art !== 'knoten') { hinweisSetzen('Verbinden: einen Knoten anklicken (Endgerät, Gerät, Armatur oder Formteil).', 3000); return; }
      if (!werk.erster) { werk.erster = t.knoten; auswaehlen(t.knoten.element); hinweisAktualisieren(); werkZeichnen(zuPapier(e.clientX, e.clientY)); return; }
      if (werk.erster.element === t.knoten.element) { werk.erster = null; hinweisAktualisieren(); werkZeichnen(); return; }
      const a = werk.erster;
      werk.erster = null;
      beschaeftigt = true;
      try { await bearbeiten.verbinden(a, t.knoten); } finally { beschaeftigt = false; }
      hinweisAktualisieren();
      werkZeichnen();
      return;
    }
    if (modus === 'trennen') {
      if (t.art !== 'linie' && t.art !== 'knoten') { hinweisSetzen('Trennen: eine Linie oder einen Knoten anklicken.', 3000); return; }
      beschaeftigt = true;
      try { await bearbeiten.trennenKlick(t, e.clientX, e.clientY); } finally { beschaeftigt = false; }
      return;
    }
    if (modus === 'einfuegen') {
      const p = zuPapier(e.clientX, e.clientY);
      const zelle = L.zelleBei(daten, p.x, p.y);
      if (!zelle) { hinweisSetzen('Einfügen: in eine Zelle (Geschoss × Strang) klicken, nicht daneben.', 3000); return; }
      const wahl = werk.wahl;
      if (!wahl) { setzeModus('auswahl'); return; }
      beschaeftigt = true;
      try {
        const lage = L.lageRunden(p, { daten, toleranz: L.FANG_PX / pxJeMm() });
        await bearbeiten.einfuegenAnlegen(kz(), wahl, zelle, lage);
      } finally { beschaeftigt = false; }
      setzeModus('auswahl');
    }
  }

  function beiKontext(e) {
    e.preventDefault();
    if (!vb || !daten) return;
    const t = trefferBei(e);
    const p = zuPapier(e.clientX, e.clientY);
    let eintraege = [];
    if (t.art === 'knoten') {
      auswaehlen(t.knoten.element);
      eintraege = L.knotenMenue({ knoten: t.knoten, element: element(t.knoten.element), verbindungen: zustand.verbindungen });
    } else if (t.art === 'linie') {
      if (t.element) auswaehlen(t.element);
      const optionen = L.trennOptionen(t.linie, zustand.verbindungen, (id) => element(id)?.kennzeichen ?? null, t.nurGeometrie ? null : t.element);
      eintraege = L.linieMenue({ linie: t.linie, elementId: t.element, element: t.element ? element(t.element) : null, optionen });
    } else {
      eintraege = L.leerMenue({ zelle: L.zelleBei(daten, p.x, p.y), hatHandLagen: (daten.knoten ?? []).some((k) => k.manuell) || (daten.verwaisteLagen ?? []).length > 0 });
    }
    kontextmenue(e.clientX, e.clientY, eintraege.map((m) => (m.trenner ? m : {
      titel: m.titel, aktiv: m.aktiv !== false, hinweis: m.hinweis,
      aktion: () => ausfuehren(m.aktion, { ...m.param, instanz }),
    })));
  }

  function beiRad(e) {
    if (!vb) return;
    e.preventDefault();
    const dy = e.deltaMode === 1 ? e.deltaY * 33 : e.deltaY;
    zoomen(Math.exp(-Math.max(-300, Math.min(300, dy)) * 0.0016), e.clientX, e.clientY);
  }

  const eingabeFeld = (t) => t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName));
  const binIchAktiv = () => !!behaelter.closest('.kachel.aktiv') || !behaelter.closest('.kachel');
  const dialogOffen = () => (document.getElementById('dialogschicht')?.childElementCount ?? 0) > 0;

  const beiTasteUnten = (e) => {
    if (eingabeFeld(e.target) || dialogOffen() || !binIchAktiv()) return;
    if (e.key === 'Escape' && modus !== 'auswahl') { e.preventDefault(); e.stopImmediatePropagation(); setzeModus('auswahl'); return; }
    if (e.code !== 'Space' || !imFenster) return;
    leertaste = true;
    svg.classList.add('leertaste');
    e.preventDefault();
  };
  const beiTasteOben = (e) => { if (e.code === 'Space') { leertaste = false; svg.classList.remove('leertaste'); } };

  svg.addEventListener('pointerdown', beiPointerDown);
  svg.addEventListener('pointermove', beiPointerMove);
  svg.addEventListener('pointerup', beiPointerUp);
  svg.addEventListener('pointercancel', beiPointerUp);
  svg.addEventListener('pointerleave', () => { if (!druck) tip.hidden = true; });
  svg.addEventListener('wheel', beiRad, { passive: false });
  svg.addEventListener('mousedown', (e) => { if (e.button === 1) e.preventDefault(); });
  svg.addEventListener('auxclick', (e) => e.preventDefault());
  svg.addEventListener('contextmenu', beiKontext);
  wurzel.addEventListener('pointerenter', () => { imFenster = true; });
  wurzel.addEventListener('pointerleave', () => { imFenster = false; });
  window.addEventListener('keydown', beiTasteUnten, true);
  window.addEventListener('keyup', beiTasteOben);
  window.addEventListener('resize', groesseGeaendert);
  const beobachter = typeof ResizeObserver === 'function' ? new ResizeObserver(groesseGeaendert) : null;
  beobachter?.observe(wurzel);

  // Wechselt die aktive Ansicht, endet das Werkzeug (wie in Revit); der Hinweis in der Statusleiste verschwindet mit ihm
  const abAnsicht = abonnieren('ansicht', (k) => { if (!zerstoert && modus !== 'auswahl' && k !== kz()) setzeModus('auswahl'); });

  // Leiste: Werkzeuge und Aktionen laufen über die Aktionen (aktionen-schema.js), wie Menüband und Kontextmenü
  leiste.addEventListener('pointerdown', (e) => e.stopPropagation());
  leiste.addEventListener('click', (e) => {
    const w = e.target.closest('[data-werkzeug]');
    if (w) { ausfuehren(w.dataset.werkzeug === 'auswahl' ? 'schema.auswahl' : `schema.${w.dataset.werkzeug}`, { instanz }); return; }
    const a = e.target.closest('[data-aktion]');
    if (a) ausfuehren(`schema.${a.dataset.aktion}`, { instanz });
  });

  // ---------- Vertrag Ansichtsmodul ----------
  const instanz = {
    aktualisieren(ereignis) {
      try {
        if (!ereignis || ereignis.alles) { nochNichtGezeigt = true; planen(0); return; }
        if (ereignis.quelle === 'fremd') for (const id of ereignis.ids ?? []) fremdOffen.add(id);
        planen();
      } catch (e) { console.error('Schema-Ansicht: Aktualisieren fehlgeschlagen', e); }
    },
    ansichtGeaendert(a) {
      try { ansicht = a ?? ansicht; planen(0); } catch (e) { console.error('Schema-Ansicht: Ansicht ändern fehlgeschlagen', e); }
    },
    auswahlGeaendert(id) { hervorheben(id); },
    zoomAlles,
    zerstoeren() {
      zerstoert = true;
      ladeNr++;
      clearTimeout(ladeTimer);
      if (modus !== 'auswahl') hinweisSetzen('');
      abAnsicht();
      window.removeEventListener('keydown', beiTasteUnten, true);
      window.removeEventListener('keyup', beiTasteOben);
      window.removeEventListener('resize', groesseGeaendert);
      beobachter?.disconnect();
      for (const t of timer) clearTimeout(t);
      timer.clear();
      behaelter.replaceChildren();
    },

    // Schnittstelle für die Aktionen
    istSchema: true,
    get kennzeichen() { return kz(); },
    get ansicht() { return ansicht; },
    get modus() { return modus; },
    get daten() { return daten; },
    get beschaeftigt() { return beschaeftigt; },
    setzeModus,
    /** Verbinden mit einem Knoten (Id) als erstem. */
    verbindenVon(id) { setzeModus('verbinden', { von: id }); },
    /** «Schema neu anordnen» mit Rückfrage. */
    neuAnordnen() { return bearbeiten.neuAnordnen(kz(), daten); },
    /** Wählt ein Element (Knoten oder Linienteil) und zoomt darauf; wartet, falls die Daten noch fehlen. */
    zeigen(id) {
      auswaehlen(id);
      wunsch = id;
      if (daten) wunschAnwenden();
      else planen(0);
    },
    /** Wo steht ein Element? { x, y } des Knotens oder null (für Tests). */
    lageVon(id) { const k = (daten?.knoten ?? []).find((x) => x.element === id); return k ? { x: k.x, y: k.y, manuell: k.manuell } : null; },
    /** Papier-mm → Bildschirm {x, y} (für Tests und Werkzeuge). */
    zuBildschirm(p) {
      const r = svg.getBoundingClientRect();
      if (!vb || !r.width) return { x: 0, y: 0 };
      return { x: r.left + ((p[0] - vb.x) / vb.w) * r.width, y: r.top + ((-p[1] - vb.y) / vb.h) * r.height };
    },
    zuModell(clientX, clientY) { const p = zuPapier(clientX, clientY); return [p.x, p.y]; },
    pxJeMm,
    /** Ändert den Ausschnitt (Papier-mm); für «Im Schema zeigen» und Tests. */
    zoomAuf(b, mindest = 0) { benutzt = true; einpassen(b, mindest); },
    get art() { return 'schema'; },
  };

  leisteAnzeigen();
  hervorheben(zustand.auswahl);
  groesseGeaendert();
  laden();
  return instanz;
}

export default { erstellen };
