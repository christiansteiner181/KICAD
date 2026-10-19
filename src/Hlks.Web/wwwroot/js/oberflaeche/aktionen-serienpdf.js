// Aktionen des Serien-PDF (entwurf/serien-pdf.md): Knopf «Serien-PDF …» im Register Ansicht (Gruppe «Planblätter») und im Kontextmenü des Knotens «Pläne»
// im Projektbrowser; dazu Gewerk eines Planblatts ändern bzw. bestätigen (Kontextmenü des Planblatts). Alles über die Kern-Tür:
// Abfrage plansaetze, Befehl planblatt_gewerk_setzen. Eingetragen wird nur über registrieren() (register.js).

import { derKlasse, element } from '../zustand.js';
import { registrieren } from './register.js';
import { zeigen } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { knoten, esc } from './werkzeug.js';
import { plansaetzeDaten, plansaetzeLaden } from './serienpdf-daten.js';
import { blattGewerk } from './serienpdf-text.js';
import { serienPdfDialog } from './serienpdf-dialog.js';

const hatBloetter = () => derKlasse('Planblatt').length > 0;

registrieren({
  id: 'plan.serienpdf', titel: 'Serien-PDF …', symbol: 'serienpdf', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  aktiv: hatBloetter, hinweis: 'Zuerst ein Planblatt anlegen.',
  ausfuehren: (p) => serienPdfDialog({ plansatz: p?.plansatz }),
});

/** Gewerk eines Planblatts von Hand setzen (Dialog mit allen Gewerken des Kerns). */
registrieren({
  id: 'plan.gewerk', titel: 'Gewerk …', symbol: 'planblatt', kuerzel: null,
  aktiv: (p) => element(p?.planblatt)?.klasse === 'Planblatt', hinweis: 'Zuerst ein Planblatt wählen.',
  async ausfuehren(p) {
    const kz = element(p.planblatt).kennzeichen;
    if (!plansaetzeDaten()) await plansaetzeLaden();
    const daten = plansaetzeDaten();
    const jetzt = blattGewerk(daten, kz);
    const w = knoten(`<div class="ansichtform"><label class="fz"><span>Gewerk von ${esc(kz)}</span><select class="f-gewerk">${(daten?.gewerke ?? []).map((g) => `<option value="${esc(g.gewerk)}"${g.gewerk === jetzt?.gewerk ? ' selected' : ''}>${esc(g.name)}</option>`).join('')}</select></label>`
      + `<p class="dlg-text leise">${jetzt ? esc(jetzt.gewerkHerkunft === 'gesetzt' ? 'Das Gewerk ist von Hand gesetzt.' : `Vorschlag des Kerns: ${jetzt.gewerkName} (${jetzt.gewerkQuelle}).`) : ''}</p></div>`);
    const ok = await zeigen({ titel: 'Gewerk des Plans', inhalt: w, breite: 460, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Setzen', wert: true, primaer: true }] });
    if (ok !== true) return;
    const r = await sende('planblatt_gewerk_setzen', { planblaetter: [kz], gewerk: w.querySelector('.f-gewerk').value });
    ergebnisMelden(r, 'Gewerk gesetzt.');
  },
});

/** Vorschlag des Kerns als Gewerk festhalten. */
registrieren({
  id: 'plan.gewerk-bestaetigen', titel: 'Gewerk bestätigen', symbol: 'planblatt', kuerzel: null,
  aktiv: (p) => element(p?.planblatt)?.klasse === 'Planblatt' && blattGewerk(plansaetzeDaten(), element(p.planblatt).kennzeichen)?.gewerkHerkunft === 'vorschlag',
  hinweis: 'Das Gewerk ist schon von Hand gesetzt.',
  async ausfuehren(p) {
    const kz = element(p.planblatt).kennzeichen;
    const b = blattGewerk(plansaetzeDaten(), kz);
    if (!b) return;
    ergebnisMelden(await sende('planblatt_gewerk_setzen', { planblaetter: [kz], gewerk: b.gewerk }), `${kz}: Gewerk ${b.gewerkName} bestätigt.`);
  },
});
