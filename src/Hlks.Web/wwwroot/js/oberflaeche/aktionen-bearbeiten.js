// Aktionen der Register «Ändern» und «Ändern | <Klasse>»: Auswählen, Löschen (mit Trockenlauf), Rückgängig, Wiederholen,
// Eigenschaften ein/aus, Umbenennen, im Netz folgen, Warum. Jede Änderung geht als Befehl an den Kern; der Zustand
// ändert sich erst, wenn das Ereignis zurückkommt.

import { zustand, element, auswaehlen } from '../zustand.js';
import { abfrage } from '../api.js';
import { registrieren, modusSetzen, optionen } from './register.js';
import { zeigen, meldung, eingabe, warumDialog } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { esc, knoten, vergleichen } from './werkzeug.js';
import { umschalten, sichtbar } from './paletten.js';
import { istMehrfach, auswahlKennzeichen } from '../werkzeuge/auswahl.js';
import { beendeWerkzeug } from '../werkzeuge/basis.js';   // «Ändern», Löschen und Netz folgen beenden ein laufendes Werkzeug (Prüfbefund 04.11.2026)

const gewaehlt = () => element(zustand.auswahl);
const ZIEL = (p) => element(p?.element ?? zustand.auswahl);

// ---------------------------------------------------------------------------------------------- Ändern

registrieren({
  id: 'aendern.auswaehlen', titel: 'Ändern', symbol: 'auswaehlen', kuerzel: 'MD', register: 'aendern', gruppe: 'Auswahl',
  ausfuehren() { beendeWerkzeug(); modusSetzen('aendern'); },   // wie Revit: «Ändern» bricht das laufende Werkzeug ab
});

registrieren({
  id: 'aendern.abbrechen', titel: 'Abbrechen', symbol: 'auswaehlen', kuerzel: 'Esc',
  ausfuehren() { beendeWerkzeug(); modusSetzen('aendern'); auswaehlen(null); },
});

// ---------------------------------------------------------------------------------------------- Löschen

/** Löschen ohne Rückfrage (Christian 09.11.2026: «löschen soll ohne nachfrage löschen»): direkt ausführen; zurücknehmen mit Rückgängig.
 *  Lehnt der Kern ab, kommt die Meldung mit Vorschlag; neue Warnungen und Fehler nennt die Erfolgsmeldung. */
async function loeschen(p) {
  const el = ZIEL(p);
  if (!el) return;
  // Bearbeitungswerkzeuge B4: Mehrfachauswahl in einem Paket (elemente_loeschen)
  const mehr = !p?.element && istMehrfach() ? auswahlKennzeichen() : null;
  const kz = mehr ? `${mehr.length} Elemente` : el.kennzeichen;
  const befehl = mehr ? 'elemente_loeschen' : 'element_loeschen';
  const par = mehr ? { elemente: mehr } : { element: kz };
  beendeWerkzeug();
  const r = await sende(befehl, par);
  if (!r.angenommen) { meldung(`Löschen von ${kz} abgelehnt: ${r.fehler || r.meldung}`, 'warnung', { vorschlag: r.vorschlag }); return; }
  const schlimm = (r.neueBefunde ?? []).filter((b) => b.schwere === 'fehler' || b.schwere === 'warnung').length;
  ergebnisMelden(r, `${kz} gelöscht${schlimm ? ` – ${schlimm} neue Warnung(en)/Fehler` : ''} (Rückgängig: Strg+Z).`);
}

registrieren({
  id: 'aendern.loeschen', titel: 'Löschen', symbol: 'loeschen', kuerzel: 'DE', kuerzelAlt: ['Entf'], register: 'aendern', gruppe: 'Bearbeiten',
  aktiv: (p) => !!ZIEL(p), hinweis: 'Zuerst ein Element auswählen.',
  ausfuehren: loeschen,
});

registrieren({
  id: 'aendern.umbenennen', titel: 'Umbenennen', symbol: 'umbenennen', kuerzel: null,
  aktiv: (p) => !!ZIEL(p), hinweis: 'Zuerst ein Element auswählen.',
  async ausfuehren(p) {
    const el = ZIEL(p);
    const neu = await eingabe('Umbenennen', 'Name', el.name || el.kennzeichen, { leerErlaubt: false });
    if (neu === null || neu === (el.name ?? '')) return;
    ergebnisMelden(await sende('element_aendern', { element: el.kennzeichen, name: neu }), `Umbenannt in «${neu}».`);
  },
});

