using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace AerolineaVIP.Models
{
    public class RegistroPasajerosViewModel
    {
        public int VueloId { get; set; }
        public vw_VueloDetalle Vuelo { get; set; }

        [Required(ErrorMessage = "Debe registrar al menos un pasajero.")]
        public List<PasajeroReservaViewModel> Pasajeros { get; set; }

        public RegistroPasajerosViewModel()
        {
            Pasajeros = new List<PasajeroReservaViewModel>();
        }
    }

    public class PasajeroReservaViewModel : IValidatableObject
    {
        [Range(1, 7, ErrorMessage = "El asiento seleccionado no es válido.")]
        public int NumeroAsiento { get; set; }

        public string CodigoAsiento { get; set; }
        public string Ubicacion { get; set; }

        [Required(ErrorMessage = "Seleccione el tipo de documento.")]
        [Display(Name = "Tipo de documento")]
        public string TipoDocumento { get; set; }

        [Required(ErrorMessage = "Ingrese el número de documento.")]
        [StringLength(20, MinimumLength = 5, ErrorMessage = "El documento debe tener entre 5 y 20 caracteres.")]
        [Display(Name = "Número de documento")]
        public string NumeroDocumento { get; set; }

        [Required(ErrorMessage = "Ingrese los nombres.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Los nombres deben tener entre 2 y 80 caracteres.")]
        [Display(Name = "Nombres")]
        public string Nombres { get; set; }

        [Required(ErrorMessage = "Ingrese los apellidos.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 80 caracteres.")]
        [Display(Name = "Apellidos")]
        public string Apellidos { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha de nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Required(ErrorMessage = "Ingrese el correo electrónico.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        [StringLength(120, ErrorMessage = "El correo no puede superar los 120 caracteres.")]
        [Display(Name = "Correo electrónico")]
        public string Correo { get; set; }

        [Required(ErrorMessage = "Ingrese el teléfono de contacto.")]
        [StringLength(25, MinimumLength = 7, ErrorMessage = "El teléfono debe tener entre 7 y 25 caracteres.")]
        [Display(Name = "Teléfono de contacto")]
        public string Telefono { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrWhiteSpace(NumeroDocumento) && NumeroDocumento.Any(c => !char.IsLetterOrDigit(c)))
            {
                yield return new ValidationResult("El documento solo puede contener letras y números.", new[] { nameof(NumeroDocumento) });
            }

            if (!string.IsNullOrWhiteSpace(Nombres) &&
                Nombres.Any(c => !char.IsLetter(c) && c != ' ' && c != '-' && c != '\''))
            {
                yield return new ValidationResult("Los nombres contienen caracteres no permitidos.", new[] { nameof(Nombres) });
            }

            if (!string.IsNullOrWhiteSpace(Apellidos) &&
                Apellidos.Any(c => !char.IsLetter(c) && c != ' ' && c != '-' && c != '\''))
            {
                yield return new ValidationResult("Los apellidos contienen caracteres no permitidos.", new[] { nameof(Apellidos) });
            }

            if (!string.IsNullOrWhiteSpace(Telefono) &&
                Telefono.Any(c => !char.IsDigit(c) && c != ' ' && c != '+' && c != '-' && c != '(' && c != ')'))
            {
                yield return new ValidationResult("El teléfono contiene caracteres no permitidos.", new[] { nameof(Telefono) });
            }

            if (!string.IsNullOrWhiteSpace(NumeroDocumento) &&
                string.Equals(TipoDocumento, "CEDULA", StringComparison.OrdinalIgnoreCase) &&
                !IdentityDocumentRules.EsCedulaEcuatorianaValida(NumeroDocumento))
            {
                yield return new ValidationResult("Ingrese una cédula ecuatoriana válida.", new[] { nameof(NumeroDocumento) });
            }

            if (FechaNacimiento.HasValue && FechaNacimiento.Value.Date > DateTime.Today)
            {
                yield return new ValidationResult("La fecha de nacimiento no puede ser futura.", new[] { nameof(FechaNacimiento) });
            }
        }
    }
}
