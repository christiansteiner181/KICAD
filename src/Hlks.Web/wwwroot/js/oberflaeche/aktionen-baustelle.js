// Aktion «Baustelle (Webseite)» (BIM to field Stufe 1, entwurf/bim-to-field.md): Knopf im Register Datei, Gruppe «Ausgabe». Kleiner Dialog mit
// Rolle (Monteur | Bauleitung) und Stand (freigegeben | Arbeitsstand – Monteur nur freigegeben, Entscheid Christian 02.11.2026). Der Export läuft
// im Kern (Abfrage baustelle_export, eine HTML-Datei in <Projekt>\ausgabe\baustelle); danach öffnet sich die Seite (GET /api/baustelle/datei).

import { abfrage, ApiFehler } from '../api.js';
import { zustand } from '../zustand.js';
import { registrieren } from './register.js';
import { zeigen, meldung } from './dialoge.js';
import { knoten, esc } from './werkzeug.js';
import { sende, ergebnisMelden } from './befehle.js';

// Register «BIM to Field» (Christian 12.11.2026: «mache für das einstellen aus KICAD einen Tab "BIM to Field"»): Webseiten für Monteur und
// Projektleiter, Zugänge (persönliche Links), Rückmeldungen. Der Monteur orientiert sich am Grundriss (was kommt wo, wo läuft es durch), die Schemas
// zeigen ihm den Zusammenhang, das 3D-Modell dort, wo der Grundriss nicht alle Zusammenhänge zeigt (Christian 12.11.2026).
async function webseite(rolle, stand) {
  try {
    const erg = await abfrage('baustelle_export', { rolle, stand });
    window.open('/api/baustelle/datei?pfad=' + encodeURIComponent(erg.datei), '_blank');
    meldung(`Webseite ${rolle === 'monteur' ? 'Monteur' : 'Projektleiter'} erzeugt: ${erg.datei}`, 'ok');
  } catch (e) { meldung(e instanceof ApiFehler ? e.message : String(e?.message ?? e), 'warnung', { vorschlag: e?.vorschlag ?? undefined }); }
}
const offen = () => !!zustand.status?.offen;
registrieren({
  id: 'bim.webseite-monteur', titel: 'Webseite Monteur', symbol: 'bildschirm', kuerzel: null, register: 'bim', gruppe: 'Webseiten',
  aktiv: offen, hinweis: 'Zuerst ein Projekt öffnen.', ausfuehren: () => webseite('monteur', 'freigegeben'),
});
registrieren({
  id: 'bim.webseite-projektleiter', titel: 'Webseite Projektleiter', symbol: 'bildschirm', kuerzel: null, register: 'bim', gruppe: 'Webseiten',
  aktiv: offen, hinweis: 'Zuerst ein Projekt öffnen.', ausfuehren: () => webseite('bauleitung', 'arbeitsstand'),
});
registrieren({
  id: 'bim.zugaenge', titel: 'Zugänge', symbol: 'eigenschaften', kuerzel: null, register: 'bim', gruppe: 'Zugänge',
  aktiv: offen, hinweis: 'Zuerst ein Projekt öffnen.', ausfuehren: () => zugaengeDialog(),
});

