// Dialoge der Familienbibliothek (Modul 2, C3): «Familien im Explorer …» (Auswahl je Projekt, familienauswahl_setzen), «Aktualisieren …» (Vorschau aus dem
// Trockenlauf von familie_aktualisieren; bei «parallel geändert» zwei Wege), «Familie laden» (Liste aus familien_bibliothek) und der Hinweis zur Bibliothek
// (Statusleiste). Jeder Dialog zeigt zuerst den Trockenlauf des Kerns; Fehler des Kerns stehen im Dialog (mit Vorschlag). Keine Fachlogik: alles geht über
// die Abfrage familien_bibliothek und die Befehle familie_laden, familie_aktualisieren, familie_in_bibliothek, familienauswahl_setzen. Texte und HTML: bibliothek-text.js.

import { zeigen, meldung } from '../oberflaeche/dialoge.js';
import { sende } from '../oberflaeche/befehle.js';
import { knoten, esc, entprellen } from '../oberflaeche/werkzeug.js';
import { familienListe, familienLaden } from './daten.js';
import { bibliothekAntwort, bibliothekFamilien, bibliothekInfo, bibliothekLaden, bibliothekAktualisieren, bibliothekDetail } from './bibliothek-daten.js';
import * as T from './bibliothek-text.js';

const kernText = (r) => { const a = T.ablehnungText(r); return a.vorschlag ? `${a.fehler} – ${a.vorschlag}` : a.fehler; };

// ================================================================================================ Familien im Explorer …

export async function explorerDialog() {
  await bibliothekLaden();
  const antwort = bibliothekAntwort();
  if (!antwort) { meldung('Zuerst ein Projekt öffnen.', 'warnung'); return undefined; }
  const familien = antwort.familien ?? [];
  const doppel = T.doppelTexte(antwort.doppel);
  const aus = antwort.bibliothek?.aktiv === false ? antwort.bibliothek.gesperrt : '';
  let s = T.auswahlLesen(antwort.auswahl);
  let suche = '';
  let kern = { hinweis: '', fehler: '' };

  const w = knoten(`<div class="bib-dlg bib-explorer">
    <label class="bib-an"><input type="checkbox" class="bib-an-box"> <b>Familien der Bibliothek im Explorer zeigen</b></label>
    <p class="leise bib-erkl">Der Explorer (Projektbrowser, Knoten «Familien») zeigt immer die Familien des Projekts. Mit den Filtern und den Häkchen bestimmst du, welche Familien der
      Bibliothek dazukommen; sie stehen grau mit der Marke «Bibliothek», bis du eine platzierst. Ein einzelnes Häkchen übersteuert die Filter.</p>
    ${aus ? `<p class="bib-kernfehler">${esc(aus)}</p>` : ''}
    <div class="bib-filterbereich"></div>
    <div class="bib-suche"><input type="search" class="bib-suchfeld" placeholder="In der Liste suchen (zeigt nur an, ändert die Auswahl nicht) …" data-kein-enter="1"></div>
    <div class="bib-liste"></div>
    <div class="bib-fuss"></div></div>`);
  const q = (sel) => w.querySelector(sel);

  const fussZeichnen = () => {
    q('.bib-fuss').innerHTML = T.explorerFussHtml({ zahlen: T.explorerZahlen(familien, s), kernHinweis: kern.hinweis, kernFehler: kern.fehler, doppel, an: s.an });
  };
  const zeichnen = () => {
    q('.bib-an-box').checked = s.an;
    q('.bib-filterbereich').innerHTML = T.explorerFilterHtml(familien, s);
    q('.bib-liste').innerHTML = T.explorerListeHtml(T.explorerZeilen(familien, s, suche), s);
    fussZeichnen();
  };
  let nr = 0;
  const trocken = entprellen(async () => {
    const mein = ++nr;
    const r = await sende('familienauswahl_setzen', T.auswahlParameter(s), { trockenlauf: true });
    if (mein !== nr) return;
    kern = r.angenommen ? { hinweis: (r.hinweise ?? [])[0] ?? '', fehler: '' } : { hinweis: '', fehler: kernText(r) };
    fussZeichnen();
  }, 300);
  const geaendert = () => { zeichnen(); trocken(); };

  w.addEventListener('click', (e) => {
    const chip = e.target.closest('.bib-chip');
    if (chip && !chip.disabled) { s = T.filterUmschalten(s, chip.dataset.feld, chip.dataset.wert); geaendert(); }
  });
  w.addEventListener('change', (e) => {
    if (e.target.classList.contains('bib-an-box')) { s = { ...s, an: e.target.checked }; geaendert(); return; }
    if (e.target.matches('.bib-tab input[type=checkbox]')) {
      const f = familien.find((x) => x.id === e.target.dataset.id);
      if (f) { s = T.einzelnSetzen(s, f, e.target.checked); geaendert(); }
    }
  });
  w.addEventListener('input', (e) => {
    if (e.target.classList.contains('bib-suchfeld')) { suche = e.target.value; q('.bib-liste').innerHTML = T.explorerListeHtml(T.explorerZeilen(familien, s, suche), s); }
  });

  zeichnen();
  trocken();
  await zeigen({
    titel: 'Familien im Explorer', inhalt: w, breite: 880, klasse: 'bib',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Speichern', primaer: true, schliesst: false,
        async klick(c) {
          // zuerst der Trockenlauf: der Kern prüft die Auswahl (Gewerke, Kategorien, Status) und nennt die Zahl
          const t = await sende('familienauswahl_setzen', T.auswahlParameter(s), { trockenlauf: true });
          if (!t.angenommen) { c.fehler(kernText(t)); return; }
          const r = await sende('familienauswahl_setzen', T.auswahlParameter(s));
          if (!r.angenommen) { c.fehler(kernText(r)); return; }
          c.schliessen(true);
          await bibliothekAktualisieren();
          meldung((r.hinweise ?? [])[0] ?? 'Familienauswahl gespeichert.', 'ok');
        } }],
  });
  return undefined;
}

