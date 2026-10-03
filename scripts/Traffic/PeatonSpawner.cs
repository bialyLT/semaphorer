using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Ambienta el cruce con transeúntes: mantiene N peatones dando vueltas por
/// las veredas y cruzan la calle en la senda cuando el eje está en rojo.
/// Ruta, aspecto y ritmo se sortean; el que termina su ruta se va y viene
/// otro. Todo por código, con los valores en data/balance.json ("peatones").
/// </summary>
public partial class PeatonSpawner : Node3D
{
	[Export] public TrafficLight? Semaforo;
	[Export] public TrafficLight? Semaforo2;
	[Export] public TrafficLight? Semaforo3;
	[Export] public TrafficLight? Semaforo4;
	[Export] public CarSpawner? Trafico;
	[Export] public Economy? Economia;
	[Export] public HUD? Hud;

	[Export] public int Cantidad = 6;
	/// <summary>Cuántos nodos recorre cada peatón antes de que se vaya.</summary>
	[Export] public int NodosRuta = 5;
	public float Velocidad = 1.6f;
	public float VelocidadCruzando = 1.9f;
	public float VelocidadApurado = 3.2f;
	public float MirarSeg = 1.2f;
	public float MirarPeriodoSeg = 2.5f;
	public float PacienciaSeg = 20f;
	public float MargenAuto = 2.5f;

	/// <summary>Ladrones de la mochila (ver Ladron.cs).</summary>
	public float ProbLadron = 0.3f;
	public float RadioEspanto = 4f;
	/// <summary>Paso 5: si la partida guardada tenía un robo a medias, el
	/// primer spawn es ladrón seguro (cierra el exploit salir-para-cancelar).</summary>
	public bool ForzarLadronInicial;
	public float RadioAcecho = 4.5f;
	public float TiempoRoboSeg = 10f;
	public float TiempoPerdonSeg = 3.5f;
	public int MaxLadrones = 1;

	readonly List<Peaton> peatos = new();
	readonly RandomNumberGenerator rng = new();
	int contador = 0;
	double proximo = 0;

	readonly Color[] ColRopa = {
		new(0.9f, 0.3f, 0.25f), new(0.25f, 0.45f, 0.8f), new(0.95f, 0.8f, 0.25f),
		new(0.3f, 0.7f, 0.45f), new(0.85f, 0.5f, 0.8f), new(0.95f, 0.6f, 0.25f),
		new(0.35f, 0.35f, 0.4f)
	};
	readonly Color[] ColPantalon = {
		new(0.2f, 0.25f, 0.4f), new(0.25f, 0.25f, 0.28f), new(0.4f, 0.3f, 0.2f)
	};
	readonly Color[] ColPiel = {
		new(0.85f, 0.6f, 0.45f), new(0.65f, 0.45f, 0.3f), new(0.92f, 0.75f, 0.62f),
		new(0.5f, 0.35f, 0.25f)
	};

	public override void _Ready()
	{
		rng.Randomize();
		// Fallback por si los exports del .tscn no enlazaron (clases C# no globales).
		Semaforo ??= GetNodeOrNull<TrafficLight>("../Semaforo");
		Semaforo2 ??= GetNodeOrNull<TrafficLight>("../Semaforo2");
		Semaforo3 ??= GetNodeOrNull<TrafficLight>("../Semaforo3");
		Semaforo4 ??= GetNodeOrNull<TrafficLight>("../Semaforo4");
		Trafico ??= GetNodeOrNull<CarSpawner>("../CarSpawner");
		Economia ??= GetNodeOrNull<Economy>("../Economy");
		Hud ??= GetNodeOrNull<HUD>("../HUD");
		CargarBalance();
	}

	void CargarBalance()
	{
		var path = "res://data/balance.json";
		if (!FileAccess.FileExists(path)) return;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		try
		{
			using var doc = JsonDocument.Parse(f.GetAsText());
			if (!doc.RootElement.TryGetProperty("peatones", out var p)) return;
			Cantidad = p.GetProperty("cantidad").GetInt32();
			NodosRuta = p.GetProperty("nodos_ruta").GetInt32();
			Velocidad = (float)p.GetProperty("velocidad").GetDouble();
			VelocidadCruzando = (float)p.GetProperty("velocidad_cruzando").GetDouble();
			VelocidadApurado = (float)p.GetProperty("velocidad_apurado").GetDouble();
			MirarSeg = (float)p.GetProperty("mirar_antes_seg").GetDouble();
			MirarPeriodoSeg = (float)p.GetProperty("mirar_periodo_seg").GetDouble();
			PacienciaSeg = (float)p.GetProperty("paciencia_max_seg").GetDouble();
			MargenAuto = (float)p.GetProperty("margen_auto_m").GetDouble();
			if (doc.RootElement.TryGetProperty("ladrones", out var l))
			{
				if (l.TryGetProperty("prob_ladron", out var v)) ProbLadron = (float)v.GetDouble();
				if (l.TryGetProperty("radio_espanto_m", out v)) RadioEspanto = (float)v.GetDouble();
				if (l.TryGetProperty("radio_acecho_m", out v)) RadioAcecho = (float)v.GetDouble();
				if (l.TryGetProperty("tiempo_robo_seg", out v)) TiempoRoboSeg = (float)v.GetDouble();
				if (l.TryGetProperty("tiempo_perdon_seg", out v)) TiempoPerdonSeg = (float)v.GetDouble();
				if (l.TryGetProperty("max_activos", out var m)) MaxLadrones = m.GetInt32();
			}
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[PeatonSpawner] balance.json inválido, uso defaults: {e.Message}");
		}
	}

