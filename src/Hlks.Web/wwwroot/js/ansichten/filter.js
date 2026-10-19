// Was eine Ansicht zeigt: Gewerke-Filter, ausgeblendete Klassen, Geschoss (Grundriss). Eine Stelle für alle Ansichten,
// damit Grundriss, 3D und Listen dieselben Regeln haben. ansicht = Darstellung eines Elements der Klasse Ansicht.

const NIE_GRAFISCH = new Set(['ansicht', 'funktion']);
const GLIEDERUNG = new Set(['Gebaeude', 'Geschoss', 'Anlage', 'System', 'Strang', 'Zone']);

export function gewerkeVon(ansicht) {
  const g = ansicht?.werte?.gewerke;
  return Array.isArray(g) && g.length > 0 ? new Set(g) : null;
}

export function ausgeblendeteKlassen(ansicht) {
  const k = ansicht?.werte?.klassenAusblenden;
  return new Set(Array.isArray(k) ? k : []);
}

/** Soll das Element in einer grafischen Ansicht (Grundriss, 3D) erscheinen? */
export function sichtbar(el, ansicht) {
  if (!el || NIE_GRAFISCH.has(el.schicht) || GLIEDERUNG.has(el.klasse) || !el.geometrie) return false;
  const aus = ausgeblendeteKlassen(ansicht);
  if (aus.has(el.klasse) || (el.basis && aus.has(el.basis))) return false;
  const gewerke = gewerkeVon(ansicht);
  if (gewerke && el.gewerk && el.gewerk !== 'allgemein' && !gewerke.has(el.gewerk)) return false;
  const art = ansicht?.werte?.art;
  if (art === 'grundriss' && ansicht.geschoss && el.geschoss !== ansicht.geschoss) return false;
  return true;
}
