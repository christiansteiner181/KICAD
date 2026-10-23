// Dialog «XR-Labor» (Modul XR, Quest App P6; Register Ansicht › Fenster): die Werkbank für das Interface-Labor der Quest App. Sechs Bereiche in Reitern:
// Stellschrauben live (Regler je Eintrag des Katalogs, sofort in der Brille), Profile (Satz speichern, laden, für die App festlegen), Varianten (A/B), Prüfaufgaben (starten, Status),
// Auswertung der Messprotokolle (je Aufgabe, Variante und Fassung der App) und Fragebogen (Komfort; SUS und NASA-TLX, sobald ihr Wortlaut mit Quelle da ist).
// Keine Logik hier: alles läuft über /api/xr/… (XrEndpunkte.cs, XrLaborEndpunkte.cs); Katalog, Prüfung und Bewertung liegen im Server, die Hilfen der Anzeige in xr-labor-text.js.
// Namen aus dem Netz (Brillen, Profile, Texte der Kataloge) gehen nur über esc() ins HTML. Der Dialog passt sich dem Fenster an und scrollt.

import { holen, senden } from '../api.js';
import { zeigen, bestaetigen, meldung } from './dialoge.js';
import { knoten } from './werkzeug.js';
import { benutzerLaden, benutzerSetzen, benutzerAlle, benutzerWert } from './benutzer.js';
import * as T from './xr-labor-text.js';

const esc = T.esc;

/** Nur ein Dialog zugleich. */
let offenerDialog = null;

