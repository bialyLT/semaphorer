using System;

/// <summary>
/// Contrato para los 3 trabajos (limpiavidrios, malabares, venta).
/// Los 3 comparten targeting (auto detenido en rojo) y contadores del
/// auto (Golpes/Atendido); cada uno pone su ritmo, su pago y su sabor.
/// </summary>
public interface IJob
{
	string Nombre { get; }
	/// <summary>Progreso 0..1 del servicio actual (para el HUD).</summary>
	float Progreso01 { get; }
	string UltimoMensaje { get; }
	/// <summary>Tono del último mensaje: "info" | "ok" | "mal".</summary>
	string UltimoTono { get; }
	bool PuedeTrabajar();
	/// <summary>Por qué no se puede trabajar ahora (para el HUD).</summary>
	string Motivo();
	/// <returns>pago en monedas si el golpe/acto completó un servicio, 0 si no</returns>
	int Actuar();
	void Reset();
	/// <summary>Se emite cuando un verde descarta progreso a medias.</summary>
	event Action? ProgresoReseteado;
	/// <summary>Se emite cuando un amable da changüí en verde.</summary>
	event Action? GraciaOtorgada;
}
