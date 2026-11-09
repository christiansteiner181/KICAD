// Anlagen und Systeme (Christian 09.11.2026: «wir brauchen eine möglichkeit systeme zu erstellen. auf dieser basis werden dann mustersysteme erfasst
// die wie die Familien in jedem Projekt als basis vorliegen.»). Register Koordination › Vorgaben › «Anlagen und Systeme»: Systeme des Projekts mit
// Systemtyp, Anlage, Gegenstück (VL/RL), Vorgabe-Produktlinie und Dämmdicke bearbeiten und neu anlegen, Anlagen anlegen, Systeme als Mustersysteme in
// die Systembibliothek (W:, neben den Familien) speichern bzw. dort entfernen und fehlende Mustersysteme ins Projekt übernehmen.
// Alles über die Befehle des Kerns: element_anlegen, element_aendern, mustersystem_speichern, mustersysteme_uebernehmen; Abfrage mustersysteme.

import { zustand, derKlasse, element } from '../zustand.js';
import { abfrage } from '../api.js';
import { serienListe } from '../werkzeuge/leitung-daten.js';
import { registrieren } from './register.js';
import { zeigen } from './dialoge.js';
import { knoten, esc } from './werkzeug.js';
import { sende, ergebnisMelden } from './befehle.js';

registrieren({
  id: 'koordination.systeme', titel: 'Anlagen und Systeme', symbol: 'koVorgaben', kuerzel: null, register: 'koordination', gruppe: 'Vorgaben',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => systemeDialog(),
});

const kz = (id) => (id ? element(id)?.kennzeichen ?? '' : '');
const wert = (el, n) => { const w = el?.werte?.[n]; return w && typeof w === 'object' && 'wert' in w ? w.wert : w; };
const sortiert = (l) => [...l].sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }));
const opt = (v, t, gewaehlt) => `<option value="${esc(v)}"${v === gewaehlt ? ' selected' : ''}>${esc(t)}</option>`;

