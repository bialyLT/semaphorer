using Godot;

/// <summary>
/// Dinero del jugador: encima (Coins, lo que la policía puede decomisar)
/// y escondite (Guardado, a salvo del decomiso pero no del ladrón).
/// Emite señales para el HUD. Bloque A: robo PARCIAL del ladrón (no
/// todo-o-nada) + señales de mundo para cámara/HUD del Paso 5.
/// </summary>
public partial class Economy : Node
{
	[Signal] public delegate void CoinsChangedEventHandler(int nuevoTotal);
	[Signal] public delegate void GuardadoCambiadoEventHandler(int nuevoGuardado);
	[Signal] public delegate void GuardadoRobadoEventHandler(int monto);
	[Signal] public delegate void RobadoDevueltoEventHandler(int monto);
	[Signal] public delegate void DecomisoEventHandler(int montoPerdido);

	public int Coins { get; private set; } = 0;
	public int Guardado { get; private set; } = 0;

	/// <summary>Fracción máxima que el ladrón se lleva por robo (tuneable). Resto queda.</summary>
	public float FraccionRoboMax { get; set; } = 0.5f;

	public override void _Ready()
	{
		Coins = SaveSystem.CargarCoins();
		Guardado = SaveSystem.CargarGuardado();
		CargarBalanceRobo();
		EmitSignal(SignalName.CoinsChanged, Coins);
		EmitSignal(SignalName.GuardadoCambiado, Guardado);
	}

	void CargarBalanceRobo()
	{
		try
		{
			var path = "res://data/balance.json";
			if (!FileAccess.FileExists(path)) return;
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			using var doc = System.Text.Json.JsonDocument.Parse(f.GetAsText());
			if (doc.RootElement.TryGetProperty("ladrones", out var l) &&
				l.TryGetProperty("fraccion_robo_max", out var v))
				FraccionRoboMax = Mathf.Clamp((float)v.GetDouble(), 0.1f, 1f);
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Economy] balance sin fraccion_robo_max: {e.Message}");
		}
	}

	public void AddCoins(int n)
	{
		Coins += n;
		EmitSignal(SignalName.CoinsChanged, Coins);
	}

	public bool Spend(int n)
	{
		if (Coins < n) return false;
		Coins -= n;
		EmitSignal(SignalName.CoinsChanged, Coins);
		return true;
	}

	void GuardarTodo()
	{
		SaveSystem.GuardarTodo(Coins, SaveSystem.CargarInventario(), SaveSystem.CargarDia(), Guardado);
	}

	/// <summary>Mete todo lo de encima en el escondite.</summary>
	public void DepositarTodo()
	{
		if (Coins <= 0) return;
		Guardado += Coins;
		Coins = 0;
		EmitSignal(SignalName.CoinsChanged, Coins);
		EmitSignal(SignalName.GuardadoCambiado, Guardado);
		GuardarTodo();
	}

	/// <summary>Saca todo lo guardado para llevar encima.</summary>
	public void RetirarTodo()
	{
		if (Guardado <= 0) return;
		Coins += Guardado;
		Guardado = 0;
		EmitSignal(SignalName.CoinsChanged, Coins);
		EmitSignal(SignalName.GuardadoCambiado, Guardado);
		GuardarTodo();
	}

	/// <summary>Decomiso policial: se pierde lo de encima, lo guardado queda.</summary>
	public void QuitarTodo()
	{
		int perdido = Coins;
		Coins = 0;
		EmitSignal(SignalName.CoinsChanged, Coins);
		if (perdido > 0) EmitSignal(SignalName.Decomiso, perdido);
		GuardarTodo();
	}

	/// <summary>
	/// Robo del ladrón: se lleva hasta FraccionRoboMax de lo guardado
	/// (con mínimo de $10 si hay algo). Devuelve el monto robado.
	/// Lo de encima no lo toca. Conviene seguir usando el escondite:
	/// la policía te quita el 100% de encima, el ladrón solo una parte.
	/// </summary>
	public int RobarGuardado()
	{
		if (Guardado <= 0) return 0;
		int monto = Mathf.Clamp(Mathf.RoundToInt(Guardado * FraccionRoboMax), 10, Guardado);
		// Si queda poco, se lo lleva todo para no dejar $1 eternos.
		if (Guardado - monto < 10) monto = Guardado;
		Guardado -= monto;
		EmitSignal(SignalName.GuardadoCambiado, Guardado);
		EmitSignal(SignalName.GuardadoRobado, monto);
		GuardarTodo();
		return monto;
	}

	/// <summary>
	/// El jugador alcanzó al ladrón en la ventana de recuperación:
	/// vuelve lo robado al escondite.
	/// </summary>
	public void DevolverRobo(int monto)
	{
		if (monto <= 0) return;
		Guardado += monto;
		EmitSignal(SignalName.GuardadoCambiado, Guardado);
		EmitSignal(SignalName.RobadoDevuelto, monto);
		GuardarTodo();
	}
}
