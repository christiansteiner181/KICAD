// Dialog «Gesten» (Modul G, entwurf/modul-gesten.md), reiner Teil: baut den HTML-Text der Abschnitte aus Daten – ohne DOM, in Node getestet.
// Die Verdrahtung (Schalter, Auswahlfelder, Testfeld, Aufnahme) steht in gesten-dialog.js. Aller Text aus Daten wird maskiert (Kameranamen, Titel, Erklärungen).
//
// Abschnitte: Kopf (Grundschalter, Zustand, Datenschutz) · Liste der Gesten (Nr, Geste, Wirkung, Art, wie die Tastaturkürzel) · Einstellungen (Gestenhand, Kamera,
// Handskelett, Belegung G2/G6, «Auf Vorgabe zurück») · Empfindlichkeit (je Stellschraube, generisch aus STELLSCHRAUBEN_VORGABE) · Testfeld · Punktfolge aufnehmen.

import * as T from './gesten-text.js';

export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const ohneWeich = (t) => String(t ?? '').replace(/[​­]/g, '');
const wahl = (wert, aktuell) => (wert === aktuell ? ' selected' : '');

/** Kopf: Grundschalter, Zustand in Worten, Datenschutz-Satz. */
export function kopfHtml(z = {}) {
  const a = T.statusAnzeige(z);
  return `<section class="gd-kopf">
    <label class="gd-schalter"><input type="checkbox" class="gd-an" aria-label="Gesten einschalten"${z.an ? ' checked' : ''}> Gesten einschalten</label>
    <span class="gd-status ${esc(a.stufe)}" role="status">${esc(a.text)}</span>
    <p class="leise gd-klein gd-fehler"${z.fehler && !z.an ? '' : ' hidden'}>${esc(z.fehler ?? '')}</p>
    <p class="leise gd-klein">${esc(T.DATENSCHUTZ)}</p>
    <p class="leise gd-klein">${esc(T.HINWEIS_NEBEN_DER_MAUS)}</p>
  </section>`;
}

/** Tabelle der Gesten wie die Liste der Tastaturkürzel: Nr, Geste, Wirkung, Art. */
export function listeHtml(zeilen = []) {
  return `<table class="tab gd-liste"><thead><tr><th>Nr</th><th>Geste</th><th>Wirkung</th><th>Art</th></tr></thead><tbody>${zeilen.map((z) =>
    `<tr><td><kbd>${esc(z.nr)}</kbd></td><td>${esc(z.name)}</td><td>${esc(z.wirkung)}</td><td class="leise">${esc(z.art)}</td></tr>`).join('')}</tbody></table>`;
}

/** Auswahl einer Aktion für die Belegung: «keine Wirkung» und die belegbaren Aktionen [{ id, titel }]. */
function aktionsAuswahl(nr, richtung, belegbar, aktuell, beschriftung) {
  const opt = [{ id: '', titel: 'keine Wirkung' }, ...belegbar].map((b) => `<option value="${esc(b.id)}"${wahl(b.id, aktuell)}>${esc(ohneWeich(b.titel))}</option>`).join('');
  return `<label class="gd-zeile"><span class="gd-etikett">${esc(beschriftung)}</span><select class="gd-beleg" data-nr="${esc(nr)}" data-richtung="${esc(richtung ?? '')}" aria-label="${esc(beschriftung)}">${opt}</select></label>`;
}

/**
 * Einstellungen: Gestenhand, Kamera (kameras = [{ id, name }]), Handskelett, Belegung G2 (je Richtung) und G6, «Auf Vorgabe zurück».
 * belegbar = [{ id, titel }] der Aktionen, die Gesten auslösen dürfen (aus dem Aktionsregister, Liste BELEGBAR).
 */
export function einstellungHtml(einst, kameras = [], belegbar = []) {
  const e = T.einstellungNormal(einst);
  const bekannt = kameras.some((k) => k.id === e.kameraId);
  const kamOpt = [`<option value=""${wahl('', e.kameraId ?? '')}>Standardkamera (eingebaute Kamera)</option>`,
    ...kameras.map((k) => `<option value="${esc(k.id)}"${wahl(k.id, e.kameraId)}>${esc(k.name || k.id)}</option>`),
    ...(e.kameraId && !bekannt ? [`<option value="${esc(e.kameraId)}" selected>Gewählte Kamera (nicht gefunden)</option>`] : [])].join('');
  return `<section class="gd-einst"><b>Einstellungen</b>
    <label class="gd-zeile"><span class="gd-etikett">Gestenhand</span><select class="gd-hand" aria-label="Gestenhand"><option value="links"${wahl('links', e.hand)}>links (die rechte bleibt an der Maus)</option><option value="rechts"${wahl('rechts', e.hand)}>rechts</option></select></label>
    <label class="gd-zeile"><span class="gd-etikett">Kamera</span><select class="gd-kamera" aria-label="Kamera">${kamOpt}</select></label>
    <label class="gd-zeile gd-haken"><input type="checkbox" class="gd-skelett" aria-label="Handskelett zeigen"${e.skelett ? ' checked' : ''}> Handskelett unten rechts zeigen (ohne Kamerabild)</label>
    <div class="gd-belegung"><b class="gd-klein">Belegung</b>
      ${aktionsAuswahl('G2', 'rechts', belegbar, e.belegung.G2.rechts, 'G2 Wisch nach rechts')}
      ${aktionsAuswahl('G2', 'links', belegbar, e.belegung.G2.links, 'G2 Wisch nach links')}
      ${aktionsAuswahl('G6', '', belegbar, e.belegung.G6, 'G6 Pistolengeste')}
      <p class="leise gd-klein">In Dialogen und Listen mit Seiten blättert G2 die Seiten, G3 rollt eine Bildschirmseite. Belegen lassen sich nur Aktionen für Ansicht und Navigation.</p>
    </div>
    <div class="gd-zeile"><button type="button" class="dlg-knopf gd-vorgabe" title="Setzt Gestenhand, Kamera, Handskelett, Belegung und Empfindlichkeit auf die Vorgabe zurück; der Grundschalter bleibt">Auf Vorgabe zurück</button></div>
  </section>`;
}