// ---------------------------------------------------------------------------------------------- Rückgängig / Wiederholen

registrieren({
  id: 'aendern.rueckgaengig', titel: 'Rückgängig', symbol: 'rueckgaengig', kuerzel: 'Strg+Z', register: 'aendern', gruppe: 'Verlauf', groesse: 'klein',
  async ausfuehren() {
    const r = await sende('rueckgaengig');
    if (r.angenommen) meldung('Rückgängig: ' + (r.meldung || 'zurückgenommen.'), 'ok');
    else meldung(r.fehler || r.meldung || 'Nichts rückgängig zu machen.', 'info', { vorschlag: r.vorschlag });
  },
});

registrieren({
  id: 'aendern.wiederholen', titel: 'Wiederholen', symbol: 'wiederholen', kuerzel: 'Strg+Y', register: 'aendern', gruppe: 'Verlauf', groesse: 'klein',
  async ausfuehren() {
    const r = await sende('wiederholen');
    if (r.angenommen) meldung('Wiederholt: ' + (r.meldung || 'wiederhergestellt.'), 'ok');
    else meldung(r.fehler || r.meldung || 'Nichts zu wiederholen.', 'info', { vorschlag: r.vorschlag });
  },
});

// ---------------------------------------------------------------------------------------------- Eigenschaften ein/aus

/** Blendet die Eigenschaftenpalette ein oder aus (Palettenverwaltung paletten.js, W1; alle Paletten: Register Ansicht › Paletten, aktionen-paletten.js). */
const paletteUmschalten = () => umschalten('eigenschaften');

registrieren({
  id: 'aendern.eigenschaften', titel: 'Eigenschaften', symbol: 'eigenschaften', kuerzel: 'PP', register: 'aendern', gruppe: 'Paletten',
  gedrueckt: () => sichtbar('eigenschaften'),
  ausfuehren: paletteUmschalten,
});

// ---------------------------------------------------------------------------------------------- Netz folgen

function netzDialogHtml(r, von, richtung) {
  const schritte = r.schritte ?? [];
  const end = r.endgeraete ?? [];
  return `<div class="netz">
    <p class="dlg-text">Von <b>${esc(von)}</b> ${richtung === 'stromab' ? 'stromab (mit der Fliessrichtung)' : 'stromauf (gegen die Fliessrichtung)'}: ${esc(r.anzahl ?? schritte.length)} Elemente.
      <span class="leise">Klick auf eine Zeile wählt das Element aus.</span></p>
    ${schritte.length === 0 ? '<p class="leise">Nichts gefunden.</p>' :
      `<table class="tab klickbar"><thead><tr><th>Tiefe</th><th>Element</th><th>Klasse</th><th>Geschoss</th><th>System / Strang</th><th>Länge bis hier</th></tr></thead><tbody>${schritte.map((s) =>
        `<tr data-kz="${esc(s.kennzeichen)}"><td>${esc(s.tiefe)}</td><td>${esc(s.kennzeichen)}</td><td>${esc(s.klasse)}</td><td>${esc(s.geschoss ?? '')}</td><td>${esc([s.system, s.strang].filter(Boolean).join(' / '))}</td><td>${s.leitungslaengeBisHier_m !== undefined ? esc(s.leitungslaengeBisHier_m) + ' m' : ''}</td></tr>`).join('')}</tbody></table>`}
    ${end.length ? `<h4>Endgeräte (${end.length})</h4><div class="chips">${end.map((k) => `<button type="button" class="chip" data-kz="${esc(typeof k === 'string' ? k : k?.kennzeichen ?? '')}">${esc(typeof k === 'string' ? k : k?.kennzeichen ?? JSON.stringify(k))}</button>`).join('')}</div>` : ''}
  </div>`;
}

