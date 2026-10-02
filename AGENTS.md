# Instrucciones del proyecto — Semaphorer

- Después de CADA modificación que toque el juego (features, fixes, balanceo,
  textos/mensajes, audio, visual con cambio de comportamiento, build/export),
  aplicar SIEMPRE la skill `versionado` antes de dar por terminado: clasificar
  (major/minor/patch), anunciar el bump y actualizar los 4 lugares juntos
  (`scripts/Core/VersionJuego.cs`, `project.godot`, `export_presets.cfg`,
  `CHANGELOG.md`).
- Solo omitir el bump si el cambio es puramente interno (docs, comentarios,
  tooling) y decirlo explícitamente.
- No commitear ni taggear: solo modificar archivos.
