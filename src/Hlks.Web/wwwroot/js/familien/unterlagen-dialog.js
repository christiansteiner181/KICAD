// Dialog «Familie aus Unterlagen» (Modul 2, C2). Schritt 1: Herstellerunterlagen hineinziehen (mehrere zugleich), hochladen, mit unterlage_einfuegen
// (zweck «familie») ablegen und mit unterlagen_lesen lesen; je Datei eine Zeile mit Art, Seiten, Befunden je Art, Tabellen, Hinweisen, Fehler rot.
// Dazu Kategorie, Hinweis und «bestehende Familie ersetzen». Schritt 2: der KI-Lauf (POST /api/auftraege) mit Fortschritt aus dem Ereignis «auftrag»,
// Dauer und Abbrechen; am Ende der Bericht, die angelegten Familien und der Knopf «Prüfen» (öffnet die Prüfansicht des Entwurfs im Familieneditor).
// Der Dialog schliesst man jederzeit: der Lauf geht weiter, ein erneutes Öffnen zeigt ihn wieder. Keine Fachlogik: alles über Befehle, Abfragen
// und die Endpunkte der Aufträge; die Entscheide (Konflikte, Freigabe) stehen in der Prüfansicht.

import { zustand, alleElemente, abonnieren } from '../zustand.js';
import { abfrage, ApiFehler } from '../api.js';
import { zeigen } from '../oberflaeche/dialoge.js';
import { sende } from '../oberflaeche/befehle.js';
import { knoten } from '../oberflaeche/werkzeug.js';
import { familienListe, familienLaden } from './daten.js';
import { auftragStarten, auftragLesen, auftragAbbrechen, auftraegeListe, beiAuftrag, laufenderAuftrag } from './auftrag.js';
import * as U from './unterlagen-text.js';
import { esc } from './unterlagen-text.js';

/** Datei roh hochladen (POST /api/unterlagen?name=…), fortschritt(geladen, gesamt). Antwort { name, stand, bytes }. */
export function dateiHochladen(datei, fortschritt = null) {
  return new Promise((ok, fehl) => {
    const x = new XMLHttpRequest();
    x.open('POST', '/api/unterlagen?name=' + encodeURIComponent(datei.name));
    x.setRequestHeader('Content-Type', 'application/octet-stream');
    if (x.upload && fortschritt) x.upload.onprogress = (e) => { if (e.lengthComputable) fortschritt(e.loaded, e.total); };
    x.onload = () => {
      let j = null;
      try { j = JSON.parse(x.responseText); } catch { /* Text */ }
      if (x.status >= 200 && x.status < 300 && j) ok(j);
      else fehl(new ApiFehler(j?.fehler ?? (x.responseText || `Hochladen fehlgeschlagen (${x.status}).`), j?.vorschlag ?? null, x.status));
    };
    x.onerror = () => fehl(new ApiFehler('Der Server ist nicht erreichbar.', 'Verbindung prüfen und erneut versuchen.', 0));
    x.send(datei);
  });
}

/**
 * Legt eine hochgeladene Datei als Herstellerunterlage ab und liest sie. Gibt { ablage, kz, eintrag } zurück oder wirft einen Fehler mit Text.
 * Der Bezug der Unterlage zu einer Familie (unterlage_einfuegen mit bezuege.familie) wird NICHT gesetzt: der Kern lehnt ihn ab, weil die Nachführung der
 * Familien (FamilienNachfuehrung.Element) jedes Element mit bezuege.familie für ein platziertes Familienelement hält («UL-1 (Unterlage) kann keine Familie
 * tragen») – Befund am Kern. Die Zuordnung zur Familie ergibt sich aus definition.unterlagen (alle Unterlagen, die eine Quelle nennt).
 */
