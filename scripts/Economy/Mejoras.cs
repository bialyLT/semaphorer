using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Árbol de mejoras de Semaphorer (F en cualquier lado, 1-9 o click).
/// Ramas EQUIPO / CALLE / NEGOCIO; los niveles pueden pedir requisito
/// ("req_id" + "req_nivel"). Lee precios.json, cobra vía Economy y
/// guarda niveles en Inventario. UI 100% por código.
/// </summary>
public partial class Mejoras : CanvasLayer
{
	[Export] public Economy? Economia;
	[Export] public Inventario? Inventario;
	[Export] public HUD? Hud;
	[Export] public AudioManager? Audio;

	public bool Abierta { get; private set; }

	/// <summary>Se emite al comprar/mejorar (el jugador refresca su herramienta).</summary>
	public event Action? CompraRealizada;

	/// <summary>Se emite al viajar (el GameManager re-skinnea la ciudad).</summary>
	public event Action<int>? ViajeRealizado;

	/// <summary>Se emite al completar todo al máximo en la última ciudad
	/// (el GameManager lanza la secuencia final, una sola vez).</summary>
	public event Action? FinalDesbloqueado;

	[Signal] public delegate void CompraHechaEventHandler();
	[Signal] public delegate void ViajeHechoEventHandler(int nuevaCiudad);

	/// <summary>Escondite en la vereda (mochila azul). Ver ProcCalle. Se lee de balance.json.</summary>
	public static Vector3 Escondite = new(5, 0.3f, 5.5f);
	public const float RadioEscondite = 3f;
	static bool esconditeCargado;

	public static void CargarEscondite()
	{
		if (esconditeCargado) return;
		esconditeCargado = true;
		try
		{
			var path = "res://data/balance.json";
			if (!FileAccess.FileExists(path)) return;
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			using var doc = JsonDocument.Parse(f.GetAsText());
			if (!doc.RootElement.TryGetProperty("escondite", out var e)) return;
			float x = e.TryGetProperty("x", out var vx) ? (float)vx.GetDouble() : Escondite.X;
			float z = e.TryGetProperty("z", out var vz) ? (float)vz.GetDouble() : Escondite.Z;
			Escondite = new Vector3(x, 0.3f, z);
		}
		catch (System.Exception ex)
		{
			GD.PushWarning($"[Mejoras] balance sin escondite: {ex.Message}");
		}
	}

	public class Nivel
	{
		public int Precio;
		public int Valor;
		public string Efecto = "";
		public string Extra = "";
		public string ReqId = "";
		public int ReqNivel;
	}

	class Item
	{
		public string Id = "";
		public string Nombre = "";
		public string Rama = "";
		public string Desc = "";
		public readonly List<Nivel> Niveles = new();
		/// <summary>Oficios que la ven en la tienda. Vacío = todos.</summary>
		public readonly List<string> Oficios = new();
	}

	/// <summary>Catálogo completo del JSON (sin filtrar).</summary>
	readonly List<Item> catalogoCompleto = new();
	/// <summary>Catálogo visible: solo el oficio actual.</summary>
	readonly List<Item> catalogo = new();
	readonly List<Button> botones = new();
	readonly List<Label> descs = new();
	readonly List<int> indiceItem = new(); // botón/descripción -> índice en catálogo
	PanelContainer? panel;
	CenterContainer? centroActual;
	ScrollContainer? scrollActual;
	Label? lblPlata;
	Label? lblViaje;
	Button? btnViaje;

	public override void _Ready()
	{
		CargarEscondite();
		Economia ??= GetNodeOrNull<Economy>("../Economy");
		Inventario ??= GetNodeOrNull<Inventario>("../Inventario");
		Hud ??= GetNodeOrNull<HUD>("../HUD");
		Audio ??= GetNodeOrNull<AudioManager>("../Audio");
		CargarCatalogo();
		AplicarOficio(SaveSystem.CargarOficio(), false);
		ConstruirUI();
		Visible = false;
		if (Economia != null) Economia.CoinsChanged += _ => { if (Abierta) Refrescar(); };
	}

