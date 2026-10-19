// Dialog «Serien-PDF» (entwurf/serien-pdf.md): links die Plansätze (automatisch je Gewerk, eigene), rechts die Blätter des gewählten Satzes mit
// Häkchen, Nummer, Titel, Format und Gewerk (Vorschlag grau mit «bestätigen»), Reihenfolge per Ziehen oder ↑↓; unten Anlass, Ordner
// (Vorschlag des Kerns, änderbar), «zusätzlich Einzel-PDFs» und «Exportieren»; danach Fortschritt je Blatt und die Dateiliste mit «Ordner öffnen».
// Alles läuft über die Kern-Tür: Abfragen plansaetze und plansatz_pdf, Befehle plansatz_setzen, planblatt_gewerk_setzen und element_loeschen.
// Das Gewerk-Vorschlagen, die Reihenfolge nach Plannummer, Dateinamen und Ordner rechnet der Kern; hier wird nur bedient (Texte und
// Parameter: serienpdf-text.js).

import { abfrage, senden } from '../api.js';
import { zeigen, meldung, bestaetigen, eingabe } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { knoten, entprellen } from './werkzeug.js';
import * as T from './serienpdf-text.js';

const esc = T.esc;

/**
 * Öffnet den Dialog. opt.plansatz: Schlüssel («auto:sanitaer», «PS-1») oder Gewerk, der zuerst gezeigt wird.
 * Gibt zurück, wenn der Dialog geschlossen wird.
 */
