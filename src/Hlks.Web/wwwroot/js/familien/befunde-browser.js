// Befund-Browser im Familieneditor (Modul 2, C2), Register «Unterlagen»: links die Unterlagen der Familie, darunter die Befunde gefiltert nach Seite, Art
// und Text, rechts das Seitenbild mit den markierten Boxen. Mit einer markierten Zelle aus dem Reiter «Typen» (oder einer Parameterzeile) übernimmt
// «als Quelle übernehmen» den gewählten Befund in die Quellen dieser Zelle (familie_aendern typ_setzen bzw. parameter_setzen) – so belegt der Mensch
// Werte auch ohne KI. Der Kern prüft, ob Wert und Quelle zusammenpassen. Abfragen: unterlagen_lesen, befunde, unterlage_bild.
//
// Modul des Editors: { id, titel, eigen: true, erstellen(host, ctx) → { aktualisieren(), zerstoeren() } }. ctx: kz, def, fam, kandidaten(), zelle(), senden(ops).

import { abfrage } from '../api.js';
import { entprellen } from '../oberflaeche/werkzeug.js';
import { bildBetrachter } from './unterlagen-bild.js';
import { dateiHochladen, unterlageAblegenUndLesen } from './unterlagen-dialog.js';
import * as U from './unterlagen-text.js';
import { kernFehler } from './familien-text.js';
import { befundVolltext } from './quelle-ansehen.js';

const esc = U.esc;
const PRO_SEITE = 60;

