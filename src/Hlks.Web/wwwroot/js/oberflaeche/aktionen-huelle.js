// Aktionen der Gebäudehülle (Modul 1c, Auftrag C1a): Register «Architektur» (Decke, Boden, Dach erzeugen) und Register «Hülle»
// (Aufbau-Editor, Zuweisen, Material, Fensterkennwerte, Übergang Rsi/Rse, Hülle prüfen, Wärmebrücken vorschlagen, Listen Raumgrenzen und Hüllbauteile).
// Keine Fachlogik: alles geht über die Befehle decke_erzeugen, material_anlegen / material_aendern / material_uebernehmen, aufbau_anlegen /
// aufbau_aendern / aufbau_zuweisen, fenster_kennwerte, uebergang_setzen, waermebruecken_vorschlagen und die Abfragen aufbau_rechnen, materialien,
// huelle_pruefen, raumgrenzen, huellbauteile des Kerns. R und U zeigt die Oberfläche nur an (Trockenlauf bzw. aufbau_rechnen); jeder Kennwert geht mit
// seiner Quelle (Pflicht). Texte, HTML und Parameter stehen in huelle-text.js (reine Funktionen, in Node getestet).

import { zustand, element, derKlasse, alleElemente, abonnieren, auswaehlen } from '../zustand.js';
import { abfrage } from '../api.js';
import { registrieren } from './register.js';
import { zeigen, meldung } from './dialoge.js';
import { sende } from './befehle.js';
import { knoten, entprellen, aufElement, freiesKennzeichen } from './werkzeug.js';
import { auswahlElemente, auswahlKennzeichen } from '../werkzeuge/auswahl.js';
import { aufbereiten } from '../ansichten/listenformat.js';
import { ansichtOeffnen } from '../fenster.js';
import * as T from './huelle-text.js';

const esc = T.esc;
const projektOffen = () => !!zustand.status?.offen;
const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';
const klon = (x) => JSON.parse(JSON.stringify(x));
const spinner = (t) => `<div class="lauf"><span class="spinner" aria-hidden="true"></span><span class="lauf-text">${esc(t)}</span></div>`;
const feldWert = (w, s) => w.querySelector(s)?.value ?? '';

/** Abfrage ohne Ausnahme: bei Fehler { fehler, vorschlag } (für Anzeige im Dialog). */
async function frage(name, p = {}) {
  try { return await abfrage(name, p); } catch (e) { console.error(name, e); return { __fehler: e?.message ?? String(e), __vorschlag: e?.vorschlag ?? '' }; }
}
const fehlerKasten = (a) => `<div class="fehlerkasten" role="alert"><b>${esc(a.__fehler)}</b>${a.__vorschlag ? `<div>Vorschlag: ${esc(a.__vorschlag)}</div>` : ''}</div>`;

/** Dialog wie zeigen(), nur verschwindet ein Fehlertext im Dialog, sobald man etwas ändert. */
function dialog(opt) {
  const vorher = opt.beiOeffnen;
  return zeigen({ ...opt, beiOeffnen: (ctrl) => { const f = () => ctrl.fehler(null); ctrl.koerper.addEventListener("input", f); ctrl.koerper.addEventListener("change", f); vorher?.(ctrl); } });
}

/** Springt zu einem Element: wählt es aus (Eigenschaften und Projektbrowser zeigen es). */
function springen(kz) {
  const e = element(kz);
  if (!e) { meldung(`${kz} gibt es nicht (mehr).`, 'warnung'); return false; }
  auswaehlen(e.id);
  return true;
}

// ================================================================================================ Decke, Boden, Dach (decke_erzeugen)

async function deckeDialog(art) {
  const def = T.DECKE_ARTEN[art];
  const gs = derKlasse('Geschoss');
  if (gs.length === 0) { meldung('Im Modell gibt es noch kein Geschoss.', 'warnung', { vorschlag: 'Zuerst ein Geschoss anlegen.' }); return; }
  const aktiv = element(zustand.aktiveAnsicht)?.geschoss ?? null;
  const vorgabe = T.geschossVorgabe(gs, aktiv);
  const aufbauten = T.aufbautenFuerDecke(derKlasse('Aufbau'), art);
  const w = knoten(`<div class="huelle-dlg">
    <p class="dlg-text">${esc(def.erklaerung)} Der Umriss ist die Aussenkontur der Wände und Räume des Geschosses.</p>
    <label class="fz"><span>Geschoss</span><select class="d-geschoss">${T.geschossOptionen(gs).map((o) => `<option value="${esc(o.wert)}"${o.wert === vorgabe ? ' selected' : ''}>${esc(o.text)}</option>`).join('')}</select></label>
    <label class="fz"><span>Dicke (mm) *</span><input type="text" class="d-dicke" inputmode="decimal" placeholder="Pflicht – keine Vorgabe" data-kein-enter="1"></label>
    <label class="fz"><span>Aufbau (optional)</span><select class="d-aufbau"><option value="">(kein Aufbau)</option>${aufbauten.map((a) => `<option value="${esc(a.kennzeichen)}">${esc(a.kennzeichen)}${a.name ? ' – ' + esc(a.name) : ''}</option>`).join('')}</select></label>
    <p class="leise">Die Dicke gibt es nur von dir (Plan des Architekten, Bauteilaufbau); der Kern setzt keine Vorgabe.${aufbauten.length === 0 ? ` Es gibt noch keinen Aufbau der Art «${esc(art)}» (Register Hülle › Aufbau …).` : ''}</p>
    <div class="d-vorschau"></div></div>`);
  const feld = () => ({ geschoss: feldWert(w, '.d-geschoss'), art, dicke: feldWert(w, '.d-dicke'), aufbau: feldWert(w, '.d-aufbau') });
  const vorschau = w.querySelector('.d-vorschau');
  let nr = 0, vorschauOk = false, erzeugt = false;

  async function vorschauRechnen() {
    const mein = ++nr;
    vorschauOk = false; erzeugt = false;
    const f = feld();
    const ok = T.deckePruefen(f);
    if (ok !== true) { vorschau.innerHTML = `<p class="leise">${esc(ok)}</p>`; return; }
    vorschau.innerHTML = spinner('Der Kern rechnet den Umriss (Trockenlauf – nichts wird geändert) …');
    const r = await sende('decke_erzeugen', T.deckeParameter(f), { trockenlauf: true });
    if (mein !== nr) return;
    vorschauOk = !!r.angenommen;
    vorschau.innerHTML = T.deckeErgebnisHtml(r);
  }
  const plan = entprellen(vorschauRechnen, 350);
  w.addEventListener('input', plan);
  w.addEventListener('change', plan);

  await dialog({
    titel: `${def.titel} erzeugen`, inhalt: w, breite: 640, klasse: 'huelle',
    beiOeffnen: () => plan(),
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: `${def.titel} erzeugen`, primaer: true, schliesst: false,
        pruefen: () => (erzeugt ? 'Schon erzeugt. Für eine weitere Platte Geschoss oder Dicke ändern.' : T.deckePruefen(feld())),
        async klick(ctrl) {
          const r = await sende('decke_erzeugen', T.deckeParameter(feld()));
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          ++nr; erzeugt = true; vorschauOk = false;
          vorschau.innerHTML = T.deckeErgebnisHtml(r);
          meldung(`${def.titel} erzeugt.`, 'ok');
        } }],
  });
  nr++;
}

