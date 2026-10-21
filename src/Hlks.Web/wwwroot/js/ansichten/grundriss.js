// Plan-Ansicht (SVG) für Grundriss, Schnitt und Ansicht: holt die Plangrafik des Kerns (/api/plangrafik/{kennzeichen}) und
// zeichnet sie – plangrafik.js macht daraus das SVG, diese Datei die Bedienung. Keine Fachlogik: Zeichnen, Zoomen,
// Auswählen. Mausrad zoomt an der Mausposition (viewBox), Verschieben mit mittlerer Taste, Leertaste+Ziehen oder linker
// Taste auf leerer Fläche, Doppelklick mittlere Taste = alles zeigen, Klick wählt, Hover zeigt Kennzeichen · Klasse · System.
// Nach jedem Ereignis «modell» wird die Grafik neu geholt (entprellt, Zoom und Auswahl bleiben).
// Darstellung «Hülle» (darstellung = huelle, Modul 1c): die Streifen zeichnet der Kern (HuelleGrafik); diese Datei zeigt nur die
// Legende unten links mit den Flächensummen je Angrenzung für das Geschoss (Abfrage raumgrenzen).
// N9 (gedrehte Grundrisse): die Plangrafik bleibt in Modellkoordinaten; gedreht wird nur die Gruppe gr-welt (plan.drehung, ausrichtung.js),
// der Ausschnitt vb liegt in den Achsen der Ansicht, zuModell/zuBildschirm rechnen zurück ins Modell. Der Nordpfeil oben rechts zeigt nach
// geografisch Nord (plan.nord), unten links stehen die Ausrichtung und die Mausposition in Modellkoordinaten.
//
// Werkzeug-Schnittstelle (Entwurf modul-1a.md «Schnittstelle Ansicht ↔ Werkzeug»): art, ansicht, massstab, zuModell,
// zuBildschirm, pxJeMm, fangdaten, werkzeugSchicht, setzeWerkzeug/werkzeug. Ist ein Werkzeug aktiv, bekommt es pointerdown,
// pointermove, pointerup, dblclick und keydown zuerst (werkzeug.ereignis(art, e, ansicht)); true = verbraucht.

import { zustand, element, auswaehlen } from '../zustand.js';
import { holen, abfrage } from '../api.js';
import { ansichtsWerte, esc } from './hilfen.js';
import { planSvg, fangdatenVon, schraffurMap, neuerMusterPraefix } from './plangrafik.js';
import { istHuelle, legendeDaten, legendeHtml } from './huelle-darstellung.js';
import { weltTransform, ansichtsBegrenzung, boxInAnsicht, drehen, drehungVon, nordCss, ausrichtungText, svgAusModell, modellAusSvg } from './ausrichtung.js';   // N9: gedrehte Grundrisse

const SVGNS = 'http://www.w3.org/2000/svg';
const FREMD_DAUER = 1500;      // ms Hervorhebung fremder Änderungen
const KLICK_TOLERANZ = 4;      // px, darunter gilt eine Mausbewegung noch als Klick
const ENTPRELLUNG = 150;       // ms zwischen Ereignis und Neuholen der Grafik
const MIN_PX = 1;              // Linien nie dünner als ein Bildschirmpixel
const BALKEN_LAENGEN = [100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000];

// Schraffurdefinitionen ändern sich nie während der Sitzung: einmal holen
let schraffurenGeholt = null;
const schraffuren = () => (schraffurenGeholt ??= holen('/api/schraffuren').then(schraffurMap).catch((e) => { schraffurenGeholt = null; console.warn('Schraffuren nicht geladen', e); return new Map(); }));

