// Aktionen des Registers «Verwalten» › Gruppe «HKAP» (Modul 1d, Auftrag C; ab Modul 3 B5 auch Stränge: Zuordnen von Strängen, Systemwahl) und ihre Dialoge: «Mit HKAP verknüpfen» (Verbindung prüfen,
// Projekt und Projektierung wählen, verknüpfen), «Abgleich mit HKAP» (Tabelle je Datenart, Konflikte mit Wahl, Aus HKAP übernehmen,
// Nach HKAP senden mit Trockenlauf), «Prüfbefunde senden» (Schwere, Vorschau, Senden) und die Aktionen für nicht platzierte Räume
// (Platzieren …, Einem gezeichneten Raum zuordnen …; vom Projektbrowser aus).
// Keine Fachlogik: alles geht über die Abfragen hkap_verbindung und hkap_abgleich und die Befehle hkap_verknuepfen, hkap_uebernehmen,
// hkap_senden, hkap_befunde_senden, hkap_raum_zuordnen des Kerns. Der Kern ruft HKAP auf (Server zu Server, HKAP hat kein CORS); die Oberfläche
// spricht nur mit KICAD. Die Texte und das HTML stehen in hkap-text.js (reine Funktionen, in Node getestet).

import { zustand, element, derKlasse, alleElemente, abonnieren } from '../zustand.js';
import { abfrage } from '../api.js';
import { registrieren, hinweisSetzen } from './register.js';
import { zeigen, meldung, bestaetigen } from './dialoge.js';
import { sende } from './befehle.js';
import { knoten } from './werkzeug.js';
import { aenderungenTabelle } from './einfuegen-text.js';
import * as T from './hkap-text.js';
import { planAnsicht, werkzeugStarten, beiWerkzeugEnde, geschossVon } from '../werkzeuge/basis.js';
import { raumWerkzeug } from '../werkzeuge/raum.js';
import { setze } from '../werkzeuge/optionen.js';
import { ansichtOeffnen } from '../fenster.js';

const esc = T.esc;
const projektOffen = () => !!zustand.status?.offen;
const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';
const NICHT_VERKNUEPFT = 'Zuerst das Projekt mit HKAP verknüpfen («Verbinden»).';

/** Die bestehende Verknüpfung (Projektinformation.hkap) oder null. */
const verknuepfung = () => T.verknuepfung(T.projektinfo(derKlasse('Projektinformation')));
const verknuepft = () => projektOffen() && !!verknuepfung();

const spinner = (text) => `<div class="lauf"><span class="spinner" aria-hidden="true"></span><span class="lauf-text">${esc(text)}</span></div>`;
/** Fehler einer Abfrage (ApiFehler oder Ausnahme): HKAP-Ausfall als Hinweiskasten ohne Fehlerrot, sonst als Fehlerkasten. */
function fehlerHtml(e) {
  const text = e?.message ?? String(e);
  return T.ergebnisHtml({ angenommen: false, fehler: text, vorschlag: e?.vorschlag ?? null });
}
const fehlerText = (r) => [r.fehler || r.meldung || 'Abgelehnt.', r.vorschlag ? `Vorschlag: ${r.vorschlag}` : ''].filter(Boolean).join(' – ');

// ================================================================================================ Mit HKAP verknüpfen

