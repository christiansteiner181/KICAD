// Dialoge des Registers «Koordination» (Modul 3b, Auftrag C1): Projektvorgaben, Kollisionen prüfen, Aussparungen vorschlagen / nummerieren / abgleichen,
// Aussparungsplan anlegen, IFC für den Architekten, BCF exportieren und importieren. Alle Dialoge sind Hüllen um Befehle und Abfragen des Kerns
// (vorgaben_setzen, kollision_pruefen, aussparungen_vorschlagen, aussparungen_nummerieren, aussparungen_abgleichen, aussparungen_ifc, bcf_export, bcf_lesen,
// bcf_import; element_anlegen, planblatt_anlegen, ansicht_platzieren für den Plan). Läufe zeigen zuerst die Vorschau des Kerns (Trockenlauf), dann wird ausgeführt.
// Projektvorgaben gehen immer mit ihrer Quelle; die Oberfläche kennt keine Vorgabewerte. Texte, HTML, Prüfungen und Parameter: koordination-text.js.
// Nachtrag C1c: Dialog «Typenliste» (Abfrage typmaterial, Befehl typmaterial_setzen mit Trockenlauf-Vorschau); nach einer Änderung öffnen Knöpfe die Dialoge «Aussparungen vorschlagen» und «Kollisionen prüfen».

import { zustand, element, derKlasse, merken } from '../zustand.js';
import { abfrage } from '../api.js';
import * as fenster from '../fenster.js';
import { zeigen, meldung } from './dialoge.js';
import { sende } from './befehle.js';
import { knoten, entprellen, aufElement, gewerkeListe } from './werkzeug.js';
import { panelOeffnen, panelNeuLaden, panelOffen, gewaehlteKollisionen, letztePaareMerken } from './koordination-panel.js';
import * as T from './koordination-text.js';

const esc = T.esc;
const klon = (x) => JSON.parse(JSON.stringify(x));
const spinner = (t) => `<div class="lauf"><span class="spinner" aria-hidden="true"></span><span class="lauf-text">${esc(t)}</span></div>`;
const geschosse = () => derKlasse('Geschoss').sort((a, b) => (a.werte?.kote ?? 0) - (b.werte?.kote ?? 0));
const wert = (w, s) => w.querySelector(s)?.value ?? '';

/** Abfrage ohne Ausnahme: bei Fehler { __fehler, __vorschlag }. */
async function frage(name, p = {}) {
  try { return await abfrage(name, p); } catch (e) { console.error(name, e); return { __fehler: e?.message ?? String(e), __vorschlag: e?.vorschlag ?? '' }; }
}
const fehlerKasten = (a) => `<div class="fehlerkasten" role="alert"><b>${esc(T.fuerMenschen(a.__fehler))}</b>${a.__vorschlag ? `<div>Vorschlag: ${esc(T.fuerMenschen(a.__vorschlag))}</div>` : ''}</div>`;

/** Dialog wie zeigen(); ein Fehlertext verschwindet, sobald man etwas ändert. */
function dialog(opt) {
  const vorher = opt.beiOeffnen;
  return zeigen({ ...opt, klasse: ('koord ' + (opt.klasse ?? '')).trim(), beiOeffnen: (ctrl) => { const f = () => ctrl.fehler(null); ctrl.koerper.addEventListener('input', f); ctrl.koerper.addEventListener('change', f); vorher?.(ctrl); } });
}

const projektName = () => zustand.status?.projekt ?? '';

// ================================================================================================ Projektvorgaben

export async function vorgabenDialog() {
  let daten = await frage('vorgaben');
  if (daten.__fehler) { meldung(daten.__fehler, 'fehler', { vorschlag: daten.__vorschlag }); return; }
  let form = T.vorgabenFormular(daten), bisher = klon(form);
  const w = knoten('<div class="huelle-dlg kp-dlg"><div class="v-inhalt"></div><h3>Vorschau des Kerns</h3><div class="v-vorschau"><p class="leise">Noch nichts geändert.</p></div></div>');
  const q = (s) => w.querySelector(s);
  let abstandOffen = false;   // der Bereich «Abstandsprüfung» ist beim Öffnen eingeklappt und bleibt es, bis man ihn aufklappt (auch nach dem Neuzeichnen)
  const zeichnen = () => { q('.v-inhalt').innerHTML = T.vorgabenHtml(daten, form, { abstandOffen }); };
  zeichnen();
  w.addEventListener('toggle', (e) => { if (e.target.matches?.('details.kp-abstand')) abstandOffen = e.target.open; }, true);

  let nr = 0;
  async function vorschau() {
    const mein = ++nr;
    const a = T.vorgabenAuswertung(form, bisher);
    const ziel = q('.v-vorschau');
    if (a.fehler.length) { ziel.innerHTML = `<p class="leise">Eingabe noch nicht vollständig:</p><ul class="hinweisliste">${a.fehler.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`; return; }
    if (Object.keys(a.werte).length === 0) { ziel.innerHTML = '<p class="leise">Noch nichts geändert.</p>'; return; }
    ziel.innerHTML = spinner('Der Kern prüft (Trockenlauf – nichts wird geändert) …');
    const r = await sende('vorgaben_setzen', { werte: a.werte }, { trockenlauf: true });
    if (mein !== nr) return;
    ziel.innerHTML = T.vorgabenVorschauHtml(r, a);
  }
  const plan = entprellen(vorschau, 350);

  w.addEventListener('input', (e) => {
    const t = e.target;
    if (t.matches('.v-wert')) form.felder[t.closest('tr').dataset.name].wert = t.value;
    else if (t.matches('.v-quelle')) form.felder[t.closest('tr').dataset.name].quelle = t.value;
    else if (t.matches('.v-weich-quelle')) form.weich.quelle = t.value;
    else if (t.matches('.w-wert')) form.weich.zeilen[Number(t.closest('tr').dataset.i)].wert = t.value;
    else return;
    plan();
  });
  w.addEventListener('change', (e) => {
    const t = e.target;
    if (t.matches('.w-a')) form.weich.zeilen[Number(t.closest('tr').dataset.i)].a = t.value;
    else if (t.matches('.w-b')) form.weich.zeilen[Number(t.closest('tr').dataset.i)].b = t.value;
    else return;
    plan();
  });
  w.addEventListener('click', (e) => {
    const alt = e.target.closest('.v-alt-weg, .v-alt-behalten');
    if (alt) { form.alt[alt.closest('[data-alt]').dataset.alt].entfernen = alt.matches('.v-alt-weg'); zeichnen(); plan(); return; }
    if (e.target.closest('.w-weg')) { form.weich.zeilen.splice(Number(e.target.closest('tr').dataset.i), 1); zeichnen(); plan(); return; }
    if (e.target.closest('.v-weich-neu')) { form.weich.zeilen.push({ a: '', b: '', wert: '' }); zeichnen(); return; }
    if (e.target.closest('.v-quelle-alle')) { if (T.quelleUebernehmen(form) > 0) { zeichnen(); plan(); } }
  });

  await dialog({
    titel: 'Projektvorgaben', inhalt: w, breite: 1000, klasse: 'dlg-hoch huelle',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Speichern', primaer: true, schliesst: false,
        pruefen: () => {
          const a = T.vorgabenAuswertung(form, bisher);
          if (a.fehler.length) return a.fehler.join(' ');
          return Object.keys(a.werte).length > 0 ? true : 'Nichts geändert.';
        },
        async klick(ctrl) {
          const a = T.vorgabenAuswertung(form, bisher);
          const r = await sende('vorgaben_setzen', { werte: a.werte });
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          ++nr;
          const neu = await frage('vorgaben');
          if (!neu.__fehler) { daten = neu; form = T.vorgabenFormular(daten); bisher = klon(form); zeichnen(); }
          q('.v-vorschau').innerHTML = T.laufHtml(r, { titel: 'Gespeichert' });
          meldung('Projektvorgaben gespeichert.', 'ok');
        } }],
  });
  nr++;
}

