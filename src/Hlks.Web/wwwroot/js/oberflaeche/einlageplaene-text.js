// Einlage- und Grundleitungspläne anlegen (Nachbearbeitung N3, Punkt 1): reine Texte und Parameter für den Dialog (einlageplaene-dialog.js), in Node getestet.
// Der Kernbefehl einlageplaene_anlegen (Plan/EinlagePlaene.cs, Modul 3 B10) legt je Planschlüssel der Verlegung (Abfrage verlegung) eine Ansicht und ein Planblatt an;
// Name, Kennzeichen, Plannummer und Gewerk bestimmt der Kern («Sanitär Einlagen D.ü.2.UG», EL-D.ü.2.UG). Der Dialog wählt nur Geschoss, Massstab und Format und
// zeigt vorab, was entsteht (Abfrage verlegung und Trockenlauf des Befehls). Keine Normwerte, keine Vorgaben: ohne Angabe gelten die des Kerns.

export const BEFEHL = 'einlageplaene_anlegen';

/** Die zwei Pläne des Befehls: art «einlage» (Einlagen in Decke, Bodenplatte, Wand) und «erdreich» (Grundleitungen). */
export const ARTEN = {
  einlage: { titel: 'Einlageplan', dialogTitel: 'Einlageplan anlegen', symbol: 'einlageplan', planWort: 'Einlagepläne', aktion: 'ansicht.einlageplan' },
  erdreich: { titel: 'Grundleitungsplan', dialogTitel: 'Grundleitungsplan anlegen', symbol: 'grundleitungsplan', planWort: 'Grundleitungspläne', aktion: 'ansicht.grundleitungsplan' },
};

export const FORMATE = ['A4', 'A3', 'A2', 'A1', 'A0'];
export const MASSSTAEBE = [10, 20, 50, 100, 200, 500];

const BAUTEIL = { decke: 'Decke', dach: 'Dach (wie Decke)', bodenplatte: 'Bodenplatte', wand: 'Wand' };
export const bauteilText = (b) => (b ? (BAUTEIL[b] ?? b) : '–');

/**
 * Zeilen der Planliste aus der Antwort der Abfrage verlegung (plaene[]):
 * [{ plan (Planschlüssel = Vorschlag für den Namen), art, bauteil, geschoss, leitungen, formteile, laenge_m }]
 */
export function planZeilen(antwort) {
  return (Array.isArray(antwort?.plaene) ? antwort.plaene : []).map((p) => ({
    plan: String(p.plan ?? ''), art: p.art ?? '', bauteil: bauteilText(p.bauteilart), geschoss: p.geschoss ?? '',
    leitungen: p.leitungen ?? 0, formteile: p.formteile ?? 0, laenge_m: p.laenge_m ?? 0,
  }));
}

/** Geschosse mit Plänen in der Reihenfolge des Modells (reihenfolge = Kennzeichen von unten nach oben); unbekannte hinten. */
export function geschossOptionen(zeilen, reihenfolge = []) {
  const mit = [...new Set(zeilen.map((z) => z.geschoss).filter(Boolean))];
  const rang = (g) => { const i = reihenfolge.indexOf(g); return i < 0 ? reihenfolge.length : i; };
  return mit.sort((a, b) => rang(a) - rang(b) || String(a).localeCompare(String(b), 'de', { numeric: true }));
}

/** Parameter des Befehls: nur, was gewählt ist (leere Auswahl = Vorgabe des Kerns). */
export function befehlParameter({ art, geschoss = '', massstab = '', format = '' } = {}) {
  const p = { art };
  if (geschoss) p.geschoss = geschoss;
  const m = Number(String(massstab).replace(',', '.'));
  if (massstab !== '' && Number.isFinite(m) && m > 0) p.massstab = m;
  if (format) p.format = format;
  return p;
}

/** Planzeilen eines Geschosses (leer = alle). */
export const planFilter = (zeilen, geschoss) => (geschoss ? zeilen.filter((z) => z.geschoss === geschoss) : zeilen);

/** Kurztext zu einer Planzeile: «12 Leitungen, 3 Formteile, 14,25 m». */
export function planSumme(z) {
  const n = (k, ein, mehr) => `${k} ${k === 1 ? ein : mehr}`;
  const m = typeof z.laenge_m === 'number' ? `${z.laenge_m.toFixed(2).replace('.', ',')} m` : '';
  return [n(z.leitungen, 'Leitung', 'Leitungen'), n(z.formteile, 'Formteil', 'Formteile'), m].filter(Boolean).join(', ');
}

/** Hinweise des Kerns (Trockenlauf bzw. Lauf) als Textzeilen ohne Leeres. */
export const hinweisZeilen = (r) => (Array.isArray(r?.hinweise) ? r.hinweise : []).map((h) => String(h ?? '').trim()).filter(Boolean);

/** Neu angelegte Elemente eines Laufs nach Klasse: Kennzeichen der angelegten Ansichten und Planblätter. */
export function angelegt(r) {
  const nach = (klasse) => (r?.aenderungen ?? []).filter((a) => a.art === 'angelegt' && a.klasse === klasse).map((a) => a.kennzeichen);
  return { ansichten: nach('Ansicht'), planblaetter: nach('Planblatt') };
}

/** Zusammenfassung eines (Trocken-)Laufs: «2 Ansichten, 2 Planblätter». */
export function laufText(r) {
  const a = angelegt(r);
  const teile = [];
  if (a.ansichten.length) teile.push(`${a.ansichten.length} ${a.ansichten.length === 1 ? 'Ansicht' : 'Ansichten'}`);
  if (a.planblaetter.length) teile.push(`${a.planblaetter.length} ${a.planblaetter.length === 1 ? 'Planblatt' : 'Planblätter'}`);
  return teile.length ? teile.join(', ') : 'nichts Neues';
}
