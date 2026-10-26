// Palette «Problem-Marker» (Modul XR, XR-E «Problem-Marker mit Nachverfolgung», Quest App V1a): die Marker aus der Brille zum Nachverfolgen – Liste (offen, erledigt,
// alle; überfällige Frist rot), ein Klick wählt den Marker und springt in der 3D-Ansicht an den Blickpunkt der Brille (ansicht3d.js blickpunkt; ohne Blickpunkt schräg
// von oben auf die Stelle). Darunter das Formular für den gewählten: Status, Zuständig, Frist, Notiz – «Speichern» schickt den Befehl element_aendern des Kerns (ein Paket,
// rückgängig machbar, Abgleich mit den anderen KICAD-Plätzen). «Als BCF exportieren …» schreibt die Marker als BCF-Themen (Abfrage bcf_export, klasse ProblemMarker).
// Keine Fachlogik: die Marker liegen als Elemente ProblemMarker im Modell (zustand.js), reine Hilfen in problemmarker-text.js (Node-Test problemmarker.test.mjs).
// Palette rechts (Vorgabe), nur auf Verlangen sichtbar: Register Koordination › «Problem-Marker» bzw. Ansicht › Paletten.

import { zustand, element, derKlasse, abonnieren, auswaehlen, merken } from '../zustand.js';
import { abfrage } from '../api.js';
import * as fenster from '../fenster.js';
import { registrieren } from './register.js';
import { meldung, zeigen, bestaetigen } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { knoten, esc } from './werkzeug.js';
import { anmelden, zeigen as paletteZeigen, verbergen as paletteVerbergen } from './paletten.js';
import * as T from './problemmarker-text.js';

const st = { filter: 'offen', gewaehlt: null };
let panel = null;
let abo = null;

const q = (s) => panel?.querySelector(s);
const heute = () => new Date().toISOString().slice(0, 10);
const marker = () => derKlasse(T.KLASSE);

function bauen(slot) {
  if (!slot) return false;
  panel = knoten(`<div id="pm-panel" class="pm-panel" role="region" aria-label="Problem-Marker">
    <div class="pm-kopf"><span class="pm-filter"></span><span class="pm-zahl leise"></span>
      <button type="button" class="kp-knopf pm-bcf" title="Die Marker als BCF-Themen für Architekt und Fachplaner">Als BCF exportieren …</button></div>
    <div class="pm-liste" role="list"></div>
    <div class="pm-detail"></div>
  </div>`);
  slot.appendChild(panel);
  panel.addEventListener('click', klick);
  abo = abonnieren('modell', () => { if (panel) zeichnen(); });
  zeichnen();
  return true;
}

function abbauen() {
  abo?.(); abo = null;
  panel?.remove();
  panel = null;
}

/** Palette einblenden (rechts) und zeichnen. */
export function panelOeffnen() {
  if (!zustand.status?.offen) { meldung('Zuerst ein Projekt öffnen.', 'info'); return; }
  paletteZeigen('problemmarker');
  if (panel) zeichnen();
}

export function panelSchliessen() { paletteVerbergen('problemmarker'); }

anmelden({
  id: 'problemmarker', titel: 'Problem-Marker', ort: 'rechts', reihenfolge: 40, vorgabeSichtbar: false, fluechtig: true, lazy: true, scroll: true,
  elementId: 'palette-problemmarker', symbol: 'problemMarker',
  bauen, abbauen, oeffnen: () => panelOeffnen(), aktiv: () => !!zustand.status?.offen,
});

registrieren({
  id: 'koordination.problemMarker', titel: 'Problem-Marker', symbol: 'problemMarker', kuerzel: 'XM', register: 'xr', gruppe: 'Koordination', groesse: 'gross',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => panelOeffnen(),
});

// ---------------------------------------------------------------------------------------------- Zeichnen

