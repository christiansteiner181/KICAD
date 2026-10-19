// «Quelle ansehen» (Modul 2, C2): zu einer Befund-Id den Befund (Datei, Seite, Stelle, Wert) und das Bild der Unterlage mit dem markierten Befund
// zeigen (Abfragen befunde und unterlage_bild; Ausschnitt = Box des Befunds mit Rand, auf Wunsch die ganze Seite). Gebraucht von der Prüfansicht
// (Abdeckung, Konfliktliste, Massvergleich) und vom Befund-Browser. Rechnet nichts: Box, Rand und Markierung folgen dem Kern (unterlagen-text.js).

import { abfrage } from '../api.js';
import { zeigen } from '../oberflaeche/dialoge.js';
import { knoten } from '../oberflaeche/werkzeug.js';
import { bildBetrachter } from './unterlagen-bild.js';
import { unterlageZuBefund, befundAusschnitt, befundText, befundStelle, befundVolltext, quellenZeile, quellenHtml, ohnePruefsumme, ART_NAMEN, urlLink, esc } from './unterlagen-text.js';

// Die reinen Texte (befundVolltext, quellenZeile, quellenHtml) stehen in unterlagen-text.js (in Node getestet)
export { befundVolltext, quellenZeile, quellenHtml };

const cache = new Map();   // Befund-Id → { befund, unterlage: { art, datei } }

/** Der Befund zu einer Id (zwischengespeichert; Befunde ändern sich nicht, solange die Datei gleich ist). Wirft Fehler mit Text. */
export async function befundLaden(kandidaten, id) {
  if (cache.has(id)) return cache.get(id);
  const u = unterlageZuBefund(kandidaten, id);
  if (!u) throw Object.assign(new Error(`Die Unterlage zum Befund ${id} liegt nicht im Projekt.`), { vorschlag: 'Die Datei mit «Familie aus Unterlagen» oder im Befund-Browser wieder hinzufügen.' });
  const r = await abfrage('befunde', { unterlage: u.ref, id, proSeite: 1 });
  const b = (r.befunde ?? [])[0];
  if (!b) throw new Error(`Befund ${id} gibt es in «${u.datei}» nicht (mehr).`);
  const erg = { befund: b, unterlage: { ref: u.ref, art: r.art, datei: r.datei ?? u.datei, url: u.url ?? '', abgerufen: u.abgerufen ?? '' } };
  cache.set(id, erg);
  return erg;
}

/** Befunde mehrerer Ids für eine Anzeige: [{ id, befund?, unterlage?, fehler? }] (Fehler je Id, nie eine Ausnahme). */
export async function befundeLaden(kandidaten, ids) {
  return Promise.all((ids ?? []).map(async (id) => {
    try { return { id, ...(await befundLaden(kandidaten, id)) }; }
    catch (e) { return { id, fehler: e.message }; }
  }));
}

/** Dialog «Quelle ansehen». */
export async function quelleAnsehen(kandidaten, befundId) {
  const w = knoten(`<div class="fu-quelle"><div class="fu-quelle-kopf"><p class="leise">Befund wird geladen …</p></div>
    <label class="fu-quelle-ganz"><input type="checkbox" class="fu-ganz"> ganze Seite zeigen</label><div class="fu-quelle-bild"></div></div>`);
  const kopf = w.querySelector('.fu-quelle-kopf');
  const viewer = bildBetrachter(w.querySelector('.fu-quelle-bild'));
  let geladen = null;

  async function bild() {
    if (!geladen) return;
    const { befund: b, unterlage: u } = geladen;
    if (u.art === 'html') return;   // Webseiten haben kein Seitenbild: Text und Link stehen oben
    const ganz = w.querySelector('.fu-ganz').checked;
    const aus = ganz ? null : befundAusschnitt(b.box, u.art);
    const p = { unterlage: u.ref, breite: aus ? 900 : 1100 };
    if (u.art === 'pdf' && b.seite) p.seite = b.seite;
    if (aus) p.ausschnitt = aus;
    try {
      await viewer.laden(p);
      viewer.markieren(b.box ? [{ box: b.box, klasse: 'gewaehlt', titel: `${b.art} ${b.id}` }] : []);
      if (!b.box) w.querySelector('.fu-bild-info').textContent += ' · dieser Befund hat keine Box (nur die Seite)';
    } catch { /* die Meldung steht im Betrachter */ }
  }
  w.querySelector('.fu-ganz').onchange = bild;

  const dialog = zeigen({ titel: 'Quelle ansehen', inhalt: w, breite: 880, klasse: 'familie fu-dlg', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
  try {
    geladen = await befundLaden(kandidaten, befundId);
    const { befund: b, unterlage: u } = geladen;
    const zeilen = [['Datei', ohnePruefsumme(u.datei)], ['Art der Datei', ART_NAMEN[u.art] ?? u.art], ['Befund', b.id], ['Art des Befunds', b.art], ['Stelle', befundStelle(b) || '–'],
      ['Wert', `${befundText(b) || '–'}`], ['Box', b.box ? b.box.map((x) => Math.round(x * 100) / 100).join(', ') : '–'], ['Sicherheit', b.sicherheit !== undefined ? String(b.sicherheit) : '1']];
    const html = u.art === 'html';
    if (u.url) zeilen.splice(1, 0, ['Aus dem Internet', null, urlLink(u.url) + (u.abgerufen ? ` <span class="leise">(abgerufen ${esc(u.abgerufen)})</span>` : '')]);
    kopf.innerHTML = `<table class="wtab fu-quelle-tab">${zeilen.map(([k, v, h]) => `<tr><th>${esc(k)}</th><td>${h ?? esc(v)}</td></tr>`).join('')}</table>`
      + (html ? `<div class="fu-quelle-html"><div class="fu-quellen-kopf">Text der Webseite an dieser Stelle</div><pre class="fu-bericht">${esc(befundVolltext(b))}</pre><p class="leise">Webseiten haben kein Seitenbild: massgebend ist der gelesene Text samt Tabellenzelle.</p></div>` : '');
    if (html) { w.querySelector('.fu-quelle-ganz').hidden = true; w.querySelector('.fu-quelle-bild').hidden = true; }
    await bild();
  } catch (e) {
    kopf.innerHTML = `<p class="fehlertext">${esc(e.message)}</p>${e.vorschlag ? `<p class="leise">${esc(e.vorschlag)}</p>` : ''}`;
  }
  await dialog;
  viewer.zerstoeren();
}
