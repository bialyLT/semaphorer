using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Modal de la tirada del tipo de gala: muestra los premios ($50 a $500
/// más el millón de adorno), ruleta con luz, TIRAR y resultado. Pausa el
/// juego como HistoriaUI. Al cerrar emite Terminada(premio) y el
/// GameManager acredita la plata.
/// </summary>
public partial class TiradaTipoUI : CanvasLayer
{
	public event Action<int>? Terminada;
	[Signal] public delegate void TiradaTipoTerminadaEventHandler(int premio);

	readonly List<int> orden = new() { TiradaTipo.Millon, 50, 100, 200, 350, 500 };
	readonly Dictionary<int, PanelContainer> tarjetas = new();
	StyleBoxFlat? estiloBase, estiloLuz;
	Button? btnTirar, btnSeguir;
	Label? lblResultado;
	readonly RandomNumberGenerator rng = new();

	bool tirado;
	bool animando;
	double tiempoAnim;
	int idxLuz;
	int resultado;
	bool pausaSuelta;

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
		UiPila.PedirPausa(GetTree(), "tirada");
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

		var titulo = new Label { Text = "🎲 LA TIRADA DEL TIPO" };
		titulo.AddThemeFontSizeOverride("font_size", 34);
		titulo.AddThemeColorOverride("font_color", new Color(1, 0.75f, 0.2f));
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(titulo);

		var texto = new Label
		{
			Text = "El tipo de gala te ofrece una tirada de la suerte.\nPodés ganar hasta $500... y dicen que alguna vez salió el millón.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		texto.AddThemeFontSizeOverride("font_size", 17);
		texto.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		caja.AddChild(texto);

		var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		fila.AddThemeConstantOverride("separation", 10);
		caja.AddChild(fila);
		foreach (var monto in orden)
		{
			var t = new PanelContainer { CustomMinimumSize = new Vector2(90, 80) };
			t.AddThemeStyleboxOverride("panel", estiloBase);
			var l = new Label
			{
				Text = TiradaTipo.TextoPremio(monto),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			l.AddThemeFontSizeOverride("font_size", 15);
			t.AddChild(l);
			fila.AddChild(t);
			tarjetas[monto] = t;
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

		btnSeguir = new Button { Text = "Seguir laburando", CustomMinimumSize = new Vector2(220, 50), Disabled = true };
		btnSeguir.Pressed += OnSeguir;
		var centro2 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		centro2.AddChild(btnSeguir);
		caja.AddChild(centro2);
		UiTheme.BotonesFoco(btnTirar, btnSeguir);
	}

	void OnTirar()
	{
		if (tirado || animando) return;
		resultado = TiradaTipo.Tirar(rng);
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
			if (btnSeguir != null && !btnSeguir.Disabled) OnSeguir();
			else OnTirar();
		}
	}

	public override void _Process(double delta)
	{
		if (!animando) return;
		tiempoAnim += delta;
		// Ruleta: la luz corre 1.4s y cae en el resultado (ya sorteado).
		int pasos = (int)(tiempoAnim / 0.09);
		PintarLuz(orden[(idxLuz + pasos) % orden.Count]);
		if (tiempoAnim >= 1.4)
		{
			animando = false;
			PintarLuz(resultado);
			MostrarResultado(resultado);
		}
	}

	void PintarLuz(int monto)
	{
		foreach (var kv in tarjetas)
			kv.Value.AddThemeStyleboxOverride("panel", kv.Key == monto ? estiloLuz : estiloBase);
	}

	void MostrarResultado(int monto)
	{
		if (lblResultado != null)
			lblResultado.Text = $"¡Ganaste {TiradaTipo.TextoPremio(monto)}!";
		if (btnSeguir != null) btnSeguir.Disabled = false;
		if (btnSeguir != null) UiTheme.FocoInicial(btnSeguir);
	}

	void OnSeguir()
	{
		if (!tirado) return;
		Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		if (!pausaSuelta) { UiPila.SoltarPausa(GetTree(), "tirada"); pausaSuelta = true; }
		Terminada?.Invoke(resultado);
		EmitSignal(SignalName.TiradaTipoTerminada, resultado);
		QueueFree();
	}
}
