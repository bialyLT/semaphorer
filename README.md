# Semaphorer

Simulador 3D PC (primera + tercera persona) de una persona que trabaja en el semáforo.
Estilo: Schedule I / Gas Station Simulator pero en un cruce latino.
MVP: **limpiaparabrisas**. Arquitectura lista para malabares y venta ambulante.

Todo por código en **C#**, incluyendo 3D procedural. Sonido: únicos assets importados (`.wav` en `audio/`, generados con `tools/generar_audio.py`; reemplazá cualquier archivo por una grabación real con el mismo nombre y listo).

## Menú

Al arrancar: título + 3 slots de partida (Jugar / Nueva con doble-click de
confirmación / borrar con X) + Opciones (sensibilidad, volumen, pantalla
completa) + Salir. Tu partida del proyecto viejo se migra sola al slot 1.
En juego, `Esc` abre la pausa (continuar / opciones / guardar y salir).

## ¿Godot trabaja con C#? Sí

Godot 4 tiene soporte oficial **Godot .NET**: programás en C# con la misma API que GDScript
(`Node3D`, `_Ready()`, `_Process()`, señales, `CharacterBody3D`, `ArrayMesh`, etc.).
Necesitás:

- **Godot 4.3+ versión .NET** (no la classic): https://godotengine.org/download (pestaña `.NET`)
- **.NET SDK 8**: https://dotnet.microsoft.com/download

En Fedora:
```bash
# .NET 8 (usuario, sin sudo) o via dnf si tenés permiso:
# Descarga tarball de Microsoft y extrae en ~/.dotnet

# Godot .NET: bajar el binario Linux x86_64 .NET desde la web oficial,
# descomprimir y correr ./Godot_v4.3-stable_mono_linux_x86_64
```

Este repo ya es un proyecto `.NET` (`SimuladorSemaforo.csproj` con `Godot.NET.Sdk/4.3.0`).
Al abrirlo en Godot .NET compila solo. En VSCode instalá extensión `godot-tools`.

## Controles Paso 1

- `WASD`: moverse | `Mouse`: mirar (click para capturar)
- `V`: alternar 1ª / 3ª persona | `Espacio`: saltar (para subir a la vereda)
- `E`: limpiar parabrisas (solo en rojo, cerca del auto detenido)
- `F`: abrir/cerrar mejoras en cualquier lado (árbol EQUIPO/CALLE/NEGOCIO, `1-7` o click para comprar/mejorar)
- `G`: escondite (guardar/sacar plata, mochila azul en la vereda este cerca de la esquina) | `I`: ver mochila
- `Esc`: pausa (continuar / opciones / guardar y salir)

## Policía y escondite

Los autos tienen colisión (ya no los atravesás). La patrulla recorre la
transversal: si te ve sobre la calzada más de lo que dura un rojo (22s
acumulados sin pisar la vereda), te decomisa la plata de encima y tus
mejoras, y te manda a la vereda. Guardá la plata con `G`
en la mochila azul del puesto: lo guardado está a salvo.

## Loop MVP

Ciclo en 4 fases (nunca doble verde): carril 1 → 2 → 3 → 4.
Cada luz: verde 19s + amarillo 3s + rojo 66s (ventanas largas de trabajo).
En tu carril: Verde (los apurados/enojados a medias se van sin pagar; el amable te da 4s de changüí) → Amarillo (posiciónate) → Rojo (limpia con E **con ritmo**: 6 toques por auto = $10 + tarifa + propina, ojo con el humor) → Verde (autos arrancan).
Los avisos del centro se borran a los 5s. El tráfico es dinámico: los autos
respawnean aleatorios (tipo, color, carril) y doblan terminando siempre en
su carril (mapa de carriles 1-4 con mano derecha, sin ir por el medio).
Hay sendas en los 4 carriles. Base: 2 autos (Tráfico Nv1 suma UN transversal
alternado, Nv2 duplica principales: 5). La patrulla circula por su carril
y respawnea como el resto. La policía ve a 45m, más rápido cerca y al doble
(200%) si le cortás el tránsito.
La patrulla (carriles 3/4) obedece sus semáforos y también se le lava el parabrisas.

## Peatones (transeúntes)

N vidas dando vueltas por las veredas (`data/balance.json` → `peatones`).
Caminan sobre un grafo de esquinas: veredas en "L" con los pies de las sendas
al medio. **Nunca andan por la calzada**: para cruzar tienen que llegar a la
senda, y ahí hacen lo que hace la gente:

1. Paran en el pie y **miran a los dos lados** (gira la cabeza, `mirar_antes_seg`).
2. Cruzan sólo si **los dos semáforos de ese eje están en ROJO**, el rojo
   alcanza para completar el cruce y no hay ningún auto en el corredor de la
   senda (`CarSpawner.HayAutoEnCorredor`).
3. Si a mitad de camino cae el verde, **se apuran** (`velocidad_apurado`).
4. Si esperan demasiado (`paciencia_max_seg`) se cansan y vuelven por donde vinieron.

`Peaton.cs` = peatón + grafo · `PeatonSpawner.cs` = ambientación · `ProcPeaton.cs` = visual.

**TODO — ladrones que roban la mochila** (pendiente de detalles):
idea planteada en `scripts/Traffic/PeatonSpawner.cs` (bloque TODO al final):
`Ladron : Peaton` que acecha al jugador en la vereda, le roba una mejora (o
toda la mochila) de `Inventario` y sale corriendo; el refresh de la mochila y
de la herramienta sale del evento `Inventario.Actualizado`.


## Estructura

```
scenes/Menu.tscn           menú principal (slots, opciones)
scenes/Cruce.tscn          escena principal (casi todo se construye por código)
scripts/Core/              GameManager, SaveSystem (slots), Ajustes
scripts/Player/            Player, CameraRig
scripts/Traffic/           TrafficLight, CarSpawner, CarAI, PatrolAI, Peaton, PeatonSpawner
scripts/Work/              IJob, WindshieldJob (JugglingJob/VendorJob = stubs Paso 2-3)
scripts/Economy/           Economy, Inventario, Mejoras (lee precios.json)
scripts/World/             ProcCalle, ProcEdificios, ProcProps, ProcSemaforo, ProcAuto, ProcPeaton
scripts/UI/                HUD, Menu, Pausa
scripts/Audio/             AudioManager (stub, sonidos importados en Paso 6)
data/                      balance.json, precios.json
```

## Roadmap

- [x] Paso 1: cruce gris jugable + semáforo + 1 auto + player FPS/TPS + $ ficticio
- [x] Paso 2: interacción real limpiaparabrisas (progreso + calidad + humor conductor)
- [x] Paso 3: economía + árbol de mejoras (EQUIPO/CALLE/NEGOCIO con requisitos)
- [x] Paso 4: arte procedural low-poly bonito
- [x] Extra: menú Semaphorer + 3 slots + opciones + pausa (+migración de partida vieja)
- [x] Extra: limpiavidrios con skins (esponja/balde) + mochila (I)
- [x] Extra: peatones que cruzan con rojo y miran antes
- [x] Paso 5: ladrones con recuperación (los alcanzás 8s y vuelve lo robado) / clima (lluvia+nublado, +$2 propina) / día-noche (6 min, reloj en HUD) / guardado mundo (autosave 30s + al salir: día/hora/pos/ladrón/policía)
- [x] Paso 6: audio importado (monedas, bocina, sirena, frote, abrir, negado + loops ambiente/lluvia, pitch ±10%, escalerita por propina)
