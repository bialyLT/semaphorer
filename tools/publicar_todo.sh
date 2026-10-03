#!/usr/bin/env bash
# Pipeline completo de publicación. Se corre DESPUÉS de hacer el cambio en el
# juego con su bump de versión (skill versionado). Orden:
#   1. verifica que compila · 2. commit+push juego · 3. tag · 4. exports
#   5. release en GitHub · 6. actualiza JSONs de la web · 7. push web
#   8. homepages · 9. verifica links de descarga.
# Uso: ./tools/publicar_todo.sh
# La PRIMERA corrida muestra el texto a publicar y se pausa (no escribe
# nada): revisarlo con el usuario y correr de nuevo con RELEASE_OK=1.
# Env: WEB_REPO (default: ../semaphorer-web),
#      WEB_URL (default: https://bialyLT.github.io/semaphorer-web)
#      RELEASE_OK=1 (confirma el texto de la release, ya aprobado)
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

# 1b. Puerta de textos: el usuario aprueba el lenguaje de jugador ANTES de
# que se escriba/publique en cualquier lado (release, JSONs, web).
echo "-- [1b/9] Texto a publicar en la release $TAG:"
awk "/^## \[$VER\]/{f=1;next}/^## \[/{f=0}f" CHANGELOG.md
python3 tools/actualizar_web.py --solo-mostrar "$VER"
if [ "${RELEASE_OK:-}" != "1" ]; then
  echo "Revisá el texto con el usuario. Si está OK, corré de nuevo con RELEASE_OK=1."
  exit 0
fi

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

# 4. Exports de las dos plataformas (sin tragar el output: si un export
# falla tiene que verse y cortar aca, no seguir hasta la release).
echo "-- [4/9] Export Windows..."
./tools/exportar_windows.sh
echo "-- [4/9] Export Linux..."
./tools/exportar_linux.sh
# Fail fast: sin los 2 zips no hay release que valga (en v1.3.1 el de
# Windows fallo en silencio y el paso 5 murio con un "no matches found").
for z in "$ZIP_WIN" "$ZIP_LIN"; do
  if [ ! -f "$z" ]; then
    echo "ERROR: no se genero $z; revisar el export de arriba."
    exit 1
  fi
  echo "   $(basename "$z") listo ($(du -h "$z" | cut -f1))."
done

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
# La web es una app Vite+React: los datos viven en public/data (la app
# los lee de ahi y Vercel sirve dist/ tras buildear). Se sincronizan SOLO
# los JSON: copiar todo landing-web pisaria el index.html entry de Vite
# (id="root") y romperia el build (paso en v1.3.1, fix en 8abc40e).
echo "-- [7/9] Push de la web..."
if [ ! -d "$WEB_REPO/.git" ]; then
  gh repo clone "$REPO_WEB" "$WEB_REPO"
fi
if [ ! -d "$WEB_REPO/public/data" ]; then
  echo "ERROR: $WEB_REPO/public/data no existe; no toco la web (revisar layout)."
  exit 1
fi
cp landing-web/data/versiones.json landing-web/data/releases.json "$WEB_REPO/public/data/"
# Guardarraíles: entry Vite intacto y versión publicada presente en datos.
if ! grep -q 'id="root"' "$WEB_REPO/index.html"; then
  echo "ERROR: $WEB_REPO/index.html no es el entry de Vite; no pusheo la web."
  exit 1
fi
if ! grep -q "\"version\": \"$VER\"" "$WEB_REPO/public/data/versiones.json"; then
  echo "ERROR: v$VER no esta en public/data/versiones.json; no pusheo la web."
  exit 1
fi
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

# 9. Links de descarga vivos (assert, no solo imprimir: un 404 aca
# significa release incompleta y el "LISTO" final no debe salir).
echo "-- [9/9] Verificando descargas..."
FALLO=0
for z in "$(basename "$ZIP_WIN")" "$(basename "$ZIP_LIN")"; do
  code=$(curl -sI -o /dev/null -w "%{http_code}" "https://github.com/$REPO_JUEGO/releases/download/$TAG/$z")
  echo "   $z -> HTTP $code"
  if [ "$code" != "200" ] && [ "$code" != "302" ]; then
    echo "ERROR: $z no resuelve (HTTP $code)."
    FALLO=1
  fi
done
if [ "$FALLO" != "0" ]; then
  exit 1
fi

echo ""
echo "LISTO v$VER: juego + release + web publicados."
