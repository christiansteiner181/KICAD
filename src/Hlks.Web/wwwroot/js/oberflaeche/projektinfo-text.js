// Projektinformationen bearbeiten (Nachbearbeitung N3, Punkt 2): reine Bausteine für den Dialog «Projektinformationen» (projektinfo-dialog.js), in Node getestet.
// Die Felder kommen aus der Katalogdefinition der Klasse Projektinformation (Abfrage katalog { klasse }): Typ, Einheit, Auswahl und Beschreibung stehen dort.
// Hier stehen nur die Anzeigenamen und die Gruppen im Dialog – keine Werte, keine Vorgaben, keine Auswahllisten. Geändert wird allein über die Kernbefehle
// element_aendern (nur die geänderten Felder; null entfernt) bzw. element_anlegen (gibt es noch kein Element der Klasse).

export const KLASSE = 'Projektinformation';

/** Felder, die der Dialog bearbeitbar macht, mit Anzeigename und Gruppe. Die Reihenfolge ist die im Dialog. */
const BEARBEITBAR = [
  ['zeile1', 'Objekt, Zeile 1', 'Projekt'],
  ['zeile2', 'Objekt, Zeile 2', 'Projekt'],
  ['projektnummer', 'Projektnummer', 'Projekt'],
  ['phase', 'Phase', 'Projekt'],
  ['rohrbezeichnung', 'Rohrbezeichnung', 'Beschriftung und Bemassung'],
  ['bemassungEinheiten', 'Masseinheit je Gewerk', 'Beschriftung und Bemassung'],
  ['nordwinkel', 'Nordwinkel', 'Lage'],
];

/** Felder, die eine andere Funktion schreibt: der Dialog zeigt sie nur und sagt, wo man sie ändert. */
export const GESETZT_VON = {
  kollisionslauf: 'Wird vom Kollisionslauf geschrieben (Register Koordination › Kollisionen prüfen).',
  ifcBezug: 'Setzt der IFC-Übertrag (Register Einfügen › IFC übernehmen).',
  uebergang: 'Register Hülle › Übergang …',
  hkap: 'Register Verwalten › HKAP › Verbinden.',
  familienAuswahl: 'Register Gebäudetechnik › Familien im Explorer ….',
};
const TITEL_LESEND = { kollisionslauf: 'Letzter Kollisionslauf', ifcBezug: 'IFC-Bezug', uebergang: 'Wärmeübergang', hkap: 'HKAP-Verknüpfung', familienAuswahl: 'Familienauswahl' };

export const GRUPPEN_REIHE = ['Projekt', 'Beschriftung und Bemassung', 'Lage', 'Weitere', 'Von anderen Funktionen gesetzt'];

/** Lesbarer Titel für ein Feld ohne eigenen Anzeigenamen: «meinFeld» → «Mein Feld». */
export function feldTitel(name) {
  const t = String(name).replace(/([a-zäöü])([A-Z])/g, '$1 $2').replace(/_/g, ' ').trim();
  return t.charAt(0).toUpperCase() + t.slice(1);
}

/**
 * Plan des Dialogs aus der Katalogdefinition der Klasse (Abfrage katalog { klasse: 'Projektinformation' }):
 * [{ titel, felder: [{ name, titel, art: 'text'|'zahl'|'auswahl'|'einheiten'|'lesen', einheit, auswahl, beschreibung, hinweis }] }]
 * art «lesen»: nur anzeigen (andere Funktion schreibt das Feld oder der Typ ist hier nicht bearbeitbar).
 */
export function feldPlan(def) {
  const eigenschaften = Array.isArray(def?.eigenschaften) ? def.eigenschaften : [];
  const nachName = new Map(eigenschaften.map((e) => [e.name, e]));
  const gruppen = new Map(GRUPPEN_REIHE.map((g) => [g, []]));
  const feld = (e, titel, art, hinweis = '') => ({
    name: e.name, titel, art, einheit: e.einheit ?? '', auswahl: Array.isArray(e.auswahl) ? e.auswahl : null, beschreibung: e.beschreibung ?? '', hinweis,
  });
  const artVon = (e) => {
    if (e.name === 'bemassungEinheiten' && e.typ === 'objekt') return 'einheiten';
    if (e.typ === 'text' || e.typ === 'zahl' || e.typ === 'auswahl') return e.typ;
    return 'lesen';
  };
  for (const [name, titel, gruppe] of BEARBEITBAR) {
    const e = nachName.get(name);
    if (e) gruppen.get(gruppe).push(feld(e, titel, artVon(e)));
  }
  for (const e of eigenschaften) {
    if (BEARBEITBAR.some(([n]) => n === e.name)) continue;
    if (Object.hasOwn(GESETZT_VON, e.name)) gruppen.get('Von anderen Funktionen gesetzt').push(feld(e, TITEL_LESEND[e.name] ?? feldTitel(e.name), 'lesen', GESETZT_VON[e.name]));
    else gruppen.get('Weitere').push(feld(e, feldTitel(e.name), artVon(e)));
  }
  return [...gruppen].filter(([, l]) => l.length > 0).map(([titel, felder]) => ({ titel, felder }));
}

