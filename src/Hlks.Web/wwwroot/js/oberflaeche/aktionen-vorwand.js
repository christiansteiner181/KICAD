// Aktionen der Geberit Vorwand (04.11.2026): Register Sanitär › Gruppe «Vorwand» mit Vorwand (Werkzeug), Montageelement (Werkzeug),
// Bestücken (vorwand_bestuecken für die gewählte Vorwand) und Stückliste (Abfrage vorwand_stueckliste im Dialog mit Befunden und Annahmen).
// Keine Fachlogik: Gestell, Befestigungen, Beplankung und Prüfungen rechnet der Kern (VorwandGestell.cs).

import { registrieren } from './register.js';
import { zustand, element } from '../zustand.js';
import { meldung, zeigen } from './dialoge.js';
import { sende } from './befehle.js';
import { abfrage } from '../api.js';
import { esc, knoten } from './werkzeug.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { vorwandWerkzeug, montageelementWerkzeug } from '../werkzeuge/vorwand.js';
import { elementtypenLaden } from '../werkzeuge/vorwand-typen.js';

const imGrundriss = () => !!zustand.status?.offen && planAnsicht()?.art === 'grundriss';
const nurGrundriss = () => (!zustand.status?.offen ? 'Zuerst ein Projekt öffnen.' : planAnsicht() ? 'Geht nur in einem Grundriss.' : 'Zuerst einen Grundriss öffnen und aktivieren.');
/** Gewählte Vorwand (auch über ein gewähltes Montageelement). */
const gewaehlteVorwand = (p) => {
  const el = element(p?.vorwand ?? p?.element ?? zustand.auswahl);
  if (el?.klasse === 'Vorwand') return el;
  if (el?.klasse === 'Montageelement' && el.teilVon) return element(el.teilVon);
  return null;
};

registrieren({
  id: 'sa.vorwand', titel: 'Vorwand', symbol: 'wand', kuerzel: null, register: 'sanitaer', gruppe: 'Vorwand',
  aktiv: imGrundriss, hinweis: nurGrundriss, gedrueckt: () => werkzeugAktiv() === 'vorwand',
  ausfuehren() { werkzeugStarten(vorwandWerkzeug()); },
});

registrieren({
  id: 'sa.montageelement', titel: 'Montage­element', symbol: 'apparat', kuerzel: null, register: 'sanitaer', gruppe: 'Vorwand',
  aktiv: imGrundriss, hinweis: nurGrundriss, gedrueckt: () => werkzeugAktiv() === 'montageelement',
  async ausfuehren() { await elementtypenLaden(); werkzeugStarten(montageelementWerkzeug()); },
});

registrieren({
  id: 'sa.vorwand-bestuecken', titel: 'Bestücken', symbol: 'haken', kuerzel: null, register: 'sanitaer', gruppe: 'Vorwand',
  aktiv: (p) => !!gewaehlteVorwand(p), hinweis: () => 'Zuerst eine Vorwand wählen.',
  async ausfuehren(p) {
    const r = await sende('vorwand_bestuecken', { vorwand: gewaehlteVorwand(p).kennzeichen });
    if (r?.angenommen) meldung((r.hinweise ?? []).join(' ') || 'Bestückt.', 'info');
    else meldung(r?.fehler || r?.meldung || 'Bestücken nicht möglich.', 'warnung', { vorschlag: r?.vorschlag });
  },
});

registrieren({
  id: 'sa.vorwand-stueckliste', titel: 'Stückliste Vorwand', symbol: 'liste', kuerzel: null, register: 'sanitaer', gruppe: 'Vorwand',
  aktiv: () => !!zustand.status?.offen, hinweis: () => 'Zuerst ein Projekt öffnen.',
  async ausfuehren(p) {
    const v = gewaehlteVorwand(p);
    const d = await abfrage('vorwand_stueckliste', v ? { vorwand: v.kennzeichen } : {});
    const liste = d?.vorwaende ?? [];
    if (!liste.length) { meldung('Das Projekt hat keine Vorwand.', 'info'); return; }
    const zahl = (x) => (x === null || x === undefined ? '' : String(x));
    const html = liste.map((w) => `
      <h3>${esc(w.vorwand)} · ${esc(w.system === 'gis' ? 'Geberit GIS' : 'Geberit Duofix')} · ${esc(zahl(w.laenge))} × ${esc(zahl(w.tiefe))} × ${esc(zahl(w.hoehe))} mm${w.raumhoch ? ' · raumhoch' : ''}</h3>
      <table class="tab"><thead><tr><th>Teil</th><th>Artikel</th><th>Zuschnitt mm</th><th>Anzahl</th></tr></thead><tbody>
      ${w.zeilen.map((z) => `<tr><td>${esc(z.titel)}</td><td>${esc(z.artikel ?? '')}</td><td>${esc(z.flaeche !== undefined ? `${z.flaeche} m²` : zahl(z.zuschnitt))}</td><td>${esc(zahl(z.anzahl))}</td></tr>`).join('')}
      </tbody></table>
      ${w.befunde.length ? `<p class="warnung"><b>Befunde</b></p><ul>${w.befunde.map((b) => `<li>${esc(b)}</li>`).join('')}</ul>` : ''}
      ${w.annahmen.length ? `<details><summary>Annahmen (${w.annahmen.length})</summary><ul>${w.annahmen.map((a) => `<li>${esc(a)}</li>`).join('')}</ul></details>` : ''}`).join('');
    await zeigen({ titel: 'Stückliste Vorwand', inhalt: knoten(`<div class="vorwand-stueckliste">${html}<p class="leise">${esc(d.grundlage ?? '')}</p></div>`), breite: 640, knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
  },
});