	/// <summary>
	/// Filtra la tienda al oficio y reconstruye la UI. La tirada termina
	/// después del _Ready, así que el GameManager la llama al terminarla.
	/// </summary>
	public void AplicarOficio(string oficio, bool reconstruir = true)
	{
		if (string.IsNullOrEmpty(oficio)) oficio = SaveSystem.OficioDefecto;
		catalogo.Clear();
		foreach (var it in catalogoCompleto)
			if (it.Oficios.Count == 0 || it.Oficios.Contains(oficio)) catalogo.Add(it);
		if (reconstruir && panel != null)
		{
			// El panel cuelga del CenterContainer, no directo de la tienda.
			centroActual?.QueueFree();
			centroActual = null;
			panel = null;
			scrollActual = null;
			ConstruirUI();
			if (Abierta)
			{
				Refrescar();
				if (botones.Count > 0) UiTheme.FocoInicial(botones[0]);
			}
		}
	}

	static string Str(JsonElement e, string prop)
	{
		return e.TryGetProperty(prop, out var v) ? v.GetString() ?? "" : "";
	}

	static int Num(JsonElement e, string prop)
	{
		return e.TryGetProperty(prop, out var v) ? v.GetInt32() : 0;
	}

	void CargarCatalogo()
	{
		var path = "res://data/precios.json";
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning("[Mejoras] sin precios.json, árbol vacío.");
			return;
		}
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		try
		{
			using var doc = JsonDocument.Parse(f.GetAsText());
			foreach (var e in doc.RootElement.GetProperty("tienda").GetProperty("items").EnumerateArray())
			{
				var it = new Item
				{
					Id = e.GetProperty("id").GetString() ?? "",
					Nombre = e.GetProperty("nombre").GetString() ?? "",
					Rama = Str(e, "rama"),
					Desc = Str(e, "desc")
				};
				foreach (var n in e.GetProperty("niveles").EnumerateArray())
				{
					it.Niveles.Add(new Nivel
					{
						Precio = n.GetProperty("precio").GetInt32(),
						Valor = n.GetProperty("valor").GetInt32(),
						Efecto = Str(n, "efecto"),
						Extra = Str(n, "extra"),
						ReqId = Str(n, "req_id"),
						ReqNivel = Num(n, "req_nivel")
					});
				}
				if (e.TryGetProperty("oficios", out var ofs))
					foreach (var o in ofs.EnumerateArray())
					{
						string s = o.GetString() ?? "";
						if (!string.IsNullOrEmpty(s)) it.Oficios.Add(s);
					}
				catalogoCompleto.Add(it);
			}
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Mejoras] precios.json inválido: {e.Message}");
		}
	}

	void ConstruirUI()
	{
		botones.Clear();
		descs.Clear();
		indiceItem.Clear();
		// Guarda scroll/foco para no perderlos al reconstruir por oficio.
		float scrollGuardado = scrollActual?.ScrollVertical ?? 0;
		panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		centroActual = UiTheme.Centrar(this, panel,
			new Vector2(500, UiTheme.AltoDisponible(this)));

		var margen = new MarginContainer();
		margen.AddThemeConstantOverride("margin_left", 16);
		margen.AddThemeConstantOverride("margin_right", 16);
		margen.AddThemeConstantOverride("margin_top", 12);
		margen.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margen);

		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		scrollActual = scroll;
		margen.AddChild(scroll);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 6);
		caja.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(caja);

		var titulo = new Label { Text = "MEJORAS" };
		UiTheme.TituloPrincipal(titulo, 26);
		caja.AddChild(titulo);

		lblPlata = new Label { Text = "$0" };
		lblPlata.AddThemeFontSizeOverride("font_size", 20);
		lblPlata.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(lblPlata);

		var sepViaje = new HSeparator();
		caja.AddChild(sepViaje);
		lblViaje = new Label { Text = "" };
		lblViaje.AddThemeFontSizeOverride("font_size", 15);
		lblViaje.HorizontalAlignment = HorizontalAlignment.Center;
		lblViaje.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		caja.AddChild(lblViaje);
		btnViaje = new Button { Text = "Viajar", CustomMinimumSize = new Vector2(220, 48) };
		btnViaje.Pressed += Viajar;
		var centroViaje = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		centroViaje.AddChild(btnViaje);
		caja.AddChild(centroViaje);

		string? ramaActual = null;
		for (int i = 0; i < catalogo.Count; i++)
		{
			int idx = i; // copia para el closure
			var it = catalogo[i];
			if (it.Rama != ramaActual)
			{
				ramaActual = it.Rama;
				var h = new Label { Text = $"══ {ramaActual} ══" };
				h.AddThemeFontSizeOverride("font_size", 16);
				h.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1));
				h.HorizontalAlignment = HorizontalAlignment.Center;
				caja.AddChild(h);
			}
			var fila = new HBoxContainer();
			fila.AddThemeConstantOverride("separation", 10);
			caja.AddChild(fila);

			var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			fila.AddChild(info);
			var lN = new Label { Text = $"{indiceItem.Count + 1}. {it.Nombre}" };
			lN.AddThemeFontSizeOverride("font_size", 19);
			info.AddChild(lN);
			var lD = new Label { Text = it.Desc };
			lD.AddThemeFontSizeOverride("font_size", 14);
			lD.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
			lD.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			info.AddChild(lD);
			descs.Add(lD);

		var b = new Button { CustomMinimumSize = new Vector2(130, 48) };
		b.FocusMode = Control.FocusModeEnum.All;
		b.Pressed += () => Comprar(idx);
		fila.AddChild(b);
		botones.Add(b);
		indiceItem.Add(idx);
		}

		if (btnViaje != null) btnViaje.FocusMode = Control.FocusModeEnum.All;

		var ayuda = new Label { Text = $"F: cerrar · 1-{Math.Min(catalogo.Count, 9)} o click: comprar / mejorar" };
		UiTheme.CuerpoGris(ayuda, 14);
		caja.AddChild(ayuda);
		if (scrollActual != null) scrollActual.ScrollVertical = (int)scrollGuardado;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Abierta) return;
		if (@event is InputEventKey k && k.Pressed && !k.Echo)
		{
			int idx = (int)k.PhysicalKeycode - (int)Key.Key1;
			if (idx >= 0 && idx < catalogo.Count)
			{
				Comprar(idx);
				// Que el teclado siga al foco y el scroll lo muestre.
				if (idx < botones.Count)
				{
					botones[idx].GrabFocus();
					scrollActual?.EnsureControlVisible(botones[idx]);
				}
			}
		}
	}

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
		Audio?.Play("abrir", 1.1f);
		if (panel != null) UiJuice.PopIn(panel);
		// Foco inicial: primer botón comprable, si no el viaje.
		foreach (var b in botones)
		{
			if (!b.Disabled) { UiTheme.FocoInicial(b); return; }
		}
		if (btnViaje != null && !btnViaje.Disabled) UiTheme.FocoInicial(btnViaje);
	}

	public void Cerrar()
	{
		if (!Abierta) return;
		Abierta = false;
		Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	/// <summary>Nivel actual del item (0 = no comprado).</summary>
	public int NivelDe(string id) => Inventario != null ? Inventario.Nivel(id) : 0;

	/// <summary>Valor del nivel actual (0 si no comprado o nivel inválido).</summary>
	/// <remarks>Busca en el catálogo COMPLETO (no el filtrado por oficio):
	/// los valores son intrínsecos; el filtro solo decide qué se MUESTRA.</remarks>
	public int ValorNivel(string id, int nivel)
	{
		foreach (var it in catalogoCompleto)
		{
			if (it.Id != id) continue;
			if (nivel < 1 || nivel > it.Niveles.Count) return 0;
			return it.Niveles[nivel - 1].Valor;
		}
		return 0;
	}

	/// <summary>Flag extra del nivel actual (ej: "sin_rechazo").</summary>
	public string ExtraNivel(string id, int nivel)
	{
		foreach (var it in catalogoCompleto)
		{
			if (it.Id != id) continue;
			if (nivel < 1 || nivel > it.Niveles.Count) return "";
			return it.Niveles[nivel - 1].Extra;
		}
		return "";
	}

	/// <summary>Efecto de un nivel concreto ("" si ese nivel no existe).</summary>
	public string EfectoDe(string id, int nivel)
	{
		foreach (var it in catalogoCompleto)
		{
			if (it.Id != id) continue;
			if (nivel < 1 || nivel > it.Niveles.Count) return "";
			return it.Niveles[nivel - 1].Efecto;
		}
		return "";
	}

	/// <summary>Precio de un nivel concreto (0 si ese nivel no existe).</summary>
	public int PrecioNivel(string id, int nivel)
	{
		foreach (var it in catalogoCompleto)
		{
			if (it.Id != id) continue;
			if (nivel < 1 || nivel > it.Niveles.Count) return 0;
			return it.Niveles[nivel - 1].Precio;
		}
		return 0;
	}

	/// <summary>Cuántos niveles tiene el item en total (0 si no existe).</summary>
	public int NivelMaximo(string id)
	{
		foreach (var it in catalogoCompleto)
			if (it.Id == id) return it.Niveles.Count;
		return 0;
	}

	/// <summary>Rama del item: "EQUIPO" / "CALLE" / "NEGOCIO".</summary>
	public string RamaDe(string id)
	{
		foreach (var it in catalogoCompleto)
			if (it.Id == id) return it.Rama;
		return "";
	}

	/// <summary>Ids del catálogo en orden de tienda (para listar la mochila).</summary>
	public string[] IdsCatalogo()
	{
		var arr = new string[catalogo.Count];
		for (int i = 0; i < catalogo.Count; i++) arr[i] = catalogo[i].Id;
		return arr;
	}

	bool ReqCumplido(Nivel n) =>
		string.IsNullOrEmpty(n.ReqId) || NivelDe(n.ReqId) >= n.ReqNivel;

	string TextoReq(Nivel n) => $"{NombreItem(n.ReqId)} Nv{n.ReqNivel}";

	void Refrescar()
	{
		int plata = Economia?.Coins ?? 0;
		if (lblPlata != null) lblPlata.Text = $"Plata: ${plata}";
		for (int f = 0; f < botones.Count && f < indiceItem.Count; f++)
		{
			var it = catalogo[indiceItem[f]];
			var b = botones[f];
			int nivel = NivelDe(it.Id);
			bool max = nivel >= it.Niveles.Count;
			if (nivel <= 0)
			{
				var n0 = it.Niveles[0];
				bool req = ReqCumplido(n0);
				if (f < descs.Count) descs[f].Text = req
					? $"{it.Desc}\nNv1: {n0.Efecto}"
					: $"{it.Desc}\n🔒 Requiere {TextoReq(n0)}";
				b.Text = req ? $"${n0.Precio}" : "🔒";
				b.Disabled = !req || plata < n0.Precio;
				b.TooltipText = req ? n0.Efecto : $"Requiere {TextoReq(n0)}";
			}
			else if (max)
			{
				if (f < descs.Count) descs[f].Text = $"{it.Desc}\nNv{nivel} MAX: {it.Niveles[nivel - 1].Efecto}";
				b.Text = "MAX";
				b.Disabled = true;
				b.TooltipText = "Nivel máximo";
			}
			else
			{
				var sig = it.Niveles[nivel]; // siguiente (índice = nivel actual)
				bool req = ReqCumplido(sig);
				if (f < descs.Count) descs[f].Text = req
					? $"{it.Desc}\nNv{nivel}: {it.Niveles[nivel - 1].Efecto} → Nv{nivel + 1}: {sig.Efecto}"
					: $"{it.Desc}\nNv{nivel}: {it.Niveles[nivel - 1].Efecto}\n🔒 Nv{nivel + 1} requiere {TextoReq(sig)}";
				b.Text = req ? $"Nv{nivel + 1} ${sig.Precio}" : "🔒";
				b.Disabled = !req || plata < sig.Precio;
				b.TooltipText = req ? sig.Efecto : $"Requiere {TextoReq(sig)}";
			}
		}
		ActualizarViaje();
	}

	/// <summary>Niveles comprados / totales de la tienda visible (objetivo ciudad).</summary>
	public (int actual, int total) ProgresoMejoras()
	{
		int a = 0, t = 0;
		foreach (var it in catalogo)
		{
			t += it.Niveles.Count;
			a += Mathf.Min(NivelDe(it.Id), it.Niveles.Count);
		}
		return (a, t);
	}

	/// <summary>¿Todo lo visible al máximo? (requisito para el pasaje).</summary>
	public bool TiendaCompleta()
	{
		foreach (var it in catalogo)
			if (NivelDe(it.Id) < it.Niveles.Count) return false;
		return true;
	}

	/// <summary>Bloque C: el pasaje pide 70% (no 100%): no obliga a comprar
	/// sigilo/tráfico aunque tu build no los quiera.</summary>
	public bool ViajeDesbloqueado()
	{
		var (a, t) = ProgresoMejoras();
		return t <= 0 || (float)a / t >= 0.7f;
	}

	void ActualizarViaje()
	{
		if (lblViaje == null || btnViaje == null) return;
		int ciudad = SaveSystem.CargarCiudad();
		int precio = Ciudades.PasajeSiguiente(ciudad);
		var (a, t) = ProgresoMejoras();
		if (precio <= 0)
		{
			lblViaje.Text = ciudad >= 3
				? $"🏁 {Ciudades.Nombre(ciudad)}: mejoras {a}/{t}. Completá todo al máximo y pasará algo..."
				: $"🏁 {Ciudades.Nombre(ciudad)}: mejoras {a}/{t}.";
			btnViaje.Visible = false;
			return;
		}
		btnViaje.Visible = true;
		int plata = Economia?.Coins ?? 0;
		bool completa = ViajeDesbloqueado();
		lblViaje.Text = $"🚂 {Ciudades.Nombre(ciudad)} → {Ciudades.Nombre(ciudad + 1)}\nMejoras {a}/{t} (70% para viajar) · Pasaje ${plata}/${precio}";
		btnViaje.Text = $"Viajar a {Ciudades.Nombre(ciudad + 1)} (${precio})";
		btnViaje.Disabled = !completa || plata < precio;
		btnViaje.TooltipText = !completa
			? "Llegá al 70% de tus mejoras para viajar"
			: plata < precio ? $"Te faltan ${precio - plata} para el pasaje" : "¡A una ciudad mejor!";
	}

	/// <summary>Viaja a la siguiente ciudad (gasta el pasaje y guarda).</summary>
	void Viajar()
	{
		if (Economia == null || Hud == null) return;
		int ciudad = SaveSystem.CargarCiudad();
		int precio = Ciudades.PasajeSiguiente(ciudad);
		if (precio <= 0) return;
		if (!ViajeDesbloqueado())
		{
			Hud.SetError("El pasaje pide el 70% de tus mejoras (no hace falta todo al máximo).");
			return;
		}
		if (!Economia.Spend(precio))
		{
			Hud.SetError($"Te faltan ${precio - Economia.Coins} para el pasaje a {Ciudades.Nombre(ciudad + 1)}.");
			return;
		}
		int nueva = ciudad + 1;
		SaveSystem.GuardarTodo(Economia.Coins, SaveSystem.CargarInventario(), 1,
			Economia.Guardado, null, nueva, null, null, null);
		Hud.SetOk($"¡Llegaste a {Ciudades.Nombre(nueva)}! Acá se paga x{Ciudades.Mult(nueva)}.");
		Refrescar();
		ViajeRealizado?.Invoke(nueva);
		EmitSignal(SignalName.ViajeHecho, nueva);
	}

	void Comprar(int idx)
	{
		if (idx < 0 || idx >= catalogo.Count) return;
		if (Economia == null || Inventario == null || Hud == null) return;
		var it = catalogo[idx];
		int nivel = NivelDe(it.Id);
		if (nivel >= it.Niveles.Count)
		{
			Hud.SetMensaje($"{it.Nombre} ya está al máximo.");
			return;
		}
		var sig = it.Niveles[nivel];
		if (!ReqCumplido(sig))
		{
			Hud.SetError($"🔒 {it.Nombre} Nv{nivel + 1} requiere {TextoReq(sig)}.");
			if (panel != null) UiJuice.ShakeX(panel, 8f);
			return;
		}
		if (!Economia.Spend(sig.Precio))
		{
			Hud.SetError($"Te faltan ${sig.Precio - Economia.Coins} para {it.Nombre} Nv{nivel + 1}.");
			if (panel != null) UiJuice.ShakeX(panel, 8f);
			Audio?.Play("negado", 0.7f);
			return;
		}
		Inventario.Mejorar(it.Id);
		Audio?.PlayCobro(sig.Precio >= 150 ? 6 : 2);
		Hud.SetOk(nivel == 0
			? $"Compraste {it.Nombre}: {sig.Efecto}"
			: $"{it.Nombre} mejorado a Nv{nivel + 1}: {sig.Efecto}");
		Refrescar();
		if (idx < botones.Count) UiJuice.Punch(botones[idx]);
		CompraRealizada?.Invoke();
		EmitSignal(SignalName.CompraHecha);
		// Todo al máximo en la última ciudad: el Tipo vuelve con la
		// tirada final (una sola vez; el GameManager filtra repetidos).
		if (SaveSystem.CargarCiudad() >= 3 && TiendaCompleta() && !SaveSystem.CargarFinalVisto())
		{
			FinalDesbloqueado?.Invoke();
		}
	}

	public string NombreItem(string id)
	{
		foreach (var it in catalogoCompleto)
			if (it.Id == id) return it.Nombre;
		return id;
	}
}
