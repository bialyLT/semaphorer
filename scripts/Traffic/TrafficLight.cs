using Godot;

/// <summary>
/// Semáforo con ciclo Verde→Amarillo→Rojo. Visual 100% procedural estilo
/// pórtico: dos columnas en las veredas, travesaño sobre la calle y cabezal
/// colgando sobre el carril (más intuitivo que el poste lateral).
/// Emite LightChanged(estado). TiempoRestante para HUD.
/// </summary>
public partial class TrafficLight : Node3D
{
	[Signal] public delegate void LightChangedEventHandler(string nuevoEstado);

	public string Estado { get; private set; } = "VERDE";
	public float TiempoRestante { get; private set; }

	int verdeSeg = 15, amarilloSeg = 3, rojoSeg = 22;
	float timer;

	readonly System.Collections.Generic.List<MeshInstance3D> lampRojos = new();
	readonly System.Collections.Generic.List<MeshInstance3D> lampAmarillos = new();
	readonly System.Collections.Generic.List<MeshInstance3D> lampVerdes = new();
	StandardMaterial3D? matVerdeOn, matAmarilloOn, matRojoOn, matOff;

	public void Setup(int verde, int amarillo, int rojo)
	{
		verdeSeg = verde; amarilloSeg = amarillo; rojoSeg = rojo;
		// Reinicia el ciclo con los nuevos tiempos (GameManager lo llama al arrancar,
		// después del _Ready; sin esto el timer quedaría con los defaults).
		EntrarEstado("VERDE", verdeSeg);
	}

	float CicloTotal() => verdeSeg + amarilloSeg + rojoSeg;

	/// <summary>
	/// Avanza el ciclo N segundos sin animación.
	/// Se usa para poner el 2º semáforo en oposición (desfase = medio ciclo).
	/// </summary>
	public void Adelantar(float segundos)
	{
		float s = segundos;
		int guard = 0;
		while (s > 0f && guard++ < 10)
		{
			if (s < TiempoRestante)
			{
				timer -= s;
				TiempoRestante = timer;
				s = 0f;
			}
			else
			{
				s -= TiempoRestante;
				if (Estado == "VERDE") EntrarEstado("AMARILLO", amarilloSeg);
				else if (Estado == "AMARILLO") EntrarEstado("ROJO", rojoSeg);
				else EntrarEstado("VERDE", verdeSeg);
			}
		}
	}

	public override void _Ready()
	{
		ConstruirVisual();
		EntrarEstado("VERDE", verdeSeg);
	}

	public override void _Process(double delta)
	{
		timer -= (float)delta;
		TiempoRestante = Mathf.Max(timer, 0f);
		if (timer <= 0f)
		{
			if (Estado == "VERDE") EntrarEstado("AMARILLO", amarilloSeg);
			else if (Estado == "AMARILLO") EntrarEstado("ROJO", rojoSeg);
			else EntrarEstado("VERDE", verdeSeg);
		}
	}

	public bool EsRojo() => Estado == "ROJO";
	public bool PuedenAvanzar() => Estado == "VERDE";

	void EntrarEstado(string nuevo, float duracion)
	{
		Estado = nuevo;
		timer = duracion;
		TiempoRestante = duracion;
		ActualizarLuces();
		EmitSignal(SignalName.LightChanged, nuevo);
	}

	void ConstruirVisual()
	{
		// El visual lo arma GameManager con ConstruirPortico (uno por enfoque).
	}

	void CrearMateriales()
	{
		if (matOff != null) return;
		matOff = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f) };
		matRojoOn = new StandardMaterial3D { AlbedoColor = Colors.Red, EmissionEnabled = true, Emission = Colors.Red, EmissionEnergyMultiplier = 3f };
		matAmarilloOn = new StandardMaterial3D { AlbedoColor = Colors.Yellow, EmissionEnabled = true, Emission = Colors.Yellow, EmissionEnergyMultiplier = 3f };
		matVerdeOn = new StandardMaterial3D { AlbedoColor = Colors.Green, EmissionEnabled = true, Emission = Colors.Green, EmissionEnergyMultiplier = 3f };
	}

	/// <summary>
	/// Arma un pórtico: columnas en ambas veredas (local ±5), travesaño
	/// sobre la calle y cabezal colgando sobre el carril (local x=<paramref name="carril"/>).
	/// Las lámparas miran a +Z local (hacia los conductores del enfoque).
	/// </summary>
	public void ConstruirPortico(Vector3 centro, float rotYGrados, float carril)
	{
		CrearMateriales();
		var g = new Node3D
		{
			Name = "Portico",
			Position = centro,
			RotationDegrees = new Vector3(0, rotYGrados, 0)
		};
		AddChild(g);
		var matOscuro = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.15f, 0.17f), Roughness = 0.6f };
		foreach (float x in new float[] { -5f, 5f })
		{
			// Base + columna sobre la vereda
			var bas = new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 0.4f },
				Position = new Vector3(x, 0.2f, 0)
			};
			bas.SetSurfaceOverrideMaterial(0, matOscuro);
			g.AddChild(bas);
			var columna = new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.13f, Height = 5.6f },
				Position = new Vector3(x, 2.8f, 0)
			};
			columna.SetSurfaceOverrideMaterial(0, matOscuro);
			g.AddChild(columna);
		}
		// Travesaño horizontal sobre la calle
		var brazo = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(11f, 0.28f, 0.28f) },
			Position = new Vector3(0, 5.6f, 0)
		};
		brazo.SetSurfaceOverrideMaterial(0, matOscuro);
		g.AddChild(brazo);
		// Colgante sobre el carril
		var colgante = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.12f, 1f, 0.12f) },
			Position = new Vector3(carril, 5.1f, 0)
		};
		colgante.SetSurfaceOverrideMaterial(0, matOscuro);
		g.AddChild(colgante);
		// Cabezal con las 3 lámparas mirando a los conductores (+Z local)
		var caja = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.7f, 1.8f, 0.5f) },
			Position = new Vector3(carril, 3.7f, 0)
		};
		caja.SetSurfaceOverrideMaterial(0, new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.09f) });
		g.AddChild(caja);

		lampRojos.Add(CrearLampara(g, new Vector3(carril, 4.25f, 0.26f)));
		lampAmarillos.Add(CrearLampara(g, new Vector3(carril, 3.7f, 0.26f)));
		lampVerdes.Add(CrearLampara(g, new Vector3(carril, 3.15f, 0.26f)));
		ActualizarLuces();
	}

	MeshInstance3D CrearLampara(Node3D padre, Vector3 pos)
	{
		var m = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f },
			Position = pos
		};
		m.SetSurfaceOverrideMaterial(0, matOff);
		padre.AddChild(m);
		// Visera sobre la lámpara
		var visera = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.07f, 0.42f) },
			Position = pos + new Vector3(0, 0.22f, 0.04f)
		};
		visera.SetSurfaceOverrideMaterial(0, new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.09f), Roughness = 0.7f });
		padre.AddChild(visera);
		return m;
	}

	void ActualizarLuces()
	{
		foreach (var l in lampRojos) l.SetSurfaceOverrideMaterial(0, Estado == "ROJO" ? matRojoOn : matOff);
		foreach (var l in lampAmarillos) l.SetSurfaceOverrideMaterial(0, Estado == "AMARILLO" ? matAmarilloOn : matOff);
		foreach (var l in lampVerdes) l.SetSurfaceOverrideMaterial(0, Estado == "VERDE" ? matVerdeOn : matOff);
	}
}
