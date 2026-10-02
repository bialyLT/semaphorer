using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Fase 4: datos de las 3 ciudades (lee data/ciudades.json con defaults).
/// El multiplicador se aplica al cobrar cada servicio; el pasaje habilita
/// el viaje cuando la tienda del oficio está completa.
/// </summary>
public static class Ciudades
{
	class Datos
	{
		public string Nombre = "";
		public float Mult = 1f;
		public int Pasaje = 0;
	}

	static Dictionary<int, Datos>? cache;

	static Dictionary<int, Datos> Tabla()
	{
		if (cache != null) return cache;
		cache = new Dictionary<int, Datos>
		{
			{ 1, new Datos { Nombre = "Cruce Viejo", Mult = 1f, Pasaje = 1500 } },
			{ 2, new Datos { Nombre = "Avenida Nueva", Mult = 2.5f, Pasaje = 6000 } },
			{ 3, new Datos { Nombre = "La Capital", Mult = 5f, Pasaje = 0 } },
		};
		var path = "res://data/ciudades.json";
		if (!FileAccess.FileExists(path)) return cache;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		try
		{
			using var doc = JsonDocument.Parse(f.GetAsText());
			foreach (var e in doc.RootElement.GetProperty("ciudades").EnumerateArray())
			{
				int id = e.GetProperty("id").GetInt32();
				if (!cache.ContainsKey(id)) cache[id] = new Datos();
				cache[id].Nombre = e.TryGetProperty("nombre", out var n) ? n.GetString() ?? "" : cache[id].Nombre;
				if (e.TryGetProperty("mult_pago", out var m)) cache[id].Mult = (float)m.GetDouble();
				if (e.TryGetProperty("precio_pasaje", out var p)) cache[id].Pasaje = p.GetInt32();
			}
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Ciudades] ciudades.json inválido, uso defaults: {e.Message}");
		}
		return cache;
	}

	static Datos De(int id) => Tabla().TryGetValue(id, out var d) ? d : new Datos { Nombre = $"C{id}" };

	public static string Nombre(int id) => De(id).Nombre;
	public static float Mult(int id) => De(id).Mult;
	/// <summary>Precio para ir a la siguiente (0 = no hay siguiente).</summary>
	public static int PasajeSiguiente(int id) => De(id).Pasaje;

	/// <summary>Aplica el multiplicador de la ciudad actual al pago.</summary>
	public static int AplicarMult(int pago)
	{
		float m = Mult(SaveSystem.CargarCiudad());
		if (m <= 1f) return pago;
		return System.Math.Max(1, (int)System.Math.Round(pago * m));
	}
}
