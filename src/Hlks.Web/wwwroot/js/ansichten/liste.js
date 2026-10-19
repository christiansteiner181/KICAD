// Listenansicht (mit Knopf «CSV …» seit N3, liste-csv.js): zeigt die Ableitung des Kerns (werte.liste: raumliste | strangliste | mengen | raumgrenzen | huellbauteile | materialliste |
// kollisionen | aussparungsliste, Vorgabe raumliste) als Tabelle. Keine eigene Rechnung – die Zahlen kommen aus abfrage(name). Kopf mit Titel und Modellstand, Sortieren per
// Klick auf den Spaltenkopf, Klick auf eine Zeile wählt das Element (Kennzeichen der ersten Spalte), Neuabfrage bei
// Modelländerungen (entprellt 400 ms).

import { zustand, element, auswaehlen } from '../zustand.js';
import { abfrage } from '../api.js';
import { aufbereiten, formatWert, sortiere } from './listenformat.js';

const ENTPRELLEN = 400;
const NAMEN = ['raumliste', 'strangliste', 'mengen', 'raumgrenzen', 'huellbauteile'];
const NAMEN_MODUL3 = ['materialliste'];   // Modul 3 (B4): Materialliste je Serie und Grösse
const NAMEN_MODUL3B = ['kollisionen', 'aussparungsliste'];   // Modul 3b (B4a): Kollisionsliste und Aussparungsliste (Klick wählt die Kollision bzw. Aussparung)

