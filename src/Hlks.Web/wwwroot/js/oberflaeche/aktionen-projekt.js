// Aktionen der Register «Datei» (Projekt öffnen, anlegen, schliessen, zuletzt geöffnet) und «Verwalten»
// (Projektinformationen, Synchronisieren, Reservierungen freigeben, Prüfbefunde, Katalog, Tastaturkürzel).
// Projekte öffnet der Server; die Oberfläche lädt danach Katalog und Modell neu (projektGeoeffnet).

import { zustand, element, auswaehlen, projektLeeren, melden as zustandMelden } from '../zustand.js';
import { abfrage, senden, holen } from '../api.js';
import { registrieren, aktionen, hinweisText } from './register.js';
import { zeigen, meldung, projektOeffnenDialog, projektAnlegenDialog, projektGeoeffnet, projektStart } from './dialoge.js';
import { sende } from './befehle.js';
import { katalogDialog } from './katalogansicht.js';
import { esc, knoten } from './werkzeug.js';
import { istSichtbar as startSichtbar } from './startbildschirm.js';
import { kuerzelZeilen } from './kuerzel-liste.js';
import { projektInfoDialog } from './projektinfo-dialog.js';   // N3: Projektinformationen bearbeiten

const projektOffen = () => !!zustand.status?.offen;
const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';

// ---------------------------------------------------------------------------------------------- Datei

registrieren({
  id: 'datei.oeffnen', titel: 'Projekt öffnen', symbol: 'ordner', kuerzel: null, register: 'datei', gruppe: 'Projekt',
  ausfuehren: () => projektOeffnenDialog(),
});
registrieren({
  id: 'datei.anlegen', titel: 'Projekt anlegen', symbol: 'neu', kuerzel: null, register: 'datei', gruppe: 'Projekt',
  ausfuehren: () => projektAnlegenDialog(false),
});
registrieren({
  id: 'datei.beispiel', titel: 'Beispiel\u00adprojekt anlegen', symbol: 'beispiel', kuerzel: null, register: 'datei', gruppe: 'Projekt',
  ausfuehren: () => projektAnlegenDialog(true),
});
registrieren({
  id: 'datei.schliessen', titel: 'Projekt schliessen', symbol: 'schliessen', kuerzel: null, register: 'datei', gruppe: 'Projekt',
  aktiv: projektOffen, hinweis: 'Es ist kein Projekt offen.',
  async ausfuehren() {
    const fenster = await import('../fenster.js');
    zustand.geladen = null;   // vorab, damit das Ereignis «projekt» den Startdialog nicht ein zweites Mal öffnet
    const status = await senden('/api/projekt/schliessen', {});
    for (const o of fenster.alle()) fenster.schliessen(o.id);
    zustand.status = status;
    zustandMelden('status', status);
    projektLeeren();
    document.getElementById('titel').textContent = 'HLKS-CAD';
    await projektStart();
  },
});
// Startbildschirm (S1): Haus-Symbol im Schnellzugriff und Strg+D zeigen ihn auch bei offenem Projekt; Esc oder ein Klick auf das
// offene Projekt kehrt zurück. Die Tastatur (Strg+D, Esc) behandelt startbildschirm.js selbst, weil sie auch aus dem Suchfeld gelten muss.
registrieren({
  id: 'start.zeigen', titel: 'Startbildschirm', symbol: 'haus', kuerzel: 'Strg+D', register: null, gruppe: 'Projekt',
  gedrueckt: () => startSichtbar(),
  async ausfuehren() { (await import('./startbildschirm.js')).umschalten(); },
});
registrieren({
  id: 'datei.zuletzt-oeffnen', titel: 'Zuletzt geöffnetes Projekt öffnen', symbol: 'zuletzt', kuerzel: null,
  async ausfuehren(p) {
    if (!p?.ordner) return;
    const status = await senden('/api/projekt/oeffnen', { ordner: p.ordner });
    await projektGeoeffnet(status);
  },
});

// ---------------------------------------------------------------------------------------------- Verwalten

registrieren({
  id: 'verwalten.projektinfo', titel: 'Projekt\u00adinformationen', symbol: 'info', kuerzel: null, register: 'verwalten', gruppe: 'Projekt',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => projektInfoDialog(),
});

registrieren({
  id: 'verwalten.synchronisieren', titel: 'Synchronisieren', symbol: 'synchronisieren', kuerzel: null, register: 'verwalten', gruppe: 'Zusammenarbeit',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT,
  async ausfuehren() {
    const r = await sende('synchronisieren');
    if (r.angenommen) meldung(r.meldung || 'Mit der Projektablage abgeglichen.', 'ok');
    else meldung(r.fehler || r.meldung || 'Synchronisieren nicht möglich.', 'warnung', { vorschlag: r.vorschlag });
  },
});

/** Gibt alle eigenen Reservierungen frei (POST /api/freigeben). */
export async function reservierungenFreigeben() {
  const r = await senden('/api/freigeben', {});
  zustand.status = await holen('/api/status');
  zustandMelden('status', zustand.status);
  const n = typeof r?.freigegeben === 'number' ? r.freigegeben : Array.isArray(r?.freigegeben) ? r.freigegeben.length : null;
  meldung(n === null ? 'Reservierungen freigegeben.' : n === 0 ? 'Keine eigenen Reservierungen vorhanden.' : `${n} Reservierung${n === 1 ? '' : 'en'} freigegeben.`, 'ok');
}

