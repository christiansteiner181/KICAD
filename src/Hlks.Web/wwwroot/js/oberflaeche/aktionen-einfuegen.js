// Aktionen des Registers «Einfügen» (Modul 1b, Auftrag C1) und die zugehörigen Dialoge: IFC übernehmen (Hochladen → ifc_pruefen →
// Übersicht, Lagebericht mit Wahl der Ausrichtung, Vorschau = Trockenlauf von ifc_uebernehmen, Übernehmen), CAD/PDF/Bild einfügen
// (Hochladen → unterlage_einfuegen), Kalibrieren (Werkzeug), Unterlagen verwalten; dazu «Koordinaten» im Register Verwalten und die
// Kontextaktionen der Unterlagen im Projektbrowser (anzeigen, Deckkraft, Lagebericht, entfernen).
// Keine Fachlogik: alles geht über Befehle und Abfragen des Kerns (ifc_pruefen, ifc_uebernehmen, unterlage_einfuegen,
// unterlage_kalibrieren, element_aendern); die Texte und das HTML der Dialoge stehen in einfuegen-text.js (in Node getestet).
// Ist ein Befehl im Kern noch nicht da, zeigt die Oberfläche dessen Fehlermeldung.

import { zustand, element, derKlasse, alleElemente, abonnieren, auswaehlen } from '../zustand.js';
import { abfrage, ApiFehler } from '../api.js';
import { registrieren, hinweisSetzen, ausfuehren } from './register.js';
import { zeigen, meldung } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { esc, knoten, entprellen } from './werkzeug.js';
import * as T from './einfuegen-text.js';
import { originalStil, gueltigeFarbe, gueltigeDeckkraft, ORIGINAL_FARBE } from '../ansichten/ifc-darstellung.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { kalibrierenWerkzeug, kalibrierbareUnterlagen } from '../werkzeuge/kalibrieren.js';
import { transformationVon } from '../werkzeuge/kalibrierung.js';

const projektOffen = () => !!zustand.status?.offen;
const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';
const mb = (bytes) => (bytes / 1048576).toFixed(bytes < 10485760 ? 1 : 0);

// ---------------------------------------------------------------------------------------------- Datei wählen und hochladen

/** Öffnet die Dateiauswahl des Browsers. Gibt die Datei zurück oder null (Abbruch). Das Feld hängt kurz im Dokument (id einfuegen-datei). */
export function dateiWaehlen(accept) {
  return new Promise((fertig) => {
    document.getElementById('einfuegen-datei')?.remove();
    const i = document.createElement('input');
    i.type = 'file'; i.accept = accept; i.id = 'einfuegen-datei';
    i.style.cssText = 'position:fixed;left:-9999px;top:0;opacity:0;width:1px;height:1px';
    i.addEventListener('change', () => { const f = i.files?.[0] ?? null; i.remove(); fertig(f); });
    i.addEventListener('cancel', () => { i.remove(); fertig(null); });
    document.body.appendChild(i);
    i.click();
  });
}

/**
 * Lädt eine Datei in den Ordner unterlagen des Projekts (POST /api/unterlagen, roh mit ?name=). fortschritt(geladen, gesamt).
 * Antwort { name, stand, bytes }; Fehler als ApiFehler (Meldung und Vorschlag des Servers).
 */
export function hochladen(datei, fortschritt = null) {
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
    x.onabort = () => fehl(new ApiFehler('Das Hochladen wurde abgebrochen.', null, 0));
    x.send(datei);
  });
}

const spinner = (text) => `<div class="lauf"><span class="spinner" aria-hidden="true"></span><span class="lauf-text">${esc(text)}</span></div>`;
// Der Kern nennt bei «nicht eindeutig» den Lagebericht als Text (mehrere Zeilen) und einen Vorschlag: Zeilenumbrüche bleiben
const fehlerKasten = (r) => `<div class="fehlerkasten" role="alert"><b style="white-space:pre-line">${esc(r.fehler || r.meldung || 'Abgelehnt.')}</b>${r.vorschlag ? `<div style="white-space:pre-line">Vorschlag: ${esc(r.vorschlag)}</div>` : ''}</div>`;

// ---------------------------------------------------------------------------------------------- IFC übernehmen

const ohnePruefsumme = T.ohnePruefsumme;

/** Register «Einfügen» → IFC übernehmen. p.datei (File) überspringt die Dateiauswahl (Tests). */
async function ifcUebernehmen(p) {
  const datei = p?.datei ?? await dateiWaehlen('.ifc');
  if (!datei) return;
  if (T.artVonDatei(datei.name) !== 'ifc') { meldung(`«${datei.name}» ist keine IFC-Datei.`, 'warnung', { vorschlag: 'Eine Datei mit der Endung .ifc wählen (IFC4 oder IFC2x3).' }); return; }
  await ifcDialog(datei);
}

