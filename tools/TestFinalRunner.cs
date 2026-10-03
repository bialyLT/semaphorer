using Godot;

// Harness headless del final (dev-only): dormitorio + diálogo +
// tirada del millón + orquestación completa con avance simulado.
// OJO: la orquestación escribe final_visto en el slot 1: el .sh hace
// backup/restore de partida1.cfg alrededor de esta prueba.
public partial class TestFinalRunner : Node
{
	int step;
	int stepFrames;
	DialogoUI? dlg;
	TiradaFinalUI? tir;
	FinalCinematica? fin;
	bool dlgOk, tirOk;
	string tirId = "";
	Node3D? falsoJugador;
	static readonly Vector3 PosFalsa = new(1, 0.5f, 2);

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		var dor = Dormitorio.Build(new Vector3(200, 0, 200));
		AddChild(dor);
		var pant = dor.GetNodeOrNull<MeshInstance3D>("Pantalla");
		Dormitorio.SetPantalla(dor, true);
		GD.Print("TEST-FINAL dormitorio hijos=", dor.GetChildCount(), " pantalla=", pant != null);
		dor.QueueFree();
		dlg = new DialogoUI();
		AddChild(dlg);
		dlg.Terminada += () => { dlgOk = true; GD.Print("TEST-FINAL dialogo OK"); };
		dlg.MostrarLineas(new (string, string)[] { ("A", "uno"), ("B", "dos") });
		step = 1;
	}

	static InputEventKey Tecla(Key k) => new() { PhysicalKeycode = k, Pressed = true };

	void EspacioAFinal()
	{
		if (fin == null || !IsInstanceValid(fin)) return;
		var ev = Tecla(Key.Space);
		foreach (var c in fin.GetChildren())
		{
			if (c is DialogoUI d && IsInstanceValid(d)) d._Input(ev);
			if (c is TiradaFinalUI t && IsInstanceValid(t)) t._Input(ev);
		}
	}

	public override void _Process(double _d)
	{
		stepFrames++;
		if (step == 1)
		{
			if (stepFrames % 10 == 0 && dlg != null && IsInstanceValid(dlg))
				dlg._Input(Tecla(Key.E));
			if (dlgOk)
			{
				step = 2; stepFrames = 0;
				tir = new TiradaFinalUI();
				AddChild(tir);
				tir.Terminada += (id) => { tirOk = true; tirId = id; GD.Print("TEST-FINAL tirada OK ", id); };
				tir.Mostrar();
			}
		}
		else if (step == 2)
		{
			if (tir != null && IsInstanceValid(tir) && (stepFrames == 10 || stepFrames == 140))
				tir._Input(Tecla(Key.Space));
			if (tirOk)
			{
				step = 3; stepFrames = 0;
				// Jugador falso: el final lo teleporta al set y debe devolverlo.
				falsoJugador = new Node3D { Name = "Player", Position = PosFalsa };
				AddChild(falsoJugador);
				fin = new FinalCinematica();
				AddChild(fin);
				fin.Terminada += () => GD.Print("TEST-FINAL orquesta OK visto=", SaveSystem.CargarFinalVisto());
				fin.Mostrar();
			}
		}
		else if (step == 3)
		{
			if (stepFrames % 20 == 0) EspacioAFinal();
			bool viva = fin != null && IsInstanceValid(fin);
			if (!viva || stepFrames > 3000)
			{
				bool volvio = falsoJugador != null && IsInstanceValid(falsoJugador)
					&& falsoJugador.GlobalPosition.DistanceTo(PosFalsa) < 0.001f;
				GD.Print("TEST-FINAL fin viva=", viva, " visto=", SaveSystem.CargarFinalVisto(), " volvio=", volvio);
				GetTree().Quit();
			}
		}
	}
}