export async function serienPdfDialog(opt = {}) {
  let daten;
  try { daten = await abfrage('plansaetze', {}); }
  catch (e) { meldung(T.fehlerText(e), 'fehler'); return; }
  if ((daten.plansaetze ?? []).length === 0) {
    meldung('Es gibt noch keine Planblätter.', 'info', { vorschlag: 'Zuerst ein Planblatt anlegen (Register Ansicht › Planblatt).' });
    return;
  }

  let aktuell = T.findeSatz(daten, opt.plansatz ?? '', opt.plansatz ?? null)?.schluessel ?? daten.plansaetze[0].schluessel;
  const zust = new Map();          // Schlüssel → { zeilen, form, gewerk } (noch nicht gespeicherte Eingaben des Menschen)
  let dragVon = -1;
  let laeuft = false;

  const w = knoten(`<div class="sp">
    <div class="sp-links"></div>
    <div class="sp-rechts">
      <div class="sp-bearbeiten">
        <div class="sp-kopf"><b class="sp-titel"></b><span class="sp-zaehl leise"></span><span class="sp-kopf-knoepfe"></span></div>
        <div class="sp-tabelle"></div>
        <div class="sp-hinweise"></div>
        <div class="sp-ausgabe"></div>
        <div class="sp-umfang leise"></div>
      </div>
      <div class="sp-lauf" hidden></div>
    </div>
  </div>`);
  const q = (s) => w.querySelector(s);

  const satz = () => T.findeSatz(daten, aktuell);
  function zustandVon(s) {
    if (!zust.has(s.schluessel)) zust.set(s.schluessel, { zeilen: T.zeilen(s), form: T.formularAus(s), gewerk: s.gewerk });
    return zust.get(s.schluessel);
  }

  // ------------------------------------------------------------------------------------------ Zeichnen

  function linksZeichnen() {
    const { automatische, eigene } = T.satzListe(daten);
    const eintrag = (s) => {
      const z = T.satzZeile(s);
      return `<button type="button" class="sp-satz${s.schluessel === aktuell ? ' aktiv' : ''}${z.angeboten ? ' angeboten' : ''}" data-schluessel="${esc(s.schluessel)}" title="${esc(s.name)}${z.angeboten ? ' (wird beim ersten Export angelegt)' : ''}">`
        + `<span class="sp-satz-name">${esc(s.name)}</span><span class="sp-satz-zahl">${esc(z.zahlText)}</span></button>`;
    };
    q('.sp-links').innerHTML = `<div class="sp-gruppe">Automatisch je Gewerk</div>${automatische.map(eintrag).join('') || '<div class="leise sp-leer">keine</div>'}`
      + `<div class="sp-gruppe">Von Hand</div>${eigene.map(eintrag).join('') || '<div class="leise sp-leer">keine</div>'}`
      + '<button type="button" class="dlg-knopf sp-neu">Neuer Plansatz …</button>';
  }

  function gewerkOptionen(z) {
    return (daten.gewerke ?? []).map((g) => `<option value="${esc(g.gewerk)}"${g.gewerk === z.gewerk ? ' selected' : ''}>${esc(g.name)}</option>`).join('');
  }

  function tabelleZeichnen() {
    const s = satz();
    const st = zustandVon(s);
    const hand = !s.automatisch;
    const zeilen = st.zeilen.map((z, i) => `<tr class="sp-zeile${z.dabei ? '' : ' aus'}" draggable="true" data-i="${i}" data-pb="${esc(z.planblatt)}">`
      + '<td class="sp-griff" title="Ziehen, um die Reihenfolge zu ändern">⠿</td>'
      + `<td class="sp-c"><input type="checkbox" class="sp-dabei" aria-label="${esc(z.nummer || z.planblatt)} exportieren"${z.dabei ? ' checked' : ''}></td>`
      + `<td class="sp-nr">${esc(z.nummer || z.planblatt)}</td><td class="sp-ti" title="${esc(z.planblatt)} · ${z.ansichten} ${z.ansichten === 1 ? 'Ansicht' : 'Ansichten'}">${esc(z.titel)}${z.ansichten === 0 ? ' <span class="sp-ohne">ohne Ansicht</span>' : ''}</td>`
      + `<td class="sp-fo">${esc(z.format)} ${esc(z.ausrichtung)}</td>`
      + `<td class="sp-gw ${z.gewerkHerkunft}" title="${esc(T.gewerkTip(z))}"><select class="sp-gewerk" aria-label="Gewerk von ${esc(z.nummer || z.planblatt)}">${gewerkOptionen(z)}</select>`
      + (z.gewerkHerkunft === 'vorschlag' ? '<button type="button" class="sp-bestaetigen" title="Vorschlag als Gewerk des Plans festhalten">bestätigen</button>' : '') + '</td>'
      + `<td class="sp-an"><button type="button" class="sp-hoch" title="Nach oben"${i === 0 ? ' disabled' : ''}>↑</button><button type="button" class="sp-runter" title="Nach unten"${i === st.zeilen.length - 1 ? ' disabled' : ''}>↓</button>`
      + (hand ? '<button type="button" class="sp-weg" title="Aus diesem Plansatz nehmen">✕</button>' : '') + '</td></tr>');
    q('.sp-tabelle').innerHTML = `<table class="tab sp-tab"><thead><tr><th></th><th></th><th>Nummer</th><th>Titel</th><th>Format</th><th>Gewerk</th><th></th></tr></thead>`
      + `<tbody>${zeilen.join('') || '<tr><td colspan="7" class="leise">Dieser Plansatz hat noch keine Pläne.</td></tr>'}</tbody></table>`;
    if (hand) {
      const mehr = s.weitere ?? [];
      q('.sp-tabelle').insertAdjacentHTML('beforeend', `<div class="sp-hinzu-zeile"><label>Plan hinzufügen <select class="sp-hinzu"><option value="">${mehr.length ? '– wählen –' : '(alle Pläne sind dabei)'}</option>`
        + `${mehr.map((b) => `<option value="${esc(b.planblatt)}">${esc(b.nummer || b.planblatt)} · ${esc(b.titel)} (${esc(b.format)})</option>`).join('')}</select></label></div>`);
    }
  }

  function kopfZeichnen() {
    const s = satz();
    const st = zustandVon(s);
    q('.sp-titel').textContent = T.satzTitel(s);
    q('.sp-zaehl').textContent = T.zaehlText(st.zeilen);
    q('.sp-kopf-knoepfe').innerHTML = '<button type="button" class="dlg-knopf klein sp-alle">Alle</button><button type="button" class="dlg-knopf klein sp-keine">Keine</button>'
      + (!s.automatisch ? '<button type="button" class="dlg-knopf klein sp-loeschen">Plansatz löschen</button>'
        : s.angelegt ? '<button type="button" class="dlg-knopf klein sp-zuruecksetzen" title="Löscht die gespeicherte Auswahl, Reihenfolge und Einstellung; der Satz wird danach wieder neu angeboten">Zurücksetzen</button>' : '');
  }

  function ausgabeZeichnen() {
    const s = satz();
    const f = zustandVon(s).form;
    q('.sp-ausgabe').innerHTML = `<label class="sp-f"><span>Anlass</span><input type="text" class="sp-anlass" value="${esc(f.anlass)}" placeholder="z. B. Revisionspläne" aria-label="Anlass"></label>`
      + `<label class="sp-f sp-f-ordner"><span>Ordner</span><span class="sp-f-zeile"><input type="text" class="sp-ordner" value="${esc(f.ordner)}" aria-label="Ordner" spellcheck="false">`
      + '<button type="button" class="dlg-knopf klein sp-vorschlag" title="Den vorgeschlagenen Ordner einsetzen">Vorschlag</button><button type="button" class="dlg-knopf klein sp-durchsuchen">Durchsuchen …</button></span></label>'
      + `<label class="haken sp-einzeln-zeile"><input type="checkbox" class="sp-einzeln"${f.einzeln ? ' checked' : ''}> zusätzlich Einzel-PDFs (je Plan ein PDF mit H+K-Namen)</label>`
      + `<div class="sp-sammel leise">Sammel-PDF: <span class="sp-sammel-name">${esc(s.vorschlag?.sammelDatei ?? '')}</span></div>`;
  }

  function hinweiseZeichnen() {
    const s = satz();
    const h = s.hinweise ?? [];
    q('.sp-hinweise').innerHTML = h.length ? `<ul class="hinweisliste">${h.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : '';
  }

  function umfangZeichnen() {
    const st = zustandVon(satz());
    q('.sp-umfang').textContent = T.umfangText(st.zeilen, st.form.einzeln);
    q('.sp-zaehl').textContent = T.zaehlText(st.zeilen);
  }

  function alleZeichnen() {
    linksZeichnen(); kopfZeichnen(); tabelleZeichnen(); hinweiseZeichnen(); ausgabeZeichnen(); umfangZeichnen();
  }

  // ------------------------------------------------------------------------------------------ Daten nachladen

  /** Holt die Pläne neu; die Eingaben des Menschen (Reihenfolge, Häkchen, Anlass, Ordner) bleiben. */
  async function nachladen(neuerSchluessel = null) {
    const neu = await abfrage('plansaetze', {});
    const alt = zust.size ? [...zust] : [];
    const alterAktueller = satz();
    daten = neu;
    zust.clear();
    for (const [schl, st] of alt) {
      const s = T.findeSatz(neu, schl, st.gewerk);
      if (!s || zust.has(s.schluessel)) continue;
      zust.set(s.schluessel, { ...st, zeilen: T.zusammenfuehren(st.zeilen, T.zeilen(s)), gewerk: s.gewerk });
      if (schl === aktuell) aktuell = s.schluessel;
    }
    if (neuerSchluessel) aktuell = T.findeSatz(neu, neuerSchluessel)?.schluessel ?? aktuell;
    if (!satz()) aktuell = T.findeSatz(neu, alterAktueller?.schluessel ?? '', alterAktueller?.gewerk)?.schluessel ?? neu.plansaetze[0]?.schluessel;
  }

  // ------------------------------------------------------------------------------------------ Bedienung

  function waehlen(schluessel) {
    if (laeuft) return;
    aktuell = schluessel;
    lauf().hidden = true; q('.sp-bearbeiten').hidden = false;
    alleZeichnen();
  }
  const lauf = () => q('.sp-lauf');

  const vorschlagNeu = entprellen(async () => {
    const s = satz(), st = zustandVon(s);
    try {
      const r = await abfrage('plansaetze', { plansatz: s.schluessel, anlass: st.form.anlass });
      const n = r.plansaetze?.[0];
      if (!n || s !== satz()) return;
      s.vorschlag = n.vorschlag;
      if (!st.form.ordnerVonHand) { st.form.ordner = n.vorschlag.ordner; const f = q('.sp-ordner'); if (f) f.value = st.form.ordner; }
      const sn = q('.sp-sammel-name'); if (sn) sn.textContent = n.vorschlag.sammelDatei;
    } catch (e) { console.error(e); }
  }, 250);

  async function gewerkSetzen(planblatt, gewerk) {
    const r = await sende('planblatt_gewerk_setzen', { planblaetter: [planblatt], gewerk });
    if (!ergebnisMelden(r, `${planblatt}: Gewerk gesetzt.`)) return;
    await nachladen();
    alleZeichnen();
  }

  w.addEventListener('click', async (e) => {
    if (laeuft) return;
    const t = e.target;
    const satzKnopf = t.closest('.sp-satz');
    if (satzKnopf) { waehlen(satzKnopf.dataset.schluessel); return; }
    if (t.closest('.sp-neu')) { await neuerSatz(); return; }
    const s = satz();
    if (!s) return;
    const st = zustandVon(s);
    const zeile = t.closest('tr.sp-zeile');
    const i = zeile ? Number(zeile.dataset.i) : -1;
    if (t.closest('.sp-hoch')) { st.zeilen = T.hoch(st.zeilen, i); tabelleZeichnen(); return; }
    if (t.closest('.sp-runter')) { st.zeilen = T.runter(st.zeilen, i); tabelleZeichnen(); return; }
    if (t.closest('.sp-alle') || t.closest('.sp-keine')) {
      const an = !!t.closest('.sp-alle');
      st.zeilen = st.zeilen.map((z) => ({ ...z, dabei: an }));
      tabelleZeichnen(); umfangZeichnen(); return;
    }
    if (t.closest('.sp-bestaetigen')) { await gewerkSetzen(zeile.dataset.pb, st.zeilen[i].gewerk); return; }
    if (t.closest('.sp-weg')) {
      const rest = st.zeilen.filter((_, k) => k !== i).map((z) => z.planblatt);
      const r = await sende('plansatz_setzen', T.blattlisteParameter(s, rest));
      if (ergebnisMelden(r, 'Plan aus dem Plansatz genommen.')) { await nachladen(); alleZeichnen(); }
      return;
    }
    if (t.closest('.sp-loeschen') || t.closest('.sp-zuruecksetzen')) {
      const zurueck = !!t.closest('.sp-zuruecksetzen');
      const ok = await bestaetigen(zurueck ? `Gespeicherte Auswahl, Reihenfolge und Einstellung von «${s.name}» löschen? Der Satz wird danach wieder neu angeboten.` : `Plansatz «${s.name}» löschen? Die Pläne bleiben.`,
        { ja: zurueck ? 'Zurücksetzen' : 'Löschen', gefahr: !zurueck, titel: zurueck ? 'Plansatz zurücksetzen' : 'Plansatz löschen' });
      if (!ok) return;
      const r = await sende('element_loeschen', { element: s.plansatz });
      if (ergebnisMelden(r, zurueck ? 'Plansatz zurückgesetzt.' : 'Plansatz gelöscht.')) {
        zust.delete(s.schluessel);
        const gewerk = s.gewerk;
        await nachladen();
        aktuell = zurueck ? (T.findeSatz(daten, 'auto:' + gewerk)?.schluessel ?? daten.plansaetze[0].schluessel) : daten.plansaetze[0].schluessel;
        alleZeichnen();
      }
      return;
    }
    if (t.closest('.sp-vorschlag')) {
      st.form.ordnerVonHand = false; st.form.ordner = s.vorschlag?.ordner ?? '';
      q('.sp-ordner').value = st.form.ordner; return;
    }
    if (t.closest('.sp-durchsuchen')) {
      const { ordnerWaehlen } = await import('./ordner-browser.js');
      const gewaehlt = await ordnerWaehlen({ titel: 'Ausgabeordner wählen', modus: 'ziel', vorgabe: st.form.ordner, okText: 'Wählen' });
      if (gewaehlt) { st.form.ordner = gewaehlt; st.form.ordnerVonHand = gewaehlt !== s.vorschlag?.ordner; q('.sp-ordner').value = gewaehlt; }
    }
  });

  w.addEventListener('change', async (e) => {
    if (laeuft) return;
    const t = e.target;
    const s = satz();
    if (!s) return;
    const st = zustandVon(s);
    const zeile = t.closest('tr.sp-zeile');
    const i = zeile ? Number(zeile.dataset.i) : -1;
    if (t.matches('.sp-dabei')) {
      st.zeilen[i] = { ...st.zeilen[i], dabei: t.checked };
      zeile.classList.toggle('aus', !t.checked); umfangZeichnen();
    } else if (t.matches('.sp-gewerk')) {
      await gewerkSetzen(zeile.dataset.pb, t.value);
    } else if (t.matches('.sp-einzeln')) {
      st.form.einzeln = t.checked; umfangZeichnen();
    } else if (t.matches('.sp-hinzu') && t.value) {
      const rest = [...st.zeilen.map((z) => z.planblatt), t.value];
      const r = await sende('plansatz_setzen', T.blattlisteParameter(s, rest));
      if (ergebnisMelden(r, 'Plan zum Plansatz hinzugefügt.')) { await nachladen(); alleZeichnen(); }
    }
  });

  w.addEventListener('input', (e) => {
    const t = e.target;
    const s = satz();
    if (!s) return;
    const st = zustandVon(s);
    if (t.matches('.sp-anlass')) { st.form.anlass = t.value; vorschlagNeu(); }
    else if (t.matches('.sp-ordner')) { st.form.ordner = t.value; st.form.ordnerVonHand = t.value !== (s.vorschlag?.ordner ?? ''); }
  });

  // Ziehen (HTML5 drag & drop); ↑↓ tun dasselbe
  w.addEventListener('dragstart', (e) => {
    const z = e.target.closest?.('tr.sp-zeile');
    if (!z || laeuft) return;
    dragVon = Number(z.dataset.i);
    z.classList.add('zieht');
    if (e.dataTransfer) { e.dataTransfer.effectAllowed = 'move'; try { e.dataTransfer.setData('text/plain', z.dataset.pb); } catch { /* egal */ } }
  });
  w.addEventListener('dragover', (e) => {
    const z = e.target.closest?.('tr.sp-zeile');
    if (!z || dragVon < 0) return;
    e.preventDefault();
    w.querySelectorAll('tr.sp-zeile.ueber').forEach((x) => x.classList.remove('ueber'));
    z.classList.add('ueber');
  });
  w.addEventListener('drop', (e) => {
    const z = e.target.closest?.('tr.sp-zeile');
    if (!z || dragVon < 0) return;
    e.preventDefault();
    const st = zustandVon(satz());
    st.zeilen = T.schieben(st.zeilen, dragVon, Number(z.dataset.i));
    dragVon = -1;
    tabelleZeichnen();
  });
  w.addEventListener('dragend', () => {
    dragVon = -1;
    w.querySelectorAll('tr.sp-zeile.zieht, tr.sp-zeile.ueber').forEach((x) => x.classList.remove('zieht', 'ueber'));
  });

  async function neuerSatz() {
    const name = await eingabe('Neuer Plansatz', 'Name', '', { platzhalter: 'z. B. Plansatz Bauherr' });
    if (!name) return;
    const r = await sende('plansatz_setzen', { name, blaetter: [] });
    if (!ergebnisMelden(r, `Plansatz «${name}» angelegt.`)) return;
    await nachladen();
    const neu = daten.plansaetze.find((s) => s.name === name);
    if (neu) aktuell = neu.schluessel;
    alleZeichnen();
  }

  // ------------------------------------------------------------------------------------------ Exportieren

  function laufZeigen(s, st) {
    const gew = T.gewaehlte(st.zeilen);
    q('.sp-bearbeiten').hidden = true;
    const l = lauf();
    l.hidden = false;
    l.innerHTML = `<div class="sp-lauf-kopf"><span class="spinner" aria-hidden="true"></span><b class="sp-lauf-titel">${esc(s.name)} wird geschrieben …</b><span class="sp-zeit leise">0 s</span></div>`
      + `<ol class="sp-fortschritt">${gew.map((z) => `<li data-pb="${esc(z.planblatt)}" class="wartet"><span class="sp-marke" aria-hidden="true">…</span><span class="sp-f-nr">${esc(z.nummerKurz || z.nummer)}</span> ${esc(z.titel)}<span class="sp-f-info leise">${esc(z.format)}</span></li>`).join('')}</ol>`
      + '<div class="sp-lauf-unten"></div>';
    const t0 = Date.now();
    const timer = setInterval(() => { const z = l.querySelector('.sp-zeit'); if (z) z.textContent = `${Math.round((Date.now() - t0) / 1000)} s`; }, 500);
    return () => clearInterval(timer);
  }

  function fertigZeigen(a, s) {
    const l = lauf();
    l.querySelector('.sp-lauf-kopf').innerHTML = `<b class="sp-lauf-titel ok">Fertig: ${esc(s.name)}</b>`;
    for (const li of l.querySelectorAll('.sp-fortschritt li')) {
      li.className = 'fertig';
      li.querySelector('.sp-marke').textContent = '✓';
    }
    l.querySelector('.sp-lauf-unten').innerHTML = `<p class="sp-ergebnis">${esc(T.ergebnisText(a))}</p>`
      + `<p class="sp-ordnerpfad"><span class="leise">Ordner</span> <code class="sp-ordner-text">${esc(a.ordner)}</code></p>${T.dateiListeHtml(a)}`
      + ((a.hinweise ?? []).length ? `<ul class="hinweisliste">${a.hinweise.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : '')
      + '<p class="sp-lauf-knoepfe"><button type="button" class="dlg-knopf primaer sp-ordner-oeffnen">Ordner öffnen</button><button type="button" class="dlg-knopf sp-zurueck">Zurück zur Auswahl</button></p>';
    l.querySelector('.sp-ordner-oeffnen').onclick = async () => {
      try { await senden('/api/ordner/zeigen', { pfad: a.ordner }); }
      catch (e) { meldung(`Der Ordner lässt sich nicht öffnen: ${T.fehlerText(e)}`, 'warnung', { vorschlag: a.ordner }); }
    };
    l.querySelector('.sp-zurueck').onclick = () => { l.hidden = true; q('.sp-bearbeiten').hidden = false; alleZeichnen(); };
  }

  function fehlerZeigen(e) {
    const l = lauf();
    l.querySelector('.sp-lauf-kopf').innerHTML = '<b class="sp-lauf-titel fehler">Nicht geschrieben</b>';
    for (const li of l.querySelectorAll('.sp-fortschritt li')) { li.className = 'abgebrochen'; li.querySelector('.sp-marke').textContent = '–'; }
    l.querySelector('.sp-lauf-unten').innerHTML = `<div class="fehlerkasten" role="alert"><b>${esc(e?.message ?? String(e))}</b>${e?.vorschlag ? `<div>Vorschlag: ${esc(e.vorschlag)}</div>` : ''}</div>`
      + '<p class="sp-lauf-knoepfe"><button type="button" class="dlg-knopf sp-zurueck">Zurück zur Auswahl</button></p>';
    l.querySelector('.sp-zurueck').onclick = () => { l.hidden = true; q('.sp-bearbeiten').hidden = false; };
  }

  async function exportieren(ctrl) {
    if (laeuft) return;
    const s = satz();
    if (!s) return;
    const st = zustandVon(s);
    const pr = T.exportPruefen(st.zeilen, st.form);
    if (pr !== true) { ctrl.fehler(pr); return; }
    const par = T.exportParameter(s, st.zeilen, st.form);
    // erst prüfen (schreibt nichts): liegen die Dateien schon da, fragt der Dialog vor dem Überschreiben
    let vor;
    try { vor = await abfrage('plansatz_pdf', { ...par, pruefen: true }); }
    catch (e) { ctrl.fehler(T.fehlerText(e)); return; }
    let ueberschreiben = false;
    if ((vor.vorhanden ?? []).length > 0) {
      if (!await bestaetigen(T.ueberschreibenText(vor), { ja: 'Überschreiben', titel: 'Dateien vorhanden', gefahr: true })) return;
      ueberschreiben = true;
    }
    laeuft = true;
    const stopp = laufZeigen(s, st);
    try {
      const a = await abfrage('plansatz_pdf', { ...par, ueberschreiben });
      stopp();
      fertigZeigen(a, s);
      laeuft = false;
      // der Satz ist jetzt angelegt bzw. gespeichert: neu laden; die Eingaben sind nun gespeichert
      zust.delete(s.schluessel);
      await nachladen(a.plansatz);
      linksZeichnen();
      ctrl.fehler(null);
    } catch (e) {
      stopp();
      laeuft = false;
      fehlerZeigen(e);
    }
  }

  alleZeichnen();
  return zeigen({
    titel: 'Serien-PDF', inhalt: w, breite: 1120, klasse: 'dlg-hoch serienpdf',
    beiOeffnen: () => { w.closest('.dlg')?.querySelector('.dlg-knopf.primaer')?.focus?.(); },
    knoepfe: [{ text: 'Schliessen', wert: true },
      { text: 'Exportieren', primaer: true, schliesst: false, klick: (ctrl) => exportieren(ctrl) }],
  });
}