async function verbinden() {
  const bestehend = verknuepfung();
  const w = knoten(`<div class="hkap-dlg">
    <p class="dlg-text">Verknüpft dieses Projekt mit einem HKAP-Projekt und einer Projektierung. KICAD ruft HKAP selbst auf (Server zu Server); HKAP muss dafür laufen.</p>
    ${bestehend ? `<h3>Bestehende Verknüpfung</h3><table class="wtab">${T.verknuepfungZeilen(bestehend).map(([k, v]) => `<tr><th>${esc(k)}</th><td>${esc(v)}</td></tr>`).join('')}</table><div class="h-gegenpruefung"></div>` : ''}
    <h3>Verbindung</h3>
    <div class="hkap-adresse"><span class="leise">HKAP-Adresse</span><input type="text" class="h-url" value="${esc(T.urlVorgabe(bestehend))}" spellcheck="false" data-kein-enter="1" aria-label="HKAP-Adresse"><button type="button" class="dlg-knopf h-pruefen">Verbindung prüfen</button></div>
    <div class="h-status"></div>
    <div class="hkap-listen h-listen" hidden>
      <div><h4>Projekt in HKAP</h4><input type="search" class="h-suche" placeholder="Projekt suchen …" data-kein-enter="1" aria-label="HKAP-Projekt suchen"><div class="h-projekte"></div></div>
      <div><h4>Projektierung</h4><div class="h-pj"></div></div>
    </div>
    <div class="h-pruefung"></div></div>`);
  const q = (s) => w.querySelector(s);
  const url = q('.h-url'), status = q('.h-status'), listen = q('.h-listen'), pruefung = q('.h-pruefung');
  let v = null, projekt = bestehend?.projekt ?? null, pj = bestehend?.projektierung ?? null, pjListe = null, nrV = 0, nrP = 0, gegenpruefungGeholt = false;

  const listenZeichnen = () => {
    q('.h-projekte').innerHTML = T.projekteHtml(v?.projekte, projekt, bestehend, q('.h-suche').value);
    q('.h-pj').innerHTML = T.projektierungenHtml(pjListe, pj, bestehend, projekt);
  };

  async function gegenpruefung() {
    if (!bestehend || gegenpruefungGeholt) return;
    gegenpruefungGeholt = true;
    try {
      const a = await abfrage('hkap_abgleich', { datenarten: ['projekt'] });
      q('.h-gegenpruefung').innerHTML = T.warnungenHtml(a?.warnungen);
    } catch (e) { console.error(e); }
  }

  async function verbindungPruefen(projektWahl = projekt) {
    const mein = ++nrV;
    status.innerHTML = spinner('Verbindung zu HKAP wird geprüft …');
    let r;
    try { r = await abfrage('hkap_verbindung', T.verbindungParameter(url.value, projektWahl)); }
    catch (e) { if (mein === nrV) { status.innerHTML = fehlerHtml(e); listen.hidden = true; } return; }
    if (mein !== nrV) return;
    v = r;
    status.innerHTML = T.verbindungHtml(r) + (r.projekteHinweis ? `<p class="leise">${esc(r.projekteHinweis)}</p>` : '');
    const ok = T.verbindungStatus(r).ok;
    listen.hidden = !ok;
    if (!ok) return;
    if (projektWahl && Array.isArray(r.projektierungen)) {
      pjListe = r.projektierungen;
      if (!pjListe.some((p) => p.id === pj)) pj = pjListe.length === 1 ? pjListe[0].id : null;
    }
    listenZeichnen();
    if (projekt) vorschau();
    gegenpruefung();
  }

  async function vorschau() {
    if (!projekt) { pruefung.innerHTML = ''; return; }
    const mein = ++nrP;
    pruefung.innerHTML = spinner('Verknüpfung wird geprüft …');
    const r = await sende('hkap_verknuepfen', T.verknuepfenParameter({ projekt, projektierung: pj, url: url.value, link: bestehend }), { trockenlauf: true });
    if (mein !== nrP) return;
    const warn = [...T.wechselWarnungen(bestehend, projekt, pj), T.namensAbweichung(bestehend, T.angabenAusHinweis((r.hinweise ?? []).join(' ')))].filter(Boolean);
    if (!pj && pjListe?.length) warn.push('Keine Projektierung gewählt: Geschosse und Räume lassen sich erst mit einer Projektierung abgleichen.');
    pruefung.innerHTML = T.warnungenHtml(warn) + (r.angenommen
      ? `<p class="hkap-summe"><b>${esc(T.hinweisFuerMenschen((r.hinweise ?? [])[0] ?? r.meldung ?? ''))}</b></p>${(r.hinweise ?? []).slice(1).length ? `<ul class="hinweisliste">${r.hinweise.slice(1).map((h) => `<li>${esc(T.hinweisFuerMenschen(h))}</li>`).join('')}</ul>` : ''}`
      : T.ergebnisHtml(r));
  }

  w.addEventListener('click', (e) => {
    if (e.target.closest('.h-pruefen')) { verbindungPruefen(); return; }
    const zp = e.target.closest('tr[data-projekt]');
    if (zp) {
      if (zp.dataset.projekt === projekt) return;
      projekt = zp.dataset.projekt; pj = null; pjListe = null;
      listenZeichnen();
      verbindungPruefen(projekt);
      return;
    }
    const zj = e.target.closest('tr[data-projektierung]');
    if (zj) { pj = zj.dataset.projektierung; listenZeichnen(); vorschau(); }
  });
  w.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter') return;
    if (e.target === url) { e.preventDefault(); verbindungPruefen(); }
    else if (e.target.closest?.('tr[data-projekt], tr[data-projektierung]')) { e.preventDefault(); e.target.click(); }
  });
  q('.h-suche').addEventListener('input', () => { q('.h-projekte').innerHTML = T.projekteHtml(v?.projekte, projekt, bestehend, q('.h-suche').value); });
  url.addEventListener('change', () => { v = null; projekt = bestehend?.projekt ?? null; pjListe = null; verbindungPruefen(); });

  await zeigen({
    titel: 'Mit HKAP verknüpfen', inhalt: w, breite: 880, klasse: 'hkap kurz',
    beiOeffnen: () => { verbindungPruefen(); },
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Verknüpfen', primaer: true, schliesst: false,
        pruefen: () => T.verknuepfenPruefen({ projekt, url: url.value }),
        async klick(ctrl) {
          const r = await sende('hkap_verknuepfen', T.verknuepfenParameter({ projekt, projektierung: pj, url: url.value, link: bestehend }));
          if (!r.angenommen) { ctrl.fehler(fehlerText(r)); return; }
          meldung(T.hinweisFuerMenschen((r.hinweise ?? [])[0] ?? r.meldung ?? 'Verknüpft.'), 'ok');
          ctrl.schliessen(true);
        } }],
  });
  nrV++; nrP++;
}

