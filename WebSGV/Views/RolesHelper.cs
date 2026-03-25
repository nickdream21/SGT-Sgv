using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Clase para gestionar roles y permisos del sistema SGV
    /// </summary>
    public static class RolesHelper
    {
        // Definición de roles
        public const string ROL_ADMIN = "ADMIN";
        public const string ROL_CONDUCTOR = "CONDUCTOR";
        public const string ROL_SUPERVISOR = "SUPERVISOR"; // Por si lo necesitas en el futuro
        public const string ROL_ADMIN_SISTEMA = "ADMINISTRADOR DE SISTEMA";

        /// <summary>
        /// Obtiene el rol del usuario actual desde la sesión
        /// </summary>
        public static string ObtenerRolActual()
        {
            if (HttpContext.Current.Session["Rol"] != null)
            {
                return HttpContext.Current.Session["Rol"].ToString().ToUpper();
            }
            return string.Empty;
        }

        /// <summary>
        /// Verifica si el usuario actual es Admin
        /// Acepta tanto "ADMIN" como "ADMINISTRADOR" o "ADMINISTRADOR DE SISTEMA"
        /// </summary>
        public static bool EsAdmin()
        {
            string rolActual = ObtenerRolActual();
            return rolActual == ROL_ADMIN || rolActual == "ADMINISTRADOR" || rolActual == ROL_ADMIN_SISTEMA;
        }

        /// <summary>
        /// Verifica si el usuario actual es Administrador de Sistema
        /// </summary>
        public static bool EsAdminSistema()
        {
            string rolActual = ObtenerRolActual();
            return rolActual == ROL_ADMIN_SISTEMA;
        }

        /// <summary>
        /// Verifica si el usuario actual es Conductor
        /// Acepta tanto "CONDUCTOR" como "CHOFER"
        /// </summary>
        public static bool EsConductor()
        {
            string rolActual = ObtenerRolActual();
            return rolActual == ROL_CONDUCTOR || rolActual == "CHOFER";
        }

        /// <summary>
        /// Verifica si el usuario tiene sesión activa
        /// </summary>
        public static bool TieneSesionActiva()
        {
            return HttpContext.Current.Session["UsuarioID"] != null;
        }

        /// <summary>
        /// Verifica si el usuario tiene permiso para acceder a una página
        /// </summary>
        public static bool TienePermiso(string seccion)
        {
            string rol = ObtenerRolActual();

            switch (seccion.ToUpper())
            {
                // Páginas accesibles solo para ADMIN
                case "DESPACHO":
                case "FACTURA":
                case "CPIC":
                case "ORDEN_VIAJE":
                case "ABASTECIMIENTO":
                case "REGISTRO":
                case "CONSULTAS":
                case "INDICADORES":
                    return EsAdmin() || rol == ROL_SUPERVISOR;

                // Auditoría: solo ADMINISTRADOR DE SISTEMA
                case "AUDITORIA":
                    return EsAdminSistema();

                // Páginas accesibles para CONDUCTOR
                case "DASHBOARD_CONDUCTOR":
                case "MIS_VIAJES":
                case "MI_PERFIL":
                    return EsConductor() || EsAdmin();

                default:
                    return false;
            }
        }

        /// <summary>
        /// Obtiene el nombre del usuario actual
        /// </summary>
        public static string ObtenerNombreUsuario()
        {
            if (HttpContext.Current.Session["Nombre"] != null)
            {
                return HttpContext.Current.Session["Nombre"].ToString();
            }
            return "Usuario";
        }

        /// <summary>
        /// Redirige al usuario a su página de inicio según su rol
        /// </summary>
        public static void RedirigirSegunRol()
        {
            if (!TieneSesionActiva())
            {
                HttpContext.Current.Response.Redirect("~/Views/Login.aspx");
                return;
            }

            if (EsConductor())
            {
                HttpContext.Current.Response.Redirect("~/Views/DashboardConductor.aspx");
            }
            else if (EsAdmin())
            {
                HttpContext.Current.Response.Redirect("~/Views/Inicio.aspx");
            }
            else
            {
                // Si no tiene rol reconocido, redirigir a login
                HttpContext.Current.Response.Redirect("~/Views/Login.aspx");
            }
        }

        /// <summary>
        /// Valida que el usuario tenga acceso a la página actual
        /// Si no tiene acceso, redirige según su rol
        /// </summary>
        public static void ValidarAccesoSeccion(string seccion)
        {
            if (!TieneSesionActiva())
            {
                HttpContext.Current.Response.Redirect("~/Views/Login.aspx");
                return;
            }

            if (!TienePermiso(seccion))
            {
                RedirigirSegunRol();
            }
        }
    }
}