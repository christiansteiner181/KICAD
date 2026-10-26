// Statusleiste: links Auswahl bzw. Hinweis zur aktiven Aktion, Mitte Abgleich mit der Projektablage und Reservierungen,
// rechts Akteur@Gerät und Modellstand. Modul 2, C3: dazu «Bibliothek erreichbar/offline · n in Warteschlange» (Klick: Pfad, Quelle, nachgeholt). Meldet Änderungen anderer Arbeitsplätze und der KI kurz («Änderung von KI: 3 Elemente»).
// Der Server hat keinen Ereignisstrom für den Abgleichszeitpunkt und die Reservierungen; die Leiste holt /api/status
// deshalb alle paar Sekunden und nach jeder Modelländerung.

import { zustand, abonnieren, element, auswaehlen, melden as zustandMelden } from '../zustand.js';
import { holen } from '../api.js';
import { hinweis, beiHinweis } from './aktionen.js';
import { reservierungenFreigeben } from './aktionen-projekt.js';
import { zeigen, meldung } from './dialoge.js';
import { esc, knoten, elementText, anzeigeName, entprellen } from './werkzeug.js';
import { bibliothekInfo, beiBibliothek, bibliothekLaden } from '../familien/bibliothek-daten.js';   // Familienbibliothek (Modul 2, C3)
import { bibliothekStatus } from '../familien/bibliothek-text.js';
import { bibliothekHinweis } from '../familien/bibliothek-dialoge.js';
import { questStatusEinrichten } from './quest-status.js';
import { xrDesktopStatusEinrichten } from './xr-desktop-status.js';
import { gestenEinrichten } from '../gesten/gesten-oberflaeche.js';   // Symbol «Gesten» (Modul G): vier Zustände, Klick schaltet, Rechtsklick öffnet den Dialog

const TAKT_MS = 3000;
const lc = (t) => String(t ?? '').toLowerCase();
let teile = null;
let verbindungOk = true;     // Ereignisstrom
let serverWeg = false;       // Abfrage von /api/status gescheitert
let abgleichFehler = null;   // letzte Meldung des Abgleichs
let fremdText = '';
let fremdTimer = null;

export function einrichten(w) {
  w.innerHTML = `<span class="sl-links"></span>
    <span class="sl-mitte"><span class="sl-abgleich"></span><button type="button" class="sl-res" title="Reservierungen anzeigen"></button><button type="button" class="sl-bib" hidden></button><button type="button" class="sl-brille" hidden></button><button type="button" class="sl-gesten aus"></button><span class="sl-desktop" hidden></span></span>
    <span class="sl-rechts"><span class="sl-akteur"></span><span class="sl-stand"></span></span>`;
  teile = {
    links: w.querySelector('.sl-links'), abgleich: w.querySelector('.sl-abgleich'), res: w.querySelector('.sl-res'),
    akteur: w.querySelector('.sl-akteur'), stand: w.querySelector('.sl-stand'), bib: w.querySelector('.sl-bib'),
  };
  teile.res.onclick = reservierungenDialog;
  teile.bib.onclick = () => bibliothekHinweis();
  questStatusEinrichten(w.querySelector('.sl-brille'));   // «Brille» (Modul XR): nur sichtbar bei eingeschalteter Automatik oder gemerkter Brille
  gestenEinrichten(w.querySelector('.sl-gesten'));   // Gesten (Modul G): immer sichtbar, damit man sie einschalten kann; die Kamera öffnet sich erst dann
  xrDesktopStatusEinrichten(w.querySelector('.sl-desktop'));   // V1c: «Brille sieht den Bildschirm» mit «Beenden», nur solange eine Brille verbunden ist

  const zeichnenSpaeter = entprellen(zeichnen, 30);
  const statusSpaeter = entprellen(statusHolen, 500);
  abonnieren('auswahl', zeichnenSpaeter);
  abonnieren('ansicht', zeichnenSpaeter);
  abonnieren('status', zeichnenSpaeter);
  beiHinweis(zeichnenSpaeter);
  beiBibliothek(zeichnenSpaeter);
  bibliothekLaden();
  abonnieren('verbindung', (d) => { verbindungOk = d?.ok !== false; zeichnen(); if (verbindungOk) statusSpaeter(); });
  abonnieren('abgleich', (d) => { abgleichFehler = d?.fehler ?? null; zeichnen(); });
  abonnieren('konflikt', (d) => {
    const k = (d?.konflikte ?? []).map((x) => (typeof x === 'string' ? x : x?.meldung ?? x?.element ?? JSON.stringify(x))).join('; ');
    meldung('Konflikt beim Abgleich' + (k ? ': ' + k : '.'), 'warnung');
  });
  abonnieren('modell', (d) => {
    if (d.quelle === 'fremd') fremdeAenderung(d);
    zeichnenSpaeter();
    if (!d.alles) statusSpaeter();
  });
  setInterval(statusHolen, TAKT_MS);
  zeichnen();
}

