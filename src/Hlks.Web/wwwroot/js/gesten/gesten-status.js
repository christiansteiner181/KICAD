// Statusleiste: Symbol «Gesten» mit vier Zuständen (Modul G, entwurf/modul-gesten.md, Muster quest-status.js): aus · bereit, keine Hand · Hand erkannt · Geste aktiv mit Namen
// («drehen»). Klick schaltet die Gesten ein/aus (Grundschalter), Rechtsklick öffnet den Dialog «Gesten». Der Text steht in gesten-text.js (statusAnzeige); hier nur Verdrahtung.
// Das Symbol ist immer da, damit man die Gesten einschalten kann; erst dann fragt der Browser nach der Kamera.

import { symbol } from '../oberflaeche/symbole.js';
import { statusAnzeige } from './gesten-text.js';

/** Verdrahtet den Knopf der Statusleiste. dialog() öffnet den Dialog «Gesten» (Vorgabe: dynamischer Import, damit er erst beim Öffnen geladen wird). */
export function gestenStatusEinrichten(knopf, steuerung, dialog = () => import('./gesten-dialog.js').then((m) => m.gestenDialog())) {
  knopf.type = 'button';
  knopf.className = 'sl-gesten aus';
  knopf.innerHTML = `<span class="sl-gesten-sym">${symbol('gesten', 14)}</span><span class="sl-gesten-text"></span>`;
  const text = knopf.querySelector('.sl-gesten-text');
  const zeichnen = () => {
    const a = statusAnzeige(steuerung.zustand());
    knopf.className = `sl-gesten ${a.stufe}`;
    knopf.dataset.stufe = a.stufe;
    knopf.title = a.titel;
    knopf.setAttribute('aria-label', `${a.text}. ${a.titel}`);
    if (text.textContent !== a.text) text.textContent = a.text;
  };
  knopf.onclick = () => { steuerung.schalten(); };
  knopf.oncontextmenu = (e) => { e.preventDefault(); dialog(); };
  const abmelden = steuerung.abonnieren((e) => { if (e.art === 'zustand' || e.art === 'schalter' || e.art === 'testmodus') zeichnen(); });
  zeichnen();
  return { zeichnen, abmelden };
}
