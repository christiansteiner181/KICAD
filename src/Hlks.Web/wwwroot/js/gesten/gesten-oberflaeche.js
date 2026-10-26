// Gesten am Arbeitsplatz (Modul G, entwurf/modul-gesten.md): verdrahtet die Gesten mit der Oberfläche – Symbol in der Statusleiste, Handskelett, Auslösen der Schritt-Gesten
// und Nachführen des Knopfs «Gesten» im Menüband. Aufgerufen von statusleiste.js beim Start. Nichts davon belastet den Start: gesten.js (Kamera, MediaPipe) wird nur geladen,
// wenn der Grundschalter an ist; ein Fehler hier bricht den Start von KICAD nie ab. Im eigenen Fenster einer Ansicht (Zweitfenster) laufen keine Gesten.

import { steuerung } from './gesten-steuerung.js';
import { gestenStatusEinrichten } from './gesten-status.js';
import { skelettEinrichten } from './gesten-skelett.js';
import { ausloesenEinrichten } from './gesten-ausloesen.js';
import { umgebungErzeugen } from './gesten-umgebung.js';
import { istZweitfenster } from '../oberflaeche/zweitfenster.js';

let eingerichtet = false;

/** knopf = das Symbol der Statusleiste. */
export function gestenEinrichten(knopf) {
  if (eingerichtet) return;
  eingerichtet = true;
  if (istZweitfenster()) { knopf.hidden = true; return; }
  try {
    gestenStatusEinrichten(knopf, steuerung);
    skelettEinrichten(steuerung);
    ausloesenEinrichten(steuerung, umgebungErzeugen());
    // der gedrückte Zustand des Knopfs «Gesten» im Menüband zieht nach, wenn der Schalter in der Statusleiste umgelegt wird (menueband.js hört auf dieses Ereignis)
    steuerung.abonnieren((e) => { if (e.art === 'schalter') window.dispatchEvent(new CustomEvent('hlks-aktionszustand')); });
    steuerung.einrichten().catch((e) => console.error('Gesten', e));
  } catch (e) {
    console.error('Gesten konnten nicht eingerichtet werden', e);
    knopf.hidden = true;
  }
}
