// Familieneditor (Modul 3, C3): die Seite «Leitungsserie» für Familien der Kategorie Rohr bzw. Kanal (Block «leitung» der Definition) und die Kontrolltabelle aus
// der Abfrage `leitungsserie`. Die Seite ist reines HTML aus der Definition (kein DOM-Zugriff, in Node getestet); Änderungen gehen über den Editor als
// op `leitung_setzen` an familie_aendern (der ganze Block wird ersetzt). Der Editor rechnet nichts: ob Parameter, Formeln, Typregeln und Winkel gelten, sagt der Kern
// (Trockenlauf bei jeder Eingabe), und die Kontrolltabelle zeigt, was der Kern über Grössen und Formteile sagt.
// Nachtrag freie Grösse und Abwasser-Serie: Felder für Einsatz, Grössenart, den Block `frei` (Vorlage, Grenzen je Seite, Schritt) und die Vorlage der Formteile.
// Die Abfrage leitungsserie rechnet alle Grössen mit allen Formteilen und dauert bei einer grossen freien Serie bis 155 s: bei einer freien Serie wird die Kontrolle
// deshalb nie ungefragt geholt, sondern auf Knopfdruck (mit Anzeige der Laufzeit und «Abbrechen»; die Oberfläche bleibt bedienbar). Sie zeigt dann auch die Probe der
// Abfrage (eine freie Grösse samt Formteilverfügbarkeit).

import { esc, feldText, leitungsArt, leitungFelder, leitungAenderung, groesseText, serieZellen, serieGruende } from './familien-text.js';
import { eingabeFeld, auswahlFeld, tabelle, leerHinweis, formZeile, knopf } from './editor-bausteine.js';

