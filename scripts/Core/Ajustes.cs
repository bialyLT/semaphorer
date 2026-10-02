using Godot;

/// <summary>
/// Ajustes básicos del juego (persisten en user://config.cfg).
/// Sensibilidad la leen Player/CameraRig en vivo; volumen y pantalla se aplican.
/// </summary>
public static class Ajustes
{
	const string Path = "user://config.cfg";

	public static float Sensibilidad = 0.003f;
	public static float Volumen = 80f;
	public static bool PantallaCompleta = false;

	public static void Cargar()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(Path) != Error.Ok) return;
		Sensibilidad = (float)cfg.GetValue("ajustes", "sensibilidad", 0.003f);
		Volumen = (float)cfg.GetValue("ajustes", "volumen", 80f);
		PantallaCompleta = (bool)cfg.GetValue("ajustes", "pantalla_completa", false);
	}

	public static void Guardar()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("ajustes", "sensibilidad", Sensibilidad);
		cfg.SetValue("ajustes", "volumen", Volumen);
		cfg.SetValue("ajustes", "pantalla_completa", PantallaCompleta);
		cfg.Save(Path);
	}

	public static void Aplicar()
	{
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(Volumen / 100f));
		DisplayServer.WindowSetMode(PantallaCompleta
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);
	}

	/// <summary>Panel de opciones reutilizable (menú y pausa). Llama a Guardar al cambiar.</summary>
	public static VBoxContainer CrearPanel()
	{
		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 10);

		var tS = new Label { Text = $"Sensibilidad mouse: {Sensibilidad:0.000}" };
		tS.AddThemeFontSizeOverride("font_size", 18);
		caja.AddChild(tS);
		var sS = new HSlider { MinValue = 0.001f, MaxValue = 0.008f, Step = 0.0005f, Value = Sensibilidad, CustomMinimumSize = new Vector2(280, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		sS.ValueChanged += v => { Sensibilidad = (float)v; tS.Text = $"Sensibilidad mouse: {Sensibilidad:0.000}"; Guardar(); };
		caja.AddChild(sS);

		var tV = new Label { Text = $"Volumen: {Volumen:0}%" };
		tV.AddThemeFontSizeOverride("font_size", 18);
		caja.AddChild(tV);
		var sV = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = Volumen, CustomMinimumSize = new Vector2(280, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		sV.ValueChanged += v => { Volumen = (float)v; tV.Text = $"Volumen: {Volumen:0}%"; Aplicar(); Guardar(); };
		caja.AddChild(sV);

		var cF = new CheckButton { Text = "Pantalla completa", ButtonPressed = PantallaCompleta };
		cF.AddThemeFontSizeOverride("font_size", 18);
		cF.Toggled += on => { PantallaCompleta = on; Aplicar(); Guardar(); };
		caja.AddChild(cF);

		return caja;
	}
}
