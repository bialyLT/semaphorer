#!/usr/bin/env bash
# Exporta Semaphorer a Windows (.exe portable) y arma el .zip para compartir.
# Uso: ./tools/exportar_windows.sh
set -e
cd "$(dirname "$0")/.."

PRESET="Windows Desktop"
OUT="build/windows/Semaphorer.exe"
VER=$(grep -oP 'config/version="\K[^"]+' project.godot)
CARPETA="build/Semaphorer-windows-v$VER"
LEEME="build/LEEME.txt"
ZIP="build/Semaphorer-windows-v$VER.zip"

echo "== Semaphorer: export Windows =="

# 1. Chequear templates
TPL_DIR="$HOME/.local/share/godot/export_templates/4.3.stable.mono"
if [ ! -d "$TPL_DIR" ]; then
  echo "FALTA: Export Templates 4.3 mono no instalados."
  echo "1. Baja Godot_v4.3-stable_mono_export_templates.tpz desde:"
  echo "   https://github.com/godotengine/godot/releases/tag/4.3-stable"
  echo "2. En Godot: Editor > Manage Export Templates > Install From File..."
  echo "3. Corre de nuevo este script."
  exit 1
fi

# 2. Compilar C# (con reintentos: el teardown de mono a veces vuelca
# core DESPUÉS de compilar bien; reintentar no cuesta y evita falsos fallos)
echo "-- Compilando C#..."
for i in 1 2 3; do
  if godot --headless --build-solutions --quit --path .; then break; fi
  echo "   build-solutions falló (intento $i/3), reintentando..."
  sleep 2
  if [ "$i" = 3 ]; then echo "ERROR: no se pudo compilar C#."; exit 1; fi
done

# 3. Exportar (carpeta vacia para no mezclar dlls viejas de C#)
echo "-- Exportando $PRESET -> $OUT ..."
rm -rf build/windows
mkdir -p build/windows
godot --headless --path . --export-release "$PRESET" "./$OUT"

echo "-- Resultado:"
ls -lh build/windows/

# 3b. Carpeta versionada con el juego (exe + pck + data_... todo junto)
echo "-- Armando $CARPETA ..."
rm -rf "$CARPETA"
mkdir -p "$CARPETA"
cp "$OUT" "${OUT%.exe}.pck" "$CARPETA/"
cp -r build/windows/data_* "$CARPETA/"

# 3c. Instrucciones para quien juega (AL LADO de la carpeta, no adentro)
cat > "$LEEME" <<'EOF'
SEMAPHORER - como jugar
=======================
1. Descomprimi TODO el contenido del zip manteniendo la estructura:
     Semaphorer-windows-vX.Y.Z/   <- carpeta del juego (version en el nombre)
       Semaphorer.exe
       Semaphorer.pck
       data_SimuladorSemaforo_windows_x86_64/
     LEEME.txt                    <- este archivo, al lado de la carpeta

2. Doble-click a Semaphorer.exe adentro de la carpeta (Windows 10/11 64-bit).
   No necesita instalar Godot ni .NET.

3. Si Windows Defender avisa (app no firmada): "Mas informacion > Ejecutar de todas formas".

Controles: WASD moverse, Mouse mirar, V camara 1a/3a, E trabajar segun tu oficio (en rojo), F mejoras, G escondite, I mochila, Espacio saltar, Esc pausa.
Musica: menu tranquilo + partida con ritmo (volumen maestro en Opciones).
EOF

# 4. Zip portable (carpeta del juego + LEEME al lado, a la raiz del zip)
echo "-- Armando $ZIP ..."
rm -f "$ZIP"
cd build && zip -r "Semaphorer-windows-v$VER.zip" "Semaphorer-windows-v$VER" LEEME.txt && cd ..
ls -lh "$ZIP"

echo ""
echo "LISTO. Comparti $ZIP."
echo "Quien lo reciba: descomprimir TODO junto y doble-click a Semaphorer.exe (Win 10/11 64-bit, sin instalar nada)."
