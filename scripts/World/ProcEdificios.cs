using Godot;

/// <summary>
/// Paso 4: edificios latinos low-poly en las 4 esquinas (fuera de calles y veredas).
/// Fachadas cálidas, ventanas, planta baja comercial con toldo y tinaco en el techo.
/// Todo por código con BoxMesh/CylinderMesh. Solo la base tiene colisión.
/// </summary>
public partial class ProcEdificios : Node3D
{
	/// <summary>Paletas por ciudad: 4 (fachada, toldo). C1 cálida, C2 fría moderna, C3 premium.</summary>
	static readonly (Color fachada, Color toldo)[][] Paletas = {
		new[] {
			(new Color(0.85f, 0.45f, 0.25f), new Color(0.2f, 0.5f, 0.7f)),
			(new Color(0.9f, 0.75f, 0.3f), new Color(0.7f, 0.2f, 0.2f)),
			(new Color(0.4f, 0.65f, 0.8f), new Color(0.25f, 0.35f, 0.25f)),
			(new Color(0.88f, 0.85f, 0.78f), new Color(0.85f, 0.45f, 0.25f)),
		},
		new[] {
			(new Color(0.55f, 0.62f, 0.72f), new Color(0.15f, 0.35f, 0.6f)),
			(new Color(0.35f, 0.42f, 0.52f), new Color(0.75f, 0.75f, 0.78f)),
			(new Color(0.62f, 0.68f, 0.75f), new Color(0.1f, 0.5f, 0.45f)),
			(new Color(0.45f, 0.5f, 0.58f), new Color(0.85f, 0.55f, 0.15f)),
		},
		new[] {
			(new Color(0.92f, 0.85f, 0.65f), new Color(0.45f, 0.1f, 0.15f)),
			(new Color(0.85f, 0.78f, 0.6f), new Color(0.1f, 0.15f, 0.35f)),
			(new Color(0.95f, 0.9f, 0.75f), new Color(0.5f, 0.35f, 0.1f)),
			(new Color(0.8f, 0.72f, 0.55f), new Color(0.2f, 0.2f, 0.25f)),
		},
	};

	public override void _Ready()
	{
		Reconstruir(SaveSystem.CargarCiudad());
	}

	/// <summary>Rehace los 4 edificios con la paleta de la ciudad (para viajes).</summary>
	public void Reconstruir(int ciudad)
	{
		foreach (var h in GetChildren()) { RemoveChild(h); h.QueueFree(); }
		var pal = Paletas[Mathf.Clamp(ciudad - 1, 0, Paletas.Length - 1)];
		Edificio("E1", new Vector3(11, 0, 16), new Vector3(7, 6, 6), pal[0].fachada, pal[0].toldo);
		Edificio("E2", new Vector3(-11, 0, 16), new Vector3(8, 9, 6), pal[1].fachada, pal[1].toldo);
		Edificio("E3", new Vector3(11, 0, -16), new Vector3(6, 5, 7), pal[2].fachada, pal[2].toldo);
		Edificio("E4", new Vector3(-11, 0, -16), new Vector3(9, 12, 6), pal[3].fachada, pal[3].toldo);
	}

	/// <param name="toldo">color del toldo comercial de planta baja</param>
	void Edificio(string nombre, Vector3 centro, Vector3 tam, Color fachada, Color toldo)
	{
		// Base con colisión (para no atravesar caminando)
		AgregarCaja(nombre, tam, centro + new Vector3(0, tam.Y / 2f, 0), fachada, true);
		// Cornisa del techo
		AgregarCaja(nombre + "Cornisa", new Vector3(tam.X + 0.4f, 0.3f, tam.Z + 0.4f),
			centro + new Vector3(0, tam.Y + 0.15f, 0), new Color(0.95f, 0.93f, 0.88f), false);
		// Tinaco (tanque de agua) en el techo
		var tinaco = new MeshInstance3D
		{
			Name = nombre + "Tinaco",
			Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 1.2f },
			Position = centro + new Vector3(tam.X / 4f, tam.Y + 0.6f, 0)
		};
		tinaco.SetSurfaceOverrideMaterial(0, new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.15f, 0.16f), Roughness = 0.7f });
		AddChild(tinaco);

		// Ventanas en la cara que mira al cruce (hacia el origen en X y Z)
		float caraX = centro.X > 0 ? centro.X - tam.X / 2f - 0.06f : centro.X + tam.X / 2f + 0.06f;
		float caraZ = centro.Z > 0 ? centro.Z - tam.Z / 2f - 0.06f : centro.Z + tam.Z / 2f + 0.06f;
		var matVidrio = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.25f, 0.35f), Roughness = 0.2f, Metallic = 0.4f };
		int pisos = Mathf.Max(1, Mathf.FloorToInt(tam.Y / 3f));
		int cols = Mathf.Max(2, Mathf.FloorToInt(tam.X / 2.2f));
		int colsLat = Mathf.Max(2, Mathf.FloorToInt(tam.Z / 2.2f));
		for (int p = 1; p < pisos; p++) // planta baja es local comercial, sin ventanas
		{
			for (int c = 0; c < cols; c++)
			{
				float x = centro.X + (c - (cols - 1) / 2f) * 2f;
				// Ventana en cara Z (frente)
				AgregarCaja($"{nombre}V{p}_{c}a", new Vector3(0.9f, 1.1f, 0.12f),
					new Vector3(x, p * 3f, caraZ), matVidrio, false);
			}
			for (int c = 0; c < colsLat; c++)
			{
				float z = centro.Z + (c - (colsLat - 1) / 2f) * 2f;
				// Ventana en cara X (lateral)
				AgregarCaja($"{nombre}W{p}_{c}a", new Vector3(0.12f, 1.1f, 0.9f),
					new Vector3(caraX, p * 3f, z), matVidrio, false);
			}
		}
		// Local comercial: franja + toldo a rayas en planta baja
		AgregarCaja(nombre + "Local", new Vector3(tam.X - 1f, 1.2f, 0.12f),
			new Vector3(centro.X, 1.1f, caraZ), new Color(0.25f, 0.25f, 0.28f), false);
		for (int c = 0; c < cols; c++)
		{
			float x = centro.X + (c - (cols - 1) / 2f) * 2f;
			float zFuera = caraZ + (centro.Z > 0 ? -0.5f : 0.5f);
			AgregarCaja($"{nombre}Toldo{c}", new Vector3(1.4f, 0.1f, 1f),
				new Vector3(x, 2.1f, zFuera), c % 2 == 0 ? toldo : new Color(0.92f, 0.92f, 0.92f), false);
		}
	}

	void AgregarCaja(string nombre, Vector3 size, Vector3 pos, Color color, bool conColision)
	{
		AgregarCaja(nombre, size, pos, new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f }, conColision);
	}

	void AgregarCaja(string nombre, Vector3 size, Vector3 pos, Material mat, bool conColision)
	{
		var m = new MeshInstance3D { Name = nombre, Mesh = new BoxMesh { Size = size }, Position = pos };
		m.SetSurfaceOverrideMaterial(0, mat);
		AddChild(m);
		if (!conColision) return;
		var body = new StaticBody3D { Name = nombre + "_Col" };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = pos });
		AddChild(body);
	}
}
