using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace WebSGV
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Ignorar todas las páginas WebForms en la carpeta Views
            routes.IgnoreRoute("Views/{*pathInfo}");
        }
    }
}