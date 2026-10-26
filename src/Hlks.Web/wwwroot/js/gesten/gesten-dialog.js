// Dialog «Gesten» (Modul G, entwurf/modul-gesten.md): Verwalten › Hilfe › «Gesten» und Rechtsklick auf das Symbol in der Statusleiste. Liste der Gesten, Einstellungen
// (Gestenhand, Kamera, Handskelett, Belegung G2/G6, Empfindlichkeit), Testfeld und «Punktfolge aufnehmen». Der HTML-Text steht in gesten-dialog-text.js, alle Entscheidungen
// und Texte in gesten-text.js; hier nur Verdrahtung. Alles läuft über die Steuerung (gesten-steuerung.js) – der Dialog kennt gesten.js nicht.
//
// Solange der Dialog offen ist, steht die Steuerung im Testmodus: erkannte Gesten erscheinen im Testfeld, lösen aber nichts aus. Die Aufnahme speichert nur Punkte
// (T.aufnahmeDatei) und bietet sie als Datei zum Speichern an; kein Kamerabild, kein Senden.

import { zeigen, meldung } from '../oberflaeche/dialoge.js';
import { knoten } from '../oberflaeche/werkzeug.js';
import { aktionen } from '../oberflaeche/aktionen.js';
import { steuerung } from './gesten-steuerung.js';
import * as T from './gesten-text.js';
import * as H from './gesten-dialog-text.js';

const ohneWeich = (t) => String(t ?? '').replace(/[​­]/g, '');
const PROTOKOLL_MAX = 8;

let offen = null;

/** Öffnet den Dialog (ein zweiter Aufruf gibt den offenen zurück). Gibt zurück, wenn er geschlossen wird. */
export function gestenDialog() {
  if (offen) return offen;
  offen = oeffnen().finally(() => { offen = null; });
  return offen;
}

/** Bietet eine Textdatei zum Speichern an (nur die Punktfolge; kein Bild). Zuerst mit dem Speicherdialog des Browsers (im App-Fenster
 *  von KICAD kam der Download über einen Link nicht an, Abnahme 26.10.2026); ohne diesen Dialog über einen Link. Ergebnis: true = gespeichert. */
async function dateiAnbieten(name, text) {
  if (typeof window.showSaveFilePicker === 'function') {
    try {
      const h = await window.showSaveFilePicker({ suggestedName: name, startIn: 'downloads', types: [{ description: 'Punktfolge der Hand', accept: { 'application/json': ['.json'] } }] });
      const s = await h.createWritable();
      await s.write(text);
      await s.close();
      return true;
    } catch (e) {
      if (e?.name === 'AbortError') return false;   // abgebrochen
      // sonst über den Link
    }
  }
  const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const a = document.createElement('a');
  a.href = url; a.download = name; a.hidden = true;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 4000);
  return true;
}

