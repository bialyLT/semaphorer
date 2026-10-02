#!/usr/bin/env python3
"""Actualiza landing-web/data/*.json para una versión (lo llama publicar_todo.sh).

- versiones.json: agrega entradas Windows/Linux con URL de la release, tamaño
  real del zip y destacado solo en la nueva (si los zips existen en build/).
- releases.json: agrega la entrada desde la sección del CHANGELOG.md.
Es idempotente: si la versión ya está, no duplica.
"""
import json
import math
import os
import re
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = "bialyLT/semaphorer"
PLATS = [
    ("windows", "Windows 10/11 64-bit"),
    ("linux", "Linux 64-bit"),
]


def version_actual():
    with open(os.path.join(RAIZ, "project.godot"), encoding="utf-8") as f:
        m = re.search(r'config/version="([^"]+)"', f.read())
    return m.group(1)


def seccion_changelog(ver):
    fecha, cambios, actual = "", [], None
    with open(os.path.join(RAIZ, "CHANGELOG.md"), encoding="utf-8") as f:
        for linea in f:
            m = re.match(r"## \[(.+?)\] - (\d{4}-\d{2}-\d{2})", linea)
            if m:
                if m.group(1) == ver:
                    fecha = m.group(2)
                    actual = True
                elif actual:
                    break
                continue
            if not actual:
                continue
            m2 = re.match(r"### (\w+)", linea)
            if m2:
                actual = m2.group(1)
                continue
            m3 = re.match(r"- (.+)", linea)
            if m3 and isinstance(actual, str):
                cambios.append({"tipo": actual, "texto": m3.group(1).strip()})
    return fecha, cambios


def cargar(path, defecto):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    return defecto


def guardar(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def main():
    ver = sys.argv[1] if len(sys.argv) > 1 else version_actual()
    data_dir = os.path.join(RAIZ, "landing-web", "data")
    fecha, cambios = seccion_changelog(ver)
    if not fecha:
        print(f"CHANGELOG sin entrada para {ver}, no toco la web.")
        return

    # releases.json: todas las releases visibles, nueva primero.
    rel_path = os.path.join(data_dir, "releases.json")
    releases = cargar(rel_path, [])
    if not any(r.get("version") == ver for r in releases):
        releases.insert(0, {"version": ver, "fecha": fecha, "cambios": cambios})
        guardar(rel_path, releases)
        print(f"releases.json: agregada v{ver} ({len(cambios)} cambios).")
    else:
        print(f"releases.json: v{ver} ya estaba.")

    # versiones.json: una entrada por plataforma con zip existente.
    ver_path = os.path.join(data_dir, "versiones.json")
    versiones = cargar(ver_path, [])
    for plat, nombre in PLATS:
        archivo = f"Semaphorer-{plat}-v{ver}.zip"
        ruta_zip = os.path.join(RAIZ, "build", archivo)
        if not os.path.exists(ruta_zip):
            print(f"versiones.json: sin {archivo}, salto {nombre}.")
            continue
        if any(v.get("version") == ver and v.get("plataforma") == nombre for v in versiones):
            print(f"versiones.json: v{ver} {nombre} ya estaba.")
            continue
        for v in versiones:
            v["destacado"] = False
        mb = math.ceil(os.path.getsize(ruta_zip) / 1024 / 1024)
        versiones.append({
            "version": ver,
            "fecha": fecha,
            "plataforma": nombre,
            "archivo": archivo,
            "url": f"https://github.com/{REPO}/releases/download/v{ver}/{archivo}",
            "tamanio_mb": mb,
            "notas": cambios[0]["texto"] if cambios else "",
            "destacado": plat == "windows",
        })
        print(f"versiones.json: agregada v{ver} {nombre} ({mb} MB).")
    guardar(ver_path, versiones)


main()
