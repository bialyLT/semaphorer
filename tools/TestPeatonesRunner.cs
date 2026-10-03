using Godot;

// Harness headless anti-encimado (dev-only): 4 peatones con la MISMA ruta
// al mismo pie de senda (donde se amontonan a esperar). Sin fix terminan
// uno encima del otro; con fix mantienen distancia personal.
public partial class TestPeatonesRunner : Node
{
	readonly System.Collections.Generic.List<Peaton> grupo = new();
	int frames;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		var nodo = Peaton.NodoEn("NEA");
		Vector3 pos = nodo != null ? nodo.Pos : Vector3.Zero;
		for (int i = 0; i < 4; i++)
		{
			var p = new Peaton
			{
				Ruta = new[] { "NEA", "NES1", "NOS1" },
				Velocidad = 1.6f,
				Position = pos
			};
			AddChild(p);
			grupo.Add(p);
		}
		GD.Print("TEST-PEATON spawn 4 en NEA");
	}

	public override void _Process(double _d)
	{
		frames++;
		if (frames % 120 == 0 || frames >= 600)
		{
			float min = 99f;
			int vivos = 0;
			for (int i = 0; i < grupo.Count; i++)
			{
				if (!IsInstanceValid(grupo[i])) continue;
				vivos++;
				for (int j = i + 1; j < grupo.Count; j++)
				{
					if (!IsInstanceValid(grupo[j])) continue;
					var a = grupo[i].Position;
					var b = grupo[j].Position;
					float d = new Vector2(a.X - b.X, a.Z - b.Z).Length();
					min = Mathf.Min(min, d);
				}
			}
			GD.Print("TEST-PEATON f", frames, " vivos=", vivos, " min=", min.ToString("F2"));
		}
		if (frames >= 600) GetTree().Quit();
	}
}
