// «Ähnliches platzieren» (Christian 02.11.2026: «bei aktiviertem bauteil, im kontext menue "ähnliches plazieren" aufführen»; Architektur nur
// untergeordnet): startet für ein gewähltes HLKS-Bauteil das Werkzeug, das es erzeugt hat, mit seinen Angaben vorbelegt –
//   Rohr, Kanal: Werkzeug Rohr bzw. Kanal mit Serie, Grösse, Höhe und System bzw. Medium der Leitung;
//   Bauteil aus einer Familie (Apparat, Armatur, Gerät …): Familie platzieren mit Familie und Typ.
// Kontextregister «Ändern | Klasse» (Gruppe Erstellen) und Rechtsklick im Grundriss (grundriss.js). Keine Fachlogik.

import { registrieren } from './register.js';
import { zustand, element } from '../zustand.js';
import { werkzeugStarten } from '../werkzeuge/basis.js';
import { setze } from '../werkzeuge/optionen.js';
import { leitungWerkzeug } from '../werkzeuge/leitung.js';
import { abzweigGroesse } from '../werkzeuge/leitung-frei.js';
import { hoeheRelativ } from '../werkzeuge/leitung-logik.js';
import { platzieren } from '../familien/aktionen-familien.js';

const LEITUNGEN = ['Rohr', 'Kanal'];

/** Lässt sich zu diesem Element Ähnliches platzieren? Leitungen mit Serie und Bauteile aus einer Familie. */
export function aehnlichMoeglich(el) {
  if (!el || el.klasse === 'Familie') return false;
  return !!el.bezuege?.familie && (LEITUNGEN.includes(el.klasse) || !!el.werte?.familientyp);
}

async function aehnlich(el) {
  if (LEITUNGEN.includes(el.klasse)) {
    const wid = el.klasse === 'Kanal' ? 'kanal' : 'rohr';
    setze(wid, 'serie', el.bezuege.familie);
    if (el.werte?.familientyp) setze(wid, 'groesse', el.werte.familientyp);
    abzweigGroesse(el.werte).forEach(([feld, w]) => setze(wid, feld, w));
    const z = el.geometrie?.punkte?.[0]?.[2];
    if (Number.isFinite(z)) setze(wid, 'hoehe', hoeheRelativ(element(el.geschoss)?.werte?.kote, z));
    if (el.system) { setze(wid, 'system', el.system); setze(wid, 'medium', ''); setze(wid, 'strang', el.teilVon && element(el.teilVon)?.klasse === 'Strang' ? el.teilVon : ''); }
    else { setze(wid, 'system', ''); setze(wid, 'strang', ''); setze(wid, 'medium', el.werte?.medium ?? ''); }
    return werkzeugStarten(leitungWerkzeug(el.klasse));
  }
  return platzieren({ familie: el.bezuege.familie, typ: el.werte.familientyp });
}

const gewaehlt = (p) => element(p?.element ?? zustand.auswahl);

registrieren({
  id: 'kontext.aehnlich', titel: 'Ähnliches platzieren', symbol: 'famPlatzieren', kuerzel: null, register: 'kontext', gruppe: 'Erstellen',
  aktiv: (p) => aehnlichMoeglich(gewaehlt(p)),
  hinweis: 'Zuerst eine Leitung oder ein Bauteil aus einer Familie wählen.',
  ausfuehren: (p) => aehnlich(gewaehlt(p)),
});
