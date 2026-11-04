// Utilidades compartidas por los grabadores de video del SGV.
import puppeteer from 'puppeteer-core';
import ffmpegPath from 'ffmpeg-static';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { voz } from './voz.mjs';

export const RAIZ = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')), '..', '..');
export const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
export const espera = ms => new Promise(r => setTimeout(r, ms));

/** Contraseña de un usuario de pruebas, leída del archivo privado (nunca se imprime). */
export function claveDe(usuario) {
  const txt = fs.readFileSync(path.join(RAIZ, 'privado', 'CREDENCIALES_USUARIOS.txt'), 'utf8');
  const m = txt.match(new RegExp(usuario.replace(/[.]/g, '\\.') + '\\s+(\\S+)'));
  if (!m) throw new Error('No está ' + usuario + ' en privado/CREDENCIALES_USUARIOS.txt');
  return m[1];
}

export async function abrirNavegador() {
  return puppeteer.launch({
    executablePath: EDGE,
    headless: 'new',
    args: ['--hide-scrollbars', '--disable-features=Translate', '--lang=es-PE'],
    defaultViewport: null
  });
}

export function ffmpeg(args) {
  const r = spawnSync(ffmpegPath, ['-hide_banner', '-loglevel', 'error', '-y', ...args], { encoding: 'utf8' });
  if (r.status !== 0) throw new Error('ffmpeg: ' + (r.stderr || r.stdout));
  return r;
}

export function duracion(archivo) {
  const r = spawnSync(ffmpegPath, ['-hide_banner', '-i', archivo], { encoding: 'utf8' });
  const m = (r.stderr || '').match(/Duration: (\d+):(\d+):([\d.]+)/);
  if (!m) throw new Error('Sin duración: ' + archivo);
  return (+m[1]) * 3600 + (+m[2]) * 60 + parseFloat(m[3]);
}

/**
 * Grabación por escenas: cada escena tiene una locución y una lista de cuadros (capturas).
 * La escena dura lo que su locución (+ pausa); los cuadros se reparten según su peso.
 */
export class Grabacion {
  constructor(dir, { ancho, alto, voice, rate = '+0%', pausa = 0.6 }) {
    this.dir = dir; this.ancho = ancho; this.alto = alto; this.voice = voice; this.rate = rate; this.pausa = pausa;
    fs.rmSync(dir, { recursive: true, force: true });
    fs.mkdirSync(dir, { recursive: true });
    this.escenas = [];
  }
  escena(texto) {
    const e = { n: this.escenas.length + 1, texto, cuadros: [] };
    this.escenas.push(e);
    this.actual = e;
    return e;
  }
  async cuadro(page, peso = 1) {
    const e = this.actual;
    const archivo = path.join(this.dir, `e${String(e.n).padStart(2, '0')}_${String(e.cuadros.length + 1).padStart(2, '0')}.png`);
    await page.screenshot({ path: archivo, type: 'png' });
    e.cuadros.push({ archivo, peso });
  }
  async armar(salida) {
    const segmentos = [];
    for (const e of this.escenas) {
      const mp3 = path.join(this.dir, `e${String(e.n).padStart(2, '0')}.mp3`);
      await voz(e.texto, mp3, this.voice, this.rate);
      const total = duracion(mp3) + this.pausa;
      const pesos = e.cuadros.reduce((a, c) => a + c.peso, 0);
      const lista = path.join(this.dir, `e${String(e.n).padStart(2, '0')}.txt`);
      let txt = '';
      e.cuadros.forEach(c => { txt += `file '${c.archivo.replace(/\\/g, '/')}'\nduration ${(total * c.peso / pesos).toFixed(3)}\n`; });
      txt += `file '${e.cuadros[e.cuadros.length - 1].archivo.replace(/\\/g, '/')}'\n`;
      fs.writeFileSync(lista, txt);
      const seg = path.join(this.dir, `seg${String(e.n).padStart(2, '0')}.mp4`);
      ffmpeg(['-f', 'concat', '-safe', '0', '-i', lista, '-i', mp3,
        '-vf', `scale=${this.ancho}:${this.alto}:flags=lanczos,format=yuv420p,fps=30`,
        '-af', `apad=whole_dur=${total.toFixed(3)}`,
        '-c:v', 'libx264', '-preset', 'medium', '-crf', '20', '-c:a', 'aac', '-b:a', '128k', '-ar', '24000', '-ac', '1',
        '-t', total.toFixed(3), seg]);
      segmentos.push(seg);
      console.log(`escena ${e.n}: ${total.toFixed(1)} s, ${e.cuadros.length} cuadros`);
    }
    const lista = path.join(this.dir, 'segmentos.txt');
    fs.writeFileSync(lista, segmentos.map(s => `file '${s.replace(/\\/g, '/')}'`).join('\n'));
    ffmpeg(['-f', 'concat', '-safe', '0', '-i', lista, '-c', 'copy', '-movflags', '+faststart', salida]);
    console.log('video:', salida, (duracion(salida)).toFixed(1), 's');
  }
}
