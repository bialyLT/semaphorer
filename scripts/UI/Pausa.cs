using Godot;

/// <summary>
/// Menú de pausa dentro del juego (Esc): continuar, opciones,
/// guardar y salir al menú. UI 100% por código.
/// </summary>
public partial class Pausa : CanvasLayer
{
	[Export] public Economy? Economia;

	bool enPausa;
	PanelContainer? panel;
	VBoxContainer? cajaOpciones;
	Button? botonInicial;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Economia ??= GetNodeOrNull<Economy>("../Economy");
		ConstruirUI();
		Visible = false;
	}

	void ConstruirUI()
	{
		panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		UiTheme.Centrar(this, panel, new Vector2(400, 0));

		var margen = new MarginContainer();
		margen.AddThemeConstantOverride("margin_left", 20);
		margen.AddThemeConstantOverride("margin_right", 20);
		margen.AddThemeConstantOverride("margin_top", 16);
		margen.AddThemeConstantOverride("margin_bottom", 16);
		panel.AddChild(margen);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 10);
		margen.AddChild(caja);

		var titulo = new Label { Text = "PAUSA" };
		UiTheme.TituloPrincipal(titulo, 32);
		caja.AddChild(titulo);

		var bSeguir = new Button { Text = "Continuar (Esc)" };
		bSeguir.Pressed += Reanudar;
		caja.AddChild(bSeguir);

		var bOpc = new Button { Text = "Opciones" };
		bOpc.Pressed += () => { if (cajaOpciones != null) cajaOpciones.Visible = !cajaOpciones.Visible; };
		caja.AddChild(bOpc);

		cajaOpciones = Ajustes.CrearPanel();
		cajaOpciones.Visible = false;
		caja.AddChild(cajaOpciones);

		var bSalir = new Button { Text = "Guardar y salir al menú" };
		bSalir.Pressed += GuardarYSalir;
		caja.AddChild(bSalir);

		var lblVersion = new Label { Text = $"v{VersionJuego.Actual}" };
		lblVersion.AddThemeFontSizeOverride("font_size", 13);
		lblVersion.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
		lblVersion.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(lblVersion);
		UiTheme.BotonesFoco(bSeguir, bOpc, bSalir);
		botonInicial = bSeguir;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// El Player está pausado y no recibe input: Esc lo maneja acá.
		if (enPausa && @event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
			Reanudar();
	}

	public void Alternar()
	{
		if (enPausa) Reanudar();
		else Pausar();
	}

	public bool Abierta() => enPausa;

	void Pausar()
	{
		enPausa = true;
		Visible = true;
		UiPila.PedirPausa(GetTree(), "pausa");
		Input.MouseMode = Input.MouseModeEnum.Visible;
		if (panel != null) UiJuice.PopIn(panel);
		if (botonInicial != null) UiTheme.FocoInicial(botonInicial);
	}

	void Reanudar()
	{
		enPausa = false;
		Visible = false;
		if (cajaOpciones != null) cajaOpciones.Visible = false;
		UiPila.SoltarPausa(GetTree(), "pausa");
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	void GuardarYSalir()
	{
		// Paso 5: guardado completo (plata + día/hora/posición/ladrón/policía).
		var gm = GetParent() as GameManager;
		if (gm != null) gm.GuardarSalida();
		else if (Economia != null) SaveSystem.Guardar(Economia.Coins);
		Ajustes.Guardar();
		UiPila.SoltarTodo(GetTree());
		GetTree()?.ChangeSceneToFile("res://scenes/Menu.tscn");
	}
}