// ================================================================================================ Aktualisieren …

export async function aktualisierenDialog({ familie: bezug } = {}) {
  await familienLaden();
  const fam = familienListe().find((f) => f.kennzeichen === bezug || f.name === bezug || f.id === bezug);
  if (!fam) { meldung(`Familie «${bezug}» gibt es nicht.`, 'warnung'); return undefined; }
  const kz = fam.kennzeichen;
  let detail;
  try { detail = (await bibliothekDetail(kz)).familie; } catch (e) { meldung(e.message ?? String(e), 'warnung', { vorschlag: e.vorschlag }); return undefined; }
  const stand = detail?.stand ?? fam.bibliothekStand;
  const wege = T.wegeFuer(stand);
  const optionen = T.versionsOptionen(stand, detail);
  let weg = wege[0] ?? null;
  let version = optionen[0]?.wert ?? '';

  const w = knoten(`<div class="bib-dlg bib-aktualisieren">
    <p class="bib-kopf"><b>${esc(fam.name)}</b> <span class="leise">${esc(kz)} · Projekt: ${fam.version != null ? `Version ${esc(fam.version)}` : ''}</span></p>
    <p class="bib-stand ${esc(stand?.art ?? '')}">${esc(T.standErklaerung(stand))}</p>
    ${wege.length > 1 ? `<fieldset class="bib-wege"><legend>Was soll gelten?</legend>${wege.map((x, i) => `<label class="bib-weg${x.warnung ? ' warnung' : ''}"><input type="radio" name="bib-weg" value="${esc(x.id)}" ${i === 0 ? 'checked' : ''}> ${esc(x.text)}</label>`).join('')}</fieldset>` : ''}
    <label class="fz bib-version" ${optionen.length > 1 ? '' : 'hidden'}><span>Fassung der Bibliothek</span><select class="bib-version-wahl">${optionen.map((o) => `<option value="${esc(o.wert)}">${esc(o.text)}</option>`).join('')}</select></label>
    <div class="bib-vorschau"><p class="leise">Vorschau wird geholt …</p></div></div>`);
  const q = (sel) => w.querySelector(sel);

  let primaer = null;
  let nr = 0;
  async function vorschau() {
    if (!weg) { q('.bib-vorschau').innerHTML = ''; return; }
    const mein = ++nr;
    q('.bib-vorschau').innerHTML = '<p class="leise">Vorschau wird geholt …</p>';
    if (weg.id === 'uebernehmen') {
      // Trockenlauf von familie_aktualisieren (Zusammenfassung, Hinweise, Ablehnung) und die strukturierte Vorschau (betroffene Elemente) der Abfrage
      const [t, d] = await Promise.all([
        sende('familie_aktualisieren', T.aktualisierenParameter(kz, version), { trockenlauf: true }),
        bibliothekDetail(kz, version).catch(() => null),
      ]);
      if (mein !== nr) return;
      const strukt = d?.familie?.vorschau ?? null;
      if (t.angenommen) {
        q('.bib-vorschau').innerHTML = T.vorschauHtml({ zusammenfassung: (t.hinweise ?? [])[0] ?? '', vorschau: strukt, hinweise: t.hinweise ?? [] });
        primaer.disabled = false;
      } else {
        const a = T.ablehnungText(t);
        q('.bib-vorschau').innerHTML = `<div class="bib-abgelehnt"><b>Das lässt sich nicht übernehmen.</b><p>${esc(a.fehler)}</p>${a.vorschlag ? `<p class="bib-vorschlag">Vorschlag: ${esc(a.vorschlag)}</p>` : ''}</div>`
          + (strukt ? `<h4 class="bib-h">Was sich geändert hätte</h4>${T.vorschauHtml({ vorschau: strukt })}` : '');
        primaer.disabled = true;
      }
    } else {
      const t = await sende('familie_in_bibliothek', T.schreibenParameter(kz, weg.alsAktuell), { trockenlauf: true });
      if (mein !== nr) return;
      const warn = weg.alsAktuell
        ? '<div class="bib-warn bib-stark"><b>Deine Fassung ersetzt die aktuelle Version der Bibliothek.</b> Die bisherige Version und alle parallelen Fassungen bleiben in der Bibliothek erhalten, gelten aber nicht mehr als aktuell. '
          + 'Andere Projekte sehen danach «neuere Version». Das entscheidet nur ein Mensch.</div>' : '';
      q('.bib-vorschau').innerHTML = warn + (t.angenommen
        ? `<ul class="bib-zeilen">${(t.hinweise ?? []).map((h) => `<li>${esc(h)}</li>`).join('')}</ul>`
        : `<div class="bib-abgelehnt"><b>Nicht möglich.</b><p>${esc(T.ablehnungText(t).fehler)}</p><p class="bib-vorschlag">${esc(T.ablehnungText(t).vorschlag)}</p></div>`);
      primaer.disabled = !t.angenommen;
    }
  }

  w.addEventListener('change', (e) => {
    if (e.target.name === 'bib-weg') {
      weg = wege.find((x) => x.id === e.target.value) ?? weg;
      q('.bib-version').hidden = optionen.length < 2 || weg.id !== 'uebernehmen';
      if (primaer) primaer.textContent = weg.titel;
      vorschau();
    } else if (e.target.classList.contains('bib-version-wahl')) { version = e.target.value; vorschau(); }
  });

  await zeigen({
    titel: `Aktualisieren – ${fam.name}`, inhalt: w, breite: 760, klasse: 'bib',
    beiOeffnen: (c) => { primaer = c.fuss.querySelector('.dlg-knopf.primaer'); if (!weg) primaer.hidden = true; else primaer.textContent = weg.titel; vorschau(); },
    knoepfe: [{ text: weg ? 'Abbrechen' : 'Schliessen', wert: false },
      { text: 'Aktualisieren', primaer: true, schliesst: false,
        async klick(c) {
          const r = weg.id === 'uebernehmen'
            ? await sende('familie_aktualisieren', T.aktualisierenParameter(kz, version))
            : await sende('familie_in_bibliothek', T.schreibenParameter(kz, weg.alsAktuell));
          if (!r.angenommen) { c.fehler(kernText(r)); return; }
          c.schliessen(true);
          await bibliothekAktualisieren();
          meldung((r.hinweise ?? [])[0] ?? `${kz} aktualisiert.`, 'ok');
        } }],
  });
  return undefined;
}

