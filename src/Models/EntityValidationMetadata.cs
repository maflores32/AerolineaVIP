using System;
using System.ComponentModel.DataAnnotations;

namespace AerolineaVIP.Models
{
    [MetadataType(typeof(AeropuertoMetadata))]
    public partial class Aeropuerto
    {
    }

    public class AeropuertoMetadata
    {
        [Display(Name = "Código IATA")]
        [Required(ErrorMessage = "El código IATA es obligatorio.")]
        [StringLength(3, MinimumLength = 3, ErrorMessage = "El código IATA debe tener exactamente tres letras.")]
        [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "El código IATA solo admite tres letras.")]
        public string CodigoIATA { get; set; }

        [Display(Name = "Nombre")]
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
        public string Nombre { get; set; }

        [Display(Name = "Localidad")]
        [Required(ErrorMessage = "La localidad es obligatoria.")]
        [StringLength(100, ErrorMessage = "La localidad no puede superar 100 caracteres.")]
        public string Localidad { get; set; }
    }

    [MetadataType(typeof(RutaMetadata))]
    public partial class Ruta : IValidatableObject
    {
        public System.Collections.Generic.IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (AeropuertoOrigenId == AeropuertoDestinoId)
            {
                yield return new ValidationResult("El origen y el destino deben ser distintos.", new[] { "AeropuertoDestinoId" });
            }
        }
    }

    public class RutaMetadata
    {
        [Display(Name = "Aeropuerto de origen")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un aeropuerto de origen.")]
        public int AeropuertoOrigenId { get; set; }

        [Display(Name = "Aeropuerto de destino")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un aeropuerto de destino.")]
        public int AeropuertoDestinoId { get; set; }

        [Display(Name = "Tarifa referencial (USD)")]
        [Range(typeof(decimal), "0.01", "10000", ErrorMessage = "La tarifa debe ser mayor que cero y no superar USD 10 000.")]
        public decimal TarifaReferencial { get; set; }
    }

    [MetadataType(typeof(FrecuenciaMetadata))]
    public partial class Frecuencia : IValidatableObject
    {
        public System.Collections.Generic.IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (HoraLlegada <= HoraSalida)
            {
                yield return new ValidationResult(
                    "La hora de llegada debe ser posterior a la de salida.",
                    new[] { "HoraLlegada" });
            }
        }
    }

    public class FrecuenciaMetadata
    {
        [Display(Name = "Ruta")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una ruta.")]
        public int RutaId { get; set; }

        [Display(Name = "Día de operación")]
        [Range(1, 7, ErrorMessage = "El día de operación debe estar entre 1 y 7.")]
        public byte DiaSemana { get; set; }

        [Display(Name = "Hora de salida")]
        [Required(ErrorMessage = "Indique la hora de salida.")]
        public TimeSpan HoraSalida { get; set; }

        [Display(Name = "Hora de llegada")]
        [Required(ErrorMessage = "Indique la hora de llegada.")]
        public TimeSpan HoraLlegada { get; set; }

    }
}
