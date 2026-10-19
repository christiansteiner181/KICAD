// Dialog «Projektinformationen» (Verwalten › Projekt › Projektinformationen, Doppelklick auf «Projektinformation» im Projektbrowser): zeigt und ändert die Angaben
// der Klasse Projektinformation (Nachbearbeitung N3, Punkt 2) – Objektzeilen, Projektnummer, Phase, Rohrbezeichnung (NW/DN), Masseinheit je Gewerk, Nordwinkel.
// Darunter ausklappbar der Überblick über das Projekt (wie bisher). Keine Fachlogik: die Felder kommen aus der Katalogdefinition, geändert wird mit element_aendern bzw.
// (gibt es noch kein Element) element_anlegen; Rückgängig, Herkunft und Prüfungen sind die des Kerns. Texte und Parameter: projektinfo-text.js.

import { zustand, derKlasse } from '../zustand.js';
import { abfrage } from '../api.js';
import { zeigen, meldung } from './dialoge.js';
import { sende } from './befehle.js';
import { klassenDefinition } from './katalogansicht.js';
import { esc, knoten, gewerkeListe } from './werkzeug.js';
import * as T from './projektinfo-text.js';

/** Zeilen der Überblickstabelle (Abfrage projekt_ueberblick und Status). */
async function ueberblickHtml() {
  let u = null;
  try { u = await abfrage('projekt_ueberblick'); } catch (e) { console.error(e); }
  const s = zustand.status ?? {};
  const zeile = (k, w) => `<tr><th>${esc(k)}</th><td>${esc(w ?? '–')}</td></tr>`;
  const stand = u?.modellStand ?? s.stand ?? {};
  const gebaeude = (u?.gebaeude ?? []).map((g) => `<b>${esc(g.kennzeichen)}</b> ${esc(g.name ?? '')}: ${(g.geschosse ?? []).map((x) => `${esc(x.kennzeichen)} (${esc(x.raeume)} Räume)`).join(', ')}`).join('<br>');
  const systeme = (u?.systeme ?? []).map((x) => `<b>${esc(x.kennzeichen)}</b> ${esc(x.name ?? '')}: ${esc((x.straenge ?? []).length)} Stränge, ${esc(x.bauteile)} Bauteile`).join('<br>');
  const befunde = u?.befunde ? `${u.befunde.fehler} Fehler, ${u.befunde.warnung} Warnungen, ${u.befunde.hinweis} Hinweise` : '–';
  const klassen = u?.elementeJeKlasse ? Object.entries(u.elementeJeKlasse).map(([k, n]) => `${esc(k)} ${esc(n)}`).join(' · ') : '–';
  return `<table class="wtab">
    ${zeile('Projekt', s.projekt ?? u?.projekt)}${zeile('Ablage', s.ort)}${zeile('Akteur', s.akteur ? `${s.akteur}@${s.geraet}` : null)}
    ${zeile('Stand', `${stand.pakete ?? '?'} Pakete · ${stand.elemente ?? '?'} Elemente · ${stand.verbindungen ?? '?'} Verbindungen`)}
    ${zeile('Prüfsumme', stand.pruefsumme)}${zeile('Prüfbefunde', befunde)}
    <tr><th>Gebäude</th><td>${gebaeude || '–'}</td></tr><tr><th>Systeme</th><td>${systeme || '–'}</td></tr>
    ${zeile('Ansichten', (u?.ansichten ?? []).join(', '))}
    <tr><th>Elemente je Klasse</th><td class="klein">${klassen}</td></tr></table>`;
}

/** Zeile eines bearbeitbaren Felds. */
function feldHtml(f, wert, einheiten) {
  const titel = esc(f.beschreibung);
  const marke = `data-feld="${esc(f.name)}"`;
  if (f.art === 'auswahl') {
    const optionen = f.auswahl ?? [];
    const liste = wert && !optionen.includes(wert) ? [...optionen, wert] : optionen;
    return `<label class="fz" title="${titel}"><span>${esc(f.titel)}</span><select ${marke}><option value="">– nicht gesetzt –</option>${liste.map((o) => `<option value="${esc(o)}"${o === wert ? ' selected' : ''}>${esc(o)}</option>`).join('')}</select></label>`;
  }
  if (f.art === 'zahl') {
    const text = wert === null || wert === undefined ? '' : String(wert).replace('.', ',');
    return `<label class="fz" title="${titel}"><span>${esc(f.titel)}</span><span class="pi-zahl"><input type="text" inputmode="decimal" ${marke} value="${esc(text)}">${f.einheit ? `<span class="pi-einheit-text">${esc(f.einheit)}</span>` : ''}</span></label>`;
  }
  if (f.art === 'einheiten') {
    const aktuell = wert && typeof wert === 'object' ? wert : {};
    const gewerke = gewerkeListe();
    const bekannt = new Set(gewerke.map(([k]) => k));
    const alle = [...gewerke, ...Object.keys(aktuell).filter((k) => !bekannt.has(k)).map((k) => [k, k])];
    return `<div class="pi-einheiten" data-feld="${esc(f.name)}" title="${titel}"><div class="pi-einheiten-kopf">${esc(f.titel)}</div>
      <div class="pi-einheiten-raster">${alle.map(([k, n]) => `<label class="pi-einheit"><span>${esc(n)}</span><select data-gewerk="${esc(k)}"><option value="">Vorgabe</option>${einheiten.map((e) => `<option value="${esc(e)}"${aktuell[k] === e ? ' selected' : ''}>${esc(e)}</option>`).join('')}${aktuell[k] && !einheiten.includes(aktuell[k]) ? `<option value="${esc(aktuell[k])}" selected>${esc(aktuell[k])}</option>` : ''}</select></label>`).join('')}</div>
      <p class="leise pi-hinweis">${esc(f.beschreibung)}</p></div>`;
  }
  return `<label class="fz" title="${titel}"><span>${esc(f.titel)}</span><input type="text" ${marke} value="${esc(wert ?? '')}"></label>`;
}

