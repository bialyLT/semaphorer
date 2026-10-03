using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Salto con confirmación: hay que MANTENER Espacio/Esc/Enter 1.5s para
/// saltear (un toque sin querer ya no salta nada). El orquestador le pasa
/// presionado/soltado en _Input, lo avanza en _Process y muestra Barra().
/// </summary>
public class SaltoHold
{
	public const float Segundos = 1.5f;
	static readonly Key[] Valid = { Key.Escape, Key.Space, Key.Enter, Key.KpEnter };

	readonly HashSet<Key> teclas = new();
	public float Progreso { get; private set; }
	public bool Manteniendo => teclas.Count > 0;

	public void AlPresionar(Key k)
	{
		if (Array.IndexOf(Valid, k) >= 0) teclas.Add(k);
	}

	public void AlSoltar(Key k) => teclas.Remove(k);

	/// <summary>Avanza el hold; devuelve true el frame que se completa.</summary>
	public bool Procesar(float delta)
	{
		if (teclas.Count > 0) Progreso = Mathf.Min(1f, Progreso + delta / Segundos);
		else Progreso = Mathf.Max(0f, Progreso - delta * 3f);
		return Progreso >= 1f;
	}

	public string Barra()
	{
		int n = Mathf.Clamp((int)(Progreso * 10f), 0, 10);
		return "[" + new string('#', n) + new string('-', 10 - n) + "]";
	}

	public void Resetear()
	{
		Progreso = 0;
		teclas.Clear();
	}
}
