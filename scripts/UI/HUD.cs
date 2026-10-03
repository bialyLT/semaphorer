using Godot;

/// <summary>
/// HUD por código: semáforos + timer, dinero, progreso, avisos con tono
/// (info/ok/error) y alerta policial con barra en la esquina.
/// </summary>
public partial class HUD : CanvasLayer
{
	Label? lblSemaforo, lblSemaforo2, lblSemaforo3, lblSemaforo4, lblDinero, lblMsg, lblProg, lblObjetivo, lblReloj;
	Minimapa? mapa;
	PanelContainer? panelPoli;
	ProgressBar? barraPoli;
	PanelContainer? panelLadron;
	ProgressBar? barraLadron;
	Label? tituloLadron;

	static readonly Color CInfo = new(1, 1, 1);
	static readonly Color COk = new(0.45f, 1, 0.5f);
	static readonly Color CMal = new(1, 0.38f, 0.32f);

	public override void _Ready()
	{
		// Minimapa arriba a la izquierda: la columna de texto baja debajo.
		mapa = new Minimapa { Position = new Vector2(16, 12) };
		AddChild(mapa);
		lblSemaforo = CrearLabel("Carril 1", new Vector2(16, 200), 22);
		lblSemaforo2 = CrearLabel("Carril 2", new Vector2(16, 230), 22);
		lblSemaforo3 = CrearLabel("Carril 3", new Vector2(16, 260), 22);
		lblSemaforo4 = CrearLabel("Carril 4", new Vector2(16, 290), 22);
		lblDinero = CrearLabel("$0", new Vector2(16, 324), 24);
		lblObjetivo = CrearLabel("", new Vector2(16, 354), 16);
		lblObjetivo.AddThemeColorOverride("font_color", new Color(0.55f, 0.8f, 1));
		lblProg = CrearLabel("", new Vector2(16, 380), 20);
		lblReloj = CrearLabel("", new Vector2(0, 172), 20);
		// Arriba a la derecha, debajo de las alertas (no pisa el aviso central).
		lblReloj.AnchorLeft = 1f; lblReloj.AnchorRight = 1f;
		lblReloj.OffsetLeft = -380f; lblReloj.OffsetRight = -16f;
		lblReloj.OffsetTop = 172f; lblReloj.OffsetBottom = 200f;
		lblReloj.HorizontalAlignment = HorizontalAlignment.Right;
		lblReloj.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.6f));
		lblMsg = CrearLabel(AyudaPorOficio(SaveSystem.CargarOficio()), new Vector2(0, 220), 30);
		// Aviso centrado y grande en el medio de la pantalla
		lblMsg.AnchorLeft = 0f; lblMsg.AnchorRight = 1f;
		lblMsg.OffsetLeft = 40f; lblMsg.OffsetRight = -40f;
		lblMsg.HorizontalAlignment = HorizontalAlignment.Center;
		lblMsg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lblMsg.AddThemeColorOverride("font_outline_color", Colors.Black);
		lblMsg.AddThemeConstantOverride("outline_size", 8);

		lblSemaforo.AddThemeColorOverride("font_color", Colors.Green);
		lblSemaforo2.AddThemeColorOverride("font_color", Colors.Green);
		lblSemaforo3.AddThemeColorOverride("font_color", Colors.Green);
		lblSemaforo4.AddThemeColorOverride("font_color", Colors.Green);
		lblDinero.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.2f));
		ConstruirAlertaPoli();
		ConstruirAlertaLadron();
		// El texto de ayuda inicial se borra solo (antes lo hacía el _Process).
		ProgramarBorrado(8f);
	}

	/// <summary>Alerta policial fija en la esquina superior derecha, con barra.</summary>
	void ConstruirAlertaPoli()
	{
		panelPoli = new PanelContainer();
		panelPoli.AnchorLeft = 1f; panelPoli.AnchorRight = 1f;
		panelPoli.AnchorTop = 0f; panelPoli.AnchorBottom = 0f;
		panelPoli.OffsetLeft = -320; panelPoli.OffsetRight = -16;
		panelPoli.OffsetTop = 12; panelPoli.OffsetBottom = 84;
		var estilo = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.03f, 0.03f, 0.85f),
			BorderColor = new Color(1, 0.25f, 0.2f),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop = 6, ContentMarginBottom = 6
		};
		panelPoli.AddThemeStyleboxOverride("panel", estilo);
		AddChild(panelPoli);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 4);
		panelPoli.AddChild(caja);

		var titulo = new Label { Text = "🚨 POLICÍA: VOLVÉ A LA VEREDA" };
		titulo.AddThemeFontSizeOverride("font_size", 16);
		titulo.AddThemeColorOverride("font_color", new Color(1, 0.45f, 0.4f));
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(titulo);

		barraPoli = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 18) };
		var fondo = new StyleBoxFlat { BgColor = new Color(0.25f, 0.08f, 0.08f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
		var relleno = new StyleBoxFlat { BgColor = new Color(1, 0.2f, 0.15f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
		barraPoli.AddThemeStyleboxOverride("background", fondo);
		barraPoli.AddThemeStyleboxOverride("fill", relleno);
		caja.AddChild(barraPoli);

		panelPoli.Visible = false;
	}

	/// <summary>Fracción 0..1 de atención policial; negativo la oculta.</summary>
	public void SetPolicia(float fraccion)
	{
		if (panelPoli == null || barraPoli == null) return;
		bool visible = fraccion >= 0f;
		if (panelPoli.Visible != visible) panelPoli.Visible = visible;
		if (!visible) { ultimaPoli = -1f; return; }
		float f = Mathf.Clamp(fraccion, 0f, 1f);
		// Filtro: la patrulla empuja cada frame, solo repintamos si cambió.
		if (Mathf.Abs(f - ultimaPoli) < 0.01f) return;
		ultimaPoli = f;
		barraPoli.Value = f;
	}

	float ultimaPoli = -1f;

	/// <summary>Alerta de robo en la mochila, debajo de la policial.</summary>
	void ConstruirAlertaLadron()
	{
		panelLadron = new PanelContainer();
		panelLadron.AnchorLeft = 1f; panelLadron.AnchorRight = 1f;
		panelLadron.AnchorTop = 0f; panelLadron.AnchorBottom = 0f;
		panelLadron.OffsetLeft = -320; panelLadron.OffsetRight = -16;
		panelLadron.OffsetTop = 92; panelLadron.OffsetBottom = 164;
		var estilo = new StyleBoxFlat
		{
			BgColor = new Color(0.14f, 0.09f, 0.02f, 0.88f),
			BorderColor = new Color(1, 0.6f, 0.1f),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop = 6, ContentMarginBottom = 6
		};
		panelLadron.AddThemeStyleboxOverride("panel", estilo);
		AddChild(panelLadron);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 4);
		panelLadron.AddChild(caja);

		tituloLadron = new Label { Text = "🥷 LADRÓN EN LA MOCHILA" };
		tituloLadron.AddThemeFontSizeOverride("font_size", 16);
		tituloLadron.AddThemeColorOverride("font_color", new Color(1, 0.7f, 0.3f));
		tituloLadron.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(tituloLadron);

		barraLadron = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 18) };
		var fondo = new StyleBoxFlat { BgColor = new Color(0.25f, 0.16f, 0.05f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
		var relleno = new StyleBoxFlat { BgColor = new Color(1, 0.6f, 0.1f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
		barraLadron.AddThemeStyleboxOverride("background", fondo);
		barraLadron.AddThemeStyleboxOverride("fill", relleno);
		caja.AddChild(barraLadron);

		panelLadron.Visible = false;
	}

	/// <summary>
	/// Progreso del robo 0..1; negativo lo oculta.
	/// Con pista: si lo estás mirando dice que sigas mirando, si no te avisa.
	/// </summary>
	public void SetLadron(float fraccion, bool mirando)
	{
		if (panelLadron == null || barraLadron == null || tituloLadron == null) return;
		panelLadron.Visible = fraccion >= 0f;
		if (fraccion < 0f) return;
		float f = Mathf.Clamp(fraccion, 0f, 1f);
		// Evita reasignar cada frame si no cambió (era push ciego a 60Hz).
		if (Mathf.Abs((float)barraLadron.Value - f) > 0.001f) barraLadron.Value = f;
		string titulo = mirando
			? "🥷 ¡SEGUÍ MIRANDO! SE ESTÁ YENDO"
			: fraccion > 0.7f ? "🥷 ¡CORRÉ A MIRAR LA MOCHILA!" : "🥷 LADRÓN EN LA MOCHILA ¡MIRALO!";
		if (tituloLadron.Text != titulo) tituloLadron.Text = titulo;
		var color = mirando ? new Color(0.45f, 1, 0.5f)
			: fraccion > 0.7f ? new Color(1, 0.25f, 0.2f) : new Color(1, 0.7f, 0.3f);
		tituloLadron.AddThemeColorOverride("font_color", color);
	}

	Label CrearLabel(string texto, Vector2 pos, int size)
	{
		var l = new Label { Text = texto, Position = pos };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_shadow_color", Colors.Black);
		l.AddThemeConstantOverride("shadow_offset_x", 2);
		l.AddThemeConstantOverride("shadow_offset_y", 2);
		AddChild(l);
		return l;
	}

	public void SetDinero(int n)
	{
		// Count-up 0.4s en vez de snapear: el cobro se siente.
		if (lblDinero == null) return;
		if (dineroMostrado == n) { if (lblDinero.Text != $"${n}") lblDinero.Text = $"${n}"; return; }
		int desde = dineroMostrado;
		dineroMostrado = n;
		tweenDinero?.Kill();
		tweenDinero = CreateTween();
		tweenDinero.TweenMethod(Callable.From<int>(v =>
		{
			if (lblDinero != null && IsInstanceValid(lblDinero)) lblDinero.Text = $"${v}";
		}), desde, n, 0.4);
		// Pop del cartelito.
		lblDinero.PivotOffset = lblDinero.Size / 2f;
		var pop = CreateTween();
		lblDinero.Scale = Vector2.One * 1.3f;
		pop.TweenProperty(lblDinero, "scale", Vector2.One, 0.25)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	int dineroMostrado;
	Tween? tweenDinero;

	public void SetSemaforo(string estado, float restante) =>
		PintarLuz(lblSemaforo, "Carril 1", estado, restante);

	public void SetSemaforo2(string estado, float restante) =>
		PintarLuz(lblSemaforo2, "Carril 2", estado, restante);

	public void SetSemaforo3(string estado, float restante) =>
		PintarLuz(lblSemaforo3, "Carril 3", estado, restante);

	public void SetSemaforo4(string estado, float restante) =>
		PintarLuz(lblSemaforo4, "Carril 4", estado, restante);

	void PintarLuz(Label? l, string nombre, string estado, float restante)
	{
		if (l == null) return;
		// Solo repinta si cambió el segundo o el estado (antes era 60fps con alloc).
		int seg = Mathf.CeilToInt(restante);
		string clave = $"{nombre}:{estado}:{seg}";
		string? vieja = l.GetMeta("clave", "").AsString();
		if (vieja == clave) return;
		l.SetMeta("clave", clave);
		l.Text = $"{nombre}: {estado} {seg}s";
		l.AddThemeColorOverride("font_color",
			estado == "ROJO" ? Colors.Red : estado == "AMARILLO" ? Colors.Yellow : Colors.Green);
	}

	public void SetMensaje(string s) => PintarAviso(s, CInfo);
	public void SetOk(string s) => PintarAviso(s, COk);
	public void SetError(string s) => PintarAviso(s, CMal);

	/// <summary>
	/// Ayuda inicial según TU oficio (antes decía "E limpiar" para todos,
	/// y los malabaristas/vendedores veían mensajes de otro rubro).
	/// </summary>
	public static string AyudaPorOficio(string? oficio)
	{
		string base_ = "Click para capturar mouse. WASD moverse, V cámara, ";
		return ((oficio ?? "").Trim().ToLowerInvariant()) switch
		{
			"malabarista" => base_ + "E hacer malabares en ROJO.",
			"vendedor" => base_ + "E ofrecer en ROJO.",
			_ => base_ + "E limpiar en ROJO.",
		};
	}

	/// <summary>Reescribe la ayuda inicial al cambiar de oficio (tirada).</summary>
	public void SetAyudaOficio(string oficio)
	{
		if (lblMsg == null) return;
		// Solo pisa si todavía muestra una ayuda vieja (no un cobro/aviso).
		string actual = lblMsg.Text;
		if (actual.StartsWith("Click para capturar mouse"))
			lblMsg.Text = AyudaPorOficio(oficio);
	}

	uint avisoId;
	void ProgramarBorrado(float seg)
	{
		avisoId++;
		uint mia = avisoId;
		var arbol = GetTree();
		if (arbol == null) return;
		var timer = arbol.CreateTimer(seg);
		timer.Timeout += () =>
		{
			if (mia != avisoId || lblMsg == null || !IsInstanceValid(lblMsg)) return;
			var fade = CreateTween();
			fade.TweenProperty(lblMsg, "modulate:a", 0f, 0.3);
			fade.TweenCallback(Callable.From(() =>
			{
				if (mia == avisoId && lblMsg != null && IsInstanceValid(lblMsg))
				{
					lblMsg.Text = "";
					lblMsg.Modulate = new Color(1, 1, 1, 1);
				}
			}));
		};
	}
	void PintarAviso(string s, Color c)
	{
		if (lblMsg == null) return;
		lblMsg.Text = s;
		lblMsg.AddThemeColorOverride("font_color", c);
		// Pop de entrada (el error además tiembla un poco).
		lblMsg.PivotOffset = lblMsg.Size / 2f;
		lblMsg.Scale = Vector2.One * 1.12f;
		var pop = CreateTween();
		pop.TweenProperty(lblMsg, "scale", Vector2.One, 0.18)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		if (c == CMal) UiJuice.ShakeX(lblMsg, 6f);
		// Se borra con fade de 0.3s a los 5s (sin preguntar la hora cada frame).
		ProgramarBorrado(5f);
	}

	public void SetProgreso(float p01)
	{
		if (lblProg == null) return;
		string t = p01 <= 0f ? "" : $"Progreso: {p01:P0}";
		if (lblProg.Text != t) lblProg.Text = t;
	}

	/// <summary>Reloj del mundo (Paso 5): lo empuja GameManager por eventos.</summary>
	public void SetReloj(string s)
	{
		if (lblReloj != null && lblReloj.Text != s) lblReloj.Text = s;
	}

	/// <summary>Objetivo de ciudad (persistente, no se borra como los avisos).</summary>
	public void SetObjetivo(string s)
	{
		if (lblObjetivo != null && lblObjetivo.Text != s) lblObjetivo.Text = s;
	}

	/// <summary>Conecta el minimapa (lo llama el GameManager al arrancar).</summary>
	public void InicializarMinimapa(Node3D? jugador, TrafficLight?[] luces, Mejoras? tienda)
	{
		mapa?.Inicializar(jugador, luces, tienda);
	}
}
