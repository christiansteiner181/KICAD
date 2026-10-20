// Dialog «IFC exportieren» (Modul 7 Teil B2, entwurf/modul-7.md): Fassung (IFC4 Vorgabe, IFC2X3), Umfang (Gewerke und Geschosse als Häkchen,
// «alle» als Vorgabe), Schalter «Räume mitgeben» (Vorgabe aus), weitere IDS-Dateien (Pfade; die IDS-Basis von H+K wird immer geprüft) und das Ziel
// als Hinweis (<Projektordner>\ausgabe\ifc, nicht wählbar). Danach der Exportbericht im Dialog: Datei mit Download, Zähler je Klasse und Entität,
// übersprungene Elemente mit Grund, Schemaprüfung, IDS-Ergebnis je Regel (Kennzeichen anklickbar = Element im Modell wählen), Dauer.
// Alles läuft über die Kern-Tür (POST /api/ifc/export); Entitäten, Zähler und Prüfungen rechnet der Kern. Reine Teile: ifc-export-text.js.

import { senden, ApiFehler } from '../api.js';
import { zustand, element, auswaehlen, derKlasse } from '../zustand.js';
import { zeigen, meldung } from './dialoge.js';
import { knoten } from './werkzeug.js';
import * as T from './ifc-export-text.js';

const esc = T.esc;

/** Gewerke aus dem Katalog (gewerke.json: id, titel); ohne Katalog die festen Gewerke der Oberfläche. */
export function gewerkeDesKatalogs() {
  const k = zustand.katalog?.gewerke;
  if (Array.isArray(k) && k.length > 0) return k.map((g) => ({ id: g.id, titel: g.titel || g.id }));
  return [['sanitaer', 'Sanitär'], ['heizung', 'Heizung'], ['lueftung', 'Lüftung'], ['elektro', 'Elektro'], ['msrl', 'MSRL']].map(([id, titel]) => ({ id, titel }));
}

/** Geschosse des Modells, oben zuerst (nach Kote). */
function geschosseDesModells() {
  return derKlasse('Geschoss').sort((a, b) => (b.werte?.kote ?? 0) - (a.werte?.kote ?? 0)).map((g) => ({ kennzeichen: g.kennzeichen, name: g.name || g.kennzeichen }));
}

/** Wählt das Element zum Kennzeichen im Modell. Gibt true zurück, wenn es das Element gibt. */
export function elementWaehlen(kz) {
  for (const k of T.kennzeichenKandidaten(kz)) {
    const e = element(k);
    if (e) { auswaehlen(e.id); return true; }
  }
  return false;
}

/** Ein Export zur Zeit, auch wenn der Dialog zwischendurch geschlossen und neu geöffnet wird. */
let exportLaeuft = false;