// ================================================================================================ Abgleich mit HKAP

async function abgleichDialog() {
  let daten = null, wahl = {}, uebrige = null, systemWahl = {}, sendenBereit = false, nr = 0, jetzt = null;
  const arten = new Set(T.UEBERNEHMBAR);
  const w = knoten(`<div class="hkap-dlg"><div class="h-kopf"></div><div class="h-ergebnis" hidden></div><div class="h-inhalt"></div></div>`);
  const q = (s) => w.querySelector(s), qa = (s) => [...w.querySelectorAll(s)];
  const kopf = q('.h-kopf'), ergebnis = q('.h-ergebnis'), inhalt = q('.h-inhalt');

  const bereit = () => T.abgleichStatus(daten).art === 'ok';
  const nichtBereit = () => 'Der Abgleich ist noch nicht geladen oder HKAP ist nicht erreichbar. «Neu laden» versucht es erneut.';

  function zeichnen() {
    const s = T.abgleichStatus(daten);
    if (s.art !== 'ok') {
      kopf.innerHTML = T.abgleichKastenHtml(daten);
      inhalt.innerHTML = '';
      return;
    }
    const link = verknuepfung();
    const offene = T.offeneKonflikte(daten, wahl, uebrige);
    kopf.innerHTML = `
      <table class="wtab hkap-kopftab"><tr><th>Verknüpft mit</th><td>${esc(T.verknuepfungText(link))}</td></tr>
        <tr><th>HKAP</th><td>${esc(daten.url ?? '')}${daten.fassung ? ` · Austauschfassung ${esc(daten.fassung)}` : ''}</td></tr>
        <tr><th>Letzter Abgleich</th><td class="leise">${esc(T.basisText(daten.basis))}</td></tr></table>
      ${T.warnungenHtml(daten.warnungen)}
      <p class="hkap-zusammen"><b class="h-zusammen">${esc(T.zusammenfassungText(daten))}</b></p>
      <div class="hkap-steuer">
        <span class="leise">Übernehmen und Senden für:</span>
        ${T.UEBERNEHMBAR.map((a) => `<label class="haken"><input type="checkbox" class="h-art" value="${a}"${arten.has(a) ? ' checked' : ''}> ${esc(T.DATENART_NAMEN[a])}</label>`).join('')}
        <span class="hkap-trenner"></span>
        <span class="leise h-konfliktzahl">${esc(konfliktSatz(offene))}</span>
        <button type="button" class="chip h-uebrige${uebrige === 'kicad' ? ' gewaehlt' : ''}" data-wert="kicad">alle übrigen: KICAD</button>
        <button type="button" class="chip h-uebrige${uebrige === 'hkap' ? ' gewaehlt' : ''}" data-wert="hkap">alle übrigen: HKAP</button>
      </div>
      <p class="leise hkap-legende">← Aus HKAP nach KICAD · → Von KICAD nach HKAP. Bei einem Konflikt haben beide Seiten seit dem letzten Abgleich geändert: «KICAD» behält den KICAD-Wert (er geht mit «Nach HKAP senden» an HKAP), «HKAP» übernimmt den HKAP-Wert; ohne Wahl bleibt der Konflikt offen und nichts wird übernommen. Gelöscht wird nie automatisch.</p>`;
    inhalt.innerHTML = T.abgleichHtml(daten, wahl, uebrige, systemWahl);
  }

  const konfliktSatz = (offene) => {
    const n = T.konfliktSchluessel(daten).length;
    return n === 0 ? 'Keine Konflikte' : `${n} ${n === 1 ? 'Konflikt' : 'Konflikte'}, ${offene} ohne Wahl`;
  };

  async function laden() {
    const mein = ++nr;
    inhalt.innerHTML = spinner('Abgleich mit HKAP wird gerechnet …');
    let a;
    try { a = await abfrage('hkap_abgleich', {}); }
    catch (e) { if (mein === nr) { kopf.innerHTML = ''; inhalt.innerHTML = fehlerHtml(e); daten = null; } return; }
    if (mein !== nr) return;
    daten = a;
    wahl = T.wahlBereinigen(wahl, a);
    systemWahl = T.systemWahlBereinigen(systemWahl, a);
    sendenBereit = false; if (jetzt) jetzt.hidden = true;
    zeichnen();
  }

  function ergebnisZeigen(html) {
    ergebnis.hidden = false;
    ergebnis.innerHTML = html;
    ergebnis.scrollIntoView({ block: 'nearest' });
  }

  // ---- Bedienung in der Tabelle
  w.addEventListener('change', (e) => {
    const sel = e.target.closest('.k-wahl');
    if (sel) {
      if (sel.value) wahl[sel.dataset.schluessel] = sel.value; else delete wahl[sel.dataset.schluessel];
      sel.closest('tr')?.classList.toggle('entschieden', !!sel.value);
      const z = q('.h-konfliktzahl'); if (z) z.textContent = konfliktSatz(T.offeneKonflikte(daten, wahl, uebrige));
      return;
    }
    const sw = e.target.closest('.s-wahl');
    if (sw) {   // Systemwahl für einen neuen HKAP-Strang (Modul 3): angekreuzte Systeme je HKAP-Strang
      const id = sw.dataset.hkap;
      const gewaehlt = qa('.s-wahl:checked').filter((c) => c.dataset.hkap === id).map((c) => c.value);
      if (gewaehlt.length > 0) systemWahl[id] = gewaehlt; else delete systemWahl[id];
      return;
    }
    if (e.target.closest('.h-art')) {
      arten.clear(); qa('.h-art:checked').forEach((c) => arten.add(c.value));
      sendenBereit = false; if (jetzt) jetzt.hidden = true;
    }
  });
  w.addEventListener('click', async (e) => {
    const chip = e.target.closest('.h-uebrige');
    if (chip) {
      uebrige = uebrige === chip.dataset.wert ? null : chip.dataset.wert;
      qa('.h-uebrige').forEach((c) => c.classList.toggle('gewaehlt', c.dataset.wert === uebrige));
      qa('.k-wahl option[value=""]').forEach((o) => { o.textContent = uebrige ? `übrige: ${T.WAHL_TEXT[uebrige]}` : 'offen lassen'; });
      const z = q('.h-konfliktzahl'); if (z) z.textContent = konfliktSatz(T.offeneKonflikte(daten, wahl, uebrige));
      return;
    }
    const zu = e.target.closest('.h-zuordnen');
    if (zu) {
      zu.disabled = true;
      const r = zu.dataset.strang !== undefined   // Stränge (Modul 3): ein HKAP-Strang bündelt mehrere KICAD-Stränge
        ? await sende('hkap_strang_zuordnen', T.strangZuordnenParameter(zu.dataset.strang, zu.dataset.hkap))
        : await sende('hkap_raum_zuordnen', T.zuordnenParameter(zu.dataset.raum, zu.dataset.hkap));
      ergebnisZeigen(T.ergebnisHtml(r));
      await laden();
    }
  });

  // ---- Aus HKAP übernehmen
  async function uebernehmen() {
    ergebnisZeigen(spinner('Übernahme aus HKAP läuft …'));
    hinweisSetzen('Übernahme aus HKAP …');
    let r;
    try { r = await sende('hkap_uebernehmen', T.uebernehmenParameter({ datenarten: [...arten], wahl, uebrige, systemWahl, abgleich: daten })); }
    finally { hinweisSetzen(''); }
    const tabelle = r.angenommen && (r.aenderungen ?? []).length > 0 ? `<h4>Änderungen in KICAD</h4>${aenderungenTabelle(r.aenderungen)}` : '';
    const befunde = r.angenommen && (r.neueBefunde ?? []).length > 0 ? `<p class="warnung">Neue Prüfbefunde nach der Übernahme: ${r.neueBefunde.length} (Register Verwalten › Prüfbefunde).</p>` : '';
    const beiKicad = Object.values(wahl).includes('kicad') || uebrige === 'kicad'
      ? '<p class="leise">Konflikte, die mit «KICAD» entschieden sind, gehen erst mit «Nach HKAP senden» an HKAP.</p>' : '';
    ergebnisZeigen(T.ergebnisHtml(r, tabelle) + befunde + beiKicad + (r.angenommen && (r.aenderungen ?? []).length > 0 ? '<p class="leise">Rückgängig mit Strg+Z (die Übernahme ist ein Paket).</p>' : ''));
    if (r.angenommen) { wahl = {}; uebrige = null; systemWahl = {}; await laden(); }
  }

  // ---- Nach HKAP senden: zuerst Trockenlauf, dann senden
  const sendeDaten = () => [...arten].filter((a) => T.SENDBAR.includes(a));
  async function sendenVorschau() {
    ergebnisZeigen(spinner('HKAP prüft (Trockenlauf – es wird nichts gesendet) …'));
    const r = await sende('hkap_senden', T.sendenParameter({ datenarten: sendeDaten() }), { trockenlauf: true });
    ergebnisZeigen(T.ergebnisHtml(r) + (r.angenommen ? '<p class="leise">Das war nur die Prüfung. «Jetzt senden» schickt die Änderungen an HKAP.</p>' : ''));
    sendenBereit = !!r.angenommen;
    if (jetzt) jetzt.hidden = !sendenBereit;
  }
  async function jetztSenden() {
    ergebnisZeigen(spinner('Senden an HKAP läuft …'));
    hinweisSetzen('Senden an HKAP …');
    let r;
    try { r = await sende('hkap_senden', T.sendenParameter({ datenarten: sendeDaten() })); }
    finally { hinweisSetzen(''); }
    const tabelle = r.angenommen && (r.aenderungen ?? []).length > 0 ? `<h4>Änderungen in KICAD (Kennungen)</h4>${aenderungenTabelle(r.aenderungen)}` : '';
    ergebnisZeigen(T.ergebnisHtml(r, tabelle));
    sendenBereit = false; if (jetzt) jetzt.hidden = true;
    if (r.angenommen) await laden();
  }

  await zeigen({
    titel: 'Abgleich mit HKAP', inhalt: w, breite: 1060, klasse: 'dlg-hoch hkap',
    beiOeffnen: (ctrl) => {
      jetzt = [...ctrl.fuss.children].find((b) => b.textContent === 'Jetzt senden') ?? null;
      if (jetzt) jetzt.hidden = true;
      laden();
    },
    knoepfe: [
      { text: 'Schliessen', wert: true, primaer: true },   // Enter schliesst nur; Übernehmen und Senden brauchen einen Klick
      { text: 'Neu laden', schliesst: false, klick: laden },
      { text: 'Nach HKAP senden …', schliesst: false, pruefen: () => (!bereit() ? nichtBereit() : T.sendenPruefen([...arten])), klick: sendenVorschau },
      { text: 'Jetzt senden', schliesst: false, pruefen: () => (sendenBereit ? true : 'Zuerst «Nach HKAP senden …» (Vorschau) ansehen.'), klick: jetztSenden },
      { text: 'Aus HKAP übernehmen', schliesst: false, pruefen: () => (!bereit() ? nichtBereit() : T.uebernehmenPruefen([...arten])), klick: uebernehmen },
    ],
  });
  nr++;
}

