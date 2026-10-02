#!/usr/bin/env bash
# Sube la release a GitHub (repo semaphorer) con el zip de Windows.
# Uso: ./tools/subir_release.sh [version]   (default: la de project.godot)
# Requiere haber corrido ./tools/exportar_windows.sh antes.
# Si tenés `gh` instalado crea la release sola; si no, te dice los pasos.
set -e
cd "$(dirname "$0")/.."

VER="${1:-$(grep -oP 'config/version="\K[^"]+' project.godot)}"
TAG="v$VER"
ZIP="build/Semaphorer-windows-v$VER.zip"

[ -f "$ZIP" ] || { echo "FALTA $ZIP. Primero: ./tools/exportar_windows.sh"; exit 1; }

# Notas desde el CHANGELOG (sección de esta versión).
NOTAS=$(awk "/^## \[$VER\]/{f=1;next}/^## \[/{f=0}f" CHANGELOG.md)
[ -n "$NOTAS" ] || NOTAS="Semaphorer $TAG"

# Tag + push del tag.
if git rev-parse "$TAG" >/dev/null 2>&1; then
  echo "-- tag $TAG ya existe, no lo recreo."
else
  git tag -a "$TAG" -m "Semaphorer $TAG"
fi
git push origin "$TAG"

# Release con los zips (Windows siempre; Linux si se exportó).
if command -v gh >/dev/null 2>&1; then
  echo "-- Creando release $TAG en GitHub..."
  ZIPS=("$ZIP")
  [ -f "build/Semaphorer-linux-v$VER.zip" ] && ZIPS+=("build/Semaphorer-linux-v$VER.zip")
  gh release create "$TAG" "${ZIPS[@]}" --title "Semaphorer $TAG" --notes "$NOTAS"
  echo "LISTO: https://github.com/bialyLT/semaphorer/releases/tag/$TAG"
else
  echo ""
  echo "No tenés 'gh'. Terminá a mano en 2 pasos:"
  echo "1. https://github.com/bialyLT/semaphorer/releases/new?tag=$TAG&title=Semaphorer+$TAG"
  echo "2. Pegá estas notas y arrastrá $ZIP:"
  echo "---"
  echo "$NOTAS"
fi
