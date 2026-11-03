// Fangen wie Revit (Bearbeitungswerkzeuge B5, entwurf/bearbeitungswerkzeuge.md): Kürzel SE, SM, SI, SP, SN, SR (nur diese Fangart) und SO (kein Fang)
// für den nächsten Punkt bzw. Griff; Verwalten › Modell › «Fangen …» mit den Fangarten je Benutzer. Die Logik steht in werkzeuge/fangwahl.js.

import { registrieren, hinweisSetzen } from './register.js';
import { zeigen } from './dialoge.js';
import { esc, knoten } from './werkzeug.js';
import { FANGARTEN, FANGNAMEN } from '../werkzeuge/fangen.js';
import { FANG_KUERZEL, einmalSetzen, einmalText, erlaubteArten, artenSetzen } from '../werkzeuge/fangwahl.js';

for (const [kuerzel, art] of Object.entries(FANG_KUERZEL)) {
  registrieren({
    id: `fangen.${art}`, titel: art === 'aus' ? 'Fang aus (einmal)' : `Fang: ${FANGNAMEN[art]} (einmal)`, symbol: 'fangen', kuerzel,
    ausfuehren() { einmalSetzen(art); hinweisSetzen(`${einmalText(art)} – gilt für den nächsten Punkt.`, 4000); },
  });
}

async function einstellungen() {
  const an = new Set(erlaubteArten());
  const w = knoten(`<div><p class="dlg-text">Welche Punkte fangen die Werkzeuge und Griffe? Anschlüsse von Leitungen und Familien werden immer gefangen.
    Einmalig überschreiben: ${Object.entries(FANG_KUERZEL).map(([k, a]) => `${k} ${a === 'aus' ? 'aus' : esc(FANGNAMEN[a])}`).join(', ')}.</p>${FANGARTEN.map((a) =>
    `<label class="haken"><input type="checkbox" value="${a}" ${an.has(a) ? 'checked' : ''}> ${esc(FANGNAMEN[a])}</label>`).join('<br>')}</div>`);
  const ok = await zeigen({ titel: 'Fangen', inhalt: w, breite: 440, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'OK', wert: true, primaer: true }] });
  if (ok) artenSetzen([...w.querySelectorAll('input:checked')].map((i) => i.value));
}

registrieren({ id: 'verwalten.fangen', titel: 'Fangen …', symbol: 'fangen', kuerzel: null, register: 'verwalten', gruppe: 'Modell', ausfuehren: einstellungen });
