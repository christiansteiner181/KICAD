// Aktion «Paletten» im Register Ansicht (Gruppe «Fenster», Auftrag W1, entwurf/fenster-revit.md): ein Knopf öffnet wie Revit («Benutzeroberfläche») eine Liste aller Paletten
// mit Häkchen (ein- und ausblenden) und dem Eintrag «Paletten zurücksetzen» (Vorgabe: Eigenschaften oben, Projektbrowser unten, links). Die Liste entsteht beim Öffnen aus der
// Palettenverwaltung (paletten.js): eine neue Palette meldet sich nur dort an und steht hier von selbst. Ein einziger Knopf in der bestehenden Gruppe «Fenster» – eine eigene
// Gruppe würde das ohnehin volle Register Ansicht verbreitern. Keine Logik hier: die Verwaltung entscheidet, wo eine Palette sitzt.

import { registrieren } from './register.js';
import { kontextmenue, meldung } from './dialoge.js';
import { liste, umschalten, sichtbar, zuruecksetzen } from './paletten.js';
import { revitAktiv } from './symbolstil.js';
import { symbolstilUmschalten } from './symbolstil-umschalten.js';

const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';
let zeiger = { x: 0, y: 0 };
if (typeof document !== 'undefined') document.addEventListener('pointerdown', (e) => { zeiger = { x: e.clientX, y: e.clientY }; }, true);

/** Einträge der Liste: je Palette ein Schalter mit Häkchen, dann «Paletten zurücksetzen». */
export function paletteneintraege() {
  const eintraege = liste().map((d) => {
    const an = sichtbar(d.id);
    const erlaubt = an || typeof d.aktiv !== 'function' || !!d.aktiv();
    return { titel: `${an ? '✓' : ' '}  ${d.titel}`, aktiv: erlaubt, hinweis: KEIN_PROJEKT, aktion: () => umschalten(d.id) };
  });
  eintraege.push({ trenner: true }, { titel: 'Paletten zurücksetzen', aktion: () => zuruecksetzen() });
  // Symbolsatz (Teil S): Häkchen «Symbole im Revit-Stil (Probe)»; der unauffälligste Ort, kein neues Register und keine neue Gruppe
  eintraege.push({ trenner: true }, { titel: `${revitAktiv() ? '✓' : ' '}  Symbole im Revit-Stil (Probe)`, aktion: () => umschaltenMitMeldung() });
  return eintraege;
}

async function umschaltenMitMeldung() {
  try { await symbolstilUmschalten(); }
  catch (e) { meldung(`Die Wahl des Symbolsatzes konnte nicht gespeichert werden: ${e.message ?? e}`, 'warnung', { vorschlag: e.vorschlag }); }
}

registrieren({
  id: 'ansicht.paletten', titel: 'Paletten', symbol: 'palette', kuerzel: null, register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  ausfuehren() {
    // unter dem Knopf, sonst an der Stelle des letzten Klicks
    const knopf = [...document.querySelectorAll('.mb-knopf[data-id="ansicht.paletten"]')].find((b) => b.getBoundingClientRect().width > 0);
    const r = knopf?.getBoundingClientRect();
    kontextmenue(r ? r.left : zeiger.x, r ? r.bottom : zeiger.y, paletteneintraege());
  },
});
