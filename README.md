# Semaphorer

Simulador 3D (primera y tercera persona) de laburo en el semáforo: limpiavidrios,
malabares o venta ambulante en un cruce latino. Cobrá en rojo, guardá la plata
en la mochila azul y que no te agarre la policía.

Hecho con Godot 4 (.NET / C#), todo procedural por código.

## Descargar

Última versión para Windows en
[Releases](https://github.com/bialyLT/semaphorer/releases): descomprimí el zip
y doble-click a `Semaphorer.exe` (no necesita instalación). Novedades,
historial y todas las descargas en la
[web del juego](https://bialyLT.github.io/semaphorer-web).

## Cómo se juega

Un tipo te ofrece una última tirada de la suerte y te toca un oficio. Después,
un tutorial jugable te enseña haciendo: bajar a la calle, trabajar un auto en
rojo, cobrar, volver a la vereda, guardar en la mochila y ver tus mejoras.

- Los conductores tienen humor: los amables dan propina, los apurados se van si
  tardás y los enojados pueden no pagar. En verde se pierde el progreso.
- La policía vigila la calzada: si te ve demasiado tiempo, decomisa lo que
  llevás encima. Lo guardado en la mochila está a salvo (pero ojo con el ladrón:
  miralo para espantarlo).
- Con lo que ganás comprás mejoras (equipo, calle, negocio) y juntás para el
  pasaje a la próxima ciudad.

| Tecla | Acción |
|---|---|
| WASD / flechas | Moverse |
| Mouse | Mirar (click para capturar) |
| E | Trabajar junto al auto en rojo |
| Espacio | Saltar (subir a la vereda) |
| F | Tienda de mejoras |
| G | Guardar/sacar plata en la mochila |
| I | Ver mochila |
| V | Cambiar cámara |
| Esc | Pausa |

## Desarrollo

Requisitos: [Godot 4.3+ versión .NET](https://godotengine.org/download)
(pestaña `.NET`, no la classic) y [.NET SDK 8](https://dotnet.microsoft.com/download).

```bash
# Compilar
dotnet build SimuladorSemaforo.csproj

# Exportar Windows (pide templates 4.3 mono instalados)
./tools/exportar_windows.sh

# Música y sonidos (se regeneran; reemplazables por grabaciones reales)
python3 tools/generar_audio.py
```

Estructura: `scenes/` (menú, cruce) · `scripts/` (juego en C#) · `data/`
(balance, precios, oficios) · `audio/` (wavs) · `tools/` (export, releases).

## Versiones

Ver [CHANGELOG.md](CHANGELOG.md). Las descargas de cada versión están en
[Releases](https://github.com/bialyLT/semaphorer/releases).
