// Fensterverhalten der Dialoge (Auftrag W1, entwurf/fenster-revit.md): verschiebbar an der Titelleiste, Grösse an Rand und Ecke, «Einklappen» auf die Titelleiste,
// Doppelklick auf die Titelleiste = zurück zur Mitte, immer ganz im sichtbaren Bereich, Lage und Grösse je Dialogart gemerkt (je Rechner, zustand.js «merken»).
// Wird nur von dialoge.js benutzt (zeigen); kein Dialog baut sein eigenes Fenster. Rechenregeln: fenstergeometrie.js, gemerkte Werte: dialog-merken.js.
//
// Zustände des Fensters (Klassen am .dlg): ohne Klasse = in der Mitte (CSS-Raster, wächst mit dem Inhalt mit, wie bisher); «verschoben» = freie Lage (left/top);
// «eingeklappt» = nur die Titelleiste. Eine eigene Grösse (width/height) setzt nur der Benutzer (Ziehen am Rand) oder die gemerkte Grösse.

import { merken } from '../zustand.js';
import { begrenzen, mindestGroesse } from './fenstergeometrie.js';
import { bereinigen, setzen, positionVergessen, SCHLUESSEL } from './dialog-merken.js';
import { sichtbereich, rechteck, rechteckSetzen, griffeAnlegen, groesseZiehen, bewegenZiehen } from './fensterbewegung.js';

const MIN_SICHT = 120;     // px: kleinerer Sichtbereich gilt als verdeckt (Fenster minimiert, Bereich ausgeblendet) – dann nicht einpassen
const MIN_GEMERKT = 120;   // px: kleinere Grössen werden weder gemerkt noch übernommen
const karte = () => bereinigen(merken.lesen(SCHLUESSEL, {}));
const speichern = (neu) => merken.schreiben(SCHLUESSEL, neu);

/** HTML der Knöpfe in der Titelleiste (Einklappen, Schliessen). */
export function kopfKnoepfeHtml(schliessbar) {
  return `<span class="dlg-knoepfe"><button type="button" class="dlg-ein" title="Einklappen" aria-label="Einklappen" aria-expanded="true">▴</button>${schliessbar ? '<button type="button" class="dlg-x" title="Schliessen (Esc)">✕</button>' : ''}</span>`;
}

/**
 * Macht den Dialog verschiebbar usw. art = Schlüssel der Dialogart (dialog-merken.js dialogArt). Gibt { abbauen() } zurück; das Fenster muss schon im Dokument stehen.
 */
