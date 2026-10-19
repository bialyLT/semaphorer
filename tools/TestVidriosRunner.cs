using Godot;

/// <summary>
/// Test manual de vidrios (dev-only, escena tools/TestVidrios.tscn):
/// el juego real + un visor del ángulo Y de la vista (pitch de la cámara:
/// positivo mirando arriba, negativo abajo). Sirve para definir la ventana
/// de pitch en la que debe funcionar el golpe.
/// </summary>
public partial class TestVidriosRunner : Node3D
{
	Label? lblDebug;
	Player? jugador;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		// Mundo real de fondo (piso, jugador, cámara, HUD, tránsito real).
		var ps = GD.Load<PackedScene>("res://scenes/Cruce.tscn");
		AddChild(ps.Instantiate());
		jugador = GetNodeOrNull<Player>("Cruce/Player");
		var capa = new CanvasLayer { Layer = 50 };
		AddChild(capa);
		lblDebug = new Label { Position = new Vector2(300, 12) };
		lblDebug.AddThemeFontSizeOverride("font_size", 22);
		lblDebug.AddThemeColorOverride("font_color", Colors.White);
		lblDebug.AddThemeColorOverride("font_shadow_color", Colors.Black);
		lblDebug.AddThemeConstantOverride("shadow_offset_x", 2);
		lblDebug.AddThemeConstantOverride("shadow_offset_y", 2);
		capa.AddChild(lblDebug);
		GD.Print("TEST-VIDRIOS listo: juego real + visor de ángulo Y.");
	}

	public override void _Process(double _delta)
	{
		if (lblDebug == null || !IsInstanceValid(lblDebug)) return;
		var cam = GetViewport()?.GetCamera3D();
		if (cam == null)
		{
			lblDebug.Text = "sin cámara";
			return;
		}
		var fwd = -cam.GlobalTransform.Basis.Z;
		float elev = 0f;
		if (fwd.LengthSquared() > 0.001f)
			elev = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(fwd.Normalized().Y, -1f, 1f)));
		string signo = elev >= 0f ? "+" : "";
		string ventana = elev >= -40f && elev <= 30f ? "✓ pega" : "✗ fuera de ventana";
		lblDebug.Text = $"Vista Y: {signo}{elev:0.0}° ({ventana})";
	}
}
