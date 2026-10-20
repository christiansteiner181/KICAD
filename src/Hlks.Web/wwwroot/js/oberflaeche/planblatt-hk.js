// Planblatt wie H+K (Modul 7 Teil A): reine Bausteine der Oberfläche – Revisionsliste der Eigenschaftenpalette (Tabelle nach dem Muster der Spezialinstallationen,
// sanitaer-palette.js), Texte und Optionen des Dialogs «Planblatt anlegen» mit Plannummern-Vorschlag. Keine Fachlogik: der Index der neuen Revision (nächster Buchstabe),
// das Datum (heute), die Plannummer und der Titel kommen vom Kern (Befehl revision_hinzufuegen, Abfrage plannummer_vorschlag); hier wird nur dargestellt und eingesammelt.
// Node-testbar (planblatt-hk.test.mjs).

import { esc } from './werkzeug.js';

/** Art des Plans für den Vorschlag (Wert = Parameter art der Abfrage plannummer_vorschlag). */
export const ART_OPTIONEN = [
  ['geschoss', 'Geschossplan'],
  ['fbh', 'Fussbodenheizung (FBH)'],
  ['steigzonen', 'Steigzonen Heizung'],
  ['grundleitungen', 'Grundleitungen'],
  ['einlagen', 'Einlagen'],
  ['kanalisation', 'Kanalisation'],
];

/** Revisionen aus dem Wert der Eigenschaft revisionen → [{ index, datum, beschreibung }]; ältere Einträge mit «text» statt «beschreibung» werden gelesen. */
export function revisionen(wert) {
  if (!Array.isArray(wert)) return [];
  return wert.filter((r) => r && typeof r === 'object').map((r) => ({ index: String(r.index ?? ''), datum: String(r.datum ?? ''), beschreibung: String(r.beschreibung ?? r.text ?? '') }));
}

/** Eine Zeile der Revisionstabelle. */
export function revisionZeileHtml(z = {}) {
  const f = (klasse, wert, titel) => `<td><input type="text" class="${klasse}" value="${esc(wert ?? '')}" title="${esc(titel)}" aria-label="${esc(titel)}"></td>`;
  return `<tr>${f('rev-i', z.index, 'Index (Buchstabe)')}${f('rev-d', z.datum, 'Datum TT.MM.JJJJ')}${f('rev-b', z.beschreibung, 'Beschreibung der Änderung')}`
    + '<td class="rev-aktion"><button type="button" class="ep-rev-weg" title="Revision löschen" aria-label="Revision löschen">×</button></td></tr>';
}

/** Tabelle der Revisionen (Eigenschaft revisionen des Planblatts): Index, Datum, Beschreibung; «+ Revision hinzufügen» fragt die Beschreibung ab und ruft den Kern. */
export function revisionFeld(wert) {
  const zeilen = revisionen(wert);
  return '<div class="ep-revisionen" data-ziel="eig:revisionen" data-typ="revisionen">'
    + '<table class="ep-rev"><thead><tr><th title="Index (Buchstabe)">Index</th><th>Datum</th><th>Beschreibung</th><th></th></tr></thead>'
    + `<tbody>${zeilen.map(revisionZeileHtml).join('')}</tbody></table>`
    + '<div class="ep-rev-fuss"><button type="button" class="ep-rev-neu" title="Revision hinzufügen: der Kern setzt den nächsten Buchstaben und das Datum von heute">+ Revision hinzufügen</button>'
    + `<span class="ep-rev-hinweis leise">${zeilen.length === 0 ? 'Noch keine Revision – der Plankopf zeigt eine Strichzeile.' : `Letzte Revision: ${esc(zeilen[zeilen.length - 1].index)}`}</span></div></div>`;
}

/** Eingesammelte Zeilen der Tabelle → Wert der Eigenschaft: vollständig leere Zeilen entfallen; keine Zeile → null (Eigenschaft entfernen). */
export function revisionenWert(zeilen) {
  const liste = (zeilen ?? []).map((z) => ({ index: String(z.index ?? '').trim(), datum: String(z.datum ?? '').trim(), beschreibung: String(z.beschreibung ?? '').trim() }))
    .filter((z) => z.index || z.datum || z.beschreibung);
  return liste.length ? liste : null;
}

/** Text unter dem Plannummernfeld: Begründung des Kerns, sonst der Grund, warum es keinen Vorschlag gibt; dazu die Hinweise des Kerns. */
export function vorschlagText(v) {
  if (!v) return '';
  const teile = [v.begruendung, ...(Array.isArray(v.hinweise) ? v.hinweise : [])].filter(Boolean);
  return teile.join(' ');
}

/** Parameter der Abfrage plannummer_vorschlag aus den Auswahlfeldern des Dialogs (leere entfallen; Geschossplan = Vorgabe). */
export function vorschlagParameter({ gewerk, geschoss, art }) {
  const p = {};
  if (gewerk) p.gewerk = gewerk;
  if (geschoss) p.geschoss = geschoss;
  if (art && art !== 'geschoss') p.art = art;
  return p;
}

/** Zeilen der Revisionstabelle aus dem DOM-Knoten (tbody tr) → [{ index, datum, beschreibung }]. */
export function zeilenAusTabelle(feld) {
  return [...feld.querySelectorAll('tbody tr')].map((tr) => ({
    index: tr.querySelector('.rev-i')?.value ?? '', datum: tr.querySelector('.rev-d')?.value ?? '', beschreibung: tr.querySelector('.rev-b')?.value ?? '',
  }));
}
