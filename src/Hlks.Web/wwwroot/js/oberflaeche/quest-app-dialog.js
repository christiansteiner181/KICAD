// Teil «Quest App» im Dialog «Quest verbinden» (Modul XR, Quest App P7; Entscheid Christian 23.10.2026: «KICAD installiert die App» per adb): Netzzugang ein/aus mit Hinweisen zu
// Netzprofil und Firewall (nur gelesen), Knopf «App installieren/aktualisieren» (über WLAN oder Kabel), gekoppelte Brillen mit «Widerrufen», Ersatzkopplung (Weg B) mit Code und
// «Bestätigen»/«Ablehnen», Ordner der abgelegten App. Keine Logik hier: alles läuft über /api/xr/… (XrEndpunkte.cs) und /api/quest/app/… (QuestAppEndpunkte.cs); die Texte und Schalter
// der Knöpfe stehen in quest-app-text.js. Namen und Kennungen der Brillen kommen aus dem Netz und werden nur über esc() ausgegeben.
// Der Dialog (quest-dialog.js) gibt den Zustand von GET /api/quest (adb, Brille) hinein und bekommt Bescheid, wenn sich dort etwas ändert.

import { holen, senden } from '../api.js';
import { bestaetigen } from './dialoge.js';
import { knoten } from './werkzeug.js';
import { benutzerLaden, benutzerSetzen, benutzerWert } from './benutzer.js';
import * as T from './quest-app-text.js';
import { brilleZeigenAn, brilleZeigenSetzen } from '../ansichten/xr-brille-einstellung.js';

const esc = T.esc;

/**
 * Erzeugt den Teil. optionen: { quest: () => Zustand von GET /api/quest oder null, beiAenderung: () => void (der Dialog liest dann /api/quest neu),
 * marke: App-Marke (T.KICAD_MARKE als Vorgabe; das XR-Labor gibt seine eigene mit: Name, Ports, Ablage, ohne Brillen-Schalter) }.
 * Gibt { knoten, aktualisieren(), beenden(), zustandNeu() } zurück: aktualisieren() zeichnet mit dem aktuellen Zustand des Dialogs neu, beenden() hält den Takt an.
 */
