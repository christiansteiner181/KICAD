// Gesten am Arbeitsplatz (Modul G): die echte Umgebung für gesten-ausloesen.js – hier, und nur hier, wird das Dokument gelesen und die Oberfläche bedient.
// Keine Logik: was eine Geste bewirkt, entscheidet gesten-ausloesen-logik.js; Aktionen laufen über das Aktionsregister (wie Tastenkürzel).

import * as fenster from '../fenster.js';
import { element } from '../zustand.js';
import { aktionen, ausfuehren, istAktiv } from '../oberflaeche/aktionen.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { seitenFinden, seiteBlaettern, rollbarFinden, seiteRollen, ansichtRollbar } from './gesten-ausloesen-logik.js';

/** Der oberste Dialog (der zuletzt angelegte in der Dialogschicht) oder null. */
const dialogOben = () => [...document.querySelectorAll('#dialogschicht > .dlg')].at(-1) ?? null;
const rollFlaeche = (dlg) => dlg.querySelector('.dlg-inhalt') ?? dlg;

/** Das aktive Ansichtsfenster mit Art, Kachel und Inhaltsfläche oder null. */
function ansichtAktiv() {
  const o = fenster.aktive();
  if (!o?.kachel) return null;
  return { art: element(o.id)?.werte?.art ?? null, kachel: o.kachel, flaeche: o.kachel.querySelector('.kachel-inhalt') ?? o.kachel };
}

export function umgebungErzeugen() {
  return {
    kontext() {
      const d = dialogOben();
      const a = d ? null : ansichtAktiv();
      return {
        dialog: d ? { seiten: !!seitenFinden(d), rollbar: !!rollbarFinden(rollFlaeche(d)) } : null,
        ansicht: a ? { seiten: !!seitenFinden(a.kachel), rollbar: ansichtRollbar(a.art) && !!rollbarFinden(a.flaeche) } : null,
      };
    },
    seite(schritt) {
      const d = dialogOben();
      const wurzel = d ?? ansichtAktiv()?.kachel;
      return seiteBlaettern(seitenFinden(wurzel), schritt);
    },
    rollen(schritt) {
      const d = dialogOben();
      const flaeche = d ? rollFlaeche(d) : ansichtAktiv()?.flaeche;
      return seiteRollen(rollbarFinden(flaeche), schritt);
    },
    async aktion(id) {
      const a = aktionen.get(id);
      if (!a) return { ok: false };
      const ok = istAktiv(a);
      const ergebnis = await ausfuehren(id);
      return { ok, ergebnis, titel: a.titel };
    },
    meldung(text) { meldung(text, 'info', { ersetze: 'geste', dauer: 1600 }); },
  };
}
