// Familieneditor (Modul 2, C1): eigene Ansichtsart im Ansichtsbereich (Reiter wie die anderen Ansichten), geöffnet aus dem Projektbrowser.
// Links die Seiten Parameter, Typen, Körper, Anschlüsse, Sinnbild und Prüfen (Tabellen und Formulare wie in Revit), rechts die Familie allein
// in vier Fenstern (vorne, oben, links, 3D) mit Typ und Detailgrad. Alles ändert über den Befehl familie_aendern (ein Paket je Änderung, rückgängig
// mit dem bestehenden Rückgängig); nach jeder Änderung werden Seite und Ansichten neu geholt. Formeln und Zahlen prüft der Kern im Trockenlauf,
// Fehler stehen rot mit seiner Meldung. Der Editor rechnet nichts.
//
// Ansichtsmodul (fenster.js): { erstellen(behaelter, familieElement) → instanz } mit aktualisieren, ansichtGeaendert, auswahlGeaendert, zoomAlles,
// zerstoeren; dazu für Menüband und Tests: istFamilieEditor, kennzeichen, tabWaehlen, aktion, zustand.

import { zustand, element, merken, alleElemente } from '../zustand.js';
import { abfrage } from '../api.js';
import { sende } from '../oberflaeche/befehle.js';
import { entprellen } from '../oberflaeche/werkzeug.js';
import { familieHolen, familienListe, familienLaden, beiFamilien } from './daten.js';
import { esc, kernFehler, pruefZeilen, leitungsArt } from './familien-text.js';
import { ansichtsBereich } from './editor-ansicht.js';
import { parameterTab, typenTab, anschlussTab, pruefenTab, parameterDialog, typNeu, anschlussDialog, parameterLoeschen } from './editor-tabellen.js';
import { koerperTab, sinnbildTab } from './editor-koerper.js';
import { leitungTab } from './editor-leitung.js';
import entwurfTab from './pruefansicht.js';
import befundeTab from './befunde-browser.js';
import { unterlagenKandidaten, zelleMerken } from './unterlagen-text.js';

// Die Reiter «Entwurf prüfen» und «Unterlagen» (Modul 2, C2) sind eigene Seiten (eigen: true): sie verwalten ihr Fenster selbst (Bilder, Eingaben) und werden
// bei Änderungen der Familie nur über aktualisieren() benachrichtigt, nicht neu geschrieben.
// Modul 3 (C3): Leitungsserien (Definition mit Block «leitung») haben die Seite «Leitungsserie» (nurSerie) und weder Körper noch Anschlüsse (nichtSerie).
koerperTab.nichtSerie = true;
anschlussTab.nichtSerie = true;
const TABS = [parameterTab, leitungTab, typenTab, koerperTab, anschlussTab, sinnbildTab, pruefenTab, entwurfTab, befundeTab];
const modul = (id) => TABS.find((t) => t.id === id) ?? TABS[0];
const sichtbareTabs = (serie) => TABS.filter((t) => (serie ? !t.nichtSerie : !t.nurSerie));

export { aktiverEditor, alleEditoren } from './aktiv.js';

