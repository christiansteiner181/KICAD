// Aktionen für Leitungen (Modul 3, Teil C1): Register Gebäudetechnik › Gruppe «Leitungen» mit Rohr (PI) und Kanal (DT), «Aufteilen mit Serie …»
// (Kontextregister und Palette, nur für Leitungen ohne Serie) und die Listenansicht «Materialliste» (Register Ansicht › Erstellen,
// Kontextmenü auf «Listen» im Projektbrowser). Keine Fachlogik: alles geht über Befehle und Abfragen des Kerns
// (leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie, materialliste, element_anlegen).

import { registrieren } from './register.js';
import { zustand, element, derKlasse } from '../zustand.js';
import { ansichtOeffnen } from '../fenster.js';
import { meldung } from './dialoge.js';
import { sende } from './befehle.js';
import { aufElement, freiesKennzeichen } from './werkzeug.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { leitungWerkzeug } from '../werkzeuge/leitung.js';
import { aufteilenDialog } from '../werkzeuge/leitung-bearbeiten.js';
import { istAltbestand } from '../werkzeuge/leitung-logik.js';

const projektOffen = () => !!zustand.status?.offen;
const imGrundriss = () => projektOffen() && planAnsicht()?.art === 'grundriss';
const nurGrundriss = () => (!projektOffen() ? 'Zuerst ein Projekt öffnen.' : planAnsicht() ? 'Geht nur in einem Grundriss.' : 'Zuerst einen Grundriss öffnen und aktivieren.');

// ---------------------------------------------------------------------------------------------- Rohr und Kanal zeichnen

for (const [id, titel, symbol, kuerzel, kategorie, wid] of [
  ['gt.rohr', 'Rohr', 'rohr', 'PI', 'Rohr', 'rohr'],
  ['gt.kanal', 'Kanal', 'kanal', 'DT', 'Kanal', 'kanal'],
]) {
  registrieren({
    id, titel, symbol, kuerzel, register: 'gebaeudetechnik', gruppe: 'Leitungen',
    aktiv: imGrundriss, hinweis: nurGrundriss,
    gedrueckt: () => werkzeugAktiv() === wid,
    ausfuehren() { werkzeugStarten(leitungWerkzeug(kategorie)); },
  });
}

// ---------------------------------------------------------------------------------------------- Aufteilen mit Serie

const gewaehlteLeitung = (p) => element(p?.leitung ?? p?.element ?? zustand.auswahl);

const aufteilenAktiv = (p) => istAltbestand(gewaehlteLeitung(p));
const aufteilenHinweis = (p) => {
  const el = gewaehlteLeitung(p);
  if (!el || !['Rohr', 'Kanal'].includes(el.klasse)) return 'Zuerst eine Leitung (Rohr oder Kanal) wählen.';
  return `${el.kennzeichen} hat schon eine Serie.`;
};

registrieren({
  id: 'leitung.aufteilen', titel: 'Aufteilen mit Serie …', symbol: 'rohr', kuerzel: null,
  aktiv: aufteilenAktiv, hinweis: aufteilenHinweis,
  ausfuehren: (p) => aufteilenDialog(gewaehlteLeitung(p).kennzeichen),
});
registrieren({
  id: 'kontext.leitung-aufteilen', titel: 'Aufteilen mit Serie …', symbol: 'rohr', kuerzel: null, register: 'kontext', gruppe: 'Leitung',
  aktiv: aufteilenAktiv, hinweis: aufteilenHinweis,
  ausfuehren: (p) => aufteilenDialog(gewaehlteLeitung(p).kennzeichen),
});

// ---------------------------------------------------------------------------------------------- Materialliste

/** Öffnet die Listenansicht «Materialliste» (Ansicht art liste, liste materialliste); beim ersten Mal wird sie angelegt. */
export async function materiallisteOeffnen() {
  let a = derKlasse('Ansicht').find((x) => x.werte?.art === 'liste' && x.werte?.liste === 'materialliste' && x.werte?.vorlage !== true);
  if (!a) {
    const kz = freiesKennzeichen('AN-LI-MATERIALLISTE');
    const r = await sende('element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name: 'Materialliste', eigenschaften: { art: 'liste', liste: 'materialliste' } });
    if (!r.angenommen) { meldung(r.fehler || r.meldung || 'Die Liste konnte nicht angelegt werden.', 'warnung', { vorschlag: r.vorschlag }); return; }
    a = await aufElement(kz);
  }
  if (a) await ansichtOeffnen(a.kennzeichen);
  else meldung('Die neue Ansicht ist noch nicht eingetroffen.', 'warnung', { vorschlag: 'Im Projektbrowser unter «Listen» nachsehen.' });
}

registrieren({
  id: 'ansicht.materialliste', titel: 'Material​liste', symbol: 'liste', kuerzel: null, register: 'ansicht', gruppe: 'Erstellen',
  aktiv: projektOffen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: materiallisteOeffnen,
});