// ================================================================================================ Kollisionen prüfen

export async function pruefenDialog() {
  const gs = geschosse();
  const ifcs = T.ifcUnterlagen(derKlasse('Unterlage'));
  let vorgaben = await frage('vorgaben');
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <h3>Paare</h3>
    ${T.PAARE.map((p) => `<label class="haken kp-paar"><input type="checkbox" class="p-paar" value="${p.id}"${T.PAARE_VORGABE.includes(p.id) ? ' checked' : ''}> <b>${esc(p.titel)}</b> <span class="leise">${esc(p.hinweis)}</span></label>`).join('')}
    <h3>Umfang</h3>
    <label class="fz"><span>Geschoss</span><select class="p-geschoss">${T.geschossOptionenHtml(gs, '', '(alle Geschosse)')}</select></label>
    <fieldset class="f-unterlagen"><legend>IFC-Unterlagen als Architektur bzw. Fremd</legend>${ifcs.length === 0 ? '<p class="leise">Keine IFC-Unterlage im Projekt – es zählen nur die in KICAD gezeichneten Bauteile.</p>'
      : `<div class="spalten">${ifcs.map((u) => `<label class="haken"><input type="checkbox" class="p-ul" value="${esc(u.kennzeichen)}"> ${esc(u.kennzeichen)} ${esc(u.name ?? '')}</label>`).join('')}</div><p class="leise">Nichts angekreuzt = alle. In KICAD gezeichnete Bauteile zählen immer.</p>`}</fieldset>
    <label class="haken"><input type="checkbox" class="p-weich"> Weiche Kollisionen prüfen (Abstandsprüfung: Abstände aus den Projektvorgaben; Paare ohne Vorgabe entfallen und werden genannt)</label>
    <div class="p-weich-hinweis hkap-kasten" hidden></div>
    <div class="p-vorgaben"></div>
    <div class="p-ergebnis"><p class="leise">«Vorschau» rechnet den Lauf im Kern, ohne etwas zu ändern (bei grossen Modellen einige Sekunden bis zu einer halben Minute).</p></div></div>`);
  const q = (s) => w.querySelector(s);
  // weiche Prüfung: aus, solange keine Abstände gesetzt sind (Entscheid Christian 19.10.2026); hat der Mensch das Häkchen angefasst, bleibt seine Wahl
  let weichAngefasst = false;
  const vorgabenZeigen = () => {
    if (!weichAngefasst && !vorgaben.__fehler) q('.p-weich').checked = T.weichVorgabe(vorgaben);
    const ohneAbstaende = !vorgaben.__fehler && !T.abstaendeGesetzt(vorgaben);
    const hinweis = q('.p-weich-hinweis');
    hinweis.hidden = !ohneAbstaende;
    hinweis.textContent = ohneAbstaende ? T.WEICH_HINWEIS_OHNE : '';
    q('.p-vorgaben').innerHTML = vorgaben.__fehler ? '' : T.fehlendeHtml(T.fehlendeFuerPruefung(vorgaben, { weich: q('.p-weich').checked }));
  };
  q('.p-weich').addEventListener('change', () => { weichAngefasst = true; vorgabenZeigen(); });
  vorgabenZeigen();
  const feld = () => ({
    paare: [...w.querySelectorAll('.p-paar:checked')].map((c) => c.value), unterlagen: [...w.querySelectorAll('.p-ul:checked')].map((c) => c.value),
    alleUnterlagen: ifcs.map((u) => u.kennzeichen), geschoss: wert(w, '.p-geschoss'), weich: q('.p-weich').checked,
  });
  let laeuft = false;
  async function lauf(ctrl, trocken) {
    const f = feld();
    const pr = T.pruefenPruefen(f);
    if (pr !== true) { ctrl.fehler(pr); return; }
    if (laeuft) return;
    laeuft = true;
    q('.p-ergebnis').innerHTML = spinner(trocken ? 'Der Kern rechnet die Vorschau (Trockenlauf – nichts wird geändert) …' : 'Der Kern prüft …');
    try {
      const r = await sende('kollision_pruefen', T.pruefenParameter(f), { trockenlauf: trocken });
      q('.p-ergebnis').innerHTML = T.laufHtml(r, { trocken });
      if (!trocken && r.angenommen) {
        meldung('Prüflauf fertig.', 'ok');
        letztePaareMerken(f.paare);   // die Kollisionsliste blendet Paare aus, die der letzte Lauf nicht geprüft hat
        ctrl.fuss.querySelector('.kp-zur-liste').hidden = false;
        panelNeuLaden();
      }
    } finally { laeuft = false; }
  }

  await dialog({
    titel: 'Kollisionen prüfen', inhalt: w, breite: 760, klasse: 'huelle',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Projektvorgaben …', schliesst: false, async klick() { await vorgabenDialog(); vorgaben = await frage('vorgaben'); vorgabenZeigen(); } },
      { text: 'Kollisionsliste', schliesst: true, klick() { panelOeffnen('kollisionen'); } },
      { text: 'Vorschau (Trockenlauf)', schliesst: false, klick: (ctrl) => lauf(ctrl, true) },
      { text: 'Prüflauf ausführen', primaer: true, schliesst: false, klick: (ctrl) => lauf(ctrl, false) }],
    beiOeffnen: (ctrl) => { const b = [...ctrl.fuss.querySelectorAll('button')].find((k) => k.textContent === 'Kollisionsliste'); b.classList.add('kp-zur-liste'); b.hidden = true; },
  });
}

// ================================================================================================ Aussparungen vorschlagen / nummerieren

export async function vorschlagenDialog() {
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <p class="dlg-text">Schlägt <b>rechteckige Aussparungen</b> vor, wo Leitungen (mit Dämmung) Wände, Decken, Dächer, Stützen oder Träger queren (auch für runde Rohre). Grösse und Zusammenfassen
      richten sich nach den <b>Projektvorgaben</b> (Spiel, Abstand zum Zusammenfassen); bestehende Vorschläge werden nachgeführt, freigegebene nicht verändert.
      Eine <b>Kernbohrung</b> (rund) entsteht nur, wenn du sie für eine Aussparung wählst: Aussparungsliste › Zeile wählen › «Als Kernbohrung».</p>
    <label class="fz"><span>Geschoss</span><select class="v-geschoss">${T.geschossOptionenHtml(geschosse(), '', '(alle Geschosse)')}</select></label>
    <details class="a-u"><summary>Einschränken auf bestimmte Leitungen oder Bauteile</summary>
      <label class="fz"><span>Leitungen</span><input type="text" class="v-leitungen" placeholder="Kennzeichen, durch Komma getrennt" data-kein-enter="1"></label>
      <label class="fz"><span>Bauteile</span><input type="text" class="v-bauteile" placeholder="Kennzeichen oder Unterlage:GlobalId" data-kein-enter="1"></label>
      <p class="leise">Ein Bauteil wird immer ganz behandelt; mit Leitungen werden die Bauteile behandelt, die diese Leitungen queren.</p></details>
    <div class="v-ergebnis"></div></div>`);
  const q = (s) => w.querySelector(s);
  const feld = () => ({ geschoss: wert(w, '.v-geschoss'), leitungen: wert(w, '.v-leitungen'), bauteile: wert(w, '.v-bauteile') });
  let nr = 0, gemacht = false;
  async function vorschau() {
    const mein = ++nr;
    gemacht = false;
    q('.v-ergebnis').innerHTML = spinner('Der Kern rechnet die Vorschläge (Trockenlauf – nichts wird geändert) …');
    const r = await sende('aussparungen_vorschlagen', T.vorschlagenParameter(feld()), { trockenlauf: true });
    if (mein !== nr) return;
    q('.v-ergebnis').innerHTML = T.laufHtml(r, { trocken: true });
  }
  const plan = entprellen(vorschau, 450);
  w.addEventListener('input', plan);
  w.addEventListener('change', plan);

  await dialog({
    titel: 'Aussparungen vorschlagen', inhalt: w, breite: 820, klasse: 'huelle dlg-hoch',
    beiOeffnen: (ctrl) => { const b = [...ctrl.fuss.querySelectorAll('button')].find((k) => k.textContent === 'Aussparungsliste'); b.classList.add('kp-zur-liste'); b.hidden = true; plan(); },
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Projektvorgaben …', schliesst: false, async klick() { await vorgabenDialog(); plan(); } },
      { text: 'Aussparungsliste', schliesst: true, klick() { panelOeffnen('aussparungen'); } },
      { text: 'Vorschläge anlegen', primaer: true, schliesst: false,
        pruefen: () => (gemacht ? 'Schon angelegt. Für weitere Vorschläge Geschoss oder Auswahl ändern.' : true),
        async klick(ctrl) {
          const r = await sende('aussparungen_vorschlagen', T.vorschlagenParameter(feld()));
          ++nr;
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          gemacht = true;
          q('.v-ergebnis').innerHTML = T.laufHtml(r);
          ctrl.fuss.querySelector('.kp-zur-liste').hidden = false;
          meldung(r.hinweise?.[0] ?? 'Aussparungen vorgeschlagen.', 'ok');
          panelNeuLaden();
        } }],
  });
  nr++;
}