/** Ein Feld des Formulars als Zeile «Beschriftung – Eingabe»; jedes Feld trägt data-pfad und data-ftyp (der Editor liest sie bei Änderungen). */
export function feldHtml(f) {
  const fk = `l:${f.pfad}`;
  const inner = f.typ === 'auswahl' || f.typ === 'mischbar'
    ? auswahlFeld(f.optionen, f.wert, { fk, leer: f.leer, titel: f.titel, extra: f.deaktiviert ? { disabled: true } : undefined })
    : eingabeFeld(f.text ?? '', { fk, live: true, platzhalter: f.platzhalter, titel: f.titel, deaktiviert: f.deaktiviert, klasse: ['zahl', 'zahlopt', 'typregel', 'masse', 'ganzzahl'].includes(f.typ) ? 'formel' : '' });
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

const zahlZelle = (v) => (v === null || v === undefined ? '–' : esc(feldText(v)));

/** Die Probe der Abfrage leitungsserie (freie Serie): eine freie Grösse mit Massen und der Formteilverfügbarkeit – eine Zeile in der Form der Kontrolltabelle. */
export function probeHtml(serie) {
  const p = serie?.probe;
  if (!p) return '';
  const kopf = `<h4 class="fe-abschnitt">Probe mit einer freien Grösse</h4>`;
  if (p.gueltig === false) return `${kopf}<p class="fe-probe-fehler fehlertext">Die Probegrösse ${esc(p.text ?? '')} nimmt die Serie nicht: ${esc(p.grund ?? '')}</p>`;
  const z = serieZellen(p);
  const zahlKopf = (t) => ({ html: esc(t), attr: { class: 'fe-zahl' } });
  const k = ['Grösse', 'Typ (Vorlage)', zahlKopf('aussen [mm]'), zahlKopf('innen [mm]'), zahlKopf('wand [mm]'), ...ZELLEN.map(([, t]) => ({ html: esc(t), attr: { class: 'fe-kz' } }))];
  const zeile = `<tr class="fe-probe" data-typ="${esc(p.typ ?? '')}"><td>${esc(p.text ?? '')}</td><td>${esc(p.typ ?? '')}${p.frei ? ' <span class="leise">(frei)</span>' : ''}</td>`
    + `<td class="fe-zahl">${zahlZelle(p.aussen)}</td><td class="fe-zahl">${zahlZelle(p.innen)}</td><td class="fe-zahl">${zahlZelle(p.wand)}</td>`
    + ZELLEN.map(([a]) => `<td class="fe-kz fe-${z[a].zustand}" data-art="${a}" title="${esc(z[a].titel)}">${esc(z[a].text)}</td>`).join('') + '</tr>';
  return `${kopf}<p class="leise fe-kontrolle-kopf">Der Kern rechnet eine Grösse zwischen den Normgrössen (Mitte der Grenzen, sonst 430 × 270) mit den Formteilen der Vorlage – zeigt, ob freie Grössen im Plan funktionieren.</p>`
    + tabelle(k, [zeile], { klasse: 'fe-kontrolle fe-probe-tabelle' });
}

/** Kontrolle einer freien Serie, die der Kern noch nicht gerechnet hat bzw. gerade rechnet (o.lauf: { zustand: 'laedt' | 'abgebrochen', sekunden }). */
function kontrolleAufAbruf(o) {
  const lauf = o.lauf ?? {};
  const kopf = '<h4 class="fe-abschnitt">Kontrolle (Abfrage leitungsserie) – auf Abruf</h4>';
  if (lauf.zustand === 'laedt') {
    return `${kopf}<div class="fe-kontrolle-lauf" data-zustand="laedt" role="status"><span class="fe-spinner" aria-hidden="true"></span>`
      + `<span>Der Kern rechnet alle Grössen mit allen Formteilen … <b class="fe-kontrolle-zeit">${esc(lauf.sekunden ?? 0)} s</b></span>${knopf('Abbrechen', 'kontrolle-abbrechen', { titel: 'Bricht das Warten ab; die Oberfläche bleibt bedienbar (der Kern rechnet im Hintergrund zu Ende)' })}</div>`
      + '<p class="leise fe-kontrolle-kopf">Das kann bei einer grossen freien Serie mehrere Minuten dauern. Die Seite lässt sich in der Zwischenzeit weiter bedienen.</p>';
  }
  return `${kopf}<div class="fe-kontrolle-lauf" data-zustand="${lauf.zustand === 'abgebrochen' ? 'abgebrochen' : 'bereit'}">`
    + `<span class="leise">${lauf.zustand === 'abgebrochen' ? 'Abgebrochen – nichts wurde geholt.' : 'Die Kontrolltabelle wird nicht von selbst geholt.'}</span>${knopf(lauf.zustand === 'abgebrochen' ? 'Noch einmal berechnen' : 'Kontrolltabelle und Probe berechnen', 'kontrolle-laden', { titel: 'Fragt den Kern: je Grösse Masse und Formteilverfügbarkeit, dazu eine Probe mit einer freien Grösse' })}</div>`
    + '<p class="leise fe-kontrolle-kopf">Der Kern rechnet für jede Grösse (Typ) Bogen, T-Stück, Reduktionen auf alle kleineren Grössen und Reinigungsöffnung. Bei einer grossen freien Serie dauert das lange – deshalb nur auf Knopfdruck.</p>';
}

/**
 * Kontrolltabelle aus der Antwort der Abfrage leitungsserie: je Grösse aussen, innen, wand und ob Bogen, T-Stück, Reduktion und Reinigungsöffnung verfügbar sind (mit Grund).
 * o (freie Serien): { aufAbruf: true, lauf: { zustand, sekunden }, veraltet } – ohne Antwort erscheint der Knopf bzw. die Laufanzeige statt «wird geladen»; mit Antwort
 * steht über der Tabelle der Knopf «Neu berechnen» (veraltet: die Definition wurde seit der Berechnung geändert).
 */
export function kontrolleHtml(serie, o = {}) {
  if (!serie) return o.aufAbruf ? kontrolleAufAbruf(o) : '<h4 class="fe-abschnitt">Kontrolle</h4><p class="fe-leer leise">Kontrolltabelle wird geladen …</p>';
  if (serie.fehler) {
    return `<h4 class="fe-abschnitt">Kontrolle</h4><p class="fe-kontrolle-fehler fehlertext">Der Kern kann die Serie noch nicht auswerten: ${esc(serie.fehler)}${serie.vorschlag ? ` <span class="leise">– ${esc(serie.vorschlag)}</span>` : ''}</p>`
      + (o.aufAbruf ? `<div class="fe-kontrolle-lauf">${knopf('Noch einmal berechnen', 'kontrolle-laden')}</div>` : '');
  }
  const groessen = serie.groessen ?? [];
  const einsatz = !!serie.einsatz;
  const zeilen = groessen.map((g) => {
    if (g.gueltig === false) {
      return `<tr class="fe-ungueltig" data-typ="${esc(g.typ)}"><td>${esc(g.typ)}</td><td colspan="${3 + 1 + ZELLEN.length + (einsatz ? 2 : 0)}" class="fehlertext" title="${esc(g.fehler)}">nicht auswertbar: ${esc(g.fehler)}${/keinen Wert/.test(g.fehler ?? '') ? ' <span class="leise">→ Werte auf der Seite «Typen» eintragen (aus der Unterlage)</span>' : ''}</td></tr>`;
    }
    const z = serieZellen(g);
    return `<tr data-typ="${esc(g.typ)}"><td>${esc(g.typ)}</td><td>${esc(groesseText(g, serie))}</td><td class="fe-zahl">${zahlZelle(g.aussen)}</td><td class="fe-zahl">${zahlZelle(g.innen)}</td><td class="fe-zahl">${zahlZelle(g.wand)}</td>`
      + (einsatz ? `<td class="fe-beschriftung">${g.beschriftung ? esc(g.beschriftung) : '–'}</td><td class="fe-zahl">${zahlZelle(g.dnHersteller)}</td>` : '')
      + ZELLEN.map(([k]) => `<td class="fe-kz fe-${z[k].zustand}" data-art="${k}" title="${esc(z[k].titel)}">${esc(z[k].text)}</td>`).join('') + '</tr>';
  });
  const zahlKopf = (t) => ({ html: esc(t), attr: { class: 'fe-zahl' } });
  const kopf = ['Typ', 'Grösse', zahlKopf('aussen [mm]'), zahlKopf('innen [mm]'), zahlKopf('wand [mm]'),
    ...(einsatz ? ['Beschriftung', { html: esc('DN Hersteller'), attr: { class: 'fe-zahl', title: 'DN laut Herstellerunterlage (Typparameter mit Ziel dn) – nie aus dem Aussendurchmesser abgeleitet' } }] : []),
    ...ZELLEN.map(([, t]) => ({ html: esc(t), attr: { class: 'fe-kz' } }))];
  const gruende = serieGruende(serie);
  const liste = gruende.length === 0
    ? (groessen.some((g) => g.gueltig !== false) ? '<p class="huelle-ok fe-kontrolle-ok">Alle Formteile sind für alle Grössen verfügbar.</p>' : '')
    : `<p class="leise fe-kontrolle-kopf">Nicht verfügbar (${gruende.length}):</p><ul class="fe-gruende">${gruende.map((g) => `<li data-art="${esc(g.art)}"><strong>${esc(g.label)}</strong> – ${g.alle ? 'alle Grössen' : esc(g.groessen.join(', '))}: ${g.mehrere ? 'z. B. ' : ''}${esc(g.grund)}${g.vorschlag ? ` <span class="leise">→ ${esc(g.vorschlag)}</span>` : ''}</li>`).join('')}</ul>`;
  const kopfzeile = `${esc(serie.kategorie ?? '')}, Querschnitt ${esc(serie.querschnitt ?? '')}${serie.einsatz ? ` · Einsatz ${esc(serie.einsatz === 'abwasser' ? 'Abwasser' : serie.einsatz)}${serie.nachAussendurchmesser ? ' (Grösse nach Aussendurchmesser)' : ''}` : ''}`
    + ` · Bogenwinkel ${Array.isArray(serie.winkel) ? esc(serie.winkel.join(', ')) + '°' : serie.winkelRegel === 'keine' ? 'nicht angegeben (jeder Winkel)' : esc(serie.winkel ?? '')} · Abzweigwinkel ${esc((serie.abzweigwinkel ?? []).join(', '))}°`
    + (serie.frei?.text ? ` · ${esc(serie.frei.text)}` : '');
  const neu = o.aufAbruf
    ? `<div class="fe-kontrolle-lauf" data-zustand="fertig">${o.veraltet ? '<span class="fe-kontrolle-veraltet fehlertext">Stand vor der letzten Änderung der Definition.</span> ' : ''}${knopf('Neu berechnen', 'kontrolle-laden', { titel: 'Fragt den Kern noch einmal (bei einer grossen freien Serie dauert das lange)' })}</div>`
    : '';
  return `<h4 class="fe-abschnitt">Kontrolle (Abfrage leitungsserie)</h4><p class="leise fe-kontrolle-kopf">${kopfzeile}</p>${neu}`
    + (groessen.length ? tabelle(kopf, zeilen, { klasse: 'fe-kontrolle' }) : leerHinweis('Die Serie hat noch keine Grösse (Typ).')) + liste + probeHtml(serie);
}

export const leitungTab = {
  id: 'leitung',
  titel: 'Leitungsserie',
  nurSerie: true,
  zeichne(ctx) {
    const art = leitungsArt(ctx.def.kategorie, ctx.klassen?.());
    const o = { art, familien: ctx.familien?.() ?? [] };
    const frei = !!ctx.def.leitung?.frei;
    return `<div class="fe-werkzeuge"><span class="leise">Leitungsserie ${esc(ctx.def.kategorie ?? '')} · die Typen sind die Grössen, die Werte kommen aus der Unterlage (Parameter und Typen). Jede Eingabe prüft der Kern.</span></div>`
      + formularHtml(ctx.def, o) + kontrolleHtml(ctx.serie?.(), frei ? { aufAbruf: true, ...(ctx.kontrolle?.() ?? {}) } : {});
  },
  aenderung(el, ctx) {
    const box = el.closest('.fe-feld');
    if (!box) return null;
    const art = leitungsArt(ctx.def.kategorie, ctx.klassen?.());
    const r = leitungAenderung(ctx.def, art, box.dataset.pfad, box.dataset.ftyp, el.value);
    return r.fehler ? { fehler: r.fehler } : { ops: r.ops };
  },
  /** Knöpfe der Kontrolle: die Abfrage leitungsserie holen bzw. abbrechen (nur bei freien Serien; der Editor führt es aus). */
  klick(k, ctx) {
    if (k.dataset.aktion === 'kontrolle-laden') return { eigen: () => ctx.kontrolleLaden() };
    if (k.dataset.aktion === 'kontrolle-abbrechen') return { eigen: () => ctx.kontrolleAbbrechen() };
    return null;
  },
};
