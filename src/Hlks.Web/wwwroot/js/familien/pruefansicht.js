// Prüfansicht eines Entwurfs (Modul 2, C2), Register «Entwurf prüfen» im Familieneditor: Bericht der KI, Freigabe (nur ohne offene Konflikte, Fehler und Masse
// ausserhalb der Toleranz; sonst die Gründe mit Sprung), Konfliktliste mit Wahl je Zeile, Abdeckung (Parameter × Typ, farbig nach Status, Klick zeigt die
// Quellen), «Quelle ansehen», «Wert bestätigen», Massvergleich je Typ und Kontrollbilder (Unterlage, Seite, Ansicht, Typ, Passpunkte anklicken oder als Zahlen).
// Alle Entscheide gehen als Befehle an den Kern (konflikt_entscheiden, wert_bestaetigen, familie_freigeben, familie_speichern) – nur ein Mensch darf sie
// auslösen, der Kern lehnt die KI ab. Die Oberfläche rechnet nichts: Status, Konflikte, Masse und Kontrollbild kommen vom Kern.
//
// Modul des Editors (editor.js): { id, titel, eigen: true, erstellen(host, ctx) → { aktualisieren(), zerstoeren() } }. ctx: kz, fam, neuLaden(),
// springen(ziel), kandidaten(), ansicht (Familienansicht: picken, markieren, einstellen), tabWaehlen(id).

import { abfrage } from '../api.js';
import { sende } from '../oberflaeche/befehle.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { kernFehler } from './familien-text.js';
import * as U from './unterlagen-text.js';
import * as H from './pruefansicht-html.js';
import { quelleAnsehen, befundeLaden, quellenHtml } from './quelle-ansehen.js';
import { bildBetrachter } from './unterlagen-bild.js';

const esc = U.esc;

