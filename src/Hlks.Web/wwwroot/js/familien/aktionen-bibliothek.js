// Aktionen der Familienbibliothek (Modul 2, C3): «Familien im Explorer …» (Register Gebäudetechnik › Apparate und Armaturen, Register «Familie» › Bibliothek,
// Kontextmenü und Knopf am Knoten «Familien»), «Aktualisieren …» (Knopf und Kontextmenü an Projektfamilien mit neuerer Version bzw. paralleler Änderung, Register
// «Familie») und das Laden bzw. Platzieren einer Bibliotheksfamilie aus dem Projektbrowser (zuerst familie_laden, dann das gewohnte Platzieren mit Typauswahl).
// Keine Fachlogik: die Dialoge in bibliothek-dialoge.js sprechen nur mit dem Kern.

import { registrieren } from '../oberflaeche/register.js';
import { zustand, element, auswaehlen } from '../zustand.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { sende } from '../oberflaeche/befehle.js';
import { aufElement } from '../oberflaeche/werkzeug.js';
import { aktiverEditor } from './aktiv.js';
import { familienListe, familienLaden } from './daten.js';
import { bibliothekAktualisieren } from './bibliothek-daten.js';
import { explorerDialog, aktualisierenDialog } from './bibliothek-dialoge.js';
import { wegeFuer } from './bibliothek-text.js';
import { platzieren } from './aktionen-familien.js';

const projektOffen = () => !!zustand.status?.offen;
const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';
const KEIN_EDITOR = 'Zuerst eine Familie im Projektbrowser doppelklicken (Familieneditor).';
const standVon = (bezug) => familienListe().find((f) => f.kennzeichen === bezug || f.name === bezug || f.id === bezug)?.bibliothekStand;
const kannAktualisieren = (bezug) => wegeFuer(standVon(bezug)).length > 0;
const KEIN_UNTERSCHIED = 'Die Familie entspricht der aktuellen Version der Bibliothek (oder ist nicht darin).';

registrieren({
  id: 'familie.explorer', titel: 'Familien im Explorer', symbol: 'famBibliothek', register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => explorerDialog(),
});
registrieren({
  id: 'familie.explorer-register', titel: 'Familien im Explorer', symbol: 'famBibliothek', register: 'familie', gruppe: 'Bibliothek',
  aktiv: () => projektOffen() && !!aktiverEditor(), hinweis: KEIN_EDITOR, ausfuehren: () => explorerDialog(),
});

registrieren({
  id: 'familie.aktualisieren', titel: 'Aktualisieren …', symbol: 'famAktualisieren',
  aktiv: (p) => projektOffen() && (!p?.familie || kannAktualisieren(p.familie)), hinweis: (p) => (projektOffen() ? KEIN_UNTERSCHIED : KEIN_PROJEKT),
  ausfuehren: (p) => aktualisierenDialog({ familie: p?.familie }),
});
registrieren({
  id: 'familie.aktualisieren-register', titel: 'Aktualisieren …', symbol: 'famAktualisieren', register: 'familie', gruppe: 'Bibliothek',
  aktiv: () => projektOffen() && !!aktiverEditor() && kannAktualisieren(aktiverEditor()?.kennzeichen), hinweis: () => (aktiverEditor() ? KEIN_UNTERSCHIED : KEIN_EDITOR),
  ausfuehren: () => aktualisierenDialog({ familie: aktiverEditor()?.kennzeichen }),
});

/** Lädt eine Bibliotheksfamilie ins Projekt (familie_laden { id } bzw. { datei }); gibt das Kennzeichen zurück oder null (Fehler stehen als Meldung). */
export async function bibliothekLaden(p = {}) {
  const r = await sende('familie_laden', p.id ? { id: p.id, ...(p.version ? { version: String(p.version) } : {}) } : { datei: p.datei });
  if (!r.angenommen) { meldung(r.fehler ?? 'Die Familie lässt sich nicht laden.', 'warnung', { vorschlag: r.vorschlag }); return null; }
  await Promise.all([familienLaden(), bibliothekAktualisieren()]);
  const kz = r.aenderungen?.find((a) => a.klasse === 'Familie')?.kennzeichen ?? familienListe().find((f) => f.id === p.id)?.kennzeichen ?? null;
  if (kz) await aufElement(kz);
  meldung((r.hinweise ?? [])[0] ?? `${kz} geladen.`, 'ok');
  return kz;
}

registrieren({
  id: 'familie.bibliothek-laden', titel: 'In das Projekt laden', symbol: 'famFamilie', aktiv: projektOffen, hinweis: KEIN_PROJEKT,
  async ausfuehren(p) { const kz = await bibliothekLaden(p ?? {}); if (kz) auswaehlen(element(kz)?.id ?? null); return kz; },
});
registrieren({
  id: 'familie.bibliothek-platzieren', titel: 'Platzieren', symbol: 'famPlatzieren', aktiv: projektOffen, hinweis: KEIN_PROJEKT,
  async ausfuehren(p) {
    const kz = await bibliothekLaden(p ?? {});
    return kz ? platzieren({ familie: kz }) : undefined;
  },
});