function erstellen(behaelter, ansichtElement) {
  // Muster-Ids je Ansicht eindeutig: Alle offenen Ansichten teilen ein Dokument; gleiche Ids (gleicher Massstab, gleicher Winkel) lösten
  // «url(#…)» auf das erste Muster im Dokument auf – lag das in einer verborgenen Ansicht (display: none), blieb die Schraffur der sichtbaren leer.
  const musterPraefix = neuerMusterPraefix();
  let ansicht = ansichtElement;
  let plan = null;               // letzte Plangrafik des Kerns
  let fang = null;               // Fangdaten, bei Bedarf aus plan berechnet
  let vb = null;                 // { x, y, w, h } in SVG-Koordinaten (y = −y des Modells)
  let gewaehlt = null;
  let letzteBreite = 0;
  let benutzt = false;           // Zoom oder Verschieben durch den Benutzer: dann bleibt der Ausschnitt
  let ladeNr = 0;                // gegen veraltete Antworten
  let legendeNr = 0;             // dasselbe für die Legende der Darstellung Hülle
  let ladeTimer = null;
  let werkzeug = null;
  let zerstoert = false;
  let nochNichtGezeigt = true;   // erste Grafik: Ausschnitt einpassen
  let wunschBox = null;          // zoomAuf vor der ersten Grafik (Schema: «Im Grundriss zeigen»): Ausschnitt (Modell), der nach dem Laden gilt
  let drehung = 0;               // N9: Drehung der Ansicht (Grad, Gegenuhrzeigersinn, Modell → Ansicht); vb liegt in den Achsen der Ansicht
  const fremdOffen = new Set();  // Ids fremder Änderungen, die nach dem Neuholen aufleuchten
  const timer = new Set();

  const art = () => ansicht?.werte?.art ?? 'grundriss';

  behaelter.innerHTML = `<div class="gr-wurzel">
      <svg class="gr-svg" xmlns="${SVGNS}" preserveAspectRatio="xMidYMid meet" tabindex="-1">
        <g class="gr-welt" transform="scale(1 -1)"><g class="gr-plan"></g><g class="gr-werkzeug"></g></g>
      </svg>
      <div class="gr-tip" hidden></div>
      <div class="gr-leer" hidden></div>
      <div class="gr-nord" title="Geografisch Nord"><svg viewBox="0 0 40 52" width="34" height="44" aria-hidden="true">
        <path d="M20 4 L31 44 L20 36 L9 44 Z" fill="var(--text-leise)" stroke="var(--text)" stroke-width="1.5" stroke-linejoin="round"/>
        <path d="M20 4 L20 36 L9 44 Z" fill="var(--text)"/>
      </svg><span>N</span></div>
      <div class="gr-legende hl-feldbox" hidden></div>
      <div class="gr-masstab"><span class="gr-massstab-text"></span><span class="gr-ausrichtung"></span><span class="gr-balken-zeile"><span class="gr-balken"></span><span class="gr-balken-text"></span></span><span class="gr-koord"></span></div>
    </div>`;
  const wurzel = behaelter.querySelector('.gr-wurzel');
  const svg = wurzel.querySelector('.gr-svg');
  const planGruppe = svg.querySelector('.gr-plan');
  const welt = svg.querySelector('.gr-welt');
  const werkzeugSchicht = svg.querySelector('.gr-werkzeug');
  const tip = wurzel.querySelector('.gr-tip');
  const leer = wurzel.querySelector('.gr-leer');
  const nord = wurzel.querySelector('.gr-nord');
  const legende = wurzel.querySelector('.gr-legende');
  const massstabText = wurzel.querySelector('.gr-massstab-text');
  const balken = wurzel.querySelector('.gr-balken');
  const balkenText = wurzel.querySelector('.gr-balken-text');
  const ausrichtungAnzeige = wurzel.querySelector('.gr-ausrichtung');
  const koordAnzeige = wurzel.querySelector('.gr-koord');

  const massstab = () => plan?.massstab ?? ansichtsWerte(ansicht).massstab;

  function artAnzeige() {
    nord.hidden = art() !== 'grundriss';   // Nordpfeil der Oberfläche nur im Grundriss
    svg.dataset.art = art();
  }

  // ---------- Grafik holen und zeichnen ----------
  async function laden() {
    const nr = ++ladeNr;
    const kz = ansicht?.kennzeichen;
    if (!kz || zerstoert) return;
    try {
      const [p, sch] = await Promise.all([holen('/api/plangrafik/' + encodeURIComponent(kz)), schraffuren()]);
      if (nr !== ladeNr || zerstoert) return;
      const warLeer = !plan || (plan.teile ?? []).length === 0;
      plan = p;
      fang = null;
      ausrichtungUebernehmen();
      planGruppe.innerHTML = planSvg(plan, sch, { idPraefix: musterPraefix });
      svg.style.setProperty('--m', String(massstab()));
      const hat = (plan.teile ?? []).length > 0;
      leer.hidden = hat;
      if (!hat) leer.innerHTML = leerText();
      massstabAnzeige();
      legendeAktualisieren();
      hervorheben(gewaehlt);
      fremdAnwenden();
      if (nochNichtGezeigt || !vb || (warLeer && hat && !benutzt)) { nochNichtGezeigt = false; zoomAlles(); } else vbAnwenden();
      if (wunschBox) { const b = wunschBox; wunschBox = null; benutzt = true; einpassen(boxInAnsicht(b, drehung)); }
    } catch (e) {
      if (nr !== ladeNr || zerstoert) return;
      console.error('Plan-Ansicht: Grafik nicht geladen', e);
      plan = null; fang = null;
      planGruppe.replaceChildren();
      leer.hidden = false;
      leer.innerHTML = `Die Grafik konnte nicht geladen werden:<br>${esc(e.message ?? e)}${e.vorschlag ? '<br>' + esc(e.vorschlag) : ''}`;
      if (!vb) zoomAlles();
    }
  }

  /**
   * N9: Ausrichtung der Plangrafik übernehmen – die eine Stelle der Drehung am Bildschirm: Gruppe gr-welt um «drehung» drehen, Nordpfeil
   * nach «nord». Ändert sich die Drehung (Ausrichtung umgeschaltet, Planwinkel neu bestimmt), bleibt die Mitte des Ausschnitts im Modell
   * dieselbe; ohne eigenen Zoom wird neu eingepasst.
   */
  function ausrichtungUebernehmen() {
    const alt = drehung;
    drehung = drehungVon(plan);
    welt.setAttribute('transform', weltTransform(drehung));
    const pfeil = nord.querySelector('svg');
    if (pfeil) pfeil.style.transform = nordCss(plan?.nord);
    nord.title = drehung || plan?.nord ? `Geografisch Nord (${Math.round((plan?.nord ?? 0) * 10) / 10}° gegen «oben»)` : 'Geografisch Nord oben';
    ausrichtungAnzeige.textContent = art() === 'grundriss' ? ausrichtungText(plan) : '';
    if (alt === drehung || !vb) return;
    if (!benutzt) { nochNichtGezeigt = true; return; }
    const m = drehen([vb.x + vb.w / 2, -(vb.y + vb.h / 2)], -alt), v = drehen(m, drehung);
    vb = { ...vb, x: v[0] - vb.w / 2, y: -v[1] - vb.h / 2 };
  }

  /** Legende der Darstellung Hülle: Summen je Angrenzung für das Geschoss; sonst ausgeblendet. */
  async function legendeAktualisieren() {
    const nr = ++legendeNr;
    if (!istHuelle(ansicht) || art() !== 'grundriss' || !ansicht?.geschoss) { legende.hidden = true; return; }
    const titel = `Hülle ${ansicht.geschoss}`;
    try {
      const a = await abfrage('raumgrenzen', { geschoss: ansicht.geschoss });
      if (nr !== legendeNr || zerstoert) return;
      legende.innerHTML = legendeHtml(legendeDaten(a), { titel });
    } catch (e) {
      if (nr !== legendeNr || zerstoert) return;
      legende.innerHTML = legendeHtml([], { titel, fehler: `Raumgrenzen nicht geladen: ${e.message ?? e}` });
    }
    legende.hidden = false;
  }

  function leerText() {
    if (art() === 'grundriss') return `Keine Elemente${ansicht?.geschoss ? ' im Geschoss ' + esc(ansicht.geschoss) : ''} sichtbar.<br>Gewerke- und Klassenfilter der Ansicht prüfen.`;
    return 'Nichts zu zeigen.<br>Schnitt- bzw. Ansichtslinie und Blickrichtung prüfen.';
  }

  function planen(ms = ENTPRELLUNG) {
    clearTimeout(ladeTimer);
    ladeTimer = setTimeout(laden, ms);
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
  const groesse = () => ({ W: svg.clientWidth, H: svg.clientHeight });

  function vbAnwenden() {
    if (!vb) return;
    svg.setAttribute('viewBox', `${vb.x} ${vb.y} ${vb.w} ${vb.h}`);
    const { W } = groesse();
    if (W > 1) svg.style.setProperty('--minpx', `${MIN_PX * vb.w / W}px`);   // 1 Bildschirmpixel in Modell-mm
    masstabsbalken();
  }

  function einpassen(b) {
    const { W, H } = groesse();
    if (W < 2 || H < 2) { vb = null; return; }
    if (!b) b = { minx: -1000, miny: -1000, maxx: 11000, maxy: 8000 };
    const rand = Math.max(b.maxx - b.minx, b.maxy - b.miny, 1000) * 0.05;
    const x0 = b.minx - rand, x1 = b.maxx + rand, y0 = b.miny - rand, y1 = b.maxy + rand;
    let w = x1 - x0, h = y1 - y0;
    const cx = (x0 + x1) / 2, cy = -(y0 + y1) / 2;
    if (w / h < W / H) w = h * W / H; else h = w * H / W;
    vb = { x: cx - w / 2, y: cy - h / 2, w, h };
    letzteBreite = W;
    vbAnwenden();
  }

  function zoomAlles() { benutzt = false; einpassen(ansichtsBegrenzung(plan)); }   // N9: Begrenzung in den Achsen der Ansicht

  function groesseGeaendert() {
    const { W, H } = groesse();
    if (W < 2 || H < 2) return;
    if (!vb || !benutzt) { zoomAlles(); return; }
    // Mitte und Zoomstufe (Modellgrösse pro Pixel) bleiben; nur der Ausschnitt wächst oder schrumpft
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

  function zoomen(faktor, clientX, clientY) {
    if (!vb) return;
    benutzt = true;
    const r = svg.getBoundingClientRect();
    const p = zuSvg(clientX, clientY);
    let w = vb.w / faktor;
    w = Math.min(Math.max(w, 100), 5e7);
    const f = vb.w / w;
    const h = vb.h / f;
    vb = { x: p.x - ((clientX - r.left) / r.width) * w, y: p.y - ((clientY - r.top) / r.height) * h, w, h };
    vbAnwenden();
  }

  function massstabAnzeige() {
    massstabText.textContent = `Massstab 1:${massstab()}`;
  }

  function masstabsbalken() {
    const { W } = groesse();
    if (!vb || W < 2) return;
    const pxProMm = W / vb.w;
    const laenge = BALKEN_LAENGEN.find((l) => l * pxProMm >= 70) ?? BALKEN_LAENGEN.at(-1);
    balken.style.width = `${Math.round(laenge * pxProMm)}px`;
    balkenText.textContent = laenge >= 1000 ? `${laenge / 1000} m` : `${laenge} mm`;
  }

  // ---------- Bedienung ----------
  const idVon = (ziel) => ziel?.closest?.('[data-id]')?.getAttribute('data-id') ?? null;
  let druck = null;            // { id, x, y, bewegt, taste }
  let schiebt = false;
  let leertaste = false;
  let imFenster = false;
  let letzteMitte = 0;

  /** Gibt das Ereignis zuerst dem Werkzeug; true = verbraucht. Mittlere Taste und Rad gehen nie ans Werkzeug. */
  function anWerkzeug(ereignisArt, e) {
    if (!werkzeug?.ereignis) return false;
    try { return werkzeug.ereignis(ereignisArt, e, instanz) === true; }
    catch (fehler) { console.error('Werkzeug', ereignisArt, fehler); return false; }
  }

  function tooltipZeigen(e, id) {
    const el = id ? element(id) : null;
    if (!el) { tip.hidden = true; return; }
    tip.textContent = [el.kennzeichen, el.klasse, el.system].filter(Boolean).join(' · ');
    const r = wurzel.getBoundingClientRect();
    tip.hidden = false;
    tip.style.left = `${Math.min(e.clientX - r.left + 14, r.width - tip.offsetWidth - 4)}px`;
    tip.style.top = `${Math.min(e.clientY - r.top + 16, r.height - tip.offsetHeight - 4)}px`;
  }

  function beiPointerDown(e) {
    if (!vb) return;
    tip.hidden = true;
    if (e.button === 1) {
      e.preventDefault();
      if (letzteMitte > 0 && performance.now() - letzteMitte < 400) { letzteMitte = 0; zoomAlles(); return; }   // Doppelklick mittlere Taste (zählt nur ohne Ziehen)
      letzteMitte = 0;
      druck = { id: null, x: e.clientX, y: e.clientY, bewegt: true, taste: 1, weg: 0 };
      schiebenStart(e);
      return;
    }
    if (werkzeug && anWerkzeug('pointerdown', e)) { e.preventDefault(); druck = null; return; }
    if (e.button === 0) {
      druck = { id: idVon(e.target), x: e.clientX, y: e.clientY, bewegt: false, taste: 0 };
      if (leertaste) { druck.bewegt = true; schiebenStart(e); }
    }
  }

  function schiebenStart(e) {
    schiebt = true;
    benutzt = true;
    try { svg.setPointerCapture(e.pointerId); } catch { /* egal */ }
    svg.classList.add('schiebt');
  }

  /** N9: Mausposition immer in Modellkoordinaten (x Ost, y Nord, mm) – auch im gedrehten Grundriss; Schnitt/Ansicht: u, v. */
  function koordZeigen(e) {
    if (!vb) return;
    const [x, y] = instanz.zuModell(e.clientX, e.clientY);
    koordAnzeige.textContent = art() === 'grundriss' ? `x ${Math.round(x)} · y ${Math.round(y)} mm (Modell)` : `u ${Math.round(x)} · v ${Math.round(y)} mm`;
  }

  function beiPointerMove(e) {
    koordZeigen(e);
    if (druck?.taste === 1 || (druck && schiebt)) {
      // Verschieben (mittlere Taste, Leertaste oder Ziehen auf leerer Fläche)
      const dx = e.clientX - druck.x, dy = e.clientY - druck.y;
      if (druck.taste === 1) druck.weg += Math.abs(dx) + Math.abs(dy);
      if (vb) {
        const { W, H } = groesse();
        vb.x -= dx * vb.w / W; vb.y -= dy * vb.h / H;
        druck.x = e.clientX; druck.y = e.clientY;
        vbAnwenden();
      }
      return;
    }
    if (werkzeug && anWerkzeug('pointermove', e)) { tip.hidden = true; return; }
    if (druck) {
      const dx = e.clientX - druck.x, dy = e.clientY - druck.y;
      if (!druck.bewegt && Math.hypot(dx, dy) > KLICK_TOLERANZ) {
        druck.bewegt = true;
        schiebenStart(e);
      }
      return;
    }
    tooltipZeigen(e, idVon(e.target));
  }

  function beiPointerUp(e) {
    if (e.button === 1 || druck?.taste === 1) {
      if (druck?.taste === 1 && druck.weg <= 4) letzteMitte = performance.now();   // Klick ohne Ziehen: zählt für den Doppelklick
      druck = null;
      if (schiebt) { schiebt = false; svg.classList.remove('schiebt'); try { svg.releasePointerCapture(e.pointerId); } catch { /* egal */ } }
      return;
    }
    if (werkzeug && e.type === 'pointerup' && anWerkzeug('pointerup', e)) { druck = null; if (schiebt) { schiebt = false; svg.classList.remove('schiebt'); } return; }
    if (!druck) return;
    const d = druck;
    druck = null;
    if (schiebt) { schiebt = false; svg.classList.remove('schiebt'); try { svg.releasePointerCapture(e.pointerId); } catch { /* egal */ } }
    if (d.taste === 0 && !d.bewegt) auswaehlen(d.id ?? null);
  }

  function beiRad(e) {
    if (!vb) return;
    e.preventDefault();
    const dy = e.deltaMode === 1 ? e.deltaY * 33 : e.deltaY;
    zoomen(Math.exp(-Math.max(-300, Math.min(300, dy)) * 0.0016), e.clientX, e.clientY);
  }

  const eingabeFeld = (t) => t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT|BUTTON)$/.test(t.tagName));
  const bin_ich_aktiv = () => !!behaelter.closest('.kachel.aktiv') || !behaelter.closest('.kachel');

  // Tasten: zuerst das Werkzeug (nur in der aktiven Ansicht, nicht aus Eingabefeldern), dann die Leertaste zum Verschieben
  const beiTasteUnten = (e) => {
    if (eingabeFeld(e.target)) return;
    if (werkzeug && bin_ich_aktiv() && anWerkzeug('keydown', e)) { e.preventDefault(); e.stopImmediatePropagation(); return; }
    if (e.code !== 'Space' || !imFenster) return;
    leertaste = true;
    svg.classList.add('leertaste');
    e.preventDefault();
  };
  const beiTasteOben = (e) => { if (e.code === 'Space') { leertaste = false; svg.classList.remove('leertaste'); } };
  const beiDoppelklick = (e) => { if (e.button === 0 && werkzeug) anWerkzeug('dblclick', e); };

  svg.addEventListener('pointerdown', beiPointerDown);
  svg.addEventListener('pointermove', beiPointerMove);
  svg.addEventListener('pointerup', beiPointerUp);
  svg.addEventListener('pointercancel', beiPointerUp);
  svg.addEventListener('dblclick', beiDoppelklick);
  svg.addEventListener('pointerleave', () => { if (!druck) tip.hidden = true; koordAnzeige.textContent = ''; });
  svg.addEventListener('wheel', beiRad, { passive: false });
  svg.addEventListener('mousedown', (e) => { if (e.button === 1) e.preventDefault(); });   // kein Autoscroll
  svg.addEventListener('auxclick', (e) => e.preventDefault());
  svg.addEventListener('contextmenu', (e) => e.preventDefault());
  wurzel.addEventListener('pointerenter', () => { imFenster = true; });
  wurzel.addEventListener('pointerleave', () => { imFenster = false; });
  window.addEventListener('keydown', beiTasteUnten, true);   // Aufnahmephase: vor den Kürzeln
  window.addEventListener('keyup', beiTasteOben);
  window.addEventListener('resize', groesseGeaendert);
  const beobachter = typeof ResizeObserver === 'function' ? new ResizeObserver(groesseGeaendert) : null;
  beobachter?.observe(wurzel);

  // ---------- Vertrag Ansichtsmodul + Werkzeug-Schnittstelle ----------
  function ansichtGeaendert(a) {
    try {
      const geschossAlt = ansicht?.geschoss, artAlt = art();
      ansicht = a ?? ansicht;
      artAnzeige();
      if (ansicht?.geschoss !== geschossAlt || art() !== artAlt) { benutzt = false; nochNichtGezeigt = true; }
      planen(0);
    } catch (e) { console.error('Plan-Ansicht: Ansicht ändern fehlgeschlagen', e); }
  }

  const instanz = {
    // Vertrag fenster.js
    aktualisieren(ereignis) {
      try {
        if (!ereignis || ereignis.alles) { nochNichtGezeigt = true; planen(0); return; }
        if (ereignis.quelle === 'fremd') for (const id of ereignis.ids ?? []) fremdOffen.add(id);
        planen();
      } catch (e) { console.error('Plan-Ansicht: Aktualisieren fehlgeschlagen', e); }
    },
    ansichtGeaendert,
    auswahlGeaendert(id) { hervorheben(id); },
    zoomAlles,
    /** Zoomt auf ein Rechteck {minx, miny, maxx, maxy} in Modell-mm (auch vor der ersten Grafik); der Ausschnitt bleibt dann stehen.
     *  N9: in gedrehten Grundrissen auf das umschliessende Rechteck in den Achsen der Ansicht. */
    zoomAuf(b) { if (plan && vb) { benutzt = true; einpassen(boxInAnsicht(b, drehung)); } else wunschBox = b; },
    zerstoeren() {
      zerstoert = true;
      ladeNr++;
      legendeNr++;
      clearTimeout(ladeTimer);
      werkzeug = null;
      window.removeEventListener('keydown', beiTasteUnten, true);
      window.removeEventListener('keyup', beiTasteOben);
      window.removeEventListener('resize', groesseGeaendert);
      beobachter?.disconnect();
      for (const t of timer) clearTimeout(t);
      timer.clear();
      behaelter.replaceChildren();
    },

    // Schnittstelle für Werkzeuge
    get art() { return art(); },
    get ansicht() { return ansicht; },
    get massstab() { return massstab(); },
    /** N9: Drehung der Ansicht in Grad (Gegenuhrzeigersinn, Modell → Ansicht); Werkzeuge fangen orthogonal zu diesen Achsen. */
    get drehung() { return drehung; },
    /** Bildschirm (clientX, clientY) → Modell-mm der Ansicht [x, y] (N9: aus den gedrehten Achsen zurück ins Modell). */
    zuModell(clientX, clientY) {
      if (!vb) return [0, 0];
      const p = zuSvg(clientX, clientY);
      return modellAusSvg([p.x, p.y], drehung);
    },
    /** Modell-mm [x, y] → Client-Pixel { x, y }. */
    zuBildschirm(p) {
      const r = svg.getBoundingClientRect();
      if (!vb || !r.width) return { x: 0, y: 0 };
      const s = svgAusModell(p, drehung);
      return { x: r.left + ((s[0] - vb.x) / vb.w) * r.width, y: r.top + ((s[1] - vb.y) / vb.h) * r.height };
    },
    /** Bildschirm-Pixel (CSS) je Modell-mm beim aktuellen Zoom. */
    pxJeMm() { const { W } = groesse(); return vb && W > 0 ? W / vb.w : 1; },
    /** Fangpunkte und -kanten aus der Plangrafik. */
    fangdaten() { return (fang ??= fangdatenVon(plan)); },
    /** SVG-<g> in Modellkoordinaten (y wie im Modell) für Vorschau, Fangsymbol, Gummiband – über allem. */
    werkzeugSchicht,
    setzeWerkzeug(w) {
      werkzeug = w ?? null;
      svg.classList.toggle('werkzeug-aktiv', !!werkzeug && !werkzeug.passiv);   // passive Werkzeuge (Griffe): kein Fadenkreuz, Hover bleibt
    },
    get werkzeug() { return werkzeug; },
  };

  artAnzeige();
  hervorheben(zustand.auswahl);
  groesseGeaendert();
  laden();

  return instanz;
}

export default { erstellen };
