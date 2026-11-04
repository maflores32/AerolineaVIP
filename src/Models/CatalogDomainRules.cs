using System.Collections.Generic;
using System.Linq;

namespace AerolineaVIP.Models
{
    public class DomainValidationError
    {
        public string PropertyName { get; set; }
        public string Message { get; set; }
    }

    public static class CatalogDomainRules
    {
        public static string NormalizeIata(string code) { return (code ?? string.Empty).Trim().ToUpperInvariant(); }

        public static IEnumerable<DomainValidationError> ValidateAeropuerto(AerolineaVIP_Entities db, Aeropuerto aeropuerto)
        {
            if (db.Aeropuertoes.Any(a => a.AeropuertoId != aeropuerto.AeropuertoId && a.CodigoIATA == aeropuerto.CodigoIATA)) yield return new DomainValidationError { PropertyName = "CodigoIATA", Message = "Ya existe otro aeropuerto con ese codigo IATA." };
        }

        public static IEnumerable<DomainValidationError> ValidateRuta(AerolineaVIP_Entities db, Ruta ruta)
        {
            if (!db.Aeropuertoes.Any(a => a.AeropuertoId == ruta.AeropuertoOrigenId && a.Activo)) yield return new DomainValidationError { PropertyName = "AeropuertoOrigenId", Message = "Seleccione un aeropuerto de origen activo." };
            if (!db.Aeropuertoes.Any(a => a.AeropuertoId == ruta.AeropuertoDestinoId && a.Activo)) yield return new DomainValidationError { PropertyName = "AeropuertoDestinoId", Message = "Seleccione un aeropuerto de destino activo." };
            if (db.Rutas.Any(r => r.RutaId != ruta.RutaId && r.AeropuertoOrigenId == ruta.AeropuertoOrigenId && r.AeropuertoDestinoId == ruta.AeropuertoDestinoId)) yield return new DomainValidationError { PropertyName = "AeropuertoDestinoId", Message = "Ya existe una ruta con ese origen y destino." };
        }

        public static IEnumerable<DomainValidationError> ValidateFrecuencia(AerolineaVIP_Entities db, Frecuencia frecuencia)
        {
            if (!db.Rutas.Any(r => r.RutaId == frecuencia.RutaId && r.Activa && r.Aeropuerto.Activo && r.Aeropuerto1.Activo)) yield return new DomainValidationError { PropertyName = "RutaId", Message = "Seleccione una ruta activa con aeropuertos activos." };
            if (db.Frecuencias.Any(f => f.FrecuenciaId != frecuencia.FrecuenciaId && f.RutaId == frecuencia.RutaId && f.DiaSemana == frecuencia.DiaSemana)) yield return new DomainValidationError { PropertyName = "DiaSemana", Message = "Ya existe una frecuencia para esa ruta y dia." };
        }

        public static bool PuedeEliminarAeropuerto(AerolineaVIP_Entities db, int id) { return !db.Rutas.Any(r => r.AeropuertoOrigenId == id || r.AeropuertoDestinoId == id); }
        public static bool PuedeEliminarRuta(AerolineaVIP_Entities db, int id) { return !db.Frecuencias.Any(f => f.RutaId == id); }
        public static bool PuedeEliminarFrecuencia(AerolineaVIP_Entities db, int id) { return !db.Vueloes.Any(v => v.FrecuenciaId == id); }

        public static bool PuedeEditarDefinicionFrecuencia(AerolineaVIP_Entities db, Frecuencia actual, Frecuencia propuesta)
        {
            if (!db.Vueloes.Any(v => v.FrecuenciaId == actual.FrecuenciaId)) return true;
            return actual.RutaId == propuesta.RutaId && actual.DiaSemana == propuesta.DiaSemana && actual.HoraSalida == propuesta.HoraSalida && actual.HoraLlegada == propuesta.HoraLlegada && !propuesta.Activa;
        }

        public static string ValidarEdicionFrecuencia(AerolineaVIP_Entities db, Frecuencia actual, Frecuencia propuesta)
        {
            return PuedeEditarDefinicionFrecuencia(db, actual, propuesta) ? null : "No se pueden cambiar la ruta ni los horarios de una frecuencia con vuelos generados. Solo puede desactivarla.";
        }
    }
}