export async function nummerierenDialog() {
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <p class="dlg-text">Vergibt Nummern «A-&lt;Geschoss&gt;-001» je Geschoss in stabiler Reihenfolge (nach x, y, z der Mitte). Bestehende Nummern bleiben; nur Aussparungen ohne Nummer bekommen die nächsten.</p>
    <label class="fz"><span>Geschoss</span><select class="n-geschoss">${T.geschossOptionenHtml(geschosse(), '', '(alle Geschosse)')}</select></label>
    <label class="haken"><input type="checkbox" class="n-alle"> Alle neu nummerieren (auch die mit Nummer)</label>
    <div class="hkap-kasten n-warn" hidden>Neu nummerieren ändert Nummern, die schon beim Architekten sein können. Nur vor dem ersten Versand sinnvoll.</div>
    <div class="n-ergebnis"></div></div>`);
  const q = (s) => w.querySelector(s);
  const feld = () => ({ geschoss: wert(w, '.n-geschoss'), alle: q('.n-alle').checked });
  let nr = 0, gemacht = false;
  async function vorschau() {
    const mein = ++nr;
    gemacht = false;
    q('.n-warn').hidden = !q('.n-alle').checked;
    q('.n-ergebnis').innerHTML = spinner('Der Kern rechnet (Trockenlauf – nichts wird geändert) …');
    const r = await sende('aussparungen_nummerieren', T.nummerierenParameter(feld()), { trockenlauf: true });
    if (mein !== nr) return;
    q('.n-ergebnis').innerHTML = T.laufHtml(r, { trocken: true });
  }
  const plan = entprellen(vorschau, 300);
  w.addEventListener('change', plan);
  await dialog({
    titel: 'Aussparungen nummerieren', inhalt: w, breite: 640, klasse: 'huelle', beiOeffnen: () => plan(),
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Nummerieren', primaer: true, schliesst: false,
        pruefen: () => (gemacht ? 'Schon nummeriert.' : true),
        async klick(ctrl) {
          const r = await sende('aussparungen_nummerieren', T.nummerierenParameter(feld()));
          ++nr;
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          gemacht = true;
          q('.n-ergebnis').innerHTML = T.laufHtml(r);
          meldung(r.hinweise?.[0] ?? 'Nummeriert.', 'ok');
          panelNeuLaden();
        } }],
  });
  nr++;
}

// ================================================================================================ Abgleich mit der neuen Architekten-Fassung

export async function abgleichDialog() {
  const ifcs = T.ifcUnterlagen(derKlasse('Unterlage'));
  if (ifcs.length === 0) { meldung('Es gibt keine IFC-Unterlage im Projekt.', 'info', { vorschlag: 'Zuerst die neue Fassung des Architekten übernehmen (Register Einfügen › IFC übernehmen).' }); return; }
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <p class="dlg-text">Nach dem Import einer neuen Architekten-Fassung (Register Einfügen › IFC übernehmen, dieselbe Datei) prüft der Kern, ob die freigegebenen, versandten und fehlenden Aussparungen
      angekommen sind: Liegt im Bauteil eine Öffnung, die sie abdeckt (oder ist das Bauteil an der Stelle offen), steht die Aussparung auf <b>angekommen</b>, sonst auf <b>fehlt</b>.</p>
    <label class="fz"><span>IFC-Unterlage</span><select class="g-unterlage">${ifcs.map((u) => `<option value="${esc(u.kennzeichen)}">${esc(u.kennzeichen)} ${esc(u.name ?? '')}${u.werte?.stand ? ' · ' + esc(u.werte.stand) : ''}</option>`).join('')}</select></label>
    <div class="g-ergebnis"></div></div>`);
  const q = (s) => w.querySelector(s);
  let nr = 0;
  async function vorschau() {
    const mein = ++nr;
    q('.g-ergebnis').innerHTML = spinner('Der Kern gleicht ab (Trockenlauf – nichts wird geändert) …');
    const r = await sende('aussparungen_abgleichen', T.abgleichParameter(wert(w, '.g-unterlage')), { trockenlauf: true });
    if (mein !== nr) return;
    q('.g-ergebnis').innerHTML = T.laufHtml(r, { trocken: true });
  }
  const plan = entprellen(vorschau, 300);
  w.addEventListener('change', plan);
  await dialog({
    titel: 'Abgleich mit der Architekten-Fassung', inhalt: w, breite: 760, klasse: 'huelle dlg-hoch', beiOeffnen: () => plan(),
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Abgleichen', primaer: true, schliesst: false,
        pruefen: () => T.abgleichPruefen(wert(w, '.g-unterlage')),
        async klick(ctrl) {
          const r = await sende('aussparungen_abgleichen', T.abgleichParameter(wert(w, '.g-unterlage')));
          ++nr;
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          q('.g-ergebnis').innerHTML = T.laufHtml(r);
          meldung(r.hinweise?.[0] ?? 'Abgeglichen.', 'ok');
          panelNeuLaden();
        } }],
  });
  nr++;
}

