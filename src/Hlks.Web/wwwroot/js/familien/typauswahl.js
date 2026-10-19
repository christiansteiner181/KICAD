// Platzieren mit Typauswahl (Modul 2, C1): die Optionen des Werkzeugs «Familie platzieren» (Optionsleiste wie in Revit: «Familie: Typ»,
// Drehung, Exemplarparameter). Reine Funktionen für Auswahl, Felder und Werte (in Node getestet) und ein kleiner Zwischenspeicher der
// Definitionen (Abfrage familie) für die Felder der Exemplarparameter. optionen.js liest die Felder über familieFelder() – ohne diese Datei
// zu kennen, nur über das Feld «felder» des Eintrags «familie».
// Annahme: Exemplarwerte der Optionsleiste gelten nur, wenn sie ausgefüllt sind (leer = Wert des Typs); ja/nein-Parameter haben «(Typ)» als Leerwert.

import { familienListe, familieHolen } from './daten.js';
import { exemplarParameter } from './familien-palette.js';
import { zahlLesen } from './familien-text.js';

/** Haken, den platzieren.js setzt (vermeidet einen Importkreis über optionen.js): wird nach einer Typwahl in der Optionsleiste gerufen. */
export const haken = { typGewaehlt: null };

export const SEP = '|';
export const typWert = (kz, typ) => `${kz}${SEP}${typ}`;
/** «FAM-1|22-600-1000» → { familie, typ } oder null. */
export function typTeile(wert) {
  const s = String(wert ?? '');
  const i = s.indexOf(SEP);
  return i > 0 && i < s.length - 1 ? { familie: s.slice(0, i), typ: s.slice(i + 1) } : null;
}

/** Auswahl «Familie: Typ» aus der Familienliste (Abfrage familien): nur gültige Familien mit Typen. [[wert, text]] */
export function typOptionen(liste) {
  const aus = [];
  const familien = [...(liste ?? [])].filter((f) => f.gueltig !== false && (f.typen ?? []).length > 0).sort((a, b) => String(a.name).localeCompare(String(b.name), 'de'));
  for (const f of familien) for (const t of f.typen) aus.push([typWert(f.kennzeichen, t), `${f.name}: ${t}`]);
  return aus;
}

/** Felder der Exemplarparameter einer Familie (Antwort familie) für die Optionsleiste. */
export function exemplarFeldDefs(fam) {
  return exemplarParameter(fam).map((p) => {
    const f = { id: `p_${p.name}`, titel: p.name, fluechtig: true, param: p.name, datentyp: p.datentyp };
    if (p.datentyp === 'ja_nein') return { ...f, art: 'auswahl', werte: [['ja', 'ja'], ['nein', 'nein']], leer: '(Typ)' };
    if (p.datentyp === 'text' || p.datentyp === 'medium') return { ...f, art: 'text', breite: 8, platzhalter: '(Typ)' };
    return { ...f, art: 'zahl', einheit: p.datentyp === 'laenge' ? 'mm' : p.datentyp === 'winkel' ? '°' : p.einheit, breite: 6, platzhalter: '(Typ)' };
  });
}

/** Alle Felder des Werkzeugs: Typ, Drehung, dann die Exemplarparameter der gewählten Familie. */
export function familieFelder(typ, fam, liste = familienListe()) {
  const felder = [
    { id: 'typ', titel: 'Typ', art: 'auswahl', get werte() { return typOptionen(liste); }, leer: 'wählen …', fluechtig: true, beiAenderung(el) { el?.blur?.(); haken.typGewaehlt?.(); } },
    { id: 'drehung', titel: 'Drehung', art: 'zahl', einheit: '°', vorgabe: 0, breite: 5, fluechtig: true },
  ];
  const t = typTeile(typ);
  if (t && fam && fam.kennzeichen === t.familie) felder.push(...exemplarFeldDefs(fam));
  return felder;
}

/** Exemplarwerte aus den Werten der Optionsleiste: nur ausgefüllte Felder; ja/nein → true/false. */
export function exemplarWerte(fam, opt) {
  const aus = {};
  for (const f of exemplarFeldDefs(fam)) {
    const v = opt?.[f.id];
    if (v === null || v === undefined || v === '') continue;
    if (f.datentyp === 'ja_nein') { aus[f.param] = v === 'ja' || v === true; continue; }
    if (f.art === 'zahl') { const z = typeof v === 'number' ? v : zahlLesen(v); if (z !== null && Number.isFinite(z)) aus[f.param] = z; continue; }
    aus[f.param] = String(v);
  }
  return aus;
}

/** Parameter des Befehls familie_platzieren aus den Werten der Optionsleiste. */
export function platzierParameter(opt, punkt, fam, geschoss) {
  const t = typTeile(opt?.typ);
  if (!t) return null;
  const p = { familie: t.familie, typ: t.typ, punkt: [Math.round(punkt[0] * 100) / 100, Math.round(punkt[1] * 100) / 100], drehung: Number(opt?.drehung) || 0 };
  const ex = fam ? exemplarWerte(fam, opt) : {};
  if (Object.keys(ex).length) p.parameter = ex;
  if (geschoss) p.geschoss = geschoss;
  return p;
}

// ------------------------------------------------------------------------------------------------ Zwischenspeicher der Definitionen

const defs = new Map();    // Kennzeichen → Antwort familie
const laufend = new Map();

/** Definition einer Familie (Antwort familie) aus dem Zwischenspeicher, sonst null. */
export const definitionAus = (kz) => defs.get(kz) ?? null;

/** Lädt die Definition (einmal je Stand der Familie; Version steht in der Liste). Gibt sie zurück. */
export async function definitionLaden(kz) {
  const v = familienListe().find((f) => f.kennzeichen === kz)?.version;
  const alt = defs.get(kz);
  if (alt && alt.version === v) return alt;
  if (laufend.has(kz)) return laufend.get(kz);
  const p = familieHolen(kz).then((f) => { if (f) defs.set(kz, f); laufend.delete(kz); return f; });
  laufend.set(kz, p);
  return p;
}

export function zwischenspeicherLeeren() { defs.clear(); laufend.clear(); }