/** Zugänge des Projekts (persönliche Links für Monteur und Projektleiter des Unternehmers): anlegen, Link kopieren, entfernen. */
async function zugaengeDialog() {
  const w = knoten(`<div style="display:grid;gap:10px">
    <div class="zg-liste"></div>
    <fieldset><legend>Neuer Zugang</legend>
      <label class="fz"><span>Name</span><input type="text" class="zg-name" placeholder="z. B. Hans Muster (Firma)"></label>
      <label class="fz"><span>Rolle</span><select class="zg-rolle"><option value="monteur">Monteur – nur freigegebene Webseiten</option><option value="projektleiter">Projektleiter – alles, auch Arbeitsstand</option></select></label>
      <button type="button" class="dlg-knopf zg-neu">Zugang anlegen</button></fieldset>
    <p class="leise">Den Link einmal am Handy oder Tablet öffnen – das Gerät bleibt angemeldet. Entfernen macht den Link ungültig.</p></div>`);
  const q = (s) => w.querySelector(s);
  async function laden() {
    const d = await (await fetch('/api/baustelle/zugaenge')).json();
    const l = d.zugaenge ?? [];
    q('.zg-liste').innerHTML = (d.testserver ? '<p class="leise">Die Links zeigen auf den Baustellen-Testserver (Port ' + esc(d.port) + ').</p>' : '')
      + (l.length === 0 ? '<p class="leise">Noch keine Zugänge.</p>' : `<table class="tab"><thead><tr><th>Name</th><th>Rolle</th><th>Link</th><th></th></tr></thead><tbody>${l.map((z) =>
        `<tr data-s="${esc(z.schluessel)}"><td>${esc(z.name)}</td><td>${z.rolle === 'projektleiter' ? 'Projektleiter' : 'Monteur'}</td><td><input type="text" readonly value="${esc(z.link)}" size="38"></td>`
        + `<td><button type="button" class="dlg-knopf zg-kopie">Kopieren</button> <button type="button" class="dlg-knopf zg-weg">Entfernen</button></td></tr>`).join('')}</tbody></table>`);
  }
  w.addEventListener('click', async (e) => {
    const tr = e.target.closest('tr[data-s]');
    if (e.target.closest('.zg-kopie') && tr) { const v = tr.querySelector('input').value; try { await navigator.clipboard.writeText(v); meldung('Link kopiert.', 'ok'); } catch { tr.querySelector('input').select(); } }
    else if (e.target.closest('.zg-weg') && tr) { await fetch('/api/baustelle/zugaenge/' + encodeURIComponent(tr.dataset.s), { method: 'DELETE' }); await laden(); }
    else if (e.target.closest('.zg-neu')) {
      const name = q('.zg-name').value.trim();
      if (!name) { meldung('Bitte einen Namen eintragen.', 'warnung'); return; }
      const r = await fetch('/api/baustelle/zugaenge', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ name, rolle: q('.zg-rolle').value }) });
      const j = await r.json();
      if (!r.ok) { meldung(j.fehler ?? 'Zugang nicht angelegt.', 'warnung', { vorschlag: j.vorschlag ?? undefined }); return; }
      q('.zg-name').value = '';
      await laden();
    }
  });
  await laden();
  await zeigen({ titel: 'Zugänge KICAD Baustelle', inhalt: w, breite: 820, knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

registrieren({
  id: 'datei.baustelle-export', titel: 'Webseite …', symbol: 'bildschirm', kuerzel: null, register: 'bim', gruppe: 'Webseiten',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => baustelleDialog(),
});

// Rückkanal (Datei von der Baustellen-Webseite): Vorschau mit rueckmeldung_lesen, Übernahme der angehakten Meldungen mit rueckmeldung_uebernehmen
// (ein Paket; Status ans Element, Mängel/Fragen/Abweichungen als Problem-Marker). Übernehmen tut der Mensch.
registrieren({
  id: 'datei.baustelle-rueckmeldung', titel: 'Rückmeldungen', symbol: 'problemMarker', kuerzel: null, register: 'bim', gruppe: 'Rückmeldungen',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => rueckmeldungDialog(),
});

const ART = { status: 'Status', mangel: 'Mangel', frage: 'Frage', abweichung: 'Abweichung' };

export function rueckmeldungDialog() {
  let inhalt = null;
  const w = knoten(`<div style="display:grid;gap:10px">
    <div class="rm-eingang"></div>
    <label>Rückmeldedatei (.json von der Baustellen-Webseite) <input type="file" accept=".json,application/json" class="rm-datei"></label>
    <div class="rm-kopf leise"></div>
    <div class="rm-liste" style="max-height:50vh;overflow:auto"></div>
  </div>`);
  const q = (s) => w.querySelector(s);
  // Über den Testserver gesendete Rückmeldungen (<Projekt>eingangaustelle, BaustelleZugang.cs) zur Auswahl
  fetch('/api/baustelle/eingang').then((r) => r.ok ? r.json() : null).then((d) => {
    const liste = d?.dateien ?? [];
    if (!liste.length) return;
    q('.rm-eingang').innerHTML = `<b>Über den Testserver eingegangen</b><div style="display:flex;flex-wrap:wrap;gap:6px;margin-top:4px">${
      liste.map((x) => `<button type="button" class="dlg-knopf" data-eingang="${esc(x.datei)}">${esc(x.person ?? '?')} · ${x.meldungen} Meldungen · ${esc(new Date(x.zeit).toLocaleString('de-CH'))}</button>`).join('')}</div>`;
  }).catch(() => {});
  q('.rm-eingang').addEventListener('click', async (e) => {
    const b = e.target.closest('[data-eingang]'); if (!b) return;
    q('.rm-liste').textContent = 'Wird gelesen …';
    try { await zeigenVon(await (await fetch('/api/baustelle/eingang/' + encodeURIComponent(b.dataset.eingang))).text()); }
    catch (f2) { inhalt = null; q('.rm-liste').textContent = `Die Rückmeldung lässt sich nicht lesen: ${f2.message ?? f2}`; }
  });
  q('.rm-datei').addEventListener('change', async () => {
    const f = q('.rm-datei').files[0]; if (!f) return;
    q('.rm-liste').textContent = 'Wird gelesen …';
    await zeigenVon(await f.text());
  });
  async function zeigenVon(text) {
    try {
      inhalt = JSON.parse(text);
      const v = await abfrage('rueckmeldung_lesen', { inhalt });
      q('.rm-kopf').textContent = `Von ${v.person ?? '?'}, gespeichert ${v.gespeichert ?? '?'}, Webseite vom ${v.exportErstellt ?? '?'}` +
        (v.modellGeaendert ? ' – das Modell wurde seither geändert, bitte prüfen.' : '');
      q('.rm-liste').innerHTML = `<table class="tabelle"><thead><tr><th></th><th>Zeit</th><th>Element</th><th>Art</th><th>Inhalt</th><th>Fotos</th></tr></thead><tbody>${
        v.meldungen.map((m) => `<tr><td><input type="checkbox" value="${esc(m.id)}"${m.uebernommen || !m.gefunden ? '' : ' checked'}${m.gefunden ? '' : ' disabled'}></td>
          <td>${esc(new Date(m.zeit).toLocaleString('de-CH'))}</td><td>${esc(m.element ?? '')}${m.gefunden ? '' : ' (fehlt)'}</td><td>${esc(ART[m.art] ?? m.art)}</td>
          <td>${esc(m.art === 'status' ? m.status : [m.text, m.mass ? `Δx ${m.mass.dx} Δy ${m.mass.dy} Δz ${m.mass.dz} mm` : ''].filter((x) => x).join(' · '))}${m.uebernommen ? ' <span class="leise">(schon übernommen)</span>' : ''}</td>
          <td>${m.fotos || ''}</td></tr>`).join('')}</tbody></table>`;
    } catch (f2) {
      inhalt = null;
      q('.rm-liste').textContent = f2 instanceof ApiFehler ? `${f2.message}${f2.vorschlag ? ' – ' + f2.vorschlag : ''}` : `Die Datei lässt sich nicht lesen: ${f2.message ?? f2}`;
    }
  }

  async function uebernehmen(ctrl) {
    if (!inhalt) { ctrl.fehler('Zuerst eine Rückmeldedatei wählen.'); return; }
    const meldungen = [...w.querySelectorAll('.rm-liste input:checked')].map((x) => x.value);
    if (meldungen.length === 0) { ctrl.fehler('Keine Meldung angehakt.'); return; }
    const r = await sende('rueckmeldung_uebernehmen', { inhalt, meldungen });
    if (ergebnisMelden(r)) ctrl.schliessen?.(true);
  }

  return zeigen({
    titel: 'Rückmeldung Baustelle', inhalt: w, breite: 820,
    knoepfe: [{ text: 'Schliessen', wert: false }, { text: 'Übernehmen', primaer: true, schliesst: false, klick: (ctrl) => uebernehmen(ctrl) }],
  });
}

export function baustelleDialog() {
  const w = knoten(`<div class="bs-dialog" style="display:grid;gap:10px">
    <div><b>Rolle</b><br>
      <label class="haken"><input type="radio" name="bs-rolle" value="monteur" checked> Monteur – nur freigegebener Stand</label><br>
      <label class="haken"><input type="radio" name="bs-rolle" value="bauleitung"> Bauleitung (Projekt- und Bauleiter)</label></div>
    <div><b>Stand</b><br>
      <label class="haken"><input type="radio" name="bs-stand" value="freigegeben" checked> Freigegeben – mit meinem Namen als Freigabe</label><br>
      <label class="haken"><input type="radio" name="bs-stand" value="arbeitsstand"> Arbeitsstand – auf jeder Seite als «nicht freigegeben» vermerkt</label></div>
    <div class="leise">Eine einzige HTML-Datei mit 3D (inkl. Architekten-IFC), Grundrissen, Schemas, Elementinfo und Materialauszug. Läuft offline
      (Tablet, USB-Stick, Mailanhang). Ziel: Projektordner ausgabe\\baustelle; der Export kommt ins Ausgabeprotokoll.</div>
    <div class="bs-ergebnis"></div>
  </div>`);
  const q = (s) => w.querySelector(s);
  const stimmen = () => {
    const monteur = q('input[name="bs-rolle"]:checked').value === 'monteur';
    const arbeit = q('input[name="bs-stand"][value="arbeitsstand"]');
    arbeit.disabled = monteur; if (monteur) q('input[name="bs-stand"][value="freigegeben"]').checked = true;
  };
  for (const r of w.querySelectorAll('input[name="bs-rolle"]')) r.addEventListener('change', stimmen);
  stimmen();

  async function exportieren() {
    const par = { rolle: q('input[name="bs-rolle"]:checked').value, stand: q('input[name="bs-stand"]:checked').value };
    q('.bs-ergebnis').textContent = 'Webseite wird erzeugt …';
    try {
      const erg = await abfrage('baustelle_export', par);
      const mb = (erg.groesseBytes / 1048576).toFixed(1);
      q('.bs-ergebnis').innerHTML = '';
      q('.bs-ergebnis').append(knoten(`<div><b>Erzeugt:</b> <code></code><br>${erg.elemente} Elemente, ${erg.ansichten} Pläne, ${erg.geschosse3d} Geschosse in 3D, ${mb} MB</div>`));
      q('.bs-ergebnis code').textContent = erg.datei;
      window.open('/api/baustelle/datei?pfad=' + encodeURIComponent(erg.datei), '_blank');
      meldung('Baustellen-Webseite erzeugt: ' + erg.datei, 'ok');
    } catch (f) {
      q('.bs-ergebnis').textContent = f instanceof ApiFehler ? `${f.message}${f.vorschlag ? ' – ' + f.vorschlag : ''}` : String(f);
    }
  }

  return zeigen({
    titel: 'Baustelle (Webseite)', inhalt: w, breite: 560,
    knoepfe: [{ text: 'Schliessen', wert: true }, { text: 'Erzeugen', primaer: true, schliesst: false, klick: () => exportieren() }],
  });
}