// ================================================================================================ Prüfbefunde senden

async function befundeSenden() {
  let nr = 0, vorschauOk = false;
  const w = knoten(`<div class="hkap-dlg">
    <p class="dlg-text">Schickt die Prüfbefunde dieses Projekts als Pendenzen an HKAP (Quelle KICAD, mit dem Raum). Erneutes Senden trifft dieselben Pendenzen; behobene Befunde bekommen in HKAP den Vorschlag «erledigt», gelöscht wird nie.</p>
    <label class="fz"><span>Ab Schwere</span><select class="b-schwere">${T.SCHWEREN.map(([wert, text]) => `<option value="${wert}"${wert === 'fehler' ? ' selected' : ''}>${esc(text)} – ${esc(T.SCHWERE_ERKLAERUNG[wert])}</option>`).join('')}</select></label>
    <div class="b-vorschau"></div>
    <div class="b-ergebnis" hidden></div></div>`);
  const q = (s) => w.querySelector(s);
  const wahl = q('.b-schwere'), vorschauFeld = q('.b-vorschau'), ergebnis = q('.b-ergebnis');

  async function vorschau() {
    const mein = ++nr;
    vorschauOk = false;
    ergebnis.hidden = true;
    vorschauFeld.innerHTML = spinner('HKAP wird gefragt (Trockenlauf – es wird nichts gesendet) …');
    const r = await sende('hkap_befunde_senden', T.befundeParameter(wahl.value), { trockenlauf: true });
    if (mein !== nr) return;
    vorschauOk = !!r.angenommen;
    vorschauFeld.innerHTML = T.ergebnisHtml(r);
  }
  wahl.addEventListener('change', vorschau);

  await zeigen({
    titel: 'Prüfbefunde an HKAP senden', inhalt: w, breite: 820, klasse: 'dlg-hoch hkap',
    beiOeffnen: () => { vorschau(); },
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Vorschau neu', schliesst: false, klick: vorschau },
      { text: 'Senden', primaer: true, schliesst: false,
        pruefen: () => (vorschauOk ? true : 'Die Vorschau ist noch nicht da oder wurde abgelehnt. Erst «Vorschau neu» ansehen.'),
        async klick() {
          ++nr;
          ergebnis.hidden = false;
          ergebnis.innerHTML = spinner('Befunde werden an HKAP gesendet …');
          hinweisSetzen('Prüfbefunde werden an HKAP gesendet …');
          let r;
          try { r = await sende('hkap_befunde_senden', T.befundeParameter(wahl.value)); }
          finally { hinweisSetzen(''); }
          vorschauFeld.innerHTML = '';
          ergebnis.innerHTML = T.ergebnisHtml(r);
          vorschauOk = false;
          ergebnis.scrollIntoView({ block: 'nearest' });
        } }],
  });
  nr++;
}