registrieren({
  id: 'verwalten.freigeben', titel: 'Meine Elemente freigeben', symbol: 'freigeben', kuerzel: null, register: 'verwalten', gruppe: 'Zusammenarbeit',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT,
  ausfuehren: reservierungenFreigeben,
});

/** Dialog der Prüfbefunde; Klick auf eine Zeile wählt das Element aus. */
async function befunde() {
  const w = knoten(`<div class="befunde"><div class="befunde-kopf"><label>Schwere <select class="b-schwere"><option value="">alle</option><option>fehler</option><option>warnung</option><option>hinweis</option></select></label>
    <span class="b-zahl leise"></span></div><div class="b-liste"></div></div>`);
  const liste = w.querySelector('.b-liste');
  const laden = async () => {
    liste.innerHTML = '<p class="leise">Wird geprüft …</p>';
    const s = w.querySelector('.b-schwere').value;
    try {
      const r = await abfrage('pruefen', s ? { schwere: s } : {});
      const b = r.befunde ?? [];
      w.querySelector('.b-zahl').textContent = `${r.anzahl ?? b.length} Befunde${r.gekuerzt ? ' (gekürzt)' : ''}` +
        (r.jeCode && Object.keys(r.jeCode).length ? ' · ' + Object.entries(r.jeCode).map(([k, n]) => `${k}: ${n}`).join(' · ') : '');
      liste.innerHTML = b.length === 0 ? '<p class="leise">Keine Befunde.</p>' :
        `<table class="tab klickbar"><thead><tr><th>Schwere</th><th>Code</th><th>Element</th><th>Befund</th><th>Vorschlag</th></tr></thead><tbody>${b.map((x) =>
          `<tr data-kz="${esc(x.element ?? '')}"><td class="schwere-${esc(x.schwere)}">${esc(x.schwere)}</td><td>${esc(x.code)}</td><td>${esc(x.element ?? '')}</td><td>${esc(x.text)}</td><td>${esc(x.vorschlag ?? '')}</td></tr>`).join('')}</tbody></table>`;
      liste.querySelectorAll('tr[data-kz]').forEach((z) => {
        z.onclick = () => {
          liste.querySelectorAll('.gewaehlt').forEach((g) => g.classList.remove('gewaehlt'));
          z.classList.add('gewaehlt');
          const e = element(z.dataset.kz);
          if (e) auswaehlen(e.id);
        };
      });
    } catch (e) { console.error(e); liste.innerHTML = `<p class="fehlertext">${esc(e.message)}</p>`; }
  };
  w.querySelector('.b-schwere').onchange = laden;
  laden();
  await zeigen({ titel: 'Prüfbefunde', inhalt: w, breite: 900, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

registrieren({
  id: 'verwalten.befunde', titel: 'Prüfbefunde', symbol: 'befunde', kuerzel: null, register: 'verwalten', gruppe: 'Modell',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: befunde,
});
registrieren({
  id: 'verwalten.katalog', titel: 'Katalog anzeigen', symbol: 'katalog', kuerzel: null, register: 'verwalten', gruppe: 'Modell',
  aktiv: () => !!zustand.katalog, hinweis: KEIN_PROJEKT, ausfuehren: katalogDialog,
});

/** Dialog der Tastaturkürzel (KS): alle Aktionen mit Kürzel, nach Register geordnet (kuerzel-liste.js; seit N3 mit allen Registern des Menübands). */
async function kuerzelDialog() {
  const zeilen = kuerzelZeilen([...aktionen.values()], hinweisText);
  const w = knoten(`<div><input type="search" class="k-suche" placeholder="Suchen …" data-kein-enter="1"><div class="k-tab"><table class="tab"><thead><tr><th>Befehl</th><th>Kürzel</th><th>Register</th><th></th></tr></thead><tbody>${zeilen.map((z) =>
    `<tr><td>${esc(z.titel)}</td><td><kbd>${esc(z.kuerzel)}</kbd></td><td>${esc(z.register)}</td><td class="leise">${esc(z.hinweis)}</td></tr>`).join('')}</tbody></table></div>
    <p class="leise">Wie in Revit: zwei Zeichen nacheinander tippen (ohne Eingabefeld im Fokus). Esc bricht ab und hebt die Auswahl auf. Eigene Kürzel folgen später.</p></div>`);
  w.querySelector('.k-suche').dataset.keinEnter = '1';
  w.querySelector('.k-suche').oninput = (e) => {
    const t = e.target.value.trim().toLowerCase();
    w.querySelectorAll('tbody tr').forEach((r) => { r.hidden = t !== '' && !r.textContent.toLowerCase().includes(t); });
  };
  await zeigen({ titel: 'Tastaturkürzel', inhalt: w, breite: 640, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}

registrieren({
  id: 'verwalten.kuerzel', titel: 'Tastaturkürzel', symbol: 'tastatur', kuerzel: 'KS', register: 'verwalten', gruppe: 'Hilfe',
  ausfuehren: kuerzelDialog,
});

// Dialog «Gesten» (Modul G, entwurf/modul-gesten.md): Liste der Gesten, Einstellungen, Testfeld, Punktfolge aufnehmen. Er wird erst beim Öffnen geladen (ohne eingeschaltete Gesten keine Last);
// Rechtsklick auf das Symbol «Gesten» in der Statusleiste öffnet ihn ebenfalls.
registrieren({
  id: 'verwalten.gesten', titel: 'Gesten', symbol: 'gesten', kuerzel: null, register: 'verwalten', gruppe: 'Hilfe',
  ausfuehren: async () => { await (await import('../gesten/gesten-dialog.js')).gestenDialog(); },
});
