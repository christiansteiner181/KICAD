// Bildbetrachter für Unterlagen (Modul 2, C2): zeigt die Antwort der Abfrage unterlage_bild (PNG als base64) mit markierten Boxen von Befunden
// und meldet Klicks als Punkt in Unterlagen-Koordinaten (für Passpunkte und zum Wählen eines Befunds). Rechnet nichts Fachliches: die Umrechnung
// Bildpunkt ↔ Unterlagen-Koordinaten steht in unterlagen-text.js (boxImBild, bildPunktZuUnterlage) und folgt dem «bereich» des Kerns.

import { abfrage } from '../api.js';
import { mitteVerschieben, rollenderVorfahre } from '../ansichten/maus.js';
import { boxImBild, bildPunktZuUnterlage, esc } from './unterlagen-text.js';

/**
 * Bildbetrachter in host. Rückgabe: { laden(p), markieren(liste), beiKlick(fn), antwort(), leeren(text), element }.
 * laden({ unterlage, seite?, ausschnitt?, breite? }) → Antwort von unterlage_bild (oder wirft ApiFehler); die Marken bleiben, bis markieren() sie ersetzt.
 * markieren([{ box, klasse?, titel?, id? }]) – Boxen in Unterlagen-Koordinaten.
 */
export function bildBetrachter(host, { klasse = '' } = {}) {
  host.innerHTML = `<div class="fu-bild ${esc(klasse)}"><div class="fu-bild-info leise"></div><div class="fu-bild-rahmen"><img class="fu-bild-img" alt="Seite der Unterlage" draggable="false"><div class="fu-bild-ueber"></div></div></div>`;
  const wurzel = host.querySelector('.fu-bild');
  const info = wurzel.querySelector('.fu-bild-info');
  const rahmen = wurzel.querySelector('.fu-bild-rahmen');
  const img = wurzel.querySelector('img');
  const ueber = wurzel.querySelector('.fu-bild-ueber');
  let antwort = null, marken = [], klickFn = null, nr = 0, tot = false, zoomFaktor = 1;
  rahmen.hidden = true;

  function markenZeichnen() {
    if (!antwort) { ueber.innerHTML = ''; return; }
    ueber.innerHTML = marken.map((m, i) => {
      const b = boxImBild(m.box, antwort.bereich, antwort.breite, antwort.hoehe, antwort.art);
      if (!b) return '';
      const p = (z, g) => `${(z / g * 100).toFixed(3)}%`;
      return `<div class="fu-marke ${esc(m.klasse ?? '')}" data-i="${i}"${m.id ? ` data-id="${esc(m.id)}"` : ''}${m.titel ? ` title="${esc(m.titel)}"` : ''} style="left:${p(b.x, antwort.breite)};top:${p(b.y, antwort.hoehe)};width:${p(b.b, antwort.breite)};height:${p(b.h, antwort.hoehe)}"></div>`;
    }).join('');
  }

  rahmen.addEventListener('click', (e) => {
    if (!antwort || !klickFn) return;
    const r = img.getBoundingClientRect();
    if (r.width < 1) return;
    const px = ((e.clientX - r.left) / r.width) * antwort.breite, py = ((e.clientY - r.top) / r.height) * antwort.hoehe;
    klickFn({ punkt: bildPunktZuUnterlage(px, py, antwort.bereich, antwort.breite, antwort.hoehe, antwort.art), px, py, ereignis: e });
  });

  const api = {
    element: wurzel,
    antwort: () => antwort,
    async laden(p) {
      const mein = ++nr;
      info.textContent = 'Bild wird geladen …';
      let r;
      try { r = await abfrage('unterlage_bild', p); }
      catch (e) {
        if (mein === nr && !tot) { antwort = null; rahmen.hidden = true; info.innerHTML = `<span class="fehlertext">${esc(e.message)}</span>${e.vorschlag ? ` <span class="leise">${esc(e.vorschlag)}</span>` : ''}`; ueber.innerHTML = ''; }
        throw e;
      }
      if (mein !== nr || tot) return r;
      antwort = { ...r, bereich: r.bereich, breite: r.breite, hoehe: r.hoehe };
      img.onload = () => { markenZeichnen(); };
      img.src = `data:${r.mime ?? 'image/png'};base64,${r.base64}`;
      rahmen.hidden = false;
      info.textContent = `${r.datei ?? ''}${r.seite ? ` · Seite ${r.seite}${r.seiten ? ` von ${r.seiten}` : ''}` : ''} · Ausschnitt ${r.bereich.map((x) => Math.round(x * 10) / 10).join(' / ')}`;
      markenZeichnen();
      wurzel.dataset.bereich = JSON.stringify(r.bereich);
      return r;
    },
    markieren(liste) { marken = liste ?? []; markenZeichnen(); },
    /** Vergrösserung: 1 = in die Breite des Behälters eingepasst, 2 = doppelt so breit (der Behälter scrollt). */
    zoom(f) { zoomFaktor = Math.min(6, Math.max(0.5, f)); rahmen.style.width = zoomFaktor === 1 ? '' : `${zoomFaktor * 100}%`; rahmen.style.maxWidth = zoomFaktor === 1 ? '' : 'none'; rahmen.style.display = zoomFaktor === 1 ? '' : 'block'; img.style.width = zoomFaktor === 1 ? '' : '100%'; },
    zoomFaktor: () => zoomFaktor,
    /** Die Marke mit dieser Id in die Mitte des sichtbaren Bereichs rollen. */
    zuMarke(id) { ueber.querySelector(`.fu-marke[data-id="${String(id).replace(/"/g, '\\"')}"]`)?.scrollIntoView({ block: 'center', inline: 'center' }); },
    beiKlick(fn) { klickFn = fn; rahmen.classList.toggle('klickbar', !!fn); },
    leeren(text = '') { nr++; antwort = null; marken = []; rahmen.hidden = true; ueber.innerHTML = ''; info.textContent = text; },
    zerstoeren() { tot = true; nr++; },
  };

  // Mittlere Taste (N10): verschiebt das vergrösserte Bild (der Behälter rollt), Doppelklick Mitte = ganz zeigen; kein Autoscroll
  let roller = null;
  mitteVerschieben(wurzel, {
    beiBeginn: () => { roller = rollenderVorfahre(rahmen); wurzel.classList.add('schiebt'); },
    beiBewegung: (dx, dy) => { if (roller) { roller.scrollLeft -= dx; roller.scrollTop -= dy; } },
    beiEnde: () => { roller = null; wurzel.classList.remove('schiebt'); },
    beiDoppelklick: () => { api.zoom(1); const r = rollenderVorfahre(rahmen); if (r) { r.scrollLeft = 0; r.scrollTop = 0; } },
  });
  return api;
}
