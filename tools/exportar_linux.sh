#!/usr/bin/env bash
# Exporta Semaphorer a Linux (.x86_64 portable) y arma el .zip para compartir.
# Uso: ./tools/exportar_linux.sh
set -e
cd "$(dirname "$0")/.."

PRESET="Linux/X11"
OUT="build/linux/Semaphorer.x86_64"
VER=$(grep -oP 'config/version="\K[^"]+' project.godot)
CARPETA="build/Semaphorer-linux-v$VER"
LEEME="build/LEEME-linux.txt"
ZIP="build/Semaphorer-linux-v$VER.zip"

echo "== Semaphorer: export Linux =="

# 1. Chequear templates
TPL_DIR="$HOME/.local/share/godot/export_templates/4.3.stable.mono"
if [ ! -d "$TPL_DIR" ]; then
  echo "FALTA: Export Templates 4.3 mono no instalados."
  exit 1
fi

# 2. Compilar C#
echo "-- Compilando C#..."
godot --headless --build-solutions --quit --path .

# 3. Exportar (carpeta vacia para no mezclar dlls viejas de C#)
echo "-- Exportando $PRESET -> $OUT ..."
rm -rf build/linux
mkdir -p build/linux
godot --headless --path . --export-release "$PRESET" "./$OUT"

echo "-- Resultado:"
ls -lh build/linux/

# 3b. Carpeta versionada con el juego (binario + pck + data_... todo junto)
echo "-- Armando $CARPETA ..."
rm -rf "$CARPETA"
mkdir -p "$CARPETA"
cp -r build/linux/. "$CARPETA/"
chmod +x "$CARPETA/Semaphorer.x86_64"

# 3c. Instrucciones para quien juega (AL LADO de la carpeta, no adentro)
cat > "$LEEME" <<'EOF'
SEMAPHORER - como jugar (Linux)
===============================
1. Descomprimi TODO el contenido del zip manteniendo la estructura:
     Semaphorer-linux-vX.Y.Z/   <- carpeta del juego (version en el nombre)
       Semaphorer.x86_64
       Semaphorer.pck
       ...
     LEEME-linux.txt            <- este archivo, al lado de la carpeta

2. Adentro de la carpeta, corré: ./Semaphorer.x86_64 (Linux 64-bit).
   No necesita instalar Godot ni .NET (si falta permiso: chmod +x Semaphorer.x86_64).

Controles: WASD moverse, Mouse mirar, V camara 1a/3a, E trabajar segun tu oficio (en rojo), F mejoras, G escondite, I mochila, Espacio saltar, Esc pausa.
Musica: menu tranquilo + partida con ritmo (volumen maestro en Opciones).
EOF

# 4. Zip portable (carpeta del juego + LEEME al lado, a la raiz del zip)
echo "-- Armando $ZIP ..."
rm -f "$ZIP"
cd build && zip -r "Semaphorer-linux-v$VER.zip" "Semaphorer-linux-v$VER" LEEME-linux.txt && cd ..
ls -lh "$ZIP"

echo ""
echo "LISTO. Comparti $ZIP."
