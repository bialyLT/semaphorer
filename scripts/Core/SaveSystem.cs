using Godot;
using System.Collections.Generic;

/// <summary>
/// Partidas guardadas por slot: user://partida1.cfg .. partida3.cfg
/// (plata + inventario + escondite + oficio + ciudad + historia + mundo).
/// SlotActual elige la activa. Los campos nuevos tienen defaults para que
/// las partidas viejas migren solas (limpiavidrios / ciudad 1 / sin prólogo).
/// Bloque A: guardado en una sola pasada (sin 5x Load por cobro), lecturas
/// seguras (save corrupto no tira excepción) y sección "mundo" para Paso 5
/// (día, hora, posición, ladrón a medias, alerta policial).
/// </summary>
public static class SaveSystem
{
	public const int Slots = 3;
	public const int VersionActual = 2;
	public static int SlotActual = 1;

	/// <summary>Oficio por defecto (partidas viejas y slots sin elegir).</summary>
	public const string OficioDefecto = "limpiavidrios";
	public const int CiudadDefecto = 1;

	static string Ruta(int slot) => $"user://partida{slot}.cfg";
	static string RutaActual => Ruta(SlotActual);

	public struct Resumen
	{
		public bool Existe;
		public int Coins;
		public int Items;
		public int Guardado;
		public string Oficio;
		public int Ciudad;
		public bool FinalVisto;
		public int Dia;
	}

	// --- Lecturas seguras (no tiran si el save está corrupto/editado) ---

	static int LeerInt(ConfigFile cfg, string sec, string key, int def)
	{
		try
		{
			var v = cfg.GetValue(sec, key, def);
			return v.VariantType switch
			{
				Variant.Type.Int => (int)v,
				Variant.Type.Float => Mathf.RoundToInt((float)v),
				Variant.Type.Bool => ((bool)v) ? 1 : 0,
				_ => int.TryParse(v.AsString(), out int n) ? n : def,
			};
		}
		catch { return def; }
	}

	static float LeerFloat(ConfigFile cfg, string sec, string key, float def)
	{
		try
		{
			var v = cfg.GetValue(sec, key, def);
			return v.VariantType switch
			{
				Variant.Type.Float => (float)v,
				Variant.Type.Int => (int)v,
				_ => float.TryParse(v.AsString(), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out float n) ? n : def,
			};
		}
		catch { return def; }
	}

	static bool LeerBool(ConfigFile cfg, string sec, string key, bool def)
	{
		try
		{
			var v = cfg.GetValue(sec, key, def);
			if (v.VariantType == Variant.Type.Bool) return (bool)v;
			if (v.VariantType == Variant.Type.Int) return (int)v != 0;
			string s = v.AsString().ToLowerInvariant();
			return s == "true" || s == "1" || s == "sí" || s == "si";
		}
		catch { return def; }
	}

	static string LeerStr(ConfigFile cfg, string sec, string key, string def)
	{
		try
		{
			string s = cfg.GetValue(sec, key, def).AsString();
			return string.IsNullOrEmpty(s) ? def : s;
		}
		catch { return def; }
	}

	static ConfigFile CargarCfg()
	{
		var cfg = new ConfigFile();
		cfg.Load(RutaActual);
		return cfg;
	}

	public static Resumen LeerResumen(int slot)
	{
		var r = new Resumen { Oficio = OficioDefecto, Ciudad = CiudadDefecto, Dia = 1 };
		var cfg = new ConfigFile();
		if (cfg.Load(Ruta(slot)) != Error.Ok) return r;
		r.Existe = true;
		r.Coins = LeerInt(cfg, "progreso", "coins", 0);
		r.Guardado = LeerInt(cfg, "progreso", "escondite", 0);
		try
		{
			var arr = cfg.GetValue("progreso", "items", new Godot.Collections.Array()).AsGodotArray();
			r.Items = arr.Count;
		}
		catch { r.Items = 0; }
		r.Oficio = LeerStr(cfg, "historia", "oficio", OficioDefecto);
		int c = LeerInt(cfg, "historia", "ciudad", CiudadDefecto);
		r.Ciudad = c < 1 || c > 3 ? CiudadDefecto : c;
		r.FinalVisto = LeerBool(cfg, "historia", "final_visto", false);
		r.Dia = LeerInt(cfg, "mundo", "dia", 1);
		return r;
	}

	public static void BorrarSlot(int slot)
	{
		string abs = ProjectSettings.GlobalizePath(Ruta(slot));
		if (System.IO.File.Exists(abs)) System.IO.File.Delete(abs);
	}