export function fensterAnbinden(fenster, { art }) {
  const kopf = fenster.querySelector('.dlg-kopf');
  const knopfEin = fenster.querySelector('.dlg-ein');
  let eingeklappt = false;
  let eigeneGroesse = false;   // width/height stammen vom Benutzer (Ziehen oder gemerkt)
  griffeAnlegen(fenster, 'dlg-griff');

  const frei = () => fenster.classList.contains('verschoben');

  /** Mindestgrösse aus dem, was lesbar bleiben muss: Titel mit Knöpfen, Knopfleiste, Fehlerzeile. */
  function mindest() {
    const fuss = fenster.querySelector('.dlg-fuss');
    const fehler = fenster.querySelector('.dlg-fehler');
    const fussBreite = fuss ? [...fuss.children].reduce((s, k) => s + k.getBoundingClientRect().width + 6, 0) + 24 : 0;
    const titel = fenster.querySelector('.dlg-titel');
    const knoepfe = fenster.querySelector('.dlg-knoepfe');
    const kopfBreite = Math.min(360, (titel?.scrollWidth ?? 0) + (knoepfe?.offsetWidth ?? 0) + 28);
    return mindestGroesse({ kopfB: kopfBreite, fussB: fussBreite, kopfH: kopf.offsetHeight, fussH: fuss?.offsetHeight ?? 0, fehlerH: fehler && !fehler.hidden ? fehler.offsetHeight : 0 });
  }

  /** Aus der Mitte (Raster) in freie Lage wechseln, ohne dass das Fenster springt. */
  function festsetzen() {
    if (frei()) return;
    const r = rechteck(fenster);
    fenster.classList.add('verschoben');
    fenster.style.left = Math.round(r.x) + 'px';
    fenster.style.top = Math.round(r.y) + 'px';
  }

  /** Holt das Fenster ganz in den sichtbaren Bereich (nach dem Öffnen, beim Verkleinern des Browserfensters, wenn der Inhalt wächst). */
  function einpassen() {
    const sicht = sichtbereich();
    // Befund 24.10.2026: war das Browserfenster kurz verdeckt (Sichtbereich 0 × 0), wurde der Dialog auf 0 × 0 px begrenzt und blieb unsichtbar
    if (sicht.b < MIN_SICHT || sicht.h < MIN_SICHT) return;
    if (fenster.style.width === '0px' || fenster.style.height === '0px') { fenster.style.width = ''; fenster.style.height = ''; eigeneGroesse = false; }
    const r = rechteck(fenster);
    if (r.b === 0 && r.h === 0) return;
    const z = begrenzen(r, sicht, eigeneGroesse && !eingeklappt ? mindest() : { b: 0, h: 0 });
    if (frei()) {
      if (Math.abs(z.x - r.x) > 0.5) fenster.style.left = Math.round(z.x) + 'px';
      if (Math.abs(z.y - r.y) > 0.5) fenster.style.top = Math.round(z.y) + 'px';
    }
    if (!eingeklappt && Math.abs(z.b - r.b) > 0.5 && (eigeneGroesse || r.b > sicht.b)) fenster.style.width = Math.round(z.b) + 'px';
    if (!eingeklappt && Math.abs(z.h - r.h) > 0.5 && (eigeneGroesse || r.h > sicht.h)) fenster.style.height = Math.round(z.h) + 'px';
  }

  function lageMerken(r) {
    const teil = { x: Math.round(r.x), y: Math.round(r.y) };
    if (eigeneGroesse && !eingeklappt && r.b >= MIN_GEMERKT && r.h >= MIN_GEMERKT) { teil.b = Math.round(r.b); teil.h = Math.round(r.h); }
    speichern(setzen(karte(), art, teil));
  }

  // gemerkte Lage und Grösse anwenden
  const gemerkt = karte()[art];
  if (gemerkt) {
    if (gemerkt.b !== undefined && gemerkt.b >= MIN_GEMERKT && gemerkt.h >= MIN_GEMERKT) {   // winzige gemerkte Grössen (z. B. 0 × 0) nicht übernehmen
      fenster.style.width = gemerkt.b + 'px';
      fenster.style.height = gemerkt.h + 'px';
      fenster.style.maxHeight = 'none';
      eigeneGroesse = true;
    }
    if (gemerkt.x !== undefined) {
      fenster.classList.add('verschoben');
      fenster.style.left = gemerkt.x + 'px';
      fenster.style.top = gemerkt.y + 'px';
    }
    einpassen();
  }

  // Verschieben an der Titelleiste
  kopf.addEventListener('pointerdown', (e) => {
    if (e.button !== 0 || e.target.closest('button')) return;
    e.preventDefault();
    festsetzen();
    bewegenZiehen(e, rechteck(fenster), {
      beiAenderung: (r) => { fenster.style.left = Math.round(r.x) + 'px'; fenster.style.top = Math.round(r.y) + 'px'; },
      beiEnde: (r, geaendert) => { if (geaendert) lageMerken(r); },
    });
  });

  // Grösse an Rand und Ecke
  fenster.addEventListener('pointerdown', (e) => {
    const g = e.target.closest('.dlg-griff');
    if (!g || e.button !== 0 || eingeklappt) return;
    e.preventDefault();
    festsetzen();
    groesseZiehen(e, g.dataset.kante, rechteck(fenster), {
      min: mindest(),
      beiAenderung: (r) => { fenster.style.maxHeight = 'none'; rechteckSetzen(fenster, r); },
      beiEnde: (r, geaendert) => { if (geaendert) { eigeneGroesse = true; lageMerken(r); } },
    });
  });

  // Doppelklick auf die Titelleiste: zurück zur Mitte (die Grösse bleibt)
  kopf.addEventListener('dblclick', (e) => {
    if (e.target.closest('button')) return;
    fenster.classList.remove('verschoben');
    fenster.style.left = '';
    fenster.style.top = '';
    speichern(positionVergessen(karte(), art));
    einpassen();
  });

  // Einklappen
  function einklappen(ja) {
    if (ja === eingeklappt) return;
    if (ja) festsetzen();   // die Titelleiste bleibt, wo sie ist
    eingeklappt = ja;
    fenster.classList.toggle('eingeklappt', ja);
    if (knopfEin) {
      knopfEin.textContent = ja ? '▾' : '▴';
      knopfEin.title = ja ? 'Ausklappen' : 'Einklappen';
      knopfEin.setAttribute('aria-label', knopfEin.title);
      knopfEin.setAttribute('aria-expanded', String(!ja));
    }
    if (!ja) einpassen();
  }
  knopfEin?.addEventListener('click', () => einklappen(!eingeklappt));

  // Browserfenster verkleinert oder Inhalt wächst: zurückholen. Aus dem ResizeObserver heraus erst im nächsten Bild (Grösse im Rückruf zu ändern, löst «ResizeObserver loop» aus).
  const beiGroesse = () => einpassen();
  let geplant = 0;
  const planen = () => { if (!geplant) geplant = requestAnimationFrame(() => { geplant = 0; einpassen(); }); };
  window.addEventListener('resize', beiGroesse);
  const beobachter = typeof ResizeObserver === 'function' ? new ResizeObserver(planen) : null;
  beobachter?.observe(fenster);

  return {
    einklappen,
    abbauen() {
      window.removeEventListener('resize', beiGroesse);
      beobachter?.disconnect();
      if (geplant) cancelAnimationFrame(geplant);
    },
  };
}
