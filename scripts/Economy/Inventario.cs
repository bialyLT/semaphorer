using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Paso 3: inventario del jugador (nivel por item de la tienda).
/// Persiste en user://savegame.cfg junto a las monedas ("id:nivel").
/// </summary>
public partial class Inventario : Node
{
	[Export] public Economy? Economia;

	/// <summary>Se emite al terminar de cargar los niveles del save
	/// (el Player arranca antes y debe refrescar su herramienta acá).</summary>
	public event Action? Cargado;

	/// <summary>Se emite cuando cambian los niveles (compra o decomiso):
	/// hay que rehacer lo que dependa de ellos (herramienta, balde, mochila).</summary>
	public event Action? Actualizado;

	[Signal] public delegate void InventarioCargadoEventHandler();
	[Signal] public delegate void InventarioActualizadoEventHandler();

	readonly Dictionary<string, int> niveles = new();

	public override void _Ready()
	{
		Economia ??= GetNodeOrNull<Economy>("../Economy");
		foreach (var raw in SaveSystem.CargarInventario())
		{
			var partes = raw.Split(':');
			int nivel = 1;
			if (partes.Length > 1) int.TryParse(partes[1], out nivel);
			// Save editado no puede inyectar sigilo:99 (radio negativo): clamp 1..5 e id válido.
			nivel = Mathf.Clamp(nivel, 1, 5);
			if (!string.IsNullOrEmpty(partes[0]) && nivel > 0)
				niveles[partes[0]] = nivel;
		}
		Cargado?.Invoke();
		EmitSignal(SignalName.InventarioCargado);
	}

	/// <summary>Nivel actual del item (0 = no comprado).</summary>
	public int Nivel(string id) => niveles.TryGetValue(id, out int n) ? n : 0;

	public bool Tiene(string id) => Nivel(id) > 0;

	/// <summary>Solo ids (para mostrar la mochila).</summary>
	public string[] Lista()
	{
		var arr = new string[niveles.Count];
		niveles.Keys.CopyTo(arr, 0);
		return arr;
	}

	/// <summary>Sube un nivel el item (la tienda valida el máximo; acá hay tope de seguridad).</summary>
	public void Mejorar(string id)
	{
		if (string.IsNullOrEmpty(id)) return;
		// Tope de seguridad: si la tienda es burlada (doble click/spam), el modelo no se dispara.
		if (Nivel(id) >= 5) return;
		niveles[id] = Nivel(id) + 1;
		Guardar();
		// Quien escuchaba CompraRealizada (tienda) también va a refrescar, pero
		// el evento sirve para cualquier otro que dependa de los niveles.
		Actualizado?.Invoke();
		EmitSignal(SignalName.InventarioActualizado);
	}

	/// <summary>Decomiso policial: se pierden todas las mejoras.</summary>
	public void Resetear()
	{
		niveles.Clear();
		Guardar();
		Actualizado?.Invoke();
		EmitSignal(SignalName.InventarioActualizado);
	}

	string[] GuardarFormato()
	{
		var arr = new string[niveles.Count];
		int i = 0;
		foreach (var kv in niveles) arr[i++] = $"{kv.Key}:{kv.Value}";
		return arr;
	}

	void Guardar()
	{
		if (Economia != null)
			SaveSystem.GuardarTodo(Economia.Coins, GuardarFormato(), 1, Economia.Guardado);
	}
}