function erstellen(behaelter, familieElement) {
  const kz = familieElement.kennzeichen;
  const eigeneId = familieElement.id;
  let fam = null;               // Antwort der Abfrage familie
  let serieDaten = null;        // Antwort der Abfrage leitungsserie (Kontrolltabelle) oder { fehler, vorschlag }; null = Familie ist keine Serie / noch nicht geladen
  let tab = merken.lesen('familieEditorTab', null);   // null: noch nichts gemerkt – dann beginnt eine Leitungsserie auf «Leitungsserie», alles andere auf «Parameter»
  if (!TABS.some((t) => t.id === tab)) tab = null;
  const istSerie = () => !!fam?.definition?.leitung;
  const tabs = () => sichtbareTabs(istSerie());
  const auswahl = { typ: null, koerper: null, sinnbild: null };
  let ladeNr = 0, tot = false, schmutzig = false, veraltet = false;
  const eigenePakete = new Set();
  let eigen = null;           // laufende eigene Seite { id, ctrl } (Modul 2, C2)
  let letzteZelle;            // zuletzt markierte Zelle des Typkatalogs bzw. Parameterzeile (für «als Quelle übernehmen»)
  const eigenAus = () => { try { eigen?.ctrl.zerstoeren?.(); } catch (e) { console.error(e); } eigen = null; };

  behaelter.innerHTML = `<div class="fe-wurzel">
    <div class="fe-links">
      <div class="fe-tabs" role="tablist"></div>
      <div class="fe-meldung" hidden role="alert"></div>
      <div class="fe-seite"></div>
      <div class="fe-fuss"></div>
    </div>
    <div class="fe-griff" title="Ziehen zum Verstellen"></div>
    <div class="fe-rechts"></div>
  </div>`;
  const wurzel = behaelter.querySelector('.fe-wurzel');
  const links = wurzel.querySelector('.fe-links');
  const tabsLeiste = wurzel.querySelector('.fe-tabs');
  const meldungsFeld = wurzel.querySelector('.fe-meldung');
  const seite = wurzel.querySelector('.fe-seite');
  const fuss = wurzel.querySelector('.fe-fuss');
  const rechts = wurzel.querySelector('.fe-rechts');

  const anteil = merken.lesen('familieEditorBreite', 0.56);
  links.style.flexBasis = `${Math.round(Math.min(0.8, Math.max(0.25, anteil)) * 100)}%`;

  const ansicht = ansichtsBereich(rechts, {
    kennzeichen: () => kz,
    def: () => fam?.definition ?? null,
    fam: () => fam,
    auswahlKoerper: () => (tab === 'koerper' ? auswahl.koerper : null),
  });

  // ---------------------------------------------------------------------------------------- Meldungen
  function meldungZeigen(art, text, vorschlag = '', hinweise = []) {
    const teile = [];
    if (text) teile.push(`<div class="fe-meldung-text">${esc(text)}${vorschlag ? ` <span class="fe-vorschlag">Vorschlag: ${esc(vorschlag)}</span>` : ''}</div>`);
    for (const h of hinweise) teile.push(`<div class="fe-meldung-hinweis">${esc(h)}</div>`);
    meldungsFeld.hidden = teile.length === 0;
    meldungsFeld.className = `fe-meldung ${art}`;
    meldungsFeld.innerHTML = teile.join('');
  }
  const meldungWeg = () => { meldungsFeld.hidden = true; meldungsFeld.innerHTML = ''; };
  const typHinweise = (r) => (r.hinweise ?? []).filter((h) => /^Typ «/.test(h));
  function fehlerKern(r) { const k = kernFehler(r); meldungZeigen('fehler', k.text, k.vorschlag); }

  // ---------------------------------------------------------------------------------------- Kontext für die Seiten
  const ctx = {
    kz,
    get fam() { return fam; },
    ansicht,
    elemente: () => alleElemente(),
    kandidaten: () => unterlagenKandidaten(fam?.definition, fam?.kennzeichen ?? kz, alleElemente()),
    zelle: () => letzteZelle,
    tabWaehlen: (id) => tabWaehlen(id),
    get def() { return fam?.definition ?? {}; },
    istSerie,
    familien: () => familienListe(),
    klassen: () => zustand.katalog?.klassen ?? [],
    serie: () => serieDaten,
    auswahl,
    medien: () => zustand.katalog?.medien ?? [],
    fehler: (t) => meldungZeigen('fehler', t),
    fehlerKern,
    neuZeichnen: () => zeichnen(),
    neuLaden: (explizit) => neuLaden(explizit),
    springen: (ziel) => springen(ziel),
    pruefen: (ops) => sende('familie_aendern', { familie: kz, ops }, { trockenlauf: true }),
    async senden(ops, opt = {}) {
      const r = await sende('familie_aendern', { familie: kz, ops });
      if (r.paketId) eigenePakete.add(r.paketId);
      if (!r.angenommen) { if (!opt.still) fehlerKern(r); return r; }
      const warn = typHinweise(r);
      if (warn.length) meldungZeigen('hinweis', '', '', warn); else meldungWeg();
      await neuLaden(true);
      return r;
    },
  };

  // ---------------------------------------------------------------------------------------- Zeichnen
  /** Kontrolltabelle: Abfrage leitungsserie (nur bei Leitungsserien). Ist die Definition nicht gültig, antwortet der Kern mit einem Fehler – er steht dann in der Tabelle. */
  async function serieLaden(f) {
    if (!f?.definition?.leitung) return null;
    try { return await abfrage('leitungsserie', { familie: kz }); } catch (e) { return { fehler: e.message, vorschlag: e.vorschlag ?? '' }; }
  }
  const fokusKey = () => { const a = document.activeElement; return a && seite.contains(a) ? a.dataset?.fk ?? null : null; };
  function fokusHerstellen(key) {
    if (!key) return;
    const el = [...seite.querySelectorAll('[data-fk]')].find((e) => e.dataset.fk === key);
    if (!el) return;
    try { el.focus({ preventScroll: true }); if (el.select && el.type === 'text') el.select(); } catch { /* egal */ }
  }

  function tabsZeichnen() {
    const z = fam ? pruefZeilen(fam).length : 0;
    tabsLeiste.innerHTML = tabs().map((t) => `<button type="button" role="tab" class="fe-tab-knopf${t.id === tab ? ' aktiv' : ''}${t.id === 'pruefen' && z ? ' mit-fehlern' : ''}" data-tab="${t.id}">${esc(t.titel)}${t.id === 'pruefen' && z ? ` (${z})` : ''}</button>`).join('');
  }

  function fussZeichnen() {
    if (!fam) { fuss.textContent = ''; return; }
    const def = fam.definition ?? {};
    fuss.innerHTML = `<span class="${fam.status === 'freigegeben' ? 'fe-status frei' : 'fe-status entwurf'}">${fam.status === 'freigegeben' ? 'freigegeben' : 'Entwurf'}</span>`
      + `<span>${esc(fam.kennzeichen)} · ${esc(fam.kategorie)} · Version ${esc(fam.version)}</span>`
      + (istSerie()
        ? `<span class="leise">${(def.parameter ?? []).length} Parameter · ${(fam.typen ?? []).length} Grössen (Typen)</span>`
        : `<span class="leise">${(def.parameter ?? []).length} Parameter · ${(fam.typen ?? []).length} Typen · ${(def.koerper ?? []).length} Körper · ${(def.anschluesse ?? []).length} Anschlüsse</span>`)
      + ((fam.verwendungen ?? []).length ? `<span class="leise">${fam.verwendungen.length} platziert</span>` : '');
  }

  function zeichnen() {
    if (tot) return;
    if (!fam) { seite.innerHTML = '<p class="fe-leer leise">Familie wird geladen …</p>'; return; }
    // Nichts gemerkt oder eine Seite, die es für diese Familie nicht gibt (zuletzt gemerkte Seite einer anderen Familie): bei Leitungsserien «Leitungsserie», sonst «Parameter»
    if (!tabs().some((t) => t.id === tab)) tab = istSerie() ? 'leitung' : 'parameter';
    wurzel.classList.toggle('fe-serie', istSerie());
    const m = modul(tab);
    if (m.eigen) {
      // eigene Seite: einmal anlegen, danach nur benachrichtigen (kein Neuschreiben: Bilder, Eingaben und Fokus bleiben)
      if (!eigen || eigen.id !== tab) {
        eigenAus();
        seite.innerHTML = '';
        const h = document.createElement('div');
        h.className = 'fu-host';
        seite.appendChild(h);
        eigen = { id: tab, ctrl: m.erstellen(h, ctx) };
      } else eigen.ctrl.aktualisieren?.();
      schmutzig = false; veraltet = false;
      tabsZeichnen(); fussZeichnen();
      wurzel.dataset.tab = tab;
      return;
    }
    eigenAus();
    const key = fokusKey();
    // Ein entferntes Feld mit geänderter Eingabe löst im Browser «change» aus: Eingabe vorher zurücksetzen (Escape, Neuzeichnen nach Fehlern)
    const a = document.activeElement;
    if (a && seite.contains(a) && a.matches('input[type=text]')) a.value = a.defaultValue;
    const y = seite.scrollTop;
    seite.innerHTML = modul(tab).zeichne(ctx);
    seite.scrollTop = y;
    fokusHerstellen(key);
    schmutzig = false; veraltet = false;
    tabsZeichnen();
    fussZeichnen();
    wurzel.dataset.tab = tab;
  }

  async function neuLaden(explizit = false) {
    if (tot) return;
    const mein = ++ladeNr;
    const f = await familieHolen(kz);
    if (mein !== ladeNr || tot) return;
    if (!f) { meldungZeigen('fehler', `Familie ${kz} konnte nicht gelesen werden.`); return; }
    fam = f;
    if (f.definition?.leitung) await familienLaden();   // die Auswahl der Formteil-Familien braucht die Liste des Projekts (sonst stünde «nicht im Projekt» da)
    serieDaten = await serieLaden(f);
    if (mein !== ladeNr || tot) return;
    if (schmutzig && !explizit && seite.contains(document.activeElement)) veraltet = true; else zeichnen();
    tabsZeichnen(); fussZeichnen();
    if (!istSerie()) ansicht.laden();   // eine Leitungsserie hat keine Körper: die Ansichten rechts entfallen
  }
  const planen = entprellen(() => neuLaden(false), 120);

  function tabWaehlen(id) {
    if (!tabs().some((t) => t.id === id)) return;
    tab = id;
    merken.schreiben('familieEditorTab', id);
    zeichnen();
    ansicht.hervorheben();
  }

  // ---------------------------------------------------------------------------------------- Bedienung
  function markieren(el, art, text, vorschlag) {
    el?.classList.toggle('fehlerhaft', art === 'fehler');
    if (el) el.title = art === 'fehler' ? `${text}${vorschlag ? ' – ' + vorschlag : ''}` : el.title;
    if (art === 'fehler') meldungZeigen('fehler', text, vorschlag);
  }

  async function pruefLive(el) {
    const nr = (el._pruefNr = (el._pruefNr ?? 0) + 1);
    let res;
    try { res = await modul(tab).aenderung(el, ctx); } catch (e) { console.error(e); return; }
    if (!res) return;
    if (nr !== el._pruefNr) return;
    if (res.fehler) { markieren(el, 'fehler', res.fehler); return; }
    if (!res.ops) return;
    const r = await ctx.pruefen(res.ops);
    if (nr !== el._pruefNr || tot) return;
    if (r.angenommen) {
      el.classList.remove('fehlerhaft');
      const warn = typHinweise(r);
      if (warn.length) meldungZeigen('hinweis', '', '', warn); else meldungWeg();
    } else { const k = kernFehler(r); markieren(el, 'fehler', k.text, k.vorschlag); }
  }
  const pruefLivePlan = entprellen((el) => pruefLive(el), 320);

  seite.addEventListener('input', (e) => {
    if (modul(tab).eigen) return;
    const el = e.target.closest('input[type=text]');
    if (!el) return;
    schmutzig = true;
    if (el.hasAttribute('data-live') || el.closest('.fe-feld')) { el._pruefNr = (el._pruefNr ?? 0) + 1; pruefLivePlan(el); }
  });

  seite.addEventListener('change', async (e) => {
    if (modul(tab).eigen) return;
    const el = e.target.closest('input, select');
    if (!el || !(el.hasAttribute('data-feld') || el.closest('.fe-feld'))) return;
    el._pruefNr = (el._pruefNr ?? 0) + 1;
    let res;
    try { res = await modul(tab).aenderung(el, ctx); } catch (err) { console.error(err); meldungZeigen('fehler', String(err.message ?? err)); return; }
    schmutzig = false;
    if (!res) { if (veraltet) neuLaden(); return; }
    if (res.fehler) { markieren(el, 'fehler', res.fehler); return; }
    if (res.eigen) { await res.eigen(); return; }
    const r = await ctx.senden(res.ops, { still: true });
    if (!r.angenommen) { const k = kernFehler(r); markieren(el, 'fehler', k.text, k.vorschlag); }
    else {
      // Ändert sich Körper oder Anschluss, sieht man es sofort; Auswahl des Körpers bleibt
      el.classList.remove('fehlerhaft');
    }
  });

  seite.addEventListener('click', async (e) => {
    if (modul(tab).eigen) return;
    const k = e.target.closest('[data-aktion]');
    if (k && seite.contains(k) && !k.disabled) {
      let res;
      try { res = await modul(tab).klick?.(k, ctx); } catch (err) { console.error(err); meldungZeigen('fehler', String(err.message ?? err)); return; }
      if (!res) return;
      if (res.fehler) { meldungZeigen('fehler', res.fehler); return; }
      if (res.eigen) { await res.eigen(); return; }
      if (res.ops) await ctx.senden(res.ops);
      return;
    }
    // Zeile wählen (Körperliste, Typenliste, Sinnbildliste)
    const tr = e.target.closest('tr[data-zeile]');
    if (tr && !e.target.closest('input, select, button, textarea')) {
      const m = modul(tab);
      if (tab === 'typen') { auswahl.typ = tr.dataset.zeile; seite.querySelectorAll('tr[data-zeile]').forEach((r) => r.classList.toggle('gewaehlt', r === tr)); return; }
      if (m.zeileGewaehlt?.(tr, ctx)) { zeichnen(); ansicht.hervorheben(); }
    }
  });

  // Auch ein Klick in ein Feld einer Zeile wählt sie (Typenliste): nur die Markierung
  seite.addEventListener('focusin', (e) => {
    const tr = e.target.closest?.('tr[data-zeile]');
    const zelle = zelleMerken(tab, tr?.dataset.zeile, e.target.dataset?.p);
    if (zelle) letzteZelle = zelle;
    if (tab === 'typen' && tr && auswahl.typ !== tr.dataset.zeile) { auswahl.typ = tr.dataset.zeile; seite.querySelectorAll('tr[data-zeile]').forEach((r) => r.classList.toggle('gewaehlt', r === tr)); }
  });

  seite.addEventListener('keydown', (e) => {
    if (modul(tab).eigen) return;
    if (e.key === 'Escape' && e.target.matches('input, select')) { e.stopPropagation(); schmutzig = false; meldungWeg(); zeichnen(); }
    else if (e.key === 'Enter' && e.target.matches('input[type=text]')) e.target.blur();
  });

  seite.addEventListener('focusout', () => {
    if (modul(tab).eigen) return; setTimeout(() => { if (veraltet && !seite.contains(document.activeElement)) neuLaden(); }, 0); });

  tabsLeiste.addEventListener('click', (e) => { const b = e.target.closest('[data-tab]'); if (b) tabWaehlen(b.dataset.tab); });

  // Teiler zwischen Seite und Ansichten
  const griff = wurzel.querySelector('.fe-griff');
  griff.addEventListener('pointerdown', (e) => {
    griff.setPointerCapture(e.pointerId);
    const r = wurzel.getBoundingClientRect();
    const bewegen = (ev) => {
      const a = Math.min(0.8, Math.max(0.25, (ev.clientX - r.left) / r.width));
      links.style.flexBasis = `${Math.round(a * 100)}%`;
      merken.schreiben('familieEditorBreite', a);
      window.dispatchEvent(new Event('resize'));
    };
    const ende = () => { griff.removeEventListener('pointermove', bewegen); griff.removeEventListener('pointerup', ende); };
    griff.addEventListener('pointermove', bewegen); griff.addEventListener('pointerup', ende);
  });

  // ---------------------------------------------------------------------------------------- Springen (Prüfliste)
  async function springen(ziel) {
    if (!ziel?.tab) return;
    if (ziel.tab === 'koerper') auswahl.koerper = ziel.name;
    if (ziel.tab === 'typen') auswahl.typ = ziel.name;
    if (ziel.tab === 'sinnbild') auswahl.sinnbild = Number(ziel.name) - 1;
    tabWaehlen(ziel.tab);
    const q = (s) => { try { return seite.querySelector(s); } catch { return null; } };
    const zeile = (n) => [...seite.querySelectorAll('tr[data-zeile]')].find((r) => r.dataset.zeile === n);
    let el = null;
    const feld = ziel.feld ?? '';
    if (ziel.tab === 'parameter') el = zeile(ziel.name)?.querySelector(`[data-feld="${feld || 'name'}"]`) ?? zeile(ziel.name)?.querySelector('[data-feld="eingabe"]');
    else if (ziel.tab === 'typen') el = [...(zeile(ziel.name)?.querySelectorAll('[data-p]') ?? [])].find((c) => c.dataset.p === feld) ?? zeile(ziel.name)?.querySelector('[data-feld="typname"]');
    else if (ziel.tab === 'anschluesse') el = zeile(ziel.name)?.querySelector(`[data-feld="${feld}"], [data-feld^="${feld}."]`) ?? zeile(ziel.name)?.querySelector('[data-feld="name"]');
    else {
      const basis = feld.replace(/\[.*$/, '');
      // Leitungsserie: «formteile.bogen» trifft das Feld «formteile.bogen.familie» (Präfix)
      el = q(`.fe-feld[data-pfad="${basis}"] input, .fe-feld[data-pfad="${basis}"] select`) ?? q(`.fe-feld[data-pfad^="${basis}"] input, .fe-feld[data-pfad^="${basis}"] select`)
        ?? q('.fe-detail input, .fe-leitung-formular input, .fe-leitung-formular select');
    }
    if (el) { el.focus(); el.scrollIntoView({ block: 'center' }); el.classList.add('springziel'); setTimeout(() => el.classList.remove('springziel'), 1800); }
  }

  // ---------------------------------------------------------------------------------------- Aktionen aus dem Menüband
  const instanz = {
    istFamilieEditor: true,
    get kennzeichen() { return kz; },
    get fam() { return fam; },
    get tab() { return tab; },
    auswahl,
    ansichtszustand: ansicht.zustand,
    tabWaehlen,
    ansichtEinstellen: (o) => ansicht.einstellen(o),
    neuLaden,
    /** Knöpfe des Registers «Familie»: Name der Aktion → Wirkung. */
    async aktion(name, param = {}) {
      if (istSerie() && ['koerper-neu', 'koerper-loeschen', 'anschluss-neu'].includes(name)) {
        tabWaehlen('leitung');
        meldungZeigen('hinweis', 'Eine Leitungsserie hat weder Körper noch Anschlüsse – die Geometrie ergibt sich aus dem Verlauf der Leitung (Seite «Leitungsserie»).');
        return undefined;
      }
      switch (name) {
        case 'parameter-neu': tabWaehlen('parameter'); return parameterDialog(ctx);
        case 'parameter-loeschen': {
          tabWaehlen('parameter');
          const n = param.name ?? seite.querySelector('tr[data-zeile]:focus-within')?.dataset.zeile;
          if (!n) { meldungZeigen('hinweis', 'Zuerst in die Zeile des Parameters klicken, der gelöscht werden soll (oder ✕ in der Zeile).'); return undefined; }
          return parameterLoeschen(ctx, n);
        }
        case 'typ-neu': tabWaehlen('typen'); return typNeu(ctx, false);
        case 'typ-kopieren': tabWaehlen('typen'); return typNeu(ctx, true, auswahl.typ);
        case 'typ-loeschen': { tabWaehlen('typen'); const k = seite.querySelector('[data-aktion="typ-loeschen-gewaehlt"]'); return k ? k.click() : undefined; }
        case 'koerper-neu': {
          tabWaehlen('koerper');
          const k = [...seite.querySelectorAll('[data-aktion="koerper-neu"]')].find((x) => x.dataset.art === param.art);
          return k?.click();
        }
        case 'koerper-loeschen': tabWaehlen('koerper'); return seite.querySelector('[data-aktion="koerper-loeschen"]')?.click();
        case 'anschluss-neu': tabWaehlen('anschluesse'); return anschlussDialog(ctx);
        case 'sinnbild': tabWaehlen('sinnbild'); return undefined;
        case 'pruefen': tabWaehlen('pruefen'); return neuLaden(true);
        case 'entwurf-pruefen': tabWaehlen('entwurf'); return undefined;
        case 'unterlagen': tabWaehlen('befunde'); return undefined;
        default: return undefined;
      }
    },
    // Vertrag fenster.js
    aktualisieren(ereignis) {
      if (tot) return;
      if (ereignis?.paket && eigenePakete.has(ereignis.paket)) return;   // eigene Änderung: Seite wurde schon neu geholt
      if (!ereignis || ereignis.alles || (ereignis.ids ?? []).includes(eigeneId)) planen();
      // Leitungsserie: die Kontrolltabelle hängt an den Formteil-Familien des Projekts (anderes Familienelement geändert → neu lesen)
      else if (istSerie() && (ereignis.ids ?? []).some((id) => element(id)?.klasse === 'Familie')) planen();
    },
    ansichtGeaendert() { /* Änderungen der Familie kommen über aktualisieren */ },
    auswahlGeaendert() { /* die Auswahl im Modell betrifft den Editor nicht */ },
    zoomAlles() { ansicht.zoomAlles(); },
    /** Die laufende eigene Seite (Prüfansicht, Befund-Browser) – für Tests. */
    get eigeneSeite() { return eigen?.ctrl ?? null; },
    zerstoeren() { tot = true; ladeNr++; eigenAus(); abmelden(); ansicht.zerstoeren(); behaelter.replaceChildren(); },
  };

  // Die Liste der Familien des Projekts ändert sich (Formteil-Familie geladen): Auswahl und Kontrolltabelle der Leitungsserie neu holen
  const abmelden = beiFamilien(() => { if (istSerie() && !tot) planen(); });

  zeichnen();
  neuLaden(true);
  return instanz;
}

export default { erstellen };