/** Öffnet den Dialog (ein zweiter Aufruf gibt den offenen zurück). Gibt zurück, wenn er geschlossen wird. */
export function xrLaborDialog() {
  if (offenerDialog) return offenerDialog;
  const z = {
    katalog: null,          // GET /api/xr/labor/katalog
    katalogFehler: null,    // { text, vorschlag }
    teilnehmer: null,       // GET /api/xr/teilnehmer
    live: {},               // Stellschrauben, die KICAD der Brille gesetzt hat (Schlüssel → Wert)
    auswertung: null,       // GET /api/xr/labor/auswertung
    fragebogen: null,       // GET /api/xr/labor/fragebogen
    bereich: 'stellschrauben',
    gestartet: {},          // Aufgabe → { laeufeVorher }
    laeuft: false,
    appVonHand: false,      // der Mensch hat die Fassung der App im Fragebogen selbst eingetragen
  };
  let offen = true;
  let timer = null;
  let auswertungTimer = null;
  const sende = new Map();  // Schlüssel → { timer, wert, ausstehend }: höchstens ein Senden je SENDE_VERZOEGERUNG_MS und Regler
  const zeilen = new Map(); // Schlüssel → { el, s } der Stellschrauben

  const w = knoten(`<div class="xl">
    <div class="xl-kopf"><span class="xl-lampe" aria-hidden="true"></span><span class="xl-brillen"></span></div>
    <div class="xl-meldung" role="status" aria-live="polite" hidden></div>
    <div class="xl-fehler" role="alert" hidden></div>
    <div class="xl-reiter" role="tablist" aria-label="Bereiche des XR-Labors">${T.BEREICHE.map((b) => `<button type="button" role="tab" class="xl-reiter-knopf" data-bereich="${esc(b.id)}" aria-selected="false">${esc(b.titel)}</button>`).join('')}</div>
    <section class="xl-bereich" data-bereich="stellschrauben" role="tabpanel" aria-label="Stellschrauben" hidden>
      <div class="leise xl-klein">Jede Änderung geht sofort an die Brille. «Vorgabe» setzt die Stellschraube zurück. Mit «folgt P5» gekennzeichnete Werte versteht die App erst ab P5; KICAD sendet sie schon.</div>
      <div class="xl-liste"></div>
    </section>
    <section class="xl-bereich" data-bereich="profile" role="tabpanel" aria-label="Profile" hidden>
      <div class="leise xl-klein">Ein Profil ist der aktuelle Satz aller Stellschrauben und Varianten unter einem Namen (gespeichert je Benutzer). «Für die App festlegen» heisst: die App holt dieses Profil beim Verbinden mit dem Katalog (Reihenfolge: Vorgabe › Profil › live gesetzte Werte).</div>
      <div class="xl-zeile-eingabe"><label class="qd-etikett" for="xl-profilname">Name</label>
        <input id="xl-profilname" type="text" class="xl-profilname" maxlength="40" spellcheck="false" autocomplete="off" data-kein-enter="1" placeholder="z. B. Sitzung sitzend" aria-label="Name des Profils">
        <button type="button" class="dlg-knopf primaer xl-profil-speichern" title="Speichert den aktuellen Satz aller Stellschrauben und Varianten unter diesem Namen">Aktuellen Satz speichern</button></div>
      <div class="xl-profile" aria-label="Gespeicherte Profile"></div>
    </section>
    <section class="xl-bereich" data-bereich="varianten" role="tabpanel" aria-label="Varianten" hidden>
      <div class="leise xl-klein">Je Bedienung eine Wahl (A/B). KICAD sendet sie als Stellschraube <code>labor.variante.&lt;name&gt;</code> und sendet sie bei jeder neuen Verbindung wieder; die App trägt sie in jedes Messprotokoll ein.</div>
      <div class="xl-varianten"></div>
    </section>
    <section class="xl-bereich" data-bereich="aufgaben" role="tabpanel" aria-label="Prüfaufgaben" hidden>
      <div class="leise xl-klein">«Aufgabe starten» schickt die Aufgabe an die verbundene Brille (Stellschraube <code>labor.aufgabe</code>). Die App misst Zeit, Fehlgriffe und Abbrüche und meldet das Ergebnis; der Status unten kommt aus den Messprotokollen.</div>
      <div class="xl-aufgaben-kopf"><button type="button" class="dlg-knopf xl-aufgabe-beenden" title="Beendet die laufende Aufgabe in der Brille">Aufgabe beenden</button></div>
      <div class="xl-aufgaben"></div>
    </section>
    <section class="xl-bereich" data-bereich="auswertung" role="tabpanel" aria-label="Auswertung" hidden>
      <div class="xl-aufgaben-kopf"><button type="button" class="dlg-knopf xl-auswertung-neu" title="Liest die Messprotokolle und Fragebogen neu">Neu lesen</button><span class="leise xl-klein xl-auswertung-stand"></span></div>
      <div class="xl-auswertung"></div>
    </section>
    <section class="xl-bereich" data-bereich="fragebogen" role="tabpanel" aria-label="Fragebogen" hidden>
      <div class="xl-fragebogen"></div>
    </section>
  </div>`);
  const q = (s) => w.querySelector(s);
  const qa = (s) => [...w.querySelectorAll(s)];

  // ------------------------------------------------------------------------------------------ Meldungen

  let meldungTimer = null;
  function meldungZeigen(art, text, vorschlag = null) {
    const m = q('.xl-meldung');
    m.hidden = !text;
    m.className = `xl-meldung ${art === 'fehler' ? 'fehler' : art === 'warnung' ? 'warnung' : ''}`.trim();
    m.innerHTML = text ? `<b>${esc(text)}</b>${vorschlag ? `<div class="qd-vorschlag">Vorschlag: ${esc(vorschlag)}</div>` : ''}` : '';
    clearTimeout(meldungTimer);
    if (text && art === 'ok') meldungTimer = setTimeout(() => { if (offen) m.hidden = true; }, 8000);
  }
  const fehlerMeldung = (err) => meldungZeigen('fehler', err?.message || String(err), err?.vorschlag || null);

  // ------------------------------------------------------------------------------------------ Kopf: verbundene Brillen

  const brillen = () => T.brillenInfo(z.teilnehmer);

  function zeichneKopf() {
    const b = brillen();
    q('.xl-lampe').dataset.stufe = b.stufe;
    q('.xl-brillen').textContent = b.text;
  }

  // ------------------------------------------------------------------------------------------ Stellschrauben

  function steuerung(s, wert) {
    const id = `xl-s-${s.schluessel.replace(/[^a-z0-9]/g, '-')}`;
    if (s.art === 'ja_nein') return `<label class="xl-schalter"><input type="checkbox" class="xl-ja" id="${id}" ${wert === true ? 'checked' : ''} aria-label="${esc(s.titel)}"> <span class="xl-ja-text"></span></label>`;
    if (s.art === 'auswahl') {
      return `<select class="xl-auswahl" id="${id}" aria-label="${esc(s.titel)}">${(s.auswahl ?? []).map((a) => `<option value="${esc(a.wert)}"${a.wert === wert ? ' selected' : ''}>${esc(a.titel)}</option>`).join('')}</select>`;
    }
    return `<input type="range" class="xl-regler" id="${id}" min="${s.min}" max="${s.max}" step="${s.schritt}" value="${wert}" aria-label="${esc(s.titel)}">`
      + `<input type="number" class="xl-zahl" min="${s.min}" max="${s.max}" step="${s.schritt}" value="${T.zahlText(s, wert)}" aria-label="${esc(s.titel)} (Zahl)" data-kein-enter="1">`
      + `<span class="xl-einheit">${esc(s.einheit === '1:n' ? '1:n' : s.einheit)}</span>`;
  }

  function baueStellschrauben() {
    zeilen.clear();
    const liste = q('.xl-liste');
    liste.innerHTML = '';
    for (const g of T.gruppen(z.katalog)) {
      const sec = knoten(`<section class="xl-gruppe" data-gruppe="${esc(g.id)}"><h4 class="xl-gruppe-titel">${esc(g.titel)}</h4></section>`);
      for (const s of g.eintraege) {
        const wert = T.aktuellerWert(s, z.live);
        const el = knoten(`<div class="xl-zeile" data-schluessel="${esc(s.schluessel)}" data-status="${esc(s.status)}">
          <div class="xl-zeile-kopf"><span class="xl-titel">${esc(s.titel)}</span>${T.folgtP5(s) ? `<span class="xl-marke" title="${esc(T.FOLGT_P5_HINWEIS)}">folgt P5</span>` : ''}
            <code class="xl-schluessel leise">${esc(s.schluessel)}</code></div>
          <div class="xl-beschreibung leise">${esc(s.beschreibung)}</div>
          <div class="xl-steuerung">${steuerung(s, wert)}<button type="button" class="dlg-knopf xl-vorgabe" title="Setzt die Stellschraube auf die Vorgabe zurück">Vorgabe</button></div>
          <div class="xl-wert leise"></div>
          <div class="xl-herkunft leise">Herkunft: ${esc(s.herkunft)}</div>
        </div>`);
        sec.append(el);
        zeilen.set(s.schluessel, { el, s });
      }
      liste.append(sec);
    }
    zeichneWerte(true);
  }

  /** Setzt die Anzeige jeder Zeile auf den aktuellen Wert (Regler nur, wenn der Mensch sie nicht gerade bedient). */
  function zeichneWerte(erzwingen = false) {
    for (const { el, s } of zeilen.values()) {
      const wert = T.aktuellerWert(s, z.live);
      const bedient = !erzwingen && el.contains(document.activeElement) && document.activeElement !== el.querySelector('.xl-vorgabe');
      if (!bedient) {
        if (s.art === 'ja_nein') el.querySelector('.xl-ja').checked = wert === true;
        else if (s.art === 'auswahl') el.querySelector('.xl-auswahl').value = String(wert);
        else {
          el.querySelector('.xl-regler').value = String(wert);
          el.querySelector('.xl-zahl').value = T.zahlText(s, wert);
        }
      }
      if (s.art === 'ja_nein') el.querySelector('.xl-ja-text').textContent = wert === true ? 'ein' : 'aus';
      const gesetzt = T.istGesetzt(s, z.live);
      el.dataset.gesetzt = gesetzt ? '1' : '0';
      el.querySelector('.xl-wert').textContent = `${gesetzt ? 'Gesetzt' : 'Nicht gesetzt (Vorgabe)'}: ${T.wertText(s, wert)} · Vorgabe: ${T.wertText(s, s.vorgabe)}`;
      el.querySelector('.xl-vorgabe').disabled = !gesetzt && T.istVorgabe(s, wert);
    }
  }

  async function sendeSchraube(schluessel, wert) {
    try { await senden('/api/xr/stellschraube', { schluessel, wert }); }
    catch (err) { fehlerMeldung(err); }
  }

  /** Sendet höchstens alle SENDE_VERZOEGERUNG_MS je Schlüssel; der letzte Wert kommt immer an. */
  function planeSenden(schluessel, wert) {
    let st = sende.get(schluessel);
    if (!st) { st = { timer: null, wert: undefined, ausstehend: false }; sende.set(schluessel, st); }
    if (st.timer) { st.wert = wert; st.ausstehend = true; return; }
    sendeSchraube(schluessel, wert);
    st.timer = setTimeout(() => {
      st.timer = null;
      if (st.ausstehend) { st.ausstehend = false; planeSenden(schluessel, st.wert); }
    }, T.SENDE_VERZOEGERUNG_MS);
  }

  function aendern(s, wert) {
    z.live[s.schluessel] = wert;
    zeichneWerte();
    planeSenden(s.schluessel, wert);
  }

  function zuruecksetzen(s) {
    delete z.live[s.schluessel];
    const st = sende.get(s.schluessel);
    if (st) { clearTimeout(st.timer); st.timer = null; st.ausstehend = false; }
    zeichneWerte(true);
    sendeSchraube(s.schluessel, null);
  }

  q('.xl-liste').addEventListener('input', (e) => {
    const t = e.target;
    const zeile = t.closest?.('.xl-zeile');
    const eintrag = zeile && zeilen.get(zeile.dataset.schluessel);
    if (!eintrag) return;
    if (t.matches('.xl-regler')) {
      const r = T.wertAusEingabe(eintrag.s, t.value);
      if (r.ok) aendern(eintrag.s, r.wert);
    }
  });
  q('.xl-liste').addEventListener('change', (e) => {
    const t = e.target;
    const zeile = t.closest?.('.xl-zeile');
    const eintrag = zeile && zeilen.get(zeile.dataset.schluessel);
    if (!eintrag) return;
    const { s } = eintrag;
    if (t.matches('.xl-zahl')) {
      const r = T.wertAusEingabe(s, t.value);
      if (r.ok) { meldungZeigen('ok', ''); aendern(s, r.wert); }
      else { meldungZeigen('fehler', `${s.titel}: ${r.text}`); zeichneWerte(true); }
    } else if (t.matches('.xl-ja')) aendern(s, t.checked);
    else if (t.matches('.xl-auswahl')) {
      const r = T.wertAusEingabe(s, t.value);
      if (r.ok) aendern(s, r.wert);
    }
  });
  q('.xl-liste').addEventListener('click', (e) => {
    const b = e.target.closest?.('.xl-vorgabe');
    if (!b || b.disabled) return;
    const eintrag = zeilen.get(b.closest('.xl-zeile')?.dataset.schluessel);
    if (eintrag) zuruecksetzen(eintrag.s);
  });

  // ------------------------------------------------------------------------------------------ Profile

  const aktivesProfil = () => { const a = benutzerWert(T.PROFIL_AKTIV, null); return typeof a === 'string' ? a : null; };

  /** Setzt den Inhalt nur, wenn er sich geändert hat: der Takt läuft alle 2 s und darf Knöpfe nicht mitten im Klick austauschen. */
  const gezeichnet = new WeakMap();
  function setzeHtml(platz, html) {
    if (gezeichnet.get(platz) === html) return;
    gezeichnet.set(platz, html);
    platz.innerHTML = html;
  }

  function zeichneProfile() {
    const liste = T.profileListe(benutzerAlle(), aktivesProfil());
    const platz = q('.xl-profile');
    if (liste.length === 0) {
      setzeHtml(platz, '<div class="leise xl-keine">Noch kein Profil gespeichert. Stellschrauben und Varianten einstellen, einen Namen eintragen und «Aktuellen Satz speichern».</div>');
      return;
    }
    setzeHtml(platz, liste.map((p) => `<div class="xl-profil" data-kennung="${esc(p.kennung)}" data-aktiv="${p.aktiv ? '1' : '0'}">
      <div class="xl-profil-text"><b class="xl-profil-name">${esc(p.name)}</b> <span class="leise">${p.anzahl} Werte</span>${p.aktiv ? ' <span class="xl-marke xl-gilt">gilt für die App</span>' : ''}</div>
      <div class="xl-profil-knoepfe">
        <button type="button" class="dlg-knopf xl-profil-laden" data-kennung="${esc(p.kennung)}" title="Sendet alle Werte dieses Profils an die Brille">Laden</button>
        <button type="button" class="dlg-knopf xl-profil-aktiv" data-kennung="${esc(p.kennung)}" title="Die App holt dieses Profil beim Verbinden mit dem Katalog">${p.aktiv ? 'Nicht mehr für die App' : 'Für die App festlegen'}</button>
        <button type="button" class="dlg-knopf xl-profil-loeschen" data-kennung="${esc(p.kennung)}" data-name="${esc(p.name)}" title="Löscht das Profil">Löschen</button>
      </div></div>`).join(''));
  }

  async function lauf(arbeit) {
    if (z.laeuft) return;
    z.laeuft = true;
    try { await arbeit(); }
    catch (err) { fehlerMeldung(err); }
    finally { z.laeuft = false; if (offen) zeichneAlles(); }
  }

  q('.xl-profil-speichern').addEventListener('click', () => lauf(async () => {
    if (!z.katalog) throw new Error('Der Katalog ist nicht geladen.');
    const name = T.profilName(q('.xl-profilname').value);
    const kennung = T.profilKennung(name);
    if (!kennung) throw Object.assign(new Error('Bitte einen Namen eintragen (Buchstaben oder Ziffern).'), { vorschlag: null });
    const vorhanden = T.profileListe(benutzerAlle(), null).find((p) => p.kennung === kennung);
    if (vorhanden && !(await bestaetigen(`Das Profil «${vorhanden.name}» gibt es schon. Mit dem aktuellen Satz überschreiben?`, { ja: 'Überschreiben', titel: 'Profil überschreiben' }))) return;
    await benutzerSetzen(T.profilSchluessel(kennung), T.profilErzeugen(name, z.katalog, z.live));
    meldungZeigen('ok', `Das Profil «${name}» ist gespeichert.`);
  }));
  q('.xl-profilname').addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); q('.xl-profil-speichern').click(); } });

  q('.xl-profile').addEventListener('click', (e) => {
    const t = e.target.closest?.('button');
    if (!t || t.disabled) return;
    const kennung = t.dataset.kennung;
    const profil = benutzerWert(T.profilSchluessel(kennung), null);
    if (t.matches('.xl-profil-laden')) {
      lauf(async () => {
        const r = T.profilZumSenden(profil, z.katalog);
        for (const { schluessel, wert } of r.senden) {
          const st = sende.get(schluessel);
          if (st) { clearTimeout(st.timer); st.timer = null; st.ausstehend = false; }
          await senden('/api/xr/stellschraube', { schluessel, wert });
          z.live[schluessel] = wert;
        }
        zeichneWerte(true);
        meldungZeigen(r.senden.length > 0 ? 'ok' : 'warnung', T.profilGeladenText(profil?.name ?? kennung, r));
      });
    } else if (t.matches('.xl-profil-aktiv')) {
      lauf(async () => {
        const jetzt = aktivesProfil() === kennung;
        await benutzerSetzen(T.PROFIL_AKTIV, jetzt ? null : kennung);
        meldungZeigen('ok', jetzt ? 'Kein Profil gilt mehr für die App; sie nimmt die Vorgaben des Katalogs.' : `Das Profil «${profil?.name ?? kennung}» gilt jetzt für die App (sie holt es beim nächsten Verbinden).`);
      });
    } else if (t.matches('.xl-profil-loeschen')) {
      const name = t.dataset.name;
      bestaetigen(`Das Profil «${name}» löschen?`, { ja: 'Löschen', gefahr: true, titel: 'Profil löschen' }).then((ok) => {
        if (!ok) return;
        lauf(async () => {
          await benutzerSetzen(T.profilSchluessel(kennung), null);
          if (aktivesProfil() === kennung) await benutzerSetzen(T.PROFIL_AKTIV, null);
          meldungZeigen('ok', `Das Profil «${name}» ist gelöscht.`);
        });
      });
    }
  });

  // ------------------------------------------------------------------------------------------ Varianten

  function zeichneVarianten() {
    const platz = q('.xl-varianten');
    const liste = z.katalog?.varianten ?? [];
    if (liste.length === 0) { platz.innerHTML = '<div class="leise xl-keine">Der Katalog hat keine Varianten.</div>'; return; }
    // nur neu zeichnen, wenn sich etwas geändert hat (der Takt läuft alle 2 s und darf den Fokus nicht stehlen)
    const stand = JSON.stringify(liste.map((v) => [v.name, T.varianteWahl(v, z.live)]));
    if (platz.dataset.stand === stand) return;
    platz.dataset.stand = stand;
    platz.innerHTML = liste.map((v) => {
      const wahl = T.varianteWahl(v, z.live);
      return `<fieldset class="xl-variante" data-variante="${esc(v.name)}"><legend><b>${esc(v.titel)}</b> <code class="leise">labor.variante.${esc(v.name)}</code></legend>
        <div class="leise xl-klein">${esc(v.beschreibung)}</div>
        <div class="xl-optionen">${v.optionen.map((o, i) => `<label class="xl-option"><input type="radio" name="xl-var-${esc(v.name)}" value="${esc(o.id)}"${o.id === wahl ? ' checked' : ''}>
          <span><b>${'AB'[i] ?? i + 1}</b> ${esc(o.titel)}${o.id === v.vorgabe ? ' <span class="leise">(Vorgabe)</span>' : ''}</span></label>`).join('')}</div></fieldset>`;
    }).join('');
  }

  q('.xl-varianten').addEventListener('change', (e) => {
    const r = e.target;
    if (!r?.matches?.('input[type="radio"]')) return;
    const name = r.closest('.xl-variante')?.dataset.variante;
    lauf(async () => {
      try {
        await senden('/api/xr/labor/variante', { name, option: r.value });
        z.live[T.VARIANTE_VORSATZ + name] = r.value;
        const v = z.katalog.varianten.find((x) => x.name === name);
        meldungZeigen('ok', `${v?.titel ?? name}: ${T.varianteTitel(v, r.value)} gesendet.`);
      } catch (err) { q('.xl-varianten').dataset.stand = ''; throw err; }
    });
  });

  // ------------------------------------------------------------------------------------------ Prüfaufgaben

  const zeileDerAufgabe = (id) => (z.auswertung?.aufgaben ?? []).find((a) => a.aufgabe === id) ?? null;

  function zeichneAufgaben() {
    const platz = q('.xl-aufgaben');
    const liste = z.katalog?.aufgaben ?? [];
    const b = brillen();
    q('.xl-aufgabe-beenden').disabled = z.laeuft || b.anzahl === 0;
    setzeHtml(platz, liste.map((a) => {
      const st = T.aufgabeStatus(a, zeileDerAufgabe(a.id), z.gestartet[a.id] ?? null);
      const k = T.aufgabeKnopf({ aufgabe: a, brillen: b, laeuft: z.laeuft });
      return `<div class="xl-aufgabe" data-aufgabe="${esc(a.id)}" data-status="${esc(st.art)}">
        <div class="xl-aufgabe-kopf"><b class="xl-aufgabe-titel">${esc(a.titel)}</b> <code class="leise">${esc(a.id)}</code><span class="xl-status" data-art="${esc(st.art)}">${esc(st.text)}</span></div>
        <div class="xl-aufgabe-anleitung">${esc(a.anleitung)}</div>
        <div class="leise xl-klein">Ort: ${esc(a.ort)} · Erfolg: ${esc(a.erfolgskriterium)}</div>
        <div class="xl-aufgabe-knoepfe"><button type="button" class="dlg-knopf primaer xl-aufgabe-start" data-aufgabe="${esc(a.id)}"${k.frei ? '' : ' disabled'}>Aufgabe starten</button>
          ${k.frei ? '' : `<span class="leise xl-klein xl-grund">${esc(k.grund)}</span>`}</div></div>`;
    }).join(''));
  }

  q('.xl-aufgaben').addEventListener('click', (e) => {
    const t = e.target.closest?.('.xl-aufgabe-start');
    if (!t || t.disabled) return;
    const id = t.dataset.aufgabe;
    lauf(async () => {
      await auswertungLesen();   // der Stand vorher: eine neue Zeile danach heisst «gemessen»
      const a = z.katalog.aufgaben.find((x) => x.id === id);
      const r = await senden('/api/xr/labor/aufgabe', { aufgabe: id });
      z.gestartet[id] = { laeufeVorher: zeileDerAufgabe(id)?.laeufe ?? 0 };
      meldungZeigen('ok', `Die Aufgabe «${a?.titel ?? id}» ist an ${r.gesendet} Brille${r.gesendet === 1 ? '' : 'n'} gesendet.`);
    });
  });
  q('.xl-aufgabe-beenden').addEventListener('click', () => lauf(async () => {
    await senden('/api/xr/labor/aufgabe', { aufgabe: null });
    z.gestartet = {};
    meldungZeigen('ok', 'Die laufende Aufgabe ist beendet.');
  }));

  // ------------------------------------------------------------------------------------------ Auswertung

  async function auswertungLesen() {
    try { z.auswertung = await holen('/api/xr/labor/auswertung'); z.auswertungFehler = null; }
    catch (err) { z.auswertungFehler = err?.message || String(err); }
    if (offen) { zeichneAuswertung(); if (z.bereich === 'aufgaben') zeichneAufgaben(); }
  }

  function tabelle(titel, kopf, zeilen2) {
    return `<table class="xl-tabelle" aria-label="${esc(titel)}"><thead><tr>${kopf.map((k) => `<th scope="col">${esc(k)}</th>`).join('')}</tr></thead>`
      + `<tbody>${zeilen2.map((r) => `<tr>${r.map((c, i) => (i === 0 ? `<th scope="row">${esc(c)}</th>` : `<td>${esc(c)}</td>`)).join('')}</tr>`).join('')}</tbody></table>`;
  }

  function zeichneAuswertung() {
    const platz = q('.xl-auswertung');
    const a = z.auswertung;
    q('.xl-auswertung-stand').textContent = a ? `${a.laeufe ?? 0} Läufe aus ${a.dateien ?? 0} Dateien${a.abgeschnitten ? ' (nur die neuesten)' : ''}${a.ungueltigeZeilen ? ` · ${a.ungueltigeZeilen} ungültige Zeilen übersprungen` : ''}` : '';
    if (z.auswertungFehler) { setzeHtml(platz, `<div class="xl-box fehler" role="alert"><b>${esc(z.auswertungFehler)}</b></div>`); return; }
    if (!a) { setzeHtml(platz, '<div class="leise xl-keine">Wird gelesen …</div>'); return; }
    if (T.auswertungLeer(a)) { setzeHtml(platz, `<div class="xl-leer" data-leer="1">${esc(T.LEERZUSTAND_TEXT)}<div class="leise xl-klein">Ordner: ${esc(a.ordner)}</div></div>`); return; }
    const kopf = ['', 'Läufe', 'Ø Zeit', 'Fehlgriffe', 'Abbrüche', 'Erfolg'];
    let html = '';
    for (const x of T.aufgabenMitLaeufen(a)) {
      html += `<details class="xl-auswertung-aufgabe" open data-aufgabe="${esc(x.aufgabe)}"><summary><b>${esc(x.titel)}</b> <span class="leise">${x.laeufe} ${x.laeufe === 1 ? 'Lauf' : 'Läufe'} · Ø ${esc(T.dauerText(x.mittelZeitMs))}${x.bekannt ? '' : ' · nicht im Katalog'}</span></summary>`
        + tabelle(`${x.titel}: je Variante`, ['Variante', ...kopf.slice(1)], x.varianten.map((v) => T.auswertungZellen(v, T.varianteAnzeige(v.variante))))
        + tabelle(`${x.titel}: je Fassung der App`, ['Fassung der App', ...kopf.slice(1)], x.fassungen.map((f) => T.auswertungZellen(f, f.app)))
        + '</details>';
    }
    const fassungen = a.fassungen ?? [];
    if (fassungen.length > 0) {
      html += `<h4 class="xl-gruppe-titel">Verlauf je Fassung der App</h4>`
        + tabelle('Verlauf je Fassung der App', ['Fassung der App', 'Läufe', 'Aufgaben', 'Ø Zeit', 'Fragebogen', 'SUS', 'TLX', 'Übelkeit'],
          fassungen.map((f) => [f.app, String(f.laeufe ?? 0), String(f.aufgaben ?? 0), T.dauerText(f.mittelZeitMs), String(f.fragebogen?.anzahl ?? 0),
            T.mittelText(f.fragebogen?.sus, 1), T.mittelText(f.fragebogen?.tlx, 1), T.mittelText(f.fragebogen?.uebelkeit, 1)]));
    }
    const fb = a.fragebogen ?? [];
    if (fb.length > 0) {
      html += `<h4 class="xl-gruppe-titel">Fragebogen</h4>`
        + tabelle('Fragebogen', ['Name', 'Zeit', 'Fassung der App', 'Antworten'], fb.map((f) => [f.name ?? '', T.zeitText(f.zeit), f.app ?? '–', T.fragebogenKurz(f)]));
    }
    setzeHtml(platz, html);
  }

  q('.xl-auswertung-neu').addEventListener('click', () => auswertungLesen());

  // ------------------------------------------------------------------------------------------ Fragebogen

  function skalaRadio(name, von, bis) {
    const felder = [];
    for (let i = von; i <= bis; i++) felder.push(`<label class="xl-punkt"><input type="radio" name="${esc(name)}" value="${i}"><span>${i}</span></label>`);
    return `<div class="xl-skala" role="radiogroup">${felder.join('')}</div>`;
  }

  function baueFragebogen() {
    const platz = q('.xl-fragebogen');
    const fb = z.fragebogen;
    if (!fb) { platz.innerHTML = '<div class="leise xl-keine">Wird gelesen …</div>'; return; }
    const hinweis = T.wortlautHinweis(fb);
    const teil = (id) => fb.teile.find((t) => t.id === id);
    const komfort = teil('komfort');
    const sus = teil('sus');
    const tlx = teil('nasa_tlx');
    const frage = (t, f, inhalt) => `<div class="xl-frage" data-frage="${esc(f.id)}"><div class="xl-frage-text">${f.wortlaut ? esc(f.wortlaut) : `<i class="xl-platzhalter">${esc(f.platzhalter ?? 'Wortlaut fehlt')}</i>`}</div>${inhalt}</div>`;
    const komfortHtml = komfort.fragen.map((f) => frage(komfort, f, f.art === 'text'
      ? `<textarea class="xl-text" data-komfort-text rows="3" maxlength="2000" aria-label="${esc(f.wortlaut)}"></textarea>`
      : `${skalaRadio(`xl-k-${f.id}`, f.skala.min, f.skala.max)}<div class="leise xl-klein xl-skala-ende"><span>${esc(f.skala.von ?? '')}</span><span>${esc(f.skala.bis ?? '')}</span></div>`)).join('');
    const susHtml = sus.fragen.map((f, i) => frage(sus, f, `${sus.wortlautVorhanden ? skalaRadio(`xl-sus-${i}`, 1, 5) : '<span class="leise xl-klein">nicht beantwortbar, solange der Wortlaut fehlt</span>'}`)).join('');
    const tlxHtml = tlx.fragen.map((f) => frage(tlx, f, tlx.wortlautVorhanden
      ? `<input type="number" class="xl-tlx" data-tlx="${esc(f.id)}" min="0" max="100" step="1" aria-label="${esc(f.wortlaut)}" data-kein-enter="1">`
      : '<span class="leise xl-klein">nicht beantwortbar, solange der Wortlaut fehlt</span>')).join('');
    platz.innerHTML = `
      ${hinweis ? `<div class="xl-box warnung xl-wortlaut-hinweis" role="note"><b>${esc(hinweis)}</b></div>` : ''}
      <div class="xl-fb-kopf">
        <div class="xl-fb-zeile"><label class="qd-etikett" for="xl-fb-name">Testperson</label><input id="xl-fb-name" type="text" class="xl-fb-name" maxlength="40" spellcheck="false" autocomplete="off" data-kein-enter="1" placeholder="Name" aria-label="Name der Testperson"></div>
        <div class="xl-fb-zeile"><label class="qd-etikett" for="xl-fb-app">Fassung der App</label><input id="xl-fb-app" type="text" class="xl-fb-app" maxlength="40" spellcheck="false" autocomplete="off" data-kein-enter="1" placeholder="aus der verbundenen Brille" aria-label="Fassung der App"></div>
        <div class="leise xl-klein xl-fb-varianten"></div>
      </div>
      <fieldset class="xl-teil" data-teil="komfort"><legend><b>${esc(komfort.titel)}</b></legend>${komfortHtml}</fieldset>
      <fieldset class="xl-teil" data-teil="sus" ${sus.wortlautVorhanden ? '' : 'disabled'}><legend><b>${esc(sus.titel)}</b></legend>
        <div class="leise xl-klein">Quelle der Bewertung: ${esc(sus.quelle)}</div>${susHtml}</fieldset>
      <fieldset class="xl-teil" data-teil="nasa_tlx" ${tlx.wortlautVorhanden ? '' : 'disabled'}><legend><b>${esc(tlx.titel)}</b></legend>
        <div class="leise xl-klein">Quelle der Bewertung: ${esc(tlx.quelle)}</div>${tlxHtml}</fieldset>
      <div class="xl-fb-zeile"><label class="qd-etikett" for="xl-fb-notiz">Bemerkungen von Christian</label><textarea id="xl-fb-notiz" class="xl-text xl-fb-notiz" rows="2" maxlength="2000" aria-label="Bemerkungen"></textarea></div>
      <div class="xl-fb-knoepfe"><button type="button" class="dlg-knopf primaer xl-fb-speichern">Fragebogen speichern</button></div>`;
    zeichneFragebogenFassung();
  }

  /** Fassung der App und Varianten für den Fragebogen: aus der verbundenen Brille und den gewählten Varianten (die Fassung lässt sich von Hand überschreiben). */
  function zeichneFragebogenFassung() {
    const feld = q('.xl-fb-app');
    if (!feld) return;
    if (!z.appVonHand && document.activeElement !== feld) feld.value = brillen().app ?? '';
    const v = T.variantenWahl(z.katalog, z.live);
    q('.xl-fb-varianten').textContent = Object.keys(v).length > 0 ? `Gewählte Varianten werden mitgespeichert: ${Object.entries(v).map(([k, o]) => `${k} = ${o}`).join(', ')}.` : '';
  }

  q('.xl-fragebogen').addEventListener('input', (e) => { if (e.target.matches?.('.xl-fb-app')) z.appVonHand = true; });

  function fragebogenLesen() {
    const radio = (name) => q(`input[name="${name}"]:checked`)?.value ?? '';
    const sus = z.fragebogen.teile.find((t) => t.id === 'sus');
    const tlx = z.fragebogen.teile.find((t) => t.id === 'nasa_tlx');
    return {
      name: q('.xl-fb-name').value, app: q('.xl-fb-app').value.trim(), varianten: T.variantenWahl(z.katalog, z.live),
      komfort: { uebelkeit: radio('xl-k-uebelkeit'), augen: radio('xl-k-augen'), natuerlich: radio('xl-k-natuerlich') },
      text: q('[data-komfort-text]').value, notiz: q('.xl-fb-notiz').value,
      sus: sus.wortlautVorhanden ? sus.fragen.map((_, i) => radio(`xl-sus-${i}`)) : null,
      tlx: tlx.wortlautVorhanden ? Object.fromEntries(tlx.fragen.map((f) => [f.id, q(`[data-tlx="${f.id}"]`).value])) : null,
    };
  }

  q('.xl-fragebogen').addEventListener('click', (e) => {
    if (!e.target.closest?.('.xl-fb-speichern')) return;
    lauf(async () => {
      const k = T.fragebogenKoerper(fragebogenLesen());
      if (!k.ok) throw Object.assign(new Error(k.text), { vorschlag: null });
      const rec = await senden('/api/xr/labor/fragebogen', k.koerper);
      meldungZeigen('ok', T.gespeichertText(rec));
      // Formular leeren (der Name bleibt für den nächsten Durchgang)
      for (const r of qa('.xl-fragebogen input[type="radio"]')) r.checked = false;
      for (const t of qa('.xl-fragebogen .xl-text, .xl-fragebogen .xl-tlx')) t.value = '';
      await auswertungLesen();
    });
  });

  // ------------------------------------------------------------------------------------------ Bereiche, Takt, Laden

  function bereichWaehlen(id) {
    z.bereich = id;
    for (const k of qa('.xl-reiter-knopf')) {
      const an = k.dataset.bereich === id;
      k.setAttribute('aria-selected', an ? 'true' : 'false');
      k.classList.toggle('aktiv', an);
    }
    for (const b of qa('.xl-bereich')) b.hidden = b.dataset.bereich !== id;
    if (id === 'auswertung' || id === 'aufgaben') auswertungLesen();
    zeichneAlles();
  }
  q('.xl-reiter').addEventListener('click', (e) => { const b = e.target.closest?.('.xl-reiter-knopf'); if (b) bereichWaehlen(b.dataset.bereich); });

  function zeichneAlles() {
    if (!offen) return;
    zeichneKopf();
    if (z.katalog) {
      zeichneWerte();
      if (z.bereich === 'profile') zeichneProfile();
      if (z.bereich === 'varianten') zeichneVarianten();
      if (z.bereich === 'aufgaben') zeichneAufgaben();
      if (z.bereich === 'fragebogen') zeichneFragebogenFassung();
    }
    for (const b of qa('.xl-profil-laden')) b.disabled = z.laeuft;
    q('.xl-profil-speichern').disabled = z.laeuft || !z.katalog;
  }

  /** Liest die verbundenen Brillen und die gesetzten Stellschrauben; Regler, die der Mensch gerade bewegt, behalten ihren Wert. */
  async function teilnehmerLesen() {
    try {
      const t = await holen('/api/xr/teilnehmer');
      if (!offen) return;
      z.teilnehmer = t;
      const server = t.stellschrauben ?? {};
      const neu = { ...server };
      for (const [k, st] of sende) if ((st.timer || st.ausstehend) && k in z.live) neu[k] = z.live[k];
      z.live = neu;
    } catch { if (offen && !z.teilnehmer) z.teilnehmer = { teilnehmer: [], stellschrauben: {} }; }
    zeichneAlles();
  }

  async function katalogLesen() {
    try {
      z.katalog = await holen('/api/xr/labor/katalog');
      z.katalogFehler = null;
    } catch (err) { z.katalog = null; z.katalogFehler = { text: err?.message || String(err), vorschlag: err?.vorschlag || null }; }
    if (!offen) return;
    const f = q('.xl-fehler');
    f.hidden = !z.katalogFehler;
    f.innerHTML = z.katalogFehler ? `<b>${esc(z.katalogFehler.text)}</b>${z.katalogFehler.vorschlag ? `<div class="qd-vorschlag">Vorschlag: ${esc(z.katalogFehler.vorschlag)}</div>` : ''}` : '';
    if (z.katalog) baueStellschrauben();
  }

  async function fragebogenLesenVomServer() {
    try { z.fragebogen = await holen('/api/xr/labor/fragebogen'); } catch { z.fragebogen = null; }
    if (offen) baueFragebogen();
  }

  function beenden() {
    offen = false;
    clearInterval(timer); clearInterval(auswertungTimer);
    clearTimeout(meldungTimer);
    // ausstehende Reglerwerte noch senden: der letzte Wert darf nicht verloren gehen
    for (const [k, st] of sende) { clearTimeout(st.timer); if (st.ausstehend) sendeSchraube(k, st.wert); }
    sende.clear();
  }

  const geschlossen = zeigen({
    titel: 'XR-Labor', inhalt: w, breite: 760, klasse: 'dlg-hoch xr-labor',
    beiOeffnen: async () => {
      bereichWaehlen('stellschrauben');
      await Promise.all([benutzerLaden().catch(() => {}), katalogLesen(), teilnehmerLesen(), fragebogenLesenVomServer()]);
      if (!offen) return;
      zeichneAlles();
      timer = setInterval(teilnehmerLesen, T.TAKT_MS);
      auswertungTimer = setInterval(() => { if (offen && (z.bereich === 'auswertung' || z.bereich === 'aufgaben')) auswertungLesen(); }, T.AUSWERTUNG_TAKT_MS);
    },
    knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }],
  }).then((wert) => { beenden(); offenerDialog = null; return wert; });
  offenerDialog = geschlossen;
  return geschlossen;
}

/** Für die Menüband-Aktion: öffnet den Dialog und fängt Fehler ab. */
export function xrLaborDialogOeffnen() {
  try { return xrLaborDialog(); }
  catch (e) { meldung(e.message ?? String(e), 'fehler'); return Promise.resolve(null); }
}