// ================================================================================================ Nicht platzierte Räume

/** HKAP-Raum-Id eines nicht platzierten Raums: Kennung der Darstellung, sonst aus der Herkunft seiner Werte (element_lesen). */
async function hkapIdFuer(raum) {
  const pj = verknuepfung()?.projektierung ?? null;
  const direkt = T.hkapRaumId(raum, null, pj);
  if (direkt) return direkt;
  try { return T.hkapRaumId(raum, await abfrage('element_lesen', { element: raum.kennzeichen }), pj); }
  catch (e) { console.error(e); return null; }
}

const keineId = (raum) => meldung(`Zu ${raum.kennzeichen} ist keine HKAP-Raum-Id bekannt.`, 'warnung', { vorschlag: 'Im Abgleich den Raum prüfen; wurde er nicht aus HKAP übernommen, ist er kein nicht platzierter Raum.' });

/** Ordnet den gezeichneten Raum dem nicht platzierten (hkap_raum_zuordnen) zu und meldet das Ergebnis. */
async function zuordnen(gezeichnet, platzhalter) {
  const id = await hkapIdFuer(platzhalter);
  if (!id) { keineId(platzhalter); return false; }
  const r = await sende('hkap_raum_zuordnen', T.zuordnenParameter(gezeichnet.kennzeichen, id));
  if (!r.angenommen) { meldung(r.fehler || r.meldung || 'Zuordnung abgelehnt.', 'warnung', { vorschlag: r.vorschlag }); return false; }
  meldung(T.hinweisFuerMenschen((r.hinweise ?? []).filter(Boolean).slice(-1)[0] ?? 'Raum zugeordnet.'), 'ok');
  return true;
}