export async function unterlageAblegenUndLesen(hoch) {
  const p = { datei: hoch.name, zweck: 'familie' };
  const r = await sende('unterlage_einfuegen', p);
  if (!r.angenommen) throw Object.assign(new Error(r.fehler || r.meldung || 'Die Datei wurde nicht angenommen.'), { vorschlag: r.vorschlag });
  const kz = r.aenderungen?.find((a) => a.klasse === 'Unterlage')?.kennzeichen ?? null;
  const l = await abfrage('unterlagen_lesen', { unterlagen: [kz ?? hoch.name] });
  const eintrag = (l.unterlagen ?? [])[0] ?? { fehler: 'Keine Antwort von unterlagen_lesen.' };
  return { ablage: hoch.name, kz: kz ?? eintrag.unterlage ?? null, eintrag };
}

const mb = (b) => (b / 1048576).toFixed(b < 10485760 ? 1 : 0);

/**
 * Öffnet den Dialog. opt: { familie? (Kennzeichen einer Familie, deren Definition ersetzt werden soll: angehakt), vorschlag? (nur vorgewählt, nicht angehakt),
 * dateien? (File[], für Tests),
 * pruefen(kz) (Aufruf bei «Prüfen»; Vorgabe: nichts) }.
 */
export async function familieAusUnterlagenDialog(opt = {}) {
  await familienLaden();
  const kategorien = U.kategorienWahl(zustand.katalog?.klassen);
  let zaehler = 0;
  const dateien = [];
  let auftrag = null;               // Stand des angezeigten Auftrags
  let empfangenUm = 0;              // performance.now() beim letzten Stand
  let ctrlRef = null, uhr = null, abfrageUhr = null, tot = false;

  const w = knoten(`<div class="fu-dialog">
    <div class="fu-s1">
      <div class="fu-drop" tabindex="0" role="button" aria-label="Unterlagen hineinziehen oder wählen">
        <div class="fu-drop-text"><b>Unterlagen hierher ziehen</b> oder <button type="button" class="fe-knopf fu-waehlen">Dateien wählen …</button></div>
        <div class="leise">PDF, DWG/DXF, STEP/IGES, STL/OBJ, IFC, Excel/CSV, Typkatalog (.txt), Bilder (PNG/JPG) – mehrere zugleich</div>
        <input type="file" class="fu-datei" id="fu-datei" multiple accept="${esc(U.DATEI_ACCEPT)}" hidden>
      </div>
      <div class="fu-dateien" role="list"></div>
      <div class="fu-felder">
        <label class="fz"><span>Kategorie</span><select class="fu-kategorie"><option value="">– die KI wählt –</option>${kategorien.map((k) => `<option>${esc(k)}</option>`).join('')}</select></label>
        <label class="fz fu-hinweis-feld"><span>Hinweis an die KI</span><textarea class="fu-hinweis" rows="3" data-kein-enter="1" placeholder="z. B. nur die Typen DN 15–25; Masse in der Tabelle auf Seite 2 gelten"></textarea></label>
        <label class="fz fu-ersetzen-zeile"><span>Bestehende Familie</span><span class="fu-ersetzen-feld"><label class="fe-hk"><input type="checkbox" class="fu-ersetzen"> ersetzen</label>
          <select class="fu-familie" disabled>${familienListe().map((f) => `<option value="${esc(f.kennzeichen)}">${esc(f.kennzeichen)} · ${esc(f.name)}</option>`).join('')}</select></span></label>
      </div>
      <div class="fz fu-internet-zeile"><span>Internet</span><span class="fu-internet-feld"><label class="fe-hk"><input type="checkbox" class="fu-internet" checked> Fehlende Angaben im Internet recherchieren</label>
        <span class="leise fu-internet-text">Werte zählen nur aus heruntergeladenen und abgelegten Dokumenten mit Quelle (Zusammenfassungen von Webseiten sind nie eine Quelle). Adressen im eigenen Netz sind gesperrt. Jeder Abruf steht im Protokoll und im Bericht.</span></span></div>
      <p class="fu-kosten leise">Der Lauf geht über das Claude-Abo von Christian Steiner und dauert einige Minuten (Zeitlimit 20 Minuten). Die KI legt nur einen <b>Entwurf</b> an: Konflikte entscheidet und freigibt ein Mensch in der Prüfansicht.</p>
      <div class="fu-fehler fehlertext" role="alert" hidden></div>
    </div>
    <div class="fu-s2" hidden></div>
    <div class="fu-fuss"></div>
  </div>`);
  const q = (s) => w.querySelector(s);
  const s1 = q('.fu-s1'), s2 = q('.fu-s2'), fuss = q('.fu-fuss'), liste = q('.fu-dateien'), fehlerFeld = q('.fu-fehler');

  // ------------------------------------------------------------------------------------ Schritt 1: Dateien
  const fehlerZeigen = (t) => { fehlerFeld.hidden = !t; fehlerFeld.textContent = t ?? ''; };

  function dateienZeichnen() {
    liste.innerHTML = dateien.length === 0 ? '<p class="leise fu-leer">Noch keine Unterlage.</p>' : dateien.map((d) => {
      const z = d.zeile;
      const zustandText = d.status === 'laedt' ? `<span class="spinner" aria-hidden="true"></span> wird hochgeladen … ${d.prozent ?? 0} %`
        : d.status === 'liest' ? '<span class="spinner" aria-hidden="true"></span> wird gelesen …' : '';
      const fehler = d.fehler || z?.fehler;
      return `<div class="fu-datei-zeile ${esc(d.status)}${fehler ? ' fehler' : ''}" role="listitem" data-id="${d.id}">
        <span class="fu-datei-name" title="${esc(d.name)}">${esc(d.name)}</span>
        <span class="fu-datei-art">${esc(z?.art ?? '')}</span><span class="fu-datei-seiten">${esc(z?.seiten ?? '')}</span>
        <span class="fu-datei-befunde">${zustandText || esc(z?.befunde ?? '')}${z?.tabellen ? ` · ${esc(z.tabellen)}` : ''}</span>
        <button type="button" class="fe-knopf klein fu-weg" data-id="${d.id}" title="Aus der Liste entfernen (die Datei bleibt als Unterlage im Projekt)">✕</button>
        ${fehler ? `<div class="fu-datei-fehler fehlertext">${esc(fehler)}</div>` : ''}
        ${(z?.hinweise ?? []).length ? `<div class="fu-datei-hinweise leise">${z.hinweise.slice(0, 3).map(esc).join(' · ')}${z.hinweise.length > 3 ? ` · … (${z.hinweise.length} Hinweise)` : ''}</div>` : ''}
      </div>`;
    }).join('');
    fussZeichnen();
  }

  async function dateiAufnehmen(file) {
    const ok = U.dateiPruefen(file.name);
    const d = { id: ++zaehler, file, name: file.name, status: 'laedt', prozent: 0, zeile: null, fehler: '' };
    dateien.push(d);
    if (ok !== true) { d.status = 'fehler'; d.fehler = ok; dateienZeichnen(); return d; }
    dateienZeichnen();
    try {
      const hoch = await dateiHochladen(file, (a, b) => { d.prozent = Math.round((a / Math.max(b, 1)) * 100); if (!tot) dateienZeichnen(); });
      d.status = 'liest'; d.prozent = 100; d.ablage = hoch.name; dateienZeichnen();
      const r = await unterlageAblegenUndLesen(hoch);
      d.kz = r.kz; d.eintrag = r.eintrag; d.zeile = U.dateiZeile(r.eintrag);
      d.status = r.eintrag.fehler ? 'fehler' : 'bereit';
    } catch (e) {
      d.status = 'fehler'; d.fehler = e.vorschlag ? `${e.message} – ${e.vorschlag}` : (e.message ?? String(e));
    }
    if (!tot) dateienZeichnen();
    return d;
  }

  const aufnehmen = (files) => { for (const f of files) dateiAufnehmen(f); };
  q('.fu-waehlen').onclick = (e) => { e.stopPropagation(); q('.fu-datei').click(); };
  q('.fu-datei').onchange = (e) => { aufnehmen([...e.target.files]); e.target.value = ''; };
  const drop = q('.fu-drop');
  drop.addEventListener('dragover', (e) => { e.preventDefault(); drop.classList.add('ueber'); });
  drop.addEventListener('dragleave', () => drop.classList.remove('ueber'));
  drop.addEventListener('drop', (e) => { e.preventDefault(); drop.classList.remove('ueber'); aufnehmen([...(e.dataTransfer?.files ?? [])]); });
  drop.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); q('.fu-datei').click(); } });
  liste.addEventListener('click', (e) => {
    const b = e.target.closest('.fu-weg');
    if (!b) return;
    const i = dateien.findIndex((d) => String(d.id) === b.dataset.id);
    if (i >= 0) { dateien.splice(i, 1); dateienZeichnen(); }
  });
  const ersetzenWahl = q('.fu-ersetzen'), familieWahl = q('.fu-familie');
  if (familienListe().length === 0) { ersetzenWahl.disabled = true; ersetzenWahl.title = 'Das Projekt hat noch keine Familie.'; }
  ersetzenWahl.onchange = () => { familieWahl.disabled = !ersetzenWahl.checked; fussZeichnen(); };
  if (opt.vorschlag && familienListe().some((f) => f.kennzeichen === opt.vorschlag)) familieWahl.value = opt.vorschlag;      // vorgewählt, aber nicht angehakt
  if (opt.familie && familienListe().some((f) => f.kennzeichen === opt.familie)) { ersetzenWahl.checked = true; familieWahl.disabled = false; familieWahl.value = opt.familie; }

  // ------------------------------------------------------------------------------------ Schritt 2: Lauf
  function laufZeichnen() {
    const a = auftrag;
    if (!a) return;
    const st = U.STATUS_AUFTRAG[a.status] ?? { text: a.status, klasse: '' };
    const laeuft = U.auftragLaeuft(a);
    const sek = (a.dauerSekunden ?? 0) + (laeuft ? (performance.now() - empfangenUm) / 1000 : 0);
    const schritte = U.schrittZeilen(a, 8);
    const fam = U.ergebnisFamilien(a);
    const erg = a.ergebnis ?? {};
    s2.innerHTML = `<div class="fu-lauf">
      <div class="fu-lauf-kopf"><span class="fu-badge ${st.klasse}">${esc(st.text)}</span><span class="fu-dauer">Dauer ${esc(U.dauerText(sek))}</span>
        <span class="leise">Auftrag ${esc(a.id)} · ${(a.eingabe?.unterlagen ?? []).length} Unterlage(n)${a.eingabe?.kategorie ? ' · ' + esc(a.eingabe.kategorie) : ''}</span></div>
      ${a.eingabe?.internet === undefined ? '' : `<p class="leise fu-lauf-internet">Internet-Recherche: <b>${a.eingabe.internet ? 'ein' : 'aus'}</b></p>`}
      ${laeuft ? '<p class="leise">Der Lauf geht über das Claude-Abo von Christian Steiner und dauert einige Minuten. Das Fenster darf geschlossen werden – der Lauf geht weiter.</p>' : ''}
      <div class="fu-schritte-kopf">Letzte Schritte (${a.schrittAnzahl ?? 0} insgesamt)</div>
      <ol class="fu-schritte">${schritte.length ? schritte.map((s) => `<li><time>${esc(s.zeit)}</time> ${esc(s.text)}</li>`).join('') : '<li class="leise">Noch keine Schritte.</li>'}</ol>
      <div class="fu-inet-feld">${U.internetListeHtml(U.internetUnterlagen(alleElemente()))}</div>
      ${a.fehler ? `<div class="fu-lauf-fehler fehlertext" role="alert">${esc(a.fehler)}</div>` : ''}
      ${!laeuft && erg.bericht ? `<div class="fu-bericht-kopf">Bericht der KI</div><pre class="fu-bericht">${esc(erg.bericht)}</pre>` : ''}
      ${!laeuft && erg.warnung ? `<div class="fu-lauf-warnung">${esc(erg.warnung)}</div>` : ''}
      ${!laeuft ? `<div class="fu-familien-kopf">Angelegte Familien</div>${fam.length ? `<table class="tab fu-familien"><thead><tr><th>Familie</th><th>Kategorie</th><th>Typen</th><th>Konflikte</th><th></th></tr></thead><tbody>${fam.map((f) => `<tr data-kz="${esc(f.kennzeichen)}">
        <td>${esc(f.kennzeichen)} ${esc(f.name ?? '')}${f.aktion === 'geaendert' ? ' <span class="leise">(ersetzt)</span>' : ''}</td><td>${esc(f.kategorie ?? '')}</td>
        <td>${Array.isArray(f.typen) ? f.typen.length : esc(f.typen ?? '')}</td><td>${f.offeneKonflikte ? `<b class="fehlertext">${f.offeneKonflikte} offen</b>` : esc(String(f.konflikte ?? 0))}</td>
        <td><button type="button" class="fe-knopf fu-pruefen-zeile" data-kz="${esc(f.kennzeichen)}">Prüfen</button></td></tr>`).join('')}</tbody></table>` : '<p class="leise">Der Lauf hat keine Familie angelegt oder geändert. Einzelheiten stehen im Bericht und in den Schritten oben (z. B. wenn jemand die Familie gerade bearbeitet, bleibt sie reserviert und die KI darf sie nicht ändern: «Verwalten › meine Elemente freigeben»).</p>'}` : ''}
    </div>`;
    fussZeichnen();
  }

  function zuSchritt2() {
    s1.hidden = true; s2.hidden = false;
    ctrlRef?.fenster.classList.add('fu-lauf-aktiv');
    laufZeichnen();
    clearInterval(uhr); clearInterval(abfrageUhr);
    uhr = setInterval(() => { if (auftrag && U.auftragLaeuft(auftrag)) laufZeichnen(); }, 1000);
    abfrageUhr = setInterval(async () => {   // Sicherheitsnetz, falls ein Ereignis verloren ging
      if (!auftrag || !U.auftragLaeuft(auftrag) || tot) return;
      try { await auftragLesen(auftrag.id); } catch { /* der nächste Versuch */ }
    }, 4000);
  }

  const modellAbo = abonnieren('modell', () => { if (!tot && !s2.hidden && auftrag) laufZeichnen(); });   // neue Unterlagen aus dem Internet erscheinen sofort
  const abmelden = beiAuftrag(async (a) => {
    if (tot || !auftrag || a.id !== auftrag.id) return;
    const warLauf = U.auftragLaeuft(auftrag);
    auftrag = a; empfangenUm = performance.now();
    if (warLauf && U.auftragEnde(a) && !(a.ergebnis && 'bericht' in a.ergebnis)) {
      try { auftrag = await auftragLesen(a.id); empfangenUm = performance.now(); } catch (e) { console.error(e); }
    }
    laufZeichnen();
  });

  async function starten() {
    const gueltig = U.startPruefen({ dateien, ersetzen: ersetzenWahl.checked, familie: familieWahl.value });
    if (gueltig !== true) { fehlerZeigen(gueltig); return; }
    fehlerZeigen('');
    const bereit = dateien.filter((d) => d.status === 'bereit');
    const fam = ersetzenWahl.checked ? familieWahl.value : null;
    ctrlRef?.beschaeftigt(true);
    try {
      const a = await auftragStarten(U.auftragParameter({ unterlagen: bereit.map((d) => d.kz ?? d.ablage), kategorie: q('.fu-kategorie').value, hinweis: q('.fu-hinweis').value, familie: fam, internet: q('.fu-internet').checked }));
      auftrag = a; empfangenUm = performance.now();
      zuSchritt2();
    } catch (e) {
      fehlerZeigen(e.vorschlag ? `${e.message} – ${e.vorschlag}` : (e.message ?? String(e)));
    } finally { ctrlRef?.beschaeftigt(false); }
  }

  async function abbrechen() {
    if (!auftrag) return;
    fuss.querySelector('.fu-abbrechen')?.setAttribute('disabled', '');
    try { auftrag = await auftragAbbrechen(auftrag.id); empfangenUm = performance.now(); } catch (e) { fehlerZeigen(e.message); }
    laufZeichnen();
  }

  // ------------------------------------------------------------------------------------ Fuss
  function fussZeichnen() {
    if (tot) return;
    if (!s2.hidden) {
      const laeuft = U.auftragLaeuft(auftrag);
      const fam = U.ergebnisFamilien(auftrag);
      fuss.innerHTML = `${laeuft ? '<button type="button" class="dlg-knopf gefahr fu-abbrechen">KI-Lauf abbrechen</button>' : '<button type="button" class="dlg-knopf fu-neu">Weitere Unterlagen …</button>'}
        <span class="fu-fuss-abstand"></span><button type="button" class="dlg-knopf fu-schliessen">Schliessen</button>
        ${!laeuft && fam.length ? `<button type="button" class="dlg-knopf primaer fu-pruefen" data-kz="${esc(fam[0].kennzeichen)}">Prüfen</button>` : ''}`;
    } else {
      const g = U.startPruefen({ dateien, ersetzen: ersetzenWahl.checked, familie: familieWahl.value });
      const laufend = laufenderAuftrag();
      fuss.innerHTML = `${laufend ? '<button type="button" class="dlg-knopf fu-laufend">Laufenden Auftrag zeigen</button>' : ''}<span class="fu-fuss-abstand"></span>
        <button type="button" class="dlg-knopf fu-schliessen">Abbrechen</button>
        <button type="button" class="dlg-knopf primaer fu-start" ${g === true ? '' : 'disabled'} title="${esc(g === true ? 'Startet den KI-Lauf' : g)}">KI-Lauf starten</button>`;
    }
  }

  fuss.addEventListener('click', async (e) => {
    const b = e.target.closest('button');
    if (!b || b.disabled) return;
    if (b.classList.contains('fu-start')) starten();
    else if (b.classList.contains('fu-abbrechen')) abbrechen();
    else if (b.classList.contains('fu-schliessen')) ctrlRef?.schliessen(null);
    else if (b.classList.contains('fu-neu')) { auftrag = null; s2.hidden = true; s1.hidden = false; ctrlRef?.fenster.classList.remove('fu-lauf-aktiv'); clearInterval(uhr); clearInterval(abfrageUhr); fussZeichnen(); }
    else if (b.classList.contains('fu-laufend')) { auftrag = laufenderAuftrag(); empfangenUm = performance.now(); zuSchritt2(); }
    else if (b.classList.contains('fu-pruefen')) pruefen(b.dataset.kz);
  });
  s2.addEventListener('click', (e) => { const b = e.target.closest('.fu-pruefen-zeile'); if (b) pruefen(b.dataset.kz); });

  function pruefen(kz) {
    ctrlRef?.schliessen({ pruefen: kz });
  }

  // ------------------------------------------------------------------------------------ Start: läuft schon einer?
  dateienZeichnen();
  (async () => {
    let a = laufenderAuftrag();
    if (!a) { try { const l = await auftraegeListe(); a = l.find(U.auftragLaeuft) ?? null; } catch { /* kein Projekt oder Server */ } }
    if (a && !tot) { try { auftrag = await auftragLesen(a.id); } catch { auftrag = a; } empfangenUm = performance.now(); zuSchritt2(); }
    if (opt.dateien) aufnehmen(opt.dateien);
  })();

  const ergebnis = await zeigen({
    titel: 'Familie aus Unterlagen', inhalt: (ctrl) => { ctrlRef = ctrl; return w; }, knoepfe: [], breite: 820, klasse: 'familie fu-dlg',
  });
  tot = true; clearInterval(uhr); clearInterval(abfrageUhr); abmelden(); modellAbo();
  if (ergebnis?.pruefen && opt.pruefen) await opt.pruefen(ergebnis.pruefen);
  return ergebnis;
}
