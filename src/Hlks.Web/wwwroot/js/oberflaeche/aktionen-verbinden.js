// Verbinden und Trennen im Grundriss (Bearbeitungswerkzeuge A2, entwurf/bearbeitungswerkzeuge.md; Christian 03.11.2026: «ja passt»):
//   Ändern › Verändern › Verbinden und Kontextregister › Netz › Verbinden – startet das Werkzeug (zwei Anschlüsse anklicken, werkzeuge/verbinden.js);
//   Kontextregister › Netz › Trennen … – die Verbindungen des gewählten Elements; eine wird sofort getrennt, mehrere zeigt eine Auswahl;
//   Rechtsklick im Grundriss – dieselben Einträge (verbindungsEintraege, grundriss.js).
// Jede Änderung ist ein Kernbefehl (verbinden, trennen), rückgängig mit Strg+Z. Keine Fachlogik.

import { zustand, element } from '../zustand.js';
import { registrieren } from './register.js';
import { zeigen } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { esc, knoten } from './werkzeug.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { verbindenWerkzeug } from '../werkzeuge/verbinden.js';
import { verbindungenVon } from '../ansichten/schema-logik.js';

const imGrundriss = () => planAnsicht()?.art === 'grundriss';
const nurGrundriss = () => (planAnsicht() ? 'Geht nur in einem Grundriss.' : 'Zuerst einen Grundriss öffnen und aktivieren.');
const ZIEL = (p) => element(p?.element ?? zustand.auswahl);

/** Verbindungen des Elements als { id, text } (Kennzeichen.Anschluss ↔ Kennzeichen.Anschluss). */
export function verbindungenText(el) {
  if (!el) return [];
  return verbindungenVon(el.kennzeichen, zustand.verbindungen).map((v) => ({ id: v.id, text: `${el.kennzeichen}.${v.anschluss} ↔ ${v.gegen}.${v.gegenAnschluss}` }));
}

async function trennen(id, text) {
  const r = await sende('trennen', { verbindung: id });
  ergebnisMelden(r, `Getrennt: ${text}.`);
  return r.angenommen;
}

async function trennenDialog(el) {
  const liste = verbindungenText(el);
  if (liste.length === 0) return;
  if (liste.length === 1) { await trennen(liste[0].id, liste[0].text); return; }
  const w = knoten(`<div><p class="dlg-text">${esc(el.kennzeichen)} hat ${liste.length} Verbindungen. Welche trennen?</p>${liste.map((v, i) =>
    `<label class="haken"><input type="radio" name="trennen" value="${esc(v.id)}"${i === 0 ? ' checked' : ''}> ${esc(v.text)}</label>`).join('<br>')}</div>`);
  const ok = await zeigen({
    titel: 'Verbindung trennen', inhalt: w, breite: 480,
    knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Trennen', wert: true, primaer: true }],
  });
  if (!ok) return;
  const id = w.querySelector('input[name="trennen"]:checked')?.value;
  const v = liste.find((x) => x.id === id);
  if (v) await trennen(v.id, v.text);
}

const starten = () => werkzeugStarten(verbindenWerkzeug());

registrieren({
  id: 'aendern.verbinden', titel: 'Verbinden', symbol: 'schemaVerbinden', kuerzel: null, register: 'aendern', gruppe: 'Verändern', groesse: 'klein',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'verbinden',
  ausfuehren: starten,
});
registrieren({
  id: 'kontext.verbinden', titel: 'Verbinden', symbol: 'schemaVerbinden', kuerzel: null, register: 'kontext', gruppe: 'Netz',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'verbinden',
  ausfuehren: starten,
});
registrieren({
  id: 'kontext.trennen', titel: 'Trennen …', symbol: 'schemaTrennen', kuerzel: null, register: 'kontext', gruppe: 'Netz',
  aktiv: (p) => verbindungenText(ZIEL(p)).length > 0,
  hinweis: (p) => (ZIEL(p) ? `${ZIEL(p).kennzeichen} hat keine Verbindung.` : 'Zuerst ein Element auswählen.'),
  ausfuehren: (p) => trennenDialog(ZIEL(p)),
});

// ---------------------------------------------------------------------------------------------- Formteil tauschen (Bearbeitungswerkzeuge B3)

const istBogen = (el) => el?.klasse === 'Bogen';
const istTStueck = (el) => el?.klasse === 'TStueck';

async function plus(el) { ergebnisMelden(await sende('bogen_zu_tstueck', { bogen: el.kennzeichen }), `${el.kennzeichen} ist jetzt ein T-Stück.`); }
// Den offenen Anschluss bestimmt der Kern (kein eindeutiger: Ablehnung mit Vorschlag)
async function minus(el) { ergebnisMelden(await sende('tstueck_zu_bogen', { tstueck: el.kennzeichen }), `Abzweig von ${el.kennzeichen} entfernt.`); }

registrieren({
  id: 'kontext.plus', titel: 'Zu T-Stück (+)', symbol: 'formteil', kuerzel: null, register: 'kontext', gruppe: 'Formteil',
  aktiv: (p) => istBogen(ZIEL(p)), hinweis: 'Zuerst einen Bogen wählen.',
  ausfuehren: (p) => plus(ZIEL(p)),
});
registrieren({
  id: 'kontext.minus', titel: 'Abzweig entfernen (−)', symbol: 'formteil', kuerzel: null, register: 'kontext', gruppe: 'Formteil',
  aktiv: (p) => istTStueck(ZIEL(p)), hinweis: 'Zuerst ein T-Stück wählen.',
  ausfuehren: (p) => minus(ZIEL(p)),
});

/** Einträge für das Rechtsklick-Menü im Grundriss: je Verbindung «Trennen: …» (höchstens 8), dazu «Verbinden …»; an Formteilen Plus/Minus. */
export function verbindungsEintraege(el) {
  const liste = verbindungenText(el);
  const aus = liste.slice(0, 8).map((v) => ({ titel: `Trennen: ${v.text}`, aktion: () => trennen(v.id, v.text) }));
  if (istBogen(el)) aus.unshift({ titel: 'Zu T-Stück (+)', aktion: () => plus(el) });
  if (istTStueck(el)) aus.unshift({ titel: 'Abzweig entfernen (−)', aktion: () => minus(el) });
  if (liste.length > 0 || (el && (el.bezuege?.familie || ['Rohr', 'Kanal'].includes(el.klasse)))) aus.push({ titel: 'Verbinden …', aktion: starten });
  return aus;
}