export function questAppTeil({ quest, beiAenderung = () => {}, beiGelesen = () => {}, marke = T.KICAD_MARKE }) {
  const appMarke = marke;   // nicht «m»: zeichneInstallieren hat ein eigenes m (Meldungsfeld)
  let zugang = null;      // GET /api/xr/zugang
  let app = null;         // GET /api/quest/app
  let netz = null;        // GET /api/quest/app/netz
  let laeuft = false;
  let weg = 'wlan';
  let wegGewaehlt = false;   // der Mensch hat gewählt: die Vorgabe nach der Brille ändert sie nicht mehr
  let ergebnis = null;    // Antwort von POST /api/quest/app/installieren
  let meldungText = null; // { art, text, vorschlag }
  let timer = null;
  let beendet = false;
  let zeichnung = '';     // zuletzt gezeichneter Zustand der Listen (vermeidet Flackern im Takt)

  const w = knoten(`<section class="qa" aria-label="Quest App">
    <b>Quest App</b>
    <div class="leise qd-klein">${esc(appMarke.einleitung)}</div>
    <div class="qa-zugang">
      <div class="qa-zeile"><span class="qa-lampe" aria-hidden="true"></span><span class="qa-zugang-text"></span>
        <button type="button" class="dlg-knopf qa-schalter" title="Öffnet bzw. schliesst den Zugang der App im Büronetz (nur dieser PC; Port ${esc(appMarke.port)}, nur gekoppelte Brillen)">Netzzugang einschalten</button></div>
      <ul class="qa-hinweise" aria-label="Hinweise zum Netz"></ul>
      <div class="leise qd-klein qa-firewall">${esc(T.firewallHinweis(appMarke))}</div>
    </div>
    <div class="qa-fassungen">
      <div class="qa-fz"><span class="qa-etikett leise">Abgelegte App</span><span class="qa-abgelegt"></span></div>
      <div class="qa-fz"><span class="qa-etikett leise">In der Brille</span><span class="qa-installiert"></span></div>
      <div class="leise qd-klein qa-vergleich"></div>
    </div>
    <div class="qa-box fehler qa-neukoppeln" role="alert" hidden><b>${esc(T.NEU_KOPPELN_HINWEIS)}</b></div>
    <div class="leise qd-klein qa-wegtext"></div>
    <div class="qa-installieren-zeile">
      ${T.WEGE.map((x) => `<label class="qa-weg" title="${esc(x.hinweis)}"><input type="radio" name="qa-weg" value="${esc(x.id)}"${x.id === 'wlan' ? ' checked' : ''}> ${esc(x.titel)}</label>`).join('')}
      <button type="button" class="dlg-knopf primaer qa-installieren" title="Installiert die abgelegte App per adb, koppelt die Brille und startet die App">App installieren</button>
    </div>
    <div class="leise qd-klein qa-grund"></div>
    <div class="qa-box qa-meldung" role="status" aria-live="polite" hidden></div>
    <ol class="qa-schritte" aria-label="Schritte der Installation" hidden></ol>
    <div class="qa-block"><b>Gekoppelte Brillen</b>
      <div class="qa-kopplungen" aria-label="Gekoppelte Brillen"></div></div>
    <div class="qa-block"><b>Ersatzkopplung (ohne Kabel)</b>
      <div class="leise qd-klein">${esc(T.ERSATZ_HINWEIS)}</div>
      <div class="qa-anfragen" aria-label="Offene Kopplungsanfragen"></div></div>
    ${appMarke.brillenZeigen ? `<div class="qa-block qa-anzeige"><label class="qd-schalter"><input type="checkbox" class="qa-zeigen" aria-label="Brillen in der 3D-Ansicht zeigen"> Verbundene Brillen in der 3D-Ansicht zeigen</label>
      <div class="leise qd-klein">Kopf mit Blickrichtung, Hände und Name, mit der Pose aus der Brille. Derselbe Schalter wie Register Ansicht › Fenster › «Brille zeigen».</div></div>` : ''}
    ${appMarke.ablageSchluessel ? `<div class="qd-zeile qa-ablage"><label class="qd-etikett" for="qa-ordner">Ordner der App</label>
      <input id="qa-ordner" type="text" class="qa-ordner" spellcheck="false" autocomplete="off" data-kein-enter="1" placeholder="leer = ${esc(appMarke.ablageVorgabe)}" aria-label="Ordner der abgelegten App">
      <button type="button" class="dlg-knopf qa-ordner-speichern">Speichern</button></div>
    <div class="leise qd-klein">Die App baut und legt dort ab: <code>${esc(appMarke.bereitstellen)}</code> (APK und version.json). Der Ordner muss auf diesem Rechner liegen.</div>`
    : `<div class="leise qd-klein qa-ablage-hinweis">Die App liegt in <code>${esc(appMarke.ablageVorgabe)}</code>; bauen und ablegen mit <code>${esc(appMarke.bereitstellen)}</code> (APK und version.json).</div>`}
  </section>`);
  const q = (s) => w.querySelector(s);

  // ------------------------------------------------------------------------------------------ Anzeige

  function zeichneZugang() {
    const a = T.zugangAnzeige(zugang, appMarke);
    const z = q('.qa-zeile');
    z.dataset.stufe = a.stufe;
    q('.qa-zugang-text').innerHTML = `<b>${esc(a.titel)}</b> <span class="leise">${esc(a.text)}</span>`;
    const s = q('.qa-schalter');
    s.textContent = T.schalterText(zugang);
    s.disabled = !zugang || laeuft;
    const h = T.hinweisListe(netz);
    q('.qa-hinweise').innerHTML = h.map((x) => `<li class="qa-hinweis ${esc(x.stufe)}" data-stufe="${esc(x.stufe)}">${esc(x.text)}</li>`).join('');
  }

  function zeichneFassungen() {
    q('.qa-abgelegt').textContent = T.abgelegtText(app);
    q('.qa-installiert').textContent = `${T.brilleText(app?.brille)} – ${T.installiertText(app, appMarke)}`;
    q('.qa-vergleich').textContent = T.vergleichText(app?.vergleich);
  }

  function zeichneInstallieren() {
    const k = T.installierenKnopf({ q: quest(), app, zugang, laeuft, weg, marke: appMarke });
    const knopf = q('.qa-installieren');
    knopf.disabled = !k.frei;
    knopf.textContent = T.installierenTitel(app, zugang);
    q('.qa-neukoppeln').hidden = !T.neuKoppeln(app, zugang);
    q('.qa-wegtext').textContent = T.appWegText(app, appMarke);
    q('.qa-grund').textContent = k.frei ? '' : k.grund;
    for (const r of w.querySelectorAll('input[name="qa-weg"]')) { r.disabled = laeuft; r.checked = r.value === weg; }
    const m = q('.qa-meldung');
    m.hidden = !meldungText;
    if (meldungText) {
      m.className = `qa-box qa-meldung ${meldungText.art === 'fehler' ? 'fehler' : meldungText.art === 'warnung' ? 'warnung' : ''}`;
      m.innerHTML = `<b>${esc(meldungText.text)}</b>${meldungText.vorschlag ? `<div class="qd-vorschlag">Vorschlag: ${esc(meldungText.vorschlag)}</div>` : ''}`;
    }
    const s = T.schritteListe(ergebnis);
    const liste = q('.qa-schritte');
    liste.hidden = s.length === 0;
    liste.innerHTML = s.map((x) => `<li class="qa-schritt ${x.ok ? 'ok' : 'schlecht'}" data-schritt="${esc(x.id)}" data-ok="${x.ok ? '1' : '0'}">${esc(x.text)}</li>`).join('');
  }

  /** Listen der Kopplungen und Anfragen; nur neu zeichnen, wenn sich etwas geändert hat (der Takt läuft alle 2 s). */
  function zeichneListen(erzwingen = false) {
    const kopplungen = T.kopplungsListe(zugang);
    const anfragen = T.anfragenListe(zugang);
    const stand = JSON.stringify([kopplungen, anfragen.map((a) => ({ ...a, restSekunden: null })), laeuft]);
    if (!erzwingen && stand === zeichnung) { restSekundenZeichnen(anfragen); return; }
    zeichnung = stand;
    q('.qa-kopplungen').innerHTML = kopplungen.length === 0
      ? '<div class="leise qa-keine">Noch keine Brille gekoppelt. «App installieren» koppelt sie.</div>'
      : kopplungen.map((k) => `<div class="qa-kopplung" data-id="${esc(k.id)}"><span class="qa-lampe ${k.verbunden ? 'verbunden' : ''}" aria-hidden="true"></span>`
        + `<span class="qa-kopplung-text">${esc(T.kopplungText(k))}</span><span class="leise qa-weg-art">${esc(k.weg)}</span>`
        + `<button type="button" class="dlg-knopf qa-widerrufen" data-id="${esc(k.id)}" data-name="${esc(k.name)}" title="Widerruft den Schlüssel dieser Brille; sie wird sofort getrennt und muss neu gekoppelt werden"${laeuft ? ' disabled' : ''}>Widerrufen</button></div>`).join('');
    const warnung = T.anfragenWarnung(anfragen);
    q('.qa-anfragen').innerHTML = (warnung ? `<div class="qa-box fehler qa-anfragen-warnung" role="alert"><b>${esc(warnung)}</b></div>` : '')
      + (anfragen.length === 0 ? '<div class="leise qa-keine">Keine offene Anfrage.</div>'
        : anfragen.map((a) => `<div class="qa-anfrage" data-anfrage="${esc(a.anfrage)}" data-code="${esc(a.code ?? '')}">`
          + `<div class="qa-anfrage-kopf"><b class="qa-anfrage-name">${esc(a.name)}</b> <span class="leise">Kennung ${esc(a.brille)}${a.ip ? ` · von ${esc(a.ip)}` : ''}</span></div>`
          + (a.code ? `<div class="qa-code" aria-label="Vergleichscode">${esc(a.code)}</div>` : `<div class="leise qa-wartet">${esc(T.wartetText)}</div>`)
          + `<div class="qa-anfrage-rest leise" data-rest="${a.restSekunden ?? ''}"></div>`
          + `<div class="qa-anfrage-knoepfe"><button type="button" class="dlg-knopf primaer qa-bestaetigen" data-anfrage="${esc(a.anfrage)}"${T.bestaetigenFrei(a, laeuft) ? '' : ' disabled'} title="Nur klicken, wenn die Brille genau denselben Code zeigt">Codes stimmen überein – koppeln</button>`
          + `<button type="button" class="dlg-knopf qa-ablehnen" data-anfrage="${esc(a.anfrage)}"${laeuft ? ' disabled' : ''}>Ablehnen</button></div></div>`).join(''));
    restSekundenZeichnen(anfragen);
  }

  function restSekundenZeichnen(anfragen) {
    for (const a of anfragen) {
      const el = w.querySelector(`.qa-anfrage[data-anfrage="${CSS.escape(a.anfrage)}"] .qa-anfrage-rest`);
      if (el) el.textContent = a.restSekunden === null ? '' : a.restSekunden > 0 ? `Gilt noch ${a.restSekunden} s.` : 'Abgelaufen.';
    }
  }

  function zeichne(erzwingen = false) {
    if (beendet) return;
    zeichneZugang();
    zeichneFassungen();
    zeichneInstallieren();
    zeichneListen(erzwingen);
    const z = q('.qa-zeigen');
    if (z) z.checked = brilleZeigenAn();
  }

  // ------------------------------------------------------------------------------------------ Server

  async function lies(pfad) { try { return await holen(pfad); } catch { return null; } }

  /** Liest Zugang, App und Netz neu (jedes für sich: fällt eines aus, bleiben die anderen). */
  async function zustandNeu({ netzNeu = false } = {}) {
    const [z, a, n] = await Promise.all([lies('/api/xr/zugang'), lies('/api/quest/app'), lies(`/api/quest/app/netz${netzNeu ? '?neu=1' : ''}`)]);
    if (beendet) return;
    if (z) zugang = z;
    if (a) app = a;
    if (n) netz = n;
    if (a) beiGelesen(app);
    if (!wegGewaehlt) weg = T.wegVorgabe(app?.brille);
    zeichne();
  }

  async function lauf(arbeit) {
    if (laeuft) return;
    laeuft = true;
    meldungText = null;
    zeichne(true);
    try { await arbeit(); }
    catch (err) { meldungText = { art: 'fehler', text: err?.message || String(err), vorschlag: err?.vorschlag || null }; }
    finally {
      laeuft = false;
      if (!beendet) { await zustandNeu(); zeichne(true); }
    }
  }

  async function taktSchritt() {
    if (beendet || laeuft) return;
    const z = await lies('/api/xr/zugang');
    if (z && !beendet && !laeuft) { zugang = z; zeichne(); }
  }

  w.addEventListener('click', (e) => {
    const t = e.target.closest?.('button');
    if (!t || t.disabled) return;
    if (t.matches('.qa-schalter')) {
      lauf(async () => {
        zugang = await senden('/api/xr/zugang', { an: T.schalterWert(zugang) });
        netz = await lies('/api/quest/app/netz?neu=1');
        meldungText = { art: 'ok', text: zugang.an ? 'Der Netzzugang ist eingeschaltet.' : 'Der Netzzugang ist ausgeschaltet; verbundene Brillen sind getrennt.' };
      });
    } else if (t.matches('.qa-installieren')) {
      lauf(async () => {
        ergebnis = null;
        ergebnis = await senden('/api/quest/app/installieren', { weg });
        meldungText = T.ergebnisMeldung(ergebnis);
        beiAenderung();
      });
    } else if (t.matches('.qa-widerrufen')) {
      const id = t.dataset.id;
      const name = t.dataset.name;
      bestaetigen(`Die Kopplung von «${name}» widerrufen? Die Brille wird getrennt und muss neu gekoppelt werden («App installieren»).`, { ja: 'Widerrufen', gefahr: true, titel: 'Kopplung widerrufen' }).then((ok) => {
        if (!ok) return;
        lauf(async () => {
          await senden('/api/xr/kopplung/widerrufen', { kopplung: id });
          meldungText = { art: 'ok', text: `Die Kopplung von «${name}» ist widerrufen. Die App in der Brille meldet «neu koppeln» und verbindet nicht mehr: «App neu koppeln» koppelt sie wieder.` };
        });
      });
    } else if (t.matches('.qa-bestaetigen')) {
      const anfrage = t.dataset.anfrage;
      lauf(async () => {
        await senden('/api/xr/kopplung/bestaetigen', { anfrage });
        meldungText = { art: 'ok', text: 'Gekoppelt. Die Brille holt ihr Paket gleich ab.' };
      });
    } else if (t.matches('.qa-ablehnen')) {
      const anfrage = t.dataset.anfrage;
      lauf(async () => {
        await senden('/api/xr/kopplung/ablehnen', { anfrage });
        meldungText = { art: 'ok', text: 'Die Anfrage ist abgelehnt.' };
      });
    } else if (t.matches('.qa-ordner-speichern') && appMarke.ablageSchluessel) {
      const pfad = q('.qa-ordner').value.trim();
      lauf(async () => {
        await benutzerSetzen(appMarke.ablageSchluessel, pfad === '' ? null : pfad);
        meldungText = { art: 'ok', text: pfad === '' ? 'Der Ordner ist zurückgesetzt.' : 'Der Ordner der App ist gespeichert.' };
      });
    }
  });
  w.addEventListener('change', (e) => {
    const r = e.target;
    if (r?.matches?.('input[name="qa-weg"]')) { weg = r.value; wegGewaehlt = true; zeichneInstallieren(); }
    else if (r?.matches?.('.qa-zeigen')) brilleZeigenSetzen(r.checked).catch((err) => { meldungText = { art: 'fehler', text: err?.message || String(err) }; zeichne(); });
  });
  q('.qa-ordner')?.addEventListener('keydown', (ev) => { if (ev.key === 'Enter') { ev.preventDefault(); q('.qa-ordner-speichern').click(); } });

  zeichne(true);
  timer = setInterval(taktSchritt, T.ZUGANG_TAKT_MS);
  // Benutzereinstellungen nur, wenn der Teil sie zeigt (Ordner der App, Brillen-Schalter); das XR-Labor zeigt beides nicht und liest sie nicht
  if (appMarke.ablageSchluessel || appMarke.brillenZeigen) {
    benutzerLaden().finally(() => {
      if (beendet) return;
      const o = q('.qa-ordner');
      if (o) o.value = String(benutzerWert(appMarke.ablageSchluessel, '') ?? '');
      zeichne();
    });
  }

  return {
    knoten: w,
    aktualisieren: () => zeichne(),
    zustandNeu,
    beenden() { beendet = true; clearInterval(timer); timer = null; },
  };
}