/** Nach dem Zeichnen eines Raums anbieten, ihn dem nicht platzierten Raum zuzuordnen (das Raum-Werkzeug legt sonst einen zweiten Raum an). */
function platzierungBeobachten(platzhalter, vorherIds) {
  let ende = null;
  const stop = () => { ab(); ende?.(); };
  const ab = abonnieren('modell', () => {
    const neu = T.neuGezeichneterRaum(vorherIds, derKlasse('Raum'));
    if (!neu) return;
    stop();
    setTimeout(async () => {
      const ja = await bestaetigen(
        `Der gezeichnete Raum ${neu.kennzeichen} gehört zum nicht platzierten Raum «${T.raumTitel(platzhalter)}» aus HKAP?\n\nZuordnen: der Platzhalter geht im gezeichneten Raum auf; was in HKAP anders steht, zeigt danach der Abgleich. Rückgängig mit Strg+Z.`,
        { ja: 'Zuordnen', nein: 'Nicht jetzt', titel: 'Gezeichneten Raum zuordnen' });
      if (ja) await zuordnen(neu, platzhalter);
    }, 150);
  });
  ende = beiWerkzeugEnde(() => setTimeout(stop, 1500));
}

async function raumPlatzieren(p) {
  const r = element(p?.element ?? zustand.auswahl);
  if (!r || !T.istNichtPlatziert(r)) { meldung('Zuerst einen nicht platzierten Raum wählen.', 'info'); return; }
  const g = T.grundrissFuer(alleElemente(), r);
  if (!g) {
    meldung(r.geschoss ? `Für das Geschoss ${r.geschoss} gibt es keinen Grundriss.` : `${r.kennzeichen} hat noch kein Geschoss.`, 'info',
      { vorschlag: r.geschoss ? 'Register Ansicht › Grundriss anlegen, dann erneut «Platzieren …».' : 'Dem Raum zuerst ein Geschoss geben (Eigenschaften).' });
    return;
  }
  await ansichtOeffnen(g.kennzeichen);
  for (let i = 0; i < 25 && !(planAnsicht() && geschossVon(planAnsicht()) === r.geschoss); i++) await new Promise((f) => setTimeout(f, 80));
  if (!planAnsicht() || geschossVon(planAnsicht()) !== r.geschoss) { meldung(`Der Grundriss ${g.kennzeichen} konnte nicht geöffnet werden.`, 'warnung'); return; }
  const vor = T.raumVorbelegung(r);
  setze('raum', 'nummer', vor.nummer);
  setze('raum', 'nutzung', vor.nutzung);
  const vorherIds = new Set(derKlasse('Raum').map((x) => x.id));
  if (!werkzeugStarten(raumWerkzeug())) return;
  platzierungBeobachten(r, vorherIds);
  meldung(`Raum «${T.raumTitel(r)}» platzieren: in einen von Wänden umschlossenen Bereich klicken. Nummer und Nutzung sind vorbelegt; danach lässt sich der gezeichnete Raum zuordnen.`, 'info');
}