/** Die Eingabe aus dem Formular: { feldname: Rohwert } (bei den Masseinheiten die Tabelle gewerk → einheit). */
function eingabeLesen(w, plan) {
  const e = {};
  for (const f of T.alleFelder(plan)) {
    if (f.art === 'lesen') continue;
    const el = w.querySelector(`[data-feld="${f.name}"]`);
    if (!el) continue;
    if (f.art === 'einheiten') e[f.name] = Object.fromEntries([...el.querySelectorAll('select[data-gewerk]')].map((s) => [s.dataset.gewerk, s.value]));
    else e[f.name] = el.value;
  }
  return e;
}

export async function projektInfoDialog() {
  let def = null, einheiten = [];
  try { def = await klassenDefinition(T.KLASSE); } catch (e) { console.error(e); }
  try { einheiten = (await klassenDefinition('Ansicht')).eigenschaften?.find((x) => x.name === 'bemassungEinheit')?.auswahl ?? []; } catch (e) { console.error(e); }
  if (!def) { meldung('Die Klasse Projektinformation ist im Katalog nicht zu lesen.', 'warnung'); return false; }
  const plan = T.feldPlan(def);
  const info = T.projektinfoVon(derKlasse(T.KLASSE));
  const werte = info?.werte ?? {};

  const gruppenHtml = plan.map((g) => `<fieldset class="pi-gruppe"><legend>${esc(g.titel)}</legend>${g.felder.map((f) => {
    if (f.art === 'lesen') return `<div class="fz pi-lesen" title="${esc(f.beschreibung)}"><span>${esc(f.titel)}</span><span class="pi-lesen-text">${werte[f.name] === undefined || werte[f.name] === null ? '<span class="leise">nicht gesetzt</span>' : esc(T.lesendText(werte[f.name]))}${f.hinweis ? `<span class="leise pi-hinweis"> – ${esc(f.hinweis)}</span>` : ''}</span></div>`;
    return feldHtml(f, werte[f.name], einheiten);
  }).join('')}</fieldset>`).join('');
  const w = knoten(`<div class="pi">
    <p class="leise pi-kopf">${info ? `Element ${esc(info.kennzeichen)}. ` : 'Das Projekt hat noch keine Angaben; beim Übernehmen wird das Element angelegt. '}Jede Änderung ist ein Befehl (Rückgängig: Strg+Z).</p>
    ${gruppenHtml}
    <details class="pi-ueberblick"><summary>Überblick über das Projekt</summary><div class="pi-ueberblick-inhalt leise">Wird geladen …</div></details>
  </div>`);
  w.querySelector('.pi-ueberblick').addEventListener('toggle', async (e) => {
    const d = e.target;
    if (!d.open || d.dataset.geladen) return;
    d.dataset.geladen = '1';
    w.querySelector('.pi-ueberblick-inhalt').innerHTML = await ueberblickHtml();
    w.querySelector('.pi-ueberblick-inhalt').classList.remove('leise');
  });

  const ok = await zeigen({
    titel: 'Projektinformationen', inhalt: w, breite: 700, klasse: 'dlg-hoch', art: 'projektinfo',
    knoepfe: [
      { text: 'Schliessen', wert: false },
      {
        text: 'Übernehmen', wert: true, primaer: true,
        pruefen: () => { try { T.aenderungen(plan, werte, eingabeLesen(w, plan)); return true; } catch (e) { return e.message; } },
        async klick() {
          const aus = T.aenderungen(plan, werte, eingabeLesen(w, plan));
          const p = info ? T.aendernParameter(info.kennzeichen, aus) : T.anlegenParameter(aus);
          if (!p) return;   // nichts geändert
          const r = await sende(info ? 'element_aendern' : 'element_anlegen', p);
          if (!r.angenommen) { const f = new Error(r.fehler || r.meldung || 'Die Änderung wurde abgelehnt.'); f.vorschlag = r.vorschlag; throw f; }
          meldung('Projektinformationen gespeichert.', 'ok');
        },
      },
    ],
  });
  return ok === true;
}
