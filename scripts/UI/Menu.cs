using Godot;
using System.Collections.Generic;

/// <summary>
/// Menú principal de Semaphorer: título, 3 slots de partida
/// (jugar / nueva con confirmación / borrar), opciones y salir.
/// UI 100% por código. Es la escena inicial del juego.
/// </summary>
public partial class Menu : CanvasLayer
{
	PanelContainer? panel;
	VBoxContainer? cajaSlots;
	VBoxContainer? cajaOpciones;
	Label? lblOpciones;
	readonly Dictionary<Button, int> armados = new();

	TutorialUI? tutorial;

	public override void _Ready()
	{
		Ajustes.Cargar();
		Ajustes.Aplicar();
		SaveSystem.MigrarLegado();
		ConstruirUI();
		RefrescarSlots();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		tutorial = new TutorialUI();
		AddChild(tutorial);
		PonerMusicaMenu();
	}

	/// <summary>Música de fondo del menú (se libera sola al cambiar de escena).</summary>
	void PonerMusicaMenu()
	{
		var musica = GD.Load<AudioStream>("res://audio/musica_menu.wav");
		if (musica == null)
		{
			GD.PushWarning("[Menu] falta audio/musica_menu.wav (generar con tools/generar_audio.py)");
			return;
		}
		AudioManager.AplicarLoop(musica);
		var p = new AudioStreamPlayer { Bus = "Master", VolumeDb = -14f, Stream = musica };
		AddChild(p);
		p.Play();
	}

	void ConstruirUI()
	{
		var fondo = new ColorRect { Color = new Color(0.07f, 0.08f, 0.12f) };
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fondo);

		panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		UiTheme.Centrar(this, panel, new Vector2(520, 0));

		var margen = new MarginContainer();
		margen.AddThemeConstantOverride("margin_left", 24);
		margen.AddThemeConstantOverride("margin_right", 24);
		margen.AddThemeConstantOverride("margin_top", 18);
		margen.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margen);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 10);
		margen.AddChild(caja);

		var titulo = new Label { Text = "SEMAPHORER" };
		titulo.AddThemeFontSizeOverride("font_size", UiTheme.TituloGrande);
		titulo.AddThemeColorOverride("font_color", UiTheme.Naranja);
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(titulo);

		var sub = new Label { Text = "limpiavidrios · malabares · venta — esquivá a la policía · llená la mochila" };
		UiTheme.CuerpoGris(sub, 15);
		caja.AddChild(sub);

		cajaSlots = new VBoxContainer();
		cajaSlots.AddThemeConstantOverride("separation", 6);
		caja.AddChild(cajaSlots);

		var filaMenu = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		filaMenu.AddThemeConstantOverride("separation", 12);
		caja.AddChild(filaMenu);

		var bTutorial = new Button { Text = "Tutorial" };
		bTutorial.Pressed += () => tutorial?.Mostrar(0);
		filaMenu.AddChild(bTutorial);
		var bOpc = new Button { Text = "Opciones" };
		bOpc.Pressed += () =>
		{
			if (cajaOpciones != null) cajaOpciones.Visible = !cajaOpciones.Visible;
			if (lblOpciones != null) lblOpciones.Visible = cajaOpciones?.Visible ?? false;
			if (cajaOpciones?.Visible ?? false) UiTheme.FocoInicial(bTutorial);
		};
		filaMenu.AddChild(bOpc);
		var bSalir = new Button { Text = "Salir" };
		bSalir.Pressed += () => GetTree()?.Quit();
		filaMenu.AddChild(bSalir);
		UiTheme.BotonesFoco(bTutorial, bOpc, bSalir);
		UiTheme.FocoInicial(bTutorial);

		lblOpciones = new Label { Text = "— Opciones —", Visible = false };
		lblOpciones.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(lblOpciones);
		cajaOpciones = Ajustes.CrearPanel();
		cajaOpciones.Visible = false;
		caja.AddChild(cajaOpciones);

		var ayuda = new Label { Text = "WASD moverse · E trabajar (según tu oficio) · F mejoras · G escondite · Espacio saltar" };
		ayuda.AddThemeFontSizeOverride("font_size", 13);
		ayuda.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
		ayuda.HorizontalAlignment = HorizontalAlignment.Center;
		ayuda.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		caja.AddChild(ayuda);

		var version = new Label { Text = $"v{VersionJuego.Actual}" };
		version.AddThemeFontSizeOverride("font_size", 13);
		version.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
		version.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(version);
	}

	void RefrescarSlots()
	{
		if (cajaSlots == null) return;
		foreach (var h in cajaSlots.GetChildren()) h.QueueFree();
		armados.Clear();
		for (int i = 1; i <= SaveSystem.Slots; i++)
		{
			int slot = i;
			var res = SaveSystem.LeerResumen(slot);
			var fila = new HBoxContainer();
			fila.AddThemeConstantOverride("separation", 8);
			cajaSlots.AddChild(fila);

			var info = new Label
			{
				Text = res.Existe
					? $"Partida {slot}: {EmojiOficio(res.Oficio)} {NombreOficio(res.Oficio)} · {Ciudades.Nombre(res.Ciudad)} · ${res.Coins} · {res.Items} mejoras · escondite ${res.Guardado}{(res.FinalVisto ? " · ★ sueño cumplido" : "")}"
					: $"Partida {slot}: vacía",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			info.AddThemeFontSizeOverride("font_size", 17);
			fila.AddChild(info);

			var bJugar = new Button { Text = "Jugar", Disabled = !res.Existe };
			bJugar.Pressed += () => { SaveSystem.SlotActual = slot; Jugar(); };
			bJugar.FocusMode = Control.FocusModeEnum.All;
			fila.AddChild(bJugar);

			var bNueva = new Button { Text = "Nueva" };
			bNueva.FocusMode = Control.FocusModeEnum.All;
			bNueva.Pressed += () =>
			{
				if (res.Existe && !Confirmado(bNueva, "¿Seguro?")) return; // queda armado
				SaveSystem.BorrarSlot(slot);
				SaveSystem.SlotActual = slot;
				Jugar();
			};
			fila.AddChild(bNueva);

			if (res.Existe)
			{
				var bBorrar = new Button { Text = "X" };
				bBorrar.FocusMode = Control.FocusModeEnum.All;
				bBorrar.Pressed += () =>
				{
					if (!Confirmado(bBorrar, "¿X?")) return; // queda armado
					SaveSystem.BorrarSlot(slot);
					RefrescarSlots();
				};
				fila.AddChild(bBorrar);
			}
		}
	}

	/// <summary>Emoji y nombre del oficio guardado en el slot.</summary>
	static string EmojiOficio(string id) => id switch
	{
		"malabarista" => "🎾",
		"vendedor" => "🥬",
		_ => "🧽",
	};

	static string NombreOficio(string id) => id switch
	{
		"malabarista" => "Malabarista",
		"vendedor" => "Vendedor",
		_ => "Limpiavidrios",
	};

	/// <summary>Doble click para confirmar acciones destructivas.</summary>
	bool Confirmado(Button b, string texto)
	{
		if (armados.TryGetValue(b, out int n) && n > 0) return true;
		armados[b] = 1;
		b.Text = texto;
		return false;
	}

	void Jugar()
	{
		GetTree()?.ChangeSceneToFile("res://scenes/Cruce.tscn");
	}
}