async function raumZuordnenDialog(p) {
  const r = element(p?.element ?? zustand.auswahl);
  if (!r || !T.istNichtPlatziert(r)) { meldung('Zuerst einen nicht platzierten Raum wählen.', 'info'); return; }
  const kandidaten = T.zuordnenKandidaten(alleElemente(), r);
  if (kandidaten.length === 0) {
    await zeigen({ titel: 'Einem gezeichneten Raum zuordnen', breite: 520,
      inhalt: `<p class="dlg-text">Es gibt keinen gezeichneten Raum, dem «${esc(T.raumTitel(r))}» zugeordnet werden könnte.</p><p class="leise">Zuerst den Raum zeichnen (Register Architektur › Raum) oder über «Platzieren …».</p>`,
      knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
    return;
  }
  const vorwahl = kandidaten.find((k) => k.gleicheNummer) ?? null;
  const w = knoten(`<div class="hkap-dlg"><p class="dlg-text">Welcher gezeichnete Raum ist <b>${esc(T.raumTitel(r))}</b> aus HKAP${r.geschoss ? ` (Geschoss ${esc(r.geschoss)})` : ''}? Der nicht platzierte Raum geht im gezeichneten auf; Werte, die in HKAP anders stehen, zeigt danach der Abgleich.</p>
    <table class="tab klickbar hkap-liste"><thead><tr><th></th><th>Raum</th><th>Name / Nutzung</th><th>Geschoss</th><th></th></tr></thead><tbody>${kandidaten.map((k, i) =>
      `<tr data-kz="${esc(k.raum.kennzeichen)}"><td><input type="radio" name="h-raum" value="${esc(k.raum.kennzeichen)}"${vorwahl && k.raum === vorwahl.raum ? ' checked' : ''} aria-label="${esc(k.raum.kennzeichen)}"></td><td>${esc(k.raum.kennzeichen)}</td><td>${esc(T.raumTitel(k.raum))}</td><td>${esc(k.raum.geschoss ?? '–')}</td><td class="leise">${k.gleicheNummer ? 'gleiche Raumnummer' : k.gleichesGeschoss ? 'gleiches Geschoss' : ''}</td></tr>`).join('')}</tbody></table></div>`);
  w.addEventListener('click', (e) => { const z = e.target.closest('tr[data-kz]'); if (z && e.target.tagName !== 'INPUT') z.querySelector('input').checked = true; });
  await zeigen({
    titel: 'Einem gezeichneten Raum zuordnen', inhalt: w, breite: 640,
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Zuordnen', primaer: true, schliesst: false,
        pruefen: () => (w.querySelector('input[name=h-raum]:checked') ? true : 'Bitte einen gezeichneten Raum wählen.'),
        async klick(ctrl) {
          const id = await hkapIdFuer(r);
          if (!id) { ctrl.fehler(`Zu ${r.kennzeichen} ist keine HKAP-Raum-Id bekannt. Im Abgleich prüfen, ob der Raum aus HKAP übernommen ist.`); return; }
          const gez = w.querySelector('input[name=h-raum]:checked').value;
          const res = await sende('hkap_raum_zuordnen', T.zuordnenParameter(gez, id));
          if (!res.angenommen) { ctrl.fehler(fehlerText(res)); return; }
          meldung(T.hinweisFuerMenschen((res.hinweise ?? []).filter(Boolean).slice(-1)[0] ?? 'Raum zugeordnet.'), 'ok');
          ctrl.schliessen(true);
        } }],
  });
}

