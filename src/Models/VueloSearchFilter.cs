using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;

namespace AerolineaVIP.Models
{
    public class VueloSearchFilter : IValidatableObject
    {
        public string Origen { get; set; }
        public string Destino { get; set; }
        [DataType(DataType.Date)]
        public DateTime? Fecha { get; set; }
        [Range(1, 7, ErrorMessage = "La cantidad de pasajeros debe estar entre 1 y 7.")]
        public int? Pasajeros { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Pasajeros.HasValue)
                Pasajeros = 1;
            if (Fecha.HasValue && Fecha.Value.Date < DateTime.Today)
                yield return new ValidationResult("La fecha de viaje no puede ser anterior a hoy.", new[] { nameof(Fecha) });
            if (!string.IsNullOrWhiteSpace(Origen) && !string.IsNullOrWhiteSpace(Destino) && string.Equals(Origen, Destino, StringComparison.OrdinalIgnoreCase))
                yield return new ValidationResult("El aeropuerto de origen y el de destino no pueden ser el mismo.", new[] { nameof(Destino) });
        }

        public IEnumerable<ValidationResult> Validar(AerolineaVIP_Entities db)
        {
            NormalizarFiltros();
            foreach (var error in ((IValidatableObject)this).Validate(new ValidationContext(this))) yield return error;
            if (!string.IsNullOrWhiteSpace(Origen) && !db.Aeropuertoes.Any(a => a.Activo && a.CodigoIATA == Origen))
                yield return new ValidationResult("El aeropuerto de origen seleccionado no es valido.", new[] { nameof(Origen) });
            if (!string.IsNullOrWhiteSpace(Destino) && !db.Aeropuertoes.Any(a => a.Activo && a.CodigoIATA == Destino))
                yield return new ValidationResult("El aeropuerto de destino seleccionado no es valido.", new[] { nameof(Destino) });
        }

        public void NormalizarFiltros()
        {
            Origen = string.IsNullOrWhiteSpace(Origen) ? null : Origen.Trim().ToUpperInvariant();
            Destino = string.IsNullOrWhiteSpace(Destino) ? null : Destino.Trim().ToUpperInvariant();
        }

        public List<vw_VueloDetalle> Buscar(AerolineaVIP_Entities db, out Dictionary<int, vw_DisponibilidadVuelo> disponibilidad)
        {
            NormalizarFiltros();
            int pasajeros = Pasajeros ?? 1;
            var vuelos = db.vw_VueloDetalle.Where(v => (v.EstadoVueloCodigo == "PROGRAMADO" || v.EstadoVueloCodigo == "CONFIRMADO") && DbFunctions.TruncateTime(v.FechaOperacion) >= DateTime.Today);
            if (!string.IsNullOrWhiteSpace(Origen))
            {
                var origenId = db.Aeropuertoes.Where(a => a.Activo && a.CodigoIATA == Origen).Select(a => a.AeropuertoId).FirstOrDefault();
                vuelos = vuelos.Where(v => v.AeropuertoOrigenId == origenId);
            }
            if (!string.IsNullOrWhiteSpace(Destino))
            {
                var destinoId = db.Aeropuertoes.Where(a => a.Activo && a.CodigoIATA == Destino).Select(a => a.AeropuertoId).FirstOrDefault();
                vuelos = vuelos.Where(v => v.AeropuertoDestinoId == destinoId);
            }
            if (Fecha.HasValue)
            {
                var fecha = Fecha.Value.Date;
                vuelos = vuelos.Where(v => DbFunctions.TruncateTime(v.FechaOperacion) == fecha);
            }
            var resultados = vuelos.OrderBy(v => v.FechaOperacion).ThenBy(v => v.HoraSalida).ToList();
            var mapaDisponibilidad = db.vw_DisponibilidadVuelo.ToDictionary(d => d.VueloId, d => d);
            disponibilidad = mapaDisponibilidad;
            return resultados.Where(v => !mapaDisponibilidad.ContainsKey(v.VueloId) || (mapaDisponibilidad[v.VueloId].Disponibles ?? 7) >= pasajeros).ToList();
        }
    }
}
