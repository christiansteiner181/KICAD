// Aktionen für Leitungen (Modul 3, Teil C1): Gruppe «Leitungen» mit Rohr (PI, Register Heizung und Sanitär) und Kanal (DT, Register Lüftung), «Aufteilen mit Produktlinie …»
// (Kontextregister und Palette, nur für Leitungen ohne Serie) und die Listenansicht «Materialliste» (Register Ansicht › Erstellen,
// Kontextmenü auf «Listen» im Projektbrowser). Keine Fachlogik: alles geht über Befehle und Abfragen des Kerns
// (leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie, materialliste, element_anlegen).

import { registrieren } from './register.js';
import { zustand, element, derKlasse } from '../zustand.js';
import { ansichtOeffnen } from '../fenster.js';
import { meldung } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { aufElement, freiesKennzeichen } from './werkzeug.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { leitungWerkzeug } from '../werkzeuge/leitung.js';
import { parallelWerkzeug } from '../werkzeuge/parallel.js';
import { fallleitungWerkzeug } from '../werkzeuge/fallleitung.js';
import { steigleitungWerkzeug } from '../werkzeuge/steigleitung.js';
import { aufteilenDialog } from '../werkzeuge/leitung-bearbeiten.js';
import { istAltbestand } from '../werkzeuge/leitung-logik.js';

const projektOffen = () => !!zustand.status?.offen;
const imGrundriss = () => projektOffen() && planAnsicht()?.art === 'grundriss';
const nurGrundriss = () => (!projektOffen() ? 'Zuerst ein Projekt öffnen.' : planAnsicht() ? 'Geht nur in einem Grundriss.' : 'Zuerst einen Grundriss öffnen und aktivieren.');

// ---------------------------------------------------------------------------------------------- Rohr und Kanal zeichnen

// Register je Gewerk (04.11.2026): Rohr unter Heizung und Sanitär, Kanal unter Lüftung
for (const [id, titel, symbol, kuerzel, kategorie, wid, register] of [
  ['gt.rohr', 'Rohr', 'rohr', 'PI', 'Rohr', 'rohr', ['heizung', 'sanitaer']],
  ['gt.kanal', 'Kanal', 'kanal', 'DT', 'Kanal', 'kanal', ['lueftung']],
]) {
  registrieren({
    id, titel, symbol, kuerzel, register, gruppe: 'Leitungen',
    aktiv: imGrundriss, hinweis: nurGrundriss,
    gedrueckt: () => werkzeugAktiv() === wid,
    ausfuehren() { werkzeugStarten(leitungWerkzeug(kategorie)); },
  });
}

// Sanitär: Abwasser- und Versorgungsleitung (Christian 09.11.2026: «bei sanitär braucht es zudem eine funktion abwasserleitung und Versorgungsleitung»)
// – Rohr mit Systemauswahl nur Abwasser bzw. nur Versorgung (Druck) und eigenem Gedächtnis für System und Produktlinie
for (const [id, titel, variante] of [['sa.abwasser', 'Abwasserleitung', 'abwasser'], ['sa.versorgung', 'Versorgungsleitung', 'versorgung']]) {
  registrieren({
    id, titel, symbol: 'rohr', kuerzel: null, register: 'sanitaer', gruppe: 'Leitungen',
    aktiv: imGrundriss, hinweis: nurGrundriss,
    gedrueckt: () => werkzeugAktiv() === variante,
    ausfuehren() { werkzeugStarten(leitungWerkzeug('Rohr', variante)); },
  });
}

// ---------------------------------------------------------------------------------------------- Fallleitung (06.11.2026)

registrieren({
  id: 'sa.fallleitung', titel: 'Fallleitung', symbol: 'rohr', kuerzel: null, register: 'sanitaer', gruppe: 'Leitungen',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'fallleitung',
  ausfuehren() { werkzeugStarten(fallleitungWerkzeug()); },
});

registrieren({
  id: 'gt.steigleitung', titel: 'Steigleitung', symbol: 'rohr', kuerzel: null, register: ['sanitaer', 'heizung'], gruppe: 'Leitungen',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'steigleitung',
  ausfuehren() { werkzeugStarten(steigleitungWerkzeug()); },
});

registrieren({
  id: 'lu.steigleitung', titel: 'Steigleitung', symbol: 'kanal', kuerzel: null, register: 'lueftung', gruppe: 'Leitungen',   // AUL, FOL, ZUL, ABL (06.11.2026)
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'steigleitungLueftung',
  ausfuehren() { werkzeugStarten(steigleitungWerkzeug('steigleitungLueftung')); },
});

// ---------------------------------------------------------------------------------------------- Parallele Leitungen (Bearbeitungswerkzeuge B2)

registrieren({
  id: 'gt.parallel', titel: 'Parallele Leitungen', symbol: 'parallel', kuerzel: null, register: ['heizung', 'lueftung', 'sanitaer'], gruppe: 'Leitungen',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'parallel',
  ausfuehren() { werkzeugStarten(parallelWerkzeug()); },
});

// ---------------------------------------------------------------------------------------------- Aufteilen mit Produktlinie

const gewaehlteLeitung = (p) => element(p?.leitung ?? p?.element ?? zustand.auswahl);

const aufteilenAktiv = (p) => istAltbestand(gewaehlteLeitung(p));
const aufteilenHinweis = (p) => {
  const el = gewaehlteLeitung(p);
  if (!el || !['Rohr', 'Kanal'].includes(el.klasse)) return 'Zuerst eine Leitung (Rohr oder Kanal) wählen.';
  return `${el.kennzeichen} hat schon eine Produktlinie.`;
};

registrieren({
  id: 'leitung.aufteilen', titel: 'Aufteilen mit Produktlinie …', symbol: 'rohr', kuerzel: null,
  aktiv: aufteilenAktiv, hinweis: aufteilenHinweis,
  ausfuehren: (p) => aufteilenDialog(gewaehlteLeitung(p).kennzeichen),
});
registrieren({
  id: 'kontext.leitung-aufteilen', titel: 'Aufteilen mit Produktlinie …', symbol: 'rohr', kuerzel: null, register: 'kontext', gruppe: 'Leitung',
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

// ---------------------------------------------------------------------------------------------- Fliessrichtung aus der Quelle

// Christian 02.11.2026: Fliessrichtung aus dem Pumpeneinbau bzw. dem Lüftungsgerät, auf Befehl (fliessrichtung_aus_quelle, ein Paket).
const QUELLEN = ['Pumpe', 'Lueftungsgeraet'];
const gewaehlteQuelle = (p) => element(p?.element ?? zustand.auswahl);
registrieren({
  id: 'kontext.fliessrichtung', titel: 'Fliessrichtung ab hier', symbol: 'netzAb', kuerzel: null, register: 'kontext', gruppe: 'Netz',
  aktiv: (p) => QUELLEN.includes(gewaehlteQuelle(p)?.klasse),
  hinweis: 'Zuerst eine Pumpe oder ein Lüftungsgerät wählen.',
  async ausfuehren(p) {
    const r = await sende('fliessrichtung_aus_quelle', { quelle: gewaehlteQuelle(p).kennzeichen });
    ergebnisMelden(r, (r.hinweise ?? []).join(' ') || null);
  },
});
