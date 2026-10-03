#!/usr/bin/env bash
# Pruebas headless de cinemáticas (dev-only).
# Uso: ./tools/probar_cinematicas.sh
# Corre: menú + viaje (skip y completo) + final (piezas y orquestación),
# y valida re-skin garantizado, millón forzado, final_visto y cero errores.
# El test del final escribe final_visto en el slot 1: se backupea y restaura.
set -u
cd "$(dirname "$0")/.."
FALLO=0
UDATA="$HOME/.local/share/godot/app_userdata/Semaphorer"
BK="/tmp/backup_final_test"

echo "== [0/3] Compilando soluciones..."
godot --headless --build-solutions --quit --path . >/dev/null 2>&1 || true

echo "== [1/3] Menú 120 frames..."
OUT=$(timeout 90 godot --headless --path . --quit-after 120 2>&1)
if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
  echo "FALLA menú: hay errores de script."; FALLO=1
else
  echo "OK menú sin errores."
fi

echo "== [2/3] Viaje con hold de Esc (1.5s para saltar)..."
OUT=$(timeout 90 godot --headless --fixed-fps 60 --path . res://tools/TestViaje.tscn 2>&1)
echo "$OUT" | grep "TEST" || true
if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
  echo "FALLA skip: hay errores de script."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST pre-skip reskin=False"; then
  echo "FALLA skip: orden inesperado."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST sigue viva=True"; then
  echo "FALLA skip: un toque corto salta (debería pedir hold)."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST garantia reskin"; then
  echo "FALLA skip: sin garantía pre-negro."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST fin .* reskin=True viva=False"; then
  echo "FALLA skip: no saltó con hold o no liberó."; FALLO=1
else
  echo "OK skip: toque no salta, hold sí, re-skin garantizado."
fi

echo "== [3/3] Viaje completo (10.5s de corrido)..."
OUT=$(timeout 150 godot --headless --fixed-fps 60 --path . res://tools/TestViaje.tscn -- --full 2>&1)
echo "$OUT" | grep "TEST" || true
if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
  echo "FALLA full: hay errores de script."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST reskin ejecutado"; then
  echo "FALLA full: el re-skin bajo negro no corrió."; FALLO=1
elif ! echo "$OUT" | grep -q "reskin=True viva=False"; then
  echo "FALLA full: no terminó y liberó sola."; FALLO=1
else
  echo "OK full: terminó sola y liberó."
fi

echo ""
if [ "$FALLO" != "0" ]; then echo "PRUEBAS: FALLA"; exit 1; fi
echo "PRUEBAS viaje+menú: PASA (3/3)"

echo "== [4/4] Final (dormitorio + diálogo + millón + orquestación)..."
rm -rf "$BK"; mkdir -p "$BK"
HABIA=0
if [ -f "$UDATA/partida1.cfg" ]; then cp "$UDATA/partida1.cfg" "$BK/"; HABIA=1; fi
# Si el slot 1 se tocó hace poco, alguien está jugando: no lo piso.
EDAD=$(( $(date +%s) - $(stat -c %Y "$UDATA/partida1.cfg" 2>/dev/null || echo 0) ))
if [ "$EDAD" -lt 180 ]; then
  echo "   slot 1 en uso reciente: omito test del final (juego abierto?)."
else
  OUT=$(timeout 180 godot --headless --fixed-fps 60 --path . res://tools/TestFinal.tscn 2>&1)
  echo "$OUT" | grep "TEST-FINAL" || true
  VISTO="no"
  if grep -q "final_visto=true" "$UDATA/partida1.cfg" 2>/dev/null; then VISTO="si"; fi
  if [ "$HABIA" = 1 ]; then cp "$BK/partida1.cfg" "$UDATA/"; else rm -f "$UDATA/partida1.cfg"; fi
  echo "   final_visto escrito durante el test: $VISTO (slot 1 restaurado)"
  if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
    echo "FALLA final: hay errores de script."; FALLO=1
  elif ! echo "$OUT" | grep -q "TEST-FINAL dialogo OK"; then
    echo "FALLA final: el diálogo no avanzó."; FALLO=1
  elif ! echo "$OUT" | grep -q "TEST-FINAL tirada OK millon"; then
    echo "FALLA final: no salió el millón."; FALLO=1
  elif ! echo "$OUT" | grep -q "TEST-FINAL orquesta OK visto=True"; then
    echo "FALLA final: la orquestación no completó."; FALLO=1
  elif ! echo "$OUT" | grep -q "volvio=True"; then
    echo "FALLA final: el jugador no volvió a su posición."; FALLO=1
  elif [ "$VISTO" != "si" ]; then
    echo "FALLA final: no se guardó final_visto."; FALLO=1
  else
    echo "OK final: piezas + millón + sueño cumplido guardado."
  fi
fi

echo ""
if [ "$FALLO" != "0" ]; then echo "PRUEBAS: FALLA"; exit 1; fi
echo "PRUEBAS cinemáticas: PASA (4/4)"

echo "== [5/5] Peatones (4 al mismo pie de senda, 10s)..."
OUT=$(LC_ALL=C timeout 120 godot --headless --fixed-fps 60 --path . res://tools/TestPeatones.tscn 2>&1)
echo "$OUT" | grep "TEST-PEATON" || true
MIN600=$(echo "$OUT" | grep "TEST-PEATON f600" | sed 's/.*min=//')
VIVOS=$(echo "$OUT" | grep "TEST-PEATON f600" | sed 's/.*vivos=//;s/ min=.*//')
if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
  echo "FALLA peatones: hay errores de script."; FALLO=1
elif [ "$VIVOS" != "4" ]; then
  echo "FALLA peatones: se perdió alguno (vivos=$VIVOS)."; FALLO=1
elif ! awk "BEGIN{exit !($MIN600 >= 0.40)}"; then
  echo "FALLA peatones: se enciman (min=$MIN600)."; FALLO=1
else
  echo "OK peatones: distancia mínima $MIN600 m."
fi

echo ""
if [ "$FALLO" != "0" ]; then echo "PRUEBAS: FALLA"; exit 1; fi
echo "PRUEBAS mundo: PASA (5/5)"

echo "== [6/6] Límites (cuerpo tipo-jugador contra el este)..."
OUT=$(LC_ALL=C timeout 90 godot --headless --fixed-fps 60 --path . res://tools/TestLimites.tscn 2>&1)
echo "$OUT" | grep "TEST-LIMITE" || true
XFIN=$(echo "$OUT" | grep "TEST-LIMITE fin" | sed 's/.*x=//;s/ y=.*//')
if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
  echo "FALLA límites: hay errores de script."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST-LIMITE clamp a=(17.5, 0.5, -17.5)"; then
  echo "FALLA límites: el clamp no trae adentro."; FALLO=1
elif ! awk "BEGIN{exit !(($XFIN > 15) && ($XFIN <= 18.5))}"; then
  echo "FALLA límites: se escapó del mundo (x=$XFIN)."; FALLO=1
else
  echo "OK límites: frenó en x=$XFIN."
fi

echo ""
if [ "$FALLO" != "0" ]; then echo "PRUEBAS: FALLA"; exit 1; fi
echo "PRUEBAS mundo: PASA (6/6)"

echo "== [7/7] Prólogo (jugador oculto + oficio jugable)..."
OUT=$(LC_ALL=C timeout 90 godot --headless --fixed-fps 60 --path . res://tools/TestPrologo.tscn 2>&1)
echo "$OUT" | grep "TEST-PROLOGO" || true
if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
  echo "FALLA prólogo: hay errores de script."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST-PROLOGO oculta=True"; then
  echo "FALLA prólogo: el jugador no se ocultó."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST-PROLOGO fin oficio=.* jugable=True visible=True"; then
  echo "FALLA prólogo: oficio inválido o no volvió."; FALLO=1
else
  echo "OK prólogo: oculto durante la tirada, volvió con oficio."
fi

echo ""
if [ "$FALLO" != "0" ]; then echo "PRUEBAS: FALLA"; exit 1; fi
echo "PRUEBAS mundo: PASA (7/7)"

echo "== [8/8] Modal borrar (abre, cancela, Esc)..."
OUT=$(LC_ALL=C timeout 90 godot --headless --fixed-fps 60 --path . res://tools/TestMenu.tscn 2>&1)
echo "$OUT" | grep "TEST-MENU" || true
if echo "$OUT" | grep -q "SCRIPT ERROR\|Parse Error"; then
  echo "FALLA modal: hay errores de script."; FALLO=1
elif echo "$OUT" | grep -q "TEST-MENU omitido"; then
  echo "OK modal: omitido (sin partidas)."
elif ! echo "$OUT" | grep -q "TEST-MENU abre=True"; then
  echo "FALLA modal: no abrió con X."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST-MENU cancela=True"; then
  echo "FALLA modal: no cerró con Cancelar."; FALLO=1
elif ! echo "$OUT" | grep -q "TEST-MENU esc=True"; then
  echo "FALLA modal: no cerró con Esc."; FALLO=1
else
  echo "OK modal: abre, cancela y Esc."
fi

echo ""
if [ "$FALLO" != "0" ]; then echo "PRUEBAS: FALLA"; exit 1; fi
echo "PRUEBAS: PASA (8/8)"