// ================================================================================================ Familie laden

export async function ladenDialog() {
  await bibliothekLaden();
  const info = bibliothekInfo();
  const familien = bibliothekFamilien();
  let suche = '';
  let gewaehlt = '';
  let version = '';
  let geladen = null;
  const w = knoten(`<div class="bib-dlg bib-laden">
    <p class="leise bib-erkl">Familien der Familienbibliothek (projektübergreifend) und die mitgelieferten. Eine Familie, die schon im Projekt ist, wird dabei auf die gewählte Fassung gebracht (wie «Aktualisieren»).</p>
    ${info?.offline ? `<p class="bib-kernfehler">${esc(info.hinweis ?? 'Die Bibliothek ist nicht erreichbar – gezeigt wird der letzte bekannte Stand; Laden geht erst wieder mit erreichbarer Bibliothek.')}</p>` : ''}
    ${info?.aktiv === false ? `<p class="bib-kernfehler">${esc(info.gesperrt ?? 'Die Familienbibliothek ist gesperrt.')}</p>` : ''}
    <div class="bib-suche"><input type="search" class="bib-suchfeld" placeholder="Suchen …" data-kein-enter="1"></div>
    <div class="bib-liste"></div>
    <label class="fz bib-version" hidden><span>Version</span><select class="bib-version-wahl"></select></label>
    <label class="fz"><span>oder Datei / voller Pfad</span><input type="text" class="fl-datei" data-kein-enter="1" placeholder="Datei aus kataloge/familien oder voller Pfad einer .familie.json"></label>
    <div class="fl-vorschau bib-vorschau"></div></div>`);
  const q = (sel) => w.querySelector(sel);
  const zeilen = () => T.ladenZeilen(familien, suche);
  const zeile = () => T.ladenZeilen(familien, '').find((z) => z.key === gewaehlt) ?? null;
  const listeZeichnen = () => { q('.bib-liste').innerHTML = T.ladenListeHtml(zeilen(), gewaehlt); };

  let nr = 0;
  const vorschau = async () => {
    const mein = ++nr;
    const p = T.ladenParameter({ zeile: zeile(), version, pfad: q('.fl-datei').value });
    const ziel = q('.fl-vorschau');
    if (!p) { ziel.innerHTML = ''; return; }
    const r = await sende('familie_laden', p, { trockenlauf: true });
    if (mein !== nr) return;
    ziel.innerHTML = r.angenommen
      ? `<p class="huelle-ok">${esc((r.hinweise ?? [])[0] ?? 'Die Familie lässt sich laden.')}</p>`
      : `<p class="fehlertext">${esc(r.fehler ?? '')}${r.vorschlag ? `<br><span class="leise">${esc(r.vorschlag)}</span>` : ''}</p>`;
  };
  const vorschauSpaeter = entprellen(vorschau, 300);

  async function versionenZeigen() {
    const z = zeile();
    const feld = q('.bib-version');
    feld.hidden = true;
    version = '';
    if (!z?.id || z.quelle === 'mitgeliefert' || !(z.versionen > 1)) return;
    try {
      const d = (await bibliothekDetail(z.id)).familie;
      const opt = (d.versionen ?? []).map((v) => ({ wert: v.aktuell ? '' : String(v.version), text: `Version ${v.version}${v.aktuell ? ' (aktuell)' : ''}${v.zeit ? ` · ${T.zeitText(v.zeit)}` : ''}${v.projekt ? ` · ${v.projekt}` : ''}` }));
      q('.bib-version-wahl').innerHTML = opt.map((o) => `<option value="${esc(o.wert)}">${esc(o.text)}</option>`).join('');
      feld.hidden = opt.length < 2;
    } catch (e) { console.warn('Versionen', e); }
  }

  w.addEventListener('click', async (e) => {
    const tr = e.target.closest('tr.bib-lade');
    if (!tr) return;
    gewaehlt = tr.dataset.key;
    q('.fl-datei').value = '';
    listeZeichnen();
    await versionenZeigen();
    vorschau();
  });
  w.addEventListener('input', (e) => {
    if (e.target.classList.contains('bib-suchfeld')) { suche = e.target.value; listeZeichnen(); }
    else if (e.target.classList.contains('fl-datei')) { gewaehlt = ''; listeZeichnen(); q('.bib-version').hidden = true; vorschauSpaeter(); }
  });
  w.addEventListener('change', (e) => { if (e.target.classList.contains('bib-version-wahl')) { version = e.target.value; vorschau(); } });

  listeZeichnen();
  await zeigen({
    titel: 'Familie laden', inhalt: w, breite: 820, klasse: 'bib',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Laden', primaer: true, schliesst: false,
        pruefen: () => (T.ladenParameter({ zeile: zeile(), version, pfad: q('.fl-datei').value }) ? true : 'Eine Familie in der Liste wählen oder eine Datei bzw. einen Pfad angeben.'),
        async klick(c) {
          const r = await sende('familie_laden', T.ladenParameter({ zeile: zeile(), version, pfad: q('.fl-datei').value }));
          if (!r.angenommen) { c.fehler(kernText(r)); return; }
          nr++;
          c.schliessen(true);
          await bibliothekAktualisieren();
          const kz = r.aenderungen?.find((a) => a.klasse === 'Familie')?.kennzeichen;
          meldung((r.hinweise ?? [])[0] ?? 'Familie geladen.', 'ok');
          geladen = kz ?? null;
        } }],
  });
  return geladen;   // Kennzeichen der geladenen Familie (oder null)
}

// ================================================================================================ Hinweis zur Bibliothek (Statusleiste)

export async function bibliothekHinweis() {
  await bibliothekLaden();
  const w = knoten('<div class="bib-dlg bib-info-dlg"></div>');
  const zeichnen = () => { w.innerHTML = T.bibliothekHinweisHtml(bibliothekInfo()); };
  zeichnen();
  await zeigen({
    titel: 'Familienbibliothek', inhalt: w, breite: 640, klasse: 'bib',
    knoepfe: [
      { text: 'Jetzt prüfen', schliesst: false, async klick() { await bibliothekAktualisieren(); zeichnen(); } },
      { text: 'Schliessen', wert: true, primaer: true }],
  });
  return undefined;
}
