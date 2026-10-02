using Godot;

/// <summary>
/// Paso 4: mobiliario urbano procedural — árboles, faroles, bancos y cestos.
/// Todo por código, sin colisión (decorado fuera del paso).
/// </summary>
public partial class ProcProps : Node3D
{
	static readonly Color Tronco = new(0.4f, 0.28f, 0.15f);
	static readonly Color Hoja1 = new(0.2f, 0.5f, 0.22f);
	static readonly Color Hoja2 = new(0.3f, 0.58f, 0.25f);
	static readonly Color Hierro = new(0.18f, 0.18f, 0.2f);
	static readonly Color Madera = new(0.5f, 0.35f, 0.2f);

	public override void _Ready()
	{
		// Árboles en las veredas (lejos del puesto y de las sendas)
		Arbol("A1", new Vector3(12.5f, 0.3f, 6), 1f);
		Arbol("A2", new Vector3(5, 0.3f, 13.5f), 0.85f);
		Arbol("A3", new Vector3(-12.5f, 0.3f, 6), 1.1f);
		Arbol("A4", new Vector3(-5, 0.3f, 13.5f), 0.9f);
		Arbol("A5", new Vector3(12.5f, 0.3f, -6), 1f);
		Arbol("A6", new Vector3(-12.5f, 0.3f, -6), 0.95f);
		// Faroles en las veredas, apartados de los semáforos
		Farol("F1", new Vector3(4.6f, 0, 10), -1);
		Farol("F2", new Vector3(-4.6f, 0, -10), 1);
		// Bancos mirando a la calle
		Banco("B1", new Vector3(10, 0.3f, 7), 180);
		Banco("B2", new Vector3(-10, 0.3f, 7), 180);
		// Cestos separados entre sí y del puesto
		Cesto("C1", new Vector3(13.2f, 0.3f, 9));
		Cesto("C2", new Vector3(-8.6f, 0.3f, 8));
	}

	void Arbol(string nombre, Vector3 basePos, float escala)
	{
		var tronco = new MeshInstance3D
		{
			Name = nombre + "T",
			Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 1.8f },
			Position = basePos + new Vector3(0, 0.9f, 0) * escala,
			Scale = new Vector3(escala, escala, escala)
		};
		tronco.SetSurfaceOverrideMaterial(0, Mat(Tronco));
		AddChild(tronco);
		// Copa con 2 esferas desparejas
		var copa1 = new MeshInstance3D
		{
			Name = nombre + "C1",
			Mesh = new SphereMesh { Radius = 1.1f, Height = 2.2f },
			Position = basePos + new Vector3(0, 2.6f, 0) * escala,
			Scale = Vector3.One * escala
		};
		copa1.SetSurfaceOverrideMaterial(0, Mat(Hoja1, 0.9f));
		AddChild(copa1);
		var copa2 = new MeshInstance3D
		{
			Name = nombre + "C2",
			Mesh = new SphereMesh { Radius = 0.75f, Height = 1.5f },
			Position = basePos + new Vector3(0.5f, 3.3f, 0.3f) * escala,
			Scale = Vector3.One * escala
		};
		copa2.SetSurfaceOverrideMaterial(0, Mat(Hoja2, 0.9f));
		AddChild(copa2);
	}

	/// <param name="lado">hacia dónde mira el brazo (-1/+1 en X)</param>
	void Farol(string nombre, Vector3 basePos, int lado)
	{
		var poste = new MeshInstance3D
		{
			Name = nombre + "P",
			Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.12f, Height = 5f },
			Position = basePos + new Vector3(0, 2.5f, 0)
		};
		poste.SetSurfaceOverrideMaterial(0, Mat(Hierro, 0.5f));
		AddChild(poste);
		AgregarCaja(nombre + "Brazo", new Vector3(1.2f, 0.1f, 0.1f),
			basePos + new Vector3(lado * 0.6f, 4.9f, 0), Hierro);
		var lampara = new MeshInstance3D
		{
			Name = nombre + "L",
			Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f },
			Position = basePos + new Vector3(lado * 1.2f, 4.7f, 0)
		};
		var matLuz = new StandardMaterial3D
		{
			AlbedoColor = new Color(1, 0.95f, 0.8f),
			EmissionEnabled = true, Emission = new Color(1, 0.95f, 0.8f), EmissionEnergyMultiplier = 1.5f
		};
		lampara.SetSurfaceOverrideMaterial(0, matLuz);
		AddChild(lampara);
	}

	/// <param name="rotY">hacia dónde mira (grados)</param>
	void Banco(string nombre, Vector3 basePos, float rotY)
	{
		var banco = new Node3D { Name = nombre, Position = basePos, RotationDegrees = new Vector3(0, rotY, 0) };
		AddChild(banco);
		void Parte(string n, Vector3 size, Vector3 pos)
		{
			var m = new MeshInstance3D { Name = n, Mesh = new BoxMesh { Size = size }, Position = pos };
			m.SetSurfaceOverrideMaterial(0, Mat(Madera));
			banco.AddChild(m);
		}
		Parte(nombre + "Asiento", new Vector3(1.8f, 0.08f, 0.5f), new Vector3(0, 0.45f, 0));
		Parte(nombre + "Respaldo", new Vector3(1.8f, 0.5f, 0.08f), new Vector3(0, 0.8f, 0.25f));
		Parte(nombre + "Pata1", new Vector3(0.08f, 0.45f, 0.45f), new Vector3(-0.8f, 0.22f, 0));
		Parte(nombre + "Pata2", new Vector3(0.08f, 0.45f, 0.45f), new Vector3(0.8f, 0.22f, 0));
	}

	void Cesto(string nombre, Vector3 basePos)
	{
		var cesto = new MeshInstance3D
		{
			Name = nombre,
			Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.26f, Height = 0.7f },
			Position = basePos + new Vector3(0, 0.35f, 0)
		};
		cesto.SetSurfaceOverrideMaterial(0, Mat(new Color(0.15f, 0.35f, 0.2f)));
		AddChild(cesto);
	}

	void AgregarCaja(string nombre, Vector3 size, Vector3 pos, Color color)
	{
		var m = new MeshInstance3D { Name = nombre, Mesh = new BoxMesh { Size = size }, Position = pos };
		m.SetSurfaceOverrideMaterial(0, Mat(color));
		AddChild(m);
	}

	static StandardMaterial3D Mat(Color c, float rough = 0.85f) =>
		new() { AlbedoColor = c, Roughness = rough };
}