/** Alle Felder des Plans in einer Liste. */
export const alleFelder = (plan) => plan.flatMap((g) => g.felder);

/** Zahl aus einer Eingabe in Schweizer Schreibweise (Dezimalkomma oder -punkt). Leer → null; sonst Fehler. */
export function zahlLesen(text, titel = 'Wert') {
  const t = String(text ?? '').trim();
  if (t === '') return null;
  const n = Number(t.replace(',', '.'));
  if (!Number.isFinite(n)) throw new Error(`${titel}: bitte eine Zahl eingeben.`);
  return n;
}

/** Tabelle {gewerk: einheit} ohne leere Einträge; leer → null (Eintrag entfernen, es gilt die Vorgabe). */
export function einheitenTabelle(tabelle) {
  const o = {};
  for (const [g, e] of Object.entries(tabelle ?? {})) if (typeof e === 'string' && e.trim() !== '') o[g] = e.trim();
  return Object.keys(o).length > 0 ? o : null;
}

const gleich = (a, b) => JSON.stringify(a ?? null) === JSON.stringify(b ?? null);
const sortiert = (o) => (o && typeof o === 'object' ? Object.fromEntries(Object.entries(o).sort(([x], [y]) => x.localeCompare(y))) : o);

/**
 * Wert eines Felds aus der Eingabe (Rohwert wie im Formular).
 *   text: getrimmt, leer → null · zahl: Zahl oder null · auswahl: Text oder null · einheiten: Tabelle oder null
 */
export function wertAusEingabe(feld, roh) {
  switch (feld.art) {
    case 'zahl': return zahlLesen(roh, feld.titel);
    case 'einheiten': return einheitenTabelle(roh);
    case 'auswahl': { const t = String(roh ?? '').trim(); return t === '' ? null : t; }
    default: { const t = String(roh ?? '').trim(); return t === '' ? null : t; }
  }
}

/**
 * Die Änderungen gegenüber dem Element: { feldname: neuerWert | null } nur für Felder, die sich unterscheiden (null entfernt den Eintrag).
 *   plan      feldPlan(...)
 *   werte     aktuelle Werte des Elements (element.werte) oder {} bzw. null, wenn es noch keines gibt
 *   eingabe   { feldname: Rohwert } der bearbeitbaren Felder
 * Wirft bei einer ungültigen Eingabe (Zahl).
 */
export function aenderungen(plan, werte, eingabe) {
  const aus = {};
  for (const f of alleFelder(plan)) {
    if (f.art === 'lesen' || !Object.hasOwn(eingabe ?? {}, f.name)) continue;
    const neu = wertAusEingabe(f, eingabe[f.name]);
    const alt = f.art === 'einheiten' ? einheitenTabelle(werte?.[f.name]) : f.art === 'zahl' ? (werte?.[f.name] ?? null) : (werte?.[f.name] ?? null);
    if (!gleich(f.art === 'einheiten' ? sortiert(neu) : neu, f.art === 'einheiten' ? sortiert(alt) : alt)) aus[f.name] = neu;
  }
  return aus;
}

/** Parameter für element_aendern; null, wenn nichts zu ändern ist. */
export function aendernParameter(kennzeichen, aus) {
  return Object.keys(aus).length === 0 ? null : { element: kennzeichen, eigenschaften: aus };
}

/** Parameter für element_anlegen, wenn das Projekt noch keine Projektinformation hat (nur gesetzte Werte; Kennzeichen vergibt der Kern). null, wenn nichts einzutragen ist. */
export function anlegenParameter(aus) {
  const eig = Object.fromEntries(Object.entries(aus).filter(([, v]) => v !== null));
  return Object.keys(eig).length === 0 ? null : { klasse: KLASSE, name: KLASSE, eigenschaften: eig };
}

/** Kurztext eines nur lesbaren Werts (Objekt, Liste): «3 Einträge» bzw. die Schlüssel; leer → «–». */
export function lesendText(wert) {
  if (wert === null || wert === undefined || wert === '') return '–';
  if (typeof wert === 'string') return wert;
  if (typeof wert === 'number' || typeof wert === 'boolean') return String(wert);
  if (Array.isArray(wert)) return wert.length === 0 ? '–' : `${wert.length} ${wert.length === 1 ? 'Eintrag' : 'Einträge'}`;
  const k = Object.keys(wert);
  return k.length === 0 ? '–' : k.join(', ');
}

/** Das Element Projektinformation eines Projekts: das erste nach Kennzeichen (wie der Kern). */
export function projektinfoVon(elemente) {
  return [...(elemente ?? [])].filter((e) => e.klasse === KLASSE).sort((a, b) => (a.kennzeichen < b.kennzeichen ? -1 : a.kennzeichen > b.kennzeichen ? 1 : 0))[0] ?? null;
}