async function ifcDialog(datei) {
  const w = knoten(`<div class="ifc-dlg"><div class="ifc-status"></div><div class="ifc-inhalt" hidden></div></div>`);
  const status = w.querySelector('.ifc-status'), inhalt = w.querySelector('.ifc-inhalt');
  const q = (s) => w.querySelector(s), qa = (s) => [...w.querySelectorAll(s)];
  let abgelegt = null, pruefung = null, bereit = false, zu = false;

  const lesen = () => ({
    bereiche: qa('.b-haken:checked').map((x) => x.value),
    geschosse: qa('.g-haken:checked').map((x) => x.value),
    ausrichtung: q('input[name=ausrichtung]:checked')?.value ?? null,
    grundlage: q('.o-grundlage')?.checked ?? true,
    konflikte: q('.o-konflikte')?.value ?? 'kicad',
    koordinationskoerper: q('.o-kk')?.value ?? '',
  });
  const parameter = () => {
    const l = lesen();
    return T.uebernahmeParameter({
      datei: abgelegt, bereiche: l.bereiche, alleBereiche: T.BEREICHE.map((b) => b[0]), geschosse: l.geschosse,
      alleGeschosse: pruefung.geschosse.map((g) => g.name), grundlage: l.grundlage, ausrichtung: l.ausrichtung,
      koordinationskoerper: l.koordinationskoerper, konflikte: l.konflikte,
    });
  };
  // Übernehmen verlangt bei Abweichung die ausdrückliche Wahl; die Vorschau (Trockenlauf) rechnet auch ohne Wahl mit dem Projektbezug
  const pruefen = (mitWahl = true) => {
    if (!bereit) return 'Die Datei wird noch geladen und geprüft.';
    const l = lesen();
    return T.uebernahmePruefen({ bereiche: l.bereiche, geschosse: l.geschosse, lage: mitWahl ? pruefung.lage : null, ausrichtung: l.ausrichtung });
  };

  function aufbauen() {
    const lage = pruefung.lage, bezug = pruefung.bezugProjekt;
    const frueher = derKlasse('Unterlage').find((u) => u.werte?.art === 'ifc' && ohnePruefsumme(String(u.werte?.original ?? '').split(/[\\/]/).pop()) === datei.name);
    const geschosse = [...(pruefung.geschosse ?? [])].sort((a, b) => (b.kote ?? 0) - (a.kote ?? 0));
    const optionen = T.ausrichtungOptionen(lage, bezug, T.projektMerkmale(alleElemente()));
    const eindeutig = T.geschossNamenEindeutig(geschosse);
    const darf = T.wahlMoeglich(lage);
    // Einpassen ist auch bei der ersten Datei wählbar, wenn der Kern es gerechnet hat (Projekt mit schon gezeichnetem Inhalt); die anderen Wahlen nicht
    const frei = (o) => o.moeglich && (darf || o.sofort);
    const vorwahl = T.vorgabeWahl(lage);
    const keineWahl = lage?.bewertung === 'erster' ? 'Erste IFC-Datei: sie legt den Projektbezug fest. Eine Wahl ist nicht nötig.'
      : lage?.bewertung === 'passt' ? 'Die Lage passt zum Projektbezug (Toleranz 10 mm, 0.01°). Eine Wahl ist nicht nötig.'
      : lage?.bewertung === 'ausdruecklich' ? 'Der Bezug ist ausdrücklich vorgegeben.' : '';
    const uebersicht = `
      ${frueher ? `<p class="dlg-text leise">Eine Datei gleichen Namens ist schon im Projekt (${esc(frueher.kennzeichen)}).</p>` : ''}
      <h3>Übersicht</h3>
      <div class="ifc-spalten">
        <div>
          <table class="wtab">${T.kopfZeilen(pruefung).map(([k, v]) => `<tr><th>${esc(k)}</th><td>${esc(v)}</td></tr>`).join('')}</table>
          ${pruefung.hinweis ? `<p class="leise">${esc(pruefung.hinweis)}</p>` : ''}
          <h4>Geschosse</h4>
          ${eindeutig ? '' : '<p class="warnung">Die Geschossnamen der Datei sind nicht eindeutig (z. B. zweimal «OG»). Darum werden alle Geschosse übernommen; einzelne Geschosse lassen sich erst wählen, wenn der Kern sie nach der Kote unterscheidet.</p>'}
          <table class="tab kompakt"><thead><tr><th></th><th>Geschoss</th><th class="zahl">Kote [m]</th><th class="zahl">Objekte</th></tr></thead><tbody>${
            geschosse.length === 0 ? '<tr><td colspan="4" class="leise">Die Datei hat keine Geschosse.</td></tr>' : geschosse.map((g) =>
              `<tr><td><input type="checkbox" class="g-haken" value="${esc(g.name)}" checked${eindeutig ? '' : ' disabled'} aria-label="${esc(g.name)} übernehmen"></td><td>${esc(g.name)}</td><td class="zahl">${esc(T.zahl(g.kote))}</td><td class="zahl">${esc(g.produkte)}</td></tr>`).join('')}</tbody></table>
        </div>
        <div>
          <h4>Bereiche</h4>
          ${T.BEREICHE.map(([id, titel, satz]) => `<label class="bereich"><input type="checkbox" class="b-haken" value="${id}" checked><span><b>${esc(titel)}</b><br><span class="leise">${esc(satz)}</span></span></label>`).join('')}
          <h4>Was die Datei enthält (wird zugeordnet)</h4>
          ${T.zaehlTabelle(pruefung.zuordnung, ['Klasse', 'Anzahl'])}
          <h4>Nicht verstanden (bleibt als Hintergrund sichtbar)</h4>
          ${T.zaehlTabelle(pruefung.ohneKlasse, ['IFC-Entität', 'Anzahl'])}
        </div>
      </div>
      `;
    const lageBlock = `
      ${lage?.bewertung === 'abweichung' ? T.abweichungKasten(lage) : ''}
      <h3>Lagebericht und Ausrichtung</h3>
      ${T.lageHtml(lage, bezug, { ohneKasten: true })}
      <fieldset class="ausrichtung"${optionen.some(frei) ? '' : ' disabled'}><legend>Ausrichtung wählen</legend>
        ${keineWahl ? `<p class="leise">${esc(keineWahl)}</p>` : ''}
        ${lage?.bewertung === 'gewaehlt' ? '<p class="leise">Eine frühere Wahl gilt weiter, solange die Abweichung gleich bleibt.</p>' : ''}
        ${optionen.map((o) => `<label class="wahl${frei(o) ? '' : ' aus'}"><input type="radio" name="ausrichtung" value="${o.wert}"${frei(o) ? '' : ' disabled'}${vorwahl === o.wert ? ' checked' : ''}><span><b>${esc(o.titel)}</b>${o.empfohlen ? ' <span class="empfohlen">empfohlen</span>' : ''}<br><span class="leise">${esc(o.satz)}${o.moeglich ? '' : ' ' + esc(o.grund)}</span>${T.anteilText(o.anteil) ? `<br><span class="leise wahl-anteil">${esc(T.anteilText(o.anteil))}</span>` : ''}</span></label>`).join('')}
      </fieldset>`;
    const rest = `
      <details class="ifc-weitere"><summary>Weitere Optionen</summary>
        <label class="haken"><input type="checkbox" class="o-grundlage" checked> Nicht Übernommenes als Hintergrund zeigen (Netz der Unterlage)</label>
        <label class="fz"><span>Bei Änderungen in KICAD</span><select class="o-konflikte"><option value="kicad">KICAD behalten (Vorgabe)</option><option value="ifc">neue IFC-Fassung übernehmen</option></select></label>
        <label class="fz"><span>Name Koordinationskörper</span><input type="text" class="o-kk" placeholder="nur wenn nicht «Koordinationskörper»"></label>
      </details>
      <h3>Vorschau</h3>
      <div class="ifc-vorschau"><p class="leise">Noch keine Vorschau. «Vorschau» rechnet einen Trockenlauf: nichts wird geändert, die Liste zeigt neu, geändert und gelöscht.</p></div>`;
    // Bei Abweichung steht die Lage mit der Wahl vor der Übersicht: die ausdrückliche Wahl darf nicht übersehen werden
    inhalt.innerHTML = (lage?.bewertung === 'abweichung' ? lageBlock + uebersicht : uebersicht + lageBlock) + rest;
    status.hidden = true;
    inhalt.hidden = false;
    qa('input[name=ausrichtung]').forEach((r) => r.addEventListener('change', () => ctrlFehler?.(null)));
  }

  let ctrlFehler = null;
  const vorschauFeld = () => q('.ifc-vorschau');

  async function vorschau(ctrl) {
    const f = pruefen(false);
    if (f !== true) { ctrl.fehler(f); return; }
    const einpassen = lesen().ausrichtung === 'einpassen';
    vorschauFeld().innerHTML = spinner(einpassen ? 'Einpassen und Trockenlauf laufen … bei grossen Modellen einige Sekunden bis Minuten.' : 'Trockenlauf läuft … bei grossen Dateien kann das einige Minuten dauern.');
    hinweisSetzen(einpassen ? 'IFC wird eingepasst, Vorschau wird gerechnet …' : 'IFC-Vorschau wird gerechnet …');
    let r;
    try { r = await sende('ifc_uebernehmen', parameter(), { trockenlauf: true }); }
    finally { hinweisSetzen(''); }
    if (!r.angenommen) { vorschauFeld().innerHTML = fehlerKasten(r); return; }
    const befunde = (r.neueBefunde ?? []).length;
    vorschauFeld().innerHTML = `<p class="leise">Trockenlauf – es wurde nichts geändert.</p>${T.einpassenHtml(T.einpassenAusErgebnis(r))}${T.aenderungenTabelle(r.aenderungen)}`
      + (befunde ? `<p class="warnung">Neue Prüfbefunde nach der Übernahme: ${befunde}</p>` : '')
      + T.hinweiseHtml(r.hinweise);
    vorschauFeld().scrollIntoView({ block: 'nearest' });
  }

  async function uebernehmen(ctrl) {
    const f = pruefen();
    if (f !== true) { ctrl.fehler(f); return; }
    vorschauFeld().innerHTML = spinner('Übernahme läuft … bei grossen Dateien kann das einige Minuten dauern. Die Oberfläche bleibt bedienbar.');
    hinweisSetzen('IFC wird übernommen …');
    let r;
    try { r = await sende('ifc_uebernehmen', parameter()); }
    finally { hinweisSetzen(''); }
    if (!r.angenommen) {
      vorschauFeld().innerHTML = fehlerKasten(r);
      if (zu) meldung(r.fehler || 'Die IFC-Übernahme wurde abgelehnt.', 'warnung', { vorschlag: r.vorschlag });
      return;
    }
    ctrl.schliessen(true);
    await ergebnisDialog(r, datei.name);
  }

  status.innerHTML = spinner('Wird hochgeladen …');
  zeigen({
    titel: `IFC übernehmen – ${datei.name}`, inhalt: w, breite: 920, klasse: 'dlg-hoch ifc',
    beiOeffnen: (ctrl) => { ctrlFehler = ctrl.fehler; laden(ctrl); },
    knoepfe: [
      { text: 'Abbrechen', wert: false },
      { text: 'Vorschau', schliesst: false, pruefen: () => pruefen(false), klick: vorschau },
      { text: 'Übernehmen', primaer: true, schliesst: false, pruefen: () => pruefen(true), klick: uebernehmen },
    ],
  }).then(() => { zu = true; });

  async function laden(ctrl) {
    try {
      const hoch = await hochladen(datei, (a, b) => { status.innerHTML = spinner(`Wird hochgeladen … ${mb(a)} von ${mb(b)} MB`); });
      abgelegt = hoch.name;
      status.innerHTML = spinner('Datei wird geprüft (Fassung, Geschosse, Lage) …');
      pruefung = await abfrage('ifc_pruefen', { datei: abgelegt });
      aufbauen();
      bereit = true;
    } catch (e) {
      console.error(e);
      status.innerHTML = `<div class="fehlerkasten" role="alert"><b>${esc(e.message ?? e)}</b>${e.vorschlag ? `<div>Vorschlag: ${esc(e.vorschlag)}</div>` : ''}</div>`;
    }
  }
}