function erstellen(host, ctx) {
  const kz = ctx.kz;
  let tot = false, masse = null, masseFehler = '', sigAlt = '', ladeNr = 0, gezeichnet = false;
  const ui = {
    zelle: null, wahl: {}, bestBegr: '', datei: '', ppOffen: false, pick: -1, ergebnis: null, ergebnisVersion: null,
    vg: { unterlage: '', seite: '1', ansicht: 'vorne', typ: '', detailgrad: 'mittel', felder: {} },
    punkte: { familie: {}, unterlage: {} },
  };

  host.innerHTML = `<div class="fu-pa" data-fu="pruefansicht">
    <div class="fu-msg" hidden role="alert"></div>
    <section class="fu-sek fu-sek-kopf"></section>
    <section class="fu-sek fu-sek-bericht"></section>
    <section class="fu-sek fu-sek-konflikte" data-abschnitt="konflikte"></section>
    <section class="fu-sek fu-sek-abd" data-abschnitt="abdeckung"><div class="fu-abd-tabelle"></div><div class="fu-abd-detailfeld"></div></section>
    <section class="fu-sek fu-sek-masse" data-abschnitt="masse"></section>
    <section class="fu-sek fu-sek-vergleich" data-abschnitt="vergleich"><h3>Kontrollbilder</h3>
      <p class="leise">Die Unterlage mit der Familie in Rot darüber. Bei PDF-Massbildern ist die Überlagerung nur Anschauung («schematisch») – massgebend ist die Abweichungsliste.</p>
      <div class="fu-vg-form"></div><div class="fu-vg-viewer"></div><div class="fu-vg-ergebnis"></div></section>
  </div>`;
  const root = host.querySelector('.fu-pa');
  const q = (s) => root.querySelector(s);
  const msgFeld = q('.fu-msg');
  const viewer = bildBetrachter(q('.fu-vg-viewer'), { klasse: 'fu-vg-unterlage' });
  q('.fu-vg-viewer').hidden = true;

  const fam = () => ctx.fam;
  const def = () => fam()?.definition ?? {};
  const kandidaten = () => ctx.kandidaten();
  const gruende = () => U.freigabeGruende(fam(), masse);

  // ---------------------------------------------------------------------------------------- Meldungen
  // Fehler und Hinweise stehen als Streifen oben (mit ✕), Erfolgsmeldungen als Einblendung unten rechts: der Streifen verdeckt sonst die Felder, an denen man arbeitet
  function msg(art, text, vorschlag = '') {
    if (art === 'ok' && text) { meldung(text, 'ok'); msgFeld.hidden = true; return; }
    msgFeld.hidden = !text;
    msgFeld.className = `fu-msg ${art}`;
    msgFeld.innerHTML = text ? `<span class="fu-msg-text">${esc(text)}${vorschlag ? ` <span class="leise">Vorschlag: ${esc(vorschlag)}</span>` : ''}</span><button type="button" class="fe-knopf klein fu-msg-zu" data-pa="msg-zu" title="Schliessen">✕</button>` : '';
  }
  const ergebnisMsg = (r, okText) => {
    if (!r.angenommen) { const k = kernFehler(r); msg('fehler', r.fehler ?? k.text, r.vorschlag ?? ''); return false; }
    msg('ok', okText || (r.hinweise ?? []).filter(Boolean).slice(0, 3).join(' ') || r.meldung || 'Erledigt.');
    return true;
  };

  // ---------------------------------------------------------------------------------------- Zeichnen
  function kopfZeichnen() { q('.fu-sek-kopf').innerHTML = H.kopfHtml(fam(), gruende(), ui); }
  function abdZeichnen() {
    q('.fu-abd-tabelle').innerHTML = H.abdeckungHtml(def(), ui);
    q('.fu-abd-detailfeld').innerHTML = ui.zelle ? H.abdeckungDetailHtml(def(), ui.zelle, ui) : '';
    quellenFuellen();
  }
  function alleZeichnen() {
    if (!fam()) { root.innerHTML = '<p class="fe-leer leise">Familie wird geladen …</p>'; return; }
    kopfZeichnen();
    const kand = kandidaten();
    q('.fu-sek-bericht').innerHTML = H.berichtHtml(fam(), kand, U.internetUnterlagen(ctx.elemente()).filter((i) => kand.some((k) => k.ref === i.kz && k.quelle !== 'frei')));
    q('.fu-sek-konflikte').innerHTML = H.konflikteHtml(def(), ui);
    abdZeichnen();
    q('.fu-sek-masse').innerHTML = H.masseHtml(masse, masseFehler);
    gezeichnet = true;
  }

  /** Text zu den Quellen des gewählten Werts (Datei, Seite, Stelle) nachladen. */
  async function quellenFuellen() {
    const li = [...root.querySelectorAll('.fu-quellen li[data-id]')];
    if (!li.length) return;
    const ids = li.map((x) => x.dataset.id);
    const erg = await befundeLaden(kandidaten(), ids);
    if (tot) return;
    for (const e of erg) {
      const el = root.querySelector(`.fu-quellen li${H.attr('data-id', e.id)} .fu-quelle-text`);
      if (el) { el.innerHTML = quellenHtml(e); el.classList.toggle('fehlertext', !!e.fehler); }
    }
  }

  function vergleichZeichnen() {
    const kand = H.vergleichbareUnterlagen(kandidaten());
    const typen = (fam()?.typen ?? []).map((t) => t.name);
    if (!kand.some((u) => u.ref === ui.vg.unterlage)) ui.vg.unterlage = (kand.find((u) => u.art === 'dxf' || u.art === 'dwg') ?? kand[0])?.ref ?? '';
    if (!typen.includes(ui.vg.typ)) ui.vg.typ = typen[0] ?? '';
    q('.fu-vg-form').innerHTML = H.vergleichFormHtml(fam(), kand, ui.vg, ui.ppOffen);
    ergebnisZeichnen();
    pickHinweis();
  }
  function ergebnisZeichnen() {
    q('.fu-vg-ergebnis').innerHTML = H.vergleichErgebnisHtml(ui.ergebnis, ui.ergebnis && ui.ergebnisVersion !== fam()?.version);
  }

  // ---------------------------------------------------------------------------------------- Laden
  async function aktualisieren() {
    if (tot) return;
    const f = fam();
    if (!f) { alleZeichnen(); return; }
    const sig = [f.version, f.status, f.gueltig, JSON.stringify(f.definition?.abdeckung ?? {}), JSON.stringify(f.definition?.konflikte ?? []), JSON.stringify(f.freigabe ?? null),
      JSON.stringify(f.bibliothek ?? null), (f.typen ?? []).map((t) => t.name).join('|'), JSON.stringify(f.definitionsfehler ?? []), (f.definition?.unterlagen ?? []).length, kandidaten().map((k) => k.ref + k.url).join(',')].join('#');
    if (sig === sigAlt && gezeichnet) return;
    sigAlt = sig;
    const nr = ++ladeNr;
    if (f.gueltig === false) { masse = null; masseFehler = 'Die Definition hat Fehler (Reiter «Prüfen»): ohne gültige Definition gibt es keinen Massvergleich.'; }
    else {
      try { masse = await abfrage('familie_masse', { familie: kz }); masseFehler = ''; }
      catch (e) { masse = null; masseFehler = `${e.message}${e.vorschlag ? ' – ' + e.vorschlag : ''}`; }
    }
    if (nr !== ladeNr || tot) return;
    alleZeichnen();
    vergleichZeichnen();
  }

  // ---------------------------------------------------------------------------------------- Aktionen
  async function nachBefehl() { await ctx.neuLaden(true); }

  async function freigeben() {
    const r = await sende('familie_freigeben', { familie: kz });
    if (ergebnisMsg(r)) await nachBefehl();
  }
  async function speichern() {
    const p = { familie: kz };
    if (ui.datei.trim()) p.datei = ui.datei.trim();
    const r = await sende('familie_speichern', p);
    if (ergebnisMsg(r)) await nachBefehl();
  }
  async function entscheiden(id) {
    const k = (def().konflikte ?? []).find((x) => x.id === id);
    const w = ui.wahl[id] ?? {};
    const radio = root.querySelector(`input[type=radio]${H.attr('data-k', id)}:checked`);
    let wahl = {};
    if (radio?.value === 'anders') wahl = { wert: root.querySelector(`.fu-k-anders${H.attr('data-k', id)}`)?.value };
    else if (radio) wahl = { index: radio.value };
    const e = U.konfliktEntscheid(kz, k, wahl, w.begr ?? root.querySelector(`.fu-k-begr${H.attr('data-k', id)}`)?.value);
    if (e.fehler) { msg('fehler', e.fehler); return; }
    const r = await sende('konflikt_entscheiden', e.parameter);
    if (ergebnisMsg(r, `Konflikt entschieden: ${U.konfliktTitel(k)} = ${U.wertText(e.parameter.wert)}${k.einheit ? ' ' + k.einheit : ''}. Die Familie bleibt Entwurf, bis sie freigegeben wird.`)) { delete ui.wahl[id]; await nachBefehl(); }
  }
  async function bestaetigen(schluessel) {
    const t = U.abdeckungTabelle(def());
    const z = [...t.zeilen.flatMap((r) => [r.vorgabe, ...r.typen]), ...t.uebrig].find((x) => x?.schluessel === schluessel);
    const e = U.bestaetigenParameter(kz, z, ui.bestBegr);
    if (e.fehler) { msg('fehler', e.fehler); return; }
    const r = await sende('wert_bestaetigen', e.parameter);
    if (ergebnisMsg(r, `Wert bestätigt: «${z.parameter}»${z.typ ? ' · Typ ' + z.typ : ''} = ${U.wertText(z.wert)}.`)) { ui.bestBegr = ''; await nachBefehl(); }
  }

  function kurzSpringen(el) {
    if (!el) return;
    el.scrollIntoView({ block: 'center', behavior: 'auto' });
    el.classList.add('springziel');
    setTimeout(() => el.classList.remove('springziel'), 1800);
  }
  function springen(z) {
    if (!z) return;
    if (z.abschnitt === 'fehler') { if (z.ziel) ctx.springen(z.ziel); else root.scrollTo?.({ top: 0 }); return; }
    if (z.abschnitt === 'konflikt') { kurzSpringen(root.querySelector(`.fu-konflikt${H.attr('data-k', z.id)}`)); return; }
    if (z.abschnitt === 'abdeckung') {
      ui.zelle = z.schluessel; abdZeichnen();
      kurzSpringen(root.querySelector(`td.fu-st${H.attr('data-s', z.schluessel)}, span.fu-st${H.attr('data-s', z.schluessel)}`)); return;
    }
    if (z.abschnitt === 'masse') kurzSpringen(root.querySelector(`.fu-sek-masse tr${H.attr('data-mk', z.schluessel)}`));
  }

  // ---------------------------------------------------------------------------------------- Kontrollbild
  const feldSetzen = (name, wert) => {
    ui.vg.felder[name] = wert;
    const el = root.querySelector(`[data-vg-pp="${name}"]`);
    if (el) el.value = wert;
  };
  function pickHinweis() {
    const h = root.querySelector('.fu-pick-hinweis');
    if (!h) return;
    h.textContent = ui.pick >= 0 ? `Jetzt: ${U.pickText(ui.pick)} (${(ui.pick % 4) + 1} von 4)` : '';
    h.classList.toggle('aktiv', ui.pick >= 0);
    const ende = root.querySelector('[data-pa="pick-ende"]');
    if (ende) ende.hidden = ui.pick < 0;
  }
  function punkteMarkieren() {
    const fa = Object.entries(ui.punkte.familie).map(([p, v]) => ({ u: v.u, v: v.v, label: p === 'a' ? 'P1' : 'P2' }));
    ctx.ansicht.markieren(ui.vg.ansicht, fa);
    viewer.markieren(Object.entries(ui.punkte.unterlage).map(([p, v]) => ({ box: [v.x - 1, v.y - 1, v.x + 1, v.y + 1], klasse: 'pass', titel: p === 'a' ? 'Punkt 1' : 'Punkt 2' })));
  }
  async function unterlageZeigen() {
    const u = kandidaten().find((x) => x.ref === ui.vg.unterlage);
    if (!u) { msg('fehler', 'Keine Unterlage gewählt.'); return; }
    q('.fu-vg-viewer').hidden = false;
    const p = { unterlage: u.ref, breite: 1000 };
    if (u.art === 'pdf' && Number(ui.vg.seite) >= 1) p.seite = Number(ui.vg.seite);
    try { await viewer.laden(p); viewer.beiKlick(unterlageKlick); punkteMarkieren(); } catch (e) { msg('fehler', e.message, e.vorschlag ?? ''); }
  }
  function unterlageKlick({ punkt }) {
    if (ui.pick < 0) return;
    const [p, wo] = U.PICK_FOLGE[ui.pick % 4];
    if (wo !== 'unterlage') { msg('hinweis', `Zuerst: ${U.pickText(ui.pick)}.`); return; }
    const f = U.pickUnterlage(p, punkt[0], punkt[1]);
    for (const [n, w] of Object.entries(f)) feldSetzen(n, w);
    ui.punkte.unterlage[p] = { x: punkt[0], y: punkt[1] };
    ui.pick++; msg('', ''); pickHinweis(); punkteMarkieren();
  }
  function familieKlick(p2) {
    if (ui.pick < 0) return;
    const [p, wo] = U.PICK_FOLGE[ui.pick % 4];
    if (wo !== 'familie') { msg('hinweis', `Zuerst: ${U.pickText(ui.pick)}.`); return; }
    const f = U.pickFamilie(p, p2.u, p2.v);
    for (const [n, w] of Object.entries(f)) feldSetzen(n, w);
    ui.punkte.familie[p] = { u: Number(f[`${p}_u`]), v: Number(f[`${p}_v`]) };
    ui.pick++; msg('', ''); pickHinweis(); punkteMarkieren();
  }
  async function pickStarten() {
    ui.ppOffen = true; ui.pick = 0; ui.punkte = { familie: {}, unterlage: {} };
    for (const n of ['a_u', 'a_v', 'a_x', 'a_y', 'b_u', 'b_v', 'b_x', 'b_y']) feldSetzen(n, '');
    const details = root.querySelector('.fu-passpunkte'); if (details) details.open = true;
    await ctx.ansicht.fensterFuer(ui.vg.ansicht);          // die Familienansicht der gewählten Ansicht muss sichtbar sein
    ctx.ansicht.picken(ui.vg.ansicht, familieKlick);
    await unterlageZeigen();
    pickHinweis();
    punkteMarkieren();
  }
  function pickEnde() {
    ui.pick = -1; ctx.ansicht.picken(null, null); viewer.beiKlick(null); pickHinweis();
  }

  async function vergleichen() {
    const status = root.querySelector('.fu-vg-status');
    const u = kandidaten().find((x) => x.ref === ui.vg.unterlage);
    const r = U.vergleichParameter({ familie: kz, typ: ui.vg.typ, unterlage: ui.vg.unterlage, ansicht: ui.vg.ansicht, art: u?.art, seite: ui.vg.seite, detailgrad: ui.vg.detailgrad, felder: ui.vg.felder });
    if (r.fehler) { status.textContent = r.fehler; status.classList.add('fehlertext'); return; }
    status.classList.remove('fehlertext');
    status.textContent = 'Kontrollbild wird berechnet …';
    q('.fu-vg-ergebnis').innerHTML = '';
    try {
      ui.ergebnis = await abfrage('familie_vergleichen', r.parameter);
      ui.ergebnisVersion = fam()?.version;
      status.textContent = '';
    } catch (e) {
      ui.ergebnis = null;
      status.textContent = '';
      q('.fu-vg-ergebnis').innerHTML = `<p class="fehlertext fu-vg-fehler">${esc(e.message)}</p>${e.vorschlag ? `<p class="leise">${esc(e.vorschlag)}</p>` : ''}`;
      return;
    }
    ergebnisZeichnen();
    q('.fu-vg-ergebnis').scrollIntoView({ block: 'nearest' });
  }

  // ---------------------------------------------------------------------------------------- Ereignisse
  root.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-pa]');
    if (!b || !root.contains(b) || b.disabled) return;
    const a = b.dataset.pa;
    try {
      if (a === 'msg-zu') msg('', '');
      else if (a === 'freigeben') await freigeben();
      else if (a === 'speichern') await speichern();
      else if (a === 'grund') springen(gruende()[Number(b.dataset.i)]?.ziel);
      else if (a === 'entscheiden') await entscheiden(b.dataset.k);
      else if (a === 'quelle') await quelleAnsehen(kandidaten(), b.dataset.id);
      else if (a === 'zelle') { ui.zelle = b.dataset.s; ui.bestBegr = ''; abdZeichnen(); }
      else if (a === 'bestaetigen') await bestaetigen(b.dataset.s);
      else if (a === 'zum-konflikt') {
        const k = (def().konflikte ?? []).find((x) => x.id === b.dataset.id);
        springen({ abschnitt: 'konflikt', id: k?.id ?? b.dataset.id });
      }
      else if (a === 'pick-start') await pickStarten();
      else if (a === 'pick-ende') pickEnde();
      else if (a === 'pp-leeren') { for (const n of ['a_u', 'a_v', 'a_x', 'a_y', 'b_u', 'b_v', 'b_x', 'b_y']) feldSetzen(n, ''); ui.punkte = { familie: {}, unterlage: {} }; ui.pick = ui.pick >= 0 ? 0 : -1; pickHinweis(); punkteMarkieren(); }
      else if (a === 'unterlage-zeigen') await unterlageZeigen();
      else if (a === 'vergleichen') await vergleichen();
    } catch (err) { console.error(err); msg('fehler', String(err.message ?? err)); }
  });
  root.addEventListener('keydown', (e) => {
    if ((e.key === 'Enter' || e.key === ' ') && e.target.matches('[data-pa="zelle"], [data-pa="grund"]')) { e.preventDefault(); e.target.click(); }
  });
  root.addEventListener('input', (e) => {
    const el = e.target;
    const f = el.dataset?.paFeld;
    if (f === 'datei') ui.datei = el.value;
    else if (f === 'best-begr') ui.bestBegr = el.value;
    else if (f === 'k-begr') (ui.wahl[el.dataset.k] ??= {}).begr = el.value;
    else if (f === 'k-anders') { const w = (ui.wahl[el.dataset.k] ??= {}); w.anders = el.value; w.index = 'anders'; const r = root.querySelector(`input[type=radio]${H.attr('data-k', el.dataset.k)}[value="anders"]`); if (r) r.checked = true; }
    else if (el.dataset?.vgPp) ui.vg.felder[el.dataset.vgPp] = el.value;
    else if (el.dataset?.vg === 'seite') ui.vg.seite = el.value;
  });
  root.addEventListener('change', async (e) => {
    const el = e.target;
    if (el.dataset?.paFeld === 'k-wahl') { (ui.wahl[el.dataset.k] ??= {}).index = el.value; return; }
    const v = el.dataset?.vg;
    if (!v) return;
    ui.vg[v] = el.value;
    if (v === 'unterlage') { vergleichZeichnen(); if (ui.pick >= 0) await unterlageZeigen(); }
    if (v === 'ansicht' && ui.pick >= 0) { ctx.ansicht.picken(null, null); await ctx.ansicht.fensterFuer(ui.vg.ansicht); ctx.ansicht.picken(ui.vg.ansicht, familieKlick); punkteMarkieren(); }
  });
  root.addEventListener('toggle', (e) => { if (e.target.matches?.('.fu-passpunkte')) ui.ppOffen = e.target.open; }, true);

  alleZeichnen();
  aktualisieren();

  return {
    aktualisieren,
    /** Für Tests und Sprünge von aussen. */
    zustand: ui,
    springen,
    zerstoeren() { tot = true; ladeNr++; pickEnde(); ctx.ansicht.markieren(null, []); viewer.zerstoeren(); host.replaceChildren(); },
  };
}

export default { id: 'entwurf', titel: 'Entwurf prüfen', eigen: true, erstellen };