// ================================================================================================ Aussparungsplan

/** Legt je gewähltem Geschoss einen Grundriss mit Darstellung «aussparungen» an (vorhandene werden weiterverwendet), auf Wunsch mit Planblatt. */
export async function planDialog() {
  const gs = geschosse();
  if (gs.length === 0) { meldung('Im Modell gibt es noch kein Geschoss.', 'warnung', { vorschlag: 'Zuerst ein Geschoss anlegen.' }); return; }
  const liste = await frage('aussparungsliste');
  const proGeschoss = new Map();
  for (const z of liste.zeilen ?? []) proGeschoss.set(z.geschoss, (proGeschoss.get(z.geschoss) ?? 0) + 1);
  const aktiv = element(zustand.aktiveAnsicht);
  const aktivGeschoss = aktiv?.klasse === 'Ansicht' ? aktiv.geschoss : null;
  const vorgewaehlt = new Set([...proGeschoss.keys()]);
  if (vorgewaehlt.size === 0 && aktivGeschoss) vorgewaehlt.add(aktivGeschoss);
  const gewerkeVorgabe = Array.isArray(aktiv?.werte?.gewerke) && aktiv.werte.gewerke.length ? aktiv.werte.gewerke : ['sanitaer', 'architektur'];
  const massVorgabe = aktiv?.werte?.art === 'grundriss' && T.PLAN_MASSSTAEBE.includes(aktiv.werte.massstab) ? aktiv.werte.massstab : 50;
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <p class="dlg-text">Legt je Geschoss einen Grundriss mit der Darstellung <b>Aussparungen</b> an (Aussparungen mit Nummer und Koten über den Wänden und Decken); auf Wunsch dazu ein Planblatt mit dieser Ansicht.</p>
    <table class="tab huelle-kennwerte"><thead><tr><th>Geschoss</th><th class="zahl">Aussparungen</th><th>Planblatt</th><th>Plannummer</th><th>Titel</th></tr></thead><tbody>
      ${gs.map((g) => `<tr data-g="${esc(g.kennzeichen)}"><td><label class="haken"><input type="checkbox" class="pl-wahl"${vorgewaehlt.has(g.kennzeichen) ? ' checked' : ''}> ${esc(g.kennzeichen)}${g.name ? ' – ' + esc(g.name) : ''}</label></td>
        <td class="zahl">${proGeschoss.get(g.kennzeichen) ?? 0}</td><td><input type="checkbox" class="pl-blatt" aria-label="Planblatt ${esc(g.kennzeichen)}"></td>
        <td><input type="text" class="pl-nummer" placeholder="z. B. 2317-101" aria-label="Plannummer ${esc(g.kennzeichen)}" data-kein-enter="1"></td>
        <td><input type="text" class="pl-titel" value="Aussparungsplan ${esc(g.kennzeichen)}" aria-label="Titel ${esc(g.kennzeichen)}" data-kein-enter="1"></td></tr>`).join('')}
    </tbody></table>
    <p class="leise">Plannummern vergibst du, KICAD denkt sich keine aus; ein Planblatt entsteht nur mit Häkchen und Nummer. Gibt es den Aussparungsplan eines Geschosses schon, wird er weiterverwendet.</p>
    <label class="fz"><span>Massstab 1 :</span><select class="pl-massstab">${T.PLAN_MASSSTAEBE.map((m) => `<option${m === massVorgabe ? ' selected' : ''}>${m}</option>`).join('')}</select></label>
    <label class="fz"><span>Blattformat</span><select class="pl-format">${T.PLAN_FORMATE.map((f) => `<option${f === 'A3' ? ' selected' : ''}>${f}</option>`).join('')}</select></label>
    <label class="haken"><input type="checkbox" class="pl-hoch"> Hochformat</label>
    <fieldset class="f-gewerke"><legend>Gewerke in der Ansicht</legend><div class="spalten">${gewerkeListe().map(([k, n]) => `<label class="haken"><input type="checkbox" value="${esc(k)}"${gewerkeVorgabe.includes(k) ? ' checked' : ''}> ${esc(n)}</label>`).join('')}</div></fieldset>
    <div class="pl-ergebnis"></div></div>`);
  const q = (s) => w.querySelector(s);
  const zeilen = () => [...w.querySelectorAll('tbody tr')].map((tr) => ({
    geschoss: tr.dataset.g, gewaehlt: tr.querySelector('.pl-wahl').checked, blatt: tr.querySelector('.pl-blatt').checked,
    nummer: tr.querySelector('.pl-nummer').value, titel: tr.querySelector('.pl-titel').value,
  }));
  const vorhandeneNummern = () => derKlasse('Planblatt').map((b) => b.werte?.nummer).filter(Boolean);

  await dialog({
    titel: 'Aussparungsplan anlegen', inhalt: w, breite: 880, klasse: 'huelle dlg-hoch',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Anlegen', primaer: true, schliesst: false,
        pruefen: () => T.planPruefen(zeilen(), vorhandeneNummern()),
        async klick(ctrl) {
          const gewerke = [...w.querySelectorAll('.f-gewerke input:checked')].map((c) => c.value);
          const protokoll = [];
          let erste = null;
          for (const z of zeilen().filter((x) => x.gewaehlt)) {
            const kz = T.planAnsichtKennzeichen(z.geschoss);
            if (element(kz)) protokoll.push({ ok: true, text: `${kz}: Aussparungsplan gibt es schon – weiterverwendet.` });
            else {
              const r = await sende('element_anlegen', T.planAnsichtParameter({ geschoss: z.geschoss, massstab: wert(w, '.pl-massstab'), gewerke }));
              if (!r.angenommen) { protokoll.push({ ok: false, text: `${kz}: ${T.ablehnung(r)}` }); continue; }
              await aufElement(kz);
              protokoll.push({ ok: true, text: `${kz}: Grundriss mit Darstellung «aussparungen» angelegt.` });
            }
            erste ??= kz;
            if (z.blatt) {
              const rb = await sende('planblatt_anlegen', T.planblattParameter({ nummer: z.nummer, titel: z.titel, format: wert(w, '.pl-format'), hochformat: q('.pl-hoch').checked }));
              if (!rb.angenommen) { protokoll.push({ ok: false, text: `Planblatt ${z.nummer}: ${T.ablehnung(rb)}` }); continue; }
              const neu = (rb.aenderungen ?? []).find((a) => a.klasse === 'Planblatt' && a.art === 'angelegt');
              if (neu?.kennzeichen) await aufElement(neu.kennzeichen);
              const rp = await sende('ansicht_platzieren', { planblatt: neu?.kennzeichen ?? z.nummer, ansicht: kz });
              protokoll.push(rp.angenommen ? { ok: true, text: `Planblatt ${z.nummer} «${z.titel}» angelegt, ${kz} platziert.` } : { ok: false, text: `Planblatt ${z.nummer} angelegt, aber ${kz} nicht platziert: ${T.ablehnung(rp)}` });
            }
          }
          q('.pl-ergebnis').innerHTML = `<h4 class="kp-ergebnis">Ergebnis</h4><ul class="hinweisliste">${protokoll.map((p) => `<li class="${p.ok ? '' : 'rot'}">${esc(p.text)}</li>`).join('')}</ul>`;
          if (protokoll.some((p) => !p.ok)) { ctrl.fehler('Nicht alles ist gelungen – siehe Ergebnis.'); return; }
          meldung('Aussparungsplan angelegt.', 'ok');
          if (erste) await fenster.ansichtOeffnen(erste);
        } }],
  });
}

// ================================================================================================ IFC für den Architekten

export async function ifcDialog(faehig) {
  if (!faehig?.vorhanden) { meldung(T.IFC_FEHLT_HINWEIS, 'info'); return; }
  const gs = geschosse();
  const hat = (n) => faehig.parameter.size === 0 || faehig.parameter.has(n);
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <p class="dlg-text">Schreibt die Aussparungen als IFC-Datei für den Architekten (Platzhalter für Aussparungen, <i>ProvisionForVoid</i>). Der Kern prüft die Datei und meldet, was er geschrieben hat.</p>
    <label class="fz"><span>Datei *</span><input type="text" class="i-datei" placeholder="C:\\Temp\\Aussparungen.ifc" value="${esc(merken.lesen('aussparungenIfcDatei', ''))}" data-kein-enter="1"></label>
    <label class="fz"><span>IFC-Fassung</span><select class="i-fassung">${T.IFC_FASSUNGEN.map((f) => `<option>${f}</option>`).join('')}</select></label>
    ${hat('geschoss') ? `<label class="fz"><span>Geschoss</span><select class="i-geschoss">${T.geschossOptionenHtml(gs, '', '(alle Geschosse)')}</select></label>` : ''}
    ${hat('status') ? `<fieldset class="f-status"><legend>Nur Aussparungen mit Status</legend><div class="spalten">${T.AUSSPARUNG_STATUS.map((s) => `<label class="haken"><input type="checkbox" value="${s}"> ${esc(T.statusText(s))}</label>`).join('')}</div><p class="leise">Nichts angekreuzt = freigegebene und versandte (Vorgabe des Kerns: was beim Architekten ankommen soll).</p></fieldset>` : ''}
    ${hat('koordinaten') ? `<label class="fz"><span>Koordinaten</span><select class="i-koord">${T.koordinatenOptionenHtml('')}</select></label>` : ''}
    <div class="i-ergebnis"></div></div>`);
  const q = (s) => w.querySelector(s);
  const feld = () => ({ datei: wert(w, '.i-datei'), fassung: wert(w, '.i-fassung'), geschoss: wert(w, '.i-geschoss'), status: [...w.querySelectorAll('.f-status input:checked')].map((c) => c.value), koordinaten: wert(w, '.i-koord') });
  await dialog({
    titel: 'IFC für den Architekten', inhalt: w, breite: 700, klasse: 'huelle',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'IFC schreiben', primaer: true, schliesst: false,
        pruefen: () => T.dateiPfadPruefen(wert(w, '.i-datei'), ['.ifc'], 'Datei'),
        async klick(ctrl) {
          const f = feld();
          merken.schreiben('aussparungenIfcDatei', f.datei.trim());
          q('.i-ergebnis').innerHTML = spinner('Der Kern schreibt die IFC-Datei …');
          const r = await frage('aussparungen_ifc', T.ifcParameter(f, faehig));
          if (r.__fehler) { q('.i-ergebnis').innerHTML = ''; ctrl.fehler(`${r.__fehler}${r.__vorschlag ? ' – ' + r.__vorschlag : ''}`); return; }
          q('.i-ergebnis').innerHTML = T.ifcExportHtml(r);
          meldung('IFC geschrieben.', 'ok');
        } }],
  });
}

