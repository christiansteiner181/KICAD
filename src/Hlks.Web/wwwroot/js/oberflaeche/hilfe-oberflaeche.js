// Hilfe (F1) in der Oberfläche: das Symbol «?» rechts in der Kopfleiste (wie Revits InfoCenter) und die Taste F1 öffnen die
// Bedienungsanleitung – eine Webseite, die KICAD selbst unter /hilfe/ ausliefert (ohne Anmeldung, ohne Internet); die Oberfläche öffnet sie nur.
// Keine Fachlogik: ob es sie auf diesem Rechner gibt, sagt der Server (GET /api/start/info › anleitung { url, vorhanden }), Schlüssel und
// Adressbau stehen in hilfe.js (reine Funktionen, mit Node getestet). Fehlt sie (noch nicht gebaut), sagt eine Meldung es – es öffnet sich nichts.
//
// Kontext von F1 (hilfe.js › hilfeKontext): offener Dialog (zeigen in dialoge.js) → #s-<Dialogtitel>; Zeiger über einem Knopf des Menübands
// (auch Flyout, Kontextregister, Schnellzugriff) oder Tastaturfokus darauf → #k-<Register>~<Knopf>; sonst die Anleitung ohne Sprungziel.
// Immer dasselbe Fenster (window.open mit festem Namen). Blockiert der Browser das Fenster oder fehlt die Adresse, sagt es eine Meldung.
// Gilt unverändert im Zweitfenster (?ansicht=…&fenster=n) und über dem Startbildschirm (der Kopf bleibt dort sichtbar).

import { holen } from '../api.js';
import { symbol } from './symbole.js';
import { meldung, obersterDialogTitel } from './dialoge.js';
import { knopfHilfe } from './menueband.js';
import { hilfeAdresse, hilfeKontext, HILFE_TOOLTIP, HILFE_FENSTER, HILFE_NICHT_EINGERICHTET, hilfeBlockiert } from './hilfe.js';

let basis = null;        // Adresse der Anleitung; nur eine gefundene wird gemerkt (ein später eingetragener Wert gilt sofort)
let laufend = null;      // laufende Abfrage

/** Adresse der Anleitung vom Server (null, wenn sie auf diesem Rechner nicht erstellt ist oder der Server nicht antwortet). */
export async function anleitungAdresse() {
  if (basis) return basis;
  laufend ??= holen('/api/start/info')
    .then((i) => { basis = i?.anleitung?.vorhanden === true && typeof i.anleitung.url === 'string' && i.anleitung.url.trim() ? i.anleitung.url.trim() : null; return basis; })
    .catch((e) => { console.error('Adresse der Bedienungsanleitung', e); return null; })
    .finally(() => { laufend = null; });
  return laufend;
}

/** Öffnet die Anleitung für den Kontext (hilfe.js › hilfeKontext) im Fenster «kicad-hilfe». Ergebnis: true, wenn das Fenster aufging. */
export async function hilfeOeffnen(kontext = { art: 'keiner' }) {
  const url = basis || await anleitungAdresse();   // ist die Adresse schon da, geht window.open ohne Umweg (Geste des Benutzers)
  const adresse = hilfeAdresse(url, kontext);
  if (!adresse) { meldung(HILFE_NICHT_EINGERICHTET, 'warnung'); return false; }
  let fenster = null;
  try { fenster = window.open(adresse, HILFE_FENSTER); } catch { fenster = null; }
  if (!fenster) { meldung(hilfeBlockiert(adresse), 'warnung'); return false; }
  try { fenster.focus?.(); } catch { /* egal */ }
  return true;
}

let fokusPerMaus = false;   // der Fokus stammt von einem Mausklick (und seither keine Taste ausser F1): zählt für F1 nicht

/** Kontext zum Zeitpunkt von F1: Dialog vor Knopf unter dem Zeiger vor Knopf mit Tastaturfokus. */
export function aktuellerKontext() {
  let k = document.querySelector('.mb-knopf:hover');
  if (!k) {
    const a = document.activeElement;
    if (a?.classList?.contains('mb-knopf') && !fokusPerMaus) k = a;   // nur per Tastatur angesteuert, nicht der Rest eines Mausklicks
  }
  return hilfeKontext({ dialogTitel: obersterDialogTitel(), knopf: k ? knopfHilfe(k) : null });
}

export function einrichten() {
  const ort = document.getElementById('kopf-rechts');
  if (ort && !ort.querySelector('.hilfe-knopf')) {
    const b = document.createElement('button');
    b.type = 'button'; b.id = 'hilfe-knopf'; b.className = 'hilfe-knopf';
    b.title = HILFE_TOOLTIP; b.setAttribute('aria-label', HILFE_TOOLTIP);
    b.innerHTML = symbol('anleitung', 18);
    b.addEventListener('click', () => { hilfeOeffnen({ art: 'keiner' }); });
    ort.appendChild(b);
  }
  document.addEventListener('pointerdown', () => { fokusPerMaus = true; }, true);
  // Edge öffnet bei F1 sonst seine eigene Hilfe: preventDefault in der Aufnahmephase, vor allen anderen Behandlern
  window.addEventListener('keydown', (e) => {
    if (!['F1', 'Shift', 'Control', 'Alt', 'Meta'].includes(e.key)) fokusPerMaus = false;   // eine Taste (Tab, Pfeile …) hat den Fokus bewegt
    if (e.key !== 'F1' || e.ctrlKey || e.altKey || e.metaKey || e.shiftKey) return;
    e.preventDefault();
    e.stopPropagation();
    if (e.repeat) return;
    hilfeOeffnen(aktuellerKontext());
  }, true);
  anleitungAdresse();   // vorwärmen, damit F1 und «?» die Adresse schon kennen
}
