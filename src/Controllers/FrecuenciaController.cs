using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize(Roles = "SuperUsuario, Administrador")]
    public class FrecuenciaController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // GET: Frecuencia
        public ActionResult Index()
        {
            var frecuencias = db.Frecuencias.Include(f => f.Ruta.Aeropuerto).Include(f => f.Ruta.Aeropuerto1)
                .OrderBy(f => f.RutaId).ThenBy(f => f.DiaSemana);
            return View(frecuencias.ToList());
        }

        // GET: Frecuencia/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Frecuencia frecuencia = db.Frecuencias.Include(f => f.Ruta.Aeropuerto).Include(f => f.Ruta.Aeropuerto1)
                .FirstOrDefault(f => f.FrecuenciaId == id.Value);
            if (frecuencia == null)
            {
                return HttpNotFound();
            }
            return View(frecuencia);
        }

        // GET: Frecuencia/Create
        public ActionResult Create()
        {
            CargarRutas();
            return View();
        }

        // POST: Frecuencia/Create
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "FrecuenciaId,RutaId,DiaSemana,HoraSalida,HoraLlegada,Activa")] Frecuencia frecuencia)
        {
            if (frecuencia == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            AgregarErroresDominio(CatalogDomainRules.ValidateFrecuencia(db, frecuencia));
            if (ModelState.IsValid)
            {
                frecuencia.Activa = true;
                db.Frecuencias.Add(frecuencia);
                db.SaveChanges();
                TempData["MensajeAdmin"] = "Frecuencia creada correctamente.";
                return RedirectToAction("Index");
            }

            CargarRutas(frecuencia.RutaId);
            return View(frecuencia);
        }

        // GET: Frecuencia/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Frecuencia frecuencia = db.Frecuencias.Include(f => f.Ruta.Aeropuerto).Include(f => f.Ruta.Aeropuerto1).FirstOrDefault(f => f.FrecuenciaId == id.Value);
            if (frecuencia == null)
            {
                return HttpNotFound();
            }
            CargarRutas(frecuencia.RutaId);
            return View(frecuencia);
        }

        // POST: Frecuencia/Edit/5
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "FrecuenciaId,RutaId,DiaSemana,HoraSalida,HoraLlegada,Activa")] Frecuencia frecuencia)
        {
            if (frecuencia == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var actual = db.Frecuencias.Find(frecuencia.FrecuenciaId);
            if (actual == null)
            {
                return HttpNotFound();
            }
            var errorEdicion = CatalogDomainRules.ValidarEdicionFrecuencia(db, actual, frecuencia);
            if (errorEdicion != null)
            {
                ModelState.AddModelError("", errorEdicion);
            }
            AgregarErroresDominio(CatalogDomainRules.ValidateFrecuencia(db, frecuencia));
            if (ModelState.IsValid)
            {
                actual.RutaId = frecuencia.RutaId;
                actual.DiaSemana = frecuencia.DiaSemana;
                actual.HoraSalida = frecuencia.HoraSalida;
                actual.HoraLlegada = frecuencia.HoraLlegada;
                actual.Activa = frecuencia.Activa;
                db.SaveChanges();
                TempData["MensajeAdmin"] = "Frecuencia actualizada correctamente.";
                return RedirectToAction("Index");
            }
            CargarRutas(frecuencia.RutaId);
            return View(frecuencia);
        }

        // GET: Frecuencia/Delete/5
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Frecuencia frecuencia = db.Frecuencias.Find(id);
            if (frecuencia == null)
            {
                return HttpNotFound();
            }
            return View(frecuencia);
        }

        // POST: Frecuencia/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult DeleteConfirmed(int id)
        {
            Frecuencia frecuencia = db.Frecuencias.Find(id);
            if (frecuencia == null)
            {
                return HttpNotFound();
            }
            if (!CatalogDomainRules.PuedeEliminarFrecuencia(db, id))
            {
                TempData["ErrorAdmin"] = "No se puede eliminar una frecuencia con vuelos generados. Desactívela.";
                return RedirectToAction("Index");
            }
            db.Frecuencias.Remove(frecuencia);
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Frecuencia eliminada.";
            return RedirectToAction("Index");
        }

        private void CargarRutas(int? rutaId = null)
        {
            var rutas = db.Rutas.Include(r => r.Aeropuerto).Include(r => r.Aeropuerto1)
                .Where(r => r.Activa && r.Aeropuerto.Activo && r.Aeropuerto1.Activo)
                .OrderBy(r => r.Aeropuerto.CodigoIATA).ToList()
                .Select(r => new SelectListItem
                {
                    Value = r.RutaId.ToString(),
                    Text = r.Aeropuerto.CodigoIATA + " → " + r.Aeropuerto1.CodigoIATA,
                    Selected = rutaId.HasValue && r.RutaId == rutaId.Value
                }).ToList();
            ViewBag.RutaId = rutas;
        }

        private void AgregarErroresDominio(IEnumerable<DomainValidationError> errores)
        {
            foreach (var error in errores)
            {
                ModelState.AddModelError(error.PropertyName, error.Message);
            }
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
