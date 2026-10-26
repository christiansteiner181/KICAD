// Dialog «Quest verbinden» (Modul XR, entwurf/modul-xr.md «Start ohne Link-fähigen PC»): macht in KICAD, was werkzeuge/quest_verbinden.ps1 macht – der Browser der
// Meta Quest öffnet KICAD als http://localhost:<Port>, adb leitet den Port der Quest auf diesen PC weiter. Der Dialog zeigt den Zustand als Ampel (adb gefunden,
// Quest verbunden, freigegeben, Weiterleitung aktiv), die Knöpfe «Verbinden», «Auf WLAN umstellen», «Über WLAN verbinden» (mit Adresse), «Trennen» und das Feld
// für den Pfad zur adb.exe (Benutzereinstellung quest.adb). Fehlt adb, führt er Schritt für Schritt durch die Einrichtung; KICAD lädt nichts herunter und
// installiert nichts. Alles läuft über /api/quest (Server: QuestDienst.cs); die Logik der Ampel und der Knöpfe steht in quest-text.js.
//
// Dazu der Schalter «Brille automatisch verbinden» (Einstellung quest.automatisch) und die gemerkte Brille (Seriennummer, MAC, letzte IP) mit «Vergessen» (quest.brillen);
// die Statusanzeige «Brille» der Statusleiste steht in quest-status.js.
//
// Öffnen: Knopf «Quest verbinden …» im Register Ansicht (Aktion ansicht.questVerbinden) und der graue Knopf «In VR begehen» bzw. der Link «Quest im Browser
// der Brille nutzen …» der 3D-Ansicht (ansichten/vr3d.js ruft questDialog() über einen dynamischen Import).

import { holen, senden } from '../api.js';
import { zeigen, meldung, bestaetigen } from './dialoge.js';
import { knoten } from './werkzeug.js';
import { benutzerLaden, benutzerSetzen, benutzerWert } from './benutzer.js';
import * as T from './quest-text.js';
import { questStatusAktualisieren } from './quest-status.js';
import { brilleZeigenAn, brilleZeigenSetzen } from '../ansichten/xr-brille-einstellung.js';

const esc = T.esc;

/** Nur ein Dialog zugleich. */
let offenerDialog = null;

