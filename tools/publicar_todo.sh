#!/usr/bin/env bash
# Pipeline completo de publicación. Se corre DESPUÉS de hacer el cambio en el
# juego con su bump de versión (skill versionado). Orden:
#   1. verifica que compila · 2. commit+push juego · 3. tag · 4. exports
#   5. release en GitHub · 6. actualiza JSONs de la web · 7. push web
#   8. homepages · 9. verifica links de descarga.
# Uso: ./tools/publicar_todo.sh
# Env: WEB_REPO (default: ../semaphorer-web),
#      WEB_URL (default: https://bialyLT.github.io/semaphorer-web)
set -e
cd "$(dirname "$0")/.."

VER=$(grep -oP 'config/version="\K[^"]+' project.godot)
TAG="v$VER"
REPO_JUEGO="bialyLT/semaphorer"
REPO_WEB="bialyLT/semaphorer-web"
WEB_REPO="${WEB_REPO:-../semaphorer-web}"
WEB_URL="${WEB_URL:-https://bialyLT.github.io/semaphorer-web}"
ZIP_WIN="build/Semaphorer-windows-v$VER.zip"
ZIP_LIN="build/Semaphorer-linux-v$VER.zip"

echo "== Publicando Semaphorer v$VER =="

# 1. Compila o no sale nada.
echo "-- [1/9] Compilando C#..."
dotnet build SimuladorSemaforo.csproj --nologo -v q 2>&1 | tail -2

# 2. Cambios del juego al remoto.
echo "-- [2/9] Push del juego..."
git add -A
if git diff --cached --quiet; then
  echo "   sin cambios para commitear."
else
  git commit -q -m "Semaphorer v$VER"
fi
git push origin master

# 3. Tag de la versión.
echo "-- [3/9] Tag $TAG..."
git rev-parse "$TAG" >/dev/null 2>&1 || git tag -a "$TAG" -m "Semaphorer $TAG"
git push origin "$TAG"

# 4. Exports de las dos plataformas.
echo "-- [4/9] Export Windows..."
./tools/exportar_windows.sh >/dev/null 2>&1 && echo "   $(basename $ZIP_WIN) listo."
echo "-- [4/9] Export Linux..."
./tools/exportar_linux.sh >/dev/null 2>&1 && echo "   $(basename $ZIP_LIN) listo."

# 5. Release en GitHub con ambos zips.
echo "-- [5/9] Release $TAG..."
NOTAS=$(awk "/^## \[$VER\]/{f=1;next}/^## \[/{f=0}f" CHANGELOG.md)
if gh release view "$TAG" >/dev/null 2>&1; then
  gh release upload "$TAG" "$ZIP_WIN" "$ZIP_LIN" --clobber
else
  gh release create "$TAG" "$ZIP_WIN" "$ZIP_LIN" --title "Semaphorer $TAG" --notes "$NOTAS"
fi

# 6. JSONs de la web con URLs y tamaños reales.
echo "-- [6/9] Actualizando datos de la web..."
python3 tools/actualizar_web.py "$VER"

# 7. Web al remoto (clona si es la primera vez).
echo "-- [7/9] Push de la web..."
if [ ! -d "$WEB_REPO/.git" ]; then
  gh repo clone "$REPO_WEB" "$WEB_REPO"
fi
cp -r landing-web/. "$WEB_REPO"/
git -C "$WEB_REPO" add -A
if git -C "$WEB_REPO" diff --cached --quiet; then
  echo "   web sin cambios."
else
  git -C "$WEB_REPO" commit -q -m "Web v$VER: descargas y releases"
  git -C "$WEB_REPO" push
fi

# 8. Homepages linkeadas (idempotente).
echo "-- [8/9] Linkeando repos con la web..."
gh repo edit "$REPO_JUEGO" --homepage "$WEB_URL" >/dev/null 2>&1 || true
gh repo edit "$REPO_WEB" --homepage "$WEB_URL" >/dev/null 2>&1 || true

# 9. Links de descarga vivos.
echo "-- [9/9] Verificando descargas..."
for z in "$(basename $ZIP_WIN)" "$(basename $ZIP_LIN)"; do
  code=$(curl -sI -o /dev/null -w "%{http_code}" "https://github.com/$REPO_JUEGO/releases/download/$TAG/$z")
  echo "   $z -> HTTP $code"
done

echo ""
echo "LISTO v$VER: juego + release + web publicados."
