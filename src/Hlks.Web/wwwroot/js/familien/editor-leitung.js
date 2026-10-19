// Familieneditor (Modul 3, C3): die Seite «Leitungsserie» für Familien der Kategorie Rohr bzw. Kanal (Block «leitung» der Definition) und die Kontrolltabelle aus
// der Abfrage `leitungsserie`. Die Seite ist reines HTML aus der Definition (kein DOM-Zugriff, in Node getestet); Änderungen gehen über den Editor als
// op `leitung_setzen` an familie_aendern (der ganze Block wird ersetzt). Der Editor rechnet nichts: ob Parameter, Formeln, Typregeln und Winkel gelten, sagt der Kern
// (Trockenlauf bei jeder Eingabe), und die Kontrolltabelle zeigt, was der Kern über Grössen und Formteile sagt.

import { esc, feldText, leitungsArt, leitungFelder, leitungAenderung, groesseText, serieZellen, serieGruende } from './familien-text.js';
import { eingabeFeld, auswahlFeld, tabelle, leerHinweis, formZeile } from './editor-bausteine.js';

/** Ein Feld des Formulars als Zeile «Beschriftung – Eingabe»; jedes Feld trägt data-pfad und data-ftyp (der Editor liest sie bei Änderungen). */
export function feldHtml(f) {
  const fk = `l:${f.pfad}`;
  const inner = f.typ === 'auswahl'
    ? auswahlFeld(f.optionen, f.wert, { fk, leer: f.leer, titel: f.titel })
    : eingabeFeld(f.text ?? '', { fk, live: true, platzhalter: f.platzhalter, titel: f.titel, deaktiviert: f.deaktiviert, klasse: f.typ === 'zahl' || f.typ === 'zahlopt' || f.typ === 'typregel' ? 'formel' : '' });
  const hinweis = f.hinweis ? `<span class="leise fe-feldhinweis">${esc(f.hinweis)}</span>` : '';
  return formZeile(f.label, `<div class="fe-feld" data-pfad="${esc(f.pfad)}" data-ftyp="${esc(f.typ)}">${inner}${hinweis}</div>`, { titel: f.titel });
}

/** Das Formular (Querschnitt, Grösse, Masse, Formteile, Regeln) aus der Definition. */
export function formularHtml(def, o = {}) {
  const felder = leitungFelder(def, o);
  let gruppe = null;
  const teile = [];
  for (const f of felder) {
    if (f.gruppe !== gruppe) { gruppe = f.gruppe; teile.push(`<h4 class="fe-abschnitt">${esc(gruppe)}</h4>`); }
    teile.push(feldHtml(f));
  }
  return `<div class="fe-formular fe-leitung-formular">${teile.join('')}</div>`;
}

const ZELLEN = [['bogen', 'Bogen'], ['tstueck', 'T-Stück'], ['reduktion', 'Reduktion'], ['reinigungsoeffnung', 'Reinigungsöffnung']];

