// Genera un MP3 con voz neural de Perú (servicio de voz de Microsoft Edge).
// Uso: node voz.mjs "texto" salida.mp3 [es-PE-CamilaNeural|es-PE-AlexNeural] [velocidad, p. ej. +0%]
import { MsEdgeTTS, OUTPUT_FORMAT } from 'msedge-tts';
import fs from 'node:fs';
import path from 'node:path';

export async function voz(texto, salida, voice = 'es-PE-CamilaNeural', rate = '+0%') {
  const tts = new MsEdgeTTS();
  await tts.setMetadata(voice, OUTPUT_FORMAT.AUDIO_24KHZ_96KBITRATE_MONO_MP3);
  const dir = fs.mkdtempSync(path.join(path.dirname(path.resolve(salida)), '.tts-'));
  const { audioFilePath } = await tts.toFile(dir, texto, { rate });
  fs.renameSync(audioFilePath, salida);
  fs.rmSync(dir, { recursive: true, force: true });
  tts.close();
  return salida;
}

if (process.argv[1] && process.argv[1].endsWith('voz.mjs')) {
  const [, , texto, salida, voice, rate] = process.argv;
  await voz(texto, salida, voice, rate);
  console.log('ok', salida, fs.statSync(salida).size, 'bytes');
}
