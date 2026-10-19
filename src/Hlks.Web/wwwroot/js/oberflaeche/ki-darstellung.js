// Darstellung der Antworten in der KI-Palette «Claude» (Modul 3c, Teil O): baut aus den Blöcken von ki-text.js › formatieren() DOM-Elemente.
// Nie innerHTML und nie insertAdjacentHTML mit fremdem Text: jedes Element entsteht mit createElement, jeder Text mit createTextNode/textContent – damit bleibt
// <script>, <img onerror=…> und Ähnliches in einer Antwort nackter Text. Links nur aus linkSicher() (http, https, mailto), im neuen Fenster mit rel="noopener noreferrer".
// Von dieser Datei hängt kein anderes Modul ab; ki-palette.js ruft blockElemente() auf. (DOM nötig; die Logik darunter ist in Node getestet.)

import { formatieren } from './ki-text.js';

export function el(tag, klasse, text) {
  const e = document.createElement(tag);
  if (klasse) e.className = klasse;
  if (text !== undefined && text !== null) e.textContent = text;
  return e;
}

/** Inline-Knoten (ki-text.js › inlineLesen) → DOM-Knoten in ziel. */
export function inlineAnhaengen(ziel, knoten) {
  for (const k of knoten ?? []) {
    switch (k.t) {
      case 'text': ziel.appendChild(document.createTextNode(k.s)); break;
      case 'umbruch': ziel.appendChild(document.createElement('br')); break;
      case 'code': ziel.appendChild(el('code', 'ki-code-inline', k.s)); break;
      case 'fett': { const e = el('strong'); inlineAnhaengen(e, k.kinder); ziel.appendChild(e); break; }
      case 'kursiv': { const e = el('em'); inlineAnhaengen(e, k.kinder); ziel.appendChild(e); break; }
      case 'link': {
        const a = el('a', 'ki-link');
        a.href = k.href;                       // schon durch linkSicher() geprüft
        a.target = '_blank';
        a.rel = 'noopener noreferrer';
        a.title = k.href;
        inlineAnhaengen(a, k.kinder);
        ziel.appendChild(a);
        break;
      }
      default: break;
    }
  }
  return ziel;
}

/** Blöcke → DOM-Knoten in ziel. */
export function blockAnhaengen(ziel, bloecke) {
  for (const b of bloecke) {
    switch (b.art) {
      case 'absatz': ziel.appendChild(inlineAnhaengen(el('p', 'ki-absatz'), b.kinder)); break;
      case 'ueberschrift': ziel.appendChild(inlineAnhaengen(el('div', `ki-ue ki-ue${b.stufe}`), b.kinder)); break;
      case 'linie': ziel.appendChild(el('hr', 'ki-linie')); break;
      case 'code': {
        const pre = el('pre', 'ki-code');
        const c = el('code', null, b.s);
        if (b.sprache) pre.dataset.sprache = b.sprache;
        pre.appendChild(c);
        ziel.appendChild(pre);
        break;
      }
      case 'zitat': { const q = el('blockquote', 'ki-zitat'); blockAnhaengen(q, b.bloecke); ziel.appendChild(q); break; }
      case 'liste': {
        const l = el(b.geordnet ? 'ol' : 'ul', 'ki-liste');
        if (b.geordnet && b.start !== 1) l.start = b.start;
        for (const p of b.punkte) {
          const li = el('li');
          if (p.einzug > 0) li.style.marginLeft = `${Math.min(p.einzug, 4) * 14}px`;
          inlineAnhaengen(li, p.kinder);
          l.appendChild(li);
        }
        ziel.appendChild(l);
        break;
      }
      case 'tabelle': {
        const wrap = el('div', 'ki-tabelle-wrap');
        const t = el('table', 'ki-tabelle');
        const kopf = el('thead'); const zk = el('tr');
        b.kopf.forEach((c, i) => { const th = el('th'); th.style.textAlign = b.ausrichtung[i] === 'mitte' ? 'center' : b.ausrichtung[i] === 'rechts' ? 'right' : 'left'; inlineAnhaengen(th, c); zk.appendChild(th); });
        kopf.appendChild(zk); t.appendChild(kopf);
        const rumpf = el('tbody');
        for (const r of b.zeilen) {
          const tr = el('tr');
          r.forEach((c, i) => { const td = el('td'); td.style.textAlign = b.ausrichtung[i] === 'mitte' ? 'center' : b.ausrichtung[i] === 'rechts' ? 'right' : 'left'; inlineAnhaengen(td, c); tr.appendChild(td); });
          rumpf.appendChild(tr);
        }
        t.appendChild(rumpf);
        wrap.appendChild(t);
        ziel.appendChild(wrap);
        break;
      }
      default: break;
    }
  }
  return ziel;
}

/** Antworttext → Knoten (Container <div class="ki-text">). */
export function textElement(roh) {
  return blockAnhaengen(el('div', 'ki-text'), formatieren(roh));
}