/** Öffnet den Dialog. Gibt zurück, wenn er geschlossen wird. */
export function ifcExportDialog() {
  const a = T.auswahlStart();
  const gewerke = gewerkeDesKatalogs();
  const geschosse = geschosseDesModells();
  let laeuft = false;
  let offen = true;
  let letzter = null;   // letzter Bericht

  const haken = (klasse, wert, text, titel = '') => `<label class="haken ie-haken"${titel ? ` title="${esc(titel)}"` : ''}><input type="checkbox" class="${klasse}" value="${esc(wert)}"> ${esc(text)}</label>`;
  const w = knoten(`<div class="ie">
    <div class="ie-form">
      <div class="ie-block"><div class="ie-etikett">Fassung</div><div class="ie-feld ie-fassungen">${T.FASSUNGEN.map((f) =>
        `<label class="haken ie-haken" title="${esc(f.hinweis)}"><input type="radio" name="ie-fassung" class="ie-fassung" value="${esc(f.id)}"${f.id === a.fassung ? ' checked' : ''}> ${esc(f.titel)}${f.id === a.fassung ? ' <span class="leise">(Vorgabe)</span>' : ''}</label>`).join('')}</div></div>
      <div class="ie-block"><div class="ie-etikett">Umfang</div><div class="ie-feld">
        <div class="ie-gruppe" data-gruppe="gewerke"><label class="haken ie-haken ie-alle"><input type="checkbox" class="ie-gewerke-alle" checked> <b>alle Gewerke</b></label>
          <span class="ie-liste-haken">${gewerke.map((g) => haken('ie-gewerk', g.id, g.titel)).join('')}</span></div>
        <div class="ie-gruppe" data-gruppe="geschosse"><label class="haken ie-haken ie-alle"><input type="checkbox" class="ie-geschosse-alle" checked> <b>alle Geschosse</b></label>
          <span class="ie-liste-haken">${geschosse.map((g) => haken('ie-geschoss', g.kennzeichen, g.name === g.kennzeichen ? g.kennzeichen : `${g.kennzeichen} · ${g.name}`)).join('') || '<span class="leise">Im Modell gibt es noch kein Geschoss.</span>'}</span></div>
      </div></div>
      <div class="ie-block"><div class="ie-etikett">Räume</div><div class="ie-feld"><label class="haken ie-haken"><input type="checkbox" class="ie-raeume"> Räume mitgeben (IfcSpace und Zonen)</label>
        <span class="leise ie-klein">Vorgabe aus: ohne Räume. Der Geschossfilter gilt weiter.</span></div></div>
      <div class="ie-block"><div class="ie-etikett">IDS-Prüfung</div><div class="ie-feld">
        <div class="ie-basis-zeile"><span class="ie-basis">IDS-Basis H+K</span> wird immer geprüft (Nennweiten, System, Geschoss, Typobjekte, Kennzeichen).</div>
        <label class="ie-ids-feld"><span class="leise">Weitere IDS-Dateien (Bauherr, Architekt): ein vollständiger Pfad je Zeile</span>
          <textarea class="ie-ids" rows="3" spellcheck="false" data-kein-enter="1" placeholder="z. B. C:\\Daten\\Bauherr.ids" aria-label="Weitere IDS-Dateien"></textarea></label></div></div>
      <div class="ie-block"><div class="ie-etikett">Ziel</div><div class="ie-feld"><code class="ie-ziel">${esc(T.zielText(zustand.status?.ort))}</code>
        <span class="leise ie-klein">Dateiname: ${esc(zustand.status?.projekt || '<Projekt>')}_&lt;Fassung&gt;_&lt;JJJJMMTT-hhmm&gt;.ifc, daneben der Bericht (.bericht.md und .bericht.json). Das Ziel lässt sich nicht wählen.</span></div></div>
      <div class="ie-umfang leise"></div>
    </div>
    <div class="ie-lauf" hidden></div>
    <div class="ie-ergebnisse" hidden></div>
  </div>`);
  const q = (s) => w.querySelector(s);
  const qa = (s) => [...w.querySelectorAll(s)];

  // ------------------------------------------------------------------------------------------ Auswahl lesen

  function auswahlLesen() {
    a.fassung = q('.ie-fassung:checked')?.value ?? 'IFC4';
    a.gewerkeAlle = q('.ie-gewerke-alle').checked;
    a.gewerke = qa('.ie-gewerk:checked').map((x) => x.value);
    a.geschosseAlle = q('.ie-geschosse-alle').checked;
    a.geschosse = qa('.ie-geschoss:checked').map((x) => x.value);
    a.raeume = q('.ie-raeume').checked;
    a.ids = q('.ie-ids').value;
    return a;
  }
  function umfangZeigen() {
    auswahlLesen();
    for (const [alle, klasse] of [['.ie-gewerke-alle', '.ie-gewerk'], ['.ie-geschosse-alle', '.ie-geschoss']]) {
      const an = q(alle).checked;
      qa(klasse).forEach((x) => { x.disabled = an; });
    }
    q('.ie-umfang').textContent = `Umfang: ${T.umfangText(a)}`;
  }

  w.addEventListener('change', (e) => {
    if (laeuft) return;
    const t = e.target;
    // ein einzelnes Häkchen nimmt «alle» weg; «alle» setzt die einzelnen zurück
    if (t.matches('.ie-gewerke-alle') && t.checked) qa('.ie-gewerk').forEach((x) => { x.checked = false; });
    if (t.matches('.ie-geschosse-alle') && t.checked) qa('.ie-geschoss').forEach((x) => { x.checked = false; });
    umfangZeigen();
  });
  w.addEventListener('input', (e) => { if (e.target.matches('.ie-ids')) umfangZeigen(); });
  // Einzelne Häkchen sind bei «alle» gesperrt; ein Klick auf die Zeile schaltet «alle» aus und hakt an
  w.addEventListener('click', (e) => {
    const lab = e.target.closest?.('label.ie-haken');
    const box = lab?.querySelector('input.ie-gewerk, input.ie-geschoss');
    if (box?.disabled && e.target !== box) {
      e.preventDefault();
      const gruppe = box.classList.contains('ie-gewerk') ? '.ie-gewerke-alle' : '.ie-geschosse-alle';
      q(gruppe).checked = false;
      box.checked = true;
      umfangZeigen();
    }
  });

  // ------------------------------------------------------------------------------------------ Ansichten wechseln

  function ansicht(welche) {
    q('.ie-form').hidden = welche !== 'form';
    q('.ie-lauf').hidden = welche !== 'lauf';
    q('.ie-ergebnisse').hidden = welche !== 'ergebnis';
  }

  function laufZeigen() {
    const l = q('.ie-lauf');
    l.innerHTML = `<div class="ie-lauf-kopf"><span class="spinner" aria-hidden="true"></span><b class="ie-lauf-titel">IFC wird geschrieben und geprüft …</b><span class="ie-zeit leise">0 s</span></div>`
      + `<p class="leise">${esc(T.umfangText(a))}</p>`
      + '<p class="leise">Aufbau, Schreiben, Schemaprüfung und IDS laufen im Kern; bei grossen Projekten dauert das einige Sekunden. Der Dialog bleibt gesperrt, bis das Ergebnis da ist.</p>';
    ansicht('lauf');
    const t0 = Date.now();
    const timer = setInterval(() => { const z = l.querySelector('.ie-zeit'); if (z) z.textContent = `${Math.round((Date.now() - t0) / 1000)} s`; }, 400);
    return () => clearInterval(timer);
  }

  function ergebnisZeigen(b) {
    letzter = b;
    const e = q('.ie-ergebnisse');
    e.innerHTML = `<div class="ie-leiste"><button type="button" class="dlg-knopf ie-ordner-oeffnen" title="Den Ordner ausgabe\\ifc im Explorer zeigen">Ordner öffnen</button>`
      + `<button type="button" class="dlg-knopf ie-zurueck">Zurück zur Auswahl</button><span class="leise ie-ordnerpfad" title="${esc(T.ordnerVon(b.datei))}">${esc(T.ordnerVon(b.datei))}</span></div>`
      + `<div class="ie-bericht">${T.berichtHtml(b)}</div>`;
    ansicht('ergebnis');
  }

  function fehlerZeigen(err) {
    const f = T.fehlerAnzeige(err);
    q('.ie-lauf').innerHTML = `<div class="ie-lauf-kopf"><b class="ie-lauf-titel fehler">${esc(f.titel)}</b></div>`
      + `<div class="fehlerkasten ie-fehler" role="alert"><b>${esc(f.text)}</b>${f.vorschlag ? `<div class="ie-vorschlag">Vorschlag: ${esc(f.vorschlag)}</div>` : ''}</div>`
      + '<p class="ie-lauf-knoepfe"><button type="button" class="dlg-knopf ie-zurueck">Zurück zur Auswahl</button></p>';
    ansicht('lauf');
  }

  // ------------------------------------------------------------------------------------------ Bedienung im Ergebnis

  w.addEventListener('click', async (e) => {
    const t = e.target;
    if (t.closest('.ie-zurueck')) { if (!laeuft) ansicht('form'); return; }
    const kz = t.closest('.ie-kz');
    if (kz) {
      qa('.ie-kz.gewaehlt').forEach((x) => x.classList.remove('gewaehlt'));
      if (elementWaehlen(kz.dataset.kz)) kz.classList.add('gewaehlt');
      else meldung(`Das Element «${kz.dataset.kz}» gibt es im Modell nicht (mehr).`, 'info', { vorschlag: 'Der Bericht stammt vom Zeitpunkt des Exports; das Element kann inzwischen geändert oder gelöscht sein.' });
      return;
    }
    if (t.closest('.ie-ordner-oeffnen') && letzter) {
      const ordner = T.ordnerVon(letzter.datei);
      try { await senden('/api/ordner/zeigen', { pfad: ordner }); }
      catch (err) { meldung(`Der Ordner lässt sich nicht öffnen: ${T.fehlerText(err)}`, 'warnung', { vorschlag: ordner }); }
    }
  });

  // ------------------------------------------------------------------------------------------ Exportieren

  async function exportieren(ctrl) {
    if (laeuft) return;   // Doppelklick-Schutz: ein Lauf zur Zeit
    if (exportLaeuft) { ctrl.fehler('Ein Export läuft noch. Bitte warten, bis er fertig ist.'); return; }
    auswahlLesen();
    const pr = T.auswahlPruefen(a);
    if (pr !== true) { ctrl.fehler(pr); ansicht('form'); return; }
    ctrl.fehler(null);
    laeuft = true; exportLaeuft = true;
    const exportKnopf = ctrl.fuss.querySelector('.dlg-knopf.primaer');
    if (exportKnopf) exportKnopf.disabled = true;
    const stopp = laufZeigen();
    try {
      const antwort = await senden('/api/ifc/export', T.anfrageAus(a));
      stopp();
      const b = antwort?.bericht ?? antwort;
      if (offen) ergebnisZeigen(b);
      else meldung(T.zusammenfassung(b), b?.ergebnis?.ids === 'bestanden' && b?.ergebnis?.schemapruefung === 'fehlerfrei' ? 'ok' : 'warnung');   // Dialog schon zu: kurze Meldung statt Bericht
    } catch (err) {
      stopp();
      if (!(err instanceof ApiFehler)) console.error(err);   // Fehler des Servers zeigt der Dialog selbst
      if (offen) fehlerZeigen(err);
      else meldung(`IFC-Export: ${T.fehlerText(err)}`, 'fehler');
    } finally {
      laeuft = false; exportLaeuft = false;
      if (exportKnopf) exportKnopf.disabled = false;
    }
  }

  umfangZeigen();
  return zeigen({
    titel: 'IFC exportieren', inhalt: w, breite: 900, klasse: 'dlg-hoch ifc-export',
    beiOeffnen: () => { w.closest('.dlg')?.querySelector('.ie-fassung:checked')?.focus?.(); },
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Exportieren', primaer: true, schliesst: false, klick: (ctrl) => exportieren(ctrl) }],
  }).then((wert) => { offen = false; return wert; });
}
