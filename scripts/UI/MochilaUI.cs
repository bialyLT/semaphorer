using Godot;

/// <summary>
/// Mochila (I): qué mejoras llevás, a qué nivel, y qué te falta para el
/// siguiente. Una tarjeta por item en orden de tienda, agrupadas por rama.
/// Reemplaza el avise de texto que mostraba el Player. UI 100% por código.
/// </summary>
public partial class MochilaUI : CanvasLayer
{
	[Export] public Inventario? Inventario;
	[Export] public Mejoras? Mejoras;
	[Export] public Economy? Economia;

	public bool Abierta { get; private set; }

	PanelContainer? panel;
	VBoxContainer? cajaItems;
	Label? lblResumen;

	public override void _Ready()
	{
		Layer = 50;
		Inventario ??= GetNodeOrNull<Inventario>("../Inventario");
		Mejoras ??= GetNodeOrNull<Mejoras>("../Mejoras");
		Economia ??= GetNodeOrNull<Economy>("../Economia");
		ConstruirUI();
		Visible = false;
		// Si comprás desde la tienda con la mochila abierta, se actualiza sola.
		if (Mejoras != null) Mejoras.CompraRealizada += Refrescar;
		// Y también si te decomisan: los niveles cambian sin pasar por la tienda.
		if (Inventario != null) Inventario.Actualizado += Refrescar;
	}

	public override void _ExitTree()
	{
		if (Mejoras != null) Mejoras.CompraRealizada -= Refrescar;
		if (Inventario != null) Inventario.Actualizado -= Refrescar;
	}

	void ConstruirUI()
	{
		panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		UiTheme.Centrar(this, panel,
			new Vector2(540, UiTheme.AltoDisponible(this, 560f)));

		var margen = new MarginContainer();
		margen.AddThemeConstantOverride("margin_left", 4);
		margen.AddThemeConstantOverride("margin_right", 4);
		panel.AddChild(margen);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 8);
		margen.AddChild(caja);

		var titulo = new Label { Text = "TU MOCHILA" };
		UiTheme.TituloPrincipal(titulo, UiTheme.Titulo);
		caja.AddChild(titulo);

		lblResumen = new Label { Text = "" };
		lblResumen.AddThemeFontSizeOverride("font_size", 15);
		lblResumen.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		lblResumen.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(lblResumen);

		caja.AddChild(new HSeparator());

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		caja.AddChild(scroll);

		cajaItems = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		cajaItems.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(cajaItems);

		caja.AddChild(new HSeparator());

		var ayuda = new Label { Text = "I o Esc: cerrar · F: comprar mejoras" };
		UiTheme.CuerpoGris(ayuda, 14);
		caja.AddChild(ayuda);

