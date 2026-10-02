using Godot;

/// <summary>
/// Rig de cámara híbrida: V alterna 1ª (ojos) y 3ª (hombro).
/// El Player rota yaw, el Rig rota pitch con el mouse.
/// Bloque A: sacudida con trauma (ladrones/decomiso) + transición
/// suave al alternar. El shake mueve la cámara, no la simulación.
/// </summary>
public partial class CameraRig : Node3D
{
	public bool PrimeraPersona { get; private set; } = true;
	Camera3D? cam;
	float pitch = 0f;
	[Export] public float Sensibilidad = 0.003f;

	readonly Vector3 offsetFPS = new(0, 1.6f, 0);
	readonly Vector3 offsetTPS = new(0, 2.2f, 3.5f);

	float trauma;
	float fovBase = 75f;
	Tween? tweenOffset;

	public override void _Ready()
	{
		cam = GetNodeOrNull<Camera3D>("Camera3D");
		if (cam != null) fovBase = cam.Fov;
		AplicarOffset(true);
	}

	public void Alternar()
	{
		PrimeraPersona = !PrimeraPersona;
		AplicarOffset(false);
	}

	/// <summary>Suma sacudida 0..1 (se acumula y decae sola). Robo chico ~0.3, decomiso ~0.8.</summary>
	public void AddShake(float cantidad)
	{
		trauma = Mathf.Clamp(trauma + cantidad, 0f, 1f);
	}

	/// <summary>Golpe corto: shake + punch de FOV que vuelve solo.</summary>
	public void Punch(float fuerza = 0.5f)
	{
		AddShake(fuerza);
		if (cam == null) return;
		cam.Fov = fovBase - fuerza * 4f;
		var tw = CreateTween();
		tw.TweenProperty(cam, "fov", fovBase, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	public void SumarPitch(float relativaY)
	{
		pitch = Mathf.Clamp(pitch - relativaY * Ajustes.Sensibilidad, -1.2f, 1.2f);
		Rotation = new Vector3(pitch, 0, 0);
	}

	void AplicarOffset(bool instantaneo)
	{
		Vector3 destino = PrimeraPersona ? offsetFPS : offsetTPS;
		if (instantaneo || GetTree() == null)
		{
			Position = destino;
			return;
		}
		tweenOffset?.Kill();
		tweenOffset = CreateTween();
		tweenOffset.TweenProperty(this, "position", destino, 0.25)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
	}

	public override void _Process(double delta)
	{
		if (cam == null) return;
		if (trauma > 0f)
		{
			trauma = Mathf.Max(0f, trauma - (float)delta * 1.6f);
			float s = trauma * trauma;
			// Shake direccional aleatorio que decae: H/V offset, no mueve al jugador.
			cam.HOffset = (float)GD.RandRange(-1.0, 1.0) * s * 0.35f;
			cam.VOffset = (float)GD.RandRange(-1.0, 1.0) * s * 0.35f;
			if (trauma <= 0f) { cam.HOffset = 0f; cam.VOffset = 0f; }
		}
	}
}
