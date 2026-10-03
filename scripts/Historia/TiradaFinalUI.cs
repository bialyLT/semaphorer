using Godot;
using System;

/// <summary>
/// La tirada FINAL: la ruleta muestra el millón pero esta vez sí sale
/// (TiradaSuerte con forzarMillon). Celebración con destello dorado
/// manual (sin Tweens: el árbol está pausado). No pide pausa ni maneja
/// Esc: lo orquesta FinalCinematica. Emite Terminada("millon").
/// </summary>
public partial class TiradaFinalUI : CanvasLayer
{
	public event Action<string>? Terminada;

	readonly string[] orden = { TiradaSuerte.Millon, "limpiavidrios", "malabarista", "vendedor" };
	readonly System.Collections.Generic.Dictionary<string, PanelContainer> tarjetas = new();
	StyleBoxFlat? estiloBase, estiloLuz;
	Button? btnTirar, btnDespertar;
	Label? lblResultado;
	ColorRect? destello;
	readonly RandomNumberGenerator rng = new();

	bool tirado;
	bool animando;
	double tiempoAnim;
	string? resultado;
	float brillo;

	public override void _Ready()
	{
		Layer = 97;
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
			BgColor = new Color(0.35f, 0.28f, 0.08f),
			BorderColor = new Color(1, 0.85f, 0.25f),
			BorderWidthLeft = 4, BorderWidthRight = 4, BorderWidthTop = 4, BorderWidthBottom = 4,
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
		if (btnTirar != null) UiTheme.FocoInicial(btnTirar);
	}

	void ConstruirUI()
	{
		var fondo = new ColorRect { Color = new Color(0, 0, 0, 0.82f) };
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fondo);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		UiTheme.Centrar(this, panel, new Vector2(600, 0));

		var margen = new MarginContainer();
		margen.AddThemeConstantOverride("margin_left", 24);
		margen.AddThemeConstantOverride("margin_right", 24);
		margen.AddThemeConstantOverride("margin_top", 18);
		margen.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margen);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 12);
		margen.AddChild(caja);

		var titulo = new Label { Text = "💰 LA TIRADA FINAL" };
		titulo.AddThemeFontSizeOverride("font_size", 34);
		titulo.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.25f));
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(titulo);

		var bajada = new Label { Text = "El Tipo sonríe. Esta vez la ruleta no está arreglada." };
		bajada.AddThemeFontSizeOverride("font_size", 17);
		bajada.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		bajada.HorizontalAlignment = HorizontalAlignment.Center;
		bajada.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		caja.AddChild(bajada);

		var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		fila.AddThemeConstantOverride("separation", 10);
		caja.AddChild(fila);
		foreach (var id in orden)
		{
			var t = new PanelContainer { CustomMinimumSize = new Vector2(140, 110) };
			t.AddThemeStyleboxOverride("panel", estiloBase);
			var l = new Label
			{
				Text = $"{TiradaSuerte.EmojiDe(id)}\n{TiradaSuerte.NombreDe(id)}",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			l.AddThemeFontSizeOverride("font_size", 15);
			t.AddChild(l);
			fila.AddChild(t);
			tarjetas[id] = t;
		}

		btnTirar = new Button { Text = "🎲 TIRAR", CustomMinimumSize = new Vector2(200, 50) };
		btnTirar.Pressed += OnTirar;
		var centro1 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		centro1.AddChild(btnTirar);
		caja.AddChild(centro1);

		lblResultado = new Label { Text = "" };
		lblResultado.AddThemeFontSizeOverride("font_size", 22);
		lblResultado.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f));
		lblResultado.HorizontalAlignment = HorizontalAlignment.Center;
		lblResultado.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		caja.AddChild(lblResultado);

		btnDespertar = new Button { Text = "😴 ...", CustomMinimumSize = new Vector2(220, 50), Disabled = true };
		btnDespertar.Pressed += OnDespertar;
		var centro2 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		centro2.AddChild(btnDespertar);
		caja.AddChild(centro2);
		UiTheme.BotonesFoco(btnTirar, btnDespertar);

		destello = new ColorRect { Color = new Color(1, 0.9f, 0.4f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		destello.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(destello);
	}

	void OnTirar()
	{
		if (tirado || animando) return;
		resultado = TiradaSuerte.Tirar(rng, true);
		tirado = true;
		animando = true;
		tiempoAnim = 0;
		if (btnTirar != null) btnTirar.Disabled = true;
	}

	void OnDespertar()
	{
		if (!Visible || resultado == null) return;
		Visible = false;
		Terminada?.Invoke(resultado);
		QueueFree();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
		// Sin Esc: el skip global es del orquestador.
		if (k.PhysicalKeycode == Key.Space || k.PhysicalKeycode == Key.Enter || k.PhysicalKeycode == Key.KpEnter)
		{
			if (btnDespertar != null && !btnDespertar.Disabled) OnDespertar();
			else OnTirar();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (animando && resultado != null)
		{
			tiempoAnim += delta;
			// La luz corre 1.6s y cae en el millón (ya sorteado).
			int pasos = (int)(tiempoAnim / 0.1);
			PintarLuz(orden[pasos % orden.Length]);
			if (tiempoAnim >= 1.6)
			{
				animando = false;
				PintarLuz(resultado);
				if (lblResultado != null)
					lblResultado.Text = "💰 ¡$1.000.000! ¡MILLONARIO!";
				if (btnDespertar != null)
				{
					btnDespertar.Text = "😴 Despertar...";
					btnDespertar.Disabled = false;
					UiTheme.FocoInicial(btnDespertar);
				}
			}
			return;
		}
		// Destello dorado pulsante tras el millón (manual: hay pausa).
		if (tirado && !animando && destello != null)
		{
			brillo += (float)delta * 2.2f;
			var c = destello.Color;
			c.A = 0.12f + 0.1f * Mathf.Sin(brillo);
			destello.Color = c;
		}
	}

	void PintarLuz(string id)
	{
		foreach (var kv in tarjetas)
			kv.Value.AddThemeStyleboxOverride("panel", kv.Key == id ? estiloLuz : estiloBase);
	}
}