function zeichnen() {
  if (!panel) return;
  const alle = marker();
  const z = T.zahlen(alle, heute());
  q('.pm-filter').innerHTML = T.FILTER.map(([id, t]) => `<button type="button" class="kp-reiter${st.filter === id ? ' aktiv' : ''}" data-filter="${id}">${esc(t)}</button>`).join('');
  q('.pm-zahl').textContent = `${z.offen} offen · ${z.erledigt} erledigt${z.ueberfaellig ? ` · ${z.ueberfaellig} überfällig` : ''}`;
  const l = T.liste(alle, st.filter);
  q('.pm-liste').innerHTML = l.length === 0
    ? `<p class="leise">${alle.length === 0 ? 'Noch keine Problem-Marker. In der Quest App: App «Marker» › «Problem markieren», dann auf die Stelle zeigen und Pinch.' : 'Keine Marker mit diesem Status.'}</p>`
    : l.map((m) => {
      const r = T.zeile(m, heute());
      return `<div class="pm-zeile${r.id === st.gewaehlt ? ' gewaehlt' : ''} pm-${r.status}" role="listitem" data-id="${esc(r.id)}" tabindex="0">
        <span class="pm-kz">${esc(r.kennzeichen)}</span><span class="pm-status">${esc(r.status)}</span>
        <span class="pm-notiz">${esc(r.notiz)}</span>
        <span class="pm-info leise">${esc([r.geschoss, r.von].filter(Boolean).join(' · '))}</span>
        <span class="pm-frist${r.ueberfaellig ? ' ueberfaellig' : ''}">${esc([r.zustaendig, r.frist ? 'bis ' + r.frist : ''].filter(Boolean).join(' · '))}</span>
      </div>`;
    }).join('');
  detailZeichnen();
}

function detailZeichnen() {
  const m = st.gewaehlt ? element(st.gewaehlt) : null;
  const d = q('.pm-detail');
  if (!m || m.klasse !== T.KLASSE) { d.innerHTML = ''; st.gewaehlt = null; return; }
  const w = m.werte ?? {};
  d.innerHTML = `<h4>${esc(m.kennzeichen)}${w.quelle === 'brille' ? ' <span class="leise">aus der Brille</span>' : ''}</h4>
    <label class="fz"><span>Status</span><select class="pm-st">${T.STATUS.map((s) => `<option value="${s}"${T.status(m) === s ? ' selected' : ''}>${s}</option>`).join('')}</select></label>
    <label class="fz"><span>Zuständig</span><input type="text" class="pm-zu" maxlength="${T.ZUSTAENDIG_MAX}" value="${esc(w.zustaendig ?? '')}" placeholder="Name, Firma bzw. Gewerk" data-kein-enter="1"></label>
    <label class="fz"><span>Frist</span><input type="date" class="pm-fr" value="${esc(T.datum(w.frist))}"></label>
    <label class="fz"><span>Notiz</span><textarea class="pm-no" rows="3" maxlength="${T.NOTIZ_MAX}">${esc(w.notiz ?? '')}</textarea></label>
    <div class="pm-knoepfe"><button type="button" class="kp-knopf primaer" data-pm="speichern">Speichern</button>
      <button type="button" class="kp-knopf" data-pm="3d">In 3D zeigen</button>
      <button type="button" class="kp-knopf" data-pm="loeschen">Löschen</button></div>
    <p class="pm-fehler fehlertext" hidden></p>`;
}

// ---------------------------------------------------------------------------------------------- Bedienen

async function klick(ev) {
  const f = ev.target.closest('[data-filter]');
  if (f) { st.filter = f.dataset.filter; zeichnen(); return; }
  if (ev.target.closest('.pm-bcf')) { await bcfExport(); return; }
  const a = ev.target.closest('[data-pm]');
  if (a) { await aktion(a.dataset.pm); return; }
  const z = ev.target.closest('.pm-zeile');
  if (z) {
    st.gewaehlt = z.dataset.id;
    auswaehlen(z.dataset.id);
    zeichnen();
    await in3d(element(z.dataset.id));
  }
}

async function aktion(was) {
  const m = st.gewaehlt ? element(st.gewaehlt) : null;
  if (!m) return;
  if (was === '3d') { await in3d(m); return; }
  if (was === 'loeschen') {
    if (!await bestaetigen(`${m.kennzeichen} löschen? Das lässt sich mit «Rückgängig» zurücknehmen.`, { ja: 'Löschen', gefahr: true, titel: 'Marker löschen' })) return;
    if (ergebnisMelden(await sende('element_loeschen', { element: m.id }), `${m.kennzeichen} gelöscht.`)) st.gewaehlt = null;
    return;
  }
  // speichern
  const r = T.aenderung(m, {
    status: q('.pm-st')?.value, zustaendig: q('.pm-zu')?.value, frist: q('.pm-fr')?.value, notiz: q('.pm-no')?.value,
  });
  const fehler = q('.pm-fehler');
  if (r.fehler) { fehler.hidden = false; fehler.textContent = r.fehler; return; }
  fehler.hidden = true;
  if (!r.parameter) { meldung('Keine Änderung.', 'info'); return; }
  ergebnisMelden(await sende('element_aendern', r.parameter), `${m.kennzeichen} gespeichert.`);
}

