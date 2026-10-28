using AerolineaVIP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace AerolineaVIP.Controllers
{
    public class HomeController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        public ActionResult Index()
        {
            var origenes = db.Aeropuertoes
                .Where(a => a.Activo)
                .OrderBy(a => a.Localidad)
                .ToList()
                .Select(a => new SelectListItem
                {
                    Value = a.CodigoIATA,
                    Text = a.Localidad + " (" + a.CodigoIATA + ")",
                    Selected = a.CodigoIATA == "UIO"
                })
                .ToList();

            var modelo = new HomeViewModel
            {
                Origenes = origenes,
                Destinos = ObtenerDestinos("UIO"),
                Pasajeros = 1
            };

            return View(modelo);
        }

        [HttpGet]
        public JsonResult Destinos(string origen)
        {
            var lista = ObtenerDestinos(origen)
                .Select(d => new { codigo = d.Value, texto = d.Text })
                .ToList();

            return Json(lista, JsonRequestBehavior.AllowGet);
        }

        private List<SelectListItem> ObtenerDestinos(string origen)
        {
            var resultado = new List<SelectListItem>();
            var aeropuertos = db.Aeropuertoes.Where(a => a.Activo).ToList();
            var aeropuertoOrigen = aeropuertos.FirstOrDefault(a => a.CodigoIATA == origen);

            if (aeropuertoOrigen == null)
            {
                return resultado;
            }

            var idsDestino = db.Rutas
                .Where(r => r.Activa && r.AeropuertoOrigenId == aeropuertoOrigen.AeropuertoId)
                .Select(r => r.AeropuertoDestinoId)
                .ToList();

            var codigos = aeropuertos
                .Where(a => idsDestino.Contains(a.AeropuertoId))
                .Select(a => a.CodigoIATA)
                .ToList();

            if (origen == "UIO" && !codigos.Contains("GPS"))
            {
                codigos.Add("GPS");
            }

            if (origen == "GPS" && !codigos.Contains("UIO"))
            {
                codigos.Add("UIO");
            }

            foreach (var a in aeropuertos.Where(x => codigos.Contains(x.CodigoIATA)).OrderBy(x => x.Localidad))
            {
                bool conEscala = (origen == "UIO" && a.CodigoIATA == "GPS") || (origen == "GPS" && a.CodigoIATA == "UIO");

                resultado.Add(new SelectListItem
                {
                    Value = a.CodigoIATA,
                    Text = a.Localidad + " (" + a.CodigoIATA + ")" + (conEscala ? " · escala en Guayaquil" : "")
                });
            }

            return resultado;
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