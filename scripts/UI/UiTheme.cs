using Godot;

/// <summary>
/// Bloque B: caja de pinturas única + helpers responsive.
/// Antes cada pantalla pintaba su naranja y su cajita a mano (~80 overrides);
/// ahora Titel/Panel/Botón salen de acá para cambio global y accesibilidad.
/// </summary>
public static class UiTheme
{
	public static readonly Color Naranja = new(1, 0.65f, 0.15f);
	public static readonly Color NaranjaClaro = new(1, 0.75f, 0.2f);
	public static readonly Color Texto = new(0.92f, 0.92f, 0.92f);
	public static readonly Color Gris = new(0.7f, 0.7f, 0.7f);
	public static readonly Color GrisOscuro = new(0.6f, 0.6f, 0.6f);
	public static readonly Color Celeste = new(0.55f, 0.8f, 1f);
	public static readonly Color Ok = new(0.45f, 1, 0.5f);
	public static readonly Color Mal = new(1, 0.38f, 0.32f);
	public static readonly Color FondoPanel = new(0.11f, 0.12f, 0.17f, 0.97f);

	public const int TituloGrande = 44;
	public const int Titulo = 30;
	public const int Subtitulo = 20;
	public const int Cuerpo = 17;
	public const int Chico = 14;

	public static StyleBoxFlat PanelBase(Color? borde = null)
	{
		return new StyleBoxFlat
		{
			BgColor = FondoPanel,
			BorderColor = borde ?? Naranja,
			BorderWidthLeft = 3, BorderWidthRight = 3,
			BorderWidthTop = 3, BorderWidthBottom = 3,
			CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
			ContentMarginLeft = 20, ContentMarginRight = 20,
			ContentMarginTop = 16, ContentMarginBottom = 16
		};
	}

	public static void TituloPrincipal(Label l, int tam = Titulo)
	{
		l.AddThemeFontSizeOverride("font_size", tam);
		l.AddThemeColorOverride("font_color", NaranjaClaro);
		l.HorizontalAlignment = HorizontalAlignment.Center;
	}

	public static void CuerpoGris(Label l, int tam = Cuerpo)
	{
		l.AddThemeFontSizeOverride("font_size", tam);
		l.AddThemeColorOverride("font_color", Gris);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
	}

	/// <summary>
	/// Envuelve el panel en un CenterContainer a pantalla completa:
	/// en 1280x720 queda centrado igual que antes, en angosto/portrait
	/// se achica solo en vez de desbordar. Devuelve el centro para foco.
	/// </summary>
	public static CenterContainer Centrar(Node raiz, PanelContainer panel, Vector2 minSize)
	{
		var centro = new CenterContainer();
		centro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		// Margen de seguridad (notch/isla en móvil; en PC vale 16).
		int arriba = 16;
		try
		{
			var segura = DisplayServer.GetDisplaySafeArea();
			if (segura.Size.X > 0) arriba = Mathf.Max(16, segura.Position.Y + 8);
		}
		catch { }
		centro.AddThemeConstantOverride("separation", 0);
		raiz.AddChild(centro);
		panel.CustomMinimumSize = minSize;
		// Que nunca pida más que la ventana menos márgenes.
		panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		centro.AddChild(panel);
		return centro;
	}

	/// <summary>Foco inicial visible para teclado/mando + vecinos automáticos.</summary>
	public static void FocoInicial(Button b)
	{
		b.FocusMode = Control.FocusModeEnum.All;
		b.CallDeferred(Button.MethodName.GrabFocus);
	}

	/// <summary>
	/// Alto para paneles con ScrollContainer adentro: el scroll reporta
	/// mínimo (0,0), así que sin altura explícita el panel colapsa y el
	/// contenido queda invisible. Se ajusta a la ventana (margen 80).
	/// </summary>
	public static float AltoDisponible(Node n, float maximo = 600f, float margen = 80f)
	{
		float h = 720f;
		try
		{
			float r = n.GetViewport()?.GetVisibleRect().Size.Y ?? 720f;
			if (r > 100f) h = r;
		}
		catch { }
		return Mathf.Clamp(h - margen, 280f, maximo);
	}

	public static void BotonesFoco(params Button[] botones)
	{
		foreach (var b in botones) b.FocusMode = Control.FocusModeEnum.All;
		// El orden de Tab sigue el orden de hijos: ya vienen en orden visual.
	}
}
