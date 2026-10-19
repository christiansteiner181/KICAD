// Aktionen der Planblätter im Register «Ansicht», Gruppe «Planblätter»: Planblatt anlegen (Befehl planblatt_anlegen),
// Ansicht auf ein Blatt platzieren (ansicht_platzieren), PDF in neuem Fenster öffnen (/api/plan/{kz}.pdf), dazu «Planblatt
// öffnen» für das Kontextmenü im Projektbrowser. Alles läuft über Befehle des Kerns; hier stehen nur Dialoge und Aufrufe.
// Eingetragen wird nur über registrieren() (register.js); geladen wird diese Datei vom Projektbrowser.

import { zustand, element, derKlasse } from '../zustand.js';
import * as fenster from '../fenster.js';
import { registrieren } from './register.js';
import { zeigen, meldung } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { esc, knoten, vergleichen, aufElement, anzeigeName } from './werkzeug.js';

const FORMATE = ['A4', 'A3', 'A2', 'A1', 'A0'];
const FORMAT_VORGABE = 'A3';                      // Vorgabe des Kerns (Blattformate.Vorgabe)
const PLANARTEN = new Set(['grundriss', 'schnitt', 'ansicht', 'schema']);   // Schema seit Modul 3 (C2): ohne Massstab, Papier-mm

const bloetter = () => derKlasse('Planblatt').sort((a, b) => vergleichen(a.werte?.nummer ?? a.kennzeichen, b.werte?.nummer ?? b.kennzeichen));
const blattText = (b) => [b.kennzeichen, b.werte?.nummer, b.werte?.titel].filter(Boolean).join(' · ');

/** Ansichten, die auf ein Blatt dürfen: Grundriss, Schnitt, Ansicht, Schema (keine Vorlagen), die noch auf keinem Blatt stehen. */
function platzierbar() {
  const belegt = new Set(derKlasse('Ansichtsfenster').map((f) => String(f.bezuege?.ansicht ?? '').toLowerCase()));
  return derKlasse('Ansicht')
    .filter((a) => PLANARTEN.has(a.werte?.art) && a.werte?.vorlage !== true && !belegt.has(a.kennzeichen.toLowerCase()))
    .sort((a, b) => vergleichen(anzeigeName(a), anzeigeName(b)));
}

/** Planblatt aus Parameter, aktiver Ansicht oder – gibt es nur eines – dem einzigen Blatt. */
function blattVon(p) {
  const b = element(p?.planblatt ?? zustand.aktiveAnsicht);
  if (b?.klasse === 'Planblatt') return b;
  const alle = bloetter();
  return alle.length === 1 ? alle[0] : null;
}

// ---------------------------------------------------------------------------------------------- Planblatt anlegen

async function planblattAnlegen() {
  const w = knoten(`<div class="ansichtform">
    <label class="fz"><span>Plannummer</span><input type="text" class="f-nummer" placeholder="z. B. 2317-101"></label>
    <label class="fz"><span>Titel (Planinhalt)</span><input type="text" class="f-titel" placeholder="z. B. Grundriss Erdgeschoss Sanitär"></label>
    <label class="fz"><span>Format</span><select class="f-format">${FORMATE.map((f) => `<option ${f === FORMAT_VORGABE ? 'selected' : ''}>${f}</option>`).join('')}</select></label>
    <label class="haken"><input type="checkbox" class="f-hoch"> Hochformat</label>
  </div>`);
  const q = (s) => w.querySelector(s);
  const ok = await zeigen({
    titel: 'Planblatt anlegen', inhalt: w, breite: 460,
    beiOeffnen: () => q('.f-nummer').focus(),
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Anlegen', wert: true, primaer: true,
        pruefen: () => !q('.f-nummer').value.trim() ? 'Die Plannummer darf nicht leer sein.' : !q('.f-titel').value.trim() ? 'Der Titel darf nicht leer sein.' : true }],
  });
  if (ok !== true) return;
  const r = await sende('planblatt_anlegen', {
    nummer: q('.f-nummer').value.trim(), titel: q('.f-titel').value.trim(), format: q('.f-format').value, hochformat: q('.f-hoch').checked,
  });
  if (!ergebnisMelden(r, 'Planblatt angelegt.')) return;
  const neu = (r.aenderungen ?? []).find((a) => a.klasse === 'Planblatt' && a.art === 'angelegt');
  if (neu?.kennzeichen && await aufElement(neu.kennzeichen)) await fenster.ansichtOeffnen(neu.kennzeichen);
}

