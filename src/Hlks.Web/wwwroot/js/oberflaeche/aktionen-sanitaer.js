// Aktion «Sanitär-Zusammenstellung» (Modul 4 Teil A-O): Register Gebäudetechnik › Gruppe «Apparate und Armaturen» öffnet die Liste «Sanitär-Zusammenstellung (W3)» der aktiven Anlage
// und legt sie beim ersten Mal an (Ansicht art liste, liste sanitaer_zusammenstellung, anlage = Kennzeichen; Befehl element_anlegen wie beim Dialog «Liste anlegen»).
// Aktive Anlage: die Anlage des gewählten Elements (Anlage selbst, sonst über system › teilVon bis zur Anlage), sonst die Anlage der aktiven Listenansicht, sonst die einzige
// Anlage des Projekts; gibt es mehrere und keine ist bestimmbar, fragt ein kleiner Dialog. Keine Fachlogik: Summen und Formeln rechnet der Kern (Abfrage sanitaer_zusammenstellung).
// Die Bestimmung der Anlage (anlageVon, anlageWaehlen) steht in sanitaer-anlage.js (rein, Node-getestet).

import { registrieren } from './register.js';
import { zustand, element, derKlasse } from '../zustand.js';
import { ansichtOeffnen } from '../fenster.js';
import { meldung, zeigen } from './dialoge.js';
import { sende } from './befehle.js';
import { aufElement, freiesKennzeichen, esc, knoten, vergleichen } from './werkzeug.js';
import { aktiveAnlage, listeFuerAnlage, listenKennzeichen, listenName, anlageMitGewerk, anlagenVorne } from './sanitaer-anlage.js';
import { systemGewerk } from './projektbrowser-gliederung.js';

const projektOffen = () => !!zustand.status?.offen;
const anlagen = () => derKlasse('Anlage').sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen));
/** Anlage mit einem Sanitärsystem (Systemtyp bzw. Medium des Katalogs). */
const istSanitaer = (a) => {
  const ctx = { systemtypen: zustand.katalog?.systemtypen ?? [], medien: zustand.katalog?.medien ?? [] };
  return anlageMitGewerk(a, derKlasse('System'), (s) => systemGewerk(ctx, s), element, 'sanitaer');
};
const hatAnlage = () => projektOffen() && anlagen().length > 0;
const hinweis = () => (projektOffen() ? 'Das Projekt hat noch keine Anlage (Sanitäranlage).' : 'Zuerst ein Projekt öffnen.');

/** Fragt die Anlage, wenn mehrere in Frage kommen. */
async function anlageFragen(liste) {
  const w = knoten(`<div><p class="dlg-text">Das Projekt hat mehrere Anlagen. Für welche soll die Sanitär-Zusammenstellung geöffnet werden?</p>
    <label class="fz"><span>Anlage</span><select class="f-anlage">${liste.map((a) => `<option value="${esc(a.kennzeichen)}">${esc(a.kennzeichen)}${a.name ? ' – ' + esc(a.name) : ''}</option>`).join('')}</select></label></div>`);
  const ok = await zeigen({ titel: 'Sanitär-Zusammenstellung', inhalt: w, breite: 460, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Öffnen', wert: true, primaer: true }] });
  return ok === true ? element(w.querySelector('.f-anlage').value) : null;
}

/** Öffnet die Liste der Anlage (kz) – legt sie an, wenn es sie noch nicht gibt. */
export async function sanitaerListeOeffnen(anlageKz) {
  const anlage = element(anlageKz);
  if (!anlage) { meldung(`Anlage «${anlageKz}» nicht gefunden.`, 'warnung'); return; }
  let a = listeFuerAnlage(derKlasse('Ansicht'), anlage.kennzeichen);
  if (!a) {
    const kz = freiesKennzeichen(listenKennzeichen(anlage));
    const r = await sende('element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name: listenName(anlage), eigenschaften: { art: 'liste', liste: 'sanitaer_zusammenstellung', anlage: anlage.kennzeichen } });
    if (!r.angenommen) { meldung(r.fehler || r.meldung || 'Die Liste konnte nicht angelegt werden.', 'warnung', { vorschlag: r.vorschlag }); return; }
    a = await aufElement(kz);
  }
  if (a) await ansichtOeffnen(a.kennzeichen);
  else meldung('Die neue Ansicht ist noch nicht eingetroffen.', 'warnung', { vorschlag: 'Im Projektbrowser unter «Listen» nachsehen.' });
}

registrieren({
  id: 'sanitaer.zusammenstellung', titel: 'Sanitär-​Zusammen​stellung', symbol: 'sanZusammenstellung', kuerzel: null, register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen',
  aktiv: hatAnlage, hinweis,
  async ausfuehren(p) {
    const alle = anlagen();
    let anlage = p?.anlage ? element(p.anlage) : aktiveAnlage({ auswahl: element(zustand.auswahl), aktiveAnsicht: element(zustand.aktiveAnsicht), anlagen: alle, element, istSanitaer });
    if (!anlage && alle.length > 1) anlage = await anlageFragen(anlagenVorne(alle, istSanitaer));
    if (anlage) await sanitaerListeOeffnen(anlage.kennzeichen);
  },
});
