// Statusleiste: «Brille sieht den Bildschirm» mit Knopf «Beenden» (Modul XR, XR-I «Desktop in der Brille», Quest App V1c). Fragt den Server alle 2 s
// (GET /api/xr/desktop/zustand, nur localhost, billig); solange eine Brille mitsieht, ist die Anzeige rot und das Fenster bekommt einen roten Rand
// (Klasse xr-desktop-aktiv am Wurzelelement des Dokuments). «Beenden» → POST /api/xr/desktop/beenden (Schluss 4010: die Brille verbindet erst nach dem nächsten Hineinziehen
// wieder). Regeln: xr-desktop-text.js.

import { holen, senden } from '../api.js';
import { meldung } from './dialoge.js';
import * as T from './xr-desktop-text.js';

let teil = null;
let timer = null;
let zustand = null;

function zeichnen() {
  if (!teil) return;
  const a = T.desktopAnzeige(zustand);
  teil.el.hidden = !a;
  document.documentElement.classList.toggle('xr-desktop-aktiv', a?.stufe === 'aktiv');
  if (!a) return;
  teil.el.className = `sl-desktop ${a.stufe}`;
  teil.el.dataset.stufe = a.stufe;
  teil.el.title = a.titel;
  teil.text.textContent = a.text;
  teil.knopf.textContent = a.knopf;
}

async function lesen() {
  try { zustand = await holen(T.ZUSTAND); } catch { zustand = null; }
  zeichnen();
}

/** «Beenden»: alle Übertragungen an Brillen trennen. */
export async function xrDesktopBeenden() {
  try {
    const a = await senden(T.BEENDEN, {});
    zustand = a?.zustand ?? null;
    meldung(T.beendetText(a), 'info');
  } catch (e) {
    meldung(e?.message || 'Beenden ist fehlgeschlagen.', 'fehler');
  }
  zeichnen();
}

/** Verdrahtet die Anzeige in der Statusleiste (versteckt, bis eine Brille verbunden ist). */
export function xrDesktopStatusEinrichten(el) {
  el.innerHTML = '<span class="sl-desktop-punkt">●</span><span class="sl-desktop-text"></span><button type="button" class="sl-desktop-beenden"></button>';
  el.hidden = true;
  teil = { el, text: el.querySelector('.sl-desktop-text'), knopf: el.querySelector('.sl-desktop-beenden') };
  teil.knopf.onclick = (ev) => { ev.stopPropagation(); xrDesktopBeenden(); };
  clearInterval(timer);
  timer = setInterval(lesen, T.TAKT_MS);
  lesen();
}
