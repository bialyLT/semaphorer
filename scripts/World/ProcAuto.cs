using Godot;

/// <summary>
/// Paso 4: fábrica procedural de autos low-poly con detalle — carrocería +
/// cabina, parabrisas ("Parabrisas", punto de interacción), ópticas delanteras
/// y traseras emisivas, paragolpes, cartel de taxi / franja del bus y ruedas
/// con tasa. Frente del auto = +Z. Sin Blender, sin imports.
/// </summary>
public static class ProcAuto
{
	/// <summary>Tamaño de la caja de colisión según tipo.</summary>
	public static Vector3 Tamano(string tipo) =>
		tipo == "bus" ? new Vector3(2.2f, 1.6f, 7f) : new Vector3(1.8f, 1.6f, 4f);

	/// <summary>
	/// Cuerpo para que el jugador no atraviese el auto. Va como hijo del auto
	/// (que se mueve por código): AnimatableBody3D acompaña sin motor físico.
	/// </summary>
	public static AnimatableBody3D Colision(string tipo)
	{
		var t = Tamano(tipo);
		var body = new AnimatableBody3D { Name = "Colision" };
		body.AddChild(new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = t },
			Position = new Vector3(0, t.Y / 2f, 0)
		});
		return body;
	}
	public static Node3D Build(string tipo, Color color)
	{
		bool esBus = tipo == "bus";
		float largo = esBus ? 7f : 4f;
		float ancho = esBus ? 2.2f : 1.8f;

		var root = new Node3D { Name = $"Auto_{tipo}" };
		var matCar = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.4f, Metallic = 0.2f };
		var matVidrio = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.6f, 0.85f, 1f, 0.5f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.1f
		};
		var matRueda = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.05f), Roughness = 0.9f };
		var matTasa = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.7f, 0.72f), Roughness = 0.3f, Metallic = 0.6f };
		var matOptica = Emisiva(new Color(1, 0.95f, 0.8f), 2f);
		var matTrasera = Emisiva(new Color(1, 0.1f, 0.1f), 2f);
		var matParagolpe = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.22f), Roughness = 0.6f };

		void Parte(string nombre, Mesh mesh, Vector3 pos, Material mat, Vector3? rot = null)
		{
			var m = new MeshInstance3D { Name = nombre, Mesh = mesh, Position = pos };
			if (rot.HasValue) m.RotationDegrees = rot.Value;
			m.SetSurfaceOverrideMaterial(0, mat);
			root.AddChild(m);
		}

		Vector3 bodySize = new(ancho, esBus ? 1.6f : 0.7f, largo);
		Parte("Carroceria", new BoxMesh { Size = bodySize }, new Vector3(0, 0.8f, 0), matCar);

		if (!esBus)
		{
			Parte("Cabina", new BoxMesh { Size = new Vector3(1.6f, 0.6f, 2f) },
				new Vector3(0, 1.4f, 0.2f), matCar);
			if (tipo == "taxi")
			{
				// Cartel del techo
				Parte("CartelBase", new BoxMesh { Size = new Vector3(0.5f, 0.08f, 0.3f) },
					new Vector3(0, 1.74f, 0.2f), matParagolpe);
				Parte("Cartel", new BoxMesh { Size = new Vector3(0.44f, 0.22f, 0.24f) },
					new Vector3(0, 1.88f, 0.2f), Emisiva(new Color(1, 0.8f, 0.1f), 1f));
			}
			if (tipo == "patrulla")
			{
				// Baliza roja/azul (PatrolAI las hace parpadear por nombre)
				Parte("BalizaBase", new BoxMesh { Size = new Vector3(1f, 0.08f, 0.3f) },
					new Vector3(0, 1.74f, 0.2f), matParagolpe);
				Parte("BalizaR", new BoxMesh { Size = new Vector3(0.44f, 0.22f, 0.24f) },
					new Vector3(-0.27f, 1.88f, 0.2f), Emisiva(new Color(1, 0.1f, 0.1f), 2f));
				Parte("BalizaB", new BoxMesh { Size = new Vector3(0.44f, 0.22f, 0.24f) },
					new Vector3(0.27f, 1.88f, 0.2f), Emisiva(new Color(0.1f, 0.3f, 1), 0.2f));
			}
		}
		else
		{
			// Franja de ventanas del colectivo
			Parte("Ventanas", new BoxMesh { Size = new Vector3(ancho + 0.04f, 0.7f, largo - 1f) },
				new Vector3(0, 1.25f, 0), matVidrio);
		}

		// Parabrisas = punto de interacción para WindshieldJob
		Parte("Parabrisas", new BoxMesh { Size = new Vector3(1.5f, 0.55f, 0.08f) },
			new Vector3(0, esBus ? 1.6f : 1.35f, largo / 2f - (esBus ? 0.05f : 0.75f)),
			matVidrio, new Vector3(-15, 0, 0));

		// Ópticas delanteras (blancas) y traseras (rojas)
		foreach (float x in new float[] { -ancho / 2f + 0.3f, ancho / 2f - 0.3f })
		{
			Parte("Optica", new BoxMesh { Size = new Vector3(0.32f, 0.2f, 0.08f) },
				new Vector3(x, 0.75f, largo / 2f + 0.01f), matOptica);
			Parte("Trasera", new BoxMesh { Size = new Vector3(0.32f, 0.18f, 0.08f) },
				new Vector3(x, 0.75f, -largo / 2f - 0.01f), matTrasera);
		}
		// Paragolpes
		Parte("ParagolpeDel", new BoxMesh { Size = new Vector3(ancho + 0.1f, 0.25f, 0.15f) },
			new Vector3(0, 0.42f, largo / 2f + 0.05f), matParagolpe);
		Parte("ParagolpeTras", new BoxMesh { Size = new Vector3(ancho + 0.1f, 0.25f, 0.15f) },
			new Vector3(0, 0.42f, -largo / 2f - 0.05f), matParagolpe);

		// Ruedas con tasa
		float ruedaY = 0.35f;
		float dx = ancho / 2f;
		float dz = largo / 2f - 0.8f;
		foreach (var p in new Vector3[]
		{
			new(dx, ruedaY, dz), new(-dx, ruedaY, dz),
			new(dx, ruedaY, -dz), new(-dx, ruedaY, -dz)
		})
		{
			Parte("Rueda", new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.35f, Height = 0.25f },
				p, matRueda, new Vector3(0, 0, 90));
			Parte("Tasa", new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.15f, Height = 0.27f },
				p, matTasa, new Vector3(0, 0, 90));
		}
		return root;
	}

	static StandardMaterial3D Emisiva(Color c, float energia) =>
		new() { AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = energia };
}