function fremdeAenderung(d) {
  const n = (d.ids ?? []).length + (d.geloescht ?? []).length;
  const akteur = d.akteur ?? 'unbekannt';
  const art = d.akteurArt && d.akteurArt !== 'mensch' && lc(d.akteurArt) !== lc(akteur) ? ` (${d.akteurArt})` : '';
  const text = `Änderung von ${akteur}${art}: ${n} Element${n === 1 ? '' : 'e'}`;
  meldung(text, 'info');
  fremdText = text;
  clearTimeout(fremdTimer);
  fremdTimer = setTimeout(() => { fremdText = ''; zeichnen(); }, 8000);
  teile?.links.classList.remove('fremd-blinken');
  void teile?.links.offsetWidth;
  teile?.links.classList.add('fremd-blinken');
}

/** Holt /api/status; meldet 'status' nur, wenn sich Reservierungen, Projekt oder zuletzt geöffnet geändert haben. */
async function statusHolen() {
  let neu;
  try { neu = await holen('/api/status'); }
  catch (e) { if (!serverWeg) console.error('Status', e); serverWeg = true; zeichnen(); return; }
  const wiederda = serverWeg;
  serverWeg = false;
  const sig = (s) => JSON.stringify([s?.offen, s?.projekt, s?.reservierungen, s?.zuletzt, s?.abgleichFehler]);
  const geaendert = sig(neu) !== sig(zustand.status);
  zustand.status = neu;
  if (geaendert || wiederda) zustandMelden('status', neu);
  else zeichnen();
}

// ---------------------------------------------------------------------------------------------- Zeichnen

function zeichnen() {
  if (!teile) return;
  const s = zustand.status;
  const sel = element(zustand.auswahl);
  const ansicht = element(zustand.aktiveAnsicht);
  teile.links.textContent = hinweis.text || fremdText || (sel ? elementText(sel) : ansicht ? `Ansicht: ${anzeigeName(ansicht)}` : s?.offen ? 'Bereit' : 'Kein Projekt offen');
  teile.links.classList.toggle('hinweis', !!hinweis.text);

  let a;
  if (!verbindungOk || serverWeg) a = '<span class="sl-rot">● Verbindung zum Server unterbrochen</span>';
  else if (abgleichFehler || s?.abgleichFehler) a = `<span class="sl-rot" title="${esc(abgleichFehler || s.abgleichFehler)}">● Abgleich gestört: ${esc(abgleichFehler || s.abgleichFehler)}</span>`;
  else if (s?.offen) a = `<span class="sl-gruen">● Abgleich ${esc(s.letzterAbgleich ?? '–')}</span>`;
  else a = '<span class="sl-leise">○ Kein Abgleich</span>';
  if (teile.abgleich.innerHTML !== a) teile.abgleich.innerHTML = a;

  const res = s?.reservierungen ?? [];
  const eigene = res.filter((r) => r.eigene).length;
  const fremde = res.length - eigene;
  const parts = [];
  if (eigene) parts.push(`${eigene} eigene reserviert`);
  if (fremde) parts.push(`${fremde} von anderen reserviert`);
  teile.res.textContent = parts.length ? parts.join(' · ') : 'Keine Reservierungen';
  teile.res.classList.toggle('fremde', fremde > 0);
  teile.res.hidden = !s?.offen;

  // Familienbibliothek (Modul 2, C3): erreichbar oder offline, Zahl in der Warteschlange
  const bib = s?.offen ? bibliothekStatus(bibliothekInfo()) : null;
  teile.bib.hidden = !bib;
  if (bib) {
    teile.bib.textContent = bib.text;
    teile.bib.title = bib.titel;
    teile.bib.className = `sl-bib ${bib.klasse}`;
  }

  teile.akteur.textContent = s?.akteur ? `${s.akteur}@${s.geraet ?? ''}` : '';
  const st = s?.stand;
  teile.stand.textContent = st ? `Stand: ${st.pakete} Pakete` : '';
  teile.stand.title = st ? `${st.elemente} Elemente · ${st.verbindungen} Verbindungen · Prüfsumme ${st.pruefsumme}` : '';
}

// ---------------------------------------------------------------------------------------------- Reservierungen

async function reservierungenDialog() {
  await statusHolen();
  const res = zustand.status?.reservierungen ?? [];
  const eigene = res.filter((r) => r.eigene).length;
  const w = knoten(`<div>${res.length === 0 ? '<p class="leise">Es ist nichts reserviert.</p>' :
    `<p class="dlg-text">Reservierungen zeigen, wer gerade woran arbeitet. Klick auf eine Zeile wählt das Element aus.</p>
     <table class="tab klickbar"><thead><tr><th>Element</th><th>Wer</th><th>bis</th><th></th></tr></thead><tbody>${res.map((r) =>
      `<tr data-kz="${esc(r.element)}"><td>${esc(r.element)}</td><td>${esc(r.akteur)}@${esc(r.geraet)}</td><td>${esc(r.bis)}</td><td>${r.eigene ? 'eigene' : 'fremd'}</td></tr>`).join('')}</tbody></table>`}</div>`);
  w.querySelectorAll('tr[data-kz]').forEach((z) => {
    z.onclick = () => { const e = element(z.dataset.kz); if (e) auswaehlen(e.id); };
  });
  await zeigen({
    titel: 'Reservierungen', inhalt: w, breite: 640,
    knoepfe: [{ text: 'Schliessen', wert: false },
      ...(eigene ? [{ text: `Meine ${eigene} freigeben`, wert: true, primaer: true, klick: () => reservierungenFreigeben() }] : [])],
  });
}