async function netzFolgen(richtung, p) {
  const el = ZIEL(p);
  if (!el) return;
  optionen.netzRichtung = richtung;
  beendeWerkzeug();
  modusSetzen('netz', { von: el.kennzeichen, richtung });
  const param = { von: el.kennzeichen, richtung };
  if (optionen.netzMedium) param.medium = optionen.netzMedium;
  let r;
  try { r = await abfrage('netz_folgen', param); }
  catch (e) { meldung(e.message, 'warnung', { vorschlag: e.vorschlag }); return; }
  modusSetzen('netz', { von: el.kennzeichen, richtung, anzahl: r.anzahl ?? (r.schritte ?? []).length });
  const w = knoten(netzDialogHtml(r, el.kennzeichen, richtung));
  const waehlen = (kz) => { const x = element(kz); if (x) auswaehlen(x.id); };
  w.querySelectorAll('tr[data-kz], .chip[data-kz]').forEach((z) => {
    z.onclick = () => {
      w.querySelectorAll('.gewaehlt').forEach((g) => g.classList.remove('gewaehlt'));
      z.classList.add('gewaehlt');
      waehlen(z.dataset.kz);
    };
  });
  await zeigen({ titel: `Netz folgen – ${richtung}`, inhalt: w, breite: 760, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

const hatAuswahl = () => !!gewaehlt();
// N36 (Christian 13.11.2026: «Ausblenden»): Netzbefehle nur an Elementen mit Rolle im Netz – nicht an Wand, Raum, Text (wie das Rechtsklick-Menü, NUR_NETZ)
export const netzElement = (el) => !!el && !!el.rolle;
registrieren({
  id: 'kontext.netz-stromab', fuer: netzElement, titel: 'Stromab folgen', symbol: 'netzAb', kuerzel: null, register: 'kontext', gruppe: 'Netz',
  aktiv: hatAuswahl, hinweis: 'Zuerst ein Element auswählen.',
  ausfuehren: (p) => netzFolgen('stromab', p),
});
registrieren({
  id: 'kontext.netz-stromauf', fuer: netzElement, titel: 'Stromauf folgen', symbol: 'netzAuf', kuerzel: null, register: 'kontext', gruppe: 'Netz',
  aktiv: hatAuswahl, hinweis: 'Zuerst ein Element auswählen.',
  ausfuehren: (p) => netzFolgen('stromauf', p),
});
registrieren({
  id: 'kontext.netz-optionen', titel: 'Netz folgen', symbol: 'netzAb', kuerzel: null,
  aktiv: hatAuswahl, hinweis: 'Zuerst ein Element auswählen.',
  ausfuehren: (p) => netzFolgen(optionen.netzRichtung, p),
});

// ---------------------------------------------------------------------------------------------- Kontextregister: weitere

registrieren({
  id: 'kontext.loeschen', titel: 'Löschen', symbol: 'loeschen', kuerzel: null, register: 'kontext', gruppe: 'Bearbeiten',
  aktiv: hatAuswahl, hinweis: 'Zuerst ein Element auswählen.',
  ausfuehren: loeschen,
});
registrieren({
  id: 'kontext.eigenschaften', titel: 'Eigenschaften', symbol: 'eigenschaften', kuerzel: null, register: 'kontext', gruppe: 'Eigenschaften',
  gedrueckt: () => sichtbar('eigenschaften'),
  ausfuehren: paletteUmschalten,
});

/** Werte, die «Warum» erklären kann: gesetzte Eigenschaften, abgeleitete Grössen und die Zuordnung. */
function warumKandidaten(el) {
  const k = new Set(Object.keys(el.werte ?? {}));
  if (el.geometrie && el.geometrie.art !== 'punkt') { if (el.geometrie.art === 'polygon') { k.add('flaeche'); k.add('volumen'); } else k.add('laenge'); }
  k.add('geschoss'); k.add('raum'); k.add('system');
  k.delete('lagen');
  return [...k].sort(vergleichen);
}
registrieren({
  id: 'kontext.warum', titel: 'Warum', symbol: 'warum', kuerzel: null, register: 'kontext', gruppe: 'Herkunft',
  aktiv: hatAuswahl, hinweis: 'Zuerst ein Element auswählen.',
  async ausfuehren(p) {
    const el = ZIEL(p);
    if (!el) return;
    const eig = p?.eigenschaft ?? null;
    await warumDialog(el.kennzeichen, eig, eig ? [] : warumKandidaten(el));
  },
});
