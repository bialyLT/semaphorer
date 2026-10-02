#!/usr/bin/env python3
"""Paso 6 — genera los únicos assets importados del juego (audio/*.wav).

Son placeholders sintetizados (44.1kHz mono 16-bit) para que el AudioManager
tenga qué reproducir. Para poner sonido real basta reemplazar cada .wav por
una grabación con el MISMO nombre: el código no cambia.

  python3 tools/generar_audio.py

Sonidos (inventario de Play() en scripts/):
  monedas, bocina, sirena, frote, abrir, negado + loops ambiente/lluvia
  + música musica_menu (menú) y musica_juego (partida).
"""
import math
import os
import random
import struct
import wave

SR = 44100
DEST = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "audio")


def guardar(nombre, muestras):
    os.makedirs(DEST, exist_ok=True)
    ruta = os.path.join(DEST, nombre + ".wav")
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(
            struct.pack("<h", max(-32768, min(32767, int(s * 32767)))) for s in muestras))
    print(f"{ruta} ({len(muestras) / SR:.2f}s)")


def seno(f, t):
    return math.sin(2 * math.pi * f * t)


def adsr(n, a=0.01, r=0.15):
    """Envolvente ataque/decaimiento simple 0..1."""
    env = []
    na = max(1, int(n * a))
    nr = max(1, int(n * r))
    for i in range(n):
        v = min(1.0, i / na)
        v *= min(1.0, (n - i) / nr)
        env.append(v)
    return env


def tono(freqs, seg, vol=0.5, arms=None):
    n = int(seg * SR)
    env = adsr(n, r=0.4)
    out = []
    for i in range(n):
        t = i / SR
        v = sum(seno(f, t) for f in freqs) / len(freqs)
        out.append(v * env[i] * vol)
    return out


def mezclar(a, b):
    n = max(len(a), len(b))
    a += [0.0] * (n - len(a))
    b += [0.0] * (n - len(b))
    return [x + y for x, y in zip(a, b)]


def loop_suave(muestras, xf_seg=0.5):
    """Crossfade punta-con-punta para que el loop no haga click."""
    xf = int(xf_seg * SR)
    n = len(muestras)
    for i in range(xf):
        k = i / xf
        muestras[i] = muestras[i] * k + muestras[n - xf + i] * (1 - k)
    return muestras[:n - xf]


random.seed(7)

# 1. Monedas: blip agudo doble (B5 -> E6), como máquina expendedora.
monedas = tono([988], 0.09, 0.5) + tono([1319], 0.16, 0.5)
guardar("monedas", monedas)

# 2. Bocina: doble tono de auto (392+494Hz con armónicos), 0.5s.
n = int(0.5 * SR)
env = adsr(n, a=0.02, r=0.2)
bocina = []
for i in range(n):
    t = i / SR
    v = (seno(392, t) + seno(494, t)) / 2
    v += 0.3 * (seno(784, t) + seno(988, t)) / 2  # cuerpo metálico
    v *= 1 + 0.06 * seno(30, t)  # vibrato de bocina barata
    bocina.append(v * env[i] * 0.45)
guardar("bocina", bocina)

# 3. Sirena: wail policial 650<->1300Hz, 1.6s.
n = int(1.6 * SR)
env = adsr(n, a=0.05, r=0.15)
sirena = []
fase = 0.0
for i in range(n):
    t = i / SR
    f = 975 + 325 * math.sin(2 * math.pi * t / 1.6)  # sube y baja una vez
    fase += 2 * math.pi * f / SR
    sirena.append(math.sin(fase) * env[i] * 0.4)
guardar("sirena", sirena)

# 4. Frote: ruido corto brillante (esponja en vidrio), 0.14s.
n = int(0.14 * SR)
env = adsr(n, a=0.05, r=0.6)
prev = 0.0
frote = []
for i in range(n):
    x = random.uniform(-1, 1)
    hp = x - prev  # pasa-altos: solo el brillo del roce
    prev = x
    frote.append((hp * 0.7 + 0.3 * seno(1800, i / SR)) * env[i] * 0.4)
guardar("frote", frote)

# 5. Abrir: swoosh UI hacia arriba 300->950Hz, 0.16s.
n = int(0.16 * SR)
env = adsr(n, a=0.05, r=0.5)
abrir, fase = [], 0.0
for i in range(n):
    f = 300 + (950 - 300) * (i / n)
    fase += 2 * math.pi * f / SR
    abrir.append(math.sin(fase) * env[i] * 0.4)
guardar("abrir", abrir)

# 6. Negado: buzz grave 130Hz, 0.28s.
n = int(0.28 * SR)
env = adsr(n, a=0.01, r=0.3)
negado = []
for i in range(n):
    t = i / SR
    v = seno(130, t) + 0.4 * seno(390, t)
    negado.append(v * env[i] * 0.4)
guardar("negado", negado)

# 7. Ambiente ciudad (loop 4s): ruido marrón suave + respiración lenta.
n = int(4.0 * SR)
brown, acc = [], 0.0
for _ in range(n):
    acc = (acc + random.uniform(-1, 1) * 0.02) * 0.998
    brown.append(acc)
pico = max(abs(v) for v in brown) or 1.0
lfo = [0.7 + 0.3 * seno(0.25, i / SR) for i in range(n)]
ambiente = [brown[i] / pico * 0.25 * lfo[i] for i in range(n)]
guardar("ambiente", loop_suave(ambiente))

