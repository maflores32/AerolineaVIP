using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP.Models;
using Microsoft.AspNet.Identity;

namespace AerolineaVIP.Controllers
{
    [Authorize]
    public class ComprobantesController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 16: /Comprobantes/Index?codigoReserva=VIP-...
        // ============================================================
        // GET: /Comprobantes/Index
        [HttpGet]
        public ActionResult Index(string codigoReserva)
        {
            string userId = User.Identity.GetUserId();
            bool esPersonal = User.IsInRole("SuperUsuario") || User.IsInRole("Administrador") || User.IsInRole("Counter");

            var query = db.vw_ComprobanteDetalle.AsQueryable();

            if (!string.IsNullOrEmpty(codigoReserva))
            {
                codigoReserva = codigoReserva.Trim().ToUpper();
                query = query.Where(c => c.CodigoReserva == codigoReserva);
            }
            else if (!esPersonal)
            {
                // Consulta estándar limpia por UsuarioId del Identity
                query = query.Where(c => c.UsuarioId == userId);
            }

            var lista = query.OrderByDescending(c => c.FechaEmision).ToList();

            ViewBag.CodigoBuscado = codigoReserva;
            return View(lista);
        }

        // ============================================================
        // DETALLE IMPRIMIBLE / PDF DEL COMPROBANTE
        // ============================================================
        [HttpGet]
        public ActionResult Imprimir(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Index");
            }

            var comprobante = db.vw_ComprobanteDetalle.FirstOrDefault(c => c.ComprobanteId == id.Value);
            if (comprobante == null)
            {
                return HttpNotFound();
            }

            // Datos del emisor académico
            var emisor = db.DatosEmisors.FirstOrDefault();

            // Pasajeros y asientos de la reserva
            var asignaciones = db.ReservaPasajeroVueloes
                .Include(a => a.Pasajero)
                .Include(a => a.Vuelo)
                .Where(a => a.ReservaId == comprobante.ReservaId)
                .ToList();

            ViewBag.Emisor = emisor;
            ViewBag.Asignaciones = asignaciones;

            return View(comprobante);
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