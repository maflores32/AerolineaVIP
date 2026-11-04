using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    public class VuelosController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 5: /Vuelos/Buscar
        // ============================================================
        [AllowAnonymous]
        public ActionResult Buscar(VueloSearchFilter filtro)
        {
            filtro = filtro ?? new VueloSearchFilter();
            filtro.NormalizarFiltros();
            CargarCombosBusqueda(filtro.Origen, filtro.Destino, filtro.Fecha, filtro.Pasajeros);
            return View();
        }

        // ============================================================
        // PANTALLA 6: /Vuelos/Resultados
        // ============================================================
        [AllowAnonymous]
        public ActionResult Resultados(VueloSearchFilter filtro)
        {
            filtro = filtro ?? new VueloSearchFilter();
            filtro.NormalizarFiltros();
            CargarCombosBusqueda(filtro.Origen, filtro.Destino, filtro.Fecha, filtro.Pasajeros);

            foreach (var error in filtro.Validar(db))
            {
                foreach (var member in error.MemberNames)
                {
                    ModelState.AddModelError(member, error.ErrorMessage);
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ErrorBusqueda = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                ViewBag.DisponibilidadDict = new Dictionary<int, vw_DisponibilidadVuelo>();
                ViewBag.Pasajeros = filtro.Pasajeros;
                return View(new List<vw_VueloDetalle>());
            }

            Dictionary<int, vw_DisponibilidadVuelo> disponibilidad;
            var vuelos = filtro.Buscar(db, out disponibilidad);
            ViewBag.DisponibilidadDict = disponibilidad;
            ViewBag.Pasajeros = filtro.Pasajeros;
            return View(vuelos);
        }

        // ============================================================
        // PANTALLA 7: /Vuelos/Disponibilidad/{id}
        // ============================================================
        [AllowAnonymous]
        public ActionResult Disponibilidad(int? id, int? pasajeros)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Buscar");
            }

            int vueloId = id.Value;
            int numPasajeros = pasajeros ?? 1;
            if (numPasajeros < 1 || numPasajeros > 7)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }
            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId);
            if (vuelo == null)
            {
                return HttpNotFound();
            }
            if ((vuelo.EstadoVueloCodigo != "PROGRAMADO" && vuelo.EstadoVueloCodigo != "CONFIRMADO") || vuelo.FechaOperacion.Date < DateTime.Today)
            {
                return HttpNotFound();
            }

            var disponibilidad = db.vw_DisponibilidadVuelo.FirstOrDefault(d => d.VueloId == vueloId);
            var asientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId)
                .OrderBy(a => a.Fila)
                .ThenBy(a => a.Lado)
                .ToList();

            ViewBag.Disponibilidad = disponibilidad;
            ViewBag.Asientos = asientos;
            ViewBag.Pasajeros = numPasajeros;

            return View(vuelo);
        }

        private void CargarCombosBusqueda(string origen, string destino, DateTime? fecha, int? pasajeros)
        {
            var aeropuertos = db.Aeropuertoes
                .Where(a => a.Activo)
                .OrderBy(a => a.Localidad)
                .ToList()
                .Select(a => new
                {
                    Codigo = a.CodigoIATA,
                    Texto = a.Localidad + " (" + a.CodigoIATA + ") - " + a.Nombre
                })
                .ToList();

            ViewBag.Origen = new SelectList(aeropuertos, "Codigo", "Texto", origen);
            ViewBag.Destino = new SelectList(aeropuertos, "Codigo", "Texto", destino);
            ViewBag.fecha = fecha.HasValue ? fecha.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.pasajeros = pasajeros ?? 1;
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