	public override void _Process(double _delta)
	{
		// Purga los que terminaron su ruta para repoblar.
		peatos.RemoveAll(p => !IsInstanceValid(p));
		if (peatos.Count >= Cantidad) return;
		double ahora = Time.GetTicksMsec() / 1000.0;
		if (ahora < proximo) return;
		proximo = ahora + rng.RandfRange(0.3f, 1.2f);
		Spawnear();
	}

	void Spawnear()
	{
		for (int intento = 0; intento < 6; intento++)
		{
			var ruta = Peaton.RutaAleatoria(NodosRuta, rng);
			if (ruta.Length < 2) return;
			var inicio = Peaton.NodoEn(ruta[0]);
			var segundo = Peaton.NodoEn(ruta[1]);
			if (inicio == null || segundo == null) return;
		// Que no nazcan encima de otro peatón (ni yendo al mismo punto).
		if (Ocupado(inicio.Pos)) continue;
		if (Ocupado(segundo.Pos, 1f)) continue;
		bool esLadron = ContarLadrones() < MaxLadrones &&
			(ForzarLadronInicial || rng.Randf() < ProbLadron);
		if (ForzarLadronInicial && esLadron) ForzarLadronInicial = false;
			if (esLadron)
			{
				var l = new Ladron
				{
					Name = $"Ladron{contador++}",
					Semaforo = Semaforo, Semaforo2 = Semaforo2,
					Semaforo3 = Semaforo3, Semaforo4 = Semaforo4,
					Trafico = Trafico,
					Economia = Economia,
					Hud = Hud,
					Velocidad = Velocidad * rng.RandfRange(0.9f, 1.15f),
					VelocidadCruzando = VelocidadCruzando * rng.RandfRange(0.9f, 1.15f),
					VelocidadApurado = VelocidadApurado,
					MirarSeg = MirarSeg * rng.RandfRange(0.8f, 1.4f),
					MirarPeriodoSeg = MirarPeriodoSeg,
					PacienciaSeg = PacienciaSeg * rng.RandfRange(0.8f, 1.3f),
					MargenAuto = MargenAuto,
					Ruta = ruta,
					// Campera oscura con capucha: sospechoso solo si prestás atención.
					Ropa = new Color(0.16f, 0.16f, 0.18f),
					Pantalon = ColPantalon[rng.RandiRange(0, ColPantalon.Length - 1)],
					Piel = ColPiel[rng.RandiRange(0, ColPiel.Length - 1)],
					Gorro = true,
					RadioAcecho = RadioAcecho,
					TiempoRoboSeg = TiempoRoboSeg,
					TiempoPerdonSeg = TiempoPerdonSeg,
					RadioEspanto = RadioEspanto,
					Position = inicio.Pos
				};
				AddChild(l);
				l.MirarInstantanea(segundo.Pos - inicio.Pos);
				peatos.Add(l);
				return;
			}
			var p = new Peaton
			{
				Name = $"Peaton{contador++}",
				Semaforo = Semaforo, Semaforo2 = Semaforo2,
				Semaforo3 = Semaforo3, Semaforo4 = Semaforo4,
				Trafico = Trafico,
				Velocidad = Velocidad * rng.RandfRange(0.9f, 1.15f),
				VelocidadCruzando = VelocidadCruzando * rng.RandfRange(0.9f, 1.15f),
				VelocidadApurado = VelocidadApurado,
				MirarSeg = MirarSeg * rng.RandfRange(0.8f, 1.4f),
				MirarPeriodoSeg = MirarPeriodoSeg,
				PacienciaSeg = PacienciaSeg * rng.RandfRange(0.8f, 1.3f),
				MargenAuto = MargenAuto,
				Ruta = ruta,
				Ropa = ColRopa[rng.RandiRange(0, ColRopa.Length - 1)],
				Pantalon = ColPantalon[rng.RandiRange(0, ColPantalon.Length - 1)],
				Piel = ColPiel[rng.RandiRange(0, ColPiel.Length - 1)],
				Gorro = rng.Randf() < 0.5f,
				Position = inicio.Pos
			};
			AddChild(p);
			p.MirarInstantanea(segundo.Pos - inicio.Pos);
			peatos.Add(p);
			return;
		}
	}

	bool Ocupado(Vector3 pos, float radio = 1.5f)
	{
		foreach (var p in peatos)
		{
			if (!IsInstanceValid(p)) continue;
			if (p.Position.DistanceTo(pos) < radio) return true;
		}
		return false;
	}

	int ContarLadrones()
	{
		int n = 0;
		foreach (var p in peatos)
			if (IsInstanceValid(p) && p is Ladron) n++;
		return n;
	}

	// ---------------------------------------------------------------------------
	// Ladrones de la mochila: implementados en scripts/Traffic/Ladron.cs
	// (Ladron : Peaton). El spawner sortea hasta MaxLadrones con ProbLadron
	// (data/balance.json → "ladrones"); el resto del diseño vive en Ladron:
	// acecho junto al escondite, barra que sube sin mirada y baja mirándolo,
	// robo del guardado con huida o desistimiento si lo mirás lo suficiente.
	// ---------------------------------------------------------------------------
}
