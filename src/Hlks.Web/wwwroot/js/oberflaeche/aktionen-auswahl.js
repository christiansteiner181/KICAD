// Auswahl wie Revit (Bearbeitungswerkzeuge B4, entwurf/bearbeitungswerkzeuge.md): Ändern › Auswahl › Filter (Mehrfachauswahl nach Klasse
// einschränken) und Kontextregister › Auswahl › «Alle Exemplare wählen» (gleiche Klasse, Familie bzw. Serie und Typ – in der Ansicht bzw. im
// Projekt). Tab (Leitungszug) und Umschalt+Klick (herausnehmen) stehen in werkzeuge/griffe.js bzw. werkzeuge/auswahl.js. Keine Fachlogik.

import { zustand, element, alleElemente } from '../zustand.js';
import { registrieren } from './register.js';
import { zeigen, meldung } from './dialoge.js';
import { esc, knoten } from './werkzeug.js';
import { auswahlElemente, istMehrfach, mengeSetzen } from '../werkzeuge/auswahl.js';
import { planAnsicht, geschossVon, werkzeugStarten } from '../werkzeuge/basis.js';
import { kopierenWerkzeug } from '../werkzeuge/aendern.js';
import { sende, ergebnisMelden } from './befehle.js';

const ZIEL = (p) => element(p?.element ?? zustand.auswahl);

/** Gleicher Typ wie Revit «Alle Exemplare»: gleiche Klasse; mit Familie bzw. Serie dieselbe und derselbe Typ. */
export function gleicherTyp(a, b) {
  if (!a || !b || a.klasse !== b.klasse) return false;
  const fa = a.bezuege?.familie ?? null, fb = b.bezuege?.familie ?? null;
  if (fa !== fb) return false;
  return (a.werte?.familientyp ?? null) === (b.werte?.familientyp ?? null);
}

async function filterDialog() {
  const alle = auswahlElemente();
  const jeKlasse = new Map();
  for (const e of alle) jeKlasse.set(e.klasse, (jeKlasse.get(e.klasse) ?? 0) + 1);
  const w = knoten(`<div><p class="dlg-text">${alle.length} Elemente gewählt. Welche Klassen bleiben in der Auswahl?</p>${[...jeKlasse].sort((x, y) => y[1] - x[1]).map(([k, n]) =>
    `<label class="haken"><input type="checkbox" value="${esc(k)}" checked> ${esc(k)} (${n})</label>`).join('<br>')}</div>`);
  const ok = await zeigen({ titel: 'Auswahlfilter', inhalt: w, breite: 420, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'OK', wert: true, primaer: true }] });
  if (!ok) return;
  const behalten = new Set([...w.querySelectorAll('input:checked')].map((i) => i.value));
  const neu = alle.filter((e) => behalten.has(e.klasse)).map((e) => e.id);
  if (neu.length === 0) { meldung('Keine Klasse gewählt – die Auswahl bleibt.', 'info'); return; }
  mengeSetzen(neu);
}

async function alleExemplare(p) {
  const el = ZIEL(p);
  if (!el) return;
  const wo = await zeigen({
    titel: 'Alle Exemplare wählen', breite: 460,
    inhalt: knoten(`<div><p class="dlg-text">Alle Elemente wie ${esc(el.kennzeichen)} (${esc(el.klasse)}${el.werte?.familientyp ? `, ${esc(el.werte.familientyp)}` : ''}) wählen:</p></div>`),
    knoepfe: [{ text: 'Abbrechen', wert: null }, { text: 'Im Projekt', wert: 'projekt' }, { text: 'In der Ansicht', wert: 'ansicht', primaer: true }],
  });
  if (!wo) return;
  const a = planAnsicht();
  const g = wo === 'ansicht' && a ? element(geschossVon(a)) : null;
  const treffer = alleElemente().filter((e) => gleicherTyp(e, el) && (!g || !e.geschoss || element(e.geschoss)?.id === g.id));
  mengeSetzen(treffer.map((e) => e.id));
  meldung(`${treffer.length} Exemplar(e) gewählt.`, 'ok');
}

registrieren({
  id: 'aendern.filter', titel: 'Filter', symbol: 'filter', kuerzel: null, register: 'aendern', gruppe: 'Auswahl', groesse: 'klein',
  aktiv: () => istMehrfach(), hinweis: 'Zuerst mehrere Elemente wählen (Strg+Klick, Fenster, Tab).',
  ausfuehren: filterDialog,
});
registrieren({
  id: 'kontext.alle-exemplare', titel: 'Alle Exemplare wählen', symbol: 'auswaehlen', kuerzel: null, register: 'kontext', gruppe: 'Auswahl',
  aktiv: (p) => !!ZIEL(p), hinweis: 'Zuerst ein Element auswählen.',
  ausfuehren: alleExemplare,
});

// ---------------------------------------------------------------------------------------------- Zwischenablage (Bearbeitungswerkzeuge B6)

let ablage = [];   // Kennzeichen

registrieren({
  id: 'aendern.ablage-kopieren', titel: 'In die Zwischenablage', symbol: 'duplizieren', kuerzel: 'Strg+C',
  aktiv: () => auswahlElemente().length > 0, hinweis: 'Zuerst Elemente wählen.',
  ausfuehren() { ablage = auswahlElemente().filter((e) => e.geometrie).map((e) => e.kennzeichen); meldung(`${ablage.length} Element(e) in der Zwischenablage.`, 'ok'); },
});

/** Strg+V: im selben Geschoss Kopieren mit Basis- und Zielpunkt; im Grundriss eines anderen Geschosses ausgerichtet (gleiche Lage, Höhe um den Kotenunterschied). */
async function einfuegen() {
  const els = ablage.map((kz) => element(kz)).filter(Boolean);
  if (!els.length) { meldung('Die Zwischenablage ist leer (Strg+C).', 'info'); return; }
  const a = planAnsicht();
  const ziel = a?.art === 'grundriss' ? element(geschossVon(a)) : null;
  const quelle = element(els[0].geschoss);
  if (ziel && quelle && ziel.id !== quelle.id) {
    const dz = Number(ziel.werte?.kote ?? 0) - Number(quelle.werte?.kote ?? 0);
    const r = await sende('elemente_kopieren', { elemente: els.map((e) => e.kennzeichen), dz, geschoss: ziel.kennzeichen });
    ergebnisMelden(r, `${els.length} Element(e) ausgerichtet in ${ziel.kennzeichen} eingefügt.`);
    return;
  }
  mengeSetzen(els.map((e) => e.id));
  setTimeout(() => werkzeugStarten(kopierenWerkzeug()), 50);
}
registrieren({ id: 'aendern.ablage-einfuegen', titel: 'Aus der Zwischenablage einfügen', symbol: 'duplizieren', kuerzel: 'Strg+V', ausfuehren: einfuegen });