export async function systemeDialog() {
  const w = knoten('<div class="sys-dlg" style="display:grid;gap:10px"></div>');
  let muster = null;

  async function laden() {
    try { muster = await abfrage('mustersysteme', {}); } catch (e) { muster = { hinweis: String(e?.message ?? e), systeme: [], bibliothek: {} }; }
    zeichnen();
  }

  function zeichnen() {
    const typen = zustand.katalog?.systemtypen ?? [];
    const anlagen = sortiert(derKlasse('Anlage'));
    const systeme = sortiert(derKlasse('System'));
    const linien = [...serienListe('Rohr'), ...serienListe('Kanal')];
    const inBib = new Set((muster?.systeme ?? []).map((s) => s.kennzeichen));
    const fehlen = (muster?.systeme ?? []).filter((s) => !s.imProjekt);
    const bib = muster?.bibliothek ?? {};
    const typOpts = (g) => opt('', '–', g) + typen.map((t) => opt(t.name, `${t.name} (${t.medium})`, g)).join('');
    const anlOpts = (g) => opt('', '–', g) + anlagen.map((a) => opt(a.kennzeichen, `${a.kennzeichen}${a.name ? ' – ' + a.name : ''}`, g)).join('');
    const sysOpts = (g, ohne) => opt('', '–', g) + systeme.filter((s) => s.kennzeichen !== ohne).map((s) => opt(s.kennzeichen, s.kennzeichen, g)).join('');
    const regeln = (zustand.katalog?.daemmregeln ?? []).map((r) => r.name);
    const regOpts = (g) => opt('', '–', g) + regeln.map((r) => opt(r, r, g)).join('');   // Dämmdicke nach Energiegesetz (09.11.2026)
    const linOpts = (g) => opt('', '–', g) + linien.map((l) => opt(l.kz, l.name, g)).join('');
    w.innerHTML = `
      <div class="leise">Mustersysteme: ${esc((muster?.systeme ?? []).length)} in der Systembibliothek ${esc(bib.pfad ?? '')}${bib.offline ? ' – <b>nicht erreichbar</b>, letzter bekannter Stand' : ''}.
        ${fehlen.length ? `${fehlen.length} fehlen im Projekt: ${esc(fehlen.map((s) => s.kennzeichen).join(', '))}.` : 'Alle sind im Projekt.'}
        ${muster?.hinweis ? `<br>${esc(muster.hinweis)}` : ''}</div>
      <table class="tabelle"><thead><tr><th title="Für «Als Mustersysteme speichern» bzw. «Aus Bibliothek entfernen» wählen">Wahl</th><th>Kennzeichen</th><th>Name</th>
        <th>Systemtyp</th><th>Anlage</th><th>Gegenstück</th><th>Produktlinie</th><th title="Dämmdicke nach Energiegesetz (MuKEn 2014 Anhang 4) aus der DN; geht der festen Dämmung vor">Dämmregel</th><th>Dämmung [mm]</th><th>Muster</th></tr></thead>
      <tbody>${systeme.map((s) => `<tr data-id="${esc(s.id)}">
        <td><input type="checkbox" class="sy-wahl"></td><td>${esc(s.kennzeichen)}</td>
        <td><input type="text" class="sy-name" value="${esc(s.name ?? '')}" size="16" data-kein-enter="1"></td>
        <td><select class="sy-typ">${typOpts(wert(s, 'systemtyp') ?? '')}</select></td>
        <td><select class="sy-anlage">${anlOpts(kz(s.teilVon))}</select></td>
        <td><select class="sy-gegen">${sysOpts(kz(s.bezuege?.gegenstueck), s.kennzeichen)}</select></td>
        <td><select class="sy-linie">${linOpts(wert(s, 'produktlinie') ?? '')}</select></td>
        <td><select class="sy-regel">${regOpts(wert(s, 'daemmregel') ?? '')}</select></td>
        <td><input type="text" inputmode="decimal" class="sy-daemm" value="${esc(wert(s, 'daemmdicke') ?? '')}" size="4" data-kein-enter="1"></td>
        <td class="leise">${inBib.has(s.kennzeichen) ? 'in Bibliothek' : '–'}</td></tr>`).join('')}
      <tr class="sy-neu"><td></td><td><input type="text" class="n-kz" placeholder="z. B. Hz-VL" size="8" data-kein-enter="1"></td>
        <td><input type="text" class="n-name" placeholder="Name" size="16" data-kein-enter="1"></td><td><select class="n-typ">${typOpts('')}</select></td>
        <td><select class="n-anlage">${anlOpts('')}</select></td><td></td><td><select class="n-linie">${linOpts('')}</select></td><td><select class="n-regel">${regOpts('')}</select></td>
        <td><input type="text" inputmode="decimal" class="n-daemm" size="4" data-kein-enter="1"></td><td><button type="button" class="dlg-knopf n-anlegen">System anlegen</button></td></tr>
      </tbody></table>
      <div style="display:flex;gap:8px;flex-wrap:wrap;align-items:center">
        <span>Neue Anlage:</span><input type="text" class="a-kz" placeholder="Kennzeichen, z. B. A-HZ" size="12" data-kein-enter="1">
        <input type="text" class="a-name" placeholder="Name, z. B. Heizung" size="16" data-kein-enter="1"><button type="button" class="dlg-knopf a-anlegen">Anlage anlegen</button>
        <span style="flex:1"></span>
        <button type="button" class="dlg-knopf p-setzen" title="Präfix des Gewerks vor die Kennzeichen setzen (Sa-KW, Hz-VL, Ka-VL, Lu-ZUL) und Systeme ohne Anlage der Hauptanlage ihres Gewerks zuordnen">Präfixe und Hauptanlagen</button>
        <button type="button" class="dlg-knopf m-uebernehmen"${fehlen.length ? '' : ' disabled'}>Fehlende Mustersysteme übernehmen</button>
        <button type="button" class="dlg-knopf m-speichern">Gewählte als Mustersysteme speichern</button>
        <button type="button" class="dlg-knopf m-entfernen">Gewählte aus Bibliothek entfernen</button>
      </div>`;
    binden();
  }

  const gewaehlt = () => [...w.querySelectorAll('tr[data-id]')].filter((tr) => tr.querySelector('.sy-wahl').checked).map((tr) => element(tr.dataset.id)?.kennzeichen).filter(Boolean);
  async function ausfuehren(befehl, par) { const r = await sende(befehl, par); const ok = ergebnisMelden(r); if (ok) await laden(); return ok; }

  function binden() {
    // Kennzeichen-Vorschlag mit Präfix (Christian 09.11.2026: «Mache bei allen Systemen einen Präfix. Sa, Hz, Ka, Lu usw.» – Format Sa-KW)
    let vorschlag = '';
    w.querySelector('.n-typ').onchange = (e) => {
      const t = (zustand.katalog?.systemtypen ?? []).find((x) => x.name === e.target.value);
      const feld = w.querySelector('.n-kz');
      const neu = t?.praefix ? `${t.praefix}-${t.kurz || t.medium}` : '';
      if (!feld.value.trim() || feld.value === vorschlag) feld.value = neu;
      vorschlag = neu;
      const name = w.querySelector('.n-name'); if (!name.value.trim() && t) name.value = t.name;
      // Hauptanlage des Gewerks vorwählen (Christian 09.11.2026: «es braucht auch eine main-anlage für alle gewerke»)
      const an = w.querySelector('.n-anlage'); if (!an.value && t?.praefix && [...an.options].some((o) => o.value === t.praefix)) an.value = t.praefix;
    };
    w.querySelector('.p-setzen').onclick = () => ausfuehren('system_praefixe_setzen', {});
    w.querySelector('.n-anlegen').onclick = async () => {
      const k = w.querySelector('.n-kz').value.trim(), typ = w.querySelector('.n-typ').value;
      if (!k || !typ) { ergebnisMelden({ angenommen: false, fehler: 'Kennzeichen und Systemtyp angeben.' }); return; }
      const eig = { systemtyp: typ };
      const lin = w.querySelector('.n-linie').value; if (lin) eig.produktlinie = lin;
      const reg = w.querySelector('.n-regel').value; if (reg) eig.daemmregel = reg;
      const d = Number(w.querySelector('.n-daemm').value.replace(',', '.')); if (d > 0) eig.daemmdicke = d;
      const o = { klasse: 'System', kennzeichen: k, name: w.querySelector('.n-name').value.trim() || typ, eigenschaften: eig };
      const an = w.querySelector('.n-anlage').value; if (an) o.teilVon = an;
      // ohne Anlage: Hauptanlage des Gewerks (legt sie bei Bedarf an)
      if (await ausfuehren('element_anlegen', o) && !an) await ausfuehren('system_praefixe_setzen', { systeme: [k] });
    };
    w.querySelector('.a-anlegen').onclick = async () => {
      const k = w.querySelector('.a-kz').value.trim();
      if (!k) { ergebnisMelden({ angenommen: false, fehler: 'Kennzeichen der Anlage angeben.' }); return; }
      await ausfuehren('element_anlegen', { klasse: 'Anlage', kennzeichen: k, name: w.querySelector('.a-name').value.trim() || k });
    };
    w.querySelector('.m-uebernehmen').onclick = () => ausfuehren('mustersysteme_uebernehmen', {});
    w.querySelector('.m-speichern').onclick = async () => {
      const l = gewaehlt();
      if (!l.length) { ergebnisMelden({ angenommen: false, fehler: 'Zuerst Systeme in der Spalte «Wahl» anhaken.' }); return; }
      await aenderungenSpeichern();
      await ausfuehren('mustersystem_speichern', { systeme: l });
    };
    w.querySelector('.m-entfernen').onclick = async () => {
      const l = gewaehlt();
      if (!l.length) { ergebnisMelden({ angenommen: false, fehler: 'Zuerst Systeme in der Spalte «Wahl» anhaken.' }); return; }
      await ausfuehren('mustersystem_speichern', { entfernen: l });
    };
  }

  /** Geänderte Zeilen speichern (element_aendern je System). */
  async function aenderungenSpeichern() {
    for (const tr of w.querySelectorAll('tr[data-id]')) {
      const s = element(tr.dataset.id);
      if (!s) continue;
      const eig = {}; const o = { element: s.kennzeichen };
      const name = tr.querySelector('.sy-name').value.trim();
      if (name !== (s.name ?? '')) o.name = name;
      const typ = tr.querySelector('.sy-typ').value; if (typ && typ !== (wert(s, 'systemtyp') ?? '')) eig.systemtyp = typ;
      const lin = tr.querySelector('.sy-linie').value; if (lin !== (wert(s, 'produktlinie') ?? '')) eig.produktlinie = lin || null;
      const reg = tr.querySelector('.sy-regel').value; if (reg !== (wert(s, 'daemmregel') ?? '')) eig.daemmregel = reg || null;
      const dt = tr.querySelector('.sy-daemm').value.trim().replace(',', '.'); const dAlt = wert(s, 'daemmdicke');
      if (dt !== String(dAlt ?? '')) eig.daemmdicke = dt === '' ? null : Number(dt);
      if (Object.keys(eig).length) o.eigenschaften = eig;
      const an = tr.querySelector('.sy-anlage').value; if (an !== kz(s.teilVon)) o.teilVon = an || null;
      const ge = tr.querySelector('.sy-gegen').value; if (ge !== kz(s.bezuege?.gegenstueck)) o.bezuege = { gegenstueck: ge || null };
      if (Object.keys(o).length === 1) continue;
      const r = await sende('element_aendern', o);
      if (!ergebnisMelden(r)) return false;
    }
    return true;
  }

  await laden();
  return zeigen({
    titel: 'Anlagen und Systeme', inhalt: w, breite: 1100,
    knoepfe: [{ text: 'Schliessen', wert: false }, { text: 'Speichern', primaer: true, schliesst: false, klick: async () => { if (await aenderungenSpeichern()) await laden(); } }],
  });
}
