using Godot;

// Harness headless de límites (dev-only): construye la ProcCalle real y
// manda un cuerpo como el jugador (capa 1, máscara 1|4) contra el este.
// Sin muros se escapa del mundo (x>35); con muros frena en ~29.5 (borde
// del suelo: el límite de gameplay lo pone el clamp dinámico del Player).
public partial class TestLimitesRunner : Node3D
{
	CharacterBody3D? cuerpo;
	int frames;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		// Garantía dura (pura, sin escena): afuera por cualquier lado → adentro.
		var a = Player.AplicarLimites(new Vector3(25, 0.5f, -30));
		var b = Player.AplicarLimites(new Vector3(-20, 0.5f, 5));
		var c = Player.AplicarLimites(new Vector3(3, 0.5f, -8));
		GD.Print("TEST-LIMITE clamp a=", a, " b=", b, " c=", c);
		AddChild(new ProcCalle { Name = "Calle" });
		cuerpo = new CharacterBody3D
		{
			Name = "FalsoJugador",
			CollisionLayer = 1,
			CollisionMask = 5, // igual que Player: 1 (mundo) + 4 (muros)
			Position = new Vector3(0, 1f, 10)
		};
		var forma = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.5f, 0.9f, 0.5f) } };
		cuerpo.AddChild(forma);
		AddChild(cuerpo);
		GD.Print("TEST-LIMITE sale hacia +X");
	}

	public override void _PhysicsProcess(double _d)
	{
		frames++;
		if (cuerpo != null && IsInstanceValid(cuerpo))
		{
			cuerpo.Velocity = new Vector3(8, 0, 0);
			cuerpo.MoveAndSlide();
		}
		if (frames >= 240)
		{
			var p = cuerpo != null && IsInstanceValid(cuerpo) ? cuerpo.GlobalPosition : Vector3.Zero;
			GD.Print("TEST-LIMITE fin x=", p.X.ToString("F2"), " y=", p.Y.ToString("F2"));
			GetTree().Quit();
		}
	}
}
