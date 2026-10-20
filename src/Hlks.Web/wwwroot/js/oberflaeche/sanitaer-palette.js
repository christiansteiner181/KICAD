// HTML-Bausteine der Eigenschaftenpalette für die Sanitär-Belastungswerte (Modul 4 Teil A-O): Auswahl der Apparateart mit Namen, die abgeleiteten Werte LU KW / LU WW / DU
// mit «?», die Belastungssummen einer Leitung, das Feld der Abflusskennzahl K mit Hinweis und die Tabelle der Spezialinstallationen. Rein (ohne DOM, in Node getestet,
// sanitaer-belastung.test.mjs); die Bausteine zeile/nurLesen liefert eigenschaften.js (Parameter H), damit das Aussehen einheitlich bleibt. Die Palette ändert nie selbst:
// die Felder tragen data-ziel «eig:<Eigenschaft>» und gehen als element_aendern an den Kern. Zahlen, Namen und Quellen kommen vom Kern; Formate in sanitaer-text.js.

import { esc } from './werkzeug.js';
import * as T from './sanitaer-text.js';

/** Eigenschaft sanitaerArt: Auswahl mit Namen («Vorgabe der Klasse (…)» für nicht gesetzt). */
export function artFeld(def, aktuell, vorgabe) {
  const opt = T.artOptionen(def, vorgabe);
  const liste = aktuell && !opt.some(([w]) => w === aktuell) ? [...opt, [aktuell, `${aktuell} (gibt es nicht)`]] : opt;
  const gewaehlt = liste.find(([w]) => w === (aktuell ?? '')) ?? liste[0];
  return `<select data-ziel="eig:sanitaerArt" data-typ="auswahl" title="${esc(gewaehlt?.[1] ?? '')}">${liste.map(([w, t]) => `<option value="${esc(w)}"${w === (aktuell ?? '') ? ' selected' : ''}>${esc(t)}</option>`).join('')}</select>`;
}

/**
 * Zeilen der Apparateart: Auswahl, Satz zur wirksamen Art, darunter LU KW / LU WW / DU (abgeleitet, mit «?»).
 *   d = { def (Definition sanitaerArt aus dem Katalog der Klasse), wert (Eigenschaft oder null), vorgabe (Schlüssel der Vorgabe der Klasse), l (element_lesen) }
 */
export function artZeilen(d, H) {
  const z = [];
  z.push(H.zeile('Apparateart', artFeld(d.def, d.wert ?? null, d.vorgabe ?? null),
    { titel: [d.def?.beschreibung, 'Eigenschaft sanitaerArt'].filter(Boolean).join('\n'), warum: 'sanitaerArt', klasse: 'ep-art' }));
  const satz = T.artHinweis(d.l?.sanitaer);
  if (satz) z.push(H.zeile('', H.nurLesen(satz), { klasse: `klein ep-sanhinweis ep-art-satz${d.l?.sanitaer?.woher === 'keine' || d.l?.sanitaer?.woher === 'unbekannt' ? ' ep-sanwarnung' : ''}` }));
  for (const w of T.apparatWerte(d.l?.abgeleitet)) {
    z.push(H.zeile(w.label, H.nurLesen(`${w.text} ${w.einheit}`), { warum: w.schluessel, klasse: `ep-sanwert ep-san-${w.schluessel}`,
      titel: `${w.label} je Stück – abgeleitet aus dem Katalog (nicht gespeichert); das «?» zeigt Quelle und Excel-Zeile` }));
  }
  return z;
}

/** Zeilen der Belastungssummen einer Leitung (Gruppe «Abgeleitet»): Summen mit «?», darunter Apparate und Hinweise. */
export function leitungZeilen(l, H) {
  const z = [];
  for (const w of T.leitungWerte(l?.abgeleitet)) {
    z.push(H.zeile(w.label, H.nurLesen(`${w.text} ${w.einheit}`), { warum: w.schluessel, klasse: `ep-sanwert ep-san-${w.schluessel}`,
      titel: `${w.label} – abgeleitet aus den Apparaten im Netz (nicht gespeichert); das «?» zeigt den Rechenweg und die Beiträge` }));
  }
  const hinweise = T.leitungHinweise(l?.sanitaer);
  if (z.length && hinweise.length) z.push(H.zeile('', H.nurLesen(hinweise.join(' · ')), { klasse: `klein ep-sanhinweis${l.sanitaer?.masche ? ' ep-sanwarnung' : ''}` }));
  return z;
}

/** Hinweiszeile unter dem Feld der Abflusskennzahl K. */
export const kHinweisZeile = (H) => H.zeile('', H.nurLesen(T.K_HINWEIS), { klasse: 'klein ep-sanhinweis ep-k-hinweis', titel: 'Es gibt keine Auswahlliste für K: der Wert gehört zur Gebäudeart und steht in SN 592000 (liegt nicht vor).' });

/** Eine Zeile der Tabelle der Spezialinstallationen. */
export function spezialZeileHtml(z = {}) {
  const f = (klasse, wert, titel, extra = '') => `<td><input type="text" class="${klasse}" value="${esc(wert ?? '')}" title="${esc(titel)}" ${extra}></td>`;
  return `<tr>${f('sp-b', z.bezeichnung, 'Bezeichnung, z. B. Garteneinrichtung', 'aria-label="Bezeichnung"')}${f('sp-v', z.versorgung, 'Versorgung in l/s (Excel Spalte I)', 'inputmode="decimal" aria-label="Versorgung l/s"')}`
    + `${f('sp-e', z.entsorgung, 'Entsorgung in l/s (Excel Spalte N)', 'inputmode="decimal" aria-label="Entsorgung l/s"')}`
    + '<td class="sp-aktion"><button type="button" class="ep-spez-weg" title="Zeile löschen" aria-label="Zeile löschen">×</button></td></tr>';
}

/** Tabelle der Spezialinstallationen (Eigenschaft spezialinstallationen der Anlage): Bezeichnung, Versorgung l/s, Entsorgung l/s, Zeile hinzufügen/löschen. */
export function spezialFeld(wert) {
  const zeilen = T.spezialZeilen(wert);
  return `<div class="ep-spezial" data-ziel="eig:spezialinstallationen" data-typ="spezial">`
    + `<table class="ep-spez"><thead><tr><th>Bezeichnung</th><th title="Versorgung in l/s (Excel Spalte I)">Versorgung l/s</th><th title="Entsorgung in l/s (Excel Spalte N)">Entsorgung l/s</th><th></th></tr></thead>`
    + `<tbody>${zeilen.map(spezialZeileHtml).join('')}</tbody></table>`
    + `<div class="ep-spez-fuss"><button type="button" class="ep-spez-neu" title="Eine Spezialinstallation hinzufügen">+ Zeile hinzufügen</button>`
    + `<span class="ep-spez-hinweis" hidden>${esc(T.SPEZIAL_FEHLT)}</span></div></div>`;
}