		var bCerrar = new Button { Text = "Cerrar (I)" };
		bCerrar.Pressed += Cerrar;
		caja.AddChild(bCerrar);
		botonCerrar = bCerrar;
		UiTheme.BotonesFoco(bCerrar);
	}

	Button? botonCerrar;

	public void Alternar()
	{
		if (Abierta) Cerrar();
		else Abrir();
	}

	void Abrir()
	{
		Abierta = true;
		Visible = true;
		Refrescar();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		if (panel != null) UiJuice.PopIn(panel);
		if (botonCerrar != null) UiTheme.FocoInicial(botonCerrar);
	}

	public void Cerrar()
	{
		if (!Abierta) return;
		Abierta = false;
		Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	void Refrescar()
	{
		if (cajaItems == null || lblResumen == null) return;
		foreach (var h in cajaItems.GetChildren()) h.QueueFree();

		if (Inventario == null || Mejoras == null)
		{
			lblResumen.Text = "";
			return;
		}

		int compradas = 0, nivelesTotales = 0;
		var catalogo = Mejoras.IdsCatalogo();
		string? ramaActual = null;
		foreach (var id in catalogo)
		{
			int nivel = Inventario.Nivel(id);
			if (nivel <= 0) continue;

			// El encabezado va al cambiar de rama, pero no antes de la primera
			// tarjeta (si solo compraste una, el rótulo sobra).
			string rama = Mejoras.RamaDe(id);
			if (compradas > 0 && rama != ramaActual) cajaItems.AddChild(HeaderRama(rama));
			ramaActual = rama;

			compradas++;
			nivelesTotales += nivel;
			cajaItems.AddChild(Tarjeta(id, nivel));
		}

		if (compradas == 0)
		{
			lblResumen.Text = "La mochila está vacía";
			cajaItems.AddChild(Vacio("Todavía no tenés ninguna mejora.\n\nComprá con F (la tienda funciona\nen cualquier lado de la calle)."));
			return;
		}

		int plata = Economia?.Coins ?? 0;
		int guardado = Economia?.Guardado ?? 0;
		lblResumen.Text = $"{compradas} de {catalogo.Length} mejoras · {nivelesTotales} nivel/es · ${plata} encima · ${guardado} escondido";
	}

	Label HeaderRama(string rama)
	{
		var l = new Label { Text = $"── {rama} ──" };
		l.AddThemeFontSizeOverride("font_size", 15);
		l.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
		l.HorizontalAlignment = HorizontalAlignment.Center;
		return l;
	}

	Label Vacio(string texto)
	{
		var l = new Label { Text = texto };
		l.AddThemeFontSizeOverride("font_size", 17);
		l.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
		l.HorizontalAlignment = HorizontalAlignment.Center;
		return l;
	}

	/// <summary>Tarjeta de un item: nombre, efecto actual y el siguiente.</summary>
	PanelContainer Tarjeta(string id, int nivel)
	{
		if (Mejoras == null) return new PanelContainer();
		int max = Mejoras.NivelMaximo(id);
		string efecto = Mejoras.EfectoDe(id, nivel);
		bool alMaximo = max > 0 && nivel >= max;
		string sigEfecto = alMaximo ? "" : Mejoras.EfectoDe(id, nivel + 1);
		int sigPrecio = alMaximo ? 0 : Mejoras.PrecioNivel(id, nivel + 1);

		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.2f, 0.28f),
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 9, ContentMarginBottom = 9
		});

		var fila = new HBoxContainer();
		fila.AddThemeConstantOverride("separation", 12);
		card.AddChild(fila);

		var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		info.AddThemeConstantOverride("separation", 2);
		fila.AddChild(info);

		var nombre = new Label { Text = Mejoras.NombreItem(id) };
		nombre.AddThemeFontSizeOverride("font_size", 20);
		info.AddChild(nombre);

		if (efecto != "")
		{
			var lActual = new Label { Text = $"· {efecto}" };
			lActual.AddThemeFontSizeOverride("font_size", 15);
			lActual.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 0.55f));
			lActual.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			info.AddChild(lActual);
		}

		if (alMaximo)
		{
			var lMax = new Label { Text = "· Al máximo" };
			lMax.AddThemeFontSizeOverride("font_size", 14);
			lMax.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.3f));
			info.AddChild(lMax);
		}
		else if (sigEfecto != "")
		{
			string precio = sigPrecio > 0 ? $"${sigPrecio}" : "sin precio";
			var lSig = new Label { Text = $"· Siguiente Nv{nivel + 1} ({precio}): {sigEfecto}" };
			lSig.AddThemeFontSizeOverride("font_size", 14);
			lSig.AddThemeColorOverride("font_color", new Color(0.65f, 0.7f, 0.8f));
			lSig.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			info.AddChild(lSig);
		}

		var badge = new Label { Text = max > 0 ? $"Nv{nivel}/{max}" : $"Nv{nivel}" };
		badge.AddThemeFontSizeOverride("font_size", 20);
		badge.AddThemeColorOverride("font_color", alMaximo
			? new Color(1, 0.8f, 0.3f) : new Color(1, 0.7f, 0.2f));
		badge.HorizontalAlignment = HorizontalAlignment.Center;
		badge.VerticalAlignment = VerticalAlignment.Center;
		badge.CustomMinimumSize = new Vector2(76, 0);
		fila.AddChild(badge);

		return card;
	}
}