// ================================================================================================ Material

async function materialDialog(p) {
  let mat = await frage('materialien');
  if (mat.__fehler) { meldung(mat.__fehler, 'fehler', { vorschlag: mat.__vorschlag }); return; }
  let form = T.materialLeer(), bisher = null;
  const waehle = (kz) => {
    const e = (mat.projekt ?? []).find((m) => m.kennzeichen === kz);
    form = T.materialFormular(e ?? null);
    bisher = e ? T.materialFormular(e) : null;
  };
  if (p?.material) waehle(p.material);

  const w = knoten(`<div class="huelle-dlg huelle-zwei">
    <div class="huelle-links">
      <h3>Projektmaterialien</h3><div class="m-liste"></div>
      <h3>Bürobibliothek</h3><div class="m-bib"></div>
    </div>
    <div class="huelle-rechts">
      <h3 class="m-titel"></h3>
      <label class="fz"><span>Name *</span><input type="text" class="m-name" data-kein-enter="1"></label>
      <label class="fz"><span>Kategorie</span><input type="text" class="m-kategorie" placeholder="z. B. Beton, Dämmstoff, Putz" data-kein-enter="1"></label>
      <table class="tab huelle-kennwerte"><thead><tr><th>Kennwert</th><th>Wert</th><th>Quelle *</th></tr></thead><tbody class="m-werte"></tbody></table>
      <p class="huelle-regel"><b>${esc(T.QUELLENSATZ)}.</b> Ohne Quelle wird nicht gespeichert; der Kern kennt keine Werte aus dem Gedächtnis. <button type="button" class="chip m-quelle-alle" title="Die erste ausgefüllte Quelle in alle Zeilen mit Wert und leerer Quelle übernehmen">Quelle in leere Zeilen übernehmen</button></p>
      <div class="m-meldung"></div>
    </div></div>`);
  const q = (s) => w.querySelector(s);

  const werteZeichnen = () => {
    q('.m-werte').innerHTML = T.KENNWERTE.map((k) => {
      const x = form.werte[k.name];
      return `<tr data-k="${k.name}"><td title="${esc(k.label)}">${esc(k.label)}${k.einheit ? ` <span class="leise">${esc(k.einheit)}</span>` : ''}</td>`
        + `<td><input type="text" class="k-wert zahl" inputmode="decimal" value="${esc(x.wert)}" aria-label="${esc(k.label)}" data-kein-enter="1"></td>`
        + `<td><input type="text" class="k-quelle" value="${esc(x.quelle)}" placeholder="Norm mit Ausgabe, Datenblatt …" aria-label="Quelle ${esc(k.kurz)}" data-kein-enter="1"></td></tr>`;
    }).join('');
  };
  const alles = () => {
    q('.m-titel').textContent = form.kennzeichen ? `${form.kennzeichen} bearbeiten` : 'Neues Material';
    q('.m-name').value = form.name; q('.m-kategorie').value = form.kategorie;
    werteZeichnen();
    q('.m-liste').innerHTML = T.materialListeHtml(mat, form.kennzeichen)
      + `<p><button type="button" class="chip m-neu">Neues Material</button></p>`;
    q('.m-bib').innerHTML = T.bibliothekHtml(mat.bibliothek);
  };
  alles();

  w.addEventListener('input', (e) => {
    if (e.target.matches('.m-name')) form.name = e.target.value;
    else if (e.target.matches('.m-kategorie')) form.kategorie = e.target.value;
    else if (e.target.matches('.k-wert, .k-quelle')) {
      const k = e.target.closest('tr').dataset.k;
      form.werte[k][e.target.matches('.k-wert') ? 'wert' : 'quelle'] = e.target.value;
    }
  });
  w.addEventListener('click', async (e) => {
    const z = e.target.closest('.m-liste tbody tr');
    if (z) { waehle(z.dataset.kz); alles(); return; }
    if (e.target.closest('.m-neu')) { form = T.materialLeer(); bisher = null; alles(); return; }
    if (e.target.closest('.m-quelle-alle')) {
      const erste = T.KENNWERTE.map((k) => form.werte[k.name]).find((x) => x.quelle.trim() !== '' && x.wert.trim() !== '');
      if (erste) { for (const k of T.KENNWERTE) { const x = form.werte[k.name]; if (x.wert.trim() !== '' && x.quelle.trim() === '') x.quelle = erste.quelle; } werteZeichnen(); }
      return;
    }
    const b = e.target.closest('.b-uebernehmen');
    if (b) {
      b.disabled = true;
      const r = await sende('material_uebernehmen', { name: b.dataset.name });
      q('.m-meldung').innerHTML = T.laufHtml(r);
      b.disabled = false;
      if (r.angenommen) { await neuLaden(r.aenderungen?.find((a) => a.klasse === 'Material' && a.art === 'angelegt')?.kennzeichen ?? null); meldung(`«${b.dataset.name}» aus der Bürobibliothek übernommen.`, 'ok'); }
    }
  });

  async function neuLaden(waehleKz) {
    const m = await frage('materialien');
    if (m.__fehler) return;
    mat = m;
    if (waehleKz) waehle(waehleKz); else if (form.kennzeichen) waehle(form.kennzeichen);
    alles();
  }
  // Änderungen anderer Arbeitsplätze oder der KI: Liste nachführen (nicht das Formular, an dem gerade getippt wird)
  const abo = abonnieren('modell', entprellen(async () => {
    const m = await frage('materialien');
    if (!m.__fehler) { mat = m; q('.m-liste').innerHTML = T.materialListeHtml(mat, form.kennzeichen) + '<p><button type="button" class="chip m-neu">Neues Material</button></p>'; q('.m-bib').innerHTML = T.bibliothekHtml(mat.bibliothek); }
  }, 300));

  await dialog({
    titel: 'Material', inhalt: w, breite: 1020, klasse: 'dlg-hoch huelle',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Speichern', primaer: true, schliesst: false,
        pruefen: () => {
          const pr = T.materialPruefen(form, bisher);
          return pr.fehler.length ? pr.fehler.join(' ') : true;
        },
        async klick(ctrl) {
          const pr = T.materialPruefen(form, bisher);
          let r;
          if (!form.kennzeichen) r = await sende('material_anlegen', T.materialAnlegenParameter(form));
          else {
            const param = T.materialAendernParameter(form, bisher);
            if (!param) { ctrl.fehler('Nichts geändert.'); return; }
            r = await sende('material_aendern', param);
          }
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          const kz = form.kennzeichen ?? r.aenderungen?.find((a) => a.klasse === 'Material' && a.art === 'angelegt')?.kennzeichen ?? null;
          await neuLaden(kz);
          q('.m-meldung').innerHTML = (pr.warnungen.length ? `<ul class="hinweisliste warnung">${pr.warnungen.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : '') + T.laufHtml(r, { titel: 'Gespeichert' });
          meldung(`${kz ?? 'Material'} gespeichert.`, 'ok');
        } }],
  });
  abo();
}

// ================================================================================================ Aufbau-Editor

async function aufbauEditor(p) {
  let kz = p?.aufbau && element(p.aufbau)?.klasse === 'Aufbau' ? element(p.aufbau).kennzeichen : null;
  let mat = await frage('materialien');
  if (mat.__fehler) { meldung(mat.__fehler, 'fehler', { vorschlag: mat.__vorschlag }); return; }
  let gespeichert = null;
  const matKz = (id) => element(id)?.kennzeichen ?? id;
  const ladeGespeichert = async () => {
    if (!kz) { gespeichert = null; return; }
    const g = await frage('aufbau_rechnen', { aufbau: kz });
    gespeichert = g.__fehler ? null : g;
  };
  await ladeGespeichert();
  let ausgang = kz ? T.aufbauFormular(element(kz), gespeichert, matKz) : T.aufbauLeer();
  if (kz) ausgang.kennzeichen = kz;
  let form = klon(ausgang);

  const w = knoten(`<div class="huelle-dlg aufbau-dlg">
    <div class="huelle-kopf">
      <label class="fz"><span>Name *</span><input type="text" class="a-name" data-kein-enter="1"></label>
      <label class="fz"><span>Art *</span><select class="a-art">${T.AUFBAU_ARTEN.map(([w_, t]) => `<option value="${w_}">${t}</option>`).join('')}</select></label>
      <label class="fz"><span>Wärmestrom</span><select class="a-strom">${T.WAERMESTROM.map(([w_, t]) => `<option value="${w_}">${esc(t)}</option>`).join('')}</select></label>
    </div>
    <h3>Schichten – von innen nach aussen</h3>
    <table class="tab huelle-schichten"><thead><tr><th class="zahl">Nr</th><th>Material</th><th>Dicke mm</th><th class="zahl">λ W/(m·K)</th><th class="zahl">R m²·K/W</th><th>innen ↑ ↓ aussen</th></tr></thead><tbody class="a-schichten"></tbody></table>
    <p class="huelle-leiste"><button type="button" class="chip a-ende">＋ Schicht aussen anfügen</button> <button type="button" class="chip a-material">Material …</button>
      <span class="leise">λ und R stammen vom Kern; R einer geänderten Schicht rechnet der Kern nach dem Speichern.</span></p>
    <h3>Rechnung des Kerns <span class="leise a-quelle-rechnung"></span></h3>
    <div class="a-summen"></div>
    <details class="a-u"><summary>U-Wert ausdrücklich setzen (Datenblatt, IFC) – geht vor der Rechnung, Quelle ist Pflicht</summary>
      <label class="fz"><span>U W/(m²·K)</span><input type="text" class="a-uwert zahl" inputmode="decimal" data-kein-enter="1"></label>
      <label class="fz"><span>Quelle des U-Werts *</span><input type="text" class="a-uquelle" placeholder="Datenblatt mit Datum, Norm mit Tabelle …" data-kein-enter="1"></label></details>
    <label class="fz"><span>Quelle der Schichtfolge</span><input type="text" class="a-quelle" placeholder="optional: Plan oder Bauteilkatalog des Architekten" data-kein-enter="1"></label>
    <div class="a-meldung"></div></div>`);
  const q = (s) => w.querySelector(s);

  const optionen = () => T.materialOptionen(mat);
  const infos = () => T.schichtInfo(form, mat, kz ? gespeichert : null);

  function felderSetzen() {
    q('.a-name').value = form.name; q('.a-art').value = form.art; q('.a-strom').value = form.waermestrom;
    q('.a-uwert').value = form.uWert; q('.a-uquelle').value = form.uQuelle; q('.a-quelle').value = form.quelle;
    if (form.uWert) q('.a-u').open = true;
  }
  function zeilenZeichnen() {
    const zeilen = form.schichten;
    const inf = infos();
    q('.a-schichten').innerHTML = zeilen.map((s, i) => T.schichtZeileHtml(i, s, inf.find((x) => x.nr === i + 1) ?? null, optionen(), zeilen.length)).join('');
  }
  function zellenAktualisieren() {
    const inf = infos();
    form.schichten.forEach((s, i) => {
      const tr = q(`.a-schichten tr[data-i="${i}"]`); if (!tr) return;
      const x = inf.find((y) => y.nr === i + 1) ?? null;
      tr.querySelector('.s-lambda').innerHTML = T.schichtLambdaHtml(x);
      tr.querySelector('.s-r').innerHTML = x?.r === null || x?.r === undefined ? '<span class="leise">·</span>' : esc(T.zahlAnzeige(x.r));
    });
  }

  let nr = 0;
  async function rechnen() {
    const mein = ++nr;
    const geaendert = T.aufbauGeaendert(form, ausgang);
    const pr = T.aufbauPruefen(form);
    let lauf = null;
    if (geaendert && pr.fehler.length === 0) {
      if (kz) { const param = T.aufbauAendernParameter(form, ausgang); lauf = param ? await sende('aufbau_aendern', param, { trockenlauf: true }) : null; }
      else lauf = await sende('aufbau_anlegen', T.aufbauAnlegenParameter(form), { trockenlauf: true });
    }
    if (mein !== nr) return;
    zellenAktualisieren();
    const ziel = q('.a-summen');
    if (lauf && !lauf.angenommen) { ziel.innerHTML = T.laufHtml(lauf); q('.a-quelle-rechnung').textContent = ''; return; }
    if (geaendert && pr.fehler.length) {
      ziel.innerHTML = `<p class="leise">Eingabe noch nicht vollständig – der Kern rechnet, sobald sie stimmt:</p><ul class="hinweisliste">${pr.fehler.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`;
      q('.a-quelle-rechnung').textContent = ''; return;
    }
    const s = T.summen(lauf, kz ? gespeichert : null);
    ziel.innerHTML = T.summenHtml(s);
    q('.a-quelle-rechnung').textContent = !geaendert && kz ? '(gespeicherter Stand, aufbau_rechnen)' : lauf ? '(Trockenlauf – noch nicht gespeichert)' : '';
    if (!geaendert && gespeichert?.uQuelle) ziel.insertAdjacentHTML('beforeend', `<details class="huelle-herleitung"><summary>Herleitung</summary><p class="leise">${esc(gespeichert.uQuelle)}</p></details>`);
  }
  const plan = entprellen(rechnen, 350);

  felderSetzen(); zeilenZeichnen(); rechnen();

  w.addEventListener('input', (e) => {
    const t = e.target;
    if (t.matches('.a-name')) form.name = t.value;
    else if (t.matches('.a-uwert')) form.uWert = t.value;
    else if (t.matches('.a-uquelle')) form.uQuelle = t.value;
    else if (t.matches('.a-quelle')) form.quelle = t.value;
    else if (t.matches('.s-dicke')) form.schichten[Number(t.closest('tr').dataset.i)].dicke = t.value;
    else return;
    plan();
  });
  w.addEventListener('change', (e) => {
    const t = e.target;
    if (t.matches('.a-art')) form.art = t.value;
    else if (t.matches('.a-strom')) form.waermestrom = t.value;
    else if (t.matches('.s-material')) form.schichten[Number(t.closest('tr').dataset.i)].material = t.value;
    else return;
    zellenAktualisieren(); plan();
  });
  w.addEventListener('click', async (e) => {
    const knopf = e.target.closest('.s-hoch, .s-runter, .s-davor, .s-weg');
    if (knopf) {
      const aktion = knopf.classList.contains('s-hoch') ? 'hoch' : knopf.classList.contains('s-runter') ? 'runter' : knopf.classList.contains('s-davor') ? 'davor' : 'weg';
      form.schichten = T.schichtenAendern(form.schichten, aktion, Number(knopf.closest('tr').dataset.i));
      zeilenZeichnen(); plan(); return;
    }
    if (e.target.closest('.a-ende')) { form.schichten = T.schichtenAendern(form.schichten, 'ende', 0); zeilenZeichnen(); plan(); return; }
    if (e.target.closest('.a-material')) {
      await materialDialog({});
      const m = await frage('materialien'); if (!m.__fehler) mat = m;
      zeilenZeichnen(); plan();
    }
  });
  // Material, Rsi/Rse oder Fremdänderungen: λ-Spalte und Rechnung nachführen
  const abo = abonnieren('modell', entprellen(async () => {
    const m = await frage('materialien'); if (!m.__fehler) mat = m;
    if (kz && !T.aufbauGeaendert(form, ausgang)) await ladeGespeichert();
    zellenAktualisieren(); plan();
  }, 400));

  async function speichern(ctrl) {
    const pr = T.aufbauPruefen(form);
    if (pr.fehler.length) { ctrl.fehler(pr.fehler.join(' ')); return; }
    const entfernen = T.uWertEntfernen(form, ausgang);
    let r = null;
    if (!kz) r = await sende('aufbau_anlegen', T.aufbauAnlegenParameter(form));
    else {
      const param = T.aufbauAendernParameter(form, ausgang);
      if (param) r = await sende('aufbau_aendern', param);
      else if (!entfernen) { ctrl.fehler('Nichts geändert.'); return; }
    }
    if (r && !r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
    const neuKz = kz ?? r?.aenderungen?.find((a) => a.klasse === 'Aufbau' && a.art === 'angelegt')?.kennzeichen ?? null;
    let r2 = null;
    if (entfernen && neuKz) {
      r2 = await sende('element_aendern', { element: neuKz, eigenschaften: { uWert: null } });
      if (!r2.angenommen) { ctrl.fehler(T.ablehnung(r2)); return; }
    }
    kz = neuKz;
    ausgang = klon(form); ausgang.kennzeichen = kz; ausgang.quelle = '';
    form.kennzeichen = kz; form.quelle = '';
    await ladeGespeichert();
    q('.a-quelle').value = '';
    zeilenZeichnen(); await rechnen();
    q('.a-meldung').innerHTML = T.laufHtml(r2 ?? r, { titel: `${kz} gespeichert` });
    meldung(`${kz} gespeichert.`, 'ok');
  }

  await dialog({
    titel: 'Aufbau', inhalt: w, breite: 980, klasse: 'dlg-hoch huelle',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Zuweisen …', schliesst: false,
        pruefen: () => (!kz ? 'Zuerst speichern, dann zuweisen.' : T.aufbauGeaendert(form, ausgang) ? 'Erst die Änderungen speichern, dann zuweisen.' : true),
        klick: () => zuweisenDialog({ aufbau: kz }) },
      { text: 'Speichern', primaer: true, schliesst: false, klick: speichern }],
  });
  nr++;
  abo();
}

// ================================================================================================ Zuweisen (aufbau_zuweisen)

async function zuweisenDialog(p) {
  const aufbauten = derKlasse('Aufbau').sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }));
  if (aufbauten.length === 0) { meldung('Es gibt noch keinen Aufbau.', 'info', { vorschlag: 'Register Hülle › Aufbau … : einen Aufbau anlegen.' }); return; }
  // vorgewählt werden nur Bauteile, die einen Aufbau bekommen können (ein gewählter Aufbau oder Raum gehört nicht dazu)
  const moeglich = new Set(T.zuweisenZeilen(alleElemente()).map((z) => z.kennzeichen));
  const gewaehlt = new Set((p?.elemente ?? auswahlKennzeichen()).map((x) => String(x)).filter((x) => moeglich.has(x)));
  const w = knoten(`<div class="huelle-dlg">
    <label class="fz"><span>Aufbau *</span><select class="z-aufbau">${aufbauten.map((a) => `<option value="${esc(a.kennzeichen)}"${a.kennzeichen === p?.aufbau ? ' selected' : ''}>${esc(a.kennzeichen)} – ${esc(a.name ?? '')} (${esc(a.werte?.art ?? '?')})</option>`).join('')}</select></label>
    <div class="huelle-leiste"><input type="search" class="z-suche" placeholder="Bauteile suchen …" data-kein-enter="1" aria-label="Bauteile suchen">
      <button type="button" class="chip z-alle">alle sichtbaren wählen</button> <button type="button" class="chip z-keine">alle sichtbaren abwählen</button><span class="leise z-zahl"></span></div>
    <div class="z-tabelle huelle-scroll"></div>
    <p class="leise">Wände ↔ Aufbau der Art wand, Decken ↔ decke oder boden, Dächer ↔ dach, Türen ↔ tuer. Die Art prüft der Kern; ein unpassendes Bauteil lehnt den ganzen Vorgang ab.</p></div>`);
  const q = (s) => w.querySelector(s);
  const zeilen = () => T.zuweisenZeilen(alleElemente(), [...gewaehlt], feldWert(w, '.z-suche'));
  const zeichnen = () => {
    q('.z-tabelle').innerHTML = T.zuweisenTabelleHtml(zeilen());
    q('.z-zahl').textContent = ` ${gewaehlt.size} gewählt`;
  };
  zeichnen();
  w.addEventListener('input', (e) => { if (e.target.matches('.z-suche')) zeichnen(); });
  w.addEventListener('change', (e) => {
    if (!e.target.matches('.z-wahl')) return;
    if (e.target.checked) gewaehlt.add(e.target.value); else gewaehlt.delete(e.target.value);
    q('.z-zahl').textContent = ` ${gewaehlt.size} gewählt`;
  });
  w.addEventListener('click', (e) => {
    if (e.target.closest('.z-alle')) { zeilen().forEach((z) => gewaehlt.add(z.kennzeichen)); zeichnen(); return; }
    if (e.target.closest('.z-keine')) { zeilen().forEach((z) => gewaehlt.delete(z.kennzeichen)); zeichnen(); return; }
    const tr = e.target.closest('.huelle-zuweisen tbody tr');
    if (tr && e.target.tagName !== 'INPUT') {
      const cb = tr.querySelector('.z-wahl'); cb.checked = !cb.checked; cb.dispatchEvent(new Event('change', { bubbles: true }));
    }
  });
  await dialog({
    titel: 'Aufbau zuweisen', inhalt: w, breite: 720, klasse: 'dlg-hoch huelle',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Zuweisen', primaer: true, schliesst: false,
        pruefen: () => T.zuweisenPruefen([...gewaehlt], feldWert(w, '.z-aufbau')),
        async klick(ctrl) {
          const r = await sende('aufbau_zuweisen', T.zuweisenParameter([...gewaehlt], feldWert(w, '.z-aufbau')));
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          meldung((r.hinweise ?? []).filter(Boolean).slice(-1)[0] ?? 'Aufbau zugewiesen.', 'ok');
          ctrl.schliessen(true);
        } }],
  });
}

// ================================================================================================ Fensterkennwerte (fenster_kennwerte)

async function fensterDialog() {
  const auswahl = T.fensterAuswahl(auswahlElemente());
  if (auswahl.Fenster.length + auswahl.Tuer.length === 0) { meldung('Zuerst Fenster oder Türen wählen (Strg+Klick für mehrere).', 'info'); return; }
  let klasse = auswahl.Fenster.length ? 'Fenster' : 'Tuer';
  const gemischt = auswahl.Fenster.length > 0 && auswahl.Tuer.length > 0;
  const w = knoten(`<div class="huelle-dlg">
    ${gemischt ? `<label class="fz"><span>Bearbeiten</span><select class="f-klasse"><option value="Fenster">${auswahl.Fenster.length} Fenster</option><option value="Tuer">${auswahl.Tuer.length} Türen</option></select></label>` : ''}
    <div class="f-auswahl leise"></div>
    <table class="tab huelle-kennwerte"><thead><tr><th>Kennwert</th><th>Wert</th><th>jetzt</th></tr></thead><tbody class="f-felder"></tbody></table>
    <label class="fz f-sonne-zeile"><span>Sonnenschutz</span><select class="f-sonne">${T.SONNENSCHUTZ.map(([w_, t]) => `<option value="${w_}">${esc(t)}</option>`).join('')}</select></label>
    <label class="fz"><span>Quelle der Zahlenwerte *</span><input type="text" class="f-quelle" placeholder="Datenblatt mit Datum, Norm mit Tabelle, IFC, Angabe von Christian" data-kein-enter="1"></label>
    <p class="huelle-regel"><b>${esc(T.QUELLENSATZ)}.</b> Leere Felder bleiben unverändert. Der Sonnenschutz braucht keine Quelle.</p>
    <div class="f-vorschau"></div></div>`);
  const q = (s) => w.querySelector(s);
  const liste = () => (klasse === 'Fenster' ? auswahl.Fenster : auswahl.Tuer);
  const werte = {};

  function felderZeichnen() {
    const elemente = liste();
    q('.f-auswahl').innerHTML = esc(elemente.map((e) => e.kennzeichen).join(', '));
    const felder = T.FENSTER_FELDER.filter((x) => x.fuer === klasse);
    q('.f-felder').innerHTML = felder.map((x) => {
      const jetzt = new Set(elemente.map((e) => e.werte?.[x.name]));
      const jetztText = jetzt.size === 1 ? (jetzt.values().next().value === undefined || jetzt.values().next().value === null ? '–' : T.zahlAnzeige([...jetzt][0])) : 'verschieden';
      return `<tr data-name="${x.name}"><td title="${esc(x.hilfe ?? '')}">${esc(x.label)}${x.einheit ? ` <span class="leise">${esc(x.einheit)}</span>` : ''}</td>`
        + `<td><input type="text" class="f-wert zahl" inputmode="decimal" value="${esc(werte[x.name] ?? '')}" aria-label="${esc(x.label)}" data-kein-enter="1"></td><td class="leise">${esc(jetztText)}</td></tr>`;
    }).join('');
    q('.f-sonne-zeile').hidden = klasse !== 'Fenster';
  }
  const formular = () => ({ werte: { ...werte }, sonnenschutz: klasse === 'Fenster' ? feldWert(w, '.f-sonne') : '', quelle: feldWert(w, '.f-quelle') });
  const kennzeichen = () => liste().map((e) => e.kennzeichen);
  felderZeichnen();

  let nr = 0;
  async function vorschau() {
    const mein = ++nr;
    const f = formular();
    const pr = T.fensterPruefen(f, klasse);
    const ziel = q('.f-vorschau');
    if (pr.fehler.length) { ziel.innerHTML = `<ul class="hinweisliste leise">${pr.fehler.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`; return; }
    ziel.innerHTML = spinner('Der Kern rechnet (Trockenlauf – nichts wird geändert) …');
    const r = await sende('fenster_kennwerte', T.fensterParameter(kennzeichen(), f, klasse), { trockenlauf: true });
    if (mein !== nr) return;
    ziel.innerHTML = r.angenommen
      ? `<h4>Vorschau des Kerns (Trockenlauf)</h4>${T.aenderungenDetailHtml(r.aenderungen)}${T.laufHtml(r)}<p class="leise" title="${esc(T.UW_TIPP)}">Uw gerechnet: ${esc(T.UW_TIPP)}</p>`
      : T.laufHtml(r);
  }
  const plan = entprellen(vorschau, 400);
  w.addEventListener('input', (e) => {
    if (e.target.matches('.f-wert')) werte[e.target.closest('tr').dataset.name] = e.target.value;
    plan();
  });
  w.addEventListener('change', (e) => {
    if (e.target.matches('.f-klasse')) { klasse = e.target.value; felderZeichnen(); }
    plan();
  });
  w.querySelector('.f-sonne-zeile').title = '';

  await dialog({
    titel: klasse === 'Fenster' ? 'Fensterkennwerte' : 'Türkennwerte', inhalt: w, breite: 720, klasse: 'huelle',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Anwenden', primaer: true, schliesst: false,
        pruefen: () => { const pr = T.fensterPruefen(formular(), klasse); return pr.fehler.length ? pr.fehler.join(' ') : true; },
        async klick(ctrl) {
          ++nr;
          const r = await sende('fenster_kennwerte', T.fensterParameter(kennzeichen(), formular(), klasse));
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          q('.f-vorschau').innerHTML = `<h4>Gesetzt</h4>${T.aenderungenDetailHtml(r.aenderungen)}${T.laufHtml(r)}`;
          meldung('Kennwerte gesetzt.', 'ok');
        } }],
  });
  nr++;
}

// ================================================================================================ Übergang (uebergang_setzen)

async function uebergangDialog() {
  const info = derKlasse('Projektinformation').sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen)))[0] ?? null;
  const bisher = T.uebergangFormular(info);
  const form = klon(bisher);
  const w = knoten(`<div class="huelle-dlg">
    <p class="dlg-text"><b>${esc(T.UEBERGANG_SATZ)}</b> Rsi und Rse (m²·K/W) gelten je Wärmestromrichtung und gegen Erdreich; die Quelle ist je Gruppe Pflicht (Norm mit Ausgabe und Tabelle). Der Kern kennt keine Vorgabewerte.</p>
    <table class="tab huelle-kennwerte"><thead><tr><th>Gruppe</th><th>Rsi</th><th>Rse</th><th>Quelle *</th></tr></thead><tbody>${T.UEBERGANG_GRUPPEN.map((g) =>
      `<tr data-g="${g.name}"><td>${esc(g.titel)}<div class="leise">${esc(g.fuer)}</div></td>`
      + `<td><input type="text" class="u-rsi zahl" inputmode="decimal" value="${esc(form[g.name].rsi)}" aria-label="Rsi ${esc(g.titel)}" data-kein-enter="1"></td>`
      + `<td><input type="text" class="u-rse zahl" inputmode="decimal" value="${esc(form[g.name].rse)}" aria-label="Rse ${esc(g.titel)}" data-kein-enter="1"></td>`
      + `<td><input type="text" class="u-quelle" value="${esc(form[g.name].quelle)}" placeholder="Norm mit Ausgabe, Tabelle, Seite" aria-label="Quelle ${esc(g.titel)}" data-kein-enter="1"></td></tr>`).join('')}</tbody></table>
    <p class="leise">Nicht ausgefüllte Gruppen bleiben, wie sie sind. Verschiedene Quellen gehen als je ein Paket an den Kern (Strg+Z macht sie einzeln rückgängig).</p>
    <div class="u-meldung"></div></div>`);
  w.addEventListener('input', (e) => {
    const tr = e.target.closest('tr[data-g]'); if (!tr) return;
    const k = e.target.matches('.u-rsi') ? 'rsi' : e.target.matches('.u-rse') ? 'rse' : 'quelle';
    form[tr.dataset.g][k] = e.target.value;
  });
  await dialog({
    titel: 'Übergang Rsi / Rse', inhalt: w, breite: 760, klasse: 'huelle',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Setzen', primaer: true, schliesst: false,
        pruefen: () => { const pr = T.uebergangPruefen(form, bisher); return pr.fehler.length ? pr.fehler.join(' ') : true; },
        async klick(ctrl) {
          const pr = T.uebergangPruefen(form, bisher);
          const hinweise = [];
          for (const a of T.uebergangAufrufe(form, pr.geaendert)) {
            const r = await sende('uebergang_setzen', a.parameter);
            if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
            hinweise.push(...(r.hinweise ?? []));
          }
          meldung(hinweise.filter(Boolean).slice(0, 2).join(' '), 'ok');
          ctrl.schliessen(true);
        } }],
  });
}

// ================================================================================================ Hülle prüfen (huelle_pruefen)

async function pruefenDialog() {
  const w = knoten('<div class="huelle-dlg"><div class="p-inhalt"></div></div>');
  const inhalt = w.querySelector('.p-inhalt');
  let ctrlRef = null;
  async function laden() {
    inhalt.innerHTML = spinner('Der Kern prüft die Hülle …');
    const d = await frage('huelle_pruefen');
    inhalt.innerHTML = d.__fehler ? fehlerKasten(d) : T.pruefHtml(d);
  }
  w.addEventListener('click', (e) => {
    const z = e.target.closest('.p-sprung');
    if (z && springen(z.dataset.kz)) ctrlRef?.schliessen(true);
  });
  await dialog({
    titel: 'Hülle prüfen', inhalt: w, breite: 760, klasse: 'dlg-hoch huelle',
    beiOeffnen: (ctrl) => { ctrlRef = ctrl; laden(); },
    knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }, { text: 'Neu prüfen', schliesst: false, klick: laden }],
  });
}

// ================================================================================================ Wärmebrücken (waermebruecken_vorschlagen)

async function waermebrueckenDialog() {
  const gs = derKlasse('Geschoss');
  const w = knoten(`<div class="huelle-dlg">
    <p class="dlg-text">Der Kern schlägt aus den Raumgrenzen vor: je Geschoss Deckenauflager entlang der Aussenwände und je Fenster oder Tür in einer Aussenwand den Fensteranschlag. Vorhandene Vorschläge werden nicht gedoppelt.</p>
    <label class="fz"><span>Geschoss</span><select class="w-geschoss"><option value="">alle Geschosse</option>${T.geschossOptionen(gs).map((o) => `<option value="${esc(o.wert)}">${esc(o.text)}</option>`).join('')}</select></label>
    <div class="w-vorschau"></div>
    <p class="huelle-regel">${esc(T.hinweisFuerMenschen(T.WB_PSI_SATZ))}</p></div>`);
  const ziel = w.querySelector('.w-vorschau');
  let nr = 0, bereit = false;
  const param = () => T.waermebrueckenParameter(feldWert(w, '.w-geschoss'));
  const zeigeLauf = (r, titel) => {
    const z = T.waermebrueckenZaehlen(r.aenderungen);
    ziel.innerHTML = r.angenommen
      ? `<h4>${esc(titel)}</h4><p><b>${z.neu} neu · ${z.geaendert} nachgeführt</b></p>${T.laufHtml(r)}${z.neu + z.geaendert > 0 ? '' : ''}`
      : T.laufHtml(r);
  };
  async function vorschau() {
    const mein = ++nr; bereit = false;
    ziel.innerHTML = spinner('Der Kern rechnet die Raumgrenzen (Trockenlauf – nichts wird geändert) …');
    const r = await sende('waermebruecken_vorschlagen', param(), { trockenlauf: true });
    if (mein !== nr) return;
    bereit = !!r.angenommen;
    zeigeLauf(r, 'Vorschau (Trockenlauf – nichts wurde geändert)');
  }
  w.addEventListener('change', vorschau);
  await dialog({
    titel: 'Wärmebrücken vorschlagen', inhalt: w, breite: 720, klasse: 'dlg-hoch huelle',
    beiOeffnen: () => vorschau(),
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Ausführen', primaer: true, schliesst: false,
        pruefen: () => (bereit ? true : 'Die Vorschau ist noch nicht da oder wurde abgelehnt.'),
        async klick(ctrl) {
          ++nr; bereit = false;
          const r = await sende('waermebruecken_vorschlagen', param());
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          zeigeLauf(r, 'Ausgeführt');
          meldung('Wärmebrücken vorgeschlagen (ohne ψ).', 'ok');
        } }],
  });
  nr++;
}

// ================================================================================================ Listen (raumgrenzen, huellbauteile)

async function listeOeffnen(name) {
  const def = T.LISTEN[name];
  meldung(`${def.titel} werden gerechnet …`, 'info');
  const d = await frage(def.abfrage);
  if (d.__fehler) { meldung(d.__fehler, 'fehler', { vorschlag: d.__vorschlag }); return; }
  if (T.ansichtZeigtZeilen(name, d, aufbereiten)) {
    // Die Listenansicht zeigt diese Ableitung: vorhandene Ansicht öffnen oder anlegen
    let a = derKlasse('Ansicht').find((x) => x.werte?.art === 'liste' && x.werte?.liste === name);
    if (!a) {
      const kz = freiesKennzeichen(def.kz);
      const r = await sende('element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name: def.titel, eigenschaften: { art: 'liste', liste: name } });
      if (!r.angenommen) { meldung(r.fehler || r.meldung, 'warnung', { vorschlag: r.vorschlag }); return; }
      a = await aufElement(kz);
    }
    if (a) await ansichtOeffnen(a.kennzeichen);
    return;
  }
  // Ersatz: die Listenansicht kennt diese Ableitung (noch) nicht – die Abfrage in einer Tabelle zeigen
  const w = knoten(`<div class="huelle-dlg"><p class="leise">Die Listenansicht zeigt diese Ableitung noch nicht vollständig; hier steht die Ausgabe der Abfrage «${esc(def.abfrage)}».</p><div class="huelle-scroll">${T.ableitungTabelleHtml(name, d)}</div></div>`);
  let ctrlRef = null;
  w.addEventListener('click', (e) => {
    const tr = e.target.closest('.huelle-ableitung tbody tr');
    if (tr?.dataset.kz && springen(tr.dataset.kz)) ctrlRef?.schliessen(true);
  });
  await dialog({ titel: def.titel, inhalt: w, breite: 1100, klasse: 'dlg-hoch huelle', beiOeffnen: (c) => { ctrlRef = c; }, knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

// ================================================================================================ Aktionen

const brauchtGeschoss = () => (derKlasse('Geschoss').length > 0 ? '' : 'Im Modell gibt es noch kein Geschoss.');
for (const [art, def] of Object.entries(T.DECKE_ARTEN)) {
  registrieren({
    id: def.id, titel: def.titel, symbol: def.symbol, kuerzel: null, register: 'architektur', gruppe: 'Decke, Boden, Dach',
    aktiv: () => projektOffen() && derKlasse('Geschoss').length > 0, hinweis: () => (projektOffen() ? brauchtGeschoss() : KEIN_PROJEKT),
    ausfuehren: () => deckeDialog(art),
  });
}

const aktivProjekt = { aktiv: projektOffen, hinweis: KEIN_PROJEKT };
registrieren({ id: 'huelle.aufbau', titel: 'Aufbau …', symbol: 'aufbau', kuerzel: null, register: 'huelle', gruppe: 'Aufbauten', ...aktivProjekt, ausfuehren: aufbauEditor });
registrieren({
  id: 'huelle.zuweisen', titel: 'Zuweisen …', symbol: 'zuweisen', kuerzel: null, register: 'huelle', gruppe: 'Aufbauten',
  aktiv: () => projektOffen() && derKlasse('Aufbau').length > 0, hinweis: () => (projektOffen() ? 'Zuerst einen Aufbau anlegen.' : KEIN_PROJEKT), ausfuehren: zuweisenDialog,
});
registrieren({ id: 'huelle.material', titel: 'Material …', symbol: 'material', kuerzel: null, register: 'huelle', gruppe: 'Materialien', ...aktivProjekt, ausfuehren: materialDialog });
const fensterGewaehlt = () => { const a = T.fensterAuswahl(auswahlElemente()); return a.Fenster.length + a.Tuer.length > 0; };
registrieren({
  id: 'huelle.fenster', titel: 'Kennwerte …', symbol: 'fensterKennwerte', kuerzel: null, register: 'huelle', gruppe: 'Fenster',
  aktiv: () => projektOffen() && fensterGewaehlt(), hinweis: () => (projektOffen() ? 'Zuerst Fenster oder Türen wählen (Strg+Klick für mehrere).' : KEIN_PROJEKT), ausfuehren: fensterDialog,
});
registrieren({ id: 'huelle.uebergang', titel: 'Übergang …', symbol: 'uebergang', kuerzel: null, register: 'huelle', gruppe: 'Übergang', ...aktivProjekt, ausfuehren: uebergangDialog });
registrieren({ id: 'huelle.pruefen', titel: 'Hülle prüfen', symbol: 'huellePruefen', kuerzel: null, register: 'huelle', gruppe: 'Prüfen', ...aktivProjekt, ausfuehren: pruefenDialog });
registrieren({ id: 'huelle.waermebruecken', titel: 'Wärmebrücken vorschlagen', symbol: 'waermebruecke', kuerzel: null, register: 'huelle', gruppe: 'Prüfen', ...aktivProjekt, ausfuehren: waermebrueckenDialog });
registrieren({ id: 'huelle.raumgrenzen', titel: 'Raumgrenzen', symbol: 'raumgrenzen', kuerzel: null, register: 'huelle', gruppe: 'Listen', ...aktivProjekt, ausfuehren: () => listeOeffnen('raumgrenzen') });
registrieren({ id: 'huelle.huellbauteile', titel: 'Hüllbauteile', symbol: 'huellbauteile', kuerzel: null, register: 'huelle', gruppe: 'Listen', ...aktivProjekt, ausfuehren: () => listeOeffnen('huellbauteile') });

export { deckeDialog, materialDialog, aufbauEditor, zuweisenDialog, fensterDialog, uebergangDialog, pruefenDialog, waermebrueckenDialog, listeOeffnen };
