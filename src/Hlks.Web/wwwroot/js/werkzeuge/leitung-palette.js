// Eigenschaftenpalette für Leitungen (Modul 3, C1) – HTML-Bausteine, rein und in Node getestet (leitung.test.mjs).
//   - Rohr/Kanal mit Serie: Gruppe «Leitung» mit Serie, Grösse (Änderung → leitung_groesse bzw. leitung_serie, Rückfrage «nur diese Leitung /
//     ganzer Zug» in leitung-bearbeiten.js), Gefälle, Dämmung (Art) und Dämmdicke; dazu eine Zeile mit den Summen des Leitungszugs;
//   - Rohr/Kanal ohne Serie (Altbestand, z. B. aus IFC): Hinweis und Knopf «Aufteilen mit Serie …» (→ leitung_aufteilen);
//   - Formteile (Bogen, T-Stück, Reduktion, Reinigungsöffnung): Gruppe «Formteil» mit Art, Familie, Typ und Winkel.
// Die Palette ändert nie selbst: die Felder tragen data-ziel «lt:serie» und «lt:groesse» (eigenschaften.js ruft leitung-bearbeiten.js);
// Gefälle, Dämmung und Dämmdicke sind gewöhnliche Eigenschaften («eig:…») und gehen als element_aendern an den Kern.
// Die Bausteine zeile/gruppe/nurLesen liefert eigenschaften.js (Parameter H), damit das Aussehen einheitlich bleibt.

import { esc } from './schicht.js';
import { groessenListe, hatSerie, istLeitungsklasse, istFormteilklasse, kategorieVon, mmText } from './leitung-logik.js';

const zahl = (w) => (w === null || w === undefined || w === '' ? '' : String(Math.round(Number(w) * 1000) / 1000));
const wertVon = (l, name) => l?.eigenschaften?.[name]?.wert;

/** Eigenschaften der Leitung, die die Gruppe «Leitung» zeigt (die allgemeine Liste lässt sie aus). */
export const LEITUNG_EIGENSCHAFTEN = ['dn', 'gefaelle', 'daemmung', 'daemmdicke'];

const optionen = (liste, aktuell) => {
  const l = liste.some(([w]) => w === aktuell) || aktuell === null || aktuell === undefined || aktuell === '' ? liste : [...liste, [aktuell, `${aktuell} (gibt es nicht mehr)`]];
  return l.map(([w, t]) => `<option value="${esc(w)}"${w === aktuell ? ' selected' : ''}>${esc(t)}</option>`).join('');
};

/** Zeile mit den Summen des Leitungszugs (Antwort leitungszug) oder ''. */
export function zugText(zug) {
  const s = zug?.summe;
  if (!s) return '';
  const g = s.leitungen ?? 0, f = s.formteile ?? 0;
  return `${g} ${g === 1 ? 'Gerade' : 'Geraden'}, ${f} ${f === 1 ? 'Formteil' : 'Formteile'}, ${mmText(s.laengeMm ?? 0)}`;
}

/**
 * Gruppe «Leitung» einer Gerade mit Serie.
 * d = { el, l (element_lesen), f (Antwort familie der Serie), serie (Antwort leitungsserie oder null), serien ([{ kz, name }] der Kategorie), zug (Antwort leitungszug oder null) }
 */
