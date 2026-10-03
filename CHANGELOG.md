# Changelog — Semaphorer

Formato: Keep a Changelog. Versiones: SemVer (`MAJOR.MINOR.PATCH`).
Ver skill `versionado` para las reglas de bump de este proyecto.

## [1.6.0] - 2026-10-03

### Added
- Nuevo minimapa en la esquina: muestra dónde estás, y con la mejora Mapa del cruce también qué semáforo está en verde.

## [1.5.10] - 2026-10-03

### Fixed
- Los semáforos ahora son más cortos: el verde dura 12 segundos.

## [1.5.9] - 2026-10-03

### Fixed
- Límite invisible del jugador ampliado y configurado dinámicamente.

## [1.5.8] - 2026-10-03

### Fixed
- La pantalla completa ya no se sale al empezar una partida nueva.

## [1.5.7] - 2026-10-03

### Fixed
- Las cinemáticas ahora se saltean manteniendo presionado.

## [1.5.6] - 2026-10-03

### Fixed
- Borrar una partida ahora pide confirmación.

## [1.5.5] - 2026-10-03

### Fixed
- Las partidas borradas ya no vuelven a aparecer.

## [1.5.4] - 2026-10-03

### Fixed
- Ya no podés caerte del mundo.

## [1.5.3] - 2026-10-03

### Fixed
- Los peatones ya no se enciman al caminar.

## [1.5.2] - 2026-10-03

### Fixed
- Las cinemáticas ahora se ven sin objetos delante de los personajes.

## [1.5.1] - 2026-10-03

### Changed
- Cambio en diálogos de la cinemática final.

## [1.5.0] - 2026-10-03

### Added
- Se agregaron las cinemáticas del final del juego.

## [1.4.0] - 2026-10-03

### Added
- Se agregó una cinemática cada vez que viajás a una nueva ciudad.

## [1.3.1] - 2026-10-02

### Added
- Se agregó una nueva cinemática en la intro del juego.

## [1.3.0] - 2026-10-02

### Added
- Cinemática intro básica en partidas nuevas: travelling de cámara en 3
  planos sobre el cruce + subtítulos ("apuestas / calle / última tirada"),
  con letterbox, fundido y typewriter. Skipeable con Esc/Espacio/Enter/click
  y cierra en la ruleta de la suerte como plano final.

## [1.2.0] - 2026-10-02

### Added
- Música de fondo: tema tranquilo en el menú (`musica_menu`) y tema con
  ritmo en la partida (`musica_juego`), en loop sin cortes. Se generan con
  `python3 tools/generar_audio.py` y se reemplazan por grabaciones reales con
  el mismo nombre sin tocar código. El volumen maestro las controla.

### Fixed
- Ambiente y lluvia ahora loopean de verdad (antes sonaban una sola vez:
  los `.import` vienen sin loop y nadie lo configuraba por código).

## [1.1.1] - 2026-10-02

### Fixed
- Texto del prólogo: ahora dice "un tipo te ofrece una última tirada de
  la suerte" (antes "el Tipo te ofrece LA ÚLTIMA TIRADA", que sonaba a
  jerga interna).

## [1.1.0] - 2026-10-02

### Added
- Tutorial jugable por objetivos: toast inferior sin pausar el juego, cada
  paso se valida haciendo (bajar a la calle, auto frenado en rojo, cobrar
  con E, subir a la vereda con Espacio, guardar con G, ver mejoras con F)
  y cierra avisando que el tutorial completo está en el menú de inicio.
- Flecha flotante "▼ MOCHILA AZUL" sobre el escondite durante el paso de
  guardar; la policía no acumula causa mientras dura el tutorial.

## [1.0.2] - 2026-10-02

### Fixed
- Mensajes por oficio: cada profesión muestra solo los suyos (limpiavidrios
  "Limpiando.../¡Vidrio limpio!", malabarista "Función.../¡Función completa!",
  vendedor "Ofreciendo.../¡Vendido!"). El verde lo procesa una sola vez el
  limpiaparabrisas (antes los 3 oficios triplicaban avisos).
- Ayuda inicial y tutorial paso 1 según tu oficio (antes decían "limpiar"
  para todos); ayuda del menú neutral ("E trabajar").

## [1.0.1] - 2026-10-02

### Fixed
- Tirada de la suerte: eliminados los avisos de "oficio en construcción
  (Fase 3)" al tocar malabarista o vendedor. Los 3 oficios ya son jugables
  (`HistoriaUI`, `GameManager` ahora muestran nombre + descripción del oficio).

### Added
- Versión visible del juego: `VersionJuego.Actual`, `config/version` en
  `project.godot`, `file_version`/`product_version` en `export_presets.cfg`
  y label `v1.0.1` en Menú y Pausa.
