using Godot;

// Harness headless del prólogo (dev-only): el Player debe quedar oculto
// mientras se elige oficio (si no, su herramienta flotaría tras el panel)
// y volver al terminar, con un oficio jugable (nunca el millón).
public partial class TestPrologoRunner : Node
{
	HistoriaUI? h;
	Node3D? falsoJugador;
	string oficio = "";
	int frames;

	static InputEventKey Tecla(Key k) => new() { PhysicalKeycode = k, Pressed = true };

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		falsoJugador = new Node3D { Name = "Player" };
		AddChild(falsoJugador);
		h = new HistoriaUI();
		AddChild(h);
		h.Terminada += (id) => { oficio = id; GD.Print("TEST-PROLOGO oficio=", id); };
		h.Mostrar();
		GD.Print("TEST-PROLOGO oculta=", !falsoJugador.Visible);
	}

	public override void _Process(double _d)
	{
		frames++;
		bool viva = h != null && IsInstanceValid(h);
		if (frames == 10 && viva) h!._UnhandledInput(Tecla(Key.Space)); // tirar
		if (frames == 150 && viva) h!._UnhandledInput(Tecla(Key.Enter)); // empezar
		if (frames >= 180)
		{
			bool visible = falsoJugador != null && IsInstanceValid(falsoJugador) && falsoJugador.Visible;
			bool jugable = oficio == "limpiavidrios" || oficio == "malabarista" || oficio == "vendedor";
			GD.Print("TEST-PROLOGO fin oficio=", oficio, " jugable=", jugable, " visible=", visible);
			GetTree().Quit();
		}
	}
}