registrieren({
  id: 'plan.anlegen', titel: 'Planblatt', symbol: 'planblatt', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  ausfuehren: planblattAnlegen,
});

// ---------------------------------------------------------------------------------------------- Ansicht platzieren

async function ansichtPlatzieren(p) {
  const blaetter = bloetter(), ansichten = platzierbar();
  if (blaetter.length === 0) { meldung('Es gibt noch kein Planblatt.', 'info', { vorschlag: 'Zuerst ein Planblatt anlegen.' }); return; }
  if (ansichten.length === 0) { meldung('Alle Grundrisse, Schnitte, Ansichten und Schemas stehen schon auf einem Planblatt.', 'info', { vorschlag: 'Eine Ansicht duplizieren oder neu anlegen.' }); return; }
  const vorAnsicht = ansichten.find((a) => a.kennzeichen === (p?.ansicht ?? zustand.aktiveAnsicht)) ?? ansichten[0];
  const vorBlatt = blattVon(p) ?? blaetter[0];
  const w = knoten(`<div class="ansichtform">
    <label class="fz"><span>Ansicht</span><select class="f-ansicht">${ansichten.map((a) => `<option value="${esc(a.kennzeichen)}" ${a === vorAnsicht ? 'selected' : ''}>${esc(anzeigeName(a))} (${esc(a.kennzeichen)})</option>`).join('')}</select></label>
    <label class="fz"><span>Planblatt</span><select class="f-blatt">${blaetter.map((b) => `<option value="${esc(b.kennzeichen)}" ${b === vorBlatt ? 'selected' : ''}>${esc(blattText(b))}</option>`).join('')}</select></label>
    <p class="dlg-text leise">Die Lage auf dem Blatt wählt der Kern (Mitte des freien Bereichs); Massstab und Ausschnitt kommen von der Ansicht (ein Schema hat keinen Massstab).</p>
  </div>`);
  const ok = await zeigen({ titel: 'Ansicht platzieren', inhalt: w, breite: 520, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Platzieren', wert: true, primaer: true }] });
  if (ok !== true) return;
  const blatt = w.querySelector('.f-blatt').value, ansicht = w.querySelector('.f-ansicht').value;
  const r = await sende('ansicht_platzieren', { planblatt: blatt, ansicht });
  if (!ergebnisMelden(r, `Ansicht ${ansicht} auf ${blatt} platziert.`)) return;
  await fenster.ansichtOeffnen(blatt);
}

registrieren({
  id: 'plan.platzieren', titel: 'Ansicht platzieren', symbol: 'grundriss', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  aktiv: () => bloetter().length > 0 && platzierbar().length > 0,
  hinweis: () => (bloetter().length === 0 ? 'Zuerst ein Planblatt anlegen.' : 'Alle Ansichten stehen schon auf einem Planblatt.'),
  ausfuehren: ansichtPlatzieren,
});

// ---------------------------------------------------------------------------------------------- PDF und Öffnen

async function blattWaehlen(p, titel) {
  const direkt = blattVon(p);
  if (direkt) return direkt;
  const liste = bloetter();
  const w = knoten(`<div class="ansichtform"><label class="fz"><span>Planblatt</span><select class="f-blatt">${liste.map((b) => `<option value="${esc(b.kennzeichen)}">${esc(blattText(b))}</option>`).join('')}</select></label></div>`);
  const ok = await zeigen({ titel, inhalt: w, breite: 460, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'OK', wert: true, primaer: true }] });
  return ok === true ? element(w.querySelector('.f-blatt').value) : null;
}

registrieren({
  id: 'plan.pdf', titel: 'PDF öffnen', symbol: 'oeffnen', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  aktiv: () => bloetter().length > 0, hinweis: 'Zuerst ein Planblatt anlegen.',
  async ausfuehren(p) {
    const b = await blattWaehlen(p, 'PDF öffnen');
    if (!b) return;
    // Der Kern erzeugt das PDF beim Abruf; der Browser zeigt es im eingebauten Betrachter (Drucken von dort)
    window.open(`/api/plan/${encodeURIComponent(b.kennzeichen)}.pdf`, '_blank', 'noopener');
  },
});

// Kontextmenü des Projektbrowsers (ohne Register)
registrieren({
  id: 'plan.oeffnen', titel: 'Öffnen', symbol: 'planblatt', kuerzel: null,
  aktiv: (p) => !!blattVon(p), hinweis: 'Zuerst ein Planblatt wählen.',
  ausfuehren: async (p) => { await fenster.ansichtOeffnen(blattVon(p).kennzeichen); },
});
