using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize(Roles = "SuperUsuario,Administrador")]
    public class AeronavesController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // GET: Aeronaves
        public ActionResult Index()
        {
            var aeronaves = db.Aeronaves.Include(a => a.EstadoAeronave).Include(a => a.ModeloAeronave);
            return View(aeronaves.ToList());
        }

        // GET: Aeronaves/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeronave aeronave = db.Aeronaves.Find(id);
            if (aeronave == null)
            {
                return HttpNotFound();
            }
            return View(aeronave);
        }

        // GET: Aeronaves/Create
        public ActionResult Create()
        {
            return RedirectToAction("Aeronaves", "Admin");
        }

        // POST: Aeronaves/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "AeronaveId,Matricula,ModeloAeronaveId,EstadoAeronaveCodigo")] Aeronave aeronave)
        {
            if (aeronave == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            aeronave.Matricula = (aeronave.Matricula ?? "").Trim().ToUpperInvariant();
            bool modeloValido = db.ModeloAeronaves.Any(m => m.ModeloAeronaveId == aeronave.ModeloAeronaveId && m.CapacidadAsientos > 0 && m.CapacidadAsientos <= 7);
            if (!System.Text.RegularExpressions.Regex.IsMatch(aeronave.Matricula, @"^[A-Z0-9-]{3,15}$") ||
                db.Aeronaves.Any(a => a.Matricula == aeronave.Matricula) || !modeloValido)
            {
                TempData["ErrorAdmin"] = "La matrícula debe ser única y el modelo debe tener capacidad de hasta 7 asientos.";
                return RedirectToAction("Aeronaves", "Admin");
            }

            db.Aeronaves.Add(new Aeronave
            {
                Matricula = aeronave.Matricula,
                ModeloAeronaveId = aeronave.ModeloAeronaveId,
                EstadoAeronaveCodigo = "DISPONIBLE"
            });
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Aeronave creada correctamente.";
            return RedirectToAction("Aeronaves", "Admin");
        }

        // GET: Aeronaves/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeronave aeronave = db.Aeronaves.Find(id);
            if (aeronave == null)
            {
                return HttpNotFound();
            }
            ViewBag.EstadoAeronaveCodigo = new SelectList(db.EstadoAeronaves, "Codigo", "Descripcion", aeronave.EstadoAeronaveCodigo);
            ViewBag.ModeloAeronaveId = new SelectList(db.ModeloAeronaves, "ModeloAeronaveId", "Nombre", aeronave.ModeloAeronaveId);
            return View(aeronave);
        }

        // POST: Aeronaves/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "AeronaveId,Matricula,ModeloAeronaveId,EstadoAeronaveCodigo")] Aeronave aeronave)
        {
            if (aeronave == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var actual = db.Aeronaves.Find(aeronave.AeronaveId);
            if (actual == null)
            {
                return HttpNotFound();
            }
            aeronave.Matricula = (aeronave.Matricula ?? "").Trim().ToUpperInvariant();
            bool matriculaValida = System.Text.RegularExpressions.Regex.IsMatch(aeronave.Matricula, @"^[A-Z0-9-]{3,15}$") &&
                !db.Aeronaves.Any(a => a.AeronaveId != aeronave.AeronaveId && a.Matricula == aeronave.Matricula);
            var modelo = db.ModeloAeronaves.FirstOrDefault(m => m.ModeloAeronaveId == aeronave.ModeloAeronaveId && m.CapacidadAsientos > 0 && m.CapacidadAsientos <= 7);
            bool estadoValido = db.EstadoAeronaves.Any(e => e.Codigo == aeronave.EstadoAeronaveCodigo);
            int asignacionesActivas = db.ReservaPasajeroVueloes.Count(a => a.Vuelo.AeronaveId == aeronave.AeronaveId &&
                (a.EstadoAsignacionCodigo == "PENDIENTE" || a.EstadoAsignacionCodigo == "CONFIRMADA"));
            bool vuelosFuturos = db.Vueloes.Any(v => v.AeronaveId == aeronave.AeronaveId && v.FechaOperacion >= DateTime.Today);
            bool capacidadValida = modelo != null && modelo.CapacidadAsientos >= asignacionesActivas;
            if (aeronave.EstadoAeronaveCodigo == "NO_DISPONIBLE" && vuelosFuturos)
            {
                ModelState.AddModelError("EstadoAeronaveCodigo", "No puede retirar de servicio una aeronave asignada a vuelos futuros.");
            }
            if (matriculaValida && capacidadValida && estadoValido && ModelState.IsValid)
            {
                actual.Matricula = aeronave.Matricula;
                actual.ModeloAeronaveId = aeronave.ModeloAeronaveId;
                actual.EstadoAeronaveCodigo = aeronave.EstadoAeronaveCodigo;
                db.SaveChanges();
                TempData["MensajeAdmin"] = "Aeronave actualizada correctamente.";
                return RedirectToAction("Aeronaves", "Admin");
            }
            ViewBag.EstadoAeronaveCodigo = new SelectList(db.EstadoAeronaves, "Codigo", "Descripcion", aeronave.EstadoAeronaveCodigo);
            ViewBag.ModeloAeronaveId = new SelectList(db.ModeloAeronaves, "ModeloAeronaveId", "Nombre", aeronave.ModeloAeronaveId);
            ModelState.AddModelError("", "Revise matrícula, modelo, estado y capacidad de las reservas existentes.");
            return View(aeronave);
        }

        // GET: Aeronaves/Delete/5
        // REGLA CLAVE: Solo el SuperUsuario tiene el rol 'Delete'; el Administrador NO puede eliminar
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeronave aeronave = db.Aeronaves.Find(id);
            if (aeronave == null)
            {
                return HttpNotFound();
            }
            return View(aeronave);
        }

        // POST: Aeronaves/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult DeleteConfirmed(int id)
        {
            Aeronave aeronave = db.Aeronaves.Find(id);
            if (aeronave == null)
            {
                return HttpNotFound();
            }
            if (db.Vueloes.Any(v => v.AeronaveId == id))
            {
                TempData["ErrorAdmin"] = "No se puede eliminar una aeronave con vuelos asociados. Cámbiela a no disponible.";
                return RedirectToAction("Aeronaves", "Admin");
            }
            db.Aeronaves.Remove(aeronave);
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Aeronave eliminada.";
            return RedirectToAction("Aeronaves", "Admin");
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