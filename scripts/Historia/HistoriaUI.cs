using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Prólogo "La última tirada" (Fase 2): historia de las apuestas + ruleta
/// de la suerte. Se muestra pausando el juego al crear partida nueva.
/// Al terminar emite Terminada(oficioId) y el GameManager aplica los $50,
/// guarda el oficio y sigue con el tutorial si corresponde.
/// </summary>
public partial class HistoriaUI : CanvasLayer
{
	public event Action<string>? Terminada;
	[Signal] public delegate void HistoriaTerminadaEventHandler(string oficio);

	readonly string[] orden = { TiradaSuerte.Millon, "limpiavidrios", "malabarista", "vendedor" };
	readonly Dictionary<string, PanelContainer> tarjetas = new();
	readonly Dictionary<string, Label> textos = new();
	StyleBoxFlat? estiloBase, estiloLuz;
	Button? btnTirar, btnEmpezar;
	Label? lblResultado;
	readonly RandomNumberGenerator rng = new();

	bool tirado;
	bool animando;
	double tiempoAnim;
	int idxLuz;
	string? resultado;

	public override void _Ready()
	{
		Layer = 90;
		ProcessMode = ProcessModeEnum.Always;
		rng.Randomize();
		estiloBase = new StyleBoxFlat
		{
			BgColor = new Color(0.14f, 0.15f, 0.22f),
			BorderColor = new Color(0.45f, 0.45f, 0.55f),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop = 8, ContentMarginBottom = 8
		};
		estiloLuz = new StyleBoxFlat
		{
			BgColor = new Color(0.22f, 0.2f, 0.1f),
			BorderColor = new Color(1, 0.75f, 0.2f),
			BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop = 8, ContentMarginBottom = 8
		};
		ConstruirUI();
		Visible = false;
	}

	public void Mostrar()
	{
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		UiPila.PedirPausa(GetTree(), "historia");
		if (btnTirar != null) UiTheme.FocoInicial(btnTirar);
	}

	void ConstruirUI()
	{
		var fondo = new ColorRect { Color = new Color(0, 0, 0, 0.78f) };
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fondo);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		UiTheme.Centrar(this, panel,
			new Vector2(600, UiTheme.AltoDisponible(this)));
		// En pantallas angostas la historia scrollea en vez de recortar.
		panel.AddChild(scroll);
		var margen = new MarginContainer();
		margen.AddThemeConstantOverride("margin_left", 24);
		margen.AddThemeConstantOverride("margin_right", 24);
		margen.AddThemeConstantOverride("margin_top", 18);
		margen.AddThemeConstantOverride("margin_bottom", 18);
		scroll.AddChild(margen);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 12);
		margen.AddChild(caja);

		var titulo = new Label { Text = "🎲 LA ÚLTIMA TIRADA" };
		titulo.AddThemeFontSizeOverride("font_size", 34);
		titulo.AddThemeColorOverride("font_color", new Color(1, 0.75f, 0.2f));
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(titulo);

		var historia = new Label
		{
			Text = "Te quedaste en la calle por las apuestas.\nTe quedan $50 y una última oportunidad: un tipo te ofrece una última tirada de la suerte.\nDicen que una vez salió el millón...",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		historia.AddThemeFontSizeOverride("font_size", 17);
		historia.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		caja.AddChild(historia);

		var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		fila.AddThemeConstantOverride("separation", 10);
		caja.AddChild(fila);
		foreach (var id in orden)
		{
			var t = new PanelContainer { CustomMinimumSize = new Vector2(140, 110) };
			t.AddThemeStyleboxOverride("panel", estiloBase);
			var l = new Label
			{
				Text = $"{TiradaSuerte.EmojiDe(id)}\n{TiradaSuerte.NombreDe(id)}\n{TiradaSuerte.DescDe(id)}",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			l.AddThemeFontSizeOverride("font_size", 14);
			t.AddChild(l);
			fila.AddChild(t);
			tarjetas[id] = t;
			textos[id] = l;
		}

		btnTirar = new Button { Text = "🍀 TIRAR", CustomMinimumSize = new Vector2(200, 50) };
		btnTirar.Pressed += OnTirar;
		var centro1 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		centro1.AddChild(btnTirar);
		caja.AddChild(centro1);

		lblResultado = new Label { Text = "" };
		lblResultado.AddThemeFontSizeOverride("font_size", 18);
		lblResultado.AddThemeColorOverride("font_color", new Color(0.5f, 1, 0.55f));
		lblResultado.HorizontalAlignment = HorizontalAlignment.Center;
		lblResultado.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		caja.AddChild(lblResultado);

		btnEmpezar = new Button { Text = "Empezar a laburar", CustomMinimumSize = new Vector2(220, 50), Disabled = true };
		btnEmpezar.Pressed += OnEmpezar;
		var centro2 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		centro2.AddChild(btnEmpezar);
		caja.AddChild(centro2);
		UiTheme.BotonesFoco(btnTirar, btnEmpezar);
	}

	void OnTirar()
	{
		if (tirado || animando) return;
		resultado = TiradaSuerte.Tirar(rng);
		tirado = true;
		animando = true;
		tiempoAnim = 0;
		idxLuz = 0;
		if (btnTirar != null) btnTirar.Disabled = true;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is InputEventKey k && k.Pressed && !k.Echo &&
			(k.PhysicalKeycode == Key.Enter || k.PhysicalKeycode == Key.KpEnter || k.PhysicalKeycode == Key.Space))
		{
			if (btnEmpezar != null && !btnEmpezar.Disabled) OnEmpezar();
			else OnTirar();
		}
	}

	public override void _Process(double delta)
	{
		if (!animando || resultado == null) return;
		tiempoAnim += delta;
		// Ruleta: la luz corre 1.4s y cae en el resultado (ya sorteado).
		int pasos = (int)(tiempoAnim / 0.09);
		PintarLuz(orden[(idxLuz + pasos) % orden.Length]);
		if (tiempoAnim >= 1.4)
		{
			animando = false;
			PintarLuz(resultado);
			MostrarResultado(resultado);
		}
	}

	void PintarLuz(string id)
	{
		foreach (var kv in tarjetas)
			kv.Value.AddThemeStyleboxOverride("panel", kv.Key == id ? estiloLuz : estiloBase);
	}

	void MostrarResultado(string id)
	{
		if (lblResultado != null)
			lblResultado.Text = $"{TiradaSuerte.EmojiDe(id)} Te tocó: {TiradaSuerte.NombreDe(id)}\n{TiradaSuerte.DescDe(id)} · Arrancás con $50.";
		if (btnEmpezar != null) btnEmpezar.Disabled = false;
		if (btnEmpezar != null) UiTheme.FocoInicial(btnEmpezar);
	}

	bool pausaSuelta;
	void OnEmpezar()
	{
		if (resultado == null) return;
		Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		if (!pausaSuelta) { UiPila.SoltarPausa(GetTree(), "historia"); pausaSuelta = true; }
		Terminada?.Invoke(resultado);
		EmitSignal(SignalName.HistoriaTerminada, resultado);
		QueueFree();
	}
}