// ================================================================================================ BCF

const bcfErhalten = (k, fallback = '') => merken.lesen(k, fallback);

export async function bcfExportDialog() {
  const gewaehlt = panelOffen() ? gewaehlteKollisionen() : [];
  const vorschlag = (() => { const alt = String(bcfErhalten('bcfExportDatei', '')); const ordner = alt.replace(/[\\/][^\\/]*$/, ''); return ordner ? `${ordner}\\${T.bcfDateiname(projektName())}` : alt; })();
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <p class="dlg-text">Schreibt die Kollisionen als <b>BCF 3.0</b> für den Architekten oder Fachplaner (je Kollision ein Topic mit Viewpoint). Der Export ändert das Modell nicht; eine vorhandene Datei wird ersetzt.</p>
    <label class="fz"><span>Datei *</span><input type="text" class="b-datei" placeholder="C:\\Temp\\Kollisionen.bcf" value="${esc(vorschlag)}" data-kein-enter="1"></label>
    ${gewaehlt.length ? `<label class="haken"><input type="checkbox" class="b-gewaehlt" checked> Nur die ${gewaehlt.length} in der Kollisionsliste gewählten Kollisionen</label>` : ''}
    <fieldset class="f-status"><legend>Status</legend><div class="spalten">${T.KOLLISION_STATUS.map((s) => `<label class="haken"><input type="checkbox" value="${s}" checked> ${esc(T.statusText(s))}</label>`).join('')}</div></fieldset>
    <label class="fz"><span>Art</span><select class="b-art"><option value="">hart und weich</option>${T.KOLLISION_ARTEN.map((a) => `<option>${a}</option>`).join('')}</select></label>
    <label class="fz"><span>Autor</span><input type="text" class="b-autor" placeholder="leer = der Bearbeiter, der die Kollision angelegt bzw. geändert hat" data-kein-enter="1"></label>
    <label class="fz"><span>Koordinaten</span><select class="b-koord">${T.koordinatenOptionenHtml('')}</select></label>
    <p class="b-zahl leise"></p>
    <div class="b-ergebnis"></div></div>`);
  const q = (s) => w.querySelector(s);
  const feld = () => ({
    datei: wert(w, '.b-datei'), kollisionen: q('.b-gewaehlt')?.checked ? gewaehlt : [],
    status: [...w.querySelectorAll('.f-status input:checked')].map((c) => c.value), art: wert(w, '.b-art'), autor: wert(w, '.b-autor'), koordinaten: wert(w, '.b-koord'),
  });
  let nr = 0;
  async function zahl() {
    const mein = ++nr;
    const f = feld();
    if (f.kollisionen.length) { q('.b-zahl').textContent = T.bcfExportVorschau(null, f.kollisionen.length); return; }
    if (f.status.length === 0) { q('.b-zahl').textContent = 'Keinen Status gewählt – nichts zu exportieren.'; return; }
    const p = {};
    if (f.status.length < T.KOLLISION_STATUS.length) p.status = f.status.join(',');
    if (f.art) p.art = f.art;
    const d = await frage('kollisionen', p);
    if (mein !== nr) return;
    q('.b-zahl').textContent = d.__fehler ? d.__fehler : T.bcfExportVorschau(d, 0);
  }
  const plan = entprellen(zahl, 250);
  w.addEventListener('change', plan);
  await dialog({
    titel: 'BCF exportieren', inhalt: w, breite: 760, klasse: 'huelle', beiOeffnen: () => plan(),
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Exportieren', primaer: true, schliesst: false,
        pruefen: () => (feld().status.length === 0 && !feld().kollisionen.length ? 'Mindestens einen Status wählen.' : T.dateiPfadPruefen(wert(w, '.b-datei'), ['.bcf', '.bcfzip'], 'Datei')),
        async klick(ctrl) {
          const f = feld();
          merken.schreiben('bcfExportDatei', f.datei.trim().replace(/^"|"$/g, ''));
          q('.b-ergebnis').innerHTML = spinner('Der Kern schreibt die BCF-Datei und prüft sie gegen die Schemas …');
          const r = await frage('bcf_export', T.bcfExportParameter(f));
          if (r.__fehler) { q('.b-ergebnis').innerHTML = ''; ctrl.fehler(`${T.fuerMenschen(r.__fehler)}${r.__vorschlag ? ' – ' + T.fuerMenschen(r.__vorschlag) : ''}`); return; }
          q('.b-ergebnis').innerHTML = T.bcfExportHtml(r);
          meldung(`BCF mit ${r.topics} Topic(s) geschrieben.`, 'ok');
        } }],
  });
  nr++;
}

export async function bcfImportDialog() {
  const w = knoten(`<div class="huelle-dlg kp-dlg">
    <p class="dlg-text">Liest Rückmeldungen aus einer BCF-3.0-Datei (z. B. vom Architekten): Topics werden über ihre Guid den Kollisionen zugeordnet, der Status und neue Kommentare werden übernommen.
      Unbekannte Topics werden nur aufgelistet, nicht angelegt.</p>
    <div class="fz kp-lesen"><span>Datei *</span><span class="kp-lesen-feld"><input type="text" class="b-datei" placeholder="C:\\Temp\\Rueckmeldung.bcf" value="${esc(bcfErhalten('bcfImportDatei', ''))}" data-kein-enter="1"> <button type="button" class="dlg-knopf b-lesen">Lesen</button></span></div>
    <div class="b-vorschau"></div></div>`);
  const q = (s) => w.querySelector(s);
  let gelesen = null, nr = 0, gemacht = false;
  async function lesen() {
    const pr = T.dateiPfadPruefen(wert(w, '.b-datei'), ['.bcf', '.bcfzip'], 'Datei');
    if (pr !== true) { q('.b-vorschau').innerHTML = `<div class="fehlerkasten" role="alert"><b>${esc(pr)}</b></div>`; return; }
    const mein = ++nr;
    const datei = wert(w, '.b-datei').trim().replace(/^"|"$/g, '');
    merken.schreiben('bcfImportDatei', datei);
    gelesen = null; gemacht = false;
    q('.b-vorschau').innerHTML = spinner('Der Kern liest die Datei …');
    const r = await frage('bcf_lesen', { datei });
    if (mein !== nr) return;
    if (r.__fehler) { q('.b-vorschau').innerHTML = fehlerKasten(r); return; }
    const probe = await sende('bcf_import', { datei }, { trockenlauf: true });
    if (mein !== nr) return;
    gelesen = { datei, r };
    q('.b-vorschau').innerHTML = `${T.bcfLesenHtml(r)}<h3>Vorschau der Übernahme</h3>${T.laufHtml(probe, { trocken: true, titel: 'Der Kern würde übernehmen (Trockenlauf – nichts wurde geändert)' })}`;
  }
  w.addEventListener('click', (e) => { if (e.target.closest('.b-lesen')) lesen(); });
  w.querySelector('.b-datei').addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); lesen(); } });
  await dialog({
    titel: 'BCF importieren', inhalt: w, breite: 1000, klasse: 'huelle dlg-hoch',
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Übernehmen', primaer: true, schliesst: false,
        pruefen: () => (!gelesen ? 'Zuerst die Datei lesen («Lesen»).' : gemacht ? 'Schon übernommen.' : true),
        async klick(ctrl) {
          const r = await sende('bcf_import', { datei: gelesen.datei });
          if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
          gemacht = true;
          q('.b-vorschau').innerHTML = T.laufHtml(r, { titel: 'Übernommen' });
          meldung(r.hinweise?.[0] ?? 'BCF übernommen.', 'ok');
          panelNeuLaden();
        } }],
  });
  nr++;
}

// ================================================================================================ Typenliste «Typname → Material»

/**
 * Typenliste des Projekts (Nachtrag B3d, Entscheide Christian 19.10.2026): welche Bauteiltypen der Architektur aus welchem Material sind. Dialog statt Palette: die Arbeit braucht Platz (Tabellen mit
 * Auswahl je Zeile, Quelle, Vorschau des Kerns) und folgt dem Muster der Projektvorgaben – Eingabe, Vorschau (Trockenlauf), dann echt. Der Dialog liest nur die Abfrage `typmaterial` und schreibt nur über
 * `typmaterial_setzen`; Kategorien, Wirkung, Zahlen und Hinweise kommen vom Kern. Jede Festlegung geht mit Quelle (Pflicht, nie vorbelegt).
 */
export async function typenDialog() {
  const form = T.typenFormular();
  let daten = await frage('typmaterial', T.typenAbfrageParameter(form));
  if (daten.__fehler) { meldung(daten.__fehler, 'fehler', { vorschlag: daten.__vorschlag }); return; }
  form.reiter = T.typenStartReiter(daten);
  const w = knoten(`<div class="huelle-dlg kp-dlg t-dlg"><div class="t-kopf"></div><div class="t-nachher"></div><div class="t-reiter"></div><div class="t-inhalt"></div>
    <h3>Vorschau des Kerns</h3><div class="t-vorschau"><p class="leise">Noch nichts gewählt.</p></div></div>`);
  const q = (s) => w.querySelector(s);
  let ctrl = null;
  const zeichnen = () => {
    q('.t-kopf').innerHTML = T.typenKopfHtml(daten) + T.typenWarnungenHtml(daten);
    q('.t-reiter').innerHTML = T.typenReiterHtml(daten, form);
    q('.t-inhalt').innerHTML = T.typenInhaltHtml(daten, form);
  };
  zeichnen();
  const knopfAktualisieren = () => { const b = q('.t-los'); if (b) b.textContent = T.typenKnopfText(form, daten); };

  // Vorschau: bei jeder Eingabe rechnet der Kern den Trockenlauf; «Festlegen» sendet nur, was die letzte angenommene Vorschau zeigt (derselbe Inhalt)
  let nr = 0, vorschauStand = null, laeuft = false;
  async function vorschau() {
    const mein = ++nr;
    const a = T.typenAuswertung(form, daten);
    const ziel = q('.t-vorschau');
    vorschauStand = null;
    if (a.fehler.length) { ziel.innerHTML = `<p class="leise">Eingabe noch nicht vollständig:</p><ul class="hinweisliste">${a.fehler.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`; return null; }
    if (a.typen.length === 0) { ziel.innerHTML = '<p class="leise">Noch nichts gewählt.</p>'; return null; }
    const par = T.typenParameter(a);
    ziel.innerHTML = spinner('Der Kern prüft (Trockenlauf – nichts wird geändert) …');
    const r = await sende('typmaterial_setzen', par, { trockenlauf: true });
    if (mein !== nr) return null;
    ziel.innerHTML = T.typenVorschauHtml(r, a);
    vorschauStand = { key: JSON.stringify(par), ok: r.angenommen === true };
    return r;
  }
  const plan = entprellen(vorschau, 400);

  async function neuLaden() {
    const neu = await frage('typmaterial', T.typenAbfrageParameter(form));
    if (neu.__fehler) { ctrl?.fehler(`${T.fuerMenschen(neu.__fehler)}${neu.__vorschlag ? ' – ' + T.fuerMenschen(neu.__vorschlag) : ''}`); return false; }
    daten = neu;
    zeichnen();
    return true;
  }

  async function anwenden() {
    if (laeuft) return;
    laeuft = true;
    try {
      const a = T.typenAuswertung(form, daten);
      const pr = T.typenPruefen(a);
      if (pr !== true) { ctrl.fehler(pr); return; }
      const par = T.typenParameter(a);
      if (vorschauStand?.key !== JSON.stringify(par) || !vorschauStand.ok) {
        // noch keine (passende) Vorschau: erst ansehen, dann nochmals klicken
        const r = await vorschau();
        if (!r) return;
        if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); return; }
        q('.t-vorschau').insertAdjacentHTML('afterbegin', '<p class="kp-warn t-nochmal"><b>Vorschau geprüft – zum Übernehmen den Knopf nochmals klicken.</b></p>');
        return;
      }
      ++nr;
      const r = await sende('typmaterial_setzen', par);
      if (!r.angenommen) { ctrl.fehler(T.ablehnung(r)); q('.t-vorschau').innerHTML = T.typenVorschauHtml(r, a); return; }
      vorschauStand = null;
      form[form.reiter] = {};
      form.quelle = '';   // die nächste Festlegung braucht ihre eigene Quelle (nie vorbelegt)
      q('.t-nachher').innerHTML = T.typenNachherHtml(par.typen);
      q('.t-vorschau').innerHTML = T.laufHtml(r, { titel: 'Festgelegt' });
      await neuLaden();
      meldung('Typenliste geändert.', 'ok');
      panelNeuLaden();
    } finally { laeuft = false; }
  }

  // Zeilen des Formulars werden erst angelegt, wenn der Mensch etwas anfasst (bis dahin gelten die Vorbelegungen der Hilfsfunktionen)
  const zeileBestaetigen = (s) => (form.bestaetigen[s] ??= { ...T.bestaetigenZeile(form, daten.ausTypname.find((r) => r.schluessel === s) ?? { schluessel: s }) });
  const zeileListe = (kz) => (form.liste[kz] ??= { ...T.listeZeile(form, daten.typen.find((t) => t.element === kz) ?? { element: kz }) });

  w.addEventListener('click', async (e) => {
    const reiter = e.target.closest('.t-reiter-knopf');
    if (reiter) { form.reiter = reiter.dataset.reiter; ++nr; zeichnen(); q('.t-vorschau').innerHTML = '<p class="leise">Noch nichts gewählt.</p>'; vorschauStand = null; plan(); return; }
    if (e.target.closest('.t-los')) { await anwenden(); return; }
    const nach = e.target.closest('.t-nach');
    if (nach) await (nach.dataset.nach === 'vorschlagen' ? vorschlagenDialog() : pruefenDialog());
  });
  w.addEventListener('input', (e) => {
    const t = e.target;
    if (t.matches('.t-quelle')) form.quelle = t.value;
    else if (t.matches('.t-zq')) zeileListe(t.closest('tr').dataset.e).quelle = t.value;
    else return;
    knopfAktualisieren();
    plan();
  });
  w.addEventListener('change', async (e) => {
    const t = e.target;
    const tr = t.closest('tr');
    if (t.matches('.t-betroffen')) { form.betroffen = t.checked; ++nr; await neuLaden(); plan(); return; }
    if (t.matches('.t-kat')) {
      if (form.reiter === 'unbekannt') { form.unbekannt[tr.dataset.s] = t.value; tr.classList.toggle('gewaehlt', t.value !== ''); }
      else if (form.reiter === 'bestaetigen') zeileBestaetigen(tr.dataset.s).kategorie = t.value;
      else zeileListe(tr.dataset.e).kategorie = t.value;
      tr.querySelector('.t-wirkung').textContent = T.kategorieWirkung(daten, t.value);
    } else if (t.matches('.t-an')) { zeileBestaetigen(tr.dataset.s).an = t.checked; tr.classList.toggle('gewaehlt', t.checked); }
    else if (t.matches('.t-an-alle')) {
      for (const r of w.querySelectorAll('.t-tab-bestaetigen tbody tr')) { zeileBestaetigen(r.dataset.s).an = t.checked; r.classList.toggle('gewaehlt', t.checked); r.querySelector('.t-an').checked = t.checked; }
    } else if (t.matches('.t-entf')) { zeileListe(tr.dataset.e).entfernen = t.checked; tr.classList.toggle('t-weg', t.checked); }
    else return;
    knopfAktualisieren();
    plan();
  });

  await dialog({
    titel: 'Typenliste «Typname → Material»', inhalt: w, breite: 'min(1180px, 96vw)', klasse: 'huelle typen',
    beiOeffnen: (c) => { ctrl = c; },
    knoepfe: [{ text: 'Schliessen', wert: true }, { text: 'Neu laden', schliesst: false, klick: async () => { ++nr; await neuLaden(); plan(); } }],
  });
  nr++;
}
