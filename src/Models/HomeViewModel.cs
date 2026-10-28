using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace AerolineaVIP.Models
{
    public class HomeViewModel
    {
        public IEnumerable<SelectListItem> Origenes { get; set; }
        public IEnumerable<SelectListItem> Destinos { get; set; }
        public int Pasajeros { get; set; }
    }
}