// Symbolsatz umschalten (Teil S): «strich» (Vorgabe) oder «revit» (Probe), ohne Neuladen. Die Wahl steht in der Benutzereinstellung «oberflaeche.symbole»
// (benutzer.js, Server BenutzerEinstellungen) und gilt für den Benutzer auf allen Rechnern. Beim Umschalten werden alle Symbole im Dokument getauscht:
// jedes SVG mit data-sym (Name) wird durch die Fassung des gewählten Satzes in gleicher Grösse ersetzt – Menüband, Schnellzugriff, Projektbrowser, Eigenschaften.
// Hier steht keine Zeichnung und keine Fachlogik (Zeichnungen: symbole.js, symbole-revit.js; Wahl: symbolstil.js).

import { symbol } from './symbole.js';
import { symbolWz } from '../werkzeuge/symbole.js';
import { symbolstil, symbolstilSetzen, EINSTELLUNG_SYMBOLE } from './symbolstil.js';
import { benutzerLaden, benutzerWert, benutzerSetzen } from './benutzer.js';

let ereignisse = [];

/** Meldet sich an, wenn der Satz gewechselt wurde (fn(stil)); für Teile, die ihre Symbole nicht über data-sym tauschen lassen. */
export function beiSymbolstil(fn) { ereignisse.push(fn); }

/** Tauscht alle Symbole unterhalb von wurzel (Vorgabe: das ganze Dokument) gegen die Fassung des gewählten Satzes. Liefert die Zahl der getauschten. */
export function symbolstilAnwenden(wurzel = document) {
  let n = 0;
  for (const alt of wurzel.querySelectorAll('svg.sym[data-sym]')) {
    const name = alt.dataset.sym;
    const px = Number(alt.getAttribute('width')) || 28;
    const text = symbolWz(name, px) ?? symbol(name, px);
    const vorlage = document.createElement('template');
    vorlage.innerHTML = text;
    const neu = vorlage.content.firstElementChild;
    if (!neu) continue;
    // Klassen und Merkmale, die jemand von aussen ans Symbol gesetzt hat (ausser dem, was die Fassung selbst setzt), bleiben
    for (const k of alt.classList) if (k !== 'sym' && k !== 'sym-rv') neu.classList.add(k);
    alt.replaceWith(neu);
    n++;
  }
  return n;
}

/** Wählt den Satz: setzt ihn, tauscht die Symbole und (speichern = true) schreibt die Benutzereinstellung. Wirft, wenn der Server das Speichern ablehnt (die Wahl gilt dann trotzdem für diese Sitzung). */
export async function symbolstilWaehlen(stil, speichern = true) {
  const neu = symbolstilSetzen(stil);
  symbolstilAnwenden();
  document.documentElement.dataset.symbole = neu;
  for (const fn of ereignisse) { try { fn(neu); } catch (e) { console.error(e); } }
  if (speichern) await benutzerSetzen(EINSTELLUNG_SYMBOLE, neu);
  return neu;
}

/** Zwischen «strich» und «revit» wechseln (Häkchen «Symbole im Revit-Stil (Probe)»). */
export function symbolstilUmschalten() {
  return symbolstilWaehlen(symbolstil() === 'revit' ? 'strich' : 'revit');
}

/** Beim Start: die Benutzereinstellung lesen und den Satz danach einstellen (ohne zu schreiben). Ohne Eintrag oder bei Fehler gilt der Strich. */
export async function symbolstilEinrichten() {
  document.documentElement.dataset.symbole = symbolstil();
  try {
    await benutzerLaden();
    const w = benutzerWert(EINSTELLUNG_SYMBOLE, 'strich');
    const soll = w === 'revit' ? 'revit' : 'strich';
    if (soll !== symbolstil()) await symbolstilWaehlen(soll, false);
  } catch (e) { console.error('Symbolsatz', e); }
}