function erstellen(host, ctx) {
  const ui = { ref: '', eintrag: null, seite: '', art: '', text: '', trefferSeite: 1, antwort: null, befunde: [], gewaehlt: null, wertUebernehmen: true, bildSeite: null };
  let tot = false, nr = 0, kandSig = '';

  host.innerHTML = `<div class="fu-bb">
    <div class="fu-bb-ul"><div class="fu-bb-ul-liste" role="listbox" aria-label="Unterlagen der Familie"></div>
      <button type="button" class="fe-knopf fu-bb-neu" title="Eine weitere Herstellerunterlage zu dieser Familie hinzufügen">＋ Unterlage hinzufügen …</button>
      <input type="file" class="fu-bb-datei" id="fu-bb-datei" accept="${esc(U.DATEI_ACCEPT)}" hidden></div>
    <div class="fu-bb-msg" hidden role="alert"></div>
    <div class="fu-bb-url leise" hidden></div>
    <div class="fu-bb-filter"></div>
    <div class="fu-bb-zelle"></div>
    <div class="fu-bb-haupt"><div class="fu-bb-liste"></div><div class="fu-bb-bildspalte"><div class="fu-bb-zoom"><button type="button" class="fe-knopf klein" data-bb-aktion="zoom-aus" title="Seitenbild verkleinern">−</button><button type="button" class="fe-knopf klein" data-bb-aktion="zoom-ein" title="Seitenbild vergrössern">＋</button><button type="button" class="fe-knopf klein" data-bb-aktion="zoom-eins" title="Seitenbild in die Breite einpassen">↔</button><span class="leise fu-bb-zoomtext"></span></div><div class="fu-bb-bild"></div><pre class="fu-bb-htmltext fu-bericht" hidden></pre></div></div>
  </div>`;
  const root = host.querySelector('.fu-bb');
  const q = (s) => root.querySelector(s);
  const viewer = bildBetrachter(q('.fu-bb-bild'), { klasse: 'fu-bb-seite' });

  const msg = (art, text) => { const m = q('.fu-bb-msg'); m.hidden = !text; m.className = `fu-bb-msg ${art}`; m.textContent = text ?? ''; };
  const kand = () => U.unterlagenKandidaten(ctx.def, ctx.kz, ctx.elemente());   // zuerst die der Familie (quelle definition/bezug), dann «weitere im Projekt» (frei)

  // ---------------------------------------------------------------------------------------- Unterlagen
  function ulZeichnen() {
    const alle = kand();
    const eigene = alle.filter((u) => u.quelle !== 'frei'), weitere = alle.filter((u) => u.quelle === 'frei');
    const knopf = (u, extra = '') => `<button type="button" class="fu-bb-ul-eintrag${extra}${u.ref === ui.ref ? ' gewaehlt' : ''}" role="option" data-ref="${esc(u.ref)}" title="${esc(u.ref)}">${esc(u.datei)} <span class="leise">${esc(U.ART_NAMEN[u.art] ?? u.art ?? '')}</span></button>`;
    q('.fu-bb-ul-liste').innerHTML = (eigene.length === 0 ? '<span class="leise">Die Familie nennt noch keine Unterlage als Quelle.</span>' : eigene.map((u) => knopf(u)).join(''))
      + (weitere.length ? `<span class="fu-bb-ul-weitere leise" title="Herstellerunterlagen des Projekts, die diese Familie noch nicht als Quelle nennt">weitere im Projekt:</span>${weitere.map((u) => knopf(u, ' weitere')).join('')}` : '');
  }

  /** Zeile «Aus dem Internet: <Link>» über den Filtern, wenn die gewählte Unterlage aus dem Internet stammt. */
  function urlZeichnen() {
    const u = kand().find((x) => x.ref === ui.ref);
    const z = q('.fu-bb-url');
    z.hidden = !u?.url;
    z.innerHTML = u?.url ? `Aus dem Internet: ${U.urlLink(u.url)}${u.abgerufen ? ` (abgerufen ${esc(u.abgerufen)})` : ''}` : '';
  }

  async function unterlageWaehlen(ref) {
    ui.ref = ref; ui.eintrag = null; ui.seite = ''; ui.art = ''; ui.text = ''; ui.trefferSeite = 1; ui.gewaehlt = null; ui.befunde = []; ui.antwort = null; ui.bildSeite = null;
    ulZeichnen(); filterZeichnen(); listeZeichnen(); urlZeichnen();
    if (!ref) { viewer.leeren('Keine Unterlage gewählt.'); return; }
    const mein = ++nr;
    try {
      const l = await abfrage('unterlagen_lesen', { unterlagen: [ref] });
      if (mein !== nr || tot) return;
      ui.eintrag = (l.unterlagen ?? [])[0] ?? null;
      if (ui.eintrag?.fehler) { msg('fehler', `${ui.eintrag.fehler}${ui.eintrag.vorschlag ? ' – ' + ui.eintrag.vorschlag : ''}`); return; }
      msg('', '');
      ui.seite = ui.eintrag?.art === 'pdf' ? '1' : '';
    } catch (e) { msg('fehler', `${e.message}${e.vorschlag ? ' – ' + e.vorschlag : ''}`); return; }
    filterZeichnen();
    await Promise.all([befundeLaden(), bildLaden()]);
  }

  // ---------------------------------------------------------------------------------------- Filter
  function filterZeichnen() {
    const e = ui.eintrag;
    if (!e) { q('.fu-bb-filter').innerHTML = ''; return; }
    const arten = Object.keys(e.jeArt ?? {});
    const seiten = e.art === 'pdf' ? e.seiten ?? 1 : 0;
    q('.fu-bb-filter').innerHTML = `
      ${e.art === 'pdf' ? `<label>Seite <select class="fe-in" data-bb="seite"><option value="">alle</option>${Array.from({ length: Math.min(seiten, 200) }, (_, i) => `<option value="${i + 1}"${String(i + 1) === ui.seite ? ' selected' : ''}>${i + 1}</option>`).join('')}</select></label>` : ''}
      <label>Art <select class="fe-in" data-bb="art"><option value="">alle</option>${arten.map((a) => `<option value="${esc(a)}"${a === ui.art ? ' selected' : ''}>${esc(a)} (${e.jeArt[a]})</option>`).join('')}</select></label>
      <label>Text <input type="text" class="fe-in" data-bb="text" data-kein-enter="1" value="${esc(ui.text)}" placeholder="Wert oder Text" size="14"></label>
      <span class="fu-bb-treffer leise"></span>
      <span class="fu-bb-blaettern"><button type="button" class="fe-knopf klein" data-bb-aktion="zurueck" title="Vorherige Trefferseite">◀</button><button type="button" class="fe-knopf klein" data-bb-aktion="weiter" title="Nächste Trefferseite">▶</button></span>`;
    trefferZeichnen();
  }
  function trefferZeichnen() {
    const a = ui.antwort;
    const t = root.querySelector('.fu-bb-treffer');
    if (t) t.textContent = a ? `${a.treffer} Treffer${a.trefferSeiten > 1 ? ` · Trefferseite ${a.trefferSeite} von ${a.trefferSeiten}` : ''}` : '';
    const z = root.querySelector('[data-bb-aktion="zurueck"]'), w = root.querySelector('[data-bb-aktion="weiter"]');
    if (z) z.disabled = !a || a.trefferSeite <= 1;
    if (w) w.disabled = !a || a.trefferSeite >= a.trefferSeiten;
  }

  // ---------------------------------------------------------------------------------------- Befunde
  async function befundeLaden() {
    if (!ui.ref) return;
    const mein = ++nr;
    const p = { unterlage: ui.ref, proSeite: PRO_SEITE, trefferSeite: ui.trefferSeite };
    if (ui.seite) p.seite = Number(ui.seite);
    if (ui.art) p.art = ui.art;
    if (ui.text.trim()) p.text = ui.text.trim();
    try {
      const r = await abfrage('befunde', p);
      if (mein !== nr || tot) return;
      ui.antwort = r; ui.befunde = r.befunde ?? [];
      if (ui.gewaehlt && !ui.befunde.some((b) => b.id === ui.gewaehlt)) ui.gewaehlt = null;
      msg('', '');
    } catch (e) { if (mein === nr) { ui.antwort = null; ui.befunde = []; msg('fehler', `${e.message}${e.vorschlag ? ' – ' + e.vorschlag : ''}`); } }
    listeZeichnen(); trefferZeichnen(); markenSetzen();
  }
  const befundeNeu = entprellen(() => { ui.trefferSeite = 1; befundeLaden(); }, 300);

  function listeZeichnen() {
    const l = q('.fu-bb-liste');
    if (!ui.eintrag) { l.innerHTML = ui.ref ? '<p class="leise">Unterlage wird gelesen …</p>' : ''; return; }
    if (ui.befunde.length === 0) { l.innerHTML = `<p class="leise">${esc(ui.antwort?.hinweis ?? 'Keine Befunde mit diesen Filtern.')}</p>`; return; }
    l.innerHTML = `<table class="tab fu-bb-tab"><thead><tr><th>Nr.</th><th>Art</th><th>Wert</th><th>Stelle</th></tr></thead><tbody>${ui.befunde.map((b) => `<tr class="fu-bb-zeile${b.id === ui.gewaehlt ? ' gewaehlt' : ''}" data-id="${esc(b.id)}" tabindex="0">
        <td>#${esc(String(b.id).split('#')[1] ?? b.id)}</td><td>${esc(b.art)}</td><td class="fu-bb-wert">${esc(U.befundText(b))}</td><td class="leise">${esc(U.befundStelle(b))}</td></tr>`).join('')}</tbody></table>`;
  }

  function markenSetzen() {
    viewer.markieren(ui.befunde.filter((b) => Array.isArray(b.box)).map((b) => ({ box: b.box, id: b.id, klasse: b.id === ui.gewaehlt ? 'gewaehlt' : 'befund', titel: `${b.art} ${U.befundText(b)}` })));
  }

  async function bildLaden() {
    if (!ui.ref || !ui.eintrag) return;
    if (ui.eintrag.art === 'html') { ui.bildSeite = null; viewer.leeren('Webseite: kein Seitenbild. Die gelesenen Texte, Zeilen und Tabellenzellen stehen in der Liste; der gewählte Befund unten.'); return; }
    const p = { unterlage: ui.ref, breite: 900 };
    if (ui.eintrag.art === 'pdf') p.seite = Number(ui.seite || 1);
    try { await viewer.laden(p); viewer.beiKlick(bildKlick); ui.bildSeite = p.seite ?? 0; markenSetzen(); }
    catch { ui.bildSeite = null; /* der Betrachter zeigt die Meldung */ }
  }

  function waehlen(id, scrollen = true) {
    ui.gewaehlt = id;
    for (const z of root.querySelectorAll('.fu-bb-zeile')) z.classList.toggle('gewaehlt', z.dataset.id === id);
    if (scrollen) root.querySelector('.fu-bb-zeile.gewaehlt')?.scrollIntoView({ block: 'nearest' });
    markenSetzen(); zelleZeichnen(); htmlDetail();
    if (scrollen === false) viewer.zuMarke(id);
  }
  /** Bei Webseiten (kein Seitenbild) der volle Text des gewählten Befunds. */
  function htmlDetail() {
    const d = q('.fu-bb-htmltext');
    const b = ui.befunde.find((x) => x.id === ui.gewaehlt);
    if (ui.eintrag?.art !== 'html' || !b) { d.hidden = true; d.textContent = ''; return; }
    d.hidden = false; d.textContent = befundVolltext(b);
  }
  function bildKlick({ punkt }) {
    const b = U.befundUnterPunkt(ui.befunde, punkt);
    if (b) waehlen(b.id);
  }

  // ---------------------------------------------------------------------------------------- Markierte Zelle, Quelle übernehmen
  function zelleZeichnen() {
    const z = ctx.zelle();
    const b = ui.befunde.find((x) => x.id === ui.gewaehlt);
    const p = z ? (ctx.def.parameter ?? []).find((x) => x.name === z.parameter) : null;
    const zahl = b && p ? U.befundWert(b, p.datentyp) : null;
    q('.fu-bb-zelle').innerHTML = `<span class="fu-bb-zelle-text">${z ? `Markierte Zelle: <b>${esc(U.zelleText(z))}</b>` : '<span class="leise">Keine Zelle markiert – im Reiter «Typen» (oder «Parameter») in eine Zelle klicken, dann hierher wechseln.</span>'}</span>
      <label class="fe-hk" title="Der Wert des Befunds wird als Wert der Zelle gesetzt; der Kern prüft ihn gegen die Quelle"><input type="checkbox" class="fu-bb-wert-haken" ${ui.wertUebernehmen && zahl !== null ? 'checked' : ''} ${zahl === null ? 'disabled' : ''}> Wert ${zahl !== null ? esc(U.wertText(zahl)) : ''} übernehmen</label>
      <button type="button" class="fe-knopf primaer fu-bb-uebernehmen" data-bb-aktion="uebernehmen" ${z && b ? '' : 'disabled'} title="${esc(!z ? 'Zuerst eine Zelle markieren' : !b ? 'Zuerst einen Befund wählen' : 'Der gewählte Befund wird Quelle dieser Zelle')}">als Quelle übernehmen</button>`;
  }

  async function uebernehmen() {
    const z = ctx.zelle();
    const b = ui.befunde.find((x) => x.id === ui.gewaehlt);
    const haken = root.querySelector('.fu-bb-wert-haken');
    const r = U.quelleUebernehmenOps(ctx.def, z, b, { wertUebernehmen: !!haken?.checked && !haken.disabled });
    if (r.fehler) { msg('fehler', r.fehler); return; }
    const erg = await ctx.senden(r.ops, { still: true });
    if (!erg.angenommen) { const k = kernFehler(erg); msg('fehler', `${k.text}${k.vorschlag ? ' – ' + k.vorschlag : ''}`); return; }
    msg('ok', `Übernommen: ${r.text}.`);
    zelleZeichnen();
  }

  // ---------------------------------------------------------------------------------------- Unterlage hinzufügen
  async function hinzufuegen(file) {
    msg('hinweis', `«${file.name}» wird hochgeladen und gelesen …`);
    const ok = U.dateiPruefen(file.name);
    if (ok !== true) { msg('fehler', ok); return; }
    try {
      const hoch = await dateiHochladen(file);
      const r = await unterlageAblegenUndLesen(hoch);
      if (r.eintrag.fehler) { msg('fehler', r.eintrag.fehler); return; }
      msg('ok', `«${file.name}» zur Familie hinzugefügt.`);
      await new Promise((f) => setTimeout(f, 400));   // das Ereignis mit der neuen Unterlage kommt gleich
      ulZeichnen();
      const neu = kand().find((u) => u.ref === r.kz) ?? kand().find((u) => u.datei === file.name);
      if (neu) await unterlageWaehlen(neu.ref);
    } catch (e) { msg('fehler', `${e.message}${e.vorschlag ? ' – ' + e.vorschlag : ''}`); }
  }

  // ---------------------------------------------------------------------------------------- Ereignisse
  root.addEventListener('click', async (e) => {
    const ul = e.target.closest('.fu-bb-ul-eintrag');
    if (ul) { await unterlageWaehlen(ul.dataset.ref); return; }
    if (e.target.closest('.fu-bb-neu')) { q('.fu-bb-datei').click(); return; }
    const z = e.target.closest('.fu-bb-zeile');
    if (z) { waehlen(z.dataset.id, false); return; }
    const a = e.target.closest('[data-bb-aktion]')?.dataset.bbAktion;
    if (a === 'uebernehmen') await uebernehmen();
    else if (a === 'zoom-ein' || a === 'zoom-aus' || a === 'zoom-eins') { viewer.zoom(a === 'zoom-eins' ? 1 : viewer.zoomFaktor() * (a === 'zoom-ein' ? 1.5 : 1 / 1.5)); q('.fu-bb-zoomtext').textContent = `${Math.round(viewer.zoomFaktor() * 100)} %`; if (ui.gewaehlt) viewer.zuMarke(ui.gewaehlt); }
    else if (a === 'weiter') { ui.trefferSeite++; await befundeLaden(); }
    else if (a === 'zurueck') { ui.trefferSeite = Math.max(1, ui.trefferSeite - 1); await befundeLaden(); }
  });
  root.addEventListener('keydown', (e) => { if ((e.key === 'Enter' || e.key === ' ') && e.target.matches('.fu-bb-zeile')) { e.preventDefault(); waehlen(e.target.dataset.id, false); } });
  root.addEventListener('input', (e) => { if (e.target.dataset?.bb === 'text') { ui.text = e.target.value; befundeNeu(); } });
  root.addEventListener('change', async (e) => {
    const el = e.target;
    if (el.classList.contains('fu-bb-datei')) { const f = el.files?.[0]; el.value = ''; if (f) await hinzufuegen(f); return; }
    if (el.classList.contains('fu-bb-wert-haken')) { ui.wertUebernehmen = el.checked; return; }
    const f = el.dataset?.bb;
    if (f === 'seite') { ui.seite = el.value; ui.trefferSeite = 1; ui.gewaehlt = null; await Promise.all([befundeLaden(), ui.seite ? bildLaden() : Promise.resolve()]); }
    else if (f === 'art') { ui.art = el.value; ui.trefferSeite = 1; await befundeLaden(); }
  });

  // ---------------------------------------------------------------------------------------- Vertrag
  function aktualisieren() {
    if (tot) return;
    const sig = kand().map((u) => u.ref).join('|');
    if (sig !== kandSig) {
      kandSig = sig;
      ulZeichnen(); urlZeichnen();
      if (!ui.ref || !kand().some((u) => u.ref === ui.ref)) { const erste = kand()[0]; if (erste) unterlageWaehlen(erste.ref); else unterlageWaehlen(''); }
    }
    zelleZeichnen();
  }

  ulZeichnen(); filterZeichnen(); listeZeichnen(); zelleZeichnen();
  aktualisieren();

  return { aktualisieren, zustand: ui, zerstoeren() { tot = true; nr++; viewer.zerstoeren(); host.replaceChildren(); } };
}

export default { id: 'befunde', titel: 'Unterlagen', eigen: true, erstellen };