function erstellen(behaelter, ansichtElement) {
  let ansicht = ansichtElement;
  let daten = null;              // aufbereitet (listenformat.aufbereiten)
  let fehler = null;
  let nummer = 0;                // Zähler gegen veraltete Antworten
  let zeitgeber = 0;
  let zerstoert = false;
  const sortierung = new Map();  // tabellenindex → { schluessel, absteigend }

  behaelter.innerHTML = '<div class="li-wurzel"><div class="li-kopf"></div><div class="li-inhalt"></div></div>';
  const wurzel = behaelter.querySelector('.li-wurzel');
  const kopf = wurzel.querySelector('.li-kopf');
  const inhalt = wurzel.querySelector('.li-inhalt');

  const listenName = () => {
    const n = String(ansicht?.werte?.liste ?? 'raumliste').toLowerCase();
    return NAMEN.includes(n) || NAMEN_MODUL3.includes(n) || NAMEN_MODUL3B.includes(n) ? n : (ansicht?.werte?.liste ?? 'raumliste');
  };

  function parameter() {
    const n = listenName();
    return (['raumliste', 'mengen', 'raumgrenzen', 'huellbauteile', 'materialliste'].includes(n) || NAMEN_MODUL3B.includes(n)) && ansicht?.geschoss ? { geschoss: ansicht.geschoss } : {};
  }

  async function laden() {
    const mein = ++nummer;
    const name = listenName();
    try {
      const antwort = await abfrage(name, parameter());
      if (zerstoert || mein !== nummer) return;
      daten = aufbereiten(name, typeof antwort === 'string' ? tryJson(antwort) : antwort);
      fehler = null;
    } catch (e) {
      if (zerstoert || mein !== nummer) return;
      console.error('Liste: Abfrage fehlgeschlagen', name, e);
      fehler = { meldung: e?.message ?? String(e), vorschlag: e?.vorschlag ?? '' };
    }
    zeichnen();
  }

  function tryJson(t) { try { return JSON.parse(t); } catch { return {}; } }

  function el(tag, klasse, text) {
    const e = document.createElement(tag);
    if (klasse) e.className = klasse;
    if (text !== undefined) e.textContent = text;
    return e;
  }

  function kopfZeichnen() {
    kopf.replaceChildren();
    if (!daten) { kopf.append(el('div', 'li-titel', 'Liste')); return; }
    kopf.append(csvKnopf());
    const titel = el('div', 'li-titel', daten.titel + (daten.projekt ? ' · ' + daten.projekt : ''));
    const s = daten.stand;
    const stand = [s.pruefsumme ? `Modellstand ${s.pruefsumme}` : '', s.pakete !== null ? `${s.pakete} ${s.pakete === 1 ? 'Paket' : 'Pakete'}` : '', daten.erzeugt ? `erzeugt ${daten.erzeugt}` : '']
      .filter(Boolean).join(' · ');
    kopf.append(titel, el('div', 'li-stand', stand));
    for (const z of daten.zusammenfassung) kopf.append(el('div', 'li-summe', z));
  }

  /** Knopf «CSV …» (Nachbearbeitung N3): öffnet den Dialog, der die Liste mit der Sortierung der Ansicht als CSV-Datei herunterlädt. */
  function csvKnopf() {
    const k = el('button', 'li-csv', 'CSV …');
    k.type = 'button';
    k.title = 'Liste als CSV-Datei speichern (UTF-8, Semikolon)';
    k.onclick = async () => {
      try { (await import('./liste-csv-dialog.js')).csvDialog(daten, sortierung); } catch (e) { console.error('Liste: CSV', e); }
    };
    return k;
  }

  function tabelle(t, index) {
    const wrap = el('div', 'li-tabelle-wrap');
    if (t.titel) wrap.append(el('h3', 'li-tabellentitel', t.titel));
    const sort = sortierung.get(index);
    const zeilen = sort ? sortiere(t.zeilen, sort.schluessel, sort.absteigend) : t.zeilen;
    const tab = el('table', 'li-tabelle');
    const thead = el('thead');
    const kopfZeile = el('tr');
    for (const sp of t.spalten) {
      const th = el('th', sp.art === 'zahl' ? 'zahl' : '');
      const aktiv = sort?.schluessel === sp.schluessel;
      th.append(sp.titel, el('span', 'li-pfeil', aktiv ? (sort.absteigend ? ' ▼' : ' ▲') : ''));
      th.title = 'Klicken zum Sortieren';
      th.setAttribute('aria-sort', aktiv ? (sort.absteigend ? 'descending' : 'ascending') : 'none');
      th.onclick = () => {
        const alt = sortierung.get(index);
        sortierung.set(index, { schluessel: sp.schluessel, absteigend: alt?.schluessel === sp.schluessel ? !alt.absteigend : false });
        zeichnen();
      };
      kopfZeile.append(th);
    }
    thead.append(kopfZeile);
    const tbody = el('tbody');
    for (const z of zeilen) {
      const tr = el('tr');
      const kz = t.schluessel ? z[t.schluessel] : null;
      if (kz) {
        tr.classList.add('klickbar');
        tr.dataset.kennzeichen = String(kz);
        tr.onclick = () => {
          const e = element(String(kz));
          if (e) auswaehlen(e.id); else console.warn('Liste: Element nicht gefunden', kz);
        };
      }
      for (const sp of t.spalten) {
        const td = el('td', sp.art === 'zahl' ? 'zahl' : '', formatWert(sp.schluessel, z[sp.schluessel]));
        tr.append(td);
      }
      tbody.append(tr);
    }
    if (zeilen.length === 0) {
      const tr = el('tr'); const td = el('td', 'li-leer', 'Keine Einträge.'); td.colSpan = Math.max(t.spalten.length, 1); tr.append(td); tbody.append(tr);
    }
    tab.append(thead, tbody);
    // Fusszeilen (Summen je Angrenzung, Raumgrenzen und Hüllbauteile): vom Kern geliefert, hier nur dargestellt
    if (Array.isArray(t.fuss) && t.fuss.length) {
      const tfoot = el('tfoot');
      for (const f of t.fuss) {
        const tr = el('tr', 'li-fuss');
        for (const sp of t.spalten) tr.append(el('td', sp.art === 'zahl' ? 'zahl' : '', formatWert(sp.schluessel, f[sp.schluessel])));
        tfoot.append(tr);
      }
      tab.append(tfoot);
    }
    wrap.append(tab);
    return wrap;
  }

  function zeichnen() {
    if (zerstoert) return;
    const scroll = inhalt.scrollTop;
    kopfZeichnen();
    inhalt.replaceChildren();
    if (fehler) {
      const box = el('div', 'li-fehler');
      box.append(el('strong', '', 'Liste konnte nicht erstellt werden. '), document.createTextNode(fehler.meldung));
      if (fehler.vorschlag) box.append(el('div', 'li-vorschlag', fehler.vorschlag));
      inhalt.append(box);
    }
    if (daten) daten.tabellen.forEach((t, i) => inhalt.append(tabelle(t, i)));
    else if (!fehler) inhalt.append(el('div', 'li-laden', 'Lade …'));
    inhalt.scrollTop = scroll;
    auswahlMarkieren(zustand.auswahl);
  }

  function auswahlMarkieren(id) {
    const gewaehlt = id ? zustand.elemente.get(id) : null;
    const kz = gewaehlt?.kennzeichen?.toLowerCase();
    for (const tr of inhalt.querySelectorAll('tr.klickbar')) {
      tr.classList.toggle('gewaehlt', !!kz && tr.dataset.kennzeichen.toLowerCase() === kz);
    }
  }

  function entprellt() {
    clearTimeout(zeitgeber);
    zeitgeber = setTimeout(laden, ENTPRELLEN);
  }

  zeichnen();
  laden();

  return {
    aktualisieren(ereignis) {
      // Änderungen nur an Ansicht-Elementen betreffen die Ableitung nicht
      const ids = ereignis?.ids;
      if (!ereignis?.alles && Array.isArray(ids) && ids.length > 0 && !(ereignis.geloescht?.length)
        && ids.every((id) => zustand.elemente.get(id)?.klasse === 'Ansicht')) return;
      entprellt();
    },
    ansichtGeaendert(a) {
      const alt = listenName() + JSON.stringify(parameter());
      ansicht = a ?? ansicht;
      if (listenName() + JSON.stringify(parameter()) !== alt) { sortierung.clear(); daten = null; zeichnen(); entprellt(); }
    },
    auswahlGeaendert: auswahlMarkieren,
    /** CSV-Dialog dieser Liste (Kontextmenü im Projektbrowser: «CSV …»); wartet, bis die Liste geladen ist. */
    async csv() { if (!daten) await laden(); return (await import('./liste-csv-dialog.js')).csvDialog(daten, sortierung); },
    zoomAlles() { inhalt.scrollTop = 0; },
    zerstoeren() {
      zerstoert = true;
      clearTimeout(zeitgeber);
      behaelter.replaceChildren();
    },
  };
}

export default { erstellen };
