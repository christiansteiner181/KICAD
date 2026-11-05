// Raumbezeichnungen aus DWG (Christian 05.11.2026: «wird ein DWG hinterlegt, sollen die Raumbezeichnungen aus dem DWG übernommen werden können»).
// Register Architektur › Raum: Unterlage und Layer wählen, Vorschau je Raum (heute → aus dem DWG, alle Texte im Raum), Häkchen, Name und/oder Nummer
// übernehmen (Befehl raumbezeichnungen_uebernehmen, ein Paket). Raumstempel ohne Raum in KICAD: «Raum anlegen» versucht raum_erkennen am Stempel.

import { abfrage, ApiFehler } from '../api.js';
import { zustand } from '../zustand.js';
import { registrieren } from './register.js';
import { zeigen } from './dialoge.js';
import { knoten, esc } from './werkzeug.js';
import { sende, ergebnisMelden } from './befehle.js';

registrieren({
  id: 'arch.raumtexte-dwg', titel: 'Raumbezeichnungen aus DWG', symbol: 'cadEinfuegen', kuerzel: null, register: 'architektur', gruppe: 'Raum',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => raumtexteDialog(),
});

export async function raumtexteDialog() {
  const liste = (await abfrage('raumbezeichnungen_dwg', {}))?.unterlagen ?? [];
  const w = knoten(`<div style="display:grid;gap:10px">
    <div style="display:flex;gap:12px;flex-wrap:wrap;align-items:center">
      <label>Unterlage <select class="rt-unterlage">${liste.map((u) => `<option value="${esc(u.unterlage)}">${esc(u.unterlage)} · ${esc(u.name ?? '')}${u.geschoss ? ' · ' + esc(u.geschoss) : ''}</option>`).join('')}</select></label>
      <label>Layer <select class="rt-layer"></select></label>
      <label class="haken"><input type="checkbox" class="rt-name" checked> Name</label>
      <label class="haken"><input type="checkbox" class="rt-nummer" checked> Nummer</label>
    </div>
    <div class="rt-liste" style="max-height:52vh;overflow:auto">${liste.length ? 'Wird gelesen …' : 'Im Projekt ist keine DWG/DXF-Unterlage eingefügt (Register Einfügen › CAD).'}</div>
  </div>`);
  const q = (s) => w.querySelector(s);
  let daten = null;

  async function laden(layer) {
    if (!liste.length) return;
    q('.rt-liste').textContent = 'Wird gelesen …';
    try {
      daten = await abfrage('raumbezeichnungen_dwg', { unterlage: q('.rt-unterlage').value, layer: layer ?? '' });
      q('.rt-layer').innerHTML = (daten.layerListe ?? []).map((l) => `<option value="${esc(l.layer)}"${l.layer === daten.layer ? ' selected' : ''}>${esc(l.layer)} (${l.texteInRaeumen})</option>`).join('');
      const zeilen = daten.raeume.filter((r) => r.name || r.nummer).map((r) => `<tr><td><input type="checkbox" value="${esc(r.raum)}" checked></td>
        <td>${esc(r.raum)}</td><td>${esc(r.nameIst || '–')} / ${esc(r.nummerIst || '–')}</td><td><b>${esc(r.name || '–')}</b> / <b>${esc(r.nummer || '–')}</b></td>
        <td class="leise">${esc(r.texte.join(' · '))}</td></tr>`).join('');
      const leer = daten.raeume.filter((r) => !r.name && !r.nummer).length;
      const ohne = daten.ohneRaum ?? [];
      q('.rt-liste').innerHTML = `<table class="tabelle"><thead><tr><th></th><th>Raum</th><th>heute (Name / Nr.)</th><th>aus dem DWG</th><th>Texte im Raum</th></tr></thead><tbody>${zeilen}</tbody></table>
        <p class="leise">${leer} Raum/Räume ohne Raumstempel auf diesem Layer.</p>
        ${ohne.length ? `<p><b>${ohne.length} Raumstempel ohne Raum in KICAD</b> (z. B. im IFC kein Raum an dieser Stelle):</p>
          <table class="tabelle"><tbody>${ohne.map((o, i) => `<tr><td>${esc(o.name)}</td><td>${esc(o.nummer ?? '')}</td><td><button type="button" class="dlg-knopf" data-anlegen="${i}">Raum anlegen</button> <span class="rt-ergebnis-${i} leise"></span></td></tr>`).join('')}</tbody></table>` : ''}`;
    } catch (f) {
      q('.rt-liste').textContent = f instanceof ApiFehler ? `${f.message}${f.vorschlag ? ' – ' + f.vorschlag : ''}` : String(f.message ?? f);
    }
  }
  q('.rt-unterlage').addEventListener('change', () => laden(null));
  q('.rt-layer').addEventListener('change', () => laden(q('.rt-layer').value));
  w.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-anlegen]'); if (!b || !daten) return;
    const o = daten.ohneRaum[Number(b.dataset.anlegen)];
    const r = await sende('raum_erkennen', { geschoss: daten.geschoss, punkt: o.punkt, name: o.name, ...(o.nummer ? { nummer: o.nummer } : {}) });
    q(`.rt-ergebnis-${b.dataset.anlegen}`).textContent = r?.angenommen ? 'angelegt' : (r?.fehler ?? 'nicht möglich');
    if (r?.angenommen) b.disabled = true;
  });

  async function uebernehmen(ctrl) {
    if (!daten) return;
    const raeume = [...w.querySelectorAll('.rt-liste input[type=checkbox]:checked')].map((x) => x.value);
    if (!raeume.length) { ctrl.fehler('Kein Raum angehakt.'); return; }
    const r = await sende('raumbezeichnungen_uebernehmen', { unterlage: daten.unterlage, layer: daten.layer, raeume, name: q('.rt-name').checked, nummer: q('.rt-nummer').checked });
    if (ergebnisMelden(r)) ctrl.schliessen?.(true);
  }

  const offen = zeigen({
    titel: 'Raumbezeichnungen aus DWG', inhalt: w, breite: 980,
    knoepfe: [{ text: 'Schliessen', wert: false }, { text: 'Übernehmen', primaer: true, schliesst: false, klick: (ctrl) => uebernehmen(ctrl) }],
  });
  laden(null);
  return offen;
}