/** Ergebnis einer Übernahme: Änderungen je Klasse und alle Hinweise des Kerns. */
async function ergebnisDialog(r, dateiname) {
  // Lagebericht «Einpassen»: aus dem Ergebnis, sonst aus der gespeicherten Lage der Unterlage (falls das Modell sie schon kennt)
  const gespeichert = () => derKlasse('Unterlage').find((u) => u.werte?.art === 'ifc' && u.werte?.lage?.ausrichtung === 'einpassen'
    && ohnePruefsumme(String(u.werte?.original ?? '').split(/[\\/]/).pop()) === dateiname)?.werte?.lage?.einpassen ?? null;
  const einpassen = T.einpassenAusErgebnis(r) ?? gespeichert();
  const w = knoten(`<div><p class="dlg-text"><b>${esc(dateiname)}</b> ist übernommen. Rückgängig geht mit Strg+Z (ein Paket).</p>
    ${T.einpassenHtml(einpassen)}
    ${T.aenderungenTabelle(r.aenderungen)}
    ${(r.neueBefunde ?? []).length ? `<p class="warnung">Neue Prüfbefunde: ${r.neueBefunde.length} (Register Verwalten → Prüfbefunde).</p>` : ''}
    <h4>Hinweise</h4>${T.hinweiseHtml(r.hinweise) || '<p class="leise">Keine.</p>'}</div>`);
  await zeigen({ titel: 'IFC übernommen', inhalt: w, breite: 760, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

// ---------------------------------------------------------------------------------------------- CAD, PDF, Bild einfügen

const EINFUEGEN = {
  cad: { titel: 'CAD einfügen', accept: '.dwg,.dxf', arten: ['dwg', 'dxf'], endungen: 'DWG oder DXF' },
  pdf: { titel: 'PDF einfügen', accept: '.pdf', arten: ['pdf'], endungen: 'PDF' },
  bild: { titel: 'Bild einfügen', accept: '.png,.jpg,.jpeg', arten: ['bild'], endungen: 'PNG oder JPG' },
};

const geschosseSortiert = () => derKlasse('Geschoss').sort((a, b) => (b.werte?.kote ?? 0) - (a.werte?.kote ?? 0));

/** Einfügen von CAD, PDF oder Bild: Datei wählen → Dialog (Geschoss, bei PDF Seite und Massstab, bei Bild Massstab) → Hochladen → unterlage_einfuegen. */
async function unterlageEinfuegen(was, p) {
  const def = EINFUEGEN[was];
  const geschosse = geschosseSortiert();
  if (geschosse.length === 0) { meldung('Es gibt noch kein Geschoss.', 'info', { vorschlag: 'Zuerst ein Geschoss anlegen (oder ein IFC übernehmen), dann die Unterlage einfügen.' }); return; }
  const datei = p?.datei ?? await dateiWaehlen(def.accept);
  if (!datei) return;
  const art = T.artVonDatei(datei.name);
  if (!def.arten.includes(art)) { meldung(`«${datei.name}» ist keine ${def.endungen}-Datei.`, 'warnung', { vorschlag: `Eine Datei vom Typ ${def.endungen} wählen.` }); return; }

  const vorGeschoss = element(zustand.aktiveAnsicht)?.geschoss;
  const w = knoten(`<div class="ansichtform">
    <p class="dlg-text">Datei: <b>${esc(datei.name)}</b> (${esc(mb(datei.size))} MB) · ${esc(T.ART_NAMEN[art])}</p>
    <label class="fz"><span>Geschoss</span><select class="f-geschoss">${geschosse.map((g) => `<option value="${esc(g.kennzeichen)}"${g.kennzeichen === vorGeschoss ? ' selected' : ''}>${esc(g.kennzeichen)}${g.name ? ' – ' + esc(g.name) : ''}</option>`).join('')}</select></label>
    ${art === 'pdf' ? '<label class="fz"><span>Seite</span><input type="text" class="f-seite" inputmode="numeric" value="1"></label>' : ''}
    ${art === 'pdf' || art === 'bild' ? '<label class="fz"><span>Massstab der Vorlage 1 :</span><input type="text" class="f-massstab" inputmode="decimal" placeholder="z. B. 100 (leer: Vorgabe des Kerns)"></label>' : ''}
    ${art === 'pdf' ? '<label class="fz"><span>Auflösung [dpi]</span><input type="text" class="f-dpi" inputmode="numeric" placeholder="leer: Vorgabe des Kerns"></label>' : ''}
    <p class="leise">${art === 'dwg' || art === 'dxf' ? 'Die Einheit liest der Kern aus der Zeichnung.' : 'Die erste Lage ergibt sich aus dem Massstab; genau ausgerichtet wird danach mit «Kalibrieren».'}</p>
    <div class="ifc-status"></div></div>`);
  const q = (s) => w.querySelector(s);
  const ok = await zeigen({
    titel: `${def.titel} – ${datei.name}`, inhalt: w, breite: 560,
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Einfügen', primaer: true, schliesst: false,
        pruefen: () => {
          const seiteText = q('.f-seite')?.value, massText = q('.f-massstab')?.value, dpiText = q('.f-dpi')?.value;
          return T.einfuegenPruefen({ art, geschoss: q('.f-geschoss').value, seite: Number(seiteText), seiteText, massstab: T.zahlAusFeld(massText), massstabText: massText, dpi: T.zahlAusFeld(dpiText), dpiText });
        },
        async klick(ctrl) {
          const st = q('.ifc-status');
          st.innerHTML = spinner('Wird hochgeladen …');
          const hoch = await hochladen(datei, (a, b) => { st.innerHTML = spinner(`Wird hochgeladen … ${mb(a)} von ${mb(b)} MB`); });
          st.innerHTML = spinner('Wird eingefügt …');
          const seiteText = q('.f-seite')?.value, massText = q('.f-massstab')?.value, dpiText = q('.f-dpi')?.value;
          const r = await sende('unterlage_einfuegen', T.einfuegenParameter({
            datei: hoch.name, geschoss: q('.f-geschoss').value, art, seite: seiteText?.trim() ? Number(seiteText) : null,
            massstab: massText?.trim() ? T.zahlAusFeld(massText) : null, dpi: dpiText?.trim() ? T.zahlAusFeld(dpiText) : null,
          }));
          st.innerHTML = '';
          if (!r.angenommen) { ctrl.fehler([r.fehler || r.meldung || 'Abgelehnt.', r.vorschlag ? `Vorschlag: ${r.vorschlag}` : ''].filter(Boolean).join(' – ')); return; }
          ergebnisMelden(r, `${T.ART_NAMEN[art]}-Unterlage eingefügt. Mit «Kalibrieren» genau ausrichten.`);
          const neu = (r.aenderungen ?? []).find((a) => a.klasse === 'Unterlage' && a.art === 'angelegt');
          if (neu?.kennzeichen) setTimeout(() => { const e = element(neu.kennzeichen); if (e) auswaehlen(e.id); }, 400);
          ctrl.schliessen(true);
        } }],
  });
  return ok;
}

// ---------------------------------------------------------------------------------------------- Lagebericht und Koordinaten

const unterlagenText = (u) => `${T.unterlageName(u)}${u.name ? ' (' + u.kennzeichen + ')' : ''}`;
const bezugProjekt = () => T.ifcBezugVon(alleElemente());

/** Dialog «Lagebericht» einer Unterlage: IFC mit der Tabelle aus «lage», andere Arten mit ihrer Transformation. */
async function lageberichtZeigen(p) {
  const u = element(p?.element ?? p?.unterlage ?? zustand.auswahl);
  if (!u || u.klasse !== 'Unterlage') { meldung('Zuerst eine Unterlage wählen.', 'info'); return; }
  const art = u.werte?.art;
  let h = '';
  if (art === 'ifc') {
    const lage = u.werte?.lage;
    h = lage ? T.lageHtml(lage, bezugProjekt()) : '<p class="leise">Für diese Unterlage ist kein Lagebericht gespeichert (sie wurde noch nicht übernommen).</p>';
    h += `<p class="leise">${esc([u.werte?.ifcSchema, u.werte?.ifcProgramm].filter(Boolean).join(', '))}${u.werte?.stand ? ` · Stand ${esc(u.werte.stand)}` : ''}</p>`;
  } else {
    const t = transformationVon(u);
    h = `<p class="dlg-text">Nur IFC-Dateien haben einen Lagebericht. Die Lage dieser ${esc(T.ART_NAMEN[art] ?? art)}-Unterlage im Modell:</p>${t
      ? `<table class="wtab"><tr><th>Faktor</th><td>${esc(T.zahl(t.faktor, 6))} (Modell-mm je Einheit der Unterlage)</td></tr><tr><th>Drehung</th><td>${esc(T.zahl(t.drehung, 4))}°</td></tr>`
        + `<tr><th>Verschiebung</th><td>x ${esc(T.zahl(t.dx, 1))} mm · y ${esc(T.zahl(t.dy, 1))} mm</td></tr></table>`
      : '<p class="leise">Die Unterlage hat noch keine Lage.</p>'}`;
  }
  const w = knoten(`<div class="lage-dlg"><p class="dlg-text"><b>${esc(unterlagenText(u))}</b> · ${esc(T.unterlageKurz(u))}</p>${h}</div>`);
  await zeigen({ titel: `Lagebericht – ${unterlagenText(u)}`, inhalt: w, breite: 820, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

/** Koordinaten-Übersicht (Register Verwalten): Projektbezug mit Quelle und alle IFC-Unterlagen mit Ausrichtung und Abweichung – nur lesend. */
async function koordinatenZeigen() {
  const bezug = bezugProjekt();
  const ifcs = T.unterlagenSortiert(derKlasse('Unterlage').filter((u) => u.werte?.art === 'ifc'));
  const w = knoten(`<div class="koord-dlg">
    <p class="dlg-text">Hier steht, wie die IFC-Dateien im Projekt ausgerichtet sind. Der erste IFC-Übertrag legt den Projektbezug fest, jede weitere Datei wird dagegen geprüft; nichts wird stillschweigend verschoben – weicht eine Datei ab, verlangt der Übertrag eine ausdrückliche Wahl.</p>
    <h3>Projektbezug</h3>
    <table class="wtab">${T.bezugZeilen(bezug).map(([k, v]) => `<tr><th>${esc(k)}</th><td>${esc(v)}</td></tr>`).join('')}</table>
    <h3>IFC-Unterlagen (${ifcs.length})</h3>
    ${ifcs.length === 0 ? '<p class="leise">Noch keine IFC-Datei übernommen.</p>' : `<table class="tab klickbar"><thead><tr><th>Unterlage</th><th>Fassung, Programm</th><th>Ausrichtung</th><th>Abweichung zum Projektbezug</th></tr></thead><tbody>${ifcs.map((u) => {
      const l = u.werte?.lage ?? {};
      const abw = l.abweichung;
      const ausr = l.ausrichtung ? (T.WAHL_TEXT[l.ausrichtung] ?? l.ausrichtung) : '–';
      const kurz = l.ausrichtung === 'einpassen' ? T.einpassenKurz(l.einpassen) : '';   // Zahlen des Kerns: Ergebnis, Drehung, Treffer
      return `<tr data-kz="${esc(u.kennzeichen)}" title="Klick: Lagebericht"><td>${esc(unterlagenText(u))}</td><td>${esc([u.werte?.ifcSchema, u.werte?.ifcProgramm].filter(Boolean).join(', ') || '–')}</td><td>${esc(ausr)}${kurz ? `<br><span class="leise">${esc(kurz)}</span>` : ''}</td>`
        + `<td class="${l.bewertung === 'abweichung' ? 'schwere-fehler' : ''}">${abw ? esc(abw.text ?? '') + '<br><span class="leise">' + esc(T.abweichungZahlen(abw)) + '</span>' : l.bewertung === 'erster' ? 'keine (setzt den Projektbezug)' : '–'}</td></tr>`;
    }).join('')}</tbody></table>`}
    <p class="leise">Zahlen in m, Winkel in Grad. Ein Klick auf eine Zeile zeigt den ganzen Lagebericht der Datei.</p></div>`);
  w.querySelectorAll('tr[data-kz]').forEach((z) => { z.onclick = () => lageberichtZeigen({ unterlage: z.dataset.kz }); });
  await zeigen({ titel: 'Koordinaten', inhalt: w, breite: 900, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

// ---------------------------------------------------------------------------------------------- Unterlagen verwalten

async function anzeigenUmschalten(p) {
  const u = element(p?.element ?? zustand.auswahl);
  if (!u || u.klasse !== 'Unterlage') return;
  const neu = u.werte?.anzeigen === false;
  const r = await sende('element_aendern', { element: u.kennzeichen, eigenschaften: { anzeigen: neu } });
  if (!r.angenommen) ergebnisMelden(r);
}

async function deckkraftDialog(p) {
  const u = element(p?.element ?? zustand.auswahl);
  if (!u || u.klasse !== 'Unterlage') return;
  const alt = Number(u.werte?.deckkraft ?? 0.5);
  const prozent = Math.round((Number.isFinite(alt) ? alt : 0.5) * 100);
  const w = knoten(`<div><label class="fz"><span>Deckkraft [%]</span><input type="range" class="d-regler" min="0" max="100" step="5" value="${prozent}"></label>
    <label class="fz"><span>Wert</span><input type="text" class="d-wert" inputmode="numeric" value="${prozent}"></label>
    <p class="leise">0 % unsichtbar, 100 % voll deckend. Die Unterlage liegt als Hintergrund unter den Elementen.</p></div>`);
  const regler = w.querySelector('.d-regler'), feld = w.querySelector('.d-wert');
  regler.oninput = () => { feld.value = regler.value; };
  feld.oninput = () => { if (feld.value.trim() !== '' && Number.isFinite(Number(feld.value))) regler.value = feld.value; };
  const ok = await zeigen({
    titel: `Deckkraft – ${unterlagenText(u)}`, inhalt: w, breite: 420,
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Übernehmen', wert: true, primaer: true, pruefen: () => { const n = Number(feld.value.replace(',', '.')); return Number.isFinite(n) && n >= 0 && n <= 100 ? true : 'Bitte eine Zahl von 0 bis 100 eingeben.'; } }],
  });
  if (ok !== true) return;
  const wert = Math.round(Number(feld.value.replace(',', '.'))) / 100;
  const r = await sende('element_aendern', { element: u.kennzeichen, eigenschaften: { deckkraft: wert } });
  if (!r.angenommen) ergebnisMelden(r);
}

/** N12: Farben einer IFC-Unterlage – Original (Farbe, Deckkraft) und Nachbau (eigene Farbe und Deckkraft oder normale Darstellung). Welche der beiden
 *  eine Ansicht zeigt, steht in der Ansicht (Sichtbarkeit/Grafiken → IFC-Unterlagen zeigen). */
async function farbenDialog(p) {
  const u = element(p?.element ?? p?.unterlage ?? zustand.auswahl);
  if (!u || u.klasse !== 'Unterlage' || u.werte?.art !== 'ifc') return;
  const o = originalStil(u);
  const nf = gueltigeFarbe(u.werte?.farbeNachbau), nd = gueltigeDeckkraft(u.werte?.deckkraftNachbau);
  const pz = (d) => Math.round(d * 100);
  const w = knoten(`<div class="ul-farben">
    <fieldset><legend>Original (die IFC-Datei selbst)</legend>
      <label class="fz"><span>Farbe</span><input type="color" class="f-orig" value="${o.farbe}"></label>
      <label class="fz"><span>Deckkraft [%]</span><input type="range" class="f-orig-d" min="0" max="100" step="5" value="${pz(o.deckkraft)}"><output class="f-orig-w">${pz(o.deckkraft)}</output></label></fieldset>
    <fieldset><legend>Nachbau (die aus der Datei übernommenen Elemente)</legend>
      <label class="haken"><input type="checkbox" class="f-nach-an" ${nf || nd !== null ? 'checked' : ''}> Eigene Farbe und Deckkraft (sonst normale Darstellung)</label>
      <label class="fz"><span>Farbe</span><input type="color" class="f-nach" value="${nf ?? '#ff8000'}"></label>
      <label class="fz"><span>Deckkraft [%]</span><input type="range" class="f-nach-d" min="0" max="100" step="5" value="${pz(nd ?? 0.6)}"><output class="f-nach-w">${pz(nd ?? 0.6)}</output></label></fieldset>
    <p class="leise">0 % unsichtbar, 100 % voll deckend. In «Beide übereinander» lassen sich Original und Nachbau so unterscheiden.</p></div>`);
  const q = (k) => w.querySelector(k);
  q('.f-orig-d').oninput = (e) => { q('.f-orig-w').textContent = e.target.value; };
  q('.f-nach-d').oninput = (e) => { q('.f-nach-w').textContent = e.target.value; };
  const ok = await zeigen({ titel: `Farben – ${unterlagenText(u)}`, inhalt: w, breite: 460, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Übernehmen', wert: true, primaer: true }] });
  if (ok !== true) return;
  const orig = q('.f-orig').value.toLowerCase(), an = q('.f-nach-an').checked;
  const r = await sende('element_aendern', { element: u.kennzeichen, eigenschaften: {
    farbeOriginal: orig === ORIGINAL_FARBE ? null : orig,
    deckkraft: Number(q('.f-orig-d').value) / 100,
    farbeNachbau: an ? q('.f-nach').value.toLowerCase() : null,
    deckkraftNachbau: an ? Number(q('.f-nach-d').value) / 100 : null,
  } });
  if (!r.angenommen) ergebnisMelden(r);
}

/** Dialog «Unterlagen verwalten»: alle Unterlagen mit Anzeige, Deckkraft, Lage, Kalibrieren und Entfernen (live nachgeführt). */
async function unterlagenVerwalten() {
  const w = knoten('<div class="ul-dlg"></div>');
  const zeichnen = () => {
    const liste = T.unterlagenSortiert(derKlasse('Unterlage'));
    w.innerHTML = liste.length === 0
      ? '<p class="leise">Noch keine Unterlagen. Im Register Einfügen: IFC übernehmen, CAD, PDF oder Bild einfügen.</p>'
      : `<table class="tab"><thead><tr><th>Unterlage</th><th>Art</th><th>Geschoss</th><th>Datei</th><th>Anzeigen</th><th>Deckkraft</th><th></th></tr></thead><tbody>${liste.map((u) => {
        const art = u.werte?.art;
        return `<tr data-kz="${esc(u.kennzeichen)}"><td>${esc(unterlagenText(u))}</td><td>${esc(T.ART_NAMEN[art] ?? art ?? '')}</td><td>${esc(u.geschoss ?? '–')}</td>`
          + `<td class="leise" title="${esc(u.werte?.datei ?? '')}">${esc(ohnePruefsumme(u.werte?.datei ?? u.werte?.original ?? ''))}</td>`
          + `<td><input type="checkbox" class="u-an" ${u.werte?.anzeigen === false ? '' : 'checked'} aria-label="anzeigen"></td>`
          + `<td><input type="text" class="u-deck" size="3" inputmode="numeric" value="${Math.round(Number(u.werte?.deckkraft ?? 0.5) * 100)}"> %</td>`
          + `<td class="u-knoepfe"><button type="button" class="dlg-knopf u-lage">Lage</button>${art === 'ifc' ? '<button type="button" class="dlg-knopf u-farben">Farben …</button>' : ''}${KALIB(art) ? '<button type="button" class="dlg-knopf u-kal">Kalibrieren</button>' : ''}<button type="button" class="dlg-knopf u-weg">Entfernen</button></td></tr>`;
      }).join('')}</tbody></table>`;
  };
  const KALIB = (art) => ['dwg', 'dxf', 'pdf', 'bild'].includes(art);
  zeichnen();
  const spaeter = entprellen(() => { if (w.isConnected) zeichnen(); }, 120);
  const abmelden = abonnieren('modell', spaeter);
  const zeile = (e) => element(e.target.closest('tr[data-kz]')?.dataset.kz);
  w.addEventListener('change', async (e) => {
    const u = zeile(e);
    if (!u) return;
    if (e.target.classList.contains('u-an')) await sende('element_aendern', { element: u.kennzeichen, eigenschaften: { anzeigen: e.target.checked } });
    else if (e.target.classList.contains('u-deck')) {
      const n = Number(e.target.value.replace(',', '.'));
      if (!Number.isFinite(n) || n < 0 || n > 100) { meldung('Die Deckkraft muss zwischen 0 und 100 % liegen.', 'warnung'); zeichnen(); return; }
      await sende('element_aendern', { element: u.kennzeichen, eigenschaften: { deckkraft: Math.round(n) / 100 } });
    }
  });
  w.addEventListener('click', (e) => {
    const u = zeile(e);
    if (!u) return;
    if (e.target.closest('.u-lage')) lageberichtZeigen({ unterlage: u.kennzeichen });
    else if (e.target.closest('.u-farben')) farbenDialog({ element: u.kennzeichen });
    else if (e.target.closest('.u-kal')) { zu?.(); ausfuehren('einfuegen.kalibrieren', { unterlage: u.kennzeichen }); }
    else if (e.target.closest('.u-weg')) ausfuehren('aendern.loeschen', { element: u.kennzeichen });
    else { auswaehlen(u.id); }
  });
  let zu = null;
  const dlg = zeigen({ titel: 'Unterlagen verwalten', inhalt: (ctrl) => { zu = () => ctrl.schliessen(true); return w; }, breite: 920, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
  await dlg;
  abmelden();
}

// ---------------------------------------------------------------------------------------------- Aktionen (Register Einfügen)

const kalibrierenMoeglich = () => !!planAnsicht() && planAnsicht().art === 'grundriss' && kalibrierbareUnterlagen().length > 0;

registrieren({
  id: 'einfuegen.ifc', titel: 'IFC über­nehmen', symbol: 'ifcUebernehmen', kuerzel: null, register: 'einfuegen', gruppe: 'Verknüpfen / Übernehmen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: ifcUebernehmen,
});
registrieren({
  id: 'einfuegen.cad', titel: 'CAD einfügen', symbol: 'cadEinfuegen', kuerzel: null, register: 'einfuegen', gruppe: 'Importieren',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: (p) => unterlageEinfuegen('cad', p),
});
registrieren({
  id: 'einfuegen.pdf', titel: 'PDF einfügen', symbol: 'pdfEinfuegen', kuerzel: null, register: 'einfuegen', gruppe: 'Importieren',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: (p) => unterlageEinfuegen('pdf', p),
});
registrieren({
  id: 'einfuegen.bild', titel: 'Bild einfügen', symbol: 'bildEinfuegen', kuerzel: null, register: 'einfuegen', gruppe: 'Importieren',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: (p) => unterlageEinfuegen('bild', p),
});
registrieren({
  id: 'einfuegen.kalibrieren', titel: 'Kalibrieren', symbol: 'kalibrieren', kuerzel: null, register: 'einfuegen', gruppe: 'Unterlagen',
  aktiv: kalibrierenMoeglich,
  hinweis: () => (!planAnsicht() || planAnsicht().art !== 'grundriss' ? 'Zuerst einen Grundriss öffnen und aktivieren.' : 'Zuerst eine DWG-, DXF-, PDF- oder Bild-Unterlage einfügen.'),
  gedrueckt: () => werkzeugAktiv() === 'kalibrieren',
  ausfuehren(p) { werkzeugStarten(kalibrierenWerkzeug({ unterlage: p?.unterlage })); },
});
registrieren({
  id: 'einfuegen.verwalten', titel: 'Unter­lagen verwalten', symbol: 'unterlagen', kuerzel: null, register: 'einfuegen', gruppe: 'Unterlagen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: unterlagenVerwalten,
});

// Register Verwalten: Koordinaten-Übersicht (lesend)
registrieren({
  id: 'verwalten.koordinaten', titel: 'Koordinaten', symbol: 'koordinaten', kuerzel: null, register: 'verwalten', gruppe: 'Projekt',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: koordinatenZeigen,
});

// Kontextaktionen der Unterlagen im Projektbrowser (nicht im Menüband)
registrieren({ id: 'unterlage.anzeigen', titel: 'Anzeigen / Ausblenden', symbol: 'unterlagen', ausfuehren: anzeigenUmschalten });
registrieren({ id: 'unterlage.deckkraft', titel: 'Deckkraft …', symbol: 'unterlagen', ausfuehren: deckkraftDialog });
registrieren({ id: 'unterlage.farben', titel: 'Farben …', symbol: 'unterlagen', ausfuehren: farbenDialog });
registrieren({ id: 'unterlage.lagebericht', titel: 'Lagebericht zeigen', symbol: 'koordinaten', ausfuehren: lageberichtZeigen });

export { ifcDialog, ergebnisDialog, lageberichtZeigen, koordinatenZeigen, unterlagenVerwalten, unterlageEinfuegen };
