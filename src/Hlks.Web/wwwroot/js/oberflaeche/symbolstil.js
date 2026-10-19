// Wahl des Symbolsatzes (Teil S, entwurf/menueband-revit.md): «strich» (Vorgabe, einfarbige Strichsymbole, symbole.js) oder «revit» (mehrfarbiger Satz im Stil
// von Revit, symbole-revit.js; Probe bis zur Freigabe). Dieses Modul hält nur die Wahl und liefert die Revit-Fassung eines Symbols; symbol() und symbolWz() fragen hier
// zuerst nach (und fallen auf den Strich zurück, wenn es zu einem Namen keine Revit-Fassung gibt). Keine Oberfläche, kein Server: Speichern und Umschalten steht in
// symbolstil-umschalten.js. Zwischenspeicher im Browser (localStorage «hlks.symbolstil»), damit die Seite beim Neuladen nicht flackert; massgebend ist die
// Benutzereinstellung «oberflaeche.symbole» (benutzer.js).

import { revitSymbol } from './symbole-revit.js';

export const SYMBOLSTILE = ['strich', 'revit'];
export const EINSTELLUNG_SYMBOLE = 'oberflaeche.symbole';

let stil = 'strich';
try {
  const t = typeof localStorage === 'object' && localStorage ? localStorage.getItem('hlks.symbolstil') : null;
  if (t && JSON.parse(t) === 'revit') stil = 'revit';
} catch { /* ohne localStorage bleibt es beim Strich */ }

export const symbolstil = () => stil;
export const revitAktiv = () => stil === 'revit';

/** Setzt den Stil (nur die Wahl; zeichnet nichts neu). Unbekanntes ergibt «strich». */
export function symbolstilSetzen(neu) {
  stil = neu === 'revit' ? 'revit' : 'strich';
  try { if (typeof localStorage === 'object' && localStorage) localStorage.setItem('hlks.symbolstil', JSON.stringify(stil)); } catch { /* egal */ }
  return stil;
}

/** Revit-Fassung des Symbols (SVG-Text) oder null: bei Stil «strich» immer null, ebenso wenn es zum Namen keine Revit-Fassung gibt. */
export function ersatzSymbol(name, groesse) {
  return stil === 'revit' ? revitSymbol(name, groesse) : null;
}