# 8. Lluvia (loop 3s): ruido blanco pasa-altos, parejo.
n = int(3.0 * SR)
prev = 0.0
lluvia = []
for _ in range(n):
    x = random.uniform(-1, 1)
    lluvia.append((x - prev) * 0.3)
    prev = x
guardar("lluvia", loop_suave(lluvia, 0.4))


def f(semi):
    """Frecuencia en Hz, semitonos relativos a A4=440."""
    return 440.0 * (2 ** (semi / 12))


def agregar(dst, src, t0_seg, vol=1.0):
    """Suma src dentro de dst a partir del segundo t0 (extiende si falta)."""
    o = int(t0_seg * SR)
    if len(dst) < o + len(src):
        dst += [0.0] * (o + len(src) - len(dst))
    for i, v in enumerate(src):
        dst[o + i] += v * vol
    return dst


def normalizar(muestras, pico=0.5):
    m = max(abs(v) for v in muestras) or 1.0
    k = pico / m
    return [v * k for v in muestras]


def pad(freqs, seg, vol=0.30):
    """Acorde colchón: senos + 2º armónico suave, ataque rápido y salida larga."""
    n = int(seg * SR)
    env = adsr(n, a=0.02, r=0.5)
    out = []
    for i in range(n):
        t = i / SR
        v = sum(seno(f, t) + 0.25 * seno(2 * f, t) for f in freqs) / len(freqs)
        out.append(v * env[i] * vol)
    return out


def campana(freqs, seg, vol=0.22):
    """Nota melódica con subida y bajada suaves (no pisa el pad)."""
    n = int(seg * SR)
    env = adsr(n, a=0.25, r=0.45)
    out = []
    for i in range(n):
        t = i / SR
        v = sum(seno(f, t) for f in freqs) / len(freqs)
        out.append(v * env[i] * vol)
    return out


def bajo_nota(freq, seg, vol=0.42):
    """Bajo redondo: fundamental + 2º armónico, envolvente por negra."""
    n = int(seg * SR)
    env = adsr(n, a=0.01, r=0.35)
    out = []
    for i in range(n):
        t = i / SR
        out.append((seno(freq, t) + 0.35 * seno(2 * freq, t)) * env[i] * vol)
    return out


def kick():
    """Bombo 0.14s: seno que cae de 120 a 45Hz + click."""
    n = int(0.14 * SR)
    env = adsr(n, a=0.005, r=0.7)
    out, fase = [], 0.0
    for i in range(n):
        k = i / n
        fr = 45 + (120 - 45) * math.exp(-k * 12)
        fase += 2 * math.pi * fr / SR
        out.append((math.sin(fase) + 0.3 * seno(2500, i / SR) * math.exp(-k * 30)) * env[i] * 0.55)
    return out


def hat(vol=0.10):
    """Hi-hat 0.04s: ruido pasa-altos muy corto."""
    n = int(0.04 * SR)
    env = adsr(n, a=0.005, r=0.8)
    out, prev = [], 0.0
    for i in range(n):
        x = random.uniform(-1, 1)
        out.append((x - prev) * env[i] * vol)
        prev = x
    return out


# 9. Música del menú (loop ~12s): pads Am-F-C-G + melodía que desciende.
AC_MENU = [
    [-24, -12, -9, -5],    # Am
    [-28, -16, -12, -9],   # F
    [-21, -17, -14, -9],   # C
    [-26, -14, -10, -7],   # G
]
MEL_MENU = [7, 5, 3, 2]  # E5 D5 C5 B4, una por acorde
menu = []
for ci, semis in enumerate(AC_MENU):
    agregar(menu, pad([f(s) for s in semis], 3.0), ci * 3.0)
    agregar(menu, campana([f(MEL_MENU[ci])], 2.2, 0.16), ci * 3.0 + 0.4)
guardar("musica_menu", loop_suave(normalizar(menu), 0.6))

# 10. Música del juego (loop ~8s a 120BPM): bajo + arpegio + kick + hats.
# Un acorde por compás (2s): Am F C G.
AC_JUEGO = [
    {"bajo": -24, "arp": [-12, -9, -5, 0]},     # Am
    {"bajo": -28, "arp": [-16, -12, -9, -4]},   # F
    {"bajo": -21, "arp": [-9, -5, -2, 3]},      # C
    {"bajo": -26, "arp": [-14, -10, -7, -2]},   # G
]
BEAT = 0.5
juego = []
K, H = kick(), hat()
for ci, ac in enumerate(AC_JUEGO):
    t0 = ci * 4 * BEAT
    raiz = ac["bajo"]
    # Bajo en negras: raíz, raíz, quinta, raíz.
    for b, st in enumerate([raiz, raiz, raiz + 7, raiz]):
        agregar(juego, bajo_nota(f(st), BEAT * 0.95), t0 + b * BEAT)
    # Arpegio en corcheas.
    for c in range(8):
        st = ac["arp"][c % 4]
        agregar(juego, tono([f(st)], 0.22, 0.20), t0 + c * BEAT / 2)
    # Kick en tiempos 1 y 3, hats en cada corchea.
    agregar(juego, K, t0, 0.9)
    agregar(juego, K, t0 + 2 * BEAT, 0.8)
    for c in range(8):
        agregar(juego, H, t0 + c * BEAT / 2, 1.0 if c % 2 else 0.7)
guardar("musica_juego", loop_suave(normalizar(juego), 0.3))

print("OK: 10 wavs en audio/. Reemplazá cualquier archivo por una grabación real con el mismo nombre.")
