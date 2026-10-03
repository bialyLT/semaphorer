using Godot;

/// <summary>
/// Cruce gris procedural: suelo, 2 calles, veredas, senda peatonal.
/// Todo por código con BoxMesh. Paso 4 lo vuelve low-poly bonito.
/// </summary>
public partial class ProcCalle : Node3D
{
	public override void _Ready()
	{
		AgregarCaja("Suelo", new Vector3(60, 0.2f, 60), new Vector3(0, -0.1f, 0), new Color(0.25f, 0.28f, 0.25f));
		AgregarCaja("CalleNS", new Vector3(8, 0.22f, 60), new Vector3(0, 0, 0), new Color(0.12f, 0.12f, 0.13f));
		AgregarCaja("CalleEO", new Vector3(60, 0.22f, 8), new Vector3(0, 0, 0), new Color(0.12f, 0.12f, 0.13f));
		// Veredas en las 4 esquinas
		AgregarCaja("Vereda1", new Vector3(10, 0.4f, 10), new Vector3(9, 0.1f, 9), new Color(0.45f, 0.45f, 0.47f));
		AgregarCaja("Vereda2", new Vector3(10, 0.4f, 10), new Vector3(-9, 0.1f, 9), new Color(0.45f, 0.45f, 0.47f));
		AgregarCaja("Vereda3", new Vector3(10, 0.4f, 10), new Vector3(9, 0.1f, -9), new Color(0.45f, 0.45f, 0.47f));
		AgregarCaja("Vereda4", new Vector3(10, 0.4f, 10), new Vector3(-9, 0.1f, -9), new Color(0.45f, 0.45f, 0.47f));
		// Cordones amarillos en los bordes de las veredas que dan a la calle
		AgregarCaja("Cordon1", new Vector3(10, 0.45f, 0.3f), new Vector3(9, 0.12f, 3.85f), new Color(0.85f, 0.7f, 0.15f), false);
		AgregarCaja("Cordon2", new Vector3(10, 0.45f, 0.3f), new Vector3(-9, 0.12f, 3.85f), new Color(0.85f, 0.7f, 0.15f), false);
		AgregarCaja("Cordon3", new Vector3(10, 0.45f, 0.3f), new Vector3(9, 0.12f, -3.85f), new Color(0.85f, 0.7f, 0.15f), false);
		AgregarCaja("Cordon4", new Vector3(10, 0.45f, 0.3f), new Vector3(-9, 0.12f, -3.85f), new Color(0.85f, 0.7f, 0.15f), false);
		// Sendas peatonales ANTES del cruce para cada sentido
		// (el que se acerca ve: línea de detención → senda → esquina).
		// Carril 1 (viene de -Z): senda en z -12..-6, línea en -14.
		// Carril 2 (viene de +Z): senda en z 6..12, línea en +14.
		// Solo visual, sin colisión para no tropezar.
		for (int i = 0; i < 5; i++)
		{
			AgregarCaja($"SendaL1{i}", new Vector3(1f, 0.24f, 6f), new Vector3(-3f + i * 1.5f, 0.01f, -9f), new Color(0.85f, 0.85f, 0.85f), false);
			AgregarCaja($"SendaL2{i}", new Vector3(1f, 0.24f, 6f), new Vector3(-3f + i * 1.5f, 0.01f, 9f), new Color(0.85f, 0.85f, 0.85f), false);
		}
		// Líneas de detención de ambos carriles, antes de su senda (solo visual)
		AgregarCaja("LineaDet1", new Vector3(3.6f, 0.24f, 0.4f), new Vector3(-2, 0.01f, -14f), new Color(0.9f, 0.9f, 0.9f), false);
		AgregarCaja("LineaDet2", new Vector3(3.6f, 0.24f, 0.4f), new Vector3(2, 0.01f, 14f), new Color(0.9f, 0.9f, 0.9f), false);
		// Sendas de la transversal (cruzan la calle EO, franjas a lo largo de X)
		for (int i = 0; i < 5; i++)
		{
			AgregarCaja($"SendaE{i}", new Vector3(6f, 0.24f, 1f), new Vector3(-9f, 0.01f, -3f + i * 1.5f), new Color(0.85f, 0.85f, 0.85f), false);
			AgregarCaja($"SendaO{i}", new Vector3(6f, 0.24f, 1f), new Vector3(9f, 0.01f, -3f + i * 1.5f), new Color(0.85f, 0.85f, 0.85f), false);
		}
		// Líneas de la transversal (para la patrulla y futuro tráfico EO)
		AgregarCaja("LineaDetEO1", new Vector3(0.4f, 0.24f, 3.6f), new Vector3(-14f, 0.01f, 0), new Color(0.9f, 0.9f, 0.9f), false);
		AgregarCaja("LineaDetEO2", new Vector3(0.4f, 0.24f, 3.6f), new Vector3(14f, 0.01f, 0), new Color(0.9f, 0.9f, 0.9f), false);
		// Punteado central de ambas calles (solo visual)
		for (int i = -4; i <= 4; i++)
		{
			if (Mathf.Abs(i) < 3) continue; // libre el cruce y las sendas
			AgregarCaja($"GuionNS{i}", new Vector3(0.25f, 0.24f, 1.6f), new Vector3(0, 0.01f, i * 5f), new Color(0.9f, 0.8f, 0.2f), false);
			AgregarCaja($"GuionEO{i}", new Vector3(1.6f, 0.24f, 0.25f), new Vector3(i * 5f, 0.01f, 0), new Color(0.9f, 0.8f, 0.2f), false);
		}
		ConstruirEscondite();
		ConstruirLimites();
	}