export function leitungGruppe(d, H) {
  const { el, l, f, serie, serien, zug } = d;
  const typ = wertVon(l, 'familientyp') ?? el.werte?.familientyp ?? null;
  const gr = serie ? groessenListe(serie).map((g) => [g.typ, g.typ]) : (f?.typen ?? []).map((t) => [t.name ?? t, t.name ?? t]);
  const aktuell = serie ? groessenListe(serie).find((g) => g.typ === typ) : null;
  const zeilen = [];
  const sl = (serien?.length ? serien : [[f?.kennzeichen, f?.name]]).map((s) => (Array.isArray(s) ? s : [s.kz, s.name]));
  zeilen.push(H.zeile('Serie', `<select data-ziel="lt:serie" data-typ="auswahl">${optionen(sl, f?.kennzeichen ?? el.bezuege?.familie ?? null)}</select>`
    + `<button type="button" class="ep-fam-knopf" data-aktion="fam-editor" data-fam="${esc(f?.kennzeichen ?? el.bezuege?.familie ?? '')}" title="Leitungsserie im Editor öffnen">Editor</button>`,
    { titel: 'Leitungsserie (Familie der Kategorie Rohr bzw. Kanal). Eine Änderung ersetzt die Formteile der Leitung (Rückfrage: nur diese Leitung oder ganzer Zug).' }));
  zeilen.push(H.zeile('Grösse', `<select data-ziel="lt:groesse" data-typ="auswahl">${optionen(gr, typ)}</select>`,
    { titel: 'Grösse der Serie. Eine Änderung führt Bögen und Reduktionen nach (Rückfrage: nur diese Leitung oder ganzer Zug).' }));
  if (aktuell) {
    const masse = [aktuell.aussen != null ? `aussen ${zahl(aktuell.aussen)}` : null, aktuell.innen != null ? `innen ${zahl(aktuell.innen)}` : null, aktuell.wand != null ? `Wand ${zahl(aktuell.wand)}` : null].filter(Boolean).join(' · ');
    if (masse) zeilen.push(H.zeile('Mass', H.nurLesen(`${masse} mm`), { titel: 'Aus der Serie (mit Quelle in der Familie)', klasse: 'klein' }));
  }
  zeilen.push(H.zeile('Gefälle', `<input type="text" inputmode="decimal" data-ziel="eig:gefaelle" data-typ="zahl" value="${esc(zahl(wertVon(l, 'gefaelle')))}"><span class="ep-einheit">%</span>`, { titel: 'Gefälle in Fliessrichtung (Schwerkraftleitungen)' }));
  zeilen.push(H.zeile('Dämmung', `<input type="text" data-ziel="eig:daemmung" data-typ="text" value="${esc(wertVon(l, 'daemmung') ?? '')}" placeholder="Art / Produkt">`, { titel: 'Art der Dämmung' }));
  zeilen.push(H.zeile('Dämmdicke', `<input type="text" inputmode="decimal" data-ziel="eig:daemmdicke" data-typ="zahl" value="${esc(zahl(wertVon(l, 'daemmdicke')))}"><span class="ep-einheit">mm</span>`, { titel: 'Dämmdicke (zweite Kontur im Plan)' }));
  const zt = zugText(zug);
  if (zt) zeilen.push(H.zeile('Leitungszug', H.nurLesen(zt), { titel: 'Zusammenhängende Leitungen über Bögen und Reduktionen bis zum nächsten T-Stück, Gerät oder offenen Ende', klasse: 'klein' }));
  return H.gruppe('Leitung', zeilen);
}

/** Hinweis und Knopf für eine Leitung ohne Serie (Altbestand). */
export function altbestandGruppe(d, H) {
  const { el } = d;
  const art = el.klasse === 'Kanal' ? 'Kanal' : 'Rohr';
  return H.gruppe('Leitung', [
    H.zeile('Serie', H.nurLesen('keine (Polylinie aus dem Bestand)'), { titel: `Diese ${art === 'Kanal' ? 'Leitung' : 'Leitung'} hat keine Leitungsserie: sie wird nicht massstäblich gezeichnet und hat keine Formteile.` }),
    H.zeile('', `<button type="button" class="ep-lt-knopf" data-aktion="lt-aufteilen" title="Zerlegt die Polylinie in Geraden mit Bögen der gewählten Serie (Befehl leitung_aufteilen)">Aufteilen mit Serie …</button>`,
      { titel: 'leitung_aufteilen: Geraden und Bögen nach einer Leitungsserie' }),
  ]);
}

/** Gruppe «Formteil»: Art, Familie, Typ, Winkel (nur lesen; der Typ steht auch in der Gruppe «Familie»). */
export function formteilGruppe(d, H) {
  const { el, l, f } = d;
  const zeilen = [H.zeile('Art', H.nurLesen(el.klasse === 'TStueck' ? 'T-Stück' : el.klasse === 'Reinigungsoeffnung' ? 'Reinigungsöffnung' : el.klasse))];
  if (f) zeilen.push(H.zeile('Familie', H.nurLesen(f.name ?? f.kennzeichen)));
  const typ = wertVon(l, 'familientyp') ?? el.werte?.familientyp;
  if (typ) zeilen.push(H.zeile('Typ', H.nurLesen(typ)));
  const w = wertVon(l, 'winkel') ?? el.werte?.winkel;
  if (w !== undefined && w !== null) zeilen.push(H.zeile('Winkel', H.nurLesen(`${zahl(w)}°`), { titel: 'Umlenkwinkel des Bogens (Exemplarwert der Familie)' }));
  return H.gruppe('Formteil', zeilen);
}

/** Welche Gruppen gehören zu diesem Element? 'leitung' | 'altbestand' | 'formteil' | null. */
export function leitungsArt(el) {
  if (istLeitungsklasse(el?.klasse)) return hatSerie(el) ? 'leitung' : 'altbestand';
  if (istFormteilklasse(el?.klasse)) return 'formteil';
  return null;
}

export { kategorieVon };
