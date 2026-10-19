// Aktionen «Familie aus Unterlagen» (Modul 2, C2): der Dialog (Register Gebäudetechnik › Apparate und Armaturen, Register «Familie» › Unterlagen, Kontextmenü und
// Knopf im Projektbrowser «Familien»), «Entwurf prüfen» (öffnet die Prüfansicht der Familie im Familieneditor) und der Befund-Browser («Unterlagen» im Register
// Familie). Dazu die Meldung, wenn ein KI-Lauf zu Ende ist – auch wenn der Dialog geschlossen wurde. Keine Fachlogik: alles geht über Befehle und Abfragen.

import { registrieren } from '../oberflaeche/register.js';
import { zustand, element } from '../zustand.js';
import { ansichtOeffnen } from '../fenster.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { aufElement } from '../oberflaeche/werkzeug.js';
import { aktiverEditor } from './aktiv.js';
import { familieAusUnterlagenDialog } from './unterlagen-dialog.js';
import { beiAuftrag } from './auftrag.js';
import { auftragEnde, ergebnisFamilien } from './unterlagen-text.js';

const projektOffen = () => !!zustand.status?.offen;
const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';
const KEIN_EDITOR = 'Zuerst eine Familie im Projektbrowser doppelklicken (Familieneditor).';
const imEditor = () => !!aktiverEditor();

/** Öffnet den Familieneditor der Familie und wählt den Reiter (Vorgabe «Entwurf prüfen»). Gibt den Editor zurück oder null. */
export async function entwurfPruefen(kz, tab = 'entwurf') {
  const f = await aufElement(kz);
  if (!f || f.klasse !== 'Familie') { meldung(`Familie «${kz}» gibt es nicht.`, 'warnung'); return null; }
  const o = await ansichtOeffnen(f.kennzeichen);
  const ed = o?.instanz ?? null;
  ed?.tabWaehlen?.(tab);
  return ed;
}

/** Dialog «Familie aus Unterlagen»; p: { familie? (ersetzen), vorschlag?, dateien? }. */
export function dialogOeffnen(p = {}) {
  return familieAusUnterlagenDialog({ familie: p.familie, vorschlag: p.vorschlag, dateien: p.dateien, pruefen: (kz) => entwurfPruefen(kz) });
}

registrieren({
  id: 'familie.aus-unterlagen', titel: 'Familie aus Unterlagen', symbol: 'famUnterlagen', register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: (p) => dialogOeffnen(p ?? {}),
});

registrieren({
  id: 'familie.aus-unterlagen-register', titel: 'Familie aus Unterlagen', symbol: 'famUnterlagen', register: 'familie', gruppe: 'Unterlagen',
  aktiv: () => projektOffen() && imEditor(), hinweis: KEIN_EDITOR, ausfuehren: () => dialogOeffnen({ vorschlag: aktiverEditor()?.kennzeichen }),
});

registrieren({
  id: 'familie.entwurf-pruefen', titel: 'Entwurf prüfen', symbol: 'famEntwurf', register: 'familie', gruppe: 'Unterlagen',
  aktiv: imEditor, hinweis: KEIN_EDITOR, ausfuehren: () => aktiverEditor()?.aktion('entwurf-pruefen'), gedrueckt: () => aktiverEditor()?.tab === 'entwurf',
});

registrieren({
  id: 'familie.unterlagen', titel: 'Unterlagen und Befunde', symbol: 'famBefunde', register: 'familie', gruppe: 'Unterlagen',
  aktiv: imEditor, hinweis: KEIN_EDITOR, ausfuehren: () => aktiverEditor()?.aktion('unterlagen'), gedrueckt: () => aktiverEditor()?.tab === 'befunde',
});

// Aktionen für Kontextmenü und Knöpfe des Projektbrowsers: kein Eintrag im Menüband
registrieren({
  id: 'familie.entwurf-pruefen-browser', titel: 'Entwurf prüfen', symbol: 'famEntwurf', aktiv: projektOffen, hinweis: KEIN_PROJEKT,
  ausfuehren: (p) => entwurfPruefen(element(p?.familie)?.kennzeichen ?? p?.familie),
});
registrieren({
  id: 'familie.ersetzen-aus-unterlagen', titel: 'Aus Unterlagen ersetzen …', symbol: 'famUnterlagen', aktiv: projektOffen, hinweis: KEIN_PROJEKT,
  ausfuehren: (p) => dialogOeffnen({ familie: p?.familie }),
});

// Ende eines KI-Laufs melden (auch bei geschlossenem Dialog); nur Läufe, die diese Seite laufen sah
const gesehen = new Set(), gemeldet = new Set();
beiAuftrag((a) => {
  if (!auftragEnde(a)) { gesehen.add(a.id); return; }
  if (!gesehen.has(a.id) || gemeldet.has(a.id)) return;
  gemeldet.add(a.id);
  if (a.status === 'fertig') {
    const fam = ergebnisFamilien(a);
    meldung(fam.length ? `KI-Lauf fertig: ${fam.map((f) => f.kennzeichen).join(', ')} als Entwurf angelegt – im Projektbrowser «Prüfen».` : 'KI-Lauf fertig, aber ohne Familie.', fam.length ? 'ok' : 'warnung');
  } else if (a.status === 'abgebrochen') meldung('KI-Lauf abgebrochen.', 'info');
  else meldung(`KI-Lauf fehlgeschlagen: ${a.fehler ?? 'unbekannter Fehler'}`, 'fehler');
});