	/// <summary>
	/// El renombre a Semaphorer cambia la carpeta de user://.
	/// Copia la partida vieja ("Simulador Semaforo") al slot 1 una sola vez.
	/// </summary>
	public static void MigrarLegado()
	{
		try
		{
			string dir = System.IO.Path.GetDirectoryName(OS.GetUserDataDir()) ?? "";
			string viejo = System.IO.Path.Combine(dir, "Simulador Semaforo", "savegame.cfg");
			string nuevo = ProjectSettings.GlobalizePath(Ruta(1));
			if (!System.IO.File.Exists(nuevo) && System.IO.File.Exists(viejo))
			{
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(nuevo)!);
				System.IO.File.Copy(viejo, nuevo);
			}
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Save] no se pudo migrar: {e.Message}");
		}
	}

	public static void Guardar(int coins, int dia = 1)
	{
		// Una sola pasada: lee lo que hay y preserva inventario/escondite/historia/mundo.
		var cfg = CargarCfg();
		string[] items = LeerItems(cfg);
		int guardado = LeerInt(cfg, "progreso", "escondite", 0);
		GuardarTodo(coins, items, dia, guardado);
	}

	/// <summary>
	/// Guarda todo en UNA pasada (un Load + un Save). Los parámetros de
	/// historia/mundo opcionales preservan lo ya guardado, así el código
	/// viejo que no los pasa no borra nada.
	/// </summary>
	public static void GuardarTodo(int coins, string[] items, int dia = 1, int guardado = 0,
		string? oficio = null, int ciudad = 0,
		bool? prologoVisto = null, bool? tiradaHecha = null, bool? finalVisto = null,
		float? horaDia = null, Vector2? posJugador = null,
		float? ladronProgreso = null, float? policiaTiempo = null)
	{
		var cfg = CargarCfg();
		cfg.SetValue("meta", "version", VersionActual);
		cfg.SetValue("progreso", "coins", coins);
		cfg.SetValue("progreso", "dia", Mathf.Max(1, dia <= 0 ? LeerInt(cfg, "progreso", "dia", 1) : dia));
		cfg.SetValue("progreso", "items", new Godot.Collections.Array<string>(items));
		cfg.SetValue("progreso", "escondite", guardado);
		cfg.SetValue("historia", "oficio", oficio ?? LeerStr(cfg, "historia", "oficio", OficioDefecto));
		int c = ciudad <= 0 ? LeerInt(cfg, "historia", "ciudad", CiudadDefecto) : ciudad;
		cfg.SetValue("historia", "ciudad", Mathf.Clamp(c, 1, 3));
		cfg.SetValue("historia", "prologo_visto", prologoVisto ?? LeerBool(cfg, "historia", "prologo_visto", false));
		cfg.SetValue("historia", "tirada_hecha", tiradaHecha ?? LeerBool(cfg, "historia", "tirada_hecha", false));
		cfg.SetValue("historia", "final_visto", finalVisto ?? LeerBool(cfg, "historia", "final_visto", false));
		// Mundo (Paso 5): solo se pisa lo que viene con valor; el resto se preserva.
		if (dia > 0) cfg.SetValue("mundo", "dia", Mathf.Max(1, dia));
		if (horaDia.HasValue) cfg.SetValue("mundo", "hora_dia", Mathf.Clamp(horaDia.Value, 0f, 24f));
		if (posJugador.HasValue)
		{
			cfg.SetValue("mundo", "pos_x", posJugador.Value.X);
			cfg.SetValue("mundo", "pos_z", posJugador.Value.Y);
		}
		if (ladronProgreso.HasValue) cfg.SetValue("mundo", "ladron_progreso", Mathf.Clamp(ladronProgreso.Value, -1f, 1f));
		if (policiaTiempo.HasValue) cfg.SetValue("mundo", "policia_tiempo", Mathf.Max(0f, policiaTiempo.Value));
		cfg.Save(RutaActual);
	}

	/// <summary>
	/// Guarda solo el estado del mundo (día/hora/posición/ladrón/policía)
	/// sin tocar plata/inventario. Para autosave periódico del Paso 5.
	/// </summary>
	public static void GuardarMundo(int? dia = null, float? horaDia = null, Vector2? posJugador = null,
		float? ladronProgreso = null, float? policiaTiempo = null)
	{
		var cfg = CargarCfg();
		cfg.SetValue("meta", "version", VersionActual);
		if (dia.HasValue && dia.Value > 0)
		{
			cfg.SetValue("progreso", "dia", dia.Value);
			cfg.SetValue("mundo", "dia", dia.Value);
		}
		if (horaDia.HasValue) cfg.SetValue("mundo", "hora_dia", Mathf.Clamp(horaDia.Value, 0f, 24f));
		if (posJugador.HasValue)
		{
			cfg.SetValue("mundo", "pos_x", posJugador.Value.X);
			cfg.SetValue("mundo", "pos_z", posJugador.Value.Y);
		}
		if (ladronProgreso.HasValue) cfg.SetValue("mundo", "ladron_progreso", Mathf.Clamp(ladronProgreso.Value, -1f, 1f));
		if (policiaTiempo.HasValue) cfg.SetValue("mundo", "policia_tiempo", Mathf.Max(0f, policiaTiempo.Value));
		cfg.Save(RutaActual);
	}

	static string[] LeerItems(ConfigFile cfg)
	{
		try
		{
			var arr = cfg.GetValue("progreso", "items", new Godot.Collections.Array()).AsGodotArray();
			var lista = new List<string>();
			foreach (var v in arr)
			{
				try { lista.Add(v.AsString()); } catch { }
			}
			return lista.ToArray();
		}
		catch { return System.Array.Empty<string>(); }
	}

	public static int CargarCoins()
	{
		return LeerInt(CargarCfg(), "progreso", "coins", 0);
	}

	public static int CargarGuardado()
	{
		return LeerInt(CargarCfg(), "progreso", "escondite", 0);
	}

	public static int CargarDia()
	{
		var cfg = CargarCfg();
		int d = LeerInt(cfg, "mundo", "dia", 0);
		if (d <= 0) d = LeerInt(cfg, "progreso", "dia", 1);
		return Mathf.Max(1, d);
	}

	public static float CargarHoraDia(float def = 12f)
	{
		return LeerFloat(CargarCfg(), "mundo", "hora_dia", def);
	}

	public static Vector2? CargarPosJugador()
	{
		var cfg = CargarCfg();
		// Sin posición guardada = null (el jugador usa su spawn de escena).
		bool hayX = cfg.HasSectionKey("mundo", "pos_x");
		bool hayZ = cfg.HasSectionKey("mundo", "pos_z");
		if (!hayX || !hayZ) return null;
		return new Vector2(LeerFloat(cfg, "mundo", "pos_x", 0f), LeerFloat(cfg, "mundo", "pos_z", 0f));
	}

	public static float CargarLadronProgreso() => LeerFloat(CargarCfg(), "mundo", "ladron_progreso", -1f);
	public static float CargarPoliciaTiempo() => LeerFloat(CargarCfg(), "mundo", "policia_tiempo", 0f);

	public static string[] CargarInventario()
	{
		return LeerItems(CargarCfg());
	}

	// --- Historia (Fase 1): oficio / ciudad / flags, con defaults seguros ---

	public static string CargarOficio()
	{
		return LeerStr(CargarCfg(), "historia", "oficio", OficioDefecto);
	}

	public static int CargarCiudad()
	{
		int c = LeerInt(CargarCfg(), "historia", "ciudad", CiudadDefecto);
		return c < 1 || c > 3 ? CiudadDefecto : c;
	}

	public static bool CargarPrologoVisto()
	{
		return LeerBool(CargarCfg(), "historia", "prologo_visto", false);
	}

	public static bool CargarTiradaHecha()
	{
		return LeerBool(CargarCfg(), "historia", "tirada_hecha", false);
	}

	public static bool CargarFinalVisto()
	{
		return LeerBool(CargarCfg(), "historia", "final_visto", false);
	}

	/// <summary>¿El slot ya tiene sección historia? (Fase 2: las partidas
	/// viejas no la tienen y migran en silencio sin mostrar el prólogo.)</summary>
	public static bool TieneHistoria()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RutaActual) != Error.Ok) return false;
		return cfg.HasSection("historia");
	}

	/// <summary>Atajo para fijar historia sin tocar plata/inventario.</summary>
	public static void GuardarHistoria(string? oficio = null, int ciudad = 0,
		bool? prologoVisto = null, bool? tiradaHecha = null, bool? finalVisto = null)
	{
		var cfg = CargarCfg();
		GuardarTodo(LeerInt(cfg, "progreso", "coins", 0), LeerItems(cfg),
			LeerInt(cfg, "progreso", "dia", 1), LeerInt(cfg, "progreso", "escondite", 0),
			oficio, ciudad, prologoVisto, tiradaHecha, finalVisto);
	}
}
