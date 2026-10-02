using Godot;
using System.Collections.Generic;

/// <summary>
/// Bloque B: pilita de pantallas + contador de pausa.
/// Antes cada pantalla hacía GetTree().Paused=true/false por su cuenta:
/// con Historia+Tutorial apilados, cerrar uno despausaba aunque quedara
/// otro abierto. Ahora se pide/suelta y solo se despausa en cero.
/// </summary>
public static class UiPila
{
	static int pausas;
	static readonly Stack<string> pila = new();

	public static void PedirPausa(SceneTree? arbol, string quien)
	{
		pila.Push(quien);
		pausas++;
		if (arbol != null) arbol.Paused = true;
	}

	public static void SoltarPausa(SceneTree? arbol, string quien)
	{
		// Saca hasta encontrar al dueño (si se cerró en desorden no se traba).
		var temp = new Stack<string>();
		while (pila.Count > 0 && pila.Peek() != quien) temp.Push(pila.Pop());
		if (pila.Count > 0) pila.Pop();
		while (temp.Count > 0) pila.Push(temp.Pop());
		pausas = Mathf.Max(0, pausas - 1);
		if (pausas == 0 && arbol != null) arbol.Paused = false;
	}

	public static void SoltarTodo(SceneTree? arbol)
	{
		pila.Clear();
		pausas = 0;
		if (arbol != null) arbol.Paused = false;
	}
}