// ================================================================================================ Aktionen

registrieren({
  id: 'hkap.verbinden', titel: 'Verbinden', symbol: 'hkapVerbinden', kuerzel: null, register: 'verwalten', gruppe: 'HKAP',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: verbinden,
});
registrieren({
  id: 'hkap.abgleich', titel: 'Abgleich', symbol: 'hkapAbgleich', kuerzel: null, register: 'verwalten', gruppe: 'HKAP',
  aktiv: verknuepft, hinweis: () => (projektOffen() ? NICHT_VERKNUEPFT : KEIN_PROJEKT), ausfuehren: abgleichDialog,
});
registrieren({
  id: 'hkap.befunde', titel: 'Prüf­befunde senden', symbol: 'hkapBefunde', kuerzel: null, register: 'verwalten', gruppe: 'HKAP',
  aktiv: verknuepft, hinweis: () => (projektOffen() ? NICHT_VERKNUEPFT : KEIN_PROJEKT), ausfuehren: befundeSenden,
});

// Kontextaktionen der nicht platzierten Räume im Projektbrowser (nicht im Menüband)
registrieren({ id: 'hkap.raum-platzieren', titel: 'Platzieren …', symbol: 'raum', ausfuehren: raumPlatzieren, aktiv: verknuepft, hinweis: NICHT_VERKNUEPFT });
registrieren({ id: 'hkap.raum-zuordnen', titel: 'Einem gezeichneten Raum zuordnen …', symbol: 'raum', ausfuehren: raumZuordnenDialog, aktiv: verknuepft, hinweis: NICHT_VERKNUEPFT });

export { verbinden, abgleichDialog, befundeSenden, raumPlatzieren, raumZuordnenDialog };