async function oeffnen() {
  const titelVon = (id) => aktionen.get(id)?.titel;
  const belegbar = T.BELEGBAR.filter((id) => aktionen.has(id)).map((id) => ({ id, titel: ohneWeich(titelVon(id)) }));
  const [kameras0, vorgabe] = await Promise.all([steuerung.kameras(), steuerung.stellschraubenVorgabe()]);
  let kameras = kameras0;
  let protokoll = [];
  let w = null;

  const daten = () => ({
    zustand: steuerung.zustand(), einst: steuerung.einstellung(), zeilen: T.gestenZeilen(steuerung.einstellung(), titelVon),
    kameras, belegbar, schrauben: vorgabe ? T.stellschraubenZeilen(vorgabe, steuerung.einstellung().stellschrauben) : null, protokoll,
  });
  const q = (s) => w.querySelector(s);

  function listeNeu() { q('.gd-liste-platz').innerHTML = H.listeHtml(daten().zeilen); }
  function kopfNeu() {
    const z = steuerung.zustand();
    const a = T.statusAnzeige(z);
    const an = q('.gd-an');
    if (an) an.checked = !!z.an;
    const s = q('.gd-status');
    if (s) { s.textContent = a.text; s.className = `gd-status ${a.stufe}`; }
    const f = q('.gd-fehler');
    if (f) { f.hidden = !(z.fehler && !z.an); f.textContent = z.fehler ?? ''; }
  }
  function testNeu() { q('.gd-test-bereich').innerHTML = H.testfeldHtml(protokoll, steuerung.zustand().laeuft); }
  function aufnahmeNeu() { const z = steuerung.zustand(); q('.gd-aufnahme-bereich').innerHTML = H.aufnahmeHtml({ laeuft: z.laeuft, aufnahme: z.aufnahme }); aufnahmeBinden(); }
  function schraubenNeu() { q('.gd-schrauben-platz').innerHTML = H.empfindlichkeitHtml(daten().schrauben); schraubenBinden(); }

  async function setzen(teil) {
    try { await steuerung.einstellungSetzen(teil); }
    catch (e) { meldung(e?.message ?? String(e), 'fehler', { vorschlag: e?.vorschlag }); }
  }

  function schraubenBinden() {
    for (const eingabe of w.querySelectorAll('.gd-schraube')) {
      eingabe.onchange = async () => {
        const zeile = daten().schrauben?.find((z) => z.name === eingabe.dataset.name);
        const neu = T.stellschraubeSetzen(steuerung.einstellung(), zeile, eingabe.value);
        await setzen({ stellschrauben: neu.stellschrauben });
        schraubenNeu();
      };
    }
    for (const knopf of w.querySelectorAll('.gd-schraube-vorgabe')) {
      knopf.onclick = async () => {
        const e = steuerung.einstellung();
        const rest = { ...e.stellschrauben };
        delete rest[knopf.dataset.name];
        await setzen({ stellschrauben: rest });
        schraubenNeu();
      };
    }
  }

  function aufnahmeBinden() {
    const start = q('.gd-aufnahme-start');
    const ende = q('.gd-aufnahme-ende');
    if (start) start.onclick = () => {
      try { steuerung.aufnahmeStarten(); aufnahmeNeu(); meldung('Aufnahme läuft: nur die Punkte der Hand werden aufgezeichnet.', 'info'); }
      catch (e) { meldung(e?.message ?? String(e), 'warnung'); }
    };
    if (ende) ende.onclick = async () => {
      let r = null;
      try { r = await steuerung.aufnahmeBeenden(); } catch (e) { meldung(e?.message ?? String(e), 'warnung'); }
      aufnahmeNeu();
      const d = T.aufnahmeDatei(r);
      if (!d) { meldung('Es wurde nichts aufgenommen.', 'info'); return; }
      if (await dateiAnbieten(d.name, d.text)) meldung(`Punktfolge mit ${d.bilder} Bildern gespeichert (nur Punkte).`, 'ok');
      else meldung('Speichern abgebrochen.', 'info');
    };
  }

  function binden() {
    q('.gd-an').onchange = async (ev) => {
      ev.target.disabled = true;
      await steuerung.schalten(ev.target.checked);
      ev.target.disabled = false;
      kopfNeu(); testNeu(); aufnahmeNeu();
      kameras = await steuerung.kameras();   // mit der Erlaubnis der Kamera erscheinen ihre Namen
      kameraNeu();
    };
    q('.gd-hand').onchange = (ev) => setzen({ hand: ev.target.value });
    q('.gd-kamera').onchange = (ev) => setzen({ kameraId: ev.target.value || null });
    q('.gd-skelett').onchange = (ev) => setzen({ skelett: ev.target.checked });
    for (const s of w.querySelectorAll('.gd-beleg')) {
      s.onchange = async () => {
        await setzen({ belegung: T.belegungSetzen(steuerung.einstellung(), s.dataset.nr, s.dataset.richtung, s.value).belegung });
        listeNeu();
      };
    }
    q('.gd-vorgabe').onclick = async () => {
      await setzen(T.aufVorgabe(steuerung.einstellung()));
      rendern();
    };
    schraubenBinden();
    aufnahmeBinden();
  }

  function kameraNeu() {
    const wahl = q('.gd-kamera');
    if (!wahl) return;
    const html = H.einstellungHtml(steuerung.einstellung(), kameras, belegbar);
    const t = document.createElement('template');
    t.innerHTML = html;
    const neu = t.content.querySelector('.gd-kamera');
    if (neu) wahl.innerHTML = neu.innerHTML;
  }

  function rendern() {
    w.innerHTML = H.dialogHtml(daten());
    binden();
  }

  const abmelden = steuerung.abonnieren((e) => {
    if (!w?.isConnected) return;
    if (e.art === 'zustand' || e.art === 'schritt') {
      const z = T.ereignisZeile(e, steuerung.einstellung(), titelVon);
      if (z && z !== protokoll[0]) { protokoll = [z, ...protokoll].slice(0, PROTOKOLL_MAX); testNeu(); }
    }
    if (e.art === 'zustand' || e.art === 'schalter') { kopfNeu(); aufnahmeNeu(); if (e.art === 'schalter') testNeu(); }
  });

  w = knoten('<div class="gd-wurzel"></div>');
  rendern();
  steuerung.testmodus(true);   // erkannte Gesten zeigen, nichts auslösen, solange der Dialog offen ist
  try {
    await zeigen({ titel: T.DIALOG_TITEL, inhalt: w, breite: 760, klasse: 'dlg-hoch gesten', art: 'gesten', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
  } finally {
    abmelden();
    steuerung.testmodus(false);
    if (steuerung.zustand().aufnahme) { try { await steuerung.aufnahmeBeenden(); } catch { /* egal */ } }
  }
}