/** Öffnet den Dialog (ein zweiter Aufruf gibt den offenen zurück). Gibt zurück, wenn er geschlossen wird. */
export function questDialog() {
  if (offenerDialog) return offenerDialog;
  let z = null;          // letzter Zustand des Servers
  let laeuft = false;    // ein Aufruf läuft
  let offen = true;
  let letzteMeldung = null;   // { text, art: 'ok' | 'fehler', vorschlag }
  let brillenZahl = null;     // Zahl der gemerkten Brillen beim letzten Lesen (null = noch nicht gelesen)

  const w = knoten(`<div class="qd">
    <div class="qd-app-platz"></div>
    <details class="qd-browser" open>
    <summary class="qd-browser-kopf"><b>Browser-Prototyp</b> <span class="leise">älterer Weg: KICAD im Browser der Brille (adb reverse, Autostart); mit der Quest App nicht nötig</span></summary>
    <div class="qd-browser-inhalt">
    <div class="qd-kopf"><b>Zustand</b><button type="button" class="dlg-knopf qd-neu" title="Liest den Zustand von adb neu">Erneut prüfen</button><span class="spinner qd-spinner" aria-hidden="true" hidden></span></div>
    <ul class="qd-ampel" aria-label="Zustand"></ul>
    <div class="qd-meldung" role="status" aria-live="polite" hidden></div>
    <div class="qd-naechste leise"></div>
    <section class="qd-einrichtung" hidden>
      <b>adb einrichten (einmalig)</b>
      <ol class="qd-schritte">
        <li><b>${esc(T.EINRICHTUNG[0].titel)}.</b> ${esc(T.EINRICHTUNG[0].text)} <a class="qd-link" href="${esc(T.PLATFORM_TOOLS_URL)}" target="_blank" rel="noopener noreferrer">Android Platform Tools (offizielle Seite)</a></li>
        <li><b>${esc(T.EINRICHTUNG[1].titel)}.</b> ${esc(T.EINRICHTUNG[1].text)}</li>
        <li><b>${esc(T.EINRICHTUNG[2].titel)}.</b> ${esc(T.EINRICHTUNG[2].text)} <button type="button" class="dlg-knopf qd-neu qd-neu-schritt">Erneut prüfen</button></li>
      </ol>
      <div class="leise qd-suchorte-zeile">Gesucht wird in: <span class="qd-suchorte"></span></div>
    </section>
    <div class="qd-aktionen">
      <button type="button" class="dlg-knopf primaer qd-verbinden" title="Leitet den Port von KICAD (adb reverse) auf die Quest weiter">Verbinden</button>
      <button type="button" class="dlg-knopf qd-wlan" title="Stellt die per Kabel verbundene Quest einmal auf adb über WLAN um; danach geht es ohne Kabel">Auf WLAN umstellen</button>
      <button type="button" class="dlg-knopf qd-trennen" title="Entfernt die Weiterleitung wieder">Trennen</button>
    </div>
    <div class="qd-zeile"><label class="qd-etikett" for="qd-ip">WLAN-Adresse der Quest</label>
      <input id="qd-ip" type="text" class="qd-ip" spellcheck="false" autocomplete="off" data-kein-enter="1" placeholder="z. B. 192.168.1.50" aria-label="WLAN-Adresse der Quest">
      <button type="button" class="dlg-knopf qd-ip-verbinden">Über WLAN verbinden</button></div>
    <div class="qd-zeile"><label class="qd-etikett" for="qd-adb">Pfad zur adb.exe</label>
      <input id="qd-adb" type="text" class="qd-adb" spellcheck="false" autocomplete="off" data-kein-enter="1" placeholder="leer = automatisch suchen" aria-label="Pfad zur adb.exe">
      <button type="button" class="dlg-knopf qd-adb-speichern">Speichern</button></div>
    <div class="leise qd-klein">Der Pfad muss auf adb.exe enden und auf diesem Rechner liegen (kein Netzlaufwerk). Leer lassen und speichern: wieder automatisch suchen.</div>
    <section class="qd-brille"><b>Brille automatisch verbinden</b>
      <label class="qd-schalter"><input type="checkbox" class="qd-auto" aria-label="Brille automatisch verbinden"> Brille automatisch verbinden</label>
      <div class="leise qd-klein">KICAD merkt sich die WLAN-Adresse (MAC) der Brille und sucht sie nach jedem Programmstart im eigenen Netz, ohne dass die IP-Adresse bekannt sein muss. Nach einem Neustart der Brille ist einmal das Kabel nötig.</div>
      <div class="qd-brillen" aria-label="Gemerkte Brille"></div>
    </section>
    <section class="qd-autostart-block"><b>Begehung beim Aufsetzen</b>
      <label class="qd-schalter"><input type="checkbox" class="qd-autostart" aria-label="Begehung beim Aufsetzen automatisch starten"> Begehung beim Aufsetzen automatisch starten</label>
      <div class="leise qd-klein">${esc(T.AUTOSTART_HINWEIS)}</div>
    </section>
    <section class="qd-hinweise"><b>Gut zu wissen</b>
      <ul>${T.HINWEISE.map((h) => `<li>${esc(h)}</li>`).join('')}</ul></section>
    <div class="qd-adresse"></div>
    </div>
    </details>
  </div>`);
  const q = (s) => w.querySelector(s);
  // Teil «App» (Netzzugang, App «H+K XR» installieren, gekoppelte Brillen, Ersatzkopplung, Netz einrichten) kommt aus HK_XR (/hkxr/brillen-app-dialog.js, erst beim Öffnen
  // geladen) und liest den Zustand von adb aus diesem Dialog
  // Der Browser-Prototyp klappt von selbst ein, sobald die App in der Brille installiert ist (der Mensch kann ihn aufklappen; das gilt dann bis zum Schliessen)
  const browser = w.querySelector('.qd-browser');
  let browserVomMenschen = false;
  browser.querySelector('summary').addEventListener('click', () => { browserVomMenschen = true; });   // nur ein Klick des Menschen zählt (das Ereignis «toggle» kommt auch beim Aufbau)
  function browserEinstellen(app) {
    if (browserVomMenschen) return;
    const soll = browserOffenRegel(app, z);
    if (browser.open !== soll) browser.open = soll;
  }
  let browserOffenRegel = () => true;
  let appTeil = { aktualisieren() {}, zustandNeu() {}, beenden() {} };   // bis HK_XR geladen ist
  let appAbgemeldet = false;
  Promise.all([import('/hkxr/brillen-app-dialog.js'), import('/hkxr/brillen-app-text.js')]).then(([d, t]) => {
    if (appAbgemeldet) return;
    browserOffenRegel = t.browserOffen;
    appTeil = d.brillenAppTeil({
      quest: () => z, beiAenderung: () => neuPruefen(), beiGelesen: browserEinstellen, marke: T.KICAD_MARKE,
      dienste: { bestaetigen, benutzerLaden, benutzerSetzen, benutzerWert, brilleZeigenAn, brilleZeigenSetzen },
    });
    q('.qd-app-platz').replaceWith(appTeil.knoten);
  }).catch((e) => { console.error('H+K XR: Teil «App» nicht geladen', e); q('.qd-app-platz').textContent = 'Der Teil «App» aus HK_XR fehlt.'; });
  const qa = (s) => [...w.querySelectorAll(s)];

  // ------------------------------------------------------------------------------------------ Anzeige

  function zeigeAmpel() {
    q('.qd-ampel').innerHTML = T.ampel(z).map((p) => `<li class="qd-punkt ${esc(p.stufe)}" data-punkt="${esc(p.id)}" data-stufe="${esc(p.stufe)}">`
      + `<span class="qd-lampe" aria-hidden="true"></span><span class="qd-titel">${esc(p.titel)}</span><span class="qd-text leise">${esc(p.text)}</span></li>`).join('');
    q('.qd-naechste').textContent = T.naechsterSchritt(z);
    const einrichten = T.einrichtungSichtbar(z);
    q('.qd-einrichtung').hidden = !einrichten;
    q('.qd-kopf .qd-neu').hidden = einrichten;   // bei fehlendem adb steht «Erneut prüfen» im dritten Schritt
    q('.qd-suchorte').textContent = (z?.suchorte ?? []).join('; ');
    q('.qd-suchorte-zeile').hidden = !einrichten || !(z?.suchorte ?? []).length;
    q('.qd-adresse').innerHTML = z ? `In der Quest den Browser öffnen und eingeben: <code class="qd-url">${esc(T.urlText(z))}</code>` : '';
    const sch = T.knoepfe(z, laeuft, q('.qd-ip').value);
    q('.qd-verbinden').disabled = !sch.verbinden;
    q('.qd-wlan').disabled = !sch.wlan;
    q('.qd-trennen').disabled = !sch.trennen;
    q('.qd-ip-verbinden').disabled = !sch.ipVerbinden;
    qa('.qd-neu').forEach((b) => { b.disabled = !sch.neuPruefen; });
    q('.qd-adb-speichern').disabled = !sch.adbSpeichern;
    const brillen = T.brillenListe(z?.brillen);
    q('.qd-auto').checked = z?.automatisch === true;
    q('.qd-auto').disabled = !z || laeuft;
    q('.qd-autostart').checked = T.autostartAn(z?.autostart);
    q('.qd-autostart').disabled = !z || laeuft;
    q('.qd-brillen').innerHTML = brillen.length === 0
      ? '<div class="leise qd-keine-brille">Noch keine Brille gemerkt. Sie wird gemerkt, sobald die Quest einmal verbunden wurde.</div>'
      : brillen.map((b) => `<div class="qd-brille-zeile" data-mac="${esc(b.mac)}"><span class="qd-brille-text">${esc(T.brilleText(b))}</span>`
        + `<button type="button" class="dlg-knopf qd-vergessen" data-mac="${esc(b.mac)}" title="Vergisst diese Brille (die Automatik sucht sie dann nicht mehr)"${laeuft ? ' disabled' : ''}>Vergessen</button></div>`).join('');
    q('.qd-spinner').hidden = !laeuft;
    appTeil.aktualisieren();
    const m = q('.qd-meldung');
    m.hidden = !letzteMeldung;
    if (letzteMeldung) {
      m.className = 'qd-meldung ' + letzteMeldung.art;   // art ist 'ok' oder 'fehler' (nie ein Serverwert)
      const vorschlag = letzteMeldung.vorschlag ? `<div class="qd-vorschlag">Vorschlag: ${esc(letzteMeldung.vorschlag)}</div>` : '';
      m.innerHTML = `<b>${esc(letzteMeldung.text)}</b>${vorschlag}`;
    }
  }

  // ------------------------------------------------------------------------------------------ Server

  /** Ruft die Schnittstelle auf und zeigt Ergebnis bzw. Fehler; liest danach den Zustand. */
  async function lauf(arbeit, okText) {
    if (laeuft) return;
    laeuft = true;
    letzteMeldung = null;
    zeigeAmpel();
    try {
      const antwort = await arbeit();
      if (antwort && typeof antwort === 'object' && 'adbGefunden' in antwort) z = antwort;
      const t = antwort?.meldung || okText;   // das blosse Neulesen des Zustands braucht keine Meldung
      letzteMeldung = t ? { art: 'ok', text: t } : null;
    } catch (err) {
      const f = T.fehlerAnzeige(err);
      letzteMeldung = { art: 'fehler', text: f.text, vorschlag: f.vorschlag };
      try { z = await holen('/api/quest'); } catch { /* der Fehler oben genügt */ }
    } finally {
      laeuft = false;
      if (offen) { zeigeAmpel(); appTeil.zustandNeu(); }   // Quest App (P7): adb, Brille oder Pfad haben sich geändert – installierte Fassung und Weg neu lesen
      // eine Brille wurde gemerkt (oder vergessen): die Statusanzeige in der Statusleiste zieht nach
      const n = T.brillenListe(z?.brillen).length;
      if (brillenZahl !== null && n !== brillenZahl) questStatusAktualisieren();
      brillenZahl = n;
    }
  }

  const neuPruefen = () => lauf(async () => { z = await holen('/api/quest'); return z; });

  w.addEventListener('click', (e) => {
    const t = e.target.closest?.('button');
    if (!t || t.disabled) return;
    if (t.matches('.qd-neu')) neuPruefen();
    else if (t.matches('.qd-verbinden')) lauf(() => senden('/api/quest/verbinden'));
    else if (t.matches('.qd-wlan')) lauf(() => senden('/api/quest/wlan'));
    else if (t.matches('.qd-trennen')) lauf(() => senden('/api/quest/trennen'));
    else if (t.matches('.qd-ip-verbinden')) lauf(() => senden('/api/quest/ip', { ip: q('.qd-ip').value.trim() }));
    else if (t.matches('.qd-vergessen')) {
      const mac = t.dataset.mac;
      lauf(async () => {
        await benutzerSetzen(T.BRILLEN_SCHLUESSEL, T.ohneBrille(T.brillenListe(z?.brillen), mac));
        await questStatusAktualisieren();
        z = await holen('/api/quest');
        return { ...z, meldung: 'Die Brille ist vergessen.' };
      });
    }
    else if (t.matches('.qd-adb-speichern')) {
      const pfad = q('.qd-adb').value.trim();
      lauf(async () => {
        await benutzerSetzen(T.ADB_SCHLUESSEL, pfad === '' ? null : pfad);
        z = await holen('/api/quest');
        return { ...z, meldung: pfad === '' ? 'Der Pfad ist zurückgesetzt: adb wird automatisch gesucht.' : 'Der Pfad zur adb.exe ist gespeichert.' };
      });
    }
  });
  q('.qd-auto').addEventListener('change', (e) => {
    const an = e.target.checked;
    lauf(async () => {
      await benutzerSetzen(T.AUTOMATIK_SCHLUESSEL, an);
      await questStatusAktualisieren();   // schaltet die Statusanzeige ein bzw. aus; beim Einschalten folgt ein Schritt
      z = await holen('/api/quest');
      return { ...z, meldung: an ? 'Die Brille wird jetzt automatisch verbunden (beim Start, beim Öffnen einer 3D-Ansicht und im Takt, bis sie bereit ist).' : 'Die Automatik ist ausgeschaltet.' };
    });
  });
  q('.qd-autostart').addEventListener('change', (e) => {
    const an = e.target.checked;
    lauf(async () => {
      await benutzerSetzen(T.AUTOSTART_SCHLUESSEL, an);
      z = await holen('/api/quest');
      return { ...z, meldung: an ? 'Nach dem Klick am PC startet die Begehung, sobald die Brille aufgesetzt ist (beim ersten Mal in der Brille «VR erlauben» bestätigen).' : 'Der Autostart ist ausgeschaltet: in der Brille die VR-Startseite von Hand öffnen und tippen.' };
    });
  });
  q('.qd-ip').addEventListener('input', zeigeAmpel);
  q('.qd-ip').addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); q('.qd-ip-verbinden').click(); } });
  q('.qd-adb').addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); q('.qd-adb-speichern').click(); } });

  zeigeAmpel();
  const geschlossen = zeigen({
    titel: 'Quest verbinden', inhalt: w, breite: 640, klasse: 'dlg-hoch quest-verbinden',
    beiOeffnen: () => {
      benutzerLaden().finally(() => { q('.qd-adb').value = String(benutzerWert(T.ADB_SCHLUESSEL, '') ?? ''); });
      neuPruefen();
    },
    knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }],
  }).then((wert) => { offen = false; offenerDialog = null; appAbgemeldet = true; appTeil.beenden(); return wert; });
  offenerDialog = geschlossen;
  return geschlossen;
}

/** Für die Menüband-Aktion und Aufrufe aus der 3D-Ansicht: öffnet den Dialog und fängt Fehler ab. */
export function questDialogOeffnen() {
  try { return questDialog(); }
  catch (e) { meldung(e.message ?? String(e), 'fehler'); return Promise.resolve(null); }
}