/** Kontrolltabelle aus der Antwort der Abfrage leitungsserie: je Grösse aussen, innen, wand und ob Bogen, T-Stück, Reduktion und Reinigungsöffnung verfügbar sind (mit Grund). */
export function kontrolleHtml(serie) {
  if (!serie) return '<h4 class="fe-abschnitt">Kontrolle</h4><p class="fe-leer leise">Kontrolltabelle wird geladen …</p>';
  if (serie.fehler) {
    return `<h4 class="fe-abschnitt">Kontrolle</h4><p class="fe-kontrolle-fehler fehlertext">Der Kern kann die Serie noch nicht auswerten: ${esc(serie.fehler)}${serie.vorschlag ? ` <span class="leise">– ${esc(serie.vorschlag)}</span>` : ''}</p>`;
  }
  const groessen = serie.groessen ?? [];
  const zahl = (v) => (v === null || v === undefined ? '–' : esc(feldText(v)));
  const zeilen = groessen.map((g) => {
    if (g.gueltig === false) {
      return `<tr class="fe-ungueltig" data-typ="${esc(g.typ)}"><td>${esc(g.typ)}</td><td colspan="${3 + 1 + ZELLEN.length}" class="fehlertext" title="${esc(g.fehler)}">nicht auswertbar: ${esc(g.fehler)}${/keinen Wert/.test(g.fehler ?? '') ? ' <span class="leise">→ Werte auf der Seite «Typen» eintragen (aus der Unterlage)</span>' : ''}</td></tr>`;
    }
    const z = serieZellen(g);
    return `<tr data-typ="${esc(g.typ)}"><td>${esc(g.typ)}</td><td>${esc(groesseText(g))}</td><td class="fe-zahl">${zahl(g.aussen)}</td><td class="fe-zahl">${zahl(g.innen)}</td><td class="fe-zahl">${zahl(g.wand)}</td>`
      + ZELLEN.map(([k]) => `<td class="fe-kz fe-${z[k].zustand}" data-art="${k}" title="${esc(z[k].titel)}">${esc(z[k].text)}</td>`).join('') + '</tr>';
  });
  const zahlKopf = (t) => ({ html: esc(t), attr: { class: 'fe-zahl' } });
  const kopf = ['Typ', 'Grösse', zahlKopf('aussen [mm]'), zahlKopf('innen [mm]'), zahlKopf('wand [mm]'), ...ZELLEN.map(([, t]) => ({ html: esc(t), attr: { class: 'fe-kz' } }))];
  const gruende = serieGruende(serie);
  const liste = gruende.length === 0
    ? (groessen.some((g) => g.gueltig !== false) ? '<p class="huelle-ok fe-kontrolle-ok">Alle Formteile sind für alle Grössen verfügbar.</p>' : '')
    : `<p class="leise fe-kontrolle-kopf">Nicht verfügbar (${gruende.length}):</p><ul class="fe-gruende">${gruende.map((g) => `<li data-art="${esc(g.art)}"><strong>${esc(g.label)}</strong> – ${g.alle ? 'alle Grössen' : esc(g.groessen.join(', '))}: ${g.mehrere ? 'z. B. ' : ''}${esc(g.grund)}${g.vorschlag ? ` <span class="leise">→ ${esc(g.vorschlag)}</span>` : ''}</li>`).join('')}</ul>`;
  const kopfzeile = `${esc(serie.kategorie ?? '')}, Querschnitt ${esc(serie.querschnitt ?? '')} · Bogenwinkel ${Array.isArray(serie.winkel) ? esc(serie.winkel.join(', ')) + '°' : serie.winkelRegel === 'keine' ? 'nicht angegeben (jeder Winkel)' : esc(serie.winkel ?? '')} · Abzweigwinkel ${esc((serie.abzweigwinkel ?? []).join(', '))}°`;
  return `<h4 class="fe-abschnitt">Kontrolle (Abfrage leitungsserie)</h4><p class="leise fe-kontrolle-kopf">${kopfzeile}</p>`
    + (groessen.length ? tabelle(kopf, zeilen, { klasse: 'fe-kontrolle' }) : leerHinweis('Die Serie hat noch keine Grösse (Typ).')) + liste;
}

export const leitungTab = {
  id: 'leitung',
  titel: 'Leitungsserie',
  nurSerie: true,
  zeichne(ctx) {
    const art = leitungsArt(ctx.def.kategorie, ctx.klassen?.());
    const o = { art, familien: ctx.familien?.() ?? [] };
    return `<div class="fe-werkzeuge"><span class="leise">Leitungsserie ${esc(ctx.def.kategorie ?? '')} · die Typen sind die Grössen, die Werte kommen aus der Unterlage (Parameter und Typen). Jede Eingabe prüft der Kern.</span></div>`
      + formularHtml(ctx.def, o) + kontrolleHtml(ctx.serie?.());
  },
  aenderung(el, ctx) {
    const box = el.closest('.fe-feld');
    if (!box) return null;
    const art = leitungsArt(ctx.def.kategorie, ctx.klassen?.());
    const r = leitungAenderung(ctx.def, art, box.dataset.pfad, box.dataset.ftyp, el.value);
    return r.fehler ? { fehler: r.fehler } : { ops: r.ops };
  },
  klick() { return null; },
};