/** Eine Zeile der Empfindlichkeit (zeile aus T.stellschraubenZeilen). */
export function schraubeHtml(z) {
  const attr = [z.min !== null ? ` min="${esc(z.min)}"` : '', z.max !== null ? ` max="${esc(z.max)}"` : '', ` step="${esc(z.schritt ?? 'any')}"`].join('');
  const bereich = z.min !== null && z.max !== null ? `Bereich ${T.wertText(z.min, z.einheit)} bis ${T.wertText(z.max, z.einheit)}` : '';
  return `<tr class="gd-schraube-zeile${z.geaendert ? ' geaendert' : ''}" data-name="${esc(z.name)}">
    <td><b>${esc(z.titel)}</b><div class="leise gd-klein">${esc(z.erklaerung)}</div></td>
    <td class="gd-wert"><input type="number" class="gd-schraube" data-name="${esc(z.name)}" value="${esc(z.wert)}"${attr} data-kein-enter="1" aria-label="${esc(z.titel)}">${z.einheit ? ` <span class="leise">${esc(z.einheit)}</span>` : ''}</td>
    <td class="leise gd-klein">${esc(bereich)}<br>Vorgabe ${esc(T.wertText(z.vorgabe, z.einheit))}</td>
    <td><button type="button" class="dlg-knopf gd-schraube-vorgabe" data-name="${esc(z.name)}"${z.geaendert ? '' : ' disabled'}>Vorgabe</button></td>
  </tr>`;
}

/** Empfindlichkeit: je Stellschraube eine Zeile; ohne Stellschrauben (Gestenlogik fehlt) ein Hinweis. */
export function empfindlichkeitHtml(zeilen = null) {
  if (!zeilen || zeilen.length === 0) return `<section class="gd-empfindlichkeit"><b>Empfindlichkeit</b><p class="leise">${esc(T.HINWEIS_STELLSCHRAUBEN_FEHLT)}</p></section>`;
  return `<section class="gd-empfindlichkeit"><b>Empfindlichkeit</b><p class="leise gd-klein">${esc(T.HINWEIS_STELLSCHRAUBEN)}</p>
    <table class="tab gd-schrauben"><thead><tr><th>Stellschraube</th><th>Wert</th><th>Bereich</th><th></th></tr></thead><tbody>${zeilen.map(schraubeHtml).join('')}</tbody></table></section>`;
}

/** Testfeld: die letzten erkannten Gesten (Zeilen aus T.ereignisZeile), neueste oben. laeuft = die Gesten sind an. */
export function testfeldHtml(protokoll = [], laeuft = false) {
  const inhalt = protokoll.length ? `<ul class="gd-test-liste">${protokoll.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : `<p class="leise gd-test-leer">${esc(laeuft ? T.TESTFELD_LEER : T.TESTFELD_AUS)}</p>`;
  return `<section class="gd-test"><b>Testfeld</b><div class="gd-test-platz" aria-live="polite">${inhalt}</div></section>`;
}

/** Punktfolge aufnehmen: Knöpfe und Hinweis; aufnahme = läuft schon, laeuft = die Gesten sind an. */
export function aufnahmeHtml({ laeuft = false, aufnahme = false } = {}) {
  return `<section class="gd-aufnahme"><b>Punktfolge aufnehmen</b><p class="leise gd-klein">${esc(T.AUFNAHME_HINWEIS)}</p>
    <div class="gd-zeile"><button type="button" class="dlg-knopf gd-aufnahme-start"${laeuft && !aufnahme ? '' : ' disabled'}>Aufnahme starten</button>
    <button type="button" class="dlg-knopf gd-aufnahme-ende"${aufnahme ? '' : ' disabled'}>Aufnahme beenden und speichern …</button></div></section>`;
}

/** Der ganze Dialog. d = { zustand, einst, zeilen, kameras, belegbar, schrauben, protokoll }. */
export function dialogHtml(d = {}) {
  const z = d.zustand ?? {};
  return `<div class="gd">${kopfHtml(z)}
    <section class="gd-gesten"><b>Gesten</b><div class="gd-liste-platz">${listeHtml(d.zeilen ?? [])}</div></section>
    ${einstellungHtml(d.einst, d.kameras ?? [], d.belegbar ?? [])}
    <div class="gd-schrauben-platz">${empfindlichkeitHtml(d.schrauben ?? null)}</div>
    <div class="gd-test-bereich">${testfeldHtml(d.protokoll ?? [], !!z.laeuft)}</div>
    <div class="gd-aufnahme-bereich">${aufnahmeHtml({ laeuft: !!z.laeuft, aufnahme: !!z.aufnahme })}</div>
  </div>`;
}