	/// <summary>
	/// Muros invisibles en el borde del mundo jugable (±18): el jugador no
	/// puede caerse al vacío. Capa 4 solo para el jugador (los autos y
	/// peatones se mueven por posición, y el rayo del ladrón usa máscara 1:
	/// nada de eso los ve). Sin malla = invisibles.
	/// </summary>
	void ConstruirLimites()
	{
		Limite("LimiteE", new Vector3(0.5f, 6f, 37f), new Vector3(18f, 2f, 0), this);
		Limite("LimiteO", new Vector3(0.5f, 6f, 37f), new Vector3(-18f, 2f, 0), this);
		Limite("LimiteN", new Vector3(37f, 6f, 0.5f), new Vector3(0, 2f, 18f), this);
		Limite("LimiteS", new Vector3(37f, 6f, 0.5f), new Vector3(0, 2f, -18f), this);
	}

	static void Limite(string nombre, Vector3 tam, Vector3 pos, Node3D padre)
	{
		var body = new StaticBody3D { Name = nombre, CollisionLayer = 4, CollisionMask = 0 };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = tam }, Position = pos });
		padre.AddChild(body);
	}

	/// <summary>
	/// Escondite apartado en la vereda.
	/// Debe coincidir con Mejoras.Escondite. G guarda/saca la plata.
	/// </summary>
	void ConstruirEscondite()
	{
		Mejoras.CargarEscondite();
		var e = Mejoras.Escondite; // punta noroeste de Vereda1, despejado
		var azul = new Color(0.15f, 0.25f, 0.5f);
		var azulOsc = new Color(0.1f, 0.18f, 0.4f);
		// Cajón que tapa la mochila
		AgregarCaja("EsconditeCajon", new Vector3(0.9f, 0.7f, 0.6f), e + new Vector3(-0.5f, 0.35f, 0.1f), new Color(0.55f, 0.42f, 0.25f), false);
		// Mochila azul asomando detrás
		AgregarCaja("Escondite", new Vector3(0.45f, 0.6f, 0.32f), e + new Vector3(0.15f, 0.3f, -0.05f), azul, false);
		AgregarCaja("EsconditeBolsillo", new Vector3(0.3f, 0.32f, 0.1f), e + new Vector3(0.15f, 0.25f, 0.14f), azulOsc, false);
		// Lona encima
		AgregarCaja("EsconditeLona", new Vector3(1.3f, 0.08f, 0.9f), e + new Vector3(-0.1f, 0.78f, 0), new Color(0.35f, 0.4f, 0.3f), false);
	}

	void AgregarCaja(string nombre, Vector3 size, Vector3 pos, Color color, bool conColision = true)
	{
		var m = new MeshInstance3D { Name = nombre, Mesh = new BoxMesh { Size = size }, Position = pos };
		m.SetSurfaceOverrideMaterial(0, new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f });
		AddChild(m);
		if (!conColision) return;
		// Cuerpo estático con la misma forma/posición: sin esto el jugador atraviesa el suelo.
		var body = new StaticBody3D { Name = nombre + "_Col" };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = pos });
		AddChild(body);
	}
}