/** Die 3D-Ansicht für den Sprung: eine offene, sonst die erste (nach Kennzeichen); null ohne 3D-Ansicht. */
function ansicht3d() {
  const alle = derKlasse('Ansicht').filter((a) => a.werte?.art === '3d' && a.werte?.vorlage !== true)
    .sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }));
  const offen = new Set(fenster.alle().map((o) => o.id));
  return alle.find((a) => offen.has(a.id)) ?? alle[0] ?? null;
}

/** Öffnet die 3D-Ansicht und stellt die Kamera auf den Blickpunkt des Markers. */
async function in3d(m) {
  const k = T.kamera(m);
  if (!k) { meldung(`${m?.kennzeichen ?? 'Der Marker'} hat keine Lage.`, 'info'); return; }
  const a = ansicht3d();
  if (!a) { meldung('Es gibt keine 3D-Ansicht.', 'info', { vorschlag: 'Im Projektbrowser eine 3D-Ansicht anlegen (Ansicht › 3D-Ansicht).' }); return; }
  const o = await fenster.ansichtOeffnen(a.kennzeichen);
  if (!o?.instanz?.blickpunkt?.(k)) meldung('Die 3D-Ansicht konnte die Kamera nicht setzen.', 'warnung');
}

async function bcfExport() {
  const alt = String(merken.lesen?.('pmBcfDatei', '') ?? '');
  const ordner = alt.replace(/[\\/][^\\/]*$/, '');
  const vorschlag = ordner ? `${ordner}\\${T.bcfDateiname(zustand.status?.projekt, heute())}` : alt;
  const w = knoten(`<div class="kp-dlg"><p class="dlg-text">Schreibt die Problem-Marker (${esc(T.FILTER.find(([id]) => id === st.filter)?.[1] ?? 'Alle')}) als <b>BCF 3.0</b>:
    je Marker ein Thema mit Status, Zuständig, Frist, Notiz und dem Blickpunkt der Brille als Kamera. Ändert das Modell nicht; eine vorhandene Datei wird ersetzt.</p>
    <label class="fz"><span>Datei *</span><input type="text" class="b-datei" placeholder="C:\\Temp\\Problem-Marker.bcf" value="${esc(vorschlag)}" data-kein-enter="1"></label>
    <div class="b-ergebnis"></div></div>`);
  await zeigen({
    titel: 'Problem-Marker als BCF exportieren', inhalt: w, breite: 620,
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Exportieren', primaer: true, schliesst: false,
        pruefen: () => (/\.(bcf|bcfzip)$/i.test(w.querySelector('.b-datei').value.trim().replace(/"$/, '')) ? null : 'Die Datei braucht die Endung .bcf oder .bcfzip.'),
        async klick(ctrl) {
          const p = T.bcfParameter(w.querySelector('.b-datei').value, st.filter);
          merken.schreiben?.('pmBcfDatei', p.datei);
          try {
            const r = await abfrage('bcf_export', p);
            w.querySelector('.b-ergebnis').innerHTML = `<p class="ok">${esc(r.topics)} Thema/Themen geschrieben: ${esc(r.datei)}<br><span class="leise">${esc(r.schemapruefung ?? '')}</span></p>`;
            meldung(`BCF mit ${r.topics} Problem-Marker(n) geschrieben.`, 'ok');
          } catch (e) { ctrl.fehler(`${e.message ?? e}${e.vorschlag ? ' – ' + e.vorschlag : ''}`); }
        } }],
  });
}

// Tastatur: Eingabe auf einer Zeile der Liste wählt wie ein Klick
document.addEventListener('keydown', (ev) => {
  if (ev.key === 'Enter' && ev.target?.classList?.contains('pm-zeile')) ev.target.click();
});
