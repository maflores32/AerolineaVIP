using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP.Models;
using Microsoft.AspNet.Identity;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize(Roles = "Cliente,Counter")]
    public class ReservasController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        private bool EsCliente
        {
            get { return User.IsInRole("Cliente"); }
        }

        // ============================================================
        // PANTALLA 8: /Reservas/SeleccionAsiento
        // ============================================================
        [HttpGet]
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult SeleccionAsiento(int? vueloId, int? pasajeros)
        {
            if (!vueloId.HasValue)
            {
                return RedirectToAction("Buscar", "Vuelos");
            }

            int numPasajeros = pasajeros ?? 1;
            if (numPasajeros < 1 || numPasajeros > 7)
            {
                TempData["ErrorAsientos"] = "Seleccione entre 1 y 7 pasajeros.";
                return RedirectToAction("Buscar", "Vuelos");
            }

            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId.Value);
            if (vuelo == null)
            {
                return HttpNotFound();
            }

            var asientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId.Value)
                .OrderBy(a => a.Fila)
                .ThenBy(a => a.Lado)
                .ToList();

            ViewBag.Asientos = asientos;
            ViewBag.Pasajeros = numPasajeros;

            return View(vuelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult SeleccionAsiento(int vueloId, int pasajeros, int[] asientosSeleccionados)
        {
            if (pasajeros < 1 || pasajeros > 7 || asientosSeleccionados == null || asientosSeleccionados.Length != pasajeros ||
                asientosSeleccionados.Distinct().Count() != asientosSeleccionados.Length ||
                asientosSeleccionados.Any(a => a < 1 || a > 7))
            {
                TempData["ErrorAsientos"] = "Debe seleccionar exactamente " + pasajeros + " asiento(s) disponible(s).";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = pasajeros });
            }

            string asientosCsv = string.Join(",", asientosSeleccionados);
            return RedirectToAction("Pasajeros", new { vueloId = vueloId, asientos = asientosCsv });
        }

        // ============================================================
        // PANTALLA 9: /Reservas/Pasajeros
        // ============================================================
        [HttpGet]
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult Pasajeros(int? vueloId, string asientos)
        {
            if (!vueloId.HasValue || string.IsNullOrEmpty(asientos))
            {
                return RedirectToAction("Buscar", "Vuelos");
            }

            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId.Value);
            if (vuelo == null)
            {
                return HttpNotFound();
            }

            var numerosAsiento = new List<byte>();
            foreach (string valor in asientos.Split(','))
            {
                byte numero;
                if (!byte.TryParse(valor.Trim(), out numero) || numero < 1 || numero > 7 || numerosAsiento.Contains(numero))
                {
                    TempData["ErrorAsientos"] = "La selección de asientos no es válida. Vuelva a elegirlos.";
                    return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId.Value });
                }
                numerosAsiento.Add(numero);
            }

            var infoAsientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId.Value && numerosAsiento.Contains(a.NumeroAsiento))
                .OrderBy(a => a.NumeroAsiento)
                .ToList();

            ViewBag.Vuelo = vuelo;
            ViewBag.InfoAsientos = infoAsientos;
            ViewBag.AsientosCsv = asientos;
            ViewBag.TiposDocumento = db.TipoDocumentoIdentidads.OrderBy(t => t.Codigo).ToList();

            return View();
        }

        // POST: /Reservas/GuardarReserva
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult GuardarReserva(
            int vueloId,
            int[] numeroAsiento,
            string[] tipoDocumento,
            string[] numeroDocumento,
            string[] nombres,
            string[] apellidos,
            DateTime?[] fechaNacimiento,
            string[] correo,
            string[] telefono)
        {
            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId);
            int cantidadPasajeros = numeroAsiento == null ? 0 : numeroAsiento.Length;
            if (vuelo == null || cantidadPasajeros < 1 || cantidadPasajeros > 7 ||
                tipoDocumento == null || tipoDocumento.Length != cantidadPasajeros ||
                numeroDocumento == null || numeroDocumento.Length != cantidadPasajeros ||
                nombres == null || nombres.Length != cantidadPasajeros ||
                apellidos == null || apellidos.Length != cantidadPasajeros ||
                correo == null || correo.Length != cantidadPasajeros ||
                telefono == null || telefono.Length != cantidadPasajeros ||
                (fechaNacimiento != null && fechaNacimiento.Length != cantidadPasajeros))
            {
                TempData["ErrorAsientos"] = "Revise los datos de todos los pasajeros e inténtelo de nuevo.";
                return RedirectToAction("Buscar", "Vuelos");
            }

            DateTime salida = vuelo.FechaOperacion.Date.Add(vuelo.HoraSalida);
            if (salida <= DateTime.Now)
            {
                TempData["ErrorAsientos"] = "No se pueden reservar vuelos que ya salieron.";
                return RedirectToAction("Buscar", "Vuelos");
            }

            if (numeroAsiento.Distinct().Count() != cantidadPasajeros || numeroAsiento.Any(n => n < 1 || n > 7))
            {
                TempData["ErrorAsientos"] = "La selección de asientos no es válida.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = cantidadPasajeros });
            }

            var asientosExistentes = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId)
                .Select(a => a.NumeroAsiento)
                .ToList();
            if (numeroAsiento.Any(n => !asientosExistentes.Contains((byte)n)))
            {
                TempData["ErrorAsientos"] = "Uno o más asientos no pertenecen a la cabina de este vuelo.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = cantidadPasajeros });
            }

            var documentosPasajeros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < cantidadPasajeros; i++)
            {
                string doc = (numeroDocumento[i] ?? "").Trim();
                string nombre = (nombres[i] ?? "").Trim();
                string apellido = (apellidos[i] ?? "").Trim();
                string email = (correo[i] ?? "").Trim();
                string telefonoPasajero = (telefono[i] ?? "").Trim();
                bool emailValido;
                try { emailValido = new System.Net.Mail.MailAddress(email).Address == email; }
                catch { emailValido = false; }
                bool documentoDuplicado = !documentosPasajeros.Add(tipoDocumento[i] + "|" + doc);
                bool cedulaInvalida = string.Equals(tipoDocumento[i], "CEDULA", StringComparison.OrdinalIgnoreCase) &&
                    !IdentityDocumentRules.EsCedulaEcuatorianaValida(doc);

                if (documentoDuplicado || cedulaInvalida || string.IsNullOrWhiteSpace(tipoDocumento[i]) || !db.TipoDocumentoIdentidads.Any(t => t.Codigo == tipoDocumento[i]) ||
                    doc.Length < 5 || doc.Length > 20 || doc.Any(c => !char.IsLetterOrDigit(c)) ||
                    nombre.Length < 2 || nombre.Length > 80 || nombre.Any(c => !char.IsLetter(c) && c != ' ' && c != '-' && c != '\'') ||
                    apellido.Length < 2 || apellido.Length > 80 || apellido.Any(c => !char.IsLetter(c) && c != ' ' && c != '-' && c != '\'') ||
                    !emailValido || email.Length > 120 || telefonoPasajero.Length < 7 || telefonoPasajero.Length > 25 ||
                    telefonoPasajero.Any(c => !char.IsDigit(c) && c != ' ' && c != '+' && c != '-' && c != '(' && c != ')') ||
                    (fechaNacimiento != null && fechaNacimiento[i].HasValue && fechaNacimiento[i].Value.Date > DateTime.Today))
                {
                    TempData["ErrorAsientos"] = "Revise nombres, documento, fecha de nacimiento, correo y teléfono del pasajero " + (i + 1) + ".";
                    return RedirectToAction("Pasajeros", new { vueloId = vueloId, asientos = string.Join(",", numeroAsiento) });
                }
            }

            // Validar estado del vuelo
            if (vuelo.EstadoVueloCodigo != "PROGRAMADO" && vuelo.EstadoVueloCodigo != "CONFIRMADO")
            {
                TempData["ErrorAsientos"] = "Este vuelo no permite nuevas reservas porque se encuentra en estado " + vuelo.EstadoVueloCodigo + ".";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = numeroAsiento.Length });
            }

            var listaAsientosBytes = numeroAsiento.Select(n => (byte)n).ToList();

            // Verificar si algún asiento ya fue reservado o comprado
            var ocupados = db.ReservaPasajeroVueloes
                .Where(r => r.VueloId == vueloId
                         && listaAsientosBytes.Contains(r.NumeroAsiento)
                         && (r.EstadoAsignacionCodigo == "CONFIRMADA" ||
                             (r.EstadoAsignacionCodigo == "PENDIENTE" &&
                              (!r.Reserva.FechaExpiracion.HasValue || r.Reserva.FechaExpiracion.Value > DateTime.Now))))
                .Any();

            if (ocupados)
            {
                TempData["ErrorAsientos"] = "Uno o más de los asientos seleccionados ya no están disponibles. Por favor elija otros.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = numeroAsiento.Length });
            }

            // TRANSACCIÓN ATÓMICA DE BASE DE DATOS (Si un asiento falla, se revierte toda la reserva)
            using (var transaccion = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    bool asientoOcupadoEnTransaccion = db.ReservaPasajeroVueloes
                        .Where(r => r.VueloId == vueloId
                                 && listaAsientosBytes.Contains(r.NumeroAsiento)
                                 && (r.EstadoAsignacionCodigo == "CONFIRMADA" ||
                                     (r.EstadoAsignacionCodigo == "PENDIENTE" &&
                                      (!r.Reserva.FechaExpiracion.HasValue || r.Reserva.FechaExpiracion.Value > DateTime.Now))))
                        .Any();
                    if (asientoOcupadoEnTransaccion)
                    {
                        transaccion.Rollback();
                        TempData["ErrorAsientos"] = "Uno o más asientos acaban de ser reservados. Elija otros.";
                        return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = cantidadPasajeros });
                    }

                    string codigoGenerado = "VIP-" + DateTime.Now.ToString("HHmmss");
                    while (db.Reservas.Any(r => r.CodigoReserva == codigoGenerado))
                    {
                        codigoGenerado = "VIP-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
                    }

                    var nuevaReserva = new Reserva
                    {
                        CodigoReserva = codigoGenerado,
                        UsuarioId = User.Identity.GetUserId(),
                        FechaCreacion = DateTime.Now,
                        FechaExpiracion = DateTime.Now.AddHours(24),
                        EstadoReservaCodigo = "PENDIENTE"
                    };

                    db.Reservas.Add(nuevaReserva);
                    db.SaveChanges(); // Guarda la reserva para obtener su ID autoincremental

                    for (int i = 0; i < numeroAsiento.Length; i++)
                    {
                        string tDoc = (tipoDocumento != null && i < tipoDocumento.Length) ? tipoDocumento[i] : "CEDULA";
                        string nDoc = (numeroDocumento != null && i < numeroDocumento.Length) ? (numeroDocumento[i] ?? "").Trim() : "";
                        string nom = (nombres != null && i < nombres.Length) ? (nombres[i] ?? "").Trim() : "";
                        string ape = (apellidos != null && i < apellidos.Length) ? (apellidos[i] ?? "").Trim() : "";
                        DateTime? fNac = (fechaNacimiento != null && i < fechaNacimiento.Length) ? fechaNacimiento[i] : null;
                        string mail = (correo != null && i < correo.Length) ? (correo[i] ?? "").Trim() : "";
                        string tel = (telefono != null && i < telefono.Length) ? (telefono[i] ?? "").Trim() : "";

                        // Validación básica de campos del pasajero
                        if (string.IsNullOrEmpty(nDoc) || string.IsNullOrEmpty(nom) || string.IsNullOrEmpty(ape))
                        {
                            throw new Exception("Debe completar los nombres, apellidos y número de documento de todos los pasajeros.");
                        }

                        var pasajero = db.Pasajeroes.FirstOrDefault(p => p.TipoDocumentoCodigo == tDoc && p.NumeroDocumento == nDoc);
                        if (pasajero == null)
                        {
                            pasajero = new Pasajero
                            {
                                TipoDocumentoCodigo = tDoc,
                                NumeroDocumento = nDoc,
                                Nombres = nom,
                                Apellidos = ape,
                                FechaNacimiento = fNac,
                                Correo = mail,
                                Telefono = tel,
                                FechaRegistro = DateTime.Now
                            };
                            db.Pasajeroes.Add(pasajero);
                            db.SaveChanges();
                        }
                        else
                        {
                            pasajero.Nombres = nom;
                            pasajero.Apellidos = ape;
                            if (fNac.HasValue) pasajero.FechaNacimiento = fNac;
                            if (!string.IsNullOrEmpty(mail)) pasajero.Correo = mail;
                            if (!string.IsNullOrEmpty(tel)) pasajero.Telefono = tel;
                            db.SaveChanges();
                        }

                        var asignacion = new ReservaPasajeroVuelo
                        {
                            ReservaId = nuevaReserva.ReservaId,
                            PasajeroId = pasajero.PasajeroId,
                            VueloId = vueloId,
                            NumeroAsiento = (byte)numeroAsiento[i],
                            ImporteTramo = vuelo.TarifaReferencial,
                            EstadoAsignacionCodigo = "PENDIENTE",
                            FechaAsignacion = DateTime.Now
                        };

                        db.ReservaPasajeroVueloes.Add(asignacion);
                    }

                    db.SaveChanges(); // Guarda todas las asignaciones
                    transaccion.Commit(); // CONFIRMAR TRANSACCIÓN COMPLETA

                    return RedirectToAction("Confirmar", new { id = nuevaReserva.ReservaId });
                }
                catch (Exception ex)
                {
                    transaccion.Rollback(); // REVERTIR TODO EN CASO DE ERROR

                    string errorAmigable = ex.Message;
                    if (ex.InnerException != null && ex.InnerException.InnerException != null)
                    {
                        errorAmigable = ex.InnerException.InnerException.Message;
                    }

                    TempData["ErrorAsientos"] = "No se pudo completar la reserva: " + errorAmigable;
                    return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = numeroAsiento.Length });
                }
            }
        }

        // ============================================================
        // PANTALLA 10: /Reservas/Confirmar/{id}
        // ============================================================

        // GET: /Reservas/Confirmar/5
        [HttpGet]
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult Confirmar(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("MisReservas");
            }

            var reserva = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .FirstOrDefault(r => r.ReservaId == id.Value);

            if (reserva == null)
            {
                return HttpNotFound();
            }

            // VALIDACIÓN DE TITULARIDAD: Solo el dueño o el personal puede ver la confirmación
            string userId = User.Identity.GetUserId();
            bool esPersonal = User.IsInRole("SuperUsuario") || User.IsInRole("Administrador") || User.IsInRole("Counter");
            if (!esPersonal && reserva.UsuarioId != userId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "No tiene permisos para ver esta reserva.");
            }

            var primerTramo = reserva.ReservaPasajeroVueloes.FirstOrDefault();
            vw_VueloDetalle vueloDetalle = null;
            if (primerTramo != null)
            {
                vueloDetalle = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == primerTramo.VueloId);
            }

            ViewBag.Vuelo = vueloDetalle;
            return View(reserva);
        }

        // ============================================================
        // PANTALLA 13: /Reservas/MisReservas
        // ============================================================
        // GET: /Reservas/MisReservas
        [HttpGet]
        public ActionResult MisReservas(string estado)
        {
            if (!EsCliente)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
            string userId = User.Identity.GetUserId();

            var query = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .Include(r => r.Compras)
                .AsQueryable();

            query = query.Where(r => r.UsuarioId == userId);

            if (!string.IsNullOrEmpty(estado))
            {
                query = query.Where(r => r.EstadoReservaCodigo == estado);
            }

            var lista = query.OrderByDescending(r => r.FechaCreacion).ToList();

            ViewBag.VuelosDict = db.vw_VueloDetalle.ToDictionary(v => v.VueloId, v => v);
            ViewBag.EstadoSeleccionado = estado;

            return View(lista);
        }

        [HttpGet]
        [SessionAuthorize(Roles = "Counter")]
        public ActionResult Gestion()
        {
            var reservas = db.Reservas
                .Include(r => r.ReservaPasajeroVueloes.Select(a => a.Pasajero))
                .OrderByDescending(r => r.FechaCreacion)
                .Take(100)
                .ToList();
            ViewBag.VuelosDict = db.vw_VueloDetalle.ToDictionary(v => v.VueloId, v => v);
            return View("MisReservas", reservas);
        }

        // ============================================================
        // PANTALLA 14: /Reservas/Gestionar/{id}
        // ============================================================
        // GET: /Reservas/Gestionar/5
        [HttpGet]
        public ActionResult Gestionar(int? id)
        {
            if (!EsCliente && !User.IsInRole("Counter"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
            if (!id.HasValue)
            {
                return RedirectToAction("MisReservas");
            }

            var reserva = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .Include(r => r.Compras)
                .FirstOrDefault(r => r.ReservaId == id.Value);

            if (reserva == null)
            {
                return HttpNotFound();
            }

            // VALIDACIÓN DE TITULARIDAD
            string userId = User.Identity.GetUserId();
            bool esCounter = User.IsInRole("Counter");
            if (!esCounter && reserva.UsuarioId != userId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "No tiene permisos para gestionar esta reserva.");
            }

            var primerTramo = reserva.ReservaPasajeroVueloes.FirstOrDefault();
            vw_VueloDetalle vuelo = null;
            var asientosDisponibles = new List<vw_EstadoAsientoVuelo>();

            if (primerTramo != null)
            {
                vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == primerTramo.VueloId);
                asientosDisponibles = db.vw_EstadoAsientoVuelo
                    .Where(a => a.VueloId == primerTramo.VueloId && a.EstadoVisual == "DISPONIBLE")
                    .OrderBy(a => a.NumeroAsiento)
                    .ToList();
            }

            ViewBag.Vuelo = vuelo;
            ViewBag.AsientosDisponibles = asientosDisponibles;
            ViewBag.EsCounter = esCounter;

            return View(reserva);
        }

        // POST: /Reservas/CambiarAsiento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarAsiento(int asignacionId, byte nuevoAsiento)
        {
            if (!EsCliente)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
            var asignacion = db.ReservaPasajeroVueloes
                .Include(a => a.Reserva)
                .FirstOrDefault(a => a.AsignacionId == asignacionId);

            if (asignacion == null)
            {
                return RedirectToAction("MisReservas");
            }

            // VALIDACIÓN DE TITULARIDAD
            string userId = User.Identity.GetUserId();
            if (asignacion.Reserva.UsuarioId != userId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            bool ocupado = db.ReservaPasajeroVueloes.Any(a =>
                a.VueloId == asignacion.VueloId &&
                a.NumeroAsiento == nuevoAsiento &&
                (a.EstadoAsignacionCodigo == "PENDIENTE" || a.EstadoAsignacionCodigo == "CONFIRMADA"));

            if (ocupado)
            {
                TempData["MensajeGestion"] = "El asiento seleccionado ya no está disponible.";
            }
            else
            {
                asignacion.NumeroAsiento = nuevoAsiento;
                db.SaveChanges();
                TempData["MensajeGestion"] = "Asiento actualizado correctamente al asiento #" + nuevoAsiento + ".";
            }

            return RedirectToAction("Gestionar", new { id = asignacion.ReservaId });
        }

        // POST: /Reservas/CancelarReserva
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelarReserva(int reservaId)
        {
            if (!EsCliente)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
            var reserva = db.Reservas
                .Include(r => r.ReservaPasajeroVueloes)
                .FirstOrDefault(r => r.ReservaId == reservaId);

            if (reserva != null && reserva.EstadoReservaCodigo != "CANCELADA")
            {
                // VALIDACIÓN DE TITULARIDAD
                string userId = User.Identity.GetUserId();
                if (reserva.UsuarioId != userId)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
                }

                reserva.EstadoReservaCodigo = "CANCELADA";
                foreach (var asig in reserva.ReservaPasajeroVueloes)
                {
                    asig.EstadoAsignacionCodigo = "CANCELADA";
                }
                db.SaveChanges();
                TempData["MensajeGestion"] = "La reserva " + reserva.CodigoReserva + " ha sido cancelada y sus asientos fueron liberados.";
            }

            return RedirectToAction("Gestionar", new { id = reservaId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}